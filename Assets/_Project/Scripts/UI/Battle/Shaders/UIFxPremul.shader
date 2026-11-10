// 전투 입자 하나뿐인 머티리얼 (FX-JUICE §3.1): 미리 곱한 알파(premultiplied) 블렌드라 같은 머티리얼로
// 보통 알파 입자와 가산(빛) 입자가 함께 그려지고 배치도 끊기지 않는다.
//   - BattleFx가 정점 색에 rgb × 불투명도(o), a = o × (1 − Add)를 쓴다 → Add 0 = 보통 알파, Add 1 = 순수 빛(더하기)
//   - 블렌드 One OneMinusSrcAlpha: 결과 = src.rgb + dst × (1 − src.a)
//   - RectMask2D(전장 가장자리)에 잘리도록 UNITY_UI_CLIP_RECT — 미리 곱한 값이라 rgb와 a를 함께 자른다
//   - 가산 픽셀은 알파가 0에 가까워 UNITY_UI_ALPHACLIP을 쓰지 않는다 (쓰면 빛이 사라진다)
//   - 그림 텍스처는 보통 알파(빛 그림은 검정→알파로 뽑은 흰 빛)라 셰이더가 tex.rgb × tex.a로 미리 곱한다
//   - 플레이어 빌드에서 빠지지 않도록 씬 빌더가 머티리얼(Sprites/Battle/UIFxPremul.mat)을 만들어 BattleFx에 직렬화한다
Shader "IdleGame/UI/FxPremul"
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
        Blend One OneMinusSrcAlpha
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
                // 정점 rgb는 이미 불투명도(o)를 곱한 값, 정점 a는 알파 블렌드 몫 (0 = 순수 빛)
                c.rgb = tex.rgb * tex.a * IN.color.rgb;
                c.a = tex.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                c *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return c;
            }
        ENDCG
        }
    }
}
