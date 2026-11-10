using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-8 튜토리얼 (UX Wave 2) 순수 규칙 — 장면 없이 도우미 함수만 본다.
    ///   · M42 · UX-SPEC-IA §8.6: 대상 없는 코치 말풍선 자리 (SHEET = 탭 화면 위 끝 − 28, MAIN = 전투 영역 아래 끝 + 282에 아래 변) · 카드 높이 150~220
    ///     · 낮은 카드의 얼굴 줄임 (196 기준 비율 — 접시가 카드 안).
    ///   · M79: 동화 인트로 [건너뛰기 ›] 나타남 (1초 뒤 0.3초, 반쯤 보이면 눌림) · 안내 줄 숨 쉬기.
    ///   · M15: 첫 만남 카드 뒤로가기 토스트는 2초에 한 번.
    ///   · 단계 마침 소리(TutorialStep): 시작과 함께 끝나는 단계(완료 조건 '항상') · 인트로 · 졸업만 조용하다.
    ///   · (RM-2 P4-6) 튜토리얼 연출 표시: 1단계 = 첫인상, 4단계 = 첫 불 밝히기 느린 화면 · FtueBattleGuard에 리플렉션 없음.
    ///   · (RM-3 FIX-P4-6) 다시 만난 날 · 이벤트 도장 칸 날 글 = Ink (라벤더 띠 4.5:1 이상, RI-2) · 이벤트 미션 목록 아래 끝 (RI-3)
    ///     · 별빛 산책 수첩 30칸 의상 아이콘 (R65).
    /// 같은 파일의 FtueBattleApiTests는 SystemsTestBase로 가드 → 전투 API(보호 resolver · ForceLampFull)를 돌린다.
    /// 좌표는 1080x1920 기준 화면을 y 위쪽(아래 끝 = 0)으로 둔다 — 화면 위에서 잰 y는 FromTop.
    /// </summary>
    public class UiWave2_WP8Tests
    {
        static readonly Rect Frame = new(0f, 0f, 1080f, 1920f);
        static readonly Vector2 SmallBubble = new(720f, SpeechBubbleView.MinHeight);
        static readonly Vector2 TallBubble = new(720f, SpeechBubbleView.MaxHeight);

        /// <summary>다 열린 탭 화면 위 끝 (아래에서 탭 바 170 + 내용 780 = 950 → 화면 위에서 970)</summary>
        const float ContentTopOpen = UIFrame.TabBarHeight + UIFrame.SheetContentHeight;
        /// <summary>전투 영역 아래 끝 (아래에서 탭 바 170 → 화면 위에서 1750)</summary>
        const float StageBottom = UIFrame.TabBarHeight;

        static float FromTop(float y) => Frame.yMax - y;
        static float Top(Vector2 center, Vector2 size) => center.y + size.y * 0.5f;
        static float Bottom(Vector2 center, Vector2 size) => center.y - size.y * 0.5f;

        // ───────────── M42 대상 없는 말풍선 ─────────────

        [Test]
        public void FreeBubble_Sheet_TopSits28BelowContentTop()
        {
            var center = CoachMarkView.FreeBubbleCenter(true, ContentTopOpen, StageBottom, Frame, SmallBubble);
            Assert.AreEqual(998f, FromTop(Top(center, SmallBubble)), 0.01f, "위 변 = 탭 화면 위(970) + 28");
            Assert.AreEqual(1148f, FromTop(Bottom(center, SmallBubble)), 0.01f, "150 높이면 아래 변 1148 (≈996~1146)");
            Assert.AreEqual(540f, center.x, 0.01f, "가운데");
        }

        [Test]
        public void FreeBubble_Main_BottomAt1468_AboveGuideBar()
        {
            foreach (var size in new[] { SmallBubble, TallBubble })
            {
                var center = CoachMarkView.FreeBubbleCenter(false, 0f, StageBottom, Frame, size);
                float bottom = FromTop(Bottom(center, size));
                float top = FromTop(Top(center, size));
                Assert.AreEqual(1468f, bottom, 0.01f, $"높이 {size.y}: 아래 변 = 전투 아래(1750) − 282");
                Assert.Less(bottom, 1492f, "가이드 퀘스트 줄(1492~1576) 위");
                Assert.Greater(top, 276f, "스테이지 패널(142~276) · AUTO(144~220)와 겹치지 않는다");
                Assert.AreEqual(540f, center.x, 0.01f, "가운데, 너비 720 → 180~900 (빠른 아이콘 줄 12~116 · 964~1068 사이)");
            }
        }

        [Test]
        public void FreeBubble_SheetIgnoresStage_MainIgnoresContent()
        {
            var sheet = CoachMarkView.FreeBubbleCenter(true, ContentTopOpen, 9999f, Frame, SmallBubble);
            var main = CoachMarkView.FreeBubbleCenter(false, -9999f, StageBottom, Frame, SmallBubble);
            Assert.AreEqual(CoachMarkView.FreeBubbleCenter(true, ContentTopOpen, StageBottom, Frame, SmallBubble), sheet);
            Assert.AreEqual(CoachMarkView.FreeBubbleCenter(false, ContentTopOpen, StageBottom, Frame, SmallBubble), main);
        }

        [Test]
        public void FreeBubble_StaysInsideSafeArea()
        {
            // 탭 화면 위 끝이 안전 영역 밖 (이상한 화면) — 위 변이 안전 영역 위 끝에서 16 안쪽으로 잘린다
            var center = CoachMarkView.FreeBubbleCenter(true, Frame.yMax + 500f, StageBottom, Frame, SmallBubble);
            Assert.AreEqual(Frame.yMax - CoachMarkView.BubbleEdgeMargin, Top(center, SmallBubble), 0.01f);
            // 안전 영역이 말풍선보다 좁으면 자르지 않는다 (가운데 그대로)
            var narrow = new Rect(0f, 0f, 600f, 1920f);
            Assert.AreEqual(300f, CoachMarkView.FreeBubbleCenter(false, 0f, StageBottom, narrow, SmallBubble).x, 0.01f);
        }

        [Test]
        public void BubbleCard_HeightFitsText_150To220()
        {
            Assert.AreEqual(150f, SpeechBubbleView.CardHeight(0f), 0.01f, "빈 글도 가장 낮은 높이");
            Assert.AreEqual(150f, SpeechBubbleView.CardHeight(88f), 0.01f, "두 줄(30px ≈ 88)은 150");
            Assert.AreEqual(186f, SpeechBubbleView.CardHeight(130f), 0.01f, "세 줄 = 글 높이 + 56");
            Assert.AreEqual(220f, SpeechBubbleView.CardHeight(400f), 0.01f, "가장 높아도 220");
        }

        [Test]
        public void BubbleFace_ShrinksWithShortCard_StaysInsideCard()
        {
            Assert.AreEqual(1f, SpeechBubbleView.FaceScale(SpeechBubbleView.FaceDesignHeight), 1e-4f, "196 카드는 빌더 자리 그대로");
            Assert.AreEqual(1f, SpeechBubbleView.FaceScale(SpeechBubbleView.MaxHeight), 1e-4f, "220 카드에서도 키우지 않는다");
            Assert.AreEqual(150f / 196f, SpeechBubbleView.FaceScale(SpeechBubbleView.MinHeight), 1e-4f);

            // 빌더 자리 (TutorialSceneBuilder.BuildBubble — 카드 왼쪽 가운데 기준, 피벗도 왼쪽 가운데):
            // 접시 PortraitBg (20,4) 146 · 얼굴 Portrait (4,16) 176. FitFace는 자리와 크기를 같은 비율로 줄인다
            foreach (float height in new[] { SpeechBubbleView.MinHeight, 186f, SpeechBubbleView.FaceDesignHeight, SpeechBubbleView.MaxHeight })
            {
                float k = SpeechBubbleView.FaceScale(height);
                float half = height * 0.5f;
                Assert.LessOrEqual((4f + 73f) * k, half, $"{height}: 접시 위 끝이 카드 위 변 안");
                Assert.GreaterOrEqual((4f - 73f) * k, -half, $"{height}: 접시 아래 끝이 카드 아래 변 안");
                Assert.LessOrEqual((16f + 88f) * k - half, 6f + 0.01f, $"{height}: 얼굴이 위 변 위로 196 카드(6px)보다 더 솟지 않는다 (이름표 위 끝 24 아래)");
                Assert.LessOrEqual((4f + 176f) * k, 190f, $"{height}: 얼굴 오른쪽 끝이 글 영역(왼쪽 190) 앞");
            }
        }

        // ───────────── M79 동화 인트로 ─────────────

        [Test]
        public void IntroSkip_HiddenFirstSecond_ThenFadesIn()
        {
            Assert.AreEqual(0f, StoryIntroView.SkipAlpha(0f));
            Assert.AreEqual(0f, StoryIntroView.SkipAlpha(1f), "1초까지는 보이지 않는다 (01 화면)");
            Assert.AreEqual(0.5f, StoryIntroView.SkipAlpha(1.15f), 1e-4f);
            Assert.AreEqual(1f, StoryIntroView.SkipAlpha(1.3f), 1e-4f);
            Assert.AreEqual(1f, StoryIntroView.SkipAlpha(2.5f), "02 화면(2.5초)에는 다 보인다");

            Assert.IsFalse(StoryIntroView.SkipInteractable(StoryIntroView.SkipAlpha(1.1f)), "반쯤 보이기 전에는 누를 수 없다");
            Assert.IsFalse(StoryIntroView.SkipInteractable(0.5f));
            Assert.IsTrue(StoryIntroView.SkipInteractable(StoryIntroView.SkipAlpha(2.5f)));
        }

        [Test]
        public void IntroHint_HiddenWhileTyping_FadesIn_ThenBreathes()
        {
            Assert.AreEqual(0f, StoryIntroView.HintAlpha(-1f, false), "타자 중에는 숨김");
            Assert.AreEqual(0f, StoryIntroView.HintAlpha(0f, false), "다 나온 순간부터 나타나기 시작");
            Assert.Less(StoryIntroView.HintAlpha(0.15f, false), StoryIntroView.HintAlpha(0.3f, false) + 1e-4f, "0.3초 동안 나타난다");

            float min = 1f, max = 0f;
            for (float t = 0.3f; t < 0.3f + 2.8f; t += 0.01f)
            {
                float a = StoryIntroView.HintAlpha(t, false);
                min = Mathf.Min(min, a);
                max = Mathf.Max(max, a);
            }
            Assert.AreEqual(0.6f, min, 0.01f, "숨 쉬기 0.8 − 0.2");
            Assert.AreEqual(1f, max, 0.01f, "숨 쉬기 0.8 + 0.2");
            // 1.4초 주기 (sin(t·4.49))
            Assert.AreEqual(StoryIntroView.HintAlpha(0.5f, false), StoryIntroView.HintAlpha(0.5f + 2f * Mathf.PI / StoryIntroView.HintPulseSpeed, false), 1e-3f);
            Assert.AreEqual(1.4f, 2f * Mathf.PI / StoryIntroView.HintPulseSpeed, 0.01f);
        }

        [Test]
        public void IntroHint_ReducedMotion_DoesNotBreathe()
        {
            Assert.AreEqual(StoryIntroView.HintReducedAlpha, StoryIntroView.HintAlpha(1f, true), 1e-4f);
            Assert.AreEqual(StoryIntroView.HintReducedAlpha, StoryIntroView.HintAlpha(2.37f, true), 1e-4f);
            Assert.AreEqual(0f, StoryIntroView.HintAlpha(-1f, true));
        }

        // ───────────── M15 첫 만남 카드 뒤로가기 ─────────────

        [Test]
        public void FirstMeetingBackToast_ThrottledTo2Seconds()
        {
            Assert.IsTrue(FirstMeetingCardView.BackToastDue(0f, float.NegativeInfinity), "처음은 바로");
            Assert.IsFalse(FirstMeetingCardView.BackToastDue(11.9f, 10f), "2초 안은 토스트 없이 먹기만");
            Assert.IsTrue(FirstMeetingCardView.BackToastDue(12f, 10f));
            Assert.AreEqual("먼저 새 친구를 만나 볼까요?", FirstMeetingCardView.BackToast);
        }

        [Test]
        public void FirstMeeting_IsKeptAfterSkip()
        {
            Assert.IsTrue(FtueDirector.FirstMeetingKeptAfterSkip, "M74 3b: 건너뛴 뒤에도 첫 만남 카드가 남는다 (설정 확인 문구가 읽는다)");
        }

        [Test]
        public void FirstMeetingCover_IsNearlyOpaque()
        {
            // WP-8 r1 18 화면: 덮개 0.84에서는 카드 양옆으로 소환 시트 글이 비쳤다
            Assert.GreaterOrEqual(FirstMeetingCardView.CoverAlpha, 0.85f, "시트 글이 카드 양옆으로 비치지 않게");
            Assert.LessOrEqual(FirstMeetingCardView.CoverAlpha, 1f);
        }

        // ───────────── 시트 밑 대상 → 사냥 버튼 (UX-SPEC-IA §5.7 (a)) ─────────────

        [Test]
        public void CoachLine_RedirectToMain_SaysGoBack_ThenRestoresStepLine()
        {
            const string step = "잔향을 써서 잔향이를 불러요\n덱의 첫 칸을 톡 눌러 보세요";
            const string back = FtueDirector.RedirectToMainLine;
            Assert.AreSame(back, FtueDirector.CoachLine(step, back, true, true), "손가락이 사냥 버튼 — 말도 사냥 버튼 (WP-8 r1 04 화면)");
            Assert.AreSame(step, FtueDirector.CoachLine(step, back, false, true), "다음 찾기가 시트 밑이 아니면 단계 글로");
            Assert.AreSame(step, FtueDirector.CoachLine(step, back, true, false), "사냥 버튼이 없는 옛 탭 바 — 단계 글만");
            Assert.AreSame(step, FtueDirector.CoachLine(step, string.Empty, true, true), "돌아가자는 말이 비었으면 단계 글");
            StringAssert.Contains("사냥", back, "손가락이 가리키는 버튼 이름");
            StringAssert.EndsWith("요", back, "해요체");
        }

        // ───────────── 단계 마침 소리 (AUDIO-SPEC 61 TutorialStep) ─────────────

        static FtueStepDef StepOf(MetaCondition complete, bool hintOnly = false, FtueAction action = FtueAction.None) =>
            new() { complete = complete, hintOnly = hintOnly, action = action };

        [Test]
        public void StepDoneSound_SkipsOnlyStepsThatEndAsTheyStart()
        {
            Assert.IsTrue(FtueDirector.PlaysStepDoneSound(StepOf(MetaCondition.Count(CounterKey.EchoSummon, 1))), "2단계 잔향이 소환");
            Assert.IsTrue(FtueDirector.PlaysStepDoneSound(StepOf(MetaCondition.StageCleared(1, 1), hintOnly: true)),
                "5단계 첫 보스 — 토스트 단계라도 1-1 클리어에 끝나면 소리");
            Assert.IsTrue(FtueDirector.PlaysStepDoneSound(StepOf(MetaCondition.Count(CounterKey.ExpeditionClaim, 1), hintOnly: true)),
                "14단계 원정 받기");
            Assert.IsFalse(FtueDirector.PlaysStepDoneSound(StepOf(MetaCondition.Always(), hintOnly: true)), "3 · 8단계 — 시작과 함께 끝남");
            Assert.IsFalse(FtueDirector.PlaysStepDoneSound(StepOf(MetaCondition.Flag(MetaFlags.IntroSeen), action: FtueAction.StoryIntro)), "인트로");
            Assert.IsFalse(FtueDirector.PlaysStepDoneSound(StepOf(MetaCondition.Always(), action: FtueAction.Graduation)), "졸업");
            Assert.IsFalse(FtueDirector.PlaysStepDoneSound(null));
        }

        // ───────────── RM-2 P4-6: 튜토리얼 연출 표시 (SPEC4 §13.3 — AppSignals.FtueFirstImpression / FtueFirstLampBeat) ─────────────

        [Test]
        public void FtueSignals_ImpressionInStep1_LampBeatInStep4Only()
        {
            var battle = StepOf(MetaCondition.Count(CounterKey.EnemyKill, 3), action: FtueAction.BattleProtection);
            var lamp = StepOf(MetaCondition.Count(CounterKey.LampLit, 1), action: FtueAction.ForceLampFull);
            var summon = StepOf(MetaCondition.Count(CounterKey.EchoSummon, 1));

            Assert.IsTrue(FtueDirector.WantsFirstImpression(battle, true), "1단계 첫 전투 동안");
            Assert.IsFalse(FtueDirector.WantsFirstImpression(battle, false), "단계가 돌지 않으면(다음 trigger 대기 · 끝) 끈다");
            Assert.IsFalse(FtueDirector.WantsFirstImpression(summon, true));
            Assert.IsFalse(FtueDirector.WantsFirstImpression(lamp, true));
            Assert.IsFalse(FtueDirector.WantsFirstImpression(null, true));

            Assert.IsTrue(FtueDirector.WantsFirstLampBeat(lamp, true, true), "4단계 불 밝히기 안내 동안");
            Assert.IsFalse(FtueDirector.WantsFirstLampBeat(lamp, true, false), "단계가 끝나 게이지 안내를 내렸으면 끈다");
            Assert.IsFalse(FtueDirector.WantsFirstLampBeat(lamp, false, true));
            Assert.IsFalse(FtueDirector.WantsFirstLampBeat(battle, true, true), "다른 단계는 평소 점화");
            Assert.IsFalse(FtueDirector.WantsFirstLampBeat(null, true, true));
        }

        [Test]
        public void FtueBattleGuard_HoldsNoReflection()
        {
            // SPEC4 §13.3: 가드는 전투 훅(ProtectionResolver · ForceLampFull)으로 넘기고 System.Reflection을 쓰지 않는다
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var field in typeof(FtueBattleGuard).GetFields(all))
                Assert.IsFalse(typeof(System.Reflection.MemberInfo).IsAssignableFrom(field.FieldType), $"리플렉션 필드 {field.Name}");
            Assert.IsTrue(FtueBattleGuard.CanFillLamp, "전투 훅이 있어 늘 채울 수 있다");
            Assert.AreEqual(BattleProtection.Tutorial.EnemyHpScale, FtueBattleGuard.EnemyHpScale);
            Assert.AreEqual(BattleProtection.Tutorial.ShopFloorHp, FtueBattleGuard.ShopFloorHp);
            Assert.AreEqual(MetaSession.TutorialProtectionMaxStage, FtueBattleGuard.MaxProtectedStage);
        }

        // ───────────── RM-3 FIX-P4-6: 칸 날 글 대비 (RI-2) · 이벤트 미션 목록 (RI-3) ─────────────

        /// <summary>출석 칸 그림(Art/UI/ui_attendance_cell_9s) 위 라벤더 띠에서 잰 가장 어두운 · 밝은 색 (날 글이 앉는 줄)</summary>
        static readonly Color CellBandDark = new Color32(201, 157, 208, 255);
        static readonly Color CellBandLight = new Color32(213, 168, 215, 255);

        [Test]
        public void CellDayLabels_InkOnLavenderBand_AtLeast4_5()
        {
            Assert.AreEqual(UITheme.Ink, ComebackCard.DayLabelColor, "다시 만난 날 '{n}일째' — 오늘이든 아니든 Ink");
            Assert.AreEqual(UITheme.Ink, EventHubPopup.StampDayColor, "이벤트 도장 칸 '{n}일' — Ink");
            foreach (var band in new[] { CellBandDark, CellBandLight })
            {
                Assert.GreaterOrEqual(ReturnPopup.Contrast(ComebackCard.DayLabelColor, band), 4.5f, $"다시 만난 날 띠 {band}");
                Assert.GreaterOrEqual(ReturnPopup.Contrast(EventHubPopup.StampDayColor, band), 4.5f, $"도장 칸 띠 {band}");
            }
        }

        [Test]
        public void EventHubMissionList_ReachesFooter_ClearsSeasonTile()
        {
            float footerTop = EventHubPopup.FooterBottom + EventHubPopup.FooterHeight;
            float seasonTop = EventHubPopup.SeasonTileBottom + EventHubPopup.SeasonTileHeight;
            Assert.AreEqual(120f, EventHubPopup.ListBottomNoSeason, 0.01f, "수첩 칸이 없으면 상점 버튼(위 끝 104) 위 16");
            Assert.AreEqual(footerTop + 16f, EventHubPopup.ListBottomNoSeason, 0.01f);
            Assert.AreEqual(240f, EventHubPopup.ListBottomWithSeason, 0.01f, "수첩 칸이 보이면 그 칸(위 끝 228) 위 12");
            Assert.AreEqual(seasonTop + 12f, EventHubPopup.ListBottomWithSeason, 0.01f);
            Assert.GreaterOrEqual(EventHubPopup.SeasonTileBottom, footerTop, "수첩 칸은 아래 줄 버튼 위");
            Assert.LessOrEqual(UISize.ClaimAllFooterHeight, EventHubPopup.FooterHeight, "[모두 받기]도 같은 줄 높이 안");

            Assert.AreEqual(EventHubPopup.ListBottomNoSeason,
                EventHubPopup.ListBottom(false, EventHubPopup.ListBottomNoSeason, EventHubPopup.ListBottomWithSeason), 0.01f);
            Assert.AreEqual(EventHubPopup.ListBottomWithSeason,
                EventHubPopup.ListBottom(true, EventHubPopup.ListBottomNoSeason, EventHubPopup.ListBottomWithSeason), 0.01f);
            Assert.AreEqual(120f, EventHubPopup.ListBottom(true, 120f, 60f), 0.01f, "수첩 쪽 값이 더 낮아도 버튼 쪽보다 내려가지 않는다");

            // Body 1328 (창 1500 − 제목 136 − 아래 36), 도장 줄이 있을 때 목록 위 끝 664 → 목록 높이 544:
            // 미션 줄 150 세 개 + 간격 12 두 개 = 474가 다 보인다 (r3 캡처: 위 끝 664 · 아래 끝 240 = 424라 셋째 줄이 반 잘렸다)
            const float body = 1328f, topWithStamps = 664f, row = 150f;
            Assert.GreaterOrEqual(body - topWithStamps - EventHubPopup.ListBottomNoSeason, 3f * row + 2f * UISpace.ListGap);
        }

        [Test]
        public void SeasonTier30_ShowsCostumeIcon_OnlyWhenItGivesTheCostume()
        {
            var texture = new Texture2D(4, 4);
            var medal = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            var costume = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            try
            {
                var outfit = MetaReward.Costume("jjak_starwalk");
                Assert.AreSame(costume, SeasonPassPopup.TierIcon(30, 30, outfit, medal, costume), "30칸이 의상을 주면 의상 그림");
                Assert.AreSame(medal, SeasonPassPopup.TierIcon(30, 30, outfit, medal, null), "의상 그림이 비면(옛 장면 · 그림 없음) 메달 그대로");
                Assert.AreSame(medal, SeasonPassPopup.TierIcon(30, 30, MetaReward.Gem(50), medal, costume), "이미 가져 대신 보상(별조각)이면 메달");
                Assert.AreSame(medal, SeasonPassPopup.TierIcon(30, 30, null, medal, costume), "보상이 없으면 메달");
                Assert.AreSame(medal, SeasonPassPopup.TierIcon(29, 30, outfit, medal, costume), "마지막이 아닌 칸은 메달");
                Assert.AreSame(medal, SeasonPassPopup.TierIcon(25, 30, MetaReward.Gem(30), medal, costume), "강조 칸(★25)도 메달");
                Assert.AreSame(medal, SeasonPassPopup.TierIcon(1, 0, outfit, medal, costume), "칸 목록이 비면 메달");
            }
            finally
            {
                Object.DestroyImmediate(medal);
                Object.DestroyImmediate(costume);
                Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// RM-2 P4-6 (FTUE-01, SPEC4 §13.3 · S-9): FtueBattleGuard가 전투 API로 넘긴다 — 보호는 BattleSystem.ProtectionResolver → 시뮬레이터
    /// (적 체력 × 0.6 한 번만, 수선점 바닥 1), 게이지는 BattleSystem.ForceLampFull. 메타 연결이 넣은 resolver는 건드리지 않는다.
    /// </summary>
    public class FtueBattleApiTests : SystemsTestBase
    {
        readonly System.Collections.Generic.List<BattleOutcome> _ended = new();
        FtueBattleGuard _guard;

        [SetUp]
        public void SetUpGuard()
        {
            _ended.Clear();
            Battle.StageEnded += (outcome, _) => _ended.Add(outcome);
            MetaDb.ftueSteps = new System.Collections.Generic.List<FtueStepDef>
            {
                new FtueStepDef { id = "intro", complete = MetaCondition.Flag(MetaFlags.IntroSeen), action = FtueAction.StoryIntro },
                new FtueStepDef { id = "battle", complete = MetaCondition.Count(CounterKey.StageClear, 99), action = FtueAction.BattleProtection },
            };
            MetaDb.ClearCache();
            CreateMetaSystems();
            Tutorial.Begin();
            Battle.ProtectionResolver = null;
            _guard = new FtueBattleGuard { RequireMetaActive = false };
        }

        [TearDown]
        public void TearDownGuard() => _guard.Detach();

        void SetEnemies(float hp, float damage)
        {
            foreach (var enemy in BattleCfg.enemies)
            {
                enemy.hp = hp;
                enemy.damage = damage;
            }
        }

        void TickSeconds(float seconds)
        {
            for (float t = 0f; t < seconds && _ended.Count == 0; t += 0.1f) Battle.Tick(0.1f);
        }

        EnemyState Find(int id)
        {
            var enemies = Battle.Sim.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Id == id) return enemies[i];
            }
            return null;
        }

        [Test]
        public void Guard_PutsItsResolver_OnlyWhenBattleHasNone()
        {
            _guard.Attach(Battle, Tutorial);
            Assert.IsTrue(_guard.OwnsResolver, "메타 연결 전 · 테스트: 가드가 같은 규칙의 resolver를 넣는다");
            Assert.IsNotNull(Battle.ProtectionResolver);
            Assert.IsTrue(Battle.ProtectionResolver(1).Enabled, "인트로 중 1-1");
            Assert.IsFalse(Battle.ProtectionResolver(4).Enabled, "1-4부터는 끔");
            _guard.Detach();
            Assert.IsNull(Battle.ProtectionResolver, "자기가 넣은 것만 뺀다");

            System.Func<int, BattleProtection> meta = _ => BattleProtection.Off;
            Battle.ProtectionResolver = meta;
            _guard.Attach(Battle, Tutorial);
            Assert.IsFalse(_guard.OwnsResolver);
            Assert.AreSame(meta, Battle.ProtectionResolver, "메타 연결(MetaSession)이 넣은 resolver는 그대로");
            _guard.Detach();
            Assert.AreSame(meta, Battle.ProtectionResolver);
        }

        [Test]
        public void Guard_KeepsShopStanding_ThroughBattleProtection()
        {
            SetEnemies(1e6f, 1e6f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false;
            _guard.Attach(Battle, Tutorial);
            Assert.IsTrue(_guard.IsProtecting, "인트로 중에도 지킨다");

            TickSeconds(120f);

            Assert.IsTrue(Battle.Sim.IsProtected, "시뮬레이터가 보호를 건다 (BattleSetup.Protection)");
            Assert.IsEmpty(_ended, "FTUE 1-1은 질 수 없다");
            Assert.GreaterOrEqual(Battle.Sim.ShopHp, FtueBattleGuard.ShopFloorHp - 0.001f);
        }

        [Test]
        public void Guard_EnemiesWeakenedOnce_ByTheSimulator()
        {
            SetEnemies(100f, 1f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            float before = -1f, after = -1f;
            // 가드가 붙기 전 · 뒤에 구독한 쪽이 같은 적을 같은 체력으로 본다 (가드가 이벤트 안에서 더 깎지 않는다)
            Battle.Event += e =>
            {
                if (e.Type == BattleEventType.EnemySpawned && before < 0f) before = Find(e.SourceId).MaxHp;
            };
            _guard.Attach(Battle, Tutorial);
            Battle.Event += e =>
            {
                if (e.Type == BattleEventType.EnemySpawned && after < 0f) after = Find(e.SourceId).MaxHp;
            };

            for (float t = 0f; t < 30f && after < 0f; t += 0.1f) Battle.Tick(0.1f);

            Assert.Greater(before, 0f, "적이 나왔다");
            Assert.AreEqual(before, after, before * 0.001f, "두 번 약해지지 않는다");
            Assert.IsTrue(Battle.Sim.IsProtected);
            Assert.AreEqual(FtueBattleGuard.EnemyHpScale, Battle.Sim.Protection.EnemyHpScale, 1e-4f, "등장할 때 × 0.6은 시뮬레이터가");
        }

        [Test]
        public void Guard_IsOff_AfterFtue_BattleCanFail()
        {
            SetEnemies(1e6f, 1e6f);
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            Battle.AutoMode = false;
            _guard.Attach(Battle, Tutorial);
            Tutorial.Skip();
            Assert.IsFalse(_guard.IsProtecting, "튜토리얼이 끝나면 끔");

            TickSeconds(120f);

            Assert.IsFalse(Battle.Sim.IsProtected);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(BattleOutcome.Failed, _ended[0]);
        }

        [Test]
        public void TryFillLamp_UsesForceLampFull_OncePerGauge()
        {
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Echo, 0, "button_base");
            _guard.Attach(Battle, Tutorial);
            Battle.Tick(0.1f);
            Assert.IsFalse(Battle.Sim.CanLightLamp);

            Assert.IsTrue(_guard.TryFillLamp());

            Assert.IsTrue(Battle.Sim.CanLightLamp);
            Assert.AreEqual(1f, Battle.Sim.LampGauge, 1e-4f);
            Assert.IsFalse(_guard.TryFillLamp(), "이미 가득");
            _guard.Enabled = false;
            Assert.IsFalse(_guard.IsProtecting, "끄면 보호도 요청하지 않는다");
        }
    }
}
