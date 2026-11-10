using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>바닥 자국 그림 (Art/VFX/Juice/decal_{petal|puddle|thread|buttons|glitter|heart})</summary>
    public enum JuiceDecal : byte
    {
        Petal,
        Puddle,
        Thread,
        Buttons,
        Glitter,
        Heart,
    }

    /// <summary>
    /// 손맛 연출 그림 묶음 (FX-JUICE §3.7 / §5). 씬 빌더(JuiceArtBuilder)가 Art/FX/Juice(빛 그림, 가산) · Art/VFX/Juice(색 그림) 파일을
    /// 정확한 경로로 굽는다. 그림이 아직 없으면 getter가 null을 돌려주고, BattleFx 기본 도구(CoreStar · Halo · Shockwave · Streaks …)가
    /// 안에서 예전 그림으로 대신한다 (코드는 새 그림이 꼭 있어야 하지 않는다). 그림을 나중에 넣으면 씬을 다시 만들어야 묶인다.
    ///   계보 그림: 명중 v2(가운데가 빈 고리 꼴) · 치명타 · 총구 불꽃 / 처치 갈래 꽃 / 바닥 자국 / 하나짜리 빛 그림·영웅 그림 /
    ///   다시 쓰는 기존 그림(색종이 · 반짝 먼지 · 빛살 · 부드러운 고리)도 여기서 함께 쥔다.
    /// </summary>
    public class JuiceArt : MonoBehaviour
    {
        [Serializable]
        public struct LineageJuice
        {
            public string lineageId;
            [Tooltip("명중 v2 (가운데가 열린 그림, Art/VFX/Juice/atk_{계보}_impact) — 없으면 atk_{계보}_hit")]
            public Sprite impact;
            [Tooltip("치명타 (Art/VFX/Juice/atk_{계보}_crit) — 없으면 atk_{계보}_hit ×1.4 + 별 폭발")]
            public Sprite crit;
            [Tooltip("총구 불꽃 (빛 그림, 오른쪽을 향함, Art/FX/Juice/muzzle_{계보}) — 없으면 코어 별에 강조색")]
            public Sprite muzzle;
        }

        [SerializeField] LineageJuice[] _lineages;
        [Tooltip("처치 꽃 (EnemyDeathFamily 순서: dust, moth, metal, shadow — Art/VFX/Juice/kill_{갈래})")]
        [SerializeField] Sprite[] _killBlooms;
        [Tooltip("바닥 자국 (JuiceDecal 순서: petal, puddle, thread, buttons, glitter, heart)")]
        [SerializeField] Sprite[] _decals;

        [Header("빛 그림 (Art/FX/Juice — 검정→알파, 가산)")]
        [SerializeField] Sprite _coreStar;
        [SerializeField] Sprite _glowCore;
        [SerializeField] Sprite _shockwave;
        [SerializeField] Sprite _starRing;
        [SerializeField] Sprite _streak;
        [SerializeField] Sprite _sparkleCluster;
        [SerializeField] Sprite _speedlines;
        [SerializeField] Sprite _flareCross;
        [SerializeField] Sprite _slashArc;
        [SerializeField] Sprite _afterimage;
        [SerializeField] Sprite _lampWaveRing;
        [SerializeField] Sprite _lightPillar;
        [SerializeField] Sprite _heroLampBloom;
        [Tooltip("플립북 빛 터짐 (P2, 4x4 = 16칸, 없으면 비어 있음)")]
        [SerializeField] Sprite[] _lightBurstFrames;

        [Header("색 그림 (Art/VFX/Juice)")]
        [SerializeField] Sprite _critStarBadge;
        [SerializeField] Sprite _comboPlate;
        [SerializeField] Sprite _heroBossKill;
        [SerializeField] Sprite _heroStageClear;

        [Header("다시 쓰는 기존 그림 (Art/FX, Art/VFX)")]
        [SerializeField] Sprite _confettiButton;
        [SerializeField] Sprite _confettiRibbon;
        [SerializeField] Sprite _confettiStar;
        [SerializeField] Sprite _confettiThread;
        [SerializeField] Sprite _moteDust;
        [SerializeField] Sprite _motePetal;
        [SerializeField] Sprite _moteTwinkle;
        [SerializeField] Sprite _moteFirefly;
        [SerializeField] Sprite _moteRaindrop;
        [SerializeField] Sprite _ringGlow;
        [SerializeField] Sprite _ringThread;
        [SerializeField] Sprite _rays;
        [SerializeField] Sprite _starBurst;
        [SerializeField] Sprite _glossShine;

        // ───────────── 계보 / 갈래 / 자국 ─────────────

        /// <summary>계보 명중 v2 그림 (없으면 null → atk_{계보}_hit)</summary>
        public Sprite Impact(string lineageId) => Find(lineageId, 0);

        /// <summary>계보 치명타 그림 (없으면 null)</summary>
        public Sprite Crit(string lineageId) => Find(lineageId, 1);

        /// <summary>계보 총구 불꽃 (빛 그림, 없으면 null)</summary>
        public Sprite Muzzle(string lineageId) => Find(lineageId, 2);

        /// <summary>처치 갈래 꽃 (없으면 null → enemy_death_{갈래})</summary>
        public Sprite KillBloom(EnemyDeathFamily family) => Pick(_killBlooms, (int)family);

        /// <summary>바닥 자국 (없으면 null → 자국 없음)</summary>
        public Sprite Decal(JuiceDecal kind) => Pick(_decals, (int)kind);

        // ───────────── 하나짜리 그림 (없으면 null) ─────────────

        public Sprite CoreStar => _coreStar;
        public Sprite GlowCore => _glowCore;
        public Sprite Shockwave => _shockwave;
        public Sprite StarRing => _starRing;
        public Sprite Streak => _streak;
        public Sprite SparkleCluster => _sparkleCluster;
        public Sprite Speedlines => _speedlines;
        public Sprite FlareCross => _flareCross;
        public Sprite SlashArc => _slashArc;
        public Sprite Afterimage => _afterimage;
        public Sprite LampWaveRing => _lampWaveRing;
        public Sprite LightPillar => _lightPillar;
        public Sprite CritStarBadge => _critStarBadge;
        public Sprite ComboPlate => _comboPlate;
        public Sprite HeroBossKill => _heroBossKill;
        public Sprite HeroLampBloom => _heroLampBloom;
        public Sprite HeroStageClear => _heroStageClear;

        /// <summary>플립북 칸 (P2 — 없으면 null, 칸 수는 LightBurstFrames.Length)</summary>
        public Sprite[] LightBurstFrames => _lightBurstFrames != null && _lightBurstFrames.Length > 0 ? _lightBurstFrames : null;

        // ───────────── 다시 쓰는 기존 그림 ─────────────

        public Sprite ConfettiButton => _confettiButton;
        public Sprite ConfettiRibbon => _confettiRibbon;
        public Sprite ConfettiStar => _confettiStar;
        public Sprite ConfettiThread => _confettiThread;
        public Sprite MoteDust => _moteDust;
        public Sprite MotePetal => _motePetal;
        public Sprite MoteTwinkle => _moteTwinkle;
        public Sprite MoteFirefly => _moteFirefly;
        public Sprite MoteRaindrop => _moteRaindrop;
        /// <summary>부드러운 빛 고리 (Art/FX/ring_glow — 충격 고리·불 밝히기 물결의 대체)</summary>
        public Sprite RingGlow => _ringGlow;
        /// <summary>실 고리 (Art/FX/fx_ring_thread)</summary>
        public Sprite RingThread => _ringThread;
        /// <summary>빛살 (Art/FX/rays — 보스 처치·불 밝히기)</summary>
        public Sprite Rays => _rays;
        /// <summary>별 폭발 (Art/VFX/vfx_star_burst — 치명타·영웅 그림의 대체)</summary>
        public Sprite StarBurst => _starBurst;
        /// <summary>광택 훑기 (Art/VFX/vfx_gloss_shine — 치명타 숫자·돔)</summary>
        public Sprite GlossShine => _glossShine;

        Sprite Find(string lineageId, int which)
        {
            if (_lineages == null || string.IsNullOrEmpty(lineageId)) return null;
            for (int i = 0; i < _lineages.Length; i++)
            {
                if (!string.Equals(_lineages[i].lineageId, lineageId, StringComparison.Ordinal)) continue;
                return which switch
                {
                    0 => _lineages[i].impact,
                    1 => _lineages[i].crit,
                    _ => _lineages[i].muzzle,
                };
            }
            return null;
        }

        static Sprite Pick(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
