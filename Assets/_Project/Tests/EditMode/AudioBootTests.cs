using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 부팅과 실패 동작 (AUDIO-SPEC §3.15, §8.3): 클립이 없으면 "[Audio]"도 없고 오류 로그도 없다 (오늘과 같음), 예전 표만 있으면 예전 경로,
    /// 라이브러리가 있으면 엔진. 편집 모드에서는 아무것도 만들지 않는다. 엔진 없이 부르는 공개 API는 모두 조용히 지나간다.
    /// 라이브러리 규칙: 귀로 고름 → QC 추천 → QC 통과 → 전부, 옛 순서 라이브러리는 이름으로 다시 맞춤.
    /// </summary>
    public class AudioBootTests
    {
        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 441, 1, 44100, false);
            _created.Add(clip);
            return clip;
        }

        AudioLibrary Library(params (string key, AudioClip clip)[] takes)
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _created.Add(library);
            library.catalogVersion = AudioCatalog.Version;
            var entries = new AudioLibrary.Entry[AudioCatalog.Count];
            for (int i = 0; i < entries.Length; i++) entries[i] = new AudioLibrary.Entry { key = AudioCatalog.Keys[i].Key };
            foreach (var (key, clip) in takes)
            {
                var id = AudioCatalog.Find(key);
                entries[id.Index].takes = new[] { new AudioLibrary.Take { clip = clip, number = 1 } };
            }
            library.entries = entries;
            library.Invalidate();
            return library;
        }

        [Test]
        public void NoClips_IsSilent_LegacyTableAlone_UsesTheLegacyPath_LibraryUsesTheEngine()
        {
            Assert.AreEqual(AudioBootMode.Silent, UISfx.ResolveBootMode(null, null));
            Assert.AreEqual(AudioBootMode.Silent, UISfx.ResolveBootMode(Library(), null), "빈 라이브러리 = 조용");
            var table = ScriptableObject.CreateInstance<UISfxTable>();
            _created.Add(table);
            Assert.AreEqual(AudioBootMode.Silent, UISfx.ResolveBootMode(null, table), "칸이 비면 조용");
            table.entries.Add(new UISfxTable.Entry { key = UISfxKey.Tap, clips = new[] { Clip("tap") } });
            table.ClearCache();
            Assert.AreEqual(AudioBootMode.Legacy, UISfx.ResolveBootMode(null, table));
            Assert.AreEqual(AudioBootMode.Engine, UISfx.ResolveBootMode(Library(("ui_tap", Clip("lib_tap"))), table));
            var bgmOnly = ScriptableObject.CreateInstance<UISfxTable>();
            _created.Add(bgmOnly);
            bgmOnly.bgm = Clip("bgm");
            Assert.AreEqual(AudioBootMode.Legacy, UISfx.ResolveBootMode(null, bgmOnly), "예전 표의 배경음만 있어도");
        }

        [Test]
        public void EditMode_BootAndPlay_CreateNothing()
        {
            UISfx.Boot();
            UISfx.Play(UISfxKey.Tap, 0.5f);
            UISfx.Play(UISfxKey.CountTick);
            UISfx.ApplySettings();
            Assert.IsNull(GameObject.Find("[Audio]"), "편집 모드에서는 \"[Audio]\"를 만들지 않는다");
        }

        [Test]
        public void ServiceCalls_WithoutAnEngine_AreQuietNoOps()
        {
            Assert.IsFalse(AudioService.IsRunning);
            var id = AudioService.Resolve("atk_needle");
            Assert.IsTrue(id.IsValid);
            AudioService.Play(id);
            AudioService.Play("ui_claim", 0.5f, 0.2f);
            AudioService.PlayAfter(id, 0.3f);
            AudioService.PushMusic(MusicState.Gacha, 42);
            AudioService.PopMusic(42);
            AudioService.PlayBgm(MusicState.Story);
            AudioService.SetAmbience(StageModifierType.Rain);
            AudioService.PlayJingle("jingle_stage_clear");
            AudioService.SetSleep(true);
            AudioService.SetSleep(false);
            AudioService.SetBattleFocus(BattleAudioFocus.Dimmed, 7);
            AudioService.ClearBattleFocus(7);
            AudioService.OnGachaReveal(ItemGrade.Mythic);
            AudioService.OnLampSumShown();
            Assert.IsFalse(AudioService.TryPlayBark(AudioService.Resolve("vox_jjakjjak_pet")), "엔진이 없으면 목소리도 false (부른 쪽이 pet_squeak으로)");
            AudioService.Duck(AudioBus.Bgm, -6f, 0.5f);
            Assert.IsNotNull(AudioService.Stats);
            Assert.AreEqual(MusicState.Silent, AudioService.Music);
            Assert.AreEqual(BattleAudioFocus.Full, AudioService.Focus);
            Assert.IsFalse(AudioService.IsAsleep);
            Assert.IsFalse(AudioService.Resolve("nope").IsValid);
        }

        [Test]
        public void VoiceBarks_DefaultOff()
        {
            bool old = AudioService.VoiceBarks;
            try
            {
                AudioService.VoiceBarks = false;
                Assert.IsFalse(AudioService.VoiceBarks, "설정 패키지가 켜기 전까지 목소리는 조용");
            }
            finally
            {
                AudioService.VoiceBarks = old;
            }
        }

        // ── 라이브러리 규칙 ──

        static AudioTakeCandidate Take(int number, bool qc = false, bool recommended = false, bool passes = true) =>
            new() { Number = number, HasQc = qc, Recommended = recommended, Passes = passes };

        [Test]
        public void EffectiveTakes_FollowTheSelectionThenQc()
        {
            var result = new List<int>();
            var takes = new List<AudioTakeCandidate>
            {
                Take(3, true, true), Take(1, true, false, false), Take(2, true, true), Take(11, true, false),
            };
            AudioLibraryRules.EffectiveTakes(takes, false, null, result);
            CollectionAssert.AreEqual(new[] { 2, 3 }, result, "QC 추천만, 번호 순");

            AudioLibraryRules.EffectiveTakes(takes, true, new byte[] { 11, 1, 7 }, result);
            CollectionAssert.AreEqual(new[] { 1, 11 }, result, "귀로 고름 ∩ 있는 파일");

            AudioLibraryRules.EffectiveTakes(takes, true, new byte[0], result);
            Assert.AreEqual(0, result.Count, "모두 끄면 조용");

            var noRec = new List<AudioTakeCandidate> { Take(1, true, false, false), Take(2, true, false, true) };
            AudioLibraryRules.EffectiveTakes(noRec, false, null, result);
            CollectionAssert.AreEqual(new[] { 2 }, result, "추천이 없으면 경계 통과");

            var noQc = new List<AudioTakeCandidate> { Take(2), Take(1) };
            AudioLibraryRules.EffectiveTakes(noQc, false, null, result);
            CollectionAssert.AreEqual(new[] { 1, 2 }, result, "QC가 없으면 전부");

            var allFail = new List<AudioTakeCandidate> { Take(1, true, false, false), Take(2, true, false, false) };
            AudioLibraryRules.EffectiveTakes(allFail, false, null, result);
            CollectionAssert.AreEqual(new[] { 1, 2 }, result, "모두 실패해도 소리는 남긴다 (귀로 고를 수 있게)");
        }

        [Test]
        public void Library_RemapsAnOldOrderByName_AndReportsClips()
        {
            var empty = Library();
            Assert.IsFalse(empty.HasAnyClip);
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _created.Add(library);
            library.catalogVersion = 0;
            library.entries = new[]
            {
                new AudioLibrary.Entry { key = "atk_needle", takes = new[] { new AudioLibrary.Take { clip = Clip("needle"), number = 3 } } },
                new AudioLibrary.Entry { key = "ui_tap" },
                new AudioLibrary.Entry { key = "removed_key" },
            };
            library.Invalidate();
            var needle = AudioCatalog.Find("atk_needle");
            Assert.AreEqual("atk_needle", library.Get(needle).key);
            Assert.IsTrue(library.WasRemapped, "옛 순서 → 이름으로 맞춤 (에디터 경고: 다시 만드세요)");
            Assert.IsTrue(library.Has(needle));
            Assert.AreEqual(1, library.TakeCount(needle));
            Assert.IsFalse(library.Has(AudioCatalog.Find("ui_tap")));
            Assert.IsNull(library.Get(AudioCatalog.Find("atk_key")));
            Assert.IsTrue(library.HasAnyClip);
            Assert.AreEqual(AudioLibraryRules.MinTrimDb, AudioLibraryRules.ClampTrim(-30f));
            Assert.AreEqual(0f, AudioLibraryRules.ClampTrim(4f), "손질은 줄이기만");
            Assert.AreEqual(0f, AudioLibraryRules.ClampTakeGain(2f), "런타임은 볼륨을 올리지 못한다");
        }

        /// <summary>
        /// 새 별칭을 더하기 전에 만든 라이브러리 (Version 1 · 칸 321개 — RM-2 별칭 6개의 칸이 없다): 엔진은 대상 칸의 테이크로 새 별칭을 울린다.
        /// 사용자 프로젝트는 G3에서 BuildAll이 라이브러리를 다시 만들 때까지 이 상태다
        /// </summary>
        [Test]
        public void OldLibrary_WithoutTheNewAliasEntries_StillSoundsThroughTheTargets()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _created.Add(library);
            library.catalogVersion = 1;
            int oldCount = AudioCatalog.ShippingKeyCount + 8 + 7;
            var entries = new AudioLibrary.Entry[oldCount];
            for (int i = 0; i < oldCount; i++) entries[i] = new AudioLibrary.Entry { key = AudioCatalog.Keys[i].Key };
            string[] targets = { "boss_death", "death_windup", "skill_firefly", "status_lamp_gain", "ui_power_up", "ui_count_finish" };
            foreach (var key in targets) entries[AudioCatalog.Find(key).Index].takes = new[] { new AudioLibrary.Take { clip = Clip(key), number = 1 } };
            library.entries = entries;
            library.Invalidate();
            var go = new GameObject("[AudioOldLibraryTest]");
            _created.Add(go);
            var engine = new AudioEngine(go, library, null);
            string[] aliases = { "boss_defeat", "death_spring", "firefly_appear", "firefly_catch", "cheer_start", AudioService.LampSumKey };
            foreach (var key in aliases)
            {
                var id = AudioCatalog.Find(key);
                Assert.IsTrue(id.IsValid, key);
                Assert.IsNull(library.Get(id), key + ": 옛 라이브러리에는 칸이 없다");
                Assert.IsTrue(engine.HasClips(id), key + "는 대상 칸의 테이크로 울린다");
            }
            Assert.IsFalse(engine.HasClips(AudioCatalog.Find("unlock_card")), "대상에 테이크가 없으면 별칭도 조용");
        }

        /// <summary>
        /// 목소리 한 번 (AudioService.TryPlayBark, P4-3b 요청 · RM-2): vox 키 · 목소리 설정 켜짐 · 클립 있음 · 공통 6초 간격이 비었을 때만 울리고 true.
        /// 그 밖에는 아무것도 울리지 않고 false — 다른 목소리가 막 울린 6초 안의 쓰다듬기는 false를 받아 pet_squeak으로 바꾼다 (조용한 한 번이 없게)
        /// </summary>
        [Test]
        public void TryPlayBark_PlaysOnlyAVoxKeyWithClips_WhileVoiceBarksIsOn_AndTheSharedGateIsFree()
        {
            // ApplySettings는 이 기기의 실제 설정(PlayerPrefs)을 읽는다: 효과음을 꺼 두었으면 목소리가 안 나는 게 맞다
            if (!GameSettings.SoundOn || GameSettings.SfxVolume <= 0f) Assert.Ignore("이 기기의 게임 설정에서 효과음이 꺼져 있어요");
            var pet = AudioCatalog.Find("vox_jjakjjak_pet");
            var hello = AudioCatalog.Find("vox_ttokdan_hello");
            var noClip = AudioCatalog.Find("vox_rouge_pet");
            var tap = AudioCatalog.Ui(UISfxKey.Tap);
            Assert.IsTrue(pet.IsValid && hello.IsValid && noClip.IsValid && tap.IsValid);
            bool old = AudioService.VoiceBarks;
            var go = new GameObject("[AudioBarkTest]");
            _created.Add(go);
            var engine = new AudioEngine(go, Library(("vox_jjakjjak_pet", Clip("pet")), ("vox_ttokdan_hello", Clip("hello")), ("ui_tap", Clip("tap"))), null);
            engine.ApplySettings();
            AudioService.Attach(engine);
            try
            {
                int committed = engine.Stats.Committed;
                AudioService.VoiceBarks = false;
                Assert.IsFalse(AudioService.TryPlayBark(pet), "목소리 설정이 꺼져 있으면 false");
                AudioService.VoiceBarks = true;
                Assert.IsFalse(AudioService.TryPlayBark(noClip), "클립이 없는 목소리 키는 false");
                Assert.IsFalse(AudioService.TryPlayBark(tap), "vox 키가 아니면 false (그런 소리는 Play로)");
                Assert.IsFalse(AudioService.TryPlayBark(AudioId.None), "무효 번호는 false");
                Assert.IsFalse(AudioService.TryPlayBark(pet, 0f), "볼륨 0은 false");
                Assert.AreEqual(committed, engine.Stats.Committed, "false일 때는 아무것도 울리지 않았다");

                Assert.IsTrue(AudioService.TryPlayBark(pet), "설정 켜짐 · 클립 있음 · 간격 비었음 → 울리고 true");
                Assert.AreEqual(committed + 1, engine.Stats.Committed, "목소리 하나가 실제로 시작됐다");
                Assert.IsFalse(AudioService.TryPlayBark(hello), "다른 목소리라도 공통 6초 간격 안이면 false");
                Assert.IsFalse(AudioService.TryPlayBark(pet), "같은 목소리도 false");
                AudioService.Play(hello);
                Assert.AreEqual(committed + 1, engine.Stats.Committed, "막힌 목소리는 울리지 않는다 (Play로 불러도 같은 간격)");
            }
            finally
            {
                AudioService.Detach(engine);
                AudioService.VoiceBarks = old;
            }
            Assert.IsFalse(AudioService.TryPlayBark(pet), "떨어진 뒤에는 false");
        }

        [Test]
        public void Library_InCatalogOrder_IsNotRemapped()
        {
            var library = Library(("ui_tap", Clip("tap")));
            Assert.IsFalse(library.WasRemapped);
            Assert.IsTrue(library.Has(AudioCatalog.Ui(UISfxKey.Tap)));
        }
    }
}
