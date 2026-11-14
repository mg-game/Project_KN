using System;
using System.Collections.Generic;

namespace IdleGame
{
    [Serializable]
    public class RewardData
    {
        public RewardType type;
        public CurrencyType currency;
        public string itemId;
        public double amount;

        public static RewardData Currency(CurrencyType currency, double amount) =>
            new RewardData { type = RewardType.Currency, currency = currency, amount = amount };

        public static RewardData Item(string itemId, int count = 1) =>
            new RewardData { type = RewardType.Item, itemId = itemId, amount = count };
    }

    /// <summary>우편/이벤트 등 여러 곳에서 쓰는 공통 보상 지급 창구.</summary>
    public class RewardService
    {
        readonly CurrencySystem _currency;
        readonly InventorySystem _inventory;
        readonly ItemDatabase _items;

        public RewardService(CurrencySystem currency, InventorySystem inventory, ItemDatabase items)
        {
            _currency = currency;
            _inventory = inventory;
            _items = items;
        }

        public void Grant(IEnumerable<RewardData> rewards)
        {
            foreach (var reward in rewards) Grant(reward);
        }

        public void Grant(RewardData reward)
        {
            switch (reward.type)
            {
                case RewardType.Currency:
                    _currency.Add(reward.currency, reward.amount);
                    break;
                case RewardType.Item:
                    _inventory.Add(reward.itemId, (int)reward.amount);
                    break;
            }
        }

        /// <summary>표시 문구 ("단추 1,200", "수선 천 ×5"). 개수는 곱하기 기호(U+00D7)로 쓴다</summary>
        public string Describe(RewardData reward)
        {
            switch (reward.type)
            {
                case RewardType.Currency:
                    return $"{CurrencyName(reward.currency)} {FormatAmount(reward.currency, reward.amount)}";
                case RewardType.Item:
                    var def = _items.Get(reward.itemId);
                    return $"{(def != null ? def.name : reward.itemId)} ×{(int)reward.amount}";
                default:
                    return string.Empty;
            }
        }

        public static string CurrencyName(CurrencyType type) => type switch
        {
            CurrencyType.Gold => "단추",
            CurrencyType.Gem => "별조각",
            CurrencyType.EventToken => "별빛 사탕",
            CurrencyType.LineageXp => "실타래",
            _ => type.ToString(),
        };

        /// <summary>재화 양 표기 (모든 재화 FormatCurrency: 100만 미만 콤마 "1,234", 그 위 "1.23B")</summary>
        public static string FormatAmount(CurrencyType type, double amount) => NumberFormatter.FormatCurrency(amount);

        /// <summary>좁은 칩용 재화 양 표기 (FormatCompact: 1만 미만 콤마, 그 위 "12.3A")</summary>
        public static string FormatAmountCompact(CurrencyType type, double amount) => NumberFormatter.FormatCompact(amount);

        /// <summary>같은 재화/아이템 보상을 합친다. (일괄 수령 결과 표시용)</summary>
        public static List<RewardData> Merge(IEnumerable<RewardData> rewards)
        {
            var merged = new List<RewardData>();
            foreach (var r in rewards)
            {
                var same = merged.Find(m => m.type == r.type && m.currency == r.currency && m.itemId == r.itemId);
                if (same != null) same.amount += r.amount;
                else merged.Add(new RewardData { type = r.type, currency = r.currency, itemId = r.itemId, amount = r.amount });
            }
            return merged;
        }
    }
}
