using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 빨간 점 알림 (M09, UX-SPEC §3.13 · §1.5 · §1.8). 모양 셋 + 숨김:
    ///   · SetDot(true): 숫자 없는 점 — 알려 주기만 하는 곳 (공지 · 출석 등).
    ///   · SetCount(n): 숫자 배지 — 받을 것이 있는 곳만 (우편 · 일지 · 원정 완료 · 던전 입장권 …). 0 이하면 숨긴다. 99를 넘으면 "99+".
    ///     빌더가 40px보다 작게 만든 배지도 숫자를 보일 때는 40px(_countSize)로 커진다 (점으로 돌아가면 원래 크기, 가운데 기준).
    ///   · SetNew(true): "NEW" 알약 — 새로 열린 기능 (RedDotView가 받을 것이 없는 FeatureDot 잎에 고른다).
    ///     빨간 원(배지 자신의 Image)과 숫자는 끄고 자식 _newPill(SceneParts.BuildBadge가 꺼 둔 채 만든다)만 켠다. 알약이 없는 옛 배지는 점으로 대신한다.
    ///   · Show(on, count) / Show(on, count, isNew): RedDotView용 지름길 (숫자 → NEW → 점 순서).
    ///   · false · 0을 주는 SetDot · SetCount · SetNew와 Hide는 모두 배지를 숨긴다 (게임오브젝트 끔).
    /// 움직임 (§1.8, 모두 unscaled 시간):
    ///   · 나타남: 꺼져 있다가 켜질 때만 0 → 1.2 → 1 (0.22초 OutBack). 이미 켜져 있으면 모양이 바뀌어도 튀지 않는다.
    ///     부모가 꺼져 있는 동안 켜졌으면 처음 보일 때 튄다.
    ///   · 숨쉬기: 숫자 배지만 3초마다 0.5초 동안 1 → 1.06 → 1, 팝업이 하나도 없을 때(전투 화면이 맨 앞)만. 모든 숫자 배지가 같은 박자로 쉰다.
    ///     점 · NEW는 숨쉬지 않는다. 움직임 줄이기(UIMotion.Reduced)면 나타남 · 숨쉬기 둘 다 없다.
    ///   · 배지마다 Update · 코루틴을 두지 않고 공용 틱 하나(Canvas.willRenderCanvases)가 움직이는 배지만 돌린다. 돌 배지가 없으면 틱을 뗀다.
    /// 크기 연출이 가운데 기준이 되도록 실행 중 처음 쓸 때 피벗만 가운데로 옮긴다 (자리 유지 — 빌더의 오른쪽 위 피벗 기준 자리는 그대로 보인다).
    /// 숫자는 SetText로 써서 할당이 없고, 같은 값이면 다시 쓰지 않는다. 배지와 글자는 누름을 받지 않는다 (raycastTarget 끔은 빌더 몫).
    /// </summary>
    public class Badge : MonoBehaviour
    {
        /// <summary>나타남 정점이 1.2가 되는 OutBack s (넘침 = 4s³ / 27(s+1)² ≈ 0.20)</summary>
        const float AppearBack = 2.59f;

        [SerializeField] TMP_Text _countText;
        [Tooltip("NEW 알약 (꺼 둔 자식 \"New\" — SceneParts.BuildBadge가 연결). 없으면 SetNew는 점으로 대신한다")]
        [SerializeField] GameObject _newPill;
        [Tooltip("숫자를 보일 때 최소 지름 (§1.5 숫자 배지 40)")]
        [SerializeField] float _countSize = UISize.BadgeCount;

        enum Mode
        {
            Hidden = 0,
            Dot = 1,
            Count = 2,
            New = 3,
        }

        Mode _mode;
        /// <summary>마지막으로 쓴 숫자 (같으면 다시 쓰지 않는다)</summary>
        int _shownCount = int.MinValue;

        bool _prepared;
        RectTransform _rect;
        Image _circle;
        bool _resizable;
        Vector2 _builtSize;
        Vector3 _baseScale = Vector3.one;
        float _appliedScale = 1f;
        /// <summary>나타남 시작 시각 (unscaled, 0보다 작으면 없음)</summary>
        float _appearStart = -1f;
        bool _tracked;

        /// <summary>지금 숫자 배지인지</summary>
        public bool IsCount => _mode == Mode.Count && gameObject.activeSelf;

        /// <summary>지금 NEW 알약인지</summary>
        public bool IsNew => _mode == Mode.New && gameObject.activeSelf;

        // ───────────────────────────── 공개 API ─────────────────────────────

        /// <summary>숫자 배지 (0 이하면 숨김, 99 넘으면 "99+")</summary>
        public void SetCount(int count)
        {
            if (count <= 0)
            {
                Hide();
                return;
            }
            Apply(Mode.Count, count);
        }

        /// <summary>숫자 없는 점을 켜고 끈다 (F-04: 공지 · 출석 등 알려 주기만 하는 곳)</summary>
        public void SetDot(bool on)
        {
            if (on) Apply(Mode.Dot, 0);
            else Hide();
        }

        /// <summary>NEW 알약을 켜고 끈다 (새로 열린 기능). 알약이 없는 배지는 점으로 대신한다</summary>
        public void SetNew(bool on)
        {
            if (!on)
            {
                Hide();
                return;
            }
            Apply(_newPill != null ? Mode.New : Mode.Dot, 0);
        }

        /// <summary>숫자가 있으면 숫자, 없으면 켜짐 여부에 따라 점 (빨간 점 트리 RedDotView용)</summary>
        public void Show(bool on, int count)
        {
            if (count > 0) SetCount(count);
            else SetDot(on);
        }

        /// <summary>숫자 → (켜짐이고 새 기능이면) NEW → 점 순서로 고른다</summary>
        public void Show(bool on, int count, bool isNew)
        {
            if (count > 0) SetCount(count);
            else if (on && isNew) SetNew(true);
            else SetDot(on);
        }

        /// <summary>배지를 숨긴다</summary>
        public void Hide()
        {
            _appearStart = -1f;
            Untrack(this);
            _mode = Mode.Hidden;
            if (_prepared) SetScale(1f);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // ───────────────────────────── 모양 ─────────────────────────────

        void Apply(Mode mode, int count)
        {
            Prepare();
            bool wasActive = gameObject.activeSelf;
            bool isNew = mode == Mode.New;

            if (_circle != null && _circle.enabled == isNew) _circle.enabled = !isNew;
            if (_newPill != null && _newPill.activeSelf != isNew) _newPill.SetActive(isNew);
            if (_countText != null)
            {
                bool showCount = mode == Mode.Count;
                if (_countText.gameObject.activeSelf != showCount) _countText.gameObject.SetActive(showCount);
                if (showCount && count != _shownCount)
                {
                    _shownCount = count;
                    if (count > 99) _countText.SetText("99+");
                    else _countText.SetText("{0}", count);
                }
            }
            if (_resizable)
            {
                var size = mode == Mode.Count ? Vector2.Max(_builtSize, new Vector2(_countSize, _countSize)) : _builtSize;
                if (_rect.sizeDelta != size) _rect.sizeDelta = size;
            }
            _mode = mode;

            if (!wasActive)
            {
                gameObject.SetActive(true);
                if (!UIMotion.Reduced) _appearStart = Time.unscaledTime;
            }
            if (_appearStart >= 0f || mode == Mode.Count) Track(this);
            else
            {
                Untrack(this);
                SetScale(1f);
            }
        }

        /// <summary>처음 한 번: 원 Image · 만든 크기를 기억하고 피벗을 가운데로 옮긴다 (보이는 자리는 그대로)</summary>
        void Prepare()
        {
            if (_prepared) return;
            _prepared = true;
            _rect = transform as RectTransform;
            _circle = GetComponent<Image>();
            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
            if (_rect == null || _rect.anchorMin != _rect.anchorMax) return;

            _resizable = true;
            _builtSize = _rect.sizeDelta;
            var center = new Vector2(0.5f, 0.5f);
            if (_rect.pivot == center) return;
            var shift = Vector2.Scale(center - _rect.pivot, _rect.rect.size);
            _rect.pivot = center;
            _rect.anchoredPosition += Vector2.Scale(shift, (Vector2)_baseScale);
        }

        void SetScale(float scale)
        {
            if (Mathf.Approximately(_appliedScale, scale)) return;
            _appliedScale = scale;
            transform.localScale = _baseScale * scale;
        }

        void OnDestroy() => Untrack(this);

        // ───────────────────────────── 공용 틱 ─────────────────────────────

        static readonly List<Badge> Moving = new();
        static readonly Canvas.WillRenderCanvases TickHandler = Tick;
        static bool s_hooked;

        static void Track(Badge badge)
        {
            if (badge._tracked) return;
            badge._tracked = true;
            Moving.Add(badge);
            if (s_hooked) return;
            s_hooked = true;
            Canvas.willRenderCanvases += TickHandler;
        }

        static void Untrack(Badge badge)
        {
            if (!badge._tracked) return;
            badge._tracked = false;
            Moving.Remove(badge);
        }

        /// <summary>캔버스를 그리기 직전 프레임마다 한 번 (움직이는 배지가 있을 때만 붙어 있다)</summary>
        static void Tick()
        {
            if (Moving.Count == 0)
            {
                Canvas.willRenderCanvases -= TickHandler;
                s_hooked = false;
                return;
            }

            float now = Time.unscaledTime;
            bool reduced = UIMotion.Reduced;
            var ui = UIManager.Instance;
            bool front = ui == null || !ui.HasOpenPopup;
            float pulse = 1f;
            if (front && !reduced)
            {
                float phase = Mathf.Repeat(now, UIMotion.BadgePulseInterval);
                if (phase < UIMotion.BadgePulse) pulse = 1f + (UIMotion.BadgePulseScale - 1f) * Mathf.Sin(phase / UIMotion.BadgePulse * Mathf.PI);
            }

            for (int i = Moving.Count - 1; i >= 0; i--)
            {
                var badge = Moving[i];
                if (badge == null)
                {
                    Moving.RemoveAt(i);
                    continue;
                }
                if (badge.Step(now, pulse, reduced)) continue;
                badge._tracked = false;
                Moving.RemoveAt(i);
            }
        }

        /// <summary>한 배지의 크기 (계속 돌아야 하면 true)</summary>
        bool Step(float now, float pulse, bool reduced)
        {
            if (!gameObject.activeInHierarchy)
            {
                // 안 보이는 동안: 나타남은 처음 보일 때로 미룬다. 숫자 배지는 다시 보일 때를 위해 남는다
                if (_appearStart >= 0f) _appearStart = now;
                return _appearStart >= 0f || _mode == Mode.Count;
            }

            float scale = 1f;
            bool keep = false;
            if (_appearStart >= 0f)
            {
                float k = (now - _appearStart) / UIMotion.BadgeAppear;
                if (reduced || k >= 1f) _appearStart = -1f;
                else
                {
                    scale = UIMotion.OutBack(k, AppearBack);
                    keep = true;
                }
            }
            if (_appearStart < 0f && _mode == Mode.Count)
            {
                scale = pulse;
                keep = true;
            }
            SetScale(scale);
            return keep;
        }
    }
}
