namespace IdleGame
{
    /// <summary>
    /// 게임 전역 서비스 접근점. GameBootstrap이 Setup으로 초기화한다.
    /// 각 시스템은 순수 C# 클래스라서 테스트에서는 직접 생성해서 쓴다.
    /// </summary>
    public static class Game
    {
        public static bool IsReady { get; private set; }

        public static PlayerData Data { get; private set; }
        public static GameContent Content { get; private set; }
        public static GameConfig Config { get; private set; }
        public static ItemDatabase Items { get; private set; }
        public static ITimeProvider Clock { get; private set; }

        public static CurrencySystem Currency { get; private set; }
        public static InventorySystem Inventory { get; private set; }
        public static RewardService Rewards { get; private set; }
        public static MailSystem Mail { get; private set; }
        /// <summary>수선점 강화 (수선점 레벨 → 초당 단추, 수선점 내구도)</summary>
        public static GrowthSystem Growth { get; private set; }
        public static CharacterSystem Characters { get; private set; }
        public static CostumeSystem Costumes { get; private set; }
        public static EvolutionSystem Evolution { get; private set; }
        public static GachaSystem Gacha { get; private set; }
        public static ShopSystem Shop { get; private set; }
        public static FormationSystem Formation { get; private set; }
        /// <summary>특성/성향/조합 시너지</summary>
        public static SynergySystem Synergy { get; private set; }
        public static ExpeditionSystem Expedition { get; private set; }
        public static BattleSystem Battle { get; private set; }
        public static DungeonSystem Dungeon { get; private set; }
        public static NoticeSystem Notice { get; private set; }
        public static OfflineRewardSystem OfflineReward { get; private set; }
        public static AccountSystem Account { get; private set; }

        // ───────────── 메타 (SPEC3) ─────────────

        /// <summary>
        /// 메타 기능이 켜져 있는지. Setup에서 켜지고, 메타 시스템이 아직 구현 중이라 NotImplementedException을 내면
        /// GameBootstrap이 끈다 (예전 오프라인 보상 흐름으로 돌아감). 메타 UI는 이 값이 false면 아무것도 하지 않는다.
        /// </summary>
        public static bool IsMetaActive { get; internal set; }
        /// <summary>메타 데이터 (콘텐츠에 없으면 빈 데이터)</summary>
        public static MetaDatabase MetaDb { get; private set; }
        /// <summary>
        /// 일일 시계: 05:00 초기화, 시간 되돌림 방지, 날·주 키. 모든 "오늘"의 기준 (M77 — 상점 일일 한도·던전 입장권도 이 날 키를 쓴다).
        /// 상점·던전보다 먼저 Setup에서 만들고, 메타가 꺼져 있어도 날 키는 읽을 수 있다 (Tick은 MetaSession이 돌린다)
        /// </summary>
        public static DailyClock DailyClock { get; private set; }
        /// <summary>전투력 (팀/유닛/권장)</summary>
        public static PowerSystem Power { get; private set; }
        /// <summary>진행 기록기: 평생/오늘/이번 주 카운터 + 메타 조건 판정</summary>
        public static ProgressTracker Progress { get; private set; }
        /// <summary>메타 보상 지급 (단추 N분치, 수선권, 선택 기억편, 친구 확정)</summary>
        public static MetaRewardService MetaRewards { get; private set; }
        /// <summary>메타 팝업 순서 (복귀 → 출석 → 공지 → 이벤트, 세션 중 개방 카드)</summary>
        public static PopupQueue Popups { get; private set; }
        /// <summary>빨간 점 트리</summary>
        public static RedDotSystem RedDots { get; private set; }
        /// <summary>기능 개방</summary>
        public static FeatureGateSystem Features { get; private set; }
        /// <summary>가이드 퀘스트 (전투 화면 한 줄)</summary>
        public static GuideQuestSystem GuideQuest { get; private set; }
        /// <summary>수선 일지 (일일·주간 미션)</summary>
        public static MissionSystem Missions { get; private set; }
        /// <summary>출석부 (새싹 7일 + 정규 28일)</summary>
        public static AttendanceSystem Attendance { get; private set; }
        /// <summary>수선 바구니 + 빠른 수선 + 두 배 받기</summary>
        public static IdleChestSystem IdleChest { get; private set; }
        /// <summary>첫 10분 튜토리얼</summary>
        public static TutorialSystem Tutorial { get; private set; }
        /// <summary>메타 세션 흐름 (GameBootstrap이 Start/Tick/OnPause/OnResume/OnQuit를 부름)</summary>
        public static MetaSession MetaSession { get; private set; }

