using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.SceneParts;
using static IdleGame.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 메타 화면 빌더 — 4차 팝업 (SPEC4, RM-2 P4-6). BuildPopups가 다른 메타 팝업 다음에 BuildSpec4Popups를 부른다.
    /// 만든 팝업은 모두 ctx.Popups에 넣어 UIManager가 TryGet으로 찾는다 (MetaNavigator · MetaPopupPresenter는 새 [SerializeField] 없이 찾는다 —
    /// 장면을 다시 만들기 전(G3)에는 팝업이 없어도 조용히 건너뛴다).
    ///   · ChapterMapPopup 940×1300 (CR-05 §8.1): 제목 '{n}장 · {장소}' + ‹ › 88, 지도 판 868×1000 (UiArt.MapBg 덮어 맞춤) + 칸 10개 96
    ///     (ChapterMapPopup.MapNodeLayout, UiArt.MapNode — 꺼짐 · 켜짐 · 보스 · 상자), 칸마다 밤 알약 '1-7' 20 + 기능 아이콘 40, 보스 초상 64
    ///     (+ 잠겼으면 자물쇠 40), 상자 표시 64, 지금 칸 숨 쉬는 빛 150, 아래 '{n}장 진행 {k}/10' 22 굵게 Cream.
    /// 아트는 P4-10의 UiArt 대신 그림(Planned) 규칙을 따른다 — 새 그림이 들어오면 다음 빌드가 코드 수정 없이 집어 간다.
    /// </summary>
    internal static partial class MetaSceneBuilder
    {
        /// <summary>4차 팝업 (BuildPopups가 부른다)</summary>
        static void BuildSpec4Popups(SceneBuildContext ctx)
        {
            ctx.Popups.Add(BuildChapterMapPopup(ctx));
            ctx.Popups.Add(BuildChapterChestPopup(ctx));
            ctx.Popups.Add(BuildStoryPagePopup(ctx));
            ctx.Popups.Add(BuildComebackCard(ctx));
            ctx.Popups.Add(BuildEventStartCard(ctx));
            ctx.Popups.Add(BuildNotificationAskPopup(ctx));
            ctx.Popups.Add(BuildSeasonPassPopup(ctx));
            ctx.Popups.Add(BuildEventHubPopup(ctx));
            ctx.Popups.Add(BuildCodexPopup(ctx));
        }

        // ═════════════════════════ 공용 ═════════════════════════

        /// <summary>그림 한 장 버튼 (투명하지 않은 그림이 누르는 자리, PressScale). 크기 · 자리는 부르는 쪽이</summary>
        static Button ImageButton(Transform parent, string name, Sprite sprite, Color color, out Image image)
        {
            image = Img(parent, name, color, sprite, true);
            image.preserveAspect = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            image.gameObject.AddComponent<PressScale>();
            return button;
        }

        /// <summary>
        /// 꺾쇠 버튼 88×88 (밤 메달 판 + UiArt.Chevron 44 — right면 오른쪽, 아니면 180° 돌려 왼쪽). 꺾쇠 그림이 없으면 '‹' · '›' 32 굵게 Cream
        /// </summary>
        static Button ChevronButton(Transform parent, string name, bool right)
        {
            var plateArt = UiArt.MedallionDark;
            bool plateIsArt = UiArt.IsArt(plateArt);
            var button = ImageButton(parent, name, plateArt, plateIsArt ? Color.white : MetaUi.Alpha(UITheme.Panel, 0.92f), out _);
            var chevron = UiArt.Chevron;
            if (chevron != null)
            {
                var icon = Img(button.transform, "Chevron", UITheme.Cream, chevron);
                icon.preserveAspect = true;
                icon.rectTransform.At(Center, Vector2.zero, new Vector2(44, 44));
                if (!right) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            }
            else
            {
                Txt(button.transform, "Label", right ? "›" : "‹", 32, UITheme.Cream, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            }
            return button;
        }

        /// <summary>정규화 자리(anchor = 점, 가운데 피벗)에 고정 크기로 놓는다 (지도 칸 — 판 크기가 바뀌어도 길이 따라간다)</summary>
        static RectTransform AtNormalized(RectTransform rt, Vector2 point, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = point;
            rt.anchorMax = point;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        // ═════════════════════════ 챕터 지도 (CR-05) ═════════════════════════

        /// <summary>
        /// 챕터 지도 Popups/ChapterMapPopup 940×1300 (SPEC4 §8.1). 창 제목이 '{n}장 · {장소}' (좌우 210 비움 — 꺾쇠 · 닫기 자리), 꺾쇠 PrevButton (28, −14) ·
        /// NextButton (−112, −14) 88. Body(868×1128): MapFrame Top(0, 1000) 안쪽 판 + RectMask2D 안 MapArt(덮어 맞춤 1024:1536) + 금테,
        /// Nodes(판 안쪽 6) 아래 CurrentGlow 150 · Node1~10 96 (보스 칸은 Boss 초상 64 + 잠겼을 때 BossLock 40) · 칸마다 LabelPill (밤 알약 88×34, 칸 아래 —
        /// ChapterMapPopup.LabelLift) 안 Label '1-1' 20 굵게 · Feature 아이콘 40 (칸 왼쪽 위, 누르는 자리 88) · ChestMarker 64 (보스 칸 오른쪽, 누르는 자리 88).
        /// 아래 FooterText 22 굵게 Cream.
        /// </summary>
        static ChapterMapPopup BuildChapterMapPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<ChapterMapPopup>(ctx.PopupRoot, "ChapterMapPopup", "1장", new Vector2(940, 1300), out var body, out var title);
            var window = (RectTransform)body.parent;
            title.rectTransform.Top(6, 104, 210, 210);
            title.FitFloor(26f);
            var prev = ChevronButton(window, "PrevButton", false);
            ((RectTransform)prev.transform).At(TopLeft, new Vector2(28, -14), new Vector2(88, 88));
            var next = ChevronButton(window, "NextButton", true);
            ((RectTransform)next.transform).At(TopRight, new Vector2(-112, -14), new Vector2(88, 88));

            const float mapHeight = 1000f;
            var frame = Sliced(body, "MapFrame", "ui_panel_inset_9s", 2f, MetaUi.Alpha(UITheme.PanelLight, 0.9f));
            frame.rectTransform.Top(0, mapHeight);
            var mask = NewRect("Mask", frame.transform).Stretch(6, 6, 6, 6);
            mask.gameObject.AddComponent<RectMask2D>();
            var mapSprite = UiArt.MapBg;
            var mapArt = Img(mask, "MapArt", mapSprite != null ? Color.white : NightPanelColor, mapSprite);
            mapArt.type = Image.Type.Simple;
            mapArt.rectTransform.Stretch();
            // 덮어 맞춤 (aspect AF-5): 가운데 자르기 그대로 (MapNodeLayout이 이 자르기에 맞춰져 있다), 그림이 바뀌면 그 비율로
            mapArt.gameObject.AddComponent<BackgroundFit>().Configure(BackgroundFitMode.Cover, new Vector2(0.5f, 0.5f), 1024f / 1536f);
            FrameOverlay(frame.transform, 2.2f);

            var nodes = NewRect("Nodes", frame.transform).Stretch(6, 6, 6, 6);
            float nodesHeight = mapHeight - 12f;
            var glow = Img(nodes, "CurrentGlow", MetaUi.Alpha(UITheme.Lamp, 0.6f), First(_icons.Glow, CircleSprite));
            AtNormalized(glow.rectTransform, ChapterMapPopup.MapNodeLayout[0], Vector2.zero, new Vector2(150, 150));
            glow.gameObject.SetActive(false);

            var off = UiArt.MapNode(UiArt.MapNodeKind.Off);
            var on = UiArt.MapNode(UiArt.MapNodeKind.On);
            var bossArt = UiArt.MapNode(UiArt.MapNodeKind.Boss);
            var chestArt = UiArt.MapNode(UiArt.MapNodeKind.Chest);
            int count = ChapterMapPopup.NodeCount;
            var buttons = new Object[count];
            var images = new Object[count];
            var labels = new Object[count];
            var featureButtons = new Object[count];
            var featureIcons = new Object[count];
            Image bossPortrait = null;
            RectTransform bossLock = null;
            for (int i = 0; i < count; i++)
            {
                bool boss = i == count - 1;
                var button = ImageButton(nodes, $"Node{i + 1}", boss ? bossArt : on, Color.white, out var image);
                AtNormalized(image.rectTransform, ChapterMapPopup.MapNodeLayout[i], Vector2.zero, new Vector2(96, 96));
                if (boss)
                {
                    // 초상 뒤 밤색 둥근 받침 58 (보스 고리 map_node_boss 안쪽 구멍 ≈ 60 — 옅은 초상이 옅은 돌길에 묻히지 않게, r1 · E1 chain 7)
                    var bossBack = Img(image.transform, "BossBack", MetaUi.Alpha(UITheme.Background, 0.82f), CircleSprite);
                    bossBack.rectTransform.At(Center, Vector2.zero, new Vector2(58, 58));
                    bossPortrait = Img(image.transform, "Boss", Color.white, null);
                    bossPortrait.preserveAspect = true;
                    bossPortrait.rectTransform.At(Center, Vector2.zero, new Vector2(64, 64));
                    bossPortrait.enabled = false;
                    // 잠긴 보스 칸: 흐리게 지우지 않고 (α0.85) 오른쪽 위에 자물쇠 40 (뷰가 켜고 끈다 — r1: α0.55로 보스 표시가 거의 안 읽혔다)
                    bossLock = LockIcon(image.rectTransform, 40f);
                    bossLock.name = "BossLock";
                    bossLock.At(TopRight, new Vector2(8, 8), new Vector2(40, 40));
                    bossLock.gameObject.SetActive(false);
                }
                // 칸 이름표 (r1: 옅은 돌길 위 Cream 글자는 읽히지 않았다): 칸 아래 밤 알약 PillNight9s 88×34 + '1-1' 20 굵게
                // (색은 뷰 — Cream · 지금 칸 Gold · 잠긴 칸 SubText, 알약 위 모두 4.5:1 넘음). 알약은 칸 아래 테두리에 4 겹치고,
                // 판 안쪽 아래 끝 + 24 밑으로는 내려가지 않는다 (ChapterMapPopup.LabelLift — 1번 칸이 판 테두리에 걸렸다)
                var pill = UiArt.Panel(image.transform, "LabelPill", UiArt.PillNight9s, UiArt.CapsulePpu(ChapterMapPopup.LabelPillHeight),
                    MetaUi.Alpha(Color.white, 0.96f), MetaUi.Alpha(UITheme.Background, 0.92f));
                var pillRect = pill.rectTransform;
                pillRect.anchorMin = pillRect.anchorMax = new Vector2(0.5f, 0f);
                pillRect.pivot = new Vector2(0.5f, 1f);
                pillRect.anchoredPosition = new Vector2(0, ChapterMapPopup.LabelLift(i, nodesHeight));
                pillRect.sizeDelta = new Vector2(88, ChapterMapPopup.LabelPillHeight);
                var label = Txt(pill.transform, "Label", BattleSystem.FormatStage(i + 1), UIType.Hint, UITheme.Cream, TextAlignmentOptions.Center, true)
                    .FitFloor(UIType.AutoSizeFloor);
                label.rectTransform.Stretch(10, 10, 0, 0);
                var feature = ImageButton(image.transform, "Feature", null, Color.white, out var featureIcon);
                featureIcon.rectTransform.At(TopLeft, new Vector2(-16, 16), new Vector2(40, 40));
                featureIcon.HitPad(new Vector2(40, 40), UISize.MinHit, UISize.MinHit);
                feature.gameObject.SetActive(false);
                buttons[i] = button;
                images[i] = image;
                labels[i] = label;
                featureButtons[i] = feature;
                featureIcons[i] = featureIcon;
            }

            var chest = ImageButton(nodes, "ChestMarker", chestArt, Color.white, out var chestImage);
            AtNormalized(chestImage.rectTransform, ChapterMapPopup.MapNodeLayout[count - 1], new Vector2(96, -8), new Vector2(64, 64));
            chestImage.HitPad(new Vector2(64, 64), UISize.MinHit, UISize.MinHit);
            chest.gameObject.SetActive(false);

            // 아래 진행 줄 22 굵게 Cream (r1: 남색 위 작은 회색 글자는 잘 안 보였다)
            var footer = Txt(body, "FooterText", "1장 진행 0/10", UIType.Caption, UITheme.Cream, TextAlignmentOptions.Center, true).FitFloor(UIType.HintMin);
            footer.rectTransform.Bottom(24, 40);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_prevButton", prev)
                .Set("_nextButton", next)
                .Set("_mapArt", mapArt)
                .SetArray("_nodeButtons", buttons)
                .SetArray("_nodeImages", images)
                .SetArray("_nodeLabels", labels)
                .Set("_currentGlow", glow.rectTransform)
                .Set("_bossPortrait", bossPortrait)
                .Set("_bossLock", bossLock != null ? bossLock.gameObject : null)
                .Set("_chestButton", chest)
                .Set("_chestImage", chestImage)
                .SetArray("_featureButtons", featureButtons)
                .SetArray("_featureIcons", featureIcons)
                .Set("_nodeOff", off)
                .Set("_nodeOn", on)
                .Set("_nodeBoss", bossArt)
                .Set("_nodeChest", chestArt)
                .Set("_chestOpen", First(ArtUI("journal_chest_open"), _icons.ChestOpen))
                .Set("_footerText", footer)
                .Apply();
            return popup;
        }

        /// <summary>
        /// 덮어 맞춘 그림 칸 (안쪽 판 + RectMask2D + 그림(비율 그대로 칸을 덮음, 넘치면 자름) + 금테). 그림이 없으면 fallback 색 판만 — Art는 꺼 둔다.
        /// art = 'Art' Image (실행 중에 그림을 바꿔 끼울 수 있다), 돌려주는 값은 칸 루트
        /// </summary>
        static RectTransform CoverArt(Transform parent, string name, Sprite sprite, Color fallback, float aspect, out Image art)
        {
            var back = Sliced(parent, name, "ui_panel_inset_9s", 2f, fallback);
            if (sprite == null) back.color = fallback;
            var mask = NewRect("Mask", back.transform).Stretch(6, 6, 6, 6);
            mask.gameObject.AddComponent<RectMask2D>();
            art = Img(mask, "Art", Color.white, sprite);
            art.type = Image.Type.Simple;
            art.rectTransform.Stretch();
            // 덮어 맞춤 (aspect AF-5, D5): 실행 중에 그림을 바꿔 끼우면(이야기 장 · 이벤트 · 시즌 배너) 새 그림의 비율로 다시 맞춘다 —
            // 빌드 때 비율에 묶이면 비율이 다른 새 그림이 늘어난다. 그림이 없을 때는 aspect
            art.gameObject.AddComponent<BackgroundFit>().Configure(BackgroundFitMode.Cover, new Vector2(0.5f, 0.5f), aspect);
            art.enabled = sprite != null;
            FrameOverlay(back.transform, 2.2f);
            return back.rectTransform;
        }

        // ═════════════════════════ 챕터 상자 (PRG-06) ═════════════════════════

        /// <summary>
        /// 챕터 상자 Popups/ChapterChestPopup 840×780 (SPEC4 §8.5): 제목 '{n}장 상자', Body(768×608): 상자 그림 Chest 300 (위 가운데, 빛 뒤),
        /// LineText 26 (y 308), Rewards 줄 (y 360, 칩 96, 가로 줄), ClaimButton '받기' Gold 520×112 (아래 가운데). 별빛 층은 창 위.
        /// </summary>
        static ChapterChestPopup BuildChapterChestPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<ChapterChestPopup>(ctx.PopupRoot, "ChapterChestPopup", "1장 상자", new Vector2(840, 780), out var body, out var title);
            var window = (RectTransform)body.parent;
            var chest = NewRect("Chest", body).At(TopCenter, new Vector2(0, 0), new Vector2(300, 300));
            var glow = Img(chest, "Glow", MetaUi.Alpha(UITheme.Gold, 0.45f), First(_icons.Glow, CircleSprite));
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(380, 380));
            var closed = First(ArtUI("journal_chest_closed"), _icons.ChestClosed);
            var open = First(ArtUI("journal_chest_open"), _icons.ChestOpen);
            var chestImage = Img(chest, "Art", Color.white, First(closed, _icons.Star));
            chestImage.preserveAspect = true;
            chestImage.rectTransform.Stretch(30, 30, 30, 30);
            var line = Txt(body, "LineText", "챕터 보스를 물리치고 찾은 상자예요", UIType.Body, UITheme.Text, TextAlignmentOptions.Center).FitFloor(UIType.Hint);
            line.rectTransform.Top(304, 44, 20, 20);
            var rewards = NewRect("Rewards", body).Top(358, 120, 40, 40);
            var row = rewards.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = UISpace.GridGap;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var chip = BuildRewardChip(rewards, "Reward", new Vector2(110, 120), 64, 24, false, true);
            var claim = SceneParts.Button(body, "ClaimButton", "받기", SceneParts.ButtonStyle.Gold, UIType.ButtonL, out var claimLabel);
            ((RectTransform)claim.transform).At(BottomCenter, new Vector2(0, 4), new Vector2(UISize.PopupCtaWidth, UISize.PopupCtaHeight));
            var sparkles = BuildSparkles(window, "Sparkles", 16, 44);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_lineText", line)
                .Set("_chest", chest)
                .Set("_chestImage", chestImage)
                .Set("_closedSprite", closed)
                .Set("_openSprite", open)
                .Set("_rewardRoot", rewards)
                .Set("_rewardTemplate", chip)
                .Set("_claimButton", claim)
                .Set("_claimLabel", claimLabel)
                .Set("_sparkles", sparkles)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 동화책 (CR-05) ═════════════════════════

        /// <summary>
        /// 동화책 Popups/StoryPagePopup 980×1400 (SPEC4 §8.1): 제목 '동화책 {n}장 · {제목}' (34까지 줄어듦). Body(908×1228) 맨 뒤 TapCatcher(투명, 다음 칸),
        /// Panels/Panel1~3 (CanvasGroup — 그림 칸 908×310 CoverArt + 그 아래 Text 26 Text 가운데, 칸 사이 8), 아래 RewardRow (보상 칩 96 + '처음 읽은 선물') ·
        /// CloseGold '닫기' Gold 360×96 / CloseSoft '닫기' Secondary 360×96 (오른쪽 아래, 하나만 보인다). 그림이 없으면 장 색 판 'Fallback'만.
        /// </summary>
        static StoryPagePopup BuildStoryPagePopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<StoryPagePopup>(ctx.PopupRoot, "StoryPagePopup", "동화책 1장", new Vector2(980, 1400), out var body, out var title);
            title.FitFloor(UIType.PanelTitle);
            var catcher = Img(body, "TapCatcher", Color.clear, null, true);
            catcher.rectTransform.Stretch(0, 0, 0, 128);
            var catcherButton = catcher.gameObject.AddComponent<Button>();
            catcherButton.targetGraphic = catcher;
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.navigation = new Navigation { mode = Navigation.Mode.None };

            const float panelHeight = 310f;
            const float textHeight = 44f;
            const float gap = 8f;
            var groups = new Object[StoryPagePopup.PanelCount];
            var arts = new Object[StoryPagePopup.PanelCount];
            var fallbacks = new Object[StoryPagePopup.PanelCount];
            var texts = new Object[StoryPagePopup.PanelCount];
            for (int i = 0; i < StoryPagePopup.PanelCount; i++)
            {
                float top = i * (panelHeight + textHeight + gap);
                var root = NewRect($"Panel{i + 1}", body).Top(top, panelHeight + textHeight);
                var group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                var frame = CoverArt(root, "Picture", null, MetaUi.Alpha(UITheme.PanelLight, 0.9f), 940f / 380f, out var art);
                frame.Top(0, panelHeight);
                var fallback = Img(frame, "Fallback", MetaUi.Alpha(UITheme.Lavender, 0.5f), RoundSprite);
                fallback.rectTransform.Stretch(8, 8, 8, 8);
                fallback.transform.SetSiblingIndex(0);
                var text = Txt(root, "Text", string.Empty, UIType.Body, UITheme.Text, TextAlignmentOptions.Center).FitFloor(UIType.Hint);
                text.rectTransform.Top(panelHeight + 2, textHeight - 2, 12, 12);
                groups[i] = group;
                arts[i] = art;
                fallbacks[i] = fallback;
                texts[i] = text;
            }

            var rewardRow = NewRect("RewardRow", body).At(BottomLeft, new Vector2(0, 0), new Vector2(460, 112));
            var chip = BuildRewardChip(rewardRow, "Reward", new Vector2(96, 112), 56, 22, false, true);
            ((RectTransform)chip.transform).At(MiddleLeft, Vector2.zero, new Vector2(96, 112));
            var rewardLabel = Txt(rewardRow, "Label", "처음 읽은 선물", UIType.Label, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Hint);
            rewardLabel.rectTransform.At(MiddleLeft, new Vector2(110, 0), new Vector2(340, 40));
            rewardRow.gameObject.SetActive(false);
            // [닫기] 하나: 처음 읽기면 Gold, 다시 읽기면 Secondary 그림으로 바꿔 끼운다 (뷰)
            var finish = SceneParts.Button(body, "FinishButton", "닫기", SceneParts.ButtonStyle.Gold, UIType.ButtonM, out var finishLabel);
            ((RectTransform)finish.transform).At(BottomRight, new Vector2(0, 8), new Vector2(360, 96));
            var finishImage = (Image)finish.targetGraphic;

            new Binder(popup)
                .Set("_titleText", title)
                .SetArray("_panels", groups)
                .SetArray("_panelArt", arts)
                .SetArray("_panelFallback", fallbacks)
                .SetArray("_panelText", texts)
                .Set("_tapCatcher", catcherButton)
                .Set("_rewardRoot", rewardRow.gameObject)
                .Set("_rewardChip", chip)
                .Set("_finishButton", finish)
                .Set("_finishImage", finishImage)
                .Set("_finishLabel", finishLabel)
                .Set("_goldSprite", UiArt.IsArt(UiArt.ButtonGold9s) ? UiArt.ButtonGold9s : null)
                .Set("_softSprite", UiArt.IsArt(UiArt.ButtonSecondary9s) ? UiArt.ButtonSecondary9s : null)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 다시 만난 날 (CR-06) ═════════════════════════

        /// <summary>
        /// 다시 만난 날 Popups/ComebackCard 900×1100 (SPEC4 §8.2): 창 제목 자리에 머리 그림 Header 840×300 (UiArt.ComebackHeader — 덮어 맞춤),
        /// Body(828×?) 'Headline' '다시 만나서 반가워요!' 56 → 44 Gold 굵게 (y 300), BodyText 26 (y 372, 두 줄), Cells 3칸 220×260 (y 470, 간격 24 —
        /// 판(출석 칸 그림) · Glow(오늘, 금빛) · Day '{n}일째' 24 굵게 Ink · 보상 칩 96 · 도장 140 · 고리 UiArt.StampRing), StampButton '도장 찍기' Gold 520×112.
        /// </summary>
        static ComebackCard BuildComebackCard(SceneBuildContext ctx)
        {
            var popup = BuildPopup<ComebackCard>(ctx.PopupRoot, "ComebackCard", string.Empty, new Vector2(900, 1100), out var body, out var title);
            var window = (RectTransform)body.parent;
            title.gameObject.SetActive(false);
            var divider = window.Find("Divider");
            if (divider != null) divider.gameObject.SetActive(false);
            body.Stretch(30, 30, 30, 30);
            var header = CoverArt(body, "Header", UiArt.ComebackHeader, NightPanelColor, 1536f / 560f, out _);
            header.Top(0, 300, 0, 0);
            var headline = Txt(body, "Headline", "다시 만나서 반가워요!", UIType.Display, UITheme.Gold, TextAlignmentOptions.Center, true).FitFloor(44f);
            headline.rectTransform.Top(312, 70);
            var text = Txt(body, "BodyText", "3일 만이에요. 친구들이 수선점을 지키며 기다렸어요.", UIType.Body, UITheme.Text, TextAlignmentOptions.Top, false, true)
                .FitFloor(UIType.Hint);
            text.rectTransform.Top(390, 76, 20, 20);

            var cellsRoot = NewRect("Cells", body).Top(478, 260);
            var row = cellsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 24f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var cellArt = UiArt.AttendanceCell9s;
            var cells = new Object[ComebackCard.Days];
            var backs = new Object[ComebackCard.Days];
            var glows = new Object[ComebackCard.Days];
            var days = new Object[ComebackCard.Days];
            var chips = new Object[ComebackCard.Days];
            var stamps = new Object[ComebackCard.Days];
            for (int i = 0; i < ComebackCard.Days; i++)
            {
                var cell = NewRect($"Cell{i + 1}", cellsRoot);
                cell.sizeDelta = new Vector2(220, 260);
                var glow = Img(cell, "Glow", MetaUi.Alpha(UITheme.Gold, 0.5f), First(_icons.Glow, CircleSprite));
                glow.rectTransform.At(Center, Vector2.zero, new Vector2(300, 320));
                glow.gameObject.SetActive(false);
                var back = UiArt.Panel(cell, "Back", cellArt, 2.4f, Color.white, UITheme.Parchment);
                back.rectTransform.Stretch();
                // 날 글은 칸 그림 위 라벤더 띠 위라 Ink · 외곽선 없음 (RM-3 RI-2 — InkSub는 띠에서 2.2:1). 런타임도 같은 색 (ComebackCard.DayLabelColor)
                var day = Txt(cell, "Day", $"{i + 1}일째", UIType.Label, ComebackCard.DayLabelColor, TextAlignmentOptions.Center, true);
                day.rectTransform.Top(12, 34, 8, 8);
                var ring = Img(cell, "Ring", MetaUi.Alpha(UITheme.Ink, 0.25f), UiArt.StampRing);
                ring.preserveAspect = true;
                ring.rectTransform.At(Center, new Vector2(0, -10), new Vector2(150, 150));
                // 양피지 칸 위 양은 Ink · 외곽선 없음, 작은 줄 '+n'은 PositiveInk (r1 — 출석 칸과 같은 규칙)
                var chip = BuildRewardChip(cell, "Reward", new Vector2(110, 120), 60, UIType.Caption, false, false, true, onLight: true);
                ((RectTransform)chip.transform).At(Center, new Vector2(0, -14), new Vector2(110, 120));
                var stamp = BuildStampGraphic(cell, "Stamp", 140);
                stamp.anchoredPosition = new Vector2(0, -10);
                stamp.gameObject.SetActive(false);
                cells[i] = cell;
                backs[i] = back;
                glows[i] = glow.gameObject;
                days[i] = day;
                chips[i] = chip;
                stamps[i] = stamp;
            }
            var stampButton = SceneParts.Button(body, "StampButton", "도장 찍기", SceneParts.ButtonStyle.Gold, UIType.ButtonL, out var stampLabel);
            ((RectTransform)stampButton.transform).At(BottomCenter, new Vector2(0, 16), new Vector2(UISize.PopupCtaWidth, UISize.PopupCtaHeight));
            var sparkles = BuildSparkles(window, "Sparkles", 16, 44);

            new Binder(popup)
                .Set("_bodyText", text)
                .SetArray("_cells", cells)
                .SetArray("_cellBacks", backs)
                .SetArray("_cellGlows", glows)
                .SetArray("_cellDays", days)
                .SetArray("_cellRewards", chips)
                .SetArray("_cellStamps", stamps)
                .Set("_stampButton", stampButton)
                .Set("_stampLabel", stampLabel)
                .Set("_sparkles", sparkles)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 이벤트 시작 카드 (EVT-01) ═════════════════════════

        /// <summary>
        /// 이벤트 시작 카드 Popups/EventStartCard 840×620 (SPEC4 §8.3 — 기능 개방 카드 틀): 제목 '{이름}가 시작됐어요!' (32까지), Body(768×448):
        /// Banner 640×240 덮어 맞춤 (UI/shop_event_banner — 이벤트 그림이 있으면 바꿔 끼운다), BodyText 28 (y 256, 두 줄),
        /// 아래 [나중에] Secondary · [가 보기 ›] Primary (100 높이 줄, 간격 24). 별빛 층은 창 위.
        /// </summary>
        static EventStartCard BuildEventStartCard(SceneBuildContext ctx)
        {
            var popup = BuildPopup<EventStartCard>(ctx.PopupRoot, "EventStartCard", "이벤트가 시작됐어요!", new Vector2(840, 620), out var body, out var title);
            var window = (RectTransform)body.parent;
            title.FitFloor(UIType.H2);
            var fallback = ArtUI("shop_event_banner");
            var banner = CoverArt(body, "Banner", fallback, NightPanelColor, 640f / 240f, out var bannerArt);
            banner.At(TopCenter, Vector2.zero, new Vector2(640, 240));
            var text = Txt(body, "BodyText", string.Empty, 28, UITheme.Text, TextAlignmentOptions.Top, false, true).FitFloor(UIType.Caption);
            text.rectTransform.At(TopCenter, new Vector2(0, -256), new Vector2(740, 80));
            var row = NewRect("Buttons", body).Bottom(0, 100);
            AddRow(row, 24);
            var later = SceneParts.Button(row, "LaterButton", "나중에", SceneParts.ButtonStyle.Secondary, 32, out _);
            var go = SceneParts.Button(row, "GoButton", "가 보기 ›", SceneParts.ButtonStyle.Primary, 32, out _);
            var sparkles = BuildSparkles(window, "Sparkles", 14, 44);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_bodyText", text)
                .Set("_banner", bannerArt)
                .Set("_fallbackBanner", fallback)
                .Set("_goButton", go)
                .Set("_laterButton", later)
                .Set("_sparkles", sparkles)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 알림 묻기 (NOTI-01) ═════════════════════════

        /// <summary>
        /// 알림 묻기 Popups/NotificationAskPopup 760×600 (SPEC4 §4.3): 제목 '알림을 보내 드릴까요?', Body(688×428): 바구니 그림 Basket 180 (UI/idle_basket_3,
        /// 없으면 바구니 자리표시), BodyText 24 (y 196, 세 줄 — 밤 시간 안내까지), 아래 [괜찮아요] Secondary (왼쪽) · [알려 주세요] Primary (오른쪽).
        /// </summary>
        static NotificationAskPopup BuildNotificationAskPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<NotificationAskPopup>(ctx.PopupRoot, "NotificationAskPopup", NotificationAskPopup.TitleText, new Vector2(760, 600),
                out var body, out _);
            var basketArt = First(ArtUI("idle_basket_3"), _icons.Basket(3));
            var basket = Img(body, "Basket", Color.white, basketArt);
            basket.preserveAspect = true;
            basket.rectTransform.At(TopCenter, new Vector2(0, 0), new Vector2(200, 180));
            if (basketArt == null)
            {
                basket.enabled = false;
                BuildBasketPlaceholder(basket.rectTransform, new Vector2(0, -6), 1.1f, out _);
            }
            var text = Txt(body, "BodyText", NotificationAskPopup.BodyText, UIType.Label, UITheme.Text, TextAlignmentOptions.Top, false, true)
                .FitFloor(UIType.HintMin);
            text.rectTransform.Top(194, 120, 10, 10);
            var row = NewRect("Buttons", body).Bottom(0, 100);
            AddRow(row, 24);
            var later = SceneParts.Button(row, "LaterButton", "괜찮아요", SceneParts.ButtonStyle.Secondary, 30, out _);
            var allow = SceneParts.Button(row, "AllowButton", "알려 주세요", SceneParts.ButtonStyle.Primary, 30, out _);

            new Binder(popup)
                .Set("_laterButton", later)
                .Set("_allowButton", allow)
                .Set("_bodyText", text)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 친구 도감 (Q-05) ═════════════════════════

        /// <summary>
        /// 친구 도감 Popups/CodexPopup 980×1500 (SPEC4 §6.2). Body(908×1328):
        /// Header Top(0, 150) 금테 판 — PercentText '도감 0%' 40 Gold 굵게 (28, −14) · Bar 640×22 (28, −74) 안 Pips 6개 (UiArt.PipButton 14, '+1%' 18 아래) ·
        /// BonusText 22 SubText (28, −114). Tabs Top(160, 64): Tab0~5 144×64 간격 8 (누르는 자리 88 — 위아래로), 탭마다 배지 36.
        /// Grid 스크롤 Stretch(0, 0, 240, 128): 5열 164×212 (간격 17 · 16) — CodexCellView 틀 (양피지 카드 · 등급 칸 틀 148 · 그림 140 · 이름 24 Ink ·
        /// 금 리본 '받기' 120×36 · 보상 칩 52 · 체크 28). EmptyText 26 SubText. 아래 오른쪽 ClaimAllButton 380×96.
        /// </summary>
        static CodexPopup BuildCodexPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<CodexPopup>(ctx.PopupRoot, "CodexPopup", "친구 도감", new Vector2(980, 1500), out var body, out _);

            var header = FramedPanel(body, "Header", MetaUi.Alpha(UITheme.PanelLight, 0.75f));
            header.rectTransform.Top(0, 150);
            var percent = Txt(header.transform, "PercentText", "도감 0%", UIType.H1, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.H2);
            percent.rectTransform.At(TopLeft, new Vector2(28, -12), new Vector2(420, 54));
            const float barHeight = 22f;
            var bar = Bar(header.transform, "Bar", barHeight, UITheme.Lavender, out var fill);
            bar.rectTransform.At(TopLeft, new Vector2(28, -72), new Vector2(640, barHeight));
            var pipRoot = NewRect("Pips", bar.transform).Stretch();
            var pips = new Object[CodexPopup.FormPipCounts.Length];
            var pipLabels = new Object[CodexPopup.FormPipCounts.Length];
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = Img(pipRoot, $"Pip{i}", MetaUi.Alpha(UITheme.SubText, 0.5f), UiArt.PipButton);
                pip.preserveAspect = true;
                var rect = pip.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(640f * CodexPopup.FormPipCounts[i] / 29f, 0f);
                rect.sizeDelta = new Vector2(14, 14);
                var label = Txt(pip.transform, "Label", "+1%", UIType.Micro, UITheme.SubText, TextAlignmentOptions.Center, true);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0, -6);
                labelRect.sizeDelta = new Vector2(64, 22);
                pips[i] = pip;
                pipLabels[i] = label;
            }
            var bonus = Txt(header.transform, "BonusText", "모은 형태 0종 · 친구 공격력·체력 +0%", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.HintMin).Ellipsis();
            bonus.rectTransform.At(TopLeft, new Vector2(28, -112), new Vector2(852, 32));

            var tabButtons = new Object[CodexPopup.TabCount];
            var tabImages = new Object[CodexPopup.TabCount];
            var tabLabels = new Object[CodexPopup.TabCount];
            var tabBadges = new Object[CodexPopup.TabCount];
            for (int i = 0; i < CodexPopup.TabCount; i++)
            {
                var tab = TabButton(body, $"Tab{i}", CodexPopup.TabNames[i], out var label);
                ((RectTransform)tab.transform).At(TopLeft, new Vector2(i * 152, -160), new Vector2(144, 64));
                label.fontSize = UIType.Label;
                label.FitFloor(UIType.Hint);
                tab.targetGraphic.HitPad(new Vector2(144, 64), 0f, UISize.MinHit);
                var badge = BuildBadge(tab.transform);
                ((RectTransform)badge.transform).At(TopRight, new Vector2(10, 10), new Vector2(36, 36));
                badge.gameObject.SetActive(false);
                tabButtons[i] = tab;
                tabImages[i] = tab.targetGraphic;
                tabLabels[i] = label;
                tabBadges[i] = badge;
            }

            var scrollRoot = NewRect("Grid", body).Stretch(0, 0, 240, 128);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var viewport = NewRect("Viewport", scrollRoot).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = Color.clear;
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 212f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(UISpace.Grid5PopupCell, 212f);
            grid.spacing = new Vector2(UISpace.Grid5PopupGap, 16f);
            grid.padding = new RectOffset(10, 10, 4, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            var cell = BuildCodexCellTemplate(content);
            cell.gameObject.SetActive(false);
            var empty = Txt(body, "EmptyText", "아직 찾은 것이 없어요. 모험을 이어 가 봐요", UIType.Body, UITheme.SubText, TextAlignmentOptions.Center, false, true)
                .FitFloor(UIType.Hint);
            empty.rectTransform.Stretch(40, 40, 520, 520);
            empty.gameObject.SetActive(false);

            var claimAll = ClaimAllButton(body, UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight, UISize.ClaimAllFooterText, out _, out var claimBadge);
            ((RectTransform)claimAll.transform).At(BottomRight, new Vector2(0, 8), new Vector2(UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight));
            claimAll.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_percentText", percent)
                .Set("_barFill", fill)
                .Set("_bar", bar.rectTransform)
                .Set("_bonusText", bonus)
                .Set("_pipRoot", pipRoot.gameObject)
                .SetArray("_pips", pips)
                .SetArray("_pipLabels", pipLabels)
                .SetArray("_tabButtons", tabButtons)
                .SetArray("_tabImages", tabImages)
                .SetArray("_tabLabels", tabLabels)
                .SetArray("_tabBadges", tabBadges)
                .Set("_scroll", scroll)
                .Set("_gridContent", content)
                .Set("_cellTemplate", cell)
                .Set("_emptyText", empty)
                .Set("_storyIcon", First(ArtLib.Icon("story"), _icons.Journal))
                .Set("_claimAllButton", claimAll)
                .Set("_claimAllBadge", claimBadge)
                .Apply();
            return popup;
        }

        /// <summary>도감 칸 틀 164×212 (CodexCellView): 양피지 카드 + 등급 칸 틀 148 + 그림 140 + 이름 24 Ink + 금 리본 '받기' + 보상 칩 52 + 체크 28</summary>
        static CodexCellView BuildCodexCellTemplate(RectTransform content)
        {
            var card = Card(content, "Cell");
            card.raycastTarget = true;
            var root = card.rectTransform;
            root.sizeDelta = new Vector2(UISpace.Grid5PopupCell, 212f);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            card.gameObject.AddComponent<PressScale>();
            var view = card.gameObject.AddComponent<CodexCellView>();

            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2.4f, Color.white, MetaUi.Alpha(UITheme.PanelLight, 0.9f));
            frame.rectTransform.At(TopCenter, new Vector2(0, -8), new Vector2(148, 148));
            var portrait = Img(frame.transform, "Portrait", Color.white, null);
            portrait.preserveAspect = true;
            portrait.rectTransform.At(Center, Vector2.zero, new Vector2(132, 132));
            var name = Txt(root, "Name", "???", UIType.Label, UITheme.Ink, TextAlignmentOptions.Center, true).FitFloor(UIType.Micro).Ellipsis();
            name.rectTransform.Bottom(8, 40, 6, 6);
            var ribbon = UiArt.Panel(root, "Ribbon", UiArt.RibbonGold9s, 2.4f, Color.white, UITheme.Gold);
            ribbon.rectTransform.At(TopCenter, new Vector2(0, -132), new Vector2(120, 36));
            Txt(ribbon.transform, "Label", "받기", UIType.Hint, UITheme.Ink, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            ribbon.gameObject.SetActive(false);
            var reward = BuildRewardChip(root, "Reward", new Vector2(52, 52), 32, 18, false, false);
            ((RectTransform)reward.transform).At(TopLeft, new Vector2(2, -2), new Vector2(52, 52));
            reward.gameObject.SetActive(false);
            var check = Img(root, "Check", Color.white, First(ArtIcon("check"), _icons.Check));
            check.preserveAspect = true;
            check.rectTransform.At(TopRight, new Vector2(-6, -6), new Vector2(28, 28));
            check.gameObject.SetActive(false);

            new Binder(view)
                .Set("_button", button)
                .Set("_frame", frame)
                .Set("_portrait", portrait)
                .Set("_fallbackSprite", _icons.Star)
                .Set("_nameText", name)
                .Set("_ribbon", ribbon.gameObject)
                .Set("_reward", reward)
                .Set("_check", check.gameObject)
                .Apply();
            return view;
        }

        // ═════════════════════════ 이벤트 모아 보기 (EVT-01) ═════════════════════════

        /// <summary>
        /// 이벤트 모아 보기 Popups/EventHubPopup 980×1500 (SPEC4 §8.3). Body(908×1328):
        /// Banner Top(0, 354) 덮어 맞춤 (UI/shop_event_banner) + 아래 어둠 Scrim + NameText 40 Cream 굵게 외곽선 (왼쪽 아래) + TimeText 24 (오른쪽 아래, 시계 글꼴).
        /// Effects Top(362, 72): Chip0~3 (밤 알약 · 아이콘 32 · 24 두 줄까지, 가로로 나눠 채움) · NightKeeperText Top(438, 26) Hint 20 Lavender.
        /// Stamps Top(466, 190): Stamp0~6 120×180 (출석 칸 그림 · 오늘 금빛 · 날 글 22 Ink · 보상 칩 64 · 도장 96) — 칸이 버튼.
        /// Missions 스크롤 Stretch(0, 0, 664, 120) (미션 줄 틀 860×150 — JournalRowModel; 아래 끝 = 상점 버튼 위 16, 수첩 칸이 보이면
        /// EventHubPopup이 그 칸 위 12 = 240으로 올린다 — RM-3 RI-3). EmptyText.
        /// 아래: SeasonTile 860×110 Bottom(118) · ShopButton '이벤트 상점 가기 ›' Primary 420×96 (왼쪽 아래) · ClaimAllButton 380×96 (오른쪽 아래).
        /// </summary>
        static EventHubPopup BuildEventHubPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<EventHubPopup>(ctx.PopupRoot, "EventHubPopup", "이벤트", new Vector2(980, 1500), out var body, out _);

            var bannerArt = ArtUI("shop_event_banner");
            var banner = CoverArt(body, "Banner", bannerArt, NightPanelColor, 908f / 354f, out var bannerImage);
            banner.Top(0, 354);
            var scrim = Img(banner, "Scrim", Color.white, FadeSprite(false));
            scrim.color = MetaUi.Alpha(UITheme.Background, 0.85f);
            scrim.rectTransform.Bottom(6, 150, 6, 6);
            var name = UiArt.Outline(Txt(banner, "NameText", "지금 열린 축제가 없어요", UIType.H1, UITheme.Cream, TextAlignmentOptions.BottomLeft, true)
                .FitFloor(UIType.Title));
            name.rectTransform.At(BottomLeft, new Vector2(28, 52), new Vector2(560, 56));
            var time = UiArt.Outline(Txt(banner, "TimeText", string.Empty, UIType.Label, UITheme.Gold, TextAlignmentOptions.BottomRight, true)
                .FitFloor(UIType.Hint));
            time.rectTransform.At(BottomRight, new Vector2(-28, 22), new Vector2(320, 34));
            name.rectTransform.anchoredPosition = new Vector2(28, 20);

            var effects = NewRect("Effects", body).Top(362, 72);
            var effectsRow = effects.gameObject.AddComponent<HorizontalLayoutGroup>();
            effectsRow.spacing = 10f;
            effectsRow.childControlWidth = true;
            effectsRow.childControlHeight = true;
            effectsRow.childForceExpandWidth = true;
            effectsRow.childForceExpandHeight = true;
            var chips = new Object[EventHubPopup.EffectChips];
            var chipIcons = new Object[EventHubPopup.EffectChips];
            var chipLabels = new Object[EventHubPopup.EffectChips];
            for (int i = 0; i < EventHubPopup.EffectChips; i++)
            {
                var chip = UiArt.Panel(effects, $"Chip{i}", UiArt.PillNight9s, UiArt.CapsulePpu(72), MetaUi.Alpha(Color.white, 0.94f),
                    MetaUi.Alpha(UITheme.Background, 0.86f));
                var icon = Img(chip.transform, "Icon", Color.white, null);
                icon.preserveAspect = true;
                icon.rectTransform.At(MiddleLeft, new Vector2(14, 0), new Vector2(32, 32));
                icon.enabled = false;
                var label = Txt(chip.transform, "Label", string.Empty, UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true, true)
                    .FitFloor(UIType.AutoSizeFloor);
                label.rectTransform.Stretch(52, 12, 4, 4);
                chip.gameObject.SetActive(false);
                chips[i] = chip.gameObject;
                chipIcons[i] = icon;
                chipLabels[i] = label;
            }
            var nightKeeper = Txt(body, "NightKeeperText", EventHubPopup.NightKeeperLine, UIType.Hint, UITheme.Lavender, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.HintMin);
            nightKeeper.rectTransform.Top(438, 26, 12, 12);
            nightKeeper.gameObject.SetActive(false);

            // 7칸 120×180, 간격 6, 가운데 맞춤 (줄 묶음 대신 자리를 직접 — 끝 검사가 칸끼리 겹친다고 보지 않게)
            var stampRow = NewRect("Stamps", body).Top(466, 190);
            const float stampPitch = 126f;
            float stampStart = (908f - (EventHubPopup.StampCells * stampPitch - 6f)) * 0.5f;
            var cells = new Object[EventHubPopup.StampCells];
            var glows = new Object[EventHubPopup.StampCells];
            var marks = new Object[EventHubPopup.StampCells];
            var days = new Object[EventHubPopup.StampCells];
            var rewards = new Object[EventHubPopup.StampCells];
            for (int i = 0; i < EventHubPopup.StampCells; i++)
            {
                var cell = UiArt.Panel(stampRow, $"Stamp{i}", UiArt.AttendanceCell9s, 2.4f, Color.white, UITheme.Parchment, true);
                cell.rectTransform.At(TopLeft, new Vector2(stampStart + i * stampPitch, -5), new Vector2(120, 180));
                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cell;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                cell.gameObject.AddComponent<PressScale>();
                var glow = Img(cell.transform, "Glow", MetaUi.Alpha(UITheme.Gold, 0.55f), First(_icons.Glow, CircleSprite));
                glow.rectTransform.At(Center, Vector2.zero, new Vector2(170, 220));
                glow.transform.SetAsFirstSibling();
                glow.gameObject.SetActive(false);
                // 날 글은 라벤더 띠 위라 Ink · 외곽선 없음 (RM-3 RI-2). 런타임도 같은 색 (EventHubPopup.StampDayColor)
                var day = Txt(cell.transform, "Day", $"{i + 1}일", UIType.Caption, EventHubPopup.StampDayColor, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
                day.rectTransform.Top(8, 30, 4, 4);
                // 양피지 칸 위 양은 Ink · 외곽선 없음 22 (r1: 크림 + 외곽선 '30' · '×15'가 양피지 위에서 거의 안 읽혔다 — 출석 칸과 같은 규칙)
                var chip = BuildRewardChip(cell.transform, "Reward", new Vector2(96, 100), 56, UIType.Caption, false, false, onLight: true);
                ((RectTransform)chip.transform).At(Center, new Vector2(0, -16), new Vector2(96, 100));
                var mark = BuildStampGraphic(cell.rectTransform, "Mark", 96);
                mark.anchoredPosition = new Vector2(0, -16);
                mark.gameObject.SetActive(false);
                cells[i] = button;
                glows[i] = glow.gameObject;
                marks[i] = mark.gameObject;
                days[i] = day;
                rewards[i] = chip;
            }
            stampRow.gameObject.SetActive(false);

            var listRoot = NewRect("Missions", body).Stretch(0, 0, 664, EventHubPopup.ListBottomNoSeason);
            var scroll = listRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var viewport = NewRect("Viewport", listRoot).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = Color.clear;
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 150f);
            var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = UISpace.ListGap;
            list.childAlignment = TextAnchor.UpperCenter;
            list.childControlWidth = false;
            list.childControlHeight = false;
            list.childForceExpandWidth = false;
            list.childForceExpandHeight = false;
            var contentFit = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            var row = BuildMissionRowTemplate(content);
            var empty = Txt(listRoot, "EmptyText", "이 축제에는 미션이 없어요. 효과는 저절로 받아요", UIType.Body, UITheme.SubText, TextAlignmentOptions.Center, false, true)
                .FitFloor(UIType.Hint);
            empty.rectTransform.Stretch(30, 30, 40, 40);
            empty.gameObject.SetActive(false);

            var seasonTile = SceneParts.Button(body, "SeasonTile", "별빛 산책 수첩 Lv.1 ›", SceneParts.ButtonStyle.Secondary, UIType.ButtonM, out var seasonLabel);
            ((RectTransform)seasonTile.transform).At(BottomCenter, new Vector2(0, EventHubPopup.SeasonTileBottom), new Vector2(860, EventHubPopup.SeasonTileHeight));
            seasonTile.gameObject.SetActive(false);
            var shop = SceneParts.Button(body, "ShopButton", "이벤트 상점 가기 ›", SceneParts.ButtonStyle.Primary, UIType.ButtonM, out _);
            ((RectTransform)shop.transform).At(BottomLeft, new Vector2(0, EventHubPopup.FooterBottom), new Vector2(420, EventHubPopup.FooterHeight));
            var claimAll = ClaimAllButton(body, UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight, UISize.ClaimAllFooterText, out _, out var claimBadge);
            ((RectTransform)claimAll.transform).At(BottomRight, new Vector2(0, EventHubPopup.FooterBottom), new Vector2(UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight));
            claimAll.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_banner", bannerImage)
                .Set("_fallbackBanner", bannerArt)
                .Set("_nameText", name)
                .Set("_timeText", time)
                .SetArray("_chips", chips)
                .SetArray("_chipIcons", chipIcons)
                .SetArray("_chipLabels", chipLabels)
                .Set("_nightKeeperText", nightKeeper)
                .Set("_stampRow", stampRow.gameObject)
                .SetArray("_stampCells", cells)
                .SetArray("_stampGlows", glows)
                .SetArray("_stampMarks", marks)
                .SetArray("_stampDays", days)
                .SetArray("_stampRewards", rewards)
                .Set("_listRoot", listRoot)
                .Set("_listContent", content)
                .Set("_rowTemplate", row)
                .Set("_emptyText", empty)
                .Set("_shopButton", shop)
                .Set("_claimAllButton", claimAll)
                .Set("_claimAllBadge", claimBadge)
                .Set("_seasonTile", seasonTile)
                .Set("_seasonLabel", seasonLabel)
                .Apply();
            return popup;
        }

        // ═════════════════════════ 수선 일지 4차 탭 (Q-04 · NEW-01) ═════════════════════════

        /// <summary>BuildRecordsPanel이 만든 부품 (BuildJournalPopup이 JournalPopup에 묶는다)</summary>
        struct RecordsParts
        {
            public TextMeshProUGUI Count;
            public Image BarFill;
            public TextMeshProUGUI Claimable;
            public MetaSegmentedControl Segments;
            public ScrollRect Scroll;
            public RectTransform ListContent;
            public TextMeshProUGUI Empty;
            public GameObject Placeholder;
        }

        /// <summary>
        /// 수선점 기록 탭 (SPEC4 §6.1, RecordsPanel 안 — Body Stretch(0, 0, 90, 0)): Summary Top(0, 150) 금테 판 — 메달 96 (UiArt.Medal(Gold), (24, 0)) ·
        /// CountText '모은 기록 0/0' 32 Cream 굵게 (140, −24) · Bar 520×20 Lavender (140, −96) · ClaimableText 22 SubText (오른쪽 위);
        /// Segments [진행 중][완료] 400×64 (y 166); List 스크롤 Stretch(0, 0, 246, 128) (줄 틀은 미션 줄 — JournalPopup이 같은 틀로 만든다) · EmptyText.
        /// 예전 '곧 열려요' 카드는 Placeholder로 남겨 꺼 둔다 (기록 시스템이 없을 때만 보인다).
        /// </summary>
        static RecordsParts BuildRecordsPanel(RectTransform records)
        {
            var parts = new RecordsParts();
            var placeholder = NewRect("Placeholder", records).Stretch();
            var recordCard = FramedPanel(placeholder, "Card", MetaUi.Alpha(UITheme.PanelLight, 0.7f));
            recordCard.rectTransform.At(Center, new Vector2(0, 80), new Vector2(760, 420));
            var recordIcon = Img(recordCard.transform, "Icon", Color.white, _icons.Journal != null ? _icons.Journal : _icons.Star);
            recordIcon.preserveAspect = true;
            recordIcon.rectTransform.At(TopCenter, new Vector2(0, -40), new Vector2(140, 140));
            Txt(recordCard.transform, "Title", "수선점 기록", 38, UITheme.Cream, TextAlignmentOptions.Center, true).rectTransform.Top(200, 56);
            Txt(recordCard.transform, "Body", "수선점이 걸어온 발자취를 모아 보여 드릴게요.\n곧 열려요!", 26, UITheme.SubText, TextAlignmentOptions.Top, false, true)
                .rectTransform.Top(270, 120, 40, 40);
            placeholder.gameObject.SetActive(false);
            parts.Placeholder = placeholder.gameObject;

            var summary = FramedPanel(records, "Summary", MetaUi.Alpha(UITheme.PanelLight, 0.75f));
            summary.rectTransform.Top(0, 150);
            var medal = Img(summary.transform, "Medal", Color.white, UiArt.Medal(UiArt.MedalKind.Gold));
            medal.preserveAspect = true;
            medal.rectTransform.At(MiddleLeft, new Vector2(24, 0), new Vector2(96, 96));
            parts.Count = Txt(summary.transform, "CountText", "모은 기록 0/0", UIType.H2, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Title);
            parts.Count.rectTransform.At(TopLeft, new Vector2(140, -24), new Vector2(420, 44));
            var bar = Bar(summary.transform, "Bar", 20, UITheme.Lavender, out parts.BarFill);
            bar.rectTransform.At(TopLeft, new Vector2(140, -96), new Vector2(520, 20));
            parts.Claimable = Txt(summary.transform, "ClaimableText", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineRight)
                .FitFloor(UIType.HintMin);
            parts.Claimable.rectTransform.At(TopRight, new Vector2(-24, -28), new Vector2(260, 34));

            parts.Segments = Segmented(records, "Segments", new[] { "진행 중", "완료" }, UIType.Label);
            ((RectTransform)parts.Segments.transform).At(TopLeft, new Vector2(0, -166), new Vector2(400, 64));

            var listRoot = NewRect("List", records).Stretch(0, 0, 246, 128);
            parts.ListContent = ScrollList(listRoot, out parts.Scroll);
            AddVerticalList(parts.ListContent, 14, new RectOffset(4, 4, 4, 16));
            parts.Empty = Txt(listRoot, "EmptyText", "모든 기록을 다 모았어요!", 28, UITheme.SubText);
            parts.Empty.rectTransform.Stretch();
            parts.Empty.gameObject.SetActive(false);
            return parts;
        }

        /// <summary>
        /// 첫 주 탭 (SPEC4 §13.6, StarterPanel 안): Header Top(0, 56) — '새내기 수선공의 첫 주' 32 Cream 굵게 + 'D-n' 알약 72×28 (밤 알약, 18 Gold) ·
        /// Sub Top(56, 28) '일주일 동안 매일 새 할 일이 열려요' 20 SubText; ScoreCard Top(92, 230) 금테 판 — PointsText '0점' 48 Cream (24, −72) ·
        /// Bar 480×22 (300, −150) 안 Chests (미션 상자 틀 — JournalPopup이 같은 틀로 만든다, 30 · 60 · 90 · 120 · 150점);
        /// Days Top(334, 64): Day1~7 112×64 간격 8 (Option 칩 '1일'…'7일' · 자물쇠 24 · 점), List 스크롤 Stretch(0, 0, 410, 128) · EmptyText.
        /// 루트에 StarterWeekPanelView (상자 틀 · 줄 틀은 일지 것을 같이 쓴다 — 별빛은 BuildJournalPopup이 나중에 묶는다).
        /// </summary>
        static StarterWeekPanelView BuildStarterPanel(RectTransform starter, MissionChestView chestTemplate, MissionRowView rowTemplate)
        {
            var view = starter.gameObject.AddComponent<StarterWeekPanelView>();
            var parts = new StarterLocal();
            var title = Txt(starter, "TitleText", "새내기 수선공의 첫 주", UIType.H2, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Title);
            title.rectTransform.At(TopLeft, new Vector2(4, 0), new Vector2(560, 52));
            var pill = UiArt.Panel(starter, "DaysPill", UiArt.PillNight9s, UiArt.CapsulePpu(28), MetaUi.Alpha(Color.white, 0.94f),
                MetaUi.Alpha(UITheme.Background, 0.86f));
            pill.rectTransform.At(TopLeft, new Vector2(572, -12), new Vector2(72, 28));
            parts.Days = Txt(pill.transform, "Text", "D-14", UIType.Micro, UITheme.Gold, TextAlignmentOptions.Center, true).FitFloor(UIType.AutoSizeFloor);
            parts.Days.rectTransform.Stretch(4, 4, 0, 0);
            var sub = Txt(starter, "SubText", "일주일 동안 매일 새 할 일이 열려요", UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft).FitFloor(UIType.HintMin);
            sub.rectTransform.Top(56, 28, 4, 4);

            var card = FramedPanel(starter, "ScoreCard", MetaUi.Alpha(UITheme.PanelLight, 0.75f));
            card.rectTransform.Top(92, 230);
            var label = Txt(card.transform, "ScoreLabel", "첫 주 점수", 26, UITheme.SubText, TextAlignmentOptions.MidlineLeft, true);
            label.rectTransform.At(TopLeft, new Vector2(28, -22), new Vector2(240, 40));
            parts.Points = Txt(card.transform, "PointsText", "0점", 48, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.H2);
            parts.Points.rectTransform.At(TopLeft, new Vector2(24, -72), new Vector2(240, 84));
            var bar = Bar(card.transform, "Bar", 22, UITheme.Lavender, out parts.BarFill);
            bar.rectTransform.At(TopLeft, new Vector2(300, -150), new Vector2(480, 22));
            parts.Bar = bar.rectTransform;
            parts.ChestRoot = NewRect("Chests", bar.transform).Stretch();

            const int days = JournalPopup.StarterDays;
            parts.DayButtons = new Object[days];
            parts.DayImages = new Object[days];
            parts.DayLabels = new Object[days];
            parts.DayLocks = new Object[days];
            parts.DayDots = new Object[days];
            var pillArt = UiArt.PillWhite9s;
            for (int i = 0; i < days; i++)
            {
                var button = Btn(starter, $"Day{i + 1}", $"{i + 1}일", UITheme.Secondary, UIType.Label, out var dayLabel);
                ((RectTransform)button.transform).At(TopLeft, new Vector2(i * 120, -334), new Vector2(112, 64));
                var image = (Image)button.targetGraphic;
                if (pillArt != null)
                {
                    image.sprite = pillArt;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = UiArt.CapsulePpu(64);
                }
                image.color = UITheme.Secondary;
                var edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = MetaUi.Alpha(UITheme.Cream, 0.6f);
                edge.effectDistance = new Vector2(2f, -2f);
                edge.enabled = false;
                image.HitPad(new Vector2(112, 64), 0f, UISize.MinHit, 4f, 4f);
                dayLabel.color = UITheme.SubText;
                Fit(dayLabel, UIType.Hint);
                var lockIcon = Img(button.transform, "Lock", Color.white, _icons.Lock);
                lockIcon.preserveAspect = true;
                lockIcon.rectTransform.At(TopLeft, new Vector2(-4, 6), new Vector2(24, 24));
                lockIcon.gameObject.SetActive(false);
                var dot = RedDotImage(button.transform, "Dot");
                dot.rectTransform.At(TopRight, new Vector2(6, 6), new Vector2(UISize.BadgeDot, UISize.BadgeDot));
                dot.gameObject.SetActive(false);
                parts.DayButtons[i] = button;
                parts.DayImages[i] = image;
                parts.DayLabels[i] = dayLabel;
                parts.DayLocks[i] = lockIcon.gameObject;
                parts.DayDots[i] = dot.gameObject;
            }

            var listRoot = NewRect("List", starter).Stretch(0, 0, 410, 128);
            parts.ListContent = ScrollList(listRoot, out parts.Scroll);
            AddVerticalList(parts.ListContent, 14, new RectOffset(4, 4, 4, 16));
            parts.Empty = Txt(listRoot, "EmptyText", "이 날의 할 일이 아직 없어요", 28, UITheme.SubText);
            parts.Empty.rectTransform.Stretch();
            parts.Empty.gameObject.SetActive(false);

            new Binder(view)
                .Set("_daysText", parts.Days)
                .Set("_pointsText", parts.Points)
                .Set("_bar", parts.Bar)
                .Set("_barFill", parts.BarFill)
                .Set("_chestRoot", parts.ChestRoot)
                .Set("_chestTemplate", chestTemplate)
                .Set("_rowTemplate", rowTemplate)
                .SetArray("_dayButtons", parts.DayButtons)
                .SetArray("_dayImages", parts.DayImages)
                .SetArray("_dayLabels", parts.DayLabels)
                .SetArray("_dayLocks", parts.DayLocks)
                .SetArray("_dayDots", parts.DayDots)
                .Set("_scroll", parts.Scroll)
                .Set("_listContent", parts.ListContent)
                .Set("_emptyText", parts.Empty)
                .Apply();
            return view;
        }

        /// <summary>BuildStarterPanel 안에서 모으는 부품</summary>
        struct StarterLocal
        {
            public TextMeshProUGUI Days;
            public TextMeshProUGUI Points;
            public RectTransform Bar;
            public Image BarFill;
            public RectTransform ChestRoot;
            public Object[] DayButtons;
            public Object[] DayImages;
            public Object[] DayLabels;
            public Object[] DayLocks;
            public Object[] DayDots;
            public ScrollRect Scroll;
            public RectTransform ListContent;
            public TextMeshProUGUI Empty;
        }

        // ═════════════════════════ 별빛 산책 수첩 (Q-06 스트레치) ═════════════════════════

        /// <summary>
        /// 별빛 산책 수첩 Popups/SeasonPassPopup 980×1500 (SPEC4 §8.4). Body(908×1328): Banner Top(0, 300) 덮어 맞춤 (UiArt.SeasonBanner),
        /// Header Top(310, 130) 금테 판 — LevelText 'Lv.0 · 0/90' 32 Cream 굵게 · Bar 600×20 · DaysText 24 (오른쪽) · SourceText Hint 20 SubText,
        /// List 스크롤 Stretch(0, 0, 452, 128) (미션 줄 틀 — 칸 아이콘 UiArt.MedallionDark, 30칸 의상 칸은 UiArt.StarwalkCostume — RM-3 R65)
        /// · EmptyText, 아래 오른쪽 ClaimAllButton 380×96.
        /// </summary>
        static SeasonPassPopup BuildSeasonPassPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<SeasonPassPopup>(ctx.PopupRoot, "SeasonPassPopup", "별빛 산책 수첩", new Vector2(980, 1500), out var body, out _);
            var bannerArt = UiArt.SeasonBanner;
            var banner = CoverArt(body, "Banner", bannerArt, NightPanelColor, 1024f / 384f, out var bannerImage);
            banner.Top(0, 300);

            var header = FramedPanel(body, "Header", MetaUi.Alpha(UITheme.PanelLight, 0.75f));
            header.rectTransform.Top(310, 130);
            var level = Txt(header.transform, "LevelText", "Lv.0 · 0/90", UIType.H2, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Title);
            level.rectTransform.At(TopLeft, new Vector2(28, -14), new Vector2(520, 44));
            var days = Txt(header.transform, "DaysText", "남은 기간 0일", UIType.Label, UITheme.Gold, TextAlignmentOptions.MidlineRight, true).FitFloor(UIType.Hint);
            days.rectTransform.At(TopRight, new Vector2(-28, -18), new Vector2(260, 36));
            var bar = Bar(header.transform, "Bar", 20, UITheme.Lavender, out var fill);
            bar.rectTransform.At(TopLeft, new Vector2(28, -66), new Vector2(852, 20));
            var source = Txt(header.transform, "SourceText", SeasonPassPopup.SourceLine, UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft)
                .FitFloor(UIType.HintMin);
            source.rectTransform.At(TopLeft, new Vector2(28, -92), new Vector2(852, 28));

            var listRoot = NewRect("List", body).Stretch(0, 0, 452, 128);
            var content = ScrollList(listRoot, out var scroll);
            AddVerticalList(content, 14, new RectOffset(4, 4, 4, 16));
            var row = BuildMissionRowTemplate(content);
            var empty = Txt(listRoot, "EmptyText", "이번 수첩은 아직 준비 중이에요", 28, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.gameObject.SetActive(false);

            var claimAll = ClaimAllButton(body, UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight, UISize.ClaimAllFooterText, out _, out var claimBadge);
            ((RectTransform)claimAll.transform).At(BottomRight, new Vector2(0, 8), new Vector2(UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight));
            claimAll.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_banner", bannerImage)
                .Set("_levelText", level)
                .Set("_barFill", fill)
                .Set("_daysText", days)
                .Set("_sourceText", source)
                .Set("_scroll", scroll)
                .Set("_listContent", content)
                .Set("_rowTemplate", row)
                .Set("_tierIcon", UiArt.MedallionDark)
                .Set("_tier30Icon", UiArt.StarwalkCostume)
                .Set("_emptyText", empty)
                .Set("_claimAllButton", claimAll)
                .Set("_claimAllBadge", claimBadge)
                .Apply();
            return popup;
        }
    }
}
