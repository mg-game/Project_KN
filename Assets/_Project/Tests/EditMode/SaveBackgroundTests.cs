using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// perf PF-3: 자동 저장의 파일 쓰기를 백그라운드로 (LocalSaveStorage.SaveInBackground). 같은 바이트 · 가장 새 진행이 이긴다 ·
    /// 바꾸기(가져오기) · 지우기 뒤에 줄 서 있던 오래된 자동 저장이 덮지 않는다 · 직렬화 버퍼를 다시 써도 JsonConvert와 같은 글자
    /// </summary>
    public class SaveBackgroundTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "idle_save_bg_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "player.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static PlayerData Sample(int level)
        {
            var data = new PlayerData { level = level, lastSaveUnix = 1000 + level, accountId = "acc" + level };
            data.currencies[CurrencyType.Gold] = 1.25e9 * level;
            data.items["r1"] = level;
            return data;
        }

        static int LevelIn(string path) => JsonConvert.DeserializeObject<PlayerData>(File.ReadAllText(path)).level;

        [Test]
        public void Serialize_EqualsJsonConvert_AndReusesBuffer()
        {
            var storage = new LocalSaveStorage(_path);
            var big = Sample(7);
            var small = new PlayerData { level = 1 };
            Assert.AreEqual(JsonConvert.SerializeObject(big), storage.Serialize(big));
            Assert.AreEqual(JsonConvert.SerializeObject(small), storage.Serialize(small), "큰 세이브 뒤 작은 세이브 — 버퍼에 남은 글자가 섞이지 않는다");
            Assert.AreEqual(JsonConvert.SerializeObject(big), storage.Serialize(big));
        }

        [Test]
        public void SaveInBackground_ThenFlush_SameBytesAsSave()
        {
            var data = Sample(3);
            string syncPath = Path.Combine(_dir, "sync", "player.json");
            new LocalSaveStorage(syncPath).Save(data);
            var storage = new LocalSaveStorage(_path);
            storage.SaveInBackground(data);
            storage.FlushPending();
            CollectionAssert.AreEqual(File.ReadAllBytes(syncPath), File.ReadAllBytes(_path));
        }

        [Test]
        public void SaveInBackground_KeepsPreviousSaveAsBackup()
        {
            var storage = new LocalSaveStorage(_path);
            storage.Save(Sample(1));
            storage.SaveInBackground(Sample(2));
            storage.FlushPending();
            Assert.AreEqual(2, storage.Load().level);
            Assert.AreEqual(1, LevelIn(_path + ".bak"), "직전 세이브는 .bak (예전과 같은 규칙)");
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void SaveInBackground_ManySnapshots_NewestWins()
        {
            var storage = new LocalSaveStorage(_path);
            for (int i = 1; i <= 40; i++) storage.SaveInBackground(Sample(i));
            storage.FlushPending();
            Assert.AreEqual(40, storage.Load().level);
        }

        [Test]
        public void SyncSave_AfterPendingBackground_Wins()
        {
            var storage = new LocalSaveStorage(_path);
            storage.SaveInBackground(Sample(2));
            storage.Save(Sample(3));
            storage.FlushPending();
            Assert.AreEqual(3, storage.Load().level, "일시정지 · 종료 저장(동기)이 줄 서 있던 자동 저장보다 새것");
        }

        [Test]
        public void Replace_AfterPendingBackground_KeepsImported()
        {
            var storage = new LocalSaveStorage(_path);
            storage.Save(Sample(1));
            storage.SaveInBackground(Sample(2));
            Assert.IsTrue(storage.Replace(Sample(9), LocalSaveStorage.ImportReason));
            storage.FlushPending();
            Assert.AreEqual(9, storage.Load().level, "가져온 세이브를 오래된 자동 저장이 덮지 않는다");
        }

        [Test]
        public void Delete_AfterPendingBackground_LeavesNoMain()
        {
            var storage = new LocalSaveStorage(_path);
            storage.SaveInBackground(Sample(2));
            storage.Delete();
            storage.FlushPending();
            Assert.IsFalse(File.Exists(_path), "지운 뒤 줄 서 있던 자동 저장이 세이브를 다시 만들지 않는다 (진행 초기화)");
        }

        [Test]
        public void SaveInBackground_WithDayKey_WritesDailyBackup()
        {
            var storage = new LocalSaveStorage(_path);
            storage.SaveInBackground(Sample(4), "2026-11-05");
            storage.FlushPending();
            Assert.IsTrue(File.Exists(_path + ".day-2026-11-05.bak"));
            Assert.AreEqual(4, LevelIn(_path + ".day-2026-11-05.bak"));
        }

        [Test]
        public void SaveInBackground_BadDayKey_IsIgnored()
        {
            var storage = new LocalSaveStorage(_path);
            storage.SaveInBackground(Sample(4), "not-a-day");
            storage.FlushPending();
            Assert.AreEqual(4, storage.Load().level);
            Assert.AreEqual(0, Directory.GetFiles(_dir, "player.json.day-*").Length);
        }
    }
}
