using System;
using System.Globalization;

namespace IdleGame
{
    /// <summary>
    /// 메타 기능의 기본 문구 (조건 설명, 기능 이름, 카운터 이름, 조사). 콘텐츠(MetaDatabase)에 문구가 없을 때 쓴다.
    /// 문구는 존댓말이 아닌 짧은 명사형("잔향이 30번 부르기")이고, 안내 문장은 부르는 쪽이 만든다.
    /// </summary>
    public static class MetaText
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>기능 이름 (콘텐츠 FeatureUnlockDef.name이 없을 때)</summary>
        public static string FeatureName(FeatureId feature) => feature switch
        {
            FeatureId.Battle => "전투",
            FeatureId.ShopUpgrade => "수선점 강화",
            FeatureId.Characters => "친구들",
            FeatureId.Formation => "편성",
            FeatureId.CharacterLevelUp => "친구 레벨업",
            FeatureId.AutoBattle => "자동 전투",
            FeatureId.IdleChest => "수선 바구니",
            FeatureId.Gacha => "잃어버린 친구 찾기",
            FeatureId.Expedition => "원정",
            FeatureId.QuickIdle => "빠른 수선",
            FeatureId.Attendance => "출석부",
            FeatureId.Speed2x => "2배속",
            FeatureId.Missions => "수선 일지",
            FeatureId.Shop => "상점",
            FeatureId.Dungeon => "잔향이 던전",
            FeatureId.Evolution => "진화 제작",
            FeatureId.Codex => "도감",
            FeatureId.Synergy => "시너지",
            FeatureId.Achievements => "수선점 기록",
            FeatureId.Costumes => "의상",
            FeatureId.EchoSlot6 => "잔향이 덱 6번째 칸",
            FeatureId.Events => "이벤트",
            FeatureId.LampPolicy => "자동 불 밝히기 방식",
            FeatureId.AutoChallenge => "자동 보스 도전",
            FeatureId.ExpeditionBatch => "원정 모두 받기",
            FeatureId.DungeonBatchSweep => "던전 입장권 한 번에 쓰기",
            FeatureId.BattleStats => "전투 기록",
            _ => feature.ToString(),
        };

        /// <summary>카운터 목표 문구 (예: "잔향이 30번 부르기", "적 300 처치")</summary>
        public static string CounterText(CounterKey key, long n)
        {
            string c = Comma(n);
            return key switch
            {
                CounterKey.Login => n <= 1 ? "수선점 들르기" : $"수선점 {c}일 들르기",
                CounterKey.StageClear => $"스테이지 {c}번 클리어",
                CounterKey.StageFail => $"스테이지 {c}번 실패",
                CounterKey.BossKill => $"보스 {c}마리 처치",
                CounterKey.EnemyKill => $"적 {c} 처치",
                CounterKey.EchoSummon => $"잔향이 {c}번 부르기",
                CounterKey.LampLit => $"불 밝히기 {c}번",
                CounterKey.ShopLevelUp => $"수선점 강화 {c}회",
                CounterKey.LineageLevelUp => $"친구 레벨업 {c}회",
                CounterKey.GachaPull => $"잃어버린 친구 찾기 {c}회",
                CounterKey.ExpeditionStart => $"원정 {c}번 보내기",
                CounterKey.ExpeditionClaim => $"원정 보상 {c}번 받기",
                CounterKey.DungeonChallenge => $"잔향이 던전 {c}번 도전",
                CounterKey.DungeonClear => $"잔향이 던전 새 층 {c}번 돌파",
                CounterKey.DungeonSweep => $"잔향이 던전 {c}번 소탕",
                CounterKey.EvolutionCraft => $"진화 {c}회 제작",
                CounterKey.ResonanceUp => $"계보 공명 {c}회",
                CounterKey.CostumeUnlock => $"의상 {c}벌 해금",
                CounterKey.CostumeEquip => $"의상 {c}번 갈아입기",
                CounterKey.ShopBuy => $"상점에서 {c}번 사기",
                CounterKey.ChestClaim => $"수선 바구니 {c}번 받기",
                CounterKey.QuickIdle => $"빠른 수선 {c}번",
                CounterKey.MailClaim => $"우편 {c}통 받기",
                CounterKey.SynergyTierMax => $"시너지 최대 단계 {c}번 켜기",
                CounterKey.GuideQuestClaim => $"가이드 퀘스트 {c}번 받기",
                CounterKey.MissionClaim => $"미션 {c}개 받기",
                CounterKey.JournalComplete => $"수선 일지 100점 {c}일",
                CounterKey.AttendanceCheckIn => $"출석 {c}일",
                CounterKey.AnyUpgrade => $"친구·수선점 강화 {c}번",
                CounterKey.AnyGrowthMilestone => $"진화·공명·의상 중 {c}번",
                CounterKey.DungeonRun => $"잔향이 던전 {c}번",
                // ── 4차 (SPEC4 §2.2) ──
                CounterKey.JournalPoints => $"수선 일지 점수 {c}점 모으기",
                CounterKey.AchievementClaim => $"수선점 기록 {c}번 받기",
                CounterKey.CodexClaim => $"도감 보상 {c}번 받기",
                CounterKey.AffinityPet => $"친구 {c}번 쓰다듬기",
                CounterKey.EventMissionClaim => $"이벤트 미션 {c}개 받기",
                CounterKey.SeasonTierClaim => $"수첩 칸 {c}개 받기",
                CounterKey.LostChestOpen => $"잃어버린 상자 {c}개 열기",
                CounterKey.PlayMinutes => $"수선점을 {c}분 돌보기",
                _ => $"{key} {c}",
            };
        }

