using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// EVT-02 오프라인 쿠폰 (SPEC4 §4.5, 확장 P2): 번호는 저장하지 않고 해시만 둔다 — SHA256(UTF8(소금 + ":" + 번호)), 소문자 16진수 64자.
    /// 번호는 CouponSystem.Normalize로 다듬은 것 (앞뒤 · 가운데 공백과 '-'를 빼고 ASCII 소문자만 대문자로, ^[A-Z0-9]{6,16}$ — 해시 만들기 창도 같은 함수).
    ///   · 해시 만들기: 메뉴 'IdleGame/쿠폰 해시 만들기' (MetaDataBuilder) — 입력한 번호는 어디에도 저장하지 않고 해시만 보여 준다.
    ///   · 실은 것: 시험용(QA) 쿠폰 하나뿐. 그 번호는 저장소 밖에 둔다. 실제 홍보 쿠폰은 사용자 결정 (밖으로 나가는 일).
    ///   · 보상은 재화 · 재료만 (우편은 메타 보관함 보상을 싣지 못한다). 30일 뒤 사라지는 우편으로 간다 (CouponSystem).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>쿠폰 해시 소금 (바꾸면 모든 해시를 다시 만들어야 한다)</summary>
        internal const string CouponSalt = "d6088fa07d59a187721cb4b5";

        /// <summary>시험용 쿠폰 해시 (번호는 저장소 밖 — RM-2 P4-8 인계 메모)</summary>
        internal const string QaCouponHash = "079b54ee7b6bf1ab499e33c3ef37fe3f48e8514ed7897b4c71047be2c4731f1e";

        static partial void FillCoupons(MetaDatabase db, GameContent content)
        {
            db.couponSalt = CouponSalt;
            db.coupons.Add(new CouponDef
            {
                hash = QaCouponHash,
                validFromDayKey = null,
                validToDayKey = null,
                rewards = new List<RewardData>
                {
                    RewardData.Currency(CurrencyType.Gem, 50),
                    RewardData.Item(MaterialIds.Cloth, 10),
                },
                mailTitle = "쿠폰 선물이 도착했어요",
                mailBody = "쿠폰으로 받은 작은 선물이에요. 수선점을 아껴 줘서 고마워요!",
            });
        }
    }
}
