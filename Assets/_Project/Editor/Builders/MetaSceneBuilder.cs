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
    /// 메타 화면 빌더 (담당: M3 메타 UI). IdleSceneBuilder가 아래 순서로 부른다.
    ///   BuildHud — 상단 바·전투 화면·탭 바가 만들어진 뒤:
    ///     · ctx.BattleRoot 아래 "MetaHud" (중첩 Canvas, CoachTargets 경로 그대로):
    ///         GuideQuestBar (왼쪽 아래, 잔향이 덱 띠 바로 위 600×84), IdleChestWidget (오른쪽, 배속·AUTO 아래 150×170, SheetFade 0.5 — 튜토리얼 중 1),
    ///         권장 전투력 알약 (스테이지 패널 옆, SheetFade 0), AUTO / 2배속 잠금 덮개,
    ///         빠른 아이콘 줄 QuickRail_L (EventButton 이벤트 · AttendanceButton 출석부) · QuickRail_R (JournalButton 수선 일지) — UX-SPEC-IA §3.3, SheetFade 0.
    ///     · ctx.TopBar(화면 틀 담당이 만든 짧은 상단 바)의 PowerChip/PowerText에 전투력 숫자 (TopBarMetaView — 숫자만, CR-14).
    ///       수선 일지 · 출석부 버튼은 더 이상 상단 바에 만들지 않는다 (빠른 아이콘으로 옮김), "초당 단추" 글자도 숨기지 않는다.
    ///     · ctx.TabButtons에 잠금 표시(알파 0.45 + 자물쇠 48px + "1-4")와 빨간 점, TabGateView(SelectGuard).
    ///       가운데 사냥 버튼(ctx.TabMain)에는 모아 보기 점 RedDotAnyView (시트가 열린 동안만, 받을 것만 — §2.3 · CR-07).
    ///   BuildPopups — 다른 팝업 다음: 수선 일지, 출석부, 수선 바구니, 복귀 화면, 기능 개방 카드 + 발표자(MetaPopupPresenter) +
    ///     이동 처리기(MetaNavigator) + 잠자기 모드 덮개 (지도가 보이는 새 모양, §4.9). 원정 탭의 예전 숫자 배지는 메타가 켜지면 빨간 점 트리가 대신한다.
    /// 아트는 ArtLib(UI/idle_basket_0..3, UI/return_header, Meta/* 등) + 공용 힉스필드 조각 UiArt(9-slice 판·버튼·막대·배지·받침),
    /// 없을 때만 예전 색 도형 자리표시. 그림 묶음은 MetaIcons 에셋. 막대는 Filled 대신 앵커로 폭을 바꾼다 (Bar / MetaUi.SetBar).
    /// </summary>
    internal static partial class MetaSceneBuilder
    {
        const string AssetDir = IdleProjectSetup.Root + "/Prefabs/UI/Meta";
        const string IconsPath = AssetDir + "/MetaIcons.asset";
        /// <summary>빌더가 코드로 만드는 흰색 + 알파 그림 (잠자기 덮개 위아래 흐림 등)</summary>
        const string GeneratedSpriteDir = IdleProjectSetup.Root + "/Sprites/UI/Meta";
        const float StripHeight = 156f;

        static MetaIcons _icons;

        static readonly Color NightPanelColor = new(0.149f, 0.114f, 0.298f, 0.85f);
        static readonly Color DeepInk = new(0.09f, 0.07f, 0.18f, 0.9f);
        static readonly Color TanBasket = new(0.78f, 0.6f, 0.42f, 1f);

        /// <summary>전투 화면 메타 HUD + 상단 바 메타 버튼 + 탭 잠금/점</summary>
        public static void BuildHud(SceneBuildContext ctx)
        {
            _icons = PrepareIcons();
            if (ctx.BattleRoot != null) BuildBattleHud(ctx);
            if (ctx.TopBar != null) BuildTopBar(ctx);
            if (ctx.TabBar != null && ctx.TabButtons != null) BuildTabGate(ctx);
        }

        /// <summary>메타 팝업 + 발표자 + 이동 처리기 + 잠자기 모드</summary>
        public static void BuildPopups(SceneBuildContext ctx)
        {
            if (_icons == null) _icons = PrepareIcons();
            var journal = BuildJournalPopup(ctx);
            var attendance = BuildAttendancePopup(ctx);
            var idleChest = BuildIdleChestPopup(ctx);
            var returnPopup = BuildReturnPopup(ctx);
            var unlockCard = BuildFeatureUnlockCard(ctx);
            var strengthen = BuildStrengthenPopup(ctx);
            ctx.Popups.Add(journal);
            ctx.Popups.Add(attendance);
            ctx.Popups.Add(idleChest);
            ctx.Popups.Add(returnPopup);
            ctx.Popups.Add(unlockCard);
            ctx.Popups.Add(strengthen);
            // 4차 (SPEC4 — MetaSceneBuilder.Spec4.cs): 챕터 지도 · 챕터 상자 · 동화책 · 다시 만난 날 · 이벤트 시작 · 알림 묻기 등
            BuildSpec4Popups(ctx);

            var meta = NewRect("MetaUI", ctx.Canvas).Stretch();
            // 새 기능 카드가 닫힌 뒤 날아간 아이콘이 자리에 닿을 때의 별빛 (카드 창은 이미 꺼져 있어 카드 밖, 팝업 층 위에 둔다 — M78)
            var slotSparkles = BuildSparkles(meta, "SlotSparkles", 12, 40);
            new Binder(unlockCard).Set("_slotSparkles", slotSparkles).Apply();
            var presenter = meta.gameObject.AddComponent<MetaPopupPresenter>();
            new Binder(presenter)
                .Set("_ui", ctx.UI)
                .Set("_returnPopup", returnPopup)
                .Set("_attendancePopup", attendance)
                .Set("_unlockCard", unlockCard)
                .Apply();
            var navigator = meta.gameObject.AddComponent<MetaNavigator>();
            new Binder(navigator)
                .Set("_journal", journal)
                .Set("_attendance", attendance)
                .Set("_idleChest", idleChest)
                .Apply();

            RetireLegacyTabBadges(ctx);
            BuildSleepMode(ctx);
        }

        // ═════════════════════════ 그림 묶음 ═════════════════════════

        /// <summary>메타 UI 그림 묶음 에셋 (없는 아트는 null → 뷰가 자리표시)</summary>
        static MetaIcons PrepareIcons()
        {
            var icons = AssetDatabase.LoadAssetAtPath<MetaIcons>(IconsPath);
            if (icons == null)
            {
                Directory.CreateDirectory(AssetDir);
                icons = ScriptableObject.CreateInstance<MetaIcons>();
                AssetDatabase.CreateAsset(icons, IconsPath);
            }

            var basket = new Sprite[4];
            for (int i = 0; i < basket.Length; i++) basket[i] = ArtLib.UI($"idle_basket_{i}");

            var featureValues = (FeatureId[])Enum.GetValues(typeof(FeatureId));
            int maxFeature = 0;
            foreach (var feature in featureValues) maxFeature = Math.Max(maxFeature, (int)feature);
            var features = new Sprite[maxFeature + 1];
            foreach (var feature in featureValues) features[(int)feature] = FeatureArt(feature);

            icons.Fill(
                ArtLib.Currency(CurrencyType.Gold), ArtLib.Currency(CurrencyType.Gem), ArtLib.Currency(CurrencyType.EventToken),
                First(ArtLib.Icon("ticket_quick"), ArtLib.Load("Meta/ticket_quick.png")),
                First(ArtLib.Icon("ticket_double"), ArtLib.Load("Meta/ticket_double.png")),
                ArtLib.Icon("shards"),
                ArtLib.Icon("lock"),
                First(ArtLib.Icon("star"), ArtLib.FX("sparkle")),
                First(ArtLib.FX("glow_soft"), CircleSprite),
                First(ArtFx("fx_sparkle"), First(ArtLib.FX("sparkle"), CircleSprite)),
                First(ArtFx("fx_ring_thread"), First(ArtLib.FX("ring_glow"), CircleSprite)),
                CircleSprite,
                RoundSprite,
                basket,
                First(ArtLib.UI("journal_chest_closed"), ArtLib.Load("Meta/chest_closed.png")),
                First(ArtLib.UI("journal_chest_open"), ArtLib.Load("Meta/chest_open.png")),
                First(ArtLib.UI("attendance_stamp"), ArtLib.Load("Meta/stamp.png")),
                First(ArtLib.Icon("journal"), ArtLib.Load("Meta/icon_journal.png")),
                First(ArtLib.Icon("attendance"), ArtLib.Load("Meta/icon_attendance.png")),
                First(ArtLib.Icon("quest"), ArtLib.Load("Meta/icon_quest.png")),
                features);
            // 공용 아트 조각은 Fill 서명을 그대로 두고 따로 묶는다 (없으면 null → 뷰가 다른 그림으로 대신)
            new Binder(icons)
                .Set("_rewardGeneric", ArtIcon("reward_generic"))
                .Set("_badge", ArtUI("ui_badge_red"))
                .Set("_plate", ArtUI("ui_plate_round"))
                .Set("_medallion", ArtUI("ui_medallion_dark"))
                .Set("_check", ArtIcon("check"))
                .Set("_stampRing", ArtUI("ui_stamp_ring"))
                .Set("_moon", First(ArtUI("moon"), ArtLib.Vfx("vfx_moon_glow")))
                .Set("_calm", ArtIcon("stage_calm"))
                .Apply();
            EditorUtility.SetDirty(icons);
            return icons;
        }

        /// <summary>
        /// 기능 아이콘: Meta/feature_{이름}.png → 없으면 가까운 기존 아이콘. 이름은 콘텐츠와 같은 snake_case 표
        /// (MetaContent.FeatureMedalPath: CharacterLevelUp → feature_level_up)라 미션 줄 · 가이드 퀘스트 바의 대신 그림이 개방 카드 메달과 같다
        /// </summary>
        static Sprite FeatureArt(FeatureId feature)
        {
            var art = ArtLib.Load(MetaContent.FeatureMedalPath(feature));
            if (art != null) return art;
            return feature switch
            {
                FeatureId.ShopUpgrade or FeatureId.CharacterLevelUp => ArtLib.UI("tab_growth"),
                FeatureId.Formation or FeatureId.Synergy or FeatureId.EchoSlot6 => ArtLib.UI("tab_formation"),
                FeatureId.Characters or FeatureId.Evolution or FeatureId.Costumes or FeatureId.Codex => ArtLib.UI("tab_characters"),
                FeatureId.Expedition or FeatureId.ExpeditionBatch or FeatureId.Dungeon => ArtLib.UI("tab_expedition"),
                FeatureId.Gacha => ArtLib.UI("tab_gacha"),
                FeatureId.Shop => ArtLib.UI("tab_shop"),
                FeatureId.AutoBattle or FeatureId.AutoChallenge => ArtLib.Icon("auto"),
                FeatureId.Speed2x => ArtLib.Icon("speed"),
                FeatureId.LampPolicy => ArtLib.Icon("lamp"),
                FeatureId.IdleChest => First(ArtLib.UI("idle_basket_2"), ArtLib.Icon("reward_generic")),
                FeatureId.QuickIdle => First(ArtLib.Icon("ticket_quick"), ArtLib.UI("idle_basket_2")),
                FeatureId.Missions or FeatureId.Achievements => First(ArtLib.Icon("journal"), ArtLib.Icon("quest")),
                FeatureId.Attendance => First(ArtLib.Icon("attendance"), ArtLib.Icon("journal")),
                FeatureId.Events => ArtLib.Icon("currency_event"),
                _ => ArtLib.Icon("guardian"),
            };
        }

        static Sprite First(Sprite a, Sprite b) => a != null ? a : b;

        /// <summary>회전하는 빛살 (FX/rays → 예전 Battle/rays → 부드러운 빛)</summary>
        static Sprite RaysArt() => First(ArtFx("rays"), First(ArtLib.Battle("rays"), _icons.Glow));

        // ═════════════════════════ 아트 조각 ═════════════════════════
        // 공용 UiArt(힉스필드 조각)를 부른다. 아트가 없으면 UiArt가 경고를 한 번 남기고, 여기서는 예전 도형으로 대신한다.

        /// <summary>Art/UI/{name}.png (없으면 경고 + null)</summary>
        static Sprite ArtUI(string name) => UiArt.UI(name, null);

        /// <summary>Art/Icons/{name}.png (없으면 경고 + null)</summary>
        static Sprite ArtIcon(string name) => UiArt.Get($"Icons/{name}.png", null);

        /// <summary>Art/FX/{name}.png 흰색 연출 그림 (없으면 경고 + null)</summary>
        static Sprite ArtFx(string name) => UiArt.Get($"FX/{name}.png", null);

        /// <summary>
        /// 9-slice 아트 한 장 (흰색 그대로, ppu로 테두리 굵기를 맞춘다: 그려지는 테두리 = 48 / ppu).
        /// 아트가 없으면 예전 둥근 사각형 + fallback 색.
        /// </summary>
        static Image Sliced(Transform parent, string name, string art, float ppu, Color fallback, bool raycast = false)
        {
            var sprite = ArtUI(art);
            if (sprite == null) return Img(parent, name, fallback, RoundSprite, raycast);
            var image = Img(parent, name, Color.white, sprite, raycast);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ppu;
            return image;
        }

        /// <summary>이미 만든 이미지에 9-slice 아트를 입힌다 (버튼 바탕 등). 아트가 없으면 그대로 둔다</summary>
        static void Reskin(Image image, string art, float ppu)
        {
            var sprite = ArtUI(art);
            if (sprite == null || image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ppu;
            image.color = Color.white;
        }

        /// <summary>
        /// 큰 칸 위에 얹는 금테 (card_frame_9s: 가운데가 비어 테두리만 그린다). 부모를 꽉 채우고 outset만큼 바깥으로 나온다.
        /// 내용보다 먼저 만들면 테두리 아래에 내용이, 나중에 만들면 내용 위에 테두리가 그려진다. 아트가 없으면 null.
        /// </summary>
        static Image FrameOverlay(Transform parent, float ppu, float outset = 0f)
        {
            var sprite = ArtUI("card_frame_9s");
            if (sprite == null) return null;
            var frame = Img(parent, "FrameArt", Color.white, sprite);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = ppu;
            frame.rectTransform.Stretch(-outset, -outset, -outset, -outset);
            return frame;
        }

        /// <summary>
        /// 큰 칸 바탕: 보랏빛 안쪽 판(ui_panel_inset_9s) + 금테(card_frame_9s) 덧씌움. 아트가 없으면 예전 반투명 둥근 사각형.
        /// 반환값은 바탕 이미지 (자식으로 내용을 붙인다).
        /// </summary>
        static Image FramedPanel(Transform parent, string name, Color fallback, float framePpu = 2.5f, bool raycast = false)
        {
            var panel = Sliced(parent, name, "ui_panel_inset_9s", 2f, fallback, raycast);
            FrameOverlay(panel.transform, framePpu);
            return panel;
        }

        /// <summary>
        /// 가로 막대 (UiArt.Bar 규칙): 틀 ui_bar_frame_9s + 채움 ui_bar_fill_9s, 둘 다 Sliced.
        /// 채움은 FillArea 안에서 오른쪽 앵커(anchorMax.x = 비율)로 폭이 바뀌고 0이면 꺼진다 (MetaUi.SetBar).
        /// UiArt.Bar와 달리 안쪽 여백을 FillArea가 가져서 비율이 아주 작아도 채움 폭이 음수가 되지 않는다.
        /// 둥근 그림에 Image.Type.Filled를 쓰면 끝이 렌즈 모양으로 깎이므로 쓰지 않는다. height는 막대 높이(배치와 같은 값).
        /// </summary>
        static Image Bar(Transform parent, string name, float height, Color fillColor, out Image fill)
        {
            var frameArt = ArtUI("ui_bar_frame_9s");
            var track = frameArt != null
                ? Img(parent, name, Color.white, frameArt)
                : Img(parent, name, MetaUi.Alpha(UITheme.Background, 0.9f), RoundSprite);
            track.type = Image.Type.Sliced;
            if (frameArt != null) track.pixelsPerUnitMultiplier = UiArt.CapsulePpu(height);

            float inset = Mathf.Max(1f, Mathf.Round(height * 0.18f));
            var area = NewRect("FillArea", track.transform).Stretch(inset, inset, inset, inset);
            var fillArt = ArtUI("ui_bar_fill_9s");
            fill = Img(area, "Fill", fillColor, fillArt != null ? fillArt : RoundSprite);
            fill.type = Image.Type.Sliced;
            if (fillArt != null) fill.pixelsPerUnitMultiplier = UiArt.CapsulePpu(height - inset * 2f);
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fill.gameObject.SetActive(false);
            return track;
        }

        /// <summary>숫자 없는 빨간 점 이미지 (ui_badge_red, 아트가 없으면 예전 원). 위치·크기는 부르는 쪽에서</summary>
        static Image RedDotImage(Transform parent, string name) => UiArt.Badge(parent, name, Center, Vector2.zero);

        /// <summary>아이콘·초상화 뒤 밝은 둥근 받침 (ui_plate_round, 흰색). 아트가 없으면 예전 원 + fallback 색</summary>
        static Image Plate(Transform parent, string name, Color fallback)
        {
            var art = UiArt.PlateRound;
            bool isArt = UiArt.IsArt(art);
            var plate = Img(parent, name, isArt ? Color.white : fallback, art);
            plate.preserveAspect = isArt;
            return plate;
        }

        // ═════════════════════════ 전투 HUD ═════════════════════════

        static void BuildBattleHud(SceneBuildContext ctx)
        {
            var hud = NewRect("MetaHud", ctx.BattleRoot).Stretch();
            // 타이머·흐림이 바뀌어도 전투 HUD 캔버스를 다시 그리지 않게 따로 캔버스
            hud.gameObject.AddComponent<Canvas>();
            hud.gameObject.AddComponent<GraphicRaycaster>();

            BuildGuideQuestBar(ctx, hud);
            BuildIdleChestWidget(hud);
            BuildRecommendedPower(hud);
            BuildLockCover(ctx, hud, "AutoButton", FeatureId.AutoBattle, HudAutoButtonPos, HudAutoButtonSize);
            BuildLockCover(ctx, hud, "SpeedButton", FeatureId.Speed2x, HudSpeedButtonPos, HudSpeedButtonSize);
            // 빠른 아이콘은 메타 HUD 맨 위 (더보기 쟁반이 다른 HUD 위로 열린다)
            BuildQuickRails(hud);
        }

        /// <summary>가이드 퀘스트 줄 크기 (M04, UX-SPEC §2.1.9 — 화면 x 16~484)</summary>
        static readonly Vector2 GuideBarSize = new(468f, 84f);

        /// <summary>
        /// 가이드 퀘스트 줄 468×84 At(BottomLeft, (16, 덱 띠 + 18)) — MAIN 화면 16~484 × 1492~1576 (M04, UX-SPEC-IA §3.4: 로컬 자리 그대로).
        /// 아래 끝이 덱 안내 글자 탭(띠 위로 18 솟음) 바로 위라 탭을 가리지 않고, 오른쪽 끝(484)이 남쪽 받침 · 지킴이 열(≈500~620) 앞에서 멈춘다.
        ///   Content / GoldEdge(완료 빛무리) · Chrome(두 번째 CanvasGroup — Panel 밤 판 · IconBack 64 받침 + Icon 52(보상 그림) · ProgressBar 10) ·
        ///   ShineMask(완료 빛 줄기) · Text 27 굵게(두 줄까지, 맞춤 20, …) · ProgressText 24 굵게 Cream · Amount '×20' 20 굵게 Cream · ClaimTag 96×46 '받기' 26.
        /// 판 위 글자(Text · ProgressText · Amount)는 전장 그림 위에 바로 놓이기도 해 공유 외곽선 재질을 입힌다 (M81). 흐림 규칙은 뷰가 정한다
        /// (가까운 적 · 띠에는 Chrome만 흐려지고 글자는 남는다). 시너지 띠·위험 화살표는 전투 빌더를 고치지 않고 여기서 찾아 묶는다.
        /// </summary>
        static void BuildGuideQuestBar(SceneBuildContext ctx, RectTransform hud)
        {
            const float deckTabRise = 18f;
            var root = NewRect("GuideQuestBar", hud).At(BottomLeft, new Vector2(UISpace.HudMargin, StripHeight + deckTabRise), GuideBarSize);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<GuideQuestBarView>();
            var button = root.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<PressScale>();

            var content = NewRect("Content", root).Stretch();
            // 완료: 판 뒤에서 금빛 빛무리가 숨 쉰다 (뷰가 알파를 흔든다)
            var edge = Img(content, "GoldEdge", MetaUi.Alpha(UITheme.Gold, 0.8f), _icons.Glow);
            edge.rectTransform.Stretch(-34, -34, -30, -30);
            edge.enabled = false;

            // 판 묶음 (가까운 적 · 시너지 띠에 흐려진다 — 글자는 이 밖이라 또렷하게 남는다)
            var chrome = NewRect("Chrome", content).Stretch();
            var chromeGroup = chrome.gameObject.AddComponent<CanvasGroup>();
            var panelArt = ArtUI("panel_night_9s");
            var panel = panelArt != null ? Img(chrome, "Panel", MetaUi.Alpha(Color.white, 0.94f), panelArt, true) : Img(chrome, "Panel", NightPanelColor, RoundSprite, true);
            panel.rectTransform.Stretch();
            if (panelArt != null)
            {
                panel.type = Image.Type.Sliced;
                panel.pixelsPerUnitMultiplier = 2f;
            }
            button.targetGraphic = panel;
            // 누르는 자리 84 → 88 (M22): 위로만 4 — 아래(덱 안내 탭 1576)에는 닿지 않는다. 보이는 자리는 그대로
            panel.raycastPadding = new Vector4(0f, 0f, 0f, -(UISize.MinHit - GuideBarSize.y));

            var iconBack = Plate(chrome, "IconBack", MetaUi.Alpha(UITheme.PanelLight, 0.95f));
            iconBack.rectTransform.At(MiddleLeft, new Vector2(10, 0), new Vector2(64, 64));
            var icon = Img(iconBack.transform, "Icon", Color.white, _icons.Quest);
            icon.preserveAspect = true;
            icon.rectTransform.At(Center, Vector2.zero, new Vector2(52, 52));

            const float barHeight = 10f;
            var barBack = Bar(chrome, "ProgressBar", barHeight, UITheme.Lavender, out var fill);
            barBack.rectTransform.Bottom(2, barHeight, 84, 110);

            var shineMask = NewRect("ShineMask", content).Stretch(6, 6, 6, 6);
            shineMask.gameObject.AddComponent<RectMask2D>();
            var shine = Img(shineMask, "Shine", MetaUi.Alpha(Color.white, 0.3f), First(ArtFx("fx_trail"), First(ArtLib.FX("trail"), RoundSprite)));
            // 가로로 긴 빛 줄기를 거의 세워서 (18도 기울임) 줄을 가로질러 훑는다
            shine.rectTransform.At(Center, Vector2.zero, new Vector2(150, 38));
            shine.rectTransform.localRotation = Quaternion.Euler(0, 0, 72);
            shine.gameObject.SetActive(false);

            // 문구: 27 굵게, 두 줄까지 (맞춤 20 — 넘치면 …)
            var text = Txt(content, "Text", "잔향이를 불러 보세요", 27, UITheme.Text, TextAlignmentOptions.MidlineLeft, true, true).FitFloor(20f).Ellipsis().Outlined();
            text.maxVisibleLines = 2;
            text.lineSpacing = -6f;
            text.rectTransform.Stretch(84, 108, 6, 12);
            var progress = Txt(content, "ProgressText", "0/1", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineRight, true)
                .FitFloor(UIType.AutoSizeFloor).Outlined();
            progress.rectTransform.At(MiddleRight, new Vector2(-12, 0), new Vector2(92, 40));
            // 보상 양 '×20' (보상 그림 받침의 오른쪽 아래 모서리에 걸친다 — 받침 x 10~74, 아래 10)
            var amount = Txt(content, "Amount", "×20", UIType.Hint, UITheme.Cream, TextAlignmentOptions.BottomRight, true)
                .FitFloor(UIType.AutoSizeFloor).Outlined();
            amount.rectTransform.At(BottomLeft, new Vector2(22, 6), new Vector2(60, 24));
            amount.gameObject.SetActive(false);

            var claim = GoldTag(content, "ClaimTag", 46, UITheme.Gold);
            claim.rectTransform.At(MiddleRight, new Vector2(-10, 0), new Vector2(96, 46));
            Txt(claim.transform, "Label", "받기", 26, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            claim.gameObject.SetActive(false);

            var mapper = ctx.BattleRoot.Find("Field") is RectTransform field ? field.GetComponent<BattleFieldMapper>() : null;

            // 비켜 줄 전투 화면: 시너지 발동 띠(칩이 이 줄 높이에 뜬다)와 방위별 위험 화살표
            var synergyBanner = ctx.BattleRoot.Find("SynergyBannerArea/SynergyBanner");
            var arrowViews = ctx.BattleRoot.GetComponentsInChildren<DangerArrowView>(true);
            var arrows = new Object[arrowViews.Length];
            var glows = new Object[arrowViews.Length];
            for (int i = 0; i < arrowViews.Length; i++)
            {
                arrows[i] = arrowViews[i].transform;
                var glow = arrowViews[i].transform.Find("Glow");
                glows[i] = glow != null ? glow.GetComponent<Image>() : null;
            }

            new Binder(view)
                .Set("_icons", _icons)
                .Set("_button", button)
                .Set("_group", group)
                .Set("_chrome", chromeGroup)
                .Set("_content", content)
                .Set("_panel", panel)
                .Set("_goldEdge", edge)
                .Set("_icon", icon)
                .Set("_iconBack", iconBack)
                .Set("_text", text)
                .Set("_progressText", progress)
                .Set("_amountText", amount)
                .Set("_progressFill", fill)
                .Set("_claimTag", claim.gameObject)
                .Set("_shine", shine.rectTransform)
                .Set("_mapper", mapper)
                .Set("_synergyBanner", synergyBanner != null ? synergyBanner.gameObject : null)
                .Set("_synergyBannerGroup", synergyBanner != null ? synergyBanner.GetComponent<CanvasGroup>() : null)
                .SetArray("_dangerArrows", arrows)
                .SetArray("_dangerGlows", glows)
                .Apply();
        }

        /// <summary>
        /// 수선 바구니 위젯 150×170 (오른쪽 24, 위 120). 시트가 열리면 반투명(0.5, 그래도 누를 수 있다 — 다 열렸을 때 4번 길을 가리지 않게)이고
        /// 튜토리얼 동안은 흐리지 않는다 (FTUE 9 · 15단계가 가리킨다) — UX-SPEC-IA §1.6 SheetFade 0.5.
        /// 바구니 아래 시간 알약 'TimePill' 104×32 (Background@0.65 + 2px Lavender@0.5 테두리 — 생성 테두리 그림, 'TimeText' 22 굵게 Cream + 외곽선, M32 · M81),
        /// 가득이면 같은 자리에 'FullTag' '가득!'. 알림은 하나만 (M09 — 점 &gt; 무료 &gt; NEW): 오른쪽 위 배지(hud.idlechest — 빨간 점, 또는 MAIN 배지 예산이
        /// 허락한 NEW, RedDotView 자리 Basket) · 빠른 수선 무료는 모서리 알약이 아니라 시간 알약 글자가 Gold '무료' (IdleChestWidgetView).
        /// </summary>
        static void BuildIdleChestWidget(RectTransform hud)
        {
            var root = NewRect("IdleChestWidget", hud).At(TopRight, new Vector2(-24, -120), new Vector2(150, 170));
            SheetFade.Attach(root.gameObject, 0.5f, true);
            var view = root.gameObject.AddComponent<IdleChestWidgetView>();
            var button = root.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<PressScale>();

            var visual = NewRect("Visual", root).Stretch();
            var hit = Img(visual, "Hit", Color.clear, null, true);
            hit.rectTransform.Stretch();
            button.targetGraphic = hit;
            var glow = Img(visual, "Glow", MetaUi.Alpha(UITheme.Gold, 0.45f), _icons.Glow);
            glow.rectTransform.At(Center, new Vector2(0, 16), new Vector2(230, 230));
            glow.enabled = false;
            var basket = Img(visual, "Basket", Color.white, _icons.Basket(0));
            basket.preserveAspect = true;
            basket.rectTransform.At(TopCenter, new Vector2(0, 0), new Vector2(136, 128));
            basket.enabled = _icons.Basket(0) != null;
            var placeholder = BuildBasketPlaceholder(visual, new Vector2(0, -6), 1f, out var fills);
            placeholder.gameObject.SetActive(_icons.Basket(0) == null);

            // 시간 알약 (바구니 아래, '방금' / 'N분' / 'N시간' — '0:00'은 없다)
            const float pillHeight = 32f;
            var timePill = Img(visual, "TimePill", MetaUi.Alpha(UITheme.Background, 0.65f), RoundSprite);
            timePill.rectTransform.At(BottomCenter, new Vector2(0, 16), new Vector2(104, pillHeight));
            CapsulePpu(timePill, pillHeight);
            var pillEdge = Img(timePill.transform, "Edge", MetaUi.Alpha(UITheme.Lavender, 0.5f), CapsuleRing(pillHeight, 2f));
            pillEdge.rectTransform.Stretch();
            pillEdge.pixelsPerUnitMultiplier = RingPpu(pillHeight);
            var time = Txt(timePill.transform, "TimeText", UIFormat.JustNow, UIType.Caption, UITheme.Cream, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor).Outlined();
            time.rectTransform.Stretch(6, 6, 0, 0);
            var full = GoldTag(visual, "FullTag", 34, UITheme.Gold);
            full.rectTransform.Bottom(15, 34, 22, 22);
            Txt(full.transform, "Label", "가득!", 22, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            full.gameObject.SetActive(false);
            const float barHeight = 12f;
            var barBack = Bar(visual, "FillBar", barHeight, UITheme.Lavender, out var fill);
            barBack.rectTransform.Bottom(2, barHeight, 18, 18);

            SetMainRole(AttachRedDot(visual, RedDotPaths.HudIdleChest, false, new Vector2(-10, -6), 30), MainBadgeRole.Basket);

            new Binder(view)
                .Set("_icons", _icons)
                .Set("_button", button)
                .Set("_visual", visual)
                .Set("_basket", basket)
                .Set("_placeholder", placeholder.gameObject)
                .SetArray("_placeholderFill", fills)
                .Set("_glow", glow)
                .Set("_timeText", time)
                .Set("_timePill", timePill.gameObject)
                .Set("_fill", fill)
                .Set("_fullTag", full.gameObject)
                .Apply();
        }

        /// <summary>둥근 판(RoundSprite, 모서리 24)을 높이 h의 알약으로 (그려지는 모서리 = h/2)</summary>
        static void CapsulePpu(Image image, float height)
        {
            if (image == null || image.sprite == null || image.sprite.border.x <= 0f) return;
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, image.sprite.border.x / (height * 0.5f));
        }

        /// <summary>알약 테두리 그림(CapsuleRing)의 9-slice 배율 — 높이 h에서 둥근 끝이 반원이 되게 (모서리 25 → h/2)</summary>
        static float RingPpu(float height) => RingRadius / Mathf.Max(1f, height * 0.5f);

        const int RingSize = 52;
        const float RingRadius = 25f;

        /// <summary>
        /// 알약(캡슐) 테두리만 그린 흰 9-slice 그림 (Sprites/UI/Meta/META_Ring_{굵기}.png, 52×52, 모서리 25 — 생성, 한 번만 그린다).
        /// 반투명 알약 위에 얇은 테두리를 얹을 때 쓴다 (UnityEngine.UI.Outline은 반투명 판 밑에 비쳐 판 전체가 물든다).
        /// 높이 pillHeight인 알약에 RingPpu(pillHeight) 배율로 그리면 선이 thickness px.
        /// </summary>
        static Sprite CapsuleRing(float pillHeight, float thickness)
        {
            float t = thickness * RingRadius * 2f / Mathf.Max(1f, pillHeight);
            int key = Mathf.RoundToInt(t * 10f);
            float ring = key / 10f;
            return Generated($"META_Ring_{key}", RingSize, RingSize, (u, v) =>
            {
                float half = RingSize * 0.5f;
                float qx = Mathf.Abs(u * RingSize - half) - (half - RingRadius);
                float qy = Mathf.Abs(v * RingSize - half) - (half - RingRadius);
                float outsideX = Mathf.Max(qx, 0f);
                float outsideY = Mathf.Max(qy, 0f);
                // 둥근 사각형 경계까지의 거리 (안쪽이 음수)
                float edge = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - RingRadius;
                float band = Mathf.Abs(edge + ring * 0.5f) - ring * 0.5f;
                return Mathf.Clamp01(0.5f - band);
            }, new Vector4(RingRadius, RingRadius, RingRadius, RingRadius));
        }

        /// <summary>
        /// 바구니 자리표시 (아트가 없을 때): 손잡이 고리 + 바구니 몸통 + 단계마다 차오르는 실타래·단추·빛나는 실 덩어리.
        /// fills[i]는 단계 i+1부터 보인다.
        /// </summary>
        static RectTransform BuildBasketPlaceholder(RectTransform parent, Vector2 offset, float scale, out Object[] fills)
        {
            var root = NewRect("BasketPlaceholder", parent).At(TopCenter, offset, new Vector2(136, 124) * scale);
            var handle = Img(root, "Handle", TanBasket, _icons.Ring != null ? _icons.Ring : CircleSprite);
            handle.rectTransform.At(Center, new Vector2(0, 14) * scale, new Vector2(104, 104) * scale);
            var blobs = new[]
            {
                (name: "Thread", color: UITheme.DustyPink, pos: new Vector2(-24, -6), size: 46f),
                (name: "Button", color: UITheme.Gold, pos: new Vector2(18, -2), size: 40f),
                (name: "GlowThread", color: UITheme.Lavender, pos: new Vector2(0, 14), size: 44f),
            };
            fills = new Object[blobs.Length];
            for (int i = 0; i < blobs.Length; i++)
            {
                var blob = Img(root, blobs[i].name, blobs[i].color, CircleSprite);
                blob.rectTransform.At(Center, blobs[i].pos * scale, Vector2.one * blobs[i].size * scale);
                blob.enabled = false;
                fills[i] = blob;
            }
            var body = Img(root, "Body", TanBasket, RoundSprite);
            body.rectTransform.At(BottomCenter, new Vector2(0, 6) * scale, new Vector2(116, 62) * scale);
            var weave = Img(body.transform, "Weave", new Color(0.62f, 0.45f, 0.3f, 1f), RoundSprite);
            weave.rectTransform.Stretch(8 * scale, 8 * scale, 22 * scale, 22 * scale);
            var rim = Img(root, "Rim", new Color(0.9f, 0.74f, 0.55f, 1f), RoundSprite);
            rim.rectTransform.At(BottomCenter, new Vector2(0, 58) * scale, new Vector2(126, 16) * scale);
            return root;
        }

        /// <summary>
        /// 전투력 비교 알약 (M12, UX-SPEC §2.1.4 — 스테이지 패널 오른쪽). 루트 At(TopLeft, (352, −12), (300, 80)) = 투명 누르는 자리 + Button + PressScale
        /// (시트가 열리면 사라진다 — 알파 1 → 0, 누름 끔, §1.6 SheetFade 0). 그 안 Pill At(MiddleLeft, 0, (300, 52)) Panel@0.88 + 'Edge' 2px 테두리(평가 색 @0.6, 생성 테두리 그림):
        /// 'RatingDot' 14 (x 14) · 'Dot' 전투력 그림 28 (x 34, Icons/guardian — 이모지 없음) · 'Text' x 68부터 칩 4px 앞까지(156) 한 줄 26 (맞춤 16 —
        /// 6자리 '999,999 / 권장 999,999'가 26에서 245px라 18로는 넘쳐 칩을 덮는다, 16에서 151) · 'RatingChip' 64×36 '여유/도전/어려움' 20 굵게.
        /// 시트 위에서는 상단 바 PowerChip을 누르면 같은 비교 토스트가 뜬다 (TopBarMetaView)
        /// </summary>
        /// <summary>추천 전투력 알약 글과 평가 칩 사이 (6 → 4: 글 칸 156px — 6자리 두 수가 16에서 151px로 들어간다)</summary>
        const float RecommendedPowerChipGap = 4f;

        static void BuildRecommendedPower(RectTransform hud)
        {
            const float pillHeight = 52f;
            var hit = Img(hud, "RecommendedPower", Color.clear, null, true);
            var root = hit.rectTransform.At(TopLeft, new Vector2(352, -12), new Vector2(300, 80));
            // 누르는 자리 80 → 88 (M22): 아래로만 8 (위는 상단 바 쪽). 알약 그림 자리는 그대로
            hit.raycastPadding = new Vector4(0f, -(UISize.MinHit - 80f), 0f, 0f);
            SheetFade.Attach(root.gameObject, 0f);
            var view = root.gameObject.AddComponent<RecommendedPowerView>();
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<PressScale>();

            var pill = Img(root, "Pill", MetaUi.Alpha(UITheme.Panel, 0.88f), RoundSprite);
            pill.rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(300, pillHeight));
            CapsulePpu(pill, pillHeight);
            var edge = Img(pill.transform, "Edge", MetaUi.Alpha(UITheme.Positive, 0.6f), CapsuleRing(pillHeight, 2f));
            edge.rectTransform.Stretch();
            edge.pixelsPerUnitMultiplier = RingPpu(pillHeight);
            var ratingDot = Img(pill.transform, "RatingDot", UITheme.Positive, CircleSprite);
            ratingDot.rectTransform.At(MiddleLeft, new Vector2(14, 0), new Vector2(14, 14));
            // 전투력 그림 (흰색 그대로 — 평가 색은 점 · 테두리 · 글자 · 칩에만). 없으면 별
            var guardian = First(ArtIcon("guardian"), _icons.Star);
            var dot = Img(pill.transform, "Dot", Color.white, guardian);
            dot.preserveAspect = true;
            dot.enabled = guardian != null;
            dot.rectTransform.At(MiddleLeft, new Vector2(34, 0), new Vector2(28, 28));
            var chip = Img(pill.transform, "RatingChip", MetaUi.Alpha(UITheme.Positive, 0.2f), RoundSprite);
            chip.rectTransform.At(MiddleRight, new Vector2(-8, 0), new Vector2(64, 36));
            CapsulePpu(chip, 36);
            var chipText = Txt(chip.transform, "Label", "여유", UIType.Hint, UITheme.Positive, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            chipText.rectTransform.Stretch(4, 4, 0, 0);
            var text = Txt(pill.transform, "Text", "0 / 권장 300", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft).FitFloor(UIType.AutoSizeFloor);
            text.rectTransform.Stretch(68, 8 + 64 + RecommendedPowerChipGap, 0, 0);
            new Binder(view)
                .Set("_root", pill.gameObject)
                .Set("_text", text)
                .Set("_dot", dot)
                .Set("_button", button)
                .Set("_ratingDot", ratingDot)
                .Set("_edge", edge)
                .Set("_ratingChip", chip)
                .Set("_ratingText", chipText)
                .Apply();
        }

        /// <summary>
        /// 전투 HUD AUTO 버튼 자리 — TopRight 기준 위치 (M22: 140×76 at (-14,-14), 누르는 칸은 raycastPadding으로 104 높이까지).
        /// 계약 C-13: 전투 빌더(WP-5)가 실제 버튼을 이 자리 · 크기로 바꾸고, 잠금 덮개(BuildLockCover)는 대상 버튼의 앵커 · 위치 · 크기 · raycastPadding을
        /// 그대로 베끼며 버튼을 못 찾을 때만 이 값을 쓴다. 두 빌더가 같은 수를 쓰도록 여기 한 곳에 둔다.
        /// </summary>
        internal static readonly Vector2 HudAutoButtonPos = new Vector2(-14f, -14f);
        /// <summary>전투 HUD AUTO 버튼 크기 (M22 · 계약 C-13 — HudAutoButtonPos 참고)</summary>
        internal static readonly Vector2 HudAutoButtonSize = new Vector2(140f, 76f);
        /// <summary>전투 HUD 배속(x1) 버튼 자리 — TopRight 기준 위치 (M22: 120×76 at (-166,-14) · 계약 C-13 — HudAutoButtonPos 참고)</summary>
        internal static readonly Vector2 HudSpeedButtonPos = new Vector2(-166f, -14f);
        /// <summary>전투 HUD 배속(x1) 버튼 크기 (M22 · 계약 C-13)</summary>
        internal static readonly Vector2 HudSpeedButtonSize = new Vector2(120f, 76f);

        /// <summary>
        /// AUTO / 2배속 버튼 위 잠금 덮개 (M38 · M22, UX-SPEC §2.1.13 — 버튼 자체는 전투 화면 담당이라 같은 자리에 덮고, 버튼의 CanvasGroup은 건드리지 않는다).
        /// 자리 · 크기는 대상 버튼에서 베끼고(없으면 C-13 상수), 덮개 'Cover'는 불투명 — 바깥 2px은 Gold@0.35를 TabNormal에 섞은 테두리, 안쪽 'Fill'은
        /// UITheme.LockCover(TabNormal α1)라 아래 AUTO 글자가 비치지 않는다. 덮개의 누르는 자리는 대상 버튼 그림의 raycastPadding을 그대로 베낀다.
        /// 'Content'(막힘 흔들기를 받는 묶음) 안에 자물쇠 34 + 짧은 'Hint' '1-2' 22 굵게 Gold (맞춤 18, Stretch(44, 6, 8, 0)).
        /// 'UnlockGlow'(열린 뒤 2초 빛 — 버튼 둘레 28px 빛 테 GlowFrame, 가운데는 비었다) · 'NewDot'(SceneParts.BuildBadge 표준 NEW 알약 52x26 · 17, 오른쪽 끝 밖으로 6까지만 — §3.13.10 · M23)은 꺼 둔다.
        /// MetaHud는 Battle/Hud 뒤에 만든 따로 캔버스라 덮개 묶음 전체가 실제 버튼 위에 그려진다 — 그래서 빛은 둥근 빛(가운데까지 차는)이 아니라
        /// 테두리만 빛나는 테로, 열린 버튼의 AUTO / x1 글자를 덮지 않고 둘레만 밝힌다.
        /// </summary>
        static void BuildLockCover(SceneBuildContext ctx, RectTransform hud, string buttonName, FeatureId feature, Vector2 fallbackPos, Vector2 fallbackSize)
        {
            var target = ctx.BattleRoot.Find("Hud/" + buttonName) as RectTransform;
            var targetButton = target != null ? target.GetComponent<Button>() : null;
            var targetGraphic = targetButton != null ? targetButton.targetGraphic : target != null ? target.GetComponent<Graphic>() : null;
            var root = NewRect(buttonName + "Lock", hud);
            if (target != null)
            {
                root.anchorMin = target.anchorMin;
                root.anchorMax = target.anchorMax;
                root.pivot = target.pivot;
                root.anchoredPosition = target.anchoredPosition;
                root.sizeDelta = target.sizeDelta;
            }
            else
            {
                root.At(TopRight, fallbackPos, fallbackSize);
            }
            var view = root.gameObject.AddComponent<FeatureLockCover>();

            // 열린 뒤 2초 숨 쉬는 빛 테 (버튼 위에 그려지므로 가운데가 빈 테 — 버튼 둘레만 빛나고 글자는 그대로 보인다)
            var glow = Img(root, "UnlockGlow", MetaUi.Alpha(UITheme.Lamp, 0f), GlowFrame(UnlockGlowPad, UnlockGlowRadius));
            glow.rectTransform.Stretch(-UnlockGlowPad, -UnlockGlowPad, -UnlockGlowPad, -UnlockGlowPad);
            glow.enabled = false;

            var cover = Img(root, "Cover", Color.Lerp(UITheme.LockCover, UITheme.Gold, UITheme.LockCoverEdge.a), RoundSprite, true);
            cover.rectTransform.Stretch();
            if (targetGraphic != null) cover.raycastPadding = targetGraphic.raycastPadding;
            var fill = Img(cover.transform, "Fill", UITheme.LockCover, RoundSprite);
            fill.rectTransform.Stretch(2, 2, 2, 2);
            cover.gameObject.AddComponent<CanvasGroup>();
            var coverButton = cover.gameObject.AddComponent<Button>();
            coverButton.targetGraphic = cover;
            coverButton.transition = Selectable.Transition.None;
            coverButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var content = NewRect("Content", cover.transform).Stretch();
            var lockSlot = NewRect("LockSlot", content).At(MiddleLeft, new Vector2(10, 0), new Vector2(36, 36));
            var lockIcon = LockIcon(lockSlot, 34);
            var hint = Txt(content, "Hint", "1-2", UIType.Caption, UITheme.Gold, TextAlignmentOptions.Center, true).FitFloor(UIType.HintMin);
            hint.rectTransform.Stretch(44, 6, 8, 0);

            // 표준 NEW 알약 52x26 · 17 (작은 알약의 15는 자동 크기 바닥 16 아래였다 — Wave 2 r2 M23). 알약이 버튼 오른쪽 끝 밖으로
            // LockNewOverhang까지만 나가게 안쪽으로 옮긴다 (AUTO는 화면 끝에서 14, x1 ↔ AUTO 틈 10 — 그대로면 18이 나가 이웃 · 화면 끝을 넘는다)
            var newBadge = BuildBadge(root, UISize.BadgeDot);
            newBadge.name = "NewDot";
            ((RectTransform)newBadge.transform).anchoredPosition -= new Vector2(LockNewShift, 0f);
            newBadge.gameObject.SetActive(false);

            new Binder(view)
                .SetInt("_feature", (int)feature)
                .Set("_cover", cover.gameObject)
                .Set("_lockIcon", lockIcon)
                .Set("_hintText", hint)
                .Set("_coverButton", coverButton)
                .Set("_target", targetButton)
                .Set("_newDot", newBadge.gameObject)
                .Set("_newBadge", newBadge)
                .Set("_content", content)
                .Set("_glow", glow)
                .Apply();
        }

        /// <summary>잠금 덮개 NEW 알약이 버튼 오른쪽 끝 밖으로 나가는 최대 폭</summary>
        const float LockNewOverhang = 6f;
        /// <summary>
        /// 잠금 덮개 NEW 배지를 표준 자리(중심 = 모서리에서 BadgeInset 안쪽)보다 왼쪽으로 옮기는 폭:
        /// 알약 반폭 26 − 나가는 폭 6 − BadgeInset 8 = 12 (알약 오른쪽 끝 = 버튼 오른쪽 끝 + 6)
        /// </summary>
        static float LockNewShift => UISize.BadgeNewWidth * 0.5f - LockNewOverhang - UISize.BadgeInset;

        /// <summary>잠금 덮개 열림 빛 테: 버튼 바깥으로 번지는 폭 · 버튼 모서리 둥글기 (GlowFrame)</summary>
        const int UnlockGlowPad = 28;
        const int UnlockGlowRadius = 20;
        /// <summary>빛 테가 버튼 안쪽으로 스미는 폭 (이만큼 들어가면 투명 — 글자를 덮지 않는다)</summary>
        const float GlowFrameInnerSoft = 5f;

        /// <summary>
        /// 둥근 사각형 둘레만 빛나는 흰 9-slice 그림 (Sprites/UI/Meta/META_GlowFrame_{pad}_{radius}.png — 생성, 한 번만 그린다).
        /// 가장자리에서 pad 안쪽의 둥근 사각형(모서리 radius) 선에서 가장 밝고, 바깥으로 pad 동안 (1 − d/pad)²로 사라지며,
        /// 안쪽은 GlowFrameInnerSoft(5px) 만에 투명해진다. 대상보다 pad씩 크게(Stretch(−pad)) 배율 1로 그리면 대상의 테두리만 빛난다
        /// </summary>
        static Sprite GlowFrame(int pad, int radius)
        {
            int border = pad + radius + 2;
            int size = border * 2 + 4;
            return Generated($"META_GlowFrame_{pad}_{radius}", size, size, (u, v) =>
            {
                float half = size * 0.5f;
                float straight = half - pad - radius;
                float qx = Mathf.Abs(u * size - half) - straight;
                float qy = Mathf.Abs(v * size - half) - straight;
                float outsideX = Mathf.Max(qx, 0f);
                float outsideY = Mathf.Max(qy, 0f);
                // 둥근 사각형 경계까지의 거리 (바깥 +, 안쪽 −)
                float edge = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                if (edge < 0f) return Mathf.Clamp01(1f + edge / GlowFrameInnerSoft);
                float k = Mathf.Clamp01(1f - edge / pad);
                return k * k;
            }, new Vector4(border, border, border, border));
        }

        /// <summary>자물쇠 아이콘 (아트가 없으면 고리 + 몸통 도형). 반환값은 흔들기·튀기기를 받는 루트</summary>
        static RectTransform LockIcon(RectTransform parent, float size)
        {
            var root = NewRect("Lock", parent).At(Center, Vector2.zero, new Vector2(size, size));
            if (_icons.Lock != null)
            {
                var image = Img(root, "Icon", Color.white, _icons.Lock);
                image.preserveAspect = true;
                image.rectTransform.Stretch();
                return root;
            }
            var shackle = Img(root, "Shackle", UITheme.Cream, _icons.Ring != null ? _icons.Ring : CircleSprite);
            shackle.rectTransform.At(TopCenter, new Vector2(0, 0), new Vector2(size * 0.62f, size * 0.62f));
            var body = Img(root, "Body", UITheme.Gold, RoundSprite);
            body.rectTransform.At(BottomCenter, Vector2.zero, new Vector2(size * 0.82f, size * 0.6f));
            Img(body.transform, "Hole", UITheme.Ink, CircleSprite).rectTransform.At(Center, Vector2.zero, new Vector2(size * 0.18f, size * 0.18f));
            return root;
        }

        // ═════════════════════════ 빠른 아이콘 (UX-SPEC-IA §3.3) ═════════════════════════

        /// <summary>
        /// 빠른 아이콘 줄 두 개: MetaHud/QuickRail_L · QuickRail_R (Stretch = StageArea, QuickRailView + SheetFade 0 — 시트가 열리면 사라진다).
        /// 왼쪽 = EventButton 이벤트 (위 주머니 1칸, 점 top.event, 'D-n' SubPill — 별이 머문 밤 산책 중) · AttendanceButton 출석부 (아래, FTUE 끝, 점 top.attendance),
        /// 오른쪽 = StatsButton 전투 기록 (위 주머니 R1 — SPEC4 CR-03 · UX-SPEC-IA §8.8, BattleStats 1-7, 점 없음 — 나타날 때 한 번 튀어나옴) ·
        /// JournalButton 수선 일지 (아래, 숫자 top.journal). 이름 · 경로는 고정 (CoachTargets, §5.8 — 칸은 줄의 바로 아래 자식).
        /// 칸은 v1 기본 자리(QuickRailView.PlaceSlot)에 놓아 둔다 — 실행 중에는 열린 아이콘만 우선순위대로 다시 놓는다 (FTUE 동안은 비어 있다).
        /// 줄마다 더보기 칸 + 바깥 누름 막 + 쟁반을 만들어 꺼 둔다 (아래 아이콘이 2개를 넘을 때만 — v1에서는 보이지 않는다).
        /// </summary>
        static void BuildQuickRails(RectTransform hud)
        {
            var left = BuildRail(hud, "QuickRail_L");
            var eventIcon = BuildRailCell(left, "EventButton", "이벤트", EventIconArt(), FeatureId.Events, RedDotPaths.TopEvent,
                false, true, false, true);
            var attendance = BuildRailCell(left, "AttendanceButton", "출석부", _icons.Attendance, FeatureId.Attendance, RedDotPaths.TopAttendance,
                false, false, true);
            QuickRailView.PlaceSlot((RectTransform)eventIcon.transform, 1, false);
            QuickRailView.PlaceSlot((RectTransform)attendance.transform, 3, false);
            FinishRail(left, false, eventIcon, attendance);

            var right = BuildRail(hud, "QuickRail_R");
            var journal = BuildRailCell(right, "JournalButton", "수선 일지", _icons.Journal, FeatureId.Missions, RedDotPaths.TopJournal,
                true, false, false);
            // 전투 기록 (CR-03): R1 위 주머니, 점 없음 (M09 — HUD에 새 점을 만들지 않는다), 누르면 UINav.OpenBattleStats (P4-3b 팝업)
            var stats = BuildRailCell(right, "StatsButton", "전투 기록", StatsIconArt(), FeatureId.BattleStats, null, false, true, false);
            QuickRailView.PlaceSlot((RectTransform)stats.transform, 1, true);
            QuickRailView.PlaceSlot((RectTransform)journal.transform, 3, true);
            FinishRail(right, true, journal, stats);
        }

        /// <summary>빠른 아이콘 줄 루트 (Stretch = StageArea, 그림 없음 — 누름을 막지 않는다). 시트가 열리면 사라진다 (SheetFade 0, 전용 CanvasGroup)</summary>
        static RectTransform BuildRail(RectTransform hud, string name)
        {
            var rail = NewRect(name, hud).Stretch();
            SheetFade.Attach(rail.gameObject, 0f);
            return rail;
        }

        /// <summary>이벤트 빠른 아이콘 그림 (Icons/currency_event → 별빛 사탕 재화 그림)</summary>
        static Sprite EventIconArt() => First(ArtLib.Icon("currency_event"), ArtLib.Currency(CurrencyType.EventToken));

        /// <summary>전투 기록 빠른 아이콘 그림 (Icons/stats — 아직 없음, 그림 요청 → Icons/quest → 별). 계획된 그림이라 없다는 경고를 남기지 않는다</summary>
        static Sprite StatsIconArt() => First(ArtLib.Icon("stats"), First(ArtLib.Icon("quest"), _icons.Star));

        /// <summary>
        /// 빠른 아이콘 한 칸 104x116 (§3.3 칸 모양): 칸 전체가 투명 누름 판 + Button + PressScale (칸 피벗 가운데 — 누름이 가운데 기준),
        /// Visual 안에 메달 판 · 아이콘 · 이름 알약 (BuildRailVisual) + 판 오른쪽 위 (−4, −4)에 빨간 점 (판의 RedDotView — 수선 일지는 숫자).
        /// 자리는 부르는 쪽이 QuickRailView.PlaceSlot으로 잡는다
        /// </summary>
        static QuickIconView BuildRailCell(RectTransform rail, string name, string label, Sprite iconArt, FeatureId feature, string dotPath,
            bool count, bool topPocket, bool afterFtue, bool subPill = false)
        {
            var hit = Img(rail, name, Color.clear, null, true);
            var root = hit.rectTransform;
            QuickRailView.PlaceSlot(root, 3, false);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<PressScale>();

            var visual = BuildRailVisual(root, label, iconArt, out var plate, out _);
            float dotSize = count ? UISize.BadgeCount : UISize.BadgeDot;
            float corner = dotSize * 0.5f - 4f;
            // 점 경로가 없는 칸(전투 기록)은 점을 달지 않는다 (M09)
            if (!string.IsNullOrEmpty(dotPath))
                SetMainRole(AttachRedDot(plate.transform, dotPath, count, new Vector2(corner, corner), dotSize), MainBadgeRole.Rail);

            // 'D-n' SubPill (UX-SPEC-IA §3.3 · SPEC4 §8.3): 칸 위로 10 솟은 64×26 밤 알약, Micro 18 Gold — 실행 중에 QuickIconView가 켠다
            GameObject pill = null;
            TextMeshProUGUI pillText = null;
            if (subPill)
            {
                var pillImage = UiArt.Panel(visual, "SubPill", UiArt.PillNight9s, UiArt.CapsulePpu(26), MetaUi.Alpha(Color.white, 0.94f),
                    MetaUi.Alpha(UITheme.Background, 0.86f));
                var pillRect = pillImage.rectTransform;
                pillRect.anchorMin = pillRect.anchorMax = new Vector2(0.5f, 1f);
                pillRect.pivot = new Vector2(0.5f, 1f);
                pillRect.anchoredPosition = new Vector2(0f, 10f);
                pillRect.sizeDelta = new Vector2(64, 26);
                pillText = Txt(pillImage.transform, "Text", "D-14", UIType.Micro, UITheme.Gold, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
                pillText.rectTransform.Stretch(4, 4, 0, 0);
                pill = pillImage.gameObject;
                pill.SetActive(false);
            }

            var view = root.gameObject.AddComponent<QuickIconView>();
            new Binder(view)
                .SetInt("_feature", (int)feature)
                .Set("_button", button)
                .Set("_visual", visual)
                .Set("_subPill", pill)
                .Set("_subPillText", pillText)
                .Apply();
            var so = new SerializedObject(view);
            so.FindProperty("_dotPath").stringValue = dotPath ?? string.Empty;
            so.FindProperty("_topPocket").boolValue = topPocket;
            so.FindProperty("_afterFtue").boolValue = afterFtue;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>
        /// 빠른 아이콘 칸 그림 묶음 "Visual" (나타남 0 → 1.2 → 1을 받는다, 누름 없음): Plate 어두운 메달 88 (위 가운데, UiArt.MedallionDark) ·
        /// Icon 62 (판 가운데, 비율 유지 — 그림이 없으면 끈다) · LabelPill 100x28 (아래 가운데, 밤색 바느질 알약 ui_pill_night_9s @0.94 — 밝은 전장 위에서도
        /// 글자 바탕이 또렷하게, 아트가 없으면 둥근 판 Background@0.86 · 이름 20 굵게 Cream — 알약 위라 외곽선 없음, 7.42).
        /// 판 대비 (7.42, 칸 사각형은 그대로): 판 뒤 'PlateShade' 부드러운 원 100 Background@0.45 (아래로 3 — 밝은 전장에서 메달이 떠 보이지 않게 받친다,
        /// FX/circle_soft — 없으면 만들지 않는다) · 판 안 아이콘 뒤 'IconLift' 빛 번짐 78 Lavender@0.35 (어두운 메달 위 어두운 아이콘의 윤곽을 띄운다,
        /// MetaIcons.Glow — 없으면 만들지 않는다). 둘 다 누름을 받지 않고 Visual 안이라 나타남 연출 · 시트 흐림을 같이 받는다
        /// </summary>
        /// <summary>빠른 아이콘 판 뒤 그림자 원 알파 · 판 안 아이콘 뒤 빛 번짐 알파 (7.42 판 대비)</summary>
        const float RailPlateShadeAlpha = 0.45f;
        const float RailIconLiftAlpha = 0.35f;

        static RectTransform BuildRailVisual(RectTransform root, string label, Sprite iconArt, out Image plate, out Image icon)
        {
            var visual = NewRect("Visual", root).Stretch();
            var shadeArt = ArtLib.FX("circle_soft");
            if (shadeArt != null)
            {
                var shade = Img(visual, "PlateShade", MetaUi.Alpha(UITheme.Background, RailPlateShadeAlpha), shadeArt);
                float shadeSize = UISize.RailPlate + 12f;
                // 판과 같은 가운데에서 3px 아래 (판 둘레로 6px씩 번지고 아래쪽이 조금 더 짙게 보인다)
                shade.rectTransform.At(TopCenter, new Vector2(0f, (shadeSize - UISize.RailPlate) * 0.5f - 3f), new Vector2(shadeSize, shadeSize));
            }
            var plateArt = UiArt.MedallionDark;
            bool plateIsArt = UiArt.IsArt(plateArt);
            plate = Img(visual, "Plate", plateIsArt ? Color.white : MetaUi.Alpha(UITheme.Panel, 0.92f), plateArt);
            plate.preserveAspect = plateIsArt;
            plate.rectTransform.At(TopCenter, Vector2.zero, new Vector2(UISize.RailPlate, UISize.RailPlate));
            var liftArt = _icons != null ? _icons.Glow : null;
            if (liftArt != null)
            {
                var lift = Img(plate.transform, "IconLift", MetaUi.Alpha(UITheme.Lavender, RailIconLiftAlpha), liftArt);
                lift.rectTransform.At(Center, Vector2.zero, new Vector2(UISize.RailIcon + 16f, UISize.RailIcon + 16f));
            }
            icon = Img(plate.transform, "Icon", Color.white, iconArt);
            icon.preserveAspect = true;
            icon.enabled = iconArt != null;
            icon.rectTransform.At(Center, Vector2.zero, new Vector2(UISize.RailIcon, UISize.RailIcon));
            var pill = UiArt.Panel(visual, "LabelPill", UiArt.PillNight9s, UiArt.CapsulePpu(UISize.RailLabelHeight),
                MetaUi.Alpha(Color.white, 0.94f), MetaUi.Alpha(UITheme.Background, 0.86f));
            pill.raycastTarget = false;
            pill.rectTransform.At(BottomCenter, Vector2.zero, new Vector2(UISize.RailLabelWidth, UISize.RailLabelHeight));
            var text = Fit(Txt(pill.transform, "Label", label, UIType.Hint, UITheme.Cream, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            text.rectTransform.Stretch(4, 4, 0, 0);
            return visual;
        }

        /// <summary>
        /// 더보기 칸 "MoreButton" (아래 아이콘이 2개를 넘을 때 2칸 — 꺼 둔다): 칸 모양은 아이콘과 같고 그림은 위로 돌린 꺾쇠(UiArt.Chevron 48),
        /// 이름 '더보기', 배지 = 쟁반으로 숨은 아이콘 중 받을 것이 있는 수 (QuickRailView가 쓴다)
        /// </summary>
        static void BuildMoreCell(RectTransform rail, bool right, out RectTransform cell, out Button button, out Badge badge)
        {
            var hit = Img(rail, "MoreButton", Color.clear, null, true);
            cell = hit.rectTransform;
            QuickRailView.PlaceSlot(cell, 2, right);
            button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            cell.gameObject.AddComponent<PressScale>();

            BuildRailVisual(cell, "더보기", null, out var plate, out var icon);
            var chevron = UiArt.Chevron;
            if (chevron != null)
            {
                icon.sprite = chevron;
                icon.enabled = true;
                icon.rectTransform.sizeDelta = new Vector2(48, 48);
                // 꺾쇠 그림은 오른쪽을 가리킨다 → 90도 돌려 위로 (쟁반이 위로 열린다)
                icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            badge = BuildBadge(plate.transform);
            ((RectTransform)badge.transform).At(TopRight, new Vector2(UISize.BadgeCount * 0.5f - 4f, UISize.BadgeCount * 0.5f - 4f),
                new Vector2(UISize.BadgeCount, UISize.BadgeCount));
            badge.gameObject.SetActive(false);
            cell.gameObject.SetActive(false);
        }

        /// <summary>
        /// 더보기 쟁반 (꺼 둔다): TrayBlocker(줄 전체 투명 막 — 누르면 닫힘, 쟁반 아래 형제) + Tray(밤 판 @0.94, 폭 128, 더보기 칸 위로 쌓임 — QuickRailView.PlaceTray,
        /// 누름을 받아 판 여백 누름이 막으로 새지 않는다) / Viewport(RectMask2D) / Content(아래 붙임 — 대리 칸이 실행 중에 들어간다, 4줄을 넘으면 스크롤)
        /// </summary>
        static void BuildTray(RectTransform rail, bool right, out Button blocker, out RectTransform tray, out ScrollRect scroll, out RectTransform content)
        {
            var block = Img(rail, "TrayBlocker", Color.clear, null, true);
            block.rectTransform.Stretch();
            blocker = block.gameObject.AddComponent<Button>();
            // 누름은 같은 오브젝트의 투명 Image가 받는다. 대상 그림을 비워 두는 것은 일부러다 — 쟁반이 열린 동안만 켜지는 바깥 누름 막이라
            // 끝 검사(IdleSceneBuilder.LintHitTargets: 꺼진 버튼도 형제끼리 겹침을 잰다)가 줄의 모든 칸과 겹친다고 경고하지 않게
            blocker.targetGraphic = null;
            blocker.transition = Selectable.Transition.None;
            blocker.navigation = new Navigation { mode = Navigation.Mode.None };
            block.gameObject.SetActive(false);

            float oneRow = UISize.RailCellHeight + 12f;
            var panel = Sliced(rail, "Tray", "panel_night_9s", 2f, MetaUi.Alpha(UITheme.Panel, 0.94f), true);
            if (panel.sprite != RoundSprite) panel.color = MetaUi.Alpha(Color.white, 0.94f);
            tray = panel.rectTransform;
            QuickRailView.PlaceTray(tray, right, oneRow);
            scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var viewport = NewRect("Viewport", tray).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            content = NewRect("Content", viewport);
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, oneRow);
            scroll.viewport = viewport;
            scroll.content = content;
            panel.gameObject.SetActive(false);
        }

        /// <summary>줄을 마무리한다: 더보기 칸 · 쟁반을 만들고 QuickRailView를 묶는다 (아이콘 = 우선순위 순서)</summary>
        static void FinishRail(RectTransform rail, bool right, params QuickIconView[] icons)
        {
            BuildMoreCell(rail, right, out var moreCell, out var moreButton, out var moreBadge);
            BuildTray(rail, right, out var blocker, out var tray, out var scroll, out var content);
            var view = rail.gameObject.AddComponent<QuickRailView>();
            var objects = new Object[icons.Length];
            for (int i = 0; i < icons.Length; i++) objects[i] = icons[i];
            new Binder(view)
                .SetArray("_icons", objects)
                .Set("_moreCell", moreCell)
                .Set("_moreButton", moreButton)
                .Set("_moreBadge", moreBadge)
                .Set("_tray", tray)
                .Set("_trayScroll", scroll)
                .Set("_trayContent", content)
                .Set("_trayBlocker", blocker)
                .Apply();
            var so = new SerializedObject(view);
            so.FindProperty("_rightSide").boolValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════════ 상단 바 ═════════════════════════

        /// <summary>
        /// 상단 바 메타 (UX-SPEC-IA §3.1 · CR-14): 화면 틀 담당이 만든 PowerChip/PowerText에 전투력 숫자를 묶는다
        /// (TopBarMetaView — 숫자만 PowerSystem.Format, 0.5초 카운트업 + 튀기기). PowerChip을 누르면 권장 전투력 토스트 —
        /// 칩에 버튼이 없으면 여기서 칩 판 Image에 붙인다.
        /// 수선 일지 · 출석부 버튼(→ 빠른 아이콘 줄)과 "전투력 · 초당 단추" 줄(PowerStatsText)은 더 이상 만들지 않고,
        /// GoldPerSecText도 숨기지 않는다 (TopBarView가 '초당 +{rate}'를 쓴다).
        /// 예전 상단 바(PowerChip 없음)에서는 묶을 곳이 없어 전투력 숫자를 보이지 않는다 (경고 한 번 — 장면 만들기는 그대로 된다).
        /// </summary>
        static void BuildTopBar(SceneBuildContext ctx)
        {
            var bar = ctx.TopBar;
            var chip = bar.Find("PowerChip") as RectTransform;
            TMP_Text power = null;
            var powerTransform = chip != null ? chip.Find("PowerText") : null;
            if (powerTransform == null) powerTransform = UIFind.Deep(bar, "PowerText", false);
            if (powerTransform != null) power = powerTransform.GetComponent<TMP_Text>();
            Button powerButton = null;
            if (chip != null)
            {
                powerButton = chip.GetComponent<Button>();
                if (powerButton == null) powerButton = MakeChipTappable(chip);
            }
            if (power == null)
            {
                Debug.LogWarning("[MetaSceneBuilder] 상단 바에 PowerChip/PowerText가 없어 전투력 숫자를 묶지 못했습니다 " +
                                 "(UX-SPEC-IA §3.1 — 화면 틀의 새 상단 바가 만든다).");
            }

            var view = NewRect("MetaTopBar", bar).gameObject.AddComponent<TopBarMetaView>();
            new Binder(view)
                .Set("_statsText", power)
                .Set("_statsPunch", power != null ? power.rectTransform : null)
                .Set("_powerButton", powerButton)
                .Apply();
        }

        /// <summary>
        /// PowerChip에 버튼이 없을 때: 칩 판(칩 자신의 Image)이 누름을 받게 하고 Button(색 눌림, 탭 바 · 버튼과 같은 눌림 색)을 붙인다.
        /// 누르는 자리는 위아래로 88까지 넓힌다 (M22 — 300x62 칩 → 300x88, 상단 바 130 안). 칩 글자는 TopBarMetaView · PowerUpFloatView가 튀기므로
        /// 크기 누름(PressScale)은 붙이지 않는다. 판 Image가 없으면 null
        /// </summary>
        static Button MakeChipTappable(RectTransform chip)
        {
            var graphic = chip.GetComponent<Image>();
            if (graphic == null) return null;
            graphic.raycastTarget = true;
            graphic.HitPad(UISize.MinHit, UISize.MinHit);
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = HighlightedTint;
            colors.pressedColor = PressedTint;
            colors.selectedColor = Color.white;
            colors.fadeDuration = TintFade;
            button.colors = colors;
            return button;
        }

        /// <summary>버튼에 빨간 점 (RedDotView는 버튼에, Badge는 자식)</summary>
        internal static RedDotView AttachRedDot(Transform target, string path, bool count, Vector2 position, float size)
        {
            var badge = BuildBadge(target);
            badge.name = "MetaDot";
            var rect = (RectTransform)badge.transform;
            rect.At(TopRight, position, new Vector2(size, size));
            var label = badge.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.fontSize = label.fontSizeMax = Mathf.Max(16f, size * 0.55f);
            badge.gameObject.SetActive(false);
            var view = target.gameObject.AddComponent<RedDotView>();
            new Binder(view).Set("_badge", badge).Apply();
            var so = new SerializedObject(view);
            so.FindProperty("_path").stringValue = path;
            so.FindProperty("_showCount").boolValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>
        /// 빨간 점 뷰를 MAIN 배지 예산(MainBadgeBudget — M09 · UX-SPEC-IA §8.5)에 넣는다: 탭 · 빠른 아이콘 · 바구니.
        /// AttachRedDot 기본은 None(예산 밖 — 메뉴 칸 · 설정 등 다른 빌더가 부르는 곳은 그대로)
        /// </summary>
        static RedDotView SetMainRole(RedDotView view, MainBadgeRole role)
        {
            if (view == null) return null;
            var so = new SerializedObject(view);
            so.FindProperty("_mainRole").intValue = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ═════════════════════════ 탭 잠금 ═════════════════════════

        static void BuildTabGate(SceneBuildContext ctx)
        {
            int count = ctx.TabButtons.Length;
            var iconGroups = new Object[count];
            var labelGroups = new Object[count];
            var locks = new Object[count];
            var hints = new Object[count];
            var lockIcons = new Object[count];
            var tabDots = new Object[count];

            for (int i = 0; i < count; i++)
            {
                var button = ctx.TabButtons[i];
                if (button == null) continue;
                var tab = (MainTab)i;
                var icon = button.transform.Find("Icon");
                var label = button.transform.Find("Label");
                if (icon != null) iconGroups[i] = AddGroup(icon.gameObject);
                if (label != null) labelGroups[i] = AddGroup(label.gameObject);

                // 고른 탭에서는 TabGateView가 숨긴다 (§3.13.1)
                // MAIN 배지 예산 (MainBadgeBudget, M09): 받을 것 점은 5개 안에서, NEW는 화면 전체에 하나일 때만
                tabDots[i] = SetMainRole(AttachRedDot(button.transform, TabGateView.RedDotPathOf(tab), false, new Vector2(-30, -10), 28), MainBadgeRole.Tab);

                if (TabGateView.FeatureOf(tab) == FeatureId.Battle) continue;
                var lockRoot = NewRect("TabLock", button.transform).At(TopCenter, new Vector2(0, -14), new Vector2(92, 92));
                var slot = NewRect("LockSlot", lockRoot).At(Center, new Vector2(0, 10), new Vector2(48, 48));
                var lockIcon = LockIcon(slot, 48);
                var hintPill = Sliced(lockRoot, "HintPill", "ui_pill_night_9s", 3.5f, DeepInk);
                hintPill.rectTransform.At(BottomCenter, new Vector2(0, 2), new Vector2(66, 28));
                var hint = Fit(Txt(hintPill.transform, "Hint", "1-4", 20, UITheme.Gold, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
                hint.rectTransform.Stretch(4, 4, 0, 0);
                lockRoot.gameObject.SetActive(false);

                locks[i] = lockRoot.gameObject;
                hints[i] = hint;
                lockIcons[i] = lockIcon;
            }

            var view = ctx.TabBar.gameObject.AddComponent<TabGateView>();
            new Binder(view)
                .Set("_controller", ctx.TabController)
                .SetArray("_iconGroups", iconGroups)
                .SetArray("_labelGroups", labelGroups)
                .SetArray("_locks", locks)
                .SetArray("_hints", hints)
                .SetArray("_lockIcons", lockIcons)
                .SetArray("_tabDots", tabDots)
                .Apply();

            if (ctx.TabMain != null) BuildMainDot(ctx);
        }

        /// <summary>
        /// 가운데 사냥 버튼(Tab_Main, 잠기지 않음 — TabGate 배열 밖)의 모아 보기 점 (UX-SPEC-IA §2.3 · §5.9 · CR-07):
        /// MetaDot At(TopCenter, (58, −14), (30, 30)) — 탭 바 빌더가 만들어 두었으면 그것을 쓰고, 없으면 여기서 만든다.
        /// RedDotAnyView는 늘 켜진 Tab_Main 루트에 붙인다: 경로 = hud.guidequest ∪ 빠른 아이콘 줄이 지금 놓은 아이콘의 점 경로,
        /// 받을 것만 (IsClaimOn), 시트가 열린 동안만 (MAIN에서는 그 점들이 빠른 아이콘 · 가이드 퀘스트 줄에 이미 보인다)
        /// </summary>
        static void BuildMainDot(SceneBuildContext ctx)
        {
            var main = ctx.TabMain.transform;
            Badge badge = null;
            var existing = main.Find("MetaDot");
            if (existing != null)
            {
                badge = existing.GetComponent<Badge>();
                if (badge == null) badge = existing.gameObject.AddComponent<Badge>();
                foreach (var graphic in existing.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            else
            {
                badge = BuildBadge(main, UISize.BadgeDot);
                badge.name = "MetaDot";
                ((RectTransform)badge.transform).At(TopCenter, new Vector2(58, -14), new Vector2(UISize.BadgeDot, UISize.BadgeDot));
            }
            badge.gameObject.SetActive(false);

            var rails = ctx.BattleRoot != null ? ctx.BattleRoot.GetComponentsInChildren<QuickRailView>(true) : Array.Empty<QuickRailView>();
            var railObjects = new Object[rails.Length];
            for (int i = 0; i < rails.Length; i++) railObjects[i] = rails[i];
            var view = main.gameObject.AddComponent<RedDotAnyView>();
            new Binder(view)
                .Set("_badge", badge)
                .SetArray("_rails", railObjects)
                .Apply();
            var so = new SerializedObject(view);
            var paths = so.FindProperty("_paths");
            paths.arraySize = 1;
            paths.GetArrayElementAtIndex(0).stringValue = RedDotPaths.HudGuideQuest;
            so.FindProperty("_onlyWhileSheetOpen").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static CanvasGroup AddGroup(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        /// <summary>원정 탭의 예전 숫자 배지(ExpeditionTabBadge)를 메타가 켜지면 치우도록 TabGateView에 알려 준다</summary>
        static void RetireLegacyTabBadges(SceneBuildContext ctx)
        {
            if (ctx.TabBar == null || ctx.TabButtons == null) return;
            var gate = ctx.TabBar.GetComponent<TabGateView>();
            if (gate == null) return;
            var owners = new List<Object>();
            var badges = new List<Object>();
            foreach (var button in ctx.TabButtons)
            {
                if (button == null) continue;
                bool hasOwner = false, foundBadge = false;
                foreach (var behaviour in button.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null || behaviour.GetType().Name != "ExpeditionTabBadge") continue;
                    owners.Add(behaviour);
                    hasOwner = true;
                    // 그 컴포넌트가 실제로 켜고 끄는 배지만 치운다 (다른 탭의 "Badge"는 다른 화면 것일 수 있다)
                    var owned = new SerializedObject(behaviour).FindProperty("_badge")?.objectReferenceValue as Component;
                    if (owned == null) continue;
                    foundBadge = true;
                    if (!badges.Contains(owned.gameObject)) badges.Add(owned.gameObject);
                }
                if (!hasOwner || foundBadge) continue;
                // 배지 참조를 못 읽었을 때만 같은 버튼의 "Badge" 자식을 이름으로 찾는다
                foreach (Transform child in button.transform)
                {
                    if (child.name == "Badge" && child.GetComponent<Badge>() != null) badges.Add(child.gameObject);
                }
            }
            new Binder(gate).SetArray("_legacyBadgeOwners", owners.ToArray()).SetArray("_legacyBadges", badges.ToArray()).Apply();
        }

        // ═════════════════════════ 수선 일지 ═════════════════════════

        /// <summary>
        /// 수선 일지 940×1500 (Popups/JournalPopup — 코치 경로 Window/Body/ClaimAllButton 그대로).
        /// 쪽 탭 Tab0~2(흰 바느질 탭 그림 — 색은 JournalPopup이 TabStyle Page로), 점수 판 + 상자 5개(상자 위 보상 이름표 'Reward', 누름 크기),
        /// 미션 줄 860×150, 아래 오른쪽 [모두 받기] = UIKit.ClaimAllButton 380×96 · 32 (Body 바로 아래 자식, 금, 개수는 배지 — M51).
        /// </summary>
        static JournalPopup BuildJournalPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<JournalPopup>(ctx.PopupRoot, "JournalPopup", "수선 일지", new Vector2(940, 1500), out var body, out _);
            var window = (RectTransform)body.parent;

            // 탭 줄 + 초기화 시간 (Tab3 '첫 주'는 첫 주가 진행 중일 때만 맨 앞에 — JournalPopup.LayoutTabs가 자리를 다시 잡는다)
            string[] tabNames = { "오늘", "이번 주", "수선점 기록", "첫 주" };
            var tabButtons = new Object[tabNames.Length];
            var tabImages = new Object[tabNames.Length];
            var tabLabels = new Object[tabNames.Length];
            var tabBadges = new Object[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                var tab = TabButton(body, $"Tab{i}", tabNames[i], out var label);
                ((RectTransform)tab.transform).At(TopLeft, new Vector2(i * 210, 0), new Vector2(200, 72));
                // 누르는 자리 72 → 88 (M22): 위아래로만 8 (옆 탭과는 10 간격 — 옆으로는 넓히지 않는다)
                tab.targetGraphic.HitPad(new Vector2(200, 72), 0f, UISize.MinHit);
                // 탭이 4개면 폭이 150으로 줄어든다 → '수선점 기록'이 들어가게 자동 크기
                label.FitFloor(UIType.Caption, 28f);
                tabButtons[i] = tab;
                tabImages[i] = tab.targetGraphic;
                tabLabels[i] = label;
                // 오늘 · 이번 주 = 받을 수 (숫자), 수선점 기록 · 첫 주 = 받을 것이 있으면 점
                var badge = BuildBadge(tab.transform);
                ((RectTransform)badge.transform).At(TopRight, new Vector2(10, 10), new Vector2(36, 36));
                badge.gameObject.SetActive(false);
                tabBadges[i] = badge;
                if (i == 3) tab.gameObject.SetActive(false);
            }
            var reset = Fit(Txt(body, "ResetText", "초기화까지 0:00:00", 24, UITheme.SubText, TextAlignmentOptions.MidlineRight), 16);
            reset.rectTransform.At(TopRight, new Vector2(0, -8), new Vector2(240, 56));

            // 미션 패널
            var missions = NewRect("MissionsPanel", body).Stretch(0, 0, 90, 0);
            var scoreCard = FramedPanel(missions, "ScoreCard", MetaUi.Alpha(UITheme.PanelLight, 0.75f));
            scoreCard.rectTransform.Top(0, 230);
            var scoreLabel = Txt(scoreCard.transform, "ScoreLabel", "오늘 일지 점수", 26, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true);
            scoreLabel.rectTransform.At(TopLeft, new Vector2(28, -22), new Vector2(260, 40));
            var scoreText = Txt(scoreCard.transform, "ScoreText", "0", 64, UITheme.Cream, TextAlignmentOptions.MidlineRight, true);
            scoreText.rectTransform.At(TopLeft, new Vector2(24, -72), new Vector2(140, 84));
            var scoreMax = Txt(scoreCard.transform, "ScoreMax", "/ 100", 28, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true);
            scoreMax.rectTransform.At(TopLeft, new Vector2(172, -96), new Vector2(120, 44));

            // 막대 300~780 (카드 864): 100점 상자(가운데 = 막대 끝, 폭 84 · 이름표 98)가 카드 안쪽 테두리보다 16px 넘게 안에 든다
            // (예전 316 + 510이면 끝 상자 오른쪽 반 · '70' 이름표가 카드 밖으로 잘렸다)
            var bar = Bar(scoreCard.transform, "Bar", 22, UITheme.Lavender, out var barFill);
            bar.rectTransform.At(TopLeft, new Vector2(300, -150), new Vector2(480, 22));
            var chestRoot = NewRect("Chests", bar.transform).Stretch();
            var chestTemplate = BuildChestTemplate(chestRoot);

            var listRoot = NewRect("List", missions).Stretch(0, 0, 246, 128);
            var listContent = ScrollList(listRoot, out var scroll);
            AddVerticalList(listContent, 14, new RectOffset(4, 4, 4, 16));
            var rowTemplate = BuildMissionRowTemplate(listContent);
            var empty = Txt(listRoot, "EmptyText", "지금은 할 일이 없어요", 28, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.gameObject.SetActive(false);

            var summary = Fit(Txt(body, "SummaryText", "오늘 얻을 수 있는 점수 0점", 22, UITheme.SubText, TextAlignmentOptions.MidlineLeft), UIType.AutoSizeFloor);
            summary.rectTransform.At(BottomLeft, new Vector2(4, 28), new Vector2(430, 54));
            // [모두 받기] 380×96 (M51 — Body 바로 아래 자식, 금, 개수 배지. 받을 것이 없으면 JournalPopup이 숨긴다 · 이름에 '(N)'을 붙이지 않는다)
            var claimAll = ClaimAllButton(body, 380, 96, 32, out var claimAllLabel, out var claimAllBadge);
            ((RectTransform)claimAll.transform).At(BottomRight, Vector2.zero, new Vector2(380, 96));

            // 수선점 기록 (Q-04, SPEC4 §6.1): 요약 카드 Top(0, 150) · [진행 중][완료] Top(166, 64) · 기록 줄 목록 Stretch(0, 0, 246, 128)
            var records = NewRect("RecordsPanel", body).Stretch(0, 0, 90, 0);
            var recordsParts = BuildRecordsPanel(records);
            records.gameObject.SetActive(false);

            // 첫 주 (NEW-01, SPEC4 §13.6): 머리 줄 · 점수 카드 + 상자 · 날 칩 7개 · 과제 줄 목록
            var starter = NewRect("StarterPanel", body).Stretch(0, 0, 90, 0);
            var starterView = BuildStarterPanel(starter, chestTemplate, rowTemplate);
            starter.gameObject.SetActive(false);

            // 연출 층
            var sparkles = BuildSparkles(window, "Sparkles", 16, 44);
            var floatLayer = NewRect("FloatLayer", window).Stretch();
            var pointsFloat = Txt(floatLayer, "PointsFloat", "+20", 44, UITheme.Gold, TextAlignmentOptions.Center, true);
            pointsFloat.rectTransform.At(Center, Vector2.zero, new Vector2(200, 60));
            UiArt.Outline(pointsFloat);
            pointsFloat.gameObject.SetActive(false);

            new Binder(popup)
                .SetArray("_tabButtons", tabButtons)
                .SetArray("_tabImages", tabImages)
                .SetArray("_tabLabels", tabLabels)
                .SetArray("_tabBadges", tabBadges)
                .Set("_resetText", reset)
                .Set("_missionsPanel", missions.gameObject)
                .Set("_scoreLabel", scoreLabel)
                .Set("_scoreText", scoreText)
                .Set("_scoreMaxText", scoreMax)
                .Set("_bar", bar.rectTransform)
                .Set("_barFill", barFill)
                .Set("_chestRoot", chestRoot)
                .Set("_chestTemplate", chestTemplate)
                .Set("_scroll", scroll)
                .Set("_listContent", listContent)
                .Set("_rowTemplate", rowTemplate)
                .Set("_emptyText", empty)
                .Set("_claimAllButton", claimAll)
                .Set("_claimAllLabel", claimAllLabel)
                .Set("_claimAllBadge", claimAllBadge)
                .Set("_summaryText", summary)
                .Set("_recordsPanel", records.gameObject)
                .Set("_recordsCountText", recordsParts.Count)
                .Set("_recordsBarFill", recordsParts.BarFill)
                .Set("_recordsClaimableText", recordsParts.Claimable)
                .Set("_recordsSegments", recordsParts.Segments)
                .Set("_recordsScroll", recordsParts.Scroll)
                .Set("_recordsListContent", recordsParts.ListContent)
                .Set("_recordsEmptyText", recordsParts.Empty)
                .Set("_hiddenRecordIcon", UiArt.Medal(UiArt.MedalKind.Hidden))
                .Set("_recordsPlaceholder", recordsParts.Placeholder)
                .Set("_starterView", starterView)
                .Set("_floatLayer", floatLayer)
                .Set("_pointsFloat", pointsFloat)
                .Set("_sparkles", sparkles)
                .Apply();
            new Binder(starterView).Set("_sparkles", sparkles).Apply();
            return popup;
        }

        /// <summary>
        /// 점수 상자 (막대 위 제자리, 84 폭: 위에 상자, 막대 아래 점수). 루트 = 버튼 + 누름 크기(PressScale, M61).
        /// 상자 위 보상 이름표 'Reward' (루트 자식 — 들썩이는 Visual 밖, At(TopCenter, (0, 24), (98, 30)), 가로 묶음 간격 4):
        /// 'RewardIcon' 24 · 'RewardAmount' 20 굵게 Cream (자동 16) · 'RewardMore' '+1' 18 SubText — 점수 카드(판) 위라 외곽선 없음 (M81).
        /// 색은 MissionChestView가 상태마다 입힌다 (M60A)
        /// </summary>
        static MissionChestView BuildChestTemplate(RectTransform chestRoot)
        {
            var root = NewRect("Chest", chestRoot).At(MiddleLeft, Vector2.zero, new Vector2(84, 196));
            root.pivot = Center;
            var view = root.gameObject.AddComponent<MissionChestView>();
            var button = root.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<PressScale>();

            var visual = NewRect("Visual", root).At(TopCenter, Vector2.zero, new Vector2(84, 84));
            var glow = Img(visual, "Glow", MetaUi.Alpha(UITheme.Lamp, 0.6f), _icons.Glow);
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(160, 160));
            glow.enabled = false;
            var hit = Img(visual, "Hit", Color.clear, null, true);
            hit.rectTransform.Stretch(-8, -8, -8, -8);
            button.targetGraphic = hit;
            bool chestArt = _icons.ChestClosed != null;
            var chest = Img(visual, "ChestImage", chestArt ? Color.white : UITheme.Gold, chestArt ? _icons.ChestClosed : RoundSprite);
            chest.preserveAspect = chestArt;
            chest.rectTransform.At(BottomCenter, new Vector2(0, chestArt ? 0 : 4), chestArt ? new Vector2(84, 80) : new Vector2(76, 58));
            var lid = Img(visual, "Lid", UITheme.Cream, RoundSprite);
            lid.rectTransform.At(BottomCenter, new Vector2(0, 54), new Vector2(84, 24));
            lid.rectTransform.pivot = new Vector2(0.08f, 0f);
            lid.rectTransform.anchoredPosition = new Vector2(-84 * 0.42f, 54);
            Img(lid.transform, "Clasp", UITheme.GoldAccent, CircleSprite).rectTransform.At(BottomCenter, new Vector2(0, -8), new Vector2(18, 18));
            lid.gameObject.SetActive(_icons.ChestClosed == null);

            Image check;
            if (_icons.Check != null)
            {
                check = Img(visual, "Check", Color.white, _icons.Check);
                check.preserveAspect = true;
                check.rectTransform.At(BottomRight, new Vector2(12, -8), new Vector2(40, 40));
            }
            else
            {
                check = Img(visual, "Check", UITheme.Positive, CircleSprite);
                check.rectTransform.At(BottomRight, new Vector2(10, -6), new Vector2(34, 34));
                CheckMark(check.rectTransform, 34);
            }
            check.gameObject.SetActive(false);
            // 받을 수 있음 점 = 공용 배지 점 모드 (§3.13.10 — 점 지름 UISize.BadgeDot, 중심은 상자 오른쪽 위 모서리에서 (−4, −4))
            var dot = BuildBadge(visual);
            dot.name = "Dot";
            float dotCorner = UISize.BadgeDot * 0.5f - 4f;
            ((RectTransform)dot.transform).At(TopRight, new Vector2(dotCorner, dotCorner), new Vector2(UISize.BadgeDot, UISize.BadgeDot));
            dot.gameObject.SetActive(false);

            var points = Txt(root, "PointsText", "20", 24, UITheme.SubText, TextAlignmentOptions.Center, true);
            points.rectTransform.At(BottomCenter, new Vector2(0, 50), new Vector2(84, 34));

            // 상자 위 보상 이름표 (M60A — 무엇을 받는지 상자 위에서 바로 읽힌다)
            var reward = NewRect("Reward", root).At(TopCenter, new Vector2(0, 24), new Vector2(98, 30));
            var rewardRow = reward.gameObject.AddComponent<HorizontalLayoutGroup>();
            rewardRow.spacing = 4;
            rewardRow.childAlignment = TextAnchor.MiddleCenter;
            rewardRow.childControlWidth = true;
            rewardRow.childControlHeight = true;
            rewardRow.childForceExpandWidth = false;
            rewardRow.childForceExpandHeight = false;
            var rewardArt = _icons != null ? First(_icons.RewardGeneric, _icons.Star) : null;
            var rewardIcon = Img(reward, "RewardIcon", Color.white, rewardArt);
            rewardIcon.preserveAspect = true;
            rewardIcon.enabled = rewardArt != null;
            var rewardIconSize = rewardIcon.gameObject.AddComponent<LayoutElement>();
            rewardIconSize.minWidth = rewardIconSize.preferredWidth = 24;
            rewardIconSize.minHeight = rewardIconSize.preferredHeight = 24;
            var rewardAmount = Txt(reward, "RewardAmount", "50", 20, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .FitFloor(UIType.AutoSizeFloor);
            rewardAmount.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var rewardMore = Txt(reward, "RewardMore", "+1", 18, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true);
            rewardMore.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            rewardMore.gameObject.SetActive(false);

            new Binder(view)
                .Set("_icons", _icons)
                .Set("_button", button)
                .Set("_visual", visual)
                .Set("_chest", chest)
                .Set("_placeholderLid", lid)
                .Set("_glow", glow)
                .Set("_pointsText", points)
                .Set("_check", check.gameObject)
                .Set("_dot", dot.gameObject)
                .Set("_rewardRoot", reward.gameObject)
                .Set("_rewardIcon", rewardIcon)
                .Set("_rewardAmount", rewardAmount)
                .Set("_rewardMore", rewardMore)
                .Apply();
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>체크 표시 (글리프 대신 막대 두 개)</summary>
        static void CheckMark(RectTransform parent, float size)
        {
            var shortBar = Img(parent, "Short", Color.white);
            shortBar.rectTransform.At(Center, new Vector2(-size * 0.14f, -size * 0.04f), new Vector2(size * 0.32f, size * 0.12f));
            shortBar.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var longBar = Img(parent, "Long", Color.white);
            longBar.rectTransform.At(Center, new Vector2(size * 0.08f, size * 0.04f), new Vector2(size * 0.52f, size * 0.12f));
            longBar.rectTransform.localRotation = Quaternion.Euler(0, 0, 50);
        }

        static MissionRowView BuildMissionRowTemplate(RectTransform listContent)
        {
            var root = NewRect("MissionRow", listContent);
            root.sizeDelta = new Vector2(860, 150);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<MissionRowView>();

            var background = Sliced(root, "Background", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.PanelLight, 0.92f));
            background.rectTransform.Stretch();
            // 받을 수 있으면 금빛 테두리(흰 바느질 테두리를 금색으로 물들임)가 숨 쉰다 (뷰가 알파를 흔든다)
            var edge = Sliced(root, "GoldEdge", "ui_slot_frame_9s", 2f, UITheme.Gold);
            edge.color = UITheme.Gold;
            edge.rectTransform.Stretch(-2, -2, -2, -2);
            edge.enabled = false;

            var iconBack = Plate(root, "IconBack", MetaUi.Alpha(UITheme.Background, 0.6f));
            iconBack.rectTransform.At(MiddleLeft, new Vector2(18, 0), new Vector2(108, 108));
            var icon = Img(iconBack.transform, "Icon", Color.white, _icons.Journal);
            icon.preserveAspect = true;
            icon.rectTransform.At(Center, Vector2.zero, new Vector2(76, 76));

            var text = Fit(Txt(root, "Text", "잔향이 30번 부르기", 30, UITheme.Text, TextAlignmentOptions.MidlineLeft, true), 20).Ellipsis();
            text.rectTransform.At(TopLeft, new Vector2(140, -18), new Vector2(440, 48));
            var progressBack = Bar(root, "ProgressBar", 20, UITheme.Lavender, out var progressFill);
            progressBack.rectTransform.At(TopLeft, new Vector2(140, -92), new Vector2(320, 20));
            var progressText = Fit(Txt(root, "ProgressText", "0/30", 22, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true), UIType.AutoSizeFloor);
            progressText.rectTransform.At(TopLeft, new Vector2(472, -84), new Vector2(120, 36));

            var pill = GoldTag(root, "PointsPill", 40, UITheme.Lamp, 3.5f);
            pill.rectTransform.At(TopRight, new Vector2(-206, -22), new Vector2(96, 40));
            var points = Fit(Txt(pill.transform, "Text", "+10점", 22, UITheme.Ink, TextAlignmentOptions.Center, true), UIType.AutoSizeFloor);
            points.rectTransform.Stretch(4, 4, 0, 0);

            var chip = BuildRewardChip(root, "RewardChip", new Vector2(96, 70), 40, 18, false, false);
            ((RectTransform)chip.transform).At(TopRight, new Vector2(-206, -70), new Vector2(96, 70));
            chip.gameObject.SetActive(false);

            // [받기]/[가기]/[완료]는 버튼 그림을 바꿔 끼운다 (색으로 덮지 않음). 비활성 그림이 상태를 보여 주므로 비활성 어둡게 하기는 끈다
            var claimSprite = ArtUI("button_gold_9s");
            var goSprite = ArtUI("button_primary_9s");
            var doneSprite = ArtUI("ui_button_disabled_9s");
            var button = Btn(root, "ActionButton", "가기 ›", UITheme.PrimaryButton, 30, out var buttonLabel);
            ((RectTransform)button.transform).At(MiddleRight, new Vector2(-18, 0), new Vector2(170, 96));
            Reskin((Image)button.targetGraphic, "button_primary_9s", 2f);
            if (doneSprite != null)
            {
                var colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
            }

            var stamp = NewRect("Stamp", root).At(Center, new Vector2(150, 0), new Vector2(132, 132));
            stamp.localRotation = Quaternion.Euler(0, 0, -12);
            var stampArt = _icons.StampRing != null ? _icons.StampRing : _icons.Ring;
            var stampRing = Img(stamp, "Ring", stampArt != null ? Color.white : MetaUi.Alpha(UITheme.DustyPink, 0.35f), stampArt != null ? stampArt : CircleSprite);
            stampRing.preserveAspect = stampArt != null;
            stampRing.rectTransform.Stretch();
            if (_icons.StampRing == null && _icons.Ring != null) stampRing.color = MetaUi.Alpha(UITheme.DustyPink, 0.9f);
            var stampText = Fit(Txt(stamp, "Text", "완료", 30, new Color(0.8f, 0.28f, 0.45f, 0.95f), TextAlignmentOptions.Center, true), 18);
            stampText.rectTransform.At(Center, Vector2.zero, new Vector2(76, 46));
            stamp.gameObject.SetActive(false);

            // 4차 줄 부품 (SPEC4 §6.1 · §13.6 — JournalRowModel로 묶을 때만 켜진다): 기록 단계 점 6개 (14, 간격 18, 막대 아래) ·
            // 숨은 기록 힌트 (Hint 20 Lavender, 제목 아래) · 잠김 알약 (자물쇠 24 + '2-3', 보상 칩 자리)
            var tierPips = new Object[MissionRowTierPips];
            for (int i = 0; i < tierPips.Length; i++)
            {
                var pip = Img(root, $"TierPip{i}", MetaUi.Alpha(UITheme.SubText, 0.5f), UiArt.PipButton);
                pip.preserveAspect = true;
                pip.rectTransform.At(TopLeft, new Vector2(140 + i * 18, -122), new Vector2(14, 14));
                pip.gameObject.SetActive(false);
                tierPips[i] = pip;
            }
            var hint = Txt(root, "HintText", string.Empty, UIType.Hint, UITheme.Lavender, TextAlignmentOptions.MidlineLeft).FitFloor(UIType.HintMin).Ellipsis();
            hint.rectTransform.At(TopLeft, new Vector2(140, -60), new Vector2(440, 28));
            hint.gameObject.SetActive(false);
            var lockPill = UiArt.Panel(root, "LockPill", UiArt.PillNight9s, UiArt.CapsulePpu(40), MetaUi.Alpha(Color.white, 0.94f),
                MetaUi.Alpha(UITheme.Background, 0.86f));
            lockPill.rectTransform.At(TopRight, new Vector2(-206, -70), new Vector2(96, 40));
            var lockIcon = Img(lockPill.transform, "Lock", Color.white, _icons.Lock);
            lockIcon.preserveAspect = true;
            lockIcon.rectTransform.At(MiddleLeft, new Vector2(8, 0), new Vector2(24, 24));
            var lockText = Txt(lockPill.transform, "Text", "1-1", UIType.Hint, UITheme.Cream, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            lockText.rectTransform.Stretch(34, 6, 0, 0);
            lockPill.gameObject.SetActive(false);

            new Binder(view)
                .Set("_icons", _icons)
                .Set("_group", group)
                .Set("_background", background)
                .Set("_goldEdge", edge)
                .Set("_icon", icon)
                .Set("_iconBack", iconBack)
                .Set("_text", text)
                .Set("_progressFill", progressFill)
                .Set("_progressText", progressText)
                .Set("_pointsPill", pill.rectTransform)
                .Set("_pointsText", points)
                .Set("_rewardChip", chip)
                .Set("_button", button)
                .Set("_buttonImage", (Image)button.targetGraphic)
                .Set("_buttonLabel", buttonLabel)
                .Set("_claimSprite", claimSprite)
                .Set("_goSprite", goSprite)
                .Set("_doneSprite", doneSprite)
                .Set("_stamp", stamp)
                .SetArray("_tierPips", tierPips)
                .Set("_hintText", hint)
                .Set("_lockPill", lockPill.gameObject)
                .Set("_lockText", lockText)
                .Apply();
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>일지 줄 틀의 기록 단계 점 수 (가장 긴 기록 줄의 단계 수보다 크거나 같게)</summary>
        const int MissionRowTierPips = 6;

        // ═════════════════════════ 출석부 ═════════════════════════

        static AttendancePopup BuildAttendancePopup(SceneBuildContext ctx)
        {
            var windowSize = new Vector2(980, 1300);
            var popup = BuildPopup<AttendancePopup>(ctx.PopupRoot, "AttendancePopup", "수선점 출석부", windowSize, out var body, out _);
            var window = (RectTransform)body.parent;
            const float bodyTop = 136f;
            const float bodyBottom = 30f;
            const float panelsTop = 304f;
            const float panelsBottom = 170f;
            const float starterGridTop = 320f;
            const float starterGridHeight = 196f;
            body.Stretch(24, 24, bodyTop, bodyBottom);

            // 머리: 짝짝이가 도장을 찍는 그림 (아트가 없으면 초상화 + 도장)
            var header = Card(body, "Header");
            header.rectTransform.Top(0, 200);
            var headerArt = ArtLib.UI("attendance_header");
            if (headerArt != null)
            {
                var art = Img(header.transform, "Art", Color.white, headerArt);
                art.preserveAspect = true;
                art.rectTransform.At(MiddleLeft, new Vector2(10, 0), new Vector2(240, 190));
            }
            else
            {
                var portraitArt = ArtLib.Character("jjak_base");
                var portrait = Img(header.transform, "Portrait", portraitArt != null ? Color.white : UITheme.Lavender, portraitArt != null ? portraitArt : CircleSprite);
                portrait.preserveAspect = true;
                portrait.rectTransform.At(MiddleLeft, new Vector2(18, 0), new Vector2(176, 176));
                var stampBadge = BuildStampGraphic(header.rectTransform, "Stamp", 76);
                stampBadge.At(MiddleLeft, new Vector2(150, -44), new Vector2(76, 76));
                stampBadge.localRotation = Quaternion.Euler(0, 0, -16);
            }
            var headerTitle = Fit(Txt(header.transform, "Title", "매일 도장을 찍으면 선물을 받아요", 32, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true), 22);
            headerTitle.rectTransform.At(TopLeft, new Vector2(256, -40), new Vector2(640, 52));
            var headerSub = Txt(header.transform, "Sub", "하루 빠져도 이어서 찍혀요", 23, UITheme.InkSub, TextAlignmentOptions.TopLeft, false, true);
            headerSub.rectTransform.At(TopLeft, new Vector2(256, -100), new Vector2(640, 70));

            // 탭
            var starterTab = TabButton(body, "StarterTab", "새싹 출석", out var starterLabel);
            ((RectTransform)starterTab.transform).At(TopLeft, new Vector2(0, -216), new Vector2(300, 72));
            var monthlyTab = TabButton(body, "MonthlyTab", "정규 출석", out var monthlyLabel);
            ((RectTransform)monthlyTab.transform).At(TopLeft, new Vector2(312, -216), new Vector2(300, 72));
            // 누르는 자리 72 → 88 (M22): 위아래로만 8 (머리 카드 아래 200 · 판 위 304 사이 — 겹치지 않는다)
            starterTab.targetGraphic.HitPad(new Vector2(300, 72), 0f, UISize.MinHit);
            monthlyTab.targetGraphic.HitPad(new Vector2(300, 72), 0f, UISize.MinHit);

            var shake = NewRect("ShakeRoot", body).Stretch(0, 0, panelsTop, panelsBottom);
            var panels = NewRect("Panels", shake).Stretch();

            // 새싹: 7일째 큰 보상 + 7칸
            var starter = NewRect("StarterPanel", panels).Stretch();
            var hero = Sliced(starter, "Hero", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.PanelLight, 0.8f));
            hero.rectTransform.Top(0, 300);
            // 빛살은 금테 안쪽에서만 돈다 (테두리는 빛살 위에 덧씌움)
            var heroMask = NewRect("Mask", hero.transform).Stretch(8, 8, 8, 8);
            heroMask.gameObject.AddComponent<RectMask2D>();
            var rays = Img(heroMask, "Rays", MetaUi.Alpha(UITheme.Lamp, 0.22f), RaysArt());
            rays.rectTransform.At(MiddleLeft, new Vector2(-88, 0), new Vector2(520, 520));
            rays.gameObject.AddComponent<MetaSpin>();
            var heroGlow = Img(heroMask, "Glow", MetaUi.Alpha(UITheme.Gold, 0.5f), _icons.Glow);
            heroGlow.rectTransform.At(MiddleLeft, new Vector2(-8, 0), new Vector2(320, 320));
            FrameOverlay(hero.transform, 2.2f);
            var heroPortrait = Img(hero.transform, "Portrait", Color.white, _icons.Star);
            heroPortrait.preserveAspect = true;
            heroPortrait.rectTransform.At(MiddleLeft, new Vector2(30, 0), new Vector2(250, 250));
            var heroTitle = Fit(Txt(hero.transform, "Title", "7일째 루즈리 확정!", 40, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true), 24);
            heroTitle.rectTransform.At(TopLeft, new Vector2(300, -78), new Vector2(600, 60));
            var heroSub = Txt(hero.transform, "Sub", "0/7일", 25, UITheme.Text, TextAlignmentOptions.TopLeft, false, true);
            heroSub.rectTransform.At(TopLeft, new Vector2(300, -150), new Vector2(600, 90));
            var starterGrid = NewRect("Grid", starter).Top(starterGridTop, starterGridHeight);
            AddGrid(starterGrid, new Vector2(128, 184), new Vector2(6, 6), 7, new RectOffset(0, 0, 4, 4));
            var starterTemplate = BuildAttendanceCellTemplate(starterGrid, 184f, 64f);

            // 정규: 28칸
            var monthly = NewRect("MonthlyPanel", panels).Stretch();
            var monthlyGrid = NewRect("Grid", monthly).Top(0, 640);
            AddGrid(monthlyGrid, new Vector2(128, 150), new Vector2(6, 6), 7, new RectOffset(0, 0, 4, 4));
            var monthlyTemplate = BuildAttendanceCellTemplate(monthlyGrid, 150f, 52f);
            monthly.gameObject.SetActive(false);

            // 안내 줄: 찍을 수 있으면 숨김, 찍었으면 '내일 또 만나요 · 다음 도장까지 7:12:03' (고정폭 시계)
            var status = Fit(Txt(body, "StatusText", "내일 또 만나요 · 다음 도장까지 0:00:00", 23, UITheme.SubText, TextAlignmentOptions.Center), UIType.AutoSizeFloor);
            status.rectTransform.Bottom(124, 40);
            status.gameObject.SetActive(false);
            var checkIn = Btn(body, "CheckInButton", "도장 찍기", UITheme.Gold, 36, out var checkInLabel);
            Skin(checkIn, "button_gold_9s");
            checkInLabel.color = UITheme.Ink;
            ((RectTransform)checkIn.transform).At(BottomCenter, Vector2.zero, new Vector2(480, 110));

            BuildAttendanceDayCard(window, out var dayCard, out var dayCatcher, out var dayTitle, out var dayState, out var dayRow, out var dayChip);
            var sparkles = BuildSparkles(window, "Sparkles", 20, 40);

            new Binder(popup)
                .Set("_icons", _icons)
                .Set("_headerTitle", headerTitle)
                .Set("_headerSub", headerSub)
                .Set("_starterTab", starterTab)
                .Set("_monthlyTab", monthlyTab)
                .Set("_starterTabImage", (Image)starterTab.targetGraphic)
                .Set("_monthlyTabImage", (Image)monthlyTab.targetGraphic)
                .Set("_starterTabLabel", starterLabel)
                .Set("_monthlyTabLabel", monthlyLabel)
                .Set("_starterPanel", starter.gameObject)
                .Set("_heroPortrait", heroPortrait)
                .Set("_heroTitle", heroTitle)
                .Set("_heroSub", heroSub)
                .Set("_starterGrid", starterGrid)
                .Set("_starterTemplate", starterTemplate)
                .Set("_monthlyPanel", monthly.gameObject)
                .Set("_monthlyGrid", monthlyGrid)
                .Set("_monthlyTemplate", monthlyTemplate)
                .Set("_checkInButton", checkIn)
                .Set("_checkInLabel", checkInLabel)
                .Set("_checkInGoldSprite", UiArt.IsArt(UiArt.ButtonGold9s) ? UiArt.ButtonGold9s : null)
                .Set("_checkInConfirmSprite", UiArt.IsArt(UiArt.ButtonPrimary9s) ? UiArt.ButtonPrimary9s : null)
                .Set("_statusText", status)
                .Set("_sparkles", sparkles)
                .Set("_shakeRoot", panels)
                .Set("_dayCard", dayCard)
                .Set("_dayCardCatcher", dayCatcher)
                .Set("_dayCardTitle", dayTitle)
                .Set("_dayCardState", dayState)
                .Set("_dayCardRow", dayRow)
                .Set("_dayCardChip", dayChip)
                .Apply();
            // 새싹 판(위 320 + 칸 196)은 정규 28칸 판 자리보다 짧다 — 그만큼 새싹을 볼 때 창을 줄여 [도장 찍기] 위 빈자리를 없앤다 (M69 칸 크기 그대로)
            float panelsHeight = windowSize.y - bodyTop - bodyBottom - panelsTop - panelsBottom;
            var attendanceSo = new SerializedObject(popup);
            attendanceSo.FindProperty("_starterTrim").floatValue = Mathf.Max(0f, panelsHeight - (starterGridTop + starterGridHeight));
            attendanceSo.ApplyModifiedPropertiesWithoutUndo();
            return popup;
        }

        /// <summary>
        /// 출석부 'N일차 보상' 카드 (M59 — 다른 날 칸을 누르면 토스트 대신, 꺼 둔다): 창 전체 투명 'Catcher'(누르면 닫힘) + 가운데 'Card' 560×300
        /// (보랏빛 안쪽 판 + 금테) — 'Title' 30 굵게 Cream · 'Row' 보상 칸 96×96 (보상 칸 버튼 — 누르면 재료 정보 · 재화 안내) · 'State' 22.
        /// 창 위 맨 앞 (반짝이 층 바로 아래)에 둔다
        /// </summary>
        static void BuildAttendanceDayCard(RectTransform window, out GameObject root, out Button catcher, out TextMeshProUGUI title,
            out TextMeshProUGUI state, out RectTransform row, out MetaRewardChip chip)
        {
            var layer = NewRect("DayCard", window).Stretch();
            var hit = Img(layer, "Catcher", Color.clear, null, true);
            hit.rectTransform.Stretch();
            catcher = hit.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.navigation = new Navigation { mode = Navigation.Mode.None };
            var card = FramedPanel(layer, "Card", MetaUi.Alpha(UITheme.Panel, 0.97f), 2.2f);
            card.rectTransform.At(Center, new Vector2(0, -60), new Vector2(560, 300));
            title = Txt(card.transform, "Title", "3일차 보상", UIType.Title + 2f, UITheme.Cream, TextAlignmentOptions.Center, true);
            title.rectTransform.Top(22, 44, 24, 24);
            row = NewRect("Row", card.transform).Top(84, 110, 20, 20);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = UISpace.GridGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            chip = BuildRewardChip(row, "Reward", new Vector2(96, 96), 52, 20, false, true);
            chip.gameObject.SetActive(false);
            state = Txt(card.transform, "State", "이날 도장을 찍으면 받아요", UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center).FitFloor(UIType.HintMin);
            state.rectTransform.Top(214, 40, 24, 24);
            root = layer.gameObject;
            root.SetActive(false);
        }

        /// <summary>
        /// 출석부 한 칸 128×cellHeight (M69: 새싹 184 · 정규 150) — 크림 카드 + 'DayText' Top(6, 28) 22 굵게 + 보상 'Reward'(MetaRewardChip:
        /// 'Tile' At(TopCenter, (0, −36), (96, 높이 − 76)) 안 'Icon' iconSize, 판 아래 'Amount' Bottom(6, 30, 4, 4) 22 굵게 Ink 맞춤 16 — 칸 안에 다 들어간다,
        /// 판 오른쪽 위 'More' 22px 라벤더 'i') + 강조 칸 금 알약 'Ribbon' Top(−8, 32, −6, −6) '7일 확정!' 20 굵게 Ink + 오늘 빛 + 판 가운데 도장.
        /// 칸 루트가 버튼 + PressScale (M61). 내용 묶음은 누름을 받지 않는다 (칸 버튼이 받는다)
        /// </summary>
        static AttendanceCellView BuildAttendanceCellTemplate(RectTransform grid, float cellHeight, float iconSize)
        {
            var root = NewRect("Cell", grid);
            root.sizeDelta = new Vector2(128, cellHeight);
            var view = root.gameObject.AddComponent<AttendanceCellView>();
            var button = root.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<PressScale>();

            var visual = NewRect("Visual", root).Stretch();
            var glow = Img(visual, "TodayGlow", MetaUi.Alpha(UITheme.Lamp, 0.8f), _icons.Glow);
            glow.rectTransform.Stretch(-26, -26, -26, -26);
            var pulse = glow.gameObject.AddComponent<MetaPulse>();
            new Binder(pulse).Set("_graphic", glow).Apply();
            glow.gameObject.SetActive(false);
            // 크림 양피지 칸 + 그 위 바느질 테두리 (강조 칸은 금색, 오늘 칸은 등불색으로 물든다 — 평소엔 투명)
            Image frame, background;
            var parchment = ArtUI("panel_parchment_9s");
            var slotFrame = ArtUI("ui_slot_frame_9s");
            if (parchment != null && slotFrame != null)
            {
                // 출석 칸 그림 (ART-UPGRADE §4.3 #4): 전용 ui_attendance_cell_9s, 없으면 지금 양피지 그대로 (UiArt.AttendanceCell9s의 대신 그림)
                background = UiArt.Panel(visual, "Background", UiArt.AttendanceCell9s, 2.4f, Color.white, UITheme.Parchment, true);
                background.rectTransform.Stretch();
                frame = Sliced(visual, "Frame", "ui_slot_frame_9s", 2.4f, Color.white);
                frame.color = MetaUi.Alpha(Color.white, 0f);
                frame.rectTransform.Stretch(-2, -2, -2, -2);
            }
            else
            {
                frame = Img(visual, "Frame", new Color(0.55f, 0.47f, 0.72f, 1f), RoundSprite);
                frame.rectTransform.Stretch();
                background = Img(visual, "Background", new Color(0.97f, 0.93f, 0.85f, 1f), RoundSprite, true);
                background.rectTransform.Stretch(4, 4, 4, 4);
            }
            button.targetGraphic = background;

            var content = NewRect("Content", visual).Stretch();
            var contentGroup = content.gameObject.AddComponent<CanvasGroup>();
            contentGroup.blocksRaycasts = false;
            var day = Txt(content, "DayText", "1일", UIType.Caption, UITheme.Ink, TextAlignmentOptions.Center, true);
            day.rectTransform.Top(6, 28);

            // 보상: 그림 판 + 판 아래 양 (MetaRewardChip — 판은 등급 색을 물들이지 않는다)
            float tileHeight = cellHeight - 76f;
            var rewardRoot = NewRect("Reward", content).Stretch();
            var chip = rewardRoot.gameObject.AddComponent<MetaRewardChip>();
            var tile = Img(rewardRoot, "Tile", UITheme.ParchmentDark, RoundSprite);
            tile.rectTransform.At(TopCenter, new Vector2(0, -36), new Vector2(96, tileHeight));
            var placeholderIcon = First(_icons.RewardGeneric, _icons.Star);
            var icon = Img(tile.transform, "Icon", Color.white, placeholderIcon);
            icon.preserveAspect = true;
            icon.enabled = placeholderIcon != null;
            icon.rectTransform.At(Center, Vector2.zero, new Vector2(iconSize, iconSize));
            var more = Img(tile.transform, "More", UITheme.Lavender, CircleSprite);
            more.rectTransform.At(TopRight, new Vector2(6, 6), new Vector2(22, 22));
            Txt(more.transform, "Label", "i", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            more.gameObject.SetActive(false);
            var amount = Txt(rewardRoot, "Amount", UIFormat.TimesSmall + "1", UIType.Caption, UITheme.Ink, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor);
            amount.rectTransform.Bottom(6, 30, 4, 4);
            new Binder(chip)
                .Set("_icons", _icons)
                .Set("_icon", icon)
                .Set("_amountText", amount)
                .Apply();

            // 강조 칸: 날짜 줄 자리에 금 알약 '7일 확정!' (칸 위로 8 · 좌우로 6 비어져 나온다)
            var ribbon = UiArt.Panel(content, "Ribbon", UiArt.PillWhite9s, UiArt.CapsulePpu(32), UITheme.Gold, UITheme.Gold);
            ribbon.raycastTarget = false;
            ribbon.rectTransform.Top(-8, 32, -6, -6);
            var ribbonText = Txt(ribbon.transform, "Text", "7일 확정!", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            ribbonText.rectTransform.Stretch(8, 8, 0, 0);
            ribbon.gameObject.SetActive(false);

            // 도장은 그림 판 가운데에 떨어진다 (칸 가운데보다 2px 위)
            float tileCenter = cellHeight * 0.5f - (36f + tileHeight * 0.5f);
            var ink = Img(visual, "Ink", MetaUi.Alpha(UITheme.DustyPink, 0.7f), _icons.Glow);
            ink.rectTransform.At(Center, new Vector2(0, tileCenter), new Vector2(130, 130));
            ink.enabled = false;
            var stamp = BuildStampGraphic(visual, "Stamp", 92);
            stamp.At(Center, new Vector2(0, tileCenter), new Vector2(92, 92));
            stamp.localRotation = Quaternion.Euler(0, 0, -14);
            stamp.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            stamp.gameObject.SetActive(false);

            new Binder(view)
                .Set("_button", button)
                .Set("_visual", visual)
                .Set("_background", background)
                .Set("_cellSprite", parchment != null && slotFrame != null ? UiArt.AttendanceCell9s : null)
                .Set("_goldCellSprite", parchment != null && slotFrame != null && UiArt.HasAttendanceCellGold9s ? UiArt.AttendanceCellGold9s : null)
                .Set("_frame", frame)
                .Set("_dayText", day)
                .Set("_reward", chip)
                .Set("_ribbon", ribbon.gameObject)
                .Set("_ribbonText", ribbonText)
                .Set("_todayGlow", glow.gameObject)
                .Set("_stamp", stamp)
                .Set("_ink", ink)
                .Set("_contentGroup", contentGroup)
                .Set("_moreMark", more.gameObject)
                .Apply();
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>단추 모양 도장 (아트가 없으면 분홍 원 + 안쪽 고리 + 단추 구멍 4개)</summary>
        static RectTransform BuildStampGraphic(RectTransform parent, string name, float size)
        {
            var root = NewRect(name, parent).At(Center, Vector2.zero, new Vector2(size, size));
            if (_icons.Stamp != null)
            {
                var art = Img(root, "Art", Color.white, _icons.Stamp);
                art.preserveAspect = true;
                art.rectTransform.Stretch();
                return root;
            }
            var outer = Img(root, "Outer", MetaUi.Alpha(new Color(0.85f, 0.4f, 0.55f), 0.88f), CircleSprite);
            outer.rectTransform.Stretch();
            var inner = Img(root, "Inner", MetaUi.Alpha(new Color(0.72f, 0.28f, 0.44f), 0.9f), _icons.Ring != null ? _icons.Ring : CircleSprite);
            inner.rectTransform.Stretch(size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);
            float hole = size * 0.14f;
            float offset = size * 0.13f;
            foreach (var position in new[] { new Vector2(-offset, offset), new Vector2(offset, offset), new Vector2(-offset, -offset), new Vector2(offset, -offset) })
            {
                Img(root, "Hole", MetaUi.Alpha(UITheme.Cream, 0.9f), CircleSprite).rectTransform.At(Center, position, new Vector2(hole, hole));
            }
            return root;
        }

        // ═════════════════════════ 수선 바구니 ═════════════════════════

        /// <summary>
        /// 수선 바구니 팝업 920×1320 (Popups/IdleChestPopup — 코치 경로 Window/Body/ClaimButton · Window/Body/QuickSection/QuickButton 그대로).
        /// 격자 · 각주 · 빠른 수선 판 · 창 높이는 뷰가 줄 수에 맞춰 옮긴다 (M32 칸 리듬 — 한 줄이면 약 1108). 빠른 수선 버튼은 Primary(보라),
        /// 무료일 때 왼쪽 위 금 'FreeRibbon' 76×30 '무료' — 이 화면의 금은 [받기] 하나 (M26).
        /// </summary>
        static IdleChestPopup BuildIdleChestPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<IdleChestPopup>(ctx.PopupRoot, "IdleChestPopup", "수선 바구니", new Vector2(920, 1320), out var body, out _);
            var window = (RectTransform)body.parent;

            // 머리: 바구니 + 쌓인 시간 / 상한
            var header = FramedPanel(body, "Header", MetaUi.Alpha(UITheme.PanelLight, 0.78f), 2.2f);
            header.rectTransform.Top(0, 300);
            var basketRoot = NewRect("BasketRoot", header.transform).At(MiddleLeft, new Vector2(14, 0), new Vector2(260, 260));
            var basketGlow = Img(basketRoot, "Glow", MetaUi.Alpha(UITheme.Lamp, 0.3f), _icons.Glow);
            basketGlow.rectTransform.At(Center, Vector2.zero, new Vector2(340, 340));
            var basket = Img(basketRoot, "Basket", Color.white, _icons.Basket(0));
            basket.preserveAspect = true;
            basket.rectTransform.Stretch(10, 10, 10, 10);
            basket.enabled = _icons.Basket(0) != null;
            var placeholder = BuildBasketPlaceholder(basketRoot, new Vector2(0, -20), 1.75f, out var fills);
            placeholder.gameObject.SetActive(_icons.Basket(0) == null);

            var elapsed = Fit(Txt(header.transform, "ElapsedText", "0분", 50, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true), 30);
            elapsed.rectTransform.At(TopLeft, new Vector2(290, -24), new Vector2(530, 66));
            var cap = Txt(header.transform, "CapText", "8시간 중", 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            cap.rectTransform.At(TopLeft, new Vector2(292, -92), new Vector2(520, 36));
            var capBar = Bar(header.transform, "CapBar", 24, UITheme.Lavender, out var capFill);
            capBar.rectTransform.At(TopLeft, new Vector2(292, -136), new Vector2(520, 24));
            var status = Txt(header.transform, "StatusText", "자리를 비워도 친구들이 선물을 모아 와요", 22, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            status.rectTransform.At(TopLeft, new Vector2(292, -172), new Vector2(530, 64));
            var bonus = Fit(Txt(header.transform, "BonusText", "다음 스테이지를 클리어하면 시간당 +2%", 22, UITheme.Positive, TextAlignmentOptions.MidlineLeft), UIType.AutoSizeFloor);
            bonus.rectTransform.At(TopLeft, new Vector2(292, -244), new Vector2(530, 36));

            // 줄: 담긴 양 + 시간당
            Txt(body, "LinesLabel", "담긴 선물 · 시간당", 26, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(316, 40);
            var grid = NewRect("Grid", body).Top(364, 356);
            AddGrid(grid, new Vector2(128, 170), new Vector2(16, 16), 6, new RectOffset(0, 0, 0, 0));
            var chipTemplate = BuildRewardChip(grid, "Line", new Vector2(128, 170), 84, 26, false, true, true);
            chipTemplate.gameObject.SetActive(false);
            var empty = Txt(body, "EmptyText", "아직 담긴 선물이 없어요", 26, UITheme.SubText);
            empty.rectTransform.Top(500, 60);
            empty.gameObject.SetActive(false);
            var footnote = Fit(Txt(body, "Footnote", "<size=70%>*</size> 단추는 앱을 꺼 둔 동안만 쌓여요 (켜 둔 동안은 바로 들어와요)", UIType.Hint, UITheme.SubText,
                TextAlignmentOptions.MidlineLeft), UIType.AutoSizeFloor);
            footnote.rectTransform.Top(726, 34);

            // 빠른 수선
            var quick = FramedPanel(body, "QuickSection", MetaUi.Alpha(UITheme.PanelLight, 0.78f), 2.2f);
            quick.rectTransform.Top(772, 204);
            Txt(quick.transform, "Title", "빠른 수선", 30, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.At(TopLeft, new Vector2(24, -16), new Vector2(300, 44));
            var preview = Txt(quick.transform, "PreviewText", "바구니 2시간치 선물을 바로 받을 수 있어요", 21, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            preview.rectTransform.At(TopLeft, new Vector2(24, -66), new Vector2(470, 124));
            preview.overflowMode = TextOverflowModes.Ellipsis;

            var quickButton = Btn(quick.transform, "QuickButton", string.Empty, UITheme.PrimaryButton, 28, out var quickTitle);
            Skin(quickButton, "button_primary_9s");
            ((RectTransform)quickButton.transform).At(TopRight, new Vector2(-20, -16), new Vector2(300, 104));
            quickTitle.text = "빠른 수선";
            quickTitle.rectTransform.Top(10, 44, 8, 8);
            var costRow = NewRect("Cost", quickButton.transform).Top(54, 40, 8, 8);
            var costLayout = costRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            costLayout.childAlignment = TextAnchor.MiddleCenter;
            costLayout.spacing = 8;
            costLayout.childControlWidth = true;
            costLayout.childControlHeight = true;
            costLayout.childForceExpandWidth = false;
            costLayout.childForceExpandHeight = false;
            var costIcon = CurrencyIcon(costRow, CurrencyType.Gem, 30);
            // '오늘 3회 남음'(무료 — '무료'는 리본 하나만) / '[별조각] 50 · 오늘 2회 남음' / '오늘 모두 썼어요' (가운데 정렬 줄 — 가장 긴 줄도 24px로 284 안에 들어간다)
            var cost = Txt(costRow, "CostText", "오늘 3회 남음", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            // 무료 리본 (버튼 왼쪽 위로 비어져 나온다 — 금 알약 '무료' Ink 20 굵게)
            var freeRibbon = UiArt.Panel(quickButton.transform, "FreeRibbon", UiArt.PillWhite9s, UiArt.CapsulePpu(30), UITheme.Gold, UITheme.Gold);
            freeRibbon.raycastTarget = false;
            freeRibbon.rectTransform.At(TopLeft, new Vector2(-8, 8), new Vector2(76, 30));
            Txt(freeRibbon.transform, "Label", "무료", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch(4, 4, 0, 0);
            freeRibbon.gameObject.SetActive(false);
            var ticket = Btn(quick.transform, "TicketButton", "빠른 수선권 사용 (0장)", UITheme.Secondary, 22, out var ticketLabel);
            Reskin((Image)ticket.targetGraphic, "ui_button_secondary_9s", 2.8f);
            ((RectTransform)ticket.transform).At(TopRight, new Vector2(-20, -128), new Vector2(300, 60));
            // 누르는 자리 60 → 88 (M22): 위는 빠른 수선 버튼(아래 끝 −120)과의 간격 8의 절반 4만, 나머지 24는 아래로
            // (판 아래 받기 버튼까지는 24 간격 — 닿지 않는다). 보이는 자리는 그대로
            ticket.targetGraphic.raycastPadding = new Vector4(0f, -(UISize.MinHit - 60f - 4f), 0f, -4f);
            var quickLocked = NewRect("Locked", quick.transform).At(TopRight, new Vector2(-20, -16), new Vector2(300, 172));
            var lockedBack = Sliced(quickLocked, "Back", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.Background, 0.7f));
            lockedBack.rectTransform.Stretch();
            var lockedSlot = NewRect("LockSlot", quickLocked).At(TopCenter, new Vector2(0, -22), new Vector2(48, 48));
            LockIcon(lockedSlot, 44);
            var lockedText = Txt(quickLocked, "Text", "스테이지 1-5를 클리어하면 열려요", 21, UITheme.SubText, TextAlignmentOptions.Top, false, true);
            lockedText.rectTransform.Stretch(14, 14, 80, 8);
            quickLocked.gameObject.SetActive(false);

            var claim = Btn(body, "ClaimButton", "받기", UITheme.Gold, 40, out var claimLabel);
            Skin(claim, "button_gold_9s");
            claimLabel.color = UITheme.Ink;
            ((RectTransform)claim.transform).At(BottomCenter, Vector2.zero, new Vector2(560, 116));

            var sparkles = BuildSparkles(window, "Sparkles", 18, 44);

            new Binder(popup)
                .Set("_icons", _icons)
                .Set("_basketRoot", basketRoot)
                .Set("_basket", basket)
                .Set("_basketPlaceholder", placeholder.gameObject)
                .SetArray("_placeholderFill", fills)
                .Set("_basketGlow", basketGlow)
                .Set("_elapsedText", elapsed)
                .Set("_capText", cap)
                .Set("_capFill", capFill)
                .Set("_statusText", status)
                .Set("_bonusText", bonus)
                .Set("_grid", grid)
                .Set("_chipTemplate", chipTemplate)
                .Set("_footnote", footnote)
                .Set("_emptyText", empty)
                .Set("_quickSection", quick.rectTransform)
                .Set("_quickPreviewText", preview)
                .Set("_quickButton", quickButton)
                .Set("_quickTitle", quickTitle)
                .Set("_quickCost", cost)
                .Set("_quickCostIcon", costIcon)
                .Set("_freeRibbon", freeRibbon.gameObject)
                .Set("_ticketButton", ticket)
                .Set("_ticketLabel", ticketLabel)
                .Set("_quickLocked", quickLocked.gameObject)
                .Set("_quickLockedText", lockedText)
                .Set("_claimButton", claim)
                .Set("_claimLabel", claimLabel)
                .Set("_sparkles", sparkles)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 복귀 화면 ═════════════════════════

        static ReturnPopup BuildReturnPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<ReturnPopup>(ctx.PopupRoot, "ReturnPopup", string.Empty, new Vector2(920, 1240), out var body, out var title);
            var window = (RectTransform)body.parent;
            var divider = window.Find("Divider");
            if (divider != null) divider.gameObject.SetActive(false);
            title.gameObject.SetActive(false);
            body.Stretch(36, 36, 36, 36);

            // 머리 그림 880×320 (UI/return_header, 없으면 밤의 수선점 자리표시)
            // 그림이 있으면 바탕은 투명 (그림이 덮고 금테가 모서리를 둥글게 감싼다), 없으면 밤색 둥근 판 + 자리표시
            var headerArt = ArtUI("return_header");
            var header = Img(window, "Header", headerArt != null ? Color.clear : UITheme.Stage, headerArt != null ? null : RoundSprite);
            header.rectTransform.At(TopCenter, new Vector2(0, -20), new Vector2(880, 320));
            header.transform.SetSiblingIndex(1);
            float headerInset = headerArt != null ? 7f : 3f;
            var headerMask = NewRect("Mask", header.transform).Stretch(headerInset, headerInset, headerInset, headerInset);
            headerMask.gameObject.AddComponent<RectMask2D>();
            if (headerArt != null)
            {
                var art = Img(headerMask, "Art", Color.white, headerArt);
                art.rectTransform.Stretch();
                // 어떤 화면비에서도 늘어나지 않게 그림 비율 그대로 칸을 덮는다 (넘치는 쪽은 Mask가 자른다)
                var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = headerArt.rect.width / headerArt.rect.height;
                FrameOverlay(header.transform, 2f);
            }
            else
            {
                BuildReturnHeaderPlaceholder(headerMask);
            }
            var close = window.Find("CloseButton");
            if (close != null) close.SetAsLastSibling();

            var heading = Txt(window, "TitleText", "다녀오셨어요!", 58, UITheme.Cream, TextAlignmentOptions.Center, true);
            heading.rectTransform.At(TopCenter, new Vector2(0, -350), new Vector2(840, 76));
            var away = Fit(Txt(window, "AwayText", "6시간 12분 동안 친구들이 수선점을 지켰어요", 28, UITheme.Text, TextAlignmentOptions.Center), 18);
            away.rectTransform.At(TopCenter, new Vector2(0, -428), new Vector2(840, 44));
            var cap = Txt(window, "CapText", "바구니 6시간 12분 / 8시간", UIType.Label, UITheme.SubText, TextAlignmentOptions.Center).FitFloor(UIType.Caption);
            cap.rectTransform.At(TopCenter, new Vector2(0, -482), new Vector2(700, 34));
            var capBar = Bar(window, "CapBar", 22, UITheme.Lavender, out var capFill);
            capBar.rectTransform.At(TopCenter, new Vector2(0, -520), new Vector2(700, 22));
            var full = Txt(window, "FullText", "바구니가 가득 찼어요!", 22, UITheme.Gold, TextAlignmentOptions.Center, false, true);
            full.rectTransform.At(TopCenter, new Vector2(0, -550), new Vector2(840, 58));
            full.gameObject.SetActive(false);

            var grid = NewRect("Grid", window).At(TopCenter, new Vector2(0, -614), new Vector2(848, 356));
            var gridLayout = AddGrid(grid, new Vector2(128, 170), new Vector2(16, 16), 6, new RectOffset(0, 0, 0, 0));
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var chipTemplate = BuildRewardChip(grid, "Reward", new Vector2(128, 170), 84, 26, true, false, true);
            chipTemplate.gameObject.SetActive(false);
            var empty = Txt(window, "EmptyText", "바구니가 비어 있어요", 26, UITheme.SubText);
            empty.rectTransform.At(TopCenter, new Vector2(0, -740), new Vector2(700, 60));
            empty.gameObject.SetActive(false);

            // 계보 한 줄
            var lineRoot = NewRect("LineageLine", window).At(TopCenter, new Vector2(0, -976), new Vector2(840, 82));
            var portraitBack = Plate(lineRoot, "PortraitBack", MetaUi.Alpha(UITheme.PanelLight, 0.9f));
            portraitBack.rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(84, 84));
            var samplePortrait = ArtLib.Character("jjak_base");
            var portrait = Img(portraitBack.transform, "Portrait", Color.white, samplePortrait);
            portrait.preserveAspect = true;
            portrait.enabled = samplePortrait != null;
            portrait.rectTransform.Stretch(9, 9, 7, 9);
            var bubble = Card(lineRoot, "Bubble");
            bubble.rectTransform.Stretch(96, 0, 0, 0);
            // 이름 앞 계보 색 점 10 + 이름 22 굵게 (색 = Lerp(계보 색, Ink, 0.55) — 크림 위 4.5:1, 뷰가 정한다)
            var lineDot = Img(bubble.transform, "NameDot", UITheme.Lavender, CircleSprite);
            lineDot.rectTransform.At(TopLeft, new Vector2(22, -15), new Vector2(10, 10));
            var lineName = Txt(bubble.transform, "Name", "짝짝이", UIType.Caption, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true);
            lineName.rectTransform.At(TopLeft, new Vector2(38, -5), new Vector2(400, 30));
            var lineText = Fit(Txt(bubble.transform, "Text", "문 앞 양말 친구들을 모두 가족이라 불렀어요", 23, UITheme.Ink, TextAlignmentOptions.MidlineLeft, false, true), UIType.AutoSizeFloor);
            lineText.rectTransform.Stretch(22, 16, 34, 4);

            // 버튼 (M31 · M26): [그냥 받기] Secondary 320×110 (왼쪽, 34 Cream) + [두 배로 받기] 금 500×120 (오른쪽 — 한 화면에 금은 하나).
            // 두 배가 안 되면 뷰가 두 배 버튼 · 안내 줄을 숨기고 [받기]를 가운데 금 520×120 · 40 Ink로 바꾼다 (그림 두 장을 바꿔 끼운다)
            var claim = SceneParts.Button(window, "ClaimButton", "그냥 받기", SceneParts.ButtonStyle.Secondary, 34, out var claimLabel);
            ((RectTransform)claim.transform).At(BottomLeft, new Vector2(40, 64), new Vector2(320, 110));
            claimLabel.color = UITheme.Cream;
            claimLabel.FitFloor(24f);
            var doubled = SceneParts.Button(window, "DoubleButton", "두 배로 받기", SceneParts.ButtonStyle.Gold, UIType.H1, out var doubleLabel);
            ((RectTransform)doubled.transform).At(BottomRight, new Vector2(-40, 64), new Vector2(500, 120));
            var doubleImage = (Image)doubled.targetGraphic;
            if (doubleImage.type == Image.Type.Sliced && UiArt.IsArt(doubleImage.sprite)) doubleImage.pixelsPerUnitMultiplier = UiArt.ButtonPpu(120);
            doubleLabel.rectTransform.Top(10, 60, 8, 8);
            doubleLabel.FitFloor(30f);
            var doubleSub = Txt(doubled.transform, "SubLabel", "하루 한 번", UIType.Caption, MetaUi.Alpha(UITheme.Ink, 0.8f), TextAlignmentOptions.Center)
                .FitFloor(UIType.AutoSizeFloor);
            doubleSub.rectTransform.Top(70, 34, 8, 8);
            // 오른쪽 위 빨간 알약 96×36 '무료' / '×2' (흰 굵은 글자 + 배지 외곽선, §1.2)
            var doubleBadge = UiArt.Panel(doubled.transform, "DoubleBadge", UiArt.PillWhite9s, UiArt.CapsulePpu(36), UITheme.RedDot, UITheme.RedDot);
            doubleBadge.raycastTarget = false;
            doubleBadge.rectTransform.At(TopRight, new Vector2(10, 14), new Vector2(96, 36));
            var doubleBadgeText = Txt(doubleBadge.transform, "Label", "무료", UIType.Hint, Color.white, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor);
            if (BadgeOutlineMaterial != null) doubleBadgeText.fontSharedMaterial = BadgeOutlineMaterial;
            doubleBadgeText.rectTransform.Stretch(6, 6, 0, 0);
            var note = Txt(window, "NoteText", "두 배는 단추와 재료에만 적용돼요 (별조각 제외)", UIType.Hint, UITheme.SubText, TextAlignmentOptions.Center)
                .FitFloor(UIType.AutoSizeFloor);
            note.rectTransform.At(BottomCenter, new Vector2(0, 24), new Vector2(840, 32));
            var claimGold = UiArt.ButtonGold9s;
            var claimSecondary = UiArt.ButtonSecondary9s;

            var sparkles = BuildSparkles(window, "Sparkles", 24, 40);

            new Binder(popup)
                .Set("_icons", _icons)
                .Set("_titleText", heading)
                .Set("_awayText", away)
                .Set("_capText", cap)
                .Set("_capFill", capFill)
                .Set("_fullText", full)
                .Set("_grid", grid)
                .Set("_chipTemplate", chipTemplate)
                .Set("_emptyText", empty)
                .Set("_lineRoot", lineRoot.gameObject)
                .Set("_linePortrait", portrait)
                .Set("_linePortraitBack", portraitBack)
                .Set("_lineName", lineName)
                .Set("_lineText", lineText)
                .Set("_lineDot", lineDot)
                .Set("_claimButton", claim)
                .Set("_claimLabel", claimLabel)
                .Set("_doubleButton", doubled)
                .Set("_doubleLabel", doubleLabel)
                .Set("_doubleSubLabel", doubleSub)
                .Set("_doubleBadge", doubleBadge.gameObject)
                .Set("_doubleBadgeText", doubleBadgeText)
                .Set("_noteText", note)
                .Set("_claimGoldSprite", UiArt.IsArt(claimGold) ? claimGold : null)
                .Set("_claimSecondarySprite", UiArt.IsArt(claimSecondary) ? claimSecondary : null)
                .Set("_sparkles", sparkles)
                .Apply();
            return popup;
        }

        /// <summary>복귀 화면 머리 자리표시: 밤하늘 + 달 + 수선점 그림 + 바구니 + 반딧불</summary>
        static void BuildReturnHeaderPlaceholder(RectTransform parent)
        {
            var sky = ArtLib.UI("bg_night");
            var back = Img(parent, "Sky", sky != null ? Color.white : UITheme.Stage, sky);
            back.rectTransform.Stretch();
            if (sky != null) back.rectTransform.At(Center, new Vector2(0, 120), new Vector2(880, 1564));
            Img(parent, "Shade", MetaUi.Alpha(UITheme.Background, 0.35f)).rectTransform.Stretch();
            var moonGlow = Img(parent, "MoonGlow", MetaUi.Alpha(UITheme.Cream, 0.35f), _icons.Glow);
            moonGlow.rectTransform.At(TopRight, new Vector2(-40, 30), new Vector2(240, 240));
            var moon = Img(parent, "Moon", _icons.Moon != null ? Color.white : UITheme.Cream, _icons.Moon != null ? _icons.Moon : CircleSprite);
            moon.preserveAspect = _icons.Moon != null;
            moon.rectTransform.At(TopRight, new Vector2(-100, -30), _icons.Moon != null ? new Vector2(110, 110) : new Vector2(84, 84));
            var lampGlow = Img(parent, "LampGlow", MetaUi.Alpha(UITheme.Lamp, 0.5f), _icons.Glow);
            lampGlow.rectTransform.At(BottomCenter, new Vector2(0, -60), new Vector2(560, 360));
            var shop = ArtLib.Battle("shop");
            if (shop != null)
            {
                var shopImage = Img(parent, "Shop", Color.white, shop);
                shopImage.preserveAspect = true;
                shopImage.rectTransform.At(BottomCenter, new Vector2(0, -36), new Vector2(330, 330));
            }
            var basketArt = _icons.Basket(2);
            if (basketArt != null)
            {
                var basket = Img(parent, "Basket", Color.white, basketArt);
                basket.preserveAspect = true;
                basket.rectTransform.At(BottomCenter, new Vector2(210, 12), new Vector2(150, 140));
            }
            else
            {
                BuildBasketPlaceholder(parent, Vector2.zero, 0.9f, out var fills).At(BottomCenter, new Vector2(210, 14), new Vector2(122, 112));
                foreach (var fill in fills) ((Image)fill).enabled = true;
            }
            var spark = First(ArtFx("fx_mote_firefly"), _icons.Sparkle);
            var positions = new[] { new Vector2(-330, 110), new Vector2(-250, 60), new Vector2(300, 140), new Vector2(360, 70), new Vector2(-150, 160) };
            for (int i = 0; i < positions.Length; i++)
            {
                var firefly = Img(parent, "Firefly", MetaUi.Alpha(UITheme.Lamp, 0.85f), spark);
                firefly.rectTransform.At(Center, positions[i], Vector2.one * (18 + 6 * (i % 3)));
                var pulse = firefly.gameObject.AddComponent<MetaPulse>();
                new Binder(pulse).Set("_graphic", firefly).Apply();
            }
        }

        // ═════════════════════════ 기능 개방 카드 ═════════════════════════

        static FeatureUnlockCard BuildFeatureUnlockCard(SceneBuildContext ctx)
        {
            // 카드 높이 680은 그림 띠가 없을 때. 넓은 그림(뽑기·던전·원정 등)이 오면 뷰가 띠 높이만큼 카드를 늘리고 글을 내린다
            var popup = BuildPopup<FeatureUnlockCard>(ctx.PopupRoot, "FeatureUnlockCard", "새 기능이 열렸어요!", new Vector2(840, 680), out var body, out var title);
            var window = (RectTransform)body.parent;

            var art = NewRect("Art", body).At(TopCenter, new Vector2(0, -4), new Vector2(200, 200));
            var rays = Img(art, "Rays", MetaUi.Alpha(UITheme.Lamp, 0.3f), RaysArt());
            rays.rectTransform.At(Center, Vector2.zero, new Vector2(380, 380));
            rays.gameObject.AddComponent<MetaSpin>();
            var glow = Img(art, "Glow", MetaUi.Alpha(UITheme.Gold, 0.55f), _icons.Glow);
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(320, 320));
            // 어두운 메달 받침 한 장 (그림 그대로, 색을 입히지 않는다). 없으면 예전 원
            var medallion = _icons.Medallion;
            var back = Img(art, "IllustrationBack", medallion != null ? Color.white : MetaUi.Alpha(UITheme.PanelLight, 0.95f), medallion != null ? medallion : CircleSprite);
            back.preserveAspect = medallion != null;
            back.rectTransform.At(Center, Vector2.zero, medallion != null ? new Vector2(196, 196) : new Vector2(178, 178));
            var illustration = Img(art, "Illustration", Color.white, _icons.Star);
            illustration.preserveAspect = true;
            illustration.rectTransform.At(Center, Vector2.zero, medallion != null ? new Vector2(116, 116) : new Vector2(140, 140));

            // 넓은 그림 띠 640×240 (둥근 금테 + 안쪽 RectMask2D, 그림은 비율 그대로 칸을 덮는다)
            var bannerRoot = NewRect("Banner", body).At(TopCenter, new Vector2(0, -214), new Vector2(640, 240));
            var bannerBack = Sliced(bannerRoot, "Back", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.PanelLight, 0.9f));
            bannerBack.rectTransform.Stretch();
            var bannerMask = NewRect("Mask", bannerRoot).Stretch(7, 7, 7, 7);
            bannerMask.gameObject.AddComponent<RectMask2D>();
            var banner = Img(bannerMask, "Art", Color.white, null);
            banner.rectTransform.Stretch();
            var bannerFitter = banner.gameObject.AddComponent<AspectRatioFitter>();
            bannerFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bannerFitter.aspectRatio = 640f / 240f;
            FrameOverlay(bannerRoot, 2.2f);
            bannerRoot.gameObject.SetActive(false);

            var name = Fit(Txt(body, "NameText", "수선 바구니", 42, UITheme.Gold, TextAlignmentOptions.Center, true), 26);
            name.rectTransform.At(TopCenter, new Vector2(0, -212), new Vector2(760, 58));
            var text = Txt(body, "BodyText", "자리를 비워도 바구니에 선물이 쌓여요", 28, UITheme.Text, TextAlignmentOptions.Top, false, true);
            text.rectTransform.At(TopCenter, new Vector2(0, -280), new Vector2(740, 120));

            // [나중에] Secondary · [가 보기 ›] Primary — 이동 버튼은 보라, 이 카드에는 받기(금)가 없다 (M26 · M76)
            var row = NewRect("Buttons", body).Bottom(0, 100);
            AddRow(row, 24);
            var later = SceneParts.Button(row, "LaterButton", "나중에", SceneParts.ButtonStyle.Secondary, 32, out var laterLabel);
            var go = SceneParts.Button(row, "GoButton", "가 보기 ›", SceneParts.ButtonStyle.Primary, 32, out var goLabel);

            var sparkles = BuildSparkles(window, "Sparkles", 18, 44);

            new Binder(popup)
                .Set("_icons", _icons)
                .Set("_titleText", title)
                .Set("_art", art)
                .Set("_illustration", illustration)
                .Set("_illustrationBack", back)
                .Set("_banner", banner)
                .Set("_bannerRoot", bannerRoot)
                .Set("_bannerFitter", bannerFitter)
                .Set("_nameText", name)
                .Set("_bodyText", text)
                .Set("_goButton", go)
                .Set("_goLabel", goLabel)
                .Set("_laterButton", later)
                .Set("_laterLabel", laterLabel)
                .Set("_sparkles", sparkles)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 강해지기 시트 (M13 2단계) ═════════════════════════

        /// <summary>
        /// 강해지기 시트 Popups/StrengthenPopup 860×760 (UX-SPEC §2.1.5 — 계약 C-12, ctx.Popups에 넣는 쪽은 BuildPopups).
        /// Body(788×588): 머리 판 Top(0, 150) — 'HeaderText' 30 Cream 굵게 (권장 · 내 · -차이, 리치 텍스트) + 평가 칩 120×40 (오른쪽 위) +
        /// 비율 막대 RatioBar 20 + 안내 'HintText' 22 SubText. 그 아래 'Rows'(세로 줄 묶음, 간격 16)에 Row0~2 788×120:
        /// 받침 88 + 아이콘 64 · 'Title' 28 Cream 굵게 · 'Desc' 22 SubText 두 줄 · 'GoButton' '가기 ›' Primary 160×64 (오른쪽).
        /// 줄 그림: 0 = 수선점 강화(강화 탭 그림), 1 = 친구 초상(실행 중에), 2 = 편성 그림. 'EmptyText'는 꺼 둔다.
        /// 4차 (PRG-03, SPEC4 §13.2): 창 860×980, 머리 Top(0, 236)에 CheerButton '친구들의 응원 받기' Gold 300×72 (28, −148) + CheerSubText Hint 20 SubText
        /// (344, −148, 두 줄), 줄은 StrengthenPopup.MaxRows(4)개 788×110 (아이콘 받침 72 · 아이콘 56 · 'Title' 28 · 'Desc' 22 · '가기 ›' 160×64).
        /// </summary>
        static StrengthenPopup BuildStrengthenPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<StrengthenPopup>(ctx.PopupRoot, "StrengthenPopup", "강해지기", new Vector2(860, 980), out var body, out _);

            const float headerHeight = 236f;
            var header = FramedPanel(body, "Header", MetaUi.Alpha(UITheme.PanelLight, 0.78f), 2.2f);
            header.rectTransform.Top(0, headerHeight);
            var headerText = Txt(header.transform, "HeaderText", "권장 0  /  내 0", UIType.Title + 2f, UITheme.Cream, TextAlignmentOptions.MidlineLeft)
                .FitFloor(22f);
            headerText.rectTransform.At(TopLeft, new Vector2(28, -16), new Vector2(586, 50));
            var chip = Img(header.transform, "RatingChip", MetaUi.Alpha(UITheme.Negative, 0.2f), RoundSprite);
            chip.rectTransform.At(TopRight, new Vector2(-24, -20), new Vector2(120, 40));
            var chipText = Txt(chip.transform, "Label", "어려움", UIType.Hint, UITheme.Negative, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            chipText.rectTransform.Stretch(8, 8, 0, 0);
            const float barHeight = 20f;
            var ratioBar = Bar(header.transform, "RatioBar", barHeight, UITheme.Negative, out var ratioFill);
            ratioBar.rectTransform.At(TopLeft, new Vector2(28, -76), new Vector2(732, barHeight));
            var hint = Txt(header.transform, "HintText", "조금만 더 강해지면 훨씬 수월해져요", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.HintMin);
            hint.rectTransform.At(TopLeft, new Vector2(28, -104), new Vector2(732, 34));
            // 친구들의 응원 (§13.2 — 튜토리얼 뒤, 장마다 한 번. 뷰가 상태를 쓴다)
            var cheer = SceneParts.Button(header.transform, "CheerButton", StrengthenPopup.CheerLabel, SceneParts.ButtonStyle.Gold, UIType.ButtonS, out var cheerLabel);
            ((RectTransform)cheer.transform).At(TopLeft, new Vector2(28, -148), new Vector2(300, 72));
            var cheerImage = (Image)cheer.targetGraphic;
            if (cheerImage.type == Image.Type.Sliced && UiArt.IsArt(cheerImage.sprite)) cheerImage.pixelsPerUnitMultiplier = UiArt.ButtonPpu(72);
            cheerImage.HitPad(new Vector2(300, 72), UISize.MinHit, UISize.MinHit);
            cheerLabel.FitFloor(UIType.Hint);
            var cheerSub = Txt(header.transform, "CheerSubText", "10분 동안 공격력 +20% · 이 장에서 한 번", UIType.Hint, UITheme.SubText,
                TextAlignmentOptions.MidlineLeft, false, true).FitFloor(UIType.HintMin);
            cheerSub.rectTransform.At(TopLeft, new Vector2(344, -148), new Vector2(420, 72));
            cheer.gameObject.SetActive(false);
            cheerSub.gameObject.SetActive(false);

            var rowsRoot = NewRect("Rows", body).Stretch(0, 0, headerHeight + 16, 0);
            var layout = rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = UISpace.GridGap;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rows = new Object[StrengthenPopup.MaxRows];
            var icons = new Object[StrengthenPopup.MaxRows];
            var titles = new Object[StrengthenPopup.MaxRows];
            var descs = new Object[StrengthenPopup.MaxRows];
            var buttons = new Object[StrengthenPopup.MaxRows];
            var rowArt = new Sprite[] { _icons.Feature(FeatureId.ShopUpgrade), null, _icons.Feature(FeatureId.Formation), null };
            for (int i = 0; i < StrengthenPopup.MaxRows; i++)
            {
                var row = Sliced(rowsRoot, $"Row{i}", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.PanelLight, 0.92f));
                row.rectTransform.sizeDelta = new Vector2(0, 110);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
                var iconBack = Plate(row.transform, "IconBack", MetaUi.Alpha(UITheme.Background, 0.6f));
                iconBack.rectTransform.At(MiddleLeft, new Vector2(16, 0), new Vector2(72, 72));
                var icon = Img(iconBack.transform, "Icon", Color.white, rowArt[i]);
                icon.preserveAspect = true;
                icon.enabled = rowArt[i] != null;
                icon.rectTransform.At(Center, Vector2.zero, new Vector2(56, 56));
                var title = Txt(row.transform, "Title", "수선점 강화", UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(22f).Ellipsis();
                title.rectTransform.At(TopLeft, new Vector2(104, -12), new Vector2(490, 40));
                var desc = Txt(row.transform, "Desc", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true).FitFloor(UIType.HintMin);
                desc.rectTransform.At(TopLeft, new Vector2(104, -52), new Vector2(490, 52));
                if (i >= StrengthenPopup.RowCount) row.gameObject.SetActive(false);
                var go = SceneParts.Button(row.transform, "GoButton", "가기 ›", SceneParts.ButtonStyle.Primary, UIType.ButtonM, out _);
                ((RectTransform)go.transform).At(MiddleRight, new Vector2(-20, 0), new Vector2(160, 64));
                var goImage = (Image)go.targetGraphic;
                if (goImage.type == Image.Type.Sliced && UiArt.IsArt(goImage.sprite)) goImage.pixelsPerUnitMultiplier = UiArt.ButtonPpu(64);
                goImage.HitPad(new Vector2(160, 64), UISize.HitTarget, UISize.MinHit);
                rows[i] = row.gameObject;
                icons[i] = icon;
                titles[i] = title;
                descs[i] = desc;
                buttons[i] = go;
            }
            var empty = Txt(body, "EmptyText", StrengthenPopup.EmptyText, UIType.Body, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            empty.rectTransform.Stretch(24, 24, headerHeight + 50, 60);
            empty.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_headerText", headerText)
                .Set("_hintText", hint)
                .Set("_ratingChip", chip)
                .Set("_ratingText", chipText)
                .Set("_ratioFill", ratioFill)
                .SetArray("_rows", rows)
                .SetArray("_rowIcons", icons)
                .SetArray("_rowTitles", titles)
                .SetArray("_rowDescs", descs)
                .SetArray("_rowButtons", buttons)
                .Set("_emptyText", empty)
                .Set("_icons", _icons)
                .Set("_cheerButton", cheer)
                .Set("_cheerLabel", cheerLabel)
                .Set("_cheerSubText", cheerSub)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 잠자기 모드 ═════════════════════════

        /// <summary>
        /// 잠자기 모드 (UX-SPEC-IA §4.9 — 지도가 보인다, CR-16): SafeArea/SleepMode/Overlay (Stretch + 위아래 400 넘침, M68 — 노치 · 둥근 모서리 밖까지).
        ///   Dim = Background 알파 0.5 (누름을 받는다 — 누르면 깸) · Frame(= SafeArea 자리) 안에
        ///   TopVeil (넘침 위 끝 ~ y 300 Background 0.92, y 460까지 흐려짐 — 상단 바 · 스테이지 패널 · x1/AUTO · 바구니 대부분) ·
        ///   BottomVeil (아래 넘침 ~ 아래에서 420 불투명, 600까지 흐려짐 — 빠른 아이콘 · 가이드 줄 · 덱 띠 · 탭 바) ·
        ///   상태 카드 At(BottomCenter, (0, 236), (880, 136)) (화면 y 1548~1684, 밤 판 0.92): 달 80 · 제목 '쉿, 친구들이 수선점을 지키고 있어요' ·
        ///   한 줄 (물리친 잊힘 · 모은 단추 · 스테이지, ' · '로 나눔) · 그 아래 Hint '화면을 누르면 깨어나요' (Bottom(196, 30), MetaPulse).
        /// 예전 가운데 수선점 그림 · Zzz는 없다 (진짜 수선점이 보인다 — SleepModeView._shop · _zzz는 비워 둔다). 누름은 Dim만 받는다.
        /// Battle/MetaHud에 CanvasGroup(SleepModeView._hudGroups — 이 뷰만 쓰는 곱하기 묶음)을 달아 자는 동안 빠른 아이콘 · 바구니 · 가이드 줄 · 잠금 덮개가
        /// 덮개와 함께 사라지게 한다 (띠 흐림 사이로 NEW · 빨간 점 · '가득!'이 비치지 않게 — 싸움만 남는다).
        /// </summary>
        static void BuildSleepMode(SceneBuildContext ctx)
        {
            const float bleed = 400f;
            const float topSolid = 300f;
            const float topFadeEnd = 460f;
            const float bottomSolid = 420f;
            const float bottomFadeEnd = 600f;

            var root = NewRect("SleepMode", ctx.SafeArea).Stretch();
            var view = root.gameObject.AddComponent<SleepModeView>();

            var overlay = NewRect("Overlay", root).Stretch(0, 0, -bleed, -bleed);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            var dim = Img(overlay, "Dim", MetaUi.Alpha(UITheme.Background, 0.5f), null, true);
            dim.rectTransform.Stretch();
            var button = overlay.gameObject.AddComponent<Button>();
            button.targetGraphic = dim;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            // 위아래 띠는 SafeArea 기준 (넘침 400까지 이어 칠해 노치 쪽도 같은 어둠)
            var frame = NewRect("Frame", overlay).Stretch(0, 0, bleed, bleed);
            var veil = MetaUi.Alpha(UITheme.Background, 0.92f);
            var topVeil = NewRect("TopVeil", frame).Top(-bleed, bleed + topFadeEnd);
            Img(topVeil, "Solid", veil).rectTransform.Top(0, bleed + topSolid);
            Img(topVeil, "Fade", veil, FadeSprite(true)).rectTransform.Top(bleed + topSolid, topFadeEnd - topSolid);
            var bottomVeil = NewRect("BottomVeil", frame).Bottom(-bleed, bleed + bottomFadeEnd);
            Img(bottomVeil, "Solid", veil).rectTransform.Bottom(0, bleed + bottomSolid);
            Img(bottomVeil, "Fade", veil, FadeSprite(false)).rectTransform.Bottom(bleed + bottomSolid, bottomFadeEnd - bottomSolid);

            // 상태 카드 (아래 띠 안)
            var card = Sliced(frame, "Card", "panel_night_9s", 2f, MetaUi.Alpha(UITheme.Panel, 0.92f));
            if (card.sprite != RoundSprite) card.color = MetaUi.Alpha(Color.white, 0.92f);
            card.rectTransform.At(BottomCenter, new Vector2(0, 236), new Vector2(880, 136));
            // 달 (UI/moon, 없으면 VFX 달빛 → 예전 원)
            var moonArt = _icons.Moon;
            var moon = Img(card.transform, "Moon", moonArt != null ? Color.white : UITheme.Cream, moonArt != null ? moonArt : CircleSprite);
            moon.preserveAspect = moonArt != null;
            moon.rectTransform.At(MiddleLeft, new Vector2(20, 0), moonArt != null ? new Vector2(80, 80) : new Vector2(56, 56));
            var title = Fit(Txt(card.transform, "Title", "쉿, 친구들이 수선점을 지키고 있어요", UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true),
                UIType.Caption);
            title.rectTransform.At(TopLeft, new Vector2(116, -22), new Vector2(740, 40));

            // 숫자 한 줄: 글자 길이만큼 차례로 (가로 줄 — SleepModeView가 1초마다, 바뀔 때만 쓴다)
            var stats = NewRect("Stats", card.transform).At(TopLeft, new Vector2(116, -70), new Vector2(740, 40));
            var row = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.spacing = 10f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            // 나눔 점은 글자 칸 사이 간격(10)으로 띄운다 (앞뒤 빈칸 너비에 기대지 않게)
            var kills = Txt(stats, "KillsText", "물리친 잊힘 0", UIType.Label, UITheme.Text, TextAlignmentOptions.MidlineLeft);
            Txt(stats, "Dot0", "·", UIType.Label, UITheme.SubText, TextAlignmentOptions.Center);
            var gold = Txt(stats, "GoldText", "모은 단추 0", UIType.Label, UITheme.Gold, TextAlignmentOptions.MidlineLeft);
            Txt(stats, "Dot1", "·", UIType.Label, UITheme.SubText, TextAlignmentOptions.Center);
            var stage = Txt(stats, "StageText", "스테이지 1-1", UIType.Label, UITheme.Text, TextAlignmentOptions.MidlineLeft);

            var hint = Txt(frame, "Hint", "화면을 누르면 깨어나요", UIType.Hint, UITheme.SubText, TextAlignmentOptions.Center);
            hint.rectTransform.Bottom(196, 30);
            var hintPulse = hint.gameObject.AddComponent<MetaPulse>();
            new Binder(hintPulse).Set("_graphic", hint).Apply();
            overlay.gameObject.SetActive(false);

            // 자는 동안 함께 사라지는 메타 HUD (BuildHud가 먼저 만든다 — 각 HUD 루트의 SheetFade 묶음과는 따로, 곱해진다)
            var metaHud = ctx.BattleRoot != null ? ctx.BattleRoot.Find("MetaHud") : null;
            CanvasGroup hudGroup = null;
            if (metaHud != null)
            {
                hudGroup = metaHud.GetComponent<CanvasGroup>();
                if (hudGroup == null) hudGroup = metaHud.gameObject.AddComponent<CanvasGroup>();
            }

            new Binder(view)
                .Set("_overlay", overlay.gameObject)
                .Set("_group", group)
                .Set("_wakeButton", button)
                .Set("_killsText", kills)
                .Set("_goldText", gold)
                .Set("_stageText", stage)
                .SetArray("_hudGroups", hudGroup != null ? new Object[] { hudGroup } : Array.Empty<Object>())
                .Apply();
        }

        /// <summary>
        /// 세로 흐림 그림 (흰색 + 알파, 8x128, 부드러운 smoothstep): opaqueTop이면 위가 불투명 → 아래로 사라짐, 아니면 아래가 불투명.
        /// 잠자기 덮개 위아래 띠의 흐려지는 끝 (색은 Image.color로 입힌다)
        /// </summary>
        static Sprite FadeSprite(bool opaqueTop) =>
            Generated(opaqueTop ? "META_FadeDown" : "META_FadeUp", 8, 128, (u, v) =>
            {
                float t = opaqueTop ? v : 1f - v;
                return t * t * (3f - 2f * t);
            });

        /// <summary>
        /// 코드로 만드는 UI 바탕 그림 (Sprites/UI/Meta/{name}.png, 흰색 + 알파 — M84 Generated와 같은 방식). 파일이 있으면 다시 그리지 않는다.
        /// alpha(u, v)는 0~1 (v = 0이 아래 줄). 가져오기 설정: Sprite, 밉맵 없음, 압축 없음, Clamp, border(9-slice, 기본 없음)
        /// </summary>
        static Sprite Generated(string name, int width, int height, Func<float, float, float> alpha, Vector4 border = default)
        {
            string path = $"{GeneratedSpriteDir}/{name}.png";
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
                Directory.CreateDirectory(GeneratedSpriteDir);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency
                                     || importer.spriteBorder != border))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ═════════════════════════ 공용 조각 ═════════════════════════

        /// <summary>
        /// 보상 칸: (frame이면 보랏빛 안쪽 판 + 금테) + 아이콘 + 양 (+ 이름) (+ 작은 줄). 일지 줄·복귀 창처럼 판 위에 바로 놓는 칸은 frame=false.
        /// 아이콘 자리표시는 선물 상자 그림 (MetaIcons.RewardGeneric).
        /// onLight = 밝은 양피지 칸 위 (이벤트 도장 칸 · 다시 만난 날 칸 — 출석 칸 그림): 양은 Ink · 외곽선 없음 · 자동 크기 하한 16
        /// (출석 칸 BuildAttendanceCellTemplate과 같은 규칙), 이름은 InkSub, 작은 줄은 PositiveInk (r1: 크림 + 외곽선 양이 양피지 위에서 거의 안 읽혔다).
        /// </summary>
        internal static MetaRewardChip BuildRewardChip(Transform parent, string name, Vector2 size, float iconSize, float amountSize,
            bool showName, bool frame, bool subLine = false, bool onLight = false)
        {
            var root = NewRect(name, parent);
            root.sizeDelta = size;
            var chip = root.gameObject.AddComponent<MetaRewardChip>();
            // 칸 전체가 누르는 자리 (투명) + 버튼 + 누름 크기 — 누르면 무엇인지 알려 준다 (M59 · M61)
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<PressScale>();
            Image frameImage = null;
            if (frame)
            {
                if (ArtUI("card_frame_9s") != null)
                {
                    // 보랏빛 안쪽 판 + 금테 덧씌움 (테두리에는 보상 등급 색을 옅게 섞는다 — MetaRewardChip)
                    Sliced(root, "Inner", "ui_panel_inset_9s", 2.6f, MetaUi.Alpha(UITheme.Panel, 0.96f)).rectTransform.Stretch(3, 3, 3, 3);
                    frameImage = FrameOverlay(root, Mathf.Min(size.x, size.y) >= 120f ? 2.2f : 2.5f);
                    frameImage.name = "Frame";
                }
                else
                {
                    frameImage = Img(root, "Frame", MetaUi.Alpha(UITheme.GoldAccent, 0.85f), RoundSprite);
                    frameImage.rectTransform.Stretch();
                    Img(root, "Inner", MetaUi.Alpha(UITheme.Panel, 0.96f), RoundSprite).rectTransform.Stretch(3, 3, 3, 3);
                }
            }
            var placeholderIcon = _icons != null ? First(_icons.RewardGeneric, _icons.Star) : null;
            var icon = Img(root, "Icon", Color.white, placeholderIcon);
            icon.enabled = placeholderIcon != null;
            icon.preserveAspect = true;
            float iconTop = frame ? 10 : 2;
            icon.rectTransform.At(TopCenter, new Vector2(0, -iconTop), new Vector2(iconSize, iconSize));
            // 보상 칸은 대개 어두운 판(안쪽 판 · 일지 줄 · 복귀 창) 위에 놓이므로 양은 크림색 + 외곽선. 밝은 양피지 칸(onLight)은 Ink · 외곽선 없음
            TextMeshProUGUI amount = onLight
                ? Txt(root, "Amount", "0", amountSize, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor)
                : UiArt.Outline(Fit(Txt(root, "Amount", "0", amountSize, UITheme.Cream, TextAlignmentOptions.Center, true),
                    Mathf.Max(UIType.AutoSizeFloor, amountSize * 0.6f)));
            float amountTop = iconTop + iconSize + 2;
            amount.rectTransform.Top(amountTop, amountSize * 1.35f, 4, 4);
            TextMeshProUGUI nameText = null;
            if (showName)
            {
                nameText = Fit(Txt(root, "Name", "이름", UIType.Micro, onLight ? UITheme.InkSub : UITheme.SubText, TextAlignmentOptions.Center),
                    UIType.AutoSizeFloor).Ellipsis();
                nameText.rectTransform.Top(amountTop + amountSize * 1.35f, 26, 4, 4);
            }
            TextMeshProUGUI sub = null;
            if (subLine)
            {
                sub = Fit(Txt(root, "Sub", "+0/시간", UIType.Micro, onLight ? UITheme.PositiveInk : UITheme.Positive, TextAlignmentOptions.Center),
                    UIType.AutoSizeFloor);
                // 금테 칸은 아래 테 선 · 네 귀 장식을 피해 안쪽으로 (긴 단추 줄은 16까지 줄어든다), 테 없는 칸은 그대로
                if (frame) sub.rectTransform.Bottom(12, 26, 12, 12);
                else sub.rectTransform.Bottom(6, 26, 4, 4);
                sub.gameObject.SetActive(false);
            }
            new Binder(chip)
                .Set("_icons", _icons)
                .Set("_button", button)
                .Set("_frame", frameImage)
                .Set("_icon", icon)
                .Set("_amountText", amount)
                .Set("_nameText", nameText)
                .Set("_subText", sub)
                .Apply();
            return chip;
        }

        /// <summary>별빛 터뜨리기 층 (부모를 꽉 채움, 맨 위)</summary>
        internal static MetaSparkleBurst BuildSparkles(RectTransform parent, string name, int count, float size)
        {
            var layer = NewRect(name, parent).Stretch();
            var burst = layer.gameObject.AddComponent<MetaSparkleBurst>();
            var particles = new Object[count];
            for (int i = 0; i < count; i++)
            {
                var image = Img(layer, "Spark", UITheme.Lamp, _icons != null ? _icons.Sparkle : First(ArtFx("fx_sparkle"), CircleSprite));
                image.rectTransform.At(Center, Vector2.zero, new Vector2(size, size));
                image.enabled = false;
                particles[i] = image;
            }
            new Binder(burst).SetArray("_particles", particles).Apply();
            return burst;
        }

        /// <summary>크림색 동화책 카드 (9-slice 테두리를 작은 카드에 맞게 줄인다)</summary>
        static Image Card(Transform parent, string name)
        {
            var card = ParchmentCard(parent, name);
            if (card.type == Image.Type.Sliced) card.pixelsPerUnitMultiplier = 1.6f;
            return card;
        }

        /// <summary>
        /// 탭 버튼: 흰 바느질 탭(ui_tab_9s)에 팝업이 고름/안 고름 색을 입힌다. 아트가 없으면 단색 둥근 버튼.
        /// 켜짐 · 꺼짐 전용 탭 그림이 둘 다 있으면(UiArt.HasTabSkin — ART-UPGRADE §4.3 #9) TabSkin을 붙여 TabStyle.Apply(Page)가 색 대신 그림을 바꾼다
        /// (수선 일지 · 출석부 · 도감 탭). 지금은 그림이 없어 붙지 않는다
        /// </summary>
        static Button TabButton(Transform parent, string name, string label, out TextMeshProUGUI labelText)
        {
            var tab = Btn(parent, name, label, UITheme.TabSelected, 28, out labelText);
            var image = (Image)tab.targetGraphic;
            Reskin(image, "ui_tab_9s", 2.8f);
            image.color = UITheme.TabSelected;
            if (UiArt.HasTabSkin)
            {
                var skin = image.gameObject.AddComponent<TabSkin>();
                new Binder(skin).Set("_on", UiArt.TabOn9s).Set("_off", UiArt.TabOff9s).Apply();
            }
            return tab;
        }

        /// <summary>
        /// 버튼 바탕 (M25 1단계 — 본문은 버튼 공장 SceneParts.SkinButton 한 줄): 그림 이름 → 모양 (button_gold_9s = Gold, button_primary_9s = Primary,
        /// ui_button_secondary_9s = Secondary). 아트면 흰색 그대로 + 비활성 그림 바꿔 끼우기(ui_button_disabled_9s), 없으면 그 모양의 단색 둥근 버튼,
        /// 이름 글자는 비활성일 때 알파 0.5 (ButtonLabelDim). 모르는 그림 이름은 그 그림을 그대로 입힌다 (배율 2 = 높이 90~110px)
        /// </summary>
        internal static void Skin(Button button, string artName)
        {
            switch (artName)
            {
                case "button_gold_9s":
                    SceneParts.SkinButton(button, SceneParts.ButtonStyle.Gold);
                    return;
                case "button_primary_9s":
                    SceneParts.SkinButton(button, SceneParts.ButtonStyle.Primary);
                    return;
                case "ui_button_secondary_9s":
                    SceneParts.SkinButton(button, SceneParts.ButtonStyle.Secondary);
                    return;
            }
            var art = ArtLib.UI(artName);
            if (art != null) UiArt.SetButtonArt(button, art, 2f);
        }

        /// <summary>
        /// 자동 크기 minSize ~ 지금 크기 (M23 — 본문은 UIKit.FitFloor: 하한 16, 더 작게 부르면 올리고 경로와 함께 경고).
        /// SettingsSceneBuilder(WP-4)도 부른다 — 서명 그대로 (계약 C-11)
        /// </summary>
        internal static TextMeshProUGUI Fit(TextMeshProUGUI label, float minSize) => label.FitFloor(minSize);

        // ── 설정 팝업용 컨트롤 (SettingsSceneBuilder가 쓴다) ──

        /// <summary>켜기/끄기 스위치 108×52</summary>
        internal static MetaSwitch Switch(Transform parent, string name)
        {
            // 스위치 길 그림 한 쌍 (ART-UPGRADE §4.3 #5): 둘 다 있을 때만 MetaSwitch가 그림을 바꿔 끼운다 (아니면 null — 지금처럼 색으로)
            var root = NewRect(name, parent);
            root.sizeDelta = new Vector2(108, 52);
            var view = root.gameObject.AddComponent<MetaSwitch>();
            // 홈 = 막대 틀(높이 52 → ppu 96/52), 켜지면 안쪽 채움이 보랏빛으로 차오른다. 손잡이는 단추 그림 (흰색 그대로)
            var trackArt = ArtUI("ui_bar_frame_9s");
            var track = trackArt != null ? Img(root, "Track", Color.white, trackArt, true) : Img(root, "Track", UITheme.Primary, RoundSprite, true);
            track.type = Image.Type.Sliced;
            if (trackArt != null) track.pixelsPerUnitMultiplier = UiArt.CapsulePpu(52);
            track.rectTransform.Stretch();
            Image fill = null;
            var fillArt = ArtUI("ui_bar_fill_9s");
            if (trackArt != null && fillArt != null)
            {
                fill = Img(track.transform, "Fill", MetaUi.Alpha(UITheme.Primary, 0f), fillArt);
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = UiArt.CapsulePpu(52 - 16);
                fill.rectTransform.Stretch(8, 8, 8, 8);
            }
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = track;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var knobArt = ArtUI("ui_pip_button");
            var knob = Img(track.transform, "Knob", knobArt != null ? Color.white : UITheme.Cream, knobArt != null ? knobArt : CircleSprite);
            knob.preserveAspect = knobArt != null;
            knob.rectTransform.At(Center, Vector2.zero, knobArt != null ? new Vector2(46, 46) : new Vector2(42, 42));
            new Binder(view).Set("_button", button).Set("_track", track).Set("_fill", fill).Set("_knob", knob.rectTransform)
                .Set("_trackOn", UiArt.HasSwitchSkin ? UiArt.SwitchOn9s : null)
                .Set("_trackOff", UiArt.HasSwitchSkin ? UiArt.SwitchOff9s : null)
                .Apply();
            return view;
        }

        /// <summary>
        /// 금 꼬리표 (받기 · 가득! · 점수 알약 — ART-UPGRADE §4.3 #6): 전용 UiArt.TagGold9s가 있으면 그 그림 (흰색, 배율 CapsulePpu(높이)),
        /// 없으면 지금처럼 button_gold_9s (배율 ppu — 0이면 ButtonPpu(높이)) · 그림도 없으면 fallback 색 둥근 판
        /// </summary>
        static Image GoldTag(Transform parent, string name, float height, Color fallback, float ppu = 0f)
        {
            var tag = UiArt.TagGold9s;
            if (tag != null && tag != UiArt.ButtonGold9s && UiArt.IsArt(tag)) return UiArt.Panel(parent, name, tag, UiArt.CapsulePpu(height), Color.white, fallback);
            return Sliced(parent, name, "button_gold_9s", ppu > 0f ? ppu : UiArt.ButtonPpu(height), fallback);
        }

        /// <summary>
        /// 여러 칸 고르기 버튼 줄 (칸 폭은 똑같이 나눔). M53 고르기 칸(Option): 칸 바탕은 흰색 틴트용 알약(ui_pill_white_9s, 칸 높이 = 줄 높이 − 8 기준 캡슐 배율)에
        /// 꺼 둔 2px Outline — 색 · 테두리 · 굵기는 MetaSegmentedControl이 TabStyle.Apply(Option)로 입힌다 (고른 칸 PrimaryButton + Cream@0.6 테두리, 아닌 칸 Secondary).
        /// 서명은 그대로 (SettingsSceneBuilder가 부른다 — 계약 C-11). 줄 높이는 부르는 쪽이 정한다 (설정 62)
        /// </summary>
        internal static MetaSegmentedControl Segmented(Transform parent, string name, string[] labels, float fontSize = 24)
        {
            const float optionHeight = 62f - 8f;
            var root = NewRect(name, parent);
            var view = root.gameObject.AddComponent<MetaSegmentedControl>();
            var back = Sliced(root, "Back", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.Background, 0.8f));
            back.rectTransform.Stretch();
            var row = NewRect("Row", root).Stretch(4, 4, 4, 4);
            AddRow(row, 4);
            var buttons = new Object[labels.Length];
            var backgrounds = new Object[labels.Length];
            var texts = new Object[labels.Length];
            var pill = UiArt.PillWhite9s;
            for (int i = 0; i < labels.Length; i++)
            {
                var button = Btn(row, $"Option{i}", labels[i], UITheme.Secondary, fontSize, out var label);
                var image = (Image)button.targetGraphic;
                if (pill != null)
                {
                    image.sprite = pill;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = UiArt.CapsulePpu(optionHeight);
                }
                image.color = UITheme.Secondary;
                var edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = MetaUi.Alpha(UITheme.Cream, 0.6f);
                edge.effectDistance = new Vector2(2f, -2f);
                edge.enabled = false;
                label.color = UITheme.SubText;
                Fit(label, Mathf.Max(UIType.AutoSizeFloor, fontSize * 0.7f));
                buttons[i] = button;
                backgrounds[i] = button.targetGraphic;
                texts[i] = label;
            }
            new Binder(view).SetArray("_buttons", buttons).SetArray("_backgrounds", backgrounds).SetArray("_labels", texts).Apply();
            return view;
        }

        /// <summary>0~100 가로 슬라이더 (정수)</summary>
        internal static Slider BuildSlider(Transform parent, string name)
        {
            var root = NewRect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            // 홈(높이 28)과 채움은 막대 아트 (Slider가 채움의 오른쪽 앵커를 옮긴다 — Filled 아님), 손잡이는 40×40 단추 그림
            const float trackHeight = 28f;
            var trackArt = ArtUI("ui_bar_frame_9s");
            var back = trackArt != null ? Img(root, "Background", Color.white, trackArt) : Img(root, "Background", MetaUi.Alpha(UITheme.Background, 0.9f), RoundSprite);
            back.type = Image.Type.Sliced;
            if (trackArt != null) back.pixelsPerUnitMultiplier = UiArt.CapsulePpu(trackHeight);
            back.rectTransform.Stretch(0, 0, 16, 16);
            var fillArea = NewRect("FillArea", root).Stretch(5, 5, 21, 21);
            var fillArt = ArtUI("ui_bar_fill_9s");
            var fill = Img(fillArea, "Fill", UITheme.Lavender, fillArt != null ? fillArt : RoundSprite);
            fill.type = Image.Type.Sliced;
            if (fillArt != null) fill.pixelsPerUnitMultiplier = UiArt.CapsulePpu(trackHeight - 10);
            fill.rectTransform.Stretch();
            var handleArea = NewRect("HandleArea", root).Stretch(20, 20, 10, 10);
            var knobArt = ArtUI("ui_pip_button");
            var handle = Img(handleArea, "Handle", knobArt != null ? Color.white : UITheme.Cream, knobArt != null ? knobArt : CircleSprite, true);
            handle.preserveAspect = true;
            handle.rectTransform.sizeDelta = new Vector2(40, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            slider.value = 70;
            return slider;
        }

        /// <summary>메타 그림 묶음 (SettingsSceneBuilder 등이 같은 에셋을 쓰도록)</summary>
        internal static MetaIcons Icons => _icons != null ? _icons : _icons = PrepareIcons();
    }
}
