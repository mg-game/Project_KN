using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 메타 데이터 파이프라인 (IdleDataBuilder와 같은 방식, 그 파일은 건드리지 않는다).
    /// MetaDatabase 에셋을 불러오거나 만들고(GUID 유지) Editor/Content/MetaContent*.cs 내용으로 덮어쓴 뒤 정합성을 검사한다.
    /// IdleProjectSetup.BuildAll이 IdleDataBuilder.BuildContent 다음에 Apply(content)를 부른다.
    /// 배치 모드: -executeMethod IdleGame.EditorTools.MetaDataBuilder.RebuildMeta (데이터 갱신 + 검사)
    ///           -executeMethod IdleGame.EditorTools.MetaDataBuilder.ValidateTargets (씬을 만든 뒤 코치 대상 경로 확인)
    /// 담당: M2.
    /// </summary>
    public static class MetaDataBuilder
    {
        public const string MetaPath = IdleProjectSetup.Root + "/Data/MetaDatabase.asset";

        /// <summary>FTUE 말풍선 한 줄 최대 글자 수 (FTUE-01 원칙)</summary>
        public const int FtueMaxLineLength = 40;
        /// <summary>FTUE 말풍선 최대 줄 수 (동화 인트로 단계는 장마다 한 줄이라 예외)</summary>
        public const int FtueMaxLines = 2;
        /// <summary>가이드 퀘스트 한 줄 권장 최대 글자 수 (600px 막대, 30pt)</summary>
        public const int GuideTextSoftLimit = 18;
        /// <summary>개방 카드 한 줄 권장 최대 글자 수 (840px 카드)</summary>
        public const int CardTextSoftLimit = 32;
        /// <summary>복귀 화면 성격 한 줄 권장 최대 글자 수 (920px 팝업)</summary>
        public const int ReturnLineSoftLimit = 28;

        /// <summary>content.Meta를 코드 기준으로 갱신해 채운다. 저장(AssetDatabase.SaveAssets)은 호출한 쪽에서 한다</summary>
        public static void Apply(GameContent content)
        {
            content.Meta = EnsureMetaDatabase(content);
            Validate(content.Meta, content);
        }

        /// <summary>MetaDatabase 에셋을 불러오거나 만들고 내용을 덮어쓴다</summary>
        public static MetaDatabase EnsureMetaDatabase(GameContent content)
        {
            var db = AssetDatabase.LoadAssetAtPath<MetaDatabase>(MetaPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<MetaDatabase>();
                Directory.CreateDirectory(Path.GetDirectoryName(MetaPath)!);
                AssetDatabase.CreateAsset(db, MetaPath);
            }
            MetaContent.Apply(db, content);
            db.ClearCache();
            EditorUtility.SetDirty(db);
            return db;
        }

        [MenuItem("IdleGame/Setup/메타 데이터 갱신", priority = 22)]
        public static void RebuildMeta()
        {
            var content = IdleDataBuilder.BuildContent();
            Apply(content);
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGame] 메타 데이터 갱신 완료: " + MetaPath);
        }

        // ═════════════════════════ 정합성 검사 ═════════════════════════

        /// <summary>
        /// 정합성 검사. 오류는 LogError(씬 생성 전 0개여야 함), 경고는 LogWarning.
        ///   · 공통: 조건 종류·카운터 키·목표값(도달 가능한지: 수선점/친구 최대 레벨, 칸 수, 계보·형태·시너지 수), 계보·시너지·플래그 이름,
        ///     보상 종류·양(양수·유한, 수선권/기억편은 정수)·아이템/계보/의상 ID
        ///   · 기능 개방: 중복, 처음부터 열려야 하는 기능(전투/강화/친구들)이 잠김, 자기 참조, 잠김 문구·카드 문구, 열리는 순서
        ///   · 가이드 퀘스트: ID·문구·보상, 반복 규칙, 보임 조건, 초기화되는 조건(오늘/이번 주 카운터·일지 점수 — 완료를 잠가 두지 않아 되돌아감),
        ///     시너지 조건이 시작 편성(+첫 만남 친구)만으로 이미 충족되는지 / 조합에 필요한 친구를 확정으로 얻는지 (StarterSynergyCheck)
        ///   · 가이드 퀘스트·미션 [가기]: 팝업을 여는 이동에 그 팝업 밖(어둠 아래) 손가락 대상을 짝지었는지 (CheckNavFocus)
        ///   · 수선 일지: ID 중복(주기 통틀어), 목표·점수, 상자 점수 오름차순, 일지가 열리는 순간 잠긴 미션을 뺀 점수로 최고 상자에 닿는지 (A4)
        ///   · 출석부: 1~N일 빠짐없이, 칸마다 보상, 새싹 마지막 날 친구 확정
        ///   · 수선 바구니: 줄 키 중복, 최소 스테이지, 상한 단계, 비율 범위, 빠른 수선 비용, GameConfig와 일치
        ///   · 복귀 문구: 계보 ID, 빈 줄, 계보마다 7줄
        ///   · 튜토리얼: ID, 말풍선 2줄·40자, 시작 조건/할 일 인자, 입력 막기에는 대상 필요, 단계 번호 상수(편성 11 / 줄 보임 6)와 ID 일치,
        ///     원정 단계에서 보낼 친구가 1명 이상 있는지 (시작 친구 + 앞 단계 첫 만남·친구 보상 — 원정은 편성과 무관, M05),
        ///     플레이어 손을 거치는 할 일(첫 만남·첫 원정)이 있는 단계가 그 할 일 플래그로 끝나는지 (미리 채워진 카운터로 끝나면 할 일이 사라짐)
        ///   · 전투력: 가중치·비율, 감싸기 배율 ≥ 1, 시작 편성이 1챕터 내내 "여유"인지
        ///   · 화면 경로 형식 (실제 씬 확인은 ValidateTargetsInScene)
        ///   · 4차 (SPEC4 — ValidateMeta4): ID 중복, 단계 · 목표 오름차순, 보상 · 조건, 도감 힌트 키가 실제 칸을 가리키는지,
        ///     §11.2 예산(기록 3,500 · 도감 2,700 · 호감도 4,000 · 동화책 + 챕터 상자 1~5장 900 · 6장부터 열 장마다 200 · 수첩 450 ·
        ///     별이 머문 밤 산책 300 · 다시 만난 날 300 · 소환 200회 150 · 바구니 별조각 줄 하루 140 · 첫 주 350 · 작은 선물 주 30 ·
        ///     반딧불 주 60 · 밤의 수선점 주 40), 해요체 문구('습니다' · '개방' · '하시겠' · '장비' 없음 — 모든 메타 문구),
        ///     동화책 한 줄 40자 · 그림(없으면 메모 — 글만 보인다), 쿠폰 해시 모양
        /// </summary>
        /// <returns>오류 목록 (비면 통과)</returns>
        public static List<string> Validate(MetaDatabase db, GameContent content) => Validate(db, content, null);

        /// <param name="warnings">경고를 받아 갈 목록 (null이면 로그만 — 시험이 "경고 0"을 확인할 때 쓴다)</param>
        public static List<string> Validate(MetaDatabase db, GameContent content, List<string> warnings)
        {
            var r = new Report();
            if (db == null)
            {
                r.Error("MetaDatabase가 없습니다.");
                Log(r, null);
                return r.Errors;
            }
            if (content?.Items == null || content.Characters == null)
                r.Warn("콘텐츠(아이템/계보) 없이 검사합니다 — ID 확인을 건너뜁니다.");

            var ctx = new Context(db, content);
            ValidateFeatures(ctx, r);
            ValidateGuideQuests(ctx, r);
            ValidateMissions(ctx, r);
            ValidateAttendance(ctx, r);
            ValidateIdleChest(ctx, r);
            ValidateReturnLines(ctx, r);
            ValidateFtue(ctx, r);
            ValidatePower(ctx, r);
            ValidateMeta4(ctx, r);
            ValidateCopy(ctx, r);

            Log(r, Summarize(db));
            warnings?.AddRange(r.Warnings);
            return r.Errors;
        }

        // ───────────── 기능 개방 (F-03) ─────────────

        static readonly FeatureId[] AlwaysOpenFeatures = { FeatureId.Battle, FeatureId.ShopUpgrade, FeatureId.Characters };

        static void ValidateFeatures(Context c, Report r)
        {
            var seen = new HashSet<FeatureId>();
            int lastStage = 0;
            FeatureId lastFeature = FeatureId.Battle;
            foreach (var def in c.Db.features)
            {
                if (def == null) { r.Error("기능 개방: 빈 항목"); continue; }
                string where = $"기능 개방 {def.feature}";
                if (!Enum.IsDefined(typeof(FeatureId), def.feature)) { r.Error($"{where}: 모르는 기능 값"); continue; }
                if (!seen.Add(def.feature)) r.Error($"{where}: 중복");
                if (string.IsNullOrWhiteSpace(def.name)) r.Warn($"{where}: 이름 없음");

                var conditions = def.conditions ?? new List<MetaCondition>();
                if (Array.IndexOf(AlwaysOpenFeatures, def.feature) >= 0 && conditions.Count > 0)
                    r.Error($"{where}: 처음부터 열려야 하는 기능에 조건이 있음 (탭이 잠겨 진행이 막힘)");
                foreach (var condition in conditions)
                {
                    if (condition == null) { r.Error($"{where}: 빈 조건"); continue; }
                    if (condition.type == MetaConditionType.FeatureUnlocked && condition.targetId == def.feature.ToString())
                        r.Error($"{where}: 자기 자신이 열려야 열리는 조건 (영원히 잠김)");
                    CheckCondition(where, condition, c, r);
                }
                // alsoRequire (AND, SPEC5 §2.8): 조건 없이 이것만 있으면 절대 열리지 않는다 (conditions 중 하나가 먼저 맞아야 한다)
                if (def.alsoRequire != null && def.alsoRequire.Count > 0)
                {
                    if (conditions.Count == 0) r.Error($"{where}: conditions 없이 alsoRequire만 있음 (열리지 않음)");
                    foreach (var condition in def.alsoRequire)
                    {
                        if (condition == null) { r.Error($"{where}: 빈 alsoRequire 조건"); continue; }
                        if (condition.type == MetaConditionType.FeatureUnlocked && condition.targetId == def.feature.ToString())
                            r.Error($"{where}: alsoRequire에 자기 자신 (영원히 잠김)");
                        CheckCondition($"{where} alsoRequire", condition, c, r);
                    }
                }
                CheckRewards(where, def.rewards, c, r);

                if (conditions.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(def.lockedHint)) r.Warn($"{where}: 잠김 글자(lockedHint) 없음");
                    if (string.IsNullOrWhiteSpace(def.lockedToast)) r.Warn($"{where}: 잠김 토스트(lockedToast) 없음 — 첫 조건으로 만든 문구를 씀");
                    if (def.showCard && string.IsNullOrWhiteSpace(def.cardText)) r.Warn($"{where}: 카드 문구 없음");
                    if (!def.showCard && def.rewards != null && def.rewards.Count > 0)
                        r.Warn($"{where}: 카드 없이 보상을 줌 (보상 날아가기 출처가 없음)");
                }
                if (!string.IsNullOrEmpty(def.cardText) && def.cardText.Length > CardTextSoftLimit)
                    r.Warn($"{where}: 카드 문구가 김 ({def.cardText.Length}자 > {CardTextSoftLimit})");

                // 다음 개방 안내(TryGetNextLocked)는 목록 순서를 쓴다 → 스테이지 조건은 오름차순이어야 자연스럽다
                int stage = StageOf(def);
                if (stage > 0 && stage != int.MaxValue)
                {
                    if (stage < lastStage) r.Warn($"{where}: 열리는 스테이지({stage})가 앞 기능 {lastFeature}({lastStage})보다 이름 (목록 순서 = 개방 순서)");
                    lastStage = Math.Max(lastStage, stage);
                    lastFeature = def.feature;
                }
            }
            foreach (FeatureId feature in Enum.GetValues(typeof(FeatureId)))
            {
                if (!seen.Contains(feature)) r.Warn($"기능 개방: {feature} 정의 없음 (처음부터 열림)");
            }
        }

        // ───────────── 가이드 퀘스트 (Q-01) ─────────────

        static void ValidateGuideQuests(Context c, Report r)
        {
            var ids = new HashSet<string>();
            var synergyCheck = new StarterSynergyCheck(c);
            foreach (var quest in c.Db.guideQuests)
            {
                if (quest == null) { r.Error("가이드 퀘스트: 빈 항목"); continue; }
                string where = $"가이드 퀘스트 {quest.id}";
                if (string.IsNullOrEmpty(quest.id) || !ids.Add(quest.id)) r.Error($"가이드 퀘스트: ID 없음/중복 '{quest.id}'");
                if (string.IsNullOrWhiteSpace(quest.text)) r.Error($"{where}: 문구 없음");
                else if (quest.text.Length > GuideTextSoftLimit) r.Warn($"{where}: 문구가 김 ({quest.text.Length}자 > {GuideTextSoftLimit}) '{quest.text}'");
                if (quest.condition == null) r.Error($"{where}: 조건 없음 (null은 바로 완료)");
                else
                {
                    CheckCondition(where, quest.condition, c, r);
                    // 가이드 퀘스트는 완료를 잠가 두지 않고 조건을 계속 다시 본다 → 초기화되는 값은 받기 전에 되돌아간다
                    var condition = quest.condition;
                    if (condition.type == MetaConditionType.DailyCounter || condition.type == MetaConditionType.WeeklyCounter)
                        r.Warn($"{where}: 오늘/이번 주 카운터는 초기화되어 퀘스트가 되돌아갈 수 있음");
                    else if (condition.type == MetaConditionType.JournalPoints)
                        r.Warn($"{where}: {(condition.targetId == "weekly" ? "이번 주 일지 점수는 월요일 05:00" : "오늘 일지 점수는 매일 05:00")}에 " +
                               "0으로 돌아가 받기 전에 퀘스트가 되돌아갈 수 있음 — 평생 카운터(예: MissionClaim)를 쓸 것");
                    synergyCheck.Check(where, condition, r);
                }
                if (quest.rewards == null || quest.rewards.Count == 0) r.Warn($"{where}: 보상 없음");
                CheckRewards(where, quest.rewards, c, r);
                CheckFeature(where, "이동", quest.navFeature, r);
                CheckPath(where, quest.focusTarget, r);
                CheckNavFocus(where, quest.navFeature, quest.focusTarget, false, r);
            }

            var repeat = c.Db.guideRepeat;
            if (repeat == null) r.Warn("가이드 퀘스트: 반복 규칙 없음");
            else if (repeat.enabled)
            {
                if (repeat.stageInterval < 1) r.Error("가이드 퀘스트 반복: 스테이지 간격이 1 미만");
                if (repeat.shopLevelStep < 1) r.Error("가이드 퀘스트 반복: 수선점 레벨 간격이 1 미만");
                if (repeat.lineageLevelStep < 1) r.Error("가이드 퀘스트 반복: 친구 레벨 간격이 1 미만");
                if (!(repeat.goldMinutes >= 0) || !(repeat.gem >= 0)) r.Error("가이드 퀘스트 반복: 보상이 음수");
                if (repeat.goldMinutes <= 0 && repeat.gem <= 0) r.Warn("가이드 퀘스트 반복: 보상이 없음");
            }
            if (c.Db.guideVisible == null) r.Warn("가이드 퀘스트 보임 조건 없음 (항상 보임)");
            else CheckCondition("가이드 퀘스트 보임 조건", c.Db.guideVisible, c, r);
        }

        // ───────────── 수선 일지 (Q-02) ─────────────

        static readonly Dictionary<MissionPeriod, int> ExpectedMissionCount = new() { { MissionPeriod.Daily, 11 }, { MissionPeriod.Weekly, 8 } };

        static void ValidateMissions(Context c, Report r)
        {
            var ids = new HashSet<string>();
            foreach (var mission in c.Db.missions)
            {
                if (mission == null) { r.Error("미션: 빈 항목"); continue; }
                string where = $"미션 {mission.id}";
                if (string.IsNullOrEmpty(mission.id) || !ids.Add(mission.id)) r.Error($"미션: ID 없음/중복 '{mission.id}'");
                if (!Enum.IsDefined(typeof(MissionPeriod), mission.period)) r.Error($"{where}: 모르는 주기");
                if (string.IsNullOrWhiteSpace(mission.text)) r.Error($"{where}: 문구 없음");
                if (!Enum.IsDefined(typeof(CounterKey), mission.counter)) r.Error($"{where}: 모르는 카운터 {(int)mission.counter}");
                if (mission.target <= 0) r.Error($"{where}: 목표가 0 이하");
                if (mission.points < 0) r.Error($"{where}: 점수가 음수");
                if (mission.points == 0 && (mission.rewards == null || mission.rewards.Count == 0)) r.Warn($"{where}: 점수도 보상도 없음");
                CheckRewards(where, mission.rewards, c, r);
                CheckFeature(where, "필요 기능", mission.requiredFeature, r);
                CheckFeature(where, "이동", mission.navFeature, r);
                CheckPath(where, mission.focusTarget, r);
                CheckNavFocus(where, mission.navFeature, mission.focusTarget, true, r);
            }

            int journalStage = StageOf(c.Db.GetFeature(FeatureId.Missions));
            foreach (MissionPeriod period in Enum.GetValues(typeof(MissionPeriod)))
            {
                var missions = c.Db.GetMissions(period);
                if (ExpectedMissionCount.TryGetValue(period, out int expected) && missions.Count != expected)
                    r.Warn($"미션 {period}: {missions.Count}개 (기준 {expected}개)");

                int total = 0, atJournalOpen = 0;
                foreach (var mission in missions)
                {
                    total += Math.Max(0, mission.points);
                    if (StageOf(c.Db.GetFeature(mission.requiredFeature)) <= journalStage) atJournalOpen += Math.Max(0, mission.points);
                }

                var chests = c.Db.GetMissionChests(period);
                for (int i = 0; i < chests.Count; i++)
                {
                    string where = $"미션 상자 {period} {chests[i].points}점";
                    if (chests[i].points <= 0) r.Error($"미션 상자 {period}[{i}]: 점수가 0 이하");
                    if (i > 0 && chests[i].points == chests[i - 1].points) r.Error($"미션 상자 {period}: 점수 {chests[i].points} 중복");
                    if (chests[i].rewards == null || chests[i].rewards.Count == 0) r.Error($"{where}: 보상 없음");
                    CheckRewards(where, chests[i].rewards, c, r);
                }
                if (chests.Count > 0 && missions.Count > 0)
                {
                    int top = chests[chests.Count - 1].points;
                    if (total < top) r.Error($"미션 {period}: 모든 미션 점수 {total} < 최고 상자 {top}점 (받을 수 없음)");
                    // A4: 일지가 열리는 순간 잠긴 기능의 미션은 숨겨지고 총점에서 빠진다 — 그래도 최고 상자에 닿아야 한다
                    else if (journalStage != int.MaxValue && atJournalOpen < top)
                        r.Error($"미션 {period}: 일지가 열리는 {StageText(journalStage)}에 받을 수 있는 점수 {atJournalOpen} < 최고 상자 {top}점 (A4)");
                }
                else if (missions.Count > 0) r.Warn($"미션 {period}: 점수 상자 없음");
            }
        }

        // ───────────── 출석부 (Q-03) ─────────────

        static void ValidateAttendance(Context c, Report r)
        {
            foreach (AttendanceTrack track in Enum.GetValues(typeof(AttendanceTrack)))
            {
                int length = track == AttendanceTrack.Starter ? AttendanceSystem.StarterLength : AttendanceSystem.MonthlyLength;
                var days = c.Db.GetAttendance(track);
                var seen = new HashSet<int>();
                foreach (var day in days)
                {
                    string where = $"출석부 {track} {day.day}일째";
                    if (day.day < 1 || day.day > length) r.Error($"출석부 {track}: {day.day}일째는 범위 밖 (1~{length})");
                    if (!seen.Add(day.day)) r.Error($"출석부 {track}: {day.day}일째 중복");
                    if (day.rewards == null || day.rewards.Count == 0) r.Error($"{where}: 보상 없음 (도장을 찍어도 받을 것이 없음)");
                    CheckRewards(where, day.rewards, c, r);
                    if (!string.IsNullOrEmpty(day.label) && day.label.Length > 10) r.Warn($"{where}: 칸 설명이 김 ({day.label.Length}자)");
                }
                if (days.Count == 0) { r.Warn($"출석부 {track}: 칸 없음"); continue; }
                for (int d = 1; d <= length; d++)
                {
                    if (!seen.Contains(d)) r.Error($"출석부 {track}: {d}일째 칸이 없음");
                }
            }

            var last = c.Db.GetAttendanceDay(AttendanceTrack.Starter, AttendanceSystem.StarterLength);
            if (last != null && (last.rewards == null || !last.rewards.Exists(x => x != null && x.type == MetaRewardType.Lineage)))
                r.Warn($"출석부 새싹 {AttendanceSystem.StarterLength}일째: 친구 확정 보상이 없음 (Q-03 \"7일째 돌아올 이유\")");
        }

        // ───────────── 수선 바구니 (IDLE-01~03) ─────────────

        static void ValidateIdleChest(Context c, Report r)
        {
            var chest = c.Db.idleChest;
            if (chest == null) { r.Error("수선 바구니: 설정 없음"); return; }

            if (!(chest.baseCapHours > 0f)) r.Error("수선 바구니: 기본 상한이 0 이하");
            if (!(chest.stageGrowth >= 1.0)) r.Error("수선 바구니: 스테이지 배율이 1 미만");
            else if (chest.stageGrowth > 1.2) r.Warn($"수선 바구니: 스테이지 배율 {chest.stageGrowth}이 너무 큼 (복리)");
            if (!(chest.offlineGoldRate >= 0f && chest.offlineGoldRate <= 1f)) r.Error("수선 바구니: 단추 비율이 0~1 밖");
            if (chest.minClaimSeconds < 0) r.Error("수선 바구니: 최소 받기 시간이 음수");

            var keys = new HashSet<string>();
            foreach (var line in chest.lines)
            {
                if (line?.reward == null) { r.Error("수선 바구니: 빈 줄"); continue; }
                if (line.reward.type != MetaRewardType.Currency && line.reward.type != MetaRewardType.Item)
                    r.Error($"수선 바구니: 줄 {line.Key} 종류는 재화/재료만");
                if (line.reward.type == MetaRewardType.Currency && line.reward.currency == CurrencyType.Gold)
                    r.Error("수선 바구니: 단추 줄은 앱을 닫아 둔 시간으로 따로 계산한다 (줄에 넣지 않음)");
                if (!keys.Add(line.Key)) r.Error($"수선 바구니: 줄 {line.Key} 중복 (carry 키가 겹침)");
                if (line.minStage < 1) r.Error($"수선 바구니: 줄 {line.Key} 최소 스테이지가 1 미만");
                CheckReward($"수선 바구니 {line.Key}", line.reward, c, r);
            }
            if (chest.lines.Count == 0) r.Warn("수선 바구니: 줄 없음 (단추만 쌓임)");

            // 상한 단계 (SPEC4 §13.4): 0이 아닌 조건(클리어 스테이지 · 수선점 레벨)이 모두 맞는 단계 중 가장 긴 상한.
            // 목록은 (스테이지, 수선점 레벨) 오름차순 + 상한도 오름차순 — 수선점 레벨을 모르는 옛 규칙(같은 스테이지면 앞 단계)에서도 짧은 상한이 먼저 잡힌다
            float lastHours = chest.baseCapHours;
            int lastStage = 0, lastShop = 0;
            foreach (var step in chest.capSteps)
            {
                if (step == null) { r.Error("수선 바구니: 빈 상한 단계"); continue; }
                string where = $"수선 바구니 상한 {StageText(step.stageCleared)}{(step.shopLevel > 0 ? $" · 수선점 Lv.{step.shopLevel}" : "")}";
                if (step.stageCleared < 0 || step.shopLevel < 0) r.Error($"{where}: 조건이 음수");
                if (step.stageCleared == 0 && step.shopLevel == 0) r.Error($"{where}: 조건이 없음 (늘 적용됨)");
                if (step.stageCleared == 0) r.Warn($"{where}: 클리어 스테이지 조건이 없음 — 수선점 레벨을 모르는 옛 규칙에서는 처음부터 적용됨");
                if (c.Config != null && step.shopLevel > c.Config.maxLevel) r.Error($"{where}: 수선점 Lv.{step.shopLevel} > 최대 {c.Config.maxLevel}");
                if (!(step.capHours > lastHours)) r.Error($"{where}: 상한 {step.capHours}시간이 앞 단계({lastHours}시간)보다 크지 않음");
                if (step.stageCleared < lastStage || (step.stageCleared == lastStage && step.shopLevel <= lastShop))
                    r.Error($"{where}: 상한 단계가 (스테이지, 수선점 레벨) 오름차순이 아님");
                lastHours = Math.Max(lastHours, step.capHours);
                lastStage = step.stageCleared;
                lastShop = step.shopLevel;
            }

            // C-01 별조각 줄 천장과 하루 예산 (SPEC4 §11.2: 어느 스테이지에서든 하루 ≤ 140, 빠른 수선 포함)
            foreach (var line in chest.lines)
            {
                if (line?.reward == null || line.reward.type != MetaRewardType.Currency || line.reward.currency != CurrencyType.Gem || line.eventOnly) continue;
                if (line.maxStageMultiplier < 0) r.Error($"수선 바구니 {line.Key}: 스테이지 배율 천장이 음수");
                if (!(line.maxStageMultiplier > 0)) { r.Error($"수선 바구니 {line.Key}: 별조각 줄에 스테이지 배율 천장이 없음 (C-01 — 스테이지마다 끝없이 늘어남)"); continue; }
                double perHour = Math.Max(0, line.reward.amount) * line.maxStageMultiplier;
                int quickCount = chest.quickIdleGemCosts?.Length ?? 0;
                double perDay = perHour * (24 + quickCount * Math.Max(0f, chest.quickIdleHours));
                if (perDay > BudgetBasketGemPerDay + 1e-6)
                    r.Error($"수선 바구니 {line.Key}: 천장에서 하루 별조각 {perDay:0.#} > 예산 {BudgetBasketGemPerDay} (빠른 수선 {quickCount}번 포함)");
            }

            var thresholds = chest.artStageThresholds;
            if (thresholds == null || thresholds.Length != 3) r.Warn("수선 바구니: 그림 단계 경계는 3개 (idle_basket_0..3)");
            else
            {
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (!(thresholds[i] > 0f && thresholds[i] <= 1f) || i > 0 && !(thresholds[i] > thresholds[i - 1]))
                        r.Error("수선 바구니: 그림 단계 경계는 0~1 사이 오름차순");
                }
            }
            if (!(chest.redDotHours > 0f) || chest.redDotHours > chest.baseCapHours) r.Warn("수선 바구니: 빨간 점 시간이 0 이하이거나 상한보다 김");

            if (!(chest.quickIdleHours > 0f)) r.Error("수선 바구니: 빠른 수선 시간이 0 이하");
            else if (chest.quickIdleHours > chest.baseCapHours) r.Warn("수선 바구니: 빠른 수선 시간이 기본 상한보다 김");
            var costs = chest.quickIdleGemCosts;
            if (costs == null || costs.Length == 0) r.Warn("수선 바구니: 빠른 수선 횟수 0");
            else
            {
                if (costs[0] != 0) r.Warn("수선 바구니: 첫 빠른 수선이 무료가 아님 (IDLE-02 하루 1번 무료)");
                for (int i = 0; i < costs.Length; i++)
                {
                    if (costs[i] < 0) r.Error("수선 바구니: 빠른 수선 비용이 음수");
                    if (i > 0 && costs[i] < costs[i - 1]) r.Warn("수선 바구니: 빠른 수선 비용이 오름차순이 아님");
                }
            }
            if (chest.maxQuickIdleTickets < 1) r.Error("수선 바구니: 빠른 수선권 상한이 1 미만");
            if (chest.returnPopupMinSeconds < 0) r.Error("수선 바구니: 복귀 화면 최소 시간이 음수");
            if (chest.freeDoublePerDay < 0) r.Error("수선 바구니: 무료 두 배 횟수가 음수");

            var config = c.Content?.Config;
            if (config != null)
            {
                if (Math.Abs(config.offlineMaxHours - chest.baseCapHours) > 0.001f)
                    r.Warn($"수선 바구니: 기본 상한 {chest.baseCapHours}시간 ≠ GameConfig.offlineMaxHours {config.offlineMaxHours}시간");
                if (Math.Abs(config.offlineRewardRate - chest.offlineGoldRate) > 0.0001f)
                    r.Warn($"수선 바구니: 단추 비율 {chest.offlineGoldRate} ≠ GameConfig.offlineRewardRate {config.offlineRewardRate}");
            }
        }

        // ───────────── 복귀 문구 (IDLE-03) ─────────────

        static void ValidateReturnLines(Context c, Report r)
        {
            var seen = new HashSet<string>();
            foreach (var def in c.Db.returnLines)
            {
                if (def == null) { r.Error("복귀 문구: 빈 항목"); continue; }
                string where = $"복귀 문구 {def.lineageId}";
                if (string.IsNullOrEmpty(def.lineageId) || !seen.Add(def.lineageId)) r.Error($"복귀 문구: 계보 ID 없음/중복 '{def.lineageId}'");
                if (c.Characters != null && !string.IsNullOrEmpty(def.lineageId) && c.Characters.GetLineage(def.lineageId) == null)
                    r.Error($"{where}: 모르는 계보");
                if (def.lines == null || def.lines.Count == 0) { r.Error($"{where}: 줄 없음"); continue; }
                if (def.lines.Count != 7) r.Warn($"{where}: {def.lines.Count}줄 (기준 7줄)");
                foreach (var line in def.lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) r.Error($"{where}: 빈 줄");
                    else if (line.Length > ReturnLineSoftLimit) r.Warn($"{where}: 줄이 김 ({line.Length}자) '{line}'");
                }
            }
            if (c.Characters?.lineages != null)
            {
                foreach (var lineage in c.Characters.lineages)
                {
                    if (lineage != null && !seen.Contains(lineage.id)) r.Warn($"복귀 문구: 계보 {lineage.id} 줄 없음");
                }
            }
        }

        // ───────────── 튜토리얼 (FTUE-01) ─────────────

        static void ValidateFtue(Context c, Report r)
        {
            var steps = c.Db.ftueSteps;
            var ids = new HashSet<string>();
            // 그 단계까지 확실히 가진 친구 (시작 친구 + 앞 단계의 첫 만남·친구 보상) — 원정 단계 확인용
            var owned = new HashSet<string>(c.StarterLineages);
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) { r.Error($"튜토리얼: {i}단계 비어 있음"); continue; }
                string where = $"튜토리얼 {i} {step.id}";
                CheckExpeditionStepRoster(where, step, owned, r);
                if (string.IsNullOrEmpty(step.id) || !ids.Add(step.id)) r.Error($"튜토리얼: ID 없음/중복 '{step.id}'");
                if (!Enum.IsDefined(typeof(FtueTrigger), step.trigger)) r.Error($"{where}: 모르는 시작 조건");
                if (!Enum.IsDefined(typeof(FtueAction), step.action)) r.Error($"{where}: 모르는 할 일");

                // 말풍선: 최대 2줄, 한 줄 40자 (동화 인트로는 장마다 한 줄)
                if (string.IsNullOrWhiteSpace(step.text)) r.Error($"{where}: 문구 없음");
                else
                {
                    var lines = step.text.Split('\n');
                    if (step.action != FtueAction.StoryIntro && lines.Length > FtueMaxLines)
                        r.Error($"{where}: 말풍선이 {lines.Length}줄 (최대 {FtueMaxLines}줄)");
                    foreach (var line in lines)
                    {
                        if (line.Length > FtueMaxLineLength) r.Error($"{where}: 한 줄 {line.Length}자 (최대 {FtueMaxLineLength}자) '{line}'");
                        if (line.Trim().Length == 0) r.Error($"{where}: 빈 줄");
                    }
                }

                switch (step.trigger)
                {
                    case FtueTrigger.FeatureUnlocked:
                        if (!Enum.TryParse<FeatureId>(step.triggerArg, out var feature)) r.Error($"{where}: 시작 조건 기능 이름 '{step.triggerArg}'을 모름");
                        else if (c.Db.GetFeature(feature)?.conditions == null || c.Db.GetFeature(feature).conditions.Count == 0)
                            r.Warn($"{where}: 시작 조건 기능 {feature}은 처음부터 열려 있어 바로 시작됨");
                        break;
                    case FtueTrigger.AfterDelay:
                        if (!TryParseFloat(step.triggerArg, out float delay) || delay < 0f) r.Error($"{where}: 시작 지연(초) '{step.triggerArg}'이 잘못됨");
                        break;
                }

                switch (step.action)
                {
                    case FtueAction.PrefillIdleChest:
                        if (!TryParseFloat(step.actionArg, out float hours) || !(hours > 0f)) r.Error($"{where}: 바구니 미리 채우기 시간 '{step.actionArg}'이 잘못됨");
                        else if (c.Db.idleChest != null && hours > c.Db.idleChest.baseCapHours) r.Warn($"{where}: 미리 채우기 {hours}시간이 바구니 상한보다 김");
                        break;
                    case FtueAction.FirstMeetingSummon:
                        if (string.IsNullOrEmpty(step.actionArg)) r.Error($"{where}: 첫 만남 계보 ID 없음");
                        else if (c.Characters != null && c.Characters.GetLineage(step.actionArg) == null) r.Error($"{where}: 모르는 계보 '{step.actionArg}'");
                        else if (c.StarterLineages.Contains(step.actionArg)) r.Error($"{where}: 시작 친구 '{step.actionArg}'는 첫 만남이 될 수 없음 (중복 기억편으로 바뀜)");
                        break;
                    case FtueAction.ShortFirstExpedition:
                        if (!TryParseFloat(step.actionArg, out float minutes) || !(minutes > 0f)) r.Error($"{where}: 첫 원정 시간(분) '{step.actionArg}'이 잘못됨");
                        break;
                    case FtueAction.StoryIntro:
                        if (i != 0) r.Warn($"{where}: 동화 인트로가 첫 단계가 아님");
                        break;
                    case FtueAction.Graduation:
                        if (i != steps.Count - 1) r.Warn($"{where}: 졸업이 마지막 단계가 아님");
                        break;
                }

                if (step.blockInput && string.IsNullOrEmpty(step.coachTarget)) r.Error($"{where}: 입력을 막는데 대상이 없음 (화면이 잠김)");
                if (step.blockInput && step.hintOnly) r.Warn($"{where}: 토스트 힌트인데 입력을 막음");
                CheckPath(where, step.coachTarget, r);

                if (step.complete == null) r.Error($"{where}: 완료 조건 없음 (null은 바로 완료)");
                else
                {
                    CheckCondition(where, step.complete, c, r);
                    if (step.complete.type == MetaConditionType.FtueStep || step.complete.type == MetaConditionType.FtueDone)
                        r.Error($"{where}: 완료 조건이 튜토리얼 자신을 기다림 (영원히 멈춤)");
                    if (step.complete.type == MetaConditionType.None && !step.hintOnly && step.action != FtueAction.Graduation &&
                        !string.IsNullOrEmpty(step.coachTarget))
                        r.Warn($"{where}: 손가락 단계가 바로 끝남 (완료 조건 없음)");
                    CheckActionCompletion(where, i, step, r);
                }
                CheckRewards(where, step.rewards, c, r);
                AddGrantedLineages(step, owned);
            }

            if (steps.Count > 0)
            {
                // 기능 개방·줄 보임 조건이 쓰는 단계 번호 상수가 실제 단계와 맞는지
                CheckFtueIndex(c, r, MetaContent.FtueStepFormation, MetaContent.FtueFormationStepId, "편성·시너지 개방 FtueStep");
                CheckFtueIndex(c, r, MetaContent.FtueStepGuideVisible, MetaContent.FtueAfterFirstBossStepId, "가이드 퀘스트 줄 보임 FtueStep");
            }
        }

        /// <summary>
        /// 단계 안에서 플레이어 손을 거쳐야 실행되는 할 일 → 그 할 일이 끝날 때 FtueDirector가 세우는 플래그.
        /// (PrefillIdleChest·EnableAutoBattle 등은 단계가 시작될 때 바로 실행되므로 단계가 곧바로 끝나도 할 일이 사라지지 않는다)
        /// </summary>
        static readonly Dictionary<FtueAction, string> ActionDoneFlags = new()
        {
            { FtueAction.FirstMeetingSummon, MetaFlags.FirstMeetingSummoned },
            { FtueAction.ShortFirstExpedition, MetaFlags.ShortFirstExpeditionUsed },
        };

        /// <summary>
        /// 할 일이 있는 단계가 그 할 일 플래그로 끝나는지. 단계는 하나씩 진행되고 입력을 막지 않으므로, 앞 단계에서 멈춘 동안
        /// 플레이어가 같은 행동(유료 소환·원정)을 하면 평생 카운터가 미리 채워져 단계가 시작과 같은 프레임에 끝나고 할 일이 실행되지 않는다.
        /// </summary>
        static void CheckActionCompletion(string where, int index, FtueStepDef step, Report r)
        {
            if (!ActionDoneFlags.TryGetValue(step.action, out var doneFlag)) return;
            var complete = step.complete;
            if (complete.type == MetaConditionType.Flag && complete.targetId == doneFlag) return;

            bool counter = complete.type == MetaConditionType.Counter || complete.type == MetaConditionType.DailyCounter ||
                           complete.type == MetaConditionType.WeeklyCounter;
            if (counter && index > 0)
                r.Warn($"{where}: 할 일 {step.action}이 있는 단계가 카운터 {complete.counter} ≥ {complete.target}로 끝남 — 앞 단계에서 멈춘 동안 " +
                       $"이미 채워지면 단계가 시작과 같은 프레임에 끝나 할 일이 실행되지 않음. 완료 조건을 MetaCondition.Flag({doneFlag})로 할 것");
            else
                r.Warn($"{where}: 할 일 {step.action}이 있는 단계의 완료 조건이 그 할 일의 플래그({doneFlag})가 아님 — 할 일이 실행되기 전에 끝날 수 있음");
        }

        /// <summary>
        /// 원정 단계(첫 원정 할 일 또는 원정 개방을 기다리는 단계)에 보낼 친구가 1명 이상인지 (M05).
        /// 원정은 편성과 무관해져 편성에 선 친구도 보낼 수 있으므로 보유 수만 본다 — 지금은 실패할 수 없지만 시작 친구 구성이 바뀔 때를 막는다.
        /// </summary>
        static void CheckExpeditionStepRoster(string where, FtueStepDef step, HashSet<string> owned, Report r)
        {
            bool expeditionStep = step.action == FtueAction.ShortFirstExpedition ||
                                  step.trigger == FtueTrigger.FeatureUnlocked && step.triggerArg == nameof(FeatureId.Expedition);
            if (expeditionStep && owned.Count < 1)
                r.Error($"{where}: 원정 단계인데 보낼 친구가 없음 (시작 친구·앞 단계 친구 보상 0명) — 첫 원정에서 막힘");
        }

        /// <summary>단계가 확실히 주는 친구 (첫 만남 확정 소환, 친구 보상)를 보유 목록에 더한다</summary>
        static void AddGrantedLineages(FtueStepDef step, HashSet<string> owned)
        {
            if (step.action == FtueAction.FirstMeetingSummon && !string.IsNullOrEmpty(step.actionArg)) owned.Add(step.actionArg);
            if (step.rewards == null) return;
            foreach (var reward in step.rewards)
            {
                if (reward != null && reward.type == MetaRewardType.Lineage && !string.IsNullOrEmpty(reward.itemId)) owned.Add(reward.itemId);
            }
        }

        static void CheckFtueIndex(Context c, Report r, int index, string expectedId, string what)
        {
            int actual = c.Db.IndexOfFtueStep(expectedId);
            if (actual != index) r.Error($"튜토리얼: {what}({index}) — '{expectedId}' 단계가 {actual}번째에 있음 (MetaContent 상수를 고칠 것)");
        }

        // ───────────── 전투력 (PRG-01) ─────────────

        static void ValidatePower(Context c, Report r)
        {
            var power = c.Db.power;
            if (power == null) { r.Warn("전투력: 설정 없음 (기본값)"); return; }
            if (!(power.recommendedBase > 0) || !(power.recommendedGrowth >= 1.0)) r.Error("전투력: 권장 전투력 기준/증가율이 잘못됨");
            if (power.guardianWeight < 0f || power.echoWeight < 0f || power.shopHpWeight < 0f) r.Error("전투력: 가중치가 음수");
            if (!(power.challengeRatio > 0f) || !(power.challengeRatio < power.comfortableRatio)) r.Error("전투력: 평가 비율은 0 < 도전 < 여유");
            if (!MetaContent.IsPowerCalibrated)
            {
                r.Warn("전투력: 권장 전투력이 아직 보정 전 (IdleGame/전투력 보정)");
                return;
            }

            // 상수를 지역 변수로 받아 둔다 (상수 비교는 컴파일러가 접어 "도달할 수 없는 코드" 경고를 낸다)
            double envelope = MetaContent.RecommendedEnvelopeScale;
            double starterPower = MetaContent.CalibrationStarterPower;

            // 권장은 최소제곱 선 위쪽을 감싸야 초록 "여유"가 지는 싸움이 되지 않는다 (MetaContent.RecommendedEnvelopeScale 주석)
            if (!(envelope >= 1.0))
                r.Warn($"전투력: 감싸기 배율 {envelope}이 1 미만 — 권장이 보정 맞춤보다 낮아 '여유'인데 지는 스테이지가 많아짐");

            // 시작 편성(레벨 1·강화 없음)은 1챕터 내내 "여유"여야 한다 — 감싸기 배율의 위쪽 한계
            if (starterPower > 0 && power.recommendedBase > 0 && power.recommendedGrowth >= 1.0)
            {
                int stage = MetaContent.StarterComfortableThroughStage;
                double recommended = Math.Round(power.recommendedBase * Math.Pow(power.recommendedGrowth, stage - 1), MidpointRounding.AwayFromZero);
                double ratio = recommended > 0 ? starterPower / recommended : double.PositiveInfinity;
                if (ratio < power.comfortableRatio - 1e-6)
                    r.Warn($"전투력: 시작 편성 {starterPower:0}이 {StageText(stage)} 권장 {recommended:0}의 " +
                           $"{ratio:0.00}배 — 1챕터에서 시작 편성이 '여유'가 아님 (감싸기 배율을 낮출 것)");
            }
        }

        // ═════════════════════════ 공통 검사 ═════════════════════════

        static void CheckCondition(string where, MetaCondition condition, Context c, Report r)
        {
            if (condition == null) return;
            if (!Enum.IsDefined(typeof(MetaConditionType), condition.type)) { r.Error($"{where}: 모르는 조건 종류 {(int)condition.type}"); return; }
            switch (condition.type)
            {
                case MetaConditionType.None:
                case MetaConditionType.FtueDone:
                    return;
                case MetaConditionType.Flag:
                    if (string.IsNullOrEmpty(condition.targetId)) r.Error($"{where}: 플래그 이름 없음");
                    else if (!KnownFlags.Contains(condition.targetId) && !condition.targetId.StartsWith(MetaContent.HiddenFlagPrefix, StringComparison.Ordinal))
                        r.Warn($"{where}: MetaFlags에 없는 플래그 '{condition.targetId}' (누가 세우는지 확인)");
                    return;
                case MetaConditionType.FeatureUnlocked:
                    if (!Enum.TryParse<FeatureId>(condition.targetId, out _)) r.Error($"{where}: 모르는 기능 '{condition.targetId}'");
                    return;
                case MetaConditionType.FtueStep:
                    if (condition.target < 0) r.Error($"{where}: 튜토리얼 단계가 음수");
                    else if (c.Db.ftueSteps.Count > 0 && condition.target > c.Db.ftueSteps.Count)
                        r.Warn($"{where}: 튜토리얼 {condition.target}단계 (전체 {c.Db.ftueSteps.Count}단계) — 졸업해야 충족");
                    return;
                case MetaConditionType.Counter:
                case MetaConditionType.DailyCounter:
                case MetaConditionType.WeeklyCounter:
                    if (!Enum.IsDefined(typeof(CounterKey), condition.counter)) r.Error($"{where}: 모르는 카운터 {(int)condition.counter}");
                    break;
                case MetaConditionType.ShopLevel:
                    if (c.Config != null && condition.target > c.Config.maxLevel) r.Error($"{where}: 수선점 Lv.{condition.target} > 최대 {c.Config.maxLevel}");
                    break;
                case MetaConditionType.LineageLevel:
                    if (!string.IsNullOrEmpty(condition.targetId) && c.Characters != null && c.Characters.GetLineage(condition.targetId) == null)
                        r.Error($"{where}: 모르는 계보 '{condition.targetId}'");
                    if (c.Config != null && condition.target > c.Config.characterMaxLevel)
                        r.Error($"{where}: 친구 Lv.{condition.target} > 최대 {c.Config.characterMaxLevel}");
                    break;
                case MetaConditionType.LineageLevelSum:
                    if (c.Config != null && c.Characters != null && condition.target > (long)c.Config.characterMaxLevel * c.Characters.lineages.Count)
                        r.Error($"{where}: 레벨 합 {condition.target}은 도달할 수 없음");
                    break;
                case MetaConditionType.OwnedLineages:
                    if (c.Characters != null && condition.target > c.Characters.lineages.Count) r.Error($"{where}: 계보 {condition.target}개 > 전체 {c.Characters.lineages.Count}개");
                    break;
                case MetaConditionType.UnlockedForms:
                    if (c.FormCount > 0 && condition.target > c.FormCount) r.Error($"{where}: 형태 {condition.target}개 > 전체 {c.FormCount}개");
                    break;
                case MetaConditionType.GuardiansPlaced:
                    if (condition.target > FormationSystem.GuardianSlotCount) r.Error($"{where}: 지킴이 {condition.target}명 > 칸 {FormationSystem.GuardianSlotCount}개");
                    break;
                case MetaConditionType.EchoesPlaced:
                    if (condition.target > FormationSystem.EchoSlotCount) r.Error($"{where}: 잔향이 {condition.target}명 > 칸 {FormationSystem.EchoSlotCount}개");
                    break;
                case MetaConditionType.ActiveSynergies:
                    if (c.Synergies != null && condition.target > c.Synergies.Count) r.Error($"{where}: 시너지 {condition.target}개 > 전체 {c.Synergies.Count}개");
                    break;
                case MetaConditionType.SynergyTier:
                    CheckSynergyTier(where, condition, c, r);
                    break;
                case MetaConditionType.JournalPoints:
                    if (!string.IsNullOrEmpty(condition.targetId) && condition.targetId != "weekly" && condition.targetId != "daily")
                        r.Warn($"{where}: 일지 점수 범위 '{condition.targetId}' (weekly 또는 비움)");
                    var period = condition.targetId == "weekly" ? MissionPeriod.Weekly : MissionPeriod.Daily;
                    int sum = c.Db.GetMissions(period).Sum(m => Math.Max(0, m.points));
                    if (sum > 0 && condition.target > sum) r.Error($"{where}: 일지 {condition.target}점 > {period} 총점 {sum}점");
                    break;
                case MetaConditionType.SynergyKindsSeen:
                    if (c.Synergies != null && condition.target > c.Synergies.Count) r.Error($"{where}: 시너지 {condition.target}종 > 전체 {c.Synergies.Count}종");
                    break;
                case MetaConditionType.CostumesOwned:
                    if (c.Characters != null && condition.target > c.CostumeCount) r.Error($"{where}: 의상 {condition.target}벌 > 전체 {c.CostumeCount}벌");
                    break;
                case MetaConditionType.AffinityLevelCount:
                    int levels = c.Db.affinity?.thresholds?.Length ?? 0;
                    if (!int.TryParse(condition.targetId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level < 1 || levels > 0 && level > levels)
                        r.Error($"{where}: 호감도 단계 '{condition.targetId}' (1~{levels})");
                    if (c.Characters != null && condition.target > c.Characters.lineages.Count) r.Error($"{where}: 계보 {condition.target}개 > 전체 {c.Characters.lineages.Count}개");
                    break;
            }
            if (condition.target <= 0) r.Error($"{where}: 목표가 0 이하 ({condition.type})");
        }

        static void CheckSynergyTier(string where, MetaCondition condition, Context c, Report r)
        {
            if (c.Synergies == null) return;
            if (!string.IsNullOrEmpty(condition.targetId))
            {
                var synergy = c.Synergies.FirstOrDefault(s => s != null && s.id == condition.targetId);
                if (synergy == null) r.Error($"{where}: 모르는 시너지 '{condition.targetId}'");
                else if (condition.target > synergy.tiers.Count) r.Error($"{where}: 시너지 {synergy.id}는 {synergy.tiers.Count}단계까지 (목표 {condition.target}단계)");
                return;
            }
            int maxTiers = c.Synergies.Where(s => s != null).Select(s => s.tiers.Count).DefaultIfEmpty(0).Max();
            if (maxTiers > 0 && condition.target > maxTiers) r.Error($"{where}: 시너지 {condition.target}단계 > 가장 긴 시너지 {maxTiers}단계");
        }

        static void CheckRewards(string where, List<MetaReward> rewards, Context c, Report r)
        {
            if (rewards == null) return;
            foreach (var reward in rewards) CheckReward(where, reward, c, r);
        }

        static void CheckReward(string where, MetaReward reward, Context c, Report r)
        {
            if (reward == null) { r.Error($"{where}: 빈 보상"); return; }
            if (!Enum.IsDefined(typeof(MetaRewardType), reward.type)) { r.Error($"{where}: 모르는 보상 종류 {(int)reward.type}"); return; }
            if (!(reward.amount > 0) || double.IsInfinity(reward.amount)) r.Error($"{where}: 보상 양이 0 이하/잘못됨 ({reward.type} {reward.amount})");
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    if (!Enum.IsDefined(typeof(CurrencyType), reward.currency)) r.Error($"{where}: 모르는 재화 {(int)reward.currency}");
                    break;
                case MetaRewardType.Item:
                    if (string.IsNullOrEmpty(reward.itemId)) r.Error($"{where}: 아이템 ID 없음");
                    else if (c.Items != null && c.Items.Get(reward.itemId) == null) r.Error($"{where}: 모르는 아이템 '{reward.itemId}'");
                    break;
                case MetaRewardType.GoldMinutes:
                    if (reward.amount > 24 * 60) r.Warn($"{where}: 단추 {reward.amount}분치는 하루보다 김");
                    break;
                case MetaRewardType.QuickIdleTicket:
                case MetaRewardType.DoubleTicket:
                case MetaRewardType.SelectShards:
                    if (Math.Abs(reward.amount - Math.Round(reward.amount)) > 1e-9) r.Error($"{where}: {reward.type}는 정수여야 함 ({reward.amount})");
                    if (reward.type == MetaRewardType.QuickIdleTicket && c.Db.idleChest != null && reward.amount > c.Db.idleChest.maxQuickIdleTickets)
                        r.Warn($"{where}: 빠른 수선권 {reward.amount}장 > 보관 상한 {c.Db.idleChest.maxQuickIdleTickets}장 (넘친 양은 버려짐)");
                    break;
                case MetaRewardType.Lineage:
                    if (string.IsNullOrEmpty(reward.itemId)) r.Error($"{where}: 계보 ID 없음");
                    else if (c.Characters != null && c.Characters.GetLineage(reward.itemId) == null) r.Error($"{where}: 모르는 계보 '{reward.itemId}'");
                    else if (c.StarterLineages.Contains(reward.itemId)) r.Warn($"{where}: 시작 친구 '{reward.itemId}'는 항상 기억편으로 바뀜");
                    break;
                case MetaRewardType.Costume:
                    if (string.IsNullOrEmpty(reward.itemId)) r.Error($"{where}: 의상 ID 없음");
                    else if (c.Characters != null && c.Characters.GetCostume(reward.itemId) == null) r.Error($"{where}: 모르는 의상 '{reward.itemId}'");
                    break;
                case MetaRewardType.FriendSelectTicket:
                    if (!Enum.TryParse<ItemGrade>(reward.itemId, out _)) r.Error($"{where}: 친구 선택권 등급 '{reward.itemId}' (ItemGrade 이름)");
                    if (Math.Abs(reward.amount - Math.Round(reward.amount)) > 1e-9) r.Error($"{where}: 친구 선택권은 정수여야 함 ({reward.amount})");
                    break;
            }
        }

        static void CheckFeature(string where, string what, FeatureId feature, Report r)
        {
            if (!Enum.IsDefined(typeof(FeatureId), feature)) r.Error($"{where}: 모르는 {what} 기능 {(int)feature}");
        }

        /// <summary>Canvas 기준 Transform.Find 경로 형식 (비면 통과)</summary>
        static void CheckPath(string where, string path, Report r)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (path.StartsWith("/") || path.EndsWith("/") || path.Contains("//") || path.Trim() != path)
                r.Error($"{where}: 화면 경로 형식이 잘못됨 '{path}'");
        }

        /// <summary>
        /// 이동과 손가락 대상이 맞는지 ([가기]: UINav.GoToFeature 뒤 UINav.FocusTarget). 이동이 팝업을 열면(MetaNavigator)
        /// 손가락 대상은 비우거나 그 팝업 안이어야 한다 — 코치마크는 팝업 위에 그려져도 팝업 밖 대상(상단 바 버튼, 전투 화면 바구니)은
        /// 팝업 어둠 아래에 깔려 누를 수 없다. 반대로 팝업 안 대상은 그 팝업을 여는 이동과만 쓴다
        /// (탭 이동은 열린 팝업을 닫고, 일지 미션 [가기]는 일지를 먼저 닫는다).
        /// </summary>
        static void CheckNavFocus(string where, FeatureId nav, string focus, bool fromJournal, Report r)
        {
            if (string.IsNullOrEmpty(focus)) return;
            string popup = PopupOpenedBy(nav);
            if (popup != null)
            {
                if (!IsUnder(focus, popup))
                    r.Warn($"{where}: 이동 {nav}가 팝업 '{popup}'을 여는데 손가락 대상 '{focus}'는 그 팝업 밖(어둠 아래)에 있음 — " +
                           "대상을 비우거나 팝업 안 버튼을 가리킬 것");
            }
            else if (IsUnder(focus, MetaContent.Targets.PopupRoot))
            {
                r.Warn($"{where}: 손가락 대상 '{focus}'는 팝업 안인데 이동 {nav}는 그 팝업을 열지 않음" +
                       $"{(fromJournal ? " (일지 [가기]는 일지를 먼저 닫음)" : "")} — 손가락이 보이지 않음");
            }
        }

        /// <summary>
        /// 이동이 여는 팝업 경로 (MetaNavigator / SynergyNavigator 기준). 탭·전투 이동이면 null.
        /// 진화·의상은 그 점이 켜진 계보의 친구 상세를 연다 (M73 — 진화 = 기억도 탭, 의상 = 의상 탭). 도감은 친구들 탭에 머문다(팝업 없음)
        /// </summary>
        static string PopupOpenedBy(FeatureId nav) => nav switch
        {
            FeatureId.IdleChest or FeatureId.QuickIdle => MetaContent.Targets.IdleChestPopup,
            FeatureId.Missions or FeatureId.Achievements => MetaContent.Targets.JournalPopup,
            FeatureId.Attendance => MetaContent.Targets.AttendancePopup,
            FeatureId.Evolution or FeatureId.Costumes => MetaContent.Targets.CharacterDetailPopup,
            FeatureId.Synergy => MetaContent.Targets.SynergyCodexPopup,
            _ => null,
        };

        static bool IsUnder(string path, string root) =>
            path == root || path.StartsWith(root + "/", StringComparison.Ordinal);

        /// <summary>기능이 열리는 가장 이른 스테이지 번호 (클리어 기준). 정의 없음/조건 없음 = 0, 스테이지 조건이 없으면 튜토리얼 조건은 0, 그 밖은 알 수 없음</summary>
        static int StageOf(FeatureUnlockDef def)
        {
            if (def == null || def.conditions == null || def.conditions.Count == 0) return 0;
            int stage = int.MaxValue;
            bool tutorial = false;
            foreach (var condition in def.conditions)
            {
                if (condition == null) continue;
                if (condition.type == MetaConditionType.StageCleared) stage = (int)Math.Min(stage, Math.Max(0, condition.target));
                else if (condition.type == MetaConditionType.FtueStep || condition.type == MetaConditionType.FtueDone) tutorial = true;
            }
            return stage != int.MaxValue ? stage : tutorial ? 0 : int.MaxValue;
        }

        /// <summary>스테이지 번호 → "1-4"</summary>
        static string StageText(int stage) => stage <= 0 ? "처음" : $"{(stage - 1) / 10 + 1}-{(stage - 1) % 10 + 1}";

        static bool TryParseFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>MetaFlags의 공개 상수 이름들</summary>
        static readonly HashSet<string> KnownFlags = new(typeof(MetaFlags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()));

        // ═════════════════════════ 화면 대상 확인 (씬) ═════════════════════════

        /// <summary>
        /// 메인 씬을 열어 메타 콘텐츠가 가리키는 코치마크/손가락 대상(가이드 퀘스트 focusTarget, 미션 focusTarget,
        /// 튜토리얼 coachTarget)과 CoachTargets 상수가 Canvas 아래에 실제로 있는지 확인한다. 씬을 만든 뒤(BuildAll) 부른다.
        /// 없는 대상은 오류로 남긴다 (게임은 말풍선만 띄우고 계속되지만 손가락이 사라진다).
        /// </summary>
        /// <returns>찾지 못한 경로 목록</returns>
        public static List<string> ValidateTargetsInScene()
        {
            var missing = new List<string>();
            var db = AssetDatabase.LoadAssetAtPath<MetaDatabase>(MetaPath);
            if (db == null) { Debug.LogError("[Meta] MetaDatabase가 없습니다: " + MetaPath); return missing; }
            if (!File.Exists(IdleSceneBuilder.ScenePath)) { Debug.LogError("[Meta] 메인 씬이 없습니다: " + IdleSceneBuilder.ScenePath); return missing; }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return missing;
            var scene = EditorSceneManager.OpenScene(IdleSceneBuilder.ScenePath, OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Canvas");
            if (canvas == null) { Debug.LogError("[Meta] 씬에 Canvas가 없습니다"); return missing; }

            var paths = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Add(string path, string user)
            {
                if (string.IsNullOrEmpty(path)) return;
                paths[path] = paths.TryGetValue(path, out var users) ? users + ", " + user : user;
            }
            foreach (var field in typeof(CoachTargets).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string)) Add((string)field.GetRawConstantValue(), "CoachTargets." + field.Name);
            }
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++) Add(CoachTargets.DeckCard(i), $"CoachTargets.DeckCard({i})");
            foreach (var quest in db.guideQuests) Add(quest?.focusTarget, quest?.id);
            foreach (var mission in db.missions) Add(mission?.focusTarget, mission?.id);
            foreach (var step in db.ftueSteps) Add(step?.coachTarget, step?.id);
            // 4차 (SPEC4 §11.4 "새 안내 · 팝업 경로는 모두 씬에 있다"): 수선점 기록 · 첫 주 과제의 [가기] 대상 (이벤트 미션은 대상 칸이 없다)
            if (db.achievements != null)
            {
                foreach (var achievement in db.achievements) Add(achievement?.focusTarget, achievement?.id);
            }
            if (db.starterWeek?.tasks != null)
            {
                foreach (var task in db.starterWeek.tasks) Add(task?.focusTarget, task?.id);
            }

            foreach (var pair in paths)
            {
                if (canvas.transform.Find(pair.Key) == null)
                {
                    missing.Add(pair.Key);
                    Debug.LogError($"[Meta] 화면 대상 없음: {pair.Key} (쓰는 곳: {pair.Value})");
                }
            }
            if (missing.Count == 0) Debug.Log($"[Meta] 화면 대상 {paths.Count}개 모두 확인");
            return missing;
        }

        /// <summary>메뉴 / 배치(-executeMethod IdleGame.EditorTools.MetaDataBuilder.ValidateTargets) 진입점</summary>
        [MenuItem("IdleGame/Setup/메타 화면 대상 확인", priority = 23)]
        public static void ValidateTargets() => ValidateTargetsInScene();

        // ═════════════════════════ 보고 ═════════════════════════

        sealed class Report
        {
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
            /// <summary>알고 있는 빈칸 (아트 대기 등 — 경고가 아니라 한 줄 안내로 남긴다)</summary>
            public readonly List<string> Notes = new();
            public void Error(string message) => Errors.Add(message);
            public void Warn(string message) => Warnings.Add(message);
            public void Note(string message) => Notes.Add(message);
        }

        /// <summary>검사에 쓰는 콘텐츠 조회 (content가 null이어도 됨)</summary>
        sealed class Context
        {
            public readonly MetaDatabase Db;
            public readonly GameContent Content;
            public readonly ItemDatabase Items;
            public readonly CharacterDatabase Characters;
            public readonly GameConfig Config;
            public readonly List<SynergyDef> Synergies;
            public readonly HashSet<string> StarterLineages;
            public readonly int FormCount;
            public readonly int CostumeCount;
            public readonly List<EnemyDef> Enemies;

            public Context(MetaDatabase db, GameContent content)
            {
                Db = db;
                Content = content;
                Items = content?.Items;
                Characters = content?.Characters;
                Config = content?.Config;
                Synergies = content?.Synergies?.synergies;
                StarterLineages = new HashSet<string>(Config?.starterLineages ?? Array.Empty<string>());
                FormCount = Characters?.lineages?.Sum(l => l?.forms?.Count ?? 0) ?? 0;
                CostumeCount = Characters?.lineages?.Sum(l => l?.costumes?.Count ?? 0) ?? 0;
                Enemies = content?.Battle?.enemies;
            }
        }

        /// <summary>
        /// 시너지 조건(ActiveSynergies / SynergyTier) 가이드 퀘스트가 처음부터 충족되는지 어림한다 (SynergySystem.Evaluate와 같은 규칙).
        ///   · 시작 편성: 새 게임 AutoFormation은 시작 친구(GameConfig.starterLineages)를 모두 기본 형태로 세운다 → 이것만으로 충족이면 경고.
        ///   · + 첫 만남 친구: 튜토리얼 FirstMeetingSummon 친구를 더한 편성(튜토리얼 11단계에서 세움). 특정 시너지를 가리키지 않는
        ///     조건("시너지 N개", "아무 시너지 N단계")이 이것으로 충족이면 경고 — 특정 조합(예: 기다림의 끝)은 그 친구를 세우는 것이 목표라 괜찮다.
        ///   · 조합을 가리키면 필요한 계보를 확정으로 얻는 곳(시작 친구·첫 만남·출석부/가이드/개방/미션/튜토리얼 친구 보상)이 있는지
        ///     — 없으면 뽑기 운에 따라 순서대로 가는 가이드 줄이 멈춘다.
        /// 특성 = 그 특성을 가진 계보 수, 성향 = 기본 형태 성향 수, 조합 = lineageIds가 모두 있음. 칸 수·잠긴 덱 칸은 보지 않는다.
        /// </summary>
        sealed class StarterSynergyCheck
        {
            readonly Context _c;
            readonly Dictionary<string, int> _starter;
            readonly Dictionary<string, int> _withFirstMeeting;
            readonly string _firstMeeting;
            readonly HashSet<string> _guaranteed;

            public StarterSynergyCheck(Context c)
            {
                _c = c;
                if (c.Synergies == null || c.Characters == null || c.StarterLineages.Count == 0) return;
                _starter = Estimate(c.StarterLineages);
                _firstMeeting = c.Db.ftueSteps.FirstOrDefault(s => s != null && s.action == FtueAction.FirstMeetingSummon)?.actionArg;
                if (!string.IsNullOrEmpty(_firstMeeting) && !c.StarterLineages.Contains(_firstMeeting))
                    _withFirstMeeting = Estimate(new HashSet<string>(c.StarterLineages) { _firstMeeting });
                _guaranteed = GuaranteedLineages(c, _firstMeeting);
            }

            public void Check(string where, MetaCondition condition, Report r)
            {
                if (_starter == null || condition == null) return;
                if (condition.type != MetaConditionType.ActiveSynergies && condition.type != MetaConditionType.SynergyTier) return;

                if (IsMet(condition, _starter))
                {
                    r.Warn($"{where}: 시작 편성(시작 친구 {_c.StarterLineages.Count}명)만으로 이미 충족 — 줄이 나타나자마자 받기라 " +
                           "가르치는 것이 없음 (아직 켜지지 않은 조합을 가리킬 것)");
                    return;
                }
                bool generic = condition.type == MetaConditionType.ActiveSynergies || string.IsNullOrEmpty(condition.targetId);
                if (generic && _withFirstMeeting != null && IsMet(condition, _withFirstMeeting))
                    r.Warn($"{where}: 시작 편성 + 첫 만남 친구 '{_firstMeeting}'로 이미 충족 (튜토리얼 11단계 편성 직후) — 특정 조합을 가리킬 것");

                if (condition.type != MetaConditionType.SynergyTier || string.IsNullOrEmpty(condition.targetId)) return;
                var def = _c.Synergies.FirstOrDefault(s => s != null && s.id == condition.targetId);
                if (def == null || def.kind != SynergyKind.Combo || def.lineageIds == null) return;
                foreach (var lineageId in def.lineageIds.Where(x => !string.IsNullOrEmpty(x)).Distinct())
                {
                    if (!_guaranteed.Contains(lineageId))
                        r.Warn($"{where}: 조합 {def.id}에 필요한 '{lineageId}'는 확정으로 얻는 곳이 없음 (시작 친구·첫 만남·친구 보상) — " +
                               "뽑기 운에 따라 가이드 줄이 멈춤");
                }
            }

            static bool IsMet(MetaCondition condition, Dictionary<string, int> active)
            {
                if (condition.type == MetaConditionType.ActiveSynergies) return active.Count >= condition.target;
                if (string.IsNullOrEmpty(condition.targetId)) return active.Count > 0 && active.Values.Max() >= condition.target;
                return active.TryGetValue(condition.targetId, out int tier) && tier >= condition.target;
            }

            /// <summary>이 계보들을 모두 기본 형태로 세웠을 때 켜지는 시너지 (시너지 ID → 단계, 1부터 = SynergyStatus.TierIndex + 1)</summary>
            Dictionary<string, int> Estimate(HashSet<string> lineageIds)
            {
                var result = new Dictionary<string, int>();
                var characters = _c.Characters;
                var lineages = lineageIds.Select(id => characters.GetLineage(id)).Where(l => l != null).Distinct().ToList();
                foreach (var def in _c.Synergies)
                {
                    if (def == null || string.IsNullOrEmpty(def.id) || def.tiers == null || def.tiers.Count == 0) continue;
                    if (def.kind == SynergyKind.Combo)
                    {
                        var required = def.lineageIds?.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
                        if (required != null && required.Count > 0 && def.tiers[0] != null && required.All(lineageIds.Contains)) result[def.id] = 1;
                        continue;
                    }

                    int count = def.kind == SynergyKind.Trait
                        ? lineages.Count(l => l.traits != null && l.traits.Contains(def.trait))
                        : lineages.Count(l => characters.GetForm(l.baseFormId)?.style == def.style);
                    // SynergySystem.ResolveTier: count 이하인 단계 중 필요 수가 가장 큰 단계
                    int bestNeed = int.MinValue, tier = 0;
                    for (int i = 0; i < def.tiers.Count; i++)
                    {
                        if (def.tiers[i] == null) continue;
                        int need = Math.Max(1, def.tiers[i].count);
                        if (count >= need && need > bestNeed)
                        {
                            bestNeed = need;
                            tier = i + 1;
                        }
                    }
                    if (tier > 0) result[def.id] = tier;
                }
                return result;
            }

            /// <summary>확정으로 얻는 계보: 시작 친구 + 첫 만남 + 메타 보상 중 친구 확정(출석부·가이드·기능 개방·미션 상자·튜토리얼)</summary>
            static HashSet<string> GuaranteedLineages(Context c, string firstMeeting)
            {
                var result = new HashSet<string>(c.StarterLineages);
                if (!string.IsNullOrEmpty(firstMeeting)) result.Add(firstMeeting);
                void AddFrom(IEnumerable<MetaReward> rewards)
                {
                    if (rewards == null) return;
                    foreach (var reward in rewards)
                    {
                        if (reward != null && reward.type == MetaRewardType.Lineage && !string.IsNullOrEmpty(reward.itemId)) result.Add(reward.itemId);
                    }
                }
                foreach (var day in c.Db.attendance) AddFrom(day?.rewards);
                foreach (var quest in c.Db.guideQuests) AddFrom(quest?.rewards);
                foreach (var feature in c.Db.features) AddFrom(feature?.rewards);
                foreach (var chest in c.Db.missionChests) AddFrom(chest?.rewards);
                foreach (var mission in c.Db.missions) AddFrom(mission?.rewards);
                foreach (var step in c.Db.ftueSteps) AddFrom(step?.rewards);
                return result;
            }
        }

        /// <summary>한 줄 요약 (통과 로그에 붙인다)</summary>
        static string Summarize(MetaDatabase db)
        {
            static double GemsOf(IEnumerable<MetaReward> rewards) => rewards?
                .Where(x => x != null && x.type == MetaRewardType.Currency && x.currency == CurrencyType.Gem).Sum(x => x.amount) ?? 0;

            double questGems = db.guideQuests.Where(q => q != null).Sum(q => GemsOf(q.rewards));
            double dailyGems = db.GetMissionChests(MissionPeriod.Daily).Sum(x => GemsOf(x.rewards));
            double weeklyGems = db.GetMissionChests(MissionPeriod.Weekly).Sum(x => GemsOf(x.rewards));
            double starterGems = db.GetAttendance(AttendanceTrack.Starter).Sum(x => GemsOf(x.rewards));
            double monthlyGems = db.GetAttendance(AttendanceTrack.Monthly).Sum(x => GemsOf(x.rewards));
            int daily = db.GetMissions(MissionPeriod.Daily).Count, weekly = db.GetMissions(MissionPeriod.Weekly).Count;
            return $"기능 {db.features.Count} / 가이드 {db.guideQuests.Count}단계(별조각 {questGems:0}) / " +
                   $"일지 일일 {daily}·주간 {weekly}(상자 별조각 {dailyGems:0}/일, {weeklyGems:0}/주) / " +
                   $"출석 새싹 {db.GetAttendance(AttendanceTrack.Starter).Count}일(별조각 {starterGems:0})·정규 {db.GetAttendance(AttendanceTrack.Monthly).Count}일(별조각 {monthlyGems:0}) / " +
                   $"바구니 줄 {db.idleChest?.lines.Count ?? 0} / 복귀 문구 {db.returnLines.Count}계보 / 튜토리얼 {db.ftueSteps.Count}단계 / " +
                   $"권장 전투력 {db.power?.recommendedBase:0}×{db.power?.recommendedGrowth:0.###}^(s−1) / " +
                   $"4차: 기록 {db.achievements.Count}단계(별조각 {db.achievements.Where(a => a != null).Sum(a => Gems(a.rewards)):0}) · " +
                   $"마일스톤 {db.gachaMilestones.Count} · 호감도 {db.affinity?.lineages?.Count ?? 0}계보 · 동화책 {db.storyChapters.Count}장 · " +
                   $"챕터 상자 {db.lostChest?.chapterChests?.Count ?? 0}줄 · 이벤트 {db.events.Count} · 수첩 {db.season?.tiers?.Count ?? 0}칸 · " +
                   $"첫 주 {db.starterWeek?.tasks?.Count ?? 0}과제 · 작은 선물 {db.dailyGifts.Count}요일 · 보스 힌트 {db.bossHints.Count} · " +
                   $"축복 {db.blessings.Count} · 쿠폰 {db.coupons.Count} · 반딧불 하루 {db.firefly?.perDay ?? 0} · 입장권 모아 두기 {db.dungeonTicketBankCap}";
        }

        static void Log(Report r, string summary)
        {
            foreach (var note in r.Notes) Debug.Log("[Meta] (안내) " + note);
            foreach (var warning in r.Warnings) Debug.LogWarning("[Meta] " + warning);
            foreach (var error in r.Errors) Debug.LogError("[Meta] " + error);
            if (r.Errors.Count == 0) Debug.Log($"[Meta] 메타 데이터 검증 통과 (경고 {r.Warnings.Count}개){(summary != null ? " — " + summary : "")}");
            else Debug.LogError($"[Meta] 메타 데이터 검증 실패: 오류 {r.Errors.Count}개, 경고 {r.Warnings.Count}개");
        }

        // ═════════════════════════ 4차 콘텐츠 (SPEC4 §2.3 · §11.2 · 부록 A · B · C) ═════════════════════════

        // §11.2 예산 (별조각). 넘으면 오류 — EconomyBudgetTests(P4-1)와 같은 표
        internal const double BudgetAchievements = 3500;
        internal const double BudgetCodex = 2700;
        internal const double BudgetAffinity = 4000;
        internal const double BudgetStoryAndChapterChests1To5 = 900;
        internal const double BudgetLateChapterChestsPer10 = 200;
        internal const double BudgetSeason = 450;
        internal const double BudgetMonthlyEvent = 300;
        internal const double BudgetComeback = 300;
        internal const double BudgetGachaFirst200 = 150;
        internal const double BudgetBasketGemPerDay = 140;
        internal const double BudgetStarterWeek = 350;
        internal const double BudgetDailyGiftsPerWeek = 30;
        internal const double BudgetFirefliesPerWeek = 60;
        internal const double BudgetNightShopPerWeek = 40;

        /// <summary>문구에 쓰지 않는 말 (SPEC4 해요체 규칙: '습니다' 끝맺음 · '개방' · '하시겠' · '장비' — 장비는 범위 밖, CR-11)</summary>
        internal static readonly string[] BannedCopy = { "습니다", "개방", "하시겠", "장비" };

        /// <summary>보상 중 별조각 합</summary>
        static double Gems(IEnumerable<MetaReward> rewards) => rewards?
            .Where(x => x != null && x.type == MetaRewardType.Currency && x.currency == CurrencyType.Gem).Sum(x => x.amount) ?? 0;

        static void ValidateMeta4(Context c, Report r)
        {
            ValidateAchievements(c, r);
            ValidateCodex(c, r);
            ValidateGachaMilestones(c, r);
            ValidateAffinity(c, r);
            ValidateStoryAndChests(c, r);
            ValidateEvents(c, r);
            ValidateSeason(c, r);
            ValidateComeback(c, r);
            ValidateCoupons(c, r);
            ValidateBlessings(c, r);
            ValidateStarterWeek(c, r);
            ValidateDailyGifts(c, r);
            ValidateFirefly(c, r);
            ValidateSmallMeta4(c, r);
        }

        // ───────────── Q-04 수선점 기록 ─────────────

        static void ValidateAchievements(Context c, Report r)
        {
            var ids = new HashSet<string>();
            var lines = new Dictionary<string, List<AchievementDef>>();
            double gems = 0;
            foreach (var def in c.Db.achievements)
            {
                if (def == null) { r.Error("수선점 기록: 빈 항목"); continue; }
                if (string.IsNullOrEmpty(def.id)) { r.Error("수선점 기록: ID 없음"); continue; }
                string where = $"수선점 기록 {def.id}";
                if (!ids.Add(def.id)) r.Error($"{where}: ID 중복");
                if (string.IsNullOrEmpty(def.lineId)) r.Error($"{where}: 줄 ID 없음");
                else if (def.id != $"{def.lineId}_{def.tier}") r.Warn($"{where}: ID는 {{줄 ID}}_{{단계}} ({def.lineId}_{def.tier})");
                if (string.IsNullOrWhiteSpace(def.title)) r.Error($"{where}: 제목 없음");
                if (def.hidden && string.IsNullOrWhiteSpace(def.hint)) r.Error($"{where}: 숨은 기록에 힌트 없음");
                if (def.condition == null) r.Error($"{where}: 조건 없음");
                else CheckCondition(where, def.condition, c, r);
                if (def.rewards == null || def.rewards.Count == 0) r.Warn($"{where}: 보상 없음");
                CheckRewards(where, def.rewards, c, r);
                CheckFeature(where, "이동", def.navFeature, r);
                CheckPath(where, def.focusTarget, r);
                CheckNavFocus(where, def.navFeature, def.focusTarget, true, r);
                gems += Gems(def.rewards);
                string line = string.IsNullOrEmpty(def.lineId) ? def.id : def.lineId;
                if (!lines.TryGetValue(line, out var tiers)) lines[line] = tiers = new List<AchievementDef>();
                tiers.Add(def);
            }
            foreach (var pair in lines)
            {
                var tiers = pair.Value.OrderBy(d => d.tier).ToList();
                for (int i = 0; i < tiers.Count; i++)
                {
                    var tier = tiers[i];
                    if (tier.tier != i + 1) r.Error($"수선점 기록 {pair.Key}: 단계가 1부터 빠짐없이 이어지지 않음 ({tier.tier})");
                    if (tier.category != tiers[0].category) r.Warn($"수선점 기록 {pair.Key}: 단계마다 종류가 다름");
                    if (i == 0 || tier.condition == null || tiers[i - 1].condition == null) continue;
                    var previous = tiers[i - 1].condition;
                    if (tier.condition.type == MetaConditionType.Flag) continue; // 숨은 기록은 서로 따로인 플래그
                    if (tier.condition.type != previous.type || tier.condition.counter != previous.counter || tier.condition.targetId != previous.targetId)
                        r.Error($"수선점 기록 {pair.Key}: 단계마다 조건 종류가 다름");
                    else if (tier.condition.target <= previous.target) r.Error($"수선점 기록 {pair.Key}: {tier.tier}단계 목표가 앞 단계보다 크지 않음");
                }
            }
            if (gems > BudgetAchievements) r.Error($"수선점 기록: 별조각 합계 {gems:0} > 예산 {BudgetAchievements:0}");
        }

        // ───────────── Q-05 도감 ─────────────

        static void ValidateCodex(Context c, Report r)
        {
            var codex = c.Db.codex;
            if (codex == null) { r.Error("도감: 설정 없음"); return; }
            if (codex.formBaseGem < 0 || codex.formTier1Gem < 0 || codex.formTier2Gem < 0 || codex.costumeGem < 0 || codex.enemyGem < 0 ||
                codex.synergyFirstGem < 0 || codex.synergyMaxGem < 0 || codex.materialGem < 0) r.Error("도감: 처음 찾은 보상이 음수");
            CheckBonusTiers("도감 형태 보너스", codex.formTiers, c.FormCount, r);
            CheckBonusTiers("도감 시너지 보너스", codex.synergyTiers, c.Synergies?.Count ?? 0, r);
            if (codex.allEnemiesShopHpPercent < 0) r.Error("도감: 적 모음 보너스가 음수");

            // 칸 목록 (content가 있을 때만): 힌트 키가 실제 칸을 가리키는지, 평생 보상이 예산 안인지
            var keys = new HashSet<string>();
            double lifetime = 0;
            bool full = c.Characters != null && c.Enemies != null && c.Synergies != null && c.Items != null;
            if (c.Characters != null)
            {
                foreach (var lineage in c.Characters.lineages)
                {
                    if (lineage == null) continue;
                    foreach (var form in lineage.forms)
                    {
                        if (form == null) continue;
                        keys.Add(CodexSystem.Key(CodexCategory.Friend, form.id));
                        lifetime += form.tier <= 0 ? codex.formBaseGem : form.tier == 1 ? codex.formTier1Gem : codex.formTier2Gem;
                    }
                    foreach (var costume in lineage.costumes)
                    {
                        if (costume == null) continue;
                        keys.Add(CodexSystem.Key(CodexCategory.Costume, costume.id));
                        lifetime += codex.costumeGem;
                    }
                }
            }
            if (c.Enemies != null)
            {
                foreach (var enemy in c.Enemies)
                {
                    if (enemy == null) continue;
                    keys.Add(CodexSystem.Key(CodexCategory.Enemy, enemy.id));
                    lifetime += codex.enemyGem;
                }
            }
            if (c.Synergies != null)
            {
                foreach (var synergy in c.Synergies)
                {
                    if (synergy == null) continue;
                    keys.Add(CodexSystem.Key(CodexCategory.Synergy, synergy.id));
                    lifetime += codex.synergyFirstGem + codex.synergyMaxGem;
                }
            }
            if (c.Items != null)
            {
                foreach (var item in c.Items.items)
                {
                    if (item == null || item.category != ItemCategory.Material) continue;
                    keys.Add(CodexSystem.Key(CodexCategory.Material, item.id));
                    lifetime += codex.materialGem;
                }
            }
            foreach (var story in c.Db.storyChapters)
            {
                if (story != null) keys.Add(CodexSystem.Key(CodexCategory.Story, story.chapter.ToString(CultureInfo.InvariantCulture)));
            }
            if (full && lifetime > BudgetCodex) r.Error($"도감: 처음 찾은 보상 평생 {lifetime:0} > 예산 {BudgetCodex:0}");

            var seen = new HashSet<string>();
            foreach (var hint in codex.hints)
            {
                if (hint == null || string.IsNullOrEmpty(hint.key)) { r.Error("도감 힌트: 키 없음"); continue; }
                string where = $"도감 힌트 {hint.key}";
                if (!seen.Add(hint.key)) r.Error($"{where}: 중복");
                if (string.IsNullOrWhiteSpace(hint.hint)) r.Error($"{where}: 문구 없음");
                if (!KeyResolves(hint.key, keys, c)) r.Error($"{where}: 그런 도감 칸이 없음");
            }
            var enemyTexts = new HashSet<string>();
            foreach (var text in codex.enemyTexts)
            {
                if (text == null || string.IsNullOrEmpty(text.enemyId)) { r.Error("도감 적 문구: 적 ID 없음"); continue; }
                if (!enemyTexts.Add(text.enemyId)) r.Error($"도감 적 문구 {text.enemyId}: 중복");
                if (string.IsNullOrWhiteSpace(text.text)) r.Error($"도감 적 문구 {text.enemyId}: 문구 없음");
                if (c.Enemies != null && !c.Enemies.Any(e => e != null && e.id == text.enemyId)) r.Error($"도감 적 문구 {text.enemyId}: 모르는 적");
            }
            if (c.Enemies != null)
            {
                foreach (var enemy in c.Enemies)
                {
                    if (enemy != null && !enemyTexts.Contains(enemy.id)) r.Warn($"도감 적 문구: {enemy.id} 문구 없음");
                }
            }
        }

        /// <summary>힌트 키가 칸을 가리키는지 (그 종류의 목록을 모르면 머리만 본다)</summary>
        static bool KeyResolves(string key, HashSet<string> keys, Context c)
        {
            if (keys.Contains(key)) return true;
            foreach (CodexCategory category in Enum.GetValues(typeof(CodexCategory)))
            {
                string prefix = CodexSystem.KeyPrefix(category);
                if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                bool known = category switch
                {
                    CodexCategory.Friend or CodexCategory.Costume => c.Characters != null,
                    CodexCategory.Enemy => c.Enemies != null,
                    CodexCategory.Synergy => c.Synergies != null,
                    CodexCategory.Material => c.Items != null,
                    CodexCategory.Story => true,
                    _ => false,
                };
                return !known && key.Length > prefix.Length;
            }
            return false;
        }

        static void CheckBonusTiers(string where, List<CodexBonusTier> tiers, int maxCount, Report r)
        {
            if (tiers == null) return;
            int lastCount = 0;
            float lastPercent = 0f;
            foreach (var tier in tiers)
            {
                if (tier == null) { r.Error($"{where}: 빈 단계"); continue; }
                if (tier.count <= lastCount) r.Error($"{where}: 개수가 오름차순이 아님 ({tier.count})");
                if (!(tier.percent > lastPercent)) r.Error($"{where}: {tier.count}개 % {tier.percent}가 앞 단계보다 크지 않음 (단계 값은 합계)");
                if (maxCount > 0 && tier.count > maxCount) r.Error($"{where}: {tier.count}개 > 전체 {maxCount}개 (닿을 수 없음)");
                lastCount = tier.count;
                lastPercent = tier.percent;
            }
        }

        // ───────────── PRG-04 소환 마일스톤 ─────────────

        static void ValidateGachaMilestones(Context c, Report r)
        {
            int lastPulls = 0;
            double first200 = 0;
            foreach (var step in c.Db.gachaMilestones)
            {
                if (step == null) { r.Error("소환 마일스톤: 빈 단계"); continue; }
                string where = $"소환 마일스톤 {step.pulls}회";
                if (step.pulls <= lastPulls) r.Error($"{where}: 소환 수가 오름차순이 아님");
                if (step.rewards == null || step.rewards.Count == 0) r.Error($"{where}: 보상 없음");
                CheckRewards(where, step.rewards, c, r);
                if (step.pulls <= 200) first200 += Gems(step.rewards);
                lastPulls = Math.Max(lastPulls, step.pulls);
            }
            var repeat = c.Db.gachaRepeat;
            if (repeat != null && repeat.interval < 0) r.Error("소환 마일스톤 되풀이: 간격이 음수");
            if (repeat != null && repeat.interval > 0)
            {
                if (repeat.rewards == null || repeat.rewards.Count == 0) r.Error("소환 마일스톤 되풀이: 보상 없음");
                CheckRewards("소환 마일스톤 되풀이", repeat.rewards, c, r);
                for (int pulls = lastPulls + repeat.interval; pulls <= 200; pulls += repeat.interval) first200 += Gems(repeat.rewards);
            }
            if (first200 > BudgetGachaFirst200) r.Error($"소환 마일스톤: 처음 200회 별조각 {first200:0} > 예산 {BudgetGachaFirst200:0}");
        }

        // ───────────── CR-10 호감도 ─────────────

        static void ValidateAffinity(Context c, Report r)
        {
            var affinity = c.Db.affinity;
            if (affinity == null) { r.Error("호감도: 설정 없음"); return; }
            var thresholds = affinity.thresholds;
            if (thresholds == null || thresholds.Length < 2) { r.Error("호감도: 단계 경계가 2개보다 적음"); return; }
            if (thresholds[0] != 0) r.Error("호감도: Lv.1 경계는 0");
            for (int i = 1; i < thresholds.Length; i++)
            {
                if (thresholds[i] <= thresholds[i - 1]) r.Error($"호감도: Lv.{i + 1} 경계 {thresholds[i]}가 오름차순이 아님");
            }
            if (affinity.gemPerLevel < 0 || affinity.petAmount < 0 || affinity.petsPerDay < 0) r.Error("호감도: 보상 · 쓰다듬기 값이 음수");
            if (affinity.frameLevel < 1 || affinity.frameLevel > thresholds.Length) r.Error($"호감도: 테두리 단계 {affinity.frameLevel} (1~{thresholds.Length})");
            if (affinity.poseLevel < 1 || affinity.poseLevel > thresholds.Length) r.Error($"호감도: 몸짓 단계 {affinity.poseLevel} (1~{thresholds.Length})");

            int storyLines = thresholds.Length / 2; // 짝수 단계마다 한 줄 (Lv2/4/6/8/10)
            var seen = new HashSet<string>();
            foreach (var line in affinity.lineages)
            {
                if (line == null || string.IsNullOrEmpty(line.lineageId)) { r.Error("호감도 문구: 계보 ID 없음"); continue; }
                string where = $"호감도 문구 {line.lineageId}";
                if (!seen.Add(line.lineageId)) r.Error($"{where}: 중복");
                if (c.Characters != null && c.Characters.GetLineage(line.lineageId) == null) r.Error($"{where}: 모르는 계보");
                if (line.story == null || line.story.Length != storyLines || line.story.Any(string.IsNullOrWhiteSpace))
                    r.Error($"{where}: 기억 조각은 {storyLines}줄 (빈 줄 없이)");
                if (line.pet == null || line.pet.Length != 7 || line.pet.Any(string.IsNullOrWhiteSpace))
                    r.Error($"{where}: 쓰다듬기 말풍선은 7줄 (빈 줄 없이)");
            }
            if (c.Characters != null)
            {
                foreach (var lineage in c.Characters.lineages)
                {
                    if (lineage != null && !seen.Contains(lineage.id)) r.Warn($"호감도 문구: 계보 {lineage.id} 줄 없음");
                }
            }
            double perLineage = 0;
            for (int level = 2; level <= thresholds.Length; level++) perLineage += affinity.gemPerLevel * level;
            int lineageCount = c.Characters?.lineages?.Count ?? affinity.lineages.Count;
            if (perLineage * lineageCount > BudgetAffinity) r.Error($"호감도: 평생 별조각 {perLineage * lineageCount:0} > 예산 {BudgetAffinity:0}");
        }

        // ───────────── CR-05 동화책 · PRG-06 챕터 상자 · 잃어버린 상자 ─────────────

        static void ValidateStoryAndChests(Context c, Report r)
        {
            var chapters = new HashSet<int>();
            double storyGems = 0;
            int missingArt = 0;
            foreach (var story in c.Db.storyChapters)
            {
                if (story == null) { r.Error("동화책: 빈 장"); continue; }
                string where = $"동화책 {story.chapter}장";
                if (story.chapter < 1) r.Error($"{where}: 장 번호가 1 미만");
                if (!chapters.Add(story.chapter)) r.Error($"{where}: 중복");
                if (string.IsNullOrWhiteSpace(story.title) || string.IsNullOrWhiteSpace(story.place)) r.Error($"{where}: 제목 · 장소 없음");
                if (story.panels == null || story.panels.Count == 0) { r.Error($"{where}: 그림 칸 없음"); continue; }
                if (story.panels.Count != 3) r.Warn($"{where}: 그림 칸 {story.panels.Count}개 (기준 3개)");
                foreach (var panel in story.panels)
                {
                    if (panel == null || string.IsNullOrWhiteSpace(panel.text)) { r.Error($"{where}: 빈 칸 문구"); continue; }
                    if (panel.text.Length > MetaContent.StoryLineMaxLength) r.Error($"{where}: 한 줄이 {panel.text.Length}자 > {MetaContent.StoryLineMaxLength}자 '{panel.text}'");
                    if (panel.art == null) missingArt++;
                }
                CheckRewards(where, story.firstReadRewards, c, r);
                storyGems += Gems(story.firstReadRewards);
            }
            if (missingArt > 0) r.Note($"동화책 그림 {missingArt}칸이 아직 없어 글만 보여요 (Art/Story/ch{{n}}_{{1..3}} — P4-10 아트 대기)");

            var chest = c.Db.lostChest;
            if (chest == null) { r.Error("챕터 상자: 설정 없음"); return; }
            if (!(chest.dropChance >= 0f && chest.dropChance <= 1f)) r.Error($"잃어버린 상자: 확률 {chest.dropChance}이 0~1 밖");
            int lastTo = 0;
            foreach (var table in chest.tables)
            {
                if (table == null) { r.Error("잃어버린 상자: 빈 표"); continue; }
                string where = $"잃어버린 상자 {table.fromStage}~{table.toStage}";
                if (table.fromStage < 1 || table.toStage < table.fromStage) r.Error($"{where}: 스테이지 구간이 잘못됨");
                if (table.fromStage <= lastTo) r.Error($"{where}: 앞 표와 겹침");
                else if (table.fromStage != lastTo + 1) r.Warn($"{where}: 앞 표와 사이가 비어 있음 ({lastTo + 1}~{table.fromStage - 1}은 표 없음)");
                lastTo = Math.Max(lastTo, table.toStage);
                if (table.entries == null || table.entries.Count == 0) r.Error($"{where}: 칸 없음");
                else
                {
                    foreach (var entry in table.entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.itemId)) { r.Error($"{where}: 빈 칸"); continue; }
                        if (c.Items != null && c.Items.Get(entry.itemId) == null) r.Error($"{where}: 모르는 아이템 '{entry.itemId}'");
                        if (entry.min < 1 || entry.max < entry.min) r.Error($"{where} {entry.itemId}: 개수 {entry.min}~{entry.max}가 잘못됨");
                        if (entry.weight <= 0) r.Error($"{where} {entry.itemId}: 가중치가 0 이하");
                    }
                }
            }

            var chestChapters = new HashSet<int>();
            foreach (var row in chest.chapterChests)
            {
                if (row == null) { r.Error("챕터 상자: 빈 줄"); continue; }
                string where = row.chapter == 0 ? "챕터 상자 기본 줄" : $"챕터 상자 {row.chapter}장";
                if (row.chapter < 0) r.Error($"{where}: 장 번호가 음수");
                if (!chestChapters.Add(row.chapter)) r.Error($"{where}: 중복");
                if (row.rewards == null || row.rewards.Count == 0) r.Error($"{where}: 보상 없음");
                CheckRewards(where, row.rewards, c, r);
            }
            if (chest.chapterChests.Count > 0 && !chestChapters.Contains(0)) r.Warn("챕터 상자: 기본 줄(chapter 0)이 없어 줄 없는 장은 상자가 비어 있음");

            double earlyChests = 0;
            for (int chapter = 1; chapter <= 5; chapter++) earlyChests += Gems(c.Db.GetChapterChest(chapter)?.rewards);
            if (storyGems + earlyChests > BudgetStoryAndChapterChests1To5)
                r.Error($"동화책 + 챕터 상자 1~5장: 별조각 {storyGems + earlyChests:0} > 예산 {BudgetStoryAndChapterChests1To5:0}");
            // 6장부터: 이어진 열 장 어디에서든 ≤ 200 (CR4-01 — 장 속도가 빨라도 별조각이 장마다 따라 붙지 않게)
            int lastChapter = Math.Max(10, chest.chapterChests.Where(x => x != null).Select(x => x.chapter).DefaultIfEmpty(0).Max()) + 10;
            for (int from = 6; from + 9 <= lastChapter; from++)
            {
                double window = 0;
                for (int chapter = from; chapter < from + 10; chapter++) window += Gems(c.Db.GetChapterChest(chapter)?.rewards);
                if (window > BudgetLateChapterChestsPer10)
                {
                    r.Error($"챕터 상자 {from}~{from + 9}장: 별조각 {window:0} > 열 장 예산 {BudgetLateChapterChestsPer10:0}");
                    break;
                }
            }
        }

        // ───────────── EVT-01 이벤트 ─────────────

        static void ValidateEvents(Context c, Report r)
        {
            var ids = new HashSet<string>();
            var missionIds = new HashSet<string>();
            foreach (var def in c.Db.events)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) { r.Error("이벤트: ID 없음"); continue; }
                string where = $"이벤트 {def.id}";
                if (!ids.Add(def.id)) r.Error($"{where}: 중복");
                if (string.IsNullOrWhiteSpace(def.name) || string.IsNullOrWhiteSpace(def.shortDesc)) r.Error($"{where}: 이름 · 한 줄 설명 없음");
                if (!Enum.IsDefined(typeof(EventKind), def.kind)) r.Error($"{where}: 모르는 효과 {(int)def.kind}");
                switch (def.scheduleType)
                {
                    case EventScheduleType.Weekdays:
                        if (def.weekdayMask <= 0 || def.weekdayMask > 127) r.Error($"{where}: 요일 비트 {def.weekdayMask} (1~127)");
                        break;
                    case EventScheduleType.MonthDays:
                        if (def.fromDay < 1 || def.toDay > 31 || def.toDay < def.fromDay) r.Error($"{where}: 날 {def.fromDay}~{def.toDay}가 잘못됨");
                        break;
                    case EventScheduleType.DailyHours:
                        if (def.fromHour < 0 || def.fromHour > 23 || def.toHour < 0 || def.toHour > 23 || def.fromHour == def.toHour)
                            r.Error($"{where}: 시각 {def.fromHour}~{def.toHour}가 잘못됨");
                        break;
                    case EventScheduleType.Dates:
                        if (def.fromMonth < 1 || def.fromMonth > 12 || def.toMonth < 1 || def.toMonth > 12 || def.fromDay < 1 || def.fromDay > 31 || def.toDay < 1 || def.toDay > 31)
                            r.Error($"{where}: 날짜 {def.fromMonth}/{def.fromDay}~{def.toMonth}/{def.toDay}가 잘못됨");
                        break;
                    default:
                        r.Error($"{where}: 모르는 일정 {(int)def.scheduleType}");
                        break;
                }
                if (def.kind == EventKind.TicketBonus ? def.bonus <= 0 : !(def.multiplier > 1.0)) r.Error($"{where}: 효과 값이 없음 (배율 {def.multiplier} · 더하기 {def.bonus})");

                double gems = 0;
                int bonusCount = 0;
                foreach (var mission in def.missions)
                {
                    if (mission == null || string.IsNullOrEmpty(mission.id)) { r.Error($"{where}: 미션 ID 없음"); continue; }
                    string at = $"{where} 미션 {mission.id}";
                    if (!missionIds.Add(mission.id)) r.Error($"{at}: ID 중복 (이벤트를 통틀어)");
                    if (string.IsNullOrWhiteSpace(mission.text)) r.Error($"{at}: 문구 없음");
                    if (!Enum.IsDefined(typeof(CounterKey), mission.counter)) r.Error($"{at}: 모르는 카운터");
                    if (mission.target <= 0) r.Error($"{at}: 목표가 0 이하");
                    if (mission.rewards == null || mission.rewards.Count == 0) r.Error($"{at}: 보상 없음");
                    CheckRewards(at, mission.rewards, c, r);
                    if (mission.isBonus) bonusCount++;
                    gems += Gems(mission.rewards);
                }
                if (bonusCount > 1) r.Error($"{where}: 보너스 미션이 {bonusCount}개 (하나만)");
                var days = new HashSet<int>();
                foreach (var stamp in def.stamps)
                {
                    if (stamp == null) { r.Error($"{where}: 빈 도장"); continue; }
                    if (stamp.day < 1 || stamp.day > 7) r.Error($"{where} 도장 {stamp.day}일: 1~7일");
                    if (!days.Add(stamp.day)) r.Error($"{where} 도장 {stamp.day}일: 중복");
                    if (stamp.rewards == null || stamp.rewards.Count == 0) r.Error($"{where} 도장 {stamp.day}일: 보상 없음");
                    CheckRewards($"{where} 도장 {stamp.day}일", stamp.rewards, c, r);
                    gems += Gems(stamp.rewards);
                }
                // 한 회차 = 달마다(MonthDays) 한 번 — 회차 별조각이 달 예산 안
                if (def.scheduleType == EventScheduleType.MonthDays && gems > BudgetMonthlyEvent)
                    r.Error($"{where}: 한 회차 별조각 {gems:0} > 달 예산 {BudgetMonthlyEvent:0}");
                else if (def.scheduleType == EventScheduleType.Weekdays && gems > 0)
                    r.Warn($"{where}: 매주 오는 이벤트에 별조각 {gems:0} (예산 표에 없음)");

                // 밤의 수선점: 바구니 별조각 줄(천장) × 겹친 시간 × (배율 − 1) × 7일 ≤ 40
                if (def.kind == EventKind.NightBonus && def.scheduleType == EventScheduleType.DailyHours && c.Db.idleChest != null)
                {
                    int hours = (def.toHour - def.fromHour + 24) % 24;
                    foreach (var line in c.Db.idleChest.lines)
                    {
                        if (line?.reward == null || line.eventOnly || line.reward.type != MetaRewardType.Currency || line.reward.currency != CurrencyType.Gem) continue;
                        if (!(line.maxStageMultiplier > 0)) continue; // 천장이 없으면 바구니 검사가 오류를 남긴다
                        double week = line.reward.amount * line.maxStageMultiplier * hours * (def.multiplier - 1.0) * 7;
                        if (week > BudgetNightShopPerWeek + 1e-6) r.Error($"{where}: 바구니 별조각 줄 +{week:0.#}/주 > 예산 {BudgetNightShopPerWeek:0}");
                    }
                }
            }
        }

        // ───────────── Q-06 수첩 ─────────────

        static void ValidateSeason(Context c, Report r)
        {
            var season = c.Db.season;
            if (season == null) { r.Error("수첩: 설정 없음"); return; }
            if (season.days <= 0 || season.xpPerTier <= 0) r.Error("수첩: 날 수 · 칸 경험치가 0 이하");
            if (season.tiers == null || season.tiers.Count == 0) { r.Note("수첩: 칸 없음 (확장 항목 꺼짐)"); return; }
            double gems = 0;
            var sorted = season.tiers.Where(t => t != null).OrderBy(t => t.tier).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var tier = sorted[i];
                string where = $"수첩 {tier.tier}칸";
                if (tier.tier != i + 1) r.Error($"{where}: 칸이 1부터 빠짐없이 이어지지 않음");
                if (tier.rewards == null || tier.rewards.Count == 0) r.Error($"{where}: 보상 없음");
                CheckRewards(where, tier.rewards, c, r);
                CheckRewards($"{where} 대신 보상", tier.fallback, c, r);
                bool costume = tier.rewards != null && tier.rewards.Any(x => x != null && x.type == MetaRewardType.Costume);
                if (costume && (tier.fallback == null || tier.fallback.Count == 0)) r.Error($"{where}: 의상 칸에 대신 보상 없음 (가진 뒤 시즌)");
                gems += Math.Max(Gems(tier.rewards), tier.fallback != null && tier.fallback.Count > 0 ? Gems(tier.fallback) : 0);
            }
            if (sorted.Count != season.tiers.Count) r.Error("수첩: 빈 칸");
            if (gems > BudgetSeason) r.Error($"수첩: 시즌 별조각 {gems:0} > 예산 {BudgetSeason:0}");
        }

        // ───────────── CR-06 다시 만난 날 ─────────────

        static void ValidateComeback(Context c, Report r)
        {
            var comeback = c.Db.comeback;
            if (comeback == null) { r.Error("다시 만난 날: 설정 없음"); return; }
            if (comeback.minAwaySeconds <= 0 || comeback.cooldownDays < 0 || comeback.lifetimeMax < 0 || !(comeback.bonusCapHours > 0))
                r.Error("다시 만난 날: 시간 · 횟수 값이 잘못됨");
            double gems = 0;
            var sorted = comeback.days.Where(d => d != null).OrderBy(d => d.day).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                string where = $"다시 만난 날 {sorted[i].day}일";
                if (sorted[i].day != i + 1) r.Error($"{where}: 날이 1부터 빠짐없이 이어지지 않음");
                if (sorted[i].rewards == null || sorted[i].rewards.Count == 0) r.Error($"{where}: 보상 없음");
                CheckRewards(where, sorted[i].rewards, c, r);
                gems += Gems(sorted[i].rewards);
            }
            if (gems > BudgetComeback) r.Error($"다시 만난 날: 한 번 별조각 {gems:0} > 예산 {BudgetComeback:0}");
        }

        // ───────────── EVT-02 쿠폰 ─────────────

        static void ValidateCoupons(Context c, Report r)
        {
            if (c.Db.coupons.Count == 0) return;
            if (string.IsNullOrEmpty(c.Db.couponSalt)) r.Error("쿠폰: 소금(couponSalt) 없음");
            var hashes = new HashSet<string>();
            foreach (var coupon in c.Db.coupons)
            {
                if (coupon == null) { r.Error("쿠폰: 빈 항목"); continue; }
                string where = $"쿠폰 {(coupon.hash != null && coupon.hash.Length >= 8 ? coupon.hash.Substring(0, 8) : coupon.hash)}…";
                if (coupon.hash == null || coupon.hash.Length != 64 || coupon.hash.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
                    r.Error($"{where}: 해시는 소문자 16진수 64자 (번호를 그대로 넣지 말 것)");
                else if (!hashes.Add(coupon.hash)) r.Error($"{where}: 중복");
                if (!IsDayKeyOrEmpty(coupon.validFromDayKey) || !IsDayKeyOrEmpty(coupon.validToDayKey)) r.Error($"{where}: 기간 날 키는 yyyy-MM-dd");
                else if (!string.IsNullOrEmpty(coupon.validFromDayKey) && !string.IsNullOrEmpty(coupon.validToDayKey) &&
                         string.CompareOrdinal(coupon.validFromDayKey, coupon.validToDayKey) > 0) r.Error($"{where}: 기간 시작이 끝보다 늦음");
                if (coupon.rewards == null || coupon.rewards.Count == 0) r.Error($"{where}: 보상 없음");
                else
                {
                    foreach (var reward in coupon.rewards)
                    {
                        if (reward == null || !(reward.amount > 0)) { r.Error($"{where}: 빈 보상 · 양 0 이하"); continue; }
                        if (reward.type == RewardType.Item && (string.IsNullOrEmpty(reward.itemId) || c.Items != null && c.Items.Get(reward.itemId) == null))
                            r.Error($"{where}: 모르는 아이템 '{reward.itemId}'");
                    }
                }
                if (string.IsNullOrWhiteSpace(coupon.mailTitle)) r.Warn($"{where}: 우편 제목 없음 (기본 제목을 씀)");
            }
        }

        static bool IsDayKeyOrEmpty(string key) => string.IsNullOrEmpty(key) ||
            DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        // ───────────── PRG-07 축복 ─────────────

        static void ValidateBlessings(Context c, Report r)
        {
            var ids = new HashSet<string>();
            foreach (var def in c.Db.blessings)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) { r.Error("축복: ID 없음"); continue; }
                string where = $"축복 {def.id}";
                if (!ids.Add(def.id)) r.Error($"{where}: 중복");
                if (string.IsNullOrWhiteSpace(def.name) || string.IsNullOrWhiteSpace(def.desc)) r.Error($"{where}: 이름 · 설명 없음");
                if (!Enum.IsDefined(typeof(StatType), def.stat)) r.Error($"{where}: 모르는 능력치 {(int)def.stat}");
                if (!(def.value > 0)) r.Error($"{where}: 값이 0 이하");
                if (def.maxStacks < 1) r.Error($"{where}: 최대 횟수가 1 미만");
                if (string.IsNullOrEmpty(def.iconKey) || ArtLib.Icon(def.iconKey) == null) r.Warn($"{where}: 아이콘 Icons/{def.iconKey} 없음");
            }
            if (c.Db.blessings.Count > 0 && c.Db.blessings.Count < 3) r.Error("축복: 셋 중 하나를 고르는데 축복이 3개보다 적음");
        }

        // ───────────── NEW-01 첫 주 ─────────────

        static void ValidateStarterWeek(Context c, Report r)
        {
            var week = c.Db.starterWeek;
            if (week == null) { r.Error("첫 주: 설정 없음"); return; }
            if (week.tasks.Count == 0) return;
            if (week.windowDays < 7 || week.maxInstallDays < 1) r.Error("첫 주: 창 날 수는 7 이상, 설치 일수는 1 이상");
            var ids = new HashSet<string>();
            var perDay = new int[8];
            int points = 0;
            foreach (var task in week.tasks)
            {
                if (task == null || string.IsNullOrEmpty(task.id)) { r.Error("첫 주: 과제 ID 없음"); continue; }
                string where = $"첫 주 {task.id}";
                if (!ids.Add(task.id)) r.Error($"{where}: 중복");
                if (task.day < 1 || task.day > 7) { r.Error($"{where}: {task.day}일째 (1~7)"); continue; }
                perDay[task.day]++;
                if (string.IsNullOrWhiteSpace(task.text)) r.Error($"{where}: 문구 없음");
                if (task.points <= 0) r.Error($"{where}: 점수가 0 이하");
                points += Math.Max(0, task.points);
                if (task.condition == null) r.Error($"{where}: 조건 없음");
                else CheckCondition(where, task.condition, c, r);
                CheckFeature(where, "잠금", task.requiredFeature, r);
                CheckFeature(where, "이동", task.navFeature, r);
                CheckPath(where, task.focusTarget, r);
                CheckNavFocus(where, task.navFeature, task.focusTarget, true, r);
            }
            for (int day = 1; day <= 7; day++)
            {
                if (perDay[day] != 4) r.Warn($"첫 주 {day}일째: 과제 {perDay[day]}개 (기준 4개)");
            }
            int lastPoints = 0;
            double gems = 0;
            foreach (var chest in week.chests)
            {
                if (chest == null) { r.Error("첫 주: 빈 상자"); continue; }
                if (chest.points <= lastPoints) r.Error($"첫 주 상자 {chest.points}점: 점수가 오름차순이 아님");
                if (chest.points > points) r.Error($"첫 주 상자 {chest.points}점: 과제 점수 합 {points}점으로 닿을 수 없음");
                if (chest.rewards == null || chest.rewards.Count == 0) r.Error($"첫 주 상자 {chest.points}점: 보상 없음");
                CheckRewards($"첫 주 상자 {chest.points}점", chest.rewards, c, r);
                gems += Gems(chest.rewards);
                lastPoints = Math.Max(lastPoints, chest.points);
            }
            if (gems > BudgetStarterWeek) r.Error($"첫 주: 별조각 {gems:0} > 예산 {BudgetStarterWeek:0}");
        }

        // ───────────── NEW-02 오늘의 작은 선물 ─────────────

        static void ValidateDailyGifts(Context c, Report r)
        {
            if (c.Db.dailyGifts.Count == 0) return;
            var days = new HashSet<int>();
            double gems = 0;
            foreach (var gift in c.Db.dailyGifts)
            {
                if (gift == null) { r.Error("작은 선물: 빈 줄"); continue; }
                string where = $"작은 선물 요일 {gift.weekday}";
                if (gift.weekday < 0 || gift.weekday > 6) r.Error($"{where}: 요일은 0(월)~6(일)");
                if (!days.Add(gift.weekday)) r.Error($"{where}: 중복");
                if (gift.rewards == null || gift.rewards.Count == 0) r.Error($"{where}: 보상 없음");
                CheckRewards(where, gift.rewards, c, r);
                gems += Gems(gift.rewards);
            }
            if (days.Count != 7) r.Warn($"작은 선물: 요일 {days.Count}개 (기준 7개 — 빠진 요일은 선물 없음)");
            if (gems > BudgetDailyGiftsPerWeek) r.Error($"작은 선물: 주당 별조각 {gems:0} > 예산 {BudgetDailyGiftsPerWeek:0}");
        }

        // ───────────── NEW-03 반딧불 ─────────────

        static void ValidateFirefly(Context c, Report r)
        {
            var firefly = c.Db.firefly;
            if (firefly == null) { r.Error("반딧불: 설정 없음"); return; }
            if (!(firefly.minInterval > 0) || firefly.maxInterval < firefly.minInterval || !(firefly.lifeSeconds > 0) || firefly.perDay < 0)
                r.Error("반딧불: 간격 · 머무는 시간 · 하루 수가 잘못됨");
            double weights = 0, gemPerCatch = 0;
            foreach (var entry in firefly.rewards)
            {
                if (entry == null || entry.weight <= 0) { r.Error("반딧불: 빈 보상 · 가중치 0 이하"); continue; }
                weights += entry.weight;
                if (entry.reward != null) CheckReward($"반딧불 보상", entry.reward, c, r);
                if (entry.fromLostChestTable && (c.Db.lostChest?.tables == null || c.Db.lostChest.tables.Count == 0))
                    r.Warn("반딧불: 잃어버린 상자 표가 없어 '표에서 재료 하나'는 대신 보상을 줌");
                if (!entry.fromLostChestTable && entry.reward != null) gemPerCatch += entry.weight * Gems(new[] { entry.reward });
            }
            if (firefly.perDay > 0 && firefly.rewards.Count == 0) r.Error("반딧불: 보상 없음");
            double week = weights > 0 ? firefly.perDay * 7 * gemPerCatch / weights : 0;
            if (week > BudgetFirefliesPerWeek + 1e-6) r.Error($"반딧불: 주당 별조각 {week:0.#} > 예산 {BudgetFirefliesPerWeek:0}");
        }

        // ───────────── 응원 · 보스 힌트 · 백업 알림 · 입장권 ─────────────

        static void ValidateSmallMeta4(Context c, Report r)
        {
            var cheer = c.Db.cheer;
            if (cheer == null || !(cheer.attackPercent > 0) || cheer.attackPercent > 100 || cheer.durationSeconds <= 0) r.Error("응원: 공격력 % · 시간이 잘못됨");

            var bosses = new HashSet<string>();
            foreach (var hint in c.Db.bossHints)
            {
                if (hint == null || string.IsNullOrEmpty(hint.enemyId)) { r.Error("보스 힌트: 적 ID 없음"); continue; }
                string where = $"보스 힌트 {hint.enemyId}";
                if (!bosses.Add(hint.enemyId)) r.Error($"{where}: 중복");
                if (string.IsNullOrWhiteSpace(hint.hint)) r.Error($"{where}: 문구 없음");
                var enemy = c.Enemies?.FirstOrDefault(e => e != null && e.id == hint.enemyId);
                if (c.Enemies != null && enemy == null) r.Error($"{where}: 모르는 적");
                else if (enemy != null && !enemy.isBoss) r.Warn($"{where}: 보스가 아님");
            }

            var backup = c.Db.backupReminder;
            if (backup == null) r.Error("백업 알림: 설정 없음");
            else if (backup.days < 0) r.Error("백업 알림: 날 수가 음수 (0 = 끔)");
            else if (backup.days > 0 && (string.IsNullOrWhiteSpace(backup.mailTitle) || string.IsNullOrWhiteSpace(backup.mailBody)))
                r.Error("백업 알림: 우편 제목 · 본문 없음");

            if (c.Db.dungeonTicketBankCap < 0) r.Error("입장권 모아 두기: 상한이 음수 (0 = 끔)");
        }

        // ───────────── 문구 (해요체 · 금지어) ─────────────

        /// <summary>모든 메타 문구에서 금지어를 찾는다 (MetaDatabase의 문자열 필드를 모두 훑는다 — 그림 · 에셋 참조는 건너뛴다)</summary>
        static void ValidateCopy(Context c, Report r)
        {
            var found = new List<string>();
            CollectBannedCopy(c.Db, "MetaDatabase", found, 0);
            foreach (var message in found) r.Error(message);
        }

        static void CollectBannedCopy(object value, string path, List<string> found, int depth)
        {
            if (value == null || depth > 8 || found.Count > 50) return;
            if (value is string text)
            {
                foreach (var word in BannedCopy)
                {
                    if (text.Contains(word)) found.Add($"문구 {path}: '{word}' (해요체 규칙) — '{text}'");
                }
                return;
            }
            if (value is UnityEngine.Object && depth > 0) return; // Sprite 같은 에셋 참조
            if (value is System.Collections.IEnumerable list)
            {
                int index = 0;
                foreach (var item in list) CollectBannedCopy(item, $"{path}[{index++}]", found, depth + 1);
                return;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.Namespace != typeof(MetaDatabase).Namespace) return;
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                CollectBannedCopy(field.GetValue(value), $"{path}.{field.Name}", found, depth + 1);
            }
        }

        // ═════════════════════════ EVT-02 쿠폰 해시 (번호는 저장하지 않는다) ═════════════════════════

        /// <summary>
        /// 쿠폰 번호 다듬기 = 런타임 CouponSystem.Normalize 그대로 (규칙이 한 곳에만 있게): 앞뒤 공백 빼기 → 가운데 공백 · '-' 지우기 →
        /// ASCII 소문자만 대문자로 → ^[A-Z0-9]{6,16}$. 맞지 않으면 null
        /// </summary>
        public static string NormalizeCoupon(string input) => CouponSystem.Normalize(input);

        /// <summary>다듬은 쿠폰 번호 길이 (CouponSystem.MinLength · MaxLength)</summary>
        public const int CouponMinLength = CouponSystem.MinLength, CouponMaxLength = CouponSystem.MaxLength;

        /// <summary>쿠폰 해시 = 런타임 CouponSystem.Hash 그대로: 소문자 16진수 SHA256(UTF8(소금 + ":" + 다듬은 번호))</summary>
        public static string CouponHash(string salt, string normalizedCode) => CouponSystem.Hash(salt, normalizedCode);

        /// <summary>쿠폰 해시 만들기 창 — 입력한 번호는 저장하지 않고(직렬화 안 함) 해시만 보여 주고 클립보드에 넣는다</summary>
        [MenuItem("IdleGame/쿠폰 해시 만들기", priority = 41)]
        public static void OpenCouponHashWindow()
        {
            var window = EditorWindow.GetWindow<CouponHashWindow>(true, "쿠폰 해시 만들기", true);
            window.minSize = new Vector2(520, 200);
        }
    }

    /// <summary>
    /// 쿠폰 해시 만들기 (EVT-02, SPEC4 §4.5). 번호 칸은 [NonSerialized]라 창 배치 저장에도 남지 않고, 해시를 만들면 바로 지운다.
    /// 소금은 MetaContent.CouponSalt. 만든 해시를 MetaContent.Coupons.cs에 붙여 넣는다 (번호는 저장소에 넣지 않는다).
    /// </summary>
    internal sealed class CouponHashWindow : EditorWindow
    {
        [NonSerialized] string _code = "";
        [NonSerialized] string _hash = "";
        [NonSerialized] string _message = "";

        void OnGUI()
        {
            EditorGUILayout.HelpBox("쿠폰 번호는 어디에도 저장하지 않아요. 해시만 MetaContent.Coupons.cs에 붙여 넣어요.", MessageType.Info);
            _code = EditorGUILayout.PasswordField("쿠폰 번호", _code ?? "");
            if (GUILayout.Button("해시 만들기"))
            {
                string code = MetaDataBuilder.NormalizeCoupon(_code);
                _code = "";
                if (code == null)
                {
                    _hash = "";
                    _message = "번호는 영문 대문자 · 숫자 6~16자예요 (공백과 '-'는 빼요).";
                }
                else
                {
                    _hash = MetaDataBuilder.CouponHash(MetaContent.CouponSalt, code);
                    EditorGUIUtility.systemCopyBuffer = _hash;
                    _message = "해시를 클립보드에 넣었어요.";
                    Debug.Log("[Coupon] 해시 " + _hash);
                }
                GUI.FocusControl(null);
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.LabelField(_message);
            if (!string.IsNullOrEmpty(_hash)) EditorGUILayout.SelectableLabel(_hash, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }
}
