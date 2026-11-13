using UnityEngine;

namespace IdleGame
{
    /// <summary>덕킹 이유 (같은 이유는 다시 걸면 갱신된다 — AUDIO-SPEC §3.10)</summary>
    public static class DuckReason
    {
        public const int External = 0;
        public const int LampLit = 1;
        public const int BossAppear = 2;
        public const int BossKill = 3;
        public const int JingleClear = 4;
        public const int JingleFail = 5;
        public const int GachaHigh = 6;
        public const int JingleCard = 7;
        public const int UiBig = 8;
        public const int CritMicro = 9;
        public const int SkillBig = 10;
        public const int Sleep = 11;
        public const int FocusDimmed = 12;
        public const int FocusHidden = 13;
        public const int Background = 14;
        public const int SlowDrag = 15;
        public const int BossEnrage = 16;
        public const int LampRelease = 17;
        public const int BossMoment = 18;
        public const int Blackout = 19;
        public const int Audition = 20;
    }

    /// <summary>덕킹에서 빠지는 소리 묶음 (비트) — 불 밝히기 소리 · 보스 소리 · 치명타 층</summary>
    public static class DuckExempt
    {
        public const int None = 0;
        public const int Lamp = 1;
        public const int Boss = 2;
        public const int Crit = 4;
    }

    /// <summary>
    /// 버스 하나의 덕킹 묶음 (순수 C#): 이유마다 하나씩, 겹치면 가장 깊은 덕이 이긴다 (곱하지 않는다).
    /// 시간 덕 = 들어감(attack) → 유지(hold) → 풀림(release), 지속 덕(잠 · 시트 집중) = 들어간 뒤 풀어 줄 때까지. 모두 dB로 직선 보간.
    /// exempt 비트가 겹치는 목소리에는 그 덕을 걸지 않는다 (불 밝히기 덕이 불빛 소리를 누르지 않게).
    /// </summary>
    public sealed class DuckStack
    {
        public const int Capacity = 16;

        struct Duck
        {
            public bool Active;
            public int Reason;
            public float Db;
            public float Attack;
            public float Hold;
            public float Release;
            public double Start;
            public bool Sustained;
            public bool Releasing;
            public double ReleaseStart;
            public float ReleaseFrom;
            public int Exempt;
        }

        readonly Duck[] _ducks = new Duck[Capacity];

        /// <summary>시간 덕을 건다 (같은 이유가 이미 있으면 지금 깊이에서 이어 간다 — 다시 0부터 들어가지 않는다)</summary>
        public void Trigger(int reason, float gainDb, float attack, float hold, float release, double now, int exempt = DuckExempt.None)
        {
            if (!(gainDb < 0f)) return;
            int slot = Find(reason, now, out float current);
            if (slot < 0) slot = FreeSlot(now);
            ref var d = ref _ducks[slot];
            float attackTime = Mathf.Max(0f, attack);
            double start = now;
            if (current < 0f)
            {
                float share = Mathf.Clamp01(current / gainDb);
                start = now - attackTime * share;
            }
            d = new Duck
            {
                Active = true,
                Reason = reason,
                Db = gainDb,
                Attack = attackTime,
                Hold = Mathf.Max(0f, hold),
                Release = Mathf.Max(0f, release),
                Start = start,
                Exempt = exempt,
            };
        }

        /// <summary>지속 덕을 켠다 / 깊이를 바꾼다 (풀어 줄 때까지 유지)</summary>
        public void Sustain(int reason, float gainDb, float attack, double now, int exempt = DuckExempt.None)
        {
            if (!(gainDb < 0f))
            {
                Release(reason, 0.1f, now);
                return;
            }
            int slot = Find(reason, now, out float current);
            if (slot >= 0 && _ducks[slot].Sustained && !_ducks[slot].Releasing && Mathf.Approximately(_ducks[slot].Db, gainDb))
            {
                _ducks[slot].Exempt = exempt;
                return;
            }
            if (slot < 0) slot = FreeSlot(now);
            float attackTime = Mathf.Max(0f, attack);
            double start = now;
            if (current < 0f) start = now - attackTime * Mathf.Clamp01(current / gainDb);
            _ducks[slot] = new Duck
            {
                Active = true,
                Reason = reason,
                Db = gainDb,
                Attack = attackTime,
                Start = start,
                Sustained = true,
                Exempt = exempt,
            };
        }

        /// <summary>그 이유의 덕을 release초에 걸쳐 푼다 (지속·시간 덕 모두)</summary>
        public void Release(int reason, float release, double now)
        {
            int slot = Find(reason, now, out float current);
            if (slot < 0) return;
            ref var d = ref _ducks[slot];
            if (d.Releasing) return;
            if (!(current < 0f) || release <= 0f)
            {
                d.Active = false;
                return;
            }
            d.Releasing = true;
            d.ReleaseStart = now;
            d.ReleaseFrom = current;
            d.Release = release;
        }

        /// <summary>그 이유의 덕이 걸려 있는지 (풀리는 중 포함)</summary>
        public bool Has(int reason)
        {
            for (int i = 0; i < _ducks.Length; i++)
            {
                if (_ducks[i].Active && _ducks[i].Reason == reason) return true;
            }
            return false;
        }

