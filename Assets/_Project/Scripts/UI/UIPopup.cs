using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 팝업 기본 클래스. 열기/닫기는 UIManager 스택으로 관리되고 Android 뒤로가기는 가장 위의 팝업부터 RequestDismiss한다.
    /// <para>
    /// 닫기 정책 (M49): 어둡게 덮기(Dim) 누름과 뒤로가기는 RequestDismiss → OnDimOrBack (기본 = Close).
    /// 소환 결과(먼저 건너뛰기) · 복귀(받는 중에는 무시) · 진화 제작처럼 바로 닫으면 안 되는 팝업은 OnDimOrBack을 덮어쓴다.
    /// X 버튼은 기본으로 바로 Close — CloseButtonUsesDismiss가 true면 X도 RequestDismiss로 보낸다.
    /// </para>
    /// <para>
    /// 열고 닫는 연출 (M48 · §1.8, 모두 unscaled): 열기 = 전체 알파 0 → 1 (0.12초 OutQuad) · 덮개 0 → 목표 (0.16초 OutCubic)
    /// · 창 0.90 → 1배 (0.20초 OutBack 1.2). 덮개 목표는 팝업 하나면 1, 위에 겹치면 0.4 (UIManager.OnPopupOpened가 정한다 — 아래 덮개는 그대로).
    /// 닫기 = 장부 정리(IsOpen=false · OnPopupClosed · OnClose · Closed)는 예전처럼 바로, 그다음 누름을 막고 0.12초 InCubic으로
    /// 창 1 → 0.94배 · 알파 → 0 · 덮개 → 0 뒤에 SetActive(false) (알파 · 크기는 되돌려 둔다). 닫히는 중에 다시 열면 지금 값에서 연다.
    /// 줄이기(UIMotion.Reduced)면 크기는 그대로 두고 알파만 0.08초 열기 / 0.06초 닫기. 꺼져 있으면 연출 없이 바로 끈다.
    /// 닫을 때 이 팝업의 코루틴은 예전처럼 멈춘다 (예전에는 SetActive(false)가 멈췄다).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPopup : MonoBehaviour
    {
        [SerializeField] protected RectTransform _window;
        [SerializeField] Button _closeButton;
        [SerializeField] Button _dimButton;
        [Tooltip("어둡게 덮기(Dim)의 CanvasGroup (겹친 팝업은 0.4까지만). 비면 Dim 버튼에서 찾거나 붙이고, Dim이 없으면 전체 알파만 쓴다")]
        [SerializeField] CanvasGroup _dimGroup;

        CanvasGroup _canvasGroup;
        Coroutine _transition;
        /// <summary>닫기 연출 중 (IsOpen은 이미 false, 오브젝트는 아직 켜짐)</summary>
        bool _closing;
        /// <summary>Open 안에서 여는 연출을 곧 시작한다 (덮개 목표를 바로 입히지 않는다 — 연출이 0에서 올린다)</summary>
        bool _openingNow;
        float _dimTarget = UIMotion.DimSingle;

        public bool IsOpen { get; private set; }
        public event Action Closed;

        /// <summary>창 (UIManager.TopPopupWindowRect · 토스트 자리). 창이 없으면 팝업 자신</summary>
        public RectTransform Window => _window != null ? _window : transform as RectTransform;

        /// <summary>닫기 연출이 도는 중인지 (보이지만 누를 수 없고 IsOpen은 false)</summary>
        public bool IsClosing => _closing;

        /// <summary>X 버튼도 RequestDismiss로 보낼지 (기본 false = X는 바로 Close). 받는 중 무시 · 확인이 필요한 팝업이 true로 덮어쓴다</summary>
        protected virtual bool CloseButtonUsesDismiss => false;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_dimGroup == null && _dimButton != null)
            {
                // 다시 빌드하기 전 씬: Dim에 CanvasGroup이 없으면 여기서 붙인다 (누름은 그대로 받는다)
                _dimGroup = _dimButton.GetComponent<CanvasGroup>();
                if (_dimGroup == null) _dimGroup = _dimButton.gameObject.AddComponent<CanvasGroup>();
            }
            if (_dimGroup == _canvasGroup) _dimGroup = null;
            if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseButton);
            if (_dimButton != null) _dimButton.onClick.AddListener(RequestDismiss);
        }

        protected virtual void OnDisable()
        {
            // 닫기 연출 중에 밖에서 꺼지면 (부모가 꺼짐 등) 코루틴은 이미 멈췄다 → 다시 켜져도 보이지 않게만 둔다 (다음 Open이 되살린다)
            _transition = null;
            if (!_closing) return;
            _closing = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>팝업을 연다. 이미 열려 있으면 맨 위로 올리고 내용을 갱신한다. 닫히는 중이면 그 자리에서 다시 연다.</summary>
        public void Open()
        {
            bool wasOpen = IsOpen;
            bool wasClosing = _closing;
            IsOpen = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            EnsureGroup();
            if (wasClosing)
            {
                StopTransition();
                _closing = false;
            }
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
            _openingNow = !wasOpen;
            var ui = UIManager.Instance;
            if (ui != null) ui.OnPopupOpened(this);
            _openingNow = false;
            OnOpen();
            // OnOpen 안에서 닫혔으면 (내용이 없어 바로 닫는 팝업 등) 연출하지 않는다
            if (!wasOpen && IsOpen)
            {
                PlayOpenAnimation(wasClosing);
                UISfx.Play(UISfxKey.PopupOpen);
            }
        }

        /// <summary>바로 닫는다 (닫기 정책 OnDimOrBack을 거치지 않는다 — 코드에서 닫을 때 · 받기 · 확인 버튼)</summary>
        public void Close()
        {
            if (!IsOpen) return;
            CloseBookkeeping();
            // Closed 처리기 안에서 다시 열렸으면 닫기 연출을 하지 않는다
            if (IsOpen) return;
            UISfx.Play(UISfxKey.PopupClose);
            PlayCloseAnimation();
        }

        /// <summary>
        /// 연출 없이 바로 닫고 끈다 (닫기 정책을 거치지 않는다). 닫히는 중이면 연출만 끝낸다.
        /// 도구 · 테스트용 UIManager.CloseAllImmediate가 쓴다.
        /// </summary>
        public void CloseImmediate()
        {
            if (IsOpen) CloseBookkeeping();
            if (IsOpen) return;
            FinishHide();
        }

        /// <summary>처리기 없이 닫힌 것으로 내리고 끈다 (UIManager.CloseAllImmediate가 닫히자마자 다시 열리는 팝업을 끝낼 때만)</summary>
        internal void ForceHidden()
        {
            IsOpen = false;
            FinishHide();
        }

        /// <summary>
        /// 어둡게 덮기 누름 · 뒤로가기 · (CloseButtonUsesDismiss면) X 버튼이 부른다. 열려 있을 때만 OnDimOrBack으로 보낸다.
        /// UIManager.CloseTop도 이것을 부른다 — 팝업이 막으면(건너뛰기 · 무시) 열린 채로 남는다.
        /// </summary>
        public void RequestDismiss()
        {
            if (IsOpen) OnDimOrBack();
        }

        /// <summary>닫아 달라는 요청 처리 (기본 = Close). 바로 닫으면 안 되는 팝업이 덮어쓴다</summary>
        protected virtual void OnDimOrBack() => Close();

        protected virtual void OnOpen() { }
        protected virtual void OnClose() { }

        /// <summary>덮개 목표 알파 (UIManager.OnPopupOpened: 팝업 하나 1, 겹치면 0.4). 연출 중이면 그 목표로 간다</summary>
        internal void SetDimTarget(float target)
        {
            _dimTarget = Mathf.Clamp01(target);
            if (!_openingNow && _transition == null && IsOpen) SetDimAlpha(_dimTarget);
        }

        void OnCloseButton()
        {
            if (CloseButtonUsesDismiss) RequestDismiss();
            else Close();
        }

        void CloseBookkeeping()
        {
            IsOpen = false;
            var ui = UIManager.Instance;
            if (ui != null) ui.OnPopupClosed(this);
            OnClose();
            Closed?.Invoke();
        }

        // ───────────── 연출 ─────────────

        void EnsureGroup()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        void StopTransition()
        {
            if (_transition == null) return;
            StopCoroutine(_transition);
            _transition = null;
        }

        void PlayOpenAnimation(bool fromCurrent)
        {
            StopTransition();
            if (!isActiveAndEnabled) return;
            _transition = StartCoroutine(OpenRoutine(fromCurrent));
        }

        void PlayCloseAnimation()
        {
            // 예전처럼 닫힐 때 이 팝업의 코루틴(드러내기 · 타이머 등)을 멈춘다 — 닫기 연출은 그 뒤에 새로 시작한다
            StopAllCoroutines();
            _transition = null;
            EnsureGroup();
            if (!isActiveAndEnabled)
            {
                FinishHide();
                return;
            }
            _closing = true;
            _canvasGroup.blocksRaycasts = false;
            _transition = StartCoroutine(CloseRoutine());
        }

        IEnumerator OpenRoutine(bool fromCurrent)
        {
            bool reduced = UIMotion.Reduced;
            float rootFrom = fromCurrent ? _canvasGroup.alpha : 0f;
            float dimFrom = fromCurrent ? DimAlpha : 0f;
            float scaleFrom = reduced ? 1f : fromCurrent ? WindowScale : UIMotion.PopupWindowFrom;
            float rootTime = reduced ? UIMotion.PopupReducedIn : UIMotion.PopupRootIn;
            float dimTime = reduced ? UIMotion.PopupReducedIn : UIMotion.PopupDimIn;
            float windowTime = reduced ? 0f : UIMotion.PopupWindowIn;
            float total = Mathf.Max(rootTime, Mathf.Max(dimTime, windowTime));

            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                _canvasGroup.alpha = Mathf.Lerp(rootFrom, 1f, UIMotion.OutQuad(t / rootTime));
                SetDimAlpha(Mathf.Lerp(dimFrom, _dimTarget, UIMotion.OutCubic(t / dimTime)));
                SetWindowScale(windowTime > 0f
                    ? Mathf.LerpUnclamped(scaleFrom, 1f, UIMotion.OutBack(t / windowTime, UIMotion.PopupOvershoot))
                    : 1f);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            SetDimAlpha(_dimTarget);
            SetWindowScale(1f);
            _transition = null;
        }

        IEnumerator CloseRoutine()
        {
            bool reduced = UIMotion.Reduced;
            float time = reduced ? UIMotion.PopupReducedOut : UIMotion.PopupOut;
            float rootFrom = _canvasGroup.alpha;
            float dimFrom = DimAlpha;
            float scaleFrom = WindowScale;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = UIMotion.InCubic(t / time);
                _canvasGroup.alpha = Mathf.Lerp(rootFrom, 0f, k);
                SetDimAlpha(Mathf.Lerp(dimFrom, 0f, k));
                if (!reduced) SetWindowScale(Mathf.Lerp(scaleFrom, UIMotion.PopupWindowTo, k));
                yield return null;
            }
            _transition = null;
            FinishHide();
        }

        /// <summary>연출을 끝내고 끈다 (알파 · 크기 · 누름은 다음에 열 때를 위해 되돌려 둔다)</summary>
        void FinishHide()
        {
            StopTransition();
            _closing = false;
            ResetVisuals();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void ResetVisuals()
        {
            EnsureGroup();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
            SetDimAlpha(_dimTarget);
            SetWindowScale(1f);
        }

        float DimAlpha => _dimGroup != null ? _dimGroup.alpha : 1f;

        void SetDimAlpha(float alpha)
        {
            if (_dimGroup != null && !Mathf.Approximately(_dimGroup.alpha, alpha)) _dimGroup.alpha = alpha;
        }

        float WindowScale => _window != null ? _window.localScale.x : 1f;

        void SetWindowScale(float scale)
        {
            if (_window == null) return;
            var target = new Vector3(scale, scale, 1f);
            if (_window.localScale != target) _window.localScale = target;
        }
    }
}
