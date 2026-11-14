using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 메타 팝업 발표자 (F-05). PopupQueue(순수 로직)에 쌓인 요청을 실제 팝업으로 띄운다. 항상 켜진 오브젝트에 붙는다.
    ///   · 접속: Awake에서 UIManager.StartupHandler를 등록한다 → 안내 메시지 다음에 큐의 복귀 화면 → (다시 만난 날) → 출석부를
    ///     한 장씩 띄우고, 공지 차례에 튜토리얼이 끝났으면 TryReserveStartupModal 후 UIManager.ShowStartupNotices(),
    ///     그다음 이벤트 시작 카드, 마지막에 EndStartupBatch. 튜토리얼 중에는 공지를 띄우지 않는다.
    ///   · 복귀(OnResume): MetaSession이 접속 묶음을 다시 열면 같은 순서로 (공지 제외) 띄운다.
    ///   · 세션 중: 매 프레임 가벼운 확인으로 TryDequeue(스테이지 사이, 열린 팝업 없음) → 기능 개방 카드 / 졸업 카드(튜토리얼 UI의 GraduationCardPopup).
    ///     졸업 카드는 FtueDirector도 꺼낼 수 있다 — 먼저 꺼낸 쪽이 띄운다.
    ///     개방 카드를 거절해 그림이 자리로 날아가는 동안(최대 FeatureUnlockCard.FlightHoldMax 0.8초)은 다음 팝업을 붙잡는다 (M78).
    ///   · 공지는 접속마다 많아야 한 장 (M50 — NoticeSystem.GetStartupNoticeToShow가 있을 때만 UIManager.ShowStartupNotices).
    ///   · 창이 닫히면 0.18초(UIMotion.StartupGap) 쉬고 다음 창을 연다 (M48 — ShowStartupNotices는 안에서 이미 쉰다).
    ///   · 접속 출석부는 자동 도장 (AttendancePopup.OpenAuto, M69 — 빠른 아이콘 · 메뉴 칸은 그냥 Open).
    ///   · 짧은 복귀(복귀 화면 기준 미만)는 토스트만 ("자리를 비운 3분 동안 바구니에 단추 1.2K만큼 쌓였어요").
    /// 4차 (SPEC4 §2.9 · RM-1 요청, RM-2 P4-6):
    ///   · 다시 만난 날(Comeback → ComebackCard) · 이벤트 시작(EventStart → EventStartCard) · 챕터 상자(ChapterChest → ChapterChestPopup) ·
    ///     동화책(StoryPage → StoryPagePopup) · 알림 묻기(NotificationAsk → NotificationAskPopup). 팝업은 UIManager.TryGet으로 찾는다 —
    ///     장면을 다시 만들기 전(G3)이라 없으면 건너뛴다. 띄울 수 없는 요청(이미 열었음 · 내용 없음)도 건너뛴다.
    ///   · 개방 카드 간격 (SPEC5 §2.8): FeatureUnlock을 꺼내면 먼저 Features.ShouldDropCard(이미 들어가 봄)면 버리고, CardReady가 아니면
    ///     발표자 곁 대기 줄(_parked)로 옮겨 다른 종류를 막지 않는다. 대기 줄은 매 확인 때 앞에서부터 보고(버릴 것 · 이제 될 것), 카드가 닫히면 NotifyCardClosed.
    ///   · 튜토리얼 동안 4차 종류는 띄우지 않고 대기 줄에서 기다린다 (튜토리얼이 끝나면 스테이지 사이에). 대기 줄도 큐처럼 같은 요청은 하나.
    ///   · 알림 묻기 감지 (SPEC4 §4.3): 1초마다 Game.IdleChest.IsFull을 보고 (할당 없음) 튜토리얼 뒤 처음 가득 찼을 때 · 가득 찬 바구니의
    ///     복귀 화면을 닫은 뒤 NotificationAskPopup.ShouldAskNow면 NotificationAsk를 큐에 넣는다.
    /// 메타가 꺼져 있으면 아무것도 하지 않는다 (UIManager가 예전 흐름을 쓴다).
    /// 팝업 하나가 예외를 내도 줄이 멈추지 않는다: 모든 흐름은 Guarded로 돌려 예외를 기록하고 건너뛰며,
    /// _busy는 finally에서 항상 풀린다. 접속 묶음 도중 예외가 나면 묶음을 닫아 남은 요청이 세션 흐름으로 넘어가게 한다.
    /// </summary>
    public class MetaPopupPresenter : MonoBehaviour
    {
        /// <summary>접속 처리기를 못 받았을 때(공지 로딩이 아주 늦는 등) 혼자 시작하기까지 기다리는 시간</summary>
        const float BootFallbackSeconds = 20f;
        const long ShortReturnToastSeconds = 60;

        [SerializeField] UIManager _ui;
        [SerializeField] ReturnPopup _returnPopup;
        [SerializeField] AttendancePopup _attendancePopup;
        [SerializeField] FeatureUnlockCard _unlockCard;

        /// <summary>알림 묻기 감지 간격 (초)</summary>
        const float NotifyPollSeconds = 1f;
        /// <summary>대기 줄 최대 길이 (넘치면 가장 오래된 개방 카드부터 버린다 — 점은 남는다)</summary>
        const int MaxParked = 8;

        Func<IEnumerator> _startupHandler;
        bool _bootHandled;
        bool _busy;
        bool _subscribed;
        float _bootWait;

        /// <summary>지금 띄울 수 없어 기다리는 요청 (개방 카드 간격 · 튜토리얼 중 4차 종류). 순서 = 들어온 순서</summary>
        readonly List<MetaPopupRequest> _parked = new(MaxParked);
        float _notifyPoll;
        /// <summary>지난 확인 때 바구니가 가득이었는지 (null = 아직 모름 — 첫 확인은 '가득 됨'으로 세지 않는다)</summary>
        bool? _wasFull;

        static MetaPopupPresenter s_instance;

        /// <summary>
        /// 그 기능의 개방 카드가 발표자 곁에서 간격을 기다리는지 (QuickIconView가 아이콘을 숨겨 두는 동안 묻는다 — 큐의 FeatureUnlock과 같은 뜻). 할당 없음
        /// </summary>
        public static bool IsCardParked(FeatureId feature)
        {
            var self = s_instance;
            if (self == null) return false;
            var parked = self._parked;
            for (int i = 0; i < parked.Count; i++)
            {
                if (parked[i].Kind == MetaPopupKind.FeatureUnlock && parked[i].Feature == feature) return true;
            }
            return false;
        }

        /// <summary>기다리는 요청 수 (테스트 · 도구)</summary>
        public int ParkedCount => _parked.Count;

        void Awake()
        {
            s_instance = this;
            _startupHandler = RunBootStartup;
            if (_ui != null) _ui.StartupHandler = _startupHandler;
        }

        void Start()
        {
            if (!MetaUi.IsActive) return;
            Game.MetaSession.Returned += OnReturned;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (_ui != null && _ui.StartupHandler == _startupHandler) _ui.StartupHandler = null;
            if (_subscribed && Game.IsReady && Game.MetaSession != null) Game.MetaSession.Returned -= OnReturned;
        }

        IEnumerator RunBootStartup()
        {
            _bootHandled = true;
            if (!MetaUi.IsActive) yield break;
            // 세션 중 확인(Update)이 이미 무언가를 띄우는 중이면 끝날 때까지 기다린다
            while (_busy) yield return null;
            _busy = true;
            try
            {
                // UIManager의 코루틴 안에서 돌기 때문에, 예외가 새면 UIManager 접속 흐름까지 끊긴다 → Guarded로 안에서 잡는다
                yield return Guarded(StartupRoutine(true), true);
            }
            finally
            {
                _busy = false;
            }
        }

        void Update()
        {
            if (!MetaUi.IsActive) return;
            PollNotificationAsk(Time.unscaledDeltaTime);
            if (_busy) return;
            if (!_bootHandled)
            {
                _bootWait += Time.unscaledDeltaTime;
                if (_bootWait < BootFallbackSeconds) return;
                _bootHandled = true;
            }

            var queue = Game.Popups;
            if (queue.InStartupBatch)
            {
                StartCoroutine(Run(StartupRoutine(false), true));
                return;
            }
            bool between = Game.Battle.IsBetweenStages;
            bool popupOpen = _ui.HasOpenPopup;
            // 대기 줄 먼저 (버릴 카드는 버리고, 이제 될 것이 있으면 띄운다)
            if (_parked.Count > 0 && TakeParked(between, popupOpen, out var parked))
            {
                StartCoroutine(Run(Show(parked), false));
                return;
            }
            if (queue.IsEmpty) return;
            if (!queue.TryDequeue(between, popupOpen, out var request)) return;
            if (!Admit(request)) return;
            StartCoroutine(Run(Show(request), false));
        }

        // ───────────── 개방 카드 간격 · 튜토리얼 중 기다리기 ─────────────

        /// <summary>
        /// 큐에서 꺼낸 요청을 지금 띄울지: 이미 들어가 본 기능의 카드 → 버림 (false), 간격이 안 된 카드 · 튜토리얼 중 4차 종류 → 대기 줄 (false), 그 밖 → true
        /// </summary>
        bool Admit(MetaPopupRequest request)
        {
            if (request.Kind == MetaPopupKind.FeatureUnlock)
            {
                var features = Game.Features;
                if (features != null && features.ShouldDropCard(request.Feature)) return false;
                if (features != null && !features.CardReady(request.Feature))
                {
                    Park(request);
                    return false;
                }
                return true;
            }
            if (IsSuppressedByTutorial(request.Kind))
            {
                Park(request);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 대기 줄에서 지금 띄울 것을 하나 꺼낸다 (앞에서부터): 개방 카드는 ShouldDropCard면 지우고 CardReady면 꺼낸다,
        /// 4차 종류는 튜토리얼이 끝났으면 꺼낸다. 띄우는 것은 스테이지 사이 · 다른 팝업 없음일 때만 (버리기는 언제나). 할당 없음
        /// </summary>
        bool TakeParked(bool betweenStages, bool popupOpen, out MetaPopupRequest request)
        {
            request = default;
            var features = Game.Features;
            for (int i = 0; i < _parked.Count; i++)
            {
                var item = _parked[i];
                if (item.Kind == MetaPopupKind.FeatureUnlock)
                {
                    if (features == null || features.ShouldDropCard(item.Feature))
                    {
                        _parked.RemoveAt(i);
                        i--;
                        continue;
                    }
                    if (!features.CardReady(item.Feature)) continue;
                }
                else if (IsSuppressedByTutorial(item.Kind)) continue;
                if (!betweenStages || popupOpen) return false;
                request = item;
                _parked.RemoveAt(i);
                return true;
            }
            return false;
        }

        void Park(MetaPopupRequest request)
        {
            for (int i = 0; i < _parked.Count; i++)
            {
                if (_parked[i].SameAs(request)) return;
            }
            if (_parked.Count >= MaxParked)
            {
                int drop = 0;
                for (int i = 0; i < _parked.Count; i++)
                {
                    if (_parked[i].Kind != MetaPopupKind.FeatureUnlock) continue;
                    drop = i;
                    break;
                }
                _parked.RemoveAt(drop);
            }
            _parked.Add(request);
        }

        /// <summary>튜토리얼 동안 띄우지 않는 4차 종류인지 (다시 만난 날 · 이벤트 시작 · 챕터 상자 · 동화책 · 알림 묻기)</summary>
        static bool IsSuppressedByTutorial(MetaPopupKind kind)
        {
            if (!IsSpec4Kind(kind)) return false;
            var tutorial = Game.Tutorial;
            return tutorial != null && tutorial.IsActive;
        }

        /// <summary>4차 종류인지 (SPEC4 §2.9)</summary>
        public static bool IsSpec4Kind(MetaPopupKind kind) =>
            kind == MetaPopupKind.Comeback || kind == MetaPopupKind.EventStart || kind == MetaPopupKind.ChapterChest
            || kind == MetaPopupKind.StoryPage || kind == MetaPopupKind.NotificationAsk;

        // ───────────── 알림 묻기 감지 (NOTI-01) ─────────────

        /// <summary>1초마다 바구니가 '가득 됨'을 본다 (할당 없음). 튜토리얼 뒤 처음 가득 찼고 물어도 되면 NotificationAsk를 큐에 넣는다</summary>
        void PollNotificationAsk(float dt)
        {
            _notifyPoll -= dt;
            if (_notifyPoll > 0f) return;
            _notifyPoll = NotifyPollSeconds;
            var chest = Game.IdleChest;
            if (chest == null) return;
            bool full = chest.IsFull;
            bool becameFull = full && _wasFull == false;
            _wasFull = full;
            if (becameFull) MaybeAskNotifications();
        }

        /// <summary>알림을 물어도 되면 (NotificationAskPopup.ShouldAskNow) 큐에 넣는다 — 큐가 같은 요청을 하나로 합친다</summary>
        static void MaybeAskNotifications()
        {
            if (!MetaUi.IsActive || !NotificationAskPopup.ShouldAskNow()) return;
            if (Game.Popups.Contains(MetaPopupKind.NotificationAsk)) return;
            Game.Popups.Enqueue(MetaPopupRequest.NotificationAsk());
        }

        /// <summary>4차 팝업 찾기 (장면에 없으면 null — 다시 만들기 전 장면)</summary>
        T Find<T>() where T : UIPopup
        {
            var ui = _ui != null ? _ui : UIManager.Instance;
            return ui != null && ui.TryGet<T>(out var popup) ? popup : null;
        }

        IEnumerator Run(IEnumerator routine, bool startup)
        {
            _busy = true;
            try
            {
                yield return Guarded(routine, startup);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// 코루틴을 직접 한 칸씩 돌리며(안에서 yield한 IEnumerator도 펼쳐서) 예외를 잡는다. 예외는 기록하고 그 흐름만 끝낸다.
        /// Unity의 중첩 코루틴은 안쪽 예외가 바깥 코루틴을 finally 없이 그냥 버리므로, MoveNext를 여기서 감싸야 _busy가 풀린다.
        /// startup이면 접속 묶음을 닫는다 — 남은 요청(출석부 등)은 세션 흐름(스테이지 사이)에서 이어 띄운다.
        /// 흐름 하나에 스택 하나를 만들 뿐 프레임마다 할당하지 않는다.
        /// </summary>
        IEnumerator Guarded(IEnumerator routine, bool startup)
        {
            var stack = new Stack<IEnumerator>(4);
            stack.Push(routine);
            bool failed = false;
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current = null;
                bool moved;
                try
                {
                    moved = top.MoveNext();
                    if (moved) current = top.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    failed = true;
                    break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return current;
            }
            if (failed && startup) CloseStartupBatchAfterFailure();
        }

        static void CloseStartupBatchAfterFailure()
        {
            if (!MetaUi.IsActive) return;
            var queue = Game.Popups;
            if (queue != null && queue.InStartupBatch) queue.EndStartupBatch();
        }

        /// <summary>접속 묶음: 복귀 → 다시 만난 날 → 출석부 → (부팅이면) 공지 → 이벤트 → 묶음 끝</summary>
        IEnumerator StartupRoutine(bool boot)
        {
            var queue = Game.Popups;
            bool returnShown = false;

            // ① 공지 앞 차례 (복귀 / 다시 만난 날 / 출석부)
            for (int guard = 0; guard < 16 && MetaUi.IsActive; guard++)
            {
                if (!queue.TryPeek(out var next) || next.Kind >= MetaPopupKind.Notice) break;
                yield return WaitNoPopup();
                if (!queue.TryDequeue(Game.Battle.IsBetweenStages, _ui.HasOpenPopup, out var request)) break;
                returnShown |= request.Kind == MetaPopupKind.Return;
                // 한 장이 실패해도 다음 장(출석부·공지)으로 이어 간다
                yield return Guarded(Show(request), false);
            }

            // ② 공지 (부팅 때만, 튜토리얼이 끝난 뒤에만, 접속 모달 한 자리 — 띄울 공지가 있을 때만, M50)
            if (boot && MetaUi.IsActive && !Game.Tutorial.IsActive && Game.Notice.GetStartupNoticeToShow() != null)
            {
                yield return WaitNoPopup();
                if (queue.TryReserveStartupModal()) yield return Guarded(_ui.ShowStartupNotices(), false);
            }

            // ③ 큐에 든 공지 / 이벤트 시작 카드
            for (int guard = 0; guard < 16 && MetaUi.IsActive; guard++)
            {
                if (!queue.TryPeek(out var next) || !PopupQueue.IsStartupKind(next.Kind)) break;
                yield return WaitNoPopup();
                if (!queue.TryDequeue(Game.Battle.IsBetweenStages, _ui.HasOpenPopup, out var request)) break;
                yield return Guarded(Show(request), false);
            }

            if (!MetaUi.IsActive) yield break;
            queue.EndStartupBatch();
            if (boot && !returnShown) ToastShortReturn(Game.MetaSession.LastAwaySeconds);
        }

        IEnumerator WaitNoPopup()
        {
            while (_ui.HasOpenPopup && MetaUi.IsActive) yield return null;
        }

        /// <summary>요청 하나를 띄우고 닫힐 때까지 기다린다. 지금은 띄울 수 없는 요청(받을 것 없음 등)은 건너뛴다</summary>
        IEnumerator Show(MetaPopupRequest request)
        {
            if (!MetaUi.IsActive) yield break;
            // 튜토리얼 중 4차 종류는 대기 줄에서 기다린다 (접속 묶음에서 꺼낸 다시 만난 날 · 이벤트 시작도)
            if (IsSuppressedByTutorial(request.Kind))
            {
                Park(request);
                yield break;
            }
            switch (request.Kind)
            {
                case MetaPopupKind.Return:
                {
                    if (!Game.IdleChest.CanClaim) break;
                    // 가득 찬 바구니의 복귀 화면이면, 닫은 뒤 알림을 물어볼 수 있다 (SPEC4 §4.3 — '처음 가득 찬 복귀 화면 바로 뒤')
                    bool wasFull = Game.IdleChest.IsFull;
                    _returnPopup.Show(request.Value);
                    yield return WaitClosed(_returnPopup);
                    if (wasFull) MaybeAskNotifications();
                    break;
                }

                case MetaPopupKind.Attendance:
                    if (!Game.Attendance.CanCheckIn) break;
                    // 접속 출석부는 여는 연출 + 0.5초 뒤 저절로 도장 (M69)
                    _attendancePopup.OpenAuto();
                    yield return WaitClosed(_attendancePopup);
                    break;

                case MetaPopupKind.Notice:
                    var notice = FindNotice(request.Key);
                    if (notice == null || Game.Notice.IsHiddenToday) break;
                    var noticePopup = _ui.Get<NoticePopup>();
                    noticePopup.OpenStartup(notice);
                    yield return WaitClosed(noticePopup);
                    break;

                case MetaPopupKind.FeatureUnlock:
                    _unlockCard.Show(request.Feature);
                    yield return WaitClosed(_unlockCard);
                    // 거절로 닫혀 그림이 자리로 날아가는 중이면 닿을 때까지 (카드가 최대 0.8초로 끊는다) 다음 팝업을 띄우지 않는다
                    while (_unlockCard.IsFlying) yield return null;
                    // 다음 카드까지 간격을 다시 센다 (같은 평가에서 함께 열린 카드는 이어서 — SPEC5 §2.8)
                    Game.Features?.NotifyCardClosed(request.Feature);
                    break;

                case MetaPopupKind.FtueGraduation:
                    // 졸업 카드는 튜토리얼 UI 것을 쓴다 (FtueDirector가 먼저 꺼내면 그쪽이 띄운다). 없으면 개방 카드 틀로 대신한다
                    if (GraduationCardPopup.Present())
                    {
                        yield return WaitClosed(_ui.Get<GraduationCardPopup>());
                        break;
                    }
                    _unlockCard.ShowGraduation(GraduationRewards());
                    yield return WaitClosed(_unlockCard);
                    break;

                case MetaPopupKind.Comeback:
                {
                    var card = Find<ComebackCard>();
                    if (card == null || !card.Show()) break;
                    yield return WaitClosed(card);
                    break;
                }

                case MetaPopupKind.EventStart:
                {
                    var card = Find<EventStartCard>();
                    if (card == null || !card.Show(request.Key)) break;
                    yield return WaitClosed(card);
                    break;
                }

                case MetaPopupKind.ChapterChest:
                {
                    var chest = Find<ChapterChestPopup>();
                    if (chest == null || !chest.Show((int)request.Value)) break;
                    yield return WaitClosed(chest);
                    break;
                }

                case MetaPopupKind.StoryPage:
                {
                    var page = Find<StoryPagePopup>();
                    if (page == null || !page.ShowFirstRead((int)request.Value)) break;
                    yield return WaitClosed(page);
                    break;
                }

                case MetaPopupKind.NotificationAsk:
                {
                    // 큐에서 기다리는 동안 상태가 바뀌었을 수 있다 (설정에서 이미 켬 등) — 띄우기 직전에 다시 본다
                    if (!NotificationAskPopup.ShouldAskNow()) break;
                    var ask = Find<NotificationAskPopup>();
                    if (ask == null) break;
                    ask.Show();
                    yield return WaitClosed(ask);
                    break;
                }

                default:
                    break;
            }
        }

        /// <summary>창이 닫힐 때까지 + 0.18초 쉼 (M48 — 닫히는 창과 다음 창이 겹쳐 깜빡이지 않게)</summary>
        static IEnumerator WaitClosed(UIPopup popup)
        {
            yield return null;
            while (popup != null && popup.IsOpen) yield return null;
            yield return new WaitForSecondsRealtime(UIMotion.StartupGap);
        }

        static NoticeData FindNotice(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var notice in Game.Notice.Notices)
            {
                if (notice != null && notice.id == id) return notice;
            }
            return null;
        }

        /// <summary>졸업 단계(마지막 튜토리얼 단계)의 보상 — 졸업 카드에 "졸업 선물"로 보여 준다</summary>
        static IReadOnlyList<MetaReward> GraduationRewards()
        {
            var db = Game.MetaDb;
            if (db == null) return null;
            for (int i = db.ftueSteps.Count - 1; i >= 0; i--)
            {
                var step = db.ftueSteps[i];
                if (step != null && step.action == FtueAction.Graduation) return step.rewards;
            }
            return null;
        }

        // ───────────── 짧은 복귀 ─────────────

        void OnReturned(long awaySeconds)
        {
            // 복귀 화면이 뜨는 긴 복귀는 접속 묶음이 처리한다
            if (!MetaUi.IsActive || Game.Popups.Contains(MetaPopupKind.Return)) return;
            ToastShortReturn(awaySeconds);
        }

        static void ToastShortReturn(long awaySeconds)
        {
            if (!MetaUi.IsActive || awaySeconds < ShortReturnToastSeconds) return;
            var db = Game.MetaDb;
            long popupMin = db != null ? db.idleChest.returnPopupMinSeconds : 600;
            if (awaySeconds >= popupMin) return;
            if (!Game.IdleChest.IsUnlocked) return;
            float rate = db != null ? db.idleChest.offlineGoldRate : 0.5f;
            // 안 D: 기준 수입으로 B를 만든 뒤 단추 · 실타래로 나눈다 (IncomeSplit)
            double income = Game.Growth.IncomePerSecond * awaySeconds * rate;
            double gold = IncomeSplit.Gold(income);
            double lineageXp = IncomeSplit.LineageXp(income);
            string lineageText = lineageXp >= 1
                ? $" · {RewardService.CurrencyName(CurrencyType.LineageXp)} {RewardService.FormatAmount(CurrencyType.LineageXp, lineageXp)}"
                : string.Empty;
            MetaUi.Toast(gold >= 1
                ? $"자리를 비운 {MetaUi.Duration(awaySeconds)} 동안 바구니에 단추 {RewardService.FormatAmount(CurrencyType.Gold, gold)}{lineageText}만큼 쌓였어요"
                : $"자리를 비운 {MetaUi.Duration(awaySeconds)} 동안 바구니에 선물이 쌓였어요");
        }
    }
}
