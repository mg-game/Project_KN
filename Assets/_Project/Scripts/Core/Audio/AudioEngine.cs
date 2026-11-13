using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 코드 버스 오디오 엔진 (AUDIO-SPEC §3). "[Audio]" 아래 소스 31개(Ui 8 · Sfx 14 · Stinger 1 · Loop 2 · Bgm 2 · Amb 4 — ProjectSettings 실제 목소리 32개 안)를
    /// 각자 자식 오브젝트에 두고 (필터가 소스마다 따로), 버스 볼륨 · 덕 · 집중 · 잠 · 백그라운드를 곱해 볼륨을 정한다.
    ///   volume = 버스 볼륨 × 덕(버스, 가장 깊은 것) × dB(손질 + 테이크 보정 + 규칙 dB + 흔들기) × 부른 볼륨, 1로 자른다.
    /// UISfx(MonoBehaviour)가 LateUpdate에서 Tick을 부른다. 전투 소리는 연출 시계 대기열(AudioScheduler)을 거쳐 PlayScheduled로 프레임보다 정확하게 울린다.
    /// 할당: 시작할 때 배열을 모두 만든다. 매 프레임 할당 없음 (LINQ · 문자열 없음).
    /// </summary>
    internal sealed class AudioEngine : IMusicOutput, IAmbienceOutput
    {
        public const int UiCount = 8;
        public const int SfxCount = 14;
        public const int StingerCount = 1;
        public const int LoopCount = 2;
        public const int BgmCount = 2;
        public const int AmbCount = 4;
        public const int UiStart = 0;
        public const int SfxStart = UiStart + UiCount;
        public const int StingerStart = SfxStart + SfxCount;
        public const int LoopStart = StingerStart + StingerCount;
        public const int BgmStart = LoopStart + LoopCount;
        public const int AmbStart = BgmStart + BgmCount;
        public const int VoiceCount = AmbStart + AmbCount;

        /// <summary>군중 묶음: 같은 60ms 창에 4개 이상이면 2개만 따로 + hit_multi 하나</summary>
        public const double CrowdWindow = 0.06;
        public const int CrowdMin = 4;
        public const int CrowdKeep = 2;
        /// <summary>느린 화면 동안 새로 시작하는 Sfx 음높이 ("시간 끌림")</summary>
        public const float TimeDragPitch = 0.94f;
        /// <summary>징글 교체 페이드</summary>
        public const float JingleSwapFade = 0.06f;
        /// <summary>목소리(바크) 전체 간격</summary>
        public const float BarkInterval = 6f;
        public const float PauseFadeOut = 0.2f;
        public const float PauseFadeIn = 0.4f;
        public const int UiPriority = 48;
        public const int EmitterPriority = 220;

        static readonly int[] BusStart = { UiStart, SfxStart, StingerStart, BgmStart, AmbStart, LoopStart };
        static readonly int[] BusSize = { UiCount, SfxCount, StingerCount, BgmCount, AmbCount, LoopCount };
        static readonly int[] UnityBusPriority = { 48, 128, 8, 0, 16, 24 };

        readonly GameObject _root;
        readonly AudioSource[] _src = new AudioSource[VoiceCount];
        readonly AudioLowPassFilter[] _lpf = new AudioLowPassFilter[VoiceCount];
        readonly VoiceSlot[] _slots = new VoiceSlot[VoiceCount];
        readonly float[] _base = new float[VoiceCount];
        readonly int[] _exempt = new int[VoiceCount];
        readonly bool[] _emitter = new bool[VoiceCount];
        readonly float[] _level = new float[VoiceCount];
        readonly float[] _applied = new float[VoiceCount];
        readonly AudioBus[] _voiceBus = new AudioBus[VoiceCount];

        readonly float[] _busGain = new float[AudioStats.BusCount];
        readonly DuckStack[] _ducks = new DuckStack[AudioStats.BusCount];
        readonly LowPassStack _bgmLpf = new();
        readonly LowPassStack _ambLpf = new();

        readonly UISfxGate _uiGate = new();
        readonly AudioGate _gate = new(AudioCatalog.Count);
        readonly ShuffleState[] _shuffle = new ShuffleState[AudioCatalog.Count];
        readonly LadderState[] _ladders = new LadderState[AudioCatalog.Ladders.Length];
        readonly TokenBucket[] _buckets = new TokenBucket[8];
        TokenBucket _battleStarts = new(28f, 10f);

        readonly AudioScheduler _scheduler = new();
        readonly MusicDirector _music;
        readonly AmbienceDirector _ambience;
        readonly System.Random _rng = new();
        readonly Func<float> _random01;
        readonly float[] _rootScratch = new float[16];
        readonly int[] _hitScratch = new int[32];
        readonly AudioStats _stats = new();

        // 징글 덕 규칙 (§3.10)
        readonly AudioId _hitMulti;
        readonly AudioId _jStageClear, _jBossDefeated, _jChapterClear, _jStageFail, _jBossAppear, _jGachaLegend, _jGachaMythic;
        readonly AudioId _uiMilestone, _uiEvolveBurst, _uiRewardDouble, _uiGachaRare, _uiGachaMythic, _uiSleepEnter, _uiSleepWake;
        readonly AudioId _bgmSleep;

        AudioLibrary _library;
        UISfxTable _legacy;
        bool _soundOn = true;
        float _sfxGain = 1f;
        float _bgmGain = 1f;
        bool _bgmMuted;
        bool _bgmSourcesMuted;

        bool _asleep;
        // 잠 중 배경음 덕 (F1 · RM-3): 걸려 있는지 · 잠 곡이 넘겨받아 SleepFade에 걸쳐 푸는 중인지 (푸는 시작 시각, UI 시계)
        bool _sleepBgmDucked;
        bool _sleepBgmOpening;
        double _sleepBgmOpenStart;
        BattleAudioFocus _focus;
        readonly int[] _focusOwners = new int[8];
        readonly BattleAudioFocus[] _focusValues = new BattleAudioFocus[8];
        int _focusCount;

        bool _paused;
        float _pauseFade = 1f;
        bool _sourcesPaused;

        float _fxScale = 1f;
        float _speedRate = 1f;
        float _unscaledDt = 1f / 60f;
        bool _latencyComp;
        double _latencySeconds;
        double _uiNow;
        double _dspNow;

        // 징글 교체
        AudioId _pendingJingle;
        float _pendingJingleVolume;
        bool _stingerFading;
        double _stingerFadeStart;
        double _jingleEnd;
        double _jingleStart;
        AudioId _currentJingle;

        // 반복 (심장 박동 · 보스 예비 동작)
        struct LoopState
        {
            public AudioId Key;
            public float Volume;
            public float Target;
            public float Pitch;
            public float TargetPitch;
            public float Rate;
            public bool Playing;
        }

        readonly LoopState[] _loops = new LoopState[LoopCount];

        double _lastBark = double.NegativeInfinity;
        double _statsTime;
        int _startsThisSecond;
        double _startsWindow;
        bool _musicStarted;

        AudioListener _ownListener;
        double _listenerCheck;

        // 오디오 장치 바뀜 (이어폰 · 블루투스 · AudioSettings.Reset): Unity가 소스를 모두 멈추고 DSP 시계를 0부터 다시 셀 수 있다.
        // 엔진 시계(_dspNow)는 raw dspTime + _dspBase로 늘 앞으로만 간다 — PlayScheduled에 넘길 때만 raw로 되돌린다
        /// <summary>DSP 시계가 이만큼 넘게 뒤로 가면 장치가 다시 시작한 것으로 본다 (초)</summary>
        const double DspBackstep = 0.05;
        double _dspBase;
        bool _audioReset;
        /// <summary>계속 울려야 하는 소스 (Loop · Bgm · Amb) — 장치가 바뀐 뒤 다시 튼다</summary>
        readonly bool[] _wantPlay = new bool[VoiceCount];
        /// <summary>그 소스의 마지막 재생 자리 (0.25초마다) — 다시 틀 때 이어서</summary>
        readonly int[] _lastSample = new int[VoiceCount];
        /// <summary>예약 시작 시각 (엔진 시계, 0 = 바로) — 도입부 뒤 본 곡</summary>
        readonly double[] _scheduledAt = new double[VoiceCount];

        public AudioEngine(GameObject root, AudioLibrary library, UISfxTable legacy)
        {
            _root = root;
            _library = library;
            _legacy = legacy;
            _random01 = NextRandom;
            for (int i = 0; i < _ducks.Length; i++) _ducks[i] = new DuckStack();
            InitBuckets();
            BuildSources();
            _music = new MusicDirector(this, _random01);
            _ambience = new AmbienceDirector(this, _random01);
            _hitMulti = AudioCatalog.Find("hit_multi");
            _jStageClear = AudioCatalog.Find("jingle_stage_clear");
            _jBossDefeated = AudioCatalog.Find("jingle_boss_defeated");
            _jChapterClear = AudioCatalog.Find("jingle_chapter_clear");
            _jStageFail = AudioCatalog.Find("jingle_stage_fail");
            _jBossAppear = AudioCatalog.Find("jingle_boss_appear");
            _jGachaLegend = AudioCatalog.Find("jingle_gacha_legend");
            _jGachaMythic = AudioCatalog.Find("jingle_gacha_mythic");
            _uiMilestone = AudioCatalog.Find("ui_level_milestone");
            _uiEvolveBurst = AudioCatalog.Find("ui_evolve_burst");
            _uiRewardDouble = AudioCatalog.Find("ui_reward_double");
            _uiGachaRare = AudioCatalog.Find("ui_gacha_rare");
            _uiGachaMythic = AudioCatalog.Find("ui_gacha_reveal_mythic");
            _uiSleepEnter = AudioCatalog.Ui(UISfxKey.SleepEnter);
            _uiSleepWake = AudioCatalog.Ui(UISfxKey.SleepWake);
            _bgmSleep = AudioCatalog.Find("bgm_sleep");
            _latencyComp = AudioService.LatencyCompensation;
            RefreshLatency();
            _dspNow = AudioSettings.dspTime;
            _uiNow = Time.unscaledTimeAsDouble;
            _scheduler.SetUiNow(_uiNow);
            _music.Boot(_dspNow);
        }

        float NextRandom() => (float)_rng.NextDouble();

        void InitBuckets()
        {
            _buckets[(int)AudioCategory.None] = new TokenBucket(1000f, 1000f);
            _buckets[(int)AudioCategory.Hit] = new TokenBucket(14f, 6f);
            _buckets[(int)AudioCategory.Launch] = new TokenBucket(8f, 3f);
            _buckets[(int)AudioCategory.Strike] = new TokenBucket(6f, 3f);
            _buckets[(int)AudioCategory.Hurt] = new TokenBucket(5f, 2f);
            _buckets[(int)AudioCategory.Death] = new TokenBucket(8f, 4f);
            _buckets[(int)AudioCategory.Status] = new TokenBucket(4f, 2f);
            _buckets[(int)AudioCategory.Spawn] = new TokenBucket(3f, 1f);
        }

        void BuildSources()
        {
            for (int bus = 0; bus < AudioStats.BusCount; bus++)
            {
                for (int i = 0; i < BusSize[bus]; i++)
                {
                    int v = BusStart[bus] + i;
                    var go = new GameObject(((AudioBus)bus) + " " + i);
                    go.transform.SetParent(_root.transform, false);
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    source.dopplerLevel = 0f;
                    source.bypassReverbZones = true;
                    source.priority = UnityBusPriority[bus];
                    source.volume = 0f;
                    _src[v] = source;
                    _voiceBus[v] = (AudioBus)bus;
                    _slots[v].Key = -1;
                    _applied[v] = -1f;
                    if ((AudioBus)bus == AudioBus.Bgm || (AudioBus)bus == AudioBus.Amb)
                    {
                        var filter = go.AddComponent<AudioLowPassFilter>();
                        filter.cutoffFrequency = LowPassStack.Open;
                        filter.lowpassResonanceQ = 1f;
                        filter.enabled = false;
                        _lpf[v] = filter;
                    }
                }
            }
        }

        // ───────────── 상태 ─────────────

        public AudioLibrary Library => _library;
        public UISfxTable Legacy => _legacy;
        public AudioStats Stats => _stats;
        public MusicDirector Music => _music;
        public AmbienceDirector Ambience => _ambience;
        public AudioScheduler Scheduler => _scheduler;
        public bool IsAsleep => _asleep;
        public BattleAudioFocus Focus => _focus;
        /// <summary>연출 시계 (초) — 전투 감독이 예약 시각을 셀 때</summary>
        public double FxNow => _scheduler.FxNow;
        public double UiNow => _uiNow;
        public bool IsJinglePlaying => _uiNow < _jingleEnd;
        public double JingleStartedAt => _jingleStart;
        public AudioId CurrentJingle => _currentJingle;
        public bool IsSlowMotion => _fxScale < 0.999f;
        double IMusicOutput.DspNow => _dspNow;

        /// <summary>카탈로그 안의 번호인지 (직접 만든 AudioId가 범위를 넘어도 예외 없이 조용히)</summary>
        static bool Known(AudioId id) => id.IsValid && id.Index < AudioCatalog.Count;

        public bool HasClips(AudioId id)
        {
            if (_library == null || !Known(id)) return false;
            var entry = _library.Get(id);
            if (entry != null && entry.HasClips) return true;
            var src = AudioCatalog.ClipSource(id);
            if (src == id) return false;
            var target = _library.Get(src);
            return target != null && target.HasClips;
        }

        /// <summary>그 키(별칭이면 대상)의 클립 칸 — 별칭은 자기 칸에 테이크가 없으면 대상 칸</summary>
        AudioLibrary.Entry ClipEntry(AudioId id)
        {
            if (_library == null || !id.IsValid) return null;
            var entry = _library.Get(id);
            if (entry != null && entry.takes != null && entry.takes.Length > 0) return entry;
            var src = AudioCatalog.ClipSource(id);
            return src != id ? _library.Get(src) : entry;
        }

        float TrimDb(AudioId id)
        {
            var entry = _library != null ? _library.Get(id) : null;
            return entry != null ? Mathf.Clamp(entry.trimDb, -40f, 6f) : 0f;
        }

        // ───────────── 설정 ─────────────

        public void ApplySettings()
        {
            _soundOn = GameSettings.SoundOn;
            _sfxGain = _soundOn ? GameSettings.SfxVolume : 0f;
            _bgmGain = _soundOn ? GameSettings.BgmVolume : 0f;
            _busGain[(int)AudioBus.Ui] = _sfxGain;
            _busGain[(int)AudioBus.Sfx] = _sfxGain;
            _busGain[(int)AudioBus.Stinger] = _sfxGain;
            _busGain[(int)AudioBus.Loop] = _sfxGain;
            _busGain[(int)AudioBus.Bgm] = _bgmGain;
            _busGain[(int)AudioBus.Amb] = _bgmGain * 0.9f;
            _bgmMuted = _bgmGain <= 0f;
            SyncBgmPause();
            RefreshVolumes(true);
        }

        /// <summary>예전 표를 바꾼다. 그 표의 배경음이 Main 곡이면(라이브러리에 Main이 없을 때) 새 배경음으로 다시 튼다</summary>
        public void SetLegacyTable(UISfxTable table)
        {
            var old = _legacy != null ? _legacy.bgm : null;
            _legacy = table;
            var main = AudioCatalog.Find("bgm_battle_night");
            bool libraryMain = _library != null && _library.Has(main);
            if (!libraryMain && old != (table != null ? table.bgm : null)) _music.StopAll();
        }

        public void ReloadLibrary(AudioLibrary library)
        {
            if (library != null) library.Invalidate();
            _library = library;
            for (int i = 0; i < _shuffle.Length; i++) _shuffle[i] = default;
        }

        public bool LatencyCompensation
        {
            get => _latencyComp;
            set
            {
                _latencyComp = value;
                RefreshLatency();
            }
        }

        void RefreshLatency()
        {
            AudioSettings.GetDSPBufferSize(out int length, out int buffers);
            _latencySeconds = AudioScheduler.LatencyCompensation(length, buffers, AudioSettings.outputSampleRate);
        }

        /// <summary>전투 감독이 매 프레임 넣는다: 연출 시간 배율 (BattleFx.TimeScale 또는 거울)</summary>
        public void SetFxTimeScale(float scale) => _fxScale = Mathf.Clamp01(scale);

        /// <summary>배속 → 예산 배율 (2배속 ×1.3)</summary>
        public void SetBattleSpeed(int speed) => _speedRate = speed >= 2 ? 1.3f : 1f;

        float BudgetRate => _speedRate * (_asleep ? 0.5f : 1f);

        // ───────────── UI ─────────────

        /// <summary>
        /// UISfx.Play 경로 (§3.2): (1) 라이브러리에 그 키 클립이 있으면 엔진 (징글 5키는 Stinger), 게이트는 예전 UISfxGate
        /// (2) 없으면 예전 표 (3) 그것도 없으면 조용히
        /// </summary>
        public bool PlayUi(UISfxKey key, float volume)
        {
            if (!(volume > 0f) || _paused) return false;
            var id = AudioCatalog.Ui(key);
            if (id.IsValid && HasClips(id))
            {
                var def = AudioCatalog.Keys[id.Index];
                if (def.Bus == AudioBus.Stinger)
                {
                    PlayJingle(id, volume);
                    return true;
                }
                return PlayUiLibrary(key, id, in def, volume);
            }
            return PlayUiLegacy(key, volume);
        }

        bool PlayUiLibrary(UISfxKey key, AudioId id, in AudioKeyDef def, float volume)
        {
            if (_sfxGain <= 0f) return false;
            var entry = ClipEntry(id);
            if (entry == null || entry.takes == null || entry.takes.Length == 0) return false;
            float pitch = 1f;
            int take;
            if (def.IsTuned && def.Ladder > 0)
            {
                int step = PitchLadder.Advance(ref _ladders[def.Ladder], in AudioCatalog.Ladders[def.Ladder], _uiNow);
                float target = AudioCatalog.LadderHz(def.Ladder, step, MusicTranspose());
                take = PickTuned(entry, def, id, target, out pitch);
            }
            else take = ShuffleBag.Next(ref _shuffle[id.Index], entry.takes.Length, NextRandom());
            if (take < 0) return false;
            var clip = entry.takes[take].clip;
            if (clip == null)
            {
                _stats.Missing++;
                return false;
            }
            if (!_uiGate.TryPlay(key, (float)_uiNow, clip.length, NextRandom(), out float gatePitch))
            {
                _stats.GateBlocks++;
                return false;
            }
            if (!def.IsTuned) pitch *= gatePitch;
            float gainDb = TrimDb(id) + entry.takes[take].gainDb;
            int v = StartVoice(AudioBus.Ui, id, in def, clip, volume, gainDb, pitch, 0f, def.Class, DuckExempt.None, 0, false);
            if (v < 0) return false;
            UiDuck(id);
            return true;
        }

        bool PlayUiLegacy(UISfxKey key, float volume)
        {
            var table = _legacy;
            if (table == null || _sfxGain <= 0f) return false;
            var entry = table.Get(key);
            if (entry == null) return false;
            var clip = table.Pick(key, NextRandom());
            if (clip == null) return false;
            if (!_uiGate.TryPlay(key, (float)_uiNow, clip.length, NextRandom(), out float pitch)) return false;
            var def = new AudioKeyDef(null, AudioGroup.Ui, AudioBus.Ui, AudioVoiceClass.Normal, UiPriority, AudioTier.B, -17, clip.length, 1, 0f, 3,
                0f, 0f, AudioKeyFlags.None, 0, 0f, -1, 0, (int)key);
            return StartVoice(AudioBus.Ui, AudioId.None, in def, clip, volume * entry.volume, 0f, pitch, 0f, AudioVoiceClass.Normal, DuckExempt.None, 0, false) >= 0;
        }

        /// <summary>UI 큰 순간 덕 (§3.10)</summary>
        void UiDuck(AudioId id)
        {
            if (id == _uiMilestone || id == _uiEvolveBurst || id == _uiRewardDouble)
                _ducks[(int)AudioBus.Bgm].Trigger(DuckReason.UiBig, -5f, 0.04f, 0.8f, 0.6f, _uiNow);
            else if (id == _uiGachaRare || id == _uiGachaMythic)
                _ducks[(int)AudioBus.Bgm].Trigger(DuckReason.GachaHigh, -8f, 0.05f, 1.8f, 0.8f, _uiNow);
        }

        // ───────────── 일반 재생 (AudioService) ─────────────

        /// <summary>바로 한 번 (AudioService.Play). 음악 키는 무시 (음악은 PushMusic), 징글 키는 PlayJingle</summary>
        public void Play(AudioId id, float volume, float pan, float pitch)
        {
            if (!Known(id) || !(volume > 0f) || _paused) return;
            var def = AudioCatalog.Keys[id.Index];
            switch (def.Bus)
            {
                case AudioBus.Stinger:
                    PlayJingle(id, volume);
                    return;
                case AudioBus.Bgm:
                case AudioBus.Loop:
                    return;
                case AudioBus.Amb:
                    if (def.Has(AudioKeyFlags.Emitter)) Emit(id, AudioGate.GainToDb(volume), pan);
                    return;
            }
            var sound = new PendingSound
            {
                Key = id.Index,
                Volume = volume,
                Pitch = pitch > 0f ? pitch : 1f,
                Pan = pan,
                Class = def.Class,
                EnemyId = -1,
                Flags = AudioPlayFlags.UiClock | AudioPlayFlags.BypassBudget,
            };
            Commit(ref sound, _dspNow);
        }

        /// <summary>
        /// 목소리(바크) 한 번 — 실제로 울렸을 때만 true (AudioService.TryPlayBark). 목소리 키(AudioKeyFlags.Vox)가 아니거나, 목소리 설정이 꺼졌거나,
        /// 그 키(별칭이면 대상)에 클립이 없거나, 목소리 공통 간격(BarkInterval — 다른 목소리가 막 울렸으면)이 아직이거나, 키 게이트 · 볼륨 0 · 일시 정지 ·
        /// 효과음 꺼짐으로 막히면 아무것도 울리지 않고 false: 부른 쪽이 대신 소리를 고른다 (예: 쓰다듬기 → pet_squeak). 할당 없음
        /// </summary>
        public bool TryPlayBark(AudioId id, float volume)
        {
            if (!Known(id) || !(volume > 0f) || _paused) return false;
            var def = AudioCatalog.Keys[id.Index];
            if (!def.Has(AudioKeyFlags.Vox) || (def.Bus != AudioBus.Ui && def.Bus != AudioBus.Sfx)) return false;
            if (!CanBark() || !HasClips(id)) return false;
            double last = _lastBark;
            var sound = new PendingSound
            {
                Key = id.Index,
                Volume = volume,
                Pitch = 1f,
                Class = def.Class,
                EnemyId = -1,
                Flags = AudioPlayFlags.UiClock | AudioPlayFlags.BypassBudget,
            };
            Commit(ref sound, _dspNow);
            // Commit은 목소리가 실제로 시작됐을 때만 공통 간격(_lastBark)을 잡는다 — CanBark가 통과했으니 바뀌었으면 울린 것
            return _lastBark != last;
        }

        /// <summary>seconds초 뒤 (실제 시계) 한 번</summary>
        public void PlayAfter(AudioId id, float seconds, float volume, float pan)
        {
            if (!Known(id) || !(volume > 0f)) return;
            var def = AudioCatalog.Keys[id.Index];
            var sound = new PendingSound
            {
                Key = id.Index,
                Volume = volume,
                Pitch = 1f,
                Pan = pan,
                Class = def.Class,
                EnemyId = -1,
                Flags = AudioPlayFlags.UiClock | AudioPlayFlags.BypassBudget,
            };
            if (!_scheduler.Add(sound, seconds)) _stats.QueueOverflow++;
        }

        /// <summary>전투 감독의 예약 (연출 시계 delay초 뒤)</summary>
        public bool Schedule(PendingSound sound, double delay)
        {
            if (_paused || sound.Key < 0 || sound.Key >= AudioCatalog.Count) return false;
            if (_focus == BattleAudioFocus.Hidden && sound.Has(AudioPlayFlags.Battle)) return false;
            if (!_scheduler.Add(sound, delay))
            {
                _stats.QueueOverflow++;
                return false;
            }
            return true;
        }

        public void FlushBattle() => _scheduler.FlushBattle();

        // ───────────── 징글 ─────────────

        /// <summary>징글 (Stinger 한 목소리 — 새 징글은 60ms 페이드로 옛 것을 바꾼다) + 그 징글의 덕</summary>
        public void PlayJingle(AudioId id, float volume = 1f)
        {
            if (!Known(id) || _paused || _sfxGain <= 0f) return;
            var entry = ClipEntry(id);
            if (entry == null || !entry.HasClips) return;
            int v = StingerStart;
            if (_slots[v].Busy && _src[v].isPlaying && _slots[v].End > _dspNow + 0.02)
            {
                _pendingJingle = id;
                _pendingJingleVolume = volume;
                if (!_stingerFading)
                {
                    _stingerFading = true;
                    _stingerFadeStart = _uiNow;
                }
                return;
            }
            StartJingle(id, volume);
        }

        void StartJingle(AudioId id, float volume)
        {
            var entry = ClipEntry(id);
            if (entry == null || entry.takes == null || entry.takes.Length == 0) return;
            int take = ShuffleBag.Next(ref _shuffle[id.Index], entry.takes.Length, NextRandom());
            var clip = take >= 0 ? entry.takes[take].clip : null;
            if (clip == null) return;
            var def = AudioCatalog.Keys[id.Index];
            float gainDb = TrimDb(id) + entry.takes[take].gainDb;
            // 징글 목소리는 하나 (Critical이라 빼앗기지 않으므로 직접 비운다)
            _src[StingerStart].Stop();
            _slots[StingerStart].Busy = false;
            int v = StartVoice(AudioBus.Stinger, id, in def, clip, volume, gainDb, 1f, 0f, AudioVoiceClass.Critical, DuckExempt.None, 0, false);
            if (v < 0) return;
            _currentJingle = id;
            _jingleStart = _uiNow;
            _jingleEnd = _uiNow + clip.length;
            _ambience.SuppressEmitters(_jingleEnd);
            JingleDuck(AudioCatalog.ClipSource(id), clip.length);
        }

        void JingleDuck(AudioId id, float length)
        {
            var bgm = _ducks[(int)AudioBus.Bgm];
            if (id == _jStageClear || id == _jBossDefeated || id == _jChapterClear)
                bgm.Trigger(DuckReason.JingleClear, -10f, 0.06f, length, 0.7f, _uiNow);
            else if (id == _jStageFail)
                bgm.Trigger(DuckReason.JingleFail, -14f, 0.1f, length, 2f, _uiNow);
            else if (id == _jBossAppear)
            {
                bgm.Trigger(DuckReason.BossAppear, -7f, 0.08f, length, 0.8f, _uiNow);
                _ducks[(int)AudioBus.Sfx].Trigger(DuckReason.BossAppear, -4f, 0.08f, length, 0.8f, _uiNow, DuckExempt.Boss);
            }
            else if (id == _jGachaLegend || id == _jGachaMythic)
                bgm.Trigger(DuckReason.GachaHigh, -8f, 0.05f, 1.8f, 0.8f, _uiNow);
            else
                bgm.Trigger(DuckReason.JingleCard, -10f, 0.08f, length, 0.8f, _uiNow);
        }

        void TickStinger()
        {
            if (!_stingerFading) return;
            int v = StingerStart;
            float k = Mathf.Clamp01((float)((_uiNow - _stingerFadeStart) / JingleSwapFade));
            if (k < 1f && _src[v].isPlaying)
            {
                _level[v] = 1f - k;
                return;
            }
            _stingerFading = false;
            _src[v].Stop();
            _slots[v].Busy = false;
            _level[v] = 1f;
            var next = _pendingJingle;
            _pendingJingle = AudioId.None;
            if (next.IsValid) StartJingle(next, _pendingJingleVolume);
        }

        // ───────────── 음악 · 환경음 요청 ─────────────

        public void PushMusic(MusicState state, int owner) => _music.Push(state, owner);
        public void PopMusic(int owner) => _music.Pop(owner);
        public void SetBaseMusic(MusicState state) => _music.SetBase(state);
        public void SetBossMusic(bool chapterBossAlive) => _music.SetBossRequest(chapterBossAlive);

        public void SetBossAlive(bool alive)
        {
            _music.SetBossAlive(alive);
            _ambience.SetBossAlive(alive);
        }

        /// <summary>스테이지 기믹 환경음 (정전이면 배경음 5 kHz 저역 통과 = '쉿')</summary>
        public void SetModifier(StageModifierType modifier, bool stageChanged)
        {
            _ambience.SetModifier(modifier, stageChanged);
            if (modifier == StageModifierType.Blackout) _bgmLpf.Set(DuckReason.Blackout, 5000f);
            else _bgmLpf.Clear(DuckReason.Blackout);
        }

        public void SuppressEmitters(double seconds) => _ambience.SuppressEmitters(_uiNow + seconds);

        // ───────────── 덕 ─────────────

        public void Duck(AudioBus bus, float gainDb, float hold, float attack, float release, int reason, int exempt = DuckExempt.None, double delay = 0)
        {
            int b = (int)bus;
            if (b < 0 || b >= _ducks.Length) return;
            _ducks[b].Trigger(reason, gainDb, attack, hold, release, _uiNow + Math.Max(0, delay), exempt);
        }

        /// <summary>
        /// 읽기 전용 (테스트용): 그 버스에 지금 걸린 덕 깊이 (dB, ≤ 0, 면제 비트 없이 가장 깊은 것). 모르는 버스는 0.
        /// </summary>
        internal float BusDuckDb(AudioBus bus) => BusDuckDb(bus, _uiNow);

        /// <summary>
        /// 읽기 전용 (테스트용): uiTime(UI 시계, 초) 시점의 덕 깊이 — 편집 모드에서는 시계가 흐르지 않으므로 들어감(attack) 뒤 시각을 직접 넣어 본다
        /// </summary>
        internal float BusDuckDb(AudioBus bus, double uiTime)
        {
            int b = (int)bus;
            if (b < 0 || b >= _ducks.Length) return 0f;
            return _ducks[b].DepthDb(uiTime);
        }

        /// <summary>읽기 전용 (테스트용): 그 버스에 그 이유의 덕이 걸려 있는지 (풀리는 중 포함). 모르는 버스는 false</summary>
        internal bool BusHasDuck(AudioBus bus, int reason)
        {
            int b = (int)bus;
            if (b < 0 || b >= _ducks.Length) return false;
            return _ducks[b].Has(reason);
        }

        // ───────────── 반복 소리 ─────────────

        /// <summary>slot(0 심장 박동, 1 보스 예비 동작) 반복을 켜거나 볼륨·음높이를 바꾼다 (fade = 볼륨이 목표에 닿는 시간)</summary>
        public void SetLoop(int slot, AudioId id, float volume, float pitch, float fade)
        {
            if (slot < 0 || slot >= LoopCount || !Known(id)) return;
            ref var loop = ref _loops[slot];
            int v = LoopStart + slot;
            if (!loop.Playing || loop.Key != id)
            {
                if (_paused || _sfxGain <= 0f) return;
                var entry = ClipEntry(id);
                if (entry == null || entry.takes == null || entry.takes.Length == 0) return;
                int take = ShuffleBag.Next(ref _shuffle[id.Index], entry.takes.Length, NextRandom());
                var clip = take >= 0 ? entry.takes[take].clip : null;
                if (clip == null) return;
                var src = _src[v];
                src.Stop();
                src.clip = clip;
                src.loop = true;
                src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
                src.panStereo = 0f;
                src.priority = AudioCatalog.Keys[id.Index].UnityPriority;
                _base[v] = AudioGate.DbToGain(TrimDb(id) + entry.takes[take].gainDb);
                _level[v] = 0f;
                src.volume = 0f;
                _applied[v] = 0f;
                src.Play();
                _wantPlay[v] = true;
                _lastSample[v] = 0;
                _scheduledAt[v] = 0;
                _slots[v] = new VoiceSlot { Busy = true, Class = AudioVoiceClass.Critical, Start = _dspNow, End = double.PositiveInfinity, Key = id.Index };
                loop = new LoopState { Key = id, Volume = 0f, Pitch = pitch, Playing = true };
            }
            loop.Target = Mathf.Clamp01(volume);
            loop.TargetPitch = Mathf.Clamp(pitch, 0.5f, 2f);
            loop.Rate = fade > 0f ? 1f / fade : float.MaxValue;
        }

        public void StopLoop(int slot, float fade)
        {
            if (slot < 0 || slot >= LoopCount) return;
            ref var loop = ref _loops[slot];
            if (!loop.Playing) return;
            loop.Target = 0f;
            loop.Rate = fade > 0f ? 1f / fade : float.MaxValue;
        }

        public bool IsLoopPlaying(int slot) => slot >= 0 && slot < LoopCount && _loops[slot].Playing;

        void TickLoops(float dt)
        {
            for (int i = 0; i < LoopCount; i++)
            {
                ref var loop = ref _loops[i];
                if (!loop.Playing) continue;
                int v = LoopStart + i;
                loop.Volume = loop.Rate >= float.MaxValue ? loop.Target : Mathf.MoveTowards(loop.Volume, loop.Target, loop.Rate * dt);
                loop.Pitch = Mathf.MoveTowards(loop.Pitch, loop.TargetPitch, dt * 2f);
                _level[v] = loop.Volume;
                if (!Mathf.Approximately(_src[v].pitch, loop.Pitch)) _src[v].pitch = loop.Pitch;
                if (loop.Volume <= 0.0001f && loop.Target <= 0f)
                {
                    _src[v].Stop();
                    _slots[v].Busy = false;
                    _wantPlay[v] = false;
                    loop = default;
                }
            }
        }

        // ───────────── 잠 · 집중 · 백그라운드 ─────────────

        /// <summary>
        /// 잠 덕 (AUDIO-SPEC §3.10 · §3.11): 배경음 ×0.35 + 저역 통과, 효과음 ×0.4. 0.6초에 들어가 깰 때까지 (깨면 0.4초에 풀린다).
        /// 배경음 덕은 잠 곡(bgm_sleep)이 지금 배경음으로 골라지지 않는 동안 — 잠 곡이 없거나, 지금 상태가 제 곡을 트는 동안 (챕터 보스 곡 등, F1 · RM-3).
        /// 잠 곡이 골라지면 그 곡으로 2초 교차하고 버스 덕은 없다 (R55 기본값). 잠든 뒤에 바뀌면 TickSleepBgm이 다시 걸거나 풀어 준다
        /// </summary>
        public const float SleepBgmDb = -9f;
        public const float SleepBgmLowPassHz = 1200f;
        public const float SleepSfxDb = -8f;
        /// <summary>잠 덕이 들어가는 시간 (초)</summary>
        const float SleepAttack = 0.6f;

        public void SetSleep(bool asleep)
        {
            if (_asleep == asleep) return;
            _asleep = asleep;
            _music.SetAsleep(asleep);
            _ambience.SetAsleep(asleep);
            var bgm = _ducks[(int)AudioBus.Bgm];
            var sfx = _ducks[(int)AudioBus.Sfx];
            if (asleep)
            {
                // 잠 곡이 넘겨받지 않으면 (없음 · 보스 곡) 배경음을 먹먹하게 — 예전에는 잠 곡이 있는지만 봐서 보스 곡이 제 음량으로 울렸다 (F1)
                bool duckBgm = !_music.SleepTrackChosen;
                if (duckBgm) EngageSleepBgm();
                sfx.Sustain(DuckReason.Sleep, SleepSfxDb, SleepAttack, _uiNow);
                if (_uiSleepEnter.IsValid) PlayUi(UISfxKey.SleepEnter, 1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSleepBgm(duckBgm);
#endif
            }
            else
            {
                bgm.Release(DuckReason.Sleep, 0.4f, _uiNow);
                sfx.Release(DuckReason.Sleep, 0.4f, _uiNow);
                _bgmLpf.Clear(DuckReason.Sleep);
                _sleepBgmDucked = false;
                _sleepBgmOpening = false;
                if (_uiSleepWake.IsValid) PlayUi(UISfxKey.SleepWake, 1f);
            }
        }

        /// <summary>잠 배경음 덕을 건다 (푸는 중이었으면 지금 깊이에서 다시 들어간다 — DuckStack.Sustain)</summary>
        void EngageSleepBgm()
        {
            _ducks[(int)AudioBus.Bgm].Sustain(DuckReason.Sleep, SleepBgmDb, SleepAttack, _uiNow);
            _bgmLpf.Set(DuckReason.Sleep, SleepBgmLowPassHz);
            _sleepBgmDucked = true;
            _sleepBgmOpening = false;
        }

        /// <summary>
        /// 잠든 동안 매 프레임 (F1 · RM-3, Tick이 음악 감독 다음에 부른다): 잠 곡이 골라지지 않으면 (잠든 뒤에 챕터 보스가 나옴 등) 배경음 덕을 다시 건다.
        /// 잠 곡이 실제로 넘겨받으면 (교차 페이드 시작 — 박 기다림 뒤) 덕과 저역 통과를 MusicDirector.SleepFade(2초)에 걸쳐 함께 푼다:
        /// 나가는 곡이 갑자기 커지거나 밝아지지 않고, 잠 곡은 덕 없이 남는다. 할당 없음
        /// </summary>
        void TickSleepBgm()
        {
            if (!_asleep) return;
            if (!_music.SleepTrackChosen)
            {
                if (_sleepBgmDucked && !_sleepBgmOpening) return;
                EngageSleepBgm();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSleepBgm(true);
#endif
                return;
            }
            if (!_sleepBgmDucked) return;
            if (!_sleepBgmOpening)
            {
                if (_music.CurrentKey != _bgmSleep) return;
                _ducks[(int)AudioBus.Bgm].Release(DuckReason.Sleep, MusicDirector.SleepFade, _uiNow);
                _sleepBgmOpening = true;
                _sleepBgmOpenStart = _uiNow;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSleepBgm(false);
#endif
            }
            float k = (float)((_uiNow - _sleepBgmOpenStart) / MusicDirector.SleepFade);
            if (k < 1f)
            {
                _bgmLpf.Set(DuckReason.Sleep, SleepGlideHz(k));
                return;
            }
            _bgmLpf.Clear(DuckReason.Sleep);
            _sleepBgmDucked = false;
            _sleepBgmOpening = false;
        }

        /// <summary>잠 저역 통과가 열리는 길 (순수): k 0 → SleepBgmLowPassHz, 1 → LowPassStack.Open, 옥타브로 고르게 (k는 0~1로 자른다)</summary>
        internal static float SleepGlideHz(float k) => SleepBgmLowPassHz * Mathf.Pow(LowPassStack.Open / SleepBgmLowPassHz, Mathf.Clamp01(k));

        /// <summary>읽기 전용 (테스트용): 배경음 저역 통과의 목표 (Hz — 요청이 없으면 LowPassStack.Open)</summary>
        internal float BgmLowPassTarget => _bgmLpf.Target;

        /// <summary>테스트 전용: UI 시계(Time.unscaledTimeAsDouble)에 더하는 초 — 편집 모드에서는 시계가 흐르지 않아 덕 · 저역 통과의 시간을 테스트가 움직인다. 게임에서는 늘 0</summary>
        internal double UiClockOffset { get; set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 검증 줄 (SPEC4 S4-40 'BGM duck logged' · V2 요청 — 에디터 · 개발 빌드만): 잠들 때 한 번, 그리고 잠든 동안 배경음 덕이 다시 걸릴 때
        /// (잠 곡이 골라지지 않게 됨 — 챕터 보스 곡) · 잠 곡이 넘겨받을 때 한 번씩 (F1 · RM-3). 같은 상태가 이어지면 줄이 없다. 깰 때도 없다.
        /// "[Verify] bgm_duck=&lt;dB&gt; [bgm_lpf=&lt;Hz&gt;] bgm_sleep=&lt;길&gt; bgm_state=&lt;음악 상태&gt;"
        ///   · duck — 잠 곡이 지금 곡으로 골라지지 않는다 (잠 곡 없음 · 보스 곡처럼 제 곡을 트는 상태): 배경음 버스 SleepBgmDb + 저역 통과 SleepBgmLowPassHz
        ///   · track — 잠 곡으로 2초 교차 (버스 덕 없음, AUDIO-SPEC §3.11): dB = 잠 곡 음량 − 지금 곡(없거나 잠 곡이면 Main) 음량 (라이브러리의 LUFS 값 — 모르면 n/a)
        ///   (RM-2의 'kept' — 잠 곡은 있는데 보스 곡이 제 음량으로 울림 — 는 F1 수정 뒤로 쓰지 않는다)
        /// </summary>
        void LogSleepBgm(bool ducked)
        {
            string line = ducked
                ? "bgm_duck=" + VerifyNumber(SleepBgmDb) + " bgm_lpf=" + VerifyNumber(SleepBgmLowPassHz) + " bgm_sleep=duck"
                : "bgm_duck=" + SleepTrackDrop() + " bgm_sleep=track";
            Debug.Log("[Verify] " + line + " bgm_state=" + _music.Effective);
        }

        /// <summary>잠 곡과 지금 곡(없거나 잠 곡이면 Main)의 음량 차 (dB ≈ LU) — 둘 중 하나라도 모르면 "n/a"</summary>
        string SleepTrackDrop()
        {
            var sleepKey = AudioCatalog.Find("bgm_sleep");
            var key = _music.CurrentKey;
            int take = _music.CurrentTake;
            if (!key.IsValid || key == sleepKey)
            {
                key = AudioCatalog.Find("bgm_battle_night");
                take = Mathf.Max(0, _music.MainTake);
            }
            float sleep = TrackLufs(sleepKey, 0);
            float from = TrackLufs(key, take);
            return sleep < 0f && from < 0f ? VerifyNumber(sleep - from) : "n/a";
        }

        float TrackLufs(AudioId key, int take)
        {
            var info = Info(key, take);
            return info != null ? info.loudnessLufs : 0f;
        }

        static string VerifyNumber(float value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
#endif

        public void SetBattleFocus(BattleAudioFocus focus, int owner)
        {
            for (int i = 0; i < _focusCount; i++)
            {
                if (_focusOwners[i] != owner) continue;
                _focusValues[i] = focus;
                RecomputeFocus();
                return;
            }
            if (_focusCount >= _focusOwners.Length)
            {
                for (int j = 0; j < _focusCount - 1; j++)
                {
                    _focusOwners[j] = _focusOwners[j + 1];
                    _focusValues[j] = _focusValues[j + 1];
                }
                _focusCount--;
            }
            _focusOwners[_focusCount] = owner;
            _focusValues[_focusCount] = focus;
            _focusCount++;
            RecomputeFocus();
        }

        public void ClearBattleFocus(int owner)
        {
            for (int i = 0; i < _focusCount; i++)
            {
                if (_focusOwners[i] != owner) continue;
                for (int j = i; j < _focusCount - 1; j++)
                {
                    _focusOwners[j] = _focusOwners[j + 1];
                    _focusValues[j] = _focusValues[j + 1];
                }
                _focusCount--;
                RecomputeFocus();
                return;
            }
        }

        void RecomputeFocus()
        {
            var focus = BattleAudioFocus.Full;
            for (int i = 0; i < _focusCount; i++)
            {
                if (_focusValues[i] > focus) focus = _focusValues[i];
            }
            if (focus == _focus) return;
            _focus = focus;
            var sfx = _ducks[(int)AudioBus.Sfx];
            var amb = _ducks[(int)AudioBus.Amb];
            var loop = _ducks[(int)AudioBus.Loop];
            if (focus == BattleAudioFocus.Dimmed)
            {
                sfx.Sustain(DuckReason.FocusDimmed, -6f, 0.15f, _uiNow);
                amb.Sustain(DuckReason.FocusDimmed, -4f, 0.15f, _uiNow);
                _bgmLpf.Set(DuckReason.FocusDimmed, 6000f);
            }
            else
            {
                sfx.Release(DuckReason.FocusDimmed, 0.25f, _uiNow);
                amb.Release(DuckReason.FocusDimmed, 0.25f, _uiNow);
                _bgmLpf.Clear(DuckReason.FocusDimmed);
            }
            if (focus == BattleAudioFocus.Hidden)
            {
                amb.Sustain(DuckReason.FocusHidden, -12f, 0.2f, _uiNow);
                loop.Sustain(DuckReason.FocusHidden, -12f, 0.2f, _uiNow);
                _scheduler.FlushBattle();
            }
            else
            {
                amb.Release(DuckReason.FocusHidden, 0.3f, _uiNow);
                loop.Release(DuckReason.FocusHidden, 0.3f, _uiNow);
            }
            _music.SetHidden(focus == BattleAudioFocus.Hidden);
            _ambience.SetHidden(focus == BattleAudioFocus.Hidden);
        }

        /// <summary>
        /// 모바일 백그라운드 (OnApplicationPause): true면 Bgm/Amb/Loop를 0.2초에 줄였다가 멈추고, Ui/Sfx는 끊고, 대기열을 비운다.
        /// false면 다시 틀고 0.4초에 걸쳐 올린다. 포커스 잃음(OnApplicationFocus)은 받지 않는다 (M01)
        /// </summary>
        public void OnPause(bool paused)
        {
            if (paused == _paused) return;
            _paused = paused;
            if (paused)
            {
                for (int v = UiStart; v < StingerStart + StingerCount; v++)
                {
                    if (_src[v].isPlaying) _src[v].Stop();
                    _slots[v].Busy = false;
                }
                _scheduler.Clear();
                _pendingJingle = AudioId.None;
                _stingerFading = false;
                return;
            }
            if (_sourcesPaused)
            {
                for (int v = LoopStart; v < VoiceCount; v++) _src[v].UnPause();
                _sourcesPaused = false;
                // 배경음을 꺼 둔 상태였으면 다시 멈춰 둔다
                _bgmSourcesMuted = false;
            }
            SyncBgmPause();
        }

        void TickPause(float dt)
        {
            float target = _paused ? 0f : 1f;
            if (Mathf.Approximately(_pauseFade, target)) return;
            float rate = _paused ? 1f / PauseFadeOut : 1f / PauseFadeIn;
            _pauseFade = Mathf.MoveTowards(_pauseFade, target, rate * dt);
            if (_paused && _pauseFade <= 0f && !_sourcesPaused)
            {
                for (int v = LoopStart; v < VoiceCount; v++) _src[v].Pause();
                _sourcesPaused = true;
            }
        }

        // ───────────── 오디오 장치 바뀜 ─────────────

        /// <summary>
        /// 오디오 설정 · 장치가 바뀜 (AudioSettings.OnAudioConfigurationChanged — 이어폰 · 블루투스 · HDMI · AudioSettings.Reset).
        /// Unity는 이때 울리던 소스를 모두 멈춘다: 다음 Tick에서 음악 · 환경음 · 반복 소리를 멈춘 자리에서 다시 튼다
        /// </summary>
        public void NotifyAudioConfigurationChanged() => _audioReset = true;

        /// <summary>
        /// 엔진 DSP 시계 (순수 — AudioEngineSmokeTests): raw dspTime + dspBase. 장치가 다시 시작해 raw가 DspBackstep보다 크게 뒤로 가면
        /// dspBase를 옮겨 마지막 값(last)에서 이어 가고 reset을 켠다 — 게이트 · 목소리 끝 시각 · 음악 페이드가 몇 시간씩 멈추지 않게
        /// </summary>
        internal static double MonotonicDsp(double raw, double last, ref double dspBase, ref bool reset)
        {
            double now = raw + dspBase;
            if (now >= last - DspBackstep) return now;
            dspBase = last - raw;
            reset = true;
            return last;
        }

        /// <summary>장치가 바뀐 뒤: 멈춘 한 방 소리 목소리를 비우고, 계속 울려야 하는 소스(반복 · 음악 · 환경음)를 마지막 자리에서 다시 튼다</summary>
        void RecoverAfterAudioReset()
        {
            _audioReset = false;
            RefreshLatency();
            for (int v = 0; v < VoiceCount; v++)
            {
                var src = _src[v];
                if (src == null) continue;
                if (v < LoopStart)
                {
                    if (_slots[v].Busy && !src.isPlaying) _slots[v].Busy = false;
                    continue;
                }
                if (!_wantPlay[v] || src.clip == null || src.isPlaying) continue;
                bool scheduled = _scheduledAt[v] > _dspNow + 0.001;
                // 한 번 도는 도입부는 다시 틀지 않는다 (본 곡이 다른 덱에 예약돼 있다)
                if (!src.loop && !scheduled) continue;
                int samples = src.clip.samples;
                if (samples > 0) src.timeSamples = scheduled ? 0 : Mathf.Clamp(_lastSample[v], 0, samples - 1);
                if (scheduled) src.PlayScheduled(_scheduledAt[v] - _dspBase);
                else src.Play();
                if (_sourcesPaused || (v >= BgmStart && _bgmSourcesMuted)) src.Pause();
            }
        }

        /// <summary>계속 울리는 소스의 재생 자리를 적어 둔다 (0.25초마다 — 장치가 바뀌면 여기서 이어 튼다)</summary>
        void TrackPositions()
        {
            for (int v = LoopStart; v < VoiceCount; v++)
            {
                if (!_wantPlay[v]) continue;
                var src = _src[v];
                if (src != null && src.isPlaying) _lastSample[v] = src.timeSamples;
            }
        }

        /// <summary>배경음 볼륨 0 · 소리 끔: Bgm/Amb 소스를 멈춰 둔다 (자리 유지, 디코딩 없음)</summary>
        void SyncBgmPause()
        {
            if (_bgmMuted == _bgmSourcesMuted || _sourcesPaused) return;
            for (int v = BgmStart; v < VoiceCount; v++)
            {
                if (_bgmMuted) _src[v].Pause();
                else _src[v].UnPause();
            }
            _bgmSourcesMuted = _bgmMuted;
        }

        // ───────────── 매 프레임 ─────────────

        /// <summary>UISfx.LateUpdate가 부른다 (dt = Time.deltaTime, unscaledDt = 실제 시간)</summary>
        public void Tick(float dt, float unscaledDt)
        {
            _unscaledDt = Mathf.Clamp(unscaledDt, 0f, 0.25f);
            _dspNow = MonotonicDsp(AudioSettings.dspTime, _dspNow, ref _dspBase, ref _audioReset);
            if (_audioReset) RecoverAfterAudioReset();
            _uiNow = Time.unscaledTimeAsDouble + UiClockOffset;
            _scheduler.SetUiNow(_uiNow);
            if (!_paused) _scheduler.AdvanceFx(Mathf.Max(0f, dt) * _fxScale);

            if (!_paused) CommitDue();
            TickStinger();
            TickLoops(_unscaledDt);
            TickPause(_unscaledDt);

            bool bgmRunning = !_paused && !_bgmMuted;
            float musicDt = bgmRunning ? _unscaledDt : 0f;
            _music.Update(_dspNow, musicDt);
            if (!_musicStarted && _music.Started)
            {
                _musicStarted = true;
                _ambience.SetActive(true, _uiNow);
            }
            _ambience.SetMusicState(_music.State);
            _ambience.SetResting(_music.IsResting);
            _ambience.Update(_uiNow, musicDt);
            TickSleepBgm();

            for (int i = 0; i < _ducks.Length; i++) _ducks[i].Tick(_uiNow);
            TickFilters(_unscaledDt);
            RefreshVolumes(false);
            TickStats();
            TickListener();
        }

        void TickFilters(float dt)
        {
            bool bgm = _bgmLpf.Tick(dt) || _bgmLpf.Enabled;
            bool amb = _ambLpf.Tick(dt) || _ambLpf.Enabled;
            ApplyFilter(BgmStart, BgmCount, _bgmLpf, bgm);
            ApplyFilter(AmbStart, AmbCount, _ambLpf, amb);
        }

        void ApplyFilter(int start, int count, LowPassStack stack, bool active)
        {
            for (int i = 0; i < count; i++)
            {
                var filter = _lpf[start + i];
                if (filter == null) continue;
                bool on = active && stack.Enabled;
                if (filter.enabled != on) filter.enabled = on;
                if (on && !Mathf.Approximately(filter.cutoffFrequency, stack.Current)) filter.cutoffFrequency = stack.Current;
            }
        }

        // ───────────── 대기열 → 소리 ─────────────

        void CommitDue()
        {
            if (_scheduler.Count == 0) return;
            bool slow = _fxScale < 0.999f;
            double look = Math.Max(0.012, 1.5 * _unscaledDt);
            double fx = _scheduler.FxNow;
            double fxHorizon = slow ? fx : fx + look;
            double lead = !slow && _latencyComp ? _latencySeconds : 0;
            var items = _scheduler.Items;
            for (int guard = 0; guard < AudioScheduler.Capacity; guard++)
            {
                int i = _scheduler.EarliestDue(fxHorizon, _uiNow + look, lead);
                if (i < 0) break;
                var item = items[i];
                _scheduler.RemoveAt(i);
                if (item.Has(AudioPlayFlags.Hit) && !item.Has(AudioPlayFlags.Kept) && FoldCrowd(ref item)) continue;
                bool ui = item.Has(AudioPlayFlags.UiClock);
                double remaining = item.Due - (ui ? _uiNow : fx);
                if (slow && !ui) remaining = 0;
                if (item.Has(AudioPlayFlags.Predicted) && _latencyComp && remaining >= _latencySeconds) remaining -= _latencySeconds;
                Commit(ref item, _dspNow + Math.Max(0, remaining));
            }
        }

        /// <summary>
        /// 군중 묶음 (§5.6): 이 명중과 같은 60ms 창에 울릴 명중이 모두 4개 이상이면 가장 중요한 2개만 따로 울리고
        /// 나머지는 버리고 hit_multi 하나를 창 가운데 시각 · 평균 좌우에 (−9 + 3·log2(N/2) dB). 이 명중이 버려지면 true
        /// </summary>
        bool FoldCrowd(ref PendingSound item)
        {
            if (!_scheduler.FoldCrowd(in item, CrowdWindow, CrowdMin, _hitScratch, out bool kept, out int total, out double meanDue, out float meanPan,
                out int folded)) return false;
            _stats.Folded += folded + (kept ? 0 : 1);
            if (_hitMulti.IsValid && HasClips(_hitMulti))
            {
                var multi = new PendingSound
                {
                    Key = _hitMulti.Index,
                    Volume = 1f,
                    Pitch = 1f,
                    GainDb = AudioScheduler.CrowdGainDb(total),
                    Pan = meanPan,
                    Class = AudioVoiceClass.Normal,
                    Flags = AudioPlayFlags.Battle | AudioPlayFlags.Predicted | AudioPlayFlags.BypassBudget,
                    EnemyId = -1,
                    Due = meanDue,
                };
                _scheduler.Insert(ref multi);
            }
            return !kept;
        }

        /// <summary>예약 하나를 실제 목소리로 (dspStart = 울릴 DSP 시각)</summary>
        void Commit(ref PendingSound item, double dspStart)
        {
            if (item.Key < 0 || item.Key >= AudioCatalog.Count) return;
            var id = new AudioId(item.Key);
            var def = AudioCatalog.Keys[item.Key];
            bool battle = item.Has(AudioPlayFlags.Battle);
            bool emitter = item.Has(AudioPlayFlags.Emitter);
            if (battle && _focus == BattleAudioFocus.Hidden) return;
            if (def.Has(AudioKeyFlags.Vox) && !CanBark()) return;
            if (def.Bus == AudioBus.Stinger)
            {
                PlayJingle(id, item.Volume);
                return;
            }
            if (def.Bus == AudioBus.Bgm || def.Bus == AudioBus.Loop || (def.Bus == AudioBus.Amb && !emitter)) return;
            var bus = emitter ? AudioBus.Sfx : def.Bus;
            if (emitter ? _busGain[(int)AudioBus.Amb] <= 0f : _busGain[(int)bus] <= 0f) return;

            if (battle && !item.Has(AudioPlayFlags.BypassBudget))
            {
                float rate = BudgetRate;
                int cat = (int)item.Category;
                bool lowClass = item.Class >= AudioVoiceClass.Normal;
                if (cat > 0 && cat < _buckets.Length && !_buckets[cat].TryTake(_uiNow, rate) && lowClass)
                {
                    _stats.BudgetHits++;
                    if (item.Category == AudioCategory.Hit) _stats.Folded++;
                    return;
                }
                if (!_battleStarts.TryTake(_uiNow, rate) && lowClass)
                {
                    _stats.BudgetHits++;
                    return;
                }
            }

            var entry = ClipEntry(id);
            if (entry == null || entry.takes == null || entry.takes.Length == 0)
            {
                _stats.Missing++;
                return;
            }
            float pitch = item.Pitch > 0f ? item.Pitch : 1f;
            int take;
            if (def.IsTuned && item.TargetHz > 0f)
            {
                take = PickTuned(entry, def, id, item.TargetHz, out float tunedPitch);
                pitch *= tunedPitch;
            }
            else
            {
                take = ShuffleBag.Next(ref _shuffle[id.Index], entry.takes.Length, NextRandom());
                if (!def.IsTuned) pitch *= AudioGate.PitchJitter(def.PitchJitter, NextRandom());
            }
            if (take < 0) return;
            var clip = entry.takes[take].clip;
            if (clip == null)
            {
                _stats.Missing++;
                return;
            }
            if (bus == AudioBus.Sfx && IsSlowMotion && !item.Has(AudioPlayFlags.NoDrag)) pitch *= TimeDragPitch;
            pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            float duration = clip.length / pitch;
            if (!_gate.TryPass(id.Index, def.MinInterval, def.MaxVoices, dspStart, duration))
            {
                _stats.GateBlocks++;
                return;
            }
            float gainDb = item.GainDb + TrimDb(id) + entry.takes[take].gainDb + AudioGate.GainJitterDb(def.VolumeJitterDb, NextRandom());
            int exempt = (item.Has(AudioPlayFlags.ExemptLamp) ? DuckExempt.Lamp : 0) | (item.Has(AudioPlayFlags.ExemptBoss) ? DuckExempt.Boss : 0) |
                (item.Has(AudioPlayFlags.ExemptCrit) ? DuckExempt.Crit : 0);
            var cls = emitter ? AudioVoiceClass.Low : item.Class;
            int v = StartVoice(bus, id, in def, clip, item.Volume, gainDb, pitch, item.Pan, cls, exempt, dspStart, emitter);
            if (v < 0) return;
            _stats.Committed++;
            if (battle)
            {
                _startsThisSecond++;
            }
            if (def.Has(AudioKeyFlags.Vox)) _lastBark = _uiNow;
            if (item.Has(AudioPlayFlags.MicroDuck))
                _ducks[(int)AudioBus.Sfx].Trigger(DuckReason.CritMicro, -4f, 0.005f, 0.06f, 0.12f, _uiNow + Math.Max(0, dspStart - _dspNow), DuckExempt.Crit);
        }

        bool CanBark() => AudioService.VoiceBarks && _uiNow - _lastBark >= BarkInterval;

        /// <summary>조율 키: 목표 음에 가장 가까운 근음의 테이크 + 음높이 배율</summary>
        int PickTuned(AudioLibrary.Entry entry, in AudioKeyDef def, AudioId id, float targetHz, out float pitch)
        {
            int count = Mathf.Min(entry.takes.Length, _rootScratch.Length);
            bool anyRoot = false;
            for (int i = 0; i < count; i++)
            {
                float root = entry.takes[i].rootHz > 0f ? entry.takes[i].rootHz : def.TargetRootHz;
                _rootScratch[i] = entry.takes[i].clip != null ? root : 0f;
                anyRoot |= _rootScratch[i] > 0f;
            }
            if (!anyRoot)
            {
                pitch = 1f;
                return ShuffleBag.Next(ref _shuffle[id.Index], entry.takes.Length, NextRandom());
            }
            int take = PitchLadder.NearestTake(targetHz, _rootScratch, count, NextRandom());
            if (take < 0)
            {
                pitch = 1f;
                return -1;
            }
            pitch = PitchLadder.PitchFor(targetHz, _rootScratch[take]);
            return take;
        }

        /// <summary>지금 곡의 옮김 (반음) — 조율 키가 따라간다 (지금 곡은 모두 0)</summary>
        int MusicTranspose()
        {
            int deck = _music.ActiveDeck;
            if (deck < 0 || _library == null) return 0;
            var entry = _library.Get(_music.CurrentKey);
            var info = entry != null ? MusicInfoOf(entry, _music.CurrentTake) : null;
            return info != null ? info.transposeSemitones : 0;
        }

        /// <summary>전투 조율 키가 쓸 옮김</summary>
        public int Transpose => MusicTranspose();

        int StartVoice(AudioBus bus, AudioId id, in AudioKeyDef def, AudioClip clip, float volume, float gainDb, float pitch, float pan,
            AudioVoiceClass voiceClass, int exempt, double dspStart, bool emitter)
        {
            int b = (int)bus;
            int v = AudioVoicePool.Choose(_slots, BusStart[b], BusSize[b], voiceClass, _dspNow, out bool steal);
            if (v < 0)
            {
                _stats.Drops[b]++;
                return -1;
            }
            if (steal) _stats.Steals[b]++;
            var src = _src[v];
            src.Stop();
            src.clip = clip;
            src.loop = false;
            src.pitch = pitch;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.priority = emitter ? EmitterPriority : bus == AudioBus.Ui ? UiPriority : def.UnityPriority;
            _base[v] = Mathf.Max(0f, volume) * AudioGate.DbToGain(gainDb);
            _exempt[v] = exempt;
            _emitter[v] = emitter;
            _level[v] = 1f;
            float vol = VoiceVolume(v);
            src.volume = vol;
            _applied[v] = vol;
            double begin = dspStart > _dspNow + 0.0005 ? dspStart : _dspNow;
            if (begin > _dspNow) src.PlayScheduled(begin - _dspBase);
            else src.Play();
            _slots[v] = new VoiceSlot
            {
                Busy = true,
                Class = voiceClass,
                Start = begin,
                End = begin + clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)),
                Key = id.Index,
            };
            return v;
        }

        float VoiceVolume(int v)
        {
            var bus = _voiceBus[v];
            float gain;
            switch (bus)
            {
                case AudioBus.Bgm:
                case AudioBus.Amb:
                    gain = _busGain[(int)bus] * _level[v] * _pauseFade * AudioGate.DbToGain(_ducks[(int)bus].DepthDb(_uiNow));
                    return Mathf.Clamp01(gain);
                case AudioBus.Loop:
                    gain = _busGain[(int)bus] * _level[v] * _base[v] * _pauseFade * AudioGate.DbToGain(_ducks[(int)bus].DepthDb(_uiNow));
                    return Mathf.Clamp01(gain);
            }
            // 빌린 환경음 한 방은 환경음 버스 볼륨 · 덕을 따른다
            var gainBus = _emitter[v] ? AudioBus.Amb : bus;
            float duck = _ducks[(int)gainBus].DepthDb(_uiNow, _exempt[v]);
            gain = _busGain[(int)gainBus] * _base[v] * _level[v] * AudioGate.DbToGain(duck);
            return Mathf.Clamp01(gain);
        }

        void RefreshVolumes(bool force)
        {
            for (int v = 0; v < VoiceCount; v++)
            {
                var src = _src[v];
                if (src == null) continue;
                bool active = _slots[v].Busy || v >= BgmStart;
                if (!active && !force) continue;
                if (_slots[v].Busy && v < LoopStart && _slots[v].End <= _dspNow)
                {
                    _slots[v].Busy = false;
                    continue;
                }
                float vol = VoiceVolume(v);
                if (!force && Mathf.Abs(vol - _applied[v]) < 0.0005f) continue;
                src.volume = vol;
                _applied[v] = vol;
            }
        }

        void TickStats()
        {
            if (_uiNow - _startsWindow >= 1.0)
            {
                _stats.BattleStartsLastSecond = _startsThisSecond;
                _startsThisSecond = 0;
                _startsWindow = _uiNow;
            }
            if (_uiNow - _statsTime < 0.25) return;
            _statsTime = _uiNow;
            TrackPositions();
            for (int b = 0; b < AudioStats.BusCount; b++)
            {
                int used = 0;
                for (int i = 0; i < BusSize[b]; i++)
                {
                    int v = BusStart[b] + i;
                    if (_slots[v].Busy && _slots[v].End > _dspNow) used++;
                    else if (v >= BgmStart && _src[v].isPlaying) used++;
                }
                _stats.VoicesInUse[b] = used;
            }
        }

        /// <summary>
        /// 듣는 귀 지킴이 (§3.15): 장면에 켜진 AudioListener가 없으면 "[Audio]"에 하나 달고, 장면 쪽이 생기면 뗀다 (2초마다 확인 — 둘이면 경고가 뜬다)
        /// </summary>
        void TickListener()
        {
            // 우리 귀를 달았을 때만 살핀다 (장면 쪽 귀가 생기면 떼야 경고가 없다). 평소에는 장면을 불러올 때만 (UISfx.OnSceneLoaded)
            if (_ownListener == null || _uiNow - _listenerCheck < 2.0) return;
            _listenerCheck = _uiNow;
            CheckListener();
        }

        public void CheckListener()
        {
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            bool other = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] == null || listeners[i] == _ownListener) continue;
                if (listeners[i].isActiveAndEnabled) other = true;
            }
            if (other && _ownListener != null)
            {
                UnityEngine.Object.Destroy(_ownListener);
                _ownListener = null;
            }
            else if (!other && _ownListener == null && _root != null)
            {
                _ownListener = _root.AddComponent<AudioListener>();
            }
        }

        // ───────────── IMusicOutput ─────────────

        /// <summary>
        /// 음악 테이크 수. Main 곡이 라이브러리에 없고 예전 표에 배경음이 있으면 그 하나를 Main으로 쓴다 (§3.2)
        /// </summary>
        public int TakeCount(AudioId key)
        {
            if (!key.IsValid) return 0;
            var entry = ClipEntry(key);
            int count = entry != null && entry.takes != null ? entry.takes.Length : 0;
            if (count == 0 && _legacy != null && _legacy.bgm != null && AudioCatalog.Keys[key.Index].Key == "bgm_battle_night") return 1;
            return count;
        }

        AudioClip MusicClip(AudioId key, int take)
        {
            var entry = ClipEntry(key);
            if (entry != null && entry.takes != null && take >= 0 && take < entry.takes.Length) return entry.takes[take].clip;
            if (_legacy != null && _legacy.bgm != null && key.IsValid && AudioCatalog.Keys[key.Index].Key == "bgm_battle_night") return _legacy.bgm;
            return null;
        }

        public int Samples(AudioId key, int take)
        {
            var clip = MusicClip(key, take);
            return clip != null ? clip.samples : 0;
        }

        public int Frequency(AudioId key, int take)
        {
            var clip = MusicClip(key, take);
            return clip != null ? clip.frequency : 0;
        }

        public AudioLibrary.MusicInfo Info(AudioId key, int take)
        {
            var entry = ClipEntry(key);
            return entry != null ? MusicInfoOf(entry, take) : null;
        }

        static AudioLibrary.MusicInfo MusicInfoOf(AudioLibrary.Entry entry, int take)
        {
            if (entry.takeMusic != null && take >= 0 && take < entry.takeMusic.Length && entry.takeMusic[take] != null && entry.takeMusic[take].bpm > 0f)
                return entry.takeMusic[take];
            return entry.HasMusicInfo ? entry.music : null;
        }

        void IMusicOutput.Play(int deck, AudioId key, int take, int startSample, double dspStart, bool loop)
        {
            if (deck < 0 || deck >= BgmCount) return;
            var clip = MusicClip(key, take);
            if (clip == null) return;
            int v = BgmStart + deck;
            var src = _src[v];
            src.Stop();
            src.clip = clip;
            src.loop = loop;
            src.pitch = 1f;
            src.panStereo = 0f;
            src.priority = 0;
            _level[v] = 0f;
            var musicEntry = ClipEntry(key);
            float takeGain = musicEntry != null && musicEntry.takes != null && take >= 0 && take < musicEntry.takes.Length ? musicEntry.takes[take].gainDb : 0f;
            // 예전 표의 배경음이면 그 표의 볼륨 (§3.2: 라이브러리에 Main 곡이 없을 때만 쓴다)
            _base[v] = _legacy != null && clip == _legacy.bgm ? Mathf.Clamp01(_legacy.bgmVolume) : AudioGate.DbToGain(TrimDb(key) + takeGain);
            src.volume = 0f;
            _applied[v] = 0f;
            if (clip.samples > 0) src.timeSamples = Mathf.Clamp(startSample, 0, clip.samples - 1);
            bool scheduled = dspStart > _dspNow + 0.001;
            if (scheduled) src.PlayScheduled(dspStart - _dspBase);
            else src.Play();
            _wantPlay[v] = true;
            _lastSample[v] = Mathf.Max(0, startSample);
            _scheduledAt[v] = scheduled ? dspStart : 0;
            if (_bgmMuted || _sourcesPaused) src.Pause();
        }

        void IMusicOutput.Stop(int deck)
        {
            if (deck < 0 || deck >= BgmCount) return;
            int v = BgmStart + deck;
            _src[v].Stop();
            _level[v] = 0f;
            _wantPlay[v] = false;
        }

        void IMusicOutput.SetLevel(int deck, float level)
        {
            if (deck < 0 || deck >= BgmCount) return;
            int v = BgmStart + deck;
            // _base는 Play가 정한다 (예전 표 bgmVolume 0 = 조용 — 0을 1로 바꾸지 않는다)
            _level[v] = Mathf.Max(0f, level) * _base[v];
        }

        int IMusicOutput.TimeSamples(int deck) => deck >= 0 && deck < BgmCount ? _src[BgmStart + deck].timeSamples : 0;

        bool IMusicOutput.IsPlaying(int deck) => deck >= 0 && deck < BgmCount && _src[BgmStart + deck].isPlaying;

        // ───────────── IAmbienceOutput ─────────────

        void IAmbienceOutput.Play(int layer, AudioId key, int take, float startShare)
        {
            if (layer < 0 || layer >= AmbCount) return;
            var entry = ClipEntry(key);
            if (entry == null || entry.takes == null || entry.takes.Length == 0) return;
            take = Mathf.Clamp(take, 0, entry.takes.Length - 1);
            var clip = entry.takes[take].clip;
            if (clip == null) return;
            int v = AmbStart + layer;
            var src = _src[v];
            src.Stop();
            src.clip = clip;
            src.loop = true;
            src.pitch = 1f;
            src.panStereo = 0f;
            src.priority = 16;
            _base[v] = AudioGate.DbToGain(TrimDb(key) + entry.takes[take].gainDb);
            _level[v] = 0f;
            src.volume = 0f;
            _applied[v] = 0f;
            int start = clip.samples > 0 ? Mathf.Clamp((int)(Mathf.Clamp01(startShare) * clip.samples), 0, clip.samples - 1) : 0;
            if (clip.samples > 0) src.timeSamples = start;
            src.Play();
            _wantPlay[v] = true;
            _lastSample[v] = start;
            _scheduledAt[v] = 0;
            if (_bgmMuted || _sourcesPaused) src.Pause();
        }

        void IAmbienceOutput.Stop(int layer)
        {
            if (layer < 0 || layer >= AmbCount) return;
            int v = AmbStart + layer;
            _src[v].Stop();
            _level[v] = 0f;
            _wantPlay[v] = false;
        }

        void IAmbienceOutput.SetLevel(int layer, float level)
        {
            if (layer < 0 || layer >= AmbCount) return;
            int v = AmbStart + layer;
            _level[v] = Mathf.Max(0f, level) * _base[v];
        }

        bool IAmbienceOutput.IsPlaying(int layer) => layer >= 0 && layer < AmbCount && _src[AmbStart + layer].isPlaying;

        public void Emit(AudioId key, float gainDb, float pan)
        {
            if (!Known(key) || _paused) return;
            var def = AudioCatalog.Keys[key.Index];
            var sound = new PendingSound
            {
                Key = key.Index,
                Volume = 1f,
                Pitch = 1f,
                GainDb = gainDb,
                Pan = pan,
                Class = AudioVoiceClass.Low,
                EnemyId = -1,
                Flags = AudioPlayFlags.Emitter | AudioPlayFlags.UiClock | AudioPlayFlags.BypassBudget,
            };
            if (def.Bus != AudioBus.Amb) sound.Flags &= ~AudioPlayFlags.Emitter;
            Commit(ref sound, _dspNow);
        }

        void IAmbienceOutput.SetLoaded(AudioId key, bool loaded)
        {
            var entry = ClipEntry(key);
            if (entry == null || entry.takes == null) return;
            for (int i = 0; i < entry.takes.Length; i++)
            {
                var clip = entry.takes[i].clip;
                if (clip == null) continue;
                if (loaded)
                {
                    if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                    continue;
                }
                bool playing = false;
                for (int v = AmbStart; v < VoiceCount; v++) playing |= _src[v].clip == clip;
                if (!playing && clip.loadState == AudioDataLoadState.Loaded) clip.UnloadAudioData();
            }
        }

        /// <summary>장면 다시 불러오기 (ResetData): 사라진 화면의 음악 · 집중 요청과 잠을 비우고 Main으로 (소리는 내지 않는다)</summary>
        public void ResetForScene()
        {
            _music.ClearRequests();
            _music.SetBase(MusicState.Main);
            _focusCount = 0;
            RecomputeFocus();
            if (_asleep)
            {
                _asleep = false;
                _music.SetAsleep(false);
                _ambience.SetAsleep(false);
                _ducks[(int)AudioBus.Bgm].Release(DuckReason.Sleep, 0.4f, _uiNow);
                _ducks[(int)AudioBus.Sfx].Release(DuckReason.Sleep, 0.4f, _uiNow);
                _bgmLpf.Clear(DuckReason.Sleep);
                _sleepBgmDucked = false;
                _sleepBgmOpening = false;
            }
            _scheduler.FlushBattle();
        }

        /// <summary>모든 소리를 멈춘다 (에디터 · 테스트 · 끄기)</summary>
        public void StopAll()
        {
            _scheduler.Clear();
            _music.StopAll();
            _ambience.StopAll();
            for (int v = 0; v < VoiceCount; v++)
            {
                if (_src[v] != null) _src[v].Stop();
                _slots[v].Busy = false;
                _level[v] = 0f;
                _wantPlay[v] = false;
            }
            for (int i = 0; i < LoopCount; i++) _loops[i] = default;
            for (int i = 0; i < _ducks.Length; i++) _ducks[i].Clear();
            // 잠 배경음 덕도 함께 지웠다: 아직 잠 중이고 잠 곡이 골라지지 않으면 다음 Tick이 다시 건다
            if (_sleepBgmDucked) _bgmLpf.Clear(DuckReason.Sleep);
            _sleepBgmDucked = false;
            _sleepBgmOpening = false;
        }
    }
}
