using System.Collections.Generic;

namespace IdleGame.UI
{
    /// <summary>재화를 얻는 곳 종류 (CurrencySources 줄의 이동 대상)</summary>
    public enum CurrencySourceKind
    {
        /// <summary>우편함 (받지 않은 그 재화 우편이 있을 때만)</summary>
        Mail = 0,
        /// <summary>수선 바구니</summary>
        IdleChest = 1,
        /// <summary>스테이지 진행 (전투 화면은 늘 보인다 — 이동 없음)</summary>
        Stage = 2,
        Expedition = 3,
        /// <summary>수선 일지 (일일·주간 미션 상자)</summary>
        Journal = 4,
        Attendance = 5,
        Dungeon = 6,
        Shop = 7,
        Events = 8,
        EventShop = 9,
    }

    /// <summary>재화 얻는 곳 한 줄 (부족 팝업 · 재화 안내)</summary>
    public readonly struct CurrencySource
    {
        public readonly CurrencySourceKind Kind;
        /// <summary>줄 제목 ("수선 바구니", 우편은 "우편함에 별조각 1,000개가 있어요 · 받기")</summary>
        public readonly string Label;
        /// <summary>줄 아래 설명 (SubText, 비어 있을 수 있음)</summary>
        public readonly string Detail;
        /// <summary>잠금 확인에 쓰는 기능 (Gated가 false면 늘 열림)</summary>
        public readonly FeatureId Feature;
        public readonly bool Gated;
        /// <summary>맨 위에서 강조할 줄 (받지 않은 별조각 우편)</summary>
        public readonly bool Highlight;

        public CurrencySource(CurrencySourceKind kind, string label, string detail, FeatureId feature, bool gated, bool highlight = false)
        {
            Kind = kind;
            Label = label;
            Detail = detail;
            Feature = feature;
            Gated = gated;
            Highlight = highlight;
        }

        /// <summary>지금 갈 수 있는지 (기능이 열렸는지, 게임이 준비 안 됐으면 열림으로 본다)</summary>
        public bool IsUnlocked => !Gated || !Game.IsReady || Game.Features == null || Game.Features.IsUnlocked(Feature);
    }

    /// <summary>
    /// 재화를 얻는 곳 표 (M08 · M59). 부족 팝업(ShortfallPopup, '별조각 얻는 곳' 모드 포함)의 줄과 재화 칩 안내 말풍선 글자가 여기서 나온다.
    ///   · 단추: 수선 바구니 → 스테이지 진행 → 원정 → 잔향이 던전 (던전 보상은 단추)
    ///   · 별조각: 우편(받지 않은 별조각 우편이 있을 때만, 맨 위 강조 '우편함에 별조각 N개가 있어요 · 받기') → 수선 일지 → 출석부 → 수선 바구니
    ///     (지금 콘텐츠에서 별조각을 주는 곳만 — 잔향이 던전은 단추를, 상점은 별조각을 받고 재료를 판다)
    ///   · 별빛 사탕: 이벤트 → 수선 바구니(이벤트 기간) → 이벤트 상점(쓰는 곳)
    ///   · 실타래 (결정 2 안 D): 단추가 나오는 모든 곳에서 함께 나온다. 줄은 단추와 같은 넷 (수선 바구니 → 스테이지 진행 → 원정 → 잔향이 던전)
    /// 이동은 Go가 UINav로 보낸다 (부르는 쪽 팝업은 먼저 닫는다). 표는 누를 때만 만든다 (매 프레임 부르지 않는다).
    /// </summary>
    public static class CurrencySources
    {
        /// <summary>그 재화를 얻는 곳 (새 목록)</summary>
        public static List<CurrencySource> For(CurrencyType type)
        {
            var list = new List<CurrencySource>(5);
            For(type, list);
            return list;
        }

