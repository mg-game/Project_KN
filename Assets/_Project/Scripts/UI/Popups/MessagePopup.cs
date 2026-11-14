using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>확인 창 모양 (M55): 보통 · 재화 쓰기(비용 줄) · 되돌릴 수 없는 일(DangerButton, 0.6초 잠금)</summary>
    public enum ConfirmStyle
    {
        Normal = 0,
        Spend = 1,
        Danger = 2,
    }

    /// <summary>확인 창 설정 (MessagePopup.Show / UIManager.ShowSpendConfirm · ShowDangerConfirm)</summary>
    public struct ConfirmOpts
    {
        /// <summary>확인 버튼 이름 (비면 '확인') — 행동이면 동사로: 구매 · 소환 · 수선 · 초기화 · 건너뛰기 · 해제 · 원정 취소 · 닫기</summary>
        public string okLabel;
        /// <summary>취소 버튼 이름 (비면 '취소')</summary>
        public string cancelLabel;
        public ConfirmStyle style;
        /// <summary>비용 줄 재화 (cost &gt; 0일 때만 비용 줄을 보인다)</summary>
        public CurrencyType? currency;
        public double cost;
        /// <summary>'오늘 다시 묻지 않기' 키 (PlayerPrefs — 값 = 고른 날의 DayKey). 비면 칸을 숨긴다</summary>
        public string skipKey;
        /// <summary>확인 없이 끝남 (취소 · 덮개 · 뒤로 · X · 다른 확인이 이 창을 넘겨받음 · 모두 닫기). 생략 가능</summary>
        public Action onCancel;
        /// <summary>어떻게 끝나든 정확히 한 번 (확인이면 onOk 다음, 아니면 onCancel 다음) — 확인 전에 막아 둔 버튼을 여기서 푼다. 생략 가능</summary>
        public Action onClosed;
        /// <summary>
        /// 취소 *버튼*을 눌렀을 때만 (덮개 · 뒤로 · X · 다른 확인이 넘겨받음 · 모두 닫기에는 부르지 않는다). 창을 닫은 뒤(onCancel → onClosed 다음)
        /// 부르고, 예외는 기록만 한다. 취소 자리가 행동인 확인이 쓴다 — 종료 확인의 '닫기' (UIManager.ShowExitConfirm, UX-SPEC 부록 D M15). 생략 가능
        /// </summary>
        public Action onCancelButton;
    }

    /// <summary>
    /// 알림(확인 하나) / 확인 · 취소 공용 팝업. SystemPopups 층(튜토리얼 · 팡파르 위)에 있다.
    ///   · 버튼: 취소(Secondary, 왼쪽) / 확인(오른쪽, 보통 · 쓰기 = PrimaryButton 그림). 되돌릴 수 없는 일은 확인 자리에 위험 버튼
    ///     (DangerButton + Cream, 열린 뒤 0.6초 잠금). 자리는 바뀌지 않는다. 이름은 닫힐 때 '확인' / '취소'로 되돌린다.
    ///   · 비용 줄 (쓰기 확인): 재화 아이콘 48 · 비용 40 굵게 Cream · '보유 4,450 → 3,550' 26 SubText, 버튼 위.
    ///   · '오늘 다시 묻지 않기' (skipKey): 상자 40 + 26 SubText. 켜고 확인하면 PlayerPrefs[skipKey] = 오늘 DayKey.
    ///   · 줄이 생기면 창이 560 → +84(비용) → +60(묻지 않기)만큼 커지고 본문 아래 여백도 같이 늘어 본문 칸 크기는 그대로다.
    ///   · 닫힘 알림 (ConfirmOpts.onCancel · onClosed): 확인 = onOk → onClosed, 그 밖의 닫힘 = onCancel → onClosed (OnClose에서).
    ///     답하지 않은 확인이 열려 있는데 다른 Show가 오면 새 내용으로 바꾼 뒤 앞 확인에 onCancel → onClosed를 알린다 (조용히 버리지 않는다).
    ///     알림이 예외를 던져도 기록만 하고 다음 알림을 부른다.
    ///   · 취소 버튼 누름만 (ConfirmOpts.onCancelButton): 취소 버튼으로 닫히면 닫힘 알림(onCancel → onClosed) 뒤에 한 번 더 부른다.
    ///     덮개 · 뒤로 · X · 넘겨받음 · 모두 닫기로 끝나면 부르지 않는다 (종료 확인 — 덮개 · 뒤로는 머물기, '닫기' 버튼만 끈다).
    ///   · 소리 (AUDIO-SPEC §8.1): 확인(위험 확인 포함) = Confirm — 그 버튼의 누름 소리(PressScale Tap)는 Awake에서 끈다 (겹치지 않게).
    ///     닫힐 때의 PopupClose는 UIPopup이 낸다. 취소는 예전 그대로 (Tap + PopupClose).
    /// </summary>
    public class MessagePopup : UIPopup
    {
        public const string DefaultOkLabel = "확인";
        public const string DefaultCancelLabel = "취소";
        public const string SkipTodayLabel = "오늘 다시 묻지 않기";

        /// <summary>위험 확인 버튼 잠금 (빠른 두 번 누름 방지)</summary>
        const float DangerLockSeconds = 0.6f;
        /// <summary>버튼 줄 위 첫 줄의 아래 끝 (Body 아래에서)</summary>
        const float RowsBottom = 112f;
        const float CostRowHeight = 72f;
        /// <summary>비용 줄이 차지하는 높이 (줄 72 + 간격 12) — 창도 이만큼 커진다</summary>
        const float CostRowStep = 84f;
        const float SkipRowHeight = 52f;
        /// <summary>묻지 않기 줄이 차지하는 높이 (줄 52 + 간격 8)</summary>
        const float SkipRowStep = 60f;

        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _bodyText;
        [SerializeField] Button _okButton;
        [SerializeField] Button _cancelButton;
        [Tooltip("확인 / 취소 이름 (비면 버튼 아래에서 찾는다)")]
        [SerializeField] TMP_Text _okLabel;
        [SerializeField] TMP_Text _cancelLabel;
        [Tooltip("되돌릴 수 없는 일의 확인 버튼 (DangerButton). 확인 버튼 자리에 대신 켠다")]
        [SerializeField] Button _dangerButton;
        [SerializeField] TMP_Text _dangerLabel;

        [Header("비용 줄 (쓰기 확인)")]
        [SerializeField] RectTransform _costRow;
        [SerializeField] Image _costIcon;
        [SerializeField] TMP_Text _costText;
        [SerializeField] TMP_Text _balanceText;
        [Tooltip("재화 아이콘 (CurrencyType 순서: 단추 · 별조각 · 별빛 사탕)")]
        [SerializeField] Sprite[] _currencyIcons;

        [Header("오늘 다시 묻지 않기")]
        [SerializeField] Toggle _skipToggle;

        Action _onOk;
        Action _onCancel;
        Action _onClosed;
        /// <summary>취소 버튼을 눌렀을 때만 (ConfirmOpts.onCancelButton — 덮개 · 뒤로 · X로 닫히면 부르지 않는다)</summary>
        Action _onCancelButton;
        /// <summary>아직 답하지 않은 확인이 열려 있다 (닫힘 알림을 아직 보내지 않음)</summary>
        bool _pending;
        string _skipKey;
        bool _layoutKnown;
        float _windowBaseHeight;
        float _bodyBaseBottom;
        Coroutine _dangerLock;

        protected override void Awake()
        {
            base.Awake();
            _okButton.onClick.AddListener(OnOk);
            _cancelButton.onClick.AddListener(OnCancelButton);
            if (_dangerButton != null) _dangerButton.onClick.AddListener(OnOk);
            if (_okLabel == null) _okLabel = _okButton.GetComponentInChildren<TMP_Text>(true);
            if (_cancelLabel == null) _cancelLabel = _cancelButton.GetComponentInChildren<TMP_Text>(true);
            if (_dangerLabel == null && _dangerButton != null) _dangerLabel = _dangerButton.GetComponentInChildren<TMP_Text>(true);
            // 확인은 자기 소리(Confirm)를 낸다 — 누름 소리를 꺼 겹치지 않게 (받기 버튼과 같은 규칙)
            MuteTapSound(_okButton);
            MuteTapSound(_dangerButton);
            CacheLayout();
        }

        static void MuteTapSound(Component button)
        {
            if (button != null && button.TryGetComponent<PressScale>(out var press)) press.TapSound = false;
        }

        /// <summary>알림: 확인 버튼 하나 (혼자 있는 확인 = Primary)</summary>
        public void ShowMessage(string title, string body) => Show(title, body, null, default, false);

        public void ShowConfirm(string title, string body, Action onOk) => Show(title, body, onOk, default, true);

        public void ShowConfirm(string title, string body, Action onOk, string okLabel, string cancelLabel = DefaultCancelLabel) =>
            Show(title, body, onOk, new ConfirmOpts { okLabel = okLabel, cancelLabel = cancelLabel }, true);

        /// <summary>확인 · 취소 (모양 · 비용 줄 · 묻지 않기는 opts)</summary>
        public void Show(string title, string body, Action onOk, ConfirmOpts opts) => Show(title, body, onOk, opts, true);

        void Show(string title, string body, Action onOk, ConfirmOpts opts, bool showCancel)
        {
            // 답하지 않은 확인이 열려 있으면 이 창이 자리를 넘겨받는다 — 앞 확인은 새 내용을 다 채운 뒤 취소로 끝났다고 알린다
            var replacedCancel = _pending ? _onCancel : null;
            var replacedClosed = _pending ? _onClosed : null;
            Open();
            _titleText.text = title;
            _bodyText.text = body;
            _onOk = onOk;
            _onCancel = opts.onCancel;
            _onClosed = opts.onClosed;
            _onCancelButton = opts.onCancelButton;
            _pending = true;
            _skipKey = string.IsNullOrEmpty(opts.skipKey) ? null : opts.skipKey;

            bool danger = opts.style == ConfirmStyle.Danger && _dangerButton != null;
            string ok = string.IsNullOrEmpty(opts.okLabel) ? DefaultOkLabel : opts.okLabel;
            SetLabel(danger ? _dangerLabel : _okLabel, ok);
            SetLabel(_cancelLabel, string.IsNullOrEmpty(opts.cancelLabel) ? DefaultCancelLabel : opts.cancelLabel);
            _cancelButton.gameObject.SetActive(showCancel);
            _okButton.gameObject.SetActive(!danger);
            if (_dangerButton != null) _dangerButton.gameObject.SetActive(danger);

            bool cost = _costRow != null && opts.currency.HasValue && opts.cost > 0d;
            if (cost) FillCost(opts.currency.Value, opts.cost);
            bool skip = _skipToggle != null && _skipKey != null;
            if (_skipToggle != null) _skipToggle.isOn = false;
            ApplyLayout(cost, skip);

            if (danger) LockDanger();
            else StopDangerLock();

            RunSafe(replacedCancel);
            RunSafe(replacedClosed);
        }

        protected override void OnClose()
        {
            // 확인 없이 닫힘 (취소 · 덮개 · 뒤로 · X · 모두 닫기): 열려 있던 확인을 취소로 끝낸다 (확인으로 닫히면 OnOk가 먼저 비워 둔다)
            var cancel = _pending ? _onCancel : null;
            var closed = _pending ? _onClosed : null;
            ClearFlow();
            _skipKey = null;
            StopDangerLock();
            SetLabel(_okLabel, DefaultOkLabel);
            SetLabel(_dangerLabel, DefaultOkLabel);
            SetLabel(_cancelLabel, DefaultCancelLabel);
            RunSafe(cancel);
            RunSafe(closed);
        }

        void OnOk()
        {
            var callback = _onOk;
            var closed = _onClosed;
            // 확인으로 끝남: 닫을 때(OnClose) 취소로 알리지 않게 먼저 비운다
            ClearFlow();
            if (_skipKey != null && _skipToggle != null && _skipToggle.isOn) RememberSkipToday(_skipKey);
            UISfx.Play(UISfxKey.Confirm);
            Close();
            RunSafe(callback);
            RunSafe(closed);
        }

        /// <summary>
        /// 취소 버튼 누름: 창을 닫고 (취소로 끝남 — OnClose가 onCancel → onClosed를 알린다) 그다음 그 확인의 onCancelButton을 부른다.
        /// 덮개 · 뒤로 · X는 Close / RequestDismiss로 바로 가서 이것을 지나지 않는다. 답할 확인이 없으면(알림 · 이미 닫힘) 닫기만
        /// </summary>
        void OnCancelButton()
        {
            var pressed = _pending ? _onCancelButton : null;
            Close();
            RunSafe(pressed);
        }

        void ClearFlow()
        {
            _onOk = null;
            _onCancel = null;
            _onClosed = null;
            _onCancelButton = null;
            _pending = false;
        }

        /// <summary>알림 하나를 부른다 (null이면 무시, 예외는 기록만 — 뒤따르는 onClosed가 언제나 불리게). UIManager의 창 없는 길도 쓴다</summary>
        internal static void RunSafe(Action action)
        {
            if (action == null) return;
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ───────────── 오늘 다시 묻지 않기 ─────────────

        /// <summary>오늘 그 키로 '다시 묻지 않기'를 골랐는지 (키가 비면 false)</summary>
        public static bool IsSkippedToday(string skipKey)
        {
            if (string.IsNullOrEmpty(skipKey)) return false;
            string today = TodayKey();
            return !string.IsNullOrEmpty(today) && PlayerPrefs.GetString(skipKey, string.Empty) == today;
        }

        static void RememberSkipToday(string skipKey)
        {
            PlayerPrefs.SetString(skipKey, TodayKey());
            PlayerPrefs.Save();
        }

        /// <summary>오늘 키 (새벽 5시 기준 DailyClock.DayKey, 게임이 준비 안 됐으면 기기 날짜)</summary>
        static string TodayKey() => Game.IsReady && Game.DailyClock != null
            ? Game.DailyClock.DayKey
            : DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // ───────────── 비용 줄 · 배치 ─────────────

        void FillCost(CurrencyType currency, double cost)
        {
            if (_costIcon != null)
            {
                var icon = IconOf(currency);
                if (icon != null)
                {
                    _costIcon.sprite = icon;
                    _costIcon.color = UITheme.CurrencyIconTint(currency);
                }
                _costIcon.enabled = _costIcon.sprite != null;
            }
            if (_costText != null) _costText.text = RewardService.FormatAmount(currency, cost);
            if (_balanceText == null) return;
            double have = Game.IsReady && Game.Currency != null ? Game.Currency.Get(currency) : 0d;
            double after = have - cost;
            string haveText = RewardService.FormatAmount(currency, have);
            string afterText = RewardService.FormatAmount(currency, Math.Max(0d, after));
            _balanceText.text = after < 0d
                ? $"보유 {haveText} → <color={UITheme.ColorTag(UITheme.Negative)}>{afterText}</color>"
                : $"보유 {haveText} → {afterText}";
        }

        Sprite IconOf(CurrencyType currency)
        {
            int index = (int)currency;
            return _currencyIcons != null && index >= 0 && index < _currencyIcons.Length ? _currencyIcons[index] : null;
        }

        void CacheLayout()
        {
            if (_layoutKnown || _window == null || _bodyText == null) return;
            _windowBaseHeight = _window.sizeDelta.y;
            _bodyBaseBottom = _bodyText.rectTransform.offsetMin.y;
            _layoutKnown = true;
        }

        /// <summary>버튼 위에 묻지 않기 → 비용 줄 순서로 쌓고, 늘어난 만큼 창과 본문 아래 여백을 키운다</summary>
        void ApplyLayout(bool cost, bool skip)
        {
            CacheLayout();
            float y = RowsBottom;
            if (_skipToggle != null)
            {
                _skipToggle.gameObject.SetActive(skip);
                if (skip)
                {
                    SetBottom((RectTransform)_skipToggle.transform, y, SkipRowHeight);
                    y += SkipRowStep;
                }
            }
            if (_costRow != null)
            {
                _costRow.gameObject.SetActive(cost);
                if (cost)
                {
                    SetBottom(_costRow, y, CostRowHeight);
                    y += CostRowStep;
                }
            }
            if (!_layoutKnown) return;
            float extra = y - RowsBottom;
            _window.sizeDelta = new Vector2(_window.sizeDelta.x, _windowBaseHeight + extra);
            var body = _bodyText.rectTransform;
            body.offsetMin = new Vector2(body.offsetMin.x, _bodyBaseBottom + extra);
        }

        /// <summary>아래쪽 고정 가로 줄 (UIKit.Bottom과 같은 앵커)의 높이 · 자리만 바꾼다</summary>
        static void SetBottom(RectTransform row, float bottom, float height)
        {
            row.offsetMin = new Vector2(row.offsetMin.x, bottom);
            row.offsetMax = new Vector2(row.offsetMax.x, bottom + height);
        }

        static void SetLabel(TMP_Text label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        // ───────────── 위험 확인 잠금 ─────────────

        void LockDanger()
        {
            StopDangerLock();
            if (!isActiveAndEnabled) return;
            _dangerButton.interactable = false;
            _dangerLock = StartCoroutine(UnlockDangerLater());
        }

        IEnumerator UnlockDangerLater()
        {
            yield return new WaitForSecondsRealtime(DangerLockSeconds);
            _dangerLock = null;
            if (_dangerButton != null) _dangerButton.interactable = true;
        }

        void StopDangerLock()
        {
            if (_dangerLock != null)
            {
                StopCoroutine(_dangerLock);
                _dangerLock = null;
            }
            if (_dangerButton != null && !_dangerButton.interactable) _dangerButton.interactable = true;
        }
    }
}
