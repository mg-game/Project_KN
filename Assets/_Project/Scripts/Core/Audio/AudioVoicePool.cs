namespace IdleGame
{
    /// <summary>목소리(AudioSource) 하나의 상태 — 시각은 DSP 시계(초)</summary>
    public struct VoiceSlot
    {
        /// <summary>울리는 중 (예약 포함 — 예약한 순간부터 바쁨)</summary>
        public bool Busy;
        public AudioVoiceClass Class;
        /// <summary>소리가 시작하는 시각 (예약이면 미래)</summary>
        public double Start;
        /// <summary>소리가 끝나는 시각 (반복이면 +∞)</summary>
        public double End;
        /// <summary>울리는 키 (AudioId.Index, 없으면 −1)</summary>
        public short Key;

        public bool IsFree(double now) => !Busy || End <= now;

        /// <summary>진행 비율 0~1 (반복·시작 전은 0)</summary>
        public double Progress(double now)
        {
            double length = End - Start;
            if (!(length > 0) || double.IsInfinity(length) || now <= Start) return 0;
            return (now - Start) / length;
        }
    }

    /// <summary>
    /// 버스 안 목소리 고르기 (순수 함수 — AUDIO-SPEC §3.4): 빈 목소리가 먼저, 없으면 빼앗는다.
    /// 후보 = 새 소리보다 높은 등급이 아닌 목소리. 고르는 순서 (1) 클립 70%를 지난 꼬리 (잘려도 안 들림) (2) 낮은 등급 (3) 오래된 것.
    /// Critical은 절대 빼앗지 않는다. 50ms가 안 된 목소리는 등급 차이가 2 이상일 때만. 후보가 없으면 −1 (새 소리를 버린다).
    /// </summary>
    public static class AudioVoicePool
    {
        /// <summary>갓 시작한 목소리 보호 (초)</summary>
        public const double YoungProtect = 0.05;
        /// <summary>이만큼 지난 목소리는 꼬리로 본다</summary>
        public const double TailShare = 0.7;

        /// <summary>slots[start..start+count) 중 쓸 목소리. steal = 울리는 목소리를 빼앗는지</summary>
        public static int Choose(VoiceSlot[] slots, int start, int count, AudioVoiceClass voiceClass, double now, out bool steal)
        {
            steal = false;
            if (slots == null || count <= 0) return -1;
            int end = System.Math.Min(slots.Length, start + count);
            for (int i = start; i < end; i++)
            {
                if (slots[i].IsFree(now)) return i;
            }

            int best = -1;
            bool bestTail = false;
            int bestClass = -1;
            double bestStart = double.MaxValue;
            int newClass = (int)voiceClass;
            for (int i = start; i < end; i++)
            {
                ref var slot = ref slots[i];
                int cls = (int)slot.Class;
                if (slot.Class == AudioVoiceClass.Critical) continue;
                // 새 소리보다 중요한 목소리는 빼앗지 않는다 (숫자가 작을수록 중요)
                if (cls < newClass) continue;
                double age = now - slot.Start;
                if (age < YoungProtect && cls - newClass < 2) continue;
                bool tail = slot.Progress(now) >= TailShare;
                bool better;
                if (best < 0) better = true;
                else if (tail != bestTail) better = tail;
                else if (cls != bestClass) better = cls > bestClass;
                else better = slot.Start < bestStart;
                if (!better) continue;
                best = i;
                bestTail = tail;
                bestClass = cls;
                bestStart = slot.Start;
            }
            steal = best >= 0;
            return best;
        }
    }
}
