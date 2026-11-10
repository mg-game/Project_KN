using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 축하 연출 (FX-04 개방 카드·졸업 카드·첫 만남·진화): 창 뒤로 천천히 도는 빛살 + 가운데에서 터지는 별빛 + 흩날리는 색종이.
    ///   · 켜질 때(OnEnable) 자동 재생할 수 있어서, 다른 화면이 만든 팝업 루트에 런타임에 붙여도 된다 (Attach).
    ///     팝업이 열리면 오브젝트가 켜지므로 OnEnable = 카드가 열린 순간이다.
    ///   · 조각은 처음 재생할 때 한 번만 만들고 다시 쓴다. Update는 할당 없음. 모두 raycastTarget 끔.
    ///   · 빛살은 "Window" 바로 뒤(형제 순서)에 두어 창을 가리지 않고, 별빛·색종이는 맨 앞에 둔다.
    ///   · 색종이는 흰 아트 여러 장(리본·단추·별·실) 중 하나를 조각마다 골라 팔레트 색으로 물들이고, 비율을 지켜 그린다.
    ///   · 움직임 줄이기 (M64, UX-SPEC §1.9 — 설정 '번쩍임 줄이기', UIMotion.Reduced): 재생할 때 한 번 읽는다.
    ///     빛살은 알파 절반(0.32 → 0.16)에 돌지도 숨쉬지도 않고, 별빛은 14개 중 6개, 색종이는 20개 중 8개만 켠다.
    ///     조각 수(풀)는 그대로 두어 실행 중에 설정을 바꿔도 다시 만들지 않는다.
    /// </summary>
    public class CelebrationFx : MonoBehaviour
    {
        /// <summary>움직임 줄이기: 켜는 별빛 · 색종이 수, 빛살 알파 배율</summary>
        const int ReducedSparks = 6;
        const int ReducedConfetti = 8;
        const float ReducedRaysAlpha = 0.5f;

        [SerializeField] Sprite _raysSprite;
        [SerializeField] Sprite _sparkSprite;
        [Tooltip("색종이 그림 (흰색, 조각마다 하나를 골라 팔레트 색으로 물들인다)")]
        [SerializeField] Sprite[] _confettiSprites;
        [Tooltip("연출 중심 (비우면 자식 \"Window\", 그것도 없으면 이 오브젝트)")]
        [SerializeField] RectTransform _anchor;
        [SerializeField] bool _playOnEnable = true;
        [SerializeField] float _raysSize = 1100f;
        [SerializeField] Color _raysColor = new(1f, 0.92f, 0.7f, 0.32f);
        [SerializeField] int _sparkCount = 14;
        [SerializeField] int _confettiCount = 20;
        [SerializeField] float _duration = 1.8f;

        /// <summary>별빛·색종이 색 (흰 아트를 물들인다)</summary>
        static readonly Color[] Palette =
        {
            UITheme.Lamp,
            UITheme.DustyPink,
            UITheme.Lavender,
            UITheme.SoftBlue,
            UITheme.Cream,
        };

        struct Piece
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Delay;
            public float Life;
            public float Size;
            public float Sway;
        }

        RectTransform _root;
        RectTransform _raysRect;
        Image _rays;
        Piece[] _sparks;
        Piece[] _confetti;
        bool _built;
        float _time = -1f;
        float _raysAlpha;
        /// <summary>이번 재생이 움직임 줄이기인지 (Play 때 한 번 읽는다)</summary>
        bool _reduced;

        /// <summary>
        /// 다른 화면이 만든 오브젝트(팝업 루트 등)에 붙인다. 이미 있으면 그것을 돌려준다.
        /// playOnEnable이면 그 오브젝트가 켜질 때마다(팝업이 열릴 때마다) 재생한다.
        /// </summary>
        public static CelebrationFx Attach(GameObject host, Sprite rays, Sprite spark, Sprite[] confetti, bool playOnEnable = true)
        {
            if (host == null) return null;
            var fx = host.GetComponent<CelebrationFx>();
            if (fx == null) fx = host.AddComponent<CelebrationFx>();
            fx._raysSprite = rays;
            fx._sparkSprite = spark;
            fx._confettiSprites = confetti;
            fx._playOnEnable = playOnEnable;
            if (host.activeInHierarchy && playOnEnable) fx.Play();
            return fx;
        }

        /// <summary>재생 중 (빛살이 도는 동안 포함)</summary>
        public bool IsPlaying => _time >= 0f;

        void OnEnable()
        {
            // AddComponent 직후(아직 스프라이트를 받기 전)에는 재생하지 않는다 — Attach가 설정한 뒤 다시 부른다
            if (_playOnEnable && _sparkSprite != null) Play();
        }

        void OnDisable() => Stop();

        /// <summary>처음부터 다시 재생</summary>
        public void Play()
        {
            if (!isActiveAndEnabled) return;
            Build();
            ApplySprites();
            _time = 0f;
            _reduced = UIMotion.Reduced;
            int sparkCount = _reduced ? Mathf.Min(ReducedSparks, _sparks.Length) : _sparks.Length;
            int confettiCount = _reduced ? Mathf.Min(ReducedConfetti, _confetti.Length) : _confetti.Length;
            var center = AnchorCenter();
            if (_raysRect != null)
            {
                _raysRect.anchoredPosition = center;
                _raysRect.localScale = Vector3.one * 0.6f;
                _rays.gameObject.SetActive(true);
                PlaceRaysBehindWindow();
            }

            for (int i = 0; i < _sparks.Length; i++)
            {
                ref var p = ref _sparks[i];
                if (i >= sparkCount)
                {
                    p.Image.gameObject.SetActive(false);
                    continue;
                }
                float angle = (i + Random.Range(-0.3f, 0.3f)) / sparkCount * Mathf.PI * 2f;
                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(260f, 520f);
                p.Delay = Random.Range(0f, 0.08f);
                p.Life = Random.Range(0.6f, 0.95f);
                p.Size = Random.Range(28f, 58f);
                p.Spin = Random.Range(-200f, 200f);
                p.Rect.anchoredPosition = center;
                p.Rect.localScale = Vector3.zero;
                p.Image.color = Color.Lerp(Palette[i % Palette.Length], Color.white, 0.5f);
                p.Rect.SetAsLastSibling();
                p.Image.gameObject.SetActive(true);
            }

            for (int i = 0; i < _confetti.Length; i++)
            {
                ref var p = ref _confetti[i];
                if (i >= confettiCount)
                {
                    p.Image.gameObject.SetActive(false);
                    continue;
                }
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                p.Velocity = new Vector2(Mathf.Cos(angle) * Random.Range(180f, 520f), Mathf.Sin(angle) * Random.Range(520f, 900f));
                p.Delay = Random.Range(0f, 0.12f);
                p.Life = Random.Range(1.3f, _duration);
                p.Size = Random.Range(26f, 40f);
                p.Spin = Random.Range(-540f, 540f);
                p.Sway = Random.Range(0f, Mathf.PI * 2f);
                p.Rect.anchoredPosition = center + new Vector2(Random.Range(-60f, 60f), Random.Range(-20f, 40f));
                // 조각마다 그림 하나 (비율 유지: 정사각 칸 안에 그린다) + 아무 방향으로 돌려 놓기
                p.Image.sprite = RandomConfetti();
                p.Image.preserveAspect = p.Image.sprite != null;
                p.Rect.sizeDelta = new Vector2(p.Size, p.Size);
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                p.Rect.localScale = Vector3.zero;
                p.Image.color = Palette[Random.Range(0, Palette.Length)];
                p.Rect.SetAsLastSibling();
                p.Image.gameObject.SetActive(true);
            }
        }

        /// <summary>스프라이트가 나중에 바뀌어도(Attach) 조각에 반영한다</summary>
        void ApplySprites()
        {
            if (_rays == null && _raysSprite != null)
            {
                _rays = NewImage("CelebrationRays", _raysSprite);
                _raysRect = _rays.rectTransform;
                _raysRect.sizeDelta = new Vector2(_raysSize, _raysSize);
                _rays.gameObject.SetActive(false);
            }
            if (_rays != null) _rays.sprite = _raysSprite;
            for (int i = 0; i < _sparks.Length; i++)
            {
                _sparks[i].Image.sprite = _sparkSprite;
                _sparks[i].Image.preserveAspect = _sparkSprite != null;
            }
            for (int i = 0; i < _confetti.Length; i++) _confetti[i].Image.sprite = ConfettiAt(i);
        }

        /// <summary>색종이 그림 i번째 (돌려 가며, 없으면 null)</summary>
        Sprite ConfettiAt(int index)
        {
            int count = _confettiSprites != null ? _confettiSprites.Length : 0;
            return count > 0 ? _confettiSprites[index % count] : null;
        }

        /// <summary>색종이 그림 하나를 무작위로 (없으면 null)</summary>
        Sprite RandomConfetti()
        {
            int count = _confettiSprites != null ? _confettiSprites.Length : 0;
            return count > 0 ? _confettiSprites[Random.Range(0, count)] : null;
        }

        /// <summary>멈추고 조각을 숨긴다</summary>
        public void Stop()
        {
            _time = -1f;
            if (!_built) return;
            if (_rays != null) _rays.gameObject.SetActive(false);
            for (int i = 0; i < _sparks.Length; i++) _sparks[i].Image.gameObject.SetActive(false);
            for (int i = 0; i < _confetti.Length; i++) _confetti[i].Image.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_time < 0f) return;
            float dt = Time.unscaledDeltaTime;
            _time += dt;

            // 빛살: 0.3초에 펼쳐지고 열려 있는 동안 천천히 돈다 (연출이 끝나도 은은하게 남는다). 줄이기면 반 알파로 가만히
            if (_raysRect != null)
            {
                float open = UIMotion.OutBack(Mathf.Clamp01(_time / 0.35f));
                _raysRect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, open);
                _raysRect.localRotation = _reduced ? Quaternion.identity : Quaternion.Euler(0f, 0f, -_time * 14f);
                float breathe = _reduced ? 1f : 0.85f + 0.15f * Mathf.Sin(_time * 2.2f);
                _raysAlpha = Mathf.Clamp01(_time / 0.25f) * breathe;
                var c = _raysColor;
                _rays.color = new Color(c.r, c.g, c.b, c.a * _raysAlpha * (_reduced ? ReducedRaysAlpha : 1f));
            }

            bool alive = false;
            for (int i = 0; i < _sparks.Length; i++) alive |= StepSpark(ref _sparks[i], dt);
            for (int i = 0; i < _confetti.Length; i++) alive |= StepConfetti(ref _confetti[i], dt);
            if (!alive && _raysRect == null) _time = -1f;
        }

        bool StepSpark(ref Piece p, float dt)
        {
            if (!p.Image.gameObject.activeSelf) return false;
            if (p.Delay > 0f)
            {
                p.Delay -= dt;
                return true;
            }
            p.Life -= dt;
            float k = 1f - Mathf.Clamp01(p.Life / 0.9f);
            p.Rect.anchoredPosition += p.Velocity * dt;
            p.Velocity *= Mathf.Exp(-dt * 4.5f);
            float scale = k < 0.15f ? k / 0.15f : Mathf.Max(0f, 1f - (k - 0.15f) / 0.85f);
            p.Rect.sizeDelta = new Vector2(p.Size, p.Size);
            p.Rect.localScale = Vector3.one * scale;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Spin * k);
            if (p.Life > 0f) return true;
            p.Image.gameObject.SetActive(false);
            return false;
        }

        static bool StepConfetti(ref Piece p, float dt)
        {
            if (!p.Image.gameObject.activeSelf) return false;
            if (p.Delay > 0f)
            {
                p.Delay -= dt;
                return true;
            }
            p.Life -= dt;
            p.Velocity.y -= 1400f * dt;
            p.Velocity.x *= Mathf.Exp(-dt * 1.2f);
            if (p.Velocity.y < -260f) p.Velocity.y = -260f;
            p.Sway += dt * 6f;
            p.Rect.anchoredPosition += (p.Velocity + new Vector2(Mathf.Sin(p.Sway) * 60f, 0f)) * dt;
            p.Rect.localRotation = Quaternion.Euler(0f, Mathf.Sin(p.Sway) * 60f, p.Rect.localEulerAngles.z + p.Spin * dt);
            float fade = Mathf.Clamp01(p.Life / 0.35f);
            p.Rect.localScale = Vector3.one;
            var c = p.Image.color;
            p.Image.color = new Color(c.r, c.g, c.b, fade);
            if (p.Life > 0f) return true;
            p.Image.gameObject.SetActive(false);
            return false;
        }

        /// <summary>조각의 anchoredPosition 기준 (조각은 부모 가운데 기준점이라 rect.center를 뺀다)</summary>
        Vector2 AnchorCenter()
        {
            var anchor = _anchor;
            if (anchor == null) anchor = transform.Find("Window") as RectTransform;
            return anchor != null && anchor != _root ? UIFind.CenterIn(anchor, _root) - _root.rect.center : Vector2.zero;
        }

        void PlaceRaysBehindWindow()
        {
            var window = _anchor != null ? _anchor : transform.Find("Window");
            if (window != null && window.parent == transform) _raysRect.SetSiblingIndex(window.GetSiblingIndex());
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            _root = (RectTransform)transform;

            if (_raysSprite != null)
            {
                _rays = NewImage("CelebrationRays", _raysSprite);
                _raysRect = _rays.rectTransform;
                _raysRect.sizeDelta = new Vector2(_raysSize, _raysSize);
                _rays.gameObject.SetActive(false);
            }

            _sparks = new Piece[Mathf.Max(0, _sparkCount)];
            for (int i = 0; i < _sparks.Length; i++)
            {
                var image = NewImage("CelebrationSpark", _sparkSprite);
                _sparks[i] = new Piece { Image = image, Rect = image.rectTransform };
                image.gameObject.SetActive(false);
            }
            _confetti = new Piece[Mathf.Max(0, _confettiCount)];
            for (int i = 0; i < _confetti.Length; i++)
            {
                var image = NewImage("CelebrationConfetti", ConfettiAt(i));
                _confetti[i] = new Piece { Image = image, Rect = image.rectTransform };
                image.gameObject.SetActive(false);
            }
        }

        Image NewImage(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = sprite != null;
            return image;
        }
    }
}
