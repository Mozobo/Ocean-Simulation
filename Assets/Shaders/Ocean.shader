// https://unitywatershader.wordpress.com/
// https://catlikecoding.com/unity/tutorials/flow/looking-through-water/


// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Ocean"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _WaterFogColor ("Water Fog Color", Color) = (0, 0, 0, 0)
		_WaterFogDensity ("Water Fog Density", Range(0, 2)) = 0.1
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.25
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _FoamColor ("Foam Color", Color) = (0, 0, 0, 0)

        [Header(Cascade 0)]
        _DisplacementsC0Sampler("Displacements C0", 2D) = "black" {}
        [HideInInspector]_DerivativesC0Sampler("Derivatives C0", 2D) = "black" {}
        [HideInInspector]_TurbulenceC0Sampler("Turbulence C0", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            float2 uv_MainTex;
            float4 screenPos;
            float2 worldUV;
            float3 worldNormal; INTERNAL_DATA
        };

        // Variables with value provided by us (Through code or through Unity's interface)
        sampler2D _MainTex;

        half _Glossiness;
        half _Metallic;
        float3 _WaterFogColor;
        float _WaterFogDensity;
        float _RefractionStrength;
        fixed4 _Color;
        fixed4 _FoamColor;

        sampler2D _DisplacementsC0Sampler;
        sampler2D _DerivativesC0Sampler;
        sampler2D _TurbulenceC0Sampler;
        float _C0LengthScale;

        float3 ColorBelowWater (float4 screenPos, float3 tangentSpaceNormal) {
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
	        return lerp(_WaterFogColor, backgroundColor, fogFactor);
        }

        void ResetAlpha (Input IN, SurfaceOutputStandard o, inout fixed4 color) {
			color.a = 1;
		}

        void vert(inout appdata_full vertexData, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 worldPos = mul(unity_ObjectToWorld, vertexData.vertex);
            float4 worldUV = float4(worldPos.xz, 0, 0);
            o.worldUV = worldUV.xy;

            float3 displacement = 0;
            displacement += tex2Dlod(_DisplacementsC0Sampler, worldUV / _C0LengthScale);
            vertexData.vertex.xyz += mul(unity_WorldToObject,displacement);
        }

        float3 WorldToTangentNormalVector(Input IN, float3 normal) {
            float3 t2w0 = WorldNormalVector(IN, float3(1, 0, 0));
            float3 t2w1 = WorldNormalVector(IN, float3(0, 1, 0));
            float3 t2w2 = WorldNormalVector(IN, float3(0, 0, 1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = _Color;

            float4 derivatives = 0;
            derivatives += tex2D(_DerivativesC0Sampler, IN.worldUV / _C0LengthScale);

            float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
            float3 worldNormal = normalize(float3(-slope.x, 1, -slope.y));
            o.Normal = WorldToTangentNormalVector(IN, worldNormal);

            o.Alpha = c.a;

           	o.Emission = ColorBelowWater(IN.screenPos, o.Normal);
        }
        ENDCG
    }
}
