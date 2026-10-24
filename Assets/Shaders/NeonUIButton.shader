Shader "Drones/UI/Neon Button"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Graphic Tint", Color) = (1,1,1,1)
        _TopShade ("Top Highlight", Range(0, 2)) = 1.18
        _BottomShade ("Bottom Shade", Range(0, 2)) = 0.76
        _GlowColor ("Edge Glow", Color) = (0.2,0.8,1,1)
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.45
        _EdgeWidth ("Edge Width", Range(0.005, 0.12)) = 0.035
        _CornerRadius ("Corner Radius", Range(0.02, 0.5)) = 0.16
        _GlossStrength ("Moving Gloss", Range(0, 1)) = 0.16

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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _TopShade, _BottomShade, _GlowStrength, _EdgeWidth, _CornerRadius, _GlossStrength;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float roundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float radius = min(_CornerRadius, 0.49);
                float distance = roundedBox(p, float2(0.5 - radius, 0.5 - radius), radius);
                float shape = 1.0 - smoothstep(-0.008, 0.008, distance);
                float edge = 1.0 - smoothstep(_EdgeWidth * 0.45, _EdgeWidth * 1.8, abs(distance));
                float3 gradient = lerp(_TopShade.xxx, _BottomShade.xxx, saturate(i.uv.y));
                float sweepCenter = frac(_Time.y * 0.12) * 1.6 - 0.3;
                float gloss = pow(saturate(1.0 - abs(i.uv.x - sweepCenter) / 0.22), 8.0) * _GlossStrength;
                fixed4 sprite = tex2D(_MainTex, i.uv);
                fixed4 result;
                result.rgb = (gradient + gloss) * i.color.rgb + _GlowColor.rgb * edge * _GlowStrength;
                result.a = sprite.a * i.color.a * shape;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
