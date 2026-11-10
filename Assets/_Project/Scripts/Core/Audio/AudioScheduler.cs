using System;

namespace IdleGame
{
    /// <summary>예약 소리 성질 (비트)</summary>
    [Flags]
    public enum AudioPlayFlags : ushort
    {
        None = 0,
        /// <summary>예측한 순간 (투사체 도착 · 빛 물결 · 처치 꽃) — 출력 지연 보정을 쓴다</summary>
        Predicted = 1 << 0,
        /// <summary>범주 예산을 건너뛴다 (수선점 피격 · 보스 · 불 밝히기 · 징글 — 자기 게이트만)</summary>
        BypassBudget = 1 << 1,
        /// <summary>군중 묶음 대상인 명중</summary>
        Hit = 1 << 2,
        /// <summary>치명타 (묶음에서 가장 중요)</summary>
        Crit = 1 << 3,
        /// <summary>UI 시계 (PlayAfter — 실제 시간)</summary>
        UiClock = 1 << 4,
        /// <summary>환경음 한 방 (빌린 Sfx 목소리, 환경음 볼륨)</summary>
        Emitter = 1 << 5,
        /// <summary>전투 소리 (전장 집중 · 잠 · 스테이지 바뀜 비우기 대상)</summary>
        Battle = 1 << 6,
        /// <summary>군중 묶음이 따로 울리기로 정한 명중</summary>
        Kept = 1 << 7,
        /// <summary>불 밝히기 덕에서 빠짐</summary>
        ExemptLamp = 1 << 8,
        /// <summary>보스 덕에서 빠짐</summary>
        ExemptBoss = 1 << 9,
        /// <summary>치명타 작은 덕에서 빠짐 (치명타 층 자신)</summary>
        ExemptCrit = 1 << 10,
        /// <summary>울리는 순간 다른 Sfx를 60ms −4 dB 누른다 (치명타)</summary>
        MicroDuck = 1 << 11,
        /// <summary>느린 화면 중에도 음높이를 끌지 않는다 (순간 소리 자체)</summary>
        NoDrag = 1 << 12,
    }

    /// <summary>예산 범주 (§3.5 토큰 통)</summary>
    public enum AudioCategory : byte
    {
        None = 0,
        Hit = 1,
        Launch = 2,
        Strike = 3,
        Hurt = 4,
        Death = 5,
        Status = 6,
        Spawn = 7,
    }

    /// <summary>예약된 소리 하나</summary>
    public struct PendingSound
    {
        public bool Active;
        /// <summary>울릴 시각 (연출 시계 — UiClock이면 실제 시계)</summary>
        public double Due;
        public short Key;
        /// <summary>추가 dB (규칙의 −3 dB 같은 것)</summary>
        public float GainDb;
        /// <summary>부른 쪽 볼륨 배율 0~1</summary>
        public float Volume;
        /// <summary>음높이 배율 (흔들기는 울릴 때 더한다)</summary>
        public float Pitch;
        /// <summary>조율 키의 목표 음 (Hz, 0 = 조율 안 함)</summary>
        public float TargetHz;
        public float Pan;
        public AudioPlayFlags Flags;
        public AudioCategory Category;
        public AudioVoiceClass Class;
        /// <summary>군중 묶음 중요도 (치명타 &gt; 보스 대상 &gt; 수선점에 가까운 것)</summary>
        public float Importance;
        /// <summary>적 ID (묶음·통계용, 없으면 −1)</summary>
        public int EnemyId;
        public int Seq;

        public bool Has(AudioPlayFlags flag) => (Flags & flag) != 0;
    }

    /// <summary>
    /// 연출 시계 대기열 (고정 96칸 — AUDIO-SPEC §3.6). 넣기 O(1), 찾기 O(n). 전투 소리는 연출 시계(BattleFx와 같은 전체 정지·느린 화면)로,
    /// UI PlayAfter는 실제 시계로 기다린다. 넘치면 가장 낮은 등급의 가장 오래된 예약을 버린다 (새 소리가 가장 낮으면 새 소리를 버린다).
    /// 실제로 울리는 일(목소리 고르기 · PlayScheduled)은 AudioEngine이 한다.
    /// </summary>
    public sealed class AudioScheduler
    {
        public const int Capacity = 96;

        readonly PendingSound[] _items = new PendingSound[Capacity];
        int _count;
        int _seq;

        /// <summary>연출 시계 (초)</summary>
        public double FxNow { get; private set; }
        /// <summary>실제 시계 (초, Time.unscaledTimeAsDouble)</summary>
        public double UiNow { get; private set; }
        public int Count => _count;
        public PendingSound[] Items => _items;

        public void AdvanceFx(double fxDt)
        {
            if (fxDt > 0) FxNow += fxDt;
        }

        public void SetUiNow(double now) => UiNow = now;

        /// <summary>delay초 뒤 (연출 시계 — UiClock이면 실제 시계)로 예약한다. 넘쳐서 버리면 false</summary>
        public bool Add(PendingSound sound, double delay)
        {
            sound.Due = (sound.Has(AudioPlayFlags.UiClock) ? UiNow : FxNow) + Math.Max(0, delay);
            return Insert(ref sound);
        }

        /// <summary>절대 시각(sound.Due)으로 넣는다</summary>
        public bool Insert(ref PendingSound sound)
        {
            sound.Active = true;
            sound.Seq = ++_seq;
            int slot = -1;
            if (_count < Capacity)
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (_items[i].Active) continue;
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                slot = Victim();
                if (slot < 0 || (int)_items[slot].Class < (int)sound.Class) return false;
                _items[slot].Active = false;
                _count--;
            }
            _items[slot] = sound;
            _count++;
            return true;
        }