        // ───────────── 메타 4차 (SPEC4 §2.4 — P4-0은 계약 스텁, 각 패키지가 채운다) ─────────────

        /// <summary>도감 (Q-05, P4-1)</summary>
        public static CodexSystem Codex { get; private set; }
        /// <summary>수선점 기록 (Q-04, P4-1)</summary>
        public static AchievementSystem Achievements { get; private set; }
        /// <summary>소환 마일스톤 + 친구 선택권 (PRG-04, P4-1)</summary>
        public static GachaMilestoneSystem GachaMilestones { get; private set; }
        /// <summary>호감도 단계 · 쓰다듬기 (CR-10, P4-1)</summary>
        public static AffinitySystem Affinity { get; private set; }
        /// <summary>챕터 상자 · 잃어버린 상자 (PRG-06, P4-1)</summary>
        public static ChapterChestSystem ChapterChests { get; private set; }
        /// <summary>동화책 (CR-05, P4-1)</summary>
        public static StorySystem Story { get; private set; }
        /// <summary>이벤트 달력 (EVT-01, P4-2)</summary>
        public static EventCalendarSystem Events { get; private set; }
        /// <summary>별빛 산책 수첩 (Q-06, P4-2)</summary>
        public static SeasonPassSystem Season { get; private set; }
        /// <summary>다시 만난 날 (CR-06, P4-2)</summary>
        public static ComebackSystem Comeback { get; private set; }
        /// <summary>쿠폰 (EVT-02, P4-2 — 모듈 아님)</summary>
        public static CouponSystem Coupons { get; private set; }
        /// <summary>첫 주 (NEW-01, P4-2)</summary>
        public static StarterWeekSystem StarterWeek { get; private set; }
        /// <summary>오늘의 작은 선물 (NEW-02, P4-2)</summary>
        public static DailyGiftSystem DailyGift { get; private set; }
        /// <summary>친구들의 응원 (PRG-03, P4-1)</summary>
        public static CheerSystem Cheer { get; private set; }
        /// <summary>길 잃은 반딧불 규칙 (NEW-03, P4-2 — 모듈 아님)</summary>
        public static FireflyRules Fireflies { get; private set; }
        /// <summary>강해지기 줄 (PRG-03, P4-1 — 모듈 아님, 순수 조회)</summary>
        public static StrengthenAdvisor Strengthen { get; private set; }
        /// <summary>백업 알림 우편 (SAVE-01 · 델타 17, P4-2 — RM-2 SB-1 계약)</summary>
        public static BackupReminderSystem BackupReminder { get; private set; }

        static RewardMailSystem _rewardMail;

