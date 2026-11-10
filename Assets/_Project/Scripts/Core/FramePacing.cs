using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 프레임 페이싱의 유일한 주인 (perf PF-1). QualitySettings.vSyncCount · Application.targetFrameRate는 여기서만 쓴다.
    /// <para>
    /// 에디터 · PC: vSyncCount 0 + targetFrameRate 60 (설정 30 · 잠자기면 30). Unity 6 소프트웨어 제한기는 정확하다
    /// (perf 측정 b: Time.unscaledDeltaTime p50/p95 16.67/16.80 ms, 1배속에서 한 번 진행한 프레임 98.6%).
    /// vSyncCount가 0이 아니면 PC에서 targetFrameRate가 무시되고, 이 PC는 드라이버 vsync도 프레임을 막지 못해 (Player.log "vsync is broken")
    /// 평균 131 fps로 들쭉날쭉 돌았다 — 1배속에서 57%의 프레임이 전투를 한 칸도 움직이지 못했다 (측정 a).
    /// </para>
    /// <para>
    /// Android · iOS: vSyncCount는 무시된다. targetFrameRate = 화면 주사율 / n (주사율을 나누지 못하는 값은 Unity가 내려서 맞춘다 —
    /// 90 Hz 화면에 60을 주면 45). 90 Hz면 90, 120 Hz면 60, 144 Hz면 72. 잠자기 30은 60 · 90 · 120 Hz 모두 30.
    /// </para>
    /// <para>
    /// 부팅(BeforeSceneLoad) 때 vSyncCount만 0으로 둔다: GameSettings.Apply · SleepModeView가 아직 targetFrameRate를 직접 써도
    /// PC에서 30/60 설정 · 잠자기 30이 실제로 먹는다 (PF-6에서 그 두 곳이 Apply · Sleeping으로 옮겨 온다).
    /// 에디터 플레이 중 QualitySettings에 쓴 값은 프로젝트 설정(현재 품질 단계)에도 남는다 — 남는 값 0은 PF-1 QualitySettings.asset 값과 같다.
    /// Game 창 'VSync (Game view only)'를 켜면 targetFrameRate가 무시된다 (6000.0.3+) — 꺼 둔다.
    /// </para>
    /// </summary>
    public static class FramePacing
    {
        /// <summary>잠자기 모드 목표 프레임</summary>
        public const int SleepFps = 30;
        /// <summary>설정을 읽기 전 기본 목표</summary>
        public const int DefaultFps = 60;
        /// <summary>59.94 Hz도 60으로 친다 (주사율 / 목표가 이 비율 아래로 떨어질 때만 한 칸 더 나눈다)</summary>
        const double Slack = 0.98;

        static bool _sleeping;
        static double _hz = 60.0;
        static float _poll;

        /// <summary>지금 실제로 맞추는 초당 프레임 (FxQualityMonitor 느림 기준 — Application.targetFrameRate 대신 이것을 읽는다)</summary>
        public static int TargetFps { get; private set; } = DefaultFps;

        /// <summary>마지막으로 읽은 화면 주사율 (Hz, 모르면 60)</summary>
        public static double RefreshHz => _hz;

        /// <summary>잠자기 모드 (SleepModeView). 켜면 30, 끄면 설정값 (바뀔 때만 Apply)</summary>
        public static bool Sleeping
        {
            get => _sleeping;
            set
            {
                if (_sleeping == value) return;
                _sleeping = value;
                Apply();
            }
        }

        /// <summary>want 이상을 지키는 화면 갱신 배수 (1~4). 주사율 · 목표가 이상하면 1</summary>
        public static int Divisor(double hz, int want)
        {
            if (!(hz > 20.0) || hz > 1000.0 || want <= 0) return 1;
            return Mathf.Clamp((int)Math.Floor(hz / (want * Slack)), 1, 4);
        }

        /// <summary>실제로 맞출 초당 프레임 (순수 함수). 모바일 = round(주사율 / Divisor), 그 밖(PC · 에디터 제한기) = want</summary>
        public static int EffectiveFps(double hz, int want, bool mobile)
        {
            if (want <= 0) want = DefaultFps;
            if (!mobile || !(hz > 20.0) || hz > 1000.0) return want;
            return Mathf.Max(1, (int)Math.Round(hz / Divisor(hz, want)));
        }

        /// <summary>vSyncCount · targetFrameRate를 맞춘다 (GameSettings.Apply · 잠자기 켜고 끄기 · 주사율이 바뀌었을 때). 할당 없음</summary>
        public static void Apply()
        {
            _hz = ReadRefreshHz();
            int want = _sleeping ? SleepFps : GameSettings.FrameRate;
            bool mobile = Application.isMobilePlatform;
            int fps = EffectiveFps(_hz, want, mobile);
            if (!mobile) VSyncOff();
            if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
            TargetFps = fps;
        }

        /// <summary>
        /// 매 프레임 (GameBootstrap.Update): 1초에 한 번 주사율을 다시 읽고 바뀌었으면 다시 맞춘다 (모바일 가변 주사율). 할당 없음.
        /// PC는 제한기가 화면과 상관없이 맞추므로 보지 않는다
        /// </summary>
        public static void Tick(float unscaledDt)
        {
            _poll -= unscaledDt;
            if (_poll > 0f) return;
            _poll = 1f;
            if (!Application.isMobilePlatform) return;
            if (Math.Abs(ReadRefreshHz() - _hz) > 0.5) Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // 도메인을 다시 읽지 않고 플레이에 들어가도(Enter Play Mode 옵션) 지난 플레이 값을 들고 오지 않게
            _sleeping = false;
            _poll = 0f;
            _hz = 60.0;
            TargetFps = DefaultFps;
            if (!Application.isMobilePlatform) VSyncOff();
        }

        static void VSyncOff()
        {
            if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0;
        }

        static double ReadRefreshHz()
        {
            double hz = Screen.currentResolution.refreshRateRatio.value;
            return hz > 20.0 && hz < 1000.0 ? hz : 60.0;
        }
    }
}
