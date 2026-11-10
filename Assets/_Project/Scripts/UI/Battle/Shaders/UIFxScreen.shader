// 전장 빛 번쩍임 (FX-JUICE §3.1 / §2.3): 스크린 블렌드 — 결과 = src + dst × (1 − src).
// 알파로 우유빛 막을 덮는 대신 밝힌다 (어두운 곳은 많이, 이미 밝은 곳은 조금 — 하얗게 날아가지 않는다).
//   - BattleFx가 Image.color = (빛 색 rgb, 세기 a)로 쓴다 → 출력 rgb = tex.rgb × tex.a × color.rgb × color.a, a = 0
//   - 블렌드 OneMinusDstColor One
//   - RectMask2D(전장 가장자리)에 잘리도록 UNITY_UI_CLIP_RECT (빛이라 rgb만 자르면 된다)
//   - 빛 번쩍임을 시작할 때만 BattleFx가 FieldFlash / BattleScreenFlash 이미지의 머티리얼을 이것으로 바꾼다 (틴트·어둡게는 기본 UI 머티리얼)
Shader "IdleGame/UI/FxScreen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
        Blend OneMinusDstColor One
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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half4 c;
                c.rgb = saturate(tex.rgb * tex.a * IN.color.rgb * IN.color.a);
                c.a = 0;

                #ifdef UNITY_UI_CLIP_RECT
                c.rgb *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return c;
            }
        ENDCG
        }
    }
}
