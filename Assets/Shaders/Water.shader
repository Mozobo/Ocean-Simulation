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

    }

    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"}
        LOD 200

        Pass {
            
            CGPROGRAM
            #pragma target 5.0 // 5.0 for tesselation

            #pragma vertex Vertex
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Fragment
            
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "Tessellation.cginc"

            #define M_PI 3.1415926535897932384626433832795f
            #define FLT_MIN 1.175494351e-38
            #define WATER_REFRACTION_INDEX 1.333f
            #define AIR_REFRACTION_INDEX 1

            // Variables with value provided by the engine
            sampler2D _CameraOpaqueTexture, _CameraDepthTexture;
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
            fixed4 _Color;
            fixed4 _FoamColor;

            float _LODScale;
            float _MaxTesselationDistance;

            int _NbCascades;
            UNITY_DECLARE_TEX2DARRAY(_DisplacementsTextures);
            UNITY_DECLARE_TEX2DARRAY(_DerivativesTextures);
            UNITY_DECLARE_TEX2DARRAY(_TurbulenceTextures);
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

                float backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
                float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(grabPos.z);
                float depthDifference = backgroundDepth - surfaceDepth;

                if (depthDifference < 0) {
                    uv = grabPos.xy / grabPos.w;
                    #if UNITY_UV_STARTS_AT_TOP
                        if (_CameraOpaqueTexture_TexelSize.y < 0) {
                            uv.y = 1 - uv.y;
                        }
                    #endif
                    backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
                    depthDifference = backgroundDepth - surfaceDepth;
                }
                
                float3 backgroundColor = tex2D(_CameraOpaqueTexture, uv).rgb;
                float fogFactor = exp2(-_WaterFogDensity * depthDifference);
                return lerp(_Color, backgroundColor, fogFactor);
            }

            float NormalDistribution(float3 normal, float3 viewDir) {
                float alpha = _Roughness * _Roughness;
                float alphaSquare = alpha * alpha;

                float3 halfwayDir = normalize(_WorldSpaceLightPos0 + viewDir);

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
                float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, viewDir);
                half3 environment = DecodeHDR(skyData, unity_SpecCube0_HDR);

                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);

                float3 H = normalize(worldPos.y + lightDirection);
                float ViewDotH = pow(saturate(dot(viewDir, -H)), 5) * 30 * _SubsurfaceScatteringIntensity;
                float3 scatter = _Color * _LightColor0 * ViewDotH;

                float normalDistribution = NormalDistribution(normal, viewDir);
                float geometryFunction = GeometryShadowingFunction(normal, viewDir, lightDirection);

                // https://rtarun9.github.io/blogs/physically_based_rendering/#what-is-physically-based-rendering
                float3 specular = _LightColor0 * (normalDistribution * geometryFunction) / max(4.0f * saturate(dot(viewDir, normal)) * saturate(dot(lightDirection, normal)), FLT_MIN);

                return (environment + scatter + specular) * _ReflectionStrength;
            }

            TessellationControlPoint Vertex(VertexData vertex) {
                TessellationControlPoint output;
                output.worldPos = mul(unity_ObjectToWorld, vertex.position);
                return output;
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
                    displacement += UNITY_SAMPLE_TEX2DARRAY_LOD(_DisplacementsTextures, float3(output.worldUV / _WaveLengths[i], i), 0);
                }
                output.worldPos += mul(unity_ObjectToWorld, displacement);

                output.screenPos = UnityObjectToClipPos(output.worldPos);
                output.grabPos = ComputeGrabScreenPos(output.screenPos);
                output.viewDir = normalize(_WorldSpaceCameraPos - output.worldPos);

                return output;
            }

            fixed4 Fragment(Vertex2FragmentData input) : SV_Target {
                float4 derivatives = 0;
                for (int i = 0; i < _NbCascades; i++) {
                    derivatives += UNITY_SAMPLE_TEX2DARRAY(_DerivativesTextures, float3(input.worldUV / _WaveLengths[i], i));
                }

                float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
                float3 objectNormal = normalize(float3(-slope.x, 1, -slope.y));
                float3 worldNormal = UnityObjectToWorldNormal(objectNormal);

                float R0 = pow((AIR_REFRACTION_INDEX - WATER_REFRACTION_INDEX) / (AIR_REFRACTION_INDEX + WATER_REFRACTION_INDEX), 2);
                float fresnel = R0 + (1 - R0) * pow(1.0 - saturate(dot(worldNormal, input.viewDir)), 5);

                float3 refraction = Refraction(input.grabPos, worldNormal);
                float3 reflection = Reflections(input.viewDir, input.worldPos, worldNormal);
                float3 emission = lerp(refraction, reflection, fresnel);

                return fixed4(emission, 1.0f);
            }
            
            ENDCG
        }
    }
}
