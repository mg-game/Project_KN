using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Q-05 도감 (SPEC4 §6.2 · 부록 A.2): 처음 찾은 보상(별조각), 영구 모음 보너스, 못 찾은 칸 힌트, 적 문구.
    ///   · 처음 찾은 보상: 형태 기본 20 · 1차 30 · 2차 50 / 의상 30 / 적 10 / 시너지 처음 20 · 최고 단계 30 (조정 #5) / 재료 5.
    ///     평생 합계 = 형태 900 + 의상 300(시즌 의상이 생기면 330) + 적 90 + 시너지 1,250 + 재료 45 = 2,585 (2,615) ≤ 2,700 (§11.2).
    ///   · 모음 보너스 (도감이 열린 뒤 저절로, 단계의 합계 값): 형태 5/10/15/20/25/29 → 친구 공격력·체력 +1/+3/+5/+7/+8/+10% ·
    ///     적 9종 모두 → 수선점 최대 내구도 +5% · 켜 본 시너지 10/20/25종 → 불빛 충전 +5/+10/+15%.
    ///   · 힌트 키 = CodexSystem.Key(종류, ID). 형태 · 의상 · 적 · 시너지 · 재료 힌트는 콘텐츠 목록(content)으로 만들고,
    ///     content가 없으면(시험) 이야기 힌트만 둔다. 적 문구는 적 ID가 정해져 있어 늘 둔다.
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>동화책이 있는 장 수 (A.5 — 이야기 칸 story:1..5)</summary>
        internal const int StoryChapterCount = 5;

        static partial void FillCodex(MetaDatabase db, GameContent content)
        {
            var codex = db.codex = new CodexConfig
            {
                formBaseGem = 20,
                formTier1Gem = 30,
                formTier2Gem = 50,
                costumeGem = 30,
                enemyGem = 10,
                synergyFirstGem = 20,
                synergyMaxGem = 30,
                materialGem = 5,
                formTiers = new List<CodexBonusTier>
                {
                    Bonus(5, 1), Bonus(10, 3), Bonus(15, 5), Bonus(20, 7), Bonus(25, 8), Bonus(29, 10),
                },
                allEnemiesShopHpPercent = 5,
                synergyTiers = new List<CodexBonusTier> { Bonus(10, 5), Bonus(20, 10), Bonus(25, 15) },
            };

            // ── 적 도감 문구 (A.2, 찾은 뒤 툴팁) ──
            var texts = codex.enemyTexts;
            texts.Add(EnemyText("enemy_dust", "잊힌 방구석에서 뭉친 먼지예요. 수가 많지만 약해요."));
            texts.Add(EnemyText("enemy_moth", "천을 갉아 먹어요. 천으로 된 친구가 맞으면 아파요."));
            texts.Add(EnemyText("enemy_tangle", "쓰러지면 작은 실뭉치 둘로 갈라져요."));
            texts.Add(EnemyText("enemy_rust", "느리지만 단단해요. 맞으면 녹이 묻어 약해져요."));
            texts.Add(EnemyText("enemy_thimble", "보호막을 두르고 와요. 저격으로 먼저 깨 주세요."));
            texts.Add(EnemyText("enemy_wisp", "보이지 않다가 빛에 드러나요."));
            texts.Add(EnemyText("enemy_windup", "뒤에서 다른 적의 태엽을 감아 빠르게 만들어요."));
            texts.Add(EnemyText("boss_shadow", "화가 나면 빨라지고 불빛을 빼앗아요."));
            texts.Add(EnemyText("boss_mothqueen", "좀나방을 불러내요. 화가 나면 더 사나워져요."));

            // ── 못 찾은 칸 힌트 (A.2) ──
            var hints = codex.hints;
            var characters = content?.Characters;
            if (characters?.lineages != null)
            {
                foreach (var lineage in characters.lineages)
                {
                    if (lineage == null) continue;
                    foreach (var form in lineage.forms)
                    {
                        if (form == null || string.IsNullOrEmpty(form.id)) continue;
                        hints.Add(Hint(CodexCategory.Friend, form.id,
                            form.tier == 0 ? "잃어버린 친구 찾기에서 만날 수 있어요" : "진화 기억도에서 단서를 찾아요"));
                    }
                    foreach (var costume in lineage.costumes)
                    {
                        if (costume == null || string.IsNullOrEmpty(costume.id) || string.IsNullOrEmpty(costume.acquisition)) continue;
                        hints.Add(Hint(CodexCategory.Costume, costume.id, costume.acquisition));
                    }
                }
            }

            var enemies = content?.Battle?.enemies;
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy == null || string.IsNullOrEmpty(enemy.id)) continue;
                    hints.Add(Hint(CodexCategory.Enemy, enemy.id, $"스테이지 {StageText(System.Math.Max(1, enemy.minStage))}부터 나타나요"));
                }
            }

            var synergies = content?.Synergies?.synergies;
            if (synergies != null)
            {
                foreach (var synergy in synergies)
                {
                    string text = SynergyHint(synergy, characters);
                    if (text != null) hints.Add(Hint(CodexCategory.Synergy, synergy.id, text));
                }
            }

            var items = content?.Items?.items;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrEmpty(item.id) || item.category != ItemCategory.Material) continue;
                    var source = item.sources != null && item.sources.Count > 0 ? item.sources[0] : null;
                    if (source == null || string.IsNullOrEmpty(source.label)) continue;
                    hints.Add(Hint(CodexCategory.Material, item.id, $"{source.label}에서 모아요"));
                }
            }

            for (int chapter = 1; chapter <= StoryChapterCount; chapter++)
            {
                hints.Add(Hint(CodexCategory.Story, chapter.ToString(), $"{chapter}장 보스를 물리치면 열려요"));
            }
        }

        /// <summary>
        /// 시너지 힌트: 조합 '{구성원1}·{구성원2} 함께 세우면 켜져요', 특성 · 성향 '{특성} 친구 {n}명이 함께 서면 켜져요' (n = 첫 단계 필요 수)
        /// </summary>
        static string SynergyHint(SynergyDef synergy, CharacterDatabase characters)
        {
            if (synergy == null || string.IsNullOrEmpty(synergy.id)) return null;
            int need = synergy.tiers != null && synergy.tiers.Count > 0 && synergy.tiers[0] != null ? System.Math.Max(1, synergy.tiers[0].count) : 2;
            switch (synergy.kind)
            {
                case SynergyKind.Combo:
                    var names = new List<string>();
                    if (synergy.lineageIds != null)
                    {
                        foreach (var id in synergy.lineageIds)
                        {
                            var lineage = characters?.GetLineage(id);
                            if (lineage != null && !string.IsNullOrEmpty(lineage.name)) names.Add(lineage.name);
                        }
                    }
                    return names.Count > 0 ? $"{SynergyText.ComboGroup(names)} 함께 세우면 켜져요" : null;
                case SynergyKind.Trait:
                    return $"{SynergyText.TraitName(synergy.trait)} 친구 {need}명이 함께 서면 켜져요";
                case SynergyKind.Style:
                    return $"{SynergyText.StyleName(synergy.style)} 친구 {need}명이 함께 서면 켜져요";
                default:
                    return null;
            }
        }

        static CodexBonusTier Bonus(int count, float percent) => new() { count = count, percent = percent };

        static CodexEnemyText EnemyText(string enemyId, string text) => new() { enemyId = enemyId, text = text };

        static CodexHintDef Hint(CodexCategory category, string id, string hint) => new() { key = CodexSystem.Key(category, id), hint = hint };
    }
}