        /// <summary>그 재화를 얻는 곳을 results에 채운다 (비우고 채움)</summary>
        public static void For(CurrencyType type, List<CurrencySource> results)
        {
            results.Clear();
            switch (type)
            {
                case CurrencyType.Gold:
                    results.Add(new CurrencySource(CurrencySourceKind.IdleChest, "수선 바구니", "자리를 비운 동안 단추가 모여요", FeatureId.IdleChest, true));
                    results.Add(new CurrencySource(CurrencySourceKind.Stage, "스테이지 진행", "전투에서 이길 때마다 단추를 받아요", FeatureId.Battle, false));
                    results.Add(new CurrencySource(CurrencySourceKind.Expedition, "원정", "친구를 보내면 단추와 재료를 가져와요", FeatureId.Expedition, true));
                    results.Add(new CurrencySource(CurrencySourceKind.Dungeon, "잔향이 던전", "층을 깨면 단추를 많이 받아요", FeatureId.Dungeon, true));
                    break;
                case CurrencyType.Gem:
                    double mail = UnclaimedMailAmount(CurrencyType.Gem);
                    if (mail > 0d)
                    {
                        string label = $"우편함에 {RewardService.CurrencyName(CurrencyType.Gem)} {NumberFormatter.FormatCurrency(mail)}개가 있어요 · 받기";
                        results.Add(new CurrencySource(CurrencySourceKind.Mail, label, "받지 않은 선물이 기다려요", FeatureId.Battle, false, true));
                    }
                    results.Add(new CurrencySource(CurrencySourceKind.Journal, "수선 일지", "미션 상자를 열면 받아요", FeatureId.Missions, true));
                    results.Add(new CurrencySource(CurrencySourceKind.Attendance, "출석부", "매일 도장을 찍으면 받아요", FeatureId.Attendance, true));
                    results.Add(new CurrencySource(CurrencySourceKind.IdleChest, "수선 바구니", "자리를 비운 동안 조금씩 모여요", FeatureId.IdleChest, true));
                    break;
                case CurrencyType.LineageXp:
                    results.Add(new CurrencySource(CurrencySourceKind.IdleChest, "수선 바구니", "자리를 비운 동안 단추와 함께 모여요", FeatureId.IdleChest, true));
                    results.Add(new CurrencySource(CurrencySourceKind.Stage, "스테이지 진행", "전투에서 이길 때마다 단추와 함께 받아요", FeatureId.Battle, false));
                    results.Add(new CurrencySource(CurrencySourceKind.Expedition, "원정", "친구를 보내면 단추와 함께 가져와요", FeatureId.Expedition, true));
                    results.Add(new CurrencySource(CurrencySourceKind.Dungeon, "잔향이 던전", "층을 깨면 단추와 함께 받아요", FeatureId.Dungeon, true));
                    break;
                case CurrencyType.EventToken:
                    results.Add(new CurrencySource(CurrencySourceKind.Events, "이벤트", "이벤트 기간에 모을 수 있어요", FeatureId.Events, true));
                    results.Add(new CurrencySource(CurrencySourceKind.IdleChest, "수선 바구니", "이벤트 기간에 바구니에 모여요", FeatureId.IdleChest, true));
                    results.Add(new CurrencySource(CurrencySourceKind.EventShop, "이벤트 상점", "모은 사탕을 선물로 바꿔요", FeatureId.Events, true));
                    break;
            }
        }

        /// <summary>받지 않은(만료되지 않은) 우편에 든 그 재화 합계. 게임이 준비 안 됐으면 0</summary>
        public static double UnclaimedMailAmount(CurrencyType type)
        {
            if (!Game.IsReady || Game.Mail == null) return 0d;
            double total = 0d;
            var mails = Game.Mail.Mails;
            for (int i = 0; i < mails.Count; i++)
            {
                var mail = mails[i];
                if (mail == null || !mail.HasRewards) continue;
                if (mail.expireAt > 0 && Game.Mail.GetRemainTime(mail) <= System.TimeSpan.Zero) continue;
                for (int r = 0; r < mail.rewards.Count; r++)
                {
                    var reward = mail.rewards[r];
                    if (reward != null && reward.type == RewardType.Currency && reward.currency == type) total += reward.amount;
                }
            }
            return total;
        }

