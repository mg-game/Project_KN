using System;
using System.Collections.Generic;
using IdleGame.UI;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 전투 소리 감독 (AUDIO-SPEC §5) — "[Audio]"에 부팅이 붙인다 (장면 · BattleView 수정 없음).
    ///   · Update: Game.Battle이 새 인스턴스면 (첫 실행 · ResetData 뒤 장면 다시 불러오기) 옛 것에서 떼고 Event · StageChanged · StageEnded · StepBack을 구독한다.
    ///   · 전투를 소리 쪽에서 시작하지 않는다: 이벤트나 StageChanged를 받거나 결과 대기 중일 때만 Sim을 읽는다.
    ///   · 연출 시계: 장면의 BattleFx.TimeScale(보스 처치 · 불 밝히기 정지 그대로)을 엔진에 넣는다. BattleFx가 없으면 같은 사건으로 거는 거울.
    ///   · 실행 순서 1000: GameBootstrap(−1000)이 전투를 돌리고 BattleFx가 연출 시계를 옮긴 뒤 — 엔진은 LateUpdate에서 예약을 울린다.
    /// 규칙은 BattleAudioLogic (순수 C#, 테스트 가능).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class BattleAudioDirector : MonoBehaviour
    {
        const float SearchInterval = 2f;

        BattleSystem _battle;
        BattleAudioLogic _logic;
        BattleAudioState _state;
        EngineSink _sink;
        AudioEngine _engine;
        BattleFx _fx;
        float _fxSearch;
        System.Random _rng;
        Func<float> _random;

        Action<BattleEvent> _onEvent;
        Action _onStageChanged;
        Action<BattleOutcome, List<RewardData>> _onStageEnded;
        Action<int, int> _onStepBack;

        void Awake()
        {
            _rng = new System.Random();
            _random = () => (float)_rng.NextDouble();
            _onEvent = OnEvent;
            _onStageChanged = OnStageChanged;
            _onStageEnded = OnStageEnded;
            _onStepBack = OnStepBack;
            _state = new BattleAudioState();
        }

        void OnDestroy() => Detach();

        void Update()
        {
            var engine = AudioService.Engine;
            if (engine == null)
            {
                Detach();
                return;
            }
            if (!Game.IsReady || Game.Battle == null)
            {
                Detach();
                engine.SetFxTimeScale(1f);
                return;
            }
            if (!ReferenceEquals(Game.Battle, _battle) || !ReferenceEquals(engine, _engine)) Attach(Game.Battle, engine);

            engine.SetFxTimeScale(FxTimeScale());
            engine.SetBattleSpeed(_battle.Speed);
            _logic.Poll(Time.deltaTime, Time.frameCount);
        }

        /// <summary>이번 프레임 연출 시간 배율: 장면 BattleFx (2초마다 다시 찾음) → 없으면 거울</summary>
        float FxTimeScale()
        {
            if (_fx == null)
            {
                _fxSearch -= Time.unscaledDeltaTime;
                if (_fxSearch <= 0f)
                {
                    _fxSearch = SearchInterval;
                    _fx = FindAnyObjectByType<BattleFx>();
                }
            }
            float mirror = _logic.TickMirror(Time.unscaledDeltaTime);
            return _fx != null && _fx.isActiveAndEnabled ? _fx.TimeScale : mirror;
        }

        void Attach(BattleSystem battle, AudioEngine engine)
        {
            Detach();
            _battle = battle;
            _engine = engine;
            _state.Bind(battle);
            if (_sink == null || _sink.Engine != engine) _sink = new EngineSink(engine);
            if (_logic == null || _sink.Fresh) _logic = new BattleAudioLogic(_state, _sink, _random);
            else _logic.Reset();
            _sink.Fresh = false;
            battle.Event += _onEvent;
            battle.StageChanged += _onStageChanged;
            battle.StageEnded += _onStageEnded;
            battle.StepBack += _onStepBack;
            // 붙기 전에 이미 시작한 스테이지: 시뮬레이터 없이 읽히는 기믹으로 환경음만 맞춘다
            engine.SetModifier(battle.CurrentStageModifier, true);
            engine.SetBossMusic(false);
        }

        void Detach()
        {
            if (_battle != null)
            {
                _battle.Event -= _onEvent;
                _battle.StageChanged -= _onStageChanged;
                _battle.StageEnded -= _onStageEnded;
                _battle.StepBack -= _onStepBack;
                _battle = null;
            }
            if (_engine != null)
            {
                _engine.FlushBattle();
                _engine.StopLoop(0, 0.2f);
                _engine.StopLoop(1, 0.05f);
                _engine.SetBossMusic(false);
                _engine.SetBossAlive(false);
                _engine.SetFxTimeScale(1f);
            }
            _state.Bind(null);
        }

        void OnEvent(BattleEvent e)
        {
            if (_logic != null) _logic.OnEvent(in e, Time.frameCount);
        }

        void OnStageChanged()
        {
            if (_logic != null) _logic.OnStageChanged(Time.frameCount);
        }

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (_logic != null) _logic.OnStageEnded(outcome);
        }

        void OnStepBack(int blocked, int lowered)
        {
            if (_logic != null) _logic.OnStepBack();
        }

        // ───────────── 출력 어댑터 (엔진) ─────────────

        sealed class EngineSink : IBattleAudioSink
        {
            readonly AudioEngine _engine;

            public EngineSink(AudioEngine engine)
            {
                _engine = engine;
                Fresh = true;
            }

            public AudioEngine Engine => _engine;
            public bool Fresh;

            public double FxNow => _engine.FxNow;
            public double UiNow => _engine.UiNow;
            public bool Has(AudioId id) => _engine.HasClips(id);
            public void Schedule(PendingSound sound, double delay) => _engine.Schedule(sound, delay);
            public void SetLoop(int slot, AudioId id, float volume, float pitch, float fade) => _engine.SetLoop(slot, id, volume, pitch, fade);
            public void StopLoop(int slot, float fade) => _engine.StopLoop(slot, fade);

            public void Duck(AudioBus bus, float gainDb, float hold, float attack, float release, int reason, int exempt, double delay) =>
                _engine.Duck(bus, gainDb, hold, attack, release, reason, exempt, delay);

            public void SetBossMusic(bool chapterBossAlive) => _engine.SetBossMusic(chapterBossAlive);
            public void SetBossAlive(bool alive) => _engine.SetBossAlive(alive);
            public void SetModifier(StageModifierType modifier, bool stageChanged) => _engine.SetModifier(modifier, stageChanged);
            public void FlushBattle() => _engine.FlushBattle();
            public void SuppressEmitters(double seconds) => _engine.SuppressEmitters(seconds);
            public bool IsAsleep => _engine.IsAsleep;
            public bool IsJinglePlaying => _engine.IsJinglePlaying;
            public double JingleStartedAt => _engine.JingleStartedAt;
            public AudioId CurrentJingle => _engine.CurrentJingle;
            public int Transpose => _engine.Transpose;
        }
    }

    /// <summary>
    /// 전투 상태 어댑터 (BattleSystem · BattleSimulator · BattleFieldMapper, 읽기만). 목록은 인덱스로 돈다 (인터페이스 foreach 박싱 없음).
    /// 시뮬레이터는 SimReady 뒤에만 건드린다 (BattleSystem.Sim이 전투를 시작하므로).
    /// </summary>
    internal sealed class BattleAudioState : IBattleAudioState
    {
        const float SearchInterval = 2f;
        const float MinionRadiusSq = 0.36f;

        BattleSystem _battle;
        bool _simReady;
        BattleFieldMapper _mapper;
        float _mapperSearchAt = float.NegativeInfinity;

        public void Bind(BattleSystem battle)
        {
            _battle = battle;
            _simReady = false;
        }

        BattleSimulator Sim => _simReady && _battle != null ? _battle.Sim : null;

        BattleFieldMapper Mapper
        {
            get
            {
                if (_mapper == null && Time.unscaledTime >= _mapperSearchAt)
                {
                    _mapperSearchAt = Time.unscaledTime + SearchInterval;
                    _mapper = UnityEngine.Object.FindAnyObjectByType<BattleFieldMapper>();
                }
                return _mapper;
            }
        }

        public int Speed => _battle != null ? _battle.Speed : 1;
        public int Stage => _battle != null ? _battle.Stage : 1;
        public int HighestStage => _battle != null ? _battle.HighestStage : 1;
        public bool IsBetweenStages => _battle != null && _battle.IsBetweenStages;
        public StageModifierType CurrentModifier => _battle != null ? _battle.CurrentStageModifier : StageModifierType.None;
        public bool SimReady => _simReady && _battle != null;

        public void MarkSimReady()
        {
            if (_battle != null) _simReady = true;
        }

        public float ShopHp => Sim != null ? Sim.ShopHp : 0f;
        public float ShopMaxHp => Sim != null ? Sim.ShopMaxHp : 0f;
        public float ShopShield => Sim != null ? Sim.ShopShield : 0f;
        public float LampGauge => Sim != null ? Sim.LampGauge : 0f;
        public int Wave => Sim != null ? Sim.Wave : 0;

        public int SynergySignature
        {
            get
            {
                var active = Sim?.Setup?.Modifiers?.ActiveSynergies;
                if (active == null) return 0;
                int hash = 17;
                for (int i = 0; i < active.Count; i++)
                {
                    var pair = active[i];
                    hash = hash * 31 + (pair.Key != null ? pair.Key.GetHashCode() : 0);
                    hash = hash * 31 + pair.Value;
                }
                return hash;
            }
        }

        static void Fill(EnemyState s, out BattleEnemyInfo info)
        {
            var def = s.Def;
            info = new BattleEnemyInfo
            {
                Id = s.Id,
                DefId = def != null ? def.id : null,
                MinionId = def != null ? def.minionId : null,
                IsBoss = s.IsBoss || (def != null && def.isBoss),
                IsSplit = s.IsSplit,
                Alive = s.Alive,
                Position = s.Position,
                Shield = s.Shield,
                BurnTimer = s.BurnTimer,
                SiegeWindup = s.SiegeWindup,
            };
        }

        public bool TryGetEnemy(int id, out BattleEnemyInfo info)
        {
            var sim = Sim;
            var enemies = sim != null ? sim.Enemies : null;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var s = enemies[i];
                if (s == null || s.Id != id) continue;
                Fill(s, out info);
                return true;
            }
            info = default;
            return false;
        }

        public bool TryGetBoss(out BattleEnemyInfo boss)
        {
            var sim = Sim;
            var enemies = sim != null ? sim.Enemies : null;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var s = enemies[i];
                if (s == null || !s.Alive || !(s.IsBoss || (s.Def != null && s.Def.isBoss))) continue;
                Fill(s, out boss);
                return true;
            }
            boss = default;
            return false;
        }

        public bool IsBossMinion(in BattleEnemyInfo spawned)
        {
            if (string.IsNullOrEmpty(spawned.DefId)) return false;
            var sim = Sim;
            var enemies = sim != null ? sim.Enemies : null;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var boss = enemies[i];
                if (boss == null || !boss.Alive || !boss.IsBoss || boss.Def == null || boss.Def.minionId != spawned.DefId) continue;
                if ((boss.Position - spawned.Position).sqrMagnitude <= MinionRadiusSq) return true;
            }
            return false;
        }

        public bool TryGetGuardian(int slot, out BattleUnitInfo info)
        {
            var sim = Sim;
            var guardians = sim != null ? sim.Guardians : null;
            if (guardians == null || slot < 0 || slot >= guardians.Count || guardians[slot] == null)
            {
                info = default;
                return false;
            }
            var g = guardians[slot];
            info = new BattleUnitInfo
            {
                LineageId = g.LineageId,
                FormId = g.FormId,
                Style = g.Stats.style,
                FormTier = FormTier(g.FormId),
                Alive = true,
                Position = BattleGeometry.GuardianPosition(slot),
            };
            return true;
        }

        public bool TryGetEcho(int id, out BattleUnitInfo info)
        {
            var sim = Sim;
            var echoes = sim != null ? sim.Echoes : null;
            int count = echoes != null ? echoes.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var s = echoes[i];
                if (s == null || s.Id != id) continue;
                info = new BattleUnitInfo
                {
                    LineageId = s.LineageId,
                    FormId = s.FormId,
                    Style = s.Stats.style,
                    FormTier = FormTier(s.FormId),
                    IsToken = s.IsToken,
                    Alive = s.Alive,
                    Position = s.Position,
                };
                return true;
            }
            info = default;
            return false;
        }

        static int FormTier(string formId)
        {
            if (string.IsNullOrEmpty(formId) || !Game.IsReady || Game.Characters?.Db == null) return 0;
            var form = Game.Characters.Db.GetForm(formId);
            return form != null ? form.tier : 0;
        }

        public SkillDef GetSkill(string skillId) => Sim != null ? Sim.GetSkill(skillId) : null;

        public float ActorScale => Mapper != null ? Mapper.ActorScale : 1f;
        public float Ppu => Mapper != null ? Mapper.Ppu : BattleAudioTiming.FallbackPpu;
        public float Squash => Mapper != null ? Mapper.Squash : BattleAudioTiming.FallbackSquash;

        public Vector2 WorldToField(Vector2 world)
        {
            var mapper = Mapper;
            if (mapper != null) return mapper.WorldToField(world);
            return new Vector2(world.x * BattleAudioTiming.FallbackPpu, world.y * BattleAudioTiming.FallbackPpu * BattleAudioTiming.FallbackSquash);
        }

        public float HealEchoRadius => _battle != null && _battle.Config != null ? _battle.Config.healEchoRadius : 0.8f;
    }
}
