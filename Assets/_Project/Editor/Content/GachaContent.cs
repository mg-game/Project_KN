using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 가챠 "잃어버린 친구 찾기": 등급을 확률로 뽑고, 그 등급의 계보 중 하나를 균등하게 뽑는다.
    /// 중복 획득은 기억편으로 변환된다 (변환량은 GameConfig / CharacterSystem 담당).
    /// </summary>
    internal static class GachaContent
    {
        public const string FriendsTableId = "friends";

        public static void ApplyFriends(GachaTable table)
        {
            table.tableId = FriendsTableId;
            table.displayName = "잃어버린 친구 찾기";
            table.costCurrency = CurrencyType.Gem;
            table.singleCost = 100;
            table.tenCost = 900;
            table.rates = new List<GachaGradeRate>
            {
                Rate(ItemGrade.Common, 45f),
                Rate(ItemGrade.Rare, 33f),
                Rate(ItemGrade.Epic, 15f),
                Rate(ItemGrade.Legendary, 6f),
                Rate(ItemGrade.Mythic, 1f),
            };
            table.pityCount = 60;
            table.pityGrade = ItemGrade.Legendary;
        }

        static GachaGradeRate Rate(ItemGrade grade, float weight) => new() { grade = grade, weight = weight };
    }
}
