using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 떠오르는 숫자의 뜻 (FX-03 / FX-JUICE §4 피해 숫자 v2). 색만 봐도 무엇인지 알 수 있게 — 금색은 치명타 전용, 빨강은 수선점 피해 전용.
    /// 순서를 바꾸지 않는다 (시험장·설정이 값으로 쓴다).
    /// </summary>
    public enum DamageNumberKind : byte
    {
        /// <summary>일반 피해: 크림 46pt (대상별 0.12초 안의 명중은 하나로 합치고 톡 튄다)</summary>
        Normal,
        /// <summary>저격·돌격 강타: 크림→살구 53pt</summary>
        Heavy,
        /// <summary>치명타: 금 74pt 쾅 — 1.8배로 내리찍고 흔들리며 별 배지·광택 (합치지 않는다)</summary>
        Crit,
        /// <summary>보스가 받은 일반 피해: 크림→라벤더 55pt, 바깥으로 흘러간다 (금색 아님)</summary>
        Boss,
        /// <summary>표식으로 커진 피해: 분홍 46pt, 앞에 작은 하트 ("♥1,234" — 색만으로 뜻을 전하지 않게)</summary>
        Mark,
        /// <summary>지속 피해 (촛불·반딧불): 주황 39pt, 톡 튀지 않고 스며 나온다, 대상별 0.5초마다 합침</summary>
        Dot,
        /// <summary>회복: 민트 "+N" 41pt</summary>
        Heal,
        /// <summary>적 보호막이 막은 피해: 소프트 블루 "막음 N" 39pt + 가로 떨림</summary>
        Block,
        /// <summary>수선점 피해: Danger "-N" 46pt, 1.4배에서 줄며 가로 떨림 (문 아래)</summary>
        ShopDamage,
        /// <summary>스킬 피해: 55pt, 시전 계보 강조색으로 물든다 (금빛 강조색은 라벤더로 — 금색은 치명타 전용), 0.25초 합침</summary>
        Skill,
        /// <summary>불 밝히기 빛 물결 피해: 등불색 51pt (금색 아님), 0.5초 합침</summary>
        Lamp,
    }

    /// <summary>
    /// 피해 숫자 v2 (FX-JUICE §4): 진짜 굵은 글꼴 46pt + 잉크 외곽선·그림자(코어 머티리얼) + 뜻별 세로 그라데이션, 뜻별 팝 곡선(첫 프레임부터 크게 →
    /// 살짝 넘침 → 1.0), 치명타 쾅(1.8배 + 눌림 + 흔들림 + 별 배지 + 광택 훑기), 마지막 일격 +15% 반짝, 같은 대상 합치기(합칠 때마다 1.1 → 1.25배 톡),
    /// 품질별 화면 상한 24/16/10 (넘치면 뜻 순위가 낮은 것부터 — 치명타·수선점 피해는 늘 띄운다), 상단 HUD 아래로 자르기, 설정(모두 / 치명타만 / 끄기).
    /// 숫자는 명중 33ms 뒤에 뜬다 (빛이 먼저, 숫자가 다음 — 회복·수선점 피해·안내 글자는 바로). 값은 끝까지 실수 (후반 피해는 2.1B를 넘는다).
    /// 늦게 뜨는 숫자는 AnchorNumber로 적에 붙여 두면 뜨는 순간 그 적이 있는 자리에서 뜬다. 연속·한꺼번에 알림 글자도 이 풀의 비워 둔 2칸을 쓴다 (BattleFx.Callouts) —
    /// 숫자는 떠 있는 알림 판 밑에 자리 잡고, 알림이 뜰 때 판에 가릴 숫자는 판 위로 올린다.
    /// 매 프레임 할당 없음 (글자는 BattleEase 캐시·미리 만든 문자열), 사라짐은 묶음 알파 (글자 메시를 매 프레임 다시 짜지 않는다).
    /// </summary>
    public partial class BattleFx
    {
        struct FloatingNumber
        {
            public RectTransform Rect;
            public TMP_Text Text;
            /// <summary>
            /// 스며 나옴·사라짐 알파 (CanvasGroup — TMP alpha를 바꾸면 매 프레임 글자 메시를 통째로 다시 짠다.
            /// 굵은 글자는 Bold 글꼴의 하위 메시라 canvasRenderer 하나로는 안 되고, 묶음 알파는 하위 메시까지 함께 흐려진다)
            /// </summary>
            public CanvasGroup Group;
            /// <summary>마지막으로 쓴 묶음 알파 (같으면 쓰지 않는다 · 광택 세기)</summary>
            public float Alpha;
            /// <summary>마지막으로 그린 자리 · 배율 (겹침 비키기·광택이 RectTransform을 다시 읽지 않게)</summary>
            public Vector2 Drawn;
            public Vector2 DrawnScale;
            public bool Alive;
            /// <summary>뜨기까지 남은 연출 시간</summary>
            public float Delay;
            public float Age;
            /// <summary>움직임 기준 수명 (떠오름·흐름이 이때 끝난다)</summary>
            public float Life;
            /// <summary>사라지기 시작하는 나이 / 끝나는 나이 (합쳐질 때마다 늦춘다)</summary>
            public float FadeAt;
            public float End;
            /// <summary>뜨는 자리 (뜨는 순간 붙은 적이 옮겨 간 만큼 더하고 상단 HUD 아래로 자른다)</summary>
            public Vector2 Position;
            /// <summary>수명 동안 가로로 흘러가는 양 (수선점 바깥쪽)</summary>
            public float DriftX;
            /// <summary>뜻 크기 × 부른 쪽 배율 (× 마지막 일격 1.15)</summary>
            public float Scale;
            public float CallerScale;
            /// <summary>떠오르는 높이(px)</summary>
            public float Rise;
            /// <summary>뜬 직후 가로 떨림 폭(px, 막음 2 · 수선점 3)</summary>
            public float Shake;
            public DamageNumberKind Kind;
            public bool Crit;
            /// <summary>연속·한꺼번에 알림 글자 (합치지도 비우지도 않는다)</summary>
            public bool Callout;
            /// <summary>뜻 없는 안내 글자 (Text · Chip — 합치지 않는다)</summary>
            public bool Plain;
            public bool Shown;
            /// <summary>마지막 일격 숫자 (+15%, 뜰 때 반짝 셋)</summary>
            public bool KillBlow;
            /// <summary>불 밝히기 숫자 벽으로 줄인 숫자 (0.85배 · 수명 60%부터 사라짐 — 마지막 일격이 되면 되돌린다)</summary>
            public bool Thinned;
            /// <summary>합치기 대상 (NoMerge면 합치지 않는다)</summary>
            public int TargetId;
            public float Value;
            /// <summary>이 시각(연출 시계 _clock)까지 같은 대상·같은 뜻의 숫자는 여기에 더한다</summary>
            public float MergeUntil;
            /// <summary>이미 떠 있는 숫자에 늦게 닿을 명중 (PendingDelay 뒤에 더한다)</summary>
            public float PendingValue;
            public float PendingDelay;
            /// <summary>합쳐질 때 톡 튀는 세기 1 → 0 / 그 꼭대기 배율 (1.1 + 0.03 × 합친 수, 1.25까지)</summary>
            public float Repop;
            public float Bump;
            public int Merges;
            /// <summary>만든(또는 마지막으로 합친) 연출 시각 (마지막 일격은 0.3초 안의 숫자만)</summary>
            public float Spawned;
            public int Sequence;
            /// <summary>보이는 글자 수 (서식 태그 빼고 — 자리·겹침 폭, 광택 훑기 폭)</summary>
            public int Chars;
            /// <summary>치명타 광택 칸 / 알림 띠 칸 (없으면 −1)</summary>
            public int Gloss;
            public int Lane;
            /// <summary>뜨기 전까지 따라갈 적 (뜨는 순간 그만큼 옮기고 놓는다). 다른 개체로 재사용되면 마지막으로 본 자리</summary>
            public EnemyView Anchor;
            public int AnchorId;
            public Vector2 AnchorStart;
            public Vector2 AnchorOffset;
            /// <summary>치명타로 뜰 때 더할 높이: 붙은 적의 몸 가운데 → 머리 위 (AnchorNumber가 붙이는 순간 잰다, 안 붙었으면 0 — 한 번 쓰면 0)</summary>
            public float HeadLift;
        }

        /// <summary>뜻 하나의 움직임 (§4.2): 처음 크기 → 꼭대기(시간) → 1.0(시간) · 떠오름 px / 수명 s · 가로 떨림 px · 스며 나오는 시간</summary>
        readonly struct NumberMotion
        {
            public readonly float Start;
            public readonly float PeakTime;
            public readonly float Peak;
            public readonly float SettleTime;
            public readonly float Rise;
            public readonly float Life;
            public readonly float Shake;
            public readonly float FadeIn;

            public NumberMotion(float start, float peakTime, float peak, float settleTime, float rise, float life, float shake = 0f, float fadeIn = 0f)
            {
                Start = start;
                PeakTime = peakTime;
                Peak = peak;
                SettleTime = settleTime;
                Rise = rise;
                Life = life;
                Shake = shake;
                FadeIn = fadeIn;
            }
        }

        /// <summary>DamageNumberKind 순서. 치명타 줄은 수명·떠오름만 쓰고 크기는 CritSlamScale</summary>
        static readonly NumberMotion[] NumberMotions =
        {
            new(0.55f, 0.070f, 1.12f, 0.120f, 64f, 0.75f), // Normal: 0.55 → 70ms 1.12 → 120ms 1.0
            new(0.50f, 0.075f, 1.18f, 0.130f, 70f, 0.80f), // Heavy
            new(1.80f, 0.070f, 0.92f, 0.140f, 90f, 1.15f), // Crit (쾅)
            new(0.50f, 0.075f, 1.18f, 0.130f, 70f, 0.85f), // Boss (강타처럼, 바깥으로 흐른다)
            new(0.55f, 0.070f, 1.12f, 0.120f, 64f, 0.75f), // Mark
            new(1.00f, 0f, 1.00f, 0f, 40f, 0.70f, 0f, 0.08f), // Dot: 톡 없이 80ms에 스며 나온다
            new(0.70f, 0.080f, 1.05f, 0.130f, 70f, 0.85f), // Heal
            new(0.80f, 0.080f, 1.00f, 0.080f, 40f, 0.70f, 2f), // Block + ±2px 떨림
            new(1.40f, 0f, 1.40f, 0.100f, 40f, 0.80f, 3f), // ShopDamage: 1.4 → 100ms 1.0 + ±3px 떨림
            new(0.55f, 0.070f, 1.12f, 0.120f, 64f, 0.80f), // Skill
            new(0.60f, 0.070f, 1.12f, 0.120f, 70f, 0.85f), // Lamp
        };

        /// <summary>숫자 기본 글자 크기 (pt, 뜻 배율은 localScale) / 숫자 칸 크기</summary>
        const float NumberFontSize = 46f;
        static readonly Vector2 NumberRectSize = new(300f, 80f);
        const float MergeWindow = 0.12f;
        /// <summary>숫자가 8개를 넘거나 밀도가 8을 넘으면 합치기 창을 넓힌다</summary>
        const float BusyMergeWindow = 0.25f;
        const float DotMergeWindow = 0.5f;
        const float HealMergeWindow = 0.5f;
        const float SkillMergeWindow = 0.25f;
        const float LampMergeWindow = 0.5f;
        const float RepopTime = 0.16f;
        /// <summary>바깥으로 흐르는 양 / 같은 대상의 이어지는 숫자가 좌우로 엇갈리는 폭 / 보스</summary>
        const float NumberDrift = 14f;
        const float NumberAlternate = 16f;
        const float BossNumberDrift = 28f;
        /// <summary>숫자 가운데가 상단 안전선 아래 머무는 여백 (× 뜻 크기) / 떠오른 끝 여백 / 가로 여백</summary>
        const float NumberTopMargin = 26f;
        const float NumberRoofMargin = 10f;
        const float NumberSideMargin = 50f;
        /// <summary>
        /// 겹침 비키기: 이미 떠 있는 숫자와 겹치면 세로로 비킨다 (가장 가까운 빈 줄, 없으면 차례로 최대 4번) — 글자 폭 ≈ 글자 수 × 0.58em(굵은 숫자 + 외곽선, 캡처에서 잰 폭 — 55pt "222"가 약 95px),
        /// 반높이 21px (× 크기). 세로 겹침 판정 = 두 반높이 + 4px, 비킨 자리 = 그보다 4px 더 (46pt 둘이면 가운데 사이 50px — 42px 넘게 띄운다).
        /// 가로는 두 반폭 + 4px 안이면 겹침 (예전 −6px 허용은 나란히 붙은 "96"과 "222"를 "96222"처럼 읽히게 했다). 치명타는 쾅·흔들림·별 배지 몫까지 1.25배 크기로 잰다
        /// </summary>
        const int NudgePasses = 4;
        const float NumberCharWidth = 0.58f;
        const float NumberHalfHeight = 21f;
        const float NudgeReach = 4f;
        const float NudgeGap = 4f;
        const float NudgeGapX = 4f;
        const float CritNudgeScale = 1.25f;
        /// <summary>빈 줄 고르기에서 아래 후보에 더하는 거리 (위로 비키면 마음껏 떠오르고, 아래로 비키면 위 이웃에 막혀 덜 떠오른다)</summary>
        const float NudgeDownCost = 8f;
        /// <summary>같은 대상 숫자와 '바로 위·아래로 쌓였다'고 보는 여유 (비킨 간격 + 이만큼까지)</summary>
        const float StackSlack = 12f;
        /// <summary>
        /// 위쪽 줄 적: 상단 안전선에 눌려 40px 넘게 내려온 숫자는 (그 적의 발 위) 몸 옆으로 48px 미끄러져 나가며 24px 떠오른다 —
        /// 수선점 바깥쪽으로, 전장 끝을 넘으면 안쪽으로. 떠오른 끝도 안전선 − 10px 아래라 그만큼 조금 낮게 뜬다
        /// </summary>
        const float NumberSlideDrop = 40f;
        const float NumberSlideOut = 48f;
        const float NumberSlideRise = 24f;
        /// <summary>뜬 직후 가로 떨림 (120ms, 약 3번)</summary>
        const float NumberShakeTime = 0.12f;
        const float NumberShakeHz = 25f;
        /// <summary>사라지기: 수명의 마지막 30%</summary>
        const float NumberFadeFrom = 0.7f;
        /// <summary>치명타: 280ms 동안 18px만 떠오르며 버틴다 / 별 배지 90 → 150px (0.35초, 가산 0.5) / 광택 60px가 +90ms부터 160ms 동안 훑는다</summary>
        const float CritHoldTime = 0.28f;
        const float CritHoldRise = 18f;
        /// <summary>
        /// 치명타 숫자는 머리 위에서: 몸 가운데 + 40px에서 뜨면 74pt 쾅 숫자가 버티는 280ms(18px만 떠오름) 내내 맞은 몸을 가려 66ms 떨림·넉백이 안 읽힌다 —
        /// 붙은 적의 머리(HeadPoint) − 몸 가운데(HitPoint) − 10px 만큼 (0 ~ 70px: 110px 적 ≈ 66px, 보스 70px) 올려 명중 +83ms부터 숫자 아래 끝이 머리 위.
        /// 위쪽 줄 적은 그 뒤 상단 안전선 자르기(몸 옆 미끄러짐)가 그대로 걸린다
        /// </summary>
        const float CritHeadGap = 10f;
        const float CritHeadLiftMax = 70f;
        const float CritBadgeFrom = 90f;
        const float CritBadgeTo = 150f;
        const float CritBadgeLife = 0.35f;
        const float GlossStart = 0.09f;
        const float GlossTime = 0.16f;
        const float GlossSize = 60f;
        const int GlossSlots = 4;
        /// <summary>마지막 일격: 0.3초 안에 만든 그 대상의 숫자 ×1.15 + 반짝 셋</summary>
        const float KillBlowWindow = 0.3f;
        const float KillBlowScale = 1.15f;
        /// <summary>
        /// 마지막 일격 반짝은 0.25초(연출 시계)에 세 묶음까지 (별빛 층 — 예산이 모자라면 버린다). 한꺼번에 처치(3+) 중이면 묶음마다 하나 —
        /// 무리는 알림·바닥 충격파가 축하한다 (20마리 싹쓸이에 반짝 60개가 처치 꽃 예산을 먹지 않게)
        /// </summary>
        const float KillTwinkleWindow = 0.25f;
        const int KillTwinkleSets = 3;
        /// <summary>
        /// 알림 판 비키기: 판(숫자 레이어 맨 위)이 가리지 않게 판 둘레 1.1배(톡 1.08배 포함) 밖에 4px 띄우고, 판 밑에서도 최소 16px은 떠오른다
        /// </summary>
        const float CalloutPlateReach = 1.1f;
        const float CalloutClearance = 4f;
        const float MinNumberRise = 16f;
        /// <summary>
        /// 알림이 뜨는 순간 이미 떠 있는 숫자를 잴 때 판(리본 꼬리 포함 1.1배) 둘레에 더하는 여유 (Wave 3 · fx2 leftovers: '2,222'가 뒤에 뜬 '한꺼번에 20!' 판의
        /// 왼쪽 꼬리로 45px 떠올라 들어갔다 — 이제 판 밑에서 멈추거나 옆으로 흘러 나간다)
        /// </summary>
        const float CalloutPad = 12f;
        /// <summary>보스 이름표 묶음(이름표 + HP 바)과 숫자 사이 틈 / HUD 옆 칸(빠른 줄 · 바구니 · 시너지 칩)과 숫자 사이 틈</summary>
        const float PlateClearance = 4f;
        const float SideKeepGap = 6f;
        /// <summary>말풍선과 숫자 사이 틈 (말풍선 비키기 KeepGap과 같은 6px — Wave 3 r3 → RM-2)</summary>
        public const float BubbleClearance = 6f;
        /// <summary>한 번에 재는 단단한 말풍선 수 (말풍선 풀 5 + 여유)</summary>
        const int MaxBubbleKeepOuts = 6;
        readonly Rect[] _bubbleRects = new Rect[MaxBubbleKeepOuts];
        /// <summary>말풍선 뷰 (같은 ShakeRoot 아래 Callouts — 처음 한 번 찾는다, 없으면 비키지 않는다)</summary>
        SkillCalloutView _calloutView;
        bool _calloutViewSearched;
        /// <summary>
        /// HUD 칸 옆 높이에서 뜬 숫자를 전장 가운데 쪽으로 미는 최대 거리 (숫자 반폭과 이 값 중 큰 쪽 — 빠른 줄 칸 104px + 틈).
        /// 그보다 멀리 밀어야 하면 맞은 적에서 떨어지므로 대신 칸 밑으로 내려 조금만 떠오른다
        /// </summary>
        public const float SidePushMax = 120f;
        /// <summary>불 밝히기 숫자 벽: 한 물결의 창(초) · 몇 개부터 줄이나 · 배율 · 사라지기 시작(수명 비율) · 이미 떠서 이보다 오래된 숫자는 되짚어 줄이지 않는다(초)</summary>
        const float LampWallWindow = LampWave.Life + 0.1f;
        public const int LampWallMin = 8;
        public const float LampWallScale = 0.85f;
        const float LampWallFade = 0.6f;
        const float LampThinLate = 0.19f;
        /// <summary>알림 글자에 늘 비워 두는 칸</summary>
        const int CalloutReserve = 2;
        /// <summary>안내 글자(Text·Chip)의 비우기 순위 (막음과 같다)</summary>
        const int PlainRank = 8;
        /// <summary>합치지 않는 숫자의 대상 키</summary>
        public const int NoMerge = int.MinValue;
        /// <summary>수선점 숫자의 대상 키</summary>
        public const int ShopNumberId = -2;

        /// <summary>표식 / 지속 피해 기본 색 (NumberColor — 테스트: 금색은 치명타 전용)</summary>
        static readonly Color MarkPink = new(0.898f, 0.545f, 0.682f, 1f);
        static readonly Color DotOrange = new(1f, 0.722f, 0.42f, 1f);

        // ── 뜻별 세로 그라데이션 (§4.2 위 → 아래) ──
        static readonly Color GradCream = NumberHex(0xFFF8E8);
        static readonly Color GradNormalBottom = NumberHex(0xF4DEC4);
        static readonly Color GradHeavyBottom = NumberHex(0xF9CFB4);
        static readonly Color GradCritTop = NumberHex(0xFFE9A8);
        static readonly Color GradCritBottom = NumberHex(0xF2C46B);
        static readonly Color GradBossTop = NumberHex(0xFFF4DC);
        static readonly Color GradBossBottom = NumberHex(0xD9C8F5);
        static readonly Color GradMarkTop = NumberHex(0xF7C1D4);
        static readonly Color GradMarkBottom = NumberHex(0xE58BAE);
        static readonly Color GradDotTop = NumberHex(0xFFD29A);
        static readonly Color GradDotBottom = NumberHex(0xFFB86B);
        static readonly Color GradHealTop = NumberHex(0xD6F7E5);
        static readonly Color GradHealBottom = NumberHex(0x8FE3B0);
        static readonly Color GradBlockTop = NumberHex(0xE2F1FF);
        static readonly Color GradBlockBottom = NumberHex(0x93C8F0);
        static readonly Color GradShopTop = NumberHex(0xFFC4CB);
        static readonly Color GradShopBottom = NumberHex(0xD9566E);
        static readonly Color GradLampTop = NumberHex(0xFFF1C9);
        static readonly Color GradLampBottom = NumberHex(0xFFD98A);

        /// <summary>
        /// 전투 중 글리프를 처음 그릴 때 동적 아틀라스가 멈칫하지 않게 미리 굽는 글자 (숫자·기호·단위·알림 문구). 한 번만 (할당 괜찮음)
        /// </summary>
        const string WarmGlyphs = "0123456789,.+-%★♥!ABCDEFGHIJKLMNOPQRSTUVWXYZ 막음깨짐연속수선반짝대눈부셔요한꺼번에";

        FloatingNumber[] _numbers;
        int _nextNumber;
        int _numberSequence;
        DamageNumberMode _numberMode;
        /// <summary>같은 대상의 이어지는 숫자 좌우 엇갈림 (대상 ID & 31 → 센 수)</summary>
        readonly int[] _numberAlternate = new int[32];
        /// <summary>치명타 숫자 위를 훑는 광택 (숫자 레이어 — 숫자보다 위에 그려진다)</summary>
        Image[] _glossImages;
        int[] _glossOwner;
        int _glossCursor;
        /// <summary>마지막 일격 반짝 창 시작(연출 시계) / 그 창에 낸 묶음 수</summary>
        float _killTwinkleFrom = -99f;
        int _killTwinkleSets;
        /// <summary>
        /// 불 밝히기 숫자 벽: 이번 물결의 첫 숫자가 닿는 시각(연출 시계) / 그 물결의 피해 수(합쳐지거나 상한에 막힌 것까지) / 맞은 피해 합(실수 그대로 — 후반 피해는 2.1B를 넘는다) /
        /// 가장 늦게 닿는 숫자 시각 / 창을 연 연출 시각(그 뒤 만든 불 밝히기 숫자가 이번 물결)
        /// </summary>
        float _lampWallFrom = -99f;
        int _lampWallCount;
        double _lampWallSum;
        float _lampWallLast = -99f;
        float _lampWallSpawn = -99f;
        /// <summary>합계 알림 예약 (숫자 8개 이상 뜬 물결 — 마지막 숫자가 닿고 LampSumDelay 뒤, BattleFx.Callouts)</summary>
        bool _lampSumPending;
        float _lampSumAt;

        // ───────────── 잠들 때 숫자 사라짐 (RI-6b) ─────────────

        /// <summary>잠들 때 떠 있는 피해 · 불 밝히기 숫자가 사라지는 시간 (실제 시간 초 — 그 뒤 숨긴다)</summary>
        public const float SleepNumberFade = 0.2f;
        /// <summary>잠든 뒤 지난 실제 시간 (사라지는 중이 아니면 −1)</summary>
        float _sleepFadeAge = -1f;
        /// <summary>이번 프레임 숫자 묶음 알파에 곱하는 잠자기 사라짐 (1 = 그대로)</summary>
        float _sleepNumberAlpha = 1f;

        /// <summary>잠자기 숫자 사라짐 배율 (순수 — SleepSignalTests): 잠든 뒤 0초 1 → SleepNumberFade(0.2초) 0, SmoothStep. 음수 = 사라짐 없음 (1)</summary>
        public static float SleepNumberAlpha(float sinceSleep) =>
            sinceSleep < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, sinceSleep / SleepNumberFade);

        /// <summary>떠 있는 숫자가 잠자기로 사라지는 중인지 (확인용)</summary>
        public bool SleepNumbersFading => _sleepFadeAge >= 0f;

        /// <summary>지금 숫자 묶음 알파에 곱하는 잠자기 사라짐 (확인용 — 1 = 그대로)</summary>
        public float SleepNumberAlphaNow => _sleepNumberAlpha;

        /// <summary>불 밝히기 합계 알림이 예약돼 있는지 (확인용 — 잠들면 버린다)</summary>
        public bool LampSumPending => _lampSumPending;

        /// <summary>
        /// 잠들 때 (SetSleepMode(true)): 예약된 불 밝히기 합계 알림을 버리고, 아직 안 뜬 숫자(지연 중)는 바로 숨기고, 늦게 닿을 합침 몫을 비운다.
        /// 떠 있는 피해 · 불 밝히기 · 안내 숫자는 SleepNumberFade(0.2초, 실제 시간)에 걸쳐 묶음 알파로 사라진 뒤 숨는다 (TickSleepNumbers).
        /// 연속 · 한꺼번에 · 합계 알림 판은 제 수명(0.9초 이하)대로 끝난다 (잠든 동안은 새로 뜨지 않는다 — AcquireCalloutNumber). 할당 없음
        /// </summary>
        void BeginSleepNumberFade()
        {
            _lampSumPending = false;
            _sleepFadeAge = 0f;
            _sleepNumberAlpha = 1f;
            if (_numbers == null) return;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var n = ref _numbers[i];
                if (!n.Alive || n.Callout) continue;
                n.PendingValue = 0f;
                n.PendingDelay = 0f;
                if (!n.Shown) HideNumber(i);
            }
        }

        /// <summary>잠들 때 숫자 사라짐을 실제 시간 dt만큼 (Advance — 사라지는 중이 아니면 바로 돌아간다). 끝나면 남은 숫자를 숨긴다</summary>
        internal void TickSleepNumbers(float dt)
        {
            if (_sleepFadeAge < 0f) return;
            _sleepFadeAge += Mathf.Max(0f, dt);
            if (_sleepFadeAge < SleepNumberFade)
            {
                _sleepNumberAlpha = SleepNumberAlpha(_sleepFadeAge);
                return;
            }
            EndSleepNumberFade();
        }

        /// <summary>사라짐 끝 (또는 사라지는 중에 깸): 남은 숫자(알림 판 빼고)를 숨기고 배율을 1로. 사라지는 중이 아니면 아무것도 안 한다</summary>
        void EndSleepNumberFade()
        {
            if (_sleepFadeAge < 0f) return;
            _sleepFadeAge = -1f;
            _sleepNumberAlpha = 1f;
            if (_numbers == null) return;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (_numbers[i].Alive && !_numbers[i].Callout) HideNumber(i);
            }
        }

        void PrepareNumbers()
        {
            _numbers = new FloatingNumber[_numberCapacity];
            _numberTemplate.enabled = false;
            _numberTemplate.raycastTarget = false;
            // §4.1: 46pt · 300×80 칸 · 굵게(템플릿 그대로 — 굵은 SDF가 있으면 진짜 굵게) · 흰 글자 × 뜻별 세로 그라데이션 (알파로 사라진다)
            _numberTemplate.fontSize = NumberFontSize;
            _numberTemplate.rectTransform.sizeDelta = NumberRectSize;
            _numberTemplate.color = Color.white;
            _numberTemplate.enableVertexGradient = true;
            _numberMaterial = CreateOutlineMaterial(_numberTemplate);
            if (_numberMaterial != null) _numberTemplate.fontSharedMaterial = _numberMaterial;
            WarmNumberGlyphs(_numberTemplate.font);
            var parent = _numberLayer != null ? _numberLayer : _layer;
            for (int i = 0; i < _numberCapacity; i++)
            {
                var text = Instantiate(_numberTemplate, parent);
                text.name = "Number";
                text.enabled = false;
                text.raycastTarget = false;
                // 사라짐은 묶음 알파로 (글자 메시를 다시 짜지 않고 Bold 하위 메시까지 함께 — FloatingNumber.Group)
                if (!text.TryGetComponent(out CanvasGroup group)) group = text.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;
                _numbers[i] = new FloatingNumber { Text = text, Rect = text.rectTransform, Group = group, Alpha = 1f, TargetId = NoMerge, Gloss = -1, Lane = -1 };
            }
            PrepareNumberGloss(parent);
            PrepareCallouts(parent);
        }

        /// <summary>숫자 글꼴(보통 + 굵은 짝)의 동적 아틀라스에 글자를 미리 넣는다 (정적 글꼴이면 건너뛴다)</summary>
        static void WarmNumberGlyphs(TMP_FontAsset font)
        {
            if (font == null) return;
            if (font.atlasPopulationMode != AtlasPopulationMode.Static) font.TryAddCharacters(WarmGlyphs);
            var weights = font.fontWeightTable;
            var bold = weights != null && weights.Length > 7 ? weights[7].regularTypeface : null;
            if (bold != null && bold != font && bold.atlasPopulationMode != AtlasPopulationMode.Static) bold.TryAddCharacters(WarmGlyphs);
        }

        /// <summary>광택 그림 칸 (숫자 레이어, 입자 템플릿 복제 — 미리 곱한 알파 머티리얼이면 가산으로 빛난다)</summary>
        void PrepareNumberGloss(Transform parent)
        {
            _glossImages = new Image[GlossSlots];
            _glossOwner = new int[GlossSlots];
            var gloss = GlossSprite();
            for (int i = 0; i < GlossSlots; i++)
            {
                var image = Instantiate(_particleTemplate, parent);
                image.name = "NumberGloss";
                image.raycastTarget = false;
                image.enabled = false;
                image.sprite = gloss;
                image.canvasRenderer.cullTransparentMesh = false;
                _glossImages[i] = image;
                _glossOwner[i] = -1;
            }
        }

        Sprite GlossSprite()
        {
            var gloss = _juice != null ? _juice.GlossShine : null;
            if (gloss == null && _library != null) gloss = _library.Vfx(SkillVfx.GlossShine);
            return gloss != null ? gloss : _sparkle;
        }

        // ───────────── 문법 ─────────────

        /// <summary>뜻의 기본 색 (그라데이션 아래쪽에 가까운 대표색 — 테스트: 금색은 치명타 전용)</summary>
        public static Color NumberColor(DamageNumberKind kind) => kind switch
        {
            DamageNumberKind.Crit => UITheme.Gold,
            DamageNumberKind.Mark => MarkPink,
            DamageNumberKind.Dot => DotOrange,
            DamageNumberKind.Heal => UITheme.Positive,
            DamageNumberKind.Block => UITheme.SoftBlue,
            DamageNumberKind.ShopDamage => UITheme.Danger,
            DamageNumberKind.Skill => UITheme.Lavender,
            DamageNumberKind.Lamp => UITheme.Lamp,
            _ => UITheme.Cream,
        };

        /// <summary>뜻의 크기 배율 (× 46pt): 일반·표식·수선점 1.0 · 강타 1.15 · 치명타 1.6 · 보스·스킬 1.2 · 불빛 1.1 · 회복 0.9 · 지속·막음 0.85</summary>
        public static float NumberScale(DamageNumberKind kind) => kind switch
        {
            DamageNumberKind.Heavy => 1.15f,
            DamageNumberKind.Crit => 1.6f,
            DamageNumberKind.Boss => 1.2f,
            DamageNumberKind.Dot => 0.85f,
            DamageNumberKind.Heal => 0.9f,
            DamageNumberKind.Block => 0.85f,
            DamageNumberKind.Skill => 1.2f,
            DamageNumberKind.Lamp => 1.1f,
            _ => 1f,
        };

        /// <summary>
        /// 뜻별 세로 그라데이션 (위 → 아래, §4.2). 스킬은 tint(시전 계보 강조색)로 물든다: 위 = lerp(tint, 크림, 0.25), 아래 = tint × 0.85.
        /// 치명타만 금빛 (FFE9A8 → F2C46B). 순수 함수 (테스트용 공개)
        /// </summary>
        public static void NumberGradient(DamageNumberKind kind, Color tint, out Color top, out Color bottom)
        {
            switch (kind)
            {
                case DamageNumberKind.Heavy:
                    top = GradCream;
                    bottom = GradHeavyBottom;
                    break;
                case DamageNumberKind.Crit:
                    top = GradCritTop;
                    bottom = GradCritBottom;
                    break;
                case DamageNumberKind.Boss:
                    top = GradBossTop;
                    bottom = GradBossBottom;
                    break;
                case DamageNumberKind.Mark:
                    top = GradMarkTop;
                    bottom = GradMarkBottom;
                    break;
                case DamageNumberKind.Dot:
                    top = GradDotTop;
                    bottom = GradDotBottom;
                    break;
                case DamageNumberKind.Heal:
                    top = GradHealTop;
                    bottom = GradHealBottom;
                    break;
                case DamageNumberKind.Block:
                    top = GradBlockTop;
                    bottom = GradBlockBottom;
                    break;
                case DamageNumberKind.ShopDamage:
                    top = GradShopTop;
                    bottom = GradShopBottom;
                    break;
                case DamageNumberKind.Lamp:
                    top = GradLampTop;
                    bottom = GradLampBottom;
                    break;
                case DamageNumberKind.Skill:
                {
                    var paint = SkillNumberTint(tint);
                    top = Color.Lerp(paint, UITheme.Cream, 0.25f);
                    bottom = new Color(paint.r * 0.85f, paint.g * 0.85f, paint.b * 0.85f, 1f);
                    break;
                }
                default:
                    top = GradCream;
                    bottom = GradNormalBottom;
                    break;
            }
        }

        /// <summary>스킬 숫자 물감: 시전 계보 강조색 (없으면 라벤더). 금빛 강조색(똑단이·우두커니·라라벨)은 치명타와 헷갈리지 않게 라벤더</summary>
        public static Color SkillNumberTint(Color tint)
        {
            if (!(tint.a > 0.01f) || IsGoldish(tint)) return UITheme.Lavender;
            return new Color(tint.r, tint.g, tint.b, 1f);
        }

        /// <summary>금빛인지 (색상 34~56°, 채도 0.35 초과, 밝기 0.6 초과)</summary>
        static bool IsGoldish(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            float degrees = hue * 360f;
            return degrees >= 34f && degrees <= 56f && saturation > 0.35f && value > 0.6f;
        }

        /// <summary>
        /// 화면 상한이 찼을 때 비우는 순위 (낮을수록 먼저 비운다): 지속 &lt; 일반 &lt; 표식 &lt; 회복 &lt; 강타 &lt; 스킬 &lt; 불빛 &lt; 보스 &lt; 막음 &lt; 수선점 &lt; 치명타
        /// </summary>
        public static int NumberDropRank(DamageNumberKind kind) => kind switch
        {
            DamageNumberKind.Dot => 0,
            DamageNumberKind.Normal => 1,
            DamageNumberKind.Mark => 2,
            DamageNumberKind.Heal => 3,
            DamageNumberKind.Heavy => 4,
            DamageNumberKind.Skill => 5,
            DamageNumberKind.Lamp => 6,
            DamageNumberKind.Boss => 7,
            DamageNumberKind.Block => 8,
            DamageNumberKind.ShopDamage => 9,
            DamageNumberKind.Crit => 10,
            _ => 1,
        };

        /// <summary>
        /// 뜻별 팝 크기 (나이 초): 첫 프레임부터 Start → PeakTime에 Peak (OutQuad) → SettleTime에 1.0 (InOutSine). 치명타는 쾅 곡선. 순수 함수
        /// </summary>
        public static float NumberPopScale(DamageNumberKind kind, float age)
        {
            if (kind == DamageNumberKind.Crit) return CritSlamScale(age, out _, out _);
            var m = NumberMotions[(int)kind];
            if (age <= 0f) return m.Start;
            if (m.PeakTime > 0f && age < m.PeakTime) return Mathf.LerpUnclamped(m.Start, m.Peak, JuiceEase.OutQuad(age / m.PeakTime));
            if (age < m.SettleTime)
            {
                float from = m.PeakTime > 0f ? m.Peak : m.Start;
                float u = (age - m.PeakTime) / Mathf.Max(0.001f, m.SettleTime - m.PeakTime);
                return Mathf.LerpUnclamped(from, 1f, m.PeakTime > 0f ? BattleEase.InOutSine(u) : JuiceEase.OutCubic(u));
            }
            return 1f;
        }

        /// <summary>치명타 쾅: 1.8배(가로 1.2 / 세로 0.85로 눌림)로 내리찍고 → 70ms 0.92 (OutCubic, 눌림 풀림) → 140ms 1.0</summary>
        static float CritSlamScale(float age, out float squashX, out float squashY)
        {
            squashX = 1f;
            squashY = 1f;
            if (age < 0.07f)
            {
                float u = JuiceEase.OutCubic(Mathf.Max(0f, age) / 0.07f);
                squashX = Mathf.LerpUnclamped(1.2f, 1f, u);
                squashY = Mathf.LerpUnclamped(0.85f, 1f, u);
                return Mathf.LerpUnclamped(1.8f, 0.92f, u);
            }
            if (age < 0.14f) return Mathf.LerpUnclamped(0.92f, 1f, JuiceEase.OutQuad((age - 0.07f) / 0.07f));
            return 1f;
        }

        /// <summary>치명타 흔들림: 7°·sin(2π·t/110ms)·(1 − t/220ms), 220ms 뒤 0</summary>
        static float CritWiggle(float age) => age < 0.22f ? 7f * Mathf.Sin(2f * Mathf.PI * age / 0.11f) * (1f - age / 0.22f) : 0f;

        /// <summary>합쳐질 때 톡: 첫 24ms 꼭대기 그대로 → OutCubic으로 돌아온다 (u = 0 → 1)</summary>
        static float RepopEnvelope(float u) => u < 0.15f ? 1f : 1f - JuiceEase.OutCubic((u - 0.15f) / 0.85f);

        static float MergeWindowOf(DamageNumberKind kind) => kind switch
        {
            DamageNumberKind.Dot => DotMergeWindow,
            DamageNumberKind.Heal => HealMergeWindow,
            DamageNumberKind.Crit => 0f,
            DamageNumberKind.Skill => SkillMergeWindow,
            DamageNumberKind.Lamp => LampMergeWindow,
            _ => MergeWindow,
        };

        /// <summary>명중 33ms 뒤에 뜨는 숫자인지 (회복·수선점 피해는 바로)</summary>
        static bool LagsImpact(DamageNumberKind kind) => kind != DamageNumberKind.Heal && kind != DamageNumberKind.ShopDamage;

        /// <summary>뜻별 숫자 글자 (실수 그대로 — 아주 큰 피해도 넘치지 않는다)</summary>
        static string Format(DamageNumberKind kind, double amount) => kind switch
        {
            DamageNumberKind.Heal => BattleEase.Plus(amount),
            DamageNumberKind.ShopDamage => BattleEase.Minus(amount),
            DamageNumberKind.Block => BattleEase.Blocked(amount),
            DamageNumberKind.Crit => BattleEase.Crit(amount),
            DamageNumberKind.Mark => BattleEase.Mark(amount),
            _ => BattleEase.Int(amount),
        };

        /// <summary>적에게 준 피해 숫자인지 (설정 '치명타만 / 끄기'가 적용되는 숫자)</summary>
        static bool IsEnemyDamage(DamageNumberKind kind) => kind != DamageNumberKind.Heal && kind != DamageNumberKind.ShopDamage;

        bool ShowKind(DamageNumberKind kind)
        {
            // 잠자기 모드: 숫자를 모두 끈다 (IDLE-06 — 깨면 다시)
            if (_sleepMode) return false;
            if (!IsEnemyDamage(kind)) return true;
            return _numberMode switch
            {
                DamageNumberMode.Off => false,
                DamageNumberMode.CritOnly => kind == DamageNumberKind.Crit,
                _ => true,
            };
        }

        /// <summary>
        /// 문법에 맞는 숫자를 띄운다. 같은 대상(targetId)·같은 뜻의 숫자가 합치기 시간 안에 있으면 거기에 더한다 (붐비면 창을 0.25초로).
        /// delay = 투사체·연출이 닿는 순간까지 (숫자는 그보다 33ms 뒤). 띄운(또는 합친) 숫자 칸 번호, 안 띄웠으면 -1.
        /// scale = 추가 배율 (리듬 강조 1.2 등). tint = 스킬 숫자를 물들일 시전 계보 강조색 (없으면 라벤더)
        /// </summary>
        public int DamageNumber(int targetId, Vector2 position, float value, DamageNumberKind kind, float delay = 0f, float scale = 1f, Color tint = default)
        {
            if (!_ready) Prepare();
            if (!ShowKind(kind)) return -1;
            if (!(value >= 0.5f)) return -1;
            delay = Mathf.Max(0f, delay) + (LagsImpact(kind) ? HitFeel.NumberLag : 0f);
            // 불 밝히기 피해는 합치기 전에 물결 합계에 더한다 (합쳐진 몫까지 — 합계 알림)
            if (kind == DamageNumberKind.Lamp) NoteLampWave(value, delay);
            float window = MergeWindowOf(kind);
            if (window > 0f && window < BusyMergeWindow && NumbersBusy(delay)) window = BusyMergeWindow;
            if (window > 0f && targetId != NoMerge)
            {
                int merged = TryMerge(targetId, kind, value, delay);
                if (merged >= 0) return merged;
            }
            int index = AcquireNumber(NumberDropRank(kind), kind == DamageNumberKind.Crit || kind == DamageNumberKind.ShopDamage);
            if (index < 0) return -1;
            float callerScale = scale > 0.01f ? scale : 1f;
            NumberGradient(kind, tint, out var top, out var bottom);
            ShowNumber(index, position, Format(kind, value), top, bottom, kind, NumberScale(kind) * callerScale, delay);
            ref var n = ref _numbers[index];
            n.TargetId = targetId;
            n.Value = value;
            n.CallerScale = callerScale;
            n.MergeUntil = _clock + delay + window;
            if (targetId != NoMerge && targetId != ShopNumberId)
            {
                // 같은 대상의 이어지는 숫자는 좌우로 엇갈린다 (겹쳐 읽히지 않게)
                int slot = targetId & (_numberAlternate.Length - 1);
                _numberAlternate[slot]++;
                n.Position.x += (_numberAlternate[slot] & 1) == 0 ? NumberAlternate : -NumberAlternate;
            }
            if (kind == DamageNumberKind.Crit) MakeCrit(index);
            else if (kind == DamageNumberKind.Lamp) ThinLampWall(index);
            return index;
        }

        /// <summary>
        /// 불 밝히기 피해 하나를 이번 빛 물결에 더한다 (DamageNumber — 합치기 전, 합쳐지거나 화면 상한에 막힌 몫까지 센다). 닿는 시각이 첫 숫자에서
        /// LampWave.Life + 0.1초를 넘으면 새 물결. 8번째 피해에서 숫자 벽을 줄이고(ThinLampWave) 합계 알림을 예약한다 — 물결의 숫자는 한 프레임에 함께
        /// 만들어지고 닿는 시각만 다르므로(거리순) 합계 알림은 가장 늦게 닿는 숫자 + LampSumDelay 뒤로 미룬다
        /// </summary>
        void NoteLampWave(float value, float delay)
        {
            float at = _clock + delay;
            if (at - _lampWallFrom > LampWallWindow)
            {
                _lampWallFrom = at;
                _lampWallLast = at;
                _lampWallSpawn = _clock;
                _lampWallCount = 0;
                _lampWallSum = 0d;
                _lampSumPending = false;
            }
            _lampWallSum += value;
            _lampWallCount++;
            if (at > _lampWallLast) _lampWallLast = at;
            if (_lampWallCount == LampWallMin)
            {
                ThinLampWave();
                _lampSumPending = true;
            }
            if (_lampSumPending) _lampSumAt = _lampWallLast + LampSumDelay;
        }

        /// <summary>
        /// 불 밝히기 숫자 벽 줄이기 (Wave 3 · fx2 leftovers nice — 방치형 궁극기는 숫자 하나하나보다 한 번의 큰 합계가 읽혀야 한다):
        /// 한 번의 빛 물결에 불 밝히기 피해가 8번 이상이면 그 물결의 숫자를 모두 0.85배로, 수명 60%부터 사라지게 하고, 금빛 합계 알림
        /// ("+합계", 수선점 위 0.9초 — BattleFx.Callouts ShowLampSum)이 큰 한 방을 맡는다. 새 숫자는 여기서(ThinLampWall), 8번째 피해 앞에 만든 숫자는
        /// 그 순간 한꺼번에(ThinLampWave — 아직 안 떴거나 막 톡 하는 중인 것만, 앞의 7개만 크게 남지 않게).
        /// 마지막 일격 숫자는 줄이지 않는다 (나중에 마지막 일격이 되면 MarkKillingBlow가 되돌린다). 품질별 화면 상한(24/16/10)은 그대로
        /// </summary>
        void ThinLampWall(int index)
        {
            if (_lampWallCount >= LampWallMin) ThinNumber(ref _numbers[index]);
        }

        /// <summary>이번 물결(창을 연 뒤 만든)의 떠 있는 불 밝히기 숫자를 줄인다 — 이미 떠서 0.19초(톡 끝)가 지난 숫자는 크기가 튀지 않게 그대로</summary>
        void ThinLampWave()
        {
            if (_numbers == null) return;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var other = ref _numbers[i];
                if (!other.Alive || other.Callout || other.Plain || other.Kind != DamageNumberKind.Lamp || other.Spawned < _lampWallSpawn) continue;
                if (other.Shown && other.Age > LampThinLate) continue;
                ThinNumber(ref other);
            }
        }

        /// <summary>숫자 벽 한 칸: 0.85배 · 수명 60%부터 사라짐 (마지막 일격 · 이미 줄인 숫자는 그대로)</summary>
        static void ThinNumber(ref FloatingNumber n)
        {
            if (n.Thinned || n.KillBlow) return;
            n.Thinned = true;
            n.Scale *= LampWallScale;
            n.FadeAt = Mathf.Min(n.FadeAt, n.Life * LampWallFade);
        }

        /// <summary>치명타 숫자: 금빛 74pt 쾅 + 흔들림 + 별 배지 + 광택 (금색은 치명타 전용)</summary>
        public int CritNumber(int targetId, Vector2 position, float value, float delay = 0f) =>
            DamageNumber(targetId, position, value, DamageNumberKind.Crit, delay);

        /// <summary>회복 숫자 (민트 "+N")</summary>
        public int HealNumber(int targetId, Vector2 position, float amount, float delay = 0f) =>
            DamageNumber(targetId, position, amount, DamageNumberKind.Heal, delay);

        /// <summary>적 보호막이 막은 피해 (소프트 블루 "막음 N")</summary>
        public int BlockNumber(int targetId, Vector2 position, float amount, float delay = 0f) =>
            DamageNumber(targetId, position, amount, DamageNumberKind.Block, delay);

        /// <summary>수선점 피해 (Danger "-N", 문 아래). 여러 적이 한꺼번에 닿으면 하나로 합친다</summary>
        public int ShopDamageNumber(Vector2 position, float damage) =>
            DamageNumber(ShopNumberId, position, damage, DamageNumberKind.ShopDamage);

        /// <summary>피해 숫자 (양수 = 피해, 음수 = 앞에 '-'). 합치지 않는 자유 숫자. 띄운 숫자 칸 번호 (없으면 -1)</summary>
        public int Number(Vector2 position, float value, Color color, float scale = 1f, float delay = 0f)
        {
            if (_sleepMode || !(Mathf.Abs(value) >= 0.5f)) return -1;
            return Text(position, value < 0f ? BattleEase.Minus(value) : BattleEase.Int(value), color, scale, delay);
        }

        /// <summary>
        /// 아직 뜨기 전인 숫자(delay)를 적에게 붙인다: 뜨는 순간 그 적이 옮겨 간 만큼 옮겨서 뜬다 (살아남은 적이 비행 중에 걸어가도 몸 위에).
        /// 그 적의 머리 높이도 재 둔다 — 치명타 숫자(CritNumber · UpgradeToCrit)는 뜨는 순간 머리 위로 올라간다 (CritHeadLift, 부르는 쪽은 더하지 않는다).
        /// 이미 떴거나 이미 붙어 있으면(합쳐진 숫자) 그대로 둔다
        /// </summary>
        public void AnchorNumber(int index, EnemyView enemy)
        {
            if (enemy == null || _numbers == null || index < 0 || index >= _numbers.Length) return;
            ref var n = ref _numbers[index];
            if (!n.Alive || n.Delay <= 0f || n.Anchor != null) return;
            n.Anchor = enemy;
            n.AnchorId = enemy.Id;
            n.AnchorStart = enemy.Rect.anchoredPosition;
            n.AnchorOffset = Vector2.zero;
            n.HeadLift = CritHeadLift(enemy);
        }

        /// <summary>치명타 숫자를 올릴 높이: 머리(HeadPoint) − 몸 가운데(HitPoint) − 10px, 0 ~ 70px (110px 적 ≈ 66px, 보스 70px)</summary>
        static float CritHeadLift(EnemyView enemy) => Mathf.Clamp(enemy.HeadPoint.y - enemy.HitPoint.y - CritHeadGap, 0f, CritHeadLiftMax);

        /// <summary>글자 (잔향 환급, 보호막 양 같은 안내 숫자). 합치지 않는다. 색 위쪽은 크림 쪽으로 옅은 그라데이션</summary>
        public int Text(Vector2 position, string text, Color color, float scale = 1f, float delay = 0f)
        {
            if (!_ready) Prepare();
            if (string.IsNullOrEmpty(text)) return -1;
            int index = AcquireNumber(PlainRank, false);
            if (index < 0) return -1;
            var bottom = new Color(color.r, color.g, color.b, 1f);
            ShowNumber(index, position, text, Color.Lerp(bottom, JuiceColors.CoreCream, 0.35f), bottom, DamageNumberKind.Normal, scale, delay);
            _numbers[index].Plain = true;
            return index;
        }

        /// <summary>작은 알림 글자 (예: 보호막 "깨짐!" 34pt): rise px 떠오르며 life초 (합치지 않는다)</summary>
        public int Chip(Vector2 position, string text, Color color, float scale, float rise, float life, float delay = 0f)
        {
            int index = Text(position, text, color, scale, delay);
            if (index < 0) return -1;
            ref var n = ref _numbers[index];
            n.Rise = rise;
            n.Life = Mathf.Max(0.1f, life);
            n.FadeAt = n.Life * NumberFadeFrom;
            n.End = n.Life;
            return index;
        }

        /// <summary>
        /// 이미 띄운 숫자를 치명타 모양으로 바꾼다 (같은 프레임에 피격 → 치명타 순서로 이벤트가 온 경우). 적에 붙은 숫자면 새로 띄운 치명타와 같이
        /// 머리 위로 올라간다 (아직 안 떴으면 뜨는 순간, 떠 있으면 지금)
        /// </summary>
        public void UpgradeToCrit(int index)
        {
            if (_numbers == null || index < 0 || index >= _numbers.Length || !_numbers[index].Alive || _numbers[index].Callout) return;
            ref var n = ref _numbers[index];
            n.Kind = DamageNumberKind.Crit;
            n.Plain = false;
            string label = Format(DamageNumberKind.Crit, n.Value);
            n.Text.text = label;
            n.Chars = VisibleChars(label);
            NumberGradient(DamageNumberKind.Crit, default, out var top, out var bottom);
            n.Text.colorGradient = new VertexGradient(top, top, bottom, bottom);
            n.Scale = NumberScale(DamageNumberKind.Crit) * Mathf.Max(0.01f, n.CallerScale) * (n.KillBlow ? KillBlowScale : 1f);
            n.MergeUntil = 0f;
            MakeCrit(index);
            if (!n.Shown) return;
            // 이미 떠 있으면 머리 위로 올려 치명타 크기로 다시 자리 잡고 (상단 안전선 · 떠오름 끝 · 옆 HUD 칸 · 보스 이름표 — 새로 뜬 치명타와 같은 규칙)
            // 그 자리에서 다시 쾅 — 배지도 그 자리에
            n.Position.y += n.HeadLift;
            n.HeadLift = 0f;
            PlaceNumber(ref n);
            AvoidHudSides(ref n);
            AvoidBossPlates(ref n, NumberHalfWidth(n.Chars, n.Scale), NumberHalfHeight * NudgeScale(in n));
            AvoidBubbles(ref n, NumberHalfWidth(n.Chars, n.Scale), NumberHalfHeight * NudgeScale(in n));
            n.Age = 0f;
            n.Drawn = n.Position;
            CritNumberShown(index);
        }

        void MakeCrit(int index)
        {
            ref var n = ref _numbers[index];
            var m = NumberMotions[(int)DamageNumberKind.Crit];
            n.Crit = true;
            n.Life = m.Life;
            n.Rise = m.Rise;
            n.FadeAt = m.Life * NumberFadeFrom;
            n.End = m.Life;
            n.Shake = 0f;
        }

        // ───────────── 마지막 일격 (OnKillNoted → BattleFx.Callouts) ─────────────

        /// <summary>그 대상의 가장 최근 숫자(0.3초 안에 만든 것)를 마지막 일격으로: ×1.15, 뜨는 순간 반짝 셋</summary>
        void MarkKillingBlow(int enemyId)
        {
            if (_numbers == null) return;
            int best = -1;
            int bestSequence = int.MinValue;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var n = ref _numbers[i];
                if (!n.Alive || n.Callout || n.Plain || n.TargetId != enemyId || _clock - n.Spawned > KillBlowWindow) continue;
                if (n.Sequence <= bestSequence) continue;
                best = i;
                bestSequence = n.Sequence;
            }
            if (best < 0) return;
            ref var blow = ref _numbers[best];
            if (blow.KillBlow) return;
            blow.KillBlow = true;
            if (blow.Thinned)
            {
                // 불 밝히기 숫자 벽에서 줄였던 숫자도 처치한 일격이면 제 크기 · 제 수명으로 (처치 숫자는 줄이지 않는다)
                blow.Thinned = false;
                blow.Scale /= LampWallScale;
                blow.FadeAt = Mathf.Max(blow.FadeAt, blow.Life * NumberFadeFrom);
            }
            blow.Scale *= KillBlowScale;
            if (blow.Shown) KillBlowTwinkles(blow.Drawn);
        }

        /// <summary>
        /// 마지막 일격 숫자 둘레 반짝 셋 (fx_mote_twinkle, 가산 0.8, 0.3초). 별빛 층이라 예산이 모자라면 버리고, 0.25초에 세 묶음까지 —
        /// 한꺼번에 처치(3+) 중이면 하나만 (처치가 몰리는 순간 처치 꽃·숫자 몫의 입자를 먹지 않게)
        /// </summary>
        void KillBlowTwinkles(Vector2 at)
        {
            if (_clock - _killTwinkleFrom > KillTwinkleWindow)
            {
                _killTwinkleFrom = _clock;
                _killTwinkleSets = 0;
            }
            if (_killTwinkleSets >= KillTwinkleSets) return;
            _killTwinkleSets++;
            int count = _multiKill >= MultiKillMin ? 1 : 3;
            var sprite = _juice != null && _juice.MoteTwinkle != null ? _juice.MoteTwinkle : _sparkle;
            float start = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                if (!Allow(FxPriority.Sparkle)) return;
                float angle = start + i * (Mathf.PI * 2f / 3f) + Random.Range(-0.3f, 0.3f);
                var offset = new Vector2(Mathf.Cos(angle) * Random.Range(48f, 66f), Mathf.Sin(angle) * Random.Range(22f, 34f));
                int index = Spawn(sprite, i % 2 == 0 ? UITheme.Lamp : UITheme.Cream, at + offset, 30f, 14f, 0.3f, i * 0.035f, sparkle: true);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Add = 0.8f;
                p.Curve = SizeCurve.Back;
                p.P1 = Vector2.up * 12f;
                p.Spin = Random.Range(-140f, 140f);
                p.FadeIn = 0.05f;
                p.FadeOut = 0.45f;
            }
        }

        // ───────────── 합치기 / 칸 ─────────────

        /// <summary>붐비는지: 살아 있는 숫자가 8개를 넘거나 이 도착 앞 0.25초 밀도가 8을 넘는다 → 합치기 창 0.25초</summary>
        bool NumbersBusy(float delay)
        {
            int alive = 0;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (_numbers[i].Alive && !_numbers[i].Callout) alive++;
            }
            return alive > HitFeel.DensityMini || Density(delay) > HitFeel.DensityMini;
        }

        int TryMerge(int targetId, DamageNumberKind kind, float value, float delay)
        {
            float landing = _clock + delay;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var n = ref _numbers[i];
                if (!n.Alive || n.Callout || n.Plain || n.TargetId != targetId || n.Kind != kind || landing > n.MergeUntil) continue;
                // 마지막 일격 창은 마지막으로 더해진 순간부터
                n.Spawned = _clock;
                if (n.Delay > 0f)
                {
                    // 아직 뜨기 전: 뜰 때 합친 값으로
                    n.Value += value;
                    n.Merges++;
                    n.Delay = Mathf.Min(n.Delay, delay);
                    string label = Format(kind, n.Value);
                    n.Text.text = label;
                    n.Chars = VisibleChars(label);
                }
                else if (delay <= 0.02f)
                {
                    n.Value += value;
                    Refresh(ref n);
                }
                else
                {
                    // 떠 있는 숫자에 늦게 닿는 명중: 닿는 순간 더한다
                    n.PendingValue += value;
                    n.PendingDelay = Mathf.Max(n.PendingDelay, delay);
                }
                return i;
            }
            return -1;
        }

        /// <summary>합쳐진 값을 다시 쓰고 1.1 → 1.25배로 톡 튄다. 사라지는 중이었으면 조금 더 머문다 (제자리에서 — 아래로 튀지 않게)</summary>
        void Refresh(ref FloatingNumber n)
        {
            string label = Format(n.Kind, n.Value);
            n.Text.text = label;
            n.Chars = VisibleChars(label);
            n.Merges++;
            n.Bump = Mathf.Min(1.25f, 1.1f + 0.03f * Mathf.Min(n.Merges, 5));
            n.Repop = 1f;
            float stay = n.Age + 0.45f * n.Life;
            if (stay <= n.FadeAt) return;
            n.FadeAt = stay;
            n.End = stay + (1f - NumberFadeFrom) * n.Life;
        }

        /// <summary>
        /// 숫자 칸 하나 (rank = 비우기 순위). 알림 몫 2칸을 빼고, 떠 있는(뜰) 숫자가 품질 상한(24/16/10)보다 적으면 빈 칸.
        /// 아니면 순위가 같거나 낮은 숫자 중 (순위 → 작은 크기 → 오래된 것) 하나를 비운다. 늘 띄울 뜻(치명타·수선점)은 그래도 없으면 남은 칸을 쓴다
        /// </summary>
        int AcquireNumber(int rank, bool always)
        {
            int capacity = _numbers.Length;
            int regular = 0;
            int free = -1;
            for (int i = 0; i < capacity; i++)
            {
                int index = (_nextNumber + i) % capacity;
                ref var n = ref _numbers[index];
                if (n.Alive)
                {
                    if (!n.Callout) regular++;
                }
                else if (free < 0) free = index;
            }
            int poolLimit = Mathf.Max(1, capacity - CalloutReserve);
            int cap = Mathf.Clamp(_maxNumbers, 1, poolLimit);
            if (regular < cap && free >= 0)
            {
                _nextNumber = (free + 1) % capacity;
                return free;
            }
            int victim = NumberVictim(rank);
            if (victim >= 0)
            {
                HideNumber(victim);
                return victim;
            }
            if (always && free >= 0 && regular < poolLimit)
            {
                _nextNumber = (free + 1) % capacity;
                return free;
            }
            return -1;
        }

        /// <summary>비울 숫자: 순위가 rank 이하인 것 중 순위 → 크기 → 오래된 순 (알림 글자는 비우지 않는다)</summary>
        int NumberVictim(int rank)
        {
            int victim = -1;
            int victimRank = int.MaxValue;
            float victimScale = float.MaxValue;
            int victimSequence = int.MaxValue;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var n = ref _numbers[i];
                if (!n.Alive || n.Callout) continue;
                int r = n.Plain ? PlainRank : NumberDropRank(n.Kind);
                if (r > rank) continue;
                bool better = r < victimRank ||
                              (r == victimRank && (n.Scale < victimScale - 0.001f || (Mathf.Abs(n.Scale - victimScale) <= 0.001f && n.Sequence < victimSequence)));
                if (!better) continue;
                victim = i;
                victimRank = r;
                victimScale = n.Scale;
                victimSequence = n.Sequence;
            }
            return victim;
        }

        void ShowNumber(int index, Vector2 position, string text, Color top, Color bottom, DamageNumberKind kind, float scale, float delay)
        {
            ref var n = ref _numbers[index];
            var m = NumberMotions[(int)kind];
            n.Alive = true;
            n.Delay = delay;
            n.Age = 0f;
            n.Life = m.Life;
            n.FadeAt = m.Life * NumberFadeFrom;
            n.End = m.Life;
            n.Rise = m.Rise;
            n.Shake = m.Shake;
            n.Position = position;
            n.DriftX = 0f;
            n.Scale = scale;
            n.CallerScale = 1f;
            n.Kind = kind;
            n.Crit = false;
            n.Callout = false;
            n.Plain = false;
            n.Shown = false;
            n.KillBlow = false;
            n.Thinned = false;
            n.TargetId = NoMerge;
            n.Value = 0f;
            n.MergeUntil = 0f;
            n.PendingValue = 0f;
            n.PendingDelay = 0f;
            n.Repop = 0f;
            n.Bump = 1.1f;
            n.Merges = 0;
            n.Spawned = _clock;
            n.Sequence = ++_numberSequence;
            n.Chars = VisibleChars(text);
            n.Gloss = -1;
            n.Lane = -1;
            n.Anchor = null;
            n.AnchorOffset = Vector2.zero;
            n.HeadLift = 0f;
            n.Drawn = position;
            n.DrawnScale = Vector2.zero;
            n.Text.text = text;
            // 글자 알파는 1로 두고 사라짐은 묶음 알파로 (알림 글자도 같은 길 — BattleFx.Callouts) — 지난 숫자가 남긴 알파를 되돌린다
            n.Text.color = Color.white;
            SetNumberAlpha(ref n, 1f);
            n.Text.colorGradient = new VertexGradient(top, top, bottom, bottom);
            n.Text.enabled = false;
            n.Rect.anchoredPosition = position;
            n.Rect.localScale = Vector3.zero;
            n.Rect.localRotation = Quaternion.identity;
            n.Rect.SetAsLastSibling();
        }

        /// <summary>숫자 한 칸의 알파 (묶음 알파 — 같은 값이면 쓰지 않는다. 묶음이 없으면 TMP 알파로)</summary>
        static void SetNumberAlpha(ref FloatingNumber n, float alpha)
        {
            if (alpha == n.Alpha) return;
            n.Alpha = alpha;
            if (n.Group != null) n.Group.alpha = alpha;
            else n.Text.alpha = alpha;
        }

        /// <summary>
        /// 보이는 글자 수: 서식 태그(&lt;…&gt;)는 세지 않는다 — 치명타 별·표식 하트 앞의 "&lt;size=70%&gt;" 18글자까지 세면 숫자 폭이 수백 px로 잡혀
        /// 치명타 숫자가 전장 가운데(x = 0)로 끌려가고 겹침 비키기가 주변 숫자를 다 밀어낸다. 할당 없음
        /// </summary>
        static int VisibleChars(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i)
                    {
                        i = close + 1;
                        continue;
                    }
                }
                count++;
                i++;
            }
            return count;
        }

        void HideNumber(int index)
        {
            ref var n = ref _numbers[index];
            n.Alive = false;
            n.Text.enabled = false;
            n.TargetId = NoMerge;
            n.Anchor = null;
            if (n.Gloss >= 0) ReleaseGloss(n.Gloss, index);
            n.Gloss = -1;
            if (n.Lane >= 0) HideCalloutLane(n.Lane, index);
            n.Lane = -1;
            n.Callout = false;
        }

        /// <summary>합치기 기록을 끊고 알림·광택·예약된 스킬 흔들림을 비운다 (스테이지 재시작·시험장 — 같은 ID의 새 적과 합쳐지지 않게)</summary>
        void ClearNumberStacks()
        {
            if (_numbers == null) return;
            for (int i = 0; i < _numbers.Length; i++)
            {
                _numbers[i].TargetId = NoMerge;
                _numbers[i].PendingValue = 0f;
                _numbers[i].PendingDelay = 0f;
            }
            for (int i = 0; i < _numberAlternate.Length; i++) _numberAlternate[i] = 0;
            if (_glossImages != null)
            {
                for (int i = 0; i < _glossImages.Length; i++)
                {
                    _glossOwner[i] = -1;
                    if (_glossImages[i] != null) _glossImages[i].enabled = false;
                }
            }
            ClearCallouts();
            ClearSkillShow();
        }

        // ───────────── 갱신 ─────────────

        void UpdateNumbers(float dt)
        {
            // 숫자 칸과 같은 시계로 도는 show 쪽 예약 (스킬 흔들림 · 연속/한꺼번에 알림)
            TickSkillShow();
            TickCallouts();
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var n = ref _numbers[i];
                if (!n.Alive) continue;
                if (n.Delay > 0f)
                {
                    n.Delay -= dt;
                    // 붙은 적이 옮겨 간 만큼을 담아 두다가 (적이 사라지거나 다른 개체가 되면 마지막으로 본 자리)
                    if (n.Anchor != null)
                    {
                        if (n.Anchor.Id == n.AnchorId && n.Anchor.isActiveAndEnabled) n.AnchorOffset = n.Anchor.Rect.anchoredPosition - n.AnchorStart;
                        else n.Anchor = null;
                    }
                    if (n.Delay > 0f) continue;
                    // 떴어야 했던 순간부터 지난 만큼만 나이를 먹는다 (첫 보이는 프레임이 팝 꼭대기 쪽)
                    n.Age = Mathf.Min(-n.Delay, dt);
                    n.Delay = 0f;
                }
                else n.Age += dt;
                if (!n.Shown) NumberShown(i);

                if (n.PendingDelay > 0f)
                {
                    n.PendingDelay -= dt;
                    if (n.PendingDelay <= 0f && n.PendingValue > 0f)
                    {
                        n.Value += n.PendingValue;
                        n.PendingValue = 0f;
                        Refresh(ref n);
                    }
                }
                n.Repop = Mathf.Max(0f, n.Repop - dt / RepopTime);

                if (n.Age >= n.End)
                {
                    HideNumber(i);
                    continue;
                }
                if (n.Callout)
                {
                    DriveCallout(i);
                    continue;
                }
                DriveNumber(ref n);
                if (n.Gloss >= 0) DriveGloss(i);
            }
        }

        /// <summary>
        /// 숫자가 처음 보이는 순간: 붙은 적 자리로 옮기고, 상단 HUD 아래·전장 안으로 자르고, 바깥쪽 흐름을 정하고, 떠 있는 숫자·알림 판을 비키고,
        /// 치명타 배지·광택 / 마지막 일격 반짝. 알림 글자가 뜨는 순간이면 그 판에 가릴 숫자를 판 위로 올린다
        /// </summary>
        void NumberShown(int index)
        {
            ref var n = ref _numbers[index];
            n.Shown = true;
            n.Position += n.AnchorOffset;
            n.AnchorOffset = Vector2.zero;
            n.Anchor = null;
            n.Text.enabled = true;
            if (n.Callout)
            {
                RaiseNumbersOverCallout(n.Lane);
                return;
            }
            // 적에 붙은 치명타 숫자는 머리 위에서 쾅 (맞은 몸의 떨림·넉백이 숫자 밑으로 보이게) — 그다음 상단 자르기·겹침 비키기.
            // 보통 숫자는 높이를 남겨 둔다 (떠 있는 동안 치명타로 바뀌면 UpgradeToCrit이 쓴다)
            if (n.Crit)
            {
                n.Position.y += n.HeadLift;
                n.HeadLift = 0f;
            }
            PlaceNumber(ref n);
            AvoidHudSides(ref n);
            NudgeNumber(index);
            n.Rect.anchoredPosition = n.Position;
            n.Drawn = n.Position;
            if (n.Crit) CritNumberShown(index);
            if (n.KillBlow) KillBlowTwinkles(n.Position);
        }

        /// <summary>
        /// 자리 (§4.2): 수선점 가운데에서 바깥으로 14px 흐르고(보스 28), 가로는 전장 안, 세로는 상단 안전선 − 26px × 크기 아래.
        /// 떠오른 끝이 안전선 − 10px을 넘으면 떠오름을 줄인다 (최소 16px).
        /// 안전선에 눌려 40px 넘게 내려온 숫자(위쪽 줄 적 — 그대로 두면 그 적의 발에 얹힌다)는 몸 옆으로 48px 미끄러져 나가며 24px 떠오른다
        /// </summary>
        void PlaceNumber(ref FloatingNumber n)
        {
            float shopX = _shop != null ? _shop.FieldPosition.x : 0f;
            float dx = n.Position.x - shopX;
            if (!n.Plain && Mathf.Abs(dx) > 1f)
            {
                float drift = n.Kind == DamageNumberKind.Boss ? BossNumberDrift : NumberDrift;
                n.DriftX = Mathf.Sign(dx) * drift * (n.Crit ? 0.4f : 1f);
            }
            // 가로: 글자 폭까지 전장 안 (치명타는 쾅 하는 순간도 거의 잘리지 않게 1.25배 폭으로)
            float reach = Mathf.Max(NumberSideMargin, NumberHalfWidth(n.Chars, n.Scale * (n.Crit ? 1.25f : 1f)) + 8f);
            float half = Mathf.Max(0f, FieldHalfWidth - reach);
            n.Position.x = Mathf.Clamp(n.Position.x, -half, half);
            float size = Mathf.Max(1f, n.Scale);
            float safeTop = SafeTop;
            float ceiling = safeTop - NumberTopMargin * size;
            float roof = safeTop - NumberRoofMargin * size;
            if (!n.Plain && n.Position.y - ceiling > NumberSlideDrop)
            {
                // 24px을 다 떠올라도 끝이 안전선 − 10px 아래이도록 조금 더 낮게 (천장에 두면 떠오름이 16px로 깎인다)
                n.Position.y = Mathf.Min(ceiling, roof - NumberSlideRise);
                n.Rise = NumberSlideRise;
                // 치명타도 같이 — 제자리 쾅보다 몸에서 떨어져 읽히는 것이 먼저
                float slide = (dx < 0f ? -1f : 1f) * NumberSlideOut;
                if (Mathf.Abs(n.Position.x + slide) > half) slide = -slide;
                n.DriftX = slide;
                return;
            }
            if (n.Position.y > ceiling) n.Position.y = ceiling;
            if (n.Position.y + n.Rise > roof) n.Rise = Mathf.Max(MinNumberRise, roof - n.Position.y);
        }

        /// <summary>
        /// 이미 떠 있는 숫자와 겹치면 세로로 비킨다 (붐비는 전장에서 20개가 한꺼번에 떠도 읽히게): 가로로 겹치는 이웃마다 그 위·아래 (두 반높이 + 8px)
        /// 자리 중 어느 이웃과도 겹치지 않는 가장 가까운 줄로 (NearestFreeRow — 위쪽을 조금 더 좋아한다. 상단 안전선 위·보이는 띠 아래 끝 밑은 안 된다).
        /// 빈 줄이 없을 만큼 붐비면 겹친 이웃마다 차례로 위나 아래로 비킨다 (GreedyNudge, 최대 4번).
        /// 같은 대상의 숫자와 바로 위·아래로 쌓이면 그 흐름·떠오름을 따르고 (StackWithTarget → FollowNeighbour), 위쪽 이웃 아래에서는 떠오름을 줄여
        /// 따라잡지 않는다 (CapRiseUnderNeighbours — 떠오르며 다시 겹치지 않게).
        /// 떠 있는 연속·한꺼번에 알림 판(숫자 레이어 맨 위라 밑의 숫자를 가린다)과 가로로 겹치면 판 밑에 머물고 떠오른 끝도 판에 닿지 않는다
        /// (판은 상단 안전선 바로 아래라 위로는 비킬 자리가 없다). 비킨 숫자의 떠오른 끝도 안전선 − 10px 아래.
        /// 할당 없음 — 다른 숫자는 마지막으로 그린 자리(Drawn)와 자리 잡은 크기(NudgeScale)로 잰다
        /// </summary>
        void NudgeNumber(int index)
        {
            ref var n = ref _numbers[index];
            float halfWidth = NumberHalfWidth(n.Chars, n.Scale);
            float halfHeight = NumberHalfHeight * NudgeScale(in n);
            float size = Mathf.Max(1f, n.Scale);
            float ceiling = SafeTop - NumberTopMargin * size;
            // 보이는 띠 아래 끝 (MAIN은 덱 띠 위, 시트가 열리면 시트 위 끝) 밑으로는 비키지 않는다
            float floor = FieldBottom + NumberHalfHeight * size + NudgeGap;
            float plateFloor = CalloutFloor(n.Position.x, halfWidth + Mathf.Abs(n.DriftX));
            bool underPlate = plateFloor < float.MaxValue;
            // 숫자 가운데가 닿아도 되는 가장 높은 곳 (떠오른 끝 포함)
            float plateRoof = plateFloor - halfHeight - CalloutClearance;
            if (underPlate)
            {
                ceiling = Mathf.Min(ceiling, plateRoof - MinNumberRise);
                if (n.Position.y > ceiling) n.Position.y = ceiling;
            }
            bool nudged = false;
            if (Crowded(index, n.Position.y, halfWidth, halfHeight))
            {
                nudged = true;
                if (!NearestFreeRow(index, halfWidth, halfHeight, ceiling, floor)) GreedyNudge(index, halfWidth, halfHeight, ceiling);
                StackWithTarget(index, halfWidth, halfHeight);
            }
            if (underPlate && n.Position.y + n.Rise > plateRoof) n.Rise = Mathf.Max(MinNumberRise, plateRoof - n.Position.y);
            float roof = SafeTop - NumberRoofMargin * size;
            if (nudged && n.Position.y + n.Rise > roof) n.Rise = Mathf.Max(MinNumberRise, roof - n.Position.y);
            AvoidBossPlates(ref n, halfWidth, halfHeight);
            AvoidBubbles(ref n, halfWidth, halfHeight);
            CapRiseUnderNeighbours(index, halfWidth, halfHeight);
        }

        /// <summary>겹침 비키기의 이웃: 떠 있는 다른 숫자 (알림 글자 빼고)</summary>
        bool IsNudgeNeighbour(int i, int index)
        {
            if (i == index) return false;
            ref var other = ref _numbers[i];
            return other.Alive && other.Shown && !other.Callout;
        }

        /// <summary>숫자 index를 높이 y에 두면 떠 있는 이웃과 겹치는지 (가로 두 반폭 + 4px · 세로 두 반높이 + 4px 안)</summary>
        bool Crowded(int index, float y, float halfWidth, float halfHeight)
        {
            float x = _numbers[index].Position.x;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (!IsNudgeNeighbour(i, index)) continue;
                ref var other = ref _numbers[i];
                float otherScale = NudgeScale(in other);
                if (Mathf.Abs(x - other.Drawn.x) >= halfWidth + NumberHalfWidth(other.Chars, otherScale) + NudgeGapX) continue;
                if (Mathf.Abs(y - other.Drawn.y) < halfHeight + NumberHalfHeight * otherScale + NudgeReach) return true;
            }
            return false;
        }

        /// <summary>
        /// 가장 가까운 빈 줄: 가로로 겹치는 이웃마다 그 위·아래 (두 반높이 + 8px) 후보 중 어느 이웃과도 겹치지 않고 천장(ceiling)과 바닥(floor) 사이인 곳.
        /// 아래 후보는 8px 더 먼 것으로 친다 (아래로 가면 위 이웃에 막혀 덜 떠오른다). 찾으면 그 높이로 옮기고 true
        /// </summary>
        bool NearestFreeRow(int index, float halfWidth, float halfHeight, float ceiling, float floor)
        {
            ref var n = ref _numbers[index];
            float baseY = n.Position.y;
            float best = baseY;
            float bestCost = float.MaxValue;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (!IsNudgeNeighbour(i, index)) continue;
                ref var other = ref _numbers[i];
                float otherScale = NudgeScale(in other);
                if (Mathf.Abs(n.Position.x - other.Drawn.x) >= halfWidth + NumberHalfWidth(other.Chars, otherScale) + NudgeGapX) continue;
                float reach = halfHeight + NumberHalfHeight * otherScale + NudgeReach + NudgeGap;
                for (int side = -1; side <= 1; side += 2)
                {
                    float y = other.Drawn.y + side * reach;
                    if (y > ceiling || y < floor) continue;
                    float cost = Mathf.Abs(y - baseY) + (y < baseY ? NudgeDownCost : 0f);
                    if (cost >= bestCost || Crowded(index, y, halfWidth, halfHeight)) continue;
                    best = y;
                    bestCost = cost;
                }
            }
            if (bestCost == float.MaxValue) return false;
            n.Position.y = best;
            return true;
        }

        /// <summary>빈 줄이 없을 만큼 붐빌 때: 겹친 이웃마다 그 위나 아래로 (reachY + 4px 자리) 차례로 비킨다 — 위로 갈 자리가 안전선 밑에 없으면 아래로. 최대 4번</summary>
        void GreedyNudge(int index, float halfWidth, float halfHeight, float ceiling)
        {
            ref var n = ref _numbers[index];
            for (int pass = 0; pass < NudgePasses; pass++)
            {
                bool moved = false;
                for (int i = 0; i < _numbers.Length; i++)
                {
                    if (!IsNudgeNeighbour(i, index)) continue;
                    ref var other = ref _numbers[i];
                    var at = other.Drawn;
                    float otherScale = NudgeScale(in other);
                    float reachX = halfWidth + NumberHalfWidth(other.Chars, otherScale) + NudgeGapX;
                    float reachY = halfHeight + NumberHalfHeight * otherScale + NudgeReach;
                    float dy = n.Position.y - at.y;
                    if (Mathf.Abs(n.Position.x - at.x) >= reachX || Mathf.Abs(dy) >= reachY) continue;
                    // 비키는 양 = reachY − |dy| + 4px (이웃 위·아래로 reachY + 4px 자리)
                    float up = at.y + reachY + NudgeGap;
                    n.Position.y = dy >= 0f && up <= ceiling ? up : at.y - reachY - NudgeGap;
                    moved = true;
                }
                if (!moved) break;
            }
        }

        /// <summary>같은 대상의 숫자와 바로 위·아래로 쌓였으면 (가장 가까운 하나) 그 흐름·떠오름을 따른다 (FollowNeighbour)</summary>
        void StackWithTarget(int index, float halfWidth, float halfHeight)
        {
            ref var n = ref _numbers[index];
            if (n.TargetId == NoMerge) return;
            int nearest = -1;
            float nearestDy = float.MaxValue;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (!IsNudgeNeighbour(i, index)) continue;
                ref var other = ref _numbers[i];
                if (other.TargetId != n.TargetId) continue;
                float otherScale = NudgeScale(in other);
                if (Mathf.Abs(n.Position.x - other.Drawn.x) >= halfWidth + NumberHalfWidth(other.Chars, otherScale) + NudgeGapX) continue;
                float dy = Mathf.Abs(n.Position.y - other.Drawn.y);
                if (dy > halfHeight + NumberHalfHeight * otherScale + NudgeReach + NudgeGap + StackSlack || dy >= nearestDy) continue;
                nearest = i;
                nearestDy = dy;
            }
            if (nearest < 0) return;
            ref var neighbour = ref _numbers[nearest];
            FollowNeighbour(ref n, in neighbour, n.Position.y > neighbour.Drawn.y);
        }

        /// <summary>
        /// 떠오르며 따라잡지 않게 (대상과 상관없이 — 다른 적의 "48"이 위의 "222"를 파고들지 않게): 가로로 겹치는(지금 자리나 흐른 끝에서) 위쪽 이웃마다
        /// 이 숫자의 떠오른 끝이 이웃의 떠오른 끝보다 두 반높이 + 8px 아래에 머물도록 떠오름을 줄인다 (최소 16px).
        /// 어린 숫자는 OutCubic 앞부분이라 더 빨리 떠오르지만 이웃의 남은 떠오름은 남은 수명이 짧아 늘 앞서 가므로, 끝 간격만 지키면 떠오르는 내내
        /// 간격이 그 아래로 줄지 않는다. 쾅 하고 버티는 치명타 이웃 아래의 숫자는 버티는 동안 이웃이 떠오를 몫까지만. 이미 사라지는 중인 이웃은 건너뛴다
        /// </summary>
        void CapRiseUnderNeighbours(int index, float halfWidth, float halfHeight)
        {
            ref var n = ref _numbers[index];
            float endX = n.Position.x + n.DriftX;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (i == index) continue;
                ref var other = ref _numbers[i];
                if (!other.Alive || !other.Shown || other.Callout || other.Age >= other.FadeAt || other.Drawn.y <= n.Position.y) continue;
                float otherScale = NudgeScale(in other);
                float reachX = halfWidth + NumberHalfWidth(other.Chars, otherScale) + NudgeGapX;
                if (Mathf.Abs(n.Position.x - other.Drawn.x) >= reachX && Mathf.Abs(endX - (other.Position.x + other.DriftX)) >= reachX) continue;
                float gap = halfHeight + NumberHalfHeight * otherScale + NudgeReach + NudgeGap;
                float cap = other.Position.y + other.Rise - gap - n.Position.y;
                if (other.Crit && !n.Crit && other.Age < CritHoldTime)
                {
                    float holdLeft = Mathf.Max(0f, Mathf.Min(CritHoldRise, other.Rise * 0.25f) - (other.Drawn.y - other.Position.y));
                    cap = Mathf.Min(cap, other.Drawn.y - gap - n.Position.y + holdLeft);
                }
                if (cap < n.Rise) n.Rise = Mathf.Max(MinNumberRise, cap);
            }
            // 반대쪽 (Wave 3 · fx2 leftovers): 이 숫자가 아직 떠오르는 이웃 위에 떴으면(불 밝히기 물결처럼 늦게 뜬 숫자가 위에) 그 이웃의 떠오른 끝을
            // 이 숫자 끝 − (두 반높이 + 8px) 아래로 — 지금 그린 높이에서 이어지게 (RetargetRise — 치명타 이웃은 버팀 곡선 그대로). 예전에는 새 숫자만 깎아서
            // 아래 숫자가 위 숫자로 파고들었다
            float nEnd = n.Position.y + n.Rise;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (i == index) continue;
                ref var other = ref _numbers[i];
                if (!other.Alive || !other.Shown || other.Callout || other.Age >= other.FadeAt || other.Drawn.y >= n.Position.y) continue;
                float otherScale = NudgeScale(in other);
                float reachX = halfWidth + NumberHalfWidth(other.Chars, otherScale) + NudgeGapX;
                if (Mathf.Abs(n.Position.x - other.Drawn.x) >= reachX && Mathf.Abs(endX - (other.Position.x + other.DriftX)) >= reachX) continue;
                float gap = halfHeight + NumberHalfHeight * otherScale + NudgeReach + NudgeGap;
                if (other.Position.y + other.Rise > nEnd - gap) RetargetRise(ref other, nEnd - gap);
            }
        }

        /// <summary>떠오른 끝을 endY로 (RetargetRise(crit, …) — 이 숫자의 나이 · 수명 · 치명타 여부로)</summary>
        static void RetargetRise(ref FloatingNumber n, float endY) => RetargetRise(n.Crit, n.Age, n.Life, ref n.Position.y, ref n.Rise, endY);

        /// <summary>
        /// 떠오른 끝을 endY로 바꾼다 — 지금 그린 높이에서 끊기지 않게 뜬 자리(positionY)를 다시 잡는다: 남은 곡선 몫으로 endY에 닿도록.
        /// 이미 endY보다 높이 그려졌으면 그 자리에 멈춘다. 막 뜬 숫자(진행 0)는 최소 16px은 떠오른다. 거의 끝난 숫자(곡선 98%)는 그대로.
        /// 치명타는 버팀 곡선(RiseAt — 280ms 동안 min(18, rise/4)만 OutQuad, 그 뒤 OutCubic) 그대로 이어지게 푼다:
        /// 그린 높이 = 자리 + 버팀 × g + (떠오름 − 버팀) × e (버티는 중 g = OutQuad, e = 0 · 그 뒤 g = 1, e = OutCubic) — 새 떠오름이 72px 이상이면 버팀 18,
        /// 아니면 버팀 = 떠오름/4로 같은 식을 풀어 새 자리 + 새 떠오름 = 끝, 그린 높이는 그대로. 테스트용 공개, 할당 없음
        /// </summary>
        internal static void RetargetRise(bool crit, float age, float life, ref float positionY, ref float rise, float endY)
        {
            if (positionY + rise <= endY) return;
            float hold = 0f;
            float g = 0f;
            float e;
            if (crit)
            {
                hold = Mathf.Min(CritHoldRise, rise * 0.25f);
                bool holding = age < CritHoldTime;
                g = holding ? JuiceEase.OutQuad(age / CritHoldTime) : 1f;
                e = holding ? 0f : BattleEase.OutCubic(Mathf.Clamp01((age - CritHoldTime) / Mathf.Max(0.01f, life - CritHoldTime)));
            }
            else e = BattleEase.OutCubic(Mathf.Clamp01(age / Mathf.Max(0.01f, life)));
            if (e >= 0.98f) return;
            if (e <= 0f && g <= 0f)
            {
                rise = Mathf.Max(MinNumberRise, endY - positionY);
                return;
            }
            float drawn = positionY + hold * g + (rise - hold) * e;
            float delta = Mathf.Max(0f, endY - drawn);
            float next;
            if (!crit) next = delta / (1f - e);
            else
            {
                next = (delta + CritHoldRise * (g - e)) / (1f - e);
                if (next < 4f * CritHoldRise) next = delta / (1f - 0.25f * g - 0.75f * e);
            }
            positionY = drawn + delta - next;
            rise = next;
        }

        /// <summary>
        /// 가로 흐름의 끝을 targetX로 바꾼다 — 지금 그린 x에서 끊기지 않게 남은 OutCubic 몫으로 (뜬 자리 Position.x를 다시 잡는다). 거의 끝났으면 false
        /// </summary>
        static bool RetargetDrift(ref FloatingNumber n, float targetX)
        {
            float k = Mathf.Clamp01(n.Age / Mathf.Max(0.01f, n.Life));
            float e = BattleEase.OutCubic(k);
            if (e >= 0.98f) return false;
            float drawn = n.Position.x + n.DriftX * e;
            float drift = (targetX - drawn) / (1f - e);
            n.Position.x = drawn - drift * e;
            n.DriftX = drift;
            return true;
        }

        /// <summary>
        /// 옆 HUD 칸 비키기 (Wave 3 · fx2 leftovers — UX-SPEC-IA §3.2~3.4): 숫자가 뜬 자리부터 떠오른 끝까지 · 흐름 끝까지의 상자가 HUD 칸
        /// (빠른 줄 칸 x ≤ 116 / ≥ 964 · 바구니 · 시너지 칩 · 보스 도전 버튼 — BattleFx.KeepOut, 사라진 칸은 빠진다)과 겹치면:
        ///   · 칸 밑에서 뜬 숫자는 (치명타도) 떠오름을 칸 아래 끝 − 6px까지 줄인다 (최소 16px — 첫 그림 전이라 튀지 않는다).
        ///   · 칸 옆 높이에서 뜬 숫자는 전장 가운데 쪽으로 칸 안쪽 끝 + 반폭 + 6px까지 민다 (예: '★4,096'이 L1 이벤트 칸 위에 떴다) —
        ///     단 반폭(또는 SidePushMax 120px)까지만. 그보다 멀면 맞은 적에서 떨어지므로(예: 위쪽 왼쪽 레인 적의 치명타가 시너지 줄 오른쪽으로 260px)
        ///     칸 밑으로 내려 16px만 떠오른다 (보이는 띠 아래 끝 밑이면 민 거리만큼만).
        /// 두 번 훑는다. 할당 없음
        /// </summary>
        void AvoidHudSides(ref FloatingNumber n)
        {
            int count = HudKeepOutCount;
            if (count == 0) return;
            float scale = Mathf.Max(0.6f, n.Scale) * (n.Crit ? CritNudgeScale : 1f);
            float halfWidth = NumberHalfWidth(n.Chars, scale);
            float halfHeight = NumberHalfHeight * scale;
            float limit = Mathf.Max(0f, FieldHalfWidth - halfWidth - 8f);
            float floor = FieldBottom + halfHeight + NudgeGap;
            float reach = Mathf.Max(halfWidth, SidePushMax);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    var r = HudKeepOut(i);
                    float low = Mathf.Min(n.Position.y, n.Position.y + n.Rise) - halfHeight;
                    float high = Mathf.Max(n.Position.y, n.Position.y + n.Rise) + halfHeight;
                    if (high <= r.yMin - SideKeepGap || low >= r.yMax + SideKeepGap) continue;
                    float x0 = Mathf.Min(n.Position.x, n.Position.x + n.DriftX) - halfWidth;
                    float x1 = Mathf.Max(n.Position.x, n.Position.x + n.DriftX) + halfWidth;
                    if (x1 <= r.xMin - SideKeepGap || x0 >= r.xMax + SideKeepGap) continue;
                    // 칸 밑에서 뜬 숫자(시너지 칩 · 보스 도전 버튼 아래 — 치명타도)는 떠오름만 줄여 칸 밑에 머문다 (최소 16px) — 옆으로 밀면 맞은 적에서 멀어진다
                    float ceiling = r.yMin - SideKeepGap - halfHeight;
                    if (n.Position.y + MinNumberRise <= ceiling)
                    {
                        n.Rise = Mathf.Max(MinNumberRise, Mathf.Min(n.Rise, ceiling - n.Position.y));
                        continue;
                    }
                    float shift = r.center.x < 0f ? r.xMax + SideKeepGap - x0 : r.xMin - SideKeepGap - x1;
                    float x = Mathf.Clamp(n.Position.x + shift, -limit, limit);
                    if (Mathf.Abs(x - n.Position.x) <= reach)
                    {
                        n.Position.x = x;
                        continue;
                    }
                    // 너무 멀다: 칸 밑으로 내려 조금만 떠오른다 (바닥 밑이면 반폭만큼만 민다)
                    float under = ceiling - MinNumberRise;
                    if (under >= floor)
                    {
                        n.Position.y = Mathf.Min(n.Position.y, under);
                        n.Rise = MinNumberRise;
                    }
                    else n.Position.x = Mathf.Clamp(n.Position.x + Mathf.Clamp(shift, -reach, reach), -limit, limit);
                }
            }
        }

        /// <summary>
        /// 보스 이름표 묶음 비키기 (Wave 3 — r2 '50'이 '망각의 큰그림자' 이름표 위에 떴다): 숫자 상자(뜬 자리 ~ 떠오른 끝, 흐름 포함)가 살아 있는 보스의
        /// 이름표 + HP 바와 겹치면 (BattleKeepOut.NumberPlateClear) 그 밑에서 뜬 숫자는 떠오름을 이름표 아래 끝 − 4px까지 줄이고(최소 16px),
        /// 이름표 높이에서 뜬 숫자는 — 그 보스가 맞은 숫자(치명타 머리 위 쾅 등)면 이름표 위로(안전선 · 위쪽 HUD 칸 아래일 때, 아니면 이름표 바로 밑),
        /// 다른 적의 숫자면 이름표 옆(가까운 쪽, 전장 안)으로 옮긴다. 예전에는 보스 치명타가 모두 ±224px 옆으로 밀려 보스에서 떨어졌다.
        /// 막 뜬 숫자 · 떠 있다가 치명타로 바뀐 숫자에만 — 할당 없음
        /// </summary>
        void AvoidBossPlates(ref FloatingNumber n, float halfWidth, float halfHeight)
        {
            int count = PlateKeepOutCount;
            if (count == 0) return;
            float limit = Mathf.Max(0f, FieldHalfWidth - halfWidth - 8f);
            float size = Mathf.Max(1f, n.Scale);
            float ceiling = SafeTop - NumberTopMargin * size;
            float roof = SafeTop - NumberRoofMargin * size;
            var half = new Vector2(halfWidth, halfHeight);
            for (int i = 0; i < count; i++)
            {
                var plate = PlateKeepOut(i);
                bool own = n.TargetId != NoMerge && n.TargetId == PlateKeepOutOwner(i);
                float top = own ? HudRoofAbove(n.Position.x, n.DriftX, halfWidth, halfHeight, plate.yMax, roof) : roof;
                n.Position = BattleKeepOut.NumberPlateClear(n.Position, ref n.Rise, n.DriftX, half, plate, own, PlateClearance, MinNumberRise, ceiling, top, limit);
            }
        }

        /// <summary>
        /// 말풍선 비키기 (Wave 3 r3 → RM-2: 회복 · 피해 숫자가 말하는 친구 자기 말풍선으로 떠올라 글자를 덮었다 — '우리 식구, 모여♥14'):
        /// 떠 있는 단단한 말풍선(SkillCalloutView.SolidBubbleRects — 묶음 알파 ≥ 0.5 · 수명 0.75 전)을 다른 적의 이름표처럼 비킨다
        /// (BattleKeepOut.NumberPlateClear, own = false): 말풍선 밑에서 뜬 숫자는 떠오름을 말풍선 아래 끝 − 6px까지 줄이고(최소 16px),
        /// 말풍선 높이에서 뜬 숫자는 말풍선 옆으로. 막 뜬 숫자 · 치명타로 바뀐 숫자에만 — 할당 없음
        /// </summary>
        void AvoidBubbles(ref FloatingNumber n, float halfWidth, float halfHeight)
        {
            if (!_calloutViewSearched)
            {
                _calloutViewSearched = true;
                var root = _numberLayer != null ? _numberLayer.parent : null;
                _calloutView = root != null ? root.GetComponentInChildren<SkillCalloutView>(true) : null;
            }
            if (_calloutView == null) return;
            int count = _calloutView.SolidBubbleRects(_bubbleRects);
            if (count == 0) return;
            float limit = Mathf.Max(0f, FieldHalfWidth - halfWidth - 8f);
            float size = Mathf.Max(1f, n.Scale);
            float ceiling = SafeTop - NumberTopMargin * size;
            float roof = SafeTop - NumberRoofMargin * size;
            var half = new Vector2(halfWidth, halfHeight);
            for (int i = 0; i < count; i++)
                n.Position = BattleKeepOut.NumberPlateClear(n.Position, ref n.Rise, n.DriftX, half, _bubbleRects[i], false, BubbleClearance, MinNumberRise,
                    ceiling, roof, limit);
        }

        /// <summary>
        /// 이름표 위로 올린 숫자가 닿아도 되는 가장 높은 가운데: roof와, 숫자 가로 폭(흐름 포함)에 걸리고 높이 above보다 위로 뻗은 HUD 칸마다
        /// 그 아래 끝 − 6px − 반높이 중 낮은 쪽 (위쪽 HUD 밑으로 숨지 않게)
        /// </summary>
        float HudRoofAbove(float x, float driftX, float halfWidth, float halfHeight, float above, float roof)
        {
            int count = HudKeepOutCount;
            float x0 = Mathf.Min(x, x + driftX) - halfWidth;
            float x1 = Mathf.Max(x, x + driftX) + halfWidth;
            for (int i = 0; i < count; i++)
            {
                var r = HudKeepOut(i);
                if (r.yMax <= above || x1 <= r.xMin - SideKeepGap || x0 >= r.xMax + SideKeepGap) continue;
                roof = Mathf.Min(roof, r.yMin - SideKeepGap - halfHeight);
            }
            return roof;
        }

        /// <summary>
        /// 겹침 비키기에서 잴 숫자 배율: 자리 잡은 크기(치명타는 쾅·흔들림·별 배지 몫 1.25배)와 지금 그린 크기(쾅 1.8배 · 합침 톡) 중 큰 쪽, 최소 0.6 —
        /// 뜨는 프레임에 그린 크기(팝 0.55배)로 재면 곧 1.12배로 커지는 이웃과 겹친다. 뜨기 전 숫자는 DrawnScale이 0이라 자리 잡은 크기
        /// </summary>
        static float NudgeScale(in FloatingNumber n) => Mathf.Max(0.6f, Mathf.Max(n.Scale * (n.Crit ? CritNudgeScale : 1f), n.DrawnScale.y));

        /// <summary>
        /// 같은 대상의 숫자 둘이 겹쳐 비켰으면 함께 움직인다: 이웃의 흐름(DriftX)을 그대로 쓰고, 위로 비켰으면 떠오름도 같게 (같은 움직임 — 치명타끼리·보통끼리).
        /// 아래로 비킨 숫자는 CapRiseUnderNeighbours가 이웃이 아직 남긴 떠오름까지만 떠오르게 한다
        /// </summary>
        static void FollowNeighbour(ref FloatingNumber n, in FloatingNumber other, bool above)
        {
            n.DriftX = other.DriftX;
            if (above && n.Crit == other.Crit) n.Rise = other.Rise;
        }

        // ───────────── 알림 판 비키기 (판·글자 칸은 BattleFx.Callouts) ─────────────

        /// <summary>
        /// 이 칸(lane)에 떠 있는(또는 이번 프레임에 뜰) 알림 판의 자리: 가운데 = 알림 글자 자리, 반폭·반높이 = 판 크기 × 1.1 (없으면 false)
        /// </summary>
        bool CalloutPlate(int lane, out Vector2 center, out Vector2 half)
        {
            center = default;
            half = default;
            if (_calloutLaneOwner == null || lane < 0 || lane >= _calloutLaneOwner.Length) return false;
            int owner = _calloutLaneOwner[lane];
            if (owner < 0 || owner >= _numbers.Length) return false;
            ref var callout = ref _numbers[owner];
            if (!callout.Alive || !callout.Callout || callout.Lane != lane) return false;
            center = callout.Position;
            half = new Vector2(_calloutPlateWidth[lane] * 0.5f, CalloutPlateHeight * 0.5f) * CalloutPlateReach;
            return true;
        }

        /// <summary>x ± halfWidth와 가로로 겹치는 알림 판 중 가장 낮은 판의 아래 끝 y (없으면 float.MaxValue)</summary>
        float CalloutFloor(float x, float halfWidth)
        {
            float floor = float.MaxValue;
            for (int lane = 0; lane < CalloutLanes; lane++)
            {
                if (!CalloutPlate(lane, out var center, out var half) || Mathf.Abs(x - center.x) >= halfWidth + half.x) continue;
                floor = Mathf.Min(floor, center.y - half.y);
            }
            return floor;
        }

        /// <summary>
        /// 알림 글자가 뜨는 순간 (판·빛무리·글자·반짝은 숫자 레이어 맨 위로 올라간다): 이미 떠 있는 숫자를 판(리본 꼬리 포함 1.1배 + 12px) 둘레와 잰다 —
        /// 지금 자리부터 떠오른 끝 · 흐름 끝까지 (Wave 3 · fx2 leftovers).
        ///   · 판 밑에서 떠오르는 중이면 (치명타도 — 버팀 곡선 그대로) 떠오른 끝을 판 아래 끝 − 반높이 − 4px로 (지금 높이에서 이어지게 — RetargetRise).
        ///   · 이미 판에 걸렸으면 판 가운데에서 먼 쪽 옆으로 남은 수명 동안 흘러 나가고(RetargetDrift), 나가는 동안 판 위로 올려 그린다
        ///     (켜진 치명타 광택도 그 숫자 위로). 옆으로 갈 자리가 없으면 예전처럼 판 위로 올리기만.
        /// 알림이 떠 있는 동안 새로 뜨는 숫자는 NudgeNumber가 판 밑에 둔다
        /// </summary>
        void RaiseNumbersOverCallout(int lane)
        {
            if (!CalloutPlate(lane, out var center, out var half)) return;
            float left = center.x - half.x - CalloutPad;
            float right = center.x + half.x + CalloutPad;
            float bottom = center.y - half.y - CalloutPad;
            float top = center.y + half.y + CalloutPad;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var other = ref _numbers[i];
                if (!other.Alive || !other.Shown || other.Callout) continue;
                float scale = Mathf.Max(0.6f, Mathf.Max(other.Scale, other.DrawnScale.y));
                float halfWidth = NumberHalfWidth(other.Chars, scale);
                float halfHeight = NumberHalfHeight * scale;
                float endX = other.Position.x + other.DriftX;
                if (Mathf.Max(other.Drawn.x, endX) + halfWidth <= left || Mathf.Min(other.Drawn.x, endX) - halfWidth >= right) continue;
                // 지금 자리부터 떠오른 끝까지
                float endY = other.Position.y + other.Rise;
                if (Mathf.Max(other.Drawn.y, endY) + halfHeight <= bottom || Mathf.Min(other.Drawn.y, other.Position.y) - halfHeight >= top) continue;
                bool below = other.Drawn.y + halfHeight <= bottom;
                if (below)
                {
                    RetargetRise(ref other, bottom - halfHeight - CalloutClearance);
                    continue;
                }
                if (!below)
                {
                    float limit = Mathf.Max(0f, FieldHalfWidth - halfWidth - 8f);
                    bool toRight = other.Drawn.x >= center.x;
                    float target = toRight ? right + halfWidth : left - halfWidth;
                    if (Mathf.Abs(target) > limit) target = toRight ? left - halfWidth : right + halfWidth;
                    if (Mathf.Abs(target) <= limit) RetargetDrift(ref other, target);
                }
                other.Rect.SetAsLastSibling();
                int gloss = other.Gloss;
                if (gloss >= 0 && _glossOwner[gloss] == i && _glossImages[gloss] != null && _glossImages[gloss].enabled) _glossImages[gloss].rectTransform.SetAsLastSibling();
            }
        }

        static float NumberHalfWidth(int chars, float scale) => Mathf.Max(1, chars) * NumberCharWidth * NumberFontSize * 0.5f * Mathf.Max(0.6f, scale);

        /// <summary>
        /// 숫자 한 칸 그리기: 뜻별 팝(치명타 쾅 + 흔들림) × 합침 톡 · 떠오름 · 바깥 흐름 · 뜬 직후 가로 떨림 · 스며 나옴/사라짐 (묶음 알파 — 글자 메시는 그대로).
        /// 그린 자리·배율은 Drawn·DrawnScale에 담아 둔다
        /// </summary>
        void DriveNumber(ref FloatingNumber n)
        {
            float age = n.Age;
            float squashX = 1f;
            float squashY = 1f;
            float pop = n.Crit ? CritSlamScale(age, out squashX, out squashY) : NumberPopScale(n.Kind, age);
            if (n.Repop > 0f) pop *= 1f + (n.Bump - 1f) * RepopEnvelope(1f - n.Repop);
            float scale = n.Scale * pop;
            n.DrawnScale = new Vector2(scale * squashX, scale * squashY);
            n.Rect.localScale = new Vector3(n.DrawnScale.x, n.DrawnScale.y, 1f);
            if (n.Crit) n.Rect.localRotation = Quaternion.Euler(0f, 0f, CritWiggle(age));

            float k = Mathf.Clamp01(age / Mathf.Max(0.01f, n.Life));
            float x = n.DriftX * BattleEase.OutCubic(k);
            if (n.Shake > 0f && age < NumberShakeTime) x += n.Shake * Mathf.Sin(age * NumberShakeHz * 2f * Mathf.PI) * (1f - age / NumberShakeTime);
            n.Drawn = n.Position + new Vector2(x, NumberRise(in n, k));
            n.Rect.anchoredPosition = n.Drawn;

            float fadeIn = NumberMotions[(int)n.Kind].FadeIn;
            float alpha = fadeIn > 0f && !n.Plain ? Mathf.Clamp01(age / fadeIn) : 1f;
            if (age > n.FadeAt) alpha *= 1f - Mathf.SmoothStep(0f, 1f, (age - n.FadeAt) / Mathf.Max(0.01f, n.End - n.FadeAt));
            // 잠들 때 0.2초에 걸쳐 사라진다 (RI-6b — 평소 1)
            alpha *= _sleepNumberAlpha;
            SetNumberAlpha(ref n, alpha);
        }

        /// <summary>떠오름: 보통은 OutCubic. 치명타는 280ms 동안 18px만 (꽝 하고 버틴다) 그 뒤 끝까지</summary>
        static float NumberRise(in FloatingNumber n, float k)
        {
            if (!n.Crit) return n.Rise * BattleEase.OutCubic(k);
            return RiseAt(true, n.Age, n.Life, n.Rise);
        }

        /// <summary>
        /// 뜬 자리에서 떠오른 높이 (px): 보통 rise × OutCubic(age / life), 치명타는 280ms 동안 min(18, rise/4)만 OutQuad로 버틴 뒤 끝까지 OutCubic.
        /// RetargetRise가 같은 곡선으로 푼다 (테스트용 공개)
        /// </summary>
        internal static float RiseAt(bool crit, float age, float life, float rise)
        {
            if (!crit) return rise * BattleEase.OutCubic(Mathf.Clamp01(age / Mathf.Max(0.01f, life)));
            float hold = Mathf.Min(CritHoldRise, rise * 0.25f);
            if (age < CritHoldTime) return hold * JuiceEase.OutQuad(age / CritHoldTime);
            return hold + (rise - hold) * BattleEase.OutCubic(Mathf.Clamp01((age - CritHoldTime) / Mathf.Max(0.01f, life - CritHoldTime)));
        }

        // ───────────── 치명타 배지 · 광택 ─────────────

        /// <summary>치명타 숫자가 뜨는 순간: 숫자 뒤에 금빛 별 배지(crit_star_badge 90 → 150px, 가산 0.5, 0.35초)와 숫자 위 광택 훑기 예약</summary>
        void CritNumberShown(int index)
        {
            ref var n = ref _numbers[index];
            var badgeArt = _juice != null ? _juice.CritStarBadge : null;
            int badge = Spawn(badgeArt != null ? badgeArt : Motif(VfxMotif.Star), badgeArt != null ? Color.white : UITheme.Gold, n.Drawn + Vector2.up * 2f,
                CritBadgeFrom, CritBadgeTo, CritBadgeLife, 0f);
            if (badge >= 0)
            {
                ref var p = ref _particles[badge];
                p.Add = 0.5f;
                p.Rotation = Random.Range(-14f, 14f);
                p.Spin = Random.Range(-30f, 30f);
                p.P1 = Vector2.up * CritHoldRise;
                p.FadeIn = 0.02f;
                p.FadeOut = 0.57f;
                p.FadeKind = FadeInQuad;
            }
            if (n.Gloss < 0) n.Gloss = TakeGloss(index);
        }

        int TakeGloss(int owner)
        {
            if (_glossImages == null || _glossImages.Length == 0) return -1;
            int slot = _glossCursor;
            _glossCursor = (_glossCursor + 1) % _glossImages.Length;
            int previous = _glossOwner[slot];
            if (previous >= 0 && previous != owner && previous < _numbers.Length && _numbers[previous].Gloss == slot) _numbers[previous].Gloss = -1;
            _glossOwner[slot] = owner;
            if (_glossImages[slot] != null) _glossImages[slot].enabled = false;
            return slot;
        }

        void ReleaseGloss(int slot, int owner)
        {
            if (_glossImages == null || slot < 0 || slot >= _glossImages.Length || _glossOwner[slot] != owner) return;
            _glossOwner[slot] = -1;
            if (_glossImages[slot] != null) _glossImages[slot].enabled = false;
        }

        /// <summary>광택 훑기: +90ms부터 160ms 동안 숫자 왼쪽 끝 → 오른쪽 끝 (vfx_gloss_shine 60px, 가산 0.6, sin 세기)</summary>
        void DriveGloss(int index)
        {
            ref var n = ref _numbers[index];
            int slot = n.Gloss;
            var image = _glossImages[slot];
            if (image == null || _glossOwner[slot] != index)
            {
                n.Gloss = -1;
                return;
            }
            float t = n.Age - GlossStart;
            if (t < 0f)
            {
                if (image.enabled) image.enabled = false;
                return;
            }
            float u = t / GlossTime;
            if (u >= 1f)
            {
                ReleaseGloss(slot, index);
                n.Gloss = -1;
                return;
            }
            var scale = n.DrawnScale;
            float width = NumberWidth(ref n) * scale.x;
            var rect = image.rectTransform;
            rect.anchoredPosition = n.Drawn + new Vector2(Mathf.Lerp(-0.5f, 0.5f, BattleEase.InOutSine(u)) * width, 4f * scale.y);
            float size = GlossSize * Mathf.Max(0.8f, scale.y / NumberScale(DamageNumberKind.Crit));
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, -18f + 36f * u);
            if (!image.enabled)
            {
                image.enabled = true;
                rect.SetAsLastSibling();
            }
            WriteColor(image, JuiceColors.CoreCream, Mathf.Sin(u * Mathf.PI) * 0.95f * n.Alpha, 0.6f);
        }

        /// <summary>글자 폭 (로컬, 배율 전): 그려진 글자 경계, 아직 없으면 글자 수 × 0.58em</summary>
        static float NumberWidth(ref FloatingNumber n)
        {
            float width = n.Text.textBounds.size.x;
            return width > 1f ? width : n.Chars * 0.58f * n.Text.fontSize;
        }

        static Color NumberHex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
