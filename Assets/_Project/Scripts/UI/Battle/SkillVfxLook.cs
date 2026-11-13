using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>스킬 연출 방식. SkillVfx 키마다 하나가 정해진다 (SkillVfxLook.Get).</summary>
    public enum SkillVfxStyle : byte
    {
        /// <summary>투사체: 시전자 → 대상으로 날아가 터진다 (실·열쇠)</summary>
        Projectile,
        /// <summary>광선: 시전자에서 레인/대상까지 곧게 뻗는다 (등불 빛줄기)</summary>
        Beam,
        /// <summary>폭발: 효과 중심에서 팡 터진다 (촛불·입맞춤·별·먼지·그림자)</summary>
        Burst,
        /// <summary>퍼지는 고리: 바닥에서 동심원이 번진다 (음파·폭풍·드러냄)</summary>
        Ring,
        /// <summary>바닥 장판: 바닥에 깔렸다가 서서히 스민다 (촛농·실타래·마법진)</summary>
        Ground,
        /// <summary>보호막 돔: 대상 위로 둥근 막이 씌워진다 (단추·우산·거품)</summary>
        Dome,
        /// <summary>떠오름: 음표·하트·반딧불이 둥실 떠오른다</summary>
        Rise,
        /// <summary>머리 위: 대상 머리 위에 표식이 톡 뜬다 (기절 별·왕관·약화 표식)</summary>
        OverHead,
        /// <summary>회전: 시전자 둘레를 빙글 돈다 (리본·회전 단추)</summary>
        Orbit,
        /// <summary>내림: 구름에서 빗방울이 떨어진다</summary>
        Rain,
    }

    /// <summary>
    /// 연출에 곁들이는 작은 모양 (흰색 + 알파, 색은 코드로 입힌다). 스킬 아트가 없을 때 대체 그림으로도 쓴다.
    /// 씬 빌더가 같은 순서로 SkillVfxLibrary._motifs에 굽는다.
    /// </summary>
    public enum VfxMotif : byte
    {
        Sparkle = 0,
        Glow = 1,
        Soft = 2,
        Ring = 3,
        Trail = 4,
        Heart = 5,
        Note = 6,
        Drop = 7,
        Star = 8,
        Button = 9,
        Crown = 10,
        Flame = 11,
        Keyhole = 12,
        Shard = 13,
        MagicCircle = 14,
        Bubble = 15,
        Swirl = 16,
        DownArrow = 17,
        Yarn = 18,

        // ── 적 공격·처치 / 맥박 연출 (ATK-03·04·10·11) ──
        /// <summary>톱니바퀴 (쇠붙이 처치 파편)</summary>
        Gear = 19,
        /// <summary>나사 (쇠붙이 처치 파편)</summary>
        Bolt = 20,
        /// <summary>용수철 (태엽쥐 공격·처치)</summary>
        Spring = 21,
        /// <summary>태엽 감개 열쇠 (태엽쥐가 다른 적을 감아 줄 때 머리 위에서 돈다)</summary>
        WindKey = 22,
        /// <summary>느낌표 (수선점이 뚫린 방향 알림 — 색만으로 뜻을 전하지 않게)</summary>
        Exclaim = 23,
        /// <summary>천이 갉아먹힌 구멍 (좀나방 '갉음' 스티커)</summary>
        Tear = 24,
        /// <summary>몽글한 얼룩 (녹물 튀김·녹 얼룩·촛농)</summary>
        Splat = 25,
        /// <summary>육각 물결 고리 (적 보호막에 막힌 명중)</summary>
        HexRing = 26,
        /// <summary>나뭇조각 (수선점 문이 맞을 때 튀는 파편)</summary>
        Splinter = 27,
        /// <summary>나방 날개 비늘 가루 (작은 물방울 꼴 잎)</summary>
        Scale = 28,
    }

    /// <summary>스킬 연출 키 하나의 모양새 (방식, 파스텔 주색/보조색, 곁들이는 입자, 대체 그림)</summary>
    public readonly struct SkillLook
    {
        public readonly SkillVfxStyle Style;
        public readonly Color Main;
        public readonly Color Accent;
        /// <summary>곁들이는 입자 모양</summary>
        public readonly VfxMotif Particle;
        /// <summary>아트가 없을 때 대신 그리는 모양</summary>
        public readonly VfxMotif Fallback;

        public SkillLook(SkillVfxStyle style, Color main, Color accent, VfxMotif particle, VfxMotif fallback)
        {
            Style = style;
            Main = main;
            Accent = accent;
            Particle = particle;
            Fallback = fallback;
        }
    }

    /// <summary>
    /// SkillVfx 키 → 연출 방식과 색. 콘텐츠는 키만 고르고, 전투 화면은 이 표대로 그린다.
    /// LOST &amp; LOVELY 톤: 파스텔 보라·분홍·하늘·크림 + 절제된 금색, 부드러운 빛.
    /// </summary>
    public static class SkillVfxLook
    {
        public const int MotifCount = 29;

        public static readonly SkillLook Default = new(SkillVfxStyle.Burst, Hex("FFF4DC"), Hex("C4B2F2"), VfxMotif.Sparkle, VfxMotif.Sparkle);

        static readonly Dictionary<string, SkillLook> Looks = Build();

        public static SkillLook Get(string key) => key != null && Looks.TryGetValue(key, out var look) ? look : Default;

        public static SkillVfxStyle StyleOf(string key) => Get(key).Style;

        public static bool IsKnown(string key) => key != null && Looks.ContainsKey(key);

        /// <summary>상태 효과 종류의 기본 연출 키 (StatusApplied에 키가 없을 때)</summary>
        public static string StatusKey(SkillEffectType type) => type switch
        {
            SkillEffectType.Stun => SkillVfx.StunStars,
            SkillEffectType.Slow => SkillVfx.SlowYarn,
            SkillEffectType.Mark => SkillVfx.LipstickHeart,
            SkillEffectType.DamageOverTime => SkillVfx.CandleBurst,
            SkillEffectType.Knockback => SkillVfx.DustPoof,
            SkillEffectType.Pull => SkillVfx.StormSwirl,
            SkillEffectType.BuffAttack => SkillVfx.BuffCrown,
            SkillEffectType.BuffAttackSpeed => SkillVfx.BuffCrown,
            SkillEffectType.Reveal => SkillVfx.RevealLight,
            SkillEffectType.Cleanse => SkillVfx.CleanseSparkle,
            SkillEffectType.Shield => SkillVfx.ShieldBubble,
            SkillEffectType.Heal => SkillVfx.HeartHeal,
            SkillEffectType.HealOverTime => SkillVfx.HeartHeal,
            SkillEffectType.Summon => SkillVfx.SummonCircle,
            SkillEffectType.ResonanceGain => SkillVfx.MusicNotes,
            SkillEffectType.LampGain => SkillVfx.Firefly,
            _ => SkillVfx.StarBurst,
        };

        // ───────────── 스테이지 기믹 ─────────────

        /// <summary>스테이지 기믹 한 줄 설명 (전투 화면 안내)</summary>
        public static string ModifierDescription(StageModifierType modifier) => modifier switch
        {
            StageModifierType.Rain => "적이 느려지고 비 특성이 강해져요",
            StageModifierType.Blackout => "그림자가 숨어요 · 빛 특성이 크게 강해져요",
            StageModifierType.FullMoon => "불빛이 잘 차오르고 밤 특성이 강해져요",
            StageModifierType.Wind => "적도 잔향이도 빨라져요",
            StageModifierType.DustStorm => "먼지뭉치가 몰려오고 천 특성이 강해져요",
            StageModifierType.Festival => "단추를 더 얻고 치장·소리 특성이 강해져요",
            _ => string.Empty,
        };

        // ───────────── 표 ─────────────

        static Dictionary<string, SkillLook> Build()
        {
            var looks = new Dictionary<string, SkillLook>(40);

            void Add(string key, SkillVfxStyle style, string main, string accent, VfxMotif particle, VfxMotif fallback) =>
                looks[key] = new SkillLook(style, Hex(main), Hex(accent), particle, fallback);

            // 천 / 단추 (짝짝이·똑단이)
            Add(SkillVfx.ThreadStitch, SkillVfxStyle.Projectile, "F2A6C6", "C4B2F2", VfxMotif.Sparkle, VfxMotif.Sparkle);
            Add(SkillVfx.PatchHeal, SkillVfxStyle.Rise, "8FE3B0", "FFB3C8", VfxMotif.Heart, VfxMotif.Heart);
            Add(SkillVfx.ButtonShield, SkillVfxStyle.Dome, "93C8F0", "FFF4DC", VfxMotif.Button, VfxMotif.Bubble);
            Add(SkillVfx.SpinButton, SkillVfxStyle.Orbit, "F2C46B", "93C8F0", VfxMotif.Button, VfxMotif.Button);
            // 열쇠 (찰칵이)
            Add(SkillVfx.KeyholeLock, SkillVfxStyle.OverHead, "E8C27A", "C4B2F2", VfxMotif.Star, VfxMotif.Keyhole);
            Add(SkillVfx.KeyBeam, SkillVfxStyle.Projectile, "FFE7A8", "E8C27A", VfxMotif.Sparkle, VfxMotif.Sparkle);
            // 촛불 (몽당이)
            Add(SkillVfx.CandleBurst, SkillVfxStyle.Burst, "FFB86B", "FFF1D6", VfxMotif.Flame, VfxMotif.Flame);
            Add(SkillVfx.WaxDrip, SkillVfxStyle.Ground, "FFF1D6", "E9A6C6", VfxMotif.Drop, VfxMotif.Soft);
            Add(SkillVfx.Firefly, SkillVfxStyle.Rise, "F4F1A0", "FFD98A", VfxMotif.Glow, VfxMotif.Glow);
            Add(SkillVfx.LanternBeam, SkillVfxStyle.Beam, "FFD98A", "FFF4DC", VfxMotif.Sparkle, VfxMotif.Glow);
            // 우산 (우두커니)
            Add(SkillVfx.UmbrellaShield, SkillVfxStyle.Dome, "7FA8D9", "F2C46B", VfxMotif.Drop, VfxMotif.Bubble);
            Add(SkillVfx.Raindrops, SkillVfxStyle.Rain, "93C8F0", "C9E4FF", VfxMotif.Drop, VfxMotif.Drop);
            Add(SkillVfx.StormSwirl, SkillVfxStyle.Ring, "8EC5F0", "A8E6D8", VfxMotif.Trail, VfxMotif.Swirl);
            // 립스틱 (루즈리)
            Add(SkillVfx.LipstickHeart, SkillVfxStyle.OverHead, "E58BAE", "FFF4DC", VfxMotif.Heart, VfxMotif.Heart);
            Add(SkillVfx.KissBurst, SkillVfxStyle.Burst, "F28CA8", "FFD1E0", VfxMotif.Heart, VfxMotif.Heart);
            Add(SkillVfx.GlossShine, SkillVfxStyle.Burst, "FFF4DC", "F2B8D0", VfxMotif.Sparkle, VfxMotif.Star);
            // 오르골 (라라벨)
            Add(SkillVfx.MusicNotes, SkillVfxStyle.Rise, "C4B2F2", "F2B8D0", VfxMotif.Note, VfxMotif.Note);
            Add(SkillVfx.SoundRing, SkillVfxStyle.Ring, "C4B2F2", "FFF4DC", VfxMotif.Note, VfxMotif.Ring);
            Add(SkillVfx.WaltzRibbon, SkillVfxStyle.Orbit, "F2B8D0", "C4B2F2", VfxMotif.Trail, VfxMotif.Swirl);
            // 공용
            Add(SkillVfx.StarBurst, SkillVfxStyle.Burst, "F2C46B", "FFF4DC", VfxMotif.Star, VfxMotif.Star);
            Add(SkillVfx.MoonGlow, SkillVfxStyle.Ground, "FFF1C4", "C4B2F2", VfxMotif.Sparkle, VfxMotif.Glow);
            Add(SkillVfx.HeartHeal, SkillVfxStyle.Rise, "FFB3C8", "8FE3B0", VfxMotif.Heart, VfxMotif.Heart);
            Add(SkillVfx.BuffCrown, SkillVfxStyle.OverHead, "F2C46B", "FFF4DC", VfxMotif.Sparkle, VfxMotif.Crown);
            Add(SkillVfx.DebuffMark, SkillVfxStyle.OverHead, "9C7BD6", "5A4A8A", VfxMotif.Soft, VfxMotif.DownArrow);
            Add(SkillVfx.StunStars, SkillVfxStyle.OverHead, "F2C46B", "FFF4DC", VfxMotif.Star, VfxMotif.Star);
            Add(SkillVfx.SlowYarn, SkillVfxStyle.Ground, "C4B2F2", "F2A6C6", VfxMotif.Yarn, VfxMotif.Yarn);
            Add(SkillVfx.SummonCircle, SkillVfxStyle.Ground, "C9A6FF", "F2C46B", VfxMotif.Sparkle, VfxMotif.MagicCircle);
            Add(SkillVfx.ShieldBubble, SkillVfxStyle.Dome, "BFE3FF", "F2B8D0", VfxMotif.Sparkle, VfxMotif.Bubble);
            Add(SkillVfx.CleanseSparkle, SkillVfxStyle.Rise, "E8FFF4", "8FE3B0", VfxMotif.Sparkle, VfxMotif.Sparkle);
            Add(SkillVfx.DustPoof, SkillVfxStyle.Burst, "B9B3CC", "FFF4DC", VfxMotif.Soft, VfxMotif.Soft);
            Add(SkillVfx.ShadowBurst, SkillVfxStyle.Burst, "6B5A8E", "C4B2F2", VfxMotif.Soft, VfxMotif.Soft);
            Add(SkillVfx.SplitYarn, SkillVfxStyle.Burst, "E9A6C6", "C4B2F2", VfxMotif.Yarn, VfxMotif.Yarn);
            Add(SkillVfx.RevealLight, SkillVfxStyle.Ring, "FFF4DC", "FFD98A", VfxMotif.Sparkle, VfxMotif.Ring);
            return looks;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
