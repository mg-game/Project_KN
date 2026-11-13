using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>숨 쉬듯 은은하게 밝아졌다 어두워지는 빛 (글로우, 강조 테두리). 색(RGB)은 그대로 두고 알파만 바꾼다.</summary>
    public class SoftPulse : MonoBehaviour
    {
        [SerializeField] float _minAlpha = 0.25f;
        [SerializeField] float _maxAlpha = 0.85f;
        [SerializeField] float _period = 1.8f;
        [SerializeField] float _scaleAmount = 0.05f;

        Graphic _graphic;
        Vector3 _baseScale = Vector3.one;
        float _offset;

        void Awake()
        {
            _graphic = GetComponent<Graphic>();
            _baseScale = transform.localScale;
            _offset = Random.value * _period;
        }

        void OnDisable() => transform.localScale = _baseScale;

        /// <summary>빛 색을 바꾼다 (알파는 계속 맥동)</summary>
        public void SetColor(Color color)
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic == null) return;
            color.a = _graphic.color.a;
            _graphic.color = color;
        }

        void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime + _offset) / Mathf.Max(0.1f, _period) * Mathf.PI * 2f);
            if (_graphic != null)
            {
                var color = _graphic.color;
                color.a = Mathf.Lerp(_minAlpha, _maxAlpha, k);
                _graphic.color = color;
            }
            if (_scaleAmount != 0f) transform.localScale = _baseScale * (1f + _scaleAmount * k);
        }
    }
}