        /// <summary>플래그 조건 문구 (MetaFlags). 모르는 플래그는 이름 그대로</summary>
        public static string FlagText(string flag) => flag switch
        {
            MetaFlags.IntroSeen => "동화 인트로 보기",
            MetaFlags.FormationDragged => "친구를 끌어 다른 방위에 세우기",
            MetaFlags.SynergyDetailViewed => "시너지 자세히 보기",
            MetaFlags.EvolutionTreeViewed => "진화 기억도 열어 보기",
            MetaFlags.CodexOpened => "도감 열어 보기",
            MetaFlags.DirectionalSummon => "잔향이를 원하는 방향으로 보내기",
            MetaFlags.FirstMeetingSummoned => "첫 만남 소환 받기",
            MetaFlags.ShortFirstExpeditionUsed => "첫 원정 보내기",
            MetaFlags.IdleChestPrefilled => "수선 바구니 열어 보기",
            MetaFlags.DeckDragHint => "잔향이를 끌어서 방향 정해 부르기",
            _ => flag ?? string.Empty,
        };

        /// <summary>정수 콤마 표기 (1,234)</summary>
        public static string Comma(long value) => value.ToString("N0", Invariant);

        // ── 잠금 안내 (M39) — 둘 다 "도달" 인덱스(highestStage가 비교하는 값)를 받는다 ──

        /// <summary>
        /// 짧은 잠금 문구 "스테이지 2-9 클리어 시" (카드·칸 자물쇠용, 뒤에 "\n열려요"를 붙여 쓴다).
        /// reached = 열리는 최고 스테이지(도달 기준)라 표기는 그 앞 스테이지(클리어 기준)다. 1 이하(처음부터 열림)면 빈 문자열.
        /// HUD 칩·탭 알약 같은 아주 작은 자물쇠는 이 문구 대신 "1-2"만 쓴다.
        /// </summary>
        public static string UnlockShort(int reached) =>
            reached <= 1 ? string.Empty : $"스테이지 {BattleSystem.FormatStage(reached - 1)} 클리어 시";

        /// <summary>
        /// 잠금 안내 문장 "스테이지 2-9를 클리어하면 열려요" (토스트용, FeatureGateSystem.GetRequirementText와 같은 꼴).
        /// reached는 UnlockShort와 같은 도달 기준. 1 이하면 빈 문자열.
        /// </summary>
        public static string UnlockSentence(int reached) =>
            reached <= 1 ? string.Empty : $"스테이지 {WithObject(BattleSystem.FormatStage(reached - 1))} 클리어하면 열려요";

        // ── 부족 안내 (M08) ──

        /// <summary>단추 부족 안내에 예상 시간을 붙이는 상한 (초). 이보다 오래 걸리면 시간을 적지 않는다</summary>
        public const double GoldShortfallEtaMaxSeconds = 600;

        /// <summary>
        /// 단추 부족 토스트 "단추 1,234개가 더 필요해요 · 약 2분" (팝업 없음). 모자란 양은 FormatCurrency라 100만 이상은 단위 표기이고,
        /// 그때는 "개"를 붙이지 않고 단위 글자 소리에 맞춘 조사를 쓴다 ("단추 1.50B가 더 필요해요" — "1.50B개"처럼 읽히지 않게).
        /// 예상 시간은 초당 단추로 10분 안에 모일 때만, 수선 바구니에 모자란 만큼 이상 쌓여 있으면 " · 바구니를 받아 보세요"를 덧붙인다.
        /// 초당 단추와 바구니 단추는 Game(Growth / IdleChest)에서 읽는다 — 게임이 준비되지 않았으면 둘 다 0.
        /// </summary>
        public static string GoldShortfall(double need, double have)
        {
            double perSecond = Game.IsReady && Game.Growth != null ? Game.Growth.GoldPerSecond : 0;
            double basket = Game.IsMetaActive && Game.IdleChest != null ? Game.IdleChest.PendingGold : 0;
            return GoldShortfall(need, have, perSecond, basket);
        }

        /// <summary>GoldShortfall의 계산 본체 (테스트용으로 초당 단추·바구니 단추를 직접 받는다)</summary>
        public static string GoldShortfall(double need, double have, double goldPerSecond, double basketGold) =>
            IncomeShortfall(CurrencyType.Gold, need, have, goldPerSecond, basketGold);

