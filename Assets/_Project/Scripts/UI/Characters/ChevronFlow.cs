using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>화살표(›››)가 앞에서부터 차례로 밝아지는 흐름 연출 (진화 Before → After)</summary>
    public class ChevronFlow : MonoBehaviour
    {
        [SerializeField] Graphic[] _items;
        [SerializeField] float _period = 1.2f;
        [SerializeField] float _minAlpha = 0.18f;

        void Update()
        {
            if (_items == null) return;
            float t = Time.unscaledTime / Mathf.Max(0.1f, _period);
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] == null) continue;
                float phase = Mathf.Repeat(t - i * 0.2f, 1f);
                float k = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.3f) / 0.3f);
                var color = _items[i].color;
                color.a = Mathf.Lerp(_minAlpha, 1f, k);
                _items[i].color = color;
            }
        }
    }
}
