using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>진동 세기 (Haptics.Play)</summary>
    public enum HapticKind
    {
        /// <summary>막힌 구매·잠긴 기능을 눌렀을 때 (10ms, 세기 50 — 옛 기기는 건너뜀)</summary>
        Light = 0,
        /// <summary>불 밝히기, 레벨업 팡파르 (18ms, 세기 110)</summary>
        Medium = 1,
        /// <summary>보스 처치, 높은 등급 소환 공개 (30ms, 세기 200)</summary>
        Heavy = 2,
    }

    /// <summary>
    /// 짧은 진동 (M18 Phase A, FX-05). 동화 같은 분위기에 맞게 아주 짧고 약하게만 쓴다.
    ///   · 설정 "진동"(GameSettings.Haptics)이 꺼져 있으면 아무것도 하지 않는다.
    ///   · 80ms 안에 한 번, 강한 진동(Heavy)은 400ms 안에 한 번까지 (HapticGate) — 연달아 오는 사건이 떨림으로 뭉개지지 않게.
    ///   · 그냥 누르기에는 쓰지 않는다. 쓰는 곳: 불 밝힘(Medium), 보스 처치(Heavy), 높은 등급 소환 공개(Heavy), 레벨업 팡파르(Medium),
    ///     막힌 구매·잠긴 기능(Light — FeatureLockCover, TabGateView, EchoCardView.Deny, CostButtonView.Deny, 오류 토스트).
    ///   · Android: VibrationEffect.createOneShot(ms, 세기). SDK 26 미만은 세기 조절이 없어 vibrate(ms)를 Medium·Heavy에만 쓴다.
    ///     VIBRATE 권한은 프로젝트가 사용자 매니페스트를 쓰지 않아 Unity가 Handheld.Vibrate 참조를 보고 넣는다 (빌드 때 링커가 쓰인다고 본
    ///     기능 목록으로 판단). Handheld.Vibrate 자체는 0.4초 넘게 길게 떨려 "아주 짧게" 규칙(M18)에 맞지 않으므로 실행하지 않는다 —
    ///     참조만 남긴다 (_useUnityVibrateFallback). Android 첫 빌드에서 aapt dump permissions로 android.permission.VIBRATE를 확인한다.
    ///   · iOS·에디터·PC: 아무것도 하지 않는다 (게이트와 설정은 똑같이 적용).
    /// 매 프레임 부르지 않는다 (사건 때만). Android 호출은 사건마다 작은 Java 객체 하나를 만든다.
    /// </summary>
    public static class Haptics
    {
        /// <summary>앱 전체가 쓰는 게이트 (테스트는 새 HapticGate를 만들어 쓴다)</summary>
        static readonly HapticGate Gate = new();

        /// <summary>
        /// 플랫폼 진동 대신 부를 함수 (4차 훅, FX-05 iOS 백엔드 — P4-7의 Platform 어셈블리가 넣는다). 설정 · 게이트는 그대로 적용된다.
        /// null = 지금처럼 (Android 경로 · 그 밖은 아무것도 하지 않음)
        /// </summary>
        public static Action<HapticKind> PlatformOverride;

        /// <summary>진동한다 (설정·게이트를 통과하면). 실제로 진동을 요청했으면 true (에디터·iOS는 게이트를 통과해도 false — PlatformOverride가 있으면 true)</summary>
        public static bool Play(HapticKind kind)
        {
            if (!GameSettings.Haptics) return false;
            if (!Gate.TryPass(kind, Time.realtimeSinceStartup)) return false;
            var platform = PlatformOverride;
            if (platform != null)
            {
                platform(kind);
                return true;
            }
            return Vibrate(kind);
        }

        /// <summary>진동 길이 (ms)</summary>
        public static long DurationMs(HapticKind kind) => kind switch
        {
            HapticKind.Heavy => 30,
            HapticKind.Medium => 18,
            _ => 10,
        };

        /// <summary>진동 세기 (1~255, Android VibrationEffect)</summary>
        public static int Amplitude(HapticKind kind) => kind switch
        {
            HapticKind.Heavy => 200,
            HapticKind.Medium => 110,
            _ => 50,
        };

        /// <summary>SDK 26 미만(세기 조절 없음)에서도 진동하는지 — 가벼운 진동은 옛 기기에서 너무 세게 느껴져 건너뛴다</summary>
        public static bool PlaysWithoutAmplitude(HapticKind kind) => kind != HapticKind.Light;

#if UNITY_ANDROID && !UNITY_EDITOR
        const int AmplitudeApiLevel = 26;
        static AndroidJavaObject _vibrator;
        static AndroidJavaClass _effectClass;
        static int _sdk;
        static bool _unavailable;
        /// <summary>
        /// 늘 false — Unity 기본 진동(Handheld.Vibrate, 길게 떨림)은 쓰지 않는다. 아래 catch의 호출은 VIBRATE 권한이 매니페스트에 들어가도록
        /// 참조로만 남긴 것이다. const·readonly로 두면 컴파일러·링커가 그 호출을 지워 권한까지 빠질 수 있어 일반 static 필드로 둔다.
        /// </summary>
        static bool _useUnityVibrateFallback;

        static bool Vibrate(HapticKind kind)
        {
            if (_unavailable) return false;
            try
            {
                if (_vibrator == null && !Connect()) return false;
                long ms = DurationMs(kind);
                if (_sdk >= AmplitudeApiLevel)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, Amplitude(kind)))
                        _vibrator.Call("vibrate", effect);
                    return true;
                }
                if (!PlaysWithoutAmplitude(kind)) return false;
                _vibrator.Call("vibrate", ms);
                return true;
            }
            catch (Exception e)
            {
                // 진동 서비스를 못 쓰는 기기: 다시 시도하지 않고 조용히 끈다 (긴 Unity 기본 진동으로 대신하지 않는다 — M18)
                _unavailable = true;
                Debug.LogWarning($"[Haptics] 진동을 쓸 수 없어 끕니다: {e.Message}");
                if (_useUnityVibrateFallback) Handheld.Vibrate(); // 실행되지 않음: VIBRATE 권한용 참조
                return false;
            }
        }

        /// <summary>Vibrator 서비스와 SDK 버전을 한 번 읽어 둔다. 진동기가 없으면 false (다시 시도하지 않음)</summary>
        static bool Connect()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _vibrator = activity?.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator"))
            {
                _vibrator?.Dispose();
                _vibrator = null;
                _unavailable = true;
                return false;
            }
            if (_sdk >= AmplitudeApiLevel) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            return true;
        }
