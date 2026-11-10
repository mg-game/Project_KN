using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 시뮬레이터의 잔향이 목록을 EchoView로 맞춰 그린다 (ID 기준 풀링).
    /// 사라지는 잔향이는 빛으로 풀려 수선점으로 흘러 들어가는 연출을 거친 뒤 풀로 돌아간다.
    /// </summary>
    public class EchoViewPool : MonoBehaviour
    {
        [SerializeField] EchoView _template;
        [SerializeField] RectTransform _layer;
        [SerializeField] BattleFx _fx;
        [SerializeField] ShopView _shop;
        [SerializeField] int _prewarm = 10;

        readonly Dictionary<int, EchoView> _active = new();
        readonly List<EchoView> _free = new();
        readonly List<EchoView> _dissolving = new();
        readonly List<int> _stale = new();
        int _stamp;
        bool _prepared;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_prepared) return;
            _prepared = true;
            _template.gameObject.SetActive(false);
            for (int i = 0; i < _prewarm; i++) _free.Add(Create());
        }

        /// <summary>빛으로 풀려 수선점으로 흘러가는 동안 보이는 몫</summary>
        const int DissolveSlack = 4;
        bool _toppedUp;

        /// <summary>
        /// 첫 Sync에 한 번: 덱 잔향이 최대(maxEchoes) + 토큰 최대(maxTokens) + DissolveSlack까지 미리 만든다 (perf PF-2 F5 — 장면 값 10 &lt; 12 + 6).
        /// 게임이 준비되지 않았으면(시험장 · 편집 모드) 직렬화 값 그대로
        /// </summary>
        void TopUp()
        {
            if (_toppedUp) return;
            _toppedUp = true;
            var config = Game.IsReady && Game.Battle != null ? Game.Battle.Config : null;
            if (config == null) return;
            int floor = Mathf.Max(0, config.maxEchoes) + Mathf.Max(0, config.maxTokens) + DissolveSlack;
            for (int have = _free.Count + _active.Count + _dissolving.Count; have < floor; have++) _free.Add(Create());
        }

        public bool TryGet(int id, out EchoView view) => _active.TryGetValue(id, out view);

        /// <param name="alpha">고정 진행 사이 보간 비율 (BattleSimulator.RenderAlpha). 1 = 시뮬레이터 자리 그대로 (시험장 대역)</param>
        /// <param name="holdScale">몸 자리를 붙잡을 배율 (EnemyFxCatalog.HoldScale). 음수 = BattleFx.TimeScale (예전 그대로)</param>
        public void Sync(IReadOnlyList<EchoState> echoes, BattleFieldMapper mapper, float dt, float alpha = 1f, float holdScale = -1f)
        {
            Prepare();
            TopUp();
            _stamp++;
            int count = echoes != null ? echoes.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var state = echoes[i];
                if (state == null || !state.Alive) continue;
                if (!_active.TryGetValue(state.Id, out var view) || view.State != state)
                {
                    if (view != null) StartDissolve(view, Vector2.zero, 0);
                    view = Take();
                    view.Bind(state, mapper);
                    _active[state.Id] = view;
                }
                view.SeenStamp = _stamp;
                view.Tick(mapper, dt, _fx, alpha, holdScale);
            }

            _stale.Clear();
            foreach (var pair in _active)
            {
                if (pair.Value.SeenStamp != _stamp) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                var view = _active[_stale[i]];
                _active.Remove(_stale[i]);
                StartDissolve(view, Vector2.zero, 0);
            }

            for (int i = _dissolving.Count - 1; i >= 0; i--)
            {
                if (_dissolving[i].TickDissolve(dt)) continue;
                Return(_dissolving[i]);
                _dissolving.RemoveAt(i);
            }
        }

        /// <summary>
        /// EchoDissolved 이벤트: 그 자리에서 빛으로 풀려 수선점으로 흘러 들어간다.
        /// fieldPosition이 0이면 잔향이 뷰의 현재 위치를 쓴다.
        /// </summary>
        public void Dissolve(int id, Vector2 fieldPosition, int refund)
        {
            if (!_active.TryGetValue(id, out var view)) return;
            _active.Remove(id);
            StartDissolve(view, fieldPosition, refund);
        }

        /// <summary>스테이지가 바뀔 때: 남은 잔향이가 모두 빛으로 풀려 수선점으로 돌아간다</summary>
        public void DissolveAll()
        {
            foreach (var view in _active.Values) StartDissolve(view, Vector2.zero, 0);
            _active.Clear();
        }

        public void ClearAll()
        {
            foreach (var view in _active.Values) Return(view);
            _active.Clear();
            for (int i = 0; i < _dissolving.Count; i++) Return(_dissolving[i]);
            _dissolving.Clear();
        }

        void StartDissolve(EchoView view, Vector2 fieldPosition, int refund)
        {
            view.BeginDissolve();
            _dissolving.Add(view);
            if (_fx == null || _shop == null) return;
            var from = fieldPosition != Vector2.zero ? fieldPosition : view.Rect.anchoredPosition;
            _fx.EchoDissolve(from, _shop.DoorPosition, view.GlowColor, refund);
        }

        EchoView Take()
        {
            if (_free.Count == 0) return Create();
            var view = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            return view;
        }

        void Return(EchoView view)
        {
            view.Release();
            _free.Add(view);
        }

        EchoView Create()
        {
            var view = Instantiate(_template, _layer);
            view.name = "Echo";
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
