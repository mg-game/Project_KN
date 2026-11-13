using System;
using System.Collections.Generic;
using IdleGame.UI;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 소리 카탈로그 (AUDIO-SPEC §4.2, §8.3): 키가 겹치지 않고 숫자 토큰으로 끝나지 않는다, 첫 306개 순서 고정(해시), UISfxKey ↔ UiMap,
    /// 별칭, 사다리가 F 펜타토닉 안, 파일 이름 규칙, 계보 · 적 · 스킬 키가 모두 있다.
    /// </summary>
    public class AudioCatalogTests
    {
        /// <summary>첫 306개 키 순서의 FNV-1a 해시 (키 + 줄바꿈). 키를 끼워 넣거나 순서를 바꾸면 깨진다 — 새 키는 끝에만</summary>
        const uint ShippingOrderHash = 0xB195A7B0u;

        static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };

        [Test]
        public void Keys_AreUnique_AndNeverEndInANumberToken()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var def in AudioCatalog.Keys)
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.Key));
                Assert.IsTrue(seen.Add(def.Key), "겹치는 키 " + def.Key);
                int us = def.Key.LastIndexOf('_');
                string last = us >= 0 ? def.Key.Substring(us + 1) : def.Key;
                Assert.IsFalse(int.TryParse(last, out _), "키가 숫자 토큰으로 끝나면 테이크 번호와 헷갈린다: " + def.Key);
                Assert.AreEqual(def.Key, def.Key.ToLowerInvariant(), "키는 소문자");
            }
            Assert.AreEqual(AudioCatalog.ShippingKeyCount + 8 + 7 + 6, AudioCatalog.Count, "306 + 별칭 8 + 예약 7 + SPEC4 별칭 6 (RM-2)");
        }

        [Test]
        public void FirstShippingKeys_KeepTheirOrder()
        {
            uint hash = 0x811C9DC5u;
            for (int i = 0; i < AudioCatalog.ShippingKeyCount; i++)
            {
                foreach (char c in AudioCatalog.Keys[i].Key + "\n")
                {
                    hash ^= (byte)c;
                    hash *= 0x01000193u;
                }
            }
            Assert.AreEqual(ShippingOrderHash, hash, "AudioId는 배열 인덱스 — 끝에만 더한다 (AudioCatalog.Version도 올린다)");
            Assert.AreEqual("ui_tap", AudioCatalog.Keys[0].Key);
            Assert.AreEqual("vox_lalabell_pet", AudioCatalog.Keys[AudioCatalog.ShippingKeyCount - 1].Key);
        }

        [Test]
        public void UiMap_CoversEveryUISfxKey_AndResolves()
        {
            var values = (UISfxKey[])Enum.GetValues(typeof(UISfxKey));
            Assert.AreEqual(81, values.Length, "12 + 69");
            Assert.AreEqual(values.Length, AudioCatalog.UiMap.Length);
            foreach (var key in values)
            {
                var id = AudioCatalog.Ui(key);
                Assert.IsTrue(id.IsValid, key + "에 키가 없다");
                var def = AudioCatalog.Get(id);
                Assert.IsTrue(def.Bus == AudioBus.Ui || def.Bus == AudioBus.Stinger, key + " 버스 " + def.Bus);
            }
            Assert.AreEqual("ui_tap", AudioCatalog.Get(AudioCatalog.Ui(UISfxKey.Tap)).Key);
            Assert.AreEqual("ui_gacha_rare", AudioCatalog.Get(AudioCatalog.Ui(UISfxKey.GachaRare)).Key);
            Assert.AreEqual("jingle_new_feature", AudioCatalog.Get(AudioCatalog.Ui(UISfxKey.FeatureUnlock)).Key);
            Assert.AreEqual(AudioBus.Stinger, AudioCatalog.Get(AudioCatalog.Ui(UISfxKey.Graduation)).Bus);
            var error = AudioCatalog.Get(AudioCatalog.Ui(UISfxKey.Error));
            Assert.AreEqual("ui_error", error.Key);
            Assert.AreEqual("ui_deny", AudioCatalog.Keys[error.AliasOf].Key, "Error는 Deny 소리를 빌려 쓴다");
        }

        [Test]
        public void UISfxKey_OldValuesUnchanged_NewValuesFixed()
        {
            Assert.AreEqual(0, (int)UISfxKey.Tap);
            Assert.AreEqual(4, (int)UISfxKey.Deny);
            Assert.AreEqual(5, (int)UISfxKey.CoinLand);
            Assert.AreEqual(11, (int)UISfxKey.GachaRare);
            Assert.AreEqual(12, (int)UISfxKey.SheetOpen);
            Assert.AreEqual(17, (int)UISfxKey.Error);
            Assert.AreEqual(26, (int)UISfxKey.CountTick);
            Assert.AreEqual(54, (int)UISfxKey.SliderTick);
            Assert.AreEqual(73, (int)UISfxKey.PetSqueak);
            Assert.AreEqual(80, (int)UISfxKey.DefeatAdvice);
            foreach (var def in AudioCatalog.Keys)
            {
                if (def.UiKey < 0) continue;
                Assert.AreEqual(def.Key, AudioCatalog.Get(AudioCatalog.UiMap[def.UiKey]).Key, "UiKey 값과 UiMap이 같은 키를 가리킨다");
            }
        }

        [Test]
        public void Aliases_ResolveToTheirTargets()
        {
            var expected = new Dictionary<string, string>
            {
                { "kill_mote", "echo_refund" },
                { "ui_error", "ui_deny" },
                { "status_reveal", "enemy_reveal" },
                { "unlock_card", "jingle_new_feature" },
                { "jingle_gacha_high", "jingle_gacha_legend" },
                { "stage_clear", "jingle_stage_clear" },
                { "stage_fail", "jingle_stage_fail" },
                { "guardian_pulse", "pulse_support" },
                // RM-2: SPEC4 §3.5 / §13.2 / §13.8 이름 + 불 밝히기 합계 (WP-5B)
                { "boss_defeat", "boss_death" },
                { "death_spring", "death_windup" },
                { "firefly_appear", "skill_firefly" },
                { "firefly_catch", "status_lamp_gain" },
                { "cheer_start", "ui_power_up" },
                { AudioService.LampSumKey, "ui_count_finish" },
            };
            foreach (var pair in expected)
            {
                var id = AudioCatalog.Find(pair.Key);
                Assert.IsTrue(id.IsValid, pair.Key);
                var def = AudioCatalog.Get(id);
                Assert.IsTrue(def.IsAlias && def.Has(AudioKeyFlags.Alias), pair.Key + " 별칭 표시");
                Assert.AreEqual(pair.Value, AudioCatalog.Get(AudioCatalog.ClipSource(id)).Key);
                Assert.AreEqual(0, def.Takes, "별칭은 자기 테이크가 없다");
            }
            Assert.AreEqual(0.5f, AudioCatalog.Get(AudioCatalog.Find("kill_mote")).MinInterval, 1e-6f, "기억 조각은 초당 2번까지");

            // 새 별칭의 자기 규칙 (버스 · 게이트): 클립만 빌리고 버스 · 등급 · 간격은 자기 것
            var appear = AudioCatalog.Get(AudioCatalog.Find("firefly_appear"));
            Assert.AreEqual(AudioBus.Sfx, appear.Bus, "반딧불 등장은 전장 소리");
            Assert.AreEqual(1, appear.MaxVoices, "반딧불 등장은 한 번에 하나 (§3.5: 나타날 때마다 1번까지)");
            Assert.AreEqual(AudioVoiceClass.Low, appear.Class, "작은 손님 소리는 중요한 소리를 빼앗지 않는다");
            Assert.AreEqual(AudioBus.Ui, AudioCatalog.Get(AudioCatalog.Find("firefly_catch")).Bus, "반딧불 잡기는 손끝 소리 (UI)");
            Assert.AreEqual(AudioBus.Ui, AudioCatalog.Get(AudioCatalog.Find("cheer_start")).Bus, "응원 받기 버튼 (UI)");
            var lampSum = AudioCatalog.Get(AudioCatalog.Find(AudioService.LampSumKey));
            Assert.AreEqual(AudioBus.Sfx, lampSum.Bus, "불 밝히기 합계 판은 전장 소리 (가려지면 조용)");
            Assert.AreEqual(1, lampSum.MaxVoices);
            var bossDefeat = AudioCatalog.Get(AudioCatalog.Find("boss_defeat"));
            Assert.AreEqual(AudioBus.Sfx, bossDefeat.Bus);
            Assert.AreEqual(AudioVoiceClass.Critical, bossDefeat.Class, "보스 처치는 빼앗기지 않는다");
            Assert.AreEqual(AudioCatalog.Get(AudioCatalog.Find("death_windup")).Bus, AudioCatalog.Get(AudioCatalog.Find("death_spring")).Bus);
            foreach (var def in AudioCatalog.Keys)
            {
                if (!def.IsAlias) continue;
                Assert.IsFalse(AudioCatalog.Keys[def.AliasOf].IsAlias, def.Key + ": 별칭의 별칭은 없다 (한 단계만 빌린다)");
                Assert.AreEqual(-1, def.UiKey, def.Key + ": 별칭은 UISfxKey 자리를 차지하지 않는다");
            }
        }

        /// <summary>
        /// SPEC4 §3.1 · §3.5 · §10.2 · §13이 이름으로 부르는 소리 (+ WP-5B 불 밝히기 합계): P4 패키지가 키 이름으로 걸므로 모두 카탈로그에 있어야 한다
        /// </summary>
        static readonly string[] Spec4CueNames =
        {
            // §3.1 등급 → 소리
            "hit_crit", "skill_cast", "death_dust", "death_moth", "death_metal", "death_shadow", "lamp_lit", "boss_appear", "stage_clear", "jingle_chapter_clear",
            "stage_fail",
            // §3.5 걸 곳
            "atk_generic", "enemy_puff", "boss_vortex", "shop_hit", "shield_block", "shield_break", "echo_summon", "echo_dissolve", "guardian_pulse", "ui_tap",
            "boss_defeat", "death_spring", "firefly_appear", "firefly_catch", "cheer_start", "gacha_bundle_drop", "gacha_bundle_open", "jingle_gacha_high",
            "chest_open", "basket_claim", "quick_idle", "unlock_card", "achievement_claim", "pet_squeak",
            // §10.2 음악
            "bgm_battle_night", "bgm_boss", "bgm_story",
            // WP-5B 불 밝히기 합계 (AudioService.OnLampSumShown)
            AudioService.LampSumKey,
        };

        [Test]
        public void Spec4CueNames_AllResolve_AndTheNewAliasesWereAppendedAtTheEnd()
        {
            foreach (var key in Spec4CueNames) Assert.IsTrue(AudioCatalog.Find(key).IsValid, "SPEC4 소리 이름이 카탈로그에 없다: " + key);
            // 끝에만 더했다: 예약 7개 자리(옛 라이브러리 순서)는 그대로, 새 별칭은 그 뒤
            int reserved = AudioCatalog.ShippingKeyCount + 8;
            Assert.AreEqual("kill_mote", AudioCatalog.Keys[AudioCatalog.ShippingKeyCount].Key);
            Assert.AreEqual("bgm_tower", AudioCatalog.Keys[reserved].Key);
            Assert.AreEqual("ui_decor_place", AudioCatalog.Keys[reserved + 6].Key);
            string[] appended = { "boss_defeat", "death_spring", "firefly_appear", "firefly_catch", "cheer_start", AudioService.LampSumKey };
            for (int i = 0; i < appended.Length; i++) Assert.AreEqual(appended[i], AudioCatalog.Keys[reserved + 7 + i].Key, "새 키는 끝에만 (AudioId = 배열 인덱스)");
            Assert.GreaterOrEqual(AudioCatalog.Version, 2, "키를 더했으면 Version을 올린다 (RM-2 = 2)");
        }

        /// <summary>
        /// 실제 라이브러리 (Resources/Audio/AudioLibrary): 모든 별칭의 대상 키에 켜 둔 테이크가 있다 — SPEC4 이름이 실제로 소리를 낸다.
        /// 새 별칭 칸이 없는 옛 라이브러리여도 엔진은 대상 칸을 쓴다 (AudioEngine.ClipEntry). 소리 파일이 없는 프로젝트는 건너뛴다
        /// </summary>
        [Test]
        public void ShippedLibrary_EveryAliasTargetHasTakes()
        {
            var library = UnityEngine.Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            if (library == null || !library.HasAnyClip) Assert.Ignore("소리 라이브러리가 없다 — 조용한 것도 정상");
            foreach (var def in AudioCatalog.Keys)
            {
                if (!def.IsAlias) continue;
                var target = AudioCatalog.ClipSource(AudioCatalog.Find(def.Key));
                Assert.IsTrue(library.Has(target), def.Key + " → " + AudioCatalog.Get(target).Key + "에 켜 둔 테이크가 없다");
            }
        }

        [Test]
        public void Ladders_AreValid_AndStayInFPentatonic()
        {
            Assert.AreEqual(7, AudioCatalog.Ladders.Length);
            for (int l = 1; l < AudioCatalog.Ladders.Length; l++)
            {
                var ladder = AudioCatalog.Ladders[l];
                Assert.Greater(ladder.Count, 0, ladder.Name);
                for (int s = 0; s < ladder.Count; s++)
                {
                    float hz = ladder.StepHz(s);
                    int semis = (int)Math.Round(AudioCatalog.Semitones(698.46f, hz));
                    int pc = ((semis % 12) + 12) % 12;
                    Assert.Contains(pc, Pentatonic, $"{ladder.Name} {s}번째 음 {hz:0.0}Hz가 F 펜타토닉 밖");
                }
            }
            // 자장가: C6 A5 F5 G5 A5 G5
            float[] lullaby = { 1046.5f, 880f, 698.46f, 783.99f, 880f, 783.99f };
            for (int i = 0; i < lullaby.Length; i++)
                Assert.AreEqual(lullaby[i], AudioCatalog.LadderHz(AudioCatalog.LadderLullaby, i), 0.5f, "자장가 " + i);
            Assert.AreEqual(2093f, AudioCatalog.LadderHz(AudioCatalog.LadderLamp, 6), 0.5f, "불빛 물결 7번째 = C7");
            Assert.AreEqual(1396.91f, AudioCatalog.LadderHz(AudioCatalog.LadderKill, 5), 0.5f, "처치 사다리 6번째 = F6");
        }

        [Test]
        public void TunedKeys_HaveRenderRoots_WithinSevenSemitonesOfTheirLadder()
        {
            string[] tuned = { "atk_musicbox", "atk_musicbox_crit", "atk_musicbox_finale", "hit_lamp", "kill_chain", "ui_count_tick", "lamp_tick", "status_chain" };
            foreach (var key in tuned)
            {
                var def = AudioCatalog.Get(AudioCatalog.Find(key));
                Assert.IsTrue(def.IsTuned, key);
                Assert.Greater(def.TargetRootHz, 0f, key);
                Assert.AreEqual(0f, def.PitchJitter, key + " 조율 키는 흔들지 않는다");
                if (def.Ladder == 0 || def.Ladder == AudioCatalog.LadderLamp || def.Ladder == AudioCatalog.LadderKill) continue;
                var ladder = AudioCatalog.Ladders[def.Ladder];
                for (int s = 0; s < ladder.Count; s++)
                    Assert.LessOrEqual(Math.Abs(AudioCatalog.Semitones(def.TargetRootHz, ladder.StepHz(s))), 7.01f, $"{key} {s}번째는 7반음 안에서 다시 맞춘다");
            }
        }

        [Test]
        public void ParseFileName_Rules()
        {
            AssertParse("atk_needle_03", "atk_needle", 3);
            AssertParse("atk_needle", "atk_needle", 1);
            AssertParse("ui_tap_2", "ui_tap", 2);
            AssertParse("bgm_gacha_intro_01", "bgm_gacha_intro", 1);
            AssertParse("ui_level_up_11", "ui_level_up", 11);
            AssertParse("amb_emit_night_06", "amb_emit_night", 6);
            Assert.IsFalse(AudioCatalog.ParseFileName("atk_needle_00", out _, out _), "테이크 0은 없다");
            Assert.IsFalse(AudioCatalog.ParseFileName("atk_needle_100", out _, out _), "테이크는 99까지");
            Assert.IsFalse(AudioCatalog.ParseFileName("", out _, out _));
        }

        static void AssertParse(string stem, string key, int take)
        {
            Assert.IsTrue(AudioCatalog.ParseFileName(stem, out string k, out int t), stem);
            Assert.AreEqual(key, k, stem);
            Assert.AreEqual(take, t, stem);
        }

        [Test]
        public void AudioId_DefaultIsInvalid_AndUnknownKeysAreInvalid()
        {
            Assert.IsFalse(default(AudioId).IsValid, "초기화 안 된 번호가 0번 키(ui_tap)를 울리지 않게");
            Assert.AreEqual(-1, default(AudioId).Index);
            Assert.IsFalse(AudioCatalog.Find("no_such_key").IsValid);
            Assert.IsFalse(AudioCatalog.Find(null).IsValid);
            var tap = AudioCatalog.Find("ui_tap");
            Assert.IsTrue(tap.IsValid);
            Assert.AreEqual(0, tap.Index);
            Assert.AreEqual(tap, new AudioId(0));
        }

        [Test]
        public void LineageEnemyAndSkillKeys_AllExist()
        {
            foreach (var lineage in AttackFxCatalog.KnownLineages)
            {
                string hit = AttackFxCatalog.Look(lineage).Sfx;
                Assert.IsTrue(AudioCatalog.Find(hit).IsValid, lineage + " 명중 " + hit);
                Assert.IsTrue(AudioCatalog.Find(AudioCatalog.FireKeyOf(hit)).IsValid, lineage + " 쏘기");
                Assert.IsTrue(AudioCatalog.Find(AudioCatalog.CritKeyOf(hit)).IsValid, lineage + " 치명타");
            }
            Assert.IsTrue(AudioCatalog.Find(AttackFxCatalog.Look(null).Sfx).IsValid, "모르는 계보 (atk_generic)");
            foreach (var enemy in EnemyFxCatalog.KnownEnemies)
            {
                bool boss = enemy.StartsWith("boss_", StringComparison.Ordinal);
                Assert.IsTrue(AudioCatalog.Find(EnemyFxCatalog.Look(enemy, boss).Sfx).IsValid, enemy + " 공격");
                string family = EnemyFxCatalog.FamilyKey(EnemyFxCatalog.Family(enemy));
                Assert.IsTrue(AudioCatalog.Find("hurt_" + family).IsValid, enemy + " 맞는 소리");
                Assert.IsTrue(AudioCatalog.Find("death_" + family).IsValid, enemy + " 처치");
            }
            foreach (var vfx in SkillVfx.All)
                Assert.IsTrue(AudioCatalog.Find("skill_" + vfx.Substring(4)).IsValid, vfx);
        }

        [Test]
        public void Buses_HaveTheRightShape()
        {
            foreach (var def in AudioCatalog.Keys)
            {
                if (def.Bus == AudioBus.Bgm || def.Bus == AudioBus.Stinger) Assert.IsTrue(def.Has(AudioKeyFlags.Music), def.Key + " 음악 표시");
                if (def.Bus == AudioBus.Amb && !def.Has(AudioKeyFlags.Emitter)) Assert.IsTrue(def.Has(AudioKeyFlags.Loop), def.Key + " 환경음은 반복");
                if (def.Has(AudioKeyFlags.Vox)) Assert.AreEqual(AudioBus.Ui, def.Bus, def.Key);
            }
            Assert.AreEqual(AudioBus.Loop, AudioCatalog.Get(AudioCatalog.Find("shop_heartbeat")).Bus);
            Assert.IsTrue(AudioCatalog.Get(AudioCatalog.Find("bgm_gacha_intro")).Has(AudioKeyFlags.Intro));
        }
    }
}
