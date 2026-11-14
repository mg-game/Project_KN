using UnityEngine;

namespace IdleGame
{
    /// <summary>게임 밸런스/운영 설정. 인스펙터에서 수치를 조정한다. (전투 수치는 BattleConfig)</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "IdleGame/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("시작 재화")]
        public double startGold = 0;
        public double startGem = 300;
        [Tooltip("첫 로드 1회 지급 (T7): 실타래 키가 없는 세이브가 튜토리얼 7단계를 마쳤거나 졸업 · 건너뜀이면 이만큼 (실타래)")]
        public double startLineageXp = 2000;
        [Tooltip("첫 로드 1회 지급 (T7): 실타래 키가 없는 세이브의 튜토리얼 지금 단계가 6단계(수선점 강화) 또는 7단계(유실자 레벨업)이면 이만큼 (실타래)")]
        public double legacyFtueLineageXp = 250;

        [Header("단추 · 실타래 나누기 (결정 2 안 D: 단추가 나오는 모든 곳에서 기준 양 B를 나눠 준다, IncomeSplit)")]
        [Tooltip("기준 양 B 중 단추 몫")]
        public double goldIncomeShare = 0.5;
        [Tooltip("기준 양 B 중 실타래 몫")]
        public double lineageXpIncomeShare = 0.5;

        [Header("레벨업 (비용 = 기본 비용 × 증가율^(레벨-1))")]
        public int maxLevel = 1000;
        [Tooltip("수선점 1 → 2레벨 비용 (단추). 안 D 기준 12 (bal 04_bal_d_note.md D2). Data/GameConfig.asset은 인스펙터 값을 유지하므로 함께 바꾼다")]
        public double levelUpBaseCost = 12;
        public double levelUpCostGrowth = 1.12;
        [Tooltip("수선점 강화에 쓰는 재화. 기본 단추 (SYS_Economy §3 #8 E안 이음매). 바꾸면 성장 화면 표시 · 강해지기 · 획득처도 함께 맞춰야 한다")]
        public CurrencyType shopUpgradeCurrency = CurrencyType.Gold;

        [Header("방치 수익 (초당 골드 = 기본값 × 증가율^(레벨-1) × 보유 효과)")]
        public double baseGoldPerSecond = 1;
        [Tooltip("수선점 레벨마다 초당 단추 배율 (복리). 레벨업 비용 증가율(levelUpCostGrowth 1.12)보다 넉넉히 낮아야 단추가 저절로 불어나지 않는다 — " +
                 "CR-07 재조정 1.10 → 1.06 (회수 기간이 레벨마다 ×1.018 → ×1.057, PROGRESSION-SIM §6.1). Data/GameConfig.asset은 인스펙터 값을 유지하므로 함께 바꾼다")]
        public double goldPerSecondGrowth = 1.06;

        [Header("레벨 달성 보상 (우편 지급)")]
        [Tooltip("N레벨마다 보상. 0이면 사용 안 함")]
        public int levelRewardInterval = 10;
        public double levelRewardGem = 50;

        [Header("오프라인 보상")]
        public float offlineMaxHours = 8f;
        [Range(0f, 1f)] public float offlineRewardRate = 0.5f;
        [Tooltip("이 시간(초)보다 짧게 비우면 보상 없음")]
        public int offlineMinSeconds = 60;

        [Header("우편")]
        public int mailExpireDays = 30;

        [Header("계정 연동")]
        [Tooltip("에디터/개발 빌드에서 가짜 로그인 사용 (Google·카카오 SDK 연동 전 테스트용). 릴리스 빌드는 항상 실제 SDK 사용")]
        public bool useMockAuth = true;
        [Tooltip("첫 계정 연동 보상 젬 (우편 지급). 0이면 없음")]
        public double accountLinkRewardGem = 300;

        [Header("캐릭터")]
        [Tooltip("새 게임 시작 시 지급하는 계보 ID")]
        public string[] starterLineages = { "jjakjjak", "ttokdan", "chalkak", "mongdang" };

        [Header("캐릭터 성장 (계보 공유 레벨, 비용 = 기본 비용 × 증가율^(레벨-1) 실타래)")]
        [Tooltip("계보 레벨 최대치")]
        public int characterMaxLevel = 100;
        [Tooltip("1 → 2레벨 비용 (실타래)")]
        public double characterLevelUpBaseCost = 50;
        public double characterLevelUpCostGrowth = 1.09;
        [Tooltip("유실자(계보) 레벨업에 쓰는 재화. 실타래 (B안 · 결정 2 안 D). 단추로 되돌리면 분리 전 비교 갈래가 된다")]
        public CurrencyType lineageLevelUpCurrency = CurrencyType.LineageXp;
        [Tooltip("레벨당 공격력 증가(%). 레벨 1 기준 선형 (레벨 11 = +80%)")]
        public float attackPerLevelPercent = 8f;
        [Tooltip("레벨당 체력 증가(%). 레벨 1 기준 선형")]
        public float hpPerLevelPercent = 8f;

        [Header("기억편 / 계보 공명")]
        [Tooltip("중복 획득 시 변환되는 기억편. 순서 = 계보 등급 (1성 일반, 2성 희귀, 3성 영웅, 4성 전설, 5성 신화)")]
        public int[] duplicateShardsByGrade = { 5, 10, 20, 40, 80 };
        [Tooltip("계보 공명 최대 레벨")]
        public int maxResonanceLevel = 10;
        [Tooltip("다음 공명 비용 = 이 값 × (현재 공명 레벨 + 1) 기억편")]
        public int resonanceCostPerLevel = 20;
        [Tooltip("공명 레벨당 계보 공격력/체력 증가(%)")]
        public float resonanceBonusPercent = 5f;

        [Header("진화 (기억편으로 재료 대체 / 조건 가속)")]
        [Tooltip("재료 1개를 대체하는 기억편. 순서 = 재료 등급, 0이면 대체 불가 (일반 재료는 항상 불가)")]
        public int[] substituteShardCostByGrade = { 0, 5, 10, 20, 40 };
        [Tooltip("진화 가속: 남은 원정 1회당 기억편. 0이면 가속 불가")]
        public double accelerateExpeditionCost = 4;
        [Tooltip("진화 가속: 남은 잔향이 출격 1회당 기억편. 0이면 가속 불가")]
        public double accelerateEchoDeployCost = 1;
        [Tooltip("진화 가속: 남은 지킴이 처치 1회당 기억편 (합계는 올림). 0이면 가속 불가")]
        public double accelerateGuardianKillCost = 0.2;

        [Header("저장")]
        public float autoSaveInterval = 30f;

        [Header("공지")]
        [Tooltip("공지 JSON URL. 비워두거나 실패하면 Resources/Data/notices.json 사용")]
        public string noticeUrl = "";

        /// <summary>중복 획득 시 변환되는 기억편 (계보 등급별)</summary>
        public int GetDuplicateShards(ItemGrade grade) => ByGrade(duplicateShardsByGrade, grade);

        /// <summary>재료 1개를 대체하는 기억편 (재료 등급별, 0 = 대체 불가)</summary>
        public int GetSubstituteShardCost(ItemGrade grade) => ByGrade(substituteShardCostByGrade, grade);

        /// <summary>등급 순서 표에서 값을 꺼낸다. 표가 비었거나 짧으면 0, 음수는 0으로 본다.</summary>
        static int ByGrade(int[] table, ItemGrade grade)
        {
            int index = (int)grade;
            return table != null && index >= 0 && index < table.Length ? System.Math.Max(0, table[index]) : 0;
        }
    }
}
