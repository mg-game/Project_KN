using System.Collections.Generic;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.SceneParts;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 메뉴 화면: 강화(수선점 강화 + 친구 레벨) / 편성(방위 링 + 잔향이 덱 + 팀 시너지 + 캐릭터 목록) /
    /// 원정(구역 카드 + 잔향이 던전) / 상점(수선점 상점 + 별이 머문 밤 산책) 탭과
    /// 관련 팝업(보상, 가방, 재료 정보, 원정 준비, 던전 소탕, 시너지 상세, 시너지 도감), 획득처/시너지 이동 처리기.
    /// 그림은 힉스필드 아트(ArtLib · 공용 조각 UiArt)를 쓰고, 아트가 없으면 색 도형 자리표시로 대신한다.
    /// 색이 있는 그림(버튼 · 패널 · 받침 · 메달)은 흰색 그대로, 흰 틴트용 조각(알약 · 칸 틀 · 단추 점 · 고리 · 탭)만 상태 색을 입힌다.
    /// 상단 바와 ≡ 메뉴 팝업은 FrameSceneBuilder가 만든다 (UX-SPEC-IA §3.1 · §3.6).
    /// </summary>
    internal static class MenuSceneBuilder
    {
        const string PrefabDir = IdleProjectSetup.Root + "/Prefabs/UI/Menu";

        // 편성 미니 전장 (방위 링은 원근으로 세로가 눌린 타원)
        static readonly Vector2 FieldSize = new(644, 386);
        static readonly Vector2 RingCenter = new(0, -20);
        const float RingRadiusX = 214f;
        const float RingRadiusY = 110f;

        /// <summary>크림 카드 위 별 색 (금색은 크림 바탕에서 흐려서 조금 진하게)</summary>
        static readonly Color StarInk = new(0.78f, 0.53f, 0.16f);
        static readonly Color NightInner = new(0.12f, 0.09f, 0.24f, 0.94f);

        enum Tone
        {
            Primary,
            Gold,
            Secondary,
            /// <summary>양피지 버튼 (크림 카드 위 작은 버튼, 글자 Ink)</summary>
            Soft,
        }

        /// <summary>방위 슬롯 순서(위 → 반시계)의 받침 그림 색 이름 (Battle/guardian_pad_{색}) — UITheme.SlotColor와 같은 순서</summary>
        static readonly string[] PadColorNames = { "pink", "mint", "lavender", "gold", "sky" };

        sealed class SharedPrefabs
        {
            public RewardSlotView Chip;
            public RewardSlotView Slot;
        }

        static SharedPrefabs _prefabs;
        static SceneBuildContext _prefabOwner;

        static SharedPrefabs Prefabs(SceneBuildContext ctx)
        {
            if (_prefabs != null && _prefabOwner == ctx) return _prefabs;
            _prefabOwner = ctx;
            _prefabs = new SharedPrefabs { Chip = BuildRewardChipPrefab(), Slot = BuildRewardSlotPrefab() };
            return _prefabs;
        }

        // ═════════════════════════ 강화 탭 ═════════════════════════

        // 강화 탭 배치 (UX-SPEC §2.4 표, 780 ContentArea 위에서부터): 머리 0~88 · 수선점 카드 96~306 · 버튼 322~472 ·
        // 친구 머리 줄 472~560 ('모두 레벨업' 그림 490~542, 누르는 자리 472~560 = 88 — M22) · 목록 560~766 (카드 196 + 위아래 5 = 데려오기 금빛 테 자리).
        // 명세 표의 머리 480~532 · 목록 534~740은 '모두 레벨업'의 누르는 자리가 52+4에 그쳐 머리 줄을 26 넓혔다 (아래 여백 40 → 14)
        const float GrowthCardTop = 96f;
        const float GrowthCardHeight = 210f;
        const float GrowthButtonsTop = 322f;
        const float GrowthButtonsHeight = 150f;
        const float LineageHeaderTop = 494f;
        const float LineageAllTop = 490f;
        static readonly Vector2 LineageAllSize = new(200, 52);
        const float LineageListTop = 560f;
        const float LineageListHeight = 206f;
        /// <summary>친구 카드 212x196 (5번째 카드가 ≈150px 엿보인다), 간격 12, 좌우 24</summary>
        static readonly Vector2 LineageCardSize = new(212, 196);
        const float LineageCardGap = 12f;
        /// <summary>목록이 넘칠 때 오른쪽 끝 흐림 폭 (패널 색 α0 → 0.9)</summary>
        const float LineageFadeWidth = 72f;

        /// <summary>
        /// 강화 탭 (MainTab.Growth, UX-SPEC §2.4 M33 · M34 · M35): 머리 '수선점 강화' · 수선점 카드(그림 180, Lv 38, 초당 단추 30 · 내구도 26 — 다음 값 PositiveInk) ·
        /// 버튼 줄(x1 Primary + 길게 누르기 + '길게' 알약 / x10 Primary / 최대 Gold — 모두 CostButtonView, 각자 ShineMask/Shine) ·
        /// '친구 레벨 N명' + '모두 레벨업' · 친구 카드 가로 목록(+ 오른쪽 끝 흐림). 780 아래로는 그리지 않는다.
        /// 이름 · 경로는 그대로: GrowthPanel/Buttons/LevelUp1Button (코치 · 연기 테스트), GrowthPanel/LineageList (+ 카드 LevelUpButton).
        /// </summary>
        public static GameObject BuildGrowthPanel(SceneBuildContext ctx)
        {
            var root = NewRect("GrowthPanel", ctx.ContentArea).Stretch();
            var view = root.gameObject.AddComponent<GrowthPanel>();
            PanelHeader(root, "수선점 강화", out _, out _);

            // 수선점 카드 (크림 카드 · Ink 글자). 카드 위 '전투력 +N'은 없다 — 상단 바 전투력 연출이 맡는다
            var card = Card(root, "ShopCard").rectTransform.Top(GrowthCardTop, GrowthCardHeight, 24, 24);
            Glow(card, "ShopGlow", WithAlpha(UITheme.Lamp, 0.55f), MiddleLeft, new Vector2(11, 0), new Vector2(230, 210));
            var shopArt = ShopArt(card, MiddleLeft, new Vector2(36, 0), 180);
            var level = Txt(card, "LevelText", "수선점 Lv.1", 38, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).FitFloor(28);
            level.rectTransform.At(TopLeft, new Vector2(250, -24), new Vector2(750, 50));
            var stat = Txt(card, "StatText", "초당 단추 1", 30, UITheme.Ink, TextAlignmentOptions.MidlineLeft).FitFloor(22);
            stat.rectTransform.At(TopLeft, new Vector2(250, -78), new Vector2(750, 42));
            var hp = Txt(card, "HpText", "수선점 내구도 300", 26, UITheme.Ink, TextAlignmentOptions.MidlineLeft).FitFloor(20);
            hp.rectTransform.At(TopLeft, new Vector2(250, -124), new Vector2(750, 38));

            // 강화 버튼 x1 (길게 누르면 연속) / x10 / 최대 — x10 · 최대는 누르기만. 금색은 최대 하나 (M26)
            var row = NewRect("Buttons", root).Top(GrowthButtonsTop, GrowthButtonsHeight, 24, 24);
            AddRow(row, 18);
            var levelUp1 = GrowthCostButton(row, "LevelUp1Button", "강화 x1", ButtonStyle.Primary, CostSurface.Dark, out _, out var cost1, out var cost1View);
            levelUp1.gameObject.AddComponent<HoldRepeatButton>();
            HoldPill(levelUp1.transform);
            var shine1 = ShineSweep(levelUp1.transform);
            var levelUp10 = GrowthCostButton(row, "LevelUp10Button", "강화 x10", ButtonStyle.Primary, CostSurface.Dark, out _, out var cost10, out var cost10View);
            var shine10 = ShineSweep(levelUp10.transform);
            // 흐림은 GrowthPanel이 금 바탕 그림 알파로만 한다 ('최대' 글자는 그대로 — 묶음 CanvasGroup은 글자까지 흐려 ≈2:1이었다)
            var levelUpMax = GrowthCostButton(row, "LevelUpMaxButton", "최대", ButtonStyle.Gold, CostSurface.Gold, out var maxLabel, out var costMax, out var costMaxView);
            var shineMax = ShineSweep(levelUpMax.transform);

            // 친구 레벨 (계보 공유): 머리 '친구 레벨 N명' + 오른쪽 '모두 레벨업' (예전 안내 줄 자리)
            var lineageHeader = Txt(root, "LineageHeader", "친구 레벨", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(24);
            lineageHeader.rectTransform.At(TopLeft, new Vector2(24, -LineageHeaderTop), new Vector2(560, 44));
            var levelAll = SceneParts.Button(root, "LevelUpAllButton", "모두 레벨업", ButtonStyle.Secondary, UIType.Label, out var levelAllLabel);
            ((RectTransform)levelAll.transform).At(TopRight, new Vector2(-30, -LineageAllTop), LineageAllSize);
            levelAllLabel.FitFloor(UIType.Micro);
            // 누르는 자리 88 (M22): 위로 18 = 강화 버튼 줄 아래 끝(472)까지, 아래로 18 = 카드 목록 위 끝(560)까지 — 이웃을 덮지 않는다
            levelAll.targetGraphic.HitPad(LineageAllSize, 0f, UISize.MinHit, 0f, 0f,
                LineageListTop - (LineageAllTop + LineageAllSize.y), LineageAllTop - (GrowthButtonsTop + GrowthButtonsHeight));

            var listRoot = NewRect("LineageList", root).Top(LineageListTop, LineageListHeight);
            var lineageContent = HorizontalScroll(listRoot, LineageCardGap, new RectOffset(24, 24, 0, 0), out var lineageScroll);
            var fade = Img(listRoot, "EdgeFade", WithAlpha(UITheme.Panel, 0.9f), FadeRightSprite());
            var fadeRect = fade.rectTransform;
            fadeRect.anchorMin = new Vector2(1f, 0f);
            fadeRect.anchorMax = new Vector2(1f, 1f);
            fadeRect.pivot = new Vector2(1f, 0.5f);
            fadeRect.anchoredPosition = Vector2.zero;
            fadeRect.sizeDelta = new Vector2(LineageFadeWidth, 0f);
            fade.SetInactive();
            var empty = Txt(listRoot, "EmptyLabel", "아직 만난 친구가 없어요.", 26, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.SetInactive();

            new Binder(view)
                .Set("_levelText", level)
                .Set("_statText", stat)
                .Set("_hpText", hp)
                .Set("_levelUp1Button", levelUp1)
                .Set("_levelUp10Button", levelUp10)
                .Set("_levelUpMaxButton", levelUpMax)
                .Set("_cost1Text", cost1)
                .Set("_cost10Text", cost10)
                .Set("_costMaxText", costMax)
                .Set("_maxLabel", maxLabel)
                .Set("_shopArt", shopArt)
                .Set("_cost1View", cost1View)
                .Set("_cost10View", cost10View)
                .Set("_costMaxView", costMaxView)
                .SetArray("_shines", shine1, shine10, shineMax)
                .Set("_lineageHeader", lineageHeader)
                .Set("_lineageScroll", lineageScroll)
                .Set("_lineageContent", lineageContent)
                .Set("_lineagePrefab", BuildGrowthLineageCardPrefab())
                .Set("_lineageEmptyLabel", empty.gameObject)
                .Set("_lineageFade", fade.gameObject)
                .Set("_levelUpAllButton", levelAll)
                .Apply();
            FitButtonBorders(root);
            return root.gameObject;
        }

        /// <summary>
        /// 강화 비용 버튼: 예전 비용 버튼 모양(제목 32 · 아이콘 + 비용 30) + 버튼 공장 그림(SkinButton) + 이름 모양(StyleLabel — 금이면 Ink) +
        /// 세 상태 보기(UIKit.CostBtn — 양피지 아닌 바탕은 부족할 때 어두운 알약). 이름은 그대로 (코치 · 테스트가 찾는다).
        /// </summary>
        static Button GrowthCostButton(RectTransform row, string name, string title, ButtonStyle style, CostSurface surface,
            out TextMeshProUGUI label, out TextMeshProUGUI cost, out CostButtonView costView)
        {
            var button = CostButton(row, name, title, CurrencyType.Gold, FlatColor(style), out label, out cost);
            SkinButton(button, style);
            StyleLabel(label, style);
            costView = CostBtn(button, cost, surface, label);
            return button;
        }

        /// <summary>강화 x1 '길게' 알약 (76x32, 오른쪽 위 (−10, −10)): PanelLight@0.85 + Cream 19 굵게, 누름 없음 — 길게 누를 수 있다는 표시</summary>
        static void HoldPill(Transform button)
        {
            var pill = UiArt.Panel(button, "HoldPill", UiArt.PillWhite9s, UiArt.CapsulePpu(32), WithAlpha(UITheme.PanelLight, 0.85f),
                WithAlpha(UITheme.PanelLight, 0.85f));
            pill.raycastTarget = false;
            pill.rectTransform.At(TopRight, new Vector2(-10, -10), new Vector2(76, 32));
            var text = Txt(pill.transform, "Text", "길게", 19, UITheme.Cream, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            text.rectTransform.Stretch(4, 4, 0, 0);
        }

        /// <summary>
        /// 살 수 있게 됨 반짝임 (M35): 버튼 맨 뒤 자식 "ShineMask"(RectMask2D, 안쪽 6) 안의 "Shine" (부드러운 빛 56x170, 20° 기울임, Cream@0.30, 꺼 둠).
        /// 두 이름 모두 'LevelUpButton'을 담지 않는다 (코치 · 튜토리얼이 그 이름으로 찾는다). 움직임은 GrowthPanel이 한다.
        /// </summary>
        static Image ShineSweep(Transform button)
        {
            var mask = NewRect("ShineMask", button).Stretch(6, 6, 6, 6);
            mask.gameObject.AddComponent<RectMask2D>();
            var shine = Img(mask, "Shine", WithAlpha(UITheme.Cream, 0.30f), SoftGlow());
            shine.rectTransform.At(Center, Vector2.zero, new Vector2(56, 170));
            shine.rectTransform.localRotation = Quaternion.Euler(0, 0, 20);
            shine.SetInactive();
            mask.SetAsLastSibling();
            return shine;
        }

        /// <summary>
        /// 강화 탭 친구 카드 (212x196, M34): 초상화 100 + 'Lv.N' 받침(Ink@0.85 · Cream 20 굵게, 88x26 (14, −80)) ·
        /// 이름 · 별 (x 112, 폭 92) · 전투력 칸 (112, −72) 92x34 (아이콘 20 + '+N' 20 PositiveInk) · 레벨업 버튼(CostButtonView + 길게 누르기 0.45초) · 데려오기 금빛 테.
        /// 이름 글자 "NameText"는 계보 이름 그대로 (CoachTargetResolver.FindCardByLineage가 찾는다).
        /// </summary>
        static GrowthLineageCardView BuildGrowthLineageCardPrefab()
        {
            var card = Card(null, "GrowthLineageCard");
            var root = card.rectTransform;
            root.sizeDelta = LineageCardSize;
            var view = card.gameObject.AddComponent<GrowthLineageCardView>();

            UiArt.Icon(root, "PortraitBg", UiArt.PlateRound, TopLeft, new Vector2(12, -12), 92);
            var portrait = Portrait(root, "Portrait", TopLeft, new Vector2(8, -4), new Vector2(100, 100), 40);
            var levelPill = UiArt.Panel(root, "LevelPill", UiArt.PillWhite9s, UiArt.CapsulePpu(26), WithAlpha(UITheme.Ink, 0.85f), WithAlpha(UITheme.Ink, 0.85f));
            levelPill.rectTransform.At(TopLeft, new Vector2(14, -80), new Vector2(88, 26));
            var level = Txt(levelPill.transform, "LevelText", "Lv.1", 20, UITheme.Cream, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            level.rectTransform.Stretch(4, 4, 0, 0);

            var name = Txt(root, "NameText", "친구", 24, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).Ellipsis().FitFloor(UIType.AutoSizeFloor);
            name.rectTransform.At(TopLeft, new Vector2(112, -14), new Vector2(92, 34));
            var stars = Txt(root, "StarsText", UITheme.Stars(1), UIType.Micro, StarInk, TextAlignmentOptions.MidlineLeft);
            stars.rectTransform.At(TopLeft, new Vector2(112, -48), new Vector2(92, 24));
            // 전투력 칸: 아이콘(전투력 칩과 같은 그림) + '+312' — '전투력 +1,234'는 92px에 16px로도 들어가지 않아 낱말 대신 아이콘
            var gainRow = NewRect("Gain", root).At(TopLeft, new Vector2(112, -72), new Vector2(92, 34));
            var gainIcon = UiArt.Icon(gainRow, "PowerIcon", PowerIcon(), MiddleLeft, Vector2.zero, 20);
            if (!UiArt.IsArt(gainIcon.sprite)) gainIcon.color = UITheme.PositiveInk;
            var gain = Txt(gainRow, "GainText", "미편성", UIType.Hint, UITheme.InkSub, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.AutoSizeFloor);
            gain.rectTransform.Stretch();

            var button = StyledButton(root, "LevelUpButton", "레벨업", Tone.Primary, 22, out var buttonLabel);
            ((RectTransform)button.transform).Bottom(10, 74, 10, 10);
            buttonLabel.rectTransform.Top(4, 32, 4, 4);
            var costRow = CenteredRow(button.transform, "Cost", 6);
            costRow.Top(36, 32, 4, 4);
            CurrencyIcon(costRow, CurrencyType.Gold, 26);
            var cost = Txt(costRow, "CostText", "0", 22, UITheme.Text, TextAlignmentOptions.MidlineLeft, true);
            var costView = CostBtn(button, cost, CostSurface.Dark, buttonLabel);
            var hold = button.gameObject.AddComponent<HoldRepeatButton>();
            SetHoldTiming(hold, UIMotion.HoldDelayCard, new Vector2(0, 18));
            BuildLineageLock(button, out var lockPill, out var lockPillText, out var lockHit);

            // 다른 화면에서 데려왔을 때 금빛 테 (GrowthPanel.FocusLineage, 꺼 둠)
            var focus = UiArt.Panel(root, "FocusRing", UiArt.SlotFrame9s, 2f, UITheme.Gold, UITheme.Gold);
            focus.rectTransform.Stretch(-5, -5, -5, -5);
            focus.SetInactive();

            new Binder(view)
                .Set("_portrait", portrait)
                .Set("_nameText", name)
                .Set("_starsText", stars)
                .Set("_levelText", level)
                .Set("_levelUpButton", button)
                .Set("_buttonLabel", buttonLabel)
                .Set("_costText", cost)
                .Set("_costRow", costRow.gameObject)
                .Set("_gainText", gain)
                .Set("_gainIcon", gainIcon.gameObject)
                .Set("_focusRing", focus.gameObject)
                .Set("_costView", costView)
                .Set("_hold", hold)
                .Set("_lockPill", lockPill)
                .Set("_lockPillText", lockPillText)
                .Set("_lockHit", lockHit)
                .Apply();
            return SaveMenuPrefab<GrowthLineageCardView>(card.gameObject, $"{PrefabDir}/GrowthLineageCard.prefab");
        }

        /// <summary>친구 카드 레벨업 잠김 '1-1' 알약 (비용 줄 자리 72x28, Micro 18 Gold · 밤 알약) — F-03, SPEC4 §5.1</summary>
        static readonly Vector2 LineageLockPillSize = new(72, 28);

        /// <summary>
        /// 친구 카드 레벨업 잠김 (F-03): 버튼 안 비용 줄 자리(위에서 38)에 '1-1' 알약 + 버튼 전체를 덮는 투명 누르기 "LockHit" (맨 위, 누르면 잠김 토스트).
        /// 둘 다 꺼 둔다 — GrowthLineageCardView가 친구 레벨업이 잠겼을 때만 켠다. 이름에 'LevelUpButton'을 담지 않는다 (코치가 그 이름으로 찾는다)
        /// </summary>
        static void BuildLineageLock(Button button, out GameObject pill, out TextMeshProUGUI pillText, out Button hit)
        {
            var pillImage = UiArt.Panel(button.transform, "LockPill", UiArt.PillNight9s, UiArt.CapsulePpu(LineageLockPillSize.y),
                WithAlpha(Color.white, 0.95f), WithAlpha(UITheme.Background, 0.9f));
            pillImage.raycastTarget = false;
            pillImage.rectTransform.At(TopCenter, new Vector2(0, -38), LineageLockPillSize);
            pillText = Txt(pillImage.transform, "Text", "1-1", UIType.Micro, UITheme.Gold, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            pillText.rectTransform.Stretch(6, 6, 0, 0);
            pill = pillImage.gameObject;
            pill.SetActive(false);

            var hitImage = Img(button.transform, "LockHit", Color.clear, null, true);
            hitImage.rectTransform.Stretch();
            hit = hitImage.gameObject.AddComponent<Button>();
            hit.transition = Selectable.Transition.None;
            hit.targetGraphic = hitImage;
            hit.navigation = new Navigation { mode = Navigation.Mode.None };
            hitImage.transform.SetAsLastSibling();
            hitImage.gameObject.SetActive(false);
        }

        /// <summary>길게 누르기 시간 · 연속 알약 자리 (실행 중 속성은 빌드 때 쓸 수 없어 직렬화 값으로)</summary>
        static void SetHoldTiming(HoldRepeatButton hold, float delay, Vector2 comboOffset)
        {
            var so = new SerializedObject(hold);
            so.FindProperty("_delay").floatValue = delay;
            so.FindProperty("_comboOffset").vector2Value = comboOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>전투력 아이콘 (ART-UPGRADE §4.3 #11): UiArt.PowerIcon = Icons/power, 없으면 상단 바 전투력 칩과 같은 Icons/guardian, 그것도 없으면 별</summary>
        static Sprite PowerIcon() => UiArt.PowerIcon;

        /// <summary>
        /// 쪽 탭 그림 한 쌍 (SX-2 · ART-UPGRADE §4.3 #9): 켜짐 · 꺼짐 전용 아트가 둘 다 있을 때만(UiArt.HasTabSkin) 탭 바탕 Image에 TabSkin을 붙이고
        /// 꺼짐 그림 · 흰색으로 다시 입힌다 — 그때 TabStyle.Apply(Page)가 색 대신 그림을 바꾼다. 아트가 없으면 아무것도 하지 않는다 (지금처럼 색으로 칠한다).
        /// 설정 탭(SettingsSceneBuilder) · 시너지 도감 탭 · 상점 쪽 탭이 쓴다 (CharacterSceneBuilder.PageTabSkin과 같은 규칙)
        /// </summary>
        internal static void PageTabSkin(Image image, float ppu)
        {
            if (image == null || !UiArt.HasTabSkin) return;
            var skin = image.gameObject.AddComponent<TabSkin>();
            new Binder(skin).Set("_on", UiArt.TabOn9s).Set("_off", UiArt.TabOff9s).Apply();
            UiArt.Skin9(image, UiArt.TabOff9s, ppu, Color.white);
        }

        /// <summary>왼쪽 투명 → 오른쪽 불투명 가로 흐림 (Sprites/UI/Menu/MENU_FadeRight.png, 흰색 + 알파 — 색은 Image.color)</summary>
        static Sprite FadeRightSprite() => Generated("MENU_FadeRight", 64, 4, (u, v) => u * u * (3f - 2f * u));

        const string GeneratedSpriteDir = IdleProjectSetup.Root + "/Sprites/UI/Menu";

        /// <summary>
        /// 코드로 만드는 UI 바탕 그림 (Sprites/UI/Menu/{name}.png, 흰색 + 알파 — 메타 · 캐릭터 빌더와 같은 방식). 파일이 있으면 다시 그리지 않는다.
        /// alpha(u, v)는 0~1 (u = 0이 왼쪽 칸, v = 0이 아래 줄). 가져오기 설정: Sprite, 밉맵 없음, 압축 없음, Clamp
        /// </summary>
        static Sprite Generated(string name, int width, int height, System.Func<float, float, float> alpha)
        {
            string path = $"{GeneratedSpriteDir}/{name}.png";
            if (!System.IO.File.Exists(path))
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
                System.IO.Directory.CreateDirectory(GeneratedSpriteDir);
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
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

        // ═════════════════════════ 편성 탭 ═════════════════════════

        /// <summary>편성 탭 (MainTab.Formation): 미니 전장(방위 링) + 팀 시너지 + 잔향이 덱 + 캐릭터 목록</summary>
        public static GameObject BuildFormationPanel(SceneBuildContext ctx)
        {
            var root = NewRect("FormationPanel", ctx.ContentArea).Stretch();
            var view = root.gameObject.AddComponent<FormationPanel>();

            var guardianSlots = new FormationSlotView[BattleGeometry.SlotCount];
            BuildFormationField(root, guardianSlots, out var guardianCount, out var teamPower);
            var synergyPanel = BuildSynergyPanel(root, out var auto, out var autoPulse);
            var echoSlots = BuildEchoDeck(root, out var echoCount);

            // 캐릭터 목록 (하나의 목록에서 지킴이/잔향이 모두 고른다). 안내 줄은 하나 (Hint 20, M80)
            var listTitle = Txt(root, "ListTitle", "친구 목록", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            listTitle.rectTransform.At(TopLeft, new Vector2(24, -536), new Vector2(240, 36));
            var hint = Txt(root, "HintText", string.Empty, UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineRight).Ellipsis().FitFloor(UIType.HintMin);
            hint.rectTransform.At(TopRight, new Vector2(-24, -536), new Vector2(800, 36));
            var listRoot = NewRect("CardList", root).Top(572, 208);
            var cardContent = HorizontalScroll(listRoot, 10, new RectOffset(16, 16, 0, 0), out var cardScroll);
            var empty = Txt(listRoot, "EmptyLabel", "편성할 친구가 없어요. 소환에서 친구를 만나 보세요.", 24, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.SetInactive();
            // 칸에서 뺀 직후 잠깐 안내 줄 자리에 뜨는 "되돌리기" (300x52): 누르는 자리 300x88 (M22 — 위아래 18씩: 위는 잔향이 덱 아래 끝 쪽으로,
            // 아래는 목록 위 끝 18 — 카드는 목록 안에서 6 내려 앉아 실제로 가리는 카드 끝은 12, 버튼이 보이는 몇 초 동안만).
            // 목록보다 뒤에 만들어 그 18을 정말 받는다 (목록 Viewport가 누름을 받는 판이라 앞에 두면 아래쪽 넓힘이 막힌다). 끌기 고스트 · 차단 안내는 그대로 맨 위
            var undo = StyledButton(root, "UndoButton", "되돌리기", Tone.Secondary, 22, out var undoText);
            ((RectTransform)undo.transform).At(TopRight, new Vector2(-24, -FormationUndoTop), FormationUndoSize);
            undoText.Ellipsis().FitFloor(UIType.Micro);
            undo.targetGraphic.HitPad(FormationUndoSize, 0f, UISize.MinHit, 0f, 0f, FormationUndoPad, FormationUndoPad);
            undo.SetInactive();

            // 드래그 고스트 / 동시 등록 차단 안내 (맨 위)
            var ghost = NewRect("DragGhost", root).At(Center, Vector2.zero, new Vector2(150, 150));
            var ghostGroup = ghost.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.interactable = false;
            ghostGroup.alpha = 0.92f;
            Glow(ghost, "Glow", WithAlpha(UITheme.Lamp, 0.6f), Center, Vector2.zero, new Vector2(220, 220));
            var ghostPortrait = Portrait(ghost, "Portrait", Center, Vector2.zero, new Vector2(140, 140), 48);
            var ghostTag = Pill(ghost, "SynergyTag", "시너지 +1", 20, UITheme.Gold, UITheme.Ink, TopCenter, new Vector2(0, 30), new Vector2(150, 36));
            var ghostTagText = ghostTag.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            ghostTag.SetInactive();
            // 놓을 수 있는 칸 위에서 '전투력 +312' / '-80' (M46): 시너지 표시 8px 아래, 어두운 알약 + Positive/Negative 24 굵게
            var ghostPower = Pill(ghost, "PowerTag", "전투력 +0", 24, WithAlpha(UITheme.Background, 0.85f), UITheme.Positive, TopCenter,
                new Vector2(0, 30 - 36 - 8), GhostPowerSize);
            var ghostPowerText = ghostPower.transform.GetChild(0).GetComponent<TextMeshProUGUI>().FitFloor(UIType.Micro);
            ghostPower.SetInactive();
            ghost.SetInactive();
            var callout = BuildBlockedCallout(root);

            new Binder(view)
                .SetArray("_guardianSlots", guardianSlots)
                .SetArray("_echoSlots", echoSlots)
                .Set("_guardianCountText", guardianCount)
                .Set("_echoCountText", echoCount)
                .Set("_teamPowerText", teamPower)
                .Set("_synergyPanel", synergyPanel)
                .Set("_autoButton", auto)
                .Set("_autoPulse", autoPulse)
                .Set("_ghostPowerTag", ghostPower)
                .Set("_ghostPowerText", ghostPowerText)
                .Set("_cardScroll", cardScroll)
                .Set("_cardContent", cardContent)
                .Set("_cardPrefab", BuildFormationCardPrefab())
                .Set("_listTitleText", listTitle)
                .Set("_hintText", hint)
                .Set("_emptyLabel", empty.gameObject)
                .Set("_dragLayer", root)
                .Set("_dragGhost", ghost)
                .Set("_dragGhostPortrait", ghostPortrait)
                .Set("_ghostSynergyTag", ghostTag)
                .Set("_ghostSynergyText", ghostTagText)
                .Set("_callout", callout)
                .Set("_undoButton", undo)
                .Set("_undoText", undoText)
                .Apply();
            FitButtonBorders(root);
            root.SetInactive();
            return root.gameObject;
        }

        /// <summary>편성 '되돌리기' 위 끝 (52 높이로 572 목록 바로 위에서 끝난다)</summary>
        const float FormationUndoTop = 520f;
        /// <summary>편성 '되돌리기' 그림 300x52 · 누르는 자리 위아래 넓힘 (18 + 52 + 18 = 88, M22)</summary>
        static readonly Vector2 FormationUndoSize = new(300, 52);
        const float FormationUndoPad = 18f;
        /// <summary>끄는 중 '전투력 +312' 알약 (24px '전투력 +1,234'가 자동 크기로 들어가게 160 — 명세 120은 글이 넘친다)</summary>
        static readonly Vector2 GhostPowerSize = new(160, 40);
        /// <summary>미니 전장 오른쪽 위 '지킴이 3/5 · [아이콘] 1,226' 알약 (M46)</summary>
        static readonly Vector2 FieldPillSize = new(292, 40);

        /// <summary>미니 전장: 수선점 + 방위 링 5칸 + 칸별 요격 범위 부채꼴 레이어 + 지킴이 수 · 팀 전투력 알약</summary>
        static void BuildFormationField(RectTransform root, FormationSlotView[] slots, out TextMeshProUGUI countText, out TextMeshProUGUI powerText)
        {
            var field = Img(root, "Field", UITheme.Ground, RoundSprite);
            var rt = field.rectTransform.At(TopLeft, new Vector2(12, -8), FieldSize);
            field.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var fieldArt = ArtLib.Battle("field");
            if (fieldArt != null)
            {
                // 1080x810 바닥 그림을 비율 그대로 깔고 위아래를 잘라낸다
                Img(rt, "FieldArt", new Color(0.82f, 0.8f, 0.92f), fieldArt).rectTransform
                    .At(Center, Vector2.zero, new Vector2(FieldSize.x, FieldSize.x * 0.75f));
            }
            Glow(rt, "GroundGlow", WithAlpha(UITheme.Lavender, 0.18f), Center, RingCenter, new Vector2(RingRadiusX * 2 + 160, RingRadiusY * 2 + 120));

            // 방위 링: 흰 실 고리(FX/fx_ring_thread)를 금빛으로. 실 중심선(반지름의 0.8418)이 칸 위치를 지나도록 키우고 세로를 눌러 원근을 맞춘다
            var ringArt = UiArt.RingThread;
            bool threadRing = ringArt != null && ringArt.name == "fx_ring_thread";
            var ring = Img(rt, "Ring", UiArt.IsArt(ringArt) ? WithAlpha(UITheme.GoldAccent, threadRing ? 0.85f : 0.75f) : WithAlpha(UITheme.Lamp, 0.1f), ringArt);
            ring.rectTransform.At(Center, RingCenter, threadRing
                ? new Vector2(RingRadiusX * 2f / UiArt.RingThreadRadius, RingRadiusY * 2f / UiArt.RingThreadRadius)
                : new Vector2(RingRadiusX * 2 + 30, RingRadiusY * 2 + 20));

            // 요격 범위: 수선점 중심 부채꼴. 레이어 너비 = 링 반지름 x2, 세로를 눌러 원근을 맞춘다
            var fanLayer = NewRect("FanLayer", rt).At(Center, RingCenter, new Vector2(RingRadiusX * 2, RingRadiusX * 2));
            fanLayer.localScale = new Vector3(1f, RingRadiusY / RingRadiusX, 1f);
            var fans = new Image[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var fan = Img(fanLayer, $"Fan_{i}", WithAlpha(UITheme.SlotColor(i), 0.2f), SoftCircle());
                fan.type = Image.Type.Filled;
                fan.fillMethod = Image.FillMethod.Radial360;
                fan.fillOrigin = (int)Image.Origin360.Top;
                fan.fillClockwise = true;
                fan.fillAmount = 0.2f;
                fan.rectTransform.At(Center, Vector2.zero, new Vector2(320, 320));
                fan.SetInactive();
                fans[i] = fan;
            }

            // 수선점
            Glow(rt, "ShopGlow", WithAlpha(UITheme.Lamp, 0.5f), Center, RingCenter + new Vector2(0, 14), new Vector2(240, 230));
            ShopArt(rt, Center, RingCenter + new Vector2(0, 6), 146);
            Pill(rt, "ShopLabel", "수선점", 20, WithAlpha(UITheme.Background, 0.82f), UITheme.Lamp, Center, RingCenter + new Vector2(0, -72), new Vector2(110, 34));

            // 방위 링 5칸 (슬롯 0 = 위, 반시계 72°)
            for (int i = 0; i < slots.Length; i++)
            {
                var direction = BattleGeometry.GuardianPosition(i);
                var position = RingCenter + new Vector2(direction.x * RingRadiusX, direction.y * RingRadiusY);
                slots[i] = BuildGuardianSlot(rt, i, position, fans[i]);
            }

            // 미니 전장 테두리: 금 테두리 덮개 (가운데는 비어 있다, 라벤더를 살짝)
            UiArt.Panel(rt, "FieldFrame", UiArt.CardFrame9s, 2f, Color.Lerp(Color.white, UITheme.Lavender, 0.3f), WithAlpha(UITheme.Divider, 0f))
                .rectTransform.Stretch();

            Pill(rt, "RingTag", "방위 링 · 지킴이", 20, WithAlpha(UITheme.PanelLight, 0.88f), UITheme.Cream, TopLeft, new Vector2(14, -12), new Vector2(184, 36));

            // 지킴이 수 + 팀 전투력 (M46): 어두운 알약 '지킴이 3/5 · [아이콘 24] 1,226' — 숫자 26 Cream 굵게 (⚔ 글자는 글꼴에 없어 그림)
            var pill = UiArt.Panel(rt, "GuardianCount", UiArt.PillNight9s, UiArt.CapsulePpu(FieldPillSize.y), WithAlpha(Color.white, 0.85f),
                WithAlpha(UITheme.Background, 0.75f));
            pill.rectTransform.At(TopRight, new Vector2(-14, -12), FieldPillSize);
            countText = Txt(pill.transform, "Text", "지킴이 0/5", 20, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.AutoSizeFloor);
            countText.rectTransform.At(MiddleLeft, new Vector2(16, 0), new Vector2(116, 36));
            Txt(pill.transform, "Dot", "·", 22, UITheme.SubText, TextAlignmentOptions.Center, true).rectTransform.At(MiddleLeft, new Vector2(132, 0), new Vector2(16, 36));
            UiArt.Icon(pill.transform, "PowerIcon", PowerIcon(), MiddleLeft, new Vector2(150, 0), 24);
            powerText = Txt(pill.transform, "PowerText", "0", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Micro);
            powerText.rectTransform.At(MiddleLeft, new Vector2(178, 0), new Vector2(102, 36));
        }

        static FormationSlotView BuildGuardianSlot(RectTransform field, int index, Vector2 position, Image fan)
        {
            var color = UITheme.SlotColor(index);
            var root = NewRect($"GuardianSlot_{index}", field).At(Center, position, new Vector2(116, 116));
            var view = root.gameObject.AddComponent<FormationSlotView>();

            var highlight = Glow(root, "Highlight", color, Center, new Vector2(0, -6), new Vector2(190, 160));
            highlight.gameObject.AddComponent<MenuGlowPulse>();
            highlight.SetInactive();
            // CR-01 칸 반짝 금빛 테 (SPEC4 §7.2 — FormationSlotView.PulseSlot이 2초 켠다, 받침 · 초상화 뒤, 누름 없음, 꺼 둠)
            var pulseRing = Img(root, "PulseRing", UITheme.Gold, UiArt.RingStitch);
            pulseRing.rectTransform.At(Center, GuardianPulseRingPosition, new Vector2(GuardianPulseRingSize, GuardianPulseRingSize));
            pulseRing.SetInactive();
            Img(root, "Hit", Color.clear, null, true).rectTransform.Stretch(-8, -8, -16, -8);

            // 방위 색 받침 그림 (Battle/guardian_pad_{색}, 흰색 그대로). 없으면 공용 받침에 방위 색을 입힌다
            var colorPad = ArtLib.Battle($"guardian_pad_{PadColorNames[index % PadColorNames.Length]}");
            var padArt = colorPad != null ? colorPad : ArtLib.Battle("guardian_pad");
            if (padArt != null)
            {
                var pad = Img(root, "Pad", colorPad != null ? Color.white : color, padArt);
                pad.preserveAspect = true;
                pad.rectTransform.At(Center, new Vector2(0, -12), new Vector2(124, 124));
            }
            else
            {
                // 타원 받침: 바깥은 방위 색, 안쪽은 어두운 면
                var pad = Img(root, "Pad", color, CircleSprite);
                pad.rectTransform.At(Center, new Vector2(0, -16), new Vector2(112, 66));
                Img(pad.transform, "Inner", WithAlpha(UITheme.Background, 0.6f), CircleSprite).rectTransform.Stretch(8, 8, 6, 8);
            }

            var plus = EmptyMark(root, new Vector2(0, 2), 54, color, 60);
            var portrait = Portrait(root, "Portrait", BottomCenter, new Vector2(0, 34), new Vector2(116, 116), 42);
            portrait.SetInactive();

            var nameBox = UiArt.Panel(root, "NameBox", UiArt.PillNight9s, UiArt.CapsulePpu(30), null, WithAlpha(UITheme.Background, 0.82f));
            nameBox.rectTransform.At(BottomCenter, new Vector2(0, -10), new Vector2(132, 30));
            var name = Fit(Txt(nameBox.transform, "NameText", string.Empty, 19, color, TextAlignmentOptions.Center, true).Ellipsis(), UIType.AutoSizeFloor);
            name.rectTransform.Stretch(6, 6, 0, 0);
            nameBox.SetInactive();

            new Binder(view)
                .SetInt("_role", (int)CharacterRole.Guardian)
                .SetInt("_index", index)
                .Set("_portrait", portrait)
                .Set("_emptyMark", plus)
                .Set("_nameBox", nameBox.gameObject)
                .Set("_nameText", name)
                .Set("_highlight", highlight)
                .Set("_fan", fan)
                .Set("_pulseRing", pulseRing)
                .Apply();
            return view;
        }

        /// <summary>CR-01 칸 반짝 금빛 테 (바느질 고리 UiArt.RingStitch, Gold): 지름 150 — 받침(124)을 13씩 두르고, 칸 가운데보다 8 아래 (받침 쪽)</summary>
        const float GuardianPulseRingSize = 150f;
        static readonly Vector2 GuardianPulseRingPosition = new(0, -8);

        // 팀 시너지 칸 404x386 (편성 패널 (664, −8)) (M44 · M22): 제목 줄 0~80 (도감 버튼 그림 11~61, 누르는 자리는 ContentArea 위 끝 −8 ~ 목록 위 끝 80 = 88,
        // 구분선 70) · 목록 80~290 (52 줄 4개) · 규칙 한 줄 290~312 (Bottom 74) · 자동 편성 368x60 Bottom 10 (누르는 자리 위아래 14 = 88).
        // 명세는 목록 72 · 자동 편성 368x64 Bottom 12였지만 도감 버튼 누르는 자리가 66에 그쳐 목록을 8 내리고 자동 편성을 4 낮췄다
        static readonly Vector2 SynergyCodexButtonSize = new(150, 50);
        /// <summary>도감 버튼 위 끝 (칸 안) · 칸 위 끝에서 ContentArea 위 끝까지 (위로 넓힐 수 있는 칸 바깥 몫)</summary>
        const float SynergyCodexTop = 11f;
        const float SynergyPanelTopGap = 8f;
        const float SynergyDividerTop = 70f;
        const float SynergyListTop = 80f;
        const float SynergyListHeight = 210f;
        const float SynergyRuleBottom = 74f;
        /// <summary>시너지 줄 376x52 (4줄이 210 목록에 꼭 맞게 — 명세 56은 규칙 줄 · 자동 편성과 함께 4줄이 들어가지 않는다)</summary>
        static readonly Vector2 SynergyRowSize = new(376, 52);
        static readonly Vector2 SynergyAutoSize = new(368, 60);
        const float SynergyAutoBottom = 10f;

        /// <summary>
        /// 오른쪽 팀 시너지 칸 (이름 TeamSynergy 그대로): 켜진 시너지 목록(드래그 미리보기·관여 강조) / 시너지 도감 버튼 / 편성 규칙 / 자동 편성.
        /// 미리보기 중에는 칸 뒤의 금빛 테두리가 숨 쉬듯 빛난다. 자동 편성 뒤 빛(AutoPulse)은 자동 편성 이득이 클 때만 (FormationPanel).
        /// 경로 FormationPanel/TeamSynergy/AutoButton은 FocusTarget 대상 (C-05) — 이름 · 부모 그대로.
        /// </summary>
        static SynergyPanelView BuildSynergyPanel(RectTransform root, out Button auto, out GameObject autoPulse)
        {
            var position = new Vector2(664, -SynergyPanelTopGap);
            var size = new Vector2(404, 386);
            // 칸 뒤 금빛 테두리 (칸보다 먼저 만들어 뒤에 그린다)
            // 흰 칸 틀(테두리만)을 불빛 색으로 — 숨 쉬듯 빛나는 금빛 테두리
            var previewFrame = UiArt.Panel(root, "SynergyPreviewFrame", UiArt.SlotFrame9s, 1.6f, UITheme.Lamp, UITheme.Lamp);
            previewFrame.rectTransform.At(TopLeft, position + new Vector2(-6, 6), size + new Vector2(12, 12));
            Pulse(previewFrame, 0f, 0.35f, 1f);
            previewFrame.SetInactive();

            var panel = NightPanel(root, "TeamSynergy");
            var rt = panel.rectTransform.At(TopLeft, position, size);
            var view = panel.gameObject.AddComponent<SynergyPanelView>();

            var title = Txt(rt, "Title", "팀 시너지", 27, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.HintMin);
            title.rectTransform.At(TopLeft, new Vector2(22, -14), new Vector2(210, 42));
            var codex = StyledButton(rt, "CodexButton", "시너지 도감", Tone.Secondary, 19, out var codexLabel);
            ((RectTransform)codex.transform).At(TopRight, new Vector2(-16, -SynergyCodexTop), SynergyCodexButtonSize);
            codexLabel.FitFloor(UIType.AutoSizeFloor);
            // 누르는 자리 88 (M22): 위로 19 = ContentArea 위 끝까지 (칸 위 8은 비어 있다 — 시트 머리로는 넘지 않는다),
            // 아래로 19 = 목록 위 끝(80)까지 — 구분선(누르기 없음)은 덮어도 목록은 덮지 않는다
            codex.targetGraphic.HitPad(SynergyCodexButtonSize, 0f, UISize.MinHit, 0f, 0f,
                SynergyListTop - (SynergyCodexTop + SynergyCodexButtonSize.y), SynergyCodexTop + SynergyPanelTopGap);
            Img(rt, "Divider", WithAlpha(UITheme.GoldAccent, 0.4f)).rectTransform.Top(SynergyDividerTop, 2, 20, 20);

            var listRoot = NewRect("SynergyList", rt).Top(SynergyListTop, SynergyListHeight, 12, 12);
            var content = ScrollList(listRoot, out var scroll);
            AddVerticalList(content, 0, new RectOffset(2, 2, 1, 1));
            var empty = Txt(listRoot, "EmptyText", string.Empty, UIType.Hint, UITheme.SubText, TextAlignmentOptions.Center, false, true).FitFloor(UIType.AutoSizeFloor);
            empty.rectTransform.Stretch(10, 10, 6, 6);
            empty.SetInactive();

            // 자동 편성 뒤 숨쉬는 빛 (꺼 둠 — 자동 편성 이득이 팀 전투력의 5%를 넘을 때만). 규칙 글보다 먼저 = 뒤에 그린다
            // 자동 편성 가운데(아래에서 40)에 맞춘다
            var pulse = Glow(rt, "AutoPulse", WithAlpha(UITheme.Lamp, 0.75f), BottomCenter, new Vector2(0, -10), new Vector2(440, 100));
            Pulse(pulse, 0.02f, 0.25f, 0.8f);
            pulse.SetInactive();
            autoPulse = pulse.gameObject;

            // 규칙 한 줄 (자세한 '(지킴이 1 + 잔향이 1)'은 시너지 도감 머리로) — 18 고정 (22 칸에 줄 높이가 넘쳐도 글자는 칸 안에 앉는다)
            string gold = UITheme.ColorTag(UITheme.GoldAccent);
            Txt(rt, "RuleText", $"<color={gold}>규칙</color> · 같은 계보 최대 2종", UIType.Micro, UITheme.SubText)
                .rectTransform.Bottom(SynergyRuleBottom, 22, 16, 16);

            // 누르는 자리 88: 위아래 14 — 위는 규칙 글(누르기 없음)까지, 아래는 칸 밖 4 (잔향이 덱 위 끝 400보다 2 위에서 멈춘다)
            auto = StyledButton(rt, "AutoButton", "자동 편성", Tone.Primary, 26, out _);
            ((RectTransform)auto.transform).At(BottomCenter, new Vector2(0, SynergyAutoBottom), SynergyAutoSize);
            auto.targetGraphic.HitPad(SynergyAutoSize, 0f, UISize.MinHit, 0f, 0f, 14f, 14f);

            new Binder(view)
                .Set("_titleText", title)
                .Set("_codexButton", codex)
                .Set("_scroll", scroll)
                .Set("_content", content)
                .Set("_rowPrefab", BuildSynergyRowPrefab())
                .Set("_emptyText", empty)
                .Set("_previewGlow", previewFrame.gameObject)
                .Apply();
            return view;
        }

        /// <summary>잔향이 덱: 6칸 (마지막 칸은 조건 충족 시 개방)</summary>
        static FormationSlotView[] BuildEchoDeck(RectTransform root, out TextMeshProUGUI echoCount)
        {
            var deck = NewRect("EchoDeck", root).Top(400, 132, 12, 12);
            NightPanel(deck, "Bg").rectTransform.Stretch();
            Txt(deck, "DeckTitle", "잔향이 덱", 28, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.At(TopLeft, new Vector2(20, -14), new Vector2(170, 40));
            // 잔향이 수 24 (예전 안내 줄 '전투 중 잔향으로 소환'은 지웠다 — 패널마다 안내는 한 줄, M80)
            echoCount = Txt(deck, "EchoCount", "잔향이 0/5", UIType.Label, UITheme.Text, TextAlignmentOptions.MidlineLeft);
            echoCount.rectTransform.At(TopLeft, new Vector2(20, -58), new Vector2(170, 36));

            var slots = new FormationSlotView[FormationSave.EchoSlots];
            for (int i = 0; i < slots.Length; i++) slots[i] = BuildEchoSlot(deck, i, new Vector2(196 + i * 142, -7));
            return slots;
        }

        static FormationSlotView BuildEchoSlot(RectTransform deck, int index, Vector2 position)
        {
            var root = NewRect($"EchoSlot_{index}", deck).At(TopLeft, position, new Vector2(130, 118));
            var view = root.gameObject.AddComponent<FormationSlotView>();

            var highlight = Glow(root, "Highlight", UITheme.EchoGlow, Center, Vector2.zero, new Vector2(196, 176));
            highlight.gameObject.AddComponent<MenuGlowPulse>();
            highlight.SetInactive();

            // 안쪽 = 보라 바느질 판 (9-slice — 3:4 빈 카드 그림은 이 칸 비율에서 잘리거나 늘어나서 쓰지 않는다),
            // 그 위에 금 테두리 덮개 (가운데가 비어 있다, 라벤더를 살짝)
            UiArt.Panel(root, "Inner", UiArt.PanelInset9s, 2f, null, NightInner).rectTransform.Stretch(3, 3, 3, 3);
            var frame = UiArt.Panel(root, "Frame", UiArt.CardFrame9s, 2f, Color.Lerp(Color.white, UITheme.Lavender, 0.3f), UITheme.Secondary, true);
            frame.rectTransform.Stretch();
            Glow(root, "EchoGlow", WithAlpha(UITheme.EchoGlow, 0.4f), Center, new Vector2(0, 8), new Vector2(124, 112));

            var plus = EmptyMark(root, new Vector2(0, 4), 50, WithAlpha(UITheme.EchoGlow, 0.85f), 52);
            var portrait = Portrait(root, "Portrait", BottomCenter, new Vector2(0, 8), new Vector2(104, 104), 38);
            portrait.SetInactive();

            var nameBox = UiArt.Panel(root, "NameBox", UiArt.PillNight9s, UiArt.CapsulePpu(26), null, WithAlpha(UITheme.Background, 0.82f));
            nameBox.rectTransform.Bottom(4, 26, 6, 6);
            var name = Fit(Txt(nameBox.transform, "NameText", string.Empty, UIType.Micro, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis(), UIType.AutoSizeFloor);
            name.rectTransform.Stretch(4, 4, 0, 0);
            nameBox.SetInactive();

            // 소환 비용 (잔향)
            var resonanceArt = ArtLib.Icon("resonance");
            var cost = Img(root, "CostBadge", resonanceArt != null ? Color.white : UITheme.Resonance, resonanceArt != null ? resonanceArt : UiArt.PipButton);
            cost.preserveAspect = true;
            cost.rectTransform.At(TopLeft, new Vector2(-8, 8), new Vector2(42, 42));
            var costText = Txt(cost.transform, "CostText", "0", 22, Color.white, TextAlignmentOptions.Center, true);
            costText.rectTransform.Stretch();
            cost.SetInactive();

            // 잠긴 칸
            var lockGroup = NewRect("Lock", root).Stretch();
            // 밤 패널 덮개 (금 테두리 장식은 덮지 않게 안쪽으로)
            UiArt.Panel(lockGroup, "Shade", UiArt.PanelNight9s, 2.4f, WithAlpha(Color.white, 0.92f), WithAlpha(UITheme.Background, 0.78f))
                .rectTransform.Stretch(7, 7, 7, 7);
            // 잠금 안내 (M28 · M39): '스테이지 2-9 클리어 시 / 열려요' — 130 칸 안에서 세 줄로 접혀 18~22 자동 크기 (줄 간격 조금 좁게)
            LockIcon(lockGroup, TopCenter, new Vector2(0, -8), 30, UITheme.SubText);
            var lockText = Txt(lockGroup, "Hint", string.Empty, 22, UITheme.SubText, TextAlignmentOptions.Center, false, true).FitFloor(UIType.Micro, 22f);
            lockText.lineSpacing = -15f;
            lockText.rectTransform.Bottom(6, 72, 6, 6);
            lockGroup.SetInactive();

            new Binder(view)
                .SetInt("_role", (int)CharacterRole.Echo)
                .SetInt("_index", index)
                .Set("_portrait", portrait)
                .Set("_emptyMark", plus)
                .Set("_nameBox", nameBox.gameObject)
                .Set("_nameText", name)
                .Set("_costBadge", cost.gameObject)
                .Set("_costText", costText)
                .Set("_lockGroup", lockGroup.gameObject)
                .Set("_lockText", lockText)
                .Set("_highlight", highlight)
                .Apply();
            return view;
        }

        static FormationCallout BuildBlockedCallout(RectTransform root)
        {
            var window = NightPanel(root, "BlockedCallout", true);
            var rt = window.rectTransform.At(TopLeft, new Vector2(170, -380), new Vector2(560, 156));
            window.gameObject.AddComponent<CanvasGroup>();
            var view = window.gameObject.AddComponent<FormationCallout>();

            UiArt.Panel(rt, "Accent", UiArt.PillWhite9s, 12f, UITheme.Negative, UITheme.Negative).rectTransform.At(MiddleLeft, new Vector2(12, 0), new Vector2(8, 116));
            var portraitBg = UiArt.Icon(rt, "PortraitBg", UiArt.PlateRound, MiddleLeft, new Vector2(30, 0), 104);
            portraitBg.color = UiArt.IsArt(portraitBg.sprite) ? Color.Lerp(Color.white, UITheme.Negative, 0.3f) : WithAlpha(UITheme.Negative, 0.25f);
            var portrait = Portrait(rt, "Portrait", MiddleLeft, new Vector2(26, 4), new Vector2(112, 112), 40);
            var title = Txt(rt, "Title", "동시 등록 차단", 26, UITheme.Negative, TextAlignmentOptions.MidlineLeft, true);
            title.rectTransform.At(TopLeft, new Vector2(152, -16), new Vector2(390, 36));
            var body = Fit(Txt(rt, "Body", string.Empty, 20, UITheme.Text, TextAlignmentOptions.TopLeft, false, true), UIType.AutoSizeFloor);
            body.rectTransform.At(TopLeft, new Vector2(152, -56), new Vector2(390, 88));

            new Binder(view).Set("_portrait", portrait).Set("_titleText", title).Set("_bodyText", body).Apply();
            window.SetInactive();
            return view;
        }

        /// <summary>편성 카드 152x196 (M45) · 특성 칩 58x26 (명세 50은 두 글자 특성 '금속' · '소리'가 16px로도 들어가지 않는다)</summary>
        static readonly Vector2 FormationCardSize = new(152, 196);
        static readonly Vector2 TraitChipSize = new(58, 26);

        /// <summary>
        /// 편성 목록 카드 (152x196, M45): 초상화 100 · 특성 칩 2개(왼쪽 위) · 'Lv.N' 알약(오른쪽 위) · 그 아래 역할 점('지'/'잔', 편성 밖일 때) 또는 '원정 중' 알약 ·
        /// 상태 띠(편성됐을 때만 — 코치가 Body/StateBand로 편성 안 된 카드를 고른다) · 이름 22 · 별 18 · 성향 칩 · 형태 전투력(아이콘 18 + 숫자 18).
        /// 원정 중이어도 흐리게 덮지 않는다 (M05). 이름 글자 경로 Body/NameText는 그대로 (CoachTargetResolver.FindCardByLineage).
        /// </summary>
        static FormationCardView BuildFormationCardPrefab()
        {
            var root = NewRect("FormationCard", null);
            root.sizeDelta = FormationCardSize;
            var view = root.gameObject.AddComponent<FormationCardView>();
            var body = NewRect("Body", root).Stretch();

            var selected = Glow(body, "SelectedGlow", WithAlpha(UITheme.Lamp, 0.9f), Center, Vector2.zero, new Vector2(204, 248));
            selected.gameObject.AddComponent<MenuGlowPulse>();
            selected.SetInactive();
            // 크림 양피지 몸통 (누르는 자리) → 초상화 배경(수선점 안, 둥근 마스크 안에 비율대로 덮기) → 금 테두리 덮개(등급 색 60%)
            Card(body, "Inner", true).rectTransform.Stretch();
            var portraitBg = PortraitBackdrop(body, "PortraitBg", out var portraitMask);
            portraitMask.Top(8, 100, 8, 8);
            var frame = UiArt.Panel(body, "Frame", UiArt.CardFrame9s, 2f, Color.Lerp(Color.white, UITheme.GradeColor(ItemGrade.Common), 0.6f),
                UITheme.GradeColor(ItemGrade.Common));
            frame.rectTransform.Stretch();
            var portrait = Portrait(body, "Portrait", TopCenter, new Vector2(0, -6), new Vector2(100, 100), 40);
            // 계보 특성 칩 (왼쪽 위 세로로 2개)
            var traitChips = new Object[2];
            for (int i = 0; i < traitChips.Length; i++) traitChips[i] = BuildTraitChip(body, i, new Vector2(6, -6 - i * 28));

            // 오른쪽 위: 'Lv.N' 알약 → 그 아래 역할 점 (편성 밖, 추천 역할) / '원정 중' 알약 (같은 자리, 원정 중이면 점 대신)
            var levelPill = UiArt.Panel(body, "LevelPill", UiArt.PillWhite9s, UiArt.CapsulePpu(26), WithAlpha(UITheme.Background, 0.8f),
                WithAlpha(UITheme.Background, 0.8f));
            levelPill.rectTransform.At(TopRight, new Vector2(-6, -6), new Vector2(58, 26));
            var levelText = Txt(levelPill.transform, "Text", "Lv.1", UIType.Micro, UITheme.Cream, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            levelText.rectTransform.Stretch(3, 3, 0, 0);
            var roleDot = Img(body, "RoleDot", UITheme.Guardian, CircleSprite);
            roleDot.rectTransform.At(TopRight, new Vector2(-6, -36), new Vector2(26, 26));
            var roleDotText = Txt(roleDot.transform, "Text", "지", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true);
            roleDotText.rectTransform.Stretch();
            // 76 폭 (x 70~146): 둘째 특성 칩(TraitChip_1, x 6~64)과 6 띄운다 — '원정 중' 18px은 안쪽 68에 들어간다
            var expedition = UiArt.Panel(body, "ExpeditionPill", UiArt.PillWhite9s, UiArt.CapsulePpu(26), UITheme.DustyPink, UITheme.DustyPink);
            expedition.rectTransform.At(TopRight, new Vector2(-6, -36), new Vector2(76, 26));
            Txt(expedition.transform, "Text", "원정 중", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor)
                .rectTransform.Stretch(4, 4, 0, 0);
            expedition.SetInactive();

            var band = UiArt.Panel(body, "StateBand", UiArt.PillWhite9s, UiArt.CapsulePpu(28), UITheme.Guardian);
            band.rectTransform.At(TopCenter, new Vector2(0, -86), new Vector2(140, 28));
            var stateText = Txt(band.transform, "StateText", "지킴이 배치 중", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            stateText.rectTransform.Stretch(4, 4, 0, 0);
            band.SetInactive();

            var name = Txt(body, "NameText", "친구", 22, UITheme.Ink, TextAlignmentOptions.Center, true).Ellipsis().FitFloor(UIType.AutoSizeFloor);
            name.rectTransform.Top(116, 28, 6, 6);
            var stars = Txt(body, "StarsText", UITheme.Stars(1), UIType.Micro, StarInk);
            stars.rectTransform.Top(144, 18, 6, 6);

            // 아래 줄 (y 10~36): 성향 칩 50 (왼쪽, x 8~58) · 형태 전투력 72 (오른쪽, x 60~132, 아이콘 18 — 상단 바 전투력 칩과 같은 그림).
            // 카드 틀(CardFrame9s)의 오른쪽 아래 모서리 장식이 안쪽 ≈19px까지 들어오므로 숫자 오른쪽 끝은 132에서 멈춘다 —
            // 그 너머의 Ink 숫자는 보라 틀 위에서 보이지 않는다 ('321'이 '32'로 읽혔다). 숫자 칸 53 = '1,234'가 18 굵게 그대로 들어간다
            var styleChip = UiArt.Panel(body, "StyleChip", UiArt.PillWhite9s, UiArt.CapsulePpu(26), UITheme.StyleColor(CombatStyle.Defense));
            styleChip.rectTransform.At(BottomLeft, new Vector2(8, 10), new Vector2(50, 26));
            var styleText = Txt(styleChip.transform, "StyleText", "방어", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            styleText.rectTransform.Stretch(2, 2, 0, 0);
            var powerRow = NewRect("Power", body).At(BottomRight, new Vector2(-20, 10), new Vector2(72, 26));
            var powerIcon = UiArt.Icon(powerRow, "PowerIcon", PowerIcon(), MiddleLeft, Vector2.zero, 18);
            if (!UiArt.IsArt(powerIcon.sprite)) powerIcon.color = UITheme.Ink;
            var power = Txt(powerRow, "PowerText", "0", UIType.Micro, UITheme.Ink, TextAlignmentOptions.MidlineRight, true).FitFloor(UIType.AutoSizeFloor);
            power.rectTransform.Stretch(19, 0, 0, 0);

            // 다른 화면에서 데려왔을 때 금빛 테 (FormationPanel.PulseRosterCard, 꺼 둠)
            var pulse = UiArt.Panel(body, "PulseRing", UiArt.SlotFrame9s, 2f, UITheme.Gold, UITheme.Gold);
            pulse.rectTransform.Stretch(-5, -5, -5, -5);
            pulse.SetInactive();

            new Binder(view)
                .Set("_body", body)
                .Set("_frame", frame)
                .Set("_portraitBg", portraitBg)
                .Set("_portrait", portrait)
                .Set("_nameText", name)
                .Set("_starsText", stars)
                .Set("_styleChip", styleChip)
                .Set("_styleText", styleText)
                .Set("_stateBand", band.gameObject)
                .Set("_stateBandImage", band)
                .Set("_stateText", stateText)
                .Set("_selectedGlow", selected.gameObject)
                .SetArray("_traitChips", traitChips)
                .Set("_levelText", levelText)
                .Set("_roleDot", roleDot)
                .Set("_roleDotText", roleDotText)
                .Set("_expeditionPill", expedition.gameObject)
                .Set("_powerText", power)
                .Set("_pulseRing", pulse.gameObject)
                .Apply();
            return SaveMenuPrefab<FormationCardView>(root.gameObject, $"{PrefabDir}/FormationCard.prefab");
        }

        /// <summary>편성 카드 특성 칩 58x26: [아이콘 20] 천 (18 굵게 Ink, 최소 16). 켜진 특성 시너지면 뒤의 금빛 테두리가 보인다.</summary>
        static TraitChipView BuildTraitChip(Transform parent, int index, Vector2 position)
        {
            var root = NewRect($"TraitChip_{index}", parent).At(TopLeft, position, TraitChipSize);
            var view = root.gameObject.AddComponent<TraitChipView>();
            // 흰 알약 그림에 특성 색 / 켜진 특성은 뒤에 금빛 알약 테두리
            var lit = UiArt.Panel(root, "LitRing", UiArt.PillWhite9s, UiArt.CapsulePpu(TraitChipSize.y + 6), UITheme.Gold);
            lit.rectTransform.Stretch(-3, -3, -3, -3);
            lit.SetInactive();
            var bg = UiArt.Panel(root, "Bg", UiArt.PillWhite9s, UiArt.CapsulePpu(TraitChipSize.y), UITheme.TraitColor(TraitType.Fabric));
            bg.rectTransform.Stretch();
            var icon = UiArt.Icon(root, "Icon", ArtLib.Trait(TraitType.Fabric), MiddleLeft, new Vector2(2, 0), 20);
            icon.enabled = icon.sprite != null;
            var fallback = Txt(icon.transform, "Fallback", string.Empty, UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true);
            fallback.rectTransform.Stretch();
            var label = Txt(root, "Label", "천", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            label.rectTransform.Stretch(22, 2, 0, 0);

            new Binder(view)
                .Set("_bg", bg)
                .Set("_icon", icon)
                .Set("_iconFallback", fallback)
                .Set("_label", label)
                .Set("_litRing", lit.gameObject)
                .Apply();
            return view;
        }

        // ═════════════════════════ 원정 탭 ═════════════════════════

        // 원정 탭 머리 한 줄 (M58, 0~100): [원정][잔향이 던전] 쪽 고르기 · 가운데 칩(진행 중 / 입장권) · 오른쪽 모두 받기
        static readonly Vector2 ExpeditionSegPos = new(24, -16);
        static readonly Vector2 ExpeditionSegSize = new(200, 68);
        static readonly Vector2 DungeonSegPos = new(232, -16);
        static readonly Vector2 DungeonSegSize = new(260, 68);
        static readonly Vector2 ExpeditionChipPos = new(508, -26);
        static readonly Vector2 ExpeditionChipSize = new(200, 48);
        static readonly Vector2 ExpeditionClaimAllSize = new(240, 64);
        /// <summary>쪽 아래 위 끝 (머리 100 아래 — ZoneList · DungeonPage 모두 Stretch(0, 0, 100, 0))</summary>
        const float ExpeditionPageTop = 100f;
        /// <summary>던전 쪽 '층별 보상' 줄 수 · 줄 크기 (카드 4~304 아래 머리 320 · 줄 364부터, 680 쪽 안에 3줄)</summary>
        const int DungeonFloorRows = 3;
        static readonly Vector2 DungeonFloorRowSize = new(1032, 88);
        const float DungeonFloorRowGap = 8f;

        /// <summary>
        /// 원정 탭 (MainTab.Expedition, UX-SPEC §2.7 M58): 머리 한 줄(제목 · 안내 없이 [원정][잔향이 던전] 쪽 고르기 + 진행 중 칩 / 입장권 칩 + 모두 받기)과
        /// 두 쪽 — 원정 쪽 = 구역 카드 목록(ZoneList, 이름 · 부모 그대로: 코치 경로 ExpeditionPanel/ZoneList), 던전 쪽 = DungeonPage(던전 카드 + 층별 보상).
        /// 쪽은 ExpeditionPanel.SelectPage가 0.12초 엇갈려 바꾼다 (모두 받기 · 진행 중 칩은 원정 쪽에서만).
        /// </summary>
        public static GameObject BuildExpeditionPanel(SceneBuildContext ctx)
        {
            var prefabs = Prefabs(ctx);
            var root = NewRect("ExpeditionPanel", ctx.ContentArea).Stretch();
            var view = root.gameObject.AddComponent<ExpeditionPanel>();

            // 쪽 고르기 = 상점 하위 탭과 같은 쪽 탭 (M53 TabStyle Page: 선택 = 양피지 + Ink 굵게, 아님 = TabNormal + SubText — ExpeditionPanel이 칠한다).
            // 금색은 도전 · 보내기 몫 (M26). 개수 배지 = 받을 원정, 점 = 남은 입장권
            var expeditionSeg = PageSegment(root, "Seg_Expedition", "원정", ExpeditionSegPos, ExpeditionSegSize, out var expeditionSegLabel, out var expeditionSegBadge);
            var dungeonSeg = PageSegment(root, "Seg_Dungeon", "잔향이 던전", DungeonSegPos, DungeonSegSize, out var dungeonSegLabel, out var dungeonSegBadge);

            var active = Pill(root, "ActiveChip", "진행 중 0/3", UIType.Label, UITheme.PanelLight, UITheme.Cream, TopLeft, ExpeditionChipPos, ExpeditionChipSize);
            var activeText = active.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            var ticketPill = Pill(root, "TicketPill", "입장권 0/2", UIType.Label, UITheme.PanelLight, UITheme.Cream, TopLeft, ExpeditionChipPos, ExpeditionChipSize);
            var ticketPillText = ticketPill.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            ticketPill.SetInactive();

            // 모두 받기 (M51 · §3.9): 240x64 · 28, 누르는 자리 88 (UIKit이 위아래로 넓힌다). 받을 원정이 없으면 숨긴다 (interactable=false 아님)
            var claimAll = UIKit.ClaimAllButton(root, ExpeditionClaimAllSize.x, ExpeditionClaimAllSize.y, UIType.ButtonM, out _, out var claimBadge);
            ((RectTransform)claimAll.transform).At(TopRight, new Vector2(-24, -18), ExpeditionClaimAllSize);
            // 모두 다시 보내기 (IDLE-04, ExpeditionBatch): 모두 받기와 같은 자리 Primary 240x64 · 28 (둘이 함께 보이지 않는다 — ExpeditionPanel) +
            // 처음 쓰기 전 NEW 알약. 이름에 'ClaimAllButton'을 담지 않는다 (튜토리얼 14단계 코치 경로)
            var resendAll = SceneParts.Button(root, "ResendAllButton", "모두 다시 보내기", ButtonStyle.Primary, UIType.ButtonM, out var resendLabel);
            ((RectTransform)resendAll.transform).At(TopRight, new Vector2(-24, -18), ExpeditionClaimAllSize);
            UiArt.SetButtonArt(resendAll, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(ExpeditionClaimAllSize.y));
            resendLabel.rectTransform.Stretch(12, 12, 0, 0);
            resendLabel.FitFloor(UIType.Hint);
            resendAll.targetGraphic.HitPad(ExpeditionClaimAllSize, 0f, UISize.MinHit);
            var resendBadge = BuildBadge(resendAll.transform);
            float resendCorner = ((RectTransform)resendBadge.transform).sizeDelta.x * 0.5f - 6f;
            ((RectTransform)resendBadge.transform).anchoredPosition = new Vector2(resendCorner, resendCorner);
            resendBadge.gameObject.SetActive(false);
            resendAll.SetInactive();
            // 던전 잠김 (F-03): 쪽 고르기 흐림(CanvasGroup) + 그 위 자물쇠 28 · '1-10' 알약 (누름 없음 — 누름은 쪽 고르기가 받아 토스트)
            var dungeonSegGroup = dungeonSeg.gameObject.AddComponent<CanvasGroup>();
            var dungeonLock = BuildSegmentLock(root, DungeonSegPos, DungeonSegSize, out var dungeonLockText);

            // 원정 쪽: 구역 카드 목록 (ExpeditionPanel의 직속 자식 그대로 — 코치 경로)
            var listRoot = NewRect("ZoneList", root).Stretch(0, 0, ExpeditionPageTop, 0);
            var listGroup = listRoot.gameObject.AddComponent<CanvasGroup>();
            var content = ScrollList(listRoot, out var scroll);
            AddVerticalList(content, 14, new RectOffset(24, 24, 4, 24));

            // 던전 쪽: 던전 카드 1032x300 (24, -4) + '층별 보상' 3줄 (층 데이터가 없으면 '매일 05:00 충전' 한 줄)
            var dungeonPage = NewRect("DungeonPage", root).Stretch(0, 0, ExpeditionPageTop, 0);
            var dungeonGroup = dungeonPage.gameObject.AddComponent<CanvasGroup>();
            var dungeon = BuildDungeonCard(dungeonPage, prefabs.Chip);
            ((RectTransform)dungeon.transform).At(TopLeft, new Vector2(24, -4), new Vector2(1032, 300));
            BuildDungeonFloorList(dungeonPage, dungeon, prefabs.Chip);
            dungeonPage.SetInactive();

            new Binder(view)
                .Set("_scroll", scroll)
                .Set("_content", content)
                .Set("_zonePrefab", BuildZoneCardPrefab(prefabs.Chip))
                .Set("_dungeonCard", dungeon)
                .Set("_activeText", activeText)
                .Set("_claimAllButton", claimAll)
                .Set("_claimAllBadge", claimBadge)
                .SetArray("_zoneArts", ZoneArts(ctx))
                .Set("_zonePage", listGroup)
                .Set("_dungeonPage", dungeonGroup)
                .Set("_expeditionSeg", expeditionSeg)
                .Set("_dungeonSeg", dungeonSeg)
                .Set("_expeditionSegLabel", expeditionSegLabel)
                .Set("_dungeonSegLabel", dungeonSegLabel)
                .Set("_expeditionSegBadge", expeditionSegBadge)
                .Set("_dungeonSegBadge", dungeonSegBadge)
                .Set("_activeChip", active.gameObject)
                .Set("_ticketPill", ticketPill.gameObject)
                .Set("_ticketPillText", ticketPillText)
                .Set("_resendAllButton", resendAll)
                .Set("_resendAllBadge", resendBadge)
                .Set("_dungeonSegGroup", dungeonSegGroup)
                .Set("_dungeonLock", dungeonLock)
                .Set("_dungeonLockText", dungeonLockText)
                .Apply();
            FitButtonBorders(root);
            root.SetInactive();
            return root.gameObject;
        }

        /// <summary>잠긴 쪽 고르기 '1-10' 알약 (Micro 18 Gold · 밤 알약)</summary>
        static readonly Vector2 SegmentLockPillSize = new(68, 28);

        /// <summary>
        /// 잠긴 쪽 고르기 덮개 (F-03, SPEC4 §5.1): 쪽 고르기 자리 위 "{이름}Lock" — 왼쪽 자물쇠 28 + 오른쪽 위 모서리 '1-10' 알약 (Micro 18 Gold, 밤 알약).
        /// 누름은 받지 않는다 (아래 쪽 고르기가 받아 잠김 토스트). 꺼 둔다 — ExpeditionPanel이 잠겼을 때만 켠다
        /// </summary>
        static GameObject BuildSegmentLock(RectTransform root, Vector2 position, Vector2 size, out TextMeshProUGUI hint)
        {
            var cover = NewRect("DungeonLock", root).At(TopLeft, position, size);
            LockIcon(cover, MiddleLeft, new Vector2(12, 0), 28, UITheme.Cream);
            var pill = Pill(cover, "LockPill", "1-10", UIType.Micro, WithAlpha(UITheme.Background, 0.9f), UITheme.Gold, TopRight, new Vector2(10, 12),
                SegmentLockPillSize);
            pill.raycastTarget = false;
            hint = pill.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            foreach (var graphic in cover.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            cover.SetInactive();
            return cover.gameObject;
        }

        /// <summary>
        /// 탭 패널 머리의 쪽 고르기 한 칸 (원정 · 던전): 흰 바느질 탭 그림(ui_tab_9s — 색은 런타임이 입힌다) + 이름 28 + 오른쪽 위 배지 (+6, +6).
        /// 누르는 자리는 위아래로 88까지 (머리 0~100 안), 옆으로는 넓히지 않는다 (옆 칸과 8px).
        /// </summary>
        static Button PageSegment(RectTransform root, string name, string label, Vector2 position, Vector2 size, out TextMeshProUGUI labelText, out Badge badge)
        {
            // 흰 바느질 탭 그림 (상점 NormalTab/EventTab과 같은 그림 · 배율) — 처음 모양은 '아님' 쪽 탭, ExpeditionPanel이 TabStyle.Apply로 칠한다
            var bg = UiArt.Panel(root, name, UiArt.Tab9s, 1.6f, UITheme.TabNormal, UITheme.TabNormal, true);
            bg.rectTransform.At(TopLeft, position, size);
            var button = AddPlainButton(bg);
            bg.HitPad(size, 0f, UISize.MinHit, 0f, 0f, 10f, 10f);
            labelText = Txt(bg.transform, "Label", label, UIType.ButtonM, UITheme.SubText, TextAlignmentOptions.Center)
                .FitFloor(UIType.Label);
            labelText.rectTransform.Stretch(12, 12, 0, 6);
            badge = BuildBadge(bg.transform);
            ((RectTransform)badge.transform).anchoredPosition = new Vector2(6, 6);
            badge.gameObject.SetActive(false);
            return button;
        }

        /// <summary>
        /// 던전 쪽 '층별 보상' (다음 도전 뒤 세 층): 머리 22 + 줄 3개(층 이름 26 · 보상 칩 64) + 층 데이터가 없을 때의 '매일 05:00 충전' 한 줄.
        /// 줄은 DungeonCardView가 다음 층이 바뀔 때만 채운다.
        /// </summary>
        static void BuildDungeonFloorList(RectTransform page, DungeonCardView card, RewardSlotView chipPrefab)
        {
            const float headerTop = 320f;
            const float rowsTop = 364f;
            Txt(page, "FloorHeader", "층별 보상", UIType.Caption, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.Top(headerTop, 40, 32, 32);
            var labels = new Object[DungeonFloorRows];
            var contents = new Object[DungeonFloorRows];
            var rows = new Object[DungeonFloorRows];
            for (int i = 0; i < DungeonFloorRows; i++)
            {
                var row = UiArt.Panel(page, $"FloorRow_{i}", UiArt.PanelInset9s, 2f, null, WithAlpha(UITheme.PanelLight, 0.6f));
                row.rectTransform.At(TopLeft, new Vector2(24, -(rowsTop + i * (DungeonFloorRowSize.y + DungeonFloorRowGap))), DungeonFloorRowSize);
                var label = Txt(row.transform, "FloorText", "2층", UIType.Body, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Hint);
                label.rectTransform.At(MiddleLeft, new Vector2(24, 0), new Vector2(120, 48));
                var chips = ChipRow(row.transform, "Rewards", 64, 8);
                chips.At(MiddleLeft, new Vector2(150, 0), new Vector2(860, 66));
                labels[i] = label;
                contents[i] = chips;
                rows[i] = row.gameObject;
            }
            var empty = Txt(page, "FloorEmpty", "매일 05:00 충전", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            empty.rectTransform.Top(rowsTop, 44, 32, 32);
            empty.SetInactive();

            new Binder(card)
                .SetArray("_floorRows", rows)
                .SetArray("_floorTexts", labels)
                .SetArray("_floorContents", contents)
                .Set("_floorEmpty", empty.gameObject)
                .Apply();
        }

        /// <summary>잠긴 구역 자물쇠 홈 · 안내 알약 색 (DeepInk — 밤하늘보다 한 단계 짙게)</summary>
        static readonly Color ZoneLockInk = new(0.06f, 0.04f, 0.13f, 1f);

        /// <summary>
        /// 원정 구역 카드 1032x172. 오른쪽 상태 칸(214x148)이 잠김 / 보내기 / 진행 중 / 완료로 바뀐다.
        ///   · 잠김 (M40A · M39): 카드를 Ink@0.22로 살짝 덮고(그림은 런타임이 (0.55, 0.52, 0.62)로 흐리게), 64 DeepInk@0.85 원 위 자물쇠 50 +
        ///     DeepInk@0.88 알약 안 '스테이지 2-9 클리어 시 / 열려요' 22 굵게 Cream (자동 크기 16~22).
        ///   · 보내기 (M57): SendButton 196x84 (이름 · 자리 그대로 — 코치 ExpeditionStart). 지난 친구 그대로 보낼 수 있으면 '다시 보내기' +
        ///     그 아래 '친구 바꾸기 ›' 링크(누르는 자리 196x48, 이름은 SendButton이 아니다), 아니면 '친구를 골라 출발해요' 안내 한 줄.
        ///   · 진행 중 (M22): 친구 얼굴 3 · 남은 시간 · 막대 · 취소 140x44 (20).
        /// </summary>
        static ExpeditionZoneCardView BuildZoneCardPrefab(RewardSlotView chipPrefab)
        {
            var card = Card(null, "ZoneCard");
            var root = card.rectTransform;
            root.sizeDelta = new Vector2(1032, 172);
            var view = card.gameObject.AddComponent<ExpeditionZoneCardView>();

            // 구역 그림 (512x256)
            var artMask = Img(root, "ArtMask", Color.white, RoundSprite);
            artMask.rectTransform.At(TopLeft, new Vector2(12, -12), new Vector2(296, 148));
            artMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var art = Img(artMask.transform, "Art", UITheme.Lavender);
            art.rectTransform.Stretch();
            var artFallback = Txt(artMask.transform, "Fallback", string.Empty, 26, UITheme.Ink, TextAlignmentOptions.Center, true, true);
            artFallback.rectTransform.Stretch(12, 12, 8, 8);
            // 다녀올 시간 단계 칩 '길게' / '밤새' (IDLE-04 — 진행 중 · 완료, 기본 단계면 숨김): 그림 왼쪽 위 밤 알약 96x32
            // (명세는 남은 시간 옆이지만 오른쪽 상태 칸 214에는 96이 들어갈 틈이 없어 그림 모서리에 둔다)
            var tierChip = Pill(root, "TierChip", "밤새", UIType.Hint, WithAlpha(UITheme.Background, 0.88f), UITheme.Cream, TopLeft, new Vector2(20, -20),
                new Vector2(96, 32));
            tierChip.raycastTarget = false;
            var tierChipText = tierChip.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            tierChip.SetInactive();

            var title = Fit(Txt(root, "Title", "원정", 30, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).Ellipsis(), 20);
            title.rectTransform.At(TopLeft, new Vector2(326, -14), new Vector2(446, 44));
            // 소요 · 인원 (캡션 22 InkSub, M29)
            var info = Txt(root, "Info", string.Empty, UIType.Caption, UITheme.InkSub, TextAlignmentOptions.MidlineLeft).FitFloor(UIType.HintMin);
            info.rectTransform.At(TopLeft, new Vector2(326, -58), new Vector2(446, 30));
            var drops = ChipRow(root, "Drops", 62, 8);
            drops.At(TopLeft, new Vector2(326, -96), new Vector2(446, 64));

            // 잠긴 구역: 카드와 같은 양피지 모양을 옅게 덮는다 (Ink 0.45 → 0.22 — 그림이 보이게, 흐림은 그림 색이 맡는다)
            var dim = UiArt.Panel(root, "Dim", UiArt.PanelParchment9s, 1.6f, new Color(0.12f, 0.09f, 0.22f, 0.27f), WithAlpha(UITheme.Ink, 0.22f));
            dim.rectTransform.Stretch();
            dim.SetInactive();

            var state = NewRect("State", root).At(TopRight, new Vector2(-14, -12), new Vector2(214, 148));

            var locked = NewRect("Locked", state).Stretch();
            // 자물쇠: DeepInk@0.85 원 64 위 자물쇠 그림 50 (원 가운데)
            Img(locked, "LockSocket", WithAlpha(ZoneLockInk, 0.85f), CircleSprite).rectTransform.At(TopCenter, new Vector2(0, -2), new Vector2(64, 64));
            LockIcon(locked, TopCenter, new Vector2(0, -9), 50, UITheme.Cream);
            // 안내 알약 (DeepInk@0.88, 둥근 사각 — 아래 2, 높이 74, 좌우 4)
            var lockPill = Img(locked, "LockPill", WithAlpha(ZoneLockInk, 0.88f), RoundSprite);
            lockPill.rectTransform.Bottom(2, 74, 4, 4);
            var lockText = Txt(lockPill.transform, "LockText", string.Empty, UIType.Caption, UITheme.Cream, TextAlignmentOptions.Center, true, true)
                .FitFloor(UIType.AutoSizeFloor);
            lockText.lineSpacing = -12f;
            lockText.rectTransform.Stretch(8, 8, 4, 4);
            locked.SetInactive();

            var ready = NewRect("Ready", state).Stretch();
            var send = StyledButton(ready, "SendButton", "보내기", Tone.Primary, 30, out var sendLabel);
            ((RectTransform)send.transform).At(Center, new Vector2(0, 12), new Vector2(196, 84));
            sendLabel.FitFloor(UIType.ButtonS);
            var sendHint = Txt(ready, "SendHint", "친구를 골라 출발해요", UIType.Hint, UITheme.InkSub).FitFloor(UIType.HintMin);
            sendHint.rectTransform.Bottom(0, 28);
            // '친구 바꾸기 ›' (다시 보내기일 때만): 같은 자리 한 줄 링크, 누르는 자리 높이 48 — 지난 친구가 채워진 준비 팝업을 연다
            var change = Txt(ready, "ChangeMembers", "친구 바꾸기 ›", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true);
            change.raycastTarget = true;
            change.rectTransform.At(BottomCenter, Vector2.zero, new Vector2(196, 28));
            var changeButton = change.gameObject.AddComponent<Button>();
            changeButton.transition = Selectable.Transition.None;
            changeButton.targetGraphic = change;
            changeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            change.gameObject.AddComponent<PressScale>();
            // 누르는 자리 196x48: 위는 8까지(보내기 버튼과의 틈을 지킨다), 모자란 만큼 아래로 12 (28 + 12 + 8)
            change.raycastPadding = new Vector4(0f, -12f, 0f, -8f);
            change.SetInactive();

            // 진행 중: 얼굴 52 (0~52) · 남은 시간 (52~86) · 막대 (90~104) · 취소 140x44 (104~148) — 서로 겹치지 않게 (M22)
            var running = NewRect("Running", state).Stretch();
            var members = new FormPortraitView[3];
            for (int i = 0; i < members.Length; i++)
            {
                var offset = new Vector2((i - 1) * 58, 0);
                UiArt.Icon(running, $"MemberBg_{i}", UiArt.PlateRound, TopCenter, offset + new Vector2(0, -2), 48);
                members[i] = Portrait(running, $"Member_{i}", TopCenter, offset, new Vector2(52, 52), 20);
            }
            var time = Txt(running, "Time", "00:00", 30, UITheme.Ink, TextAlignmentOptions.Center, true);
            time.rectTransform.At(TopCenter, new Vector2(0, -52), new Vector2(200, 34));
            // 캡슐 막대 (틀 + 채움, 채움 너비는 ExpeditionZoneCardView가 anchorMax.x로)
            var barBg = UiArt.Bar(running, "ProgressBg", UITheme.Primary, out var fillRect, 14f);
            barBg.rectTransform.At(TopCenter, new Vector2(0, -90), new Vector2(196, 14));
            fillRect.anchorMax = new Vector2(0.3f, 1f);
            var fill = fillRect.GetComponent<Image>();
            var cancel = Btn(running, "CancelButton", "취소", UITheme.ParchmentDark, UIType.Hint, out var cancelLabel);
            cancelLabel.color = UITheme.InkSub;
            ((RectTransform)cancel.transform).At(BottomCenter, new Vector2(0, 0), new Vector2(140, 44));
            UiArt.Skin9((Image)cancel.targetGraphic, UiArt.PillWhite9s, UiArt.CapsulePpu(44), UITheme.ParchmentDark, WithAlpha(UITheme.Ink, 0.12f));
            // 누르는 자리 88: 위로 22(막대 위는 누를 것이 없다), 아래로 12(카드 끝까지)
            cancel.targetGraphic.HitPad(new Vector2(140, 44), 0f, UISize.MinHit, 0f, 0f, 12f, 22f);
            running.SetInactive();

            var done = NewRect("Done", state).Stretch();
            var claimGlow = Glow(done, "ClaimGlow", UITheme.Lamp, Center, new Vector2(0, -6), new Vector2(270, 160));
            claimGlow.gameObject.AddComponent<MenuGlowPulse>();
            Txt(done, "DoneText", "원정 완료!", 22, StarInk, TextAlignmentOptions.Center, true).rectTransform.Top(0, 30);
            var claim = StyledButton(done, "ClaimButton", "받기", Tone.Gold, 34, out _);
            ((RectTransform)claim.transform).At(Center, new Vector2(0, -12), new Vector2(196, 84));
            done.SetInactive();

            var flash = Img(root, "Flash", WithAlpha(UITheme.Lamp, 0f), RoundSprite);
            flash.rectTransform.Stretch(-4, -4, -4, -4);
            flash.SetInactive();

            new Binder(view)
                .Set("_art", art)
                .Set("_artFallbackText", artFallback)
                .Set("_titleText", title)
                .Set("_infoText", info)
                .Set("_dropContent", drops)
                .Set("_dropPrefab", chipPrefab)
                .Set("_lockedGroup", locked.gameObject)
                .Set("_lockText", lockText)
                .Set("_dim", dim.gameObject)
                .Set("_readyGroup", ready.gameObject)
                .Set("_sendButton", send)
                .Set("_sendLabel", sendLabel)
                .Set("_sendHint", sendHint.gameObject)
                .Set("_changeButton", changeButton)
                .Set("_runningGroup", running.gameObject)
                .SetArray("_memberPortraits", members)
                .Set("_timeText", time)
                .Set("_progressFill", fill)
                .Set("_cancelButton", cancel)
                .Set("_doneGroup", done.gameObject)
                .Set("_claimButton", claim)
                .Set("_flash", flash)
                .Set("_tierChip", tierChip.gameObject)
                .Set("_tierChipText", tierChipText)
                .Apply();
            return SaveMenuPrefab<ExpeditionZoneCardView>(card.gameObject, $"{PrefabDir}/ZoneCard.prefab");
        }

        /// <summary>
        /// 잔향이 던전 카드 1032x300 (원정 탭 던전 쪽 맨 위 — 자리는 부르는 쪽이 정한다).
        /// 오른쪽 위 입장권 칩 아래에 '충전까지 7:38:48' (20 SubText, 입장권이 덜 찼을 때만 — DungeonCardView가 1초마다, M77).
        /// </summary>
        static DungeonCardView BuildDungeonCard(RectTransform parent, RewardSlotView chipPrefab)
        {
            var card = Img(parent, "DungeonCard", UITheme.Stage, RoundSprite);
            var rt = card.rectTransform;
            rt.sizeDelta = new Vector2(1032, 300);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var view = card.gameObject.AddComponent<DungeonCardView>();

            var banner = ArtLib.UI("dungeon_banner");
            if (banner != null)
            {
                Img(rt, "Banner", Color.white, banner).rectTransform.At(Center, Vector2.zero, new Vector2(1032, 1032 * 384f / 1024f));
            }
            else
            {
                Glow(rt, "BannerGlow", WithAlpha(UITheme.EchoGlow, 0.45f), MiddleRight, new Vector2(80, 0), new Vector2(760, 520));
                Glow(rt, "BannerGlow2", WithAlpha(UITheme.DustyPink, 0.25f), MiddleRight, new Vector2(-140, -60), new Vector2(360, 300));
            }
            // (던전 그림은 왼쪽이 이미 어두워 글자 뒤 덮개가 필요 없다)
            if (banner == null) Img(rt, "Shade", WithAlpha(UITheme.Background, 0.55f)).rectTransform.Stretch(0, 400, 0, 0);

            Txt(rt, "Title", "잔향이 던전", 38, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.At(TopLeft, new Vector2(28, -18), new Vector2(520, 52));
            Txt(rt, "Desc", "지킴이 없이 잔향이 덱만으로 층을 돌파해요", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.HintMin).rectTransform.At(TopLeft, new Vector2(28, -70), new Vector2(600, 32));
            var floor = Txt(rt, "FloorText", "다음 도전 1층", 30, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            floor.rectTransform.At(TopLeft, new Vector2(28, -106), new Vector2(420, 48));
            var best = Txt(rt, "BestText", string.Empty, UIType.Caption, UITheme.Cream, TextAlignmentOptions.MidlineLeft).FitFloor(UIType.HintMin);
            best.rectTransform.At(TopLeft, new Vector2(28, -154), new Vector2(420, 30));
            var rewardHeader = Txt(rt, "RewardHeader", "1층 보상", UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            rewardHeader.rectTransform.At(TopLeft, new Vector2(28, -192), new Vector2(300, 28));
            var rewards = ChipRow(rt, "Rewards", 64, 8);
            rewards.At(TopLeft, new Vector2(28, -222), new Vector2(560, 66));

            var ticket = Pill(rt, "TicketChip", "입장권 0/0", 24, WithAlpha(UITheme.Background, 0.8f), UITheme.Cream, TopRight, new Vector2(-22, -20), new Vector2(230, 50));
            var ticketText = ticket.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            // 입장권 충전까지 남은 시간 (M77) — 덜 찼을 때만, 그림 위에서 읽히게 어두운 알약 위에
            var resetPill = Pill(rt, "TicketReset", "충전까지 0:00:00", UIType.Hint, WithAlpha(UITheme.Background, 0.72f), UITheme.SubText, TopRight,
                new Vector2(-22, -74), new Vector2(230, 28));
            var resetText = resetPill.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            resetPill.SetInactive();
            // 권장 줄 (PRG-01 #2): 오른쪽 '권장 12,345 · 잔향이 9,876' (Label 24, 칩 왼쪽에 오른쪽 맞춤) + 평가 칩 88x32 ('여유' / '도전' / '어려움' Micro 18,
            // 바탕 = 평가 색 — DungeonCardView). 다음 층 글 줄(106~154) 높이, 충전 알약(74~102) 아래 · 버튼(194~) 위. 권장이 0이면 둘 다 숨긴다
            var rating = Pill(rt, "RatingChip", "도전", UIType.Micro, UITheme.Gold, UITheme.Ink, TopRight, new Vector2(-22, -116), DungeonRatingSize);
            rating.raycastTarget = false;
            var ratingText = rating.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            rating.SetInactive();
            var rec = Txt(rt, "RecText", string.Empty, UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineRight, true).Ellipsis().FitFloor(UIType.HintMin);
            rec.rectTransform.At(TopRight, new Vector2(-(22 + DungeonRatingSize.x + 10), -114), new Vector2(440, 36));
            rec.SetInactive();

            // 소탕 210x84 · 도전 230x84 (사이 14): 누르는 자리는 위아래로만 2씩 넓혀 88 (M22 — 카드 아래 끝 22 안, 옆 버튼과 겹치지 않는다)
            var sweepSize = new Vector2(210, 84);
            var challengeSize = new Vector2(230, 84);
            var sweep = StyledButton(rt, "SweepButton", "소탕", Tone.Secondary, 30, out _);
            ((RectTransform)sweep.transform).At(BottomRight, new Vector2(-268, 22), sweepSize);
            sweep.targetGraphic.HitPad(sweepSize, 0f, UISize.MinHit, 0f, 0f);
            var challenge = StyledButton(rt, "ChallengeButton", "도전", Tone.Gold, 32, out _);
            ((RectTransform)challenge.transform).At(BottomRight, new Vector2(-24, 22), challengeSize);
            challenge.targetGraphic.HitPad(challengeSize, 0f, UISize.MinHit, 0f, 0f);
            var badge = BuildBadge(challenge.transform);
            ((RectTransform)badge.transform).anchoredPosition = new Vector2(6, 6);

            var flash = Img(rt, "Flash", WithAlpha(UITheme.EchoGlow, 0f), RoundSprite);
            flash.rectTransform.Stretch();
            flash.SetInactive();

            new Binder(view)
                .Set("_floorText", floor)
                .Set("_bestText", best)
                .Set("_ticketText", ticketText)
                .Set("_ticketResetGroup", resetPill.gameObject)
                .Set("_ticketResetText", resetText)
                .Set("_rewardHeader", rewardHeader)
                .Set("_rewardContent", rewards)
                .Set("_rewardPrefab", chipPrefab)
                .Set("_challengeButton", challenge)
                .Set("_sweepButton", sweep)
                .Set("_badge", badge)
                .Set("_flash", flash)
                .Set("_recText", rec)
                .Set("_ratingChip", rating)
                .Set("_ratingText", ratingText)
                .Apply();
            return view;
        }

        /// <summary>던전 권장 줄 평가 칩 (Micro 18 '어려움'이 들어가게 88x32)</summary>
        static readonly Vector2 DungeonRatingSize = new(88, 32);

        /// <summary>구역 그림 (zone_{구역 ID}). 데이터에 있는 구역 + 기본 8구역</summary>
        static Object[] ZoneArts(SceneBuildContext ctx)
        {
            var ids = new List<string> { "exp_1_1", "exp_1_2", "exp_1_3", "exp_1_4", "exp_2_1", "exp_2_2", "exp_2_3", "exp_2_4" };
            if (ctx.Content.Expeditions != null)
            {
                foreach (var zone in ctx.Content.Expeditions.zones)
                {
                    if (zone != null && !string.IsNullOrEmpty(zone.id) && !ids.Contains(zone.id)) ids.Add(zone.id);
                }
            }
            var arts = new List<Object>();
            foreach (var id in ids)
            {
                var art = ArtLib.UI("zone_" + id);
                if (art != null) arts.Add(art);
            }
            return arts.ToArray();
        }

        // ═════════════════════════ 상점 탭 ═════════════════════════

        /// <summary>상점 쪽 고르기 줄 (M51: 제목 줄 없이 위 12, 좌우 24) · 초기화 알약 330x40 (탭 줄 세로 가운데, 오른쪽 −24)</summary>
        const float ShopTabTop = 12f;
        const float ShopTabHeight = 72f;
        static readonly Vector2 ShopResetPillSize = new(330, 40);
        /// <summary>
        /// '축제 중' 알약 (EVT-01 §8.3) 100x30: 이벤트 탭 오른쪽 위 모서리에 걸친다 (탭 오른쪽 끝 + 6, 탭 위 끝 − 10 → ContentArea 안 y 2~32,
        /// 탭 이름 줄(가운데 ±15) 위). Micro 18 굵게 Ink on DustyPink (밝은 바탕 — 대비 7:1)
        /// </summary>
        static readonly Vector2 ShopFestivalPillSize = new(100, 30);
        static readonly Vector2 ShopFestivalPillPosition = new(6, 10);
        /// <summary>오늘의 작은 선물 카드 (NEW-02 §13.7): 860x120, 설명 글(98~162) 아래 위에서 168, 가운데</summary>
        const float DailyGiftCardTop = 168f;
        const float DailyGiftCardWidth = 860f;
        const float DailyGiftChestSize = 88f;
        static readonly Vector2 DailyGiftClaimSize = new(180, 80);
        static readonly Vector2 DailyGiftChipSize = new(96, 104);

        /// <summary>
        /// 상점 탭 (MainTab.Shop, UX-SPEC §2.9): 제목 줄 없이 쪽 고르기 [수선점 상점 300][별이 머문 밤 산책 330] (M53 쪽 탭 — 흰 바느질 탭 그림에 ShopPanel이 TabStyle로 칠한다,
        /// 이벤트 탭은 고르면 6px 분홍 밑줄 · 축제 중이면 '축제 중' 알약) + 오른쪽 '초기화까지' 알약 + 설명 두 줄 + 오늘의 작은 선물 카드(NEW-02, 수선점 상점 쪽 168 —
        /// 보이면 목록이 132 내려간다) + 상품 4열 격자(240x290, Grid4) / 이벤트 배너. 쪽 탭은 켜짐 · 꺼짐 전용 그림이 있으면 TabSkin (ART-UPGRADE §4.3 #9).
        /// 경로 ShopPanel/ProductList는 그대로 (코치).
        /// </summary>
        public static GameObject BuildShopPanel(SceneBuildContext ctx)
        {
            var prefabs = Prefabs(ctx);
            var root = NewRect("ShopPanel", ctx.ContentArea).Stretch();
            var view = root.gameObject.AddComponent<ShopPanel>();

            // 하위 탭: 흰 바느질 탭 그림 (색 · 글자는 ShopPanel이 TabStyle.Apply로)
            var normalTab = Btn(root, "NormalTab", "수선점 상점", UITheme.Parchment, UIType.ButtonM, out var normalLabel);
            ((RectTransform)normalTab.transform).At(TopLeft, new Vector2(24, -ShopTabTop), new Vector2(300, ShopTabHeight));
            UiArt.Skin9((Image)normalTab.targetGraphic, UiArt.Tab9s, 1.6f, UITheme.Parchment, UITheme.Parchment);
            PageTabSkin((Image)normalTab.targetGraphic, 1.6f);
            normalLabel.FitFloor(UIType.Label);
            normalLabel.rectTransform.Stretch(12, 12, 0, 6);
            // 누르는 자리 88 (M22): 위아래 8씩 — 위는 ContentArea 위 끝(4)에서 멈추고, 아래는 설명 글(98~, 누르기 없음) 앞, 옆으로는 넓히지 않는다
            normalTab.targetGraphic.HitPad(new Vector2(300, ShopTabHeight), 0f, UISize.MinHit, 0f, 0f, 8f, 8f);
            var eventTab = Btn(root, "EventTab", "별이 머문 밤 산책", UITheme.TabNormal, UIType.ButtonM, out var eventLabel);
            ((RectTransform)eventTab.transform).At(TopLeft, new Vector2(336, -ShopTabTop), new Vector2(330, ShopTabHeight));
            UiArt.Skin9((Image)eventTab.targetGraphic, UiArt.Tab9s, 1.6f, UITheme.TabNormal, UITheme.TabNormal);
            PageTabSkin((Image)eventTab.targetGraphic, 1.6f);
            eventLabel.FitFloor(UIType.Label);
            eventLabel.rectTransform.Stretch(12, 12, 0, 6);
            eventTab.targetGraphic.HitPad(new Vector2(330, ShopTabHeight), 0f, UISize.MinHit, 0f, 0f, 8f, 8f);
            // 이벤트 탭 밑줄 (안쪽 24, 아래에서 6 위, 6px 분홍 — 고르면 TabStyle이 켠다)
            var eventAccent = UiArt.Panel(eventTab.transform, "Accent", UiArt.PillWhite9s, UiArt.CapsulePpu(6), UITheme.DustyPink, UITheme.DustyPink);
            eventAccent.rectTransform.Bottom(6, 6, 24, 24);
            eventAccent.enabled = false;
            var sparkle = UiArt.Sparkle;
            GameObject sparkleObject = null;
            if (UiArt.IsArt(sparkle))
            {
                var sparkleImage = Img(eventTab.transform, "Sparkle", UITheme.Lamp, sparkle);
                sparkleImage.preserveAspect = true;
                sparkleImage.rectTransform.At(TopRight, new Vector2(12, 12), new Vector2(40, 40));
                sparkleObject = sparkleImage.gameObject;
            }
            // '축제 중' (EVT-01 §8.3 — 별이 머문 밤 산책이 열려 있는 동안 ShopPanel이 켠다, 누름 없음)
            var festivalPill = Pill(eventTab.transform, "FestivalPill", ShopPanel.FestivalPillText, UIType.Micro, UITheme.DustyPink, UITheme.Ink,
                TopRight, ShopFestivalPillPosition, ShopFestivalPillSize);
            festivalPill.SetInactive();

            var desc = Txt(root, "DescText", string.Empty, 22, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            desc.rectTransform.Top(98, 64, 30, 30);
            var banner = BuildEventBanner(root, out var eventDesc, out var balance);

            // 초기화까지 (M77): 탭 줄 세로 가운데 (12 + 36 = 48), 오른쪽 −24 — 수선점 상점 쪽에서만
            var resetPill = Pill(root, "ResetPill", "초기화까지 0:00:00", UIType.Caption, WithAlpha(UITheme.Background, 0.75f), UITheme.SubText, TopRight,
                new Vector2(-24, -(ShopTabTop + (ShopTabHeight - ShopResetPillSize.y) * 0.5f)), ShopResetPillSize);
            var resetText = resetPill.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

            var dailyGift = BuildDailyGiftCard(root);

            var listRoot = NewRect("ProductList", root).Stretch(0, 0, ShopPanel.NormalListTop, 0);
            var content = ScrollList(listRoot, out var scroll);
            // 4열 격자 (M24: 240x290, 간격 16, 좌우 36)
            Grid4(content, 290f, 4f, 24f);
            var empty = Txt(listRoot, "EmptyLabel", "준비 중인 상품이 없어요.", 26, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.SetInactive();

            new Binder(view)
                .Set("_normalTab", normalTab)
                .Set("_eventTab", eventTab)
                .Set("_eventTabAccent", eventAccent)
                .Set("_resetPill", resetPill.gameObject)
                .Set("_resetText", resetText)
                .Set("_normalTabLabel", normalLabel)
                .Set("_eventTabLabel", eventLabel)
                .Set("_descText", desc)
                .Set("_eventBanner", banner)
                .Set("_eventDescText", eventDesc)
                .Set("_eventBalanceText", balance)
                .Set("_listRoot", listRoot)
                .Set("_scroll", scroll)
                .Set("_content", content)
                .Set("_cardPrefab", BuildProductCardPrefab())
                .Set("_emptyLabel", empty.gameObject)
                .Set("_dailyGift", dailyGift)
                .Set("_festivalPill", festivalPill.gameObject)
                .Set("_eventSparkle", sparkleObject)
                .Apply();
            FitButtonBorders(root);
            root.SetInactive();
            return root.gameObject;
        }

        /// <summary>
        /// 오늘의 작은 선물 카드 "DailyGiftCard" (NEW-02 §13.7, 860x120 밤 판, 위에서 168 가운데 — 꺼 둠, ShopPanel이 켠다):
        /// Chest 88 (UI/journal_chest_closed, 받은 뒤 journal_chest_open) 왼쪽 20 · Title '오늘의 작은 선물' Title 28 굵게 Cream (124, −14) ·
        /// Line Caption 22 SubText (124, −64) · Reward 보상 칸 96x104 (아이콘 64, 오른쪽 −216) · ClaimButton [받기] Gold 180x80 오른쪽 −20 (누르는 자리 위아래 88).
        /// 상품 목록보다 먼저 만들어 목록 뒤에 놓인다 (카드가 보이면 목록이 그 아래에서 시작해 겹치지 않는다)
        /// </summary>
        static DailyGiftCardView BuildDailyGiftCard(RectTransform root)
        {
            _ = MetaSceneBuilder.Icons;
            var card = NightPanel(root, "DailyGiftCard");
            card.rectTransform.At(TopCenter, new Vector2(0, -DailyGiftCardTop), new Vector2(DailyGiftCardWidth, DailyGiftCardView.CardHeight));
            var view = card.gameObject.AddComponent<DailyGiftCardView>();

            var closed = ArtLib.UI("journal_chest_closed");
            var open = ArtLib.UI("journal_chest_open");
            var chest = UiArt.Icon(card.transform, "Chest", closed != null ? closed : UiArt.Star, MiddleLeft, new Vector2(20, 0), DailyGiftChestSize);
            if (closed == null) chest.color = UITheme.Gold;
            Txt(card.transform, "Title", DailyGiftCardView.TitleText, UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .FitFloor(UIType.Label).rectTransform.At(TopLeft, new Vector2(124, -14), new Vector2(400, 44));
            var line = Txt(card.transform, "Line", DailyGiftCardView.ReadyLine, UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.HintMin, UIType.Caption);
            line.rectTransform.At(TopLeft, new Vector2(124, -64), new Vector2(400, 40));

            var chip = MetaSceneBuilder.BuildRewardChip(card.transform, "Reward", DailyGiftChipSize, 64, UIType.Caption, false, false);
            ((RectTransform)chip.transform).At(MiddleRight, new Vector2(-216, 0), DailyGiftChipSize);

            var claim = StyledButton(card.transform, "ClaimButton", "받기", Tone.Gold, UIType.ButtonM, out _);
            ((RectTransform)claim.transform).At(MiddleRight, new Vector2(-20, 0), DailyGiftClaimSize);
            claim.targetGraphic.HitPad(DailyGiftClaimSize, 0f, UISize.MinHit);

            new Binder(view)
                .Set("_chest", chest)
                .Set("_chestClosed", closed)
                .Set("_chestOpen", open)
                .Set("_lineText", line)
                .Set("_rewardChip", chip)
                .Set("_claimButton", claim)
                .Apply();
            card.SetInactive();
            return view;
        }

        /// <summary>'별이 머문 밤 산책' 배너: 그림 + 소개 + 별빛 사탕 보유량</summary>
        static GameObject BuildEventBanner(RectTransform root, out TextMeshProUGUI desc, out TextMeshProUGUI balance)
        {
            var banner = Img(root, "EventBanner", UITheme.Stage, RoundSprite);
            var rt = banner.rectTransform.Top(96, 206, 24, 24);
            banner.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var art = ArtLib.UI("shop_event_banner");
            if (art != null)
            {
                // 덮어 맞춤 (aspect AF-6, D4): 틀(가로로 늘어남)이 1032보다 넓어져도 Stage 색 띠 없이 그림이 틀을 덮는다. 폭 1080에서는 예전과 같은 자르기
                var eventArt = Img(rt, "Art", Color.white, art);
                eventArt.rectTransform.Stretch();
                eventArt.gameObject.AddComponent<BackgroundFit>().Configure(BackgroundFitMode.Cover, new Vector2(0.5f, 0.5f));
            }
            else
            {
                Glow(rt, "Glow", WithAlpha(UITheme.DustyPink, 0.4f), MiddleRight, new Vector2(60, 0), new Vector2(620, 420));
                Glow(rt, "Glow2", WithAlpha(UITheme.Lamp, 0.3f), MiddleRight, new Vector2(-180, 40), new Vector2(260, 220));
            }
            // (배너 그림 왼쪽은 어두운 밤하늘이라 글자 뒤 덮개 없이 읽힌다 — 각진 덮개는 그림이 없을 때만)
            if (art == null) Img(rt, "Shade", WithAlpha(UITheme.Background, 0.5f)).rectTransform.Stretch(0, 420, 0, 0);

            Txt(rt, "Title", "별이 머문 밤 산책", 36, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.At(TopLeft, new Vector2(26, -16), new Vector2(600, 50));
            desc = Fit(Txt(rt, "Desc", string.Empty, UIType.Caption, UITheme.Cream, TextAlignmentOptions.TopLeft, false, true), UIType.AutoSizeFloor);
            desc.rectTransform.At(TopLeft, new Vector2(26, -72), new Vector2(600, 74));

            var chip = UiArt.Panel(rt, "Balance", UiArt.PillNight9s, UiArt.CapsulePpu(64), null, WithAlpha(UITheme.Background, 0.82f));
            chip.rectTransform.At(BottomRight, new Vector2(-20, 18), new Vector2(260, 64));
            var icon = CurrencyIcon(chip.transform, CurrencyType.EventToken, 46).rectTransform;
            icon.anchorMin = icon.anchorMax = MiddleLeft;
            icon.anchoredPosition = new Vector2(38, 0);
            balance = Txt(chip.transform, "Amount", "0", 32, UITheme.EventToken, TextAlignmentOptions.MidlineRight, true);
            balance.rectTransform.Stretch(70, 20, 0, 0);
            Txt(rt, "BalanceLabel", "보유한 별빛 사탕", UIType.Hint, UITheme.Cream, TextAlignmentOptions.MidlineRight)
                .rectTransform.At(BottomRight, new Vector2(-24, 86), new Vector2(260, 28));

            banner.SetInactive();
            return banner.gameObject;
        }

        /// <summary>
        /// 상품 카드 240x290 (M56): 아이콘 124(수량 '×5' 배지) · 이름 23 · '오늘 2/3 · 보유 15' 20 InkSub · 가격 버튼(CostButtonView, 어두운 바탕) ·
        /// 품절 덮개(CanvasGroup — 품절이 되는 순간 0.2초에 나타난다).
        /// </summary>
        static ShopProductCardView BuildProductCardPrefab()
        {
            var card = Card(null, "ProductCard");
            var root = card.rectTransform;
            root.sizeDelta = new Vector2(240, 290);
            var view = card.gameObject.AddComponent<ShopProductCardView>();

            var glow = Glow(root, "IconGlow", WithAlpha(UITheme.Lavender, 0.45f), TopCenter, new Vector2(0, 4), new Vector2(200, 176));
            var icon = BuildRewardChip(root, "Icon", 30);
            ((RectTransform)icon.transform).At(TopCenter, new Vector2(0, -16), new Vector2(124, 124));

            var name = Fit(Txt(root, "NameText", "상품", 23, UITheme.Ink, TextAlignmentOptions.Center, true).Ellipsis(), 18);
            name.rectTransform.Top(148, 34, 10, 10);
            var limit = Txt(root, "LimitText", string.Empty, UIType.Hint, UITheme.InkSub).Ellipsis().FitFloor(UIType.AutoSizeFloor);
            limit.rectTransform.Top(182, 26, 8, 8);

            var buy = StyledButton(root, "BuyButton", string.Empty, Tone.Primary, 28, out var buyLabel);
            Object.DestroyImmediate(buyLabel.gameObject);
            ((RectTransform)buy.transform).Bottom(12, 70, 14, 14);
            var priceRow = CenteredRow(buy.transform, "Price", 8);
            priceRow.Stretch(8, 8, 0, 0);
            var priceFallback = ArtLib.Currency(CurrencyType.Gold) != null ? ArtLib.Currency(CurrencyType.Gold) : UiArt.Star;
            var priceIcon = Img(priceRow, "PriceIcon", Color.white, priceFallback);
            priceIcon.preserveAspect = true;
            var priceLayout = priceIcon.gameObject.AddComponent<LayoutElement>();
            priceLayout.preferredWidth = 36;
            priceLayout.preferredHeight = 36;
            var price = Txt(priceRow, "PriceText", "0", 28, UITheme.Text, TextAlignmentOptions.MidlineLeft, true);

            // 품절: 카드 모양 그대로 어둡게 덮고 빨간 도장 그림(-10도) + "품절"
            var soldOut = UiArt.Panel(root, "SoldOut", UiArt.PanelParchment9s, 1.6f, new Color(0.1f, 0.08f, 0.18f, 0.55f), WithAlpha(UITheme.Ink, 0.5f));
            soldOut.rectTransform.Stretch();
            var stampArt = UiArt.StampSoldout;
            bool stampHasArt = UiArt.IsArt(stampArt);
            var stamp = Img(soldOut.transform, "Stamp", stampHasArt ? Color.white : WithAlpha(UITheme.Negative, 0.92f), stampArt);
            if (stampHasArt)
            {
                stamp.type = Image.Type.Simple;
                stamp.preserveAspect = true;
            }
            stamp.rectTransform.At(Center, new Vector2(0, 24), stampHasArt ? new Vector2(196, 82) : new Vector2(170, 62));
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, stampHasArt ? -10 : 10);
            var stampText = UiArt.Outline(Txt(stamp.transform, "Text", "품절", 32, stampHasArt ? UITheme.Negative : Color.white, TextAlignmentOptions.Center, true));
            stampText.rectTransform.Stretch();
            var soldOutGroup = soldOut.gameObject.AddComponent<CanvasGroup>();
            soldOut.SetInactive();

            // 가격 버튼 세 상태 (M07: 넉넉함 / 부족함 = 어두운 알약 + CostShort, 누를 수 있음 / 품절 · 보유 중 = 쓸 수 없음)
            UIKit.CostBtn(buy, price, CostSurface.Dark);

            new Binder(view)
                .Set("_icon", icon)
                .Set("_iconGlow", glow)
                .Set("_nameText", name)
                .Set("_limitText", limit)
                .Set("_buyButton", buy)
                .Set("_priceIcon", priceIcon)
                .Set("_priceText", price)
                .SetArray("_currencyIcons", CurrencySprites())
                .Set("_priceFallback", priceFallback)
                .Set("_soldOut", soldOut.gameObject)
                .Set("_soldOutText", stampText)
                .Set("_soldOutGroup", soldOutGroup)
                .Apply();
            return SaveMenuPrefab<ShopProductCardView>(card.gameObject, $"{PrefabDir}/ProductCard.prefab");
        }

        // ═════════════════════════ 팝업 ═════════════════════════

        /// <summary>메뉴 관련 팝업 + 획득처 이동 처리기 + 원정 탭 빨간 점</summary>
        public static void BuildPopups(SceneBuildContext ctx)
        {
            var prefabs = Prefabs(ctx);
            var popups = new[]
            {
                BuildRewardPopup(ctx, prefabs.Slot),
                BuildBagPopup(ctx, prefabs.Slot),
                BuildItemInfoPopup(ctx),
                BuildExpeditionSetupPopup(ctx, prefabs.Chip),
                BuildDungeonSweepPopup(ctx, prefabs.Chip),
                BuildSynergyDetailPopup(ctx),
                BuildSynergyCodexPopup(ctx),
            };
            foreach (var popup in popups)
            {
                FitButtonBorders(popup.transform);
                ctx.Popups.Add(popup);
            }
            BuildSourceNavigator(ctx);
            BuildSynergyNavigator(ctx);
            BuildExpeditionTabBadge(ctx);
        }

        static UIPopup BuildRewardPopup(SceneBuildContext ctx, RewardSlotView slotPrefab)
        {
            var popup = BuildPopup<RewardPopup>(ctx.PopupRoot, "RewardPopup", "보상 획득", new Vector2(940, 860), out var body, out var title);
            var window = (RectTransform)body.parent;
            var glow = Glow(window, "TitleGlow", WithAlpha(UITheme.Lamp, 0.55f), TopCenter, new Vector2(0, 60), new Vector2(640, 280));
            glow.transform.SetSiblingIndex(0);
            var sparkle = UiArt.Sparkle;
            if (UiArt.IsArt(sparkle))
            {
                var left = Img(window, "SparkleL", UITheme.Lamp, sparkle);
                left.preserveAspect = true;
                left.rectTransform.At(TopCenter, new Vector2(-250, -30), new Vector2(52, 52));
                var right = Img(window, "SparkleR", UITheme.Lamp, sparkle);
                right.preserveAspect = true;
                right.rectTransform.At(TopCenter, new Vector2(250, -44), new Vector2(40, 40));
            }

            var subtitle = Fit(Txt(body, "Subtitle", string.Empty, 28, UITheme.Text, TextAlignmentOptions.Center, false, true), 20);
            subtitle.rectTransform.Top(0, 80);
            var list = NewRect("Slots", body).Stretch(0, 0, 92, 128);
            var content = ScrollList(list, out _);
            // 팝업 5칸 격자 (M24: 164x196, 간격은 SceneParts.Grid5Popup)
            var grid = Grid5Popup(content, 196f, 18f, 10f, 10f);
            grid.childAlignment = TextAnchor.UpperCenter;
            var empty = Txt(body, "EmptyLabel", "이번에는 얻은 보상이 없어요.", 28, UITheme.SubText);
            empty.rectTransform.Stretch(0, 0, 92, 128);
            empty.SetInactive();
            // 확인 (보라 — 이 팝업의 주 버튼, M26). 보조 버튼이 있으면 RewardPopup이 둘을 380씩 나란히 둔다 (보조 왼쪽, 사이 24).
            // 빌드 자리는 둘 다 '나란히' 자리 (확인 x 12~392 · 보조 −392~−12) — 예전처럼 확인을 가운데 400에 두면 꺼 둔 보조와 겹쳐 보였다 (M22 검사).
            // 열 때 RewardPopup.LayoutButtons가 늘 크기 · 자리를 다시 정한다 (보조가 없으면 확인 가운데 400)
            var confirm = StyledButton(body, "ConfirmButton", "확인", Tone.Primary, 36, out _);
            ((RectTransform)confirm.transform).At(BottomCenter, new Vector2(202, 0), new Vector2(380, 108));
            var action = StyledButton(body, "ActionButton", "바로 입혀 보기", Tone.Secondary, 32, out var actionLabel);
            ((RectTransform)action.transform).At(BottomCenter, new Vector2(-202, 0), new Vector2(380, 108));
            actionLabel.FitFloor(UIType.ButtonS);
            action.SetInactive();

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_subtitleText", subtitle)
                .Set("_slotContent", content)
                .Set("_slotPrefab", slotPrefab)
                .Set("_emptyLabel", empty.gameObject)
                .Set("_confirmButton", confirm)
                .Set("_glow", glow)
                .Set("_actionButton", action)
                .Set("_actionLabel", actionLabel)
                .Apply();
            return popup;
        }

        /// <summary>가방 창 (M70: 960x1060 — 재료가 많으면 목록이 스크롤된다)</summary>
        static readonly Vector2 BagWindowSize = new(960, 1060);
        /// <summary>가방 아래 '재료 모으는 곳' 칸 높이 (본문 888 아래 끝에 붙는다) · 바로 가기 한 칸 · 칸 사이</summary>
        const float BagSourceGuideHeight = 238f;
        static readonly Vector2 BagSourceTileSize = new(264, 156);
        const float BagSourceTileGap = 20f;
        /// <summary>
        /// 가방 머리 그림 띠 (ART-UPGRADE §4.3 #10 — UiArt.BagHeader 1200x400이 있을 때만): 제목 아래 본문 맨 위 888x300 (둥근 마스크 + 비율대로 덮기 +
        /// 금테 card_frame_9s), 아래 틈 16. 띠가 있으면 창이 그만큼(316) 커지고 본문 내용은 316 내려간다. 그림이 없으면 예전 그대로 (960x1060)
        /// </summary>
        const float BagHeaderHeight = 300f;
        const float BagHeaderGap = 16f;
        const float BagHeaderFramePpu = 2.2f;

        /// <summary>가방 창 높이 (테스트용): 머리 그림이 있으면 1060 + 316 = 1376, 없으면 1060</summary>
        internal static float BagWindowHeight(bool header) => BagWindowSize.y + (header ? BagHeaderHeight + BagHeaderGap : 0f);

        /// <summary>
        /// 가방 (M70): 960x1060, 모은 재료 요약 + 안내 한 줄 + 5칸 격자(164x204, 스크롤) + 아래 끝 '재료 모으는 곳' 칸 (늘 보인다 —
        /// 재료 9종 = 격자 2줄이라 1060 창 아래가 비던 자리를 채운다): 888x238 PanelLight@0.6, 안내 '재료는 원정·던전·상점에서 모을 수 있어요' 26 +
        /// 바로 가기 3칸 264x156 (보조 버튼 그림 · 그림 64 · 이름 28 굵게 '원정 ›' · 한 줄 설명 20 · 잠기면 자물쇠 32, BagPopup이 흐린다).
        /// 격자 목록은 그 위 16에서 끝난다 (재료가 늘면 목록만 스크롤).
        /// </summary>
        static UIPopup BuildBagPopup(SceneBuildContext ctx, RewardSlotView slotPrefab)
        {
            const int columns = 5;
            const float cellHeight = 204f;
            const float rowGap = 18f;
            var headerArt = UiArt.BagHeader;
            bool hasHeader = headerArt != null;
            var windowSize = new Vector2(BagWindowSize.x, BagWindowHeight(hasHeader));
            var popup = BuildPopup<BagPopup>(ctx.PopupRoot, "BagPopup", "가방", windowSize, out var body, out _);

            // 머리 그림 띠 (그림이 있을 때만) — 그 아래로 요약 · 안내 · 격자를 내린다
            float top = 0f;
            if (hasHeader)
            {
                BuildBagHeader(body, headerArt);
                top = BagHeaderHeight + BagHeaderGap;
            }

            var summary = Txt(body, "Summary", string.Empty, 28, UITheme.Text, TextAlignmentOptions.MidlineLeft);
            summary.rectTransform.At(TopLeft, new Vector2(0, -top), new Vector2(360, 50));
            Fit(Txt(body, "Hint", "재료를 누르면 설명과 획득처를 볼 수 있어요", 22, UITheme.SubText, TextAlignmentOptions.MidlineRight), 16)
                .rectTransform.At(TopRight, new Vector2(0, -(4 + top)), new Vector2(520, 44));

            var list = NewRect("Items", body).Stretch(0, 0, 66 + top, BagSourceGuideHeight + 16);
            var content = ScrollList(list, out _);
            AddGrid(content, new Vector2(164, cellHeight), new Vector2(17, rowGap), columns, new RectOffset(0, 0, 6, 24));
            var empty = Txt(list, "EmptyLabel", "아직 모은 재료가 없어요.", 28, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.SetInactive();

            // 재료 모으는 곳 (본문 아래 끝 — PanelLight@0.6): 안내 한 줄 + 바로 가기 [원정 ›][잔향이 던전 ›][상점 ›] (BagPopup.SourceTypes 순서)
            var guide = UiArt.Panel(body, "SourceGuide", UiArt.PanelInset9s, 2f, WithAlpha(Color.white, 0.6f), WithAlpha(UITheme.PanelLight, 0.6f), true);
            guide.rectTransform.At(BottomCenter, Vector2.zero, new Vector2(888, BagSourceGuideHeight));
            Txt(guide.transform, "Text", "재료는 원정·던전·상점에서 모을 수 있어요", UIType.Body, UITheme.Text, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.Hint).rectTransform.Top(14, 36, 28, 28);
            string[] ids = { "Expedition", "Dungeon", "Shop" };
            string[] names = { "원정 ›", "잔향이 던전 ›", "상점 ›" };
            string[] descs = { "친구를 보내 모아 와요", "층을 넘으면 받아요", "매일 새로 채워져요" };
            Sprite[] icons = { ArtLib.UI("tab_expedition"), ArtLib.Icon("echo"), ArtLib.UI("tab_shop") };
            var sourceButtons = new Object[ids.Length];
            var sourceGroups = new Object[ids.Length];
            var sourceLocks = new Object[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var tile = StyledButton(guide.transform, $"SourceButton_{ids[i]}", names[i], Tone.Secondary, UIType.ButtonM, out var tileLabel);
                ((RectTransform)tile.transform).At(TopLeft, new Vector2(28 + i * (BagSourceTileSize.x + BagSourceTileGap), -60), BagSourceTileSize);
                tileLabel.FitFloor(UIType.Label);
                tileLabel.rectTransform.Top(80, 34, 10, 10);
                var icon = UiArt.Icon(tile.transform, "Icon", icons[i], TopCenter, new Vector2(0, -12), 64);
                if (icons[i] == null) icon.enabled = false;
                Txt(tile.transform, "Desc", descs[i], UIType.Hint, UITheme.SubText, TextAlignmentOptions.Center).Ellipsis().FitFloor(UIType.HintMin)
                    .rectTransform.Top(114, 30, 10, 10);
                var lockIcon = UiArt.Icon(tile.transform, "Lock", ArtLib.Icon("lock"), TopRight, new Vector2(-10, -10), 32);
                if (lockIcon.sprite == null) lockIcon.color = UITheme.SubText;
                sourceButtons[i] = tile;
                sourceGroups[i] = tile.gameObject.AddComponent<CanvasGroup>();
                sourceLocks[i] = lockIcon.SetInactive();
            }

            new Binder(popup)
                .Set("_content", content)
                .Set("_slotPrefab", slotPrefab)
                .Set("_summaryText", summary)
                .Set("_emptyLabel", empty.gameObject)
                .SetArray("_sourceButtons", sourceButtons)
                .SetArray("_sourceGroups", sourceGroups)
                .SetArray("_sourceLocks", sourceLocks)
                .Apply();
            return popup;
        }

        /// <summary>
        /// 가방 머리 띠 "Header" (본문 맨 위 Top(0, 300), 폭 = 본문): 둥근 마스크 안에 그림을 비율대로 덮어 깔고(AspectRatioFitter EnvelopeParent — 1200x400을 888x300에),
        /// 금테(card_frame_9s, 없으면 생략)를 덧씌운다. 누름 없음
        /// </summary>
        static void BuildBagHeader(RectTransform body, Sprite art)
        {
            var maskImage = Img(body, "Header", Color.white, RoundSprite);
            maskImage.pixelsPerUnitMultiplier = 1.5f;
            maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var mask = maskImage.rectTransform.Top(0, BagHeaderHeight);
            var image = Img(mask, "Art", Color.white, art);
            image.type = Image.Type.Simple;
            image.rectTransform.Stretch();
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / Mathf.Max(1f, art.rect.height);
            var frameArt = ArtLib.UI("card_frame_9s");
            if (frameArt == null) return;
            var frame = Img(mask, "Frame", Color.white, frameArt);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = BagHeaderFramePpu;
            frame.rectTransform.Stretch();
        }

        static UIPopup BuildItemInfoPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<ItemInfoPopup>(ctx.PopupRoot, "ItemInfoPopup", "재료 정보", new Vector2(880, 1120), out var body, out _);

            var icon = BuildRewardChip(body, "Icon", 48);
            ((RectTransform)icon.transform).At(TopLeft, Vector2.zero, new Vector2(176, 176));
            var name = Fit(Txt(body, "Name", string.Empty, 38, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis(), 26);
            name.rectTransform.At(TopLeft, new Vector2(200, -8), new Vector2(608, 56));
            var grade = Txt(body, "Grade", string.Empty, 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            grade.rectTransform.At(TopLeft, new Vector2(200, -68), new Vector2(608, 36));
            var count = Txt(body, "Count", string.Empty, 26, UITheme.Text, TextAlignmentOptions.MidlineLeft);
            count.rectTransform.At(TopLeft, new Vector2(200, -112), new Vector2(608, 40));
            var desc = Fit(Txt(body, "Desc", string.Empty, 26, UITheme.Text, TextAlignmentOptions.TopLeft, false, true), 18);
            desc.lineSpacing = 6;
            desc.rectTransform.Top(196, 148);

            Img(body, "Divider", UITheme.Divider).rectTransform.Top(358, 2);
            Txt(body, "SourceHeader", "획득처", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(376, 46);
            var sources = NewRect("Sources", body).Stretch(0, 0, 432, 0);
            var content = ScrollList(sources, out _);
            AddVerticalList(content, 12, new RectOffset(0, 0, 0, 12));
            var noSource = Txt(sources, "NoSource", "획득처 정보가 없어요.", 26, UITheme.SubText);
            noSource.rectTransform.Stretch();
            noSource.SetInactive();

            new Binder(popup)
                .Set("_icon", icon)
                .Set("_nameText", name)
                .Set("_gradeText", grade)
                .Set("_countText", count)
                .Set("_descText", desc)
                .Set("_sourceContent", content)
                .Set("_sourcePrefab", BuildSourceRowPrefab())
                .Set("_noSourceLabel", noSource.gameObject)
                .Apply();
            return popup;
        }

        /// <summary>
        /// 획득처 한 줄 808x96: 종류 알약 · 이름 28 · '가기 ›' Primary 180x70 (M76 — 스테이지도 같은 말).
        /// 잠긴 줄(ItemSourceRowView): 줄 전체 α0.55(CanvasGroup), 이름 아래 자물쇠 28 + '스테이지 2-9 클리어 시' 20, 버튼 자리에 보조 '잠김' (누르면 이유 토스트).
        /// </summary>
        static ItemSourceRowView BuildSourceRowPrefab()
        {
            var bg = UiArt.Panel(null, "SourceRow", UiArt.PanelInset9s, 2f, null, UITheme.PanelLight);
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(808, 96);
            var view = bg.gameObject.AddComponent<ItemSourceRowView>();
            var group = bg.gameObject.AddComponent<CanvasGroup>();

            var chip = UiArt.Panel(root, "TypeChip", UiArt.PillWhite9s, UiArt.CapsulePpu(42), UITheme.SoftBlue);
            chip.rectTransform.At(MiddleLeft, new Vector2(18, 0), new Vector2(96, 42));
            var type = Txt(chip.transform, "TypeText", "원정", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true);
            type.rectTransform.Stretch();
            var label = Fit(Txt(root, "Label", string.Empty, 28, UITheme.Text, TextAlignmentOptions.MidlineLeft).Ellipsis(), 18);
            label.rectTransform.Stretch(132, 214, 0, 0);
            var go = StyledButton(root, "GoButton", "가기 ›", Tone.Primary, 26, out var goLabel);
            ((RectTransform)go.transform).At(MiddleRight, new Vector2(-14, 0), new Vector2(180, 70));
            var lockedButton = StyledButton(root, "LockedButton", "잠김", Tone.Secondary, 26, out _);
            ((RectTransform)lockedButton.transform).At(MiddleRight, new Vector2(-14, 0), new Vector2(180, 70));
            lockedButton.SetInactive();

            // 잠긴 줄: 이름 아래 자물쇠 28 + 짧은 이유 (이름은 ItemSourceRowView가 위로 올린다)
            var lockRow = NewRect("LockRow", root).Bottom(10, 32, 132, 214);
            LockIcon(lockRow, MiddleLeft, Vector2.zero, 28, UITheme.SubText);
            var lockText = Txt(lockRow, "Text", string.Empty, UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis().FitFloor(UIType.HintMin);
            lockText.rectTransform.Stretch(36, 0, 0, 0);
            lockRow.SetInactive();

            new Binder(view)
                .Set("_typeChip", chip)
                .Set("_typeText", type)
                .Set("_labelText", label)
                .Set("_goButton", go)
                .Set("_goLabel", goLabel)
                .Set("_group", group)
                .Set("_lockRow", lockRow.gameObject)
                .Set("_lockText", lockText)
                .Set("_lockedButton", lockedButton)
                .Apply();
            return SaveMenuPrefab<ItemSourceRowView>(bg.gameObject, $"{PrefabDir}/ItemSourceRow.prefab");
        }

        static UIPopup BuildExpeditionSetupPopup(SceneBuildContext ctx, RewardSlotView chipPrefab)
        {
            var popup = BuildPopup<ExpeditionSetupPopup>(ctx.PopupRoot, "ExpeditionSetupPopup", "원정 준비", new Vector2(960, 1600), out var body, out var title);

            var artMask = Img(body, "ArtMask", Color.white, RoundSprite);
            artMask.rectTransform.Top(0, 190);
            artMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var art = Img(artMask.transform, "Art", UITheme.Lavender);
            art.rectTransform.At(Center, Vector2.zero, new Vector2(888, 444));
            // 그림 아래 이름 띠: 밤 패널 (아트가 없으면 반투명 띠)
            var shade = UiArt.Panel(artMask.transform, "Shade", UiArt.PanelNight9s, 2.4f, WithAlpha(Color.white, 0.88f), WithAlpha(UITheme.Background, 0.6f));
            shade.rectTransform.Bottom(0, 56);
            var place = Txt(shade.transform, "Place", string.Empty, 28, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            place.rectTransform.Stretch(20, 20, 0, 0);

            var desc = Fit(Txt(body, "Desc", string.Empty, 24, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true), 17);
            desc.rectTransform.Top(204, 66);
            var info = Txt(body, "Info", string.Empty, 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            info.rectTransform.Top(276, 40);
            Img(body, "Divider", UITheme.Divider).rectTransform.Top(326, 2);

            Txt(body, "MembersHeader", "함께 갈 친구", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.At(TopLeft, new Vector2(0, -342), new Vector2(300, 50));
            var selectCount = Txt(body, "SelectCount", "0/3명", 26, UITheme.Text, TextAlignmentOptions.MidlineRight);
            selectCount.rectTransform.At(TopRight, new Vector2(-200, -346), new Vector2(180, 44));
            var auto = StyledButton(body, "AutoButton", "자동 선택", Tone.Secondary, 24, out _);
            var autoSize = new Vector2(184, 56);
            ((RectTransform)auto.transform).At(TopRight, new Vector2(0, -340), autoSize);
            // 누르는 자리 88 (M22): 위아래 16씩 (324~412 — 구분선 · 안내 글은 누르기 없음, 친구 격자 440에 닿지 않는다)
            auto.targetGraphic.HitPad(autoSize, 0f, UISize.MinHit, 0f, 0f, 16f, 16f);
            // 안내 한 줄 (M05 문장 — 원정 탭 머리의 안내는 M58로 지우고 여기로 옮겼다). 보낼 친구가 없으면 '모두 원정 중이에요'
            var hint = Txt(body, "Hint", "친구를 보내 재료를 모아요 · 원정 중에도 함께 싸워요", UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft)
                .Ellipsis().FitFloor(UIType.HintMin);
            hint.rectTransform.Top(402, 32);

            // 칸 바깥 6px 고른 테가 잘리지 않게 격자 둘레를 6~8 비운다 (4칸 x 208 + 간격 14 = 874 + 12 ≤ 888)
            var members = NewRect("Members", body).Top(440, 606);
            var memberContent = ScrollList(members, out _);
            AddGrid(memberContent, new Vector2(208, 250), new Vector2(14, 16), 4, new RectOffset(6, 6, 8, 12));
            var noMember = Txt(members, "NoMember", "아직 함께 갈 친구가 없어요. 소환에서 친구를 만나 보세요.", 26, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            noMember.rectTransform.Stretch();
            noMember.SetInactive();

            Txt(body, "DropsHeader", "예상 획득", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(1060, 46);
            var drops = ChipRow(body, "Drops", 96, 14);
            drops.At(TopLeft, new Vector2(0, -1112), new Vector2(888, 100));

            var start = StyledButton(body, "StartButton", "출발", Tone.Gold, 34, out var startLabel);
            ((RectTransform)start.transform).At(BottomCenter, Vector2.zero, new Vector2(480, 110));
            var tierRow = BuildTierRow(body, out var tierButtons, out var tierTimes, out var tierScales);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_art", art)
                .Set("_artFallbackText", place)
                .Set("_descText", desc)
                .Set("_infoText", info)
                .Set("_selectCountText", selectCount)
                .Set("_memberContent", memberContent)
                .Set("_memberPrefab", BuildMemberCellPrefab())
                .Set("_noMemberLabel", noMember.gameObject)
                .Set("_dropContent", drops)
                .Set("_dropPrefab", chipPrefab)
                .Set("_autoButton", auto)
                .Set("_startButton", start)
                .Set("_startLabel", startLabel)
                .Set("_hintText", hint)
                .Set("_tierRow", tierRow)
                .SetArray("_tierButtons", tierButtons)
                .SetArray("_tierTimeTexts", tierTimes)
                .SetArray("_tierScaleTexts", tierScales)
                .Apply();
            return popup;
        }

        /// <summary>원정 준비 '다녀올 시간' 줄 위 끝 (예상 획득 칩 1112~1212 아래 10, 출발 버튼 1318 위 8) · 칸 240x88 · 사이 12</summary>
        const float TierRowTop = 1222f;
        static readonly Vector2 TierOptionSize = new(240, 88);
        const float TierOptionGap = 12f;

        /// <summary>
        /// 원정 준비 '다녀올 시간' 줄 (IDLE-04, SPEC4 §5.3): 왼쪽 이름 24 굵게 + 오른쪽 칸 고르기 3칸 240x88 (M53 Option — 흰 바느질 탭 그림, 고르면 보라 + 크림 테두리,
        /// ExpeditionSetupPopup이 TabStyle.Apply로 칠한다): 1줄 시간 26 굵게 · 2줄 '보상 ×2.8' Caption 22. 꺼 둔다 (고를 수 있는 단계가 둘 이상일 때만 켠다)
        /// </summary>
        static GameObject BuildTierRow(RectTransform body, out Object[] buttons, out Object[] times, out Object[] scales)
        {
            var row = NewRect("TierRow", body).Top(TierRowTop, TierOptionSize.y);
            Txt(row, "Label", "다녀올 시간", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.HintMin)
                .rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(140, 60));
            int count = ExpeditionSystem.DurationTierCount;
            buttons = new Object[count];
            times = new Object[count];
            scales = new Object[count];
            for (int i = 0; i < count; i++)
            {
                float right = (count - 1 - i) * (TierOptionSize.x + TierOptionGap);
                var bg = UiArt.Panel(row, $"Tier_{i}", UiArt.Tab9s, 1.6f, UITheme.Secondary, UITheme.Secondary, true);
                bg.rectTransform.At(MiddleRight, new Vector2(-right, 0), TierOptionSize);
                var edge = bg.gameObject.AddComponent<Outline>();
                edge.enabled = false;
                var button = AddPlainButton(bg);
                var time = Txt(bg.transform, "Time", "30분", 26, UITheme.SubText, TextAlignmentOptions.Center, true).FitFloor(UIType.Hint);
                time.rectTransform.Top(10, 36, 10, 10);
                var scale = Txt(bg.transform, "Scale", "보상 ×1", UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center).FitFloor(UIType.HintMin);
                scale.rectTransform.Top(46, 30, 10, 10);
                buttons[i] = button;
                times[i] = time;
                scales[i] = scale;
            }
            row.SetInactive();
            return row.gameObject;
        }

        /// <summary>
        /// 원정 준비 친구 칸 208x250: 초상화 156 · 편성 중 알약('지킴이' / '잔향이' 120x26, 초상화 아래 끝) · 이름 24 · 'Lv.N ★' 22 ·
        /// 고르면 바깥 6px Lamp 테 + 금빛 순서 배지 (테두리는 등급 색 그대로 — C-06) · 못 보내면 사유 덮개.
        /// </summary>
        static ExpeditionMemberCellView BuildMemberCellPrefab()
        {
            // 크림 양피지 칸(누르는 자리) + 테두리만 있는 흰 칸 틀(등급 색 / 고르면 금빛 — ExpeditionMemberCellView._frame)
            var cell = Card(null, "MemberCell", true);
            var root = cell.rectTransform;
            root.sizeDelta = new Vector2(208, 250);
            var button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = cell;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            cell.gameObject.AddComponent<PressScale>();
            var view = cell.gameObject.AddComponent<ExpeditionMemberCellView>();

            // 고른 칸 바깥 6px Lamp 테 (흰 칸 틀을 불빛 색으로 — 맨 뒤, 꺼 둠)
            var selectedRing = UiArt.Panel(root, "SelectedRing", UiArt.SlotFrame9s, 1.4f, UITheme.Lamp, UITheme.Lamp);
            selectedRing.rectTransform.Stretch(-6, -6, -6, -6);
            selectedRing.transform.SetAsFirstSibling();
            selectedRing.SetInactive();
            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2f, UITheme.GradeColor(ItemGrade.Common));
            frame.rectTransform.Stretch();
            UiArt.Icon(root, "PortraitBg", UiArt.PlateRound, TopCenter, new Vector2(0, -12), 144);
            var portrait = Portrait(root, "Portrait", TopCenter, new Vector2(0, -6), new Vector2(156, 156), 54);
            // 편성 중 알약: 초상화 아래 끝에 걸쳐 (Cream 바탕 · Ink 18 굵게)
            var placed = UiArt.Panel(root, "PlacedPill", UiArt.PillWhite9s, UiArt.CapsulePpu(26), UITheme.Cream, UITheme.Cream);
            placed.rectTransform.At(TopCenter, new Vector2(0, -138), new Vector2(120, 26));
            var placedText = Txt(placed.transform, "Text", "지킴이", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            placedText.rectTransform.Stretch(6, 6, 0, 0);
            placed.SetInactive();
            var name = Fit(Txt(root, "Name", "친구", 24, UITheme.Ink, TextAlignmentOptions.Center, true).Ellipsis(), 18);
            name.rectTransform.Top(166, 34, 8, 8);
            var level = Txt(root, "Level", "Lv.1", UIType.Caption, UITheme.InkSub).FitFloor(UIType.AutoSizeFloor);
            level.rectTransform.Top(200, 32, 8, 8);

            var disabled = UiArt.Panel(root, "Disabled", UiArt.PanelInset9s, 2f, new Color(0.1f, 0.08f, 0.2f, 0.7f), WithAlpha(UITheme.Background, 0.66f));
            disabled.rectTransform.Stretch(5, 5, 5, 5);
            var reason = Txt(disabled.transform, "Reason", string.Empty, UIType.Hint, UITheme.Cream, TextAlignmentOptions.Center, true, true).FitFloor(UIType.AutoSizeFloor);
            reason.rectTransform.Stretch(12, 12, 40, 40);
            disabled.SetInactive();

            // 고른 순서: 금빛 단추 점 + 숫자
            var check = UiArt.Icon(root, "Check", UiArt.PipButton, TopRight, new Vector2(-6, -6), 54);
            check.color = UITheme.Gold;
            var order = Txt(check.transform, "Order", "1", 28, UITheme.Ink, TextAlignmentOptions.Center, true);
            order.rectTransform.Stretch();
            check.SetInactive();

            new Binder(view)
                .Set("_button", button)
                .Set("_frame", frame)
                .Set("_portrait", portrait)
                .Set("_nameText", name)
                .Set("_levelText", level)
                .Set("_check", check.gameObject)
                .Set("_orderText", order)
                .Set("_disabled", disabled.gameObject)
                .Set("_reasonText", reason)
                .Set("_selectedRing", selectedRing.gameObject)
                .Set("_placedPill", placed.gameObject)
                .Set("_placedText", placedText)
                .Apply();
            return SaveMenuPrefab<ExpeditionMemberCellView>(cell.gameObject, $"{PrefabDir}/ExpeditionMemberCell.prefab");
        }

        static UIPopup BuildDungeonSweepPopup(SceneBuildContext ctx, RewardSlotView chipPrefab)
        {
            var popup = BuildPopup<DungeonSweepPopup>(ctx.PopupRoot, "DungeonSweepPopup", "잔향이 던전 소탕", new Vector2(860, 820), out var body, out _);

            Txt(body, "Info", "돌파한 층을 골라 바로 보상을 받아요.", 24, UITheme.SubText).rectTransform.Top(0, 40);
            var stepper = NewRect("Stepper", body).Top(60, 110);
            var min = StepButton(stepper, "MinButton", "1층", -320, 110, 26);
            var prev = StepButton(stepper, "PrevButton", "<", -196, 100, 40);
            var floor = Txt(stepper, "Floor", "1층", 56, UITheme.Cream, TextAlignmentOptions.Center, true);
            floor.rectTransform.At(Center, Vector2.zero, new Vector2(240, 100));
            var next = StepButton(stepper, "NextButton", ">", 196, 100, 40);
            var max = StepButton(stepper, "MaxButton", "최고", 320, 110, 26);

            // 여러 장 소탕 (IDLE-05, SPEC4 §5.2): 층 줄 아래 '[-] ×{n}장 [+]' (잠기면 숨기고 아래 칸 Details를 SweepCountShift만큼 올린다 — DungeonSweepPopup)
            var countRow = NewRect("CountRow", body).Top(SweepCountTop, SweepCountHeight);
            var countMinus = StepButton(countRow, "CountMinusButton", "-", -170, 100, 40);
            var countText = Txt(countRow, "Count", UIFormat.Times + "1장", 40, UITheme.Cream, TextAlignmentOptions.Center, true);
            countText.rectTransform.At(Center, Vector2.zero, new Vector2(220, 88));
            var countPlus = StepButton(countRow, "CountPlusButton", "+", 170, 100, 40);
            countRow.SetInactive();

            // 아래 칸 (구분선 · 보상 · 입장권): 여러 장 줄이 보일 때 자리 — 숨기면 SweepCountShift만큼 올라가 예전 자리(186 · 204 · 254 · 384)가 된다
            var details = NewRect("Details", body).Stretch();
            Img(details, "Divider", UITheme.Divider).rectTransform.Top(186 + SweepCountShift, 2);
            var rewardHeader = Txt(details, "RewardHeader", string.Empty, 26, UITheme.Cream, TextAlignmentOptions.Center, true);
            rewardHeader.rectTransform.Top(204 + SweepCountShift, 40);
            var rewards = ChipRow(details, "Rewards", 104, 14);
            rewards.At(TopCenter, new Vector2(0, -(254 + SweepCountShift)), new Vector2(788, 110));
            rewards.GetComponent<GridLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var ticket = Txt(details, "Ticket", string.Empty, 24, UITheme.SubText);
            ticket.rectTransform.Top(384 + SweepCountShift, 40);

            var row = NewRect("Buttons", body).Bottom(0, 104);
            AddRow(row, 20);
            var cancel = StyledButton(row, "CancelButton", "취소", Tone.Secondary, 32, out _);
            var sweep = StyledButton(row, "SweepButton", "소탕", Tone.Gold, 34, out var sweepLabel);

            new Binder(popup)
                .Set("_floorText", floor)
                .Set("_ticketText", ticket)
                .Set("_minButton", min)
                .Set("_prevButton", prev)
                .Set("_nextButton", next)
                .Set("_maxButton", max)
                .Set("_sweepButton", sweep)
                .Set("_cancelButton", cancel)
                .Set("_rewardHeader", rewardHeader)
                .Set("_rewardContent", rewards)
                .Set("_rewardPrefab", chipPrefab)
                .Set("_countRow", countRow.gameObject)
                .Set("_countMinus", countMinus)
                .Set("_countPlus", countPlus)
                .Set("_countText", countText)
                .Set("_sweepLabel", sweepLabel)
                .Set("_details", details)
                .Apply();
            // (_countShift는 DungeonSweepPopup의 기본값 100 = SweepCountShift — Binder에 실수 값 쓰기가 없어 둘을 같이 고친다)
            return popup;
        }

        /// <summary>던전 소탕 '[-] ×n장 [+]' 줄 위 끝 · 높이 (층 줄 60~170 아래 12) · 줄을 숨기면 아래 칸이 올라가는 거리 (줄 88 + 사이 12)</summary>
        const float SweepCountTop = 182f;
        const float SweepCountHeight = 88f;
        const float SweepCountShift = 100f;

        static Button StepButton(RectTransform parent, string name, string label, float x, float width, float fontSize)
        {
            var button = StyledButton(parent, name, label, Tone.Secondary, fontSize, out _);
            ((RectTransform)button.transform).At(Center, new Vector2(x, 0), new Vector2(width, 92));
            return button;
        }

        /// <summary>UINav.SourceRequested 처리기 (항상 켜져 있는 SafeArea 아래)</summary>
        static void BuildSourceNavigator(SceneBuildContext ctx)
        {
            var navigator = NewRect("MenuNavigator", ctx.SafeArea).gameObject.AddComponent<SourceNavigator>();
            new Binder(navigator)
                .Set("_expedition", ctx.ContentArea.GetComponentInChildren<ExpeditionPanel>(true))
                .Set("_shop", ctx.ContentArea.GetComponentInChildren<ShopPanel>(true))
                .Apply();
        }

        /// <summary>원정 탭 버튼에 빨간 점 (받을 원정 + 남은 던전 입장권)</summary>
        static void BuildExpeditionTabBadge(SceneBuildContext ctx)
        {
            var tab = ctx.SafeArea.Find("TabBar/Tab_ExpeditionPanel");
            if (tab == null) return;
            var badge = BuildBadge(tab);
            ((RectTransform)badge.transform).anchoredPosition = new Vector2(-30, -8);
            var updater = tab.gameObject.AddComponent<ExpeditionTabBadge>();
            new Binder(updater).Set("_badge", badge).Apply();
        }

        // ═════════════════════════ 시너지 ═════════════════════════

        /// <summary>시너지 조합 ID (SPEC2). 콘텐츠에 없는 아이콘도 미리 연결해 둔다.</summary>
        static readonly string[] ComboIds =
        {
            "combo_mending_duo", "combo_rainy_lantern", "combo_locked_musicbox", "combo_vanity_secret",
            "combo_night_walk", "combo_parade", "combo_key_and_lock", "combo_shop_family",
            "combo_clear_sky", "combo_last_song", "combo_waiting_end", "combo_seven_lights",
        };

        /// <summary>UINav.SynergyOpenRequested 처리기 + 시너지 아이콘 묶음 (항상 켜져 있는 SafeArea 아래)</summary>
        static void BuildSynergyNavigator(SceneBuildContext ctx)
        {
            var navigator = NewRect("SynergyNavigator", ctx.SafeArea).gameObject.AddComponent<SynergyNavigator>();
            new Binder(navigator)
                .SetArray("_traitIcons", EnumSprites<TraitType>(t => ArtLib.Trait(t)))
                .SetArray("_styleIcons", EnumSprites<CombatStyle>(s => ArtLib.Style(s)))
                .SetArray("_stageIcons", EnumSprites<StageModifierType>(m => m == StageModifierType.None ? null : ArtLib.StageModifier(m)))
                .SetArray("_comboIcons", ComboSprites(ctx))
                .Set("_placeholder", null)
                .Set("_socket", UiArt.IsArt(UiArt.MedallionDark) ? UiArt.MedallionDark : null)
                .Set("_plate", UiArt.IsArt(UiArt.PlateRound) ? UiArt.PlateRound : null)
                .Apply();
        }

        /// <summary>enum 값 순서(정수값 = 인덱스)대로 스프라이트 배열</summary>
        static Object[] EnumSprites<T>(System.Func<T, Sprite> load) where T : System.Enum
        {
            var values = (T[])System.Enum.GetValues(typeof(T));
            int max = 0;
            foreach (var value in values) max = Mathf.Max(max, System.Convert.ToInt32(value));
            var sprites = new Object[max + 1];
            foreach (var value in values) sprites[System.Convert.ToInt32(value)] = load(value);
            return sprites;
        }

        static Object[] ComboSprites(SceneBuildContext ctx)
        {
            var ids = new List<string>(ComboIds);
            var db = ctx.Content != null ? ctx.Content.Synergies : null;
            if (db != null)
            {
                foreach (var def in db.synergies)
                {
                    if (def != null && def.kind == SynergyKind.Combo && !string.IsNullOrEmpty(def.id) && !ids.Contains(def.id)) ids.Add(def.id);
                }
            }
            var sprites = new List<Object>();
            foreach (var id in ids)
            {
                var sprite = ArtLib.Synergy(id);
                if (sprite != null) sprites.Add(sprite);
            }
            return sprites.ToArray();
        }

        /// <summary>
        /// 편성 화면 시너지 한 줄 (376x52, M44): 아이콘 고리 44 (x 5) + 둘레 실 고리(종류 색) · 이름 21 굵게 (56, −2) 190x28 · 진행 20 굵게 오른쪽 110x28 ·
        /// 단계 점 12 (x 56부터 16 간격, y −33) · ×1.5 알약 64x26 · 발동 배지 64x26 (18 굵게, Ink on Gold). 종류 칩은 꺼 둔다 (연결은 그대로).
        /// </summary>
        static SynergyRowView BuildSynergyRowPrefab()
        {
            var root = NewRect("SynergyRow", null);
            root.sizeDelta = SynergyRowSize;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<SynergyRowView>();

            // 미리보기 발광 = 흰 칸 틀을 불빛 색으로, 바탕 = 보라 바느질 판, 그 위 테두리 = 흰 칸 틀(강조색)
            var glow = UiArt.Panel(root, "Glow", UiArt.SlotFrame9s, 2.6f, UITheme.Lamp, UITheme.Lamp);
            glow.rectTransform.Stretch(-4, -4, -3, -3);
            Pulse(glow, 0.015f, 0.3f, 1f);
            glow.SetInactive();
            var bg = UiArt.Panel(root, "Bg", UiArt.PanelInset9s, 3f, null, UITheme.PanelLight, true);
            bg.rectTransform.Stretch(1, 1, 1, 1);
            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 3f, UITheme.Lavender);
            frame.rectTransform.Stretch();
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<PressScale>();

            // 아이콘 홈: 어두운 메달 (특성/조합 메달 아이콘이면 런타임에 숨긴다 — SynergyIcons.ApplyInRing)
            var ring = MedallionSocket(root, MiddleLeft, new Vector2(5, 0), 44);
            var icon = SynergyIcon(ring.transform, 6);
            var fallback = Txt(icon.transform, "Fallback", "?", 20, UITheme.Cream, TextAlignmentOptions.Center, true);
            fallback.rectTransform.Stretch();
            // 종류 색 실 고리 (특성 라벤더 · 조합 더스티 핑크 · 역할 소프트 블루 — SynergyRowView가 칠한다). 아이콘 위에 둘러 메달 그림도 감싼다
            var kindRing = Img(root, "KindRing", UITheme.Lavender, UiArt.RingStitch);
            kindRing.type = Image.Type.Simple;
            kindRing.preserveAspect = true;
            kindRing.rectTransform.At(MiddleLeft, new Vector2(2, 0), new Vector2(50, 50));

            var name = Txt(root, "NameText", "시너지", 21, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis().FitFloor(17f);
            name.rectTransform.At(TopLeft, new Vector2(56, -2), new Vector2(190, 28));
            var progress = Txt(root, "ProgressText", "0/2", UIType.Hint, UITheme.Text, TextAlignmentOptions.MidlineRight, true).FitFloor(UIType.AutoSizeFloor);
            progress.rectTransform.At(TopRight, new Vector2(-10, -2), new Vector2(110, 28));

            // 종류 칩: 줄이 좁아 꺼 둔다 (고리 색이 대신 말한다 — 연결은 그대로)
            var kind = UiArt.Panel(root, "KindChip", UiArt.PillWhite9s, UiArt.CapsulePpu(26), UITheme.Lavender);
            kind.rectTransform.At(TopLeft, new Vector2(56, -25), new Vector2(52, 26));
            var kindText = Txt(kind.transform, "Text", "특성", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            kindText.rectTransform.Stretch(2, 2, 0, 0);
            kind.SetInactive();

            var pips = new Object[4];
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = UiArt.Icon(root, $"Pip_{i}", UiArt.PipButton, TopLeft, new Vector2(56 + i * 16, -33), 12);
                pip.color = UITheme.Divider;
                pips[i] = pip;
            }

            var favored = UiArt.Panel(root, "FavoredTag", UiArt.PillWhite9s, UiArt.CapsulePpu(26), WithAlpha(UITheme.Lamp, 0.95f));
            favored.rectTransform.At(TopRight, new Vector2(-80, -25), new Vector2(64, 26));
            var favoredText = Txt(favored.transform, "Text", "×1.5", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            favoredText.rectTransform.Stretch(2, 2, 0, 0);
            favored.SetInactive();

            var delta = UiArt.Panel(root, "DeltaBadge", UiArt.PillWhite9s, UiArt.CapsulePpu(26), UITheme.Gold);
            delta.rectTransform.At(TopRight, new Vector2(-10, -25), new Vector2(64, 26));
            var deltaText = Txt(delta.transform, "Text", "발동", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            deltaText.rectTransform.Stretch(2, 2, 0, 0);
            delta.SetInactive();

            new Binder(view)
                .Set("_button", button)
                .Set("_group", group)
                .Set("_bg", bg)
                .Set("_frame", frame)
                .Set("_glow", glow)
                .Set("_iconRing", ring)
                .Set("_icon", icon)
                .Set("_iconFallback", fallback)
                .Set("_nameText", name)
                .Set("_kindChip", kind)
                .Set("_kindText", kindText)
                .Set("_progressText", progress)
                .SetArray("_pips", pips)
                .Set("_favoredTag", favored.gameObject)
                .Set("_favoredText", favoredText)
                .Set("_deltaBadge", delta)
                .Set("_deltaText", deltaText)
                .Set("_kindRing", kindRing)
                .Apply();
            return SaveMenuPrefab<SynergyRowView>(root.gameObject, $"{PrefabDir}/SynergyRow.prefab");
        }

        /// <summary>
        /// 시너지 상세 팝업: 큰 아이콘 · 종류 · 상태/진행 막대 · 설명 · 강해지는 스테이지 기믹 · 단계별 효과 · 관여 친구.
        /// 내용 길이가 시너지마다 달라서 세로 레이아웃(높이 자동)으로 쌓는다.
        /// </summary>
        static UIPopup BuildSynergyDetailPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<SynergyDetailPopup>(ctx.PopupRoot, "SynergyDetailPopup", "시너지", new Vector2(960, 1680), out var body, out var title);
            Fit(title.Ellipsis(), 28);

            var buttons = NewRect("Buttons", body).Bottom(0, 100);
            AddRow(buttons, 20);
            var codex = StyledButton(buttons, "CodexButton", "시너지 도감", Tone.Secondary, 30, out _);
            var formation = StyledButton(buttons, "FormationButton", "편성하러 가기", Tone.Primary, 30, out _);

            var scrollRoot = NewRect("Scroll", body).Stretch(0, 0, 0, 116);
            var content = ScrollList(scrollRoot, out var scroll);
            VList(content, 18, new RectOffset(4, 4, 4, 24));

            // ── 머리 (아이콘 + 종류 + 상태 + 진행 막대) ──
            var header = Section(content, "Header", 228);
            var iconGlow = Glow(header, "IconGlow", UITheme.Lavender, TopLeft, new Vector2(-30, 30), new Vector2(252, 252));
            var ring = MedallionSocket(header, TopLeft, new Vector2(8, -8), 180);
            var icon = SynergyIcon(ring.transform, 26);
            var iconFallback = Txt(icon.transform, "Fallback", "?", 66, UITheme.Cream, TextAlignmentOptions.Center, true);
            iconFallback.rectTransform.Stretch();

            var kind = UiArt.Panel(header, "KindChip", UiArt.PillWhite9s, UiArt.CapsulePpu(38), UITheme.Lavender);
            kind.rectTransform.At(TopLeft, new Vector2(212, -10), new Vector2(88, 38));
            var kindText = Txt(kind.transform, "Text", "특성", 21, UITheme.Ink, TextAlignmentOptions.Center, true);
            kindText.rectTransform.Stretch();
            var subtitle = Fit(Txt(header, "Subtitle", string.Empty, 22, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis(), 16);
            subtitle.rectTransform.At(TopLeft, new Vector2(314, -10), new Vector2(566, 38));
            var status = Fit(Txt(header, "Status", string.Empty, 28, UITheme.Cream, TextAlignmentOptions.TopLeft, true, true), 18);
            status.lineSpacing = 2;
            status.rectTransform.At(TopLeft, new Vector2(212, -58), new Vector2(668, 88));
            var progressGroup = NewRect("ProgressGroup", header).At(TopLeft, new Vector2(212, -152), new Vector2(668, 66));
            var progress = BuildProgressBar(progressGroup, "Progress", 110, 30, 18);
            ((RectTransform)progress.transform).Stretch();

            var desc = Txt(content, "Desc", string.Empty, 25, UITheme.Text, TextAlignmentOptions.TopLeft, false, true);
            desc.lineSpacing = 6;

            // ── 강해지는 스테이지 기믹 (보라 바느질 판 + 기믹 아이콘 그림, 원판 없이) ──
            var favored = UiArt.Panel(content, "Favored", UiArt.PanelInset9s, 2f, null, UITheme.PanelLight);
            Section(favored.rectTransform, 88);
            var stageIcon = UiArt.Icon(favored.transform, "StageIcon", null, MiddleLeft, new Vector2(18, 0), 56);
            stageIcon.enabled = false;
            var stageFallback = Txt(stageIcon.transform, "Fallback", "?", 26, UITheme.Cream, TextAlignmentOptions.Center, true);
            stageFallback.rectTransform.Stretch();
            var favoredText = Fit(Txt(favored.transform, "Text", string.Empty, 25, UITheme.Text, TextAlignmentOptions.MidlineLeft, false, true), 17);
            favoredText.rectTransform.Stretch(96, 186, 4, 4);
            var nowTag = Pill(favored.transform, "NowTag", "지금 발동 중!", 21, UITheme.Lamp, UITheme.Ink, MiddleRight, new Vector2(-16, 0), new Vector2(162, 44));
            Pulse(nowTag, 0.04f, 0.65f, 1f);
            nowTag.SetInactive();

            // ── 단계별 효과 ──
            SectionTitle(content, "TierHeader", "단계별 효과");
            var tiers = NewRect("Tiers", content);
            VList(tiers, 12, new RectOffset(0, 0, 0, 0));

            // ── 관여 친구 ──
            var membersTitle = SectionTitle(content, "MemberHeader", "함께하는 친구");
            var membersHint = Txt(content, "MemberHint", string.Empty, 22, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            var members = NewRect("Members", content);
            var grid = members.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 214);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperLeft;

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_scroll", scroll)
                .Set("_iconGlow", iconGlow)
                .Set("_iconRing", ring)
                .Set("_icon", icon)
                .Set("_iconFallback", iconFallback)
                .Set("_kindChip", kind)
                .Set("_kindText", kindText)
                .Set("_subtitleText", subtitle)
                .Set("_statusText", status)
                .Set("_progress", progress)
                .Set("_progressGroup", progressGroup.gameObject)
                .Set("_descText", desc)
                .Set("_favoredGroup", favored.gameObject)
                .Set("_favoredBg", favored)
                .Set("_favoredIcon", stageIcon)
                .Set("_favoredFallback", stageFallback)
                .Set("_favoredText", favoredText)
                .Set("_favoredNowTag", nowTag.gameObject)
                .Set("_tierContent", tiers)
                .Set("_tierPrefab", BuildSynergyTierRowPrefab())
                .Set("_membersTitle", membersTitle)
                .Set("_membersHint", membersHint)
                .Set("_memberContent", members)
                .Set("_memberPrefab", BuildSynergyMemberCellPrefab())
                .Set("_codexButton", codex)
                .Set("_formationButton", formation)
                .Apply();
            return popup;
        }

        /// <summary>시너지 단계 한 줄 (높이 자동): 문턱 배지 · "3명 이상" · 범위 칩 · 상태 칩 / 설명 / 능력치 / 보너스 스킬</summary>
        static SynergyTierRowView BuildSynergyTierRowPrefab()
        {
            var root = NewRect("SynergyTierRow", null);
            root.sizeDelta = new Vector2(880, 160);
            var view = root.gameObject.AddComponent<SynergyTierRowView>();
            VList(root, 6, new RectOffset(20, 20, 14, 18));

            // 바탕 = 보라 바느질 판, 그 위 테두리 = 흰 칸 틀(상태 색). 둘 다 레이아웃에서 뺀다
            var bg = UiArt.Panel(root, "Bg", UiArt.PanelInset9s, 2f, null, UITheme.PanelLight);
            Ignore(bg).rectTransform.Stretch(1, 1, 1, 1);
            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2.2f, UITheme.Divider);
            Ignore(frame).rectTransform.Stretch();

            var header = Section(root, "Header", 50);
            // 문턱 배지: 단추 점 그림(라벤더 → 런타임에 강조색) + 숫자
            var badge = UiArt.Icon(header, "Badge", UiArt.PipButton, MiddleLeft, Vector2.zero, 50);
            badge.color = UITheme.Lavender;
            var badgeText = Fit(Txt(badge.transform, "Text", "2", 24, UITheme.Ink, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            badgeText.rectTransform.Stretch(4, 4, 0, 0);
            var title = Fit(Txt(header, "Title", "2명 이상", 27, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis(), 18);
            title.rectTransform.At(MiddleLeft, new Vector2(64, 0), new Vector2(380, 44));
            var scope = UiArt.Panel(header, "ScopeChip", UiArt.PillWhite9s, UiArt.CapsulePpu(36), UITheme.Lavender);
            scope.rectTransform.At(MiddleRight, new Vector2(-152, 0), new Vector2(116, 36));
            var scopeText = Fit(Txt(scope.transform, "Text", "구성원만", 19, UITheme.Cream, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            scopeText.rectTransform.Stretch(6, 6, 0, 0);
            var state = UiArt.Panel(header, "StateChip", UiArt.PillWhite9s, UiArt.CapsulePpu(40), UITheme.Secondary);
            state.rectTransform.At(MiddleRight, Vector2.zero, new Vector2(140, 40));
            var stateText = Fit(Txt(state.transform, "Text", "발동 중", 21, UITheme.Ink, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            stateText.rectTransform.Stretch(6, 6, 0, 0);

            var desc = Txt(root, "Desc", string.Empty, 23, UITheme.Text, TextAlignmentOptions.TopLeft, false, true);
            desc.lineSpacing = 4;
            var modifiers = Txt(root, "Modifiers", string.Empty, 23, UITheme.Positive, TextAlignmentOptions.TopLeft, true, true);
            modifiers.lineSpacing = 6;

            var skill = NewRect("Skill", root);
            VList(skill, 0, new RectOffset(16, 16, 10, 12));
            var skillBg = UiArt.Panel(skill, "Bg", UiArt.PanelInset9s, 2.4f, Color.Lerp(Color.white, UITheme.Gold, 0.3f), WithAlpha(UITheme.Gold, 0.14f));
            Ignore(skillBg).rectTransform.Stretch();
            var skillText = Txt(skill, "Text", string.Empty, 22, UITheme.Text, TextAlignmentOptions.TopLeft, false, true);
            skillText.lineSpacing = 4;
            skill.gameObject.SetActive(false);

            new Binder(view)
                .Set("_bg", bg)
                .Set("_frame", frame)
                .Set("_badge", badge)
                .Set("_badgeText", badgeText)
                .Set("_titleText", title)
                .Set("_scopeChip", scope)
                .Set("_scopeText", scopeText)
                .Set("_stateChip", state)
                .Set("_stateText", stateText)
                .Set("_descText", desc)
                .Set("_modifierText", modifiers)
                .Set("_skillGroup", skill.gameObject)
                .Set("_skillText", skillText)
                .Apply();
            return SaveMenuPrefab<SynergyTierRowView>(root.gameObject, $"{PrefabDir}/SynergyTierRow.prefab");
        }

        /// <summary>
        /// 시너지 상세 친구 칸 (160x214): 초상화 · 이름 · 상태 칩. 테두리는 늘 등급 색 (C-06 — 금빛 테두리가 전설 등급과 헷갈리지 않게),
        /// 편성 중이면 칸 밖 6px Lamp 테 + 오른쪽 위 체크 + 은은한 빛.
        /// </summary>
        static SynergyMemberCellView BuildSynergyMemberCellPrefab()
        {
            var hit = Img(null, "SynergyMemberCell", Color.clear, null, true);
            var root = hit.rectTransform;
            root.sizeDelta = new Vector2(160, 214);
            var view = hit.gameObject.AddComponent<SynergyMemberCellView>();
            var button = AddPlainButton(hit);

            var glow = Glow(root, "PlacedGlow", WithAlpha(UITheme.Lamp, 0.8f), Center, Vector2.zero, new Vector2(214, 268));
            Pulse(glow, 0.03f, 0.3f, 0.85f);
            glow.SetInactive();
            var placedRing = SelectionRing(root, "PlacedRing");
            // 보라 바느질 판 + 흰 칸 틀(등급 색) + 둥근 받침
            var inner = UiArt.Panel(root, "Inner", UiArt.PanelInset9s, 2f, null, UITheme.Parchment);
            inner.rectTransform.Stretch(2, 2, 2, 2);
            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2f, UITheme.GradeColor(ItemGrade.Common));
            frame.rectTransform.Stretch();
            UiArt.Icon(root, "PortraitBg", UiArt.PlateRound, TopCenter, new Vector2(0, -10), 130);
            var portrait = Portrait(root, "Portrait", TopCenter, new Vector2(0, -4), new Vector2(138, 138), 48);

            var lockRoot = NewRect("Lock", root).Stretch();
            LockIcon(lockRoot, TopRight, new Vector2(-10, -10), 30, UITheme.InkLine);
            lockRoot.gameObject.SetActive(false);
            var check = SelectionCheck(root, "PlacedCheck", new Vector2(-6, -6));

            var name = Fit(Txt(root, "Name", "친구", 20, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis(), UIType.AutoSizeFloor);
            name.rectTransform.Top(146, 30, 8, 8);
            var state = UiArt.Panel(root, "StateChip", UiArt.PillWhite9s, UiArt.CapsulePpu(30), UITheme.Lavender);
            state.rectTransform.Bottom(10, 30, 12, 12);
            var stateText = Fit(Txt(state.transform, "Text", "보유", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            stateText.rectTransform.Stretch(4, 4, 0, 0);

            new Binder(view)
                .Set("_button", button)
                .Set("_frame", frame)
                .Set("_inner", inner)
                .Set("_placedGlow", glow.gameObject)
                .Set("_portrait", portrait)
                .Set("_nameText", name)
                .Set("_stateChip", state)
                .Set("_stateText", stateText)
                .Set("_lockMark", lockRoot.gameObject)
                .Set("_placedRing", placedRing)
                .Set("_placedCheck", check)
                .Apply();
            return SaveMenuPrefab<SynergyMemberCellView>(hit.gameObject, $"{PrefabDir}/SynergyMemberCell.prefab");
        }

        /// <summary>고름 · 편성 중 바깥 테: 칸 밖으로 6px 나온 흰 칸 틀을 Lamp로 (테두리 색은 등급 색 그대로 둔다 — C-06). 꺼 둔 채 돌려준다</summary>
        static GameObject SelectionRing(RectTransform cell, string name)
        {
            var ring = UiArt.Panel(cell, name, UiArt.SlotFrame9s, 1.6f, UITheme.Lamp, UITheme.Lamp);
            ring.rectTransform.Stretch(-6, -6, -6, -6);
            ring.SetInactive();
            return ring.gameObject;
        }

        /// <summary>고름 · 편성 중 체크 (Icons/check 34, 오른쪽 위). 그림이 없으면 금빛 단추 점. 꺼 둔 채 돌려준다</summary>
        static GameObject SelectionCheck(RectTransform cell, string name, Vector2 position)
        {
            var art = UiArt.Check;
            var check = UiArt.Icon(cell, name, UiArt.IsArt(art) ? art : UiArt.PipButton, TopRight, position, 34);
            if (!UiArt.IsArt(art)) check.color = UITheme.Gold;
            check.SetInactive();
            return check.gameObject;
        }

        /// <summary>
        /// 시너지 도감 팝업: 요약(켜진 시너지 수 · 만들 수 있는 조합 · 이번 스테이지 기믹) / 특성·역할·조합 탭 / 시너지 목록.
        /// </summary>
        static UIPopup BuildSynergyCodexPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<SynergyCodexPopup>(ctx.PopupRoot, "SynergyCodexPopup", "시너지 도감", new Vector2(960, 1700), out var body, out _);

            // 요약 띠
            var summaryBox = UiArt.Panel(body, "SummaryBox", UiArt.PanelInset9s, 2f, null, WithAlpha(UITheme.PanelLight, 0.6f));
            summaryBox.rectTransform.Top(0, 110);
            var summary = Fit(Txt(summaryBox.transform, "Summary", string.Empty, 27, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true, true), 17);
            // 세 줄 (켜진 수 · 만들 수 있는 조합 · 편성 규칙 70%) — 셋째 줄이 18px 아래로 줄지 않게 높이 104
            summary.rectTransform.At(MiddleLeft, new Vector2(24, 0), new Vector2(420, 104));
            Img(summaryBox.transform, "Split", WithAlpha(UITheme.GoldAccent, 0.4f)).rectTransform.At(MiddleLeft, new Vector2(456, 0), new Vector2(2, 70));
            // 이번 스테이지 기믹 아이콘 그림 (원판 없이 68px, 기믹 없으면 stage_calm)
            var calmIcon = ArtLib.Icon("stage_calm");
            var stageIcon = UiArt.Icon(summaryBox.transform, "StageIcon", calmIcon, MiddleLeft, new Vector2(474, 0), 68);
            stageIcon.enabled = calmIcon != null;
            var stageFallback = Txt(stageIcon.transform, "Fallback", "?", 26, UITheme.Cream, TextAlignmentOptions.Center, true);
            stageFallback.SetInactive();
            stageFallback.rectTransform.Stretch();
            var stage = Fit(Txt(summaryBox.transform, "StageText", string.Empty, 25, UITheme.Text, TextAlignmentOptions.MidlineLeft, false, true), 16);
            stage.lineSpacing = 2;
            stage.rectTransform.At(MiddleLeft, new Vector2(554, 0), new Vector2(320, 98));

            // 탭 (특성 / 역할 / 조합) — 쪽 탭 모양 (M53 Page: 고른 탭 양피지 + Ink, 아래 6px 종류 색 밑줄 — 안쪽 24, 아래에서 6 위)
            var tabs = NewRect("Tabs", body).Top(126, 84);
            AddRow(tabs, 14);
            var tabButtons = new Object[3];
            var tabBgs = new Object[3];
            var tabLabels = new Object[3];
            var tabAccents = new Object[3];
            string[] names = { "특성", "역할", "조합" };
            var kinds = new[] { SynergyKind.Trait, SynergyKind.Style, SynergyKind.Combo };
            for (int i = 0; i < names.Length; i++)
            {
                // 흰 바느질 탭 그림 (색은 SynergyCodexPopup이 TabStyle로 입힌다 — 켜짐 · 꺼짐 전용 그림이 둘 다 있으면 TabSkin, ART-UPGRADE §4.3 #9)
                var tab = UiArt.Panel(tabs, $"Tab_{i}", UiArt.Tab9s, 1.6f, UITheme.TabNormal, UITheme.TabNormal, true);
                PageTabSkin(tab, 1.6f);
                tabButtons[i] = AddPlainButton(tab);
                tabBgs[i] = tab;
                var label = Fit(Txt(tab.transform, "Label", names[i], 29, UITheme.SubText, TextAlignmentOptions.Center, true), 18);
                label.rectTransform.Stretch(10, 10, 0, 6);
                tabLabels[i] = label;
                var accent = UiArt.Panel(tab.transform, "Accent", UiArt.PillWhite9s, UiArt.CapsulePpu(6), SynergyUI.KindColor(kinds[i]), SynergyUI.KindColor(kinds[i]));
                accent.rectTransform.Bottom(6, 6, 24, 24);
                accent.enabled = false;
                tabAccents[i] = accent;
            }
            var tabHint = Txt(body, "TabHint", string.Empty, UIType.Caption, UITheme.SubText).FitFloor(UIType.HintMin);
            tabHint.rectTransform.Top(220, 44);

            var list = NewRect("List", body).Stretch(0, 0, 274, 0);
            var content = ScrollList(list, out var scroll);
            AddVerticalList(content, 14, new RectOffset(0, 0, 4, 20));
            var empty = Txt(list, "EmptyLabel", "아직 시너지 정보가 없어요.", 28, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.SetInactive();

            new Binder(popup)
                .Set("_summaryText", summary)
                .Set("_stageIcon", stageIcon)
                .Set("_stageFallback", stageFallback)
                .Set("_calmIcon", calmIcon)
                .Set("_stageText", stage)
                .SetArray("_tabButtons", tabButtons)
                .SetArray("_tabBackgrounds", tabBgs)
                .SetArray("_tabLabels", tabLabels)
                .SetArray("_tabAccents", tabAccents)
                .Set("_tabHintText", tabHint)
                .Set("_scroll", scroll)
                .Set("_content", content)
                .Set("_rowPrefab", BuildSynergyCodexRowPrefab())
                .Set("_emptyLabel", empty.gameObject)
                .Apply();
            return popup;
        }

        /// <summary>시너지 도감 한 줄 (888x180): 큰 아이콘 · 이름 · 종류/상태 칩 · 단계 요약 · 친구 얼굴 · 진행 막대 / 조합 보유·편성 수</summary>
        static SynergyCodexRowView BuildSynergyCodexRowPrefab()
        {
            var hit = Img(null, "SynergyCodexRow", Color.clear, null, true);
            var root = hit.rectTransform;
            root.sizeDelta = new Vector2(888, 180);
            var view = hit.gameObject.AddComponent<SynergyCodexRowView>();
            var button = AddPlainButton(hit);

            // 바탕 = 보라 바느질 판, 그 위 테두리 = 흰 칸 틀(켜지면 강조색)
            var bg = UiArt.Panel(root, "Bg", UiArt.PanelInset9s, 2f, null, UITheme.PanelLight);
            bg.rectTransform.Stretch(1, 1, 1, 1);
            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2.2f, UITheme.Divider);
            frame.rectTransform.Stretch();

            var iconGlow = Glow(root, "IconGlow", UITheme.Lavender, MiddleLeft, new Vector2(-10, 0), new Vector2(184, 184));
            iconGlow.SetInactive();
            var ring = MedallionSocket(root, MiddleLeft, new Vector2(20, 0), 128);
            var icon = SynergyIcon(ring.transform, 18);
            var iconFallback = Txt(icon.transform, "Fallback", "?", 46, UITheme.Cream, TextAlignmentOptions.Center, true);
            iconFallback.rectTransform.Stretch();

            var name = Fit(Txt(root, "Name", "시너지", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis(), 20);
            name.rectTransform.At(TopLeft, new Vector2(166, -12), new Vector2(460, 46));
            var state = UiArt.Panel(root, "StateChip", UiArt.PillWhite9s, UiArt.CapsulePpu(38), UITheme.Secondary);
            state.rectTransform.At(TopRight, new Vector2(-18, -16), new Vector2(150, 38));
            var stateText = Fit(Txt(state.transform, "Text", "꺼짐", 19, UITheme.SubText, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            stateText.rectTransform.Stretch(6, 6, 0, 0);
            var kind = UiArt.Panel(root, "KindChip", UiArt.PillWhite9s, UiArt.CapsulePpu(38), UITheme.Lavender);
            kind.rectTransform.At(TopRight, new Vector2(-178, -16), new Vector2(72, 38));
            var kindText = Txt(kind.transform, "Text", "특성", 19, UITheme.Ink, TextAlignmentOptions.Center, true);
            kindText.rectTransform.Stretch();

            // 단계 설명 (폭 470 — 오른쪽 기믹 칩 앞에 16px 넘게 비운다, M44)
            var summary = Fit(Txt(root, "Summary", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true), UIType.AutoSizeFloor);
            summary.rectTransform.At(TopLeft, new Vector2(166, -60), new Vector2(470, 56));

            // 상태 칩 아래 기믹 칩 (약 210): 기믹 아이콘 34 + '이번 스테이지 ×1.5' 18 굵게 금빛 (아닐 때 '×1.5' SubText)
            var favored = NewRect("Favored", root).At(TopRight, new Vector2(-18, -60), new Vector2(210, 34));
            var favoredIcon = UiArt.Icon(favored, "Icon", null, MiddleLeft, Vector2.zero, 34);
            favoredIcon.enabled = false;
            var favoredFallback = Txt(favoredIcon.transform, "Fallback", "?", UIType.Micro, UITheme.Cream, TextAlignmentOptions.Center, true);
            favoredFallback.rectTransform.Stretch();
            var favoredText = Fit(Txt(favored, "Text", "×1.5", UIType.Micro, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true), UIType.AutoSizeFloor);
            favoredText.rectTransform.Stretch(40, 0, 0, 0);
            favored.gameObject.SetActive(false);

            var members = NewRect("Members", root).At(BottomLeft, new Vector2(166, 14), new Vector2(400, 52));
            var row = members.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 6;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var more = Txt(root, "MoreText", "+0", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true);
            more.rectTransform.At(BottomLeft, new Vector2(572, 14), new Vector2(50, 52));
            more.SetInactive();

            var progress = BuildProgressBar(root, "Progress", 64, 22, UIType.Micro);
            ((RectTransform)progress.transform).At(BottomRight, new Vector2(-18, 12), new Vector2(248, 56));
            var combo = Fit(Txt(root, "ComboText", string.Empty, UIType.Caption, UITheme.Text, TextAlignmentOptions.MidlineRight, true), UIType.AutoSizeFloor);
            combo.rectTransform.At(BottomRight, new Vector2(-20, 14), new Vector2(260, 52));
            combo.SetInactive();

            new Binder(view)
                .Set("_button", button)
                .Set("_bg", bg)
                .Set("_frame", frame)
                .Set("_iconGlow", iconGlow)
                .Set("_iconRing", ring)
                .Set("_icon", icon)
                .Set("_iconFallback", iconFallback)
                .Set("_nameText", name)
                .Set("_kindChip", kind)
                .Set("_kindText", kindText)
                .Set("_stateChip", state)
                .Set("_stateText", stateText)
                .Set("_summaryText", summary)
                .Set("_memberContent", members)
                .Set("_memberPrefab", BuildSynergyMiniMemberPrefab())
                .Set("_moreText", more)
                .Set("_progress", progress)
                .Set("_comboText", combo)
                .Set("_favoredGroup", favored.gameObject)
                .Set("_favoredIcon", favoredIcon)
                .Set("_favoredFallback", favoredFallback)
                .Set("_favoredText", favoredText)
                .Apply();
            return SaveMenuPrefab<SynergyCodexRowView>(hit.gameObject, $"{PrefabDir}/SynergyCodexRow.prefab");
        }

        /// <summary>도감 줄의 작은 친구 얼굴 (52x52): 상태 고리 + 초상화 + 편성 점</summary>
        static SynergyMiniMemberView BuildSynergyMiniMemberPrefab()
        {
            // 바느질 고리(상태 색) + 둥근 받침 + 초상화 + 편성 별
            var ringArt = UiArt.RingStitch;
            var ring = Img(null, "SynergyMiniMember", UITheme.Lavender, ringArt);
            ring.preserveAspect = true;
            var root = ring.rectTransform;
            root.sizeDelta = new Vector2(52, 52);
            var view = ring.gameObject.AddComponent<SynergyMiniMemberView>();
            var plateArt = UiArt.PlateRound;
            var inner = Img(root, "Inner", UiArt.IsArt(plateArt) ? Color.white : UITheme.Parchment, plateArt);
            inner.preserveAspect = true;
            inner.rectTransform.Stretch(6, 6, 6, 6);
            var portrait = Portrait(root, "Portrait", Center, new Vector2(0, 2), new Vector2(48, 48), 20);
            var dot = UiArt.Icon(root, "PlacedDot", UiArt.Star, BottomRight, new Vector2(4, -4), 20);
            dot.SetInactive();

            new Binder(view)
                .Set("_ring", ring)
                .Set("_inner", inner)
                .Set("_portrait", portrait)
                .Set("_placedDot", dot.gameObject)
                .Apply();
            return SaveMenuPrefab<SynergyMiniMemberView>(ring.gameObject, $"{PrefabDir}/SynergyMiniMember.prefab");
        }

        /// <summary>
        /// 시너지 진행 막대: 왼쪽 "3/4", 오른쪽 막대 위에 단계 문턱 눈금 4개 (위치는 런타임에 앵커로).
        /// 크기는 호출하는 쪽에서 정한다.
        /// </summary>
        static SynergyProgressBar BuildProgressBar(Transform parent, string name, float countWidth, float countSize, float tickLabelSize)
        {
            var root = NewRect(name, parent);
            var view = root.gameObject.AddComponent<SynergyProgressBar>();

            var count = Fit(Txt(root, "Count", "0/2", countSize, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true), Mathf.Max(UIType.AutoSizeFloor, countSize * 0.6f));
            count.rectTransform.anchorMin = new Vector2(0f, 0f);
            count.rectTransform.anchorMax = new Vector2(0f, 1f);
            count.rectTransform.pivot = new Vector2(0f, 0.5f);
            count.rectTransform.offsetMin = Vector2.zero;
            count.rectTransform.offsetMax = new Vector2(countWidth, 0);

            // 캡슐 막대 (틀 + 채움). 채움 너비는 SynergyProgressBar가 anchorMax.x로
            var track = UiArt.Bar(root, "Track", UITheme.Lavender, out var fillRect, 16f);
            track.rectTransform.Bottom(8, 16, countWidth + 6, 10);
            var fill = fillRect.GetComponent<Image>();

            var ticks = new Object[4];
            var dots = new Object[4];
            var labels = new Object[4];
            for (int i = 0; i < ticks.Length; i++)
            {
                var tick = NewRect($"Tick_{i}", track.transform);
                tick.anchorMin = tick.anchorMax = new Vector2(0.25f * (i + 1), 0.5f);
                tick.pivot = Center;
                tick.anchoredPosition = Vector2.zero;
                tick.sizeDelta = new Vector2(20, 20);
                var dot = UiArt.Icon(tick, "Dot", UiArt.PipButton, Center, Vector2.zero, 18);
                dot.color = UITheme.Divider;
                var label = Txt(tick, "Label", "2", tickLabelSize, UITheme.SubText, TextAlignmentOptions.Center, true);
                label.rectTransform.At(Center, new Vector2(0, tickLabelSize + 4), new Vector2(40, tickLabelSize + 8));
                ticks[i] = tick;
                dots[i] = dot;
                labels[i] = label;
            }

            new Binder(view)
                .Set("_fill", fill.rectTransform)
                .Set("_fillImage", fill)
                .SetArray("_ticks", ticks)
                .SetArray("_tickDots", dots)
                .SetArray("_tickLabels", labels)
                .Set("_countText", count)
                .Apply();
            return view;
        }

        /// <summary>세로 레이아웃 (자식 높이도 레이아웃이 정한다 — TMP/LayoutElement 선호 높이)</summary>
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

        /// <summary>세로 레이아웃 안의 고정 높이 칸</summary>
        static RectTransform Section(RectTransform parent, string name, float height) => Section(NewRect(name, parent), height);

        static RectTransform Section(RectTransform rt, float height)
        {
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return rt;
        }

        static TextMeshProUGUI SectionTitle(RectTransform parent, string name, string text)
        {
            var title = Txt(parent, name, text, 30, UITheme.Cream, TextAlignmentOptions.BottomLeft, true);
            Section(title.rectTransform, 48);
            Img(title.transform, "Underline", WithAlpha(UITheme.GoldAccent, 0.45f)).rectTransform.Bottom(-6, 2);
            return title;
        }

        /// <summary>레이아웃 계산에서 빼는 배경</summary>
        static Image Ignore(Image image)
        {
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        /// <summary>반복 발광 (MenuGlowPulse). 큰 테두리는 크기를 거의 흔들지 않게 scaleAmount를 작게.</summary>
        static MenuGlowPulse Pulse(Component target, float scaleAmount, float minAlpha, float maxAlpha)
        {
            var pulse = target.gameObject.AddComponent<MenuGlowPulse>();
            var so = new SerializedObject(pulse);
            so.FindProperty("_scaleAmount").floatValue = scaleAmount;
            so.FindProperty("_minAlpha").floatValue = minAlpha;
            so.FindProperty("_maxAlpha").floatValue = maxAlpha;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pulse;
        }

        // ═════════════════════════ 공용 프리팹 ═════════════════════════

        /// <summary>작은 보상 칩 (원정/던전 드롭, 상점 아이콘). 셀 크기에 맞춰 늘어난다.</summary>
        static RewardSlotView BuildRewardChipPrefab()
        {
            var chip = BuildRewardChip(null, "RewardChip", 26);
            ((RectTransform)chip.transform).sizeDelta = new Vector2(72, 72);
            return SaveMenuPrefab<RewardSlotView>(chip.gameObject, $"{PrefabDir}/RewardChip.prefab");
        }

        static RewardSlotView BuildRewardChip(Transform parent, string name, float fallbackSize)
        {
            // 바탕 = 보라 바느질 판(누르는 자리), 그 위 = 흰 칸 틀(등급 색 — RewardSlotView._frame)
            var inner = UiArt.Panel(parent, name, UiArt.PanelInset9s, 2f, null, NightInner, true);
            var root = inner.rectTransform;
            var view = inner.gameObject.AddComponent<RewardSlotView>();
            var button = AddPlainButton(inner);

            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2f, UITheme.GradeColor(ItemGrade.Common));
            frame.rectTransform.Stretch();
            var glow = Img(root, "Glow", WithAlpha(UITheme.Lavender, 0.45f), SoftGlow());
            glow.rectTransform.Stretch(4, 4, 4, 4);
            var placeholder = UiArt.Star;
            var icon = Img(root, "Icon", Color.white, placeholder);
            icon.preserveAspect = true;
            icon.rectTransform.Stretch(8, 8, 6, 10);
            var fallback = Txt(icon.transform, "Fallback", "?", fallbackSize, UITheme.Cream, TextAlignmentOptions.Center, true);
            fallback.rectTransform.Stretch();

            // 에픽 이상 재료: 위쪽 3px 등급 띠 (M83 — 작은 칩은 알약 대신)
            var gradeBar = Img(root, "GradeBar", UITheme.GradeColor(ItemGrade.Epic), RoundSprite);
            gradeBar.rectTransform.Top(5, 3, 12, 12);
            gradeBar.SetInactive();

            // 수량 '×3' (작은 곱하기 + 줄임 표기 — 64 칩에서도 16 아래로 줄지 않게 받침을 칩 폭 가까이)
            var amountBox = UiArt.Panel(root, "AmountBox", UiArt.PillNight9s, UiArt.CapsulePpu(24), null, WithAlpha(UITheme.Background, 0.85f));
            amountBox.rectTransform.Bottom(-6, 24, 2, 2);
            var amount = Fit(Txt(amountBox.transform, "Amount", "×1", 18, UITheme.Cream, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            amount.rectTransform.Stretch(2, 2, 0, 0);

            var dim = Img(root, "Dim", WithAlpha(UITheme.Background, 0.6f), RoundSprite);
            dim.pixelsPerUnitMultiplier = 1.6f;
            dim.rectTransform.Stretch();
            dim.SetInactive();

            new Binder(view)
                .Set("_frame", frame)
                .Set("_glow", glow)
                .Set("_icon", icon)
                .Set("_fallbackText", fallback)
                .Set("_amountText", amount)
                .Set("_amountBox", amountBox.gameObject)
                .Set("_dim", dim.gameObject)
                .Set("_button", button)
                .Set("_placeholder", placeholder)
                .SetArray("_currencyIcons", CurrencySprites())
                .Set("_gradeBar", gradeBar)
                .Apply();
            return view;
        }

        /// <summary>큰 보상 칸 (보상 팝업, 가방): 아이콘 + 이름 + 수량</summary>
        static RewardSlotView BuildRewardSlotPrefab()
        {
            // 바탕 = 보라 바느질 판(누르는 자리), 그 위 = 흰 칸 틀(등급 색 — RewardSlotView._frame)
            var inner = UiArt.Panel(null, "RewardSlot", UiArt.PanelInset9s, 2f, null, NightInner, true);
            var root = inner.rectTransform;
            root.sizeDelta = new Vector2(164, 196);
            var view = inner.gameObject.AddComponent<RewardSlotView>();
            var button = AddPlainButton(inner);

            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2f, UITheme.GradeColor(ItemGrade.Common));
            frame.rectTransform.Stretch();
            var glow = Img(root, "Glow", WithAlpha(UITheme.Lavender, 0.45f), SoftGlow());
            glow.rectTransform.At(Center, new Vector2(0, 20), new Vector2(136, 136));
            var placeholder = UiArt.Star;
            var icon = Img(root, "Icon", Color.white, placeholder);
            icon.preserveAspect = true;
            icon.rectTransform.At(Center, new Vector2(0, 20), new Vector2(104, 104));
            var fallback = Txt(icon.transform, "Fallback", "?", 40, UITheme.Cream, TextAlignmentOptions.Center, true);
            fallback.rectTransform.Stretch();

            var name = Fit(Txt(root, "Name", string.Empty, 20, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis(), UIType.AutoSizeFloor);
            name.rectTransform.Bottom(36, 30, 6, 6);
            var amountBox = UiArt.Panel(root, "AmountBox", UiArt.PillNight9s, UiArt.CapsulePpu(30), null, WithAlpha(UITheme.Background, 0.85f));
            amountBox.rectTransform.Bottom(8, 30, 12, 12);
            var amount = Fit(Txt(amountBox.transform, "Amount", "×1", 20, UITheme.Gold, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            amount.rectTransform.Stretch(4, 4, 0, 0);

            var dim = Img(root, "Dim", WithAlpha(UITheme.Background, 0.6f), RoundSprite);
            dim.rectTransform.Stretch();
            dim.SetInactive();

            // 등급 알약 (M83): 왼쪽 위 52x24, 등급 색 + 등급 이름 18 굵게 Ink — 보통 등급 · 재화는 숨긴다 (흐림 덮개 위)
            var gradePill = UiArt.Panel(root, "GradePill", UiArt.PillWhite9s, UiArt.CapsulePpu(24), UITheme.GradeColor(ItemGrade.Rare), UITheme.GradeColor(ItemGrade.Rare));
            gradePill.rectTransform.At(TopLeft, new Vector2(8, -8), new Vector2(52, 24));
            var gradeName = Txt(gradePill.transform, "GradeName", "레어", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            gradeName.rectTransform.Stretch(3, 3, 0, 0);
            gradePill.SetInactive();

            new Binder(view)
                .Set("_frame", frame)
                .Set("_glow", glow)
                .Set("_icon", icon)
                .Set("_fallbackText", fallback)
                .Set("_amountText", amount)
                .Set("_amountBox", amountBox.gameObject)
                .Set("_nameText", name)
                .Set("_dim", dim.gameObject)
                .Set("_button", button)
                .Set("_placeholder", placeholder)
                .SetArray("_currencyIcons", CurrencySprites())
                .Set("_gradePill", gradePill.gameObject)
                .Set("_gradePillImage", gradePill)
                .Set("_gradeName", gradeName)
                .Apply();
            return SaveMenuPrefab<RewardSlotView>(inner.gameObject, $"{PrefabDir}/RewardSlot.prefab");
        }

        // ═════════════════════════ 조각 / 도우미 ═════════════════════════

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        static Sprite SoftCircle()
        {
            var art = ArtLib.FX("circle_soft");
            return art != null ? art : CircleSprite;
        }

        static Sprite SoftGlow()
        {
            var art = ArtLib.FX("glow_soft");
            return art != null ? art : CircleSprite;
        }

        /// <summary>부드러운 빛 (FX/glow_soft). 아트가 없으면 옅은 원으로 대신한다.</summary>
        static Image Glow(Transform parent, string name, Color color, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var art = ArtLib.FX("glow_soft");
            if (art == null) color.a *= 0.4f;
            var glow = Img(parent, name, color, art != null ? art : CircleSprite);
            glow.rectTransform.At(anchor, position, size);
            return glow;
        }

        /// <summary>크림색 카드 (9-slice 테두리를 작은 카드에 맞게 줄인다)</summary>
        static Image Card(Transform parent, string name, bool raycast = false)
        {
            var card = ParchmentCard(parent, name, raycast);
            if (card.type == Image.Type.Sliced) card.pixelsPerUnitMultiplier = 1.6f;
            return card;
        }

        static Image NightPanel(Transform parent, string name, bool raycast = false)
        {
            var panel = Window(parent, name, raycast);
            if (panel.type == Image.Type.Sliced) panel.pixelsPerUnitMultiplier = 1.6f;
            return panel;
        }

        /// <summary>
        /// 버튼 공장 버튼 (M25 · M82): SceneParts.Button — 단색 자리표시는 PrimaryButton / Gold / Secondary / ParchmentDark,
        /// 이름은 모양에 맞는 색 굵게(금 · 양피지 = Ink), 누름 크기 · 비활성 흐림 포함. 크기 · 자리는 부르는 쪽이 잡는다.
        /// </summary>
        static Button StyledButton(Transform parent, string name, string label, Tone tone, float fontSize, out TextMeshProUGUI labelText) =>
            SceneParts.Button(parent, name, label, Style(tone), fontSize, out labelText);

        /// <summary>이 빌더의 버튼 톤 → 공용 버튼 모양 (Primary · Gold · Secondary · Soft)</summary>
        static ButtonStyle Style(Tone tone) => tone switch
        {
            Tone.Primary => ButtonStyle.Primary,
            Tone.Gold => ButtonStyle.Gold,
            Tone.Soft => ButtonStyle.Soft,
            _ => ButtonStyle.Secondary,
        };

        /// <summary>
        /// 버튼 그림 (SceneParts.SkinButton 한 줄): 보라(Primary) / 금(Gold) / 차분한 보조(Secondary) / 양피지(Soft).
        /// 흰색 그대로, 비활성은 그림 교체(ui_button_disabled_9s). 테두리 배율은 FitButtonBorders가 높이에 맞춘다.
        /// </summary>
        static void Skin(Button button, Tone tone) => SkinButton(button, Style(tone));

        /// <summary>
        /// 아트 버튼(SpriteSwap)의 9-slice 배율을 버튼 높이에 맞춘다 (StyledButton은 크기를 정하기 전에 그림을 입히므로 다 만든 뒤 한 번).
        /// 높이를 알 수 없는 버튼(부모를 꽉 채우는 등)은 그대로 둔다.
        /// </summary>
        static void FitButtonBorders(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.transition != Selectable.Transition.SpriteSwap) continue;
                if (!(button.targetGraphic is Image image) || image.type != Image.Type.Sliced) continue;
                float height = KnownHeight((RectTransform)button.transform);
                if (height > 0f) image.pixelsPerUnitMultiplier = UiArt.ButtonPpu(height);
            }
        }

        /// <summary>빌드 시점에 알 수 있는 높이 (가로 레이아웃이 높이를 정하면 그 줄 높이, 세로 앵커가 한 점이면 sizeDelta.y)</summary>
        static float KnownHeight(RectTransform rt)
        {
            if (rt.parent is RectTransform parent && parent.TryGetComponent<HorizontalLayoutGroup>(out var row) && row.childControlHeight)
            {
                if (!Mathf.Approximately(parent.anchorMin.y, parent.anchorMax.y)) return -1f;
                var element = rt.GetComponent<LayoutElement>();
                float rowHeight = parent.sizeDelta.y - row.padding.vertical;
                return element != null && element.preferredHeight > 0f && !row.childForceExpandHeight
                    ? Mathf.Min(rowHeight, element.preferredHeight)
                    : rowHeight;
            }
            return Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y) ? rt.sizeDelta.y : -1f;
        }

        /// <summary>프리팹 저장 전에 버튼 그림 배율을 맞춘다</summary>
        static T SaveMenuPrefab<T>(GameObject go, string path) where T : Component
        {
            FitButtonBorders(go.transform);
            return SavePrefab<T>(go, path);
        }

        static Button AddPlainButton(Image target)
        {
            var button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            target.gameObject.AddComponent<PressScale>();
            return button;
        }

        /// <summary>
        /// 알약 모양 글자 칩 (anchor/position/size로 배치). 글자는 첫 번째 자식.
        /// 어두운 바탕색이면 밤 알약 그림(ui_pill_night_9s, 흰색 + 바탕색의 알파),
        /// 밝은 바탕색이면 흰 알약 그림(ui_pill_white_9s)에 그 색을 입힌다 (런타임에 색을 바꿔도 된다). 캡슐 배율 = 96 / 높이.
        /// </summary>
        static Image Pill(Transform parent, string name, string text, float fontSize, Color background, Color textColor,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            bool light = background.grayscale > 0.45f;
            var art = light ? UiArt.PillWhite9s : UiArt.PillNight9s;
            var tint = light ? background : WithAlpha(Color.white, Mathf.Clamp01(background.a + 0.1f));
            var pill = UiArt.Panel(parent, name, art, UiArt.CapsulePpu(size.y), tint, background);
            pill.rectTransform.At(anchor, position, size);
            var label = Fit(Txt(pill.transform, "Text", text, fontSize, textColor, TextAlignmentOptions.Center, true), Mathf.Max(UIType.AutoSizeFloor, fontSize * 0.7f));
            label.rectTransform.Stretch(10, 10, 0, 0);
            return pill;
        }

        /// <summary>빈 칸 표시: 바늘 꽂힌 천 십자 그림(ui_slot_plus). 아트가 없으면 색 "+" 글자</summary>
        static GameObject EmptyMark(RectTransform root, Vector2 position, float size, Color fallbackColor, float fallbackFontSize)
        {
            var art = UiArt.SlotPlus;
            if (art != null) return UiArt.Icon(root, "Plus", art, Center, position, size).gameObject;
            var plus = Txt(root, "Plus", "+", fallbackFontSize, fallbackColor, TextAlignmentOptions.Center, true);
            plus.rectTransform.At(Center, position, new Vector2(80, 80));
            return plus.gameObject;
        }

        /// <summary>
        /// 어두운 메달 홈 (ui_medallion_dark, 흰색). 역할 아이콘 · 글자 자리표시를 이 안에 넣고,
        /// 테두리까지 그려진 메달 아이콘(특성/조합)이면 런타임에 숨긴다 (SynergyIcons.ApplyInRing).
        /// </summary>
        static Image MedallionSocket(Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var art = UiArt.MedallionDark;
            var socket = Img(parent, "IconRing", UiArt.IsArt(art) ? Color.white : UITheme.Lavender, art);
            socket.preserveAspect = true;
            socket.rectTransform.At(anchor, position, new Vector2(size, size));
            if (!UiArt.IsArt(art)) Img(socket.transform, "Inner", NightInner, CircleSprite).rectTransform.Stretch(size * 0.05f, size * 0.05f, size * 0.05f, size * 0.05f);
            return socket;
        }

        /// <summary>홈 안의 시너지 아이콘 (런타임이 스프라이트를 정할 때까지 꺼 둔다 — 흰 네모 방지)</summary>
        static Image SynergyIcon(Transform socket, float inset)
        {
            var icon = Img(socket, "Icon", Color.white, null);
            icon.preserveAspect = true;
            icon.enabled = false;
            icon.rectTransform.Stretch(inset, inset, inset, inset);
            return icon;
        }

        /// <summary>
        /// 네모 초상화 배경 (UI/portrait_bg — 수선점 안 그림)을 둥근 사각 마스크 안에 비율대로 덮어 깐다.
        /// 반환값은 그림 (색은 계보 색을 옅게 — FormationCardView), mask는 위치를 잡을 마스크.
        /// </summary>
        static Image PortraitBackdrop(Transform parent, string name, out RectTransform mask)
        {
            var maskImage = Img(parent, name + "Mask", Color.white, RoundSprite);
            maskImage.pixelsPerUnitMultiplier = 1.5f;
            maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            mask = maskImage.rectTransform;
            var art = UiArt.PortraitBg;
            var image = Img(mask, name, UiArt.IsArt(art) ? Color.Lerp(Color.white, UITheme.Lavender, 0.3f) : WithAlpha(UITheme.Lavender, 0.32f), art);
            image.type = Image.Type.Simple;
            image.rectTransform.Stretch();
            if (UiArt.IsArt(art))
            {
                var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.rect.width / Mathf.Max(1f, art.rect.height);
            }
            return image;
        }

        /// <summary>자동 크기: 최대 = 지금 글자 크기, 최소 = max(minSize, 16) (UIKit.FitFloor — 16보다 작게 부르면 경고하고 올린다, M23)</summary>
        static TextMeshProUGUI Fit(TextMeshProUGUI label, float minSize) => label.FitFloor(minSize);

        /// <summary>캐릭터 초상화 (아트가 없으면 둥근 받침 그림에 계보 색을 옅게 + 첫 글자)</summary>
        static FormPortraitView Portrait(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float initialSize)
        {
            var plate = UiArt.PlateRound;
            var image = Img(parent, name, Color.white, plate);
            image.preserveAspect = true;
            image.rectTransform.At(anchor, position, size);
            var view = image.gameObject.AddComponent<FormPortraitView>();
            var initial = Txt(image.transform, "Initial", "?", initialSize, UITheme.Ink, TextAlignmentOptions.Center, true);
            initial.rectTransform.Stretch();
            new Binder(view).Set("_image", image).Set("_initialText", initial).Set("_placeholder", plate).Apply();
            return view;
        }

        /// <summary>수선점 그림 (Battle/shop). 없으면 지붕 + 몸통 + 창문 불빛으로 그린 집.</summary>
        static RectTransform ShopArt(Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var art = ArtLib.Battle("shop");
            if (art != null)
            {
                var image = Img(parent, "ShopArt", Color.white, art);
                image.preserveAspect = true;
                return image.rectTransform.At(anchor, position, new Vector2(size, size));
            }

            var root = NewRect("ShopArt", parent).At(anchor, position, new Vector2(size, size));
            var roof = Img(root, "Roof", UITheme.DustyPink);
            roof.rectTransform.At(Center, new Vector2(0, size * 0.12f), new Vector2(size * 0.5f, size * 0.5f));
            roof.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var body = Img(root, "Body", UITheme.PanelLight, RoundSprite);
            body.rectTransform.At(BottomCenter, new Vector2(0, size * 0.06f), new Vector2(size * 0.7f, size * 0.5f));
            Img(body.transform, "Window", UITheme.Lamp, RoundSprite).rectTransform.At(Center, new Vector2(0, size * 0.03f), new Vector2(size * 0.24f, size * 0.2f));
            return root;
        }

        /// <summary>자물쇠 (Icons/lock). 없으면 고리 + 몸통으로 그린다.</summary>
        static void LockIcon(Transform parent, Vector2 anchor, Vector2 position, float size, Color color)
        {
            var art = ArtLib.Icon("lock");
            if (art != null)
            {
                var image = Img(parent, "LockIcon", Color.white, art);
                image.preserveAspect = true;
                image.rectTransform.At(anchor, position, new Vector2(size, size));
                return;
            }

            var root = NewRect("LockIcon", parent).At(anchor, position, new Vector2(size, size));
            var shackle = Img(root, "Shackle", color, CircleSprite);
            shackle.rectTransform.At(TopCenter, Vector2.zero, new Vector2(size * 0.56f, size * 0.56f));
            Img(shackle.transform, "Hole", WithAlpha(UITheme.Background, 0.9f), CircleSprite).rectTransform.Stretch(size * 0.1f, size * 0.1f, size * 0.1f, size * 0.1f);
            var body = Img(root, "Body", color, RoundSprite);
            body.pixelsPerUnitMultiplier = 3f;
            body.rectTransform.At(BottomCenter, Vector2.zero, new Vector2(size * 0.8f, size * 0.58f));
            Img(body.transform, "Keyhole", WithAlpha(UITheme.Background, 0.9f), CircleSprite).rectTransform.At(Center, Vector2.zero, new Vector2(size * 0.16f, size * 0.16f));
        }

        /// <summary>가로 스크롤 목록. 반환값은 항목을 붙일 Content (항목 크기는 프리팹 크기 그대로).</summary>
        static RectTransform HorizontalScroll(RectTransform root, float spacing, RectOffset padding, out ScrollRect scroll)
        {
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var viewport = Img(root, "Viewport", Color.clear, null, true).rectTransform.Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.padding = padding;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>한 줄 칩 격자 (셀 크기로 칩 크기를 정한다)</summary>
        static RectTransform ChipRow(Transform parent, string name, float cell, float spacing)
        {
            var rt = NewRect(name, parent);
            var grid = rt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            grid.childAlignment = TextAnchor.MiddleLeft;
            return rt;
        }

        /// <summary>가운데 정렬 가로 줄 (아이콘 + 글자)</summary>
        static RectTransform CenteredRow(Transform parent, string name, float spacing)
        {
            var rt = NewRect(name, parent);
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = spacing;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return rt;
        }

        static Object[] CurrencySprites() => new Object[]
        {
            ArtLib.Currency(CurrencyType.Gold),
            ArtLib.Currency(CurrencyType.Gem),
            ArtLib.Currency(CurrencyType.EventToken),
        };
    }
}
