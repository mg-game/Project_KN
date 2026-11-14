using System.Collections.Generic;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static IdleGame.EditorTools.SceneParts;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 메인 씬(세로 1080x1920 기준)과 UI 프리팹을 생성한다.
    /// 화면 틀 (UX-SPEC-IA §1.1): 상단 바 130 / 전투 화면 130~1750 (MAIN · SHEET 모두 세로 전체) / 하단 탭 바 170 (3 + 사냥 + 3)
    /// + 시트 = 머리 56 + 탭 내용 780 (탭을 고르면 탭 바 뒤에서 올라와 전투 아래쪽을 잠깐 덮는다 — 처음엔 꺼져 있다, MAIN)
    /// + 팝업들 + 튜토리얼·팡파르 덮개 + 토스트. 틀의 뼈대(시트 영역 · 시트 머리 · 7칸 탭 바)는 여기서 만든다 (CR-10).
    /// 상단 바 · ≡ 메뉴는 FrameSceneBuilder, 화면별 내용은 BattleSceneBuilder / CharacterSceneBuilder / MenuSceneBuilder가 만들고,
    /// 메타(가이드 퀘스트·바구니·빠른 아이콘·복귀·개방 카드)는 MetaSceneBuilder, 설정 팝업은 SettingsSceneBuilder,
    /// 튜토리얼·보상 날아가기는 TutorialSceneBuilder가 만든다.
    /// <para>
    /// 캔버스 층 (UX-SPEC §3.1, 아래 → 위): Background → SafeArea(전투 · 시트(중첩 캔버스) · 상단 바 · 탭 바 — 형제 순서 그대로라
    /// 시트는 탭 바 뒤에서 나오고 상단 바 아래에 머문다) → Popups → TooltipLayer(안내 말풍선, 안전 영역) → TutorialOverlay → FanfareLayer
    /// → SystemPopups(확인 · 부족 창) → ToastLayer(토스트, 안전 영역).
    /// 빌드 끝에 글자 크기 하한(M23) · 누르는 자리(M22)를 검사해 경고만 남긴다.
    /// </para>
    /// </summary>
    public static class IdleSceneBuilder
    {
        public const string ScenePath = IdleProjectSetup.Root + "/Scenes/Main.unity";
        const string PrefabDir = IdleProjectSetup.Root + "/Prefabs/UI";
        const string SpriteDir = IdleProjectSetup.Root + "/Sprites/UI";

        const float TopBarHeight = SceneBuildContext.TopBarHeight;
        const float TabBarHeight = SceneBuildContext.TabBarHeight;
        const float ContentHeight = SceneBuildContext.ContentHeight;

        /// <param name="scene">비어 있는 새 씬. 에셋을 로드하기 전에 만들어 둬야 참조가 유지된다.</param>
        public static void Build(Scene scene, TMP_FontAsset font, GameContent content)
        {
            DefaultFont = font;
            RoundSprite = RoundedSprite($"{SpriteDir}/UI_Round.png", 64, 24, true);
            CircleSprite = RoundedSprite($"{SpriteDir}/UI_Circle.png", 128, 64, false);
            BuildInlineSprites();

            var ctx = new SceneBuildContext { Content = content };
            ctx.ItemSlotPrefab = BuildItemSlotPrefab();
            var mailItem = BuildMailItemPrefab();
            var noticeItem = BuildNoticeItemPrefab();

            BuildCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvas = BuildCanvas(out var ui);
            ctx.Canvas = canvas;
            ctx.UI = ui;
            var background = ArtLib.UI("bg_night");
            var bgImage = background != null
                ? Img(canvas, "Background", Color.white, background)
                : Img(canvas, "Background", UITheme.Background);
            bgImage.rectTransform.Stretch();
            // 19.5:9 · 20:9 화면에서도 늘어나지 않게 비율을 지킨 채 화면을 덮는다 (넘치는 양옆은 화면 밖)
            if (background != null) Envelope(bgImage, background);

            var safeArea = NewRect("SafeArea", canvas).Stretch();
            safeArea.gameObject.AddComponent<SafeArea>();
            ctx.SafeArea = safeArea;
            // 화면 틀 (UX-SPEC-IA §1.1): 전투는 상단 바와 탭 바 사이 세로 전체 (130~1750) — 시트가 열리면 그 아래쪽을 덮는다. 형제 순서는 그대로
            ctx.StageArea = NewRect("StageArea", safeArea).Stretch(0, 0, TopBarHeight, TabBarHeight);
            ctx.ContentArea = BuildContentArea(safeArea);
            // 시트 머리 (§4.2): ContentArea 위 끝, 패널보다 앞 형제 — 돌아가기 칩(M72)도 여기 산다
            ctx.SheetHeader = BuildSheetHeader(ctx.ContentArea, out var returnChip);
            ctx.PopupRoot = NewRect("Popups", canvas).Stretch();
            // 안내 말풍선 층 (M59): 팝업 바로 위 — 튜토리얼 덮개 · 팡파르 층은 BuildOverlays가 이 위에 만든다
            ctx.TooltipLayer = SafeLayer(canvas, "TooltipLayer");
            ctx.InfoTooltip = BuildInfoTooltip(ctx.TooltipLayer);

            // 상단 바 (UX-SPEC-IA §3.1 — 틀 빌더가 만들고 돌려준다)
            ctx.TopBar = FrameSceneBuilder.BuildTopBar(ctx);
            BattleSceneBuilder.Build(ctx);
            ctx.BattleRoot = ctx.StageArea.Find("Battle") as RectTransform;

            // 탭 순서 = MainTab (탭 바는 가운데에 사냥을 끼워 3 + 1 + 3)
            var growthPanel = MenuSceneBuilder.BuildGrowthPanel(ctx);
            var formationPanel = MenuSceneBuilder.BuildFormationPanel(ctx);
            var characterPanel = CharacterSceneBuilder.BuildCharacterPanel(ctx);
            var expeditionPanel = MenuSceneBuilder.BuildExpeditionPanel(ctx);
            var gachaPanel = CharacterSceneBuilder.BuildGachaPanel(ctx);
            var shopPanel = MenuSceneBuilder.BuildShopPanel(ctx);
            BuildTabBar(ctx,
                ("강화", "growth", growthPanel),
                ("편성", "formation", formationPanel),
                ("친구들", "characters", characterPanel),
                ("원정", "expedition", expeditionPanel),
                ("소환", "gacha", gachaPanel),
                ("상점", "shop", shopPanel));
            // 메타 HUD (가이드 퀘스트 줄 / 수선 바구니 / 빠른 아이콘 / 탭 잠금 · 사냥 점) — 상단 바·전투 화면·탭 바가 있어야 한다
            MetaSceneBuilder.BuildHud(ctx);

            CharacterSceneBuilder.BuildPopups(ctx);
            MenuSceneBuilder.BuildPopups(ctx);
            // ≡ 메뉴 (UX-SPEC-IA §3.6 — ctx.Popups에 스스로 넣는다)
            FrameSceneBuilder.BuildMenuPopup(ctx);
            ctx.Popups.Add(BuildMailPopup(ctx.PopupRoot, mailItem));
            ctx.Popups.Add(BuildNoticePopup(ctx.PopupRoot, noticeItem));
            ctx.Popups.Add(BuildOfflineRewardPopup(ctx.PopupRoot));
            ctx.Popups.Add(SettingsSceneBuilder.BuildSettingsPopup(ctx));
            MetaSceneBuilder.BuildPopups(ctx);
            foreach (var popup in ctx.Popups) popup.gameObject.SetActive(false);
            // 튜토리얼 코치마크 / 보상 날아가기는 팝업 위, 시스템 팝업 · 토스트 아래
            TutorialSceneBuilder.BuildOverlays(ctx);

            // 시스템 팝업 층 (§3.1 · M15): 확인 · 부족 창은 튜토리얼 덮개 · 동화 인트로 · 팡파르 위에 뜬다 (뒤에 숨어 뜨지 않게)
            ctx.SystemPopupRoot = NewRect("SystemPopups", canvas).Stretch();
            foreach (var popup in new UIPopup[] { BuildMessagePopup(ctx.SystemPopupRoot), BuildShortfallPopup(ctx.SystemPopupRoot) })
            {
                ctx.Popups.Add(popup);
                popup.gameObject.SetActive(false);
            }
            // 토스트 층 (§3.1 · M14): 맨 위, 안전 영역
            ctx.ToastLayer = SafeLayer(canvas, "ToastLayer");
            var toast = BuildToast(ctx.ToastLayer);
            // 뒤로가기는 돌아가기 칩이 보일 때만 돌아간다 (§4.7)
            new Binder(ui).SetArray("_popups", ctx.Popups.ToArray()).Set("_toast", toast).Set("_returnChip", returnChip).Apply();
            // 돌아가기 칩은 코치마크가 보이는 동안 숨고, 시트 자동 복귀 시계는 0에 묶인다 — 코치마크는 BuildOverlays가 만들므로 여기서 잇는다
            var coach = canvas.GetComponentInChildren<CoachMarkView>(true);
            new Binder(returnChip).Set("_coach", coach).Apply();
            if (ctx.Sheet != null) new Binder(ctx.Sheet).Set("_coach", coach).Apply();

            var bootstrap = new GameObject("[Game]").AddComponent<GameBootstrap>();
            new Binder(bootstrap)
                .Set("_config", content.Config)
                .Set("_itemDatabase", content.Items)
                .Set("_characterDatabase", content.Characters)
                .SetArray("_gachaTables", System.Linq.Enumerable.ToArray(content.GachaTables))
                .Set("_battleConfig", content.Battle)
                .Set("_expeditionDatabase", content.Expeditions)
                .Set("_shopDatabase", content.Shops)
                .Set("_synergyDatabase", content.Synergies)
                .Set("_metaDatabase", content.Meta)
                .Set("_ui", ui)
                .Apply();

            // 빌드 끝 검사 (경고만): 글자 크기 하한 (M23) · 누르는 자리 88 (M22)
            LintText(canvas);
            LintHitTargets(canvas);

            // 시트는 닫힌 채 시작한다 (MAIN — UX-SPEC-IA §1.2): 열린 자리(970~1750) 그대로 꺼 둔다. 다른 빌더가 다 만든 뒤에 끈다
            // (패널 Awake · OnEnable은 시트가 처음 열릴 때. SheetController가 실행할 때 닫힌 자리로 옮긴다)
            ctx.ContentArea.gameObject.SetActive(false);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }


        // ───────────── 글자 속 그림 (TMP 스프라이트) ─────────────

        const string InlineSpriteAssetPath = IdleProjectSetup.Root + "/Generated/UiInlineSprites.asset";
        const string InlineAtlasPath = IdleProjectSetup.Root + "/Generated/UiInlineSprites.png";
        /// <summary>글자 속 그림 한 칸 크기 (Icons/*.png 원본 256px 그대로)</summary>
        const int InlineCell = 256;
        /// <summary>글자 속 그림 이름 = Icons/{이름}.png (&lt;sprite name="star"&gt;)</summary>
        static readonly string[] InlineSpriteNames = { "star", "affinity" };

        /// <summary>
        /// TMP 글자 속 그림 묶음 (Generated/UiInlineSprites): star = Icons/star, affinity = Icons/affinity.
        /// 그림을 한 장(가로로 붙인 아틀라스, 밉맵)에 모아 TMP 스프라이트 에셋을 만들고 TMP 기본 스프라이트 에셋으로 지정한다.
        /// → UITheme.Stars가 내는 &lt;sprite name="star"&gt;가 모든 글자에서 힉스필드 별 그림으로 보인다.
        /// 원래 기본 스프라이트 에셋(이모지)은 대체 목록에 남긴다. 아이콘이 없으면 만들지 않는다 (경고).
        /// </summary>
        static void BuildInlineSprites()
        {
            var sources = new Texture2D[InlineSpriteNames.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                string file = $"{ArtLib.Root}/Icons/{InlineSpriteNames[i]}.png";
                if (!System.IO.File.Exists(file))
                {
                    Debug.LogWarning($"[IdleSceneBuilder] {file}이 없어 글자 속 그림(UiInlineSprites)을 만들지 않습니다.");
                    foreach (var made in sources) if (made != null) Object.DestroyImmediate(made);
                    return;
                }
                sources[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                sources[i].LoadImage(System.IO.File.ReadAllBytes(file));
            }

            // 아틀라스: 칸마다 원본을 그대로(크기가 다르면 바이리니어로 맞춰) 붙인다
            var atlas = new Texture2D(InlineCell * sources.Length, InlineCell, TextureFormat.RGBA32, false);
            var pixels = new Color32[atlas.width * atlas.height];
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                for (int y = 0; y < InlineCell; y++)
                {
                    for (int x = 0; x < InlineCell; x++)
                    {
                        Color32 color = source.width == InlineCell && source.height == InlineCell
                            ? source.GetPixel(x, y)
                            : source.GetPixelBilinear((x + 0.5f) / InlineCell, (y + 0.5f) / InlineCell);
                        pixels[y * atlas.width + i * InlineCell + x] = color;
                    }
                }
                Object.DestroyImmediate(source);
            }
            atlas.SetPixels32(pixels);
            atlas.Apply();
            UiArt.EnsureGeneratedFolder();
            System.IO.File.WriteAllBytes(InlineAtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(InlineAtlasPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(InlineAtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapBias = -0.3f;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(InlineAtlasPath);

            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(InlineSpriteAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, InlineSpriteAssetPath);
            }
            // 버전을 먼저 적어야 표를 읽을 때 옛 형식 변환(UpgradeSpriteAsset)이 돌지 않는다
            var so = new SerializedObject(asset);
            so.FindProperty("m_Version").stringValue = "1.1.0";
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
            asset.spriteSheet = sheet;

            // 글자 높이(폰트 ascender)에 맞춰 그리고 0.85배로 줄여 한글과 크기를 맞춘다. 아래 12%는 기준선 아래로.
            var glyphs = asset.spriteGlyphTable;
            var characters = asset.spriteCharacterTable;
            glyphs.Clear();
            characters.Clear();
            for (int i = 0; i < InlineSpriteNames.Length; i++)
            {
                var glyph = new TMP_SpriteGlyph((uint)i,
                    new UnityEngine.TextCore.GlyphMetrics(InlineCell, InlineCell, 0f, InlineCell * 0.88f, InlineCell * 1.06f),
                    new UnityEngine.TextCore.GlyphRect(i * InlineCell, 0, InlineCell, InlineCell), 0.85f, 0);
                glyphs.Add(glyph);
                characters.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = InlineSpriteNames[i], scale = 1f });
            }

            var material = asset.material;
            if (material == null)
            {
                material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "UiInlineSprites Material" };
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            material.SetTexture(ShaderUtilities.ID_MainTex, sheet);
            EditorUtility.SetDirty(material);

            var previous = TMP_Settings.defaultSpriteAsset;
            if (previous != null && previous != asset) asset.fallbackSpriteAssets = new System.Collections.Generic.List<TMP_SpriteAsset> { previous };
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);

            if (TMP_Settings.instance != null && TMP_Settings.defaultSpriteAsset != asset)
            {
                TMP_Settings.defaultSpriteAsset = asset;
                EditorUtility.SetDirty(TMP_Settings.instance);
            }
            AssetDatabase.SaveAssets();
        }

        // ───────────── 씬 기본 ─────────────

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UITheme.Background;
            camera.orthographic = true;
            go.transform.position = new Vector3(0, 0, -10);
        }

        static RectTransform BuildCanvas(out UIManager ui)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            {
                layer = LayerMask.NameToLayer("UI"),
            };
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<CanvasScalerAdapter>();

            ui = go.AddComponent<UIManager>();
            return (RectTransform)go.transform;
        }

        /// <summary>캔버스를 꽉 채우고 안전 영역(노치 · 홈 표시줄)을 따르는 층 (안내 말풍선 · 토스트)</summary>
        static RectTransform SafeLayer(RectTransform canvas, string name)
        {
            var layer = NewRect(name, canvas).Stretch();
            layer.gameObject.AddComponent<SafeArea>();
            return layer;
        }


        // ───────────── 탭 바 (UX-SPEC-IA §2 — 3 + 사냥 + 3) ─────────────

        /// <summary>탭 바 아래로 이어 칠하는 띠 높이 (어떤 홈 표시줄 여백보다 크다 — 약 34pt ≈ 100 캔버스 단위)</summary>
        const float BottomBleedHeight = 400f;
        /// <summary>선택 탭 위 막대 (탭 바 위 끝, M52 — MAIN에서는 보이지 않는다)</summary>
        static readonly Vector2 IndicatorSize = new(104, 6);
        /// <summary>강화 탭 아이콘 밑 금빛 크기 · 처음 알파 (M35d — 숨쉬기는 MainTabController)</summary>
        const float GrowthGlowSize = 120f;
        const float GrowthGlowAlpha = 0.45f;
        /// <summary>탭 아이콘 자리 (TopCenter 기준) · 크기</summary>
        static readonly Vector2 TabIconPosition = new(0, -14);
        const float TabIconSize = 92f;
        /// <summary>전용 탭 바 그림 높이 (navbar_9s 720x340) — Tiled 배율 340 / 170 = 2: 한 장 높이라 무늬가 오른쪽 끝까지 이어진다 (B5)</summary>
        const float NavBarArtHeight = 340f;
        /// <summary>사냥 칸 (§2.2): 메달 위 가운데에서 2 아래 · 아이콘 21 아래 · 이름 받침 아래에서 10 위 · 모아 보는 점 (58, −14) 30</summary>
        const float MainMedallionTop = 2f;
        const float MainIconTop = 21f;
        const float MainLabelBottom = 10f;
        static readonly Vector2 MainDotPosition = new(58, -14);
        const float MainDotSize = 30f;
        /// <summary>사냥 빛 처음 알파 (Lamp@0.45, 멈춰 있다 — MAIN에서만, MainTabController가 끄고 켠다)</summary>
        const float MainGlowAlpha = 0.45f;
        /// <summary>메달 뒤 실 고리 (B5 시안: 172, 12° 돌림 — MAIN 0.55 / SHEET 0.3은 MainTabController)</summary>
        const float MainRingSize = 172f;
        const float MainRingAngle = 12f;
        const float MainRingAlpha = 0.55f;

        /// <summary>
        /// 하단 탭 바 (UX-SPEC-IA §2): 홈 표시줄 띠(BottomBleed, M68) → 벨벳 띠(전용 navbar_9s면 Tiled 배율 2, 없거나 테두리가 양 끝 장식이 아니면 벨벳 띠를 뒤집어)
        /// → 7칸 [강화 · 편성 · 친구들 | 사냥(Tab_Main 192) | 원정 · 소환 · 상점] (옆 칸 148 — 6·148 + 192 = 1080, 넓은 화면은 같은 비율로 늘어난다)
        /// → 선택 막대(Indicator, M52). 옆 탭 = 받침 · [강화: 금빛] · 이름 · 아이콘 (지금 모양 그대로, 좁아졌을 뿐). 사냥 칸은 BuildMainTab.
        /// 탭 바에는 MainTabController와 SheetController가 함께 산다 (시트 머리 · ContentArea를 여기서 잇는다).
        /// 탭 패널 루트에는 바꿀 때 페이드용 CanvasGroup을 붙인다. 옆 탭 버튼 · 패널 이름(코치 경로)은 그대로다.
        /// ctx.TabButtons는 옆 탭 6개(순서 = MainTab), 사냥은 ctx.TabMain (TabGate 배열에 넣지 않는다).
        /// </summary>
        static void BuildTabBar(SceneBuildContext ctx, params (string label, string icon, GameObject panel)[] tabs)
        {
            var bar = NewRect("TabBar", ctx.SafeArea).Bottom(0, TabBarHeight);
            // 홈 표시줄 띠 (M68): 탭 바 아래로 400 — 노치 · 홈 표시줄 기기에서 밤 배경 그림이 탭 바 밑으로 비치지 않게. 맨 뒤, 레이아웃 제외
            var bleed = Img(bar, "BottomBleed", UITheme.TabNormal);
            var bleedRect = bleed.rectTransform;
            bleedRect.anchorMin = Vector2.zero;
            bleedRect.anchorMax = new Vector2(1f, 0f);
            bleedRect.pivot = new Vector2(0.5f, 1f);
            bleedRect.anchoredPosition = Vector2.zero;
            bleedRect.sizeDelta = new Vector2(0f, BottomBleedHeight);
            bleed.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // 벨벳 띠: 전용 탭 바(위에 금 끈 · 스캘럽, 아래 84줄이 BottomBleed 색)는 뒤집지 않고 Tiled 배율 2.
            // 없으면 상단 바와 같은 벨벳 띠를 세로로 뒤집어 금 솔기를 위로. 레이아웃 칸에서 뺀다.
            // 전용 그림의 테두리가 양 끝 장식(180, 0, 180, 0)이 아니면(임포트 규칙 BorderOverrides가 없어 48 테두리로 들어옴 —
            // Tiled가 끝 장식 · 위아래 띠를 띠 가운데에 되풀이한다) 상단 바(FrameSceneBuilder.BuildVelvet)처럼 벨벳 띠로 대신하고 경고 한 번
            var navArt = UiArt.NavBar9s(out bool dedicatedNav);
            if (dedicatedNav && !HasEndCapsOnly(navArt))
            {
                Debug.LogWarning($"[IdleSceneBuilder] Art/UI/navbar_9s.png의 9-slice 테두리 {navArt.border}(왼쪽, 아래, 오른쪽, 위)가 양 끝 장식이 아니라 "
                                 + "예전 벨벳 띠로 대신합니다 — ArtImportPostprocessor.BorderOverrides에 navbar_9s = (180, 0, 180, 0)을 넣고 GetVersion()을 올려 다시 임포트하세요.");
                navArt = UiArt.BarVelvet9s;
                dedicatedNav = false;
            }
            Image velvet;
            if (dedicatedNav)
            {
                velvet = Img(bar, "Velvet", Color.white, navArt);
                velvet.type = Image.Type.Tiled;
                velvet.pixelsPerUnitMultiplier = NavBarArtHeight / TabBarHeight;
            }
            else
            {
                velvet = UiArt.Panel(bar, "Velvet", navArt, 1.5f, null, UITheme.TabNormal);
                if (UiArt.IsArt(navArt)) velvet.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            }
            velvet.raycastTarget = false;
            velvet.rectTransform.Stretch();
            velvet.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // 7칸 줄: 칸 폭은 LayoutElement (옆 148 · 가운데 192), 남는 폭은 같은 비율로 나눈다
            var row = AddRow(bar, 0);
            row.childForceExpandWidth = false;
            var controller = bar.gameObject.AddComponent<MainTabController>();
            var sheet = bar.gameObject.AddComponent<SheetController>();
            ctx.TabBar = bar;
            ctx.TabController = controller;
            ctx.Sheet = sheet;
            ctx.TabButtons = new Button[tabs.Length];

            var buttons = new Object[tabs.Length];
            var panels = new Object[tabs.Length];
            var plates = new Object[tabs.Length];
            var panelGroups = new Object[tabs.Length];
            Image growthGlow = null;
            var plateArt = UiArt.TabSelected;
            // 사냥 칸은 가운데 (옆 탭 3 | 사냥 | 3 — MainTab 순서는 그대로)
            int mainSlot = tabs.Length / 2;
            Button mainButton = null;
            Image mainGlow = null, mainRing = null;
            TextMeshProUGUI mainLabel = null;
            Graphic mainDim = null;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (i == mainSlot) mainButton = BuildMainTab(bar, out mainGlow, out mainRing, out mainLabel, out mainDim);
                var button = Btn(bar, $"Tab_{tabs[i].panel.name}", tabs[i].label, UITheme.TabNormal, 28, out var label, rounded: false);
                SetSlotWidth(button.gameObject, UISize.TabSideWidth);
                // 누르는 자리만 (투명) — 선택 표시는 SelectedPlate를 켜고 끈다. 탭 소리는 MainTabController(Tab)가 낸다
                button.targetGraphic.color = new Color(1f, 1f, 1f, 0f);
                button.transition = Selectable.Transition.None;
                MuteTapSound(button);
                label.rectTransform.Bottom(18, 44);
                UiArt.Outline(label);

                // 선택 받침 (아이콘 뒤): 위가 둥근 보라 판 — 그림이면 0.75로 눌러 Cream 이름이 받침 보라 위에서도 읽힌다 (외곽선은 그대로)
                bool plateHasArt = UiArt.IsArt(plateArt);
                var plate = Img(button.transform, "SelectedPlate", plateHasArt ? SelectedPlateArtTint : UITheme.TabSelected, plateArt);
                if (plateHasArt)
                {
                    plate.type = Image.Type.Simple;
                    plate.preserveAspect = true;
                }
                plate.rectTransform.Stretch(6, 6, 2, 0);
                plate.transform.SetAsFirstSibling();
                plate.SetInactive();
                plates[i] = plate.gameObject;

                // 강화 탭: 아이콘 밑 금빛 (받침 바로 위, 이름 · 아이콘 · 잠금 점 아래 — 꺼 둔다)
                if (i == (int)MainTab.Growth) growthGlow = BuildGrowthGlow(button.transform);

                var iconArt = ArtLib.UI($"tab_{tabs[i].icon}");
                var icon = Img(button.transform, "Icon", iconArt != null ? Color.white : UITheme.SubText, iconArt != null ? iconArt : UiArt.Star);
                icon.preserveAspect = true;
                icon.rectTransform.At(TopCenter, TabIconPosition, new Vector2(TabIconSize, TabIconSize));
                // 강화 탭: 실타래 꼬리표 (T4, 아이콘 위. 유실자 레벨업이 열린 뒤에만 보인다)
                if (i == (int)MainTab.Growth) BuildLineageXpTag(button.transform);
                buttons[i] = button;
                ctx.TabButtons[i] = button;
                panels[i] = tabs[i].panel;
                // 패널 바꿈 페이드 (0.12초 알파만 — 누름은 그대로 받는다)
                var group = tabs[i].panel.GetComponent<CanvasGroup>();
                panelGroups[i] = group != null ? group : tabs[i].panel.AddComponent<CanvasGroup>();
            }
            if (mainButton == null) mainButton = BuildMainTab(bar, out mainGlow, out mainRing, out mainLabel, out mainDim);
            ctx.TabMain = mainButton;

            // 선택 막대 (M52): 흰 캡슐 그림 104x6, 탭 바 위 끝 (시트 아래 끝에 닿는다). 전용 탭 바는 금 끈 위라 금색이 묻혀 Cream으로 (B5).
            // 앵커 x = 첫 탭 가운데 (MAIN에서는 알파 0 — MainTabController가 옮기고 켠다), 누름 없음
            var indicatorTint = dedicatedNav ? UITheme.Cream : UITheme.Gold;
            var indicator = UiArt.Panel(bar, "Indicator", UiArt.BarFill9s, UiArt.CapsulePpu(IndicatorSize.y), indicatorTint, indicatorTint);
            indicator.raycastTarget = false;
            var indicatorRect = indicator.rectTransform;
            float barWidth = UISize.TabSideWidth * tabs.Length + UISize.TabCentreWidth;
            float x01 = UISize.TabSideWidth * 0.5f / Mathf.Max(1f, barWidth);
            indicatorRect.anchorMin = new Vector2(x01, 1f);
            indicatorRect.anchorMax = new Vector2(x01, 1f);
            indicatorRect.pivot = new Vector2(0.5f, 1f);
            indicatorRect.anchoredPosition = Vector2.zero;
            indicatorRect.sizeDelta = IndicatorSize;
            indicator.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            new Binder(controller)
                .SetArray("_tabButtons", buttons)
                .SetArray("_panels", panels)
                .SetArray("_selectedPlates", plates)
                .SetInt("_defaultIndex", -1)
                .Set("_indicator", indicatorRect)
                .SetArray("_panelGroups", panelGroups)
                .Set("_growthGlow", growthGlow)
                .Set("_mainButton", mainButton)
                .Set("_mainGlow", mainGlow != null ? mainGlow.gameObject : null)
                .Set("_mainLabel", mainLabel)
                .Set("_mainIcon", mainDim)
                .Set("_mainRing", mainRing)
                .Set("_sheet", sheet)
                .Apply();
            BindSheet(ctx, sheet);
        }

        /// <summary>
        /// 9-slice 테두리가 양 끝 장식뿐인지 (왼쪽 · 오른쪽 &gt; 0, 위 · 아래 0 — sprite.border = (왼쪽, 아래, 오른쪽, 위)).
        /// 전용 탭 바를 Tiled로 깔 수 있는 조건 (상단 바 FrameSceneBuilder.HasEndCaps와 같은 규칙)
        /// </summary>
        static bool HasEndCapsOnly(Sprite sprite)
        {
            var border = sprite.border;
            return border.x > 0f && border.z > 0f && border.y <= 0f && border.w <= 0f;
        }

        /// <summary>탭 칸 폭: 기준 폭 width, 남는 폭은 같은 비율로 (태블릿처럼 넓은 화면)</summary>
        static void SetSlotWidth(GameObject slot, float width)
        {
            if (!slot.TryGetComponent(out LayoutElement element)) element = slot.AddComponent<LayoutElement>();
            element.minWidth = 0f;
            element.preferredWidth = width;
            element.flexibleWidth = width;
        }

        /// <summary>
        /// 가운데 사냥 "Tab_Main" (§2.2, 192x170 — 누르면 MAIN, 시트가 열려 있으면 닫기). 자식 (아래 → 위):
        ///   · Glow: 메달 가운데 200 (탭 바 위로 23 솟음), 부드러운 빛 Lamp@0.45, 멈춰 있다 — MAIN에서만 (CR-12)
        ///   · Ring: 메달 뒤 실 고리 172, 12° (B5 시안 — MAIN 0.55 / SHEET 0.3, 실 고리 아트가 없으면 만들지 않는다)
        ///   · Hit: 칸 전체 투명 누르는 자리 + PressScale (누름 소리 Tap) — 메달 · 아이콘은 그 아래 자식이라 함께 줄어든다
        ///     · Medallion 150 (위 가운데 (0, −2)): 전용 사냥 메달(nav_main_button — 수선점이 그려져 있다)이면 Icon을 꺼 두고, 없으면 어두운 메달
        ///     · Icon 112 (위 가운데 (0, −21), 비율 유지): Battle/shop
        ///   · LabelPlate 112x36 (아래 가운데 (0, 10)): 밤 알약 + 2px GoldAccent 테두리, Label '사냥' 26 굵게 (MAIN Gold / SHEET SubText)
        ///   · MetaDot 30 (위 가운데 (58, −14)): 꺼 둔 배지 — 메타(MetaSceneBuilder)가 RedDotAnyView를 붙여 _badge로 잇는다
        /// Hit 말고는 모두 누름을 받지 않는다. Button은 Tab_Main 루트 (대상 그림 = Hit). dimTarget = SHEET에서 0.8로 흐려지는 그림.
        /// </summary>
        static Button BuildMainTab(RectTransform bar, out Image glow, out Image ring, out TextMeshProUGUI label, out Graphic dimTarget)
        {
            var root = NewRect("Tab_Main", bar);
            SetSlotWidth(root.gameObject, UISize.TabCentreWidth);
            float medallionCenter = -(MainMedallionTop + UISize.MainMedallion * 0.5f);

            var glowArt = ArtLib.FX("glow_soft");
            glow = Img(root, "Glow", UITheme.WithAlpha(UITheme.Lamp, MainGlowAlpha), glowArt != null ? glowArt : UiArt.Puff);
            glow.type = Image.Type.Simple;
            glow.preserveAspect = true;
            glow.raycastTarget = false;
            CenterAt(glow.rectTransform, medallionCenter, UISize.MainGlow);

            ring = null;
            var ringArt = UiArt.RingThread;
            if (UiArt.IsArt(ringArt))
            {
                ring = Img(root, "Ring", UITheme.WithAlpha(Color.white, MainRingAlpha), ringArt);
                ring.type = Image.Type.Simple;
                ring.preserveAspect = true;
                ring.raycastTarget = false;
                CenterAt(ring.rectTransform, medallionCenter, MainRingSize);
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, MainRingAngle);
            }

            var hit = Img(root, "Hit", new Color(1f, 1f, 1f, 0f), null, true);
            hit.rectTransform.Stretch();
            hit.gameObject.AddComponent<PressScale>();

            var medallionArt = UiArt.NavMainButton(out bool dedicated);
            var medallion = Img(hit.transform, "Medallion", Color.white, medallionArt);
            medallion.type = Image.Type.Simple;
            medallion.preserveAspect = true;
            medallion.raycastTarget = false;
            medallion.rectTransform.At(TopCenter, new Vector2(0f, -MainMedallionTop), new Vector2(UISize.MainMedallion, UISize.MainMedallion));

            var shopArt = ArtLib.Battle("shop");
            var icon = Img(hit.transform, "Icon", Color.white, shopArt != null ? shopArt : UiArt.Star);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.At(TopCenter, new Vector2(0f, -MainIconTop), new Vector2(UISize.MainIcon, UISize.MainIcon));
            // 전용 메달에는 수선점이 그려져 있다 (두 번 보이지 않게 끈다)
            if (dedicated) icon.gameObject.SetActive(false);
            dimTarget = dedicated ? medallion : icon;

            var plateSize = new Vector2(UISize.MainLabelPlateWidth, UISize.MainLabelPlateHeight);
            var plate = UiArt.Panel(root, "LabelPlate", UiArt.PillNight9s, UiArt.CapsulePpu(plateSize.y), null, UITheme.Background);
            plate.raycastTarget = false;
            plate.rectTransform.At(BottomCenter, new Vector2(0f, MainLabelBottom), plateSize);
            var plateEdge = plate.gameObject.AddComponent<Outline>();
            plateEdge.effectColor = UITheme.GoldAccent;
            plateEdge.effectDistance = new Vector2(2f, -2f);
            label = Txt(plate.transform, "Label", "사냥", UIType.Body, UITheme.Gold, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch();

            var dot = BuildBadge(root);
            dot.name = "MetaDot";
            ((RectTransform)dot.transform).At(TopCenter, MainDotPosition, new Vector2(MainDotSize, MainDotSize));
            dot.gameObject.SetActive(false);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        /// <summary>위 가운데 앵커 · 가운데 피벗으로 놓는다 (centerY = 위 끝에서 가운데까지, 아래가 −) — 돌려도 제자리에서 돈다</summary>
        static void CenterAt(RectTransform rt, float centerY, float size)
        {
            rt.anchorMin = rt.anchorMax = TopCenter;
            rt.pivot = Center;
            rt.anchoredPosition = new Vector2(0f, centerY);
            rt.sizeDelta = new Vector2(size, size);
        }

        /// <summary>
        /// 시트를 잇는다 (§5.4): SheetController ← ContentArea · 머리 · CanvasGroup · HandleHit 버튼 · DragArea 끌기 · StageArea,
        /// 머리의 두 끌기(DragArea · HandleHit) ← SheetController. 코치마크는 BuildOverlays 뒤에 Build가 잇는다.
        /// </summary>
        static void BindSheet(SceneBuildContext ctx, SheetController sheet)
        {
            var header = ctx.SheetHeader;
            var dragArea = header != null ? header.Find("DragArea") : null;
            var handleHit = header != null ? header.Find("HandleHit") : null;
            var returnChip = header != null ? header.Find("ReturnChip") : null;
            var drag = dragArea != null ? dragArea.GetComponent<SheetDragHandle>() : null;
            var hitDrag = handleHit != null ? handleHit.GetComponent<SheetDragHandle>() : null;
            if (drag != null) new Binder(drag).Set("_sheet", sheet).Apply();
            if (hitDrag != null) new Binder(hitDrag).Set("_sheet", sheet).Apply();
            new Binder(sheet)
                .Set("_content", ctx.ContentArea)
                .Set("_header", header)
                .Set("_group", ctx.ContentArea.GetComponent<CanvasGroup>())
                .Set("_handle", handleHit != null ? handleHit.GetComponent<Button>() : null)
                .Set("_drag", drag)
                .Set("_stage", ctx.StageArea)
                .Set("_returnChipGroup", returnChip != null ? returnChip.GetComponent<CanvasGroup>() : null)
                .Apply();
        }

        /// <summary>실타래 꼬리표 알약 크기 · 자리 (탭 버튼 위 끝에 걸쳐 가운데, 위로 8 나온다)</summary>
        static readonly Vector2 LineageTagSize = new(108, 34);
        const float LineageTagTop = 8f;

        /// <summary>
        /// 강화 탭 실타래 꼬리표 (T4 · 결정 6 #2, 결정 2 안 D): 탭 버튼 위 끝에 걸친 어두운 알약 108x34 (실타래 그림 22 산호 틴트 + 잔액 20 굵게, K/M 축약).
        /// 늘 켜진 묶음 "LineageXpTag"(LineageXpBalanceView)가 유실자 레벨업 개방(1-1 클리어) 뒤에 "Pill"을 켠다. 보유 전원 최대면 "모두 최대".
        /// 누름을 받지 않는다 (탭 버튼 누르기를 막지 않게). 실타래 날아가기 2순위 도착지 (TutorialSceneBuilder가 RewardFlyer에 잇는다). 새 그림 없음
        /// </summary>
        static void BuildLineageXpTag(Transform button)
        {
            var holder = NewRect("LineageXpTag", button).Stretch();
            var pill = UiArt.Panel(holder, "Pill", UiArt.PillNight9s, UiArt.CapsulePpu(LineageTagSize.y), null,
                UITheme.WithAlpha(UITheme.PanelLight, 0.92f), true);
            pill.raycastTarget = false;
            pill.rectTransform.At(TopCenter, new Vector2(0, LineageTagTop), LineageTagSize);
            var icon = CurrencyIcon(pill.transform, CurrencyType.LineageXp, 22);
            icon.raycastTarget = false;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = MiddleLeft;
            iconRect.anchoredPosition = new Vector2(19, 0);
            var amount = Txt(pill.transform, "Amount", "0", 20, UITheme.Cream, TextAlignmentOptions.MidlineRight, true).FitFloor(UIType.AutoSizeFloor);
            amount.raycastTarget = false;
            amount.rectTransform.Stretch(34, 12, 0, 0);
            pill.gameObject.SetActive(false);
            var view = holder.gameObject.AddComponent<LineageXpBalanceView>();
            new Binder(view)
                .Set("_amount", amount)
                .Set("_root", pill.gameObject)
                .SetBool("_requireUnlock", true)
                .SetBool("_compact", true)
                .Apply();
        }

        /// <summary>
        /// 강화 탭 금빛 (M35d): 부드러운 빛 그림(FX/glow_soft, 없으면 FX 퍼프) Gold@0.45, 아이콘 가운데에 120x120.
        /// 받침(SelectedPlate) 바로 뒤 형제 — 이름 · 아이콘 · 나중에 붙는 빨간 점 · 잠금보다 아래에 그려진다. 꺼 두고 MainTabController가 켠다.
        /// </summary>
        static Image BuildGrowthGlow(Transform button)
        {
            var art = ArtLib.FX("glow_soft");
            var glow = Img(button, "AffordGlow", UITheme.WithAlpha(UITheme.Gold, GrowthGlowAlpha), art != null ? art : UiArt.Puff);
            glow.type = Image.Type.Simple;
            glow.preserveAspect = true;
            glow.raycastTarget = false;
            // 아이콘(위 기준 -14, 92px)과 가운데를 맞춘다
            float centerY = TabIconPosition.y - TabIconSize * 0.5f;
            glow.rectTransform.At(TopCenter, new Vector2(0, centerY + GrowthGlowSize * 0.5f), new Vector2(GrowthGlowSize, GrowthGlowSize));
            glow.transform.SetSiblingIndex(1);
            glow.gameObject.SetActive(false);
            return glow;
        }

        /// <summary>
        /// 탭 콘텐츠 영역 = 시트의 몸 (UX-SPEC-IA §1.1 · §4): 탭 바 바로 위 780 (열린 자리 970~1750 — 패널 배치는 예전 그대로).
        /// 중첩 Canvas(덮어 그리기 없음 — 형제 순서대로라 뒤 형제 상단 바 · 탭 바가 위에 그려진다) + GraphicRaycaster + CanvasGroup(줄이기 알파):
        /// 시트가 움직여도 루트 캔버스를 다시 묶지 않는다. SafeArea 바로 아래 자식 그대로 (패널 경로 "SafeArea/ContentArea/*Panel" 고정).
        /// 누빔 벨벳 배경(UI/content_bg)을 비율대로 덮어 깔고 넘치는 부분은 잘라낸다.
        /// 배경은 마스크(RectMask2D)가 있는 별도 자식이라 탭 패널(형제)은 잘리지 않는다. 아트가 없으면 Panel 색 판.
        /// 그 위 투명 "Blocker"(패널 뒤)가 시트 몸 전체의 누름을 받아, 패널 빈 곳을 눌러도 밑의 전투(덱 · 램프 등)가 눌리지 않는다.
        /// 빌드 끝에 꺼 둔다 (MAIN으로 시작).
        /// </summary>
        static RectTransform BuildContentArea(RectTransform safeArea)
        {
            var art = ArtLib.UI("content_bg");
            RectTransform area;
            if (art == null)
            {
                area = Img(safeArea, "ContentArea", UITheme.Panel).rectTransform.Bottom(TabBarHeight, ContentHeight);
            }
            else
            {
                area = NewRect("ContentArea", safeArea).Bottom(TabBarHeight, ContentHeight);
                var backdrop = NewRect("Backdrop", area).Stretch();
                backdrop.gameObject.AddComponent<RectMask2D>();
                var image = Img(backdrop, "Art", Color.white, art);
                image.rectTransform.Stretch();
                Envelope(image, art);
            }
            // 누름 막이 (투명, 시트 몸 전체 — 패널 뒤): 시트 밑 전투(덱 · 램프 · 가이드 퀘스트 줄 · 수호자 자리)는 켜진 채 누름만 막힌다 (§4.5)
            Img(area, "Blocker", new Color(1f, 1f, 1f, 0f), null, true).rectTransform.Stretch();
            // 중첩 캔버스 (덮어 그리기 없음) + 자기 누름 판정 + 줄이기 알파
            area.gameObject.AddComponent<Canvas>();
            area.gameObject.AddComponent<GraphicRaycaster>();
            area.gameObject.AddComponent<CanvasGroup>();
            return area;
        }

        // ───────────── 시트 머리 (UX-SPEC-IA §4.2) ─────────────

        /// <summary>머리 위 그림자 높이 · 진한 쪽 알파 (시트 쪽으로 Background 0 → 0.45)</summary>
        const float SheetShadowHeight = 40f;
        const float SheetShadowAlpha = 0.45f;
        /// <summary>전용 시트 틀(sheet_frame_9s 192, 테두리 48) 배율 2 → 테두리 24 · 모서리 둥글기 약 20 (B5)</summary>
        const float SheetFramePpu = 2f;
        /// <summary>손잡이 위치 (위 가운데에서 8 아래) · 꺾쇠 크기 · 손잡이 테두리 알파</summary>
        const float SheetHandleTop = 8f;
        const float SheetChevronSize = 28f;
        const float SheetHandleEdgeAlpha = 0.8f;
        /// <summary>머리 칩 자리 가장자리 (왼쪽 돌아가기 칩 · 오른쪽 예약 칸)</summary>
        const float HeaderChipEdge = 16f;
        /// <summary>머리 칩 글자 24 (자동 크기 20까지)</summary>
        const float HeaderChipText = 24f;
        /// <summary>세로 페이드 스프라이트 (그림자) — 생성 UI 조각</summary>
        const string FadeSpritePath = SpriteDir + "/UI_FadeV.png";
        const int FadeSpriteWidth = 4;
        const int FadeSpriteHeight = 64;

        /// <summary>
        /// 시트 머리 "ContentArea/SheetHeader" (1080x56, ContentArea 위 끝에 붙는다 — 앵커 (0,1)–(1,1), 피벗 (0.5, 0)). 자식 (아래 → 위):
        ///   · Shadow: 머리 위 40, 생성 세로 페이드 Background 0 → 0.45 (시트 쪽으로), 누름 없음
        ///   · Velvet: 전용 시트 틀(sheet_frame_9s — 위가 둥근 금 끈, 아래 열림)이면 Sliced 배율 2, 없으면 벨벳 띠를 뒤집어 금 솔기를 위로 (탭 바처럼)
        ///   · DragArea: 머리 전체 투명 누르는 자리 + SheetDragHandle (AutoShot이 끈다 — 경로 고정)
        ///   · Handle: 위 가운데 (0, −8) 120x40 밤 알약 + GoldAccent@0.8 2px 테두리, 아래를 가리키는 꺾쇠 28 Cream (보이기만)
        ///   · HandleHit: 위 가운데 (0, +24) 200x80, 피벗 (0.5, 1) — 시트 위 끝 24 위부터 머리 아래 끝까지 (누르는 자리는 위로 8 더 = 88, M22).
        ///     투명 Button(닫기) + SheetDragHandle
        ///   · ReturnChip: 돌아가기 칩 (M72, 왼쪽 칸 300x48 — BuildReturnChip)
        ///   · RightSlot: 오른쪽 칸 300x48 (비워 둔다 — P2 키우기 자리)
        /// 머리는 불투명하다 (내용 배경과 함께 시트가 그 밑을 모두 가린다). ContentArea의 앞 형제(배경 · 누름 막이 바로 뒤) — 패널이 위로 넘쳐 그리는 것
        /// (편성 끌기 그림자)이 머리에 가리지 않게. SheetController · 끌기는 탭 바를 만든 뒤 BindSheet가 잇는다.
        /// </summary>
        static RectTransform BuildSheetHeader(RectTransform contentArea, out ReturnChipView returnChip)
        {
            var header = NewRect("SheetHeader", contentArea);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(0.5f, 0f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, SceneBuildContext.SheetHeaderHeight);
            // 배경 · 누름 막이 바로 뒤, 패널보다 앞 (패널은 이 뒤에 화면 빌더가 붙인다)
            var blocker = contentArea.Find("Blocker");
            if (blocker != null) header.SetSiblingIndex(blocker.GetSiblingIndex() + 1);

            // 그림자: 머리 위 40 (시트 쪽으로 진해진다)
            var shadow = Img(header, "Shadow", UITheme.WithAlpha(UITheme.Background, SheetShadowAlpha), FadeSprite());
            shadow.type = Image.Type.Simple;
            var shadowRect = shadow.rectTransform;
            shadowRect.anchorMin = new Vector2(0f, 1f);
            shadowRect.anchorMax = Vector2.one;
            shadowRect.pivot = new Vector2(0.5f, 0f);
            shadowRect.anchoredPosition = Vector2.zero;
            shadowRect.sizeDelta = new Vector2(0f, SheetShadowHeight);

            // 벨벳
            var frameArt = UiArt.SheetFrame9s(out bool dedicatedFrame);
            Image velvet;
            if (dedicatedFrame)
            {
                velvet = UiArt.Panel(header, "Velvet", frameArt, SheetFramePpu, Color.white, UITheme.Panel);
            }
            else
            {
                var velvetArt = UiArt.BarVelvet9s;
                velvet = UiArt.Panel(header, "Velvet", velvetArt, 1.5f, null, UITheme.Panel);
                if (UiArt.IsArt(velvetArt)) velvet.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            }
            velvet.raycastTarget = false;
            velvet.rectTransform.Stretch();

            // 끄는 자리 (머리 전체, 투명)
            var dragArea = Img(header, "DragArea", new Color(1f, 1f, 1f, 0f), null, true);
            dragArea.rectTransform.Stretch();
            dragArea.gameObject.AddComponent<SheetDragHandle>();

            // 손잡이 (보이기만)
            var handleSize = new Vector2(UISize.SheetHandleWidth, UISize.SheetHandleHeight);
            var handle = UiArt.Panel(header, "Handle", UiArt.PillNight9s, UiArt.CapsulePpu(handleSize.y), null, UITheme.Background);
            handle.raycastTarget = false;
            handle.rectTransform.At(TopCenter, new Vector2(0f, -SheetHandleTop), handleSize);
            var handleEdge = handle.gameObject.AddComponent<Outline>();
            handleEdge.effectColor = UITheme.WithAlpha(UITheme.GoldAccent, SheetHandleEdgeAlpha);
            handleEdge.effectDistance = new Vector2(2f, -2f);
            var chevronArt = UiArt.Chevron;
            if (chevronArt != null)
            {
                // 오른쪽 꺾쇠를 −90° 돌려 아래를 가리킨다 (흰색 틴트용 → Cream)
                var chevron = UiArt.Icon(handle.transform, "Chevron", chevronArt, Center, Vector2.zero, SheetChevronSize);
                chevron.color = UITheme.Cream;
                chevron.rectTransform.pivot = Center;
                chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }

            // 손잡이 누르는 자리 (시트 위 끝 24 위 ~ 머리 아래 끝, 투명): 누르면 닫기, 끌어도 시트가 따라온다
            var hitImage = Img(header, "HandleHit", new Color(1f, 1f, 1f, 0f), null, true);
            var hitRect = hitImage.rectTransform;
            hitRect.anchorMin = hitRect.anchorMax = TopCenter;
            hitRect.pivot = TopCenter;
            hitRect.anchoredPosition = new Vector2(0f, UISize.SheetHandleHitRise);
            hitRect.sizeDelta = new Vector2(UISize.SheetHandleHitWidth, UISize.SheetHandleHitHeight);
            // 누르는 자리 하한 88 (M22): 모자란 만큼 위로만 (전투 띠 쪽 — 아래는 패널 내용이라 넓히지 않는다)
            hitImage.raycastPadding = new Vector4(0f, 0f, 0f, -Mathf.Max(0f, UISize.MinHit - UISize.SheetHandleHitHeight));
            var hit = hitImage.gameObject.AddComponent<Button>();
            hit.targetGraphic = hitImage;
            hit.transition = Selectable.Transition.None;
            hit.navigation = new Navigation { mode = Navigation.Mode.None };
            hitImage.gameObject.AddComponent<SheetDragHandle>();

            // 돌아가기 칩 (왼쪽 칸) · 오른쪽 예약 칸
            returnChip = BuildReturnChip(header);
            NewRect("RightSlot", header).At(MiddleRight, new Vector2(-HeaderChipEdge, 0f), new Vector2(UISize.HeaderChipWidth, UISize.HeaderChipHeight));
            return header;
        }

        /// <summary>
        /// 세로 페이드 스프라이트 (Sprites/UI/UI_FadeV.png, 흰색 4x64): 위 0 → 아래 1 (t^1.5 — 아래쪽에서 빨리 진해진다).
        /// 시트 그림자 같은 UI 기본 조각 (생성 그림). 처음 쓸 때 만들고 가져오기 설정(스프라이트 · 클램프 · 밉맵 없음 · 무압축)을 맞춘다.
        /// </summary>
        static Sprite FadeSprite()
        {
            if (!System.IO.File.Exists(FadeSpritePath))
            {
                var texture = new Texture2D(FadeSpriteWidth, FadeSpriteHeight, TextureFormat.RGBA32, false);
                var pixels = new Color32[FadeSpriteWidth * FadeSpriteHeight];
                for (int y = 0; y < FadeSpriteHeight; y++)
                {
                    // 텍스처 y = 0이 아래 (진한 쪽)
                    float t = 1f - (y + 0.5f) / FadeSpriteHeight;
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Pow(t, 1.5f));
                    for (int x = 0; x < FadeSpriteWidth; x++) pixels[y * FadeSpriteWidth + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FadeSpritePath)!);
                System.IO.File.WriteAllBytes(FadeSpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(FadeSpritePath);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(FadeSpritePath);
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled
                                     || importer.wrapMode != TextureWrapMode.Clamp
                                     || importer.textureCompression != TextureImporterCompression.Uncompressed))
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
            return AssetDatabase.LoadAssetAtPath<Sprite>(FadeSpritePath);
        }

        /// <summary>비율을 지킨 채 부모를 덮는다 (AspectRatioFitter EnvelopeParent — 넘치는 부분은 부모 마스크/화면 밖)</summary>
        static void Envelope(Image image, Sprite art)
        {
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / Mathf.Max(1f, art.rect.height);
        }

        // ───────────── 공용 팝업 ─────────────

        /// <summary>우편함 아래 줄 높이 (안내 글 · 모두 받기 96) + 목록과의 간격</summary>
        const float MailFooterHeight = UISize.ClaimAllFooterHeight;
        const float MailFooterGap = UISpace.S24;
        /// <summary>우편함 아래 왼쪽 안내 글 폭 (모두 받기 왼쪽에 24 띄움 — M51)</summary>
        const float MailInfoWidth = 440f;

        /// <summary>
        /// 우편함 (M20 · M51 · M26 · M76): 개수 · 목록 · 아래 줄 [안내 글 440 | 24 | 모두 받기 380x96 금 + 개수 배지].
        /// 모두 받기는 받을 우편이 있을 때만 켜진다 (MailPopup.Refresh). 이름 "ClaimAllButton"은 그대로 (우편 테스트).
        /// </summary>
        static UIPopup BuildMailPopup(RectTransform root, MailItemView itemPrefab)
        {
            var popup = BuildPopup<MailPopup>(root, "MailPopup", "우편함", new Vector2(960, 1560), out var body, out _);
            float listBottom = MailFooterHeight + MailFooterGap;

            var count = Txt(body, "CountText", "우편 0개", 28, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            count.rectTransform.Top(0, 50);
            var content = ScrollList(NewRect("MailList", body).Stretch(0, 0, 60, listBottom), out _);
            AddVerticalList(content, 16, new RectOffset(0, 0, 0, 16));
            var empty = Txt(body, "EmptyLabel", "받은 우편이 없어요.", 30, UITheme.SubText);
            empty.rectTransform.Stretch(0, 0, 60, listBottom);

            Txt(body, "InfoText", "보관 기간이 지난 우편은 자동으로 사라져요.", UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft, false, true)
                .rectTransform.At(BottomLeft, Vector2.zero, new Vector2(MailInfoWidth, MailFooterHeight));
            var claimAll = ClaimAllButton(body, UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight, UISize.ClaimAllFooterText, out _, out var claimBadge);
            ((RectTransform)claimAll.transform).At(BottomRight, Vector2.zero, new Vector2(UISize.ClaimAllFooterWidth, UISize.ClaimAllFooterHeight));
            MuteTapSound(claimAll);
            claimAll.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_listContent", content)
                .Set("_itemPrefab", itemPrefab)
                .Set("_countText", count)
                .Set("_emptyLabel", empty.gameObject)
                .Set("_claimAllButton", claimAll)
                .Set("_claimAllBadge", claimBadge)
                .Apply();
            return popup;
        }

        /// <summary>공지사항 (M65 · M76): 목록 ↔ 상세. 빈 목록 글은 불러오는 중 / 없음을 NoticePopup이 바꾼다</summary>
        static UIPopup BuildNoticePopup(RectTransform root, NoticeItemView itemPrefab)
        {
            var popup = BuildPopup<NoticePopup>(root, "NoticePopup", "공지사항", new Vector2(960, 1560), out var body, out _);

            var listView = NewRect("ListView", body).Stretch(0, 0, 0, 100);
            var listContent = ScrollList(NewRect("NoticeList", listView).Stretch(), out _);
            AddVerticalList(listContent, 14, new RectOffset(0, 0, 0, 16));
            var empty = Txt(listView, "EmptyLabel", NoticePopup.EmptyText, 30, UITheme.SubText);
            empty.rectTransform.Stretch();

            var detailView = NewRect("DetailView", body).Stretch(0, 0, 0, 100);
            var back = SceneParts.Button(detailView, "BackButton", "‹ 목록", ButtonStyle.Secondary, 28, out _);
            var backSize = new Vector2(170, 70);
            ((RectTransform)back.transform).At(TopLeft, Vector2.zero, backSize);
            FitButtonArt(back, backSize.y);
            back.targetGraphic.HitPad(backSize, 0f, UISize.MinHit);
            var title = Txt(detailView, "TitleText", "제목", 36, UITheme.Text, TextAlignmentOptions.TopLeft, true, true);
            title.rectTransform.Top(92, 100);
            var date = Txt(detailView, "DateText", "2026-01-01", 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            date.rectTransform.Top(196, 40);
            Img(detailView, "Divider", UITheme.Divider).rectTransform.Top(246, 2);

            var bodyContent = ScrollList(NewRect("BodyScroll", detailView).Stretch(0, 0, 266, 0), out var detailScroll);
            var bodyLayout = bodyContent.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;
            bodyLayout.padding = new RectOffset(0, 0, 0, 30);
            var bodyText = Txt(bodyContent, "BodyText", "내용", 28, UITheme.Text, TextAlignmentOptions.TopLeft, false, true);
            bodyText.lineSpacing = 8;
            detailView.SetInactive();

            var hideToday = CheckToggle(body, "HideTodayToggle", "오늘 하루 보지 않기", 28);
            var hideSize = new Vector2(480, 70);
            ((RectTransform)hideToday.transform).At(BottomLeft, new Vector2(0, 15), hideSize);
            PadToggle(hideToday, hideSize.y);

            new Binder(popup)
                .Set("_listView", listView.gameObject)
                .Set("_listContent", listContent)
                .Set("_itemPrefab", itemPrefab)
                .Set("_emptyLabel", empty.gameObject)
                .Set("_emptyText", empty)
                .Set("_detailView", detailView.gameObject)
                .Set("_backButton", back)
                .Set("_detailTitle", title)
                .Set("_detailDate", date)
                .Set("_detailBody", bodyText)
                .Set("_detailScroll", detailScroll)
                .Set("_hideTodayToggle", hideToday)
                .Apply();
            return popup;
        }

        /// <summary>오프라인 보상 (메타가 꺼졌을 때의 예전 화면 · M26 · M76): 받기는 금 버튼</summary>
        static UIPopup BuildOfflineRewardPopup(RectTransform root)
        {
            var popup = BuildPopup<OfflineRewardPopup>(root, "OfflineRewardPopup", "오프라인 보상", new Vector2(860, 760), out var body, out _);

            Txt(body, "DescText", "자리를 비운 동안 친구들이 단추와 실타래를 모아왔어요!", 30, UITheme.Text, TextAlignmentOptions.Center, false, true)
                .rectTransform.Top(10, 90);
            var duration = Txt(body, "DurationText", "0분 동안 모은 단추", 28, UITheme.SubText);
            duration.rectTransform.Top(110, 46);

            var amount = NewRect("Amount", body).Top(176, 100);
            var amountRow = amount.gameObject.AddComponent<HorizontalLayoutGroup>();
            amountRow.childAlignment = TextAnchor.MiddleCenter;
            amountRow.spacing = 16;
            amountRow.childControlWidth = true;
            amountRow.childControlHeight = true;
            amountRow.childForceExpandWidth = false;
            amountRow.childForceExpandHeight = false;
            CurrencyIcon(amount, CurrencyType.Gold, 70);
            var gold = Txt(amount, "GoldText", "0", 64, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            // 실타래 줄 (결정 2 안 D: 단추와 함께 모인 실타래, 0이면 줄을 끈다)
            var lineageRow = NewRect("LineageXpAmount", body).Top(282, 64);
            var lineageLayout = lineageRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            lineageLayout.childAlignment = TextAnchor.MiddleCenter;
            lineageLayout.spacing = 12;
            lineageLayout.childControlWidth = true;
            lineageLayout.childControlHeight = true;
            lineageLayout.childForceExpandWidth = false;
            lineageLayout.childForceExpandHeight = false;
            CurrencyIcon(lineageRow, CurrencyType.LineageXp, 48);
            var lineageXp = Txt(lineageRow, "LineageXpText", "0", 44, UITheme.LineageXp, TextAlignmentOptions.MidlineLeft, true);

            var info = Txt(body, "InfoText", "보상은 최대 8시간까지 쌓여요.", UIType.Caption, UITheme.SubText);
            info.rectTransform.Top(356, 40);
            var claim = SceneParts.Button(body, "ClaimButton", "받기", ButtonStyle.Gold, 40, out _);
            var claimSize = new Vector2(420, 120);
            ((RectTransform)claim.transform).At(BottomCenter, new Vector2(0, 10), claimSize);
            FitButtonArt(claim, claimSize.y);
            MuteTapSound(claim);

            new Binder(popup)
                .Set("_durationText", duration)
                .Set("_goldText", gold)
                .Set("_lineageXpText", lineageXp)
                .Set("_infoText", info)
                .Set("_claimButton", claim)
                .Apply();
            return popup;
        }

        /// <summary>확인 창 줄 높이 · 아래 끝 (MessagePopup이 런타임에 줄을 쌓고 창을 키운다 — 값은 거기와 같다)</summary>
        const float MessageButtonRowHeight = 100f;
        const float MessageRowsBottom = 112f;
        const float MessageCostRowHeight = 72f;
        const float MessageSkipRowHeight = 52f;
        static readonly Vector2 MessageSkipSize = new(340, MessageSkipRowHeight);
        const float MessageSkipBox = 40f;

        /// <summary>
        /// 확인 창 (M15 · M55 · M76, SystemPopups 층): 820x560, 본문 30 + 버튼 줄 100 [취소 Secondary | 확인 Primary | (위험 확인 Danger, 꺼 둠)].
        /// 꺼 둔 줄: CostRow (아이콘 48 · 비용 40 굵게 Cream · '보유 a → b' 26 SubText) · SkipToggle (상자 40 + '오늘 다시 묻지 않기' 26 SubText).
        /// 버튼 자리는 바뀌지 않는다 (확인과 위험 확인은 같은 오른쪽 칸을 번갈아 쓴다).
        /// </summary>
        static UIPopup BuildMessagePopup(RectTransform root)
        {
            var popup = BuildPopup<MessagePopup>(root, "MessagePopup", "알림", new Vector2(820, 560), out var body, out var title);

            var text = Txt(body, "BodyText", "내용", 30, UITheme.Text, TextAlignmentOptions.Center, false, true);
            text.rectTransform.Stretch(0, 0, 0, 120);
            var row = NewRect("Buttons", body).Bottom(0, MessageButtonRowHeight);
            AddRow(row, UISize.PairGap);
            var cancel = SceneParts.Button(row, "CancelButton", MessagePopup.DefaultCancelLabel, ButtonStyle.Secondary, 32, out var cancelLabel);
            var ok = SceneParts.Button(row, "OkButton", MessagePopup.DefaultOkLabel, ButtonStyle.Primary, 32, out var okLabel);
            var danger = SceneParts.Button(row, "DangerButton", MessagePopup.DefaultOkLabel, ButtonStyle.Danger, 32, out var dangerLabel);
            danger.gameObject.SetActive(false);

            // 비용 줄 (쓰기 확인): 가운데 모은 가로 줄
            var costRow = NewRect("CostRow", body).Bottom(MessageRowsBottom, MessageCostRowHeight);
            var costLayout = costRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            costLayout.childAlignment = TextAnchor.MiddleCenter;
            costLayout.spacing = UISpace.S12;
            costLayout.childControlWidth = true;
            costLayout.childControlHeight = true;
            costLayout.childForceExpandWidth = false;
            costLayout.childForceExpandHeight = false;
            var costIcon = CurrencyIcon(costRow, CurrencyType.Gem, 48);
            costIcon.name = "CostIcon";
            var costText = Txt(costRow, "CostText", "0", 40, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            var balance = Txt(costRow, "BalanceText", "보유 0 → 0", UIType.Body, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            costRow.gameObject.SetActive(false);

            // 오늘 다시 묻지 않기 (상자 40 + 26 SubText, 가운데)
            var skip = CheckToggle(body, "SkipToggle", MessagePopup.SkipTodayLabel, UIType.Body);
            ((RectTransform)skip.transform).At(BottomCenter, new Vector2(0, MessageRowsBottom), MessageSkipSize);
            if (skip.transform.Find("Box") is RectTransform box)
            {
                box.At(MiddleLeft, Vector2.zero, new Vector2(MessageSkipBox, MessageSkipBox));
                if (box.Find("Check") is RectTransform check) check.sizeDelta = new Vector2(MessageSkipBox - 8f, MessageSkipBox - 8f);
            }
            if (skip.transform.Find("Label") is RectTransform skipLabelRect && skipLabelRect.TryGetComponent<TextMeshProUGUI>(out var skipLabel))
            {
                skipLabel.color = UITheme.SubText;
                skipLabelRect.Stretch(MessageSkipBox + UISpace.S12, 0, 0, 0);
            }
            // 아래 버튼 줄(0~100)과 겹치지 않게 아래로는 10까지만 넓힌다
            PadToggle(skip, MessageSkipRowHeight, MessageRowsBottom - MessageButtonRowHeight - 2f);
            skip.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_bodyText", text)
                .Set("_okButton", ok)
                .Set("_cancelButton", cancel)
                .Set("_okLabel", okLabel)
                .Set("_cancelLabel", cancelLabel)
                .Set("_dangerButton", danger)
                .Set("_dangerLabel", dangerLabel)
                .Set("_costRow", costRow)
                .Set("_costIcon", costIcon)
                .Set("_costText", costText)
                .Set("_balanceText", balance)
                .SetArray("_currencyIcons", CurrencySprites())
                .Set("_skipToggle", skip)
                .Apply();
            return popup;
        }

        /// <summary>부족 창 얻는 곳 줄 수 · 줄 높이 · 간격, 필요 묶음 높이 (820x700 창 본문 528에 맞춘다: 96 + 16 + 4·96 + 3·10)</summary>
        const int ShortfallRowCount = 4;
        const float ShortfallRowHeight = 96f;
        const float ShortfallRowGap = 10f;
        const float ShortfallNeedHeight = 96f;
        static readonly Vector2 ShortfallGoSize = new(160, 72);

        /// <summary>
        /// 재화 부족 창 (M08, SystemPopups 층): 820x700. 위 96 = 재화 아이콘 72 + '필요 · 보유'(34 / 26) + '더 필요해요'(Negative 28),
        /// 그 아래 얻는 곳 4줄 (각 96: 보라 바느질 판 + 제목 28 · 설명 22 + [가기 ›] Primary 160x72, 강조 줄은 금 테두리).
        /// 얻는 곳 모드에서는 필요 묶음을 숨기고 줄을 위로 올린다 (ShortfallPopup).
        /// </summary>
        static UIPopup BuildShortfallPopup(RectTransform root)
        {
            var popup = BuildPopup<ShortfallPopup>(root, "ShortfallPopup", "별조각이 부족해요", new Vector2(820, 700), out var body, out var title);

            var need = NewRect("Need", body).Top(0, ShortfallNeedHeight);
            var icon = CurrencyIcon(need, CurrencyType.Gem, 72);
            icon.name = "CurrencyIcon";
            icon.rectTransform.At(MiddleLeft, new Vector2(8, 0), icon.rectTransform.sizeDelta);
            var needText = Txt(need, "NeedText", "필요 0 · 보유 0", 34, UITheme.Cream, TextAlignmentOptions.BottomLeft).Ellipsis();
            needText.rectTransform.Stretch(100, 0, 0, 44);
            var gapText = Txt(need, "GapText", "0 더 필요해요", UIType.Title, UITheme.Negative, TextAlignmentOptions.TopLeft, true).Ellipsis();
            gapText.rectTransform.Stretch(100, 0, ShortfallNeedHeight - 44, 0);

            var rowsRoot = NewRect("Sources", body).Stretch(0, 0, ShortfallNeedHeight + UISpace.S16, 0);
            var rowsLayout = rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = ShortfallRowGap;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = false;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            var rows = new Object[ShortfallRowCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = BuildShortfallRow(rowsRoot, i);

            var emptyText = Txt(rowsRoot, "EmptyText", "지금은 얻을 수 있는 곳이 없어요", UIType.Body, UITheme.SubText);
            emptyText.rectTransform.Stretch();
            emptyText.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            emptyText.gameObject.SetActive(false);

            new Binder(popup)
                .Set("_titleText", title)
                .Set("_needGroup", need.gameObject)
                .Set("_icon", icon)
                .Set("_needText", needText)
                .Set("_gapText", gapText)
                .SetArray("_rows", rows)
                .Set("_rowsRoot", rowsRoot)
                .SetArray("_currencyIcons", CurrencySprites())
                .Set("_emptyText", emptyText)
                .Apply();
            return popup;
        }

        static ShortfallSourceRow BuildShortfallRow(RectTransform parent, int index)
        {
            var bg = UiArt.Panel(parent, $"Source{index}", UiArt.PanelInset9s, 1.8f, null, UITheme.PanelLight);
            bg.rectTransform.sizeDelta = new Vector2(0, ShortfallRowHeight);
            var element = bg.gameObject.AddComponent<LayoutElement>();
            element.minHeight = ShortfallRowHeight;
            element.preferredHeight = ShortfallRowHeight;
            var group = bg.gameObject.AddComponent<CanvasGroup>();

            // 강조 테두리 (받지 않은 별조각 우편 줄): 흰색 틴트용 칸 틀에 금색
            var highlight = UiArt.Panel(bg.transform, "Highlight", UiArt.SlotFrame9s, 2.2f, UITheme.Gold, UITheme.GoldAccent);
            highlight.rectTransform.Stretch();
            highlight.gameObject.SetActive(false);

            float textRight = ShortfallGoSize.x + UISpace.S16 + UISpace.S16;
            var titleText = Txt(bg.transform, "Title", "얻는 곳", UIType.Title, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis()
                .FitFloor(UIType.Hint, UIType.Title);
            titleText.rectTransform.Stretch(UISpace.S24, textRight, 12, 44);
            var detail = Txt(bg.transform, "Detail", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
            detail.rectTransform.Stretch(UISpace.S24, textRight, 52, 10);

            var go = SceneParts.Button(bg.transform, "GoButton", "가기 ›", ButtonStyle.Primary, UIType.ButtonM, out _);
            ((RectTransform)go.transform).At(MiddleRight, new Vector2(-UISpace.S16, 0), ShortfallGoSize);
            FitButtonArt(go, ShortfallGoSize.y);
            go.targetGraphic.HitPad(ShortfallGoSize, 0f, UISize.MinHit, maxB: 12f, maxT: 12f);

            var view = bg.gameObject.AddComponent<ShortfallSourceRow>();
            new Binder(view)
                .Set("_title", titleText)
                .Set("_detail", detail)
                .Set("_button", go)
                .Set("_highlight", highlight.gameObject)
                .Set("_group", group)
                .Apply();
            return view;
        }

        /// <summary>재화 아이콘 (CurrencyType 순서: 단추 · 별조각 · 별빛 사탕 · 실타래, 없으면 null)</summary>
        static Object[] CurrencySprites() => new Object[]
        {
            ArtLib.Currency(CurrencyType.Gold),
            ArtLib.Currency(CurrencyType.Gem),
            ArtLib.Currency(CurrencyType.EventToken),
            ArtLib.Currency(CurrencyType.LineageXp),
        };

        /// <summary>9-slice 아트 버튼의 배율을 높이에 맞춘다 (버튼 공장 기본 2는 90~110px용)</summary>
        static void FitButtonArt(Button button, float height)
        {
            if (button.targetGraphic is Image image && image.type == Image.Type.Sliced && UiArt.IsArt(image.sprite))
                image.pixelsPerUnitMultiplier = UiArt.ButtonPpu(height);
        }

        /// <summary>자기 소리를 따로 내는 버튼(탭 = Tab, 받기 = Claim)의 누름 소리를 끈다 (PressScale.TapSound — 소리가 겹치지 않게)</summary>
        static void MuteTapSound(Component button)
        {
            if (button != null && button.TryGetComponent<PressScale>(out var press)) press.TapSound = false;
        }

        /// <summary>
        /// 체크 칸의 누르는 자리를 88 높이로 (상자 · 글자 둘 다 누름을 받는다 — 자식을 만들지 않고 raycastPadding).
        /// maxBottom: 아래로 넓힐 수 있는 만큼 (바로 아래 버튼 줄과 겹치지 않게)
        /// </summary>
        static void PadToggle(Toggle toggle, float height, float maxBottom = 99f)
        {
            if (toggle.targetGraphic != null)
                toggle.targetGraphic.HitPad(KnownSize(toggle.targetGraphic.rectTransform), 0f, UISize.MinHit, maxB: maxBottom);
            if (toggle.transform.Find("Label") is RectTransform label && label.TryGetComponent<TMP_Text>(out var text))
                text.HitPad(new Vector2(0f, height), 0f, UISize.MinHit, maxB: maxBottom);
        }

        // ───────────── 토스트 (M14) ─────────────

        /// <summary>
        /// 토스트 "Toast" (ToastLayer의 맨 뒤 형제, ToastView): 위 가운데 피벗, 안전 영역 위에서 480, 폭 880 — 자리 · 폭은 나타날 때마다
        /// ToastView가 맨 위 팝업 창에 맞춰 다시 정한다. 밤 패널 그림(금테, 알파 0.95 — 아트가 없으면 Panel@0.95 + 2px GoldAccent@0.5),
        /// 높이는 글에 맞춘다 (최소 88 · 위아래 18, 왼쪽 24 · 오른쪽 28).
        ///   · KindIcon (40x40): 흰 알약 그림을 종류 색으로 (Info Lavender 점 / Success Positive + '✓' / Error Negative + '!' — Glyph는 Ink 굵게).
        ///   · ErrorBar: Error일 때만 왼쪽 6px Negative 막대.
        ///   · Text: 28 Text, 줄바꿈, 자동 크기 없음, 넘치면 '…'.
        /// ToastView 연결: _text · _kindIcon · _kindGlyph · _errorBar.
        /// </summary>
        static ToastView BuildToast(RectTransform layer)
        {
            var art = UiArt.PanelNight9s;
            var bg = UiArt.Panel(layer, "Toast", art, 1.8f, new Color(1f, 1f, 1f, 0.95f), UITheme.ToastBack);
            if (!UiArt.IsArt(art))
            {
                var edge = bg.gameObject.AddComponent<Outline>();
                edge.effectColor = UITheme.ToastEdge;
                edge.effectDistance = new Vector2(2f, -2f);
            }
            bg.rectTransform.At(TopCenter, new Vector2(0, -UISize.ToastTop), new Vector2(UISize.ToastWidth, UISize.ToastMinHeight));
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var row = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)UISpace.S24, (int)UISize.ToastPadX, (int)UISize.ToastPadY, (int)UISize.ToastPadY);
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            bg.gameObject.AddComponent<LayoutElement>().minHeight = UISize.ToastMinHeight;
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Error 막대 (왼쪽 6px, 테두리 안쪽, 레이아웃 제외 — ToastView가 켜고 끈다)
            var errorBar = UiArt.Panel(bg.transform, "ErrorBar", UiArt.BarFill9s, 16f, UITheme.Negative, UITheme.Negative);
            var barRect = errorBar.rectTransform;
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.offsetMin = new Vector2(10f, 14f);
            barRect.offsetMax = new Vector2(16f, -14f);
            errorBar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            errorBar.gameObject.SetActive(false);

            // 종류 표시 40x40: 흰색 틴트용 알약 그림 (ToastView가 종류 색을 입힌다) + 그 위 기호
            var kind = UiArt.Panel(bg.transform, "KindIcon", UiArt.PillWhite9s, UiArt.CapsulePpu(UISize.ToastKindIcon), UITheme.Lavender, UITheme.Lavender);
            var kindElement = kind.gameObject.AddComponent<LayoutElement>();
            kindElement.minWidth = kindElement.preferredWidth = UISize.ToastKindIcon;
            kindElement.minHeight = kindElement.preferredHeight = UISize.ToastKindIcon;
            var glyph = Txt(kind.transform, "Glyph", string.Empty, UIType.Body, UITheme.Ink, TextAlignmentOptions.Center, true);
            glyph.rectTransform.Stretch();

            var text = Txt(bg.transform, "Text", string.Empty, UIType.Title, UITheme.Text, TextAlignmentOptions.MidlineLeft, false, true);
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var view = bg.gameObject.AddComponent<ToastView>();
            new Binder(view)
                .Set("_text", text)
                .Set("_kindIcon", kind)
                .Set("_kindGlyph", glyph)
                .Set("_errorBar", errorBar)
                .Apply();
            return view;
        }

        // ───────────── 돌아가기 칩 (M72 — 시트 머리 왼쪽 칸, UX-SPEC-IA §4.2) ─────────────

        /// <summary>× 칸 폭 (칩 오른쪽 끝)</summary>
        const float ReturnChipCloseSize = 48f;

        /// <summary>
        /// 고른 탭 받침 그림(ui_tab_selected)의 틴트 (WP-P 요청): 0.75로 눌러 Cream 이름이 받침 보라 위 ≈4.7:1 (흰색이면 ≈2.8:1).
        /// 받침 아래 따뜻한 빛 위는 여전히 ≈2.2:1이라 이름 글자의 어두운 외곽선(UiArt.Outline)은 그대로 둔다. 그림이 없으면 UITheme.TabSelected
        /// </summary>
        static readonly Color SelectedPlateArtTint = new(0.75f, 0.75f, 0.75f, 1f);

        /// <summary>
        /// 돌아가기 칩 "SheetHeader/ReturnChip" (ReturnChipView, 경로 고정): 머리 왼쪽 가운데 (16, 0) 300x48 — 칸은 시트가 열린 동안만 차 있다
        /// (MAIN으로 가면 비운다, CR-05). PanelLight 바느질 알약 + 2px GoldAccent 테두리, Label('‹ …' 24 Cream, 자동 크기 20~24, 왼쪽 16)
        /// + 오른쪽 끝 CloseButton(× 칸 48, '×' 22). 누르는 자리: 칩은 머리 아래 끝까지(4) + 위로 시트 위 끝을 넘어 88 높이까지,
        /// ×도 같은 높이에 옆으로 88 (M22 — 왼쪽 8은 이름 칸 끝까지, 칩 누름을 뺏지 않게 · 오른쪽 32는 칩 밖 머리 끄는 자리 위). raycastPadding — 자식을 만들지 않는다.
        /// 루트는 켜 두고 보임은 CanvasGroup으로 (처음 0, 누름 없음). 튜토리얼 · 코치마크 · 팝업 동안은 숨는다 (M72 규칙 — ReturnChipView).
        /// 누름 피드백은 색(UIKit 누름 색) — 크기는 칩의 한 번 튐(ReturnChipView)이 쓴다.
        /// </summary>
        static ReturnChipView BuildReturnChip(RectTransform header)
        {
            var size = new Vector2(UISize.HeaderChipWidth, UISize.HeaderChipHeight);
            var chip = UiArt.Panel(header, "ReturnChip", UiArt.PillWhite9s, UiArt.CapsulePpu(size.y), UITheme.PanelLight, UITheme.PanelLight, true);
            var rt = chip.rectTransform;
            rt.At(MiddleLeft, new Vector2(HeaderChipEdge, 0f), size);
            var edge = chip.gameObject.AddComponent<Outline>();
            edge.effectColor = UITheme.GoldAccent;
            edge.effectDistance = new Vector2(2f, -2f);
            var group = chip.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = HighlightedTint;
            colors.pressedColor = PressedTint;
            colors.selectedColor = Color.white;
            colors.fadeDuration = TintFade;
            button.colors = colors;
            // 아래는 머리 아래 끝까지, 위는 88 높이가 되도록 시트 위 끝을 넘어 (그 위는 전투 띠 — 시트가 열린 동안 누를 것이 없다)
            float below = (SceneBuildContext.SheetHeaderHeight - size.y) * 0.5f;
            float above = Mathf.Max(below, UISize.MinHit - size.y - below);
            var padding = new Vector4(0f, -below, 0f, -above);
            chip.raycastPadding = padding;

            var label = Txt(chip.transform, "Label", "‹ 돌아가기", HeaderChipText, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis()
                .FitFloor(UIType.Hint, HeaderChipText);
            label.rectTransform.Stretch(UISpace.S16, ReturnChipCloseSize + UISpace.S8, 0, 0);

            var closeImage = Img(chip.transform, "CloseButton", Color.clear, null, true);
            closeImage.rectTransform.At(MiddleRight, Vector2.zero, new Vector2(ReturnChipCloseSize, ReturnChipCloseSize));
            var close = closeImage.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImage;
            close.transition = Selectable.Transition.None;
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            // × 누르는 자리 88x88 (WP-9 요청): 높이는 칩과 같고, 옆은 HitPad로 — 모자란 40을 왼쪽 8(이름 칸과의 틈) · 오른쪽 32로 (maxL/maxR)
            float closeLeft = UISpace.S8;
            float closeRight = Mathf.Max(0f, UISize.MinHit - ReturnChipCloseSize - closeLeft);
            closeImage.HitPad(new Vector2(ReturnChipCloseSize, ReturnChipCloseSize), ReturnChipCloseSize + 2f * closeRight, 0f, closeLeft, closeRight);
            var closeSide = closeImage.raycastPadding;
            closeImage.raycastPadding = new Vector4(closeSide.x, padding.y, closeSide.z, padding.w);
            Txt(closeImage.transform, "Glyph", "×", UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center, true).rectTransform.Stretch();

            var view = chip.gameObject.AddComponent<ReturnChipView>();
            new Binder(view)
                .Set("_button", button)
                .Set("_label", label)
                .Set("_closeButton", close)
                .Set("_pulseTarget", rt)
                .Apply();
            return view;
        }

        // ───────────── 프리팹 ─────────────

        static ItemSlotView BuildItemSlotPrefab()
        {
            // 바탕 = 보라 바느질 안쪽 판, 그 위에 테두리만 있는 칸 틀(등급 색 틴트 = ItemSlotView._frame)
            var inner = UiArt.Panel(null, "ItemSlot", UiArt.PanelInset9s, 2f, null, UITheme.PanelLight);
            var root = inner.rectTransform;
            root.sizeDelta = new Vector2(180, 210);
            var view = inner.gameObject.AddComponent<ItemSlotView>();

            var frame = UiArt.Panel(root, "Frame", UiArt.SlotFrame9s, 2.2f, UITheme.GradeColor(ItemGrade.Common));
            frame.rectTransform.Stretch();
            var grade = Txt(root, "GradeText", "일반", 22, UITheme.SubText, TextAlignmentOptions.Center, true);
            grade.rectTransform.Top(10, 30, 8, 8);
            var placeholder = UiArt.Star;
            var icon = Img(root, "Icon", Color.white, placeholder);
            icon.rectTransform.At(Center, new Vector2(0, 10), new Vector2(96, 96));
            icon.preserveAspect = true;
            var name = Txt(root, "NameText", "아이템", 24, UITheme.Text).Ellipsis();
            name.rectTransform.Bottom(36, 40, 8, 8);
            name.enableAutoSizing = true;
            name.fontSizeMin = 16;
            name.fontSizeMax = 24;
            var count = Txt(root, "CountText", UIFormat.TimesSmall + "1", 22, UITheme.Text, TextAlignmentOptions.MidlineRight, true);
            count.rectTransform.Bottom(6, 32, 8, 14);

            // NEW (M09: 새로 얻음은 빨간 점이 아니라 금 NEW 알약): 흰 알약 그림에 금색 52x26 + Ink 굵게 (§1.5, 글자 하한 18)
            var newBadge = UiArt.Panel(root, "NewBadge", UiArt.PillWhite9s, UiArt.CapsulePpu(UISize.BadgeNewHeight), UITheme.Gold, UITheme.Gold);
            newBadge.raycastTarget = false;
            newBadge.rectTransform.At(TopLeft, new Vector2(-6, 6), new Vector2(UISize.BadgeNewWidth, UISize.BadgeNewHeight));
            var newText = Txt(newBadge.transform, "Text", "NEW", UIType.Micro, UITheme.Ink, TextAlignmentOptions.Center, true);
            newText.rectTransform.Stretch(2, 2, 0, 0);

            new Binder(view)
                .Set("_frame", frame)
                .Set("_icon", icon)
                .Set("_placeholderIcon", placeholder)
                .Set("_gradeText", grade)
                .Set("_nameText", name)
                .Set("_countText", count)
                .Set("_newBadge", newBadge.gameObject)
                .Apply();
            return SavePrefab<ItemSlotView>(inner.gameObject, $"{PrefabDir}/ItemSlot.prefab");
        }

        /// <summary>우편 줄 받기 버튼 자리 · 크기 (받은 뒤 같은 자리에 '받음' 도장)</summary>
        static readonly Vector2 MailClaimPosition = new(-24, 20);
        static readonly Vector2 MailClaimSize = new(200, 90);

        static MailItemView BuildMailItemPrefab()
        {
            var bg = UiArt.Panel(null, "MailItem", UiArt.PanelInset9s, 1.8f, null, UITheme.PanelLight);
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(900, 200);
            var view = bg.gameObject.AddComponent<MailItemView>();

            var title = Txt(root, "TitleText", "제목", 32, UITheme.Text, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            title.rectTransform.At(TopLeft, new Vector2(28, -18), new Vector2(560, 48));
            var expire = Txt(root, "ExpireText", "30일 남음", 22, UITheme.SubText, TextAlignmentOptions.MidlineRight);
            expire.rectTransform.At(TopRight, new Vector2(-28, -22), new Vector2(240, 40));
            var body = Txt(root, "BodyText", "내용", 24, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            body.rectTransform.At(TopLeft, new Vector2(28, -72), new Vector2(600, 66));
            body.overflowMode = TextOverflowModes.Ellipsis;
            var reward = Txt(root, "RewardText", string.Empty, 26, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
            reward.rectTransform.At(BottomLeft, new Vector2(28, 16), new Vector2(600, 44));
            // 받기 = 금 (M26: 주기 — 줄마다 반복되는 받기는 한 화면 금 하나 규칙의 예외)
            var claim = SceneParts.Button(root, "ClaimButton", "받기", ButtonStyle.Gold, 32, out var claimLabel);
            ((RectTransform)claim.transform).At(BottomRight, MailClaimPosition, MailClaimSize);
            FitButtonArt(claim, MailClaimSize.y);
            MuteTapSound(claim);

            // 받은 뒤 버튼 자리에 찍히는 도장 (금 굵게 + 어두운 외곽선, 살짝 기울임 — MailItemView.PlayClaimed)
            var stamp = Txt(root, "Stamp", "받음", 34, UITheme.Gold, TextAlignmentOptions.Center, true);
            UiArt.Outline(stamp);
            stamp.rectTransform.At(BottomRight, MailClaimPosition, MailClaimSize);
            stamp.gameObject.SetActive(false);

            new Binder(view)
                .Set("_titleText", title)
                .Set("_bodyText", body)
                .Set("_rewardText", reward)
                .Set("_expireText", expire)
                .Set("_claimButton", claim)
                .Set("_claimLabel", claimLabel)
                .Set("_stamp", stamp.rectTransform)
                .Apply();
            return SavePrefab<MailItemView>(bg.gameObject, $"{PrefabDir}/MailItem.prefab");
        }

        static NoticeItemView BuildNoticeItemPrefab()
        {
            var bg = UiArt.Panel(null, "NoticeItem", UiArt.PanelInset9s, 1.8f, null, UITheme.PanelLight, true);
            var root = bg.rectTransform;
            root.sizeDelta = new Vector2(900, 120);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // 누름 (M61): 버튼 색 (누름 0.92/0.90/0.95 · 손 올림 0.95 · 0.06초) + 누름 크기
            var colors = button.colors;
            colors.highlightedColor = HighlightedTint;
            colors.pressedColor = PressedTint;
            colors.selectedColor = Color.white;
            colors.fadeDuration = TintFade;
            button.colors = colors;
            bg.gameObject.AddComponent<PressScale>();
            var view = bg.gameObject.AddComponent<NoticeItemView>();

            var dot = UiArt.Badge(root, "UnreadDot", MiddleLeft, new Vector2(22, 0), 26f);
            var title = Txt(root, "TitleText", "공지 제목", 30, UITheme.Text, TextAlignmentOptions.MidlineLeft).Ellipsis();
            title.rectTransform.Stretch(60, 250, 0, 0);
            var date = Txt(root, "DateText", "2026-01-01", 22, UITheme.SubText, TextAlignmentOptions.MidlineRight);
            date.rectTransform.At(MiddleRight, new Vector2(-28, 0), new Vector2(210, 40));

            new Binder(view)
                .Set("_button", button)
                .Set("_titleText", title)
                .Set("_dateText", date)
                .Set("_unreadDot", dot.gameObject)
                .Apply();
            return SavePrefab<NoticeItemView>(bg.gameObject, $"{PrefabDir}/NoticeItem.prefab");
        }

        // ───────────── 빌드 끝 검사 (경고만 — M23 글자 하한 · M22 누르는 자리) ─────────────

        /// <summary>검사가 쓰는 기준 캔버스 크기 (CanvasScaler 기준 해상도)</summary>
        static readonly Vector2 ReferenceCanvas = new(1080, 1920);
        /// <summary>경고 한 번에 적는 항목 상한</summary>
        const int LintListLimit = 150;

        /// <summary>
        /// 글자 크기 하한 검사 (§1.3 M23): 씬과 UI 프리팹의 모든 TMP 글자 중 기본 크기 &lt; 18, 또는 자동 크기 최소 &lt; 16(최대 &lt; 18)을
        /// 계층 경로와 함께 한 번의 경고로 남긴다. 빈 글자(런타임에 채움) · 피해 숫자 틀 · 대신 글자(Fallback)는 뺀다. 빌드는 막지 않는다.
        /// </summary>
        static void LintText(RectTransform canvas)
        {
            var problems = new List<string>();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true)) CheckText(label, PathOf(label.transform), problems);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var label in prefab.GetComponentsInChildren<TMP_Text>(true))
                    CheckText(label, $"{System.IO.Path.GetFileName(path)}:{PathOf(label.transform)}", problems);
            }
            if (problems.Count == 0) return;
            Debug.LogWarning($"[IdleSceneBuilder] 글자 크기 하한(기본 {UIType.Floor:0}, 자동 최소 {UIType.AutoSizeFloor:0}) 아래 {problems.Count}개 — 경고만 (M23):\n"
                             + JoinLimited(problems));
        }

        static void CheckText(TMP_Text label, string path, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(label.text) || IsTextLintExempt(label.transform)) return;
            if (label.enableAutoSizing)
            {
                if (label.fontSizeMin < UIType.AutoSizeFloor || label.fontSizeMax < UIType.Floor)
                    problems.Add($"{path} (자동 {label.fontSizeMin:0.#}~{label.fontSizeMax:0.#})");
            }
            else if (label.fontSize < UIType.Floor)
            {
                problems.Add($"{path} ({label.fontSize:0.#})");
            }
        }

        /// <summary>글자 검사에서 빼는 것: 피해 숫자 틀 · 대신 글자 (그림이 없을 때만 보이는 글자)</summary>
        static bool IsTextLintExempt(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                string name = t.name;
                if (name == "NumberTemplate" || name == "Fallback" || name.StartsWith("Damage", System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// 누르는 자리 검사 (M22): 버튼의 누르는 그림 크기 + raycastPadding이 88보다 작으면 경고 (기준 1080x1920에서 앵커로 잰다 —
        /// 레이아웃 그룹 · 크기 맞춤이 정하는 크기는 모르므로 건너뛴다). 스크롤 목록 안 칸 · 전투 시너지 줄은 뺀다.
        /// 같은 부모 아래 두 버튼의 넓힌 누르는 자리가 겹치면 따로 경고한다.
        /// </summary>
        static void LintHitTargets(RectTransform canvas)
        {
            var small = new List<string>();
            var overlaps = new List<string>();
            var byParent = new Dictionary<Transform, List<(Button button, Rect hit)>>();
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                var graphic = button.targetGraphic;
                if (graphic == null || !graphic.raycastTarget || IsHitLintExempt(button)) continue;
                var rt = graphic.rectTransform;
                if (!TryReferenceSize(rt, canvas, out var size)) continue;
                var pad = graphic.raycastPadding;
                float width = size.x - pad.x - pad.z;
                float height = size.y - pad.y - pad.w;
                if (width < UISize.MinHit - 0.5f || height < UISize.MinHit - 0.5f) small.Add($"{PathOf(button.transform)} ({width:0}x{height:0})");

                if (rt != button.transform || !TryReferenceRect(rt, canvas, out var rect)) continue;
                var hit = Rect.MinMaxRect(rect.xMin + pad.x, rect.yMin + pad.y, rect.xMax - pad.z, rect.yMax - pad.w);
                var parent = rt.parent;
                if (!byParent.TryGetValue(parent, out var siblings)) byParent[parent] = siblings = new List<(Button, Rect)>();
                foreach (var other in siblings)
                {
                    if (other.hit.Overlaps(hit)) overlaps.Add($"{PathOf(other.button.transform)} ↔ {button.name}");
                }
                siblings.Add((button, hit));
            }
            if (small.Count > 0)
                Debug.LogWarning($"[IdleSceneBuilder] 누르는 자리 {UISize.MinHit:0}px 미만 버튼 {small.Count}개 — 경고만 (M22, UIKit.HitPad로 넓힌다):\n" + JoinLimited(small));
            if (overlaps.Count > 0)
                Debug.LogWarning($"[IdleSceneBuilder] 넓힌 누르는 자리가 겹치는 버튼 {overlaps.Count}쌍 — 경고만 (M22, HitPad의 maxL/R/B/T로 줄인다):\n" + JoinLimited(overlaps));
        }

        /// <summary>누르는 자리 검사에서 빼는 버튼: 스크롤 목록 안의 칸 (폭이 간격에 묶임) · 전투 시너지 줄</summary>
        static bool IsHitLintExempt(Button button)
        {
            if (button.GetComponentInParent<ScrollRect>(true) != null) return true;
            for (var t = button.transform; t != null; t = t.parent)
            {
                if (t.name == "SynergyRow" || t.name == "SynergySummary") return true;
            }
            return false;
        }

        /// <summary>기준 캔버스에서의 크기 (앵커 · sizeDelta로 잰다). 레이아웃 그룹 · 크기 맞춤이 정하는 크기면 false</summary>
        static bool TryReferenceSize(RectTransform rt, RectTransform canvas, out Vector2 size)
        {
            size = default;
            if (rt == canvas)
            {
                size = ReferenceCanvas;
                return true;
            }
            if (rt.GetComponent<ContentSizeFitter>() != null || rt.GetComponent<AspectRatioFitter>() != null) return false;
            if (!(rt.parent is RectTransform parent) || IsLayoutDriven(rt, parent)) return false;
            bool stretchX = !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);
            bool stretchY = !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);
            var parentSize = Vector2.zero;
            if ((stretchX || stretchY) && !TryReferenceSize(parent, canvas, out parentSize)) return false;
            size = new Vector2(
                stretchX ? parentSize.x * (rt.anchorMax.x - rt.anchorMin.x) + rt.sizeDelta.x : rt.sizeDelta.x,
                stretchY ? parentSize.y * (rt.anchorMax.y - rt.anchorMin.y) + rt.sizeDelta.y : rt.sizeDelta.y);
            return size.x > 0f && size.y > 0f;
        }

        /// <summary>부모 기준 사각형 (부모 왼쪽 아래 = 0). 같은 부모의 형제끼리 비교할 때만 쓴다</summary>
        static bool TryReferenceRect(RectTransform rt, RectTransform canvas, out Rect rect)
        {
            rect = default;
            if (!(rt.parent is RectTransform parent) || !TryReferenceSize(rt, canvas, out _) || !TryReferenceSize(parent, canvas, out var parentSize))
                return false;
            var min = Vector2.Scale(rt.anchorMin, parentSize) + rt.offsetMin;
            var max = Vector2.Scale(rt.anchorMax, parentSize) + rt.offsetMax;
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        /// <summary>부모 레이아웃 그룹이 이 자식의 크기를 정하는지 (ignoreLayout이면 아니다)</summary>
        static bool IsLayoutDriven(RectTransform rt, RectTransform parent)
        {
            if (rt.TryGetComponent<LayoutElement>(out var element) && element.ignoreLayout) return false;
            if (parent.GetComponent<GridLayoutGroup>() != null) return true;
            var group = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            return group != null && (group.childControlWidth || group.childControlHeight);
        }

        static string JoinLimited(List<string> lines)
        {
            if (lines.Count <= LintListLimit) return string.Join("\n", lines);
            return string.Join("\n", lines.GetRange(0, LintListLimit)) + $"\n… 외 {lines.Count - LintListLimit}개";
        }
    }
}
