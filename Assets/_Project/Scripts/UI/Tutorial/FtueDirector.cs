using System.Globalization;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 첫 10분 튜토리얼 연출 감독 (FTUE-01). TutorialSystem의 단계 이벤트를 받아 화면을 움직인다.
    ///   · 코치마크: 단계 문구 + 대상(콘텐츠 coachTarget → 열린 탭 안쪽 버튼까지 CoachTargetResolver가 따라감).
    ///     다른 팝업이 위에 있으면 숨기고(대상이 그 팝업 안이면 보여 줌), 인트로 중에도 숨긴다. hintOnly 단계는 토스트 한 줄.
    ///   · 단계 action:
    ///       StoryIntro → 동화 인트로 3장 → Tutorial.Signal(IntroSeen)
    ///       BattleProtection → 전투 쪽 보호 (1-1~1-3 적 체력 60%, 수선점 최소 1 — BattleSetup.Protection, FtueBattleGuard가 resolver를 잇는다)
    ///       ForceLampFull → 8마리째를 잡았거나 4초가 지나면 Game.Battle.ForceLampFull로 게이지를 채운다 (그때까지는 테두리 + "게이지가 차면 눌러요", 찰 때 손가락)
    ///   · 튜토리얼 연출 표시 (SPEC4 §13.3, AppSignals — 매 프레임 바뀔 때만 쓴다, 메타가 꺼지거나 감독이 꺼지면 내린다):
    ///       FtueFirstImpression = 1단계(BattleProtection) 동안 → 지킴이 명중 그림 ×1.3 + 반짝임 2개 (BattleView)
    ///       FtueFirstLampBeat = 4단계(ForceLampFull) 동안 → 첫 불 밝히기 0.2배 0.6초 + 큰 배너 (단계는 그 불 밝히기로 끝나고, 표시는 다음 프레임에 내린다)
    ///       EnableAutoBattle → Battle.AutoMode = true
    ///       PrefillIdleChest → IdleChest.Prefill(시간) (한 번만)
    ///       FirstMeetingSummon → 소환 탭 위 첫 만남 카드 → PullGuaranteed → 결과 팝업 + "첫 만남 확정 소환" 리본.
    ///         카드는 단계와 상관없이 MetaFlags.FirstMeetingSummoned가 없으면(소환 개방 뒤) 소환 시트를 덮는다 —
    ///         소환 개방(1-3)이 첫 만남 단계보다 먼저 올 수 있어(바구니를 안 받은 채) 유료 소환으로 단계 조건(GachaPull 1)을
    ///         먼저 채워 확정 친구를 놓치지 않게. 첫 만남이 아닌 단계 중에 카드가 떠 있으면 손가락을 카드로 잠깐 돌리고,
    ///         확정 소환 없이 첫 만남 단계가 이미 지나갔으면(옛 세이브) 받을 때까지 손가락을 소환 탭으로 돌린다.
    ///         튜토리얼을 건너뛰었거나 끝난 뒤에도 카드만은 계속 띄운다 (M74 3b — FirstMeetingKeptAfterSkip): 단계 안내 · 손가락 없이 카드가 소환 시트를 덮는다.
    ///       ShortFirstExpedition → 처음 보낸 원정의 끝 시각을 actionArg분 뒤로 당긴다 (한 번만, 공개 저장 값 사용)
    ///       Graduation → 단계가 끝나면 TutorialSystem이 PopupQueue에 졸업 카드를 넣는다 → 스테이지 사이에 GraduationCardPopup.
    ///         졸업 카드 · 출석부가 모두 닫히고 조용해지면, 우편에 받을 선물(개업 선물)이 남아 있을 때 우편 버튼에 손가락 3초 + 토스트 (M06, 한 번).
    ///   · 단계 보상(M06 6단계 단추 250 등)은 대상 버튼에서 상단 칩으로 날아간다. 한 단계를 마치면 마침 소리(TutorialStep — 시작과 함께 끝나는 단계 · 인트로 · 졸업은 빼고, PlaysStepDoneSound).
    ///   · UINav.FocusRequested → 어둠 없이 손가락만 3초. hintOnly 단계 중에도 보이고, 그 대상을 덮는 팝업·인트로가 있을 때만 숨긴다.
    ///     대상이 첫 만남 카드 덮개 밑(소환 시트 안)이면 손가락을 카드의 [문 열어 주기]로 옮긴다 (건너뛴 뒤 가이드 퀘스트 · 소환 미션 [가기]).
    ///   · 다른 화면 팝업이 열린 순간(UIPopup.IsOpen이 켜지는 순간 — 닫히는 연출 중인 팝업은 열린 것으로 치지 않는다) 메타 플래그를 세운다:
    ///     시너지 상세 → SynergyDetailViewed(Signal), 친구 상세(진화 기억도) → EvolutionTreeViewed, 시너지 도감/도감 → CodexOpened. 개방 카드에는 축하 연출을 붙인다.
    ///   · 0.25초마다 Tutorial.Refresh(스테이지 사이 여부)로 BetweenStages trigger를 놓치지 않는다. 메타가 꺼지면 모두 숨긴다.
    ///   · 화면 틀 (UX-SPEC-IA §5.7): 탭 화면은 전투 아래쪽을 잠깐 덮는 시트다.
    ///       (a) 단계가 시작된 뒤 처음(팝업 없이) 찾은 대상이 시트 밑이면(CoachTargetResolver.RedirectedToMain — 덱 · 램프) 한 번만 UINav.GoToMain().
    ///           그 뒤 플레이어가 시트를 다시 열면 닫지 않고 손가락만 사냥 버튼으로 간다.
    ///           손가락이 사냥 버튼에 가 있는 동안 말풍선은 단계 글('덱의 첫 칸을 톡 눌러 보세요') 대신 돌아가자는 말(_redirectToMainText) —
    ///           손가락과 말이 어긋나지 않게 (WP-8 r1). 다음 찾기가 시트 밑이 아니면 단계 글로 돌아온다 (찾을 때만 바뀐다).
    ///       (b) 첫 만남 카드는 소환 시트가 열렸을 때만 덮는다. 대상이 없는 첫 만남 단계는 MAIN에 덮을 자리가 없으니 소환 시트를 연다 (단계마다 한 번).
    ///       (c) UIFrame.SheetStateChanged(열림/닫힘 목표가 바뀜)마다 대상을 다시 찾는다 — 시트 목표 상태와 같은 프레임에 손가락이 옮겨 간다 (CR-02).
    ///       (d) 졸업: 졸업 단계가 시작될 때와 카드를 띄우기 직전에 UINav.GoToMain() — 큰 지도 위에서 졸업한다 (CR-27).
    /// 부팅 순서: MetaSession.Start(GameBootstrap.Awake)에서 이미 시작된 단계는 Start에서 다시 받아 그린다.
    /// </summary>
    public class FtueDirector : MonoBehaviour
    {
        [SerializeField] RectTransform _canvas;
        [SerializeField] RectTransform _popupRoot;
        [SerializeField] MainTabController _tabs;
        [SerializeField] CoachMarkView _coach;
        [SerializeField] StoryIntroView _intro;
        [SerializeField] FirstMeetingCardView _firstMeeting;

        [Header("말하는 친구")]
        [SerializeField] Sprite _speakerPortrait;
        [SerializeField] string _speakerName = "짝짝이";

        [Header("동작")]
        [SerializeField] string _gachaTableId = "friends";
        [SerializeField] float _pollInterval = 0.25f;
        [SerializeField] float _focusSeconds = 3f;
        [SerializeField] string _lampWaitText = "불빛 게이지가 가득 차면 눌러 주세요";
        [Tooltip("첫 불 밝히기: 스테이지에서 이만큼 잡았거나")]
        [SerializeField] int _lampKills = 8;
        [Tooltip("첫 불 밝히기: 단계가 시작되고 이 시간(초)이 지나면 게이지를 채운다")]
        [SerializeField] float _lampSeconds = 4f;
        [SerializeField] string _shortExpeditionToast = "첫 원정은 금방 돌아와요!";
        [Tooltip("막힌 곳을 여러 번 눌러 입력 막기를 풀었을 때 (설정은 상단 바 ≡ 메뉴 안에 있다)")]
        [SerializeField] string _releasedToast = "다른 곳도 눌러 볼 수 있어요. 튜토리얼은 메뉴 › 설정에서 건너뛸 수 있어요.";
        [Tooltip("졸업 뒤 우편에 받을 선물이 남아 있을 때 (M06)")]
        [SerializeField] string _mailHintToast = "개업 선물이 우편함에 와 있어요";
        [Tooltip("졸업 뒤 우편 안내: 우편 버튼에 손가락만 이만큼(초)")]
        [SerializeField] float _mailHintSeconds = 3f;
        [Tooltip("첫 만남 단계가 아닌데 소환 탭에 첫 만남 카드가 떠 있을 때 (소환이 먼저 열림)")]
        [SerializeField] string _firstMeetingCardText = "첫 만남은 무료예요!\n문을 열어 누가 찾아왔는지 봐요";
        [Tooltip("확정 소환 없이 첫 만남 단계가 지나갔을 때 소환 탭으로 데려가는 말")]
        [SerializeField] string _firstMeetingWaitingText = "소환 탭에서 첫 만남 친구가 기다려요\n먼저 문을 열어 맞이해 주세요";
        [Tooltip("단계 대상이 열린 시트 밑(덱 · 램프 등)이라 손가락이 사냥 버튼으로 갔을 때의 말 (UX-SPEC-IA §5.7)")]
        [SerializeField] string _redirectToMainText = RedirectToMainLine;

        [Header("축하 연출 (개방 카드 등 다른 화면 팝업에 붙인다)")]
        [SerializeField] Sprite _raysSprite;
        [SerializeField] Sprite _sparkSprite;
        [Tooltip("색종이 그림 (흰색, 여러 장)")]
        [SerializeField] Sprite[] _confettiSprites;
        [SerializeField] string[] _celebratedPopups = { "FeatureUnlockCard", "FeatureUnlockPopup" };

        TutorialSystem _tutorial;
        BattleSystem _battle;
        ExpeditionSystem _expedition;
        readonly FtueBattleGuard _guard = new();
        /// <summary>단계 보상을 풀어 날릴 때 다시 쓰는 목록 (단추 → 단추 · 실타래, 안 D)</summary>
        readonly System.Collections.Generic.List<MetaReward> _stepRewards = new();
        CoachTargetResolver _resolver;
        bool _started;

        FtueStepDef _step;
        string _message;
        RectTransform _target;
        RectTransform _dragTo;
        bool _contextDirty;
        float _poll;
        float _resolveTimer;
        bool _lastPopupOpen;
        int _lastTab = -2;
        /// <summary>(a) 이 단계의 첫 대상 찾기(팝업 없이)가 아직이다 — 그 결과가 시트 밑이면 한 번만 MAIN으로</summary>
        bool _mainOnFirstResolve;
        /// <summary>(b) 대상이 없는 첫 만남 단계에서 소환 시트를 이미 열었다 (단계마다 한 번 — 플레이어가 닫으면 다시 열지 않는다)</summary>
        bool _firstMeetingTabRequested;
        /// <summary>
        /// (a) 마지막 대상 찾기에서 단계 대상이 시트 밑이라 손가락이 사냥 버튼으로 갔다 (CoachTargetResolver.RedirectedToMain).
        /// 대상을 찾을 때만 바뀐다 — 켜져 있는 동안 말풍선은 _redirectToMainText
        /// </summary>
        bool _redirected;

        /// <summary>손가락이 사냥 버튼으로 돌아간 동안의 말 기본값 (_redirectToMainText)</summary>
        internal const string RedirectToMainLine = "전투 화면에서 할 일이 있어요\n사냥을 눌러 돌아가요";

        /// <summary>콘텐츠의 첫 만남(FirstMeetingSummon) 단계와 그 인덱스 (없으면 null / -1)</summary>
        FtueStepDef _firstMeetingStep;
        int _firstMeetingStepIndex = -1;
        string _firstMeetingLineage;
        /// <summary>확정 소환을 이미 받았음 (MetaFlags.FirstMeetingSummoned를 한 번 본 뒤로는 다시 찾지 않는다 — 튜토리얼이 끝난 뒤에도 매 프레임 보므로)</summary>
        bool _firstMeetingClaimed;
        /// <summary>지금 손가락이 단계 대상 대신 첫 만남(카드 또는 소환 탭)을 가리킴</summary>
        bool _firstMeetingDetour;
        bool _firstMeetingResultOpen;
        GachaResultPopup _gachaPopup;
        GachaPanel _gachaPanel;
        readonly GachaResult[] _singleResult = new GachaResult[1];

        int _shortExpeditionMinutes;
        int _expeditionCount = -1;
        bool _lampGate;
        float _lampTime;

        UIPopup _synergyDetail;
        UIPopup _characterDetail;
        UIPopup _synergyCodex;
        UIPopup _illustration;
        bool _synergyDetailOpen;
        bool _characterDetailOpen;
        bool _codexOpen;

        /// <summary>졸업했다 — 졸업 카드 · 출석부가 닫히고 조용해지면 우편 안내를 한 번 (M06)</summary>
        bool _mailHintPending;
        /// <summary>우편 안내를 기다리며 팝업 없이 흐른 시간</summary>
        float _mailHintQuiet;
        /// <summary>우편 안내 전에 팝업 · 졸업 카드 없이 이만큼(초) 조용해야 한다 (닫히는 연출 · 졸업 카드 → 출석부 사이)</summary>
        const float MailHintQuietSeconds = 0.6f;

        void Start()
        {
            if (!Game.IsReady || Game.Tutorial == null)
            {
                enabled = false;
                return;
            }
            _tutorial = Game.Tutorial;
            _battle = Game.Battle;
            _expedition = Game.Expedition;
            FindFirstMeetingStep();
            _resolver = new CoachTargetResolver(_canvas, _popupRoot)
            {
                FirstMeetingButton = _firstMeeting != null ? _firstMeeting.OpenButton : null,
                FirstMeetingLineage = _firstMeetingLineage,
            };

            var portrait = _speakerPortrait;
            if (portrait == null) portrait = Game.Content?.Characters?.GetForm("jjak_base")?.portrait;
            _coach.SetSpeaker(portrait, _speakerName);

            _tutorial.StepStarted += OnStepStarted;
            _tutorial.StepCompleted += OnStepCompleted;
            _tutorial.Finished += OnFinished;
            UINav.FocusRequested += OnFocusRequested;
            _battle.StageEnded += OnStageEnded;
            _expedition.Changed += OnExpeditionChanged;
            if (_firstMeeting != null) _firstMeeting.OpenPressed += OnFirstMeetingPressed;
            _coach.BlockReleased += OnBlockReleased;
            UIFrame.SheetStateChanged += OnSheetStateChanged;
            _guard.Attach(_battle, _tutorial);

            CachePopups();
            _started = true;

            // 부팅 때(GameBootstrap.Awake → MetaSession.Start) 이미 시작된 단계를 다시 그린다
            if (Game.IsMetaActive && _tutorial.IsStepRunning) OnStepStarted(_tutorial.Current);
        }

        void OnDisable() => SetFtueSignals(false, false);

        void OnDestroy()
        {
            SetFtueSignals(false, false);
            UINav.FocusRequested -= OnFocusRequested;
            if (!_started) return;
            _tutorial.StepStarted -= OnStepStarted;
            _tutorial.StepCompleted -= OnStepCompleted;
            _tutorial.Finished -= OnFinished;
            _battle.StageEnded -= OnStageEnded;
            _expedition.Changed -= OnExpeditionChanged;
            if (_firstMeeting != null) _firstMeeting.OpenPressed -= OnFirstMeetingPressed;
            _coach.BlockReleased -= OnBlockReleased;
            UIFrame.SheetStateChanged -= OnSheetStateChanged;
            _guard.Detach();
        }

        /// <summary>(c) 시트 목표 상태가 바뀜 (열기 · 닫기를 부른 그 프레임) — 다음 Tick에서 대상을 다시 찾는다 (CR-02)</summary>
        void OnSheetStateChanged(bool open) => _contextDirty = true;

        void CachePopups()
        {
            if (_popupRoot == null) return;
            _synergyDetail = PopupChild("SynergyDetailPopup");
            _characterDetail = PopupChild("CharacterDetailPopup");
            _synergyCodex = PopupChild("SynergyCodexPopup");
            _illustration = PopupChild("IllustrationPopup");
            foreach (var name in _celebratedPopups)
            {
                var card = Child(name);
                if (card != null) CelebrationFx.Attach(card, _raysSprite, _sparkSprite, _confettiSprites);
            }
            var gachaPanel = UIFind.Path(_canvas, CoachTargets.GachaPanel);
            if (gachaPanel != null) _gachaPanel = gachaPanel.GetComponent<GachaPanel>();
        }

        GameObject Child(string name)
        {
            var child = _popupRoot.Find(name);
            return child != null ? child.gameObject : null;
        }

        /// <summary>팝업 층의 그 이름 팝업 (UIPopup이 없으면 null — 열림은 IsOpen으로 본다)</summary>
        UIPopup PopupChild(string name)
        {
            var child = _popupRoot.Find(name);
            return child != null ? child.GetComponent<UIPopup>() : null;
        }

        /// <summary>
        /// FTUE 첫 만남 단계와 그 친구를 찾아 둔다 (카드는 단계가 시작되기 전·지난 뒤에도 뜰 수 있고,
        /// 친구는 편성 끌어 놓기 출처 카드를 찾을 때도 쓴다)
        /// </summary>
        void FindFirstMeetingStep()
        {
            _firstMeetingStep = null;
            _firstMeetingStepIndex = -1;
            _firstMeetingLineage = null;
            var db = Game.MetaDb;
            if (db == null) return;
            for (int i = 0; i < db.ftueSteps.Count; i++)
            {
                var step = db.ftueSteps[i];
                if (!IsFirstMeetingStep(step)) continue;
                _firstMeetingStep = step;
                _firstMeetingStepIndex = i;
                _firstMeetingLineage = step.actionArg;
                return;
            }
        }

        static bool IsFirstMeetingStep(FtueStepDef step) => step != null && step.action == FtueAction.FirstMeetingSummon;

        /// <summary>
        /// 첫 만남 확정 소환을 아직 받지 않았고 받을 수 있음 (M15). 단계 완료가 아니라 MetaFlags.FirstMeetingSummoned만 본다 —
        /// 첫 만남 단계의 완료 조건(GachaPull 1)은 평생 횟수라, 단계가 시작되기 전에 한 유료 소환으로도 채워진다.
        /// 콘텐츠에 첫 만남 단계가 있고 소환이 열렸으면 튜토리얼 상태와 상관없다 (M74 3b — 건너뛰었거나 끝난 뒤에도 전설 우두커니 확정을 받을 수 있다).
        /// 받은 뒤로는 기억해 두고 플래그를 다시 찾지 않는다. 할당 없음 (프레임당 한 번).
        /// </summary>
        bool IsFirstMeetingPending()
        {
            if (_firstMeetingClaimed || _firstMeetingStepIndex < 0 || string.IsNullOrEmpty(_firstMeetingLineage)) return false;
            if (Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned))
            {
                _firstMeetingClaimed = true;
                return false;
            }
            return Game.Features == null || Game.Features.IsUnlocked(FeatureId.Gacha);
        }

        // ───────────────────────────── 단계 이벤트 ─────────────────────────────

        void OnStepStarted(FtueStepDef step)
        {
            if (step == null) return;
            _step = step;
            // 문구는 단계가 바뀔 때 한 번만 정한다 (Describe는 문자열을 만든다)
            _message = string.IsNullOrEmpty(step.text) ? Game.Progress.Describe(step.complete) : step.text;
            _target = null;
            _dragTo = null;
            _contextDirty = true;
            _lampGate = false;
            _mainOnFirstResolve = true;
            _firstMeetingTabRequested = false;
            _redirected = false;

            switch (step.action)
            {
                case FtueAction.StoryIntro:
                    if (Game.Progress.HasFlag(MetaFlags.IntroSeen)) _tutorial.Signal(MetaFlags.IntroSeen);
                    else if (_intro != null && !_intro.IsPlaying) _intro.Play(OnIntroDone, step.text);
                    break;
                case FtueAction.EnableAutoBattle:
                    _battle.AutoMode = true;
                    break;
                case FtueAction.PrefillIdleChest:
                    if (!Game.Progress.HasFlag(MetaFlags.IdleChestPrefilled)) Game.IdleChest.Prefill(ParseFloat(step.actionArg, 2f));
                    break;
                case FtueAction.FirstMeetingSummon:
                    // 카드를 띄울지는 단계가 아니라 플래그로 정한다 (IsFirstMeetingPending) — 여기서는 친구만 맞춘다
                    if (!string.IsNullOrEmpty(step.actionArg))
                    {
                        _firstMeetingLineage = step.actionArg;
                        _resolver.FirstMeetingLineage = _firstMeetingLineage;
                    }
                    break;
                case FtueAction.ShortFirstExpedition:
                    _shortExpeditionMinutes = Mathf.Max(1, Mathf.RoundToInt(ParseFloat(step.actionArg, 1f)));
                    _expeditionCount = _expedition.Active.Count;
                    break;
                case FtueAction.ForceLampFull:
                    _lampGate = true;
                    _lampTime = 0f;
                    break;
                case FtueAction.Graduation:
                    // (d) 큰 지도 위에서 졸업한다: 카드는 이 단계가 끝나며 큐에 들어가고, 누가 꺼내 띄우든(이 감독 · 메타 발표자) 그 전에 시트를 내린다
                    UINav.GoToMain();
                    break;
            }

            if (step.hintOnly && !string.IsNullOrEmpty(step.text) && UIManager.Instance != null) UIManager.Instance.ShowToast(step.text);
        }

        void OnStepCompleted(FtueStepDef step)
        {
            if (step == null) return;
            // 단계 보상(이미 지급됨)은 대상 버튼에서 날아간다. 6단계 단추 250은 강화 x1 버튼에서 (M06).
            // 안 D: 데이터 단추는 지급 때 단추 · 실타래로 나뉘었으므로 같은 규칙으로 풀어서 날린다 (단추 → 상단 칩, 실타래 → 강화 탭 잔액 칩)
            if (step.rewards != null && step.rewards.Count > 0 && step.action != FtueAction.Graduation)
            {
                if (Game.IsReady && Game.MetaRewards != null)
                {
                    Game.MetaRewards.ResolveAll(step.rewards, _stepRewards);
                    RewardFlyer.Fly(_stepRewards, UIFind.IsShown(_target) ? _target : null);
                    _stepRewards.Clear();
                }
                else RewardFlyer.Fly(step.rewards, UIFind.IsShown(_target) ? _target : null);
            }
            // 한 단계 마침 소리 (AUDIO-SPEC 61 'FTUE step done') — 시작과 함께 끝나는 단계 · 인트로 · 졸업은 뺀다 (PlaysStepDoneSound)
            if (PlaysStepDoneSound(step)) UISfx.Play(UISfxKey.TutorialStep);
            // 첫 만남 단계가 확정 소환 없이 끝났으면(단계 전에 유료 소환 등) 카드는 남겨 둔다 — 받을 때까지 소환 탭을 덮는다
            if (IsFirstMeetingStep(step) && _firstMeeting != null && _firstMeeting.IsShown && !IsFirstMeetingPending()) _firstMeeting.Hide();
            if (step.action == FtueAction.ShortFirstExpedition) _shortExpeditionMinutes = 0;
            ClearStep();
        }

        /// <summary>
        /// 단계를 마칠 때 마침 소리(UISfxKey.TutorialStep)를 낼지 (테스트용 순수 함수).
        /// 내지 않는 것: 완료 조건이 '항상'(MetaConditionType.None — 3단계 방향 소환 · 8단계 AUTO 토스트 힌트)이라 시작과 함께 끝나는 단계,
        /// 인트로(책장 소리 · 이야기 음악) · 졸업(졸업 징글). hintOnly는 화면 안내만 정하는 값이라 보지 않는다 —
        /// 5단계 첫 보스(1-1 클리어) · 14단계 원정 받기처럼 플레이어가 해낸 순간에 끝나는 토스트 단계도 마침 소리를 낸다.
        /// </summary>
        internal static bool PlaysStepDoneSound(FtueStepDef step) =>
            step != null && step.action != FtueAction.StoryIntro && step.action != FtueAction.Graduation
            && step.complete != null && step.complete.type != MetaConditionType.None;

        void OnFinished()
        {
            ClearStep();
            // 카드는 일단 내린다 — 받지 않았으면(건너뜀 등) 다음 Tick이 소환 시트에서 다시 띄운다 (M74 3b)
            if (_firstMeeting != null) _firstMeeting.Hide();
            if (_intro != null && _intro.IsPlaying) _intro.Stop();
            bool skipped = Game.Data.meta.ftue.skipped;
            // 건너뛰었는데 자동 전투가 이미 열려 있으면 튜토리얼이 꺼 둔 AUTO를 돌려준다
            if (skipped && Game.Features.IsUnlocked(FeatureId.AutoBattle) && !_battle.AutoMode) _battle.AutoMode = true;
            // 끝까지 마쳤다(졸업): 졸업 카드 · 출석부 뒤에 개업 선물 우편을 한 번 알려 준다 (M06 — StepMailHint)
            if (!skipped)
            {
                _mailHintPending = true;
                _mailHintQuiet = 0f;
            }
        }

        void ClearStep()
        {
            _step = null;
            _message = null;
            _target = null;
            _dragTo = null;
            _lampGate = false;
            _firstMeetingDetour = false;
            _mainOnFirstResolve = false;
            _firstMeetingTabRequested = false;
            _redirected = false;
            _coach.Hide();
            _coach.Suppressed = false;
        }

        void OnBlockReleased()
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowToast(_releasedToast);
        }

        void OnIntroDone()
        {
            if (Game.IsReady && Game.IsMetaActive) _tutorial.Signal(MetaFlags.IntroSeen);
        }

        void OnStageEnded(BattleOutcome outcome, System.Collections.Generic.List<RewardData> rewards)
        {
            if (Game.IsMetaActive) _tutorial.Refresh(true);
        }

        void OnFocusRequested(string path)
        {
            if (!Game.IsReady || !Game.IsMetaActive) return;
            var target = UIFind.Path(_canvas, path);
            if (target != null) _coach.Focus(target, _focusSeconds);
        }

        // ───────────────────────────── 첫 만남 / 첫 원정 ─────────────────────────────

        /// <summary>
        /// 튜토리얼을 건너뛰어도 첫 만남 소환(전설 우두커니 확정)을 소환이 열리면 그대로 받을 수 있는지 (M74 3b) — 그렇다.
        /// 건너뛴 뒤에도 이 감독이 첫 만남 카드만은 계속 띄운다: IsFirstMeetingPending은 튜토리얼 상태를 보지 않고,
        /// 튜토리얼이 꺼진 Tick도 카드를 돌린다 (소환 시트가 열리면 덮고, 누르면 확정 소환 · 결과 팝업 · 리본).
        /// 설정의 튜토리얼 건너뛰기 확인 문구(SettingsPopup)가 읽는다: true = '첫 만남 소환은 소환이 열리면 그대로 받을 수 있어요' 줄,
        /// false였다면 '받을 수 없어요' 경고 줄. 상수가 아닌 속성이라 읽는 쪽 if 갈래에 CS0162 경고가 없다.
        /// </summary>
        public static bool FirstMeetingKeptAfterSkip => true;

        void OnFirstMeetingPressed()
        {
            if (!Game.IsMetaActive || string.IsNullOrEmpty(_firstMeetingLineage)) return;
            _contextDirty = true;
            if (Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned))
            {
                // 이미 받았다 (연타 등) — 확정 소환은 한 번만
                _firstMeeting.Hide();
                return;
            }
            var table = Game.Gacha.GetTable(_gachaTableId);
            if (table == null && Game.Content.GachaTables != null)
            {
                foreach (var candidate in Game.Content.GachaTables)
                {
                    table = candidate;
                    break;
                }
            }

            var result = Game.Gacha.PullGuaranteed(table, _firstMeetingLineage);
            Game.Progress.SetFlag(MetaFlags.FirstMeetingSummoned);
            _firstMeetingDetour = false;
            _firstMeeting.Hide();
            if (result == null)
            {
                Debug.LogWarning($"[FTUE] 첫 만남 친구 '{_firstMeetingLineage}'를 찾지 못했습니다.");
                return;
            }

            if (_gachaPopup == null) _gachaPopup = UIManager.Instance.Get<GachaResultPopup>();
            if (_gachaPopup == null || table == null) return;
            _singleResult[0] = result;
            string cost = RewardService.FormatAmount(table.costCurrency, Game.Gacha.GetCost(table, 1));
            var panel = _gachaPanel;
            _gachaPopup.Show(_singleResult, "1회 더 소환", cost, Game.Gacha.CanAfford(table, 1), () =>
            {
                if (panel != null) panel.Pull(1);
            });
            _firstMeetingResultOpen = true;
            _firstMeeting.SetRibbon(true, _gachaPopup.transform.Find("Window") as RectTransform);
        }

        void OnExpeditionChanged()
        {
            if (_shortExpeditionMinutes <= 0 || !Game.IsMetaActive) return;
            var active = _expedition.Active;
            if (_expeditionCount >= 0 && active.Count > _expeditionCount && !Game.Progress.HasFlag(MetaFlags.ShortFirstExpeditionUsed))
            {
                ExpeditionSave newest = null;
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i] != null && (newest == null || active[i].startUnix >= newest.startUnix)) newest = active[i];
                }
                if (newest != null)
                {
                    long end = newest.startUnix + _shortExpeditionMinutes * 60L;
                    if (newest.endUnix > end) newest.endUnix = end;
                    Game.Progress.SetFlag(MetaFlags.ShortFirstExpeditionUsed);
                    if (UIManager.Instance != null) UIManager.Instance.ShowToast(_shortExpeditionToast);
                }
            }
            _expeditionCount = active.Count;
        }

        // ───────────────────────────── 프레임 ─────────────────────────────

        void Update()
        {
            if (!_started) return;
            if (!Game.IsReady || !Game.IsMetaActive)
            {
                HideAll();
                return;
            }
            try
            {
                Tick(Time.unscaledDeltaTime);
                StepFtueSignals();
            }
            catch (System.NotImplementedException e)
            {
                // 메타 시스템이 아직 구현 중이면 조용히 멈춘다 (GameBootstrap의 안전장치와 같은 뜻)
                Debug.LogWarning($"[FTUE] 메타 기능이 아직 구현되지 않아 튜토리얼 연출을 멈춥니다: {e.Message}");
                HideAll();
                enabled = false;
            }
        }

        void Tick(float dt)
        {
            var ui = UIManager.Instance;
            bool popupOpen = ui != null && ui.HasOpenPopup;
            bool introPlaying = _intro != null && _intro.IsPlaying;
            WatchPopups();
            TryShowGraduation(popupOpen);
            StepFirstMeetingRibbon();
            StepFocus(popupOpen, introPlaying);
            StepMailHint(popupOpen, introPlaying, dt);

            if (!_tutorial.IsActive)
            {
                if (_coach.IsActive || _coach.Suppressed) ClearStep();
                // M74 3b: 건너뛰었거나 끝난 뒤에도 첫 만남 확정 소환을 받을 때까지 카드만은 돌린다 (단계 안내 · 손가락은 없다)
                StepFirstMeetingCard(popupOpen, _tabs != null ? _tabs.SelectedIndex : -1, IsFirstMeetingPending());
                return;
            }

            _poll += dt;
            if (_poll >= _pollInterval)
            {
                _poll = 0f;
                _tutorial.Refresh(_battle.IsBetweenStages);
            }

            // 첫 만남 카드는 단계가 돌고 있지 않아도(다음 단계 trigger를 기다리는 중 등) 소환 탭을 덮는다
            int tab = _tabs != null ? _tabs.SelectedIndex : -1;
            bool firstMeetingPending = IsFirstMeetingPending();
            StepFirstMeetingCard(popupOpen, tab, firstMeetingPending);
            if (_step == null || !_tutorial.IsStepRunning) return;

            _resolveTimer += dt;
            if (_contextDirty || popupOpen != _lastPopupOpen || tab != _lastTab || _resolveTimer >= 1f
                || (_target != null && !UIFind.IsShown(_target)))
            {
                ResolveTarget(popupOpen, tab, firstMeetingPending);
            }

            StepLamp(dt);
            Present(popupOpen, introPlaying, firstMeetingPending);
        }

        void HideAll()
        {
            SetFtueSignals(false, false);
            if (_coach.IsActive) _coach.Hide();
            _coach.FocusSuppressed = true;
            if (_firstMeeting != null && _firstMeeting.IsShown) _firstMeeting.Hide();
            if (_intro != null && _intro.IsPlaying) _intro.Stop();
        }

        /// <summary>
        /// 손가락만 잠깐(Focus)은 단계 안내와 따로 숨긴다 (M21): hintOnly 단계는 상관없고,
        /// 인트로 중이거나 그 대상 밖을 덮는 팝업이 열려 있을 때만 숨긴다. 튜토리얼이 끝난 뒤(가이드 퀘스트 [가기])에도 돈다.
        /// 첫 만남 카드가 소환 시트를 덮고 있으면 그 밑을 가리키던 손가락은 카드의 [문 열어 주기]로 옮긴다 (FocusOffFirstMeetingCover).
        /// </summary>
        void StepFocus(bool popupOpen, bool introPlaying)
        {
            var focus = FocusOffFirstMeetingCover();
            _coach.FocusSuppressed = focus != null && (introPlaying || (popupOpen && !_resolver.IsInsidePopup(focus)));
        }

        /// <summary>
        /// 손가락만 잠깐(Focus)이 첫 만남 카드 덮개 밑(탭 화면 안의 보이는 대상)을 가리키면 카드의 [문 열어 주기]로 옮긴다 — 남은 시간 그대로.
        /// 건너뛴 뒤에도 카드가 남아(M74 3b) 가이드 퀘스트 9 · 소환 미션(md_gacha · mw_gacha)의 [가기]가 소환 1회 버튼을 가리킬 수 있는데,
        /// 덮개가 그 버튼을 막고 손가락(코치 층)은 카드 위에 그려져 눌리지 않는 버튼을 가리키게 된다. 첫 만남이 그 소환 1회도 채운다.
        /// 카드가 뜬 그 프레임에도 부른다 (StepFirstMeetingCard — [가기]가 시트를 연 프레임에는 손가락이 카드보다 먼저 온다). 할당 없음.
        /// 반환 = (옮긴 뒤의) 손가락 대상, 없으면 null.
        /// </summary>
        RectTransform FocusOffFirstMeetingCover()
        {
            var focus = _coach.FocusTarget;
            if (focus == null || _firstMeeting == null || !_firstMeeting.IsShown) return focus;
            var open = _firstMeeting.OpenButton;
            var sheet = UIFrame.SheetRect;
            if (open == null || focus == open || sheet == null || !focus.IsChildOf(sheet) || !UIFind.IsShown(focus)) return focus;
            _coach.RetargetFocus(open);
            return open;
        }

        void ResolveTarget(bool popupOpen, int tab, bool firstMeetingPending)
        {
            _contextDirty = false;
            _lastPopupOpen = popupOpen;
            _lastTab = tab;
            _resolveTimer = 0f;
            _firstMeetingDetour = false;
            _redirected = false;
            if (_firstMeeting != null && _firstMeeting.IsShown && !popupOpen)
            {
                // 카드가 소환 탭을 덮고 있으면 손가락은 늘 [문 열어 주기] (첫 만남 단계가 아니면 첫 만남 말로 바꿔 말한다)
                _target = _firstMeeting.OpenButton;
                _dragTo = null;
                _firstMeetingDetour = !IsFirstMeetingStep(_step);
                return;
            }
            if (firstMeetingPending && !IsFirstMeetingStep(_step) && _tutorial.StepIndex > _firstMeetingStepIndex)
            {
                // 확정 소환 없이 첫 만남 단계가 지나갔다 (옛 세이브) — 우두커니 없이는 편성 단계(지킴이 4)가 막히므로 소환 탭부터
                _target = _resolver.Resolve(_firstMeetingStep, popupOpen, out _);
                _dragTo = null;
                _firstMeetingDetour = true;
                return;
            }
            _target = _resolver.Resolve(_step, popupOpen, out _dragTo);
            _redirected = _resolver.RedirectedToMain;
            // 팝업 안에서 찾은 것은 단계 대상이 아니다 — 팝업이 닫힌 뒤 처음 찾을 때 본다
            if (popupOpen || !_mainOnFirstResolve) return;

            // (a) 단계가 시작된 뒤 처음 찾은 대상이 시트 밑이면 한 번만 MAIN으로 (다시 시트를 열면 닫지 않고 손가락만 사냥 버튼으로)
            _mainOnFirstResolve = false;
            if (!_redirected) return;
            UINav.GoToMain();
            // 받는 쪽이 없으면(틀이 아직 없는 장면) 시트는 그대로 — 손가락은 사냥 버튼에 남는다
            if (UIFrame.SheetOpen) return;
            // 목표 상태가 바로 MAIN이 됐다: 같은 프레임에 원래 대상을 다시 찾는다 (닫힘 알림이 켠 _contextDirty도 이것으로 끝)
            _contextDirty = false;
            _lastTab = _tabs != null ? _tabs.SelectedIndex : -1;
            _target = _resolver.Resolve(_step, false, out _dragTo);
            _redirected = _resolver.RedirectedToMain;
        }

        /// <summary>
        /// 말풍선 글 (테스트용 순수 함수): 손가락이 시트 밑 대상 대신 사냥 버튼에 가 있으면(redirected, 사냥 버튼을 찾음 — hasTarget)
        /// 돌아가자는 말, 아니면 단계 글. 사냥 버튼이 없는 옛 탭 바(대상 null — 말풍선만)나 돌아가자는 말이 비었으면 단계 글 그대로.
        /// 고르기만 한다 (새 문자열을 만들지 않는다) — 말풍선은 글이 바뀔 때만 다시 쓴다 (SpeechBubbleView.Say).
        /// </summary>
        internal static string CoachLine(string stepLine, string redirectLine, bool redirected, bool hasTarget) =>
            redirected && hasTarget && !string.IsNullOrEmpty(redirectLine) ? redirectLine : stepLine;

        void Present(bool popupOpen, bool introPlaying, bool firstMeetingPending)
        {
            var step = _step;
            bool detour = _firstMeetingDetour && firstMeetingPending;
            bool hintOnly = step.hintOnly && !detour;
            bool targetInPopup = _resolver.IsInsidePopup(_target);
            _coach.Suppressed = introPlaying || hintOnly || (popupOpen && !targetInPopup);
            if (detour)
            {
                bool onCard = _firstMeeting != null && _target != null && _target == _firstMeeting.OpenButton;
                _coach.Present(onCard ? _firstMeetingCardText : _firstMeetingWaitingText, _target, false);
                return;
            }
            if (hintOnly || step.action == FtueAction.StoryIntro)
            {
                if (_coach.IsActive) _coach.Hide();
                return;
            }

            string message = CoachLine(_message, _redirectToMainText, _redirected, _target != null);
            if (!ReferenceEquals(message, _message))
            {
                // (a) 대상이 시트 밑이라 손가락이 사냥 버튼에 있다 — 돌아가자는 말 + 톡톡 (램프를 기다리는 단계여도: 램프가 시트 밑이라 보이지 않는다)
                _coach.Present(message, _target, step.blockInput);
                return;
            }
            if (_lampGate && !LampReady())
            {
                // 게이지가 찰 때까지(8마리째 · 4초 뒤 ForceLampFull)는 어둠·손가락 없이 테두리와 안내만 (차는 순간 손가락)
                _coach.Present(string.IsNullOrEmpty(_step.text) ? _lampWaitText : message, _target, false, dim: false,
                    pointer: CoachMarkView.PointerStyle.None);
                return;
            }
            if (_dragTo != null)
            {
                _coach.PresentDrag(message, _target, _dragTo);
                return;
            }
            _coach.Present(message, _target, step.blockInput && _target != null);
        }

        /// <summary>
        /// FTUE-01 4단계: "처치 8회째 게이지를 강제로 가득" — 이 단계에서 8마리째를 잡았거나 4초가 지나면 Game.Battle.ForceLampFull
        /// (SPEC4 §13.3 — 메인 스테이지 · 전투 중 · 게이지 &lt; 1일 때만 채운다). 결과 배너 중이면 다음 전투에서 다시 채운다.
        /// </summary>
        void StepLamp(float dt)
        {
            if (!_lampGate || LampReady()) return;
            _lampTime += dt;
            var sim = _battle.Sim;
            if (sim != null && (sim.Kills >= _lampKills || _lampTime >= _lampSeconds)) _battle.ForceLampFull();
        }

        /// <summary>튜토리얼 연출 표시를 지금 단계에 맞춘다 (Update — Tick 뒤). 할당 없음</summary>
        void StepFtueSignals()
        {
            bool running = _tutorial.IsStepRunning;
            SetFtueSignals(WantsFirstLampBeat(_step, running, _lampGate), WantsFirstImpression(_step, running));
        }

        /// <summary>AppSignals의 튜토리얼 연출 표시를 바뀔 때만 쓴다</summary>
        static void SetFtueSignals(bool lampBeat, bool impression)
        {
            if (AppSignals.FtueFirstLampBeat != lampBeat) AppSignals.FtueFirstLampBeat = lampBeat;
            if (AppSignals.FtueFirstImpression != impression) AppSignals.FtueFirstImpression = impression;
        }

        /// <summary>첫 불 밝히기 느린 화면을 켤지 (테스트용 순수 함수): 4단계(ForceLampFull)가 돌고 있고 게이지 안내 중</summary>
        internal static bool WantsFirstLampBeat(FtueStepDef step, bool stepRunning, bool lampGate) =>
            stepRunning && lampGate && step != null && step.action == FtueAction.ForceLampFull;

        /// <summary>첫 전투 첫인상을 켤지 (테스트용 순수 함수): 1단계(BattleProtection — 첫 전투 슬로건)가 돌고 있을 때만</summary>
        internal static bool WantsFirstImpression(FtueStepDef step, bool stepRunning) =>
            stepRunning && step != null && step.action == FtueAction.BattleProtection;

        bool LampReady()
        {
            var sim = _battle.Sim;
            return sim != null && sim.CanLightLamp;
        }

        /// <summary>
        /// 첫 만남 카드: 확정 소환을 아직 받지 않았으면(pending) 소환 시트 위(ContentArea 자리)를 덮는다 — 첫 만남 단계가 시작되기 전이든 지난 뒤든.
        /// 시트가 열려 있을 때만 덮을 자리가 있다 (MAIN에서는 ContentArea가 꺼져 있다, UX-SPEC-IA §5.7 (b)).
        /// 대상이 없는 첫 만남 단계는 예전에는 탭과 상관없이 덮었다 — 이제는 소환 시트를 연다 (단계마다 한 번, 플레이어가 닫으면 다시 열지 않는다).
        /// 팝업이 열려 있으면 숨긴다 (덮개가 팝업보다 위 층이라).
        /// </summary>
        void StepFirstMeetingCard(bool popupOpen, int tab, bool pending)
        {
            if (_firstMeeting == null) return;
            if (pending && !popupOpen && tab != (int)MainTab.Gacha && !_firstMeetingTabRequested
                && IsFirstMeetingStep(_step) && _tutorial.IsStepRunning && string.IsNullOrEmpty(_step.coachTarget))
            {
                _firstMeetingTabRequested = true;
                UINav.GoToTab(MainTab.Gacha);
                // 고르기는 바로 일어난다 (목표 상태) — 이번 프레임에 카드를 띄운다
                if (_tabs != null) tab = _tabs.SelectedIndex;
            }
            bool want = pending && !popupOpen && tab == (int)MainTab.Gacha;
            if (want == _firstMeeting.IsShown) return;
            if (want)
            {
                var db = Game.Content?.Characters;
                var lineage = db?.GetLineage(_firstMeetingLineage);
                _firstMeeting.Show(lineage, lineage != null ? db.GetForm(lineage.baseFormId)?.portrait : null);
                // 이 프레임에 이미 덮개 밑을 가리키던 손가락([가기]가 소환 시트를 열며 요청)을 카드 버튼으로 (StepFocus는 카드보다 먼저 돌았다)
                FocusOffFirstMeetingCover();
            }
            else
            {
                _firstMeeting.Hide();
            }
            _contextDirty = true;
        }

        /// <summary>
        /// '첫 만남 확정 소환' 리본은 결과 창이 첫 만남 결과를 보여 주는 동안만 (GachaResultPopup.ShowsFirstMeeting — 열려 있고 결과가 모두 IsGuaranteed).
        /// 그 창에서 [1회 더 소환]을 하면 다음 결과부터 내린다 (잔액 칩은 GachaResultPopup이 그 창이 닫힐 때까지 숨겨 두어 겹치지 않는다)
        /// </summary>
        void StepFirstMeetingRibbon()
        {
            if (!_firstMeetingResultOpen) return;
            if (_gachaPopup != null && _gachaPopup.ShowsFirstMeeting) return;
            _firstMeetingResultOpen = false;
            if (_firstMeeting != null) _firstMeeting.SetRibbon(false);
        }

        /// <summary>
        /// 다른 화면 팝업이 열린 순간을 보고 메타 플래그를 세운다 (그 화면 파일을 고치지 않고). 열림은 UIPopup.IsOpen으로 본다 —
        /// 닫힌 팝업은 닫히는 연출 0.12초 동안 켜져 있어 activeSelf로는 그 사이 다시 연 것을 새로 열림으로 알 수 없었다 (WP8-COACH-TOPPOPUP).
        /// </summary>
        void WatchPopups()
        {
            bool open = _synergyDetail != null && _synergyDetail.IsOpen;
            if (open && !_synergyDetailOpen) _tutorial.Signal(MetaFlags.SynergyDetailViewed);
            _synergyDetailOpen = open;

            open = _characterDetail != null && _characterDetail.IsOpen;
            if (open && !_characterDetailOpen) Game.Progress.SetFlag(MetaFlags.EvolutionTreeViewed);
            _characterDetailOpen = open;

            open = (_synergyCodex != null && _synergyCodex.IsOpen) || (_illustration != null && _illustration.IsOpen);
            if (open && !_codexOpen) Game.Progress.SetFlag(MetaFlags.CodexOpened);
            _codexOpen = open;
        }

        /// <summary>
        /// 졸업 뒤 우편 안내 (M06, 한 번): 졸업 카드가 아직 큐에 있거나 팝업(졸업 카드 · 출석부 첫 도장) · 인트로가 떠 있으면 기다리고,
        /// 팝업 없이 0.6초 조용해지면 — 우편에 받을 선물이 남아 있을 때만 — 우편 버튼에 어둠 없는 손가락 3초(CoachMarkView.Focus) + 토스트.
        /// 받을 것이 없으면 조용히 끝낸다. 기다리는 동안 매 프레임 가벼운 확인만 (할당 없음).
        /// 의도한 차이 (UX-SPEC §2.12 M06 · 계획 8.02는 '3초 hintOnly 펄스'): 버튼을 맥박치게 하지 않고 어둠 없는 손가락을 쓴다 —
        /// 졸업까지 10분 동안 따라온 같은 손가락이라 더 잘 보이고, 팝업 · 인트로가 덮으면 숨는 Focus 규칙(StepFocus)을 그대로 쓰며,
        /// 우편 버튼 자신의 눌림 크기 연출(PressScale)과 크기를 두고 다투지 않는다. 어둠 · 입력 막기가 없어 'hintOnly'의 뜻(막지 않는 안내)은 지킨다.
        /// 이 안내를 보는 캡처 · 테스트는 아직 없다 (UX-SPEC §5의 04b는 6단계 보상 화면이다) — 졸업 뒤 확인은 WP-9에 맡겼다:
        /// 졸업 카드 · 출석부가 닫히고 받을 우편이 있으면 손가락 대상 = TopBar/MailButton, 토스트 = '개업 선물이 우편함에 와 있어요'.
        /// </summary>
        void StepMailHint(bool popupOpen, bool introPlaying, float dt)
        {
            if (!_mailHintPending) return;
            var queue = Game.Popups;
            if (popupOpen || introPlaying || (queue != null && queue.Contains(MetaPopupKind.FtueGraduation)))
            {
                _mailHintQuiet = 0f;
                return;
            }
            _mailHintQuiet += dt;
            if (_mailHintQuiet < MailHintQuietSeconds) return;
            _mailHintPending = false;
            var mail = Game.Mail;
            if (mail == null || !mail.HasClaimableRewards) return;
            var button = UIFind.Path(_canvas, CoachTargets.MailButton);
            if (button != null && UIFind.IsShown(button)) _coach.Focus(button, _mailHintSeconds);
            if (UIManager.Instance != null) UIManager.Instance.ShowToast(_mailHintToast, ToastKind.Info);
        }

        /// <summary>
        /// 졸업 카드 요청이 큐 맨 앞이면 스테이지 사이에 꺼내 띄운다 (메타 발표자가 먼저 꺼내면 그쪽이 띄운다).
        /// 띄우기 직전에 MAIN으로 (d) — 졸업 단계가 시작될 때도 내렸지만, 그 사이 플레이어가 시트를 다시 열었을 수 있다.
        /// </summary>
        void TryShowGraduation(bool popupOpen)
        {
            var queue = Game.Popups;
            if (queue == null || popupOpen || !_battle.IsBetweenStages) return;
            if (_intro != null && _intro.IsPlaying) return;
            if (!queue.TryPeek(out var request) || request.Kind != MetaPopupKind.FtueGraduation) return;
            if (!queue.TryDequeue(true, false, out request) || request.Kind != MetaPopupKind.FtueGraduation) return;
            UINav.GoToMain();
            GraduationCardPopup.Present();
        }

        static float ParseFloat(string text, float fallback) =>
            !string.IsNullOrEmpty(text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0f
                ? value
                : fallback;
    }
}
