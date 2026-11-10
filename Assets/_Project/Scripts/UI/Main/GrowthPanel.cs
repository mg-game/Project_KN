using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 강화 탭 (UX-SPEC §2.4 M33 · M34 · M35 · M19 · M07): 수선점 강화(단추로 x1 길게 누르기 / x10 / 최대 → 초당 단추 · 수선점 내구도)와
    /// 보유 친구들의 계보 공유 레벨업 목록.
    ///   · 비용 버튼 세 상태 (CostButtonView): 넉넉함 / 부족함(누를 수 있음 — 흔들기 + 단추 부족 토스트, 1.5초에 한 번) / 쓸 수 없음('최대 레벨').
    ///     단추는 매 프레임 늘어나므로 단추 변화에는 상태만 바꾸고(같으면 아무것도 안 함), 문구는 레벨 · 가방 · 스테이지가 바뀔 때만 다시 만든다.
    ///   · 최대: 올릴 수 있는 레벨이 2 이상이면 '최대 +N'(그 비용), 1 이하면 '최대'만 흐리게(금 바탕 그림만 α0.65 · 글자 그대로, 누를 수 없음 — x1이 같은 일을 한다).
    ///     최대 레벨이면 x1 · x10과 같은 '최대 레벨' (누를 수 없음 — 몇 번을 다시 그려도 켜지지 않는다).
    ///   · x1은 길게 누르면 연속 강화 (HoldRepeatButton — 반복은 토스트 없이, 막히면 토스트 한 번 + 흔들기). x10 · 최대는 누르기만.
    ///   · 살 수 있게 되면 그 버튼에 0.45초 반짝 + 펀치 (쓴 지 1.5초 안이면 쉼), x10은 살 수 있는 동안 4초마다 한 번 (그동안만 코루틴).
    ///     코치 손가락이 x1을 가리키는 동안 · 움직임 줄이기면 반짝이지 않는다.
    ///   · 친구 카드 정렬 (M34): 튜토리얼 · 가이드 퀘스트가 가리키는 계보 → 편성 칸 순서 → 편성 밖은 등급 높은 순 → ID.
    ///     켜질 때와 보유 수가 바뀔 때만 다시 정렬한다 (레벨업으로 자리가 바뀌지 않는다 — 길게 누르는 카드가 손가락 밑에 그대로).
    ///   · 강화 탭 빛 이음새 (M35d): Cost10을 MainTabController.GrowthCost10이 읽는다 (패널이 꺼져 있어도 맞게 Game.Growth에서 바로).
    /// </summary>
    public class GrowthPanel : MonoBehaviour
    {
        /// <summary>x1을 이만큼 안에 3번 누르면 길게 누르기 안내를 한 번 보여 준다 (초)</summary>
        const float HoldHintWindow = 8f;
        const int HoldHintTaps = 3;
        /// <summary>길게 누르기 안내를 이미 보여 줬는지 (튜토리얼 힌트 깃발 — 저장된다)</summary>
        internal const string HoldHintFlag = "hint.growth.hold";
        const string HoldHintText = "강화 x1을 길게 누르면 연속으로 강화돼요";
        /// <summary>찾아온 친구 카드를 금빛 테로 보여 주는 시간 (초)</summary>
        const float FocusSeconds = 1.5f;
        const float FocusScroll = 0.3f;
        /// <summary>
        /// 최대 버튼이 흐려질 때 금 바탕 그림의 알파 (올릴 수 있는 레벨이 1 이하). 바탕 그림만 흐리고 '최대' 글자는 α1 그대로 둔다 —
        /// 비활성 그림 + 묶음 α0.5는 누빔 바탕이 비쳐 빈 칸처럼 보이고 '최대'가 1.3:1이었고 (Wave 2 r2), 묶음 CanvasGroup α0.6도
        /// 흐린 금 위에 흐린 글자가 한 번 더 섞여 ≈2:1이었다 (Wave 3 리뷰). 바탕만 α0.65면 흐린 금 위 Ink '최대' ≈3.4:1
        /// (가장 어두운 Background 위 · 누빔 바탕 ≈3.6:1, ≥3:1), 흐린 금은 밝은 금과 ≈2:1로 흐려진 것이 보인다
        /// </summary>
        internal const float MaxDimAlpha = 0.65f;
        /// <summary>오른쪽 끝 흐림을 끄는 스크롤 위치 (끝에 거의 닿으면)</summary>
        const float FadeHideAt = 0.98f;

        [Header("수선점")]
        [SerializeField] TMP_Text _levelText;
        [SerializeField] TMP_Text _statText;
        [Tooltip("수선점 내구도 (전투 최대 체력)")]
        [SerializeField] TMP_Text _hpText;
        [SerializeField] Button _levelUp1Button;
        [SerializeField] Button _levelUp10Button;
        [SerializeField] Button _levelUpMaxButton;
        [SerializeField] TMP_Text _cost1Text;
        [SerializeField] TMP_Text _cost10Text;
        [SerializeField] TMP_Text _costMaxText;
        [SerializeField] TMP_Text _maxLabel;
        [Tooltip("레벨업하면 수선점 그림이 살짝 튄다")]
        [SerializeField] RectTransform _shopArt;

        [Header("비용 버튼 상태 (M07) · 반짝임 (M35)")]
        [SerializeField] CostButtonView _cost1View;
        [SerializeField] CostButtonView _cost10View;
        [SerializeField] CostButtonView _costMaxView;
        [Tooltip("x1 · x10 · 최대 버튼의 반짝임 (ShineMask/Shine, 꺼 둠)")]
        [SerializeField] Image[] _shines = Array.Empty<Image>();

        [Header("친구 레벨")]
        [SerializeField] TMP_Text _lineageHeader;
        [SerializeField] ScrollRect _lineageScroll;
        [SerializeField] RectTransform _lineageContent;
        [SerializeField] GrowthLineageCardView _lineagePrefab;
        [SerializeField] GameObject _lineageEmptyLabel;
        [Tooltip("목록이 넘칠 때 오른쪽 끝 흐림 (72px)")]
        [SerializeField] GameObject _lineageFade;
        [Tooltip("'모두 레벨업' (가장 낮은 친구부터 돌아가며 단추가 떨어질 때까지)")]
        [SerializeField] Button _levelUpAllButton;

        // ───────────── 강화 탭 빛 이음새 (M35d · Wave 2 C-01) ─────────────

        static int s_cost10Level = -1;
        static int s_cost10Remain = -1;
        static double s_cost10;

        /// <summary>
        /// 강화 x10 비용 (단추) — 강화 탭 빛(MainTabController.GrowthCost10, M35d)이 단추가 바뀔 때마다 읽는다. 0 이하 = 살 것이 없음 (최대 레벨 · 게임 준비 전).
        /// 패널이 꺼져 있어도(MAIN · 다른 시트) 맞아야 하므로 Game.Growth에서 바로 계산한다: GetLevelUpCost(Level, min(10, RemainLevels)).
        /// 레벨 · 남은 레벨이 바뀔 때만 다시 계산해 둔다 (단추가 바뀔 때마다 불리므로 할당 없이).
        /// </summary>
        public static double Cost10
        {
            get
            {
                if (!Game.IsReady || Game.Growth == null) return 0d;
                var growth = Game.Growth;
                int remain = growth.RemainLevels;
                if (remain <= 0) return 0d;
                int level = growth.Level;
                if (level != s_cost10Level || remain != s_cost10Remain)
                {
                    s_cost10Level = level;
                    s_cost10Remain = remain;
                    s_cost10 = growth.GetLevelUpCost(level, Math.Min(10, remain));
                }
                return s_cost10;
            }
        }

        /// <summary>
        /// 강화 탭 빛 이음새: 장면이 뜬 뒤 한 번 MainTabController.GrowthCost10 = () => Cost10을 넣고 계속 둔다
        /// (패널은 MAIN에서 꺼진 채 시작해 OnEnable로는 늦다). 지난 장면의 비용 기억도 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RegisterGrowthCost10()
        {
            s_cost10Level = -1;
            s_cost10Remain = -1;
            MainTabController.GrowthCost10 = () => Cost10;
        }

        // ───────────── 다른 화면에서 데려오기 (Wave 2 C-03) ─────────────

        static string s_focusLineage;
        static GrowthPanel s_active;

        /// <summary>
        /// 다른 화면에서 이 친구 카드로 데려온다 (강해지기 시트의 '가장 낮은 편성 친구' 줄 등 — 보통 UINav.GoToTab(MainTab.Growth) 바로 뒤에 부른다).
        /// 패널이 꺼져 있으면(MAIN · 다른 시트) 미뤄 두었다가 켜질 때 카드를 보이게 굴리고 금빛 테로 1.5초 펀치한다.
        /// 보유하지 않은 계보면 조용히 지나간다.
        /// </summary>
        public static void FocusLineage(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId)) return;
            s_focusLineage = lineageId;
            var panel = s_active;
            if (panel != null && panel._started && panel.isActiveAndEnabled) panel.StartFocus();
        }

        UIListPool<GrowthLineageCardView> _lineageCards;
        HoldRepeatButton _hold1;
        CoachMarkView _coach;
        bool _coachKnown;
        Coroutine _shopPunch;
        Coroutine _focusRoutine;
        Coroutine _ambient;
        readonly Coroutine[] _shineRoutines = new Coroutine[3];
        bool _started;
        /// <summary>기능 열림을 듣는 기능 시스템 (친구 레벨업 1-1 잠금이 풀리면 카드를 다시 그린다 — F-03). 메타가 꺼져 있으면 null</summary>
        FeatureGateSystem _features;
        /// <summary>
        /// 친구 목록을 다시 그려야 함. 스테이지 클리어 때 편성 계보마다 호감도가 따로 올라
        /// Characters.Changed가 한 프레임에 여러 번 오므로 모았다가 LateUpdate에서 한 번만 그린다.
        /// </summary>
        bool _lineagesDirty;

        // 지금 보여주는 비용 (단추가 바뀌면 이 값과 비교해 상태만 바꾼다)
        double _cost1;
        double _cost10;
        double _costMax;
        int _maxCount;
        /// <summary>단추가 이보다 적어지면 최대로 올릴 수 있는 레벨 수가 줄어든다</summary>
        double _maxShrinkBelow = double.NegativeInfinity;
        /// <summary>단추가 이만큼 모이면 최대로 올릴 수 있는 레벨 수가 늘어난다</summary>
        double _maxGrowAt = double.PositiveInfinity;

        /// <summary>버튼별 지난 '살 수 있음' (x1 · x10 · 최대). 켜진 뒤 첫 판정은 반짝이지 않는다</summary>
        readonly bool[] _affordable = new bool[3];
        bool _affordKnown;
        /// <summary>최대 버튼의 원래 전환 (그림 교체) — 흐린 동안만 None으로 두어 비활성 그림으로 바뀌지 않게 한다</summary>
        Selectable.Transition _maxTransition = Selectable.Transition.SpriteSwap;
        float _lastSpendTime = float.NegativeInfinity;
        /// <summary>x1 한 번 누름 시각 (길게 누르기 안내 — 가장 최근 3번)</summary>
        readonly float[] _tapTimes = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        int _tapIndex;

        // 친구 정렬 (켜질 때 · 보유 수가 바뀔 때만)
        readonly List<LineageDef> _order = new();
        readonly List<LineageSortKey> _sortKeys = new();
        readonly Dictionary<string, LineageDef> _lineageById = new();
        HorizontalLayoutGroup _lineageLayout;
        bool _fadeWide;

        static readonly Comparison<LineageSortKey> SortComparison = (a, b) => CompareLineages(in a, in b);

        void Start()
        {
            _levelUp1Button.onClick.AddListener(OnLevelUp1);
            _levelUp10Button.onClick.AddListener(OnLevelUp10);
            _levelUpMaxButton.onClick.AddListener(OnLevelUpMax);
            if (_levelUpMaxButton.transition != Selectable.Transition.None) _maxTransition = _levelUpMaxButton.transition;
            if (_levelUpAllButton != null) _levelUpAllButton.onClick.AddListener(LevelUpAll);
            _hold1 = _levelUp1Button.GetComponent<HoldRepeatButton>();
            if (_hold1 != null)
            {
                _hold1.Step = StepLevelUp1;
                _hold1.Denied += OnHold1Denied;
            }
            if (_lineageScroll != null) _lineageScroll.onValueChanged.AddListener(OnLineageScrolled);
            _lineageLayout = _lineageContent != null ? _lineageContent.GetComponent<HorizontalLayoutGroup>() : null;
            _lineageCards = new UIListPool<GrowthLineageCardView>(_lineagePrefab, _lineageContent);

            Game.Currency.Changed += OnCurrencyChanged;
            Game.Growth.LevelChanged += OnLevelChanged;
            Game.Inventory.Changed += RefreshShop;
            // 초당 단추는 도달한 최고 스테이지에도 비례한다
            Game.Battle.StageChanged += RefreshShop;
            Game.Characters.Changed += MarkLineagesDirty;
            if (Game.IsMetaActive && Game.Features != null)
            {
                _features = Game.Features;
                _features.Unlocked += OnFeatureUnlocked;
            }
            _started = true;
            RefreshShop();
            RefreshLineages(true);
            ScrollForCoach();
            if (s_focusLineage != null) StartFocus();
        }

        void OnEnable()
        {
            s_active = this;
            if (!_started) return;
            _affordKnown = false;
            RefreshShop();
            RefreshLineages(true);
            ScrollForCoach();
            if (s_focusLineage != null) StartFocus();
        }

        void OnDisable()
        {
            if (s_active == this) s_active = null;
            StopShines();
            _focusRoutine = null;
            _shopPunch = null;
            if (_shopArt != null) _shopArt.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            if (s_active == this) s_active = null;
            if (!_started || !Game.IsReady) return;
            Game.Currency.Changed -= OnCurrencyChanged;
            Game.Growth.LevelChanged -= OnLevelChanged;
            Game.Inventory.Changed -= RefreshShop;
            Game.Battle.StageChanged -= RefreshShop;
            Game.Characters.Changed -= MarkLineagesDirty;
            if (_features != null) _features.Unlocked -= OnFeatureUnlocked;
            _features = null;
        }

        /// <summary>친구 레벨업이 열리면(1-1) 카드의 '1-1' 알약을 걷는다 (다음 LateUpdate에 한 번)</summary>
        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.CharacterLevelUp) _lineagesDirty = true;
        }

        void LateUpdate()
        {
            if (_lineagesDirty) RefreshLineages(false);
        }

        void MarkLineagesDirty() => _lineagesDirty = true;

        /// <summary>단추는 매 프레임 바뀐다: 최대 레벨 수가 바뀌는 경계를 넘을 때만 최대 칸을 다시 쓰고, 나머지는 상태만 바꾼다.</summary>
        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (type != CurrencyType.Gold || !isActiveAndEnabled) return;
            if (value < _maxShrinkBelow || value >= _maxGrowAt) RefreshMax();
            RefreshAffordable(value);
            for (int i = 0; i < _lineageCards.Count; i++) _lineageCards[i].RefreshAffordable(value);
        }

        void OnLevelChanged(int oldLevel, int newLevel)
        {
            RefreshShop();
            if (_shopArt != null && isActiveAndEnabled) MenuFx.Restart(this, ref _shopPunch, MenuFx.Punch(_shopArt, UIMotion.PunchS, UIMotion.PunchDuration));
        }

        // ───────────── 강화 버튼 ─────────────

        void OnLevelUp1()
        {
            if (Game.Growth.IsMaxLevel)
            {
                UIManager.Instance.ShowToast("수선점이 최대 레벨이에요.");
                return;
            }
            if (!Game.Growth.TryLevelUp(1))
            {
                Deny(_cost1View, _cost1);
                return;
            }
            Spent(_cost1View);
            CountTapForHoldHint();
        }

        void OnLevelUp10()
        {
            if (Game.Growth.IsMaxLevel)
            {
                UIManager.Instance.ShowToast("수선점이 최대 레벨이에요.");
                return;
            }
            if (!Game.Growth.TryLevelUp(10))
            {
                Deny(_cost10View, _cost10);
                return;
            }
            Spent(_cost10View);
        }

        void OnLevelUpMax()
        {
            if (Game.Growth.IsMaxLevel)
            {
                UIManager.Instance.ShowToast("수선점이 최대 레벨이에요.");
                return;
            }
            if (Game.Growth.LevelUpMax() == 0)
            {
                Deny(_costMaxView, Game.Growth.GetLevelUpCost(Game.Growth.Level));
                return;
            }
            Spent(_costMaxView);
        }

        /// <summary>x1 길게 누르기 반복 한 번 — 토스트 없이 (막히면 false → 누르기 끝 + Denied 한 번)</summary>
        bool StepLevelUp1()
        {
            if (Game.Growth.IsMaxLevel || !Game.Growth.TryLevelUp(1)) return false;
            Spent(_cost1View);
            return true;
        }

        void OnHold1Denied()
        {
            if (Game.Growth.IsMaxLevel)
            {
                UIManager.Instance.ShowToast("수선점이 최대 레벨이에요.");
                return;
            }
            Deny(_cost1View, _cost1);
        }

        /// <summary>모자람: 이름 · 비용 흔들기 + 단추 부족 토스트 ('단추 N개가 더 필요해요 · 약 …', 1.5초에 한 번 — UIManager.ShowShortfall)</summary>
        static void Deny(CostButtonView view, double cost)
        {
            if (view != null) view.Deny();
            UIManager.Instance.ShowShortfall(CurrencyType.Gold, cost);
        }

        void Spent(CostButtonView view)
        {
            _lastSpendTime = Time.unscaledTime;
            if (view != null) view.Spent();
        }

        /// <summary>x1을 8초 안에 3번 눌렀으면 길게 누르기를 한 번 알려 준다 (튜토리얼 중에는 말풍선과 겹치지 않게 쉼)</summary>
        void CountTapForHoldHint()
        {
            float now = Time.unscaledTime;
            _tapTimes[_tapIndex] = now;
            _tapIndex = (_tapIndex + 1) % HoldHintTaps;
            float oldest = _tapTimes[_tapIndex];
            if (now - oldest > HoldHintWindow) return;
            if (!Game.IsMetaActive || Game.Tutorial == null || Game.Tutorial.IsActive || Game.Tutorial.HasHintFlag(HoldHintFlag)) return;
            Game.Tutorial.SetHintFlag(HoldHintFlag);
            UIManager.Instance.ShowToast(HoldHintText, ToastKind.Info);
        }

        // ───────────── 갱신 ─────────────

        void RefreshShop()
        {
            if (!isActiveAndEnabled) return;

            var growth = Game.Growth;
            int level = growth.Level;
            bool maxed = growth.IsMaxLevel;
            _levelText.text = maxed ? $"수선점 Lv.{level} <size=70%>(MAX)</size>" : $"수선점 Lv.{level}";

            // 초당 단추는 레벨마다 10%씩 늘어 1,000 미만에서도 소수로 보여줘야 차이가 보인다 (1 → 1.1). 다음 값은 양피지 위 PositiveInk 굵게
            string next = UITheme.ColorTag(UITheme.PositiveInk);
            string current = MenuText.RateText(growth.GoldPerSecond);
            _statText.text = maxed
                ? $"초당 단추 {current}"
                : $"초당 단추 {current} → <b><color={next}>{MenuText.RateText(growth.GetGoldPerSecondAt(level + 1))}</color></b>";
            string hp = NumberFormatter.FormatCurrency(growth.ShopMaxHp);
            _hpText.text = maxed
                ? $"수선점 내구도 {hp}"
                : $"수선점 내구도 {hp} → <b><color={next}>{NumberFormatter.FormatCurrency(growth.GetShopMaxHpAt(level + 1))}</color></b>";

            int remain = growth.RemainLevels;
            _cost1 = growth.GetLevelUpCost(level, Mathf.Min(1, remain));
            _cost10 = growth.GetLevelUpCost(level, Mathf.Min(10, remain));
            _cost1Text.text = NumberFormatter.FormatCurrency(_cost1);
            _cost10Text.text = NumberFormatter.FormatCurrency(_cost10);

            if (maxed)
            {
                SetUnavailable(_cost1View);
                SetUnavailable(_cost10View);
                // 최대 레벨은 다른 버튼과 같은 비활성 그림 + '최대 레벨' — 흐림만 걷고 누르기는 켜지 않는다 (ShowMaxLevel)
                ShowMaxLevel(_levelUpMaxButton, _maxTransition, _costMaxView);
                StopShines();
                return;
            }
            RefreshMax();
            RefreshAffordable(Game.Currency.Get(CurrencyType.Gold));
        }

        static void SetUnavailable(CostButtonView view)
        {
            if (view != null) view.SetState(CostButtonView.State.Unavailable, "최대 레벨");
        }

        /// <summary>
        /// 최대 칸: 지금 단추로 올릴 수 있는 만큼 '최대 +N' (2 이상). 1 이하면 '최대'만 흐리게 (비용 숨김, 누를 수 없음 — x1이 같은 일을 한다,
        /// 금 바탕 그림만 α0.65 · 글자는 그대로 — ApplyMaxDim).
        /// 올릴 수 있는 레벨 수가 바뀌는 단추 경계도 함께 기억해 두어, 단추가 경계를 넘을 때만 다시 계산한다.
        /// </summary>
        void RefreshMax()
        {
            var growth = Game.Growth;
            if (growth.IsMaxLevel) return;
            int level = growth.Level;
            int remain = growth.RemainLevels;
            int count = growth.GetAffordableCount(remain);
            // GetAffordableCount와 같은 순서로 더한 합이라 경계가 정확히 맞는다
            double affordableCost = growth.GetLevelUpCost(level, count);
            _maxShrinkBelow = count > 0 ? affordableCost : double.NegativeInfinity;
            _maxGrowAt = count < remain ? affordableCost + growth.GetLevelUpCost(level + count) : double.PositiveInfinity;
            _maxCount = count;

            bool usable = count >= 2;
            _costMax = usable ? affordableCost : growth.GetLevelUpCost(level, Mathf.Min(1, remain));
            _maxLabel.text = usable ? $"최대 +{count}" : "최대";
            _costMaxText.text = NumberFormatter.FormatCurrency(_costMax);
            if (_costMaxView != null) _costMaxView.SetState(CostButtonView.State.Affordable);
            var row = _costMaxText.transform.parent;
            if (row != null && row != _levelUpMaxButton.transform && row.gameObject.activeSelf != usable) row.gameObject.SetActive(usable);
            ApplyMaxDim(_levelUpMaxButton, _maxTransition, !usable);
        }

        /// <summary>
        /// 최대 버튼 흐림 켜기 · 끄기: 흐리면 누를 수 없게 하되 그림 교체 전환을 잠시 None으로 두어 금 버튼 그림이 그대로 남고(비활성 그림 → 빈 칸처럼 보임),
        /// 금 바탕 그림(targetGraphic)의 색 알파만 MaxDimAlpha — '최대' 글자는 α1 그대로 (묶음 CanvasGroup으로 흐리면 글자가 흐린 금 위에서
        /// 한 번 더 흐려져 ≈2:1이었다). 풀면 누르기 → 원래 전환(transition) 순서로 되돌린다 (전환을 먼저 되돌리면 비활성 그림이 한 번 깜빡인다). 바뀐 것만 쓴다
        /// </summary>
        internal static void ApplyMaxDim(Button button, Selectable.Transition transition, bool dim)
        {
            if (dim)
            {
                if (button.transition != Selectable.Transition.None) button.transition = Selectable.Transition.None;
                // 이미 비활성 그림으로 바뀌어 있었으면 (전환을 바꾸기 전에 꺼져 있던 장면) 원래 그림으로
                if (button.targetGraphic is Image skin && skin.overrideSprite != skin.sprite) skin.overrideSprite = null;
                if (button.interactable) button.interactable = false;
            }
            else
            {
                if (!button.interactable) button.interactable = true;
                if (button.transition != transition) button.transition = transition;
            }
            SetSkinAlpha(button, MaxSkinAlpha(dim));
        }

        /// <summary>
        /// 최대 레벨의 최대 버튼: 흐림(전환 None · 바탕 α)만 걷고 CostButtonView를 Unavailable('최대 레벨')로 — x1 · x10과 같은 비활성 그림.
        /// 누르기는 켜지 않는다: 두 번째부터는 SetState가 같은 상태라 아무것도 하지 않아서, 여기서 켜면 (예전 ApplyMaxDim(false)) 시트를 다시 열 때 ·
        /// 스테이지를 깰 때 · 가방이 바뀔 때마다 최대 레벨의 최대 버튼이 다시 눌렸다 (Wave 3 리뷰). 흐린 채 닿았으면 이미 꺼져 있고, 아니면 CostButtonView가 끈다.
        /// 바탕 α를 SetState보다 먼저 되돌린다 — 넉넉함을 떠날 때 CostButtonView가 지금 바탕 색을 '보통'으로 기억한다
        /// </summary>
        internal static void ShowMaxLevel(Button button, Selectable.Transition transition, CostButtonView view)
        {
            if (button.transition != transition) button.transition = transition;
            SetSkinAlpha(button, 1f);
            if (view != null) SetUnavailable(view);
            else if (button.interactable) button.interactable = false;
        }

        /// <summary>바탕 그림의 색 알파만 바꾼다 (최대 버튼은 레벨 안에서 늘 넉넉함 — CostButtonView가 그동안 바탕 색을 다시 쓰지 않는다)</summary>
        static void SetSkinAlpha(Button button, float alpha)
        {
            var skin = button.targetGraphic;
            if (skin == null) return;
            var color = skin.color;
            if (color.a == alpha) return;
            color.a = alpha;
            skin.color = color;
        }

        /// <summary>최대 버튼 바탕 그림 알파: 흐림 = MaxDimAlpha, 아니면 1</summary>
        internal static float MaxSkinAlpha(bool dim) => dim ? MaxDimAlpha : 1f;

        /// <summary>단추 변화마다 비용 상태만 바꾼다 (매 프레임 호출되므로 문자열을 만들지 않는다 — 같은 상태면 CostButtonView가 아무것도 안 한다)</summary>
        void RefreshAffordable(double gold)
        {
            if (Game.Growth.IsMaxLevel) return;
            bool a1 = gold >= _cost1;
            bool a10 = gold >= _cost10;
            bool aMax = _maxCount >= 2;
            SetAffordable(_cost1View, _cost1Text, a1);
            SetAffordable(_cost10View, _cost10Text, a10);
            UpdateShines(a1, a10, aMax);
        }

        static void SetAffordable(CostButtonView view, TMP_Text costText, bool affordable)
        {
            if (view != null) view.SetAffordable(affordable);
            else if (costText != null) costText.color = affordable ? UITheme.Text : UITheme.CostShortText;
        }

        // ───────────── 반짝임 (M35) ─────────────

        /// <summary>못 사다가 살 수 있게 된 버튼만 한 번 반짝 + 펀치. x10은 살 수 있는 동안 4초마다 한 번 (그동안만 코루틴)</summary>
        void UpdateShines(bool a1, bool a10, bool aMax)
        {
            if (!_affordKnown)
            {
                _affordKnown = true;
                _affordable[0] = a1;
                _affordable[1] = a10;
                _affordable[2] = aMax;
            }
            else
            {
                if (a1 && !_affordable[0]) PlayFlipShine(0, _levelUp1Button);
                if (a10 && !_affordable[1]) PlayFlipShine(1, _levelUp10Button);
                if (aMax && !_affordable[2]) PlayFlipShine(2, _levelUpMaxButton);
                _affordable[0] = a1;
                _affordable[1] = a10;
                _affordable[2] = aMax;
            }

            bool ambient = a10 && !UIMotion.Reduced && _shines.Length > 1 && _shines[1] != null && isActiveAndEnabled;
            if (ambient && _ambient == null) _ambient = StartCoroutine(AmbientShine());
            else if (!ambient && _ambient != null)
            {
                StopCoroutine(_ambient);
                _ambient = null;
            }
        }

        bool ShinesQuiet => UIMotion.Reduced || Time.unscaledTime - _lastSpendTime < UIMotion.ShineSpendQuiet || CoachOnLevelUp1();

        void PlayFlipShine(int index, Button button)
        {
            if (ShinesQuiet || index >= _shines.Length || _shines[index] == null) return;
            MenuFx.Restart(this, ref _shineRoutines[index], Sweep(_shines[index], index, button.transform));
        }

        IEnumerator AmbientShine()
        {
            while (true)
            {
                for (float wait = UIMotion.ShineAmbientInterval; wait > 0f; wait -= Time.unscaledDeltaTime) yield return null;
                if (!ShinesQuiet && _shineRoutines[1] == null) _shineRoutines[1] = StartCoroutine(Sweep(_shines[1], 1, null));
            }
        }

        /// <summary>반짝임 한 번: 0.45초 동안 왼쪽 밖 → 오른쪽 밖, 알파 0 → 0.30 → 0. punch가 있으면 버튼을 살짝 튀긴다 (0.06, 0.20)</summary>
        IEnumerator Sweep(Image shine, int index, Transform punch)
        {
            var rt = shine.rectTransform;
            var mask = rt.parent as RectTransform;
            float half = (mask != null ? mask.rect.width : 300f) * 0.5f + rt.rect.width;
            shine.gameObject.SetActive(true);
            if (punch != null) StartCoroutine(MenuFx.Punch(punch, 0.06f, 0.20f));
            for (float t = 0f; t < UIMotion.ShineSweep; t += Time.unscaledDeltaTime)
            {
                float k = t / UIMotion.ShineSweep;
                rt.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, UIMotion.OutCubic(k)), 0f);
                shine.color = UITheme.WithAlpha(UITheme.Cream, Mathf.Sin(k * Mathf.PI) * UIMotion.ShineAlpha);
                yield return null;
            }
            shine.gameObject.SetActive(false);
            _shineRoutines[index] = null;
        }

        void StopShines()
        {
            if (_ambient != null) StopCoroutine(_ambient);
            _ambient = null;
            for (int i = 0; i < _shineRoutines.Length; i++)
            {
                if (_shineRoutines[i] != null) StopCoroutine(_shineRoutines[i]);
                _shineRoutines[i] = null;
            }
            for (int i = 0; i < _shines.Length; i++)
            {
                if (_shines[i] != null) _shines[i].gameObject.SetActive(false);
            }
            _levelUp1Button.transform.localScale = Vector3.one;
            _levelUp10Button.transform.localScale = Vector3.one;
            _levelUpMaxButton.transform.localScale = Vector3.one;
        }

        /// <summary>코치 손가락이 강화 x1을 가리키는 중 (튜토리얼 6단계 — 반짝임이 손가락과 겹치지 않게)</summary>
        bool CoachOnLevelUp1()
        {
            if (!_coachKnown)
            {
                _coachKnown = true;
                _coach = FindAnyObjectByType<CoachMarkView>(FindObjectsInactive.Include);
            }
            return _coach != null && _coach.IsActive && _coach.Target == (RectTransform)_levelUp1Button.transform;
        }

        // ───────────── 친구 레벨 (M34) ─────────────

        /// <param name="enabled">켜질 때 (정렬을 새로 한다). 아니면 보유 수가 바뀔 때만 다시 정렬</param>
        void RefreshLineages(bool enabled)
        {
            // 꺼져 있으면 OnEnable에서 다시 그리므로 모아 둔 신호는 버린다
            _lineagesDirty = false;
            if (!isActiveAndEnabled || _lineageCards == null) return;

            List<LineageDef> owned = Game.Characters.GetOwnedLineages();
            if (enabled || owned.Count != _order.Count || !SameMembers(owned)) Sort(owned);

            _lineageCards.SetCount(_order.Count);
            for (int i = 0; i < _order.Count; i++) _lineageCards[i].Set(_order[i]);
            _lineageEmptyLabel.SetActive(_order.Count == 0);
            if (_lineageHeader != null)
            {
                _lineageHeader.text = $"친구 레벨 <color={UITheme.ColorTag(UITheme.SubText)}><size=80%>{_order.Count}명</size></color>";
            }
            if (_levelUpAllButton != null) _levelUpAllButton.interactable = _order.Count > 0 && !TutorialTargetsLineage();
            RefreshFade();
        }

        bool SameMembers(List<LineageDef> owned)
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] == null || !_lineageById.ContainsKey(owned[i].id)) return false;
            }
            return true;
        }

        /// <summary>정렬 (켜질 때 · 보유 수가 바뀔 때만 — 레벨업으로는 다시 정렬하지 않는다)</summary>
        void Sort(List<LineageDef> owned)
        {
            string priority = PriorityLineage();
            _sortKeys.Clear();
            _lineageById.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                var lineage = owned[i];
                if (lineage == null || string.IsNullOrEmpty(lineage.id) || _lineageById.ContainsKey(lineage.id)) continue;
                _lineageById[lineage.id] = lineage;
                _sortKeys.Add(new LineageSortKey(lineage.id, lineage.id == priority, PlacedSlot(lineage.id), (int)lineage.grade));
            }
            _sortKeys.Sort(SortComparison);
            _order.Clear();
            for (int i = 0; i < _sortKeys.Count; i++) _order.Add(_lineageById[_sortKeys[i].Id]);
        }

        /// <summary>그 계보가 편성된 가장 앞 칸 (지킴이 0~4, 잔향이 5~10). 편성 밖이면 -1</summary>
        static int PlacedSlot(string lineageId)
        {
            var formation = Game.Formation;
            var db = Game.Characters.Db;
            for (int i = 0; i < FormationSystem.GuardianSlotCount; i++)
            {
                var form = formation.GetGuardian(i);
                if (!string.IsNullOrEmpty(form) && db.GetLineageOfForm(form)?.id == lineageId) return i;
            }
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++)
            {
                var form = formation.GetEcho(i);
                if (!string.IsNullOrEmpty(form) && db.GetLineageOfForm(form)?.id == lineageId) return FormationSystem.GuardianSlotCount + i;
            }
            return -1;
        }

        /// <summary>지금 튜토리얼 단계 · 가이드 퀘스트가 레벨을 올리라고 가리키는 계보 (없으면 null)</summary>
        static string PriorityLineage()
        {
            if (!Game.IsMetaActive) return null;
            var step = Game.Tutorial != null ? Game.Tutorial.Current : null;
            if (step?.complete != null && step.complete.type == MetaConditionType.LineageLevel && !string.IsNullOrEmpty(step.complete.targetId))
                return step.complete.targetId;
            var quest = Game.GuideQuest != null ? Game.GuideQuest.Current : null;
            if (quest?.condition != null && quest.condition.type == MetaConditionType.LineageLevel && !string.IsNullOrEmpty(quest.condition.targetId))
                return quest.condition.targetId;
            return null;
        }

        /// <summary>지금 튜토리얼 단계가 친구 레벨업을 가르치는 중 ('모두 레벨업'은 그동안 막는다 — 손가락이 가리키는 카드 대신 다른 친구가 오르지 않게)</summary>
        static bool TutorialTargetsLineage()
        {
            if (!Game.IsMetaActive || Game.Tutorial == null || !Game.Tutorial.IsActive) return false;
            var complete = Game.Tutorial.Current?.complete;
            if (complete == null) return false;
            return complete.type == MetaConditionType.LineageLevel || complete.type == MetaConditionType.LineageLevelSum
                   || (IsCounter(complete.type) && complete.counter == CounterKey.LineageLevelUp);
        }

        static bool IsCounter(MetaConditionType type) =>
            type == MetaConditionType.Counter || type == MetaConditionType.DailyCounter || type == MetaConditionType.WeeklyCounter;

        /// <summary>코치 손가락이 떠 있으면 목록을 처음으로 (튜토리얼 계보 카드가 맨 앞이라 화면 안에 — CoachTargetResolver.FindCardByLineage)</summary>
        void ScrollForCoach()
        {
            if (_lineageScroll == null || !CoachVisible()) return;
            _lineageScroll.horizontalNormalizedPosition = 0f;
        }

        bool CoachVisible()
        {
            if (!_coachKnown)
            {
                _coachKnown = true;
                _coach = FindAnyObjectByType<CoachMarkView>(FindObjectsInactive.Include);
            }
            return _coach != null && _coach.IsActive;
        }

        /// <summary>목록이 보이는 폭보다 넓으면 오른쪽 끝 흐림 (끝까지 굴리면 끈다). 폭은 카드 수로 바로 계산 (레이아웃을 기다리지 않는다)</summary>
        void RefreshFade()
        {
            if (_lineageFade == null || _lineageScroll == null || _lineageScroll.viewport == null) return;
            float cardWidth = _lineagePrefab != null ? ((RectTransform)_lineagePrefab.transform).sizeDelta.x : 212f;
            int count = _order.Count;
            float width = count * cardWidth;
            if (_lineageLayout != null) width += _lineageLayout.padding.horizontal + Mathf.Max(0, count - 1) * _lineageLayout.spacing;
            _fadeWide = width > _lineageScroll.viewport.rect.width + 0.5f;
            ApplyFade(_lineageScroll.horizontalNormalizedPosition);
        }

        void OnLineageScrolled(Vector2 position) => ApplyFade(position.x);

        void ApplyFade(float position)
        {
            if (_lineageFade == null) return;
            bool show = _fadeWide && position < FadeHideAt;
            if (_lineageFade.activeSelf != show) _lineageFade.SetActive(show);
        }

        /// <summary>
        /// 모두 레벨업 (P2): 단추가 떨어질 때까지 가장 낮은 레벨 친구부터 한 레벨씩 돌아가며 올린다. '친구 N명 레벨업 · 전투력 +X'.
        /// 한 명도 못 올리면 가장 싼 친구 기준 단추 부족 토스트. 튜토리얼이 친구 레벨업을 가르치는 동안은 막혀 있다.
        /// </summary>
        void LevelUpAll()
        {
            if (TutorialTargetsLineage() || _order.Count == 0) return;
            // 친구 레벨업이 잠겼으면 (F-03, 1-1 전) 카드와 같은 안내 — 레벨은 그대로
            if (GrowthLineageCardView.LevelUpLocked)
            {
                string locked = Game.Features.GetRequirementText(FeatureId.CharacterLevelUp);
                UIManager.Instance.ShowToast(string.IsNullOrEmpty(locked) ? MetaText.UnlockSentence(2) : locked, ToastKind.Error);
                Haptics.Play(HapticKind.Light);
                return;
            }
            var characters = Game.Characters;
            long powerBefore = Game.Power != null ? Game.Power.TeamPower : 0;
            var raised = new HashSet<string>();
            double cheapest = double.PositiveInfinity;
            // 한 번에 너무 오래 돌지 않게 (친구 30명 × 최대 레벨이어도 넉넉)
            for (int guard = 0; guard < 2000; guard++)
            {
                LineageDef pick = null;
                int pickLevel = int.MaxValue;
                double gold = Game.Currency.Get(CurrencyType.Gold);
                for (int i = 0; i < _order.Count; i++)
                {
                    var lineage = _order[i];
                    int level = characters.GetLevel(lineage.id);
                    if (level >= characters.MaxLevel) continue;
                    double cost = characters.GetLevelUpCost(lineage.id);
                    if (cost < cheapest && raised.Count == 0) cheapest = cost;
                    if (cost > gold || level >= pickLevel) continue;
                    pick = lineage;
                    pickLevel = level;
                }
                if (pick == null || !characters.TryLevelUp(pick.id)) break;
                raised.Add(pick.id);
            }

            if (raised.Count == 0)
            {
                if (double.IsPositiveInfinity(cheapest)) UIManager.Instance.ShowToast("모든 친구가 최대 레벨이에요.");
                else UIManager.Instance.ShowShortfall(CurrencyType.Gold, cheapest);
                return;
            }
            _lastSpendTime = Time.unscaledTime;
            UISfx.Play(UISfxKey.Spend);
            long gain = Game.Power != null ? Math.Max(0, Game.Power.TeamPower - powerBefore) : 0;
            UIManager.Instance.ShowToast(gain > 0
                ? $"친구 {raised.Count}명 레벨업 · 전투력 +{PowerSystem.Format(gain)}"
                : $"친구 {raised.Count}명 레벨업", ToastKind.Success);
        }

        // ───────────── 데려오기 (C-03) ─────────────

        void StartFocus() => MenuFx.Restart(this, ref _focusRoutine, FocusRoutine());

        IEnumerator FocusRoutine()
        {
            // 시트가 막 열렸으면 목록 레이아웃이 자리를 잡을 때까지 한 프레임
            yield return null;
            Canvas.ForceUpdateCanvases();
            string id = s_focusLineage;
            s_focusLineage = null;
            int index = -1;
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i].id == id)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0 || index >= _lineageCards.Count || _lineageScroll == null)
            {
                _focusRoutine = null;
                yield break;
            }

            var card = _lineageCards[index];
            float to = CenteredPosition((RectTransform)card.transform);
            float from = _lineageScroll.horizontalNormalizedPosition;
            if (!UIMotion.Reduced)
            {
                for (float t = 0f; t < FocusScroll; t += Time.unscaledDeltaTime)
                {
                    _lineageScroll.horizontalNormalizedPosition = Mathf.Lerp(from, to, UIMotion.OutCubic(t / FocusScroll));
                    yield return null;
                }
            }
            _lineageScroll.horizontalNormalizedPosition = to;
            _lineageScroll.velocity = Vector2.zero;
            card.PlayFocus(FocusSeconds);
            _focusRoutine = null;
        }

        /// <summary>카드가 보이는 칸 가운데에 오는 스크롤 위치 (0~1)</summary>
        float CenteredPosition(RectTransform card)
        {
            float viewport = _lineageScroll.viewport.rect.width;
            float scrollable = _lineageContent.rect.width - viewport;
            if (scrollable <= 0f) return 0f;
            float center = card.localPosition.x + (0.5f - card.pivot.x) * card.rect.width;
            return Mathf.Clamp01((center - viewport * 0.5f) / scrollable);
        }

        // ───────────── 정렬 키 (M34, 테스트용 순수 함수) ─────────────

        /// <summary>계보 카드 정렬 키</summary>
        internal readonly struct LineageSortKey
        {
            public readonly string Id;
            /// <summary>지금 튜토리얼 · 가이드 퀘스트가 가리키는 계보</summary>
            public readonly bool Priority;
            /// <summary>편성 칸 순서 (지킴이 0~4, 잔향이 5~10), 편성 밖 -1</summary>
            public readonly int Slot;
            /// <summary>등급 (ItemGrade 정수, 클수록 높다)</summary>
            public readonly int Grade;

            public LineageSortKey(string id, bool priority, int slot, int grade)
            {
                Id = id;
                Priority = priority;
                Slot = slot;
                Grade = grade;
            }
        }

        /// <summary>가리키는 계보 → 편성된 계보(칸 순서) → 편성 밖(등급 높은 순) → ID 순</summary>
        internal static int CompareLineages(in LineageSortKey a, in LineageSortKey b)
        {
            if (a.Priority != b.Priority) return a.Priority ? -1 : 1;
            bool placedA = a.Slot >= 0;
            bool placedB = b.Slot >= 0;
            if (placedA != placedB) return placedA ? -1 : 1;
            int order = placedA ? a.Slot.CompareTo(b.Slot) : b.Grade.CompareTo(a.Grade);
            return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
        }
    }
}
