using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전장 배너: 스테이지 시작 / 실패 / 보스 등장은 가운데, 스테이지 클리어·보스 처치·불 밝히기(두 번째부터)는
    /// HUD 아래 빈 바닥의 작은 리본으로 짧게 (UX-01 — 전장 한가운데를 오래 가리지 않게, 보상은 리본 아래 한 줄로).
    /// 여러 개가 겹치면 순서대로 보여준다.
    /// <para>
    /// 작은 리본 자리 (M67 · UX-SPEC-IA §8.3): 배너 띠(BannerArea) 위 끝에서 RibbonDrop(293)px 아래가 가운데 — HUD 아래 끝 218 + 간격 24 + 리본 반 높이 51.
    /// 띠 위 끝은 MAIN · 시트 모두 StageArea 위 끝이라 기준 화면 372~474에 머문다 (시너지 줄 · 보스 도전 버튼과 가로로 비켜 있다).
    /// 팝업이 열려 있으면 새 리본은 버리고, 떠 있던 리본은 0.15초에 걷는다 (M21 — 보상 날아가기는 StageClearRewardFlyer가 따로 건너뛴다).
    /// </para>
    /// <para>
    /// 부제는 Cream + 공유 외곽선 재질 (빌더)이고 자기 받침(SubtitlePlate — 보상 줄과 같은 어두운 바느질 알약)에 얹혀 리본 아트 아래 금실
    /// (배너 y 44~50) 바로 밑에 매달린다: 받침 위 끝 = 배너 아래 끝에서 SubtitlePlateTop(40). 예전엔 부제 칸이 금실 · 바느질 선에 걸쳐 선이 글자를
    /// 가로지르고 반쯤은 전장 그림 위에 떠 있었다 (검증 w3 r1). 받침은 부제가 있을 때만 켜고 글 폭에 맞추며 배너 띠(keep-out)에 들어간다.
    /// 작은 리본의 부제(보상 한 줄)는 0.6배로 줄어들므로 37px(화면에서 약 22px)로 그린다.
    /// </para>
    /// <para>
    /// 보스에 막혀 내려감 (M13): BattleHud가 BattleSystem.StepBack에서 ArmStepBack을 부르면 같은 프레임에 오는 실패 배너(Alert) 하나를
    /// '아쉬워요! 1-9에서 힘을 모아요' + 누를 수 있는 부제('강해지는 방법 보기 ›', 약 3초)로 바꿔 단다.
    /// </para>
    /// <para>
    /// 배너 띠 (keep-out — Wave 3 WP-5A ↔ WP-5B 약속): 배너가 떠 있는 동안(나타남 · 머무름 · 사라짐) KeepOutActive = true, KeepOutWorld = 배너
    /// 사각형 + 부제 받침 + 보상 줄 알약들의 합집합 (Battle 캔버스 월드 좌표, 매 프레임 지금 크기 · 자리 그대로). 전장 이름표 · 체력 막대 · 말풍선은
    /// OverlapsKeepOut / TryGetKeepOut으로 이 자리를 비킨다(흐리거나 옮긴다). 그리는 순서는 원래 배너가 위다 (BannerArea가 Field 뒤 형제).
    /// 작은 리본은 언제나 띠 위 끝에서 RibbonDrop 아래 · 가로 가운데 516x102 (= 860x170 × RibbonScale), 가운데 배너는 보이는 띠 가운데 860x170 —
    /// 미리 비켜 두고 싶은 쪽은 이 상수로 자리를 잡는다. 할당 없음 (정적 모서리 배열 하나).
    /// </para>
    /// <para>
    /// 제목 칸 (검증 rm2 r1): 밤 리본 아트(768x192, 9-slice 좌우 200 · 배율 1.4 → 화면 143)는 양끝 ≈ 77px가 갈라진 꼬리 · 접힘 · 금별 장식이다.
    /// 제목은 좌우 TitleSideInset(100) 안 — 860 배너에서 칸 660 — 에 자동 크기(TitleMinSize 40 ~ 58)로 맞춰 평평한 가운데 띠에만 놓인다
    /// (예전 좌우 30이면 '수선점이 위험해요… 다시 도전!'의 '!'가 오른쪽 접힘 · 별에 걸리고 '아쉬워요! …'의 왼쪽 끝이 꼬리에 닿았다).
    /// 장면을 다시 굽기 전(좌우 30인 씬)에도 깨어날 때 같은 여백으로 고친다 (ApplyTitleInset).
    /// </para>
    /// </summary>
    public class BattleBannerView : MonoBehaviour
    {
        public enum Style
        {
            /// <summary>작고 짧은 안내 (스테이지 시작)</summary>
            Info,
            /// <summary>금빛 빛살 + 보상 (클리어)</summary>
            Celebrate,
            /// <summary>붉게 흔들림 (실패 / 보스)</summary>
            Alert,
            /// <summary>HUD 아래 빈 바닥의 작은 리본 (클리어 · 보스 처치 · 불 밝히기): 0.6배 크기, 짧게, 보상은 부제 한 줄로</summary>
            Ribbon,
        }

        /// <summary>작은 리본 크기 배율 (860x170 → 516x102)</summary>
        public const float RibbonScale = 0.6f;
        /// <summary>작은 리본 가운데가 배너 띠 위 끝(= StageArea 위 끝)에서 이만큼 아래 (HUD 아래 끝 218 + 간격 24 + 반 높이 51, UX-SPEC-IA §8.3)</summary>
        public const float RibbonDrop = 293f;
        /// <summary>배너 크기 (빌더의 Banner 사각형과 같다 — keep-out을 미리 잡는 쪽이 쓴다)</summary>
        public static readonly Vector2 BannerSize = new(860f, 170f);
        /// <summary>
        /// 리본 아트 양끝의 꼬리 · 접힘 · 금별 장식 폭 (배너 좌표, 아트 x ≈ 108 / 1.4 — 앞 띠 금테 안쪽이 시작하는 곳). 제목은 이 안쪽에만
        /// </summary>
        public const float RibbonTailWidth = 77f;
        /// <summary>제목 칸 좌우 여백 (꼬리 77 + 금테에서 띄우는 23) — 860 배너에서 제목 칸 660</summary>
        public const float TitleSideInset = 100f;
        /// <summary>제목 자동 크기 하한 (팝업 제목 크기 — 긴 실패 문구도 이보다 작아지지 않는다. 안내 배너는 최대 44라 하한도 44 이하)</summary>
        public const float TitleMinSize = UIType.PopupTitle;
        /// <summary>팝업이 열려 떠 있던 리본을 걷는 시간 (M21)</summary>
        const float PopupFade = 0.15f;
        /// <summary>누를 수 있는 배너(보스에 막혀 내려감)가 머무는 시간 — 나타남 0.38 · 사라짐 0.3을 더해 약 3초 동안 부제를 누를 수 있다</summary>
        const float TapHold = 2.3f;
        /// <summary>작은 리본 부제 크기 (0.6배로 줄어 화면에서 약 22px — Caption)</summary>
        internal const float RibbonSubtitleSize = 37f;
        /// <summary>
        /// 작은 리본 부제 줄임 하한 (FitRibbonSubtitle): 보상이 넷(단추 · 별빛 사탕 · 수선 천 · 리본 조각)이면 37로는 780 칸을 넘친다(≈ 1,020)
        /// → 27까지 줄인다 (× 0.6 = 화면 16.2 ≥ UIType.AutoSizeFloor)
        /// </summary>
        internal const float RibbonSubtitleMin = 27f;
        const float SubtitleSize = 24f;
        /// <summary>
        /// 부제 받침 · 부제 칸의 위 끝 (배너 아래 끝에서 위로, 둘 다 피벗 위 가운데). 리본(배너 y 16~154, 9-slice 배율 1.4)의 아래 금실은 y 44~50,
        /// 그 밑 그림자 끝이 ≈ 42 — 받침은 그 바로 밑에서 아래로 매달린다 (빌더 SubtitlePlate · Subtitle)
        /// </summary>
        public const float SubtitlePlateTop = 40f;
        /// <summary>부제 받침 높이: 가운데 배너(부제 24) 44 · 작은 리본(부제 37~27, 0.6배 → 화면 약 32) 54</summary>
        public const float SubtitlePlateHeight = 44f;
        internal const float RibbonSubtitlePlateHeight = 54f;
        /// <summary>부제 받침 폭 = 글 폭 + SubtitlePlatePad, SubtitlePlateMin ~ SubtitlePlateMax (부제 칸 780 + 48 — 배너 폭 860 안)</summary>
        internal const float SubtitlePlatePad = 48f;
        internal const float SubtitlePlateMin = 220f;
        internal const float SubtitlePlateMax = 828f;
        /// <summary>받침 알약 그림(ui_pill_night_9s 192x96) 높이 — 높이가 바뀌어도 끝이 둥근 캡슐로 남게 9-slice 배율 = 96 / 높이</summary>
        const float PlateArtHeight = 96f;

        sealed class Request
        {
            public string Title;
            public string Subtitle;
            public Color Color;
            public Style Style;
            public float Hold;
            /// <summary>부제를 누르면 부를 일 (null이면 누를 수 없음)</summary>
            public Action Tap;
            public readonly List<string> Lines = new();
            public readonly List<Color> LineColors = new();
            public readonly List<Sprite> LineIcons = new();
            /// <summary>줄 아이콘에 곱할 색 (실타래 임시 그림 = 산호, 그 밖 흰색, UITheme.CurrencyIconTint)</summary>
            public readonly List<Color> LineIconTints = new();
        }

        [SerializeField] RectTransform _banner;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Image _ribbon;
        [SerializeField] Image _ribbonEdge;
        [SerializeField] Image _glow;
        [SerializeField] Image _rays;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [Tooltip("부제 받침 (어두운 바느질 알약, 리본 아래 금실 바로 밑 — 부제가 있을 때만 켜고 글 폭에 맞춘다)")]
        [SerializeField] Image _subtitlePlate;
        [Tooltip("부제 누르기 (보스에 막혀 내려갈 때만 켠다 — 그 배너 동안 배너 묶음이 누름을 받고 부제 글자만 누르는 자리가 된다)")]
        [SerializeField] Button _subtitleButton;
        [SerializeField] TMP_Text[] _lines;
        [Tooltip("보상 줄 바탕 (줄마다 하나, 페이드/팝 대상)")]
        [SerializeField] CanvasGroup[] _lineGroups;
        [Tooltip("보상 줄 왼쪽 아이콘 (줄마다 하나, 없으면 글자만)")]
        [SerializeField] Image[] _lineIcons;
        [Tooltip("CurrencyType 순서의 재화 아이콘 (단추 · 별조각 · 별빛 사탕)")]
        [SerializeField] Sprite[] _currencyIcons;
        [Tooltip("재료 그림이 없는 보상의 기본 아이콘")]
        [SerializeField] Sprite _rewardIcon;
        [Tooltip("true = 리본이 금테 아트라 흰색에 요청 색을 살짝만 섞는다 (어두운 Panel 색을 곱하면 금테가 탁해진다)")]
        [SerializeField] bool _ribbonIsArt;

        readonly Queue<Request> _queue = new();
        Coroutine _routine;
        Action _tap;

        /// <summary>ArmStepBack을 부른 프레임 (그 프레임의 Alert 하나만 바꾼다)</summary>
        int _armFrame = -1;
        string _armTitle;
        string _armSubtitle;
        Color _armColor;
        Action _armTap;

        // ───────────── 배너 띠 (keep-out) ─────────────

        static readonly Vector3[] Corners = new Vector3[4];
        static bool s_keepOutActive;
        static Rect s_keepOutWorld;

        /// <summary>배너가 떠 있는지 (나타남 · 머무름 · 사라짐 동안). false면 KeepOutWorld는 의미 없다</summary>
        public static bool KeepOutActive => s_keepOutActive;

        /// <summary>떠 있는 배너 + 보상 줄이 차지하는 자리 (Battle 캔버스 월드 좌표의 축 정렬 사각형, 매 프레임 지금 크기 · 자리)</summary>
        public static Rect KeepOutWorld => s_keepOutWorld;

        /// <summary>월드 사각형이 배너 자리와 겹치는지 (pad = 월드 단위 여유). 배너가 없으면 false</summary>
        public static bool OverlapsKeepOut(Rect worldRect, float pad = 0f)
        {
            if (!s_keepOutActive) return false;
            var r = s_keepOutWorld;
            return worldRect.xMax > r.xMin - pad && worldRect.xMin < r.xMax + pad
                && worldRect.yMax > r.yMin - pad && worldRect.yMin < r.yMax + pad;
        }

        /// <summary>RectTransform(지금 월드 모서리)이 배너 자리와 겹치는지. 배너가 없거나 target이 없으면 false</summary>
        public static bool OverlapsKeepOut(RectTransform target, float pad = 0f)
        {
            if (!s_keepOutActive || target == null) return false;
            return OverlapsKeepOut(WorldRect(target), pad);
        }

        /// <summary>
        /// 배너 자리를 space의 로컬 좌표(축 정렬)로 (예: 말풍선 레이어 Callouts, 이름표가 있는 Actors — 카메라 로컬). 배너가 없으면 false.
        /// 회전 없는 계층을 가정한다 (화면 흔들림의 아주 작은 기울기는 무시).
        /// </summary>
        public static bool TryGetKeepOut(RectTransform space, out Rect local)
        {
            local = default;
            if (!s_keepOutActive || space == null) return false;
            Vector2 a = space.InverseTransformPoint(new Vector3(s_keepOutWorld.xMin, s_keepOutWorld.yMin, 0f));
            Vector2 b = space.InverseTransformPoint(new Vector3(s_keepOutWorld.xMax, s_keepOutWorld.yMax, 0f));
            local = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return true;
        }

        /// <summary>
        /// 작은 리본이 놓이는 자리 (배너 띠 로컬, 가운데 원점 — 띠 높이 areaHeight). 가운데 y = areaHeight/2 − RibbonDrop, 크기 516x102.
        /// 순수 계산 (테스트 · 미리 비키는 쪽용)
        /// </summary>
        public static Rect RibbonRectInArea(float areaHeight)
        {
            var size = BannerSize * RibbonScale;
            float centerY = RibbonCenterY(areaHeight);
            return new Rect(-size.x * 0.5f, centerY - size.y * 0.5f, size.x, size.y);
        }

        /// <summary>작은 리본 가운데 y (배너 띠 가운데 기준): 띠 위 끝에서 RibbonDrop 아래. 띠 높이를 모르면(0 이하) −50</summary>
        public static float RibbonCenterY(float areaHeight) => areaHeight > 0f ? areaHeight * 0.5f - RibbonDrop : -50f;

        static Rect WorldRect(RectTransform rt)
        {
            rt.GetWorldCorners(Corners);
            return Rect.MinMaxRect(
                Mathf.Min(Corners[0].x, Corners[2].x), Mathf.Min(Corners[0].y, Corners[2].y),
                Mathf.Max(Corners[0].x, Corners[2].x), Mathf.Max(Corners[0].y, Corners[2].y));
        }

        /// <summary>정적 상태는 장면을 다시 불러와도 남으므로(PlayMode 테스트) 처음에 지운다</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_keepOutActive = false;
            s_keepOutWorld = default;
        }

        void Awake()
        {
            _banner.gameObject.SetActive(false);
            if (_title != null) ApplyTitleInset(_title.rectTransform, TitleSideInset);
            if (_subtitleButton != null) _subtitleButton.onClick.AddListener(OnSubtitleTap);
            SetTappable(null);
        }

        void OnDisable()
        {
            _routine = null;
            _queue.Clear();
            _banner.gameObject.SetActive(false);
            SetTappable(null);
            s_keepOutActive = false;
        }

        public void Show(string title, string subtitle, Color color, Style style, float hold)
        {
            Enqueue(new Request { Title = title, Subtitle = subtitle, Color = color, Style = style, Hold = hold });
        }

        /// <summary>보상 줄을 붙인 배너 (클리어/실패). 작은 리본이면 보상을 부제 한 줄로 이어 붙인다 ("+ 단추 120 · + 별빛 사탕 1")</summary>
        public void ShowWithRewards(string title, string subtitle, Color color, Style style, float hold, List<RewardData> rewards)
        {
            // 팝업이 열려 있으면 작은 리본은 보상 줄을 만들기 전에 버린다 (M21)
            if (style == Style.Ribbon && PopupOpen) return;
            var request = new Request { Title = title, Subtitle = subtitle, Color = color, Style = style, Hold = hold };
            if (rewards != null && Game.IsReady && Game.Rewards != null)
            {
                System.Text.StringBuilder inline = style == Style.Ribbon ? new System.Text.StringBuilder() : null;
                foreach (var reward in RewardService.Merge(rewards))
                {
                    if (inline != null)
                    {
                        if (inline.Length > 0) inline.Append("  ·  ");
                        inline.Append("+ ").Append(Game.Rewards.Describe(reward));
                        continue;
                    }
                    if (request.Lines.Count >= _lines.Length) break;
                    request.Lines.Add("+ " + Game.Rewards.Describe(reward));
                    request.LineColors.Add(reward.type == RewardType.Currency ? UITheme.CurrencyColor(reward.currency) : UITheme.Cream);
                    request.LineIcons.Add(RewardIcon(reward));
                    request.LineIconTints.Add(reward.type == RewardType.Currency ? UITheme.CurrencyIconTint(reward.currency) : Color.white);
                }
                if (inline != null && inline.Length > 0) request.Subtitle = inline.ToString();
            }
            Enqueue(request);
        }

        /// <summary>
        /// 다음 실패 배너(Alert)를 바꿔 단다 — 보스에 막혀 한 단계 내려갈 때 (BattleHud가 BattleSystem.StepBack에서 부른다).
        /// StepBack은 같은 실패의 StageEnded 바로 앞에 울리므로 같은 프레임에 오는 Alert 하나만 바꾸고, 프레임이 지나면 버린다:
        /// 제목 · 부제 · 색을 바꾸고 부제를 누를 수 있게 한다(tap, 약 3초). 보상 줄은 그대로. 실패 배너가 오지 않으면(시험장 등) 아무 일도 없다.
        /// </summary>
        public void ArmStepBack(string title, string subtitle, Color color, Action tap)
        {
            _armFrame = Time.frameCount;
            _armTitle = title;
            _armSubtitle = subtitle;
            _armColor = color;
            _armTap = tap;
        }

        /// <summary>보상 줄 아이콘: 재화 아이콘 / 재료 그림 / 기본 보상 아이콘</summary>
        Sprite RewardIcon(RewardData reward)
        {
            if (reward.type == RewardType.Currency)
            {
                int index = (int)reward.currency;
                var currency = _currencyIcons != null && index >= 0 && index < _currencyIcons.Length ? _currencyIcons[index] : null;
                return currency != null ? currency : _rewardIcon;
            }
            var item = Game.IsReady && Game.Items != null ? Game.Items.Get(reward.itemId) : null;
            return item != null && item.icon != null ? item.icon : _rewardIcon;
        }

        /// <summary>대기 중인 안내 배너(Info)만 버린다</summary>
        public void DropPendingInfo()
        {
            if (_queue.Count == 0) return;
            int count = _queue.Count;
            for (int i = 0; i < count; i++)
            {
                var request = _queue.Dequeue();
                if (request.Style != Style.Info) _queue.Enqueue(request);
            }
        }

        /// <summary>팝업이 열려 있는지 (M21 — 작은 리본을 띄우지 않는다)</summary>
        static bool PopupOpen => UIManager.Instance != null && UIManager.Instance.HasOpenPopup;

        void Enqueue(Request request)
        {
            if (request.Style == Style.Ribbon && PopupOpen) return;
            if (request.Style == Style.Alert && _armFrame == Time.frameCount)
            {
                _armFrame = -1;
                request.Title = _armTitle;
                request.Subtitle = _armSubtitle;
                request.Color = _armColor;
                request.Tap = _armTap;
                if (_armTap != null) request.Hold = Mathf.Max(request.Hold, TapHold);
                _armTap = null;
            }
            // 같은 안내가 연달아 쌓이지 않도록 대기 중인 Info는 최신 것만 남긴다
            if (request.Style == Style.Info) DropPendingInfo();
            _queue.Enqueue(request);
            if (_routine == null && isActiveAndEnabled) _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (_queue.Count > 0) yield return Play(_queue.Dequeue());
            _banner.gameObject.SetActive(false);
            s_keepOutActive = false;
            _routine = null;
        }

        IEnumerator Play(Request request)
        {
            bool info = request.Style == Style.Info;
            bool ribbon = request.Style == Style.Ribbon;
            // 기다리는 사이 팝업이 열렸으면 작은 리본은 건너뛴다 (M21)
            if (ribbon && PopupOpen) yield break;
            // 작은 리본은 HUD 아래 빈 바닥 가운데에 작게 (가운데 전장을 가리지 않게)
            float baseScale = ribbon ? RibbonScale : 1f;
            var basePosition = ribbon ? new Vector2(0f, RibbonHeight()) : Vector2.zero;
            _title.text = request.Title;
            // 긴 문구(실패 안내 등)는 자동 크기로 배너 안에 맞춘다
            _title.fontSizeMax = info ? 44f : 58f;
            _title.fontSizeMin = Mathf.Min(TitleMinSize, _title.fontSizeMax);
            _title.fontSize = _title.fontSizeMax;
            _title.color = request.Style == Style.Celebrate || ribbon ? UITheme.Cream : Color.Lerp(request.Color, Color.white, 0.55f);
            _subtitle.text = request.Subtitle ?? string.Empty;
            // 작은 리본은 0.6배로 줄어드므로 부제(보상 한 줄)를 키워 둔다 (화면에서 약 22px) — 보상이 많아 칸(780)을 넘치면 배너를 켠 뒤
            // FitRibbonSubtitle이 27까지 줄인다. 가운데 배너는 예전처럼 24 고정. 누를 수 있는 부제는 금빛 (가기 › 글자)
            _subtitle.fontSize = ribbon ? RibbonSubtitleSize : SubtitleSize;
            _subtitle.color = request.Tap != null ? UITheme.Gold : UITheme.Cream;
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(request.Subtitle));
            SetTappable(request.Tap);
            _ribbon.color = _ribbonIsArt ? Color.Lerp(Color.white, request.Color, 0.2f)
                : Color.Lerp(UITheme.Panel, request.Color, info ? 0.25f : ribbon ? 0.32f : 0.4f);
            if (_ribbonEdge != null) _ribbonEdge.color = BattleEase.WithAlpha(request.Color, 0.9f);
            _glow.color = BattleEase.WithAlpha(request.Color, 0f);
            _rays.gameObject.SetActive(request.Style == Style.Celebrate);
            _rays.color = BattleEase.WithAlpha(UITheme.Lamp, 0f);
            for (int i = 0; i < _lines.Length; i++)
            {
                bool used = i < request.Lines.Count;
                LineRoot(i).gameObject.SetActive(used);
                if (!used) continue;
                _lines[i].text = request.Lines[i];
                _lines[i].color = request.LineColors[i];
                if (_lineIcons != null && i < _lineIcons.Length && _lineIcons[i] != null)
                {
                    var icon = i < request.LineIcons.Count ? request.LineIcons[i] : null;
                    _lineIcons[i].sprite = icon;
                    _lineIcons[i].color = i < request.LineIconTints.Count ? request.LineIconTints[i] : Color.white;
                    _lineIcons[i].enabled = icon != null;
                }
                SetLine(i, 0f, 0.6f);
            }
            _banner.gameObject.SetActive(true);
            if (ribbon) FitRibbonSubtitle();
            FitSubtitlePlate(ribbon);
            _banner.anchoredPosition = basePosition;
            float glowPeak = ribbon ? 0.35f : 0.6f;
            int lines = request.Lines.Count;

            // 등장
            float inTime = ribbon ? 0.26f : 0.38f;
            for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
            {
                if (ribbon && PopupOpen)
                {
                    yield return FadeAway(basePosition, baseScale, lines);
                    yield break;
                }
                float k = t / inTime;
                float scale = Mathf.LerpUnclamped(0.55f, 1f, BattleEase.OutBack(k, 2.4f)) * baseScale;
                _banner.localScale = new Vector3(scale, scale, 1f);
                _group.alpha = Mathf.Clamp01(k * 2.5f);
                _glow.color = BattleEase.WithAlpha(request.Color, glowPeak * k);
                ApplyAlert(request.Style, k);
                UpdateKeepOut(lines);
                yield return null;
            }
            _banner.localScale = new Vector3(baseScale, baseScale, 1f);
            _group.alpha = 1f;

            // 유지 (+ 보상 줄이 하나씩 톡톡 떠오름)
            float hold = request.Hold + lines * 0.25f;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime)
            {
                if (ribbon && PopupOpen)
                {
                    yield return FadeAway(basePosition, baseScale, lines);
                    yield break;
                }
                float time = Time.unscaledTime;
                _glow.color = BattleEase.WithAlpha(request.Color, (ribbon ? 0.25f : 0.45f) + 0.15f * Mathf.Sin(time * 4f));
                if (request.Style == Style.Celebrate)
                {
                    _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 25f);
                    _rays.color = BattleEase.WithAlpha(UITheme.Lamp, Mathf.Clamp01(t * 3f) * 0.55f);
                }
                for (int i = 0; i < lines && i < _lines.Length; i++)
                {
                    float local = Mathf.Clamp01((t - 0.15f - i * 0.18f) / 0.3f);
                    SetLine(i, local, Mathf.LerpUnclamped(0.6f, 1f, BattleEase.OutBack(local, 3f)));
                }
                if (request.Style == Style.Alert && t < 0.5f) ApplyAlert(request.Style, 1f - t * 2f);
                UpdateKeepOut(lines);
                yield return null;
            }

            // 퇴장
            float outTime = ribbon ? 0.24f : 0.3f;
            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
            {
                float k = t / outTime;
                _group.alpha = 1f - k;
                float scale = (1f + 0.06f * k) * baseScale;
                _banner.localScale = new Vector3(scale, scale, 1f);
                _banner.anchoredPosition = basePosition + new Vector2(0f, 30f * BattleEase.OutCubic(k));
                UpdateKeepOut(lines);
                yield return null;
            }
            _group.alpha = 0f;
            SetTappable(null);
            s_keepOutActive = false;
        }

        /// <summary>팝업이 열려 떠 있던 리본을 PopupFade초에 걷는다 (줄이기면 바로)</summary>
        IEnumerator FadeAway(Vector2 basePosition, float baseScale, int lines)
        {
            float from = _group.alpha;
            if (!UIMotion.Reduced)
            {
                for (float t = 0f; t < PopupFade; t += Time.unscaledDeltaTime)
                {
                    _group.alpha = from * (1f - t / PopupFade);
                    _banner.localScale = new Vector3(baseScale, baseScale, 1f);
                    _banner.anchoredPosition = basePosition;
                    UpdateKeepOut(lines);
                    yield return null;
                }
            }
            _group.alpha = 0f;
            SetTappable(null);
            s_keepOutActive = false;
        }

        /// <summary>
        /// 작은 리본 자리: 배너 띠(BannerArea) 위 끝에서 RibbonDrop(293)px 아래가 가운데 (영역 가운데 기준 높이/2 − 293).
        /// HUD 아래 빈 바닥 위 — 띠 위 끝은 MAIN · 시트 모두 StageArea 위 끝이라 기준 화면 372~474에 머문다 (UX-SPEC-IA §8.3)
        /// </summary>
        float RibbonHeight()
        {
            var area = _banner.parent as RectTransform;
            return RibbonCenterY(area != null ? area.rect.height : 0f);
        }

        /// <summary>
        /// 작은 리본 부제(보상 한 줄)를 칸 폭(780)에 맞춘다: 37에서 잰 한 줄 폭이 칸보다 넓으면 비율대로 줄인다 (RibbonSubtitleMin 아래로는 안 줄인다).
        /// TMP 자동 크기를 쓰지 않는 까닭: 부제 칸 높이 44가 37 글자의 줄 높이(≈ 54)보다 낮아 짧은 줄도 늘 ≈ 30으로 줄여 버린다. 리본마다 한 번 잰다
        /// </summary>
        void FitRibbonSubtitle()
        {
            if (_subtitle == null || !_subtitle.gameObject.activeInHierarchy) return;
            float width = _subtitle.rectTransform.rect.width;
            if (width <= 0f) return;
            float preferred = _subtitle.GetPreferredValues(_subtitle.text).x;
            _subtitle.fontSize = FitFontSize(preferred, width, RibbonSubtitleSize, RibbonSubtitleMin);
        }

        /// <summary>
        /// 부제 받침을 부제에 맞춘다: 부제가 비면 끄고, 폭 = 글 폭 + SubtitlePlatePad (SubtitlePlateWidth), 높이 = 가운데 배너 44 · 작은 리본 54.
        /// 부제 글 칸도 같은 높이로 — 둘 다 위 끝(SubtitlePlateTop)에 매달려 있어 가운데가 맞는다. 배너마다 한 번 (FitRibbonSubtitle 다음 — 줄인 크기로 잰다)
        /// </summary>
        void FitSubtitlePlate(bool ribbon)
        {
            if (_subtitlePlate == null) return;
            bool show = _subtitle != null && _subtitle.gameObject.activeSelf;
            if (_subtitlePlate.gameObject.activeSelf != show) _subtitlePlate.gameObject.SetActive(show);
            if (!show) return;
            float height = ribbon ? RibbonSubtitlePlateHeight : SubtitlePlateHeight;
            float width = SubtitlePlateWidth(_subtitle.GetPreferredValues(_subtitle.text).x);
            _subtitlePlate.rectTransform.sizeDelta = new Vector2(width, height);
            if (_subtitlePlate.type == Image.Type.Sliced) _subtitlePlate.pixelsPerUnitMultiplier = PlateArtHeight / height;
            var label = _subtitle.rectTransform;
            label.sizeDelta = new Vector2(label.sizeDelta.x, height);
        }

        /// <summary>
        /// 가로로 늘어나는(앵커 x 0 ~ 1) 제목 칸의 좌우 여백을 inset 이상으로 넓힌다 (세로 · 이미 넓은 여백은 그대로). 장면을 다시 굽기 전 씬
        /// (좌우 30)에서도 제목이 리본의 평평한 띠 안에 들게 깨어날 때 한 번. 바꿨으면 true
        /// </summary>
        internal static bool ApplyTitleInset(RectTransform title, float inset)
        {
            if (title == null || Mathf.Abs(title.anchorMin.x) > 0.001f || Mathf.Abs(title.anchorMax.x - 1f) > 0.001f) return false;
            var min = title.offsetMin;
            var max = title.offsetMax;
            if (min.x >= inset - 0.01f && -max.x >= inset - 0.01f) return false;
            title.offsetMin = new Vector2(Mathf.Max(min.x, inset), min.y);
            title.offsetMax = new Vector2(Mathf.Min(max.x, -inset), max.y);
            return true;
        }

        /// <summary>부제 받침 폭 = 글 폭 + SubtitlePlatePad (SubtitlePlateMin ~ SubtitlePlateMax). 순수 계산</summary>
        internal static float SubtitlePlateWidth(float textWidth) =>
            Mathf.Clamp(Mathf.Max(0f, textWidth) + SubtitlePlatePad, SubtitlePlateMin, SubtitlePlateMax);

        /// <summary>최대 크기에서 잰 한 줄 폭(preferredAtMax)이 width 안에 들도록 줄인 크기 (0.1 단위 내림, min ~ max). 순수 계산</summary>
        internal static float FitFontSize(float preferredAtMax, float width, float max, float min)
        {
            if (preferredAtMax <= width || preferredAtMax <= 0f || width <= 0f) return max;
            float size = Mathf.Floor(max * width / preferredAtMax * 10f) / 10f;
            return Mathf.Clamp(size, min, max);
        }

        /// <summary>배너 띠 자리를 지금 배너(+ 부제 받침 + 보상 줄)의 월드 사각형으로 (할당 없음)</summary>
        void UpdateKeepOut(int lines)
        {
            var rect = WorldRect(_banner);
            if (_subtitlePlate != null && _subtitlePlate.gameObject.activeInHierarchy) rect = Union(rect, WorldRect(_subtitlePlate.rectTransform));
            for (int i = 0; i < lines && i < _lines.Length; i++)
            {
                if (!(LineRoot(i) is RectTransform line)) continue;
                rect = Union(rect, WorldRect(line));
            }
            s_keepOutWorld = rect;
            s_keepOutActive = true;
        }

        static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        /// <summary>부제를 누를 수 있게 하거나(tap) 끈다: 배너 묶음이 누름을 받고 부제 글자만 누르는 자리가 된다 (그림 · 제목 · 보상 줄은 누름을 받지 않는다)</summary>
        void SetTappable(Action tap)
        {
            _tap = tap;
            bool on = tap != null && _subtitleButton != null;
            if (_group != null)
            {
                _group.blocksRaycasts = on;
                _group.interactable = on;
            }
            if (_subtitle != null) _subtitle.raycastTarget = on;
        }

        void OnSubtitleTap()
        {
            var tap = _tap;
            SetTappable(null);
            tap?.Invoke();
        }

        Transform LineRoot(int index) =>
            _lineGroups != null && index < _lineGroups.Length && _lineGroups[index] != null ? _lineGroups[index].transform : _lines[index].transform;

        void SetLine(int index, float alpha, float scale)
        {
            if (_lineGroups != null && index < _lineGroups.Length && _lineGroups[index] != null) _lineGroups[index].alpha = alpha;
            else _lines[index].alpha = alpha;
            LineRoot(index).localScale = new Vector3(scale, scale, 1f);
        }

        void ApplyAlert(Style style, float strength)
        {
            if (style != Style.Alert) return;
            float shake = UIMotion.Reduced ? 0f : Mathf.Sin(Time.unscaledTime * 60f) * 10f * strength;
            _banner.anchoredPosition = new Vector2(shake, 0f);
        }
    }
}