        /// <summary>넘칠 때 버릴 칸: 가장 낮은 등급 중 가장 오래된 것</summary>
        int Victim()
        {
            int victim = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_items[i].Active) continue;
                if (victim < 0) { victim = i; continue; }
                int cls = (int)_items[i].Class;
                int best = (int)_items[victim].Class;
                if (cls > best || (cls == best && _items[i].Seq < _items[victim].Seq)) victim = i;
            }
            return victim;
        }

        /// <summary>지금(연출 fxHorizon / 실제 uiHorizon)까지 울릴 예약 중 가장 이른 칸 (없으면 −1). predictedLead = 지연 보정 몫만큼 더 일찍 꺼낸다</summary>
        public int EarliestDue(double fxHorizon, double uiHorizon, double predictedLead)
        {
            int best = -1;
            double bestDue = double.MaxValue;
            for (int i = 0; i < Capacity; i++)
            {
                ref var item = ref _items[i];
                if (!item.Active) continue;
                bool ui = item.Has(AudioPlayFlags.UiClock);
                double horizon = ui ? uiHorizon : fxHorizon + (item.Has(AudioPlayFlags.Predicted) ? predictedLead : 0);
                if (item.Due > horizon) continue;
                // 서로 다른 시계는 남은 시간으로 비교한다
                double remaining = item.Due - (ui ? UiNow : FxNow);
                if (remaining < bestDue)
                {
                    bestDue = remaining;
                    best = i;
                }
            }
            return best;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Capacity || !_items[index].Active) return;
            _items[index].Active = false;
            _count--;
        }

        /// <summary>
        /// [from, to) 연출 시각에 울릴 명중 예약을 indices에 모은다 (Kept는 빼지 않는다). 모은 수를 돌려준다
        /// </summary>
        public int CollectHits(double from, double to, int[] indices)
        {
            int n = 0;
            for (int i = 0; i < Capacity && n < indices.Length; i++)
            {
                ref var item = ref _items[i];
                if (!item.Active || !item.Has(AudioPlayFlags.Hit) || item.Has(AudioPlayFlags.UiClock)) continue;
                if (item.Due < from - 1e-9 || item.Due >= to) continue;
                indices[n++] = i;
            }
            return n;
        }

        /// <summary>
        /// 군중 묶음 결정 (순수 — §5.6): 방금 꺼낸 명중 item과 [item.Due, item.Due + window) 창의 명중이 모두 min개 이상이면
        /// 가장 중요한 2개만 남기고 (대기열에 있으면 Kept 표시) 나머지는 대기열에서 지운다. 묶었으면 true.
        /// itemKept = item이 남는 둘에 드는지, total = 창 안 명중 수 (item 포함), meanDue/meanPan = hit_multi를 놓을 평균, folded = 지운 수 (item 제외)
        /// </summary>
        public bool FoldCrowd(in PendingSound item, double window, int min, int[] scratch, out bool itemKept, out int total, out double meanDue,
            out float meanPan, out int folded)
        {
            itemKept = true;
            folded = 0;
            meanDue = item.Due;
            meanPan = item.Pan;
            int n = CollectHits(item.Due, item.Due + window, scratch);
            total = n + 1;
            if (total < min) return false;
            int bestA = -2;
            int bestB = -2;
            float impA = float.MinValue;
            float impB = float.MinValue;
            Rank(-1, item.Importance, ref bestA, ref impA, ref bestB, ref impB);
            for (int k = 0; k < n; k++) Rank(scratch[k], _items[scratch[k]].Importance, ref bestA, ref impA, ref bestB, ref impB);
            double dueSum = item.Due;
            float panSum = item.Pan;
            for (int k = 0; k < n; k++)
            {
                int index = scratch[k];
                dueSum += _items[index].Due;
                panSum += _items[index].Pan;
                if (index == bestA || index == bestB)
                {
                    _items[index].Flags |= AudioPlayFlags.Kept;
                    continue;
                }
                RemoveAt(index);
                folded++;
            }
            meanDue = dueSum / total;
            meanPan = panSum / total;
            itemKept = bestA == -1 || bestB == -1;
            return true;
        }

        static void Rank(int index, float importance, ref int bestA, ref float impA, ref int bestB, ref float impB)
        {
            if (importance > impA)
            {
                bestB = bestA;
                impB = impA;
                bestA = index;
                impA = importance;
            }
            else if (importance > impB)
            {
                bestB = index;
                impB = importance;
            }
        }

        /// <summary>전투 예약을 모두 버린다 (StageChanged · 백그라운드 · 전장 숨김 — 한꺼번에 따라잡지 않게). 버린 수</summary>
        public int FlushBattle()
        {
            int removed = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_items[i].Active || !_items[i].Has(AudioPlayFlags.Battle)) continue;
                _items[i].Active = false;
                _count--;
                removed++;
            }
            return removed;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) _items[i].Active = false;
            _count = 0;
        }

        /// <summary>지연 보정 (§3.6): clamp(버퍼 × 버퍼 수 / 출력 샘플레이트 − 0.025, 0, 0.06) 초</summary>
        public static double LatencyCompensation(int bufferLength, int numBuffers, int outputSampleRate)
        {
            if (bufferLength <= 0 || numBuffers <= 0 || outputSampleRate <= 0) return 0;
            double latency = (double)bufferLength * numBuffers / outputSampleRate - 0.025;
            return Math.Max(0, Math.Min(0.06, latency));
        }

        /// <summary>군중 묶음 소리의 dB: −9 + 3·log2(N/2), 0 dB 상한</summary>
        public static float CrowdGainDb(int count) => (float)Math.Min(0.0, -9.0 + 3.0 * Math.Log(Math.Max(1, count) / 2.0, 2.0));
    }
}
