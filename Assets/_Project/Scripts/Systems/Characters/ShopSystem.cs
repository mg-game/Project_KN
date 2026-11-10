using System;
using System.Globalization;

namespace IdleGame
{
    /// <summary>
    /// 상점 (일반 상점 / '별이 머문 밤 산책' 이벤트 상점). 일일/전체 구매 제한. 의상 상품은 보유하면 더 살 수 없다.
    /// 일일 구매 수는 "오늘"이 바뀌면 초기화된다 (M77). 오늘 = DailyClock 날 키 (현지 05:00 초기화, 시계 되돌림에도 거꾸로 가지 않음) —
    /// 출석·일지·무료 빠른 수선과 같은 시각에 함께 채워진다. DailyClock.DayChanged를 받아 CheckDayRollover가 기록을 비우고
    /// Changed를 알려, 열려 있는 상점의 품절 표시도 다시 채워진다. 읽을 때도 날 키를 비교하므로(GetDailyCount) 이벤트를 놓쳐도 안전하다.
    /// 일일 시계가 없으면(편집 모드 테스트) 예전처럼 현지 자정 기준이고, 재화 변경을 박동 삼아 자정을 확인한다.
    /// 날짜 형식은 그대로 "yyyy-MM-dd"라 옛 세이브를 옮기지 않는다 (00:00~05:00 사이에 업데이트하면 한 번 더 채워질 수 있다 — 허용).
    /// </summary>
    public class ShopSystem
    {
        /// <summary>구매 또는 일일 초기화(새벽 5시)로 구매 기록이 바뀌었을 때</summary>
        public event Action Changed;
        /// <summary>상품을 샀을 때 (Changed 다음에). 진행 기록기(ShopBuy)용</summary>
        public event Action<ShopProductDef> Purchased;

        readonly PlayerData _data;
        readonly ShopDatabase _db;
        readonly CurrencySystem _currency;
        readonly RewardService _rewards;
        readonly CostumeSystem _costumes;
        readonly ITimeProvider _clock;
        /// <summary>"오늘"의 기준 (null이면 현지 자정 기준 — 편집 모드 테스트)</summary>
        readonly DailyClock _dailyClock;

        /// <summary>(일일 시계가 없을 때) 마지막으로 날짜를 확인한 현지 하루의 범위 (UTC). 이 안이면 날짜 문자열을 만들지 않고 넘어간다</summary>
        DateTime _dayStartUtc = DateTime.MaxValue;
        DateTime _dayEndUtc = DateTime.MinValue;

        public ShopSystem(PlayerData data, ShopDatabase db, CurrencySystem currency, RewardService rewards, CostumeSystem costumes, ITimeProvider clock,
            DailyClock dailyClock = null)
        {
            _data = data;
            _db = db;
            _currency = currency;
            _rewards = rewards;
            _costumes = costumes;
            _clock = clock;
            _dailyClock = dailyClock;
            if (_dailyClock != null)
            {
                // 날이 바뀌면 (DailyClock.Refresh — 부팅·복귀·1초마다) 바로 비운다
                _dailyClock.DayChanged += OnDayChanged;
            }
            else
            {
                // 방치 수익으로 재화는 매 프레임 바뀌므로, 이를 박동 삼아 자정 초기화를 확인한다 (평소엔 시각 비교 한 번)
                _currency.Changed += OnCurrencyChanged;
            }
        }

