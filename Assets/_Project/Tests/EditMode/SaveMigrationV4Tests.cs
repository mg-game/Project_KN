using System;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 세이브 버전 3 → 4 이전 (SPEC4 §2.1 · §9.4 P4-0 done-when, 로드맵 CR4-24):
    /// 실제 버전 3 세이브(AutoShot 플레이어 세이브 사본, Fixtures/autoshot_player_v3.json)가 LocalSaveStorage로 읽히고,
    /// 도감 채우기 예약(codex.backfillPending) · 4차 칸이 모두 있음 · 튜토리얼을 마친 세이브의 PlayMinutes = min(270, 30 × 설치 일수)
    /// (① 설치 ~ 마지막 세션 날로 바로, ② 오늘까지 센 설치 일수로 다음 MetaSession.Start에서 한 번 더 — 오래 쉰 세이브) ·
    /// 다시 저장하면 버전 4, 다시 읽어도 두 번 옮기지 않음. 사용자 세이브는 건드리지 않는다 (임시 폴더).
    /// </summary>
    public class SaveMigrationV4Tests
    {
        const string FixtureName = "autoshot_player_v3.json";

        string _dir;

        [SetUp]
        public void CreateTempDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ll_save_v4_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void DeleteTempDir()
        {
            try
            {
                if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
                // 임시 폴더 — 지우지 못해도 테스트 결과와 무관
            }
        }

        /// <summary>픽스처 경로: Assets/_Project/Tests/EditMode/Fixtures (verify_proj 사본에서도 그 사본의 Assets 기준). 못 찾으면 이 파일 옆</summary>
        static string FixturePath([CallerFilePath] string thisFile = "")
        {
            string fromAssets = Path.Combine(Application.dataPath, "_Project", "Tests", "EditMode", "Fixtures", FixtureName);
            if (File.Exists(fromAssets)) return fromAssets;
            string besideThis = string.IsNullOrEmpty(thisFile) ? null : Path.Combine(Path.GetDirectoryName(thisFile) ?? ".", "Fixtures", FixtureName);
            return besideThis != null && File.Exists(besideThis) ? besideThis : fromAssets;
        }

        static JObject ReadFixture()
        {
            string path = FixturePath();
            Assert.IsTrue(File.Exists(path), $"픽스처가 없음: {path}");
            return JObject.Parse(File.ReadAllText(path));
        }

        /// <summary>JSON을 임시 세이브 파일로 쓰고 실제 저장소로 읽는다</summary>
        PlayerData LoadJson(JObject json, out LocalSaveStorage storage, out string path)
        {
            path = Path.Combine(_dir, "player.json");
            File.WriteAllText(path, json.ToString(Formatting.None));
            storage = new LocalSaveStorage(path);
            var data = storage.Load();
            Assert.AreEqual(SaveLoadResult.Loaded, storage.LastLoadResult);
            Assert.IsNotNull(data);
            return data;
        }

        static long Lifetime(PlayerData data, CounterKey key) =>
            data.meta.counters.lifetime.TryGetValue(key, out long value) ? value : 0;

        [Test]
        public void Fixture_IsVersion3_WithoutMeta4Sections()
        {
            var json = ReadFixture();
            Assert.AreEqual(3, (int)json["version"], "픽스처는 버전 3 세이브여야 한다");
            Assert.IsNull(json["meta"]?["codex"], "버전 3 세이브에는 4차 칸이 없다");
            Assert.IsNull(json["battle"]?["lampPolicy"]);
        }

        [Test]
        public void V3Save_Loads_AndMigratesToV4()
        {
            var data = LoadJson(ReadFixture(), out _, out _);

            Assert.AreEqual(4, PlayerData.CurrentVersion);
            Assert.AreEqual(PlayerData.CurrentVersion, data.version);
            Assert.IsTrue(data.meta.codex.backfillPending, "버전 4 이전은 도감 채우기를 예약한다");

            // 4차 칸이 모두 있다 (null 아님)
            var meta = data.meta;
            Assert.IsNotNull(meta.achievements?.claimed);
            Assert.IsNotNull(meta.codex?.found);
            Assert.IsNotNull(meta.codex.claimed);
            Assert.IsNotNull(meta.codex.seenSkills);
            Assert.IsNotNull(meta.gachaMilestones);
            Assert.IsNotNull(meta.events?.instances);
            Assert.IsNotNull(meta.season?.claimedTiers);
            Assert.IsNotNull(meta.story?.seenChapters);
            Assert.IsNotNull(meta.story.rewardedChapters);
            Assert.IsNotNull(meta.affinity?.claimedLevel);
            Assert.IsNotNull(meta.affinity.pets);
            Assert.IsNotNull(meta.coupons?.redeemed);
            Assert.IsNotNull(meta.chapterChests?.opened);
            Assert.IsNotNull(meta.starterWeek?.done);
            Assert.IsNotNull(meta.starterWeek.claimed);
            Assert.IsNotNull(meta.starterWeek.chests);
            Assert.IsNotNull(meta.dailyGift);
            Assert.IsNotNull(meta.firefly);
            Assert.IsNotNull(meta.cheer);
            Assert.IsNotNull(meta.backup, "백업 알림 칸 (RM-2 SB-1 — 더하기만, 버전 그대로)");
            Assert.IsNotNull(data.dungeon.blessings);
            Assert.IsNotNull(data.dungeon.blessingOffer);

            // 새 값은 안전한 기본값
            Assert.AreEqual(0, data.battle.lampPolicy, "불 밝히기 방식 = Auto");
            Assert.AreEqual(-1, data.dungeon.ticketBank, "입장권 모아 두기 = 아직 옮기지 않음");
            Assert.AreEqual(0, meta.wallet.friendSelectTickets);
            Assert.AreEqual(0, meta.idleChest.bonusCapSeconds);
            Assert.AreEqual(0, meta.gachaMilestones.pulls);
            Assert.IsNull(meta.starterWeek.startDayKey);
            Assert.IsFalse(meta.starterWeek.declined);
            Assert.IsNull(meta.backup.lastExportDayKey);
            Assert.IsNull(meta.backup.lastReminderDayKey);

            // 옛 값은 그대로
            Assert.AreEqual(3, data.battle.highestStage);
            Assert.AreEqual(39, data.battle.totalKills);
            Assert.AreEqual(39, Lifetime(data, CounterKey.EnemyKill));
            Assert.IsTrue(meta.ftue.done);
            Assert.AreEqual(5, data.lineages.Count);
            Assert.IsTrue(meta.features.unlocked.Contains(FeatureId.ExpeditionBatch));
        }

        [Test]
        public void V3Save_PastFtue_SeedsPlayMinutes_FromInstallDays()
        {
            var json = ReadFixture();
            // 픽스처: 설치 날 = 마지막 날 (설치 1일째) → 30분
            var data = LoadJson(json, out _, out _);
            Assert.AreEqual(MetaSave.PlayMinutesSeedPerDay, Lifetime(data, CounterKey.PlayMinutes));

            // 설치 3일째 → 90분
            json["meta"]["clock"]["installDayKey"] = "2026-11-03";
            json["meta"]["clock"]["lastDayKey"] = "2026-11-05";
            data = LoadJson(json, out _, out _);
            Assert.AreEqual(90, Lifetime(data, CounterKey.PlayMinutes));

            // 오래 한 세이브는 상한 270
            json["meta"]["clock"]["installDayKey"] = "2026-09-01";
            data = LoadJson(json, out _, out _);
            Assert.AreEqual(MetaSave.PlayMinutesSeedCap, Lifetime(data, CounterKey.PlayMinutes));
            Assert.IsTrue(data.meta.playMinutesSeedPending, "둘째 단계(오늘까지 센 설치 일수)는 다음 MetaSession.Start");
        }

        [Test]
        public void V3Save_LapsedPlayer_SecondStepUsesTodaysInstallDays()
        {
            // 설치 9/1, 마지막 세션 9/2 (둘째 날까지만 하고 쉼), 11/5에 다시 켬 → InstallDays 조건 = 66일째
            var json = ReadFixture();
            json["meta"]["clock"]["installDayKey"] = "2026-09-01";
            json["meta"]["clock"]["lastDayKey"] = "2026-09-02";
            var data = LoadJson(json, out _, out _);
            Assert.AreEqual(2 * MetaSave.PlayMinutesSeedPerDay, Lifetime(data, CounterKey.PlayMinutes), "① 마이그레이션 = 설치 ~ 마지막 세션 날 (2일)");
            Assert.IsTrue(data.meta.playMinutesSeedPending);

            var clock = new DailyClock(data, new FakeClock { UtcNow = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc) }, TimeZoneInfo.Utc);
            clock.Refresh();
            Assert.AreEqual(66, clock.InstallDay);

            Assert.IsTrue(data.meta.ApplyPendingPlayMinutesSeed(clock.InstallDay));
            Assert.AreEqual(MetaSave.PlayMinutesSeedCap, Lifetime(data, CounterKey.PlayMinutes), "② 오늘까지 센 설치 일수 → 상한 270");
            Assert.IsFalse(data.meta.playMinutesSeedPending);
            Assert.IsFalse(data.meta.ApplyPendingPlayMinutesSeed(clock.InstallDay), "한 번만");
        }

        [Test]
        public void SecondStep_NeverLowersPlayMinutes()
        {
            var data = LoadJson(ReadFixture(), out _, out _); // 설치 1일째 → 30분, 둘째 단계 남음
            data.meta.counters.lifetime[CounterKey.PlayMinutes] = 200;

            Assert.IsFalse(data.meta.ApplyPendingPlayMinutesSeed(3), "90분 < 200분 — 그대로");
            Assert.AreEqual(200, Lifetime(data, CounterKey.PlayMinutes));
            Assert.IsFalse(data.meta.playMinutesSeedPending, "올리지 않아도 끈다");
        }

        [Test]
        public void V3Save_BeforeFtueDone_DoesNotSeedPlayMinutes()
        {
            var json = ReadFixture();
            json["meta"]["ftue"]["done"] = false;
            json["meta"]["ftue"]["skipped"] = false;

            var data = LoadJson(json, out _, out _);

            Assert.AreEqual(0, Lifetime(data, CounterKey.PlayMinutes), "새 플레이어는 지금부터 센다");
            Assert.IsFalse(data.meta.playMinutesSeedPending);
            Assert.IsTrue(data.meta.codex.backfillPending, "도감 채우기 예약은 튜토리얼과 상관없이");
        }

        [Test]
        public void V3Save_KeepsLargerPlayMinutes()
        {
            var json = ReadFixture();
            json["meta"]["counters"]["lifetime"]["PlayMinutes"] = 500;

            var data = LoadJson(json, out _, out _);

            Assert.AreEqual(500, Lifetime(data, CounterKey.PlayMinutes), "이미 더 크면 줄이지 않는다");
        }

        [Test]
        public void ReSave_WritesVersion4_AndReloadDoesNotMigrateAgain()
        {
            var data = LoadJson(ReadFixture(), out var storage, out string path);
            long seeded = Lifetime(data, CounterKey.PlayMinutes);

            storage.Save(data);
            var saved = JObject.Parse(File.ReadAllText(path));
            Assert.AreEqual(4, (int)saved["version"]);
            Assert.IsNotNull(saved["meta"]?["codex"], "4차 칸이 저장된다");
            Assert.AreEqual(true, (bool)saved["meta"]["codex"]["backfillPending"]);
            Assert.AreEqual(true, (bool)saved["meta"]["playMinutesSeedPending"], "둘째 단계 예약도 저장된다 (MetaSession.Start 전에 꺼져도)");
            Assert.IsNotNull(saved["battle"]?["lampPolicy"]);
            Assert.IsNotNull(saved["dungeon"]?["ticketBank"]);

            var reloaded = storage.Load();
            Assert.AreEqual(SaveLoadResult.Loaded, storage.LastLoadResult);
            Assert.AreEqual(4, reloaded.version);
            Assert.IsTrue(reloaded.meta.codex.backfillPending, "채우기 예약은 도감이 쓸 때까지 남는다");
            Assert.AreEqual(seeded, Lifetime(reloaded, CounterKey.PlayMinutes), "버전 4 세이브는 다시 옮기지 않는다 (PlayMinutes 그대로)");
        }

        [Test]
        public void V4Save_DoesNotRequestBackfillAgain()
        {
            var json = ReadFixture();
            json["version"] = 4;
            var data = LoadJson(json, out _, out _);

            Assert.IsFalse(data.meta.codex.backfillPending);
            Assert.AreEqual(0, Lifetime(data, CounterKey.PlayMinutes));
            Assert.IsFalse(data.meta.playMinutesSeedPending);
        }

        [Test]
        public void LegacySaveWithoutMeta_PastProgress_SeedsCap()
        {
            var json = ReadFixture();
            json["version"] = 2;
            json.Remove("meta");
            json["level"] = 5;

            var data = LoadJson(json, out _, out _);

            Assert.IsTrue(data.meta.ftue.done, "옛 세이브의 진행한 플레이어는 튜토리얼을 마친 것으로");
            Assert.IsTrue(data.meta.codex.backfillPending);
            Assert.AreEqual(MetaSave.PlayMinutesSeedCap, Lifetime(data, CounterKey.PlayMinutes), "설치 기록이 없으면 오래 한 세이브로 본다");
        }

        [Test]
        public void NewSave_IsVersion4_WithoutBackfill()
        {
            var data = new PlayerData();
            data.Sanitize();

            Assert.AreEqual(4, data.version);
            Assert.IsFalse(data.meta.codex.backfillPending);
            Assert.AreEqual(0, Lifetime(data, CounterKey.PlayMinutes));
            Assert.IsFalse(data.meta.playMinutesSeedPending);
        }

        [Test]
        public void Sanitize_RestoresNullSections_AndClamps()
        {
            var json = ReadFixture();
            json["version"] = 4;
            var meta = (JObject)json["meta"];
            foreach (var name in new[] { "achievements", "codex", "gachaMilestones", "events", "season", "story", "affinity", "coupons",
                         "chapterChests", "starterWeek", "dailyGift", "firefly", "cheer", "backup" })
                meta[name] = JValue.CreateNull();
            json["battle"]["lampPolicy"] = 7;
            json["dungeon"]["ticketBank"] = -5;
            json["dungeon"]["blessings"] = JValue.CreateNull();
            json["meta"]["wallet"]["friendSelectTickets"] = -2;

            var data = LoadJson(json, out _, out _);

            Assert.IsNotNull(data.meta.achievements);
            Assert.IsNotNull(data.meta.codex?.found);
            Assert.IsNotNull(data.meta.events?.instances);
            Assert.IsNotNull(data.meta.cheer);
            Assert.IsNotNull(data.meta.firefly);
            Assert.IsNotNull(data.meta.dailyGift);
            Assert.IsNotNull(data.meta.backup, "백업 알림 칸이 null이어도 Sanitize가 되살린다 (RM-2 SB-1)");
            Assert.AreEqual(0, data.battle.lampPolicy);
            Assert.AreEqual(-1, data.dungeon.ticketBank);
            Assert.IsNotNull(data.dungeon.blessings);
            Assert.AreEqual(0, data.meta.wallet.friendSelectTickets);
        }

        [Test]
        public void EventSave_DropsInstancesOlderThan60Days()
        {
            var events = new EventSave();
            events.instances["old@2026-09-01"] = new EventInstanceSave { eventId = "old", startDayKey = "2026-09-01" };
            events.instances["kept@2026-09-06"] = new EventInstanceSave { eventId = "kept", startDayKey = "2026-09-06" };
            events.instances["broken"] = null;

            events.Sanitize("2026-11-05");

            Assert.IsFalse(events.instances.ContainsKey("old@2026-09-01"), "65일 지난 회차");
            Assert.IsTrue(events.instances.ContainsKey("kept@2026-09-06"), "60일 = 남김");
            Assert.IsFalse(events.instances.ContainsKey("broken"));
            Assert.IsNotNull(events.instances["kept@2026-09-06"].baseline);
        }
    }
}
