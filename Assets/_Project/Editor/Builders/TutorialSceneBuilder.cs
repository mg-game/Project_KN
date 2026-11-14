using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.SceneParts;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 튜토리얼 & 팡파르 빌더 (담당: M4). IdleSceneBuilder가 팝업을 모두 만든 뒤, 토스트보다 먼저 부른다
    /// (그래서 Canvas에서 팝업 위, 토스트 아래에 놓인다).
    ///   BuildOverlays:
    ///     · 졸업 카드 팝업 "GraduationCard" (ctx.Popups에 추가) + 다른 화면의 개방 카드("FeatureUnlockCard")에 축하 연출.
    ///     · "TutorialOverlay" (하위 Canvas + GraphicRaycaster, FtueDirector):
    ///         첫 만남 카드(소환 탭 위) → 코치마크(구멍 뚫린 어둠·테두리·손가락·말풍선) → 동화 인트로 3장 (뒤로 갈수록 위).
    ///         UINav.FocusRequested(경로)와 Game.Tutorial 이벤트를 받는다. 대상은 Canvas 기준 Transform.Find(CoachTargets 경로).
    ///     · "FanfareLayer" (하위 Canvas, 입력 없음): 레벨업·진화 팡파르, 전투력 상승 숫자(안전 영역 위에서 230),
    ///         스테이지 클리어 보상 날아가기, 보상 날아가기(RewardFlyer: 출처 → 상단 재화 칩, 아이콘 최대 10개, 0.55~0.8초, 칩 1.15배 튐).
    ///       도착지 (UX-SPEC-IA §4.10 · §5.7): 단추 · 별조각 = ctx.TopBar 아래 "GoldChip"/"GemChip", 재료 = 상단 바 "MenuButton"(≡ — 가방은 메뉴 안),
    ///       별빛 사탕 = 빠른 아이콘 CoachTargets.EventButton (시트가 열려 흐려지면 대신 상점 탭 "Tab_ShopPanel", CR-03), 친구들 = "Tab_CharacterPanel".
    ///       틀(WP-IA)이 들어온 뒤로 예전 상단 바 이름("BagButton" · "EventChip")으로 잇는 대체 경로는 없다 — 찾지 못하면 RewardFlyer가 대체 도착지(상점 탭 · 단추 칩)로 보낸다.
    ///     · 코치마크의 대상 없는 말풍선 자리 = ctx.ContentArea(SHEET) · ctx.StageArea(MAIN)를 직접 잇는다 (M42 · UX-SPEC-IA §8.6).
    ///     · 동화 인트로: 안전 영역 오른쪽 위 [건너뛰기 ›] + 아래 안내 줄 띠 (M79).
    /// 버튼은 버튼 공장(SceneParts.SkinButton, M25), 자동 크기 글자는 하한 16(UIKit.FitFloor, M23)으로 만든다.
    /// 그림은 모두 Higgsfield 아트(TutorialArt 참고). 아트가 아직 없으면 경고를 남기고 단순 도형으로 대신한다.
    /// </summary>
    internal static partial class TutorialSceneBuilder
    {
        static readonly Color DimColor = new(0.04f, 0.02f, 0.1f, 0f);

        public static void BuildOverlays(SceneBuildContext ctx)
        {
            var art = TutorialArt.Load();

            var graduation = BuildGraduationCard(ctx, art);
            ctx.Popups.Add(graduation);
            graduation.gameObject.SetActive(false);
            AttachUnlockCelebrations(ctx, art);

            BuildTutorialOverlay(ctx, art);
            BuildFanfareLayer(ctx, art);
        }

        // ═════════════════════════ 튜토리얼 덮개 ═════════════════════════

        static void BuildTutorialOverlay(SceneBuildContext ctx, TutorialArt art)
        {
            var root = NewRect("TutorialOverlay", ctx.Canvas).Stretch();
            // 하위 Canvas: 손가락·말풍선이 매 프레임 움직여도 게임 UI 전체를 다시 묶지 않게 한다
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            var director = root.gameObject.AddComponent<FtueDirector>();

            var firstMeeting = BuildFirstMeeting(root, ctx, art);
            var coach = BuildCoachMark(root, ctx, art);
            var intro = BuildStoryIntro(root, art);

            new Binder(director)
                .Set("_canvas", ctx.Canvas)
                .Set("_popupRoot", ctx.PopupRoot)
                .Set("_tabs", ctx.TabController)
                .Set("_coach", coach)
                .Set("_intro", intro)
                .Set("_firstMeeting", firstMeeting)
                .Set("_speakerPortrait", art.Jjak)
                .Set("_raysSprite", art.Rays)
                .Set("_sparkSprite", art.Spark)
                .SetArray("_confettiSprites", art.Confetti)
                .Apply();
        }

        static CoachMarkView BuildCoachMark(RectTransform parent, SceneBuildContext ctx, TutorialArt art)
        {
            var root = NewRect("CoachMark", parent).Stretch();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var view = root.gameObject.AddComponent<CoachMarkView>();

            var dimRect = NewRect("Dim", root).Stretch();
            var dim = dimRect.gameObject.AddComponent<HoleDimGraphic>();
            dim.color = DimColor;
            dim.raycastTarget = true;

            // 바느질 점선 테두리 (흰 크림 아트, Lamp로 물들이고 런타임이 알파를 숨 쉬듯 바꾼다)
            var highlight = Img(root, "Highlight", WithAlpha(UITheme.Lamp, 0.9f), art.Outline);
            highlight.type = Image.Type.Sliced;
            // 아트가 없을 때의 둥근 사각형은 테두리만 그린다 (대상을 가리지 않게)
            highlight.fillCenter = UiArt.IsArt(art.Outline);
            highlight.rectTransform.At(Center, Vector2.zero, new Vector2(200, 200));

            var finger = BuildFinger(root, art);
            var bubble = BuildBubble(root, art);

            // 대상 없는 말풍선 자리 (M42 · UX-SPEC-IA §8.6): SHEET = 탭 화면 위쪽, MAIN = 전투 영역 아래쪽 (가이드 퀘스트 줄 위).
            // 경로 찾기 없이 직접 잇는다 (ContentArea는 MAIN에서 꺼져 있어 런타임 찾기가 실패할 수 있다)
            new Binder(view)
                .Set("_group", group)
                .Set("_dim", dim)
                .Set("_highlight", highlight)
                .Set("_finger", finger)
                .Set("_bubble", bubble)
                .Set("_safeArea", ctx.SafeArea)
                .Set("_freeAnchor", ctx.ContentArea)
                .Set("_stageAnchor", ctx.StageArea)
                .Apply();
            return view;
        }

        static FingerPointerView BuildFinger(RectTransform parent, TutorialArt art)
        {
            var holder = NewRect("Finger", parent).Stretch();
            var view = holder.gameObject.AddComponent<FingerPointerView>();

            // 누르는 순간 퍼지는 별 매듭 고리 (흰색 아트, 크림으로 물들인다)
            var ripple = Img(holder, "Ripple", WithAlpha(UITheme.Cream, 0f), art.Ripple);
            ripple.preserveAspect = true;
            ripple.rectTransform.At(Center, Vector2.zero, new Vector2(170, 170));
            // 끌기 안내 별똥 꼬리 (머리가 오른쪽 = 손끝 쪽. 길이는 런타임이 그림 비율 안에서 정한다)
            var trail = Img(holder, "Trail", WithAlpha(UITheme.Lamp, 0f), art.Trail);
            trail.rectTransform.At(Center, Vector2.zero, new Vector2(160, 40));

            var hand = Img(holder, "Hand", Color.white, art.Finger);
            hand.preserveAspect = true;
            var rt = hand.rectTransform;
            rt.anchorMin = rt.anchorMax = Center;
            // 피벗 = 손끝 (tutorial_finger 256x256: 둥근 손끝이 x 128, 위에서 3px)
            rt.pivot = new Vector2(0.5f, 0.985f);
            rt.sizeDelta = new Vector2(150, 150);

            new Binder(view)
                .Set("_finger", rt)
                .Set("_fingerImage", hand)
                .Set("_ripple", ripple)
                .Set("_trail", trail)
                .Apply();
            return view;
        }

        static SpeechBubbleView BuildBubble(RectTransform parent, TutorialArt art)
        {
            var holder = NewRect("Bubble", parent).Stretch();
            var view = holder.gameObject.AddComponent<SpeechBubbleView>();

            // 카드 720 너비, 높이는 런타임이 글에 맞춘다 (150~220, SpeechBubbleView.CardHeight — 빌드 때는 가장 낮은 150)
            const float cardHeight = SpeechBubbleView.MinHeight;
            float tailY = -(cardHeight * 0.5f - 6f);
            var card = NewRect("Card", holder).At(Center, Vector2.zero, new Vector2(720, cardHeight));
            var group = card.gameObject.AddComponent<CanvasGroup>();
            Image tail = null;
            if (art.BubbleTail == null)
            {
                // 아트가 없으면 마름모 꼬리를 카드 뒤에 깔아 변 밖으로 반만 내민다
                tail = Img(card, "Tail", UITheme.Parchment);
                tail.rectTransform.At(Center, new Vector2(0, tailY), new Vector2(42, 42));
                tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            var background = ParchmentCard(card, "Background", true);
            background.rectTransform.Stretch();
            background.pixelsPerUnitMultiplier = 1.6f;
            if (art.BubbleTail != null)
            {
                // 꼬리(아래를 가리킴)는 카드 앞에 놓여 아래 변 금테를 덮는다 (꼬리 위쪽 변에는 외곽선이 없다).
                // 피벗 = 위쪽 가운데. 대상이 위에 있으면 SpeechBubbleView가 위아래로 뒤집는다
                tail = Img(card, "Tail", Color.white, art.BubbleTail);
                tail.preserveAspect = true;
                tail.rectTransform.At(Center, new Vector2(0, tailY), new Vector2(56, 56));
                tail.rectTransform.pivot = new Vector2(0.5f, 1f);
            }

            // 얼굴 접시 · 얼굴 그림은 196 높이 카드에 맞춘 자리 (SpeechBubbleView.FaceDesignHeight, 카드 왼쪽 가운데 기준).
            // 그보다 낮은 카드(150~)는 런타임이 같은 기준점으로 자리 · 크기를 비율대로 줄인다 (FitFace — 접시가 위 변을 넘지 않게)
            var plate = Plate(card, "PortraitBg", art);
            plate.rectTransform.At(MiddleLeft, new Vector2(20, 4), new Vector2(146, 146));
            var portrait = Picture(card, "Portrait", art.Jjak, Vector2.zero, new Vector2(176, 176));
            portrait.rectTransform.At(MiddleLeft, new Vector2(4, 16), new Vector2(176, 176));

            // 이름표: 보라 버튼 아트 (48 높이 → ppu 3)
            var nameTag = SlicedArt(card, "NameTag", "button_primary_9s", 3f, UITheme.Primary);
            nameTag.rectTransform.At(TopLeft, new Vector2(150, 24), new Vector2(150, 48));
            var name = Txt(nameTag.transform, "NameText", "짝짝이", 26, UITheme.Cream, TextAlignmentOptions.Center, true);
            name.rectTransform.Stretch();

            // 글 30 고정 (M42 — 자동 크기를 켜면 카드 높이를 글에 맞출 때 글 높이가 다시 글자 크기를 바꾼다), 줄 간격 4.
            // 여백 위 30(이름표 아래) · 아래 22 — 카드 높이 = 글 높이 + 56 (SpeechBubbleView.TextPad)
            var text = Txt(card, "Text", string.Empty, 30, UITheme.Ink, TextAlignmentOptions.MidlineLeft, false, true);
            text.rectTransform.Stretch(190, 34, 30, 22);
            text.lineSpacing = 4;
            text.enableAutoSizing = false;

            new Binder(view)
                .Set("_root", card)
                .Set("_group", group)
                .Set("_text", text)
                .Set("_nameText", name)
                .Set("_portrait", portrait)
                .Set("_portraitPlate", plate.rectTransform)
                .Set("_tail", tail.rectTransform)
                .Apply();
            return view;
        }

        static FirstMeetingCardView BuildFirstMeeting(RectTransform parent, SceneBuildContext ctx, TutorialArt art)
        {
            var holder = NewRect("FirstMeeting", parent).Stretch();
            var view = holder.gameObject.AddComponent<FirstMeetingCardView>();

            // 덮개는 탭 바 위에서만 그린다 (UX-SPEC-IA §5.7): 소환 시트는 탭 바 뒤에서 0.30초 동안 올라오고 카드가 그 자리를 따라가므로,
            // 안전 영역에서 아래 탭 바 높이만큼 잘라 둔다 (RectMask2D — 잘린 곳은 그리지도 누름을 막지도 않는다). 다 열린 자리(ContentArea)는 잘리지 않는다
            var coverSafe = NewRect("CoverSafe", holder).Stretch();
            coverSafe.gameObject.AddComponent<SafeArea>();
            var coverClip = NewRect("CoverClip", coverSafe).Stretch(0, 0, 0, SceneBuildContext.TabBarHeight);
            coverClip.gameObject.AddComponent<RectMask2D>();

            // 탭 패널 자리를 덮는 덮개 (런타임에 ContentArea 크기로 맞춘다). 거의 불투명 (FirstMeetingCardView.CoverAlpha 0.94) —
            // 카드(860) 양옆으로 소환 시트 글이 비쳐 어수선하지 않게 (WP-8 r1)
            var cover = Img(coverClip, "Cover", new Color(0.04f, 0.02f, 0.1f, FirstMeetingCardView.CoverAlpha), null, true);
            cover.rectTransform.At(Center, Vector2.zero, new Vector2(1080, SceneBuildContext.ContentHeight));
            var group = cover.gameObject.AddComponent<CanvasGroup>();

            var cardImage = ParchmentCard(cover.transform, "Card", true);
            cardImage.pixelsPerUnitMultiplier = 1.6f;
            var card = cardImage.rectTransform;
            card.At(Center, Vector2.zero, new Vector2(860, 700));
            Txt(card, "TitleText", "첫 만남", 46, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Top(28, 64);
            var glow = Img(card, "Glow", WithAlpha(UITheme.Lavender, 0.7f), art.Glow);
            glow.rectTransform.At(TopCenter, new Vector2(0, -96), new Vector2(420, 420));
            var silhouette = Picture(card, "Silhouette", ArtLib.Character("udu_base"), Vector2.zero, new Vector2(300, 300));
            silhouette.rectTransform.At(TopCenter, new Vector2(0, -104), new Vector2(300, 300));
            silhouette.color = new Color(0.1f, 0.07f, 0.22f, 0.92f);
            var hint = Txt(card, "HintText", "기다림 끝에 찾아온 친구", 24, UITheme.InkSub, TextAlignmentOptions.Center, true);
            hint.rectTransform.At(TopCenter, new Vector2(0, -408), new Vector2(760, 38));
            Txt(card, "BodyText", "누군가 수선점 문을 두드려요…", 32, UITheme.Ink, TextAlignmentOptions.Center, true)
                .rectTransform.At(TopCenter, new Vector2(0, -450), new Vector2(780, 50));
            var open = GoldButton(card, "OpenButton", "문 열어 주기 (무료)", new Vector2(540, 108));
            ((RectTransform)open.transform).At(BottomCenter, new Vector2(0, 66), new Vector2(540, 108));
            Txt(card, "NoteText", "첫 만남 확정 소환 · 공개 확률과 천장 계산에 넣지 않아요", 20, UITheme.InkSub, TextAlignmentOptions.Center)
                .rectTransform.Bottom(20, 36, 30, 30);

            // 결과 팝업 창 위쪽 변에 걸리는 금색 리본 (글자 두 줄은 리본 앞면 안에)
            const float ribbonHeight = 124f;
            var ribbon = PillBackground(holder, "ResultRibbon", PillStyle.GoldRibbon, ribbonHeight);
            ribbon.rectTransform.At(Center, new Vector2(0, 700), new Vector2(660, ribbonHeight));
            var ribbonGroup = ribbon.gameObject.AddComponent<CanvasGroup>();
            ribbonGroup.alpha = 0f;
            ribbonGroup.blocksRaycasts = false;
            var ribbonFace = PillFace(ribbon, PillStyle.GoldRibbon, ribbonHeight);
            Txt(ribbonFace, "TitleText", "첫 만남 확정 소환", 34, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Top(0, 48);
            Txt(ribbonFace, "SubText", "공개 확률과 천장 계산에 넣지 않아요", 20, UITheme.Ink, TextAlignmentOptions.Center).rectTransform.Top(46, 30);

            new Binder(view)
                .Set("_root", cover.rectTransform)
                .Set("_group", group)
                .Set("_card", card)
                .Set("_silhouette", silhouette)
                .Set("_glow", glow)
                .Set("_hintText", hint)
                .Set("_openButton", open)
                .Set("_coverArea", ctx.ContentArea)
                .Set("_ribbon", ribbon.rectTransform)
                .Set("_ribbonGroup", ribbonGroup)
                .Apply();
            return view;
        }

        // ═════════════════════════ 동화 인트로 ═════════════════════════

        static StoryIntroView BuildStoryIntro(RectTransform parent, TutorialArt art)
        {
            var root = Img(parent, "StoryIntro", new Color(0.03f, 0.02f, 0.08f, 1f), null, true);
            root.rectTransform.Stretch();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<StoryIntroView>();

            var pages = new Object[3];
            var arts = new Object[3];
            var glows = new Object[3];
            var bobs = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var page = NewRect($"Page{i + 1}", root.rectTransform).Stretch();
                var pageGroup = page.gameObject.AddComponent<CanvasGroup>();
                pageGroup.alpha = 0f;
                pageGroup.blocksRaycasts = false;
                pages[i] = pageGroup;
                var artRoot = NewRect("Art", page).Stretch();
                arts[i] = artRoot;

                var story = ArtLib.Load($"Story/intro_{i + 1}.png");
                if (story != null)
                {
                    Backdrop(artRoot, "Picture", story, Color.white, IntroKeep);
                    var warm = Img(artRoot, "Glow", WithAlpha(UITheme.Lamp, 0f), art.Glow);
                    warm.rectTransform.At(Center, new Vector2(0, 120), new Vector2(900, 900));
                    glows[i] = warm;
                }
                else
                {
                    ComposePage(i, artRoot, art, out var glow, out var bob);
                    glows[i] = glow;
                    bobs[i] = bob;
                }
            }

            // 글 띠 (밤 패널 아트, 살짝 비친다) + 장 표시 + 안내
            var band = SlicedArt(root.rectTransform, "CaptionBand", "panel_night_9s", 1.6f, new Color(0.06f, 0.04f, 0.14f, 0.8f));
            if (UiArt.IsArt(band.sprite)) band.color = WithAlpha(Color.white, 0.92f);
            band.rectTransform.Bottom(250, 200, 50, 50);
            var caption = Txt(band.transform, "Caption", string.Empty, 38, UITheme.Cream, TextAlignmentOptions.Center, false, true);
            caption.rectTransform.Stretch(36, 36, 18, 18);
            caption.lineSpacing = 6;
            caption.FitFloor(26, 38);

            // 장 표시: 흰 단추 (색·알파·크기는 StoryIntroView가 바꾼다)
            var dotsRow = NewRect("Dots", root.rectTransform).At(BottomCenter, new Vector2(0, 204), new Vector2(180, 32));
            var dots = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var dot = Img(dotsRow, $"Dot{i}", WithAlpha(Color.white, 0.35f), art.Pip);
                dot.preserveAspect = true;
                dot.rectTransform.At(Center, new Vector2((i - 1) * 44, 0), new Vector2(26, 26));
                dots[i] = dot;
            }
            // 안내 줄 (M79): 아래에서 140, 360x44 밤 띠(0.06,0.04,0.14,0.45) 위 28 Cream — 밝은 등불 그림 위에서도 읽힌다.
            // 띠째(CanvasGroup) 글이 다 나온 뒤 0.3초 동안 나타나 숨 쉰다 (StoryIntroView). 장 표시 점(204~236)과 겹치지 않는다
            var hintBand = Img(root.rectTransform, "HintBand", new Color(0.06f, 0.04f, 0.14f, 0.45f), RoundSprite);
            hintBand.rectTransform.At(BottomCenter, new Vector2(0, 140), new Vector2(360, 44));
            var hintGroup = hintBand.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintGroup.interactable = false;
            hintGroup.blocksRaycasts = false;
            var hint = Txt(hintBand.transform, "Hint", "화면을 눌러 주세요  ›", 28, UITheme.Cream, TextAlignmentOptions.Center);
            hint.rectTransform.Stretch();

            // 건너뛰기 (M79): 안전 영역 틀 "Safe" 안 오른쪽 위 (노치 · 상태 바 밑에 숨지 않게) 196x64, 피벗 (1,1).
            // 밤 알약 (0.06,0.04,0.14,0.6) + '건너뛰기 ›' 26 Cream@0.9. 전용 CanvasGroup — 연 지 1초 뒤 0.3초 동안 나타나고 반쯤 보여야 눌린다.
            // 누르는 자리는 위아래로 넓혀 96 (M22). 맨 뒤 형제(맨 위)라 화면 전체 누름(다음 장)보다 먼저 받는다
            var safe = NewRect("Safe", root.rectTransform).Stretch();
            safe.gameObject.AddComponent<SafeArea>();
            var skip = Btn(safe, "Skip", "건너뛰기 ›", new Color(0.06f, 0.04f, 0.14f, 0.6f), 26, out var skipLabel);
            ((RectTransform)skip.transform).At(TopRight, new Vector2(-36, -44), new Vector2(196, 64));
            skipLabel.color = WithAlpha(UITheme.Cream, 0.9f);
            ((Image)skip.targetGraphic).HitPad();
            var skipGroup = skip.gameObject.AddComponent<CanvasGroup>();
            skipGroup.alpha = 0f;
            skipGroup.interactable = false;
            skipGroup.blocksRaycasts = false;

            new Binder(view)
                .Set("_group", group)
                .SetArray("_pages", pages)
                .SetArray("_pageArt", arts)
                .SetArray("_pageGlows", glows)
                .SetArray("_pageBobs", bobs)
                .Set("_caption", caption)
                .SetArray("_dots", dots)
                .Set("_hint", hint)
                .Set("_hintGroup", hintGroup)
                .Set("_skipButton", skip)
                .Set("_skipGroup", skipGroup)
                .Apply();
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// 동화 그림(Story/intro_n)이 아직 없을 때 기존 아트로 짠 장면:
        /// ① 밤하늘 아래 골목으로 모여드는 잊힌 친구들 ② 수선점에 불이 켜짐 ③ 짝짝이가 문을 열고 맞이함.
        /// </summary>
        static void ComposePage(int index, RectTransform root, TutorialArt art, out Image glow, out RectTransform bob)
        {
            bob = null;
            switch (index)
            {
                case 0:
                {
                    Backdrop(root, "Sky", art.Night, Color.white);
                    // 친구들이 모이는 자리에 은은한 빛
                    glow = Img(root, "GatherGlow", WithAlpha(UITheme.Cream, 0f), art.Glow);
                    glow.preserveAspect = true;
                    glow.rectTransform.At(Center, new Vector2(0, -240), new Vector2(900, 900));
                    // 골목으로 흘러드는 잊힌 친구들 (제 색 그대로)
                    string[] friends = { "ttok_base", "chal_base", "mong_base", "rouge_base" };
                    for (int i = 0; i < friends.Length; i++)
                    {
                        var position = new Vector2(-330 + i * 220, -260 + (i % 2) * 40);
                        Img(root, $"Wisp{i}", WithAlpha(UITheme.Lavender, 0.35f), art.Glow).rectTransform.At(Center, position, new Vector2(260, 260));
                        Picture(root, $"Friend{i}", ArtLib.Character(friends[i]), position, new Vector2(190, 190));
                    }
                    break;
                }
                case 1:
                {
                    Backdrop(root, "Sky", art.Night, new Color(0.7f, 0.68f, 0.82f));
                    glow = Img(root, "ShopGlow", WithAlpha(UITheme.Lamp, 0f), art.Glow);
                    glow.rectTransform.At(Center, new Vector2(0, 160), new Vector2(1000, 1000));
                    if (art.Shop != null) Picture(root, "Shop", art.Shop, new Vector2(0, 160), new Vector2(760, 760));
                    else BuildHouse(root, "Shop", new Vector2(0, 160), new Vector2(520, 560));
                    break;
                }
                default:
                {
                    Backdrop(root, "Sky", art.Night, new Color(0.78f, 0.76f, 0.88f));
                    Img(root, "ShopGlow", WithAlpha(UITheme.Lamp, 0.6f), art.Glow).rectTransform.At(Center, new Vector2(0, 360), new Vector2(980, 980));
                    if (art.Shop != null) Picture(root, "Shop", art.Shop, new Vector2(0, 360), new Vector2(820, 820));
                    else BuildHouse(root, "Shop", new Vector2(0, 360), new Vector2(560, 600));
                    glow = Img(root, "DoorGlow", WithAlpha(UITheme.Cream, 0f), art.Glow);
                    glow.rectTransform.At(Center, new Vector2(0, -140), new Vector2(620, 620));
                    var jjak = Picture(root, "Jjakjjak", art.Jjak, new Vector2(0, -150), new Vector2(480, 480));
                    bob = jjak.rectTransform;
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = (i * 72 + 20) * Mathf.Deg2Rad;
                        var spark = Img(root, $"Spark{i}", WithAlpha(UITheme.Cream, 0.85f), art.Spark);
                        spark.preserveAspect = true;
                        spark.rectTransform.At(Center, new Vector2(Mathf.Cos(angle) * 300, -150 + Mathf.Sin(angle) * 260), new Vector2(46, 46));
                    }
                    break;
                }
            }
        }

        /// <summary>수선점 그림(Battle/shop)이 없을 때만 쓰는 집 모양 자리표시 (불 켜진 창 + 문)</summary>
        static void BuildHouse(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var house = Img(parent, name, UITheme.Ground, RoundSprite);
            house.rectTransform.At(Center, position, size);
            Img(house.transform, "Roof", UITheme.Secondary, RoundSprite).rectTransform.Top(-40, 140, -40, -40);
            Img(house.transform, "Window", UITheme.Lamp, RoundSprite).rectTransform.At(Center, new Vector2(-120, 60), new Vector2(120, 120));
            Img(house.transform, "Door", UITheme.DustyPink, RoundSprite).rectTransform.At(BottomCenter, new Vector2(60, 0), new Vector2(150, 230));
        }

        // ═════════════════════════ 졸업 카드 / 개방 카드 축하 ═════════════════════════

        static GraduationCardPopup BuildGraduationCard(SceneBuildContext ctx, TutorialArt art)
        {
            var popup = BuildPopup<GraduationCardPopup>(ctx.PopupRoot, "GraduationCard", "수선점 졸업", new Vector2(920, 1240), out var body, out _);
            var window = popup.transform.Find("Window") as RectTransform;

            // 금색 리본 제목 (글자는 리본 앞면 안에)
            const float ribbonHeight = 104f;
            var ribbon = PillBackground(body, "Ribbon", PillStyle.GoldRibbon, ribbonHeight);
            ribbon.rectTransform.Top(4, ribbonHeight, 30, 30);
            var title = Txt(PillFace(ribbon, PillStyle.GoldRibbon, ribbonHeight), "TitleText", "이제 수선점 주인이에요!", 44, UITheme.Ink,
                TextAlignmentOptions.Center, true);
            title.rectTransform.Stretch();
            title.FitFloor(30, 44);

            // 수선점과 함께한 친구들
            var stage = NewRect("Stage", body).Top(116, 470);
            var stageGlow = Img(stage, "Glow", WithAlpha(UITheme.Lamp, 0.55f), art.Glow);
            stageGlow.preserveAspect = true;
            stageGlow.rectTransform.At(Center, new Vector2(0, 40), new Vector2(640, 640));
            if (art.Shop != null) Picture(stage, "Shop", art.Shop, new Vector2(0, 70), new Vector2(420, 420));
            else BuildHouse(stage, "Shop", new Vector2(0, 70), new Vector2(300, 320));
            // 친구 자리 5칸: 초상화는 런타임이 채운다 (그림이 없으면 꺼 둔다)
            var friends = new Object[5];
            for (int i = 0; i < friends.Length; i++)
            {
                var friend = Picture(stage, $"Friend{i}", null, new Vector2((i - 2) * 158, -150), new Vector2(150, 150));
                friends[i] = friend;
            }

            Pill(body, "TitlePill", "칭호 · 수선점 주인", 26, PillStyle.LavenderRibbon, UITheme.Ink, Vector2.zero, new Vector2(420, 62), out _)
                .rectTransform.At(TopCenter, new Vector2(0, -600), new Vector2(420, 62));
            var text = Txt(body, "BodyText", "첫 10분을 함께해 줘서 고마워요.\n이제 친구들과 매일 수선점을 지켜 주세요.", 30, UITheme.Cream,
                TextAlignmentOptions.Center, false, true);
            text.rectTransform.Top(666, 100);
            text.lineSpacing = 6;

            // 보상 (최대 4칸)
            var row = NewRect("Rewards", body).Top(782, 140);
            var slots = new Object[4];
            var icons = new Object[4];
            var amounts = new Object[4];
            bool hasFrame = UiArt.IsArt(UiArt.UI("card_frame_9s", null));
            for (int i = 0; i < slots.Length; i++)
            {
                // 보상 칸: 안쪽 패널 + 금테 카드 틀
                var slot = SlicedArt(row, $"Reward{i}", "ui_panel_inset_9s", 2f, WithAlpha(UITheme.PanelLight, 0.9f));
                slot.rectTransform.At(Center, new Vector2((i - 1.5f) * 200, 0), new Vector2(186, 136));
                if (hasFrame) SlicedArt(slot.transform, "Frame", "card_frame_9s", 2.2f, Color.clear).rectTransform.Stretch();
                var icon = Img(slot.transform, "Icon", Color.white, art.Gem);
                icon.preserveAspect = true;
                icon.rectTransform.At(TopCenter, new Vector2(0, -10), new Vector2(72, 72));
                // 22 굵게, 자동 크기 하한 16 (M23 — 14에서 올림). 칸 안 글 영역 174x40: 16이면 한글 10자 남짓까지 한 줄, 넘치면 … (Ellipsis)
                var amount = Txt(slot.transform, "Amount", "별조각 300", 22, UITheme.Gold, TextAlignmentOptions.Center, true).Ellipsis();
                amount.rectTransform.Bottom(8, 40, 6, 6);
                amount.FitFloor(UIType.AutoSizeFloor, 22);
                slots[i] = slot.gameObject;
                icons[i] = icon;
                amounts[i] = amount;
            }

            var confirm = GoldButton(body, "ConfirmButton", "좋아요!", new Vector2(480, 116), 40);
            ((RectTransform)confirm.transform).At(BottomCenter, new Vector2(0, 6), new Vector2(480, 116));

            var celebration = popup.gameObject.AddComponent<CelebrationFx>();
            new Binder(celebration)
                .Set("_raysSprite", art.Rays)
                .Set("_sparkSprite", art.Spark)
                .SetArray("_confettiSprites", art.Confetti)
                .Set("_anchor", window)
                .Apply();
            SetBool(celebration, "_playOnEnable", false);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_bodyText", text)
                .SetArray("_rewardSlots", slots)
                .SetArray("_rewardIcons", icons)
                .SetArray("_rewardTexts", amounts)
                .SetArray("_friends", friends)
                .Set("_confirmButton", confirm)
                .Set("_celebration", celebration)
                .Set("_goldIcon", art.Gold)
                .Set("_gemIcon", art.Gem)
                .Set("_eventIcon", art.EventToken)
                .Set("_lineageXpIcon", art.LineageXp)
                .Set("_ticketIcon", art.QuickTicket)
                .Set("_shardsIcon", art.Shards)
                .Apply();
            return popup;
        }

        /// <summary>메타 UI(M3)의 기능 개방 카드에 빛살·별빛·색종이를 붙인다 (카드가 열릴 때마다 재생). 없으면 런타임에 FtueDirector가 다시 찾는다</summary>
        static void AttachUnlockCelebrations(SceneBuildContext ctx, TutorialArt art)
        {
            foreach (var name in new[] { "FeatureUnlockCard", "FeatureUnlockPopup" })
            {
                var card = ctx.PopupRoot.Find(name);
                if (card == null || card.GetComponent<CelebrationFx>() != null) continue;
                var fx = card.gameObject.AddComponent<CelebrationFx>();
                new Binder(fx).Set("_raysSprite", art.Rays).Set("_sparkSprite", art.Spark).SetArray("_confettiSprites", art.Confetti).Apply();
            }
        }

        // ═════════════════════════ 팡파르 층 ═════════════════════════

        static void BuildFanfareLayer(SceneBuildContext ctx, TutorialArt art)
        {
            var root = NewRect("FanfareLayer", ctx.Canvas).Stretch();
            // 하위 Canvas (입력 없음): 아이콘이 날아가는 동안 게임 UI 전체를 다시 묶지 않게
            root.gameObject.AddComponent<Canvas>();

            var stageFlyer = root.gameObject.AddComponent<StageClearRewardFlyer>();
            new Binder(stageFlyer).Set("_battleRoot", ctx.BattleRoot).Apply();

            BuildLevelUpFanfare(root, ctx, art);
            BuildPowerUpFloat(root, ctx, art);
            BuildRewardFlyer(root, ctx, art);
        }

        static void BuildRewardFlyer(RectTransform parent, SceneBuildContext ctx, TutorialArt art)
        {
            var layer = NewRect("RewardFlyer", parent).Stretch();
            var flyer = layer.gameObject.AddComponent<RewardFlyer>();

            var icon = Img(layer, "IconTemplate", Color.white, art.Star);
            icon.preserveAspect = true;
            icon.rectTransform.At(Center, Vector2.zero, new Vector2(64, 64));
            var spark = Img(layer, "SparkTemplate", Color.white, art.Spark);
            spark.preserveAspect = true;
            spark.rectTransform.At(Center, Vector2.zero, new Vector2(32, 32));
            var count = Txt(layer, "CountTemplate", "+0", 34, UITheme.Gold, TextAlignmentOptions.Center, true);
            count.rectTransform.At(Center, Vector2.zero, new Vector2(360, 56));
            count.gameObject.AddComponent<CanvasGroup>();

            var top = ctx.TopBar;
            // 별빛 사탕: 전투 화면 빠른 아이콘 (메타 HUD가 먼저 만든다). 시트가 열려 흐려지면 RewardFlyer가 상점 탭으로 바꾼다
            var eventTarget = ctx.Canvas.Find(CoachTargets.EventButton);
            // 재료: ≡ 메뉴 버튼 (가방 · 공지 · 설정이 메뉴 안으로 갔다)
            var bagTarget = top != null ? top.Find("MenuButton") : null;
            // 실타래 (T10): 강화 탭 친구 머리 줄 잔액 칩 → 강화 탭 버튼 꼬리표 (둘 다 안 보이면 RewardFlyer가 받침만 띄운다)
            var growthPanel = ctx.Canvas.GetComponentInChildren<GrowthPanel>(true);
            var lineageChip = growthPanel != null ? growthPanel.transform.Find("LineageXpChip") as RectTransform : null;
            var growthTab = TabButtonRect(ctx, MainTab.Growth);
            var lineageTag = growthTab != null ? growthTab.Find("LineageXpTag/Pill") as RectTransform : null;
            if (lineageChip == null || lineageTag == null)
                Debug.LogWarning("[TutorialSceneBuilder] 실타래 날아가기 도착지(GrowthPanel/LineageXpChip · Tab_GrowthPanel/LineageXpTag/Pill)를 찾지 못했어요");
            new Binder(flyer)
                .Set("_canvasRoot", ctx.Canvas)
                .Set("_iconTemplate", icon)
                .Set("_sparkTemplate", spark)
                .Set("_countTemplate", count)
                .Set("_goldTarget", top != null ? top.Find("GoldChip") : null)
                .Set("_gemTarget", top != null ? top.Find("GemChip") : null)
                .Set("_eventTarget", eventTarget)
                .Set("_eventFallbackTarget", TabButtonRect(ctx, MainTab.Shop))
                .Set("_bagTarget", bagTarget)
                .Set("_friendsTarget", TabButtonRect(ctx, MainTab.Characters))
                .Set("_lineageXpChipTarget", lineageChip)
                .Set("_lineageXpTagTarget", lineageTag)
                .Set("_goldIcon", art.Gold)
                .Set("_gemIcon", art.Gem)
                .Set("_eventIcon", art.EventToken)
                .Set("_lineageXpIcon", art.LineageXp)
                .Set("_quickTicketIcon", art.QuickTicket)
                .Set("_doubleTicketIcon", art.DoubleTicket)
                .Set("_selectShardsIcon", art.Shards)
                .Set("_fallbackIcon", art.Star)
                .Apply();
        }

        /// <summary>하단 탭 버튼 "Tab_{패널}" (ctx.TabButtons, 순서 = MainTab — 가운데 사냥 버튼은 들어 있지 않다). 탭 바 전이면 null</summary>
        static RectTransform TabButtonRect(SceneBuildContext ctx, MainTab tab)
        {
            int index = (int)tab;
            var buttons = ctx.TabButtons;
            return buttons != null && index < buttons.Length && buttons[index] != null ? (RectTransform)buttons[index].transform : null;
        }

        static void BuildPowerUpFloat(RectTransform parent, SceneBuildContext ctx, TutorialArt art)
        {
            // 안전 영역을 따라가는 틀 (노치 아래 230)
            var frame = NewRect("SafeFrame", parent).Stretch();
            frame.gameObject.AddComponent<SafeArea>();

            var root = NewRect("PowerUpFloat", frame).At(TopCenter, new Vector2(0, -230), new Vector2(560, 120));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var view = root.gameObject.AddComponent<PowerUpFloatView>();
            Img(root, "Shade", new Color(0.05f, 0.03f, 0.12f, 0.6f), art.Glow).rectTransform.At(Center, Vector2.zero, new Vector2(640, 220));
            Txt(root, "Caption", "전투력", 24, UITheme.SubText, TextAlignmentOptions.Center, true).rectTransform.Top(0, 34);
            var amount = Txt(root, "Amount", "+0", 56, UITheme.Positive, TextAlignmentOptions.Center, true);
            amount.rectTransform.Top(30, 80);

            new Binder(view)
                .Set("_root", root)
                .Set("_group", group)
                .Set("_amountText", amount)
                .Set("_canvasRoot", ctx.Canvas)
                .Apply();
        }

        static void BuildLevelUpFanfare(RectTransform parent, SceneBuildContext ctx, TutorialArt art)
        {
            var layer = NewRect("LevelUpFanfare", parent).Stretch();
            var view = layer.gameObject.AddComponent<LevelUpFanfareView>();

            var ringGlow = Img(layer, "RingGlow", WithAlpha(UITheme.Lamp, 0f), art.Glow);
            ringGlow.rectTransform.At(Center, Vector2.zero, new Vector2(380, 380));
            // 실 고리 (흰 크림 아트, 런타임이 금색으로 물들이며 퍼뜨린다)
            var ring = Img(layer, "Ring", WithAlpha(UITheme.Gold, 0f), art.Ring);
            ring.preserveAspect = true;
            ring.rectTransform.At(Center, Vector2.zero, new Vector2(260, 260));
            var ribbon = Pill(layer, "LevelUpRibbon", "Lv.UP!", 34, PillStyle.GoldRibbon, UITheme.Ink, Vector2.zero, new Vector2(260, 76), out var ribbonText);
            var ribbonGroup = ribbon.gameObject.AddComponent<CanvasGroup>();

            var chips = new Object[3];
            var chipTexts = new Object[3];
            var chipGroups = new Object[3];
            for (int i = 0; i < chips.Length; i++)
            {
                var chip = Pill(layer, $"Chip{i}", string.Empty, 26, PillStyle.Night, UITheme.Lavender, Vector2.zero,
                    new Vector2(600, 66), out var chipText);
                chips[i] = chip.rectTransform;
                chipTexts[i] = chipText;
                chipGroups[i] = chip.gameObject.AddComponent<CanvasGroup>();
            }

            var spark = Img(layer, "SparkTemplate", Color.white, art.Spark);
            spark.preserveAspect = true;
            spark.rectTransform.At(Center, Vector2.zero, new Vector2(36, 36));

            // 진화: 빛살·색종이(카드 뒤) + "새 형태 해금!" 카드
            var celebrationRoot = NewRect("EvolutionCelebration", layer).Stretch();
            var celebration = celebrationRoot.gameObject.AddComponent<CelebrationFx>();
            var card = NewRect("EvolutionCard", layer).At(Center, Vector2.zero, new Vector2(640, 640));
            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            Img(card, "Glow", WithAlpha(UITheme.Lavender, 0.75f), art.Glow).rectTransform.At(Center, new Vector2(0, 60), new Vector2(620, 620));
            // 둥근 받침 위에 새 형태 (초상화는 런타임이 채운다)
            Plate(card, "PortraitPlate", art).rectTransform.At(Center, new Vector2(0, 60), new Vector2(340, 340));
            var portrait = Picture(card, "Portrait", null, new Vector2(0, 70), new Vector2(400, 400));
            Pill(card, "TitlePill", "새 형태 해금!", 40, PillStyle.LavenderRibbon, UITheme.Ink, new Vector2(0, -190), new Vector2(400, 84), out _);
            var formName = Txt(card, "NameText", "양말 카라반", 38, UITheme.Cream, TextAlignmentOptions.Center, true);
            formName.rectTransform.At(Center, new Vector2(0, -272), new Vector2(600, 56));

            new Binder(celebration)
                .Set("_raysSprite", art.Rays)
                .Set("_sparkSprite", art.Spark)
                .SetArray("_confettiSprites", art.Confetti)
                .Set("_anchor", card)
                .Apply();
            SetBool(celebration, "_playOnEnable", false);

            new Binder(view)
                .Set("_canvasRoot", ctx.Canvas)
                .Set("_ring", ring)
                .Set("_ringGlow", ringGlow)
                .Set("_ribbon", ribbon.rectTransform)
                .Set("_ribbonText", ribbonText)
                .Set("_ribbonGroup", ribbonGroup)
                .SetArray("_chips", chips)
                .SetArray("_chipTexts", chipTexts)
                .SetArray("_chipGroups", chipGroups)
                .Set("_sparkTemplate", spark)
                .Set("_evolutionCard", card)
                .Set("_evolutionGroup", cardGroup)
                .Set("_evolutionPortrait", portrait)
                .Set("_evolutionName", formName)
                .Set("_celebration", celebration)
                .Set("_raysSprite", art.Rays)
                .SetArray("_confettiSprites", art.Confetti)
                .Set("_sparkSprite", art.Spark)
                .Apply();
        }
    }
}
