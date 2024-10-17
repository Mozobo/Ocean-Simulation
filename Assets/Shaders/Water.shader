// References:
// https://unitywatershader.wordpress.com/
// https://catlikecoding.com/unity/tutorials/flow/looking-through-water/
// https://catlikecoding.com/unity/tutorials/rendering/part-8/
// https://github.com/usunyu/my-awesome-projects/blob/main/Shader/Unity%20HLSL%20Shader/Assets/Hawaii%20Environment/Water/Tasharen%20Water.shader
// https://github.com/leonjovanovic/water-shader-unity/blob/main/Assets/Shaders/WavesDistortion.shader
// https://en.wikibooks.org/wiki/Cg_Programming/Unity/Specular_Highlights
// https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-1/
// https://abyssal.eu/a-look-through-the-waters-surface/
// https://docs.unity3d.com/Manual/SL-SurfaceShaders.html
// https://docs.unity3d.com/Manual/SL-BuiltinFunctions.html
// https://rtarun9.github.io/blogs/physically_based_rendering/#what-is-physically-based-rendering
// https://en.wikipedia.org/wiki/Schlick's_approximation

// Tesselation
// https://docs.unity3d.com/Manual/SL-SurfaceShaderTessellation.html
// https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e
// https://www.youtube.com/watch?v=63ufydgBcIk
// https://catlikecoding.com/unity/tutorials/advanced-rendering/tessellation/

