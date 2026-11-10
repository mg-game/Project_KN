using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 팝업 스택, 토스트, 뒤로가기, 접속 시 팝업 순서, 확인 · 부족 안내 창구.
    /// <para>
    /// 뒤로가기 (Android 뒤로 / PC Esc, UX-SPEC-IA §4.7 — UX-SPEC §3.2를 대신한다):
    /// ① 뒤로 처리기(PushBackHandler, 나중에 넣은 것부터 — 잠자기 · 동화 인트로 · 첫 만남 카드 · 빠른 아이콘 쟁반) → 열린 안내 말풍선 닫기
    /// → ② 맨 위 팝업 RequestDismiss (막는 팝업도 뒤로를 먹은 것으로 친다 — 종료 확인이 그 뒤에 숨어 뜨지 않게)
    /// → ③ 돌아가기 칩이 보일 때만 UINav.TryPopReturn (칸을 넣을 때의 틀로 되돌리고 다시 연다. 튜토리얼 · 코치마크 · 팝업으로 숨은 칩에는 걸리지 않는다, CR-05)
    /// → ④ 시트가 열려 있으면 MAIN으로 (SheetBackHandler — 머리를 끄는 중이면 끌기를 끝내고 지금 p에서 닫는다, CR-21)
    /// → ⑤ 종료 확인 (확인 창은 SystemPopups 층이라 튜토리얼 · 팡파르 위에 뜬다. 열려 있을 때 한 번 더 누르면 닫힌다).
    /// 시트가 닫히는 중에 한 번 더 누르면 바로 ⑤로 간다 (두 번 눌러 끄기는 없다).
    /// </para>
    /// <para>
    /// 닫기 세 가지: CloseTop = 맨 위 팝업에 닫아 달라고 요청 (RequestDismiss — 실제로 닫혔을 때만 true, 그래서 while(CloseTop())도 멈춘다) /
    /// CloseAll = 화면 이동 전에 위에서부터 모두 Close (닫기 연출 있음, 막는 팝업도 닫는다) /
    /// CloseAllImmediate = 도구 · 테스트용, 연출 · 닫기 정책 없이 모두 바로 끈다 (언제나 열린 팝업 0개로 끝난다).
    /// </para>
    /// <para>
    /// 토스트 (§3.5 M14): ShowToast(글, 종류)가 ToastView에 넘긴다. 줄 세우기 · 종류 모양 · 시간 · 자리는 ToastView가 한다 —
    /// 자리는 토스트가 나타날 때마다 맨 위 팝업 창(TopPopupWindowRect, Awake에서 ToastView.TopWindow로 연결)에 맞춘다.
    /// </para>
    /// 겹친 팝업은 덮개를 0.4까지만 (위 팝업만, 아래 덮개는 그대로 — M48). 접속 팝업은 앞 창이 닫힌 뒤 0.18초(UIMotion.StartupGap) 쉬고 다음 창을 연다.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] UIPopup[] _popups;
        [SerializeField] ToastView _toast;
        [Tooltip("돌아가기 칩 (SafeArea/ContentArea/SheetHeader/ReturnChip) — 뒤로가기는 칩이 보일 때만 돌아간다. 비면 처음 한 번 장면에서 찾는다")]
        [SerializeField] ReturnChipView _returnChip;

        /// <summary>모두 닫기 반복 상한 (닫히면서 다른 팝업을 여는 경우)</summary>
        const int MaxCloseLoop = 64;

        public const string ExitTitle = "수선점 문을 닫을까요?";
        public const string ExitBody = "자리를 비워도 친구들이 바구니에 선물을 모아 둬요.";
        /// <summary>종료 확인의 '닫기' (게임을 끈다) — 왼쪽 Secondary (확인 창의 취소 자리, Wave 2 r2: 안전한 '머물기'를 강조)</summary>
        public const string ExitOkLabel = "닫기";
        /// <summary>종료 확인의 '더 지킬래요' (머문다) — 오른쪽 Primary (확인 창의 확인 자리). 덮개 · 뒤로 · X도 머물기다</summary>
        public const string ExitCancelLabel = "더 지킬래요";

        readonly List<UIPopup> _stack = new();
        readonly List<Func<bool>> _backHandlers = new();
        readonly List<Func<bool>> _backScratch = new();
        string _startupMessageTitle;
        string _startupMessageBody;
        float _nextGoldShortfallToast;
        Coroutine _basketPunch;
        bool _returnChipSearched;

        /// <summary>열린 팝업이 있는지 (닫히는 연출 중인 팝업은 세지 않는다)</summary>
        public bool HasOpenPopup => _stack.Count > 0;

        /// <summary>
        /// 맨 위에 보이는 열린 팝업 (위 층 SystemPopups가 먼저, 같은 층이면 나중에 연 것). 없으면 null.
        /// 뒤로가기 · CloseTop · 토스트 자리가 이것을 쓴다.
        /// </summary>
        public UIPopup TopPopup
        {
            get
            {
                UIPopup best = null;
                int bestLayer = int.MinValue;
                for (int i = _stack.Count - 1; i >= 0; i--)
                {
                    var popup = _stack[i];
                    if (popup == null) continue;
                    var parent = popup.transform.parent;
                    int layer = parent != null ? parent.GetSiblingIndex() : 0;
                    if (layer > bestLayer)
                    {
                        best = popup;
                        bestLayer = layer;
                    }
                }
                return best;
            }
        }

        /// <summary>맨 위 팝업의 창 (UIPopup.Window). 열린 팝업이 없으면 null — 토스트 · 안내가 창을 피할 때 쓴다</summary>
        public RectTransform TopPopupWindowRect
        {
            get
            {
                var top = TopPopup;
                return top != null ? top.Window : null;
            }
        }

        /// <summary>
        /// 접속 팝업 처리기. 메타 UI의 발표자(MetaPopupPresenter, M3)가 Awake에서 등록한다.
        /// 등록되어 있고 메타가 켜져 있으면(Game.IsMetaActive) 안내 메시지 다음 순서를 이 코루틴에 맡긴다
        /// (PopupQueue: 복귀 화면 → 출석부 → 공지 → 이벤트). 공지는 ShowStartupNotices를 다시 써도 된다.
        /// 없으면 예전 순서 (오프라인 보상 → 팝업 공지).
        /// </summary>
        public Func<IEnumerator> StartupHandler { get; set; }

        void Awake()
        {
            Instance = this;
            foreach (var popup in _popups)
            {
                if (popup != null) popup.gameObject.SetActive(false);
            }
            // 토스트는 나타날 때마다 맨 위 팝업 창을 물어 자리를 정한다 (§3.5)
            if (_toast != null) _toast.TopWindow = () => TopPopupWindowRect;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // 장면이 내려간다 (데이터 초기화 · 테스트의 장면 다시 읽기): 정적인 돌아가기 칸이 옛 장면 팝업을 붙잡은 채 새 장면으로 넘어가지 않게
            UINav.ClearReturn();
        }

        void Update()
        {
            // Android 뒤로가기 / PC Esc
            if (Input.GetKeyDown(KeyCode.Escape)) OnBack();
        }

        public T Get<T>() where T : UIPopup
        {
            if (TryGet<T>(out var match)) return match;
            Debug.LogError($"[UI] {typeof(T).Name} 팝업이 등록되어 있지 않습니다.");
            return null;
        }

        /// <summary>그 종류의 팝업 (없으면 false, 오류 로그 없음 — 씬을 다시 빌드하기 전에 새 팝업을 찾을 때)</summary>
        public bool TryGet<T>(out T popup) where T : UIPopup
        {
            if (_popups != null)
            {
                foreach (var candidate in _popups)
                {
                    if (candidate is T match)
                    {
                        popup = match;
                        return true;
                    }
                }
            }
            popup = null;
            return false;
        }

        // ═════════════════════════ 토스트 (M14) ═════════════════════════

        /// <summary>
        /// 토스트 (종류는 글자로 짐작: '부족' · '잠겨' · '아직 열리지' · '만료' · '클리어하면'(잠금 안내 MetaText.UnlockSentence)이
        /// 들어 있으면 Error, 그 밖은 Info). 받기 · 얻음 같은 좋은 소식은 ShowToast(message, ToastKind.Success)로 부른다.
        /// </summary>
        public void ShowToast(string message) => ShowToast(message, GuessToastKind(message));

        /// <summary>토스트 (종류: Info 라벤더 점 / Success 체크 / Error '!' + 막힘 소리 · 약한 진동 — 줄 세우기 · 같은 글 합치기는 ToastView)</summary>
        public void ShowToast(string message, ToastKind kind)
        {
            if (_toast == null || string.IsNullOrEmpty(message)) return;
            _toast.Show(message, kind);
        }

        /// <summary>글자로 토스트 종류를 짐작한다 (막힘 · 모자람 · 잠김 · 만료 = Error — §3.5의 바꿀 목록)</summary>
        public static ToastKind GuessToastKind(string message)
        {
            if (string.IsNullOrEmpty(message)) return ToastKind.Info;
            return message.IndexOf("부족", StringComparison.Ordinal) >= 0
                   || message.IndexOf("잠겨", StringComparison.Ordinal) >= 0
                   || message.IndexOf("아직 열리지", StringComparison.Ordinal) >= 0
                   || message.IndexOf("만료", StringComparison.Ordinal) >= 0
                   || message.IndexOf("클리어하면", StringComparison.Ordinal) >= 0
                ? ToastKind.Error
                : ToastKind.Info;
        }

        // ═════════════════════════ 확인 · 쓰기 확인 · 부족 (M15 · M55 · M08) ═════════════════════════

        public void ShowMessage(string title, string body)
        {
            var popup = Get<MessagePopup>();
            if (popup != null) popup.ShowMessage(title, body);
        }

        /// <summary>
        /// 확인 · 취소 (취소는 왼쪽 Secondary, 확인은 오른쪽 Primary — 자리는 바뀌지 않는다). 이름은 닫힐 때 '확인' / '취소'로 되돌아간다.
        /// 행동 버튼 이름은 동사로 (구매 · 소환 · 수선 · 종료 …).
        /// <para>
        /// 닫힘 알림 (세 확인 공통, 둘 다 생략 가능): onCancel = 확인 없이 끝남 (취소 · 덮개 · 뒤로 · X · 다른 확인이 자리를 넘겨받음 · 모두 닫기),
        /// onClosed = 어떻게 끝나든 정확히 한 번 (확인이면 onOk 다음, 취소면 onCancel 다음). 창을 띄우지 못하면(확인 창 없음 · 쓰기 확인의
        /// 부족 · 오늘 묻지 않기) 그 자리에서 바로 부른다 — 확인 전에 버튼을 막고 onClosed에서 푸는 쪽이 그대로 쓸 수 있다.
        /// </para>
        /// </summary>
        public void ShowConfirm(string title, string body, Action onOk, string okLabel = MessagePopup.DefaultOkLabel,
            string cancelLabel = MessagePopup.DefaultCancelLabel, Action onCancel = null, Action onClosed = null)
        {
            var popup = Get<MessagePopup>();
            if (popup == null)
            {
                MessagePopup.RunSafe(onClosed);
                return;
            }
            popup.Show(title, body, onOk, new ConfirmOpts
            {
                okLabel = okLabel,
                cancelLabel = cancelLabel,
                onCancel = onCancel,
                onClosed = onClosed,
            });
        }

        /// <summary>
        /// 재화를 쓰기 전 확인 (§3.7): 비용 줄(아이콘 · 비용 · '보유 4,450 → 3,550')과, skipKey가 있으면 '오늘 다시 묻지 않기'.
        /// 모자라면 확인 창을 띄우지 않고 부족 안내로 보낸다 (ShowShortfall) → false (onCancel 없이 onClosed만 바로).
        /// skipKey로 오늘 묻지 않기로 했으면 창 없이 바로 onOk → onClosed → true. 창을 띄우면 true (닫힐 때 onOk 또는 onCancel, 그다음 onClosed).
        /// 확인 창이 없는 장면이면 false (onClosed만 바로). 닫힘 알림은 ShowConfirm 설명과 같다.
        /// </summary>
        public bool ShowSpendConfirm(string title, string body, CurrencyType currency, double cost, string okLabel, Action onOk,
            string skipKey = null, Action onCancel = null, Action onClosed = null)
        {
            if (!CanAfford(currency, cost))
            {
                ShowShortfall(currency, cost);
                MessagePopup.RunSafe(onClosed);
                return false;
            }
            if (MessagePopup.IsSkippedToday(skipKey))
            {
                MessagePopup.RunSafe(onOk);
                MessagePopup.RunSafe(onClosed);
                return true;
            }
            var popup = Get<MessagePopup>();
            if (popup == null)
            {
                MessagePopup.RunSafe(onClosed);
                return false;
            }
            popup.Show(title, body, onOk, new ConfirmOpts
            {
                okLabel = okLabel,
                style = ConfirmStyle.Spend,
                currency = currency,
                cost = cost,
                skipKey = skipKey,
                onCancel = onCancel,
                onClosed = onClosed,
            });
            return true;
        }

        /// <summary>
        /// 되돌릴 수 없는 일 확인 (데이터 초기화 · 튜토리얼 건너뛰기 · 계정 해제 · 원정 취소): 확인 버튼은 DangerButton + Cream 글자,
        /// 열린 뒤 0.6초 동안 누를 수 없다 (빠른 두 번 누름 방지). 자리는 바뀌지 않는다. 닫힘 알림은 ShowConfirm 설명과 같다.
        /// </summary>
        public void ShowDangerConfirm(string title, string body, string okLabel, Action onOk, string cancelLabel = MessagePopup.DefaultCancelLabel,
            Action onCancel = null, Action onClosed = null)
        {
            var popup = Get<MessagePopup>();
            if (popup == null)
            {
                MessagePopup.RunSafe(onClosed);
                return;
            }
            popup.Show(title, body, onOk, new ConfirmOpts
            {
                okLabel = okLabel,
                cancelLabel = cancelLabel,
                style = ConfirmStyle.Danger,
                onCancel = onCancel,
                onClosed = onClosed,
            });
        }

        /// <summary>
        /// 재화가 모자랄 때 (§3.8 M08): 단추는 팝업 없이 토스트 '단추 N개가 더 필요해요 · 약 2분' (1.5초에 한 번, 바구니에 모자란 만큼 있으면
        /// ' · 바구니를 받아 보세요' + 바구니 한 번 튀기기). 별조각 · 별빛 사탕은 부족 팝업(필요 · 보유 · 얻는 곳 4줄).
        /// need ≤ 0이면 '얻는 곳' 모드 (단추도 팝업 — 상단 바 단추 칩).
        /// need &gt; 0인데 이미 그만큼 있으면 아무것도 띄우지 않는다 (모든 재화 공통 — 부르는 쪽이 늦게 확인함: 우편 · 날아온 별조각이 막 들어온 직후의
        /// '모자람' 버튼 등. '부족해요' 제목에 모자란 줄이 없는 팝업이 되지 않게). 버튼 모양은 부르는 쪽의 다음 갱신이 맞춘다.
        /// </summary>
        public void ShowShortfall(CurrencyType type, double need)
        {
            double have = Game.IsReady && Game.Currency != null ? Game.Currency.Get(type) : 0d;
            // 이미 모자라지 않으면 알릴 것이 없다
            if (need > 0d && !(have < need)) return;
            if (type == CurrencyType.Gold && need > 0d)
            {
                ShowGoldShortfall(need, have);
                return;
            }
            if (TryGet<ShortfallPopup>(out var popup))
            {
                popup.Show(type, need);
                return;
            }
            // 다시 빌드하기 전 씬 (부족 팝업이 없음)
            if (need > 0d)
            {
                string name = RewardService.CurrencyName(type);
                ShowToast($"{KoreanText.WithSubject(name)} 부족해요. (보유 {RewardService.FormatAmount(type, have)} / 필요 {RewardService.FormatAmount(type, need)})",
                    ToastKind.Error);
            }
        }

        static bool CanAfford(CurrencyType currency, double cost) =>
            !(cost > 0d) || !Game.IsReady || Game.Currency == null || Game.Currency.Has(currency, cost);

        void ShowGoldShortfall(double need, double have)
        {
            float now = Time.unscaledTime;
            if (now < _nextGoldShortfallToast) return;
            _nextGoldShortfallToast = now + UIMotion.ShortfallToastThrottle;
            ShowToast(MetaText.GoldShortfall(need, have), ToastKind.Error);

            double gap = Math.Max(0d, Math.Ceiling(need - have - 1e-9));
            if (gap > 0d && Game.IsMetaActive && Game.IdleChest != null && Game.IdleChest.PendingGold >= gap) PunchBasketWidget();
        }

        /// <summary>바구니 위젯을 한 번 툭 튀긴다 (0.08 / 0.22초, 원래 크기 기준)</summary>
        void PunchBasketWidget()
        {
            if (_basketPunch != null || !isActiveAndEnabled) return;
            var widget = UIFind.Path(transform, CoachTargets.IdleChestWidget);
            if (!UIFind.IsShown(widget)) return;
            _basketPunch = StartCoroutine(Punch(widget, UIMotion.PunchS, UIMotion.PunchDuration));
        }

        IEnumerator Punch(RectTransform target, float strength, float duration)
        {
            var baseScale = target.localScale;
            for (float t = 0f; t < duration && target != null; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                target.localScale = baseScale * (1f + strength * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                yield return null;
            }
            if (target != null) target.localScale = baseScale;
            _basketPunch = null;
        }

        // ═════════════════════════ 팝업 스택 ═════════════════════════

        /// <summary>
        /// 맨 위 팝업에 닫아 달라고 요청한다 (RequestDismiss — 소환 결과는 먼저 건너뛰기, 받는 중인 복귀 화면은 무시).
        /// 그 팝업이 실제로 닫혔을 때만 true — 열린 팝업이 없거나 팝업이 막으면 false (그래서 while(CloseTop())은 멈춘다).
        /// 모두 닫아야 하면 CloseAll(화면 이동) · CloseAllImmediate(도구 · 테스트)를 쓴다.
        /// </summary>
        public bool CloseTop()
        {
            var top = TopPopup;
            if (top == null) return false;
            top.RequestDismiss();
            return !top.IsOpen;
        }

        /// <summary>
        /// 열린 팝업을 위에서부터 모두 Close한다 (닫기 연출 있음, 닫기 정책은 거치지 않는다 — 다른 탭 · 화면으로 이동하기 전에).
        /// 닫힌 수를 돌려준다.
        /// </summary>
        public int CloseAll()
        {
            int closed = 0;
            for (int guard = 0; guard < MaxCloseLoop && _stack.Count > 0; guard++)
            {
                var top = TopPopup;
                if (top == null)
                {
                    _stack.RemoveAll(Missing);
                    continue;
                }
                top.Close();
                if (top.IsOpen) break;
                closed++;
            }
            return closed;
        }

        /// <summary>
        /// 모든 팝업을 연출 · 닫기 정책 없이 바로 끈다 (AutoShot · 테스트의 "모두 닫기"). 닫히는 중인 팝업도 끝내고 안내 말풍선도 닫는다.
        /// 닫히면서 다시 여는 팝업이 있어도 마지막에는 장부에서 빼고 끈다 — 끝나면 언제나 HasOpenPopup == false. 닫은 수를 돌려준다.
        /// </summary>
        public int CloseAllImmediate()
        {
            int closed = 0;
            for (int guard = 0; guard < MaxCloseLoop && _stack.Count > 0; guard++)
            {
                var top = _stack[_stack.Count - 1];
                if (top == null)
                {
                    _stack.RemoveAt(_stack.Count - 1);
                    continue;
                }
                top.CloseImmediate();
                closed++;
                // 자기 Closed 처리기에서 다시 열렸으면 처리기 없이 내린다 (무한 반복 방지)
                if (top.IsOpen && _stack.Count > 0 && _stack[_stack.Count - 1] == top) ForceHide(top);
            }
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i] != null) ForceHide(_stack[i]);
            }
            _stack.Clear();
            if (_popups != null)
            {
                foreach (var popup in _popups)
                {
                    if (popup != null && popup.IsClosing) popup.CloseImmediate();
                }
            }
            if (InfoTooltipView.IsOpen) InfoTooltipView.Hide();
            return closed;
        }

        void ForceHide(UIPopup popup)
        {
            _stack.Remove(popup);
            popup.ForceHidden();
        }

        static readonly Predicate<UIPopup> Missing = popup => popup == null;

        internal void OnPopupOpened(UIPopup popup)
        {
            _stack.Remove(popup);
            _stack.Add(popup);
            popup.transform.SetAsLastSibling();
            popup.SetDimTarget(_stack.Count > 1 ? UIMotion.DimStacked : UIMotion.DimSingle);
        }

        internal void OnPopupClosed(UIPopup popup) => _stack.Remove(popup);

        // ═════════════════════════ 뒤로가기 (M15) ═════════════════════════

        /// <summary>
        /// 뒤로 처리기를 넣는다 (나중에 넣은 것부터 확인). 처리기가 true를 돌려주면 뒤로가기를 먹은 것 — 팝업 · 종료 확인으로 가지 않는다.
        /// 같은 대리자를 다시 넣으면 맨 뒤(가장 먼저 확인)로 옮긴다. 지울 때도 같은 대리자 인스턴스를 넘긴다 (람다는 필드에 담아 둔다).
        /// </summary>
        public void PushBackHandler(Func<bool> handler)
        {
            if (handler == null) return;
            _backHandlers.Remove(handler);
            _backHandlers.Add(handler);
        }

        public void RemoveBackHandler(Func<bool> handler)
        {
            if (handler != null) _backHandlers.Remove(handler);
        }

        /// <summary>
        /// 시트 뒤로 처리기 (UX-SPEC-IA §4.7 4단계): 시트가 열려 있으면(UIFrame.SheetOpen) 머리 끌기를 먼저 끝내고 MAIN으로 내린 뒤 true,
        /// 아니면 false. MainTabController가 넣는다 (Start). 뒤로 순서: 뒤로 처리기 → 안내 말풍선 → 맨 위 팝업 →
        /// 돌아가기 칩(보일 때만 UINav.TryPopReturn) → 이것 → 종료 확인. 닫히는 중에 한 번 더 누르면 종료 확인으로 간다.
        /// 넣은 주인이 사라졌으면(장면 전환) 부르지 않고 지운다.
        /// </summary>
        public Func<bool> SheetBackHandler { get; set; }

        /// <summary>뒤로가기 한 번 (Esc · Android 뒤로가 부른다 — 검증 도구가 직접 불러도 된다). 순서는 클래스 설명 (§4.7)</summary>
        public void OnBack()
        {
            // ① 뒤로 처리기 (나중에 넣은 것부터) → 열린 안내 말풍선
            if (RunBackHandlers()) return;
            if (InfoTooltipView.IsOpen)
            {
                InfoTooltipView.Hide();
                return;
            }
            // ② 맨 위 팝업
            var top = TopPopup;
            if (top != null)
            {
                top.RequestDismiss();
                return;
            }
            // ③ 돌아가기 칩 — 보일 때만 (숨은 칩에 뒤로가기가 걸리지 않게, CR-05). 뒤로를 먹는다
            if (UINav.HasReturn && ReturnChipShown())
            {
                UINav.TryPopReturn();
                return;
            }
            // ④ 시트 → MAIN
            if (RunSheetBackHandler()) return;
            // ⑤ 종료 확인
            ShowExitConfirm();
        }

        /// <summary>
        /// 종료 확인 (Wave 2 r2 · UX-SPEC 부록 D M15): 오른쪽 Primary = '더 지킬래요' (확인 자리, 머물기), 왼쪽 Secondary = '닫기' (취소 자리).
        /// 끄기는 그 취소 *버튼*을 누를 때만 (ConfirmOpts.onCancelButton = Quit) — 덮개 · 뒤로 · X · 다른 확인이 넘겨받음 · 모두 닫기는 모두 머물기.
        /// 확인 창이 없는 장면이면 아무것도 띄우지 않는다 (머물기).
        /// </summary>
        void ShowExitConfirm()
        {
            var popup = Get<MessagePopup>();
            if (popup == null) return;
            popup.Show(ExitTitle, ExitBody, null, new ConfirmOpts
            {
                okLabel = ExitCancelLabel,
                cancelLabel = ExitOkLabel,
                onCancelButton = Quit,
            });
        }

        /// <summary>돌아가기 칩이 지금 보이는지 (칩이 켜진 시트 안에 있고 숨김 조건이 없음). 칩이 없는 장면이면 false</summary>
        bool ReturnChipShown()
        {
            if (_returnChip == null && !_returnChipSearched)
            {
                _returnChipSearched = true;
                _returnChip = FindFirstObjectByType<ReturnChipView>(FindObjectsInactive.Include);
            }
            // 꺼진 시트 안의 칩은 마지막 보임 값을 들고 있을 수 있다 — 켜져 있을 때만 믿는다
            return _returnChip != null && _returnChip.isActiveAndEnabled && _returnChip.IsVisible;
        }

        bool RunSheetBackHandler()
        {
            var handler = SheetBackHandler;
            if (handler == null) return false;
            // 넣은 주인이 사라졌으면 (씬 전환 · 파괴) 지운다
            if (handler.Target is UnityEngine.Object owner && owner == null)
            {
                SheetBackHandler = null;
                return false;
            }
            try
            {
                return handler();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        bool RunBackHandlers()
        {
            if (_backHandlers.Count == 0) return false;
            _backScratch.Clear();
            _backScratch.AddRange(_backHandlers);
            for (int i = _backScratch.Count - 1; i >= 0; i--)
            {
                var handler = _backScratch[i];
                if (!_backHandlers.Contains(handler)) continue;
                // 처리기의 주인이 사라졌으면 (씬 전환 · 파괴) 지운다
                if (handler.Target is UnityEngine.Object owner && owner == null)
                {
                    _backHandlers.Remove(handler);
                    continue;
                }
                bool handled;
                try
                {
                    handled = handler();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    continue;
                }
                if (handled)
                {
                    _backScratch.Clear();
                    return true;
                }
            }
            _backScratch.Clear();
            return false;
        }

        // ═════════════════════════ 접속 팝업 ═════════════════════════

        /// <summary>접속 팝업보다 먼저 보여줄 안내 (예: 세이브 복구 결과). ShowStartupPopups 전에 부른다.</summary>
        public void QueueStartupMessage(string title, string body)
        {
            _startupMessageTitle = title;
            _startupMessageBody = body;
        }

        /// <summary>접속 시 팝업: 안내 메시지 → (메타) StartupHandler 또는 (예전) 오프라인 보상 → 팝업 공지 (한 장씩)</summary>
        public void ShowStartupPopups() => StartCoroutine(StartupSequence());

        IEnumerator StartupSequence()
        {
            if (!string.IsNullOrEmpty(_startupMessageTitle))
            {
                var message = Get<MessagePopup>();
                if (message != null)
                {
                    message.ShowMessage(_startupMessageTitle, _startupMessageBody);
                    _startupMessageTitle = _startupMessageBody = null;
                    yield return new WaitWhile(() => message.IsOpen);
                    // 닫히는 연출(0.12초)이 끝난 뒤 다음 창 (덮개 · 창이 겹치지 않게, M48)
                    yield return new WaitForSecondsRealtime(UIMotion.StartupGap);
                }
            }

            if (!Game.IsReady) yield break;
            if (Game.IsMetaActive && StartupHandler != null)
            {
                yield return StartupHandler();
                yield break;
            }

            if (Game.OfflineReward.HasPending)
            {
                var offline = Get<OfflineRewardPopup>();
                if (offline != null)
                {
                    offline.Open();
                    yield return new WaitWhile(() => offline.IsOpen);
                    yield return new WaitForSecondsRealtime(UIMotion.StartupGap);
                }
                if (!Game.IsReady) yield break;
            }

            yield return ShowStartupNotices();
        }

        /// <summary>
        /// 접속 공지 한 장 (M50): 읽지 않은 첫 팝업 공지(NoticeSystem.GetStartupNoticeToShow)만 띄우고 닫힐 때까지 기다린다.
        /// 오늘 하루 숨김이거나 읽지 않은 팝업 공지가 없으면 아무것도 띄우지 않는다 — 나머지 공지는 공지 목록 · 설정의 점으로만 알린다.
        /// 메타 발표자가 공지 차례(접속 모달 한 자리)에 다시 쓴다. 띄웠으면 닫힌 뒤 접속 쉼(0.18초)까지 기다리고 돌아간다 —
        /// 바로 다음 창(이벤트 카드 등)이 닫히는 공지 위에 겹쳐 열리지 않게 (M48).
        /// </summary>
        public IEnumerator ShowStartupNotices()
        {
            if (!Game.IsReady || Game.Notice == null) yield break;
            var notice = Game.Notice.GetStartupNoticeToShow();
            if (notice == null) yield break;
            var popup = Get<NoticePopup>();
            if (popup == null) yield break;
            popup.OpenStartup(notice);
            yield return new WaitWhile(() => popup.IsOpen);
            yield return new WaitForSecondsRealtime(UIMotion.StartupGap);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
