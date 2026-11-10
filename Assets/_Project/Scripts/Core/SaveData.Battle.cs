using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>편성: 지킴이 방위 링 5칸 + 잔향이 덱 6칸. 값은 형태 ID (빈칸은 null)</summary>
    [Serializable]
    public class FormationSave
    {
        public const int GuardianSlots = 5;
        public const int EchoSlots = 6;

        public string[] guardians = new string[GuardianSlots];
        public string[] echoes = new string[EchoSlots];
        /// <summary>
        /// 플레이어가 칸에 직접 놓은 횟수 (끌어 놓기/눌러 놓기 성공, 자동 편성 제외). 편성 탭 사용법 안내 줄은
        /// 이 값이 3 미만일 때만 보인다 (M80). 옛 세이브는 0부터 센다.
        /// </summary>
        public int placementsDone;

        public void Sanitize()
        {
            guardians = Resize(guardians, GuardianSlots);
            echoes = Resize(echoes, EchoSlots);
            if (placementsDone < 0) placementsDone = 0;
        }

        static string[] Resize(string[] source, int length)
        {
            if (source != null && source.Length == length) return source;
            var result = new string[length];
            if (source != null) Array.Copy(source, result, Math.Min(source.Length, length));
            return result;
        }
    }

    [Serializable]
    public class BattleSave
    {
        /// <summary>현재 진행 중인 스테이지 (1부터)</summary>
        public int stage = 1;
        /// <summary>도달한 최고 스테이지</summary>
        public int highestStage = 1;
        /// <summary>자동 소환 + 자동 불 밝히기</summary>
        public bool autoMode = true;
        /// <summary>클리어 시 다음 스테이지로 자동 진행</summary>
        public bool autoAdvance = true;
        /// <summary>전투 배속 (1 또는 2)</summary>
        public int speed = 1;
        public long totalKills;
        /// <summary>
        /// 보스에 막혀 한 단계 내려왔을 때 그 막힌 스테이지 (0 = 없음). 이 스테이지 이상을 클리어하면 0으로 돌아간다.
        /// 아래 스테이지에 있으면 "파밍 중" (BattleSystem.IsFarming, 자동 보스 도전 개방 뒤).
        /// </summary>
        public int challengeTarget;
        /// <summary>
        /// 파밍 중 자동 보스 도전 (FeatureId.AutoChallenge가 열린 뒤에만 쓴다). 기본 켜짐: 막히면 파밍 스테이지를
        /// BattleSystem.AutoChallengeEvery번 클리어할 때마다 스스로 다시 도전하므로 방치·오프라인 진행이 멈추지 않는다
        /// (꺼 두면 보스 도전 버튼을 누를 때까지 그 스테이지를 반복). 필드에 초기값을 두었으므로 이 값이 없는 옛 세이브도 켜짐으로 읽힌다
        /// (Newtonsoft는 JSON에 있는 필드만 덮어쓴다).
        /// </summary>
        public bool autoChallenge = true;
        /// <summary>자동 불 밝히기 방식 (LampPolicy 숫자, 0 = Auto — 옛 세이브도 Auto, CR-02)</summary>
        public int lampPolicy;

        public void Sanitize()
        {
            if (stage < 1) stage = 1;
            if (highestStage < stage) highestStage = stage;
            if (speed < 1) speed = 1;
            // 도달한 적 없는 스테이지에 막혔을 수는 없다 (손상된 값)
            if (challengeTarget < 0 || challengeTarget > highestStage) challengeTarget = 0;
            if (lampPolicy < 0 || lampPolicy > 2) lampPolicy = 0;
        }
    }

    [Serializable]
    public class ExpeditionSave
    {
        public string zoneId;
        public List<string> lineageIds = new();
        public long startUnix;
        public long endUnix;
        /// <summary>다녀올 시간 단계 (IDLE-04: 0 기본 · 1 길게 ×3 · 2 밤새 ×6, 옛 세이브는 0)</summary>
        public int durationTier;

        public void Sanitize()
        {
            lineageIds ??= new List<string>();
            if (durationTier < 0 || durationTier > 2) durationTier = 0;
        }
    }

    /// <summary>구역별로 마지막에 보낸 친구 (원정 "다시 보내기", 준비 창 미리 채우기). 받거나 취소해도 남는다</summary>
    [Serializable]
    public class ExpeditionMembersSave
    {
        public string zoneId;
        /// <summary>보낸 순서 그대로의 계보 ID</summary>
        public List<string> lineageIds = new();
        /// <summary>마지막에 보낸 다녀올 시간 단계 (다시 보내기가 그대로 쓴다, 0~2)</summary>
        public int lastTier;

        public void Sanitize()
        {
            lineageIds ??= new List<string>();
            lineageIds.RemoveAll(string.IsNullOrEmpty);
            if (lastTier < 0 || lastTier > 2) lastTier = 0;
        }
    }

    [Serializable]
    public class DungeonSave
    {
        /// <summary>입장권 모아 두기 저장 상한 (콘텐츠 상한 9 + 수요 이벤트 2보다 넉넉하게 — 실제 상한은 DungeonSystem이 지킨다)</summary>
        public const int MaxTicketBank = 99;
        /// <summary>잔향 축복 최대 개수 (PRG-07)</summary>
        public const int MaxBlessings = 10;

        /// <summary>돌파한 최고 층 (0 = 아직 없음)</summary>
        public int highestFloor;
        public int ticketsUsed;
        /// <summary>입장권 사용 기준 로컬 날짜 yyyy-MM-dd</summary>
        public string ticketDate;

        // ── 4차 ──
        /// <summary>고른 잔향 축복 ID (던전 전용 능력치, PRG-07)</summary>
        public List<string> blessings = new();
        /// <summary>아직 고르지 않은 축복 수 (3층마다 1)</summary>
        public int blessingPicksPending;
        /// <summary>지금 보여 줄 축복 3개 (다시 열어도 같게 저장)</summary>
        public List<string> blessingOffer = new();
        /// <summary>무료 다시 고르기를 쓴 주 키</summary>
        public string respecWeekKey;
        /// <summary>모아 둔 입장권 (NEW-04, -1 = 아직 옮기지 않음 → 처음 볼 때 그날 남은 입장권이 된다)</summary>
        public int ticketBank = -1;
        /// <summary>모아 둔 입장권을 마지막으로 채운 날 키</summary>
        public string bankDayKey;

        public void Sanitize()
        {
            if (highestFloor < 0) highestFloor = 0;
            if (ticketsUsed < 0) ticketsUsed = 0;
            blessings ??= new List<string>();
            blessings.RemoveAll(string.IsNullOrEmpty);
            blessingOffer ??= new List<string>();
            blessingOffer.RemoveAll(string.IsNullOrEmpty);
            if (blessingPicksPending < 0) blessingPicksPending = 0;
            if (blessingPicksPending > MaxBlessings) blessingPicksPending = MaxBlessings;
            if (ticketBank < -1) ticketBank = -1;
            if (ticketBank > MaxTicketBank) ticketBank = MaxTicketBank;
        }
    }
}
