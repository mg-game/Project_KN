using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 튜토리얼 말풍선: 크림색 동화책 카드(너비 720) + 왼쪽 친구 얼굴(기본 짝짝이) + 이름표 + 글 (글 영역 너비 496 — 30이면 한 줄 17자 안팎).
    ///   · 글은 30 고정(자동 크기 없음, 줄 간격 4)이고 카드 높이가 글에 맞춘다 (M42): 높이 = 글 높이 + 56을 150~220으로 (CardHeight).
    ///     FTUE 글은 대개 2줄(150), 긴 글은 3~4줄까지 감긴다. 220 안에 다 들어가지 않는 긴 글(30에서 4줄 넘게)만 글자를 2씩 줄여 본다
    ///     (24까지 — 카드 밖으로 넘치지 않게). 그러니 글은 24~30, 최대 4~5줄.
    ///   · 얼굴(접시 PortraitBg · 얼굴 그림)은 196 높이 카드에 맞춰 놓여 있다 (FaceDesignHeight): 더 낮은 카드는 카드 왼쪽 가운데를 기준으로
    ///     자리와 크기를 같은 비율로 줄인다 (FitFace — 150 카드에서 접시가 위 변을 넘거나 얼굴이 이름표 위로 솟지 않게). 높이가 바뀔 때만.
    ///   · 글은 한 글자씩 나타난다 (초당 60자). 말풍선을 누르면 바로 다 보인다.
    ///   · 꼬리(ui_bubble_tail, 아래를 가리키는 그림, 피벗 = 위쪽 가운데)는 대상 쪽 변(위/아래)에 붙어 변을 살짝 덮고,
    ///     대상 x를 따라간다. 대상이 위에 있으면 위아래로 뒤집는다. 대상이 없으면 꼬리를 숨긴다.
    ///   · 나타날 때 '뽀'(UISfxKey.CoachPop) 하며 톡 튀고, 머무는 동안 살짝 둥실거린다 (움직임 줄이기면 튐 · 둥실 없이 알파만). Update는 할당 없음.
    /// </summary>
    public class SpeechBubbleView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _text;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Image _portrait;
        [Tooltip("얼굴 뒤 접시 (카드의 PortraitBg). 비어 있으면(이 필드 전에 빌드한 장면) 카드에서 그 이름의 자식을 찾는다")]
        [SerializeField] RectTransform _portraitPlate;
        [SerializeField] RectTransform _tail;
        [Tooltip("꼬리가 카드 변 안쪽으로 겹치는 깊이 (변의 금테를 덮는다)")]
        [SerializeField] float _tailInset = 6f;
        [SerializeField] float _charsPerSecond = 60f;

        /// <summary>카드 높이 = 글 높이 + TextPad를 MinHeight~MaxHeight로 (M42 — 글 위 30 · 아래 22 여백 + 4). 빌더도 쓴다</summary>
        public const float MinHeight = 150f;
        public const float MaxHeight = 220f;
        public const float TextPad = 56f;
        /// <summary>MaxHeight에 다 들어가지 않는 글만 이 크기까지 2씩 줄인다 (보통 글은 빌더의 30 그대로)</summary>
        public const float MinFontSize = 24f;
        /// <summary>
        /// 얼굴(접시 146 · 얼굴 그림 176, 카드 왼쪽 가운데 기준 — 빌더 BuildBubble) 자리를 맞춘 카드 높이.
        /// 이보다 낮은 카드는 얼굴을 높이 비율로 줄이고(FaceScale), 높은 카드(~220)는 그대로 둔다. 빌더도 쓴다
        /// </summary>
        public const float FaceDesignHeight = 196f;
        /// <summary>_portraitPlate가 비었을 때 카드에서 찾는 접시 이름 (빌더와 같다)</summary>
        const string PlateName = "PortraitBg";
        /// <summary>나타날 때 0.85 → 1 튐 (OutBack)</summary>
        const float PopTime = 0.22f;

        float _time;
        float _visibleChars;
        int _totalChars;
        bool _shown;
        float _alphaTarget;
        Vector2 _position;
        /// <summary>빌더가 정한 글자 크기 (30) — 긴 글을 줄였다가 다음 글에서 되돌린다</summary>
        float _baseFontSize;
        /// <summary>빌더가 놓은 얼굴 그림 · 접시 자리 (196 카드 기준 anchoredPosition — FitFace가 비율로 줄인다)</summary>
        Vector2 _portraitBase;
        Vector2 _plateBase;
        /// <summary>지금 얼굴 비율 (장면에 저장된 값은 1 — 빌더 자리 그대로)</summary>
        float _faceScale = 1f;

        /// <summary>말풍선이 떠 있음 (사라지는 중 포함)</summary>
        public bool IsShown => _shown;

        /// <summary>말풍선 카드 크기 (배치 계산용 — 글을 바꾸면 Say 안에서 높이가 바로 바뀐다)</summary>
        public Vector2 Size => _root.rect.size;

        void Awake()
        {
            if (_root == null) _root = (RectTransform)transform;
            if (_group != null) _group.alpha = 0f;
            if (_text != null)
            {
                // 높이를 글에 맞추므로 자동 크기는 쓰지 않는다 (켜 두면 글 높이가 다시 글자 크기를 바꾼다 — 다시 빌드하기 전 장면도 같게)
                if (_text.enableAutoSizing)
                {
                    _text.enableAutoSizing = false;
                    _text.fontSize = Mathf.Max(_text.fontSize, _text.fontSizeMax);
                }
                _baseFontSize = _text.fontSize;
            }
            if (_portraitPlate == null) _portraitPlate = _root.Find(PlateName) as RectTransform;
            if (_portrait != null) _portraitBase = _portrait.rectTransform.anchoredPosition;
            if (_portraitPlate != null) _plateBase = _portraitPlate.anchoredPosition;
            _root.gameObject.SetActive(false);
        }

        /// <summary>얼굴 비율 (M42): 카드 높이 / 196, 1을 넘지 않는다 — 150 카드면 약 0.77 (테스트용 순수 함수)</summary>
        internal static float FaceScale(float cardHeight) => Mathf.Clamp01(cardHeight / FaceDesignHeight);

        /// <summary>
        /// 얼굴(얼굴 그림 · 접시)을 카드 높이에 맞춘다: 자리(anchoredPosition — 카드 왼쪽 가운데 기준)와 크기(localScale — 피벗도 왼쪽 가운데)를
        /// 같은 비율로 줄여, 196 카드의 얼굴 모양이 그대로 작아진다. 150 카드에서도 접시는 카드 안, 얼굴 머리는 위 변 위로 몇 px만 나온다
        /// (줄이지 않으면 접시가 위 변을 2px 넘고 얼굴이 29px 솟아 이름표 · 위쪽 꼬리 자리를 덮었다). 높이가 바뀔 때만 (FitToText).
        /// </summary>
        void FitFace(float height)
        {
            float k = FaceScale(height);
            if (Mathf.Approximately(k, _faceScale)) return;
            _faceScale = k;
            var scale = new Vector3(k, k, 1f);
            if (_portrait != null)
            {
                var rt = _portrait.rectTransform;
                rt.localScale = scale;
                rt.anchoredPosition = _portraitBase * k;
            }
            if (_portraitPlate != null)
            {
                _portraitPlate.localScale = scale;
                _portraitPlate.anchoredPosition = _plateBase * k;
            }
        }

        /// <summary>카드 높이 (M42): 글 높이 + 56을 150~220으로 자른다 (테스트용 순수 함수)</summary>
        internal static float CardHeight(float preferredTextHeight) =>
            Mathf.Clamp(preferredTextHeight + TextPad, MinHeight, MaxHeight);

        /// <summary>
        /// 글에 맞춰 카드 높이를 정한다 (글이 바뀔 때만 — GetPreferredValues는 매 프레임 부르지 않는다).
        /// 글 영역 너비는 그대로라 카드 높이만 바꾼다 (글 영역은 카드에 늘여 붙어 같이 커진다). 꼬리 자리는 다음 Place가 새 높이로 맞춘다.
        /// </summary>
        void FitToText(string message)
        {
            if (_text == null || _baseFontSize <= 0f) return;
            float width = _text.rectTransform.rect.width;
            if (!(width > 1f)) return;
            float size = _baseFontSize;
            _text.fontSize = size;
            float preferred = _text.GetPreferredValues(message, width, 0f).y;
            while (preferred + TextPad > MaxHeight && size - 2f >= MinFontSize)
            {
                size -= 2f;
                _text.fontSize = size;
                preferred = _text.GetPreferredValues(message, width, 0f).y;
            }
            var card = _root.sizeDelta;
            float height = CardHeight(preferred);
            if (!Mathf.Approximately(card.y, height)) _root.sizeDelta = new Vector2(card.x, height);
            FitFace(height);
        }

        /// <summary>말하는 친구 (얼굴, 이름)</summary>
        public void SetSpeaker(Sprite portrait, string speakerName)
        {
            if (_portrait != null)
            {
                _portrait.sprite = portrait;
                _portrait.enabled = portrait != null;
            }
            if (_nameText != null) _nameText.text = speakerName;
        }

        /// <summary>글을 바꾸고 다시 타자 연출 (같은 글이면 그대로)</summary>
        public void Say(string message, bool instant = false)
        {
            message ??= string.Empty;
            bool appearing = !_shown;
            if (appearing)
            {
                // 먼저 켠다: 꺼진 TMP는 ForceMeshUpdate · 글 크기 재기를 건너뛰어 글자 수가 옛 값(처음엔 0)으로 남는다 —
                // 그러면 타자 연출이 0에 멈춰 빈 카드만 보였다 (M42 03 화면)
                _shown = true;
                _root.gameObject.SetActive(true);
                _time = 0f;
                UISfx.Play(UISfxKey.CoachPop);
            }
            if (appearing || _text.text != message)
            {
                // 처음 뜰 때도 맞춘다 (장면에 저장된 높이는 빌드 때 값)
                FitToText(message);
                _text.text = message;
                _text.ForceMeshUpdate(true);
                _totalChars = _text.textInfo.characterCount;
                _visibleChars = instant ? _totalChars : 0f;
                _text.maxVisibleCharacters = instant ? _totalChars : 0;
                _time = 0f;
            }
            else if (instant && _visibleChars < _totalChars)
            {
                // 같은 글을 바로 다 보여 달라는 요청 (막힌 곳을 누름 — CoachMarkView). 예전에는 글이 같으면 무시됐다
                _visibleChars = _totalChars;
                _text.maxVisibleCharacters = _totalChars;
            }
            _alphaTarget = 1f;
        }

        /// <summary>천천히 사라진다</summary>
        public void Hide() => _alphaTarget = 0f;

        /// <summary>바로 숨긴다</summary>
        public void HideNow()
        {
            _alphaTarget = 0f;
            _shown = false;
            if (_group != null) _group.alpha = 0f;
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 놓을 자리 (부모 로컬, 말풍선 중심). tailUp이면 꼬리가 위쪽 변에 붙어 위의 대상을 가리킨다.
        /// tailX = 말풍선 중심 기준 꼬리 x (NaN이면 꼬리 숨김)
        /// </summary>
        public void Place(Vector2 center, bool tailUp, float tailX)
        {
            _position = center;
            if (_tail == null) return;
            bool showTail = !float.IsNaN(tailX);
            if (_tail.gameObject.activeSelf != showTail) _tail.gameObject.SetActive(showTail);
            if (!showTail) return;
            float halfHeight = _root.rect.height * 0.5f;
            float halfWidth = _root.rect.width * 0.5f;
            _tail.anchoredPosition = new Vector2(Mathf.Clamp(tailX, -halfWidth + 48f, halfWidth - 48f),
                tailUp ? halfHeight - _tailInset : -halfHeight + _tailInset);
            // 그림은 아래를 가리킨다: 위쪽 변에 붙을 때는 뒤집는다
            var scale = _tail.localScale;
            float flip = tailUp ? -1f : 1f;
            if (!Mathf.Approximately(scale.y, flip)) _tail.localScale = new Vector3(scale.x, flip, scale.z);
        }

        /// <summary>누르면 남은 글을 바로 보여 준다</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_visibleChars < _totalChars)
            {
                _visibleChars = _totalChars;
                _text.maxVisibleCharacters = _totalChars;
            }
        }

        void Update()
        {
            if (!_shown) return;
            float dt = Time.unscaledDeltaTime;
            _time += dt;

            if (_group != null)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, _alphaTarget, dt / 0.16f);
                if (_alphaTarget <= 0f && _group.alpha <= 0f)
                {
                    HideNow();
                    return;
                }
            }

            if (_visibleChars < _totalChars)
            {
                _visibleChars = Mathf.Min(_totalChars, _visibleChars + _charsPerSecond * dt);
                _text.maxVisibleCharacters = (int)_visibleChars;
            }

            // 움직임 줄이기: 튐 · 둥실 없이 제자리 (알파만 바뀐다)
            bool reduced = UIMotion.Reduced;
            float pop = reduced || _time >= PopTime ? 1f : UIMotion.OutBack(_time / PopTime);
            _root.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, pop);
            _root.anchoredPosition = reduced ? _position : _position + new Vector2(0f, 3f * Mathf.Sin(_time * 2.4f));
        }
    }
}
