using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 스킬 효과 하나. 값의 의미 (시전자 공격력 = 레벨·시너지가 반영된 공격력):
    /// <list type="bullet">
    /// <item>Damage: value × 공격력 피해</item>
    /// <item>Heal: value × 공격력만큼 회복 (대상 Shop이면 수선점, 아군이면 잔향이)</item>
    /// <item>Shield: value × 공격력만큼 피해 흡수 보호막, duration초 유지 (Shop이면 수선점 보호막)</item>
    /// <item>Slow: 이동 속도 value(0~1)만큼 감소, duration초</item>
    /// <item>Stun: duration초 기절 (보스는 BattleConfig.bossStunScale 배)</item>
    /// <item>Knockback: 수선점 바깥쪽으로 value(월드 단위)만큼 밀어냄</item>
    /// <item>Pull: 시전자(잔향이) 또는 대상 무리 중심 쪽으로 value만큼 끌어당김</item>
    /// <item>Mark: 받는 피해 +value%, duration초</item>
    /// <item>BuffAttack / BuffAttackSpeed: 대상 아군 공격력 / 공격 속도 +value%, duration초</item>
    /// <item>Summon: 시전자 능력치 × value 의 작은 아군(토큰 잔향이) count마리, duration초 유지</item>
    /// <item>ResonanceGain: 잔향 +value</item>
    /// <item>LampGain: 불빛 게이지 +value (1 = 가득)</item>
    /// <item>Reveal: 보이지 않는 적(Phasing)을 radius 안에서 duration초 드러냄</item>
    /// <item>DamageOverTime: 초당 value × 공격력 피해, duration초 (불꽃/녹이기)</item>
    /// <item>Execute (Passive 전용): 체력 30% 이하 적에게 주는 피해 +value%</item>
    /// <item>Chain: 첫 대상에서 가까운 적으로 count번 튀며 value × 공격력 피해</item>
    /// <item>AttackRamp (Passive 전용): 살아 있는 동안 초당 공격력 +value%, 최대 count%</item>
    /// <item>Cleanse: 대상 아군의 약화(녹 등) 제거 + duration초 면역</item>
    /// <item>HealOverTime: 초당 value × 공격력 회복, duration초</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class SkillEffectDef
    {
        public SkillEffectType type;
        public float value;
        public float duration;
        public int count;
    }

    /// <summary>
    /// 형태마다 하나씩 가지는 고유 스킬. 지킴이/잔향이 어느 쪽으로 배치돼도 같은 스킬을 쓴다
    /// (OnSummon/OnDissolve는 잔향이일 때만, 나머지는 양쪽 모두).
    /// </summary>
    [Serializable]
    public class SkillDef
    {
        public string id;
        public string name;
        [TextArea] public string description;
        [Tooltip("시전 시 머리 위에 잠깐 뜨는 대사. 예: \"꿰매 줄게!\"")]
        public string castText;
        public SkillTrigger trigger = SkillTrigger.Cooldown;
        [Tooltip("Cooldown 트리거의 시전 간격(초). OnKill/OnShopDamaged는 최소 간격")]
        public float cooldown = 8f;
        [Tooltip("전투 시작 후 첫 시전까지(초). 0이면 cooldown의 절반")]
        public float initialDelay;
        public SkillTargetType target = SkillTargetType.EnemiesInRange;
        [Tooltip("대상 범위 반지름 (월드 단위, 방위 링 = 1)")]
        public float radius = 0.6f;
        public List<SkillEffectDef> effects = new();
        [Tooltip("연출 키 (SkillVfx 상수). Art/VFX/{키}.png")]
        public string vfx;

        public bool IsEmpty => effects == null || effects.Count == 0;
    }

    /// <summary>
    /// 스킬 연출 키. 콘텐츠(스킬 데이터)가 이 키를 쓰고, 아트는 Art/VFX/{키}.png 를 만들고,
    /// 전투 화면은 키별 연출 방식(SkillVfx.StyleOf)으로 그린다.
    /// </summary>
    public static class SkillVfx
    {
        public const string ThreadStitch = "vfx_thread_stitch";     // 바늘과 실 호 — 투사체/선
        public const string PatchHeal = "vfx_patch_heal";           // 하트 천 패치 — 회복
        public const string ButtonShield = "vfx_button_shield";     // 큰 단추 방패 — 보호막
        public const string SpinButton = "vfx_spin_button";         // 회전 단추 칼날 — 회전 베기
        public const string KeyholeLock = "vfx_keyhole_lock";       // 열쇠구멍 자물쇠 — 기절
        public const string KeyBeam = "vfx_key_beam";               // 열쇠 빛 화살 — 투사체
        public const string CandleBurst = "vfx_candle_burst";       // 촛불 불꽃 폭발
        public const string WaxDrip = "vfx_wax_drip";               // 촛농 웅덩이 — 감속 장판
        public const string Firefly = "vfx_firefly";                // 반딧불 무리 — 떠오름
        public const string LanternBeam = "vfx_lantern_beam";       // 등불 빛줄기 — 광선
        public const string UmbrellaShield = "vfx_umbrella_shield"; // 우산 방패 돔
        public const string Raindrops = "vfx_raindrops";            // 빗방울 — 내림
        public const string StormSwirl = "vfx_storm_swirl";         // 폭풍 소용돌이 — 고리
        public const string LipstickHeart = "vfx_lipstick_heart";   // 립스틱 하트 표식
        public const string KissBurst = "vfx_kiss_burst";           // 입맞춤 자국 폭발
        public const string GlossShine = "vfx_gloss_shine";         // 광택 반짝임
        public const string MusicNotes = "vfx_music_notes";         // 음표 — 떠오름
        public const string SoundRing = "vfx_sound_ring";           // 음파 고리 — 퍼짐
        public const string WaltzRibbon = "vfx_waltz_ribbon";       // 리본 회오리 — 회전
        public const string StarBurst = "vfx_star_burst";           // 별 폭발 (치명타/공용)
        public const string MoonGlow = "vfx_moon_glow";             // 달빛 회복 오라
        public const string HeartHeal = "vfx_heart_heal";           // 하트 회복 — 떠오름
        public const string BuffCrown = "vfx_buff_crown";           // 강화 왕관/화살표 — 머리 위
        public const string DebuffMark = "vfx_debuff_mark";         // 약화 표식
        public const string StunStars = "vfx_stun_stars";           // 기절 별 — 머리 위 회전
        public const string SlowYarn = "vfx_slow_yarn";             // 실타래 거미줄 — 감속 장판
        public const string SummonCircle = "vfx_summon_circle";     // 소환 마법진 — 바닥
        public const string ShieldBubble = "vfx_shield_bubble";     // 보호막 거품
        public const string CleanseSparkle = "vfx_cleanse_sparkle"; // 정화 반짝
        public const string DustPoof = "vfx_dust_poof";             // 먼지 퐁 (적 처치)
        public const string ShadowBurst = "vfx_shadow_burst";       // 그림자 폭발 (보스/적)
        public const string SplitYarn = "vfx_split_yarn";           // 실뭉치 분열
        public const string RevealLight = "vfx_reveal_light";       // 드러냄 빛 고리

        /// <summary>스테이지 기믹 화면 덮개 (Art/VFX/overlay_{기믹}.png)</summary>
        public static string Overlay(StageModifierType modifier) => $"overlay_{modifier.ToString().ToLowerInvariant()}";

        public static readonly string[] All =
        {
            ThreadStitch, PatchHeal, ButtonShield, SpinButton, KeyholeLock, KeyBeam, CandleBurst, WaxDrip, Firefly,
            LanternBeam, UmbrellaShield, Raindrops, StormSwirl, LipstickHeart, KissBurst, GlossShine, MusicNotes,
            SoundRing, WaltzRibbon, StarBurst, MoonGlow, HeartHeal, BuffCrown, DebuffMark, StunStars, SlowYarn,
            SummonCircle, ShieldBubble, CleanseSparkle, DustPoof, ShadowBurst, SplitYarn, RevealLight,
        };
    }
}
