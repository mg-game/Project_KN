using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 메타 기본 문구 (MetaText): 잠금 안내 UnlockShort / UnlockSentence (M39 — 도달 인덱스를 받아 클리어 스테이지로 쓴다),
    /// 단추 부족 안내 GoldShortfall (M08), 조사.
    /// </summary>
    public class MetaTextTests
    {
        [Test]
        public void UnlockSentence_UsesClearedStage_WithParticle()
        {
            Assert.AreEqual("스테이지 2-9를 클리어하면 열려요", MetaText.UnlockSentence(20), "도달 20(2-10) = 2-9 클리어");
            Assert.AreEqual("스테이지 1-3을 클리어하면 열려요", MetaText.UnlockSentence(4), "받침에 맞는 조사");
            Assert.AreEqual("스테이지 1-10을 클리어하면 열려요", MetaText.UnlockSentence(11));
            Assert.AreEqual(string.Empty, MetaText.UnlockSentence(1), "처음부터 열림");
        }

        [Test]
        public void UnlockShort_IsCompactPrefix_EmptyWhenOpenFromStart()
        {
            Assert.AreEqual(string.Empty, MetaText.UnlockShort(1));
            Assert.AreEqual(string.Empty, MetaText.UnlockShort(0));
            Assert.AreEqual("스테이지 1-1 클리어 시", MetaText.UnlockShort(2));
            Assert.AreEqual("스테이지 2-9 클리어 시", MetaText.UnlockShort(20));
            Assert.AreEqual("스테이지 2-9 클리어 시\n열려요", MetaText.UnlockShort(20) + "\n열려요", "카드 자물쇠 두 줄");
        }

        [Test]
        public void UnlockSentence_MatchesFeatureGateWording()
        {
            // FeatureGateSystem.GetRequirementText(StageCleared n) == UnlockSentence(n + 1)
            Assert.AreEqual("스테이지 1-4를 클리어하면 열려요", MetaText.UnlockSentence(MetaCondition.StageIndex(1, 4) + 1));
        }

        [Test]
        public void GoldShortfall_GapOnly_WhenSlowOrUnknownRate()
        {
            Assert.AreEqual("단추 1,234개가 더 필요해요", MetaText.GoldShortfall(2234, 1000, 0, 0));
            Assert.AreEqual("단추 1,234개가 더 필요해요", MetaText.GoldShortfall(2234, 1000, 1, 0), "10분(600초) 넘게 걸리면 시간을 적지 않는다");
            Assert.AreEqual("단추 2개가 더 필요해요", MetaText.GoldShortfall(100, 98.5, 0, 0), "보유 소수점 때문에 적게 말하지 않는다 (올림)");
        }

        [Test]
        public void GoldShortfall_UnitNotation_DropsCounter_AndUsesSoundParticle()
        {
            Assert.AreEqual("단추 999,999개가 더 필요해요", MetaText.GoldShortfall(999999, 0, 0, 0), "100만 미만은 그대로 (콤마 + 개)");
            Assert.AreEqual("단추 1.00B가 더 필요해요", MetaText.GoldShortfall(1e6, 0, 0, 0), "100만부터 단위 표기 — '개'를 붙이지 않는다");
            Assert.AreEqual("단추 1.50B가 더 필요해요", MetaText.GoldShortfall(2.5e6, 1e6, 0, 0));
            Assert.AreEqual("단추 1.00M이 더 필요해요", MetaText.GoldShortfall(1e39, 0, 0, 0), "단위 글자 소리(엠)에 맞춘 조사");
            Assert.AreEqual("단추 2.00B가 더 필요해요 · 약 20초", MetaText.GoldShortfall(2e6, 0, 1e5, 0), "예상 시간은 그대로 붙는다");
            Assert.AreEqual("단추 1.00B가 더 필요해요 · 바구니를 받아 보세요", MetaText.GoldShortfall(1e6, 0, 0, 1e6));
            StringAssert.DoesNotContain("개가", MetaText.GoldShortfall(3.3e9, 0, 0, 0));
        }

        [Test]
        public void Particles_ReadDigitsAndUnitLetters()
        {
            Assert.AreEqual("1.50B가", MetaText.WithSubject("1.50B"), "비");
            Assert.AreEqual("1.50M이", MetaText.WithSubject("1.50M"), "엠");
            Assert.AreEqual("1.50R이", MetaText.WithSubject("1.50R"), "알");
            Assert.AreEqual("스테이지 1-3이", MetaText.WithSubject("스테이지 1-3"));
            Assert.AreEqual("단추가", MetaText.WithSubject("단추"));
            Assert.AreEqual("별조각이", MetaText.WithSubject("별조각"));
            Assert.AreEqual("1.50B를", MetaText.WithObject("1.50B"));
            Assert.AreEqual("1.50N을", MetaText.WithObject("1.50N"), "엔");
            Assert.AreEqual("12.3AA를", MetaText.WithObject("12.3AA"), "에이");
            Assert.AreEqual("1.50L로", MetaText.WithDirection("1.50L"), "엘 = ㄹ 받침");
            Assert.AreEqual("1.50M으로", MetaText.WithDirection("1.50M"));
            Assert.AreEqual("1.50C로", MetaText.WithDirection("1.50C"));
            Assert.AreEqual("1-7로", MetaText.WithDirection("1-7"), "칠 = ㄹ 받침");
            Assert.AreEqual("1-3으로", MetaText.WithDirection("1-3"));
            Assert.AreEqual(string.Empty, MetaText.WithSubject(string.Empty));
        }

        [Test]
        public void GoldShortfall_AddsEta_UnderTenMinutes()
        {
            Assert.AreEqual("단추 120개가 더 필요해요 · 약 2분", MetaText.GoldShortfall(220, 100, 1, 0));
            Assert.AreEqual("단추 30개가 더 필요해요 · 약 30초", MetaText.GoldShortfall(130, 100, 1, 0));
            Assert.AreEqual("단추 599개가 더 필요해요 · 약 10분", MetaText.GoldShortfall(699, 100, 1, 0), "599초 = 반올림 10분");
        }

        [Test]
        public void GoldShortfall_SuggestsBasket_WhenItCoversTheGap()
        {
            Assert.AreEqual("단추 1,234개가 더 필요해요 · 바구니를 받아 보세요", MetaText.GoldShortfall(2234, 1000, 0, 1234));
            Assert.AreEqual("단추 1,234개가 더 필요해요", MetaText.GoldShortfall(2234, 1000, 0, 1233), "모자라면 권하지 않는다");
            Assert.AreEqual("단추 60개가 더 필요해요 · 약 1분 · 바구니를 받아 보세요", MetaText.GoldShortfall(160, 100, 1, 500));
        }

        [Test]
        public void GoldShortfall_NoGap_IsZero()
        {
            Assert.AreEqual("단추 0개가 더 필요해요", MetaText.GoldShortfall(100, 150, 10, 1000), "부르는 쪽이 부족할 때만 쓰지만 음수로 쓰지 않는다");
        }

        [Test]
        public void FlagText_DeckDragHint_IsDescribed()
        {
            Assert.AreEqual("hint.deck.drag", MetaFlags.DeckDragHint);
            Assert.AreNotEqual(MetaFlags.DeckDragHint, MetaText.FlagText(MetaFlags.DeckDragHint));
        }
    }
}
