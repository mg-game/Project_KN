using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 키별 제한 (순수 C#, 시각은 부르는 쪽이 준다 — AUDIO-SPEC §3.5): 같은 키는 minInterval 안에 한 번, 동시에 maxVoices개까지.
    /// 막힌 시도는 기록하지 않는다. UI 키는 예전 UISfxGate가 맡고 (FeedbackGateTests), 이 게이트는 전투·환경음·징글 쪽이다.
    /// </summary>
    public sealed class AudioGate
    {
        /// <summary>키 하나가 동시에 쓰는 칸 수 상한</summary>
        public const int SlotsPerKey = 6;

        readonly double[] _lastPlay;
        readonly double[] _busyUntil;

        public AudioGate(int keyCount)
        {
            int count = Math.Max(1, keyCount);
            _lastPlay = new double[count];
            _busyUntil = new double[count * SlotsPerKey];
            Reset();
        }

        public int KeyCount => _lastPlay.Length;

        /// <summary>지금(now) 그 키를 울려도 되면 true (그리고 시각·칸을 기록). duration = 실제로 울리는 길이 (클립 길이 ÷ 음높이)</summary>
        public bool TryPass(int key, float minInterval, int maxVoices, double now, float duration)
        {
            if (key < 0 || key >= _lastPlay.Length) return false;
            if (now - _lastPlay[key] < minInterval - 1e-6) return false;
            int slots = Mathf.Clamp(maxVoices, 1, SlotsPerKey);
            int slot = -1;
            int baseIndex = key * SlotsPerKey;
            for (int i = 0; i < slots; i++)
            {
                if (_busyUntil[baseIndex + i] <= now)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return false;
            _lastPlay[key] = now;
            _busyUntil[baseIndex + slot] = now + Math.Max(0f, duration);
            return true;
        }

        /// <summary>그 키를 마지막으로 울린 시각 (없으면 −∞)</summary>
        public double LastPlay(int key) => key >= 0 && key < _lastPlay.Length ? _lastPlay[key] : double.NegativeInfinity;

        public void Reset()
        {
            for (int i = 0; i < _lastPlay.Length; i++) _lastPlay[i] = double.NegativeInfinity;
            for (int i = 0; i < _busyUntil.Length; i++) _busyUntil[i] = double.NegativeInfinity;
        }

        /// <summary>음높이 흔들기: 1 ± range (random01 0~1, 0.5 = 1)</summary>
        public static float PitchJitter(float range, float random01) => 1f + (Mathf.Clamp01(random01) * 2f - 1f) * Mathf.Max(0f, range);

        /// <summary>볼륨 흔들기: ± rangeDb (random01 0~1)</summary>
        public static float GainJitterDb(float rangeDb, float random01) => (Mathf.Clamp01(random01) * 2f - 1f) * Mathf.Max(0f, rangeDb);

        /// <summary>dB → 배율</summary>
        public static float DbToGain(float db) => db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);

        /// <summary>배율 → dB (0이면 −80)</summary>
        public static float GainToDb(float gain) => gain <= 0.0001f ? -80f : 20f * Mathf.Log10(gain);
    }

    /// <summary>
    /// 범주 예산 (토큰 통, 계속 채워짐): 초당 rate개, 최대 burst개 모임. rateScale = 2배속 1.3 · 잠 0.5 (§3.5).
    /// 값 형식이라 배열에 넣어 쓴다 (할당 없음)
    /// </summary>
    public struct TokenBucket
    {
        public float Rate;
        public float Burst;
        float _tokens;
        double _last;
        bool _ready;

        public TokenBucket(float rate, float burst)
        {
            Rate = Mathf.Max(0f, rate);
            Burst = Mathf.Max(1f, burst);
            _tokens = Burst;
            _last = 0;
            _ready = false;
        }

        /// <summary>지금 남은 토큰 (채운 뒤)</summary>
        public float Peek(double now, float rateScale = 1f)
        {
            Refill(now, rateScale);
            return _tokens;
        }

        /// <summary>토큰 하나를 쓴다. 비었으면 false (쓰지 않음)</summary>
        public bool TryTake(double now, float rateScale = 1f)
        {
            Refill(now, rateScale);
            if (_tokens < 1f) return false;
            _tokens -= 1f;
            return true;
        }

        void Refill(double now, float rateScale)
        {
            if (!_ready)
            {
                _ready = true;
                _tokens = Burst;
                _last = now;
                return;
            }
            double dt = now - _last;
            _last = now;
            if (dt > 0) _tokens = Mathf.Min(Burst, _tokens + (float)dt * Rate * Mathf.Max(0f, rateScale));
        }

        public void Reset()
        {
            _ready = false;
            _tokens = Burst;
        }
    }

    /// <summary>섞어 뽑기 상태 (키마다 2바이트: 마지막 · 그 전, 0 = 없음 · 그 밖 = 테이크 인덱스 + 1)</summary>
    public struct ShuffleState
    {
        public byte Last;
        public byte Prev;
    }

    /// <summary>
    /// 섞어 뽑기 (§3.5): 테이크가 2개 이상이면 방금 것을 다시 뽑지 않고, 4개 이상이면 방금 두 개를 피한다. random01로 고른다 (결정적, 테스트 가능)
    /// </summary>
    public static class ShuffleBag
    {
        /// <summary>count개 중 하나 (0 ~ count−1, count가 0이면 −1)</summary>
        public static int Next(ref ShuffleState state, int count, float random01)
        {
            if (count <= 0) return -1;
            if (count == 1)
            {
                state.Prev = state.Last;
                state.Last = 1;
                return 0;
            }
            int a = state.Last - 1;
            int b = count >= 4 ? state.Prev - 1 : -1;
            if (a >= count) a = -1;
            if (b >= count || b == a) b = -1;
            int allowed = count - (a >= 0 ? 1 : 0) - (b >= 0 ? 1 : 0);
            if (allowed <= 0)
            {
                a = -1;
                b = -1;
                allowed = count;
            }
            int pick = Mathf.Clamp((int)(Mathf.Clamp01(random01) * allowed), 0, allowed - 1);
            int chosen = 0;
            for (int i = 0; i < count; i++)
            {
                if (i == a || i == b) continue;
                if (pick == 0)
                {
                    chosen = i;
                    break;
                }
                pick--;
            }
            state.Prev = state.Last;
            state.Last = (byte)(chosen + 1);
            return chosen;
        }
    }

    /// <summary>사다리 진행 상태 (키 하나 또는 소스 하나)</summary>
    public struct LadderState
    {
        public int Step;
        public double Last;
        public bool Started;

        public void Reset()
        {
            Step = 0;
            Last = 0;
            Started = false;
        }
    }

    /// <summary>
    /// 조율 사다리 진행 (§1.3, §3.5): Window 안에 이어지면 한 칸 오르고 맨 위는 유지, Reset보다 오래 조용하면 처음 칸.
    /// 그 사이(Window &lt; 간격 ≤ Reset)는 그 칸 그대로. 근음이 여럿인 키는 가장 가까운(반음 기준) 테이크를 고른다
    /// </summary>
    public static class PitchLadder
    {
        /// <summary>이번 음의 칸 (상태를 갱신)</summary>
        public static int Advance(ref LadderState state, in PitchLadderDef def, double now)
        {
            int top = Math.Max(0, def.Count - 1);
            int step;
            double gap = now - state.Last;
            if (!state.Started || gap > def.Reset) step = 0;
            else if (gap <= def.Window) step = Math.Min(top, state.Step + 1);
            else step = Math.Min(top, state.Step);
            state.Step = step;
            state.Last = now;
            state.Started = true;
            return step;
        }

        /// <summary>목표 음(Hz)에 가장 가까운 근음 (반음 거리) — 그 근음의 반음 거리를 돌려준다 (없으면 float.MaxValue)</summary>
        public static float NearestRootDistance(float targetHz, float[] rootHz, int count)
        {
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!(rootHz[i] > 0f)) continue;
                float d = Mathf.Abs(AudioCatalog.Semitones(rootHz[i], targetHz));
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// 목표 음에 가장 가까운 근음을 가진 테이크 인덱스 (같은 근음이 여럿이면 random01로 고름). 근음이 하나도 없으면 −1
        /// </summary>
        public static int NearestTake(float targetHz, float[] rootHz, int count, float random01)
        {
            float best = NearestRootDistance(targetHz, rootHz, count);
            if (best == float.MaxValue) return -1;
            int ties = 0;
            for (int i = 0; i < count; i++)
            {
                if (rootHz[i] > 0f && Mathf.Abs(Mathf.Abs(AudioCatalog.Semitones(rootHz[i], targetHz)) - best) < 0.25f) ties++;
            }
            int pick = Mathf.Clamp((int)(Mathf.Clamp01(random01) * ties), 0, Math.Max(0, ties - 1));
            for (int i = 0; i < count; i++)
            {
                if (!(rootHz[i] > 0f) || Mathf.Abs(Mathf.Abs(AudioCatalog.Semitones(rootHz[i], targetHz)) - best) >= 0.25f) continue;
                if (pick == 0) return i;
                pick--;
            }
            return -1;
        }

        /// <summary>목표 음을 내는 음높이 배율 (= 목표 / 근음). 근음이 없으면 1</summary>
        public static float PitchFor(float targetHz, float rootHz) => targetHz > 0f && rootHz > 0f ? targetHz / rootHz : 1f;
    }
}
