using NUnit.Framework;

namespace IdleGame.Tests
{
    public class NumberFormatterTests
    {
        [TestCase(0, "0")]
        [TestCase(999.9, "999")]
        [TestCase(1000, "1.00A")]
        [TestCase(1234, "1.23A")]
        [TestCase(12345, "12.3A")]
        [TestCase(123456, "123A")]
        [TestCase(999999, "999A")]
        [TestCase(1e6, "1.00B")]
        [TestCase(1.999e6, "1.99B")]
        [TestCase(-1500, "-1.50A")]
        [TestCase(1e78, "1.00Z")]
        [TestCase(1e81, "1.00AA")]
        public void Format(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.Format(value));
        }

        [TestCase(1, "A")]
        [TestCase(26, "Z")]
        [TestCase(27, "AA")]
        [TestCase(52, "AZ")]
        [TestCase(53, "BA")]
        public void GetUnit(int group, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.GetUnit(group));
        }

        [TestCase(1300, "1,300")]
        [TestCase(999999, "999,999")]
        [TestCase(2_500_000, "2.50B")]
        public void FormatInt(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.FormatInt(value));
        }

        [TestCase(0, "0")]
        [TestCase(999.9, "999")]
        [TestCase(1234.5, "1,234")]
        [TestCase(999999, "999,999")]
        [TestCase(1e6, "1.00B")]
        [TestCase(1.55e6, "1.55B")]
        [TestCase(-1500, "-1,500")]
        [TestCase(double.NaN, "0")]
        public void FormatCurrency(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.FormatCurrency(value));
        }

        [TestCase(0, "0")]
        [TestCase(9999, "9,999")]
        [TestCase(10000, "10A")]
        [TestCase(12345, "12.3A")]
        [TestCase(100000, "100A")]
        [TestCase(999999, "999A")]
        [TestCase(1e6, "1B")]
        [TestCase(1.5e6, "1.5B")]
        [TestCase(1.55e6, "1.55B")]
        [TestCase(1e81, "1AA")]
        [TestCase(-12345, "-12.3A")]
        [TestCase(double.NaN, "0")]
        public void FormatCompact(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormatter.FormatCompact(value));
        }

        [Test]
        public void FormatCompact_NeverPadsTrailingZeros_AndFitsFiveGlyphs()
        {
            // 한 글자 단위(A~Z)까지: 칩 폭 5글자 안, 소수점 뒤 끝자리 0 없음 ("1.00A" / "10.0A" 금지)
            for (double v = 1; v < 1e78; v *= 1.37)
            {
                string text = NumberFormatter.FormatCompact(v);
                Assert.LessOrEqual(text.Length, 5, $"{v} → {text}");
                StringAssert.DoesNotMatch(@"\.\d*0(?!\d)", text, $"{v} → {text}");
                StringAssert.DoesNotMatch(@"\.[A-Z]", text, $"{v} → {text}");
            }
        }

        [Test]
        public void RewardFormatAmount_UsesCurrencyFormat_ForEveryCurrency()
        {
            Assert.AreEqual("1,200", RewardService.FormatAmount(CurrencyType.Gold, 1200));
            Assert.AreEqual("999,999", RewardService.FormatAmount(CurrencyType.Gold, 999999.7));
            Assert.AreEqual("1.00B", RewardService.FormatAmount(CurrencyType.Gold, 1e6));
            Assert.AreEqual("1,300", RewardService.FormatAmount(CurrencyType.Gem, 1300));
            Assert.AreEqual("12.3A", RewardService.FormatAmountCompact(CurrencyType.Gold, 12345));
            Assert.AreEqual("9,999", RewardService.FormatAmountCompact(CurrencyType.EventToken, 9999));
        }

        [TestCase("골드", "골드가")]
        [TestCase("젬", "젬이")]
        public void KoreanSubjectParticle(string word, string expected)
        {
            Assert.AreEqual(expected, KoreanText.WithSubject(word));
        }
    }
}
