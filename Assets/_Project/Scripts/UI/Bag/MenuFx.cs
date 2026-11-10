using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 메뉴 화면 공용 짧은 연출 (등장 팝, 강조 펀치, 페이드, 게이지 채우기, 깜빡임). 모두 unscaled 시간 기준.
    /// 이징은 UIMotion 하나만 쓴다 (EaseOutCubic · EaseOutBack은 UIMotion.OutCubic · OutBack을 부르는 한 줄 — 같은 식).
    /// 공개 이름 · 인자는 그대로 둔다 (MainTabController · MailPopup · 튜토리얼 뷰가 부른다, Wave 2 C-08).
    /// </summary>
    public static class MenuFx
    {
        /// <summary>깜빡임을 줄일 때(UIMotion.Reduced) 한 번 · 최소 주기 · 최대 알파 (§1.9)</summary>
        const int ReducedFlashCount = 1;
        const float ReducedFlashMinPeriod = 0.6f;
        const float ReducedFlashMaxAlpha = 0.3f;

        public static float EaseOutCubic(float k) => UIMotion.OutCubic(k);

        public static float EaseOutBack(float k) => UIMotion.OutBack(k);

        /// <summary>진행 막대 채움을 목표로 옮긴다 (UIMotion.EaseFill — 줄어들면 바로, 늘면 큰 차이일수록 빨리)</summary>
        public static float EaseFill(float current, float target, float dt) => UIMotion.EaseFill(current, target, dt);

        /// <summary>코루틴을 다시 시작한다 (이전 것은 멈춤). 꺼져 있으면 아무것도 하지 않는다.</summary>
        public static void Restart(MonoBehaviour host, ref Coroutine handle, IEnumerator routine)
        {
            if (handle != null) host.StopCoroutine(handle);
            handle = host.isActiveAndEnabled ? host.StartCoroutine(routine) : null;
        }

        /// <summary>0에서 살짝 넘치게 커졌다가 1로 (등장)</summary>
        public static IEnumerator PopIn(Transform target, float duration = 0.24f)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * EaseOutBack(t / duration);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        /// <summary>현재 크기에서 한 번 툭 커졌다 돌아오기 (강조). 기본 = UIMotion 펀치 M (0.12, 0.22초)</summary>
        public static IEnumerator Punch(Transform target, float strength = UIMotion.PunchM, float duration = UIMotion.PunchDuration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                target.localScale = Vector3.one * (1f + strength * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t / duration));
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>Filled 이미지의 채움 정도를 부드럽게 바꾼다 (요격 범위 부채꼴이 펼쳐지는 연출 등)</summary>
        public static IEnumerator FillTo(Image image, float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                image.fillAmount = Mathf.Lerp(from, to, EaseOutCubic(t / duration));
                yield return null;
            }
            image.fillAmount = to;
        }

        /// <summary>
        /// 그래픽을 count번 깜빡여 시선을 끈다 (획득처 이동 후 강조).
        /// 움직임 줄이기(UIMotion.Reduced · 설정 '번쩍임 줄이기')면 한 번만, 주기 0.6초 이상, 알파 0.3 이하로 (§1.9).
        /// </summary>
        public static IEnumerator Flash(Graphic graphic, Color color, int count = 3, float period = 0.5f)
        {
            if (UIMotion.Reduced)
            {
                count = Mathf.Min(count, ReducedFlashCount);
                period = Mathf.Max(period, ReducedFlashMinPeriod);
                color.a = Mathf.Min(color.a, ReducedFlashMaxAlpha);
            }
            graphic.gameObject.SetActive(true);
            for (float t = 0; t < count * period; t += Time.unscaledDeltaTime)
            {
                float wave = Mathf.Sin(t / period * Mathf.PI);
                graphic.color = new Color(color.r, color.g, color.b, color.a * wave * wave);
                yield return null;
            }
            graphic.gameObject.SetActive(false);
        }
    }
}
