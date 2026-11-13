using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using IdleGame.UI;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace IdleGame.Verify
{
    /// <summary>
    /// 프레임 간격 측정 도구 (검증 복사본 전용). 플레이어를 -perfprobe &lt;폴더&gt; 로 실행하면:
    /// 진행된 세이브의 임시 복사본으로 부팅 → 시작 팝업을 닫고 AUTO 전투를 MAIN에서 돌림 → 기본 150초 동안 매 프레임
    /// unscaled dt · 스크립트 구간 시간 · 전투 시뮬레이터 스텝 수(ElapsedTime 차이 / StepSeconds) · 누산기 · ProfilerRecorder 값
    /// (Main Thread, GC Allocated In Frame, GC.Collect, Canvas.BuildBatch, Canvas.SendWillRenderCanvases, CanvasUpdate.*, TMP, 스크립트별 Update [Invoke] …)을
    /// 미리 잡은 배열에 기록하고(측정 중 할당 없음), 사건(자동 저장 · 스테이지 · 시트 열고 닫기 · 2배속 · 팝업 · GC)을 events.csv에 남긴다.
    /// 시나리오: 10/30/50/70/90/110/130초에 탭 시트를 탭 버튼(onClick — UI와 같은 길)으로 열고 4초 뒤 같은 버튼으로 닫는다, 75~105초 2배속.
    /// 끝나면 저장 비용 미세 측정(Save ×5, JSON만 ×5, 강제 GC) 뒤 frames.csv · events.csv · scripts.csv · stats_available.txt · summary.txt를 쓰고 종료.
    /// 선택 인자: -perfprobe-save &lt;원본 세이브&gt; · -perfprobe-seconds &lt;초&gt; · -perfprobe-vsync &lt;n&gt; · -perfprobe-fps &lt;n&gt; · -perfprobe-tag &lt;이름&gt;.
    /// 사용자 세이브(AppData/LocalLow)는 건드리지 않는다. 잠자기 모드는 측정 동안 _idle을 5초마다 0으로 (30fps로 떨어지지 않게), FX 품질 설정은 끝에 되돌린다.
    /// <para>
    /// RM-3 P4-9 (SPEC4 §3.1 · §11.3, perf M-0에서 옮김): -perfprobe-bench = 개발용 부하 시험 Game.Battle.DebugStress(20, 40) (개발 빌드 · 에디터만 —
    /// 릴리스 플레이어에는 그 함수가 없어 stress "unavailable"로 적는다) → 10초 데우기 → 30초 기록 (시트 · 배속 바꾸기 없음, 부하 시험이 배속 2를 고정) →
    /// {폴더}/perf_summary.json (프레임 간격 · 메인 스레드 바쁜 시간 p95 · Canvas.BuildBatch · GC/프레임 · Batches · FX 예산 · 오디오 ms · 예외 · 아틀라스 묶임 + targets).
    /// frames.csv 열 추가: track_flags (추적하는 적 — 느림 1 · 기절 2 · 맞는 몸짓 4 · 공격 몸짓 8 · 공성 예비 동작 16; PERF-2 A3가 그 프레임을 뺀다) ·
    /// fx_upper · fx_ground · fx_rej_hero (BattleFx.Stats) · script_gc_b (스크립트 구간 할당 — PerfProbeEarly.Update ~ 이 LateUpdate, 스레드 할당 API가 될 때, 아니면 −1).
    /// 오디오 (AUDIO-SPEC §8 — 2배속 · 적 20에서 메인 스레드 ≤ 0.15ms · 0B): UISfx · *Audio* 스크립트 마커는 96개 한도와 상관없이 늘 잡고, 기록이 끝난 뒤
    /// AudioEngine.Tick을 120번 직접 불러 호출당 할당 · 시간을 잰다. 끝나면 다음 Update에서 Application.Quit (코루틴 안에서 바로 끄지 않는다).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class PerfProbe : MonoBehaviour
    {
        const string DefaultSave =
            @"C:\Users\PC\AppData\Local\Temp\claude\d--UnityProject-unity-idle\5af81621-3ac1-471f-b619-0795eeedc9ed\scratchpad\verify_out_rm1_r2\shots\autoshot_player.json";

        const int MaxFrames = 48000;
        const int MaxScriptMarkers = 96;
        const float SheetHold = 4f;
        static readonly float[] SheetTimes = { 10f, 30f, 50f, 70f, 90f, 110f, 130f };
        static readonly MainTab[] SheetTabs = { MainTab.Growth, MainTab.Formation, MainTab.Characters, MainTab.Expedition, MainTab.Gacha, MainTab.Shop, MainTab.Growth };
        const float Speed2On = 75f;
        const float Speed2Off = 105f;
        /// <summary>-perfprobe-nomask: RectMask2D를 끄는 구간 (F6 비용 A/B — 36~42초와 비교)</summary>
        const float NoMaskOn = 42f;
        const float NoMaskOff = 48f;
        /// <summary>-perfprobe-alloccs 기록 구간 (기본 시나리오 56~68초, 부하 시험 4~16초)</summary>
        const float AllocOn = 56f;
        const float AllocOff = 68f;
        const float BenchAllocOn = 4f;
        const float BenchAllocOff = 16f;
        /// <summary>-perfprobe-bench: 부하 시험 적 수 · 체력 배율 · 데우기 · 기록 초 (SPEC4 §3.1)</summary>
        const int BenchEnemies = 20;
        const float BenchHpScale = 40f;
        const float BenchWarmup = 10f;
        const float BenchSeconds = 30f;
        /// <summary>SPEC4 §11.3 · AUDIO-SPEC §8 기준 (perf_summary.json targets)</summary>
        const float TargetBusyP95Ms = 8f;
        const float TargetBuildBatchMs = 0.6f;
        const float TargetGcBytes = 64f;
        const float TargetAudioMs = 0.15f;
        /// <summary>AudioEngine.Tick 미세 측정 횟수 (dt 1/60 — 2초어치)</summary>
        const int AudioTickCalls = 120;

        /// <summary>(열 이름, 마커/카운터 이름) — 단위: 이름이 _ns면 ns→ms로 바꿔 적는다, _b는 바이트, _n은 개수</summary>
        static readonly string[,] CoreStats =
        {
            { "main_ms", "Main Thread" },
            { "render_ms", "Render Thread" },
            { "playerloop_ms", "PlayerLoop" },
            { "cpu_main_ft_ms", "CPU Main Thread Frame Time" },
            { "cpu_render_ft_ms", "CPU Render Thread Frame Time" },
            { "cpu_total_ft_ms", "CPU Total Frame Time" },
            { "gpu_ft_ms", "GPU Frame Time" },
            { "gc_alloc_b", "GC Allocated In Frame" },
            { "gc_alloc_n", "GC Allocation In Frame Count" },
            { "gc_used_b", "GC Used Memory" },
            { "gc_collect_ms", "GC.Collect" },
            { "buildbatch_ms", "Canvas.BuildBatch" },
            { "willrender_ms", "UIEvents.WillRenderCanvases" },
            { "layout_ms", "Layout" },
            { "emitgeometry_ms", "PostLateUpdate.PlayerEmitCanvasGeometry" },
            { "canvasrect_ms", "EarlyUpdate.UpdateCanvasRectTransform" },
            { "geometryjob_ms", "Canvas.GeometryJob" },
            { "renderoverlays_ms", "Canvas.RenderOverlays" },
            { "updatecanvases_ms", "PostLateUpdate.PlayerUpdateCanvases" },
            { "cu_prelayout_ms", "CanvasUpdate.Prelayout" },
            { "cu_layout_ms", "CanvasUpdate.Layout" },
            { "cu_postlayout_ms", "CanvasUpdate.PostLayout" },
            { "cu_prerender_ms", "CanvasUpdate.PreRender" },
            { "cu_lateprerender_ms", "CanvasUpdate.LatePreRender" },
            { "clipcull_ms", "ClipperRegistry.Cull" },
            { "tmp_gen_ms", "TMP.GenerateText" },
            { "tmp_addchars_ms", "TMP.TryAddCharacters" },
            { "tmp_addchar_ms", "TMP.TryAddCharacter" },
            { "tmp_addglyph_ms", "TMP.TryAddGlyph" },
            { "tmp_setarray_ms", "TMP.SetArraySizes" },
            { "tmp_parse_ms", "TMP Parse Text" },
            { "ll_autosave_ms", "LL.AutoSave" },
            { "ll_stagestart_ms", "LL.StageStart" },
            { "ll_stageend_ms", "LL.StageEnd" },
            { "scripts_update_ms", "Update.ScriptRunBehaviourUpdate" },
            { "scripts_late_ms", "PreLateUpdate.ScriptRunBehaviourLateUpdate" },
            { "coroutines_ms", "Update.ScriptRunDelayedDynamicFrameRate" },
            { "waitfps_ms", "WaitForTargetFPS" },
            { "waitpresent_ms", "Gfx.WaitForPresentOnGfxThread" },
            { "waitgfxcmd_ms", "Gfx.WaitForGfxCommandsFromMainThread" },
            { "present_ms", "Gfx.PresentFrame" },
            { "waitrender_ms", "Gfx.WaitForRenderThread" },
            { "dxgiwait_ms", "DXGI.WaitOnSwapChain" },
            { "d3d12lastpresent_ms", "GfxDeviceD3D12.WaitForLastPresent" },
            { "semwait_ms", "Semaphore.WaitForSignal" },
            { "gc_incr_ms", "GarbageCollector.CollectIncremental" },
            { "finishrender_ms", "PostLateUpdate.FinishFrameRendering" },
            { "audio_ms", "PostLateUpdate.UpdateAudio" },
            { "readobject_ms", "Loading.ReadObject" },
            { "awakefromload_ms", "Loading.AwakeFromLoad" },
            { "instantiate_ms", "Instantiate" },
            { "activate_ms", "GameObject.Activate" },
            { "deactivate_ms", "GameObject.Deactivate" },
            { "shader_gpu_ms", "Shader.CreateGPUProgram" },
            { "drawcalls_n", "Draw Calls Count" },
            { "batches_n", "Batches Count" },
            { "setpass_n", "SetPass Calls Count" },
            { "verts_n", "Vertices Count" },
        };

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool QueryThreadCycleTime(IntPtr threadHandle, out ulong cycleTime);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessorNumber();

        /// <summary>메인 스레드가 실제로 돈 CPU 사이클 (선점 · 코어 경쟁을 벽시계와 비교). 윈도우가 아니면 0</summary>
        static ulong ThreadCycles()
        {
            try { return QueryThreadCycleTime(GetCurrentThread(), out ulong c) ? c : 0UL; }
            catch (Exception) { return 0UL; }
        }

        static int ProcessorNumber()
        {
            try { return (int)GetCurrentProcessorNumber(); }
            catch (Exception) { return -1; }
        }

        const int WatchBuckets = 8000; // 50 ms buckets -> 400 s
        float[] _watchMax;
        long _t0Ticks;
        volatile bool _watchRun;
        System.Threading.Thread _watch;
        float[] _lateT;

        void StartWatchdog()
        {
            _watchMax = new float[WatchBuckets];
            _watchRun = true;
            _watch = new System.Threading.Thread(() =>
            {
                long last = Stopwatch.GetTimestamp();
                double tickMs = 1000.0 / Stopwatch.Frequency;
                while (_watchRun)
                {
                    System.Threading.Thread.Sleep(1);
                    long now = Stopwatch.GetTimestamp();
                    float gap = (float)((now - last) * tickMs);
                    last = now;
                    int b = (int)((now - _t0Ticks) * tickMs / 50.0);
                    if (b >= 0 && b < WatchBuckets && gap > _watchMax[b]) _watchMax[b] = gap;
                }
            }) { IsBackground = true, Name = "PerfProbeWatchdog", Priority = System.Threading.ThreadPriority.Normal };
            _watch.Start();
        }

        void WriteWatchdog()
        {
            if (_watchMax == null) return;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("t,max_gap_ms\n");
            int last = Math.Min(WatchBuckets, (int)(_seconds * 20) + 40);
            for (int b = 0; b < last; b++) sb.Append((b * 0.05).ToString("0.00", inv)).Append(',').Append(_watchMax[b].ToString("0.###", inv)).Append('\n');
            File.WriteAllText(Path.Combine(_dir, "watchdog.csv"), sb.ToString());
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct ProcessMemoryCounters
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool K32GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);

        uint _lastFaults;
        bool _faultsOk = true;
        int[] _faults;
        float[] _workingSetMb;

        void ReadMemory(int i)
        {
            if (!_faultsOk) return;
            try
            {
                if (K32GetProcessMemoryInfo(GetCurrentProcess(), out var c, (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProcessMemoryCounters))))
                {
                    _faults[i] = _lastFaults == 0 ? 0 : (int)(c.PageFaultCount - _lastFaults);
                    _lastFaults = c.PageFaultCount;
                    _workingSetMb[i] = (float)(c.WorkingSetSize.ToUInt64() / 1048576.0);
                }
            }
            catch (Exception) { _faultsOk = false; }
        }

        double _cyclesPerMs;
        ulong _lastCycles;
        long _lastLateTicks;

        // ── 인자 ──
        string _dir;
        string _tag;
        float _seconds = 150f;
        int _vsync = -1;
        int _fps = int.MinValue;
        bool _dirtySample;
        bool _noMaskWindow;
        int _noMaskPhase;
        /// <summary>-perfprobe-alloccs: 56~68초(시트 · 2배속 없는 전투) 프로파일러 이진 기록 + 할당 호출 스택 → alloc.raw (에디터 Profiler에서 연다, GC Alloc 열)</summary>
        bool _allocCapture;
        int _allocPhase;
        /// <summary>-perfprobe-bench: 개발용 부하 시험 (DebugStress) 시나리오</summary>
        bool _bench;
        string _stress = "off";
        bool _quit;
        BattleFx _fx;
        int _particleBudget = -1;
        int _groundBudget = -1;
        string _fxQuality = "-";
        /// <summary>GC.GetAllocatedBytesForCurrentThread가 실제로 바이트를 세는지 (Allocate에서 4KB를 만들어 본다)</summary>
        bool _threadAllocOk;
        int _logErrors, _logExceptions;
        /// <summary>오디오 스크립트 마커 자리 (UISfx · *Audio* — 96개 한도 밖에서도 늘 잡는다)</summary>
        readonly List<int> _audioMarkers = new List<int>(8);
        readonly StringBuilder _audioBench = new StringBuilder();
        double _audioTickBytesPerCall = double.NaN;
        double _audioTickMsPerCall = double.NaN;
        string _audioTickNote = "not run";
        RectMask2D[] _masks;
        /// <summary>추적하는 적의 뷰 (보이는 자리 draw_x/draw_y — 보간 PF-2 F1 판정)</summary>
        EnemyViewPool _enemyPool;
        static string s_sourceSave;
        static string s_probeSave;

        // ── 상태 ──
        bool _recording;
        bool _done;
        double _t0;
        int _n;
        MainTabController _tabs;
        Component _sleep;
        FieldInfo _sleepIdle;
        float _sleepResetTimer;
        int _sheetIndex;
        bool _sheetOpenCmd;
        float _sheetOpenAt;
        int _speedPhase;
        bool _popupOpen;
        object _popupTop;
        float _popupAge;
        int _lastGc;
        float _lastSaveTimer = -1f;
        float _lastNightTimer = -1f;
        bool _lastSettled = true;
        int _evTotal, _evKill, _evSpawn, _evHit;
        string[] _eventNames;
        readonly Stopwatch _clock = Stopwatch.StartNew();

        Func<object, float> _getAcc;
        FieldInfo _accField;
        Func<object, float> _getSaveTimer;
        FieldInfo _saveTimerField;
        Func<object, float> _getNightTimer;
        FieldInfo _nightTimerField;
        bool _accBoxing;
        bool _saveBoxing;

        // ── 기록 열 ──
        int[] _frame;
        double[] _tStart;
        float[] _udt, _dt, _work, _late, _acc, _simTime, _pscale, _sheetP, _trackX, _trackY, _drawX, _drawY, _probeMs;
        short[] _steps, _speed, _enemies, _echoes, _evT, _evK, _evS, _evH;
        int[] _gc0, _trackId;
        float[] _cpuMs, _lateIntervalMs;
        short[] _coreNum;
        byte[] _flags; // 1 = between stages, 2 = focused, 4 = popup open, 8 = sheet open, 16 = sheet settled, 32 = asleep-reset frame
        /// <summary>추적하는 적의 움직임 상태 (PERF-2 A3): 1 = 느림(SlowTimer) · 2 = 기절(StunTimer) · 4 = 맞는 몸짓(HitAnim) · 8 = 공격 몸짓(AttackAnim) · 16 = 공성 예비 동작(SiegeWindup) · 0 = 없음 / 추적 없음</summary>
        byte[] _trackFlags;
        short[] _fxUpper, _fxGround, _fxRejHero;
        int[] _scriptGc;
        float _prevSimTime = -1f;
        object _prevSim;

        ProfilerRecorder[] _core;
        bool[] _coreValid;
        bool[] _coreNs;
        float[][] _coreCols;
        ProfilerRecorder[] _scripts;
        string[] _scriptNames;
        float[][] _scriptCols;
        int _scriptCount;

        // ── 다시 그릴 그래픽 표본 (-perfprobe-dirty: 30프레임마다 CanvasUpdateRegistry 큐를 들여다본다 — 할당이 있으므로 a/b 측정과 따로 돌린다) ──
        FieldInfo _graphicQueueField;
        FieldInfo _layoutQueueField;
        int _dirtySamples;
        readonly List<int> _dirtyGraphicCounts = new List<int>(1024);
        readonly List<int> _dirtyLayoutCounts = new List<int>(1024);
        readonly Dictionary<string, int> _dirtyByKey = new Dictionary<string, int>(1024);
        readonly Dictionary<string, int> _dirtyByType = new Dictionary<string, int>(64);

        void SampleDirty()
        {
            var registry = CanvasUpdateRegistry.instance;
            if (registry == null) return;
            if (_graphicQueueField == null)
            {
                _graphicQueueField = typeof(CanvasUpdateRegistry).GetField("m_GraphicRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                _layoutQueueField = typeof(CanvasUpdateRegistry).GetField("m_LayoutRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_graphicQueueField == null) return;
            }
            _dirtySamples++;
            int g = 0;
            // IndexedSet<T>.GetEnumerator throws NotImplementedException - index it through IList<T>
            if (_graphicQueueField.GetValue(registry) is IList<ICanvasElement> graphics)
            {
                for (int q = 0; q < graphics.Count; q++)
                {
                    var element = graphics[q];
                    if (element == null) continue;
                    g++;
                    var tr = element.transform;
                    string type = element.GetType().Name;
                    _dirtyByType[type] = (_dirtyByType.TryGetValue(type, out int tc) ? tc : 0) + 1;
                    string key = type + " @ " + ShortPath(tr);
                    _dirtyByKey[key] = (_dirtyByKey.TryGetValue(key, out int kc) ? kc : 0) + 1;
                }
            }
            int l = 0;
            if (_layoutQueueField != null && _layoutQueueField.GetValue(registry) is IList<ICanvasElement> layouts)
            {
                for (int q = 0; q < layouts.Count; q++)
                {
                    var element = layouts[q];
                    if (element == null) continue;
                    l++;
                    string key = "LAYOUT " + element.GetType().Name + " @ " + ShortPath(element.transform);
                    _dirtyByKey[key] = (_dirtyByKey.TryGetValue(key, out int kc) ? kc : 0) + 1;
                }
            }
            _dirtyGraphicCounts.Add(g);
            _dirtyLayoutCounts.Add(l);
        }

        /// <summary>부모 4단계까지의 이름 경로 (숫자 꼬리는 #으로 — 풀에서 나온 같은 종류를 묶는다)</summary>
        static string ShortPath(Transform tr)
        {
            if (tr == null) return "?";
            var sb = new StringBuilder();
            var cur = tr;
            for (int d = 0; d < 5 && cur != null; d++)
            {
                string name = cur.name;
                int cut = name.Length;
                while (cut > 0 && (char.IsDigit(name[cut - 1]) || name[cut - 1] == '(' || name[cut - 1] == ')' || name[cut - 1] == ' ' || name[cut - 1] == '_')) cut--;
                name = cut < name.Length ? name.Substring(0, cut) + "#" : name;
                sb.Insert(0, d == 0 ? name : name + "/");
                cur = cur.parent;
            }
            return sb.ToString();
        }

        void WriteDirty()
        {
            if (_dirtySamples == 0) return;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("samples: ").Append(_dirtySamples).Append('\n');
            _dirtyGraphicCounts.Sort();
            _dirtyLayoutCounts.Sort();
            int gn = _dirtyGraphicCounts.Count;
            double gsum = 0;
            foreach (int v in _dirtyGraphicCounts) gsum += v;
            sb.Append("graphic_rebuild_per_frame mean=").Append((gsum / Math.Max(1, gn)).ToString("0.0", inv))
                .Append(" p50=").Append(gn > 0 ? _dirtyGraphicCounts[gn / 2] : 0)
                .Append(" p95=").Append(gn > 0 ? _dirtyGraphicCounts[Math.Min(gn - 1, (int)(gn * 0.95))] : 0)
                .Append(" max=").Append(gn > 0 ? _dirtyGraphicCounts[gn - 1] : 0).Append('\n');
            int ln = _dirtyLayoutCounts.Count;
            sb.Append("layout_rebuild_per_frame p50=").Append(ln > 0 ? _dirtyLayoutCounts[ln / 2] : 0)
                .Append(" max=").Append(ln > 0 ? _dirtyLayoutCounts[ln - 1] : 0).Append('\n');
            sb.Append("-- by type (total over samples, avg per sampled frame) --\n");
            var types = new List<KeyValuePair<string, int>>(_dirtyByType);
            types.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var pair in types)
                sb.Append(pair.Key).Append('\t').Append(pair.Value).Append('\t').Append(((double)pair.Value / _dirtySamples).ToString("0.00", inv)).Append('\n');
            sb.Append("-- by element (top 80) --\n");
            var keys = new List<KeyValuePair<string, int>>(_dirtyByKey);
            keys.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int k = 0; k < keys.Count && k < 80; k++)
                sb.Append(((double)keys[k].Value / _dirtySamples).ToString("0.00", inv)).Append('\t').Append(keys[k].Key).Append('\n');
            File.WriteAllText(Path.Combine(_dir, "dirty.txt"), sb.ToString());
        }

        // ── 사건 ──
        struct Ev
        {
            public double T;
            public int Frame;
            public int Row;
            public string Type;
            public string Detail;
        }

        readonly List<Ev> _events = new List<Ev>(4096);
        readonly StringBuilder _benchText = new StringBuilder();
        readonly Dictionary<string, string> _env = new Dictionary<string, string>();

        // ═════════════════════════ 켜기 ═════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void UseProbeSave()
        {
            var dir = Arg("-perfprobe");
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            s_sourceSave = Arg("-perfprobe-save") ?? DefaultSave;
            s_probeSave = Path.Combine(dir, "probe_player.json");
            foreach (var file in new[] { s_probeSave, s_probeSave + ".bak", s_probeSave + ".tmp" })
            {
                if (File.Exists(file)) File.Delete(file);
            }
            if (File.Exists(s_sourceSave)) File.Copy(s_sourceSave, s_probeSave, true);
            else Debug.LogWarning($"[PerfProbe] 원본 세이브가 없어 새 게임으로 시작: {s_sourceSave}");
            GameBootstrap.SavePathOverride = s_probeSave;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var dir = Arg("-perfprobe");
            if (dir == null) return;
            var go = new GameObject("[PerfProbe]");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfProbeEarly>();
            var probe = go.AddComponent<PerfProbe>();
            probe._dir = dir;
            probe._tag = Arg("-perfprobe-tag") ?? "run";
            if (float.TryParse(Arg("-perfprobe-seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 5f) probe._seconds = s;
            if (int.TryParse(Arg("-perfprobe-vsync"), out int v)) probe._vsync = v;
            if (int.TryParse(Arg("-perfprobe-fps"), out int f)) probe._fps = f;
            probe._dirtySample = HasFlag("-perfprobe-dirty");
            probe._noMaskWindow = HasFlag("-perfprobe-nomask");
            probe._allocCapture = HasFlag("-perfprobe-alloccs");
            probe._bench = HasFlag("-perfprobe-bench");
            if (probe._bench && !(float.TryParse(Arg("-perfprobe-seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float bs) && bs > 5f)) probe._seconds = BenchSeconds;
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            var prefs = SnapshotPrefs();
            Mark("probe_start", _tag);

            // 부팅 + 시작 팝업
            float wait = 0f;
            while ((!Game.IsReady || UIManager.Instance == null) && wait < 30f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            Mark("game_ready", wait.ToString("0.00", CultureInfo.InvariantCulture));
            yield return new WaitForSecondsRealtime(3f);
            UnlockAll();
            CloseAll("boot1");
            UINav.GoToMain(true);
            if (Game.Battle != null)
            {
                Game.Battle.AutoMode = true;
                Game.Battle.Speed = 1;
            }
            yield return new WaitForSecondsRealtime(2f);
            CloseAll("boot2");
            yield return new WaitForSecondsRealtime(2f);
            CloseAll("boot3");
            UINav.GoToMain(true);

            if (_vsync >= 0) QualitySettings.vSyncCount = _vsync;
            if (_fps != int.MinValue) Application.targetFrameRate = _fps;
            if (_vsync >= 0 || _fps != int.MinValue) Mark("frame_override", $"vSyncCount={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate}");

            Bind();
            Allocate();
            SetupRecorders();
            if (_bench)
            {
                StartStress(true);
                Mark("bench_stress", _stress);
                // 데우기 (SPEC4 §3.1 — 10초): 적이 차고 풀 · 글자 캐시가 데워지게. 팝업은 2초마다 닫는다
                float warm = 0f, close = 0f;
                while (warm < BenchWarmup)
                {
                    warm += Time.unscaledDeltaTime;
                    close += Time.unscaledDeltaTime;
                    if (close >= 2f)
                    {
                        close = 0f;
                        CloseAll("warmup");
                    }
                    yield return null;
                }
                Mark("bench_warm", warm.ToString("0.00", CultureInfo.InvariantCulture));
            }
            CaptureEnv();
            if (!_bench) yield return new WaitForSecondsRealtime(3f); // 덮개 · 시트 닫힘 · 마커 등록이 가라앉게

            Game.Battle.Event += OnBattleEvent;
            Game.Battle.StageChanged += OnStageChanged;
            Game.Battle.StageEnded += OnStageEnded;
            UIFrame.SheetStateChanged += OnSheetState;
            GameSettings.Changed += OnSettingsChanged;
            Calibrate();
            _lastGc = GC.CollectionCount(0);
            _lastCycles = ThreadCycles();
            _lastLateTicks = Stopwatch.GetTimestamp();
            _t0Ticks = _lastLateTicks;
            StartWatchdog();
            _t0 = Time.realtimeSinceStartupAsDouble;
            _recording = true;
            Mark("record_start", $"seconds={_seconds}");

            double limit = _seconds + 30.0;
            while (_recording && Time.realtimeSinceStartupAsDouble - _t0 < limit) yield return null;
            _recording = false;
            _watchRun = false;
            Mark("record_end", $"frames={_n}");

            Game.Battle.Event -= OnBattleEvent;
            Game.Battle.StageChanged -= OnStageChanged;
            Game.Battle.StageEnded -= OnStageEnded;
            UIFrame.SheetStateChanged -= OnSheetState;
            GameSettings.Changed -= OnSettingsChanged;
            // 오디오 엔진 미세 측정은 부하 시험이 아직 도는 동안 (소리가 나는 상태 그대로, 기록은 이미 끝났다)
            try { AudioTickBench(); }
            catch (Exception e) { _audioTickNote = "error " + e.GetType().Name + ": " + e.Message; }
            if (_bench) StartStress(false);
            if (Game.Battle != null) Game.Battle.Speed = 1;
            UINav.GoToMain(true);
            yield return null;

            yield return SafeBench(MicroBench());

            try { WriteWatchdog(); }
            catch (Exception e) { Debug.LogException(e); }
            try { WriteDirty(); }
            catch (Exception e) { Debug.LogException(e); }
            try { WriteAll(); }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(Path.Combine(_dir, "write_error.txt"), e.ToString()); }
            try { WriteSummaryJson(); }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(Path.Combine(_dir, "summary_error.txt"), e.ToString()); }

            DisposeRecorders();
            RestorePrefs(prefs);
            _done = true;
            Application.logMessageReceived -= OnLog;
            yield return null;
            // 다음 프레임 Update에서 끈다 (코루틴 안에서 바로 Quit하지 않는다 — RI-8 종료 충돌 후보와 같은 꼴을 피한다)
            _quit = true;
        }

        void Update()
        {
            if (!_quit) return;
            _quit = false;
            Application.Quit();
        }

        /// <summary>
        /// 개발용 부하 시험 켜기 / 끄기 (BattleSystem.DebugStress — 개발 빌드 · 에디터에만 있다, SPEC4 §3.1). 릴리스 플레이어에서는 아무것도 하지 않고 "unavailable"
        /// </summary>
        void StartStress(bool on)
        {
            if (Game.Battle == null)
            {
                if (on) _stress = "no battle";
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                Game.Battle.DebugStress(on ? BenchEnemies : 0, BenchHpScale);
                if (on) _stress = "on enemies=" + BenchEnemies.ToString(CultureInfo.InvariantCulture) + " hpScale=" + BenchHpScale.ToString("0.#", CultureInfo.InvariantCulture) +
                                  " speed=" + Game.Battle.Speed.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                _stress = "error " + e.GetType().Name + ": " + e.Message;
            }
#else
            if (on) _stress = "unavailable (release build: DebugStress is dev-only)";
#endif
        }

        /// <summary>사이클 → ms 환산 (메인 스레드에서 80ms 바쁜 루프 세 번 중 최대 — 그 동안 선점되면 작게 나오므로)</summary>
        void Calibrate()
        {
            double best = 0;
            double x = 0;
            for (int k = 0; k < 3; k++)
            {
                ulong c0 = ThreadCycles();
                long s0 = Stopwatch.GetTimestamp();
                while ((Stopwatch.GetTimestamp() - s0) * 1000.0 / Stopwatch.Frequency < 80.0) x += Math.Sqrt(x + 1.0);
                ulong c1 = ThreadCycles();
                double ms = (Stopwatch.GetTimestamp() - s0) * 1000.0 / Stopwatch.Frequency;
                if (c1 > c0 && ms > 0) best = Math.Max(best, (c1 - c0) / ms);
            }
            _cyclesPerMs = best;
            _env["cycles_per_ms"] = best.ToString("0", CultureInfo.InvariantCulture);
            _env["calibrate_sink"] = (x > 0).ToString();
        }

        void Bind()
        {
            _tabs = FindAnyObjectByType<MainTabController>();
            _sleep = FindAnyObjectByType<SleepModeView>();
            _enemyPool = FindAnyObjectByType<EnemyViewPool>();
            _fx = FindAnyObjectByType<BattleFx>();
            if (_sleep != null) _sleepIdle = _sleep.GetType().GetField("_idle", BindingFlags.Instance | BindingFlags.NonPublic);

            var sim = Game.Battle.Sim;
            _accField = sim.GetType().GetField("_accumulator", BindingFlags.Instance | BindingFlags.NonPublic);
            _getAcc = Getter(_accField, out _accBoxing);
            var boot = GameBootstrap.Instance;
            if (boot != null)
            {
                _saveTimerField = boot.GetType().GetField("_autoSaveTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                _getSaveTimer = Getter(_saveTimerField, out _saveBoxing);
                _nightTimerField = boot.GetType().GetField("_nightVisitTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                _getNightTimer = Getter(_nightTimerField, out _);
            }
            _eventNames = Enum.GetNames(typeof(BattleEventType));
        }

        /// <summary>private float 필드를 할당 없이 읽는 함수 (식 트리 컴파일). 안 되면 FieldInfo.GetValue (상자 24B/회 — boxing=true)</summary>
        static Func<object, float> Getter(FieldInfo field, out bool boxing)
        {
            boxing = false;
            if (field == null || field.FieldType != typeof(float)) return null;
            try
            {
                var p = Expression.Parameter(typeof(object), "o");
                var body = Expression.Field(Expression.Convert(p, field.DeclaringType), field);
                return Expression.Lambda<Func<object, float>>(body, p).Compile();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PerfProbe] 식 트리 컴파일 실패 ({field.Name}) — GetValue로: {e.Message}");
                boxing = true;
                return o => (float)field.GetValue(o);
            }
        }

        void Allocate()
        {
            _frame = new int[MaxFrames];
            _tStart = new double[MaxFrames];
            _udt = new float[MaxFrames];
            _dt = new float[MaxFrames];
            _work = new float[MaxFrames];
            _late = new float[MaxFrames];
            _acc = new float[MaxFrames];
            _simTime = new float[MaxFrames];
            _pscale = new float[MaxFrames];
            _sheetP = new float[MaxFrames];
            _trackX = new float[MaxFrames];
            _drawX = new float[MaxFrames];
            _drawY = new float[MaxFrames];
            _trackY = new float[MaxFrames];
            _probeMs = new float[MaxFrames];
            _steps = new short[MaxFrames];
            _speed = new short[MaxFrames];
            _enemies = new short[MaxFrames];
            _echoes = new short[MaxFrames];
            _evT = new short[MaxFrames];
            _evK = new short[MaxFrames];
            _evS = new short[MaxFrames];
            _evH = new short[MaxFrames];
            _gc0 = new int[MaxFrames];
            _trackId = new int[MaxFrames];
            _flags = new byte[MaxFrames];
            _cpuMs = new float[MaxFrames];
            _lateIntervalMs = new float[MaxFrames];
            _coreNum = new short[MaxFrames];
            _lateT = new float[MaxFrames];
            _faults = new int[MaxFrames];
            _workingSetMb = new float[MaxFrames];
            _trackFlags = new byte[MaxFrames];
            _fxUpper = new short[MaxFrames];
            _fxGround = new short[MaxFrames];
            _fxRejHero = new short[MaxFrames];
            _scriptGc = new int[MaxFrames];
            // 스레드 할당 API 확인: 4KB를 만들어 그만큼 늘었는지 (0만 돌려주는 런타임이면 script_gc_b · 오디오 바이트는 n/a)
            try
            {
                long a = GC.GetAllocatedBytesForCurrentThread();
                var test = new byte[4096];
                long b = GC.GetAllocatedBytesForCurrentThread();
                GC.KeepAlive(test);
                _threadAllocOk = b - a >= 4096;
            }
            catch (Exception) { _threadAllocOk = false; }
        }

        void SetupRecorders()
        {
            var handles = new List<ProfilerRecorderHandle>(8192);
            ProfilerRecorderHandle.GetAvailable(handles);
            var byName = new Dictionary<string, ProfilerRecorderDescription>(handles.Count);
            var avail = new StringBuilder();
            var scriptNames = new List<string>();
            foreach (var h in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (string.IsNullOrEmpty(d.Name)) continue;
                if (!byName.ContainsKey(d.Name)) byName[d.Name] = d;
                avail.Append(d.Category.Name).Append('\t').Append(d.Name).Append('\t').Append(d.UnitType).Append('\n');
                if (d.Name.EndsWith("() [Invoke]", StringComparison.Ordinal) && (d.Name.Contains(".Update()") || d.Name.Contains(".LateUpdate()")))
                    scriptNames.Add(d.Name);
            }
            File.WriteAllText(Path.Combine(_dir, "stats_available.txt"), avail.ToString());

            int count = CoreStats.GetLength(0);
            _core = new ProfilerRecorder[count];
            _coreValid = new bool[count];
            _coreNs = new bool[count];
            _coreCols = new float[count][];
            for (int i = 0; i < count; i++)
            {
                string label = CoreStats[i, 0];
                string name = CoreStats[i, 1];
                _coreNs[i] = label.EndsWith("_ms", StringComparison.Ordinal);
                try
                {
                    _core[i] = byName.TryGetValue(name, out var d)
                        ? ProfilerRecorder.StartNew(d.Category, name, 1)
                        : ProfilerRecorder.StartNew(ProfilerCategory.Internal, name, 1);
                    _coreValid[i] = _core[i].Valid;
                    if (_coreValid[i] && byName.TryGetValue(name, out var desc) && desc.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) _coreNs[i] = false;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PerfProbe] 기록기 실패 {name}: {e.Message}");
                }
                _coreCols[i] = new float[MaxFrames];
            }

            // 오디오 스크립트(UISfx.LateUpdate · BattleAudioDirector.Update …)는 96개 한도에 잘리지 않게 앞으로, 나머지는 이름 순 (AUDIO-SPEC §8 — RM-3 P4-9)
            scriptNames.Sort((x, y) =>
            {
                int ax = IsAudioMarker(x) ? 0 : 1, ay = IsAudioMarker(y) ? 0 : 1;
                return ax != ay ? ax.CompareTo(ay) : string.CompareOrdinal(x, y);
            });
            _scriptCount = Mathf.Min(scriptNames.Count, MaxScriptMarkers);
            _scripts = new ProfilerRecorder[_scriptCount];
            _scriptNames = new string[_scriptCount];
            _scriptCols = new float[_scriptCount][];
            for (int i = 0; i < _scriptCount; i++)
            {
                _scriptNames[i] = scriptNames[i];
                var d = byName[scriptNames[i]];
                try { _scripts[i] = ProfilerRecorder.StartNew(d.Category, scriptNames[i], 1); }
                catch (Exception) { }
                _scriptCols[i] = new float[MaxFrames];
                if (IsAudioMarker(scriptNames[i])) _audioMarkers.Add(i);
            }
            _env["script_markers_found"] = scriptNames.Count.ToString(CultureInfo.InvariantCulture);
            _env["available_stats"] = byName.Count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>오디오 쪽 스크립트 마커인지 ("UISfx.LateUpdate() [Invoke]", "BattleAudioDirector.Update() [Invoke]" …)</summary>
        static bool IsAudioMarker(string name) =>
            name.StartsWith("UISfx.", StringComparison.Ordinal) || name.IndexOf("Audio", StringComparison.Ordinal) >= 0;

        void DisposeRecorders()
        {
            if (_core != null) for (int i = 0; i < _core.Length; i++) _core[i].Dispose();
            if (_scripts != null) for (int i = 0; i < _scripts.Length; i++) _scripts[i].Dispose();
        }

        void CaptureEnv()
        {
            var res = Screen.currentResolution;
            _env["tag"] = _tag;
            _env["scenario"] = _bench ? "bench" : "scenario150";
            _env["stress"] = _stress;
            _env["thread_alloc_api"] = _threadAllocOk ? "ok" : "n/a";
            if (_fx != null)
            {
                _particleBudget = _fx.ParticleBudget;
                _fxQuality = GameSettings.FxQuality.ToString();
                _groundBudget = HitFeel.GroundBudget(GameSettings.FxQuality);
            }
            _env["fx_budget"] = $"upper={_particleBudget} ground={_groundBudget} quality={_fxQuality}";
            _env["dirty_sampling"] = _dirtySample.ToString();
            _env["unity"] = Application.unityVersion;
            _env["debug_build"] = Debug.isDebugBuild.ToString();
            _env["platform"] = Application.platform.ToString();
            _env["refresh_hz"] = res.refreshRateRatio.value.ToString("0.###", CultureInfo.InvariantCulture);
            _env["refresh_ratio"] = $"{res.refreshRateRatio.numerator}/{res.refreshRateRatio.denominator}";
            _env["monitor_res"] = $"{res.width}x{res.height}";
            _env["window"] = $"{Screen.width}x{Screen.height} mode={Screen.fullScreenMode}";
            _env["quality"] = $"{QualitySettings.GetQualityLevel()} {QualitySettings.names[QualitySettings.GetQualityLevel()]}";
            _env["vSyncCount"] = QualitySettings.vSyncCount.ToString(CultureInfo.InvariantCulture);
            _env["targetFrameRate"] = Application.targetFrameRate.ToString(CultureInfo.InvariantCulture);
            _env["maxQueuedFrames"] = QualitySettings.maxQueuedFrames.ToString(CultureInfo.InvariantCulture);
            _env["gfx"] = $"{SystemInfo.graphicsDeviceType} | {SystemInfo.graphicsDeviceName}";
            _env["cpu"] = $"{SystemInfo.processorType} x{SystemInfo.processorCount}";
            _env["ram_mb"] = SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture);
            _env["gc_incremental"] = UnityEngine.Scripting.GarbageCollector.isIncremental.ToString();
            _env["gc_mode"] = UnityEngine.Scripting.GarbageCollector.GCMode.ToString();
            _env["gc_slice_ns"] = UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds.ToString(CultureInfo.InvariantCulture);
            _env["time_maxdt"] = Time.maximumDeltaTime.ToString("0.####", CultureInfo.InvariantCulture);
            _env["fixed_dt"] = Time.fixedDeltaTime.ToString("0.#####", CultureInfo.InvariantCulture);
            _env["fx_quality"] = GameSettings.FxQuality.ToString();
            _env["fx_quality_auto"] = GameSettings.FxQualityAuto.ToString();
            _env["settings_frame_rate"] = GameSettings.FrameRate.ToString(CultureInfo.InvariantCulture);
            _env["stage"] = Game.Battle != null ? Game.Battle.StageLabel : "-";
            _env["meta_active"] = Game.IsMetaActive.ToString();
            _env["acc_getter"] = _getAcc == null ? "none" : _accBoxing ? "boxing" : "compiled";
            _env["save_timer_getter"] = _getSaveTimer == null ? "none" : _saveBoxing ? "boxing" : "compiled";
            _env["autosave_interval"] = Game.Config != null ? Game.Config.autoSaveInterval.ToString("0.#", CultureInfo.InvariantCulture) : "-";
            _env["sleep_view"] = _sleep != null ? (_sleepIdle != null ? "found(_idle)" : "found(no field)") : "none";
            _env["source_save"] = s_sourceSave ?? "-";
            _env["probe_save"] = s_probeSave ?? "-";
            _env["stopwatch_hz"] = Stopwatch.Frequency.ToString(CultureInfo.InvariantCulture);
            _env["canvases"] = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length.ToString(CultureInfo.InvariantCulture);
            var valid = new StringBuilder();
            for (int i = 0; i < _core.Length; i++) valid.Append(CoreStats[i, 0]).Append(_coreValid[i] ? "=ok " : "=NO ");
            _env["core_recorders"] = valid.ToString();
        }

        // ═════════════════════════ 매 프레임 ═════════════════════════

        int _lateErrors;

        /// <summary>한 프레임 기록 — 예외가 나도 행은 넘어가고(같은 행에서 멈추지 않게) 처음 다섯 번만 사건으로 남긴다</summary>
        void LateUpdate()
        {
            if (!_recording || _done) return;
            int before = _n;
            try { RecordFrame(); }
            catch (Exception e)
            {
                if (_n == before && before < MaxFrames) _n = before + 1;
                if (_lateErrors++ < 5) Mark("probe_error", e.GetType().Name + ": " + e.Message);
            }
        }

        void RecordFrame()
        {
            long probeStart = Stopwatch.GetTimestamp();
            int i = _n;
            if (i >= MaxFrames)
            {
                _recording = false;
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            float rel = (float)(now - _t0);
            ulong cycles = ThreadCycles();
            _cpuMs[i] = _cyclesPerMs > 0 && cycles >= _lastCycles ? (float)((cycles - _lastCycles) / _cyclesPerMs) : -1f;
            _lastCycles = cycles;
            _lateIntervalMs[i] = (float)((probeStart - _lastLateTicks) * 1000.0 / Stopwatch.Frequency);
            _lastLateTicks = probeStart;
            _coreNum[i] = (short)ProcessorNumber();
            _lateT[i] = (float)((probeStart - _t0Ticks) * 1000.0 / Stopwatch.Frequency / 1000.0);
            ReadMemory(i);

            _frame[i] = Time.frameCount;
            _tStart[i] = Time.unscaledTimeAsDouble - _t0;
            _udt[i] = Time.unscaledDeltaTime * 1000f;
            _dt[i] = Time.deltaTime * 1000f;
            _work[i] = PerfProbeEarly.UpdateStartFrame == Time.frameCount
                ? (float)((probeStart - PerfProbeEarly.UpdateStartTicks) * 1000.0 / Stopwatch.Frequency)
                : -1f;
            _late[i] = (float)((now - Time.unscaledTimeAsDouble) * 1000.0);

            // 전투 시뮬레이터
            var battle = Game.Battle;
            var sim = battle != null ? battle.Sim : null;
            byte flags = 0;
            if (sim != null)
            {
                float simTime = sim.ElapsedTime;
                int steps;
                if (!ReferenceEquals(sim, _prevSim) || _prevSimTime < 0f || simTime < _prevSimTime - 0.0001f) steps = -1; // 새 전투 (시간이 0으로)
                else steps = Mathf.RoundToInt((simTime - _prevSimTime) / BattleSimulator.StepSeconds);
                _prevSim = sim;
                _prevSimTime = simTime;
                _steps[i] = (short)steps;
                _simTime[i] = simTime;
                _acc[i] = _getAcc != null ? _getAcc(sim) * 1000f : -1f;
                _speed[i] = (short)battle.Speed;
                _pscale[i] = battle.PresentationScale;
                if (battle.IsBetweenStages) flags |= 1;
                var enemies = sim.Enemies;
                int count = enemies != null ? enemies.Count : 0;
                _enemies[i] = (short)count;
                _echoes[i] = (short)(sim.Echoes != null ? sim.Echoes.Count : 0);
                int trackId = int.MaxValue;
                Vector2 trackPos = default;
                EnemyState trackState = null;
                for (int k = 0; k < count; k++)
                {
                    var e = enemies[k];
                    if (e == null || !e.Alive) continue;
                    if (e.Id < trackId)
                    {
                        trackId = e.Id;
                        trackPos = e.Position;
                        trackState = e;
                    }
                }
                _trackFlags[i] = TrackFlags(trackState);
                _trackId[i] = trackId == int.MaxValue ? -1 : trackId;
                _trackX[i] = trackPos.x;
                _trackY[i] = trackPos.y;
                _drawX[i] = float.NaN;
                _drawY[i] = float.NaN;
                if (trackId != int.MaxValue && _enemyPool != null && _enemyPool.TryGet(trackId, out var trackView) && trackView != null)
                {
                    var drawn = trackView.Rect.anchoredPosition;
                    _drawX[i] = drawn.x;
                    _drawY[i] = drawn.y;
                }
            }
            else
            {
                _steps[i] = -2;
                _trackFlags[i] = 0;
            }

            // FX 예산 (BattleFx.LateUpdate — 순서 50 — 가 이 프레임 표본을 이미 담았다; 구조체 복사, 할당 없음)
            if (_fx != null)
            {
                var stats = _fx.Stats;
                _fxUpper[i] = (short)Mathf.Clamp(stats.LiveUpper, 0, short.MaxValue);
                _fxGround[i] = (short)Mathf.Clamp(stats.LiveGround, 0, short.MaxValue);
                _fxRejHero[i] = (short)Mathf.Clamp(stats.RejectedHero, 0, short.MaxValue);
            }
            else
            {
                _fxUpper[i] = -1;
                _fxGround[i] = -1;
                _fxRejHero[i] = -1;
            }
            _scriptGc[i] = _threadAllocOk && PerfProbeEarly.UpdateStartFrame == Time.frameCount
                ? (int)Math.Min(int.MaxValue, Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - PerfProbeEarly.UpdateStartAlloc))
                : -1;

            _evT[i] = (short)_evTotal;
            _evK[i] = (short)_evKill;
            _evS[i] = (short)_evSpawn;
            _evH[i] = (short)_evHit;
            _evTotal = _evKill = _evSpawn = _evHit = 0;

            int gc = GC.CollectionCount(0);
            _gc0[i] = gc;
            if (gc != _lastGc)
            {
                Mark("gc_collect_count", (gc - _lastGc).ToString(CultureInfo.InvariantCulture));
                _lastGc = gc;
            }

            if (Application.isFocused) flags |= 2;
            var ui = UIManager.Instance;
            bool popup = ui != null && ui.HasOpenPopup;
            if (popup) flags |= 4;
            if (UIFrame.SheetOpen) flags |= 8;
            if (UIFrame.SheetSettled) flags |= 16;
            _sheetP[i] = UIFrame.SheetProgress;

            // 프로파일러 기록기 (LastValue = 바로 앞 프레임)
            for (int c = 0; c < _core.Length; c++)
            {
                if (!_coreValid[c]) continue;
                long v = _core[c].LastValue;
                _coreCols[c][i] = _coreNs[c] ? v * 1e-6f : v;
            }
            for (int c = 0; c < _scriptCount; c++) _scriptCols[c][i] = _scripts[c].Valid ? _scripts[c].LastValue * 1e-6f : 0f;

            // 자동 저장 · 밤 방문 확인 (GameBootstrap 타이머가 0으로 돌아가면 그 프레임 Update에서 일어났다)
            if (_getSaveTimer != null && GameBootstrap.Instance != null)
            {
                float st = _getSaveTimer(GameBootstrap.Instance);
                if (_lastSaveTimer >= 0f && st < _lastSaveTimer - 0.5f) Mark("autosave", null);
                _lastSaveTimer = st;
                if (_getNightTimer != null)
                {
                    float nt = _getNightTimer(GameBootstrap.Instance);
                    if (_lastNightTimer >= 0f && nt < _lastNightTimer - 0.5f) Mark("night_visit_check", null);
                    _lastNightTimer = nt;
                }
            }

            bool settled = UIFrame.SheetSettled;
            if (settled != _lastSettled)
            {
                Mark(settled ? "sheet_settled" : "sheet_moving", null);
                _lastSettled = settled;
            }

            // 팝업 (2초 넘게 열려 있으면 닫는다 — MAIN 전투를 계속 보게)
            object top = popup ? (object)ui.TopPopup : null;
            if (popup != _popupOpen || !ReferenceEquals(top, _popupTop))
            {
                Mark(popup ? "popup_open" : "popup_closed", popup && top != null ? top.GetType().Name : null);
                _popupOpen = popup;
                _popupTop = top;
                _popupAge = 0f;
            }
            if (popup)
            {
                _popupAge += Time.unscaledDeltaTime;
                if (_popupAge > 2f)
                {
                    CloseAll("auto");
                    _popupAge = 0f;
                }
            }

            // 잠자기 막기 (5초마다 _idle = 0)
            _sleepResetTimer += Time.unscaledDeltaTime;
            if (_sleepResetTimer >= 5f && _sleep != null && _sleepIdle != null)
            {
                _sleepResetTimer = 0f;
                _sleepIdle.SetValue(_sleep, 0f);
                flags |= 32;
            }
            if (_dirtySample && i % 30 == 15)
            {
                try { SampleDirty(); }
                catch (Exception e)
                {
                    _dirtySample = false;
                    Mark("dirty_sampler_off", e.GetType().Name + ": " + e.Message);
                }
            }
            _flags[i] = flags;

            Drive(rel);

            _n = i + 1;
            _probeMs[i] = (float)((Stopwatch.GetTimestamp() - probeStart) * 1000.0 / Stopwatch.Frequency);
            if (rel >= _seconds) _recording = false;
        }

        /// <summary>추적하는 적의 움직임 상태 비트 (frames.csv track_flags — PERF-2 A3가 그 프레임 ±2를 뺀다). 없으면 0</summary>
        internal static byte TrackFlags(EnemyState e)
        {
            if (e == null) return 0;
            int f = 0;
            if (e.SlowTimer > 0f) f |= 1;
            if (e.StunTimer > 0f) f |= 2;
            if (e.HitAnim > 0f) f |= 4;
            if (e.AttackAnim > 0f) f |= 8;
            if (e.SiegeWindup > 0f) f |= 16;
            return (byte)f;
        }

        /// <summary>시나리오: 20초마다 탭 시트 열고(탭 버튼) 4초 뒤 닫기, 75~105초 2배속 (부하 시험은 시트 · 배속을 건드리지 않는다)</summary>
        void Drive(float rel)
        {
            if (_tabs != null && !_bench)
            {
                if (!_sheetOpenCmd && _sheetIndex < SheetTimes.Length && rel >= SheetTimes[_sheetIndex])
                {
                    var tab = SheetTabs[_sheetIndex];
                    string via = PressTab(tab);
                    Mark("sheet_open_cmd", $"{tab} via={via}");
                    _sheetOpenCmd = true;
                    _sheetOpenAt = rel;
                }
                else if (_sheetOpenCmd && rel >= _sheetOpenAt + SheetHold)
                {
                    var tab = SheetTabs[_sheetIndex];
                    string via;
                    if (_tabs.SelectedIndex == (int)tab) via = PressTab(tab);
                    else
                    {
                        UINav.GoToMain();
                        via = $"GoToMain(selected={_tabs.SelectedIndex})";
                    }
                    Mark("sheet_close_cmd", $"{tab} via={via}");
                    _sheetOpenCmd = false;
                    _sheetIndex++;
                }
            }

            if (!_bench && _speedPhase == 0 && rel >= Speed2On)
            {
                Game.Battle.Speed = 2;
                Mark("speed", "2");
                _speedPhase = 1;
            }
            else if (!_bench && _speedPhase == 1 && rel >= Speed2Off)
            {
                Game.Battle.Speed = 1;
                Mark("speed", "1");
                _speedPhase = 2;
            }

            if (_allocCapture)
            {
                if (_allocPhase == 0 && rel >= (_bench ? BenchAllocOn : AllocOn))
                {
                    Profiler.logFile = Path.Combine(_dir, "alloc");
                    Profiler.enableBinaryLog = true;
                    Profiler.enableAllocationCallstacks = true;
                    Profiler.enabled = true;
                    Mark("alloccs", "on");
                    _allocPhase = 1;
                }
                else if (_allocPhase == 1 && rel >= (_bench ? BenchAllocOff : AllocOff))
                {
                    Profiler.enabled = false;
                    Profiler.enableBinaryLog = false;
                    Profiler.logFile = "";
                    Mark("alloccs", "off");
                    _allocPhase = 2;
                }
            }

            if (_noMaskWindow)
            {
                if (_noMaskPhase == 0 && rel >= NoMaskOn)
                {
                    _masks = FindObjectsByType<RectMask2D>(FindObjectsSortMode.None);
                    foreach (var mask in _masks) if (mask != null) mask.enabled = false;
                    Mark("nomask", "on " + _masks.Length);
                    _noMaskPhase = 1;
                }
                else if (_noMaskPhase == 1 && rel >= NoMaskOff)
                {
                    foreach (var mask in _masks) if (mask != null) mask.enabled = true;
                    Mark("nomask", "off");
                    _noMaskPhase = 2;
                }
            }
        }

        /// <summary>UI가 쓰는 길 그대로: 탭 버튼 onClick (MainTabController.OnTabClicked — 열기 / 같은 탭이면 닫기). 버튼이 없으면 Select</summary>
        string PressTab(MainTab tab)
        {
            var button = _tabs.GetTabButton(tab);
            if (button != null && button.isActiveAndEnabled && button.interactable)
            {
                button.onClick.Invoke();
                return "button";
            }
            _tabs.Select(tab);
            return "Select";
        }

        // ═════════════════════════ 사건 ═════════════════════════

        void Mark(string type, string detail)
        {
            _events.Add(new Ev
            {
                T = _recording || _t0 > 0 ? Time.realtimeSinceStartupAsDouble - _t0 : -Time.realtimeSinceStartupAsDouble,
                Frame = Time.frameCount,
                Row = _recording ? _n : -1,
                Type = type,
                Detail = detail,
            });
        }

        void OnBattleEvent(BattleEvent e)
        {
            _evTotal++;
            switch (e.Type)
            {
                case BattleEventType.EnemyKilled:
                    _evKill++;
                    break;
                case BattleEventType.EnemySpawned:
                    _evSpawn++;
                    break;
                case BattleEventType.EnemyHit:
                    _evHit++;
                    break;
                case BattleEventType.EnemyStrike:
                case BattleEventType.GuardianAttack:
                case BattleEventType.EchoAttack:
                case BattleEventType.GuardianPulse:
                case BattleEventType.ShopDamaged:
                case BattleEventType.ShopHealed:
                case BattleEventType.StatusApplied:
                case BattleEventType.EchoHealed:
                    break;
                default:
                    int t = (int)e.Type;
                    Mark("battle", t >= 0 && t < _eventNames.Length ? _eventNames[t] : "?");
                    break;
            }
        }

        void OnStageChanged() => Mark("stage_changed", Game.Battle.StageLabel);

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards) => Mark("stage_ended", outcome.ToString());

        void OnSheetState(bool open) => Mark("sheet_state", open ? "open" : "close");

        void OnSettingsChanged() => Mark("settings_changed", $"fx={GameSettings.FxQuality} fps={Application.targetFrameRate}");

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (_recording)
                {
                    if (type == LogType.Exception) _logExceptions++;
                    else _logErrors++;
                }
                if (_recording) Mark("log_" + type, condition.Length > 160 ? condition.Substring(0, 160) : condition);
                try { File.AppendAllText(Path.Combine(_dir, "errors.txt"), $"[{type}] {condition}\n{stackTrace}\n"); }
                catch (IOException) { }
            }
        }

        // ═════════════════════════ 저장 비용 미세 측정 ═════════════════════════

        IEnumerator SafeBench(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    _benchText.Append("bench_error: ").Append(e).Append('\n');
                    yield break;
                }
                yield return current;
            }
        }

        /// <summary>바로 앞 프레임의 GC Allocated In Frame (기록기가 없으면 -1)</summary>
        long FrameAlloc()
        {
            for (int c = 0; c < _core.Length; c++)
                if (CoreStats[c, 0] == "gc_alloc_b") return _coreValid[c] ? _core[c].LastValue : -1;
            return -1;
        }

        IEnumerator MicroBench()
        {
            var sw = new Stopwatch();
            var boot = GameBootstrap.Instance;
            yield return null;
            yield return null;
            _benchText.Append("bench_idle_frame_alloc_b: ").Append(FrameAlloc()).Append('\n');
            if (boot != null)
            {
                for (int k = 0; k < 5; k++)
                {
                    yield return null;
                    sw.Restart();
                    boot.Save();
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds;
                    yield return null; // 다음 프레임에서 저장 프레임의 할당을 읽는다
                    _benchText.Append("save_ms[").Append(k).Append("]: ").Append(ms.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" frame_alloc_b=").Append(FrameAlloc()).Append('\n');
                }
            }
            if (s_probeSave != null && File.Exists(s_probeSave)) _benchText.Append("save_file_bytes: ").Append(new FileInfo(s_probeSave).Length).Append('\n');

            var json = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json");
            var serialize = json?.GetMethod("SerializeObject", new[] { typeof(object) });
            if (serialize != null && Game.Data != null)
            {
                for (int k = 0; k < 5; k++)
                {
                    yield return null;
                    sw.Restart();
                    var text = (string)serialize.Invoke(null, new object[] { Game.Data });
                    sw.Stop();
                    double ms = sw.Elapsed.TotalMilliseconds;
                    yield return null;
                    _benchText.Append("json_ms[").Append(k).Append("]: ").Append(ms.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" chars=").Append(text.Length).Append(" frame_alloc_b=").Append(FrameAlloc()).Append('\n');
                }
                // 파일 쓰기만 (측정 폴더 · 같은 크기 · Save와 같은 순서: 쓰기 → 복사 → 지우기 → 옮기기)
                var probeText = (string)serialize.Invoke(null, new object[] { Game.Data });
                string scratchFile = Path.Combine(_dir, "bench_io.json");
                for (int k = 0; k < 5; k++)
                {
                    sw.Restart();
                    File.WriteAllText(scratchFile + ".tmp", probeText);
                    if (File.Exists(scratchFile))
                    {
                        File.Copy(scratchFile, scratchFile + ".bak", true);
                        File.Delete(scratchFile);
                    }
                    File.Move(scratchFile + ".tmp", scratchFile);
                    sw.Stop();
                    _benchText.Append("io_only_ms[").Append(k).Append("]: ").Append(sw.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
                }
            }
            else _benchText.Append("json: JsonConvert not found\n");

            _benchText.Append("mono_used_b: ").Append(Profiler.GetMonoUsedSizeLong()).Append('\n');
            _benchText.Append("mono_heap_b: ").Append(Profiler.GetMonoHeapSizeLong()).Append('\n');
            _benchText.Append("gc_total_b: ").Append(GC.GetTotalMemory(false)).Append('\n');
            for (int k = 0; k < 2; k++)
            {
                sw.Restart();
                GC.Collect();
                sw.Stop();
                _benchText.Append("full_gc_ms[").Append(k).Append("]: ").Append(sw.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
            }
            _benchText.Append("gc_total_after_b: ").Append(GC.GetTotalMemory(false)).Append('\n');
        }

        // ═════════════════════════ 쓰기 ═════════════════════════

        void WriteAll()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(_n * 400);
            sb.Append("row,frame,t,udt_ms,dt_ms,work_ms,late_ms,probe_ms,steps,acc_ms,sim_t,speed,pscale,between,focused,popup,sheet_open,sheet_settled,sleep_reset,sheet_p,enemies,echoes,ev_total,ev_kill,ev_spawn,ev_hit,gc0,track_id,track_x,track_y,draw_x,draw_y,cpu_ms,late_interval_ms,core,late_t,page_faults,ws_mb,track_flags,fx_upper,fx_ground,fx_rej_hero,script_gc_b");
            for (int c = 0; c < _core.Length; c++) if (_coreValid[c]) sb.Append(',').Append(CoreStats[c, 0]);
            sb.Append('\n');
            for (int i = 0; i < _n; i++)
            {
                byte f = _flags[i];
                sb.Append(i).Append(',').Append(_frame[i]).Append(',').Append(_tStart[i].ToString("0.######", inv)).Append(',')
                    .Append(_udt[i].ToString("0.####", inv)).Append(',').Append(_dt[i].ToString("0.####", inv)).Append(',')
                    .Append(_work[i].ToString("0.####", inv)).Append(',').Append(_late[i].ToString("0.####", inv)).Append(',')
                    .Append(_probeMs[i].ToString("0.####", inv)).Append(',')
                    .Append(_steps[i]).Append(',').Append(_acc[i].ToString("0.####", inv)).Append(',').Append(_simTime[i].ToString("0.####", inv)).Append(',')
                    .Append(_speed[i]).Append(',').Append(_pscale[i].ToString("0.###", inv)).Append(',')
                    .Append((f & 1) != 0 ? 1 : 0).Append(',').Append((f & 2) != 0 ? 1 : 0).Append(',').Append((f & 4) != 0 ? 1 : 0).Append(',')
                    .Append((f & 8) != 0 ? 1 : 0).Append(',').Append((f & 16) != 0 ? 1 : 0).Append(',').Append((f & 32) != 0 ? 1 : 0).Append(',')
                    .Append(_sheetP[i].ToString("0.###", inv)).Append(',')
                    .Append(_enemies[i]).Append(',').Append(_echoes[i]).Append(',').Append(_evT[i]).Append(',').Append(_evK[i]).Append(',')
                    .Append(_evS[i]).Append(',').Append(_evH[i]).Append(',').Append(_gc0[i]).Append(',').Append(_trackId[i]).Append(',')
                    .Append(_trackX[i].ToString("0.#####", inv)).Append(',').Append(_trackY[i].ToString("0.#####", inv)).Append(',')
                    .Append(_drawX[i].ToString("0.###", inv)).Append(',').Append(_drawY[i].ToString("0.###", inv)).Append(',')
                    .Append(_cpuMs[i].ToString("0.####", inv)).Append(',').Append(_lateIntervalMs[i].ToString("0.####", inv)).Append(',').Append(_coreNum[i]).Append(',').Append(_lateT[i].ToString("0.######", inv))
                    .Append(',').Append(_faults[i]).Append(',').Append(_workingSetMb[i].ToString("0.#", inv))
                    .Append(',').Append(_trackFlags[i]).Append(',').Append(_fxUpper[i]).Append(',').Append(_fxGround[i]).Append(',').Append(_fxRejHero[i])
                    .Append(',').Append(_scriptGc[i]);
                for (int c = 0; c < _core.Length; c++)
                {
                    if (!_coreValid[c]) continue;
                    sb.Append(',').Append(_coreCols[c][i].ToString(_coreNs[c] ? "0.####" : "0", inv));
                }
                sb.Append('\n');
            }
            File.WriteAllText(Path.Combine(_dir, "frames.csv"), sb.ToString());

            // 스크립트별 Update/LateUpdate (한 번이라도 0이 아닌 열만)
            var used = new List<int>();
            for (int c = 0; c < _scriptCount; c++)
            {
                var col = _scriptCols[c];
                for (int i = 0; i < _n; i++)
                {
                    if (col[i] > 0f)
                    {
                        used.Add(c);
                        break;
                    }
                }
            }
            sb.Clear();
            sb.Append("row");
            foreach (int c in used) sb.Append(',').Append('"').Append(_scriptNames[c].Replace("\"", "'")).Append('"');
            sb.Append('\n');
            for (int i = 0; i < _n; i++)
            {
                sb.Append(i);
                foreach (int c in used) sb.Append(',').Append(_scriptCols[c][i].ToString("0.####", inv));
                sb.Append('\n');
            }
            File.WriteAllText(Path.Combine(_dir, "scripts.csv"), sb.ToString());

            sb.Clear();
            sb.Append("t,frame,row,type,detail\n");
            foreach (var e in _events)
            {
                sb.Append(e.T.ToString("0.######", inv)).Append(',').Append(e.Frame).Append(',').Append(e.Row).Append(',').Append(e.Type).Append(',')
                    .Append('"').Append((e.Detail ?? string.Empty).Replace("\"", "'").Replace('\n', ' ')).Append('"').Append('\n');
            }
            File.WriteAllText(Path.Combine(_dir, "events.csv"), sb.ToString());

            sb.Clear();
            foreach (var pair in _env) sb.Append(pair.Key).Append(": ").Append(pair.Value).Append('\n');
            sb.Append("frames: ").Append(_n).Append('\n');
            if (_n > 0)
            {
                var sorted = new float[_n];
                Array.Copy(_udt, sorted, _n);
                Array.Sort(sorted);
                int over20 = 0, over33 = 0;
                double sum = 0;
                for (int i = 0; i < _n; i++)
                {
                    sum += sorted[i];
                    if (sorted[i] > 20f) over20++;
                    if (sorted[i] > 33.4f) over33++;
                }
                sb.Append("udt_mean_ms: ").Append((sum / _n).ToString("0.###", inv)).Append('\n');
                sb.Append("udt_p50_ms: ").Append(sorted[(int)(_n * 0.50)].ToString("0.###", inv)).Append('\n');
                sb.Append("udt_p95_ms: ").Append(sorted[Mathf.Min(_n - 1, (int)(_n * 0.95))].ToString("0.###", inv)).Append('\n');
                sb.Append("udt_p99_ms: ").Append(sorted[Mathf.Min(_n - 1, (int)(_n * 0.99))].ToString("0.###", inv)).Append('\n');
                sb.Append("udt_max_ms: ").Append(sorted[_n - 1].ToString("0.###", inv)).Append('\n');
                sb.Append("udt_over20: ").Append(over20).Append('\n');
                sb.Append("udt_over33: ").Append(over33).Append('\n');
            }
            sb.Append("script_markers_used: ").Append(used.Count).Append('\n');
            sb.Append("-- bench --\n").Append(_benchText);
            sb.Append("-- audio --\n").Append(_audioBench);
            File.WriteAllText(Path.Combine(_dir, "summary.txt"), sb.ToString());
            Debug.Log($"[PerfProbe] 기록 {_n} 프레임 → {_dir}");
        }

        // ═════════════════════════ 설정 되돌리기 ═════════════════════════

        static readonly string[] PrefKeys = { "settings.fx_quality", "settings.fx_quality_auto" };

        static (bool has, int value)[] SnapshotPrefs()
        {
            var snap = new (bool, int)[PrefKeys.Length];
            for (int i = 0; i < PrefKeys.Length; i++) snap[i] = (PlayerPrefs.HasKey(PrefKeys[i]), PlayerPrefs.GetInt(PrefKeys[i], 0));
            return snap;
        }

        static void RestorePrefs((bool has, int value)[] snap)
        {
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                if (snap[i].has) PlayerPrefs.SetInt(PrefKeys[i], snap[i].value);
                else PlayerPrefs.DeleteKey(PrefKeys[i]);
            }
            PlayerPrefs.Save();
        }

        // ═════════════════════════ 도우미 ═════════════════════════

        static void UnlockAll()
        {
            if (!Game.IsMetaActive) return;
            Game.Tutorial.Skip();
            foreach (FeatureId feature in Enum.GetValues(typeof(FeatureId)))
            {
                if (!Game.Features.IsUnlocked(feature)) Game.Features.ForceUnlock(feature, false);
            }
        }

        void CloseAll(string why)
        {
            if (UIManager.Instance == null) return;
            bool had = UIManager.Instance.HasOpenPopup;
            string top = had && UIManager.Instance.TopPopup != null ? UIManager.Instance.TopPopup.GetType().Name : null;
            UIManager.Instance.CloseAllImmediate();
            if (had) Mark("popup_closeall", $"{why} top={top}");
        }

        // ═════════════════════════ 오디오 미세 측정 (AUDIO-SPEC §8) ═════════════════════════

        /// <summary>
        /// UISfx가 매 프레임 부르는 AudioEngine.Tick(dt, unscaledDt)을 120번 (dt 1/60) 직접 불러 호출당 할당 · 시간을 잰다. 엔진은 internal이라 리플렉션으로
        /// 대리자를 한 번 만든다 (호출 자체는 할당 없음). 스레드 할당 API가 안 되면 바이트는 n/a. 기록이 끝난 뒤에만 부른다
        /// </summary>
        void AudioTickBench()
        {
            var sfx = FindAnyObjectByType<UISfx>();
            var field = typeof(UISfx).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic);
            object engine = sfx != null && field != null ? field.GetValue(sfx) : null;
            if (engine == null)
            {
                _audioTickNote = sfx == null ? "UISfx not found" : field == null ? "UISfx._engine not found" : "engine null (audio off)";
                return;
            }
            var method = engine.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float), typeof(float) }, null);
            if (method == null || method.ReturnType != typeof(void))
            {
                _audioTickNote = "AudioEngine.Tick(float, float) not found";
                return;
            }
            var tick = (Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), engine, method);
            const float dt = 1f / 60f;
            for (int k = 0; k < 10; k++) tick(dt, dt); // 데우기
            long a0 = _threadAllocOk ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            long t0 = Stopwatch.GetTimestamp();
            for (int k = 0; k < AudioTickCalls; k++) tick(dt, dt);
            long t1 = Stopwatch.GetTimestamp();
            long a1 = _threadAllocOk ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            _audioTickMsPerCall = (t1 - t0) * 1000.0 / Stopwatch.Frequency / AudioTickCalls;
            _audioTickBytesPerCall = _threadAllocOk ? (double)(a1 - a0) / AudioTickCalls : double.NaN;
            _audioTickNote = "ok";
            var inv = CultureInfo.InvariantCulture;
            _audioBench.Append("audio_tick_calls: ").Append(AudioTickCalls).Append('\n')
                .Append("audio_tick_ms_per_call: ").Append(_audioTickMsPerCall.ToString("0.#####", inv)).Append('\n')
                .Append("audio_tick_alloc_b_per_call: ").Append(_threadAllocOk ? _audioTickBytesPerCall.ToString("0.##", inv) : "n/a").Append('\n');
        }

        // ═════════════════════════ perf_summary.json (SPEC4 §3.1 · §11.3) ═════════════════════════

        /// <summary>정렬한 값의 백분위 (q 0~1, 바닥 순위). 비면 NaN</summary>
        internal static double Pct(List<double> sorted, double q)
        {
            if (sorted == null || sorted.Count == 0) return double.NaN;
            int k = (int)Math.Min(sorted.Count - 1, Math.Max(0, Math.Floor(q * sorted.Count)));
            return sorted[k];
        }

        static double Mean(List<double> values)
        {
            if (values == null || values.Count == 0) return double.NaN;
            double s = 0;
            foreach (var v in values) s += v;
            return s / values.Count;
        }

        static string J(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("0.####", CultureInfo.InvariantCulture);

        static string JS(string s) => "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ") + "\"";

        static string JB(bool? b) => b == null ? "null" : b.Value ? "true" : "false";

        /// <summary>CoreStats 열 이름의 자리 (기록기가 없으면 −1)</summary>
        int CoreIndex(string label)
        {
            if (_core == null) return -1;
            for (int c = 0; c < CoreStats.GetLength(0); c++)
                if (CoreStats[c, 0] == label) return c < _coreValid.Length && _coreValid[c] ? c : -1;
            return -1;
        }

        List<double> CoreValues(string label)
        {
            var list = new List<double>(_n);
            int c = CoreIndex(label);
            if (c < 0) return list;
            for (int i = 0; i < _n; i++)
            {
                double v = _coreCols[c][i];
                if (v >= 0) list.Add(v);
            }
            return list;
        }

        static string Dist(List<double> values)
        {
            var sorted = new List<double>(values);
            sorted.Sort();
            return "{\"n\": " + sorted.Count.ToString(CultureInfo.InvariantCulture) + ", \"mean\": " + J(Mean(sorted)) + ", \"p50\": " + J(Pct(sorted, 0.50)) +
                   ", \"p95\": " + J(Pct(sorted, 0.95)) + ", \"p99\": " + J(Pct(sorted, 0.99)) + ", \"max\": " + J(sorted.Count > 0 ? sorted[sorted.Count - 1] : double.NaN) + "}";
        }

        /// <summary>
        /// 기록 뒤 한 번 (할당 괜찮다): 프레임 간격 · 메인 스레드 바쁜 시간 (Main Thread − WaitForTargetFPS − Gfx.WaitForPresentOnGfxThread, perf analyze.py busy_ms와 같은 정의) ·
        /// Canvas.BuildBatch · WillRenderCanvases · GC/프레임 · Batches · FX 예산 · 오디오 · 예외 · 아틀라스 묶임 → {폴더}/perf_summary.json.
        /// 기록기가 없는 값(Valid false)은 null이고 markers_na에 이름이 든다 ("marker n/a" — 실패가 아니다, SPEC4 §3.1)
        /// </summary>
        void WriteSummaryJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var interval = new List<double>(_n);
            for (int i = 0; i < _n; i++) interval.Add(_udt[i]);
            var main = CoreValues("main_ms");
            var busy = new List<double>(_n);
            int cMain = CoreIndex("main_ms"), cFps = CoreIndex("waitfps_ms"), cPresent = CoreIndex("waitpresent_ms");
            if (cMain >= 0)
            {
                for (int i = 0; i < _n; i++)
                {
                    double v = _coreCols[cMain][i];
                    if (cFps >= 0) v -= _coreCols[cFps][i];
                    if (cPresent >= 0) v -= _coreCols[cPresent][i];
                    busy.Add(Math.Max(0, v));
                }
            }
            var buildBatch = CoreValues("buildbatch_ms");
            var willRender = CoreValues("willrender_ms");
            var gc = CoreValues("gc_alloc_b");
            var gcCollect = CoreValues("gc_collect_ms");
            var batches = CoreValues("batches_n");
            var setpass = CoreValues("setpass_n");
            var drawcalls = CoreValues("drawcalls_n");
            var updateAudio = CoreValues("audio_ms");
            var scriptGc = new List<double>(_n);
            for (int i = 0; i < _n; i++)
                if (_scriptGc[i] >= 0) scriptGc.Add(_scriptGc[i]);
            int gcFrames = 0, collects = 0;
            double collectMax = 0;
            foreach (var v in gc)
                if (v > 0) gcFrames++;
            foreach (var v in gcCollect)
            {
                if (v > 0) collects++;
                collectMax = Math.Max(collectMax, v);
            }

            // FX 예산 (위쪽 · 바닥 동시 입자 — 예산을 넘은 프레임 수, 영웅 층 탈락 합)
            int upperMax = -1, groundMax = -1, upperOver = 0, groundOver = 0, rejHero = 0;
            for (int i = 0; i < _n; i++)
            {
                upperMax = Math.Max(upperMax, _fxUpper[i]);
                groundMax = Math.Max(groundMax, _fxGround[i]);
                if (_particleBudget > 0 && _fxUpper[i] > _particleBudget) upperOver++;
                if (_groundBudget > 0 && _fxGround[i] > _groundBudget) groundOver++;
                if (_fxRejHero[i] > 0) rejHero += _fxRejHero[i];
            }

            // 오디오 스크립트 마커 (프레임마다 합, ms)
            var audio = new List<double>(_n);
            if (_audioMarkers.Count > 0)
            {
                for (int i = 0; i < _n; i++)
                {
                    double s = 0;
                    foreach (int m in _audioMarkers) s += _scriptCols[m][i];
                    audio.Add(s);
                }
            }
            var audioNames = new StringBuilder();
            foreach (int m in _audioMarkers) audioNames.Append(audioNames.Length > 0 ? ", " : string.Empty).Append(JS(_scriptNames[m]));

            // 아틀라스: 불러온 스프라이트 중 묶인 것과 그 페이지 이름 (아틀라스 끔 / 켬 A/B — perf_capture.ps1)
            int spritesSeen = 0, spritesPacked = 0;
            var pages = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite == null) continue;
                spritesSeen++;
                if (!sprite.packed) continue;
                spritesPacked++;
                if (sprite.texture != null) pages.Add(sprite.texture.name);
            }
            var pageNames = new StringBuilder();
            foreach (var page in pages) pageNames.Append(pageNames.Length > 0 ? ", " : string.Empty).Append(JS(page));

            var na = new StringBuilder();
            for (int c = 0; c < CoreStats.GetLength(0); c++)
                if (!_coreValid[c]) na.Append(na.Length > 0 ? ", " : string.Empty).Append(JS(CoreStats[c, 0]));

            var busySorted = new List<double>(busy);
            busySorted.Sort();
            double busyP95 = Pct(busySorted, 0.95);
            double bbMean = Mean(buildBatch);
            double gcMean = Mean(gc);
            var audioSorted = new List<double>(audio);
            audioSorted.Sort();
            double audioMean = Mean(audio);
            bool? audioZero = _threadAllocOk && !double.IsNaN(_audioTickBytesPerCall) ? _audioTickBytesPerCall <= 0.0 : (bool?)null;

            var sb = new StringBuilder(4096);
            sb.Append("{\n");
            sb.Append("  \"probe\": \"PerfProbe RM-3 P4-9\",\n");
            sb.Append("  \"tag\": ").Append(JS(_tag)).Append(",\n");
            sb.Append("  \"scenario\": ").Append(JS(_bench ? "bench" : "scenario150")).Append(",\n");
            sb.Append("  \"stress\": ").Append(JS(_stress)).Append(",\n");
            sb.Append("  \"debug_build\": ").Append(JB(Debug.isDebugBuild)).Append(",\n");
            sb.Append("  \"window\": ").Append(JS(Screen.width.ToString(inv) + "x" + Screen.height.ToString(inv))).Append(",\n");
            sb.Append("  \"vsync\": ").Append(QualitySettings.vSyncCount).Append(", \"target_fps\": ").Append(Application.targetFrameRate).Append(",\n");
            sb.Append("  \"warmup_s\": ").Append(J(_bench ? BenchWarmup : 0)).Append(", \"sampled_s\": ").Append(J(_seconds)).Append(", \"frames\": ").Append(_n).Append(",\n");
            sb.Append("  \"frame_interval_ms\": ").Append(Dist(interval)).Append(",\n");
            sb.Append("  \"frame_busy_ms\": ").Append(Dist(busy)).Append(",\n");
            sb.Append("  \"main_thread_ms\": ").Append(Dist(main)).Append(",\n");
            sb.Append("  \"buildbatch_ms\": ").Append(Dist(buildBatch)).Append(",\n");
            sb.Append("  \"willrender_ms\": ").Append(Dist(willRender)).Append(",\n");
            sb.Append("  \"gc_alloc_b_per_frame\": ").Append(Dist(gc)).Append(",\n");
            sb.Append("  \"gc_frames_with_alloc_pct\": ").Append(J(gc.Count > 0 ? 100.0 * gcFrames / gc.Count : double.NaN)).Append(",\n");
            sb.Append("  \"gc_collect_frames\": ").Append(collects).Append(", \"gc_collect_max_ms\": ").Append(J(collectMax)).Append(",\n");
            sb.Append("  \"script_gc_b_per_frame\": ").Append(_threadAllocOk ? Dist(scriptGc) : "null").Append(",\n");
            sb.Append("  \"batches\": ").Append(Dist(batches)).Append(",\n");
            sb.Append("  \"setpass\": ").Append(Dist(setpass)).Append(",\n");
            sb.Append("  \"drawcalls\": ").Append(Dist(drawcalls)).Append(",\n");
            sb.Append("  \"fx\": {\"quality\": ").Append(JS(_fxQuality)).Append(", \"particle_budget\": ").Append(_particleBudget)
                .Append(", \"ground_budget\": ").Append(_groundBudget).Append(", \"upper_max\": ").Append(upperMax).Append(", \"ground_max\": ").Append(groundMax)
                .Append(", \"upper_over_budget_frames\": ").Append(upperOver).Append(", \"ground_over_budget_frames\": ").Append(groundOver)
                .Append(", \"rejected_hero\": ").Append(rejHero).Append("},\n");
            sb.Append("  \"audio\": {\"markers\": [").Append(audioNames).Append("], \"scripts_ms\": ").Append(Dist(audio))
                .Append(", \"engine_update_audio_ms\": ").Append(Dist(updateAudio))
                .Append(", \"tick_ms_per_call\": ").Append(J(_audioTickMsPerCall)).Append(", \"tick_alloc_b_per_call\": ").Append(J(_audioTickBytesPerCall))
                .Append(", \"tick_note\": ").Append(JS(_audioTickNote)).Append("},\n");
            sb.Append("  \"log_exceptions\": ").Append(_logExceptions).Append(", \"log_errors\": ").Append(_logErrors).Append(",\n");
            sb.Append("  \"atlas\": {\"sprites_loaded\": ").Append(spritesSeen).Append(", \"sprites_packed\": ").Append(spritesPacked)
                .Append(", \"pages\": [").Append(pageNames).Append("]},\n");
            sb.Append("  \"thread_alloc_api\": ").Append(JB(_threadAllocOk)).Append(",\n");
            sb.Append("  \"markers_na\": [").Append(na).Append("],\n");
            sb.Append("  \"targets\": {")
                .Append("\"busy_p95_le_8ms\": ").Append(JB(double.IsNaN(busyP95) ? (bool?)null : busyP95 <= TargetBusyP95Ms))
                .Append(", \"buildbatch_mean_le_0_6ms\": ").Append(JB(double.IsNaN(bbMean) ? (bool?)null : bbMean <= TargetBuildBatchMs))
                .Append(", \"gc_mean_le_64b\": ").Append(JB(double.IsNaN(gcMean) ? (bool?)null : gcMean <= TargetGcBytes))
                .Append(", \"particle_budget_never_exceeded\": ").Append(JB(_fx == null ? (bool?)null : upperOver == 0 && groundOver == 0))
                .Append(", \"exceptions_0\": ").Append(JB(_logExceptions == 0))
                .Append(", \"audio_mean_le_0_15ms\": ").Append(JB(double.IsNaN(audioMean) ? (bool?)null : audioMean <= TargetAudioMs))
                .Append(", \"audio_p95_le_0_15ms\": ").Append(JB(audioSorted.Count == 0 ? (bool?)null : Pct(audioSorted, 0.95) <= TargetAudioMs))
                .Append(", \"audio_tick_0b\": ").Append(JB(audioZero))
                .Append("}\n");
            sb.Append("}\n");
            File.WriteAllText(Path.Combine(_dir, "perf_summary.json"), sb.ToString());
            Debug.Log("[PerfProbe] perf_summary.json " + _tag + " busy_p95=" + J(busyP95) + " buildbatch=" + J(bbMean) + " gc=" + J(gcMean) +
                      " batches=" + J(Mean(batches)) + " audio=" + J(audioMean) + " packed=" + spritesPacked.ToString(inv) + "/" + spritesSeen.ToString(inv));
        }

        static bool HasFlag(string name)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == name) return true;
            return false;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
