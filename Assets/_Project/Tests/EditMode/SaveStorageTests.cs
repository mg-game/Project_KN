using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    public class SaveStorageTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "idle_save_test_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "player.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Load_WithoutFile_ReturnsNull()
        {
            Assert.IsNull(new LocalSaveStorage(_path).Load());
        }

        [Test]
        public void SaveAndLoad_RoundTrip()
        {
            var data = new PlayerData { level = 42, noticeHiddenDate = "2026-10-28", lastSaveUnix = 123456789 };
            data.currencies[CurrencyType.Gold] = 1.2345e30;
            data.currencies[CurrencyType.Gem] = 777;
            data.currencies[CurrencyType.LineageXp] = 0;
            data.items["r1"] = 3;
            data.gachaPity["normal"] = 17;
            data.readNoticeIds.Add("notice_0001");
            data.sentOnceMailKeys.Add("welcome");
            data.accountId = "acc123";
            data.linkedAccounts[AuthProviderType.Kakao] = new LinkedAccount { userId = "k1", displayName = "카카오 유저", linkedAt = 42 };
            data.mails.Add(new MailData
            {
                id = "m1",
                title = "보상",
                rewards = { RewardData.Currency(CurrencyType.Gem, 100), RewardData.Item("r1", 2) },
                expireAt = 999,
            });

            var storage = new LocalSaveStorage(_path);
            storage.Save(data);
            var loaded = storage.Load();

            Assert.AreEqual(42, loaded.level);
            Assert.AreEqual(1.2345e30, loaded.currencies[CurrencyType.Gold]);
            Assert.AreEqual(777, loaded.currencies[CurrencyType.Gem]);
            Assert.IsTrue(loaded.currencies.ContainsKey(CurrencyType.LineageXp), "실타래 키는 값이 0이어도 저장된다 (T7 첫 로드 1회 지급이 다시 돌지 않게)");
            Assert.AreEqual(3, loaded.items["r1"]);
            Assert.AreEqual(17, loaded.gachaPity["normal"]);
            Assert.IsTrue(loaded.readNoticeIds.Contains("notice_0001"));
            Assert.IsTrue(loaded.sentOnceMailKeys.Contains("welcome"));
            Assert.AreEqual("2026-10-28", loaded.noticeHiddenDate);
            Assert.AreEqual(123456789, loaded.lastSaveUnix);
            Assert.AreEqual(2, loaded.mails[0].rewards.Count);
            Assert.AreEqual(RewardType.Item, loaded.mails[0].rewards[1].type);
            Assert.AreEqual("acc123", loaded.accountId);
            Assert.AreEqual("k1", loaded.linkedAccounts[AuthProviderType.Kakao].userId);
        }

        [Test]
        public void CorruptedSave_FallsBackToBackup()
        {
            var storage = new LocalSaveStorage(_path);
            storage.Save(new PlayerData { level = 5 });
            storage.Save(new PlayerData { level = 6 });
            File.WriteAllText(_path, "{ broken json");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[Save\]"));
            var loaded = storage.Load();

            Assert.AreEqual(5, loaded.level);
        }

        [Test]
        public void Delete_RemovesAllFiles()
        {
            var storage = new LocalSaveStorage(_path);
            storage.Save(new PlayerData());
            storage.Save(new PlayerData());

            storage.Delete();

            Assert.IsNull(storage.Load());
        }

        sealed class FixedClock : ITimeProvider
        {
            public DateTime UtcNow => new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        }

        [Test]
        public void ClockConstructor_SavesAndLoadsLikeTheDefault_AndKeepsPreviousAsBak()
        {
            var storage = new LocalSaveStorage(_path, new FixedClock());
            storage.Save(new PlayerData { level = 3 });
            storage.Save(new PlayerData { level = 4 });

            Assert.AreEqual(4, storage.Load().level);
            Assert.AreEqual(SaveLoadResult.Loaded, storage.LastLoadResult);
            Assert.AreEqual(3, new LocalSaveStorage(_path + ".bak").Load().level, "직전 세이브는 .bak");
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void ISaveStorage_NewMembers_DoNotThrow_OnEmptyStorage()
        {
            ISaveStorage storage = new LocalSaveStorage(_path);

            Assert.DoesNotThrow(() => storage.SnapshotDaily("2026-11-05"));
            Assert.IsEmpty(storage.ListBackups());
            Assert.IsFalse(storage.RestoreBackup(_path + ".bak"));
            Assert.IsTrue(storage.Replace(new PlayerData { level = 2 }, "import"));
            Assert.AreEqual(2, storage.Load().level);
        }

        // ── RI-7: 일시정지 중에는 자동 저장 타이머가 멈춘다 (GameBootstrap.ShouldAutoSave) ──

        [Test]
        public void ShouldAutoSave_CountsUpToTheInterval_ThenSaves()
        {
            float timer = 0f;

            Assert.IsFalse(GameBootstrap.ShouldAutoSave(ref timer, 10f, 30f, paused: false));
            Assert.IsFalse(GameBootstrap.ShouldAutoSave(ref timer, 19.5f, 30f, paused: false));
            Assert.AreEqual(29.5f, timer, 1e-4f);
            Assert.IsTrue(GameBootstrap.ShouldAutoSave(ref timer, 0.5f, 30f, paused: false), "30초가 되면 저장할 차례");
        }

        [Test]
        public void ShouldAutoSave_WhilePaused_FreezesTheTimer_AndNeverSaves()
        {
            float timer = 29f;

            for (int frame = 0; frame < 120; frame++)
                Assert.IsFalse(GameBootstrap.ShouldAutoSave(ref timer, 3600f, 30f, paused: true), "일시정지 중에는 저장하지 않는다");
            Assert.AreEqual(29f, timer, "일시정지 중에는 타이머가 흐르지 않는다");

            Assert.IsFalse(GameBootstrap.ShouldAutoSave(ref timer, 0.5f, 30f, paused: false));
            Assert.IsTrue(GameBootstrap.ShouldAutoSave(ref timer, 0.5f, 30f, paused: false), "복귀 뒤에는 남은 만큼만 더 흐르면 저장");
        }

        [Test]
        public void ShouldAutoSave_PauseThenResume_FirstAutoSaveComesAFullIntervalAfterResume()
        {
            // GameBootstrap: 일시정지 → Save()가 타이머를 0으로 → 80시간 동안 프레임이 돌아도 (가짜 시계 캡처) 저장 없음 → 복귀
            float timer = 0f;
            int saves = 0;
            for (int frame = 0; frame < 600; frame++)
            {
                if (GameBootstrap.ShouldAutoSave(ref timer, 0.5f, 30f, paused: true)) saves++;
            }
            for (int frame = 0; frame < 59; frame++)
            {
                if (GameBootstrap.ShouldAutoSave(ref timer, 0.5f, 30f, paused: false)) saves++;
            }
            Assert.AreEqual(0, saves, "복귀 뒤 29.5초까지 자동 저장 없음 — lastSaveUnix가 일시정지 전 값으로 남는다");
            Assert.IsTrue(GameBootstrap.ShouldAutoSave(ref timer, 0.5f, 30f, paused: false));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void ShouldAutoSave_IgnoresNonPositiveOrNaNDelta(float dt)
        {
            float timer = 12f;

            Assert.IsFalse(GameBootstrap.ShouldAutoSave(ref timer, dt, 30f, paused: false));
            Assert.AreEqual(12f, timer);
        }

        // ── perf A5(a): 부팅 예열 (LocalSaveStorage.Warm) — 쓰지 않고, 작업 스레드 · 계약을 미리 ──

        [Test]
        public void Warm_DoesNotWriteAnyFile_AndTheWarmWriterFinishes()
        {
            var storage = new LocalSaveStorage(_path);

            storage.Warm(new PlayerData { level = 9 });

            Assert.IsTrue(storage.FlushPending(), "예열 작업 스레드는 줄 선 저장이 없으면 바로 끝난다");
            Assert.IsFalse(storage.IsWriting);
            Assert.IsFalse(Directory.Exists(_dir) && Directory.GetFiles(_dir).Length > 0, "예열은 파일을 쓰지 않는다");
        }

        [Test]
        public void Warm_ThenBackgroundSave_WritesTheNewestSave()
        {
            var storage = new LocalSaveStorage(_path);
            storage.Warm(new PlayerData { level = 1 });

            storage.SaveInBackground(new PlayerData { level = 7 });
            Assert.IsTrue(storage.FlushPending());

            Assert.AreEqual(7, storage.Load().level, "예열 중에 줄 선 자동 저장도 그 작업 스레드가 이어서 쓴다");
        }

        [Test]
        public void Warm_Null_AndTwice_DoNotThrow()
        {
            var storage = new LocalSaveStorage(_path);

            Assert.DoesNotThrow(() => storage.Warm(null));
            Assert.DoesNotThrow(() => storage.Warm(new PlayerData()));
            Assert.IsTrue(storage.FlushPending());
            Assert.IsNull(storage.Load(), "아직 저장한 적이 없다");
        }

        [Test]
        public void WarmContracts_PlayerData_ReachesNestedSaveTypes_AndNeverThrows()
        {
            int types = LocalSaveStorage.WarmContracts(typeof(PlayerData));

            Assert.Greater(types, 10, "PlayerData → 목록 · 사전 항목 형식까지 따라간다");
            Assert.LessOrEqual(types, LocalSaveStorage.WarmContractsMaxTypes);
            Assert.AreEqual(0, LocalSaveStorage.WarmContracts(null));
            Assert.AreEqual(1, LocalSaveStorage.WarmContracts(typeof(PlayerData), maxTypes: 1));
        }
    }
}
