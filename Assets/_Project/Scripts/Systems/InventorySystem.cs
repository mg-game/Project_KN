using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    public readonly struct OwnedItem
    {
        public readonly ItemDef Def;
        public readonly int Count;

        public OwnedItem(ItemDef def, int count)
        {
            Def = def;
            Count = count;
        }
    }

    public class InventorySystem
    {
        public event Action Changed;
        /// <summary>
        /// (아이템 ID, 더한 개수) 아이템을 얻었을 때 — Add · AddRange마다, 개수가 0보다 클 때만 (4차 훅: 도감 재료 찾기, SPEC4 §2.5).
        /// Changed보다 먼저 울린다. 받는 쪽은 할당 없이 가볍게
        /// </summary>
        public event Action<string, int> ItemAdded;

        readonly PlayerData _data;
        readonly ItemDatabase _items;
        double _goldBonusCache = -1;
        /// <summary>상한 경고를 이미 남긴 아이템 (같은 아이템으로 로그가 쌓이지 않게 세션당 한 번)</summary>
        readonly HashSet<string> _capWarned = new();

        public InventorySystem(PlayerData data, ItemDatabase items)
        {
            _data = data;
            _items = items;
            ClampToCap();
        }

        /// <summary>아이템 데이터 (도감 재료 칸 목록 · 힌트용, 없으면 null)</summary>
        public ItemDatabase Db => _items;

        /// <summary>
        /// 한 종류의 보유 상한 (I-14 · SYS_Economy §2.1: int 넘침 방지 — 상한에서 멈춘다).
        /// ItemDatabase.countCap, 0 이하이거나 데이터가 없으면 ItemDatabase.DefaultCountCap
        /// </summary>
        public int CountCap => _items != null && _items.countCap > 0 ? _items.countCap : ItemDatabase.DefaultCountCap;

        public int GetCount(string itemId) => itemId != null && _data.items.TryGetValue(itemId, out var count) ? count : 0;

        public void Add(string itemId, int count = 1)
        {
            if (AddInternal(itemId, count)) NotifyChanged();
        }

        /// <summary>여러 개를 한 번에 추가하고 Changed는 한 번만 알린다.</summary>
        public void AddRange(IEnumerable<string> itemIds)
        {
            bool changed = false;
            foreach (var id in itemIds) changed |= AddInternal(id, 1);
            if (changed) NotifyChanged();
        }

        /// <summary>아이템을 count개 소모한다. 모자라거나 count가 0 이하면 아무것도 하지 않고 false.</summary>
        public bool TryConsume(string itemId, int count)
        {
            int have = GetCount(itemId);
            if (count <= 0 || have < count) return false;
            if (have == count) _data.items.Remove(itemId);
            else _data.items[itemId] = have - count;
            NotifyChanged();
            return true;
        }

        /// <summary>보유 아이템 목록 (높은 등급 우선)</summary>
        public List<OwnedItem> GetOwnedItems()
        {
            var result = new List<OwnedItem>();
            foreach (var pair in _data.items)
            {
                var def = pair.Value > 0 ? _items.Get(pair.Key) : null;
                if (def != null) result.Add(new OwnedItem(def, pair.Value));
            }
            result.Sort((a, b) => a.Def.grade != b.Def.grade
                ? b.Def.grade.CompareTo(a.Def.grade)
                : string.CompareOrdinal(a.Def.id, b.Def.id));
            return result;
        }

        /// <summary>보유한 아이템 종류별 골드 보너스 합계(%)</summary>
        public double TotalGoldBonusPercent
        {
            get
            {
                if (_goldBonusCache < 0)
                {
                    double sum = 0;
                    foreach (var pair in _data.items)
                    {
                        var def = pair.Value > 0 ? _items.Get(pair.Key) : null;
                        if (def != null) sum += def.goldBonusPercent;
                    }
                    _goldBonusCache = sum;
                }
                return _goldBonusCache;
            }
        }

        /// <summary>
        /// 지급 (I-14): 0은 지급 없음(로그 없이 무시), 음수는 거부 + 오류 로그. 상한을 넘는 몫은 버리고 상한에서 멈춘다 (경고는 아이템마다 한 번).
        /// ItemAdded는 실제로 늘어난 개수로 울린다 (상한에 걸려 하나도 못 넣으면 울리지 않는다)
        /// </summary>
        bool AddInternal(string itemId, int count)
        {
            if (count == 0) return false;
            if (count < 0)
            {
                Debug.LogError($"[Inventory] 잘못된 지급을 거부했어요: {itemId} {count}");
                return false;
            }
            if (_items.Get(itemId) == null)
            {
                Debug.LogWarning($"[Inventory] 존재하지 않는 아이템: {itemId}");
                return false;
            }
            int have = GetCount(itemId);
            int cap = CountCap;
            long next = (long)have + count;
            if (next > cap)
            {
                next = Math.Max(have, cap);
                if (_capWarned.Add(itemId)) Debug.LogWarning($"[Inventory] 상한 {cap}에서 멈췄어요: {itemId} (+{count})");
            }
            int added = (int)(next - have);
            if (added <= 0) return false;
            _data.items[itemId] = (int)next;
            ItemAdded?.Invoke(itemId, added);
            return true;
        }

        /// <summary>세이브에 상한보다 많이 들어 있으면 상한으로 맞춘다 (로드 무결성 — 만들 때 한 번)</summary>
        void ClampToCap()
        {
            if (_data?.items == null) return;
            int cap = CountCap;
            List<string> over = null;
            foreach (var pair in _data.items)
            {
                if (pair.Value > cap) (over ??= new List<string>()).Add(pair.Key);
            }
            if (over == null) return;
            foreach (var key in over)
            {
                Debug.LogWarning($"[Save] 무결성: 재료 {key} 개수 {_data.items[key]} → 상한 {cap}");
                _data.items[key] = cap;
            }
        }

        void NotifyChanged()
        {
            _goldBonusCache = -1;
            Changed?.Invoke();
        }
    }
}
