using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>환경음 소스 넷(바닥 · 기믹 A/B · 보스)과 한 방 소리를 실제로 울리는 쪽 (AudioEngine, 테스트는 가짜)</summary>
    public interface IAmbienceOutput
    {
        int TakeCount(AudioId key);
        /// <summary>layer에서 key/take를 반복으로 튼다 (startShare 0~1 = 시작 자리 — 반복 클립이라 어디서 시작해도 이음매가 없다)</summary>
        void Play(int layer, AudioId key, int take, float startShare);
        void Stop(int layer);
        /// <summary>층 음량 곡선 (0~1+, 버스 볼륨 · 덕은 출력 쪽이 곱하고 1로 자른다)</summary>
        void SetLevel(int layer, float level);
        bool IsPlaying(int layer);
        /// <summary>한 방 소리 (빌린 Low 등급 Sfx 목소리, 환경음 볼륨)</summary>
        void Emit(AudioId key, float gainDb, float pan);
        /// <summary>클립 데이터를 미리 올리거나(true) 내린다 — preloadAudioData = false인 환경음 (§3.12)</summary>
        void SetLoaded(AudioId key, bool loaded);
    }

    /// <summary>
    /// 환경음 감독 (AUDIO-SPEC §3.12, 순수 C#):
    ///   · 바닥: amb_night (던전 음악 상태면 amb_dungeon, 잠이면 amb_sleep +3 dB). 소환·이야기 화면 아래에서는 −6 dB. 정전이면 바닥 −6 dB.
    ///   · 기믹 층: 스테이지마다 sim.StageModifier → amb_rain / wind / dust / blackout / fullmoon / festival, A/B를 2초에 교차. 없음 = 조용.
    ///   · 보스 층: 보스가 살아 있는 동안 amb_boss (1.5초 페이드).
    ///   · 음악 쉼 동안 +3 dB (쉼이 곧 환경음이다).
    ///   · 한 방: 12~30초마다 지금 층의 묶음(비/바람/축제, 아니면 밤)에서 하나, 좌우 ±0.6, −6…−12 dB. 징글 · 보스 처치 순간 · 전장 숨김 동안은 쉰다.
    ///   · 쓰지 않은 지 2스테이지 지난 기믹 층 클립은 내린다.
    /// </summary>
    public sealed class AmbienceDirector
    {
        public const int LayerBase = 0;
        public const int LayerModA = 1;
        public const int LayerModB = 2;
        public const int LayerBoss = 3;
        public const int LayerCount = 4;

        public const float ModifierFade = 2f;
        public const float BossFade = 1.5f;
        public const float BaseFade = 1f;
        public const float BaseSwapFade = 0.6f;
        public const float EmitMin = 12f;
        public const float EmitMax = 30f;
        public const float EmitRetry = 3f;
        public const float EmitGainMin = -12f;
        public const float EmitGainMax = -6f;
        public const float EmitPan = 0.6f;
        public const float MenuDuckDb = -6f;
        public const float BlackoutBaseDb = -6f;
        public const float SleepBoostDb = 3f;
        public const float RestBoostDb = 3f;
        /// <summary>쓰지 않은 지 이만큼 스테이지가 지나면 클립 데이터를 내린다</summary>
        public const int UnloadAfterStages = 2;

        struct Layer
        {
            public AudioId Key;
            public float Level;
            public float Target;
            public float Rate;
            public bool Playing;
        }

        readonly IAmbienceOutput _out;
        readonly Func<float> _random;
        readonly Layer[] _layers = new Layer[LayerCount];
        readonly AudioId _night;
        readonly AudioId _dungeon;
        readonly AudioId _sleep;
        readonly AudioId _boss;
        readonly AudioId[] _modKeys = new AudioId[7];
        readonly int[] _modUsedStage = new int[7];
        readonly bool[] _modLoaded = new bool[7];
        readonly AudioId _emitNight;
        readonly AudioId _emitRain;
        readonly AudioId _emitWind;
        readonly AudioId _emitFestival;

        bool _active;
        bool _bossAlive;
        bool _asleep;
        bool _resting;
        bool _hidden;
        MusicState _music = MusicState.Main;
        StageModifierType _modifier;
        int _modLayer = LayerModA;
        int _stage;
        double _suppressUntil;
        double _nextEmit = -1;
        double _now;

        public AmbienceDirector(IAmbienceOutput output, Func<float> random01)
        {
            _out = output;
            _random = random01 ?? (() => 0.5f);
            _night = AudioCatalog.Find("amb_night");
            _dungeon = AudioCatalog.Find("amb_dungeon");
            _sleep = AudioCatalog.Find("amb_sleep");
            _boss = AudioCatalog.Find("amb_boss");
            _modKeys[(int)StageModifierType.None] = AudioId.None;
            _modKeys[(int)StageModifierType.Rain] = AudioCatalog.Find("amb_rain");
            _modKeys[(int)StageModifierType.Blackout] = AudioCatalog.Find("amb_blackout");
            _modKeys[(int)StageModifierType.FullMoon] = AudioCatalog.Find("amb_fullmoon");
            _modKeys[(int)StageModifierType.Wind] = AudioCatalog.Find("amb_wind");
            _modKeys[(int)StageModifierType.DustStorm] = AudioCatalog.Find("amb_dust");
            _modKeys[(int)StageModifierType.Festival] = AudioCatalog.Find("amb_festival");
            _emitNight = AudioCatalog.Find("amb_emit_night");
            _emitRain = AudioCatalog.Find("amb_emit_rain");
            _emitWind = AudioCatalog.Find("amb_emit_wind");
            _emitFestival = AudioCatalog.Find("amb_emit_festival");
            for (int i = 0; i < _modUsedStage.Length; i++) _modUsedStage[i] = int.MinValue / 2;
        }

        public StageModifierType Modifier => _modifier;
        public bool Active => _active;
        /// <summary>정전 기믹 중 (출력 쪽이 배경음에 5 kHz 저역 통과)</summary>
        public bool Hush => _modifier == StageModifierType.Blackout;
        public AudioId BaseKey => _layers[LayerBase].Key;
        public AudioId ModifierKey => _layers[_modLayer].Key;
        public double NextEmit => _nextEmit;

        public float LayerLevel(int layer) => layer >= 0 && layer < LayerCount ? _layers[layer].Level : 0f;
        public float LayerTarget(int layer) => layer >= 0 && layer < LayerCount ? _layers[layer].Target : 0f;
        public AudioId LayerKey(int layer) => layer >= 0 && layer < LayerCount ? _layers[layer].Key : AudioId.None;

        /// <summary>환경음을 켠다 (음악과 함께 부팅 0.5초 뒤) / 끈다</summary>
        public void SetActive(bool active, double now)
        {
            _active = active;
            if (active && _nextEmit < 0) _nextEmit = now + Mathf.Lerp(EmitMin, EmitMax, _random());
        }

        /// <summary>
        /// 스테이지 기믹 층 (StageChanged마다). stageChanged = 새 스테이지 (안 쓰는 층 클립 내리기 계산)
        /// </summary>
        public void SetModifier(StageModifierType modifier, bool stageChanged)
        {
            int index = Mathf.Clamp((int)modifier, 0, _modKeys.Length - 1);
            if (stageChanged)
            {
                _stage++;
                _modUsedStage[index] = _stage;
                for (int i = 1; i < _modKeys.Length; i++)
                {
                    if (!_modKeys[i].IsValid) continue;
                    bool used = _stage - _modUsedStage[i] <= UnloadAfterStages;
                    if (i == index) used = true;
                    if (used && !_modLoaded[i] && i == index)
                    {
                        _out.SetLoaded(_modKeys[i], true);
                        _modLoaded[i] = true;
                    }
                    else if (!used && _modLoaded[i] && _layers[LayerModA].Key != _modKeys[i] && _layers[LayerModB].Key != _modKeys[i])
                    {
                        _out.SetLoaded(_modKeys[i], false);
                        _modLoaded[i] = false;
                    }
                }
            }
            if (modifier == _modifier) return;
            _modifier = modifier;
            var key = _modKeys[index];
            // 지금 층은 빠지고, 다른 층에 새 소리를 들인다
            FadeLayer(_modLayer, 0f, ModifierFade);
            if (!key.IsValid || _out.TakeCount(key) <= 0) return;
            int next = _modLayer == LayerModA ? LayerModB : LayerModA;
            if (_layers[next].Playing) _out.Stop(next);
            _out.Play(next, key, 0, _random());
            _layers[next] = new Layer { Key = key, Level = 0f, Playing = true };
            _modLayer = next;
            FadeLayer(next, 1f, ModifierFade);
            if (!_modLoaded[index])
            {
                _out.SetLoaded(key, true);
                _modLoaded[index] = true;
            }
        }

        public void SetBossAlive(bool alive) => _bossAlive = alive;
        public void SetAsleep(bool asleep) => _asleep = asleep;
        public void SetResting(bool resting) => _resting = resting;
        public void SetHidden(bool hidden) => _hidden = hidden;
        public void SetMusicState(MusicState state) => _music = state;

        /// <summary>한 방 소리를 until(같은 시계)까지 쉰다 (징글 · 보스 처치 순간)</summary>
        public void SuppressEmitters(double until)
        {
            if (until > _suppressUntil) _suppressUntil = until;
        }

        /// <summary>매 프레임 (now = 실제 시계, dt = 실제 시간 — 배경음이 꺼진 동안 0)</summary>
        public void Update(double now, float dt)
        {
            _now = now;
            UpdateBase();
            UpdateModifier();
            UpdateBoss();
            for (int i = 0; i < LayerCount; i++) StepLayer(i, dt);
            UpdateEmitters(now);
        }

        AudioId DesiredBase()
        {
            if (!_active) return AudioId.None;
            if (_music == MusicState.Dungeon && _out.TakeCount(_dungeon) > 0) return _dungeon;
            if (_asleep && _out.TakeCount(_sleep) > 0) return _sleep;
            return _out.TakeCount(_night) > 0 ? _night : AudioId.None;
        }

        float BedDb()
        {
            float db = 0f;
            if (_music == MusicState.Gacha || _music == MusicState.Story) db += MenuDuckDb;
            if (_resting) db += RestBoostDb;
            return db;
        }

        void UpdateBase()
        {
            ref var layer = ref _layers[LayerBase];
            var desired = DesiredBase();
            if (layer.Key != desired)
            {
                if (layer.Playing && layer.Level > 0.001f)
                {
                    FadeLayer(LayerBase, 0f, BaseSwapFade);
                    return;
                }
                if (layer.Playing) _out.Stop(LayerBase);
                layer = new Layer { Key = desired };
                if (!desired.IsValid) return;
                _out.Play(LayerBase, desired, 0, _random());
                layer.Playing = true;
                layer.Level = 0f;
            }
            if (!desired.IsValid) return;
            float db = BedDb();
            if (_modifier == StageModifierType.Blackout && desired == _night) db += BlackoutBaseDb;
            if (desired == _sleep) db += SleepBoostDb;
            float target = AudioGate.DbToGain(db);
            if (!Mathf.Approximately(layer.Target, target) || layer.Rate <= 0f) FadeLayer(LayerBase, target, BaseFade);
        }

        void UpdateModifier()
        {
            bool allowed = _active && _music != MusicState.Dungeon && !_asleep;
            float bed = AudioGate.DbToGain(BedDb());
            for (int i = LayerModA; i <= LayerModB; i++)
            {
                ref var layer = ref _layers[i];
                if (!layer.Playing) continue;
                float target = allowed && i == _modLayer && _modifier != StageModifierType.None ? bed : 0f;
                if (!Mathf.Approximately(layer.Target, target)) FadeLayer(i, target, ModifierFade);
            }
        }

        void UpdateBoss()
        {
            ref var layer = ref _layers[LayerBoss];
            bool want = _active && _bossAlive && _out.TakeCount(_boss) > 0;
            if (want && !layer.Playing)
            {
                _out.Play(LayerBoss, _boss, 0, _random());
                layer = new Layer { Key = _boss, Playing = true };
            }
            if (!layer.Playing) return;
            float target = want ? AudioGate.DbToGain(BedDb()) : 0f;
            if (!Mathf.Approximately(layer.Target, target)) FadeLayer(LayerBoss, target, BossFade);
        }

        void FadeLayer(int index, float target, float time)
        {
            ref var layer = ref _layers[index];
            layer.Target = Mathf.Max(0f, target);
            float distance = Mathf.Abs(layer.Target - layer.Level);
            layer.Rate = time > 0f ? Mathf.Max(distance, 0.25f) / time : float.MaxValue;
        }

        void StepLayer(int index, float dt)
        {
            ref var layer = ref _layers[index];
            if (!layer.Playing) return;
            float before = layer.Level;
            if (!Mathf.Approximately(layer.Level, layer.Target))
                layer.Level = layer.Rate >= float.MaxValue ? layer.Target : Mathf.MoveTowards(layer.Level, layer.Target, layer.Rate * Mathf.Max(0f, dt));
            if (!Mathf.Approximately(before, layer.Level) || dt <= 0f) _out.SetLevel(index, layer.Level);
            if (layer.Level <= 0.0001f && layer.Target <= 0f && index != LayerBase)
            {
                _out.Stop(index);
                layer = new Layer { Key = AudioId.None };
            }
        }

        void UpdateEmitters(double now)
        {
            if (!_active || _nextEmit < 0 || now < _nextEmit) return;
            if (_hidden || now < _suppressUntil || _asleep || _music == MusicState.Dungeon)
            {
                _nextEmit = now + EmitRetry;
                return;
            }
            var key = _modifier switch
            {
                StageModifierType.Rain => _emitRain,
                StageModifierType.Wind => _emitWind,
                StageModifierType.Festival => _emitFestival,
                _ => _emitNight,
            };
            if (_out.TakeCount(key) <= 0) key = _emitNight;
            if (_out.TakeCount(key) > 0)
            {
                float gain = Mathf.Lerp(EmitGainMin, EmitGainMax, _random()) + (_resting ? RestBoostDb : 0f);
                float pan = (_random() * 2f - 1f) * EmitPan;
                _out.Emit(key, gain, pan);
            }
            _nextEmit = now + Mathf.Lerp(EmitMin, EmitMax, _random());
        }

        /// <summary>모두 멈춘다</summary>
        public void StopAll()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                if (_layers[i].Playing) _out.Stop(i);
                _layers[i] = default;
            }
            _modifier = StageModifierType.None;
        }
    }
}
