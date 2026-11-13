using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 스테이지 진행 + 편성 → 전투 시뮬레이션 연결. 클리어/실패 보상, 다음 스테이지 자동 진행,
    /// 잔향이 출격 / 지킴이 처치 기록을 담당한다. GameBootstrap이 매 프레임 Tick을 호출한다.
    /// <para>
    /// 흐름: 전투 → 결과(StageEnded) → stageEndPause초 대기(뷰가 결과 배너 표시) → 다음 전투(StageChanged).
    /// 클리어 = 보상 + 편성 계보 호감도 + 최고 스테이지 갱신 + (자동 진행이면) 다음 스테이지.
    /// 실패 = 같은 스테이지 재도전, 연속 FailsBeforeStepBack회 실패하면 한 단계 아래로 (StepBack, 막힌 스테이지 = ChallengeTarget).
    /// 편성/캐릭터가 바뀌면 다음 Tick에 반영한다: 편성 구성이 바뀌면 다시 시작, 능력치만 바뀌면 진행 중인 전투에 바로 적용.
    /// 시뮬레이터 인스턴스(Sim)는 계속 같은 것을 쓰고 Start만 다시 부른다 (뷰는 Event/StageChanged만 구독하면 된다).
    /// </para>
    /// <para>
    /// 보스에 막힘 (M13): 내려간 뒤 막힌 스테이지 아래에 있으면 "파밍 중"(IsFarming). 자동 보스 도전(FeatureId.AutoChallenge, 2-1 클리어)이
    /// 열린 뒤에는 파밍 중 클리어해도 그 스테이지를 반복하고(붙잡기), ChallengeBoss()로 다시 도전하거나 AutoChallenge(기본 켜짐)가 켜져 있으면
    /// AutoChallengeEvery번째 클리어마다 스스로 다시 도전한다 — 기본값이 켜짐이라 버튼을 누르지 않아도 진행이 멈추지 않는다.
    /// 열리기 전에는 지금처럼 클리어하면 바로 다시 도전한다 (새 플레이어·방치 진행 유지).
    /// 막힌 스테이지 이상을 클리어하면 ChallengeTarget은 0으로 돌아간다.
    /// </para>
    /// <para>
    /// 전투 구성에는 형태 고유 스킬(CharacterFormDef.skill), 계보 특성(LineageDef.traits), 편성 시너지(SynergySystem.BuildModifiers),
    /// 스테이지 기믹(GetStageModifier — 1-1~1-3 없음, 이후 비/바람/정전/먼지 폭풍/보름달이 돌아가며, 챕터 10번째는 축제의 밤)이 들어간다.
    /// 시너지는 편성으로 정해지므로 편성 변경 시 다시 시작할 때 함께 새로 계산된다.
    /// </para>
    /// <para>
    /// 누적 기록은 일어날 때마다 바로 세이브 데이터에 쓴다 (모아 두면 저장 직후 앱이 종료될 때 사라진다).
    /// 자주 오는 ProgressChanged 알림은 받는 쪽(UI)이 dirty 플래그로 모아서 갱신한다.
    /// </para>
    /// </summary>
    public class BattleSystem
    {
        /// <summary>시뮬레이터 이벤트 전달 (뷰 연출용)</summary>
        public event Action<BattleEvent> Event;
        /// <summary>새 스테이지 전투가 시작됨 (클리어/실패/편성 변경 후)</summary>
        public event Action StageChanged;
        /// <summary>스테이지 종료와 지급된 보상</summary>
        public event Action<BattleOutcome, List<RewardData>> StageEnded;
        /// <summary>
        /// (막힌 스테이지 = ChallengeTarget, 내려간 스테이지) 연속 실패로 한 단계 아래로 내려가게 됨. 같은 실패의 StageEnded 바로 앞에 울린다
        /// (HUD "아쉬워요! 1-9에서 힘을 모아요" + 보스 도전 버튼). 1스테이지처럼 더 내려갈 곳이 없으면 울리지 않는다.
        /// 울릴 때(그리고 이어지는 StageEnded 동안)는 결과 배너 대기 중이라 Stage가 아직 막힌 스테이지 → IsFarming은 false다.
        /// 내려간 스테이지 전투가 시작되는 다음 StageChanged부터 true가 되므로 보스 도전 버튼은 StageChanged에서 다시 판단하고,
        /// 배너 문구는 이 두 인자로 만든다.
        /// </summary>
        public event Action<int, int> StepBack;

        public const int MaxSpeed = 2;
        /// <summary>같은 스테이지에서 이만큼 연속 실패하면 한 단계 아래로 내려간다</summary>
        public const int FailsBeforeStepBack = 2;
        /// <summary>자동 보스 도전이 켜져 있으면 파밍 스테이지를 이만큼 클리어할 때마다 막힌 스테이지에 다시 도전한다</summary>
        public const int AutoChallengeEvery = 3;

        readonly PlayerData _data;
        readonly BattleConfig _config;
        readonly CharacterSystem _characters;
        readonly FormationSystem _formation;
        readonly SynergySystem _synergy;
        readonly CurrencySystem _currency;
        readonly RewardService _rewards;
        readonly System.Random _random;
        readonly BattleSimulator _sim;

        bool _started;
        bool _restartPending;
        bool _refreshPending;
        bool _ended;
        float _endTimer;
        int _pendingStage;
        int _consecutiveFails;
        /// <summary>파밍 스테이지 연속 클리어 수 (자동 보스 도전 주기용, 저장 안 함)</summary>
        int _farmClears;
        bool _lastEndSteppedBack;
        int _shopLevel;
        BattleOutcome _lastOutcome;
        /// <summary>마지막으로 만든 전투의 시너지 능력치 (단추 보상 계산용)</summary>
        BattleModifiers _modifiers;
        readonly HashSet<string> _lineageScratch = new();

        public BattleSystem(PlayerData data, BattleConfig config, CharacterSystem characters, FormationSystem formation,
            SynergySystem synergy, CurrencySystem currency, RewardService rewards, System.Random random)
        {
            _synergy = synergy;
            _data = data;
            _config = config != null ? config : CreateDefaultConfig();
            _characters = characters;
            _formation = formation;
            _currency = currency;
            _rewards = rewards;
            _random = random ?? new System.Random();

            // 공용 Random은 전투를 실제로 시작할 때만 쓴다 (생성만으로 다른 시스템의 난수 순서를 바꾸지 않게)
            _sim = new BattleSimulator(_config, 0);
            _sim.Event += OnSimEvent;
            if (_formation != null) _formation.Changed += OnFormationChanged;
            if (_characters != null) _characters.Changed += OnCharactersChanged;
        }

        static BattleConfig CreateDefaultConfig()
        {
            var config = ScriptableObject.CreateInstance<BattleConfig>();
            config.ApplyDefaults();
            return config;
        }

        BattleSave Save => _data.battle ??= new BattleSave();

        public BattleConfig Config => _config;

        public BattleSimulator Sim
        {
            get
            {
                EnsureStarted();
                return _sim;
            }
        }

        public int Stage => Math.Max(1, Save.stage);
        public int HighestStage => Math.Max(Stage, Save.highestStage);
        /// <summary>예: 스테이지 13 → "2-3" (10스테이지 = 1챕터)</summary>
        public string StageLabel => FormatStage(Stage);

        public static string FormatStage(int stage)
        {
            stage = Math.Max(1, stage);
            int chapter = (stage - 1) / 10 + 1;
            int number = (stage - 1) % 10 + 1;
            return $"{chapter}-{number}";
        }

        /// <summary>자동 소환 + 자동 불 밝히기 (저장됨)</summary>
        public bool AutoMode
        {
            get => Save.autoMode;
            set
            {
                Save.autoMode = value;
                _sim.AutoMode = value;
            }
        }

        /// <summary>클리어 시 다음 스테이지 자동 진행 (저장됨)</summary>
        public bool AutoAdvance
        {
            get => Save.autoAdvance;
            set => Save.autoAdvance = value;
        }

        /// <summary>배속 1 또는 2 (저장됨)</summary>
        public int Speed
        {
            get => Mathf.Clamp(Save.speed, 1, MaxSpeed);
            set => Save.speed = Mathf.Clamp(value, 1, MaxSpeed);
        }

        /// <summary>스테이지가 끝나 결과를 보여 주는 중 (다음 전투 대기)</summary>
        public bool IsBetweenStages => _ended;
        /// <summary>다음 전투까지 남은 시간(초)</summary>
        public float NextStageDelay => _ended ? Mathf.Max(0f, _endTimer) : 0f;
        /// <summary>마지막으로 끝난 전투 결과 (아직 없으면 Running)</summary>
        public BattleOutcome LastOutcome => _lastOutcome;
        /// <summary>현재 스테이지 연속 실패 횟수</summary>
        public int ConsecutiveFails => _consecutiveFails;

        // ───────────────────────────── 보스에 막힘 (파밍 / 보스 도전) ─────────────────────────────

        /// <summary>
        /// 기능 개방 확인. Game.SetupMeta가 FeatureGateSystem.IsUnlocked를 넣는다 (정의 없음 = 열림 규칙까지 같아진다).
        /// 비어 있으면(시스템만 만든 테스트) 세이브의 열린 기능 목록(meta.features.unlocked)을 본다 — 콘텐츠에 AutoChallenge 정의가 있으면 같은 결과.
        /// </summary>
        public Func<FeatureId, bool> FeatureResolver { get; set; }

        /// <summary>자동 보스 도전(파밍 붙잡기 + 보스 도전 버튼 + 자동 다시 도전)이 열렸는지 (2-1 클리어)</summary>
        public bool IsAutoChallengeUnlocked
        {
            get
            {
                if (FeatureResolver != null) return FeatureResolver(FeatureId.AutoChallenge);
                var unlocked = _data.meta?.features?.unlocked;
                return unlocked != null && unlocked.Contains(FeatureId.AutoChallenge);
            }
        }

        /// <summary>보스에 막혀 내려온 그 스테이지 (0 = 없음). 그 스테이지 이상을 클리어하면 0 (저장됨)</summary>
        public int ChallengeTarget => Math.Max(0, Save.challengeTarget);

        /// <summary>
        /// 막힌 스테이지 아래에서 힘을 모으는 중 (HUD "⟲ 1-9 반복 중" + 보스 도전 버튼).
        /// 스펙 식(ChallengeTarget &gt; 0 &amp;&amp; Stage &lt; ChallengeTarget)에 "자동 보스 도전이 열림"을 더했다: 열리기 전에는 늘 false —
        /// 그때는 클리어하면 바로 다시 도전하므로 배너 한 줄만 보여 준다.
        /// 연속 실패로 내려갈 때는 StepBack·StageEnded 동안 아직 false이고(Stage가 막힌 스테이지), 내려간 스테이지의 다음 StageChanged부터 true.
        /// </summary>
        public bool IsFarming
        {
            get
            {
                int target = Save.challengeTarget;
                return target > 0 && Stage < target && IsAutoChallengeUnlocked;
            }
        }

        /// <summary>
        /// 파밍 중 AutoChallengeEvery번째 클리어마다 스스로 보스에 다시 도전 (저장됨, 기본 켜짐, 자동 보스 도전이 열린 뒤에만 쓰인다).
        /// 끄면 ChallengeBoss()를 부를 때까지 파밍 스테이지를 반복한다 (보스 도전 버튼의 반복 토글).
        /// </summary>
        public bool AutoChallenge
        {
            get => Save.autoChallenge;
            set
            {
                if (Save.autoChallenge == value) return;
                Save.autoChallenge = value;
                _farmClears = 0;
            }
        }

        /// <summary>이번 파밍에서 연속으로 클리어한 수 (자동 보스 도전 "2/3" 표시용, 다시 도전하거나 이동하면 0)</summary>
        public int FarmClears => _farmClears;

        /// <summary>마지막으로 끝난 전투가 연속 실패로 한 단계 내려간 실패였는지 (StageEnded 처리 중 배너 문구 고르기용)</summary>
        public bool LastEndSteppedBack => _lastEndSteppedBack;

        /// <summary>
        /// 막힌 스테이지에 바로 다시 도전 (보스 도전 버튼). 막힌 스테이지가 없거나 이미 그 스테이지 이상이면 false.
        /// 붙잡기(파밍 반복)도 풀린다 — 다시 연속 실패하면 또 내려와 파밍한다.
        /// </summary>
        public bool ChallengeBoss()
        {
            int target = ChallengeTarget;
            if (target <= 0 || Stage >= target) return false;
            GoToStage(target);
            return true;
        }

        // ───────────────────────────── 4차 훅 (SPEC4 §2.5 · §13.3 — P4-0이 멤버를 두고, P4-3b가 전투에 적용) ─────────────────────────────
        // 함수 훅은 null = 없음. 훅이 예외를 내면 그 훅만 끄고(null) 전투는 그대로 이어 간다.

        /// <summary>결과 배너 대기를 늘릴 수 있는 최대 길이 (끝난 순간부터, 초 — CR-01)</summary>
        public const float MaxEndPause = 6f;

        /// <summary>
        /// 자동 불 밝히기 방식 (CR-02, BattleSave.lampPolicy에 저장). 바꾸면 진행 중인 전투에 바로 적용되고, 다시 시작할 때 BattleSetup으로 넘긴다.
        /// 잠김(FeatureId.LampPolicy, 1-6 전)이면 저장 값과 상관없이 Auto로 싸운다 (EffectiveLampPolicy)
        /// </summary>
        public LampPolicy LampPolicy
        {
            get => (LampPolicy)Mathf.Clamp(Save.lampPolicy, 0, 2);
            set
            {
                Save.lampPolicy = Mathf.Clamp((int)value, 0, 2);
                _sim.LampPolicy = EffectiveLampPolicy;
            }
        }

        /// <summary>불 밝히기 방식 고르기가 열렸는지 (1-6 클리어). FeatureResolver 우선, 없으면 세이브의 열린 기능 목록</summary>
        public bool IsLampPolicyUnlocked => IsFeatureUnlocked(FeatureId.LampPolicy);

        /// <summary>실제로 전투에 쓰는 방식 (잠겨 있으면 Auto)</summary>
        public LampPolicy EffectiveLampPolicy => IsLampPolicyUnlocked ? LampPolicy : LampPolicy.Auto;

        bool IsFeatureUnlocked(FeatureId feature)
        {
            if (FeatureResolver != null)
            {
                try
                {
                    return FeatureResolver(feature);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return false;
                }
            }
            var unlocked = _data.meta?.features?.unlocked;
            return unlocked != null && unlocked.Contains(feature);
        }

        /// <summary>
        /// 클리어 재료 굴림 대신 (스테이지, 보상 목록 — 여기에 더한다) → true면 굴렸으니 기본 재료 굴림(RollClearDrops)을 건너뛴다.
        /// 잃어버린 상자(ChapterChestSystem.TryRollLostChest)가 넣는다. null = 지금 굴림
        /// </summary>
        public Func<int, List<RewardData>, bool> ClearDropHook;

        /// <summary>스테이지 클리어 별빛 사탕 배율 (이벤트 '별이 머문 밤 산책' ×2). null = 1</summary>
        public Func<double> CandyMultiplier;

        /// <summary>잠깐 오르는 공격력 % (친구들의 응원 +20, BuildSetup에서 AttackMultiplier에 곱한다). null = 0. 바뀌면 RequestUnitRefresh</summary>
        public Func<float> TempAttackPercent;

        /// <summary>메인 스테이지 번호 → 튜토리얼 전투 보호 (MetaSession.Connect가 넣는다 — 스테이지 ≤ 3이고 튜토리얼 보호 단계). null = 꺼짐</summary>
        public Func<int, BattleProtection> ProtectionResolver;

        float _presentationScale = 1f;
        /// <summary>끝난 뒤 흐른 실제 시간 (ExtendEndPause의 '전체 길이' 기준)</summary>
        float _endElapsed;

        /// <summary>
        /// 연출 시간 배율 (FX-01: 멈칫 · 느린 화면 동안 시뮬레이션도 같이 느려지게, 0.05~1). 시뮬레이션 Tick에만 곱하고
        /// 결과 배너 대기 · 다시 시작 · 능력치 갱신에는 곱하지 않는다. BattleView가 매 프레임 BattleFx.TimeScale을 넣고 꺼질 때 1로 돌린다.
        /// 던전 · 방치 보상 · 경제 흐름 시험은 손대지 않는다 (기본 1)
        /// </summary>
        public float PresentationScale
        {
            get => _presentationScale;
            set => _presentationScale = float.IsNaN(value) ? 1f : Mathf.Clamp(value, 0.05f, 1f);
        }

        /// <summary>
        /// 결과 배너 대기를 늘린다 (끝난 동안만 — 패배 분석 고리 CR-01). totalSeconds = 끝난 순간부터의 전체 길이 (≤ MaxEndPause 6초).
        /// 이미 그보다 길게 남아 있으면 그대로 둔다. 방치 · 던전 경로는 부르지 않는다
        /// </summary>
        public void ExtendEndPause(float totalSeconds)
        {
            if (!_ended || float.IsNaN(totalSeconds)) return;
            float remaining = Mathf.Min(MaxEndPause, totalSeconds) - _endElapsed;
            if (remaining > _endTimer) _endTimer = remaining;
        }

        /// <summary>결과 배너 대기를 건너뛰고 다음 Tick에 다음 전투로 ('바로 다시 도전 ›'). 끝난 동안만</summary>
        public void SkipEndPause()
        {
            if (_ended) _endTimer = 0f;
        }

        /// <summary>
        /// 불빛 게이지를 가득 채운다 (튜토리얼 첫 불 밝히기 — FtueDirector). 메인 스테이지 · 전투 진행 중 · 게이지 &lt; 1일 때만 (BattleSimulator.FillLamp).
        /// 끝난 동안(결과 배너)에는 아무것도 하지 않는다
        /// </summary>
        public void ForceLampFull()
        {
            if (!_started || _ended || _sim.Mode != BattleMode.Stage) return;
            _sim.FillLamp();
        }

        /// <summary>이번 전투의 튜토리얼 보호 (ProtectionResolver, 개발용 부하 시험이면 그 값). 예외 = 훅을 끄고 꺼짐</summary>
        BattleProtection ResolveProtection()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_stressEnemies > 0) return new BattleProtection { Enabled = true, EnemyHpScale = _stressHpScale, ShopFloorHp = 1f };
