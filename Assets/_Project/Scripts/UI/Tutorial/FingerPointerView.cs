using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 튜토리얼 손가락. 손가락 그림은 끝이 위를 향하고(피벗 = 손끝), 대상 쪽으로 돌려 놓는다.
    ///   · 톡톡: 대상 중심으로 다가가 눌렀다 떨어지기를 반복하고, 누르는 순간 물결 고리가 퍼진다 (1주기 0.95초).
    ///   · 끌기: from → to로 미끄러지듯 옮겨 가고 끝에서 사라졌다가 다시 (끌어 놓기 안내).
    ///     별똥 꼬리(머리가 오른쪽인 그림)가 손끝을 따라온다. 꼬리는 그림 비율의 _trailMaxStretch배까지만 늘인다.
    ///   · Nudge: 어둠 밖을 눌렀을 때 "여기예요" 하고 크게 한 번 튄다.
    /// 좌표는 부모(코치마크 층) 로컬. Update는 할당 없음.
    /// </summary>
    public class FingerPointerView : MonoBehaviour
    {
        [SerializeField] RectTransform _finger;
        [SerializeField] Image _fingerImage;
        [SerializeField] Image _ripple;
        [SerializeField] Image _trail;
        [Tooltip("꼬리 그림을 가로로 늘여도 되는 최대 배율 (그림 비율 기준)")]
        [SerializeField] float _trailMaxStretch = 1.5f;
        [SerializeField] float _tapPeriod = 0.95f;
        [SerializeField] float _dragPeriod = 1.5f;
        [Tooltip("손가락 그림이 기본으로 가리키는 방향(도). 0 = 위")]
        [SerializeField] float _spriteAngle;

        enum Mode
        {
            Hidden,
            Tap,
            Drag,
        }

        Mode _mode;
        Vector2 _target;
        Vector2 _dragFrom;
        Vector2 _approach = new(0.55f, -0.83f);
        float _time;
        float _nudge = -1f;
        float _alpha;
        float _alphaTarget;

        /// <summary>손가락이 보이는 중</summary>
        public bool IsVisible => _mode != Mode.Hidden;

        void Awake() => ApplyHidden();

        /// <summary>대상 지점을 톡톡 누른다. approach = 대상에서 손가락 몸통 쪽 방향</summary>
        public void ShowTap(Vector2 target, Vector2 approach)
        {
            bool wasHidden = _mode == Mode.Hidden;
            _mode = Mode.Tap;
            _target = target;
            _approach = approach.sqrMagnitude > 0.001f ? approach.normalized : new Vector2(0.55f, -0.83f);
            _alphaTarget = 1f;
            if (wasHidden) Restart();
            gameObject.SetActive(true);
        }

        /// <summary>from에서 to로 끌어 놓는 시늉</summary>
        public void ShowDrag(Vector2 from, Vector2 to)
        {
            bool restart = _mode != Mode.Drag || (from - _dragFrom).sqrMagnitude > 4f || (to - _target).sqrMagnitude > 4f;
            _mode = Mode.Drag;
            _dragFrom = from;
            _target = to;
            var dir = to - from;
            _approach = dir.sqrMagnitude > 0.001f ? new Vector2(-dir.y, dir.x).normalized : new Vector2(0.55f, -0.83f);
            if (_approach.y > 0f) _approach = -_approach; // 손바닥이 아래로 오게
            _alphaTarget = 1f;
            if (restart) Restart();
            gameObject.SetActive(true);
        }

        /// <summary>위치만 옮긴다 (대상이 움직일 때, 주기는 그대로)</summary>
        public void MoveTarget(Vector2 target) => _target = target;

        /// <summary>천천히 사라진다</summary>
        public void Hide()
        {
            _alphaTarget = 0f;
            if (_mode == Mode.Hidden) return;
            if (_alpha <= 0.01f) ApplyHidden();
        }

        /// <summary>어둠 밖을 눌렀을 때 크게 한 번 튄다</summary>
        public void Nudge()
        {
            if (_mode == Mode.Hidden) return;
            _nudge = 0f;
        }

        void Restart()
        {
            _time = 0f;
            _alpha = 0f;
        }

        void ApplyHidden()
        {
            _mode = Mode.Hidden;
            _alpha = 0f;
            _alphaTarget = 0f;
            if (_finger != null) _finger.gameObject.SetActive(false);
            if (_ripple != null) _ripple.gameObject.SetActive(false);
            if (_trail != null) _trail.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_mode == Mode.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            _alpha = Mathf.MoveTowards(_alpha, _alphaTarget, dt / 0.18f);
            if (_alphaTarget <= 0f && _alpha <= 0f)
            {
                ApplyHidden();
                return;
            }
            if (!_finger.gameObject.activeSelf) _finger.gameObject.SetActive(true);

            float nudge = 0f;
            if (_nudge >= 0f)
            {
                _nudge += dt;
                nudge = Mathf.Sin(Mathf.Clamp01(_nudge / 0.35f) * Mathf.PI);
                if (_nudge >= 0.35f) _nudge = -1f;
            }

            if (_mode == Mode.Tap) StepTap(nudge);
            else StepDrag();
        }

        void StepTap(float nudge)
        {
            float phase = _time % _tapPeriod / _tapPeriod;
            // 0~0.45 다가감, 0.45~0.6 꾹, 0.6~1 물러남
            float press = phase < 0.45f ? UIMotion.OutCubic(phase / 0.45f)
                : phase < 0.6f ? 1f
                : 1f - UIMotion.OutCubic((phase - 0.6f) / 0.4f);
            float distance = Mathf.Lerp(34f, 4f, press) + nudge * 26f;
            SetFinger(_target + _approach * distance, 1f - 0.08f * press + nudge * 0.18f, _alpha);

            if (_ripple == null) return;
            if (phase >= 0.45f && phase < 0.95f)
            {
                float k = (phase - 0.45f) / 0.5f;
                if (!_ripple.gameObject.activeSelf) _ripple.gameObject.SetActive(true);
                _ripple.rectTransform.anchoredPosition = _target;
                _ripple.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.25f, UIMotion.OutCubic(k));
                var c = _ripple.color;
                _ripple.color = new Color(c.r, c.g, c.b, 0.75f * (1f - k) * _alpha);
            }
            else if (_ripple.gameObject.activeSelf)
            {
                _ripple.gameObject.SetActive(false);
            }
        }

        void StepDrag()
        {
            float phase = _time % _dragPeriod / _dragPeriod;
            // 0~0.15 내려앉기, 0.15~0.75 끌기, 0.75~1 놓고 사라지기
            float move = phase < 0.15f ? 0f : phase < 0.75f ? UIMotion.OutCubic((phase - 0.15f) / 0.6f) : 1f;
            float fade = phase < 0.1f ? phase / 0.1f : phase > 0.85f ? 1f - (phase - 0.85f) / 0.15f : 1f;
            var tip = Vector2.LerpUnclamped(_dragFrom, _target, move);
            float press = phase < 0.15f ? phase / 0.15f : phase < 0.75f ? 1f : 1f - (phase - 0.75f) / 0.1f;
            SetFinger(tip + _approach * Mathf.Lerp(26f, 4f, Mathf.Clamp01(press)), 1f - 0.08f * Mathf.Clamp01(press), _alpha * fade);

            if (_trail != null)
            {
                bool show = phase > 0.15f && phase < 0.85f;
                if (_trail.gameObject.activeSelf != show) _trail.gameObject.SetActive(show);
                if (show)
                {
                    var dir = tip - _dragFrom;
                    float distance = dir.magnitude;
                    var along = distance > 0.001f ? dir / distance : Vector2.right;
                    var rt = _trail.rectTransform;
                    // 머리는 손끝에, 길이는 지나온 거리만큼 (그림이 뭉개지지 않게 최대 길이까지만)
                    float length = Mathf.Clamp(distance, 1f, TrailMaxLength(rt.sizeDelta.y));
                    rt.anchoredPosition = tip - along * (length * 0.5f);
                    rt.sizeDelta = new Vector2(length, rt.sizeDelta.y);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
                    var c = _trail.color;
                    _trail.color = new Color(c.r, c.g, c.b, 0.55f * _alpha * fade);
                }
            }
            if (_ripple != null && _ripple.gameObject.activeSelf) _ripple.gameObject.SetActive(false);
        }

        /// <summary>꼬리 최대 길이 = 높이 × 그림 가로세로 비율 × 최대 배율</summary>
        float TrailMaxLength(float height)
        {
            var sprite = _trail.sprite;
            float aspect = sprite != null ? sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : 4f;
            return Mathf.Max(1f, height * aspect * _trailMaxStretch);
        }

        void SetFinger(Vector2 tip, float scale, float alpha)
        {
            _finger.anchoredPosition = tip;
            // 손끝이 대상(−approach 방향)을 향하도록
            float angle = Mathf.Atan2(-_approach.y, -_approach.x) * Mathf.Rad2Deg - 90f - _spriteAngle;
            _finger.localRotation = Quaternion.Euler(0f, 0f, angle);
            _finger.localScale = Vector3.one * scale;
            var c = _fingerImage.color;
            _fingerImage.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
