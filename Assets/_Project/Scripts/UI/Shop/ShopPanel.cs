using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 상점 탭 (UX-SPEC §2.9): 제목 줄 없이 위 12에 쪽 고르기 "수선점 상점" / "별이 머문 밤 산책"(이벤트, 별빛 사탕) — M53 쪽 탭 모양(이벤트 탭은 고르면 6px 분홍 밑줄).
    /// 수선점 상점 쪽에서는 탭 줄 오른쪽에 '초기화까지 7:38:48' 알약 (1초마다, 0이 되면 하루 넘김을 확인하고 카드를 다시 채운다 — M77). 상품 카드는 4열 격자.
    /// 구매 횟수·보유 여부는 Shop.Changed / Costumes.Changed에, 가격 버튼 상태는 Currency.Changed에 맞춰 갱신한다.
    /// SPEC4 (P4-4):
    ///   · NEW-02 오늘의 작은 선물 (§13.7): 수선점 상점 쪽 맨 위(설명 아래 168)에 DailyGiftCardView 860x120 — 보이면 상품 목록이 132 내려간다 (168 → 300).
    ///     상점이 열렸고(1-8) 오늘 선물 내용이 있을 때만, DailyGift.Changed(받음 · 날 넘김)에 다시 그린다. 카드가 없는 예전 장면은 지금 그대로.
    ///   · EVT-01 '축제 중' 알약 (§8.3): 별이 머문 밤 산책(Festival 이벤트)이 열려 있는 동안 이벤트 탭 오른쪽 위에 DustyPink 알약 (그동안 반짝이 그림은 숨긴다).
    ///     Events.StageCandyMultiplier &gt; 1 = 축제 이벤트가 켜져 있음 (GetActive와 같은 판정, 할당 없음). Events.Changed · 보일 때 다시 본다.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        public const string NormalShopId = ShopDatabase.NormalShopId;
        public const string EventShopId = ShopDatabase.EventShopId;
        /// <summary>상품 목록 윗변 위치 (일반 상점 / 이벤트 배너 아래). 씬 빌더와 공유</summary>
        public const float NormalListTop = 168f;
        public const float EventListTop = 312f;
        /// <summary>'축제 중' 알약 글 (별이 머문 밤 산책이 열려 있는 동안 이벤트 탭에)</summary>
        public const string FestivalPillText = "축제 중";

        [SerializeField] Button _normalTab;
        [SerializeField] Button _eventTab;
        [SerializeField] TMP_Text _normalTabLabel;
        [SerializeField] TMP_Text _eventTabLabel;
        [Tooltip("이벤트 탭 아래 6px 분홍 밑줄 (고르면 켜진다)")]
        [SerializeField] Image _eventTabAccent;
        [SerializeField] TMP_Text _descText;
        [SerializeField] GameObject _eventBanner;
        [SerializeField] TMP_Text _eventDescText;
        [SerializeField] TMP_Text _eventBalanceText;
        [SerializeField] RectTransform _listRoot;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        [SerializeField] ShopProductCardView _cardPrefab;
        [SerializeField] GameObject _emptyLabel;
        [Header("초기화까지 (M77)")]
        [SerializeField] GameObject _resetPill;
        [SerializeField] TMP_Text _resetText;
        [Header("SPEC4 (다시 빌드한 장면에만 — 없으면 건너뛴다)")]
        [Tooltip("NEW-02 오늘의 작은 선물 카드 (수선점 상점 쪽 맨 위)")]
        [SerializeField] DailyGiftCardView _dailyGift;
        [Tooltip("EVT-01 '축제 중' 알약 (이벤트 탭 오른쪽 위, 꺼 둠)")]
        [SerializeField] GameObject _festivalPill;
        [Tooltip("이벤트 탭 반짝이 그림 (알약이 보이는 동안 숨긴다, 없어도 됨)")]
        [SerializeField] GameObject _eventSparkle;

        UIListPool<ShopProductCardView> _cards;
        string _shopId = NormalShopId;
        bool _started;
        float _resetTimer;
        long _shownResetSeconds = -1;

        public string ShopId => _shopId;

        void Start()
        {
            _cards = new UIListPool<ShopProductCardView>(_cardPrefab, _content);
            _normalTab.onClick.AddListener(() => SelectShop(NormalShopId));
            _eventTab.onClick.AddListener(() => SelectShop(EventShopId));

            Game.Shop.Changed += Refresh;
            Game.Costumes.Changed += Refresh;
            Game.Currency.Changed += OnCurrencyChanged;
            if (Game.DailyGift != null) Game.DailyGift.Changed += OnDailyGiftChanged;
            if (Game.Events != null) Game.Events.Changed += OnEventsChanged;
            _started = true;
            Refresh();
        }

        void OnEnable()
        {
            if (!_started) return;
            _resetTimer = 0f;
            Refresh();
        }

        void OnDestroy()
        {
            if (!_started || !Game.IsReady) return;
            Game.Shop.Changed -= Refresh;
            Game.Costumes.Changed -= Refresh;
            Game.Currency.Changed -= OnCurrencyChanged;
            if (Game.DailyGift != null) Game.DailyGift.Changed -= OnDailyGiftChanged;
            if (Game.Events != null) Game.Events.Changed -= OnEventsChanged;
        }

        /// <summary>'초기화까지' 알약: 수선점 상점 쪽이 보이는 동안 1초마다 (초가 바뀔 때만 글을 쓴다)</summary>
        void Update()
        {
            if (!_started || _resetPill == null || !_resetPill.activeSelf) return;
            _resetTimer -= Time.unscaledDeltaTime;
            if (_resetTimer > 0f) return;
            _resetTimer = 1f;
            if (Game.Shop.TimeUntilReset.TotalSeconds < 1d)
            {
                // 새벽 초기화: 구매 횟수를 비우고(Changed → Refresh) 카드를 다시 채운다. 비울 게 없었으면 직접 다시 채운다
                if (!Game.Shop.CheckDayRollover()) Refresh();
                return;
            }
            WriteReset();
        }

        /// <summary>'초기화까지 7:38:48' (초가 바뀔 때만 글을 쓴다 — 하루 넘김은 Update가 본다)</summary>
        void WriteReset()
        {
            if (_resetText == null) return;
            var left = Game.Shop.TimeUntilReset;
            long seconds = (long)left.TotalSeconds;
            if (seconds == _shownResetSeconds) return;
            _shownResetSeconds = seconds;
            _resetText.text = "초기화까지 " + UIFormat.ClockMono(left);
        }

        /// <summary>하위 상점 선택 (획득처 이동에서도 호출)</summary>
        public void SelectShop(string shopId)
        {
            _shopId = shopId == EventShopId ? EventShopId : NormalShopId;
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            Refresh();
        }

        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (!isActiveAndEnabled || !_started) return;
            if (type == CurrencyType.EventToken) RefreshBalance();
            for (int i = 0; i < _cards.Count; i++) _cards[i].RefreshAffordable();
        }

        void Refresh()
        {
            if (!isActiveAndEnabled || !_started) return;

            bool isEvent = _shopId == EventShopId;
            TabStyle.Apply(_normalTab.targetGraphic, _normalTabLabel, !isEvent, TabVariant.Page);
            TabStyle.Apply(_eventTab.targetGraphic, _eventTabLabel, isEvent, TabVariant.Page, _eventTabAccent, UITheme.DustyPink);

            var shop = Game.Shop.Db.Get(_shopId);
            _eventBanner.SetActive(isEvent);
            _descText.gameObject.SetActive(!isEvent);
            string description = shop != null ? shop.description : string.Empty;
            _descText.text = description;
            _eventDescText.text = description;
            RefreshDailyGift();
            RefreshFestival();
            RefreshBalance();

            if (_resetPill != null)
            {
                _resetPill.SetActive(!isEvent);
                if (!isEvent)
                {
                    _shownResetSeconds = -1;
                    _resetTimer = 1f;
                    WriteReset();
                }
            }

            int count = shop != null ? shop.products.Count : 0;
            _cards.SetCount(count);
            for (int i = 0; i < count; i++) _cards[i].Set(shop, shop.products[i]);
            _emptyLabel.SetActive(count == 0);
        }

        // ───────────── SPEC4: 오늘의 작은 선물 · 축제 중 ─────────────

        /// <summary>
        /// 상품 목록 윗변 (테스트용 순수 함수): 이벤트 쪽 312, 수선점 상점 쪽 168 — 선물 카드가 보이면 그만큼(132) 더 내려 300
        /// </summary>
        internal static float ListTop(bool isEvent, bool giftShown) =>
            isEvent ? EventListTop : NormalListTop + (giftShown ? DailyGiftCardView.ListShift : 0f);

        void OnDailyGiftChanged()
        {
            if (!isActiveAndEnabled || !_started) return;
            RefreshDailyGift();
        }

        void OnEventsChanged()
        {
            if (!isActiveAndEnabled || !_started) return;
            RefreshFestival();
        }

        /// <summary>선물 카드를 보이거나 숨기고 상품 목록 윗변을 맞춘다 (카드가 없는 예전 장면은 지금 그대로 168 / 312)</summary>
        void RefreshDailyGift()
        {
            bool isEvent = _shopId == EventShopId;
            bool gift = !isEvent && _dailyGift != null && DailyGiftCardView.ShouldShow;
            if (_dailyGift != null)
            {
                if (_dailyGift.gameObject.activeSelf != gift) _dailyGift.gameObject.SetActive(gift);
                else if (gift) _dailyGift.Refresh();
            }
            float top = ListTop(isEvent, gift);
            if (!Mathf.Approximately(_listRoot.offsetMax.y, -top)) _listRoot.offsetMax = new Vector2(_listRoot.offsetMax.x, -top);
        }

        /// <summary>'축제 중' 알약 (별이 머문 밤 산책이 열려 있는 동안) — 알약이 보이면 같은 모서리의 반짝이 그림은 숨긴다</summary>
        void RefreshFestival()
        {
            if (_festivalPill == null) return;
            bool festival = IsFestivalRunning;
            if (_festivalPill.activeSelf != festival) _festivalPill.SetActive(festival);
            if (_eventSparkle != null && _eventSparkle.activeSelf == festival) _eventSparkle.SetActive(!festival);
        }

        /// <summary>축제(Festival) 이벤트가 지금 열려 있는지 — 별빛 사탕 배율이 1보다 크면 축제 중 (EventCalendarSystem: 잠겼으면 1)</summary>
        static bool IsFestivalRunning
        {
            get
            {
                if (!MetaUi.IsActive || Game.Events == null) return false;
                try
                {
                    return Game.Events.StageCandyMultiplier > 1.0;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Shop] 이벤트 상태를 읽지 못했어요: {e.Message}");
                    return false;
                }
            }
        }

        void RefreshBalance()
        {
            _eventBalanceText.text = RewardService.FormatAmount(CurrencyType.EventToken, Game.Currency.Get(CurrencyType.EventToken));
        }
    }
}
