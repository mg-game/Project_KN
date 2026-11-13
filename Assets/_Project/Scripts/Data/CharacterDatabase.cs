using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class MaterialCost
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class EvolutionCondition
    {
        public ProgressType type;
        public int target;
    }

    /// <summary>
    /// 계보 안의 한 형태 (기본형 또는 진화형). 레벨·호감도는 계보가 공유하고,
    /// 형태는 전투 성향과 기본 능력치만 나눈다.
    /// </summary>
    [Serializable]
    public class CharacterFormDef
    {
        public string id;
        public string name;
        [Tooltip("기본형은 비워 둔다")]
        public string parentFormId;
        [Tooltip("0 = 기본형, 1 = 1차 진화, 2 = 2차 진화")]
        public int tier;
        public CombatStyle style;
        [Tooltip("추천 배치 역할 (태그 표시용, 배치는 어느 쪽이든 가능)")]
        public CharacterRole recommendedRole;
        [TextArea] public string description;
        public Sprite portrait;

        [Header("고유 스킬")]
        public SkillDef skill = new();

        [Header("기본 능력치 (레벨 1)")]
        public float attack = 10f;
        public float hp = 100f;
        [Tooltip("공격 간격(초)")]
        public float attackInterval = 1f;
        [Tooltip("지킴이: 수선점 중심에서의 요격 반경 / 잔향이: 공격 사거리 (월드 단위, 방위 링 반지름 = 1)")]
        public float range = 1.6f;
        [Tooltip("지킴이 부채꼴 요격 각도(도)")]
        public float arcDegrees = 72f;
        [Tooltip("잔향이 이동 속도 (월드 단위/초)")]
        public float moveSpeed = 0.8f;
        [Tooltip("잔향이 소환 비용 (잔향)")]
        public int summonCost = 5;
        [Tooltip("잔향이 유지 시간(초)")]
        public float lifetime = 12f;

        [Header("진화 레시피 (기본형은 비워 둔다)")]
        [Tooltip("단서 발견에 필요한 계보 레벨 (부모 형태 해금 + 이 레벨 도달 시 단서 발견)")]
        public int clueLevel = 5;
        public List<MaterialCost> materials = new();
        public List<EvolutionCondition> conditions = new();
    }

    [Serializable]
    public class CostumeDef
    {
        public string id;
        public string name;
        [Tooltip("감정 변화. 예: \"외로움 → 위로\"")]
        public string emotion;
        [TextArea] public string description;
        [Tooltip("획득 방법 안내 문구")]
        public string acquisition;
        public CostumeUnlockType unlockType;
        public int unlockTarget;
        [Tooltip("FormUnlocked일 때 형태 ID")]
        public string unlockTargetId;
        [Tooltip("EventShop일 때 별빛 사탕 가격")]
        public int price;
        [Tooltip("보유 시 이 계보 공격력 증가(%)")]
        public float attackBonusPercent = 3f;
        public Sprite portrait;
        [Tooltip("컨셉 시트 일러스트 (도감 보기)")]
        public Sprite illustration;
    }

    [Serializable]
    public class LineageDef
    {
        public string id;
        public string name;
        [Tooltip("예: 양말족")]
        public string species;
        [Tooltip("원래 사물. 예: 짝을 잃은 양말")]
        public string origin;
        [TextArea] public string personality;
        public ItemGrade grade;
        public BodyType bodyType;
        [Tooltip("특성 (재질/성질). 특성 시너지에 쓰인다")]
        public List<TraitType> traits = new();

        [Header("컬러 (메인 1 / 서브 1 / 포인트 1 / 중립 1)")]
        public Color mainColor = Color.white;
        public Color subColor = Color.white;
        public Color accentColor = Color.white;
        public Color neutralColor = Color.white;

        public string baseFormId;
        public List<CharacterFormDef> forms = new();
        public List<CostumeDef> costumes = new();

        public int Stars => (int)grade + 1;
    }

    /// <summary>캐릭터 계보 / 진화 형태 / 외형(의상) 데이터</summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "IdleGame/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        public List<LineageDef> lineages = new();

        Dictionary<string, LineageDef> _lineages;
        Dictionary<string, CharacterFormDef> _forms;
        Dictionary<string, LineageDef> _formOwner;
        Dictionary<string, CostumeDef> _costumes;
        Dictionary<string, LineageDef> _costumeOwner;
        Dictionary<ItemGrade, List<LineageDef>> _byGrade;

        public LineageDef GetLineage(string id)
        {
            EnsureCache();
            return id != null && _lineages.TryGetValue(id, out var def) ? def : null;
        }

        public CharacterFormDef GetForm(string formId)
        {
            EnsureCache();
            return formId != null && _forms.TryGetValue(formId, out var def) ? def : null;
        }

        /// <summary>형태가 속한 계보</summary>
        public LineageDef GetLineageOfForm(string formId)
        {
            EnsureCache();
            return formId != null && _formOwner.TryGetValue(formId, out var def) ? def : null;
        }

        public CostumeDef GetCostume(string costumeId)
        {
            EnsureCache();
            return costumeId != null && _costumes.TryGetValue(costumeId, out var def) ? def : null;
        }

        public LineageDef GetLineageOfCostume(string costumeId)
        {
            EnsureCache();
            return costumeId != null && _costumeOwner.TryGetValue(costumeId, out var def) ? def : null;
        }

        public IReadOnlyList<LineageDef> GetByGrade(ItemGrade grade)
        {
            EnsureCache();
            return _byGrade.TryGetValue(grade, out var list) ? list : Array.Empty<LineageDef>();
        }

        /// <summary>형태의 직계 자식 진화형들</summary>
        public List<CharacterFormDef> GetChildren(string formId)
        {
            var result = new List<CharacterFormDef>();
            var lineage = GetLineageOfForm(formId);
            if (lineage == null) return result;
            foreach (var form in lineage.forms)
            {
                if (form.parentFormId == formId) result.Add(form);
            }
            return result;
        }

        void OnValidate() => _lineages = null;

        void EnsureCache()
        {
            if (_lineages != null) return;
            _lineages = new Dictionary<string, LineageDef>();
            _forms = new Dictionary<string, CharacterFormDef>();
            _formOwner = new Dictionary<string, LineageDef>();
            _costumes = new Dictionary<string, CostumeDef>();
            _costumeOwner = new Dictionary<string, LineageDef>();
            _byGrade = new Dictionary<ItemGrade, List<LineageDef>>();
            foreach (var lineage in lineages)
            {
                if (lineage == null || string.IsNullOrEmpty(lineage.id)) continue;
                if (!_lineages.TryAdd(lineage.id, lineage))
                {
                    Debug.LogWarning($"[CharacterDatabase] 중복 계보 ID: {lineage.id}");
                    continue;
                }
                if (!_byGrade.TryGetValue(lineage.grade, out var list)) _byGrade[lineage.grade] = list = new List<LineageDef>();
                list.Add(lineage);
                foreach (var form in lineage.forms)
                {
                    if (form == null || string.IsNullOrEmpty(form.id)) continue;
                    if (!_forms.TryAdd(form.id, form)) Debug.LogWarning($"[CharacterDatabase] 중복 형태 ID: {form.id}");
                    else _formOwner[form.id] = lineage;
                }
                foreach (var costume in lineage.costumes)
                {
                    if (costume == null || string.IsNullOrEmpty(costume.id)) continue;
                    if (!_costumes.TryAdd(costume.id, costume)) Debug.LogWarning($"[CharacterDatabase] 중복 의상 ID: {costume.id}");
                    else _costumeOwner[costume.id] = lineage;
                }
            }
        }
    }
}
