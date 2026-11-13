using System;

namespace IdleGame
{
    public class CurrencySystem
    {
        /// <summary>(재화 종류, 변경 후 값)</summary>
        public event Action<CurrencyType, double> Changed;

        readonly PlayerData _data;

        public CurrencySystem(PlayerData data)
        {
            _data = data;
        }

        public double Get(CurrencyType type) => _data.currencies.TryGetValue(type, out var value) ? value : 0;

        public bool Has(CurrencyType type, double amount) => Get(type) >= amount;

        public void Add(CurrencyType type, double amount)
        {
            if (amount <= 0 || double.IsNaN(amount)) return;
            Set(type, Get(type) + amount);
        }

        public bool TrySpend(CurrencyType type, double amount)
        {
            if (amount < 0 || double.IsNaN(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!Has(type, amount)) return false;
            Set(type, Get(type) - amount);
            return true;
        }

        void Set(CurrencyType type, double value)
        {
            _data.currencies[type] = value;
            Changed?.Invoke(type, value);
        }
    }
}
