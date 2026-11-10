using System;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 탭 시트 (UX-SPEC-IA §4 · §5.4): 탭을 고르면 836px 시트(머리 56 + 탭 내용 780)가 탭 바 뒤에서 올라와 전투 아래쪽을 잠깐 덮는다.
    /// SafeArea/TabBar(늘 켜져 있음)에 MainTabController와 함께 산다. 보이는 양 p를 UIFrame에 알리면(SetProgress)
    /// 전투 올림(BattleFrameDriver) · 띠 여백 · HUD 흐림(SheetFade)이 따라간다. 열기 · 닫기는 MainTabController(Select · ShowMain)가 부른다.
    /// <para>
    /// 움직임 (§1.5): 열기 0.30 / 닫기 0.24초 OutCubic (UIMotion.SheetOpen / SheetClose). 도중에 바꾸면 지금 p에서 남은 몫 × 그 시간
    /// (최소 SheetSettleMin 0.10). 위치 = ContentArea.anchoredPosition.y = 170 − 836·(1 − p) (p = 0이면 머리가 탭 바 바로 뒤).
    /// 닫기가 끝나면(p = 0) UIFrame에 0 · 멈춤을 알리고 ContentArea를 끈 뒤 Closed. ContentArea를 켠 뒤 첫 두 프레임(UIMotion.SheetFirstFrames)은
    /// 시계를 1/30초까지만 넘긴다 — 무거운 패널의 긴 첫 프레임(OnEnable + 중첩 캔버스 빌드)이 미끄러짐 앞부분을 건너뛰지 않게 (CR-13).
    /// </para>
    /// <para>
    /// 줄이기(UIMotion.Reduced): 미끄러짐 없이 ContentArea CanvasGroup 알파만 (들어옴 0.10 / 나감 0.08초, 선형).
    /// p는 계단처럼 알린다 — 열기를 시작할 때 1, 닫기가 끝날 때 0 (전투 올림 · HUD 흐림이 그 자리에서 바로 바뀐다).
    /// 끌기는 줄이기에서도 손가락을 따라가고, 놓으면 바로 제자리(다시 열기) 또는 알파로 닫는다.
    /// </para>
    /// <para>
    /// 머리 끌기 (§4.3): SheetDragHandle(DragArea · HandleHit)이 BeginDrag / DragTo / EndDrag로 넘긴다 — 끄는 동안은 손가락이 p를 정하고
    /// 놓으면 닫기(UINav.GoToMain → ShowMain) 또는 다시 열기. 닫기(Close)는 끄는 중이면 끌기를 판단 없이 끝내고 지금 p에서 닫는다 (뒤로가기, CR-21).
    /// 손잡이(HandleHit)를 누르면 닫기 — 끌고 놓은 누름은 누름으로 치지 않는다.
    /// </para>
    /// <para>
    /// 자동 복귀 (§4.8): GameSettings.SheetAutoReturn이 켜져 있고 시트가 열려 멈춘 동안, 입력 없이(SleepModeView.HasInput — 잠자기 시계와 같은 정의)
    /// unscaled UIMotion.SheetAutoReturnSeconds(60초, 테스트는 AutoReturnSecondsForTests)가 지나면 UINav.GoToMain() (연출 있음).
    /// 한 프레임에 SheetIdleMaxStep(0.1초)까지만 더하고, 포커스 · 일시정지에 처음부터 (백그라운드에서 돌아온 첫 프레임에 닫히지 않게, CR-25).
    /// 팝업 · 튜토리얼 · 코치마크 · 끌기 · 시트 움직임 동안은 0에 묶는다. 설정은 GameSettings.Changed에서만 다시 읽는다 (매 프레임 아님).
    /// </para>
    /// 움직이는 중 · 끄는 중 · 열린 동안(자동 복귀가 켜져 있을 때만)만 enabled — 닫혀 멈췄으면 Update가 없다. 매 프레임 할당 없음.
    /// UIFrame.Bind(Awake) · Unbind(OnDestroy)로 정적 틀 상태를 이 장면에 잇는다 (CR-20).
    /// </summary>
    public class SheetController : MonoBehaviour
    {
        /// <summary>ContentArea 형제 중 전투 영역 이름 (_stage가 비었을 때 찾는다)</summary>
        const string StageAreaName = "StageArea";

        [Tooltip("탭 내용 영역 SafeArea/ContentArea (중첩 Canvas + GraphicRaycaster + CanvasGroup, 닫히면 꺼진다)")]
        [SerializeField] RectTransform _content;
        [Tooltip("시트 머리 ContentArea/SheetHeader (1080x56, 내용 위 끝에 붙는다)")]
        [SerializeField] RectTransform _header;
        [Tooltip("ContentArea의 CanvasGroup (줄이기 알파)")]
        [SerializeField] CanvasGroup _group;
        [Tooltip("머리 손잡이 누르는 자리 SheetHeader/HandleHit (누르면 닫기)")]
        [SerializeField] Button _handle;
        [Tooltip("머리 끌기 (SheetHeader/DragArea — HandleHit에도 같은 컴포넌트가 하나 더 있다)")]
        [SerializeField] SheetDragHandle _drag;
        [Tooltip("전투 영역 SafeArea/StageArea (UIFrame.IsUnderSheet 기준). 비면 ContentArea의 형제 StageArea를 찾는다")]
        [SerializeField] RectTransform _stage;
        [Tooltip("코치마크 (보이는 동안 자동 복귀 시계를 0에 묶는다). 비면 처음 한 번 장면에서 찾는다")]
        [SerializeField] CoachMarkView _coach;
        [Tooltip("머리의 돌아가기 칩 CanvasGroup — 칸이 빈 채 시트를 켜면 알파 0에서 시작한다 (바로 닫힌 시트 안에 남은 지난 칩이 한 번 비치지 않게)")]
        [SerializeField] CanvasGroup _returnChipGroup;

        /// <summary>
        /// 테스트 고리 (UiFrameTests (h)): 0보다 크면 자동 복귀 시간을 이 초로 줄인다. 0 이하 = UIMotion.SheetAutoReturnSeconds.
        /// 테스트는 끝날 때 0으로 되돌린다 (실행 시작에도 0으로).
        /// </summary>
        public static float AutoReturnSecondsForTests { get; set; }

        /// <summary>닫기가 끝남 (p = 0, ContentArea를 끈 뒤) — MainTabController가 패널을 끈다. 이미 닫혀 있던 시트를 닫으면 부르지 않는다</summary>
        public event Action Closed;

        /// <summary>머리를 끌고 있는지</summary>
        public bool IsDragging => _activeDrag != null;

        /// <summary>지금 보이는 양 p (0 = 닫힘 ~ 1 = 다 열림, 이징을 거친 값 — UIFrame.SheetProgress와 같다)</summary>
        public float Progress => _p;

        /// <summary>목표 상태가 열림인지 (Open부터 true, Close부터 false — UIFrame.SheetOpen과 같다)</summary>
        public bool IsOpen => _open;

        /// <summary>시트가 화면에 있는지 (ContentArea가 켜져 있음 — 열림 · 여닫는 중)</summary>
        public bool IsShown => _content != null && _content.gameObject.activeSelf;

        /// <summary>다 닫혀 멈췄는지 (목표 닫힘 + ContentArea 꺼짐)</summary>
        public bool IsClosed => !_open && !IsShown;

        /// <summary>움직이지 않음 (열기 · 닫기 · 끌기가 끝남)</summary>
        public bool IsSettled => !_animating && _activeDrag == null;

        float _p;
        bool _open;
        bool _animating;
        /// <summary>지금 움직임의 방향 (true = 열기)</summary>
        bool _animOpen;
        /// <summary>지금 움직임이 줄이기 알파인지 (아니면 미끄러짐)</summary>
        bool _fadeMode;
        float _from;
        float _to;
        float _time;
        float _duration;
        int _firstFrames;
        SheetDragHandle _activeDrag;

        bool _autoReturn;
        float _idle;
        Vector3 _lastMouse;
        bool _coachSearched;
        bool _bound;

        float AutoReturnSeconds => AutoReturnSecondsForTests > 0f ? AutoReturnSecondsForTests : UIMotion.SheetAutoReturnSeconds;

        // ───────────── 수명 ─────────────

        void Awake()
        {
            if (_stage == null && _content != null && _content.parent != null) _stage = _content.parent.Find(StageAreaName) as RectTransform;
            if (_group == null && _content != null) _group = _content.GetComponent<CanvasGroup>();
            UIFrame.Bind(_content, _stage);
            _bound = true;

            // 언제나 MAIN(닫힘)으로 시작한다 (§1.2) — 빌더는 ContentArea를 열린 자리에 두고 끈 채 저장한다
            _p = 0f;
            _open = false;
            ApplyPosition(0f);
            if (_group != null) _group.alpha = 1f;
            if (_content != null && _content.gameObject.activeSelf) _content.gameObject.SetActive(false);

            if (_handle != null) _handle.onClick.AddListener(OnHandleTap);
            _autoReturn = GameSettings.SheetAutoReturn;
            GameSettings.Changed += OnSettingsChanged;
            _lastMouse = Input.mousePosition;
            enabled = false;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
            if (_handle != null) _handle.onClick.RemoveListener(OnHandleTap);
            // 이 장면의 시트일 때만 푼다 (장면이 내려감 — 정적 상태가 다음 장면으로 넘어가지 않게, CR-20)
            if (_bound && UIFrame.SheetRect == _content) UIFrame.Unbind();
            _bound = false;
        }

        void OnEnable() => ResetIdle();

        void OnApplicationFocus(bool focused) => ResetIdle();

        void OnApplicationPause(bool paused) => ResetIdle();

        void OnSettingsChanged()
        {
            _autoReturn = GameSettings.SheetAutoReturn;
            _idle = 0f;
            Refresh();
        }

        // ───────────── 열기 · 닫기 ─────────────

        /// <summary>
        /// 시트를 연다 (tab = MainTab 번호): ContentArea를 켜고 목표 1, 지금 p에서 연출, UIFrame.SetState(true, tab).
        /// 이미 열려 있거나 열리는 중이면 탭만 바꾼다 (미끄러짐 · 올림 없음). 닫히는 중이면 지금 p에서 다시 연다.
        /// MainTabController.Select가 패널을 고른 뒤 부른다.
        /// </summary>
        public void Open(int tab)
        {
            if (_content == null) return;
            bool wasOpen = _open;
            if (!_content.gameObject.activeSelf) Activate();
            _open = true;
            UIFrame.SetState(true, tab);
            // 끄는 중이면 손가락이 p를 쥐고 있다 (탭만 바뀐다)
            if (_activeDrag == null && !(wasOpen && (_animating ? _animOpen : IsFullyOpen))) StartMove(true);
            Refresh();
        }

        /// <summary>
        /// 시트를 내린다: 목표 0, 지금 p에서 연출 (immediate면 바로 — 도구 · 테스트). p = 0이면 ContentArea를 끄고 Closed. UIFrame.SetState(false, −1).
        /// 끄는 중이면 끌기를 판단 없이 끝내고 지금 p에서 닫는다 (뒤로가기 · 사냥 버튼, CR-21). MainTabController.ShowMain이 부른다.
        /// </summary>
        public void Close(bool immediate = false)
        {
            EndDragSilently();
            _open = false;
            UIFrame.SetState(false, -1);
            if (!IsShown)
            {
                // 이미 닫혀 있다 (그래도 멈춘 0으로 맞춰 둔다)
                _animating = false;
                _fadeMode = false;
                if (_p != 0f || !UIFrame.SheetSettled) SetP(0f, true);
                Refresh();
                return;
            }
            if (immediate) FinishClose();
            else StartMove(false);
            Refresh();
        }

        /// <summary>머리 끌기를 놓기 판단 없이 끝낸다 — 시트는 지금 p에서 열린 자리로 돌아간다 (바로 Close를 부르면 지금 p에서 닫힌다). 끌고 있지 않으면 아무 일 없다</summary>
        public void CancelDrag()
        {
            if (_activeDrag != null) _activeDrag.Cancel();
            else if (_drag != null) _drag.Cancel();
        }

        bool IsFullyOpen => _p >= 1f && (_group == null || _group.alpha >= 0.999f);

        /// <summary>ContentArea를 켠다 (닫힌 자리에서 — 첫 두 프레임 시계 제한, 줄이기면 알파 0에서)</summary>
        void Activate()
        {
            ApplyPosition(_p);
            if (_group != null) _group.alpha = UIMotion.Reduced && _p <= 0f ? 0f : 1f;
            // 돌아가기 칩: 바로 닫힘(칩으로 MAIN에 돌아감 등)으로 꺼진 칩은 숨는 연출 없이 마지막 알파를 들고 있다 — 칸이 비었으면 숨긴 채 켠다
            if (_returnChipGroup != null && !UINav.HasReturn)
            {
                _returnChipGroup.alpha = 0f;
                _returnChipGroup.blocksRaycasts = false;
                _returnChipGroup.interactable = false;
            }
            _content.gameObject.SetActive(true);
            _firstFrames = UIMotion.SheetFirstFrames;
        }

        /// <summary>지금 p(줄이기면 알파)에서 열기 · 닫기 연출을 시작한다</summary>
        void StartMove(bool open)
        {
            _animOpen = open;
            _time = 0f;
            if (UIMotion.Reduced && _group != null)
            {
                // 줄이기: 알파만. 열기는 시작할 때 p = 1 (전투 올림 · 흐림이 바로), 닫기는 끝날 때 p = 0
                _fadeMode = true;
                if (open) SetP(1f, false);
                _from = _group.alpha;
                _to = open ? 1f : 0f;
                _duration = Mathf.Abs(_to - _from) * (open ? UIMotion.SheetReducedIn : UIMotion.SheetReducedOut);
            }
            else
            {
                _fadeMode = false;
                if (_group != null) _group.alpha = 1f;
                _from = _p;
                _to = open ? 1f : 0f;
                float full = open ? UIMotion.SheetOpen : UIMotion.SheetClose;
                _duration = Mathf.Abs(_to - _from) < 0.0001f ? 0f : Mathf.Max(UIMotion.SheetSettleMin, Mathf.Abs(_to - _from) * full);
            }
            _animating = true;
            if (_duration <= 0f) Step(0f);
            else UIFrame.SetProgress(_p, false);
        }

        void Step(float dt)
        {
            _time += dt;
            float k = _duration > 0f ? Mathf.Clamp01(_time / _duration) : 1f;
            bool done = k >= 1f;
            if (_fadeMode)
            {
                if (_group != null) _group.alpha = Mathf.Lerp(_from, _to, k);
                if (!done) return;
            }
            else if (!done)
            {
                SetP(Mathf.LerpUnclamped(_from, _to, UIMotion.OutCubic(k)), false);
                return;
            }

            _animating = false;
            _fadeMode = false;
            if (_animOpen) SetP(1f, true);
            else FinishClose();
        }

        /// <summary>닫기 끝: p = 0 멈춤을 알리고 ContentArea를 끈 뒤 Closed (알파는 다음 열기를 위해 1로)</summary>
        void FinishClose()
        {
            _animating = false;
            _fadeMode = false;
            _firstFrames = 0;
            SetP(0f, true);
            bool wasShown = IsShown;
            if (wasShown) _content.gameObject.SetActive(false);
            if (_group != null) _group.alpha = 1f;
            Refresh();
            if (!wasShown) return;
            var closed = Closed;
            if (closed == null) return;
            try
            {
                closed();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void SetP(float p, bool settled)
        {
            _p = Mathf.Clamp01(p);
            ApplyPosition(_p);
            UIFrame.SetProgress(_p, settled);
        }

        /// <summary>ContentArea 자리: y = 170 − 836·(1 − p) (열림 = 탭 바 위 970~1750, 닫힘 = 머리가 탭 바 바로 뒤)</summary>
        void ApplyPosition(float p)
        {
            if (_content == null) return;
            var position = _content.anchoredPosition;
            float y = UIFrame.TabBarHeight - UIFrame.SheetHeight * (1f - p);
            if (position.y == y) return;
            position.y = y;
            _content.anchoredPosition = position;
        }

        /// <summary>MAIN으로 (MainTabController.ShowMain이 받는다 — 받는 쪽이 없으면 스스로 닫는다)</summary>
        void RequestMain()
        {
            UINav.GoToMain();
            if (_open) Close();
        }

        /// <summary>손잡이 누름 → 닫기. 끌다가 놓은 누름(누름이 끌기 끝보다 먼저 온다)은 무시</summary>
        void OnHandleTap()
        {
            if (_activeDrag != null || !_open) return;
            RequestMain();
        }

        // ───────────── 끌기 (SheetDragHandle이 부른다) ─────────────

        /// <summary>끌기 시작: 열린(또는 열리는) 시트만 잡을 수 있다. 연출을 멈추고 손가락이 p를 쥔다</summary>
        internal bool BeginDrag(SheetDragHandle handle)
        {
            if (handle == null || !_open || !IsShown) return false;
            if (_activeDrag != null && _activeDrag != handle) return false;
            _activeDrag = handle;
            _animating = false;
            if (_fadeMode)
            {
                _fadeMode = false;
                if (_group != null) _group.alpha = 1f;
            }
            UIFrame.SetProgress(_p, false);
            Refresh();
            return true;
        }

        /// <summary>끄는 동안 p (0~1)</summary>
        internal void DragTo(SheetDragHandle handle, float p)
        {
            if (handle != _activeDrag) return;
            SetP(p, false);
        }

        /// <summary>놓음: dismiss면 MAIN으로 (지금 p에서 닫힘), 아니면 지금 p에서 다시 연다 (줄이기면 바로)</summary>
        internal void EndDrag(SheetDragHandle handle, bool dismiss)
        {
            if (handle != _activeDrag) return;
            _activeDrag = null;
            if (dismiss) RequestMain();
            else if (_open) StartMove(true);
            Refresh();
        }

        /// <summary>끌기가 판단 없이 끝남 (포커스 잃음 M01 · 꺼짐 · CancelDrag): 열린 자리로 돌아간다</summary>
        internal void OnDragCancelled(SheetDragHandle handle)
        {
            if (handle != _activeDrag) return;
            _activeDrag = null;
            if (_open) StartMove(true);
            Refresh();
        }

        /// <summary>끌기를 되돌리지 않고 끝낸다 (닫기 직전 — 지금 p에서 닫히게)</summary>
        void EndDragSilently()
        {
            var drag = _activeDrag;
            if (drag == null) return;
            _activeDrag = null;
            drag.Cancel();
        }

        // ───────────── 프레임 ─────────────

        void Update()
        {
            float raw = Time.unscaledDeltaTime;
            float dt = raw;
            if (_firstFrames > 0)
            {
                _firstFrames--;
                dt = Mathf.Min(dt, UIMotion.SheetFirstFrameMaxStep);
            }
            if (_animating && _activeDrag == null) Step(dt);
            TickAutoReturn(raw);
            Refresh();
        }

        /// <summary>움직이는 중 · 끄는 중 · 열린 동안(자동 복귀 시계)만 Update를 돌린다</summary>
        void Refresh()
        {
            bool want = _animating || _activeDrag != null || (_open && _autoReturn && IsShown);
            if (enabled != want) enabled = want;
        }

        // ───────────── 자동 복귀 (§4.8) ─────────────

        void TickAutoReturn(float dt)
        {
            // 입력 정의는 잠자기 시계와 같다 (마우스 자리는 매 프레임 갱신해 둔다)
            bool input = SleepModeView.HasInput(ref _lastMouse);
            if (!_autoReturn || !_open || _animating || _activeDrag != null || input || AutoReturnHeld())
            {
                _idle = 0f;
                return;
            }
            _idle += Mathf.Min(dt, UIMotion.SheetIdleMaxStep);
            if (_idle < AutoReturnSeconds) return;
            _idle = 0f;
            RequestMain();
        }

        /// <summary>시계를 0에 묶는 동안: 팝업 · 튜토리얼 · 코치마크(안내 · 손가락)가 보임</summary>
        bool AutoReturnHeld()
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.HasOpenPopup) return true;
            if (Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive) return true;
            var coach = Coach();
            return coach != null && ((coach.IsActive && !coach.Suppressed) || (coach.FocusTarget != null && !coach.FocusSuppressed));
        }

        CoachMarkView Coach()
        {
            if (_coach == null && !_coachSearched)
            {
                _coachSearched = true;
                _coach = FindFirstObjectByType<CoachMarkView>(FindObjectsInactive.Include);
            }
            return _coach;
        }

        void ResetIdle()
        {
            _idle = 0f;
            _lastMouse = Input.mousePosition;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTestHooks() => AutoReturnSecondsForTests = 0f;
    }
}
