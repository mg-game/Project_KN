using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 실타래 잔액 표시 (T4 · T6 · T10, 결정 2 안 D). 강화 탭 버튼의 꼬리표와 강화 탭 친구 머리 줄의 잔액 칩이 쓴다.
    ///   · 숫자 = 실타래 잔액 − 날아오는 중인 양(RewardFlyer.PendingOf) − 보상 팝업이 붙잡은 양(RewardPopup.HeldOf), 0 아래로 내려가지 않는다.
    ///     보상 팝업이 떠 있는 동안 숫자가 위 · 아래 · 위로 흔들리지 않게 한다 (T10). _compact면 K/M 축약 (꼬리표).
    ///   · 보유한 유실자가 모두 최대 레벨이면 숫자 대신 "모두 최대" (T6). 실타래는 그대로 쌓인다.
    ///   · _requireUnlock이면 유실자 레벨업이 열린 뒤(FeatureId.CharacterLevelUp, 1-1 클리어)부터 _root를 켠다 (꼬리표). 이 컴포넌트는 늘 켜진 자리에 둔다.
    ///   · _button이 있으면 누르면 얻는 곳 팝업 (ShowShortfall(실타래, 0)). 길게 누르면 같은 오브젝트의 CurrencyStripView가 말풍선을 띄운다.
    /// 다시 쓰는 때: 실타래 변경 · 떠 있는 양 변경 · 붙잡은 양 변경 · 유실자 변경(합류 · 레벨) · 기능 개방. 같은 글이면 다시 쓰지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LineageXpBalanceView : MonoBehaviour
    {
        /// <summary>보유 전원 최대 레벨일 때 숫자 대신 보이는 글 (T6)</summary>
        public const string AllMaxText = "모두 최대";

        [Tooltip("숫자 글자")]
        [SerializeField] TMP_Text _amount;
        [Tooltip("켜고 끌 그림 묶음 (비면 늘 보임). 날아가기 도착지도 이 자리")]
        [SerializeField] GameObject _root;
        [Tooltip("유실자 레벨업이 열린 뒤에만 보인다 (강화 탭 꼬리표)")]
        [SerializeField] bool _requireUnlock;
        [Tooltip("K/M 축약 숫자 (좁은 꼬리표)")]
        [SerializeField] bool _compact;
        [Tooltip("누르면 얻는 곳 팝업 (비면 누름 없음)")]
        [SerializeField] Button _button;

        bool _subscribed;
        FeatureGateSystem _features;
        string _shown;

        /// <summary>날아가기 도착지 (켜고 끄는 묶음, 없으면 이 칸)</summary>
        public RectTransform Target => _root != null ? (RectTransform)_root.transform : (RectTransform)transform;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnClick);
        }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy()
        {
            Unsubscribe();
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        void Subscribe()
        {
            if (_subscribed || !Game.IsReady || Game.Currency == null) return;
            _subscribed = true;
            Game.Currency.Changed += OnCurrencyChanged;
            if (Game.Characters != null) Game.Characters.Changed += Refresh;
            RewardFlyer.PendingChanged += OnPendingChanged;
            RewardPopup.HeldChanged += Refresh;
            if (Game.IsMetaActive && Game.Features != null)
            {
                _features = Game.Features;
                _features.Unlocked += OnFeatureUnlocked;
            }
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.Currency != null) Game.Currency.Changed -= OnCurrencyChanged;
            if (Game.Characters != null) Game.Characters.Changed -= Refresh;
            RewardFlyer.PendingChanged -= OnPendingChanged;
            RewardPopup.HeldChanged -= Refresh;
            if (_features != null) _features.Unlocked -= OnFeatureUnlocked;
            _features = null;
        }

        static CurrencyType Currency => Game.IsReady && Game.Characters != null ? Game.Characters.LevelUpCurrency : CurrencyType.LineageXp;

        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (type == Currency) Refresh();
        }

        void OnPendingChanged(CurrencyType type)
        {
            if (type == Currency) Refresh();
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature == FeatureId.CharacterLevelUp) Refresh();
        }

        /// <summary>보이기 · 숫자를 지금 값으로 맞춘다</summary>
        public void Refresh()
        {
            if (!Game.IsReady || Game.Currency == null) return;
            if (_root != null)
            {
                bool visible = !_requireUnlock || Game.Characters == null || Game.Characters.IsLevelUpUnlocked;
                if (_root.activeSelf != visible) _root.SetActive(visible);
                if (!visible) return;
            }
            if (_amount == null) return;
            string text = Text(Currency, _compact);
            if (text == _shown) return;
            _shown = text;
            _amount.text = text;
        }

        /// <summary>보이는 숫자 (잔액 − 날아오는 양 − 붙잡은 양, 0 이상)</summary>
        public static double ShownAmount(CurrencyType type)
        {
            if (!Game.IsReady || Game.Currency == null) return 0d;
            double shown = Game.Currency.Get(type) - RewardFlyer.PendingOf(type) - RewardPopup.HeldOf(type);
            return shown > 0d ? shown : 0d;
        }

        /// <summary>보이는 글: 보유 전원 최대면 "모두 최대", 아니면 ShownAmount (compact면 K/M 축약)</summary>
        public static string Text(CurrencyType type, bool compact)
        {
            if (Game.IsReady && Game.Characters != null && Game.Characters.AllOwnedMaxLevel) return AllMaxText;
            double amount = ShownAmount(type);
            return compact ? RewardService.FormatAmountCompact(type, amount) : RewardService.FormatAmount(type, amount);
        }

        void OnClick()
        {
            var ui = UIManager.Instance;
            if (ui != null) ui.ShowShortfall(Currency, 0d);
        }
    }
}
