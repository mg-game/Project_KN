using System;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 별빛 터뜨리기 (도장 '꾹', 상자 열기, 개방 카드, 받기). 미리 만든 이미지 몇 개를 돌려 쓰고 Update에서 움직인다 — 할당 없음.
    /// 빌더가 자식으로 별빛 이미지를 만들어 _particles에 넣는다.
    /// </summary>
    public class MetaSparkleBurst : MonoBehaviour
    {
        [SerializeField] Image[] _particles = Array.Empty<Image>();
        [SerializeField] float _lifetime = 0.7f;
        [SerializeField] float _speed = 260f;

        struct Particle
        {
            public Vector2 Origin;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Spin;
            public float Size;
            public Color Color;
        }

        Particle[] _state = Array.Empty<Particle>();
        int _next;
        int _alive;

        void Awake()
        {
            _state = new Particle[_particles.Length];
            foreach (var particle in _particles)
            {
                if (particle != null) particle.enabled = false;
            }
        }

        /// <summary>대상 사각형 가운데에서 터뜨린다 (좌표 변환 포함)</summary>
        public void BurstAt(RectTransform target, int count, Color color, float spread = 1f)
        {
            if (target == null) return;
            var self = (RectTransform)transform;
            var world = target.TransformPoint(target.rect.center);
            Vector2 local = self.InverseTransformPoint(world);
            Burst(local - self.rect.center, count, color, spread);
        }

        /// <summary>local(이 오브젝트 가운데 기준) 위치에서 count개를 사방으로 터뜨린다</summary>
        public void Burst(Vector2 local, int count, Color color, float spread = 1f)
        {
            if (_particles.Length == 0) return;
            if (!gameObject.activeInHierarchy) return;
            count = Mathf.Min(count, _particles.Length);
            for (int i = 0; i < count; i++)
            {
                int index = _next;
                _next = (_next + 1) % _particles.Length;
                float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.35f, 0.35f);
                float speed = _speed * spread * UnityEngine.Random.Range(0.55f, 1.15f);
                var tint = color;
                tint.a = 1f;
                _state[index] = new Particle
                {
                    Origin = local,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Age = 0f,
                    Life = _lifetime * UnityEngine.Random.Range(0.75f, 1.2f),
                    Spin = UnityEngine.Random.Range(-240f, 240f),
                    Size = UnityEngine.Random.Range(0.6f, 1.15f),
                    Color = tint,
                };
                var image = _particles[index];
                image.enabled = true;
                image.color = tint;
                image.rectTransform.anchoredPosition = local;
            }
            _alive = _particles.Length;
        }

        void Update()
        {
            if (_alive == 0) return;
            float dt = Time.unscaledDeltaTime;
            int alive = 0;
            for (int i = 0; i < _particles.Length; i++)
            {
                var image = _particles[i];
                if (image == null || !image.enabled) continue;
                ref var p = ref _state[i];
                p.Age += dt;
                float k = p.Age / p.Life;
                if (k >= 1f)
                {
                    image.enabled = false;
                    continue;
                }
                alive++;
                float ease = MetaUi.EaseOutCubic(k);
                var rect = image.rectTransform;
                rect.anchoredPosition = p.Origin + p.Velocity * (ease * p.Life * 0.6f) + new Vector2(0f, -40f * k * k);
                rect.localRotation = Quaternion.Euler(0f, 0f, p.Spin * p.Age);
                rect.localScale = Vector3.one * (p.Size * (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f * 0.7f));
                var color = p.Color;
                color.a = 1f - k * k;
                image.color = color;
            }
            _alive = alive;
        }

        void OnDisable()
        {
            foreach (var particle in _particles)
            {
                if (particle != null) particle.enabled = false;
            }
            _alive = 0;
        }
    }
}
