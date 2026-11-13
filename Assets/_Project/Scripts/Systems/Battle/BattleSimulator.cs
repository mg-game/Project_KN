using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 수선점 방어 전투 시뮬레이션 (순수 C#, 시드 고정 시 결정적).
    /// 적은 5방향에서 수선점으로 다가오고, 지킴이는 자기 방향 부채꼴 안의 적을 요격한다.
    /// 잔향은 시간/처치로 차오르고, 잔향을 써서 잔향이를 소환한다(탭 = 가장 위험한 방향 자동, 드래그 = 지정 방향).
    /// 잔향이가 사라질 때 빛으로 풀려 잔향 일부를 돌려주고 불빛 게이지를 채운다.
    /// 불빛 게이지가 차면 "수선점 불 밝히기"로 전체 공격. 일정 수 처치 후 보스, 보스 처치 = 클리어, 수선점 내구도 0 = 실패.
    /// <para>
    /// 2차: 형태 고유 스킬(BattleSimulator.Skills.cs), 버프/약화·보호막·회복 같은 상태(BattleSimulator.Status.cs),
    /// 적 기믹과 스테이지 기믹(BattleSimulator.Gimmicks.cs), 시너지 능력치(BattleSetup.Modifiers)가 더해진다.
    /// 전투 시작 연출 이벤트(SynergyActivated / StageModifierStarted)와 전투 시작 스킬은 Start 직후 첫 진행 때 나온다
    /// (뷰가 StageChanged를 받고 상태를 비운 뒤에 받도록).
    /// </para>
    /// <para>
    /// 뷰 안내: 상태 객체(EnemyState/EchoState)는 등장할 때마다 새로 만들고 재사용하지 않는다.
    /// 죽은 객체는 Alive = false로 남으므로 참조를 들고 있어도 안전하다. 목록(Enemies/Echoes)은 같은 List 인스턴스를
    /// 계속 재사용한다(Start 때 비워짐). 등장하면 바로 목록에 들어가고, 죽은 것은 진행 한 번이 끝날 때 빠진다
    /// (Tick이 끝난 뒤에는 살아 있는 것만 남는다). 내부는 StepSeconds 고정 간격으로 진행되므로
    /// 뷰는 위치를 보간(부드럽게 따라가기)해서 그리면 된다.
    /// </para>
    /// <para>
    /// 이벤트 출처: GuardianAttack/SkillCast/EnemyKilled의 SourceId는 지킴이 슬롯(0~4) 또는 잔향이 ID(EchoIdBase 이상).
    /// 수선점이 덱 칸 대신 쓴 스킬(덱의 전투 시작 스킬, 전장에 구성원이 없는 팀 공용 스킬)과 그 처치, 불 밝히기 처치는 -1이다.
    /// EnemyHit의 SourceId는 지킴이 기본 공격(투사체)일 때만 지킴이 슬롯이고, 나머지(잔향이·스킬·지속 피해·불 밝히기)는 -1이다.
    /// </para>
    /// <para>
    /// 공격 연출 이벤트 (밸런스와 무관, BattleTypes 주석 참고): EnemyStrike(SourceId = 적, TargetId = 잔향이 ID 또는 -1 수선점,
    /// 잔향이는 적 하나당 그 적의 strikeInterval마다 묶음) / GuardianPulse(SourceId = 지원·힐 지킴이 슬롯, 회복량 0이어도 나옴).
    /// </para>
    /// </summary>
    public partial class BattleSimulator
    {
        /// <summary>내부 고정 진행 간격(초). Tick을 어떻게 나눠 불러도 같은 시드면 같은 전투가 된다</summary>
        public const float StepSeconds = 1f / 60f;
        /// <summary>Tick 한 번에 처리하는 최대 시간(초). 앱이 멈췄다 돌아와도 한꺼번에 몰아서 돌지 않는다</summary>
        public const float MaxTickSeconds = 1f;
        /// <summary>잔향이 ID의 시작 값. 이벤트 SourceId가 이 값 이상이면 잔향이, 아니면 지킴이 슬롯</summary>
        public const int EchoIdBase = 1000;

        const float AnimDecayPerSecond = 4f;
        const float AuraSlowSeconds = 0.1f;
        const float EchoHitSlowSeconds = 1f;
        const float BossThreatScale = 3f;
        const float CoveredLaneThreatScale = 0.5f;
        const float EchoRetargetIdleSeconds = 0.8f;
        const float EchoSpawnRadius = 0.2f;

        public event Action<BattleEvent> Event;

        readonly BattleConfig _config;
        System.Random _random;
        /// <summary>치명타 판정 전용 난수 (전투 내용이 적 등장 순서를 바꾸지 않게 따로 쓴다)</summary>
        System.Random _combatRandom;

        // ── 전투 상태 ──
        BattleSetup _setup;
        BattleModifiers _mods = new();
        BattleOutcome _outcome;
        bool _started;
        bool _stepping;
        bool _spawningEnabled;
        /// <summary>시작 연출 이벤트/전투 시작 스킬을 아직 내보내지 않음 (첫 진행 때 처리)</summary>
        bool _startPending;
        float _time;
        float _accumulator;
        float _resonance;
        float _maxResonance;
        float _lamp;
        float _shopHp;
        float _shopMaxHp;
        int _kills;
        int _killsToBoss;
        bool _bossSpawned;
        EnemyUnit _boss;
        int _nextEnemyId;
        int _nextEchoId;

        // ── 스테이지 배율 ──
        float _hpScale;
        float _damageScale;
        float _speedScale;

        // ── 웨이브 ──
        int _wave;
        int _waveSize;
        int _waveRemaining;
        float _waveRest;
        float _waveRestTime;
        float _spawnTimer;
        float _spawnInterval;
        int _pressureA;
        int _pressureB;
        int _pressureCount;

        // ── 자동 전투 ──
        float _autoTimer;
        int _autoNext;

        // ── 위협도 (매 진행마다 갱신) ──
        readonly float[] _danger = new float[BattleGeometry.SlotCount];
        readonly Vector2[] _dangerVector = new Vector2[BattleGeometry.SlotCount];
        float _dangerAngle;
        float _dangerReference = 1f;
        float _maxDanger;
        /// <summary>지킴이가 없는 방향에 적이 있음</summary>
        bool _uncoveredThreat;

        // ── 유닛 ──
        readonly GuardianState[] _guardians = new GuardianState[BattleGeometry.SlotCount];
        readonly GuardianUnit[] _guardianUnits = new GuardianUnit[BattleGeometry.SlotCount];
        readonly List<DeckSlotState> _deck = new();
        readonly List<DeckRuntime> _deckRuntime = new();
        readonly List<EchoUnit> _echoUnits = new();
        readonly List<EchoState> _echoes = new();
        readonly List<EnemyUnit> _enemyUnits = new();
        readonly List<EnemyState> _enemies = new();
        readonly List<EnemyDef> _spawnPool = new();
        readonly List<float> _spawnWeights = new();
        readonly List<EnemyDef> _bossPool = new();
        readonly Dictionary<string, EnemyDef> _enemyLookup = new();
        float _spawnWeightTotal;
        bool _listsDirty;
        /// <summary>덱에서 소환된(토큰 제외) 살아 있는 잔향이 수 / 토큰 수</summary>
        int _deckEchoCount;
        int _tokenCount;

        // ───────────────────────────── 내부 유닛 ─────────────────────────────

        /// <summary>지킴이 / 잔향이 공용 전투 수치 (시너지·스테이지 기믹·버프·스킬 반영)</summary>
        abstract class CombatUnit
        {
            public string FormId;
            public string LineageId;
            /// <summary>특성 비트 (1 &lt;&lt; TraitType)</summary>
            public int TraitMask;
            public bool IsLight;
            public bool IsMetal;
            public bool IsFabric;
            /// <summary>팀 + 계보 시너지 능력치 (값 복사본)</summary>
            public readonly StatBlock Mods = new();

            /// <summary>스킬 계산용 공격력 (레벨·시너지·스테이지 특성 반영, 성향 배율·버프 제외)</summary>
            public float SkillAttack;
            public float HealPower = 1f;
            public float ShieldPower = 1f;
            public float StunPower = 1f;
            public float SlowPower = 1f;
            public float BossDamage = 1f;
            /// <summary>치명타 확률 0~1 / 치명타 추가 피해 배율 (0.5 = +50%)</summary>
            public float CritChance;
            public float CritDamage;
            /// <summary>스킬 재사용 대기 배율 (1 = 감소 없음)</summary>
            public float CooldownScale = 1f;
            public float AttackSpeedScale = 1f;
            public float RangeScale = 1f;

            // 강화 (가장 센 것 하나만 유지)
            public float AttackBuff;
            public float AttackBuffTimer;
            public float SpeedBuff;
            public float SpeedBuffTimer;
            // 약화 (녹: 공격 속도 감소, 갉아먹힘: 공격력 감소) / 정화 면역
            public float RustPercent;
            public float RustTimer;
            public float GnawPercent;
            public float GnawTimer;
            public float CleanseTimer;
            // 패시브
            public float ExecutePercent;
            public float RampPerSecond;
            public float RampCap;
            public float Ramp;

            public readonly List<SkillSlot> Skills = new();
            /// <summary>형태 고유 스킬 칸 (없으면 null)</summary>
            public SkillSlot Primary;

            public abstract int SourceId { get; }
            public abstract Vector2 Position { get; }
            public abstract bool IsAlive { get; }
            public abstract CharacterStats Stats { get; }
            public abstract CombatStyle Style { get; }

            /// <summary>버프/약화/패시브를 반영한 공격력 배율</summary>
            public float AttackFactor =>
                Mathf.Max(0.1f, (1f + (AttackBuffTimer > 0f ? AttackBuff : 0f) / 100f + Ramp / 100f) * (1f - (GnawTimer > 0f ? GnawPercent : 0f) / 100f));

            /// <summary>버프/약화를 반영한 공격 속도 배율 (클수록 빠름)</summary>
            public float SpeedFactor =>
                Mathf.Max(0.1f, (1f + (SpeedBuffTimer > 0f ? SpeedBuff : 0f) / 100f) / (1f + (RustTimer > 0f ? RustPercent : 0f) / 100f));

            public float CurrentSkillAttack => SkillAttack * AttackFactor;
            public bool HasDebuff => RustTimer > 0f || GnawTimer > 0f;
            public bool HasTrait(TraitType trait) => (TraitMask & (1 << (int)trait)) != 0;
        }

        sealed class GuardianUnit : CombatUnit
        {
            public GuardianState State;
            public int Slot;
            public Vector2 RingPosition;
            public Vector2 Direction;
            public float CosHalfArc;
            public float RangeSq;
            public float Range;
            public float Damage;
            public float Interval;
            public float SupportTimer;
            /// <summary>마지막으로 노린 적 (EnemiesNearTarget 스킬 기준)</summary>
            public EnemyUnit Target;

            public override int SourceId => Slot;
            public override Vector2 Position => RingPosition;
            public override bool IsAlive => true;
            public override CharacterStats Stats => State.Stats;
            public override CombatStyle Style => State.Stats.style;
        }

        sealed class EchoUnit : CombatUnit
        {
            public EchoState State;
            public CombatStyle StyleValue;
            public float Cooldown;
            public float Interval;
            public float Damage;
            public float Range;
            public bool Melee;
            public int Cost;
            public float MoveSpeed;
            public float HpScale = 1f;
            public float LifetimeScale = 1f;
            public float MoveScale = 1f;
            /// <summary>지키러 나가는 방향 (라디안)</summary>
            public float GuardAngle;
            /// <summary>방향을 지정하지 않고 소환됨 → 할 일이 없으면 가장 위험한 방향으로 옮겨 간다</summary>
            public bool AutoDirected;
            public float IdleTime;
            public float SupportTimer;
            public EnemyUnit Target;
            public float ShieldTimer;
            public float HotRate;
            public float HotTimer;
            public float HotAcc;
            public float HotTick;
            public int HealSourceId = -1;

            public override int SourceId => State.Id;
            public override Vector2 Position => State.Position;
            public override bool IsAlive => State.Alive;
            public override CharacterStats Stats => State.Stats;
            public override CombatStyle Style => StyleValue;
        }

        sealed class EnemyUnit
        {
            public EnemyState State;
            /// <summary>수선점 → 적 방향 단위 벡터 (적은 이 선을 따라 직진)</summary>
            public Vector2 OutDirection;
            public float Radius;
            public int Lane;
            public float Speed;
            public float Damage;
            public float EchoDamage;
            public bool AtShop;
            /// <summary>다음 수선점 공격까지 남은 시간. 밀려나 있는 동안은 멈춰 있다가 돌아오면 이어서 센다</summary>
            public float SiegeTimer;
            /// <summary>수선점에 한 번이라도 붙어 공격했음 (다시 붙을 때는 바로 때리지 않는다)</summary>
            public bool HasSieged;
            public float MinionTimer;
            public EnemyGimmick Gimmicks;
            // 상태
            public float SlowMul = 1f;
            public float MarkPercent;
            public float BurnDps;
            public float BurnAcc;
            public float BurnTick;
            public CombatUnit BurnSource;
            public float RevealTimer;
            public float HasteMul = 1f;
            public float AuraTick;
            public float DrainTimer;
            /// <summary>연쇄 피해가 이미 지나간 적 표시 (할당 없이 중복 방지)</summary>
            public int ChainStamp;
            // 잔향이 공격 연출 묶음 (EnemyStrike): 지금 때리는 잔향이 ID, 다음 이벤트를 낼 수 있는 시각, 지난 이벤트 뒤로 쌓인 피해
            public int StrikeEchoId = -1;
            public float StrikeReadyTime;
            public float StrikeAcc;

            public bool Has(EnemyGimmick gimmick) => (Gimmicks & gimmick) != 0;
        }

        /// <summary>덱 칸의 전투 내부 정보 (특성, 스킬, 수선점 대리 시전자)</summary>
        sealed class DeckRuntime
        {
            public int TraitMask;
            public readonly List<SkillInfo> Skills = new();
            /// <summary>전투 시작 스킬 / 팀 공용 스킬을 수선점에서 대신 쓰는 시전자 (쓸 스킬이 없으면 null)</summary>
            public ShopCaster Caster;
            /// <summary>소환 시 적만 노리는 스킬이 있음 → 자동 전투는 적이 없을 때 이 칸을 미리 부르지 않는다</summary>
            public bool NeedsEnemyOnSummon;
        }

        public BattleSimulator(BattleConfig config, int seed)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            _config = config;
            SetSeed(seed);
            _setup = new BattleSetup();
        }

        void SetSeed(int seed)
        {
            _random = new System.Random(seed);
            _combatRandom = new System.Random(unchecked(seed * 486187739 + 97));
            Seed = seed;
        }

        public int Seed { get; private set; }
        public BattleSetup Setup => _setup;
        public BattleMode Mode => _setup.Mode;
        /// <summary>스테이지 번호 또는 던전 층</summary>
        public int Level => _setup.Level;
        public bool IsStarted => _started;
        public BattleOutcome Outcome => _outcome;
        public float ElapsedTime => _time;

        /// <summary>
        /// 그리기 보간 비율 0~1 = 아직 진행하지 않은 누적 시간 / StepSeconds (perf PF-2 F1). 뷰는 Lerp(PrevPosition, Position, RenderAlpha)로 그린다 —
        /// 한 진행(16.7 ms) 늦게 그리는 대신, 프레임에 진행이 0 · 1 · 2번이어도(144 Hz 화면 · 2배속 · 프레임 흔들림) 움직임이 고르다.
        /// 진행 중이 아니면(시작 전 · 결과 뒤) 1 = 마지막 자리 그대로. 배속 · PresentationScale은 누적 속도만 바꾸므로 느린 화면도 고르게 흐른다
        /// </summary>
        public float RenderAlpha => _started && _outcome == BattleOutcome.Running ? Mathf.Clamp01(_accumulator / StepSeconds) : 1f;

        /// <summary>Start 뒤 고정 진행 횟수 (개발 · 측정: 프레임마다 몇 번 진행했는지)</summary>
        public int StepCount { get; private set; }

        /// <summary>길이 = BattleGeometry.SlotCount, 빈 슬롯은 null</summary>
        public IReadOnlyList<GuardianState> Guardians => _guardians;
        /// <summary>인덱스 = 덱 슬롯, 빈칸은 null</summary>
        public IReadOnlyList<DeckSlotState> Deck => _deck;
        /// <summary>살아 있는 잔향이 (스킬로 불려 나온 토큰 포함, IsToken)</summary>
        public IReadOnlyList<EchoState> Echoes => _echoes;
        /// <summary>살아 있는 적</summary>
        public IReadOnlyList<EnemyState> Enemies => _enemies;

        /// <summary>
        /// 적 ID → 적 정의 ID (EnemyDef.id). 모르면 null. 처치 이벤트(EnemyKilled — TargetId = 적 ID) 처리 중에도 찾을 수 있다
        /// (죽은 적은 이벤트를 다 낸 뒤 목록에서 빠진다). 할당 없음 — 도감 적 찾기(4차, SPEC4 §2.5)가 쓴다
        /// </summary>
        public string GetEnemyDefId(int enemyId)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy != null && enemy.Id == enemyId) return enemy.Def != null ? enemy.Def.id : null;
            }
            return null;
        }

        /// <summary>
        /// 불빛 게이지를 가득 채운다 (튜토리얼 첫 불 밝히기 FTUE-01, SPEC4 §13.3). 전투가 시작돼 진행 중이고 게이지가 덜 찼을 때만 —
        /// 끝난 전투·시작 전에는 아무것도 하지 않는다. 할당 없음
        /// </summary>
        public void FillLamp()
        {
            if (!_started || _outcome != BattleOutcome.Running || _lamp >= 1f) return;
            _lamp = 1f;
        }

        /// <summary>
        /// 자동 불 밝히기 방식 (CR-02). BattleSetup.LampPolicy를 그대로 읽고 쓴다 — 진행 중인 전투에 바로 적용되고, 다시 시작하면 BattleSystem이 새 구성에 넣는다.
        /// 잠김(1-6 전) → Auto는 BattleSystem이 정한다
        /// </summary>
        public LampPolicy LampPolicy
        {
            get => _setup.LampPolicy;
            set => _setup.LampPolicy = value;
        }

        /// <summary>
        /// 튜토리얼 전투 보호 (FTUE-01). 켜져 있고 메인 스테이지면: 새로 나오는 적(보스 · 분열체 · 부하 포함)의 최대 체력 × EnemyHpScale,
        /// 수선점 내구도는 ShopFloorHp 아래로 내려가지 않는다 (실패 판정 전에 막는다). 던전은 늘 꺼짐. 진행 중에도 바꿀 수 있다 (BattleSystem이 매 Tick 다시 넣는다)
        /// </summary>
        public BattleProtection Protection
        {
            get => _setup.Protection;
            set
            {
                _setup.Protection = value;
                ApplyProtection();
            }
        }

        /// <summary>보호가 실제로 걸려 있는지 (메인 스테이지 + Enabled)</summary>
        public bool IsProtected => _protShopFloor > 0f || _protHpScale != 1f;

        float _protHpScale = 1f;
        float _protShopFloor;

        void ApplyProtection()
        {
            var p = _setup.Protection;
            bool on = p.Enabled && _setup.Mode == BattleMode.Stage;
            _protHpScale = on && p.EnemyHpScale > 0f && !float.IsInfinity(p.EnemyHpScale) ? Mathf.Clamp(p.EnemyHpScale, 0.01f, 100f) : 1f;
            _protShopFloor = on && p.ShopFloorHp > 0f && !float.IsInfinity(p.ShopFloorHp) ? p.ShopFloorHp : 0f;
        }

        /// <summary>
        /// 자동 불 밝히기 판단표 (CR-02, 순수 — LampPolicyTests). 살아 있는 적이 없으면 언제나 false.
        /// Auto: 적 minEnemies 이상 · 보스 · 수선점 35% 미만 / SaveForBoss: 보스 · 수선점 25% 미만 / Manual: 쓰지 않음
        /// </summary>
        public static bool AutoLampDecision(LampPolicy policy, int alive, bool bossAlive, float shopRatio, int minEnemies)
        {
            if (alive <= 0) return false;
            switch (policy)
            {
                case LampPolicy.Manual:
                    return false;
                case LampPolicy.SaveForBoss:
                    return bossAlive || shopRatio < SaveForBossShopRatio;
                default:
                    return alive >= minEnemies || bossAlive || shopRatio < AutoShopRatio;
            }
        }

        /// <summary>Auto 방식이 불을 켜는 수선점 내구도 비율 (지금 규칙)</summary>
        public const float AutoShopRatio = 0.35f;
        /// <summary>'보스까지 아끼기'가 불을 켜는 수선점 내구도 비율</summary>
        public const float SaveForBossShopRatio = 0.25f;

        /// <summary>잔향 보유량</summary>
        public float Resonance => _resonance;
        public float MaxResonance => _maxResonance;
        /// <summary>불빛 게이지 0~1</summary>
        public float LampGauge => _lamp;
        public bool CanLightLamp => _started && _outcome == BattleOutcome.Running && _lamp >= 1f;
        public float ShopHp => _shopHp;
        /// <summary>수선점 최대 내구도 (시너지 ShopMaxHpPercent 반영)</summary>
        public float ShopMaxHp => _shopMaxHp;
        public int Kills => _kills;
        /// <summary>보스 등장까지 필요한 처치 수</summary>
        public int KillsToBoss => _killsToBoss;
        public bool BossSpawned => _bossSpawned;
        /// <summary>등장한 보스 (없거나 처치됐으면 null)</summary>
        public EnemyState Boss => _boss != null && _boss.State.Alive ? _boss.State : null;
        /// <summary>현재 웨이브 번호 (첫 웨이브 전 0)</summary>
        public int Wave => _wave;
        /// <summary>
        /// 지킴이도 잔향이 덱도 없으면 false. 이때는 적이 나오지 않고 전투가 Running인 채로 기다린다
        /// (뷰는 "편성을 해 주세요" 같은 안내를 띄우면 된다).
        /// </summary>
        public bool HasDefenders => _spawningEnabled;

        /// <summary>방치 모드: 잔향이 자동 소환 + 게이지가 차면 자동 불 밝히기</summary>
        public bool AutoMode { get; set; }

        // ── 스킬 / 시너지 / 기믹 (계약: 뷰가 읽는 값) ──
        /// <summary>수선점 보호막 남은 양</summary>
        public float ShopShield => _shopShield;
        /// <summary>이번 전투의 스테이지 기믹</summary>
        public StageModifierType StageModifier => _setup.StageModifier;
        /// <summary>시전된 스킬 정의 (SkillCast 이벤트의 Key로 조회). 없으면 null</summary>
        public SkillDef GetSkill(string skillId) =>
            skillId != null && _skillRegistry.TryGetValue(skillId, out var def) ? def : null;

        public void Start(BattleSetup setup) => StartInternal(setup);

        /// <summary>새 시드로 다시 시작 (BattleSystem이 스테이지마다 다른 전투를 만들 때 사용)</summary>
        public void Start(BattleSetup setup, int seed)
        {
            SetSeed(seed);
            StartInternal(setup);
        }

        void StartInternal(BattleSetup setup)
        {
            _setup = setup ?? new BattleSetup();
            if (_setup.Level < 1) _setup.Level = 1;
            _mods = _setup.Modifiers ?? new BattleModifiers();
            ApplyProtection();
            _outcome = BattleOutcome.Running;
            _started = true;
            _startPending = true;
            _time = 0f;
            _accumulator = 0f;
            StepCount = 0;
            _kills = 0;
            _bossSpawned = false;
            _boss = null;
            _nextEnemyId = 1;
            _nextEchoId = EchoIdBase;
            _lamp = 0f;
            _autoTimer = 0f;
            _autoNext = 0;
            _deckEchoCount = 0;
            _tokenCount = 0;
            ResetSkillState();
            ResetShopStatus();

            bool dungeon = _setup.Mode == BattleMode.EchoDungeon;
            ComputeGlobalStats();
            _maxResonance = Mathf.Max(1f, _config.maxResonance);
            float startResonance = (dungeon ? _config.dungeonStartResonance : _config.startResonance) + _startResonanceBonus;
            _resonance = Mathf.Clamp(startResonance, 0f, _maxResonance);
            float rawShopHp = SafePositive(_setup.ShopMaxHp, 100f);
            _setup.ShopMaxHp = rawShopHp;
            _shopMaxHp = Mathf.Max(1f, rawShopHp * _shopMaxHpScale);
            _shopHp = _shopMaxHp;

            SetupScaling(dungeon);
            BuildGuardians(dungeon);
            BuildDeck();
            BuildSpawnPools(dungeon);

            _enemyUnits.Clear();
            _echoUnits.Clear();
            _enemies.Clear();
            _echoes.Clear();
            _listsDirty = false;

            _wave = 0;
            _waveRemaining = 0;
            _waveRest = Mathf.Max(0f, _config.firstWaveDelay);
            PlanNextWave();

            bool anyGuardian = false;
            for (int i = 0; i < _guardians.Length; i++) anyGuardian |= _guardians[i] != null;
            bool anyDeck = false;
            for (int i = 0; i < _deck.Count; i++) anyDeck |= _deck[i] != null;
            _spawningEnabled = anyGuardian || anyDeck;

            Array.Clear(_danger, 0, _danger.Length);
            Array.Clear(_dangerVector, 0, _dangerVector.Length);
            _maxDanger = 0f;
            _uncoveredThreat = false;
            _dangerReference = Mathf.Max(1f, _shopMaxHp * _config.dangerReferenceRatio);
            _dangerAngle = BattleGeometry.SlotAngle(_pressureA);
        }

        void SetupScaling(bool dungeon)
        {
            int n = _setup.Level - 1;
            if (dungeon)
            {
                _hpScale = Mathf.Min(1e30f, Mathf.Max(0.01f, _config.dungeonHpBase) * Mathf.Pow(Mathf.Max(1f, _config.dungeonHpGrowth), n));
                _damageScale = Mathf.Min(1e30f, Mathf.Pow(Mathf.Max(1f, _config.dungeonDamageGrowth), n));
                _killsToBoss = _config.GetDungeonKillsToBoss(_setup.Level);
            }
            else
            {
                _hpScale = _config.GetStageHpScale(_setup.Level);
                _damageScale = _config.GetStageDamageScale(_setup.Level);
                _killsToBoss = _config.GetKillsToBoss(_setup.Level);
            }
            _speedScale = Mathf.Clamp(1f + _config.stageSpeedGrowth * n, 0.1f, Mathf.Max(1f, _config.maxSpeedMultiplier));

            int waveMax = Mathf.Max(1, _config.waveSizeMax);
            float waveScale = StageWaveSizeScale();
            int waveSize = Mathf.Clamp(_config.waveSizeBase + Mathf.FloorToInt(_config.waveSizePerStage * n), 1, waveMax);
            _waveSize = Mathf.Max(1, Mathf.RoundToInt(waveSize * waveScale));
            _spawnInterval = Mathf.Max(Mathf.Max(0.05f, _config.spawnIntervalMin), _config.spawnIntervalBase - _config.spawnIntervalPerStage * n);
            _waveRestTime = Mathf.Max(Mathf.Max(0f, _config.waveRestMin), _config.waveRestBase - 0.05f * n);
        }

        void BuildGuardians(bool dungeon)
        {
            var source = _setup.Guardians;
            for (int slot = 0; slot < _guardians.Length; slot++)
            {
                var unit = !dungeon && source != null && slot < source.Length ? source[slot] : null;
                if (unit == null || string.IsNullOrEmpty(unit.FormId))
                {
                    _guardians[slot] = null;
                    _guardianUnits[slot] = null;
                    continue;
                }
                var state = new GuardianState
                {
                    Slot = slot,
                    FormId = unit.FormId,
                    LineageId = unit.LineageId,
                    Stats = SanitizeStats(unit.Stats),
                };
                var g = new GuardianUnit
                {
                    State = state,
                    Slot = slot,
                    RingPosition = BattleGeometry.GuardianPosition(slot),
                    FormId = unit.FormId,
                    LineageId = unit.LineageId,
                };
                SetTraits(g, TraitMaskOf(unit.Traits));
                _guardians[slot] = state;
                _guardianUnits[slot] = g;
                ApplyGuardianStats(g);
                // 지원/힐 성향은 전투 시작 직후 한 번 강화·회복을 걸어 준다
                g.SupportTimer = 0.5f;
                BuildUnitSkills(g, CollectSkillInfos(unit.Skill, unit.FormId, unit.LineageId, null));
            }
        }

        /// <summary>성향 배율과 시너지·스테이지 기믹을 반영한 지킴이 내부 수치 계산 (타이머·스킬은 유지)</summary>
        void ApplyGuardianStats(GuardianUnit g)
        {
            RefreshCombatStats(g);
            var stats = g.State.Stats;
            float range = stats.range * g.RangeScale;
            float damage = g.SkillAttack;
            float interval = stats.attackInterval / g.AttackSpeedScale;
            switch (stats.style)
            {
                case CombatStyle.Assault:
                    damage *= _config.assaultDamageScale;
                    range *= _config.assaultRangeScale;
                    break;
                case CombatStyle.Capture:
                    damage *= _config.captureDamageScale;
                    break;
                case CombatStyle.Snipe:
                    damage *= _config.snipeDamageScale;
                    range *= _config.snipeRangeScale;
                    interval *= _config.snipeIntervalScale;
                    break;
                case CombatStyle.Support:
                    damage *= _config.supportDamageScale;
                    break;
                case CombatStyle.Heal:
                    damage *= _config.healDamageScale;
                    break;
            }
            range = Mathf.Max(range, _config.shopGuardRadius + 0.1f);
            g.Direction = BattleGeometry.FromAngle(BattleGeometry.SlotAngle(g.Slot), 1f);
            g.CosHalfArc = Mathf.Cos(Mathf.Clamp(stats.arcDegrees, 10f, 360f) * 0.5f * Mathf.Deg2Rad);
            g.Range = range;
            g.RangeSq = range * range;
            g.Damage = Mathf.Max(0f, damage);
            g.Interval = Mathf.Max(0.1f, interval);
        }

        void BuildDeck()
        {
            _deck.Clear();
            _deckRuntime.Clear();
            var source = _setup.Deck;
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                var unit = source[i];
                if (unit == null || string.IsNullOrEmpty(unit.FormId))
                {
                    _deck.Add(null);
                    _deckRuntime.Add(null);
                    continue;
                }
                var stats = SanitizeStats(unit.Stats);
                var runtime = new DeckRuntime { TraitMask = TraitMaskOf(unit.Traits) };
                CollectSkillInfos(unit.Skill, unit.FormId, unit.LineageId, runtime.Skills);
                var slot = new DeckSlotState
                {
                    Slot = i,
                    FormId = unit.FormId,
                    LineageId = unit.LineageId,
                    Stats = stats,
                    Cost = EchoCost(stats.summonCost, unit.LineageId, unit.FormId),
                    Cooldown = 0f,
                    CooldownMax = Mathf.Max(0.1f, _config.summonCooldown),
                };
                runtime.Caster = BuildShopCaster(slot, runtime.TraitMask, runtime.Skills);
                runtime.NeedsEnemyOnSummon = HasEnemyOnlySummonSkill(runtime.Skills);
                _deckRuntime.Add(runtime);
                _deck.Add(slot);
            }
        }

        void BuildSpawnPools(bool dungeon)
        {
            _spawnPool.Clear();
            _spawnWeights.Clear();
            _bossPool.Clear();
            _enemyLookup.Clear();
            _spawnWeightTotal = 0f;
            int poolLevel = dungeon ? Mathf.Max(1, Mathf.CeilToInt(_setup.Level * _config.dungeonStagePerFloor)) : _setup.Level;
            var enemies = _config.enemies;
            if (enemies == null) return;
            foreach (var def in enemies)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                _enemyLookup.TryAdd(def.id, def);
                if (def.minStage > poolLevel) continue;
                if (def.isBoss)
                {
                    _bossPool.Add(def);
                }
                else if (def.spawnWeight > 0f)
                {
                    float weight = def.spawnWeight * StageSpawnWeightScale(def);
                    if (!(weight > 0f)) continue;
                    _spawnPool.Add(def);
                    _spawnWeights.Add(weight);
                    _spawnWeightTotal += weight;
                }
            }
        }

        EnemyDef FindEnemyDef(string id) => id != null && _enemyLookup.TryGetValue(id, out var def) ? def : null;

        float AttackMultiplier => _setup.AttackMultiplier > 0f && !float.IsInfinity(_setup.AttackMultiplier) ? _setup.AttackMultiplier : 1f;

        static float SafePositive(float value, float fallback) =>
            value > 0f && !float.IsInfinity(value) ? value : fallback;

        /// <summary>잘못된 데이터가 와도 전투가 깨지지 않도록 능력치를 안전한 범위로 맞춘다</summary>
        static CharacterStats SanitizeStats(CharacterStats stats)
        {
            stats.attack = float.IsNaN(stats.attack) || float.IsInfinity(stats.attack) ? 0f : Mathf.Max(0f, stats.attack);
            stats.hp = SafePositive(stats.hp, 1f);
            stats.attackInterval = Mathf.Max(0.1f, SafePositive(stats.attackInterval, 1f));
            stats.range = Mathf.Max(0.1f, SafePositive(stats.range, 1f));
            stats.arcDegrees = Mathf.Clamp(SafePositive(stats.arcDegrees, 72f), 10f, 360f);
            stats.moveSpeed = Mathf.Max(0.05f, SafePositive(stats.moveSpeed, 0.8f));
            stats.lifetime = Mathf.Max(1f, SafePositive(stats.lifetime, 10f));
            stats.summonCost = Math.Max(0, stats.summonCost);
            return stats;
        }

        /// <summary>
        /// 진행 중인 전투의 능력치만 갱신한다 (레벨업/공명/의상/수선점 강화/시너지 수치 반영).
        /// 편성 구성(슬롯별 형태)이 다르면 아무것도 하지 않고 false → 호출자가 다시 시작해야 한다.
        /// 이미 소환된 잔향이는 소환 당시 능력치를 유지한다. 스킬과 스테이지 기믹은 처음 그대로다.
        /// </summary>
        public bool RefreshUnits(BattleSetup setup)
        {
            if (!_started || setup == null || setup.Mode != _setup.Mode) return false;
            bool dungeon = _setup.Mode == BattleMode.EchoDungeon;
            for (int slot = 0; slot < _guardians.Length; slot++)
            {
                var unit = !dungeon && setup.Guardians != null && slot < setup.Guardians.Length ? setup.Guardians[slot] : null;
                string formId = unit != null && !string.IsNullOrEmpty(unit.FormId) ? unit.FormId : null;
                if (formId != _guardians[slot]?.FormId) return false;
            }
            int deckCount = setup.Deck?.Count ?? 0;
            if (deckCount != _deck.Count) return false;
            for (int i = 0; i < deckCount; i++)
            {
                var unit = setup.Deck[i];
                string formId = unit != null && !string.IsNullOrEmpty(unit.FormId) ? unit.FormId : null;
                if (formId != _deck[i]?.FormId) return false;
            }

            // 같은 구성이므로 새 구성을 그대로 받아들인다 (Setup으로 최신 능력치를 볼 수 있게)
            float previousShopHp = _setup.ShopMaxHp;
            setup.Level = _setup.Level;
            setup.StageModifier = _setup.StageModifier;
            _setup = setup;
            _mods = setup.Modifiers ?? new BattleModifiers();
            ApplyProtection();
            ComputeGlobalStats();
            for (int slot = 0; slot < _guardians.Length; slot++)
            {
                var g = _guardianUnits[slot];
                if (g == null) continue;
                g.State.Stats = SanitizeStats(setup.Guardians[slot].Stats);
                ApplyGuardianStats(g);
            }
            for (int i = 0; i < deckCount; i++)
            {
                var slot = _deck[i];
                if (slot == null) continue;
                slot.Stats = SanitizeStats(setup.Deck[i].Stats);
                slot.Cost = EchoCost(slot.Stats.summonCost, slot.LineageId, slot.FormId);
            }
            SetShopMaxHp(SafePositive(setup.ShopMaxHp, previousShopHp));
            return true;
        }

        /// <summary>수선점 최대 내구도 변경 (시너지 배율 적용 전 값. 늘어난 만큼 현재 내구도도 채운다)</summary>
        public void SetShopMaxHp(float maxHp)
        {
            maxHp = SafePositive(maxHp, _setup.ShopMaxHp > 0f ? _setup.ShopMaxHp : 100f);
            _setup.ShopMaxHp = maxHp;
            float effective = Mathf.Max(1f, maxHp * _shopMaxHpScale);
            if (!_started)
            {
                _shopMaxHp = _shopHp = effective;
                return;
            }
            if (effective > _shopMaxHp) _shopHp += effective - _shopMaxHp;
            _shopMaxHp = effective;
            _shopHp = Mathf.Min(_shopHp, _shopMaxHp);
            _dangerReference = Mathf.Max(1f, _shopMaxHp * _config.dangerReferenceRatio);
        }

        // ───────────────────────────── 진행 ─────────────────────────────

        /// <summary>내부적으로 고정 간격으로 나눠 진행한다</summary>
        public void Tick(float dt)
        {
            if (!_started || _outcome != BattleOutcome.Running) return;
            if (!(dt > 0f)) return; // 0, 음수, NaN 무시
            _accumulator += Mathf.Min(dt, MaxTickSeconds);
            _stepping = true;
            try
            {
                while (_accumulator >= StepSeconds)
                {
                    _accumulator -= StepSeconds;
                    Step(StepSeconds);
                    if (_outcome != BattleOutcome.Running)
                    {
                        _accumulator = 0f;
                        break;
                    }
                }
            }
            finally
            {
                _stepping = false;
                FlushCasts();
                RemoveDead();
            }
        }

        /// <summary>이번 진행 전 자리 · 공격 자세를 남긴다 (뷰 보간 전용 — 같은 List, 할당 없음, 판정은 읽지 않는다. perf PF-2 F1 · F4)</summary>
        void SnapshotForView()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                e.PrevPosition = e.Position;
                e.PrevAttackAnim = e.AttackAnim;
            }
            for (int i = 0; i < _echoes.Count; i++) _echoes[i].PrevPosition = _echoes[i].Position;
        }

        void Step(float dt)
        {
            SnapshotForView();
            StepCount++;
            _time += dt;
            if (_startPending) BeginBattle();

            _resonance = Mathf.Min(_maxResonance, _resonance + _config.resonanceRegen * _resonanceRegenScale * dt);
            for (int i = 0; i < _deck.Count; i++)
            {
                var slot = _deck[i];
                if (slot != null && slot.Cooldown > 0f) slot.Cooldown = Mathf.Max(0f, slot.Cooldown - dt);
            }

            UpdateShopStatus(dt);
            UpdateSpawner(dt);
            UpdateEnemyGimmicks(dt);
            UpdateEnemies(dt);
            FlushCasts();
            if (_outcome == BattleOutcome.Running) UpdateGuardians(dt);
            FlushCasts();
            if (_outcome == BattleOutcome.Running) UpdateEchoes(dt);
            FlushCasts();
            RemoveDead();
            if (_outcome != BattleOutcome.Running) return;

            UpdateDanger();
            if (AutoMode) UpdateAuto(dt);
            RetryHeldStartCasts(dt);
            FlushCasts();
            RemoveDead();
        }

        /// <summary>
        /// 전투 시작 연출 이벤트 + 전투 시작 스킬 (첫 진행 때 한 번).
        /// 지킴이는 자기 자리에서 쓰고, 덱 칸(아직 소환 전인 잔향이)의 전투 시작 스킬은 수선점이 대신 쓴다 — 던전처럼 덱만 있어도 나간다.
        /// 팀 공용 스킬(시너지 보너스)은 구성원이 몇이든 한 번만 (지킴이 우선, 그다음 공격력 순).
        /// </summary>
        void BeginBattle()
        {
            _startPending = false;
            var synergies = _mods.ActiveSynergies;
            for (int i = 0; i < synergies.Count; i++)
                Emit(new BattleEvent(BattleEventType.SynergyActivated, -1, -1, Vector2.zero, synergies[i].Value, synergies[i].Key));
            if (_setup.StageModifier != StageModifierType.None)
                Emit(new BattleEvent(BattleEventType.StageModifierStarted, -1, -1, Vector2.zero, (int)_setup.StageModifier,
                    SkillVfx.Overlay(_setup.StageModifier)));
            _triggerBatch++;
            for (int slot = 0; slot < _guardianUnits.Length; slot++)
            {
                var g = _guardianUnits[slot];
                if (g != null) QueueTrigger(g, SkillTrigger.OnBattleStart, null, Vector2.zero, false);
            }
            QueueDeckTrigger(SkillTrigger.OnBattleStart);
            _triggerBatch--;
            if (_triggerBatch == 0) CommitShared();
            FlushCasts();
        }

        // ───────────────────────────── 적 생성 ─────────────────────────────

        void PlanNextWave()
        {
            _pressureA = _random.Next(BattleGeometry.SlotCount);
            _pressureCount = _random.NextDouble() < _config.twoPressureChance ? 2 : 1;
            _pressureB = (_pressureA + 1 + _random.Next(BattleGeometry.SlotCount - 1)) % BattleGeometry.SlotCount;
        }

        void UpdateSpawner(float dt)
        {
            if (!_spawningEnabled) return;
            float rate = _bossSpawned ? Mathf.Clamp01(_config.bossPhaseSpawnScale) : 1f;
            if (rate <= 0f) return;

            if (_waveRemaining <= 0)
            {
                _waveRest -= dt * rate;
                if (_waveRest > 0f) return;
                _wave++;
                _waveRemaining = _waveSize;
                _spawnTimer = 0f;
                // 등장할 일반 적이 없으면 바로 보스
                if (!_bossSpawned && _spawnPool.Count == 0) SpawnBoss();
            }

            _spawnTimer -= dt * rate;
            if (_spawnTimer > 0f) return;
            _spawnTimer = _spawnInterval;
            int maxEnemies = _setup.MaxEnemiesOverride > 0 ? _setup.MaxEnemiesOverride : _config.maxEnemies;
            if (_spawnPool.Count == 0 || _enemyUnits.Count >= maxEnemies) return;

            float angle = BattleGeometry.SlotAngle(PickLane()) + RandomJitter();
            SpawnEnemy(PickEnemyDef(), angle, BattleGeometry.SpawnRadius, false);
            _waveRemaining--;
            if (_waveRemaining <= 0)
            {
                _waveRest = _waveRestTime;
                PlanNextWave();
            }
        }

        int PickLane()
        {
            if (_random.NextDouble() < _config.pressureBias)
                return _pressureCount == 2 && _random.NextDouble() < 0.5 ? _pressureB : _pressureA;
            return _random.Next(BattleGeometry.SlotCount);
        }

        float RandomJitter() => ((float)_random.NextDouble() * 2f - 1f) * _config.laneJitterDegrees * Mathf.Deg2Rad;

        EnemyDef PickEnemyDef()
        {
            float roll = (float)_random.NextDouble() * _spawnWeightTotal;
            for (int i = 0; i < _spawnPool.Count; i++)
            {
                roll -= _spawnWeights[i];
                if (roll < 0f) return _spawnPool[i];
            }
            return _spawnPool[_spawnPool.Count - 1];
        }

        void SpawnBoss()
        {
            if (_bossSpawned) return;
            _bossSpawned = true;
            if (_bossPool.Count == 0)
            {
                // 보스 데이터가 없으면 처치 목표 달성만으로 클리어
                Clear();
                return;
            }
            var def = _bossPool[(_setup.Level - 1) % _bossPool.Count];
            float angle = BattleGeometry.SlotAngle(_pressureA) + RandomJitter() * 0.5f;
            _boss = SpawnEnemy(def, angle, BattleGeometry.SpawnRadius, true);
        }

        EnemyUnit SpawnEnemy(EnemyDef def, float angle, float radius, bool boss, bool split = false)
        {
            float splitScale = split ? Mathf.Clamp(_config.splitHpScale, 0.05f, 1f) : 1f;
            // 튜토리얼 보호: 등장할 때 최대 체력 × EnemyHpScale (분열체는 부모와 같은 배율이 한 번만 걸린다)
            float maxHp = Mathf.Max(1f, def.hp * _hpScale * splitScale * _protHpScale);
            var outDir = BattleGeometry.FromAngle(angle, 1f);
            var unit = new EnemyUnit
            {
                OutDirection = outDir,
                Radius = radius,
                Lane = BattleGeometry.NearestSlot(angle),
                Speed = Mathf.Max(0.01f, def.speed * _speedScale * StageEnemySpeedScale() * (split ? Mathf.Max(0.1f, _config.splitSpeedScale) : 1f)),
                Damage = Mathf.Max(0f, def.damage * _damageScale * (split ? Mathf.Clamp(_config.splitDamageScale, 0f, 1f) : 1f)),
                MinionTimer = Mathf.Max(1f, def.minionInterval),
                Gimmicks = def.gimmicks,
                State = new EnemyState
                {
                    Id = _nextEnemyId++,
                    Def = def,
                    Position = outDir * radius,
                    PrevPosition = outDir * radius,
                    Hp = maxHp,
                    MaxHp = maxHp,
                    IsBoss = boss,
                    Alive = true,
                    IsSplit = split,
                },
            };
            unit.EchoDamage = unit.Damage * Mathf.Max(0f, def.echoDamageMultiplier) * _config.enemyEchoDps;
            InitEnemyGimmicks(unit, splitScale);
            _enemyUnits.Add(unit);
            _enemies.Add(unit.State);

            var state = unit.State;
            Emit(new BattleEvent(BattleEventType.EnemySpawned, state.Id, -1, state.Position, boss ? 1f : 0f));
            if (boss) Emit(new BattleEvent(BattleEventType.BossSpawned, state.Id, -1, state.Position, maxHp));
            return unit;
        }

        // ───────────────────────────── 적 이동 ─────────────────────────────

        void UpdateEnemies(float dt)
        {
            float decay = dt * AnimDecayPerSecond;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var unit = _enemyUnits[i];
                var e = unit.State;
                if (!e.Alive) continue;
                e.HitAnim = Mathf.Max(0f, e.HitAnim - decay);
                e.AttackAnim = Mathf.Max(0f, e.AttackAnim - decay);
                UpdateEnemyStatus(unit, dt);
                if (_outcome != BattleOutcome.Running) return;
                if (!e.Alive) continue;

                if (e.IsBoss) UpdateMinions(unit, dt);

                if (unit.AtShop)
                {
                    // 수선점에 붙은 보스: 주기적으로 내구도를 갉아먹는다 (기절하면 공격 간격도 예비 동작도 멈춘다)
                    if (e.StunTimer > 0f)
                    {
                        e.SiegeWindup = 0f;
                        continue;
                    }
                    unit.SiegeTimer -= dt * (e.Enraged ? Mathf.Max(1f, _config.enrageSpeedScale) : 1f);
                    if (unit.SiegeTimer <= 0f)
                    {
                        unit.SiegeTimer = Mathf.Max(0.1f, _config.bossSiegeInterval);
                        e.SiegeWindup = 0f;
                        DamageShop(unit);
                        if (_outcome != BattleOutcome.Running) return;
                        continue;
                    }
                    e.SiegeWindup = Mathf.Clamp01(1f - unit.SiegeTimer / Mathf.Max(0.05f, _config.bossSiegeWindupSeconds));
                    continue;
                }
                e.SiegeWindup = 0f;
                if (e.StunTimer > 0f) continue;

                var blocker = FindBlocker(unit);
                if (blocker != null)
                {
                    DamageEcho(blocker, unit, unit.EchoDamage * EnemyRageScale(unit) * dt);
                    continue;
                }

                float speed = unit.Speed * (e.SlowTimer > 0f ? unit.SlowMul : 1f) * unit.HasteMul
                              * (e.Enraged ? Mathf.Max(0.1f, _config.enrageSpeedScale) : 1f);
                unit.Radius -= speed * dt;
                if (unit.Radius <= BattleGeometry.ShopRadius)
                {
                    unit.Radius = BattleGeometry.ShopRadius;
                    e.Position = unit.OutDirection * unit.Radius;
                    if (e.IsBoss)
                    {
                        unit.AtShop = true;
                        // 처음 붙을 때만 바로 한 대. 넉백/끌어당김으로 떨어졌다 돌아온 보스는 남은 공격 간격을 이어서 센다
                        // (밀어내는 방어 스킬이 오히려 다음 공격과 불빛 흡수를 앞당기지 않게)
                        if (!unit.HasSieged)
                        {
                            unit.HasSieged = true;
                            unit.SiegeTimer = Mathf.Max(0.1f, _config.bossSiegeInterval);
                            DamageShop(unit);
                        }
                    }
                    else
                    {
                        // 일반 적은 수선점에 닿으면 피해를 주고 사라진다 (처치 아님)
                        e.Alive = false;
                        _listsDirty = true;
                        DamageShop(unit);
                    }
                    if (_outcome != BattleOutcome.Running) return;
                    continue;
                }
                e.Position = unit.OutDirection * unit.Radius;
            }
        }

        void UpdateMinions(EnemyUnit boss, float dt)
        {
            var def = boss.State.Def;
            if (string.IsNullOrEmpty(def.minionId) || def.minionCount <= 0) return;
            boss.MinionTimer -= dt;
            if (boss.MinionTimer > 0f) return;
            float interval = Mathf.Max(1f, def.minionInterval);
            if (boss.State.Enraged) interval = Mathf.Max(1f, interval * Mathf.Clamp(_config.enrageMinionIntervalScale, 0.1f, 1f));
            boss.MinionTimer = interval;
            var minion = FindEnemyDef(def.minionId);
            if (minion == null || minion.isBoss) return;
            float baseAngle = Mathf.Atan2(boss.OutDirection.y, boss.OutDirection.x);
            for (int i = 0; i < def.minionCount && _enemyUnits.Count < _config.maxEnemies; i++)
            {
                float angle = baseAngle + RandomJitter() * 0.75f;
                float radius = Mathf.Min(BattleGeometry.SpawnRadius, boss.Radius + 0.15f);
                SpawnEnemy(minion, angle, radius, false);
            }
        }

        /// <summary>적의 발을 묶는 근접 잔향이 (없으면 null). 보이지 않는 적(Phasing)은 잔향이 사이를 스며 지나간다</summary>
        EchoUnit FindBlocker(EnemyUnit enemy)
        {
            if (!enemy.State.Revealed) return null;
            float radiusSq = _config.echoBlockRadius * _config.echoBlockRadius;
            var pos = enemy.State.Position;
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var echo = _echoUnits[i];
                if (!echo.Melee || !echo.State.Alive || echo.State.Hp <= 0f) continue;
                if ((echo.State.Position - pos).sqrMagnitude <= radiusSq) return echo;
            }
            return null;
        }

        // ───────────────────────────── 지킴이 ─────────────────────────────

        void UpdateGuardians(float dt)
        {
            float decay = dt * AnimDecayPerSecond;
            float guardRadiusSq = _config.shopGuardRadius * _config.shopGuardRadius;
            for (int slot = 0; slot < _guardianUnits.Length; slot++)
            {
                var g = _guardianUnits[slot];
                if (g == null) continue;
                var state = g.State;
                state.AttackAnim = Mathf.Max(0f, state.AttackAnim - decay);
                if (state.Cooldown > 0f) state.Cooldown = Mathf.Max(0f, state.Cooldown - dt);
                UpdateUnitStatus(g, dt);
                state.BuffTimer = Mathf.Max(g.AttackBuffTimer, g.SpeedBuffTimer);
                state.DebuffTimer = Mathf.Max(g.RustTimer, g.GnawTimer);

                var style = state.Stats.style;
                if (style == CombatStyle.Support || style == CombatStyle.Heal)
                {
                    g.SupportTimer -= dt;
                    if (g.SupportTimer <= 0f)
                    {
                        // 맥박 연출 이벤트를 먼저 낸다 (회복량이 0이어도 맥박은 보이게) — 결과(ShopHealed/EchoHealed)는 그 뒤에
                        bool support = style == CombatStyle.Support;
                        Emit(new BattleEvent(BattleEventType.GuardianPulse, slot, -1, g.RingPosition, (int)style,
                            support ? BattleEventKeys.PulseSupport : BattleEventKeys.PulseHeal));
                        if (support)
                        {
                            g.SupportTimer = Mathf.Max(0.5f, _config.supportInterval);
                            SupportPulse(g);
                        }
                        else
                        {
                            g.SupportTimer = Mathf.Max(0.5f, _config.healInterval);
                            HealPulse(g);
                        }
                    }
                }

                UpdateSkillTimers(g, dt);
                if (_outcome != BattleOutcome.Running) return;

                bool wantsTarget = state.Cooldown <= 0f;
                if (!wantsTarget && style != CombatStyle.Defense) continue;

                // 부채꼴 안 적 탐색 (방어형은 범위 안 적 전부 감속)
                EnemyUnit target = null;
                float best = float.MaxValue;
                float auraSlow = (1f - _config.defenseSlowFactor) * g.SlowPower;
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    var unit = _enemyUnits[i];
                    var e = unit.State;
                    if (!e.Alive || !CanSee(g, unit)) continue;
                    if (!InFan(e.Position, g, guardRadiusSq)) continue;
                    if (style == CombatStyle.Defense) ApplySlow(unit, auraSlow, AuraSlowSeconds, false);
                    if (!wantsTarget) continue;
                    float score = TargetScore(style, unit);
                    if (score < best)
                    {
                        best = score;
                        target = unit;
                    }
                }
                if (target == null) continue;

                g.Target = target;
                float damage = g.Damage * g.AttackFactor;
                state.Cooldown = g.Interval / g.SpeedFactor;
                state.AttackAnim = 1f;
                Emit(new BattleEvent(BattleEventType.GuardianAttack, slot, target.State.Id, target.State.Position, damage));
                OnBasicHit(g, target);
                HitEnemy(target, damage, g, slot, true, 0f);
                if (_outcome != BattleOutcome.Running) return;
            }
        }

        bool InFan(Vector2 pos, GuardianUnit g, float guardRadiusSq)
        {
            float distSq = pos.sqrMagnitude;
            // 수선점에 바짝 붙은 적은 방향과 상관없이 모든 지킴이가 공격
            if (distSq <= guardRadiusSq) return true;
            if (distSq > g.RangeSq) return false;
            float dot = pos.x * g.Direction.x + pos.y * g.Direction.y;
            return dot >= Mathf.Sqrt(distSq) * g.CosHalfArc;
        }

        /// <summary>낮을수록 먼저 노린다. 기본은 수선점에 가장 가까운 적, 저격은 보스 우선, 포획은 아직 묶이지 않은 적 우선</summary>
        static float TargetScore(CombatStyle style, EnemyUnit unit)
        {
            float score = unit.Radius;
            if (style == CombatStyle.Snipe && unit.State.IsBoss) score -= 10f;
            if (style == CombatStyle.Capture && unit.State.StunTimer > 0f) score += 5f;
            return score;
        }

        /// <summary>성향별 기본 공격 부가 효과: 포획 = 짧은 기절, 지원 = 표식, 방어 잔향이 = 감속, 빛 특성 = 보이지 않는 적 드러냄</summary>
        void OnBasicHit(CombatUnit u, EnemyUnit target)
        {
            switch (u.Style)
            {
                case CombatStyle.Capture:
                    Stun(target, _config.captureStunSeconds, u);
                    break;
                case CombatStyle.Support:
                    ApplyMark(target, _config.supportMarkPercent, _config.supportMarkSeconds);
                    break;
                case CombatStyle.Defense:
                    if (u is EchoUnit) ApplySlow(target, (1f - _config.defenseSlowFactor) * u.SlowPower, EchoHitSlowSeconds, false);
                    break;
            }
            if (u.IsLight && target.Has(EnemyGimmick.Phasing)) Reveal(target, _config.lightHitRevealSeconds);
        }

        /// <summary>지원 성향 지킴이: 수선점 회복 + 양옆 지킴이와 가까운 잔향이 공격력 강화 (회복량은 스킬처럼 강화·공격력 상승 반영)</summary>
        void SupportPulse(GuardianUnit g)
        {
            float heal = g.CurrentSkillAttack * g.HealPower * _config.supportHealScale;
            HealShop(heal, g.Slot, -1, g.RingPosition);
            float buff = Mathf.Max(0f, _config.supportBuffSeconds);
            int left = (g.Slot + 1) % BattleGeometry.SlotCount;
            int right = (g.Slot + BattleGeometry.SlotCount - 1) % BattleGeometry.SlotCount;
            if (_guardianUnits[left] != null) ApplyAttackBuff(_guardianUnits[left], _config.supportBuffPercent, buff);
            if (_guardianUnits[right] != null) ApplyAttackBuff(_guardianUnits[right], _config.supportBuffPercent, buff);
            BuffEchoesNear(g.RingPosition, _config.supportEchoBuffRadius, _config.supportBuffPercent, buff);
        }

        /// <summary>힐 성향 지킴이: 수선점 회복 + 자기 방향 근처 잔향이 회복 (회복량은 스킬처럼 강화·공격력 상승 반영)</summary>
        void HealPulse(GuardianUnit g)
        {
            float power = g.CurrentSkillAttack * g.HealPower;
            HealShop(power * _config.healShopScale, g.Slot, -1, g.RingPosition);
            HealEchoesNear(g.RingPosition, _config.healEchoRadius, power * _config.healEchoScale, g.Slot);
        }

        // ───────────────────────────── 잔향이 ─────────────────────────────

        public bool CanSummon(int deckSlot)
        {
            if (!_started || _outcome != BattleOutcome.Running) return false;
            if (deckSlot < 0 || deckSlot >= _deck.Count) return false;
            var slot = _deck[deckSlot];
            if (slot == null || slot.Cooldown > 0f) return false;
            if (_resonance < slot.Cost) return false;
            return _deckEchoCount < Mathf.Max(1, _config.maxEchoes);
        }

        /// <summary>angle이 null이면 가장 위험한 방향으로 자동 이동</summary>
        public bool TrySummon(int deckSlot, float? angle = null)
        {
            if (!CanSummon(deckSlot)) return false;
            var slot = _deck[deckSlot];
            var runtime = _deckRuntime[deckSlot];
            _resonance -= slot.Cost;
            slot.Cooldown = slot.CooldownMax;

            bool auto = !angle.HasValue || float.IsNaN(angle.Value) || float.IsInfinity(angle.Value);
            float guardAngle = auto ? _dangerAngle : angle.Value;
            var unit = CreateEcho(slot.FormId, slot.LineageId, runtime.TraitMask, slot.Stats, deckSlot, guardAngle, auto,
                BattleGeometry.FromAngle(guardAngle, EchoSpawnRadius), false, slot.Cost);
            BuildUnitSkills(unit, runtime.Skills);
            Emit(new BattleEvent(BattleEventType.EchoSummoned, unit.State.Id, -1, unit.State.Position, deckSlot));
            QueueTrigger(unit, SkillTrigger.OnSummon, null, unit.State.Position, false);
            if (!Busy)
            {
                FlushCasts();
                RemoveDead();
            }
            return true;
        }

        /// <summary>잔향이 상태 생성 + 목록 등록 (덱 소환과 스킬 토큰 공용)</summary>
        EchoUnit CreateEcho(string formId, string lineageId, int traitMask, CharacterStats stats, int deckSlot, float guardAngle,
            bool auto, Vector2 position, bool token, int cost)
        {
            var unit = new EchoUnit
            {
                StyleValue = stats.style,
                Cost = cost,
                GuardAngle = guardAngle,
                AutoDirected = auto,
                SupportTimer = 0.5f,
                FormId = formId,
                LineageId = lineageId,
            };
            SetTraits(unit, traitMask);
            unit.State = new EchoState
            {
                Id = _nextEchoId++,
                DeckSlot = deckSlot,
                FormId = formId,
                LineageId = lineageId,
                Stats = stats,
                Position = position,
                PrevPosition = position,
                Heading = guardAngle,
                Alive = true,
                IsToken = token,
            };
            ApplyEchoStats(unit);
            float hp = stats.hp * _config.echoHpScale * (stats.style == CombatStyle.Defense ? 1.5f : 1f) * unit.HpScale;
            float life = Mathf.Max(1f, stats.lifetime * unit.LifetimeScale);
            unit.State.Hp = unit.State.MaxHp = Mathf.Max(1f, hp);
            unit.State.Life = unit.State.MaxLife = life;
            _echoUnits.Add(unit);
            _echoes.Add(unit.State);
            if (token) _tokenCount++;
            else _deckEchoCount++;
            return unit;
        }

        void ApplyEchoStats(EchoUnit unit)
        {
            RefreshCombatStats(unit);
            var stats = unit.State.Stats;
            float damage = unit.SkillAttack;
            float interval = stats.attackInterval / unit.AttackSpeedScale;
            float range = Mathf.Clamp(stats.range * _config.echoRangeScale, _config.echoRangeMin, Mathf.Max(_config.echoRangeMin, _config.echoRangeMax));
            switch (stats.style)
            {
                case CombatStyle.Assault:
                    damage *= _config.assaultDamageScale;
                    break;
                case CombatStyle.Capture:
                    damage *= _config.captureDamageScale;
                    break;
                case CombatStyle.Snipe:
                    damage *= _config.snipeDamageScale;
                    interval *= _config.snipeIntervalScale;
                    range *= _config.echoSnipeRangeScale;
                    break;
                case CombatStyle.Support:
                    damage *= _config.supportDamageScale;
                    break;
                case CombatStyle.Heal:
                    damage *= _config.healDamageScale;
                    break;
            }
            unit.Melee = range <= _config.echoBlockRadius * 1.2f;
            unit.Damage = Mathf.Max(0f, damage);
            unit.Interval = Mathf.Max(0.1f, interval);
            unit.Range = Mathf.Max(0.05f, range * unit.RangeScale);
            unit.MoveSpeed = Mathf.Max(0.05f, stats.moveSpeed * unit.MoveScale);
        }

        void UpdateEchoes(float dt)
        {
            float decay = dt * AnimDecayPerSecond;
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var unit = _echoUnits[i];
                var s = unit.State;
                if (!s.Alive) continue;
                s.AttackAnim = Mathf.Max(0f, s.AttackAnim - decay);
                if (unit.Cooldown > 0f) unit.Cooldown = Mathf.Max(0f, unit.Cooldown - dt);
                s.Life -= dt;
                if (s.Hp <= 0f || s.Life <= 0f)
                {
                    Dissolve(unit);
                    continue;
                }
                UpdateUnitStatus(unit, dt);
                UpdateEchoStatus(unit, dt);
                s.BuffTimer = Mathf.Max(unit.AttackBuffTimer, unit.SpeedBuffTimer);
                s.DebuffTimer = Mathf.Max(unit.RustTimer, unit.GnawTimer);

                if (unit.StyleValue == CombatStyle.Support || unit.StyleValue == CombatStyle.Heal)
                {
                    unit.SupportTimer -= dt;
                    if (unit.SupportTimer <= 0f) EchoSupportPulse(unit);
                }

                UpdateSkillTimers(unit, dt);
                if (_outcome != BattleOutcome.Running) return;

                UpdateEchoTarget(unit);
                if (unit.Target != null) EchoEngage(unit, dt);
                else EchoPatrol(unit, dt);
                if (_outcome != BattleOutcome.Running) return;
            }
        }

        /// <summary>
        /// 지원 잔향이 = 수선점 조금 회복 + 주변 잔향이 강화, 힐 잔향이 = 주변 잔향이(자신 포함) 회복 + 수선점 조금 회복.
        /// 회복량은 스킬처럼 강화·공격력 상승(몽당이처럼 오래 탈수록 따뜻해짐)·갉아먹힘을 반영한다.
        /// </summary>
        void EchoSupportPulse(EchoUnit unit)
        {
            var s = unit.State;
            float power = unit.CurrentSkillAttack * unit.HealPower;
            if (unit.StyleValue == CombatStyle.Support)
            {
                unit.SupportTimer = Mathf.Max(0.5f, _config.supportInterval);
                HealShop(power * _config.supportHealScale * _config.echoSupportHealScale, -1, s.Id, s.Position);
                BuffEchoesNear(s.Position, _config.supportEchoBuffRadius, _config.supportBuffPercent, _config.supportBuffSeconds);
            }
            else
            {
                unit.SupportTimer = Mathf.Max(0.5f, _config.healInterval);
                HealEchoesNear(s.Position, _config.healEchoRadius, power * _config.healEchoScale, s.Id);
                HealShop(power * _config.healShopScale * _config.echoSupportHealScale, -1, s.Id, s.Position);
            }
        }

        void UpdateEchoTarget(EchoUnit unit)
        {
            var s = unit.State;
            float aggro = Mathf.Max(unit.Range, _config.echoAggroRadius);
            if (unit.Target != null)
            {
                var t = unit.Target.State;
                // 너무 멀어지거나 죽은(또는 다시 숨은) 적은 놓아준다
                if (!t.Alive || !CanSee(unit, unit.Target) || (t.Position - s.Position).sqrMagnitude > aggro * aggro * 2.25f) unit.Target = null;
            }
            if (unit.Target == null)
            {
                float best = aggro * aggro;
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    var enemy = _enemyUnits[i];
                    var e = enemy.State;
                    if (!e.Alive || !CanSee(unit, enemy)) continue;
                    float d = (e.Position - s.Position).sqrMagnitude;
                    if (d <= best)
                    {
                        best = d;
                        unit.Target = enemy;
                    }
                }
            }
            s.TargetEnemyId = unit.Target != null ? unit.Target.State.Id : -1;
        }

        void EchoEngage(EchoUnit unit, float dt)
        {
            var s = unit.State;
            var target = unit.Target;
            var delta = target.State.Position - s.Position;
            float dist = delta.magnitude;
            float engage = unit.Melee ? Mathf.Min(unit.Range, _config.echoBlockRadius) * 0.9f : unit.Range * 0.9f;
            unit.IdleTime = 0f;
            if (dist > 0.0001f) s.Heading = Mathf.Atan2(delta.y, delta.x);
            if (dist > engage)
            {
                float move = Mathf.Min(unit.MoveSpeed * dt, dist - engage);
                s.Position += delta / dist * move;
                dist -= move;
            }
            if (dist > unit.Range || unit.Cooldown > 0f) return;

            unit.Cooldown = unit.Interval / unit.SpeedFactor;
            s.AttackAnim = 1f;
            var e = target.State;
            float damage = unit.Damage * unit.AttackFactor;
            Emit(new BattleEvent(BattleEventType.EchoAttack, s.Id, e.Id, e.Position, damage));
            OnBasicHit(unit, target);
            HitEnemy(target, damage, unit, -1, true, 0f);
        }

        void EchoPatrol(EchoUnit unit, float dt)
        {
            var s = unit.State;
            // 할 일이 없는 자동 소환 잔향이는 가장 위험한 방향으로 옮겨 간다
            if (unit.AutoDirected && _enemyUnits.Count > 0)
            {
                unit.IdleTime += dt;
                if (unit.IdleTime >= EchoRetargetIdleSeconds)
                {
                    unit.IdleTime = 0f;
                    unit.GuardAngle = _dangerAngle;
                }
            }
            var goal = BattleGeometry.FromAngle(unit.GuardAngle, _config.echoPatrolRadius);
            var delta = goal - s.Position;
            float dist = delta.magnitude;
            if (dist < 0.01f) return;
            float move = Mathf.Min(unit.MoveSpeed * dt, dist);
            s.Position += delta / dist * move;
            s.Heading = Mathf.Atan2(delta.y, delta.x);
        }

        /// <summary>잔향이가 빛으로 풀려 수선점으로 흘러 들어간다 → 잔향 일부 환급 + 불빛 게이지 (토큰은 환급 없음, 게이지 조금)</summary>
        void Dissolve(EchoUnit unit)
        {
            var s = unit.State;
            s.Alive = false;
            s.Hp = Mathf.Max(0f, s.Hp);
            s.Shield = 0f;
            unit.Target = null;
            _listsDirty = true;
            if (s.IsToken) _tokenCount = Math.Max(0, _tokenCount - 1);
            else _deckEchoCount = Math.Max(0, _deckEchoCount - 1);
            float refund = s.IsToken ? 0f : unit.Cost * Mathf.Clamp01(_config.echoRefundRate);
            _resonance = Mathf.Min(_maxResonance, _resonance + refund);
            AddLampGain(_config.lampPerEchoDissolve * (s.IsToken ? Mathf.Clamp01(_config.tokenLampScale) : 1f));
            Emit(new BattleEvent(BattleEventType.EchoDissolved, s.Id, s.DeckSlot, s.Position, refund));
            if (!s.IsToken) QueueTrigger(unit, SkillTrigger.OnDissolve, null, s.Position, true);
        }

        // ───────────────────────────── 피해 / 처치 ─────────────────────────────

        /// <summary>
        /// 실제 피해 적용: 보호막이 먼저 흡수하고 남은 만큼 체력. killer는 처치 기록/처치 시 스킬용 (없으면 null).
        /// EnemyHit의 Value는 보호막 흡수분을 포함한 전체 피해.
        /// </summary>
        void DamageEnemy(EnemyUnit unit, float damage, int hitSourceId, CombatUnit killer, bool fromLamp)
        {
            var e = unit.State;
            if (!e.Alive || !(damage > 0f)) return;
            float total = damage;
            if (e.Shield > 0f)
            {
                float absorbed = Mathf.Min(e.Shield, damage);
                e.Shield -= absorbed;
                damage -= absorbed;
                if (e.Shield <= 0.0001f)
                {
                    e.Shield = 0f;
                    Emit(new BattleEvent(BattleEventType.ShieldBroken, hitSourceId, e.Id, e.Position));
                }
            }
            e.Hp -= damage;
            e.HitAnim = 1f;
            Emit(new BattleEvent(BattleEventType.EnemyHit, hitSourceId, e.Id, e.Position, total));
            if (e.Hp <= 0f) Kill(unit, killer, fromLamp);
            else CheckEnrage(unit);
        }

        void Kill(EnemyUnit unit, CombatUnit killer, bool fromLamp)
        {
            var e = unit.State;
            e.Alive = false;
            e.Hp = 0f;
            _listsDirty = true;
            _kills++;
            if (killer is GuardianUnit guardian) guardian.State.Kills++;
            _resonance = Mathf.Min(_maxResonance, _resonance + (e.IsBoss ? _config.resonanceBossKillGain : _config.resonanceKillGain));
            // 불 밝히기로 쓰러뜨린 적은 게이지를 채우지 않는다 (연쇄 방지)
            if (!fromLamp && !e.IsBoss) AddLampGain(_config.lampPerKill);
            Emit(new BattleEvent(BattleEventType.EnemyKilled, killer?.SourceId ?? -1, e.Id, e.Position, e.IsBoss ? 1f : 0f));

            if (unit.Has(EnemyGimmick.Splitter) && !e.IsSplit) Split(unit);
            if (killer != null && killer.IsAlive) QueueTrigger(killer, SkillTrigger.OnKill, null, e.Position, true);

            if (e.IsBoss)
            {
                Clear();
            }
            else if (!_bossSpawned && _kills >= _killsToBoss)
            {
                SpawnBoss();
            }
        }

        /// <summary>불빛 게이지 직접 충전 (배율 없음)</summary>
        void AddLamp(float amount)
        {
            if (!(amount > 0f)) return;
            _lamp = Mathf.Min(1f, _lamp + amount);
            if (_lamp > 0.999f) _lamp = 1f; // 부동소수 오차로 가득 차지 않는 일 방지
        }

        /// <summary>불빛 게이지 충전 (시너지 LampGainPercent / 보름달 배율 적용)</summary>
        void AddLampGain(float amount) => AddLamp(amount * _lampGainScale);

        void Clear()
        {
            if (_outcome != BattleOutcome.Running) return;
            _outcome = BattleOutcome.Cleared;
            Emit(new BattleEvent(BattleEventType.StageCleared, -1, -1, default, _time));
        }

        void Fail()
        {
            if (_outcome != BattleOutcome.Running) return;
            _outcome = BattleOutcome.Failed;
            Emit(new BattleEvent(BattleEventType.StageFailed, -1, -1, default, _time));
        }

        // ───────────────────────────── 불 밝히기 ─────────────────────────────

        public bool TryLightLamp()
        {
            if (!CanLightLamp) return false;
            _lamp = 0f;
            int count = _enemyUnits.Count;
            int alive = 0;
            for (int i = 0; i < count; i++) if (_enemyUnits[i].State.Alive) alive++;
            Emit(new BattleEvent(BattleEventType.LampLit, -1, -1, Vector2.zero, alive));
            // 수선점 불빛은 숨어 있던 그림자도 드러낸다
            for (int i = 0; i < count; i++)
            {
                var unit = _enemyUnits[i];
                if (unit.State.Alive && unit.Has(EnemyGimmick.Phasing)) Reveal(unit, _config.lampRevealSeconds);
            }
            for (int i = 0; i < count; i++)
            {
                var unit = _enemyUnits[i];
                var e = unit.State;
                if (!e.Alive) continue;
                float ratio = e.IsBoss ? _config.lampBossDamageRatio : _config.lampDamageRatio;
                DamageEnemy(unit, e.MaxHp * Mathf.Max(0f, ratio) * _lampDamageScale, -1, null, true);
                if (_outcome != BattleOutcome.Running) break;
            }
            QueueTriggerAll(SkillTrigger.OnLampLit);
            // 진행 도중(이벤트 콜백 등)이 아니면 바로 스킬 처리 + 목록 정리
            if (!Busy)
            {
                FlushCasts();
                RemoveDead();
            }
            return true;
        }

        // ───────────────────────────── 위협도 ─────────────────────────────

        void UpdateDanger()
        {
            Array.Clear(_danger, 0, _danger.Length);
            Array.Clear(_dangerVector, 0, _dangerVector.Length);
            _uncoveredThreat = false;
            float span = BattleGeometry.SpawnRadius - BattleGeometry.ShopRadius;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var unit = _enemyUnits[i];
                if (!unit.State.Alive) continue;
                float t = Mathf.Clamp01((unit.Radius - BattleGeometry.ShopRadius) / span);
                float threat = Mathf.Max(0.1f, unit.Damage * EnemyRageScale(unit)) * (1f - 0.7f * t);
                if (unit.State.IsBoss) threat *= BossThreatScale;
                if (_guardians[unit.Lane] != null) threat *= CoveredLaneThreatScale;
                else _uncoveredThreat = true;
                _danger[unit.Lane] += threat;
                _dangerVector[unit.Lane] += unit.OutDirection * threat;
            }

            int maxSlot = -1;
            _maxDanger = 0f;
            for (int slot = 0; slot < _danger.Length; slot++)
            {
                if (_danger[slot] > _maxDanger)
                {
                    _maxDanger = _danger[slot];
                    maxSlot = slot;
                }
            }
            // 적이 없으면 다음 웨이브 압박 방향을 미리 가리킨다
            _dangerAngle = maxSlot >= 0
                ? Mathf.Atan2(_dangerVector[maxSlot].y, _dangerVector[maxSlot].x)
                : BattleGeometry.SlotAngle(_pressureA);
        }

        /// <summary>적 위협이 가장 큰 방향 (라디안)</summary>
        public float GetDangerAngle() => _dangerAngle;

        /// <summary>방위 슬롯 방향의 위협도 0~1 (경고 화살표 표시용)</summary>
        public float GetDanger(int slot)
        {
            if (slot < 0 || slot >= _danger.Length) return 0f;
            return Mathf.Clamp01(_danger[slot] / _dangerReference);
        }

        // ───────────────────────────── 자동 전투 ─────────────────────────────

        void UpdateAuto(float dt)
        {
            _autoTimer -= dt;
            if (_autoTimer <= 0f)
            {
                _autoTimer = Mathf.Max(StepSeconds, _config.autoSummonInterval);
                AutoSummon();
            }
            if (CanLightLamp && ShouldAutoLamp()) TryLightLamp();
        }

        /// <summary>
        /// 덱을 순서대로 돌며 소환. 차례인 슬롯의 잔향이 모자라면 모일 때까지 기다린다.
        /// 지킴이가 없는 방향에 적이 있거나 위협도가 기준 이상일 때, 또는 잔향이 넘치기 직전일 때 소환한다.
        /// 적이 하나도 없을 때(웨이브 사이) 넘치는 잔향으로 미리 부를 때는, 소환 시 적을 노리는 스킬을 가진 칸은 건너뛴다
        /// (목마 돌격·소나기 같은 한 번뿐인 스킬이 헛돌지 않게 — 적이 나오면 그때 부른다).
        /// </summary>
        void AutoSummon()
        {
            if (_deck.Count == 0) return;
            bool threatened = _enemyUnits.Count > 0 && (_uncoveredThreat || _maxDanger / _dangerReference >= _config.autoSummonDanger);
            bool overflowing = _resonance >= _maxResonance - 0.5f;
            if (!threatened && !overflowing) return;
            if (_deckEchoCount >= Mathf.Max(1, _config.maxEchoes)) return;
            bool enemyAlive = AnyEnemyAlive();

            for (int k = 0; k < _deck.Count; k++)
            {
                int index = (_autoNext + k) % _deck.Count;
                var slot = _deck[index];
                if (slot == null || slot.Cooldown > 0f) continue;
                if (!enemyAlive && _deckRuntime[index].NeedsEnemyOnSummon) continue;
                if (_resonance < slot.Cost) return;
                if (TrySummon(index)) _autoNext = (index + 1) % _deck.Count;
                return;
            }
        }

        bool ShouldAutoLamp()
        {
            if (_setup.LampPolicy == LampPolicy.Manual) return false;
            int alive = 0;
            for (int i = 0; i < _enemyUnits.Count; i++) if (_enemyUnits[i].State.Alive) alive++;
            float ratio = _shopMaxHp > 0f ? _shopHp / _shopMaxHp : 1f;
            return AutoLampDecision(_setup.LampPolicy, alive, _boss != null && _boss.State.Alive, ratio, _config.autoLampMinEnemies);
        }

        // ───────────────────────────── 정리 ─────────────────────────────

        /// <summary>진행 중이거나 스킬을 처리하는 중 (이때는 목록 정리/스킬 처리를 미룬다)</summary>
        bool Busy => _stepping || _flushing;

        /// <summary>죽은 유닛을 내부/공개 목록에서 함께 뺀다 (두 목록은 같은 순서로 유지, 할당 없음)</summary>
        void RemoveDead()
        {
            if (!_listsDirty || _flushing) return;
            _listsDirty = false;

            int write = 0;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var unit = _enemyUnits[i];
                if (!unit.State.Alive) continue;
                _enemyUnits[write] = unit;
                _enemies[write] = unit.State;
                write++;
            }
            _enemyUnits.RemoveRange(write, _enemyUnits.Count - write);
            _enemies.RemoveRange(write, _enemies.Count - write);
            if (_boss != null && !_boss.State.Alive) _boss = null;

            write = 0;
            for (int i = 0; i < _echoUnits.Count; i++)
            {
                var unit = _echoUnits[i];
                if (!unit.State.Alive) continue;
                _echoUnits[write] = unit;
                _echoes[write] = unit.State;
                write++;
            }
            _echoUnits.RemoveRange(write, _echoUnits.Count - write);
            _echoes.RemoveRange(write, _echoes.Count - write);
        }

        void Emit(in BattleEvent e) => Event?.Invoke(e);
    }
}
