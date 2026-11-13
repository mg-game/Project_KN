using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>검증 결과. 오류 = 게임이 잘못 동작하는 데이터, 경고 = 아트 누락처럼 동작은 하지만 손봐야 하는 것.</summary>
    internal sealed class ContentReport
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();

        public void Error(string message) => Errors.Add(message);
        public void Warn(string message) => Warnings.Add(message);
    }

    /// <summary>
    /// 콘텐츠 에셋 사이의 참조 정합성 검사 (ID 중복, 진화 트리, 레시피 재료, 획득처, 상점 상품, 의상 해금, 가챠 등급, 아트 누락).
    /// 스킬 / 시너지 / 적 기믹 검사는 ContentValidator.Skills.cs에 있다.
    /// 에셋을 수정하지 않는다. 진입점은 IdleDataBuilder.Validate.
    /// </summary>
    internal static partial class ContentValidator
    {
        const string LogTag = "[콘텐츠 검증]";

        public static ContentReport Check(GameContent content)
        {
            var report = new ContentReport();
            if (content == null)
            {
                report.Error("GameContent가 없습니다.");
                return report;
            }

            var index = new ContentIndex(content);
            CheckItems(report, content, index);
            CheckCharacters(report, content, index);
            CheckFormSkills(report, content);
            CheckSynergies(report, content);
            CheckSkillIds(report, content);
            CheckSkillCoverage(report, content);
            CheckGacha(report, content);
            CheckExpeditions(report, content, index);
            CheckShops(report, content, index);
            CheckBattle(report, content.Battle);
            CheckConfig(report, content);
            CheckArt(report, content);
            CheckSkillArt(report, content);
            return report;
        }

        public static void Log(ContentReport report)
        {
            foreach (var error in report.Errors) Debug.LogError($"{LogTag} {error}");
            foreach (var warning in report.Warnings) Debug.LogWarning($"{LogTag} {warning}");
            string result = report.Errors.Count == 0 ? "통과" : "실패";
            Debug.Log($"{LogTag} {result}: 오류 {report.Errors.Count}개, 경고 {report.Warnings.Count}개");
        }

        // ───────────────────────── 아이템 ─────────────────────────

        static void CheckItems(ContentReport r, GameContent c, ContentIndex index)
        {
            if (c.Items == null)
            {
                r.Error("ItemDatabase 에셋이 없습니다.");
                return;
            }
            CheckUnique(r, "아이템", Safe(c.Items.items), i => i.id);

            foreach (var item in Safe(c.Items.items))
            {
                if (item == null) continue;
                string at = $"아이템 '{item.id}'";
                if (string.IsNullOrEmpty(item.name)) r.Error($"{at}: 이름이 비어 있습니다.");
                if (string.IsNullOrEmpty(item.description)) r.Warn($"{at}: 설명이 비어 있습니다.");
                if (item.category == ItemCategory.Material && Safe(item.sources).Count() == 0) r.Warn($"{at}: 획득처가 없습니다.");
                foreach (var source in Safe(item.sources))
                {
                    if (source != null) CheckSource(r, item, source, index, c.Battle);
                }
            }
        }

        /// <summary>
        /// 획득처 하나 (M17 드롭 표 감사): 가리키는 원정 구역·던전 층·상점·스테이지가 실제로 그 아이템을 주는지.
        /// 주지 않으면 "모으러 가기"가 빈손으로 보내므로 경고한다 (원정 = ExpeditionDatabase 드롭, 던전 = DungeonSystem 보상 층,
        /// 상점 = 상품 보상, 스테이지 = BattleSystem 스테이지 드롭).
        /// </summary>
        static void CheckSource(ContentReport r, ItemDef item, ItemSource source, ContentIndex index, BattleConfig battle)
        {
            string at = $"아이템 '{item.id}' 획득처";
            if (string.IsNullOrEmpty(source.label)) r.Warn($"{at}: 표시 문구(label)가 비어 있습니다.");

            switch (source.type)
            {
                case ItemSourceType.Stage:
                    if (!StageDrops.Contains(item.id))
                        r.Warn($"{at}: 스테이지 보상은 {string.Join("/", StageDrops)}만 주는데 이 아이템을 스테이지 획득처로 적었습니다.");
                    break;

                case ItemSourceType.Expedition:
                    if (!index.Zones.TryGetValue(source.targetId ?? "", out var zone))
                        r.Error($"{at}: ExpeditionDatabase에 없는 원정 구역 '{source.targetId}'");
                    else if (!Safe(zone.drops).Any(d => d != null && d.itemId == item.id))
                        r.Warn($"{at}: 원정 구역 '{zone.id}'의 드롭 목록에 이 아이템이 없습니다.");
                    break;

                case ItemSourceType.Dungeon:
                    if (!int.TryParse(source.targetId, out int floor) || floor < 1)
                        r.Error($"{at}: 던전 층 '{source.targetId}'은(는) 1 이상의 숫자여야 합니다.");
                    else if (battle != null)
                    {
                        int dropFloor = DungeonDropFloor(battle, item.id);
                        if (dropFloor < 0) r.Warn($"{at}: 잔향이 던전은 이 아이템을 주지 않습니다 (DungeonSystem 보상).");
                        else if (floor < dropFloor) r.Warn($"{at}: 던전 {floor}층으로 적었지만 이 아이템은 {dropFloor}층부터 나옵니다.");
                    }
                    break;

                case ItemSourceType.Shop:
                    if (!index.Shops.TryGetValue(source.targetId ?? "", out var shop))
                        r.Error($"{at}: ShopDatabase에 없는 상점 '{source.targetId}'");
                    else if (!Safe(shop.products).Any(p => p != null && Safe(p.rewards).Any(w => IsItem(w, item.id))))
                        r.Warn($"{at}: 상점 '{shop.id}'에 이 아이템을 주는 상품이 없습니다.");
                    break;
            }
        }

        static bool IsItem(RewardData reward, string itemId) => reward != null && reward.type == RewardType.Item && reward.itemId == itemId;

        /// <summary>스테이지 클리어가 확률로 주는 재료 (BattleSystem 스테이지 보상 — stageClothChance / stageRibbonChance)</summary>
        static readonly string[] StageDrops = { "mat_cloth", "mat_ribbon" };

        /// <summary>던전이 그 아이템을 주기 시작하는 층 (DungeonSystem: 기억의 실 / 오르골 태엽 / 달빛 조각). 주지 않으면 -1</summary>
        static int DungeonDropFloor(BattleConfig battle, string itemId) => itemId switch
        {
            DungeonSystem.ThreadItemId => battle.dungeonThreadFloor,
            DungeonSystem.SpringItemId => battle.dungeonSpringFloor,
            DungeonSystem.MoonlightItemId => battle.dungeonMoonlightFloor,
            _ => -1,
        };

        // ───────────────────────── 캐릭터 ─────────────────────────

        static void CheckCharacters(ContentReport r, GameContent c, ContentIndex index)
        {
            var db = c.Characters;
            if (db == null)
            {
                r.Error("CharacterDatabase 에셋이 없습니다.");
                return;
            }

            var lineages = Safe(db.lineages).ToList();
            if (lineages.Count == 0) r.Error("계보가 하나도 없습니다.");
            CheckUnique(r, "계보", lineages, l => l.id);
            CheckUnique(r, "형태", lineages.Where(l => l != null).SelectMany(l => Safe(l.forms)), f => f.id);
            CheckUnique(r, "의상", lineages.Where(l => l != null).SelectMany(l => Safe(l.costumes)), x => x.id);

            foreach (var lineage in lineages)
            {
                if (lineage != null) CheckLineage(r, lineage, index);
            }
        }

        static void CheckLineage(ContentReport r, LineageDef lineage, ContentIndex index)
        {
            string at = $"계보 '{lineage.id}'";
            if (string.IsNullOrEmpty(lineage.name)) r.Error($"{at}: 이름이 비어 있습니다.");
            if (string.IsNullOrEmpty(lineage.personality)) r.Warn($"{at}: 성격 설명이 비어 있습니다.");
            CheckTraits(r, at, lineage);

            var forms = Safe(lineage.forms).Where(f => f != null).ToList();
            if (forms.Count == 0)
            {
                r.Error($"{at}: 형태가 없습니다.");
                return;
            }

            var baseForm = forms.Find(f => f.id == lineage.baseFormId);
            if (baseForm == null)
                r.Error($"{at}: 계보에 없는 기본형 '{lineage.baseFormId}'");
            else if (baseForm.tier != 0 || !string.IsNullOrEmpty(baseForm.parentFormId))
                r.Error($"{at}: 기본형 '{baseForm.id}'은(는) tier 0이고 부모가 없어야 합니다.");

            foreach (var form in forms) CheckForm(r, lineage, forms, form, index);
            foreach (var costume in Safe(lineage.costumes))
            {
                if (costume != null) CheckCostume(r, lineage, forms, costume, index);
            }
        }

        static void CheckForm(ContentReport r, LineageDef lineage, List<CharacterFormDef> forms, CharacterFormDef form, ContentIndex index)
        {
            string at = $"형태 '{form.id}'";
            if (string.IsNullOrEmpty(form.name)) r.Error($"{at}: 이름이 비어 있습니다.");
            if (string.IsNullOrEmpty(form.description)) r.Warn($"{at}: 설명이 비어 있습니다.");
            CheckStats(r, at, form);

            if (form.tier <= 0)
            {
                if (form.id != lineage.baseFormId) r.Error($"{at}: tier 0 형태는 계보 기본형('{lineage.baseFormId}') 하나뿐이어야 합니다.");
                return;
            }

            var parent = forms.Find(f => f.id == form.parentFormId);
            if (parent == null)
                r.Error($"{at}: 같은 계보('{lineage.id}')에 없는 부모 형태 '{form.parentFormId}'");
            else if (parent.tier != form.tier - 1)
                r.Error($"{at}: tier {form.tier} 형태의 부모 '{parent.id}'은(는) tier {form.tier - 1}이어야 합니다 (현재 {parent.tier}).");

            if (form.clueLevel < 1) r.Warn($"{at}: 단서 발견 레벨(clueLevel)이 0입니다.");
            CheckRecipe(r, at, form, index);
        }

        static void CheckRecipe(ContentReport r, string at, CharacterFormDef form, ContentIndex index)
        {
            var materials = Safe(form.materials).ToList();
            if (materials.Count == 0) r.Warn($"{at}: 진화 재료가 없습니다.");
            foreach (var cost in materials)
            {
                if (cost == null || !index.Items.TryGetValue(cost.itemId ?? "", out var item))
                    r.Error($"{at}: ItemDatabase에 없는 레시피 재료 '{cost?.itemId}'");
                else if (cost.count <= 0)
                    r.Error($"{at}: 재료 '{cost.itemId}' 수량이 0 이하입니다.");
                else if (item.category != ItemCategory.Material)
                    r.Warn($"{at}: 레시피 재료 '{cost.itemId}'이(가) 재료(Material) 분류가 아닙니다.");
            }

            var conditions = Safe(form.conditions).ToList();
            if (conditions.Count == 0) r.Warn($"{at}: 진화 조건이 없습니다.");
            foreach (var condition in conditions)
            {
                if (condition != null && condition.target <= 0) r.Error($"{at}: 진화 조건 {condition.type}의 목표가 0 이하입니다.");
            }
        }

        static void CheckStats(ContentReport r, string at, CharacterFormDef f)
        {
            if (f.attack <= 0 || f.hp <= 0 || f.attackInterval <= 0 || f.range <= 0 || f.moveSpeed <= 0 || f.lifetime <= 0 || f.summonCost <= 0)
            {
                r.Error($"{at}: 능력치에 0 이하 값이 있습니다 (공격 {f.attack}, 체력 {f.hp}, 간격 {f.attackInterval}, 반경 {f.range}, " +
                        $"이동 {f.moveSpeed}, 유지 {f.lifetime}, 비용 {f.summonCost}).");
            }
            if (f.arcDegrees <= 0 || f.arcDegrees > 360) r.Error($"{at}: 부채꼴 각도 {f.arcDegrees}°는 0~360 사이여야 합니다.");
        }

        static void CheckCostume(ContentReport r, LineageDef lineage, List<CharacterFormDef> forms, CostumeDef costume, ContentIndex index)
        {
            string at = $"의상 '{costume.id}'";
            if (string.IsNullOrEmpty(costume.name)) r.Error($"{at}: 이름이 비어 있습니다.");
            if (string.IsNullOrEmpty(costume.description)) r.Warn($"{at}: 설명이 비어 있습니다.");
            if (string.IsNullOrEmpty(costume.acquisition)) r.Warn($"{at}: 획득 방법 문구가 비어 있습니다.");
            if (costume.attackBonusPercent < 0) r.Error($"{at}: 보유 효과(공격력 %)가 음수입니다.");

            switch (costume.unlockType)
            {
                case CostumeUnlockType.FormUnlocked:
                    if (!forms.Exists(f => f.id == costume.unlockTargetId))
                        r.Error($"{at}: 계보 '{lineage.id}'에 없는 해금 대상 형태 '{costume.unlockTargetId}'");
                    break;

                case CostumeUnlockType.EventShop:
                    if (costume.price <= 0) r.Error($"{at}: 이벤트 상점 가격이 0 이하입니다.");
                    if (!index.CostumeProducts.TryGetValue(costume.id, out var product))
                        r.Error($"{at}: 이벤트 상점 의상인데 이 의상을 파는 상품이 없습니다.");
                    else if (product.currency != CurrencyType.EventToken || Math.Abs(product.price - costume.price) > 0.001)
                        r.Warn($"{at}: 상품 '{product.id}' 가격({product.currency} {product.price})이 의상 가격(별빛 사탕 {costume.price})과 다릅니다.");
                    break;

                case CostumeUnlockType.StageReached:
                case CostumeUnlockType.ExpeditionCount:
                case CostumeUnlockType.Resonance:
                    if (costume.unlockTarget <= 0) r.Error($"{at}: 해금 목표({costume.unlockType})가 0 이하입니다.");
                    break;
            }
        }

        // ───────────────────────── 가챠 ─────────────────────────

        static void CheckGacha(ContentReport r, GameContent c)
        {
            var tables = c.GachaTables;
            if (tables == null || tables.Count == 0)
            {
                r.Error("가챠 테이블이 없습니다.");
                return;
            }
            CheckUnique(r, "가챠 테이블", tables, t => t.tableId);

            foreach (var table in tables)
            {
                if (table == null) continue;
                string at = $"가챠 '{table.tableId}'";
                if (table.singleCost <= 0 || table.tenCost <= 0) r.Error($"{at}: 소환 비용이 0 이하입니다.");

                var rates = Safe(table.rates).Where(x => x != null).ToList();
                if (rates.Sum(x => x.weight) <= 0) r.Error($"{at}: 등급 확률 합이 0입니다.");
                foreach (var rate in rates)
                {
                    if (rate.weight > 0 && !HasLineageOfGrade(c.Characters, rate.grade))
                        r.Error($"{at}: {rate.grade} 등급 확률({rate.weight})이 있지만 그 등급의 계보가 없습니다.");
                }
                if (table.pityCount > 0 && !HasLineageOfGrade(c.Characters, table.pityGrade))
                    r.Error($"{at}: 천장 등급 {table.pityGrade}의 계보가 없습니다.");
            }
        }

        static bool HasLineageOfGrade(CharacterDatabase db, ItemGrade grade) =>
            db != null && Safe(db.lineages).Any(l => l != null && l.grade == grade);

        // ───────────────────────── 원정 / 상점 / 전투 / 설정 ─────────────────────────

        static void CheckExpeditions(ContentReport r, GameContent c, ContentIndex index)
        {
            if (c.Expeditions == null)
            {
                r.Error("ExpeditionDatabase 에셋이 없습니다.");
                return;
            }
            var zones = Safe(c.Expeditions.zones).ToList();
            if (zones.Count == 0) r.Error("원정 구역이 없습니다.");
            CheckUnique(r, "원정 구역", zones, z => z.id);

            foreach (var zone in zones)
            {
                if (zone == null) continue;
                string at = $"원정 구역 '{zone.id}'";
                if (zone.durationMinutes <= 0) r.Error($"{at}: 원정 시간이 0 이하입니다.");
                if (zone.maxMembers <= 0) r.Error($"{at}: 참여 인원이 0 이하입니다.");
                foreach (var drop in Safe(zone.drops))
                {
                    if (drop != null && !index.Items.ContainsKey(drop.itemId ?? "")) r.Error($"{at}: ItemDatabase에 없는 드롭 '{drop.itemId}'");
                }
            }
        }

        static void CheckShops(ContentReport r, GameContent c, ContentIndex index)
        {
            if (c.Shops == null)
            {
                r.Error("ShopDatabase 에셋이 없습니다.");
                return;
            }
            var shops = Safe(c.Shops.shops).ToList();
            if (shops.Count == 0) r.Error("상점이 없습니다.");
            CheckUnique(r, "상점", shops, s => s.id);
            CheckUnique(r, "상점 상품", shops.Where(s => s != null).SelectMany(s => Safe(s.products)), p => p.id);
            // 상품 이름에 묶음 수를 넣지 않으므로(M30 — "수선 천", 개수는 배지 "×5") 한 상점 안에서 이름이 겹치면 구별할 수 없다
            foreach (var shop in shops.Where(s => s != null))
            {
                var names = new HashSet<string>();
                foreach (var product in Safe(shop.products))
                {
                    if (product == null || string.IsNullOrEmpty(product.name)) continue;
                    if (!names.Add(product.name)) r.Error($"상점 '{shop.id}': 상품 이름 '{product.name}'이(가) 겹칩니다 (개수는 이름이 아니라 배지로 보여 줌).");
                }
            }

            foreach (var shop in shops.Where(s => s != null))
            {
                foreach (var product in Safe(shop.products))
                {
                    if (product != null) CheckProduct(r, shop, product, index);
                }
            }
        }

        static void CheckProduct(ContentReport r, ShopDef shop, ShopProductDef product, ContentIndex index)
        {
            string at = $"상품 '{product.id}'({shop.id})";
            if (string.IsNullOrEmpty(product.name)) r.Error($"{at}: 이름이 비어 있습니다.");
            if (product.price <= 0) r.Warn($"{at}: 가격이 0입니다.");

            if (!string.IsNullOrEmpty(product.costumeId))
            {
                if (!index.Costumes.Contains(product.costumeId)) r.Error($"{at}: CharacterDatabase에 없는 의상 '{product.costumeId}'");
                return;
            }

            var rewards = Safe(product.rewards).Where(w => w != null).ToList();
            if (rewards.Count == 0) r.Error($"{at}: 지급 보상이 없습니다.");
            foreach (var reward in rewards)
            {
                if (reward.amount <= 0) r.Error($"{at}: 보상 수량이 0 이하입니다.");
                if (reward.type == RewardType.Item && !index.Items.ContainsKey(reward.itemId ?? ""))
                    r.Error($"{at}: ItemDatabase에 없는 보상 아이템 '{reward.itemId}'");
            }
        }

        static void CheckBattle(ContentReport r, BattleConfig battle)
        {
            if (battle == null)
            {
                r.Error("BattleConfig 에셋이 없습니다.");
                return;
            }
            var enemies = Safe(battle.enemies).ToList();
            if (enemies.Count == 0) r.Error("적이 하나도 없습니다.");
            CheckUnique(r, "적", enemies, e => e.id);

            foreach (var enemy in enemies.Where(e => e != null))
            {
                string at = $"적 '{enemy.id}'";
                if (string.IsNullOrEmpty(enemy.name)) r.Error($"{at}: 이름이 비어 있습니다.");
                if (enemy.hp <= 0 || enemy.speed <= 0) r.Error($"{at}: 체력/속도가 0 이하입니다.");
                CheckEnemyGimmicks(r, at, enemy, battle);
            }
            if (enemies.Count > 0 && !enemies.Any(e => e != null && e.isBoss)) r.Error("보스 적이 없습니다 (스테이지 보스 단계 진행 불가).");
        }

        static void CheckConfig(ContentReport r, GameContent c)
        {
            if (c.Config == null)
            {
                r.Error("GameConfig 에셋이 없습니다.");
                return;
            }
            var lineageIds = new HashSet<string>(Safe(c.Characters?.lineages).Where(l => l != null).Select(l => l.id));
            foreach (var id in Safe(c.Config.starterLineages))
            {
                if (!lineageIds.Contains(id ?? "")) r.Error($"GameConfig 시작 지급 계보: CharacterDatabase에 없는 계보 '{id}'");
            }
        }

        // ───────────────────────── 아트 (경고) ─────────────────────────

        static void CheckArt(ContentReport r, GameContent c)
        {
            var lineages = Safe(c.Characters?.lineages).Where(l => l != null).ToList();
            var forms = lineages.SelectMany(l => Safe(l.forms)).Where(f => f != null).ToList();
            var costumes = lineages.SelectMany(l => Safe(l.costumes)).Where(x => x != null).ToList();
            var conceptCostumes = costumes.Where(x => CharacterContent.ConceptSheetCostumes.Contains(x.id)).ToList();
            var items = Safe(c.Items?.items).Where(i => i != null).ToList();
            var enemies = Safe(c.Battle?.enemies).Where(e => e != null).ToList();

            int before = r.Warnings.Count;
            WarnMissingArt(r, "캐릭터", "Characters", forms.Count, forms.Where(f => f.portrait == null).Select(f => f.id));
            WarnMissingArt(r, "의상", "Costumes", costumes.Count, costumes.Where(x => x.portrait == null).Select(x => x.id));
            WarnMissingArt(r, "컨셉 시트", "Concept", conceptCostumes.Count, conceptCostumes.Where(x => x.illustration == null).Select(x => x.id));
            WarnMissingArt(r, "아이템", "Items", items.Count, items.Where(i => i.icon == null).Select(i => i.id));
            WarnMissingArt(r, "적", "Enemies", enemies.Count, enemies.Where(e => e.sprite == null).Select(e => e.id));
            WarnMissingSynergyIcons(r, c);
            if (r.Warnings.Count > before)
            {
                r.Warn("아트 파일을 새로 추가했다면 'IdleGame/Setup/콘텐츠 데이터 갱신'을 다시 실행해 스프라이트를 연결하세요.");
            }
        }

        static void WarnMissingArt(ContentReport r, string kind, string folder, int total, IEnumerable<string> missingIds)
        {
            var missing = missingIds.ToList();
            if (missing.Count == 0) return;
            r.Warn($"아트 없음 - {kind} {missing.Count}/{total}개 ({ArtLib.Root}/{folder}/{{id}}.png): {string.Join(", ", missing)}");
        }

        // ───────────────────────── 공통 ─────────────────────────

        static void CheckUnique<T>(ContentReport r, string kind, IEnumerable<T> entries, Func<T, string> getId) where T : class
        {
            var seen = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    r.Error($"{kind}: 비어 있는(null) 항목이 있습니다.");
                    continue;
                }
                string id = getId(entry);
                if (string.IsNullOrEmpty(id)) r.Error($"{kind}: ID가 비어 있는 항목이 있습니다.");
                else if (!seen.Add(id)) r.Error($"{kind}: 중복 ID '{id}'");
            }
        }

        static IEnumerable<T> Safe<T>(IEnumerable<T> list) => list ?? Enumerable.Empty<T>();

        /// <summary>ID → 정의 조회표 (검증 중 참조 확인용)</summary>
        sealed class ContentIndex
        {
            public readonly Dictionary<string, ItemDef> Items = new();
            public readonly HashSet<string> Costumes = new();
            public readonly Dictionary<string, ExpeditionZoneDef> Zones = new();
            public readonly Dictionary<string, ShopDef> Shops = new();
            /// <summary>의상 ID → 그 의상을 파는 첫 상품</summary>
            public readonly Dictionary<string, ShopProductDef> CostumeProducts = new();

            public ContentIndex(GameContent c)
            {
                foreach (var item in Safe(c.Items?.items))
                {
                    if (!string.IsNullOrEmpty(item?.id)) Items.TryAdd(item.id, item);
                }
                foreach (var lineage in Safe(c.Characters?.lineages))
                {
                    foreach (var costume in Safe(lineage?.costumes))
                    {
                        if (!string.IsNullOrEmpty(costume?.id)) Costumes.Add(costume.id);
                    }
                }
                foreach (var zone in Safe(c.Expeditions?.zones))
                {
                    if (!string.IsNullOrEmpty(zone?.id)) Zones.TryAdd(zone.id, zone);
                }
                foreach (var shop in Safe(c.Shops?.shops))
                {
                    if (string.IsNullOrEmpty(shop?.id)) continue;
                    Shops.TryAdd(shop.id, shop);
                    foreach (var product in Safe(shop.products))
                    {
                        if (!string.IsNullOrEmpty(product?.costumeId)) CostumeProducts.TryAdd(product.costumeId, product);
                    }
                }
            }
        }
    }
}
