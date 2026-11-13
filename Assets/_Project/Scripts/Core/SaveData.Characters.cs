using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>계보 하나의 저장 데이터. 레벨/호감도는 계보 안의 모든 형태가 공유한다.</summary>
    [Serializable]
    public class LineageSave
    {
        public int level = 1;
        /// <summary>기억편 (중복 획득 시 변환되는 계보 전용 자원)</summary>
        public int shards;
        /// <summary>계보 공명 레벨</summary>
        public int resonance;
        /// <summary>호감도</summary>
        public int affinity;
        public HashSet<string> unlockedForms = new();
        /// <summary>계보별 누적 기록 (원정 횟수, 잔향이 출격 횟수 등)</summary>
        public Dictionary<ProgressType, int> progress = new();
        /// <summary>진화 가속으로 채운 조건 진행도. 키 = "{formId}/{조건 인덱스}"</summary>
        public Dictionary<string, int> conditionBonus = new();
        /// <summary>장착한 의상 ID (null = 기본 외형)</summary>
        public string equippedCostume;
        public long obtainedUnix;

        public void Sanitize()
        {
            if (level < 1) level = 1;
            if (shards < 0) shards = 0;
            if (resonance < 0) resonance = 0;
            unlockedForms ??= new HashSet<string>();
            progress ??= new Dictionary<ProgressType, int>();
            conditionBonus ??= new Dictionary<string, int>();
        }
    }

    [Serializable]
    public class ShopSave
    {
        /// <summary>상품 ID → 누적 구매 수</summary>
        public Dictionary<string, int> totalPurchases = new();
        /// <summary>상품 ID → 오늘 구매 수 (dailyDate가 바뀌면 초기화)</summary>
        public Dictionary<string, int> dailyPurchases = new();
        /// <summary>로컬 날짜 yyyy-MM-dd</summary>
        public string dailyDate;

        public void Sanitize()
        {
            totalPurchases ??= new Dictionary<string, int>();
            dailyPurchases ??= new Dictionary<string, int>();
        }
    }
}
