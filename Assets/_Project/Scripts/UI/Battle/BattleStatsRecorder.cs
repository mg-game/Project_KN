using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 기록 (CR-03, SPEC4 §7.4 — 순수 C#): BattleView가 밝혀낸 출처(기본 공격 → 지킴이 칸, 잔향이 기본 공격 → 잔향이, 시전 → 시전자)로
    /// 친구마다 피해 · 회복 · 보호막 · 처치를 더한다. 친구는 형태 ID로 묶는다 (같은 형태의 잔향이들은 하나로).
    /// 저장: 1초짜리 칸 60개 고리(지난 60초) × 친구 16명 + 이번 스테이지 합 (StageChanged에 비운다). 배열은 고정 — 기록할 때 할당 없음.
    /// 16명이 넘치면 (지난 60초 합 + 이번 스테이지 합)이 가장 작은 친구를 빼고 그 자리에 넣는다 — 같으면 가장 오래 기록이 없던 친구
    /// (스테이지가 바뀌어 합이 모두 0이어도 지금 싸우는 친구 대신 편성에서 빠진 친구가 먼저 빠진다)
    /// </summary>
    public sealed class BattleStatsRecorder
    {
        public const int MaxUnits = 16;
        public const int Buckets = 60;

        /// <summary>한 친구의 합 (Fill이 채운다)</summary>
        public struct UnitTotals
        {
            public string FormId;
            public bool IsEcho;
            public double Damage;
            public double Heal;
            public double Shield;
            public int Kills;

            public double Score => Damage + Heal + Shield;
        }

        readonly string[] _forms = new string[MaxUnits];
        readonly bool[] _echo = new bool[MaxUnits];
        readonly double[] _stDamage = new double[MaxUnits];
        readonly double[] _stHeal = new double[MaxUnits];
        readonly double[] _stShield = new double[MaxUnits];
        readonly int[] _stKills = new int[MaxUnits];
        readonly float[] _bDamage = new float[Buckets * MaxUnits];
        readonly float[] _bHeal = new float[Buckets * MaxUnits];
        readonly float[] _bShield = new float[Buckets * MaxUnits];
        readonly int[] _bKills = new int[Buckets * MaxUnits];
        /// <summary>칸마다 담고 있는 절대 초 (−1 = 빔)</summary>
        readonly long[] _bucketSecond = new long[Buckets];
        /// <summary>친구마다 마지막으로 기록한 절대 초 (넘칠 때 같은 점수면 가장 오래된 친구를 뺀다)</summary>
        readonly long[] _lastSecond = new long[MaxUnits];

        public BattleStatsRecorder() => ResetAll();

        /// <summary>기록이 있는 친구 수</summary>
        public int UnitCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxUnits; i++) if (_forms[i] != null) n++;
                return n;
            }
        }

        /// <summary>그 형태의 칸이 있는지 (넘쳐서 빠졌으면 false)</summary>
        public bool Contains(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return false;
            for (int i = 0; i < MaxUnits; i++)
            {
                if (string.Equals(_forms[i], formId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>이번 스테이지 합만 비운다 (StageChanged — 지난 60초 고리는 이어진다)</summary>
        public void ResetStage()
        {
            Array.Clear(_stDamage, 0, MaxUnits);
            Array.Clear(_stHeal, 0, MaxUnits);
            Array.Clear(_stShield, 0, MaxUnits);
            Array.Clear(_stKills, 0, MaxUnits);
        }

        /// <summary>모두 비운다 (새 전투 화면)</summary>
        public void ResetAll()
        {
            ResetStage();
            Array.Clear(_forms, 0, MaxUnits);
            Array.Clear(_echo, 0, MaxUnits);
            Array.Clear(_bDamage, 0, _bDamage.Length);
            Array.Clear(_bHeal, 0, _bHeal.Length);
            Array.Clear(_bShield, 0, _bShield.Length);
            Array.Clear(_bKills, 0, _bKills.Length);
            for (int i = 0; i < Buckets; i++) _bucketSecond[i] = -1;
            for (int i = 0; i < MaxUnits; i++) _lastSecond[i] = long.MinValue;
        }

        public void AddDamage(string formId, bool echo, float amount, float now) => Add(formId, echo, now, amount, 0f, 0f, 0);
        public void AddHeal(string formId, bool echo, float amount, float now) => Add(formId, echo, now, 0f, amount, 0f, 0);
        public void AddShield(string formId, bool echo, float amount, float now) => Add(formId, echo, now, 0f, 0f, amount, 0);
        public void AddKill(string formId, bool echo, float now) => Add(formId, echo, now, 0f, 0f, 0f, 1);

        void Add(string formId, bool echo, float now, float damage, float heal, float shield, int kills)
        {
            if (string.IsNullOrEmpty(formId) || float.IsNaN(now)) return;
            damage = Safe(damage);
            heal = Safe(heal);
            shield = Safe(shield);
            if (damage <= 0f && heal <= 0f && shield <= 0f && kills <= 0) return;
            long second = (long)Math.Floor(now);
            int unit = UnitIndex(formId, echo, damage + heal + shield, second);
            if (unit < 0) return;
            _lastSecond[unit] = second;
            _stDamage[unit] += damage;
            _stHeal[unit] += heal;
            _stShield[unit] += shield;
            _stKills[unit] += kills;
            int b = Bucket(now);
            int k = b * MaxUnits + unit;
            _bDamage[k] += damage;
            _bHeal[k] += heal;
            _bShield[k] += shield;
            _bKills[k] += kills;
        }

        static float Safe(float v) => v > 0f && !float.IsInfinity(v) ? v : 0f;

        /// <summary>
        /// 형태의 칸 (없으면 빈 칸, 꽉 찼으면 (지난 60초 합 + 이번 스테이지 합)이 가장 작은 친구 자리 — 같으면 마지막 기록이 가장 오래된 친구,
        /// 그 친구가 이번 몫보다 크면 −1). second = 지금 절대 초
        /// </summary>
        int UnitIndex(string formId, bool echo, float amount, long second)
        {
            int free = -1;
            for (int i = 0; i < MaxUnits; i++)
            {
                if (_forms[i] == null)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (string.Equals(_forms[i], formId, StringComparison.Ordinal)) return i;
            }
            if (free < 0)
            {
                double min = double.MaxValue;
                long oldest = long.MaxValue;
                for (int i = 0; i < MaxUnits; i++)
                {
                    double score = _stDamage[i] + _stHeal[i] + _stShield[i] + WindowScore(i, second);
                    if (score < min || (score <= min && _lastSecond[i] < oldest))
                    {
                        min = score;
                        oldest = _lastSecond[i];
                        free = i;
                    }
                }
                if (free < 0 || min > amount) return -1;
                ClearUnit(free);
            }
            _forms[free] = formId;
            _echo[free] = echo;
            return free;
        }

        /// <summary>그 친구의 지난 60초 피해 + 회복 + 보호막 (넘칠 때만 — 60칸을 한 번 돈다)</summary>
        double WindowScore(int unit, long second)
        {
            double sum = 0d;
            for (int b = 0; b < Buckets; b++)
            {
                long s = _bucketSecond[b];
                if (s < 0 || s > second || second - s >= Buckets) continue;
                int k = b * MaxUnits + unit;
                sum += _bDamage[k] + _bHeal[k] + _bShield[k];
            }
            return sum;
        }

        void ClearUnit(int unit)
        {
            _stDamage[unit] = _stHeal[unit] = _stShield[unit] = 0d;
            _stKills[unit] = 0;
            for (int b = 0; b < Buckets; b++)
            {
                int k = b * MaxUnits + unit;
                _bDamage[k] = _bHeal[k] = _bShield[k] = 0f;
                _bKills[k] = 0;
            }
        }

        /// <summary>지금 초의 칸 (다른 초를 담고 있었으면 비우고 쓴다)</summary>
        int Bucket(float now)
        {
            long second = (long)Math.Floor(now);
            int b = (int)(((second % Buckets) + Buckets) % Buckets);
            if (_bucketSecond[b] == second) return b;
            _bucketSecond[b] = second;
            int start = b * MaxUnits;
            for (int i = start; i < start + MaxUnits; i++)
            {
                _bDamage[i] = _bHeal[i] = _bShield[i] = 0f;
                _bKills[i] = 0;
            }
            return b;
        }

        /// <summary>
        /// 친구별 합을 into에 채운다 (비우고 피해 많은 순, max명까지). window = 지난 60초 (아니면 이번 스테이지). 피해 · 회복 · 보호막 · 처치가 모두 0인 친구는 빠진다.
        /// 창이 열려 있는 동안 1초에 한 번 부른다 (into는 부르는 쪽 목록)
        /// </summary>
        public int Fill(List<UnitTotals> into, bool window, float now, int max = 11)
        {
            if (into == null) return 0;
            into.Clear();
            long second = (long)Math.Floor(now);
            for (int u = 0; u < MaxUnits; u++)
            {
                if (_forms[u] == null) continue;
                var t = new UnitTotals { FormId = _forms[u], IsEcho = _echo[u] };
                if (window)
                {
                    for (int b = 0; b < Buckets; b++)
                    {
                        long s = _bucketSecond[b];
                        if (s < 0 || s > second || second - s >= Buckets) continue;
                        int k = b * MaxUnits + u;
                        t.Damage += _bDamage[k];
                        t.Heal += _bHeal[k];
                        t.Shield += _bShield[k];
                        t.Kills += _bKills[k];
                    }
                }
                else
                {
                    t.Damage = _stDamage[u];
                    t.Heal = _stHeal[u];
                    t.Shield = _stShield[u];
                    t.Kills = _stKills[u];
                }
                if (t.Score <= 0d && t.Kills <= 0) continue;
                into.Add(t);
            }
            into.Sort(ByDamage);
            if (into.Count > max && max >= 0) into.RemoveRange(max, into.Count - max);
            return into.Count;
        }

        static readonly Comparison<UnitTotals> ByDamage = (a, b) =>
        {
            int c = b.Damage.CompareTo(a.Damage);
            return c != 0 ? c : b.Score.CompareTo(a.Score);
        };
    }

    /// <summary>전투 기록이 보는 피격 맥락 (BattleView의 연출 맥락을 옮긴 것)</summary>
    public enum HitCreditContext : byte
    {
        None,
        /// <summary>지킴이 기본 공격 (GuardianAttack 뒤)</summary>
        Basic,
        /// <summary>잔향이 기본 공격 (EchoAttack 뒤 — 피격 SourceId는 −1이라 대상 ID로 맞춘다)</summary>
        EchoBasic,
        /// <summary>스킬 시전 (SkillCast 뒤)</summary>
        Cast,
        /// <summary>불 밝히기 (LampLit 뒤 — 누구 몫도 아니다)</summary>
        Lamp,
    }

    /// <summary>
    /// 전투 기록(CR-03)의 피격 주인 찾기 (순수 C#, 고정 배열 — 할당 없음):
    ///   · EnemyHit의 SourceId ≥ 0 → 그 지킴이 칸 (시뮬레이터는 지킴이 기본 공격일 때만 칸을 싣는다);
    ///   · 맥락을 연 이벤트와 같은 시뮬레이터 진행(sameStep — 2배속이면 한 프레임에 진행이 둘)일 때만 맥락을 믿는다:
    ///     잔향이 기본 공격은 그 대상일 때만 그 잔향이, 시전은 시전자, 불 밝히기는 아무에게도;
    ///   · 그 밖(지속 피해 틱 — 진행 맨 앞 UpdateEnemies에서 SourceId −1로 온다)은 그 적에게 불을 붙인 친구 (StatusApplied(DamageOverTime)의 출처, 마지막으로 붙인 친구).
    ///     이벤트 없는 패시브 오라의 지속 피해 · 피해는 출처를 알 수 없어 기록하지 않는다
    /// </summary>
    public sealed class HitCredit
    {
        /// <summary>불붙은 적을 기억하는 칸 수 (넘치면 가장 오래된 칸부터 덮는다)</summary>
        public const int BurnSlots = 32;

        readonly int[] _burnEnemy = new int[BurnSlots];
        readonly int[] _burnSource = new int[BurnSlots];
        int _cursor;

        public HitCredit() => Clear();

        /// <summary>모두 잊는다 (새 전투 — 적 ID는 Start마다 1부터 다시 센다)</summary>
        public void Clear()
        {
            for (int i = 0; i < BurnSlots; i++)
            {
                _burnEnemy[i] = -1;
                _burnSource[i] = -1;
            }
            _cursor = 0;
        }

        /// <summary>기억 중인 불붙은 적 수</summary>
        public int BurnCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < BurnSlots; i++) if (_burnEnemy[i] >= 0) n++;
                return n;
            }
        }

        /// <summary>적에게 불(지속 피해)을 붙인 친구를 적는다 (StatusApplied, Value = DamageOverTime). 같은 적이면 덮는다</summary>
        public void NoteBurn(int enemyId, int sourceId)
        {
            if (enemyId < 0 || sourceId < 0) return;
            int free = -1;
            for (int i = 0; i < BurnSlots; i++)
            {
                if (_burnEnemy[i] == enemyId)
                {
                    _burnSource[i] = sourceId;
                    return;
                }
                if (free < 0 && _burnEnemy[i] < 0) free = i;
            }
            if (free < 0)
            {
                free = _cursor;
                _cursor = (_cursor + 1) % BurnSlots;
            }
            _burnEnemy[free] = enemyId;
            _burnSource[free] = sourceId;
        }

        /// <summary>그 적에게 불을 붙인 친구 (없으면 false)</summary>
        public bool TryGetBurn(int enemyId, out int sourceId)
        {
            sourceId = -1;
            if (enemyId < 0) return false;
            for (int i = 0; i < BurnSlots; i++)
            {
                if (_burnEnemy[i] != enemyId) continue;
                sourceId = _burnSource[i];
                return true;
            }
            return false;
        }

        /// <summary>그 적을 잊는다 (처치 · 불이 꺼짐)</summary>
        public void Forget(int enemyId)
        {
            if (enemyId < 0) return;
            for (int i = 0; i < BurnSlots; i++)
            {
                if (_burnEnemy[i] != enemyId) continue;
                _burnEnemy[i] = -1;
                _burnSource[i] = -1;
            }
        }

        /// <summary>
        /// 피격(EnemyHit) 하나의 주인 출처 ID (지킴이 칸 0~4 · 잔향이 ID — −1 = 아무에게도 올리지 않는다). burned = 불붙은 적 기록으로 찾았는지
        /// </summary>
        public int Resolve(int hitSource, int targetId, HitCreditContext context, bool sameStep, int echoSource, int echoTarget, int castSource,
            out bool burned)
        {
            burned = false;
            if (hitSource >= 0) return hitSource;
            if (sameStep)
            {
                switch (context)
                {
                    case HitCreditContext.EchoBasic:
                        if (targetId == echoTarget) return echoSource;
                        break;
                    case HitCreditContext.Cast:
                        return castSource;
                    case HitCreditContext.Lamp:
                        return -1;
                }
            }
            if (!TryGetBurn(targetId, out int burner)) return -1;
            burned = true;
            return burner;
        }
    }
}
