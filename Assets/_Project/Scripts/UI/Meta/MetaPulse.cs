using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>숨 쉬듯 커졌다 작아지고 알파가 오가는 빛 (오늘 칸, 받을 수 있는 상자)</summary>
    public class MetaPulse : MonoBehaviour
    {
        [SerializeField] float _speed = 3f;
        [SerializeField] float _scale = 0.06f;
        [SerializeField] Graphic _graphic;
        [SerializeField] float _minAlpha = 0.35f;
        [SerializeField] float _maxAlpha = 0.9f;

        float _time;
        Vector3 _baseScale = Vector3.one;

        void Awake() => _baseScale = transform.localScale;

        void OnEnable() => _time = 0f;

        void OnDisable() => transform.localScale = _baseScale;

        void Update()
        {
            _time += Time.unscaledDeltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(_time * _speed);
            transform.localScale = _baseScale * (1f + _scale * wave);
            if (_graphic != null)
            {
                var color = _graphic.color;
                color.a = Mathf.Lerp(_minAlpha, _maxAlpha, wave);
                _graphic.color = color;
            }
        }
    }
}
