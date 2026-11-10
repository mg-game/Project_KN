using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 세이브 백업 (SAVE-01, SPEC4 §4.2): Replace가 바꾸기 전 백업을 남기고 목록에 보이는지, SnapshotDaily가 새것 3개만 두는지,
    /// RestoreBackup 왕복, 목록 이름 · 정렬, 읽지 못한 파일 · 다른 경로 거절. 새 기능은 예외를 내지 않는다.
    /// </summary>
    public class SaveBackupTests
    {
        sealed class FixedClock : ITimeProvider
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);
        }

        string _dir;
        string _path;
        FixedClock _clock;
        LocalSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "idle_save_backup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "player.json");
            _clock = new FixedClock();
            _storage = new LocalSaveStorage(_path, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static PlayerData Save(int level, long savedUnix, int highestStage = 1)
        {
            var data = new PlayerData { level = level, lastSaveUnix = savedUnix, accountId = "acc-0001-xyz" };
            data.battle.highestStage = highestStage;
            return data;
        }

        [Test]
        public void Replace_WritesBackupOfCurrent_ListedAsBeforeImport_ThenSavesNewData()
        {
            _storage.Save(Save(10, 1000, highestStage: 14));

            bool replaced = _storage.Replace(Save(55, 2000, highestStage: 40), LocalSaveStorage.ImportReason);

            Assert.IsTrue(replaced);
            Assert.AreEqual(55, _storage.Load().level, "새 진행으로 바뀐다");
            long importUnix = TimeUtil.ToUnix(_clock.UtcNow);
            Assert.IsTrue(File.Exists($"{_path}.import-{importUnix}.bak"), "player.json.import-{unix}.bak");

            var backups = _storage.ListBackups();
            var import = backups.Single(b => b.Kind == SaveBackupKind.BeforeReplace);
            Assert.AreEqual(LocalSaveStorage.LabelBeforeImport, import.Label);
            Assert.AreEqual(10, import.Summary.shopLevel);
            Assert.AreEqual(14, import.Summary.highestStage);
            Assert.AreEqual(1000, import.SavedUnix);
            Assert.AreEqual("acc-0001", import.Summary.accountIdShort);
        }

        [Test]
        public void Replace_WithoutCurrentSave_JustSaves()
        {
            Assert.IsTrue(_storage.Replace(Save(3, 100), "import"));

            Assert.AreEqual(3, _storage.Load().level);
            Assert.IsFalse(_storage.ListBackups().Any(b => b.Kind == SaveBackupKind.BeforeReplace));
        }

        [Test]
        public void Replace_Null_IsRefused()
        {
            _storage.Save(Save(4, 100));

            Assert.IsFalse(_storage.Replace(null, "import"));
            Assert.AreEqual(4, _storage.Load().level);
        }

        [Test]
        public void Replace_BackupFails_ReturnsFalse_AndKeepsTheCurrentSave()
        {
            // 바꾸기 전 백업 자리를 폴더로 막아 복사가 실패하게 한다. Replace는 예외 없이 false — 부르는 쪽(SaveImportPopup)은 false면 다시 불러오지 않는다
            _storage.Save(Save(4, 100));
            Directory.CreateDirectory($"{_path}.import-{TimeUtil.ToUnix(_clock.UtcNow)}.bak");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[Save\]"));

            bool replaced = true;
            Assert.DoesNotThrow(() => replaced = _storage.Replace(Save(9, 900), LocalSaveStorage.ImportReason));

            Assert.IsFalse(replaced);
            Assert.AreEqual(4, _storage.Load().level, "지금 진행은 그대로");
            Assert.IsFalse(File.Exists($"{_path}.tmp"), "새 진행을 쓰기 시작하지 않았다");
        }

        [Test]
        public void Replace_UnsafeReason_FallsBackToImport()
        {
            _storage.Save(Save(4, 100));

            Assert.IsTrue(_storage.Replace(Save(5, 200), "../evil name"));

            Assert.AreEqual(1, Directory.GetFiles(_dir, "player.json.import-*.bak").Length);
            Assert.IsEmpty(Directory.GetFiles(_dir, "*evil*"));
        }

        [Test]
        public void Replace_KeepsNewestThreeBackupsPerReason()
        {
            _storage.Save(Save(1, 100));
            for (int i = 0; i < 5; i++)
            {
                _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
                Assert.IsTrue(_storage.Replace(Save(2 + i, 200 + i), "import"));
            }

            var files = Directory.GetFiles(_dir, "player.json.import-*.bak");
            Assert.AreEqual(LocalSaveStorage.ReplaceBackupsKept, files.Length);
        }

        [Test]
        public void Replace_TwiceInOneSecond_KeepsBoth()
        {
            _storage.Save(Save(1, 100));
            _storage.Replace(Save(2, 200), "import");
            _storage.Replace(Save(3, 300), "import");

            Assert.AreEqual(2, _storage.ListBackups().Count(b => b.Kind == SaveBackupKind.BeforeReplace));
        }

        [Test]
        public void SnapshotDaily_KeepsNewestThree_AndOncePerDay()
        {
            string[] days = { "2026-11-01", "2026-11-02", "2026-11-03", "2026-11-04", "2026-11-05" };
            for (int i = 0; i < days.Length; i++)
            {
                _storage.Save(Save(10 + i, 1000 + i));
                _storage.SnapshotDaily(days[i]);
            }
            // 같은 날 두 번째 저장은 그 날 백업을 바꾸지 않는다
            _storage.Save(Save(99, 9999));
            _storage.SnapshotDaily("2026-11-05");

            var dailies = _storage.ListBackups().Where(b => b.Kind == SaveBackupKind.Daily).ToList();
            Assert.AreEqual(LocalSaveStorage.DailySnapshotsKept, dailies.Count);
            CollectionAssert.AreEqual(new[] { 14, 13, 12 }, dailies.Select(b => b.Summary.shopLevel).ToArray(), "새것부터 · 11-05 백업은 첫 저장(14)");
            CollectionAssert.AreEqual(new[] { "오늘", "어제", "그저께" }, dailies.Select(b => b.Label).ToArray());
            Assert.IsFalse(File.Exists($"{_path}.day-2026-11-01.bak"));
            Assert.IsFalse(File.Exists($"{_path}.day-2026-11-02.bak"));
        }

        [Test]
        public void SnapshotDaily_IgnoresBadKey_AndMissingSave()
        {
            _storage.SnapshotDaily("2026-11-05");
            Assert.IsEmpty(_storage.ListBackups(), "세이브가 없으면 남길 것이 없다");

            _storage.Save(Save(1, 100));
            _storage.SnapshotDaily("../../x");
            _storage.SnapshotDaily(null);
            _storage.SnapshotDaily("2026-13-40");

            Assert.IsEmpty(Directory.GetFiles(_dir, "*.day-*"));
        }

        [Test]
        public void RestoreBackup_RoundTrip_AndKeepsCurrentAsImportBackup()
        {
            _storage.Save(Save(7, 700));
            _storage.SnapshotDaily("2026-11-05");
            _storage.Save(Save(8, 800));
            _clock.UtcNow = _clock.UtcNow.AddMinutes(5);

            var daily = _storage.ListBackups().Single(b => b.Kind == SaveBackupKind.Daily);
            Assert.IsTrue(_storage.RestoreBackup(daily.Path));

            Assert.AreEqual(7, _storage.Load().level, "백업 내용으로 돌아간다");
            var import = _storage.ListBackups().Single(b => b.Kind == SaveBackupKind.BeforeReplace);
            Assert.AreEqual(8, import.Summary.shopLevel, "되돌리기 전 진행은 따로 남는다");
            Assert.IsTrue(File.Exists(daily.Path), "복구에 쓴 백업은 그대로 남는다");
        }

        [Test]
        public void RestoreBackup_FromLastSaveBak_Works()
        {
            _storage.Save(Save(1, 100));
            _storage.Save(Save(2, 200));

            var last = _storage.ListBackups().Single(b => b.Kind == SaveBackupKind.LastSave);
            Assert.AreEqual(LocalSaveStorage.LabelLastSave, last.Label);
            Assert.IsTrue(_storage.RestoreBackup(last.Path));

            Assert.AreEqual(1, _storage.Load().level);
        }

        [Test]
        public void RestoreBackup_RefusesForeignUnreadableOrMissingFiles()
        {
            _storage.Save(Save(5, 500));
            string foreign = Path.Combine(Path.GetTempPath(), "player.json.bak");
            string unreadable = $"{_path}.day-2026-11-04.bak";
            File.WriteAllText(unreadable, "{ broken");
            string quarantined = $"{_path}.corrupt-123";
            File.WriteAllText(quarantined, "{}");

            Assert.IsFalse(_storage.RestoreBackup(foreign));
            Assert.IsFalse(_storage.RestoreBackup(unreadable));
            Assert.IsFalse(_storage.RestoreBackup(quarantined));
            Assert.IsFalse(_storage.RestoreBackup($"{_path}.day-2026-10-01.bak"));
            Assert.IsFalse(_storage.RestoreBackup(_path));
            Assert.IsFalse(_storage.RestoreBackup(null));
            Assert.IsFalse(_storage.RestoreBackup(""));
            Assert.AreEqual(5, _storage.Load().level, "거절하면 아무것도 바뀌지 않는다");
            Assert.IsEmpty(Directory.GetFiles(_dir, "player.json.import-*.bak"));
        }

        [Test]
        public void ListBackups_SkipsUnreadable_IgnoresOtherFiles_NewestFirst()
        {
            _storage.Save(Save(1, 100));
            _storage.SnapshotDaily("2026-11-03");
            _storage.Save(Save(2, 300));
            File.WriteAllText($"{_path}.day-2026-11-04.bak", "not json");
            File.WriteAllText($"{_path}.corrupt-55", "{}");
            File.WriteAllText(Path.Combine(_dir, "other.json.bak"), "{}");
            File.WriteAllText($"{_path}.tmp", "{}");

            var backups = _storage.ListBackups();

            Assert.AreEqual(2, backups.Count, "직전 .bak + 11-03 백업 (읽지 못한 11-04 · 다른 파일은 뺀다)");
            Assert.AreEqual(SaveBackupKind.LastSave, backups[0].Kind);
            Assert.AreEqual(SaveBackupKind.Daily, backups[1].Kind);
            Assert.GreaterOrEqual(backups[0].SavedUnix, backups[1].SavedUnix);
        }

        [Test]
        public void ListBackups_EmptyDirectory_IsEmpty()
        {
            Assert.IsEmpty(_storage.ListBackups());
            Assert.IsEmpty(new LocalSaveStorage(Path.Combine(_dir, "missing", "player.json")).ListBackups());
        }

        [Test]
        public void Delete_RemovesDailyAndImportBackups_KeepsQuarantineAndOtherFiles()
        {
            // 초기화 확인 글: '모든 진행 상황이 삭제되고 처음부터 시작해요. 되돌릴 수 없어요' — 백업도 남기지 않는다
            _storage.Save(Save(6, 600));
            _storage.SnapshotDaily("2026-11-04");
            _storage.SnapshotDaily("2026-11-05");
            _storage.Replace(Save(7, 700), "import");
            _storage.Replace(Save(8, 800), "restore_test");
            string quarantined = $"{_path}.corrupt-123";
            File.WriteAllText(quarantined, "{}");
            string other = Path.Combine(_dir, "other.json.day-2026-11-05.bak");
            File.WriteAllText(other, "{}");
            Assert.AreEqual(5, _storage.ListBackups().Count, "직전 .bak + 날마다 2 + 바꾸기 전 2");

            _storage.Delete();

            Assert.IsNull(_storage.Load());
            Assert.IsEmpty(_storage.ListBackups(), "초기화 뒤 '백업에서 복구'에 지난 진행이 없다");
            Assert.IsEmpty(Directory.GetFiles(_dir, "player.json*.bak"));
            Assert.IsTrue(File.Exists(quarantined), "격리해 둔 손상 파일은 남긴다 (SaveRecoveryTests와 같은 규칙)");
            Assert.IsTrue(File.Exists(other), "다른 세이브의 파일은 건드리지 않는다");
        }

        [Test]
        public void Delete_ThenFirstSaveOfTheSameDay_TakesAFreshDailySnapshot()
        {
            _storage.Save(Save(6, 600));
            _storage.SnapshotDaily("2026-11-05");
            _storage.Delete();

            _storage.Save(Save(1, 900));
            _storage.SnapshotDaily("2026-11-05");

            var daily = _storage.ListBackups().Single(b => b.Kind == SaveBackupKind.Daily);
            Assert.AreEqual(1, daily.Summary.shopLevel, "오늘 칸이 초기화 전 세이브로 막혀 있지 않다");
        }

        [Test]
        public void Delete_WithoutAnyFiles_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _storage.Delete());
            Assert.DoesNotThrow(() => new LocalSaveStorage(Path.Combine(_dir, "missing", "player.json")).Delete());
        }

        // ── RI-8: 종료 전에 백그라운드 자동 저장을 기다린다 (LocalSaveStorage.FlushPending, GameBootstrap.OnApplicationQuit) ──

        [Test]
        public void FlushPending_WithoutAnyBackgroundSave_ReturnsTrueAtOnce()
        {
            Assert.IsTrue(_storage.FlushPending());
            Assert.IsTrue(_storage.FlushPending(GameBootstrap.QuitFlushTimeoutMs));
            Assert.IsTrue(_storage.FlushPending(0));
        }

        [Test]
        public void FlushPending_AfterBackgroundSave_WaitsForTheWrite_AndItsDailyBackup()
        {
            _storage.Save(Save(5, 500));

            _storage.SaveInBackground(Save(6, 600), "2026-11-05");
            Assert.IsTrue(_storage.FlushPending(LocalSaveStorage.DefaultFlushTimeoutMs), "기다린 뒤에는 쓰는 중인 작업이 없다");

            Assert.IsFalse(_storage.IsWriting);
            Assert.AreEqual(6, _storage.Load().level);
            Assert.AreEqual(5, new LocalSaveStorage(_path + ".bak").Load().level, "직전 세이브는 .bak");
            Assert.IsTrue(File.Exists($"{_path}.day-2026-11-05.bak"), "날마다 백업도 기다린 뒤에 남아 있다");
        }

        [Test]
        public void QuitSequence_SyncSaveThenFlush_LeavesTheNewestProgress_AndNoWriterRunning()
        {
            // OnApplicationQuit: Save() (동기) → FlushBackgroundSave() — 줄 서 있던 더 오래된 자동 저장은 본 파일을 덮지 않는다
            _storage.Save(Save(1, 100));
            for (int i = 2; i <= 20; i++) _storage.SaveInBackground(Save(i, 100 + i), "2026-11-05");
            _storage.Save(Save(99, 999));

            Assert.IsTrue(_storage.FlushPending(GameBootstrap.QuitFlushTimeoutMs * 4));

            Assert.IsFalse(_storage.IsWriting, "종료 뒤에 파일을 만지는 작업 스레드가 남지 않는다");
            Assert.AreEqual(99, _storage.Load().level, "종료 저장이 가장 새 진행");
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void FlushPending_OutOfRangeTimeouts_DoNotThrow(int timeoutMs)
        {
            _storage.SaveInBackground(Save(3, 300));

            Assert.DoesNotThrow(() => _storage.FlushPending(timeoutMs));
            Assert.IsTrue(_storage.FlushPending(), "그 뒤 기본 대기로 마저 끝난다");
            Assert.AreEqual(3, _storage.Load().level);
        }

        [TestCase("2026-11-05", "2026-11-05", "오늘")]
        [TestCase("2026-11-04", "2026-11-05", "어제")]
        [TestCase("2026-11-03", "2026-11-05", "그저께")]
        [TestCase("2026-10-28", "2026-11-05", "10월 28일")]
        [TestCase("2026-12-31", "2027-01-01", "어제")]
        [TestCase("2026-11-04", null, "11월 4일")]
        [TestCase("bad", "2026-11-05", "bad")]
        public void DayLabel_ByDistanceFromToday(string dayKey, string todayKey, string expected)
        {
            Assert.AreEqual(expected, LocalSaveStorage.DayLabel(dayKey, todayKey));
        }
    }
}
