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
        _MaxLODLevel("Max LOD Level", Range(0, 16)) = 8

        [Header(Tesselation parameters)]
        _TesselationLevel("Tesselation Level", Range(1,100)) = 10
        _MaxTesselationDistance("Max Tesselation Distance", Range(1, 10000)) = 250
        _TesselationDecayFactor("Decay Factor", Range(1, 10)) = 4
        _CullingTollerance("Culling tollerance", Range(1, 10)) = 6

        [Header(Reflection parameters)]
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 1
        _ReflectionCubemap ("Reflection Cubemap", Cube) = "" {}

        [Header(Refraction parameters)]
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.5
		_WaterFogDensity ("Water Fog Density", Range(0, 1)) = 0.1

        [Header(Subsurface scattering parameters)]
        _SubsurfaceScatteringIntensity ("Subsurface Scattering Intensity", Range(0, 1)) = 0.5
        _SubsurfaceScatteringColor ("Scatter color", Color) = (0, 0, 0, 1)

        [Header(Shadows parameters)]
        _ShadowsColor ("Color of the shadows", Color) = (0, 0, 0, 1)
        _ShadowsIntensity ("Shadows Strength", Range(0, 1)) = 0.25

        [Header(Foam parameters)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.5
        _FoamBlending ("Foam Blending", Range(0, 1)) = 0.5

        [Header(Ashikhmin Shirley BRDF parameters)]
        _SpecularTerm ("Specular weight", Range(0, 1)) = 1
        _EX ("E X", Range(0, 1)) = 0.25
        _EY ("E Y", Range(0, 1)) = 0.25
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
                float3 positionOS : POSITION; // Object system
            };

            struct TessellationControlPoint {
                float3 positionWS : INTERNALTESSPOS; // World System
                float4 positionCS : SV_POSITION;
            };

            struct TessellationFactors {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct Vertex2FragmentData {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 worldUV : TEXCOORD2;
                float4 positionSS: TEXCOORD3; // texture coordinate for sampling a GrabPass texure
                float lodLevel: TEXCOORD4;
            };

            // Variables with value provided by us (Through code or through Unity's interface)
            half _Roughness;
            float _MaxLODLevel;
            float _WaterFogDensity;
            float _RefractionStrength;
            float _ReflectionStrength;
            float4 _Color;

            float4 _ShadowsColor;
            float _ShadowsIntensity;

            float _SubsurfaceScatteringIntensity;
            float4 _SubsurfaceScatteringColor;

            float _TesselationLevel;
            float _MaxTesselationDistance;
            float _TesselationDecayFactor;
            float _CullingTollerance;

            // Ashikhmin Shirley BRDF
            float _SpecularTerm;
            float _EX;
            float _EY;

            float3 _FoamColor;
            float _FoamThreshold;
            float _FoamBlending;

            TEXTURECUBE(_ReflectionCubemap);
            SAMPLER(sampler_ReflectionCubemap);

            int _NbCascades;
            TEXTURE2D_ARRAY(_DisplacementsTextures);
            SAMPLER(sampler_DisplacementsTextures);
            TEXTURE2D_ARRAY(_DerivativesTextures);
            SAMPLER(sampler_DerivativesTextures);
            TEXTURE2D_ARRAY(_TurbulenceTextures);
            SAMPLER(sampler_TurbulenceTextures);
            uniform float _WaveLengths [5];
            
            // For correct refractions, in the URP pipeline asset you have to enable both 'Depth Texture' and 'Opaque Texture'
            // Also set the Depth Priming Mode to Forced or the main camera will not render the scene correctly
            float3 UnderwaterView(float4 positionSS, float3 normalWS) {
                float2 uvOffset = normalWS.xy * _RefractionStrength;
                uvOffset.y *= _CameraOpaqueTexture_TexelSize.z * abs(_CameraOpaqueTexture_TexelSize.y);
                float2 uv = (positionSS.xy + uvOffset) / positionSS.w;

                #if UNITY_UV_STARTS_AT_TOP
                    if (_CameraOpaqueTexture_TexelSize.y < 0) {
                        uv.y = 1 - uv.y;
                    }
                #endif

                float backgroundDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r, _ZBufferParams);
                float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(positionSS.z);
                float depthDifference = backgroundDepth - surfaceDepth;

                if (depthDifference < 0) {
                    uv = positionSS.xy / positionSS.w;
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

            float NormalDistribution(float3 h, float3 normalWS, float3 viewDir, float roughness) {
                float alpha = roughness * roughness;
                float alphaSquare = alpha * alpha;
                float nDotH = saturate(dot(normalWS, h));
                
                return alphaSquare / (max(M_PI * pow((nDotH * nDotH * (alphaSquare - 1.0f) + 1.0f), 2.0f), FLT_MIN));
            }

            float SchlickBeckmannGS(float3 normalWS, float3 x, float roughness) {
                float k = roughness / 2.0f;
                float nDotX = saturate(dot(normalWS, x));
                
                return nDotX / (max((nDotX * (1.0f - k) + k), FLT_MIN));
            }

            float GeometryShadowingFunction(float3 normalWS, float3 viewDir, float3 lightDir, float roughness) {
                return SchlickBeckmannGS(normalWS, viewDir, roughness) * SchlickBeckmannGS(normalWS, lightDir, roughness);    
            }

            float3 EnvironmentReflections (float3 viewDir, float3 normalWS) {
                //float3 reflectionDir = viewDir.zyx;
                float3 reflectionDir = normalWS;
                float3 environment = SAMPLE_TEXTURECUBE(_ReflectionCubemap, sampler_ReflectionCubemap, reflectionDir);
                return environment * M_PI * _ReflectionStrength;
            }

            // https://www.researchgate.net/publication/2523875_An_anisotropic_phong_BRDF_model
            float3 AshikhminShirleyBRDF(float3 h, float3 viewDir, float3 lightDir, float3 normalWS, float fresnel, float ex, float ey) {
                if (dot(lightDir, float3(0.0, 1.0, 0.0)) <= 0.0) return 0.0;
                float cos2PhiH = max((h.x * h.x) / max(1.0 - h.z * h.z, FLT_MIN), 0.0);
                float sin2PhiH = max((h.y * h.y) / max(1.0 - h.z * h.z, FLT_MIN), 0.0);
                float d = sqrt((ex + 1) * (ey + 1)) * pow(max(dot(h, normalWS), 0.0), ex * cos2PhiH + ey * sin2PhiH);

                float specular = max(d * fresnel / max(4 * M_PI * dot(h, viewDir) * max(dot(normalWS, viewDir), dot(normalWS, lightDir)), FLT_MIN), 0.0);

                //float diffuse = max((28 / (23 * M_PI)) * (1 - fresnel) * (1 - pow(1 - 0.5 * dot(n, l), 5)) * (1 - pow(1 - 0.5 * dot(n, v), 5)), 0.0);

                return _MainLightColor * specular;
            }

            float3 CookTorranceBRDF(float3 h, float3 normalWS, float3 viewDir, float3 lightDir, float fresnel, float roughness) {
                if (dot(lightDir, float3(0.0, 1.0, 0.0)) <= 0.0) return 0.0;
                float normalDistribution = max(NormalDistribution(h, normalWS, viewDir, roughness), 0.0);
                float geometryFunction = max(GeometryShadowingFunction(normalWS, viewDir, lightDir, roughness), 0.0);

                // https://rtarun9.github.io/blogs/physically_based_rendering/#what-is-physically-based-rendering
                return _MainLightColor * normalDistribution * geometryFunction / max(4.0f * saturate(dot(viewDir, normalWS)) * saturate(dot(lightDir, normalWS)), FLT_MIN);
            }

            float3 SubsurfaceScatteringApproximation(float waveHeight, float3 lightDir, float3 viewDir) {
                float coeff = _SubsurfaceScatteringIntensity * max(0, waveHeight) * pow(max(0, dot(lightDir, viewDir)), 4);
                return coeff * _SubsurfaceScatteringColor * _MainLightColor;
                // return coeff * _Color * _MainLightColor;
            }

            TessellationControlPoint Vertex(VertexData input) {
                TessellationControlPoint output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS);
                output.positionWS = posInputs.positionWS;
                output.positionCS = posInputs.positionCS;
                return output;
            }

            float DistanceBasedTessFactor (float3 positionWS, float minDist, float maxDist, float tess) {
                float dist = distance (positionWS.xyz, _WorldSpaceCameraPos);
                float normalizedDist = saturate((dist - minDist) / (maxDist - minDist));
                float decayFactor = exp(-_TesselationDecayFactor * normalizedDist);
                float f = saturate(decayFactor) * tess;
                return f;
            }

            // https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e
            // Returns true if the point is outside the bounds set by lower and higher
            bool IsOutOfBounds(float3 p, float3 lower, float3 higher) {
                return p.x < lower.x || p.x > higher.x || p.y < lower.y || p.y > higher.y || p.z < lower.z || p.z > higher.z;
            }

            // https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e
            // Returns true if the given vertex is outside the camera fustum and should be culled
            bool IsPointOutOfFrustum(float4 positionCS) {
                float3 culling = positionCS.xyz;
                float w = positionCS.w;
                // UNITY_RAW_FAR_CLIP_VALUE is either 0 or 1, depending on graphics API
                // Most use 0, however OpenGL uses 1
                float3 lowerBounds = float3(-w - _CullingTollerance, -w - _CullingTollerance, -w * UNITY_RAW_FAR_CLIP_VALUE - _CullingTollerance);
                float3 higherBounds = float3(w + _CullingTollerance, w + _CullingTollerance, w + _CullingTollerance);
                return IsOutOfBounds(culling, lowerBounds, higherBounds);
            }

            // https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e
            // Returns true if it should be clipped due to frustum or winding culling
            bool ShouldClipPatch(float4 p0PositionCS, float4 p1PositionCS, float4 p2PositionCS) {
                bool allOutside = IsPointOutOfFrustum(p0PositionCS) &&
                    IsPointOutOfFrustum(p1PositionCS) &&
                    IsPointOutOfFrustum(p2PositionCS);
                return allOutside;
            }

            // The patch constant function runs once per triangle, or "patch"
            // It runs in parallel to the hull function
            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) {
                // Calculate tessellation factors
                TessellationFactors f;
                if (ShouldClipPatch(patch[0].positionCS, patch[1].positionCS, patch[2].positionCS)) {
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0; // Cull the patch
                } else {
                    float3 edgePosition0 = 0.5 * (patch[1].positionWS + patch[2].positionWS);
                    float3 edgePosition1 = 0.5 * (patch[0].positionWS + patch[2].positionWS);
                    float3 edgePosition2 = 0.5 * (patch[0].positionWS + patch[1].positionWS);

                    f.edge[0] = DistanceBasedTessFactor(edgePosition0, 1, _MaxTesselationDistance, _TesselationLevel);
                    f.edge[1] = DistanceBasedTessFactor(edgePosition1, 1, _MaxTesselationDistance, _TesselationLevel);
                    f.edge[2] = DistanceBasedTessFactor(edgePosition2, 1, _MaxTesselationDistance, _TesselationLevel);
                    f.inside = (f.edge[0] + f.edge[1] + f.edge[2]) / 3.0;
                }
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
                output.positionWS = BARYCENTRIC_INTERPOLATE(positionWS);
                output.worldUV = output.positionWS.xz;

                float lodFactor = distance(output.positionWS, _WorldSpaceCameraPos) / _MaxTesselationDistance;
                output.lodLevel = lerp(0.0, _MaxLODLevel, lodFactor);

                float3 displacement = 0;
                for (int i = 0; i < _NbCascades; i++) {
                    displacement += SAMPLE_TEXTURE2D_ARRAY_LOD(_DisplacementsTextures, sampler_DisplacementsTextures, output.worldUV / _WaveLengths[i], i, output.lodLevel);
                }
                output.positionWS += mul(unity_ObjectToWorld, displacement);

                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.positionSS = ComputeScreenPos(output.positionCS);
                output.viewDir = normalize(_WorldSpaceCameraPos - output.positionWS);

                return output;
            }

            float4 Fragment(Vertex2FragmentData input) : SV_Target {
                float4 derivatives = 0;
                for (int i = 0; i < _NbCascades; i++) {
                    derivatives += SAMPLE_TEXTURE2D_ARRAY_LOD(_DerivativesTextures, sampler_DerivativesTextures, input.worldUV / _WaveLengths[i], i, input.lodLevel);
                }

                float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
                float3 normalOS = normalize(float3(-slope.x, 1, -slope.y));
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));

                float turbulence = 0;
                for (int i = 0; i < _NbCascades; i++) {
                    turbulence += 1 - saturate(SAMPLE_TEXTURE2D_ARRAY_LOD(_TurbulenceTextures, sampler_TurbulenceTextures, input.worldUV / _WaveLengths[i], i, input.lodLevel).x);
                }

                float3 lightDirection = normalize(_MainLightPosition);
                float3 H = normalize(input.viewDir + lightDirection);

                float R0 = pow((AIR_REFRACTION_INDEX - WATER_REFRACTION_INDEX) / (AIR_REFRACTION_INDEX + WATER_REFRACTION_INDEX), 2);
                float fresnel = R0 + (1 - R0) * pow(1.0 - saturate(dot(normalWS, input.viewDir)), 5 * exp(-2.69*_Roughness)) / (1 + 22.7 * pow(_Roughness, 1.5));
                float fresnelH = R0 + (1 - R0) * pow(1.0 - saturate(dot(H, input.viewDir)), 5);

                // The shadow coords are computed in the fragment stage because if computed in the domain, the borders between shadow cascades appear as shadows
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS); 
                float shadowFactor = MainLightRealtimeShadow(shadowCoord);

                float3 refraction = UnderwaterView(input.positionSS, normalWS);
                refraction += SubsurfaceScatteringApproximation(input.positionWS.y, lightDirection, -input.viewDir);

                float3 reflections = EnvironmentReflections(input.viewDir, normalWS);
                
                float dynamicRoughness = lerp(_Roughness, _Roughness * 1.5, pow(1.0 - abs(normalWS.y), 2.0));
                float nu = _EX * 100.0 * (1.0 - dynamicRoughness); // Controls anisotropy along x-axis
                float nv = _EY * 10.0 * (1.0 - dynamicRoughness);  // Controls anisotropy along z-axis
                float3 ashikhminShirleySpec = AshikhminShirleyBRDF(H, input.viewDir, lightDirection, normalWS, fresnelH, nu, nv);
                float3 cookTorranceSpec = CookTorranceBRDF(H, normalWS, input.viewDir, lightDirection, fresnelH, dynamicRoughness);

                // Blending factor based on view angle, adding Ashikhmin-Shirley at flatter angles
                reflections += (cookTorranceSpec + ashikhminShirleySpec * saturate(dot(input.viewDir, normalWS))) * shadowFactor;

                float3 emission = lerp(lerp(refraction, reflections, fresnel), _ShadowsColor, _ShadowsIntensity * (1 - shadowFactor));
                if (turbulence >= _FoamThreshold) emission = lerp(emission, _FoamColor, _FoamBlending);

                return float4(emission, 1.0f);
            }
            
            ENDHLSL
        }
    }
}
