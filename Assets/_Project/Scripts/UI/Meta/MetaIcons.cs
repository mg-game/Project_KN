using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 메타 UI가 쓰는 그림 묶음 (재화·수선권·기능 아이콘, 바구니 4단계, 도장, 상자 등).
    /// ArtLib은 에디터 전용이라 MetaSceneBuilder가 이 에셋을 채워 뷰에 연결한다 (Fill + 공용 조각은 Binder).
    /// 아트가 없는 칸은 null → 뷰가 가까운 다른 그림(선물 상자 등)으로 대신하고, 단색 도형으로 칠하지 않는다.
    /// 재료 아이콘은 ItemDef.icon, 친구는 기본 형태 초상화, 의상은 의상 초상화를 런타임에 읽는다.
    /// </summary>
    public class MetaIcons : ScriptableObject
    {
        [Header("재화 / 메타 보관함")]
        [SerializeField] Sprite _gold;
        [SerializeField] Sprite _gem;
        [SerializeField] Sprite _eventToken;
        [SerializeField] Sprite _quickTicket;
        [SerializeField] Sprite _doubleTicket;
        [SerializeField] Sprite _selectShards;

        [Header("공용")]
        [SerializeField] Sprite _lock;
        [SerializeField] Sprite _star;
        [SerializeField] Sprite _glow;
        [SerializeField] Sprite _sparkle;
        [SerializeField] Sprite _ring;
        [SerializeField] Sprite _circle;
        [SerializeField] Sprite _round;

        [Header("수선 바구니 (idle_basket_0..3)")]
        [SerializeField] Sprite[] _basket = new Sprite[4];

        [Header("수선 일지 / 출석부")]
        [SerializeField] Sprite _chestClosed;
        [SerializeField] Sprite _chestOpen;
        [SerializeField] Sprite _stamp;
        [SerializeField] Sprite _journal;
        [SerializeField] Sprite _attendance;
        [SerializeField] Sprite _quest;

        [Header("기능 아이콘 (FeatureId 값 순서)")]
        [SerializeField] Sprite[] _features = Array.Empty<Sprite>();

        [Header("공용 아트 조각 (빌더가 Binder로 채움)")]
        [Tooltip("아이콘이 없는 보상의 대신 그림 (Icons/reward_generic, 선물 상자)")]
        [SerializeField] Sprite _rewardGeneric;
        [Tooltip("빨간 알림 점 (UI/ui_badge_red)")]
        [SerializeField] Sprite _badge;
        [Tooltip("아이콘·초상화 뒤 밝은 받침 (UI/ui_plate_round)")]
        [SerializeField] Sprite _plate;
        [Tooltip("어두운 메달 받침 (UI/ui_medallion_dark)")]
        [SerializeField] Sprite _medallion;
        [Tooltip("체크 표시 (Icons/check)")]
        [SerializeField] Sprite _check;
        [Tooltip("\"완료\" 도장 고리 (UI/ui_stamp_ring)")]
        [SerializeField] Sprite _stampRing;
        [Tooltip("달 (UI/moon)")]
        [SerializeField] Sprite _moon;
        [Tooltip("고요한 밤 수선점 (Icons/stage_calm)")]
        [SerializeField] Sprite _calm;

        public Sprite Lock => _lock;
        public Sprite Star => _star;
        public Sprite Glow => _glow;
        public Sprite Sparkle => _sparkle;
        public Sprite Ring => _ring;
        /// <summary>자리표시 원 (아트가 없을 때)</summary>
        public Sprite Circle => _circle;
        /// <summary>자리표시 둥근 사각형 (아트가 없을 때)</summary>
        public Sprite Round => _round;
        public Sprite ChestClosed => _chestClosed;
        public Sprite ChestOpen => _chestOpen;
        public Sprite Stamp => _stamp;
        public Sprite Journal => _journal;
        public Sprite Attendance => _attendance;
        public Sprite Quest => _quest;
        public Sprite QuickTicket => _quickTicket;
        public Sprite DoubleTicket => _doubleTicket;
        /// <summary>아이콘이 없는 보상의 대신 그림 (선물 상자). 흰색 그대로 쓴다</summary>
        public Sprite RewardGeneric => _rewardGeneric;
        /// <summary>빨간 알림 점</summary>
        public Sprite Badge => _badge;
        /// <summary>아이콘·초상화 뒤 밝은 둥근 받침</summary>
        public Sprite Plate => _plate;
        /// <summary>어두운 메달 받침 (개방 카드 아이콘 자리)</summary>
        public Sprite Medallion => _medallion;
        public Sprite Check => _check;
        /// <summary>"완료" 도장 고리 (가운데는 비어 글자를 얹는다)</summary>
        public Sprite StampRing => _stampRing;
        public Sprite Moon => _moon;
        /// <summary>고요한 밤 수선점 그림</summary>
        public Sprite Calm => _calm;

        public Sprite Currency(CurrencyType type) => type switch
        {
            CurrencyType.Gold => _gold,
            CurrencyType.Gem => _gem,
            _ => _eventToken,
        };

        /// <summary>바구니 그림 단계 0~3 (없으면 null)</summary>
        public Sprite Basket(int stage)
        {
            if (_basket == null || _basket.Length == 0) return null;
            return _basket[Mathf.Clamp(stage, 0, _basket.Length - 1)];
        }

        /// <summary>기능 아이콘 (가이드 퀘스트 줄 / 개방 카드 / 미션 행). 없으면 null</summary>
        public Sprite Feature(FeatureId feature)
        {
            int index = (int)feature;
            return _features != null && index >= 0 && index < _features.Length ? _features[index] : null;
        }

        /// <summary>보상 아이콘 (재료는 ItemDef.icon, 친구는 기본 형태 초상화). 없으면 null</summary>
        public Sprite Reward(MetaReward reward)
        {
            if (reward == null) return null;
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    return Currency(reward.currency);
                case MetaRewardType.GoldMinutes:
                    return _gold;
                case MetaRewardType.Item:
                    return Game.Items != null ? Game.Items.Get(reward.itemId)?.icon : null;
                case MetaRewardType.QuickIdleTicket:
                    return _quickTicket;
                case MetaRewardType.DoubleTicket:
                    return _doubleTicket;
                case MetaRewardType.SelectShards:
                    return _selectShards;
                case MetaRewardType.Lineage:
                    var lineage = Game.Characters?.Db.GetLineage(reward.itemId);
                    return lineage != null ? Game.Characters.GetPortrait(lineage.baseFormId) : null;
                case MetaRewardType.Costume:
                    return Game.Characters?.Db.GetCostume(reward.itemId)?.portrait;
                default:
                    return null;
            }
        }

        /// <summary>에디터 빌더용: 칸을 채운다</summary>
        public void Fill(Sprite gold, Sprite gem, Sprite eventToken, Sprite quickTicket, Sprite doubleTicket, Sprite selectShards,
            Sprite lockIcon, Sprite star, Sprite glow, Sprite sparkle, Sprite ring, Sprite circle, Sprite round, Sprite[] basket,
            Sprite chestClosed, Sprite chestOpen, Sprite stamp, Sprite journal, Sprite attendance, Sprite quest, Sprite[] features)
        {
            _gold = gold;
            _gem = gem;
            _eventToken = eventToken;
            _quickTicket = quickTicket;
            _doubleTicket = doubleTicket;
            _selectShards = selectShards;
            _lock = lockIcon;
            _star = star;
            _glow = glow;
            _sparkle = sparkle;
            _ring = ring;
            _circle = circle;
            _round = round;
            _basket = basket ?? new Sprite[4];
            _chestClosed = chestClosed;
            _chestOpen = chestOpen;
            _stamp = stamp;
            _journal = journal;
            _attendance = attendance;
            _quest = quest;
            _features = features ?? Array.Empty<Sprite>();
        }
    }
}
