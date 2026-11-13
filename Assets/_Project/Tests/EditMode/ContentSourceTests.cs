using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    /// <summary>
    /// 재료 획득처 콘텐츠 감사 (M17): 코드 원본 ItemContent.Build()의 획득처가 실제 드롭 표와 맞는지 —
    /// 원정 = ExpeditionDatabase 기본 드롭, 상점 = ShopDatabase 기본 상품, 던전 = DungeonSystem 보상 층(BattleConfig), 스테이지 = 수선 천·리본 조각.
    /// 순서: 수선 천은 일반 상점이 처음, 스테이지는 늘 맨 뒤. (에디터 어셈블리는 시험 어셈블리가 참조하지 않아 불러온 어셈블리에서 찾는다)
    /// </summary>
    public class ContentSourceTests
    {
        ExpeditionDatabase _expeditions;
        ShopDatabase _shops;
        BattleConfig _battle;

        [SetUp]
        public void SetUp()
        {
            _expeditions = ScriptableObject.CreateInstance<ExpeditionDatabase>();
            _expeditions.ApplyDefaults();
            _shops = ScriptableObject.CreateInstance<ShopDatabase>();
            _shops.ApplyDefaults();
            _battle = ScriptableObject.CreateInstance<BattleConfig>();
            _battle.ApplyDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_expeditions);
            Object.DestroyImmediate(_shops);
            Object.DestroyImmediate(_battle);
        }

        static List<ItemDef> ContentItems()
        {
            Type content = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IdleGame.Editor") continue;
                content = assembly.GetType("IdleGame.EditorTools.ItemContent", false);
                break;
            }
            Assert.IsNotNull(content, "IdleGame.Editor 어셈블리의 ItemContent를 찾을 수 없다");
            var build = content.GetMethod("Build", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.IsNotNull(build, "ItemContent.Build()");
            var items = build.Invoke(null, null) as List<ItemDef>;
            Assert.IsNotNull(items, "ItemContent.Build()는 List<ItemDef>를 돌려준다");
            return items;
        }

        [Test]
        public void Cloth_ShopFirst_StageLast()
        {
            var cloth = ContentItems().Find(i => i.id == "mat_cloth");
            Assert.IsNotNull(cloth);
            var sources = cloth.sources;
            Assert.AreEqual(ItemSourceType.Shop, sources[0].type, "수선 천은 단추로 바로 사는 일반 상점이 먼저");
            Assert.AreEqual(ShopDatabase.NormalShopId, sources[0].targetId);
            CollectionAssert.AreEqual(new[] { "exp_1_4", "exp_1_1", "exp_2_4" },
                sources.FindAll(s => s.type == ItemSourceType.Expedition).ConvertAll(s => s.targetId));
            Assert.AreEqual(ItemSourceType.Stage, sources[sources.Count - 1].type);
        }

        [Test]
        public void EverySource_ActuallyDropsTheItem()
        {
            foreach (var item in ContentItems())
            {
                if (item.category != ItemCategory.Material) continue;
                Assert.IsNotEmpty(item.sources, $"{item.id}: 획득처 없음");
                for (int i = 0; i < item.sources.Count; i++)
                {
                    var source = item.sources[i];
                    string at = $"{item.id} 획득처 {i} {source.type} '{source.targetId}'";
                    Assert.IsFalse(string.IsNullOrEmpty(source.label), $"{at}: 표시 문구");
                    switch (source.type)
                    {
                        case ItemSourceType.Expedition:
                        {
                            var zone = _expeditions.Get(source.targetId);
                            Assert.IsNotNull(zone, $"{at}: 원정 구역 없음");
                            Assert.IsTrue(zone.drops.Exists(d => d.itemId == item.id), $"{at}: 그 구역은 이 재료를 주지 않는다");
                            break;
                        }
                        case ItemSourceType.Shop:
                        {
                            var shop = _shops.Get(source.targetId);
                            Assert.IsNotNull(shop, $"{at}: 상점 없음");
                            Assert.IsTrue(shop.products.Exists(p => p.rewards.Exists(r => r.type == RewardType.Item && r.itemId == item.id)),
                                $"{at}: 그 상점은 이 재료를 팔지 않는다");
                            break;
                        }
                        case ItemSourceType.Dungeon:
                        {
                            int dropFloor = item.id switch
                            {
                                DungeonSystem.ThreadItemId => _battle.dungeonThreadFloor,
                                DungeonSystem.SpringItemId => _battle.dungeonSpringFloor,
                                DungeonSystem.MoonlightItemId => _battle.dungeonMoonlightFloor,
                                _ => -1,
                            };
                            Assert.GreaterOrEqual(dropFloor, 1, $"{at}: 던전은 이 재료를 주지 않는다");
                            Assert.IsTrue(int.TryParse(source.targetId, out int floor), $"{at}: 층은 숫자");
                            Assert.GreaterOrEqual(floor, dropFloor, $"{at}: {dropFloor}층부터 나온다");
                            break;
                        }
                        case ItemSourceType.Stage:
                            CollectionAssert.Contains(new[] { "mat_cloth", "mat_ribbon" }, item.id, $"{at}: 스테이지는 수선 천·리본 조각만 준다");
                            Assert.AreEqual(item.sources.Count - 1, i, $"{at}: 스테이지는 늘 열려 있어 맨 뒤");
                            break;
                    }
                }
            }
        }
    }
}
