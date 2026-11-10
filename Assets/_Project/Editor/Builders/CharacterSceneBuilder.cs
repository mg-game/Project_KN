using System;
using System.Collections.Generic;
using System.IO;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.SceneParts;
using static IdleGame.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 캐릭터 화면: 친구들 탭(계보 도감) / 캐릭터 상세(진화 기억도·스킬·시너지·의상·정보)·레시피·진화 제작·형태 정보·의상 도감 팝업 /
    /// 소환 탭·소환 결과 팝업. 판 · 알약 · 받침 · 고리 · 막대 · 배지는 힉스필드 공용 조각(UiArt)으로 그리고,
    /// 초상화 배경은 portrait_bg(네모) / ui_plate_round(원), 소환 카드는 card_bg_grade1~5 / gacha_card_back을 쓴다.
    /// 아트가 없으면 계보 색 도형과 코드로 만든 빛 스프라이트로 대신한다 (UiArt가 경고를 한 번 남긴다).
    /// 색 있는 그림은 흰색 그대로(또는 흰색 → 색 35% 이하), 흰색 틴트용 조각(알약 · 탭 · 고리 · 막대 채움 · 꺾쇠 · FX)만 상태 색을 입힌다.
    /// 스킬/특성/성향/시너지 아이콘은 CharacterIconLibrary 에셋에 모아 런타임 뷰에 연결한다.
    /// </summary>
    internal static class CharacterSceneBuilder
    {
        const string PrefabDir = IdleProjectSetup.Root + "/Prefabs/UI/Characters";
        const string GeneratedDir = IdleProjectSetup.Root + "/Sprites/UI/Characters";
        const string IconLibraryPath = PrefabDir + "/CharacterIcons.asset";

        /// <summary>조합 시너지 ID (SPEC2). 데이터베이스가 비어 있어도 아트를 미리 묶어 둔다.</summary>
        static readonly string[] ComboSynergyIds =
        {
            "combo_mending_duo", "combo_rainy_lantern", "combo_locked_musicbox", "combo_vanity_secret",
            "combo_night_walk", "combo_parade", "combo_key_and_lock", "combo_shop_family",
            "combo_clear_sky", "combo_last_song", "combo_waiting_end", "combo_seven_lights",
        };

        /// <summary>
        /// 버튼 색 계열 → SceneParts.ButtonStyle (M25 · M26): Gold = 주기 · 확정 (진화 제작, 10회 소환), Primary = 나아가기 · 되풀이 (가기 ›, …하러 가기, 레벨업, 1회 소환),
        /// Secondary = 보조 · 짝지은 확인 (정렬, 도감, 레시피 보기), Soft = 크림 카드 위 작은 버튼, Ghost = 바탕 없는 글자 링크 (라벤더 + ' ›').
        /// 한 화면에 금 버튼은 하나 (준비 완료 금 덮개 ReadyArt는 예외).
        /// </summary>
        enum Tone
        {
            Primary,
            Gold,
            Secondary,
            Soft,
            Ghost,
        }

        static Sprite _glow;
        static Sprite _sparkle;
        static Sprite _ring;
        /// <summary>돌아가는 진화 빛 고리 (VFX/vfx_reveal_light, 없으면 _ring)</summary>
        static Sprite _revealLight;
        /// <summary>큰 제목 반짝이 (VFX/vfx_gloss_shine, 없으면 _sparkle)</summary>
        static Sprite _gloss;
        static Sprite _fadeUp;
        static Sprite _fadeDown;
        /// <summary>왼쪽이 진하고 오른쪽으로 옅어지는 판 (소환 배너 글 받침, M84)</summary>
        static Sprite _fadeLeft;
        static CharacterIconLibrary _icons;

        /// <summary>크기가 정해진 뒤 높이에 맞춰 배율을 정할 9-slice (FitSlices)</summary>
        static readonly List<(Image image, SliceKind kind)> _pendingSlices = new();

        /// <summary>9-slice 배율을 높이로 정하는 방식</summary>
        enum SliceKind
        {
            /// <summary>버튼: UiArt.ButtonPpu (90px 이상 → 2, 60px → 2.8, 40px → 3.5)</summary>
            Button,
            /// <summary>192x96 캡슐(알약): 96 / 높이</summary>
            Capsule,
        }

        /// <summary>크림 카드 안의 목록 줄 바탕</summary>
        static readonly Color RowWash = new(0.91f, 0.84f, 0.71f, 0.55f);
        static readonly Color NightShade = new(0.06f, 0.04f, 0.13f, 0.92f);

        // ═════════════════════ 탭 패널 ═════════════════════

        /// <summary>
        /// 친구들 탭 패널 (MainTab.Characters, 시트 안 780px): 머리 줄 88 (PanelHeader '친구들', M51) — 왼쪽 칸 도감 입구 CodexButton 230x56 (Q-05: 밤 알약 +
        /// 금테 막대, 누르는 자리 88, 빨간 점 RedDotPaths.Codex, 처음 열렸을 때 NEW 알약) + '진화 제작 가능' 칩,
        /// 오른쪽 칸 정렬 '⇅ 등급순' 보조 180x64 (M22 — '⇅'는 Noto Sans KR에 있다) / 격자 96~ (Grid4 240x330, 간격 16, 좌우 36 — 첫 열 x 36, M24).
        /// 코치 경로 CharacterPanel/Grid는 그대로.
        /// </summary>
        public static GameObject BuildCharacterPanel(SceneBuildContext ctx)
        {
            PrepareSprites();
            PrepareIcons(ctx);
            var cardPrefab = BuildCharacterCardPrefab();

            var root = NewRect("CharacterPanel", ctx.ContentArea).Stretch();
            var view = root.gameObject.AddComponent<CharacterPanel>();
            PanelHeader(root, "친구들", out var headerLeft, out var headerRight);

            // 도감 입구 (Q-05, SPEC4 §6.2): 밤색 알약 + 금테 막대 = 버튼 CodexButton (머리 왼쪽 칸). 도감이 열리면 '도감 n%', 열리기 전에는 '도감 보유/전체'
            // (누르면 잠금 안내). 누르는 자리는 위아래 16씩 = 머리 줄 88 (M22). 처음 열렸을 때 NEW 알약, 받을 도감 보상은 빨간 점
            var collectionImage = UiArt.Panel(headerLeft, "CodexButton", UiArt.PillNight9s, UiArt.CapsulePpu(56), null, null, true);
            var collection = collectionImage.rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(230, 56));
            var codexButton = AddButton(collectionImage);
            codexButton.transition = Selectable.Transition.None;
            collectionImage.gameObject.AddComponent<PressScale>();
            collectionImage.HitPad(new Vector2(230, 56), 0f, UISize.MinHit, maxB: 16f, maxT: 16f);
            var collectionText = Txt(collection, "Text", "도감 0/0", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            collectionText.rectTransform.Stretch(24, 14, 0, 14);
            UiArt.Bar(collection, "Bar", UITheme.GoldAccent, out var collectionFill, 12).rectTransform.Bottom(7, 12, 24, 24);
            var codexNew = Pill(collection, "NewPill", "NEW", UITheme.Gold, UITheme.Ink, UIType.Micro, out _);
            codexNew.rectTransform.At(TopRight, new Vector2(12, 14), new Vector2(60, 28));
            codexNew.gameObject.SetActive(false);
            MetaSceneBuilder.AttachRedDot(collection, RedDotPaths.Codex, false, new Vector2(8, 10), 26);

            var alertChip = Pill(headerLeft, "AlertChip", "진화 제작 가능 0", UITheme.RedDot, Color.white, 22, out var alertText);
            alertChip.rectTransform.At(MiddleLeft, new Vector2(242, 0), new Vector2(220, 44));

            // 정렬: 보조 버튼 180x64, 머리 오른쪽 칸 가운데 오른쪽. 누르는 자리는 위아래 12씩 = 머리 줄 88 그대로 (M22 —
            // 위는 시트 머리 아래 끝, 아래는 격자 96 앞에서 멈춘다)
            var sort = ToneButton(headerRight, "SortButton", "⇅ 등급순", Tone.Secondary, UIType.ButtonS, out var sortLabel);
            ((RectTransform)sort.transform).At(MiddleRight, Vector2.zero, new Vector2(180, 64));
            sort.targetGraphic.HitPad(new Vector2(180, 64), 0f, UISize.MinHit, maxB: 12f, maxT: 12f);

            var grid = ScrollList(NewRect("Grid", root).Stretch(0, 0, UISpace.HeaderContentTop, 0), out _);
            Grid4(grid, 330);

            new Binder(view)
                .Set("_gridContent", grid)
                .Set("_cardPrefab", cardPrefab)
                .Set("_collectionText", collectionText)
                .Set("_collectionFill", collectionFill)
                .Set("_sortButton", sort)
                .Set("_sortLabel", sortLabel)
                .Set("_alertChip", alertChip.gameObject)
                .Set("_alertText", alertText)
                .Set("_codexButton", codexButton)
                .Set("_codexNew", codexNew.gameObject)
                .Apply();
            FitSlices();
            root.SetInactive();
            return root.gameObject;
        }

        /// <summary>
        /// 소환 탭 패널 (MainTab.Gacha, 시트 안 780px): 배너 20–360 (글 받침 M84) / 확률 372–430 / 천장 440–512 (글 + 막대, M63) /
        /// 소환 버튼 526–676 (비용 버튼 세 상태, M07) / 별조각 얻는 곳 링크 682–714 (모자랄 때만, M08) / 소환 마일스톤 줄 718–774 (PRG-04).
        /// 예전 안내 줄(중복 → 기억편, 688–764)은 마일스톤 줄이 대신하고 그 글은 마일스톤 창 아래로 옮겼다 (M80 한 화면 안내 한 줄).
        /// 코치 경로 GachaPanel/Buttons/Pull1Button은 그대로.
        /// </summary>
        public static GameObject BuildGachaPanel(SceneBuildContext ctx)
        {
            PrepareSprites();
            var table = ctx.Content.GachaTables[0];
            var root = NewRect("GachaPanel", ctx.ContentArea).Stretch();
            var view = root.gameObject.AddComponent<GachaPanel>();

            // 배너: 넓은 배너(3:1)를 둥근 틀에 꽉 채운다. 없으면 예전 배너(2:1)를 위쪽 기준으로 채워 아래만 잘리게 한다.
            var banner = Img(root, "Banner", UITheme.Ground, RoundSprite).rectTransform.Top(20, 340, 30, 30);
            var bannerMask = banner.gameObject.AddComponent<Mask>();
            bannerMask.showMaskGraphic = true;
            var wideBanner = ArtLib.UI("gacha_banner_wide");
            var bannerArt = wideBanner != null ? wideBanner : ArtLib.UI("gacha_banner");
            if (bannerArt != null)
            {
                bannerMask.showMaskGraphic = false;
                var art = Img(banner, "Art", Color.white, bannerArt);
                art.rectTransform.Stretch();
                if (wideBanner == null) art.rectTransform.pivot = TopCenter;
                Envelope(art, bannerArt);
            }
            else
            {
                BuildBannerFallback(banner, ctx.Characters);
            }
            var sparkles = Burst(banner, "Sparkles", 0f, 38f);
            Rt(sparkles).Stretch();
            Img(banner, "Shade", new Color(0.05f, 0.03f, 0.12f, 0.88f), _fadeUp).rectTransform.Bottom(0, 190);
            // 글 받침 (M84): 왼쪽 아래가 진하고 오른쪽으로 옅어지는 판 — 밝은 배너 그림 위에서도 제목 · 부제가 읽히게 (누름 안 받음)
            Img(banner, "TextScrim", new Color(0.05f, 0.03f, 0.12f, 0.62f), _fadeLeft).rectTransform
                .At(BottomLeft, Vector2.zero, new Vector2(600, 230));
            var title = Txt(banner, "TitleText", table.displayName, 54, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            title.rectTransform.At(BottomLeft, new Vector2(36, 70), new Vector2(540, 72));
            AutoSize(title, 42, 54);
            var subtitle = Txt(banner, "Subtitle", "새로운 계보의 친구들이 수선점에서 기다리고 있어요", UIType.Label, UITheme.Lavender, TextAlignmentOptions.MidlineLeft)
                .Ellipsis();
            subtitle.rectTransform.At(BottomLeft, new Vector2(38, 30), new Vector2(560, 38));
            AutoSize(subtitle, UIType.Hint, UIType.Label);

            // 별 개수별 확률
            var rates = NewRect("Rates", root).Top(372, 58, 30, 30);
            AddRow(rates, 10);
            var rateTexts = new Object[5];
            for (int i = 0; i < rateTexts.Length; i++)
            {
                Pill(rates, $"Rate{i}", "★ 0%", UITheme.PanelLight, UITheme.Cream, 24, out var rateText);
                rateTexts[i] = rateText;
            }

            // 천장 (M63): 윗줄 글(34) + 아랫줄 꽉 찬 막대 18 — 바탕(PanelLight 알약)은 0이어도 보이고, 채움은 등급 색 흰 판, 20%마다 옅은 눈금
            var pityRow = NewRect("Pity", root).Top(440, 72, 30, 30);
            var pity = Txt(pityRow, "PityText", string.Empty, UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft);
            pity.rectTransform.Top(0, 34);
            var pityTrack = UiArt.Panel(pityRow, "PityBar", UiArt.PillWhite9s, UiArt.CapsulePpu(18), UITheme.PanelLight, UITheme.PanelLight);
            pityTrack.rectTransform.Bottom(6, 18);
            var pityFillImage = Img(pityTrack.transform, "Fill", UITheme.GradeColor(table.pityGrade), WhiteSprite);
            var pityFill = pityFillImage.rectTransform;
            pityFill.anchorMin = Vector2.zero;
            pityFill.anchorMax = new Vector2(0f, 1f);
            pityFill.pivot = new Vector2(0f, 0.5f);
            pityFill.offsetMin = new Vector2(3, 3);
            pityFill.offsetMax = new Vector2(-3, -3);
            var pityFlash = Img(pityFill, "Flash", UITheme.WithAlpha(UITheme.Cream, 0f), WhiteSprite);
            pityFlash.rectTransform.Stretch();
            pityFlash.enabled = false;
            for (int i = 1; i <= 4; i++)
            {
                var tick = Img(pityTrack.transform, $"Tick{i}", UITheme.WithAlpha(UITheme.Cream, 0.25f), WhiteSprite).rectTransform;
                tick.anchorMin = tick.anchorMax = new Vector2(i * 0.2f, 0.5f);
                tick.pivot = Center;
                tick.anchoredPosition = Vector2.zero;
                tick.sizeDelta = new Vector2(2, 10);
            }

            // 소환 버튼 (M07): 둘 다 비용 버튼 세 상태 — 1회 = 보라(어두운 바탕), 10회 = 금. 모자라도 누를 수 있다 (흔들기 + 부족 팝업)
            var row = NewRect("Buttons", root).Top(526, 150, 30, 30);
            AddRow(row, 20);
            var pull1 = CostButton(row, "Pull1Button", "1회 소환", table.costCurrency, UITheme.PrimaryButton, out var pull1Title, out var cost1);
            ApplyArt(pull1, Tone.Primary);
            var pull1View = CostBtn(pull1, cost1, CostSurface.Dark, pull1Title);
            var pull10 = CostButton(row, "Pull10Button", "10회 소환", table.costCurrency, UITheme.Gold, out var pull10Title, out var cost10);
            ApplyArt(pull10, Tone.Gold);
            pull10Title.color = UITheme.Ink;
            cost10.color = UITheme.Ink;
            var pull10View = CostBtn(pull10, cost10, CostSurface.Gold, pull10Title);
            var discount = SaleTag(pull10.transform, "Discount", "10% 할인", 20, out var discountText);
            discount.rectTransform.At(TopRight, new Vector2(16, 18), new Vector2(132, 44));

            // 소환 마일스톤 줄 (PRG-04 · SPEC4 §6.4, Top(718, 56, 30, 30)): '누적 소환 n' (Label 24 굵게 Cream) / 막대 520×18 GoldAccent + 단계 점 5 /
            // 다음 보상 칩 / 받을 수 있으면 금 '받기' 96×40. 줄 전체가 투명 판 버튼 → GachaMilestonePopup (빨간 점 RedDotPaths.GachaMilestone).
            // 누르는 자리: 위 26 · 아래 6 = 88 (M22). 별조각 링크(뒤 형제)는 줄 위 4px 틈까지만 받는다 — 둘 다 보일 때 줄 몸통(718~774)은 늘 줄이 받고,
            // 가운데 420폭의 위 넓힘(692~718)만 링크 몫이다
            var milestoneImage = Img(root, "MilestoneRow", Color.clear, null, true);
            var milestoneRect = milestoneImage.rectTransform.Top(718, 56, 30, 30);
            var milestoneRow = AddButton(milestoneImage);
            milestoneRow.transition = Selectable.Transition.None;
            milestoneImage.gameObject.AddComponent<PressScale>();
            // 한쪽으로 치우친 넓힘이라 raycastPadding을 바로 쓴다 (HitPad는 모자란 만큼을 반씩 나눈 뒤 각 쪽을 잘라 78에 그친다): 56 + 아래 6 + 위 26 = 88
            milestoneImage.raycastPadding = new Vector4(0f, -6f, 0f, -(UISize.MinHit - 56f - 6f));
            var milestoneText = Txt(milestoneRect, "PullsText", "누적 소환 0", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            milestoneText.rectTransform.At(MiddleLeft, new Vector2(4, 0), new Vector2(206, 40));
            AutoSize(milestoneText, UIType.Hint, UIType.Label);
            var milestoneTrack = UiArt.Bar(milestoneRect, "Bar", UITheme.GoldAccent, out var milestoneFill, 18);
            milestoneTrack.rectTransform.At(MiddleLeft, new Vector2(220, 0), new Vector2(520, 18));
            var milestonePips = new Object[5];
            for (int i = 0; i < milestonePips.Length; i++)
            {
                var pip = Img(milestoneTrack.transform, $"Pip{i}", UITheme.WithAlpha(UITheme.Cream, 0.45f), UiArt.PipButton);
                pip.type = Image.Type.Simple;
                pip.preserveAspect = true;
                var pipRect = pip.rectTransform;
                pipRect.anchorMin = pipRect.anchorMax = new Vector2((i + 1) / 5f, 0.5f);
                pipRect.pivot = Center;
                pipRect.anchoredPosition = Vector2.zero;
                pipRect.sizeDelta = new Vector2(26, 26);
                milestonePips[i] = pip;
            }
            var milestoneReward = RewardChip(milestoneRect, "NextReward", new Vector2(64, 58), 30, UIType.Micro);
            ((RectTransform)milestoneReward.transform).At(MiddleRight, new Vector2(-112, 0), new Vector2(64, 58));
            var milestoneClaim = Pill(milestoneRect, "ClaimPill", "받기", UITheme.Gold, UITheme.Ink, UIType.Hint, out _);
            milestoneClaim.rectTransform.At(MiddleRight, Vector2.zero, new Vector2(96, 40));
            milestoneClaim.gameObject.SetActive(false);
            MetaSceneBuilder.AttachRedDot(milestoneRect, RedDotPaths.GachaMilestone, false, new Vector2(10, 14), 26);

            // 별조각 얻는 곳 (M08): 어느 쪽 소환이든 모자랄 때만 켜지는 글자 링크 (Ghost 22 Lavender + ' ›').
            // 누르는 자리는 위(소환 버튼과 6px)로 3px, 아래로 마일스톤 줄(718) 위 틈 4px까지 — 32 + 7 = 39 (88 미만: 위는 소환 버튼, 아래는 누르는
            // 마일스톤 줄에 막힌다 — UiSceneLint 빚 예외 GachaPanel/GemSourceLink). 예전에는 아래 안내 글(누름 없음)로 53px 넓혔지만 그 자리에
            // 마일스톤 줄이 들어왔고, 링크가 뒤 형제라 그만큼 넓히면 줄의 막대 · 단계 점 누름(718~767)을 가로챈다 (PRG-04)
            string currencyName = RewardService.CurrencyName(table.costCurrency);
            var gemLink = SceneParts.Button(root, "GemSourceLink", $"{currencyName} 얻는 곳", ButtonStyle.Ghost, UIType.Caption, out var gemLinkLabel);
            ((RectTransform)gemLink.transform).At(TopCenter, new Vector2(0, -682), new Vector2(420, 32));
            gemLinkLabel.rectTransform.Stretch();
            AutoSize(gemLinkLabel, UIType.Hint, UIType.Caption);
            const float gemLinkPadTop = 3f;
            const float gemLinkPadBottom = 718f - (682f + 32f);
            gemLink.targetGraphic.raycastPadding = new Vector4(0f, -gemLinkPadBottom, 0f, -gemLinkPadTop);
            gemLink.gameObject.SetActive(false);

            new Binder(view)
                .Set("_table", table)
                .Set("_titleText", title)
                .Set("_bannerSparkles", sparkles)
                .SetArray("_rateTexts", rateTexts)
                .Set("_pityText", pity)
                .Set("_pityFill", pityFill)
                .Set("_pityFlash", pityFlash)
                .Set("_pull1Button", pull1)
                .Set("_pull10Button", pull10)
                .Set("_cost1Text", cost1)
                .Set("_cost10Text", cost10)
                .Set("_discountText", discountText)
                .Set("_pull1View", pull1View)
                .Set("_pull10View", pull10View)
                .Set("_gemLinkButton", gemLink)
                .Set("_gemLinkLabel", gemLinkLabel)
                .Set("_milestoneRow", milestoneRow)
                .Set("_milestoneText", milestoneText)
                .Set("_milestoneFill", milestoneFill)
                .SetArray("_milestonePips", milestonePips)
                .Set("_milestoneReward", milestoneReward)
                .Set("_milestoneClaim", milestoneClaim.gameObject)
                .Apply();
            FitSlices();
            root.SetInactive();
            return root.gameObject;
        }

        /// <summary>배너 아트가 없을 때: 밤하늘 그라데이션 + 빛 + 높은 등급 친구들</summary>
        static void BuildBannerFallback(RectTransform banner, CharacterDatabase db)
        {
            Img(banner, "Sky", new Color(0.54f, 0.42f, 0.88f, 0.6f), _fadeDown).rectTransform.Stretch();
            Img(banner, "GlowPink", new Color(0.91f, 0.65f, 0.78f, 0.55f), _glow).rectTransform.At(Center, new Vector2(250, -20), new Vector2(620, 620));
            Img(banner, "GlowBlue", new Color(0.58f, 0.78f, 0.94f, 0.4f), _glow).rectTransform.At(Center, new Vector2(-340, 90), new Vector2(460, 460));
            Img(banner, "MoonGlow", new Color(1f, 0.95f, 0.85f, 0.45f), _glow).rectTransform.At(TopLeft, new Vector2(20, 20), new Vector2(200, 200));
            Img(banner, "Moon", UITheme.Cream, CircleSprite).rectTransform.At(TopLeft, new Vector2(80, -40), new Vector2(80, 80));
            Img(banner, "MoonBite", new Color(0.62f, 0.5f, 0.9f, 1f), CircleSprite).rectTransform.At(TopLeft, new Vector2(100, -30), new Vector2(72, 72));

            // 높은 등급 계보 기본형 아트 (최대 3)
            var featured = new List<LineageDef>();
            if (db != null)
            {
                foreach (var lineage in db.lineages)
                {
                    if (lineage != null && ArtLib.Character(lineage.baseFormId) != null) featured.Add(lineage);
                }
            }
            featured.Sort((a, b) => b.grade.CompareTo(a.grade));
            var slots = new[] { (x: -40f, size: 320f), (x: -280f, size: 250f), (x: -470f, size: 210f) };
            for (int i = Mathf.Min(featured.Count, slots.Length) - 1; i >= 0; i--)
            {
                var art = Img(banner, $"Featured{i}", Color.white, ArtLib.Character(featured[i].baseFormId));
                art.preserveAspect = true;
                art.rectTransform.At(BottomRight, new Vector2(slots[i].x, -6), new Vector2(slots[i].size, slots[i].size));
            }
        }

        // ═════════════════════ 팝업 ═════════════════════

        /// <summary>캐릭터 관련 팝업을 만들어 ctx.Popups에 추가하고, 캐릭터 열기 요청 수신기를 붙인다.</summary>
        public static void BuildPopups(SceneBuildContext ctx)
        {
            PrepareSprites();
            PrepareIcons(ctx);
            var nodePrefab = BuildNodePrefab();
            var costumePrefab = BuildCostumeItemPrefab();
            var materialPrefab = BuildRecipeMaterialPrefab();
            var conditionPrefab = BuildRecipeConditionPrefab();
            var sourcePrefab = BuildRecipeSourcePrefab();
            var gachaCardPrefab = BuildGachaCardPrefab();
            var synergyRowPrefab = BuildSynergyRowPrefab();
            var formSkillPrefab = BuildFormSkillRowPrefab();

            ctx.Popups.Add(BuildDetailPopup(ctx, nodePrefab, costumePrefab, synergyRowPrefab, formSkillPrefab));
            ctx.Popups.Add(BuildFormInfoPopup(ctx, synergyRowPrefab));
            ctx.Popups.Add(BuildRecipePopup(ctx, materialPrefab, conditionPrefab, sourcePrefab));
            ctx.Popups.Add(BuildCraftPopup(ctx));
            ctx.Popups.Add(BuildIllustrationPopup(ctx));
            ctx.Popups.Add(BuildGachaResultPopup(ctx, gachaCardPrefab));
            ctx.Popups.Add(BuildGachaMilestonePopup(ctx));
            ctx.Popups.Add(BuildFriendTicketPopup(ctx));

            // 탭/팝업이 꺼져 있어도 UINav.OpenCharacter를 받을 수 있게 항상 켜진 팝업 루트에 둔다
            NewRect("CharacterNavRouter", ctx.PopupRoot).gameObject.AddComponent<CharacterNavRouter>();
            FitSlices();
        }

        // ───────────── 캐릭터 상세 (와이어프레임 01) ─────────────

        /// <summary>
        /// 친구 상세 (본문 928x1548): 머리 0–340 (초상화 · 이름 · 칩) / 보유 계보 레벨 칸 · 호감도 · 기억편 · 공명 칸 208–470 / 탭 484–564 / 쪽 578~.
        /// 창 왼쪽 위 단추 잔액 칩 (M47, Window 바로 아래 — Body 경로는 그대로). 레벨업 = 보라 비용 버튼 + 길게 누르기, 공명 = 보조 버튼 + 준비 완료 금 덮개 (M54).
        /// </summary>
        static UIPopup BuildDetailPopup(SceneBuildContext ctx, EvolutionNodeView nodePrefab, CostumeItemView costumePrefab,
            LineageSynergyRowView synergyRowPrefab, FormSkillRowView formSkillPrefab)
        {
            var popup = BuildPopup<CharacterDetailPopup>(ctx.PopupRoot, "CharacterDetailPopup", "친구 상세", new Vector2(1000, 1720), out var body, out var title);
            PopupBalanceChip((RectTransform)body.parent, CurrencyType.Gold);
            AutoSize(title, UIType.H2, UIType.H1);

            // 머리: 초상화 + 이름 / 종족 / 별 / 역할·성향·특성 칩
            var frame = InsetPanel(body, "PortraitFrame").rectTransform.At(TopLeft, Vector2.zero, new Vector2(300, 340));
            var glow = Img(frame, "Glow", new Color(1f, 1f, 1f, 0.6f), _glow);
            glow.rectTransform.At(Center, new Vector2(0, 30), new Vector2(440, 440));
            Pulse(glow, 0.3f, 0.7f, 2.4f, 0.04f);
            var portrait = Portrait(frame, "Portrait", new Vector2(288, 328), PortraitBackdrop.Night, false, new Vector4(0, 0, 18, 0));
            Rt(portrait).At(Center, Vector2.zero, new Vector2(288, 328));
            CardFrameOverlay(frame, 1.4f);
            // 호감도 Lv5 하트 바느질 틀 (CR-10 §6.3 — 친구들 카드와 같은 틀을 머리 초상화에도): 전용 아트가 없으면 틴트용 틀을 DustyPink로 (HasAffinityFrame9s)
            var portraitAffinityFrame = UiArt.Panel(frame, "AffinityFrame", UiArt.AffinityFrame9s, 2f,
                UiArt.HasAffinityFrame9s ? Color.white : UITheme.WithAlpha(UITheme.DustyPink, 0.9f), UITheme.WithAlpha(UITheme.DustyPink, 0.6f));
            portraitAffinityFrame.rectTransform.Stretch(-2, -2, -2, -2);
            portraitAffinityFrame.gameObject.SetActive(false);

            var name = Txt(body, "NameText", "이름", 46, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            name.rectTransform.At(TopLeft, new Vector2(324, 0), new Vector2(604, 66));
            AutoSize(name, 30, 46);
            var species = Txt(body, "SpeciesText", "종족", 25, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
            species.rectTransform.At(TopLeft, new Vector2(326, -64), new Vector2(602, 40));
            var stars = Txt(body, "StarsText", UITheme.Stars(ItemGrade.Epic), 32, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            stars.rectTransform.At(TopLeft, new Vector2(324, -104), new Vector2(420, 46));
            var chipRow = ChipRow(body, "Chips", 8, TextAnchor.MiddleLeft);
            chipRow.At(TopLeft, new Vector2(324, -156), new Vector2(604, 42));
            var roleChip = Chip(chipRow, "RoleChip", 42, 21, true, UITheme.Guardian, UITheme.Ink, "지킴이");
            var styleChip = Chip(chipRow, "StyleChip", 42, 21, true, UITheme.SoftBlue, UITheme.Ink, "방어");
            var traitChips = new Object[2];
            for (int i = 0; i < traitChips.Length; i++) traitChips[i] = Chip(chipRow, $"TraitChip{i}", 42, 21, true, UITheme.Cream, UITheme.Ink, "특성");

            // 보유 계보: 레벨 / 호감도 / 기억편 / 계보 공명
            var owned = NewRect("OwnedGroup", body).Top(0, 470);
            var levelBox = InsetPanel(owned, "LevelBox").rectTransform.At(TopLeft, new Vector2(324, -208), new Vector2(604, 132));
            // 캡션 22 (M29 · S §1.3 하한): 칸이 모자라면 20까지 줄인다
            var levelCaption = Txt(levelBox, "Caption", "계보 레벨 · 모든 형태가 함께 성장", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
            levelCaption.rectTransform.At(TopLeft, new Vector2(22, -10), new Vector2(360, 32));
            AutoSize(levelCaption, UIType.Hint, UIType.Caption);
            var level = Txt(levelBox, "LevelText", "Lv.1", 46, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            level.rectTransform.At(BottomLeft, new Vector2(22, 10), new Vector2(340, 76));
            // 레벨업 (M19 · M07): 보라 비용 버튼 세 상태 (어두운 바탕) + 길게 누르면 반복 (런타임이 Step · Denied를 건다)
            var levelUp = ToneButton(levelBox, "LevelUpButton", "레벨업", Tone.Primary, 28, out var levelUpLabel);
            ((RectTransform)levelUp.transform).At(MiddleRight, new Vector2(-12, 0), new Vector2(220, 112));
            levelUpLabel.rectTransform.Top(10, 44, 8, 8);
            var levelCost = CostLine(levelUp.transform, CurrencyType.Gold, 30, 26, 58);
            var levelUpView = CostBtn(levelUp, levelCost, CostSurface.Dark, levelUpLabel);
            var levelUpHold = levelUp.gameObject.AddComponent<HoldRepeatButton>();

            var affinityTile = StatTile(owned, "AffinityTile", 0, 214, "호감도", out var affinity);
            var affinityArt = ArtLib.Icon("affinity");
            if (affinityArt != null) UiArt.Icon(affinityTile, "Heart", affinityArt, TopLeft, new Vector2(21, -18), 40);
            else Txt(affinityTile, "Heart", "♥", 40, UITheme.DustyPink, TextAlignmentOptions.Center, true).rectTransform.At(TopLeft, new Vector2(16, -10), new Vector2(50, 56));
            var shardTile = StatTile(owned, "ShardTile", 228, 214, "기억편", out var shards);
            ShardIcon(shardTile, 34).At(TopLeft, new Vector2(24, -21), new Vector2(34, 34));

            var resonanceTile = InsetPanel(owned, "ResonanceTile").rectTransform.At(TopLeft, new Vector2(456, -352), new Vector2(472, 118));
            var resonance = Txt(resonanceTile, "ResonanceText", "계보 공명 Lv.0", 28, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            resonance.rectTransform.At(TopLeft, new Vector2(20, -14), new Vector2(280, 42));
            // 공명 효과 줄 '계보 능력치 +5% → +10%' (M60B, Positive — 어두운 칸, 자동 크기 16까지)
            var resonanceBonus = Txt(resonanceTile, "BonusText", "계보 능력치 +0%", 21, UITheme.Positive, TextAlignmentOptions.MidlineLeft);
            resonanceBonus.rectTransform.At(TopLeft, new Vector2(20, -56), new Vector2(280, 32));
            AutoSize(resonanceBonus, UIType.AutoSizeFloor, 21);
            // 안내 한 줄 (M80: Hint 20, 최소 18)
            var resonanceHint = Txt(resonanceTile, "Caption", "기억편으로 계보를 공명시켜요", UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            resonanceHint.rectTransform.At(TopLeft, new Vector2(20, -86), new Vector2(280, 28));
            AutoSize(resonanceHint, UIType.HintMin, UIType.Hint);
            // 공명 버튼 (M54): 보조 버튼 + Cream 이름 · 비용, 준비 완료면 금 덮개(ReadyArt) + Ink (런타임)
            var resonanceButton = ToneButton(resonanceTile, "ResonanceButton", "공명", Tone.Secondary, 26, out var resonanceLabel);
            ((RectTransform)resonanceButton.transform).At(MiddleRight, new Vector2(-10, 0), new Vector2(166, 98));
            resonanceLabel.color = UITheme.Cream;
            resonanceLabel.rectTransform.Top(8, 40, 6, 6);
            var resonanceCost = Txt(resonanceButton.transform, "CostText", "기억편 0", 20, UITheme.Cream, TextAlignmentOptions.Center, true);
            resonanceCost.rectTransform.Top(50, 34, 6, 6);
            var resonanceReady = ReadyArt(resonanceButton);
            var resonanceDot = Dot(resonanceButton.transform, new Vector2(8, 8));

            // 미보유 계보
            var locked = NewRect("LockedGroup", body).Top(0, 470);
            var lockedBox = InsetPanel(locked, "Box").rectTransform.At(TopLeft, new Vector2(324, -208), new Vector2(604, 132));
            Txt(lockedBox, "Text", "아직 만나지 못한 친구예요.\n<size=85%>'잃어버린 친구 찾기'에서 만날 수 있어요.</size>", 25, UITheme.SubText,
                TextAlignmentOptions.Center, false, true).rectTransform.Stretch(16, 16, 8, 8);
            // 이동 버튼은 보라 (M26 — 금은 주기 · 확정에만)
            var gachaButton = ToneButton(locked, "GachaButton", "소환하러 가기", Tone.Primary, 32, out _);
            ((RectTransform)gachaButton.transform).At(TopLeft, new Vector2(0, -352), new Vector2(928, 118));
            locked.SetInactive();

            // 탭
            var tabs = NewRect("Tabs", body).Top(484, 80);
            AddRow(tabs, 12);
            string[] tabNames = { "진화 기억도", "스킬·시너지", "의상", "정보" };
            var tabButtons = new Object[tabNames.Length];
            var tabLabels = new Object[tabNames.Length];
            var tabDots = new Object[tabNames.Length];
            GameObject costumeTabLock = null;
            for (int i = 0; i < tabNames.Length; i++)
            {
                var tab = Btn(tabs, $"Tab{i}", tabNames[i], UITheme.PanelLight, 27, out var tabLabel);
                TabArt(tab);
                PageTabSkin(tab);
                tabButtons[i] = tab;
                tabLabels[i] = tabLabel;
                tabDots[i] = Dot(tab.transform, new Vector2(-12, -10), 22);
                if (i == CharacterDetailPopup.TabCostume)
                {
                    // 의상이 잠긴 동안(2-5 전) 이름 왼쪽 자물쇠 24 (F-03 — 쪽은 그대로 볼 수 있다)
                    var tabLock = LockGlyph(tab.transform, 24, UITheme.WithAlpha(UITheme.Cream, 0.9f));
                    tabLock.At(MiddleLeft, new Vector2(44, 0), new Vector2(24, 24));
                    tabLock.gameObject.SetActive(false);
                    costumeTabLock = tabLock.gameObject;
                }
            }

            var pages = NewRect("Pages", body).Stretch(0, 0, 578, 0);
            var tree = BuildTreePage(pages, nodePrefab);
            var skills = BuildSkillPage(pages, synergyRowPrefab, formSkillPrefab);
            var costumes = BuildCostumePage(pages, costumePrefab);
            var info = BuildInfoPage(pages);
            skills.gameObject.SetActive(false);
            costumes.gameObject.SetActive(false);
            info.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_portrait", portrait)
                .Set("_portraitGlow", glow)
                .Set("_portraitAffinityFrame", portraitAffinityFrame.gameObject)
                .Set("_nameText", name)
                .Set("_speciesText", species)
                .Set("_starsText", stars)
                .Set("_roleChip", roleChip)
                .Set("_styleChip", styleChip)
                .SetArray("_traitChips", traitChips)
                .Set("_ownedGroup", owned.gameObject)
                .Set("_levelText", level)
                .Set("_levelUpButton", levelUp)
                .Set("_levelUpLabel", levelUpLabel)
                .Set("_levelUpCostText", levelCost)
                .Set("_affinityText", affinity)
                .Set("_shardsText", shards)
                .Set("_resonanceText", resonance)
                .Set("_resonanceBonusText", resonanceBonus)
                .Set("_resonanceButton", resonanceButton)
                .Set("_resonanceCostText", resonanceCost)
                .Set("_resonanceDot", resonanceDot)
                .Set("_levelUpView", levelUpView)
                .Set("_levelUpHold", levelUpHold)
                .Set("_resonanceReadyArt", resonanceReady.gameObject)
                .Set("_resonanceLabel", resonanceLabel)
                .Set("_lockedGroup", locked.gameObject)
                .Set("_gachaButton", gachaButton)
                .SetArray("_tabButtons", tabButtons)
                .SetArray("_tabLabels", tabLabels)
                .SetArray("_tabPages", tree.gameObject, skills.gameObject, costumes.gameObject, info.gameObject)
                .SetArray("_tabDots", tabDots)
                .Set("_tree", tree)
                .Set("_skills", skills)
                .Set("_costumes", costumes)
                .Set("_info", info)
                .Set("_costumeTabLock", costumeTabLock)
                .Apply();
            return popup;
        }

        /// <summary>호감도 / 기억편 칸 (ui_panel_inset_9s): 아이콘 자리(왼쪽 위) + 숫자 + 이름</summary>
        static RectTransform StatTile(RectTransform parent, string name, float x, float width, string caption, out TextMeshProUGUI value)
        {
            var tile = InsetPanel(parent, name).rectTransform.At(TopLeft, new Vector2(x, -352), new Vector2(width, 118));
            value = Txt(tile, "ValueText", "0", 34, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            value.rectTransform.At(TopLeft, new Vector2(70, -10), new Vector2(width - 80, 56));
            AutoSize(value, 22, 34);
            // 캡션 22 (M29 · S §1.3 하한)
            var captionText = Txt(tile, "Caption", caption, UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
            captionText.rectTransform.At(BottomLeft, new Vector2(22, 10), new Vector2(width - 30, 34));
            AutoSize(captionText, UIType.Hint, UIType.Caption);
            return tile;
        }

        static EvolutionTreeView BuildTreePage(RectTransform pages, EvolutionNodeView nodePrefab)
        {
            var page = NewRect("TreePage", pages).Stretch();
            var view = page.gameObject.AddComponent<EvolutionTreeView>();

            Txt(page, "Header", "진화 기억도", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.At(TopLeft, new Vector2(4, 0), new Vector2(300, 46));
            var progress = Txt(page, "ProgressText", "해금 0/0", 22, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            progress.rectTransform.At(TopLeft, new Vector2(4, -46), new Vector2(460, 34));

            var filter = NewRect("Filter", page).At(TopRight, new Vector2(0, -10), new Vector2(390, 62));
            AddRow(filter, 8);
            string[] filterNames = { "전체", "지킴이", "잔향이" };
            var filterButtons = new Object[filterNames.Length];
            var filterLabels = new Object[filterNames.Length];
            for (int i = 0; i < filterNames.Length; i++)
            {
                var button = Btn(filter, $"Filter{i}", filterNames[i], UITheme.PanelLight, 24, out var label);
                TabArt(button);
                filterButtons[i] = button;
                filterLabels[i] = label;
            }

            var card = ParchmentCard(page, "TreeCard").rectTransform.Stretch(0, 0, 90, 124);
            // 범례: 노드 고리와 같은 바느질 고리(ui_ring_stitch)를 상태 색으로 — 미발견 고리는 글자가 아닌 선이라 InkLine, 이름은 캡션 22 InkSub (M29)
            var legend = NewRect("Legend", card).Bottom(12, 32, 20, 20);
            var legendRow = HRow(legend, 6, TextAnchor.MiddleCenter);
            legendRow.childControlWidth = true;
            legendRow.childControlHeight = true;
            LegendItem(legend, "Unlocked", UITheme.GoldAccent, "해금");
            LegendGap(legend, 18);
            LegendItem(legend, "Clue", UITheme.Lavender, "단서 발견");
            LegendGap(legend, 18);
            LegendItem(legend, "Undiscovered", UITheme.InkLine, "미발견");
            LegendGap(legend, 18);
            Txt(legend, "Hint", "·    노드를 눌러 확인해요", UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineLeft);
            var area = NewRect("Area", card).Stretch(16, 16, 20, 52);
            var lines = NewRect("Lines", area).Stretch();
            var lineTemplate = Img(lines, "Line", UITheme.InkLine);
            lineTemplate.gameObject.SetActive(false);

            // 진화 레시피 버튼 뒤 금빛 (버튼 크기의 1.3배 부드러운 빛 — 제작 가능할 때만 런타임이 켠다)
            var checkGlow = Img(page, "CheckGlow", new Color(UITheme.GoldAccent.r, UITheme.GoldAccent.g, UITheme.GoldAccent.b, 0.6f), _glow);
            checkGlow.rectTransform.Bottom(-17, 146, -139, -139);
            Pulse(checkGlow, 0.15f, 0.75f, 1.3f, 0f);
            // 진화 레시피 보기 (M54 · M75): 보조 버튼 + Cream 32 (자동 크기 26–34), 제작 가능하면 금 덮개 + Ink 글 (EvolutionTreeView가 상태별 글을 쓴다)
            var check = ToneButton(page, "CheckButton", "진화 레시피 보기 ›", Tone.Secondary, 32, out var checkLabel);
            ((RectTransform)check.transform).Bottom(0, 112);
            checkLabel.color = UITheme.Cream;
            checkLabel.rectTransform.Stretch(24, 24, 0, 0);
            AutoSize(checkLabel, 26, 34);
            var checkReady = ReadyArt(check);

            new Binder(view)
                .Set("_area", area)
                .Set("_lineLayer", lines)
                .Set("_lineTemplate", lineTemplate)
                .Set("_nodePrefab", nodePrefab)
                .Set("_progressText", progress)
                .SetArray("_filterButtons", filterButtons)
                .SetArray("_filterLabels", filterLabels)
                .Set("_checkButton", check)
                .Set("_checkLabel", checkLabel)
                .Set("_checkGlow", checkGlow.gameObject)
                .Set("_checkReadyArt", checkReady.gameObject)
                .Apply();
            return view;
        }

        /// <summary>스킬·시너지 탭: 기본형 고유 스킬 카드 / 시너지 목록 / 형태별 스킬 목록 (한 스크롤)</summary>
        static CharacterSkillView BuildSkillPage(RectTransform pages, LineageSynergyRowView synergyPrefab, FormSkillRowView formPrefab)
        {
            var page = NewRect("SkillPage", pages).Stretch();
            var view = page.gameObject.AddComponent<CharacterSkillView>();

            var content = ScrollList(NewRect("Scroll", page).Stretch(), out var scroll);
            VList(content, 16, new RectOffset(0, 0, 0, 28));
            var skillCard = SkillCard(content, "SkillCard");

            var synergyCard = ListCard(content, "SynergyCard", out var synergyTitle, out var synergyHeader);
            // 도감 열기는 보조 버튼 (M26, 글자 S 24 — 자동 크기 18까지)
            var codex = ToneButton(synergyHeader, "CodexButton", "시너지 도감 ›", Tone.Secondary, UIType.ButtonS, out var codexLabel);
            ((RectTransform)codex.transform).At(MiddleRight, Vector2.zero, new Vector2(176, 46));
            AutoSize(codexLabel, UIType.Micro, UIType.ButtonS);
            var synergyEmpty = Txt(synergyCard, "EmptyLabel", "이 친구와 함께 켜지는 시너지가 아직 없어요.", 22, UITheme.InkSub, TextAlignmentOptions.Center);
            Pref(synergyEmpty, 64);

            // 형태별 스킬: '눌러서 자세히' 안내 글은 없애고 (M80) 줄마다 오른쪽 아래 'i' 표시로 누를 수 있음을 알린다
            var formCard = ListCard(content, "FormSkillCard", out var formsTitle, out _);

            new Binder(view)
                .Set("_scroll", scroll)
                .Set("_skillCard", skillCard)
                .Set("_synergyTitle", synergyTitle)
                .Set("_synergyContent", synergyCard)
                .Set("_synergyPrefab", synergyPrefab)
                .Set("_synergyEmpty", synergyEmpty.gameObject)
                .Set("_codexButton", codex)
                .Set("_formsTitle", formsTitle)
                .Set("_formContent", formCard)
                .Set("_formPrefab", formPrefab)
                .Apply();
            return view;
        }

        static CostumeListView BuildCostumePage(RectTransform pages, CostumeItemView itemPrefab)
        {
            var page = NewRect("CostumePage", pages).Stretch();
            var view = page.gameObject.AddComponent<CostumeListView>();

            var summary = Txt(page, "SummaryText", string.Empty, 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            summary.rectTransform.Top(0, 44, 4, 4);
            var content = ScrollList(NewRect("List", page).Stretch(0, 0, 54, 0), out var scroll);
            VList(content, 14, new RectOffset(0, 0, 0, 24));
            var empty = Txt(page, "EmptyLabel", "아직 이 친구의 외형 이야기가 준비되지 않았어요.", 26, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            empty.rectTransform.Stretch(40, 40, 54, 0);

            new Binder(view)
                .Set("_listContent", content)
                .Set("_itemPrefab", itemPrefab)
                .Set("_summaryText", summary)
                .Set("_emptyLabel", empty.gameObject)
                .Set("_scroll", scroll)
                .Apply();
            return view;
        }

        static CharacterInfoView BuildInfoPage(RectTransform pages)
        {
            var page = NewRect("InfoPage", pages).Stretch();
            var view = page.gameObject.AddComponent<CharacterInfoView>();
            var content = ScrollList(NewRect("Scroll", page).Stretch(), out _);
            VList(content, 14, new RectOffset(0, 0, 0, 28));

            var affinity = BuildAffinitySection(content);

            var storyCard = ParchmentCard(content, "StoryCard").rectTransform;
            Pref(storyCard, 230);
            CardTitle(storyCard, "이야기");
            var story = Txt(storyCard, "StoryText", string.Empty, 25, UITheme.Ink, TextAlignmentOptions.TopLeft, false, true);
            story.rectTransform.Stretch(30, 30, 66, 18);
            story.lineSpacing = 6;

            var profileCard = ParchmentCard(content, "ProfileCard").rectTransform;
            Pref(profileCard, 430);
            CardTitle(profileCard, "프로필");
            var profile = Txt(profileCard, "ProfileText", string.Empty, 24, UITheme.Ink, TextAlignmentOptions.TopLeft, false, true);
            profile.rectTransform.Stretch(30, 30, 66, 74);
            profile.lineSpacing = 10;
            Txt(profileCard, "ColorLabel", "컬러", 22, UITheme.InkSub, TextAlignmentOptions.MidlineLeft).rectTransform.At(BottomLeft, new Vector2(30, 22), new Vector2(90, 44));
            // 색 견본: 흰 단추(ui_pip_button)를 계보 색으로 칠한다 (CharacterInfoView)
            var swatches = new Object[4];
            var pip = UiArt.PipButton;
            for (int i = 0; i < swatches.Length; i++)
            {
                var swatch = Img(profileCard, $"Swatch{i}", Color.white, pip);
                swatch.type = Image.Type.Simple;
                swatch.preserveAspect = true;
                swatch.rectTransform.At(BottomLeft, new Vector2(118 + i * 58, 20), new Vector2(48, 48));
                swatches[i] = swatch;
            }

            var statsCard = ParchmentCard(content, "StatsCard").rectTransform;
            Pref(statsCard, 382);
            var statsTitle = CardTitle(statsCard, "능력치");
            var stats = Txt(statsCard, "StatsText", string.Empty, 23, UITheme.Ink, TextAlignmentOptions.TopLeft, false, true);
            stats.rectTransform.Stretch(30, 30, 70, 18);
            stats.lineSpacing = 14;

            new Binder(view)
                .Set("_affinity", affinity)
                .Set("_storyText", story)
                .Set("_profileText", profile)
                .SetArray("_swatches", swatches)
                .Set("_statsTitle", statsTitle)
                .Set("_statsText", stats)
                .Apply();
            return view;
        }

        // ───────────── 형태 정보 ─────────────

        static UIPopup BuildFormInfoPopup(SceneBuildContext ctx, LineageSynergyRowView synergyRowPrefab)
        {
            var popup = BuildPopup<FormInfoPopup>(ctx.PopupRoot, "FormInfoPopup", "형태 정보", new Vector2(940, 1720), out var body, out var title);

            // 머리 (고정): 초상화 + 이름 / 단계 / 성향·역할 칩 / 특성 칩 / 성향 안내 / 전투력
            var frame = InsetPanel(body, "PortraitFrame").rectTransform.At(TopLeft, Vector2.zero, new Vector2(330, 400));
            var glow = Img(frame, "Glow", new Color(1f, 1f, 1f, 0.6f), _glow);
            glow.rectTransform.At(Center, new Vector2(0, 20), new Vector2(460, 460));
            Pulse(glow, 0.3f, 0.7f, 2.2f, 0.04f);
            var portrait = Portrait(frame, "Portrait", new Vector2(318, 388), PortraitBackdrop.Night, false, new Vector4(0, 0, 16, 0));
            Rt(portrait).At(Center, Vector2.zero, new Vector2(318, 388));
            CardFrameOverlay(frame, 1.4f);

            var name = Txt(body, "NameText", "형태", 42, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            name.rectTransform.At(TopLeft, new Vector2(354, 0), new Vector2(514, 62));
            AutoSize(name, 28, 42);
            var tier = Txt(body, "TierText", "기본형", 26, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            tier.rectTransform.At(TopLeft, new Vector2(356, -62), new Vector2(512, 40));
            var roleRow = ChipRow(body, "RoleChips", 8, TextAnchor.MiddleLeft);
            roleRow.At(TopLeft, new Vector2(354, -110), new Vector2(514, 40));
            var styleChip = Chip(roleRow, "StyleChip", 40, 20, true, UITheme.SoftBlue, UITheme.Ink, "방어");
            var roleChip = Chip(roleRow, "RoleChip", 40, 20, true, UITheme.Guardian, UITheme.Ink, "지킴이 추천");
            var traitRow = ChipRow(body, "TraitChips", 8, TextAnchor.MiddleLeft);
            traitRow.At(TopLeft, new Vector2(354, -158), new Vector2(514, 40));
            var traitChips = new Object[2];
            for (int i = 0; i < traitChips.Length; i++) traitChips[i] = Chip(traitRow, $"TraitChip{i}", 40, 20, true, UITheme.Cream, UITheme.Ink, "특성");
            var styleHint = Txt(body, "StyleHintText", string.Empty, 22, UITheme.Cream, TextAlignmentOptions.TopLeft, false, true);
            styleHint.rectTransform.At(TopLeft, new Vector2(356, -212), new Vector2(512, 122));
            styleHint.lineSpacing = 8;
            AutoSize(styleHint, 17, 22);
            var power = Txt(body, "PowerText", "전투력 0", 34, UITheme.Gold, TextAlignmentOptions.MidlineLeft);
            power.rectTransform.At(TopLeft, new Vector2(354, -342), new Vector2(514, 56));

            // 본문 (스크롤): 고유 스킬 / 시너지 / 이야기 / 능력치
            var content = ScrollList(NewRect("Scroll", body).Stretch(0, 0, 416, 128), out var scroll);
            VList(content, 14, new RectOffset(0, 0, 0, 24));
            var skillCard = SkillCard(content, "SkillCard");

            var synergyCard = ListCard(content, "SynergyCard", out var synergyTitle, out _);
            var synergyEmpty = Txt(synergyCard, "EmptyLabel", "함께 켜지는 시너지가 아직 없어요.", 22, UITheme.InkSub, TextAlignmentOptions.Center);
            Pref(synergyEmpty, 64);

            var descCard = ParchmentCard(content, "DescCard").rectTransform;
            Pref(descCard, 220);
            CardTitle(descCard, "이야기");
            var desc = Txt(descCard, "DescText", string.Empty, 24, UITheme.Ink, TextAlignmentOptions.TopLeft, false, true);
            desc.rectTransform.Stretch(30, 30, 64, 18);
            desc.lineSpacing = 6;
            AutoSize(desc, 18, 24);

            var statsCard = ParchmentCard(content, "StatsCard").rectTransform;
            Pref(statsCard, 320);
            StatColumn(statsCard, "Common", "기본 능력치", UITheme.ParchmentDark, 28, out var common);
            StatColumn(statsCard, "Guardian", "지킴이로 배치하면", UITheme.Guardian, 300, out var guardian);
            StatColumn(statsCard, "Echo", "잔향이로 소환하면", UITheme.EchoGlow, 572, out var echo);

            // 이동 버튼은 보라 (M26)
            var formation = ToneButton(body, "FormationButton", "편성하러 가기", Tone.Primary, 34, out _);
            ((RectTransform)formation.transform).Bottom(0, 112);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_portrait", portrait)
                .Set("_glow", glow)
                .Set("_nameText", name)
                .Set("_tierText", tier)
                .Set("_styleChip", styleChip)
                .Set("_roleChip", roleChip)
                .SetArray("_traitChips", traitChips)
                .Set("_styleHintText", styleHint)
                .Set("_powerText", power)
                .Set("_scroll", scroll)
                .Set("_skillCard", skillCard)
                .Set("_synergyTitle", synergyTitle)
                .Set("_synergyContent", synergyCard)
                .Set("_synergyPrefab", synergyRowPrefab)
                .Set("_synergyEmpty", synergyEmpty.gameObject)
                .Set("_descText", desc)
                .Set("_commonStatsText", common)
                .Set("_guardianStatsText", guardian)
                .Set("_echoStatsText", echo)
                .Set("_formationButton", formation)
                .Apply();
            return popup;
        }

        static void StatColumn(RectTransform card, string name, string title, Color chipColor, float x, out TextMeshProUGUI text)
        {
            var chip = Pill(card, $"{name}Title", title, chipColor, UITheme.Ink, 20, out _);
            chip.rectTransform.At(TopLeft, new Vector2(x, -24), new Vector2(250, 38));
            text = Txt(card, $"{name}Stats", string.Empty, 22, UITheme.Ink, TextAlignmentOptions.TopLeft, false, true);
            text.rectTransform.At(TopLeft, new Vector2(x + 4, -80), new Vector2(256, 224));
            text.lineSpacing = 18;
        }

        // ───────────── 진화 레시피 (와이어프레임 02) ─────────────

        /// <summary>
        /// 진화 레시피 (창 1000x1720): 요약 0–196 / 스크롤 212~ (① 필요 재료 → ② 진화 조건 → ③ 획득처 → 진화하면 얻는 스킬, 절 제목 앞 8px, M71) /
        /// 안내 126–196 / [재료 모으러 가기 446x112 보라] [진화 제작 시작 462x112 금 + 뒤 빛]. AutoShot은 Show("jjak_caravan")로 연다.
        /// </summary>
        static UIPopup BuildRecipePopup(SceneBuildContext ctx, RecipeMaterialView materialPrefab, RecipeConditionView conditionPrefab, RecipeSourceView sourcePrefab)
        {
            var popup = BuildPopup<EvolutionRecipePopup>(ctx.PopupRoot, "EvolutionRecipePopup", "진화 레시피", new Vector2(1000, 1720), out var body, out var title);

            // 요약: 부모 → 진화형
            var summary = InsetPanel(body, "Summary").rectTransform.Top(0, 196);
            var from = Portrait(summary, "FromPortrait", new Vector2(124, 124), PortraitBackdrop.Night, true, new Vector4(-14, -14, -6, -20));
            Rt(from).At(TopLeft, new Vector2(24, -18), new Vector2(124, 124));
            RingFrame(Rt(from), UITheme.Lavender);
            // 부모 이름 캡션 22 (M29 · S §1.3 하한): 긴 이름은 20까지 줄이고 그래도 길면 말줄임
            var fromName = Txt(summary, "FromName", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center).Ellipsis();
            fromName.rectTransform.At(TopLeft, new Vector2(8, -154), new Vector2(156, 32));
            AutoSize(fromName, UIType.Hint, UIType.Caption);
            Chevrons(summary, "Arrow", 34, 30).At(TopLeft, new Vector2(160, -52), new Vector2(84, 64));
            // 진화형 초상화 중심 (329, -89)에 빛 · 돌아가는 빛 고리를 맞춘다 (고리는 가운데 피벗으로 돌아야 흔들리지 않는다)
            var toCenter = new Vector2(329, -89);
            var toGlow = Img(summary, "ToGlow", new Color(UITheme.Lavender.r, UITheme.Lavender.g, UITheme.Lavender.b, 0.7f), _glow);
            Centered(toGlow.rectTransform, TopLeft, toCenter, new Vector2(206, 206));
            Pulse(toGlow, 0.35f, 0.85f, 1.8f, 0.05f);
            // vfx_reveal_light의 빛 띠는 반지름 0.45~0.7이라 초상화(146) 바깥으로 빛이 번지도록 1.6배로 깐다
            var toRing = Img(summary, "ToRing", Color.white, _revealLight);
            toRing.preserveAspect = true;
            Centered(toRing.rectTransform, TopLeft, toCenter, new Vector2(234, 234));
            toRing.gameObject.AddComponent<SlowSpin>();
            var to = Portrait(summary, "ToPortrait", new Vector2(146, 146), PortraitBackdrop.Night, true, new Vector4(-16, -16, -6, -24));
            Rt(to).At(TopLeft, new Vector2(256, -16), new Vector2(146, 146));
            RingFrame(Rt(to), UITheme.GoldAccent);
            var toName = Txt(summary, "ToName", string.Empty, 38, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            toName.rectTransform.At(TopLeft, new Vector2(430, -16), new Vector2(482, 52));
            AutoSize(toName, 26, 38);
            var toTags = Txt(summary, "ToTags", string.Empty, 22, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
            toTags.rectTransform.At(TopLeft, new Vector2(432, -70), new Vector2(480, 34));
            Txt(summary, "Certain", "진화 결과는 랜덤이 아닌 확정 제작이에요", 20, UITheme.Lavender, TextAlignmentOptions.MidlineLeft)
                .rectTransform.At(TopLeft, new Vector2(432, -106), new Vector2(480, 30));
            ShardIcon(summary, 30).At(TopLeft, new Vector2(434, -148), new Vector2(30, 30));
            var shardsText = Txt(summary, "ShardsText", string.Empty, 22, UITheme.Cream, TextAlignmentOptions.MidlineLeft);
            shardsText.rectTransform.At(TopLeft, new Vector2(474, -146), new Vector2(436, 34));

            // 본문 (스크롤, M71): ① 필요 재료 → ② 진화 조건 → ③ 획득처 → 진화하면 얻는 스킬 (맨 끝, 원래 크기). 절 제목 앞마다 8px 틈
            var content = ScrollList(NewRect("Scroll", body).Stretch(0, 0, 212, 206), out var scroll);
            VList(content, 12, new RectOffset(0, 0, 6, 24));
            SectionGap(content, "MaterialsGap");
            SectionTitle(content, "MaterialsTitle", "① 필요 재료");
            var materials = NewRect("Materials", content);
            Pref(materials, 340);
            HRow(materials, 16, TextAnchor.UpperCenter);
            SectionGap(content, "ConditionsGap");
            SectionTitle(content, "ConditionsTitle", "② 진화 조건");
            var conditions = NewRect("Conditions", content);
            VList(conditions, 10, new RectOffset());
            var conditionEmpty = Txt(content, "ConditionEmpty", "추가 조건 없이 재료만 모으면 돼요.", 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            Pref(conditionEmpty, 56);
            SectionGap(content, "SourcesGap");
            SectionTitle(content, "SourcesTitle", "③ 획득처");
            var sources = NewRect("Sources", content);
            VList(sources, 10, new RectOffset());
            var sourceEmpty = Txt(content, "SourceEmpty", "획득처 정보가 아직 없어요.", 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            Pref(sourceEmpty, 56);
            SectionGap(content, "SkillGap");
            var skillTitle = SectionTitle(content, "SkillTitle", "진화하면 얻는 스킬");
            var skillCard = SkillCard(content, "SkillCard");

            // 하단: [재료 모으러 가기 (보라, '{재료} 사러 가기 · {목적지}' 한 줄 30 → 22)] [진화 제작 시작 (금 — 바로 제작, M75)]
            var hint = Txt(body, "CraftHint", string.Empty, 22, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            hint.rectTransform.Bottom(126, 70);
            var collect = ToneButton(body, "CollectButton", "재료 모으러 가기", Tone.Primary, UIType.ButtonL, out var collectLabel);
            ((RectTransform)collect.transform).At(BottomLeft, Vector2.zero, new Vector2(446, 112));
            collectLabel.rectTransform.Stretch(16, 16, 0, 0);
            collectLabel.Ellipsis();
            AutoSize(collectLabel, UIType.Caption, UIType.ButtonL);
            // 진화 제작 버튼 뒤 금빛 (버튼 462x112의 1.3배 부드러운 빛, 버튼과 같은 중심)
            var craftGlow = Img(body, "CraftGlow", new Color(UITheme.GoldAccent.r, UITheme.GoldAccent.g, UITheme.GoldAccent.b, 0.7f), _glow);
            craftGlow.rectTransform.At(BottomRight, new Vector2(69, -17), new Vector2(600, 146));
            Pulse(craftGlow, 0.15f, 0.75f, 1.3f, 0f);
            var craft = ToneButton(body, "CraftButton", "진화 제작 시작", Tone.Gold, 34, out var craftLabel);
            ((RectTransform)craft.transform).At(BottomRight, Vector2.zero, new Vector2(462, 112));
            AutoSize(craftLabel, UIType.ButtonM, 34);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_fromPortrait", from)
                .Set("_fromNameText", fromName)
                .Set("_toPortrait", to)
                .Set("_toNameText", toName)
                .Set("_toTagsText", toTags)
                .Set("_shardsText", shardsText)
                .Set("_scroll", scroll)
                .Set("_skillTitle", skillTitle)
                .Set("_skillCard", skillCard)
                .Set("_materialContent", materials)
                .Set("_materialPrefab", materialPrefab)
                .Set("_conditionContent", conditions)
                .Set("_conditionPrefab", conditionPrefab)
                .Set("_conditionEmpty", conditionEmpty.gameObject)
                .Set("_sourceContent", sources)
                .Set("_sourcePrefab", sourcePrefab)
                .Set("_sourceEmpty", sourceEmpty.gameObject)
                .Set("_collectButton", collect)
                .Set("_collectLabel", collectLabel)
                .Set("_craftButton", craft)
                .Set("_craftHintText", hint)
                .Set("_craftGlow", craftGlow.gameObject)
                .Apply();
            return popup;
        }

        // ───────────── 진화 제작 (와이어프레임 03) ─────────────

        static UIPopup BuildCraftPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<EvolutionCraftPopup>(ctx.PopupRoot, "EvolutionCraftPopup", "진화 제작", new Vector2(1000, 1280), out var body, out _);
            var window = (RectTransform)body.parent;

            // Before → After
            CraftCard(body, "BeforeCard", "Before", TopLeft, out var beforePortrait, out var beforeName, out var beforeRoleChip, out var beforeRole);
            var after = CraftCard(body, "AfterCard", "After", TopRight, out var afterPortrait, out var afterName, out var afterRoleChip, out var afterRole);
            var afterGlow = Img(after, "Glow", new Color(1f, 1f, 1f, 0.6f), _glow);
            afterGlow.rectTransform.At(TopCenter, new Vector2(0, 14), new Vector2(330, 330));
            afterGlow.transform.SetSiblingIndex(1);
            Pulse(afterGlow, 0.3f, 0.8f, 1.6f, 0.05f);
            var afterBurst = Burst(after, "Burst", 200f, 44f);
            Rt(afterBurst).At(Center, new Vector2(0, 34), new Vector2(10, 10));

            var arrow = NewRect("Arrow", body).At(TopCenter, new Vector2(0, -150), new Vector2(240, 130));
            Chevrons(arrow, "Chevrons", 54, 46).Top(0, 80);
            Txt(arrow, "Caption", "확정 제작", 22, UITheme.Lavender, TextAlignmentOptions.Center, true).rectTransform.Bottom(0, 38);

            // 소모 재료
            Txt(body, "MaterialsHeader", "소모 재료", 28, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(462, 44, 4, 4);
            var materials = NewRect("Materials", body).Top(512, 214);
            HRow(materials, 16, TextAnchor.MiddleCenter);

            // 제작 진행
            Txt(body, "ProgressHeader", "제작 진행", 28, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(744, 44, 4, 4);
            var percent = Txt(body, "ProgressText", "0%", 30, UITheme.Gold, TextAlignmentOptions.MidlineRight, true);
            percent.rectTransform.Top(744, 44, 4, 4);
            var bar = UiArt.Bar(body, "ProgressBar", UITheme.Lamp, out var fill, 40).rectTransform.Top(796, 40);
            var head = NewRect("Head", bar).At(MiddleLeft, Vector2.zero, new Vector2(60, 60));
            head.pivot = new Vector2(0.5f, 0.5f);
            var headGlow = Img(head, "Glow", UITheme.Lamp, _glow);
            headGlow.rectTransform.At(Center, Vector2.zero, new Vector2(130, 130));
            Pulse(headGlow, 0.5f, 1f, 0.6f, 0.08f);
            Img(head, "Spark", Color.white, _sparkle).rectTransform.At(Center, Vector2.zero, new Vector2(64, 64)).gameObject.AddComponent<SlowSpin>();
            var sparks = Burst(head, "Sparks", 80f, 26f);
            Rt(sparks).At(Center, Vector2.zero, new Vector2(10, 10));

            var craft = ToneButton(body, "CraftButton", "진화 제작", Tone.Gold, 36, out var craftLabel);
            ((RectTransform)craft.transform).At(TopCenter, new Vector2(0, -870), new Vector2(560, 120));

            // 공유 안내: 호감도 아이콘 + 문구 / 안내 단추(Icons/info)
            var footer = InsetPanel(body, "Footer").rectTransform.Bottom(0, 84);
            var footerLine = NewRect("Line", footer).Stretch(80, 80, 0, 0);
            var footerRow = HRow(footerLine, 12, TextAnchor.MiddleCenter);
            footerRow.childControlWidth = true;
            footerRow.childControlHeight = true;
            var footerHeart = ArtLib.Icon("affinity");
            if (footerHeart != null)
            {
                var heartIcon = Img(footerLine, "Heart", Color.white, footerHeart);
                heartIcon.preserveAspect = true;
                var heartLayout = heartIcon.gameObject.AddComponent<LayoutElement>();
                heartLayout.preferredWidth = heartLayout.preferredHeight = 40;
            }
            Txt(footerLine, "Text", footerHeart != null ? "레벨 / 호감도 공유" : "♥  레벨 / 호감도 공유", 26, UITheme.Cream,
                TextAlignmentOptions.Center, true);
            var info = InfoButton(footer);
            ((RectTransform)info.transform).At(MiddleRight, new Vector2(-14, 0), new Vector2(56, 56));
            // 누르는 자리 88 (M22): 사방 16 — 위는 제작 버튼과 34 간격 안, 옆은 안내 글(누름 없음) 쪽
            info.targetGraphic.HitPad(new Vector2(56, 56), UISize.MinHit, UISize.MinHit);
            // 안내 말풍선: 본문 위를 덮는 겹층 (축하 화면처럼 창의 자식 — 열리면 제작 버튼을 통째로 가리고, 누르면 닫힌다).
            // 본문 형제가 아니므로 제작 버튼과 누르는 자리를 나눠 갖지 않는다. 자리는 예전 본문 기준 Bottom(96, 170, 30, 30) 그대로
            var tooltipImage = UiArt.Panel(window, "Tooltip", UiArt.PanelNight9s, 2f, null, new Color(0.09f, 0.07f, 0.2f, 0.97f), true);
            tooltipImage.rectTransform.Bottom(body.offsetMin.y + 96, 170, body.offsetMin.x + 30, -body.offsetMax.x + 30);
            var tooltip = tooltipImage.gameObject.AddComponent<Button>();
            tooltip.transition = Selectable.Transition.None;
            tooltip.navigation = new Navigation { mode = Navigation.Mode.None };
            Txt(tooltipImage.transform, "Text",
                    "진화형은 계보의 레벨 · 호감도를 그대로 나눠 써요.\n다시 키울 필요 없이 전투 역할만 새로 골라 편성하세요.",
                    24, UITheme.Cream, TextAlignmentOptions.Center, false, true)
                .rectTransform.Stretch(28, 28, 14, 14);
            tooltipImage.gameObject.SetActive(false);

            // 해금 완료 축하 화면 (창 전체를 덮는다)
            var celebration = NewRect("Celebration", window).Stretch();
            var celebrationGroup = celebration.gameObject.AddComponent<CanvasGroup>();
            Img(celebration, "Dim", new Color(0.04f, 0.02f, 0.1f, 0.88f), RoundSprite, true).rectTransform.Stretch();
            var ambient = Burst(celebration, "Ambient", 0f, 40f);
            Rt(ambient).Stretch(20, 20, 20, 20);
            var card = ParchmentCard(celebration, "Card").rectTransform.At(Center, new Vector2(0, 20), new Vector2(840, 1100));
            Txt(card, "Title", "해금 완료", 56, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Top(44, 76);
            foreach (float side in new[] { -1f, 1f })
            {
                var spark = Img(card, side < 0 ? "SparkLeft" : "SparkRight", Color.white, _gloss);
                spark.preserveAspect = true;
                spark.rectTransform.At(TopCenter, new Vector2(side * 196, -36), new Vector2(88, 88));
                Pulse(spark, 0.5f, 1f, 1.1f, 0.15f);
            }
            var celebrationGlow = Img(card, "Glow", new Color(UITheme.Lavender.r, UITheme.Lavender.g, UITheme.Lavender.b, 0.75f), _glow);
            celebrationGlow.rectTransform.At(TopCenter, new Vector2(0, -110), new Vector2(580, 580));
            Pulse(celebrationGlow, 0.45f, 0.9f, 2f, 0.05f);
            // 돌아가는 빛 고리: 초상화 중심(0, -400)에 가운데 피벗으로 (위 가운데 피벗이면 고리가 원을 그리며 흔들린다)
            var ring = Img(card, "Ring", new Color(1f, 1f, 1f, 0.9f), _revealLight);
            ring.preserveAspect = true;
            Centered(ring.rectTransform, TopCenter, new Vector2(0, -400), new Vector2(500, 500));
            ring.gameObject.AddComponent<SlowSpin>();
            var celebrationPortrait = Portrait(card, "Portrait", new Vector2(400, 400), PortraitBackdrop.None, false, Vector4.zero);
            Rt(celebrationPortrait).At(TopCenter, new Vector2(0, -200), new Vector2(400, 400));
            var burst = Burst(card, "Burst", 400f, 64f);
            Rt(burst).At(TopCenter, new Vector2(0, -395), new Vector2(10, 10));
            var celebrationText = Txt(card, "Text", string.Empty, 36, UITheme.Ink, TextAlignmentOptions.Center, true, true);
            celebrationText.rectTransform.Top(630, 104, 40, 40);
            var celebrationSub = Txt(card, "SubText", string.Empty, 24, UITheme.InkSub, TextAlignmentOptions.Center, false, true);
            celebrationSub.rectTransform.Top(736, 188, 44, 44);
            celebrationSub.lineSpacing = 8;
            AutoSize(celebrationSub, 18, 24);
            var buttons = NewRect("Buttons", card).Bottom(44, 116, 50, 50);
            AddRow(buttons, 20);
            // 짝지은 확인은 보조(왼쪽), 이동은 보라 '편성하러 가기 ›' (M26 — 금은 진화 제작 하나)
            var confirm = ToneButton(buttons, "ConfirmButton", "확인", Tone.Secondary, 32, out _);
            var formation = ToneButton(buttons, "FormationButton", "편성하러 가기 ›", Tone.Primary, 32, out _);
            celebration.SetInactive();

            new Binder(popup)
                .Set("_beforePortrait", beforePortrait)
                .Set("_beforeNameText", beforeName)
                .Set("_beforeRoleChip", beforeRoleChip)
                .Set("_beforeRoleText", beforeRole)
                .Set("_afterPortrait", afterPortrait)
                .Set("_afterNameText", afterName)
                .Set("_afterRoleChip", afterRoleChip)
                .Set("_afterRoleText", afterRole)
                .Set("_afterGlow", afterGlow)
                .Set("_afterBurst", afterBurst)
                .Set("_materialContent", materials)
                .Set("_materialPrefab", ctx.ItemSlotPrefab)
                .Set("_progressFill", fill)
                .Set("_progressHead", head)
                .Set("_progressText", percent)
                .Set("_progressSparks", sparks)
                .Set("_craftButton", craft)
                .Set("_craftLabel", craftLabel)
                .Set("_infoButton", info)
                .Set("_tooltip", tooltip)
                .Set("_celebration", celebrationGroup)
                .Set("_celebrationCard", card)
                .Set("_celebrationPortrait", celebrationPortrait)
                .Set("_celebrationText", celebrationText)
                .Set("_celebrationSubText", celebrationSub)
                .Set("_celebrationBurst", burst)
                .Set("_celebrationAmbient", ambient)
                .Set("_confirmButton", confirm)
                .Set("_formationButton", formation)
                .Apply();
            return popup;
        }

        /// <summary>진화 제작 Before / After 카드</summary>
        static RectTransform CraftCard(RectTransform body, string name, string caption, Vector2 anchor,
            out FriendPortrait portrait, out TextMeshProUGUI nameText, out Image roleChip, out TextMeshProUGUI roleText)
        {
            var card = ParchmentCard(body, name).rectTransform.At(anchor, Vector2.zero, new Vector2(330, 440));
            Txt(card, "Caption", caption, 24, UITheme.InkSub, TextAlignmentOptions.Center, true).rectTransform.Top(16, 34);
            portrait = Portrait(card, "Portrait", new Vector2(260, 260), PortraitBackdrop.Light, false, new Vector4(0, 0, 10, 0));
            Rt(portrait).At(TopCenter, new Vector2(0, -56), new Vector2(260, 260));
            nameText = Txt(card, "NameText", string.Empty, 30, UITheme.Ink, TextAlignmentOptions.Center, true).Ellipsis();
            nameText.rectTransform.Top(322, 46, 14, 14);
            AutoSize(nameText, 20, 30);
            roleChip = Pill(card, "RoleChip", "지킴이", UITheme.Guardian, UITheme.Ink, 22, out roleText);
            roleChip.rectTransform.At(TopCenter, new Vector2(0, -376), new Vector2(130, 40));
            return card;
        }

        // ───────────── 의상 도감 보기 ─────────────

        static UIPopup BuildIllustrationPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<IllustrationPopup>(ctx.PopupRoot, "IllustrationPopup", "도감", new Vector2(1040, 1780), out var body, out var title);

            var frame = ParchmentCard(body, "Frame").rectTransform.Stretch(0, 0, 0, 64);
            var image = Img(frame, "Image", Color.white);
            image.preserveAspect = true;
            image.rectTransform.Stretch(18, 18, 18, 18);
            var empty = Txt(frame, "EmptyLabel", "아직 일러스트가 준비되지 않았어요.", 28, UITheme.InkSub, TextAlignmentOptions.Center, false, true);
            empty.rectTransform.Stretch(40, 40, 0, 0);
            Txt(body, "Caption", "아무 곳이나 탭하면 닫혀요", 24, UITheme.SubText).rectTransform.Bottom(0, 50);

            var tapImage = Img(body, "TapArea", Color.clear, null, true);
            tapImage.rectTransform.Stretch();
            var tap = tapImage.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.navigation = new Navigation { mode = Navigation.Mode.None };

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_image", image)
                .Set("_emptyLabel", empty.gameObject)
                .Set("_tapArea", tap)
                .Apply();
            return popup;
        }

        // ───────────── 소환 결과 (와이어프레임 05) ─────────────

        /// <summary>
        /// 소환 결과 (창 1000x1556, 본문 928x1384 — 결과가 5장 이하면 런타임이 한 줄(270)만큼 접는다): 대표 결과 0–300 / 카드 316–856 (위에 공개 무대 RevealStage —
        /// 꾸러미 300 · 빛 · 초상화 280 · 별가루 · ★4 이상을 크게 보여 줄 때 카드를 가리는 어둠, CR-04) / 요약 · 건너뛰기 안내 862–912 / 기억편 사용처 926–1152 /
        /// 작은 줄 SubActions 1168–1256 ([진화 기억도 보기 ›] · [편성하러 가기 ›] 같은 보조 알약 300x88 둘) / 아래 줄 1272–1384 ([확인] 보조 · [N회 더 소환] 금, 452x112 둘).
        /// 묶음 사이는 16씩 — 빈 띠가 남지 않게 창 높이를 내용에 맞췄다.
        /// 창 왼쪽 위 별조각 잔액 칩 (M47). 건너뛰기 판(SkipArea)은 공개 중에만 켜지는 본문 전체 투명 판 (맨 뒤 형제 — 맨 위에 그려진다),
        /// [건너뛰기 ›](SkipChip, 본문 오른쪽 위 — 대표 결과 자리라 공개 중에는 비어 있다)는 0.5초부터. 꾸러미 그림 _bundleSprites = UiArt.GachaBundle(묶임 · 빛남 · 열림).
        /// 대표 결과 글: 제목 / 별 + NEW / 한 줄 안내 / '전투력 N' (아이콘 24 + Label 24 굵게 Cream) / 따라잡기 (Caption 22 Lavender) / 특성 칩 · 고유 스킬.
        /// FtueDirector가 찾는 Window 자식과 _treeButton 연결은 그대로.
        /// </summary>
        static UIPopup BuildGachaResultPopup(SceneBuildContext ctx, GachaCardView cardPrefab)
        {
            var popup = BuildPopup<GachaResultPopup>(ctx.PopupRoot, "GachaResultPopup", "소환 결과", new Vector2(1000, 1556), out var body, out var title);
            var window = (RectTransform)body.parent;
            var tables = ctx.Content != null ? ctx.Content.GachaTables : null;
            var currency = tables != null && tables.Count > 0 && tables[0] != null ? tables[0].costCurrency : CurrencyType.Gem;
            PopupBalanceChip(window, currency);
            foreach (float side in new[] { -1f, 1f })
            {
                var spark = Img(title.transform.parent, side < 0 ? "TitleSparkLeft" : "TitleSparkRight", Color.white, _gloss);
                spark.preserveAspect = true;
                spark.rectTransform.At(TopCenter, new Vector2(side * 156, -22), new Vector2(64, 64));
                Pulse(spark, 0.4f, 1f, 1.2f, 0.15f);
            }

            // 대표 결과
            var highlight = InsetPanel(body, "Highlight").rectTransform.Top(0, 300);
            var highlightGroup = highlight.gameObject.AddComponent<CanvasGroup>();
            var highlightGlow = Img(highlight, "Glow", new Color(1f, 1f, 1f, 0.6f), _glow);
            highlightGlow.rectTransform.At(MiddleLeft, new Vector2(-30, 0), new Vector2(380, 380));
            Pulse(highlightGlow, 0.35f, 0.8f, 1.8f, 0.05f);
            var highlightPortrait = Portrait(highlight, "Portrait", new Vector2(260, 280), PortraitBackdrop.None, false, Vector4.zero);
            Rt(highlightPortrait).At(MiddleLeft, new Vector2(30, 0), new Vector2(260, 280));
            var highlightBurst = Burst(highlight, "Burst", 220f, 48f);
            Rt(highlightBurst).At(MiddleLeft, new Vector2(155, 0), new Vector2(10, 10));
            var highlightTitle = Txt(highlight, "TitleText", string.Empty, 44, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            highlightTitle.rectTransform.At(TopLeft, new Vector2(316, -26), new Vector2(590, 62));
            AutoSize(highlightTitle, 28, 44);
            var highlightStars = Txt(highlight, "StarsText", string.Empty, 34, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            highlightStars.rectTransform.At(TopLeft, new Vector2(318, -92), new Vector2(400, 48));
            var newTag = Pill(highlight, "NewTag", "새로운 계보 획득!", UITheme.GoldAccent, UITheme.Ink, 20, out _);
            newTag.rectTransform.At(TopLeft, new Vector2(670, -98), new Vector2(220, 38));
            var highlightMessage = Txt(highlight, "MessageText", string.Empty, 25, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            highlightMessage.rectTransform.At(TopLeft, new Vector2(318, -146), new Vector2(590, 40));
            highlightMessage.lineSpacing = 6;
            AutoSize(highlightMessage, 19, 25);
            // 새 친구 전투력 (PRG-01 §7.1 #3): 아이콘 24 + '전투력 N' Label 24 굵게 Cream
            var highlightPower = NewRect("PowerRow", highlight).At(TopLeft, new Vector2(318, -188), new Vector2(400, 32));
            UiArt.Icon(highlightPower, "PowerIcon", UiArt.PowerIcon, MiddleLeft, Vector2.zero, 24);
            var highlightPowerText = Txt(highlightPower, "PowerText", "전투력 0", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            highlightPowerText.rectTransform.Stretch(32, 0, 0, 0);
            highlightPower.gameObject.SetActive(false);
            // 따라잡기 (PRG-05 §6.5): '친구들 평균의 80%인 Lv.n부터 함께해요' Caption 22 Lavender
            var highlightCatchUp = Txt(highlight, "CatchUpText", string.Empty, UIType.Caption, UITheme.Lavender, TextAlignmentOptions.MidlineLeft).Ellipsis();
            highlightCatchUp.rectTransform.At(TopLeft, new Vector2(318, -220), new Vector2(590, 30));
            AutoSize(highlightCatchUp, UIType.Hint, UIType.Caption);
            highlightCatchUp.gameObject.SetActive(false);
            var highlightTraits = ChipRow(highlight, "Traits", 8, TextAnchor.MiddleLeft);
            highlightTraits.At(TopLeft, new Vector2(318, -254), new Vector2(590, 40));
            var highlightTraitChips = new Object[2];
            for (int i = 0; i < highlightTraitChips.Length; i++)
                highlightTraitChips[i] = Chip(highlightTraits, $"TraitChip{i}", 36, 19, true, UITheme.Cream, UITheme.Ink, "특성");
            var highlightSkill = Txt(highlightTraits, "SkillText", string.Empty, 22, UITheme.Cream, TextAlignmentOptions.MidlineLeft);

            // 카드
            var cards = NewRect("Cards", body).Top(316, 540);
            AddGrid(cards, new Vector2(170, 250), new Vector2(14, 20), 5, new RectOffset(0, 0, 8, 0));
            var stage = BuildRevealStage(body, out var stageGroup, out var stageDim, out var bundle, out var bundleImage, out var bundleGlow,
                out var revealPortrait, out var revealBurst);
            var skipHint = Txt(body, "SkipHint", "화면을 탭하면 바로 확인해요", UIType.Caption, UITheme.SubText);
            skipHint.rectTransform.Top(866, 40);
            var summary = Txt(body, "SummaryText", string.Empty, 28, UITheme.Cream, TextAlignmentOptions.Center, true);
            summary.rectTransform.Top(862, 50);

            // 기억편 사용처
            var shardInfo = InsetPanel(body, "ShardInfo").rectTransform.Top(926, 226);
            ShardIcon(shardInfo, 32).At(TopLeft, new Vector2(22, -18), new Vector2(32, 32));
            Txt(shardInfo, "Header", "기억편 사용처", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.At(TopLeft, new Vector2(66, -12), new Vector2(400, 44));
            var tiles = NewRect("Tiles", shardInfo).Stretch(20, 20, 66, 20);
            AddRow(tiles, 14);
            ShardUseTile(tiles, "Resonance", "계보 공명", "공명 레벨 상승", ArtLib.Icon("resonance"));
            ShardUseTile(tiles, "Substitute", "희귀 재료 대체", "재료 부족 보완", ArtLib.Item("mat_moonlight"));
            ShardUseTile(tiles, "Accelerate", "진화 가속", "조건 기간 단축", ArtLib.Icon("speed"));

            // 작은 줄 (M16): 왼쪽 '진화 기억도 보기 ›' (대표 결과가 있으면), 오른쪽 '편성하러 가기 ›' (빈 칸 + 편성 안 된 친구가 있을 때만 — 꺼 둠).
            // 둘 다 같은 보조 알약 300x88 (보이는 크기 = 누르는 자리 88, M22) — 금은 아래 줄 다시 소환 하나. 기억편 사용처 아래 16, 아래 줄 위 16
            var subActions = NewRect("SubActions", body).Bottom(128, UISize.MinHit);
            var tree = ToneButton(subActions, "TreeButton", "진화 기억도 보기 ›", Tone.Secondary, 26, out _);
            ((RectTransform)tree.transform).At(MiddleLeft, Vector2.zero, new Vector2(300, UISize.MinHit));
            var formation = ToneButton(subActions, "FormationButton", "편성하러 가기 ›", Tone.Secondary, 26, out _);
            ((RectTransform)formation.transform).At(MiddleRight, Vector2.zero, new Vector2(300, UISize.MinHit));
            formation.gameObject.SetActive(false);

            // 아래 줄 (M26 · M47): [확인] 보조 왼쪽, [N회 더 소환] 금 + 별조각 비용 (비용 버튼 세 상태) 오른쪽 — 452x112 둘, 사이 20
            var row = NewRect("Buttons", body).Bottom(0, 112);
            AddRow(row, 20).padding = new RectOffset(2, 2, 0, 0);
            var confirm = ToneButton(row, "ConfirmButton", "확인", Tone.Secondary, 34, out _);
            var retry = ToneButton(row, "RetryButton", "다시 소환", Tone.Gold, 28, out var retryLabel);
            retryLabel.rectTransform.Top(8, 44, 8, 8);
            var retryCost = CostLine(retry.transform, currency, 28, 24, 54);
            retryCost.color = UITheme.Ink;
            var retryView = CostBtn(retry, retryCost, CostSurface.Gold, retryLabel);

            // 건너뛰기 판: 공개 중에만 켜지는 본문 전체 투명 판 (맨 뒤 형제라 카드 · 버튼 위에서 누름을 받는다, M49)
            var skipImage = Img(body, "SkipArea", Color.clear, null, true);
            skipImage.rectTransform.Stretch();
            var skip = skipImage.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.navigation = new Navigation { mode = Navigation.Mode.None };
            skipImage.gameObject.SetActive(false);

            // 중복 → 기억편 녹는 조각 (꺼 둔 본보기 — 런타임이 복제해 요약 줄로 날린다, 누름 안 받음)
            var melt = ShardIcon(body, 44);
            melt.name = "MeltShard";
            melt.anchorMin = melt.anchorMax = Center;
            melt.pivot = Center;
            melt.gameObject.SetActive(false);

            // [건너뛰기 ›] (CR-04): 본문 오른쪽 위 220x64 (대표 결과 자리 — 공개 중에는 비어 있다), 0.5초부터. 누르는 자리 88 (M22). 건너뛰기 판보다 뒤 형제
            var skipChip = SceneParts.Button(body, "SkipChip", "건너뛰기", ButtonStyle.Ghost, UIType.Label, out var skipChipLabel);
            ((RectTransform)skipChip.transform).At(TopRight, Vector2.zero, new Vector2(220, 64));
            skipChipLabel.rectTransform.Stretch(8, 8, 0, 0);
            skipChipLabel.alignment = TextAlignmentOptions.MidlineRight;
            skipChip.targetGraphic.HitPad(new Vector2(220, 64), UISize.MinHit, UISize.MinHit);
            skipChip.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_cardContent", cards)
                .Set("_cardPrefab", cardPrefab)
                .Set("_skipButton", skip)
                .Set("_skipHint", skipHint)
                .Set("_highlight", highlightGroup)
                .Set("_highlightPortrait", highlightPortrait)
                .Set("_highlightGlow", highlightGlow)
                .Set("_highlightTitle", highlightTitle)
                .Set("_highlightStars", highlightStars)
                .Set("_highlightMessage", highlightMessage)
                .Set("_highlightNewTag", newTag.gameObject)
                .SetArray("_highlightTraitChips", highlightTraitChips)
                .Set("_highlightSkillText", highlightSkill)
                .Set("_highlightBurst", highlightBurst)
                .Set("_summaryText", summary)
                .Set("_shardInfo", shardInfo.gameObject)
                .Set("_confirmButton", confirm)
                .Set("_retryButton", retry)
                .Set("_retryLabel", retryLabel)
                .Set("_retryCostText", retryCost)
                .Set("_treeButton", tree)
                .Set("_retryView", retryView)
                .Set("_formationButton", formation)
                .Set("_highlightPower", highlightPower.gameObject)
                .Set("_highlightPowerText", highlightPowerText)
                .Set("_highlightCatchUp", highlightCatchUp)
                .Set("_revealStage", stage)
                .Set("_revealGroup", stageGroup)
                .Set("_revealDim", stageDim)
                .Set("_bundle", bundle)
                .Set("_bundleImage", bundleImage)
                .Set("_bundleGlow", bundleGlow)
                .SetArray("_bundleSprites", UiArt.GachaBundle(UiArt.BundleState.Closed), UiArt.GachaBundle(UiArt.BundleState.Glow),
                    UiArt.GachaBundle(UiArt.BundleState.Open))
                .Set("_revealPortrait", revealPortrait)
                .Set("_revealBurst", revealBurst)
                .Set("_skipChip", skipChip)
                .Set("_meltTemplate", melt)
                .Apply();
            return popup;
        }

        /// <summary>기억편 사용처 칸 (ui_panel_inset_9s, 높이 140): 위 아이콘 56px / 이름 / 설명. 아이콘이 없으면 글자만 가운데로.</summary>
        static void ShardUseTile(RectTransform parent, string name, string title, string desc, Sprite icon)
        {
            var tile = InsetPanel(parent, name).rectTransform;
            bool hasIcon = icon != null;
            if (hasIcon) UiArt.Icon(tile, "Icon", icon, TopCenter, new Vector2(0, -8), 56);
            Txt(tile, "Title", title, 24, UITheme.Cream, TextAlignmentOptions.Center, true).rectTransform.Top(hasIcon ? 64 : 26, 34, 6, 6);
            Txt(tile, "Desc", desc, 20, UITheme.Lavender, TextAlignmentOptions.Center).rectTransform.Top(hasIcon ? 98 : 70, 28, 6, 6);
        }

        // ═════════════════════ 프리팹 ═════════════════════

        static CharacterCardView BuildCharacterCardPrefab()
        {
            var bg = InsetPanel(null, "CharacterCard", true);
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(240, 330);
            var button = AddButton(bg);
            bg.gameObject.AddComponent<PressScale>();
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            var view = bg.gameObject.AddComponent<CharacterCardView>();

            var glow = Img(root, "GradeGlow", Color.white, _glow);
            glow.rectTransform.At(TopCenter, new Vector2(0, 0), new Vector2(250, 250));
            var portrait = Portrait(root, "Portrait", new Vector2(224, 232), PortraitBackdrop.Night, false, new Vector4(4, 4, 12, 0));
            Rt(portrait).At(TopCenter, new Vector2(0, -8), new Vector2(224, 232));
            Img(root, "Shade", NightShade, _fadeUp).rectTransform.Bottom(0, 150);
            var line = Img(root, "GradeLine", UITheme.GradeColor(ItemGrade.Epic), RoundSprite);
            line.rectTransform.Bottom(94, 4, 30, 30);
            var name = Txt(root, "NameText", "이름", 28, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis();
            name.rectTransform.Bottom(46, 44, 8, 8);
            AutoSize(name, 18, 28);
            // 아래 줄 (M45): 왼쪽 'Lv.1' 24 굵게 Cream (좌우 14) + 오른쪽 '진화 1/11' 20 Ink · Lavender 알약 92x28 (오른쪽 12).
            // 미보유는 같은 자리에 종족 이름 (가운데, 알약 없음 — 런타임)
            var info = Txt(root, "InfoText", "Lv.1", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            info.rectTransform.Bottom(12, 34, 14, 14);
            var evolutionPill = Pill(root, "EvolutionPill", "진화 1/1", UITheme.Lavender, UITheme.Ink, UIType.Hint, out var evolutionText);
            evolutionPill.rectTransform.At(BottomRight, new Vector2(-12, 15), new Vector2(92, 28));
            evolutionText.rectTransform.Stretch(4, 4, 0, 0);
            AutoSize(evolutionText, UIType.AutoSizeFloor, UIType.Hint);
            var starsBg = UiArt.Panel(root, "StarsBg", UiArt.PillNight9s, UiArt.CapsulePpu(34), new Color(1f, 1f, 1f, 0.92f), new Color(0.06f, 0.04f, 0.13f, 0.65f));
            starsBg.rectTransform.At(TopLeft, new Vector2(10, -10), new Vector2(124, 34));
            var stars = Txt(starsBg.transform, "StarsText", "★★★", 22, UITheme.Gold, TextAlignmentOptions.Center, true);
            stars.rectTransform.Stretch(4, 4, 0, 0);
            var traits = NewRect("Traits", root).At(TopLeft, new Vector2(10, -50), new Vector2(150, 68));
            var traitLayout = traits.gameObject.AddComponent<VerticalLayoutGroup>();
            traitLayout.spacing = 6;
            traitLayout.childAlignment = TextAnchor.UpperLeft;
            traitLayout.childControlWidth = true;
            traitLayout.childControlHeight = true;
            traitLayout.childForceExpandWidth = false;
            traitLayout.childForceExpandHeight = false;
            var traitChips = new Object[2];
            for (int i = 0; i < traitChips.Length; i++) traitChips[i] = Chip(traits, $"TraitChip{i}", 30, UIType.Micro, true, NightShade, UITheme.Cream, "특성");
            CardFrameOverlay(root, 1.5f);
            // 미보유 알약: 특성 칩 둘 (카드 위 50~118) 아래로 내려 두 번째 칩 글자를 가리지 않게 — 카드 위 168~210 (초상화 8~240 안, 등급 선 232 위)
            var lockedChip = Pill(root, "LockedChip", "미보유", new Color(0.06f, 0.04f, 0.13f, 0.82f), UITheme.SubText, 22, out _);
            lockedChip.rectTransform.At(Center, new Vector2(0, -24), new Vector2(120, 42));
            // 전투력 (PRG-01 §7.1 #4, 보유만): 등급 선 위 왼쪽 — 칼 아이콘 20 (M45 카드와 같은 그림) + 숫자 20 굵게 Cream (밤 카드, 외곽선)
            var powerRow = NewRect("PowerRow", root).At(BottomLeft, new Vector2(14, 100), new Vector2(150, 26));
            UiArt.Icon(powerRow, "PowerIcon", UiArt.PowerIcon, MiddleLeft, Vector2.zero, 20);
            var powerText = UiArt.Outline(Txt(powerRow, "PowerText", "0", UIType.Hint, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true));
            powerText.rectTransform.Stretch(26, 0, 0, 0);
            AutoSize(powerText, UIType.AutoSizeFloor, UIType.Hint);
            powerRow.gameObject.SetActive(false);
            // 호감도 Lv5 하트 바느질 틀 (CR-10): 전용 아트가 없으면 틴트용 칸 틀(SlotFrame9s)을 DustyPink로 (HasAffinityFrame9s)
            var affinityFrame = UiArt.Panel(root, "AffinityFrame", UiArt.AffinityFrame9s, 2f,
                UiArt.HasAffinityFrame9s ? Color.white : UITheme.WithAlpha(UITheme.DustyPink, 0.9f), UITheme.WithAlpha(UITheme.DustyPink, 0.6f));
            affinityFrame.rectTransform.Stretch(-2, -2, -2, -2);
            affinityFrame.gameObject.SetActive(false);
            var dot = Dot(root, new Vector2(-8, -8), 30);

            new Binder(view)
                .Set("_button", button)
                .Set("_group", group)
                .Set("_portrait", portrait)
                .Set("_gradeGlow", glow)
                .Set("_gradeLine", line)
                .Set("_nameText", name)
                .Set("_starsText", stars)
                .Set("_infoText", info)
                .Set("_evolutionPill", evolutionPill.gameObject)
                .Set("_evolutionText", evolutionText)
                .Set("_lockedChip", lockedChip.gameObject)
                .Set("_alertDot", dot)
                .SetArray("_traitChips", traitChips)
                .Set("_powerRow", powerRow.gameObject)
                .Set("_powerText", powerText)
                .Set("_affinityFrame", affinityFrame.gameObject)
                .Apply();
            return SaveFitted<CharacterCardView>(bg.gameObject, $"{PrefabDir}/CharacterCard.prefab");
        }

        /// <summary>진화 기억도 노드 (168 x 180): 원형 초상화 + 이름 + 상태 칩</summary>
        static EvolutionNodeView BuildNodePrefab()
        {
            var hit = Img(null, "EvolutionNode", Color.clear, null, true);
            var root = hit.rectTransform;
            root.sizeDelta = new Vector2(168, 180);
            var button = AddButton(hit);
            button.transition = Selectable.Transition.None;
            hit.gameObject.AddComponent<PressScale>();
            var group = hit.gameObject.AddComponent<CanvasGroup>();
            var view = hit.gameObject.AddComponent<EvolutionNodeView>();

            var body = NewRect("Body", root).Stretch();
            var focus = Img(body, "FocusRing", Color.white, _ring);
            focus.rectTransform.At(TopCenter, new Vector2(0, 22), new Vector2(168, 168));
            Pulse(focus, 0.55f, 1f, 1.2f, 0.04f);
            focus.gameObject.SetActive(false);
            var glow = Img(body, "Glow", UITheme.Lavender, _glow);
            glow.rectTransform.At(TopCenter, new Vector2(0, 36), new Vector2(200, 200));
            Pulse(glow, 0.35f, 0.95f, 1.6f, 0.06f);
            var portrait = Portrait(body, "Portrait", new Vector2(110, 110), PortraitBackdrop.Light, true, new Vector4(-14, -14, -6, -22));
            Rt(portrait).At(TopCenter, new Vector2(0, -9), new Vector2(110, 110));
            // 상태 고리 (ui_ring_stitch, 흰색 틴트용): 받침 테두리를 덮도록 초상화 위에 얹고, 뷰가 해금 · 단서 · 미발견 색을 입힌다
            var ring = RingFrame(Rt(portrait), UITheme.GoldAccent);
            var lockGlyph = LockGlyph(body, 46, new Color(UITheme.Cream.r, UITheme.Cream.g, UITheme.Cream.b, 0.9f));
            lockGlyph.At(TopCenter, new Vector2(0, -40), new Vector2(46, 46));

            // 역할 아이콘(Icons/guardian · echo)을 46px로 그대로 그린다. 역할 색 받침(원)은 아트가 없을 때만 뷰가 켠다.
            var guardianArt = ArtLib.Icon("guardian");
            var roleBadge = Img(body, "RoleBadge", UITheme.Guardian, CircleSprite);
            roleBadge.rectTransform.At(TopCenter, new Vector2(-50, 4), new Vector2(46, 46));
            roleBadge.enabled = guardianArt == null;
            var roleIcon = Img(roleBadge.transform, "Icon", Color.white, guardianArt);
            roleIcon.preserveAspect = true;
            roleIcon.rectTransform.Stretch();
            roleIcon.enabled = guardianArt != null;
            var roleLetter = Txt(roleBadge.transform, "Letter", "지", 21, UITheme.Ink, TextAlignmentOptions.Center, true);
            roleLetter.rectTransform.Stretch();
            roleLetter.gameObject.SetActive(guardianArt == null);
            var dot = UiArt.Badge(body, "AlertDot", TopCenter, new Vector2(52, 2), 28);

            var name = Txt(body, "NameText", "형태", 22, UITheme.Ink, TextAlignmentOptions.Center, true).Ellipsis();
            name.rectTransform.At(TopCenter, new Vector2(0, -124), new Vector2(170, 30));
            AutoSize(name, UIType.AutoSizeFloor, 22);
            var chip = Pill(body, "StateChip", "해금", UITheme.PrimaryButton, Color.white, 18, out var stateText);
            chip.rectTransform.At(TopCenter, new Vector2(0, -152), new Vector2(112, 28));
            var burst = Burst(body, "Burst", 150f, 36f);
            Rt(burst).At(TopCenter, new Vector2(0, -59), new Vector2(10, 10));

            new Binder(view)
                .Set("_button", button)
                .Set("_group", group)
                .Set("_body", body)
                .Set("_glow", glow)
                .Set("_ring", ring)
                .Set("_focusRing", focus)
                .Set("_portrait", portrait)
                .Set("_lock", lockGlyph.gameObject)
                .Set("_nameText", name)
                .Set("_stateChip", chip)
                .Set("_stateText", stateText)
                .Set("_roleBadge", roleBadge)
                .Set("_roleIcon", roleIcon)
                .Set("_roleLetter", roleLetter)
                .Set("_guardianIcon", ArtLib.Icon("guardian"))
                .Set("_echoIcon", ArtLib.Icon("echo"))
                .Set("_alertDot", dot.gameObject)
                .Set("_burst", burst)
                .Apply();
            return SaveFitted<EvolutionNodeView>(hit.gameObject, $"{PrefabDir}/EvolutionNode.prefab");
        }

        /// <summary>의상 한 줄 (928 x 216, 크림 카드)</summary>
        static CostumeItemView BuildCostumeItemPrefab()
        {
            var card = ParchmentCard(null, "CostumeItem");
            var root = card.rectTransform;
            root.sizeDelta = new Vector2(928, 216);
            Pref(card, 216);
            var view = card.gameObject.AddComponent<CostumeItemView>();

            // 장착 중 강조: 카드 전체에 보라 · 금 카드 틀(card_frame_9s)을 덧씌운다 (뷰가 켜고 끈다)
            var equippedFrame = UiArt.Panel(root, "EquippedWash", UiArt.CardFrame9s, 1.6f, null,
                new Color(UITheme.GoldAccent.r, UITheme.GoldAccent.g, UITheme.GoldAccent.b, 0.22f));
            equippedFrame.rectTransform.Stretch(-2, -2, -2, -2);
            var portraitFrame = NewRect("PortraitFrame", root).At(MiddleLeft, new Vector2(18, 0), new Vector2(176, 184));
            var portrait = Portrait(portraitFrame, "Portrait", new Vector2(168, 176), PortraitBackdrop.Light, false, new Vector4(0, 0, 6, 0));
            Rt(portrait).At(Center, Vector2.zero, new Vector2(168, 176));
            CardFrameOverlay(portraitFrame, 2.2f);
            var lockGlyph = LockGlyph(portraitFrame, 40, new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.8f));
            lockGlyph.At(BottomRight, new Vector2(-8, 8), new Vector2(40, 40));
            var equippedTag = Pill(portraitFrame, "EquippedTag", "장착 중", UITheme.GoldAccent, UITheme.Ink, 18, out _);
            equippedTag.rectTransform.At(TopLeft, new Vector2(-6, 8), new Vector2(96, 30));
            var burst = Burst(portraitFrame, "Burst", 170f, 36f);
            Rt(burst).At(Center, Vector2.zero, new Vector2(10, 10));

            var name = Txt(root, "NameText", "의상", 30, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            name.rectTransform.At(TopLeft, new Vector2(214, -16), new Vector2(470, 44));
            AutoSize(name, 22, 30);
            var emotion = Txt(root, "EmotionText", "외로움 → 위로", 24, CharacterUI.InkPink, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            emotion.rectTransform.At(TopLeft, new Vector2(214, -60), new Vector2(470, 34));
            // 얻는 법 (양피지 캡션 22 InkSub, 두 줄이 넘치면 18까지 — M29 · M23)
            var acquisition = Txt(root, "AcquisitionText", string.Empty, UIType.Caption, UITheme.InkSub, TextAlignmentOptions.TopLeft, false, true);
            acquisition.rectTransform.At(TopLeft, new Vector2(214, -96), new Vector2(470, 62));
            acquisition.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(acquisition, UIType.Micro, UIType.Caption);
            var progress = Txt(root, "ProgressText", string.Empty, 21, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            progress.rectTransform.At(BottomLeft, new Vector2(214, 12), new Vector2(480, 36));

            var action = ToneButton(root, "ActionButton", "장착", Tone.Primary, 28, out var actionLabel);
            ((RectTransform)action.transform).At(TopRight, new Vector2(-18, -22), new Vector2(196, 84));
            var dot = Dot(action.transform, new Vector2(8, 8));
            var gallery = ToneButton(root, "GalleryButton", "도감 보기", Tone.Soft, 24, out _);
            ((RectTransform)gallery.transform).At(BottomRight, new Vector2(-18, 18), new Vector2(196, 66));

            new Binder(view)
                .Set("_equippedFrame", equippedFrame)
                .Set("_portrait", portrait)
                .Set("_nameText", name)
                .Set("_emotionText", emotion)
                .Set("_acquisitionText", acquisition)
                .Set("_progressText", progress)
                .Set("_actionButton", action)
                .Set("_actionLabel", actionLabel)
                .Set("_galleryButton", gallery)
                .Set("_equippedTag", equippedTag.gameObject)
                .Set("_lockIcon", lockGlyph.gameObject)
                .Set("_alertDot", dot)
                .Set("_burst", burst)
                .Set("_primarySprite", ArtOrNull(UiArt.ButtonPrimary9s))
                .Set("_goldSprite", ArtOrNull(UiArt.ButtonGold9s))
                .Set("_disabledSprite", ArtOrNull(UiArt.ButtonDisabled9s))
                .Apply();
            return SaveFitted<CostumeItemView>(card.gameObject, $"{PrefabDir}/CostumeItem.prefab");
        }

        /// <summary>
        /// 진화 레시피 재료 카드 (220 x 330, 크림 카드). 위에서부터 아이콘(30~140) / 이름(148~184) / 보유·필요(184~224),
        /// 아래 "기억편 대체" 버튼(240~316)은 숫자와 겹치지 않는 별도 칸이다. 대체 버튼이 없으면 같은 칸에 '획득처 ›' 글자 링크 (M71).
        /// </summary>
        static RecipeMaterialView BuildRecipeMaterialPrefab()
        {
            var card = ParchmentCard(null, "RecipeMaterial");
            var root = card.rectTransform;
            root.sizeDelta = new Vector2(220, 330);
            var view = card.gameObject.AddComponent<RecipeMaterialView>();

            var gradeLine = Img(root, "GradeLine", UITheme.GradeColor(ItemGrade.Rare), RoundSprite);
            gradeLine.rectTransform.Top(14, 6, 44, 44);
            var iconBackground = Plate(root, "IconBackground");
            iconBackground.rectTransform.At(TopCenter, new Vector2(0, -30), new Vector2(110, 110));
            var icon = Img(iconBackground.transform, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.Stretch(14, 14, 14, 14);
            var letter = Txt(iconBackground.transform, "Letter", "?", 44, UITheme.Ink, TextAlignmentOptions.Center, true);
            letter.rectTransform.Stretch();
            var check = CheckMark(root, 40);
            check.rectTransform.At(TopCenter, new Vector2(50, -26), new Vector2(40, 40));

            var name = Txt(root, "NameText", "재료", 24, UITheme.Ink, TextAlignmentOptions.Center, true).Ellipsis();
            name.rectTransform.At(TopCenter, new Vector2(0, -148), new Vector2(204, 36));
            AutoSize(name, 16, 24);
            var count = Txt(root, "CountText", "0 / 0", 28, UITheme.Ink, TextAlignmentOptions.Center, true);
            count.rectTransform.At(TopCenter, new Vector2(0, -184), new Vector2(204, 40));
            AutoSize(count, 20, 28);

            var substitute = ToneButton(root, "SubstituteButton", "기억편 대체", Tone.Primary, 20, out var substituteLabel);
            ((RectTransform)substitute.transform).At(BottomCenter, new Vector2(0, 14), new Vector2(196, 76));
            substituteLabel.rectTransform.Top(6, 32, 6, 6);
            var substituteCost = Txt(substitute.transform, "CostText", "기억편 0", 18, UITheme.Cream, TextAlignmentOptions.Center);
            substituteCost.rectTransform.Top(38, 30, 6, 6);
            // 대체 버튼이 없을 때 그 자리의 '획득처 ›' 글자 링크 (M71 — 20px InkSub(양피지 위), 누르면 레시피가 그 재료의 획득처 줄로 스크롤, 꺼 둠)
            var sourceLink = SceneParts.Button(root, "SourceLink", "획득처 ›", ButtonStyle.Ghost, UIType.Hint, out var sourceLinkLabel);
            ((RectTransform)sourceLink.transform).At(BottomCenter, new Vector2(0, 14), new Vector2(196, 76));
            sourceLinkLabel.rectTransform.Stretch();
            sourceLinkLabel.color = UITheme.InkSub;
            // 누르는 자리 88 (S §1.5 MinHit): 위아래 6씩 — 위 숫자 칸(224)까지 16, 아래 카드 끝까지 14라 넘치지 않는다
            sourceLink.targetGraphic.HitPad(new Vector2(196, 76), 0f, UISize.MinHit, maxB: 7f, maxT: 7f);
            sourceLink.gameObject.SetActive(false);

            new Binder(view)
                .Set("_iconBackground", iconBackground)
                .Set("_icon", icon)
                .Set("_iconLetter", letter)
                .Set("_gradeLine", gradeLine)
                .Set("_nameText", name)
                .Set("_countText", count)
                .Set("_check", check.gameObject)
                .Set("_substituteButton", substitute)
                .Set("_substituteCostText", substituteCost)
                .Set("_sourceLink", sourceLink)
                .Apply();
            return SaveFitted<RecipeMaterialView>(card.gameObject, $"{PrefabDir}/RecipeMaterial.prefab");
        }

        /// <summary>
        /// 진화 조건 한 줄 (가로 늘림 x 84): 체크 고리 / [조건 글 바로 뒤 '›' = 투명 버튼 GoButton 70~520] / 진행 막대 / 진행도 / 가속 (M71).
        /// '›'는 글 끝에 붙어 따라간다 (GoButton 가로 줄 — 글은 제 폭만큼, 길면 줄어들어 …, '›'는 24 고정). 줄 가운데 홀로 뜨지 않게.
        /// </summary>
        static RecipeConditionView BuildRecipeConditionPrefab()
        {
            var bg = InsetPanel(null, "RecipeCondition");
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(928, 84);
            Pref(bg, 84);
            var view = bg.gameObject.AddComponent<RecipeConditionView>();

            var circle = CheckMark(root, 46);
            circle.rectTransform.At(MiddleLeft, new Vector2(18, 0), new Vector2(46, 46));
            var mark = circle.transform.GetChild(0).gameObject;
            // 조건 글 자리 = 투명 버튼 (M71 — 누르면 그 조건을 채우는 곳으로, 다 채우면 런타임이 끈다) + 글 끝 22px '›'. 칠한 버튼은 '가속' 하나
            var goImage = Img(root, "GoButton", Color.clear, null, true);
            goImage.rectTransform.At(MiddleLeft, new Vector2(70, 0), new Vector2(450, 84));
            // 누르는 자리 88 (S §1.5 MinHit): 위아래 2씩 — 조건 줄 사이 간격 10의 절반 안
            goImage.HitPad(new Vector2(450, 84), 0f, UISize.MinHit, maxB: 2f, maxT: 2f);
            var go = AddButton(goImage);
            go.transition = Selectable.Transition.None;
            goImage.gameObject.AddComponent<PressScale>();
            // 글 + '›' 가로 줄 (위 8, 왼쪽 10 — 예전 글 자리 그대로): 폭만 줄이 정한다 (높이 40은 각자). 글 최소 폭 0이라 넘치면 글만 줄어 …으로 잘리고 '›'는 남는다
            var goRow = goImage.gameObject.AddComponent<HorizontalLayoutGroup>();
            goRow.padding = new RectOffset(10, 8, 8, 0);
            goRow.spacing = 6;
            goRow.childAlignment = TextAnchor.UpperLeft;
            goRow.childControlWidth = true;
            goRow.childControlHeight = false;
            goRow.childForceExpandWidth = false;
            goRow.childForceExpandHeight = false;
            var label = Txt(goImage.transform, "LabelText", "조건", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft).Ellipsis();
            label.rectTransform.At(TopLeft, new Vector2(10, -8), new Vector2(404, 40));
            var goChevron = Txt(goImage.transform, "Chevron", "›", UIType.Caption, UITheme.Lavender, TextAlignmentOptions.Center, true);
            goChevron.rectTransform.At(TopLeft, new Vector2(420, -8), new Vector2(24, 40));
            var chevronLayout = goChevron.gameObject.AddComponent<LayoutElement>();
            chevronLayout.minWidth = 24;
            chevronLayout.preferredWidth = 24;
            UiArt.Bar(root, "ProgressBar", UITheme.Lavender, out var fillRect, 14).rectTransform.At(BottomLeft, new Vector2(80, 12), new Vector2(420, 14));
            var fill = fillRect.GetComponent<Image>();
            var progress = Txt(root, "ProgressText", "0 / 0", 26, UITheme.Text, TextAlignmentOptions.MidlineRight, true);
            progress.rectTransform.At(MiddleRight, new Vector2(-226, 0), new Vector2(180, 44));
            var accelerate = ToneButton(root, "AccelerateButton", "가속", Tone.Primary, 22, out var accelerateLabel);
            ((RectTransform)accelerate.transform).At(MiddleRight, new Vector2(-12, 0), new Vector2(200, 68));
            accelerateLabel.rectTransform.Top(4, 30, 6, 6);
            var accelerateCost = Txt(accelerate.transform, "CostText", "기억편 0", 18, UITheme.Cream, TextAlignmentOptions.Center);
            accelerateCost.rectTransform.Top(34, 28, 6, 6);

            new Binder(view)
                .Set("_checkCircle", circle)
                .Set("_checkMark", mark)
                .Set("_labelText", label)
                .Set("_progressText", progress)
                .Set("_progressFill", fill.rectTransform)
                .Set("_progressFillImage", fill)
                .Set("_accelerateButton", accelerate)
                .Set("_accelerateCostText", accelerateCost)
                .Set("_goButton", go)
                .Set("_goChevron", goChevron.gameObject)
                .Apply();
            return SaveFitted<RecipeConditionView>(bg.gameObject, $"{PrefabDir}/RecipeCondition.prefab");
        }

        /// <summary>
        /// 획득처 한 줄 (가로 늘림 x 80): 아이콘(+ 잠김 자물쇠) / 재료 / '부족' / 획득처(+ 잠김 한 줄 'n-n 클리어 시') / [가기 › | 잠김] (M17 · M39 · M76).
        /// 줄 전체 CanvasGroup — 잠긴 줄은 런타임이 α0.55로 흐리게 한다.
        /// </summary>
        static RecipeSourceView BuildRecipeSourcePrefab()
        {
            var bg = InsetPanel(null, "RecipeSource");
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(928, 80);
            Pref(bg, 80);
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            var view = bg.gameObject.AddComponent<RecipeSourceView>();

            var iconBackground = Plate(root, "IconBackground");
            iconBackground.rectTransform.At(MiddleLeft, new Vector2(16, 0), new Vector2(56, 56));
            var icon = Img(iconBackground.transform, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.Stretch(7, 7, 7, 7);
            var letter = Txt(iconBackground.transform, "Letter", "?", 24, UITheme.Ink, TextAlignmentOptions.Center, true);
            letter.rectTransform.Stretch();
            // 잠긴 줄: 아이콘 오른쪽 아래 28px 자물쇠 (M17 · M39, 꺼 둠 — 런타임이 켠다)
            var lockGlyph = LockGlyph(iconBackground.transform, 28, new Color(UITheme.Cream.r, UITheme.Cream.g, UITheme.Cream.b, 0.95f));
            lockGlyph.At(BottomRight, new Vector2(6, -6), new Vector2(28, 28));
            lockGlyph.gameObject.SetActive(false);
            var material = Txt(root, "MaterialText", "재료", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            material.rectTransform.At(MiddleLeft, new Vector2(86, 0), new Vector2(228, 40));
            // '부족' 꼬리표: NegativeInk 바탕 + Cream 글자 (M29)
            var needTag = Pill(root, "NeedTag", "부족", UITheme.NegativeInk, UITheme.Cream, 18, out _);
            needTag.rectTransform.At(MiddleLeft, new Vector2(320, 0), new Vector2(70, 30));
            var source = Txt(root, "SourceText", "획득처", 25, UITheme.Lavender, TextAlignmentOptions.MidlineRight).Ellipsis();
            source.rectTransform.At(MiddleRight, new Vector2(-186, 0), new Vector2(330, 40));
            // 잠긴 줄의 열리는 조건 한 줄 (Hint 20 SubText, 'n-n 클리어 시' — 런타임이 획득처 이름을 13px 올리고 켠다)
            var unlock = Txt(root, "UnlockText", string.Empty, UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineRight).Ellipsis();
            unlock.rectTransform.At(MiddleRight, new Vector2(-186, -16), new Vector2(330, 28));
            unlock.gameObject.SetActive(false);
            // 갈 수 있음 = 보라 '가기 ›' (M26 · M76), 잠김 = 보조 '잠김' (그림만 바꿔 끼운다 — 누를 수 있고 이유 토스트)
            var go = ToneButton(root, "GoButton", "가기 ›", Tone.Primary, 24, out var goLabel);
            ((RectTransform)go.transform).At(MiddleRight, new Vector2(-12, 0), new Vector2(160, 60));

            new Binder(view)
                .Set("_iconBackground", iconBackground)
                .Set("_icon", icon)
                .Set("_iconLetter", letter)
                .Set("_materialText", material)
                .Set("_sourceText", source)
                .Set("_needTag", needTag.gameObject)
                .Set("_goButton", go)
                .Set("_group", group)
                .Set("_lock", lockGlyph.gameObject)
                .Set("_unlockText", unlock)
                .Set("_goLabel", goLabel)
                .Set("_primarySprite", ArtOrNull(UiArt.ButtonPrimary9s))
                .Set("_secondarySprite", ArtOrNull(UiArt.ButtonSecondary9s))
                .Apply();
            return SaveFitted<RecipeSourceView>(bg.gameObject, $"{PrefabDir}/RecipeSource.prefab");
        }

        /// <summary>
        /// 시너지 한 줄 (가로 늘림 x 112, 크림 카드 안): 아이콘 / 종류 칩 + 이름 / 단계·조합 설명 / 발동 상태 + 편성 진행.
        /// 누르면 시너지 도감으로 이동한다.
        /// </summary>
        static LineageSynergyRowView BuildSynergyRowPrefab()
        {
            var bg = UiArt.Panel(null, "LineageSynergyRow", UiArt.Tab9s, 2f, RowWash, RowWash, true);
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(876, 112);
            Pref(bg, 112);
            var button = AddButton(bg);
            bg.gameObject.AddComponent<PressScale>();
            var view = bg.gameObject.AddComponent<LineageSynergyRowView>();

            var iconBackground = Plate(root, "IconBackground");
            iconBackground.rectTransform.At(MiddleLeft, new Vector2(16, 0), new Vector2(78, 78));
            var icon = Img(iconBackground.transform, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.Stretch(9, 9, 9, 9);
            var letter = Txt(iconBackground.transform, "Letter", "?", 32, UITheme.Ink, TextAlignmentOptions.Center, true);
            letter.rectTransform.Stretch();

            var head = ChipRow(root, "Head", 10, TextAnchor.MiddleLeft);
            head.Top(12, 38, 110, 210);
            var kind = Chip(head, "KindChip", 30, UIType.Micro, false, UITheme.Lavender, UITheme.Ink, "특성");
            var name = Txt(head, "NameText", "시너지", 26, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true);
            // 단계 설명 (캡션 22 InkSub, 두 줄이 넘치면 16까지 줄어든다 — M29 · M23)
            var detail = Txt(root, "DetailText", string.Empty, UIType.Caption, UITheme.InkSub, TextAlignmentOptions.TopLeft, false, true);
            detail.rectTransform.Top(52, 56, 112, 210);
            detail.lineSpacing = 2;
            AutoSize(detail, UIType.AutoSizeFloor, UIType.Caption);

            var stateRow = ChipRow(root, "StateRow", 6, TextAnchor.MiddleRight);
            stateRow.At(TopRight, new Vector2(-46, -14), new Vector2(160, 32));
            var state = Chip(stateRow, "StateChip", 30, UIType.Micro, false, UITheme.GoldAccent, UITheme.Ink, "발동 중");
            var progress = Txt(root, "ProgressText", string.Empty, UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineRight, true);
            progress.rectTransform.At(TopRight, new Vector2(-46, -50), new Vector2(160, 34));
            AutoSize(progress, UIType.Micro, UIType.Caption);
            // 꺾쇠는 글자가 아닌 그림 → InkLine (M29)
            var chevronArt = UiArt.Chevron;
            if (chevronArt != null)
            {
                var chevron = Img(root, "Chevron", CharacterUI.WithAlpha(UITheme.InkLine, 0.7f), chevronArt);
                chevron.preserveAspect = true;
                chevron.rectTransform.At(MiddleRight, new Vector2(-6, 0), new Vector2(40, 30));
            }
            else
            {
                Txt(root, "Chevron", "›", 46, CharacterUI.WithAlpha(UITheme.InkLine, 0.7f), TextAlignmentOptions.Center, true)
                    .rectTransform.At(MiddleRight, new Vector2(-8, 3), new Vector2(34, 64));
            }

            new Binder(view)
                .Set("_button", button)
                .Set("_background", bg)
                .Set("_iconBackground", iconBackground)
                .Set("_icon", icon)
                .Set("_iconLetter", letter)
                .Set("_kindChip", kind)
                .Set("_nameText", name)
                .Set("_detailText", detail)
                .Set("_stateChip", state)
                .Set("_progressText", progress)
                .Set("_icons", _icons)
                .Apply();
            return SaveFitted<LineageSynergyRowView>(bg.gameObject, $"{PrefabDir}/LineageSynergyRow.prefab");
        }

        /// <summary>
        /// 형태별 스킬 한 줄 (가로 늘림 x 126, 크림 카드 안): 둥근 초상화 + 겹친 스킬 구슬 / 형태 이름·성향·단계 /
        /// 스킬 이름 + 발동 시점 / 설명 한 줄 / 해금 상태 칩.
        /// </summary>
        static FormSkillRowView BuildFormSkillRowPrefab()
        {
            var bg = UiArt.Panel(null, "FormSkillRow", UiArt.Tab9s, 2f, RowWash, RowWash, true);
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(876, 126);
            Pref(bg, 126);
            var button = AddButton(bg);
            bg.gameObject.AddComponent<PressScale>();
            var view = bg.gameObject.AddComponent<FormSkillRowView>();

            var portrait = Portrait(root, "Portrait", new Vector2(96, 96), PortraitBackdrop.Light, true, new Vector4(-12, -12, -4, -18));
            Rt(portrait).At(MiddleLeft, new Vector2(14, 4), new Vector2(96, 96));
            var orb = SkillOrb(root, "Orb", 50, false);
            Rt(orb).At(BottomLeft, new Vector2(80, 8), new Vector2(50, 50));

            // 캡션 22 InkSub (M29): 형태 이름 줄 · 설명 줄. 설명은 오른쪽 아래 'i' 표시 자리만큼 비운다
            var form = Txt(root, "FormText", "형태", UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineLeft).Ellipsis();
            form.rectTransform.Top(8, 34, 146, 194);
            var skill = Txt(root, "SkillText", "스킬", 27, UITheme.Ink, TextAlignmentOptions.MidlineLeft).Ellipsis();
            skill.rectTransform.Top(42, 42, 146, 22);
            var desc = Txt(root, "DescText", string.Empty, UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineLeft).Ellipsis();
            desc.rectTransform.Top(84, 34, 146, 52);
            var stateRow = ChipRow(root, "StateRow", 6, TextAnchor.MiddleRight);
            stateRow.At(TopRight, new Vector2(-16, -12), new Vector2(170, 30));
            var state = Chip(stateRow, "StateChip", 28, UIType.Micro, false, UITheme.PrimaryButton, Color.white, "해금");
            // 누르면 자세히 (M80): 안내 글 대신 오른쪽 아래 24px 'i' (Icons/info — 없으면 꺾쇠 글자)
            var infoArt = ArtLib.Icon("info");
            if (infoArt != null) UiArt.Icon(root, "InfoCue", infoArt, BottomRight, new Vector2(-16, 14), 24);
            else Txt(root, "InfoCue", "›", 30, UITheme.InkLine, TextAlignmentOptions.Center, true).rectTransform.At(BottomRight, new Vector2(-14, 10), new Vector2(24, 32));

            new Binder(view)
                .Set("_button", button)
                .Set("_background", bg)
                .Set("_portrait", portrait)
                .Set("_orb", orb)
                .Set("_formText", form)
                .Set("_skillText", skill)
                .Set("_descText", desc)
                .Set("_stateChip", state)
                .Apply();
            return SaveFitted<FormSkillRowView>(bg.gameObject, $"{PrefabDir}/FormSkillRow.prefab");
        }

        /// <summary>
        /// 소환 결과 카드 (170 x 250): 뒷면 / 앞면 / 등급 빛 / 반짝이. 뒷면은 분실물 꾸러미 그림(UiArt.GachaBundle 묶임 — 전용 아트가 없으면 카드 뒷면),
        /// 새 친구는 금 리본 '새 친구!' (RibbonGold9s 150×34, Ink 20 굵게 — CR-04).
        /// </summary>
        static GachaCardView BuildGachaCardPrefab()
        {
            var root = NewRect("GachaCard", null);
            root.sizeDelta = new Vector2(170, 250);
            var view = root.gameObject.AddComponent<GachaCardView>();

            var glow = Img(root, "Glow", Color.white, _glow);
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(300, 380));
            Pulse(glow, 0.35f, 0.85f, 1.6f, 0.04f);
            var flip = NewRect("Flip", root).Stretch();

            // 뒷면: 카드 뒷면 그림(gacha_card_back) + 달 둘레를 도는 옅은 실 고리 (★4 이상 예고 때 금빛으로 밝아진다)
            Image back;
            Image backGlow;
            var bundleArt = UiArt.GachaBundle(UiArt.BundleState.Closed);
            var backArt = UiArt.IsArt(bundleArt) ? bundleArt : ArtLib.UI("gacha_card_back");
            if (backArt != null)
            {
                back = Img(flip, "Back", Color.white, backArt);
                back.type = Image.Type.Simple;
                back.preserveAspect = true;
                back.rectTransform.Stretch();
                backGlow = Img(back.transform, "Ring", CharacterUI.WithAlpha(UITheme.Lavender, 0.45f), _ring);
                backGlow.preserveAspect = true;
                backGlow.rectTransform.At(Center, Vector2.zero, new Vector2(150, 150));
                backGlow.gameObject.AddComponent<SlowSpin>();
            }
            else
            {
                back = Img(flip, "Back", UITheme.PanelLight, RoundSprite);
                back.rectTransform.Stretch();
                Img(back.transform, "Inner", UITheme.Panel, RoundSprite).rectTransform.Stretch(8, 8, 8, 8);
                backGlow = Img(back.transform, "Ring", UITheme.Lavender, _ring);
                backGlow.rectTransform.At(Center, Vector2.zero, new Vector2(160, 160));
                backGlow.gameObject.AddComponent<SlowSpin>();
                var emblem = Img(back.transform, "Emblem", UITheme.Cream, _sparkle);
                emblem.rectTransform.At(Center, Vector2.zero, new Vector2(96, 96));
                Pulse(emblem, 0.5f, 1f, 1.4f, 0.08f);
                Txt(back.transform, "Label", "LOST & LOVELY", UIType.Micro, UITheme.SubText, TextAlignmentOptions.Center, true).rectTransform.Bottom(12, 26);
                CardFrameOverlay(back.rectTransform, 2.2f);
            }

            // 앞면: 등급 배경 그림(card_bg_grade1~5, 뷰가 등급마다 바꾼다) 위에 초상화 아트를 바로 얹는다
            var front = NewRect("Front", flip).Stretch();
            var gradeBacks = new Object[5];
            for (int i = 0; i < gradeBacks.Length; i++) gradeBacks[i] = ArtLib.UI($"card_bg_grade{i + 1}");
            var frontBg = Img(front, "Bg", UITheme.PanelLight, RoundSprite);
            frontBg.rectTransform.Stretch();
            Image gradeBack = null;
            var firstBack = gradeBacks[(int)ItemGrade.Epic] as Sprite;
            if (firstBack != null)
            {
                frontBg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                gradeBack = Img(frontBg.transform, "GradeArt", Color.white, firstBack);
                gradeBack.rectTransform.Stretch();
                Envelope(gradeBack, firstBack);
            }
            var portrait = Portrait(front, "Portrait", new Vector2(160, 172), PortraitBackdrop.None, false, new Vector4(2, 2, 6, 0));
            Rt(portrait).At(TopCenter, new Vector2(0, -5), new Vector2(160, 172));
            Img(front, "Shade", NightShade, _fadeUp).rectTransform.Bottom(0, 120);
            // 등급 선은 초상화 바로 아래 (초상화를 가로지르지 않게)
            var gradeLine = Img(front, "GradeLine", UITheme.GradeColor(ItemGrade.Epic), RoundSprite);
            gradeLine.rectTransform.Bottom(70, 3, 18, 18);
            var stars = Txt(front, "StarsText", UITheme.Stars(ItemGrade.Epic), 22, UITheme.Gold, TextAlignmentOptions.Center, true);
            stars.rectTransform.Bottom(76, 28);
            var name = Txt(front, "NameText", "이름", 24, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis();
            name.rectTransform.Bottom(36, 34, 6, 6);
            AutoSize(name, 16, 24);
            var newChip = UiArt.Panel(front, "NewChip", UiArt.RibbonGold9s, 2.4f, Color.white, UITheme.GoldAccent);
            newChip.rectTransform.At(BottomCenter, new Vector2(0, 2), new Vector2(150, 34));
            Txt(newChip.transform, "Label", "새 친구!", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch(8, 8, 0, 0);
            var shardChip = Pill(front, "ShardChip", "기억편 +0", UITheme.Lavender, UITheme.Ink, 18, out var shardText);
            shardChip.rectTransform.At(BottomCenter, new Vector2(0, 6), new Vector2(140, 28));
            CardFrameOverlay(front, 2.2f);
            // 리본 · 기억편 알약은 카드 틀(아래 장식 테) 위에 그린다 — 틀 뒤에 있으면 글자 아래쪽이 가린다
            newChip.transform.SetAsLastSibling();
            shardChip.transform.SetAsLastSibling();
            var newBadge = Pill(front, "NewBadge", "NEW", UITheme.RedDot, Color.white, 18, out _);
            newBadge.rectTransform.At(TopLeft, new Vector2(-6, 6), new Vector2(68, 30));
            var pityTag = Pill(front, "PityTag", "확정", UITheme.Gold, UITheme.Ink, 18, out _);
            pityTag.rectTransform.At(TopRight, new Vector2(6, 6), new Vector2(64, 30));
            var flash = Img(front, "Flash", new Color(1f, 1f, 1f, 0f), RoundSprite);
            flash.rectTransform.Stretch();
            front.SetInactive();

            var burst = Burst(root, "Burst", 190f, 40f);
            Rt(burst).At(Center, Vector2.zero, new Vector2(10, 10));

            new Binder(view)
                .Set("_flip", flip)
                .Set("_back", back.gameObject)
                .Set("_backGlow", backGlow)
                .Set("_front", front.gameObject)
                .Set("_gradeBack", gradeBack)
                .SetArray("_gradeBacks", gradeBacks)
                .Set("_glow", glow)
                .Set("_gradeLine", gradeLine)
                .Set("_portrait", portrait)
                .Set("_nameText", name)
                .Set("_starsText", stars)
                .Set("_newBadge", newBadge.gameObject)
                .Set("_newChip", newChip.gameObject)
                .Set("_shardChip", shardChip.gameObject)
                .Set("_shardText", shardText)
                .Set("_pityTag", pityTag.gameObject)
                .Set("_flash", flash)
                .Set("_burst", burst)
                .Apply();
            return SaveFitted<GachaCardView>(root.gameObject, $"{PrefabDir}/GachaCard.prefab");
        }

        // ───────────── 소환 공개 무대 (CR-04) ─────────────

        /// <summary>
        /// 소환 공개 무대 (카드 자리 Top(316, 540) 위, 꺼 둠 — GachaResultPopup이 켜고 맨 뒤 형제로 올린다, 5장 이하면 카드 자리와 함께 접힌다):
        /// 어둠(10회 중 ★4 이상을 크게 보여 줄 때 카드를 가린다) / 등급 빛 460 / 분실물 꾸러미 300 (UiArt.GachaBundle 묶임 — 아트가 없으면 카드 뒷면) /
        /// 초상화 280 / 별가루. 누름을 받지 않는다 (건너뛰기 판이 받는다).
        /// </summary>
        static RectTransform BuildRevealStage(RectTransform body, out CanvasGroup group, out Image dim, out RectTransform bundle, out Image bundleImage,
            out Image glow, out Image portrait, out SparkleBurst burst)
        {
            var stage = NewRect("RevealStage", body).Top(316, 540);
            group = stage.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            dim = Img(stage, "Dim", UITheme.WithAlpha(NightShade, 0f), RoundSprite);
            dim.rectTransform.Stretch(-8, -8, -8, -8);
            dim.gameObject.SetActive(false);
            glow = Img(stage, "Glow", UITheme.WithAlpha(Color.white, 0f), _glow);
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(460, 460));
            bundleImage = Img(stage, "Bundle", Color.white, UiArt.GachaBundle(UiArt.BundleState.Closed));
            bundleImage.type = Image.Type.Simple;
            bundleImage.preserveAspect = true;
            bundle = bundleImage.rectTransform;
            Centered(bundle, Center, Vector2.zero, new Vector2(300, 300));
            portrait = Img(stage, "Portrait", Color.white);
            portrait.preserveAspect = true;
            Centered(portrait.rectTransform, Center, Vector2.zero, new Vector2(280, 280));
            portrait.gameObject.SetActive(false);
            burst = Burst(stage, "Burst", 260f, 44f);
            Rt(burst).At(Center, Vector2.zero, new Vector2(10, 10));
            stage.gameObject.SetActive(false);
            return stage;
        }

        // ───────────── 소환 마일스톤 · 친구 선택권 (PRG-04) ─────────────

        /// <summary>미리 만드는 마일스톤 단계 줄 수 (콘텐츠 5 + 되풀이 2 + 앞 여유 1)</summary>
        const int MilestoneRows = 8;

        /// <summary>
        /// 소환 마일스톤 창 (BuildPopup 820×1000, 본문 748×828, SPEC4 §6.4): 머리 0–64 '누적 소환 n회' (Title 28 Cream) + [모두 받기] 보조 220×64 오른쪽 /
        /// 친구 선택권 줄 76–164 (선택권이 있을 때만: 밤 판 + '친구 선택권 k장' + [고르기 ›] 보라 200×72) / 단계 목록 176~ (스크롤, 줄 110 × 8:
        /// 보상 칩 · '{10}회 · 별조각 100' · 상태 '완료' / '{k}회 남음' 또는 [받기] 금 160×80) / 아래 안내 0–68 (Hint 20 SubText 두 줄, 소환 탭에서 옮긴 기억편 안내 포함).
        /// </summary>
        static UIPopup BuildGachaMilestonePopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<GachaMilestonePopup>(ctx.PopupRoot, "GachaMilestonePopup", "소환 마일스톤", new Vector2(820, 1000), out var body, out var title);
            AutoSize(title, UIType.H2, UIType.H1);
            var pulls = Txt(body, "PullsText", "누적 소환 0회", UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            pulls.rectTransform.Top(0, 64, 4, 240);
            AutoSize(pulls, UIType.Label, UIType.Title);
            var claimAll = ToneButton(body, "ClaimAllButton", "모두 받기", Tone.Secondary, UIType.ButtonS, out _);
            ((RectTransform)claimAll.transform).At(TopRight, Vector2.zero, new Vector2(220, 64));
            claimAll.targetGraphic.HitPad(new Vector2(220, 64), UISize.MinHit, UISize.MinHit, maxT: 12f, maxB: 12f);
            claimAll.gameObject.SetActive(false);

            var ticketRow = InsetPanel(body, "TicketRow").rectTransform.Top(76, 88);
            var ticketText = Txt(ticketRow, "Text", "친구 선택권 1장", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            ticketText.rectTransform.Stretch(24, 230, 0, 0);
            var ticketButton = ToneButton(ticketRow, "PickButton", "고르기 ›", Tone.Primary, UIType.ButtonS, out _);
            ((RectTransform)ticketButton.transform).At(MiddleRight, new Vector2(-8, 0), new Vector2(200, 72));
            ticketButton.targetGraphic.HitPad(new Vector2(200, 72), UISize.MinHit, UISize.MinHit, maxT: 8f, maxB: 8f);
            ticketRow.gameObject.SetActive(false);

            var list = ScrollList(NewRect("Steps", body).Stretch(0, 0, 176, 80), out _);
            VList(list, 8, new RectOffset(0, 0, 0, 12));
            var rows = new Object[MilestoneRows];
            var labels = new Object[MilestoneRows];
            var states = new Object[MilestoneRows];
            var claims = new Object[MilestoneRows];
            var chips = new Object[MilestoneRows];
            for (int i = 0; i < MilestoneRows; i++)
            {
                var row = InsetPanel(list, $"Step{i}").rectTransform;
                Pref(row, 110);
                var chip = RewardChip(row, "Reward", new Vector2(72, 84), 44, UIType.Hint);
                ((RectTransform)chip.transform).At(MiddleLeft, new Vector2(16, 0), new Vector2(72, 84));
                var label = Txt(row, "Label", "10회 · 별조각 100", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
                label.rectTransform.Stretch(104, 196, 0, 0);
                AutoSize(label, UIType.Hint, UIType.Label);
                var state = Txt(row, "State", "완료", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineRight);
                state.rectTransform.At(MiddleRight, new Vector2(-24, 0), new Vector2(170, 40));
                var claim = ToneButton(row, "ClaimButton", "받기", Tone.Gold, UIType.ButtonS, out _);
                ((RectTransform)claim.transform).At(MiddleRight, new Vector2(-14, 0), new Vector2(160, 80));
                claim.targetGraphic.HitPad(new Vector2(160, 80), UISize.MinHit, UISize.MinHit);
                claim.gameObject.SetActive(false);
                rows[i] = row.gameObject;
                labels[i] = label;
                states[i] = state;
                claims[i] = claim;
                chips[i] = chip;
            }
            var footer = Txt(body, "FooterText", GachaMilestonePopup.FooterHint, UIType.Hint, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            footer.rectTransform.Bottom(0, 68, 8, 8);
            AutoSize(footer, UIType.HintMin, UIType.Hint);

            new Binder(popup)
                .Set("_pullsText", pulls)
                .Set("_claimAllButton", claimAll)
                .Set("_ticketRow", ticketRow.gameObject)
                .Set("_ticketText", ticketText)
                .Set("_ticketButton", ticketButton)
                .SetArray("_rows", rows)
                .SetArray("_rowLabels", labels)
                .SetArray("_rowStates", states)
                .SetArray("_rowClaims", claims)
                .SetArray("_rowChips", chips)
                .Set("_footerText", footer)
                .Apply();
            return popup;
        }

        /// <summary>미리 만드는 친구 선택권 칸 수 (전설 이하 계보 — 지금 콘텐츠 6, 여유 2)</summary>
        const int FriendTicketCells = 8;

        /// <summary>
        /// 친구 선택권 (BuildPopup 900×1100, 본문 828×928, SPEC4 §6.4): 안내 0–56 (Caption 22 SubText) / 칸 격자 68~ (4열 × 2줄, 칸 192×304:
        /// 초상화 180 · 처음 만나면 '새 친구!' 금 리본 · 이름 (Label 24 Cream) · 별 · '기억편 +n' / '새 친구' (Caption 22 Lavender), 고른 칸은 금테) /
        /// [이 친구로 할게요] 금 560×112 아래 가운데.
        /// </summary>
        static UIPopup BuildFriendTicketPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<FriendTicketPopup>(ctx.PopupRoot, "FriendTicketPopup", "친구 선택권", new Vector2(900, 1100), out var body, out var title);
            AutoSize(title, UIType.H2, UIType.H1);
            var ticket = Txt(body, "TicketText", "선택권 1장", UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            ticket.rectTransform.Top(0, 56, 8, 8);
            AutoSize(ticket, UIType.Hint, UIType.Caption);
            var grid = NewRect("Grid", body).Top(68, 640);
            AddGrid(grid, new Vector2(192, 304), new Vector2(20, 24), 4, new RectOffset(0, 0, 0, 0));
            var cells = new Object[FriendTicketCells];
            var portraits = new Object[FriendTicketCells];
            var names = new Object[FriendTicketCells];
            var stars = new Object[FriendTicketCells];
            var subs = new Object[FriendTicketCells];
            var ribbons = new Object[FriendTicketCells];
            var frames = new Object[FriendTicketCells];
            for (int i = 0; i < FriendTicketCells; i++)
            {
                var cellImage = InsetPanel(grid, $"Cell{i}", true);
                var cell = AddButton(cellImage);
                cell.transition = Selectable.Transition.None;
                cellImage.gameObject.AddComponent<PressScale>();
                var cellRect = cellImage.rectTransform;
                var frame = UiArt.Panel(cellRect, "Selected", UiArt.SlotFrame9s, 1.6f, UITheme.GoldAccent, UITheme.GoldAccent);
                frame.raycastTarget = false;
                frame.rectTransform.Stretch(-4, -4, -4, -4);
                frame.gameObject.SetActive(false);
                var portrait = Portrait(cellRect, "Portrait", new Vector2(180, 180), PortraitBackdrop.Night, false, new Vector4(0, 0, 8, 0));
                Rt(portrait).At(TopCenter, new Vector2(0, -6), new Vector2(180, 180));
                var ribbon = UiArt.Panel(cellRect, "NewRibbon", UiArt.RibbonGold9s, 2.4f, Color.white, UITheme.GoldAccent);
                ribbon.rectTransform.At(TopCenter, new Vector2(0, -156), new Vector2(150, 34));
                Txt(ribbon.transform, "Label", "새 친구!", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch(8, 8, 0, 0);
                ribbon.gameObject.SetActive(false);
                var name = Txt(cellRect, "NameText", "이름", UIType.Label, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis();
                name.rectTransform.Top(192, 36, 6, 6);
                AutoSize(name, UIType.AutoSizeFloor, UIType.Label);
                var star = Txt(cellRect, "StarsText", UITheme.Stars(ItemGrade.Epic), UIType.Hint, UITheme.Gold, TextAlignmentOptions.Center, true);
                star.rectTransform.Top(228, 28, 6, 6);
                var sub = Txt(cellRect, "SubText", "새 친구", UIType.Caption, UITheme.Lavender, TextAlignmentOptions.Center).Ellipsis();
                sub.rectTransform.Top(258, 36, 6, 6);
                AutoSize(sub, UIType.Hint, UIType.Caption);
                cells[i] = cell;
                portraits[i] = portrait;
                names[i] = name;
                stars[i] = star;
                subs[i] = sub;
                ribbons[i] = ribbon.gameObject;
                frames[i] = frame.gameObject;
            }
            var confirm = ToneButton(body, "ConfirmButton", "이 친구로 할게요", Tone.Gold, UIType.ButtonL, out _);
            ((RectTransform)confirm.transform).At(BottomCenter, Vector2.zero, new Vector2(560, 112));

            new Binder(popup)
                .Set("_ticketText", ticket)
                .SetArray("_cells", cells)
                .SetArray("_portraits", portraits)
                .SetArray("_names", names)
                .SetArray("_stars", stars)
                .SetArray("_subTexts", subs)
                .SetArray("_newRibbons", ribbons)
                .SetArray("_selectFrames", frames)
                .Set("_confirmButton", confirm)
                .Apply();
            return popup;
        }

        // ───────────── 호감도 칸 (CR-10) ─────────────

        /// <summary>
        /// 친구 상세 정보 탭 맨 위 호감도 칸 (SPEC4 §6.3, PanelInset9s 밤 판, 높이 440 — 기억 조각 다섯 줄이 들어가도록 스펙 300보다 크다):
        /// 하트 배지 40 (UiArt.AffinityBadge) + 뒤 후광 (UiArt.AffinityHalo — 전용 아트가 없으면 흰 글로우를 DustyPink로) + 'Lv.n 호감도' (Title 28 굵게 Cream) /
        /// 오른쪽 위 보상 칩 + [받기] 금 180×80 (받을 단계가 있을 때만) / 막대 560×20 DustyPink + '{지금}/{다음}' (Label 24) /
        /// '기억 조각 k/5' (Label 24 굵게) · '오늘 쓰다듬기 k/3' (Caption 22 SubText) / 기억 조각 다섯 줄 (Body 26, 50씩) / 받을 때 하트 터짐 (UiArt.HeartBurst).
        /// 보유 계보만 보인다 (AffinitySectionView가 켜고 끈다).
        /// </summary>
        static AffinitySectionView BuildAffinitySection(RectTransform content)
        {
            var card = InsetPanel(content, "AffinitySection").rectTransform;
            Pref(card, 440);
            var view = card.gameObject.AddComponent<AffinitySectionView>();
            var heartCenter = new Vector2(44, -38);
            var halo = Img(card, "Halo", UiArt.HasAffinityHalo ? Color.white : UITheme.WithAlpha(UITheme.DustyPink, 0.75f), UiArt.AffinityHalo);
            halo.type = Image.Type.Simple;
            halo.preserveAspect = true;
            Centered(halo.rectTransform, TopLeft, heartCenter, new Vector2(92, 92));
            var heart = UiArt.Icon(card, "Heart", UiArt.AffinityBadge, TopLeft, Vector2.zero, 40);
            Centered(heart.rectTransform, TopLeft, heartCenter, new Vector2(40, 40));
            var level = Txt(card, "LevelText", "Lv.1 호감도", UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            level.rectTransform.At(TopLeft, new Vector2(80, -16), new Vector2(360, 44));

            var claim = ToneButton(card, "ClaimButton", "받기", Tone.Gold, UIType.ButtonM, out _);
            ((RectTransform)claim.transform).At(TopRight, new Vector2(-20, -12), new Vector2(180, 80));
            claim.targetGraphic.HitPad(new Vector2(180, 80), UISize.MinHit, UISize.MinHit);
            claim.gameObject.SetActive(false);
            var chip = RewardChip(card, "RewardChip", new Vector2(72, 84), 44, UIType.Hint);
            ((RectTransform)chip.transform).At(TopRight, new Vector2(-212, -10), new Vector2(72, 84));
            chip.gameObject.SetActive(false);

            var bar = UiArt.Bar(card, "Bar", UITheme.DustyPink, out var fill, 20);
            bar.rectTransform.At(TopLeft, new Vector2(24, -102), new Vector2(560, 20));
            var progress = Txt(card, "ProgressText", "0/50", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            progress.rectTransform.At(TopLeft, new Vector2(600, -92), new Vector2(240, 40));
            AutoSize(progress, UIType.Hint, UIType.Label);

            var storyHeader = Txt(card, "StoryHeader", "기억 조각 0/5", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            storyHeader.rectTransform.At(TopLeft, new Vector2(24, -136), new Vector2(400, 36));
            var pets = Txt(card, "PetsText", "오늘 쓰다듬기 0/3", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineRight);
            pets.rectTransform.At(TopRight, new Vector2(-24, -136), new Vector2(360, 36));
            var lines = new Object[AffinitySectionView.StoryCount];
            for (int i = 0; i < lines.Length; i++)
            {
                var line = Txt(card, $"Story{i}", $"…  <size=77%>Lv.{(i + 1) * 2}에 들을 수 있어요</size>", UIType.Body, UITheme.SubText,
                    TextAlignmentOptions.MidlineLeft, false, true);
                line.rectTransform.Top(178 + i * 50, 48, 24, 24);
                AutoSize(line, UIType.Hint, UIType.Body);
                lines[i] = line;
            }
            var burst = Img(card, "HeartBurst", Color.white, UiArt.HeartBurst);
            burst.type = Image.Type.Simple;
            burst.preserveAspect = true;
            Centered(burst.rectTransform, TopLeft, heartCenter, new Vector2(96, 96));
            burst.gameObject.SetActive(false);

            new Binder(view)
                .Set("_levelText", level)
                .Set("_barFill", fill)
                .Set("_progressText", progress)
                .Set("_rewardChip", chip)
                .Set("_claimButton", claim)
                .Set("_storyHeader", storyHeader)
                .SetArray("_storyLines", lines)
                .Set("_petsText", pets)
                .Set("_heartBurst", burst)
                .Apply();
            return view;
        }

        /// <summary>
        /// 쪽 탭 그림 한 쌍 (SX-2 · ART-UPGRADE §4.3 #9): 켜짐 · 꺼짐 전용 아트가 둘 다 있을 때만(UiArt.HasTabSkin) TabSkin을 붙인다 —
        /// 그때 TabStyle.Apply(Page)가 색 대신 그림을 바꾼다. 아트가 없으면 아무것도 하지 않는다 (지금처럼 색으로 칠한다)
        /// </summary>
        static void PageTabSkin(Button tab)
        {
            if (!UiArt.HasTabSkin || !(tab.targetGraphic is Image image)) return;
            var skin = image.gameObject.AddComponent<TabSkin>();
            new Binder(skin).Set("_on", UiArt.TabOn9s).Set("_off", UiArt.TabOff9s).Apply();
            UiArt.Skin9(image, UiArt.TabOff9s, 1.6f, Color.white);
        }

        /// <summary>보상 칩 (MetaSceneBuilder.BuildRewardChip, 테 없음). 줄 · 칸 안에 넣으므로 칩 자체는 누름을 받지 않는다 (줄 · 버튼이 받는다)</summary>
        static MetaRewardChip RewardChip(Transform parent, string name, Vector2 size, float iconSize, float amountSize)
        {
            _ = MetaSceneBuilder.Icons;
            var chip = MetaSceneBuilder.BuildRewardChip(parent, name, size, iconSize, amountSize, false, false);
            if (chip.TryGetComponent<Image>(out var hit)) hit.raycastTarget = false;
            return chip;
        }

        // ═════════════════════ 공용 조각 ═════════════════════

        static RectTransform Rt(Component component) => (RectTransform)component.transform;

        /// <summary>
        /// 초상화 (배경 그림 + 아트 + 아트가 없을 때의 자리표시). artInset이 음수면 아트를 틀보다 크게 그린다.
        ///   - 네모: 둥근 마스크 안에 portrait_bg를 꽉 채운다 (EnvelopeParent, 비율 유지). 아트는 마스크 밖이라 잘리지 않는다.
        ///   - 원: 밝은 받침(ui_plate_round)이 그대로 배경이자 원형 마스크 (알파로 자른다).
        /// 배경 색은 FriendPortrait가 계보 색으로 살짝(25~35%) 물들인다.
        /// </summary>
        static FriendPortrait Portrait(Transform parent, string name, Vector2 size, PortraitBackdrop backdrop, bool circle, Vector4 artInset)
        {
            var root = NewRect(name, parent);
            root.sizeDelta = size;
            var view = root.gameObject.AddComponent<FriendPortrait>();

            Image backdropImage = null;
            RectTransform holder = root;
            if (circle)
            {
                var mask = Plate(root, "Mask");
                mask.rectTransform.Stretch();
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = backdrop != PortraitBackdrop.None;
                if (backdrop != PortraitBackdrop.None) backdropImage = mask;
                holder = mask.rectTransform;
            }
            else if (backdrop != PortraitBackdrop.None)
            {
                var clip = Img(root, "BackdropMask", Color.white, RoundSprite);
                clip.rectTransform.Stretch();
                clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var art = UiArt.PortraitBg;
                backdropImage = Img(clip.transform, "Backdrop", Color.white, art);
                backdropImage.rectTransform.Stretch();
                if (UiArt.IsArt(art))
                {
                    backdropImage.type = Image.Type.Simple;
                    Envelope(backdropImage, art);
                }
            }

            float side = Mathf.Min(size.x, size.y);
            var placeholder = Plate(holder, "Placeholder");
            placeholder.rectTransform.At(Center, new Vector2(0, -side * 0.03f), Vector2.one * side * 0.62f);
            var initial = Txt(placeholder.transform, "Initial", "?", Mathf.Max(18, side * 0.26f), UITheme.Ink, TextAlignmentOptions.Center, true);
            initial.rectTransform.Stretch();

            var image = Img(holder, "Image", Color.white);
            image.preserveAspect = true;
            image.rectTransform.Stretch(artInset.x, artInset.y, artInset.z, artInset.w);

            new Binder(view)
                .Set("_image", image)
                .Set("_backdrop", backdropImage)
                .Set("_placeholder", placeholder)
                .Set("_initial", initial)
                .SetInt("_backdropStyle", (int)backdrop)
                .Apply();
            return view;
        }

        /// <summary>card_frame_9s 아트가 있으면 테두리를 덧씌운다</summary>
        static void CardFrameOverlay(Transform parent, float pixelsPerUnitMultiplier)
        {
            var art = ArtLib.UI("card_frame_9s");
            if (art == null) return;
            var frame = Img(parent, "Frame", Color.white, art);
            frame.rectTransform.Stretch(-4, -4, -4, -4);
            frame.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }

        /// <summary>색 계열 → 버튼 공장 모양 (SceneParts.ButtonStyle)</summary>
        static ButtonStyle Style(Tone tone) => tone switch
        {
            Tone.Gold => ButtonStyle.Gold,
            Tone.Secondary => ButtonStyle.Secondary,
            Tone.Soft => ButtonStyle.Soft,
            Tone.Ghost => ButtonStyle.Ghost,
            _ => ButtonStyle.Primary,
        };

        /// <summary>
        /// 색 계열 버튼 = 버튼 공장 SceneParts.Button (M25): 바탕 아트(button_primary_9s / button_gold_9s / ui_button_secondary_9s / panel_parchment_9s,
        /// 비활성 = ui_button_disabled_9s 바꿔 끼우기) + 이름 색 (Gold · Soft = Ink, Ghost = Lavender + ' ›', 나머지 = Text) + 누를 수 없을 때 이름 흐림.
        /// 아트가 없으면 단색 (Primary = PrimaryButton, Gold = UITheme.Gold …). 9-slice 배율은 크기가 정해진 뒤 FitSlices가 버튼 높이로 맞춘다.
        /// </summary>
        static Button ToneButton(Transform parent, string name, string label, Tone tone, float fontSize, out TextMeshProUGUI labelText)
        {
            var button = SceneParts.Button(parent, name, label, Style(tone), fontSize, out labelText);
            DeferButtonPpu(button);
            return button;
        }

        /// <summary>이미 만든 버튼에 색 계열 바탕 입히기 (SceneParts.SkinButton 한 줄 + 배율 미루기). 이름 색은 부르는 쪽이 정한다.</summary>
        static void ApplyArt(Button button, Tone tone)
        {
            SkinButton(button, Style(tone));
            DeferButtonPpu(button);
        }

        /// <summary>그림 버튼(9-slice 아트)만 높이에 맞춘 배율을 미뤄 둔다</summary>
        static void DeferButtonPpu(Button button)
        {
            if (button.targetGraphic is Image image && image.type == Image.Type.Sliced && UiArt.IsArt(image.sprite)) DeferPpu(image, SliceKind.Button);
        }

        /// <summary>
        /// 준비 완료 금 덮개 (M54): 보조 버튼 바탕 위에 깔리는 button_gold_9s (Sliced, 배율 1.6, 꽉 채움, 맨 뒤 형제 = 바탕 바로 위, 누름 안 받음).
        /// 꺼 둔 채 만들고 런타임 뷰가 '준비 완료'일 때만 켠다 (금 버튼 하나 규칙의 예외). 아트가 없으면 UITheme.Gold 단색.
        /// </summary>
        static Image ReadyArt(Button button)
        {
            var art = UiArt.ButtonGold9s;
            var ready = UiArt.Panel(button.transform, "ReadyArt", art, 1.6f, Color.white, UITheme.Gold);
            ready.raycastTarget = false;
            ready.rectTransform.Stretch();
            ready.transform.SetAsFirstSibling();
            ready.gameObject.SetActive(false);
            return ready;
        }

        /// <summary>탭 · 필터 버튼: 흰 바느질 판(ui_tab_9s, 틴트용). 선택 색은 런타임 뷰가 입힌다.</summary>
        static void TabArt(Button button)
        {
            var art = UiArt.Tab9s;
            if (!UiArt.IsArt(art)) return;
            UiArt.Skin9((Image)button.targetGraphic, art, 1.6f, UITheme.PanelLight);
        }

        /// <summary>밤 패널 안의 작은 칸 (ui_panel_inset_9s, 흰색). 없으면 예전 PanelLight 둥근 사각형.</summary>
        static Image InsetPanel(Transform parent, string name, bool raycast = false) =>
            UiArt.Panel(parent, name, UiArt.PanelInset9s, 2f, null, UITheme.PanelLight, raycast);

        /// <summary>밝은 둥근 받침 (ui_plate_round, 비율 유지). 색은 런타임이 흰색 → 계보/등급 색으로 옅게 입힌다.</summary>
        static Image Plate(Transform parent, string name)
        {
            var plate = Img(parent, name, Color.white, UiArt.PlateRound);
            plate.type = Image.Type.Simple;
            plate.preserveAspect = true;
            return plate;
        }

        /// <summary>
        /// 둥근 초상화 위에 덧씌우는 바느질 고리 (ui_ring_stitch, 흰색 틴트용). 초상화의 자식이라 같이 움직인다.
        /// 고리 띠(반지름 0.76~0.89)가 받침 테두리(0.88~0.95)를 덮도록 초상화의 1.14배. 아트가 없으면 빛 고리(_ring).
        /// </summary>
        static Image RingFrame(RectTransform portrait, Color color)
        {
            var art = UiArt.RingStitch;
            var ring = Img(portrait, "Ring", color, UiArt.IsArt(art) ? art : _ring);
            ring.type = Image.Type.Simple;
            ring.preserveAspect = true;
            float side = Mathf.Min(portrait.sizeDelta.x, portrait.sizeDelta.y);
            ring.rectTransform.At(Center, Vector2.zero, Vector2.one * side * 1.14f);
            return ring;
        }

        /// <summary>진화 기억도 범례 한 칸: 22px 바느질 고리(상태 색) + 이름 (캡션 22 InkSub, M29)</summary>
        static void LegendItem(RectTransform row, string name, Color color, string label)
        {
            var ring = Img(row, $"{name}Ring", color, UiArt.RingStitch);
            ring.type = Image.Type.Simple;
            ring.preserveAspect = true;
            var layout = ring.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = layout.preferredHeight = 22;
            Txt(row, $"{name}Label", label, UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineLeft);
        }

        /// <summary>가로 줄 사이 빈칸</summary>
        static void LegendGap(RectTransform row, float width)
        {
            var gap = NewRect("Gap", row).gameObject.AddComponent<LayoutElement>();
            gap.preferredWidth = gap.minWidth = width;
        }

        /// <summary>
        /// 할인 꼬리표 (ui_tag_sale, 비율 유지) + 글자. 글자의 부모가 꼬리표라 뷰가 부모째 켜고 끈다.
        /// 아트가 없으면 GoldAccent 알약 + Ink 글자 (M09 · M82 — 빨간 점 색은 받을 것에만 쓴다).
        /// </summary>
        static Image SaleTag(Transform parent, string name, string text, float fontSize, out TextMeshProUGUI label)
        {
            var art = UiArt.TagSale;
            if (!UiArt.IsArt(art)) return Pill(parent, name, text, UITheme.GoldAccent, UITheme.Ink, fontSize, out label);
            var tag = Img(parent, name, Color.white, art);
            tag.type = Image.Type.Simple;
            tag.preserveAspect = true;
            // 왼쪽 별 · 오른쪽 홈을 피해 가운데 분홍 띠에 글자를 얹는다
            label = Txt(tag.transform, "Label", text, fontSize, UITheme.Ink, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch(22, 24, 0, 2);
            return tag;
        }

        /// <summary>안내 단추 (Icons/info 그림을 그대로 누른다). 아트가 없으면 예전 동그라미 "i".</summary>
        static Button InfoButton(Transform parent)
        {
            var art = ArtLib.Icon("info");
            if (art == null)
            {
                var fallback = Btn(parent, "InfoButton", "i", UITheme.Secondary, 30);
                ((Image)fallback.targetGraphic).sprite = CircleSprite;
                ((Image)fallback.targetGraphic).type = Image.Type.Simple;
                return fallback;
            }
            var image = Img(parent, "InfoButton", Color.white, art, true);
            image.preserveAspect = true;
            image.gameObject.AddComponent<PressScale>();
            return AddButton(image);
        }

        /// <summary>
        /// 배경 그림을 부모에 꽉 채운다 (비율 유지, 넘치는 쪽은 잘림 — 부모에 마스크가 있어야 한다). BackgroundFit Cover (aspect AF-7):
        /// 자르는 자리 = 피벗 (예전 AspectRatioFitter와 같은 자르기 — 옛 소환 배너는 위 가운데 피벗이라 위를 남긴다), 등급 뒤판처럼 실행 중에 그림을 바꾸면 그 비율로
        /// </summary>
        static void Envelope(Image image, Sprite art)
        {
            image.preserveAspect = false;
            image.gameObject.AddComponent<BackgroundFit>()
                .Configure(BackgroundFitMode.Cover, image.rectTransform.pivot, art.rect.width / Mathf.Max(1f, art.rect.height));
        }

        /// <summary>가운데 피벗으로 배치 (anchor 기준 center 위치). 회전 연출(SlowSpin)이 제자리에서 돌게 한다.</summary>
        static RectTransform Centered(RectTransform rt, Vector2 anchor, Vector2 center, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = Center;
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>진짜 아트면 그대로, 자리표시면 null (런타임 뷰가 아트 유무로 갈라 그리도록)</summary>
        static Sprite ArtOrNull(Sprite sprite) => UiArt.IsArt(sprite) ? sprite : null;

        static void DeferPpu(Image image, SliceKind kind) => _pendingSlices.Add((image, kind));

        /// <summary>크기가 정해진 뒤 9-slice 배율을 높이에 맞춘다 (버튼 = UiArt.ButtonPpu, 알약 = 96 / 높이)</summary>
        static void FitSlices()
        {
            foreach (var (image, kind) in _pendingSlices)
            {
                if (image == null) continue;
                float height = SliceHeight(image.rectTransform);
                if (height < 1f) continue;
                image.pixelsPerUnitMultiplier = kind == SliceKind.Button ? UiArt.ButtonPpu(height) : UiArt.CapsulePpu(height);
            }
            _pendingSlices.Clear();
        }

        /// <summary>레이아웃이 높이를 정하는 자식은 (아직 레이아웃 전이므로) LayoutElement 높이나 부모 높이로 계산한다</summary>
        static float SliceHeight(RectTransform rt)
        {
            if (rt.parent is RectTransform parent && parent.TryGetComponent<HorizontalOrVerticalLayoutGroup>(out var layout) && layout.childControlHeight)
            {
                if (rt.TryGetComponent<LayoutElement>(out var element) && element.preferredHeight > 0f) return element.preferredHeight;
                if (layout is HorizontalLayoutGroup && layout.childForceExpandHeight) return parent.rect.height - layout.padding.vertical;
            }
            return rt.rect.height;
        }

        /// <summary>프리팹 저장 전에 9-slice 배율을 맞춘다</summary>
        static T SaveFitted<T>(GameObject go, string path) where T : Component
        {
            FitSlices();
            return SavePrefab<T>(go, path);
        }

        /// <summary>버튼 안의 재화 아이콘 + 비용 한 줄 (top부터 높이 44)</summary>
        static TextMeshProUGUI CostLine(Transform button, CurrencyType currency, float iconSize, float fontSize, float top)
        {
            var line = NewRect("Cost", button).Top(top, 44, 8, 8);
            var row = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 8;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            CurrencyIcon(line, currency, iconSize);
            return Txt(line, "CostText", "0", fontSize, UITheme.Text, TextAlignmentOptions.MidlineLeft, true);
        }

        /// <summary>
        /// 글자 알약. 어두운 바탕색이면 밤색 알약(ui_pill_night_9s, 흰색 · 바탕 알파만), 밝은 색이면 흰색 틴트용 알약(ui_pill_white_9s)에 그 색을 입힌다.
        /// 캡슐 배율(96 / 높이)은 크기가 정해진 뒤 FitSlices가 맞춘다. 아트가 없으면 예전 둥근 사각형.
        /// </summary>
        static Image Pill(Transform parent, string name, string text, Color background, Color textColor, float fontSize, out TextMeshProUGUI label)
        {
            bool night = background.grayscale < 0.3f;
            var art = night ? UiArt.PillNight9s : UiArt.PillWhite9s;
            var pill = UiArt.Panel(parent, name, art, 2f, night ? new Color(1f, 1f, 1f, background.a) : background, background);
            if (UiArt.IsArt(art)) DeferPpu(pill, SliceKind.Capsule);
            label = Txt(pill.transform, "Label", text, fontSize, textColor, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch(6, 6, 0, 0);
            return pill;
        }

        /// <summary>빨간 알림 배지 (ui_badge_red, 흰색 · 비율 유지)</summary>
        static GameObject Dot(Transform parent, Vector2 position, float size = 26f) =>
            UiArt.Badge(parent, "AlertDot", TopRight, position, size).gameObject;

        /// <summary>바느질 고리(ui_ring_stitch, 틴트용 · 기본 초록) + 체크 아이콘(Icons/check). 첫 자식이 체크 표시.</summary>
        static Image CheckMark(Transform parent, float size)
        {
            var ringArt = UiArt.RingStitch;
            var checkArt = ArtLib.Icon("check");
            var circle = Img(parent, "Check", CharacterUI.InkPositive, ringArt);
            circle.type = Image.Type.Simple;
            circle.preserveAspect = true;
            circle.rectTransform.sizeDelta = new Vector2(size, size);
            if (checkArt != null)
            {
                var mark = Img(circle.transform, "Mark", Color.white, checkArt);
                mark.preserveAspect = true;
                float inset = size * 0.14f;
                mark.rectTransform.Stretch(inset, inset, inset, inset);
            }
            else
            {
                Txt(circle.transform, "Mark", "✓", size * 0.68f, Color.white, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            }
            return circle;
        }

        /// <summary>기억편 아이콘 자리 (아트가 없으면 라벤더 마름모). 반환값은 위치를 정할 바깥 칸.</summary>
        static RectTransform ShardIcon(Transform parent, float size)
        {
            var holder = NewRect("ShardIcon", parent);
            holder.sizeDelta = new Vector2(size, size);
            var art = ArtLib.Icon("shards");
            if (art != null)
            {
                var icon = Img(holder, "Icon", Color.white, art);
                icon.preserveAspect = true;
                icon.rectTransform.Stretch();
            }
            else
            {
                var diamond = Img(holder, "Diamond", UITheme.Lavender);
                diamond.rectTransform.At(Center, Vector2.zero, Vector2.one * size * 0.66f);
                diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                Img(diamond.transform, "Shine", new Color(1f, 1f, 1f, 0.45f)).rectTransform.Stretch(0, size * 0.33f, 0, size * 0.33f);
            }
            return holder;
        }

        /// <summary>자물쇠 (아트가 없으면 몸통 + 고리를 도형으로)</summary>
        static RectTransform LockGlyph(Transform parent, float size, Color color)
        {
            var art = ArtLib.Icon("lock");
            if (art != null)
            {
                // 자물쇠 그림은 색이 있으므로 알파만 쓴다
                var icon = Img(parent, "Lock", CharacterUI.WithAlpha(Color.white, color.a), art);
                icon.preserveAspect = true;
                icon.rectTransform.sizeDelta = new Vector2(size, size);
                return icon.rectTransform;
            }

            var root = NewRect("Lock", parent);
            root.sizeDelta = new Vector2(size, size);
            float bodyWidth = size * 0.66f;
            float bodyHeight = size * 0.5f;
            float thickness = Mathf.Max(4f, size * 0.11f);
            float shackleWidth = bodyWidth * 0.66f;
            float shackleHeight = size * 0.4f;
            float bodyBottom = size * 0.04f;
            Img(root, "PostLeft", color).rectTransform.At(BottomCenter, new Vector2(-(shackleWidth - thickness) * 0.5f, bodyBottom + bodyHeight - 2), new Vector2(thickness, shackleHeight));
            Img(root, "PostRight", color).rectTransform.At(BottomCenter, new Vector2((shackleWidth - thickness) * 0.5f, bodyBottom + bodyHeight - 2), new Vector2(thickness, shackleHeight));
            Img(root, "Top", color, RoundSprite).rectTransform.At(BottomCenter, new Vector2(0, bodyBottom + bodyHeight + shackleHeight - thickness - 2), new Vector2(shackleWidth, thickness));
            var body = Img(root, "Body", color, RoundSprite);
            body.rectTransform.At(BottomCenter, new Vector2(0, bodyBottom), new Vector2(bodyWidth, bodyHeight));
            Img(body.transform, "Keyhole", new Color(0.1f, 0.07f, 0.19f, 0.85f), CircleSprite).rectTransform.At(Center, new Vector2(0, 2), new Vector2(size * 0.14f, size * 0.14f));
            return root;
        }

        /// <summary>
        /// ›››  화살표 흐름 (ChevronFlow): 꺾쇠 그림(ui_chevron, 금색 틴트) 셋. height = 꺾쇠 높이, step = 꺾쇠 사이 간격.
        /// 그림 칸은 4:3이고 꺾쇠는 가운데 절반 폭이라 칸을 겹쳐 늘어놓는다. 아트가 없으면 "›" 글자.
        /// </summary>
        static RectTransform Chevrons(Transform parent, string name, float height, float step)
        {
            var root = NewRect(name, parent);
            var art = UiArt.Chevron;
            float fontSize = height * 1.35f;
            float width = art != null ? height * 4f / 3f : step * 0.87f;
            var row = HRow(root, art != null ? step - width : -fontSize * 0.25f, TextAnchor.MiddleCenter);
            row.childControlWidth = true;
            row.childControlHeight = true;
            var items = new Graphic[3];
            for (int i = 0; i < items.Length; i++)
            {
                Graphic chevron;
                if (art != null)
                {
                    var image = Img(root, $"Chevron{i}", UITheme.GoldAccent, art);
                    image.preserveAspect = true;
                    chevron = image;
                }
                else
                {
                    chevron = Txt(root, $"Chevron{i}", "›", fontSize, UITheme.GoldAccent, TextAlignmentOptions.Center, true);
                }
                var layout = chevron.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = width;
                layout.preferredHeight = art != null ? height : fontSize * 1.2f;
                items[i] = chevron;
            }
            var flow = root.gameObject.AddComponent<ChevronFlow>();
            new Binder(flow).SetArray("_items", items).Apply();
            return root;
        }

        /// <summary>반짝이 연출 (비활성 템플릿 하나 포함). 위치는 호출한 쪽에서 정한다 (피벗은 가운데로).</summary>
        static SparkleBurst Burst(Transform parent, string name, float radius, float sparkSize)
        {
            var root = NewRect(name, parent);
            var burst = root.gameObject.AddComponent<SparkleBurst>();
            var template = Img(root, "Spark", Color.white, _sparkle);
            template.rectTransform.At(Center, Vector2.zero, new Vector2(sparkSize, sparkSize));
            template.gameObject.SetActive(false);
            new Binder(burst).Set("_template", template).Apply();
            SetFloats(burst, ("_radius", radius));
            return burst;
        }

        static SoftPulse Pulse(Component target, float minAlpha, float maxAlpha, float period, float scale)
        {
            var pulse = target.gameObject.AddComponent<SoftPulse>();
            SetFloats(pulse, ("_minAlpha", minAlpha), ("_maxAlpha", maxAlpha), ("_period", period), ("_scaleAmount", scale));
            return pulse;
        }

        static TextMeshProUGUI CardTitle(RectTransform card, string title)
        {
            var text = Txt(card, "Title", title, 26, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true);
            text.rectTransform.At(TopLeft, new Vector2(30, -20), new Vector2(600, 40));
            return text;
        }

        /// <summary>세로 목록의 절 제목 앞 8px 틈 (M71)</summary>
        static void SectionGap(RectTransform parent, string name) => Pref(NewRect(name, parent), UISpace.S8);

        static TextMeshProUGUI SectionTitle(RectTransform parent, string name, string text)
        {
            var label = Txt(parent, name, text, 30, UITheme.Cream, TextAlignmentOptions.BottomLeft, true);
            Pref(label, 54);
            return label;
        }

        /// <summary>자동 크기 min ~ max (M23: UIKit.FitFloor — 최소는 16 아래로 내려가지 않고, 16보다 작게 부르면 경로와 함께 경고)</summary>
        static void AutoSize(TextMeshProUGUI text, float min, float max) => text.FitFloor(min, max);

        static Button AddButton(Image target)
        {
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        static LayoutElement Pref(Component component, float height)
        {
            if (!component.TryGetComponent<LayoutElement>(out var layout)) layout = component.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            return layout;
        }

        /// <summary>자식 높이를 직접 정하는 세로 목록 (자식은 LayoutElement 높이)</summary>
        static VerticalLayoutGroup VList(RectTransform rt, float spacing, RectOffset padding)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>자식 크기를 그대로 두는 가로 줄</summary>
        static HorizontalLayoutGroup HRow(RectTransform rt, float spacing, TextAnchor align)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = align;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return row;
        }

        static void SetFloats(Object target, params (string field, float value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
                prop.floatValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════ 스킬 / 특성 / 시너지 조각 ═════════════════════

        /// <summary>
        /// 아이콘 + 글자 칩 (CharacterChip). 글자 길이에 맞춰 폭이 정해지므로 부모는 가로/세로 레이아웃(ChipRow 등)이어야 한다.
        /// withIcon이면 왼쪽에 아이콘 칸 (아트가 없으면 런타임에 숨김).
        /// 바탕은 흰색 틴트용 알약(ui_pill_white_9s, 칩 색), 초상화 위 glass 칩은 밤색 알약(ui_pill_night_9s)으로 런타임이 바꿔 끼운다.
        /// </summary>
        static CharacterChip Chip(Transform parent, string name, float height, float fontSize, bool withIcon, Color background, Color textColor, string text)
        {
            var bg = UiArt.Panel(parent, name, UiArt.PillWhite9s, UiArt.CapsulePpu(height), background, background);
            bg.rectTransform.sizeDelta = new Vector2(120, height);
            var layout = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            int pad = Mathf.RoundToInt(height * 0.36f);
            layout.padding = new RectOffset(withIcon ? Mathf.RoundToInt(height * 0.18f) : pad, pad, 0, 0);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = bg.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            Image icon = null;
            if (withIcon)
            {
                icon = Img(bg.transform, "Icon", Color.white);
                icon.preserveAspect = true;
                var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
                iconLayout.minWidth = iconLayout.preferredWidth = height - 8;
                iconLayout.minHeight = iconLayout.preferredHeight = height - 8;
                icon.gameObject.SetActive(false);
            }
            var label = Txt(bg.transform, "Label", text, fontSize, textColor, TextAlignmentOptions.Center, true);
            var chip = bg.gameObject.AddComponent<CharacterChip>();
            new Binder(chip)
                .Set("_background", bg)
                .Set("_icon", icon)
                .Set("_label", label)
                .Set("_icons", _icons)
                .Set("_glassSprite", ArtOrNull(UiArt.PillNight9s))
                .Apply();
            return chip;
        }

        /// <summary>칩을 글자 길이대로 나란히 놓는 가로 줄 (넘치는 폭은 비워 둔다)</summary>
        static RectTransform ChipRow(Transform parent, string name, float spacing, TextAnchor align)
        {
            var row = NewRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>
        /// 스킬 아이콘 구슬: 어두운 메달 받침(ui_medallion_dark) + 스킬 연출 아트 + 구슬 테(ui_skill_orb_frame, 효과 색 30%).
        /// 연출 아트가 없으면 별 아이콘(Icons/star) + 첫 글자. 공용 조각이 없으면 예전 원 · 빛 고리.
        /// </summary>
        static CharacterSkillOrb SkillOrb(Transform parent, string name, float size, bool glow)
        {
            var root = NewRect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            var orb = root.gameObject.AddComponent<CharacterSkillOrb>();

            Image glowImage = null;
            if (glow)
            {
                glowImage = Img(root, "Glow", new Color(1f, 1f, 1f, 0.6f), _glow);
                glowImage.rectTransform.At(Center, Vector2.zero, Vector2.one * size * 1.8f);
                Pulse(glowImage, 0.3f, 0.8f, 2.2f, 0.05f);
            }
            var medallion = UiArt.MedallionDark;
            bool hasMedallion = UiArt.IsArt(medallion);
            var backdrop = Img(root, "Backdrop", hasMedallion ? Color.white : UITheme.Panel, medallion);
            backdrop.type = Image.Type.Simple;
            backdrop.preserveAspect = true;
            backdrop.rectTransform.Stretch();
            var star = UiArt.Star;
            bool hasStar = UiArt.IsArt(star);
            var fallback = Img(root, "Fallback", hasStar ? Color.white : UITheme.Lavender, star);
            fallback.type = Image.Type.Simple;
            fallback.preserveAspect = true;
            fallback.rectTransform.At(Center, Vector2.zero, Vector2.one * size * 0.64f);
            var letter = Txt(fallback.transform, "Letter", "?", Mathf.Max(14f, size * 0.32f), UITheme.Ink, TextAlignmentOptions.Center, true);
            letter.rectTransform.Stretch();
            var icon = Img(root, "Icon", Color.white);
            icon.preserveAspect = true;
            // 구슬 테 안쪽 구멍(반지름 0.73)을 넘지 않게 연출 아트를 줄여 그린다
            float inset = (hasMedallion ? 0.16f : 0.04f) * size;
            icon.rectTransform.Stretch(inset, inset, inset, inset);
            var frameArt = UiArt.SkillOrbFrame;
            Image ring;
            if (UiArt.IsArt(frameArt))
            {
                ring = Img(root, "Frame", Color.white, frameArt);
                ring.type = Image.Type.Simple;
                ring.preserveAspect = true;
                ring.rectTransform.At(Center, Vector2.zero, Vector2.one * size * 1.06f);
            }
            else
            {
                Img(root, "Gloss", new Color(1f, 1f, 1f, 0.14f), CircleSprite).rectTransform.At(TopCenter, new Vector2(0, -size * 0.08f), new Vector2(size * 0.56f, size * 0.28f));
                ring = Img(root, "Ring", UITheme.Lavender, _ring);
                ring.rectTransform.At(Center, Vector2.zero, Vector2.one * size * 1.2f);
            }

            new Binder(orb)
                .Set("_backdrop", backdrop)
                .Set("_ring", ring)
                .Set("_icon", icon)
                .Set("_fallback", fallback)
                .Set("_letter", letter)
                .Set("_glow", glowImage)
                .Set("_icons", _icons)
                .Apply();
            return orb;
        }

        /// <summary>
        /// 스킬 카드 (크림 카드, 가로 늘림 x 372): 구슬 / 머리 문구 / 이름 / 발동 시점 칩 / 설명 / 대상·효과 / 시전 대사 말풍선.
        /// 세로 목록 안에 넣으면 폭이 목록에 맞춰진다.
        /// </summary>
        static CharacterSkillCard SkillCard(RectTransform parent, string name)
        {
            const float height = 372f;
            var card = ParchmentCard(parent, name);
            var root = card.rectTransform;
            root.sizeDelta = new Vector2(928, height);
            Pref(card, height);
            var view = card.gameObject.AddComponent<CharacterSkillCard>();

            var orb = SkillOrb(root, "Orb", 124, true);
            Rt(orb).At(TopLeft, new Vector2(34, -42), new Vector2(124, 124));
            var caption = Txt(root, "CaptionText", "고유 스킬", UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            caption.rectTransform.Top(18, 34, 186, 28);
            var nameText = Txt(root, "NameText", "스킬", 34, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            nameText.rectTransform.Top(50, 52, 186, 28);
            AutoSize(nameText, 24, 34);
            var chips = ChipRow(root, "Chips", 8, TextAnchor.MiddleLeft);
            chips.Top(104, 38, 186, 28);
            var trigger = Chip(chips, "TriggerChip", 36, 20, false, UITheme.SoftBlue, UITheme.Ink, "8초마다");
            var echoOnly = Chip(chips, "EchoOnlyChip", 36, 20, true, UITheme.EchoGlow, UITheme.Ink, "잔향이 전용");
            var desc = Txt(root, "DescText", string.Empty, 23, UITheme.Ink, TextAlignmentOptions.TopLeft, false, true);
            desc.rectTransform.Top(152, 96, 186, 28);
            desc.lineSpacing = 4;
            AutoSize(desc, 17, 23);

            // 구분선은 글자가 아닌 선 → InkLine (M29)
            Img(root, "Divider", CharacterUI.WithAlpha(UITheme.InkLine, 0.25f)).rectTransform.Bottom(120, 2, 32, 32);
            var effects = Txt(root, "EffectsText", string.Empty, 20, UITheme.Ink, TextAlignmentOptions.MidlineLeft, false, true);
            effects.rectTransform.Bottom(64, 54, 32, 32);
            AutoSize(effects, UIType.AutoSizeFloor, 20);
            var quote = UiArt.Panel(root, "Quote", UiArt.PillWhite9s, UiArt.CapsulePpu(44), new Color(0.95f, 0.82f, 0.87f, 0.85f), new Color(0.93f, 0.8f, 0.84f, 0.6f));
            quote.rectTransform.Bottom(16, 44, 32, 32);
            var quoteText = Txt(quote.transform, "Text", "“…”", 21, CharacterUI.InkPink, TextAlignmentOptions.Center, true).Ellipsis();
            quoteText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            quoteText.rectTransform.Stretch(16, 16, 0, 0);

            new Binder(view)
                .Set("_orb", orb)
                .Set("_captionText", caption)
                .Set("_nameText", nameText)
                .Set("_triggerChip", trigger)
                .Set("_echoOnlyChip", echoOnly)
                .Set("_descText", desc)
                .Set("_effectsText", effects)
                .Set("_quote", quote.gameObject)
                .Set("_quoteText", quoteText)
                .Apply();
            return view;
        }

        /// <summary>
        /// 크림 카드 + 세로 목록. 첫 줄은 제목 줄(header, 오른쪽 190px은 버튼/안내 자리), 그 뒤에 항목이 붙는다.
        /// 반환한 카드를 UIListPool 부모로 쓰면 항목이 제목 줄 아래로 쌓이고 카드 높이가 따라 늘어난다.
        /// </summary>
        static RectTransform ListCard(RectTransform parent, string name, out TextMeshProUGUI title, out RectTransform header)
        {
            var card = ParchmentCard(parent, name).rectTransform;
            card.sizeDelta = new Vector2(928, 200);
            VList(card, 10, new RectOffset(26, 26, 22, 26));
            header = NewRect("Header", card);
            Pref(header, 50);
            title = Txt(header, "Title", string.Empty, 26, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            title.rectTransform.Stretch(6, 190, 0, 0);
            return card;
        }

        /// <summary>
        /// 스킬 연출(Art/VFX) / 특성 / 성향 / 조합 시너지 / 역할 아이콘을 CharacterIconLibrary 에셋에 묶는다.
        /// 아직 없는 아트는 빠지고, 런타임 뷰가 색 도형으로 대신한다.
        /// </summary>
        static void PrepareIcons(SceneBuildContext ctx)
        {
            var library = AssetDatabase.LoadAssetAtPath<CharacterIconLibrary>(IconLibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(PrefabDir);
                library = ScriptableObject.CreateInstance<CharacterIconLibrary>();
                AssetDatabase.CreateAsset(library, IconLibraryPath);
            }

            var vfxKeys = new List<string>();
            var vfxSprites = new List<Object>();
            foreach (var key in SkillVfx.All)
            {
                var sprite = ArtLib.Vfx(key);
                if (sprite == null) continue;
                vfxKeys.Add(key);
                vfxSprites.Add(sprite);
            }

            var traits = new Object[EnumSlots(typeof(TraitType))];
            foreach (TraitType trait in Enum.GetValues(typeof(TraitType))) traits[(int)trait] = ArtLib.Trait(trait);
            var styles = new Object[EnumSlots(typeof(CombatStyle))];
            foreach (CombatStyle style in Enum.GetValues(typeof(CombatStyle))) styles[(int)style] = ArtLib.Style(style);

            var synergyIds = new List<string>();
            var synergySprites = new List<Object>();
            var comboIds = new List<string>(ComboSynergyIds);
            var synergyDb = ctx.Content != null ? ctx.Content.Synergies : null;
            if (synergyDb != null)
            {
                foreach (var def in synergyDb.synergies)
                {
                    if (def != null && def.kind == SynergyKind.Combo && !string.IsNullOrEmpty(def.id) && !comboIds.Contains(def.id)) comboIds.Add(def.id);
                }
            }
            foreach (var id in comboIds)
            {
                var sprite = ArtLib.Synergy(id);
                if (sprite == null) continue;
                synergyIds.Add(id);
                synergySprites.Add(sprite);
            }

            var so = new SerializedObject(library);
            SetStrings(so, "_vfxKeys", vfxKeys);
            SetObjects(so, "_vfxSprites", vfxSprites);
            SetObjects(so, "_traitIcons", traits);
            SetObjects(so, "_styleIcons", styles);
            SetStrings(so, "_synergyIds", synergyIds);
            SetObjects(so, "_synergySprites", synergySprites);
            Find(so, "_guardianIcon").objectReferenceValue = ArtLib.Icon("guardian");
            Find(so, "_echoIcon").objectReferenceValue = ArtLib.Icon("echo");
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(library);
            _icons = library;
        }

        /// <summary>enum 값을 그대로 인덱스로 쓰는 배열 길이 (최댓값 + 1)</summary>
        static int EnumSlots(Type enumType)
        {
            int max = -1;
            foreach (var value in Enum.GetValues(enumType)) max = Mathf.Max(max, Convert.ToInt32(value));
            return max + 1;
        }

        static SerializedProperty Find(SerializedObject so, string field) =>
            so.FindProperty(field) ?? throw new InvalidOperationException($"{so.targetObject.GetType().Name}.{field} 필드를 찾을 수 없습니다.");

        static void SetStrings(SerializedObject so, string field, IList<string> values)
        {
            var prop = Find(so, field);
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        static void SetObjects(SerializedObject so, string field, IList<Object> values)
        {
            var prop = Find(so, field);
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ═════════════════════ 연출 스프라이트 ═════════════════════

        /// <summary>
        /// 힉스필드 FX 아트를 쓰고 (반짝이 = fx_sparkle → vfx_gloss_shine, 고리 = fx_ring_thread → ring_glow),
        /// 없으면 코드로 흰색 + 알파 스프라이트를 만든다. 진화 빛 고리 / 큰 제목 반짝이는 VFX 그림.
        /// </summary>
        static void PrepareSprites()
        {
            _glow = ArtOr(ArtLib.FX("glow_soft"), "CH_Glow", 128, 128, RadialGlow);
            _sparkle = ArtOr(FirstArt(ArtLib.FX("fx_sparkle"), ArtLib.Vfx("vfx_gloss_shine")), "CH_Sparkle", 128, 128, SparkleShape);
            _ring = ArtOr(FirstArt(ArtLib.FX("fx_ring_thread"), ArtLib.FX("ring_glow")), "CH_Ring", 256, 256, RingShape);
            _revealLight = FirstArt(ArtLib.Vfx("vfx_reveal_light"), _ring);
            _gloss = FirstArt(ArtLib.Vfx("vfx_gloss_shine"), _sparkle);
            _fadeUp = Generated("CH_FadeUp", 8, 128, (u, v) => Mathf.Pow(1f - v, 1.3f));
            _fadeDown = Generated("CH_FadeDown", 8, 128, (u, v) => Mathf.Pow(v, 1.3f));
            _fadeLeft = Generated("CH_FadeLeft", 128, 8, (u, v) => Mathf.Pow(1f - u, 1.6f));
        }

        static Sprite ArtOr(Sprite art, string name, int width, int height, Func<float, float, float> alpha) =>
            art != null ? art : Generated(name, width, height, alpha);

        /// <summary>처음으로 있는 아트 (Unity 객체라 ?? 대신 != null로 고른다)</summary>
        static Sprite FirstArt(params Sprite[] candidates)
        {
            foreach (var sprite in candidates)
            {
                if (sprite != null) return sprite;
            }
            return null;
        }

        static float RadialGlow(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.5f;
            return r >= 1f ? 0f : Mathf.Pow(1f - r, 2.2f);
        }

        static float SparkleShape(float u, float v)
        {
            float x = Mathf.Abs(u - 0.5f) * 2f;
            float y = Mathf.Abs(v - 0.5f) * 2f;
            float r2 = x * x + y * y;
            float core = Mathf.Exp(-r2 * 40f);
            float halo = 0.3f * Mathf.Exp(-r2 * 8f);
            float beamH = Mathf.Exp(-y * 28f) * Mathf.Pow(Mathf.Max(0f, 1f - x), 2.4f);
            float beamV = Mathf.Exp(-x * 28f) * Mathf.Pow(Mathf.Max(0f, 1f - y), 2.4f);
            return (core + halo + beamH + beamV) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 6f + 0.4f);
        }

        static float RingShape(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.5f;
            float d = r - 0.8f;
            float alpha = Mathf.Exp(-d * d / (0.055f * 0.055f)) + 0.35f * Mathf.Exp(-d * d / (0.16f * 0.16f));
            return alpha * Mathf.Clamp01((1f - r) * 10f);
        }

        static Sprite Generated(string name, int width, int height, Func<float, float, float> alpha)
        {
            string path = $"{GeneratedDir}/{name}.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float a = Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height));
                        pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                Directory.CreateDirectory(GeneratedDir);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
