using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class ItemSource
    {
        public ItemSourceType type;
        [Tooltip("원정 구역 ID / 던전 최소 층(문자열 숫자) / 상점 ID")]
        public string targetId;
        [Tooltip("표시용. 예: \"원정 2-4\", \"잔향이 던전 3층\", \"이벤트 상점\"")]
        public string label;
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public ItemGrade grade;
        public ItemCategory category;
        [TextArea] public string description;
        public Sprite icon;
        [Tooltip("보유 시 골드 획득량 증가(%). 같은 아이템 중복 보유는 한 번만 적용")]
        public float goldBonusPercent;
        [Tooltip("추천 콘텐츠 / 획득처")]
        public List<ItemSource> sources = new();
    }

    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "IdleGame/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        /// <summary>재료 한 종류의 보유 상한 기본값 (I-14 — 임시, 값은 BAL에서 확정)</summary>
        public const int DefaultCountCap = 999_999;

        public List<ItemDef> items = new();
        [Tooltip("재료 한 종류의 보유 상한 (I-14 · SYS_Economy §2.1 — 임시 999,999, 값은 BAL에서 확정). 0 이하 = 기본값")]
        public int countCap = DefaultCountCap;

        Dictionary<string, ItemDef> _byId;
        Dictionary<ItemGrade, List<ItemDef>> _byGrade;

        public ItemDef Get(string id)
        {
            EnsureCache();
            return id != null && _byId.TryGetValue(id, out var def) ? def : null;
        }

        public IReadOnlyList<ItemDef> GetByGrade(ItemGrade grade)
        {
            EnsureCache();
            return _byGrade.TryGetValue(grade, out var list) ? list : Array.Empty<ItemDef>();
        }

        void OnValidate()
        {
            _byId = null;
            _byGrade = null;
        }

        void EnsureCache()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, ItemDef>();
            _byGrade = new Dictionary<ItemGrade, List<ItemDef>>();
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                if (_byId.ContainsKey(item.id))
                {
                    Debug.LogWarning($"[ItemDatabase] 중복 아이템 ID: {item.id}");
                    continue;
                }
                _byId.Add(item.id, item);
                if (!_byGrade.TryGetValue(item.grade, out var list)) _byGrade[item.grade] = list = new List<ItemDef>();
                list.Add(item);
            }
        }
    }
}
