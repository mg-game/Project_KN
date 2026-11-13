using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    /// <summary>
    /// 4차 메타 콘텐츠 (RM-2 P4-8, SPEC4 부록 A · B · C · §2.8 · §11.2): 코드 원본 MetaContent.Apply(db, content, allStretch)가 채운 MetaDatabase를 본다.
    ///   · 기능 행 (모든 FeatureId에 행 — MetaDataBuilder 경고 두 개 정리, S5-4 카드 문구, 4차 기능 카드는 CardsReady만, 4차 행에는 앱 시간 바닥 없음)
    ///   · 수선점 기록 17줄 62단계 3,400 · 도감 설정 · 힌트 키 · 소환 마일스톤 · 호감도 · 동화책 · 챕터 상자(6장부터 5장마다 별조각) ·
    ///     이벤트 넷 · 수첩 · 다시 만난 날 · 쿠폰(해시만) · 축복 · 첫 주 · 작은 선물 · 반딧불 · 응원 · 보스 힌트 · 백업 알림 · 입장권 모아 두기
    ///   · 확장 항목은 끈 채로 싣는다 (수첩 칸 · 축복 비움, 잃어버린 상자 0, 반딧불 0, 입장권 0) — 켠 내용(allStretch)도 예산 안
    ///   · 바구니 별조각 줄 천장 1.5 + 16시간 단계, 전투력 구간 151, 해요체 금지어, MetaDataBuilder.Validate 오류 · 경고 0
    /// 에디터 어셈블리(IdleGame.Editor)는 시험 어셈블리가 참조하지 않아 불러온 어셈블리에서 찾는다 (ContentSourceTests와 같은 방식).
    /// </summary>
    public class ContentMetaTests
    {
        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ───────────────────────── 기능 행 (SPEC4 §2.8) ─────────────────────────

        [Test]
        public void EveryFeature_HasARow_AndNewRowsFollowSpec()
        {
            var db = Build(false);
            foreach (FeatureId feature in Enum.GetValues(typeof(FeatureId)))
            {
                Assert.IsNotNull(db.GetFeature(feature), $"{feature} 행이 없다 (MetaDataBuilder '정의 없음' 경고)");
            }

            var sweep = db.GetFeature(FeatureId.DungeonBatchSweep);
            Assert.AreEqual(1, sweep.conditions.Count);
            Assert.AreEqual(MetaConditionType.DungeonFloor, sweep.conditions[0].type);
            Assert.AreEqual(5, sweep.conditions[0].target, "던전 5층 돌파");
            Assert.AreEqual("잔향이 던전 5층을 돌파하면 열려요", sweep.lockedToast);
            Assert.AreEqual("이제 던전 입장권을 한 번에 모두 쓸 수 있어요", sweep.cardText);

            var stats = db.GetFeature(FeatureId.BattleStats);
            Assert.AreEqual(MetaConditionType.StageCleared, stats.conditions[0].type);
            Assert.AreEqual(MetaCondition.StageIndex(1, 7), stats.conditions[0].target, "1-7");
            Assert.IsFalse(stats.showCard, "전투 기록은 카드 없이 한 번 반짝임");

            var auto = db.GetFeature(FeatureId.AutoChallenge);
            Assert.AreEqual("이제 보스 도전과 원정 다시 보내기를 친구들이 알아서 해요", auto.cardText, "S5-4");
            Assert.AreEqual(auto.showCard ? 1 : 0, auto.rewards.Count(r => r.type == MetaRewardType.QuickIdleTicket), "빠른 수선권 1장은 카드와 함께");

            var batch = db.GetFeature(FeatureId.ExpeditionBatch);
            Assert.IsFalse(batch.showCard, "S5-4: 원정 한 번에 보내기는 카드 없음");
            Assert.AreEqual(0, batch.rewards.Count, "선물은 자동 보스 도전 카드로 옮겼다");

            Assert.IsFalse(db.GetFeature(FeatureId.Codex).showCard, "도감은 NEW 표시로만");
            Assert.AreEqual("불 밝히기 버튼을 길게 누르면 쓰는 방식을 고를 수 있어요", db.GetFeature(FeatureId.LampPolicy).cardText);
        }

        [Test]
        public void FeatureCards_GiftsOnlyWithCards_AndNoPlayFloorsOnSpec4Rows()
        {
            var db = Build(false);
            Assert.IsTrue(db.GetFeature(FeatureId.Speed2x).showCard, "2배속 버튼은 이미 있어 카드를 켠다");
            foreach (var def in db.features)
            {
                if (def.rewards.Count > 0) Assert.IsTrue(def.showCard, $"{def.feature}: 카드 없이 선물 (날아갈 곳이 없음)");
                Assert.AreEqual(0, def.alsoRequire?.Count ?? 0, $"{def.feature}: 4차 기능 행에는 앱 시간 바닥이 없다 (R2 행의 것 — SPEC5 §2.8)");
            }
            var events = db.GetFeature(FeatureId.Events);
            Assert.AreEqual(events.showCard ? 50 : 0, AmountOf(events.rewards, CurrencyType.EventToken), "이벤트 카드 선물 별빛 사탕 50");
        }

        [Test]
        public void FeatureCards_S5_OnWhereTheScreenExists_AtMostTwoPerStage()
        {
            var db = Build(false);
            // S-5 (RM-2 마지막 착지): 화면 담당이 닫힌 기능은 카드를 켠다 (SPEC4 §2.8)
            foreach (var feature in new[] { FeatureId.Speed2x, FeatureId.LampPolicy, FeatureId.AutoChallenge, FeatureId.Achievements, FeatureId.Events })
                Assert.IsTrue(db.GetFeature(feature).showCard, $"{feature}: 화면이 있어 카드를 켠다 (S-5)");
            // 소탕 장수 고르기는 있지만 카드 [가 보기]가 갈 길(MetaNavigator.OnFeature · FeatureSlot)이 없어 아직 끈다 — 길이 들어오면 이 줄을 IsTrue로
            Assert.IsFalse(db.GetFeature(FeatureId.DungeonBatchSweep).showCard, "DungeonBatchSweep: [가 보기] 길이 들어올 때까지 카드 없음");
            foreach (var feature in new[] { FeatureId.BattleStats, FeatureId.Codex, FeatureId.ExpeditionBatch })
                Assert.IsFalse(db.GetFeature(feature).showCard, $"{feature}: 표대로 카드 없음");

            // 카드 선물은 카드와 함께 켜진다
            var auto = db.GetFeature(FeatureId.AutoChallenge);
            Assert.AreEqual(1, auto.rewards.Count(r => r.type == MetaRewardType.QuickIdleTicket && r.amount == 1), "자동 보스 도전 카드 선물 빠른 수선권 1장");
            Assert.AreEqual(50, AmountOf(db.GetFeature(FeatureId.Events).rewards, CurrencyType.EventToken), 1e-9, "이벤트 카드 선물 별빛 사탕 50");

            // 한 스테이지 클리어에 카드는 두 장까지 (1-2 · 1-3 · 1-6 · 1-10이 두 장)
            var perStage = new Dictionary<long, List<FeatureId>>();
            foreach (var def in db.features)
            {
                if (!def.showCard || def.conditions == null) continue;
                var stage = def.conditions.FirstOrDefault(c => c != null && c.type == MetaConditionType.StageCleared);
                if (stage == null) continue;
                if (!perStage.TryGetValue(stage.target, out var list)) perStage[stage.target] = list = new List<FeatureId>();
                list.Add(def.feature);
            }
            foreach (var pair in perStage)
                Assert.LessOrEqual(pair.Value.Count, 2, $"스테이지 {pair.Key}: 카드 {string.Join(", ", pair.Value)} — 한 번에 두 장까지");
            Assert.AreEqual(2, perStage[MetaCondition.StageIndex(1, 6)].Count, "1-6: 2배속 + 불 밝히기 방식");
        }

        // ───────────────────────── Q-04 수선점 기록 (A.1) ─────────────────────────

        [Test]
        public void Achievements_SeventeenLines_SixtyTwoTiers_3400Gems()
        {
            var db = Build(false);
            Assert.AreEqual(17, db.AchievementLineIds.Count, "17줄");
            Assert.AreEqual(62, db.achievements.Count, "62단계");
            Assert.AreEqual(3400, db.achievements.Sum(a => AmountOf(a.rewards, CurrencyType.Gem)), 1e-9, "평생 3,400 (예산 ≤ 3,500)");
            Assert.AreEqual(db.achievements.Count, db.achievements.Select(a => a.id).Distinct().Count(), "ID 중복 없음");

            foreach (var lineId in db.AchievementLineIds)
            {
                var tiers = db.GetAchievementLine(lineId);
                for (int i = 0; i < tiers.Count; i++)
                {
                    Assert.AreEqual(i + 1, tiers[i].tier, $"{lineId}: 단계는 1부터");
                    Assert.AreEqual($"{lineId}_{i + 1}", tiers[i].id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(tiers[i].title), $"{tiers[i].id}: 제목");
                    if (i > 0 && tiers[i].condition.type != MetaConditionType.Flag)
                        Assert.Greater(tiers[i].condition.target, tiers[i - 1].condition.target, $"{tiers[i].id}: 목표 오름차순");
                }
            }

            var hidden = db.GetAchievementLine("ach_hidden");
            Assert.AreEqual(5, hidden.Count, "숨은 기록 5개");
            foreach (var def in hidden)
            {
                Assert.IsTrue(def.hidden);
                Assert.AreEqual(AchievementCategory.Hidden, def.category);
                Assert.AreEqual(MetaConditionType.Flag, def.condition.type);
                StringAssert.StartsWith("ach.hidden.", def.condition.targetId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.hint), $"{def.id}: 힌트");
                Assert.AreEqual(50, AmountOf(def.rewards, CurrencyType.Gem), 1e-9);
            }
            Assert.AreEqual(5, hidden.Select(h => h.condition.targetId).Distinct().Count(), "플래그는 서로 따로");
            // 찾은 뒤 설명 (A.1 'Done descriptions', AchievementDef.doneText) — 숨은 기록에만
            string[] done =
            {
                "비 오는 스테이지에서 우두커니가 지킨 채 클리어했어요",
                "수선점이 한 번도 다치지 않고 챕터 보스를 물리쳤어요",
                "한 스테이지에서 불을 세 번 밝혔어요",
                "짝짝이의 형태 11종을 모두 해금했어요",
                "자정이 넘은 밤에 출석 도장을 찍었어요",
            };
            for (int i = 0; i < hidden.Count; i++) Assert.AreEqual(done[i], hidden[i].doneText, $"{hidden[i].id}: 찾은 뒤 설명 (A.1)");
            foreach (var def in db.achievements)
                if (!def.hidden) Assert.IsTrue(string.IsNullOrEmpty(def.doneText), $"{def.id}: 숨은 기록이 아니면 찾은 뒤 설명 없음");

            var stage = db.GetAchievementLine("ach_stage");
            Assert.AreEqual("스테이지 1-10 클리어", stage[0].title);
            Assert.AreEqual(MetaCondition.StageIndex(10, 10), stage[4].condition.target, "10-10");
            Assert.AreEqual("적 100,000마리 물리치기", db.GetAchievement("ach_kill_4").title);
            Assert.AreEqual("10", db.GetAchievement("ach_affinity_1").condition.targetId, "호감도 Lv.10 친구 수");
        }

        // ───────────────────────── Q-05 도감 (A.2) ─────────────────────────

        [Test]
        public void Codex_BonusTiers_EnemyTexts_StoryHints()
        {
            var db = Build(false);
            var codex = db.codex;
            CollectionAssert.AreEqual(new[] { 5, 10, 15, 20, 25, 29 }, codex.formTiers.Select(t => t.count).ToArray());
            CollectionAssert.AreEqual(new[] { 1f, 3f, 5f, 7f, 8f, 10f }, codex.formTiers.Select(t => t.percent).ToArray());
            CollectionAssert.AreEqual(new[] { 10, 20, 25 }, codex.synergyTiers.Select(t => t.count).ToArray());
            CollectionAssert.AreEqual(new[] { 5f, 10f, 15f }, codex.synergyTiers.Select(t => t.percent).ToArray());
            Assert.AreEqual(5f, codex.allEnemiesShopHpPercent);
            Assert.AreEqual(30, codex.synergyMaxGem, "조정 #5");
            Assert.AreEqual(9, codex.enemyTexts.Select(t => t.enemyId).Distinct().Count(), "적 9종 문구");
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                var hint = db.GetCodexHint(CodexSystem.Key(CodexCategory.Story, chapter.ToString()));
                Assert.IsNotNull(hint, $"이야기 {chapter}장 힌트");
                Assert.AreEqual($"{chapter}장 보스를 물리치면 열려요", hint.hint);
            }
        }

        [Test]
        public void Codex_EveryEntryHasAHint_AndEveryHintResolves_WithContent()
        {
            var content = BuildContent();
            var db = Build(false, content);
            var keys = new HashSet<string>();
            foreach (var lineage in content.Characters.lineages)
            {
                foreach (var form in lineage.forms) keys.Add(CodexSystem.Key(CodexCategory.Friend, form.id));
                foreach (var costume in lineage.costumes) keys.Add(CodexSystem.Key(CodexCategory.Costume, costume.id));
            }
            foreach (var enemy in content.Battle.enemies) keys.Add(CodexSystem.Key(CodexCategory.Enemy, enemy.id));
            foreach (var synergy in content.Synergies.synergies) keys.Add(CodexSystem.Key(CodexCategory.Synergy, synergy.id));
            foreach (var item in content.Items.items.Where(i => i.category == ItemCategory.Material)) keys.Add(CodexSystem.Key(CodexCategory.Material, item.id));
            for (int chapter = 1; chapter <= 5; chapter++) keys.Add(CodexSystem.Key(CodexCategory.Story, chapter.ToString()));

            foreach (var hint in db.codex.hints) Assert.IsTrue(keys.Contains(hint.key), $"힌트 '{hint.key}'가 가리키는 칸이 없다");
            foreach (var key in keys) Assert.IsNotNull(db.GetCodexHint(key), $"칸 '{key}'에 힌트가 없다");
            Assert.AreEqual(keys.Count, db.codex.hints.Count, "힌트 중복 없음");

            var dust = db.GetCodexHint(CodexSystem.Key(CodexCategory.Enemy, "enemy_dust"));
            Assert.AreEqual("스테이지 1-1부터 나타나요", dust.hint);
            Assert.AreEqual("진화 기억도에서 단서를 찾아요", db.GetCodexHint(CodexSystem.Key(CodexCategory.Friend, "jjak_caravan")).hint);
        }

        // ───────────────────────── PRG-04 · CR-10 · CR-05 ─────────────────────────

        [Test]
        public void GachaMilestones_Ladder_Repeat_Budget()
        {
            var db = Build(false);
            var steps = db.GetGachaMilestones();
            CollectionAssert.AreEqual(new[] { 10, 30, 50, 100, 200 }, steps.Select(s => s.pulls).ToArray());
            Assert.AreEqual(100, steps.Sum(s => AmountOf(s.rewards, CurrencyType.Gem)), 1e-9, "처음 200회 별조각 100 (≤ 150)");
            var ticket = steps[3].rewards.Single();
            Assert.AreEqual(MetaRewardType.FriendSelectTicket, ticket.type, "100회 = 친구 선택권");
            Assert.AreEqual(nameof(ItemGrade.Legendary), ticket.itemId, "전설 이하 (§2.11 #4)");
            Assert.AreEqual(1, ticket.amount);
            Assert.AreEqual(100, db.gachaRepeat.interval);
            Assert.AreEqual(300, AmountOf(db.gachaRepeat.rewards, CurrencyType.Gem), 1e-9);
        }

        [Test]
        public void Affinity_SevenLineages_FiveStoryLines_SevenPetLines_Budget()
        {
            var content = BuildContent();
            var db = Build(false, content);
            var affinity = db.affinity;
            CollectionAssert.AreEqual(new long[] { 0, 50, 150, 350, 700, 1200, 2000, 3200, 4800, 7000 }, affinity.thresholds);
            Assert.AreEqual(10, affinity.gemPerLevel, "조정 #6");
            Assert.AreEqual(content.Characters.lineages.Count, affinity.lineages.Count);
            foreach (var lineage in content.Characters.lineages)
            {
                var lines = db.GetAffinityLines(lineage.id);
                Assert.IsNotNull(lines, $"{lineage.id} 문구");
                Assert.AreEqual(5, lines.story.Length);
                Assert.AreEqual(7, lines.pet.Length);
                Assert.IsTrue(lines.story.Concat(lines.pet).All(s => !string.IsNullOrWhiteSpace(s)), $"{lineage.id}: 빈 줄 없음");
            }
            double perLineage = Enumerable.Range(2, 9).Sum(level => affinity.gemPerLevel * level);
            Assert.AreEqual(540, perLineage, 1e-9);
            Assert.LessOrEqual(perLineage * content.Characters.lineages.Count, 4000, "평생 3,780 ≤ 4,000");
        }

        [Test]
        public void Story_FiveChapters_ThreePanels_LinesFit_FirstReadGift()
        {
            var db = Build(false);
            Assert.AreEqual(5, db.storyChapters.Count);
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                var story = db.GetStoryChapter(chapter);
                Assert.IsNotNull(story, $"{chapter}장");
                Assert.AreEqual(3, story.panels.Count);
                foreach (var panel in story.panels) Assert.LessOrEqual(panel.text.Length, 40, $"{chapter}장 '{panel.text}'");
                Assert.AreEqual(50, AmountOf(story.firstReadRewards, CurrencyType.Gem), 1e-9);
            }
            Assert.IsNull(db.GetStoryChapter(6), "6장부터는 동화책 없음");
            Assert.AreEqual("불이 켜진 골목", db.GetStoryChapter(1).title);
        }

        // ───────────────────────── PRG-06 챕터 상자 · 잃어버린 상자 (B.2 · B.3 · RD-13) ─────────────────────────

        [Test]
        public void ChapterChests_B2Rows_LateChapterGemsEveryFifthChapter()
        {
            var db = Build(false);
            double[] early = { 100, 100, 120, 120, 150 };
            for (int chapter = 1; chapter <= 5; chapter++)
                Assert.AreEqual(early[chapter - 1], AmountOf(db.GetChapterChest(chapter).rewards, CurrencyType.Gem), 1e-9, $"{chapter}장");
            Assert.AreEqual(2, AmountOf(db.GetChapterChest(1).rewards, MaterialThread), 1e-9, "1장 기억의 실 2");

            double storyAndEarly = Enumerable.Range(1, 5).Sum(c => AmountOf(db.GetStoryChapter(c).firstReadRewards, CurrencyType.Gem) +
                                                                  AmountOf(db.GetChapterChest(c).rewards, CurrencyType.Gem));
            Assert.AreEqual(840, storyAndEarly, 1e-9, "동화책 250 + 상자 590 (≤ 900)");

            for (int chapter = 6; chapter <= 260; chapter++)
            {
                var chest = db.GetChapterChest(chapter);
                Assert.IsNotNull(chest, $"{chapter}장 상자");
                Assert.AreEqual(6, AmountOf(chest.rewards, MaterialThread), 1e-9, $"{chapter}장 재료는 장마다");
                double gem = AmountOf(chest.rewards, CurrencyType.Gem);
                if (chapter <= 200) Assert.AreEqual(chapter % 5 == 0 ? 100 : 0, gem, 1e-9, $"{chapter}장 별조각은 5로 나누어떨어지는 장만");
            }
            for (int from = 6; from <= 250; from++)
            {
                double window = Enumerable.Range(from, 10).Sum(c => AmountOf(db.GetChapterChest(c).rewards, CurrencyType.Gem));
                Assert.LessOrEqual(window, 200, $"{from}~{from + 9}장 별조각 (열 장마다 ≤ 200, CR4-01)");
            }
        }

        [Test]
        public void LostChest_OffUntilG3_TablesCoverEveryStage_ExpectationNearToday()
        {
            Assert.AreEqual(0f, Build(false).lostChest.dropChance, "확장 항목: 끈 채로 싣는다");
            var db = Build(true);
            Assert.AreEqual(0.6f, db.lostChest.dropChance, 1e-6f);
            foreach (int stage in new[] { 1, 10, 11, 20, 21, 150, 5000 }) Assert.IsNotNull(db.GetLostChestTable(stage), $"스테이지 {stage} 표");

            var table = db.GetLostChestTable(5);
            double weights = table.entries.Sum(e => e.weight);
            double expected = 0.6 * table.entries.Sum(e => e.weight / weights * (e.min + e.max) / 2.0);
            Assert.AreEqual(0.83, expected, 0.01, "1~10 기대 재료 ≈0.83/클리어 (지금 0.78)");
        }

        // ───────────────────────── EVT-01 · Q-06 · CR-06 ─────────────────────────

        [Test]
        public void Events_FourEvents_SchedulesEffects_StarlitWalkBudget()
        {
            var db = Build(false);
            Assert.AreEqual(4, db.events.Count);

            var weekend = db.GetEvent("evt_weekend_expedition");
            Assert.AreEqual(EventScheduleType.Weekdays, weekend.scheduleType);
            Assert.AreEqual((1 << 5) | (1 << 6), weekend.weekdayMask, "토 · 일 (bit0 = 월)");
            Assert.AreEqual(EventKind.DropBoost, weekend.kind);
            Assert.AreEqual(2, weekend.multiplier, 1e-9);

            var wednesday = db.GetEvent("evt_wednesday_dungeon");
            Assert.AreEqual(1 << 2, wednesday.weekdayMask, "수요일");
            Assert.AreEqual(2, wednesday.bonus, "입장권 +2");

            var night = db.GetEvent("evt_night_shop");
            Assert.AreEqual(EventScheduleType.DailyHours, night.scheduleType);
            Assert.AreEqual(21, night.fromHour);
            Assert.AreEqual(2, night.toHour, "자정을 넘는다");
            Assert.AreEqual(1.2, night.multiplier, 1e-9);

            var starlit = db.GetEvent("evt_starlit_walk");
            Assert.AreEqual(EventScheduleType.MonthDays, starlit.scheduleType);
            Assert.AreEqual(1, starlit.fromDay);
            Assert.AreEqual(14, starlit.toDay);
            Assert.AreEqual(7, starlit.missions.Count, "6 + 보너스 1");
            var bonus = starlit.missions.Single(m => m.isBonus);
            Assert.AreEqual(CounterKey.EventMissionClaim, bonus.counter);
            Assert.AreEqual(6, bonus.target);
            Assert.AreEqual(7, starlit.stamps.Count);
            Assert.IsTrue(starlit.stamps.Single(s => s.day == 7).highlight);
            double gems = starlit.missions.Sum(m => AmountOf(m.rewards, CurrencyType.Gem)) + starlit.stamps.Sum(s => AmountOf(s.rewards, CurrencyType.Gem));
            Assert.AreEqual(250, gems, 1e-9, "한 달 250 ≤ 300");
            Assert.AreEqual(db.events.SelectMany(e => e.missions).Count(), db.events.SelectMany(e => e.missions).Select(m => m.id).Distinct().Count(), "미션 ID 중복 없음");
        }

        [Test]
        public void Season_EmptyWhileOff_ThirtyTiersWhenOn_Budget()
        {
            var off = Build(false);
            Assert.AreEqual(0, off.season.tiers.Count, "확장 항목: 칸 없음 = 꺼짐");
            Assert.AreEqual(90, off.season.xpPerTier);
            Assert.AreEqual(28, off.season.days);

            var db = Build(true);
            Assert.AreEqual(30, db.season.tiers.Count);
            for (int tier = 1; tier <= 30; tier++)
            {
                var def = db.GetSeasonTier(tier);
                Assert.IsNotNull(def, $"{tier}칸");
                Assert.AreEqual(tier % 5 == 0, def.highlight, $"{tier}칸 ★");
            }
            var last = db.GetSeasonTier(30);
            Assert.AreEqual(50, AmountOf(last.fallback, CurrencyType.Gem), 1e-9, "대신 보상 별조각 50");
            Assert.AreEqual(30, last.fallback.Where(r => r.type == MetaRewardType.SelectShards).Sum(r => r.amount), 1e-9, "+ 선택 기억편 30");
            double season = db.season.tiers.Sum(t => Math.Max(AmountOf(t.rewards, CurrencyType.Gem), AmountOf(t.fallback, CurrencyType.Gem)));
            Assert.AreEqual(440, season, 1e-9, "390 + 대신 보상 50 ≤ 450");
        }

        [Test]
        public void Comeback_ThreeDays_Budget()
        {
            var db = Build(false);
            Assert.AreEqual(259200, db.comeback.minAwaySeconds, "72시간");
            Assert.AreEqual(14, db.comeback.cooldownDays);
            Assert.AreEqual(6, db.comeback.lifetimeMax);
            Assert.AreEqual(24f, db.comeback.bonusCapHours);
            Assert.AreEqual(300, AmountOf(db.GetComebackDay(1).rewards, CurrencyType.Gem), 1e-9);
            Assert.AreEqual(2, db.GetComebackDay(1).rewards.Single(r => r.type == MetaRewardType.QuickIdleTicket).amount, 1e-9);
            Assert.AreEqual(20, db.GetComebackDay(3).rewards.Single(r => r.type == MetaRewardType.SelectShards).amount, 1e-9);
            Assert.AreEqual(300, db.comeback.days.Sum(d => AmountOf(d.rewards, CurrencyType.Gem)), 1e-9, "한 번 ≤ 300");
        }

        // ───────────────────────── EVT-02 · PRG-07 · NEW-01 ~ 04 ─────────────────────────

        [Test]
        public void Coupons_OnlyTheQaHash_NoPlaintext()
        {
            var db = Build(true);
            Assert.AreEqual(1, db.coupons.Count, "시험용 쿠폰 하나만 (실제 쿠폰은 사용자 결정)");
            Assert.IsFalse(string.IsNullOrEmpty(db.couponSalt), "소금");
            var coupon = db.coupons[0];
            Assert.AreEqual(64, coupon.hash.Length);
            Assert.IsTrue(coupon.hash.All(ch => ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f'), "소문자 16진수 (번호를 그대로 두지 않음)");
            Assert.IsNotNull(db.GetCoupon(coupon.hash));
            Assert.IsTrue(coupon.rewards.Count > 0 && coupon.rewards.All(r => r.amount > 0), "재화 · 재료 보상");

            var builder = EditorType("IdleGame.EditorTools.MetaDataBuilder");
            var normalize = builder.GetMethod("NormalizeCoupon", BindingFlags.Public | BindingFlags.Static);
            var hash = builder.GetMethod("CouponHash", BindingFlags.Public | BindingFlags.Static);
            Assert.AreEqual("ABCD12EF", normalize.Invoke(null, new object[] { "  ab-cd 12ef " }));
            Assert.IsNull(normalize.Invoke(null, new object[] { "AB1" }), "6자보다 짧음");
            Assert.IsNull(normalize.Invoke(null, new object[] { "ABCD!234" }), "영문 · 숫자만");
            Assert.IsNull(normalize.Invoke(null, new object[] { "\u0131bcd1234" }), "ASCII 밖 글자는 대문자로 바꾸지 않는다 (CouponSystem.Normalize와 같음)");
            Assert.IsNull(normalize.Invoke(null, new object[] { "ABCD1234ABCD12345" }), "16자보다 김");
            Assert.AreEqual("ABCDEF", normalize.Invoke(null, new object[] { "\tab cd\u00a0ef\n" }), "탭 · 줄바꿈 · 줄바꿈 없는 공백도 지운다");
            var made = (string)hash.Invoke(null, new object[] { "salt", "ABCD12EF" });
            Assert.AreEqual(64, made.Length);
            Assert.AreEqual(made, hash.Invoke(null, new object[] { "salt", "ABCD12EF" }), "같은 번호 = 같은 해시");
            Assert.AreNotEqual(made, hash.Invoke(null, new object[] { "salt2", "ABCD12EF" }), "소금이 다르면 다르다");

            // 편집기의 해시 만들기 창과 런타임 쿠폰 입력이 같은 규칙인지 (P4-2 CouponSystem — 어긋나면 만든 해시로 아무도 쿠폰을 못 받는다)
            foreach (string input in new[] { "  ab-cd 12ef ", "AB1", "ABCD!234", "\u0131bcd1234", "\u017Fabcdef", "ABCD1234ABCD12345", "\tab cd\u00a0ef\n", "", null })
            {
                string mine = (string)normalize.Invoke(null, new object[] { input });
                Assert.AreEqual(CouponSystem.Normalize(input), mine, $"다듬기 '{input}'");
                if (mine != null) Assert.AreEqual(CouponSystem.Hash(db.couponSalt, mine), hash.Invoke(null, new object[] { db.couponSalt, mine }), $"해시 '{input}'");
            }
        }

        [Test]
        public void Blessings_EmptyWhileOff_TwelveWhenOn()
        {
            Assert.AreEqual(0, Build(false).blessings.Count, "확장 항목: 비움 = 꺼짐");
            var db = Build(true);
            Assert.AreEqual(12, db.blessings.Count);
            Assert.AreEqual(12, db.blessings.Select(b => b.id).Distinct().Count());
            Assert.IsTrue(db.blessings.All(b => b.maxStacks == 2 && b.value > 0 && Enum.IsDefined(typeof(StatType), b.stat)));
            Assert.AreEqual(StatType.EchoCostReductionFlat, db.GetBlessing("b_cost").stat);
        }

        [Test]
        public void StarterWeek_28Tasks_180Points_FiveChests_Budget()
        {
            var db = Build(false);
            var week = db.starterWeek;
            Assert.AreEqual(14, week.windowDays);
            Assert.AreEqual(7, week.maxInstallDays);
            Assert.AreEqual(28, week.tasks.Count);
            for (int day = 1; day <= 7; day++) Assert.AreEqual(4, db.GetStarterTasks(day).Count, $"{day}일째 4과제");
            Assert.AreEqual(180, week.tasks.Sum(t => t.points), "5점 × 24 + 15점 × 4");
            Assert.IsTrue(db.GetStarterTasks(7).All(t => t.points == 15));
            Assert.AreEqual(28, week.tasks.Select(t => t.id).Distinct().Count());
            CollectionAssert.AreEqual(new[] { 30, 60, 90, 120, 150 }, week.chests.Select(c => c.points).ToArray());
            Assert.IsTrue(week.chests.Last().highlight);
            Assert.AreEqual(300, week.chests.Sum(c => AmountOf(c.rewards, CurrencyType.Gem)), 1e-9, "별조각 300 ≤ 350");
            var journal = db.GetStarterTask("fw_d1_journal");
            Assert.AreEqual(MetaConditionType.JournalPoints, journal.condition.type, "오늘 일지 40점 (완료로 굳는다)");
            Assert.AreEqual(40, journal.condition.target);
        }

        [Test]
        public void DailyGifts_SevenWeekdays_Budget()
        {
            var db = Build(false);
            Assert.AreEqual(7, db.dailyGifts.Count);
            for (int weekday = 0; weekday < 7; weekday++) Assert.IsNotNull(db.GetDailyGift(weekday), $"요일 {weekday}");
            Assert.AreEqual(20, db.GetDailyGift(4).rewards.Sum(r => r.type == MetaRewardType.Currency && r.currency == CurrencyType.Gem ? r.amount : 0), 1e-9, "금요일 별조각 20");
            Assert.AreEqual(20, db.dailyGifts.Sum(g => AmountOf(g.rewards, CurrencyType.Gem)), 1e-9, "주당 ≤ 30");
        }

        [Test]
        public void Firefly_ZeroWhileOff_SixWhenOn_Budget()
        {
            Assert.AreEqual(0, Build(false).firefly.perDay, "확장 항목: 하루 0마리 = 꺼짐");
            var firefly = Build(true).firefly;
            Assert.AreEqual(6, firefly.perDay);
            Assert.AreEqual(360f, firefly.minInterval);
            Assert.AreEqual(720f, firefly.maxInterval);
            Assert.AreEqual(8f, firefly.lifeSeconds);
            CollectionAssert.AreEqual(new[] { 55, 35, 10 }, firefly.rewards.Select(r => r.weight).ToArray());
            Assert.IsTrue(firefly.rewards[1].fromLostChestTable);
            double weights = firefly.rewards.Sum(r => r.weight);
            double week = firefly.perDay * 7 * firefly.rewards.Where(r => !r.fromLostChestTable).Sum(r => r.weight * AmountOf(new List<MetaReward> { r.reward }, CurrencyType.Gem)) / weights;
            Assert.AreEqual(42, week, 1e-9, "주당 42 ≤ 60");
        }

        [Test]
        public void TicketBank_OffWhileStretchOff()
        {
            Assert.AreEqual(0, Build(false).dungeonTicketBankCap, "0 = 끔 = 날마다 초기화 (지금)");
            Assert.AreEqual(9, Build(true).dungeonTicketBankCap);
        }

        [Test]
        public void Cheer_BossHints_BackupReminder()
        {
            var db = Build(false);
            Assert.AreEqual(20f, db.cheer.attackPercent);
            Assert.AreEqual(600, db.cheer.durationSeconds);
            Assert.AreEqual(2, db.bossHints.Count);
            StringAssert.Contains("불빛을 빼앗아요", db.GetBossHint("boss_shadow").hint);
            StringAssert.Contains("좀나방을 불러내요", db.GetBossHint("boss_mothqueen").hint);
            Assert.AreEqual(14, db.backupReminder.days, "마지막 백업(없으면 설치한 날) 뒤 14일, 그 뒤로 14일마다");
            Assert.IsFalse(string.IsNullOrWhiteSpace(db.backupReminder.mailTitle));
            Assert.IsFalse(string.IsNullOrWhiteSpace(db.backupReminder.mailBody));
            StringAssert.Contains("설정 › 계정·데이터", db.backupReminder.mailBody, "세이브 코드를 복사하는 곳 (SPEC4 §2.12)");
            // 첫 우편은 한 번도 내보내지 않은 세이브(설치한 날부터 셈)에도 가고, 뒤 우편은 마지막 알림부터 14일마다 간다 (BackupReminderSystem)
            // → 문구는 날 수도 "이미 저장했다"도 말하지 않는다
            StringAssert.DoesNotContain("14일", db.backupReminder.mailBody);
            StringAssert.DoesNotContain("저장한 지", db.backupReminder.mailBody);
        }

        // ───────────────────────── 바구니 · 전투력 · 가이드 22단계 ─────────────────────────

        /// <summary>콘텐츠 값만 본다 — 상한 규칙(IdleChestSystem)으로 본 12 · 16시간은 ContentMetaSystemTests.IdleChest_BuiltCapSteps_SixteenHoursFromShopLevel200</summary>
        [Test]
        public void IdleChest_GemLineCeiling_SixteenHourStep_Content()
        {
            var chest = Build(false).idleChest;
            var gem = chest.lines.Single(l => l.reward.type == MetaRewardType.Currency && l.reward.currency == CurrencyType.Gem);
            Assert.AreEqual(1.5, gem.maxStageMultiplier, 1e-12, "C-01 별조각 줄 천장");
            Assert.IsTrue(chest.lines.Where(l => l != gem).All(l => l.maxStageMultiplier == 0), "재료 · 사탕 줄은 계속 자란다");

            foreach (int stage in new[] { 1, 20, 50, 100, 200, 1000 })
            {
                double multiplier = Math.Min(Math.Pow(chest.stageGrowth, stage - 1), gem.maxStageMultiplier);
                double perDay = gem.reward.amount * multiplier * (24 + chest.quickIdleGemCosts.Length * chest.quickIdleHours);
                Assert.LessOrEqual(perDay, 140 + 1e-9, $"스테이지 {stage}: 하루 별조각 (빠른 수선 포함, §11.2)");
            }

            Assert.AreEqual(2, chest.capSteps.Count);
            Assert.AreEqual(12f, chest.capSteps[0].capHours);
            Assert.AreEqual(0, chest.capSteps[0].shopLevel);
            Assert.AreEqual(16f, chest.capSteps[1].capHours);
            Assert.AreEqual(200, chest.capSteps[1].shopLevel, "수선점 Lv.200 16시간 (IDLE-01)");
            Assert.AreEqual(8f, chest.baseCapHours, "2-1 전 기본 상한");
        }

        /// <summary>콘텐츠 값만 본다 — PowerSystem.GetRecommendedPower로 본 30 · 151 이음매는 ContentMetaSystemTests.Power_BuiltConfig_RecommendedCurve_IsContinuousAtBothTapers</summary>
        [Test]
        public void Power_TaperStages_ConfigPins()
        {
            var power = Build(false).power;
            Assert.AreEqual(30, power.recommendedTaperStage);
            Assert.AreEqual(151, power.recommendedTaperStage2, "SPEC5 §4.3 · 델타 2");
            Assert.AreEqual(1f, power.shopShareCap);
            Assert.GreaterOrEqual(power.recommendedGrowthLate, 1.0, "늦은 구간 증가율이 비지 않는다 (0이면 앞 구간 성장률로 돌아감)");
            Assert.GreaterOrEqual(power.recommendedGrowthLate2, 1.0);
        }

        /// <summary>
        /// 콘텐츠 값만 본다 (S-3 스위치에 맞는 22단계 보상). "1장 상자 내용을 한 번만 받는다"는 실제 시스템으로
        /// GuideQuestTests.BuiltContent_Quest22_AndChapterOneChest_PayChapterOneOnce가 본다
        /// </summary>
        [Test]
        public void GuideQuest22_Rewards_FollowTheS3Switch()
        {
            var db = Build(false);
            var quest = db.guideQuests.Single(q => q.id == "gq_22");
            var moved = (bool)EditorType("IdleGame.EditorTools.MetaContent")
                .GetField("ChapterChestMovedToSystem", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (moved)
            {
                Assert.AreEqual(1, quest.rewards.Count, "S-3 뒤: 22단계는 별조각 100만 (상자는 챕터 상자 팝업)");
                Assert.AreEqual(100, AmountOf(quest.rewards, CurrencyType.Gem), 1e-9);
            }
            else
            {
                var chest = db.GetChapterChest(1);
                Assert.AreEqual(200, AmountOf(quest.rewards, CurrencyType.Gem), 1e-9, "S-3 전: 별조각 100 + 1장 상자 100");
                foreach (var reward in chest.rewards.Where(r => r.type == MetaRewardType.Item))
                    Assert.AreEqual(reward.amount, AmountOf(quest.rewards, reward.itemId), 1e-9, $"1장 상자 {reward.itemId}가 22단계에");
            }
        }

        // ───────────────────────── 문구 · 검증 ─────────────────────────

        [Test]
        public void Copy_HasNoBannedWords()
        {
            var db = Build(true);
            var found = new List<string>();
            Walk(db, "db", found, 0);
            CollectionAssert.IsEmpty(found, "해요체 규칙: '습니다' · '개방' · '하시겠' · '장비' 없음");
        }

        [Test]
        public void MetaDataBuilderValidate_NoErrorsNoWarnings_WithContent()
        {
            var content = BuildContent();
            var db = Build(false, content);
            var validate = EditorType("IdleGame.EditorTools.MetaDataBuilder").GetMethod("Validate", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(MetaDatabase), typeof(GameContent), typeof(List<string>) }, null);
            Assert.IsNotNull(validate, "MetaDataBuilder.Validate(db, content, warnings)");
            var warnings = new List<string>();
            List<string> errors;
            LogAssert.ignoreFailingMessages = true; // 오류는 아래에서 목록으로 본다
            try
            {
                errors = (List<string>)validate.Invoke(null, new object[] { db, content, warnings });
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
            CollectionAssert.IsEmpty(errors, "MetaDataBuilder.Validate 오류");
            CollectionAssert.IsEmpty(warnings, "MetaDataBuilder.Validate 경고 (DungeonBatchSweep · BattleStats 정의 없음 두 개가 정리됐는지)");
        }

        [Test]
        public void Power_CalibratedSegments_AreSet_AndStarterStaysComfortableInChapterOne()
        {
            var power = Build(false).power;
            Assert.Greater(power.recommendedGrowthLate, 1.0, "세 구간 보정이 들어가 있다 (S-4)");
            Assert.Greater(power.recommendedGrowthLate2, 1.0);
            Assert.Less(power.recommendedGrowthLate, power.recommendedGrowth, "적 성장이 30스테이지 뒤 완만해지는 것을 따라간다");
            Assert.Less(power.recommendedGrowthLate2, power.recommendedGrowthLate, "151스테이지 뒤 한 번 더");
            Assert.Greater(power.dungeonRecommendedBase, 0, "던전 권장 줄이 켜졌다");
            Assert.GreaterOrEqual(power.dungeonRecommendedGrowth, 1.0);
            double rec10 = power.recommendedBase * Math.Pow(power.recommendedGrowth, 9);
            Assert.LessOrEqual(rec10 * power.comfortableRatio, 1017, "시작 편성(1,017)은 1-10까지 '여유'");
        }

        // ───────────────────────── 도우미 ─────────────────────────

        const string MaterialThread = "mat_thread";

        static readonly string[] BannedWords = { "습니다", "개방", "하시겠", "장비" };

        MetaDatabase Build(bool allStretch, GameContent content = null)
        {
            var db = ScriptableObject.CreateInstance<MetaDatabase>();
            _created.Add(db);
            ApplyMetaContent(db, content, allStretch);
            return db;
        }

        /// <summary>코드 원본 메타 콘텐츠로 db를 채운다 (MetaContent.Apply — 게임 데이터와 같은 내용, allStretch = 확장 항목까지). 시스템 시험도 쓴다</summary>
        internal static void ApplyMetaContent(MetaDatabase db, GameContent content, bool allStretch)
        {
            var apply = EditorType("IdleGame.EditorTools.MetaContent").GetMethod("Apply", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(MetaDatabase), typeof(GameContent), typeof(bool) }, null);
            Assert.IsNotNull(apply, "MetaContent.Apply(MetaDatabase, GameContent, bool)");
            apply.Invoke(null, new object[] { db, content, allStretch });
            db.ClearCache();
        }

        /// <summary>코드 원본 아이템 목록 (재료 9종 포함 — ItemContent.Build)</summary>
        internal static List<ItemDef> BuildItems() => (List<ItemDef>)Invoke("IdleGame.EditorTools.ItemContent", "Build", Type.EmptyTypes);

        /// <summary>코드 원본 콘텐츠 (에셋 없이): 계보 · 재료 · 시너지 · 적 · 설정 기본값</summary>
        GameContent BuildContent()
        {
            var characters = Create<CharacterDatabase>();
            characters.lineages = (List<LineageDef>)Invoke("IdleGame.EditorTools.CharacterContent", "Build", Type.EmptyTypes);
            var items = Create<ItemDatabase>();
            items.items = BuildItems();
            var synergies = Create<SynergyDatabase>();
            synergies.synergies = (List<SynergyDef>)Invoke("IdleGame.EditorTools.SynergyContent", "Build", new[] { typeof(IEnumerable<LineageDef>) }, characters.lineages);
            var battle = Create<BattleConfig>();
            battle.ApplyDefaults();
            return new GameContent
            {
                Config = Create<GameConfig>(),
                Items = items,
                Characters = characters,
                Battle = battle,
                Synergies = synergies,
            };
        }

        T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        static object Invoke(string typeName, string method, Type[] types, params object[] args)
        {
            var info = EditorType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
            Assert.IsNotNull(info, $"{typeName}.{method}");
            return info.Invoke(null, args);
        }

        internal static Type EditorType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                var type = assembly.GetType(name, false);
                Assert.IsNotNull(type, $"IdleGame.Editor 어셈블리의 {name}");
                return type;
            }
            Assert.Fail("IdleGame.Editor 어셈블리를 찾을 수 없다");
            return null;
        }

        static double AmountOf(IEnumerable<MetaReward> rewards, CurrencyType currency) =>
            rewards?.Where(r => r != null && r.type == MetaRewardType.Currency && r.currency == currency).Sum(r => r.amount) ?? 0;

        static double AmountOf(IEnumerable<MetaReward> rewards, string itemId) =>
            rewards?.Where(r => r != null && r.type == MetaRewardType.Item && r.itemId == itemId).Sum(r => r.amount) ?? 0;

        /// <summary>MetaDatabase의 문자열을 모두 훑는다 (에셋 참조 · 기본 형식은 건너뜀)</summary>
        static void Walk(object value, string path, List<string> found, int depth)
        {
            if (value == null || depth > 8) return;
            if (value is string text)
            {
                foreach (var word in BannedWords)
                {
                    if (text.Contains(word)) found.Add($"{path}: '{word}' — {text}");
                }
                return;
            }
            if (value is Object && depth > 0) return;
            if (value is IEnumerable list)
            {
                int index = 0;
                foreach (var item in list) Walk(item, $"{path}[{index++}]", found, depth + 1);
                return;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.Namespace != typeof(MetaDatabase).Namespace) return;
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Walk(field.GetValue(value), $"{path}.{field.Name}", found, depth + 1);
        }
    }

    /// <summary>
    /// 코드 원본 메타 콘텐츠를 실제 시스템에 넣고 본다 (RM-2 P4-8 — 콘텐츠 값만 보는 ContentMetaTests를 보탠다):
    ///   · 권장 전투력 (PowerSystem.GetRecommendedPower, 보정한 세 구간 S-4): 30 · 151 이음매에서 뛰지 않고 각 구간 성장률로 이어진다
    ///   · 바구니 상한 (IdleChestSystem, S-2 · SPEC4 §13.4): 2-1 = 12시간, 수선점 Lv.200 = 16시간, 2-1 전 = 기본 상한
    /// </summary>
    public class ContentMetaSystemTests : MetaSystemsTestBase
    {
        void UseBuiltContent()
        {
            ContentMetaTests.ApplyMetaContent(MetaDb, null, false);
            Rebuild();
        }

        [Test]
        public void Power_BuiltConfig_RecommendedCurve_IsContinuousAtBothTapers()
        {
            UseBuiltContent();
            var config = MetaDb.power;
            Assert.AreEqual(30, config.recommendedTaperStage);
            Assert.AreEqual(151, config.recommendedTaperStage2);
            Assert.Greater(config.recommendedGrowth, config.recommendedGrowthLate, "30스테이지 뒤 완만해진다");
            Assert.Greater(config.recommendedGrowthLate, config.recommendedGrowthLate2, "151스테이지 뒤 한 번 더");

            // 한 걸음 배율 (권장값은 정수로 반올림 — 29스테이지에서 이미 수천이라 오차 0.1% 아래)
            double Step(int stage) => (double)Power.GetRecommendedPower(stage + 1) / Power.GetRecommendedPower(stage);
            const double Tolerance = 1e-3;
            Assert.AreEqual(config.recommendedGrowth, Step(29), Tolerance, "29 → 30: 첫 구간");
            Assert.AreEqual(config.recommendedGrowthLate, Step(30), Tolerance, "30 → 31: 둘째 구간이 30에서 이어진다 (뛰거나 꺼지지 않음)");
            Assert.AreEqual(config.recommendedGrowthLate, Step(150), Tolerance, "150 → 151: 둘째 구간");
            Assert.AreEqual(config.recommendedGrowthLate2, Step(151), Tolerance, "151 → 152: 셋째 구간이 151에서 이어진다");

            Assert.AreEqual(Math.Round(config.recommendedBase), Power.GetRecommendedPower(1), 1.0, "1-1 = 기준값");
            long previous = 0;
            for (int stage = 1; stage <= 400; stage++)
            {
                long recommended = Power.GetRecommendedPower(stage);
                Assert.Greater(recommended, previous, $"스테이지 {stage}: 권장 전투력은 늘 오른다");
                previous = recommended;
            }
        }

        [Test]
        public void IdleChest_BuiltCapSteps_SixteenHoursFromShopLevel200()
        {
            UseBuiltContent();
            double baseHours = MetaDb.idleChest.baseCapHours;

            SetCleared(10);
            Data.level = 500;
            Assert.AreEqual(baseHours, IdleChest.Cap.TotalHours, 1e-6, "1-10까지는 기본 상한 (16시간 단계도 2-1을 함께 본다)");
            SetCleared(11);
            Data.level = 1;
            Assert.AreEqual(12.0, IdleChest.Cap.TotalHours, 1e-6, "2-1 클리어 = 12시간");
            SetCleared(30);
            Data.level = 199;
            Assert.AreEqual(12.0, IdleChest.Cap.TotalHours, 1e-6, "수선점 Lv.199 = 12시간 그대로");
            Data.level = 200;
            Assert.AreEqual(16.0, IdleChest.Cap.TotalHours, 1e-6, "수선점 Lv.200 = 16시간 (IDLE-01 · C-01)");
        }
    }
}
