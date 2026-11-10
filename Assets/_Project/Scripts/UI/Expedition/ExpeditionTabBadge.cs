using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 원정 탭 버튼의 빨간 점: 받을 수 있는 원정 수 (+ 남은 던전 입장권이 있으면 1).
    /// 원정은 시간이 지나면 완료되므로 1초마다도 확인한다. 탭 버튼(항상 켜져 있음)에 붙인다.
    /// </summary>
    public class ExpeditionTabBadge : MonoBehaviour
    {
        [SerializeField] Badge _badge;

        bool _started;
        float _timer;

        void Start()
        {
            Game.Expedition.Changed += Refresh;
            Game.Dungeon.Changed += Refresh;
            _started = true;
            Refresh();
        }

        void OnDestroy()
        {
            if (!_started || !Game.IsReady) return;
            Game.Expedition.Changed -= Refresh;
            Game.Dungeon.Changed -= Refresh;
        }

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 1f;
            Refresh();
        }

        void Refresh()
        {
            int count = Game.Expedition.CompletedCount + (Game.Dungeon.TicketsLeft > 0 ? 1 : 0);
            _badge.SetCount(count);
        }
    }
}
