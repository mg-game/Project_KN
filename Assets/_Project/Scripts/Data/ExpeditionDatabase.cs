using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    [Serializable]
    public class ExpeditionDrop
    {
        public string itemId;
        public int min = 1;
        public int max = 1;
        [Range(0f, 1f)] public float chance = 1f;
    }

    [Serializable]
    public class ExpeditionZoneDef
    {
        [Tooltip("exp_{챕터}_{번호}. 예: exp_2_4")]
        public string id;
        [Tooltip("예: 원정 2-4")]
        public string name;
        [Tooltip("장소 이름. 예: 비 오는 버스 정류장")]
        public string place;
        [TextArea] public string description;
        [Tooltip("개방에 필요한 최고 스테이지")]
        public int requiredStage = 1;
        public int durationMinutes = 30;
        public int maxMembers = 3;
        public double goldReward;
        public List<ExpeditionDrop> drops = new();
    }

    /// <summary>원정(파견) 구역 데이터. 기본값은 ApplyDefaults에서 채운다.</summary>
    [CreateAssetMenu(fileName = "ExpeditionDatabase", menuName = "IdleGame/Expedition Database")]
    public class ExpeditionDatabase : ScriptableObject
    {
        public List<ExpeditionZoneDef> zones = new();

        public ExpeditionZoneDef Get(string id) => string.IsNullOrEmpty(id) ? null : zones.Find(z => z != null && z.id == id);

        /// <summary>기본 원정 구역 8곳(챕터 1·2)을 채운다.</summary>
        public void ApplyDefaults()
        {
            zones = new List<ExpeditionZoneDef>
            {
                Zone("exp_1_1", "원정 1-1", "잃어버린 골목", 1, 10, 300,
                    "누군가 떨어뜨린 물건들이 바람에 굴러 모이는 좁은 골목. 처음 원정을 떠나기 좋은 곳이에요.",
                    Drop("mat_ribbon", 2, 4), Drop("mat_cloth", 1, 2, 0.6f)),
                Zone("exp_1_2", "원정 1-2", "젖은 버스 정류장", 3, 20, 600,
                    "막차가 떠난 정류장 벤치 아래, 비에 젖은 레이스 손수건 조각들이 기다리고 있어요.",
                    Drop("mat_lace", 1, 3), Drop("mat_ribbon", 1, 2, 0.4f)),
                Zone("exp_1_3", "원정 1-3", "꺼진 가로등 아래", 5, 30, 1000,
                    "불이 꺼진 가로등 아래에는 누군가 켜 두었던 초의 촛농이 굳어 있어요.",
                    Drop("mat_wax", 2, 4), Drop("mat_lace", 1, 1, 0.3f)),
                Zone("exp_1_4", "원정 1-4", "닫힌 장난감 가게", 8, 45, 3000,
                    "셔터가 내려간 장난감 가게. 진열장 뒤편에 단추와 천 조각이 잔뜩 숨겨져 있어요.",
                    Drop("mat_cloth", 2, 4), Drop("mat_ribbon", 2, 3)),
                Zone("exp_2_1", "원정 2-1", "비 오는 광장", 11, 60, 2500,
                    "주인을 잃은 우산들이 펼쳐진 채 비를 맞는 광장. 부러진 우산살을 주워 올 수 있어요.",
                    Drop("mat_rib", 1, 3), Drop("mat_cloth", 1, 2, 0.5f)),
                Zone("exp_2_2", "원정 2-2", "먼지 쌓인 다락방", 14, 90, 4000,
                    "오래된 상자들이 잠든 다락방. 운이 좋으면 멈춘 오르골의 태엽을 찾을 수도 있어요.",
                    Drop("mat_lace", 2, 4), Drop("mat_spring", 1, 1, 0.15f)),
                Zone("exp_2_3", "원정 2-3", "분실물 보관소", 17, 120, 6000,
                    "주인을 기다리는 물건들이 선반마다 빼곡한 보관소. 가끔 기억의 실이 엉켜 있어요.",
                    Drop("mat_cloth", 2, 4), Drop("mat_thread", 1, 2, 0.25f)),
                Zone("exp_2_4", "원정 2-4", "별빛 세탁소", 20, 180, 9000,
                    "밤에만 문을 여는 세탁소. 별빛에 말린 수선 천을 한 아름 얻을 수 있어요.",
                    Drop("mat_cloth", 6, 10), Drop("mat_ribbon", 2, 4, 0.5f)),
            };
        }

        static ExpeditionZoneDef Zone(string id, string name, string place, int requiredStage, int minutes, double gold,
            string description, params ExpeditionDrop[] drops) => new ExpeditionZoneDef
        {
            id = id,
            name = name,
            place = place,
            description = description,
            requiredStage = requiredStage,
            durationMinutes = minutes,
            maxMembers = 3,
            goldReward = gold,
            drops = new List<ExpeditionDrop>(drops),
        };

        static ExpeditionDrop Drop(string itemId, int min, int max, float chance = 1f) =>
            new ExpeditionDrop { itemId = itemId, min = min, max = max, chance = chance };
    }
}
