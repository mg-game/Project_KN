using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 아트 에셋 경로 규칙. 파일이 아직 없으면 null을 돌려주므로 호출하는 쪽은 자리표시로 대체한다.
    ///   Art/Characters/{formId}.png, Art/Costumes/{costumeId}.png, Art/Concept/{name}.png,
    ///   Art/Enemies/{enemyId}.png, Art/Items/{itemId}.png, Art/Icons/{name}.png,
    ///   Art/Battle/{name}.png, Art/UI/{name}.png (이름이 _9s로 끝나면 9-slice), Art/FX/{name}.png
    /// </summary>
    internal static class ArtLib
    {
        public const string Root = IdleProjectSetup.Root + "/Art";

        public static Sprite Load(string relativePath) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{relativePath}");

        public static Sprite Character(string formId) => Load($"Characters/{formId}.png");
        public static Sprite Costume(string costumeId) => Load($"Costumes/{costumeId}.png");
        public static Sprite Concept(string name) => Load($"Concept/{name}.png");
        public static Sprite Enemy(string enemyId) => Load($"Enemies/{enemyId}.png");
        public static Sprite Item(string itemId) => Load($"Items/{itemId}.png");
        public static Sprite Icon(string name) => Load($"Icons/{name}.png");
        public static Sprite Battle(string name) => Load($"Battle/{name}.png");
        public static Sprite UI(string name) => Load($"UI/{name}.png");
        /// <summary>스킬/기믹 연출 (SkillVfx 키, 예: vfx_candle_burst, overlay_rain)</summary>
        public static Sprite Vfx(string key) => string.IsNullOrEmpty(key) ? null : Load($"VFX/{key}.png");
        /// <summary>특성 아이콘 Art/Icons/trait_{fabric}.png</summary>
        public static Sprite Trait(TraitType trait) => Icon($"trait_{trait.ToString().ToLowerInvariant()}");
        /// <summary>성향 아이콘 Art/Icons/style_{defense}.png</summary>
        public static Sprite Style(CombatStyle style) => Icon($"style_{style.ToString().ToLowerInvariant()}");
        /// <summary>스테이지 기믹 아이콘 Art/Icons/stage_{rain}.png</summary>
        public static Sprite StageModifier(StageModifierType modifier) => Icon($"stage_{modifier.ToString().ToLowerInvariant()}");
        /// <summary>조합 시너지 아이콘 Art/Synergy/{synergyId}.png</summary>
        public static Sprite Synergy(string synergyId) => Load($"Synergy/{synergyId}.png");

        /// <summary>연출용 흰색 소프트 스프라이트 (색은 Image.color로 입힌다)</summary>
        public static Sprite FX(string name) => Load($"FX/{name}.png");

        public static Sprite Currency(CurrencyType type) => Icon(type switch
        {
            CurrencyType.Gold => "currency_gold",
            CurrencyType.Gem => "currency_gem",
            _ => "currency_event",
        });
    }
}
