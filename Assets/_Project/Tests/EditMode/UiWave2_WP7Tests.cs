using System.Collections.Generic;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-7 메타 (UX Wave 2) 순수 규칙 — 장면 없이 도우미 함수만 본다.
    ///   · M31: 복귀 화면 칸 줄 수 · 창 높이 (한 줄 1054 · 두 줄 1240), 말풍선 이름 색 대비 ≥ 4.5:1.
    ///   · M32: 수선 바구니 칸 리듬 (각주는 격자 아래 14 · 빠른 수선 판은 각주 아래 44 · 한 줄이면 창 1108).
    ///   · M73: 진화 · 의상으로 열 친구 고르기 (점 켜진 친구 → 짝짝이 → 처음 가진 친구 → 없음).
    ///   · M72: 일지 [가기 ›]가 탭으로 갈 때만 돌아가기 칩.
    ///   · 탭 자동 이동 (UI_InGame §4.3 공통 규칙, AutoTabRunner): 일지 · 도감 [모두 받기] 뒤 다음 받을 탭 · 열 때 첫 탭.
    ///   · M13 · M12: 강해지기 머리 글 · 권장 전투력 알약 글, M30: 시간당 표기.
    ///   · RM-2 P4-6 (SPEC4): 챕터 지도 칸 상태 · 진행 수, 알림 미리 묻기 조건, 이벤트 시작 카드 제목 · 회차 키, 다시 만난 날 본문,
    ///     챕터 상자 한 줄, 4차 팝업 종류, 도감 보너스 한 줄, 이벤트 모아 보기 글 (다음 시작 · 남은 시간 · 보여 줄 이벤트), 일지 줄 모델,
    ///     바구니 16시간 · 천장 글, 이/가, 강해지기 조언 줄 글, 첫 주 날 칩, 이벤트 'D-n', MAIN 배지 자리 셈 (화면 묶음 하나 = 자리 하나),
    ///     통합 r1: 복귀 칸 줄 가운데 · 지도 칸 이름표 자리 · 대비 · 잠긴 보스 칸 (+ ChapterChestPopupFillTests: 처음 Show의 상자 내용 칸).
    /// </summary>
    public class UiWave2_WP7Tests
    {
        // ───────────── M31 복귀 화면 ─────────────

        [Test]
        public void ReturnLayout_OneRow_Window1054()
        {
            ReturnPopup.Layout(3, out int rows, out float grid, out _, out float window);
            Assert.AreEqual(1, rows);
            Assert.AreEqual(170f, grid, 0.01f);
            Assert.AreEqual(1054f, window, 0.01f, "창 = 1240 − (356 − 170)");
        }

        [Test]
        public void ReturnLayout_SixFitOneRow_SevenWrap()
        {
            ReturnPopup.Layout(6, out int six, out _, out _, out float sixWindow);
            ReturnPopup.Layout(7, out int seven, out float sevenGrid, out _, out float sevenWindow);
            Assert.AreEqual(1, six);
            Assert.AreEqual(1054f, sixWindow, 0.01f);
            Assert.AreEqual(2, seven);
            Assert.AreEqual(356f, sevenGrid, 0.01f, "2·170 + 16");
            Assert.AreEqual(1240f, sevenWindow, 0.01f);
        }

        [Test]
        public void ReturnLayout_Empty_KeepsOneRow()
        {
            ReturnPopup.Layout(0, out int rows, out float grid, out float lineTop, out _);
            Assert.AreEqual(1, rows);
            Assert.AreEqual(170f, grid, 0.01f);
            Assert.Greater(lineTop, ReturnPopup.GridTop, "계보 줄은 격자 아래");
        }

        [Test]
        public void SpeakerColor_AlwaysReadableOnParchment()
        {
            var samples = new[] { Color.yellow, Color.white, UITheme.Gold, UITheme.Lavender, UITheme.DustyPink, new Color(0.2f, 0.6f, 1f) };
            foreach (var sample in samples)
            {
                var color = ReturnPopup.SpeakerColor(sample);
                Assert.GreaterOrEqual(ReturnPopup.Contrast(color, UITheme.Parchment), 4.5f, $"계보 색 {sample}");
            }
        }

        [Test]
        public void Contrast_BlackOnWhite_Is21()
        {
            Assert.AreEqual(21f, ReturnPopup.Contrast(Color.black, Color.white), 0.05f);
            Assert.AreEqual(1f, ReturnPopup.Contrast(Color.white, Color.white), 0.001f);
        }

        // ───────────── M32 수선 바구니 칸 리듬 ─────────────

        [Test]
        public void IdleChestRhythm_OneRowWithTicket()
        {
            IdleChestPopup.Rhythm(4, true, out float grid, out float footnote, out float quick, out float quickHeight, out float window);
            Assert.AreEqual(170f, grid, 0.01f);
            Assert.AreEqual(IdleChestPopup.GridTop + 170f + 14f, footnote, 0.01f, "각주 = 격자 아래 14 (간격 ≤ 20)");
            Assert.AreEqual(footnote + 44f, quick, 0.01f);
            Assert.AreEqual(204f, quickHeight, 0.01f);
            Assert.AreEqual(1108f, window, 0.01f, "136 + 판 아래 끝 + 24 + 116 + 36");
        }

        [Test]
        public void IdleChestRhythm_NoTicketShrinksPanel()
        {
            IdleChestPopup.Rhythm(4, true, out _, out _, out _, out _, out float withTicket);
            IdleChestPopup.Rhythm(4, false, out _, out _, out _, out float quickHeight, out float noTicket);
            Assert.AreEqual(180f, quickHeight, 0.01f);
            Assert.AreEqual(withTicket - 24f, noTicket, 0.01f);
        }

        [Test]
        public void IdleChestRhythm_SecondRowAdds186()
        {
            IdleChestPopup.Rhythm(6, true, out _, out _, out _, out _, out float oneRow);
            IdleChestPopup.Rhythm(7, true, out float grid, out _, out _, out _, out float twoRows);
            Assert.AreEqual(356f, grid, 0.01f);
            Assert.AreEqual(oneRow + 186f, twoRows, 0.01f);
            Assert.Less(twoRows, 1320f, "예전 고정 창 1320보다 작다");
        }

        [Test]
        public void FormatRate_FollowsM30()
        {
            Assert.AreEqual("3.12", IdleChestPopup.FormatRate(3.126));
            Assert.AreEqual("45.6", IdleChestPopup.FormatRate(45.67));
            Assert.AreEqual("1,800", IdleChestPopup.FormatRate(1800));
            // 1만부터는 칸 위 담긴 양과 같은 칸 표기 (FormatCompact) — 금테 칸 안에서 길어지지 않는다
            Assert.AreEqual("12.3A", IdleChestPopup.FormatRate(12345));
            Assert.AreEqual(NumberFormatter.FormatCompact(1550000), IdleChestPopup.FormatRate(1550000));
            Assert.AreEqual("0", IdleChestPopup.FormatRate(double.NaN));
        }

        [Test]
        public void RateText_OfflineOnlyStarIsSmall()
        {
            StringAssert.EndsWith("/시간<size=70%>*</size>", IdleChestPopup.RateText(5, true));
            Assert.AreEqual("+5/시간", IdleChestPopup.RateText(5, false));
        }

        // ───────────── M73 진화 · 의상으로 열 친구 ─────────────

        static readonly List<string> Owned = new() { "teddy", "jjakjjak", "button" };

        [Test]
        public void PickLineage_DotWins()
        {
            Assert.AreEqual("button", MetaNavigator.PickLineage(Owned, id => id == "button", "jjakjjak"));
        }

        [Test]
        public void PickLineage_NoDot_FallsBackToGuide()
        {
            Assert.AreEqual("jjakjjak", MetaNavigator.PickLineage(Owned, _ => false, "jjakjjak"));
            Assert.AreEqual("jjakjjak", MetaNavigator.PickLineage(Owned, null, "jjakjjak"));
        }

        [Test]
        public void PickLineage_GuideNotOwned_FirstOwned()
        {
            Assert.AreEqual("teddy", MetaNavigator.PickLineage(new List<string> { "teddy", "button" }, _ => false, "jjakjjak"));
        }

        [Test]
        public void PickLineage_NothingOwned_Null()
        {
            Assert.IsNull(MetaNavigator.PickLineage(new List<string>(), _ => true, "jjakjjak"));
            Assert.IsNull(MetaNavigator.PickLineage(null, _ => true, "jjakjjak"));
        }

        // ───────────── M72 일지 돌아가기 칩 ─────────────

        [Test]
        public void JournalReturnChip_OnlyForTabs()
        {
            Assert.IsTrue(JournalPopup.NavOpensTab(FeatureId.ShopUpgrade));
            Assert.IsTrue(JournalPopup.NavOpensTab(FeatureId.Formation));
            Assert.IsTrue(JournalPopup.NavOpensTab(FeatureId.Gacha));
            Assert.IsTrue(JournalPopup.NavOpensTab(FeatureId.Expedition));
            Assert.IsFalse(JournalPopup.NavOpensTab(FeatureId.Battle), "전투 화면");
            Assert.IsFalse(JournalPopup.NavOpensTab(FeatureId.Missions), "일지 자신");
            Assert.IsFalse(JournalPopup.NavOpensTab(FeatureId.Attendance), "팝업");
            Assert.IsFalse(JournalPopup.NavOpensTab(FeatureId.IdleChest), "팝업");
            Assert.IsFalse(JournalPopup.NavOpensTab(FeatureId.AutoBattle), "HUD 버튼");
        }

        // ───────────── M13 · M12 전투력 글 ─────────────

        [Test]
        public void StrengthenHeader_ShowsGapOnlyWhenShort()
        {
            string shortText = StrengthenPopup.HeaderText(12000, 9500);
            StringAssert.Contains("12,000", shortText);
            StringAssert.Contains("9,500", shortText);
            StringAssert.Contains("-2,500", shortText);
            StringAssert.DoesNotContain("-", StrengthenPopup.HeaderText(9000, 9500).Replace("</", string.Empty));
        }

        [Test]
        public void PowerPillText_HasMineAndRecommended()
        {
            string text = RecommendedPowerView.PillText(9500, 12000, PowerRating.Hard);
            StringAssert.Contains("<b>9,500</b>", text);
            StringAssert.Contains("/ 권장 12,000", text);
        }

        // ───────────── M04 가이드 줄 보상 양 ─────────────

        [Test]
        public void GuideRewardAmount_TimesForCurrency_EmptyForFriend()
        {
            StringAssert.StartsWith(UIFormat.TimesSmall, GuideQuestBarView.RewardAmountLabel(MetaReward.Currency(CurrencyType.Gem, 20)));
            Assert.AreEqual(string.Empty, GuideQuestBarView.RewardAmountLabel(new MetaReward { type = MetaRewardType.Lineage, itemId = "teddy", amount = 1 }));
            Assert.AreEqual(string.Empty, GuideQuestBarView.RewardAmountLabel(null));
        }

        // ───────────── M78 개방 카드 ─────────────

        [Test]
        public void UnlockCard_NotPresentingWithoutScene()
        {
            Assert.IsFalse(FeatureUnlockCard.IsPresenting(FeatureId.Missions));
            Assert.LessOrEqual(FeatureUnlockCard.FlightHoldMax, 0.8f, "§2.11 — 다음 팝업은 최대 0.8초만 붙잡는다");
        }

        // ───────────── RM-2 P4-6: 챕터 지도 (CR-05) ─────────────

        [Test]
        public void ChapterMap_NodeState_ClearedFrontierLocked()
        {
            // highest = 아직 깨지 않은 맨 앞 스테이지 (BattleSystem.HighestStage)
            Assert.AreEqual(ChapterMapPopup.NodeState.Cleared, ChapterMapPopup.StateOf(6, 7));
            Assert.AreEqual(ChapterMapPopup.NodeState.Frontier, ChapterMapPopup.StateOf(7, 7));
            Assert.AreEqual(ChapterMapPopup.NodeState.Locked, ChapterMapPopup.StateOf(8, 7));
        }

        [Test]
        public void ChapterMap_NodeToast_ReplayOnlyForClearedNodes()
        {
            Assert.AreEqual("1-7을 다시 지켜요", ChapterMapPopup.NodeToast(7, ChapterMapPopup.NodeState.Cleared));
            Assert.AreEqual("1-2를 다시 지켜요", ChapterMapPopup.NodeToast(2, ChapterMapPopup.NodeState.Cleared));
            Assert.AreEqual("1-8에 도전해요", ChapterMapPopup.NodeToast(8, ChapterMapPopup.NodeState.Frontier), "깨지 않은 맨 앞 칸에 '다시'를 쓰지 않는다");
        }

        [Test]
        public void ChapterMap_ChestMarkerOpensChestOnlyAfterBoss()
        {
            Assert.IsTrue(ChapterMapPopup.ChestOpensFromMap(1, 11, false), "1-10을 깼고 상자를 안 열었으면 지도에서 연다");
            Assert.IsFalse(ChapterMapPopup.ChestOpensFromMap(1, 10, false), "보스가 도전 중이면 말풍선만");
            Assert.IsFalse(ChapterMapPopup.ChestOpensFromMap(1, 25, true), "이미 연 상자");
            Assert.IsFalse(ChapterMapPopup.ChestOpensFromMap(2, 15, false), "2장 보스 전");
        }

        [Test]
        public void Journal_MissionsRoute_PicksDailyOrWeekly()
        {
            // FeatureId.Missions 이동(가이드 퀘스트 18 '일지 미션 2개 받기')은 [첫 주]가 아니라 미션 쪽으로 연다
            Assert.AreEqual(0, JournalPopup.MissionsTab(1, 2), "오늘 받을 것이 있으면 [오늘]");
            Assert.AreEqual(1, JournalPopup.MissionsTab(0, 2), "오늘 없고 이번 주에 있으면 [이번 주]");
            Assert.AreEqual(0, JournalPopup.MissionsTab(0, 0), "아무것도 없으면 [오늘]");
        }

        // 일지 탭 값: 오늘 0 · 이번 주 1 · 기록 2 · 첫 주 3. 화면 순서: 첫 주 → 오늘 → 이번 주 → 기록 (UI_InGame §4.3 탭 자동 이동)
        const int JDaily = 0, JWeekly = 1, JRecords = 2, JStarter = 3;
        static bool[] JVisible(bool records = true, bool starter = true) => new[] { true, true, records, starter };
        static int[] JClaimable(int daily = 0, int weekly = 0, int records = 0, int starter = 0) => new[] { daily, weekly, records, starter };

        [Test]
        public void Journal_AutoTab_StaysWhileCurrentTabHasRewards()
        {
            Assert.AreEqual(-1, JournalPopup.NextClaimTab(JDaily, JVisible(), JClaimable(daily: 1, weekly: 2)), "지금 탭에 남았으면 머문다 (상자 포함)");
            Assert.AreEqual(-1, JournalPopup.NextClaimTab(JDaily, JVisible(), JClaimable()), "아무 탭에도 없으면 머문다");
        }

        [Test]
        public void Journal_AutoTab_MovesRightInScreenOrder()
        {
            Assert.AreEqual(JWeekly, JournalPopup.NextClaimTab(JDaily, JVisible(), JClaimable(weekly: 1, records: 1)), "오늘 → 오른쪽 첫 받을 탭 [이번 주]");
            Assert.AreEqual(JRecords, JournalPopup.NextClaimTab(JDaily, JVisible(), JClaimable(records: 3, starter: 1)), "오늘 → 이번 주(없음) 건너 [기록]");
            Assert.AreEqual(JDaily, JournalPopup.NextClaimTab(JStarter, JVisible(), JClaimable(daily: 1, weekly: 1)), "첫 주 → [오늘]");
        }

        [Test]
        public void Journal_AutoTab_WrapsAroundOnce()
        {
            Assert.AreEqual(JStarter, JournalPopup.NextClaimTab(JRecords, JVisible(), JClaimable(daily: 1, starter: 1)), "기록(맨 끝) → 처음 [첫 주]");
            Assert.AreEqual(JDaily, JournalPopup.NextClaimTab(JWeekly, JVisible(), JClaimable(daily: 2)), "이번 주 → 기록(없음) → 첫 주(없음) → [오늘]");
        }

        [Test]
        public void Journal_AutoTab_SkipsHiddenTabs()
        {
            Assert.AreEqual(-1, JournalPopup.NextClaimTab(JDaily, JVisible(records: false), JClaimable(records: 2)), "잠긴 기록 탭은 건너뛴다");
            Assert.AreEqual(JDaily, JournalPopup.NextClaimTab(JRecords, JVisible(starter: false), JClaimable(daily: 1, starter: 1)), "끝난 첫 주 탭은 건너뛴다");
        }

        [Test]
        public void Journal_FirstClaimTab_OpensFirstTabWithRewards()
        {
            Assert.AreEqual(JStarter, JournalPopup.FirstClaimTab(JVisible(), JClaimable(daily: 1, starter: 1)), "첫 주에 있으면 [첫 주] (예전 규칙 그대로)");
            Assert.AreEqual(JWeekly, JournalPopup.FirstClaimTab(JVisible(), JClaimable(weekly: 1, records: 1)), "오늘 없고 이번 주에 있으면 [이번 주] (예전 규칙 그대로)");
            Assert.AreEqual(JRecords, JournalPopup.FirstClaimTab(JVisible(), JClaimable(records: 1)), "기록에만 있으면 [기록] (새로 넣음)");
            Assert.AreEqual(JDaily, JournalPopup.FirstClaimTab(JVisible(records: false), JClaimable(records: 1)), "기록이 잠겼으면 [오늘]");
            Assert.AreEqual(JDaily, JournalPopup.FirstClaimTab(JVisible(), JClaimable()), "아무것도 없으면 [오늘]");
        }

        [Test]
        public void Codex_AutoTab_MovesToNextTabWithRewards()
        {
            // 도감 탭 값 = 화면 순서: 친구 0 · 의상 1 · 적 2 · 시너지 3 · 재료 4 · 이야기 5 (스크린샷 사례: 시너지 6 · 재료 2)
            Assert.AreEqual(4, CodexPopup.NextClaimTab(3, new[] { 0, 0, 0, 0, 2, 0 }), "[시너지]를 다 받으면 [재료]로");
            Assert.AreEqual(-1, CodexPopup.NextClaimTab(3, new[] { 0, 0, 0, 1, 2, 0 }), "[시너지]에 남았으면 머문다");
            Assert.AreEqual(0, CodexPopup.NextClaimTab(4, new[] { 1, 0, 0, 0, 0, 0 }), "[재료] → [이야기](없음) → 처음 [친구]");
            Assert.AreEqual(-1, CodexPopup.NextClaimTab(4, new[] { 0, 0, 0, 0, 0, 0 }), "아무 탭에도 없으면 머문다");
        }

        [Test]
        public void AutoTabRunner_GuardsBadInput()
        {
            int[] order = { 2, 0, 1 };
            Assert.AreEqual(1, AutoTabRunner.NextTab(0, order, null, new[] { 0, 1, 1 }), "화면 순서 2 → 0 → 1: [0] 오른쪽은 [1]");
            Assert.AreEqual(2, AutoTabRunner.NextTab(1, order, null, new[] { 0, 0, 1 }), "[1](맨 끝) → 처음 [2]");
            Assert.AreEqual(-1, AutoTabRunner.NextTab(5, order, null, new[] { 0, 1, 1 }), "없는 탭 값이면 머문다");
            Assert.AreEqual(-1, AutoTabRunner.NextTab(0, null, null, new[] { 0, 1, 1 }), "순서가 없으면 머문다");
            Assert.AreEqual(2, AutoTabRunner.FirstTab(order, null, new[] { 1, 0, 1 }, 0), "열 때: 화면 순서로 첫 받을 탭 [2]");
            Assert.AreEqual(0, AutoTabRunner.FirstTab(order, new[] { true, true, false }, new[] { 1, 0, 1 }, 7), "숨은 [2]는 건너 [0]");
            Assert.AreEqual(7, AutoTabRunner.FirstTab(order, null, new[] { 0, 0, 0 }, 7), "없으면 fallback");
        }

        [Test]
        public void ChapterMap_ChapterAndStageMath()
        {
            Assert.AreEqual(1, ChapterMapPopup.ChapterOf(1));
            Assert.AreEqual(1, ChapterMapPopup.ChapterOf(10));
            Assert.AreEqual(2, ChapterMapPopup.ChapterOf(11));
            Assert.AreEqual(1, ChapterMapPopup.ChapterOf(0), "0 이하는 1장");
            Assert.AreEqual(17, ChapterMapPopup.StageOf(2, 7));
            Assert.AreEqual("2-7", BattleSystem.FormatStage(ChapterMapPopup.StageOf(2, 7)));
            Assert.AreEqual(0, ChapterMapPopup.ClearedCount(2, 11), "2장 첫 칸이 맨 앞이면 0/10");
            Assert.AreEqual(6, ChapterMapPopup.ClearedCount(1, 7));
            Assert.AreEqual(10, ChapterMapPopup.ClearedCount(1, 25), "다 깬 챕터는 10");
        }

        [Test]
        public void ChapterMap_LayoutHasTenNodesInsideTheMap()
        {
            Assert.AreEqual(ChapterMapPopup.NodeCount, ChapterMapPopup.MapNodeLayout.Length);
            for (int i = 0; i < ChapterMapPopup.MapNodeLayout.Length; i++)
            {
                var p = ChapterMapPopup.MapNodeLayout[i];
                // 칸 96 + 아래 이름 32가 판(868×988) 안에 들어온다
                Assert.That(p.x * 868f, Is.InRange(56f, 812f), $"칸 {i + 1} x");
                Assert.That(p.y * 988f, Is.InRange(88f, 940f), $"칸 {i + 1} y");
            }
        }

        [Test]
        public void ChapterMap_FeaturesInChapter_UsesStageClearedConditions()
        {
            var features = new List<FeatureUnlockDef>
            {
                new() { feature = FeatureId.Codex, conditions = { MetaCondition.Of(MetaConditionType.StageCleared, 10) } },
                new() { feature = FeatureId.AutoChallenge, conditions = { MetaCondition.Of(MetaConditionType.StageCleared, 11) } },
                new() { feature = FeatureId.Events, conditions = { MetaCondition.Of(MetaConditionType.StageCleared, 21) } },
            };
            var into = new List<FeatureId>();
            Assert.AreEqual(1, ChapterMapPopup.FeaturesInChapter(features, 1, into));
            Assert.AreEqual(FeatureId.Codex, into[0]);
            Assert.AreEqual(1, ChapterMapPopup.FeaturesInChapter(features, 2, into));
            Assert.AreEqual(FeatureId.AutoChallenge, into[0]);
        }

        // ───────────── RM-2 P4-6: 알림 미리 묻기 (NOTI-01) ─────────────

        [Test]
        public void NotificationAsk_Rules()
        {
            const long now = 1_800_000_000L;
            const long week = NotificationAskPopup.AskCooldownSeconds;
            Assert.IsTrue(NotificationAskPopup.ShouldAsk(NotificationPermission.Unknown, 0, 0, now), "처음");
            Assert.IsTrue(NotificationAskPopup.ShouldAsk(NotificationPermission.SoftDeclined, 1, now - week, now), "한 번 '괜찮아요' 뒤 7일");
            Assert.IsFalse(NotificationAskPopup.ShouldAsk(NotificationPermission.SoftDeclined, 1, now - week + 1, now), "7일이 안 됨");
            Assert.IsFalse(NotificationAskPopup.ShouldAsk(NotificationPermission.SoftDeclined, 2, 0, now), "두 번까지만");
            Assert.IsFalse(NotificationAskPopup.ShouldAsk(NotificationPermission.Granted, 0, 0, now));
            Assert.IsFalse(NotificationAskPopup.ShouldAsk(NotificationPermission.Denied, 0, 0, now), "거절은 설정에서만 다시");
            StringAssert.DoesNotContain("습니다", NotificationAskPopup.BodyText);
        }

        // ───────────── RM-2 P4-6: 이벤트 시작 · 다시 만난 날 · 챕터 상자 ─────────────

        [Test]
        public void EventStart_TitleAndInstanceKey()
        {
            Assert.AreEqual("evt_weekend_expedition", EventStartCard.EventIdOf("evt_weekend_expedition@2026-11-07"));
            Assert.AreEqual("evt_x", EventStartCard.EventIdOf("evt_x"));
            Assert.IsNull(EventStartCard.EventIdOf(null));
            Assert.IsNull(EventStartCard.EventIdOf("@2026-11-07"));
            Assert.AreEqual("주말 원정 축제가 시작됐어요!", EventStartCard.TitleFor("주말 원정 축제"));
            Assert.AreEqual("별이 머문 밤 산책이 시작됐어요!", EventStartCard.TitleFor("별이 머문 밤 산책"));
            Assert.AreEqual("새 이벤트가 시작됐어요!", EventStartCard.TitleFor(null));
        }

        [Test]
        public void Comeback_BodyCountsWholeDays_AtLeastThree()
        {
            Assert.AreEqual("3일 만이에요. 친구들이 수선점을 지키며 기다렸어요.", ComebackCard.BodyFor(259_200));
            StringAssert.StartsWith("5일 만이에요", ComebackCard.BodyFor(5 * 86_400 + 3_600));
            StringAssert.StartsWith("3일 만이에요", ComebackCard.BodyFor(10), "72시간 아래로 부르면 3일");
        }

        [Test]
        public void ChapterChest_LineUsesObjectParticle()
        {
            Assert.AreEqual("망각의 큰그림자를 물리치고 찾은 상자예요", ChapterChestPopup.LineFor("망각의 큰그림자"));
            Assert.AreEqual("좀나방 여왕을 물리치고 찾은 상자예요", ChapterChestPopup.LineFor("좀나방 여왕"));
            Assert.AreEqual("챕터 보스를 물리치고 찾은 상자예요", ChapterChestPopup.LineFor(null));
        }

        [Test]
        public void Presenter_Spec4Kinds()
        {
            Assert.IsTrue(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.Comeback));
            Assert.IsTrue(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.EventStart));
            Assert.IsTrue(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.ChapterChest));
            Assert.IsTrue(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.StoryPage));
            Assert.IsTrue(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.NotificationAsk));
            Assert.IsFalse(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.FeatureUnlock));
            Assert.IsFalse(MetaPopupPresenter.IsSpec4Kind(MetaPopupKind.Return));
            Assert.IsFalse(MetaPopupPresenter.IsCardParked(FeatureId.Codex), "장면 없이 대기 카드 없음");
        }

        // ───────────── RM-2 P4-6: 도감 (Q-05) ─────────────

        [Test]
        public void Codex_BonusLine_UsesTierTotals()
        {
            var config = new CodexConfig
            {
                formTiers =
                {
                    new CodexBonusTier { count = 5, percent = 1 }, new CodexBonusTier { count = 10, percent = 3 }, new CodexBonusTier { count = 15, percent = 5 },
                    new CodexBonusTier { count = 20, percent = 7 }, new CodexBonusTier { count = 25, percent = 8 }, new CodexBonusTier { count = 29, percent = 10 },
                },
                synergyTiers = { new CodexBonusTier { count = 10, percent = 5 }, new CodexBonusTier { count = 20, percent = 10 } },
                allEnemiesShopHpPercent = 5,
            };
            Assert.AreEqual("모은 형태 20종 · 친구 공격력·체력 +7%", CodexPopup.BonusLine(CodexCategory.Friend, 20, 29, config));
            Assert.AreEqual("모은 형태 4종 · 친구 공격력·체력 +0%", CodexPopup.BonusLine(CodexCategory.Friend, 4, 29, config));
            Assert.AreEqual("적 9종 모두 만나면 수선점 내구도 +5%", CodexPopup.BonusLine(CodexCategory.Enemy, 3, 9, config));
            Assert.AreEqual("켜 본 시너지 12종 · 불빛 충전 +5%", CodexPopup.BonusLine(CodexCategory.Synergy, 12, 25, config));
            Assert.AreEqual(10f, CodexPopup.TierPercent(config.formTiers, 29));
            Assert.AreEqual(0f, CodexPopup.TierPercent(null, 29));
            Assert.AreEqual(CodexPopup.TabCount, CodexPopup.TabNames.Length);
        }

        // ───────────── RM-2 P4-6: 이벤트 모아 보기 (EVT-01) ─────────────

        [Test]
        public void EventHub_NextStartAndHours()
        {
            Assert.AreEqual("토요일 새벽 5시부터", EventHubPopup.NextStartLabel(new EventDef { scheduleType = EventScheduleType.Weekdays, weekdayMask = (1 << 5) | (1 << 6) }));
            Assert.AreEqual("수요일 새벽 5시부터", EventHubPopup.NextStartLabel(new EventDef { scheduleType = EventScheduleType.Weekdays, weekdayMask = 1 << 2 }));
            Assert.AreEqual("매달 1일 새벽 5시부터", EventHubPopup.NextStartLabel(new EventDef { scheduleType = EventScheduleType.MonthDays, fromDay = 1, toDay = 14 }));
            Assert.AreEqual("밤 9시부터", EventHubPopup.NextStartLabel(new EventDef { scheduleType = EventScheduleType.DailyHours, fromHour = 21, toHour = 2 }));
            Assert.AreEqual("새벽 2시", EventHubPopup.HourLabel(2));
            Assert.AreEqual("아침 8시", EventHubPopup.HourLabel(8));
            Assert.AreEqual("낮 12시", EventHubPopup.HourLabel(12));
            Assert.AreEqual("밤 12시", EventHubPopup.HourLabel(0));
            Assert.AreEqual("곧 끝나요", EventHubPopup.TimeLeftLabel(System.TimeSpan.Zero));
            Assert.AreEqual("2일 3시간 남았어요", EventHubPopup.TimeLeftLabel(new System.TimeSpan(2, 3, 10, 0)));
            StringAssert.EndsWith("남았어요", EventHubPopup.TimeLeftLabel(System.TimeSpan.FromHours(5)));
            StringAssert.DoesNotContain("습니다", EventHubPopup.NightKeeperLine);
        }

        [Test]
        public void EventHub_PicksEventWithMissionsOrStamps()
        {
            var weekend = new ActiveEventStatus { Def = new EventDef { id = "evt_weekend" }, InstanceKey = "evt_weekend@2026-11-07" };
            var walk = new ActiveEventStatus
            {
                Def = new EventDef { id = "evt_walk", stamps = { new EventStampDef { day = 1 } } },
                InstanceKey = "evt_walk@2026-11-01",
            };
            var night = new ActiveEventStatus { Def = new EventDef { id = "evt_night" } };
            Assert.AreSame(walk, EventHubPopup.PickFeatured(new List<ActiveEventStatus> { weekend, night, walk }));
            Assert.AreSame(weekend, EventHubPopup.PickFeatured(new List<ActiveEventStatus> { weekend, night }), "미션 · 도장 없으면 첫 이벤트");
            Assert.IsNull(EventHubPopup.PickFeatured(new List<ActiveEventStatus>()));
        }

        // ───────────── RM-2 P4-6: 일지 줄 모델 (Q-04 · NEW-01) ─────────────

        [Test]
        public void JournalRowModel_FromAchievement()
        {
            var def = new AchievementDef { id = "ach_stage_2", title = "스테이지 3-10 클리어", hint = "비 오는 날", navFeature = FeatureId.Battle };
            var claimable = JournalRowModel.FromAchievement(new AchievementLineStatus { Current = def, TierIndex = 1, TierCount = 4, Value = 30, Target = 30, Claimable = true });
            Assert.AreEqual(JournalRowState.Claimable, claimable.State);
            Assert.AreEqual(2, claimable.TierReached, "받은 1단계 + 받을 수 있는 2단계");
            Assert.AreEqual(4, claimable.TierCount);
            Assert.IsNull(claimable.Hint);
            var hidden = JournalRowModel.FromAchievement(new AchievementLineStatus { Current = def, TierCount = 1, HiddenUnrevealed = true });
            Assert.AreEqual("???", hidden.Text);
            Assert.AreEqual("비 오는 날", hidden.Hint);
            Assert.AreEqual(JournalRowState.InProgress, hidden.State, "이동 없는 진행 중");
            var done = JournalRowModel.FromAchievement(new AchievementLineStatus { Current = def, TierIndex = 3, TierCount = 4, AllDone = true });
            Assert.AreEqual(JournalRowState.Done, done.State);
            Assert.AreEqual(1f, done.Ratio);
            Assert.AreEqual(4, done.TierReached);
        }

        [Test]
        public void JournalRowModel_FromStarterTaskAndEventMission()
        {
            var task = new StarterTaskDef { id = "sw_1_1", text = "스테이지 1-10 클리어", points = 5, navFeature = FeatureId.Battle };
            var locked = JournalRowModel.FromStarterTask(new StarterTaskStatus { Def = task, Locked = true, Value = 0, Target = 1 }, "1-7");
            Assert.AreEqual(JournalRowState.Locked, locked.State);
            Assert.AreEqual("+5", locked.PointsText);
            Assert.AreEqual("1-7", locked.LockText);
            var ready = JournalRowModel.FromStarterTask(new StarterTaskStatus { Def = task, Done = true, Value = 1, Target = 1 });
            Assert.AreEqual(JournalRowState.Claimable, ready.State);
            var mission = new EventMissionDef { id = "m1", text = "별빛 사탕 50개 모으기", target = 50 };
            var row = JournalRowModel.FromEventMission(new EventMissionStatus { Def = mission, Progress = 12, Target = 50 });
            Assert.AreEqual(JournalRowState.InProgress, row.State);
            Assert.AreEqual(0.24f, row.Ratio, 0.001f);
            Assert.AreEqual(JournalRowState.Go, JournalRowModel.Pending(FeatureId.Shop, null));
            Assert.AreEqual("12,340", MissionRowView.CompactCount(12_340));
            Assert.AreEqual(NumberFormatter.FormatCompact(1_550_000), MissionRowView.CompactCount(1_550_000));
        }

        // ───────────── RM-2 P4-6: 바구니 글 (IDLE-01 C.6) · 조사 ─────────────

        [Test]
        public void IdleChest_CapNoteAndCappedGrowthLine()
        {
            Assert.AreEqual("수선점 Lv.200이 되면 바구니가 16시간까지 늘어나요", IdleChestPopup.ShopLevelCapNote(200, 16f));
            Assert.AreEqual("다음 스테이지를 클리어하면 시간당 +0.25%", IdleChestPopup.GrowthLine(0.25f, false));
            Assert.AreEqual("다음 스테이지를 클리어하면 재료 시간당 +2%", IdleChestPopup.GrowthLine(2f, true));
            Assert.AreEqual("Lv.2가", MetaUi.WithSubject("Lv.2"));
            Assert.AreEqual("짝짝이가", MetaUi.WithSubject("짝짝이"));
            Assert.AreEqual("단추가", MetaUi.WithSubject("단추"));
            Assert.AreEqual("수선 천이", MetaUi.WithSubject("수선 천"));
        }

        // ───────────── RM-2 P4-6: 강해지기 조언 줄 (PRG-03 C.5) ─────────────

        [Test]
        public void Strengthen_AdviceRowText_FollowsC5()
        {
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.ShopUpgrade, Amount = 5, Gain = 1234 }, 10_000, out var title, out var desc);
            Assert.AreEqual("수선점 5번 강화", title, "n = StrengthenRow.Amount (P4-1 b11)");
            Assert.AreEqual("전투력 +1,234", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.ShopUpgrade, Gain = 10 }, 10_000, out title, out _);
            Assert.AreEqual("수선점 강화", title, "횟수가 없으면 숫자 없이");
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.ShopUpgrade, TargetId = "5", Gain = 10 }, 10_000, out title, out _);
            Assert.AreEqual("수선점 강화", title, "TargetId는 수로 읽지 않는다");
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.LineageLevel, TargetId = "no_such", Amount = 3, Gain = 50 }, 10_000, out title, out desc);
            Assert.AreEqual("친구 레벨 +3", title, "C.5 '{이름} 레벨 +{n}' (이름을 못 찾으면 '친구')");
            Assert.AreEqual("전투력 +50", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.LineageLevel, Gain = 50 }, 10_000, out title, out _);
            Assert.AreEqual("친구 레벨 올리기", title);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.WeakDirection, TargetId = "왼쪽 아래", Amount = 62, Slot = 2 }, 10_000, out title, out desc);
            Assert.AreEqual("약한 방향 보강 · 왼쪽 아래", title, "§7.2 · C.5");
            Assert.AreEqual("지난 패배 피해의 62%가 이쪽으로 왔어요", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.WeakDirection, TargetId = "위쪽" }, 10_000, out _, out desc);
            Assert.AreEqual("편성에서 그 방향 칸을 채워 봐요", desc);
            Assert.AreEqual(2f, StrengthenPopup.WeakSlotPulseSeconds, "패배 분석 고리와 같은 반짝 2초");
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.AutoFormation, Gain = 600 }, 10_000, out title, out desc);
            Assert.AreEqual("자동 편성으로 바꾸기", title);
            Assert.AreEqual("지금보다 6% 강해요 · 전투력 +600", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.EvolutionReady }, 10_000, out title, out desc);
            Assert.AreEqual("재료가 모두 모였어요", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.MissingMaterial }, 10_000, out title, out desc);
            Assert.AreEqual("재료가 조금 모자라요", title);
            // C.5 FillSlot · SynergyOneShort (P4-1 Role · CandidateId — 이름을 못 찾으면 이름 없이)
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.FillSlot, Role = CharacterRole.Echo, TargetId = "no_such", CandidateId = "no_such", Gain = 500 },
                10_000, out title, out desc);
            Assert.AreEqual("빈 잔향이 자리 채우기", title);
            Assert.AreEqual("전투력 +500", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.FillSlot, Role = CharacterRole.Guardian, Gain = 500 }, 10_000, out title, out _);
            Assert.AreEqual("빈 지킴이 자리 채우기", title);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.SynergyOneShort, TargetId = "no_such", CandidateId = "no_such", Gain = 500 },
                10_000, out title, out desc);
            Assert.AreEqual("시너지 한 명만 더", title);
            Assert.AreEqual("한 명 더 세우면 켜져요 · 전투력 +500", desc);
            StrengthenPopup.RowText(new StrengthenRow { Kind = StrengthenKind.SynergyOneShort }, 10_000, out _, out desc);
            Assert.AreEqual("한 명 더 세우면 켜져요", desc);
            StringAssert.DoesNotContain("습니다", StrengthenPopup.EmptyText);
            Assert.AreEqual("지금은 더 할 일이 없어요. 바구니와 원정으로 재료를 모아 봐요", StrengthenPopup.EmptyText);
            Assert.AreEqual(4, StrengthenPopup.MaxRows);
        }

        // ───────────── RM-2 P4-6: 첫 주 날 칩 · 이벤트 'D-n' ─────────────

        [Test]
        public void StarterWeek_OpenDaysAndLockedText()
        {
            // StarterWeekSystem.DayIndex는 1부터 센다 (시작한 날 = 1, IsDayOpen(day) = day ≤ DayIndex) — 내일 칩은 잠겨 있어야 한다
            Assert.AreEqual(1, StarterWeekPanelView.OpenDays(0), "진행 중이 아니면(0) 1일만");
            Assert.AreEqual(1, StarterWeekPanelView.OpenDays(1), "첫날은 1일만 열림");
            Assert.AreEqual(2, StarterWeekPanelView.OpenDays(2), "둘째 날 = 1~2일 (3~7일 잠김)");
            Assert.AreEqual(3, StarterWeekPanelView.OpenDays(3));
            Assert.AreEqual(7, StarterWeekPanelView.OpenDays(7));
            Assert.AreEqual(7, StarterWeekPanelView.OpenDays(12), "7일째 뒤로는 모두 열림");
            Assert.AreEqual("4일째에 열려요", StarterWeekPanelView.LockedDayText(4));
            Assert.AreEqual(StarterWeekPanelView.Days, JournalPopup.StarterDays);
        }

        [Test]
        public void EventSubPill_DaysLeftRoundsUp()
        {
            Assert.AreEqual(0, QuickIconView.DaysLeft(System.TimeSpan.Zero));
            Assert.AreEqual(1, QuickIconView.DaysLeft(System.TimeSpan.FromHours(3)));
            Assert.AreEqual(1, QuickIconView.DaysLeft(System.TimeSpan.FromDays(1)));
            Assert.AreEqual(14, QuickIconView.DaysLeft(new System.TimeSpan(13, 2, 0, 0)));
            Assert.AreEqual("D-3", QuickIconView.SubPillText(3));
        }

        [Test]
        public void MainBadgeSeats_OnePerScreenCluster_RailFirst()
        {
            // V2 r5 main: 우편 + ≡ (한 묶음) · 이벤트 · 출석부 · 일지 칸 · 탭 둘 → 탭 하나만 (묶음 5)
            int left = MainBadgeBudget.Seat(true, 3, false, 2, out int rail, out bool basket, out int tabs);
            Assert.AreEqual(3, rail, "빠른 아이콘 받을 것 칸은 상단 바 다음에 모두 앉는다");
            Assert.IsFalse(basket);
            Assert.AreEqual(1, tabs);
            Assert.AreEqual(0, left);
            // V2 r5 battle_later: 바구니 점 + 같은 줄 셋 → 탭은 쉰다
            left = MainBadgeBudget.Seat(true, 3, true, 1, out rail, out basket, out tabs);
            Assert.AreEqual(3, rail);
            Assert.IsTrue(basket, "바구니는 탭보다 먼저");
            Assert.AreEqual(0, tabs);
            Assert.AreEqual(0, left);
            Assert.AreEqual(MainBadgeBudget.MaxMain, MainBadgeBudget.Seat(false, 0, false, 0, out _, out _, out _), "빈 화면은 NEW 자리 5");
            Assert.AreEqual(1, MainBadgeBudget.Seat(true, 1, true, 1, out _, out _, out _), "자리가 남으면 NEW 하나");

            for (int top = 0; top < 2; top++)
            for (int cells = 0; cells <= 6; cells++)
            for (int b = 0; b < 2; b++)
            for (int t = 0; t <= 6; t++)
            {
                left = MainBadgeBudget.Seat(top == 1, cells, b == 1, t, out rail, out basket, out tabs);
                int used = top + rail + (basket ? 1 : 0) + tabs;
                string state = $"상단 {top} · 칸 {cells} · 바구니 {b} · 탭 {t}";
                Assert.LessOrEqual(used, MainBadgeBudget.MaxMain, state);
                Assert.AreEqual(MainBadgeBudget.MaxMain, used + left, state + " — 자리는 모두 셈에 든다");
                Assert.LessOrEqual(rail, cells, state);
                Assert.LessOrEqual(tabs, t, state);
                if (basket || tabs > 0) Assert.AreEqual(cells, rail, state + " — 빠른 아이콘 칸이 먼저");
                if (tabs > 0) Assert.AreEqual(b == 1, basket, state + " — 바구니가 탭보다 먼저");
            }
        }

        [Test]
        public void Season_LevelLine()
        {
            Assert.AreEqual("Lv.3 · 20/90", SeasonPassPopup.LevelLine(3, 290, 90, 30));
            Assert.AreEqual("Lv.30 · 최고 단계", SeasonPassPopup.LevelLine(30, 2700, 90, 30));
            Assert.AreEqual("Lv.0 · 0/90", SeasonPassPopup.LevelLine(0, 0, 90, 0));
            StringAssert.DoesNotContain("습니다", SeasonPassPopup.SourceLine);
        }

        // ───────────── 통합 r1 ─────────────

        /// <summary>복귀 칸 (r1 축제 사탕 7칸): 줄마다 가운데 — 덜 찬 마지막 줄도 왼쪽에 붙지 않는다. 칸 내용과 계보 말풍선 사이 12 이상</summary>
        [Test]
        public void ReturnChips_PartialLastRowCentered()
        {
            var cell = new Vector2(ReturnPopup.ChipWidth, ReturnPopup.ChipHeight);
            var gap = new Vector2(ReturnPopup.ChipGap, ReturnPopup.ChipGap);
            float width = ReturnPopup.ChipsPerRow * cell.x + (ReturnPopup.ChipsPerRow - 1) * gap.x;
            Assert.AreEqual(848f, width, 0.01f, "장면 격자 너비");

            Assert.AreEqual(0f, ReturnPopup.ChipOffset(0, 6, width, cell, gap).x, 0.01f, "6칸 한 줄은 격자를 꽉 채운다");
            Assert.AreEqual(width - cell.x, ReturnPopup.ChipOffset(5, 6, width, cell, gap).x, 0.01f);

            var seventh = ReturnPopup.ChipOffset(6, 7, width, cell, gap);
            Assert.AreEqual((width - cell.x) * 0.5f, seventh.x, 0.01f, "7칸 = 6 + 1: 일곱째는 둘째 줄 가운데");
            Assert.AreEqual(cell.y + gap.y, seventh.y, 0.01f, "둘째 줄");

            float left = ReturnPopup.ChipOffset(6, 9, width, cell, gap).x;
            float right = width - (ReturnPopup.ChipOffset(8, 9, width, cell, gap).x + cell.x);
            Assert.AreEqual(left, right, 0.01f, "9칸 = 6 + 3: 둘째 줄 셋이 가운데");
            Assert.AreEqual((width - (3 * cell.x + 2 * gap.x)) * 0.5f, ReturnPopup.ChipOffset(0, 3, width, cell, gap).x, 0.01f, "3칸 한 줄도 가운데");

            Assert.GreaterOrEqual(ReturnPopup.ChipHeight - ReturnPopup.ChipContentHeight - ReturnPopup.LineTuck, 12f, "칸 내용 ↔ 계보 말풍선 12 이상");
        }

        /// <summary>
        /// 챕터 지도 (r1): 칸 이름표는 밤 알약 위 (글자 색 모두 4.5:1 넘음), 모든 이름표가 판 안쪽 아래 끝 + 24 위 (1번 칸은 올린다),
        /// 잠긴 보스 칸은 α0.85 이상 (자물쇠와 함께), 다른 잠긴 칸은 예전 α0.55
        /// </summary>
        [Test]
        public void ChapterMap_LabelsOnPills_InsideMap_BossNotFaded()
        {
            const float nodesHeight = 1000f - 12f;
            for (int i = 0; i < ChapterMapPopup.NodeCount; i++)
            {
                float lift = ChapterMapPopup.LabelLift(i, nodesHeight);
                float bottom = ChapterMapPopup.MapNodeLayout[i].y * nodesHeight - ChapterMapPopup.NodeSize * 0.5f + lift - ChapterMapPopup.LabelPillHeight;
                Assert.GreaterOrEqual(lift, ChapterMapPopup.LabelOverlap, $"{i + 1}번 칸: 이름표는 칸 아래 테두리에 겹친다");
                Assert.Less(lift, ChapterMapPopup.NodeSize * 0.5f, $"{i + 1}번 칸: 이름표는 칸 아래쪽에 남는다");
                Assert.GreaterOrEqual(bottom, ChapterMapPopup.LabelMinBottom - 0.01f, $"{i + 1}번 칸: 이름표 아래 끝이 판 안쪽 아래 끝 + 24 위");
            }
            Assert.Greater(ChapterMapPopup.LabelLift(0, nodesHeight), ChapterMapPopup.LabelOverlap, "1번 칸 이름표는 올린다 (r1 '2-1')");
            Assert.AreEqual(ChapterMapPopup.LabelOverlap, ChapterMapPopup.LabelLift(5, nodesHeight), 0.001f, "다른 칸은 그대로");

            var pill = new Color32(39, 33, 79, 255); // ui_pill_night_9s 가운데 색
            Assert.GreaterOrEqual(ReturnPopup.Contrast(ChapterMapPopup.LabelColor(ChapterMapPopup.NodeState.Cleared, false), pill), 4.5f, "깬 칸 Cream");
            Assert.GreaterOrEqual(ReturnPopup.Contrast(ChapterMapPopup.LabelColor(ChapterMapPopup.NodeState.Frontier, true), pill), 4.5f, "지금 칸 Gold");
            Assert.GreaterOrEqual(ReturnPopup.Contrast(ChapterMapPopup.LabelColor(ChapterMapPopup.NodeState.Locked, false), pill), 4.5f, "잠긴 칸 SubText");
            Assert.AreNotEqual(ChapterMapPopup.LabelColor(ChapterMapPopup.NodeState.Cleared, false),
                ChapterMapPopup.LabelColor(ChapterMapPopup.NodeState.Locked, false), "잠긴 칸은 색으로 구별");

            Assert.GreaterOrEqual(ChapterMapPopup.NodeAlpha(ChapterMapPopup.NodeState.Locked, true), 0.85f, "잠긴 보스 칸은 흐리게 지우지 않는다");
            Assert.AreEqual(ChapterMapPopup.LockedAlpha, ChapterMapPopup.NodeAlpha(ChapterMapPopup.NodeState.Locked, false), 0.001f);
            Assert.AreEqual(1f, ChapterMapPopup.NodeAlpha(ChapterMapPopup.NodeState.Cleared, true), 0.001f);
            Assert.AreEqual(1f, ChapterMapPopup.NodeAlpha(ChapterMapPopup.NodeState.Frontier, false), 0.001f);
        }
    }

    /// <summary>
    /// 챕터 상자 창 (통합 r1, SPEC4 §8.5 내용 칸): IdleSceneBuilder가 모든 팝업을 꺼 둔 채 저장해서 처음 Show 때는 Awake가 아직 돌지 않았다 —
    /// 내용 칸이 비어 보였다. EditMode에서는 Awake가 저절로 돌지 않으므로 '꺼진 창에 처음 Show'를 그대로 흉내 낸다.
    /// 그 뒤 Awake가 돌아도 (실제 게임: Open → SetActive) 칸이 두 벌이 되지 않는다.
    /// </summary>
    public class ChapterChestPopupFillTests : SystemsTestBase
    {
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        GameObject _root;

        [SetUp]
        public void SetUpGame()
        {
            MetaDb.lostChest.chapterChests.Add(new ChapterChestDef
            {
                chapter = 1, rewards = new List<MetaReward> { MetaReward.Gem(100), MetaReward.Item("m_cloth", 10), MetaReward.Item("m_thread", 2) },
            });
            MetaDb.ClearCache();
            var content = new GameContent
            {
                Config = Config,
                Items = Items,
                Characters = CharDb,
                GachaTables = new[] { Table },
                Battle = BattleCfg,
                Expeditions = ExpeditionDb,
                Shops = ShopDb,
                Synergies = SynergyDb,
                Meta = MetaDb,
            };
            Game.Setup(PlayerData.CreateNew(Config), content, Clock, new System.Random(7));
        }

        [TearDown]
        public void TearDownGame()
        {
            Game.Shutdown();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
        }

        [Test]
        public void Show_BeforeAwake_FillsContents_AndAwakeKeepsThePool()
        {
            Assert.IsTrue(MetaUi.IsActive, "Game.Setup = 메타 켜짐");
            // 꺼진 부모 (여는 연출 코루틴을 돌리지 않는다) 아래 꺼진 창 (IdleSceneBuilder가 모든 팝업을 꺼 둔다)
            _root = new GameObject("PopupRoot");
            _root.SetActive(false);
            var go = new GameObject("ChapterChestPopup", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            go.SetActive(false);
            var popup = go.AddComponent<ChapterChestPopup>();
            var rewards = new GameObject("Rewards", typeof(RectTransform)).GetComponent<RectTransform>();
            rewards.SetParent(go.transform, false);
            var template = new GameObject("Reward", typeof(RectTransform)).AddComponent<MetaRewardChip>();
            template.transform.SetParent(rewards, false);
            SetField(popup, "_rewardRoot", rewards);
            SetField(popup, "_rewardTemplate", template);

            int expected = Game.ChapterChests.PeekChapterChest(1).Count;
            Assert.AreEqual(3, expected);
            Assert.IsTrue(popup.Show(1));
            Assert.AreEqual(expected, ActiveChips(rewards, template), "처음 Show (Awake 전)에도 내용 칸이 PeekChapterChest만큼 찬다");
            Assert.IsFalse(template.gameObject.activeSelf, "틀은 꺼진다");

            var awake = typeof(ChapterChestPopup).GetMethod("Awake", Private);
            Assert.IsNotNull(awake, "ChapterChestPopup.Awake");
            awake.Invoke(popup, null);
            Assert.IsTrue(popup.Show(1));
            Assert.AreEqual(expected, ActiveChips(rewards, template), "Awake가 풀을 새로 만들지 않는다 (칸이 두 벌이 되지 않는다)");
            Assert.IsFalse(Game.ChapterChests.IsChapterChestOpened(1), "보여 주기만 — 받지 않는다");
        }

        static int ActiveChips(Transform root, MetaRewardChip template)
        {
            int count = 0;
            foreach (Transform child in root)
            {
                if (child != template.transform && child.gameObject.activeSelf && child.GetComponent<MetaRewardChip>() != null) count++;
            }
            return count;
        }

        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Private);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }
    }
}
