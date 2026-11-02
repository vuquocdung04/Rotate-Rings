// Soft drop shadow, two sources:
// - SDF on: _MainTex alpha is a distance field (RingExtra_SDF: 1 inside, 0.5 at the edge, fading out);
//   the shadow edge and softness are thresholds on it.
// - SDF off: the ring atlas alpha sampled at a higher mip (needs mipmaps on the texture).
// The material's tiling/offset maps PathMesh's UVs onto the atlas (RingExtra: (0.5, 2) / (0, -1)).
Shader "RotateRings/RingShadow"
{
    Properties
    {
        _MainTex ("Ring Atlas / SDF", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.35, 0.25, 0.15, 0.3)
        [Toggle] _UseSDF ("Alpha Is SDF", Float) = 0
        _Edge ("SDF Edge (0.5 = body outline, lower = bigger)", Range(0, 1)) = 0.42
        _SdfSoftness ("SDF Softness", Range(0.001, 0.5)) = 0.14
        _Softness ("Softness (mip bias, non-SDF)", Range(0, 6)) = 2.5
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
                half _UseSDF, _Edge, _SdfSoftness, _Softness;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a;
                if (_UseSDF > 0.5)
                {
                    half d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                    a = smoothstep(_Edge - _SdfSoftness, _Edge + _SdfSoftness, d);
                }
                else
                    a = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, i.uv, _Softness).a;
                return half4(_ShadowColor.rgb, a * _ShadowColor.a);
            }
            ENDHLSL
        }
    }
}