        /// <summary>다음 일일 초기화까지 남은 시간 (상점 "초기화까지 7:38:48"). 일일 시계가 없으면 다음 현지 자정까지</summary>
        public TimeSpan TimeUntilReset
        {
            get
            {
                if (_dailyClock != null) return _dailyClock.TimeUntilDailyReset;
                var now = _clock.UtcNow;
                var next = now.ToLocalTime().Date.AddDays(1).ToUniversalTime();
                return next > now ? next - now : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// "오늘"이 바뀌었으면 일일 구매 기록을 비우고 Changed를 알린다. 비웠으면 true.
        /// 같은 날 안에서는 캐시된 날 키 비교(또는 시각 비교) 한 번이라 자주 불러도 된다 (DailyClock.DayChanged에서 자동으로 불린다).
        /// </summary>
        public bool CheckDayRollover()
        {
            if (_dailyClock == null)
            {
                var now = _clock.UtcNow;
                if (now >= _dayStartUtc && now < _dayEndUtc) return false;
                UpdateDayBounds(now);
            }

            var shop = _data.shop;
            if (shop.dailyPurchases.Count == 0) return false;
            string today = Today;
            if (shop.dailyDate == today) return false;
            shop.dailyPurchases.Clear();
            shop.dailyDate = today;
            Changed?.Invoke();
            return true;
        }

        public ShopDatabase Db => _db;

        /// <summary>상점의 상품. 없으면 null</summary>
        public ShopProductDef GetProduct(string shopId, string productId) => _db != null ? _db.FindProduct(shopId, productId) : null;

        /// <summary>남은 구매 가능 횟수. 제한 없으면 -1 (의상 상품은 최대 1, 보유 중이면 0)</summary>
        public int GetRemaining(ShopProductDef product)
        {
            if (product == null || string.IsNullOrEmpty(product.id)) return 0;
            bool isCostume = IsCostume(product);
            if (isCostume && _costumes.IsUnlocked(product.costumeId)) return 0;

            int remaining = -1;
            if (product.dailyLimit > 0) remaining = Limit(remaining, product.dailyLimit - GetDailyCount(product.id));
            if (product.totalLimit > 0) remaining = Limit(remaining, product.totalLimit - GetTotalCount(product.id));
            if (isCostume) remaining = Limit(remaining, 1);
            return remaining;
        }

        public bool CanBuy(ShopProductDef product) => Validate(product) == ShopBuyResult.Ok;

        public ShopBuyResult TryBuy(string shopId, string productId)
        {
            var product = GetProduct(shopId, productId);
            var result = Validate(product);
            if (result != ShopBuyResult.Ok) return result;
            if (!_currency.TrySpend(product.currency, product.price)) return ShopBuyResult.NotEnoughCurrency;

            RecordPurchase(product.id);
            if (IsCostume(product)) _costumes.Unlock(product.costumeId);
            else _rewards.Grant(product.rewards);
            Changed?.Invoke();
            Purchased?.Invoke(product);
            return ShopBuyResult.Ok;
        }

        /// <summary>구매 결과 안내 문구 (토스트용). product를 주면 "별빛 사탕이 부족해요"처럼 재화 이름을 쓴다</summary>
        public static string Describe(ShopBuyResult result, ShopProductDef product = null) => result switch
        {
            ShopBuyResult.Ok => "구매했어요!",
            ShopBuyResult.NotFound => "지금은 판매하지 않는 상품이에요",
            ShopBuyResult.NotEnoughCurrency => product != null
                ? $"{KoreanText.WithSubject(RewardService.CurrencyName(product.currency))} 부족해요"
                : "재화가 부족해요",
            ShopBuyResult.SoldOut => "구매 가능 횟수를 모두 사용했어요",
            ShopBuyResult.AlreadyOwned => "이미 보유한 의상이에요",
            _ => string.Empty,
        };

        ShopBuyResult Validate(ShopProductDef product)
        {
            if (!IsSellable(product)) return ShopBuyResult.NotFound;
            if (IsCostume(product) && _costumes.IsUnlocked(product.costumeId)) return ShopBuyResult.AlreadyOwned;
            if (GetRemaining(product) == 0) return ShopBuyResult.SoldOut;
            if (!_currency.Has(product.currency, product.price)) return ShopBuyResult.NotEnoughCurrency;
            return ShopBuyResult.Ok;
        }

        /// <summary>ID, 가격, 지급 내용(의상 또는 보상)이 올바른 상품인지</summary>
        bool IsSellable(ShopProductDef product)
        {
            if (product == null || string.IsNullOrEmpty(product.id) || double.IsNaN(product.price) || product.price < 0) return false;
            return IsCostume(product)
                ? _costumes.Get(product.costumeId) != null
                : product.rewards != null && product.rewards.Count > 0;
        }

        static bool IsCostume(ShopProductDef product) => !string.IsNullOrEmpty(product.costumeId);

        /// <summary>남은 횟수 합치기 (-1 = 무제한)</summary>
        static int Limit(int current, int left)
        {
            left = Math.Max(0, left);
            return current < 0 ? left : Math.Min(current, left);
        }

        void OnCurrencyChanged(CurrencyType type, double value) => CheckDayRollover();

        void OnDayChanged(string dayKey) => CheckDayRollover();

        /// <summary>now가 속한 현지 하루(자정 ~ 다음 자정)를 UTC로 기억한다. 기기 시각을 되돌려도 범위를 벗어나 다시 계산된다</summary>
        void UpdateDayBounds(DateTime utcNow)
        {
            var localDate = utcNow.ToLocalTime().Date;
            _dayStartUtc = localDate.ToUniversalTime();
            _dayEndUtc = localDate.AddDays(1).ToUniversalTime();
        }

        void RecordPurchase(string productId)
        {
            var shop = _data.shop;
            string today = Today;
            if (shop.dailyDate != today)
            {
                shop.dailyPurchases.Clear();
                shop.dailyDate = today;
            }
            shop.dailyPurchases[productId] = GetDailyCount(productId) + 1;
            shop.totalPurchases[productId] = GetTotalCount(productId) + 1;
        }

        /// <summary>오늘 구매 수. 저장된 날 키가 오늘이 아니면 0 (기록 초기화는 CheckDayRollover 또는 다음 구매 때)</summary>
        int GetDailyCount(string productId) =>
            _data.shop.dailyDate == Today && _data.shop.dailyPurchases.TryGetValue(productId, out var count) ? count : 0;

        int GetTotalCount(string productId) => _data.shop.totalPurchases.TryGetValue(productId, out var count) ? count : 0;

        /// <summary>오늘 날 키 "yyyy-MM-dd" (DailyClock = 현지 05:00 기준, 같은 날이면 같은 문자열 인스턴스라 할당 없음)</summary>
        string Today => _dailyClock != null ? _dailyClock.DayKey : _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
