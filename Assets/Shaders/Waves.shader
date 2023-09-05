Shader "Custom/Waves"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _Gravity ("Gravity", Float) = 9.81
        _WaveA ("Wave A (dir, amplitude, wavelength)", Vector) = (1,0,0.5,10)
        _WaveB ("Wave B", Vector) = (0,1,0.25,20)
		_WaveC ("Wave C", Vector) = (1,1,0.15,10)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        #define PI 3.14159265359

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        float _Gravity;
        float4 _WaveA, _WaveB, _WaveC;

        
        float3 GerstnerWave (float4 wave, float3 p, inout float3 tangent, inout float3 binormal) {
		    float steepness = wave.z;
		    float wavelength = wave.w;
		    float frequency = 2 * PI / wavelength;
			float speed = sqrt(_Gravity / frequency);
			float2 direction = normalize(wave.xy);
			float f = frequency * (dot(direction, p.xz) - speed * _Time.y);
			float amplitude = steepness / frequency;

			tangent += float3(
				-direction.x * direction.x * (steepness * sin(f)),
				direction.x * (steepness * cos(f)),
				-direction.x * direction.y * (steepness * sin(f))
			);
			binormal += float3(
				-direction.x * direction.y * (steepness * sin(f)),
				direction.y * (steepness * cos(f)),
				-direction.y * direction.y * (steepness * sin(f))
			);

			return float3(
				direction.x * amplitude * cos(f),
				amplitude * exp(sin(f) - 1),
				direction.y * amplitude * cos(f)
			);
            
		}


        float3 calculateNormal(float3 tangent, float3 binormal) {
            return normalize(cross(binormal, tangent));
        }

        void vert(inout appdata_full vertexData) {
            float3 p = vertexData.vertex;
			float3 tangent = float3(1, 0, 0);
			float3 binormal = float3(0, 0, 1);
			p += GerstnerWave(_WaveA, p, tangent, binormal);
			p += GerstnerWave(_WaveB, p, tangent, binormal);
            p += GerstnerWave(_WaveC, p, tangent, binormal);
			vertexData.vertex.xyz = p;
			vertexData.normal = calculateNormal(tangent, binormal);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
