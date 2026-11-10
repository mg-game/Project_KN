using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 공개 박자 (CR-04, SPEC4 §7.5) — 장면 · Game 없이 시간만으로 정해지는 순수 규칙 (GachaRevealTimingTests).
    /// <para>
    /// 1회: 0.00–0.30 분실물 꾸러미가 y+320에서 떨어진다 (OutBack s=1.4) → 0.30–0.70 ±6° 세 번 흔들림 (실제 최고 등급이 ★4 이상이면 ×1.6 +
    /// 그 등급 빛) → 0.70–1.05 열림 (열린 그림 + 등급 빛 터짐) → 1.05–1.35 초상화 그림자(0.1, 0.08, 0.18) → 흰색 → 대표 결과.
    /// </para>
    /// <para>
    /// 10회: 꾸러미가 0.04초 간격으로 나타난 뒤(하나 0.12초) 차례로 0.08초에 뒤집힌다. 뒤집기 사이 TenFlipStep(0.085초)은 공개 소리가
    /// 1초에 12번을 넘지 않게 한 값이다. ★4 이상은 뒤집기를 멈추고 1.6초 크게 보여 준다 (1회 박자의 0.70초부터를 앞 0.9초에 맞춰 줄이고 나머지는 머무름).
    /// 중복은 뒤집힌 뒤 0.4초 동안 기억편이 요약 줄로 날아간다. ★4 이상이 없으면 4초 안에 끝난다.
    /// </para>
    /// 건너뛰기 [건너뛰기 ›]는 0.5초부터 보인다. 예고(흔들림 빛)는 실제 등급보다 높게 보이지 않는다 (★4 미만은 예고 없음).
    /// 움직임 줄이기면 흔들림 없음 · 별가루 절반.
    /// </summary>
    public static class GachaRevealTiming
    {
        // ───────────── 1회 ─────────────

        /// <summary>꾸러미가 다 떨어진 때 (초)</summary>
        public const float DropEnd = 0.30f;
        /// <summary>흔들림이 끝나고 열리기 시작하는 때</summary>
        public const float ShakeEnd = 0.70f;
        /// <summary>다 열린 때 (초상화 그림자가 밝아지기 시작)</summary>
        public const float OpenEnd = 1.05f;
        /// <summary>초상화가 흰색이 된 때 = 1회 공개 끝 (대표 결과가 뜬다)</summary>
        public const float SilhouetteEnd = 1.35f;
        /// <summary>1회 공개 전체 길이</summary>
        public const float SingleDuration = SilhouetteEnd;
        /// <summary>떨어지기 시작하는 높이 (px, 제자리 위)</summary>
        public const float DropHeight = 320f;
        /// <summary>떨어질 때 OutBack 넘침 (s)</summary>
        public const float DropOvershoot = 1.4f;
        /// <summary>흔들림 폭 (도)</summary>
        public const float ShakeDegrees = 6f;
        /// <summary>흔들림 횟수 (0.30–0.70 사이)</summary>
        public const int ShakeCycles = 3;
        /// <summary>★4 이상일 때 흔들림 배율</summary>
        public const float HighShakeScale = 1.6f;

        // ───────────── 10회 ─────────────

        /// <summary>꾸러미가 하나씩 나타나는 간격 (초)</summary>
        public const float TenAppearStagger = 0.04f;
        /// <summary>꾸러미 하나가 튀어나오는 시간</summary>
        public const float TenAppearPop = 0.12f;
        /// <summary>한 장 뒤집기 (초)</summary>
        public const float TenFlip = 0.08f;
        /// <summary>뒤집기 사이 (1/0.085 ≈ 11.8 — 공개 소리 1초에 12번 이하)</summary>
        public const float TenFlipStep = 0.085f;
        /// <summary>★4 이상 한 장을 크게 보여 주는 시간</summary>
        public const float HighReveal = 1.6f;
        /// <summary>크게 보여 주는 동안 1회 박자(0.70 → 1.35)를 맞춰 넣는 앞부분 (나머지는 머무름)</summary>
        public const float HighRevealBeats = 0.9f;
        /// <summary>중복 한 장이 기억편으로 녹아 요약 줄로 가는 시간</summary>
        public const float DupMelt = 0.4f;
        /// <summary>마지막 뒤집기 뒤 쉼 (마지막 녹음이 닿을 만큼)</summary>
        public const float Settle = 0.45f;
        /// <summary>★4 이상이 없는 10회의 상한 (초)</summary>
        public const float MaxTenWithoutHigh = 4f;

        // ───────────── 공통 ─────────────

        /// <summary>[건너뛰기 ›]가 나타나는 때 (초)</summary>
        public const float SkipAppear = 0.5f;
        /// <summary>초상화 그림자 색 (1.05초에서 흰색으로)</summary>
        public static readonly Color SilhouetteTint = new(0.1f, 0.08f, 0.18f, 1f);

        /// <summary>크게 보여 주는 등급 (★4 전설 이상)</summary>
        public static bool IsHigh(ItemGrade grade) => grade >= ItemGrade.Legendary;

        /// <summary>건너뛰기 버튼이 보일 때인지</summary>
        public static bool SkipVisible(float elapsed) => elapsed >= SkipAppear;

        /// <summary>공개 소리가 1초에 나는 최대 횟수 (10회 뒤집기 간격으로 정해진다)</summary>
        public static float MaxRevealSoundsPerSecond => 1f / TenFlipStep;

        // ───────────── 1회 박자 ─────────────

        /// <summary>1회 공개 단계</summary>
        public enum Beat
        {
            Drop = 0,
            Shake = 1,
            Open = 2,
            Silhouette = 3,
            Done = 4,
        }

        /// <summary>t초의 1회 공개 단계</summary>
        public static Beat SingleBeat(float t)
        {
            if (t < DropEnd) return Beat.Drop;
            if (t < ShakeEnd) return Beat.Shake;
            if (t < OpenEnd) return Beat.Open;
            if (t < SilhouetteEnd) return Beat.Silhouette;
            return Beat.Done;
        }

        /// <summary>꾸러미가 제자리보다 얼마나 위에 있는지 (px, 0.30초부터 0). OutBack이라 잠깐 0 아래로 넘쳤다 돌아온다</summary>
        public static float DropOffset(float t)
        {
            if (t <= 0f) return DropHeight;
            if (t >= DropEnd) return 0f;
            return DropHeight * (1f - UIMotion.OutBack(t / DropEnd, DropOvershoot));
        }

        /// <summary>흔들림 폭 (도): 움직임 줄이기면 0, 실제 최고 등급이 ★4 이상이면 ×1.6</summary>
        public static float ShakeAmplitude(ItemGrade actual, bool reduced) =>
            reduced ? 0f : ShakeDegrees * (IsHigh(actual) ? HighShakeScale : 1f);

        /// <summary>t초의 꾸러미 기울기 (도). 0.30–0.70 사이에 세 번 흔들리고 그 밖은 0</summary>
        public static float ShakeAngle(float t, ItemGrade actual, bool reduced)
        {
            if (t < DropEnd || t >= ShakeEnd) return 0f;
            float u = (t - DropEnd) / (ShakeEnd - DropEnd);
            return Mathf.Sin(u * ShakeCycles * 2f * Mathf.PI) * ShakeAmplitude(actual, reduced);
        }

        /// <summary>열림 진행 0~1 (0.70 → 1.05)</summary>
        public static float OpenProgress(float t) => Mathf.Clamp01((t - ShakeEnd) / (OpenEnd - ShakeEnd));

        /// <summary>초상화가 그림자에서 흰색으로 밝아진 정도 0~1 (1.05 → 1.35)</summary>
        public static float SilhouetteBlend(float t) => Mathf.Clamp01((t - OpenEnd) / (SilhouetteEnd - OpenEnd));

        /// <summary>t초의 초상화 색 (그림자 → 흰색)</summary>
        public static Color PortraitTint(float t) => Color.Lerp(SilhouetteTint, Color.white, SilhouetteBlend(t));

        /// <summary>10회 중 크게 보여 주기의 u초(0 ~ HighReveal)에 해당하는 1회 박자 시각 (0.70초부터, 앞 0.9초에 맞춰 줄임)</summary>
        public static float HighRevealBeatTime(float u) =>
            ShakeEnd + Mathf.Clamp01(u / HighRevealBeats) * (SilhouetteEnd - ShakeEnd);

        // ───────────── 10회 박자 ─────────────

        /// <summary>count장이 다 나타난 때 (초)</summary>
        public static float AppearEnd(int count) => count <= 0 ? 0f : (count - 1) * TenAppearStagger + TenAppearPop;

        /// <summary>index번째 꾸러미가 나타나기 시작하는 때</summary>
        public static float AppearStart(int index) => Mathf.Max(0, index) * TenAppearStagger;

        /// <summary>
        /// index번째 장의 차례 (highBefore = 그 앞의 ★4 이상 장 수): 보통 장은 이때 뒤집고, ★4 이상은 이때 크게 보여 주기(HighReveal)를 시작해
        /// 그 뒤에 뒤집는다. 다음 차례 = 이번 뒤집기 + TenFlipStep (GachaResultPopup.MultiReveal이 공개 시작부터 이 값으로 기다린다)
        /// </summary>
        public static float FlipStart(int count, int index, int highBefore) =>
            AppearEnd(count) + Mathf.Max(0, index) * TenFlipStep + Mathf.Max(0, highBefore) * HighReveal;

        /// <summary>count장 공개 전체 길이 (highCount = ★4 이상 장 수)</summary>
        public static float MultiDuration(int count, int highCount) =>
            count <= 0 ? 0f : AppearEnd(count) + count * TenFlipStep + Mathf.Clamp(highCount, 0, count) * HighReveal + Settle;

        // ───────────── 등급 빛 ─────────────

        static readonly Color LightCream = new(1f, 0.957f, 0.863f, 1f);   // FFF4DC
        static readonly Color LightPink = new(0.914f, 0.651f, 0.776f, 1f); // E9A6C6
        static readonly Color LightGold = new(0.949f, 0.769f, 0.42f, 1f);  // F2C46B
        static readonly Color LightLavender = new(0.769f, 0.698f, 0.949f, 1f); // C4B2F2

        /// <summary>등급 빛: ★1–2 크림 FFF4DC · ★3 분홍 E9A6C6 · ★4 금 F2C46B · ★5 라벤더 C4B2F2</summary>
        public static Color GradeLight(ItemGrade grade) => grade switch
        {
            ItemGrade.Mythic => LightLavender,
            ItemGrade.Legendary => LightGold,
            ItemGrade.Epic => LightPink,
            _ => LightCream,
        };

        /// <summary>
        /// 흔들림 예고에 쓰는 등급 (공정성): 실제가 ★4 이상이면 실제 등급 그대로, 아니면 예고 없음(일반) — 실제보다 높게 보이지 않는다
        /// </summary>
        public static ItemGrade AnticipationGrade(ItemGrade actual) => IsHigh(actual) ? actual : ItemGrade.Common;

        /// <summary>흔들림에 등급 빛을 켜는지 (실제 ★4 이상만)</summary>
        public static bool AnticipationGlow(ItemGrade actual) => IsHigh(actual);

        /// <summary>열릴 때 별가루 수: ★4 12 · ★5 18 (움직임 줄이기면 절반), 그 아래는 0</summary>
        public static int StardustCount(ItemGrade grade, bool reduced)
        {
            int count = grade >= ItemGrade.Mythic ? 18 : grade == ItemGrade.Legendary ? 12 : 0;
            return reduced ? count / 2 : count;
        }

        /// <summary>무지개 별가루인지 (★5 — 여섯 빛깔을 돌린다)</summary>
        public static bool RainbowStardust(ItemGrade grade) => grade >= ItemGrade.Mythic;

        /// <summary>무지개 별가루 빛깔 수</summary>
        public const int RainbowHues = 6;

        /// <summary>
        /// 무지개 별가루 count개 중 hue번째 빛깔의 몫: 고르게 나누고 나머지는 앞 빛깔부터 하나씩 (합 = count — ★5 움직임 줄이기 9 = 2·2·2·1·1·1).
        /// 빛깔 밖이거나 count가 0 이하면 0
        /// </summary>
        public static int RainbowShare(int count, int hue)
        {
            if (count <= 0 || hue < 0 || hue >= RainbowHues) return 0;
            return count / RainbowHues + (hue < count % RainbowHues ? 1 : 0);
        }

        /// <summary>무지개 별가루 i번째 빛깔 (여섯 빛깔을 돈다, 옅은 파스텔)</summary>
        public static Color RainbowHue(int index)
        {
            int i = ((index % 6) + 6) % 6;
            return Color.HSVToRGB(i / 6f, 0.35f, 1f);
        }
    }
}
