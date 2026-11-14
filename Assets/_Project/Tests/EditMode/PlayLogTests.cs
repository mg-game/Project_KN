using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGame.Tests
{
    /// <summary>
    /// 플레이 기록 (CR-09, SPEC4 §4.4): 줄 형식이 올바른 JSON인지, Flush 전에는 쓰지 않고 멈춤(Flush) · 10초(Tick)에 쓰는지,
    /// 크기 한도에서 playlog.1.jsonl로 돌리는지, 쓸 수 없는 경로면 경고 뒤 꺼지고 예외를 내지 않는지, 요약 · 내보내기.
    /// </summary>
    public class PlayLogTests
    {
        sealed class FixedClock : ITimeProvider
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        }

        string _dir;
        FixedClock _clock;

        string LogPath => Path.Combine(_dir, PlayLog.FileName);
        string RotatedPath => Path.Combine(_dir, PlayLog.RotatedFileName);

        [SetUp]
        public void SetUp()
        {
            PlayLog.Close();
            _dir = Path.Combine(Path.GetTempPath(), "idle_playlog_" + Guid.NewGuid().ToString("N"));
            _clock = new FixedClock();
        }

        [TearDown]
        public void TearDown()
        {
            PlayLog.Close();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        string[] FileLines(string path) => File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).ToArray() : new string[0];

        [Test]
        public void Lines_AreValidJson_WithEnvelope_AndEscapedText()
        {
            Assert.IsTrue(PlayLog.Open(_dir, _clock));

            PlayLog.Begin("stage_end")
                .Int("stage", 12)
                .Str("res", "clear")
                .Int("pow", 1234567890123L)
                .Int("neg", -45)
                .Num("sec", 34.567, 1)
                .Num("tiny", -0.004, 2)
                .Bool("double", true)
                .Ints("dir", new[] { 0, 5, 10, 200, 3 })
                .End();
            PlayLog.Begin("note").Str("text", "따옴표 \" 역슬래시 \\ 줄\n탭\t제어\u0001 끝").Str("none", null).End();
            PlayLog.Flush();

            var lines = FileLines(LogPath);
            Assert.AreEqual(2, lines.Length);
            var first = JObject.Parse(lines[0]);
            Assert.AreEqual(TimeUtil.ToUnix(_clock.UtcNow), first.Value<long>("ts"));
            Assert.AreEqual("stage_end", first.Value<string>("ev"));
            Assert.AreEqual(12, first.Value<int>("stage"));
            Assert.AreEqual("clear", first.Value<string>("res"));
            Assert.AreEqual(1234567890123L, first.Value<long>("pow"));
            Assert.AreEqual(-45, first.Value<int>("neg"));
            Assert.AreEqual(34.6, first.Value<double>("sec"), 1e-9);
            Assert.AreEqual(0.0, first.Value<double>("tiny"), 1e-9);
            Assert.IsTrue(first.Value<bool>("double"));
            CollectionAssert.AreEqual(new[] { 0, 5, 10, 200, 3 }, first["dir"].Values<int>().ToArray());
            var second = JObject.Parse(lines[1]);
            Assert.AreEqual("따옴표 \" 역슬래시 \\ 줄\n탭\t제어\u0001 끝", second.Value<string>("text"));
            Assert.AreEqual(JTokenType.Null, second["none"].Type);
        }

        [Test]
        public void NothingIsWritten_UntilFlush_FlushOnPauseWritesAll()
        {
            PlayLog.Open(_dir, _clock);
            PlayLog.Begin("session_start").Int("stage", 1).End();
            PlayLog.Begin("session_end").Int("sec", 30).End();

            Assert.AreEqual(0, FileLines(LogPath).Length, "매 줄마다 쓰지 않는다");
            Assert.Greater(PlayLog.PendingBytes, 0);

            PlayLog.Flush(); // GameBootstrap.OnApplicationPause(true) → PlayLog.Flush

            Assert.AreEqual(2, FileLines(LogPath).Length);
            Assert.AreEqual(0, PlayLog.PendingBytes);
        }

        [Test]
        public void Tick_FlushesEveryTenSeconds()
        {
            PlayLog.Open(_dir, _clock);
            PlayLog.Begin("a").End();

            PlayLog.Tick(9.5f);
            Assert.AreEqual(0, FileLines(LogPath).Length);
            PlayLog.Tick(0.6f);
            Assert.AreEqual(1, FileLines(LogPath).Length);

            PlayLog.Begin("b").End();
            PlayLog.Tick(5f);
            Assert.AreEqual(1, FileLines(LogPath).Length, "다음 10초가 차기 전에는 쓰지 않는다");
        }

        [Test]
        public void RotatesAtSizeLimit_KeepsTwoFiles_EachUnderLimit()
        {
            const long limit = 1024;
            PlayLog.Open(_dir, _clock, limit);
            for (int i = 0; i < 60; i++) PlayLog.Begin("stage_end").Int("stage", i).Str("res", "clear").Num("sec", 31.5, 1).End();
            PlayLog.Flush();

            Assert.IsTrue(File.Exists(RotatedPath), "playlog.1.jsonl로 돌렸다");
            Assert.LessOrEqual(new FileInfo(LogPath).Length, limit);
            Assert.LessOrEqual(new FileInfo(RotatedPath).Length, limit);
            var all = FileLines(RotatedPath).Concat(FileLines(LogPath)).ToArray();
            foreach (var line in all) Assert.DoesNotThrow(() => JObject.Parse(line), line);
            Assert.AreEqual(59, JObject.Parse(all.Last()).Value<int>("stage"), "가장 새 줄은 지금 파일 끝");
            Assert.Less(all.Length, 60, "두 번 넘게 돌리면 가장 오래된 줄은 사라진다");
            var stages = all.Select(l => JObject.Parse(l).Value<int>("stage")).ToArray();
            CollectionAssert.IsOrdered(stages);
            Assert.AreEqual(Enumerable.Range(stages[0], stages.Length), stages, "남은 줄은 빠짐없이 이어진다");
        }

        [Test]
        public void ExistingFileSize_CountsTowardTheLimit()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(LogPath, new string('x', 1000) + "\n");
            PlayLog.Open(_dir, _clock, 1024);

            PlayLog.Begin("stage_end").Int("stage", 1).End();
            PlayLog.Flush();

            Assert.IsTrue(File.Exists(RotatedPath), "이미 1000바이트 있던 파일은 돌린다");
            Assert.AreEqual(1, FileLines(LogPath).Length);
        }

        [Test]
        public void UnwritableDirectory_LogsWarning_DisablesLog_NeverThrows()
        {
            Directory.CreateDirectory(_dir);
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "file, not a folder");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[PlayLog\]"));
            bool opened = PlayLog.Open(Path.Combine(blocker, "sub"), _clock);

            Assert.IsFalse(opened);
            Assert.IsFalse(PlayLog.IsOpen);
            Assert.DoesNotThrow(() =>
            {
                PlayLog.Begin("stage_end").Int("stage", 1).End();
                PlayLog.Tick(20f);
                PlayLog.Flush();
            });
            Assert.AreEqual(string.Empty, PlayLog.Summary());
            Assert.AreEqual(string.Empty, PlayLog.Tail());
        }

        [Test]
        public void NotOpen_EverythingIsANoOp()
        {
            Assert.IsFalse(PlayLog.IsOpen);
            Assert.DoesNotThrow(() => PlayLog.Begin("x").Int("a", 1).Ints("b", null).End());
            Assert.AreEqual(0, PlayLog.PendingBytes);
            Assert.AreEqual(string.Empty, PlayLog.Summary());
            Assert.IsFalse(PlayLog.Open(null));
        }

        [Test]
        public void Summary_CountsClearsAndFails_InTheWindow_PlusFtueTable()
        {
            PlayLog.Open(_dir, _clock);
            _clock.UtcNow = _clock.UtcNow.AddHours(-30);
            PlayLog.Begin("stage_end").Str("res", "clear").Num("sec", 30, 1).End(); // 24시간 밖
            _clock.UtcNow = _clock.UtcNow.AddHours(30);
            PlayLog.Begin("ftue_step").Int("n", 1).Num("t", 5, 1).End();
            PlayLog.Begin("ftue_step").Int("n", 2).Num("t", 20, 1).End();
            for (int i = 0; i < 3; i++) PlayLog.Begin("stage_end").Str("res", "clear").Num("sec", 30, 1).End();
            PlayLog.Begin("stage_end").Str("res", "fail").Num("sec", 30, 1).End();

            string summary = PlayLog.Summary(24);

            StringAssert.StartsWith("최근 24시간 · 스테이지 클리어 3 · 실패 1 · 시간당 90.0스테이지", summary);
            StringAssert.Contains("1단계  0:05  (+0:05)", summary);
            StringAssert.Contains("2단계  0:20  (+0:15)", summary);
            StringAssert.DoesNotContain("습니다", summary);
        }

        [Test]
        public void Summary_IsEmpty_WhenNothingWasLogged()
        {
            PlayLog.Open(_dir, _clock);
            Assert.AreEqual(string.Empty, PlayLog.Summary());
        }

        [Test]
        public void Tail_ReturnsTheLastLines_AcrossRotation()
        {
            PlayLog.Open(_dir, _clock, 1024);
            for (int i = 0; i < 40; i++) PlayLog.Begin("e").Int("i", i).End();

            var tail = PlayLog.Tail(5).Split('\n');

            Assert.AreEqual(5, tail.Length);
            CollectionAssert.AreEqual(new[] { 35, 36, 37, 38, 39 }, tail.Select(l => JObject.Parse(l).Value<int>("i")).ToArray());
            Assert.AreEqual(string.Empty, PlayLog.Tail(0));
        }

        [Test]
        public void Open_SameDirectoryTwice_KeepsPendingLines()
        {
            PlayLog.Open(_dir, _clock);
            PlayLog.Begin("a").End();
            Assert.IsTrue(PlayLog.Open(_dir, _clock));
            PlayLog.Close();

            Assert.AreEqual(1, FileLines(LogPath).Length, "Close가 남은 줄을 쓴다");
        }
    }

    /// <summary>플레이 기록 구독 (PlayLogRecorder): 세션 줄 · 소환 줄, 연결을 풀면 더 쓰지 않는다</summary>
    public class PlayLogRecorderTests : SystemsTestBase
    {
        string _dir;
        float _now;

        [SetUp]
        public void SetUpLog()
        {
            PlayLog.Close();
            _dir = Path.Combine(Path.GetTempPath(), "idle_playlog_rec_" + Guid.NewGuid().ToString("N"));
            PlayLog.Open(_dir, Clock);
            _now = 100f;
        }

        [TearDown]
        public void TearDownLog()
        {
            PlayLog.Close();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        List<JObject> Rows()
        {
            PlayLog.Flush();
            string path = Path.Combine(_dir, PlayLog.FileName);
            return File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).Select(JObject.Parse).ToList() : new List<JObject>();
        }

        PlayLogRecorder Make() => new(Battle, Power, Gacha, GuideQuest, IdleChest, Features, Tutorial, MetaDb, () => _now);

        [Test]
        public void Session_StartPauseResumeQuit_WritesSessionLines()
        {
            var recorder = Make();
            recorder.Connect();
            _now += 42f;
            recorder.OnPause();
            recorder.OnPause();
            recorder.OnResume();
            _now += 8f;
            recorder.OnQuit();
            recorder.Disconnect();

            var events = Rows().Select(r => r.Value<string>("ev")).ToArray();
            CollectionAssert.AreEqual(new[] { "session_start", "session_end", "session_start", "session_end" }, events);
            var rows = Rows();
            Assert.AreEqual(42, rows[1].Value<int>("sec"));
            Assert.AreEqual(8, rows[3].Value<int>("sec"));
            Assert.IsNotNull(rows[0].Value<string>("v"));
            Assert.AreEqual("editor", rows[0].Value<string>("build"));
        }

        [Test]
        public void GachaPull_WritesCountAndGrades_AndDisconnectStops()
        {
            var recorder = Make();
            recorder.Connect();
            Currency.Add(CurrencyType.Gem, 1000);

            Assert.IsTrue(Gacha.TryPull(Table, 10, out var results));

            var gacha = Rows().Single(r => r.Value<string>("ev") == "gacha");
            Assert.AreEqual(10, gacha.Value<int>("n"));
            CollectionAssert.AreEqual(results.Select(r => (int)r.Lineage.grade).ToArray(), gacha["grades"].Values<int>().ToArray());

            recorder.Disconnect();
            Gacha.TryPull(Table, 1, out _);
            Assert.AreEqual(1, Rows().Count(r => r.Value<string>("ev") == "gacha"), "연결을 풀면 쓰지 않는다");
        }

        [Test]
        public void LevelUps_WriteSpendLinesPerSink()
        {
            // SYS_Economy §3 #8 E안: 레벨업에 쓴 재화를 소비처(수선점 · 친구)별로 남긴다 (나눌지 판정 근거)
            var recorder = new PlayLogRecorder(Battle, Power, Gacha, GuideQuest, IdleChest, Features, Tutorial, MetaDb, () => _now, Growth, Characters);
            recorder.Connect();
            Currency.Add(CurrencyType.Gold, 1e6);
            Currency.Add(CurrencyType.LineageXp, 1e6);

            int from = Growth.Level;
            double shopCost = Growth.GetLevelUpCost(from, 2);
            Assert.IsTrue(Growth.TryLevelUp(2));
            var shop = Rows().Single(r => r.Value<string>("ev") == "spend" && r.Value<string>("sink") == "shop");
            Assert.AreEqual("gold", shop.Value<string>("cur"));
            Assert.AreEqual(shopCost, shop.Value<double>("amt"), 0.5, "정수로 반올림해 쓴다");
            Assert.AreEqual(from, shop.Value<int>("from"));
            Assert.AreEqual(from + 2, shop.Value<int>("to"));

            Characters.Grant("sock");
            int level = Characters.GetLevel("sock");
            double lineageCost = Characters.GetLevelUpCost("sock");
            Assert.IsTrue(Characters.TryLevelUp("sock"));
            var lineage = Rows().Single(r => r.Value<string>("ev") == "spend" && r.Value<string>("sink") == "lineage");
            Assert.AreEqual("lxp", lineage.Value<string>("cur"), "유실자 레벨업은 실타래 (안 D)");
            Assert.AreEqual(lineageCost, lineage.Value<double>("amt"), 0.5);
            Assert.AreEqual("sock", lineage.Value<string>("id"));
            Assert.AreEqual(level + 1, lineage.Value<int>("lv"));

            recorder.Disconnect();
            Growth.TryLevelUp();
            Assert.AreEqual(2, Rows().Count(r => r.Value<string>("ev") == "spend"), "연결을 풀면 쓰지 않는다");
        }

        [Test]
        public void NullSystems_AreTolerated()
        {
            var recorder = new PlayLogRecorder(null, null, null, null, null, null, null, null, () => _now);

            Assert.DoesNotThrow(() =>
            {
                recorder.Connect();
                recorder.OnPause();
                recorder.OnResume();
                recorder.Disconnect();
            });
            Assert.AreEqual(4, Rows().Count);
        }
    }
}
