using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 노치/홈 인디케이터 영역을 피하도록 RectTransform을 Screen.safeArea에 맞춘다.
    /// 세로 놀이 틀 (aspect AF-2): 대상 = Screen.safeArea ∩ PlayFrame 기둥이고, 부모가 화면 전체라고 가정하지 않는다 —
    /// 부모의 화면 위치(루트 캔버스까지 앵커를 거슬러 올라가 잰다, 오프셋이 있으면 모서리로 잰다) 기준 앵커로 바꿔 쓴다.
    /// 그래서 캔버스 직속(SafeArea · TooltipLayer · ToastLayer)도, 틀에 묶인 층 안의 것(StoryIntro/Safe · FirstMeeting/CoverSafe · SafeFrame)도 맞다.
    /// 틀이 없는 폰에서는 예전과 똑같이 safeArea / 화면. 안전 영역 · 화면 크기 · 틀(PlayFrame.Version)이 바뀔 때만 다시 맞춘다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        static readonly Vector3[] Corners = new Vector3[4];

        RectTransform _rect;
        Transform _root;
        Rect _appliedArea;
        Vector2Int _appliedScreen;
        int _appliedFrame = -1;

        void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (_appliedArea != Screen.safeArea || _appliedScreen.x != Screen.width || _appliedScreen.y != Screen.height
                || _appliedFrame != PlayFrame.Version)
            {
                Apply();
            }
        }

        void Apply()
        {
            var area = Screen.safeArea;
            _appliedArea = area;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
            _appliedFrame = PlayFrame.Version;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            if (!TryParentNormalized(out var parent)) parent = new Rect(0f, 0f, 1f, 1f);
            PlayFrame.SafeAnchors(area, Screen.width, Screen.height, PlayFrame.Column, parent, out var min, out var max);

            if (_rect.anchorMin != min) _rect.anchorMin = min;
            if (_rect.anchorMax != max) _rect.anchorMax = max;
            if (_rect.offsetMin != Vector2.zero) _rect.offsetMin = Vector2.zero;
            if (_rect.offsetMax != Vector2.zero) _rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 부모의 화면 정규화 사각형 (루트 캔버스 = 0~1). 루트까지 오프셋 없는 앵커만 있으면 앵커를 겹쳐 계산하고(레이아웃 시점과 무관),
        /// 오프셋이 있는 조상이 있으면 부모 모서리(오버레이 캔버스 = 화면 픽셀)로 잰다. 루트 캔버스 밖이면 false
        /// </summary>
        bool TryParentNormalized(out Rect parentNorm)
        {
            parentNorm = new Rect(0f, 0f, 1f, 1f);
            if (_root == null)
            {
                var canvas = GetComponentInParent<Canvas>(true);
                if (canvas == null) return false;
                _root = canvas.rootCanvas.transform;
            }
            var norm = new Rect(0f, 0f, 1f, 1f);
            var t = _rect.parent as RectTransform;
            while (t != null && t != _root)
            {
                if (t.offsetMin.sqrMagnitude > 0.0001f || t.offsetMax.sqrMagnitude > 0.0001f) return TryParentFromCorners(out parentNorm);
                norm = PlayFrame.Nest(t.anchorMin, t.anchorMax, norm);
                t = t.parent as RectTransform;
            }
            if (t == null) return false;
            parentNorm = norm;
            return true;
        }

        bool TryParentFromCorners(out Rect parentNorm)
        {
            parentNorm = new Rect(0f, 0f, 1f, 1f);
            if (!(_rect.parent is RectTransform parent) || !_root.TryGetComponent<Canvas>(out var canvas)
                || canvas.renderMode != RenderMode.ScreenSpaceOverlay || Screen.width <= 0 || Screen.height <= 0) return false;
            parent.GetWorldCorners(Corners);
            parentNorm = Rect.MinMaxRect(Corners[0].x / Screen.width, Corners[0].y / Screen.height,
                Corners[2].x / Screen.width, Corners[2].y / Screen.height);
            return parentNorm.width > 0f && parentNorm.height > 0f;
        }
    }
}
