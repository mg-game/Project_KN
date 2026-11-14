using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace IdleGame.UI
{
    /// <summary>
    /// 보상 날아가기 (FX-04 · M20 · M21 · M78, UX-SPEC §3.12 · §2.2). 보상 출처에서 아이콘 최대 10개가 곡선을 그리며 상단 재화 칩으로
    /// 0.55~0.8초 동안 시차를 두고 날아가고, 도착하면 칩이 1.15배로 튀며 별빛이 터지고, 칩 아래 받침 위에 "+1,234"가 카운트업된다.
    ///   · 도착지: 단추 → GoldChip, 별조각 → GemChip, 별빛 사탕 → 전투 화면 빠른 아이콘 이벤트(시트가 열려 흐려지면 상점 탭), 재료 → ≡ 메뉴 버튼,
    ///     빠른/두 배 수선권 → 수선 바구니 위젯(없으면 가방 자리), 친구·의상·선택 기억편 → 친구들 탭.
    ///     실타래 (T10, 결정 2 안 D): 날리기 시작할 때 한 번 정한다. 강화 탭이 열려 있고 잔액 칩이 보이면 그 칩, 아니면 강화 탭 꼬리표가 보이면 꼬리표,
    ///     둘 다 안 보이면 날리지 않고 출발 자리에 "+N" 받침만 (떠 있는 양 0). 보임 = 켜져 있고 가리지 않고 열린 팝업이 없음. 단추 칩으로 대신 가지 않는다.
    ///     날릴 때마다 정한다: 원래 도착지가 꺼져 있거나 가려졌으면(UIFrame.IsFeedbackHidden — 시트 밑, SheetFade로 흐려짐 ·
    ///     시트가 열리는 중이면 다 열렸을 때 사라지는 HUD도, CR-03)
    ///     그때만 대신 도착지(별빛 사탕은 상점 탭, 그다음 GoldChip · 친구들은 가방 자리)로 — 다시 보이면 다음 날림부터 원래 자리로.
    ///   · 호출은 각 UI가 한다: RewardFlyer.Fly(받은 목록, 누른 버튼). 스테이지 클리어·바구니·미션·출석·우편·보상 팝업 [확인]·상점 구매에 공통.
    ///     MetaRewardService.Grant가 돌려준 목록(풀린 값)을 그대로 넘기면 된다. GoldMinutes가 섞여 있으면 지금 초당 단추로 푼다.
    ///   · 떠 있는 양 (M21): 보상은 날기 전에 이미 들어와 있다. 재화(단추 · 별조각 · 별빛 사탕)는 날리는 순간 PendingOf에 더하고
    ///     아이콘이 닿을 때마다 뺀다 (그 재화가 날고 있는 아이콘이 없으면 0). 상단 바(TopBarView)는 보유량 − PendingOf를 보여 줘서
    ///     숫자가 아이콘과 함께 오른다. 바뀔 때마다(더함 · 닿음 · 비움) PendingChanged를 알린다.
    ///     안전 배수: 떠 있는 양이 남았는데 1.2초 동안 아무것도 닿지 않으면 0 — 날린 직후에는 그 재화 첫 아이콘이 닿을 때(시차 + 튀어나옴 + 비행)부터 잰다.
    ///     층이 꺼지거나 없어질 때(장면 바뀜)도 0.
    ///     한 번에 묶는 양이 많아 묶음 칸이 차면 같은 도착지의 마지막 묶음에 더한다 (버리지 않는다).
    ///   · "+N" 받침 (§2.2): 칩 아래 끝에 가운데를 둔 알약 (높이 40, 좌우 14, 최소 폭 72, 어두운 금실 알약 아트 α0.92),
    ///     글자 28 굵게 재화 색, x = 칩 가운데 + 칩 폭의 1/4. 화면 아래쪽 도착지(친구들 탭)는 위쪽 끝 위에.
    ///     빠른 아이콘 칸(QuickRail_L/R 바로 아래 자식 — 이벤트 등)은 칸 아래 끝이 이름 알약('이벤트')이라 가리므로, 메달 판 옆 화면 가운데 쪽에
    ///     판 가운데 높이로 둔다 (판 끝에서 10px 띄우고 글자가 길어지면 바깥으로 자란다 — Wave 2 r2 '이벤…+3').
    ///     수선 바구니 위젯(수선권)도 같은 규칙으로 바구니 그림 옆에 둔다 (아래 끝 '가득!' · 시간 알약을 덮지 않게).
    ///     상단 바 우편 · ≡ 메뉴(재료)는 옆 · 아래가 다른 버튼이라 버튼 자기 이름표 자리에 가운데로 두고 알약 α1로 이름을 다 덮는다
    ///     (Wave 3 r1 '메뉴' 위에 반쯤 얹힌 '+1' — 사라지면 이름이 돌아온다).
    ///     단추 칩 받침 아래의 '초당 +N'은 TopBarView가 단추가 날아오는 동안 흐려 둔다.
    ///     14px 아래에서 0.25초에 올라오고, 마지막 도착 뒤 0.75초 머물다 0.3초에 사라진다.
    ///   · 자리로 날아가기 FlyTo (M78): 새 기능 카드를 닫으면 그 그림이 기능 자리(탭 · 상단 버튼 등)로 0.55초 날아가 닿으면 자리를 1.15배로 튀긴다.
    ///   · 닿는 소리: 단추 CoinLand, 그 밖의 재화 · 아이템 GemLand (UISfx — 소리 표가 없으면 조용히 지나간다).
    ///   · 아이콘·별빛·글자는 미리 만든 풀을 돌려 쓰고, Update는 할당하지 않는다 (호출 한 번에 작은 정리만). 할 일이 없으면 Update를 끈다.
    ///   · 칩은 다른 화면(TopBarView)이 숫자를 고쳐 쓰므로 칩 글자는 건드리지 않고 크기만 튀긴다. 카운트업은 이 층의 글자로 보여 준다.
    ///   · 이 층은 팝업 위에 있다 (팝업 어둠 아래 칩이 가려져도 별빛과 "+N"은 보인다). 전투 보상은 팝업이 열려 있으면 날리지 않는다 (StageClearRewardFlyer).
    /// </summary>
    public class RewardFlyer : MonoBehaviour
    {
        /// <summary>한 번에 날리는 아이콘 최대 수</summary>
        public const int MaxIconsPerFly = 10;

        enum Dest
        {
            Gold = 0,
            Gem = 1,
            Event = 2,
            LineageXp = 3,
            Bag = 4,
            Ticket = 5,
            Friends = 6,
        }

        const int DestCount = 7;
        /// <summary>재화 도착지 수 (Gold · Gem · Event · LineageXp, CurrencyType과 같은 순서)</summary>
        const int CurrencyDests = 4;
        /// <summary>한 번에 모으는 (도착지, 그림) 묶음 최대 — 넘치면 같은 도착지 마지막 묶음에 더한다</summary>
        const int MaxRequests = 12;
        /// <summary>떠 있는 양이 남았는데 이만큼 아무것도 닿지 않으면 0으로 (초)</summary>
        const float PendingDrain = 1.2f;

        // ── "+N" 받침 (§2.2) ──
        const float LabelHeight = 40f;
        const float LabelPadX = 14f;
        const float LabelMinWidth = 72f;
        const float LabelFontSize = 28f;
        const float LabelAlpha = 0.92f;
        const float LabelRise = 14f;
        const float LabelRiseTime = 0.25f;
        const float LabelHoldTime = 0.75f;
        const float LabelFadeTime = 0.3f;
        /// <summary>화면 아래쪽 도착지에서 받침을 대상 위로 띄우는 틈</summary>
        const float LabelGapAbove = 6f;
        const float LabelEdgeMargin = 8f;
        /// <summary>판 옆 받침 (빠른 아이콘 칸 · 수선 바구니): 판 끝과 받침 사이 틈 (판 옆, 화면 가운데 쪽)</summary>
        const float RailLabelGap = 10f;
        /// <summary>빠른 아이콘 줄 이름 (CoachTargets — 칸은 줄의 바로 아래 자식, UX-SPEC-IA §5.8)</summary>
        const string RailLeftName = "QuickRail_L";
        const string RailRightName = "QuickRail_R";
        /// <summary>상단 바 아이콘 버튼 (FrameSceneBuilder — "TopBar" 바로 아래 "MailButton" · "MenuButton", 이름표 자식 "Label")</summary>
        const string TopBarName = "TopBar";
        const string MailButtonName = "MailButton";
        const string MenuButtonName = "MenuButton";
        const string TopIconLabelName = "Label";
        /// <summary>이름표를 못 찾았을 때 버튼 아래에서 잡는 이름표 줄 높이 (FrameSceneBuilder TopLabelHeight)</summary>
        const float TopIconLabelRow = 26f;
        /// <summary>수선 바구니 위젯 (CoachTargets.IdleChestWidget의 끝 이름, MetaSceneBuilder — 바구니 그림 "Visual/Basket")</summary>
        const string BasketWidgetName = "IdleChestWidget";
        const string BasketPlatePath = "Visual/Basket";

        // ── 자리로 날아가기 (M78) ──
        const float SlotIconSize = 96f;
        /// <summary>베지어 조절점: 출발 · 도착 가운데에서 위로</summary>
        const float SlotArcLift = 220f;
        /// <summary>마지막 이만큼(초) 동안 사라진다</summary>
        const float SlotFadeTail = 0.08f;
        const int MaxSlotPunches = 4;

        static RewardFlyer s_instance;
        /// <summary>재화별 떠 있는 양 (날리는 중이라 상단 바에 아직 안 더한 양)</summary>
        static readonly double[] Pending = new double[CurrencyDests];
        /// <summary>재화별 미리 잡아 둔 양 (곧 날릴 것: StageClearRewardFlyer가 1-1 개방 뒤로 미룬 실타래, Reserve)</summary>
        static readonly double[] Reserved = new double[CurrencyDests];

        [Header("층 (이 오브젝트의 RectTransform이 비행 좌표계)")]
        [SerializeField] RectTransform _canvasRoot;
        [SerializeField] Image _iconTemplate;
        [SerializeField] Image _sparkTemplate;
        [SerializeField] TMP_Text _countTemplate;
        [SerializeField] int _iconPool = 30;
        [SerializeField] int _sparkPool = 36;
        [Tooltip("\"+N\" 받침 그림 (비우면 어두운 금실 알약 아트를 찾는다)")]
        [SerializeField] Sprite _labelSprite;

        [Header("도착지")]
        [SerializeField] RectTransform _goldTarget;
        [SerializeField] RectTransform _gemTarget;
        [SerializeField] RectTransform _eventTarget;
        [Tooltip("별빛 사탕 도착지(빠른 아이콘 이벤트)가 꺼졌거나 가려졌을 때 (시트가 열려 빠른 아이콘 줄이 흐려짐 — 상점 탭, CR-03). 이것도 안 되면 GoldChip")]
        [SerializeField] RectTransform _eventFallbackTarget;
        [SerializeField] RectTransform _bagTarget;
        [SerializeField] RectTransform _friendsTarget;
        [Tooltip("실타래 도착지 1순위: 강화 탭 친구 머리 줄 잔액 칩 (T10)")]
        [SerializeField] RectTransform _lineageXpChipTarget;
        [Tooltip("실타래 도착지 2순위: 강화 탭 버튼 꼬리표 (T10). 둘 다 안 보이면 받침만")]
        [SerializeField] RectTransform _lineageXpTagTarget;
        [Tooltip("수선권이 날아갈 곳 (Canvas 기준 경로, 메타 HUD가 만든다). 없거나 꺼져 있으면 가방")]
        [SerializeField] string _ticketTargetPath = CoachTargets.IdleChestWidget;

        [Header("아이콘")]
        [SerializeField] Sprite _goldIcon;
        [SerializeField] Sprite _gemIcon;
        [SerializeField] Sprite _eventIcon;
        [Tooltip("실타래 (임시 그림, 산호 틴트 UITheme.CurrencyIconTint)")]
        [SerializeField] Sprite _lineageXpIcon;
        [SerializeField] Sprite _quickTicketIcon;
        [SerializeField] Sprite _doubleTicketIcon;
        [SerializeField] Sprite _selectShardsIcon;
        [Tooltip("아이콘이 없을 때 (색으로 칠한 원)")]
        [SerializeField] Sprite _fallbackIcon;

        [Header("시간 (초)")]
        [SerializeField] float _popTime = 0.16f;
        [SerializeField] float _minFlight = 0.55f;
        [SerializeField] float _maxFlight = 0.8f;
        [SerializeField] float _stagger = 0.05f;
        [SerializeField] float _iconSize = 64f;

        struct Icon
        {
            public bool Active;
            public Image Image;
            public RectTransform Rect;
            public float Delay;
            public float Time;
            public float Flight;
            public Vector2 Start;
            public Vector2 Scatter;
            public Vector2 Control;
            public int Dest;
            public double Value;
            public float Spin;
            // 자리로 날아가기 (FlyTo)
            public bool Slot;
            public RectTransform Target;
            public Vector2 End;
            public Action OnArrive;
        }

        struct Spark
        {
            public bool Active;
            public Image Image;
            public RectTransform Rect;
            public float Time;
            public float Duration;
            public Vector2 From;
            public Vector2 To;
            public float Size;
            public float Spin;
        }

        struct SlotPunch
        {
            public RectTransform Target;
            public Vector3 BaseScale;
            public float Time;
        }

        sealed class DestState
        {
            public RectTransform Target;
            public Vector3 BaseScale = Vector3.one;
            public bool HasBaseScale;
            public float Punch = -1f;
            public int InFlight;
            public double Arrived;
            public double Shown;
            public bool LabelActive;
            public float LabelHold;
            public float LabelAge;
            public RectTransform LabelRoot;
            /// <summary>받침 알약 그림 (이름표를 덮는 자리면 α1, 그 밖은 LabelAlpha)</summary>
            public Image LabelPlate;
            public TMP_Text Label;
            public CanvasGroup LabelGroup;
            public Vector2 LabelAnchor;
            /// <summary>받침이 자라는 쪽: 0 = 가운데 기준, +1 = LabelAnchor.x가 왼쪽 끝(오른쪽으로 자람), −1 = 오른쪽 끝 (빠른 아이콘 옆)</summary>
            public float LabelSide;
            public float LabelWidth;
            public readonly char[] Buffer = new char[FanfareFormat.BufferSize];
            public long LastWritten = -1;
            public Color Tint = Color.white;
            /// <summary>마지막으로 묶음을 받았거나 아이콘이 닿은 시각 (떠 있는 양 안전 배수)</summary>
            public float LastActivity;
            /// <summary>이번 Fly에서 떠 있는 양이 바뀜 (Launch 뒤 한 번 알린다)</summary>
            public bool PendingDirty;
        }

        struct Request
        {
            public int Dest;
            public Sprite Sprite;
            public Color Tint;
            public double Amount;
        }

        static readonly Vector3[] Corners = new Vector3[4];

        RectTransform _layer;
        Icon[] _icons;
        Spark[] _sparks;
        readonly DestState[] _dests = new DestState[DestCount];
        /// <summary>도착지마다 원래 정해진 칩 (빌더 연결 — 대신 도착지로 바꿔도 여기는 그대로 둔다. 수선권은 경로로 찾는다)</summary>
        readonly RectTransform[] _configured = new RectTransform[DestCount];
        /// <summary>MaxRequests + 도착지마다 한 칸 (칸이 차도 처음 오는 도착지는 늘 들어간다)</summary>
        readonly Request[] _requests = new Request[MaxRequests + DestCount];
        int _requestCount;
        readonly int[] _counts = new int[MaxRequests + DestCount];
        readonly SlotPunch[] _slotPunches = new SlotPunch[MaxSlotPunches];
        RectTransform _ticketTarget;
        /// <summary>할 일이 없어 스스로 끄는 중 (OnDisable에서 진짜 꺼짐과 구별)</summary>
        bool _sleeping;

        /// <summary>장면에 날아가기 층이 있는지 (할 일이 없으면 Update만 쉬고, 층이 켜져 있으면 언제든 쓸 수 있다)</summary>
        public static bool IsAvailable => s_instance != null && s_instance.gameObject.activeInHierarchy;

        /// <summary>떠 있는 양이 바뀌었다 (그 재화) — 날림 시작 · 아이콘 도착 · 안전 배수 · 층 꺼짐. 상단 바가 숫자를 다시 맞춘다</summary>
        public static event Action<CurrencyType> PendingChanged;

        // ───────────────────────────── 공개 API ─────────────────────────────

        /// <summary>
        /// 그 재화의 떠 있는 양 (이미 보유량에 들어왔지만 아이콘이 아직 닿지 않은 양, 0 이상). 상단 바는 보유량 − 이 값을 보여 준다.
        /// </summary>
        public static double PendingOf(CurrencyType currency)
        {
            int index = CurrencyIndex(currency);
            double pending = Pending[index] + Reserved[index];
            return pending > 0 ? pending : 0;
        }

        /// <summary>
        /// 곧 날릴 양을 떠 있는 양에 미리 잡는다 (이미 보유량에 들어왔지만 날리기를 미룬 양. 그동안 잔액 표시가 오르내리지 않게, T10).
        /// 날릴 때 ReleaseReserved로 풀고 같은 프레임에 Fly한다. 층이 꺼지면 함께 비운다
        /// </summary>
        public static void Reserve(CurrencyType currency, double amount)
        {
            if (!(amount > 0)) return;
            Reserved[CurrencyIndex(currency)] += amount;
            PendingChanged?.Invoke(currency);
        }

        /// <summary>미리 잡은 양을 푼다 (0 아래로 내려가지 않는다)</summary>
        public static void ReleaseReserved(CurrencyType currency, double amount)
        {
            int index = CurrencyIndex(currency);
            if (!(amount > 0) || Reserved[index] <= 0) return;
            Reserved[index] = Math.Max(0, Reserved[index] - amount);
            PendingChanged?.Invoke(currency);
        }

        /// <summary>메타 보상 목록을 from 위치에서 날린다 (from이 null이면 화면 가운데). 수선권·친구 등도 받는 곳으로 날아간다</summary>
        public static void Fly(IReadOnlyList<MetaReward> rewards, RectTransform from)
        {
            if (!IsAvailable || rewards == null || rewards.Count == 0) return;
            s_instance.BeginRequests();
            for (int i = 0; i < rewards.Count; i++) s_instance.AddRequest(rewards[i]);
            s_instance.Launch(from);
        }

        /// <summary>재화/재료 보상 목록을 from 위치에서 날린다 (스테이지 클리어·우편 등)</summary>
        public static void Fly(IReadOnlyList<RewardData> rewards, RectTransform from)
        {
            if (!IsAvailable || rewards == null || rewards.Count == 0) return;
            s_instance.BeginRequests();
            for (int i = 0; i < rewards.Count; i++) s_instance.AddRequest(rewards[i]);
            s_instance.Launch(from);
        }

        /// <summary>보상 하나</summary>
        public static void Fly(MetaReward reward, RectTransform from)
        {
            if (!IsAvailable || reward == null) return;
            s_instance.BeginRequests();
            s_instance.AddRequest(reward);
            s_instance.Launch(from);
        }

        /// <summary>재화 하나 (예: 퀘스트 단추 보상)</summary>
        public static void FlyCurrency(CurrencyType currency, double amount, RectTransform from)
        {
            if (!IsAvailable || !(amount > 0)) return;
            s_instance.BeginRequests();
            s_instance.AddCurrency(currency, amount);
            s_instance.Launch(from);
        }

        /// <summary>
        /// 그림 하나를 from에서 to로 날린다 (M78: 새 기능 카드를 닫으면 그 기능 자리로). 96px, 0.55초 InOutSine 2차 베지어
        /// (출발 · 도착 가운데에서 220px 위 조절점, 움직이는 도착지를 따라간다), 크기 1 → 0.7, 마지막 0.08초에 사라진다.
        /// 닿으면 to를 1.15배로 0.2초 튀기고 별빛을 조금 뿌린 뒤 onArrive를 부른다 (부르는 쪽은 MetaSparkleBurst 같은 덧붙임만 한다).
        /// 층이 없거나 to가 안 보이거나 아이콘 풀이 다 찼으면 날리지 않고 onArrive를 바로 부르고 false.
        /// 층이 중간에 꺼지면 onArrive는 오지 않는다 (기다리는 쪽은 자기 시간 제한을 둔다 — §2.11 최대 0.8초).
        /// </summary>
        public static bool FlyTo(Sprite icon, RectTransform from, RectTransform to, Action onArrive)
        {
            if (!IsAvailable || to == null || !to.gameObject.activeInHierarchy || !s_instance.LaunchSlot(icon, from, to, onArrive))
            {
                onArrive?.Invoke();
                return false;
            }
            return true;
        }

        // ───────────────────────────── 수명 ─────────────────────────────

        void Awake()
        {
            s_instance = this;
            _layer = (RectTransform)transform;
            ResetPending(false);

            _icons = new Icon[Mathf.Max(MaxIconsPerFly, _iconPool)];
            for (int i = 0; i < _icons.Length; i++)
            {
                var image = Instantiate(_iconTemplate, _layer);
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
                _icons[i] = new Icon { Image = image, Rect = image.rectTransform };
            }
            _sparks = new Spark[Mathf.Max(8, _sparkPool)];
            for (int i = 0; i < _sparks.Length; i++)
            {
                var image = Instantiate(_sparkTemplate, _layer);
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
                _sparks[i] = new Spark { Image = image, Rect = image.rectTransform };
            }

            var targets = new[] { _goldTarget, _gemTarget, _eventTarget, _lineageXpChipTarget, _bagTarget, null, _friendsTarget };
            for (int i = 0; i < DestCount; i++) _configured[i] = targets[i];
            var tints = new[] { UITheme.Gold, UITheme.Gem, UITheme.EventToken, UITheme.LineageXp, UITheme.Lavender, UITheme.Lamp, UITheme.DustyPink };
            var labelSprite = LabelSprite();
            for (int i = 0; i < DestCount; i++)
            {
                var root = NewLabelPill(i, labelSprite, out var group);
                var label = Instantiate(_countTemplate, root);
                SetupLabel(label);
                root.gameObject.SetActive(false);
                _dests[i] = new DestState
                {
                    Target = targets[i], LabelRoot = root, LabelPlate = root.GetComponent<Image>(), Label = label, LabelGroup = group, Tint = tints[i],
                };
            }

            _iconTemplate.gameObject.SetActive(false);
            _sparkTemplate.gameObject.SetActive(false);
            _countTemplate.gameObject.SetActive(false);
            Sleep();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            // 장면이 내려가는 중: 알림 없이 비운다 (받는 화면도 함께 사라진다)
            ResetPending(false);
        }

        void OnDisable()
        {
            if (_sleeping) return;
            // 층이 꺼짐: 튄 칩이 커진 채로 남지 않게, 떠 있는 양은 비운다 (상단 바가 바로 보유량을 보여 준다)
            for (int i = 0; i < DestCount; i++)
            {
                var dest = _dests[i];
                if (dest == null) continue;
                if (dest.Target != null && dest.HasBaseScale && dest.Punch >= 0f) dest.Target.localScale = dest.BaseScale;
                dest.Punch = -1f;
            }
            for (int i = 0; i < _slotPunches.Length; i++)
            {
                ref var punch = ref _slotPunches[i];
                if (punch.Target != null) punch.Target.localScale = punch.BaseScale;
                punch.Target = null;
            }
            // 장면이 내려가는 중(다시 불러오기 · 끝내기)이면 알리지 않는다 — 받는 상단 바도, 게임(Game.Currency)도 함께 사라지는 중이다
            ResetPending(gameObject.scene.isLoaded);
        }

        /// <summary>할 일이 없으면 Update를 끈다 (아이콘 · 별빛 · 받침 · 튀기기 · 떠 있는 양이 모두 없을 때)</summary>
        void Sleep()
        {
            _sleeping = true;
            enabled = false;
            _sleeping = false;
        }

        void Wake()
        {
            if (!enabled) enabled = true;
        }

        static void ResetPending(bool notify)
        {
            for (int i = 0; i < CurrencyDests; i++)
            {
                if (Pending[i] == 0 && Reserved[i] == 0) continue;
                Pending[i] = 0;
                Reserved[i] = 0;
                if (notify) PendingChanged?.Invoke(CurrencyOf(i));
            }
        }

        static int CurrencyIndex(CurrencyType currency) => currency switch
        {
            CurrencyType.Gold => (int)Dest.Gold,
            CurrencyType.Gem => (int)Dest.Gem,
            CurrencyType.LineageXp => (int)Dest.LineageXp,
            _ => (int)Dest.Event,
        };

        static CurrencyType CurrencyOf(int dest) => dest switch
        {
            (int)Dest.Gold => CurrencyType.Gold,
            (int)Dest.Gem => CurrencyType.Gem,
            (int)Dest.LineageXp => CurrencyType.LineageXp,
            _ => CurrencyType.EventToken,
        };

        // ───────────────────────────── "+N" 받침 ─────────────────────────────

        Sprite LabelSprite()
        {
            if (_labelSprite != null) return _labelSprite;
            var sprite = UIFind.LoadedSprite(UIFind.ArtPillNight);
            if (sprite != null) return sprite;
            // 상단 재화 칩도 같은 어두운 알약이다
            var chip = _goldTarget != null ? _goldTarget.GetComponent<Image>() : null;
            return chip != null && chip.sprite != null && chip.sprite.border != Vector4.zero ? chip.sprite : null;
        }

        RectTransform NewLabelPill(int index, Sprite sprite, out CanvasGroup group)
        {
            var go = new GameObject("CountPill" + index, typeof(RectTransform), typeof(Image), typeof(CanvasGroup)) { layer = gameObject.layer };
            var root = (RectTransform)go.transform;
            root.SetParent(_layer, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(LabelMinWidth, LabelHeight);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            var color = sprite != null ? new Color(1f, 1f, 1f, LabelAlpha) : UITheme.WithAlpha(UITheme.Panel, LabelAlpha);
            UIFind.SkinCapsule(image, sprite, LabelHeight, color);
            group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return root;
        }

        static void SetupLabel(TMP_Text label)
        {
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(LabelPadX, 0f);
            rect.offsetMax = new Vector2(-LabelPadX, 0f);
            rect.localScale = Vector3.one;
            label.enableAutoSizing = false;
            label.fontSize = LabelFontSize;
            label.fontStyle |= FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            var group = label.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            label.gameObject.SetActive(true);
        }

        /// <summary>
        /// 받침 자리: 칩 아래 끝 · 칩 가운데 + 폭/4. 화면 아래쪽 대상(하단 탭)은 위쪽 끝 위.
        /// 빠른 아이콘 칸은 메달 판 옆 (화면 가운데 쪽, 판 가운데 높이), 수선 바구니 위젯도 바구니 그림 옆 (아래 끝 '가득!' · 시간 알약을 덮지 않게)
        /// — side에 자라는 쪽을 돌려준다 (그 밖은 0 = 가운데 기준).
        /// 상단 바 우편 · ≡ 메뉴는 버튼 자기 이름표('메뉴') 자리에 가운데로 두고 이름표를 다 덮는다 (cover = 알약 α1 — 반쯤 걸쳐 '메뉴' 위에 '+1'이 얹혀
        /// 보이지 않게, 사라지면 이름이 돌아온다). 옆은 이웃 버튼(우편 봉투 · 숫자 배지)이고 아래는 배속 · AUTO라 비어 있는 자리가 없다.
        /// 받침이 켜질 때 한 번만 부른다 (이름 비교 · Find는 매 프레임이 아니다)
        /// </summary>
        Vector2 LabelAnchorFor(int index, out float side, out bool cover)
        {
            side = 0f;
            cover = false;
            var target = _dests[index].Target;
            if (target == null || !target.gameObject.activeInHierarchy) return TopRightFallback() + new Vector2(0f, -58f);
            var box = UIFind.RectIn(target, _layer, Corners);
            if (IsRailCell(target)) return RailLabelAnchor(box, _layer.rect.center.x, out side);
            if (IsTopIconButton(target))
            {
                cover = true;
                var label = target.Find(TopIconLabelName) as RectTransform;
                bool hasLabel = label != null && label.gameObject.activeInHierarchy;
                return TopIconLabelAnchor(box, hasLabel ? UIFind.RectIn(label, _layer, Corners) : default, hasLabel);
            }
            if (target.name == BasketWidgetName)
            {
                var basket = target.Find(BasketPlatePath) as RectTransform;
                var plate = basket != null && basket.gameObject.activeInHierarchy ? UIFind.RectIn(basket, _layer, Corners) : box;
                return BesidePlateAnchor(plate, _layer.rect.center.x, out side);
            }
            if (box.center.y < _layer.rect.center.y) return new Vector2(box.center.x, box.yMax + LabelGapAbove + LabelHeight * 0.5f);
            return new Vector2(box.center.x + box.width * 0.25f, box.yMin);
        }

        /// <summary>상단 바 아이콘 버튼인지 (부모가 TopBar이고 이름이 MailButton · MenuButton — 재화 칩도 TopBar 아래라 이름까지 본다)</summary>
        static bool IsTopIconButton(RectTransform target)
        {
            var parent = target.parent;
            if (parent == null || parent.name != TopBarName) return false;
            string name = target.name;
            return name == MenuButtonName || name == MailButtonName;
        }

        /// <summary>
        /// 상단 바 아이콘 버튼 받침 자리 = 이름표 가운데 (이름표가 없으면 버튼 아래 끝 26px 줄 가운데). 받침(40 높이 · 최소 폭 72)이
        /// 이름표(26 높이, 두 글자)를 위아래 · 옆으로 다 덮는다
        /// </summary>
        internal static Vector2 TopIconLabelAnchor(Rect button, Rect label, bool hasLabel) =>
            hasLabel ? label.center : new Vector2(button.center.x, button.yMin + TopIconLabelRow * 0.5f);

        /// <summary>
        /// 판(그림) 옆 받침 자리: 판 가운데 높이, 판 끝에서 RailLabelGap 띄운 화면 가운데 쪽.
        /// 돌려주는 x는 받침의 가까운 끝 (side +1 = 왼쪽 끝 → 오른쪽으로 자람, −1 = 오른쪽 끝 → 왼쪽으로 자람)
        /// </summary>
        internal static Vector2 BesidePlateAnchor(Rect plate, float screenCenterX, out float side)
        {
            bool towardRight = plate.center.x <= screenCenterX;
            side = towardRight ? 1f : -1f;
            return new Vector2(towardRight ? plate.xMax + RailLabelGap : plate.xMin - RailLabelGap, plate.center.y);
        }

        /// <summary>빠른 아이콘 칸인지 (부모가 QuickRail_L · QuickRail_R)</summary>
        static bool IsRailCell(RectTransform target)
        {
            var parent = target.parent;
            if (parent == null) return false;
            string name = parent.name;
            return name == RailLeftName || name == RailRightName;
        }

        /// <summary>
        /// 빠른 아이콘 칸 받침 자리 (칸 = 위 가운데 메달 판 RailPlate + 아래 이름 알약): 판 가운데 높이, 판 끝에서 RailLabelGap 띄운 화면 가운데 쪽.
        /// 돌려주는 x는 받침의 가까운 끝 (side +1 = 왼쪽 끝 → 오른쪽으로 자람, −1 = 오른쪽 끝 → 왼쪽으로 자람)
        /// </summary>
        internal static Vector2 RailLabelAnchor(Rect cell, float screenCenterX, out float side)
        {
            bool towardRight = cell.center.x <= screenCenterX;
            side = towardRight ? 1f : -1f;
            float plateHalf = Mathf.Min(UISize.RailPlate, cell.width) * 0.5f;
            float plateCenterY = cell.yMax - Mathf.Min(UISize.RailPlate, cell.height) * 0.5f;
            return new Vector2(cell.center.x + side * (plateHalf + RailLabelGap), plateCenterY);
        }

        /// <summary>받침 알약 α (이름표를 덮는 자리는 1 — 밑의 이름이 비치지 않게). 같으면 쓰지 않는다</summary>
        static void SetPlateAlpha(Image plate, float alpha)
        {
            if (plate == null) return;
            var color = plate.color;
            if (Mathf.Approximately(color.a, alpha)) return;
            color.a = alpha;
            plate.color = color;
        }

        /// <summary>받침 가운데 x: 가운데 기준이면 그대로, 옆 자리면 가까운 끝 + 폭/2 (자라는 쪽으로)</summary>
        internal static float LabelCenterX(float anchorX, float side, float width) => anchorX + side * width * 0.5f;

        // ───────────────────────────── 요청 정리 ─────────────────────────────

        void BeginRequests() => _requestCount = 0;

        void AddRequest(MetaReward reward)
        {
            if (reward == null || !(reward.amount > 0)) return;
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    AddCurrency(reward.currency, reward.amount);
                    break;
                case MetaRewardType.Item:
                    AddItem(reward.itemId, reward.amount);
                    break;
                case MetaRewardType.GoldMinutes:
                    // 지급 결과에는 없지만 미리보기 목록이 넘어오면 지금 기준 초당 수입으로 풀어 단추 · 실타래로 나눈다 (안 D)
                    if (Game.IsReady && Game.Growth != null)
                    {
                        double income = Game.Growth.IncomePerSecond * 60.0 * reward.amount;
                        AddCurrency(CurrencyType.Gold, IncomeSplit.GoldWhole(income));
                        AddCurrency(CurrencyType.LineageXp, IncomeSplit.LineageXpWhole(income));
                    }
                    break;
                case MetaRewardType.QuickIdleTicket:
                    Add((int)Dest.Ticket, _quickTicketIcon, UITheme.Lamp, reward.amount);
                    break;
                case MetaRewardType.DoubleTicket:
                    Add((int)Dest.Ticket, _doubleTicketIcon != null ? _doubleTicketIcon : _quickTicketIcon, UITheme.Lavender, reward.amount);
                    break;
                case MetaRewardType.SelectShards:
                    Add((int)Dest.Friends, _selectShardsIcon, UITheme.Lavender, reward.amount);
                    break;
                case MetaRewardType.Lineage:
                    Add((int)Dest.Friends, LineagePortrait(reward.itemId), LineageColor(reward.itemId), 1);
                    break;
                case MetaRewardType.Costume:
                    Add((int)Dest.Friends, CostumePortrait(reward.itemId), UITheme.DustyPink, 1);
                    break;
            }
        }

        void AddRequest(RewardData reward)
        {
            if (reward == null || !(reward.amount > 0)) return;
            if (reward.type == RewardType.Currency) AddCurrency(reward.currency, reward.amount);
            else AddItem(reward.itemId, reward.amount);
        }

        void AddCurrency(CurrencyType currency, double amount)
        {
            switch (currency)
            {
                case CurrencyType.Gold:
                    Add((int)Dest.Gold, _goldIcon, UITheme.Gold, amount);
                    break;
                case CurrencyType.Gem:
                    Add((int)Dest.Gem, _gemIcon, UITheme.Gem, amount);
                    break;
                case CurrencyType.LineageXp:
                    Add((int)Dest.LineageXp, _lineageXpIcon, UITheme.LineageXp, amount);
                    break;
                default:
                    Add((int)Dest.Event, _eventIcon, UITheme.EventToken, amount);
                    break;
            }
        }

        void AddItem(string itemId, double amount)
        {
            var def = Game.IsReady && Game.Items != null ? Game.Items.Get(itemId) : null;
            var tint = def != null ? UITheme.GradeColor(def.grade) : UITheme.Lavender;
            Add((int)Dest.Bag, def != null ? def.icon : null, tint, amount);
        }

        void Add(int dest, Sprite sprite, Color tint, double amount)
        {
            if (!(amount > 0)) return;
            int sameDest = -1;
            for (int i = 0; i < _requestCount; i++)
            {
                if (_requests[i].Dest != dest) continue;
                sameDest = i;
                if (_requests[i].Sprite != sprite) continue;
                _requests[i].Amount += amount;
                Accept(dest, amount);
                return;
            }
            if (_requestCount >= MaxRequests && sameDest >= 0)
            {
                // 묶음 칸이 찼다: 같은 도착지 마지막 묶음에 더한다 (버리지 않는다)
                _requests[sameDest].Amount += amount;
                Accept(dest, amount);
                return;
            }
            // 칸이 차도 처음 오는 도착지는 남겨 둔 칸에 들어간다 (도착지마다 한 칸)
            _requests[_requestCount++] = new Request { Dest = dest, Sprite = sprite, Tint = tint, Amount = amount };
            Accept(dest, amount);
        }

        /// <summary>묶음을 받았다: 재화면 떠 있는 양에 더한다 (알림은 Launch 뒤 한 번)</summary>
        void Accept(int dest, double amount)
        {
            if (dest >= CurrencyDests) return;
            Pending[dest] += amount;
            var state = _dests[dest];
            state.PendingDirty = true;
            state.LastActivity = Time.unscaledTime;
        }

        /// <summary>Launch 뒤: 날고 있는 아이콘이 없는 재화는 0으로 (닿을 것이 없다), 바뀐 재화를 알린다</summary>
        void FlushPending()
        {
            for (int i = 0; i < CurrencyDests; i++)
            {
                var state = _dests[i];
                if (!state.PendingDirty) continue;
                state.PendingDirty = false;
                if (state.InFlight == 0) Pending[i] = 0;
                PendingChanged?.Invoke(CurrencyOf(i));
            }
        }

        static Sprite LineagePortrait(string lineageId)
        {
            var db = Game.IsReady ? Game.Content?.Characters : null;
            var lineage = db?.GetLineage(lineageId);
            return lineage != null ? db.GetForm(lineage.baseFormId)?.portrait : null;
        }

        static Color LineageColor(string lineageId)
        {
            var lineage = Game.IsReady ? Game.Content?.Characters?.GetLineage(lineageId) : null;
            return lineage != null ? lineage.mainColor : UITheme.DustyPink;
        }

        static Sprite CostumePortrait(string costumeId) =>
            Game.IsReady ? Game.Content?.Characters?.GetCostume(costumeId)?.portrait : null;

        // ───────────────────────────── 발사 ─────────────────────────────

        void Launch(RectTransform from)
        {
            if (_requestCount == 0) return;
            Wake();
            var start = from != null && from.gameObject.activeInHierarchy ? UIFind.CenterIn(from, _layer) : _layer.rect.center;

            // 아이콘 수: 보상마다 최소 1개, 많은 양일수록 더 (합계 최대 10)
            int total = 0;
            for (int i = 0; i < _requestCount; i++)
            {
                double amount = _requests[i].Amount;
                int count = Mathf.Clamp(1 + (int)(Math.Log10(amount + 1) * 1.6), 1, 6);
                _counts[i] = count;
                total += count;
            }
            while (total > MaxIconsPerFly)
            {
                int biggest = -1;
                for (int i = 0; i < _requestCount; i++)
                {
                    if (_counts[i] > 1 && (biggest < 0 || _counts[i] > _counts[biggest])) biggest = i;
                }
                if (biggest < 0) break;
                _counts[biggest]--;
                total--;
            }

            float now = Time.unscaledTime;
            int order = 0;
            for (int r = 0; r < _requestCount && order < MaxIconsPerFly; r++)
            {
                var request = _requests[r];
                var dest = _dests[request.Dest];
                var target = ResolveTarget(request.Dest);
                if (target == null && request.Dest == (int)Dest.LineageXp)
                {
                    // 실타래 도착지가 하나도 안 보임 (T10): 날리지 않고 출발 자리에 "+N" 받침만, 떠 있는 양은 0
                    LabelOnly(request.Dest, request.Amount, start);
                    continue;
                }
                var end = target != null ? UIFind.CenterIn(target, _layer) : TopRightFallback();
                int count = _counts[r];
                double each = request.Amount / count;
                float firstArrival = float.MaxValue;
                for (int k = 0; k < count && order < MaxIconsPerFly; k++, order++)
                {
                    int slot = FreeIcon();
                    if (slot < 0) break;
                    SpawnIcon(slot, request, start, end, each, order);
                    dest.InFlight++;
                    ref var icon = ref _icons[slot];
                    firstArrival = Mathf.Min(firstArrival, icon.Delay + _popTime + icon.Flight);
                }
                // 안전 배수는 이 재화의 첫 아이콘이 닿을 즈음부터 잰다 (시차 0.05 × 순서 + 튀어나옴 + 비행이 1.2초를 넘을 수 있다)
                if (request.Dest < CurrencyDests && firstArrival < float.MaxValue)
                    dest.LastActivity = Mathf.Max(dest.LastActivity, now + firstArrival);
            }
            FlushPending();
        }

        /// <summary>
        /// 이번 날림의 도착지: 원래 칩(_configured, 수선권은 바구니 위젯)에 닿을 수 있으면(CanLand) 그것, 아니면 대신 도착지
        /// (별빛 사탕 → 상점 탭 _eventFallbackTarget → GoldChip, 수선권 → 가방 자리 → GoldChip, 친구들 → 가방 자리, 그 밖 → GoldChip).
        /// 닿을 수 있다 = 켜져 있고 UIFrame.IsFeedbackHidden이 아니고, 시트가 열렸으면(목표 상태) 다 열렸을 때 사라지는 HUD 안도 아님 (CR-03):
        /// SheetFade는 오브젝트를 켜 둔 채 알파 0으로 흐리므로 켜짐만 보면 시트가 열린 동안 사라진 빠른 아이콘으로 날아가고,
        /// 지금 알파만 보면 열리는 중에 날린 아이콘이 닿을 즈음 사라진 아이콘에 닿는다 (CanLand). 날림(묶음)마다 한 번 묻는다 — 매 프레임 묻지 않는다.
        /// 원래 칩을 덮어쓰지 않으므로 한 번 대신 날아도 원래 칩이 다시 보이면 다음 날림부터 돌아온다.
        /// 도착지가 바뀔 때만 크기 기준을 다시 잡는다 (튀는 중이던 칩은 원래 크기로 되돌려 둔다).
        /// </summary>
        RectTransform ResolveTarget(int dest)
        {
            var state = _dests[dest];
            RectTransform target;
            if (dest == (int)Dest.Ticket)
            {
                if (_ticketTarget == null || !_ticketTarget.gameObject.activeInHierarchy)
                    _ticketTarget = UIFind.Path(_canvasRoot, _ticketTargetPath);
                target = UIFind.IsShown(_ticketTarget) && CanLand(_ticketTarget) ? _ticketTarget : _bagTarget;
            }
            else if (dest == (int)Dest.LineageXp)
            {
                // T10: 잔액 칩(강화 탭이 열려 있을 때만 켜져 있다) → 꼬리표 → 없음(받침만). 열린 팝업이 있으면 둘 다 안 보임. 단추 칩으로 대신 가지 않는다
                var ui = UIManager.Instance;
                bool popup = ui != null && ui.HasOpenPopup;
                target = popup ? null : CanLand(_configured[dest]) ? _configured[dest] : CanLand(_lineageXpTagTarget) ? _lineageXpTagTarget : null;
            }
            else
            {
                target = _configured[dest];
                if (dest == (int)Dest.Event && !CanLand(target)) target = _eventFallbackTarget;
            }
            if (dest != (int)Dest.LineageXp && !CanLand(target)) target = dest == (int)Dest.Friends ? _bagTarget : _goldTarget;
            if (target != state.Target)
            {
                if (state.Target != null && state.HasBaseScale && state.Punch >= 0f) state.Target.localScale = state.BaseScale;
                state.Punch = -1f;
                state.Target = target;
                state.HasBaseScale = false;
            }
            return target;
        }

        /// <summary>
        /// 날아가 닿을 수 있는 도착지: 켜져 있고, 지금 가리지 않고(UIFrame.IsFeedbackHidden — 열린 시트 밑 · SheetFade 지금 알파 &lt; 0.05),
        /// 시트 목표 상태로도 가리지 않는다 — 시트가 열려(UIFrame.SheetOpen) 가장 가까운 SheetFade의 다 열린 알파(SheetAlpha)가 0.05 아래면
        /// 아직 흐려지는 중이어도 가린 것으로 본다 (CoachTargetResolver.HiddenBySheet와 같은 규칙, CR-03).
        /// 아이콘은 튀어나옴 0.16 + 비행 0.55~0.8초 뒤에 닿는데 빠른 아이콘 줄은 열기 0.30초 만에 알파 0이 되므로,
        /// 지금 알파만 보면 열기를 누른 직후(또는 머리를 끄는 동안) 날린 별빛 사탕이 사라진 아이콘에 닿는다.
        /// 튜토리얼 동안 흐리지 않는 SheetFade(수선 바구니)는 다 열린 알파가 0.5라 이 규칙에 걸리지 않는다.
        /// </summary>
        static bool CanLand(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy || UIFrame.IsFeedbackHidden(target)) return false;
            if (!UIFrame.SheetOpen) return true;
            var fade = target.GetComponentInParent<SheetFade>(true);
            return fade == null || fade.SheetAlpha >= SheetFade.HiddenBelow;
        }

        /// <summary>그 칩의 원래 크기 — 다른 도착지가 같은 칩(대신 도착지)을 이미 기억하고 있으면 그 값 (튀는 중인 크기를 원래로 잡지 않게)</summary>
        Vector3 BaseScaleOf(DestState dest)
        {
            for (int i = 0; i < DestCount; i++)
            {
                var other = _dests[i];
                if (other != null && other != dest && other.Target == dest.Target && other.HasBaseScale) return other.BaseScale;
            }
            return dest.Target.localScale;
        }

        Vector2 TopRightFallback()
        {
            var rect = _layer.rect;
            return new Vector2(rect.xMax - 160f, rect.yMax - 80f);
        }

        int FreeIcon()
        {
            for (int i = 0; i < _icons.Length; i++)
            {
                if (!_icons[i].Active) return i;
            }
            return -1;
        }

        void SpawnIcon(int slot, Request request, Vector2 start, Vector2 end, double value, int order)
        {
            ref var icon = ref _icons[slot];
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(50f, 115f);
            var scatter = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            // 곡선: 중간에서 진행 방향 옆으로 휘고 살짝 위로 뜬다
            var mid = (scatter + end) * 0.5f;
            var dir = end - scatter;
            var side = new Vector2(-dir.y, dir.x).normalized * (Random.value < 0.5f ? -1f : 1f);
            var control = mid + side * Random.Range(90f, 210f) + Vector2.up * Random.Range(40f, 120f);

            icon.Active = true;
            icon.Slot = false;
            icon.Target = null;
            icon.OnArrive = null;
            icon.Delay = order * _stagger;
            icon.Time = 0f;
            icon.Flight = Random.Range(_minFlight, _maxFlight);
            icon.Start = start;
            icon.Scatter = scatter;
            icon.Control = control;
            icon.Dest = request.Dest;
            icon.Value = value;
            icon.Spin = Random.Range(-90f, 90f);

            bool hasSprite = request.Sprite != null;
            icon.Image.sprite = hasSprite ? request.Sprite : _fallbackIcon;
            // 대신 쓰는 아이콘(별 아트)은 색을 살짝만 입힌다 (아트를 진한 색으로 덮지 않게). 실타래 임시 그림은 산호를 곱한다 (T2)
            icon.Image.color = hasSprite
                ? request.Dest < CurrencyDests ? UITheme.CurrencyIconTint(CurrencyOf(request.Dest)) : Color.white
                : Color.Lerp(Color.white, request.Tint, 0.35f);
            icon.Image.preserveAspect = true;
            icon.Rect.sizeDelta = new Vector2(_iconSize, _iconSize);
            icon.Rect.anchoredPosition = start;
            icon.Rect.localScale = Vector3.zero;
            icon.Rect.localRotation = Quaternion.identity;
            icon.Rect.SetAsLastSibling();
            icon.Image.gameObject.SetActive(true);
        }

        bool LaunchSlot(Sprite sprite, RectTransform from, RectTransform to, Action onArrive)
        {
            int slot = FreeIcon();
            if (slot < 0) return false;
            Wake();
            ref var icon = ref _icons[slot];
            icon.Active = true;
            icon.Slot = true;
            icon.Target = to;
            icon.OnArrive = onArrive;
            icon.Delay = 0f;
            icon.Time = 0f;
            icon.Flight = UIMotion.FlyToSlot;
            icon.Start = from != null && from.gameObject.activeInHierarchy ? UIFind.CenterIn(from, _layer) : _layer.rect.center;
            icon.End = UIFind.CenterIn(to, _layer);
            icon.Dest = -1;
            icon.Value = 0;

            icon.Image.sprite = sprite != null ? sprite : _fallbackIcon;
            icon.Image.color = Color.white;
            icon.Image.preserveAspect = true;
            icon.Rect.sizeDelta = new Vector2(SlotIconSize, SlotIconSize);
            icon.Rect.anchoredPosition = icon.Start;
            icon.Rect.localScale = Vector3.one;
            icon.Rect.localRotation = Quaternion.identity;
            icon.Rect.SetAsLastSibling();
            icon.Image.gameObject.SetActive(true);
            return true;
        }

        // ───────────────────────────── 프레임 ─────────────────────────────

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool busy = false;
            for (int i = 0; i < _icons.Length; i++)
            {
                if (!_icons[i].Active) continue;
                StepIcon(ref _icons[i], dt);
                busy = true;
            }
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (!_sparks[i].Active) continue;
                StepSpark(ref _sparks[i], dt);
                busy = true;
            }
            for (int i = 0; i < DestCount; i++) busy |= StepDest(i, dt);
            busy |= StepSlotPunches(dt);
            busy |= DrainPending();
            if (!busy) Sleep();
        }

        void StepIcon(ref Icon icon, float dt)
        {
            if (icon.Slot)
            {
                StepSlot(ref icon, dt);
                return;
            }
            if (icon.Delay > 0f)
            {
                icon.Delay -= dt;
                return;
            }
            icon.Time += dt;
            var rt = icon.Rect;

            if (icon.Time < _popTime)
            {
                // 출처에서 톡 튀어나와 흩어진다
                float k = icon.Time / _popTime;
                rt.anchoredPosition = Vector2.LerpUnclamped(icon.Start, icon.Scatter, UIMotion.OutCubic(k));
                rt.localScale = Vector3.one * (1.15f * UIMotion.OutBack(k));
                return;
            }

            float t = Mathf.Clamp01((icon.Time - _popTime) / icon.Flight);
            // 처음엔 느리게 떠올랐다가 칩으로 빨려 들어간다
            float e = t * t * (3f - 2f * t);
            e = Mathf.Lerp(e, t * t, 0.45f);
            var end = DestPosition(icon.Dest);
            float u = 1f - e;
            rt.anchoredPosition = u * u * icon.Scatter + 2f * u * e * icon.Control + e * e * end;
            rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.55f, e * e);
            rt.localRotation = Quaternion.Euler(0f, 0f, icon.Spin * Mathf.Sin(t * Mathf.PI));

            if (t < 1f) return;
            icon.Active = false;
            icon.Image.gameObject.SetActive(false);
            Arrive(icon.Dest, icon.Value, end);
        }

        void StepSlot(ref Icon icon, float dt)
        {
            icon.Time += dt;
            float k = icon.Flight > 0f ? Mathf.Clamp01(icon.Time / icon.Flight) : 1f;
            float e = UIMotion.InOutSine(k);
            if (icon.Target != null && icon.Target.gameObject.activeInHierarchy) icon.End = UIFind.CenterIn(icon.Target, _layer);
            var control = (icon.Start + icon.End) * 0.5f + new Vector2(0f, SlotArcLift);
            float u = 1f - e;
            icon.Rect.anchoredPosition = u * u * icon.Start + 2f * u * e * control + e * e * icon.End;
            icon.Rect.localScale = Vector3.one * Mathf.Lerp(1f, UIMotion.FlyToSlotEndScale, e);
            float tail = icon.Flight - icon.Time;
            var color = icon.Image.color;
            color.a = tail < SlotFadeTail ? Mathf.Clamp01(tail / SlotFadeTail) : 1f;
            icon.Image.color = color;
            if (k < 1f) return;

            icon.Active = false;
            icon.Image.gameObject.SetActive(false);
            var target = icon.Target;
            var callback = icon.OnArrive;
            icon.Target = null;
            icon.OnArrive = null;
            if (target != null && target.gameObject.activeInHierarchy) PunchSlot(target);
            for (int i = 0; i < 4; i++) SpawnSpark(icon.End, UITheme.Lamp);
            if (callback == null) return;
            try
            {
                callback();
            }
            catch (Exception e2)
            {
                Debug.LogException(e2);
            }
        }

        Vector2 DestPosition(int dest)
        {
            var target = _dests[dest].Target;
            return target != null && target.gameObject.activeInHierarchy ? UIFind.CenterIn(target, _layer) : TopRightFallback();
        }

        /// <summary>
        /// 받침만 (T10 실타래 도착지가 안 보임): 아이콘을 날리지 않고 at 자리 위에 "+N" 받침을 띄운다. 떠 있는 양에 더했던 몫은 바로 뺀다
        /// </summary>
        void LabelOnly(int destIndex, double value, Vector2 at)
        {
            var dest = _dests[destIndex];
            if (destIndex < CurrencyDests)
            {
                Pending[destIndex] = dest.InFlight == 0 ? 0 : Math.Max(0, Pending[destIndex] - value);
                dest.PendingDirty = true;
            }
            dest.Arrived += value;
            if (!dest.LabelActive)
            {
                dest.LabelActive = true;
                dest.Shown = 0;
                dest.LastWritten = -1;
                dest.LabelAge = 0f;
                dest.LabelWidth = 0f;
                dest.LabelSide = 0f;
                dest.LabelAnchor = at + new Vector2(0f, LabelGapAbove + LabelHeight);
                SetPlateAlpha(dest.LabelPlate, LabelAlpha);
                dest.Label.color = dest.Tint;
                dest.LabelGroup.alpha = 0f;
                dest.LabelRoot.gameObject.SetActive(true);
                dest.LabelRoot.SetAsLastSibling();
            }
            dest.LabelHold = LabelHoldTime;
            for (int i = 0; i < 3; i++) SpawnSpark(at, dest.Tint);
        }

        void Arrive(int destIndex, double value, Vector2 at)
        {
            var dest = _dests[destIndex];
            dest.InFlight = Mathf.Max(0, dest.InFlight - 1);
            dest.Arrived += value;
            dest.Punch = 0f;
            if (dest.Target != null && !dest.HasBaseScale)
            {
                dest.BaseScale = BaseScaleOf(dest);
                dest.HasBaseScale = true;
            }

            if (destIndex < CurrencyDests)
            {
                // 떠 있는 양에서 뺀다. 그 재화가 날고 있는 아이콘이 없으면 정확히 0 (나눗셈 찌꺼기도 함께)
                Pending[destIndex] = dest.InFlight == 0 ? 0 : Math.Max(0, Pending[destIndex] - value);
                dest.LastActivity = Time.unscaledTime;
                PendingChanged?.Invoke(CurrencyOf(destIndex));
            }

            if (!dest.LabelActive)
            {
                dest.LabelActive = true;
                dest.Shown = 0;
                dest.LastWritten = -1;
                dest.LabelAge = 0f;
                dest.LabelWidth = 0f;
                dest.LabelAnchor = LabelAnchorFor(destIndex, out dest.LabelSide, out bool cover);
                SetPlateAlpha(dest.LabelPlate, cover ? 1f : LabelAlpha);
                dest.Label.color = dest.Tint;
                dest.LabelGroup.alpha = 0f;
                dest.LabelRoot.gameObject.SetActive(true);
                dest.LabelRoot.SetAsLastSibling();
            }
            dest.LabelHold = LabelHoldTime;

            for (int i = 0; i < 3; i++) SpawnSpark(at, dest.Tint);
            UISfx.Play(destIndex == (int)Dest.Gold ? UISfxKey.CoinLand : UISfxKey.GemLand);
        }

        void SpawnSpark(Vector2 at, Color tint)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (_sparks[i].Active) continue;
                ref var spark = ref _sparks[i];
                float angle = Random.Range(0f, Mathf.PI * 2f);
                spark.Active = true;
                spark.Time = 0f;
                spark.Duration = Random.Range(0.28f, 0.42f);
                spark.From = at;
                spark.To = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(26f, 58f);
                spark.Size = Random.Range(22f, 38f);
                spark.Spin = Random.Range(-220f, 220f);
                spark.Image.color = Color.Lerp(tint, Color.white, 0.45f);
                spark.Rect.sizeDelta = new Vector2(spark.Size, spark.Size);
                spark.Rect.SetAsLastSibling();
                spark.Image.gameObject.SetActive(true);
                return;
            }
        }

        static void StepSpark(ref Spark spark, float dt)
        {
            spark.Time += dt;
            float k = Mathf.Clamp01(spark.Time / spark.Duration);
            spark.Rect.anchoredPosition = Vector2.LerpUnclamped(spark.From, spark.To, UIMotion.OutCubic(k));
            spark.Rect.localScale = Vector3.one * (k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f);
            spark.Rect.localRotation = Quaternion.Euler(0f, 0f, spark.Spin * k);
            if (k < 1f) return;
            spark.Active = false;
            spark.Image.gameObject.SetActive(false);
        }

        /// <summary>칩 튀기기 + "+N" 받침 (할 일이 남았으면 true)</summary>
        bool StepDest(int index, float dt)
        {
            var dest = _dests[index];
            bool busy = false;
            // 칩 튀기기: 1 → 1.15 → 1 (0.22초)
            if (dest.Punch >= 0f && dest.Target != null)
            {
                dest.Punch += dt;
                const float punchTime = 0.22f;
                float k = Mathf.Clamp01(dest.Punch / punchTime);
                dest.Target.localScale = dest.BaseScale * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
                if (k >= 1f)
                {
                    dest.Target.localScale = dest.BaseScale;
                    dest.Punch = -1f;
                }
                else busy = true;
            }
            else if (dest.Punch >= 0f)
            {
                dest.Punch = -1f;
            }

            if (!dest.LabelActive) return busy;

            // "+N" 카운트업: 도착한 양을 부드럽게 따라간다 (글자는 정수가 바뀔 때만 쓰고, 그때 받침 폭을 맞춘다)
            dest.LabelAge += dt;
            double gap = dest.Arrived - dest.Shown;
            dest.Shown = gap <= 0.5 ? dest.Arrived : dest.Shown + gap * (1f - Mathf.Exp(-dt * 12f));
            long whole = (long)Math.Floor(dest.Shown);
            if (whole != dest.LastWritten)
            {
                dest.LastWritten = whole;
                FanfareFormat.Set(dest.Label, dest.Buffer, whole);
                float width = Mathf.Max(LabelMinWidth, dest.Label.GetPreferredValues().x + LabelPadX * 2f);
                if (!Mathf.Approximately(width, dest.LabelWidth))
                {
                    dest.LabelWidth = width;
                    dest.LabelRoot.sizeDelta = new Vector2(width, LabelHeight);
                }
            }

            var area = _layer.rect;
            float half = dest.LabelWidth * 0.5f + LabelEdgeMargin;
            var anchor = dest.LabelAnchor;
            anchor.x = LabelCenterX(anchor.x, dest.LabelSide, dest.LabelWidth);
            if (area.width > half * 2f) anchor.x = Mathf.Clamp(anchor.x, area.xMin + half, area.xMax - half);
            float rise = Mathf.Min(1f, dest.LabelAge / LabelRiseTime);
            dest.LabelRoot.anchoredPosition = anchor + new Vector2(0f, -LabelRise * (1f - UIMotion.OutCubic(rise)));
            float pop = dest.Punch >= 0f ? Mathf.Sin(Mathf.Clamp01(dest.Punch / 0.22f) * Mathf.PI) : 0f;
            dest.LabelRoot.localScale = Vector3.one * (1f + 0.12f * pop);

            if (dest.InFlight > 0) dest.LabelHold = LabelHoldTime;
            else dest.LabelHold -= dt;

            float fade = dest.LabelHold > 0f ? 1f : Mathf.Clamp01(1f + dest.LabelHold / LabelFadeTime);
            dest.LabelGroup.alpha = Mathf.Min(fade, Mathf.Clamp01(dest.LabelAge / 0.12f));
            if (fade > 0f) return true;

            dest.LabelActive = false;
            dest.Arrived = 0;
            dest.Shown = 0;
            dest.LabelRoot.gameObject.SetActive(false);
            return busy;
        }

        /// <summary>자리로 날아가기 도착: 대상을 1.15배로 0.2초 튀긴다 (같은 대상이 이미 튀는 중이면 처음부터)</summary>
        void PunchSlot(RectTransform target)
        {
            int free = -1;
            for (int i = 0; i < _slotPunches.Length; i++)
            {
                if (_slotPunches[i].Target == target)
                {
                    _slotPunches[i].Time = 0f;
                    return;
                }
                if (free < 0 && _slotPunches[i].Target == null) free = i;
            }
            if (free < 0) return;
            _slotPunches[free] = new SlotPunch { Target = target, BaseScale = target.localScale, Time = 0f };
        }

        bool StepSlotPunches(float dt)
        {
            bool busy = false;
            for (int i = 0; i < _slotPunches.Length; i++)
            {
                ref var punch = ref _slotPunches[i];
                if (punch.Target == null) continue;
                punch.Time += dt;
                float k = Mathf.Clamp01(punch.Time / UIMotion.FlyToSlotPunch);
                punch.Target.localScale = punch.BaseScale * (1f + (UIMotion.FlyToSlotPunchScale - 1f) * Mathf.Sin(k * Mathf.PI));
                if (k < 1f)
                {
                    busy = true;
                    continue;
                }
                punch.Target.localScale = punch.BaseScale;
                punch.Target = null;
            }
            return busy;
        }

        /// <summary>안전 배수: 떠 있는 양이 남았는데 한동안 아무것도 닿지 않았으면 0으로 (남아 있으면 true)</summary>
        bool DrainPending()
        {
            bool any = false;
            float now = Time.unscaledTime;
            for (int i = 0; i < CurrencyDests; i++)
            {
                if (!(Pending[i] > 0)) continue;
                if (now - _dests[i].LastActivity <= PendingDrain)
                {
                    any = true;
                    continue;
                }
                Pending[i] = 0;
                PendingChanged?.Invoke(CurrencyOf(i));
            }
            return any;
        }
    }
}
