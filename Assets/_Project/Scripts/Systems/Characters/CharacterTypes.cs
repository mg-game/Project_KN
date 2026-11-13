using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>레벨·공명·의상 보너스를 반영한 형태의 최종 능력치</summary>
    public struct CharacterStats
    {
        public CombatStyle style;
        public float attack;
        public float hp;
        public float attackInterval;
        /// <summary>지킴이: 수선점 중심 기준 요격 반경 / 잔향이: 공격 사거리</summary>
        public float range;
        /// <summary>지킴이 부채꼴 각도(도)</summary>
        public float arcDegrees;
        public float moveSpeed;
        public int summonCost;
        public float lifetime;

        /// <summary>목록 정렬/자동 편성용 전투력</summary>
        public float Power => attack / UnityEngine.Mathf.Max(0.1f, attackInterval) * 10f + hp;
    }

    public sealed class GrantResult
    {
        public LineageDef Lineage;
        public bool IsNew;
        /// <summary>중복이라 기억편으로 변환된 양 (신규면 0)</summary>
        public int ShardsGained;
        /// <summary>
        /// 새 친구가 시작하는 레벨 (PRG-05 따라잡기: 친구들 평균의 80%). 1 = 따라잡기 없음 (중복도 1). 1보다 크면 소환 결과가
        /// '친구들 평균의 80%인 Lv.{n}부터 함께해요'를 보여 준다. P4-1 A-1 계약 — CharacterSystem이 채우기 전에는 늘 1
        /// </summary>
        public int StartLevel = 1;
    }

    public sealed class MaterialStatus
    {
        public string ItemId;
        public int Have;
        public int Need;
        public bool Enough => Have >= Need;
    }

    public sealed class ConditionStatus
    {
        public EvolutionCondition Condition;
        public int Progress;
        public bool Done => Progress >= Condition.target;
        /// <summary>예: "원정 10회"</summary>
        public string Label;
    }

    /// <summary>진화 레시피 / 조건 확인 결과</summary>
    public sealed class EvolutionCheck
    {
        public CharacterFormDef Form;
        public LineageDef Lineage;
        public EvolutionState State;
        public List<MaterialStatus> Materials = new();
        public List<ConditionStatus> Conditions = new();
        public bool CanCraft;
    }

    public enum ShopBuyResult
    {
        Ok = 0,
        NotFound = 1,
        NotEnoughCurrency = 2,
        SoldOut = 3,
        AlreadyOwned = 4,
    }
}