        /// <summary>
        /// 실타래 부족 토스트 "실타래 1,234개가 더 필요해요 · 약 2분" (단추와 같은 꼴, 결정 6 #6). 수입 속도 = 실타래 수입 속도
        /// (GrowthSystem.IncomeRateOf, 초당 수입 × 실타래 몫), 바구니 = 지금 받으면 들어올 실타래
        /// </summary>
        public static string LineageXpShortfall(double need, double have)
        {
            double perSecond = Game.IsReady && Game.Growth != null ? Game.Growth.IncomeRateOf(CurrencyType.LineageXp) : 0;
            double basket = Game.IsMetaActive && Game.IdleChest != null ? Game.IdleChest.PendingLineageXp : 0;
            return IncomeShortfall(CurrencyType.LineageXp, need, have, perSecond, basket);
        }

        /// <summary>단추 · 실타래 부족 토스트 본체 (재화 이름 · 수입 속도 · 바구니 양을 받는다)</summary>
        public static string IncomeShortfall(CurrencyType type, double need, double have, double goldPerSecond, double basketGold)
        {
            double gap = Gap(need, have);
            string gold = RewardService.CurrencyName(type);
            string amount = NumberFormatter.FormatCurrency(gap);
            string text = gap < NumberFormatter.CurrencyCommaLimit
                ? $"{gold} {amount}개가 더 필요해요"
                : $"{gold} {WithSubject(amount)} 더 필요해요";
            if (gap <= 0) return text;
            if (goldPerSecond > 0 && !double.IsInfinity(goldPerSecond))
            {
                double seconds = gap / goldPerSecond;
                if (seconds < GoldShortfallEtaMaxSeconds) text += " · 약 " + EtaText(seconds);
            }
            if (basketGold >= gap) text += " · 바구니를 받아 보세요";
            return text;
        }

        /// <summary>모자란 양 (올림 정수, 0 이상). 보유량의 소수점 때문에 실제보다 적게 말하지 않는다</summary>
        static double Gap(double need, double have)
        {
            if (double.IsNaN(need) || double.IsNaN(have)) return 0;
            return Math.Max(0, Math.Ceiling(need - have - 1e-9));
        }

        /// <summary>"30초" / "2분" (1분 미만은 초, 그 밖은 반올림한 분)</summary>
        static string EtaText(double seconds)
        {
            if (seconds < 60) return $"{Math.Max(1, (int)Math.Ceiling(seconds))}초";
            return $"{Math.Max(1, (int)Math.Round(seconds / 60.0))}분";
        }

        /// <summary>받침 유무에 따라 목적격 조사(을/를)를 붙인다. 숫자·단위 글자로 끝나면 읽는 소리로 판단한다 (1-4를, 1-3을, 1.50M을)</summary>
        public static string WithObject(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            return word + (HasFinalConsonant(word[word.Length - 1]) ? "을" : "를");
        }

        /// <summary>
        /// 받침 유무에 따라 주격 조사(이/가)를 붙인다. 숫자·단위 글자로 끝나면 읽는 소리로 판단한다 (1.50B가, 1.50M이, 1-3이).
        /// KoreanText.WithSubject는 한글이 아니면 "이(가)"를 쓰므로, 숫자·큰 수 단위가 끝에 오는 문구는 이쪽을 쓴다.
        /// </summary>
        public static string WithSubject(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            return word + (HasFinalConsonant(word[word.Length - 1]) ? "이" : "가");
        }

        /// <summary>받침 유무에 따라 "으로/로"를 붙인다 (ㄹ 받침은 "로" — 1 일, 7 칠, 8 팔, L 엘, R 알 포함)</summary>
        public static string WithDirection(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            char last = word[word.Length - 1];
            bool rieul = last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 == 8
                         || last == '1' || last == '7' || last == '8'
                         || last == 'L' || last == 'R' || last == 'l' || last == 'r';
            return word + (HasFinalConsonant(last) && !rieul ? "으로" : "로");
        }

        /// <summary>
        /// 마지막 글자에 받침이 있는지 (한글 음절, 숫자는 읽는 소리 기준: 0 영, 1 일, 3 삼, 6 육, 7 칠, 8 팔.
        /// 로마자는 알파벳 이름 기준 — 큰 수 단위(NumberFormatter A~Z)에서 받침이 있는 것은 L 엘, M 엠, N 엔, R 알뿐)
        /// </summary>
        public static bool HasFinalConsonant(char last)
        {
            if (last >= 0xAC00 && last <= 0xD7A3) return (last - 0xAC00) % 28 != 0;
            switch (last)
            {
                case '0':
                case '1':
                case '3':
                case '6':
                case '7':
                case '8':
                case 'L':
                case 'M':
                case 'N':
                case 'R':
                case 'l':
                case 'm':
                case 'n':
                case 'r':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>"6시간 12분" / "12분" / "30초" (복귀 화면, 바구니)</summary>
        public static string HoursMinutes(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            int hours = (int)span.TotalHours;
            int minutes = span.Minutes;
            if (hours > 0) return minutes > 0 ? $"{hours}시간 {minutes}분" : $"{hours}시간";
            if (minutes > 0) return $"{minutes}분";
            return $"{Math.Max(0, span.Seconds)}초";
        }
    }
}
