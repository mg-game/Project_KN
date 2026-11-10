using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 기준 해상도보다 길쭉한 폰은 가로 기준, 태블릿처럼 넓은 화면은 세로 기준으로 맞춘다.
    /// <para>
    /// 세로 놀이 틀 (aspect AF-2, PlayFrame): 화면이 9:16보다 넓으면 캔버스 직속 층 중 가로로 꽉 찬 층(BattleScreenFlash · Popups ·
    /// MetaUI · TutorialOverlay · FanfareLayer · SystemPopups …)의 가로 앵커를 가운데 기둥으로 좁히고, 안전 영역 층(SafeArea · TooltipLayer ·
    /// ToastLayer)은 SafeArea가 스스로 기둥을 따른다 → 틀 안은 캔버스 단위 1080×1920, 폰과 똑같은 배치. 양옆은 PlayFrameSides(맨 위 형제)가 덮는다.
    /// 크기를 스스로 정하는 층(Background의 비율 덮개)은 건드리지 않는다 — 화면 전체 그대로, 틀 안에서는 전투 · 바들에 가려 보이지 않는다.
    /// 부모를 바꾸지 않으므로 "SafeArea/…" · "Popups/…" 경로는 그대로다. 9:16 이하(폰)는 앵커 0~1 그대로 · 양옆은 만들지도 않는다.
    /// </para>
    /// 실행 순서 −1000: CanvasScaler · SafeArea · 전투 매퍼보다 먼저 같은 프레임에 맞춘다 (창 크기 바꿈 · 접기 직후 한 프레임 어긋남 없음).
    /// 매 프레임 일은 예전처럼 비율 비교 하나 (+ 틀 최대 비율 비교 하나).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(CanvasScaler))]
    public class CanvasScalerAdapter : MonoBehaviour
    {
        [Tooltip("넓은 화면 양옆 그림 (비면 캔버스 직속 Background의 그림)")]
        [SerializeField] Sprite _sideArt;

        CanvasScaler _scaler;
        float _appliedAspect;
        float _appliedMaxAspect;
        PlayFrameSides _sides;
        /// <summary>틀에 묶은 캔버스 직속 층 (처음에 가로로 꽉 찼던 층 — 다시 넓어지면 0~1로 되돌린다)</summary>
        readonly List<RectTransform> _framed = new(12);

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update()
        {
            if (!Mathf.Approximately(_appliedAspect, CurrentAspect) || _appliedMaxAspect != PlayFrame.MaxAspect) Apply();
        }

        void OnDestroy()
        {
            // 장면을 내리면 다음 장면의 SafeArea가 지난 틀을 따르지 않게
            PlayFrame.SetApplied(PlayFrame.FullColumn);
        }

        static float CurrentAspect => Screen.height > 0 ? (float)Screen.width / Screen.height : 0f;

        void Apply()
        {
            _appliedAspect = CurrentAspect;
            _appliedMaxAspect = PlayFrame.MaxAspect;
            var reference = _scaler.referenceResolution;
            float referenceAspect = reference.x / reference.y;
            _scaler.matchWidthOrHeight = _appliedAspect > referenceAspect ? 1f : 0f;

            var column = PlayFrame.ColumnFor(Screen.width, Screen.height, PlayFrame.MaxAspect);
            FrameLayers(column);
            ApplySides(column);
            PlayFrame.SetApplied(column);
        }

        /// <summary>캔버스 직속 층의 가로 앵커를 기둥에 묶는다 (안전 영역 층 · 양옆은 빼고). 할당 없음 (목록은 처음 본 층만 한 번 더한다)</summary>
        void FrameLayers(Vector2 column)
        {
            var root = transform;
            for (int i = 0; i < root.childCount; i++)
            {
                if (!(root.GetChild(i) is RectTransform layer)) continue;
                if (_sides != null && layer == _sides.transform) continue;
                if (!_framed.Contains(layer))
                {
                    // 처음 보는 층: 가로로 꽉 찬 층만 묶는다. 안전 영역 층은 SafeArea가 기둥 ∩ 안전 영역으로 스스로 맞추고,
                    // 크기를 스스로 정하는 층(Background — 비율 덮개 AspectRatioFitter/BackgroundFit)은 화면 전체에 둔다 (양옆 그림 밑, 안 보인다)
                    if (layer.anchorMin.x != 0f || layer.anchorMax.x != 1f) continue;
                    if (layer.TryGetComponent<SafeArea>(out _) || layer.TryGetComponent<ILayoutSelfController>(out _)) continue;
                    _framed.Add(layer);
                }
                var min = layer.anchorMin;
                var max = layer.anchorMax;
                if (min.x == column.x && max.x == column.y) continue;
                min.x = column.x;
                max.x = column.y;
                layer.anchorMin = min;
                layer.anchorMax = max;
            }
        }

        void ApplySides(Vector2 column)
        {
            bool framed = column.x > 0f || column.y < 1f;
            if (!framed)
            {
                if (_sides != null && _sides.gameObject.activeSelf) _sides.gameObject.SetActive(false);
                return;
            }
            if (_sides == null)
            {
                var existing = transform.Find(PlayFrameSides.ObjectName);
                _sides = existing != null && existing.TryGetComponent<PlayFrameSides>(out var found)
                    ? found
                    : PlayFrameSides.Create((RectTransform)transform, SideArt());
            }
            // 놓고 나서 켠다 (켜질 때 그림 맞춤이 바른 크기로 한 번에 된다). 언제나 맨 위 형제
            _sides.Apply(column, Screen.width);
            if (_sides.transform.GetSiblingIndex() != transform.childCount - 1) _sides.transform.SetAsLastSibling();
            if (!_sides.gameObject.activeSelf) _sides.gameObject.SetActive(true);
        }

        Sprite SideArt()
        {
            if (_sideArt != null) return _sideArt;
            var background = transform.Find("Background");
            return background != null && background.TryGetComponent<Image>(out var image) ? image.sprite : null;
        }
    }
}
