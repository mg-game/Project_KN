using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using IdleGame.UI;
using Newtonsoft.Json;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 세이브 코드 가져오기 (SAVE-01, SPEC4 §4.2): 내보내기 → 읽기 왕복, 한 글자 바뀜 → Checksum, 쓰레기 · 머리말 틀림 → BadFormat,
    /// 빈 코드 → Empty, 버전 99 → NewerVersion, 5 MB → TooLarge, 체크섬은 맞는데 JSON이 아님 → Corrupt. 실패해도 예외를 내지 않는다.
    /// </summary>
    public class SaveCodeImportTests
    {
        static PlayerData MakeSave()
        {
            var data = new PlayerData { level = 87, accountId = "1a2b3c4d5e6f", lastSaveUnix = 1_793_000_000 };
            data.currencies[CurrencyType.Gold] = 1.5e12;
            data.currencies[CurrencyType.Gem] = 320;
            data.items["r1"] = 4;
            data.lineages["sock"] = new LineageSave { level = 12 };
            data.lineages["button"] = new LineageSave { level = 3 };
            data.battle.stage = 24;
            data.battle.highestStage = 24;
            data.flags.Add("ftue.done");
            data.Sanitize();
            return data;
        }

        /// <summary>JSON을 Export와 같은 형식(gzip + CRC32, "LL1.{crc}.{base64}")으로 싼다 — 시험용으로 버전 · 내용을 마음대로 넣으려고</summary>
        static string Pack(string json)
        {
            var raw = Encoding.UTF8.GetBytes(json);
            byte[] packed;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
                packed = output.ToArray();
            }
            return $"LL1.{Crc32(packed):x8}.{Convert.ToBase64String(packed)}";
        }

        static uint Crc32(byte[] bytes)
        {
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in bytes)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
            return ~crc;
        }

        [Test]
        public void RoundTrip_ExportThenParse_EqualsOriginal()
        {
            var original = MakeSave();
            string code = SaveCode.Export(original);

            bool ok = SaveCode.TryParse(code, out var parsed, out var error, out var summary);

            Assert.IsTrue(ok, error.ToString());
            Assert.AreEqual(SaveCodeError.None, error);
            Assert.AreEqual(JsonConvert.SerializeObject(original), JsonConvert.SerializeObject(parsed), "Sanitize 뒤 JSON이 같아야 한다");
            Assert.AreEqual(24, summary.highestStage);
            Assert.AreEqual(87, summary.shopLevel);
            Assert.AreEqual(2, summary.lineages);
            Assert.AreEqual(1_793_000_000, summary.savedUnix);
            Assert.AreEqual("1a2b3c4d", summary.accountIdShort);
        }

        [Test]
        public void RoundTrip_ThreeArgOverload_AndTryDecode_Agree()
        {
            string code = SaveCode.Export(MakeSave());

            Assert.IsTrue(SaveCode.TryParse(code, out var parsed, out var error));
            Assert.AreEqual(SaveCodeError.None, error);
            Assert.AreEqual(87, parsed.level);
            Assert.IsTrue(SaveCode.TryDecode(code, out string json));
            StringAssert.Contains("\"level\":87", json);
        }

        [Test]
        public void SurroundingWhitespaceAndLineBreaks_AreIgnored()
        {
            string code = SaveCode.Export(MakeSave());
            int mid = code.Length / 2;
            string messy = "  \n" + code.Substring(0, mid) + "\r\n" + code.Substring(mid) + "\n ";

            Assert.IsTrue(SaveCode.TryParse(messy, out _, out var error), error.ToString());
        }

        [Test]
        public void OneFlippedBase64Char_IsChecksum()
        {
            string code = SaveCode.Export(MakeSave());
            int payload = code.IndexOf('.', 4) + 1;
            int at = payload + (code.Length - payload) / 2;
            char flipped = code[at] == 'A' ? 'B' : 'A';
            string broken = code.Substring(0, at) + flipped + code.Substring(at + 1);

            Assert.IsFalse(SaveCode.TryParse(broken, out var data, out var error, out var summary));
            Assert.AreEqual(SaveCodeError.Checksum, error);
            Assert.IsNull(data);
            Assert.AreEqual(default(SaveSummary), summary);
        }

        [Test]
        public void CutShort_IsChecksumOrBadFormat_NeverThrows()
        {
            string code = SaveCode.Export(MakeSave());
            string cut = code.Substring(0, code.Length - 9);

            Assert.DoesNotThrow(() => SaveCode.TryParse(cut, out _, out _, out _));
            SaveCode.TryParse(cut, out _, out var error, out _);
            Assert.That(error, Is.EqualTo(SaveCodeError.Checksum).Or.EqualTo(SaveCodeError.BadFormat));
        }

        [TestCase("hello world")]
        [TestCase("LL1.")]
        [TestCase("LL1.zzzzzzzz.AAAA")]
        [TestCase("LL1.0000.AAAA")]
        [TestCase("LL1.12345678.***not base64***")]
        [TestCase("LL2.12345678.H4sIAAAAAAAAA")]
        [TestCase("{\"level\":3}")]
        public void Garbage_OrWrongPrefix_IsBadFormat(string code)
        {
            Assert.IsFalse(SaveCode.TryParse(code, out var data, out var error, out _));
            Assert.AreEqual(SaveCodeError.BadFormat, error);
            Assert.IsNull(data);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n ")]
        public void Empty_IsEmpty(string code)
        {
            Assert.IsFalse(SaveCode.TryParse(code, out var data, out var error, out _));
            Assert.AreEqual(SaveCodeError.Empty, error);
            Assert.IsNull(data);
        }

        [Test]
        public void Version99_IsNewerVersion_CheckedBeforeSanitize()
        {
            string code = Pack("{\"version\":99,\"level\":5,\"futureOnly\":{\"x\":[1,2,3]}}");

            Assert.IsFalse(SaveCode.TryParse(code, out var data, out var error, out _));
            Assert.AreEqual(SaveCodeError.NewerVersion, error);
            Assert.IsNull(data);
        }

        [Test]
        public void VersionAfterOtherFields_IsStillNewerVersion()
        {
            string code = Pack("{\"level\":5,\"battle\":{\"stage\":3},\"version\":99}");

            SaveCode.TryParse(code, out _, out var error, out _);
            Assert.AreEqual(SaveCodeError.NewerVersion, error);
        }

        [Test]
        public void OlderVersion_IsMigrated_ToCurrent()
        {
            string code = Pack("{\"version\":3,\"level\":9,\"battle\":{\"stage\":5,\"highestStage\":7}}");

            Assert.IsTrue(SaveCode.TryParse(code, out var data, out var error, out var summary), error.ToString());
            Assert.AreEqual(PlayerData.CurrentVersion, data.version);
            Assert.AreEqual(9, summary.shopLevel);
            Assert.AreEqual(7, summary.highestStage);
        }

        [Test]
        public void FiveMegabytes_IsTooLarge()
        {
            var data = MakeSave();
            data.noticeHiddenDate = new string('x', 5 * 1024 * 1024);
            string code = SaveCode.Export(data);

            Assert.IsFalse(SaveCode.TryParse(code, out var parsed, out var error, out _));
            Assert.AreEqual(SaveCodeError.TooLarge, error);
            Assert.IsNull(parsed);
            Assert.IsFalse(SaveCode.TryDecode(code, out _), "검사도 같은 한도를 쓴다");
        }

        [Test]
        public void HugeClipboard_IsTooLarge_WithoutDecoding()
        {
            string code = "LL1.12345678." + new string('A', 6 * 1024 * 1024);

            SaveCode.TryParse(code, out _, out var error, out _);
            Assert.AreEqual(SaveCodeError.TooLarge, error);
        }

        [Test]
        public void ValidChecksum_ButNotASave_IsCorrupt()
        {
            SaveCode.TryParse(Pack("[1,2,3]"), out _, out var arrayError, out _);
            SaveCode.TryParse(Pack("{ \"level\": broken"), out _, out var brokenError, out _);
            SaveCode.TryParse(Pack("null"), out _, out var nullError, out _);

            Assert.AreEqual(SaveCodeError.Corrupt, arrayError);
            Assert.AreEqual(SaveCodeError.Corrupt, brokenError);
            Assert.AreEqual(SaveCodeError.Corrupt, nullError);
        }

        [Test]
        public void ValidChecksum_ButNotGzip_IsCorrupt()
        {
            var bytes = Encoding.UTF8.GetBytes("this is not gzip at all");
            string code = $"LL1.{Crc32(bytes):x8}.{Convert.ToBase64String(bytes)}";

            SaveCode.TryParse(code, out _, out var error, out _);
            Assert.AreEqual(SaveCodeError.Corrupt, error);
        }

        [Test]
        public void ErrorText_FollowsSpecLines()
        {
            Assert.AreEqual(string.Empty, SaveCode.ErrorText(SaveCodeError.None));
            Assert.AreEqual("코드가 잘렸거나 바뀌었어요. 다시 복사해 주세요", SaveCode.ErrorText(SaveCodeError.Checksum));
            Assert.AreEqual(SaveCode.ErrorText(SaveCodeError.Checksum), SaveCode.ErrorText(SaveCodeError.BadFormat));
            Assert.AreEqual("더 새 버전에서 만든 코드예요. 앱을 업데이트해 주세요", SaveCode.ErrorText(SaveCodeError.NewerVersion));
            Assert.AreEqual("이 코드는 읽을 수 없어요", SaveCode.ErrorText(SaveCodeError.TooLarge));
            Assert.AreEqual("이 코드는 읽을 수 없어요", SaveCode.ErrorText(SaveCodeError.Corrupt));
            foreach (SaveCodeError error in Enum.GetValues(typeof(SaveCodeError)))
            {
                string text = SaveCode.ErrorText(error);
                StringAssert.DoesNotContain("습니다", text);
                StringAssert.DoesNotContain("개방", text);
            }
        }

        [Test]
        public void SaveSummary_FromNull_IsEmpty_AndShortIdKeepsShortIds()
        {
            Assert.AreEqual(default(SaveSummary), SaveSummary.From(null));
            var summary = SaveSummary.From(new PlayerData { accountId = "abc" });
            Assert.AreEqual("abc", summary.accountIdShort);
            Assert.AreEqual(1, summary.highestStage);
            Assert.AreEqual(1, summary.shopLevel);
            Assert.AreEqual(string.Empty, SaveSummary.From(new PlayerData()).accountIdShort);
        }
    }
}