        public static void Setup(PlayerData data, GameContent content, ITimeProvider clock, System.Random random)
        {
            var config = content.Config;
            Data = data;
            Content = content;
            Config = config;
            Items = content.Items;
            Clock = clock;

            Currency = new CurrencySystem(data);
            Inventory = new InventorySystem(data, content.Items);
            Rewards = new RewardService(Currency, Inventory, content.Items);
            Mail = new MailSystem(data, Rewards, clock, config.mailExpireDays);
            Growth = new GrowthSystem(data, config, Currency, Inventory);
            Characters = new CharacterSystem(data, config, content.Characters, Currency, clock);
            Costumes = new CostumeSystem(data, content.Characters, Characters, clock);
            Evolution = new EvolutionSystem(data, config, Characters, Inventory, content.Items);
            Gacha = new GachaSystem(data, Currency, Characters, content.Characters, content.GachaTables, random);
            // 일일 시계는 상점·던전의 "오늘"(05:00 초기화)이라 둘보다 먼저 만든다 (SetupMeta도 이 인스턴스를 쓴다)
            DailyClock = new DailyClock(data, clock);
            Shop = new ShopSystem(data, content.Shops, Currency, Rewards, Costumes, clock, DailyClock);
            Formation = new FormationSystem(data, Characters);
            Synergy = new SynergySystem(data, content.Synergies, Characters, Formation);
            Expedition = new ExpeditionSystem(data, content.Expeditions, Characters, Formation, Rewards, clock, random);
            Battle = new BattleSystem(data, content.Battle, Characters, Formation, Synergy, Currency, Rewards, random);
            Dungeon = new DungeonSystem(data, content.Battle, Characters, Formation, Synergy, Rewards, clock, DailyClock);
            Notice = new NoticeSystem(data, clock);
            OfflineReward = new OfflineRewardSystem(config, Growth, Currency, clock);
            // 서버/SDK가 정해지면 LocalAccountService와 기본 공급자를 실제 구현으로 교체한다
            Account = new AccountSystem(data, clock, new LocalAccountService(), AccountSystem.CreateDefaultProviders(config.useMockAuth));
            _rewardMail = new RewardMailSystem(Growth, Account, Mail, config);
            SetupMeta(data, content, config, clock);

            IsReady = true;
        }

        /// <summary>메타 시스템 생성. 생성자는 의존성만 저장하고, 이벤트 연결·부팅 처리는 GameBootstrap → MetaSession.Start가 한다</summary>
        static void SetupMeta(PlayerData data, GameContent content, GameConfig config, ITimeProvider clock)
        {
            var db = content.Meta != null ? content.Meta : MetaDatabase.CreateEmpty();
            MetaDb = db;
            DailyClock ??= new DailyClock(data, clock);
            Power = new PowerSystem(data, db, Characters, Formation, Synergy, Growth, Costumes);
            Progress = new ProgressTracker(data, DailyClock, Battle, Growth, Characters, Evolution, Costumes, Gacha, Shop, Expedition, Dungeon,
                Formation, Synergy, Mail, Power);
            MetaRewards = new MetaRewardService(data, db, Rewards, content.Items, Characters, Costumes, Growth);
            Popups = new PopupQueue();
            RedDots = new RedDotSystem();
            Features = new FeatureGateSystem(data, db, Progress, Popups, MetaRewards);
            // 자동 보스 도전 개방을 기능 개방과 같은 규칙(정의 없음 = 열림)으로 본다 (M13). Shutdown이 Battle을 비우므로 따로 풀지 않는다
            Battle.FeatureResolver = Features.IsUnlocked;
            GuideQuest = new GuideQuestSystem(data, db, Progress, MetaRewards, Features, Growth, Characters);
            Missions = new MissionSystem(data, db, DailyClock, Progress, MetaRewards, Features);
            Attendance = new AttendanceSystem(data, db, DailyClock, Progress, MetaRewards, Features);
            IdleChest = new IdleChestSystem(data, db, config, DailyClock, Growth, Currency, MetaRewards, Progress, Features);
            Tutorial = new TutorialSystem(data, db, Progress, Features, MetaRewards, Popups, GuideQuest);
            MetaSession = new MetaSession(data, db, DailyClock, Progress, Power, RedDots, Popups, Features, GuideQuest, Missions,
                Attendance, IdleChest, Tutorial, MetaRewards, Battle, Notice, Mail, Expedition, Dungeon, Evolution, Characters, Costumes,
                Formation);
            SetupMeta4(data, db, content);
            IsMetaActive = true;
        }

