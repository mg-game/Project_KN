using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 화면이 쓰는 작은 아이콘 묶음 (스킬 연출 키 → 아이콘, 특성 / 성향 / 시너지 / 역할 아이콘).
    /// ArtLib은 에디터 전용이라 씬 빌더(CharacterSceneBuilder)가 이 에셋을 채워 뷰에 연결한다.
    /// 아트가 없는 항목은 null → 뷰가 색 도형으로 대신한다.
    /// </summary>
    public class CharacterIconLibrary : ScriptableObject
    {
        [Tooltip("SkillVfx 키 (vfx_thread_stitch …)")]
        [SerializeField] string[] _vfxKeys = Array.Empty<string>();
        [SerializeField] Sprite[] _vfxSprites = Array.Empty<Sprite>();
        [Tooltip("TraitType 값 순서 (Fabric, Metal, Light, Sound, Night, Rain, Charm)")]
        [SerializeField] Sprite[] _traitIcons = Array.Empty<Sprite>();
        [Tooltip("CombatStyle 값 순서 (Defense, Assault, Capture, Snipe, Support, Heal)")]
        [SerializeField] Sprite[] _styleIcons = Array.Empty<Sprite>();
        [Tooltip("조합 시너지 ID → Art/Synergy/{id}.png")]
        [SerializeField] string[] _synergyIds = Array.Empty<string>();
        [SerializeField] Sprite[] _synergySprites = Array.Empty<Sprite>();
        [SerializeField] Sprite _guardianIcon;
        [SerializeField] Sprite _echoIcon;

        Dictionary<string, Sprite> _vfx;
        Dictionary<string, Sprite> _synergy;

        void OnEnable()
        {
            _vfx = null;
            _synergy = null;
        }

        /// <summary>스킬 연출 키의 아이콘 (없으면 null)</summary>
        public Sprite Vfx(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            _vfx ??= BuildMap(_vfxKeys, _vfxSprites);
            return _vfx.TryGetValue(key, out var sprite) ? sprite : null;
        }

        public Sprite Skill(SkillDef skill) => skill != null ? Vfx(skill.vfx) : null;

        public Sprite Trait(TraitType trait) => At(_traitIcons, (int)trait);

        public Sprite Style(CombatStyle style) => At(_styleIcons, (int)style);

        public Sprite Role(CharacterRole role) => role == CharacterRole.Guardian ? _guardianIcon : _echoIcon;

        /// <summary>시너지 아이콘: 데이터에 넣은 아이콘 → 특성/성향 아이콘 → 조합 아트 순서로 찾는다.</summary>
        public Sprite Synergy(SynergyDef def)
        {
            if (def == null) return null;
            if (def.icon != null) return def.icon;
            switch (def.kind)
            {
                case SynergyKind.Trait: return Trait(def.trait);
                case SynergyKind.Style: return Style(def.style);
            }
            if (string.IsNullOrEmpty(def.id)) return null;
            _synergy ??= BuildMap(_synergyIds, _synergySprites);
            return _synergy.TryGetValue(def.id, out var sprite) ? sprite : null;
        }

        static Sprite At(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;

        static Dictionary<string, Sprite> BuildMap(string[] keys, Sprite[] sprites)
        {
            var map = new Dictionary<string, Sprite>();
            if (keys == null || sprites == null) return map;
            int count = Mathf.Min(keys.Length, sprites.Length);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(keys[i]) && sprites[i] != null) map[keys[i]] = sprites[i];
            }
            return map;
        }
    }
}
