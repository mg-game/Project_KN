using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// THIRD_PARTY_AUDIO.md 대조 (AUDIO-SPEC §7.2 · §8.4 #6): 바깥 파일(CC0 층 · 생성 음성)이 섞인 테이크는 모두 자기 줄이 있어야 하고,
    /// 프로젝트의 실제 audio_qc.json + THIRD_PARTY_AUDIO.md로 돌린 빌더 대조는 경고가 0이어야 한다.
    /// 빌더는 IdleGame.Editor 어셈블리에 있어 반사로 부른다 (ContentSourceTests와 같은 방식).
    /// </summary>
    public class AudioThirdPartyTests
    {
        const string SampleMarkdown =
            "# Third-party audio (all CC0 1.0 unless stated)\n" +
            "| Our key / take | Pack | Original file | Source URL | Licence (verified in) | Processing |\n" +
            "|---|---|---|---|---|---|\n" +
            "| atk_needle_01 | Interface Sounds 1.0 (2020) | tick_004.ogg | https://kenney.nl/assets/interface-sounds | CC0 1.0 | +3 st |\r\n" +
            "| atk_needle_01 | RPG Audio 1.0 (2014) | dropLeather.ogg | https://kenney.nl/assets/rpg-audio | CC0 1.0 | +7 st |\n" +
            "| amb_rain_01 | AMB Rain Loop 2 | amb_rain2.flac | https://opengameart.org/content/amb-rain-loop-2 | CC0 1.0 | 40 s window |\n" +
            "| jingle_stage_clear, jingle_stage_fail | 40 files | none | - | - | original |\n" +
            "| Pack | Author | Official page | Downloaded from | Licence evidence |\n";

        static Type Builder()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                var type = assembly.GetType("IdleGame.EditorTools.AudioDataBuilder", false);
                Assert.IsNotNull(type, "IdleGame.Editor 어셈블리의 AudioDataBuilder를 찾을 수 없다");
                return type;
            }
            Assert.Fail("IdleGame.Editor 어셈블리를 찾을 수 없다");
            return null;
        }

        static object Invoke(string name, params object[] args)
        {
            var method = Builder().GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "AudioDataBuilder." + name);
            return method.Invoke(null, args);
        }

        static Dictionary<string, HashSet<string>> Rows(string text) => (Dictionary<string, HashSet<string>>)Invoke("ParseThirdPartyRows", text);

        static bool Check(string stem, string source, Dictionary<string, HashSet<string>> rows, List<string> missing) =>
            (bool)Invoke("CheckThirdPartyTake", stem, source, rows, missing);

        [Test]
        public void Rows_AreParsedPerTake_GroupAndSourceTablesAreSkipped()
        {
            var rows = Rows(SampleMarkdown);
            Assert.AreEqual(2, rows.Count, "테이크 줄만: atk_needle_01, amb_rain_01");
            CollectionAssert.AreEquivalent(new[] { "tick_004.ogg", "dropLeather.ogg" }, rows["atk_needle_01"]);
            CollectionAssert.AreEquivalent(new[] { "amb_rain2.flac" }, rows["amb_rain_01"]);
            Assert.AreEqual(0, Rows(null).Count, "글이 없으면 빈 표 (예외 없음)");
            Assert.AreEqual(0, Rows(string.Empty).Count);
        }

        [Test]
        public void ExternalTakes_NeedTheirRow_AndEveryNamedFile()
        {
            var rows = Rows(SampleMarkdown);
            var missing = new List<string>();

            Assert.IsTrue(Check("atk_needle_01", "hyb:atk_needle cand=2 seed=0xfc66ce2a + cc0:interface-sounds/tick_004.ogg + cc0:rpg-audio/dropLeather.ogg", rows, missing));
            Assert.IsTrue(Check("amb_rain_01", "HYB: original synthesis + CC0 layers (see THIRD_PARTY_AUDIO.md)", rows, missing));
            Assert.AreEqual(0, missing.Count, "줄이 있으면 경고 없음: " + string.Join(" / ", missing));

            Assert.IsTrue(Check("atk_needle_04", "hyb:atk_needle cand=8 + cc0:interface-sounds/tick_004.ogg", rows, missing), "줄이 없는 테이크");
            Assert.IsTrue(Check("atk_needle_01", "hyb:atk_needle + cc0:rpg-audio/metalClick.ogg", rows, missing), "줄은 있지만 그 파일이 없음");
            Assert.IsTrue(Check("amb_rain_02", "HYB: original synthesis + CC0 layers (see THIRD_PARTY_AUDIO.md)", rows, missing), "이름 없는 CC0 출처");
            Assert.IsTrue(Check("vox_rouge_pet_01", "vox:seed_audio voice=x", rows, missing), "생성 음성은 기록 줄이 필요");
            Assert.AreEqual(4, missing.Count, string.Join(" / ", missing));

            Assert.IsFalse(Check("boss_wave_01", "syn:boss_wave cand=3 seed=0x8c62cc00", rows, missing), "16진 씨앗의 'cc0'은 바깥 파일이 아니다");
            Assert.IsFalse(Check("ui_tap_01", null, rows, missing), "출처가 없으면 원본 합성으로 본다");
            Assert.IsFalse(Check("jingle_stage_clear_01", "mus:jingle_stage_clear variant=E seed=1", rows, missing));
            Assert.AreEqual(4, missing.Count);
        }

        [Test]
        public void ProjectFiles_EveryExternalFileIsListed()
        {
            var missing = (List<string>)Invoke("ThirdPartyWarningsOnDisk");
            if (missing == null) Assert.Ignore("audio_qc.json이 없다 — 소리 파일이 없는 프로젝트 (조용한 것도 정상)");
            Assert.IsEmpty(missing, "THIRD_PARTY_AUDIO.md에 줄이 없는 바깥 파일:\n" + string.Join("\n", missing));
        }
    }
}
