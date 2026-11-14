using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Q-04 수선점 기록 (SPEC4 §6.1 · 부록 A.1): 17줄 62단계, 별조각 합계 3,400 (예산 ≤ 3,500, §11.2).
    ///   · 단계 하나 = AchievementDef 하나 (id = {lineId}_{tier}, tier 1부터). 화면은 줄마다 가장 낮은 안 받은 단계를 보여 준다.
    ///   · 진행은 설치 때부터 센다 (평생 카운터 · 상태 조건) — 2-3에 기록이 열리면 앞 단계가 한꺼번에 받을 수 있게 된다 (의도된 첫 방문 선물).
    ///   · 숨은 기록 5개(ach_hidden)는 플래그 조건이다. 플래그는 AchievementSystem(P4-1)이 전투 · 메타 사건에서 세운다 (<see cref="HiddenFlagPrefix"/>).
    ///     한 줄에 다섯 단계로 두었지만(17줄) 플래그는 서로 따로라, 어느 것을 먼저 찾아도 받을 수 있어야 한다 (P4-1 규칙).
    ///     title = 찾은 뒤 이름, hint = 찾기 전 힌트 ('???'는 화면이 그린다), doneText = 찾은 뒤 설명 (아래 HiddenDoneText 표에서 채운다).
    ///   · 줄 아이콘은 표 A.1 (없으면 다음 후보). [가기]는 그 행동을 하는 화면으로 (전투는 이동 없음).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>숨은 기록 플래그 머리 (PlayerData.flags "ach.hidden.*" — AchievementSystem이 세운다, MetaDataBuilder가 알고 있는 플래그로 본다)</summary>
        internal const string HiddenFlagPrefix = "ach.hidden.";

        /// <summary>h1 비 오는 스테이지를 우두커니를 지킴이로 세운 채 클리어</summary>
        internal const string HiddenRainUdu = AchievementSystem.FlagRainUdu;
        /// <summary>h2 수선점이 한 번도 다치지 않고 챕터 보스(x-10) 클리어</summary>
        internal const string HiddenCleanBoss = AchievementSystem.FlagCleanBoss;
        /// <summary>h3 한 스테이지에서 불 밝히기 3번</summary>
        internal const string HiddenThreeLamps = AchievementSystem.FlagThreeLamps;
        /// <summary>h4 짝짝이 형태 11종을 모두 해금</summary>
        internal const string HiddenAllSocks = AchievementSystem.FlagAllSocks;
        /// <summary>h5 자정 ~ 새벽 5시(현지)에 출석 도장</summary>
        internal const string HiddenMidnight = AchievementSystem.FlagMidnight;

        /// <summary>숨은 기록을 찾은 뒤 보여 줄 한 줄 (플래그 → 설명, 부록 A.1 "Done descriptions" → AchievementDef.doneText)</summary>
        internal static readonly IReadOnlyDictionary<string, string> HiddenDoneText = new Dictionary<string, string>
        {
            { HiddenRainUdu, "비 오는 스테이지에서 레이니가 지킨 채 클리어했어요" },
            { HiddenCleanBoss, "수선점이 한 번도 다치지 않고 챕터 보스를 물리쳤어요" },
            { HiddenThreeLamps, "한 스테이지에서 불을 세 번 밝혔어요" },
            { HiddenAllSocks, "짝짝이의 형태 11종을 모두 해금했어요" },
            { HiddenMidnight, "자정이 넘은 밤에 출석 도장을 찍었어요" },
        };

        static partial void FillAchievements(MetaDatabase db, GameContent content)
        {
            var list = db.achievements;

            AddLine(list, "ach_stage", AchievementCategory.Stage, Pick(ArtLib.Icon("quest"), StageIcon()), FeatureId.Battle, null,
                n => MetaCondition.Of(MetaConditionType.StageCleared, n), n => $"스테이지 {StageText((int)n)} 클리어",
                (MetaCondition.StageIndex(1, 10), 20), (MetaCondition.StageIndex(2, 10), 40), (MetaCondition.StageIndex(3, 10), 60),
                (MetaCondition.StageIndex(5, 10), 80), (MetaCondition.StageIndex(10, 10), 100));

            AddLine(list, "ach_kill", AchievementCategory.Kill, Pick(ArtLib.Enemy("enemy_dust"), ArtLib.Icon("guardian")), FeatureId.Battle, null,
                n => MetaCondition.Count(CounterKey.EnemyKill, n), n => $"적 {Num(n)}마리 물리치기",
                (100, 10), (1000, 30), (10000, 60), (100000, 100));

            AddLine(list, "ach_lamp", AchievementCategory.Lamp, ArtLib.Icon("lamp"), FeatureId.Battle, null,
                n => MetaCondition.Count(CounterKey.LampLit, n), n => $"불 밝히기 {Num(n)}번",
                (10, 20), (100, 50), (1000, 100));

            AddLine(list, "ach_echo", AchievementCategory.Echo, ArtLib.Icon("echo"), FeatureId.Battle, null,
                n => MetaCondition.Count(CounterKey.EchoSummon, n), n => $"잔향이 {Num(n)}번 부르기",
                (100, 20), (1000, 50), (10000, 100));

            AddLine(list, "ach_friend", AchievementCategory.Friend, ArtLib.Icon("guardian"), FeatureId.Gacha, Targets.GachaPull1,
                n => MetaCondition.Of(MetaConditionType.OwnedLineages, n), n => $"친구 {n}명 만나기",
                (4, 10), (5, 30), (6, 60), (7, 100));

            // [가기] 진화는 진화할 수 있는 계보의 친구 상세(기억도 탭)를 연다 — 손가락은 두지 않는다 (팝업 밖 대상은 어둠 아래)
            AddLine(list, "ach_form", AchievementCategory.Form, ArtLib.Icon("shards"), FeatureId.Evolution, null,
                n => MetaCondition.Of(MetaConditionType.UnlockedForms, n), n => $"형태 {n}종 해금",
                (5, 20), (10, 40), (20, 70), (29, 100));

            AddLine(list, "ach_expedition", AchievementCategory.Expedition, ArtLib.UI("tab_expedition"), FeatureId.Expedition, Targets.ExpeditionZoneList,
                n => MetaCondition.Count(CounterKey.ExpeditionClaim, n), n => $"원정 {Num(n)}번 다녀오기",
                (10, 20), (50, 50), (200, 100));

            AddLine(list, "ach_dungeon", AchievementCategory.Dungeon, ArtLib.Icon("echo"), FeatureId.Dungeon, null,
                n => MetaCondition.Of(MetaConditionType.DungeonFloor, n), n => $"잔향이 던전 {n}층 돌파",
                (5, 20), (10, 40), (20, 70), (30, 100));

            AddLine(list, "ach_gacha", AchievementCategory.Gacha, ArtLib.UI("tab_gacha"), FeatureId.Gacha, Targets.GachaPull1,
                n => MetaCondition.Count(CounterKey.GachaPull, n), n => $"잃어버린 친구 {Num(n)}번 찾기",
                (10, 10), (50, 30), (100, 60), (300, 100));

            AddLine(list, "ach_synergy", AchievementCategory.Synergy, ArtLib.Icon("resonance"), FeatureId.Formation, Targets.FormationSynergy,
                n => MetaCondition.Of(MetaConditionType.SynergyKindsSeen, n), n => $"시너지 {n}종 켜 보기",
                (5, 10), (10, 30), (20, 70), (25, 100));

            AddLine(list, "ach_shop", AchievementCategory.ShopLevel, ArtLib.UI("tab_growth"), FeatureId.ShopUpgrade, Targets.GrowthLevelUp1,
                n => MetaCondition.Of(MetaConditionType.ShopLevel, n), n => $"수선점 Lv.{n}",
                (10, 10), (50, 30), (100, 60), (200, 100));

            AddLine(list, "ach_levelsum", AchievementCategory.LevelSum, ArtLib.Icon("star"), FeatureId.ShopUpgrade, Targets.GrowthLineageList,
                n => MetaCondition.Of(MetaConditionType.LineageLevelSum, n), n => $"친구 레벨 합계 {n}",
                (50, 20), (200, 50), (500, 100));

            // 기록은 수선 일지 팝업 안에 있어 일지 이동은 두지 않는다 (같은 팝업)
            AddLine(list, "ach_journal", AchievementCategory.Journal, ArtLib.Icon("journal"), FeatureId.Battle, null,
                n => MetaCondition.Count(CounterKey.JournalComplete, n), n => $"수선 일지 100점 {n}일",
                (7, 20), (30, 50), (100, 100));

            AddLine(list, "ach_attend", AchievementCategory.Attendance, ArtLib.Icon("attendance"), FeatureId.Attendance, null,
                n => MetaCondition.Count(CounterKey.AttendanceCheckIn, n), n => $"출석 도장 {n}일",
                (7, 20), (28, 50), (100, 100));

            AddLine(list, "ach_costume", AchievementCategory.Costume, Pick(ArtLib.Costume("jjak_cheer"), ArtLib.Icon("star")), FeatureId.Costumes, null,
                n => MetaCondition.Of(MetaConditionType.CostumesOwned, n), n => $"의상 {n}벌 모으기",
                (3, 20), (6, 50), (10, 100));

            AddLine(list, "ach_affinity", AchievementCategory.Affinity, ArtLib.Icon("affinity"), FeatureId.Characters, Targets.CharacterGrid,
                n => MetaCondition.Of(MetaConditionType.AffinityLevelCount, n, AffinityMaxLevelId), n => $"호감도 Lv.10 친구 {n}명",
                (1, 30), (3, 60), (7, 100));

            // ── 숨은 기록 (A.1 Hidden): 찾기 전 '???' + 힌트, 찾은 뒤 이름 ──
            var hiddenIcon = UiArt.Medal(UiArt.MedalKind.Hidden);
            AddHidden(list, 1, HiddenRainUdu, "비 오는 날의 쓸모", "비 오는 날, 누군가 쓸모를 찾을지도 몰라요", hiddenIcon);
            AddHidden(list, 2, HiddenCleanBoss, "무사한 밤", "보스가 문 앞까지 오지 못하게 지켜 봐요", hiddenIcon);
            AddHidden(list, 3, HiddenThreeLamps, "불빛 세 번", "한 판에 불을 여러 번 밝혀 볼까요?", hiddenIcon);
            AddHidden(list, 4, HiddenAllSocks, "한 짝을 찾아서", "짝짝이의 모든 모습을 만나 봐요", hiddenIcon);
            AddHidden(list, 5, HiddenMidnight, "늦은 밤의 손님", "모두가 잠든 시간에 들러 볼까요?", hiddenIcon);
        }

        /// <summary>호감도 최고 단계 (AffinityLevelCount의 targetId — "Lv.10인 계보 수")</summary>
        const string AffinityMaxLevelId = "10";

        /// <summary>숨은 기록 별조각 (단계마다)</summary>
        const int HiddenRecordGem = 50;

        /// <summary>한 줄의 단계들 (target 오름차순, 별조각 보상 하나씩)</summary>
        static void AddLine(List<AchievementDef> list, string lineId, AchievementCategory category, Sprite icon, FeatureId nav, string focus,
            Func<long, MetaCondition> condition, Func<long, string> title, params (long target, int gem)[] tiers)
        {
            var lineIcon = Pick(icon, ArtLib.Icon("journal"), ArtLib.Icon("star"));
            for (int i = 0; i < tiers.Length; i++)
            {
                list.Add(new AchievementDef
                {
                    id = $"{lineId}_{i + 1}",
                    lineId = lineId,
                    tier = i + 1,
                    category = category,
                    title = title(tiers[i].target),
                    condition = condition(tiers[i].target),
                    rewards = Rewards(Gem(tiers[i].gem)),
                    navFeature = nav,
                    focusTarget = focus,
                    icon = lineIcon,
                });
            }
        }

        static void AddHidden(List<AchievementDef> list, int tier, string flag, string title, string hint, Sprite icon) =>
            list.Add(new AchievementDef
            {
                id = $"ach_hidden_{tier}",
                lineId = "ach_hidden",
                tier = tier,
                category = AchievementCategory.Hidden,
                title = title,
                condition = MetaCondition.Flag(flag),
                rewards = Rewards(Gem(HiddenRecordGem)),
                hidden = true,
                hint = hint,
                doneText = HiddenDoneTextOf(flag),
                navFeature = FeatureId.Battle,
                icon = Pick(icon, ArtLib.Icon("lock"), ArtLib.Icon("journal")),
            });

        /// <summary>그 숨은 기록 플래그의 찾은 뒤 설명 (표에 없으면 null = 보이지 않음)</summary>
        static string HiddenDoneTextOf(string flag) =>
            !string.IsNullOrEmpty(flag) && HiddenDoneText.TryGetValue(flag, out var text) ? text : null;

        /// <summary>스테이지 번호 → "3-10"</summary>
        static string StageText(int stage) => StageLabel((stage - 1) / 10 + 1, (stage - 1) % 10 + 1);

        /// <summary>천 단위 쉼표 (100,000)</summary>
        static string Num(long n) => n.ToString("#,0", CultureInfo.InvariantCulture);
    }
}
