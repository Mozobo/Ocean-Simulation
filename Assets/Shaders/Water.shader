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
// https://docs.unity3d.com/Manual/SL-SurfaceShaderTessellation.html


// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Water"
{
    Properties
    {
        [Header(General parameters)]
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Glossiness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Shininess ("Shininess", Float) = 10

        [Header(Tesselation parameters)]
        _LODScale("LOD_scale", Range(1,10)) = 0

        [Header(Reflection parameters)]
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 1
        _SubsurfaceScatteringColor ("Subsurface Scattering Color", Color) = (1,1,1,1)
        _SubsurfaceScatteringIntensity ("Subsurface Scattering Strength", Range(0, 1)) = 0.25

        [Header(Refraction parameters)]
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.5
		_WaterFogDensity ("Water Fog Density", Range(0, 1)) = 0.1

    }
    SubShader
    {

        Tags { "Queue"="Transparent" "RenderType"="Transparent"}
        LOD 200

        GrabPass { "_WaterBackground" }

        CGPROGRAM
        // Physically based Standard lighting model
        #pragma surface surf Standard alpha vertex:vert finalcolor:ResetAlpha
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        // Variables with value provided by the engine
        sampler2D _CameraDepthTexture, _WaterBackground;
        float4 _CameraDepthTexture_TexelSize;

        struct Input
        {
            float4 screenPos;
            float3 viewDir;
            float3 worldPos;
            float2 worldUV;
            float3 worldNormal; INTERNAL_DATA
            //float lodC0;
        };

        // Variables with value provided by us (Through code or through Unity's interface)
        half _Glossiness;
        half _Metallic;
        float _Shininess;
        float _WaterFogDensity;
        float _RefractionStrength;
        float _ReflectionStrength;
        float _SubsurfaceScatteringIntensity;
        fixed4 _Color;
        fixed4 _SubsurfaceScatteringColor;
        fixed4 _FoamColor;
        float _LODScale;

        int _NbCascades;
        UNITY_DECLARE_TEX2DARRAY(_DisplacementsTextures);
        UNITY_DECLARE_TEX2DARRAY(_DerivativesTextures);
        UNITY_DECLARE_TEX2DARRAY(_TurbulenceTextures);
        uniform float _WaveLengths [5];

        float3 Refraction (float4 screenPos, float3 tangentSpaceNormal) {
            float2 uvOffset = tangentSpaceNormal.xy * _RefractionStrength;
            uvOffset.y *= _CameraDepthTexture_TexelSize.z * abs(_CameraDepthTexture_TexelSize.y);
	        float2 uv = (screenPos.xy + uvOffset) / screenPos.w;

            #if UNITY_UV_STARTS_AT_TOP
                if (_CameraDepthTexture_TexelSize.y < 0) {
                    uv.y = 1 - uv.y;
                }
            #endif

            float backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(screenPos.z);
            float depthDifference = backgroundDepth - surfaceDepth;

            if (depthDifference < 0) {
                uv = screenPos.xy / screenPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                    if (_CameraDepthTexture_TexelSize.y < 0) {
                        uv.y = 1 - uv.y;
                    }
                #endif
                backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
                depthDifference = backgroundDepth - surfaceDepth;
            }
            
            float3 backgroundColor = tex2D(_WaterBackground, uv).rgb;
            float fogFactor = exp2(-_WaterFogDensity * depthDifference);
	        return lerp(_Color, backgroundColor, fogFactor);
        }

        float4 Reflections (Input IN, float3 surfaceNormal) {
            // Reflections
            float3 reflectionDir = reflect(-IN.viewDir, surfaceNormal);
            float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectionDir);
            half3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
            half reflectionFactor = dot(IN.viewDir, surfaceNormal);
            float3 environmentReflections = skyColor * (1 - reflectionFactor);

            float attenuation;
            float3 lightDirection;
 
            if (0.0 == _WorldSpaceLightPos0.w) {  // directional light?
               attenuation = 1.0; // no attenuation
               lightDirection = normalize(_WorldSpaceLightPos0.xyz);
            } else { // point or spot light
               float3 vertexToLightSource = _WorldSpaceLightPos0.xyz - IN.worldPos.xyz;
               float distance = length(vertexToLightSource);
               attenuation = 1.0 / distance; // linear attenuation 
               lightDirection = normalize(vertexToLightSource);
            }

            float lightAngle = dot(surfaceNormal, lightDirection);
            float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color;
            
            /*
            float3 H = normalize(surfaceNormal + _WorldSpaceLightPos0);
            float ViewDotH = pow(saturate(dot(IN.viewDir, -H)), 5) * 30 * _SubsurfaceScatteringIntensity;
            float3 subsurfaceScattering = attenuation * _SubsurfaceScatteringColor * ViewDotH ;
            */

            float3 specularReflection;
            if (lightAngle < 0.0) { // light source on the wrong side?
               specularReflection = float3(0.0, 0.0, 0.0); // no specular reflection
            } else { // light source on the right side
               specularReflection = attenuation * _LightColor0.rgb * pow(max(0.0, dot(reflect(-lightDirection, surfaceNormal), IN.viewDir)), _Shininess);
            }

            return float4((specularReflection + environmentReflections) * _ReflectionStrength, 1.0);
        }

        void ResetAlpha (Input IN, SurfaceOutputStandard o, inout fixed4 color) {
			color.a = 1;
		}

        void vert(inout appdata_full vertexData, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 worldPos = mul(unity_ObjectToWorld, vertexData.vertex);
            float4 worldUV = float4(worldPos.xz, 0, 0);
            o.worldUV = worldUV.xy;

            /*float viewDist = length(_WorldSpaceCameraPos.xyz - worldPos);
            float lodC0 = min(_LODScale * _C0LengthScale / viewDist, 1);
            o.lodC0 = lodC0;*/

            float3 displacement = 0;
            // displacement += tex2Dlod(_DisplacementsC0Sampler, worldUV / _C0LengthScale) * lodC0;
            //displacement += tex2Dlod(_DisplacementsC0Sampler, worldUV / _C0LengthScale);
            for (int i = 0; i < _NbCascades; i++) {
                displacement += UNITY_SAMPLE_TEX2DARRAY_LOD(_DisplacementsTextures, float3(worldUV.xy / _WaveLengths[i], i), 0);
            }
            vertexData.vertex.xyz += mul(unity_WorldToObject, displacement);
        }

        float3 WorldToTangentNormalVector(Input IN, float3 normal) {
            float3 t2w0 = WorldNormalVector(IN, float3(1, 0, 0));
            float3 t2w1 = WorldNormalVector(IN, float3(0, 1, 0));
            float3 t2w2 = WorldNormalVector(IN, float3(0, 0, 1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;

            float4 derivatives = 0;
            // derivatives += tex2D(_DerivativesC0Sampler, IN.worldUV / _C0LengthScale) * IN.lodC0;
            //derivatives += tex2D(_DerivativesC0Sampler, IN.worldUV / _C0LengthScale);
            for (int i = 0; i < _NbCascades; i++) {
                derivatives += UNITY_SAMPLE_TEX2DARRAY(_DerivativesTextures, float3(IN.worldUV / _WaveLengths[i], i));
            }

            float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
            float3 worldNormal = normalize(float3(-slope.x, 1, -slope.y));
            o.Normal = WorldToTangentNormalVector(IN, worldNormal);

            o.Emission = Refraction(IN.screenPos, o.Normal) + Reflections(IN, o.Normal);
        }
        ENDCG
    }
}
