using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 저장되는 플레이어 데이터. 필드를 추가할 때는 기본값을 넣고,
    /// 구조가 바뀌면 CurrentVersion을 올린 뒤 Sanitize에서 마이그레이션한다.
    /// 캐릭터/전투 관련 저장 구조는 SaveData.Characters.cs / SaveData.Battle.cs에 있다.
    /// 열거형 키 딕셔너리(currencies, linkedAccounts, LineageSave.progress)는 키를 열거형 멤버 "이름"으로 저장하므로
    /// 멤버 이름을 바꾸면 기존 키가 사라진다. 이 빌드가 모르는 키는 로드 때 건너뛴다 (TolerantEnumKeyDictionaryConverter).
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        /// <summary>
        /// 2: LOST &amp; LOVELY 캐릭터/전투/원정 데이터 추가 · 3: 메타(일일 시계/진행 기록/퀘스트/미션/출석/바구니/기능 개방/튜토리얼) 추가 ·
        /// 4: 4차 메타(기록/도감/소환 마일스톤/이벤트/수첩/동화책/호감도/쿠폰/챕터 상자/첫 주/작은 선물/반딧불/응원 + 불 밝히기 방식 · 원정 시간 단계 ·
        /// 잔향 축복 · 입장권 모아 두기) 추가 — MetaSave.MigrateToV4 (도감 채우기 예약, 튜토리얼을 마친 세이브의 PlayMinutes)
        /// </summary>
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        /// <summary>게임 계정 ID (첫 실행 시 게스트로 발급)</summary>
        public string accountId;
        public Dictionary<AuthProviderType, LinkedAccount> linkedAccounts = new();
        /// <summary>수선점 레벨 (성장 탭 "수선점 강화")</summary>
        public int level = 1;
        public Dictionary<CurrencyType, double> currencies = new();
        /// <summary>
        /// 별조각 2풀 (I-14 · D-30 #3): 결제로 받은 몫. currencies[Gem] = gemPaid + gemFree (호환용 합 — 화면 · 비교는 합을 쓴다).
        /// 옛 세이브에는 이 칸이 없어 0으로 읽히고, 로드 때 ReconcileGemPools가 합 전부를 gemFree로 옮긴다
        /// </summary>
        public double gemPaid;
        /// <summary>별조각 2풀: 결제가 아닌 지급(보상 · 시작 지급 · 우편)으로 받은 몫. 소비는 이 풀부터</summary>
        public double gemFree;
        /// <summary>아이템(재료) ID → 보유 개수</summary>
        public Dictionary<string, int> items = new();
        /// <summary>뽑기 테이블 ID → 천장 카운트</summary>
        public Dictionary<string, int> gachaPity = new();
        public List<MailData> mails = new();
        /// <summary>1회성 우편(환영, 레벨 보상 등) 중복 발송 방지 키</summary>
        public HashSet<string> sentOnceMailKeys = new();
        public HashSet<string> readNoticeIds = new();
        /// <summary>"오늘 하루 보지 않기"를 누른 로컬 날짜 (yyyy-MM-dd)</summary>
        public string noticeHiddenDate;
        public long lastSaveUnix;

        /// <summary>보유 계보 ID → 계보 성장 데이터 (레벨/기억편/공명/호감도/해금 형태/누적 기록)</summary>
        public Dictionary<string, LineageSave> lineages = new();
        /// <summary>보유 의상 ID</summary>
        public HashSet<string> unlockedCostumes = new();
        public ShopSave shop = new();
        public FormationSave formation = new();
        public BattleSave battle = new();
        /// <summary>진행 중인 원정</summary>
        public List<ExpeditionSave> expeditions = new();
        /// <summary>구역별로 마지막에 보낸 친구 (원정 다시 보내기). 옛 세이브는 빈 목록</summary>
        public List<ExpeditionMembersSave> expeditionLastMembers = new();
        public DungeonSave dungeon = new();
        /// <summary>1회성 플래그 (예: night_visit, MetaFlags 상수)</summary>
        public HashSet<string> flags = new();
        /// <summary>메타 기능 (일일 시계 / 진행 기록 / 가이드 퀘스트 / 수선 일지 / 출석부 / 수선 바구니 / 기능 개방 / 튜토리얼)</summary>
        public MetaSave meta = new();

        public static PlayerData CreateNew(GameConfig config)
        {
            var data = new PlayerData();
            data.currencies[CurrencyType.Gold] = config.startGold;
            data.currencies[CurrencyType.Gem] = config.startGem;
            data.gemFree = config.startGem;
            return data;
        }

        /// <summary>로드 직후 호출. 누락/손상된 필드를 보정한다.</summary>
        public void Sanitize()
        {
            linkedAccounts ??= new Dictionary<AuthProviderType, LinkedAccount>();
            currencies ??= new Dictionary<CurrencyType, double>();
            items ??= new Dictionary<string, int>();
            SanitizeAmounts();
            ReconcileGemPools();
            gachaPity ??= new Dictionary<string, int>();
            mails ??= new List<MailData>();
            sentOnceMailKeys ??= new HashSet<string>();
            readNoticeIds ??= new HashSet<string>();
            mails.RemoveAll(m => m == null);
            foreach (var mail in mails) mail.rewards ??= new List<RewardData>();
            if (level < 1) level = 1;

            lineages ??= new Dictionary<string, LineageSave>();
            var broken = new List<string>();
            foreach (var pair in lineages)
            {
                if (pair.Value == null) broken.Add(pair.Key);
                else pair.Value.Sanitize();
            }
            foreach (var key in broken) lineages.Remove(key);
            unlockedCostumes ??= new HashSet<string>();
            shop ??= new ShopSave();
            shop.Sanitize();
            formation ??= new FormationSave();
            formation.Sanitize();
            battle ??= new BattleSave();
            battle.Sanitize();
            expeditions ??= new List<ExpeditionSave>();
            expeditions.RemoveAll(e => e == null || string.IsNullOrEmpty(e.zoneId));
            foreach (var expedition in expeditions) expedition.Sanitize();
            expeditionLastMembers ??= new List<ExpeditionMembersSave>();
            expeditionLastMembers.RemoveAll(m => m == null || string.IsNullOrEmpty(m.zoneId));
            foreach (var members in expeditionLastMembers) members.Sanitize();
            dungeon ??= new DungeonSave();
            dungeon.Sanitize();
            flags ??= new HashSet<string>();

            // 버전 3 미만 세이브에는 meta가 없어 새로 만들어진다. 이미 진행한 플레이어는 튜토리얼을 끝낸 것으로 옮긴다
            bool legacyMeta = version < 3 || meta == null;
            // 버전 4 미만은 4차 칸이 없다 (빈 기본값으로 읽힘). 메타 이전 세이브는 MigrateFromLegacy 뒤에 이어서 옮긴다
            bool v4Migration = version < 4;
            meta ??= new MetaSave();
            meta.Sanitize();
            if (legacyMeta) meta.MigrateFromLegacy(HasLegacyProgress());
            if (v4Migration) meta.MigrateToV4();

            version = CurrentVersion;
        }

        /// <summary>
        /// 로드 무결성 (I-14 · SYS_Economy §2.1): 재화 저장값 · 별조각 풀이 음수 · NaN · ∞면 0으로, 재료 개수가 음수면 지운다.
        /// 고친 것마다 경고 로그 ([Save] 무결성). 재료 상한은 아이템 데이터를 아는 InventorySystem이 만들어질 때 맞춘다
        /// </summary>
        void SanitizeAmounts()
        {
            List<CurrencyType> badCurrencies = null;
            foreach (var pair in currencies)
            {
                if (!IsValidAmount(pair.Value)) (badCurrencies ??= new List<CurrencyType>()).Add(pair.Key);
            }
            if (badCurrencies != null)
            {
                foreach (var key in badCurrencies)
                {
                    UnityEngine.Debug.LogWarning($"[Save] 무결성: 재화 {key} 저장값 {currencies[key]} → 0");
                    currencies[key] = 0;
                }
            }

            List<string> badItems = null;
            foreach (var pair in items)
            {
                if (pair.Value < 0) (badItems ??= new List<string>()).Add(pair.Key);
            }
            if (badItems != null)
            {
                foreach (var key in badItems)
                {
                    UnityEngine.Debug.LogWarning($"[Save] 무결성: 재료 {key} 개수 {items[key]} → 0");
                    items.Remove(key);
                }
            }

            if (!IsValidAmount(gemPaid))
            {
                UnityEngine.Debug.LogWarning($"[Save] 무결성: 별조각 유료 풀 {gemPaid} → 0");
                gemPaid = 0;
            }
            if (!IsValidAmount(gemFree))
            {
                UnityEngine.Debug.LogWarning($"[Save] 무결성: 별조각 무료 풀 {gemFree} → 0");
                gemFree = 0;
            }
        }

        /// <summary>저장할 수 있는 재화 값: 0 이상 · 유한 (NaN은 비교가 모두 false라 여기서 걸린다)</summary>
        static bool IsValidAmount(double value) => value >= 0 && !double.IsInfinity(value);

        /// <summary>
        /// 별조각 2풀을 합(currencies[Gem])에 맞춘다 (멱등): 유료 = min(유료, 합), 무료 = 합 − 유료.
        /// 옛 세이브(풀 칸 없음 = 0 · 0)는 합 전부가 무료가 되고, 다른 경로가 합만 바꿨으면 차이를 무료 풀에서 맞춘다.
        /// 로드(Sanitize)와 CurrencySystem이 별조각을 다루기 전에 부른다
        /// </summary>
        public void ReconcileGemPools()
        {
            currencies ??= new Dictionary<CurrencyType, double>();
            double total = currencies.TryGetValue(CurrencyType.Gem, out var value) && IsValidAmount(value) ? value : 0;
            if (!IsValidAmount(gemPaid)) gemPaid = 0;
            if (!IsValidAmount(gemFree)) gemFree = 0;
            if (gemPaid + gemFree == total) return;
            gemPaid = Math.Min(gemPaid, total);
            gemFree = total - gemPaid;
        }

        /// <summary>
        /// 버전 3 미만 세이브가 실제로 진행한 플레이어인지. 시작 친구는 첫 실행 때 자동 지급·저장되므로
        /// 계보 보유 여부는 신호가 아니다 (설치만 한 플레이어도 가지고 있다).
        /// 수선점 강화 / 1-1 너머 도달 / 계보 레벨업 중 하나라도 있으면 진행한 것으로 본다.
        /// </summary>
        bool HasLegacyProgress()
        {
            if (level > 1) return true;
            if (battle != null && battle.highestStage > 1) return true;
            foreach (var pair in lineages)
            {
                if (pair.Value != null && pair.Value.level > 1) return true;
            }
            return false;
        }
    }
}
