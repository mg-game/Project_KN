using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>획득처 하나를 지금 갈 수 있는지 (SourceAvailability.Check 결과)</summary>
    public readonly struct SourceCheck
    {
        /// <summary>지금 가면 그 재료를 얻을 수 있음</summary>
        public readonly bool Available;
        /// <summary>갈 수 없을 때 이유 (토스트 "{재료}은(는) {이유}"). 갈 수 있으면 빈 문자열</summary>
        public readonly string Reason;
        /// <summary>잠긴 획득처를 정렬할 도달 스테이지 (원정 구역 requiredStage / 기능이 열리는 스테이지 + 1). 모르면 int.MaxValue, 열렸으면 0</summary>
        public readonly int RequiredStage;

        public SourceCheck(bool available, string reason, int requiredStage)
        {
            Available = available;
            Reason = reason ?? string.Empty;
            RequiredStage = requiredStage;
        }

        /// <summary>var (available, reason) = SourceAvailability.Check(source, itemId);</summary>
        public void Deconstruct(out bool available, out string reason)
        {
            available = Available;
            reason = Reason;
        }
    }

    /// <summary>
    /// 재료 획득처(ItemSource)를 지금 갈 수 있는지 (M17). "재료 모으러 가기"·레시피 획득처 줄·아이템 정보 획득처 줄이
    /// 팝업을 닫고 이동하기 전에 먼저 본다 — 잠긴 곳으로 보내 놓고 아무것도 못 하게 하지 않는다.
    ///   · 원정: 원정 기능이 열렸고 그 구역이 열림 (이유: "스테이지 2-9를 클리어하면 열려요" — MetaText.UnlockSentence(requiredStage))
    ///   · 던전: 잔향이 던전 기능이 열림
    ///   · 상점 / 이벤트: 기능이 열렸고 그 상점에 그 재료를 주는 상품이 오늘 남아 있음 (이유: "오늘 상점 수량을 모두 샀어요")
    ///   · 스테이지: 늘 갈 수 있음 (이동하지 않고 StageHint 토스트만, 목록에서 맨 뒤)
    /// 기능 잠금 이유는 FeatureGateSystem.GetRequirementText와 같은 문장이다. 팝업 열 때·누를 때만 부른다 (매 프레임 아님).
    /// </summary>
    public static class SourceAvailability
    {
        /// <summary>상점 상품을 오늘 다 샀을 때</summary>
        public const string ShopSoldOutReason = "오늘 상점 수량을 모두 샀어요";
        /// <summary>상점에 그 재료를 주는 상품이 없을 때 (콘텐츠 실수 — ContentValidator가 경고)</summary>
        public const string ShopMissingReason = "지금은 상점에서 팔지 않아요";
        /// <summary>원정 구역 데이터가 없을 때</summary>
        public const string ZoneMissingReason = "지금은 갈 수 없는 원정이에요";
        /// <summary>스테이지 획득처를 누르면 (이동 없음)</summary>
        public const string StageHint = "전투(스테이지)에서 가끔 얻을 수 있어요";

        static readonly SourceCheck Open = new(true, string.Empty, 0);

        /// <summary>지금 게임 상태(Game.*)로 확인한다. itemId를 주면 상점 재고까지 본다 (없으면 상점이 열렸는지만)</summary>
        public static SourceCheck Check(ItemSource source, string itemId = null) =>
            Check(source, itemId, Game.Features, Game.Expedition, Game.Shop);

        /// <summary>시스템을 직접 받아 확인한다 (테스트용). 시스템이 null이면 그 조건은 통과로 본다</summary>
        public static SourceCheck Check(ItemSource source, string itemId, FeatureGateSystem features, ExpeditionSystem expedition, ShopSystem shop)
        {
            if (source == null) return new SourceCheck(false, string.Empty, int.MaxValue);
            switch (source.type)
            {
                case ItemSourceType.Stage:
                    return Open;

                case ItemSourceType.Expedition:
                {
                    var zone = expedition?.Db != null ? expedition.Db.Get(source.targetId) : null;
                    if (expedition != null && zone == null) return new SourceCheck(false, ZoneMissingReason, int.MaxValue);
                    int zoneStage = zone != null ? Math.Max(1, zone.requiredStage) : 0;
                    if (!IsUnlocked(features, FeatureId.Expedition))
                        return new SourceCheck(false, features.GetRequirementText(FeatureId.Expedition), Math.Max(zoneStage, FeatureStage(features, FeatureId.Expedition)));
                    if (expedition != null && !expedition.IsZoneUnlocked(zone.id))
                        return new SourceCheck(false, MetaText.UnlockSentence(zoneStage), zoneStage);
                    return Open;
                }

                case ItemSourceType.Dungeon:
                    if (!IsUnlocked(features, FeatureId.Dungeon))
                        return new SourceCheck(false, features.GetRequirementText(FeatureId.Dungeon), FeatureStage(features, FeatureId.Dungeon));
                    return Open;

                case ItemSourceType.Shop:
                case ItemSourceType.Event:
                {
                    var feature = source.type == ItemSourceType.Event ? FeatureId.Events : FeatureId.Shop;
                    if (!IsUnlocked(features, feature))
                        return new SourceCheck(false, features.GetRequirementText(feature), FeatureStage(features, feature));
                    if (shop == null || string.IsNullOrEmpty(itemId) || shop.Db == null) return Open;
                    var def = shop.Db.Get(source.targetId);
                    // 이벤트 획득처가 상점이 아닌 곳(예: 이벤트 보상)을 가리키면 기능만 본다
                    if (def == null) return source.type == ItemSourceType.Event ? Open : new SourceCheck(false, ShopMissingReason, int.MaxValue);
                    bool sells = false;
                    for (int i = 0; i < def.products.Count; i++)
                    {
                        var product = def.products[i];
                        if (!Gives(product, itemId)) continue;
                        sells = true;
                        if (shop.GetRemaining(product) != 0) return Open;
                    }
                    return new SourceCheck(false, sells ? ShopSoldOutReason : ShopMissingReason, sells ? 0 : int.MaxValue);
                }

                default:
                    return Open;
            }
        }

        /// <summary>
        /// 재료의 획득처 중 목록 순서로 처음 갈 수 있는 곳 (콘텐츠가 가까운 곳부터 적는다). 없으면 null, firstReason = 첫 잠금 이유.
        /// "재료 모으러 가기"가 쓴다 (스테이지 획득처도 갈 수 있는 곳으로 센다 — 이동 대신 StageHint).
        /// </summary>
        public static ItemSource FirstAvailable(ItemDef item, out string firstReason) =>
            FirstAvailable(item, out firstReason, Game.Features, Game.Expedition, Game.Shop);

        /// <summary>FirstAvailable (시스템을 직접 받는다 — 테스트용)</summary>
        public static ItemSource FirstAvailable(ItemDef item, out string firstReason, FeatureGateSystem features, ExpeditionSystem expedition,
            ShopSystem shop)
        {
            firstReason = string.Empty;
            if (item?.sources == null) return null;
            for (int i = 0; i < item.sources.Count; i++)
            {
                var source = item.sources[i];
                if (source == null) continue;
                var check = Check(source, item.id, features, expedition, shop);
                if (check.Available) return source;
                if (firstReason.Length == 0) firstReason = check.Reason;
            }
            return null;
        }

        /// <summary>
        /// 획득처 줄 표시 순서 (into를 비우고 채움): 갈 수 있는 곳을 콘텐츠 순서대로(스테이지는 그 맨 뒤),
        /// 그다음 잠긴 곳을 열리는 스테이지 순서로. 같은 스테이지면 콘텐츠 순서. checks에는 into와 같은 순서로 결과를 채운다 (null이면 건너뜀).
        /// </summary>
        public static void SortForDisplay(ItemDef item, List<ItemSource> into, List<SourceCheck> checks = null) =>
            SortForDisplay(item, into, checks, Game.Features, Game.Expedition, Game.Shop);

        /// <summary>SortForDisplay (시스템을 직접 받는다 — 테스트용)</summary>
        public static void SortForDisplay(ItemDef item, List<ItemSource> into, List<SourceCheck> checks, FeatureGateSystem features,
            ExpeditionSystem expedition, ShopSystem shop)
        {
            if (into == null) return;
            into.Clear();
            checks?.Clear();
            if (item?.sources == null) return;

            var keys = new List<long>(item.sources.Count);
            var results = new List<SourceCheck>(item.sources.Count);
            for (int i = 0; i < item.sources.Count; i++)
            {
                var source = item.sources[i];
                if (source == null) continue;
                var check = Check(source, item.id, features, expedition, shop);
                // 정렬 키: 0 = 갈 수 있음, 1 = 갈 수 있는 스테이지, 2 = 잠김(열리는 스테이지 순) — 같은 키 안에서는 콘텐츠 순서
                long group = check.Available ? (source.type == ItemSourceType.Stage ? 1 : 0) : 2;
                long stage = check.Available ? 0 : Math.Min(int.MaxValue, Math.Max(0, check.RequiredStage));
                long key = (group << 40) | (stage << 8) | (long)Math.Min(255, i);
                // 삽입 정렬 (획득처는 몇 개뿐)
                int at = keys.Count;
                while (at > 0 && keys[at - 1] > key) at--;
                keys.Insert(at, key);
                into.Insert(at, source);
                results.Insert(at, check);
            }
            checks?.AddRange(results);
        }

        static bool IsUnlocked(FeatureGateSystem features, FeatureId feature) => features == null || features.IsUnlocked(feature);

        static bool Gives(ShopProductDef product, string itemId)
        {
            if (product?.rewards == null) return false;
            for (int i = 0; i < product.rewards.Count; i++)
            {
                var reward = product.rewards[i];
                if (reward != null && reward.type == RewardType.Item && reward.itemId == itemId) return true;
            }
            return false;
        }

        /// <summary>기능이 열리는 도달 스테이지 (스테이지 클리어 조건 중 가장 이른 것 + 1). 스테이지 조건이 없으면 int.MaxValue</summary>
        static int FeatureStage(FeatureGateSystem features, FeatureId feature)
        {
            var def = features?.GetDef(feature);
            if (def?.conditions == null) return int.MaxValue;
            long best = long.MaxValue;
            for (int i = 0; i < def.conditions.Count; i++)
            {
                var condition = def.conditions[i];
                if (condition != null && condition.type == MetaConditionType.StageCleared) best = Math.Min(best, condition.target + 1);
            }
            return best == long.MaxValue ? int.MaxValue : (int)Math.Min(int.MaxValue, Math.Max(1, best));
        }
    }
}
