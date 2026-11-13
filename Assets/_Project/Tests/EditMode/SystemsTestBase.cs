using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    public sealed class FakeClock : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan span) => UtcNow += span;
    }

    /// <summary>테스트용 콘텐츠 픽스처</summary>
    public static class TestContent
    {
        /// <summary>
        /// sock(영웅): sock_base → sock_a(재료 m_cloth x3, 원정 2회) → sock_a2(계보 레벨 10)
        ///                      → sock_b(재료 m_thread x1, 잔향이 출격 3회)
        ///   의상: sock_night(야간 방문), sock_event(이벤트 상점 50), sock_stage(스테이지 5)
        /// candle(희귀): candle_base → candle_a(재료 m_cloth x1)
        /// button(일반): button_base
        /// </summary>
        public static CharacterDatabase CreateCharacters()
        {
            var db = ScriptableObject.CreateInstance<CharacterDatabase>();
            db.lineages = new List<LineageDef>
            {
                new LineageDef
                {
                    id = "sock", name = "양말", grade = ItemGrade.Epic, baseFormId = "sock_base",
                    forms = new List<CharacterFormDef>
                    {
                        Form("sock_base", null, 0, CombatStyle.Defense),
                        Form("sock_a", "sock_base", 1, CombatStyle.Assault,
                            new[] { Cost("m_cloth", 3) }, new[] { Cond(ProgressType.ExpeditionCount, 2) }),
                        Form("sock_b", "sock_base", 1, CombatStyle.Capture,
                            new[] { Cost("m_thread", 1) }, new[] { Cond(ProgressType.EchoDeployCount, 3) }),
                        Form("sock_a2", "sock_a", 2, CombatStyle.Snipe,
                            Array.Empty<MaterialCost>(), new[] { Cond(ProgressType.LineageLevel, 10) }),
                    },
                    costumes = new List<CostumeDef>
                    {
                        new CostumeDef { id = "sock_night", name = "밤지기 양말", unlockType = CostumeUnlockType.NightVisit, attackBonusPercent = 5 },
                        new CostumeDef { id = "sock_event", name = "축제 양말", unlockType = CostumeUnlockType.EventShop, price = 50, attackBonusPercent = 5 },
                        new CostumeDef { id = "sock_stage", name = "여행자 양말", unlockType = CostumeUnlockType.StageReached, unlockTarget = 5, attackBonusPercent = 5 },
                    },
                },
                new LineageDef
                {
                    id = "candle", name = "촛불", grade = ItemGrade.Rare, baseFormId = "candle_base",
                    forms = new List<CharacterFormDef>
                    {
                        Form("candle_base", null, 0, CombatStyle.Snipe),
                        Form("candle_a", "candle_base", 1, CombatStyle.Support, new[] { Cost("m_cloth", 1) }, Array.Empty<EvolutionCondition>()),
                    },
                },
                new LineageDef
                {
                    id = "button", name = "단추", grade = ItemGrade.Common, baseFormId = "button_base",
                    forms = new List<CharacterFormDef> { Form("button_base", null, 0, CombatStyle.Assault) },
                },
            };
            return db;
        }

        public static CharacterFormDef Form(string id, string parent, int tier, CombatStyle style,
            MaterialCost[] materials = null, EvolutionCondition[] conditions = null) => new CharacterFormDef
        {
            id = id,
            name = id,
            parentFormId = parent,
            tier = tier,
            style = style,
            recommendedRole = style == CombatStyle.Defense || style == CombatStyle.Snipe ? CharacterRole.Guardian : CharacterRole.Echo,
            clueLevel = tier == 0 ? 0 : tier * 5,
            materials = new List<MaterialCost>(materials ?? Array.Empty<MaterialCost>()),
            conditions = new List<EvolutionCondition>(conditions ?? Array.Empty<EvolutionCondition>()),
        };

        public static MaterialCost Cost(string itemId, int count) => new MaterialCost { itemId = itemId, count = count };

        public static EvolutionCondition Cond(ProgressType type, int target) => new EvolutionCondition { type = type, target = target };
    }

    /// <summary>Game.Setup과 같은 순서로 시스템을 직접 만들어 쓰는 테스트 베이스</summary>
    public abstract class SystemsTestBase
    {
        protected FakeClock Clock;
        protected GameConfig Config;
        protected ItemDatabase Items;
        protected CharacterDatabase CharDb;
        protected GachaTable Table;
        protected BattleConfig BattleCfg;
        protected ExpeditionDatabase ExpeditionDb;
        protected ShopDatabase ShopDb;
        protected SynergyDatabase SynergyDb;
        /// <summary>메타 데이터 (기본은 비어 있음 — 메타 테스트는 내용을 채운 뒤 CreateMetaSystems를 다시 부른다)</summary>
        protected MetaDatabase MetaDb;
        protected PlayerData Data;

        protected CurrencySystem Currency;
        protected InventorySystem Inventory;
        protected RewardService Rewards;
        protected MailSystem Mail;
        protected GrowthSystem Growth;
        protected CharacterSystem Characters;
        protected CostumeSystem Costumes;
        protected EvolutionSystem Evolution;
        protected GachaSystem Gacha;
        protected ShopSystem Shop;
        protected FormationSystem Formation;
        protected SynergySystem Synergy;
        protected ExpeditionSystem Expedition;
        protected BattleSystem Battle;
        protected DungeonSystem Dungeon;
        protected NoticeSystem Notice;
        protected OfflineRewardSystem Offline;
        protected AccountSystem Account;

        // ── 메타 (Game.SetupMeta와 같은 순서) ──
        protected DailyClock DailyClock;
        protected PowerSystem Power;
        protected ProgressTracker Progress;
        protected MetaRewardService MetaRewards;
        protected PopupQueue Popups;
        protected RedDotSystem RedDots;
        protected FeatureGateSystem Features;
        protected GuideQuestSystem GuideQuest;
        protected MissionSystem Missions;
        protected AttendanceSystem Attendance;
        protected IdleChestSystem IdleChest;
        protected TutorialSystem Tutorial;
        protected MetaSession MetaSession;

        // ── 메타 4차 (Game.SetupMeta4와 같은 순서 · 같은 모듈 넣기, SPEC4 §2.4) ──
        protected CodexSystem Codex;
        protected AchievementSystem Achievements;
        protected GachaMilestoneSystem GachaMilestones;
        protected AffinitySystem Affinity;
        protected ChapterChestSystem ChapterChests;
        protected StorySystem Story;
        protected EventCalendarSystem Events;
        protected SeasonPassSystem Season;
        protected ComebackSystem Comeback;
        protected CouponSystem Coupons;
        protected StarterWeekSystem StarterWeek;
        protected DailyGiftSystem DailyGift;
        protected CheerSystem Cheer;
        protected FireflyRules Fireflies;
        protected StrengthenAdvisor Strengthen;
        protected BackupReminderSystem BackupReminder;

        [SetUp]
        public void SetUpSystems()
        {
            Clock = new FakeClock();
            Config = ScriptableObject.CreateInstance<GameConfig>();
            Config.startGold = 0;
            Config.startGem = 0;

            Items = ScriptableObject.CreateInstance<ItemDatabase>();
            Items.items = new List<ItemDef>
            {
                new ItemDef { id = "c1", name = "일반1", grade = ItemGrade.Common, category = ItemCategory.Equipment, goldBonusPercent = 1 },
                new ItemDef { id = "c2", name = "일반2", grade = ItemGrade.Common, category = ItemCategory.Equipment, goldBonusPercent = 1 },
                new ItemDef { id = "r1", name = "희귀1", grade = ItemGrade.Rare, category = ItemCategory.Equipment, goldBonusPercent = 3 },
                new ItemDef { id = "l1", name = "전설1", grade = ItemGrade.Legendary, category = ItemCategory.Equipment, goldBonusPercent = 20 },
                new ItemDef { id = "m_cloth", name = "수선 천", grade = ItemGrade.Common, category = ItemCategory.Material },
                new ItemDef { id = "m_thread", name = "기억의 실", grade = ItemGrade.Rare, category = ItemCategory.Material },
            };
            CharDb = TestContent.CreateCharacters();

            Table = ScriptableObject.CreateInstance<GachaTable>();
            Table.tableId = "test";
            Table.singleCost = 100;
            Table.tenCost = 900;
            Table.pityCount = 0;
            Table.pityGrade = ItemGrade.Legendary;
            Table.rates = new List<GachaGradeRate>
            {
                new GachaGradeRate { grade = ItemGrade.Common, weight = 70 },
                new GachaGradeRate { grade = ItemGrade.Rare, weight = 29 },
                new GachaGradeRate { grade = ItemGrade.Legendary, weight = 1 },
            };

            BattleCfg = ScriptableObject.CreateInstance<BattleConfig>();
            BattleCfg.ApplyDefaults();
            ExpeditionDb = ScriptableObject.CreateInstance<ExpeditionDatabase>();
            ExpeditionDb.ApplyDefaults();
            ShopDb = ScriptableObject.CreateInstance<ShopDatabase>();
            ShopDb.ApplyDefaults();
            SynergyDb = ScriptableObject.CreateInstance<SynergyDatabase>();
            MetaDb = MetaDatabase.CreateEmpty();

            CreateSystems(new System.Random(1234));
        }

        [TearDown]
        public void TearDownSystems()
        {
            Object.DestroyImmediate(Config);
            Object.DestroyImmediate(Items);
            Object.DestroyImmediate(CharDb);
            Object.DestroyImmediate(Table);
            Object.DestroyImmediate(BattleCfg);
            Object.DestroyImmediate(ExpeditionDb);
            Object.DestroyImmediate(ShopDb);
            Object.DestroyImmediate(SynergyDb);
            Object.DestroyImmediate(MetaDb);
        }

        protected void CreateSystems(System.Random random)
        {
            Data = PlayerData.CreateNew(Config);
            Currency = new CurrencySystem(Data);
            Inventory = new InventorySystem(Data, Items);
            Rewards = new RewardService(Currency, Inventory, Items);
            Mail = new MailSystem(Data, Rewards, Clock, Config.mailExpireDays);
            Growth = new GrowthSystem(Data, Config, Currency, Inventory);
            Characters = new CharacterSystem(Data, Config, CharDb, Currency, Clock);
            Costumes = new CostumeSystem(Data, CharDb, Characters, Clock);
            Evolution = new EvolutionSystem(Data, Config, Characters, Inventory, Items);
            Gacha = new GachaSystem(Data, Currency, Characters, CharDb, new[] { Table }, random);
            Shop = new ShopSystem(Data, ShopDb, Currency, Rewards, Costumes, Clock);
            Formation = new FormationSystem(Data, Characters);
            Synergy = new SynergySystem(Data, SynergyDb, Characters, Formation);
            Expedition = new ExpeditionSystem(Data, ExpeditionDb, Characters, Formation, Rewards, Clock, random);
            Battle = new BattleSystem(Data, BattleCfg, Characters, Formation, Synergy, Currency, Rewards, random);
            Dungeon = new DungeonSystem(Data, BattleCfg, Characters, Formation, Synergy, Rewards, Clock);
            Notice = new NoticeSystem(Data, Clock);
            Offline = new OfflineRewardSystem(Config, Growth, Currency, Clock);
            Account = new AccountSystem(Data, Clock, new LocalAccountService(),
                new IAuthProvider[] { new MockAuthProvider(AuthProviderType.Google), new MockAuthProvider(AuthProviderType.Kakao) });
            _ = new RewardMailSystem(Growth, Account, Mail, Config);
            CreateMetaSystems();
        }

        /// <summary>
        /// 메타 시스템을 만든다 (CreateSystems가 부름). 일일 시계는 UTC 시간대로 고정해 기기 시간대와 무관하게 한다
        /// (FakeClock 기본값 2026-10-28 03:00 UTC = 05:00 전이라 날 키 "2026-10-27").
        /// MetaDb 내용을 바꾼 뒤 시스템만 다시 만들 때도 부른다 (Data는 그대로).
        /// </summary>
        protected void CreateMetaSystems()
        {
            DailyClock = new DailyClock(Data, Clock, TimeZoneInfo.Utc);
            Power = new PowerSystem(Data, MetaDb, Characters, Formation, Synergy, Growth, Costumes);
            Progress = new ProgressTracker(Data, DailyClock, Battle, Growth, Characters, Evolution, Costumes, Gacha, Shop, Expedition, Dungeon,
                Formation, Synergy, Mail, Power);
            MetaRewards = new MetaRewardService(Data, MetaDb, Rewards, Items, Characters, Costumes, Growth);
            Popups = new PopupQueue();
            RedDots = new RedDotSystem();
            Features = new FeatureGateSystem(Data, MetaDb, Progress, Popups, MetaRewards);
            GuideQuest = new GuideQuestSystem(Data, MetaDb, Progress, MetaRewards, Features, Growth, Characters);
            Missions = new MissionSystem(Data, MetaDb, DailyClock, Progress, MetaRewards, Features);
            Attendance = new AttendanceSystem(Data, MetaDb, DailyClock, Progress, MetaRewards, Features);
            IdleChest = new IdleChestSystem(Data, MetaDb, Config, DailyClock, Growth, Currency, MetaRewards, Progress, Features);
            Tutorial = new TutorialSystem(Data, MetaDb, Progress, Features, MetaRewards, Popups);
            MetaSession = new MetaSession(Data, MetaDb, DailyClock, Progress, Power, RedDots, Popups, Features, GuideQuest, Missions,
                Attendance, IdleChest, Tutorial, MetaRewards, Battle, Notice, Mail, Expedition, Dungeon, Evolution, Characters, Costumes);
            CreateMeta4Systems();
        }

        /// <summary>4차 메타 시스템을 Game.SetupMeta4와 같은 순서로 만들고 모듈을 세션에 넣는다 (CreateMetaSystems가 부름)</summary>
        protected void CreateMeta4Systems()
        {
            Codex = new CodexSystem(Data, MetaDb, Progress, MetaRewards, Features, Characters, Costumes, Evolution, Synergy, Battle, Inventory);
            Achievements = new AchievementSystem(Data, MetaDb, Progress, MetaRewards, Features, Codex, Battle, DailyClock);
            GachaMilestones = new GachaMilestoneSystem(Data, MetaDb, Gacha, MetaRewards, Characters);
            Affinity = new AffinitySystem(Data, MetaDb, Characters, MetaRewards, DailyClock, Progress);
            ChapterChests = new ChapterChestSystem(Data, MetaDb, Battle, MetaRewards, Popups, Items, Tutorial);
            Story = new StorySystem(Data, MetaDb, Battle, MetaRewards, Popups, Tutorial);
            Events = new EventCalendarSystem(Data, MetaDb, DailyClock, Progress, MetaRewards, Features, Popups, Battle, Expedition, Dungeon, IdleChest);
            Season = new SeasonPassSystem(Data, MetaDb, DailyClock, Progress, MetaRewards, Features);
            Comeback = new ComebackSystem(Data, MetaDb, DailyClock, MetaRewards, IdleChest, Popups, Tutorial);
            Coupons = new CouponSystem(Data, MetaDb, DailyClock, Mail);
            StarterWeek = new StarterWeekSystem(Data, MetaDb, DailyClock, Progress, MetaRewards, Features);
            DailyGift = new DailyGiftSystem(Data, MetaDb, DailyClock, MetaRewards, Features);
            Cheer = new CheerSystem(Data, MetaDb, DailyClock, Battle, Tutorial);
            Fireflies = new FireflyRules(Data, MetaDb, DailyClock, MetaRewards, Battle, Tutorial);
            Strengthen = new StrengthenAdvisor(Power, Growth, Characters, Formation, Synergy, Evolution, Battle, Inventory, Cheer);
            BackupReminder = new BackupReminderSystem(Data, MetaDb, DailyClock, Mail);

            MetaSession.AddModule(Codex);
            MetaSession.AddModule(Achievements);
            MetaSession.AddModule(GachaMilestones);
            MetaSession.AddModule(Affinity);
            MetaSession.AddModule(ChapterChests);
            MetaSession.AddModule(Story);
            MetaSession.AddModule(Events);
            MetaSession.AddModule(Season);
            MetaSession.AddModule(Comeback);
            MetaSession.AddModule(StarterWeek);
            MetaSession.AddModule(DailyGift);
            MetaSession.AddModule(Cheer);
            MetaSession.AddModule(BackupReminder);
        }

        protected long NowUnix => TimeUtil.ToUnix(Clock.UtcNow);
    }
}
