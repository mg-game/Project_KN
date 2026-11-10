using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 반짝이 연출. Play()는 가운데에서 사방으로 터지는 별빛, 앰비언트 모드는 영역 안에서 은은하게 반짝인다.
    /// 반짝이 이미지는 비활성 템플릿을 복제해서 재사용한다 (RectTransform 피벗은 가운데).
    /// </summary>
    public class SparkleBurst : MonoBehaviour
    {
        [SerializeField] Image _template;
        [SerializeField] float _radius = 260f;
        [SerializeField] float _ambientInterval = 0.2f;

        readonly List<Image> _pool = new();
        bool _ambient;
        Color _ambientColor = Color.white;
        float _ambientTimer;

        void Awake()
        {
            if (_template != null) _template.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            foreach (var spark in _pool) spark.gameObject.SetActive(false);
        }

        /// <summary>영역 안에서 계속 반짝이게 한다</summary>
        public void SetAmbient(bool on, Color color)
        {
            _ambient = on;
            _ambientColor = color;
            _ambientTimer = 0f;
        }

        /// <summary>가운데에서 사방으로 별빛을 터뜨린다</summary>
        public void Play(Color color, int count = 14, float duration = 0.9f)
        {
            if (!isActiveAndEnabled || _template == null) return;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = _radius * Random.Range(0.5f, 1f);
                float size = Random.Range(0.55f, 1.25f);
                float time = duration * Random.Range(0.7f, 1.1f);
                StartCoroutine(Fly(Get(), color, direction * distance, size, time, Random.Range(0f, 0.08f)));
            }
        }

        void Update()
        {
            if (!_ambient || _template == null) return;
            _ambientTimer -= Time.unscaledDeltaTime;
            if (_ambientTimer > 0f) return;
            _ambientTimer = _ambientInterval * Random.Range(0.6f, 1.4f);

            var rect = ((RectTransform)transform).rect;
            var position = new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax));
            StartCoroutine(Twinkle(Get(), _ambientColor, position, Random.Range(0.35f, 0.85f), Random.Range(0.8f, 1.5f)));
        }

        Image Get()
        {
            foreach (var spark in _pool)
            {
                if (!spark.gameObject.activeSelf) return spark;
            }
            var created = Instantiate(_template, transform);
            _pool.Add(created);
            return created;
        }

        IEnumerator Fly(Image spark, Color color, Vector2 target, float size, float duration, float delay)
        {
            var rt = spark.rectTransform;
            spark.gameObject.SetActive(true);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;
            spark.color = CharacterUI.WithAlpha(color, 0f);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            float spin = Random.Range(-160f, 160f);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rt.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, target, CharacterMotion.EaseOutCubic(k));
                float scale = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f * 0.7f;
                rt.localScale = Vector3.one * (size * scale);
                rt.localRotation = Quaternion.Euler(0f, 0f, spin * k);
                spark.color = CharacterUI.WithAlpha(color, color.a * (k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f));
                yield return null;
            }
            spark.gameObject.SetActive(false);
        }

        IEnumerator Twinkle(Image spark, Color color, Vector2 position, float size, float duration)
        {
            var rt = spark.rectTransform;
            spark.gameObject.SetActive(true);
            rt.anchoredPosition = position;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / duration * Mathf.PI);
                rt.localScale = Vector3.one * (size * k);
                spark.color = CharacterUI.WithAlpha(color, color.a * k);
                yield return null;
            }
            spark.gameObject.SetActive(false);
        }
    }
}
