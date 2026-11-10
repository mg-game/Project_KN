using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면이 쓰는 스킬/시너지/기믹 그림 묶음. 씬 빌더가 Art/VFX, Art/Icons, Art/Synergy에서 읽어 굽고,
    /// 아트가 없는 키는 절차적으로 그린 흰색 모양(VfxMotif)으로 채운다 (isArt = false → 코드가 색을 입힌다).
    /// 계보별 기본 공격(투사체·명중), 적 종류별 수선점 공격·처치 그림도 여기 있다 (없으면 null → 흰색 빛 입자로 대체).
    /// 런타임에는 ArtLib(에디터 전용)을 쓸 수 없으므로 이 컴포넌트를 통해서만 조회한다. 비어 있어도 예외 없이 null을 돌려준다.
    /// </summary>
    public class SkillVfxLibrary : MonoBehaviour
    {
        [Serializable]
        public struct VfxEntry
        {
            public string key;
            public Sprite sprite;
            [Tooltip("true = 색이 있는 아트 (흰색으로 그린다), false = 흰색 대체 모양 (주색을 입힌다)")]
            public bool isArt;
        }

        [Serializable]
        public struct IconEntry
        {
            public string id;
            public Sprite sprite;
        }

        [Tooltip("SkillVfx 키별 연출 그림")]
        [SerializeField] VfxEntry[] _vfx;
        [Tooltip("VfxMotif 순서의 흰색 모양")]
        [SerializeField] Sprite[] _motifs;
        [Tooltip("TraitType 순서")]
        [SerializeField] Sprite[] _traitIcons;
        [Tooltip("CombatStyle 순서")]
        [SerializeField] Sprite[] _styleIcons;
        [Tooltip("StageModifierType 순서 (None 칸은 비움)")]
        [SerializeField] Sprite[] _stageIcons;
        [Tooltip("StageModifierType 순서의 화면 덮개 (Art/VFX/overlay_*)")]
        [SerializeField] Sprite[] _overlays;
        [Tooltip("조합 시너지 아이콘 (Art/Synergy/{id})")]
        [SerializeField] IconEntry[] _synergyIcons;
        [Tooltip("기본 공격·적 공격·처치 그림 (Art/VFX/atk_*, enemy_atk_*, enemy_death_*, enemy_hit_*). id = 파일 이름")]
        [SerializeField] IconEntry[] _attackArt;
        [Tooltip("스킬 아이콘 (Art/Icons/skill_{키에서 vfx_를 뺀 이름}, 배지 크기용). id = SkillVfx 키")]
        [SerializeField] IconEntry[] _skillIcons;

        /// <summary>근접 베기 (돌격 지킴이 / 잔향이 기본 공격)</summary>
        public const string SlashId = "atk_slash";
        /// <summary>저격 지킴이 기본 공격의 긴 빛 꼬리 (가로로 긴 그림)</summary>
        public const string SnipeTrailId = "atk_snipe_trail";
        /// <summary>적이 수선점을 들이받을 때 문에서 튀는 나뭇조각</summary>
        public const string ShopHitId = "enemy_hit_shop";

        Dictionary<string, int> _vfxIndex;
        Dictionary<string, Sprite> _synergyIndex;
        Dictionary<string, Sprite> _attackIndex;
        Dictionary<string, Sprite> _skillIconIndex;
        // 계보/적 ID → 그림 (문자열 이어 붙이기는 ID마다 처음 한 번만)
        readonly Dictionary<string, Sprite> _shotCache = new(StringComparer.Ordinal);
        readonly Dictionary<string, Sprite> _hitCache = new(StringComparer.Ordinal);
        readonly Dictionary<string, Sprite> _enemyAttackCache = new(StringComparer.Ordinal);
        readonly Dictionary<string, Sprite> _deathCache = new(StringComparer.Ordinal);

        void Awake() => EnsureIndex();

        void EnsureIndex()
        {
            if (_vfxIndex != null) return;
            _vfxIndex = new Dictionary<string, int>(_vfx != null ? _vfx.Length : 0);
            for (int i = 0; _vfx != null && i < _vfx.Length; i++)
            {
                if (!string.IsNullOrEmpty(_vfx[i].key)) _vfxIndex[_vfx[i].key] = i;
            }
            _synergyIndex = new Dictionary<string, Sprite>(_synergyIcons != null ? _synergyIcons.Length : 0);
            for (int i = 0; _synergyIcons != null && i < _synergyIcons.Length; i++)
            {
                if (!string.IsNullOrEmpty(_synergyIcons[i].id) && _synergyIcons[i].sprite != null) _synergyIndex[_synergyIcons[i].id] = _synergyIcons[i].sprite;
            }
            _attackIndex = new Dictionary<string, Sprite>(_attackArt != null ? _attackArt.Length : 0, StringComparer.Ordinal);
            for (int i = 0; _attackArt != null && i < _attackArt.Length; i++)
            {
                if (!string.IsNullOrEmpty(_attackArt[i].id) && _attackArt[i].sprite != null) _attackIndex[_attackArt[i].id] = _attackArt[i].sprite;
            }
            _skillIconIndex = new Dictionary<string, Sprite>(_skillIcons != null ? _skillIcons.Length : 0, StringComparer.Ordinal);
            for (int i = 0; _skillIcons != null && i < _skillIcons.Length; i++)
            {
                if (!string.IsNullOrEmpty(_skillIcons[i].id) && _skillIcons[i].sprite != null) _skillIconIndex[_skillIcons[i].id] = _skillIcons[i].sprite;
            }
        }

        /// <summary>
        /// 배지 크기(22~42px)용 스킬 아이콘: Art/Icons/skill_* (색 있는 아트, isArt = true).
        /// 아이콘이 없으면 연출 그림(TryGetVfx)으로 대신한다 — 512px 연출 그림은 작게 줄이면 뭉개지므로 아이콘을 먼저 쓴다.
        /// </summary>
        public bool TryGetSkillIcon(string key, out Sprite sprite, out bool isArt)
        {
            EnsureIndex();
            if (key != null && _skillIconIndex.TryGetValue(key, out sprite))
            {
                isArt = true;
                return true;
            }
            return TryGetVfx(key, out sprite, out isArt);
        }

        /// <summary>
        /// 연출 키의 그림. 키를 모르면 false (sprite = null). 아트가 없어 흰색 대체 모양을 쓰면(빌더가 구운 대체 모양 · 빈 칸 · 모르는 키)
        /// 에디터 · 개발 빌드에서 키마다 한 번 "[SkillVfxLibrary] fallback {키}"를 남긴다 (ATK-07 5단계 빈 VFX 증명)
        /// </summary>
        public bool TryGetVfx(string key, out Sprite sprite, out bool isArt)
        {
            EnsureIndex();
            if (key != null && _vfxIndex.TryGetValue(key, out int index))
            {
                sprite = _vfx[index].sprite;
                isArt = _vfx[index].isArt && sprite != null;
                if (sprite != null)
                {
                    if (!isArt) NoteFallback(key);
                    return true;
                }
            }
            sprite = Motif(SkillVfxLook.Get(key).Fallback);
            isArt = false;
            NoteFallback(key);
            return sprite != null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>이미 알린 대체 키 (처음 빠졌을 때 만든다 — 아트를 찾은 길에는 할당 없음)</summary>
        HashSet<string> _fallbackLogged;
#endif

        /// <summary>대체 모양을 쓴 연출 키를 키마다 한 번 알린다 (에디터 · 개발 빌드만, 출시 빌드에서는 빈 함수). 같은 키 두 번째부터 할당 없음</summary>
        void NoteFallback(string key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (string.IsNullOrEmpty(key)) return;
            _fallbackLogged ??= new HashSet<string>(StringComparer.Ordinal);
            if (_fallbackLogged.Add(key)) Debug.Log("[SkillVfxLibrary] fallback " + key);
#endif
        }

        /// <summary>연출 키의 그림 (아트 또는 대체 모양, 없으면 null)</summary>
        public Sprite Vfx(string key) => TryGetVfx(key, out var sprite, out _) ? sprite : null;

        public bool IsArt(string key) => TryGetVfx(key, out _, out bool isArt) && isArt;

        public Sprite Motif(VfxMotif motif) => Pick(_motifs, (int)motif);

        public Sprite TraitIcon(TraitType trait) => Pick(_traitIcons, (int)trait);

        public Sprite StyleIcon(CombatStyle style) => Pick(_styleIcons, (int)style);

        public Sprite StageIcon(StageModifierType modifier) => Pick(_stageIcons, (int)modifier);

        public Sprite Overlay(StageModifierType modifier) => Pick(_overlays, (int)modifier);

        /// <summary>시너지 아이콘: 데이터에 넣은 아이콘 → Art/Synergy/{id} → 특성/역할 아이콘 순. 없으면 null</summary>
        public Sprite SynergyIcon(SynergyDef def)
        {
            if (def == null) return null;
            if (def.icon != null) return def.icon;
            EnsureIndex();
            if (!string.IsNullOrEmpty(def.id) && _synergyIndex.TryGetValue(def.id, out var sprite)) return sprite;
            return def.kind switch
            {
                SynergyKind.Trait => TraitIcon(def.trait),
                SynergyKind.Style => StyleIcon(def.style),
                _ => null,
            };
        }

        // ───────────── 기본 공격 / 적 공격 / 처치 그림 (모두 색 있는 아트, 없으면 null → 흰색 빛 입자로 대체) ─────────────

        /// <summary>공격 그림 하나 (id = Art/VFX 파일 이름)</summary>
        public Sprite AttackArt(string id)
        {
            EnsureIndex();
            return id != null && _attackIndex.TryGetValue(id, out var sprite) ? sprite : null;
        }

        /// <summary>지킴이 기본 공격 투사체 (atk_{계보}_shot, 오른쪽을 향한 그림)</summary>
        public Sprite LineageShot(string lineageId) => Cached(_shotCache, lineageId, "atk_", "_shot");

        /// <summary>계보 기본 공격이 닿는 순간 (atk_{계보}_hit)</summary>
        public Sprite LineageHit(string lineageId) => Cached(_hitCache, lineageId, "atk_", "_hit");

        /// <summary>적이 수선점·잔향이를 공격하는 그림 (enemy_atk_{dust} / enemy_atk_boss_shadow)</summary>
        public Sprite EnemyAttack(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;
            EnsureIndex();
            if (_enemyAttackCache.TryGetValue(enemyId, out var sprite)) return sprite;
            var look = EnemyFxCatalog.Look(enemyId);
            string kind = enemyId.StartsWith(EnemyPrefix, StringComparison.Ordinal) ? enemyId.Substring(EnemyPrefix.Length) : enemyId;
            sprite = AttackArt(look.ArtKey ?? "enemy_atk_" + kind);
            _enemyAttackCache[enemyId] = sprite;
            return sprite;
        }

        /// <summary>적이 쓰러질 때 흩어지는 그림 (enemy_death_{dust|moth|metal|shadow}, 갈래는 EnemyFxCatalog.Family)</summary>
        public Sprite EnemyDeath(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return EnemyDeath(EnemyDeathFamily.Dust);
            EnsureIndex();
            if (_deathCache.TryGetValue(enemyId, out var sprite)) return sprite;
            sprite = EnemyDeath(EnemyFxCatalog.Family(enemyId));
            _deathCache[enemyId] = sprite;
            return sprite;
        }

        /// <summary>처치 갈래의 그림 (없으면 null)</summary>
        public Sprite EnemyDeath(EnemyDeathFamily family) => AttackArt(EnemyFxCatalog.DeathArtKey(family));

        /// <summary>
        /// 적 종류 → 처치 그림 갈래 이름: 좀나방류(moth), 녹·골무·태엽 같은 쇠붙이(metal), 망각의 그림자류(shadow), 나머지 먼지·실뭉치(dust)
        /// </summary>
        public static string DeathKind(string enemyId) => EnemyFxCatalog.FamilyKey(EnemyFxCatalog.Family(enemyId));

        const string EnemyPrefix = "enemy_";

        Sprite Cached(Dictionary<string, Sprite> cache, string id, string prefix, string suffix)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            if (cache.TryGetValue(id, out var sprite)) return sprite;
            sprite = AttackArt(prefix + id + suffix);
            cache[id] = sprite;
            return sprite;
        }

        static Sprite Pick(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
