using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 시뮬레이터의 적 목록을 EnemyView로 맞춰 그린다 (ID 기준 풀링).
    /// 새 적은 등장 연출(갈래 색 먼지 소용돌이 / 보스는 그림자 웅덩이, 분열로 생긴 적은 원래 자리에서 통 튀어나옴)과 함께 나타나고,
    /// 목록에서 사라진 적은 처치(갈래별 처치 연출, 마지막 일격이 닿는 순간) 또는 수선점 도달(빨려 들어감) 연출 후 풀로 돌아간다.
    /// </summary>
    public class EnemyViewPool : MonoBehaviour
    {
        [SerializeField] EnemyView _template;
        [SerializeField] RectTransform _layer;
        [SerializeField] BattleFx _fx;
        [SerializeField] ShopView _shop;
        [SerializeField] int _prewarm = 24;

        readonly Dictionary<int, EnemyView> _active = new();
        readonly List<EnemyView> _free = new();
        readonly List<EnemyView> _exiting = new();
        readonly List<int> _stale = new();
        /// <summary>분열로 생긴 적 ID → 원래 적이 있던 자리 (처음 그릴 때 거기서 튀어나온다)</summary>
        readonly Dictionary<int, Vector2> _splitOrigins = new();
        int _stamp;
        bool _prepared;

        /// <summary>현재 화면에 살아 있는 적 뷰</summary>
        public Dictionary<int, EnemyView>.ValueCollection Active => _active.Values;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_prepared) return;
            _prepared = true;
            _template.gameObject.SetActive(false);
            // 보스 이름표 자리를 연출이 모은다 (말풍선 · 숫자가 비킨다 — Wave 3 WP-5B)
            if (_fx != null) _fx.BindEnemies(this);
            for (int i = 0; i < _prewarm; i++) _free.Add(Create());
        }

        /// <summary>처치 · 도달 연출로 목록에서 빠진 뒤에도 잠깐 보이는 적 몫</summary>
        const int ExitSlack = 8;
        bool _toppedUp;

        /// <summary>
        /// 첫 Sync에 한 번: 시뮬레이터 최대 적 수(BattleConfig.maxEnemies) + ExitSlack까지 미리 만든다 (perf PF-2 F5 — 장면 값 24는 최대 40보다 작아
        /// 촘촘한 물결 · 분열 · 2배속에서 전투 한가운데 Instantiate가 돌았다). 게임이 준비되지 않았으면(시험장 · 편집 모드) 직렬화 값 그대로
        /// </summary>
        void TopUp()
        {
            if (_toppedUp) return;
            _toppedUp = true;
            var config = Game.IsReady && Game.Battle != null ? Game.Battle.Config : null;
            if (config == null) return;
            int floor = Mathf.Max(0, config.maxEnemies) + ExitSlack;
            for (int have = _free.Count + _active.Count + _exiting.Count; have < floor; have++) _free.Add(Create());
        }

        public bool TryGet(int id, out EnemyView view) => _active.TryGetValue(id, out view);

        /// <param name="dt">연출 시계 (실제 dt × BattleFx.TimeScale) — 전체 정지·느린 화면 동안 적은 몸 자리까지 함께 멈춘다 (EnemyView.Tick)</param>
        /// <param name="alpha">고정 진행 사이 보간 비율 (BattleSimulator.RenderAlpha). 1 = 시뮬레이터 자리 그대로 (시험장)</param>
        /// <param name="holdScale">몸 자리를 붙잡을 배율 (EnemyFxCatalog.HoldScale — 시뮬레이션이 덜 느려진 몫만). 음수 = BattleFx.TimeScale (예전 그대로)</param>
        public void Sync(IReadOnlyList<EnemyState> enemies, BattleFieldMapper mapper, float dt, float alpha = 1f, float holdScale = -1f)
        {
            Prepare();
            TopUp();
            _stamp++;
            float timeScale = holdScale >= 0f ? holdScale : _fx != null ? _fx.TimeScale : dt > 0f ? 1f : 0f;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var state = enemies[i];
                if (state == null || !state.Alive) continue;
                if (!_active.TryGetValue(state.Id, out var view) || view.State != state)
                {
                    if (view != null) StartExit(view);
                    view = Take();
                    view.Bind(state, mapper);
                    _active[state.Id] = view;
                    if (_splitOrigins.TryGetValue(state.Id, out var origin))
                    {
                        _splitOrigins.Remove(state.Id);
                        view.HopFrom(origin);
                    }
                    else if (_fx != null) _fx.EnemySpawn(view.Rect.anchoredPosition, view.Family, view.IsBoss);
                }
                view.SeenStamp = _stamp;
                view.Tick(mapper, dt, timeScale, alpha);
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
                StartExit(view);
            }

            for (int i = _exiting.Count - 1; i >= 0; i--)
            {
                if (_exiting[i].TickExit(dt)) continue;
                Return(_exiting[i]);
                _exiting.RemoveAt(i);
            }
        }

        /// <summary>
        /// 처치 이벤트를 받은 적 표시 (목록에서 빠질 때 '풀려남' 연출). hold = 마지막 일격이 아직 날아오는 중이면 닿기까지 남은 시간.
        /// 실제 보류는 EnemyView.KillDelay (마지막 일격 + 하얀 버팀) — 처치 꽃·보상 단추·연속 처치는 그 순간에 맞춘다
        /// </summary>
        public void MarkKilled(int id, float hold = 0f)
        {
            if (_active.TryGetValue(id, out var view)) view.MarkKilled(hold);
        }

        /// <summary>처치 표시 + 처치한 일격의 등급 (치명타·스킬 처치는 처치 꽃이 터질 때 화면 킥)</summary>
        public void MarkKilled(int id, float hold, HitTier byTier)
        {
            if (_active.TryGetValue(id, out var view)) view.MarkKilled(hold, byTier);
        }

        /// <summary>처치 꽃이 터지기까지 남은 시간 (MarkKilled 뒤 — 없으면 0)</summary>
        public float KillDelay(int id) => _active.TryGetValue(id, out var view) ? view.KillDelay : 0f;

        /// <summary>분열 이벤트: 새 적이 처음 그려질 때 원래 자리(전장 좌표)에서 통 튀어나오게 한다</summary>
        public void RegisterSplit(int newId, Vector2 fieldOrigin)
        {
            if (newId < 0) return;
            if (_splitOrigins.Count > 32) _splitOrigins.Clear();
            _splitOrigins[newId] = fieldOrigin;
        }

        /// <summary>스테이지가 바뀔 때: 처치된 적은 펑, 나머지는 제자리에서 흐려지며 사라진다</summary>
        public void ExitAll()
        {
            _splitOrigins.Clear();
            foreach (var view in _active.Values) StartExit(view, true);
            _active.Clear();
        }

        /// <summary>연출 없이 전부 치운다</summary>
        public void ClearAll()
        {
            foreach (var view in _active.Values) Return(view);
            _active.Clear();
            for (int i = 0; i < _exiting.Count; i++) Return(_exiting[i]);
            _exiting.Clear();
        }

        void StartExit(EnemyView view, bool vanish = false)
        {
            view.BeginExit(_shop != null ? _shop.FieldPosition : Vector2.zero, vanish);
            if (view.WasKilled && _fx != null)
            {
                _fx.EnemyDeath(new EnemyDeathRequest
                {
                    EnemyId = view.State != null && view.State.Def != null ? view.State.Def.id : null,
                    Point = view.HitPoint,
                    Feet = view.Rect.anchoredPosition,
                    BodyColor = view.BodyColor,
                    Boss = view.IsBoss,
                    Delay = view.KillDelay,
                    Shop = _shop != null ? _shop.DoorPosition : Vector2.zero,
                    // 보스 처치 순간의 크림 실루엣·인쇄 어긋남이 이 몸 그림을 따르고, 치명타·스킬 처치는 화면 킥
                    Body = view.Body,
                    KilledBy = view.KilledBy,
                });
            }
            _exiting.Add(view);
        }

        EnemyView Take()
        {
            if (_free.Count == 0) return Create();
            var view = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            return view;
        }

        void Return(EnemyView view)
        {
            view.Release();
            _free.Add(view);
        }

        /// <summary>
        /// 풀과 따로 노는 적 하나 (연출 시험장의 허수아비). parent 아래에 만들고 꺼 둔다 — 부른 쪽이 Bind/Tick/Destroy를 맡는다
        /// </summary>
        public EnemyView CreateLoose(RectTransform parent)
        {
            var view = Instantiate(_template, parent != null ? parent : _layer);
            view.name = "Scarecrow";
            view.KeepOut = _fx;
            view.gameObject.SetActive(false);
            return view;
        }

        EnemyView Create()
        {
            var view = Instantiate(_template, _layer);
            view.name = "Enemy";
            // 이름표 · HP 바가 HUD 밑으로 들어가지 않게 연출에 물어본다 (EnemyView.PlaceStatus)
            view.KeepOut = _fx;
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
