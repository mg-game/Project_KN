// 스테이지 기믹 덮개의 빛 구멍 (ATK-14): 기본 UI 셰이더와 같이 그리되, _Hole0~3 자리 둘레만 알파를 줄인다.
// 정전(Blackout) 스테이지에서 빛 특성 계보(몽당이·라라벨)가 명중하면 그 자리의 어둠(번짐·가장자리·덮개 그림)에 0.4초 동안 구멍이 열린다.
//   - _HoleN = (x, y, 반지름, 세기) — x, y, 반지름은 루트 캔버스 좌표 (RectMask2D의 _ClipRect와 같은 공간, 정점 위치 그대로).
//     세기 0이면 구멍 없음. 가운데 알파 × (1 - 세기), 반지름의 35%까지 그대로 → 반지름에서 원래대로 부드럽게.
//   - RectMask2D(전장 가장자리)에 잘리도록 UNITY_UI_CLIP_RECT + UnityGet2DClipping, Stencil 속성은 기본 UI 셰이더와 같다 (Mask 호환).
//   - 씬 빌더가 머티리얼(Sprites/Battle/UIAmbienceHole.mat)을 만들어 StageAmbienceView에 직렬화한다 (플레이어 빌드에서 빠지지 않게).
//     런타임은 그 사본을 덮개 이미지들에 씌우고 구멍 값만 바꾼다 (에셋은 건드리지 않는다).
Shader "IdleGame/UI/AmbienceHole"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Hole0 ("Hole 0 (xy, radius, strength)", Vector) = (0,0,0,0)
        _Hole1 ("Hole 1 (xy, radius, strength)", Vector) = (0,0,0,0)
        _Hole2 ("Hole 2 (xy, radius, strength)", Vector) = (0,0,0,0)
        _Hole3 ("Hole 3 (xy, radius, strength)", Vector) = (0,0,0,0)

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
            float4 _Hole0;
            float4 _Hole1;
            float4 _Hole2;
            float4 _Hole3;

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

            // 구멍 하나의 알파 배율 (세기 0이면 1). 반지름은 1 이상으로 막아 0으로 나누지 않는다
            half HoleMask(float2 p, float4 hole)
            {
                float radius = max(hole.z, 1.0);
                float d = distance(p, hole.xy);
                return 1.0 - saturate(hole.w) * (1.0 - smoothstep(radius * 0.35, radius, d));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float2 p = IN.worldPosition.xy;
                color.a *= HoleMask(p, _Hole0) * HoleMask(p, _Hole1) * HoleMask(p, _Hole2) * HoleMask(p, _Hole3);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