#else
        static bool Vibrate(HapticKind kind) => false;
#endif
    }

    /// <summary>
    /// 진동 간격 제한 (순수 C#, 시각은 부르는 쪽이 준다): 아무 진동이나 MinInterval 안에 한 번, Heavy는 HeavyInterval 안에 한 번.
    /// 막힌 진동은 시각을 기록하지 않는다 (막혔다고 다음 진동이 더 밀리지 않게).
    /// </summary>
    public sealed class HapticGate
    {
        /// <summary>진동 사이 최소 간격 (초)</summary>
        public const float MinInterval = 0.08f;
        /// <summary>강한 진동 사이 최소 간격 (초)</summary>
        public const float HeavyInterval = 0.4f;

        float _last = float.NegativeInfinity;
        float _lastHeavy = float.NegativeInfinity;

        /// <summary>지금(now, 초) 진동해도 되면 true (그리고 시각을 기록)</summary>
        public bool TryPass(HapticKind kind, float now)
        {
            if (now - _last < MinInterval) return false;
            if (kind == HapticKind.Heavy && now - _lastHeavy < HeavyInterval) return false;
            _last = now;
            if (kind == HapticKind.Heavy) _lastHeavy = now;
            return true;
        }

        public void Reset()
        {
            _last = float.NegativeInfinity;
            _lastHeavy = float.NegativeInfinity;
        }
    }
}
