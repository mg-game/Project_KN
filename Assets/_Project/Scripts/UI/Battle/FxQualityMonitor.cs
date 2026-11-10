using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 실행 중 이펙트 품질 자동 판정 (ATK-06 / CR-08): 전투 화면에서 5초 평균 프레임 시간이 22ms(약 45fps)를 넘는 구간이
    /// 60초 안에 2번 생기면 "한 단계 낮추자"고 알린다 (BattleView가 GameSettings.LowerFxQualityAuto + 토스트 한 번).
    /// 앱 전환·로딩처럼 0.25초를 넘는 한 프레임은 세지 않는다. 자동으로 다시 올리지는 않는다. 순수 C# (할당 없음, 테스트 가능).
    /// 일부러 낮춘 프레임 목표(설정 30fps · 잠자기 모드)는 느림이 아니다: 느린 기준은 목표 프레임 시간 × 1.25 (22ms보다 짧아지지 않는다),
    /// 목표가 바뀌면 처음부터 다시 센다.
    /// </summary>
    public sealed class FxQualityMonitor
    {
        /// <summary>평균을 내는 구간 (초)</summary>
        public const float Window = 5f;
        /// <summary>이보다 느린 평균 프레임이면 느린 구간 (초)</summary>
        public const float SlowFrame = 0.022f;
        /// <summary>느린 구간 두 번을 묶어 보는 시간 (초)</summary>
        public const float Span = 60f;
        public const int StrikesToLower = 2;
        /// <summary>이보다 긴 한 프레임은 세지 않는다 (초)</summary>
        public const float MaxFrame = 0.25f;

        /// <summary>목표 프레임 시간보다 이만큼 느려야 느린 구간 (목표 30fps면 41.7ms)</summary>
        public const float TargetSlack = 1.25f;

        float _clock;
        float _windowTime;
        int _windowFrames;
        float _firstStrike;
        int _strikes;
        int _target;

        /// <summary>지금까지 센 느린 구간 수 (Span 안)</summary>
        public int Strikes => _strikes;

        /// <summary>느린 구간으로 보는 평균 프레임 시간 (초). targetFrameRate가 0 이하면 22ms, 아니면 max(22ms, 1.25 / 목표)</summary>
        public static float SlowThreshold(int targetFrameRate) =>
            targetFrameRate > 0 ? Mathf.Max(SlowFrame, TargetSlack / targetFrameRate) : SlowFrame;

        /// <summary>
        /// 한 프레임을 기록한다. 품질을 한 단계 낮춰야 하면 true (그 뒤는 처음부터 다시 센다).
        /// targetFrameRate = 지금 프레임 목표 (설정 30/60 · 잠자기 30, 0 = 모름 → 22ms 기준). 목표가 바뀌면 처음부터 다시 센다
        /// </summary>
        public bool Sample(float dt, int targetFrameRate = 0)
        {
            if (targetFrameRate != _target)
            {
                Reset();
                _target = targetFrameRate;
            }
            if (!(dt > 0f) || dt > MaxFrame) return false;
            _clock += dt;
            _windowTime += dt;
            _windowFrames++;
            if (_windowTime < Window) return false;

            float average = _windowTime / Mathf.Max(1, _windowFrames);
            _windowTime = 0f;
            _windowFrames = 0;
            if (average <= SlowThreshold(_target)) return false;

            if (_strikes > 0 && _clock - _firstStrike > Span) _strikes = 0;
            if (_strikes == 0) _firstStrike = _clock;
            _strikes++;
            if (_strikes < StrikesToLower) return false;
            Reset();
            return true;
        }

        /// <summary>처음부터 다시 센다 (전투 화면을 다시 볼 때, 품질·프레임 설정을 바꾼 뒤). 마지막으로 본 프레임 목표는 남긴다</summary>
        public void Reset()
        {
            _clock = 0f;
            _windowTime = 0f;
            _windowFrames = 0;
            _firstStrike = 0f;
            _strikes = 0;
        }
    }
}
