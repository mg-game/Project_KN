using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>강조용 반복 발광 (원정 받기 버튼, 선택 표시 등). 알파와 크기를 부드럽게 오르내린다.</summary>
    public class MenuGlowPulse : MonoBehaviour
    {
        [SerializeField] Graphic _graphic;
        [SerializeField] float _minAlpha = 0.25f;
        [SerializeField] float _maxAlpha = 0.9f;
        [SerializeField] float _scaleAmount = 0.08f;
        [SerializeField] float _speed = 3f;

        Color _baseColor = Color.white;
        Vector3 _baseScale = Vector3.one;
        bool _initialized;

        void Awake() => Initialize();

        void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic != null) _baseColor = _graphic.color;
            _baseScale = transform.localScale;
        }

        /// <summary>발광 색을 바꾼다 (알파는 펄스가 덮어쓴다)</summary>
        public void SetColor(Color color)
        {
            Initialize();
            _baseColor = color;
        }

        void OnDisable()
        {
            transform.localScale = _baseScale;
        }

        void Update()
        {
            if (_graphic == null) return;
            float wave = (Mathf.Sin(Time.unscaledTime * _speed) + 1f) * 0.5f;
            var color = _baseColor;
            color.a = Mathf.Lerp(_minAlpha, _maxAlpha, wave);
            _graphic.color = color;
            transform.localScale = _baseScale * (1f + _scaleAmount * wave);
        }
    }
}
