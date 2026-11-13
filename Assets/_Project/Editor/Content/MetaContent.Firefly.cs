using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// NEW-03 길 잃은 반딧불 (SPEC4 §13.8 · 부록 C.4, 확장 P2): 튜토리얼 뒤 앞에서 전투를 보는 6~12분마다 한 마리, 8초 머묾, 하루 6마리까지.
    /// 보상: 단추 10분치 w55 · 최고 스테이지 잃어버린 상자 표에서 재료 하나 w35 · 별조각 10 w10.
    /// 주당 별조각 (모두 잡으면) 6 × 7 × 10% × 10 = 42 ≤ 60 (§11.2).
    /// 끈 동안(<see cref="Stretch.Firefly"/>) 하루 0마리 — 보상 표는 그대로 둔다. 첫 잡기 토스트 '길 잃은 반딧불이 선물을 두고 갔어요'는 보기(P4-3b).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>하루 최대 반딧불 (확장 항목이 켜졌을 때)</summary>
        internal const int FireflyPerDay = 6;

        static partial void FillFirefly(MetaDatabase db, GameContent content)
        {
            db.firefly = new FireflyConfig
            {
                minInterval = 360f,
                maxInterval = 720f,
                lifeSeconds = 8f,
                perDay = On(Stretch.Firefly) ? FireflyPerDay : 0,
                rewards = new List<FireflyRewardDef>
                {
                    new() { reward = MetaReward.GoldMinutes(10), weight = 55 },
                    // 표에서 재료 하나 — 표를 못 찾으면 이 수선 천 1장 (FireflyRules)
                    new() { reward = Mat(MaterialIds.Cloth, 1), fromLostChestTable = true, weight = 35 },
                    new() { reward = Gem(10), weight = 10 },
                },
            };
        }
    }
}
