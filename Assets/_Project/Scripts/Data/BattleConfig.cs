using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class EnemyDef
    {
        public string id;
        public string name;
        [TextArea] public string description;
        public Sprite sprite;
        [Tooltip("1스테이지(1층) 기준 체력. 스테이지가 오를수록 BattleConfig의 성장률만큼 늘어난다")]
        public float hp = 30f;
        [Tooltip("이동 속도 (월드 단위/초, 방위 링 반지름 = 1)")]
        public float speed = 0.35f;
        [Tooltip("수선점에 닿았을 때 입히는 피해 (보스는 수선점에 붙어 이 피해를 주기적으로 입힌다)")]
        public float damage = 5f;
        public bool isBoss;

        [Header("등장")]
        [Tooltip("이 스테이지(던전 층)부터 등장")]
        public int minStage = 1;
        [Tooltip("일반 적 등장 가중치 (보스는 무시)")]
        public float spawnWeight = 1f;
        [Tooltip("잔향이에게 주는 피해 배율 (예: 좀나방은 천을 갉아먹어 잔향이에게 아프다)")]
        public float echoDamageMultiplier = 1f;

        [Header("연출")]
        [Tooltip("발이 묶여 잔향이를 때리는 공격 연출(EnemyStrike) 간격(초) — 적마다 다른 공격 리듬 (ATK-03 적 표: 좀나방 0.6 · 그림자 0.8 · " +
                 "실뭉치 0.9 · 먼지뭉치/태엽쥐 1.0 · 골무 1.2 · 녹얼룩 1.4 · 보스 2.0). 피해는 매 진행 그대로 들어가므로 밸런스 영향 없음. " +
                 "0 이하면 BattleConfig.enemyStrikeInterval")]
        public float strikeInterval;

        [Header("기믹")]
        public EnemyGimmick gimmicks;
        [Tooltip("Shielded: 보호막 양 (1스테이지 기준, 체력과 같이 성장)")]
        public float shieldAmount;
        [Tooltip("Splitter: 처치 시 나뉘는 적 ID / 수")]
        public string splitInto;
        public int splitCount = 2;
        [Tooltip("Rusting/Mender/Hastener 오라 반지름 (월드 단위)")]
        public float auraRadius = 0.5f;
        [Tooltip("오라 세기: Rusting = 공격 속도 감소%, Mender = 초당 회복(최대 체력 %), Hastener = 이동 속도 증가%")]
        public float auraValue = 20f;
        [Tooltip("Mender: 초당 회복(최대 체력 %). Hastener와 함께 쓸 때 따로 정한다 (0이면 auraValue를 쓴다)")]
        public float auraHealPercent;

        [Header("보스 전용")]
        [Tooltip("주기적으로 불러내는 부하 적 ID (비우면 없음)")]
        public string minionId;
        [Tooltip("부하 소환 간격(초)")]
        public float minionInterval = 6f;
        [Tooltip("한 번에 불러내는 부하 수")]
        public int minionCount = 2;
    }

    /// <summary>
    /// 수선점 방어 전투 밸런스. 수치/적 목록 기본값은 ApplyDefaults에서 채운다.
    /// (IdleDataBuilder가 에셋을 처음 만들 때 호출)
    /// 밸런스 목표: 시작 친구들(짝짝이·똑단이 방어 지킴이 + 찰칵이 저격 지킴이 + 몽당이 지원 잔향이)이 자동 전투로 1~5스테이지를 여유 있게 깨고,
    /// 10~15스테이지 무렵부터 수선점/캐릭터 강화가 필요하다. 한 스테이지는 40~70초.
    /// 2차(스킬·시너지) 이후 기준: 시작 편성은 고유 스킬 + 시너지(조합 3·특성 2·역할 1)를 켠 채로 1-1~1-5를 37~41초에 내구도 100%로 깨고,
    /// 레벨 1 그대로면 2-6 무렵부터 위태로워진다 (스킬/시너지가 없으면 2-4). 적 체력/피해는 이를 맞추려 1차보다 ×1.3 / ×1.1.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "IdleGame/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        public List<EnemyDef> enemies = new();

        [Header("스테이지 진행")]
        [Tooltip("1스테이지에서 보스 등장까지 필요한 처치 수")]
        public int killsToBossBase = 15;
        [Tooltip("스테이지마다 늘어나는 보스 등장 처치 수")]
        public float killsToBossPerStage = 0.6f;
        [Tooltip("보스 등장 처치 수 상한")]
        public int killsToBossMax = 40;
        [Tooltip("스테이지마다 적 체력 배율 (복리)")]
        public float stageHpGrowth = 1.11f;
        [Tooltip("스테이지마다 적 피해 배율 (복리)")]
        public float stageDamageGrowth = 1.08f;
        [Tooltip("이 스테이지부터는 성장률을 완만하게 바꾼다 (캐릭터 성장은 레벨당 선형이므로 후반 벽 완화)")]
        public int stageGrowthTaperStage = 30;
        [Tooltip("완만 구간 스테이지마다 적 체력 배율 (복리)")]
        public float stageHpGrowthLate = 1.07f;
        [Tooltip("완만 구간 스테이지마다 적 피해 배율 (복리)")]
        public float stageDamageGrowthLate = 1.05f;
        [Tooltip("두 번째 완만 구간 시작 스테이지 (0 = 없음). 16-1 = 151 — 저작 챕터(1~15) 뒤 끝없는 사다리의 기울기")]
        public int stageGrowthTaperStage2 = 151;
        [Tooltip("두 번째 완만 구간 스테이지마다 적 체력 배율 (복리) — 수선 비법이 클리어 단추로 버틸 수 있는 기울기")]
        public float stageHpGrowthLate2 = 1.02f;
        [Tooltip("두 번째 완만 구간 스테이지마다 적 피해 배율 (복리)")]
        public float stageDamageGrowthLate2 = 1.015f;
        [Tooltip("스테이지마다 늘어나는 적 이동 속도 비율 (단리)")]
        public float stageSpeedGrowth = 0.01f;
        [Tooltip("적 이동 속도 배율 상한")]
        public float maxSpeedMultiplier = 1.35f;

        [Header("웨이브")]
        [Tooltip("전투 시작 후 첫 적이 나오기까지(초)")]
        public float firstWaveDelay = 1.5f;
        [Tooltip("1스테이지 웨이브당 적 수")]
        public int waveSizeBase = 5;
        [Tooltip("스테이지마다 늘어나는 웨이브당 적 수")]
        public float waveSizePerStage = 0.25f;
        [Tooltip("웨이브당 적 수 상한")]
        public int waveSizeMax = 14;
        [Tooltip("1스테이지 웨이브 안 적 생성 간격(초)")]
        public float spawnIntervalBase = 1.1f;
        [Tooltip("스테이지마다 줄어드는 생성 간격(초)")]
        public float spawnIntervalPerStage = 0.02f;
        [Tooltip("생성 간격 하한(초)")]
        public float spawnIntervalMin = 0.45f;
        [Tooltip("웨이브 사이 쉬는 시간(초)")]
        public float waveRestBase = 3.5f;
        [Tooltip("웨이브 사이 쉬는 시간 하한(초)")]
        public float waveRestMin = 2f;
        [Tooltip("웨이브마다 정한 압박 방향(1~2곳)에서 적이 나올 확률. 나머지는 5방향 무작위")]
        [Range(0f, 1f)] public float pressureBias = 0.7f;
        [Tooltip("압박 방향이 두 곳이 될 확률")]
        [Range(0f, 1f)] public float twoPressureChance = 0.4f;
        [Tooltip("레인 각도 흔들림(도)")]
        public float laneJitterDegrees = 20f;
        [Tooltip("동시에 존재할 수 있는 적 수 상한")]
        public int maxEnemies = 40;
        [Tooltip("보스 등장 후 일반 적 생성 속도 배율 (0 = 보스만)")]
        [Range(0f, 1f)] public float bossPhaseSpawnScale = 0.5f;

        [Header("수선점")]
        [Tooltip("이 반지름 안까지 들어온 적은 모든 지킴이가 돌아서 공격한다 (방위 링 반지름 = 1)")]
        public float shopGuardRadius = 0.7f;
        [Tooltip("수선점에 붙은 보스가 공격하는 간격(초). 일반 적은 닿으면 피해를 주고 사라진다")]
        public float bossSiegeInterval = 2f;
        [Tooltip("수선점에 붙은 보스가 공격하기 전 예비 동작 시간(초). 연출용 EnemyState.SiegeWindup만 바뀌고 밸런스 영향 없음")]
        public float bossSiegeWindupSeconds = 0.35f;
        [Tooltip("방향 위협도 1 = 수선점 최대 내구도의 이 비율만큼의 피해가 몰려옴")]
        public float dangerReferenceRatio = 0.12f;

        [Header("지킴이 성향")]
        [Tooltip("방어: 부채꼴 안 적 이동 속도 배율")]
        [Range(0.1f, 1f)] public float defenseSlowFactor = 0.55f;
        [Tooltip("돌격: 피해 배율")]
        public float assaultDamageScale = 1.6f;
        [Tooltip("돌격: 요격 반경 배율 (근거리)")]
        public float assaultRangeScale = 0.8f;
        [Tooltip("포획: 피해 배율")]
        public float captureDamageScale = 0.85f;
        [Tooltip("포획: 맞은 적 기절 시간(초)")]
        public float captureStunSeconds = 0.6f;
        [Tooltip("보스에게 적용되는 기절 시간 배율")]
        [Range(0f, 1f)] public float bossStunScale = 0.3f;
        [Tooltip("저격: 피해 배율")]
        public float snipeDamageScale = 1.7f;
        [Tooltip("저격: 요격 반경 배율 (원거리)")]
        public float snipeRangeScale = 1.35f;
        [Tooltip("저격: 공격 간격 배율")]
        public float snipeIntervalScale = 1.5f;
        [Tooltip("지원: 피해 배율")]
        public float supportDamageScale = 0.6f;
        [Tooltip("지원: 회복/강화 주기(초)")]
        public float supportInterval = 4f;
        [Tooltip("지원: 회복량 = 공격력 × 이 값")]
        public float supportHealScale = 1.5f;
        [Tooltip("지원: 양옆 지킴이 공격력 증가(%)")]
        public float supportBuffPercent = 15f;
        [Tooltip("지원: 강화 유지 시간(초)")]
        public float supportBuffSeconds = 4.5f;
        [Tooltip("지원: 강화를 받는 주변 잔향이 반경 (지원 지킴이 자리/지원 잔향이 기준)")]
        public float supportEchoBuffRadius = 0.8f;
        [Tooltip("지원: 기본 공격이 거는 표식 (받는 피해 증가 %)")]
        public float supportMarkPercent = 10f;
        [Tooltip("지원: 표식 유지 시간(초)")]
        public float supportMarkSeconds = 2.5f;

        [Header("힐 성향")]
        [Tooltip("힐: 피해 배율")]
        public float healDamageScale = 0.5f;
        [Tooltip("힐: 회복 주기(초)")]
        public float healInterval = 3.5f;
        [Tooltip("힐: 수선점 회복량 = 공격력 × 이 값 (잔향이는 여기에 '지원 성향 잔향이 회복량 배율'을 더 곱한다)")]
        public float healShopScale = 2f;
        [Tooltip("힐: 주변 잔향이 회복량 = 공격력 × 이 값")]
        public float healEchoScale = 1.5f;
        [Tooltip("힐: 회복을 받는 주변 잔향이 반경")]
        public float healEchoRadius = 0.8f;

        [Header("잔향 (전투 자원)")]
        public float startResonance = 10f;
        [Tooltip("잔향이 던전 시작 잔향")]
        public float dungeonStartResonance = 30f;
        [Tooltip("초당 잔향 회복")]
        public float resonanceRegen = 1f;
        public float maxResonance = 99f;
        [Tooltip("일반 적 처치 시 잔향")]
        public float resonanceKillGain = 1f;
        [Tooltip("보스 처치 시 잔향")]
        public float resonanceBossKillGain = 10f;

        [Header("잔향이")]
        [Tooltip("덱 슬롯별 재소환 대기 시간(초)")]
        public float summonCooldown = 2.5f;
        [Tooltip("동시에 존재할 수 있는 잔향이 수")]
        public int maxEchoes = 12;
        [Tooltip("빛으로 풀려 사라질 때 돌려받는 잔향 = 소환 비용 × 이 값")]
        [Range(0f, 1f)] public float echoRefundRate = 0.3f;
        [Tooltip("잔향이 체력 = 형태 체력 × 이 값")]
        public float echoHpScale = 1f;
        [Tooltip("잔향이 공격 사거리 = 형태 사거리 × 이 값 (아래 최소/최대로 제한)")]
        public float echoRangeScale = 0.2f;
        public float echoRangeMin = 0.22f;
        public float echoRangeMax = 0.9f;
        [Tooltip("저격 성향 잔향이 사거리 배율")]
        public float echoSnipeRangeScale = 2.2f;
        [Tooltip("잔향이가 적을 알아채는 거리")]
        public float echoAggroRadius = 0.9f;
        [Tooltip("잔향이가 지키러 나가는 거리 (수선점 중심 기준)")]
        public float echoPatrolRadius = 1.45f;
        [Tooltip("잔향이와 이 거리 안에 붙은 적은 발이 묶여 잔향이와 싸운다 (근접 잔향이만 해당)")]
        public float echoBlockRadius = 0.3f;
        [Tooltip("발이 묶인 적이 잔향이에게 주는 초당 피해 = 적 피해 × 이 값")]
        public float enemyEchoDps = 0.8f;
        [Tooltip("발이 묶인 적이 잔향이를 때리는 연출 이벤트(EnemyStrike) 기본 간격(초) — 적 정의에 strikeInterval이 없을 때만 쓴다 (적별 리듬은 EnemyDef.strikeInterval). " +
                 "피해는 매 진행 그대로 들어가고 이벤트만 이 간격으로 묶는다 (밸런스 영향 없음)")]
        public float enemyStrikeInterval = 0.5f;
        [Tooltip("지원 성향 잔향이 회복량 배율 (지킴이 회복 대비)")]
        public float echoSupportHealScale = 0.6f;

        [Header("자동 전투 (방치)")]
        [Tooltip("자동 소환 판단 간격(초)")]
        public float autoSummonInterval = 0.5f;
        [Tooltip("이 위협도 이상인 방향이 있을 때 자동 소환")]
        [Range(0f, 1f)] public float autoSummonDanger = 0.05f;
        [Tooltip("자동 불 밝히기: 적이 이 수 이상이거나 보스가 있으면 발동")]
        public int autoLampMinEnemies = 5;

        [Header("불빛 게이지 / 수선점 불 밝히기")]
        [Tooltip("일반 적 처치 시 게이지 (1 = 가득)")]
        public float lampPerKill = 0.05f;
        [Tooltip("잔향이가 빛으로 풀릴 때 게이지")]
        public float lampPerEchoDissolve = 0.05f;
        [Tooltip("불 밝히기 피해 = 일반 적 최대 체력 × 이 값")]
        public float lampDamageRatio = 1f;
        [Tooltip("불 밝히기 피해 = 보스 최대 체력 × 이 값")]
        public float lampBossDamageRatio = 0.12f;

        [Header("스킬 / 상태")]
        [Tooltip("모든 유닛의 기본 치명타 확률(%) — 시너지 CritChancePercent가 더해진다")]
        public float baseCritChance = 5f;
        [Tooltip("기본 치명타 추가 피해(%) — 50이면 1.5배. 시너지 CritDamagePercent가 더해진다")]
        public float baseCritDamage = 50f;
        [Tooltip("보호막 스킬에 유지 시간이 없을 때 기본 유지 시간(초)")]
        public float defaultShieldSeconds = 8f;
        [Tooltip("수선점 보호막 최대치 = 수선점 최대 내구도 × 이 값")]
        public float shopShieldCapRatio = 0.5f;
        [Tooltip("소환 스킬 토큰(작은 아군) 기본 유지 시간(초)")]
        public float tokenDefaultLifetime = 8f;
        [Tooltip("동시에 존재할 수 있는 토큰 수 (덱 잔향이 수와 따로 센다)")]
        public int maxTokens = 6;
        [Tooltip("토큰이 빛으로 풀릴 때 불빛 게이지 = 잔향이 게이지 × 이 값 (잔향 환급 없음)")]
        [Range(0f, 1f)] public float tokenLampScale = 0.4f;
        [Tooltip("보스에게 넉백/끌어당김 거리 배율")]
        [Range(0f, 1f)] public float bossKnockbackScale = 0.35f;
        [Tooltip("보스에게 스킬 감속 세기 배율")]
        [Range(0f, 1f)] public float bossSlowScale = 0.6f;
        [Tooltip("감속 최대치 (0.8 = 이동 속도 80% 감소까지)")]
        [Range(0f, 0.95f)] public float maxSlow = 0.8f;
        [Tooltip("스킬 재사용 대기 감소 상한(%)")]
        public float cooldownReductionCap = 50f;
        [Tooltip("수선점 피해 감소 상한(%)")]
        public float damageReductionCap = 60f;
        [Tooltip("불 밝히기가 보이지 않는 적을 드러내는 시간(초)")]
        public float lampRevealSeconds = 3f;

        [Header("적 기믹")]
        [Tooltip("분열(Splitter)로 생긴 작은 적의 체력/보호막 배율")]
        [Range(0.05f, 1f)] public float splitHpScale = 0.4f;
        [Tooltip("분열로 생긴 작은 적의 이동 속도 배율")]
        public float splitSpeedScale = 1.2f;
        [Tooltip("분열로 생긴 작은 적이 수선점에 주는 피해 배율")]
        [Range(0f, 1f)] public float splitDamageScale = 0.5f;
        [Tooltip("분열한 적이 퍼지는 각도(도)")]
        public float splitSpreadDegrees = 14f;
        [Tooltip("보이지 않는 적(Phasing)이 저절로 드러나는 수선점 중심 반경")]
        public float phasingRevealRadius = 0.85f;
        [Tooltip("빛 특성 유닛 주변에서 보이지 않는 적이 드러나는 반경")]
        public float lightRevealRadius = 0.55f;
        [Tooltip("빛 특성 유닛에게 맞은 보이지 않는 적이 드러나 있는 시간(초)")]
        public float lightHitRevealSeconds = 1.5f;
        [Tooltip("분노(Enrage)하는 체력 비율")]
        [Range(0f, 1f)] public float enrageThreshold = 0.5f;
        [Tooltip("분노한 적 이동/공격 속도 배율")]
        public float enrageSpeedScale = 1.3f;
        [Tooltip("분노한 적 피해 배율")]
        public float enrageDamageScale = 1.4f;
        [Tooltip("분노한 보스 부하 소환 간격 배율")]
        [Range(0.1f, 1f)] public float enrageMinionIntervalScale = 0.6f;
        [Tooltip("불빛을 빨아들이는 적(LampDrain)이 수선점에 닿을 때 빼앗는 게이지 (1 = 가득)")]
        public float lampDrainOnHit = 0.15f;
        [Tooltip("불빛을 빨아들이는 적이 수선점 가까이에서 버틸 때 빼앗는 간격(초)")]
        public float lampDrainInterval = 3f;
        [Tooltip("버티는 동안 한 번에 빼앗는 게이지")]
        public float lampDrainPerTick = 0.04f;
        [Tooltip("불빛을 빨아들이기 시작하는 수선점 중심 반경")]
        public float lampDrainRadius = 1.3f;
        [Tooltip("좀나방(FabricEater)이 천 특성 잔향이에게 주는 피해 배율")]
        public float fabricEaterEchoScale = 1.5f;
        [Tooltip("좀나방 가까이의 천 특성 유닛 공격력 감소(%)")]
        public float fabricEaterAttackDownPercent = 20f;
        [Tooltip("좀나방이 천을 갉아먹는 반경")]
        public float fabricEaterRadius = 0.4f;
        [Tooltip("녹/갉아먹힘 약화가 오라를 벗어난 뒤 남아 있는 시간(초)")]
        public float debuffLingerSeconds = 1f;
        [Tooltip("회복 오라(Mender)가 보스를 회복시킬 때 배율")]
        [Range(0f, 1f)] public float bossMendScale = 0.3f;

        [Header("스테이지 기믹 — 일정")]
        [Tooltip("1챕터(1-1 ~ 1-10) 스테이지별 기믹")]
        public StageModifierType[] firstChapterModifiers =
        {
            StageModifierType.None, StageModifierType.None, StageModifierType.None, StageModifierType.Rain, StageModifierType.Wind,
            StageModifierType.None, StageModifierType.Blackout, StageModifierType.DustStorm, StageModifierType.FullMoon, StageModifierType.Festival,
        };
        [Tooltip("2챕터부터: 각 챕터 2·3·4·6·7·8·9번째 스테이지에 돌아가며 나오는 기믹 (챕터마다 한 칸씩 밀린다). 1·5번째는 없음, 10번째는 축제")]
        public StageModifierType[] modifierCycle =
        {
            StageModifierType.Rain, StageModifierType.Wind, StageModifierType.Blackout, StageModifierType.DustStorm, StageModifierType.FullMoon,
        };

        [Header("스테이지 기믹 — 효과")]
        [Tooltip("비 오는 밤: 적 이동 속도 배율")]
        public float rainEnemySpeedScale = 0.85f;
        [Tooltip("비 오는 밤: 비 특성 공격력·회복·보호막 증가(%)")]
        public float rainTraitBonusPercent = 25f;
        [Tooltip("비 오는 밤: 빛 특성 공격력·회복·보호막 감소(%)")]
        public float rainLightPenaltyPercent = 10f;
        [Tooltip("정전: 보이지 않는 적이 저절로 드러나는 수선점 중심 반경 (평소보다 좁다)")]
        public float blackoutRevealRadius = 0.6f;
        [Tooltip("정전: 보이지 않는 적 등장 가중치 배율")]
        public float blackoutPhasingWeightScale = 2.5f;
        [Tooltip("정전: 빛 특성 공격력·회복·보호막 증가(%)")]
        public float blackoutLightBonusPercent = 30f;
        [Tooltip("보름달: 불빛 게이지 충전 증가(%)")]
        public float fullMoonLampGainPercent = 30f;
        [Tooltip("보름달: 잔향이 유지 시간 증가(%)")]
        public float fullMoonEchoLifetimePercent = 25f;
        [Tooltip("보름달: 밤 특성 공격력·회복·보호막 증가(%)")]
        public float fullMoonNightBonusPercent = 20f;
        [Tooltip("바람: 적 이동 속도 배율")]
        public float windEnemySpeedScale = 1.2f;
        [Tooltip("바람: 잔향이 이동 속도 증가(%)")]
        public float windEchoSpeedPercent = 30f;
        [Tooltip("먼지 폭풍: 대량으로 몰려오는 적 ID")]
        public string dustStormEnemyId = "enemy_dust";
        [Tooltip("먼지 폭풍: 그 적의 등장 가중치 배율")]
        public float dustStormWeightScale = 3f;
        [Tooltip("먼지 폭풍: 웨이브당 적 수 배율")]
        public float dustStormWaveSizeScale = 1.3f;
        [Tooltip("먼지 폭풍: 모든 유닛 사거리 감소(%)")]
        public float dustStormRangePenaltyPercent = 15f;
        [Tooltip("먼지 폭풍: 천 특성 공격력·회복·보호막 증가(%)")]
        public float dustStormFabricBonusPercent = 20f;
        [Tooltip("축제의 밤: 클리어 단추 보상 증가(%)")]
        public float festivalGoldPercent = 50f;
        [Tooltip("축제의 밤: 치장/소리 특성 공격력·회복·보호막 증가(%)")]
        public float festivalTraitBonusPercent = 15f;

        [Header("잔향이 던전")]
        [Tooltip("1층 적 체력 배율 (지킴이 없이 잔향이만 싸우므로 스테이지보다 낮게 시작)")]
        public float dungeonHpBase = 0.8f;
        [Tooltip("층마다 적 체력 배율 (복리)")]
        public float dungeonHpGrowth = 1.18f;
        [Tooltip("층마다 적 피해 배율 (복리)")]
        public float dungeonDamageGrowth = 1.1f;
        [Tooltip("1층 보스 등장 처치 수")]
        public int dungeonKillsBase = 12;
        [Tooltip("층마다 늘어나는 보스 등장 처치 수")]
        public float dungeonKillsPerFloor = 1f;
        [Tooltip("층 → 적 종류 해금에 쓰는 스테이지 환산 배율")]
        public float dungeonStagePerFloor = 2f;
        [Tooltip("제한 시간(초). 넘기면 실패")]
        public float dungeonTimeLimit = 120f;
        [Tooltip("하루 입장권")]
        public int dungeonDailyTickets = 3;

        [Header("스테이지 보상")]
        [Tooltip("1스테이지 클리어 단추")]
        public double stageGoldBase = 60;
        [Tooltip("스테이지마다 단추 배율 (복리)")]
        public double stageGoldGrowth = 1.13;
        [Tooltip("클리어마다 별빛 사탕")]
        public int stageCandy = 3;
        [Tooltip("N스테이지마다 (10, 20, ...) 별빛 사탕을 대신 이만큼")]
        public int stageCandyMilestone = 10;
        public int stageCandyMilestoneInterval = 10;
        [Tooltip("클리어 시 수선 천 획득 확률")]
        [Range(0f, 1f)] public float stageClothChance = 0.35f;
        [Tooltip("클리어 시 리본 조각 획득 확률")]
        [Range(0f, 1f)] public float stageRibbonChance = 0.25f;
        [Tooltip("실패 시 받는 단추 = 클리어 단추 × 이 값 × 진행도(처치/보스 등장 처치)")]
        [Range(0f, 1f)] public float failGoldRatio = 0.3f;
        [Tooltip("클리어 시 편성된 계보 호감도")]
        public int clearAffinity = 1;
        [Tooltip("스테이지 종료 후 다음 전투까지(초). 이 동안 뷰가 결과 배너를 보여 준다")]
        public float stageEndPause = 1.5f;

        [Header("던전 보상")]
        public double dungeonGoldBase = 150;
        public double dungeonGoldGrowth = 1.15;
        [Tooltip("기억의 실이 나오기 시작하는 층")]
        public int dungeonThreadFloor = 3;
        [Tooltip("오르골 태엽이 나오기 시작하는 층")]
        public int dungeonSpringFloor = 5;
        [Tooltip("달빛 조각이 나오기 시작하는 층")]
        public int dungeonMoonlightFloor = 10;
        [Tooltip("달빛 조각 획득 확률 (낮음)")]
        [Range(0f, 1f)] public float dungeonMoonlightChance = 0.12f;

        public EnemyDef GetEnemy(string id) => enemies.Find(e => e != null && e.id == id);

        /// <summary>스테이지 보스 등장까지 필요한 처치 수</summary>
        public int GetKillsToBoss(int stage) =>
            Mathf.Clamp(killsToBossBase + Mathf.FloorToInt((Mathf.Max(1, stage) - 1) * killsToBossPerStage), 1, Mathf.Max(1, killsToBossMax));

        /// <summary>스테이지 적 체력 배율 (1스테이지 = 1)</summary>
        public float GetStageHpScale(int stage) => TaperedScale(stageHpGrowth, stageHpGrowthLate, stageHpGrowthLate2, stage);

        /// <summary>스테이지 적 피해 배율 (1스테이지 = 1)</summary>
        public float GetStageDamageScale(int stage) => TaperedScale(stageDamageGrowth, stageDamageGrowthLate, stageDamageGrowthLate2, stage);

        /// <summary>
        /// 세 구간 복리 배율 (PROGRESSION-SIM §8.1 — 성장 시뮬레이션 하네스가 돌린 코드 그대로). 스테이지 s → s+1 한 걸음의 배율은
        /// s가 stageGrowthTaperStage보다 작으면 growth, stageGrowthTaperStage2보다 작으면 lateGrowth, 그 뒤로는 lateGrowth2다
        /// (기본 체력: 1~30 ×1.11 · 30~151 ×1.07 · 151~ ×1.02). 그래서 151스테이지 값까지는 예전 두 구간 곡선 그대로 이어지고
        /// 151 → 152가 첫 ×1.02 걸음이다 (첫 완만 구간도 30 → 31이 첫 ×1.07). 두 번째 시작이 첫 번째보다 앞이면 첫 번째 시작부터 바로 lateGrowth2.
        /// stageGrowthTaperStage2가 0 이하면 두 번째 구간이 없어 예전 곡선과 똑같다. 1e30에서 멈춘다.
        /// </summary>
        float TaperedScale(float growth, float lateGrowth, float lateGrowth2, int stage)
        {
            int steps = Mathf.Max(1, stage) - 1;
            int taper = Mathf.Max(1, stageGrowthTaperStage) - 1;
            int early = Mathf.Min(steps, taper);
            int late = steps - early;
            int late2 = 0;
            if (stageGrowthTaperStage2 > 0)
            {
                int taper2 = Math.Max(taper, stageGrowthTaperStage2 - 1);
                late2 = Math.Max(0, steps - Math.Max(early, taper2));
                late -= late2;
            }
            double scale = Math.Pow(Mathf.Max(1f, growth), early) * Math.Pow(Mathf.Max(1f, lateGrowth), late)
                           * (late2 > 0 ? Math.Pow(Math.Max(1.0, lateGrowth2), late2) : 1.0);
            return (float)Math.Min(scale, 1e30);
        }

        /// <summary>
        /// 스테이지 번호의 기믹. 1챕터는 firstChapterModifiers 그대로,
        /// 2챕터부터는 1·5번째 없음, 10번째 축제, 나머지는 modifierCycle을 챕터마다 한 칸씩 밀어 가며 돌린다.
        /// </summary>
        public StageModifierType GetStageModifier(int stage)
        {
            stage = Mathf.Max(1, stage);
            int chapter = (stage - 1) / 10 + 1;
            int number = (stage - 1) % 10 + 1;
            if (chapter == 1)
                return firstChapterModifiers != null && number - 1 < firstChapterModifiers.Length ? firstChapterModifiers[number - 1] : StageModifierType.None;
            if (number == 10) return StageModifierType.Festival;
            if (number == 1 || number == 5 || modifierCycle == null || modifierCycle.Length == 0) return StageModifierType.None;
            int index = number < 5 ? number - 2 : number - 3; // 2,3,4 → 0,1,2 / 6,7,8,9 → 3,4,5,6
            return modifierCycle[(index + chapter - 2) % modifierCycle.Length];
        }

        /// <summary>던전 층 보스 등장까지 필요한 처치 수</summary>
        public int GetDungeonKillsToBoss(int floor) =>
            Mathf.Clamp(dungeonKillsBase + Mathf.FloorToInt((Mathf.Max(1, floor) - 1) * dungeonKillsPerFloor), 1, Mathf.Max(1, killsToBossMax));

        /// <summary>
        /// 기본 적 9종(보스 2 포함)과 기믹을 채운다. 밸런스 수치의 기본값은 위 필드 초기값이다
        /// (새 에셋/CreateInstance에는 이미 들어 있다). minStage 순서대로 새 기믹이 하나씩 나온다:
        /// 1 먼지뭉치 · 2 좀나방(천 갉아먹기) · 3 엉킨 실뭉치(분열) · 4 녹얼룩(녹) · 5 골무 껍데기(보호막) ·
        /// 6 망각의 그림자(보이지 않음) · 8 태엽쥐(가속 + 회복 오라). 보스: 큰그림자(분노 + 불빛 흡수), 좀나방 여왕(분노 + 부하).
        /// 잔향이를 때리는 공격 연출 간격(strikeInterval)은 ATK-03 적 표의 주기를 따른다 (빠른 좀나방 0.6초 ~ 느린 녹얼룩 1.4초, 보스 2초).
        /// </summary>
        public void ApplyDefaults()
        {
            enemies = new List<EnemyDef>
            {
                new EnemyDef
                {
                    id = "enemy_dust", name = "먼지뭉치",
                    description = "오래 쓰지 않은 물건 위에 내려앉은 먼지가 뭉쳐 굴러다닌다. 약하지만 늘 무리 지어 몰려오고, 먼지 폭풍이 부는 밤엔 끝없이 쏟아진다.",
                    hp = 34f, speed = 0.3f, damage = 4.4f, minStage = 1, spawnWeight = 5f, strikeInterval = 1f,
                },
                new EnemyDef
                {
                    id = "enemy_moth", name = "좀나방",
                    description = "옷장 깊은 곳에서 날아온 좀나방. 빠르게 날아들어 천을 갉아먹는다. 천으로 된 친구들은 가까이 붙으면 힘이 빠지고, 천 잔향이는 더 아프게 물린다.",
                    hp = 21f, speed = 0.48f, damage = 3.3f, minStage = 2, spawnWeight = 3f, echoDamageMultiplier = 2f, strikeInterval = 0.6f,
                    gimmicks = EnemyGimmick.FabricEater,
                },
                new EnemyDef
                {
                    id = "enemy_tangle", name = "엉킨 실뭉치",
                    description = "풀지 못한 채 버려진 실이 엉켜 굴러온다. 쓰러뜨리면 엉킨 실이 풀리며 작은 실뭉치 둘로 나뉘어 다시 굴러온다.",
                    hp = 47f, speed = 0.3f, damage = 5.5f, minStage = 3, spawnWeight = 3f, strikeInterval = 0.9f,
                    gimmicks = EnemyGimmick.Splitter, splitInto = "enemy_tangle", splitCount = 2,
                },
                new EnemyDef
                {
                    id = "enemy_rust", name = "녹얼룩",
                    description = "비를 맞고 잊힌 쇠붙이에서 번진 얼룩. 느릿느릿 다가오지만 아주 단단하고 아프다. 곁에 있는 금속 친구들은 녹이 슬어 손이 굼떠진다.",
                    hp = 98f, speed = 0.19f, damage = 8.8f, minStage = 4, spawnWeight = 2f, strikeInterval = 1.4f,
                    gimmicks = EnemyGimmick.Rusting, auraRadius = 0.55f, auraValue = 30f,
                },
                new EnemyDef
                {
                    id = "enemy_thimble", name = "골무 껍데기",
                    description = "바느질 상자에서 떨어져 나온 낡은 골무. 단단한 금속 껍데기가 공격을 먼저 받아 내므로, 껍데기를 깨야 속까지 닿는다.",
                    hp = 44f, speed = 0.22f, damage = 6.6f, minStage = 5, spawnWeight = 2f, strikeInterval = 1.2f,
                    gimmicks = EnemyGimmick.Shielded, shieldAmount = 30f,
                },
                new EnemyDef
                {
                    id = "enemy_wisp", name = "망각의 그림자",
                    description = "아무도 이름을 불러 주지 않은 기억이 그림자가 되어 스며든다. 평소엔 보이지 않아서 빛을 가진 친구나 수선점 불빛 가까이에서만 붙잡을 수 있다.",
                    hp = 39f, speed = 0.38f, damage = 6.6f, minStage = 6, spawnWeight = 2f, strikeInterval = 0.8f,
                    gimmicks = EnemyGimmick.Phasing,
                },
                new EnemyDef
                {
                    id = "enemy_windup", name = "태엽쥐",
                    description = "태엽이 다 풀린 장난감 쥐. 무리 뒤에서 졸졸 따라오며 곁의 적들 태엽을 감아 더 빨리 달리게 하고, 해진 곳을 조금씩 고쳐 준다. 먼저 쓰러뜨리자.",
                    hp = 39f, speed = 0.24f, damage = 4.4f, minStage = 8, spawnWeight = 1.5f, strikeInterval = 1f,
                    gimmicks = EnemyGimmick.Hastener | EnemyGimmick.Mender, auraRadius = 0.6f, auraValue = 25f, auraHealPercent = 3f,
                },
                new EnemyDef
                {
                    id = "boss_shadow", name = "망각의 큰그림자",
                    description = "잊힌 것들의 그림자가 한데 모여 생긴 거대한 어둠. 수선점 가까이 들러붙어 불빛을 빨아들이고, 상처 입으면 더욱 사나워진다.",
                    hp = 416f, speed = 0.16f, damage = 11f, isBoss = true, minStage = 1, strikeInterval = 2f,
                    gimmicks = EnemyGimmick.Enrage | EnemyGimmick.LampDrain,
                },
                new EnemyDef
                {
                    id = "boss_mothqueen", name = "좀나방 여왕",
                    description = "버려진 옷장의 주인. 날갯짓할 때마다 좀나방 떼를 불러내고, 화가 나면 더 자주 불러낸다. 천으로 된 친구들을 특히 괴롭힌다.",
                    hp = 364f, speed = 0.2f, damage = 9.9f, isBoss = true, minStage = 3, strikeInterval = 2f,
                    minionId = "enemy_moth", minionInterval = 6f, minionCount = 2,
                    gimmicks = EnemyGimmick.Enrage | EnemyGimmick.FabricEater,
                },
            };
        }
    }
}
