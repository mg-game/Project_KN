using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class StatModifier
    {
        public StatType stat;
        [Tooltip("대부분 % (예: 15 = +15%). Flat 이름의 능력치는 고정값")]
        public float value;
    }

    /// <summary>시너지 단계. 유닛 수가 count 이상이면 이 단계가 켜진다 (높은 단계가 낮은 단계를 대체).</summary>
    [Serializable]
    public class SynergyTier
    {
        public int count = 2;
        [Tooltip("표시용 단계 설명. 예: \"천 특성 아군 체력 +15%, 수선점 초당 0.5% 회복\"")]
        public string description;
        [Tooltip("true면 시너지 구성원(특성/성향을 가진 유닛)에게만, false면 팀 전체에 적용")]
        public bool membersOnly = true;
        public List<StatModifier> modifiers = new();
        [Tooltip("이 단계에서 구성원에게 추가로 붙는 스킬 (선택). 예: 3단계에서 전투 시작 시 보호막")]
        public SkillDef bonusSkill;
    }

    /// <summary>
    /// 시너지 정의.
    /// Trait: 편성된 유닛 중 trait을 가진 형태의 수 (같은 계보의 지킴이+잔향이는 2로 센다).
    /// Style: 편성된 유닛 중 style 성향의 수.
    /// Combo: lineageIds 계보가 모두 편성됨 (지킴이든 잔향이든). 조합은 단계가 하나(tiers[0])다.
    /// </summary>
    [Serializable]
    public class SynergyDef
    {
        public string id;
        public string name;
        [TextArea] public string description;
        public SynergyKind kind;
        public TraitType trait;
        public CombatStyle style;
        public List<string> lineageIds = new();
        public List<SynergyTier> tiers = new();
        public Sprite icon;
        [Tooltip("이 스테이지 기믹에서 효과가 커짐(배율). None이면 없음")]
        public StageModifierType favoredModifier;
        public float favoredMultiplier = 1.5f;
    }

    /// <summary>특성/성향/조합 시너지 데이터. 내용은 IdleDataBuilder(콘텐츠)가 채운다.</summary>
    [CreateAssetMenu(fileName = "SynergyDatabase", menuName = "IdleGame/Synergy Database")]
    public class SynergyDatabase : ScriptableObject
    {
        public List<SynergyDef> synergies = new();

        public SynergyDef Get(string id) => string.IsNullOrEmpty(id) ? null : synergies.Find(s => s != null && s.id == id);
    }
}
