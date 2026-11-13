using System;

namespace IdleGame
{
    /// <summary>
    /// 친구들의 응원 (PRG-03, SPEC4 §13.2): 튜토리얼 뒤 장마다 한 번 (막혀서 반복 중이면 막힌 스테이지의 장, 아니면 지금 스테이지의 장 —
    /// 마지막으로 받은 장보다 뒤 장에서만, 앞 장으로 돌아가 되풀이하지 못한다),
    /// durationSeconds(600) 동안 공격력 +attackPercent(20)% — 실제 시간(DailyClock.EffectiveUnix)이라 앱을 닫아도 흐른다. 시계 되돌림 중에는 못 쓴다.
    /// 효과는 BattleSystem.TempAttackPercent(Connect에서 한 번 잇는다) + 시작 · 끝에 Battle.RequestUnitRefresh. 전투력 표시(PowerSystem)에는 넣지 않는다.
    /// Changed = 시작 · 끝남 · 쓸 수 있게 됨 (1초마다 본다, 할당 없음).
    /// </summary>
    public class CheerSystem : IMetaModule
    {
        /// <summary>응원이 시작 · 끝남 · 쓸 수 있게 됨</summary>
        public event Action Changed;

        static readonly CheerConfig DefaultConfig = new();

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly BattleSystem _battle;

        bool _connected;
        Func<float> _hook;
        float _timer;
        bool _wasActive;
        bool _couldUse;

        public CheerSystem(PlayerData data, MetaDatabase db, DailyClock clock, BattleSystem battle, TutorialSystem tutorial)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _battle = battle;
        }

        CheerConfig Config => _db != null && _db.cheer != null ? _db.cheer : DefaultConfig;

        CheerSave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.cheer ??= new CheerSave();
                _data.meta.cheer.Sanitize();
                return _data.meta.cheer;
            }
        }

        long NowUnix => _clock != null ? _clock.EffectiveUnix : 0;

        /// <summary>응원을 정하는 장 (막힌 보스가 있으면 그 장, 아니면 지금 스테이지의 장)</summary>
        public int CurrentChapter
        {
            get
            {
                if (_battle == null) return 1;
                int stage = _battle.ChallengeTarget > 0 ? _battle.ChallengeTarget : _battle.Stage;
                return (Math.Max(1, stage) - 1) / 10 + 1;
            }
        }

        /// <summary>
        /// 지금 응원을 받을 수 있는지 (튜토리얼 뒤 · 되돌림 아님 · 켜져 있지 않음 · 마지막으로 받은 장보다 뒤 장).
        /// 장은 앞으로만 센다: 스테이지 목록 · 챕터 지도로 앞 장에 갔다 돌아와도 다시 열리지 않는다 (lastChapter 0 = 아직 없음)
        /// </summary>
        public bool CanUse
        {
            get
            {
                var save = Save;
                var ftue = _data?.meta?.ftue;
                if (save == null || ftue == null || !ftue.done) return false;
                if (_clock != null && _clock.IsTampered) return false;
                if (IsActive) return false;
                return CurrentChapter > save.lastChapter;
            }
        }

        /// <summary>응원이 켜져 있는지</summary>
        public bool IsActive
        {
            get
            {
                var save = Save;
                return save != null && save.endUnix > 0 && NowUnix < save.endUnix;
            }
        }

        /// <summary>남은 시간 (초)</summary>
        public float Remaining
        {
            get
            {
                var save = Save;
                if (save == null) return 0f;
                long left = save.endUnix - NowUnix;
                return left > 0 ? left : 0f;
            }
        }

        /// <summary>응원이 켜져 있으면 공격력 % (BattleSystem.TempAttackPercent가 부름, 할당 없음)</summary>
        public float AttackPercent => IsActive ? Math.Max(0f, Config.attackPercent) : 0f;

        /// <summary>응원을 받는다 (이 장 기록 + 끝 시각, 유닛 다시 만들기). 받을 수 없으면 false</summary>
        public bool TryUse()
        {
            if (!CanUse) return false;
            var save = Save;
            save.lastChapter = CurrentChapter;
            save.endUnix = NowUnix + Math.Max(1, Config.durationSeconds);
            _wasActive = true;
            _couldUse = false;
            RefreshUnits();
            Changed?.Invoke();
            return true;
        }

        // ───────────────────────────── IMetaModule ─────────────────────────────

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            _hook = ResolveAttackPercent;
            if (_battle != null) _battle.TempAttackPercent = _hook;
            _wasActive = IsActive;
            _couldUse = CanUse;
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot) => CheckState();

        public void OnDayChanged(string dayKey)
        {
        }

        /// <summary>1초마다 켜짐 · 쓸 수 있음이 바뀌었는지 본다 (끝나면 유닛 다시 만들기). 할당 없음</summary>
        public void Tick(float unscaledDeltaTime)
        {
            _timer += unscaledDeltaTime;
            if (_timer < 1f) return;
            _timer = 0f;
            CheckState();
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        float ResolveAttackPercent() => AttackPercent;

        void CheckState()
        {
            bool active = IsActive;
            bool canUse = CanUse;
            bool changed = false;
            if (active != _wasActive)
            {
                _wasActive = active;
                RefreshUnits();
                changed = true;
            }
            if (canUse != _couldUse)
            {
                _couldUse = canUse;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        void RefreshUnits()
        {
            try
            {
                _battle?.RequestUnitRefresh();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Cheer] 유닛 다시 만들기 요청 실패: {e.Message}");
            }
        }
    }
}