        /// <summary>지금 깊이 (dB, ≤ 0): exemptBits와 겹치는 덕은 빼고 가장 깊은 것</summary>
        public float DepthDb(double now, int exemptBits = DuckExempt.None)
        {
            float deepest = 0f;
            for (int i = 0; i < _ducks.Length; i++)
            {
                ref var d = ref _ducks[i];
                if (!d.Active) continue;
                if ((d.Exempt & exemptBits) != 0) continue;
                float level = Level(in d, now);
                if (level < deepest) deepest = level;
            }
            return deepest;
        }

        /// <summary>끝난 덕을 지우고, 아직 움직이거나 걸려 있는 덕이 있으면 true</summary>
        public bool Tick(double now)
        {
            bool any = false;
            for (int i = 0; i < _ducks.Length; i++)
            {
                ref var d = ref _ducks[i];
                if (!d.Active) continue;
                if (Finished(in d, now))
                {
                    d.Active = false;
                    continue;
                }
                any = true;
            }
            return any;
        }

        public void Clear()
        {
            for (int i = 0; i < _ducks.Length; i++) _ducks[i].Active = false;
        }

        int Find(int reason, double now, out float current)
        {
            current = 0f;
            for (int i = 0; i < _ducks.Length; i++)
            {
                if (!_ducks[i].Active || _ducks[i].Reason != reason) continue;
                current = Level(in _ducks[i], now);
                return i;
            }
            return -1;
        }

        int FreeSlot(double now)
        {
            int weakest = 0;
            float weakestLevel = float.MinValue;
            for (int i = 0; i < _ducks.Length; i++)
            {
                if (!_ducks[i].Active || Finished(in _ducks[i], now)) return i;
                float level = Level(in _ducks[i], now);
                if (level > weakestLevel)
                {
                    weakestLevel = level;
                    weakest = i;
                }
            }
            return weakest;
        }

        static float Level(in Duck d, double now)
        {
            return Envelope(d.Db, d.Attack, d.Hold, d.Release, d.Sustained, d.Releasing, d.ReleaseFrom, now - d.Start, now - d.ReleaseStart);
        }

        static bool Finished(in Duck d, double now)
        {
            if (d.Releasing) return now - d.ReleaseStart >= d.Release;
            if (d.Sustained) return false;
            return now - d.Start >= d.Attack + d.Hold + d.Release;
        }

        /// <summary>
        /// 덕 깊이 (dB) — age = 건 뒤 시간, releaseAge = 풀기 시작한 뒤 시간. 들어감은 0 → depth 직선, 유지, 풀림은 depth → 0 직선 (순수 함수, 테스트용)
        /// </summary>
        public static float Envelope(float depthDb, float attack, float hold, float release, bool sustained, bool releasing, float releaseFrom,
            double age, double releaseAge)
        {
            if (releasing)
            {
                if (release <= 0f || releaseAge >= release) return 0f;
                return releaseFrom * (1f - (float)(releaseAge / release));
            }
            if (age < 0) return 0f;
            if (attack > 0f && age < attack) return depthDb * (float)(age / attack);
            if (sustained) return depthDb;
            double t = age - attack;
            if (t < hold) return depthDb;
            t -= hold;
            if (release <= 0f || t >= release) return 0f;
            return depthDb * (1f - (float)(t / release));
        }
    }

    /// <summary>
    /// 저역 통과 요청 묶음 (버스 하나): 잠 1.2 kHz · 시트 집중 6 kHz · 정전 5 kHz — 가장 낮은 값이 이긴다. 로그 주파수로 부드럽게 따라간다.
    /// 요청이 없으면 22 kHz (필터는 꺼 둔다 — §3.14)
    /// </summary>
    public sealed class LowPassStack
    {
        public const float Open = 22000f;
        const int Capacity = 8;
        /// <summary>한 옥타브 움직이는 데 걸리는 시간 (초)</summary>
        const float OctaveTime = 0.12f;

        readonly int[] _reasons = new int[Capacity];
        readonly float[] _cutoffs = new float[Capacity];
        readonly bool[] _active = new bool[Capacity];
        float _current = Open;

        public float Current => _current;
        public bool Enabled => _current < Open - 1f;

        public float Target
        {
            get
            {
                float target = Open;
                for (int i = 0; i < Capacity; i++)
                {
                    if (_active[i] && _cutoffs[i] < target) target = _cutoffs[i];
                }
                return target;
            }
        }

        public void Set(int reason, float cutoffHz)
        {
            int free = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (_active[i] && _reasons[i] == reason)
                {
                    _cutoffs[i] = Mathf.Clamp(cutoffHz, 200f, Open);
                    return;
                }
                if (!_active[i] && free < 0) free = i;
            }
            if (free < 0) return;
            _active[free] = true;
            _reasons[free] = reason;
            _cutoffs[free] = Mathf.Clamp(cutoffHz, 200f, Open);
        }

        public void Clear(int reason)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_active[i] && _reasons[i] == reason) _active[i] = false;
            }
        }

        /// <summary>dt만큼 목표로 다가간다. 아직 움직이면 true</summary>
        public bool Tick(float dt)
        {
            float target = Target;
            if (Mathf.Approximately(_current, target)) return false;
            float from = Mathf.Log(_current, 2f);
            float to = Mathf.Log(target, 2f);
            float step = dt / OctaveTime;
            float next = Mathf.MoveTowards(from, to, step);
            _current = Mathf.Abs(next - to) < 0.001f ? target : Mathf.Pow(2f, next);
            return !Mathf.Approximately(_current, target);
        }

        public void Snap() => _current = Target;

        public void ClearAll()
        {
            for (int i = 0; i < Capacity; i++) _active[i] = false;
            _current = Open;
        }
    }
}
