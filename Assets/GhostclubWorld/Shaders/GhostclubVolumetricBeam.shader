Shader "Ghostclub/VolumetricBeam"
{
    Properties
    {
        _Color ("Beam Color", Color) = (0.2, 0.8, 1.0, 0.6)
        _FalloffPower ("Falloff Power", Range(0.5, 4.0)) = 1.8
        _NoiseSpeed ("Smoke Noise Speed", Float) = 0.5
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FalloffPower;
                float _NoiseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance along cone (Z in local space)
                float lengthFade = pow(saturate(1.0 - input.uv.y), _FalloffPower);
                
                // Fresnel / view-angle edge softness
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float rim = abs(dot(viewDir, normalize(input.normalWS)));
                float edgeSoftness = saturate(rim * 1.5);

                float alpha = lengthFade * edgeSoftness * input.color.a * _Color.a;
                half3 finalColor = _Color.rgb * input.color.rgb * 2.0;

                return half4(finalColor * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
