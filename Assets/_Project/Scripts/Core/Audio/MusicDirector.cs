using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>음악 덱 두 개(교차 페이드 짝)를 실제로 울리는 쪽 (AudioEngine, 테스트는 가짜). 시각은 DSP 시계(초)</summary>
    public interface IMusicOutput
    {
        double DspNow { get; }
        /// <summary>그 음악 키의 켜 둔 테이크 수 (없으면 0)</summary>
        int TakeCount(AudioId key);
        /// <summary>그 테이크의 길이 (샘플) / 샘플레이트 — 없으면 0</summary>
        int Samples(AudioId key, int take);
        int Frequency(AudioId key, int take);
        /// <summary>박자 정보 (없으면 null)</summary>
        AudioLibrary.MusicInfo Info(AudioId key, int take);
        /// <summary>deck에서 key/take를 startSample부터 dspStart(≤ 지금이면 바로)에 튼다</summary>
        void Play(int deck, AudioId key, int take, int startSample, double dspStart, bool loop);
        void Stop(int deck);
        /// <summary>덱 음량 곡선 (0~1). 버스 볼륨 · 덕은 출력 쪽이 곱한다</summary>
        void SetLevel(int deck, float level);
        int TimeSamples(int deck);
        bool IsPlaying(int deck);
    }

    /// <summary>
    /// 음악 감독 (AUDIO-SPEC §3.11, 순수 C# — IMusicOutput으로만 소리를 낸다):
    ///   · 상태 우선순위 Story 60 &gt; Gacha 50 &gt; Event 40 &gt; Dungeon 30 &gt; Boss 20 &gt; Main 10 &gt; Silent 0. 요청 = 주인 스택(PushMusic/PopMusic, 8칸)
    ///     + 전투가 내는 Boss 요청 (챕터 보스가 살아 있고 전장이 숨겨지지 않았을 때) + 바닥 상태(기본 Main). 그중 가장 높은 것.
    ///   · 교차 페이드 (같은 힘 sin/cos): Main↔Boss 1.2초 (곡의 다음 박에서, 0.6초까지 기다림) · Main→Gacha 0.4초에 빠지고 소환 곡은 바로 ·
    ///     Gacha→Main 1.2초 · Story/Dungeon/Event로 1.0초 · Silent로 0.8초 · 잠 곡 2초.
    ///   · 돌아오기: Main 곡은 떠날 때 자리를 기억하고 1.5초 앞에서 1.2초에 걸쳐 다시 들어온다.
    ///   · 도입부: {곡}_intro가 있으면 먼저 한 번, 본 곡은 다른 덱에 도입부 끝 샘플에 맞춰 PlayScheduled.
    ///   · 돌림과 쉼 (Main만, 몇 시간씩 켜 두는 게임): 곡마다 2바퀴 → 4초 교차 페이드로 다른 곡 (바로 앞 곡 제외).
    ///     곡 3개마다 쉼: 6초에 걸쳐 빠지고 45~75초 환경음만 (IsResting — 환경음 +3 dB), 다음 곡이 4초에 들어온다. 보스가 살아 있으면 쉬지 않는다.
    ///   · 시작은 Boot 0.5초 뒤 (첫 실행의 PushMusic(Story)가 소리 바뀜 없이 이기게).
    /// </summary>
    public sealed class MusicDirector
    {
        public const float BootDelay = 0.5f;
        public const float MainBossFade = 1.2f;
        public const float BeatWaitMax = 0.6f;
        public const float MainToGachaOut = 0.4f;
        public const float GachaInFade = 0.05f;
        public const float GachaToMainFade = 1.2f;
        public const float MenuFade = 1.0f;
        public const float SilentFade = 0.8f;
        public const float SleepFade = 2.0f;
        public const float ResumeRewind = 1.5f;
        public const float ResumeFadeIn = 1.2f;
        public const int LoopsPerPiece = 2;
        public const float RotationFade = 4f;
        public const int PiecesPerRest = 3;
        public const float RestFadeOut = 6f;
        public const float RestMin = 45f;
        public const float RestMax = 75f;
        public const float RestFadeIn = 4f;
        /// <summary>도입부를 시작할 때 미리 잡는 여유 (정확한 끝 시각을 알기 위해 PlayScheduled)</summary>
        public const double IntroLead = 0.05;
        const int StackSize = 8;
        const int ResumeSlots = 16;

        public static int Priority(MusicState state) => state switch
        {
            MusicState.Story => 60,
            MusicState.Gacha => 50,
            MusicState.Event => 40,
            MusicState.Dungeon => 30,
            MusicState.Boss => 20,
            MusicState.Main => 10,
            _ => 0,
        };

        struct Request
        {
            public int Owner;
            public MusicState State;
        }

        struct Track
        {
            public AudioId Key;
            public int Take;
            public MusicState State;
            public bool Sleep;

            public bool IsNone => !Key.IsValid;
            public bool Same(in Track other) => Key == other.Key && (Take == other.Take || !Key.IsValid);
        }

        struct Deck
        {
            public bool Used;
            public AudioId Key;
            public int Take;
            public float Level;
            public float From;
            public float To;
            public double FadeStart;
            public float FadeTime;
            public bool Fading;
            public bool StopAtZero;
        }

        struct ResumePoint
        {
            public short Key;
            public int Take;
            public int Sample;
            public bool Used;
        }

        readonly IMusicOutput _out;
        readonly Func<float> _random;
        readonly Request[] _stack = new Request[StackSize];
        readonly Deck[] _decks = new Deck[2];
        readonly ResumePoint[] _resume = new ResumePoint[ResumeSlots];

        readonly AudioId _main;
        readonly AudioId _boss;
        readonly AudioId _dungeon;
        readonly AudioId _gacha;
        readonly AudioId _story;
        readonly AudioId _event;
        readonly AudioId _sleepKey;
        /// <summary>곡마다 도입부 키 ({곡}_intro — 만들 때 한 번 찾는다, 지금은 bgm_gacha_intro만)</summary>
        readonly AudioId[] _introOf = new AudioId[8];

        int _stackCount;
        MusicState _base = MusicState.Main;
        bool _bossRequest;
        bool _bossAlive;
        bool _hidden;
        bool _asleep;
        bool _started;
        double _startAt;

        int _active = -1;
        Track _current;
        MusicState _state = MusicState.Silent;

        bool _switchPending;
        double _switchAt;

        bool _introActive;
        int _introDeck = -1;
        double _loopAt;
        bool _loopScheduled;
        AudioId _loopKey;

        int _mainTake = -1;
        int _lastMainTake = -1;
        int _piecesSinceRest;
        double _pieceTime;
        bool _resting;
        double _restUntil;
        bool _restFadeInPending;
        double _lastNow;

        public MusicDirector(IMusicOutput output, Func<float> random01)
        {
            _out = output;
            _random = random01 ?? (() => 0.5f);
            _main = AudioCatalog.Find("bgm_battle_night");
            _boss = AudioCatalog.Find("bgm_boss");
            _dungeon = AudioCatalog.Find("bgm_dungeon");
            _gacha = AudioCatalog.Find("bgm_gacha");
            _story = AudioCatalog.Find("bgm_story");
            _event = AudioCatalog.Find("bgm_event");
            _sleepKey = AudioCatalog.Find("bgm_sleep");
            var keys = new[] { _main, _boss, _dungeon, _gacha, _story, _event, _sleepKey };
            for (int i = 0; i < keys.Length; i++)
                _introOf[i] = keys[i].IsValid ? AudioCatalog.Find(AudioCatalog.Keys[keys[i].Index].Key + "_intro") : AudioId.None;
        }

        AudioId IntroOf(AudioId key)
        {
            if (key == _main) return _introOf[0];
            if (key == _boss) return _introOf[1];
            if (key == _dungeon) return _introOf[2];
            if (key == _gacha) return _introOf[3];
            if (key == _story) return _introOf[4];
            if (key == _event) return _introOf[5];
            return key == _sleepKey ? _introOf[6] : AudioId.None;
        }

        /// <summary>지금 들리는(또는 들어오는) 음악 상태</summary>
        public MusicState State => _state;
        /// <summary>요청으로 정해진 상태 (아직 교차 페이드 중일 수 있음)</summary>
        public MusicState Effective => EffectiveState();
        public bool IsResting => _resting;
        public bool Started => _started;
        public AudioId CurrentKey => _current.Key;
        public int CurrentTake => _current.Take;
        public int ActiveDeck => _active;
        public bool IntroActive => _introActive;
        public int PiecesSinceRest => _piecesSinceRest;
        public int MainTake => _mainTake;
        public double RestUntil => _restUntil;
        public bool SwitchPending => _switchPending;

        /// <summary>부팅: delay초 뒤부터 음악을 시작한다</summary>
        public void Boot(double dspNow, float delay = BootDelay)
        {
            _started = false;
            _startAt = dspNow + Math.Max(0f, delay);
        }

        // ───────────── 요청 ─────────────

        public void Push(MusicState state, int owner)
        {
            for (int i = 0; i < _stackCount; i++)
            {
                if (_stack[i].Owner != owner) continue;
                for (int j = i; j < _stackCount - 1; j++) _stack[j] = _stack[j + 1];
                _stackCount--;
                break;
            }
            if (_stackCount >= StackSize)
            {
                for (int j = 0; j < _stackCount - 1; j++) _stack[j] = _stack[j + 1];
                _stackCount--;
            }
            _stack[_stackCount++] = new Request { Owner = owner, State = state };
        }

        public void Pop(int owner)
        {
            for (int i = 0; i < _stackCount; i++)
            {
                if (_stack[i].Owner != owner) continue;
                for (int j = i; j < _stackCount - 1; j++) _stack[j] = _stack[j + 1];
                _stackCount--;
                return;
            }
        }

        /// <summary>요청 스택을 비운다 (장면 다시 불러오기)</summary>
        public void ClearRequests()
        {
            _stackCount = 0;
            _bossRequest = false;
        }

        /// <summary>바닥 상태 (기본 Main — 제목 화면 등에서 바꿀 수 있다)</summary>
        public void SetBase(MusicState state) => _base = state;

        /// <summary>전투가 내는 Boss 요청 (챕터 보스 = 10의 배수 스테이지 보스가 살아 있을 때)</summary>
        public void SetBossRequest(bool chapterBossAlive) => _bossRequest = chapterBossAlive;

        /// <summary>아무 보스나 살아 있는지 (쉼을 막는다)</summary>
        public void SetBossAlive(bool alive) => _bossAlive = alive;

        /// <summary>전장이 전체 화면에 가려졌는지 (그동안 Boss 요청을 쉰다)</summary>
        public void SetHidden(bool hidden) => _hidden = hidden;

        /// <summary>잠: bgm_sleep이 있으면 2초에 걸쳐 그 곡으로 (없으면 출력 쪽이 Main을 먹먹하게 한다)</summary>
        public void SetAsleep(bool asleep) => _asleep = asleep;

        public bool HasSleepTrack => _out.TakeCount(_sleepKey) > 0;

        /// <summary>잠 곡(bgm_sleep)이 지금 배경음으로 골라지는지: 잠 중 · 곡 있음 · 지금 상태가 Main(또는 제 곡이 없는 상태). 아무것도 바꾸지 않는다</summary>
        public bool SleepTrackChosen => _asleep && HasSleepTrack && DesiredTrack(EffectiveState()).Sleep;

        MusicState EffectiveState()
        {
            var best = _base;
            if (_bossRequest && !_hidden && Priority(MusicState.Boss) > Priority(best)) best = MusicState.Boss;
            for (int i = 0; i < _stackCount; i++)
            {
                if (Priority(_stack[i].State) > Priority(best)) best = _stack[i].State;
            }
            return best;
        }

        // ───────────── 진행 ─────────────

        /// <summary>매 프레임 (dt = 실제 시간 — 배경음이 꺼져 멈춘 동안은 0)</summary>
        public void Update(double now, float dt)
        {
            _lastNow = now;
            if (!_started)
            {
                if (now < _startAt) return;
                _started = true;
            }

            UpdateIntro(now);

            var target = EffectiveState();
            if (_resting && (target != MusicState.Main || _bossAlive)) EndRest(now, false);

            var desired = DesiredTrack(target);
            if (!_switchPending && !_introActive && !desired.Same(in _current))
            {
                PlanSwitch(desired, target, now);
            }
            else if (_introActive && !desired.Same(in _current))
            {
                CancelIntro(now);
                PlanSwitch(desired, target, now);
            }
            if (_switchPending && now >= _switchAt) ExecuteSwitch(DesiredTrack(EffectiveState()), EffectiveState(), now);

            UpdateRotation(now, dt);
            UpdateFades(now);
        }

        Track DesiredTrack(MusicState state)
        {
            var none = new Track { Key = AudioId.None, Take = -1, State = MusicState.Silent };
            switch (state)
            {
                case MusicState.Silent:
                    return none;
                case MusicState.Boss:
                    if (_out.TakeCount(_boss) > 0) return new Track { Key = _boss, Take = 0, State = state };
                    break;
                case MusicState.Dungeon:
                    if (_out.TakeCount(_dungeon) > 0) return new Track { Key = _dungeon, Take = 0, State = state };
                    break;
                case MusicState.Gacha:
                    if (_out.TakeCount(_gacha) > 0) return new Track { Key = _gacha, Take = 0, State = state };
                    break;
                case MusicState.Story:
                    if (_out.TakeCount(_story) > 0) return new Track { Key = _story, Take = 0, State = state };
                    break;
                case MusicState.Event:
                    if (_out.TakeCount(_event) > 0) return new Track { Key = _event, Take = 0, State = state };
                    break;
            }
            // Main (또는 곡이 없는 상태)
            if (_asleep && _out.TakeCount(_sleepKey) > 0) return new Track { Key = _sleepKey, Take = 0, State = MusicState.Main, Sleep = true };
            if (_resting) return none;
            int takes = _out.TakeCount(_main);
            if (takes <= 0) return none;
            if (_mainTake < 0 || _mainTake >= takes) _mainTake = PickMainTake(takes);
            return new Track { Key = _main, Take = _mainTake, State = MusicState.Main };
        }

        int PickMainTake(int takes)
        {
            if (takes <= 1) return 0;
            int pick = Mathf.Clamp((int)(_random() * (takes - 1)), 0, takes - 2);
            int exclude = _lastMainTake >= 0 && _lastMainTake < takes ? _lastMainTake : -1;
            if (exclude < 0) return Mathf.Clamp((int)(_random() * takes), 0, takes - 1);
            return pick >= exclude ? pick + 1 : pick;
        }

        void PlanSwitch(in Track desired, MusicState target, double now)
        {
            _switchPending = true;
            _switchAt = now;
            // Main ↔ Boss: 곡의 다음 박에서 (0.6초까지 기다림)
            bool mainBoss = (_current.State == MusicState.Main && target == MusicState.Boss) ||
                (_current.State == MusicState.Boss && target == MusicState.Main);
            if (mainBoss && _active >= 0 && !_current.IsNone)
            {
                double wait = BeatWait(_active, _current);
                if (wait >= 0 && wait <= BeatWaitMax) _switchAt = now + wait;
            }
        }

        /// <summary>그 덱 곡의 다음 박까지 (초, 박 정보가 없으면 −1)</summary>
        double BeatWait(int deck, in Track track)
        {
            var info = _out.Info(track.Key, track.Take);
            int freq = _out.Frequency(track.Key, track.Take);
            if (info == null || info.bpm <= 0f || freq <= 0) return -1;
            double beat = 60.0 / info.bpm;
            double position = (double)_out.TimeSamples(deck) / freq - info.beatOffset;
            if (position < 0) return -position;
            double into = position % beat;
            double wait = beat - into;
            return wait >= beat - 1e-4 ? 0 : wait;
        }

        void ExecuteSwitch(in Track desired, MusicState target, double now)
        {
            _switchPending = false;
            if (desired.Same(in _current) && !desired.IsNone) return;
            var from = _current;
            float fadeOut;
            float fadeIn;
            FadeTimes(from, desired, out fadeOut, out fadeIn);

            // 나가는 곡: 자리를 기억하고 빠진다
            if (_active >= 0 && _decks[_active].Used)
            {
                if (from.State == MusicState.Main && !from.Sleep && from.Key == _main) SaveResume(from.Key, from.Take, _out.TimeSamples(_active));
                FadeTo(_active, 0f, fadeOut, now, true);
            }

            if (desired.IsNone)
            {
                _active = -1;
                _current = desired;
                // 쉼 동안은 Main 그대로 (돌림이 이어진다), 곡이 없는 상태는 Silent
                _state = _resting && target == MusicState.Main ? MusicState.Main : MusicState.Silent;
                return;
            }

            int deck = _active < 0 ? 0 : 1 - _active;
            if (_decks[deck].Used)
            {
                _out.Stop(deck);
                _decks[deck] = default;
            }

            int start = 0;
            if (desired.State == MusicState.Main && !desired.Sleep && desired.Key == _main)
            {
                int resume = TakeResume(desired.Key, desired.Take);
                int freq = _out.Frequency(desired.Key, desired.Take);
                if (resume > 0 && freq > 0)
                {
                    start = Math.Max(0, resume - (int)(ResumeRewind * freq));
                    fadeIn = Math.Max(fadeIn, ResumeFadeIn);
                }
            }
            if (_restFadeInPending)
            {
                _restFadeInPending = false;
                fadeIn = RestFadeIn;
                start = 0;
            }

            // 도입부는 다른 상태에서 들어올 때만 (돌림 · 잠 곡 바뀜에는 없음)
            var intro = desired.State != from.State ? IntroOf(desired.Key) : AudioId.None;
            if (intro.IsValid && _out.TakeCount(intro) > 0 && _out.Samples(intro, 0) > 0 && _out.Frequency(intro, 0) > 0)
            {
                double introStart = now + IntroLead;
                _out.Play(deck, intro, 0, 0, introStart, false);
                _decks[deck] = new Deck { Used = true, Key = intro, Take = 0, Level = 0f };
                FadeTo(deck, 1f, GachaInFade, now, false);
                _introActive = true;
                _introDeck = deck;
                _loopAt = introStart + (double)_out.Samples(intro, 0) / _out.Frequency(intro, 0);
                _loopScheduled = false;
                _loopKey = desired.Key;
                _active = deck;
                _current = desired;
                _state = target;
                UpdateIntro(now);
                return;
            }

            _out.Play(deck, desired.Key, desired.Take, start, 0, true);
            _decks[deck] = new Deck { Used = true, Key = desired.Key, Take = desired.Take, Level = 0f };
            FadeTo(deck, 1f, fadeIn, now, false);
            _active = deck;
            if (desired.State == MusicState.Main && !desired.Sleep && desired.Key == _main)
            {
                // 새 곡이면 처음부터 2바퀴, 돌아온 곡이면 기억한 자리부터 센다
                int freq = _out.Frequency(desired.Key, desired.Take);
                _pieceTime = start > 0 && freq > 0 ? (double)start / freq : 0;
            }
            _current = desired;
            _state = target;
        }

        void FadeTimes(in Track from, in Track to, out float fadeOut, out float fadeIn)
        {
            if (from.IsNone)
            {
                fadeOut = 0f;
                fadeIn = to.State == MusicState.Main ? ResumeFadeIn : MenuFade;
                if (to.State == MusicState.Gacha) fadeIn = GachaInFade;
                return;
            }
            if (to.IsNone)
            {
                fadeOut = _resting ? RestFadeOut : SilentFade;
                fadeIn = 0f;
                return;
            }
            if (from.Sleep || to.Sleep)
            {
                fadeOut = fadeIn = SleepFade;
                return;
            }
            if (from.State == MusicState.Main && to.State == MusicState.Main)
            {
                fadeOut = fadeIn = RotationFade;
                return;
            }
            if ((from.State == MusicState.Main && to.State == MusicState.Boss) || (from.State == MusicState.Boss && to.State == MusicState.Main))
            {
                fadeOut = fadeIn = MainBossFade;
                return;
            }
            if (to.State == MusicState.Gacha)
            {
                fadeOut = MainToGachaOut;
                fadeIn = GachaInFade;
                return;
            }
            if (from.State == MusicState.Gacha && to.State == MusicState.Main)
            {
                fadeOut = fadeIn = GachaToMainFade;
                return;
            }
            fadeOut = fadeIn = MenuFade;
        }

        void UpdateIntro(double now)
        {
            if (!_introActive) return;
            int loopDeck = 1 - _introDeck;
            if (!_loopScheduled)
            {
                ref var d = ref _decks[loopDeck];
                bool free = !d.Used || (!d.Fading && d.Level <= 0.0001f);
                if (!free && now >= _loopAt - 0.03)
                {
                    // 나가는 곡이 아직 빠지는 중이면 끊고 본 곡을 잡는다 (도입부 0.4초 페이드보다 길 때만)
                    _out.Stop(loopDeck);
                    d = default;
                    free = true;
                }
                if (free)
                {
                    if (d.Used) _out.Stop(loopDeck);
                    double at = Math.Max(_loopAt, now + 0.01);
                    _out.Play(loopDeck, _loopKey, 0, 0, at, true);
                    _decks[loopDeck] = new Deck { Used = true, Key = _loopKey, Take = 0, Level = _decks[_introDeck].Level };
                    _out.SetLevel(loopDeck, _decks[loopDeck].Level);
                    _loopScheduled = true;
                }
            }
            if (_loopScheduled && now >= _loopAt)
            {
                // 본 곡이 시작했다: 역할을 바꾼다 (도입부는 끝나서 스스로 멈춘다)
                var intro = _decks[_introDeck];
                _decks[loopDeck].Level = Mathf.Max(_decks[loopDeck].Level, intro.Level);
                if (intro.Fading)
                {
                    _decks[loopDeck].From = intro.From;
                    _decks[loopDeck].To = intro.To;
                    _decks[loopDeck].FadeStart = intro.FadeStart;
                    _decks[loopDeck].FadeTime = intro.FadeTime;
                    _decks[loopDeck].Fading = true;
                }
                _out.Stop(_introDeck);
                _decks[_introDeck] = default;
                _active = loopDeck;
                _introActive = false;
                _introDeck = -1;
            }
        }

        void CancelIntro(double now)
        {
            if (!_introActive) return;
            int loopDeck = 1 - _introDeck;
            if (_loopScheduled)
            {
                _out.Stop(loopDeck);
                _decks[loopDeck] = default;
            }
            _active = _introDeck;
            _introActive = false;
            _introDeck = -1;
            _loopScheduled = false;
        }

        // ───────────── 돌림 · 쉼 ─────────────

        void UpdateRotation(double now, float dt)
        {
            if (_resting)
            {
                if (now >= _restUntil) EndRest(now, true);
                return;
            }
            if (_state != MusicState.Main || _current.IsNone || _current.Sleep || _current.Key != _main || _active < 0 || _switchPending) return;
            int takes = _out.TakeCount(_main);
            int samples = _out.Samples(_main, _current.Take);
            int freq = _out.Frequency(_main, _current.Take);
            if (samples <= 0 || freq <= 0) return;
            double length = (double)samples / freq;
            _pieceTime += Math.Max(0f, dt);
            double turn = LoopsPerPiece * length - RotationFade;
            if (_pieceTime < turn) return;

            _pieceTime = 0;
            _piecesSinceRest++;
            ForgetResume(_main, _current.Take);
            _lastMainTake = _current.Take;
            if (_piecesSinceRest >= PiecesPerRest && !_bossAlive)
            {
                _piecesSinceRest = 0;
                _resting = true;
                _restUntil = now + RestFadeOut + Mathf.Lerp(RestMin, RestMax, _random());
                _mainTake = -1;
                return;
            }
            if (takes > 1) _mainTake = PickMainTake(takes);
        }

        void EndRest(double now, bool fadeInNext)
        {
            if (!_resting) return;
            _resting = false;
            _restFadeInPending = fadeInNext;
            int takes = _out.TakeCount(_main);
            _mainTake = takes > 0 ? PickMainTake(takes) : -1;
        }

        // ───────────── 음량 곡선 ─────────────

        void FadeTo(int deck, float level, float time, double now, bool stopAtZero)
        {
            ref var d = ref _decks[deck];
            if (!d.Used) return;
            if (time <= 0f)
            {
                d.Level = level;
                d.Fading = false;
                _out.SetLevel(deck, level);
                if (stopAtZero && level <= 0f)
                {
                    _out.Stop(deck);
                    d = default;
                }
                return;
            }
            d.From = d.Level;
            d.To = level;
            d.FadeStart = now;
            d.FadeTime = time;
            d.Fading = true;
            d.StopAtZero = stopAtZero;
        }

        void UpdateFades(double now)
        {
            for (int i = 0; i < 2; i++)
            {
                ref var d = ref _decks[i];
                if (!d.Used || !d.Fading) continue;
                float k = d.FadeTime > 0f ? Mathf.Clamp01((float)((now - d.FadeStart) / d.FadeTime)) : 1f;
                d.Level = EqualPower(d.From, d.To, k);
                _out.SetLevel(i, d.Level);
                if (k < 1f) continue;
                d.Fading = false;
                if (d.StopAtZero && d.To <= 0f)
                {
                    _out.Stop(i);
                    d = default;
                }
            }
        }

        /// <summary>같은 힘 곡선: 올라갈 때 sin, 내려갈 때 cos (교차하면 합 = 1)</summary>
        public static float EqualPower(float from, float to, float k)
        {
            k = Mathf.Clamp01(k);
            if (to >= from) return from + (to - from) * Mathf.Sin(k * Mathf.PI * 0.5f);
            return to + (from - to) * Mathf.Cos(k * Mathf.PI * 0.5f);
        }

        /// <summary>덱의 지금 곡선 값 (테스트 · 출력)</summary>
        public float Level(int deck) => deck >= 0 && deck < 2 ? _decks[deck].Level : 0f;

        public bool DeckUsed(int deck) => deck >= 0 && deck < 2 && _decks[deck].Used;

        // ───────────── 돌아오기 자리 ─────────────

        void SaveResume(AudioId key, int take, int sample)
        {
            if (!key.IsValid) return;
            int slot = -1;
            for (int i = 0; i < ResumeSlots; i++)
            {
                if (_resume[i].Used && _resume[i].Key == key.Index && _resume[i].Take == take)
                {
                    slot = i;
                    break;
                }
                if (!_resume[i].Used && slot < 0) slot = i;
            }
            if (slot < 0) slot = 0;
            _resume[slot] = new ResumePoint { Used = true, Key = key.Index, Take = take, Sample = Math.Max(0, sample) };
        }

        int TakeResume(AudioId key, int take)
        {
            for (int i = 0; i < ResumeSlots; i++)
            {
                if (!_resume[i].Used || _resume[i].Key != key.Index || _resume[i].Take != take) continue;
                return _resume[i].Sample;
            }
            return 0;
        }

        void ForgetResume(AudioId key, int take)
        {
            for (int i = 0; i < ResumeSlots; i++)
            {
                if (_resume[i].Used && _resume[i].Key == key.Index && _resume[i].Take == take) _resume[i].Used = false;
            }
        }

        /// <summary>기억한 자리 (테스트용, 없으면 0)</summary>
        public int ResumeSample(AudioId key, int take) => TakeResume(key, take);

        /// <summary>모두 멈추고 처음 상태로 (에디터 · 테스트)</summary>
        public void StopAll()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_decks[i].Used) _out.Stop(i);
                _decks[i] = default;
            }
            _active = -1;
            _current = new Track { Key = AudioId.None, Take = -1 };
            _state = MusicState.Silent;
            _switchPending = false;
            _introActive = false;
            _resting = false;
        }
    }
}
