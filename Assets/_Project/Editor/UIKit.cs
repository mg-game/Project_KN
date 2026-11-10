using System;
using System.IO;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>씬 빌더용 uGUI 생성 헬퍼</summary>
    internal static class UIKit
    {
        public static readonly Vector2 TopLeft = new(0f, 1f);
        public static readonly Vector2 TopCenter = new(0.5f, 1f);
        public static readonly Vector2 TopRight = new(1f, 1f);
        public static readonly Vector2 MiddleLeft = new(0f, 0.5f);
        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Vector2 MiddleRight = new(1f, 0.5f);
        public static readonly Vector2 BottomLeft = new(0f, 0f);
        public static readonly Vector2 BottomCenter = new(0.5f, 0f);
        public static readonly Vector2 BottomRight = new(1f, 0f);

        public static TMP_FontAsset DefaultFont;
        public static Sprite RoundSprite;
        public static Sprite CircleSprite;

        // ───────────── 생성 ─────────────

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        public static Image Img(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        public static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, bool wrap = false)
        {
            var label = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            // Ellipsis는 세로로도 잘라서 줄 높이가 큰 한글 폰트는 통째로 사라질 수 있다. 필요한 곳에서만 Ellipsis()로 켠다.
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>가로로 넘치는 한 줄 텍스트를 …으로 자른다. 영역 높이는 글자 크기의 1.5배 이상이어야 한다.</summary>
        public static TextMeshProUGUI Ellipsis(this TextMeshProUGUI label)
        {
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>단색 버튼을 눌렀을 때 색 (M61: 0.8 회색 대신 살짝 라벤더 — 누름 크기 변화(PressScale)와 같은 0.06초)</summary>
        public static readonly Color PressedTint = new(0.92f, 0.90f, 0.95f, 1f);
        /// <summary>단색 버튼에 손을 올렸을 때 색</summary>
        public static readonly Color HighlightedTint = new(0.95f, 0.95f, 0.95f, 1f);
        /// <summary>단색 버튼 색 바뀜 시간 (누름 0.06초에 맞춤)</summary>
        public const float TintFade = 0.06f;

        public static Button Btn(Transform parent, string name, string label, Color color, float fontSize, out TextMeshProUGUI labelText, bool rounded = true)
        {
            var image = Img(parent, name, color, rounded ? RoundSprite : null, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = HighlightedTint;
            colors.pressedColor = PressedTint;
            colors.selectedColor = Color.white;
            colors.fadeDuration = TintFade;
            // 아트 버튼이 비활성일 때 갈색으로 탁해지지 않게 옅은 라벤더 + 반투명 (아트 버튼은 UiArt.SetButtonArt가 스프라이트 교체로 바꾼다)
            colors.disabledColor = new Color(0.86f, 0.84f, 0.94f, 0.8f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            image.gameObject.AddComponent<PressScale>();

            labelText = Txt(image.transform, "Label", label, fontSize, UITheme.Text, TextAlignmentOptions.Center, true);
            labelText.rectTransform.Stretch(8, 8, 0, 0);
            return button;
        }

        public static Button Btn(Transform parent, string name, string label, Color color, float fontSize) =>
            Btn(parent, name, label, color, fontSize, out _);

        /// <summary>닫기 버튼: 바느질 X 단추 그림(ui_button_close). 아트가 없으면 막대 두 개로 그린 X</summary>
        public static Button CloseButton(Transform parent)
        {
            var button = Btn(parent, "CloseButton", string.Empty, UITheme.Secondary, 10, out var label);
            Object.DestroyImmediate(label.gameObject);
            var art = UiArt.Close;
            if (art != null)
            {
                var image = (Image)button.targetGraphic;
                image.sprite = art;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                return button;
            }
            foreach (float angle in new[] { 45f, -45f })
            {
                var bar = Img(button.transform, "Bar", UITheme.Text);
                bar.rectTransform.At(Center, Vector2.zero, new Vector2(38, 6));
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            return button;
        }

        public static Toggle CheckToggle(Transform parent, string name, string label, float fontSize)
        {
            var root = NewRect(name, parent);
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };

            // 상자 = 보라 바느질 안쪽 판, 체크 = 금색 체크 아이콘 (아트가 없으면 둥근 사각형)
            var box = UiArt.Panel(root, "Box", UiArt.PanelInset9s, 3f, null, UITheme.PanelLight, true);
            box.rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(56, 56));
            var checkArt = UiArt.Check;
            var check = UiArt.IsArt(checkArt)
                ? UiArt.Icon(box.transform, "Check", checkArt, Center, new Vector2(2, 2), 44)
                : Img(box.transform, "Check", UITheme.Gold, RoundSprite);
            if (!UiArt.IsArt(checkArt)) check.rectTransform.At(Center, Vector2.zero, new Vector2(32, 32));

            var text = Txt(root, "Label", label, fontSize, UITheme.Text, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Stretch(72, 0, 0, 0);
            text.raycastTarget = true;

            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>세로 스크롤 리스트. 반환값은 항목을 붙일 Content.</summary>
        public static RectTransform ScrollList(RectTransform root, out ScrollRect scroll)
        {
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var viewport = Img(root, "Viewport", Color.clear, null, true).rectTransform.Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = TopLeft;
            content.anchorMax = TopRight;
            content.pivot = TopCenter;
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static VerticalLayoutGroup AddVerticalList(RectTransform content, float spacing, RectOffset padding)
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static GridLayoutGroup AddGrid(RectTransform content, Vector2 cell, Vector2 spacing, int columns, RectOffset padding)
        {
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.padding = padding;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            return grid;
        }

        public static HorizontalLayoutGroup AddRow(RectTransform rt, float spacing)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            return row;
        }

        public static GameObject SetInactive(this Component component)
        {
            component.gameObject.SetActive(false);
            return component.gameObject;
        }

        // ───────────── 배치 ─────────────

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>위쪽 고정, 가로로 늘림</summary>
        public static RectTransform Top(this RectTransform rt, float top, float height, float left = 0, float right = 0)
        {
            rt.anchorMin = TopLeft;
            rt.anchorMax = TopRight;
            rt.pivot = TopCenter;
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>아래쪽 고정, 가로로 늘림</summary>
        public static RectTransform Bottom(this RectTransform rt, float bottom, float height, float left = 0, float right = 0)
        {
            rt.anchorMin = BottomLeft;
            rt.anchorMax = BottomRight;
            rt.pivot = BottomCenter;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
            return rt;
        }

        /// <summary>한 점 기준 고정 크기 배치 (anchor = pivot)</summary>
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        // ───────────── 에셋 ─────────────

        public static T SavePrefab<T>(GameObject go, string path) where T : Component
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }

        /// <summary>
        /// 안티앨리어싱된 둥근 사각형 스프라이트 생성 (9-slice). radius = size/2 이면 원.
        /// RoundSprite/CircleSprite는 아트가 없을 때의 자리표시와 마스크 · 어둡게 덮기 · 터치 영역에만 쓴다 (보이는 그림은 UiArt).
        /// </summary>
        public static Sprite RoundedSprite(string path, int size, int radius, bool sliced)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        var nearest = new Vector2(Mathf.Clamp(p.x, radius, size - radius), Mathf.Clamp(p.y, radius, size - radius));
                        float alpha = Mathf.Clamp01(radius - Vector2.Distance(p, nearest) + 0.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ───────────── 흰 네모 · 글자 외곽선 재질 (M02 · M81 · M82) ─────────────

        const string WhiteSpritePath = IdleProjectSetup.Root + "/Sprites/UI/UI_White.png";
        const int WhiteSpriteSize = 4;

        static Sprite _whiteSprite;
        static Material _outlineMaterial;
        static Material _badgeOutlineMaterial;

        /// <summary>
        /// 4x4 불투명 흰 스프라이트 (Sprites/UI/UI_White.png, 테두리 없음 · FullRect · Bilinear · Clamp · 밉맵 없음).
        /// Filled 이미지에 스프라이트가 없으면 fillAmount가 무시되어 막대가 줄지 않는다 (M02) — Filled()가 비어 있을 때 이것을 넣는다.
        /// 처음 쓸 때 만들고 불러 둔다 (IdleSceneBuilder가 따로 준비할 필요 없음).
        /// </summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null) _whiteSprite = LoadWhiteSprite();
                return _whiteSprite;
            }
        }

        static Sprite LoadWhiteSprite()
        {
            string path = WhiteSpritePath;
            if (!File.Exists(path))
            {
                var texture = new Texture2D(WhiteSpriteSize, WhiteSpriteSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[WhiteSpriteSize * WhiteSpriteSize];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool changed = importer.textureType != TextureImporterType.Sprite
                               || importer.spriteImportMode != SpriteImportMode.Single
                               || importer.spriteBorder != Vector4.zero
                               || settings.spriteMeshType != SpriteMeshType.FullRect
                               || importer.alphaIsTransparency
                               || importer.mipmapEnabled
                               || importer.wrapMode != TextureWrapMode.Clamp
                               || importer.filterMode != FilterMode.Bilinear
                               || importer.textureCompression != TextureImporterCompression.Uncompressed;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = Vector4.zero;
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// 전장 위 맨글자용 공유 외곽선 재질 (M81, Fonts/NotoSansKR-Regular SDF Outline.mat — 두께 0.18, Background 색, 부드러움 0, 팽창 0.05).
        /// IdleProjectSetup이 빌드마다 값을 다시 입힌다. 없으면 DefaultFont로 만든다 (DefaultFont도 없으면 null).
        /// </summary>
        public static Material OutlineMaterial
        {
            get
            {
                if (_outlineMaterial == null) _outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(IdleProjectSetup.OutlineMaterialPath);
                if (_outlineMaterial == null && DefaultFont != null) _outlineMaterial = IdleProjectSetup.EnsureOutlineMaterial(DefaultFont);
                return _outlineMaterial;
            }
        }

        /// <summary>빨간 점 위 흰 숫자 · NEW 글자의 외곽선 재질 (M82, Fonts/NotoSansKR-Regular SDF Badge.mat — BadgeTextOutline #8A1F3A, 두께 0.2)</summary>
        public static Material BadgeOutlineMaterial
        {
            get
            {
                if (_badgeOutlineMaterial == null) _badgeOutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(IdleProjectSetup.BadgeOutlineMaterialPath);
                if (_badgeOutlineMaterial == null && DefaultFont != null) _badgeOutlineMaterial = IdleProjectSetup.EnsureBadgeOutlineMaterial(DefaultFont);
                return _badgeOutlineMaterial;
            }
        }

        /// <summary>
        /// 공유 외곽선 재질을 입힌다 (fontSharedMaterial만 — 인스턴스 재질을 만들지 않아 프리팹에 그대로 저장된다).
        /// 판 없이 전장 그림 위에 바로 그리는 글자에만 쓴다 (상점 HP 값, 바구니 시간, 등불 %, 보스 진행, 고리 안내, 받침 없는 이름표).
        /// 크림 카드 · 패널 · 버튼 위 글자, Ink/InkSub 글자, 피해 숫자에는 쓰지 않는다.
        /// </summary>
        public static T Outlined<T>(this T label) where T : TMP_Text
        {
            var material = OutlineMaterial;
            if (material != null) label.fontSharedMaterial = material;
            return label;
        }

        // ───────────── 글자 크기 하한 (M23) ─────────────

        /// <summary>
        /// 자동 크기: 최대 = 지금 글자 크기, 최소 = max(minSize, 16) (UIType.AutoSizeFloor).
        /// 16보다 작게 요청하면 16으로 올리고 계층 경로와 함께 경고한다 (올린 만큼 칸이 모자라면 그 화면 담당이 칸을 키운다).
        /// MenuSceneBuilder.Fit · MetaSceneBuilder.Fit · CharacterSceneBuilder.AutoSize · TutorialSceneBuilder.Art 글자 도우미가 이것을 부른다.
        /// </summary>
        public static T FitFloor<T>(this T label, float minSize) where T : TMP_Text => FitFloor(label, minSize, label.fontSize);

        /// <summary>자동 크기 minSize ~ maxSize (minSize 하한 16, 경고는 위와 같다)</summary>
        public static T FitFloor<T>(this T label, float minSize, float maxSize) where T : TMP_Text
        {
            if (minSize < UIType.AutoSizeFloor)
            {
                Debug.LogWarning($"[UIKit] 자동 크기 최소 {minSize:0.#}px는 하한 {UIType.AutoSizeFloor:0}px보다 작아 올립니다: {PathOf(label.transform)}");
            }
            float min = Mathf.Max(minSize, UIType.AutoSizeFloor);
            label.enableAutoSizing = true;
            label.fontSizeMax = Mathf.Max(maxSize, min);
            label.fontSizeMin = min;
            return label;
        }

        /// <summary>계층 경로 "Canvas/SafeArea/…/Label" (경고 · 검사 글자용)</summary>
        public static string PathOf(Transform transform)
        {
            if (transform == null) return "(없음)";
            var builder = new System.Text.StringBuilder(transform.name);
            for (var t = transform.parent; t != null; t = t.parent) builder.Insert(0, t.name + "/");
            return builder.ToString();
        }

        // ───────────── 누르는 자리 (M22) ─────────────

        /// <summary>
        /// 누르는 자리를 넓힌다 (자식을 만들지 않고 Graphic.raycastPadding으로): 보이는 크기가 minW x minH보다 작으면 모자란 만큼 양쪽으로 반씩.
        /// 각 쪽은 maxL / maxR / maxB / maxT까지 (옆에 다른 버튼이 있으면 그 사이 간격의 절반을 넘기지 않게 부르는 쪽이 준다).
        /// 버튼의 targetGraphic(이미 raycastTarget=true인 이미지)에 쓴다. 크기는 지금 RectTransform 값으로 잰다 —
        /// 레이아웃 그룹이 크기를 정하는 버튼은 크기를 아는 HitPad(size, …)를 쓴다. 기본 96은 UISize.HitTarget, 하한은 UISize.MinHit 88.
        /// </summary>
        public static T HitPad<T>(this T graphic, float minW = UISize.HitTarget, float minH = UISize.HitTarget,
            float maxL = 99f, float maxR = 99f, float maxB = 99f, float maxT = 99f) where T : Graphic =>
            HitPad(graphic, KnownSize(graphic.rectTransform), minW, minH, maxL, maxR, maxB, maxT);

        /// <summary>HitPad (보이는 크기를 직접 준다)</summary>
        public static T HitPad<T>(this T graphic, Vector2 size, float minW = UISize.HitTarget, float minH = UISize.HitTarget,
            float maxL = 99f, float maxR = 99f, float maxB = 99f, float maxT = 99f) where T : Graphic
        {
            float x = Mathf.Max(0f, (minW - size.x) * 0.5f);
            float y = Mathf.Max(0f, (minH - size.y) * 0.5f);
            graphic.raycastPadding = new Vector4(
                -Mathf.Min(x, Mathf.Max(0f, maxL)),
                -Mathf.Min(y, Mathf.Max(0f, maxB)),
                -Mathf.Min(x, Mathf.Max(0f, maxR)),
                -Mathf.Min(y, Mathf.Max(0f, maxT)));
            return graphic;
        }

        /// <summary>빌드 시점에 알 수 있는 크기 (축마다 앵커가 한 점이면 sizeDelta, 아니면 지금 rect)</summary>
        public static Vector2 KnownSize(RectTransform rt)
        {
            var rect = rt.rect.size;
            float width = Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x) ? rt.sizeDelta.x : rect.x;
            float height = Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y) ? rt.sizeDelta.y : rect.y;
            return new Vector2(width, height);
        }

        // ───────────── 비용 버튼 · 모두 받기 (M07 · M51) ─────────────

        /// <summary>
        /// 비용 버튼에 세 상태 보기(CostButtonView, M07)를 붙인다. 이름 · 자리는 바꾸지 않는다:
        ///   · 비용 줄 = costText의 부모("Cost", 가운데 정렬 가로 줄). 그 맨 뒤(첫 형제)에 CostPill(어두운 알약, 높이 40, 좌우 12, 레이아웃 제외, 꺼 둠)을 깔고
        ///     줄을 내용 폭으로 줄인다(ContentSizeFitter — 가운데는 그대로라 지금 모양이 같다). 양피지 바탕은 알약 없음.
        ///   · disabledColor = 흰색 (쓸 수 없음 모양은 뷰가 정한다 — 이 버튼만). 버튼 공장의 ButtonLabelDim이 있으면 뗀다 (이름 색은 뷰 몫).
        ///   · label이 null이면 버튼 아래 "Label"을 쓴다.
        /// 상태는 런타임 뷰가 SetState로 바꾼다 (부족 = 누를 수 있음 + Deny).
        /// </summary>
        public static CostButtonView CostBtn(Button button, TextMeshProUGUI costText, CostSurface surface, TextMeshProUGUI label = null)
        {
            var colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            var dim = button.GetComponent<ButtonLabelDim>();
            if (dim != null) Object.DestroyImmediate(dim);

            if (label == null)
            {
                var found = button.transform.Find("Label");
                if (found != null) label = found.GetComponent<TextMeshProUGUI>();
            }

            RectTransform row = null;
            Image icon = null;
            Image pill = null;
            if (costText != null && costText.rectTransform.parent is RectTransform parent && parent != button.transform)
            {
                row = parent;
                for (int i = 0; i < row.childCount && icon == null; i++)
                {
                    var child = row.GetChild(i);
                    if (child == costText.transform || child.name == CostPillName) continue;
                    icon = child.GetComponent<Image>();
                }
                if (surface != CostSurface.Parchment)
                {
                    pill = CostPill(row);
                    if (row.GetComponent<HorizontalLayoutGroup>() != null && row.GetComponent<ContentSizeFitter>() == null)
                        row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            var view = button.GetComponent<CostButtonView>();
            if (view == null) view = button.gameObject.AddComponent<CostButtonView>();
            new Binder(view)
                .Set("_button", button)
                .Set("_skin", button.targetGraphic as Image)
                .Set("_label", label)
                .Set("_costText", costText)
                .Set("_costIcon", icon)
                .Set("_costPill", pill)
                .Set("_costRow", row)
                .SetInt("_surface", (int)surface)
                .Set("_unavailableSprite", RoundSprite)
                .Apply();
            return view;
        }

        /// <summary>비용 알약 오브젝트 이름</summary>
        const string CostPillName = "CostPill";

        /// <summary>비용 뒤 어두운 알약 (밤 알약 그림, 없으면 둥근 사각형 CostPillBack). 비용 줄 폭 + 좌우 12, 높이 40, 맨 뒤, 꺼 둠</summary>
        static Image CostPill(RectTransform row)
        {
            var existing = row.Find(CostPillName);
            if (existing != null) return existing.GetComponent<Image>();
            var art = UiArt.PillNight9s;
            Image pill;
            if (UiArt.IsArt(art))
            {
                pill = UiArt.Panel(row, CostPillName, art, UiArt.CapsulePpu(UISize.CostPillHeight), new Color(1f, 1f, 1f, 0.92f), UITheme.CostPillBack);
            }
            else
            {
                pill = Img(row, CostPillName, UITheme.CostPillBack, RoundSprite);
                // RoundSprite 모서리 24 → 높이 40 캡슐 (24 / 1.2 = 20)
                pill.pixelsPerUnitMultiplier = RoundSprite != null ? Mathf.Max(1f, RoundSprite.border.x / (UISize.CostPillHeight * 0.5f)) : 1f;
            }
            pill.raycastTarget = false;
            var rt = pill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = Center;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(UISize.CostPillPad * 2f, UISize.CostPillHeight);
            pill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rt.SetAsFirstSibling();
            pill.gameObject.SetActive(false);
            return pill;
        }

        /// <summary>
        /// 모두 받기 버튼 (M51). 이름은 항상 "ClaimAllButton" (우편 테스트 · 원정 튜토리얼 · 일지 코치 경로가 찾는다).
        /// 금 버튼(button_gold_9s) + "모두 받기" Ink 굵게 (자동 크기 22 ~ fontSize), 개수 배지는 중심이 오른쪽 위 모서리에서 (-6, -6) (이름에 "(N)"을 붙이지 않는다).
        /// disabledColor 흰색 — 규칙은 SetActive(count &gt; 0)이지 interactable=false가 아니다. 높이가 88보다 작으면 누르는 자리를 위아래로 넓힌다.
        /// 자리: 탭 패널 머리 오른쪽 240x64 · 28 (원정), 팝업 아래 오른쪽 380x96 · 32 (수선 일지 · 우편). 위치는 부르는 쪽이 정한다 (크기만 정해 둔다).
        /// 개수가 0에서 생길 때의 0.85 → 1 튀어나옴(UIMotion.ClaimAllPop)은 부르는 쪽 몫.
        /// </summary>
        public static Button ClaimAllButton(RectTransform parent, float width, float height, float fontSize, out TextMeshProUGUI label, out Badge badge)
        {
            var button = SceneParts.Button(parent, "ClaimAllButton", "모두 받기", SceneParts.ButtonStyle.Gold, fontSize, out label);
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(width, height);
            var image = (Image)button.targetGraphic;
            if (image.type == Image.Type.Sliced && UiArt.IsArt(image.sprite)) image.pixelsPerUnitMultiplier = UiArt.ButtonPpu(height);
            label.rectTransform.Stretch(16, 16, 0, 0);
            label.FitFloor(Mathf.Min(22f, fontSize), fontSize);

            var colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;

            badge = SceneParts.BuildBadge(button.transform);
            // 배지 중심 = 오른쪽 위 모서리에서 (-6, -6). BuildBadge는 오른쪽 위 피벗이라 anchoredPosition = 중심 + 크기/2 (40px → (14, 14))
            var badgeRect = (RectTransform)badge.transform;
            float badgeCorner = badgeRect.sizeDelta.x * 0.5f - 6f;
            badgeRect.anchoredPosition = new Vector2(badgeCorner, badgeCorner);
            badge.gameObject.SetActive(false);

            image.HitPad(new Vector2(width, height), 0f, UISize.MinHit);
            return button;
        }
    }

    /// <summary>[SerializeField] private 필드에 참조를 연결한다. 필드명이 틀리면 즉시 예외.</summary>
    internal sealed class Binder
    {
        readonly SerializedObject _so;

        public Binder(Object target)
        {
            _so = new SerializedObject(target);
        }

        public Binder Set(string field, Object value)
        {
            Find(field).objectReferenceValue = value;
            return this;
        }

        /// <summary>
        /// 필드가 있으면 연결하고 없으면 조용히 넘어간다 (다른 작업 묶음이 곧 더할 필드 — 예: UIPopup._dimGroup, Badge._newPill).
        /// 필드가 생기면 Set과 같다.
        /// </summary>
        public Binder TrySet(string field, Object value)
        {
            var prop = _so.FindProperty(field);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) return this;
            prop.objectReferenceValue = Coerce(prop, value);
            return this;
        }

        /// <summary>
        /// GameObject를 컴포넌트 필드에 넣을 때 그 오브젝트의 맞는 컴포넌트로 바꾼다 (필드 형식 "PPtr&lt;$Image&gt;" → GetComponent("Image"), 반대도 같다).
        /// 아직 모르는 필드 형식(다른 묶음이 곧 더할 필드)에 GameObject든 컴포넌트든 넘겨도 맞게 들어가게.
        /// </summary>
        static Object Coerce(SerializedProperty prop, Object value)
        {
            // 스크립트 형식은 "PPtr<$Image>", 엔진 형식은 "PPtr<GameObject>" · "PPtr<RectTransform>"
            string type = prop.type;
            const string prefix = "PPtr<";
            if (value == null || type == null || !type.StartsWith(prefix, StringComparison.Ordinal) || !type.EndsWith(">", StringComparison.Ordinal)) return value;
            string typeName = type.Substring(prefix.Length, type.Length - prefix.Length - 1).TrimStart('$');
            if (typeName == "GameObject") return value is Component component ? component.gameObject : value;
            if (value.GetType().Name == typeName) return value;
            var go = value as GameObject ?? (value as Component)?.gameObject;
            if (go == null) return value;
            var match = go.GetComponent(typeName);
            return match != null ? match : value;
        }

        /// <summary>그 이름의 직렬화 필드가 있는지</summary>
        public bool Has(string field) => _so.FindProperty(field) != null;

        /// <summary>int/enum 필드 (enum은 실제 값)</summary>
        public Binder SetInt(string field, int value)
        {
            Find(field).intValue = value;
            return this;
        }

        public Binder SetArray(string field, params Object[] values)
        {
            var prop = Find(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            return this;
        }

        public void Apply() => _so.ApplyModifiedPropertiesWithoutUndo();

        SerializedProperty Find(string field) =>
            _so.FindProperty(field) ?? throw new InvalidOperationException($"{_so.targetObject.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
    }
}