#endif
            var resolver = ProtectionResolver;
            if (resolver == null) return BattleProtection.Off;
            try
            {
                return resolver(Stage);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ProtectionResolver = null;
                return BattleProtection.Off;
            }
        }

        /// <summary>BuildSetup의 잠깐 공격력 % (응원). 0 미만은 0, 예외 = 훅을 끄고 0</summary>
        float ResolveTempAttackPercent()
        {
            var hook = TempAttackPercent;
            if (hook == null) return 0f;
            try
            {
                float value = hook();
                return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, 1000f);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                TempAttackPercent = null;
                return 0f;
            }
        }

        /// <summary>진행 중 보호가 바뀌었으면 시뮬레이터에 넣는다 (튜토리얼 단계가 전투 도중 바뀌어도 수선점 바닥이 바로 따른다, 할당 없음)</summary>
        void SyncProtection()
        {
            var want = ResolveProtection();
            var have = _sim.Protection;
            if (want.Enabled != have.Enabled || want.EnemyHpScale != have.EnemyHpScale || want.ShopFloorHp != have.ShopFloorHp) _sim.Protection = want;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int _stressEnemies;
        float _stressHpScale = 1f;

        /// <summary>개발용 부하 시험 중인지 (DebugStress)</summary>
        public bool IsDebugStress => _stressEnemies > 0;

        /// <summary>
        /// 개발용 부하 시험 (SPEC4 §3.1 — P4-9 PerfProbe): 계보마다 하나씩 성향을 섞은 지킴이 5 + 잔향이 5 덱의 개발 편성, 배속 2 고정,
        /// 적 체력 × hpScale · 수선점 최소 1 · 동시 적 상한 enemies로 적이 계속 살아 있게 한다. 세이브의 편성 · 배속은 건드리지 않는다.
        /// enemies ≤ 0 = 끄고 원래 편성으로 다시 시작. 릴리스 빌드에는 없다
        /// </summary>
        public void DebugStress(int enemies = 20, float hpScale = 40f)
        {
            _stressEnemies = Math.Max(0, enemies);
            _stressHpScale = hpScale > 0f && !float.IsInfinity(hpScale) ? hpScale : 1f;
            _ended = false;
            StartBattle();
        }

        /// <summary>개발 편성: 계보 순서대로 지킴이 5칸 (형태를 돌려 가며 성향을 섞는다) + 이어지는 계보로 덱 5칸</summary>
        void ApplyStress(BattleSetup setup)
        {
            setup.MaxEnemiesOverride = _stressEnemies;
            setup.Protection = ResolveProtection();
            var lineages = _characters?.Db != null ? _characters.Db.lineages : null;
            if (lineages == null || lineages.Count == 0) return;
            int n = lineages.Count;
            for (int slot = 0; slot < setup.Guardians.Length; slot++)
                setup.Guardians[slot] = StressUnit(lineages[slot % n], slot);
            setup.Deck.Clear();
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++)
                setup.Deck.Add(StressUnit(lineages[(setup.Guardians.Length + i) % n], i + 1));
        }

        UnitSetup StressUnit(LineageDef lineage, int formIndex)
        {
            if (lineage == null || lineage.forms == null || lineage.forms.Count == 0) return null;
            var form = lineage.forms[formIndex % lineage.forms.Count] ?? lineage.forms[0];
            if (form == null || string.IsNullOrEmpty(form.id)) return null;
            var unit = new UnitSetup
            {
                FormId = form.id,
                LineageId = lineage.id,
                Stats = _characters.GetStats(form.id),
                Skill = form.skill != null && !form.skill.IsEmpty ? form.skill : null,
            };
            if (lineage.traits != null) unit.Traits.AddRange(lineage.traits);
            return unit;
        }
