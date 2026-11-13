using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 코치마크 (FTUE-01): 대상 RectTransform 자리에 구멍 뚫린 어둠(α0.6) + 금빛 테두리 + 손가락(톡톡/끌기) + 말풍선.
    ///   · Present: 튜토리얼 단계 안내. 대상이 없거나 꺼져 있으면 어둠·손가락 없이 말풍선만 빈 자리에 (M42 · UX-SPEC-IA §8.6, FreeBubbleCenter):
    ///       SHEET(시트 목표 상태가 열림) = 탭 화면(_freeAnchor = ContentArea)의 다 열린 위 끝에서 28 아래가 말풍선 위 변 (화면 y ≈998~1148).
    ///         시트가 올라오는 0.3초 동안에도 그 자리에서 기다린다 (말풍선이 시트에 실려 탭 바 위로 미끄러지지 않게).
    ///       MAIN(시트 닫힘 · 닫는 중) = 전투 영역(_stageAnchor = StageArea) 아래 끝에서 282 위가 말풍선 아래 변, 가운데 (화면 y 1468 —
    ///         가이드 퀘스트 줄 1492 바로 위). 전투 위쪽 띠(스테이지 · 권장 전투력 · AUTO)를 가리지 않는다.
    ///       두 기준 영역이 이어지지 않은 옛 장면(이 필드 전에 빌드)은 틀이 묶어 둔 UIFrame.SheetRect · StageRect를 쓴다 (같은 ContentArea · StageArea).
    ///     대상이 있으면 예전처럼 손가락 너머에 놓는다 (PlaceBubble). 말풍선 높이는 글에 맞춘다 (SpeechBubbleView, 150~220).
    ///     대상 밖 입력 막기는 blockInput일 때만 (구멍 안은 그대로 눌린다). 막힌 곳을 누르면 손가락이 툭 튄다.
    ///   · Focus: 어둠 없이 손가락만 몇 초 (UINav.FocusRequested — 가이드 퀘스트 이동 뒤 · 졸업 뒤 우편 안내). 끝나면 원래 안내로 돌아온다.
    ///     RetargetFocus는 남은 시간 그대로 대상만 바꾼다 (덮인 버튼 → 첫 만남 카드 버튼).
    ///     손가락이 처음 보이는 순간 '뽀'(UISfxKey.CoachPop) — 말풍선이 뜰 때는 말풍선이 같은 소리를 낸다.
    ///   · Suppressed: 튜토리얼 안내만 잠깐 숨긴다 (다른 팝업·인트로가 위에 있을 때, hintOnly 단계 — FtueDirector가 정한다).
    ///     손가락만 잠깐(Focus)은 따로 FocusSuppressed로만 숨긴다 — hintOnly 단계(원정 받기 등)가 오래 이어져도
    ///     가이드 퀘스트·일지 [가기] 뒤 손가락은 늘 보인다.
    ///   · 대상이 움직이면 매 프레임 따라간다 (어둠 메시는 구멍이 1px 이상 바뀔 때만 다시 만든다). Update는 할당 없음.
    ///   · 움직임 줄이기(UIMotion.Reduced): 구멍이 조여들지 않고(처음부터 대상 크기, 어둠은 알파로만), 금빛 테두리는 맥박 없이 멈춘다 (pulse 1).
    ///     손가락 톡톡 · 끌기는 안내 자체라 그대로 움직인다.
    /// 좌표는 이 오브젝트(화면 전체로 늘린 층) 로컬.
    /// </summary>
    public class CoachMarkView : MonoBehaviour
    {
        [SerializeField] CanvasGroup _group;
        [SerializeField] HoleDimGraphic _dim;
        [SerializeField] Image _highlight;
        [SerializeField] FingerPointerView _finger;
        [SerializeField] SpeechBubbleView _bubble;
        [Tooltip("말풍선이 벗어나지 않을 영역 (보통 SafeArea)")]
        [SerializeField] RectTransform _safeArea;

        [Header("모양")]
        [SerializeField] float _dimAlpha = 0.6f;
        [SerializeField] float _holePadding = 14f;
        [SerializeField] float _maxRadius = 30f;
        [SerializeField] float _irisTime = 0.35f;
        [Tooltip("손가락 길이 (말풍선을 손가락 너머에 놓는다)")]
        [SerializeField] float _fingerReach = 170f;

        [Header("대상 없는 말풍선 자리 (M42 · UX-SPEC-IA §8.6 — 빌더가 직접 잇는다, 경로 찾기 없음)")]
        [Tooltip("SHEET 기준: 탭 화면 영역 (ContentArea). 시트가 열려 있으면 그 위 끝에서 28 아래에 말풍선 위 변")]
        [SerializeField] RectTransform _freeAnchor;
        [Tooltip("MAIN 기준: 전투 영역 (StageArea). 그 아래 끝에서 282 위에 말풍선 아래 변 (가이드 퀘스트 줄 위)")]
        [SerializeField] RectTransform _stageAnchor;

        /// <summary>대상 없는 말풍선 — SHEET: 탭 화면 위 끝과 말풍선 위 변 사이 (M42, 화면 y 970 + 28 = 998)</summary>
        internal const float FreeSheetInset = 28f;
        /// <summary>대상 없는 말풍선 — MAIN: 전투 영역 아래 끝과 말풍선 아래 변 사이 (UX-SPEC-IA §8.6, 화면 y 1750 − 282 = 1468)</summary>
        internal const float FreeMainLift = 282f;
        /// <summary>말풍선이 안전 영역 가장자리에서 띄우는 거리 (대상 있는 자리 · 없는 자리 모두)</summary>
        internal const float BubbleEdgeMargin = 16f;

        /// <summary>손가락 모양</summary>
        public enum PointerStyle
        {
            None = 0,
            Tap = 1,
            Drag = 2,
        }

        RectTransform _root;
        static readonly Vector3[] Corners = new Vector3[4];

        // 안내 상태
        bool _active;
        string _message;
        RectTransform _target;
        RectTransform _dragTo;
        bool _blockInput;
        bool _useDim;
        PointerStyle _pointer;
        float _iris;

        // 손가락만 잠깐
        RectTransform _focus;
        float _focusLeft;
        /// <summary>이번 Focus 요청의 손가락이 이미 보였음 ('뽀' 소리는 한 번만)</summary>
        bool _focusPopped;

        bool _suppressed;
        bool _focusSuppressed;
        bool _wasVisible;

        // 막힌 곳을 계속 누르면(대상이 눌리지 않는 등) 그 대상의 입력 막기를 풀어 준다 — 튜토리얼에 갇히지 않게
        const int ReleaseTaps = 5;
        const float ReleaseWindow = 4f;
        int _outsideTaps;
        float _firstTapTime;
        RectTransform _releasedTarget;

        /// <summary>막힌 곳을 여러 번 눌러 입력 막기를 풀었음 (FtueDirector가 안내 토스트)</summary>
        public event System.Action BlockReleased;

        /// <summary>튜토리얼 안내가 켜져 있음 (숨김 중이어도)</summary>
        public bool IsActive => _active;

        /// <summary>튜토리얼 안내(Present/PresentDrag)를 숨김 — 다른 팝업·인트로가 위에 있을 때, hintOnly 단계. 손가락만 잠깐(Focus)과는 상관없다</summary>
        public bool Suppressed
        {
            get => _suppressed;
            set => _suppressed = value;
        }

        /// <summary>손가락만 잠깐(Focus)을 숨김 — 그 대상을 덮는 팝업이 위에 있거나 인트로 중일 때 (FtueDirector가 정한다)</summary>
        public bool FocusSuppressed
        {
            get => _focusSuppressed;
            set => _focusSuppressed = value;
        }

        /// <summary>지금 안내하는 대상 (없으면 null)</summary>
        public RectTransform Target => _target;

        /// <summary>손가락만 잠깐 가리키는 대상 (없거나 시간이 끝났으면 null)</summary>
        public RectTransform FocusTarget => _focus;

        void Awake()
        {
            _root = (RectTransform)transform;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }
            if (_dim != null)
            {
                _dim.Tapped += OnDimTapped;
                _dim.enabled = false;
            }
            if (_highlight != null) _highlight.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_dim != null) _dim.Tapped -= OnDimTapped;
        }

        /// <summary>말하는 친구</summary>
        public void SetSpeaker(Sprite portrait, string speakerName) => _bubble.SetSpeaker(portrait, speakerName);

        /// <summary>튜토리얼 안내를 보여 준다 (같은 대상이면 구멍 연출을 다시 하지 않는다)</summary>
        public void Present(string message, RectTransform target, bool blockInput, bool dim = true, PointerStyle pointer = PointerStyle.Tap)
        {
            // 대상이 바뀌거나 어둠이 새로 켜질 때 구멍이 다시 조여든다
            if (target != _target || !_active || (dim && !_useDim)) _iris = 0f;
            _active = true;
            _message = message;
            _target = target;
            _dragTo = null;
            _blockInput = blockInput;
            _useDim = dim;
            _pointer = pointer;
        }

        /// <summary>from에서 to로 끌어 놓는 안내 (어둠 없음)</summary>
        public void PresentDrag(string message, RectTransform from, RectTransform to)
        {
            _active = true;
            _message = message;
            _target = from;
            _dragTo = to;
            _blockInput = false;
            _useDim = false;
            _pointer = PointerStyle.Drag;
        }

        /// <summary>글만 바꾼다 (대상·손가락 그대로)</summary>
        public void SetMessage(string message) => _message = message;

        /// <summary>튜토리얼 안내를 끈다 (손가락만 잠깐(Focus)은 그대로)</summary>
        public void Hide()
        {
            _active = false;
            _target = null;
            _dragTo = null;
        }

        /// <summary>어둠 없이 손가락만 seconds초 (가이드 퀘스트 이동 뒤 등)</summary>
        public void Focus(RectTransform target, float seconds)
        {
            if (target == null) return;
            _focus = target;
            _focusLeft = Mathf.Max(0.5f, seconds);
            _focusPopped = false;
        }

        /// <summary>
        /// 손가락만 잠깐(Focus)의 대상만 바꾼다 — 남은 시간 · '뽀'(이미 울렸으면 다시 울리지 않는다)는 그대로.
        /// 가리키던 버튼이 덮개 밑으로 들어갔을 때 덮개의 버튼으로 옮긴다 (첫 만남 카드 — FtueDirector). 손가락이 없으면 아무것도 하지 않는다
        /// </summary>
        public void RetargetFocus(RectTransform target)
        {
            if (_focus == null || target == null) return;
            _focus = target;
        }

        void OnDimTapped()
        {
            _finger.Nudge();
            if (_bubble.IsShown) _bubble.Say(_message, instant: true);

            float now = Time.unscaledTime;
            if (_outsideTaps == 0 || now - _firstTapTime > ReleaseWindow)
            {
                _outsideTaps = 0;
                _firstTapTime = now;
            }
            if (++_outsideTaps < ReleaseTaps) return;
            _outsideTaps = 0;
            _releasedTarget = _target;
            BlockReleased?.Invoke();
        }

        bool Blocking => _blockInput && _useDim && (_target == null || _target != _releasedTarget);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_focus != null)
            {
                _focusLeft -= dt;
                if (_focusLeft <= 0f || _focus == null) _focus = null;
            }

            // 손가락만 잠깐(Focus)은 안내 숨김(Suppressed: hintOnly·팝업)과 따로 본다 — 긴 hintOnly 단계 중에도 [가기] 손가락이 나온다
            bool focusVisible = _focus != null && !_focusSuppressed;
            bool wantVisible = focusVisible || (_active && !_suppressed);
            if (_group != null)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, wantVisible ? 1f : 0f, dt / 0.2f);
                _group.blocksRaycasts = wantVisible && !focusVisible && Blocking;
            }
            if (!wantVisible)
            {
                if (_wasVisible && (_group == null || _group.alpha <= 0f)) HideParts();
                return;
            }
            _wasVisible = true;

            if (focusVisible)
            {
                StepFocus();
                return;
            }
            StepPresentation(dt);
        }

        void HideParts()
        {
            _wasVisible = false;
            if (_dim != null)
            {
                _dim.enabled = false;
                _dim.BlockOutside = false;
            }
            if (_highlight != null) _highlight.gameObject.SetActive(false);
            _finger.Hide();
            _bubble.Hide();
        }

        void StepFocus()
        {
            SetDim(false, default, 0f, 0f);
            if (_highlight != null) _highlight.gameObject.SetActive(false);
            _bubble.Hide();
            // 이동 직후 한두 프레임은 대상이 아직 배치 전일 수 있다 — 보일 때까지 손가락만 숨긴다
            if (!UIFind.IsShown(_focus))
            {
                _finger.Hide();
                return;
            }
            var rect = UIFind.RectIn(_focus, _root, Corners);
            _finger.ShowTap(rect.center - _root.rect.center, ApproachFor(rect.center));
            if (_focusPopped) return;
            // 손가락이 처음 보이는 순간 한 번 '뽀' (말풍선 없는 안내라 소리로 눈길을 끈다)
            _focusPopped = true;
            UISfx.Play(UISfxKey.CoachPop, FocusPopVolume);
        }

        /// <summary>손가락만 잠깐(Focus)이 나타날 때 '뽀' 소리 크기 (말풍선 등장보다 작게 — [가기] 누름 소리 바로 뒤라)</summary>
        const float FocusPopVolume = 0.7f;

        void StepPresentation(float dt)
        {
            bool targetShown = UIFind.IsShown(_target);
            if (!targetShown)
            {
                // 대상이 없으면 말풍선만 빈 자리에 (SHEET = 탭 화면 위쪽, MAIN = 가이드 퀘스트 줄 위 — M42 · IA §8.6)
                SetDim(false, default, 0f, 0f);
                if (_highlight != null) _highlight.gameObject.SetActive(false);
                _finger.Hide();
                if (string.IsNullOrEmpty(_message))
                {
                    _bubble.Hide();
                    return;
                }
                _bubble.Say(_message);
                PlaceFreeBubble();
                return;
            }

            var rect = UIFind.RectIn(_target, _root, Corners);
            var padded = Rect.MinMaxRect(rect.xMin - _holePadding, rect.yMin - _holePadding, rect.xMax + _holePadding, rect.yMax + _holePadding);

            // 구멍이 크게 열려 있다가 대상으로 조여든다 (움직임 줄이기면 처음부터 대상 크기 — 어둠 · 테두리는 알파로만 나타난다)
            bool reduced = UIMotion.Reduced;
            _iris = Mathf.Min(_irisTime, _iris + dt);
            float k = UIMotion.OutCubic(_iris / Mathf.Max(0.01f, _irisTime));
            if (_useDim)
            {
                float grow = reduced ? 1f : Mathf.Lerp(2.4f, 1f, k);
                var hole = new Rect(padded.center - padded.size * grow * 0.5f, padded.size * grow);
                hole = Rect.MinMaxRect(Mathf.Round(hole.xMin), Mathf.Round(hole.yMin), Mathf.Round(hole.xMax), Mathf.Round(hole.yMax));
                float radius = Mathf.Min(_maxRadius, Mathf.Min(padded.width, padded.height) * 0.5f);
                SetDim(true, hole, radius, _dimAlpha * k);
            }
            else
            {
                SetDim(false, default, 0f, 0f);
            }

            if (_highlight != null)
            {
                bool show = _pointer != PointerStyle.Drag;
                if (_highlight.gameObject.activeSelf != show) _highlight.gameObject.SetActive(show);
                if (show)
                {
                    // 테두리 맥박 (움직임 줄이기면 멈춘 채 가장 또렷한 모양 — pulse 1). 손가락 톡톡은 안내 자체라 그대로 둔다
                    float pulse = reduced ? 1f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    var hr = _highlight.rectTransform;
                    hr.anchoredPosition = padded.center - _root.rect.center;
                    hr.sizeDelta = padded.size + new Vector2(18f, 18f);
                    hr.localScale = Vector3.one * (1f + 0.025f * pulse);
                    var c = _highlight.color;
                    _highlight.color = new Color(c.r, c.g, c.b, (0.55f + 0.45f * pulse) * k);
                }
            }

            var center = rect.center;
            var approach = ApproachFor(center);
            switch (_pointer)
            {
                case PointerStyle.Tap:
                    _finger.ShowTap(center - _root.rect.center, approach);
                    break;
                case PointerStyle.Drag when UIFind.IsShown(_dragTo):
                    var to = UIFind.RectIn(_dragTo, _root, Corners).center;
                    _finger.ShowDrag(center - _root.rect.center, to - _root.rect.center);
                    center = to;
                    rect = UIFind.RectIn(_dragTo, _root, Corners);
                    approach = ApproachFor(center);
                    break;
                default:
                    _finger.Hide();
                    break;
            }

            PlaceBubble(rect, approach);
        }

        /// <summary>손가락 몸통 방향: 대상이 아래쪽이면 위에서, 위쪽이면 아래에서. 오른쪽 끝이면 왼쪽에서</summary>
        Vector2 ApproachFor(Vector2 center)
        {
            var area = SafeRect();
            bool lower = center.y < area.center.y;
            float side = center.x > area.center.x + area.width * 0.22f ? -1f : 1f;
            return new Vector2(0.52f * side, lower ? 0.85f : -0.85f);
        }

        void PlaceBubble(Rect target, Vector2 approach)
        {
            if (string.IsNullOrEmpty(_message))
            {
                _bubble.Hide();
                return;
            }
            _bubble.Say(_message);
            var area = SafeRect();
            var size = _bubble.Size;
            bool below = approach.y < 0f; // 손가락이 아래에 있으면 말풍선도 아래, 손가락 너머에
            float y = below
                ? target.yMin - _fingerReach - size.y * 0.5f
                : target.yMax + _fingerReach + size.y * 0.5f;
            // 손가락과 겹치지 않게 반대쪽으로 조금 비킨다
            float x = target.center.x - Mathf.Sign(approach.x) * 110f;
            x = Mathf.Clamp(x, area.xMin + size.x * 0.5f + BubbleEdgeMargin, area.xMax - size.x * 0.5f - BubbleEdgeMargin);
            y = Mathf.Clamp(y, area.yMin + size.y * 0.5f + BubbleEdgeMargin, area.yMax - size.y * 0.5f - BubbleEdgeMargin);
            var offset = _root.rect.center;
            _bubble.Place(new Vector2(x, y) - offset, below, target.center.x - x);
        }

        /// <summary>
        /// SHEET 기준 영역: 빌더가 이은 _freeAnchor, 없으면(이 필드가 생기기 전에 빌드한 장면) 틀이 묶어 둔 UIFrame.SheetRect —
        /// 둘 다 같은 SafeArea/ContentArea다 (SheetController가 Awake에서 묶는다). 할당 없음.
        /// </summary>
        RectTransform FreeAnchor => _freeAnchor != null ? _freeAnchor : UIFrame.SheetRect;

        /// <summary>MAIN 기준 영역: 빌더가 이은 _stageAnchor, 없으면 UIFrame.StageRect (같은 SafeArea/StageArea)</summary>
        RectTransform StageAnchor => _stageAnchor != null ? _stageAnchor : UIFrame.StageRect;

        /// <summary>대상 없는 말풍선을 빈 자리에 놓는다 (꼬리 없음). 매 프레임 — 사각형 계산만, 할당 없음</summary>
        void PlaceFreeBubble()
        {
            var safe = SafeRect();
            bool onSheet = FreeOnSheet(out float contentTop);
            // 전투 영역이 없는 장면(틀 이전)은 안전 영역 아래 끝 + 탭 바 높이를 전투 아래 끝으로 본다
            var stage = StageAnchor;
            float stageBottom = stage != null ? UIFind.RectIn(stage, _root, Corners).yMin : safe.yMin + UIFrame.TabBarHeight;
            var center = FreeBubbleCenter(onSheet, contentTop, stageBottom, safe, _bubble.Size);
            _bubble.Place(center - _root.rect.center, false, float.NaN);
        }

        /// <summary>
        /// 대상 없는 말풍선을 시트(탭 화면) 위에 놓을지, 그렇다면 기준이 되는 탭 화면 위 끝 (이 층 로컬 y).
        /// 시트 목표 상태(UIFrame.SheetOpen)를 따른다: 열기를 부른 그 프레임부터 다 열린 자리, 닫기를 부른 그 프레임부터 MAIN 자리.
        /// 위 끝 = 지금 위 끝 + 아직 남은 미끄러짐 (SheetController: anchoredPosition.y = TabBarHeight − SheetHeight·(1 − p), 다 열리면 TabBarHeight) —
        /// 그래서 올라오는 동안에도 말풍선이 다 열린 자리에서 기다린다. 틀(SheetController)이 묶이지 않은 장면은 탭 화면이 켜져 있으면 지금 자리.
        /// 기준 영역은 FreeAnchor (_freeAnchor가 없는 옛 장면은 UIFrame.SheetRect — 시트가 열려 있는데 MAIN 자리로 떨어져 시트 한가운데를 덮지 않게).
        /// </summary>
        bool FreeOnSheet(out float contentTop)
        {
            contentTop = 0f;
            var anchor = FreeAnchor;
            if (anchor == null || !anchor.gameObject.activeInHierarchy) return false;
            bool framed = UIFrame.SheetRect == anchor;
            if (framed && !UIFrame.SheetOpen) return false;
            contentTop = UIFind.RectIn(anchor, _root, Corners).yMax;
            if (!framed) return true;
            // 남은 미끄러짐은 부모(SafeArea) 로컬 단위 → 이 층 로컬 단위
            var parent = anchor.parent;
            float scale = parent != null ? parent.lossyScale.y / Mathf.Max(1e-5f, _root.lossyScale.y) : 1f;
            contentTop += (UIFrame.TabBarHeight - anchor.anchoredPosition.y) * scale;
            return true;
        }

        /// <summary>
        /// 대상 없는 말풍선 중심 (M42 · UX-SPEC-IA §8.6, 같은 좌표계의 y 위쪽 사각형들로 계산 — 테스트용 순수 함수).
        ///   · onSheet: 말풍선 위 변 = contentTop − FreeSheetInset (28).
        ///   · MAIN: 말풍선 아래 변 = stageBottom + FreeMainLift (282) — 가이드 퀘스트 줄 위.
        /// 가로는 안전 영역 가운데. 안전 영역 안쪽 BubbleEdgeMargin(16)을 넘지 않게 자른다 (안전 영역이 말풍선보다 작으면 자르지 않는다).
        /// </summary>
        internal static Vector2 FreeBubbleCenter(bool onSheet, float contentTop, float stageBottom, Rect safe, Vector2 size)
        {
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            float y = onSheet ? contentTop - FreeSheetInset - halfH : stageBottom + FreeMainLift + halfH;
            float x = safe.center.x;
            float minX = safe.xMin + halfW + BubbleEdgeMargin, maxX = safe.xMax - halfW - BubbleEdgeMargin;
            float minY = safe.yMin + halfH + BubbleEdgeMargin, maxY = safe.yMax - halfH - BubbleEdgeMargin;
            if (minX <= maxX) x = Mathf.Clamp(x, minX, maxX);
            if (minY <= maxY) y = Mathf.Clamp(y, minY, maxY);
            return new Vector2(x, y);
        }

        void SetDim(bool on, Rect hole, float radius, float alpha)
        {
            if (_dim == null) return;
            if (!on)
            {
                if (_dim.enabled) _dim.enabled = false;
                _dim.BlockOutside = false;
                return;
            }
            if (!_dim.enabled) _dim.enabled = true;
            var c = _dim.color;
            if (!Mathf.Approximately(c.a, alpha)) _dim.color = new Color(c.r, c.g, c.b, alpha);
            _dim.SetHole(hole, radius);
            _dim.BlockOutside = Blocking;
        }

        Rect SafeRect() => _safeArea != null ? UIFind.RectIn(_safeArea, _root, Corners) : _root.rect;
    }
}
