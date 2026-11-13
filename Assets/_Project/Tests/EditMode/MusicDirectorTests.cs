using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 음악 감독 (AUDIO-SPEC §3.11): 부팅 0.5초 뒤 시작, 우선순위 · 주인 스택, 같은 힘 교차 페이드, Main↔Boss는 다음 박에서,
    /// Main으로 돌아오면 1.5초 앞에서, 소환 도입부 → 본 곡을 도입부 끝 샘플에 PlayScheduled, 곡 2바퀴마다 돌림 · 곡 3개마다 쉼 (보스가 있으면 쉬지 않음).
    /// </summary>
    public class MusicDirectorTests
    {
        const int Rate = 44100;

        sealed class FakeMusic : IMusicOutput
        {
            public struct Call
            {
                public int Deck;
                public string Key;
                public int Take;
                public int Start;
                public double At;
                public bool Loop;
            }

            struct Track
            {
                public int Takes;
                public int Samples;
                public AudioLibrary.MusicInfo Info;
            }

            readonly Dictionary<short, Track> _tracks = new();
            public readonly List<Call> Plays = new();
            public readonly List<int> Stops = new();
            public readonly float[] Levels = new float[2];
            public readonly bool[] Playing = new bool[2];
            public readonly int[] Time = new int[2];
            public double DspNow { get; set; }

            public void Add(string key, int takes, float seconds, float bpm = 0f)
            {
                _tracks[AudioCatalog.Find(key).Index] = new Track
                {
                    Takes = takes,
                    Samples = (int)(seconds * Rate),
                    Info = bpm > 0f ? new AudioLibrary.MusicInfo { bpm = bpm, beatsPerBar = 4 } : null,
                };
            }

            public int TakeCount(AudioId key) => key.IsValid && _tracks.TryGetValue(key.Index, out var t) ? t.Takes : 0;
            public int Samples(AudioId key, int take) => key.IsValid && _tracks.TryGetValue(key.Index, out var t) ? t.Samples : 0;
            public int Frequency(AudioId key, int take) => TakeCount(key) > 0 ? Rate : 0;
            public AudioLibrary.MusicInfo Info(AudioId key, int take) => key.IsValid && _tracks.TryGetValue(key.Index, out var t) ? t.Info : null;

            public void Play(int deck, AudioId key, int take, int startSample, double dspStart, bool loop)
            {
                Plays.Add(new Call { Deck = deck, Key = AudioCatalog.Keys[key.Index].Key, Take = take, Start = startSample, At = dspStart, Loop = loop });
                Playing[deck] = true;
                Time[deck] = startSample;
            }

            public void Stop(int deck)
            {
                Playing[deck] = false;
                Stops.Add(deck);
            }

            public void SetLevel(int deck, float level) => Levels[deck] = level;
            public int TimeSamples(int deck) => Time[deck];
            public bool IsPlaying(int deck) => Playing[deck];

            public Call LastPlay(string key)
            {
                for (int i = Plays.Count - 1; i >= 0; i--)
                {
                    if (Plays[i].Key == key) return Plays[i];
                }
                return new Call { Deck = -1 };
            }

            public int CountPlays(string key)
            {
                int n = 0;
                foreach (var call in Plays)
                {
                    if (call.Key == key) n++;
                }
                return n;
            }
        }

        FakeMusic _out;
        MusicDirector _music;
        double _now;

        [SetUp]
        public void SetUp()
        {
            _out = new FakeMusic();
            _music = new MusicDirector(_out, () => 0.5f);
            _now = 0;
        }

        void Step(double seconds, double dt = 0.05)
        {
            double end = _now + seconds;
            while (_now < end - 1e-9)
            {
                _now += dt;
                _out.DspNow = _now;
                _music.Update(_now, (float)dt);
            }
        }

        void StartMain(float seconds = 60f, int takes = 1, float bpm = 0f)
        {
            _out.Add("bgm_battle_night", takes, seconds, bpm);
            _music.Boot(0);
            Step(2.0);
        }

        [Test]
        public void Boot_StartsMainAfterHalfASecond_AndFadesIn()
        {
            _out.Add("bgm_battle_night", 1, 60f);
            _music.Boot(0);
            _music.Update(0.4, 0.4f);
            Assert.AreEqual(0, _out.Plays.Count, "첫 실행의 PushMusic(Story)가 바뀜 없이 이기도록 0.5초 기다린다");
            _now = 0.4;
            Step(0.2);
            Assert.AreEqual(1, _out.Plays.Count);
            Assert.AreEqual("bgm_battle_night", _out.Plays[0].Key);
            Assert.IsTrue(_out.Plays[0].Loop);
            Step(0.5);
            Assert.That(_out.Levels[0], Is.InRange(0.5f, 0.95f), "1.2초 같은 힘 페이드 중간");
            Step(1.5);
            Assert.AreEqual(1f, _out.Levels[0], 1e-4f);
            Assert.AreEqual(MusicState.Main, _music.State);
        }

        [Test]
        public void OwnerStack_HighestPriorityWins()
        {
            Assert.AreEqual(MusicState.Main, _music.Effective);
            _music.Push(MusicState.Gacha, 1);
            Assert.AreEqual(MusicState.Gacha, _music.Effective);
            _music.Push(MusicState.Story, 2);
            _music.Push(MusicState.Event, 3);
            Assert.AreEqual(MusicState.Story, _music.Effective);
            _music.Pop(2);
            Assert.AreEqual(MusicState.Gacha, _music.Effective);
            _music.Pop(1);
            Assert.AreEqual(MusicState.Event, _music.Effective);
            _music.Pop(3);
            Assert.AreEqual(MusicState.Main, _music.Effective);
            _music.SetBossRequest(true);
            Assert.AreEqual(MusicState.Boss, _music.Effective);
            _music.SetHidden(true);
            Assert.AreEqual(MusicState.Main, _music.Effective, "전장이 숨겨지면 보스 요청은 쉰다");
            _music.SetHidden(false);
            _music.Push(MusicState.Dungeon, 4);
            Assert.AreEqual(MusicState.Dungeon, _music.Effective, "던전 30 > 보스 20");
            _music.Push(MusicState.Dungeon, 4);
            _music.Pop(4);
            Assert.AreEqual(MusicState.Boss, _music.Effective, "같은 주인은 한 칸");
        }

        [Test]
        public void Crossfade_IsEqualPower()
        {
            _out.Add("bgm_dungeon", 1, 60f);
            StartMain();
            _music.Push(MusicState.Dungeon, 7);
            Step(0.5);
            float a = _out.Levels[0];
            float b = _out.Levels[1];
            Assert.AreEqual(1f, a * a + b * b, 0.08f, "같은 힘: 나가는 cos² + 들어오는 sin² = 1");
            Assert.AreEqual("bgm_dungeon", _out.LastPlay("bgm_dungeon").Key);
            Step(1.0);
            Assert.AreEqual(0f, _out.Levels[0], 1e-4f);
            Assert.AreEqual(1f, _out.Levels[1], 1e-4f);
            Assert.Contains(0, _out.Stops, "다 빠진 덱은 멈춘다");
            Assert.AreEqual(MusicState.Dungeon, _music.State);
        }

        [Test]
        public void MainToBoss_StartsOnTheNextBeat()
        {
            _out.Add("bgm_boss", 1, 50f, 116f);
            StartMain(60f, 1, 120f);
            int deck = _music.ActiveDeck;
            _out.Time[deck] = (int)(0.3 * Rate);
            _music.SetBossRequest(true);
            Step(0.05);
            Assert.IsTrue(_music.SwitchPending, "박(0.5초)의 0.3 지점 → 0.2초 기다림");
            Assert.AreEqual(0, _out.CountPlays("bgm_boss"));
            Step(0.3);
            Assert.AreEqual(1, _out.CountPlays("bgm_boss"));
            Assert.AreEqual(MusicState.Boss, _music.State);
        }

        [Test]
        public void ReturningToMain_Resumes1p5SecondsEarlier()
        {
            _out.Add("bgm_gacha", 1, 40f);
            StartMain();
            _out.Time[_music.ActiveDeck] = 30 * Rate;
            _music.Push(MusicState.Gacha, 9);
            Step(0.5);
            Assert.AreEqual(30 * Rate, _music.ResumeSample(AudioCatalog.Find("bgm_battle_night"), 0));
            _music.Pop(9);
            Step(0.1);
            var resume = _out.LastPlay("bgm_battle_night");
            Assert.AreEqual((int)(28.5 * Rate), resume.Start, "떠난 자리 1.5초 앞에서");
            Assert.AreEqual(2, _out.CountPlays("bgm_battle_night"));
        }

        [Test]
        public void GachaIntro_ThenTheLoopAtTheExactEndSample()
        {
            _out.Add("bgm_gacha", 1, 40f);
            _out.Add("bgm_gacha_intro", 1, 2f);
            StartMain();
            double pushAt = _now;
            _music.Push(MusicState.Gacha, 3);
            Step(0.05);
            var intro = _out.LastPlay("bgm_gacha_intro");
            Assert.AreNotEqual(-1, intro.Deck);
            Assert.IsFalse(intro.Loop, "도입부는 한 번");
            Assert.AreEqual(pushAt + 0.05 + MusicDirector.IntroLead, intro.At, 0.06);
            Assert.IsTrue(_music.IntroActive);
            Step(0.6);
            var loop = _out.LastPlay("bgm_gacha");
            Assert.AreNotEqual(-1, loop.Deck, "Main이 0.4초에 빠진 뒤 본 곡을 예약");
            Assert.AreNotEqual(intro.Deck, loop.Deck, "다른 덱");
            Assert.IsTrue(loop.Loop);
            Assert.AreEqual(intro.At + 2.0, loop.At, 1e-6, "도입부 끝 샘플에 딱 맞춰");
            Step(2.0);
            Assert.IsFalse(_music.IntroActive);
            Assert.AreEqual(loop.Deck, _music.ActiveDeck, "역할이 바뀐다");
        }

        [Test]
        public void Rotation_PlaysEachPieceTwice_AndRestsAfterThree()
        {
            StartMain(10f, 3);
            Assert.AreEqual(1, _out.CountPlays("bgm_battle_night"));
            bool rested = false;
            double restSeen = -1;
            for (int i = 0; i < 2000 && _out.CountPlays("bgm_battle_night") < 4; i++)
            {
                Step(0.1, 0.1);
                if (_music.IsResting && !rested)
                {
                    rested = true;
                    restSeen = _now;
                    Assert.AreEqual(3, _out.CountPlays("bgm_battle_night"), "곡 3개 뒤에 쉼");
                }
            }
            Assert.IsTrue(rested);
            Assert.AreEqual(4, _out.CountPlays("bgm_battle_night"), "쉼 뒤 다음 곡");
            Assert.GreaterOrEqual(_now - restSeen, MusicDirector.RestFadeOut + MusicDirector.RestMin - 0.2, "6초 빠짐 + 45~75초 환경음만");
            for (int i = 1; i < _out.Plays.Count; i++)
                Assert.AreNotEqual(_out.Plays[i - 1].Take, _out.Plays[i].Take, "바로 앞 곡은 다시 고르지 않는다");
        }

        [Test]
        public void NoRest_WhileABossIsAlive()
        {
            StartMain(10f, 3);
            _music.SetBossAlive(true);
            for (int i = 0; i < 800; i++)
            {
                Step(0.1, 0.1);
                Assert.IsFalse(_music.IsResting);
            }
            Assert.GreaterOrEqual(_out.CountPlays("bgm_battle_night"), 4);
        }

        [Test]
        public void EqualPowerCurve_EndsExactly()
        {
            Assert.AreEqual(0f, MusicDirector.EqualPower(0f, 1f, 0f), 1e-6f);
            Assert.AreEqual(1f, MusicDirector.EqualPower(0f, 1f, 1f), 1e-6f);
            Assert.AreEqual(0f, MusicDirector.EqualPower(1f, 0f, 1f), 1e-6f);
            float up = MusicDirector.EqualPower(0f, 1f, 0.3f);
            float down = MusicDirector.EqualPower(1f, 0f, 0.3f);
            Assert.AreEqual(1f, up * up + down * down, 1e-4f);
        }
    }
}