#endif

        /// <summary>진행 중인 전투의 유닛 능력치를 다음 Tick에 다시 계산한다 (응원 시작 · 끝 등 — 편성은 그대로)</summary>
        public void RequestUnitRefresh() => _refreshPending = true;

        // ───────────────────────────── 진행 ─────────────────────────────

        public void Tick(float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            EnsureStarted();

            if (_restartPending)
            {
                _restartPending = false;
                _refreshPending = false;
                Restart();
            }
            else if (_refreshPending)
            {
                _refreshPending = false;
                if (!_ended && !_sim.RefreshUnits(BuildSetup())) StartBattle();
            }

            if (_data.level != _shopLevel)
            {
                _shopLevel = _data.level;
                _sim.SetShopMaxHp(GrowthSystem.CalcShopMaxHp(_shopLevel));
            }

            if (_ended)
            {
                // 결과 배너는 배속 · 연출 배율과 상관없이 실제 시간으로 보여 준다
                _endElapsed += dt;
                _endTimer -= dt;
                if (_endTimer <= 0f) BeginPendingStage();
                return;
            }

            SyncProtection();
            int speed = Speed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_stressEnemies > 0) speed = MaxSpeed;
#endif
            // 연출 배율(멈칫 · 느린 화면)은 시뮬레이션에만 (FX-01)
            _sim.Tick(dt * speed * _presentationScale);

            if (_sim.Outcome != BattleOutcome.Running) HandleEnd();
        }

        /// <summary>잔향이 소환 + 출격 횟수 기록. angle null = 가장 위험한 방향</summary>
        public bool TrySummon(int deckSlot, float? angle = null)
        {
            EnsureStarted();
            return !_ended && _sim.TrySummon(deckSlot, angle);
        }

        public bool TryLightLamp()
        {
            EnsureStarted();
            return !_ended && _sim.TryLightLamp();
        }

        /// <summary>현재 편성으로 현재 스테이지를 다시 시작 (편성 변경 시)</summary>
        public void Restart()
        {
            _restartPending = false;
            if (_ended) BeginPendingStage();
            else StartBattle();
        }

        /// <summary>최고 스테이지 아래 스테이지로 이동해 반복 (파밍). 막힌 스테이지(ChallengeTarget)는 그대로 둔다</summary>
        public void GoToStage(int stage)
        {
            Save.stage = Mathf.Clamp(stage, 1, HighestStage);
            _ended = false;
            _consecutiveFails = 0;
            _farmClears = 0;
            StartBattle();
        }

        void EnsureStarted()
        {
            if (!_started) StartBattle();
        }

        void BeginPendingStage()
        {
            _ended = false;
            Save.stage = Math.Max(1, _pendingStage);
            StartBattle();
        }

        static readonly Unity.Profiling.ProfilerMarker s_stageStartMarker = new("LL.StageStart");
        static readonly Unity.Profiling.ProfilerMarker s_stageEndMarker = new("LL.StageEnd");

        /// <summary>새 전투 시작 (perf PF-5 L3: LL.StageStart 마커 — StageChanged를 받는 뷰 · 배너까지 포함해 잰다)</summary>
        void StartBattle()
        {
            using (s_stageStartMarker.Auto()) StartBattleCore();
        }

        void StartBattleCore()
        {
            _started = true;
            _ended = false;
            _restartPending = false;
            _refreshPending = false;
            _shopLevel = _data.level;
            var save = Save;
            if (save.stage < 1) save.stage = 1;
            if (save.highestStage < save.stage) save.highestStage = save.stage;

            _sim.AutoMode = save.autoMode;
            _sim.Start(BuildSetup(), _random.Next());
            StageChanged?.Invoke();
        }

        /// <summary>현재 편성/캐릭터 능력치/수선점 레벨/시너지/스테이지 기믹으로 전투 구성을 만든다</summary>
        BattleSetup BuildSetup()
        {
            var stageModifier = GetStageModifier(Stage);
            _modifiers = _synergy != null ? _synergy.BuildModifiers(stageModifier) ?? new BattleModifiers() : new BattleModifiers();
            var setup = new BattleSetup
            {
                Mode = BattleMode.Stage,
                Level = Stage,
                ShopMaxHp = GrowthSystem.CalcShopMaxHp(_data.level),
                // 팀 공격력 보너스 × 잠깐 오르는 공격력 (친구들의 응원 — 전투력 표시에는 넣지 않는다)
                AttackMultiplier = (1f + Mathf.Max(0f, _formation.TeamAttackBonusPercent) / 100f) * (1f + ResolveTempAttackPercent() / 100f),
                Modifiers = _modifiers,
                StageModifier = stageModifier,
                LampPolicy = EffectiveLampPolicy,
                Protection = ResolveProtection(),
            };
            for (int slot = 0; slot < BattleGeometry.SlotCount && slot < FormationSystem.GuardianSlotCount; slot++)
                setup.Guardians[slot] = MakeUnit(_characters, _formation.GetGuardian(slot));
            for (int slot = 0; slot < FormationSystem.EchoSlotCount; slot++)
                setup.Deck.Add(MakeUnit(_characters, _formation.GetEcho(slot)));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_stressEnemies > 0) ApplyStress(setup);
