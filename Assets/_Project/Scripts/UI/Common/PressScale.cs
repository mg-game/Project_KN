using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 누르는 동안 살짝 작아지는 버튼 피드백 (M61, UX-SPEC §3.10 · §1.8). 자기 transform만 키우고 줄인다 (자식 "Visual"을 찾지 않는다).
    ///   · 누름: 0.06초 OutCubic으로 0.94배. 누른 채 밖으로 나가면 0.08초에 돌아오고, 다시 들어오면 0.06초에 다시 눌린다.
    ///   · 안에서 뗌: 0.16초 동안 살짝 넘쳤다가(0.94에서 떼면 정점 약 1.027) 1로. 밖에서 뗌 · 스크롤로 넘어감: 0.10초 OutCubic (넘침 없음 — 클릭도 없다).
    ///   · 누르는 자리는 크기가 바뀌는 동안에도 원래 크기 그대로다: 이 오브젝트(와 그 아래, 안쪽 PressScale 몫은 빼고)의 raycastTarget 그림마다
    ///     raycastPadding을 배율만큼 늘려(넘칠 때는 줄여) 화면 위 누르는 자리를 고정하고, 1배로 돌아오면 원래 패딩으로 되돌린다.
    ///     (안 그러면 가장자리 3% 안을 누른 손가락이 줄어든 자리 밖이 되어 나감 → 커짐 → 들어옴 → 줄어듦으로 떨리고, 길게 누르기도 끊긴다.)
    ///   · 안에서 뗐고 손가락이 드래그 임계값 안에서만 움직였으면 Tap 소리 (볼륨 0.5, UISfx — 소리 표가 없으면 조용히 지나간다).
    ///     길게 눌러 반복한 뒤(HoldRepeatButton이 클릭을 막음)나 스크롤로 넘어간 누름에는 소리가 없다.
    ///     자기 소리를 따로 내는 버튼(하단 탭 Tab, 받기 Claim)은 TapSound를 끈다.
    ///   · 움직일 때만 프레임마다 돈다 (FrameTicker). 쉬는 동안에는 Update가 없다 — 포인터 이벤트를 받아야 하므로 enabled는 끄지 않는다.
    ///   · 꺼질 때(OnDisable) 원래 크기 · 원래 누르는 자리로 바로 돌아간다.
    ///   · 창 포커스를 잃으면 누름을 끝내고 원래 크기로 되돌린다 (M01): 알트탭 중에는 EventSystem이 떼기를 놓쳐 작아진 채로 남을 수 있다.
    ///     누름만 끝낼 뿐 게임은 멈추지 않는다.
    /// 코치마크 강조는 크기만 바뀌므로 영향이 없다. 누를 수 없는(interactable=false) 버튼은 줄어들지 않는다.
    /// </summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        /// <summary>안에서 뗄 때 OutBack s — 0.94에서 1로 가며 정점 약 1.027 (§1.8 '정점 ≈1.025~1.03'; s=1.2로는 0.3%밖에 넘치지 않는다)</summary>
        const float ReleaseBack = 4.5f;
        const float TapVolume = 0.5f;
        /// <summary>이보다 작은 배율에서는 누르는 자리를 보정하지 않는다 (0으로 나누기 방지)</summary>
        const float MinPadScale = 0.05f;

        /// <summary>누르는 자리 그림을 모을 때 쓰는 공용 목록 (누름 시작에 한 번, 할당 없이 다시 쓴다)</summary>
        static readonly List<Graphic> GraphicScratch = new();

        [SerializeField] float _pressedScale = UIMotion.PressScale;
        [Tooltip("탭 소리 (Tap 0.5). 자기 소리를 따로 내는 버튼(하단 탭 = Tab, 받기 = Claim)은 끈다 — 소리가 겹치지 않게")]
        [SerializeField] bool _tapSound = true;

        enum Curve
        {
            OutCubic = 0,
            Release = 1,
        }

        Selectable _selectable;
        Vector3 _baseScale = Vector3.one;
        FrameTicker _ticker;

        bool _held;
        bool _inside;
        int _pointerId;
        /// <summary>지금 배율 (원래 크기 기준)</summary>
        float _current = 1f;
        float _from = 1f;
        float _to = 1f;
        float _time;
        float _duration;
        Curve _curve;

        /// <summary>배율이 1이 아닌 동안 누르는 자리를 지키는 그림들과 그 원래 raycastPadding (처음 줄어들 때 모은다 — 버튼마다 처음 한 번만 목록을 만든다)</summary>
        List<Graphic> _hitGraphics;
        List<Vector4> _hitPadding;
        /// <summary>지금 raycastPadding을 보정해 둔 상태인지 (1배로 돌아오면 되돌린다)</summary>
        bool _padded;

        /// <summary>지금 눌려 있는지</summary>
        public bool IsHeld => _held;

        /// <summary>탭 소리를 낼지 (자기 소리가 있는 버튼은 빌더 · 화면이 끈다)</summary>
        public bool TapSound
        {
            get => _tapSound;
            set => _tapSound = value;
        }

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _baseScale = transform.localScale;
            _ticker = new FrameTicker(this, Tick);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_held || (_selectable != null && !_selectable.IsInteractable())) return;
            _held = true;
            _inside = true;
            _pointerId = eventData.pointerId;
            TweenTo(_pressedScale, UIMotion.PressDown, Curve.OutCubic);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_held || eventData.pointerId != _pointerId) return;
            _held = false;
            // 스크롤이 시작되면 EventSystem이 누른 버튼에 떼기를 보낸다 — 그때는 넘침 없이 조용히 돌아간다
            if (_inside && !eventData.dragging)
            {
                TweenTo(1f, UIMotion.ReleaseInside, Curve.Release);
                if (_tapSound && IsTap(eventData)) UISfx.Play(UISfxKey.Tap, TapVolume);
            }
            else
            {
                TweenTo(1f, UIMotion.ReleaseOutside, Curve.OutCubic);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_held && eventData.pointerId != _pointerId) return;
            _inside = false;
            if (_held) TweenTo(1f, UIMotion.PressExit, Curve.OutCubic);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_held && eventData.pointerId != _pointerId) return;
            _inside = true;
            if (_held) TweenTo(_pressedScale, UIMotion.PressDown, Curve.OutCubic);
        }

        void OnDisable() => ResetPress();

        /// <summary>포커스를 잃으면 눌린 크기를 되돌린다 (누르기만 끝낸다, M01). 눌리지도 움직이지도 않는 버튼의 크기는 건드리지 않는다</summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused && (_held || (_ticker != null && _ticker.Running))) ResetPress();
        }

        /// <summary>누름을 버리고 원래 크기로 (꺼짐 · 포커스 잃음)</summary>
        void ResetPress()
        {
            _ticker?.Stop();
            _held = false;
            _inside = false;
            Apply(1f);
        }

        /// <summary>안에서 뗀 짧은 누름인지 (스크롤 · 길게 누르기 · 큰 움직임이면 아니다)</summary>
        bool IsTap(PointerEventData eventData)
        {
            if (eventData.dragging || !eventData.eligibleForClick) return false;
            if (_selectable != null && !_selectable.IsInteractable()) return false;
            var system = EventSystem.current;
            float threshold = system != null ? system.pixelDragThreshold : 10f;
            return (eventData.position - eventData.pressPosition).sqrMagnitude <= threshold * threshold;
        }

        void TweenTo(float target, float duration, Curve curve)
        {
            _from = _current;
            _to = target;
            _time = 0f;
            _duration = duration;
            _curve = curve;
            _ticker.Start();
            if (!_ticker.Running) Apply(target); // 꺼진 컴포넌트: 움직이지 않고 바로
        }

        bool Tick()
        {
            _time += Time.unscaledDeltaTime;
            float k = _duration > 0f ? Mathf.Clamp01(_time / _duration) : 1f;
            if (k >= 1f)
            {
                // 끝은 목표 값 그대로 (1배면 누르는 자리도 정확히 원래 패딩으로 돌아간다)
                Apply(_to);
                return false;
            }
            float eased = _curve == Curve.Release ? UIMotion.OutBack(k, ReleaseBack) : UIMotion.OutCubic(k);
            Apply(Mathf.LerpUnclamped(_from, _to, eased));
            return true;
        }

        void Apply(float scale)
        {
            _current = scale;
            transform.localScale = _baseScale * scale;
            if (scale == 1f) RestoreHitArea();
            else KeepHitArea(scale);
        }

        // ───────────────────────────── 누르는 자리 유지 ─────────────────────────────

        /// <summary>
        /// 배율 scale에서도 화면 위 누르는 자리가 1배 때와 같도록 raycastPadding을 고친다 (GraphicRaycaster는 크기가 바뀐 사각형 + 패딩으로 맞춘다).
        /// 이 오브젝트 좌표(원점 = 피벗, 크기 변화의 중심)에서 원래 패딩 사각형을 1/scale배 한 것이 새 사각형이다.
        /// </summary>
        void KeepHitArea(float scale)
        {
            if (!(scale > MinPadScale)) return;
            if (!_padded) CaptureHitArea();
            float inv = 1f / scale;
            var self = transform;
            for (int i = 0; i < _hitGraphics.Count; i++)
            {
                var graphic = _hitGraphics[i];
                if (graphic == null) continue;
                var rt = graphic.rectTransform;
                var r = rt.rect;
                var pad = _hitPadding[i];
                Vector2 min = new(r.xMin + pad.x, r.yMin + pad.y);
                Vector2 max = new(r.xMax - pad.z, r.yMax - pad.w);
                if (rt == self)
                {
                    min *= inv;
                    max *= inv;
                }
                else
                {
                    // 자식 그림: 이 오브젝트 좌표로 옮겨 피벗 기준으로 늘린 뒤 그림 좌표로 되돌린다 (이 오브젝트의 배율은 두 변환에서 서로 지워진다)
                    min = rt.InverseTransformPoint(self.TransformPoint((Vector2)self.InverseTransformPoint(rt.TransformPoint(min)) * inv));
                    max = rt.InverseTransformPoint(self.TransformPoint((Vector2)self.InverseTransformPoint(rt.TransformPoint(max)) * inv));
                }
                graphic.raycastPadding = new Vector4(min.x - r.xMin, min.y - r.yMin, r.xMax - max.x, r.yMax - max.y);
            }
        }

        /// <summary>누르는 자리 그림과 원래 패딩을 모은다 (켜진 raycastTarget만, 안쪽에 다른 PressScale이 있으면 그쪽 몫 — 같은 패딩을 서로 고쳐 쓰지 않게)</summary>
        void CaptureHitArea()
        {
            _padded = true;
            _hitGraphics ??= new List<Graphic>(2);
            _hitPadding ??= new List<Vector4>(2);
            _hitGraphics.Clear();
            _hitPadding.Clear();
            GetComponentsInChildren(false, GraphicScratch);
            for (int i = 0; i < GraphicScratch.Count; i++)
            {
                var graphic = GraphicScratch[i];
                if (graphic == null || !graphic.raycastTarget) continue;
                if (graphic.transform != transform && graphic.GetComponentInParent<PressScale>() != this) continue;
                _hitGraphics.Add(graphic);
                _hitPadding.Add(graphic.raycastPadding);
            }
            GraphicScratch.Clear();
        }

        /// <summary>원래 raycastPadding으로 되돌린다 (1배 · 꺼짐 · 포커스 잃음)</summary>
        void RestoreHitArea()
        {
            if (!_padded) return;
            _padded = false;
            for (int i = 0; i < _hitGraphics.Count; i++)
            {
                var graphic = _hitGraphics[i];
                if (graphic != null) graphic.raycastPadding = _hitPadding[i];
            }
            _hitGraphics.Clear();
            _hitPadding.Clear();
        }
    }

    /// <summary>
    /// 할 일이 있을 때만 프레임마다 도는 코루틴 (같은 객체를 StartCoroutine에 다시 넘겨 할당 없이 다시 쓴다).
    /// 포인터 이벤트를 받는 컴포넌트는 enabled = false로 쉬면 이벤트를 못 받는다 (ExecuteEvents가 isActiveAndEnabled를 본다) —
    /// 그래서 Update를 두지 않고 움직일 때만 이것을 돌린다. tick이 false를 돌려주면 멈춘다 (처음 한 번은 Start 안에서 바로 불린다).
    /// 게임오브젝트가 꺼지면 코루틴도 멈추므로 주인은 OnDisable에서 Stop을 부른다.
    /// tick이 예외를 던지면 기록하고 멈춘 것으로 표시한다 (Unity는 던진 코루틴을 말없이 끝내므로, 그대로 두면 Running이 true인 채
    /// 다음 Start가 아무것도 하지 않는다 — 버튼이 꺼질 때까지 다시 돌지 않는다). 주인의 상태 정리는 주인 몫이다.
    /// </summary>
    internal sealed class FrameTicker : IEnumerator
    {
        readonly MonoBehaviour _host;
        readonly Func<bool> _tick;
        bool _alive;

        public FrameTicker(MonoBehaviour host, Func<bool> tick)
        {
            _host = host;
            _tick = tick;
        }

        /// <summary>돌고 있는지</summary>
        public bool Running => _alive;

        /// <summary>돌기 시작한다 (이미 돌면 그대로). 주인이 꺼져 있으면 시작하지 않는다 — Running으로 확인한다</summary>
        public void Start()
        {
            if (_alive || _host == null || !_host.isActiveAndEnabled) return;
            _alive = true;
            _host.StartCoroutine(this);
        }

        public void Stop()
        {
            if (!_alive) return;
            _alive = false;
            if (_host != null) _host.StopCoroutine(this);
        }

        bool IEnumerator.MoveNext()
        {
            if (!_alive) return false;
            bool more;
            try
            {
                more = _tick();
            }
            catch (Exception e)
            {
                Debug.LogException(e, _host);
                more = false;
            }
            // tick 안에서 Stop이 불렸으면(주인이 꺼짐 등) 더 돌지 않는다
            if (more && _alive) return true;
            _alive = false;
            return false;
        }

        object IEnumerator.Current => null;

        void IEnumerator.Reset() { }
    }
}
