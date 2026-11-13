using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>재료 아이템 ID. 레시피/상점/원정 데이터에서 오타를 컴파일 단계에서 잡기 위해 상수로 둔다.</summary>
    internal static class MaterialIds
    {
        public const string Cloth = "mat_cloth";
        public const string Ribbon = "mat_ribbon";
        public const string Wax = "mat_wax";
        public const string Thread = "mat_thread";
        public const string Lace = "mat_lace";
        public const string Rib = "mat_rib";
        public const string Suitcase = "mat_suitcase";
        public const string Spring = "mat_spring";
        public const string Moonlight = "mat_moonlight";
    }

    /// <summary>
    /// 재료 9종. 획득처(sources)는 "재료 모으러 가기" 이동과 획득처 줄에 쓰인다.
    /// targetId 규칙: 원정 = 구역 ID, 던전 = 최소 층(숫자 문자열), 상점 = 상점 ID, 스테이지 = "".
    /// 순서 = 가장 쉽게 닿는 곳부터 (M17 — SourceAvailability.FirstAvailable이 이 순서로 처음 열린 곳을 고른다):
    ///   단추로 사는 일반 상점 → 많이·확실히 주는 원정 → 확률로 주는 원정 → 별조각 상점 → 스테이지(늘 열림, 맨 뒤).
    /// 원정 획득처는 ExpeditionDatabase 드롭 표, 던전 층은 DungeonSystem 보상 층, 상점은 ShopDatabase 상품과 맞춘다
    /// (ContentValidator가 드롭하지 않는 구역·층·상점을 가리키면 경고). 스테이지 드롭은 BattleSystem(수선 천·리본 조각).
    /// </summary>
    internal static class ItemContent
    {
        public static List<ItemDef> Build() => new()
        {
            Material(MaterialIds.Cloth, "수선 천", ItemGrade.Common,
                "수선점 바구니에 차곡차곡 개켜 둔 천 조각. 해진 곳을 덧대고 터진 곳을 감싸는, 모든 수선의 시작이에요.\n" +
                "별빛 세탁소에서 갓 걷어 온 천에서는 아직 햇살 냄새가 나요.",
                Shop(ShopDatabase.NormalShopId, NormalShopLabel), Expedition("exp_1_4", "원정 1-4"), Expedition("exp_1_1", "원정 1-1"),
                Expedition("exp_2_4", "원정 2-4"), Stage()),

            Material(MaterialIds.Ribbon, "리본 조각", ItemGrade.Common,
                "선물 상자에서 풀려나 골목 어귀를 떠돌던 리본 끄트머리.\n" +
                "누군가의 축하를 한 번 묶어 본 적이 있어서, 아직 무언가를 꼭 묶어 두는 힘이 남아 있어요.",
                Shop(ShopDatabase.NormalShopId, NormalShopLabel), Expedition("exp_1_1", "원정 1-1"), Expedition("exp_1_4", "원정 1-4"),
                Expedition("exp_1_2", "원정 1-2"), Stage()),

            Material(MaterialIds.Wax, "촛농 방울", ItemGrade.Common,
                "꺼진 가로등 아래에 동그랗게 굳어 있던 촛농. 손바닥에 올려 두면 아직 미지근한 온기가 느껴져요.\n" +
                "누군가를 끝까지 밝혀 준 촛불이 남기고 간 마지막 체온이에요.",
                Shop(ShopDatabase.NormalShopId, NormalShopLabel), Expedition("exp_1_3", "원정 1-3")),

            Material(MaterialIds.Thread, "기억의 실", ItemGrade.Rare,
                "잔향이가 빛으로 풀려 사라질 때 남기고 간 가느다란 실. 잊힌 물건의 기억이 한 올 한 올 꼬여 있어요.\n" +
                "형태를 새로 엮어 내는 진화 제작에 꼭 필요해요.",
                Dungeon(3), Expedition("exp_2_3", "원정 2-3"), Shop(ShopDatabase.NormalShopId, NormalShopLabel)),

            Material(MaterialIds.Lace, "레이스 조각", ItemGrade.Rare,
                "젖은 버스 정류장 벤치에 걸린 채 주인을 기다리던 레이스.\n" +
                "빗물에 흠뻑 젖어도 무늬가 흐트러지지 않는, 섬세하고 질긴 천이에요.",
                Expedition("exp_1_2", "원정 1-2"), Expedition("exp_2_2", "원정 2-2"), Expedition("exp_1_3", "원정 1-3"),
                Shop(ShopDatabase.NormalShopId, NormalShopLabel)),

            Material(MaterialIds.Rib, "우산살", ItemGrade.Rare,
                "비 오는 광장에 버려진 우산에서 빠져나온 가는 금속 살.\n" +
                "휘어져도 다시 곧게 펴지는 탄성이 있어, 몸의 뼈대와 틀을 세우는 데 쓰여요.",
                Expedition("exp_2_1", "원정 2-1"), Shop(ShopDatabase.NormalShopId, NormalShopLabel)),

            Material(MaterialIds.Suitcase, "여행 가방 조각", ItemGrade.Epic,
                "끝내 떠나지 못한 여행 가방에서 떼어 낸 가죽 조각. 모서리의 낡은 스티커에 가 보지 못한 도시들의 이름이 남아 있어요.\n" +
                "'별이 머문 밤 산책' 이벤트 상점에서 구할 수 있어요.",
                Shop(ShopDatabase.EventShopId, EventShopLabel)),

            Material(MaterialIds.Spring, "오르골 태엽", ItemGrade.Epic,
                "멈춰 버린 오르골 속에서 꺼낸 작은 태엽.\n" +
                "귀에 가까이 대면, 끝까지 연주되지 못한 노래의 한 소절이 아주 작게 들려요.",
                Dungeon(5), Expedition("exp_2_2", "원정 2-2")),

            Material(MaterialIds.Moonlight, "달빛 조각", ItemGrade.Legendary,
                "잔향이 던전 가장 깊은 곳에 고여 있던 달빛이 단단하게 굳은 조각. 밤지기의 모자에 꿰매어진 것과 같은 빛이에요.\n" +
                "2차 진화의 마지막 한 땀에 쓰여요.",
                Dungeon(10), Shop(ShopDatabase.EventShopId, EventShopLabel)),
        };

        /// <summary>획득처 줄·"사러 가기 · {목적지}" 버튼에 보이는 상점 이름 (상점 탭 이름과 같게)</summary>
        const string NormalShopLabel = "수선점 상점";
        const string EventShopLabel = "이벤트 상점";

        static ItemDef Material(string id, string name, ItemGrade grade, string description, params ItemSource[] sources) => new()
        {
            id = id,
            name = name,
            grade = grade,
            category = ItemCategory.Material,
            description = description,
            sources = new List<ItemSource>(sources),
        };

        static ItemSource Expedition(string zoneId, string label) => Source(ItemSourceType.Expedition, zoneId, label);
        static ItemSource Dungeon(int floor) => Source(ItemSourceType.Dungeon, floor.ToString(), $"잔향이 던전 {floor}층");
        static ItemSource Shop(string shopId, string label) => Source(ItemSourceType.Shop, shopId, label);
        static ItemSource Stage() => Source(ItemSourceType.Stage, "", "스테이지 보상");

        static ItemSource Source(ItemSourceType type, string targetId, string label) =>
            new() { type = type, targetId = targetId, label = label };
    }
}
