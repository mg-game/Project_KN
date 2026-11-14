using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 스테이지 클리어 보상 날아가기 (FX-04 / UX-01): Battle.StageEnded(Cleared, 보상)를 받으면 전투 화면의 수선점에서
    /// 상단 재화 칩으로 보상 아이콘이 날아간다. 전투 화면 파일(UI/Battle)은 다른 작업 중이라 여기서 따로 붙인다.
    /// 팝업이 열려 있으면 날리지 않는다 (M21, UX-SPEC §2.1.7): 보상은 이미 들어왔고, 어둠 위로 전투 코인이 날면 팝업을 가린다.
    /// 그때는 떠 있는 양도 늘지 않으니 상단 바 숫자가 바로 맞는다. 줄에 쌓았다가 나중에 날리지도 않는다 (지난 보상이 뒤늦게 날지 않게).
    /// 전투 화면 쪽에 같은 연출이 생기면 이 컴포넌트를 끈다 (_enabled).
    /// 실타래 줄 (T10, 결정 2 안 D): 1-1 첫 클리어(유실자 레벨업이 이 클리어로 열림 · 최고 스테이지 2 이하)에서만 같은 프레임에 날리지 않고,
    /// 다음 프레임 이후(개방 처리로 꼬리표가 켜진 뒤) 팝업(개방 카드 등)이 닫힌 뒤 한 번 날린다. 미루는 동안 그 양은 RewardFlyer.Reserve로
    /// 날아오는 양에 먼저 잡아 꼬리표 · 잔액 칩 숫자가 오르내리지 않게 한다. 그 밖의 클리어는 다른 줄과 함께 바로 날린다.
    /// </summary>
    public class StageClearRewardFlyer : MonoBehaviour
    {
        [SerializeField] bool _enabled = true;
        [Tooltip("전투 화면 루트 (Canvas 기준 SafeArea/StageArea/Battle)")]
        [SerializeField] RectTransform _battleRoot;
        [Tooltip("출처: 전투 화면 안의 수선점 (이름으로 찾는다)")]
        [SerializeField] string _sourceName = "Shop";

        BattleSystem _battle;
        RectTransform _source;
        /// <summary>나중에 날릴 실타래 (T10)</summary>
        double _deferredLineageXp;
        /// <summary>실타래를 미룬 프레임 (같은 프레임에는 날리지 않는다)</summary>
        int _deferredFrame = -1;
        readonly List<RewardData> _scratch = new();

        void Start()
        {
            if (!Game.IsReady) return;
            _battle = Game.Battle;
            _battle.StageEnded += OnStageEnded;
        }

        void OnDestroy()
        {
            if (_battle != null) _battle.StageEnded -= OnStageEnded;
            // 날리지 못한 미룬 양은 잡아 둔 것만 푼다 (보상은 이미 들어와 있다)
            if (_deferredLineageXp > 0d) RewardFlyer.ReleaseReserved(CurrencyType.LineageXp, _deferredLineageXp);
            _deferredLineageXp = 0d;
        }

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (!_enabled || outcome != BattleOutcome.Cleared || rewards == null || rewards.Count == 0) return;
            bool deferLineage = IsFirstUnlockClear;
            _scratch.Clear();
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                if (deferLineage && reward.type == RewardType.Currency && reward.currency == CurrencyType.LineageXp && reward.amount > 0)
                {
                    _deferredLineageXp += reward.amount;
                    _deferredFrame = Time.frameCount;
                    RewardFlyer.Reserve(CurrencyType.LineageXp, reward.amount);
                    continue;
                }
                _scratch.Add(reward);
            }
            var ui = UIManager.Instance;
            if (ui != null && ui.HasOpenPopup) return;
            if (_scratch.Count > 0) RewardFlyer.Fly(_scratch, Source);
            _scratch.Clear();
        }

        /// <summary>미룬 실타래: 같은 프레임이 지났고 열린 팝업이 없으면 한 번 날린다 (T10)</summary>
        void Update()
        {
            if (!(_deferredLineageXp > 0d) || Time.frameCount == _deferredFrame) return;
            var ui = UIManager.Instance;
            if (ui != null && ui.HasOpenPopup) return;
            double amount = _deferredLineageXp;
            _deferredLineageXp = 0d;
            RewardFlyer.ReleaseReserved(CurrencyType.LineageXp, amount);
            if (_enabled) RewardFlyer.FlyCurrency(CurrencyType.LineageXp, amount, Source);
        }

        /// <summary>
        /// 유실자 레벨업을 여는 1-1 첫 클리어인지 (T10): 클리어 처리로 최고 스테이지가 2가 됐고(1-1 너머 처음) 개방 처리(꼬리표 켬)가 아직이거나 이 프레임에 열림.
        /// 최고 스테이지 2 이하에서만 참이라 그 뒤 클리어는 미루지 않는다
        /// </summary>
        static bool IsFirstUnlockClear => Game.IsReady && Game.Battle != null && Game.Battle.HighestStage <= 2;

        RectTransform Source
        {
            get
            {
                if (_source == null || !_source.gameObject.activeInHierarchy) _source = UIFind.Deep(_battleRoot, _sourceName);
                return UIFind.IsShown(_source) ? _source : _battleRoot;
            }
        }
    }
}