        /// <summary>
        /// 4차 메타 (SPEC4 §2.4 표 순서): 기존 시스템 뒤에 만들고, 모듈은 만든 순서대로 MetaSession에 넣는다.
        /// 쿠폰 · 반딧불 규칙 · 강해지기는 모듈이 아니다. 생성자는 의존성만 받고 연결은 MetaSession.Start → 모듈 Connect가 한다
        /// </summary>
        static void SetupMeta4(PlayerData data, MetaDatabase db, GameContent content)
        {
            Codex = new CodexSystem(data, db, Progress, MetaRewards, Features, Characters, Costumes, Evolution, Synergy, Battle, Inventory);
            Achievements = new AchievementSystem(data, db, Progress, MetaRewards, Features, Codex, Battle, DailyClock);
            GachaMilestones = new GachaMilestoneSystem(data, db, Gacha, MetaRewards, Characters);
            Affinity = new AffinitySystem(data, db, Characters, MetaRewards, DailyClock, Progress);
            ChapterChests = new ChapterChestSystem(data, db, Battle, MetaRewards, Popups, content.Items, Tutorial);
            Story = new StorySystem(data, db, Battle, MetaRewards, Popups, Tutorial);
            Events = new EventCalendarSystem(data, db, DailyClock, Progress, MetaRewards, Features, Popups, Battle, Expedition, Dungeon, IdleChest);
            Season = new SeasonPassSystem(data, db, DailyClock, Progress, MetaRewards, Features);
            Comeback = new ComebackSystem(data, db, DailyClock, MetaRewards, IdleChest, Popups, Tutorial);
            Coupons = new CouponSystem(data, db, DailyClock, Mail);
            StarterWeek = new StarterWeekSystem(data, db, DailyClock, Progress, MetaRewards, Features);
            DailyGift = new DailyGiftSystem(data, db, DailyClock, MetaRewards, Features);
            Cheer = new CheerSystem(data, db, DailyClock, Battle, Tutorial);
            Fireflies = new FireflyRules(data, db, DailyClock, MetaRewards, Battle, Tutorial);
            Strengthen = new StrengthenAdvisor(Power, Growth, Characters, Formation, Synergy, Evolution, Battle, Inventory, Cheer);
            // 백업 알림은 SPEC4 §2.4 표 뒤에 붙는다 (RM-2 SB-1): 모듈은 응원 다음
            BackupReminder = new BackupReminderSystem(data, db, DailyClock, Mail);

            var session = MetaSession;
            session.AddModule(Codex);
            session.AddModule(Achievements);
            session.AddModule(GachaMilestones);
            session.AddModule(Affinity);
            session.AddModule(ChapterChests);
            session.AddModule(Story);
            session.AddModule(Events);
            session.AddModule(Season);
            session.AddModule(Comeback);
            session.AddModule(StarterWeek);
            session.AddModule(DailyGift);
            session.AddModule(Cheer);
            session.AddModule(BackupReminder);
        }

        public static void Shutdown()
        {
            IsReady = false;
            Data = null;
            Content = null;
            Config = null;
            Items = null;
            Clock = null;
            Currency = null;
            Inventory = null;
            Rewards = null;
            Mail = null;
            Growth = null;
            Characters = null;
            Costumes = null;
            Evolution = null;
            Gacha = null;
            Shop = null;
            Formation = null;
            Synergy = null;
            Expedition = null;
            Battle = null;
            Dungeon = null;
            Notice = null;
            OfflineReward = null;
            Account = null;
            _rewardMail = null;
            IsMetaActive = false;
            MetaDb = null;
            DailyClock = null;
            Power = null;
            Progress = null;
            MetaRewards = null;
            Popups = null;
            RedDots = null;
            Features = null;
            GuideQuest = null;
            Missions = null;
            Attendance = null;
            IdleChest = null;
            Tutorial = null;
            MetaSession = null;
            Codex = null;
            Achievements = null;
            GachaMilestones = null;
            Affinity = null;
            ChapterChests = null;
            Story = null;
            Events = null;
            Season = null;
            Comeback = null;
            Coupons = null;
            StarterWeek = null;
            DailyGift = null;
            Cheer = null;
            Fireflies = null;
            Strengthen = null;
            BackupReminder = null;
        }
    }
}
