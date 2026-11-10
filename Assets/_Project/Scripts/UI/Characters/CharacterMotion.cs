using System;
using System.Collections;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 화면용 짧은 연출 (튀어나오기, 통통 튀기, 페이드, 카드 뒤집기). 모두 unscaled time.
    /// 이징은 UIMotion 하나를 부른다 (M24 — 식은 그대로라 움직임은 바뀌지 않는다).
    /// </summary>
    public static class CharacterMotion
    {
        /// <summary>UIMotion.OutCubic</summary>
        public static float EaseOutCubic(float t) => UIMotion.OutCubic(t);

        /// <summary>UIMotion.InCubic</summary>
        public static float EaseInCubic(float t) => UIMotion.InCubic(t);

        /// <summary>목표를 살짝 넘었다가 돌아오는 곡선 (UIMotion.OutBack, s = 1.70158)</summary>
        public static float EaseOutBack(float t) => UIMotion.OutBack(t);

        /// <summary>반짝이 개수: 움직임 줄이기(UIMotion.Reduced)면 절반 (M64 — 18 → 9, 10 → 5, 8 → 4), 최소 1</summary>
        public static int BurstCount(int count) => UIMotion.Reduced ? Mathf.Max(1, count / 2) : count;

        /// <summary>작은 크기에서 통 튀어나오며 등장</summary>
        public static IEnumerator Pop(Transform target, float duration = 0.24f, float from = 0f)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t / duration));
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        /// <summary>제자리에서 한 번 통 부풀었다 돌아온다 (레벨업, 해금 강조)</summary>
        public static IEnumerator Punch(Transform target, float amount = 0.12f, float duration = 0.22f)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * (1f + amount * Mathf.Sin(t / duration * Mathf.PI));
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

        /// <summary>가로로 접혔다 펴지는 카드 뒤집기. 가운데(옆면)에서 atMiddle을 호출한다.</summary>
        public static IEnumerator Flip(Transform target, float duration, Action atMiddle)
        {
            float half = duration * 0.5f;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                target.localScale = new Vector3(1f - EaseInCubic(t / half), 1f, 1f);
                yield return null;
            }
            target.localScale = new Vector3(0f, 1f, 1f);
            atMiddle?.Invoke();
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                target.localScale = new Vector3(EaseOutBack(t / half), 1f, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
