using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    public class GachaResult
    {
        public LineageDef Lineage { get; }
        public bool IsNew { get; }
        /// <summary>천장으로 확정된 결과인지</summary>
        public bool IsPity { get; }
        /// <summary>중복이라 기억편으로 변환된 양 (신규면 0)</summary>
        public int ShardsGained { get; }
        /// <summary>확정 소환(첫 만남, PullGuaranteed)인지 — 소환 마일스톤 누적에 세지 않는다 (PRG-04)</summary>
        public bool IsGuaranteed { get; }
        /// <summary>새 친구가 시작한 레벨 (PRG-05 따라잡기 = GrantResult.StartLevel, 중복 · 따라잡기 없음 = 1)</summary>
        public int StartLevel { get; }

        public GachaResult(LineageDef lineage, bool isNew, bool isPity, int shardsGained, bool isGuaranteed = false, int startLevel = 1)
        {
            Lineage = lineage;
            IsNew = isNew;
            IsPity = isPity;
            ShardsGained = shardsGained;
            IsGuaranteed = isGuaranteed;
            StartLevel = Math.Max(1, startLevel);
        }
    }

    public readonly struct GachaRate
    {
        public readonly ItemGrade Grade;
        public readonly double Percent;

        public GachaRate(ItemGrade grade, double percent)
        {
            Grade = grade;
            Percent = percent;
        }
    }

    /// <summary>
    /// 캐릭터 계보 소환. 등급을 가중치로 먼저 뽑고, 해당 등급 계보 중 하나를 균등하게 뽑는다.
    /// 새 계보는 획득, 중복은 계보 전용 기억편으로 변환된다.
    /// </summary>
    public class GachaSystem
    {
        /// <summary>(테이블, 결과) 소환을 마쳤을 때 — 유료 소환과 첫 만남 확정 소환 모두. 진행 기록기(GachaPull)용</summary>
        public event Action<GachaTable, IReadOnlyList<GachaResult>> Pulled;

        readonly PlayerData _data;
        readonly CurrencySystem _currency;
        readonly CharacterSystem _characters;
        readonly CharacterDatabase _db;
        readonly System.Random _random;
        readonly Dictionary<string, GachaTable> _tables = new();

        public GachaSystem(PlayerData data, CurrencySystem currency, CharacterSystem characters, CharacterDatabase db,
            IEnumerable<GachaTable> tables, System.Random random)
        {
            _data = data;
            _currency = currency;
            _characters = characters;
            _db = db;
            _random = random;
            if (tables == null) return;
            foreach (var table in tables)
            {
                if (table != null) _tables[table.tableId] = table;
            }
        }

        public GachaTable GetTable(string tableId) =>
            tableId != null && _tables.TryGetValue(tableId, out var table) ? table : null;

        public int GetPityCount(GachaTable table) =>
            table != null && table.tableId != null && _data.gachaPity.TryGetValue(table.tableId, out var count) ? count : 0;

        /// <summary>천장까지 남은 횟수. 천장이 없거나 천장 등급 계보가 없으면 -1</summary>
        public int GetRemainToPity(GachaTable table) =>
            IsPityActive(table) ? Mathf.Max(1, table.pityCount - GetPityCount(table)) : -1;

        /// <summary>count회 소환 비용. 10회는 할인가</summary>
        public double GetCost(GachaTable table, int count)
        {
            if (table == null || count <= 0) return 0;
            return count == 10 ? table.tenCost : table.singleCost * count;
        }

        public bool CanAfford(GachaTable table, int count) =>
            table != null && count > 0 && _currency.Has(table.costCurrency, GetCost(table, count));

        /// <summary>등급별 표시 확률(%). 계보가 없는 등급은 제외된다. (확률 공개용)</summary>
        public List<GachaRate> GetRates(GachaTable table)
        {
            var rates = new List<GachaRate>();
            if (table == null) return rates;
            double total = GetTotalWeight(table);
            if (total <= 0) return rates;
            foreach (var rate in table.rates)
            {
                if (IsRollable(rate)) rates.Add(new GachaRate(rate.grade, rate.weight / total * 100.0));
            }
            return rates;
        }

        public bool TryPull(GachaTable table, int count, out List<GachaResult> results)
        {
            results = null;
            if (table == null || count <= 0) return false;
            if (GetTotalWeight(table) <= 0)
            {
                Debug.LogError($"[Gacha] '{table.tableId}' 테이블에 뽑을 수 있는 계보가 없습니다.");
                return false;
            }
            if (!_currency.TrySpend(table.costCurrency, GetCost(table, count))) return false;

            results = new List<GachaResult>(count);
            int pity = GetPityCount(table);
            for (int i = 0; i < count; i++)
            {
                var grade = RollGrade(table, ref pity, out bool isPity);
                var candidates = _db.GetByGrade(grade);
                var lineage = candidates[_random.Next(candidates.Count)];
                var grant = _characters.Grant(lineage.id);
                results.Add(new GachaResult(lineage, grant.IsNew, isPity, grant.ShardsGained, startLevel: grant.StartLevel));
            }

            _data.gachaPity[table.tableId] = pity;
            Pulled?.Invoke(table, results);
            return true;
        }

        /// <summary>
        /// 첫 만남 확정 소환 (FTUE 10단계): 재화를 쓰지 않고 lineageId를 확정 지급한다. 천장 카운트와 확률에는 넣지 않는다.
        /// 한 번만 쓰도록 하는 것은 부르는 쪽(MetaFlags.FirstMeetingSummoned) 책임. 없는 계보면 null.
        /// </summary>
        public GachaResult PullGuaranteed(GachaTable table, string lineageId)
        {
            var grant = _characters.Grant(lineageId);
            if (grant == null) return null;
            var result = new GachaResult(grant.Lineage, grant.IsNew, false, grant.ShardsGained, isGuaranteed: true, startLevel: grant.StartLevel);
            Pulled?.Invoke(table, new[] { result });
            return result;
        }

        /// <summary>천장 등급이 정해져 있고 그 등급 계보가 있어야 천장이 동작한다.</summary>
        bool IsPityActive(GachaTable table) =>
            table != null && table.pityCount > 0 && _db.GetByGrade(table.pityGrade).Count > 0;

        ItemGrade RollGrade(GachaTable table, ref int pity, out bool isPity)
        {
            isPity = false;
            pity++;
            var grade = RollByWeight(table);

            bool usePity = IsPityActive(table);
            if (usePity && pity >= table.pityCount && grade < table.pityGrade)
            {
                grade = table.pityGrade;
                isPity = true;
            }
            if (usePity && grade >= table.pityGrade) pity = 0;
            return grade;
        }

        ItemGrade RollByWeight(GachaTable table)
        {
            double roll = _random.NextDouble() * GetTotalWeight(table);
            ItemGrade last = default;
            foreach (var rate in table.rates)
            {
                if (!IsRollable(rate)) continue;
                last = rate.grade;
                roll -= rate.weight;
                if (roll < 0) return rate.grade;
            }
            return last;
        }

        double GetTotalWeight(GachaTable table)
        {
            double total = 0;
            foreach (var rate in table.rates)
            {
                if (IsRollable(rate)) total += rate.weight;
            }
            return total;
        }

        bool IsRollable(GachaGradeRate rate) => rate.weight > 0 && _db.GetByGrade(rate.grade).Count > 0;
    }
}
