using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class ShopProductDef
    {
        [Tooltip("모든 상점을 통틀어 고유해야 한다 (구매 기록 키)")]
        public string id;
        public string name;
        [Tooltip("지급 보상 (재화/재료)")]
        public List<RewardData> rewards = new();
        [Tooltip("의상 상품이면 의상 ID (rewards 대신 의상 해금)")]
        public string costumeId;
        public CurrencyType currency = CurrencyType.Gold;
        public double price;
        [Tooltip("하루 구매 제한. 0이면 무제한")]
        public int dailyLimit;
        [Tooltip("전체 구매 제한. 0이면 무제한")]
        public int totalLimit;
    }

    [Serializable]
    public class ShopDef
    {
        [Tooltip("shop_normal / shop_event")]
        public string id;
        public string name;
        [TextArea] public string description;
        public List<ShopProductDef> products = new();
    }

    /// <summary>상점 데이터. 기본값은 ApplyDefaults에서 채운다.</summary>
    [CreateAssetMenu(fileName = "ShopDatabase", menuName = "IdleGame/Shop Database")]
    public class ShopDatabase : ScriptableObject
    {
        /// <summary>수선점 상점 (단추/별조각 → 재료, 일일 제한)</summary>
        public const string NormalShopId = "shop_normal";
        /// <summary>'별이 머문 밤 산책' 이벤트 상점 (별빛 사탕 → 희귀 재료/의상)</summary>
        public const string EventShopId = "shop_event";

        public List<ShopDef> shops = new();

        public ShopDef Get(string id) => id == null ? null : shops.Find(s => s != null && s.id == id);

        /// <summary>상점의 상품. 상점이나 상품이 없으면 null</summary>
        public ShopProductDef FindProduct(string shopId, string productId)
        {
            var shop = Get(shopId);
            return shop == null || productId == null ? null : shop.products.Find(p => p != null && p.id == productId);
        }

        /// <summary>기본 상점/상품을 채운다.</summary>
        public void ApplyDefaults()
        {
            shops = new List<ShopDef>
            {
                new ShopDef
                {
                    id = NormalShopId,
                    name = "수선점 상점",
                    description = "잊힌 친구들을 고칠 재료를 팔아요.\n구매 횟수는 매일 새벽 5시에 다시 채워져요.",
                    products = new List<ShopProductDef>
                    {
                        // 단추 가격은 결정 2 안 D(단추 수입이 절반)에 맞춰 × 0.5 (400 → 200, 300 → 150)
                        Material("normal_cloth", "mat_cloth", "수선 천", 5, CurrencyType.Gold, 200, dailyLimit: 3),
                        Material("normal_ribbon", "mat_ribbon", "리본 조각", 5, CurrencyType.Gold, 150, dailyLimit: 3),
                        Material("normal_wax", "mat_wax", "촛농 방울", 5, CurrencyType.Gold, 150, dailyLimit: 3),
                        Material("normal_thread", "mat_thread", "기억의 실", 1, CurrencyType.Gem, 30, dailyLimit: 3),
                        Material("normal_lace", "mat_lace", "레이스 조각", 1, CurrencyType.Gem, 30, dailyLimit: 3),
                        Material("normal_rib", "mat_rib", "우산살", 1, CurrencyType.Gem, 30, dailyLimit: 3),
                    },
                },
                new ShopDef
                {
                    id = EventShopId,
                    name = "별이 머문 밤 산책",
                    description = "스테이지를 클리어하고 모은 별빛 사탕으로\n밤 산책에서만 만날 수 있는 물건과 의상을 바꿔 가세요.",
                    products = new List<ShopProductDef>
                    {
                        Material("event_suitcase", "mat_suitcase", "여행 가방 조각", 1, CurrencyType.EventToken, 20, dailyLimit: 5),
                        Material("event_moonlight", "mat_moonlight", "달빛 조각", 1, CurrencyType.EventToken, 60, totalLimit: 10),
                        Costume("event_jjak_sleepcap", "jjak_sleepcap", "수면모자 짝짝이", 150),
                        Costume("event_jjak_festival", "jjak_festival", "축제 지휘자 짝짝이", 300),
                    },
                },
            };
        }

        /// <summary>
        /// 재료 상품. 이름은 재료 이름만 쓴다 (M30 — 개수는 카드 배지 "×5"가 맡고, 확인 창·구매 완료 제목은 보상에서 "이름 ×5"를 만든다).
        /// 같은 재료를 묶음 수만 다르게 두 상품으로 팔면 이름이 겹치므로 ContentValidator가 상품 이름 중복을 막는다.
        /// </summary>
        static ShopProductDef Material(string id, string itemId, string itemName, int count, CurrencyType currency, double price,
            int dailyLimit = 0, int totalLimit = 0) => new ShopProductDef
        {
            id = id,
            name = itemName,
            rewards = new List<RewardData> { RewardData.Item(itemId, count) },
            currency = currency,
            price = price,
            dailyLimit = dailyLimit,
            totalLimit = totalLimit,
        };

        /// <summary>의상 상품은 별빛 사탕으로 한 번만 살 수 있다.</summary>
        static ShopProductDef Costume(string id, string costumeId, string costumeName, double price) => new ShopProductDef
        {
            id = id,
            name = costumeName,
            costumeId = costumeId,
            currency = CurrencyType.EventToken,
            price = price,
            totalLimit = 1,
        };
    }
}
