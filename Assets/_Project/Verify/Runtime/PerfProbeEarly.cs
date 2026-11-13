using System;
using System.Diagnostics;
using UnityEngine;

namespace IdleGame.Verify
{
    /// <summary>
    /// PerfProbe 짝: 모든 스크립트보다 먼저 Update를 돌며 그 프레임의 스크립트 구간 시작 시각 · 스레드 할당 바이트를 남긴다
    /// (PerfProbe.LateUpdate가 끝 시각 · 바이트와 뺀다 — work_ms · script_gc_b). -perfprobe가 있을 때만 PerfProbe가 붙인다. 할당 없음.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class PerfProbeEarly : MonoBehaviour
    {
        public static long UpdateStartTicks;
        public static int UpdateStartFrame = -1;
        /// <summary>이 프레임 Update 시작의 GC.GetAllocatedBytesForCurrentThread (API가 0만 주면 0)</summary>
        public static long UpdateStartAlloc;

        void Update()
        {
            UpdateStartTicks = Stopwatch.GetTimestamp();
            UpdateStartFrame = Time.frameCount;
            UpdateStartAlloc = GC.GetAllocatedBytesForCurrentThread();
        }
    }
}
