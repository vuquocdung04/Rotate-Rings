Shader "UIJuice/UI Shine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ShineColor ("Shine Color", Color) = (1,1,1,1)
        _ShineLocation ("Shine Location", Range(-1,2)) = -1
        _ShineWidth ("Shine Width", Range(0.01,0.5)) = 0.10
        _ShineSoftness ("Shine Softness", Range(0.001,0.3)) = 0.06
        _ShineGloss ("Shine Gloss", Range(1,10)) = 3.5
        _ShineAngle ("Shine Angle", Range(0,360)) = 45
        _ShineIntensity ("Shine Intensity", Range(0,5)) = 1
        _ShineTint ("Shine Tint From Sprite", Range(0,1)) = 0.35
        _ShineAspect ("Shine Aspect", Float) = 1

        [Toggle(SHINE_AUTO)] _ShineAuto ("Shine Auto Play", Float) = 1
        _ShineDuration ("Shine Duration", Range(0.05,5)) = 0.6
        _ShineInterval ("Shine Interval", Range(0,20)) = 2
        _ShineTimeOffset ("Shine Time Offset", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local_vertex SHINE_AUTO

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float4 uvShine  : TEXCOORD0;
                #ifdef UNITY_UI_CLIP_RECT
                float2 localPos : TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _ShineColor;
            half _ShineLocation;
            half _ShineWidth;
            half _ShineSoftness;
            half _ShineGloss;
            half _ShineAngle;
            half _ShineIntensity;
            half _ShineTint;
            half _ShineAspect;
            float _ShineDuration;
            float _ShineInterval;
            float _ShineTimeOffset;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color * _Color;

                float2 uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.uvShine.xy = uv;

                #ifdef UNITY_UI_CLIP_RECT
                OUT.localPos = v.vertex.xy;
                #endif

                float2 dir;
                sincos(_ShineAngle * (UNITY_PI / 180.0), dir.y, dir.x);
                dir.x *= _ShineAspect;

                float mn = min(dir.x, 0.0) + min(dir.y, 0.0);
                float mx = max(dir.x, 0.0) + max(dir.y, 0.0);
                float proj = (dot(uv, dir) - mn) / max(mx - mn, 1e-5);

                float location = _ShineLocation;
                float active = 1.0;

                #ifdef SHINE_AUTO
                float duration = max(_ShineDuration, 1e-3);
                float period = duration + max(_ShineInterval, 0.0);
                float k = fmod(_Time.y + _ShineTimeOffset, period) / duration;
                float edge = _ShineWidth + _ShineSoftness;
                location = lerp(-edge, 1.0 + edge, saturate(k));
                active = step(k, 1.0);
                #endif

                OUT.uvShine.z = proj - location;
                OUT.uvShine.w = active * _ShineIntensity * _ShineColor.a;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.uvShine.xy) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.localPos, _ClipRect);
                #endif

                half band = 1.0h - smoothstep(_ShineWidth - _ShineSoftness,
                                              _ShineWidth + _ShineSoftness,
                                              abs(IN.uvShine.z));
                band = pow(max(band, 1e-4h), _ShineGloss);

                half maxc = max(color.r, max(color.g, color.b));
                half3 hue = color.rgb / max(maxc, 1e-3h);
                half3 shineRGB = _ShineColor.rgb * lerp(1.0h, hue, saturate(maxc * 10.0h) * _ShineTint);

                color.rgb += shineRGB * (band * IN.uvShine.w * color.a);

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
