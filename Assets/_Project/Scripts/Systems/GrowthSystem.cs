using System;

namespace IdleGame
{
    /// <summary>
    /// 수선점 강화. 단추(골드)로 수선점 레벨을 올리면 초당 단추와 수선점 내구도(전투 최대 체력)가 오른다.
    /// 초당 단추는 도달한 최고 스테이지에 비례해서도 늘어난다.
    /// </summary>
    public class GrowthSystem
    {
        /// <summary>수선점 1레벨 내구도</summary>
        public const float ShopBaseHp = 300f;
        /// <summary>레벨마다 내구도 배율 (복리)</summary>
        public const double ShopHpGrowth = 1.04;
        /// <summary>최고 스테이지 1단계마다 초당 단추 증가 비율 (단리)</summary>
        public const double StageGoldBonusPerStage = 0.1;

        /// <summary>(이전 레벨, 새 레벨)</summary>
        public event Action<int, int> LevelChanged;

        readonly PlayerData _data;
        readonly GameConfig _config;
        readonly CurrencySystem _currency;
        readonly InventorySystem _inventory;

        public GrowthSystem(PlayerData data, GameConfig config, CurrencySystem currency, InventorySystem inventory)
        {
            _data = data;
            _config = config;
            _currency = currency;
            _inventory = inventory;
        }

        public int Level => _data.level;
        public int MaxLevel => _config.maxLevel;
        public bool IsMaxLevel => Level >= MaxLevel;
        public int RemainLevels => Math.Max(0, MaxLevel - Level);

        /// <summary>fromLevel → fromLevel+1 비용</summary>
        public double GetLevelUpCost(int fromLevel) =>
            _config.levelUpBaseCost * Math.Pow(_config.levelUpCostGrowth, fromLevel - 1);

        /// <summary>fromLevel부터 count 레벨을 올리는 총 비용</summary>
        public double GetLevelUpCost(int fromLevel, int count)
        {
            double total = 0;
            for (int i = 0; i < count; i++) total += GetLevelUpCost(fromLevel + i);
            return total;
        }

        /// <summary>현재 골드로 올릴 수 있는 레벨 수 (최대 maxCount)</summary>
        public int GetAffordableCount(int maxCount)
        {
            double gold = _currency.Get(CurrencyType.Gold);
            int limit = Math.Min(maxCount, RemainLevels);
            double total = 0;
            int count = 0;
            while (count < limit)
            {
                double next = total + GetLevelUpCost(Level + count);
                if (next > gold) break;
                total = next;
                count++;
            }
            return count;
        }

        public double GoldBonusMultiplier => 1 + _inventory.TotalGoldBonusPercent / 100.0;

        /// <summary>도달한 최고 스테이지에 따른 초당 단추 배율 (1스테이지 = 1)</summary>
        public double StageGoldMultiplier => 1 + StageGoldBonusPerStage * (Math.Max(1, _data.battle?.highestStage ?? 1) - 1);

        public double GetGoldPerSecondAt(int level) =>
            _config.baseGoldPerSecond * Math.Pow(_config.goldPerSecondGrowth, level - 1) * GoldBonusMultiplier * StageGoldMultiplier;

        public double GoldPerSecond => GetGoldPerSecondAt(Level);

        /// <summary>수선점 레벨별 내구도 (전투에서 수선점 최대 체력)</summary>
        public static float CalcShopMaxHp(int level) =>
            (float)Math.Min(float.MaxValue / 4, ShopBaseHp * Math.Pow(ShopHpGrowth, Math.Max(1, level) - 1));

        public float GetShopMaxHpAt(int level) => CalcShopMaxHp(level);

        /// <summary>현재 수선점 내구도</summary>
        public float ShopMaxHp => CalcShopMaxHp(Level);

        /// <summary>count 레벨을 한 번에 올린다. 골드가 모자라면 아무것도 하지 않는다.</summary>
        public bool TryLevelUp(int count = 1)
        {
            count = Math.Min(count, RemainLevels);
            if (count <= 0) return false;
            if (!_currency.TrySpend(CurrencyType.Gold, GetLevelUpCost(Level, count))) return false;

            int oldLevel = _data.level;
            _data.level += count;
            LevelChanged?.Invoke(oldLevel, _data.level);
            return true;
        }

        /// <summary>가능한 만큼 레벨업하고 올린 레벨 수를 반환한다.</summary>
        public int LevelUpMax()
        {
            int count = GetAffordableCount(RemainLevels);
            return count > 0 && TryLevelUp(count) ? count : 0;
        }
    }
}
