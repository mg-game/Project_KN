using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 메타 기능의 세션 흐름을 묶는다 (GameBootstrap이 부르는 유일한 메타 창구, 순수 C#).
    ///   Start (부팅, Game.Setup + 시작 친구 지급 뒤):
    ///     (Refresh 전에 meta.clock.maxSeenUnix를 읽어 둠) → DailyClock.Refresh → (첫 실행이면 설치 날 기록)
    ///     → (4차 마이그레이션 뒤 첫 Start면 PlayMinutes를 오늘까지 센 설치 일수로 채움, CR4-24) → 시스템 연결(아래)
    ///     → FeatureGate.Evaluate(옛 세이브는 조용히) → 오늘 첫 접속이면 Login 카운터
    ///     → 비운 시간 = 지금 − max(meta.clock.lastSessionEndUnix, PlayerData.lastSaveUnix, 읽어 둔 maxSeen) (되돌림이면 0)
    ///     → IdleChest.AddOfflineTime → lastSessionEndUnix = 유효 시각 (이 공백은 한 번만 센다) → PopupQueue.BeginStartupBatch + 접속 요청:
    ///        복귀 화면(비운 시간 ≥ returnPopupMinSeconds이고 바구니에 받을 것이 있음) → 출석부(CanCheckIn).
    ///        공지는 부팅(Awake) 뒤에 비동기로 불러오므로 큐에 넣지 않는다 — 발표자가 출석부 다음(Notice 자리)에
    ///        튜토리얼이 끝났으면 UIManager.ShowStartupNotices()를 돌린다.
    ///     → 새 게임이라 튜토리얼이 0단계부터 시작하면 자동 전투를 끈다 (Battle.AutoMode = false, G13 — FtueAction.EnableAutoBattle로 다시 켬.
    ///        튜토리얼을 건너뛰었거나 마친 뒤에 자동 전투가 열리면 여기서 다시 켠다)
    ///     → Tutorial.Begin → RegisterRedDots.
    ///   Tick (매 프레임, 실제 시간): DailyClock.Tick → Tutorial.Tick → Power.Refresh → Progress.Flush → RedDots.Refresh. 할당 없음.
    ///     일시정지(OnPause ~ OnResume) 중에 프레임이 돌아도(runInBackground · 에디터) DailyClock.Tick은 건너뛴다 — 돌리면 maxSeen이
    ///     지금으로 올라가 복귀 때 비운 시간(바닥 = maxSeen)이 0으로 잘린다 (RI-7). 날 바뀜은 복귀의 Refresh가 알린다.
    ///   OnPause: meta.clock.lastSessionEndUnix = 유효 시각 (GameBootstrap이 바로 저장).
    ///   OnResume: (maxSeen을 읽어 둠) → DailyClock.Refresh → 비운 시간만큼 AddOfflineTime → lastSessionEndUnix = 유효 시각
    ///     → 복귀 화면/출석부 요청(접속 묶음) → Returned. 출석부는 오래 비웠거나(≥ returnPopupMinSeconds) 날이 바뀐 복귀에서만 다시 띄운다
    ///     (잠금 화면·로그인 창처럼 잠깐 나갔다 오면 빨간 점만).
    ///   OnQuit: lastSessionEndUnix = 유효 시각.
    /// 비운 시간 기준에 유효 시각과 maxSeen을 쓰는 까닭 (시계 되돌림 악용 막기): 기기 시계를 하루 전으로 돌렸다가(되돌림이라 0초)
    /// 그 상태로 나가면 기기 시각으로는 마지막 세션 끝·마지막 저장이 하루 전으로 찍힌다. 시계를 원래대로 돌리고 들어오면
    /// 하루를 비운 것처럼 보이므로, 플레이어가 되돌릴 수 없는 maxSeen(실행 중 1초마다 올라감)과 유효 시각을 바닥으로 삼는다.
    /// 구독 규칙 (중복 구독 금지):
    ///   · 자기 상태 갱신용 구독은 각 시스템 생성자에서 한다 — ProgressTracker(기존 시스템 이벤트), Missions/Attendance/IdleChest/Progress
    ///     (DailyClock.DayChanged/WeekChanged → 자기 오늘 상태 비우기), GuideQuest/FeatureGate(Progress.Changed → Refresh/Evaluate),
    ///     Power(Formation/Characters/Synergy/Growth/Costumes.Changed → 더러움).
    ///   · 시스템 사이 연결은 여기 Start에서 한 번 — DailyClock.DayChanged → Login 카운터 + 출석부 요청(접속 중 날이 바뀌면),
    ///     Progress.Changed → Tutorial.Refresh(Battle.IsBetweenStages) + RedDots.MarkDirty,
    ///     Features.Unlocked(AutoBattle) → 튜토리얼이 끝났으면(건너뜀 포함) Battle.AutoMode = true,
    ///     Mail/Notice/Expedition/Evolution/Characters/Costumes/Dungeon/Features/Missions/Attendance/IdleChest/GuideQuest.Changed → RedDots.MarkDirty.
    /// 복귀 토스트(비운 시간이 짧을 때 "단추 1,800 모였어요")는 발표자(M3)가 Returned를 받아 띄운다.
    /// <para>
    /// 빨간 점 갱신은 필요한 가지만 더럽힌다 (전투 중 Progress.Changed마다 모든 잎을 평가하지 않게):
    /// 진행 → 일지·가이드 퀘스트, 친구 가지(진화 제작 가능 계산에 할당이 든다)는 친구·진화·의상·재료가 바뀌는 사건 때만,
    /// 시간이 지나면 바뀌는 잎(바구니 1시간·가득, 원정 완료, 던전 입장권 날짜 = DailyClock 날 키, 05:00 초기화)은 1초마다,
    /// 편성 빈칸(tab.formation.empty)은 편성·친구·원정이 바뀔 때만.
    /// 전투 HUD 토글(자동 전투·2배속·자동 불 밝히기 방식)은 "새로 열림" 점을 등록하지 않는다 (M09 — 개방 카드와 한 번의 반짝임으로 충분).
    /// 복귀(OnResume) 때 띄울 접속 요청이 하나도 없으면 접속 묶음을 열지 않는다 (공지는 부팅 때만).
    /// 발표자가 EndStartupBatch를 부르지 못해도 StartupBatchTimeout초 뒤에 묶음을 닫아 세션 모달이 막히지 않게 한다.
    /// </para>
    /// <para>
    /// 4차 (SPEC4 §2.4 · SPEC5 §2.8):
    ///   · 메타 모듈(IMetaModule — 도감 · 기록 · 이벤트 …)을 Game.SetupMeta가 AddModule로 넣은 순서대로 부른다:
    ///     Start = 기존 연결 → 모듈 Connect → 기능 개방 평가 → 접속 카운터 → 비운 시간 → 모듈 BeforeAwayApplied(비운 초, true) → 바구니 → 접속 요청
    ///     → 모듈 AfterStartup(true) → 튜토리얼 → 빨간 점(모듈 포함). OnResume도 같은 차례(boot = false) — 모듈이 접속 종류 요청을 넣으면 접속 묶음을 연다.
    ///     Tick = 진행 기록 Flush 뒤 · 빨간 점 Refresh 앞, 날 바뀜 = OnDayChanged. 모듈이 예외를 내면 그 모듈만 끄고 한 번 기록한다.
    ///   · 앱을 켜 둔 시간 (CounterKey.PlayMinutes): 앞에 있는 시간(잠자기 포함, 일시정지 · 복귀 첫 프레임 제외, 한 프레임 최대 MaxPlayDeltaSeconds)을 모아
    ///     1분마다 한 번 Increment. 같은 시간을 기능 개방 카드 간격(FeatureGateSystem.TickCards)에도 넣는다.
    ///   · Connect에서 4차 훅을 잇는다: 친구 · 진화 · 의상 · 던전 FeatureResolver = 기능 개방, 던전 입장권 모아 두기 상한, 튜토리얼 전투 보호(스테이지 ≤ 3).
    /// </para>
    /// </summary>
    public class MetaSession
    {
        /// <summary>(비운 초) 부팅·복귀 처리가 끝남. 짧은 복귀(복귀 화면 기준 미만)는 이것으로 토스트만</summary>
        public event Action<long> Returned;

        /// <summary>접속 묶음이 이 시간(실제 초) 넘게 열려 있으면 닫는다 (발표자 안전장치)</summary>
        public const float StartupBatchTimeout = 120f;
        /// <summary>앱을 켜 둔 시간에 한 프레임이 더할 수 있는 최대 초 (멈칫 · 디버거로 크게 튄 프레임)</summary>
        public const float MaxPlayDeltaSeconds = 10f;
        /// <summary>튜토리얼 전투 보호를 거는 마지막 메인 스테이지 (1-3, FtueBattleGuard와 같은 규칙)</summary>
        public const int TutorialProtectionMaxStage = 3;
        /// <summary>시간으로 바뀌는 빨간 점을 다시 보는 간격 (초)</summary>
        const float TimedDotInterval = 1f;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;
        readonly PowerSystem _power;
        readonly RedDotSystem _redDots;
        readonly PopupQueue _popups;
        readonly FeatureGateSystem _features;
        readonly GuideQuestSystem _guide;
        readonly MissionSystem _missions;
        readonly AttendanceSystem _attendance;
        readonly IdleChestSystem _idleChest;
        readonly TutorialSystem _tutorial;
        readonly MetaRewardService _rewards;
        readonly BattleSystem _battle;
        readonly NoticeSystem _notice;
        readonly MailSystem _mail;
        readonly ExpeditionSystem _expedition;
        readonly DungeonSystem _dungeon;
        readonly EvolutionSystem _evolution;
        readonly CharacterSystem _characters;
        readonly CostumeSystem _costumes;
        readonly FormationSystem _formation;

        bool _connected;
        bool _characterDotsDirty;
        float _timedDotTimer;
        float _batchTimer;
        /// <summary>던전 입장권 점을 마지막으로 본 날 키 (DailyClock, 05:00 초기화)</summary>
        string _lastDayKey;

        // ── 4차 모듈 (넣은 순서대로 부른다, 예외를 낸 모듈은 끈다) ──
        readonly List<IMetaModule> _modules = new();
        readonly List<bool> _moduleOff = new();
        /// <summary>
        /// 모듈마다 Connect를 이미 불렀는지 (모듈당 한 번만 — 모듈은 Connect에서 이벤트를 구독한다). Start가 IsStarted 전에 예외로 끊기면
        /// 다음 OnResume이 Start를 다시 부르는데, 그때 이미 이은 모듈이 두 번 구독해 두 번 세지 않게 (그 사이 넣은 모듈만 잇는다)
        /// </summary>
        readonly List<bool> _moduleConnected = new();

        // ── 앱을 켜 둔 시간 (PlayMinutes) ──
        /// <summary>아직 1분이 안 된 앞에 있는 시간 (초, 저장 안 함)</summary>
        float _playSeconds;
        /// <summary>백그라운드 (OnPause ~ OnResume) — 세지 않고, 시계(DailyClock.Tick)도 돌리지 않는다 (RI-7)</summary>
        bool _playPaused;
        /// <summary>복귀 첫 프레임의 dt는 백그라운드 시간을 품을 수 있어 버린다</summary>
        bool _skipPlayDelta;

        public MetaSession(PlayerData data, MetaDatabase db, DailyClock clock, ProgressTracker progress, PowerSystem power,
            RedDotSystem redDots, PopupQueue popups, FeatureGateSystem features, GuideQuestSystem guide, MissionSystem missions,
            AttendanceSystem attendance, IdleChestSystem idleChest, TutorialSystem tutorial, MetaRewardService rewards,
            BattleSystem battle, NoticeSystem notice, MailSystem mail, ExpeditionSystem expedition, DungeonSystem dungeon,
            EvolutionSystem evolution, CharacterSystem characters, CostumeSystem costumes, FormationSystem formation = null)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _progress = progress;
            _power = power;
            _redDots = redDots;
            _popups = popups;
            _features = features;
            _guide = guide;
            _missions = missions;
            _attendance = attendance;
            _idleChest = idleChest;
            _tutorial = tutorial;
            _rewards = rewards;
            _battle = battle;
            _notice = notice;
            _mail = mail;
            _expedition = expedition;
            _dungeon = dungeon;
            _evolution = evolution;
            _characters = characters;
            _costumes = costumes;
            _formation = formation;
        }

        /// <summary>Start를 마침</summary>
        public bool IsStarted { get; private set; }

        /// <summary>마지막 부팅/복귀 때 비운 시간 (초)</summary>
        public long LastAwaySeconds => _data.meta.returnScreen.lastAwaySeconds;

        MetaSave Meta => _data.meta ??= new MetaSave();

        /// <summary>
        /// 4차 메타 모듈을 넣는다 (Game.SetupMeta가 정해진 순서로, Start 전에). null · 같은 모듈 두 번은 무시.
        /// Start 뒤에 넣으면 그 자리에서 Connect · RegisterRedDots까지 해 준다 (테스트 · 늦게 만든 모듈)
        /// </summary>
        public void AddModule(IMetaModule module)
        {
            if (module == null || _modules.Contains(module)) return;
            _modules.Add(module);
            _moduleOff.Add(false);
            _moduleConnected.Add(false);
            if (!IsStarted) return;
            int index = _modules.Count - 1;
            _moduleConnected[index] = true;
            try
            {
                module.Connect();
                if (_redDots != null) module.RegisterRedDots(_redDots);
            }
            catch (Exception e)
            {
                DisableModule(index, "AddModule", e);
            }
        }

        /// <summary>넣은 모듈 (넣은 순서)</summary>
        public IReadOnlyList<IMetaModule> Modules => _modules;

        /// <summary>예외를 내서 꺼진 모듈인지 (테스트 · 개발 HUD)</summary>
        public bool IsModuleDisabled(IMetaModule module)
        {
            int index = module != null ? _modules.IndexOf(module) : -1;
            return index >= 0 && _moduleOff[index];
        }

        /// <summary>아직 1분이 안 된 앱을 켜 둔 시간 (초, 다음 PlayMinutes까지)</summary>
        public float PendingPlaySeconds => _playSeconds;

        /// <summary>부팅 처리 (위 순서). 두 번 부르면 무시</summary>
        public void Start()
        {
            if (IsStarted) return;

            long seenFloor = MaxSeenUnix; // Refresh가 지금 시각으로 올리기 전에 읽는다
            _clock?.Refresh();
            ApplyMigrationPlayMinutesSeed();
            Connect();
            ModulesConnect();
            _features?.Evaluate();
            CountLogin();

            long away = ComputeAwaySeconds(seenFloor);
            (Meta.returnScreen ??= new ReturnSave()).lastAwaySeconds = away;
            ModulesBeforeAwayApplied(away, true);
            if (away > 0) _idleChest?.AddOfflineTime(away);
            ConsumeAwayGap();

            _popups?.BeginStartupBatch();
            _batchTimer = 0f;
            RequestStartupPopups(away, _attendance != null && _attendance.CanCheckIn);
            ModulesAfterStartup(true);

            if (IsFreshTutorial()) _battle.AutoMode = false;
            _tutorial?.Begin();
            RegisterRedDots();
            _lastDayKey = _clock?.DayKey;

            IsStarted = true;
            Returned?.Invoke(away);
        }

        /// <summary>매 프레임 (Time.unscaledDeltaTime)</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!IsStarted) return;
            float dt = unscaledDeltaTime > 0f && !float.IsInfinity(unscaledDeltaTime) ? unscaledDeltaTime : 0f;

            if (!_playPaused) _clock?.Tick(dt); // 일시정지 중 maxSeen을 올리면 복귀 때 비운 시간이 잘린다 (RI-7)
            _tutorial?.Tick(dt);
            _power?.Refresh();
            TickPlayTime(dt);
            _progress?.Flush();
            ModulesTick(dt);

            if (_characterDotsDirty)
            {
                _characterDotsDirty = false;
                _redDots?.MarkDirty(RedDotPaths.TabCharacters);
            }
            _timedDotTimer += dt;
            if (_timedDotTimer >= TimedDotInterval)
            {
                _timedDotTimer = 0f;
                MarkTimedDots();
            }
            _redDots?.Refresh();

            if (_popups != null && _popups.InStartupBatch)
            {
                _batchTimer += dt;
                if (_batchTimer >= StartupBatchTimeout) _popups.EndStartupBatch();
            }
        }

        /// <summary>백그라운드 진입 (저장 직전). 앱을 켜 둔 시간은 복귀까지 세지 않는다</summary>
        public void OnPause()
        {
            _playPaused = true;
            MarkSessionEnd();
        }

        /// <summary>백그라운드에서 복귀</summary>
        public void OnResume()
        {
            _playPaused = false;
            _skipPlayDelta = true;
            if (!IsStarted)
            {
                Start();
                return;
            }

            long seenFloor = MaxSeenUnix; // Refresh가 지금 시각으로 올리기 전에 읽는다
            bool dayChanged = _clock != null && _clock.Refresh();
            long away = ComputeAwaySeconds(seenFloor);
            (Meta.returnScreen ??= new ReturnSave()).lastAwaySeconds = away;
            ModulesBeforeAwayApplied(away, false);
            if (away > 0) _idleChest?.AddOfflineTime(away);
            ConsumeAwayGap();
            CountLogin();

            bool attendance = WantsAttendanceOnResume(away, dayChanged);
            bool batch = WantsReturnPopup(away) || attendance;
            if (batch)
            {
                _popups?.BeginStartupBatch();
                _batchTimer = 0f;
                RequestStartupPopups(away, attendance);
            }
            // 모듈이 접속 종류(다시 만난 날 · 이벤트 시작)를 새로 넣었으면 묶음을 연다 — 원래 큐에 있던 요청만으로는 열지 않는다 (지금 규칙 그대로)
            int startupBefore = _popups != null ? _popups.CountStartupKinds() : 0;
            ModulesAfterStartup(false);
            if (!batch && _popups != null && _popups.CountStartupKinds() > startupBefore)
            {
                _popups.BeginStartupBatch();
                _batchTimer = 0f;
            }
            _redDots?.MarkDirty();
            Returned?.Invoke(away);
        }

        /// <summary>앱 종료 (저장 직전)</summary>
        public void OnQuit() => MarkSessionEnd();

        /// <summary>
        /// 기본 빨간 점 등록 (RedDotPaths): 일지(숫자) / 출석 / 바구니 / 가이드 퀘스트 / 우편(숫자) / 공지(점, 설정 아래) /
        /// 원정 완료 / 던전 입장권(그날 처음, 1층 이상 돌파했을 때) / 편성 빈칸 / 계보별 진화·공명·의상 /
        /// 새로 열린 기능 (전투 HUD 토글 제외 — IsHudToggle).
        /// Start가 부른다. 같은 경로를 다시 등록하면 덮어쓴다.
        /// </summary>
        public void RegisterRedDots()
        {
            var dots = _redDots;
            if (dots == null) return;

            if (_missions != null) dots.RegisterCount(RedDotPaths.TopJournal, () => _missions.TotalClaimableCount);
            if (_attendance != null) dots.Register(RedDotPaths.TopAttendance, () => _attendance.CanCheckIn);
            if (_idleChest != null) dots.Register(RedDotPaths.HudIdleChest, () => _idleChest.HasRedDot);
            if (_guide != null) dots.Register(RedDotPaths.HudGuideQuest, () => _guide.IsVisible && _guide.IsComplete);
            if (_mail != null) dots.RegisterCount(RedDotPaths.TopMail, () => _mail.Count);
            if (_notice != null) dots.Register(RedDotPaths.TopSettingsNotice, HasUnreadNotice);

            if (_expedition != null)
            {
                var zones = _expedition.Db != null ? _expedition.Db.zones : null;
                if (zones != null && zones.Count > 0)
                {
                    foreach (var zone in zones)
                    {
                        if (zone == null || string.IsNullOrEmpty(zone.id)) continue;
                        string zoneId = zone.id;
                        dots.Register(RedDotPaths.ExpeditionZone(zoneId), () => IsZoneDone(zoneId));
                    }
                }
                else
                {
                    dots.Register(RedDotPaths.ExpeditionDone, () => IsOpen(FeatureId.Expedition) && _expedition.CompletedCount > 0);
                }
            }

            if (_dungeon != null)
            {
                dots.Register(RedDotPaths.DungeonTickets, () =>
                    IsOpen(FeatureId.Dungeon) && _dungeon.HighestFloor >= 1 && _dungeon.DailyTickets > 0 && _dungeon.TicketsLeft >= _dungeon.DailyTickets);
                // 고를 잔향 축복이 남음 (PRG-07, 확장 — 축복 표가 비면 BlessingPicksPending = 0). DungeonTickets의 자식 잎이라 OnDungeonChanged가 함께 다시 본다
                dots.Register(RedDotPaths.DungeonBlessing, () => IsOpen(FeatureId.Dungeon) && _dungeon.BlessingPicksPending > 0);
            }

            if (_formation != null && _characters != null) dots.Register(RedDotPaths.FormationEmpty, HasFormationVacancy);

            var lineages = _characters?.Db != null ? _characters.Db.lineages : null;
            if (lineages != null)
            {
                foreach (var lineage in lineages)
                {
                    if (lineage == null || string.IsNullOrEmpty(lineage.id)) continue;
                    string lineageId = lineage.id;
                    if (_evolution != null)
                        dots.Register(RedDotPaths.LineageEvolution(lineageId), () =>
                            IsOpen(FeatureId.Evolution) && _characters.IsOwned(lineageId) && _evolution.CountCraftable(lineageId) > 0);
                    dots.Register(RedDotPaths.LineageResonance(lineageId), () => _characters.CanResonance(lineageId));
                    if (_costumes != null)
                        dots.Register(RedDotPaths.LineageCostume(lineageId), () =>
                            IsOpen(FeatureId.Costumes) && _costumes.CountUnlockable(lineageId) > 0);
                }
            }

            if (_features != null)
            {
                foreach (FeatureId feature in Enum.GetValues(typeof(FeatureId)))
                {
                    string path = RedDotPaths.FeatureDot(feature);
                    if (path == null) continue;
                    if (IsHudToggle(feature))
                    {
                        dots.Unregister(path);
                        continue;
                    }
                    var captured = feature;
                    dots.Register(path, () => _features.IsNew(captured));
                }
            }

            // 4차 모듈 잎 (RedDotPaths.JournalRecords · Codex · GachaMilestone …)
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_moduleOff[i]) continue;
                try
                {
                    _modules[i].RegisterRedDots(dots);
                }
                catch (Exception e)
                {
                    DisableModule(i, nameof(IMetaModule.RegisterRedDots), e);
                }
            }
            dots.MarkDirty();
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>시스템 사이 연결 (한 번만)</summary>
        void Connect()
        {
            if (_connected) return;
            _connected = true;

            if (_clock != null)
            {
                _clock.DayChanged += OnDayChanged;
                _clock.TamperChanged += OnTamperChanged;
            }
            if (_progress != null)
            {
                _progress.Changed += OnProgressChanged;
                _progress.Incremented += OnCounterIncremented;
            }
            if (_features != null)
            {
                _features.Changed += OnFeaturesChanged;
                _features.Unlocked += OnFeatureUnlocked;
            }
            if (_missions != null) _missions.Changed += OnMissionsChanged;
            if (_attendance != null) _attendance.Changed += OnAttendanceChanged;
            if (_idleChest != null) _idleChest.Changed += OnIdleChestChanged;
            if (_guide != null) _guide.Changed += OnGuideChanged;
            if (_mail != null) _mail.Changed += OnMailChanged;
            if (_notice != null) _notice.Changed += OnNoticeChanged;
            if (_expedition != null) _expedition.Changed += OnExpeditionChanged;
            if (_dungeon != null) _dungeon.Changed += OnDungeonChanged;
            if (_evolution != null) _evolution.FormUnlocked += OnFormUnlocked;
            if (_characters != null) _characters.Changed += OnCharactersChanged;
            if (_costumes != null) _costumes.Changed += OnCharactersChanged;
            if (_formation != null) _formation.Changed += OnFormationChanged;
            if (_rewards != null) _rewards.Granted += OnMetaRewardsGranted;
            if (_battle != null) _battle.StageEnded += OnStageEnded;
            ConnectMeta4Hooks();
        }

        /// <summary>
        /// 4차 훅 연결 (SPEC4 §2.5 · §13.3 · §13.9): 친구 · 진화 · 의상 · 던전 · 원정(모두 다시 보내기)의 기능 열림 확인 = FeatureGateSystem.IsUnlocked (정의 없음 = 열림),
        /// 던전 입장권 모아 두기 상한 = MetaDatabase.dungeonTicketBankCap, 튜토리얼 전투 보호 = 스테이지 ≤ 3이고 튜토리얼 보호 단계(또는 첫 단계).
        /// 훅을 읽는 쪽(거절 · 모아 두기 · 보호 적용)은 각 시스템이 맡는다. 델리게이트는 여기서 한 번만 만든다
        /// </summary>
        void ConnectMeta4Hooks()
        {
            if (_features != null)
            {
                Func<FeatureId, bool> resolver = _features.IsUnlocked;
                if (_characters != null) _characters.FeatureResolver = resolver;
                if (_evolution != null) _evolution.FeatureResolver = resolver;
                if (_costumes != null) _costumes.FeatureResolver = resolver;
                if (_dungeon != null) _dungeon.FeatureResolver = resolver;
                // 모두 다시 보내기(ExpeditionBatch 2-1)가 잠기면 ResendAll · CountResendable = 0 (F-03, P4-2)
                if (_expedition != null) _expedition.FeatureResolver = resolver;
            }
            if (_dungeon != null) _dungeon.TicketBankCap = TicketBankCap;
            if (_battle != null) _battle.ProtectionResolver = ResolveProtection;
        }

        /// <summary>던전 입장권 모아 두기 상한 (콘텐츠, 0 = 끔)</summary>
        int TicketBankCap() => _db != null ? Math.Max(0, _db.dungeonTicketBankCap) : 0;

        /// <summary>메인 스테이지의 튜토리얼 전투 보호 (지금 FtueBattleGuard.IsProtecting과 같은 규칙 — 적 체력 60%, 수선점 최소 1). 할당 없음</summary>
        BattleProtection ResolveProtection(int stage) =>
            stage <= TutorialProtectionMaxStage && WantsTutorialProtection() ? BattleProtection.Tutorial : BattleProtection.Off;

        bool WantsTutorialProtection() =>
            _tutorial != null && (_tutorial.WantsBattleProtection || (_tutorial.IsActive && _tutorial.StepIndex == 0));

        // ── 4차 모듈 호출 (넣은 순서, 예외를 낸 모듈은 끈다 — 할당 없음) ──

        /// <summary>모듈 Connect (모듈당 한 번 — _moduleConnected). Start 뒤에 넣은 모듈은 AddModule이 그 자리에서 잇는다</summary>
        void ModulesConnect()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_moduleOff[i] || _moduleConnected[i]) continue;
                _moduleConnected[i] = true;
                try
                {
                    _modules[i].Connect();
                }
                catch (Exception e)
                {
                    DisableModule(i, nameof(IMetaModule.Connect), e);
                }
            }
        }

        void ModulesBeforeAwayApplied(long away, bool boot)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_moduleOff[i]) continue;
                try
                {
                    _modules[i].BeforeAwayApplied(away, boot);
                }
                catch (Exception e)
                {
                    DisableModule(i, nameof(IMetaModule.BeforeAwayApplied), e);
                }
            }
        }

        void ModulesAfterStartup(bool boot)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_moduleOff[i]) continue;
                try
                {
                    _modules[i].AfterStartup(boot);
                }
                catch (Exception e)
                {
                    DisableModule(i, nameof(IMetaModule.AfterStartup), e);
                }
            }
        }

        void ModulesDayChanged(string dayKey)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_moduleOff[i]) continue;
                try
                {
                    _modules[i].OnDayChanged(dayKey);
                }
                catch (Exception e)
                {
                    DisableModule(i, nameof(IMetaModule.OnDayChanged), e);
                }
            }
        }

        void ModulesTick(float dt)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_moduleOff[i]) continue;
                try
                {
                    _modules[i].Tick(dt);
                }
                catch (Exception e)
                {
                    DisableModule(i, nameof(IMetaModule.Tick), e);
                }
            }
        }

        /// <summary>예외를 낸 모듈을 끄고 한 번 기록한다 (나머지 메타는 그대로 돈다)</summary>
        void DisableModule(int index, string step, Exception e)
        {
            if (index < 0 || index >= _moduleOff.Count || _moduleOff[index]) return;
            _moduleOff[index] = true;
            var module = _modules[index];
            UnityEngine.Debug.LogException(new InvalidOperationException(
                $"[Meta] 4차 모듈 {module?.GetType().Name}이(가) {step}에서 예외를 내서 끕니다: {e.Message}", e));
        }

        /// <summary>
        /// 앱을 켜 둔 시간 (CounterKey.PlayMinutes): 일시정지 중 · 복귀 첫 프레임은 세지 않고, 한 프레임은 MaxPlayDeltaSeconds까지.
        /// 1분이 찰 때마다 Increment 한 번 (Progress.Changed가 1분에 한 번 늘 뿐). 같은 시간을 개방 카드 간격에 넣는다. 할당 없음
        /// </summary>
        void TickPlayTime(float dt)
        {
            if (_playPaused) return;
            if (_skipPlayDelta)
            {
                _skipPlayDelta = false;
                return;
            }
            float step = dt > MaxPlayDeltaSeconds ? MaxPlayDeltaSeconds : dt;
            if (!(step > 0f)) return;
            _features?.TickCards(step);
            _playSeconds += step;
            if (_playSeconds < 60f) return;
            int minutes = (int)(_playSeconds / 60f);
            _playSeconds -= minutes * 60f;
            _progress?.Increment(CounterKey.PlayMinutes, minutes);
        }

        /// <summary>
        /// 4차 마이그레이션 PlayMinutes 채우기 둘째 단계 (CR4-24, MetaSave.ApplyPendingPlayMinutesSeed): 시계를 새로 읽은 뒤
        /// 오늘까지 센 설치 일수(DailyClock.InstallDay)로 올린다. 기능 개방 평가 전이라 이벤트 없이 저장값만 바꾼다.
        /// 시계가 없거나 되돌림 중이면(날이 마지막 날에 머문다) 다음 Start로 미룬다
        /// </summary>
        void ApplyMigrationPlayMinutesSeed()
        {
            if (_clock == null || _clock.IsTampered || !Meta.playMinutesSeedPending) return;
            Meta.ApplyPendingPlayMinutesSeed(_clock.InstallDay);
        }

        /// <summary>오늘 첫 접속이면 Login 카운터 (날 키 기준 하루 1번)</summary>
        void CountLogin()
        {
            if (_progress != null && _progress.GetDaily(CounterKey.Login) == 0) _progress.Increment(CounterKey.Login);
        }

        /// <summary>본 적 있는 가장 늦은 기기 시각 (저장값). 부팅·복귀 때 DailyClock.Refresh보다 먼저 읽어 비운 시간의 바닥으로 쓴다</summary>
        long MaxSeenUnix => Meta.clock != null ? Meta.clock.maxSeenUnix : 0;

        /// <summary>
        /// 비운 시간 = 지금 − max(마지막 세션 끝, 마지막 저장, Refresh 전 maxSeen). 되돌림이거나 기록이 없으면 0.
        /// maxSeen은 실행 중 1초마다 올라가고 되돌림 중에는 내려가지 않으므로, 시계를 되돌린 채 나갔다가
        /// 원래대로 돌리고 들어와도 되돌린 만큼이 비운 시간으로 잡히지 않는다.
        /// </summary>
        long ComputeAwaySeconds(long seenFloor)
        {
            if (_clock == null || _clock.IsTampered) return 0;
            long last = Math.Max(Math.Max(Meta.clock?.lastSessionEndUnix ?? 0, _data.lastSaveUnix), seenFloor);
            if (last <= 0) return 0;
            return Math.Max(0, _clock.NowUnix - last);
        }

        /// <summary>
        /// 이번 공백을 썼다고 표시한다 (lastSessionEndUnix = 유효 시각). 복귀가 일시정지 없이 두 번 오거나
        /// 다음 저장 전에 앱이 꺼져도 같은 공백이 두 번 바구니에 들어가지 않는다.
        /// </summary>
        void ConsumeAwayGap()
        {
            if (_clock == null) return;
            (Meta.clock ??= new MetaClockSave()).lastSessionEndUnix = _clock.EffectiveUnix;
        }

        int ReturnPopupMinSeconds => _db != null && _db.idleChest != null ? Math.Max(0, _db.idleChest.returnPopupMinSeconds) : 600;

        bool WantsReturnPopup(long away) => away > 0 && away >= ReturnPopupMinSeconds && _idleChest != null && _idleChest.CanClaim;

        /// <summary>
        /// 복귀 때 출석부를 다시 띄울지: 오늘 찍을 수 있고, 오래 비웠거나(≥ returnPopupMinSeconds — 새 접속으로 본다) 비운 사이 날이 바뀜.
        /// 잠금 화면·앱 전환·로그인/권한 창처럼 잠깐 나갔다 오면 띄우지 않고 빨간 점으로만 알린다 (닫은 출석부가 매번 다시 열리지 않게).
        /// </summary>
        bool WantsAttendanceOnResume(long away, bool dayChanged) =>
            _attendance != null && _attendance.CanCheckIn && (dayChanged || away >= Math.Max(1, ReturnPopupMinSeconds));

        /// <summary>접속 요청: 복귀 화면 → 출석부 (큐가 순서를 맞춘다)</summary>
        void RequestStartupPopups(long away, bool attendance)
        {
            if (_popups == null) return;
            if (WantsReturnPopup(away))
            {
                _popups.Enqueue(MetaPopupRequest.Return(away));
                (Meta.returnScreen ??= new ReturnSave()).lastShownUnix = _clock != null ? _clock.NowUnix : 0;
            }
            if (attendance) _popups.Enqueue(MetaPopupRequest.Attendance());
        }

        /// <summary>새 게임이 튜토리얼 0단계에서 시작하는지 (자동 전투를 끈다)</summary>
        bool IsFreshTutorial()
        {
            if (_battle == null || _db == null || _db.ftueSteps == null || _db.ftueSteps.Count == 0) return false;
            var ftue = Meta.ftue;
            return ftue != null && !ftue.done && ftue.step == 0;
        }

        /// <summary>튜토리얼이 진행 중인지 (단계가 있고 아직 졸업·건너뛰기 전)</summary>
        bool IsTutorialRunning()
        {
            if (_tutorial != null) return _tutorial.IsActive;
            var ftue = Meta.ftue;
            return _db != null && _db.ftueSteps != null && _db.ftueSteps.Count > 0 && ftue != null && !ftue.done;
        }

        /// <summary>
        /// 세션 끝 = 유효 시각 (max(기기 시각, maxSeen)). 되돌린 시계로 나가도 끝 시각이 과거로 찍히지 않아,
        /// 시계를 원래대로 돌리고 들어왔을 때 되돌린 만큼이 비운 시간으로 잡히지 않는다.
        /// </summary>
        void MarkSessionEnd()
        {
            var clock = Meta.clock ??= new MetaClockSave();
            if (_clock != null) clock.lastSessionEndUnix = _clock.EffectiveUnix;
        }

        bool IsOpen(FeatureId feature) => _features == null || _features.IsUnlocked(feature);

        /// <summary>
        /// 전투 HUD 토글 (자동 전투 / 2배속 / 자동 불 밝히기 방식 / 전투 기록): "새로 열림" 점을 달지 않는다 (M09).
        /// 개방 카드와 한 번의 반짝임(MetaPulse)이면 충분하고, 잠금 덮개의 새 표시는 첫 탭에 꺼진다 (FeatureLockCover).
        /// 전투 기록(BattleStats)은 점 경로 부모도 없다 (RedDotPaths.ParentOf = null).
        /// </summary>
        public static bool IsHudToggle(FeatureId feature) =>
            feature == FeatureId.AutoBattle || feature == FeatureId.Speed2x || feature == FeatureId.LampPolicy || feature == FeatureId.BattleStats;

        /// <summary>
        /// 편성 빈칸 점 (M16): 편성이 열렸고, 열린 지킴이·잔향이 칸 중 빈칸이 있고, 편성에 없는 보유 친구가 있음.
        /// 원정 중인 친구도 센다 — 원정 중에도 편성해 함께 싸울 수 있다 (M05, FormationSystem.CanPlace·AutoFormation과 같은 기준).
        /// 같은 계보는 지킴이 1 + 잔향이 1까지라 "편성에 없는 계보"는 어느 빈칸에든 설 수 있다. 할당 없음.
        /// </summary>
        bool HasFormationVacancy()
        {
            if (!IsOpen(FeatureId.Formation) || !HasEmptyFormationSlot()) return false;
            var lineages = _characters.Db != null ? _characters.Db.lineages : null;
            if (lineages == null) return false;
            for (int i = 0; i < lineages.Count; i++)
            {
                var lineage = lineages[i];
                if (lineage == null || string.IsNullOrEmpty(lineage.id) || !_characters.IsOwned(lineage.id)) continue;
                if (_formation.IsLineagePlaced(lineage.id)) continue;
                return true;
            }
            return false;
        }

        bool HasEmptyFormationSlot()
        {
            for (int i = 0; i < FormationSystem.GuardianSlotCount; i++)
            {
                if (_formation.GetGuardian(i) == null) return true;
            }
            int echoSlots = _formation.UnlockedEchoSlots;
            for (int i = 0; i < echoSlots; i++)
            {
                if (_formation.GetEcho(i) == null) return true;
            }
            return false;
        }

        bool IsZoneDone(string zoneId)
        {
            if (!IsOpen(FeatureId.Expedition)) return false;
            var expedition = _expedition.GetActive(zoneId);
            return expedition != null && _expedition.IsComplete(expedition);
        }

        /// <summary>안 읽은 공지가 있는지 (할당 없이 훑기)</summary>
        bool HasUnreadNotice()
        {
            var notices = _notice.Notices;
            if (notices == null) return false;
            for (int i = 0; i < notices.Count; i++)
            {
                if (notices[i] != null && !_notice.IsRead(notices[i])) return true;
            }
            return false;
        }

        /// <summary>시간이 지나면 바뀌는 잎 (1초마다)</summary>
        void MarkTimedDots()
        {
            var dots = _redDots;
            if (dots == null) return;
            dots.MarkDirty(RedDotPaths.HudIdleChest);
            dots.MarkDirty(RedDotPaths.ExpeditionDone);
            // 던전 입장권은 DailyClock 날 키(현지 05:00)가 바뀔 때 다시 채워진다 (DungeonSystem 규칙) — 날 키가 바뀐 때만 다시 본다.
            // 날 키 문자열은 같은 날이면 같은 인스턴스라 비교에 할당이 없다
            string today = _clock?.DayKey;
            if (today != _lastDayKey)
            {
                _lastDayKey = today;
                dots.MarkDirty(RedDotPaths.DungeonTickets);
            }
        }

        // ── 연결 처리 ──

        void OnDayChanged(string dayKey)
        {
            if (!IsStarted) return;
            CountLogin();
            // 접속 중에 날이 바뀌면 출석부를 다음 스테이지 사이에 띄운다 (묶음 밖이라 TryDequeue가 스테이지 사이를 기다린다)
            if (_attendance != null && _attendance.CanCheckIn) _popups?.Enqueue(MetaPopupRequest.Attendance());
            ModulesDayChanged(dayKey);
            _redDots?.MarkDirty();
        }

        /// <summary>되돌림 중에는 받을 것 점(일지·출석·바구니)이 꺼지고, 풀리면 다시 켜진다</summary>
        void OnTamperChanged(bool tampered) => _redDots?.MarkDirty();

        void OnProgressChanged()
        {
            _tutorial?.Refresh(_battle != null && _battle.IsBetweenStages);
            var dots = _redDots;
            if (dots == null) return;
            dots.MarkDirty(RedDotPaths.TopJournal);
            dots.MarkDirty(RedDotPaths.HudGuideQuest);
            dots.MarkDirty(RedDotPaths.HudIdleChest);
            dots.MarkDirty(RedDotPaths.TopAttendance);
        }

        /// <summary>재료·스테이지가 바뀌는 카운터만 친구 가지를 다음 프레임에 다시 보게 표시 (전투 처치마다 오므로 가볍게)</summary>
        void OnCounterIncremented(CounterKey key, long amount)
        {
            switch (key)
            {
                case CounterKey.StageClear:
                case CounterKey.StageFail:
                case CounterKey.ShopBuy:
                case CounterKey.MailClaim:
                    _characterDotsDirty = true;
                    break;
            }
        }

        void OnFeaturesChanged()
        {
            _redDots?.MarkDirty();
            _tutorial?.Refresh(_battle != null && _battle.IsBetweenStages);
        }

        /// <summary>
        /// 자동 전투가 열렸는데 튜토리얼이 이미 끝났으면(건너뜀 포함) 자동 전투를 켠다 — 새 게임에서 끈 것(G13)을 되돌린다.
        /// 튜토리얼 중이면 8단계(FtueAction.EnableAutoBattle)가 켜고, 건너뛸 때 이미 열려 있었으면 FtueDirector가 켠다.
        /// 옛 세이브의 조용한 개방은 Unlocked를 울리지 않으므로 저장된 설정을 건드리지 않는다.
        /// </summary>
        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature != FeatureId.AutoBattle || _battle == null || IsTutorialRunning()) return;
            _battle.AutoMode = true;
        }

        void OnMissionsChanged() => _redDots?.MarkDirty(RedDotPaths.TopJournal);

        void OnAttendanceChanged() => _redDots?.MarkDirty(RedDotPaths.TopAttendance);

        void OnIdleChestChanged() => _redDots?.MarkDirty(RedDotPaths.HudIdleChest);

        void OnGuideChanged() => _redDots?.MarkDirty(RedDotPaths.HudGuideQuest);

        void OnMailChanged() => _redDots?.MarkDirty(RedDotPaths.TopMail);

        void OnNoticeChanged() => _redDots?.MarkDirty(RedDotPaths.TopSettingsNotice);

        /// <summary>원정은 편성 빈칸 점과 무관하다 (원정 중에도 편성할 수 있다 — M05). 편성 빈칸은 Formation/Characters.Changed에서만 본다</summary>
        void OnExpeditionChanged()
        {
            _redDots?.MarkDirty(RedDotPaths.ExpeditionDone);
            _characterDotsDirty = true;
        }

        void OnDungeonChanged()
        {
            _redDots?.MarkDirty(RedDotPaths.DungeonTickets); // 자식 잎 DungeonBlessing(축복 고르기)도 함께 다시 본다
            _characterDotsDirty = true;
        }

        void OnFormUnlocked(CharacterFormDef form) => _characterDotsDirty = true;

        void OnCharactersChanged()
        {
            _characterDotsDirty = true;
            _redDots?.MarkDirty(RedDotPaths.FormationEmpty); // 새 친구
        }

        void OnFormationChanged() => _redDots?.MarkDirty(RedDotPaths.FormationEmpty);

        void OnMetaRewardsGranted(System.Collections.Generic.IReadOnlyList<MetaReward> rewards) => _characterDotsDirty = true;

        /// <summary>스테이지가 끝나면 덱 6번째 칸 개방(2-10 도달) 등으로 전투력이 바뀔 수 있다</summary>
        void OnStageEnded(BattleOutcome outcome, System.Collections.Generic.List<RewardData> rewards) => _power?.MarkDirty();
    }
}
