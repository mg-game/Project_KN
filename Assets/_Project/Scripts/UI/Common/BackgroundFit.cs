using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>배경 그림 맞춤 방식 (BackgroundFit)</summary>
    public enum BackgroundFitMode
    {
        /// <summary>부모를 덮는다 (비율 그대로, 넘치는 쪽은 잘림 — 부모에 마스크가 있어야 한다). 자르는 자리는 Focus</summary>
        Cover = 0,
        /// <summary>부모 안에 다 들어간다 (비율 그대로, 남는 쪽은 Backing이 채운다). 놓는 자리는 Focus</summary>
        Contain = 1,
        /// <summary>부모 그대로 (9-slice · 타일 · 그라데이션 — 늘려도 되는 그림). "배경"이라는 표시만</summary>
        Fill = 2,
    }

    /// <summary>
    /// 배경 그림 하나(Image · RawImage)를 부모 안에 맞춘다 — 절대 늘이거나 찌그러뜨리지 않는다 (aspect AF-1, "배경이 해상도 안 맞을 때").
    /// AspectRatioFitter(EnvelopeParent)를 대신한다: 같은 덮개에 더해
    /// · Focus — 그림의 uv 점 Focus가 부모의 같은 상대 위치에 온다 ((0.5,0.5) 가운데, (0.5,1) 위를 남김). AspectRatioFitter의 "피벗 기준 자르기"와 같다.
    /// · Keep — 꼭 남길 그림 영역(uv). 화면이 너무 좁거나 길면 배율을 낮춰서라도 Keep을 다 보이고, 모자란 곳은 Backing이 보인다.
    /// · 그림을 바꿔 끼우면(Image.sprite · RawImage.texture) 그 그림의 비율로 다시 맞춘다 — 빌드 때 비율에 묶이지 않는다.
    /// · Backing — 그림이 부모를 다 못 덮을 때만 켜는 뒤판 (덮으면 꺼서 겹쳐 그리지 않는다).
    /// 매 프레임 일은 없다 (Update 없음, 할당 없음): 켜질 때 · 자기/부모 크기가 바뀔 때 · 그림이 바뀔 때만 맞춘다.
    /// 앵커 · 위치 · 크기를 DrivenRectTransformTracker로 잡으므로 에디터에서 다시 맞춰도 씬이 바뀐 것으로 치지 않는다.
    /// 피벗 · 회전 · 배율은 건드리지 않는다 (localScale.x = −1 좌우 뒤집기도 그대로 된다).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class BackgroundFit : UIBehaviour, ILayoutSelfController
    {
        /// <summary>Keep 없음 (폭 · 높이 0)</summary>
        public static readonly Rect NoKeep = new(0f, 0f, 0f, 0f);

        [SerializeField] BackgroundFitMode _mode = BackgroundFitMode.Cover;
        [Tooltip("그림의 이 uv 점이 부모의 같은 상대 위치에 온다 (Cover = 자르는 자리, Contain = 놓는 자리)")]
        [SerializeField] Vector2 _focus = new(0.5f, 0.5f);
        [Tooltip("꼭 남길 그림 영역 (uv, 왼쪽 아래 = 0). 폭 · 높이가 0이면 없음 — Cover에서만 쓴다")]
        [SerializeField] Rect _keep = new(0f, 0f, 0f, 0f);
        [Tooltip("그림이 없을 때 쓸 비율 (가로/세로). 0이면 그림이 없을 때 부모 그대로")]
        [SerializeField] float _fallbackAspect;
        [Tooltip("그림이 부모를 다 덮지 못할 때만 켜는 뒤판 (선택)")]
        [SerializeField] Graphic _backing;

        RectTransform _rect;
        Graphic _graphic;
        DrivenRectTransformTracker _tracker;
        UnityEngine.Events.UnityAction _onGraphicDirty;
        float _appliedAspect = -1f;
        bool _fitting;

        public BackgroundFitMode Mode => _mode;
        public Vector2 Focus => _focus;
        public Rect Keep => _keep;
        public float FallbackAspect => _fallbackAspect;
        public Graphic Backing => _backing;
        /// <summary>마지막으로 맞춘 그림 비율 (가로/세로, 아직 안 맞췄으면 −1)</summary>
        public float AppliedAspect => _appliedAspect;

        RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>빌더 · 코드에서 한 번에 설정하고 바로 맞춘다</summary>
        public BackgroundFit Configure(BackgroundFitMode mode, Vector2 focus, float fallbackAspect = 0f, Graphic backing = null)
        {
            _mode = mode;
            _focus = new Vector2(Mathf.Clamp01(focus.x), Mathf.Clamp01(focus.y));
            _fallbackAspect = Mathf.Max(0f, fallbackAspect);
            _backing = backing;
            Fit();
            return this;
        }

        /// <summary>꼭 남길 그림 영역 (uv). NoKeep이면 없음</summary>
        public BackgroundFit SetKeep(Rect keepUv)
        {
            _keep = keepUv;
            Fit();
            return this;
        }

        /// <summary>자르는 · 놓는 자리만 바꾼다</summary>
        public void SetFocus(Vector2 focus)
        {
            var clamped = new Vector2(Mathf.Clamp01(focus.x), Mathf.Clamp01(focus.y));
            if (clamped == _focus) return;
            _focus = clamped;
            Fit();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Hook();
            Fit();
        }

        protected override void OnDisable()
        {
            Unhook();
            _tracker.Clear();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            Fit();
        }

        protected override void OnDidApplyAnimationProperties() => Fit();

        public void SetLayoutHorizontal() => Fit();

        public void SetLayoutVertical()
        {
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _focus = new Vector2(Mathf.Clamp01(_focus.x), Mathf.Clamp01(_focus.y));
            _fallbackAspect = Mathf.Max(0f, _fallbackAspect);
            // OnValidate 안에서는 RectTransform을 바꾸지 않는다 (SendMessage 경고) — 다음 에디터 틱에 맞춘다
            UnityEditor.EditorApplication.delayCall += DelayedFit;
        }

        void DelayedFit()
        {
            if (this != null) Fit();
        }
#endif

        void Hook()
        {
            if (_graphic == null) TryGetComponent(out _graphic);
            if (_graphic == null) return;
            _onGraphicDirty ??= OnGraphicDirty;
            // Image.sprite 교체 → SetLayoutDirty (크기가 같은 그림이면 건너뛴다 — 크기가 같으면 비율도 같다)
            _graphic.RegisterDirtyLayoutCallback(_onGraphicDirty);
            // RawImage.texture 교체는 레이아웃을 더럽히지 않고 재질만 더럽힌다
            if (_graphic is RawImage) _graphic.RegisterDirtyMaterialCallback(_onGraphicDirty);
        }

        void Unhook()
        {
            if (_graphic == null || _onGraphicDirty == null) return;
            _graphic.UnregisterDirtyLayoutCallback(_onGraphicDirty);
            if (_graphic is RawImage) _graphic.UnregisterDirtyMaterialCallback(_onGraphicDirty);
        }

        void OnGraphicDirty()
        {
            if (!Mathf.Approximately(ReadAspect(), _appliedAspect)) Fit();
        }

        /// <summary>지금 그림의 비율 (가로/세로). 그림이 없으면 FallbackAspect (0 = 모름)</summary>
        public float ReadAspect()
        {
            if (_graphic == null) TryGetComponent(out _graphic);
            if (_graphic is Image image && image.sprite != null)
            {
                var r = image.sprite.rect;
                if (r.width > 0f && r.height > 0f) return r.width / r.height;
            }
            else if (_graphic is RawImage raw && raw.texture != null)
            {
                var uv = raw.uvRect;
                float w = raw.texture.width * Mathf.Abs(uv.width);
                float h = raw.texture.height * Mathf.Abs(uv.height);
                if (w > 0f && h > 0f) return w / h;
            }
            return _fallbackAspect;
        }

        /// <summary>지금 크기로 다시 맞춘다 (값이 같으면 아무것도 쓰지 않는다)</summary>
        public void Fit()
        {
            if (_fitting || !IsActive()) return;
            var rt = Rect;
            if (!(rt.parent is RectTransform parent)) return;
            var size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float aspect = ReadAspect();
            _appliedAspect = aspect;
            _fitting = true;
            try
            {
                _tracker.Clear();
                _tracker.Add(this, rt, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
                var fit = Compute(size, aspect, _mode, _focus, _keep);
                // 앵커 (0,0)~(1,1): 오프셋 = 맞춘 사각형 가장자리 − 부모 가장자리 (피벗과 무관)
                var offsetMin = fit.min;
                var offsetMax = fit.max - size;
                if (rt.anchorMin != Vector2.zero) rt.anchorMin = Vector2.zero;
                if (rt.anchorMax != Vector2.one) rt.anchorMax = Vector2.one;
                if (!Near(rt.offsetMin, offsetMin)) rt.offsetMin = offsetMin;
                if (!Near(rt.offsetMax, offsetMax)) rt.offsetMax = offsetMax;
                if (_backing != null)
                {
                    bool show = !Covers(fit, size);
                    if (_backing.enabled != show) _backing.enabled = show;
                }
            }
            finally
            {
                _fitting = false;
            }
        }

        static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;

        // ───────────── 순수 계산 (BackgroundFitTests) ─────────────

        /// <summary>
        /// 부모(크기 parent, 왼쪽 아래 = 0) 안에서 그림(비율 aspect = 가로/세로)이 차지할 사각형. 언제나 가로/세로 = aspect (늘림 없음) —
        /// Fill이거나 비율을 모르면(≤ 0) 부모 그대로.
        /// Cover: 짧은 쪽을 맞춰 덮고 Focus로 자른다. Keep(uv)이 있으면 Keep이 부모 안에 다 들어가도록 배율을 낮추고(필요할 때만)
        /// 덮는 범위 안에서 Keep이 부모 안에 오도록 민다. Contain: 긴 쪽을 맞춰 안에 넣고 Focus로 놓는다.
        /// </summary>
        public static Rect Compute(Vector2 parent, float aspect, BackgroundFitMode mode, Vector2 focus, Rect keep)
        {
            if (mode == BackgroundFitMode.Fill || aspect <= 0f || parent.x <= 0f || parent.y <= 0f) return new Rect(Vector2.zero, parent);
            bool artWider = aspect > parent.x / parent.y;
            bool cover = mode == BackgroundFitMode.Cover;
            // Cover: 그림이 더 넓으면 높이를 맞추고(옆이 넘침), 아니면 폭을 맞춘다. Contain은 반대
            float height = cover == artWider ? parent.y : parent.x / aspect;
            bool hasKeep = cover && HasKeep(keep);
            if (hasKeep) height = Mathf.Min(height, Mathf.Min(parent.y / keep.height, parent.x / (keep.width * aspect)));
            var size = new Vector2(height * aspect, height);
            float x = Place(parent.x, size.x, focus.x, hasKeep, keep.xMin, keep.xMax);
            float y = Place(parent.y, size.y, focus.y, hasKeep, keep.yMin, keep.yMax);
            return new Rect(x, y, size.x, size.y);
        }

        /// <summary>Keep이 쓸 만한 영역인지 (폭 · 높이 &gt; 0)</summary>
        public static bool HasKeep(Rect keep) => keep.width > 0f && keep.height > 0f;

        /// <summary>
        /// 한 축의 자리: Focus 자리 (p − s)·focus. Keep이 있고 그림이 이 축을 덮으면(s ≥ p) 덮는 범위 [p − s, 0]과
        /// Keep이 부모 안에 드는 범위 [−k0·s, p − k1·s]가 겹치는 곳으로 민다 (Keep 크기 ≤ p라 언제나 겹친다).
        /// </summary>
        static float Place(float p, float s, float focus, bool hasKeep, float k0, float k1)
        {
            float at = (p - s) * Mathf.Clamp01(focus);
            if (!hasKeep || s < p) return at;
            float lo = Mathf.Max(p - s, -k0 * s);
            float hi = Mathf.Min(0f, p - k1 * s);
            return lo <= hi ? Mathf.Clamp(at, lo, hi) : at;
        }

        /// <summary>맞춘 사각형이 부모(0,0 ~ parent)를 다 덮는지 (tolerance px 안)</summary>
        public static bool Covers(Rect fit, Vector2 parent, float tolerance = 0.5f) =>
            fit.xMin <= tolerance && fit.yMin <= tolerance && fit.xMax >= parent.x - tolerance && fit.yMax >= parent.y - tolerance;
    }
}
