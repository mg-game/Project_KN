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
    /// 화면 틀(IA) 빌더: 짧은 상단 바(130)와 ≡ 메뉴 팝업 (UX-SPEC-IA §3.1 · §3.6). IdleSceneBuilder가 부른다.
    /// 시트 머리(BuildSheetHeader)는 틀의 뼈대라 IdleSceneBuilder에 있다 (CR-10). 틀 전용 아트는 UiArt.TopBar9s(out dedicated) 등 (없으면 기존 아트).
    /// 좌표는 1080x1920 기준, 상단 바 안 TopLeft · TopRight 기준 (§3.1 표 그대로).
    /// </summary>
    internal static class FrameSceneBuilder
    {
        // ═════════════════════════ 상단 바 (§3.1) ═════════════════════════

        /// <summary>상단 바 높이 (UIFrame.TopBarHeight 130 — 전투 화면 StageArea는 이 아래에서 시작한다)</summary>
        const float BarHeight = UIFrame.TopBarHeight;
        /// <summary>노치 띠 높이 (M68: 바 위로 이어 칠한다 — 어떤 노치보다 크다)</summary>
        const float TopBleedHeight = 400f;
        /// <summary>전용 아트가 없을 때 벨벳 띠(ui_bar_velvet_9s) 9-slice 배율 (예전 상단 바 그대로)</summary>
        const float FallbackVelvetPpu = 1.5f;

        // 왼쪽: 수선점 레벨 (Title 28 굵게 Cream, 자동 크기 22까지) · 전투력 칩 (PanelLight@0.6 둥근 판)
        static readonly Vector2 LevelTextPosition = new(24, -10);
        static readonly Vector2 LevelTextSize = new(292, 40);
        const float LevelTextMin = 22f;
        static readonly Vector2 PowerChipPosition = new(16, -54);
        static readonly Vector2 PowerChipSize = new(300, 62);
        const float PowerChipAlpha = 0.6f;
        static readonly Vector2 PowerIconPosition = new(12, 0);
        const float PowerIconSize = 40f;
        static readonly Vector2 PowerTextPosition = new(60, 0);
        static readonly Vector2 PowerTextSize = new(228, 56);
        const float PowerTextFont = 38f;
        const float PowerTextMin = 28f;

        // 가운데 오른쪽: 단추 칩 (+ 아래 초당 단추) · 별조각 칩 (SceneParts.CurrencyChip)
        static readonly Vector2 GoldChipPosition = new(-446, -14);
        static readonly Vector2 GoldChipSize = new(240, 60);
        static readonly Vector2 GoldPerSecPosition = new(-446, -80);
        static readonly Vector2 GoldPerSecSize = new(240, 28);
        static readonly Vector2 GemChipPosition = new(-224, -14);
        static readonly Vector2 GemChipSize = new(210, 60);
        /// <summary>재화 칩 위 끝 (바 위에서 14) — 누르는 자리를 위로 넓혀도 바 밖(y &lt; 0)으로 나가지 않게</summary>
        const float ChipTopInset = 14f;
        /// <summary>별조각 칩 '+' 배지 (M08): 36x36 PrimaryButton 원 + Cream '+', 오른쪽 가운데에서 (−8, 0)</summary>
        const float GemPlusSize = 36f;
        static readonly Vector2 GemPlusPosition = new(-8, 0);
        const float GemPlusFont = 30f;
        /// <summary>별조각 칩 숫자: 오른쪽을 '+' 자리만큼 비운다 (20 → 52), 자동 크기 24~32 ("12,345"도 210 칩 안에)</summary>
        const float GemAmountLeft = 64f;
        const float GemAmountRight = 52f;
        const float GemAmountMin = 24f;

        // 오른쪽: 우편 · ≡ 메뉴 아이콘 버튼 (96x96 — 사이 8px라 누르는 자리를 넓히지 않는다)
        static readonly Vector2 MailButtonPosition = new(-116, -17);
        static readonly Vector2 MenuButtonPosition = new(-12, -17);
        static readonly Vector2 TopIconPosition = new(0, -2);
        const float TopLabelFont = 20f;
        const float TopLabelHeight = 26f;

        /// <summary>
        /// 상단 바 "SafeArea/TopBar"를 만들고 TopBarView를 묶은 뒤 그 바를 돌려준다 (IdleSceneBuilder가 ctx.TopBar에 넣는다 — 메타 · 튜토리얼 빌더보다 먼저).
        /// §3.1 (높이 UIFrame.TopBarHeight 130, 자식 순서 그대로):
        ///   TopBleed (첫 형제, 바 위로 400 — Panel 색, M68) · Velvet (전용 topbar_9s 바둑판 · 없으면 예전 벨벳 띠, 금 솔기가 아래) ·
        ///   LevelText '수선점 Lv.N' · PowerChip(+ Icon · PowerText — 이름에 "Power": PowerUpFloatView · 메타 빌더가 PowerChip/PowerText를 찾는다) ·
        ///   GoldChip · GoldPerSecText(이름 그대로, '초당 +N') · GemChip · MailButton(이름 · 경로 그대로, 숫자 배지) · MenuButton(≡, 점 = RedDotView(top.settings)).
        /// 로고 · 공지 · 가방 · 설정 · 일지 · 출석 · EventChip은 만들지 않는다 (≡ 메뉴 · 전투 화면 빠른 아이콘으로).
        /// </summary>
        public static RectTransform BuildTopBar(SceneBuildContext ctx)
        {
            var bar = NewRect("TopBar", ctx.SafeArea).Top(0, BarHeight);
            var view = bar.gameObject.AddComponent<TopBarView>();

            BuildTopBleed(bar);
            BuildVelvet(bar);

            // 왼쪽: 수선점 레벨 + 전투력 칩 (숫자 · 누름은 메타 빌더가 PowerChip · PowerChip/PowerText를 찾아 TopBarMetaView에 묶는다)
            var level = Txt(bar, "LevelText", "수선점 Lv.1", UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(LevelTextMin);
            level.rectTransform.At(TopLeft, LevelTextPosition, LevelTextSize);
            BuildPowerChip(bar);

            // 재화: 단추 칩 아래에 초당 단추, 그 오른쪽에 별조각 칩. 두 칩은 버튼 (누르면 얻는 곳, 길게 누르면 안내 — M08)
            var goldText = CurrencyChip(bar, "GoldChip", CurrencyType.Gold, GoldChipPosition, GoldChipSize);
            var goldButton = ChipButton(goldText, GoldChipSize);
            var goldPerSec = Txt(bar, "GoldPerSecText", "초당 +0", UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineRight);
            goldPerSec.rectTransform.At(TopRight, GoldPerSecPosition, GoldPerSecSize);
            var gemText = CurrencyChip(bar, "GemChip", CurrencyType.Gem, GemChipPosition, GemChipSize);
            var gemButton = ChipButton(gemText, GemChipSize);
            GemPlusBadge(gemText);

            // 오른쪽: 우편 (숫자 배지) · ≡ 메뉴 (점 = 메뉴에만 있는 알림 — v1은 안 읽은 공지, top.settings 아래로 모인다)
            var mail = TopIconButton(bar, "MailButton", "우편", ArtLib.Icon("menu_mail"), MailButtonPosition);
            var mailBadge = CornerBadge(mail.transform, UISize.BadgeCount);
            var menu = TopIconButton(bar, "MenuButton", "메뉴", MenuIcon(), MenuButtonPosition);
            AttachDot(menu.transform, RedDotPaths.TopSettings, false);

            new Binder(view)
                .Set("_levelText", level)
                .Set("_goldPerSecText", goldPerSec)
                .Set("_goldText", goldText)
                .Set("_gemText", gemText)
                .Set("_mailButton", mail)
                .Set("_menuButton", menu)
                .Set("_mailBadge", mailBadge)
                .Set("_goldChipButton", goldButton)
                .Set("_gemChipButton", gemButton)
                .Apply();
            return bar;
        }

        /// <summary>
        /// 재화 칩을 버튼으로 (M08): 칩 판(CurrencyStripView가 붙은 자기 Image)에 Button(색 변화 없음, 탐색 없음) + PressScale.
        /// 누르는 자리는 위아래로 88까지 (raycastPadding — 60 칩이면 14씩: 바 안 y 0~88, 96 위). 옆으로는 넓히지 않는다 (옆 칩과 12px).
        /// 버튼이 생기면 CurrencyStripView는 안내 말풍선을 0.35초 길게 누르기로 바꾼다 (누름은 TopBarView가 얻는 곳으로).
        /// </summary>
        static Button ChipButton(TextMeshProUGUI amount, Vector2 size)
        {
            var chip = amount.rectTransform.parent.GetComponent<Image>();
            chip.raycastTarget = true;
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            chip.gameObject.AddComponent<PressScale>();
            chip.HitPad(size, 0f, UISize.MinHit, 0f, 0f, 99f, ChipTopInset);
            return button;
        }

        /// <summary>
        /// 별조각 칩 '+' 배지 (M08): 오른쪽 가운데 (−8, 0)에 36x36 PrimaryButton 원 + Cream '+' 30 굵게 (누름 없음 — 칩 전체가 버튼).
        /// 숫자 칸은 오른쪽 여백 52로 줄이고 자동 크기 24~32 (SceneParts.CurrencyChip은 그대로 두고 만든 뒤에 고친다).
        /// </summary>
        static void GemPlusBadge(TextMeshProUGUI amount)
        {
            var chip = amount.rectTransform.parent;
            amount.rectTransform.Stretch(GemAmountLeft, GemAmountRight, 0, 0);
            amount.FitFloor(GemAmountMin, UIType.H2);

            var circle = Img(chip, "PlusBadge", UITheme.PrimaryButton, CircleSprite);
            circle.rectTransform.At(MiddleRight, GemPlusPosition, new Vector2(GemPlusSize, GemPlusSize));
            var plus = Txt(circle.transform, "Plus", "+", GemPlusFont, UITheme.Cream, TextAlignmentOptions.Center, true);
            // 한글 글꼴의 '+'는 줄 가운데보다 조금 낮게 앉는다 — 4px 올려 원 가운데에 맞춘다
            plus.rectTransform.Stretch(0, 0, -4, 4);
        }

        /// <summary>노치 띠 (M68): 첫 형제, 바 위 가장자리에 붙어 위로 400 — 노치 기기에서 밤 배경이 바 위로 비치지 않게. Panel 색 (전용 아트 위 78줄과 같은 색), 누름 없음</summary>
        static void BuildTopBleed(RectTransform bar)
        {
            var bleed = Img(bar, "TopBleed", UITheme.Panel);
            var rect = bleed.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, TopBleedHeight);
        }

        /// <summary>
        /// 바탕 "Velvet" (Stretch). 전용 topbar_9s(576x260, 양 끝 장식 테두리 (216, 0, 216, 0))면 흰색 Tiled, 배율 = 그림 높이 / 바 높이 (260 / 130 = 2):
        /// 그림이 세로로 정확히 한 장이라 가운데 72px 무늬가 이음매 없이 이어진다 (폭 1080 · 1440은 딱 맞고 1200은 오른쪽 끝에 무늬가 한 번 끊긴다).
        /// 전용 아트가 없거나 테두리가 양 끝 장식이 아니면(임포트 규칙 BorderOverrides가 아직 없어 48 테두리로 들어옴 — 끝 장식이 띠 가운데에 되풀이된다)
        /// 예전 벨벳 띠(ui_bar_velvet_9s, 배율 1.5, 금 솔기가 아래)로 대신하고, 그것도 없으면 Panel 색 판 + 옅은 금 선.
        /// </summary>
        static void BuildVelvet(RectTransform bar)
        {
            var art = UiArt.TopBar9s(out bool dedicated);
            if (dedicated && !HasEndCaps(art))
            {
                Debug.LogWarning($"[FrameSceneBuilder] Art/UI/topbar_9s.png의 9-slice 테두리 {art.border}(왼쪽, 아래, 오른쪽, 위)가 양 끝 장식이 아니라 "
                                 + "예전 벨벳 띠로 대신합니다 — ArtImportPostprocessor.BorderOverrides에 topbar_9s = (216, 0, 216, 0)을 넣고 GetVersion()을 올려 다시 임포트하세요.");
                art = UiArt.BarVelvet9s;
                dedicated = false;
            }

            if (dedicated)
            {
                var tiled = Img(bar, "Velvet", Color.white, art);
                tiled.type = Image.Type.Tiled;
                tiled.pixelsPerUnitMultiplier = art.rect.height / BarHeight;
                tiled.rectTransform.Stretch();
                return;
            }

            UiArt.Panel(bar, "Velvet", art, FallbackVelvetPpu, null, UITheme.Panel).rectTransform.Stretch();
            if (!UiArt.IsArt(art)) Img(bar, "BottomLine", UITheme.WithAlpha(UITheme.GoldAccent, 0.45f)).rectTransform.Bottom(0, 2);
        }

        /// <summary>테두리가 양 끝 장식뿐인지 (왼쪽 · 오른쪽 &gt; 0, 위 · 아래 0) — sprite.border = (왼쪽, 아래, 오른쪽, 위)</summary>
        static bool HasEndCaps(Sprite sprite)
        {
            var border = sprite.border;
            return border.x > 0f && border.z > 0f && border.y <= 0f && border.w <= 0f;
        }

        /// <summary>
        /// 전투력 칩 "PowerChip" (300x62, PanelLight@0.6 둥근 판 + Button(색 변화 없음) + PressScale — 누르면 권장 전투력 토스트, 메타 빌더가 이 Button을
        /// TopBarMetaView._powerButton에 묶는다): Icon 40 (전투력 그림 UiArt.PowerIcon = Icons/power, 없으면 지킴이 그림, 그것도 없으면 금색 별 — ART-UPGRADE §4.3 #11) 왼쪽 12 ·
        /// "PowerText" 왼쪽 60에서 228x56, 38 굵게 Gold, 자동 크기 28~40 (TopBarMetaView가 숫자만 쓴다). 누르는 자리는 위아래로 88까지.
        /// </summary>
        static Button BuildPowerChip(RectTransform bar)
        {
            var chip = Img(bar, "PowerChip", UITheme.WithAlpha(UITheme.PanelLight, PowerChipAlpha), RoundSprite, true);
            chip.rectTransform.At(TopLeft, PowerChipPosition, PowerChipSize);
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            chip.gameObject.AddComponent<PressScale>();
            chip.HitPad(PowerChipSize, 0f, UISize.MinHit);

            var power = UiArt.PowerIcon;
            var icon = UiArt.Icon(chip.transform, "Icon", power != null ? power : UiArt.Star, MiddleLeft, PowerIconPosition, PowerIconSize);
            // 그림이 하나도 없어 별로 대신하면 금색 (전투력 · 지킴이 그림은 흰색 그대로)
            if (power == null || power == UiArt.Star) icon.color = UITheme.Gold;
            var text = Txt(chip.transform, "PowerText", "0", PowerTextFont, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true)
                .FitFloor(PowerTextMin, UIType.H1);
            text.rectTransform.At(MiddleLeft, PowerTextPosition, PowerTextSize);
            return button;
        }

        /// <summary>
        /// 상단 바 아이콘 버튼 (96x96, TopRight 기준): 투명 판(누르는 자리) + Button(색 변화 없음) + PressScale,
        /// 아이콘 64 위 가운데 (0, −2) · 이름 20 굵게 Cream 외곽선 아래 가운데 96x26. 아이콘이 없으면 별 그림.
        /// </summary>
        static Button TopIconButton(RectTransform bar, string name, string label, Sprite icon, Vector2 position)
        {
            var size = new Vector2(UISize.TopIconButton, UISize.TopIconButton);
            var hit = Img(bar, name, Color.clear, null, true);
            hit.rectTransform.At(TopRight, position, size);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            hit.gameObject.AddComponent<PressScale>();

            UiArt.Icon(hit.transform, "Icon", icon != null ? icon : UiArt.Star, TopCenter, TopIconPosition, UISize.TopIcon);
            var text = Txt(hit.transform, "Label", label, TopLabelFont, UITheme.Cream, TextAlignmentOptions.Center, true);
            text.rectTransform.At(BottomCenter, Vector2.zero, new Vector2(UISize.TopIconButton, TopLabelHeight));
            UiArt.Outline(text);
            return button;
        }

        /// <summary>≡ 메뉴 아이콘: Icons/menu_all (§9.3), 없으면 Icons/menu_settings</summary>
        static Sprite MenuIcon()
        {
            var art = ArtLib.Icon("menu_all");
            return art != null ? art : ArtLib.Icon("menu_settings");
        }

        // ═════════════════════════ ≡ 메뉴 팝업 (§3.6) ═════════════════════════

        /// <summary>메뉴 창 폭</summary>
        const float MenuWindowWidth = 900f;
        /// <summary>빌드 때 칸 수 (v1 여섯 + SPEC4 다섯 = 열한 칸 → 네 줄, 창 1180. 런타임은 보이는 칸 수로 다시 잰다 — MenuPopup.FitWindow)</summary>
        const int MenuBuildTiles = 11;
        /// <summary>칸 판(보라 바느질 안쪽 판) 9-slice 배율 (테두리 24px)</summary>
        const float MenuTilePpu = 2f;
        /// <summary>칸 아이콘 자리 (위 가운데 기준)</summary>
        static readonly Vector2 MenuTileIconPosition = new(0, -18);
        /// <summary>칸 이름표: 아래에서 16, 높이 36</summary>
        const float MenuTileLabelBottom = 16f;
        const float MenuTileLabelHeight = 36f;
        /// <summary>
        /// 격자 위 끝을 Body 위 끝(창 위에서 136)보다 이만큼 내린다 → 창 위에서 176 (Wave 2 r2: 두 줄 격자 아래 빈 창이 188px로
        /// 제목 구분선 아래 틈보다 훨씬 커서 창이 아래로 무거워 보였다). 창 높이 300 + 220·줄은 그대로 (AutoShot IA-menu 740) —
        /// 몇 줄이든 격자 아래 빈 곳은 148로 같다
        /// </summary>
        const float MenuGridTopInset = 40f;

        /// <summary>
        /// ≡ 메뉴 팝업 "Popups/MenuPopup" (ctx.PopupRoot 아래, BuildPopup(900, 300 + 220·줄) — 열한 칸 네 줄 1180, 제목 '메뉴')을 만들어
        /// ctx.Popups에 스스로 넣고 돌려준다 (IdleSceneBuilder가 다른 팝업 빌더 다음, 팝업을 끄기 전에 부른다).
        /// Window/Body/Grid: 3열 격자, 칸 240x196, 간격 24, 가운데 (Body가 창 위에서 136, 격자는 그 안에서 40 내려 창 위에서 176). 칸 순서와 이름(경로 고정 —
        /// AutoShot.MenuTileOrder와 같은 순서, 로드맵 델타 36: SPEC4 칸은 활동 칸 뒤 · 가방 · 공지 · 설정 앞):
        /// JournalTile 수선 일지 · AttendanceTile 출석부 · EventTile 이벤트 ·
        /// EventHubTile 이벤트 · SeasonTile 산책 수첩 · CodexTile 도감 · AchievementsTile 수선점 기록 · CouponTile 쿠폰 (SPEC4, UX-SPEC-IA §8.8) ·
        /// BagTile 가방 · NoticeTile 공지 · SettingsTile 설정.
        /// 칸 = 보라 바느질 안쪽 판(PanelInset9s, 자기 Image = Plate) + Button + PressScale, Icon 104 위 가운데 (0, −18), 이름 26 굵게 Cream Bottom(16, 36),
        /// 배지는 칸 오른쪽 위 모서리에서 (−8, −8): 일지 숫자 · 출석 점 · 이벤트 모음 점(top.event) · 수첩 점(top.event.season) · 도감 점(tab.characters.codex) ·
        /// 기록 점(top.journal.records) = RedDotView, 공지 숫자 = MenuPopup._noticeBadge. EventTile에도 이벤트 점(top.event)을 붙이되, 이벤트 모음 칸이 보이면
        /// (EventTile = 이벤트 상점 길) MenuPopup이 그 점을 쉬게 한다 (_eventTileDot.Suppressed — 모음 칸이 숨은 동안은 EventTile이 이벤트 NEW를 보여 준다).
        /// 잠긴 칸 숨김 · 창 높이 맞춤 · 누름 · EventTile 이름표는 MenuPopup이 한다.
        /// </summary>
        public static MenuPopup BuildMenuPopup(SceneBuildContext ctx)
        {
            var size = new Vector2(MenuWindowWidth, MenuPopup.WindowHeight(MenuPopup.RowsFor(MenuBuildTiles)));
            var popup = BuildPopup<MenuPopup>(ctx.PopupRoot, "MenuPopup", "메뉴", size, out var body, out _);

            var grid = NewRect("Grid", body).Stretch(0, 0, MenuGridTopInset, 0);
            var gap = UISize.MenuTileGap;
            AddGrid(grid, new Vector2(UISize.MenuTileWidth, UISize.MenuTileHeight), new Vector2(gap, gap), MenuPopup.Columns, new RectOffset(0, 0, 0, 0));

            var journal = MenuTile(grid, "JournalTile", "수선 일지", ArtLib.Icon("journal"));
            AttachDot(journal.transform, RedDotPaths.TopJournal, true);
            var attendance = MenuTile(grid, "AttendanceTile", "출석부", ArtLib.Icon("attendance"));
            AttachDot(attendance.transform, RedDotPaths.TopAttendance, false);
            var eventTile = MenuTile(grid, "EventTile", MenuPopup.EventTileLabel, ArtLib.Icon("currency_event"), out var eventLabel);
            // 이벤트 점은 EventTile에도 — 모음 칸이 숨은 동안(이벤트 콘텐츠 전 · 3-1 전) 새로 열림 NEW를 보여 주고, 모음 칸이 보이면 MenuPopup이 쉬게 한다
            var eventTileDot = AttachDot(eventTile.transform, RedDotPaths.TopEvent, false);

            // SPEC4 칸 (UX-SPEC-IA §8.8, 로드맵 델타 36) — 받는 쪽은 P4-6 (MetaNavigator: 이벤트 모음 · 수첩 · 도감 · 수선점 기록 탭), 쿠폰은 이 패키지의 CouponPopup
            var eventHub = MenuTile(grid, "EventHubTile", MenuPopup.EventHubTileLabel, TileIcon("Meta/feature_events.png", "Icons/stage_festival.png"));
            AttachDot(eventHub.transform, RedDotPaths.TopEvent, false);
            var season = MenuTile(grid, "SeasonTile", MenuPopup.SeasonTileLabel, TileIcon("UI/season_icon.png", "Icons/stage_fullmoon.png"));
            AttachDot(season.transform, RedDotPaths.Season, false);
            var codex = MenuTile(grid, "CodexTile", MenuPopup.CodexTileLabel, TileIcon("Meta/feature_codex.png", "UI/gacha_card_back.png"));
            AttachDot(codex.transform, RedDotPaths.Codex, false);
            var achievements = MenuTile(grid, "AchievementsTile", MenuPopup.AchievementsTileLabel, TileIcon("Meta/feature_achievements.png", "UI/medal_gold.png"));
            AttachDot(achievements.transform, RedDotPaths.JournalRecords, false);
            var coupon = MenuTile(grid, "CouponTile", MenuPopup.CouponTileLabel, TileIcon("Icons/coupon.png", "Icons/reward_generic.png"));

            var bag = MenuTile(grid, "BagTile", "가방", ArtLib.Icon("menu_bag"));
            var notice = MenuTile(grid, "NoticeTile", "공지", ArtLib.Icon("menu_notice"));
            var noticeBadge = CornerBadge(notice.transform, UISize.BadgeCount);
            var settings = MenuTile(grid, "SettingsTile", "설정", ArtLib.Icon("menu_settings"));

            new Binder(popup)
                .Set("_journalTile", journal)
                .Set("_attendanceTile", attendance)
                .Set("_eventTile", eventTile)
                .Set("_bagTile", bag)
                .Set("_noticeTile", notice)
                .Set("_settingsTile", settings)
                .Set("_noticeBadge", noticeBadge)
                .Set("_eventHubTile", eventHub)
                .Set("_seasonTile", season)
                .Set("_codexTile", codex)
                .Set("_achievementsTile", achievements)
                .Set("_couponTile", coupon)
                .Set("_eventTileLabel", eventLabel)
                .Set("_eventTileDot", eventTileDot)
                .Apply();
            ctx.Popups.Add(popup);
            return popup;
        }

        /// <summary>SPEC4 칸 아이콘: 앞의 그림부터 있는 것 (Art/ 아래 경로), 다 없으면 null (칸이 별로 대신한다). 전용 메뉴 아이콘은 나중 그림 (P4-10 목록)</summary>
        static Sprite TileIcon(string preferred, string fallback)
        {
            var art = ArtLib.Load(preferred);
            return art != null ? art : ArtLib.Load(fallback);
        }

        /// <summary>메뉴 칸 하나 (이름표는 쓰지 않을 때)</summary>
        static Button MenuTile(RectTransform grid, string name, string label, Sprite icon) => MenuTile(grid, name, label, icon, out _);

        /// <summary>메뉴 칸 하나: 안쪽 판(누르는 자리 = 칸 전체) + Button(색 변화 없음) + PressScale, 아이콘 104, 이름 26 굵게 Cream</summary>
        static Button MenuTile(RectTransform grid, string name, string label, Sprite icon, out TextMeshProUGUI labelText)
        {
            var plate = UiArt.Panel(grid, name, UiArt.PanelInset9s, MenuTilePpu, null, UITheme.PanelLight, true);
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            plate.gameObject.AddComponent<PressScale>();

            UiArt.Icon(plate.transform, "Icon", icon != null ? icon : UiArt.Star, TopCenter, MenuTileIconPosition, UISize.IconXL);
            var text = Txt(plate.transform, "Label", label, UIType.Body, UITheme.Cream, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.Caption, UIType.Body);
            text.rectTransform.Bottom(MenuTileLabelBottom, MenuTileLabelHeight);
            labelText = text;
            return button;
        }

        // ═════════════════════════ 배지 (§1.5 — 중심이 누르는 자리 오른쪽 위 모서리에서 (−8, −8)) ═════════════════════════

        /// <summary>
        /// 모서리 배지: SceneParts.BuildBadge를 지름 size로 줄이고(점 30 · 숫자 40) 중심을 오른쪽 위 모서리에서 (−8, −8)에 둔다. 꺼 둔 채 돌려준다
        /// (뷰가 SetCount · SetDot으로 켠다 — 첫 프레임에 "0"이 비치지 않게).
        /// </summary>
        static Badge CornerBadge(Transform target, float size)
        {
            var badge = BuildBadge(target);
            float corner = size * 0.5f - UISize.BadgeInset;
            ((RectTransform)badge.transform).At(TopRight, new Vector2(corner, corner), new Vector2(size, size));
            badge.gameObject.SetActive(false);
            return badge;
        }

        /// <summary>
        /// 빨간 점 트리 배지 (RedDotView를 target에, 배지는 자식): count면 숫자 배지 40, 아니면 점 30.
        /// 메타 빌더의 AttachRedDot과 같은 모양이지만 이 파일 안에서 끝낸다 (그 파일은 다른 단계가 고친다).
        /// </summary>
        static RedDotView AttachDot(Transform target, string path, bool count)
        {
            var badge = CornerBadge(target, count ? UISize.BadgeCount : UISize.BadgeDot);
            var view = target.gameObject.AddComponent<RedDotView>();
            var so = new SerializedObject(view);
            so.FindProperty("_path").stringValue = path;
            so.FindProperty("_badge").objectReferenceValue = badge;
            so.FindProperty("_showCount").boolValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }
    }
}
