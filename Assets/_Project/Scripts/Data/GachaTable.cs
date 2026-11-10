using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class GachaGradeRate
    {
        public ItemGrade grade;
        [Tooltip("가중치. 확률(%) = 가중치 / 전체 가중치 합")]
        public float weight;
    }

    /// <summary>뽑기 테이블. 등급을 가중치로 먼저 뽑고, 해당 등급 아이템 중 하나를 균등하게 뽑는다.</summary>
    [CreateAssetMenu(fileName = "GachaTable", menuName = "IdleGame/Gacha Table")]
    public class GachaTable : ScriptableObject
    {
        public string tableId = "normal";
        public string displayName = "일반 소환";

        [Header("비용")]
        public CurrencyType costCurrency = CurrencyType.Gem;
        public double singleCost = 100;
        [Tooltip("10회 소환 비용 (할인 적용가)")]
        public double tenCost = 900;

        [Header("등급 확률")]
        public List<GachaGradeRate> rates = new();

        [Header("천장")]
        [Tooltip("N회째 소환에서 천장 등급 이상 확정. 0이면 사용 안 함")]
        public int pityCount = 50;
        public ItemGrade pityGrade = ItemGrade.Legendary;
    }
}
