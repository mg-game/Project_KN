using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 재화 지급 · 소비 (I-14 · SYS_Economy §2.1).
    /// - 지급: 0은 지급 없음(보상 계산이 0인 정상 흐름이라 로그 없이 무시). 음수 · NaN · ∞는 거부하고 오류 로그.
    /// - 소비: 음수 · NaN은 ArgumentOutOfRangeException (예전 그대로). 모자라면 false.
    /// - 별조각 2풀: 출처가 Purchase면 유료(PlayerData.gemPaid), 그 밖은 무료(gemFree). 소비는 무료 먼저, 모자라면 유료.
    ///   currencies[Gem]은 두 풀의 합으로 유지한다 (화면 · 비교 · 옛 코드 호환). 다른 경로가 합만 바꿨으면
    ///   다루기 전에 PlayerData.ReconcileGemPools로 풀을 합에 맞춘다.
    /// - 합이 double 범위를 넘으면 double.MaxValue에서 멈춘다 (∞를 저장하지 않는다).
    /// </summary>
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

        /// <summary>별조각 유료 풀 (결제로 받은 몫)</summary>
        public double GemPaid
        {
            get
            {
                _data.ReconcileGemPools();
                return _data.gemPaid;
            }
        }

        /// <summary>별조각 무료 풀 (결제가 아닌 지급으로 받은 몫)</summary>
        public double GemFree
        {
            get
            {
                _data.ReconcileGemPools();
                return _data.gemFree;
            }
        }

        /// <summary>지급할 수 있는 양: 0보다 크고 유한 (NaN은 비교가 false라 걸린다)</summary>
        public static bool IsValidGrant(double amount) => amount > 0 && !double.IsInfinity(amount);

        /// <summary>결제가 아닌 지급 (보상 · 우편 · 시작 지급)</summary>
        public void Add(CurrencyType type, double amount) => Add(type, amount, CurrencySource.Game);

        public void Add(CurrencyType type, double amount, CurrencySource source)
        {
            if (amount == 0) return;
            if (!IsValidGrant(amount))
            {
                Debug.LogError($"[Currency] 잘못된 지급을 거부했어요: {type} {amount} ({source})");
                return;
            }
            if (type == CurrencyType.Gem)
            {
                _data.ReconcileGemPools();
                if (source == CurrencySource.Purchase) _data.gemPaid = Bounded(_data.gemPaid + amount);
                else _data.gemFree = Bounded(_data.gemFree + amount);
                Set(type, Bounded(_data.gemPaid + _data.gemFree));
                return;
            }
            Set(type, Bounded(Get(type) + amount));
        }

        public bool TrySpend(CurrencyType type, double amount)
        {
            if (amount < 0 || double.IsNaN(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!Has(type, amount)) return false;
            if (type == CurrencyType.Gem)
            {
                _data.ReconcileGemPools();
                double fromFree = Math.Min(_data.gemFree, amount);
                _data.gemFree -= fromFree;
                _data.gemPaid = Math.Max(0, _data.gemPaid - (amount - fromFree));
                Set(type, _data.gemPaid + _data.gemFree);
                return true;
            }
            Set(type, Get(type) - amount);
            return true;
        }

        static double Bounded(double value) => double.IsPositiveInfinity(value) ? double.MaxValue : value;

        void Set(CurrencyType type, double value)
        {
            _data.currencies[type] = value;
            Changed?.Invoke(type, value);
        }
    }
}
