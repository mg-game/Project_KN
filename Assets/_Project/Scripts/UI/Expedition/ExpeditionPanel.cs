using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 원정 탭 (UX-SPEC §2.7 M58): 머리 한 줄 [원정][잔향이 던전] 쪽 고르기 + 가운데 칩 + 모두 받기, 그 아래 두 쪽.
    ///   · 원정 쪽: 구역 카드 목록(보내기 / 다시 보내기 / 진행 / 받기) — 가운데 '진행 중 1/3', 오른쪽 모두 받기(받을 원정이 있을 때만 보인다, §3.9).
    ///   · 던전 쪽: 잔향이 던전 카드 + 층별 보상 — 가운데 '입장권 1/2', 모두 받기는 숨긴다. 쪽이 보이는 동안 던전 음악 (AudioService.PushMusic).
    ///   · 쪽은 앱을 켜는 동안 기억한다. 처음에는 원정 쪽 — 입장권이 남았는데 원정 자리가 모두 가는 중(받을 원정 없음)이면 던전 쪽. 0.12초 엇갈려 바뀐다 (움직임 줄이기면 바로).
    ///   · 퀘스트 · 일지 '가기'나 코치가 구역 목록(ExpeditionPanel/ZoneList…)을 가리키면 원정 쪽으로 돌린다 (UINav.FocusRequested — 패널이 꺼져 있어도 다음에 켜질 때).
    ///   · 쪽 고르기 배지: 원정 = 받을 원정 수, 던전 = 입장권이 남았으면 점.
    /// 남은 시간은 1초마다 갱신하고, 시간이 다 되면 카드가 "받기"로 바뀐다. 획득처 이동(SourceNavigator)은 FocusZone(원정 쪽) / FocusDungeon(던전 쪽).
    ///   · 모두 다시 보내기 (IDLE-04, ExpeditionBatch 2-1): 모두 받기와 같은 자리 — 받을 원정이 없고 다시 보낼 구역(CountResendable)이 있을 때만 보인다.
    ///     누르면 ExpeditionSystem.ResendAll → '원정 N곳을 다시 보냈어요'. 처음 쓰기 전까지 NEW 알약 (튜토리얼 힌트 깃발). 잠겼으면 숨김.
    ///   · 던전 잠김 (F-03, SPEC4 §5.1 — Dungeon 1-10): Seg_Dungeon α0.45 + 자물쇠 28 + '1-10' 알약, 누르면 잠김 토스트 + 약한 진동이고 쪽은 바뀌지 않는다.
    ///     FocusDungeon · 처음 쪽 고르기도 원정 쪽에 머문다. 덮개 그림이 없는 예전 씬도 쪽은 바뀌지 않는다 (토스트만).
    /// </summary>
    public class ExpeditionPanel : MonoBehaviour
    {
        public const int PageExpedition = 0;
        public const int PageDungeon = 1;

        static readonly Color[] FallbackColors =
        {
            UITheme.Lavender, UITheme.SoftBlue, UITheme.DustyPink, UITheme.Cream,
        };

        /// <summary>던전 음악을 올리고 내리는 주인 번호 (다른 곳과 겹치지 않게)</summary>
        const int DungeonMusicOwner = 0x45585044; // "EXPD"

        /// <summary>이번 실행에서 고른 쪽 (−1 = 아직 정하지 않음 — 처음 켤 때 정한다)</summary>
        static int s_page = -1;
        /// <summary>구역 목록 코치 경로 (CoachTargets — 이것으로 시작하는 대상을 가리키면 원정 쪽으로)</summary>
        static readonly string ZoneListPath = CoachTargets.ExpeditionPanel + "/ZoneList";
        /// <summary>켜져 있는 패널 (가리키기 요청이 오면 바로 쪽을 바꾼다)</summary>
        static ExpeditionPanel s_live;
        /// <summary>모두 다시 보내기를 처음 쓰면 세우는 힌트 깃발 (NEW 알약을 끈다 — 저장된다)</summary>
        internal const string ResendAllHintFlag = "hint.expedition.resend_all";
        /// <summary>잠긴 던전 쪽 고르기 흐림 (F-03)</summary>
        internal const float LockedSegmentAlpha = 0.45f;

        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        [SerializeField] ExpeditionZoneCardView _zonePrefab;
        [SerializeField] DungeonCardView _dungeonCard;
        [SerializeField] TMP_Text _activeText;
        [SerializeField] Button _claimAllButton;
        [SerializeField] Badge _claimAllBadge;
        [Tooltip("구역 그림 (스프라이트 이름 = zone_{구역 ID})")]
        [SerializeField] Sprite[] _zoneArts;

        [Header("쪽 (M58)")]
        [Tooltip("원정 쪽 = ZoneList (코치 경로 ExpeditionPanel/ZoneList — 패널 직속 자식)")]
        [SerializeField] CanvasGroup _zonePage;
        [Tooltip("던전 쪽 = DungeonPage (던전 카드 + 층별 보상)")]
        [SerializeField] CanvasGroup _dungeonPage;
        [SerializeField] Button _expeditionSeg;
        [SerializeField] Button _dungeonSeg;
        [SerializeField] TMP_Text _expeditionSegLabel;
        [SerializeField] TMP_Text _dungeonSegLabel;
        [SerializeField] Badge _expeditionSegBadge;
        [SerializeField] Badge _dungeonSegBadge;
        [Tooltip("원정 쪽 가운데 '진행 중 1/3'")]
        [SerializeField] GameObject _activeChip;
        [Tooltip("던전 쪽 가운데 '입장권 1/2'")]
        [SerializeField] GameObject _ticketPill;
        [SerializeField] TMP_Text _ticketPillText;

        [Header("SPEC4 (다시 빌드한 씬부터)")]
        [Tooltip("모두 다시 보내기 (ClaimAllButton과 같은 자리, IDLE-04)")]
        [SerializeField] Button _resendAllButton;
        [Tooltip("모두 다시 보내기 NEW 알약 (처음 쓰기 전까지)")]
        [SerializeField] Badge _resendAllBadge;
        [Tooltip("던전 쪽 고르기 흐림 (잠김이면 α0.45, F-03)")]
        [SerializeField] CanvasGroup _dungeonSegGroup;
        [Tooltip("던전 쪽 고르기 자물쇠 28 + '1-10' 알약")]
        [SerializeField] GameObject _dungeonLock;
        [SerializeField] TMP_Text _dungeonLockText;

        UIListPool<ExpeditionZoneCardView> _cards;
        bool _started;
        float _tickTimer;
        Coroutine _focus;
        Coroutine _pageFade;
        Coroutine _claimPop;
        int _shownPage = -1;
        bool _musicPushed;
        int _shownActive = -1;
        int _shownMax = -1;
        int _shownCompleted = -1;
        int _shownTickets = -1;
        int _shownDaily = -1;
        /// <summary>던전 잠김 모양을 마지막으로 그린 값 (−1 = 아직)</summary>
        int _shownDungeonLocked = -1;
        /// <summary>기능 열림을 듣는 기능 시스템 (던전 · 원정 한 번에 보내기가 열리면 다시 그린다). 메타가 꺼져 있으면 null</summary>
        FeatureGateSystem _features;

        /// <summary>던전이 아직 잠겼는지 (F-03 — 메타가 꺼져 있으면 잠그지 않는다, 예전과 같다)</summary>
        internal static bool DungeonLocked => Game.IsMetaActive && Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Dungeon);

        /// <summary>원정 한 번에 보내기(ExpeditionBatch)가 열렸는지 (메타가 꺼져 있으면 숨긴다)</summary>
        static bool BatchUnlocked => Game.IsMetaActive && Game.Features != null && Game.Features.IsUnlocked(FeatureId.ExpeditionBatch);

        /// <summary>모두 다시 보내기가 보이는지 (테스트용 순수 함수): 원정 쪽 · 받을 원정 없음 · 열림 · 다시 보낼 구역 1곳 이상</summary>
        internal static bool ShowResendAll(bool expeditionPage, int completed, bool unlocked, int resendable) =>
            expeditionPage && completed <= 0 && unlocked && resendable >= 1;

        /// <summary>지금 보이는 쪽 (PageExpedition / PageDungeon)</summary>
        public int Page => s_page < 0 ? PageExpedition : s_page;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            s_page = -1;
            s_live = null;
            // 패널이 꺼져 있으면(Awake 전) 스스로 들을 수 없어 정적으로 듣는다 — 도메인을 다시 읽지 않는 재생에서도 한 번만
            UINav.FocusRequested -= OnFocusRequested;
            UINav.FocusRequested += OnFocusRequested;
        }

        /// <summary>
        /// 퀘스트 · 일지 '가기'(MetaNavigator.GoAndFocus — 원정 탭만 열고 쪽은 고르지 않는다)가 구역 목록을 가리키면 원정 쪽으로 돌린다.
        /// 기억한 쪽이 던전이면 목록이 꺼져 있어 손가락이 숨은 대상을 가리키게 된다. 켜져 있으면 바로 바꾸고, 아니면 다음에 켜질 때.
        /// </summary>
        static void OnFocusRequested(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(ZoneListPath, System.StringComparison.Ordinal)) return;
            var live = s_live;
            if (live != null && live.isActiveAndEnabled) live.SelectPage(PageExpedition);
            else s_page = PageExpedition;
        }

        void Start()
        {
            _cards = new UIListPool<ExpeditionZoneCardView>(_zonePrefab, _content);
            _claimAllButton.onClick.AddListener(ClaimAll);
            if (_expeditionSeg != null) _expeditionSeg.onClick.AddListener(() => SelectPage(PageExpedition));
            if (_dungeonSeg != null) _dungeonSeg.onClick.AddListener(() => SelectPage(PageDungeon));
            if (_resendAllButton != null) _resendAllButton.onClick.AddListener(ResendAll);
            BuildCards();

            Game.Expedition.Changed += Refresh;
            Game.Dungeon.Changed += Refresh;
            Game.Battle.StageChanged += Refresh;
            if (Game.IsMetaActive && Game.Features != null)
            {
                _features = Game.Features;
                _features.Unlocked += OnFeatureUnlocked;
            }
            _started = true;
            // 다 돌아왔지만 아직 안 받은 원정은 '바쁜 자리'가 아니다 (받기 · 모두 받기는 원정 쪽에 있다). 던전이 잠겼으면 늘 원정 쪽
            var expedition = Game.Expedition;
            if (s_page < 0) s_page = DungeonLocked ? PageExpedition
                : LaunchPage(Game.Dungeon.TicketsLeft, expedition.Active.Count - expedition.CompletedCount, expedition.MaxConcurrent);
            if (TutorialWantsZones()) s_page = PageExpedition;
            ApplyPage(true);
            Refresh();
        }

        void OnEnable()
        {
            s_live = this;
            if (!_started) return;
            // 튜토리얼이 구역 카드(보내기 · 받기)를 가리키면 원정 쪽으로 (손가락이 꺼진 쪽을 가리키지 않게)
            if (TutorialWantsZones()) s_page = PageExpedition;
            ApplyPage(true);
            Refresh();
        }

        /// <summary>지금 튜토리얼 단계가 원정 보내기 · 받기를 가르치는지 (코치 경로 ExpeditionPanel/ZoneList — 던전 쪽이면 목록이 꺼져 있다)</summary>
        static bool TutorialWantsZones()
        {
            if (!Game.IsMetaActive || Game.Tutorial == null || !Game.Tutorial.IsActive) return false;
            var step = Game.Tutorial.Current;
            if (step == null) return false;
            if (!string.IsNullOrEmpty(step.coachTarget) && step.coachTarget.Contains("/ZoneList")) return true;
            var condition = step.complete;
            if (condition == null || (condition.type != MetaConditionType.Counter && condition.type != MetaConditionType.DailyCounter)) return false;
            return condition.counter == CounterKey.ExpeditionStart || condition.counter == CounterKey.ExpeditionClaim;
        }

        void OnDisable()
        {
            if (s_live == this) s_live = null;
            // 시트가 내려가면 쪽 음악도 내린다 (다시 켜지면 ApplyPage가 올린다)
            SetDungeonMusic(false);
            if (_pageFade != null)
            {
                _pageFade = null;
                SnapPages();
            }
            // 모두 받기 튀어나옴이 도중에 멈추면 작은 채로 남는다 — 원래 크기로
            if (_claimPop != null)
            {
                StopCoroutine(_claimPop);
                _claimPop = null;
                if (_claimAllButton != null) _claimAllButton.transform.localScale = Vector3.one;
            }
        }

        void OnDestroy()
        {
            if (s_live == this) s_live = null;
            SetDungeonMusic(false);
            if (!_started || !Game.IsReady) return;
            Game.Expedition.Changed -= Refresh;
            Game.Dungeon.Changed -= Refresh;
            Game.Battle.StageChanged -= Refresh;
            if (_features != null) _features.Unlocked -= OnFeatureUnlocked;
            _features = null;
        }

        /// <summary>던전 · 원정 한 번에 보내기가 열리면 쪽 고르기 · 모두 다시 보내기를 다시 그린다</summary>
        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature != FeatureId.Dungeon && feature != FeatureId.ExpeditionBatch) return;
            _shownDungeonLocked = -1;
            _shownTickets = -1;
            if (isActiveAndEnabled && _started) RefreshHeader();
        }

        void Update()
        {
            _tickTimer -= Time.unscaledDeltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = 1f;

            bool completed = false;
            for (int i = 0; i < _cards.Count; i++) completed |= _cards[i].Tick();
            if (completed) RefreshHeader();
            // 친구가 돌아오거나 자리가 나면 다시 보낼 수 있는 구역이 생긴다 (1초마다 — 보이는 상태가 바뀔 때만 켜고 끈다)
            else RefreshResendAll();
            // 날짜가 바뀌면 입장권이 충전되므로 던전 카드도 가볍게 갱신 (충전까지 남은 시간도 여기서 1초마다)
            _dungeonCard.Refresh();
            RefreshTickets();
        }

        /// <summary>
        /// 앱을 켠 뒤 처음 보여 줄 쪽: 원정 — 단, 입장권이 남았는데 원정 자리가 모두 '아직 가는 중'이면 던전 (테스트용 순수 함수).
        /// runningExpeditions = 진행 중 − 다 돌아온(받을) 원정. 받을 원정이 하나라도 있으면 자리가 비므로 늘 원정 쪽이다.
        /// </summary>
        internal static int LaunchPage(int ticketsLeft, int runningExpeditions, int maxConcurrent) =>
            ticketsLeft > 0 && runningExpeditions >= maxConcurrent ? PageDungeon : PageExpedition;

        void BuildCards()
        {
            var zones = Game.Expedition.Db.zones;
            _cards.SetCount(zones.Count);
            for (int i = 0; i < zones.Count; i++)
            {
                _cards[i].Init(this);
                _cards[i].Set(zones[i], FindArt(zones[i].id), FallbackColors[i % FallbackColors.Length]);
            }
        }

        void Refresh()
        {
            if (!isActiveAndEnabled || !_started) return;
            for (int i = 0; i < _cards.Count; i++) _cards[i].Refresh();
            _dungeonCard.Refresh();
            RefreshHeader();
        }

        void RefreshHeader()
        {
            var expedition = Game.Expedition;
            int activeCount = expedition.Active.Count;
            int max = expedition.MaxConcurrent;
            if (activeCount != _shownActive || max != _shownMax)
            {
                _shownActive = activeCount;
                _shownMax = max;
                _activeText.text = $"진행 중 <color={UITheme.ColorTag(UITheme.Gold)}>{activeCount}</color>/{max}";
            }
            int completed = expedition.CompletedCount;
            if (completed != _shownCompleted)
            {
                bool appear = _shownCompleted <= 0 && completed > 0;
                _shownCompleted = completed;
                _claimAllBadge.SetCount(completed);
                if (_expeditionSegBadge != null) _expeditionSegBadge.SetCount(completed);
                RefreshClaimAll();
                // 0에서 생기면 0.85 → 1 튀어나옴 (UIMotion.ClaimAllPop)
                if (appear && !UIMotion.Reduced && _claimAllButton.gameObject.activeInHierarchy) MenuFx.Restart(this, ref _claimPop, PopClaimAll());
            }
            RefreshClaimAll();
            RefreshTickets();
        }

        /// <summary>모두 받기는 원정 쪽에서 받을 원정이 있을 때만 보인다 (§3.9 — 누를 수 없게 두지 않고 숨긴다). 같은 자리의 모두 다시 보내기도 맞춘다</summary>
        void RefreshClaimAll()
        {
            bool show = Page == PageExpedition && Game.Expedition.CompletedCount > 0;
            if (_claimAllButton.gameObject.activeSelf != show) _claimAllButton.gameObject.SetActive(show);
            RefreshResendAll();
        }

        /// <summary>모두 다시 보내기 (모두 받기와 같은 자리 — 둘이 함께 보이는 일은 없다) + 처음 쓰기 전 NEW 알약</summary>
        void RefreshResendAll()
        {
            if (_resendAllButton == null) return;
            var expedition = Game.Expedition;
            bool unlocked = BatchUnlocked;
            bool show = ShowResendAll(Page == PageExpedition, expedition.CompletedCount, unlocked, unlocked ? expedition.CountResendable() : 0);
            if (_resendAllButton.gameObject.activeSelf != show) _resendAllButton.gameObject.SetActive(show);
            if (!show || _resendAllBadge == null) return;
            bool fresh = Game.Tutorial != null && !Game.Tutorial.HasHintFlag(ResendAllHintFlag);
            if (_resendAllBadge.IsNew != fresh)
            {
                if (fresh) _resendAllBadge.SetNew(true);
                else _resendAllBadge.Hide();
            }
        }

        /// <summary>모두 다시 보내기: 다시 보낼 수 있는 구역을 모두 지난 친구 · 단계로 (자리만큼) — '원정 N곳을 다시 보냈어요'</summary>
        void ResendAll()
        {
            if (!BatchUnlocked) return;
            if (Game.Tutorial != null && !Game.Tutorial.HasHintFlag(ResendAllHintFlag)) Game.Tutorial.SetHintFlag(ResendAllHintFlag);
            int sent = Game.Expedition.ResendAll();
            if (sent <= 0)
            {
                UIManager.Instance.ShowToast("지금 다시 보낼 수 있는 원정이 없어요");
                RefreshResendAll();
                return;
            }
            UISfx.Play(UISfxKey.ExpeditionDepart);
            UIManager.Instance.ShowToast($"원정 {sent}곳을 다시 보냈어요", ToastKind.Success);
            RefreshResendAll();
        }

        IEnumerator PopClaimAll()
        {
            var target = _claimAllButton.transform;
            for (float t = 0f; t < UIMotion.ClaimAllPop; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * Mathf.LerpUnclamped(UIMotion.ClaimAllPopFrom, 1f, UIMotion.OutBack(t / UIMotion.ClaimAllPop));
                yield return null;
            }
            target.localScale = Vector3.one;
            _claimPop = null;
        }

        void RefreshTickets()
        {
            var dungeon = Game.Dungeon;
            int tickets = dungeon.TicketsLeft;
            // 뒤 수 = 하루 입장권, 입장권 모아 두기(NEW-04)가 켜졌으면 상한 (던전 카드와 같은 글)
            int daily = DungeonCardView.TicketMax(dungeon);
            if (tickets == _shownTickets && daily == _shownDaily) return;
            _shownTickets = tickets;
            _shownDaily = daily;
            if (_ticketPillText != null) _ticketPillText.text = DungeonCardView.TicketLabel(tickets, daily);
            bool locked = DungeonLocked;
            if (_dungeonSegBadge != null) _dungeonSegBadge.SetDot(tickets > 0 && !locked);
            RefreshDungeonLock(locked);
        }

        /// <summary>던전 쪽 고르기 잠김 모양 (α0.45 + 자물쇠 · '1-10' 알약) — 바뀔 때만</summary>
        void RefreshDungeonLock(bool locked)
        {
            int state = locked ? 1 : 0;
            if (state == _shownDungeonLocked) return;
            _shownDungeonLocked = state;
            if (_dungeonSegGroup != null) _dungeonSegGroup.alpha = locked ? LockedSegmentAlpha : 1f;
            if (_dungeonLock != null) _dungeonLock.SetActive(locked);
            if (locked && _dungeonLockText != null)
            {
                string hint = Game.Features != null ? Game.Features.GetLockedHint(FeatureId.Dungeon) : null;
                _dungeonLockText.text = string.IsNullOrEmpty(hint) ? BattleSystem.FormatStage(10) : hint;
            }
        }

        /// <summary>잠긴 던전 쪽을 눌렀다: 잠김 토스트 + 약한 진동 (쪽은 그대로)</summary>
        static void DenyDungeon()
        {
            string text = Game.Features != null ? Game.Features.GetRequirementText(FeatureId.Dungeon) : null;
            if (string.IsNullOrEmpty(text)) text = MetaText.UnlockSentence(11);
            UIManager.Instance.ShowToast(text, ToastKind.Error);
            Haptics.Play(HapticKind.Light);
        }

        // ───────────── 쪽 (M58) ─────────────

        /// <summary>쪽을 고른다 (쪽 고르기 버튼 · 획득처 이동). 이번 실행 동안 기억한다</summary>
        public void SelectPage(int page)
        {
            page = page == PageDungeon ? PageDungeon : PageExpedition;
            // 잠긴 던전 (F-03): 쪽을 바꾸지 않고 이유만 알린다
            if (page == PageDungeon && DungeonLocked)
            {
                DenyDungeon();
                return;
            }
            bool same = s_page == page && _shownPage == page;
            s_page = page;
            if (same) return;
            ApplyPage(!isActiveAndEnabled || !_started || UIMotion.Reduced);
        }

        /// <summary>고른 쪽을 화면에 맞춘다: 쪽 고르기 색 · 가운데 칩 · 모두 받기 · 던전 음악 · 쪽 (instant면 엇갈림 없이)</summary>
        void ApplyPage(bool instant)
        {
            // 기억한 쪽이 던전인데 (아직) 잠겼으면 원정 쪽으로
            if (s_page == PageDungeon && DungeonLocked) s_page = PageExpedition;
            int page = Page;
            bool dungeon = page == PageDungeon;
            StyleSegment(_expeditionSeg, _expeditionSegLabel, !dungeon);
            StyleSegment(_dungeonSeg, _dungeonSegLabel, dungeon);
            if (_activeChip != null) _activeChip.SetActive(!dungeon);
            if (_ticketPill != null) _ticketPill.SetActive(dungeon);
            if (_started) RefreshClaimAll();
            SetDungeonMusic(dungeon && isActiveAndEnabled);

            bool changed = _shownPage != page;
            _shownPage = page;
            if (instant || !changed || _zonePage == null || _dungeonPage == null)
            {
                if (_pageFade != null) StopCoroutine(_pageFade);
                _pageFade = null;
                SnapPages();
                return;
            }
            MenuFx.Restart(this, ref _pageFade, CrossFade(dungeon ? _zonePage : _dungeonPage, dungeon ? _dungeonPage : _zonePage));
        }

        void SnapPages()
        {
            bool dungeon = Page == PageDungeon;
            SetPage(_zonePage, !dungeon);
            SetPage(_dungeonPage, dungeon);
        }

        static void SetPage(CanvasGroup page, bool on)
        {
            if (page == null) return;
            page.alpha = 1f;
            page.interactable = true;
            page.blocksRaycasts = true;
            if (page.gameObject.activeSelf != on) page.gameObject.SetActive(on);
        }

        /// <summary>
        /// 0.12초 엇갈려 바꾸기: 나가는 쪽은 사라지고 들어오는 쪽은 같은 자리에서 나타난다 (그동안 나가는 쪽은 누를 수 없다).
        /// 들어오는 쪽은 처음부터 누를 수 있게 되돌린다 — 엇갈리는 중에 쪽을 다시 바꾸면 방금 나가던 쪽이 들어오는 쪽이 된다.
        /// </summary>
        IEnumerator CrossFade(CanvasGroup from, CanvasGroup to)
        {
            float duration = UIMotion.PanelFade;
            from.interactable = false;
            from.blocksRaycasts = false;
            to.interactable = true;
            to.blocksRaycasts = true;
            to.alpha = 0f;
            to.gameObject.SetActive(true);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = UIMotion.OutCubic(t / duration);
                from.alpha = 1f - k;
                to.alpha = k;
                yield return null;
            }
            to.alpha = 1f;
            from.alpha = 1f;
            from.interactable = true;
            from.blocksRaycasts = true;
            from.gameObject.SetActive(false);
            _pageFade = null;
        }

        /// <summary>
        /// 쪽 고르기 한 칸 모양 = 상점 · 설정 · 도감 · 일지와 같은 쪽 탭 (M53, TabStyle Page): 선택 = 양피지 + Ink 굵게, 아님 = TabNormal + SubText.
        /// 금색은 화면마다 하나뿐인 주 버튼(도전 · 보내기) 몫이라 쪽 고르기에는 쓰지 않는다 (M26). 쪽을 바꿀 때만 부른다 (색 · 굵기는 바뀔 때만 다시 그려진다).
        /// </summary>
        static void StyleSegment(Button segment, TMP_Text label, bool selected) =>
            TabStyle.Apply(segment != null ? segment.targetGraphic : null, label, selected, TabVariant.Page);

        void SetDungeonMusic(bool on)
        {
            if (on == _musicPushed) return;
            _musicPushed = on;
            if (on) AudioService.PushMusic(MusicState.Dungeon, DungeonMusicOwner);
            else AudioService.PopMusic(DungeonMusicOwner);
        }

        // ───────────── 카드에서 호출 ─────────────

        public void OpenSetup(ExpeditionZoneCardView card)
        {
            var expedition = Game.Expedition;
            if (expedition.Active.Count >= expedition.MaxConcurrent)
            {
                UIManager.Instance.ShowToast($"원정은 동시에 {expedition.MaxConcurrent}곳까지 보낼 수 있어요.");
                return;
            }
            UIManager.Instance.Get<ExpeditionSetupPopup>().Show(card.Zone, card.Art, FallbackColorOf(card.Zone));
        }

        /// <summary>'다시 보내기' (M57): 지난번 친구 그대로 바로 출발. 그사이 못 가게 됐으면 준비 팝업으로</summary>
        public void Resend(ExpeditionZoneCardView card)
        {
            var zone = card.Zone;
            if (zone == null) return;
            if (!Game.Expedition.TryResend(zone.id))
            {
                OpenSetup(card);
                return;
            }
            UISfx.Play(UISfxKey.ExpeditionDepart);
            UIManager.Instance.ShowToast($"{MenuText.Toward(zone.place)} 친구들이 다시 출발했어요! ({MenuText.Minutes(zone.durationMinutes)})", ToastKind.Success);
        }

        public void Claim(ExpeditionZoneDef zone)
        {
            if (BlockIfClockTampered()) return;
            var result = Game.Expedition.Claim(zone.id);
            if (result == null) return;
            UISfx.Play(UISfxKey.ExpeditionReturn);
            var subtitle = string.IsNullOrEmpty(zone.place)
                ? $"{zone.name}에서 친구들이 돌아왔어요!"
                : $"{zone.place}에서 친구들이 돌아왔어요!";
            UIManager.Instance.Get<RewardPopup>().Show("원정 완료", subtitle, result.Rewards);
        }

        /// <summary>진행 중 원정 취소 (되돌릴 수 없어 위험 확인 — 확인 버튼 '원정 취소', 0.6초 잠금, M55)</summary>
        public void ConfirmCancel(ExpeditionZoneDef zone)
        {
            UIManager.Instance.ShowDangerConfirm("원정 취소",
                "지금 취소하면 보상 없이 친구들이 돌아와요.\n원정을 취소할까요?",
                "원정 취소",
                () =>
                {
                    if (Game.Expedition.TryCancel(zone.id)) UIManager.Instance.ShowToast("친구들이 수선점으로 돌아왔어요.");
                },
                "계속 보내기");
        }

        /// <summary>시계 되돌림 중이면 안내 토스트를 띄우고 true (시스템도 받기를 막는다 — 메타 활성 여부와 무관)</summary>
        static bool BlockIfClockTampered()
        {
            if (!Game.Expedition.IsClockTampered) return false;
            UIManager.Instance.ShowToast(DailyClock.TamperedMessage);
            return true;
        }

        void ClaimAll()
        {
            if (BlockIfClockTampered()) return;
            var results = Game.Expedition.ClaimAll();
            if (results == null || results.Count == 0)
            {
                UIManager.Instance.ShowToast("받을 수 있는 원정 보상이 없어요.");
                return;
            }
            UISfx.Play(UISfxKey.ExpeditionReturn);
            var rewards = new List<RewardData>();
            foreach (var result in results) rewards.AddRange(result.Rewards);
            // 보상 팝업의 확인을 누르면 보상이 상단 바로 날아간다 (M20)
            UIManager.Instance.Get<RewardPopup>().Show("원정 완료", $"원정 {results.Count}곳에서 친구들이 돌아왔어요!", rewards);
        }

        // ───────────── 획득처 이동 (SourceNavigator) ─────────────

        /// <summary>원정 쪽으로 바꾸고 구역 카드로 스크롤해 반짝여 알려준다</summary>
        public void FocusZone(string zoneId)
        {
            SelectPage(PageExpedition);
            StartFocus(() => FindCard(zoneId));
        }

        /// <summary>던전 쪽으로 바꾸고 던전 카드를 반짝여 알려준다</summary>
        public void FocusDungeon()
        {
            // 잠긴 던전으로는 데려가지 않는다 (SelectPage가 토스트로 알린다, F-03)
            if (DungeonLocked)
            {
                SelectPage(PageDungeon);
                return;
            }
            SelectPage(PageDungeon);
            if (!isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _focus, FocusDungeonRoutine());
        }

        IEnumerator FocusDungeonRoutine()
        {
            while (!_started) yield return null;
            // 쪽이 엇갈려 나타난 뒤에 반짝인다
            while (_pageFade != null) yield return null;
            _dungeonCard.Highlight();
            _focus = null;
        }

        void StartFocus(System.Func<Component> target)
        {
            if (!isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _focus, FocusRoutine(target));
        }

        IEnumerator FocusRoutine(System.Func<Component> getTarget)
        {
            // 탭이 막 켜졌으면 Start(카드 생성)와 레이아웃이 끝날 때까지 기다린다
            while (!_started) yield return null;
            while (_pageFade != null) yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var target = getTarget();
            if (target == null)
            {
                _focus = null;
                yield break;
            }

            float from = _scroll.verticalNormalizedPosition;
            float to = NormalizedPositionOf((RectTransform)target.transform);
            const float duration = 0.35f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                _scroll.verticalNormalizedPosition = Mathf.Lerp(from, to, MenuFx.EaseOutCubic(t / duration));
                yield return null;
            }
            _scroll.verticalNormalizedPosition = to;
            _scroll.velocity = Vector2.zero;

            if (target is ExpeditionZoneCardView card) card.Highlight();
            _focus = null;
        }

        /// <summary>대상의 윗부분이 뷰포트 위쪽에 오도록 하는 스크롤 위치</summary>
        float NormalizedPositionOf(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float top = -_content.InverseTransformPoint(corners[1]).y - 16f;
            float scrollable = _content.rect.height - _scroll.viewport.rect.height;
            if (scrollable <= 0f) return 1f;
            return 1f - Mathf.Clamp01(top / scrollable);
        }

        ExpeditionZoneCardView FindCard(string zoneId)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Zone != null && _cards[i].Zone.id == zoneId) return _cards[i];
            }
            return null;
        }

        Sprite FindArt(string zoneId)
        {
            if (_zoneArts == null) return null;
            string name = "zone_" + zoneId;
            foreach (var art in _zoneArts)
            {
                if (art != null && art.name == name) return art;
            }
            return null;
        }

        Color FallbackColorOf(ExpeditionZoneDef zone)
        {
            int index = Game.Expedition.Db.zones.IndexOf(zone);
            return FallbackColors[Mathf.Max(0, index) % FallbackColors.Length];
        }
    }
}
