using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 수선점 방어 전장 좌표계. 수선점이 원점, 지킴이 방위 링 반지름이 1인 2D 평면.
    /// 방위 슬롯 0은 위쪽이고 반시계 방향으로 72°씩 돈다. 각도는 라디안.
    /// 화면에 그릴 때는 뷰가 y를 눌러 타원(원근)으로 표현한다.
    /// </summary>
    public static class BattleGeometry
    {
        public const int SlotCount = 5;
        public const float RingRadius = 1f;
        /// <summary>적이 이 반지름 안에 들어오면 수선점에 닿은 것</summary>
        public const float ShopRadius = 0.32f;
        public const float SpawnRadius = 2.5f;

        public static float SlotAngle(int slot) => Mathf.PI * 0.5f + slot * (Mathf.PI * 2f / SlotCount);

        public static Vector2 FromAngle(float angle, float radius) => new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

        public static Vector2 GuardianPosition(int slot) => FromAngle(SlotAngle(slot), RingRadius);

        /// <summary>각도에 가장 가까운 방위 슬롯</summary>
        public static int NearestSlot(float angle)
        {
            float step = Mathf.PI * 2f / SlotCount;
            float rel = Mathf.Repeat(angle - Mathf.PI * 0.5f + step * 0.5f, Mathf.PI * 2f);
            return Mathf.Clamp((int)(rel / step), 0, SlotCount - 1);
        }

        /// <summary>두 각도의 차이 (-π ~ π)</summary>
        public static float DeltaAngle(float a, float b) => Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg) * Mathf.Deg2Rad;
    }

    public sealed class UnitSetup
    {
        public string FormId;
        public string LineageId;
        public CharacterStats Stats;
        /// <summary>형태 고유 스킬 (CharacterFormDef.skill, 없으면 null)</summary>
        public SkillDef Skill;
        /// <summary>계보 특성 (LineageDef.traits)</summary>
        public List<TraitType> Traits = new();
    }

    public sealed class BattleSetup
    {
        public BattleMode Mode;
        /// <summary>스테이지 번호 또는 던전 층</summary>
        public int Level = 1;
        /// <summary>방위 슬롯별 지킴이 (빈칸 null). 길이 = BattleGeometry.SlotCount</summary>
        public UnitSetup[] Guardians = new UnitSetup[BattleGeometry.SlotCount];
        /// <summary>잔향이 덱 (인덱스 = 덱 슬롯, 빈칸 null)</summary>
        public List<UnitSetup> Deck = new();
        public float ShopMaxHp = 100f;
        /// <summary>팀 시너지 등 전체 공격력 배율</summary>
        public float AttackMultiplier = 1f;
        /// <summary>특성/성향/조합 시너지 능력치 (SynergySystem.BuildModifiers)</summary>
        public BattleModifiers Modifiers = new();
        /// <summary>이번 전투의 스테이지 기믹 (BattleSystem이 스테이지 번호로 정한다)</summary>
        public StageModifierType StageModifier;
        /// <summary>자동 불 밝히기 방식 (CR-02, BattleSimulator.ShouldAutoLamp가 읽는다 — 진행 중에도 BattleSimulator.LampPolicy로 바뀐다). 기본 Auto = 지금 규칙</summary>
        public LampPolicy LampPolicy = LampPolicy.Auto;
        /// <summary>튜토리얼 패배 불가 보호 (FTUE-01 — BattleSystem.ProtectionResolver, 메인 스테이지에서만 시뮬레이터가 적용). 기본 꺼짐</summary>
        public BattleProtection Protection = BattleProtection.Off;
        /// <summary>동시에 살아 있는 적 상한 (0 = BattleConfig.maxEnemies). 개발용 부하 시험(BattleSystem.DebugStress)만 쓴다</summary>
        public int MaxEnemiesOverride;
    }

    /// <summary>
    /// 튜토리얼 전투 보호 (FTUE-01, SPEC4 §13.3): 켜지면 적 최대 체력 × EnemyHpScale (보스 · 분열체 포함), 수선점 내구도는 ShopFloorHp 아래로 내려가지 않는다.
    /// 메인 스테이지에만 (던전은 보지 않는다). 값 타입이라 만들 때 할당이 없다
    /// </summary>
    public struct BattleProtection
    {
        public bool Enabled;
        /// <summary>적 최대 체력 배율 (튜토리얼 0.6)</summary>
        public float EnemyHpScale;
        /// <summary>수선점 내구도 바닥 (튜토리얼 1)</summary>
        public float ShopFloorHp;

        /// <summary>꺼짐 (지금)</summary>
        public static BattleProtection Off => new() { Enabled = false, EnemyHpScale = 1f, ShopFloorHp = 0f };

        /// <summary>튜토리얼 보호 (적 체력 60%, 수선점 최소 1 — 지금 FtueBattleGuard와 같은 값)</summary>
        public static BattleProtection Tutorial => new() { Enabled = true, EnemyHpScale = 0.6f, ShopFloorHp = 1f };
    }

    /// <summary>StatType별 수치 묶음 (값은 StatType 주석의 단위)</summary>
    public sealed class StatBlock
    {
        static readonly int Count = System.Enum.GetValues(typeof(StatType)).Length;
        readonly float[] _values = new float[Count];

        public float this[StatType stat]
        {
            get => _values[(int)stat];
            set => _values[(int)stat] = value;
        }

        public void Add(StatType stat, float value) => _values[(int)stat] += value;

        public void Add(StatBlock other)
        {
            if (other == null) return;
            for (int i = 0; i < Count; i++) _values[i] += other._values[i];
        }

        public void Clear() => System.Array.Clear(_values, 0, Count);

        public bool IsZero
        {
            get
            {
                for (int i = 0; i < Count; i++) if (_values[i] != 0f) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// 전투에 들어가는 시너지 능력치. Team은 모두에게, Lineage[계보 ID]는 그 계보 유닛(지킴이·잔향이 모두)에게,
    /// Form[형태 ID]는 그 형태 유닛에게만 더해진다. 유닛 하나가 받는 값 = For(계보 ID, 형태 ID).
    /// 구성원 전용 단계: 특성/조합 시너지는 계보 단위(Lineage / BonusSkills[계보 ID]) — 계보의 형태가 모두 구성원이다.
    /// 역할 시너지는 형태 단위(Form / FormBonusSkills[형태 ID]) — 같은 계보라도 성향이 다른 반대편 형태는 받지 않는다.
    /// </summary>
    public sealed class BattleModifiers
    {
        public readonly StatBlock Team = new();
        public readonly Dictionary<string, StatBlock> Lineage = new();
        /// <summary>형태 전용 능력치 (역할 시너지의 구성원 전용 단계). 칸을 만들 때 GetOrCreateForm을 쓴다</summary>
        public readonly Dictionary<string, StatBlock> Form = new();
        public readonly Dictionary<string, List<SkillDef>> BonusSkills = new();
        /// <summary>형태 전용 보너스 스킬 (역할 시너지의 구성원 전용 bonusSkill)</summary>
        public readonly Dictionary<string, List<SkillDef>> FormBonusSkills = new();
        /// <summary>Form 칸 형태의 계보 ID (GetOrCreateForm이 기록. 수선점·잔향 같은 전체 능력치를 유닛 단위로 합칠 때 쓴다)</summary>
        public readonly Dictionary<string, string> FormLineage = new();
        /// <summary>켜진 시너지 ID와 단계 (전투 화면 시작 연출용)</summary>
        public readonly List<KeyValuePair<string, int>> ActiveSynergies = new();

        /// <summary>계보 전용 + 팀 합계 (값 복사본). 형태 전용 칸(역할 시너지)은 빠진다 — 유닛 하나의 값은 For(계보, 형태)</summary>
        public StatBlock For(string lineageId) => For(lineageId, null);

        /// <summary>팀 + 계보 전용 + 형태 전용 합계 (값 복사본). formId가 null이면 For(계보)와 같다</summary>
        public StatBlock For(string lineageId, string formId)
        {
            var total = new StatBlock();
            FillFor(total, lineageId, formId);
            return total;
        }

        /// <summary>For(계보, 형태)와 같은 합계를 target에 채운다 (할당 없음, target은 먼저 비운다)</summary>
        public void FillFor(StatBlock target, string lineageId, string formId)
        {
            if (target == null) return;
            target.Clear();
            target.Add(Team);
            if (lineageId != null && Lineage.TryGetValue(lineageId, out var own)) target.Add(own);
            if (formId != null && Form.TryGetValue(formId, out var form)) target.Add(form);
        }

        public StatBlock GetOrCreate(string lineageId)
        {
            if (!Lineage.TryGetValue(lineageId, out var block)) Lineage[lineageId] = block = new StatBlock();
            return block;
        }

        /// <summary>형태 전용 칸. lineageId(그 형태의 계보, 없으면 null)는 FormLineage에 기록된다</summary>
        public StatBlock GetOrCreateForm(string formId, string lineageId = null)
        {
            if (!Form.TryGetValue(formId, out var block)) Form[formId] = block = new StatBlock();
            if (!string.IsNullOrEmpty(lineageId)) FormLineage[formId] = lineageId;
            return block;
        }

        /// <summary>형태 전용 보너스 스킬을 더한다 (같은 스킬은 한 번만)</summary>
        public void AddFormBonusSkill(string formId, SkillDef skill)
        {
            if (string.IsNullOrEmpty(formId) || skill == null) return;
            if (!FormBonusSkills.TryGetValue(formId, out var skills)) FormBonusSkills[formId] = skills = new List<SkillDef>();
            if (!skills.Contains(skill)) skills.Add(skill);
        }
    }

    public sealed class GuardianState
    {
        public int Slot;
        public string FormId;
        public string LineageId;
        public CharacterStats Stats;
        public float Cooldown;
        /// <summary>공격 직후 1 → 0으로 줄어드는 연출 값</summary>
        public float AttackAnim;
        public int Kills;
        /// <summary>고유 스킬 남은 쿨타임 / 전체 (Cooldown 트리거가 아니면 0)</summary>
        public float SkillCooldown;
        public float SkillCooldownMax;
        /// <summary>강화(공격력/공속 버프) 남은 시간 — 연출용</summary>
        public float BuffTimer;
        /// <summary>녹 등 약화 남은 시간 — 연출용</summary>
        public float DebuffTimer;
    }

    public sealed class DeckSlotState
    {
        public int Slot;
        public string FormId;
        public string LineageId;
        public CharacterStats Stats;
        public int Cost;
        public float Cooldown;
        public float CooldownMax;
    }

    public sealed class EchoState
    {
        public int Id;
        public int DeckSlot;
        public string FormId;
        public string LineageId;
        public CharacterStats Stats;
        public Vector2 Position;
        /// <summary>
        /// 마지막 고정 진행(Step) 전의 자리 — 뷰 보간 전용 (그릴 자리 = Vector2.LerpUnclamped(PrevPosition, Position, BattleSimulator.RenderAlpha)).
        /// 진행을 시작할 때마다 Position을 옮겨 적고, 등장할 때는 Position과 같다. 밸런스 · 판정은 읽지 않는다 (perf PF-2 F1)
        /// </summary>
        public Vector2 PrevPosition;
        /// <summary>진행 방향 (라디안)</summary>
        public float Heading;
        public float Hp;
        public float MaxHp;
        /// <summary>남은 유지 시간</summary>
        public float Life;
        public float MaxLife;
        public bool Alive;
        public int TargetEnemyId = -1;
        public float AttackAnim;
        /// <summary>보호막 남은 양</summary>
        public float Shield;
        public float BuffTimer;
        public float DebuffTimer;
        /// <summary>스킬로 불려 나온 작은 아군 (덱 슬롯 없음, DeckSlot = -1)</summary>
        public bool IsToken;
        public float SkillCooldown;
        public float SkillCooldownMax;
    }

    public sealed class EnemyState
    {
        public int Id;
        public EnemyDef Def;
        public Vector2 Position;
        /// <summary>마지막 고정 진행 전의 자리 — 뷰 보간 전용 (EchoState.PrevPosition과 같은 규칙, perf PF-2 F1)</summary>
        public Vector2 PrevPosition;
        public float Hp;
        public float MaxHp;
        public bool IsBoss;
        public bool Alive;
        public float SlowTimer;
        public float StunTimer;
        public float HitAnim;
        /// <summary>보호막 남은 양 / 최대 (Shielded 기믹)</summary>
        public float Shield;
        public float MaxShield;
        /// <summary>표식(받는 피해 증가) 남은 시간</summary>
        public float MarkTimer;
        /// <summary>지속 피해 남은 시간</summary>
        public float BurnTimer;
        /// <summary>Phasing 적이 지금 드러나 있는지 (아니면 반투명으로 그리고 공격 불가)</summary>
        public bool Revealed = true;
        /// <summary>분노 상태 (Enrage 기믹)</summary>
        public bool Enraged;
        /// <summary>분열로 생긴 작은 적</summary>
        public bool IsSplit;
        /// <summary>공격 직후 1 → 0으로 줄어드는 연출 값 (EnemyStrike를 낼 때 1, 초당 4씩 줄어듦)</summary>
        public float AttackAnim;
        /// <summary>마지막 고정 진행 전의 AttackAnim — 뷰 보간 전용 (내밀기 자세가 60 Hz 계단으로 보이지 않게, perf PF-2 F4)</summary>
        public float PrevAttackAnim;
        /// <summary>
        /// 수선점에 붙은 보스의 다음 공격 예비 동작 0 → 1 (공격 BattleConfig.bossSiegeWindupSeconds 전부터 차올라 공격 순간 1, 공격하면 0).
        /// 수선점에 붙어 있지 않거나 기절 중이면 0. 몸 젖히기 · 문 앞 텔레그래프 원 같은 연출용 (밸런스 영향 없음)
        /// </summary>
        public float SiegeWindup;
    }

    /// <summary>BattleEvent.Key로 쓰는 고정 문자열 (스킬/시너지/연출 키가 아닌 것)</summary>
    public static class BattleEventKeys
    {
        /// <summary>GuardianPulse: 지원 성향 맥박 (수선점 회복 + 양옆 지킴이·가까운 잔향이 공격력 강화)</summary>
        public const string PulseSupport = "support";
        /// <summary>GuardianPulse: 힐 성향 맥박 (수선점 회복 + 가까운 잔향이 회복)</summary>
        public const string PulseHeal = "heal";
    }

    public enum BattleOutcome
    {
        Running = 0,
        Cleared = 1,
        Failed = 2,
    }

    public enum BattleEventType
    {
        EnemySpawned,
        EnemyHit,
        EnemyKilled,
        BossSpawned,
        /// <summary>적이 수선점 내구도를 깎음. SourceId = 적 ID, Value = 피해량 (보호막에 다 막히면 없음 — 공격 자체는 EnemyStrike가 먼저 온다)</summary>
        ShopDamaged,
        /// <summary>지원형 회복 (Value = 회복량)</summary>
        ShopHealed,
        /// <summary>SourceId = 지킴이 슬롯, TargetId = 적 ID</summary>
        GuardianAttack,
        /// <summary>SourceId = 잔향이 ID, TargetId = 적 ID</summary>
        EchoAttack,
        /// <summary>SourceId = 잔향이 ID, Value = 덱 슬롯</summary>
        EchoSummoned,
        /// <summary>잔향이가 빛으로 풀려 수선점으로 흘러 들어감. Position = 사라진 위치, Value = 돌려받은 잔향</summary>
        EchoDissolved,
        /// <summary>수선점 불 밝히기 발동</summary>
        LampLit,
        StageCleared,
        StageFailed,

        // ── 스킬 / 시너지 / 기믹 (Key에 문자열 정보) ──
        /// <summary>
        /// 스킬 시전. SourceId = 지킴이 슬롯(0~4) 또는 잔향이 ID(>= 1000), TargetId = 주 대상 적 ID(-1 가능),
        /// Position = 효과 중심, Value = 반지름, Key = 스킬 ID. 연출 키는 sim.GetSkill(Key).vfx
        /// </summary>
        SkillCast,
        /// <summary>보호막 생김. TargetId = 잔향이 ID 또는 -1(수선점), Value = 양, Key = 연출 키</summary>
        ShieldGained,
        /// <summary>보호막 깨짐. TargetId = 잔향이/적 ID 또는 -1(수선점)</summary>
        ShieldBroken,
        /// <summary>잔향이 회복. TargetId = 잔향이 ID, Value = 회복량</summary>
        EchoHealed,
        /// <summary>
        /// 상태 부여. TargetId = 적 ID(또는 잔향이/지킴이: Key 접두 "echo:"/"guardian:"),
        /// Value = (int)SkillEffectType, Key = 연출 키
        /// </summary>
        StatusApplied,
        /// <summary>적 분열. SourceId = 원래 적 ID, TargetId = 새 적 ID</summary>
        EnemySplit,
        /// <summary>보이지 않던 적이 드러남. TargetId = 적 ID</summary>
        EnemyRevealed,
        /// <summary>보스/적 분노. TargetId = 적 ID</summary>
        EnemyEnraged,
        /// <summary>토큰 아군 소환. SourceId = 시전자, TargetId = 새 잔향이 ID</summary>
        TokenSummoned,
        /// <summary>전투 시작 시 시너지 켜짐. Key = 시너지 ID, Value = 단계 인덱스</summary>
        SynergyActivated,
        /// <summary>전투 시작 시 스테이지 기믹. Value = (int)StageModifierType</summary>
        StageModifierStarted,
        /// <summary>치명타. TargetId = 적 ID, Value = 피해</summary>
        CriticalHit,
        /// <summary>불빛 게이지를 빼앗김 (LampDrain). Value = 빼앗긴 양</summary>
        LampDrained,

        // ── 공격 연출 (피해/회복은 위 이벤트와 상태로 따로 들어가고, 이 이벤트는 "언제 휘둘렀나"만 알려 준다) ──
        /// <summary>
        /// 적의 공격. SourceId = 적 ID, TargetId = 잔향이 ID 또는 -1(수선점), Position = 맞은 자리,
        /// Value = 이 공격에 묶인 피해 (보호막 흡수분 포함), Key = 적 정의 ID (EnemyDef.id, 예: "enemy_moth").
        /// <para>
        /// 잔향이: 발이 묶여 싸우는 동안 피해는 매 진행 조금씩 들어가므로 적 하나당 그 적의 공격 리듬 EnemyDef.strikeInterval
        /// (ATK-03 적 표: 좀나방 0.6초 ~ 녹얼룩 1.4초, 보스 2초 — 비어 있으면 BattleConfig.enemyStrikeInterval)에 한 번만 묶어서 낸다
        /// — 붙자마자(지난 공격에서 간격이 지났으면) 한 번, 그 뒤로 붙어 있는 동안 간격마다.
        /// Value = 그 잔향이에게 지난 이벤트 뒤로 준 피해 합계 (붙자마자 내는 첫 이벤트는 닿은 순간 한 진행 분량이라 작다).
        /// 기절한 적·잔향이 피해가 0인 적은 내지 않는다. 대상이 바뀌어도 간격은 적 하나 기준이다. Position = 적과 잔향이의 가운데.
        /// </para>
        /// <para>
        /// 수선점: 일반 적이 닿을 때(그 적은 이때 사라진다 — Alive = false, EnemyKilled 없음) / 수선점에 붙은 보스의 공격마다 한 번
        /// (보스는 붙자마자 한 대, 그 뒤 bossSiegeInterval마다 — 그 전 예비 동작은 EnemyState.SiegeWindup).
        /// Value = 피해 감소 반영 후 피해 — 보호막에 다 막혀 ShopDamaged가 없어도 나온다. Position = 적이 들어온 방향의 수선점 가장자리.
        /// 같은 공격의 ShieldBroken · LampDrained · ShopDamaged보다 먼저 온다.
        /// </para>
        /// 낼 때마다 그 적의 EnemyState.AttackAnim = 1. 피해 계산(밸런스)은 이 이벤트와 상관없다.
        /// </summary>
        EnemyStrike,
        /// <summary>
        /// 지원/힐 성향 지킴이의 맥박 (지원 supportInterval, 힐 healInterval마다. 첫 맥박은 전투 시작 0.5초 뒤).
        /// 수선점이 가득 차 회복량이 0이어도 나온다. SourceId = 지킴이 슬롯, TargetId = -1, Position = 지킴이 자리,
        /// Value = (int)CombatStyle (Support / Heal), Key = BattleEventKeys.PulseSupport("support") / PulseHeal("heal").
        /// 바로 뒤에 그 맥박의 결과가 온다: ShopHealed(SourceId = 슬롯, 회복량이 있을 때) · EchoHealed(SourceId = 슬롯, 힐만).
        /// 지원 강화는 이벤트 없이 GuardianState.BuffTimer / EchoState.BuffTimer로 보인다
        /// (범위: 지원 = 양옆 지킴이 + supportEchoBuffRadius 안 잔향이, 힐 = healEchoRadius 안 잔향이).
        /// 지원/힐 성향 잔향이의 맥박은 이 이벤트를 내지 않는다.
        /// </summary>
        GuardianPulse,
    }

    public readonly struct BattleEvent
    {
        public readonly BattleEventType Type;
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly Vector2 Position;
        public readonly float Value;
        /// <summary>스킬 ID / 시너지 ID / 연출 키 등 (없으면 null)</summary>
        public readonly string Key;

        public BattleEvent(BattleEventType type, int sourceId = -1, int targetId = -1, Vector2 position = default, float value = 0f,
            string key = null)
        {
            Type = type;
            SourceId = sourceId;
            TargetId = targetId;
            Position = position;
            Value = value;
            Key = key;
        }
    }

    public enum PlaceResult
    {
        Ok = 0,
        InvalidSlot = 1,
        SlotLocked = 2,
        NotOwned = 3,
        FormLocked = 4,
        /// <summary>이미 반대편(지킴이↔잔향이)에 등록된 형태</summary>
        OnOtherSide = 5,
        /// <summary>같은 계보는 지킴이 1종 + 잔향이 1종까지</summary>
        LineageLimit = 6,
        /// <summary>원정 파견 중</summary>
        Dispatched = 7,
    }

    /// <summary>편성 팀 시너지 (예: "짝짝이 계보 공명 Lv.2")</summary>
    public sealed class SynergyInfo
    {
        public LineageDef Lineage;
        public int ResonanceLevel;
        public float AttackBonusPercent;
        public string Label;
    }

    public sealed class ExpeditionResult
    {
        public ExpeditionZoneDef Zone;
        public List<string> LineageIds = new();
        public List<RewardData> Rewards = new();
    }

    public sealed class DungeonResult
    {
        public bool Cleared;
        public int Floor;
        public float SurvivedSeconds;
        public List<RewardData> Rewards = new();
    }
}