#endif
            return setup;
        }

        /// <summary>보유·해금된 형태의 전투 유닛 구성 (능력치 + 고유 스킬 + 계보 특성). 아니면 null. 던전도 같이 쓴다</summary>
        internal static UnitSetup MakeUnit(CharacterSystem characters, string formId)
        {
            if (string.IsNullOrEmpty(formId) || characters == null) return null;
            var db = characters.Db;
            var lineage = db != null ? db.GetLineageOfForm(formId) : null;
            if (lineage == null || !characters.IsOwned(lineage.id) || !characters.IsFormUnlocked(formId)) return null;
            var form = db.GetForm(formId);
            var unit = new UnitSetup
            {
                FormId = formId,
                LineageId = lineage.id,
                Stats = characters.GetStats(formId),
                Skill = form?.skill != null && !form.skill.IsEmpty ? form.skill : null,
            };
            if (lineage.traits != null) unit.Traits.AddRange(lineage.traits);
            return unit;
        }

        // ───────────────────────────── 스테이지 기믹 ─────────────────────────────

        /// <summary>스테이지 번호의 기믹 (1-1~1-3 없음, 이후 규칙적으로 순환, 챕터 10번째는 축제). BattleConfig.GetStageModifier</summary>
        public StageModifierType GetStageModifier(int stage) => _config.GetStageModifier(stage);

        /// <summary>현재 스테이지의 기믹</summary>
        public StageModifierType CurrentStageModifier => GetStageModifier(Stage);

        /// <summary>
        /// 이 스테이지 단추 보상 증가(%) = 편성 시너지 GoldGainPercent + 축제의 밤 보너스.
        /// 시너지 값은 마지막으로 전투를 만들 때의 편성 기준이다.
        /// </summary>
        public float GetGoldBonusPercent(int stage)
        {
            float percent = BattleSimulator.SumGlobal(_modifiers, StatType.GoldGainPercent);
            if (GetStageModifier(stage) == StageModifierType.Festival) percent += _config.festivalGoldPercent;
            return Mathf.Max(-90f, percent);
        }

        // ───────────────────────────── 결과 ─────────────────────────────

        /// <summary>전투 끝 (perf PF-5 L3: LL.StageEnd 마커 — StageEnded를 받는 메타 · 보상 · 도감까지 포함해 잰다)</summary>
        void HandleEnd()
        {
            using (s_stageEndMarker.Auto()) HandleEndCore();
        }

        void HandleEndCore()
        {
            var outcome = _sim.Outcome;
            int stage = Math.Max(1, _sim.Level);
            _ended = true;
            _endTimer = Mathf.Max(0f, _config.stageEndPause);
            _endElapsed = 0f;
            _lastOutcome = outcome;

            var save = Save;
            List<RewardData> rewards;
            bool steppedBack = false;
            if (outcome == BattleOutcome.Cleared)
            {
                rewards = GetClearRewards(stage);
                // 잃어버린 상자(ClearDropHook)가 굴렸으면 기본 재료 굴림은 건너뛴다 (FTUE 전 · 확률 0이면 훅이 false → 지금 굴림)
                if (!RollClearDropHook(stage, rewards)) RollClearDrops(rewards);
                _rewards.Grant(rewards);
                GiveClearAffinity();
                _consecutiveFails = 0;
                save.highestStage = Math.Max(save.highestStage, stage + 1);
                if (save.challengeTarget > 0 && stage >= save.challengeTarget)
                {
                    // 막혔던 보스를 넘었다
                    save.challengeTarget = 0;
                    _farmClears = 0;
                }
                _pendingStage = NextStageAfterClear(stage);
            }
            else
            {
                rewards = GetFailRewards(stage);
                _rewards.Grant(rewards);
                _consecutiveFails++;
                if (_consecutiveFails >= FailsBeforeStepBack)
                {
                    _consecutiveFails = 0;
                    _pendingStage = Math.Max(1, stage - 1);
                    if (stage > 1)
                    {
                        save.challengeTarget = stage;
                        _farmClears = 0;
                        steppedBack = true;
                    }
                }
                else
                {
                    _pendingStage = stage;
                }
            }
            _lastEndSteppedBack = steppedBack;
            if (steppedBack) StepBack?.Invoke(stage, _pendingStage);
            StageEnded?.Invoke(outcome, rewards);
        }

        /// <summary>
        /// 클리어 뒤 다음 스테이지. 자동 진행이 꺼져 있으면 같은 스테이지.
        /// 막힌 스테이지 아래(파밍)에서 자동 보스 도전이 열려 있으면: 붙잡아 같은 스테이지를 반복하고,
        /// AutoChallenge가 켜져 있으면 AutoChallengeEvery번째 클리어마다 막힌 스테이지로 간다.
        /// 막힌 곳이 없거나 아직 열리기 전이면 지금처럼 다음 스테이지 (클리어하면 바로 다시 도전).
        /// </summary>
        int NextStageAfterClear(int stage)
        {
            var save = Save;
            if (!save.autoAdvance) return stage;
            int target = save.challengeTarget;
            if (target <= 0 || stage >= target || !IsAutoChallengeUnlocked)
            {
                _farmClears = 0;
                return stage + 1;
            }
            if (save.autoChallenge && ++_farmClears >= AutoChallengeEvery)
            {
                _farmClears = 0;
                return Math.Min(target, HighestStage);
            }
            return stage;
        }

        /// <summary>클리어 확정 보상 (단추 + 별빛 사탕). 단추는 시너지/축제 보너스 반영. 재료는 클리어할 때 확률로 추가된다</summary>
        public List<RewardData> GetClearRewards(int stage)
        {
            stage = Math.Max(1, stage);
            var rewards = new List<RewardData>
            {
                RewardData.Currency(CurrencyType.Gold, Math.Floor(StageGold(stage) * (1.0 + GetGoldBonusPercent(stage) / 100.0))),
            };
            int interval = _config.stageCandyMilestoneInterval;
            int candy = interval > 0 && stage % interval == 0 ? _config.stageCandyMilestone : _config.stageCandy;
            if (candy > 0)
            {
                // 이벤트 배율 ('별이 머문 밤 산책' ×2 — EventCalendarSystem.Connect가 넣는다, P4-2 R4). 1 이하 · 무한 · NaN이면 그대로
                double multiplier = ReadCandyMultiplier();
                if (multiplier > 1.0 && !double.IsInfinity(multiplier)) candy = (int)Math.Min(int.MaxValue, Math.Floor(candy * multiplier + 1e-9));
                rewards.Add(RewardData.Currency(CurrencyType.EventToken, candy));
            }
            return rewards;
        }

        /// <summary>CandyMultiplier 훅 (null = 1). 훅이 예외를 내면 한 번 기록하고 떼어 낸다 (킬 스위치에 닿지 않게)</summary>
        double ReadCandyMultiplier()
        {
            var hook = CandyMultiplier;
            if (hook == null) return 1.0;
            try
            {
                return hook();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CandyMultiplier = null;
                return 1.0;
            }
        }

        double StageGold(int stage) =>
            Math.Floor(Math.Max(0, _config.stageGoldBase) * Math.Pow(Math.Max(1, _config.stageGoldGrowth), Math.Max(1, stage) - 1));

        bool RollClearDropHook(int stage, List<RewardData> rewards)
        {
            var hook = ClearDropHook;
            if (hook == null) return false;
            try
            {
                return hook(stage, rewards);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ClearDropHook = null;
                return false;
            }
        }

        void RollClearDrops(List<RewardData> rewards)
        {
            if (_random.NextDouble() < _config.stageClothChance) rewards.Add(RewardData.Item("mat_cloth", _random.Next(1, 3)));
            if (_random.NextDouble() < _config.stageRibbonChance) rewards.Add(RewardData.Item("mat_ribbon", 1));
        }

        /// <summary>실패해도 처치한 만큼 단추 일부를 받는다</summary>
        List<RewardData> GetFailRewards(int stage)
        {
            var rewards = new List<RewardData>();
            float progress = _sim.KillsToBoss > 0 ? Mathf.Clamp01((float)_sim.Kills / _sim.KillsToBoss) : 0f;
            double gold = Math.Floor(StageGold(stage) * (1.0 + GetGoldBonusPercent(stage) / 100.0) * _config.failGoldRatio * progress);
            if (gold >= 1) rewards.Add(RewardData.Currency(CurrencyType.Gold, gold));
            return rewards;
        }

        void GiveClearAffinity()
        {
            if (_config.clearAffinity <= 0) return;
            _lineageScratch.Clear();
            foreach (var g in _sim.Guardians) if (g != null && !string.IsNullOrEmpty(g.LineageId)) _lineageScratch.Add(g.LineageId);
            foreach (var d in _sim.Deck) if (d != null && !string.IsNullOrEmpty(d.LineageId)) _lineageScratch.Add(d.LineageId);
            foreach (var lineageId in _lineageScratch)
            {
                if (_characters.IsOwned(lineageId)) _characters.AddAffinity(lineageId, _config.clearAffinity);
            }
        }

        // ───────────────────────────── 이벤트 / 기록 ─────────────────────────────

        void OnSimEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.EnemyKilled:
                    Save.totalKills++;
                    if (e.SourceId >= 0 && e.SourceId < _sim.Guardians.Count)
                    {
                        var lineageId = _sim.Guardians[e.SourceId]?.LineageId;
                        if (!string.IsNullOrEmpty(lineageId) && _characters.IsOwned(lineageId))
                            _characters.AddProgress(lineageId, ProgressType.GuardianKillCount, 1);
                    }
                    break;
                case BattleEventType.EchoSummoned:
                    int slot = (int)e.Value;
                    var deck = _sim.Deck;
                    var deckLineage = slot >= 0 && slot < deck.Count ? deck[slot]?.LineageId : null;
                    if (!string.IsNullOrEmpty(deckLineage) && _characters.IsOwned(deckLineage))
                        _characters.AddProgress(deckLineage, ProgressType.EchoDeployCount, 1);
                    break;
            }
            Event?.Invoke(e);
        }

        void OnFormationChanged() => _restartPending = true;

        void OnCharactersChanged() => _refreshPending = true;
    }
}
