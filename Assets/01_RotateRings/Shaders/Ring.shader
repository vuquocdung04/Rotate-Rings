// Grey atlas used as a light/shade mask around the vertex color: texture == _MidGray shows the pure color.
// Optional normal-mapped highlight (only for meshes with tangents, i.e. the ring arc).
// The material's tiling/offset maps PathMesh's RingFinal layout onto other atlases (RingExtra: (0.5, 2) / (0, -1)).
// Tile Body repeats the body slice along the path (mirrored, a whole number of times) so grain isn't stretched.
Shader "RotateRings/Ring"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _Color ("Tint (set per renderer for sprites)", Color) = (1, 1, 1, 1)
        _MidGray ("Mid Gray (texture value = pure color)", Range(0.1, 1)) = 0.5
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 0.6
        _Specular ("Specular", Range(0, 1)) = 0.08
        _Gloss ("Gloss", Range(1, 128)) = 32
        _LightDir ("Light Dir (to light)", Vector) = (-0.4, 0.6, -1, 0)
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.02
        [Toggle] _BodyTiling ("Tile Body (wood / marble grain)", Float) = 0
        _TileThick ("Body Tile Length (x thickness)", Float) = 6
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 body : TEXCOORD1; // PathMesh: s / length, on body, length / thickness, closed
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float4 body : TEXCOORD4;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _LightDir;
                half4 _Color;
                half _MidGray, _ShadeStrength, _Specular, _Gloss, _AlphaClip;
                float _BodyTiling, _TileThick;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.uv = v.uv;
                o.color = v.color;
                o.normalWS = n.normalWS;
                o.tangentWS = n.tangentWS;
                o.bitangentWS = n.bitangentWS;
                o.body = v.body;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float2 dx = ddx(uv), dy = ddy(uv);

                // Body tiling: odd count on open paths (ends on the tail cap's side), even on closed ones (seamless).
                float tiles = max(1.0, i.body.z / max(0.01, _TileThick));
                tiles = i.body.w > 0.5 ? max(2.0, 2.0 * round(tiles * 0.5)) : 2.0 * floor(tiles * 0.5) + 1.0;
                float t = i.body.x * tiles;
                float pingPong = 1.0 - abs(frac(t * 0.5) * 2.0 - 1.0);
                if (_BodyTiling > 0.5 && i.body.y > 0.5)
                {
                    uv.x = lerp(0.125, 0.875, pingPong);
                    dx.x = ddx(t) * 0.75;
                    dy.x = ddy(t) * 0.75;
                }
                uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;
                dx *= _MainTex_ST.xy;
                dy *= _MainTex_ST.xy;

                half4 tex = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, dx, dy);
                clip(tex.a - _AlphaClip);

                half shade = lerp(1.0h, tex.r / _MidGray, _ShadeStrength);
                half3 col = i.color.rgb * _Color.rgb * shade;

                if (_Specular > 0.001h)
                {
                    half3 nTS = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv, dx, dy));
                    float3 nWS = normalize(TransformTangentToWorld(nTS, half3x3(i.tangentWS, i.bitangentWS, i.normalWS)));
                    float3 H = normalize(normalize(_LightDir.xyz) + float3(0, 0, -1));
                    col += pow(saturate(dot(nWS, H)), _Gloss) * _Specular;
                }
                return half4(col, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
