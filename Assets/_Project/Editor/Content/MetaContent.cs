using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 메타 콘텐츠 원본 (기능 개방 / 가이드 퀘스트 / 수선 일지 / 출석부 / 수선 바구니 / 복귀 문구 / 튜토리얼 / 전투력 +
    /// 4차: 수선점 기록 / 도감 / 소환 마일스톤 / 호감도 / 동화책 / 챕터 상자 / 이벤트 / 수첩 / 다시 만난 날 / 쿠폰 / 축복 / 첫 주 /
    /// 오늘의 작은 선물 / 반딧불 / 응원 / 보스 힌트 / 백업 알림).
    /// Apply가 MetaDatabase를 기본값으로 비운 뒤 Fill* 부분 메서드로 채운다. 부분 메서드는 MetaContent.*.cs 파일
    /// (예: MetaContent.Features.cs에 <c>static partial void FillFeatures(MetaDatabase db, GameContent content) { ... }</c>)에서 구현한다.
    /// 구현하지 않은 부분은 비어 있고, 비어 있어도 게임이 돈다 (정의 없는 기능은 열림 · 빈 4차 콘텐츠 = 그 기능 꺼짐). 담당: M2 · P4-8.
    /// 수치·문구 기준: scratchpad/IDLE_RESEARCH.md (F-03 개방표, Q-01 30단계, Q-02 일일 11·주간 8 + 상자, Q-03 새싹 7·정규 28,
    /// IDLE-01 시간당 표·상한, IDLE-02 빠른 수선, IDLE-03 계보 성격 한 줄 7×7, FTUE-01 16단계, PRG-01 권장 전투력),
    /// 4차는 Docs/SPEC4.md (부록 A · B · C, §2.8, §11.2 예산).
    /// content는 아이템/계보/의상 ID 확인용 (null일 수 있음 — 도감 힌트처럼 목록이 필요한 것은 그때 건너뛴다).
    /// </summary>
    internal static partial class MetaContent
    {
        public static void Apply(MetaDatabase db, GameContent content) => Apply(db, content, false);

        /// <summary>
        /// allStretch = true: 꺼 둔 확장 항목(<see cref="Stretch"/>)까지 모두 켠 내용으로 채운다 — 시험 · 예산 검사용.
        /// 게임 데이터(MetaDataBuilder)는 늘 false로 부른다
        /// </summary>
        internal static void Apply(MetaDatabase db, GameContent content, bool allStretch)
        {
            _allStretch = allStretch;
            try
            {
                db.features = new List<FeatureUnlockDef>();
                db.guideQuests = new List<GuideQuestDef>();
                db.guideRepeat = new GuideRepeatConfig();
                db.guideVisible = MetaCondition.Always();
                db.missions = new List<MissionDef>();
                db.missionChests = new List<MissionChestDef>();
                db.attendance = new List<AttendanceDayDef>();
                db.idleChest = new IdleChestConfig();
                db.returnLines = new List<ReturnLineDef>();
                db.ftueSteps = new List<FtueStepDef>();
                db.power = new PowerConfig();

                // ── 4차 (SPEC4 §2.3 · §13) ──
                db.achievements = new List<AchievementDef>();
                db.codex = new CodexConfig();
                db.gachaMilestones = new List<GachaMilestoneDef>();
                db.gachaRepeat = new GachaRepeatMilestone();
                db.events = new List<EventDef>();
                db.season = new SeasonConfig();
                db.comeback = new ComebackConfig();
                db.affinity = new AffinityConfig();
                db.storyChapters = new List<StoryChapterDef>();
                db.lostChest = new LostChestConfig();
                db.coupons = new List<CouponDef>();
                db.couponSalt = "";
                db.blessings = new List<BlessingDef>();
                db.starterWeek = new StarterWeekConfig();
                db.dailyGifts = new List<DailyGiftDef>();
                db.firefly = new FireflyConfig();
                db.cheer = new CheerConfig();
                db.bossHints = new List<BossHintDef>();
                db.dungeonTicketBankCap = 0;
                db.backupReminder = new BackupReminderConfig();

                FillFeatures(db, content);
                FillGuideQuests(db, content);
                FillMissions(db, content);
                FillAttendance(db, content);
                FillIdleChest(db, content);
                FillReturnLines(db, content);
                FillFtue(db, content);
                FillPower(db, content);

                FillAchievements(db, content);
                FillCodex(db, content);
                FillGachaMilestones(db, content);
                FillAffinity(db, content);
                FillStory(db, content);
                FillChapterChests(db, content);
                FillEvents(db, content);
                FillSeason(db, content);
                FillComeback(db, content);
                FillCoupons(db, content);
                FillBlessings(db, content);
                FillStarterWeek(db, content);
                FillDailyGifts(db, content);
                FillFirefly(db, content);
                FillCheer(db, content);
                FillBossHints(db, content);
                FillBackup(db, content);

                // NEW-04 입장권 모아 두기 (확장): 0 = 끔 = 지금처럼 날마다 초기화 (SPEC4 §13.9 콘텐츠 9)
                db.dungeonTicketBankCap = On(Stretch.TicketBank) ? DungeonTicketBankCap : 0;
            }
            finally
            {
                _allStretch = false;
            }
            db.ClearCache();
        }

        // ───────────── 확장 항목 (SPEC4 스트레치 · RD-2 — 콘텐츠로 끈다, RM-2 plan §3.2 #17) ─────────────

        /// <summary>
        /// 확장 항목 켜기. 4차 확장 항목에는 FeatureId가 없어 기능 잠금으로 끌 수 없으므로 콘텐츠를 비워 끈다 —
        /// 시스템은 빈 콘텐츠를 "꺼짐"으로 본다 (P4-1 · P4-2 규칙). G3에서 검증된 항목만 하나씩 true로 바꾼다.
        /// 끈 동안: 수첩 칸 없음 · 잃어버린 상자 확률 0 · 축복 없음 · 반딧불 하루 0마리 · 입장권 모아 두기 0 · 별빛 산책 짝짝이 의상 줄 없음.
        /// 쿠폰은 스위치가 없다 — 늘 시험용 해시 하나만 싣는다 (실제 쿠폰 번호는 사용자 결정, SPEC4 §4.5).
        /// 내용은 코드에 그대로 있다 (시험 · 예산 검사는 <c>Apply(db, content, allStretch: true)</c>로 본다).
        /// </summary>
        internal static class Stretch
        {
            /// <summary>Q-06 별빛 산책 수첩 30칸 (+ 30칸 의상 jjak_starwalk 줄)</summary>
            public static readonly bool Season = false;
            /// <summary>PRG-06 잃어버린 상자 (클리어마다 0.6). 챕터 상자는 핵심이라 늘 켜짐</summary>
            public static readonly bool LostChest = false;
            /// <summary>PRG-07 잔향 축복</summary>
            public static readonly bool Blessings = false;
            /// <summary>NEW-03 길 잃은 반딧불 (하루 6마리)</summary>
            public static readonly bool Firefly = false;
            /// <summary>NEW-04 던전 입장권 모아 두기 (9장)</summary>
            public static readonly bool TicketBank = false;
        }

        /// <summary>NEW-04 모아 둘 수 있는 던전 입장권 (확장 항목이 켜졌을 때)</summary>
        internal const int DungeonTicketBankCap = 9;

        /// <summary>Apply(…, allStretch: true) 도중이면 true</summary>
        static bool _allStretch;

        /// <summary>그 확장 항목을 데이터에 넣을지 (스위치 또는 시험용 전체 켜기)</summary>
        static bool On(bool stretchSwitch) => stretchSwitch || _allStretch;

        // ───────────── 1~3차 ─────────────

        /// <summary>F-03 개방표 → db.features (아이콘은 ArtLib)</summary>
        static partial void FillFeatures(MetaDatabase db, GameContent content);

        /// <summary>Q-01 1~30단계 → db.guideQuests, 반복 규칙 → db.guideRepeat, 줄 보임 조건 → db.guideVisible</summary>
        static partial void FillGuideQuests(MetaDatabase db, GameContent content);

        /// <summary>Q-02 일일 11개 / 주간 8개 → db.missions, 상자 20~100 → db.missionChests</summary>
        static partial void FillMissions(MetaDatabase db, GameContent content);

        /// <summary>Q-03 새싹 7일 + 정규 28일 → db.attendance</summary>
        static partial void FillAttendance(MetaDatabase db, GameContent content);

        /// <summary>IDLE-01 시간당 표 / 상한 / IDLE-02 빠른 수선 / IDLE-03 두 배 → db.idleChest</summary>
        static partial void FillIdleChest(MetaDatabase db, GameContent content);

        /// <summary>IDLE-03 계보 성격 한 줄 (계보마다 7줄) → db.returnLines</summary>
        static partial void FillReturnLines(MetaDatabase db, GameContent content);

        /// <summary>FTUE-01 16단계 → db.ftueSteps</summary>
        static partial void FillFtue(MetaDatabase db, GameContent content);

        /// <summary>PRG-01 가중치·권장 전투력 → db.power</summary>
        static partial void FillPower(MetaDatabase db, GameContent content);

        // ───────────── 4차 (SPEC4 부록 A · B · C) ─────────────

        /// <summary>Q-04 수선점 기록 17줄 62단계 (A.1) → db.achievements</summary>
        static partial void FillAchievements(MetaDatabase db, GameContent content);

        /// <summary>Q-05 도감 보상 · 모음 보너스 · 힌트 · 적 문구 (A.2) → db.codex</summary>
        static partial void FillCodex(MetaDatabase db, GameContent content);

        /// <summary>PRG-04 소환 마일스톤 (B.1, §6.4) → db.gachaMilestones / db.gachaRepeat</summary>
        static partial void FillGachaMilestones(MetaDatabase db, GameContent content);

        /// <summary>CR-10 호감도 단계 · 기억 조각 · 쓰다듬기 말풍선 (A.3 · A.4) → db.affinity</summary>
        static partial void FillAffinity(MetaDatabase db, GameContent content);

        /// <summary>CR-05 동화책 1~5장 (A.5) → db.storyChapters</summary>
        static partial void FillStory(MetaDatabase db, GameContent content);

        /// <summary>PRG-06 챕터 상자 (B.2 + RD-13) · 잃어버린 상자 표 (B.3, 확장) → db.lostChest</summary>
        static partial void FillChapterChests(MetaDatabase db, GameContent content);

        /// <summary>EVT-01 이벤트 넷 (A.7, §8.3) → db.events</summary>
        static partial void FillEvents(MetaDatabase db, GameContent content);

        /// <summary>Q-06 별빛 산책 수첩 (A.8, 확장) → db.season</summary>
        static partial void FillSeason(MetaDatabase db, GameContent content);

        /// <summary>CR-06 다시 만난 날 (B.5, §8.2) → db.comeback</summary>
        static partial void FillComeback(MetaDatabase db, GameContent content);

        /// <summary>EVT-02 쿠폰 해시 (시험용 하나, §4.5) → db.coupons / db.couponSalt</summary>
        static partial void FillCoupons(MetaDatabase db, GameContent content);

        /// <summary>PRG-07 잔향 축복 12종 (B.4, 확장) → db.blessings</summary>
        static partial void FillBlessings(MetaDatabase db, GameContent content);

        /// <summary>NEW-01 첫 주 28과제 · 상자 5 (C.2) → db.starterWeek</summary>
        static partial void FillStarterWeek(MetaDatabase db, GameContent content);

        /// <summary>NEW-02 오늘의 작은 선물 7요일 (C.3) → db.dailyGifts</summary>
        static partial void FillDailyGifts(MetaDatabase db, GameContent content);

        /// <summary>NEW-03 길 잃은 반딧불 (C.4, 확장) → db.firefly</summary>
        static partial void FillFirefly(MetaDatabase db, GameContent content);

        /// <summary>PRG-03 친구들의 응원 (C.5) → db.cheer</summary>
        static partial void FillCheer(MetaDatabase db, GameContent content);

        /// <summary>PRG-02 보스 힌트 (C.1) → db.bossHints</summary>
        static partial void FillBossHints(MetaDatabase db, GameContent content);

        /// <summary>SAVE-01 백업 알림 우편 (델타 17, SB-1) → db.backupReminder</summary>
        static partial void FillBackup(MetaDatabase db, GameContent content);
    }
}
