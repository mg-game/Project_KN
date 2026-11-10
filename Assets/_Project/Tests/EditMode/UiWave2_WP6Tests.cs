using System;
using System.Collections.Generic;
using IdleGame.UI;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-6 친구 · 소환 (UX Wave 2) 순수 규칙 — 장면 · Game 없이 도우미 함수만 본다.
    ///   · M16: 소환 결과 '편성하러 가기 ›' 대상 (빈 칸 + 보유 · 미배치, 대표 결과 먼저).
    ///   · M75: 진화 레시피 버튼 글 (상태 넷).
    ///   · M17 · M72: '재료 모으러 가기' 글 · 잠긴 획득처 토스트 · 잠긴 줄 한 줄 · 획득처 줄 순서.
    ///   · M60B: 공명 효과 줄 '→'. M76: 해금 문구 · '했습니다' 없음. M08 · M63: 소환 탭 글.
    ///   · M29: 양피지 위 색 글자 (InkTint 파스텔 태그 · InkPink 감정 문구) 4.5:1.
    ///   · RM-2 P4-5 (SPEC4): 첫 만남 결과 판정 · 대표 결과 번호 · 전투력 / 따라잡기 / 녹음 줄 (CR-04 · PRG-01 · PRG-05), 소환 마일스톤 줄 · 창 (PRG-04),
    ///     친구 선택권 (전설 이하), 호감도 칸 (CR-10), 열린 탭 안 잠금 글 (F-03), 도감 알약 (Q-05), 새 글의 금지 말('습니다' · '개방' · '장비'),
    ///     친구들 카드 점 = 탭 점의 계보 잎 넷 (호감도 포함, CR-10), 첫 만남 창의 잔액 칩 (다시 소환해도 창이 닫힐 때까지 숨김).
    /// </summary>
    public class UiWave2_WP6Tests
    {
        static GachaResult Result(string id, bool isNew = true, ItemGrade grade = ItemGrade.Rare) =>
            new(new LineageDef { id = id, name = id, grade = grade }, isNew, false, isNew ? 0 : 10);

        static Func<string, bool> Set(params string[] ids)
        {
            var set = new HashSet<string>(ids);
            return set.Contains;
        }

        // ───────────── M16 편성하러 가기 ─────────────

        [Test]
        public void FormationCandidate_NoEmptySlot_IsNull()
        {
            var results = new[] { Result("a") };
            Assert.IsNull(GachaResultPopup.FormationCandidate(results, results[0], false, Set("a"), Set()));
        }

        [Test]
        public void FormationCandidate_PrefersBestWhenOwnedAndUnplaced()
        {
            var results = new[] { Result("a"), Result("b") };
            Assert.AreEqual("b", GachaResultPopup.FormationCandidate(results, results[1], true, Set("a", "b"), Set()));
        }

        [Test]
        public void FormationCandidate_SkipsPlacedBest_FallsBackToResultOrder()
        {
            var results = new[] { Result("a"), Result("b"), Result("c") };
            Assert.AreEqual("a", GachaResultPopup.FormationCandidate(results, results[1], true, Set("a", "b", "c"), Set("b")));
        }

        [Test]
        public void FormationCandidate_AllPlacedOrUnowned_IsNull()
        {
            var results = new[] { Result("a"), Result("b") };
            Assert.IsNull(GachaResultPopup.FormationCandidate(results, results[0], true, Set("a"), Set("a")));
        }

        // ───────────── M75 진화 레시피 버튼 ─────────────

        [Test]
        public void CheckLabel_FourStates()
        {
            Assert.AreEqual("진화 확인", EvolutionTreeView.CheckLabel(false, "카라반", true, 0, 0, false));
            Assert.AreEqual("진화 단서 찾는 중", EvolutionTreeView.CheckLabel(true, null, false, 0, 0, false));
            StringAssert.StartsWith("카라반 진화 제작!", EvolutionTreeView.CheckLabel(true, "카라반", true, 0, 0, false));
            StringAssert.Contains("준비 완료", EvolutionTreeView.CheckLabel(true, "카라반", true, 0, 0, false));
            string pending = EvolutionTreeView.CheckLabel(true, "카라반", false, 1, 3, false);
            StringAssert.StartsWith("다음 진화 · 카라반", pending);
            StringAssert.Contains("재료 1/3", pending);
            StringAssert.Contains("조건 0/2", EvolutionTreeView.CheckLabel(true, "카라반", false, 0, 2, true));
        }

        // ───────────── M17 · M72 재료 모으러 가기 ─────────────

        [Test]
        public void CollectLabel_ShopSaysBuy_OthersSayCollect_WithDot()
        {
            var shop = new ItemSource { type = ItemSourceType.Shop, label = "수선점 상점" };
            var expedition = new ItemSource { type = ItemSourceType.Expedition, label = "원정 2-4" };
            Assert.AreEqual("수선 천 사러 가기 · 수선점 상점", CharacterUI.CollectLabel("수선 천", shop));
            Assert.AreEqual("리본 모으러 가기 · 원정 2-4", CharacterUI.CollectLabel("리본", expedition));
            Assert.AreEqual("리본 모으러 가기", CharacterUI.CollectLabel("리본", null));
        }

        [Test]
        public void CollectButtonLabel_NothingMissing_SaysDone()
        {
            Assert.AreEqual(EvolutionRecipePopup.CollectDoneLabel, EvolutionRecipePopup.CollectButtonLabel(false, "수선 천", null));
            var shop = new ItemSource { type = ItemSourceType.Shop, label = "수선점 상점" };
            StringAssert.Contains("·", EvolutionRecipePopup.CollectButtonLabel(true, "수선 천", shop));
            Assert.AreEqual("재료 모으러 가기", EvolutionRecipePopup.CollectButtonLabel(true, null, null));
        }

        [Test]
        public void LockedSourceToast_TopicParticleAndFallback()
        {
            Assert.AreEqual("수선 천은 스테이지 2-9를 클리어하면 열려요", CharacterUI.LockedSourceToast("수선 천", "스테이지 2-9를 클리어하면 열려요"));
            Assert.AreEqual("리본은 오늘 상점 수량을 모두 샀어요", CharacterUI.LockedSourceToast("리본", "오늘 상점 수량을 모두 샀어요"));
            Assert.AreEqual("단추는 지금은 얻을 곳이 없어요", CharacterUI.LockedSourceToast("단추", null));
        }

        [Test]
        public void LockedLine_UsesUnlockShortWhenStageKnown_ElseReason()
        {
            Assert.AreEqual(MetaText.UnlockShort(20), RecipeSourceView.LockedLine(20, "스테이지 2-9를 클리어하면 열려요"));
            StringAssert.Contains("2-9", RecipeSourceView.LockedLine(20, "x"));
            Assert.AreEqual("오늘 상점 수량을 모두 샀어요", RecipeSourceView.LockedLine(0, "오늘 상점 수량을 모두 샀어요"));
            Assert.AreEqual("지금은 갈 수 없는 원정이에요", RecipeSourceView.LockedLine(int.MaxValue, "지금은 갈 수 없는 원정이에요"));
        }

        [Test]
        public void DisplayKey_AvailableFirst_StageNext_LockedByStage()
        {
            long open = EvolutionRecipePopup.DisplayKey(true, false, 0, 5);
            long stage = EvolutionRecipePopup.DisplayKey(true, true, 0, 0);
            long lockedEarly = EvolutionRecipePopup.DisplayKey(false, false, 12, 9);
            long lockedLate = EvolutionRecipePopup.DisplayKey(false, false, 30, 1);
            long lockedUnknown = EvolutionRecipePopup.DisplayKey(false, false, int.MaxValue, 0);
            Assert.Less(open, stage, "갈 수 있는 곳 → 스테이지");
            Assert.Less(stage, lockedEarly, "스테이지 → 잠긴 곳");
            Assert.Less(lockedEarly, lockedLate, "잠긴 곳은 열리는 스테이지 순");
            Assert.Less(lockedLate, lockedUnknown, "모르는 스테이지는 맨 뒤");
            Assert.Less(EvolutionRecipePopup.DisplayKey(true, false, 0, 1), EvolutionRecipePopup.DisplayKey(true, false, 0, 2), "같은 묶음은 순서대로");
        }

        // ───────────── M60B · M76 · M08 · M63 글 ─────────────

        [Test]
        public void ResonanceLine_ShowsArrow_OrMax()
        {
            StringAssert.Contains("→", CharacterDetailPopup.ResonanceLine(5f, 10f, true));
            StringAssert.Contains("+10%", CharacterDetailPopup.ResonanceLine(5f, 10f, true));
            StringAssert.Contains("(MAX)", CharacterDetailPopup.ResonanceLine(50f, 50f, false));
        }

        [Test]
        public void CelebrationTitle_SaysUnlockedFriendly()
        {
            string title = EvolutionCraftPopup.CelebrationTitle("짝짝 카라반");
            StringAssert.EndsWith("해금됐어요!", title);
            StringAssert.DoesNotContain("했습니다", title);
            StringAssert.DoesNotContain("되었습니다", title);
        }

        [Test]
        public void GemLinkText_MailOrSources()
        {
            Assert.AreEqual("별조각 얻는 곳 ›", GachaPanel.GemLinkText("별조각", false));
            Assert.AreEqual("우편함에 별조각이 있어요 ›", GachaPanel.GemLinkText("별조각", true));
        }

        [Test]
        public void PityLine_ShowsCountOfTotal()
        {
            string line = GachaPanel.PityLine(ItemGrade.Legendary, 60, 0, 60, false);
            StringAssert.Contains("확정까지", line);
            StringAssert.Contains("0/60", line);
        }

        // ───────────── RM-2 P4-5: 소환 결과 (CR-04 · PRG-01 · PRG-05 · 첫 만남) ─────────────

        [Test]
        public void IsFirstMeeting_OnlyWhenEveryResultIsGuaranteed()
        {
            var guaranteed = new GachaResult(new LineageDef { id = "a", name = "a" }, true, false, 0, true);
            Assert.IsTrue(GachaResultPopup.IsFirstMeeting(new[] { guaranteed }));
            Assert.IsFalse(GachaResultPopup.IsFirstMeeting(new[] { guaranteed, Result("b") }));
            Assert.IsFalse(GachaResultPopup.IsFirstMeeting(new[] { Result("b") }));
            Assert.IsFalse(GachaResultPopup.IsFirstMeeting(new GachaResult[0]));
            Assert.IsFalse(GachaResultPopup.IsFirstMeeting(null));
        }

        [Test]
        public void PickBestIndex_NewFirst_ThenHighestGrade_ThenEarliest()
        {
            var results = new[] { Result("a", false, ItemGrade.Mythic), Result("b", true, ItemGrade.Rare), Result("c", true, ItemGrade.Epic), Result("d", true, ItemGrade.Epic) };
            Assert.AreEqual(2, GachaResultPopup.PickBestIndex(results), "새 계보 중 최고 등급, 같으면 앞의 것");
            var dups = new[] { Result("a", false, ItemGrade.Rare), Result("b", false, ItemGrade.Legendary) };
            Assert.AreEqual(1, GachaResultPopup.PickBestIndex(dups), "새 계보가 없으면 전체 최고 등급");
            Assert.AreEqual(-1, GachaResultPopup.PickBestIndex(new GachaResult[0]));
        }

        [Test]
        public void HighlightLines_PowerCatchUpMelt()
        {
            Assert.AreEqual("전투력 1,234", GachaResultPopup.PowerLine(1234));
            Assert.AreEqual(string.Empty, GachaResultPopup.CatchUpLine(1), "시작 레벨 1이면 따라잡기 줄 없음");
            Assert.AreEqual("친구들 평균의 80%인 Lv.14부터 함께해요", GachaResultPopup.CatchUpLine(14));
            StringAssert.Contains("+1,200", GachaResultPopup.MeltLine(1200));
            StringAssert.StartsWith("기억편", GachaResultPopup.MeltLine(5));
        }

        // ───────────── RM-2 P4-5: 소환 마일스톤 (PRG-04) ─────────────

        static List<GachaMilestoneStep> Steps(int pulls, params (int at, bool claimed)[] steps)
        {
            var list = new List<GachaMilestoneStep>();
            for (int i = 0; i < steps.Length; i++)
                list.Add(new GachaMilestoneStep { Index = i, Pulls = steps[i].at, Reached = pulls >= steps[i].at, Claimed = steps[i].claimed });
            return list;
        }

        [Test]
        public void MilestoneWindow_StartsAtFirstUnclaimed_FromPreviousStep()
        {
            var steps = Steps(35, (10, true), (30, false), (50, false), (100, false), (200, false), (300, false), (400, false));
            GachaPanel.MilestoneWindow(steps, 5, out int first, out int last, out int from);
            Assert.AreEqual(1, first, "처음 받지 않은 단계");
            Assert.AreEqual(5, last, "점 다섯 개");
            Assert.AreEqual(10, from, "막대는 앞 단계(10회)부터");
            Assert.AreEqual(0.25f, GachaPanel.MilestoneFill(35, 10, 110), 1e-4f);
            Assert.AreEqual(1f, GachaPanel.MilestoneFill(500, 10, 300));
            Assert.AreEqual(0f, GachaPanel.MilestoneFill(5, 10, 300));

            var allClaimed = Steps(500, (10, true), (30, true));
            GachaPanel.MilestoneWindow(allClaimed, 5, out first, out last, out from);
            Assert.AreEqual(1, first, "모두 받았으면 마지막 단계");
            Assert.AreEqual(1, last);
            Assert.AreEqual(10, from);

            GachaPanel.MilestoneWindow(new List<GachaMilestoneStep>(), 5, out first, out last, out from);
            Assert.AreEqual(-1, first, "단계가 없으면 -1 (줄을 숨긴다)");
        }

        [Test]
        public void MilestoneTexts()
        {
            Assert.AreEqual("누적 소환 1,234", GachaPanel.MilestoneLine(1234));
            Assert.AreEqual("누적 소환 0", GachaPanel.MilestoneLine(-3));
            Assert.AreEqual("10회 · 별조각 100", GachaMilestonePopup.RowLabel(10, "별조각 100"));
            Assert.AreEqual("200회", GachaMilestonePopup.RowLabel(200, null));
            Assert.AreEqual("7회 남음", GachaMilestonePopup.RemainLabel(7));
            Assert.AreEqual("1회 남음", GachaMilestonePopup.RemainLabel(0), "닿기 직전에도 0회로 쓰지 않는다");
            StringAssert.Contains("첫 만남 소환을 세지 않아요", GachaMilestonePopup.FooterHint);
            StringAssert.Contains("확률과 천장은 그대로예요", GachaMilestonePopup.FooterHint);
        }

        [Test]
        public void MilestonePopup_FirstVisible_KeepsTwoClaimedAbove()
        {
            var steps = Steps(120, (10, true), (30, true), (50, true), (100, true), (200, false), (300, false));
            Assert.AreEqual(2, GachaMilestonePopup.FirstVisible(steps));
            Assert.AreEqual(0, GachaMilestonePopup.FirstVisible(Steps(0, (10, false), (30, false))));
            Assert.AreEqual(0, GachaMilestonePopup.FirstVisible(null));
        }

        [Test]
        public void FriendTicket_LegendaryOrLower_AndSubLine()
        {
            Assert.IsTrue(FriendTicketPopup.Eligible(new LineageDef { id = "a", grade = ItemGrade.Legendary }));
            Assert.IsTrue(FriendTicketPopup.Eligible(new LineageDef { id = "a", grade = ItemGrade.Common }));
            Assert.IsFalse(FriendTicketPopup.Eligible(new LineageDef { id = "a", grade = ItemGrade.Mythic }), "신화는 고를 수 없다");
            Assert.IsFalse(FriendTicketPopup.Eligible(new LineageDef { id = "", grade = ItemGrade.Rare }));
            Assert.IsFalse(FriendTicketPopup.Eligible(null));
            Assert.AreEqual("기억편 +30", FriendTicketPopup.SubLine(true, 30));
            Assert.AreEqual("새 친구", FriendTicketPopup.SubLine(false, 30));
        }

        // ───────────── RM-2 P4-5: 호감도 칸 (CR-10) ─────────────

        [Test]
        public void Affinity_StoryLevels_AndLabels()
        {
            Assert.AreEqual(2, AffinitySectionView.StoryLevel(0));
            Assert.AreEqual(10, AffinitySectionView.StoryLevel(4));
            Assert.AreEqual("Lv.3 호감도", AffinitySectionView.LevelLabel(3));
            Assert.AreEqual("MAX", AffinitySectionView.ProgressLabel(7200, 7000, 10));
            Assert.AreEqual("120/150", AffinitySectionView.ProgressLabel(120, 150, 2));
            Assert.AreEqual("40", AffinitySectionView.ProgressLabel(40, 0, 1), "다음 단계를 모르면 지금 값만");
            Assert.AreEqual(0.5f, AffinitySectionView.ProgressFill(100, 50, 150, 2), 1e-5f);
            Assert.AreEqual(1f, AffinitySectionView.ProgressFill(7000, 7000, 7000, 10));
            Assert.AreEqual(0f, AffinitySectionView.ProgressFill(10, 0, 0, 1));
            StringAssert.Contains("Lv.6에 들을 수 있어요", AffinitySectionView.LockedStoryLine(6));
        }

        [Test]
        public void Affinity_ClaimGems_TenTimesEachReachedLevel()
        {
            Assert.AreEqual(20, AffinitySectionView.ClaimGems(1, 2, 10), "Lv.2 = 20");
            Assert.AreEqual(20 + 30 + 40, AffinitySectionView.ClaimGems(1, 4, 10));
            Assert.AreEqual(50, AffinitySectionView.ClaimGems(4, 5, 10));
            Assert.AreEqual(0, AffinitySectionView.ClaimGems(5, 5, 10));
            Assert.AreEqual(540, AffinitySectionView.ClaimGems(1, 10, 10), "계보 하나 540 (SPEC4 §6.3)");
        }

        // ───────────── RM-2 P4-5: 열린 탭 안 잠금 (F-03) · 도감 알약 (Q-05) ─────────────

        [Test]
        public void LockedTexts_StageHintOrSentence()
        {
            Assert.IsTrue(CharacterUI.IsStageHint("1-10"));
            Assert.IsTrue(CharacterUI.IsStageHint("2-5"));
            Assert.IsFalse(CharacterUI.IsStageHint("튜토리얼"));
            Assert.IsFalse(CharacterUI.IsStageHint("3일째"));
            Assert.IsFalse(CharacterUI.IsStageHint(null));
            Assert.AreEqual("1-10 클리어 시", CharacterUI.LockedShort("1-10"));
            Assert.AreEqual("잠겨 있어요", CharacterUI.LockedShort("튜토리얼"));
            Assert.AreEqual("스테이지 2-5 클리어 시 열려요", CharacterUI.LockedLong("2-5", "x"));
            Assert.AreEqual("튜토리얼을 마치면 열려요", CharacterUI.LockedLong("튜토리얼", "튜토리얼을 마치면 열려요"));
            Assert.AreEqual("아직 열리지 않았어요", CharacterUI.LockedLong(string.Empty, null));
        }

        [Test]
        public void CodexLabel_FloorsPercent()
        {
            Assert.AreEqual("도감 <b>12</b>%", CharacterPanel.CodexLabel(12.9f));
            Assert.AreEqual("도감 <b>100</b>%", CharacterPanel.CodexLabel(140f));
            Assert.AreEqual("도감 <b>0</b>%", CharacterPanel.CodexLabel(-3f));
        }

        [Test]
        public void CardAlert_AnyLineageLeaf_IncludingAffinity()
        {
            // 친구들 탭 점의 계보 잎 넷 (진화 · 공명 · 의상 · 호감도) 중 하나면 카드 점도 켠다 — 받을 호감도 단계만 있어도 (CR-10)
            Assert.IsFalse(CharacterUI.CardAlert(false, false, false, false));
            Assert.IsTrue(CharacterUI.CardAlert(true, false, false, false), "진화 제작 가능");
            Assert.IsTrue(CharacterUI.CardAlert(false, true, false, false), "공명 가능");
            Assert.IsTrue(CharacterUI.CardAlert(false, false, true, false), "의상 해금 가능");
            Assert.IsTrue(CharacterUI.CardAlert(false, false, false, true), "받을 호감도 단계");
        }

        [Test]
        public void FirstMeetingSession_StaysUntilTheWindowCloses()
        {
            Assert.IsTrue(GachaResultPopup.FirstMeetingSession(true, false, false), "첫 만남 결과");
            Assert.IsTrue(GachaResultPopup.FirstMeetingSession(true, true, false), "열린 창에 첫 만남 결과");
            Assert.IsTrue(GachaResultPopup.FirstMeetingSession(false, true, true), "그 창에서 1회 더 소환 — 리본이 남아 칩을 계속 숨긴다");
            Assert.IsFalse(GachaResultPopup.FirstMeetingSession(false, false, true), "닫혔다 새로 연 창은 이번 결과만");
            Assert.IsFalse(GachaResultPopup.FirstMeetingSession(false, true, false), "보통 소환 창의 다시 소환");
        }

        [Test]
        public void NewCopy_HasNoBannedWords()
        {
            var texts = new[]
            {
                GachaMilestonePopup.FooterHint, GachaMilestonePopup.RowLabel(10, "별조각 100"), GachaMilestonePopup.RemainLabel(3),
                GachaResultPopup.CatchUpLine(9), GachaResultPopup.PowerLine(10), GachaResultPopup.MeltLine(3), GachaPanel.MilestoneLine(4),
                FriendTicketPopup.SubLine(true, 1), FriendTicketPopup.SubLine(false, 1),
                AffinitySectionView.LevelLabel(2), AffinitySectionView.LockedStoryLine(4),
                CharacterUI.LockedShort("1-10"), CharacterUI.LockedLong("1-10", null), CharacterPanel.CodexLabel(5f),
            };
            foreach (var text in texts)
            {
                StringAssert.DoesNotContain("습니다", text);
                StringAssert.DoesNotContain("개방", text);
                StringAssert.DoesNotContain("장비", text);
            }
        }

        // ───────────── M29 양피지 위 색 글자 (24px 이하 4.5:1) ─────────────

        /// <summary>WCAG 대비 (sRGB 값 → 선형 휘도)</summary>
        static float Contrast(UnityEngine.Color a, UnityEngine.Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05f) / (Math.Min(la, lb) + 0.05f);
        }

        static float Luminance(UnityEngine.Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        static float Linear(float v) => v <= 0.04045f ? v / 12.92f : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4f);

        [Test]
        public void InkTint_PastelTags_ReadOnParchment()
        {
            var pastels = new List<UnityEngine.Color>
            {
                UITheme.Guardian, UITheme.EchoGlow, UITheme.Gold, UITheme.Resonance, UITheme.Lamp, UITheme.Lavender,
            };
            foreach (var style in new[] { CombatStyle.Defense, CombatStyle.Assault, CombatStyle.Capture, CombatStyle.Snipe, CombatStyle.Support, CombatStyle.Heal })
                pastels.Add(UITheme.StyleColor(style));
            foreach (var trait in new[] { TraitType.Fabric, TraitType.Metal, TraitType.Light, TraitType.Sound, TraitType.Night, TraitType.Rain, TraitType.Charm })
                pastels.Add(UITheme.TraitColor(trait));
            foreach (var pastel in pastels)
            {
                float ratio = Contrast(CharacterSkillUI.InkTint(pastel), UITheme.Parchment);
                Assert.GreaterOrEqual(ratio, 4.5f, $"InkTint({UITheme.ColorTag(pastel)}) 양피지 대비 {ratio:0.00}");
            }
        }

        [Test]
        public void InkPink_ReadsOnParchmentAndQuotePill()
        {
            Assert.GreaterOrEqual(Contrast(CharacterUI.InkPink, UITheme.Parchment), 4.5f, "의상 감정 문구 (양피지)");
            // 스킬 카드 말풍선: 흰 알약에 (0.95, 0.82, 0.87) 알파 0.85를 양피지 위에 (CharacterSceneBuilder SkillCard 'Quote')
            var pill = UnityEngine.Color.Lerp(UITheme.Parchment, new UnityEngine.Color(0.95f, 0.82f, 0.87f), 0.85f);
            Assert.GreaterOrEqual(Contrast(CharacterUI.InkPink, pill), 4.5f, "스킬 카드 말풍선");
        }
    }
}
