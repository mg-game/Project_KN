using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>리스트 항목 재사용 풀. 필요한 개수만 활성화하고 남는 건 꺼둔다.</summary>
    public class UIListPool<T> where T : Component
    {
        readonly T _prefab;
        readonly Transform _parent;
        readonly List<T> _items = new();

        public UIListPool(T prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public int Count { get; private set; }

        public T this[int index] => _items[index];

        public void SetCount(int count)
        {
            while (_items.Count < count) _items.Add(Object.Instantiate(_prefab, _parent));
            for (int i = 0; i < _items.Count; i++) _items[i].gameObject.SetActive(i < count);
            Count = count;
        }
    }
}
