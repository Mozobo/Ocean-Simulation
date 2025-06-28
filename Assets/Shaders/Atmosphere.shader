Shader "Custom/Atmosphere"
{
    Properties {
        _SunSize ("Sun Size", Range(0,1)) = 0.04
        _SunIntensity("Sun Intensity", Range(0, 10)) = 2
    }
    SubShader {
        Tags { 
            "Queue" = "Background" 
            "RenderType" = "Background" 
            "PreviewType" = "Skybox"
        }
        Cull Off ZWrite Off
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 positionWS : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };
            
            uniform half _SunSize;
            uniform half _SunIntensity;

            TEXTURE2D(_SkyViewLUT);
            SAMPLER(sampler_SkyViewLUT);
            TEXTURE2D(_TransmittanceLUT);
            SAMPLER(sampler_TransmittanceLUT);

            float4 SampleSkyLUT(float3 rayDir) {
                rayDir = normalize(rayDir);
            
                float azimuthAngle = atan2(rayDir.x, rayDir.z); // [-π, π]
                float altitudeAngle = asin(rayDir.y); // [-π/2, π/2]
            
                float2 uv = float2(
                    (azimuthAngle + PI) / (2.0 * PI),
                    0.5 + 0.5 * sign(altitudeAngle) * sqrt(abs(altitudeAngle) * 2.0/PI)
                );

                return SAMPLE_TEXTURE2D(_SkyViewLUT, sampler_SkyViewLUT, uv);
            }

            float4 SampleTransmittanceLUT(float3 sunDir) {
                sunDir = normalize(sunDir);

                float3 up = float3(0.0, 1.0, 0.0);
                float sunCosZenithAngle = dot(sunDir, up);
                float2 uv = float2(
                    0,
                    0.5 + 0.5 * sign(sunCosZenithAngle) * sqrt(abs(sunCosZenithAngle) * 2.0/PI)
                );
                return SAMPLE_TEXTURE2D(_TransmittanceLUT, sampler_TransmittanceLUT, uv);
            }

            // Calculates the sun shape
            // Code source: https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/DefaultResourcesExtra/Skybox-Procedural.shader#L346
            half SunIntensity(half3 lightPos, half3 ray) {
                if (ray.y <= 0.0) return 0;
                half3 delta = lightPos - ray;
                half dist = length(delta);
                half spot = 1.0 - smoothstep(0.0, _SunSize, dist);
                return spot * spot * _SunIntensity;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.positionWS = mul(unity_ObjectToWorld, v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.viewDir = normalize(o.positionWS - _WorldSpaceCameraPos);
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 sunColor = SunIntensity(_MainLightPosition, i.viewDir) * _MainLightColor * max(0.5, SampleTransmittanceLUT(_MainLightPosition));
                float4 skyColor = SampleSkyLUT(i.viewDir) * _SunIntensity;

                return sunColor + skyColor;
            }
            ENDHLSL
        }
    }
}
