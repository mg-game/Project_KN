using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 캐릭터 콘텐츠 원본: 계보 7종(특성 2개씩) / 형태 29종(고유 스킬 1개씩) / 의상 10종.
    /// 계보별 데이터는 CharacterContent.Jjakjjak.cs(짝짝이) / CharacterContent.Lineages.cs(나머지 6종)에 있다.
    /// 형태 능력치는 FormStats 표로 계산하고, 스킬은 SkillContent 표에서 붙이며,
    /// 의상 획득 안내 문구는 해금 조건에서 만들어 숫자가 어긋나지 않게 한다.
    ///
    /// 성향 분포 (29종): 방어 7 · 힐 5 · 지원 6 · 돌격 4 · 포획 3 · 저격 4.
    /// 시작 친구는 짝짝이·똑단이(방어) + 찰칵이(저격)이고, 몽당이(힐)가 잔향이로 수선점을 돌본다.
    /// </summary>
    internal static partial class CharacterContent
    {
        /// <summary>단서 발견 레벨: 부모 형태 해금 + 계보 레벨이 이 값 이상이면 레시피가 보인다</summary>
        const int Tier1ClueLevel = 5;
        const int Tier2ClueLevel = 25;

        /// <summary>스테이지 표기 "챕터-번호"의 챕터당 스테이지 수</summary>
        const int StagesPerChapter = 10;

        /// <summary>사용자 컨셉 시트가 있는 의상 (Art/Concept/{costumeId}.png)</summary>
        public static readonly IReadOnlyCollection<string> ConceptSheetCostumes = new HashSet<string>
        {
            "jjak_caterpillar", "jjak_nightkeeper", "jjak_plush", "jjak_puppet", "jjak_sleepcap", "jjak_threadweb",
        };

        public static List<LineageDef> Build()
        {
            var lineages = new List<LineageDef>
            {
                Jjakjjak(),
                Ttokdan(),
                Chalkak(),
                Mongdang(),
                Udukeoni(),
                Rouge(),
                Lalabell(),
            };
            AttachSkills(lineages);
            return lineages;
        }

        /// <summary>형태마다 고유 스킬(SkillContent)을 붙인다. 표에 없는 형태는 빈 스킬로 두고 ContentValidator가 오류로 알린다.</summary>
        static void AttachSkills(List<LineageDef> lineages)
        {
            var skills = SkillContent.Build();
            foreach (var lineage in lineages)
            {
                foreach (var form in lineage.forms)
                {
                    form.skill = skills.TryGetValue(form.id, out var skill) ? skill : new SkillDef();
                }
            }
        }

        // ───────────────────────── 계보 ─────────────────────────

        /// <param name="traits">계보 특성 2개 (특성 시너지에 쓰인다)</param>
        /// <param name="palette">"메인 서브 포인트 중립" 16진 색 4개를 공백으로 구분</param>
        static LineageDef Lineage(string id, string name, string species, string origin, ItemGrade grade, BodyType body,
            TraitType[] traits, string palette, string personality, CharacterFormDef[] forms, CostumeDef[] costumes = null)
        {
            var colors = palette.Split(' ');
            var lineage = new LineageDef
            {
                id = id,
                name = name,
                species = species,
                origin = origin,
                personality = personality,
                grade = grade,
                bodyType = body,
                traits = new List<TraitType>(traits),
                mainColor = Hex(colors[0]),
                subColor = Hex(colors[1]),
                accentColor = Hex(colors[2]),
                neutralColor = Hex(colors[3]),
                forms = new List<CharacterFormDef>(forms),
                costumes = new List<CostumeDef>(costumes ?? new CostumeDef[0]),
            };

            foreach (var form in lineage.forms)
            {
                FormStats.Apply(form, grade);
                if (form.tier == 0 && string.IsNullOrEmpty(lineage.baseFormId)) lineage.baseFormId = form.id;
            }
            foreach (var costume in lineage.costumes)
            {
                costume.acquisition = DescribeUnlock(costume, lineage);
            }
            return lineage;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        // ───────────────────────── 형태 ─────────────────────────

        static CharacterFormDef Base(string id, string name, CombatStyle style, CharacterRole role, string description) =>
            new()
            {
                id = id,
                name = name,
                parentFormId = "",
                tier = 0,
                style = style,
                recommendedRole = role,
                description = description,
                clueLevel = 0,
            };

        /// <summary>진화형. 레시피는 .Needs(...).When(...)으로 붙인다.</summary>
        static CharacterFormDef Evolve(string id, string name, int tier, string parentFormId, CombatStyle style, CharacterRole role,
            string description) =>
            new()
            {
                id = id,
                name = name,
                parentFormId = parentFormId,
                tier = tier,
                style = style,
                recommendedRole = role,
                description = description,
                clueLevel = tier >= 2 ? Tier2ClueLevel : Tier1ClueLevel,
            };

        /// <summary>진화 제작에 소모하는 재료</summary>
        static CharacterFormDef Needs(this CharacterFormDef form, params (string itemId, int count)[] materials)
        {
            form.materials = new List<MaterialCost>();
            foreach (var (itemId, count) in materials)
            {
                form.materials.Add(new MaterialCost { itemId = itemId, count = count });
            }
            return form;
        }

        /// <summary>진화 조건 (모두 달성해야 제작 가능)</summary>
        static CharacterFormDef When(this CharacterFormDef form, params (ProgressType type, int target)[] conditions)
        {
            form.conditions = new List<EvolutionCondition>();
            foreach (var (type, target) in conditions)
            {
                form.conditions.Add(new EvolutionCondition { type = type, target = target });
            }
            return form;
        }

        // ───────────────────────── 의상 ─────────────────────────

        /// <param name="rarity">보유 효과(계보 공격력 %)를 정하는 의상 희귀도</param>
        static CostumeDef Costume(string id, string name, string emotion, ItemGrade rarity, Unlock unlock, string description) =>
            new()
            {
                id = id,
                name = name,
                emotion = emotion,
                description = description,
                unlockType = unlock.Type,
                unlockTarget = unlock.Target,
                unlockTargetId = unlock.TargetId ?? "",
                price = unlock.Price,
                attackBonusPercent = CostumeAttackBonus(rarity),
            };

        /// <summary>의상 보유 효과: 희귀도별 계보 공격력 3~8%</summary>
        static float CostumeAttackBonus(ItemGrade rarity) => rarity switch
        {
            ItemGrade.Common => 3f,
            ItemGrade.Rare => 4f,
            ItemGrade.Epic => 5f,
            ItemGrade.Legendary => 6f,
            _ => 8f,
        };

        /// <summary>의상 해금 조건</summary>
        readonly struct Unlock
        {
            public readonly CostumeUnlockType Type;
            public readonly int Target;
            public readonly string TargetId;
            public readonly int Price;

            Unlock(CostumeUnlockType type, int target = 0, string targetId = null, int price = 0)
            {
                Type = type;
                Target = target;
                TargetId = targetId;
                Price = price;
            }

            /// <summary>수선집 야간 방문 (현지 20:00~05:59 접속)</summary>
            public static Unlock NightVisit => new(CostumeUnlockType.NightVisit);
            public static Unlock Stage(int stage) => new(CostumeUnlockType.StageReached, stage);
            public static Unlock Expeditions(int count) => new(CostumeUnlockType.ExpeditionCount, count);
            public static Unlock Resonance(int level) => new(CostumeUnlockType.Resonance, level);
            /// <summary>'별이 머문 밤 산책' 이벤트 상점 (별빛 사탕)</summary>
            public static Unlock EventShop(int price) => new(CostumeUnlockType.EventShop, price: price);
            public static Unlock Form(string formId) => new(CostumeUnlockType.FormUnlocked, targetId: formId);
            /// <summary>별빛 산책 수첩 30칸 보상 (Q-06 · 시즌 한정, MetaReward.Costume으로 받는다 — CostumeSystem.CanUnlock은 늘 false)</summary>
            public static Unlock Season => new(CostumeUnlockType.Season);
        }

        /// <summary>
        /// 도감 "획득 방법" 문구. 해금 조건 수치에서 만든다.
        /// 실제 해금 경로(CostumeSystem.CanUnlock / 상점 상품)와 다른 경로를 약속하지 않는다:
        /// NightVisit은 night_visit 플래그로만 열리고 이벤트 상점은 이 의상을 팔지 않으므로 이벤트 경로는 적지 않는다.
        /// </summary>
        static string DescribeUnlock(CostumeDef costume, LineageDef lineage) => costume.unlockType switch
        {
            CostumeUnlockType.Default => "처음부터 함께해요.",
            CostumeUnlockType.NightVisit => "수선집 야간 방문(밤 8시 ~ 새벽 6시 접속)으로 만나 보세요!",
            CostumeUnlockType.EventShop => $"'별이 머문 밤 산책' 이벤트 상점에서 별빛 사탕 {costume.price}개로 교환할 수 있어요.",
            CostumeUnlockType.StageReached => StageReachedText(costume.unlockTarget),
            CostumeUnlockType.ExpeditionCount => $"{lineage.name} 계보로 원정을 {costume.unlockTarget}회 완료하면 받을 수 있어요.",
            CostumeUnlockType.Resonance => $"{lineage.name} 계보 공명 Lv.{costume.unlockTarget} 달성 시 받을 수 있어요.",
            CostumeUnlockType.FormUnlocked => $"진화형 '{FormName(lineage, costume.unlockTargetId)}' 해금 시 함께 열려요.",
            CostumeUnlockType.Season => "'별빛 산책 수첩' 30칸을 모두 채우면 받을 수 있어요.",
            _ => "",
        };

        static string StageLabel(int stage) =>
            $"{(stage - 1) / StagesPerChapter + 1}-{(stage - 1) % StagesPerChapter + 1}";

        /// <summary>
        /// 스테이지 도달 의상 (unlockTarget = 도달 스테이지): 다른 잠금 문구와 같은 "클리어" 기준으로 쓴다 (M39).
        /// 도달 2-10 → "스테이지 2-9를 클리어하면 받을 수 있어요." 처음부터 도달해 있는 값(1 이하)이면 바로 받을 수 있다고 쓴다.
        /// </summary>
        static string StageReachedText(int reached) => reached <= 1
            ? "처음부터 받을 수 있어요."
            : $"스테이지 {MetaText.WithObject(StageLabel(reached - 1))} 클리어하면 받을 수 있어요.";

        static string FormName(LineageDef lineage, string formId) =>
            lineage.forms.Find(f => f.id == formId)?.name ?? formId;
    }
}