// Shadows
// https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/use-built-in-shader-methods-shadows.html
// https://www.youtube.com/watch?v=1bm0McKAh9E

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Water" {
    
    Properties {
        [Header(General parameters)]
        _Color ("Color", Color) = (1,1,1,1)
        _Roughness ("Roughness", Range(0,1)) = 0.5

        [Header(Tesselation parameters)]
        _LODScale("LOD_scale", Range(1,100)) = 10 // Tesselation factor
        _MaxTesselationDistance("Max Tesselation Distance", Range(1, 10000)) = 250

        [Header(Reflection parameters)]
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 1
        _SubsurfaceScatteringIntensity ("Subsurface Scattering Strength", Range(0, 1)) = 0.25

        [Header(Refraction parameters)]
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.5
		_WaterFogDensity ("Water Fog Density", Range(0, 1)) = 0.1

        [Header(Shadows parameters)]
        _ShadowsColor ("Color of the shadows", Color) = (0, 0, 0, 1)
        _ShadowsIntensity ("Shadows Strength", Range(0, 1)) = 0.25

    }

    SubShader {
        Tags { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalRenderPipeline"
        }
        LOD 200

        Pass {
            
            HLSLPROGRAM
            #pragma target 5.0 // 5.0 for tesselation

            #pragma vertex Vertex
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Fragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS 
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define M_PI 3.1415926535897932384626433832795f
            #define FLT_MIN 1.175494351e-38
            #define WATER_REFRACTION_INDEX 1.333f
            #define AIR_REFRACTION_INDEX 1.0f

            // Variables with value provided by the engine
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            float4 _CameraOpaqueTexture_TexelSize;

            struct VertexData {
                float4 position : POSITION; // Object system
            };

            struct TessellationControlPoint {
                float4 worldPos : INTERNALTESSPOS; // World System
            };

            struct TessellationFactors {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct Vertex2FragmentData {
                float4 screenPos : SV_Position;
                float3 viewDir : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 worldUV : TEXCOORD2;
                float4 grabPos: TEXCOORD3; // texture coordinate for sampling a GrabPass texure
            };

            // Variables with value provided by us (Through code or through Unity's interface)
            half _Roughness;
            float _WaterFogDensity;
            float _RefractionStrength;
            float _ReflectionStrength;
            float _SubsurfaceScatteringIntensity;
            float4 _Color;
            float4 _FoamColor;

            float4 _ShadowsColor;
            float _ShadowsIntensity;

            float _LODScale;
            float _MaxTesselationDistance;

            int _NbCascades;
            TEXTURE2D_ARRAY(_DisplacementsTextures);
            SAMPLER(sampler_DisplacementsTextures);
            TEXTURE2D_ARRAY(_DerivativesTextures);
            SAMPLER(sampler_DerivativesTextures);
            TEXTURE2D_ARRAY(_TurbulenceTextures);
            SAMPLER(sampler_TurbulenceTextures);
            uniform float _WaveLengths [5];
            
            // For correct refractions, in the URP pipeline asset you have to enable both 'Depth Texture' and 'Opaque Texture'
            float3 Refraction (float4 grabPos, float3 worldNormal) {
                float2 uvOffset = worldNormal.xy * _RefractionStrength;
                uvOffset.y *= _CameraOpaqueTexture_TexelSize.z * abs(_CameraOpaqueTexture_TexelSize.y);
                float2 uv = (grabPos.xy + uvOffset) / grabPos.w;

                #if UNITY_UV_STARTS_AT_TOP
                    if (_CameraOpaqueTexture_TexelSize.y < 0) {
                        uv.y = 1 - uv.y;
                    }
                #endif

                float backgroundDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r, _ZBufferParams);
                float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(grabPos.z);
                float depthDifference = backgroundDepth - surfaceDepth;

                if (depthDifference < 0) {
                    uv = grabPos.xy / grabPos.w;
                    #if UNITY_UV_STARTS_AT_TOP
                        if (_CameraOpaqueTexture_TexelSize.y < 0) {
                            uv.y = 1 - uv.y;
                        }
                    #endif
                    backgroundDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r, _ZBufferParams);
                    depthDifference = backgroundDepth - surfaceDepth;
                }
                
                float3 backgroundColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
                float fogFactor = exp2(-_WaterFogDensity * depthDifference);
                return lerp(_Color, backgroundColor, fogFactor);
            }

            float NormalDistribution(float3 normal, float3 viewDir) {
                float alpha = _Roughness * _Roughness;
                float alphaSquare = alpha * alpha;

                float3 halfwayDir = normalize(_MainLightPosition + viewDir);

                float nDotH = saturate(dot(normal, halfwayDir));
                
                return alphaSquare / (max(M_PI * pow((nDotH * nDotH * (alphaSquare - 1.0f) + 1.0f), 2.0f), FLT_MIN));
            }

            float SchlickBeckmannGS(float3 normal, float3 x) {
                float k = _Roughness / 2.0f;
                float nDotX = saturate(dot(normal, x));
                
                return nDotX / (max((nDotX * (1.0f - k) + k), FLT_MIN));
            }

            float GeometryShadowingFunction(float3 normal, float3 viewDir, float3 lightDir) {
                return SchlickBeckmannGS(normal, viewDir) * SchlickBeckmannGS(normal, lightDir);    
            }

            float3 Reflections (float3 viewDir, float3 worldPos, float3 normal) {
                float4 skyData = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, viewDir, 0.0f);
                half3 environment = skyData.rgb;

                float3 lightDirection = normalize(_MainLightPosition.xyz);

                float3 H = normalize(worldPos.y + lightDirection);
                float ViewDotH = pow(saturate(dot(viewDir, -H)), 5) * 30 * _SubsurfaceScatteringIntensity;
                float3 scatter = _Color * _MainLightColor * ViewDotH;

                float normalDistribution = NormalDistribution(normal, viewDir);
                float geometryFunction = GeometryShadowingFunction(normal, viewDir, lightDirection);

                // https://rtarun9.github.io/blogs/physically_based_rendering/#what-is-physically-based-rendering
                float3 specular = _MainLightColor * (normalDistribution * geometryFunction) / max(4.0f * saturate(dot(viewDir, normal)) * saturate(dot(lightDirection, normal)), FLT_MIN);

                return (environment + scatter + specular) * _ReflectionStrength;
            }

            TessellationControlPoint Vertex(VertexData vertex) {
                TessellationControlPoint output;
                output.worldPos = mul(unity_ObjectToWorld, vertex.position);
                return output;
            }

            float UnityCalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess) {
                float3 wpos = mul(unity_ObjectToWorld,vertex).xyz;
                float dist = distance (wpos, _WorldSpaceCameraPos);
                float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
                return f;
            }

            float4 UnityCalcTriEdgeTessFactors (float3 triVertexFactors) {
                float4 tess;
                tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
                tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
                tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
                tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
                return tess;
            }

            float4 UnityDistanceBasedTess (float4 v0, float4 v1, float4 v2, float minDist, float maxDist, float tess) {
                float3 f;
                f.x = UnityCalcDistanceTessFactor (v0,minDist,maxDist,tess);
                f.y = UnityCalcDistanceTessFactor (v1,minDist,maxDist,tess);
                f.z = UnityCalcDistanceTessFactor (v2,minDist,maxDist,tess);

                return UnityCalcTriEdgeTessFactors (f);
            }

            // The patch constant function runs once per triangle, or "patch"
            // It runs in parallel to the hull function
            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) {
                // Calculate tessellation factors
                float4 factors = UnityDistanceBasedTess(patch[0].worldPos, patch[1].worldPos, patch[2].worldPos, 1, _MaxTesselationDistance, _LODScale);
                TessellationFactors f;
                f.edge[0] = factors.x;
                f.edge[1] = factors.y;
                f.edge[2] = factors.z;
                f.inside = factors.w;
                return f;
            }

            [domain("tri")] // Signal we're inputting triangles
            [outputcontrolpoints(3)] // Triangles have three points
            [outputtopology("triangle_cw")] // Signal we're outputting triangles
            [patchconstantfunc("PatchConstantFunction")] // Register the patch constant function
            [partitioning("integer")] // Select a partitioning mode: integer, fractional_odd, fractional_even or pow2
            TessellationControlPoint Hull(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID) {
                return patch[id];
            }

            // Call this macro to interpolate between a triangle patch, passing the field name
            #define BARYCENTRIC_INTERPOLATE(fieldName) \
                    patch[0].fieldName * barycentricCoordinates.x + \
                    patch[1].fieldName * barycentricCoordinates.y + \
                    patch[2].fieldName * barycentricCoordinates.z

            [domain("tri")] // Signal we're inputting triangles
            // Params:
            // The output of the patch constant function
            // The Input triangle
            // The barycentric coordinates of the vertex on the triangle
            Vertex2FragmentData Domain(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation) {
                Vertex2FragmentData output;
                output.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                output.worldUV = output.worldPos.xz;

                float3 displacement = 0;
                for (int i = 0; i < _NbCascades; i++) {
                    displacement += SAMPLE_TEXTURE2D_ARRAY_LOD(_DisplacementsTextures, sampler_DisplacementsTextures, output.worldUV / _WaveLengths[i], i, 0);
                }
                output.worldPos += mul(unity_ObjectToWorld, displacement);

                output.screenPos = TransformObjectToHClip(output.worldPos);
                output.grabPos = ComputeScreenPos(output.screenPos);
                output.viewDir = normalize(_WorldSpaceCameraPos - output.worldPos);

                return output;
            }

            float4 Fragment(Vertex2FragmentData input) : SV_Target {
                float4 derivatives = 0;
                for (int i = 0; i < _NbCascades; i++) {
                    derivatives += SAMPLE_TEXTURE2D_ARRAY_LOD(_DerivativesTextures, sampler_DerivativesTextures, input.worldUV / _WaveLengths[i], i, 0);
                }

                float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
                float3 objectNormal = normalize(float3(-slope.x, 1, -slope.y));
                float3 worldNormal = TransformObjectToWorldNormal(objectNormal);

                float R0 = pow((AIR_REFRACTION_INDEX - WATER_REFRACTION_INDEX) / (AIR_REFRACTION_INDEX + WATER_REFRACTION_INDEX), 2);
                float fresnel = R0 + (1 - R0) * pow(1.0 - saturate(dot(worldNormal, input.viewDir)), 5);

                // The shadow coords are computed in the fragment stage because if computed in the domain, the borders between shadow cascades appear as shadows
                float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos); 
                float shadowFactor = MainLightRealtimeShadow(shadowCoord);

                float3 refraction = Refraction(input.grabPos, worldNormal);
                float3 reflection = Reflections(input.viewDir, input.worldPos, worldNormal) * shadowFactor;

                float3 emission = lerp(lerp(refraction, reflection, fresnel), _ShadowsColor, _ShadowsIntensity * (1 - shadowFactor));

                return float4(emission, 1.0f);
            }
            
            ENDHLSL
        }
    }
}
