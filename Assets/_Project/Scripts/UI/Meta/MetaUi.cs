using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 메타 UI(가이드 퀘스트·바구니·일지·출석·복귀·개방 카드) 공용 도우미: 켜짐 확인, 시간·숫자 표기, 보상 표기, 이징, 보상 날아가기 연결.
    /// 메타가 꺼져 있으면(Game.IsMetaActive == false, 메타 시스템 구현 전 안전장치) 모든 메타 UI는 숨거나 아무것도 하지 않는다.
    /// </summary>
    public static class MetaUi
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>메타 UI를 그려도 되는지 (게임 준비 + 메타 켜짐)</summary>
        public static bool IsActive => Game.IsReady && Game.IsMetaActive;

        /// <summary>토스트 (UIManager가 없으면 무시 — 종류는 글자로 짐작: 잠김 · 부족 = Error, 그 밖 Info)</summary>
        public static void Toast(string message)
        {
            if (!string.IsNullOrEmpty(message) && UIManager.Instance != null) UIManager.Instance.ShowToast(message);
        }

        /// <summary>종류를 정해 띄우는 토스트 (받기 · 얻음 같은 좋은 소식 = Success, M14)</summary>
        public static void Toast(string message, ToastKind kind)
        {
            if (!string.IsNullOrEmpty(message) && UIManager.Instance != null) UIManager.Instance.ShowToast(message, kind);
        }

        /// <summary>
        /// 게임(튜토리얼·저장값)이 이미 켜 둔 전투 HUD 기능인지: 자동 전투(AutoMode 켜짐) / 2배속(배속 2).
        /// 이런 기능은 "새로 열림" 점·손가락으로 버튼을 누르라고 하면 오히려 꺼 버리게 되므로 봤음으로 친다.
        /// </summary>
        public static bool IsFeatureAlreadyOn(FeatureId feature)
        {
            if (!IsActive || Game.Battle == null) return false;
            return feature switch
            {
                FeatureId.AutoBattle => Game.Battle.AutoMode,
                FeatureId.Speed2x => Game.Battle.Speed > 1,
                _ => false,
            };
        }

        /// <summary>시간 되돌림 중이면 토스트를 띄우고 true (받기 버튼 앞에서 확인)</summary>
        public static bool BlockIfTampered()
        {
            if (!IsActive || !Game.DailyClock.IsTampered) return false;
            Toast(DailyClock.TamperedMessage);
            return true;
        }

        /// <summary>
        /// 받은 보상을 상단 재화 칩으로 날린다 (FX-04 RewardFlyer). 메타 UI의 모든 보상 연출은 여기 한 곳을 거친다.
        /// 날아가기 층이 씬에 없으면 아무것도 하지 않는다 (지급은 이미 끝난 상태).
        /// </summary>
        public static void FlyRewards(IReadOnlyList<MetaReward> rewards, RectTransform from)
        {
            if (rewards == null || rewards.Count == 0 || !RewardFlyer.IsAvailable) return;
            RewardFlyer.Fly(rewards, from);
        }

        // ───────────── 보상 미리보기 (안 D) ─────────────

        static readonly List<MetaReward> s_preview = new();

        /// <summary>
        /// 데이터 보상 목록을 화면에 보일 값으로 푼다 (into를 비우고 채움, MetaRewardService.PreviewAll). 고정 단추는 실제 지급과 같은 나누기로
        /// 단추 ⌊n/2⌋ · 실타래 ⌊n/2⌋ 두 칩, N분치는 "단추 · 실타래 N분치" 글 그대로, 같은 재화는 합친다 (팀 결정: 미리보기 칩 = 실제 지급).
        /// 메타가 꺼져 있으면 그대로 복사
        /// </summary>
        public static void Preview(IReadOnlyList<MetaReward> rewards, List<MetaReward> into)
        {
            if (into == null) return;
            if (IsActive && Game.MetaRewards != null)
            {
                Game.MetaRewards.PreviewAll(rewards, into);
                return;
            }
            into.Clear();
            if (rewards == null) return;
            for (int i = 0; i < rewards.Count; i++)
                if (rewards[i] != null) into.Add(rewards[i].Clone());
        }

        /// <summary>미리보기의 첫 줄과 줄 수 (한 칸만 보이는 자리용: 첫 칩 + 더 있다는 표시). 다음 호출 전까지만 쓴다 (공용 목록)</summary>
        public static MetaReward FirstPreview(IReadOnlyList<MetaReward> rewards, out int count)
        {
            Preview(rewards, s_preview);
            count = s_preview.Count;
            return count > 0 ? s_preview[0] : null;
        }

        // ───────────── 시간 표기 ─────────────

        /// <summary>"6시간 12분" / "12분" / "40초" (0 이하 → "0분")</summary>
        public static string Duration(TimeSpan span)
        {
            if (span <= TimeSpan.Zero) return "0분";
            int hours = (int)span.TotalHours;
            int minutes = span.Minutes;
            if (hours > 0) return minutes > 0 ? $"{hours}시간 {minutes}분" : $"{hours}시간";
            if (minutes > 0) return $"{minutes}분";
            return $"{Math.Max(1, span.Seconds)}초";
        }

        public static string Duration(double seconds) => Duration(TimeSpan.FromSeconds(Math.Max(0, seconds)));

        /// <summary>"7:12:03" (시간이 0이면 "12:03")</summary>
        public static string Clock(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            int hours = (int)span.TotalHours;
            return hours > 0 ? $"{hours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes:00}:{span.Seconds:00}";
        }

        /// <summary>바구니 위젯 "6:12" (시:분)</summary>
        public static string HourMinute(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            return $"{(int)span.TotalHours}:{span.Minutes:00}";
        }

        /// <summary>남은 시간 한 줄 "초기화까지 7:12:03" 또는 하루가 넘으면 "초기화까지 2일 5시간"</summary>
        public static string ResetIn(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalHours >= 24) return $"초기화까지 {(int)span.TotalDays}일 {span.Hours}시간";
            return "초기화까지 " + Clock(span);
        }

        /// <summary>시간(소수) → "8시간" / "1.5시간"</summary>
        public static string Hours(float hours) =>
            Mathf.Approximately(hours, Mathf.Round(hours)) ? $"{Mathf.RoundToInt(hours)}시간" : $"{hours.ToString("0.#", Invariant)}시간";

        // ───────────── 조사 ─────────────

        /// <summary>이/가 (숫자로 끝나면 읽는 소리로: "Lv.200이", "Lv.2가" — 한글도 숫자도 아니면 '이(가)')</summary>
        public static string WithSubject(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            char last = word[word.Length - 1];
            bool final;
            if (last >= 0xAC00 && last <= 0xD7A3) final = (last - 0xAC00) % 28 != 0;
            else if (last >= '0' && last <= '9') final = "013678".IndexOf(last) >= 0; // 영 일 삼 육 칠 팔
            else return word + "이(가)";
            return word + (final ? "이" : "가");
        }

        /// <summary>을/를 (숫자로 끝나면 읽는 소리로: "2-1을", "1-2를")</summary>
        public static string WithObject(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            char last = word[word.Length - 1];
            bool final;
            if (last >= 0xAC00 && last <= 0xD7A3) final = (last - 0xAC00) % 28 != 0;
            else if (last >= '0' && last <= '9') final = "013678".IndexOf(last) >= 0; // 영 일 삼 육 칠 팔
            else return word + "을(를)";
            return word + (final ? "을" : "를");
        }

        // ───────────── 숫자 / 보상 표기 ─────────────

        /// <summary>
        /// 초당 수치 표기 (초당 단추). 1,000 미만도 유효숫자 3자리까지 소수로 보여 강화 한 번의 차이가 보이게 한다
        /// (1 → 1.1 → 1.21, 12.3, 456). 1,000 이상은 NumberFormatter (1.23A). 상단 바 원래 표기와 같다.
        /// </summary>
        public static string Rate(double perSecond)
        {
            if (!(perSecond >= 0) || perSecond >= 1000) return NumberFormatter.Format(perSecond);
            if (perSecond < 10) return (Math.Floor(perSecond * 100 + 1e-9) / 100).ToString("0.##", Invariant);
            if (perSecond < 100) return (Math.Floor(perSecond * 10 + 1e-9) / 10).ToString("0.#", Invariant);
            return Math.Floor(perSecond).ToString("0", Invariant);
        }

        /// <summary>정수 콤마 (100만 이상은 단위 표기)</summary>
        public static string Int(double value) => NumberFormatter.FormatInt(value);

        /// <summary>
        /// 보상 칸 아래 짧은 양 표기 (M30: 재화는 칩용 줄인 표기 "12.3A" · "1,200"(FormatCompact), 재료 · 수선권 "×5"(작은 ×),
        /// 단추 N분치 "30분치", 친구/의상은 이름). 리치 텍스트 TMP 칸 전용 (×는 &lt;size=80%&gt;)
        /// </summary>
        public static string ShortAmount(MetaReward reward)
        {
            if (reward == null) return string.Empty;
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    return RewardService.FormatAmountCompact(reward.currency, reward.amount);
                case MetaRewardType.GoldMinutes:
                    return reward.amount >= 60 && Math.Abs(reward.amount % 60) < 1e-6
                        ? $"{(int)(reward.amount / 60)}시간치"
                        : $"{(int)Math.Round(reward.amount)}분치";
                case MetaRewardType.Lineage:
                case MetaRewardType.Costume:
                    return NameOf(reward);
                default:
                    return UIFormat.TimesSmall + Int(Math.Round(reward.amount));
            }
        }

        /// <summary>보상 이름 (MetaRewardService.NameOf가 있으면 그것, 없으면 기본)</summary>
        public static string NameOf(MetaReward reward)
        {
            if (reward == null) return string.Empty;
            if (IsActive)
            {
                try
                {
                    return Game.MetaRewards.NameOf(reward);
                }
                catch (NotImplementedException)
                {
                    // 메타 보상 서비스가 아직 구현 전이면 아래 기본 이름
                }
            }
            return reward.type switch
            {
                MetaRewardType.Currency => RewardService.CurrencyName(reward.currency),
                MetaRewardType.Item => Game.Items != null ? Game.Items.Get(reward.itemId)?.name ?? reward.itemId : reward.itemId,
                MetaRewardType.GoldMinutes => MetaRewardService.IncomeLabel,
                MetaRewardType.QuickIdleTicket => "빠른 수선권",
                MetaRewardType.DoubleTicket => "두 배 수선권",
                MetaRewardType.SelectShards => "선택 기억편",
                MetaRewardType.Lineage => Game.Characters?.Db.GetLineage(reward.itemId)?.name ?? reward.itemId,
                MetaRewardType.Costume => Game.Characters?.Db.GetCostume(reward.itemId)?.name ?? reward.itemId,
                _ => reward.itemId,
            };
        }

        /// <summary>보상 한 줄 설명 (예: "별조각 300", "수선 천 x5", "단추 30분치")</summary>
        public static string Describe(MetaReward reward)
        {
            if (reward == null) return string.Empty;
            if (IsActive)
            {
                try
                {
                    return Game.MetaRewards.Describe(reward);
                }
                catch (NotImplementedException)
                {
                    // 기본 표기로
                }
            }
            return reward.type switch
            {
                MetaRewardType.Lineage or MetaRewardType.Costume => NameOf(reward),
                MetaRewardType.Currency => $"{NameOf(reward)} {ShortAmount(reward)}",
                MetaRewardType.GoldMinutes => $"{MetaRewardService.IncomeLabel} {ShortAmount(reward)}",
                _ => $"{NameOf(reward)} {ShortAmount(reward)}",
            };
        }

        /// <summary>목록 설명 (", "로 잇기)</summary>
        public static string Describe(IReadOnlyList<MetaReward> rewards, string separator = ", ")
        {
            if (rewards == null || rewards.Count == 0) return string.Empty;
            var parts = new string[rewards.Count];
            for (int i = 0; i < rewards.Count; i++) parts[i] = Describe(rewards[i]);
            return string.Join(separator, parts);
        }

        /// <summary>
        /// 데이터 보상 목록(퀘스트 · 출석 · 일지 상자 · 개방 카드 등, 아직 풀지 않은 것)의 설명. 단추 · N분치는 실제 지급과 같게 단추 · 실타래 두 줄로
        /// 푼 뒤 쓴다 (Preview). 이미 푼 목록(지급 결과 · 바구니 미리보기)은 Describe를 쓴다 (다시 풀면 단추 줄이 또 나뉜다)
        /// </summary>
        public static string DescribeData(IReadOnlyList<MetaReward> rewards, string separator = ", ")
        {
            if (rewards == null || rewards.Count == 0) return string.Empty;
            var shown = new List<MetaReward>(rewards.Count + 1);
            Preview(rewards, shown);
            return Describe(shown, separator);
        }

        /// <summary>
        /// 목록 설명, 보상 하나는 한 덩어리로 (M28): 보상마다 안의 빈칸을 줄바꿈 없는 빈칸(U+00A0)으로 바꿔 '단추 3.60A' · '수선 천 ×5'가
        /// 두 줄로 갈라지지 않고, 보상 사이(", ")에서만 줄이 바뀐다. 바뀔 때만 부른다 (할당 있음)
        /// </summary>
        public static string DescribeKeep(IReadOnlyList<MetaReward> rewards, string separator = ", ")
        {
            if (rewards == null || rewards.Count == 0) return string.Empty;
            var parts = new string[rewards.Count];
            for (int i = 0; i < rewards.Count; i++) parts[i] = Keep(Describe(rewards[i]));
            return string.Join(separator, parts);
        }

        /// <summary>글 안의 빈칸을 모두 줄바꿈 없는 빈칸으로 (한 덩어리로 묶을 때)</summary>
        public static string Keep(string text) => string.IsNullOrEmpty(text) ? text ?? string.Empty : text.Replace(' ', UIFormat.NbspChar);

        /// <summary>보상 종류별 바탕색 (아이콘 아트가 없을 때 자리표시 색)</summary>
        public static Color RewardColor(MetaReward reward)
        {
            if (reward == null) return UITheme.SubText;
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    return UITheme.CurrencyColor(reward.currency);
                case MetaRewardType.GoldMinutes:
                    return UITheme.Gold;
                case MetaRewardType.Item:
                    var item = Game.Items != null ? Game.Items.Get(reward.itemId) : null;
                    return item != null ? UITheme.GradeColor(item.grade) : UITheme.SubText;
                case MetaRewardType.QuickIdleTicket:
                    return UITheme.SoftBlue;
                case MetaRewardType.DoubleTicket:
                    return UITheme.Lavender;
                case MetaRewardType.SelectShards:
                    return UITheme.DustyPink;
                case MetaRewardType.Lineage:
                    var lineage = Game.Characters?.Db.GetLineage(reward.itemId);
                    return lineage != null ? lineage.mainColor : UITheme.Lavender;
                default:
                    return UITheme.Lavender;
            }
        }

        /// <summary>보상 등급 테두리 색 (재료 등급, 친구 등급, 나머지는 금색)</summary>
        public static Color RewardFrameColor(MetaReward reward)
        {
            if (reward == null) return UITheme.Divider;
            switch (reward.type)
            {
                case MetaRewardType.Item:
                    var item = Game.Items != null ? Game.Items.Get(reward.itemId) : null;
                    return item != null ? UITheme.GradeColor(item.grade) : UITheme.Divider;
                case MetaRewardType.Lineage:
                    var lineage = Game.Characters?.Db.GetLineage(reward.itemId);
                    return lineage != null ? UITheme.GradeColor(lineage.grade) : UITheme.GoldAccent;
                case MetaRewardType.Costume:
                    return UITheme.GradeColor(ItemGrade.Epic);
                case MetaRewardType.Currency when reward.currency == CurrencyType.Gem:
                    return UITheme.Gem;
                default:
                    return UITheme.GoldAccent;
            }
        }

        // ───────────── 이징 ─────────────

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float k = 1f - t;
            return 1f - k * k * k;
        }

        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>살짝 넘쳤다가 돌아오는 이징 (팝 인)</summary>
        public static float EaseOutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        /// <summary>0 → 1 → 0 사인 펄스 (튀기기)</summary>
        public static float Punch(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        /// <summary>색 알파만 바꾼 새 색</summary>
        public static Color Alpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        // ───────────── 막대 ─────────────

        /// <summary>
        /// 가로 막대 채움 (빌더의 Bar: 9-slice 채움 그림이 FillArea 안에서 왼쪽에 붙어 있다).
        /// 오른쪽 앵커를 비율로 옮겨 폭을 바꾸고, 0이면 끈다. 둥근 그림에 fillAmount를 쓰면 끝이 렌즈 모양으로 깎이므로 쓰지 않는다.
        /// </summary>
        public static void SetBar(Image fill, float ratio)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            var rect = fill.rectTransform;
            var max = rect.anchorMax;
            if (!Mathf.Approximately(max.x, ratio)) rect.anchorMax = new Vector2(ratio, max.y);
            bool visible = ratio > 0.001f;
            if (fill.gameObject.activeSelf != visible) fill.gameObject.SetActive(visible);
        }

        /// <summary>SetBar로 정한 지금 비율 (꺼져 있으면 0)</summary>
        public static float BarRatio(Image fill) =>
            fill == null || !fill.gameObject.activeSelf ? 0f : fill.rectTransform.anchorMax.x;
    }
}
