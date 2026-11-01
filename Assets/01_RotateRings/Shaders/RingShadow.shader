// Soft drop shadow: the ring atlas alpha sampled at a higher mip (needs mipmaps on the texture).
Shader "RotateRings/RingShadow"
{
    Properties
    {
        _MainTex ("Ring Atlas", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.35, 0.25, 0.15, 0.3)
        _Softness ("Softness (mip bias)", Range(0, 6)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ShadowColor;
                half _Softness;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, i.uv, _Softness).a;
                return half4(_ShadowColor.rgb, a * _ShadowColor.a);
            }
            ENDHLSL
        }
    }
}
