using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    public class DungeonSystemTests : SystemsTestBase
    {
        void SetEnemies(float hp, float damage)
        {
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = hp;
                enemy.damage = damage;
            }
        }

        void PlaceEchoDeck()
        {
            Characters.Grant("button");
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Formation.TryPlace(CharacterRole.Echo, 1, "sock_a");
        }

        static bool HasItem(DungeonResult result, string itemId) =>
            result.Rewards.Exists(r => r.type == RewardType.Item && r.itemId == itemId);

        [Test]
        public void Tickets_ResetOnNextLocalDay()
        {
            Data.dungeon.highestFloor = 5;
            Assert.AreEqual(3, Dungeon.DailyTickets);
            Assert.AreEqual(3, Dungeon.TicketsLeft);

            Assert.IsNotNull(Dungeon.Sweep(5));
            Assert.IsNotNull(Dungeon.Sweep(1));
            Assert.IsNotNull(Dungeon.Sweep(3));
            Assert.AreEqual(0, Dungeon.TicketsLeft);
            Assert.IsNull(Dungeon.Sweep(1), "입장권 없음");

            Clock.Advance(TimeSpan.FromHours(12));
            Clock.Advance(TimeSpan.FromHours(12));

            Assert.AreEqual(3, Dungeon.TicketsLeft);
            Assert.IsNotNull(Dungeon.Sweep(1));
            Assert.AreEqual(2, Dungeon.TicketsLeft);
        }

        /// <summary>일일 시계(05:00 초기화, UTC 시간대)를 쓰는 던전 — Game.Setup과 같은 연결 (M77)</summary>
        (DungeonSystem dungeon, DailyClock clock) CreateDailyDungeon(DateTime utcNow)
        {
            Clock.UtcNow = utcNow;
            var dailyClock = new DailyClock(Data, Clock, TimeZoneInfo.Utc);
            dailyClock.Refresh();
            return (new DungeonSystem(Data, BattleCfg, Characters, Formation, Synergy, Rewards, Clock, dailyClock), dailyClock);
        }

        [Test]
        public void WithDailyClock_Tickets_RefillAt0500_NotAtMidnight()
        {
            Data.dungeon.highestFloor = 5;
            var (dungeon, _) = CreateDailyDungeon(new DateTime(2026, 11, 3, 23, 0, 0, DateTimeKind.Utc));
            dungeon.Sweep(1);
            dungeon.Sweep(1);
            dungeon.Sweep(1);
            Assert.AreEqual(0, dungeon.TicketsLeft);
            Assert.AreEqual("2026-11-03", Data.dungeon.ticketDate, "입장권 날짜는 DailyClock 날 키");

            Clock.UtcNow = new DateTime(2026, 11, 4, 0, 30, 0, DateTimeKind.Utc);
            Assert.AreEqual(0, dungeon.TicketsLeft, "자정이 지나도 새벽 5시 전에는 그대로 (상점·출석과 같은 시각)");
            Assert.AreEqual(TimeSpan.FromMinutes(270), dungeon.TimeUntilTicketReset);

            Clock.UtcNow = new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(3, dungeon.TicketsLeft);
            Assert.IsNotNull(dungeon.Sweep(1));
            Assert.AreEqual("2026-11-04", Data.dungeon.ticketDate);
            Assert.AreEqual(2, dungeon.TicketsLeft);
        }

        [Test]
        public void WithDailyClock_RollbackDoesNotRefillTickets()
        {
            Data.dungeon.highestFloor = 5;
            var (dungeon, dailyClock) = CreateDailyDungeon(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
            dungeon.Sweep(1);
            dungeon.Sweep(1);
            dungeon.Sweep(1);

            Clock.UtcNow = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);
            dailyClock.Refresh();

            Assert.AreEqual(0, dungeon.TicketsLeft, "기기 시계를 되돌려도 날 키는 거꾸로 가지 않는다");
        }

        [Test]
        public void Sweep_OnlyClearedFloors_GivesFloorRewards()
        {
            Assert.IsNull(Dungeon.Sweep(1), "돌파한 층이 없다");
            Data.dungeon.highestFloor = 5;
            Assert.IsNull(Dungeon.Sweep(6));
            Assert.IsNull(Dungeon.Sweep(0));
            Assert.AreEqual(3, Dungeon.TicketsLeft, "실패한 소탕은 입장권을 쓰지 않는다");

            var result = Dungeon.Sweep(5);

            Assert.IsTrue(result.Cleared);
            Assert.AreEqual(5, result.Floor);
            Assert.IsTrue(HasItem(result, "mat_thread"));
            Assert.IsTrue(HasItem(result, "mat_spring"));
            Assert.Greater(Currency.Get(CurrencyType.Gold), 0);
        }

        [Test]
        public void Rewards_UnlockByFloor()
        {
            bool Has(int floor, string itemId) => Dungeon.GetRewards(floor).Exists(r => r.type == RewardType.Item && r.itemId == itemId);

            Assert.IsFalse(Has(2, "mat_thread"));
            Assert.IsTrue(Has(3, "mat_thread"));
            Assert.IsFalse(Has(4, "mat_spring"));
            Assert.IsTrue(Has(5, "mat_spring"));
            Assert.AreEqual(0f, Dungeon.GetMoonlightChance(9));
            Assert.Greater(Dungeon.GetMoonlightChance(10), 0f);
            Assert.Less(Dungeon.GetMoonlightChance(10), 0.5f, "달빛 조각은 낮은 확률");
            Assert.IsTrue(Dungeon.GetRewards(1).Exists(r => r.type == RewardType.Currency && r.currency == CurrencyType.Gold));
        }

        [Test]
        public void Challenge_EmptyDeck_ReturnsNull()
        {
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"); // 지킴이는 던전에 들어가지 않는다

            Assert.IsFalse(Dungeon.HasDeck);
            Assert.IsNull(Dungeon.Challenge());
            Assert.AreEqual(3, Dungeon.TicketsLeft);
        }

        [Test]
        public void Challenge_Cleared_AdvancesFloorAndUsesTicket()
        {
            SetEnemies(1f, 1f);
            PlaceEchoDeck();
            int changed = 0;
            Dungeon.Changed += () => changed++;

            var result = Dungeon.Challenge();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Cleared);
            Assert.AreEqual(1, result.Floor);
            Assert.Greater(result.SurvivedSeconds, 0f);
            Assert.AreEqual(1, Dungeon.HighestFloor);
            Assert.AreEqual(2, Dungeon.NextFloor);
            Assert.AreEqual(2, Dungeon.TicketsLeft);
            Assert.Greater(Currency.Get(CurrencyType.Gold), 0);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void Challenge_Cleared_RecordsEchoDeployCount()
        {
            SetEnemies(1f, 1f);
            PlaceEchoDeck();
            int progressEvents = 0;
            Characters.ProgressChanged += (_, type) => { if (type == ProgressType.EchoDeployCount) progressEvents++; };

            var result = Dungeon.Challenge();

            Assert.IsTrue(result.Cleared);
            int deploys = Characters.GetProgress("button", ProgressType.EchoDeployCount)
                          + Characters.GetProgress("sock", ProgressType.EchoDeployCount);
            Assert.Greater(deploys, 0, "던전에서 소환한 잔향이도 출격 기록으로 센다");
            Assert.LessOrEqual(progressEvents, 2, "계보별로 모아서 한 번에 반영");
        }

        [Test]
        public void Challenge_Failed_DoesNotRecordEchoDeployCount()
        {
            SetEnemies(1e6f, 1e6f);
            PlaceEchoDeck();

            var result = Dungeon.Challenge();

            Assert.IsFalse(result.Cleared);
            Assert.AreEqual(0, Characters.GetProgress("button", ProgressType.EchoDeployCount), "실패는 무료로 반복되므로 세지 않는다");
            Assert.AreEqual(0, Characters.GetProgress("sock", ProgressType.EchoDeployCount));
        }

        [Test]
        public void Challenge_Failed_KeepsTicketAndFloor()
        {
            SetEnemies(1e6f, 1e6f);
            PlaceEchoDeck();

            var result = Dungeon.Challenge();

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Cleared);
            Assert.IsEmpty(result.Rewards);
            Assert.AreEqual(0, Dungeon.HighestFloor);
            Assert.AreEqual(3, Dungeon.TicketsLeft);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gold), 1e-9);
        }

        [Test]
        public void Challenge_NoTickets_ReturnsNull()
        {
            SetEnemies(1f, 1f);
            PlaceEchoDeck();
            Data.dungeon.ticketDate = Clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            Data.dungeon.ticketsUsed = 3;

            Assert.AreEqual(0, Dungeon.TicketsLeft);
            Assert.IsNull(Dungeon.Challenge());
            Assert.AreEqual(0, Dungeon.HighestFloor);
        }

        // ───────────────────────────── 시너지 ─────────────────────────────

        static double Gold(System.Collections.Generic.List<RewardData> rewards)
        {
            double total = 0;
            foreach (var r in rewards)
                if (r.type == RewardType.Currency && r.currency == CurrencyType.Gold) total += r.amount;
            return total;
        }

        /// <summary>돌격 2명이면 팀 단추 획득량 +50% (잔향이 덱만으로도 켜진다)</summary>
        void UseAssaultGoldSynergy()
        {
            SynergyDb.synergies = new System.Collections.Generic.List<SynergyDef>
            {
                new SynergyDef
                {
                    id = "style_assault", name = "돌격", kind = SynergyKind.Style, style = CombatStyle.Assault,
                    tiers = new System.Collections.Generic.List<SynergyTier>
                    {
                        new SynergyTier
                        {
                            count = 2, membersOnly = false,
                            modifiers = new System.Collections.Generic.List<StatModifier>
                            {
                                new StatModifier { stat = StatType.GoldGainPercent, value = 50f },
                            },
                        },
                    },
                },
            };
        }

        [Test]
        public void Rewards_DeckSynergyGoldBonus_AppliesToPreviewChallengeAndSweep()
        {
            UseAssaultGoldSynergy();
            double baseGold = Gold(Dungeon.GetRewards(1));
            Assert.Greater(baseGold, 0);
            Assert.AreEqual(0f, Dungeon.GoldBonusPercent, "덱이 비어 있으면 보너스 없음");

            PlaceEchoDeck(); // button_base, sock_a — 둘 다 돌격
            Assert.AreEqual(50f, Dungeon.GoldBonusPercent, 1e-4);
            double boosted = Math.Floor(baseGold * 1.5);
            Assert.AreEqual(boosted, Gold(Dungeon.GetRewards(1)), 1e-9, "보상 미리보기에도 반영");

            SetEnemies(1f, 1f);
            var result = Dungeon.Challenge();
            Assert.IsTrue(result.Cleared);
            Assert.AreEqual(boosted, Gold(result.Rewards), 1e-9, "도전 단추 보상");

            var sweep = Dungeon.Sweep(1);
            Assert.IsNotNull(sweep);
            Assert.AreEqual(boosted, Gold(sweep.Rewards), 1e-9, "소탕도 같은 보너스");
        }

        [Test]
        public void Rewards_GuardianSideSynergyDoesNotCount()
        {
            UseAssaultGoldSynergy();
            Characters.Grant("button");
            Characters.Grant("sock");
            Characters.UnlockForm("sock_a");
            Formation.TryPlace(CharacterRole.Guardian, 0, "button_base");
            Formation.TryPlace(CharacterRole.Echo, 0, "sock_a");

            Assert.AreEqual(0f, Dungeon.GoldBonusPercent, "던전 시너지는 잔향이 덱만 센다");
        }

        [Test]
        public void CreateSetup_EchoesOnly_WithoutGuardianResonanceAttack()
        {
            PlaceEchoDeck();
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"));
            Assert.Greater(Formation.TeamAttackBonusPercent, 0f, "스테이지에서는 지킴이+잔향이 계보 공명 보너스가 붙는다");

            var setup = Dungeon.CreateSetup(4);

            Assert.IsNotNull(setup);
            Assert.AreEqual(BattleMode.EchoDungeon, setup.Mode);
            Assert.AreEqual(4, setup.Level);
            Assert.AreEqual(1f, setup.AttackMultiplier, "지킴이가 싸우지 않는 던전에는 계보 공명 공격력이 붙지 않는다");
            Assert.AreEqual(StageModifierType.None, setup.StageModifier);
            foreach (var guardian in setup.Guardians) Assert.IsNull(guardian);
            Assert.AreEqual("button_base", setup.Deck[0]?.FormId);
            Assert.AreEqual("sock_a", setup.Deck[1]?.FormId);
        }
    }
}
