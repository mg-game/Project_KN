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

        void Start()
        {
            if (!Game.IsReady) return;
            _battle = Game.Battle;
            _battle.StageEnded += OnStageEnded;
        }

        void OnDestroy()
        {
            if (_battle != null) _battle.StageEnded -= OnStageEnded;
        }

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (!_enabled || outcome != BattleOutcome.Cleared || rewards == null || rewards.Count == 0) return;
            var ui = UIManager.Instance;
            if (ui != null && ui.HasOpenPopup) return;
            if (_source == null || !_source.gameObject.activeInHierarchy) _source = UIFind.Deep(_battleRoot, _sourceName);
            RewardFlyer.Fly(rewards, UIFind.IsShown(_source) ? _source : _battleRoot);
        }
    }
}
