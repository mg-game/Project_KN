using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 세이브 복구 테스트 (씬 없이 돈다): 다른 버전이 저장한 모르는 열거형 키는 건너뛰고,
    /// 읽지 못한 세이브/백업은 격리해 이후 저장이 원본을 덮어쓰지 않는지 확인한다.
    /// </summary>
    public class SaveRecoveryTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "idle_save_recovery_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "player.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Load_WithoutFiles_ReportsNotFound()
        {
            var storage = new LocalSaveStorage(_path);

            Assert.IsNull(storage.Load());
            Assert.AreEqual(SaveLoadResult.NotFound, storage.LastLoadResult);
        }

        [Test]
        public void UnknownEnumKeys_AreSkipped_RestOfSaveLoads()
        {
            // 롤백/구버전 빌드가 모르는 키: CurrencyType "RetiredToken", AuthProviderType "Apple", ProgressType "DungeonClearCount"
            File.WriteAllText(_path,
                "{\"level\":12," +
                "\"currencies\":{\"Gold\":500,\"RetiredToken\":3,\"Gem\":40}," +
                "\"linkedAccounts\":{\"Apple\":{\"userId\":\"a1\"},\"Kakao\":{\"userId\":\"k1\"}}," +
                "\"lineages\":{\"jjakjjak\":{\"level\":7,\"progress\":{\"GuardianKillCount\":40,\"DungeonClearCount\":{\"nested\":[1,2]},\"ExpeditionCount\":3}}}}");

            var storage = new LocalSaveStorage(_path);
            var loaded = storage.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveLoadResult.Loaded, storage.LastLoadResult);
            Assert.AreEqual(12, loaded.level);
            Assert.AreEqual(2, loaded.currencies.Count);
            Assert.AreEqual(500, loaded.currencies[CurrencyType.Gold], 1e-9);
            Assert.AreEqual(40, loaded.currencies[CurrencyType.Gem], 1e-9);
            Assert.AreEqual(1, loaded.linkedAccounts.Count);
            Assert.AreEqual("k1", loaded.linkedAccounts[AuthProviderType.Kakao].userId);
            var progress = loaded.lineages["jjakjjak"].progress;
            Assert.AreEqual(2, progress.Count);
            Assert.AreEqual(40, progress[ProgressType.GuardianKillCount]);
            Assert.AreEqual(3, progress[ProgressType.ExpeditionCount]);
            Assert.AreEqual(7, loaded.lineages["jjakjjak"].level);
        }

        [Test]
        public void EnumKeyDictionaries_RoundTrip_IncludingUndefinedNumericKeys()
        {
            var data = new PlayerData();
            data.currencies[CurrencyType.EventToken] = 77;
            data.currencies[(CurrencyType)99] = 5;
            data.lineages["jjakjjak"] = new LineageSave();
            data.lineages["jjakjjak"].progress[ProgressType.EchoDeployCount] = 30;

            var storage = new LocalSaveStorage(_path);
            storage.Save(data);
            var loaded = storage.Load();

            Assert.AreEqual(77, loaded.currencies[CurrencyType.EventToken], 1e-9);
            Assert.AreEqual(5, loaded.currencies[(CurrencyType)99], 1e-9);
            Assert.AreEqual(30, loaded.lineages["jjakjjak"].progress[ProgressType.EchoDeployCount]);
        }

        [Test]
        public void UnreadableSaveAndBackup_AreQuarantined_AndSurviveLaterSaves()
        {
            const string brokenMain = "{ \"level\": broken main";
            const string brokenBackup = "{ \"level\": broken backup";
            File.WriteAllText(_path, brokenMain);
            File.WriteAllText(_path + ".bak", brokenBackup);

            var storage = new LocalSaveStorage(_path);
            Assert.IsNull(storage.Load());
            Assert.AreEqual(SaveLoadResult.Unreadable, storage.LastLoadResult);

            // 새 게임으로 두 번 저장해도 (기존: 첫 저장에 main → .bak, 두 번째에 .bak 덮어씀) 원본이 남아 있어야 한다
            storage.Save(new PlayerData { level = 2 });
            storage.Save(new PlayerData { level = 3 });

            var kept = Directory.GetFiles(_dir, "*.corrupt-*").Select(File.ReadAllText).ToList();
            Assert.AreEqual(2, kept.Count);
            CollectionAssert.Contains(kept, brokenMain);
            CollectionAssert.Contains(kept, brokenBackup);
            Assert.AreEqual(3, storage.Load().level);
        }

        [Test]
        public void CorruptMain_RecoversFromBackup_AndKeepsGoodBackup()
        {
            var storage = new LocalSaveStorage(_path);
            storage.Save(new PlayerData { level = 5 });
            storage.Save(new PlayerData { level = 6 });
            File.WriteAllText(_path, "{ broken json");

            var loaded = storage.Load();
            Assert.AreEqual(5, loaded.level);
            Assert.AreEqual(SaveLoadResult.RecoveredFromBackup, storage.LastLoadResult);

            // 다음 저장이 깨진 main을 .bak으로 복사해 정상 백업을 덮으면 안 된다
            storage.Save(new PlayerData { level = 7 });
            Assert.AreEqual(5, new LocalSaveStorage(_path + ".bak").Load().level);
            Assert.AreEqual(1, Directory.GetFiles(_dir, "*.corrupt-*").Length);
        }

        [Test]
        public void Delete_KeepsQuarantinedFiles()
        {
            File.WriteAllText(_path, "{ broken json");
            var storage = new LocalSaveStorage(_path);
            storage.Load();
            storage.Save(new PlayerData());

            storage.Delete();

            Assert.IsNull(storage.Load());
            Assert.AreEqual(1, Directory.GetFiles(_dir, "*.corrupt-*").Length);
        }
    }

    /// <summary>접속 시 차례로 띄울 팝업 공지 목록</summary>
    public class StartupNoticeListTests
    {
        sealed class FixedClock : ITimeProvider
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc);
        }

        static NoticeData MakeNotice(string id, int order, bool popup) =>
            new NoticeData { id = id, title = id, date = "2026-11-03", order = order, popup = popup };

        [Test]
        public void GetStartupNotices_ReturnsEveryPopupNotice_InSortOrder()
        {
            var notice = new NoticeSystem(new PlayerData(), new FixedClock());
            notice.SetNotices(new[]
            {
                MakeNotice("event", 90, true),
                MakeNotice("guide", 80, false),
                MakeNotice("open", 100, true),
            });

            var startup = notice.GetStartupNotices();

            CollectionAssert.AreEqual(new[] { "open", "event" }, startup.Select(n => n.id).ToArray());
            Assert.AreEqual("open", notice.GetStartupNotice().id);
        }

        [Test]
        public void GetStartupNotices_EmptyWhileHiddenToday_BackNextDay()
        {
            var clock = new FixedClock();
            var notice = new NoticeSystem(new PlayerData(), clock);
            notice.SetNotices(new[] { MakeNotice("open", 100, true), MakeNotice("event", 90, true) });

            notice.HideForToday();
            Assert.IsEmpty(notice.GetStartupNotices());

            clock.UtcNow += TimeSpan.FromDays(1);
            Assert.AreEqual(2, notice.GetStartupNotices().Count);
        }
    }
}
