using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Art 폴더(Assets/_Project/Art/**)의 PNG 임포트 규칙.
    ///   - 전부 Sprite(Single), 알파 투명, 최대 2048, 고품질 압축(모바일 ASTC / PC BC7)
    ///   - 일반 아트(캐릭터 · 아이콘 · 초상화 · 아이템 · UI 조각 · Synergy · Story · Meta)는 밉맵 + 카이저 필터 + 트라이리니어 + 밉맵 바이어스 -0.3.
    ///     256~1024px 그림을 40~200px로 줄여 그리는 곳이 많아서, 밉맵이 없으면 초상화 · 아이콘 가장자리가 계단처럼 깨진다.
    ///     화면 크기 배경(Story/*, UI/bg_*, UI/*banner*, UI/*_header, UI/content_bg, Battle/field)은
    ///     거의 원래 크기로 그리므로 밉맵을 만들지 않는다 (메모리 절약).
    ///     UI/portrait_bg(1024px)와 UI/card_bg_*(384x512)는 배경이지만 초상화 · 카드 크기(100~260px)로 줄여 그리므로 밉맵을 만든다.
    ///     UI/zone_exp_*(1344x672)도 원정 목록 카드(296x148, 약 4.5배 축소)에서 밉맵이 없으면 가장자리가 계단처럼 깨지고 스크롤할 때 깜빡여서 밉맵을 만든다 (v7).
    ///   - 이름이 _9s로 끝나면 9-slice 테두리 48px(리본은 BorderOverrides) + FullRect 메시 + 무압축(테두리 번짐 방지)
    ///     + 밉맵(카이저, 트라이리니어 — 배율 1.6~4.8로 줄여 그리는 바느질 테두리가 깨지지 않게)
    ///   - FX 폴더(코드로 만든 흰색 글로우 + 힉스필드로 그린 흰색 · 크림색 틴트용 그림: fx_sparkle, fx_ring_thread, fx_confetti_* 등)는
    ///     Clamp + FullRect + 무압축(발광 그라데이션 밴딩 방지) + 밉맵(박스 필터, 트라이리니어 — 작게 흩뿌릴 때 깜빡임 방지)
    ///   - VFX 폴더(스킬 연출 · 공격 연출 · 스테이지 덮개 그림)는 FullRect + Clamp + 고품질 압축.
    ///     스킬/공격 연출은 HUD · 스킬 구슬 · 말풍선에서 40~120px로 줄여 그리고 회전 · 확대 연출이 많아서
    ///     밉맵 + 트라이리니어로 축소 깜빡임을 막고, 최대 512로 자른다.
    ///     스테이지 덮개(overlay_*)는 화면 크기로 깔리므로 밉맵 없이 최대 1024 (1080x810 → 1024x768, 블록 압축이 가능한 4의 배수).
    ///     (무압축이면 연출 33장 + 덮개 6장이 RGBA32로 약 55MB를 늘 차지하고 빌드 용량도 그만큼 커진다)
    ///   - Synergy 폴더(조합 문장)는 아이콘과 같은 규칙(Tight 메시 + 고품질 압축 + 밉맵)
    ///   - Art/Story(첫 실행 이야기 장면), Art/Meta도 같은 규칙으로 임포트한다.
    ///   - FX-JUICE (v6): Art/FX/Juice/*(빛 그림)는 FX 규칙, Art/VFX/Juice/*(색 그림)는 VFX 규칙을 그대로 따른다.
    ///     영웅 그림 hero_*(보스 처치·불 밝히기·스테이지 클리어, 화면 420~620px)는 최대 1024 (Android ASTC 4x4),
    ///     플립북 fb_*_4x4는 최대 1024, 연속 처치 판 plate_combo_9s는 9-slice 테두리 (150, 40, 150, 40).
    ///   - v9 (PHASE4 P4-10): 출석 칸 ui_attendance_cell(_gold)_9s(192x256)는 테두리 (44, 44, 44, 88) — 위 머리띠가 테두리 안에 들어간다.
    ///     모바일(Android · iOS) 크기 제한: Characters/ · Costumes/(1024)는 최대 768 + ASTC 4x4 (상주 메모리 약 52MB → 29MB),
    ///     Meta/*_banner(1280x480 해금 카드 띠)는 ASTC 6x6. PC(Standalone) 설정은 그대로.
    ///     모바일 플랫폼 설정(영웅 그림 Android 포함)은 ApplyMobileSizes 한곳에서 정하고, 규칙에 맞지 않는 그림은 덮어쓰기를 지운다
    ///     (규칙 하나를 빼고 버전을 올리면 그 그림들은 기본 설정으로 돌아간다).
    /// </summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        const string ArtRoot = IdleProjectSetup.Root + "/Art/";
        const string FxFolder = ArtRoot + "FX/";
        const string VfxFolder = ArtRoot + "VFX/";
        const string NineSliceSuffix = "_9s";
        /// <summary>스테이지 기믹 덮개 파일 이름 앞부분 (SkillVfx.Overlay)</summary>
        const string OverlayPrefix = "overlay_";
        const int NineSliceBorder = 48;
        const int MaxSize = 2048;
        const int VfxMaxSize = 512;
        const int OverlayMaxSize = 1024;
        /// <summary>일반 아트 밉맵 바이어스 (음수 = 조금 더 선명한 밉 단계를 고른다)</summary>
        const float DefaultMipBias = -0.3f;

        /// <summary>
        /// 테두리가 48px가 아닌 9-slice (파일 이름 → 왼쪽, 아래, 오른쪽, 위).
        /// 리본은 양 끝 꼬리 · 접힘이 테두리 안에 들어가야 늘려도 모양이 유지된다.
        /// </summary>
        static readonly Dictionary<string, Vector4> BorderOverrides = new()
        {
            { "ui_ribbon_gold_9s", new Vector4(140, 40, 140, 40) },
            { "ui_ribbon_lavender_9s", new Vector4(140, 40, 140, 40) },
            { "ui_ribbon_night_9s", new Vector4(200, 48, 200, 48) },
            // 연속 처치·한꺼번에 알림 판: 양 끝 꼬리가 150px 테두리 안에 들어간다 (FX-JUICE E11)
            { "plate_combo_9s", new Vector4(150, 40, 150, 40) },
            // 새 프레임 상단 · 하단 바 (UX-SPEC-IA B5): 양 끝 장식만 테두리, 가운데는 Tiled로 이어 붙인다 (위아래 테두리 0)
            { "topbar_9s", new Vector4(216, 0, 216, 0) },
            { "navbar_9s", new Vector4(180, 0, 180, 0) },
            // 출석 칸 (ART-UPGRADE B3b, 192x256): 위 30% 머리띠가 늘어나지 않게 위 테두리만 88 (v9)
            { "ui_attendance_cell_9s", new Vector4(44, 44, 44, 88) },
            { "ui_attendance_cell_gold_9s", new Vector4(44, 44, 44, 88) },
        };

        /// <summary>화면 크기로 그리는 영웅 그림 접두어 (최대 1024) / 플립북 접미어 (최대 1024)</summary>
        const string HeroPrefix = "hero_";
        const string FlipbookPrefix = "fb_";
        const string FlipbookSuffix = "_4x4";
        const int HeroMaxSize = 1024;
        /// <summary>영웅 그림 Android ASTC 압축 품질 (0~100)</summary>
        const int HeroCompressionQuality = 100;

        /// <summary>모바일에서 최대 768로 줄이는 폴더 (Art/ 아래 경로 앞부분) — 1024 캐릭터 · 의상 그림 (v9)</summary>
        static readonly string[] MobileCappedPrefixes = { "Characters/", "Costumes/" };
        const int MobileCappedMaxSize = 768;
        /// <summary>Android 플랫폼 이름 (영웅 그림은 Android만 덮어쓴다)</summary>
        const string AndroidPlatform = "Android";
        /// <summary>모바일 플랫폼 이름 (TextureImporter 플랫폼 설정 — "iPhone"은 .meta의 iOS 항목)</summary>
        static readonly string[] MobilePlatforms = { AndroidPlatform, "iPhone" };
        /// <summary>해금 카드 띠 그림 (Meta/feature_*_banner → 모바일 ASTC 6x6, v9)</summary>
        const string MetaFolder = "Meta/";
        const string BannerSuffix = "_banner";

        /// <summary>밉맵을 만들지 않는 화면 크기 배경 (Art/ 아래 경로가 이것으로 시작하면 해당)</summary>
        static readonly string[] NoMipPrefixes =
        {
            "Story/", "UI/bg_", "UI/content_bg", "Battle/field",
        };

        /// <summary>텍스처 종류별 임포트 규칙</summary>
        enum ArtKind
        {
            /// <summary>캐릭터 · 아이콘 · 배경 등 일반 아트</summary>
            Default,
            /// <summary>9-slice 패널/버튼 (_9s)</summary>
            NineSlice,
            /// <summary>FX 폴더: 흰색 글로우 · 틴트용 흰색 그림</summary>
            Glow,
            /// <summary>VFX 폴더: 스킬 연출 · 공격 연출 그림</summary>
            Vfx,
            /// <summary>VFX 폴더의 스테이지 덮개 (overlay_*)</summary>
            Overlay,
        }

        /// <summary>규칙을 바꾸면 올린다 (버전이 바뀌면 유니티가 텍스처를 다시 임포트한다). v6 FX-JUICE, v7 UI/zone_* 밉맵, v8 상단 · 하단 바 테두리, v9 출석 칸 테두리 · 모바일 크기 제한</summary>
        public override uint GetVersion() => 9;

        /// <summary>
        /// Art 폴더 전체를 강제 재임포트해 규칙을 적용한다.
        /// 배치: -executeMethod IdleGame.EditorTools.ArtImportPostprocessor.ReimportArt
        /// </summary>
        [MenuItem("IdleGame/Art/Reimport Art (임포트 규칙 적용)")]
        public static void ReimportArt()
        {
            AssetDatabase.ImportAsset(ArtRoot.TrimEnd('/'),
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!IsArtPng(path)) return;

            var importer = (TextureImporter)assetImporter;
            var kind = KindOf(path);

            ApplyCommon(importer);
            ApplyMipmapsAndSize(importer, kind, path);
            ApplyMeshAndBorder(importer, kind, path);
            ApplyCompression(importer, kind);
            ApplyJuiceSizes(importer, path);
            ApplyMobileSizes(importer, path);
        }

        /// <summary>
        /// FX-JUICE 큰 그림: 영웅 그림 hero_*는 화면 420~620px로 그리므로 최대 1024 (VFX 512 제한을 넘긴다),
        /// 플립북 fb_*_4x4(16칸 1024 시트)도 최대 1024. 영웅 그림의 Android ASTC 4x4 덮어쓰기는 ApplyMobileSizes가 정한다.
        /// </summary>
        static void ApplyJuiceSizes(TextureImporter importer, string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            bool hero = name.StartsWith(HeroPrefix, System.StringComparison.Ordinal);
            bool flipbook = name.StartsWith(FlipbookPrefix, System.StringComparison.Ordinal) && name.EndsWith(FlipbookSuffix, System.StringComparison.Ordinal);
            if (!hero && !flipbook) return;
            importer.maxTextureSize = HeroMaxSize;
        }

        /// <summary>모바일 플랫폼 규칙 (MobileRuleOf — 위에서 먼저 맞는 것 하나)</summary>
        enum MobileRule
        {
            /// <summary>덮어쓰지 않는다 (남아 있던 덮어쓰기는 지운다)</summary>
            None,
            /// <summary>Characters/ · Costumes/: Android · iOS 최대 768 + ASTC 4x4</summary>
            Capped,
            /// <summary>Meta/*_banner: Android · iOS 크기 그대로(2048) + ASTC 6x6</summary>
            Banner,
            /// <summary>hero_*: Android만 최대 1024 + ASTC 4x4 · 품질 100 (FX-JUICE v6, iOS는 덮어쓰지 않는다)</summary>
            Hero,
        }

        /// <summary>그림 하나의 모바일 규칙. 순서: Characters/ · Costumes/ → Meta/*_banner → hero_* → 없음</summary>
        static MobileRule MobileRuleOf(string path)
        {
            string relative = path.Substring(ArtRoot.Length);
            foreach (var prefix in MobileCappedPrefixes)
            {
                if (relative.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return MobileRule.Capped;
            }
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (relative.StartsWith(MetaFolder, System.StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(BannerSuffix, System.StringComparison.Ordinal)) return MobileRule.Banner;
            if (name.StartsWith(HeroPrefix, System.StringComparison.Ordinal)) return MobileRule.Hero;
            return MobileRule.None;
        }

        /// <summary>
        /// 모바일(Android · iOS) 플랫폼 설정을 이곳 한곳에서 정한다 (ART-UPGRADE §4.3 #19, v9 — MobileRule):
        /// Characters/ · Costumes/는 최대 768 + ASTC 4x4 (지금 고품질 압축과 같은 블록, 화면에서 768px보다 크게 그리지 않는다),
        /// Meta/*_banner는 크기 그대로 ASTC 6x6, 영웅 그림 hero_*는 Android만 최대 1024 + ASTC 4x4 (선명하게).
        /// 어느 규칙에도 맞지 않는 플랫폼은 남아 있던 덮어쓰기를 지운다 — 규칙을 빼거나 그림을 폴더 밖으로 옮긴 뒤
        /// 버전을 올려 다시 임포트하면 예전 768 · ASTC 덮어쓰기가 .meta에 남지 않는다 (덮어쓰지 않은 그림은 .meta를 바꾸지 않는다).
        /// PC(Standalone) 설정은 건드리지 않는다.
        /// </summary>
        static void ApplyMobileSizes(TextureImporter importer, string path)
        {
            var rule = MobileRuleOf(path);
            foreach (var platform in MobilePlatforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                bool apply = rule == MobileRule.Capped || rule == MobileRule.Banner
                    || (rule == MobileRule.Hero && platform == AndroidPlatform);
                if (!apply)
                {
                    if (!settings.overridden) continue;
                    settings.overridden = false;
                    importer.SetPlatformTextureSettings(settings);
                    continue;
                }
                settings.overridden = true;
                switch (rule)
                {
                    case MobileRule.Capped:
                        settings.maxTextureSize = MobileCappedMaxSize;
                        settings.format = TextureImporterFormat.ASTC_4x4;
                        break;
                    case MobileRule.Banner:
                        settings.maxTextureSize = MaxSize;
                        settings.format = TextureImporterFormat.ASTC_6x6;
                        break;
                    default:
                        settings.maxTextureSize = HeroMaxSize;
                        settings.format = TextureImporterFormat.ASTC_4x4;
                        settings.compressionQuality = HeroCompressionQuality;
                        break;
                }
                importer.SetPlatformTextureSettings(settings);
            }
        }

        /// <summary>Art 폴더 아래 PNG인지</summary>
        static bool IsArtPng(string path) =>
            path.StartsWith(ArtRoot) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

        static ArtKind KindOf(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith(NineSliceSuffix)) return ArtKind.NineSlice;
            if (path.StartsWith(FxFolder)) return ArtKind.Glow;
            if (path.StartsWith(VfxFolder)) return name.StartsWith(OverlayPrefix) ? ArtKind.Overlay : ArtKind.Vfx;
            return ArtKind.Default;
        }

        /// <summary>모든 아트 공통: 스프라이트 단일, 알파 투명, 밉맵 끔(종류별로 ApplyMipmapsAndSize에서 켠다), 바이리니어, 클램프</summary>
        static void ApplyCommon(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.mipMapBias = 0f;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxSize;
        }

        /// <summary>
        /// 일반 아트는 밉맵 + 카이저 + 트라이리니어(큰 배경은 제외), FX는 밉맵 + 박스 필터,
        /// 스킬/공격 연출은 밉맵 + 트라이리니어 (작게 줄이거나 돌릴 때 깜빡임 방지) + 최대 512,
        /// 스테이지 덮개는 화면 크기로 그리므로 밉맵 없이 최대 1024. 9-slice도 밉맵 + 트라이리니어 (배율로 줄여 그린다).
        /// ApplyMeshAndBorder가 지금 설정을 읽어 다시 쓰므로 그보다 먼저 정한다.
        /// </summary>
        static void ApplyMipmapsAndSize(TextureImporter importer, ArtKind kind, string path)
        {
            switch (kind)
            {
                case ArtKind.Default:
                    if (IsLargeBackdrop(path)) break;
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    importer.borderMipmap = false;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.mipMapBias = DefaultMipBias;
                    break;
                case ArtKind.Glow:
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                    importer.borderMipmap = false;
                    importer.filterMode = FilterMode.Trilinear;
                    break;
                case ArtKind.Vfx:
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                    importer.borderMipmap = false;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.maxTextureSize = VfxMaxSize;
                    break;
                case ArtKind.Overlay:
                    importer.maxTextureSize = OverlayMaxSize;
                    break;
                case ArtKind.NineSlice:
                    // 192px 판 · 96px 캡슐을 배율 1.6~4.8로 줄여 그린다 (테두리 48 → 10~30px, 540px 폭 화면에선 그 절반).
                    // 밉맵이 없으면 바느질 점선 · 금테가 계단처럼 끊기고 깜빡인다. 한 장짜리 그림이라 밉 단계에서도 조각 경계가 생기지 않는다.
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    importer.borderMipmap = false;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.mipMapBias = DefaultMipBias;
                    break;
            }
        }

        /// <summary>밉맵을 만들지 않는 화면 크기 배경 (Story/*, UI/bg_*, UI/*banner*, UI/*_header, UI/content_bg, Battle/field)</summary>
        static bool IsLargeBackdrop(string path)
        {
            string relative = path.Substring(ArtRoot.Length);
            foreach (var prefix in NoMipPrefixes)
            {
                if (relative.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (!relative.StartsWith("UI/", System.StringComparison.OrdinalIgnoreCase)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name.Contains("banner") || name.EndsWith("_header");
        }

        /// <summary>9-slice는 테두리 48px(리본은 BorderOverrides), 9-slice/연출/덮개/FX는 FullRect 메시(늘려 그리거나 돌릴 때 잘림 방지)</summary>
        static void ApplyMeshAndBorder(TextureImporter importer, ArtKind kind, string path)
        {
            bool nineSlice = kind == ArtKind.NineSlice;
            bool fullRect = kind != ArtKind.Default;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = nineSlice ? BorderOf(path) : Vector4.zero;
            importer.SetTextureSettings(settings);
        }

        /// <summary>9-slice 테두리 (왼쪽, 아래, 오른쪽, 위). 기본 48px, BorderOverrides에 있으면 그 값</summary>
        static Vector4 BorderOf(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return BorderOverrides.TryGetValue(name, out var border)
                ? border
                : new Vector4(NineSliceBorder, NineSliceBorder, NineSliceBorder, NineSliceBorder);
        }

        /// <summary>9-slice와 FX 글로우만 무압축, 나머지(스킬 연출 · 덮개 포함)는 고품질 압축</summary>
        static void ApplyCompression(TextureImporter importer, ArtKind kind)
        {
            bool uncompressed = kind == ArtKind.NineSlice || kind == ArtKind.Glow;
            importer.textureCompression = uncompressed
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
        }
    }
}
