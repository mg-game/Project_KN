using System;
using System.Collections;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 탭 자동 이동 (UI_InGame §4.3 공통 규칙) 공용 부품. 탭과 [모두 받기]가 함께 있는 팝업이 하나씩 들고 쓴다 (수선 일지 · 친구 도감).
    /// 새 탭 팝업도 이 부품을 쓴다 (화면마다 따로 짜지 않는다).
    ///   · [모두 받기]로 받은 뒤 Schedule → UIMotion.AutoTabDelay (0.9초, 움직임 줄이기 0.4초) 기다림 → 팝업이 넘길 탭을 고른다 (NextTab).
    ///   · 줄의 [받기] · 칸 하나 받기는 부르지 않는다 (골라 받는 중이라 화면을 바꾸지 않는다).
    ///   · 기다리는 동안 화면을 누르면 취소 (팝업 Update에서 Tick). 튜토리얼 중이면 걸지 않는다.
    ///   · 넘어가기 직전 다시 확인: 팝업이 열려 있고 맨 위 팝업이며 튜토리얼 중이 아닐 때만 (기다리는 사이 기능 개방 카드 등이 위에 뜨면 넘기지 않는다).
    ///   · 도착한 탭 글자 1 → 1.08 → 1 (UIMotion.AutoTabPulse 0.3초, 줄이기면 없음). 버튼 루트는 누름 연출과 싸우므로 글자만 키운다.
    ///   · 팝업은 탭을 직접 바꿀 때와 닫을 때 Stop을 부른다.
    /// </summary>
    public sealed class AutoTabRunner
    {
        readonly UIPopup _host;
        Coroutine _wait;
        /// <summary>건 프레임 (그 프레임의 누름은 [모두 받기] 자체라 취소로 보지 않는다)</summary>
        int _scheduledFrame = -1;
        Coroutine _pulse;
        Transform _pulsed;

        public AutoTabRunner(UIPopup host)
        {
            _host = host;
        }

        /// <summary>넘어가기를 기다리는 중인지</summary>
        public bool IsWaiting => _wait != null;

        /// <summary>
        /// [모두 받기] 뒤 넘어갈 탭 (순수): 지금 탭에 받을 것이 남았으면 −1 (머문다).
        /// 아니면 화면 순서(order)로 지금 탭 오른쪽부터 한 바퀴 돌며 보이고 받을 것이 있는 첫 탭, 없으면 −1.
        /// visible · claimable은 탭 값으로 찾는다 (visible이 null이면 모두 보임)
        /// </summary>
        public static int NextTab(int current, int[] order, bool[] visible, int[] claimable)
        {
            if (order == null || claimable == null || current < 0 || current >= claimable.Length) return -1;
            if (claimable[current] > 0) return -1;
            int at = Array.IndexOf(order, current);
            if (at < 0) return -1;
            for (int step = 1; step < order.Length; step++)
            {
                int tab = order[(at + step) % order.Length];
                if (Visible(visible, tab) && tab < claimable.Length && claimable[tab] > 0) return tab;
            }
            return -1;
        }

        /// <summary>처음 열 탭 (순수): 화면 순서로 보이고 받을 것이 있는 첫 탭, 없으면 fallback</summary>
        public static int FirstTab(int[] order, bool[] visible, int[] claimable, int fallback)
        {
            if (order == null || claimable == null) return fallback;
            foreach (int tab in order)
            {
                if (Visible(visible, tab) && tab >= 0 && tab < claimable.Length && claimable[tab] > 0) return tab;
            }
            return fallback;
        }

        static bool Visible(bool[] visible, int tab) => visible == null || (tab >= 0 && tab < visible.Length && visible[tab]);

        static bool TutorialRunning => Game.Tutorial != null && Game.Tutorial.IsActive;

        /// <summary>[모두 받기]로 받은 뒤 부른다. 기다림이 끝나고 넘어가도 될 때만 onElapsed (팝업이 탭을 고르고 SelectTab · Pulse)</summary>
        public void Schedule(Action onElapsed)
        {
            Cancel();
            if (_host == null || !_host.isActiveAndEnabled || TutorialRunning) return;
            _scheduledFrame = Time.frameCount;
            _wait = _host.StartCoroutine(Wait(onElapsed));
        }

        /// <summary>기다림만 취소</summary>
        public void Cancel()
        {
            if (_wait != null && _host != null) _host.StopCoroutine(_wait);
            _wait = null;
        }

        /// <summary>기다림 취소 + 반짝임 멈춤 (탭을 직접 바꿀 때 · 닫을 때)</summary>
        public void Stop()
        {
            Cancel();
            StopPulse();
        }

        /// <summary>팝업 Update에서 매 프레임: 기다리는 동안 화면을 누르면 지금 탭에 머문다</summary>
        public void Tick()
        {
            if (_wait != null && Time.frameCount != _scheduledFrame && PointerPressedThisFrame()) Cancel();
        }

        IEnumerator Wait(Action onElapsed)
        {
            float wait = UIMotion.Reduced ? UIMotion.AutoTabDelayReduced : UIMotion.AutoTabDelay;
            for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;
            _wait = null;
            if (!CanMove()) yield break;
            onElapsed?.Invoke();
        }

        bool CanMove()
        {
            if (_host == null || !_host.IsOpen || !MetaUi.IsActive || TutorialRunning) return false;
            var ui = UIManager.Instance;
            return ui == null || ui.TopPopup == _host;
        }

        /// <summary>도착한 탭 글자가 한 번 톡 (줄이기면 없음)</summary>
        public void Pulse(Component label)
        {
            if (label == null || UIMotion.Reduced || _host == null || !_host.isActiveAndEnabled) return;
            StopPulse();
            _pulsed = label.transform;
            _pulse = _host.StartCoroutine(PulseRoutine(_pulsed));
        }

        IEnumerator PulseRoutine(Transform target)
        {
            for (float t = 0f; t < UIMotion.AutoTabPulse; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * (1f + (UIMotion.AutoTabPulseScale - 1f) * MetaUi.Punch(t / UIMotion.AutoTabPulse));
                yield return null;
            }
            target.localScale = Vector3.one;
            _pulse = null;
            _pulsed = null;
        }

        void StopPulse()
        {
            if (_pulse != null && _host != null) _host.StopCoroutine(_pulse);
            _pulse = null;
            if (_pulsed != null) _pulsed.localScale = Vector3.one;
            _pulsed = null;
        }

        /// <summary>이번 프레임에 화면을 눌렀는지 (예전 입력 방식, AttendancePopup과 같은 판정)</summary>
        public static bool PointerPressedThisFrame()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            }
            return false;
        }
    }
}
