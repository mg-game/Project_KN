// IdleGame.Platform 어셈블리(Android · iOS 전용). iOS 기기 빌드에서만 본문이 있다 (FX-05 iOS 백엔드, SPEC4 §13.5 g — 스트레치).
#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IdleGame.Platform
{
    /// <summary>
    /// iOS 짧은 진동 (FX-05): Plugins/iOS/LLHaptics.mm의 UIImpactFeedbackGenerator (Light · Medium · Heavy, 종류마다 하나씩 만들어 prepare).
    /// 장면을 읽기 전에 Haptics.PlatformOverride에 넣는다 — 설정 '진동'과 HapticGate(80 ms · Heavy 400 ms)는 Haptics.Play가 그대로 적용한다.
    /// 기기에서만 확인할 수 있다 (SPEC4 §11.5 #6). 문제가 생기면 Enabled를 false로 바꾸면 예전처럼 아무것도 하지 않는다.
    /// </summary>
    public static class IosHapticsBackend
    {
        /// <summary>스트레치 스위치: false면 등록하지 않는다 (iOS는 예전처럼 진동 없음)</summary>
        public static readonly bool Enabled = true;

        static bool _failed;

        [DllImport("__Internal")]
        static extern void LLHaptics_Impact(int style);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (!Enabled || Haptics.PlatformOverride != null) return;
            Haptics.PlatformOverride = Impact;
        }

        static void Impact(HapticKind kind)
        {
            if (_failed) return;
            try
            {
                LLHaptics_Impact((int)kind);
            }
            catch (Exception e)
            {
                // 네이티브 함수를 찾지 못한 빌드 등: 한 번 알리고 끈다 (게임은 그대로)
                _failed = true;
                Debug.LogWarning($"[Haptics] iOS 진동을 쓸 수 없어 꺼요: {e.Message}");
            }
        }
    }
}
#endif
