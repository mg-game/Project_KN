using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>기본 공격 투사체가 날아가는 길</summary>
    public enum AttackPath : byte
    {
        /// <summary>곧게 (빗방울 창·단추·열쇠)</summary>
        Straight,
        /// <summary>위로 둥글게 휘는 호 (바늘·불씨)</summary>
        Arc,
        /// <summary>좌우로 살랑이며 (입술 하트)</summary>
        Wobble,
        /// <summary>통통 두 번 튀는 호 + 살랑임 (리본 음표)</summary>
        Bounce,
    }

    /// <summary>계보 명중에 곁들이는 사물 모티프 연출</summary>
    public enum AttackExtra : byte
    {
        None,
        /// <summary>짝짝이: X 바늘땀 자국 2개가 적 몸에 잠깐 붙는다 ("꿰맸다")</summary>
        Stitch,
        /// <summary>똑단이: 작은 단추 3개가 바닥에 통통 튀었다 사라진다</summary>
        Buttons,
        /// <summary>몽당이: 촛농 방울이 떨어져 바닥 얼룩 + 불씨가 떠오른다</summary>
        Wax,
        /// <summary>우두커니: 물방울이 튀고 바닥에 물결 고리 (비 오는 날엔 더 크게)</summary>
        Ripple,
        /// <summary>루즈리: 작은 하트가 떠오른다</summary>
        Hearts,
        /// <summary>라라벨: 작은 음파 고리</summary>
        SoundRing,
        /// <summary>찰칵이: 금빛 고리가 대상 위로 오므라든다 ("잠김")</summary>
        LockRing,
    }

    /// <summary>투사체가 흘리는 작은 조각 / 총구 빛줄기 모양 (FX-JUICE §1.21 '불꽃과 조각' 열)</summary>
    public enum LineageShard : byte
    {
        /// <summary>짝짝이: 가는 보라 바늘 획 (fx_streak 3px)</summary>
        NeedleTick,
        /// <summary>똑단이: 작은 단추</summary>
        Button,
        /// <summary>찰칵이: 금빛 반짝 먼지 (fx_mote_twinkle)</summary>
        Twinkle,
        /// <summary>몽당이: 떠오르는 불씨 (motif_flame)</summary>
        Ember,
        /// <summary>우두커니: 포물선 물방울</summary>
        Drop,
        /// <summary>루즈리: 작은 하트</summary>
        Heart,
        /// <summary>라라벨: 음표</summary>
        Note,
    }

    /// <summary>일반 명중 고리 그림 (FX-JUICE §1.21 '고리' 열)</summary>
    public enum LineageRing : byte
    {
        /// <summary>빛 충격 고리 (fx_shockwave)</summary>
        Shockwave,
        /// <summary>바늘땀 실 고리 (fx_ring_thread)</summary>
        Thread,
        /// <summary>음파 고리 (vfx_sound_ring)</summary>
        SoundRing,
        /// <summary>발밑 바닥에 누운 충격 고리 (물결)</summary>
        Ground,
    }

    /// <summary>계보 치명타 특수 연출 (FX-JUICE E5 11행 / §1.21)</summary>
    public enum CritSpecial : byte
    {
        None,
        /// <summary>짝짝이: X 바늘땀 도장 2개 + 실 채찍</summary>
        StitchWhip,
        /// <summary>똑단이: 단추 '딸깍' + 작은 단추 6개 튐 + 금빛 바늘땀 고리</summary>
        ButtonClack,
        /// <summary>찰칵이: 열쇠구멍 잠김 고리 + 금빛 섬광 훑기</summary>
        KeyLock,
        /// <summary>몽당이: 불꽃이 위로 솟고 따뜻한 빛 웅덩이 + 불씨 6</summary>
        FlameFlare,
        /// <summary>우두커니: 물왕관 + 겹 물결 + 물방울 8</summary>
        SplashCrown,
        /// <summary>루즈리: 입맞춤 도장 + 하트 3 + 분홍 빛 웅덩이</summary>
        KissStamp,
        /// <summary>라라벨: 세 음 부채 + 금빛 오선 고리</summary>
        ChordFan,
    }

    /// <summary>
    /// 계보 손맛 서명 (FX-JUICE §1.21): 총구 빛줄기 색 · 비행 중 흘리는 조각 · 일반 명중 고리 · 치명타 바닥 자국 · 치명타 특수.
    /// 크기는 기준 px (× 액터 배율). 중력은 px/s² (음수 = 떠오른다)
    /// </summary>
    public readonly struct LineageSignature
    {
        /// <summary>총구 빛줄기(E1 3행) 색 — 보조색, 찰칵이·똑단이·라라벨은 강조(금)색</summary>
        public readonly Color Spark;
        public readonly LineageShard Shard;
        public readonly Color ShardColor;
        public readonly float ShardSize;
        public readonly float ShardGravity;
        public readonly LineageRing Ring;
        public readonly Color RingTint;
        /// <summary>치명타 바닥 자국이 있는지 (라라벨은 음표가 떠오르므로 없음)</summary>
        public readonly bool HasDecal;
        public readonly JuiceDecal Decal;
        public readonly CritSpecial Special;

        public LineageSignature(Color spark, LineageShard shard, Color shardColor, float shardSize, float shardGravity, LineageRing ring, Color ringTint,
            bool hasDecal, JuiceDecal decal, CritSpecial special)
        {
            Spark = spark;
            Shard = shard;
            ShardColor = shardColor;
            ShardSize = shardSize;
            ShardGravity = shardGravity;
            Ring = ring;
            RingTint = ringTint;
            HasDecal = hasDecal;
            Decal = decal;
            Special = special;
        }
    }

    /// <summary>
    /// 명중 등급 (FX-01 타격 사다리). 번쩍임 길이·대상 정지·넉백·흔들림이 등급마다 다르다.
    /// 수치는 HitFeel이 정한다.
    /// </summary>
    public enum HitTier : byte
    {
        /// <summary>T0 일반 기본 공격</summary>
        Normal,
        /// <summary>T1 강타 (저격·돌격 명중, 보호막 깨짐)</summary>
        Heavy,
        /// <summary>T2 치명타</summary>
        Crit,
        /// <summary>T3 스킬 피해</summary>
        Skill,
        /// <summary>불 밝히기 빛 물결</summary>
        Lamp,
        /// <summary>지속 피해 (촛불·반딧불): 번쩍임만 약하게, 넉백 없음</summary>
        Tick,
    }

    /// <summary>계보 하나의 기본 공격 모양새 (ATK-01 계보 표). 크기는 기준 화면(가로 1080) 픽셀</summary>
    public readonly struct LineageAttackLook
    {
        public readonly string Id;
        public readonly Color Main;
        public readonly Color Sub;
        public readonly Color Accent;
        public readonly AttackPath Path;
        /// <summary>호 높이 (px)</summary>
        public readonly float ArcPx;
        /// <summary>좌우 살랑임 폭 (px)</summary>
        public readonly float WobblePx;
        /// <summary>기준 거리에서의 비행 시간 (초)</summary>
        public readonly float Life;
        /// <summary>명중 그림 크기 (px)</summary>
        public readonly float HitSize;
        /// <summary>그림이 없을 때 날아가는 흰색 모양 / 명중 모양</summary>
        public readonly VfxMotif ShotMotif;
        public readonly VfxMotif HitMotif;
        /// <summary>그림이 없을 때 흰색 모양이 도는 빠르기 (도/초, 0 = 진행 방향을 본다)</summary>
        public readonly float FallbackSpin;
        /// <summary>명중 그림을 돌리지 않는다 (찰칵이 열쇠구멍 문장)</summary>
        public readonly bool UprightHit;
        /// <summary>저격이 아니어도 가는 빛 꼬리(atk_snipe_trail)를 단다 (찰칵이 열쇠 빛 화살)</summary>
        public readonly bool Streak;
        public readonly AttackExtra Extra;
        /// <summary>효과음 키 (AUD-01 예약)</summary>
        public readonly string Sfx;
        /// <summary>
        /// 그림 투사체가 도는 빠르기 (도/초, SPEC4 §13.5 a — 똑단이 단추 720, 나머지 0). 0이 아니면 그림 머리 · 잔상이 진행 방향을 보지 않고
        /// 가운데를 축으로 돈다 (AttackFxCatalog.ArtShotPose). 그림이 없을 때는 FallbackSpin
        /// </summary>
        public readonly float ArtSpin;

        public LineageAttackLook(string id, Color main, Color sub, Color accent, AttackPath path, float arcPx, float wobblePx, float life, float hitSize,
            VfxMotif shotMotif, VfxMotif hitMotif, float fallbackSpin, bool uprightHit, bool streak, AttackExtra extra, string sfx, float artSpin = 0f)
        {
            Id = id;
            Main = main;
            Sub = sub;
            Accent = accent;
            Path = path;
            ArcPx = arcPx;
            WobblePx = wobblePx;
            Life = life;
            HitSize = hitSize;
            ShotMotif = shotMotif;
            HitMotif = hitMotif;
            FallbackSpin = fallbackSpin;
            UprightHit = uprightHit;
            Streak = streak;
            Extra = extra;
            Sfx = sfx;
            ArtSpin = artSpin;
        }

        /// <summary>바닥 고리·바늘땀처럼 어두운 바닥 위에 그리는 색 (보조색이 너무 어두우면 주색 쪽으로 밝힌다)</summary>
        public Color GroundColor => Sub.grayscale < 0.3f ? Color.Lerp(Sub, Main, 0.6f) : Sub;
    }

    /// <summary>성향별 기본 공격 변형 (ATK-01 성향 표)</summary>
    public readonly struct StyleAttackMod
    {
        public readonly CombatStyle Style;
        public readonly float FlightScale;
        public readonly float ArcScale;
        public readonly float HitScale;
        /// <summary>계보 길 대신 곧게, 비행 시간 고정 (저격)</summary>
        public readonly bool Straight;
        /// <summary>투사체 가로 배율 (저격 1.3)</summary>
        public readonly float ShotStretch;
        /// <summary>가는 빛 꼬리(atk_snipe_trail, 길이 = 거리 × 0.6)</summary>
        public readonly bool SnipeTrail;
        /// <summary>명중 자리에 초승달 베기(atk_slash)를 겹친다 (돌격)</summary>
        public readonly bool Slash;
        /// <summary>명중점에서 바닥까지 실 끈 (포획)</summary>
        public readonly bool Thread;
        /// <summary>감속이 걸려 있으면 바닥에 보조색 납작 고리 (방어)</summary>
        public readonly bool SlowRing;
        public readonly float NumberScale;
        public readonly HitTier Tier;
        /// <summary>잔향이가 이 성향이면 멀리서 쏜다 (저격·지원·힐), 아니면 근접 베기</summary>
        public readonly bool Ranged;

        public StyleAttackMod(CombatStyle style, float flightScale, float arcScale, float hitScale, bool straight, float shotStretch, bool snipeTrail,
            bool slash, bool thread, bool slowRing, float numberScale, HitTier tier, bool ranged)
        {
            Style = style;
            FlightScale = flightScale;
            ArcScale = arcScale;
            HitScale = hitScale;
            Straight = straight;
            ShotStretch = shotStretch;
            SnipeTrail = snipeTrail;
            Slash = slash;
            Thread = thread;
            SlowRing = slowRing;
            NumberScale = numberScale;
            Tier = tier;
            Ranged = ranged;
        }
    }

    /// <summary>
    /// 기본 공격 연출 목록 (ATK-01 / ATK-02 / ATK-07): 계보 ID → 투사체·명중 그림(Art/VFX/atk_{계보}_shot·_hit, 씬 빌더가 굽는다)과
    /// 계보 모양새(길·호·비행 시간·명중 크기·곁들임), 성향별 변형, 비행 시간·처치 보류 규칙.
    /// 그림이 없으면 null을 돌려주고 BattleFx가 흰색 모양(VfxMotif)에 계보 색을 입혀 대체한다 (예외 없음).
    /// 모양새 표는 데이터만 (MonoBehaviour 없이 테스트할 수 있게 static).
    /// </summary>
    public class AttackFxCatalog : MonoBehaviour
    {
        [Serializable]
        public struct LineageArt
        {
            public string lineageId;
            [Tooltip("투사체 (끝이 오른쪽을 향한 그림, Art/VFX/atk_{계보}_shot)")]
            public Sprite shot;
            [Tooltip("명중 (가운데 정렬, Art/VFX/atk_{계보}_hit)")]
            public Sprite hit;
        }

        [SerializeField] LineageArt[] _lineages;
        [Tooltip("초승달 베기 (Art/VFX/atk_slash) — 돌격 지킴이 / 근접 잔향이")]
        [SerializeField] Sprite _slash;
        [Tooltip("저격 빛 꼬리 (Art/VFX/atk_snipe_trail, 512x128, 끝이 오른쪽)")]
        [SerializeField] Sprite _snipeTrail;

        /// <summary>투사체 그림에서 끝(tip)이 오는 가로 위치 (256 중 252)</summary>
        public const float ShotTipPivot = 0.97f;
        /// <summary>비행 시간을 정한 기준 거리 (px, 기준 화면에서 방위 링 지킴이 → 부채꼴 가운데 적). 화면 크기(액터 배율)만큼 늘려 잰다</summary>
        public const float ReferenceDistance = 240f;
        /// <summary>저격 비행 시간 (거리와 상관없이 짧게)</summary>
        public const float SnipeLife = 0.12f;
        /// <summary>처치 보류 최소 / 최대 (ATK-09)</summary>
        public const float KillHoldMin = 0.14f;
        public const float KillHoldMax = 0.30f;
        /// <summary>
        /// 기본 공격 비행 시간 상한 = 처치 보류 상한 − 0.02 (어느 배속·거리·성향에서도 마지막 일격이 처치 연출보다 먼저 닿게 —
        /// 1배속 라라벨 힐처럼 길게 튀는 비행도 0.28초 안에)
        /// </summary>
        public const float MaxFlight = KillHoldMax - 0.02f;

        /// <summary>정식 계보 7종 (씬 빌더가 이 순서로 그림을 굽는다)</summary>
        public static readonly string[] KnownLineages = { "jjakjjak", "ttokdan", "chalkak", "mongdang", "udukeoni", "rouge", "lalabell" };

        static readonly LineageAttackLook[] Looks =
        {
            new("jjakjjak", Hex(0xF3E6D0), Hex(0x8A63C9), Hex(0xE79BB8), AttackPath.Arc, 24f, 0f, 0.20f, 110f,
                VfxMotif.Trail, VfxMotif.Star, 0f, false, false, AttackExtra.Stitch, "atk_needle"),
            new("ttokdan", Hex(0x93C8F0), Hex(0xFFF4DC), Hex(0xF2C46B), AttackPath.Straight, 0f, 0f, 0.18f, 100f,
                VfxMotif.Button, VfxMotif.Star, 720f, false, false, AttackExtra.Buttons, "atk_button", 720f),
            new("chalkak", Hex(0xE8C27A), Hex(0xC4B2F2), Hex(0xE9A6C6), AttackPath.Straight, 0f, 0f, 0.12f, 120f,
                VfxMotif.Trail, VfxMotif.Keyhole, 0f, true, true, AttackExtra.LockRing, "atk_key"),
            new("mongdang", Hex(0xFFF1D6), Hex(0xE9A6C6), Hex(0xFFB86B), AttackPath.Arc, 50f, 0f, 0.24f, 110f,
                VfxMotif.Flame, VfxMotif.Flame, 0f, false, false, AttackExtra.Wax, "atk_candle"),
            new("udukeoni", Hex(0x7FA8D9), Hex(0x2D2458), Hex(0xF2C46B), AttackPath.Straight, 0f, 0f, 0.16f, 110f,
                VfxMotif.Drop, VfxMotif.Ring, 0f, false, false, AttackExtra.Ripple, "atk_rain"),
            new("rouge", Hex(0xE58BAE), Hex(0xE8C27A), Hex(0xFFF4DC), AttackPath.Wobble, 0f, 8f, 0.22f, 115f,
                VfxMotif.Heart, VfxMotif.Heart, 0f, false, false, AttackExtra.Hearts, "atk_kiss"),
            new("lalabell", Hex(0xC4B2F2), Hex(0xF2B8D0), Hex(0xF2C46B), AttackPath.Bounce, 40f, 6f, 0.26f, 120f,
                VfxMotif.Note, VfxMotif.Note, 0f, false, false, AttackExtra.SoundRing, "atk_musicbox"),
        };

        /// <summary>계보를 모를 때 (데이터가 비었거나 새 계보): 라벤더 빛 구슬</summary>
        static readonly LineageAttackLook Generic = new(null, UITheme.Lavender, UITheme.SoftBlue, UITheme.Cream, AttackPath.Arc, 24f, 0f, 0.2f, 100f,
            VfxMotif.Glow, VfxMotif.Star, 0f, false, false, AttackExtra.None, "atk_generic");

        /// <summary>
        /// 계보 손맛 서명 (Looks와 같은 순서). 우두커니 보조색(#2D2458 잉크)은 빛으로 더하면 보이지 않으므로 총구 빛줄기는 주색,
        /// 찰칵이의 '금빛' 반짝임은 주색(#E8C27A 열쇠 금)으로 — 금(#F2C46B)은 치명타·불 밝히기에 아껴 둔다.
        /// 똑단이 실 고리는 스펙 §1.21대로 주색(#93C8F0 하늘) — 보조색 크림(#FFF4DC)이면 크림 실 그림 위에 크림이라 흰 비눗방울로 보였다
        /// </summary>
        static readonly LineageSignature[] Signatures =
        {
            new(Hex(0xA587D8), LineageShard.NeedleTick, Hex(0x8A63C9), 14f, 0f, LineageRing.Thread, Hex(0xDCCBF4), true, JuiceDecal.Thread,
                CritSpecial.StitchWhip),
            new(Hex(0xF2C46B), LineageShard.Button, Hex(0x93C8F0), 10f, 160f, LineageRing.Thread, Hex(0x93C8F0), true, JuiceDecal.Buttons,
                CritSpecial.ButtonClack),
            new(Hex(0xE8C27A), LineageShard.Twinkle, Hex(0xE8C27A), 12f, 0f, LineageRing.Shockwave, Hex(0xF2C46B), true, JuiceDecal.Glitter,
                CritSpecial.KeyLock),
            new(Hex(0xE9A6C6), LineageShard.Ember, Hex(0xFFB86B), 12f, -120f, LineageRing.Shockwave, Hex(0xFFB86B), true, JuiceDecal.Petal,
                CritSpecial.FlameFlare),
            new(Hex(0x7FA8D9), LineageShard.Drop, Hex(0x7FA8D9), 10f, 300f, LineageRing.Ground, Hex(0x93C0EC), true, JuiceDecal.Puddle,
                CritSpecial.SplashCrown),
            new(Hex(0xE8C27A), LineageShard.Heart, Hex(0xE58BAE), 10f, -30f, LineageRing.Shockwave, Hex(0xE58BAE), true, JuiceDecal.Heart,
                CritSpecial.KissStamp),
            new(Hex(0xF2C46B), LineageShard.Note, Hex(0xF2B8D0), 12f, -20f, LineageRing.SoundRing, Color.white, false, JuiceDecal.Glitter,
                CritSpecial.ChordFan),
        };

        static readonly LineageSignature GenericSignature = new(UITheme.Cream, LineageShard.Twinkle, UITheme.Cream, 12f, 0f, LineageRing.Shockwave,
            UITheme.Lavender, false, JuiceDecal.Glitter, CritSpecial.None);

        // ───────────── 지킴이 웅크림 (FX-JUICE E1) ─────────────

        /// <summary>쏘기 전 웅크림 W = clamp(비행 × 0.3, 0.02, 0.05)초, 2배속이면 0.03초까지 (총구·투사체는 W 뒤에 나가고 도착 시각은 그대로)</summary>
        public const float WindupRatio = 0.3f;
        public const float WindupMin = 0.02f;
        public const float WindupMax = 0.05f;
        public const float WindupMaxFast = 0.03f;

        /// <summary>CombatStyle 값 순서 (Defense, Assault, Capture, Snipe, Support, Heal)</summary>
        static readonly StyleAttackMod[] Mods =
        {
            new(CombatStyle.Defense, 1f, 1f, 1f, false, 1f, false, false, false, true, 1f, HitTier.Normal, false),
            new(CombatStyle.Assault, 0.8f, 0.5f, 1.1f, false, 1f, false, true, false, false, 1.15f, HitTier.Heavy, false),
            new(CombatStyle.Capture, 1f, 1.4f, 1f, false, 1f, false, false, true, false, 1f, HitTier.Normal, false),
            new(CombatStyle.Snipe, 1f, 0f, 1.25f, true, 1.3f, true, false, false, false, 1.15f, HitTier.Heavy, true),
            new(CombatStyle.Support, 1f, 2f, 0.85f, false, 1f, false, false, false, false, 1f, HitTier.Normal, true),
            new(CombatStyle.Heal, 1.1f, 2.2f, 0.8f, false, 1f, false, false, false, false, 1f, HitTier.Normal, true),
        };

        // ───────────── 모양새 표 ─────────────

        /// <summary>계보의 기본 공격 모양새 (모르면 라벤더 빛 구슬)</summary>
        public static LineageAttackLook Look(string lineageId)
        {
            if (!string.IsNullOrEmpty(lineageId))
            {
                for (int i = 0; i < Looks.Length; i++)
                {
                    if (string.Equals(Looks[i].Id, lineageId, StringComparison.Ordinal)) return Looks[i];
                }
            }
            return Generic;
        }

        /// <summary>표에 있는 계보인지 (테스트·검사용)</summary>
        public static bool HasLook(string lineageId) => !string.IsNullOrEmpty(lineageId) && Look(lineageId).Id != null;

        /// <summary>계보 손맛 서명 (총구 빛줄기·흘리는 조각·명중 고리·자국·치명타 특수). 모르는 계보면 크림 반짝임 기본값</summary>
        public static LineageSignature Signature(string lineageId)
        {
            if (!string.IsNullOrEmpty(lineageId))
            {
                for (int i = 0; i < Looks.Length && i < Signatures.Length; i++)
                {
                    if (string.Equals(Looks[i].Id, lineageId, StringComparison.Ordinal)) return Signatures[i];
                }
            }
            return GenericSignature;
        }

        /// <summary>
        /// 지킴이 웅크림 시간 (초): clamp(비행 × 0.3, 0.02, 0.05), 2배속이면 최대 0.03. 투사체가 적어도 비행의 절반은 날아가도록 비행 × 0.5를 넘지 않는다
        /// </summary>
        public static float Windup(float flight, int speed)
        {
            float windup = Mathf.Clamp(flight * WindupRatio, WindupMin, WindupMax);
            if (speed >= 2) windup = Mathf.Min(windup, WindupMaxFast);
            return Mathf.Clamp(windup, 0f, Mathf.Max(0f, flight * 0.5f));
        }

        /// <summary>리듬 강조 명중(같은 대상 사슬의 3번째마다 — FX-JUICE E6)의 피해 숫자 배율</summary>
        public const float RhythmNumberScale = 1.2f;

        /// <summary>
        /// 피해 숫자 뜻 (FX-JUICE §4.2 우선순위): 치명타 &gt; 표식 &gt; 스킬 &gt; 불 밝히기 &gt; 보스 &gt; 강타 &gt; 일반. 지속 피해 틱은 늘 Dot (치명타가 나지 않는다)
        /// </summary>
        public static DamageNumberKind NumberKind(HitTier tier, bool marked, bool boss)
        {
            if (tier == HitTier.Crit) return DamageNumberKind.Crit;
            if (tier == HitTier.Tick) return DamageNumberKind.Dot;
            if (marked) return DamageNumberKind.Mark;
            if (tier == HitTier.Skill) return DamageNumberKind.Skill;
            if (tier == HitTier.Lamp) return DamageNumberKind.Lamp;
            if (boss) return DamageNumberKind.Boss;
            return tier == HitTier.Heavy ? DamageNumberKind.Heavy : DamageNumberKind.Normal;
        }

        /// <summary>성향 변형 (모르는 값이면 방어 기준)</summary>
        public static StyleAttackMod Style(CombatStyle style)
        {
            int index = (int)style;
            return index >= 0 && index < Mods.Length ? Mods[index] : Mods[0];
        }

        /// <summary>
        /// 그림 투사체 머리 · 잔상의 자세 (SPEC4 §13.5 a): ArtSpin이 0이 아니면 가운데 기준점(0.5, 0.5) · 진행 방향 안 봄 · Spin = ArtSpin
        /// (똑단이 단추가 팽이처럼 돌며 날아간다), 아니면 끝 기준점(ShotTipPivot, 0.5) · 진행 방향을 본다 · 돌지 않는다
        /// </summary>
        public static void ArtShotPose(in LineageAttackLook look, out Vector2 pivot, out bool alignToPath, out float spin)
        {
            if (look.ArtSpin != 0f)
            {
                pivot = new Vector2(0.5f, 0.5f);
                alignToPath = false;
                spin = look.ArtSpin;
                return;
            }
            pivot = new Vector2(ShotTipPivot, 0.5f);
            alignToPath = true;
            spin = 0f;
        }

        /// <summary>
        /// 투사체에 가는 빛 꼬리(atk_snipe_trail)를 다는지 (SPEC4 §13.5 b): 지킴이는 저격이거나 꼬리 계보(찰칵이 열쇠 빛 화살),
        /// 잔향이 원거리 투사체는 저격일 때만 (연구 ATK-02 — 꼬리는 저격 몫)
        /// </summary>
        public static bool DrawsStreak(in LineageAttackLook look, in StyleAttackMod mod, bool echo) => mod.SnipeTrail || (!echo && look.Streak);

        // ───────────── 시간 규칙 (ATK-09) ─────────────

        /// <summary>배속에 따른 비행 시간 배율 (2배속이면 0.6 — 시뮬레이션 시간으로는 1.2배라 처치보다 늦게 닿지 않게)</summary>
        public static float SpeedFlightScale(int speed) => speed >= 2 ? 0.6f : 1f;

        /// <summary>배속에 따른 명중 팝·처치 연출 시간 배율 (2배속 0.8)</summary>
        public static float SpeedPopScale(int speed) => speed >= 2 ? 0.8f : 1f;

        /// <summary>
        /// 투사체 비행 시간: 계보 기본값(저격은 0.12초 고정) × 거리 배율(√(거리/(기준 × 액터 배율)), 0.75~1.3) × 성향 배율 × 배속 배율,
        /// 최대 MaxFlight(0.28초 — 처치 보류 상한 안에 닿게). 저격은 거리와 상관없이 짧게 꽂힌다.
        /// unitScale = 액터 배율 (BattleFieldMapper.ActorScale — 화면이 커도 같은 자리의 적이면 비행 시간이 같다)
        /// </summary>
        public static float FlightTime(in LineageAttackLook look, in StyleAttackMod mod, float distancePx, int speed, float unitScale = 1f)
        {
            float life;
            if (mod.Straight) life = SnipeLife;
            else
            {
                float reference = ReferenceDistance * Mathf.Max(0.5f, unitScale);
                float reach = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, distancePx) / reference), 0.75f, 1.3f);
                life = look.Life * reach * mod.FlightScale;
            }
            return Mathf.Clamp(life * SpeedFlightScale(speed), 0.05f, MaxFlight);
        }

        /// <summary>
        /// 처치 보류 시간: 대상에게 아직 날아오는 투사체가 있으면 가장 늦은 도착 + 0.02초까지 버틴다 (최소 0.14, 최대 0.30).
        /// 처치 그림은 그 순간에 터진다 — 마지막 일격이 허공을 치지 않게.
        /// </summary>
        public static float KillHold(float remainingFlight) => Mathf.Clamp(Mathf.Max(KillHoldMin, remainingFlight + 0.02f), KillHoldMin, KillHoldMax);

        // ───────────── 그림 ─────────────

        /// <summary>계보 투사체 그림 (없으면 null)</summary>
        public Sprite Shot(string lineageId) => Find(lineageId, true);

        /// <summary>계보 명중 그림 (없으면 null)</summary>
        public Sprite Hit(string lineageId) => Find(lineageId, false);

        public Sprite Slash => _slash;
        public Sprite SnipeTrail => _snipeTrail;

        Sprite Find(string lineageId, bool shot)
        {
            if (_lineages == null || string.IsNullOrEmpty(lineageId)) return null;
            for (int i = 0; i < _lineages.Length; i++)
            {
                if (!string.Equals(_lineages[i].lineageId, lineageId, StringComparison.Ordinal)) continue;
                return shot ? _lineages[i].shot : _lineages[i].hit;
            }
            return null;
        }

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
