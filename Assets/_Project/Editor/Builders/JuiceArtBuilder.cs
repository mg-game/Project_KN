using System;
using System.Collections.Generic;
using IdleGame.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 손맛 연출 그림 묶음(JuiceArt) 굽기 (FX-JUICE §3.7 / §5). BattleSceneBuilder가 부르고 BattleFx._juice에 묶는다.
    ///   빛 그림 (Art/FX/Juice — 검정→알파, 가산): fx_core_star · fx_glow_core · fx_shockwave · fx_star_ring · fx_streak · fx_sparkle_cluster ·
    ///     fx_speedlines · fx_flare_cross · fx_slash_arc · fx_afterimage · fx_lamp_wave_ring · fx_light_pillar · hero_lamp_bloom · muzzle_{계보}
    ///   색 그림 (Art/VFX/Juice): atk_{계보}_impact · atk_{계보}_crit · kill_{갈래} · crit_star_badge · plate_combo_9s · hero_boss_kill ·
    ///     hero_stage_clear · decal_{petal|puddle|thread|buttons|glitter|heart} · fb_light_burst_4x4 (P2 플립북, 16칸으로 나뉘어 있을 때만)
    ///   다시 쓰는 기존 그림: Art/FX/fx_confetti_* · fx_mote_* · ring_glow · fx_ring_thread · rays, Art/VFX/vfx_star_burst · vfx_gloss_shine
    /// 정확한 경로로 ArtLib.Load만 한다 — 아직 없는 새 그림은 null로 굽고 "[JuiceArt] missing: …" 한 줄을 남긴다 (런타임은 예전 그림으로 대신한다).
    /// 선택 그림(P2 플립북 fb_light_burst_4x4)은 없어도 missing에 넣지 않고 정보 한 줄만 남긴다 — 빌드 · 캡처 기록에서 깨진 그림처럼 보이지 않게 (Wave 3).
    /// 그림을 나중에 넣으면 씬을 다시 만들어야 묶인다 (BuildAll · fx_capture가 fx_proj에서 한다).
    /// </summary>
    internal static class JuiceArtBuilder
    {
        /// <summary>Art/ 아래 빛 그림 폴더 / 색 그림 폴더</summary>
        public const string FxJuice = "FX/Juice";
        public const string VfxJuice = "VFX/Juice";
        const string FlipbookName = "fb_light_burst_4x4";
        const int FlipbookFrames = 16;

        /// <summary>바닥 자국 파일 이름 (JuiceDecal 순서)</summary>
        static readonly string[] DecalNames = { "decal_petal", "decal_puddle", "decal_thread", "decal_buttons", "decal_glitter", "decal_heart" };

        /// <summary>root 아래에 JuiceArt를 만들고 그림을 굽는다</summary>
        public static JuiceArt Build(RectTransform root)
        {
            var holder = UIKit.NewRect("JuiceArt", root);
            var juice = holder.gameObject.AddComponent<JuiceArt>();
            var missing = new List<string>();

            AttackFxBuilder.BindLineageJuice(juice, missing);
            EnemyFxBuilder.BindKillBlooms(juice, missing);

            var decals = new Object[DecalNames.Length];
            for (int i = 0; i < DecalNames.Length; i++) decals[i] = Load(VfxJuice, DecalNames[i], missing);

            new Binder(juice)
                .SetArray("_decals", decals)
                // 빛 그림 (§5 #1~6, 27~32, 37)
                .Set("_coreStar", Load(FxJuice, "fx_core_star", missing))
                .Set("_glowCore", Load(FxJuice, "fx_glow_core", missing))
                .Set("_shockwave", Load(FxJuice, "fx_shockwave", missing))
                .Set("_starRing", Load(FxJuice, "fx_star_ring", missing))
                .Set("_streak", Load(FxJuice, "fx_streak", missing))
                .Set("_sparkleCluster", Load(FxJuice, "fx_sparkle_cluster", missing))
                .Set("_speedlines", Load(FxJuice, "fx_speedlines", missing))
                .Set("_flareCross", Load(FxJuice, "fx_flare_cross", missing))
                .Set("_slashArc", Load(FxJuice, "fx_slash_arc", missing))
                .Set("_afterimage", Load(FxJuice, "fx_afterimage", missing))
                .Set("_lampWaveRing", Load(FxJuice, "fx_lamp_wave_ring", missing))
                .Set("_lightPillar", Load(FxJuice, "fx_light_pillar", missing))
                .Set("_heroLampBloom", Load(FxJuice, "hero_lamp_bloom", missing))
                .SetArray("_lightBurstFrames", Flipbook(missing))
                // 색 그림 (§5 #7, 26, 33, 34)
                .Set("_critStarBadge", Load(VfxJuice, "crit_star_badge", missing))
                .Set("_comboPlate", Load(VfxJuice, "plate_combo_9s", missing))
                .Set("_heroBossKill", Load(VfxJuice, "hero_boss_kill", missing))
                .Set("_heroStageClear", Load(VfxJuice, "hero_stage_clear", missing))
                // 다시 쓰는 기존 그림 (§5.3 Reuse as-is)
                .Set("_confettiButton", ArtLib.FX("fx_confetti_button"))
                .Set("_confettiRibbon", ArtLib.FX("fx_confetti_ribbon"))
                .Set("_confettiStar", ArtLib.FX("fx_confetti_star"))
                .Set("_confettiThread", ArtLib.FX("fx_confetti_thread"))
                .Set("_moteDust", ArtLib.FX("fx_mote_dust"))
                .Set("_motePetal", ArtLib.FX("fx_mote_petal"))
                .Set("_moteTwinkle", ArtLib.FX("fx_mote_twinkle"))
                .Set("_moteFirefly", ArtLib.FX("fx_mote_firefly"))
                .Set("_moteRaindrop", ArtLib.FX("fx_mote_raindrop"))
                .Set("_ringGlow", ArtLib.FX("ring_glow"))
                .Set("_ringThread", ArtLib.FX("fx_ring_thread"))
                .Set("_rays", Or(ArtLib.FX("rays"), ArtLib.Battle("rays")))
                .Set("_starBurst", ArtLib.Vfx(SkillVfx.StarBurst))
                .Set("_glossShine", ArtLib.Vfx("vfx_gloss_shine"))
                .Apply();

            if (missing.Count > 0) Debug.Log("[JuiceArt] missing: " + string.Join(", ", missing));
            else if (!HasFlipbook) Debug.Log("[JuiceArt] all juice art bound (optional P2 flipbook " + FlipbookName + " not present — nothing uses it)");
            else Debug.Log("[JuiceArt] all juice art bound");
            return juice;
        }

        /// <summary>Art/{folder}/{name}.png (없으면 missing에 이름을 적고 null)</summary>
        public static Sprite Load(string folder, string name, List<string> missing)
        {
            var sprite = ArtLib.Load($"{folder}/{name}.png");
            if (sprite == null) missing?.Add(name);
            return sprite;
        }

        /// <summary>
        /// 플립북 빛 터짐 (P2, 선택): Art/VFX/Juice/fb_light_burst_4x4.png가 16칸(Multiple)으로 나뉘어 있으면 그 칸을 이름 순서로. 없거나 한 장이면 빈 배열
        /// (어떤 연출도 이 그림에 기대지 않는다). 없으면 missing에 넣지 않는다 — HasFlipbook만 false (Build가 정보 한 줄)
        /// </summary>
        static Object[] Flipbook(List<string> missing)
        {
            HasFlipbook = false;
            string path = $"{ArtLib.Root}/{VfxJuice}/{FlipbookName}.png";
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite) sprites.Add(sprite);
            }
            if (sprites.Count < FlipbookFrames)
            {
                // 한 장뿐이면(칸을 안 나눈 그림) 고칠 일이 있으니 missing에 남긴다. 아예 없으면 선택 그림이라 조용히
                if (sprites.Count > 0) missing.Add(FlipbookName + " (not sliced into " + FlipbookFrames + ")");
                return Array.Empty<Object>();
            }
            HasFlipbook = true;
            sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            var frames = new Object[FlipbookFrames];
            for (int i = 0; i < FlipbookFrames; i++) frames[i] = sprites[i];
            return frames;
        }

        /// <summary>마지막 Build에서 플립북이 16칸으로 묶였는지</summary>
        static bool HasFlipbook;

        static Sprite Or(Sprite a, Sprite b) => a != null ? a : b;
    }
}
