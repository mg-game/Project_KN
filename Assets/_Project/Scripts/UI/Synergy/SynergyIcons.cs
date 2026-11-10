using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 아이콘 찾기. 씬 빌더가 SynergyNavigator에 연결한 스프라이트(ArtLib.Trait/Style/Synergy/StageModifier)를
    /// 등록해 두고, 프리팹 뷰(시너지 행, 도감 행, 편성 카드 특성 칩)가 여기서 꺼내 쓴다.
    /// 아트가 없으면 아이콘 이미지를 끄고 글자(첫 글자)만 보인다 (자리표시 스프라이트를 연결했으면 그것을 강조색으로).
    /// 고리 안 아이콘: 특성/조합 아이콘은 테두리까지 그려진 메달 그림이라 홈을 숨기고,
    /// 역할 아이콘 · 글자 자리표시는 어두운 메달 홈(ui_medallion_dark, Socket) 안에 넣는다.
    /// </summary>
    public static class SynergyIcons
    {
        static Sprite[] _traits = System.Array.Empty<Sprite>();
        static Sprite[] _styles = System.Array.Empty<Sprite>();
        static Sprite[] _stages = System.Array.Empty<Sprite>();
        static Sprite[] _combos = System.Array.Empty<Sprite>();
        static Sprite _placeholder;
        static Sprite _socket;
        static Sprite _plate;

        /// <param name="traits">TraitType 순서</param>
        /// <param name="styles">CombatStyle 순서</param>
        /// <param name="stages">StageModifierType 순서</param>
        /// <param name="combos">조합 아이콘 (스프라이트 이름 = 시너지 ID)</param>
        /// <param name="placeholder">아트가 없을 때 강조색으로 칠할 자리표시 (null이면 아이콘을 끄고 글자만)</param>
        /// <param name="socket">메달 홈 (ui_medallion_dark) — 역할 아이콘 · 글자 자리표시 뒤</param>
        /// <param name="plate">밝은 둥근 받침 (ui_plate_round) — 계보 공명 줄의 초상화 뒤</param>
        public static void Register(Sprite[] traits, Sprite[] styles, Sprite[] stages, Sprite[] combos, Sprite placeholder,
            Sprite socket = null, Sprite plate = null)
        {
            _traits = traits ?? System.Array.Empty<Sprite>();
            _styles = styles ?? System.Array.Empty<Sprite>();
            _stages = stages ?? System.Array.Empty<Sprite>();
            _combos = combos ?? System.Array.Empty<Sprite>();
            _placeholder = placeholder;
            _socket = socket;
            _plate = plate;
        }

        public static Sprite Placeholder => _placeholder;

        /// <summary>메달 홈 (ui_medallion_dark). 없으면 null</summary>
        public static Sprite Socket => _socket;

        /// <summary>밝은 둥근 받침 (ui_plate_round). 없으면 null</summary>
        public static Sprite Plate => _plate;

        public static Sprite Trait(TraitType trait) => At(_traits, (int)trait);

        public static Sprite Style(CombatStyle style) => At(_styles, (int)style);

        public static Sprite Stage(StageModifierType modifier) => At(_stages, (int)modifier);

        /// <summary>시너지 아이콘: 데이터에 넣은 icon → 특성/역할/조합 아트 → null</summary>
        public static Sprite Of(SynergyDef def)
        {
            if (def == null) return null;
            if (def.icon != null) return def.icon;
            switch (def.kind)
            {
                case SynergyKind.Trait: return Trait(def.trait);
                case SynergyKind.Style: return Style(def.style);
                default:
                    foreach (var sprite in _combos)
                    {
                        if (sprite != null && sprite.name == def.id) return sprite;
                    }
                    return null;
            }
        }

        /// <summary>
        /// 아이콘을 입힌다. 아트가 없으면 글자를 보이고, 자리표시가 있으면 그것을 강조색으로, 없으면 이미지를 끈다
        /// (흰 네모가 보이지 않게 — 글자는 이미지의 자식이라 이미지 컴포넌트만 끈다).
        /// </summary>
        public static void Apply(Image image, TMP_Text fallback, Sprite sprite, Color accent, string initial)
        {
            if (image == null) return;
            bool hasArt = sprite != null;
            bool usePlaceholder = !hasArt && _placeholder != null;
            image.sprite = hasArt ? sprite : _placeholder;
            image.preserveAspect = true;
            image.color = hasArt ? Color.white : accent;
            image.enabled = hasArt || usePlaceholder;
            if (fallback == null) return;
            fallback.gameObject.SetActive(!hasArt);
            fallback.text = initial;
        }

        public static void Apply(Image image, TMP_Text fallback, SynergyDef def) =>
            Apply(image, fallback, Of(def), SynergyUI.AccentColor(def), SynergyUI.Initial(def));

        /// <summary>
        /// 고리(메달 홈) 안의 시너지 아이콘. 특성/조합 아이콘은 테두리까지 그려진 메달 그림이라 홈(부모 이미지)을 숨기고
        /// 고리 자리를 덮도록 cover만큼 키운다. 역할 아이콘과 글자 자리표시는 어두운 메달 홈(Socket) 안쪽 inset만큼 들여 넣는다.
        /// 아이콘은 홈에 꽉 차게 늘인 자식이어야 한다. 홈의 색(알파)은 부르는 쪽이 정한다.
        /// </summary>
        public static void ApplyInRing(Image image, TMP_Text fallback, SynergyDef def, float inset, float cover)
        {
            Apply(image, fallback, def);
            if (image == null) return;
            bool medallion = HasArt(image) && def != null && def.kind != SynergyKind.Style;
            var ring = image.transform.parent != null ? image.transform.parent.GetComponent<Image>() : null;
            if (ring != null)
            {
                if (_socket != null) ring.sprite = _socket;
                ring.preserveAspect = true;
                ring.enabled = !medallion;
            }
            float pad = medallion ? -cover : inset;
            var rt = image.rectTransform;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        /// <summary>아트가 있는 아이콘인지 (자리표시 원이 아닌지)</summary>
        public static bool HasArt(Image image) => image != null && image.enabled && image.sprite != null && image.sprite != _placeholder;

        public static void ApplyStage(Image image, TMP_Text fallback, StageModifierType modifier)
        {
            var name = UITheme.StageModifierName(modifier);
            Apply(image, fallback, Stage(modifier), UITheme.StageModifierColor(modifier), string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1));
        }

        static Sprite At(Sprite[] sprites, int index) => index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