        // ───────────── 안내 말풍선 글자 (InfoTooltipView) ─────────────

        /// <summary>재화 이름 (단추 / 별조각 / 별빛 사탕 / 실타래)</summary>
        public static string Title(CurrencyType type) => RewardService.CurrencyName(type);

        /// <summary>어디에 쓰는지 한 줄</summary>
        public static string Body(CurrencyType type) => type switch
        {
            CurrencyType.Gold => "수선점 강화와 상점 재료에 써요",
            CurrencyType.Gem => "친구 소환과 빠른 수선, 상점에서 써요",
            CurrencyType.EventToken => "이벤트 상점에서 선물로 바꿔요",
            CurrencyType.LineageXp => "유실자 레벨업에 써요",
            _ => string.Empty,
        };

        /// <summary>얻는 곳 한 줄 (말풍선 맨 아래, 라벤더)</summary>
        public static string SourceLine(CurrencyType type) => type switch
        {
            CurrencyType.Gold => "얻는 곳 · 수선 바구니 · 스테이지 · 원정 · 던전",
            CurrencyType.Gem => "얻는 곳 · 우편 · 수선 일지 · 출석부 · 수선 바구니",
            CurrencyType.EventToken => "얻는 곳 · 이벤트 · 수선 바구니(이벤트 기간)",
            CurrencyType.LineageXp => "얻는 곳 · 단추가 나오는 모든 곳 (바구니 · 스테이지 · 원정 · 던전 · 일지 · 출석)",
            _ => string.Empty,
        };

        // ───────────── 이동 ─────────────

        /// <summary>
        /// 그 줄의 화면으로 간다 (UINav). 잠겼으면 잠금 안내 토스트만 띄우고 false.
        /// 우편은 우편함을 열고, 스테이지는 이동 없이 true (전투 화면은 늘 위에 보인다 — 부르는 쪽 팝업이 닫히면 보인다).
        /// 부르는 쪽은 자기 팝업을 먼저 닫는다.
        /// </summary>
        public static bool Go(in CurrencySource source)
        {
            if (!source.IsUnlocked)
            {
                if (Game.IsReady && Game.Features != null && !Game.Features.TryUse(source.Feature, out var locked)) Toast(locked);
                return false;
            }
            switch (source.Kind)
            {
                case CurrencySourceKind.Mail:
                    var ui = UIManager.Instance;
                    if (ui == null) return false;
                    var mail = ui.Get<MailPopup>();
                    if (mail == null) return false;
                    mail.Open();
                    return true;
                case CurrencySourceKind.IdleChest:
                    UINav.OpenIdleChest();
                    return true;
                case CurrencySourceKind.Stage:
                    return true;
                case CurrencySourceKind.Expedition:
                    UINav.GoToFeature(FeatureId.Expedition);
                    return true;
                case CurrencySourceKind.Journal:
                    UINav.GoToFeature(FeatureId.Missions);
                    return true;
                case CurrencySourceKind.Attendance:
                    UINav.GoToFeature(FeatureId.Attendance);
                    return true;
                case CurrencySourceKind.Dungeon:
                    UINav.GoToFeature(FeatureId.Dungeon);
                    return true;
                case CurrencySourceKind.Shop:
                    UINav.GoToFeature(FeatureId.Shop);
                    return true;
                case CurrencySourceKind.Events:
                    UINav.GoToFeature(FeatureId.Events);
                    return true;
                case CurrencySourceKind.EventShop:
                    UINav.GoToSource(ItemSourceType.Event, string.Empty);
                    return true;
                default:
                    return false;
            }
        }

        static void Toast(string message)
        {
            if (!string.IsNullOrEmpty(message) && UIManager.Instance != null) UIManager.Instance.ShowToast(message);
        }
    }
}
