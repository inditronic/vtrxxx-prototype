Shader "Ghostclub/FloorReflection"
{
    Properties
    {
        _ReflectionTex ("Reflection Texture", 2D) = "black" {}
        _BumpMap ("Floor Normal Map", 2D) = "bump" {}
        _ReflectionIntensity ("Reflection Intensity", Range(0.0, 1.0)) = 0.55
        _Distortion ("Normal Distortion", Range(0.0, 0.08)) = 0.012
        _FresnelPower ("Fresnel Power", Range(0.5, 6.0)) = 1.8
        _BaseTint ("Floor Dark Tint", Color) = (0.0, 0.0, 0.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        LOD 100
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _ReflectionTex;
            sampler2D _BumpMap;
            float4 _BumpMap_ST;
            float _ReflectionIntensity;
            float _Distortion;
            float _FresnelPower;
            fixed4 _BaseTint;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BumpMap);
                o.screenPos = ComputeScreenPos(o.pos);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / max(0.0001, i.screenPos.w);

                // Flip Y because the mirrored camera below Y=0 has inverted vertical pitch symmetry
                float2 reflUV = float2(screenUV.x, 1.0 - screenUV.y);

                // Sample concrete micro-normal ripple for realistic polished club floor reflection
                half3 normalTS = UnpackNormal(tex2D(_BumpMap, i.uv));
                reflUV += normalTS.xy * _Distortion;
                reflUV = clamp(reflUV, 0.002, 0.998);

                // Multi-tap slight blur for realistic polished concrete gloss
                fixed4 refl0 = tex2D(_ReflectionTex, reflUV);
                fixed4 refl1 = tex2D(_ReflectionTex, clamp(reflUV + float2(0.0025, 0.0015), 0.002, 0.998));
                fixed4 refl2 = tex2D(_ReflectionTex, clamp(reflUV - float2(0.0025, 0.0015), 0.002, 0.998));
                fixed4 reflCol = (refl0 * 0.5 + refl1 * 0.25 + refl2 * 0.25);

                // View-dependent Fresnel factor (stronger reflection at grazing angles, still visible looking down)
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float ndotv = saturate(abs(viewDir.y));
                float fresnel = lerp(1.0, 0.38, pow(ndotv, _FresnelPower));

                float3 finalRGB = _BaseTint.rgb + reflCol.rgb * (_ReflectionIntensity * fresnel);
                return fixed4(finalRGB, 1.0);
            }
            ENDCG
        }
    }
}
