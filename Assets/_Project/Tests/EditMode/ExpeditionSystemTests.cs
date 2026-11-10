using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    public class ExpeditionSystemTests : SystemsTestBase
    {
        [Test]
        public void Defaults_HaveEightZones_InStageOrder()
        {
            var zones = ExpeditionDb.zones;
            Assert.AreEqual(8, zones.Count);
            var ids = new HashSet<string>();
            int lastStage = 0;
            foreach (var zone in zones)
            {
                Assert.IsTrue(ids.Add(zone.id), zone.id);
                StringAssert.StartsWith("exp_", zone.id);
                Assert.IsNotEmpty(zone.name);
                Assert.IsNotEmpty(zone.place);
                Assert.IsNotEmpty(zone.description);
                Assert.IsNotEmpty(zone.drops);
                Assert.GreaterOrEqual(zone.requiredStage, lastStage);
                Assert.Greater(zone.durationMinutes, 0);
                Assert.AreEqual(3, zone.maxMembers);
                lastStage = zone.requiredStage;
            }
            Assert.AreEqual(10, ExpeditionDb.Get("exp_1_1").durationMinutes);
            Assert.AreEqual(20, ExpeditionDb.Get("exp_2_4").requiredStage);
            Assert.IsNotNull(ExpeditionDb.Get("exp_2_4").drops.Find(d => d.itemId == "mat_cloth"));
        }

        [Test]
        public void ZoneUnlock_FollowsHighestStage()
        {
            Assert.IsTrue(Expedition.IsZoneUnlocked("exp_1_1"));
            Assert.IsFalse(Expedition.IsZoneUnlocked("exp_1_2"));
            Assert.IsFalse(Expedition.IsZoneUnlocked("nope"));

            Data.battle.highestStage = 3;

            Assert.IsTrue(Expedition.IsZoneUnlocked("exp_1_2"));
            Assert.IsFalse(Expedition.IsZoneUnlocked("exp_1_3"));
        }

        [Test]
        public void StartAndClaim_FollowsClock()
        {
            Characters.Grant("sock");
            int changed = 0;
            Expedition.Changed += () => changed++;

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, Expedition.Active.Count);
            Assert.IsTrue(Formation.IsDispatched("sock"));
            Assert.IsFalse(Expedition.CanSelect("sock", out var reason));
            Assert.AreEqual("원정 파견 중이에요.", reason);

            var active = Expedition.GetActive("exp_1_1");
            Assert.AreEqual(TimeSpan.FromMinutes(10), Expedition.GetRemaining(active));
            Assert.IsNull(Expedition.Claim("exp_1_1"), "완료 전에는 받을 수 없다");

            Clock.Advance(TimeSpan.FromMinutes(9));
            Assert.IsFalse(Expedition.IsComplete(active));
            Assert.AreEqual(TimeSpan.FromMinutes(1), Expedition.GetRemaining(active));
            Assert.AreEqual(0, Expedition.CompletedCount);

            Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.IsTrue(Expedition.IsComplete(active));
            Assert.AreEqual(1, Expedition.CompletedCount);

            var result = Expedition.Claim("exp_1_1");

            Assert.IsNotNull(result);
            Assert.AreEqual("exp_1_1", result.Zone.id);
            CollectionAssert.AreEqual(new[] { "sock" }, result.LineageIds);
            Assert.AreEqual(300, Currency.Get(CurrencyType.Gold), 1e-6, "1명 파견 = 구역 단추 그대로");
            foreach (var reward in result.Rewards)
            {
                if (reward.type == RewardType.Item) Assert.IsNotNull(ExpeditionDb.Get("exp_1_1").drops.Find(d => d.itemId == reward.itemId));
            }
            Assert.AreEqual(1, Characters.GetProgress("sock", ProgressType.ExpeditionCount));
            Assert.AreEqual(ExpeditionSystem.CompleteAffinity, Characters.GetAffinity("sock"));
            Assert.AreEqual(0, Expedition.Active.Count);
            Assert.IsFalse(Formation.IsDispatched("sock"));
            Assert.IsNull(Expedition.Claim("exp_1_1"), "두 번 받을 수 없다");
        }

        [Test]
        public void Start_AllowedWhenPlacedInFormation_AndStaysPlaced()
        {
            // M05: 원정과 편성은 따로 — 편성에 있는 친구도 보낼 수 있고, 원정 중에도 편성에서 빠지지 않는다
            Characters.Grant("sock");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");

            Assert.IsTrue(Expedition.CanSelect("sock", out var reason));
            Assert.IsNull(reason);
            Assert.IsTrue(Expedition.CanSelect("button", out _), "잔향이 덱에 있어도 보낼 수 있다");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock", "button" }));

            Assert.AreEqual(1, Expedition.Active.Count);
            Assert.AreEqual("sock_base", Formation.GetGuardian(0), "편성은 그대로");
            Assert.AreEqual("button_base", Formation.GetEcho(0));
            Assert.IsTrue(Formation.IsDispatched("sock"));
            Assert.IsFalse(Expedition.CanSelect("sock", out reason), "다른 원정에는 못 간다");
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void LastMembers_RememberedPerZone_InSentOrder()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Data.battle.highestStage = 3;
            Assert.IsEmpty(Expedition.GetLastMembers("exp_1_1"), "보낸 적 없으면 빈 목록");
            Assert.IsEmpty(Expedition.GetLastMembers(null));
            Assert.IsEmpty(Expedition.GetLastMembers("nope"));

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "candle", "sock" }));
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "button" }));

            CollectionAssert.AreEqual(new[] { "candle", "sock" }, Expedition.GetLastMembers("exp_1_1"));
            CollectionAssert.AreEqual(new[] { "button" }, Expedition.GetLastMembers("exp_1_2"));
            Assert.AreEqual(2, Data.expeditionLastMembers.Count, "세이브에 저장된다");

            // 받아도, 취소해도 남는다 (다시 보내기는 받은 직후에 쓴다)
            Clock.Advance(TimeSpan.FromMinutes(10));
            Assert.IsNotNull(Expedition.Claim("exp_1_1"));
            Assert.IsTrue(Expedition.TryCancel("exp_1_2"));
            CollectionAssert.AreEqual(new[] { "candle", "sock" }, Expedition.GetLastMembers("exp_1_1"));
            CollectionAssert.AreEqual(new[] { "button" }, Expedition.GetLastMembers("exp_1_2"));

            // 다른 친구로 보내면 바뀐다 (목록은 진행 중 원정 기록과 따로)
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "button" }));
            CollectionAssert.AreEqual(new[] { "button" }, Expedition.GetLastMembers("exp_1_1"));
            Expedition.GetActive("exp_1_1").lineageIds.Add("sock");
            CollectionAssert.AreEqual(new[] { "button" }, Expedition.GetLastMembers("exp_1_1"));
            Assert.AreEqual(2, Data.expeditionLastMembers.Count, "구역마다 하나");
        }

        [Test]
        public void Resend_UsesLastMembers_OnlyWhenAllStillEligible()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Data.battle.highestStage = 3;
            Assert.IsFalse(Expedition.CanResend("exp_1_1"), "기억한 친구가 없으면 다시 보내기 없음");
            Assert.IsFalse(Expedition.TryResend("exp_1_1"));

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock", "candle" }));
            Assert.IsFalse(Expedition.CanResend("exp_1_1"), "진행 중인 구역");
            Clock.Advance(TimeSpan.FromMinutes(10));
            Assert.IsFalse(Expedition.CanResend("exp_1_1"), "완료돼도 받기 전에는 진행 중");
            Expedition.Claim("exp_1_1");
            Assert.IsTrue(Expedition.CanResend("exp_1_1"));

            // 한 명이라도 다른 원정에 나가 있으면 일부만 몰래 보내지 않는다
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "candle" }));
            Assert.IsFalse(Expedition.CanResend("exp_1_1"));
            Assert.IsFalse(Expedition.TryResend("exp_1_1"));
            Assert.IsNull(Expedition.GetActive("exp_1_1"));
            Expedition.TryCancel("exp_1_2");

            int changed = 0;
            Expedition.Changed += () => changed++;
            Assert.IsTrue(Expedition.TryResend("exp_1_1"));
            Assert.AreEqual(1, changed);
            CollectionAssert.AreEqual(new[] { "sock", "candle" }, Expedition.GetActive("exp_1_1").lineageIds);
            CollectionAssert.AreEqual(new[] { "sock", "candle" }, Expedition.GetLastMembers("exp_1_1"));
        }

        [Test]
        public void Resend_BlockedWhenSlotsFullOrZoneLocked()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Data.battle.highestStage = 20;
            Assert.IsTrue(Expedition.TryStart("exp_1_4", new[] { "sock" }));
            Clock.Advance(TimeSpan.FromHours(3));
            Expedition.Claim("exp_1_4");

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "candle" }));
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "button" }));
            Assert.IsTrue(Expedition.CanResend("exp_1_4"), "자리 하나 남음");

            // 세 번째 자리를 sock 없이 채운다 (테스트 계보가 셋뿐이라 기록을 직접 넣음)
            Data.expeditions.Add(new ExpeditionSave { zoneId = "exp_1_3", startUnix = NowUnix, endUnix = NowUnix + 600 });
            Assert.AreEqual(Expedition.MaxConcurrent, Expedition.Active.Count);
            Assert.IsFalse(Expedition.CanResend("exp_1_4"), "동시 원정 자리가 꽉 참");
            Assert.IsFalse(Expedition.TryResend("exp_1_4"));

            Expedition.TryCancel("exp_1_3");
            Assert.IsTrue(Expedition.CanResend("exp_1_4"));
            Data.battle.highestStage = 1;
            Assert.IsFalse(Expedition.CanResend("exp_1_4"), "잠긴 구역");
        }

        [Test]
        public void LastMembers_OldSaveWithoutField_LoadsEmpty()
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>("{\"version\":3,\"expeditions\":[]}");
            data.Sanitize();
            Assert.IsNotNull(data.expeditionLastMembers);
            Assert.IsEmpty(data.expeditionLastMembers);

            var broken = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>(
                "{\"version\":3,\"expeditionLastMembers\":[null,{\"zoneId\":\"\"},{\"zoneId\":\"exp_1_1\",\"lineageIds\":null},{\"zoneId\":\"exp_1_2\",\"lineageIds\":[\"sock\",null,\"\"]}]}");
            broken.Sanitize();
            Assert.AreEqual(2, broken.expeditionLastMembers.Count, "빈 항목은 버린다");
            Assert.IsEmpty(broken.expeditionLastMembers[0].lineageIds);
            CollectionAssert.AreEqual(new[] { "sock" }, broken.expeditionLastMembers[1].lineageIds);
        }

        [Test]
        public void Start_RejectsBadRequests()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");

            Assert.IsFalse(Expedition.TryStart("exp_1_1", new[] { "button" }), "미보유");
            Assert.IsFalse(Expedition.TryStart("exp_1_1", Array.Empty<string>()));
            Assert.IsFalse(Expedition.TryStart("exp_1_1", null));
            Assert.IsFalse(Expedition.TryStart("exp_1_1", new[] { "sock", "sock" }), "같은 계보 중복");
            Assert.IsFalse(Expedition.TryStart("exp_1_1", new[] { "sock", "candle", "sock", "candle" }), "인원 초과");
            Assert.IsFalse(Expedition.TryStart("exp_1_2", new[] { "sock" }), "잠긴 구역");
            Assert.IsFalse(Expedition.TryStart("nope", new[] { "sock" }));
            Assert.AreEqual(0, Expedition.Active.Count);

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
            Assert.IsFalse(Expedition.TryStart("exp_1_1", new[] { "candle" }), "같은 구역은 하나만");
        }

        [Test]
        public void MaxConcurrent_LimitsActiveExpeditions()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Data.battle.highestStage = 20;
            Assert.AreEqual(3, Expedition.MaxConcurrent);

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" }));
            Assert.IsTrue(Expedition.TryStart("exp_1_2", new[] { "candle" }));
            Assert.IsTrue(Expedition.TryStart("exp_1_3", new[] { "button" }));
            Assert.AreEqual(3, Expedition.Active.Count);

            // 완료됐어도 받기 전까지는 자리를 차지한다
            Clock.Advance(TimeSpan.FromHours(1));
            Expedition.Claim("exp_1_1");
            Assert.IsTrue(Expedition.TryStart("exp_1_4", new[] { "sock" }));
        }

        [Test]
        public void MoreMembers_GiveMoreGold()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");

            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock", "candle", "button" }));
            Clock.Advance(TimeSpan.FromMinutes(10));
            var result = Expedition.Claim("exp_1_1");

            double expected = Math.Floor(300 * (1 + ExpeditionSystem.ExtraMemberGoldBonus * 2));
            Assert.AreEqual(expected, Currency.Get(CurrencyType.Gold), 1e-6);
            foreach (var id in new[] { "sock", "candle", "button" })
                Assert.AreEqual(1, Characters.GetProgress(id, ProgressType.ExpeditionCount), id);
            Assert.AreEqual(3, result.LineageIds.Count);
        }

        [Test]
        public void ClaimAll_ClaimsOnlyCompleted()
        {
            Characters.Grant("sock");
            Characters.Grant("candle");
            Data.battle.highestStage = 3;
            Expedition.TryStart("exp_1_1", new[] { "sock" });    // 10분
            Expedition.TryStart("exp_1_2", new[] { "candle" });  // 20분

            Clock.Advance(TimeSpan.FromMinutes(15));
            var results = Expedition.ClaimAll();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("exp_1_1", results[0].Zone.id);
            Assert.AreEqual(1, Expedition.Active.Count);
        }

        [Test]
        public void ClockRollback_DisplayAndCompletionStayInSync()
        {
            Characters.Grant("sock");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock" })); // 10분
            var active = Expedition.GetActive("exp_1_1");

            Clock.Advance(TimeSpan.FromMinutes(-30)); // 기기 시간이 시작 전으로 되돌아감

            Assert.AreEqual(TimeSpan.FromMinutes(10), Expedition.GetRemaining(active), "원래 소요 시간보다 길어지지 않는다");
            Assert.AreEqual(0f, Expedition.GetProgress(active));
            Assert.IsFalse(Expedition.IsComplete(active));

            Clock.Advance(TimeSpan.FromMinutes(4));
            Assert.AreEqual(TimeSpan.FromMinutes(6), Expedition.GetRemaining(active), "되돌린 뒤에도 남은 시간이 줄어든다");
            Assert.AreEqual(0.4f, Expedition.GetProgress(active), 1e-4f);
            Assert.IsFalse(Expedition.IsComplete(active));

            Clock.Advance(TimeSpan.FromMinutes(6));
            Assert.AreEqual(TimeSpan.Zero, Expedition.GetRemaining(active));
            Assert.IsTrue(Expedition.IsComplete(active), "표시된 남은 시간이 끝나면 실제로도 완료");
            Assert.IsNotNull(Expedition.Claim("exp_1_1"));
        }

        [Test]
        public void Cancel_ReturnsMembersWithoutRewards()
        {
            Characters.Grant("sock");
            Expedition.TryStart("exp_1_1", new[] { "sock" });

            Assert.IsTrue(Expedition.TryCancel("exp_1_1"));
            Assert.IsFalse(Expedition.TryCancel("exp_1_1"));

            Assert.AreEqual(0, Expedition.Active.Count);
            Assert.IsFalse(Formation.IsDispatched("sock"));
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gold), 1e-9);
            Assert.AreEqual(0, Characters.GetProgress("sock", ProgressType.ExpeditionCount));
            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"));
        }
    }
}
