using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 작은 안내 말풍선 (M59) — 아이템이 아닌 것(재화 칩 · 특성 칩 · 시너지 아이콘)의 설명. 아이템은 ItemInfoPopup을 쓴다.
    /// 씬에 하나: TooltipLayer(팝업 위, 튜토리얼 덮개 아래, 안전 영역) 아래 "InfoTooltip" — SceneParts.BuildInfoTooltip이 만든다.
    ///   · 모양: 폭 360~520 (글에 맞춤), 안쪽 여백 20/16, 밤 패널 그림(Panel@0.96 + 금테), 대상 쪽 16px 꺾쇠.
    ///     제목 Cream 26 굵게 / 본문 SubText 22 (3줄까지, 넘치면 …) / 얻는 곳 Lavender 20 (없으면 숨김).
    ///   · 자리: 대상 위, 안전 영역 위 끝에서 24px 안쪽에 걸리면 아래로 뒤집는다. 좌우는 안전 영역 안 16px 여백으로 자른다.
    ///   · 닫기: 3초 뒤, 또는 아무 곳이나 누르면 (열려 있는 동안만 켜지는 화면 전체 투명 판 Catcher가 그 누름을 받는다).
    ///   · 누르기 판정(누름 / 길게 누름)은 대상 쪽이 한다 (CurrencyStripView 등) — 이 뷰는 Show / Hide만.
    /// 열려 있거나 사라지는 동안만 Update가 켜진다. 글자 배치는 Show 때 한 번 (매 프레임 계산 없음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfoTooltipView : MonoBehaviour
    {
        [Tooltip("말풍선 판 (가운데 기준, 크기는 Show가 정한다)")]
        [SerializeField] RectTransform _panel;
        [Tooltip("판 페이드 (raycast 막지 않음)")]
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _body;
        [SerializeField] TMP_Text _source;
        [Tooltip("대상을 가리키는 꺾쇠 (판의 자식, 오른쪽을 가리키는 그림 — 돌려서 쓴다)")]
        [SerializeField] RectTransform _arrow;
        [Tooltip("열려 있는 동안만 켜지는 화면 전체 투명 판 (누르면 닫힘)")]
        [SerializeField] Button _catcher;

        /// <summary>본문 최대 줄 수</summary>
        const int BodyMaxLines = 3;
        /// <summary>제목 · 본문 · 얻는 곳 사이 간격</summary>
        const float LineGap = 6f;
        /// <summary>꺾쇠가 판 테두리에 겹치는 양</summary>
        const float ArrowOverlap = 3f;
        /// <summary>꺾쇠가 판 모서리에서 떨어지는 최소 거리</summary>
        const float ArrowCornerInset = 26f;
        /// <summary>글자 크기 재기에 쓰는 '제한 없음' (TMP가 쓰는 큰 값과 같다)</summary>
        const float Unbounded = 32767f;

        static InfoTooltipView s_instance;
        static readonly Vector3[] Corners = new Vector3[4];

        RectTransform _root;
        bool _open;
        float _hideAt;
        float _alpha;

        /// <summary>말풍선이 열려 있는지</summary>
        public static bool IsOpen => s_instance != null && s_instance._open;

        /// <summary>씬에 말풍선이 있는지 (없으면 Show는 아무것도 하지 않는다)</summary>
        public static bool IsAvailable => s_instance != null;

        /// <summary>
        /// anchor 위(자리가 없으면 아래)에 말풍선을 띄운다. 이미 열려 있으면 내용과 자리를 바꾸고 3초를 다시 센다.
        /// 말풍선이 씬에 없거나 제목 · 본문이 모두 비었으면 false.
        /// </summary>
        public static bool Show(RectTransform anchor, string title, string body, string source = null)
        {
            var self = s_instance;
            if (self == null || anchor == null) return false;
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return false;
            self.Open(anchor, title, body, source);
            return true;
        }

        /// <summary>말풍선을 닫는다 (짧게 사라진다)</summary>
        public static void Hide()
        {
            if (s_instance != null) s_instance.Close(false);
        }

        void Awake()
        {
            s_instance = this;
            _root = (RectTransform)transform;
            if (_catcher != null) _catcher.onClick.AddListener(Hide);
            Close(true);
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (_catcher != null) _catcher.onClick.RemoveListener(Hide);
        }

        void OnDisable() => Close(true);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_open && Time.unscaledTime >= _hideAt) _open = false;

            float target = _open ? 1f : 0f;
            if (UIMotion.Reduced) _alpha = target;
            else
            {
                float duration = _open ? UIMotion.TooltipFadeIn : UIMotion.TooltipFadeOut;
                _alpha = Mathf.MoveTowards(_alpha, target, dt / Mathf.Max(0.01f, duration));
            }
            if (_group != null) _group.alpha = _open ? UIMotion.OutCubic(_alpha) : _alpha;

            if (!_open && _alpha <= 0f) Close(true);
        }

        void Open(RectTransform anchor, string title, string body, string source)
        {
            if (_panel == null) return;
            // 글자 크기를 재기 전에 판을 켠다 (한 번도 켜진 적 없는 TMP는 아직 준비 전) — 알파 0이라 깜빡이지 않는다
            if (!_panel.gameObject.activeSelf)
            {
                _alpha = 0f;
                if (_group != null) _group.alpha = 0f;
                _panel.gameObject.SetActive(true);
            }
            SetText(_title, title);
            SetText(_body, body);
            SetText(_source, source);

            Layout(out var size);
            Place(anchor, size);

            if (_catcher != null) _catcher.gameObject.SetActive(true);
            _open = true;
            _hideAt = Time.unscaledTime + UIMotion.TooltipAutoHide;
            enabled = true;
            if (UIMotion.Reduced)
            {
                _alpha = 1f;
                if (_group != null) _group.alpha = 1f;
            }
        }

        /// <summary>닫기. immediate면 바로 숨기고 Update를 끈다</summary>
        void Close(bool immediate)
        {
            _open = false;
            if (_catcher != null && _catcher.gameObject.activeSelf) _catcher.gameObject.SetActive(false);
            if (!immediate && _panel != null && _panel.gameObject.activeSelf && !UIMotion.Reduced)
            {
                enabled = true;
                return;
            }
            _alpha = 0f;
            if (_group != null) _group.alpha = 0f;
            if (_panel != null && _panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
            enabled = false;
        }

        static void SetText(TMP_Text label, string text)
        {
            if (label == null) return;
            bool on = !string.IsNullOrEmpty(text);
            if (label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
            if (on) label.text = text;
        }

        /// <summary>글에 맞춰 판 크기를 정하고 글자 칸을 위에서부터 쌓는다</summary>
        void Layout(out Vector2 size)
        {
            float padX = UISize.TooltipPadX;
            float padY = UISize.TooltipPadY;
            float minContent = UISize.TooltipMinWidth - padX * 2f;
            float maxContent = UISize.TooltipMaxWidth - padX * 2f;

            float want = 0f;
            want = Mathf.Max(want, PreferredWidth(_title));
            want = Mathf.Max(want, PreferredWidth(_body));
            want = Mathf.Max(want, PreferredWidth(_source));
            float content = Mathf.Clamp(Mathf.Ceil(want) + 2f, minContent, maxContent);

            float y = padY;
            y = Stack(_title, y, content, 0);
            y = Stack(_body, y, content, BodyMaxLines);
            y = Stack(_source, y, content, 0);
            y += padY - LineGap;

            size = new Vector2(content + padX * 2f, Mathf.Max(y, padY * 2f + 24f));
            _panel.sizeDelta = size;
        }

        static float PreferredWidth(TMP_Text label)
        {
            if (label == null || !label.gameObject.activeSelf) return 0f;
            return label.GetPreferredValues(label.text, Unbounded, Unbounded).x;
        }

        /// <summary>글자 하나를 top 아래에 놓고 다음 top을 돌려준다 (maxLines가 있으면 그 줄 수에서 자른다)</summary>
        static float Stack(TMP_Text label, float top, float width, int maxLines)
        {
            if (label == null || !label.gameObject.activeSelf) return top;
            float height = label.GetPreferredValues(label.text, width, Unbounded).y;
            if (maxLines > 0)
            {
                float lineHeight = label.fontSize * LineHeightEm(label);
                height = Mathf.Min(height, lineHeight * maxLines + 2f);
            }
            height = Mathf.Ceil(height);
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(UISize.TooltipPadX, -top - height);
            rt.offsetMax = new Vector2(-UISize.TooltipPadX, -top);
            return top + height + LineGap;
        }

        /// <summary>글꼴 줄 높이 (em, 글꼴 정보가 없으면 Noto Sans KR 값 1.45)</summary>
        static float LineHeightEm(TMP_Text label)
        {
            var font = label.font;
            if (font == null || font.faceInfo.pointSize <= 0) return 1.45f;
            return font.faceInfo.lineHeight / font.faceInfo.pointSize;
        }

        /// <summary>대상 위(또는 아래)에 판과 꺾쇠를 놓는다 (이 뷰의 사각형 = 안전 영역 기준)</summary>
        void Place(RectTransform anchor, Vector2 size)
        {
            anchor.GetWorldCorners(Corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                var p = _root.InverseTransformPoint(Corners[i]);
                left = Mathf.Min(left, p.x);
                right = Mathf.Max(right, p.x);
                bottom = Mathf.Min(bottom, p.y);
                top = Mathf.Max(top, p.y);
            }

            var area = _root.rect;
            float arrow = UISize.TooltipArrow;
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            float margin = UISize.TooltipEdgeMargin;

            // 위에 놓을 자리가 안전 영역 위 끝 24px 안쪽까지 없으면 아래로
            bool above = top + arrow + size.y <= area.yMax - UISize.TooltipFlipMargin;
            float centerY = above ? top + arrow + halfH : bottom - arrow - halfH;
            centerY = Mathf.Clamp(centerY, area.yMin + margin + halfH, Mathf.Max(area.yMin + margin + halfH, area.yMax - margin - halfH));

            float anchorX = (left + right) * 0.5f;
            float minX = area.xMin + margin + halfW;
            float maxX = Mathf.Max(minX, area.xMax - margin - halfW);
            float centerX = Mathf.Clamp(anchorX, minX, maxX);

            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = new Vector2(centerX, centerY) - area.center;

            if (_arrow == null) return;
            float inset = Mathf.Min(ArrowCornerInset, halfW);
            float arrowX = Mathf.Clamp(anchorX - centerX, -halfW + inset, halfW - inset);
            float arrowY = above ? -halfH - arrow * 0.5f + ArrowOverlap : halfH + arrow * 0.5f - ArrowOverlap;
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            _arrow.anchoredPosition = new Vector2(arrowX, arrowY);
            // 꺾쇠 그림은 오른쪽을 가리킨다: 위에 뜨면 아래(-90°), 아래에 뜨면 위(+90°)
            _arrow.localRotation = Quaternion.Euler(0f, 0f, above ? -90f : 90f);
        }
    }
}
