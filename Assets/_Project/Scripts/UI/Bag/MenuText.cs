using System;
using System.Globalization;

namespace IdleGame.UI
{
    /// <summary>
    /// 메뉴 화면 공용 문구 도우미: 받침에 맞는 조사(을/를, 은/는, 와/과, 으로/로), 시간·스테이지·초당 수치 표기.
    /// 숫자로 끝나는 말(예: "원정 1-1")은 읽는 소리로 받침을 판단한다.
    /// </summary>
    public static class MenuText
    {
        const int NoFinal = 0;
        const int FinalRieul = 8;
        const int Unknown = -1;

        /// <summary>을/를. 예) 짝짝이를, 양말 카라반을</summary>
        public static string Object(string word) => Attach(word, "을", "를");

        /// <summary>은/는. 예) 짝짝이는, 똑단이는</summary>
        public static string Topic(string word) => Attach(word, "은", "는");

        /// <summary>이/가</summary>
        public static string Subject(string word) => Attach(word, "이", "가");

        /// <summary>과/와</summary>
        public static string And(string word) => Attach(word, "과", "와");

        /// <summary>으로/로 (ㄹ 받침은 '로'). 예) 지킴이로, 원정 1-1로</summary>
        public static string Toward(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            int final = FinalConsonant(word);
            if (final == Unknown) return word + "(으)로";
            return word + (final == NoFinal || final == FinalRieul ? "로" : "으로");
        }

        /// <summary>남은 시간 시계 표기. 예) 09:42, 1:05:09</summary>
        public static string Clock(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            int hours = (int)span.TotalHours;
            return hours > 0 ? $"{hours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes:00}:{span.Seconds:00}";
        }

        /// <summary>분 단위 소요 시간. 예) 10분, 1시간, 1시간 30분</summary>
        public static string Minutes(int minutes)
        {
            if (minutes < 60) return $"{minutes}분";
            int hours = minutes / 60;
            int rest = minutes % 60;
            return rest == 0 ? $"{hours}시간" : $"{hours}시간 {rest}분";
        }

        /// <summary>
        /// 초당 수치 표기 (예: 초당 단추). 1,000 미만도 유효숫자 3자리까지 소수로 보여줘서
        /// 강화 한 번의 차이가 보이게 한다 (1 → 1.1 → 1.21, 12.3, 456). 보유량 표기처럼 내림하고,
        /// 1,000 이상은 NumberFormatter와 같다 (1.23A).
        /// </summary>
        public static string Rate(double perSecond)
        {
            if (!(perSecond >= 0) || perSecond >= 1000) return NumberFormatter.Format(perSecond);
            if (perSecond < 10) return Truncate(perSecond, 100).ToString("0.##", CultureInfo.InvariantCulture);
            if (perSecond < 100) return Truncate(perSecond, 10).ToString("0.#", CultureInfo.InvariantCulture);
            return Math.Floor(perSecond).ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 화면에 쓰는 초당 수치 (§1.10 M30 — 상단 바 '초당 +1.1' · 강화 카드 '초당 단추 1 → 1.1'): 1,000 미만은 Rate(소수 1~2자리),
        /// 그 위는 재화 표기 FormatCurrency (1,800 · 1.23B — 상단 바 칩과 같은 모양)
        /// </summary>
        public static string RateText(double perSecond) => perSecond >= 1000 ? NumberFormatter.FormatCurrency(perSecond) : Rate(perSecond);

        /// <summary>예) 스테이지 13 → "스테이지 2-3"</summary>
        public static string Stage(int stage) => "스테이지 " + BattleSystem.FormatStage(stage);

        /// <summary>
        /// 리치 텍스트 글자 안에서 줄이 갈리지 않는 스테이지 표기 (M28: TMP는 "2-10"의 '-' 뒤에서 줄을 바꿀 수 있다).
        /// 예) 스테이지 13 → "스테이지 &lt;nobr&gt;2-3&lt;/nobr&gt;"
        /// </summary>
        public static string StageKeep(int stage) => "스테이지 " + UIFormat.Keep(BattleSystem.FormatStage(stage));

        public static string SourceTypeName(ItemSourceType type) => type switch
        {
            ItemSourceType.Stage => "전투",
            ItemSourceType.Expedition => "원정",
            ItemSourceType.Dungeon => "던전",
            ItemSourceType.Shop => "상점",
            ItemSourceType.Event => "이벤트",
            _ => type.ToString(),
        };

        /// <summary>소수 자리를 내림한다 (factor 100 = 둘째 자리). 부동소수 오차로 1.1이 1.09가 되지 않게 살짝 보정.</summary>
        static double Truncate(double value, double factor) => Math.Floor(value * factor + 1e-9) / factor;

        static string Attach(string word, string withFinal, string withoutFinal)
        {
            if (string.IsNullOrEmpty(word)) return word;
            int final = FinalConsonant(word);
            if (final == Unknown) return $"{word}{withFinal}({withoutFinal})";
            return word + (final == NoFinal ? withoutFinal : withFinal);
        }

        /// <summary>마지막 글자의 종성 번호 (0 = 받침 없음, 8 = ㄹ, -1 = 판단 불가)</summary>
        static int FinalConsonant(string word)
        {
            char last = word[word.Length - 1];
            if (last >= 0xAC00 && last <= 0xD7A3) return (last - 0xAC00) % 28;
            return last switch
            {
                // 영(ㅇ) 일(ㄹ) 이 삼(ㅁ) 사 오 육(ㄱ) 칠(ㄹ) 팔(ㄹ) 구
                '0' => 21,
                '1' => FinalRieul,
                '2' => NoFinal,
                '3' => 16,
                '4' => NoFinal,
                '5' => NoFinal,
                '6' => 1,
                '7' => FinalRieul,
                '8' => FinalRieul,
                '9' => NoFinal,
                _ => Unknown,
            };
        }
    }
}
