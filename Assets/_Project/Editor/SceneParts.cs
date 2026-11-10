using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 여러 화면 빌더가 같이 쓰는 UI 조각 (팝업 틀, 패널 머리, 버튼 공장, 재화 칩/아이콘, 비용 버튼, 빨간 점, 안내 말풍선, 격자). 그림은 UiArt.
    /// 숫자는 UITokens(UIType · UISpace · UISize), 색은 UITheme. 아트는 흰색 그대로 쓰고, 단색은 아트가 없을 때의 자리표시에만 쓴다.
    /// </summary>
    internal static class SceneParts
    {
        // ═════════════════════════ 버튼 공장 (M25 · M26 · M82) ═════════════════════════

        /// <summary>
        /// 버튼 모양 (M26 뜻):
        ///   · Gold = 주기 · 확정 (받기, 모두 받기, 두 배로 받기, 도장 찍기, 출발, 도전, 진화 제작, 10회 소환, 최대 강화) — 한 화면에 하나 (줄마다 반복되는 받기 · 준비 완료 덮개는 예외)
        ///   · Primary = 나아가기 · 이동 · 되풀이 (가기 ›, …하러 가기, 보내기, 레벨업, 강화 x1/x10, 1회 소환, 혼자 있는 확인)
        ///   · Secondary = 닫기 · 보조 (취소, 나중에, 짝지은 확인, 정렬, 도감, 자동 선택, 되돌리기, 그냥 받기) — 새 줄에서는 왼쪽
        ///   · Soft = 크림 카드 위 작은 버튼 · Danger = 지우기 · 초기화 같은 파괴 · Ghost = 바탕 없는 글자 링크
        /// </summary>
        public enum ButtonStyle
        {
            Gold = 0,
            Primary = 1,
            Secondary = 2,
            Soft = 3,
            Danger = 4,
            Ghost = 5,
        }

        /// <summary>단색 버튼(아트 없음)이 비활성일 때 색 (불투명한 차가운 회색 — 0.55 · 알파 0.7처럼 탁해지지 않게)</summary>
        public static readonly Color DisabledTint = new(0.62f, 0.60f, 0.70f, 1f);

        /// <summary>Secondary 단색 자리표시 테두리 (Lavender@0.35, 2px)</summary>
        static readonly Color SecondaryEdge = new(0.769f, 0.698f, 0.949f, 0.35f);

        /// <summary>
        /// 버튼 바탕 입히기 (크기 · 이름 · 글자는 건드리지 않는다). 아트가 있으면 흰색 그대로 + 스프라이트 교체(비활성 = ui_button_disabled_9s, UiArt.SetButtonArt):
        ///   Gold = button_gold_9s, Primary = button_primary_9s, Secondary = ui_button_secondary_9s,
        ///   Soft = ui_button_soft_9s (UiArt.ButtonSoft9s — 아직 없으면 panel_parchment_9s 그대로, SX-2) (9-slice 배율 ppu, 기본 2 = 24px 모서리).
        ///   Danger = 흰색 틴트용 ui_tab_9s에 DangerButton 색 (비활성일 때는 흰색으로 돌려 회색 그림이 물들지 않게 — ButtonLabelDim).
        ///   Ghost = 바탕 없음 (투명 이미지가 누르는 자리).
        /// 아트가 없으면 단색 둥근 사각형: Gold = UITheme.Gold, Primary = PrimaryButton, Secondary = Secondary + 2px Lavender@0.35 테두리,
        ///   Soft = ParchmentDark, Danger = DangerButton. 단색은 누름 (0.92, 0.90, 0.95) · 비활성 (0.62, 0.60, 0.70) · 0.06초.
        /// 버튼 아래 "Label" 글자가 있으면 ButtonLabelDim을 붙여 비활성일 때 이름을 알파 0.5로 흐리게 한다 (공장 버튼만).
        /// Menu.Skin · Character.ApplyArt · Meta.Skin · Tutorial.GoldButton 본문이 이 한 줄이 된다.
        /// </summary>
        public static void SkinButton(Button button, ButtonStyle style, float ppu = 2f)
        {
            if (button == null || !(button.targetGraphic is Image image)) return;
            Graphic tintedSkin = null;
            var skinTint = Color.white;
            switch (style)
            {
                case ButtonStyle.Gold:
                    SkinArt(button, image, UiArt.ButtonGold9s, ppu, UITheme.Gold, false);
                    break;
                case ButtonStyle.Primary:
                    SkinArt(button, image, UiArt.ButtonPrimary9s, ppu, UITheme.PrimaryButton, false);
                    break;
                case ButtonStyle.Secondary:
                    SkinArt(button, image, UiArt.ButtonSecondary9s, ppu, UITheme.Secondary, true);
                    break;
                case ButtonStyle.Soft:
                    SkinArt(button, image, UiArt.ButtonSoft9s, ppu, UITheme.ParchmentDark, false);
                    break;
                case ButtonStyle.Danger:
                    var tab = UiArt.Tab9s;
                    if (UiArt.IsArt(tab))
                    {
                        UiArt.SetButtonArt(button, tab, ppu);
                        image.color = UITheme.DangerButton;
                        tintedSkin = image;
                        skinTint = UITheme.DangerButton;
                    }
                    else SkinFlat(button, image, UITheme.DangerButton, false);
                    break;
                default:
                    SkinGhost(button, image);
                    break;
            }

            // 비용 버튼은 이름 색을 CostButtonView가 정한다 (흐림을 겹치지 않는다)
            if (button.GetComponent<CostButtonView>() != null) return;
            var label = button.transform.Find("Label");
            if (label != null && label.TryGetComponent<TMP_Text>(out var text)) AttachLabelDim(button, text, tintedSkin, skinTint);
        }

        /// <summary>그 모양의 이름 글자 색 · 굵기 (Gold · Soft = Ink, Danger = Cream, Ghost = Lavender + ' ›', 나머지 = Text — 모두 굵게)</summary>
        public static void StyleLabel(TMP_Text label, ButtonStyle style)
        {
            if (label == null) return;
            label.color = style switch
            {
                ButtonStyle.Gold or ButtonStyle.Soft => UITheme.Ink,
                ButtonStyle.Danger => UITheme.Cream,
                ButtonStyle.Ghost => UITheme.Lavender,
                _ => UITheme.Text,
            };
            label.fontStyle |= FontStyles.Bold;
            if (style == ButtonStyle.Ghost && !string.IsNullOrEmpty(label.text)
                && !label.text.TrimEnd().EndsWith("›", System.StringComparison.Ordinal))
                label.text = label.text.TrimEnd() + " ›";
        }

        /// <summary>
        /// 버튼 공장: 바탕(SkinButton) + 이름(StyleLabel, 굵게) + 누름 크기(PressScale) + 비활성 흐림(ButtonLabelDim). 크기 · 자리는 정하지 않는다 (부르는 쪽이 At/Stretch).
        /// 글자 크기 안내: XL 36 · L 30 · M 28 · S 24 (UIType.Button*). leadingIcon이 있으면 이름 앞에 아이콘을 붙여 가운데로 모은다.
        /// 9-slice 배율은 2(24px 모서리) — 높이가 90~110이 아니면 부르는 쪽이 UiArt.ButtonPpu(높이)로 맞춘다.
        /// </summary>
        public static Button Button(Transform parent, string name, string label, ButtonStyle style, float fontSize, out TextMeshProUGUI labelText,
            Sprite leadingIcon = null)
        {
            var button = Btn(parent, name, label, FlatColor(style), fontSize, out labelText);
            StyleLabel(labelText, style);
            SkinButton(button, style);
            if (leadingIcon != null) AddLeadingIcon(button, labelText, leadingIcon, fontSize);
            return button;
        }

        /// <summary>그 모양의 단색 자리표시 색</summary>
        public static Color FlatColor(ButtonStyle style) => style switch
        {
            ButtonStyle.Gold => UITheme.Gold,
            ButtonStyle.Primary => UITheme.PrimaryButton,
            ButtonStyle.Secondary => UITheme.Secondary,
            ButtonStyle.Soft => UITheme.ParchmentDark,
            ButtonStyle.Danger => UITheme.DangerButton,
            _ => Color.clear,
        };

        static void SkinArt(Button button, Image image, Sprite art, float ppu, Color flat, bool outlineFallback)
        {
            if (UiArt.IsArt(art))
            {
                UiArt.SetButtonArt(button, art, ppu);
                return;
            }
            SkinFlat(button, image, flat, outlineFallback);
        }

        /// <summary>단색 둥근 사각형 (아트가 없을 때). 누름 · 손 올림 · 비활성 색과 0.06초 (M61 · M25)</summary>
        static void SkinFlat(Button button, Image image, Color color, bool outline)
        {
            image.sprite = RoundSprite;
            image.type = RoundSprite != null && RoundSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = HighlightedTint;
            colors.pressedColor = PressedTint;
            colors.selectedColor = Color.white;
            colors.disabledColor = DisabledTint;
            colors.fadeDuration = TintFade;
            button.colors = colors;
            if (outline && image.GetComponent<Outline>() == null)
            {
                var edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = SecondaryEdge;
                edge.effectDistance = new Vector2(2f, -2f);
            }
        }

        /// <summary>
        /// 바탕 없는 글자 링크: 투명 이미지가 누르는 자리. 스프라이트 교체로 두고 비활성 그림에 흰 네모(투명 색이라 보이지 않음)를 넣어,
        /// 비활성이 바뀔 때 ButtonLabelDim이 알림을 받게 한다.
        /// </summary>
        static void SkinGhost(Button button, Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.clear;
            image.raycastTarget = true;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { disabledSprite = WhiteSprite };
        }

        static void AttachLabelDim(Button button, TMP_Text label, Graphic tintedSkin, Color skinTint)
        {
            var dim = button.GetComponent<ButtonLabelDim>();
            if (dim == null) dim = button.gameObject.AddComponent<ButtonLabelDim>();
            var so = new UnityEditor.SerializedObject(dim);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_tintedSkin").objectReferenceValue = tintedSkin;
            so.FindProperty("_skinTint").colorValue = skinTint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>이름 앞 아이콘: 버튼 안에 가운데 정렬 가로 줄 "Content"(아이콘 + 이름)를 만들어 이름을 옮긴다</summary>
        static void AddLeadingIcon(Button button, TextMeshProUGUI label, Sprite icon, float fontSize)
        {
            var content = NewRect("Content", button.transform).Stretch(12, 12, 0, 0);
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 10;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            float size = Mathf.Round(fontSize * 1.25f);
            var image = Img(content, "Icon", Color.white, icon);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size;
            element.preferredHeight = size;

            label.rectTransform.SetParent(content, false);
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // ═════════════════════════ 창 · 카드 ═════════════════════════

        /// <summary>밤 하늘색 창 틀 (아트가 있으면 금테 9-slice, 없으면 둥근 사각형)</summary>
        public static Image Window(Transform parent, string name, bool raycast = true)
        {
            var frame = ArtLib.UI("panel_night_9s");
            return frame != null ? Img(parent, name, Color.white, frame, raycast) : Img(parent, name, UITheme.Panel, RoundSprite, raycast);
        }

        /// <summary>크림색 동화책 카드 (아트가 있으면 금테 9-slice, 없으면 둥근 사각형)</summary>
        public static Image ParchmentCard(Transform parent, string name, bool raycast = false)
        {
            var frame = ArtLib.UI("panel_parchment_9s");
            return frame != null ? Img(parent, name, Color.white, frame, raycast) : Img(parent, name, UITheme.Parchment, RoundSprite, raycast);
        }

        /// <summary>
        /// 팝업 틀: {name}(전체 CanvasGroup + 팝업) / Dim(어둡게 덮기, 눌러서 닫기, 자기 CanvasGroup — UIPopup._dimGroup) / Window / Title · Divider · CloseButton · Body.
        /// 코치 · 메타 경로가 "{name}/Window/Body/…"를 찾으므로 이 계층은 바꾸지 않는다.
        /// 닫기 버튼은 그림 76x76, 누르는 자리 100x100 (raycastPadding -12, M22). 덮개 CanvasGroup은 raycast를 그대로 받는다 (M48).
        /// </summary>
        public static T BuildPopup<T>(RectTransform root, string name, string title, Vector2 size, out RectTransform body, out TextMeshProUGUI titleText)
            where T : UIPopup
        {
            var rt = NewRect(name, root).Stretch();
            rt.gameObject.AddComponent<CanvasGroup>();
            var popup = rt.gameObject.AddComponent<T>();

            var dim = Img(rt, "Dim", UITheme.Dim, null, true);
            dim.rectTransform.Stretch();
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };

            var window = Window(rt, "Window").rectTransform.At(Center, Vector2.zero, size);
            titleText = Txt(window, "Title", title, UIType.PopupTitle, UITheme.Cream, TextAlignmentOptions.Center, true);
            titleText.rectTransform.Top(6, 104, 120, 120);
            // 제목 아래 금실 구분선 (비율 고정 — 가운데 단추가 찌그러지지 않게). 아트가 없으면 옅은 금색 선
            var dividerArt = UiArt.DividerGold;
            if (dividerArt != null)
            {
                var divider = Img(window, "Divider", Color.white, dividerArt);
                divider.preserveAspect = true;
                divider.rectTransform.Top(88, 44, 40, 40);
            }
            else Img(window, "Divider", UITheme.GoldAccent * new Color(1, 1, 1, 0.6f)).rectTransform.Top(110, 2, 40, 40);
            var close = CloseButton(window);
            ((RectTransform)close.transform).At(TopRight, new Vector2(-22, -22), new Vector2(UISize.CloseButton, UISize.CloseButton));
            float pad = UISize.CloseHitPad;
            close.targetGraphic.raycastPadding = new Vector4(-pad, -pad, -pad, -pad);

            body = NewRect("Body", window).Stretch(36, 36, 136, 36);
            new Binder(popup).Set("_window", window).Set("_closeButton", close).Set("_dimButton", dimButton)
                .TrySet("_dimGroup", dimGroup)
                .Apply();
            return popup;
        }

        /// <summary>
        /// 쓰는 팝업의 잔액 칩 (M47): 창 왼쪽 위 (30, -30), 210x56 밤 알약 + 재화 아이콘 36 + 보유량 26 Cream 굵게 (CurrencyStripView가 쓴다).
        /// 이름은 "BalanceChip", Window의 바로 아래 자식 (Body 기준 경로를 바꾸지 않는다). 누르면 재화 안내 말풍선 (M59).
        /// fitTitle이면 가운데 제목이 칩과 겹치지 않게 제목 좌우를 250씩 비운다 (Top(6, 104, 250, 250)).
        /// </summary>
        public static CurrencyStripView PopupBalanceChip(RectTransform window, CurrencyType type, bool fitTitle = true)
        {
            var size = new Vector2(UISize.BalanceChipWidth, UISize.BalanceChipHeight);
            var chip = UiArt.Panel(window, "BalanceChip", UiArt.PillNight9s, UiArt.CapsulePpu(size.y), null,
                UITheme.WithAlpha(UITheme.PanelLight, 0.9f), true);
            chip.rectTransform.At(TopLeft, new Vector2(UISize.BalanceChipInset, -UISize.BalanceChipInset), size);
            var icon = CurrencyIcon(chip.transform, type, 36).rectTransform;
            icon.anchorMin = icon.anchorMax = MiddleLeft;
            icon.anchoredPosition = new Vector2(30, 0);
            var amount = Txt(chip.transform, "Amount", "0", 26, UITheme.Cream, TextAlignmentOptions.MidlineRight, true).FitFloor(18f);
            amount.rectTransform.Stretch(60, 18, 0, 0);

            var view = chip.gameObject.AddComponent<CurrencyStripView>();
            new Binder(view).SetInt("_type", (int)type).Set("_amount", amount).Apply();

            if (fitTitle && window.Find("Title") is RectTransform title) title.Top(6, 104, 250, 250);
            return view;
        }

        /// <summary>패널 제목 (예전 모양: 40 굵게, (30, -26) 500x56). 새 화면은 PanelHeader를 쓴다</summary>
        public static TextMeshProUGUI PanelTitle(RectTransform parent, string title)
        {
            var text = Txt(parent, "TitleText", title, 40, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            text.rectTransform.At(TopLeft, new Vector2(30, -26), new Vector2(500, 56));
            return text;
        }

        /// <summary>탭 패널 머리 줄 (M51): 위 88px "Header" (제목 + 왼쪽 칸 + 오른쪽 칸 240). 내용은 y 96부터. 자세한 것은 아래 overload</summary>
        public static RectTransform PanelHeader(RectTransform parent, string title, out RectTransform left, out RectTransform right) =>
            PanelHeader(parent, title, null, out left, out right, out _, out _);

        /// <summary>
        /// 탭 패널 머리 줄 (M51 · C-15): parent 위 88px "Header", 좌우 여백 24.
        ///   · 제목(있으면): "TitleText" 34 Cream 굵게, x 24, 폭은 글에 맞춰 220까지 (넘치면 자동 크기 24까지).
        ///   · left: 제목 끝 + 16부터 오른쪽 칸 앞 16까지 늘어나는 칸 (48px 칩 — PanelLight 알약 24 글자 — 을 부르는 쪽이 넣는다).
        ///   · hint(있으면): left 칸을 채우는 한 줄 안내 (Hint 20 SubText, 최소 18, 넘치면 …). 기준 폭 1080에서 200px보다 좁으면 숨긴다.
        ///   · right: 오른쪽 240x88 칸 (모두 받기 240x64 · 정렬 180x64를 가운데 오른쪽에 둔다).
        /// 머리 아래 내용은 UISpace.HeaderContentTop(96)부터.
        /// </summary>
        public static RectTransform PanelHeader(RectTransform parent, string title, string hint, out RectTransform left, out RectTransform right,
            out TextMeshProUGUI titleText, out TextMeshProUGUI hintText)
        {
            float margin = UISpace.ContentMargin;
            float height = UISpace.HeaderHeight;
            var header = NewRect("Header", parent).Top(0, height);

            float x = margin;
            titleText = null;
            if (!string.IsNullOrEmpty(title))
            {
                float width = Mathf.Min(UISpace.HeaderTitleMax, Mathf.Ceil(EstimateTextWidth(title, UIType.PanelTitle)) + 4f);
                titleText = Txt(header, "TitleText", title, UIType.PanelTitle, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true)
                    .FitFloor(UIType.Label);
                titleText.rectTransform.At(MiddleLeft, new Vector2(x, 0), new Vector2(width, 56));
                x += width + UISpace.S16;
            }

            right = NewRect("Right", header).At(MiddleRight, new Vector2(-margin, 0), new Vector2(UISpace.HeaderRightSlot, height));
            float rightEdge = margin + UISpace.HeaderRightSlot + UISpace.S16;

            left = NewRect("Left", header);
            left.anchorMin = Vector2.zero;
            left.anchorMax = Vector2.one;
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = new Vector2(x, 0f);
            left.offsetMax = new Vector2(-rightEdge, 0f);

            hintText = null;
            if (!string.IsNullOrEmpty(hint))
            {
                hintText = Txt(left, "HintText", hint, UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
                hintText.FitFloor(UIType.HintMin);
                hintText.rectTransform.Stretch();
                float available = ReferenceWidth - x - rightEdge;
                if (available < 200f) hintText.gameObject.SetActive(false);
            }
            return header;
        }

        /// <summary>기준 화면 폭</summary>
        const float ReferenceWidth = 1080f;

        /// <summary>
        /// 빌드 때 글 폭 어림 (한글 · 한자 1em, 숫자 · 영문 0.6em, 빈칸 0.26em, 그 밖 0.8em — Noto Sans KR Bold 기준).
        /// 머리 제목처럼 빌드 때 정해진 짧은 글의 칸 폭에만 쓴다 (런타임 글은 TMP가 잰다).
        /// </summary>
        public static float EstimateTextWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float em = 0f;
            foreach (char c in text)
            {
                if (c == ' ' || c == '\u00A0') em += 0.26f;
                else if (c >= 0x1100 && (c <= 0x11FF || (c >= 0x3000 && c <= 0x9FFF) || (c >= 0xAC00 && c <= 0xD7A3))) em += 1f;
                else if (c < 0x80) em += char.IsUpper(c) ? 0.66f : 0.6f;
                else em += 0.8f;
            }
            return em * fontSize;
        }

        // ═════════════════════════ 상단 바 메뉴 버튼 (M10 · M22) ═════════════════════════

        /// <summary>예전 상단 바 메뉴 버튼 크기 (110x72)</summary>
        static readonly Vector2 LegacyMenuButtonSize = new(110, 72);

        /// <summary>
        /// 상단 바 메뉴 버튼 (예전 모양 그대로 110x72): 바탕 판 없이 아이콘 56px + 아래 이름표 21.
        /// 아이콘은 이름으로 고른다 (가방/공지/우편/설정/출석/일지 → Icons/menu_bag|menu_notice|menu_mail|menu_settings|attendance|journal).
        /// 누르는 자리는 투명 판을 위아래로 넓혀 88 (M22). 아이콘 아트가 없으면 글자 버튼. 새 상단 바는 크기를 받는 overload를 쓴다.
        /// </summary>
        public static Button MenuButton(RectTransform parent, string name, string label, Vector2 position)
        {
            var size = LegacyMenuButtonSize;
            var iconArt = ArtLib.Icon(MenuIconName(label));
            if (iconArt == null) return TextMenuButton(parent, name, label, position, size, 28f);

            var hit = Img(parent, name, Color.clear, null, true);
            hit.rectTransform.At(TopRight, position, size);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            hit.gameObject.AddComponent<PressScale>();
            hit.HitPad(size, 0f, UISize.MinHit);

            UiArt.Icon(hit.transform, "Icon", iconArt, TopCenter, new Vector2(0, 4), 56);
            var text = Txt(hit.transform, "Label", label, 21, UITheme.Text, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor, 21f);
            text.rectTransform.Bottom(-8, 28, -6, -6);
            UiArt.Outline(text);
            return button;
        }

        /// <summary>
        /// 상단 바 메뉴 버튼 (M10, 기본 120x80 · TopRight 기준 position). 이름에 맞는 아이콘 아트가 있으면 IconMenuButton(아이콘 44 + 이름 20),
        /// 없으면 Secondary 버튼에 이름 30 굵게. 누르는 자리는 위아래로 88까지 (HitPad). MetaSceneBuilder.TopButton도 이것을 부른다.
        /// </summary>
        public static Button MenuButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var iconArt = ArtLib.Icon(MenuIconName(label));
            return iconArt != null
                ? IconMenuButton(parent, name, iconArt, label, position, size)
                : TextMenuButton(parent, name, label, position, size, UISize.MenuButtonText);
        }

        /// <summary>
        /// 아이콘 메뉴 버튼 (아이콘 이름으로): Art/UI/{iconKey}.png → Art/Icons/{iconKey}.png 순서로 찾고, 없으면 글자 버튼 (M10). size 기본 120x80.
        /// </summary>
        public static Button IconMenuButton(RectTransform parent, string name, string iconKey, string label, Vector2 position, Vector2? size = null)
        {
            Sprite icon = null;
            if (!string.IsNullOrEmpty(iconKey))
            {
                icon = ArtLib.UI(iconKey);
                if (icon == null) icon = ArtLib.Icon(iconKey);
            }
            return IconMenuButton(parent, name, icon, label, position, size ?? new Vector2(UISize.MenuButtonWidth, UISize.MenuButtonHeight));
        }

        /// <summary>
        /// 아이콘 메뉴 버튼 (M10): 투명 판(누르는 자리) + 아이콘(기본 44, 버튼 높이 80 기준 비례) Top(0, -6) + 이름 20 굵게 Bottom(0, 4).
        /// 바탕 판은 없다 — 상단 바 벨벳 위에 그대로. 누르는 자리는 위아래로 88까지. icon이 null이면 글자 버튼.
        /// </summary>
        public static Button IconMenuButton(RectTransform parent, string name, Sprite icon, string label, Vector2 position, Vector2 size)
        {
            if (icon == null) return TextMenuButton(parent, name, label, position, size, UISize.MenuButtonText);

            var hit = Img(parent, name, Color.clear, null, true);
            hit.rectTransform.At(TopRight, position, size);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            hit.gameObject.AddComponent<PressScale>();
            hit.HitPad(size, 0f, UISize.MinHit);

            float scale = size.y / UISize.MenuButtonHeight;
            float iconSize = Mathf.Clamp(Mathf.Round(UISize.MenuIcon * scale), 36f, 56f);
            UiArt.Icon(hit.transform, "Icon", icon, TopCenter, new Vector2(0, -6), iconSize);
            var text = Txt(hit.transform, "Label", label, UISize.MenuIconLabel, UITheme.Text, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor, UISize.MenuIconLabel);
            text.rectTransform.Bottom(4, 26, -6, -6);
            UiArt.Outline(text);
            return button;
        }

        /// <summary>아이콘이 없을 때의 글자 메뉴 버튼 (Secondary 그림, 이름 굵게)</summary>
        static Button TextMenuButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, float fontSize)
        {
            var button = Button(parent, name, label, ButtonStyle.Secondary, fontSize, out var text);
            ((RectTransform)button.transform).At(TopRight, position, size);
            var image = (Image)button.targetGraphic;
            if (image.type == Image.Type.Sliced && UiArt.IsArt(image.sprite)) image.pixelsPerUnitMultiplier = UiArt.ButtonPpu(size.y);
            text.FitFloor(UIType.Caption, fontSize);
            image.HitPad(size, 0f, UISize.MinHit);
            return button;
        }

        /// <summary>메뉴 이름 → 아이콘 이름 (Icons/)</summary>
        static string MenuIconName(string label) => label switch
        {
            "가방" => "menu_bag",
            "공지" => "menu_notice",
            "우편" => "menu_mail",
            "설정" => "menu_settings",
            "출석" or "출석부" => "attendance",
            "일지" or "수선 일지" => "journal",
            _ => "menu_" + label,
        };

        // ═════════════════════════ 빨간 점 · 배지 (M09 · M82) ═════════════════════════

        /// <summary>
        /// 숫자 빨간 배지 (ui_badge_red 40px + 흰 숫자 22 굵게, BadgeTextOutline 외곽선 0.2 — 자동 크기 16~22, '99+'도 들어간다). 배지와 글자는 누름을 받지 않는다.
        /// 자리: 부모(누르는 자리) 오른쪽 위 모서리에 앵커 · 피벗 (예전 기준 그대로), anchoredPosition (size/2 - 8, size/2 - 8)
        /// → 배지 중심이 모서리에서 안쪽으로 (-8, -8) (UISize.BadgeInset). 40px 배지면 anchoredPosition (12, 12).
        /// anchoredPosition만 바꿔 옮기는 호출부(던전 도전 버튼 (6, 6) · 원정 탭 (-30, -8) · UIKit.ClaimAllButton)가 이 오른쪽 위 피벗을 기준으로 자리를 잡았다:
        /// 중심 = anchoredPosition - size/2. 다른 기준이 필요하면 At(...)으로 앵커 · 피벗 · 크기를 함께 다시 잡는다.
        /// 점 모드(Badge.SetDot)에서는 숫자만 숨는다. NEW 알약(자식 "New", Gold 52x26 · Ink 17 굵게 — compactNew면 44x22 · 15)은 꺼 둔 채 만들어
        /// Badge._newPill(GameObject)이 있으면 연결한다. 알약은 빨간 원(배지 자신의 Image) 안에 있다 → Badge.SetNew(true)는 원 Image를 끄고(enabled = false)
        /// 숫자도 숨기며, SetDot · SetCount는 원을 다시 켜고 알약을 숨긴다 (원 위에 알약 · 지난 숫자가 겹치지 않게).
        /// 나타남(0 → 1.2 → 1) · 숨쉬기 크기 연출을 가운데 기준으로 하려면 Badge가 실행 중에 피벗만 가운데로 옮긴다 (자리 유지: anchoredPosition += ((0.5, 0.5) - pivot) × 크기).
        /// 상단 바 · 탭 · 바구니 · 설정 등 모든 빨간 점이 이것을 쓴다.
        /// </summary>
        public static Badge BuildBadge(Transform parent) => BuildBadge(parent, UISize.BadgeCount, false);

        /// <summary>BuildBadge (지름 size — 숫자가 들어가는 배지는 40보다 작게 하지 않는다). 피벗 · 자리 기준은 위와 같다.</summary>
        public static Badge BuildBadge(Transform parent, float size, bool compactNew = false)
        {
            size = Mathf.Max(size, UISize.BadgeCount);
            // 오른쪽 위 피벗 (예전 자리 기준 유지 — 위치만 바꾸는 호출부가 같은 자리에 그려진다): 중심 = 모서리에서 (-8, -8)
            float corner = size * 0.5f - UISize.BadgeInset;
            var dot = UiArt.Badge(parent, "Badge", TopRight, new Vector2(corner, corner), size, "0");

            var badge = dot.gameObject.AddComponent<Badge>();
            var count = dot.transform.Find("Count").GetComponent<TextMeshProUGUI>();
            count.color = Color.white;
            count.fontStyle |= FontStyles.Bold;
            count.fontSize = UISize.BadgeCountText;
            count.FitFloor(UIType.AutoSizeFloor, UISize.BadgeCountText);
            var outline = BadgeOutlineMaterial;
            if (outline != null) count.fontSharedMaterial = outline;
            count.raycastTarget = false;

            var newPill = BadgeNewPill(dot.transform, compactNew);
            new Binder(badge).Set("_countText", count).TrySet("_newPill", newPill.gameObject).Apply();
            return badge;
        }

        /// <summary>NEW 알약 (Gold 흰색 틴트용 알약 + Ink 굵게 "NEW", 배지 가운데, 꺼 둠, 누름 안 받음)</summary>
        static Image BadgeNewPill(Transform badge, bool compact)
        {
            var size = compact
                ? new Vector2(UISize.BadgeNewSmallWidth, UISize.BadgeNewSmallHeight)
                : new Vector2(UISize.BadgeNewWidth, UISize.BadgeNewHeight);
            float fontSize = compact ? UISize.BadgeNewSmallText : UISize.BadgeNewText;
            var pill = UiArt.Panel(badge, "New", UiArt.PillWhite9s, UiArt.CapsulePpu(size.y), UITheme.Gold, UITheme.Gold);
            pill.raycastTarget = false;
            pill.rectTransform.At(Center, Vector2.zero, size);
            var text = Txt(pill.transform, "Text", "NEW", fontSize, UITheme.Ink, TextAlignmentOptions.Center, true);
            text.rectTransform.Stretch(4, 4, 0, 0);
            text.raycastTarget = false;
            pill.gameObject.SetActive(false);
            return pill;
        }

        // ═════════════════════════ 재화 ═════════════════════════

        /// <summary>
        /// 상단 바 재화 칩 (어두운 바느질 알약, 캡슐 배율 = 96 / 높이): 아이콘 44 + 보유량 32 굵게 (숫자는 TopBarView가 쓴다).
        /// 안내 고리 (M59): 칩이 누름을 받고 CurrencyStripView(숫자 칸 없음)가 붙는다 — 누르면 재화 설명 · 쓰는 곳 · 얻는 곳(CurrencySources) 말풍선.
        /// 칩을 버튼으로 만들면(M08) 안내는 0.35초 길게 누르기로 바뀐다.
        /// </summary>
        public static TextMeshProUGUI CurrencyChip(RectTransform parent, string name, CurrencyType type, Vector2 position, Vector2 size)
        {
            // 어두운 바느질 알약 (캡슐 배율 = 96 / 높이)
            var chipImage = UiArt.Panel(parent, name, UiArt.PillNight9s, UiArt.CapsulePpu(size.y), null, UITheme.PanelLight, true);
            var chip = chipImage.rectTransform.At(TopRight, position, size);
            var icon = CurrencyIcon(chip, type, 44).rectTransform;
            icon.anchorMin = icon.anchorMax = MiddleLeft;
            icon.anchoredPosition = new Vector2(36, 0);
            var amount = Txt(chip, "Amount", "0", 32, UITheme.Text, TextAlignmentOptions.MidlineRight, true);
            amount.rectTransform.Stretch(64, 20, 0, 0);

            var hook = chip.gameObject.AddComponent<CurrencyStripView>();
            new Binder(hook).SetInt("_type", (int)type).Apply();
            return amount;
        }

        /// <summary>재화 아이콘. 아트가 없으면 별조각은 마름모, 나머지는 별 아이콘(Icons/star). LayoutElement 포함.</summary>
        public static Image CurrencyIcon(Transform parent, CurrencyType type, float size)
        {
            Image icon;
            var art = ArtLib.Currency(type);
            if (art != null)
            {
                icon = Img(parent, $"{type}Icon", Color.white, art);
                icon.preserveAspect = true;
            }
            else if (type == CurrencyType.Gem)
            {
                icon = Img(parent, "GemIcon", UITheme.Gem);
                icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                size *= 0.72f;
            }
            else
            {
                var star = UiArt.Star;
                icon = Img(parent, $"{type}Icon", UiArt.IsArt(star) ? Color.white : UITheme.CurrencyColor(type), star);
                icon.preserveAspect = true;
            }
            var layout = icon.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            return icon;
        }

        /// <summary>
        /// 비용 버튼 (예전 모양): 제목 32 (Top 24) + 재화 아이콘 34 · 비용 30 굵게 (Top 86, "Cost" 가로 줄). 바탕은 color 단색 — 부르는 쪽이 SkinButton/아트로 입힌다.
        /// 세 상태(넉넉함 · 부족함 · 쓸 수 없음)는 UIKit.CostBtn(button, costText, surface)로 붙인다.
        /// </summary>
        public static Button CostButton(Transform parent, string name, string title, CurrencyType currency, Color color,
            out TextMeshProUGUI titleText, out TextMeshProUGUI costText)
        {
            var button = Btn(parent, name, title, color, 32, out titleText);
            titleText.rectTransform.Top(24, 50, 8, 8);

            var line = NewRect("Cost", button.transform).Top(86, 50, 8, 8);
            var row = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 10;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            CurrencyIcon(line, currency, 34);
            costText = Txt(line, "CostText", "0", 30, UITheme.Text, TextAlignmentOptions.MidlineLeft, true);
            return button;
        }

        // ═════════════════════════ 격자 (M24) ═════════════════════════

        /// <summary>4열 격자 Grid4 (친구들 · 상점): 칸 240 x cellHeight, 간격 16, 좌우 36 → 1080 폭에서 첫 열 x 36</summary>
        public static GridLayoutGroup Grid4(RectTransform content, float cellHeight, float top = 6f, float bottom = 28f) =>
            AddGrid(content, new Vector2(UISpace.Grid4Cell, cellHeight), new Vector2(UISpace.Grid4Gap, UISpace.Grid4Gap), UISpace.Grid4Columns,
                new RectOffset((int)UISpace.Grid4Pad, (int)UISpace.Grid4Pad, Mathf.RoundToInt(top), Mathf.RoundToInt(bottom)));

        /// <summary>팝업 5열 격자 Grid5Popup (가방 164x204 · 보상 164x196): 칸 164, 가로 간격 17 → 본문 888을 꽉 채움</summary>
        public static GridLayoutGroup Grid5Popup(RectTransform content, float cellHeight, float rowGap = UISpace.Grid5PopupGap, float top = 6f, float bottom = 24f) =>
            AddGrid(content, new Vector2(UISpace.Grid5PopupCell, cellHeight), new Vector2(UISpace.Grid5PopupGap, rowGap), UISpace.Grid5PopupColumns,
                new RectOffset(0, 0, Mathf.RoundToInt(top), Mathf.RoundToInt(bottom)));

        // ═════════════════════════ 안내 말풍선 (M59) ═════════════════════════

        /// <summary>
        /// 안내 말풍선 InfoTooltipView를 layer(TooltipLayer — 팝업 위, 안전 영역) 아래 "InfoTooltip"으로 만든다. IdleSceneBuilder가 층을 만든 뒤 부른다.
        ///   InfoTooltip(전체, 뷰) / Catcher(화면 전체 + 노치 바깥 400px 투명 판, 누르면 닫힘, 꺼 둠) / Panel(밤 패널 그림 Panel@0.96 + 금테, 꺼 둠)
        ///   / Panel/Arrow(금색 꺾쇠 — ui_chevron 틴트, 없으면 작은 마름모) · Title(Cream 26 굵게) · Body(SubText 22, 3줄 …) · Source(Lavender 20)
        /// 크기 · 자리는 런타임이 글에 맞춰 정한다.
        /// </summary>
        public static InfoTooltipView BuildInfoTooltip(RectTransform layer)
        {
            var root = NewRect("InfoTooltip", layer).Stretch();
            var view = root.gameObject.AddComponent<InfoTooltipView>();

            var catcherImage = Img(root, "Catcher", Color.clear, null, true);
            catcherImage.rectTransform.Stretch(-400, -400, -400, -400);
            var catcher = catcherImage.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.navigation = new Navigation { mode = Navigation.Mode.None };
            catcherImage.gameObject.SetActive(false);

            var panel = UiArt.Panel(root, "Panel", UiArt.PanelNight9s, 2.4f, new Color(1f, 1f, 1f, 0.96f), UITheme.TooltipBack);
            panel.raycastTarget = false;
            panel.rectTransform.At(Center, Vector2.zero, new Vector2(UISize.TooltipMinWidth, 140));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var chevron = UiArt.Chevron;
            Image arrow;
            if (UiArt.IsArt(chevron))
            {
                arrow = Img(panel.transform, "Arrow", UITheme.GoldAccent, chevron);
                arrow.type = Image.Type.Simple;
                arrow.preserveAspect = true;
                arrow.rectTransform.At(Center, Vector2.zero, new Vector2(24, 18));
            }
            else
            {
                arrow = Img(panel.transform, "Arrow", UITheme.TooltipBack, WhiteSprite);
                arrow.rectTransform.At(Center, Vector2.zero, new Vector2(14, 14));
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            arrow.raycastTarget = false;

            var title = Txt(panel.transform, "Title", string.Empty, UIType.Body, UITheme.Cream, TextAlignmentOptions.TopLeft, true, true);
            var body = Txt(panel.transform, "Body", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true).Ellipsis();
            var source = Txt(panel.transform, "Source", string.Empty, UIType.Hint, UITheme.Lavender, TextAlignmentOptions.TopLeft, false, true);
            title.rectTransform.Top(UISize.TooltipPadY, 34, UISize.TooltipPadX, UISize.TooltipPadX);
            body.rectTransform.Top(UISize.TooltipPadY + 40, 32, UISize.TooltipPadX, UISize.TooltipPadX);
            source.rectTransform.Top(UISize.TooltipPadY + 78, 28, UISize.TooltipPadX, UISize.TooltipPadX);
            panel.gameObject.SetActive(false);

            new Binder(view)
                .Set("_panel", panel.rectTransform)
                .Set("_group", group)
                .Set("_title", title)
                .Set("_body", body)
                .Set("_source", source)
                .Set("_arrow", arrow.rectTransform)
                .Set("_catcher", catcher)
                .Apply();
            return view;
        }
    }
}
