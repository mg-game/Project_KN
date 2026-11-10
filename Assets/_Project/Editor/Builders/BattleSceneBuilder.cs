using System;
using System.Collections.Generic;
using System.IO;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 전투 화면 (ctx.StageArea, 기준 1080x1620 — MAIN은 세로 전체, 시트가 열리면 아래쪽이 가려지고 전투가 올라간다 · UX-SPEC-IA §6): 수선점 방어.
    ///   배경(밤 광장) / 전장(바닥 평면: 불빛 웅덩이·부채꼴 요격 범위·금빛 방위 링, 받침대, 수선점, 액터, 연출, 위험 표시)
    ///   HUD(스테이지·보스 진행, 수선점 내구도, AUTO·배속) / 하단 띠(잔향 보유량, 잔향이 덱 6칸, 수선점 불 밝히기) / 배너 / 드래그 미리보기.
    ///   스킬·기믹: 바닥 장판 레이어, 기믹 분위기(덮개·날씨 입자), 시전 말풍선, 알림 띠(레인 사이 빈 곳), 시너지 띠/아이콘 줄,
    ///   상태 표시(보호막·표식·불꽃·왕관…), 잔향이·덱 카드 스킬 배지, 전장 안 번쩍임.
    ///   기본 공격·적 공격·처치 그림(Art/VFX/atk_*, enemy_atk_*, enemy_death_*, enemy_hit_*)도 스킬 그림 묶음에 함께 굽는다.
    ///   계보별 기본 공격 목록(AttackFxCatalog)·흰 번쩍임 머티리얼·연출 레이어 하위 Canvas는 AttackFxBuilder가 만든다.
    ///   방위 링 칸 번쩍임 + "!" 표시·정전 빛 구멍 머티리얼·공격 연출 시험장(개발 빌드)은 EnemyFxBuilder가 만든다.
    /// 아트(ArtLib)가 없으면 Sprites/Battle에 절차적으로 그린 자리표시 스프라이트를 쓴다 (스킬 연출은 흰색 모양 + 파스텔 색).
    /// 전장과 화면은 각각 중첩 Canvas라서 매 프레임 움직여도 다른 UI를 다시 그리지 않는다.
    /// </summary>
    internal static class BattleSceneBuilder
    {
        const float StripHeight = 156f;
        /// <summary>기준 화면에서의 월드 1당 픽셀 / 세로 눌림 / 수선점 위치 (BattleFieldMapper가 런타임에 다시 계산)</summary>
        const float RefPpu = 205f;
        const float RefSquash = 0.735f;
        static readonly Vector2 RefOrigin = new(0f, -63f);

        const float GuardianSize = 132f;
        const float EchoSize = 80f;
        const float ShopWorldWidth = 1f;
        /// <summary>수선점 그림에서 문/바닥 중심이 오는 높이 (원점에 맞춤)</summary>
        static readonly Vector2 ShopPivot = new(0.5f, 0.22f);
        const float CardWidth = 98f;
        const float CardHeight = 122f;

        public static void Build(SceneBuildContext ctx)
        {
            var art = BattleArt.Load();
            var skillArt = SkillArtSet.Load(art);

            var root = NewRect("Battle", ctx.StageArea).Stretch();
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            var view = root.gameObject.AddComponent<BattleView>();
            var library = BuildSkillLibrary(root, skillArt);
            var attackCatalog = AttackFxBuilder.BuildCatalog(root);
            EnemyFxBuilder.ReportMissingArt();
            var flashMaterial = AttackFxBuilder.WhiteFlashMaterial();
            // FX-JUICE: 손맛 그림 묶음 + 입자(미리 곱한 알파) · 빛 번쩍임(스크린) 머티리얼 (셰이더가 없으면 null → 런타임이 기본 머티리얼로)
            var juiceArt = JuiceArtBuilder.Build(root);
            var premulMaterial = AttackFxBuilder.PremulMaterial();
            var screenMaterial = AttackFxBuilder.ScreenMaterial();

            var backdrop = BuildBackground(root, art);

            // ───── 전장 ─────
            var field = NewRect("Field", root).Stretch(0, 0, 0, StripHeight);
            field.gameObject.AddComponent<Canvas>();
            field.gameObject.AddComponent<GraphicRaycaster>();
            // 생성 반지름 밖에서 걸어 들어오는 적이 상단 바를 덮지 않도록 전장 영역으로 자른다
            field.gameObject.AddComponent<RectMask2D>();
            // 전투 카메라 (UX-SPEC-IA §6.1): 시트가 열리면 BattleFrameDriver가 mapper.SetView로 바닥 그림과 함께 올린다.
            // 전장 창(Field)은 그대로 자르고(RectMask2D), 화면 흔들림(ShakeRoot)은 카메라 안에서 그대로 움직인다
            var fieldView = NewRect("Camera", field).Stretch();
            var shake = NewRect("ShakeRoot", fieldView).Stretch();
            var ground = NewRect("GroundPlane", shake).At(Center, RefOrigin, Vector2.zero);
            ground.localScale = new Vector3(1f, RefSquash, 1f);
            var ambient = NewRect("Ambient", shake).Stretch();
            var pads = NewRect("Pads", shake).Stretch();
            // 스킬 장판·마법진 (받침대 위, 액터 아래)
            var groundFx = NewRect("GroundFx", shake).Stretch();
            // 수선점 후광만 액터 뒤에 깔고, 건물은 액터 레이어에서 발 높이로 함께 정렬한다 (위쪽 레인의 적·보스는 건물 뒤로 가려짐)
            var shopBackLayer = NewRect("ShopBackLayer", shake).Stretch();
            var actors = NewRect("Actors", shake).Stretch();
            // 스테이지 기믹 분위기 (비·정전 등은 액터 위로 덮이고, 스킬 빛은 그 위에서 빛난다)
            var ambienceLayer = NewRect("Ambience", shake).Stretch();
            // FX-JUICE 전장 어둡게 (스킬 조명·보스 처치·불 밝히기 임팩트 프레임): Fx 아래라 액터·배경·날씨만 어두워지고 연출·숫자는 밝게 남는다
            var fieldDim = Img(shake, "FieldDim", Alpha(JuiceColors.Dim, 0f));
            fieldDim.rectTransform.Stretch(-24, -24, -24, -24);
            fieldDim.raycastTarget = false;
            fieldDim.enabled = false;
            var fxLayer = NewRect("Fx", shake).Stretch();
            var edge = NewRect("Edge", shake).Stretch();
            // 스킬·기믹 번쩍임은 전장 안에서만 (흔들려도 가장자리가 비지 않게 조금 크게)
            var fieldFlash = Img(shake, "FieldFlash", Alpha(Color.white, 0f));
            fieldFlash.rectTransform.Stretch(-24, -24, -24, -24);
            fieldFlash.enabled = false;
            // 말풍선 다음에 피해 숫자 — 숫자가 말풍선 위에 그려진다 (Wave 3 r2: 말풍선이 막 뜬 숫자를 반쯤 가렸다)
            var calloutLayer = NewRect("Callouts", shake).Stretch();
            var numbers = NewRect("Numbers", shake).Stretch();
            // 매 프레임 움직이는 연출 레이어는 하위 Canvas로 (입자 하나가 움직여도 액터·받침대를 다시 그리지 않게)
            AttackFxBuilder.AddLayerCanvas(groundFx);
            AttackFxBuilder.AddLayerCanvas(ambienceLayer);
            AttackFxBuilder.AddLayerCanvas(fieldDim.rectTransform);
            AttackFxBuilder.AddLayerCanvas(fxLayer);
            AttackFxBuilder.AddLayerCanvas(numbers);

            var mapper = field.gameObject.AddComponent<BattleFieldMapper>();
            new Binder(mapper).Set("_groundPlane", ground).Set("_view", fieldView).Apply();
            if (backdrop != null)
            {
                // 바닥 그림 속 광장 링(월드 반지름 1.6)에 전장을 맞춘다
                new Binder(mapper).Set("_backdropFrame", root).Set("_backdrop", backdrop).Apply();
                SetVector2(mapper, "_backdropSize", art.Field.rect.size);
                if (art.FieldIsTall)
                {
                    // 세로로 늘린 그림(field_tall 1080x2048): 링 값을 그대로 넣는다. MeasurePlazaRing은 쓰지 않는다 —
                    // 찾기 창(그림 높이의 10~90%)이 생성된 픽셀까지 넘어간다 (UX-SPEC-IA §1.4)
                    SetVector2(mapper, "_backdropRingCenter", BattleArt.TallRingCenter);
                    SetVector2(mapper, "_backdropRingRadius", BattleArt.TallRingRadius);
                }
                else if (BattleArtBaker.MeasurePlazaRing(art.Field, out var ringCenter, out var ringRadius))
                {
                    // 그림 속 보라 광장 링을 재서 맞춘다 (못 재면 컴포넌트 기본값 = 현재 field.png 측정값)
                    SetVector2(mapper, "_backdropRingCenter", ringCenter);
                    SetVector2(mapper, "_backdropRingRadius", ringRadius);
                }
            }

            // 바닥 평면: 불빛 웅덩이 → 부채꼴 → 방위 링 → 드래그 미리보기 → 불 밝히기 고리
            var shopLight = Img(ground, "ShopLight", Alpha(UITheme.Lamp, 0.3f), art.Glow);
            shopLight.rectTransform.At(Center, Vector2.zero, Vector2.one * (1.5f * 1.4f * RefPpu));
            var fans = new Image[BattleGeometry.SlotCount];
            for (int i = 0; i < fans.Length; i++)
            {
                fans[i] = Filled(Img(ground, $"Fan{i}", Alpha(UITheme.SlotColor(i), 0.14f), art.RangeDisc), Image.FillMethod.Radial360,
                    (int)Image.Origin360.Top, true);
                fans[i].rectTransform.At(Center, Vector2.zero, Vector2.one * (2f * 1.6f * RefPpu));
                fans[i].fillAmount = 0.2f;
                fans[i].enabled = false;
            }
            float ringDiameter = 2f * RefPpu / BattleArt.RingRadius;
            var ringHalo = Img(ground, "RingHalo", Alpha(UITheme.GoldAccent, 0.2f), art.Ring);
            ringHalo.rectTransform.At(Center, Vector2.zero, Vector2.one * ringDiameter * 1.08f);
            var ring = Img(ground, "Ring", Alpha(UITheme.Lamp, 0.7f), art.Ring);
            ring.rectTransform.At(Center, Vector2.zero, Vector2.one * ringDiameter);
            // 적이 수선점을 때린 방향의 링 칸이 빨갛게 번쩍인다 (ATK-03)
            var ringFlashes = EnemyFxBuilder.BuildSlotFlashes(ground, art.Ring, ringDiameter);
            var aimFan = Filled(Img(ground, "AimFan", Alpha(UITheme.EchoGlow, 0.4f), art.RangeDisc), Image.FillMethod.Radial360,
                (int)Image.Origin360.Top, true);
            aimFan.rectTransform.At(Center, Vector2.zero, Vector2.one * (2f * 2.4f * RefPpu));
            var aimArrow = BuildAimArrow(ground, art, out var aimLine, out var aimHead);
            var lampRing = Img(ground, "LampRing", UITheme.Lamp, art.Ring);
            lampRing.rectTransform.At(Center, Vector2.zero, Vector2.one * 100f);

            var fireflies = BuildFireflies(ambient, art);
            var shop = BuildShop(actors, shopBackLayer, mapper, shopLight.rectTransform, art, skillArt);

            var guardians = new GuardianSlotView[BattleGeometry.SlotCount];
            for (int i = 0; i < guardians.Length; i++) guardians[i] = BuildGuardian(i, actors, pads, fans[i], mapper, art, skillArt, library);

            var enemyTemplate = BuildEnemyTemplate(actors, art, skillArt, flashMaterial);
            var echoTemplate = BuildEchoTemplate(actors, art, skillArt, library, flashMaterial);

            var fx = BuildFx(ctx, fxLayer, groundFx, numbers, shake, mapper, shop, lampRing, fireflies, art, library, fieldFlash);
            new Binder(fx).Set("_catalog", attackCatalog).SetIf("_coinSprite", ArtLib.Icon("currency_gold")).Apply();
            new Binder(fx)
                .Set("_juice", juiceArt)
                .Set("_fieldDim", fieldDim)
                .Set("_premulMaterial", premulMaterial)
                .Set("_screenMaterial", screenMaterial)
                .Set("_flashMaterial", flashMaterial)
                // ATK-08: 녹 얼룩 · 좀나방 구멍 그림 (L0 미지원 — 없으면 굽지 않고 런타임이 흰 모양 + 색)
                .SetIf("_rustStainArt", UiArt.RustStain)
                .SetIf("_mothHoleArt", UiArt.MothHole)
                .Apply();
            var ambience = BuildAmbience(ambienceLayer, art, library);
            // 정전 어둠 덮개에 빛 구멍 (ATK-14, 셰이더가 없으면 구멍 없이)
            new Binder(ambience).SetIf("_holeMaterial", EnemyFxBuilder.AmbienceHoleMaterial()).Apply();
            var callouts = BuildCallouts(calloutLayer, root);
            var enemyPool = actors.gameObject.AddComponent<EnemyViewPool>();
            new Binder(enemyPool).Set("_template", enemyTemplate).Set("_layer", actors).Set("_fx", fx).Set("_shop", shop).Apply();
            var echoPool = actors.gameObject.AddComponent<EchoViewPool>();
            new Binder(echoPool).Set("_template", echoTemplate).Set("_layer", actors).Set("_fx", fx).Set("_shop", shop).Apply();

            var ringView = BuildDangerArrows(edge, mapper, ring, ringHalo, art);
            EnemyFxBuilder.BindRingAlerts(ringView, edge, ringFlashes, skillArt.Motif(VfxMotif.Exclaim));

            // ───── 화면 (HUD / 하단 띠 / 배너 / 드래그) ─────
            // 위기 비네트는 전장 가장자리만 (HUD 아래 띠·덱은 덮지 않는다)
            var vignette = Img(root, "DangerVignette", Alpha(UITheme.Danger, 0f), art.Vignette);
            vignette.rectTransform.Stretch(0, 0, 0, StripHeight);
            vignette.enabled = false;

            var hud = BuildHud(root, art, vignette, out var stageLabel);
            var synergyHud = BuildSynergyHud(hud.transform as RectTransform, art, library);
            // 보스 도전 버튼(M13) · 스테이지 고르기(M13 P2)는 시너지 줄 뒤에 — 같은 자리에서 누름을 먼저 받고 위에 그려진다
            BuildBossChallenge(hud, art);
            BuildStageList(hud, art);
            var banner = BuildBanner(root, art);
            // 연속 실패로 내려갈 때 실패 배너를 '아쉬워요! 1-9에서 힘을 모아요'로 바꿔 단다 (BattleHud → BattleBannerView.ArmStepBack)
            new Binder(hud).Set("_banner", banner).Apply();
            var synergyBanner = BuildSynergyBanner(root, art, library);
            var notice = BuildNotice(root, art, mapper);
            var strip = BuildStrip(root, art, hud, out var deckPanel, out var lamp);
            var aim = BuildAimOverlay(root, mapper, strip, guardians, aimFan, aimArrow, aimLine, aimHead, art);
            var deck = BuildDeck(deckPanel, aim, art, skillArt, library);
            // 맨 아래 금 줄: 아래 띠가 금테 판이면 판 자체의 금실이 있으므로 그리지 않는다
            if (art.Panel == null) Img(root, "BottomEdge", Alpha(UITheme.GoldAccent, 0.55f)).rectTransform.Bottom(0, 3);
            // ───── 4차 (RM-2 P4-3b): 패배 분석 고리 · 응원 칩 · 불 밝히기 방식 고르기 · 쓰다듬기 · 전투 기록 창 ─────
            var defeatView = BuildDefeatAnalysis(root, art, skillArt);
            var cheerChip = BuildCheerChip(root);
            BuildLampPicker(root, lamp, art);
            BuildPetHits(guardians, callouts, aim);
            BuildStatsPopup(ctx);
            var firefly = BuildStrayFirefly(root, aim, art);
            // 공격 연출 시험장 (개발 빌드: 스테이지 이름표 5번) — 맨 위에 두어 조작 패널이 HUD·배너 위에 뜬다
            var gallery = EnemyFxBuilder.BuildGallery(root, StripHeight, stageLabel, fx, mapper, enemyPool, shop, ringView, guardians, library, ambience, actors);

            new Binder(view)
                .Set("_mapper", mapper)
                .Set("_actorLayer", actors)
                .Set("_shop", shop)
                .Set("_ring", ringView)
                .SetArray("_guardians", guardians)
                .Set("_enemies", enemyPool)
                .Set("_echoes", echoPool)
                .Set("_deck", deck)
                .Set("_lamp", lamp)
                .Set("_hud", hud)
                .Set("_fx", fx)
                .Set("_banner", banner)
                .Set("_library", library)
                .Set("_callouts", callouts)
                .Set("_notice", notice)
                .Set("_synergyBanner", synergyBanner)
                .Set("_synergyHud", synergyHud)
                .Set("_ambience", ambience)
                .Set("_gallery", gallery)
                .Set("_defeatView", defeatView)
                .Set("_cheerChip", cheerChip)
                .Set("_firefly", firefly)
                .Apply();

            // 시트가 열린 만큼 전투를 올리고(카메라 + 바닥 그림) 배너 · 경고 띠를 보이는 띠 안에 둔다 (UX-SPEC-IA §6.3 — UIFrame.SheetProgressChanged만 듣는다)
            var frameDriver = root.gameObject.AddComponent<BattleFrameDriver>();
            new Binder(frameDriver)
                .Set("_mapper", mapper)
                .SetArray("_bandAreas", vignette.rectTransform, (RectTransform)banner.transform, (RectTransform)synergyBanner.transform,
                    (RectTransform)notice.transform)
                // 시트가 다 열려 멈추면 하단 띠 Canvas · GraphicRaycaster를 끈다 (가려진 덱 · 등불이 매 프레임 다시 그리지 않게, CR-18)
                .Set("_stripCanvas", strip.GetComponent<Canvas>())
                .Set("_stripRaycaster", strip.GetComponent<GraphicRaycaster>())
                .Apply();
        }

        // ───────────── 배경 ─────────────

        /// <summary>바닥 그림. 아트면 크기/위치는 BattleFieldMapper가 화면을 덮도록 정하고 그 RectTransform을 돌려준다.</summary>
        static RectTransform BuildBackground(RectTransform root, BattleArt art)
        {
            var frame = NewRect("Background", root).Stretch();
            frame.gameObject.AddComponent<RectMask2D>();
            // 시트가 열리고 닫히는 동안 바닥 그림이 매 프레임 올라가므로 자체 Canvas — 전투 화면 Canvas(HUD)를 다시 묶지 않는다 (UX-SPEC-IA §6.1)
            var backdropCanvas = frame.gameObject.AddComponent<Canvas>();
            backdropCanvas.overrideSorting = false;
            RectTransform backdrop = null;
            if (art.FieldIsArt)
            {
                var image = Img(frame, "FieldArt", Color.white, art.Field);
                // 기준 화면의 전투 영역(1080x1620 = 1920 − 상단 바 130 − 탭 바 170)을 덮는 크기 (런타임에 BattleFieldMapper가 다시 맞춘다)
                float stageHeight = 1920f - UIFrame.TopBarHeight - UIFrame.TabBarHeight;
                float cover = Mathf.Max(1080f / art.Field.rect.width, stageHeight / art.Field.rect.height);
                image.rectTransform.At(Center, Vector2.zero, art.Field.rect.size * cover);
                backdrop = image.rectTransform;
            }
            else
            {
                Img(frame, "FieldFallback", Color.white, art.Field).rectTransform.Stretch();
            }
            Img(root, "EdgeShade", Alpha(UITheme.Background, 0.5f), art.Vignette).rectTransform.Stretch();
            return backdrop;
        }

        // ───────────── 수선점 ─────────────

        /// <summary>수선점: 건물은 액터 레이어(발 높이 정렬), 뒤 후광은 액터 뒤 레이어. 둘 다 ShopView.Layout이 같은 자리로 맞춘다</summary>
        static ShopView BuildShop(RectTransform actors, RectTransform backLayer, BattleFieldMapper mapper, RectTransform groundLight, BattleArt art,
            SkillArtSet skillArt)
        {
            float width = ShopWorldWidth * RefPpu;
            var back = NewRect("ShopBack", backLayer).BattleAt(Center, ShopPivot, RefOrigin, new Vector2(width, width));
            var root = NewRect("Shop", actors).BattleAt(Center, ShopPivot, RefOrigin, new Vector2(width, width));
            var view = root.gameObject.AddComponent<ShopView>();

            var backGlow = Img(back, "BackGlow", Alpha(UITheme.Lamp, 0.3f), art.Glow);
            backGlow.rectTransform.At(Center, new Vector2(0, 10), new Vector2(width * 1.7f, width * 1.5f));

            var building = Img(root, "Building", Color.white, art.Shop);
            building.preserveAspect = true;
            var buildingRect = building.rectTransform;
            buildingRect.anchorMin = Vector2.zero;
            buildingRect.anchorMax = Vector2.one;
            buildingRect.pivot = ShopPivot;
            buildingRect.offsetMin = Vector2.zero;
            buildingRect.offsetMax = Vector2.zero;

            var pulseGlow = Img(root, "PulseGlow", Alpha(UITheme.EchoGlow, 0f), art.Glow);
            pulseGlow.rectTransform.At(Center, new Vector2(0, -10), new Vector2(width * 1.25f, width * 1.25f));
            pulseGlow.enabled = false;

            // 이름표: 어두운 바느질 알약 (34px → 캡슐 배율 ≈ 2.8, 예전 금 밑줄 없이)
            // ART-UPGRADE #17: 이름표 9-slice (ui_nameplate_9s — 없으면 밤하늘 알약 그대로)
            var plate = UiArt.Panel(root, "NamePlate", UiArt.Nameplate9s, UiArt.CapsulePpu(34f), null, Alpha(UITheme.Panel, 0.88f));
            plate.rectTransform.At(BottomCenter, new Vector2(0, 4), new Vector2(112, 34));
            if (!UiArt.IsArt(plate.sprite)) Img(plate.transform, "Line", Alpha(UITheme.GoldAccent, 0.7f)).rectTransform.Bottom(4, 2, 18, 18);
            Txt(plate.transform, "Label", "수선점", 21, UITheme.Cream, TextAlignmentOptions.Center, true).rectTransform.Stretch(0, 0, 0, 2);

            // 수선점 보호막 돔: 건물 크기에 맞춰 늘어나도록 비율 앵커 (가로 1.45배, 바닥 = pivot 높이 0.22)
            var domeSprite = skillArt.Vfx(SkillVfx.ShieldBubble, out bool domeIsArt);
            var dome = Img(root, "ShieldDome", Alpha(domeIsArt ? Color.white : ShieldTint, 0f), domeSprite);
            dome.preserveAspect = true;
            SetAnchors(dome.rectTransform, new Vector2(-0.225f, -0.145f), new Vector2(1.225f, 1.305f));
            dome.enabled = false;
            var shine = Img(dome.transform, "Shine", Alpha(UITheme.Cream, 0f), art.Sparkle);
            shine.rectTransform.At(Center, Vector2.zero, new Vector2(40, 40));
            shine.enabled = false;
            var rim = Img(back, "ShieldRim", Alpha(ShieldTint, 0f), art.Ring);
            SetAnchors(rim.rectTransform, new Vector2(-0.25f, 0.22f - 0.75f * RefSquash), new Vector2(1.25f, 0.22f + 0.75f * RefSquash));
            rim.enabled = false;

            new Binder(view)
                .Set("_mapper", mapper)
                .Set("_root", root)
                .Set("_backRoot", back)
                .Set("_building", buildingRect)
                .Set("_buildingImage", building)
                .Set("_backGlow", backGlow)
                .Set("_pulseGlow", pulseGlow)
                .Set("_groundLight", groundLight)
                .Set("_groundLightImage", groundLight.GetComponent<Image>())
                .Set("_dome", dome)
                .Set("_domeRim", rim)
                .Set("_domeShine", shine)
                .Apply();
            SetColor(view, "_domeTint", domeIsArt ? Color.white : ShieldTint);
            // ATK-08: 금 간 덮개 shop_crack_1..3 (건물 그림과 같은 자리 · 입력 없음 · 처음엔 꺼짐). 그림이 하나도 없으면 만들지 않는다
            var cracks = new List<Object>();
            for (int stage = 1; stage <= 3; stage++)
            {
                var crackSprite = UiArt.ShopCrack(stage);
                if (crackSprite == null) continue;
                var crack = Img(buildingRect, $"Crack{stage}", Color.white, crackSprite);
                crack.preserveAspect = true;
                crack.rectTransform.Stretch();
                crack.enabled = false;
                cracks.Add(crack);
            }
            if (cracks.Count == 3) new Binder(view).SetArray("_crackOverlays", cracks.ToArray()).Apply();
            return view;
        }

        // ───────────── 지킴이 ─────────────

        static GuardianSlotView BuildGuardian(int slot, RectTransform actors, RectTransform pads, Image fan, BattleFieldMapper mapper, BattleArt art,
            SkillArtSet skillArt, SkillVfxLibrary library)
        {
            var color = UITheme.SlotColor(slot);
            var position = RefWorldToField(BattleGeometry.GuardianPosition(slot));

            // 받침대 (빈칸이면 빈 받침대 + 바느질 "+", 누르면 편성 탭으로).
            // 방위 색 받침대 그림(guardian_pad_{색})이 있으면 흰색 그대로 — 회색 받침대에 방위 색을 곱하면 납작한 색 판이 된다
            var slotPad = art.SlotPads != null && slot < art.SlotPads.Length ? art.SlotPads[slot] : null;
            bool padColored = slotPad != null;
            bool padArt = padColored || art.PadIsArt;
            var pad = NewRect($"Pad{slot}", pads).At(Center, position, new Vector2(120, 120));
            var padGlow = Img(pad, "Glow", Alpha(color, 0.5f), art.Ring);
            padGlow.rectTransform.At(Center, new Vector2(0, -3), new Vector2(140, 140 * RefSquash));
            var padImage = Img(pad, "Pad", padColored ? Color.white : color, padColored ? slotPad : art.Pad, true);
            padImage.preserveAspect = true;
            padImage.rectTransform.At(Center, new Vector2(0, padArt ? -8 : -5), padArt ? new Vector2(124, 124) : new Vector2(106, 106));
            var emptyButton = padImage.gameObject.AddComponent<Button>();
            emptyButton.transition = Selectable.Transition.None;
            emptyButton.navigation = new Navigation { mode = Navigation.Mode.None };

            var empty = NewRect("Empty", pad).At(Center, new Vector2(0, 2), new Vector2(50, 50));
            if (ArtIcon(empty, "Plus", UiArt.SlotPlus, Center, Vector2.zero, 46f) == null)
            {
                Img(empty, "Bar", Alpha(UITheme.Cream, 0.85f), RoundSprite).rectTransform.At(Center, Vector2.zero, new Vector2(30, 8));
                Img(empty, "Bar", Alpha(UITheme.Cream, 0.85f), RoundSprite).rectTransform.At(Center, Vector2.zero, new Vector2(8, 30));
            }

            var aimHighlight = Img(pad, "AimHighlight", Alpha(color, 0f), art.Ring);
            aimHighlight.rectTransform.At(Center, new Vector2(0, -3), new Vector2(176, 176 * RefSquash));
            aimHighlight.enabled = false;

            // 지킴이 본체 (발 기준)
            var actor = NewRect($"Guardian{slot}", actors).BattleAt(Center, BottomCenter, position, new Vector2(GuardianSize, GuardianSize + 20));
            var guardian = actor.gameObject.AddComponent<GuardianSlotView>();
            var body = NewRect("Body", actor).BattleAt(BottomCenter, BottomCenter, Vector2.zero, new Vector2(GuardianSize, GuardianSize));
            var halo = Img(body, "Halo", Alpha(color, 0.4f), art.Glow);
            halo.rectTransform.At(Center, new Vector2(0, -4), new Vector2(180, 180));
            var portrait = Img(body, "Portrait", Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.At(BottomCenter, new Vector2(0, -10), new Vector2(GuardianSize, GuardianSize));
            var fallback = BuildToken(body, "Fallback", 88f, new Vector2(0, 10), out var initial);

            // 역할 배지: 원판·테 없이 그림만 (런타임이 지킴이 역할 그림 Icons/style_*로 바꾼다, 기본 = Icons/guardian 방패)
            bool shieldArt = ArtLib.Icon("guardian") != null;
            Image badge;
            if (shieldArt)
            {
                badge = Img(body, "Badge", Color.white, art.Shield);
                badge.preserveAspect = true;
                badge.rectTransform.At(BottomCenter, new Vector2(40, 84), new Vector2(40, 40));
            }
            else
            {
                badge = Img(body, "Badge", color, CircleSprite);
                badge.rectTransform.At(BottomCenter, new Vector2(42, 88), new Vector2(36, 36));
                Img(badge.transform, "Rim", Alpha(UITheme.Cream, 0.9f), art.Ring).rectTransform.Stretch(-6, -6, -6, -6);
                var badgeIcon = Img(badge.transform, "Icon", Color.white, art.Shield);
                badgeIcon.preserveAspect = true;
                badgeIcon.rectTransform.At(Center, Vector2.zero, new Vector2(24, 24));
            }

            // 고유 스킬 보석 (방패 배지와 대칭으로 몸 왼쪽): 어두운 메달 홈 + 스킬 아이콘 + 바느질 쿨타임 고리 + 준비되면 금빛
            var gem = NewRect("SkillGem", body).BattleAt(BottomCenter, Center, new Vector2(-42, 106), new Vector2(38, 38));
            var gemGlow = Img(gem, "Glow", Alpha(UITheme.Gold, 0f), art.Glow);
            gemGlow.rectTransform.At(Center, Vector2.zero, new Vector2(86, 86));
            gemGlow.enabled = false;
            var gemRing = SkillSocket(gem, art, 40f, 52f, 26f, UITheme.Lavender, out var gemIcon);
            gem.gameObject.SetActive(false);

            // 강화(왕관) / 약화(표식) — 머리 위
            var buffSprite = skillArt.Vfx(SkillVfx.BuffCrown, out bool buffIsArt);
            var debuffSprite = skillArt.Vfx(SkillVfx.DebuffMark, out bool debuffIsArt);
            var status = Img(actor, "Status", Color.white, buffSprite);
            status.preserveAspect = true;
            status.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 168), new Vector2(50, 50));
            status.enabled = false;

            new Binder(guardian)
                .Set("_mapper", mapper)
                .SetInt("_slot", slot)
                .Set("_body", body)
                .Set("_portrait", portrait)
                .Set("_fallback", fallback)
                .Set("_fallbackInitial", initial)
                .Set("_halo", halo)
                .Set("_badge", badge)
                .Set("_pad", pad)
                .Set("_padImage", padImage)
                .Set("_padGlow", padGlow)
                .Set("_emptyMark", empty.gameObject)
                .Set("_emptyButton", emptyButton)
                .Set("_aimHighlight", aimHighlight)
                .Set("_fan", fan)
                .Set("_skillGem", gem)
                .Set("_skillIcon", gemIcon)
                .Set("_skillRing", gemRing)
                .Set("_skillGlow", gemGlow)
                .Set("_library", library)
                .Set("_statusIcon", status)
                .Set("_buffSprite", buffSprite)
                .Set("_debuffSprite", debuffSprite)
                .SetIf("_padFilledSprite", slotPad)
                .SetIf("_padEmptySprite", padColored ? art.PadEmpty : null)
                .Apply();
            SetBool(guardian, "_padColored", padColored);
            SetBool(guardian, "_badgeTinted", !shieldArt);
            SetColor(guardian, "_buffTint", buffIsArt ? Color.white : SkillVfxLook.Get(SkillVfx.BuffCrown).Main);
            SetColor(guardian, "_debuffTint", debuffIsArt ? Color.white : SkillVfxLook.Get(SkillVfx.DebuffMark).Main);
            return guardian;
        }

        /// <summary>
        /// 스킬 배지 (지킴이 보석 · 잔향이 배지 공용, 부모 칸 가운데 기준): 어두운 메달 홈(ui_medallion_dark, 흰색 그대로) + 어두운 고리 자리 +
        /// 바느질 고리(ui_ring_stitch, Radial360 쿨타임 — 색은 런타임) + 스킬 아이콘 자리 (그림은 런타임이 SkillVfxLibrary.TryGetSkillIcon으로).
        /// 아트가 없으면 예전 어두운 원판 + 빛 고리. back = 메달 지름, ring = 고리 지름, icon = 아이콘 크기 (메달 안쪽 어두운 홈 ≈ 지름의 68%)
        /// </summary>
        static Image SkillSocket(RectTransform parent, BattleArt art, float back, float ring, float icon, Color ringTint, out Image iconImage)
        {
            var medallion = UiArt.MedallionDark;
            bool medallionArt = UiArt.IsArt(medallion);
            var backImage = Img(parent, "Back", medallionArt ? Color.white : Alpha(UITheme.Panel, 0.94f), medallionArt ? medallion : CircleSprite);
            backImage.preserveAspect = true;
            backImage.rectTransform.At(Center, Vector2.zero, Vector2.one * back);
            var stitch = UiArt.RingStitch;
            var ringSprite = UiArt.IsArt(stitch) ? stitch : art.Ring;
            Img(parent, "RingBack", Alpha(UITheme.Background, 0.7f), ringSprite).rectTransform.At(Center, Vector2.zero, Vector2.one * ring);
            var ringImage = Filled(Img(parent, "Ring", ringTint, ringSprite), Image.FillMethod.Radial360, (int)Image.Origin360.Top, true);
            ringImage.rectTransform.At(Center, Vector2.zero, Vector2.one * ring);
            iconImage = Img(parent, "Icon", Color.white);
            iconImage.preserveAspect = true;
            iconImage.rectTransform.At(Center, Vector2.zero, Vector2.one * icon);
            return ringImage;
        }

        /// <summary>초상화가 없을 때의 대체 토큰: 계보 색 원 + 크림 테 + 이름 첫 글자</summary>
        static Image BuildToken(RectTransform parent, string name, float size, Vector2 bottomPosition, out TextMeshProUGUI initial)
        {
            var token = Img(parent, name, UITheme.Lavender, CircleSprite);
            token.rectTransform.At(BottomCenter, bottomPosition, new Vector2(size, size));
            var rim = Img(token.transform, "Rim", Alpha(UITheme.Cream, 0.9f), CircleSprite);
            rim.rectTransform.Stretch(-4, -4, -4, -4);
            rim.transform.SetAsFirstSibling();
            var inner = Img(token.transform, "Shade", Alpha(Color.black, 0.12f), CircleSprite);
            inner.rectTransform.Stretch(size * 0.1f, size * 0.1f, size * 0.18f, 0);
            initial = Txt(token.transform, "Initial", "?", size * 0.46f, UITheme.Cream, TextAlignmentOptions.Center, true);
            initial.rectTransform.Stretch();
            token.gameObject.SetActive(false);
            return token;
        }

        // ───────────── 적 / 잔향이 템플릿 ─────────────

        static EnemyView BuildEnemyTemplate(RectTransform actors, BattleArt art, SkillArtSet skillArt, Material flashMaterial)
        {
            var root = NewRect("EnemyTemplate", actors).BattleAt(Center, BottomCenter, new Vector2(0, 200), new Vector2(96, 104));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var view = root.gameObject.AddComponent<EnemyView>();

            var shadow = Img(root, "Shadow", Alpha(Color.black, 0.35f), art.Soft);
            shadow.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 2), new Vector2(60, 18));
            // 주변 회복/가속 오라 (태엽쥐 등) — 크기는 auraRadius로 런타임에 맞춘다
            var gimmickRing = Img(root, "GimmickRing", Alpha(UITheme.Positive, 0f), art.Ring);
            gimmickRing.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 2), new Vector2(160, 160 * RefSquash));
            gimmickRing.enabled = false;
            // 감속: 실타래 거미줄 아트가 있으면 발밑에 깔고, 없으면 하늘색 고리
            var yarn = skillArt.Vfx(SkillVfx.SlowYarn, out bool yarnIsArt);
            var slowRing = Img(root, "SlowRing", yarnIsArt ? Color.white : UITheme.SoftBlue, yarnIsArt ? yarn : art.Ring);
            slowRing.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 2), yarnIsArt ? new Vector2(100, 100 * RefSquash) : new Vector2(84, 30));
            slowRing.enabled = false;
            var aura = Img(root, "Aura", Alpha(UITheme.Danger, 0.3f), art.Glow);
            aura.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 70), new Vector2(250, 250));
            aura.gameObject.SetActive(false);
            var enrage = Img(root, "EnrageAura", Alpha(EnrageTint, 0f), art.Glow);
            enrage.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 35), new Vector2(150, 150));
            enrage.enabled = false;

            var body = NewRect("Body", root).BattleAt(BottomCenter, new Vector2(0.5f, 0.06f), Vector2.zero, new Vector2(78, 78));
            var sprite = Img(body, "Sprite", Color.white);
            sprite.preserveAspect = true;
            sprite.rectTransform.Stretch();
            // 피격 흰 번쩍임 (같은 그림을 런타임에 넣는다)
            var flash = AttackFxBuilder.FlashOverlay(body, "Flash", flashMaterial);
            if (flash != null) flash.rectTransform.Stretch();

            // 아트가 없을 때: 잊힘에서 생긴 둥근 그림자 덩어리 (빛나는 눈)
            var blob = Img(body, "Blob", UITheme.Enemy, CircleSprite);
            blob.rectTransform.Stretch(5, 5, 8, 0);
            Img(blob.transform, "Shade", Alpha(Color.black, 0.18f), CircleSprite).rectTransform.Stretch(8, 3, 26, 0);
            Img(blob.transform, "Shine", Alpha(Color.white, 0.18f), CircleSprite).rectTransform.At(TopLeft, new Vector2(12, -8), new Vector2(18, 13));
            foreach (float x in new[] { -12f, 12f })
            {
                var eye = Img(blob.transform, "Eye", UITheme.Cream, CircleSprite);
                eye.rectTransform.At(Center, new Vector2(x, 5), new Vector2(15, 17));
                Img(eye.transform, "Pupil", UITheme.Danger, CircleSprite).rectTransform.At(Center, new Vector2(0, -2), new Vector2(8, 9));
            }

            // 보호막 거품 (골무 껍데기 등) / 지속 피해 불꽃 — 몸 앞
            var bubbleSprite = skillArt.Vfx(SkillVfx.ShieldBubble, out bool bubbleIsArt);
            var shieldBubble = Img(root, "ShieldBubble", Alpha(bubbleIsArt ? Color.white : ShieldTint, 0.35f), bubbleSprite);
            shieldBubble.preserveAspect = true;
            shieldBubble.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 35), new Vector2(105, 105));
            shieldBubble.enabled = false;
            var flames = new Object[2];
            for (int i = 0; i < flames.Length; i++)
            {
                var flame = Img(root, "Flame", i == 0 ? FlameTint : UITheme.Lamp, skillArt.Motif(VfxMotif.Flame));
                flame.rectTransform.BattleAt(BottomCenter, BottomCenter, new Vector2(i == 0 ? -14 : 14, 20), new Vector2(26, 34));
                flame.enabled = false;
                flames[i] = flame;
            }

            // HP 바: 바느질 캡슐 틀 + 흰 캡슐 채움 (색은 런타임) + 흰 잔상
            var hp = Bar(root, "Hp", 10f, UITheme.Negative, 1f, out var fill, out var trail, Alpha(Color.white, 0.75f));
            hp.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 84), new Vector2(62, 10));
            // PRG-02 §13.1: 보스 HP 바의 분노 눈금 2×10 (EnemyView가 보스에만 켠다)
            var enrageTick = Img(hp.transform, "EnrageTick", Alpha(UITheme.Cream, 0.85f));
            enrageTick.rectTransform.anchorMin = enrageTick.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            enrageTick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            enrageTick.rectTransform.sizeDelta = new Vector2(2f, 10f);
            enrageTick.rectTransform.anchoredPosition = Vector2.zero;
            enrageTick.enabled = false;

            // 보스 이름표: 어두운 바느질 알약 (예전 붉은 밑줄 없이)
            // ART-UPGRADE #17: 이름표 9-slice (ui_nameplate_9s — 없으면 밤하늘 알약 그대로)
            var plate = UiArt.Panel(root, "BossPlate", UiArt.Nameplate9s, UiArt.CapsulePpu(36f), null, Alpha(UITheme.Background, 0.88f));
            plate.rectTransform.BattleAt(BottomCenter, BottomCenter, new Vector2(0, 177), new Vector2(220, 36));
            if (!UiArt.IsArt(plate.sprite)) Img(plate.transform, "Edge", Alpha(UITheme.Danger, 0.9f)).rectTransform.Bottom(0, 3, 14, 14);
            var bossName = Txt(plate.transform, "Name", "보스", 22, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis();
            bossName.rectTransform.Stretch(16, 16, 0, 2);
            plate.gameObject.SetActive(false);

            // 보호막 바 (HP 바 위) / 표식 하트 (HP 바 오른쪽)
            var shieldBar = Bar(root, "ShieldBar", 7f, UITheme.SoftBlue, 1f, out var shieldFill, out _);
            shieldBar.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 96), new Vector2(62, 7));
            shieldBar.gameObject.SetActive(false);
            var markSprite = skillArt.Vfx(SkillVfx.LipstickHeart, out bool markIsArt);
            var mark = Img(root, "Mark", markIsArt ? Color.white : SkillVfxLook.Get(SkillVfx.LipstickHeart).Main, markSprite);
            mark.preserveAspect = true;
            mark.rectTransform.BattleAt(BottomCenter, Center, new Vector2(46, 84), new Vector2(28, 28));
            mark.enabled = false;

            // 기절: 머리 위를 도는 별 셋 (금별 그림 Icons/star 흰색 그대로, 없으면 흰 별 모양에 금색)
            var stun = NewRect("Stun", root).BattleAt(BottomCenter, Center, new Vector2(0, 72), new Vector2(64, 64));
            var starArt = ArtLib.Icon("star");
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                var star = Img(stun, "Star", starArt != null ? Color.white : UITheme.Gold, starArt != null ? starArt : skillArt.Motif(VfxMotif.Star));
                star.preserveAspect = true;
                star.rectTransform.At(Center, new Vector2(Mathf.Cos(angle) * 24f, Mathf.Sin(angle) * 9f), new Vector2(20, 20));
            }
            stun.gameObject.SetActive(false);

            new Binder(view)
                .Set("_body", body)
                .Set("_sprite", sprite)
                .SetIf("_flash", flash)
                .Set("_blob", blob)
                .Set("_shadow", shadow)
                .Set("_aura", aura)
                .Set("_group", group)
                .Set("_hpRoot", hp.rectTransform)
                .Set("_enrageTick", enrageTick)
                .Set("_hpFill", fill)
                .Set("_hpTrail", trail)
                .Set("_bossPlate", plate.gameObject)
                .Set("_bossName", bossName)
                .Set("_slowRing", slowRing)
                .Set("_stunOrbit", stun)
                .Set("_shieldRoot", shieldBar.rectTransform)
                .Set("_shieldFill", shieldFill)
                .Set("_shieldBubble", shieldBubble)
                .Set("_markIcon", mark)
                .SetArray("_burnFlames", flames)
                .Set("_enrageAura", enrage)
                .Set("_gimmickRing", gimmickRing)
                .Apply();
            root.gameObject.SetActive(false);
            return view;
        }

        static EchoView BuildEchoTemplate(RectTransform actors, BattleArt art, SkillArtSet skillArt, SkillVfxLibrary library, Material flashMaterial)
        {
            var root = NewRect("EchoTemplate", actors).BattleAt(Center, BottomCenter, new Vector2(0, -200), new Vector2(EchoSize, EchoSize + 30));
            var view = root.gameObject.AddComponent<EchoView>();

            // 발밑 남은 시간 고리 (바닥에 눕도록 세로로 눌림)
            var life = NewRect("LifeRing", root).BattleAt(BottomCenter, Center, new Vector2(0, 2), new Vector2(70, 70));
            life.localScale = new Vector3(1f, RefSquash, 1f);
            var lifeBack = Img(life, "Back", Alpha(UITheme.Background, 0.35f), art.Ring);
            lifeBack.rectTransform.Stretch();
            var lifeFill = Filled(Img(life, "Fill", UITheme.EchoGlow, art.Ring), Image.FillMethod.Radial360, (int)Image.Origin360.Top, true);
            lifeFill.rectTransform.Stretch();

            var aura = Img(root, "Aura", Alpha(UITheme.EchoGlow, 0.5f), art.Glow);
            aura.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 14 + EchoSize * 0.45f), new Vector2(136, 136));

            var body = NewRect("Body", root).BattleAt(BottomCenter, BottomCenter, new Vector2(0, 14), new Vector2(EchoSize, EchoSize));
            var bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
            bodyGroup.blocksRaycasts = false;
            bodyGroup.interactable = false;
            bodyGroup.alpha = 0.75f;
            var flip = NewRect("Flip", body).Stretch();
            var portrait = Img(flip, "Portrait", Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.At(BottomCenter, new Vector2(0, -6), new Vector2(EchoSize, EchoSize));
            // 적에게 맞았을 때 흰 번쩍임 (초상화와 같은 자리·같은 그림)
            var echoFlash = AttackFxBuilder.FlashOverlay(flip, "Flash", flashMaterial);
            if (echoFlash != null) echoFlash.rectTransform.At(BottomCenter, new Vector2(0, -6), new Vector2(EchoSize, EchoSize));
            var fallback = BuildToken(flip, "Fallback", 54f, new Vector2(0, 6), out var initial);

            var hp = Bar(root, "Hp", 7f, UITheme.EchoGlow, 1f, out var hpFill, out _);
            hp.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, EchoSize + 22), new Vector2(48, 7));
            hp.gameObject.SetActive(false);

            // 보호막 거품 (몸 앞) / 강화·약화 아이콘 (머리 위)
            var bubbleSprite = skillArt.Vfx(SkillVfx.ShieldBubble, out bool bubbleIsArt);
            var shieldBubble = Img(root, "ShieldBubble", Alpha(bubbleIsArt ? Color.white : ShieldTint, 0.45f), bubbleSprite);
            shieldBubble.preserveAspect = true;
            shieldBubble.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 14 + EchoSize * 0.45f), new Vector2(118, 118));
            shieldBubble.enabled = false;
            var buffSprite = skillArt.Vfx(SkillVfx.BuffCrown, out bool buffIsArt);
            var debuffSprite = skillArt.Vfx(SkillVfx.DebuffMark, out bool debuffIsArt);
            var status = Img(root, "Status", Color.white, buffSprite);
            status.preserveAspect = true;
            status.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, EchoSize + 40), new Vector2(40, 40));
            status.enabled = false;

            // 고유 스킬 배지 (몸 왼쪽 옆): 어두운 메달 홈 + 스킬 아이콘 + 바느질 쿨타임 고리
            var badge = NewRect("SkillBadge", root).BattleAt(BottomCenter, Center, new Vector2(-44, 60), new Vector2(26, 26));
            var badgeRing = SkillSocket(badge, art, 28f, 36f, 19f, UITheme.Lavender, out var badgeIcon);
            badge.gameObject.SetActive(false);

            new Binder(view)
                .Set("_body", body)
                .Set("_flip", flip)
                .Set("_bodyGroup", bodyGroup)
                .Set("_portrait", portrait)
                .SetIf("_flash", echoFlash)
                .Set("_fallback", fallback)
                .Set("_fallbackInitial", initial)
                .Set("_aura", aura)
                .Set("_lifeRing", life)
                .Set("_lifeBack", lifeBack)
                .Set("_lifeFill", lifeFill)
                .Set("_hpRoot", hp.rectTransform)
                .Set("_hpFill", hpFill)
                .Set("_shieldBubble", shieldBubble)
                .Set("_statusIcon", status)
                .Set("_buffSprite", buffSprite)
                .Set("_debuffSprite", debuffSprite)
                .Set("_skillBadge", badge)
                .Set("_skillIcon", badgeIcon)
                .Set("_skillRing", badgeRing)
                .Set("_library", library)
                .Apply();
            SetColor(view, "_buffTint", buffIsArt ? Color.white : SkillVfxLook.Get(SkillVfx.BuffCrown).Main);
            SetColor(view, "_debuffTint", debuffIsArt ? Color.white : SkillVfxLook.Get(SkillVfx.DebuffMark).Main);
            root.gameObject.SetActive(false);
            return view;
        }

        // ───────────── 연출 / 위험 표시 ─────────────

        /// <summary>떠다니는 반딧불 16마리: 힉스필드 반딧불 그림(FX/fx_mote_firefly, 흰색 — 옅은 색만), 없으면 예전 빛 점 / 반짝임</summary>
        static Image[] BuildFireflies(RectTransform layer, BattleArt art)
        {
            var colors = new[] { UITheme.Lamp, UITheme.Cream, UITheme.Lavender, UITheme.DustyPink };
            var firefly = ArtLib.FX("fx_mote_firefly");
            var fireflies = new Image[16];
            for (int i = 0; i < fireflies.Length; i++)
            {
                if (firefly != null)
                {
                    float bug = 18f + (i * 7 % 5) * 3f;
                    fireflies[i] = Img(layer, "Firefly", Alpha(Color.Lerp(Color.white, colors[i % colors.Length], 0.5f), 0.5f), firefly);
                    fireflies[i].preserveAspect = true;
                    fireflies[i].rectTransform.At(Center, Vector2.zero, new Vector2(bug, bug));
                    continue;
                }
                float size = 10f + (i * 7 % 5) * 3f;
                fireflies[i] = Img(layer, "Firefly", Alpha(colors[i % colors.Length], 0.5f), i % 3 == 0 ? art.Sparkle : art.Glow);
                fireflies[i].rectTransform.At(Center, Vector2.zero, new Vector2(size, size) * (i % 3 == 0 ? 1.4f : 1f));
            }
            return fireflies;
        }

        static BattleFx BuildFx(SceneBuildContext ctx, RectTransform fxLayer, RectTransform groundFx, RectTransform numbers, RectTransform shake,
            BattleFieldMapper mapper, ShopView shop, Image lampRing, Image[] fireflies, BattleArt art, SkillVfxLibrary library, Image fieldFlash)
        {
            var fx = fxLayer.gameObject.AddComponent<BattleFx>();

            var particle = Img(fxLayer, "ParticleTemplate", Color.white, art.Sparkle);
            particle.rectTransform.At(Center, Vector2.zero, new Vector2(20, 20));
            var pillar = Img(fxLayer, "LampPillar", Alpha(UITheme.Cream, 0f), art.Glow);
            pillar.rectTransform.BattleAt(Center, new Vector2(0.5f, 0.12f), RefOrigin, new Vector2(240, 900));

            var number = Txt(numbers, "NumberTemplate", "0", 38, UITheme.Cream, TextAlignmentOptions.Center, true);
            number.rectTransform.At(Center, Vector2.zero, new Vector2(220, 60));

            // 전체 화면 번쩍임 (상단 바/탭까지 덮도록 캔버스 직속, 팝업보다는 아래) — 스테이지 클리어/실패·보스·불 밝히기 같은 드문 순간만.
            // 스킬·기믹 번쩍임은 전장 안 FieldFlash
            var flash = Img(ctx.Canvas, "BattleScreenFlash", Alpha(UITheme.Lamp, 0f));
            flash.rectTransform.Stretch();
            flash.transform.SetSiblingIndex(ctx.PopupRoot.GetSiblingIndex());

            new Binder(fx)
                .Set("_mapper", mapper)
                .Set("_shop", shop)
                .Set("_layer", fxLayer)
                .Set("_groundLayer", groundFx)
                .Set("_library", library)
                .SetInt("_particleCapacity", 440)
                .SetInt("_groundCapacity", 96)
                .SetInt("_numberCapacity", 48)
                .Set("_numberLayer", numbers)
                .Set("_shakeRoot", shake)
                .Set("_particleTemplate", particle)
                .Set("_numberTemplate", number)
                .Set("_sparkle", art.Sparkle)
                .Set("_glow", art.Glow)
                .Set("_trail", art.Trail)
                .Set("_ring", art.Ring)
                .Set("_soft", art.Soft)
                .Set("_puff", art.Puff)
                .Set("_screenFlash", flash)
                .Set("_fieldFlash", fieldFlash)
                .Set("_lampRing", lampRing)
                .Set("_lampPillar", pillar)
                .SetArray("_fireflies", fireflies)
                .Apply();
            return fx;
        }

        static DirectionRingView BuildDangerArrows(RectTransform edge, BattleFieldMapper mapper, Image ring, Image ringHalo, BattleArt art)
        {
            var ringView = edge.gameObject.AddComponent<DirectionRingView>();
            var arrows = new DangerArrowView[BattleGeometry.SlotCount];
            for (int i = 0; i < arrows.Length; i++)
            {
                var position = RefWorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(i), 2.2f));
                var root = NewRect($"Danger{i}", edge).At(Center, position, new Vector2(90, 110));
                var glow = Img(root, "Glow", Alpha(UITheme.Danger, 0f), art.Glow);
                glow.rectTransform.At(Center, new Vector2(0, -8), new Vector2(170, 170));
                glow.enabled = false;
                var chevrons = new Image[3];
                for (int c = 0; c < chevrons.Length; c++)
                {
                    chevrons[c] = Img(root, "Chevron", Alpha(Color.white, 0f), art.Chevron);
                    if (art.ChevronIsArt)
                    {
                        // 바느질 꺾쇠 그림(ui_chevron)은 오른쪽을 향하므로 위(수선점 쪽 = 로컬 +y)로 돌린다. 보이는 폭 ≈ 64, 길이 ≈ 47
                        chevrons[c].preserveAspect = true;
                        chevrons[c].rectTransform.At(Center, new Vector2(0, (c - 1) * 24f), new Vector2(89, 67));
                        chevrons[c].rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                    }
                    else chevrons[c].rectTransform.At(Center, new Vector2(0, (c - 1) * 24f), new Vector2(64, 44));
                    chevrons[c].enabled = false;
                }
                arrows[i] = root.gameObject.AddComponent<DangerArrowView>();
                new Binder(arrows[i])
                    .Set("_mapper", mapper)
                    .SetInt("_slot", i)
                    .Set("_glow", glow)
                    .SetArray("_chevrons", chevrons)
                    .Apply();
            }
            new Binder(ringView)
                .Set("_mapper", mapper)
                .Set("_ring", ring)
                .Set("_ringHalo", ringHalo)
                .SetArray("_arrows", arrows)
                .Apply();
            SetFloat(ringView, "_ringSpriteRadius", BattleArt.RingRadius);
            return ringView;
        }

        // ───────────── HUD ─────────────

        /// <summary>AUTO / 배속 토글의 누르는 칸 높이 (M22 — 76 + 위아래 14, raycastPadding. 잠금 덮개가 그대로 베낀다 · 계약 C-13)</summary>
        const float HudToggleHitHeight = 104f;

        /// <summary>
        /// 위쪽 HUD (Battle 로컬, 위 끝 기준 — 기준 화면 y는 +130, UX-SPEC-IA §3.2):
        ///   StagePanel 330x134 (14,−12): 스테이지 이름 32 굵게 + 진행 줄('보스까지' 20 굵게 Cream@0.8 · 막대 136x18 · '{처치}/{목표}' 22 굵게 Cream 외곽선, M11)
        ///   + 수선점 내구도 254x32 (글자 22 굵게 Cream — 외곽선 재질은 BattleHud.Awake). 파밍 중이면 이름 앞 작은 순환 그림 'FarmIcon' (M13).
        ///   판을 누르면 스테이지 고르기 (BattleHud — 튜토리얼 중 막힘. 진행 톡이 판 크기를 쓰므로 PressScale은 붙이지 않는다).
        ///   AUTO 140x76 (−14,−14) · x1 120x76 (−166,−14) — MetaSceneBuilder.HudAutoButton* / HudSpeedButton* (계약 C-13), 누르는 칸 104 높이 (M22).
        ///   EmptyHint 560x64 (BottomCenter, 0, 296) — 누르는 칸 88 높이.
        /// 경로 Hud/StagePanel · Hud/AutoButton · Hud/SpeedButton은 코치 대상이라 이름 · 부모를 바꾸지 않는다 (잠금 덮개도 이 이름으로 찾는다).
        /// </summary>
        static BattleHud BuildHud(RectTransform root, BattleArt art, Image vignette, out TextMeshProUGUI stageLabel)
        {
            var hudRoot = NewRect("Hud", root).Stretch();
            var hud = hudRoot.gameObject.AddComponent<BattleHud>();

            // 왼쪽 위 기둥: 스테이지 / 보스까지 진행 / 수선점 내구도 (가운데 위는 위쪽 방위 적이 보이도록 비워 둔다)
            // 스테이지 판: 금테 밤하늘 9-slice (덱·잔향 판과 같은 그림, 예전 금 막대 없이) — 누름을 받는다 (스테이지 고르기)
            var stagePanel = Panel(hudRoot, "StagePanel", art, 1.6f, true);
            stagePanel.rectTransform.At(TopLeft, new Vector2(14, -12), new Vector2(330, 134));
            var panel = stagePanel.rectTransform;
            var stagePanelButton = stagePanel.gameObject.AddComponent<Button>();
            stagePanelButton.targetGraphic = stagePanel;
            stagePanelButton.transition = Selectable.Transition.None;
            stagePanelButton.navigation = new Navigation { mode = Navigation.Mode.None };
            if (art.Panel == null) Img(panel, "Accent", Alpha(UITheme.GoldAccent, 0.7f)).rectTransform.At(TopLeft, new Vector2(10, -14), new Vector2(4, 34));
            // 파밍 중 이름 앞 순환 그림 (⟲ 글자는 글꼴에 없다 — Icons/auto 그림 그대로, BattleHud가 켜고 이름을 오른쪽으로 민다)
            var farmIcon = Img(panel, "FarmIcon", Color.white, art.Auto);
            farmIcon.preserveAspect = true;
            farmIcon.rectTransform.At(TopLeft, new Vector2(20, -12), new Vector2(32, 32));
            farmIcon.enabled = false;
            var stageText = Txt(panel, "StageText", "스테이지 1-1", 32, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(24f, 32f);
            stageText.rectTransform.Top(6, 44, 24, 12);
            stageLabel = stageText;

            // 진행 줄 (M11): 이름 '보스까지' (보스 = '보스 HP' Negative, 파밍 = '보스 도전까지 힘을 모으는 중' 한 줄 — BattleHud가 바꾼다)
            var bossLabel = Txt(panel, "BossLabel", "보스까지", UIType.Hint, Alpha(UITheme.Cream, 0.8f), TextAlignmentOptions.MidlineLeft, true)
                .FitFloor(UIType.AutoSizeFloor);
            bossLabel.rectTransform.At(TopLeft, new Vector2(22, -52), new Vector2(86, 28));
            // 막대: 바느질 캡슐 틀 + 흰 캡슐 채움 (색은 BattleHud가 입힌다, 폭은 anchorMax.x — 채움은 틀 안 3px)
            var progressBack = Bar(panel, "ProgressBar", 18f, UITheme.Lavender, 0f, out var progressFill, out _);
            progressBack.rectTransform.At(TopLeft, new Vector2(112, -59), new Vector2(136, 18));
            // PRG-02 §13.1: 분노 눈금 2×22 Cream 0.85 (가로 앵커 = enrageThreshold, BattleHud가 보스 단계에만 켠다)
            var enrageTick = Img(progressBack.transform, "EnrageTick", Alpha(UITheme.Cream, 0.85f));
            enrageTick.rectTransform.anchorMin = enrageTick.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            enrageTick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            enrageTick.rectTransform.sizeDelta = new Vector2(2f, 22f);
            enrageTick.rectTransform.anchoredPosition = Vector2.zero;
            enrageTick.enabled = false;
            new Binder(hud).Set("_enrageTick", enrageTick).Apply();
            // 숫자: 판 위 맨글자라 공유 외곽선 (M81)
            var progressText = Txt(panel, "ProgressText", "0/20", 22, UITheme.Cream, TextAlignmentOptions.MidlineRight, true).Outlined();
            progressText.rectTransform.At(TopRight, new Vector2(-14, -51), new Vector2(68, 30));

            var shopIcon = Img(panel, "ShopIcon", Color.white, art.Shop);
            shopIcon.preserveAspect = true;
            shopIcon.rectTransform.At(TopLeft, new Vector2(10, -82), new Vector2(48, 48));
            var hpBack = Bar(panel, "ShopHpBar", 32f, UITheme.Positive, 1f, out var hpFill, out var hpTrail, Alpha(Color.white, 0.7f));
            hpBack.rectTransform.At(TopLeft, new Vector2(62, -92), new Vector2(254, 32));
            // 내구도 글자 받침 (M11 · 검증 w3 r1 — 초록 · 금 · 빨강 채움 위 Cream 글자가 ≈ 1.7:1이었다): 어두운 바느질 알약 α0.72 —
            // 반투명이라 채움 끝이 받침 밑으로도 비친다(초록 위 ≈ 6.5:1). 폭은 BattleHud가 글자 폭 + 28로 맞춘다 (자릿수가 바뀔 때만). 보호막 띠는 그 위
            var hpPlate = Pill(hpBack.transform, "TextPlate", 24f, Alpha(UITheme.Background, 0.72f));
            if (UiArt.IsArt(hpPlate.sprite)) hpPlate.color = Alpha(Color.white, 0.72f);
            hpPlate.rectTransform.At(Center, Vector2.zero, new Vector2(136, 24));
            // 수선점 보호막: HP 바 아래쪽에 겹치는 하늘색 캡슐 띠 (예전 렌즈 모양 Filled 대신 앵커 폭)
            float hpInset = hpFill.rectTransform.offsetMin.y;
            var shieldFill = UiArt.Panel(hpBack.transform, "Shield", UiArt.BarFill9s, UiArt.CapsulePpu(7f), Alpha(UITheme.SoftBlue, 0.9f), Alpha(UITheme.SoftBlue, 0.9f));
            var shieldRect = shieldFill.rectTransform;
            shieldRect.anchorMin = Vector2.zero;
            shieldRect.anchorMax = new Vector2(0f, 0f);
            shieldRect.pivot = new Vector2(0f, 0f);
            shieldRect.offsetMin = new Vector2(hpInset, hpInset);
            shieldRect.offsetMax = new Vector2(-hpInset, hpInset + 7f);
            shieldFill.enabled = false;
            // 내구도 글자 22 굵게 Cream (M11). 런타임 외곽선 재질(두께 0.3 · #21123D · 팽창 0.1)은 BattleHud.Awake가 입힌다 — 빌드 때는 공유 외곽선
            var hpText = Txt(hpBack.transform, "Text", "100 / 100", 22, UITheme.Cream, TextAlignmentOptions.Center, true);
            hpText.rectTransform.Stretch();
            UiArt.Outline(hpText);

            // 편성이 비었을 때 안내 (누르면 편성 탭): 보라 단추 9-slice (예전 금 밑줄 없이)
            var emptyHint = Img(hudRoot, "EmptyHint", Alpha(UITheme.Panel, 0.94f), RoundSprite, true);
            // 링을 비켜 아래 띠에 (UX-SPEC-IA §3.4 At(BottomCenter, (0, 296), (560, 64)) — 기준 화면 260~820 x 1390~1454).
            // 피벗은 가운데로 둬서 PressScale이 예전처럼 가운데로 줄어든다 (같은 사각형). 시트가 열리면 사라진다 (올라간 전투의 길을 가리지 않게, §1.6)
            emptyHint.rectTransform.BattleAt(BottomCenter, Center, new Vector2(0, 296 + 32), new Vector2(560, 64));
            SheetFade.Attach(emptyHint.gameObject, 0f);
            var emptyButton = emptyHint.gameObject.AddComponent<Button>();
            emptyButton.targetGraphic = emptyHint;
            emptyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            emptyHint.gameObject.AddComponent<PressScale>();
            UiArt.SetButtonArt(emptyButton, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(64f));
            // 누르는 칸 88 높이 (M22 — 위아래 12)
            emptyHint.HitPad(new Vector2(560, 64), 0f, UISize.MinHit);
            if (!UiArt.IsArt(emptyHint.sprite)) Img(emptyHint.transform, "Edge", Alpha(UITheme.GoldAccent, 0.7f)).rectTransform.Bottom(6, 2, 40, 40);
            Txt(emptyHint.transform, "Text", "편성 탭에서 지킴이를 배치해 주세요", 26, UITheme.Cream, TextAlignmentOptions.Center, true)
                .rectTransform.Stretch(16, 16, 0, 4);

            // AUTO / 배속: 보라 단추 9-slice (켜짐 흰색 · 꺼짐 옅은 회보라 — BattleHud가 바꾼다). 자리 · 크기는 계약 C-13 상수 (잠금 덮개와 같은 값)
            var auto = ToggleButton(hudRoot, "AutoButton", "AUTO", art.Auto, MetaSceneBuilder.HudAutoButtonPos, MetaSceneBuilder.HudAutoButtonSize,
                out var autoBg, out var autoLabel, out var autoIcon);
            var speed = ToggleButton(hudRoot, "SpeedButton", "x1", art.Speed, MetaSceneBuilder.HudSpeedButtonPos, MetaSceneBuilder.HudSpeedButtonSize,
                out var speedBg, out var speedLabel, out var speedIcon);
            // 자리표시 화살촉은 위를 보므로 빨리 감기처럼 오른쪽으로 눕힌다
            if (!art.SpeedIsArt) speedIcon.localRotation = Quaternion.Euler(0, 0, -90);
            bool toggleArt = UiArt.IsArt(autoBg.sprite);

            new Binder(hud)
                .Set("_stageText", stageText)
                .Set("_progressFill", progressFill)
                .Set("_progressText", progressText)
                .Set("_stagePanel", stagePanel.rectTransform)
                .Set("_bossLabel", bossLabel)
                .Set("_progressBar", progressBack.rectTransform)
                .Set("_farmIcon", farmIcon)
                .Set("_stagePanelButton", stagePanelButton)
                .Set("_shopHpRoot", hpBack.rectTransform)
                .Set("_shopHpFill", hpFill)
                .Set("_shopHpTrail", hpTrail)
                .Set("_shopHpText", hpText)
                .Set("_shopHpPlate", hpPlate.rectTransform)
                .Set("_shopShieldFill", shieldFill)
                .Set("_autoButton", auto)
                .Set("_autoBackground", autoBg)
                .Set("_autoLabel", autoLabel)
                .Set("_autoIcon", autoIcon)
                .Set("_speedButton", speed)
                .Set("_speedBackground", speedBg)
                .Set("_speedLabel", speedLabel)
                .Set("_dangerVignette", vignette)
                .Set("_emptyHint", emptyButton)
                .Apply();
            if (toggleArt)
            {
                SetColor(hud, "_toggleOnTint", Color.white);
                SetColor(hud, "_toggleOffTint", new Color(0.62f, 0.6f, 0.75f, 1f));
            }
            return hud;
        }

        static Button ToggleButton(RectTransform parent, string name, string label, Sprite icon, Vector2 position, Vector2 size,
            out Image background, out TextMeshProUGUI labelText, out RectTransform iconRect)
        {
            var button = Btn(parent, name, label, UITheme.Secondary, 28, out labelText);
            var rt = (RectTransform)button.transform;
            rt.At(TopRight, position, size);
            // 보라 단추 9-slice (76px 높이 → 배율 2.8), 상태는 BattleHud가 흰색 / 회보라로 바꾼다
            UiArt.SetButtonArt(button, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(size.y));
            background = (Image)button.targetGraphic;
            // 누르는 칸 104 높이 (M22 — 위아래 14, 위로는 StageArea 위 끝까지). 잠금 덮개(MetaSceneBuilder.BuildLockCover)가 이 raycastPadding을 그대로 베낀다 (C-13)
            background.HitPad(size, 0f, HudToggleHitHeight);
            // 자기 소리(AutoToggle · SpeedToggle)는 BattleHud가 낸다 — 누름 소리는 끈다 (겹치지 않게)
            if (button.TryGetComponent<PressScale>(out var press)) press.TapSound = false;
            labelText.rectTransform.Stretch(50, 8, 0, 0);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.FitFloor(22f, 28f);
            var iconImage = Img(rt, "Icon", Color.white, icon);
            iconImage.preserveAspect = true;
            iconImage.rectTransform.At(MiddleLeft, new Vector2(10, 0), new Vector2(40, 40));
            iconRect = iconImage.rectTransform;
            iconRect.pivot = Center;
            iconRect.anchoredPosition = new Vector2(30, 0);
            return button;
        }

        /// <summary>
        /// 보스 도전 버튼 'BossChallengeButton' (M13, UX-SPEC §2.1.5 · IA §3.2): TopLeft(16,−226) 230x68 — 기준 화면 16~246 x 356~424,
        /// 시너지 줄 아래 · 리본 자리(282~) 왼쪽. 루트는 고정 자리 + SheetFade 0.5 (시트에서 반투명, 그래도 누를 수 있다 — IA §1.6),
        /// 'Body'(금 단추 9-slice, Button + PressScale, 자체 CanvasGroup — BattleHud가 x−40에서 미끄러져 들어오게 한다)에
        /// 'Title' '보스 도전 1-10' 24 굵게 Ink · 'Recommend' '권장 12,345 ›' 18 굵게 (Positive/NegativeInk, 누르면 강해지기 창) ·
        /// 'LoopToggle' 44px 반복 단추 (자동 보스 도전, 켜지면 24px 순환 배지). 둘레 'Glow'는 도전해 볼 만할 때 숨쉰다.
        /// 몸통 누르는 칸 88 높이 (위아래 10). 파밍 중에만 켜진다 (BattleHud). 이름 BossChallengeButton은 AutoShot이 찾는다.
        /// 시너지 줄 뒤에 만들어 겹치는 누름 띠에서는 이쪽이 먼저 받는다.
        /// </summary>
        static void BuildBossChallenge(BattleHud hud, BattleArt art)
        {
            var hudRoot = (RectTransform)hud.transform;
            var root = NewRect("BossChallengeButton", hudRoot).At(TopLeft, new Vector2(16, -226), new Vector2(230, 68));
            SheetFade.Attach(root.gameObject, 0.5f);

            var glow = Img(root, "Glow", Alpha(UITheme.Gold, 0f), art.Glow);
            glow.rectTransform.Stretch(-30, -30, -26, -26);
            glow.enabled = false;

            var bodyImage = Img(root, "Body", UITheme.Gold, RoundSprite, true);
            var body = bodyImage.rectTransform.Stretch();
            var bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = bodyImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UiArt.SetButtonArt(button, UiArt.ButtonGold9s, UiArt.ButtonPpu(68f));
            // 보스 도전 소리(BossChallenge)는 BattleHud가 낸다 — 누름 소리는 끈다
            body.gameObject.AddComponent<PressScale>().TapSound = false;
            bodyImage.HitPad(new Vector2(230, 68), 0f, UISize.MinHit);

            var title = Txt(body, "Title", "보스 도전 1-10", UIType.Label, UITheme.Ink, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.Hint);
            title.rectTransform.Stretch(16, 62, 5, 31);
            // 권장 줄: 누르면 강해지기 창 (C-12) — 금 바탕 위라 Ink 계열 (모자라면 NegativeInk, 넉넉하면 PositiveInk — BattleHud)
            var recommend = Txt(body, "Recommend", "권장 0 ›", UIType.Micro, UITheme.PositiveInk, TextAlignmentOptions.MidlineLeft, true)
                .FitFloor(UIType.AutoSizeFloor);
            recommend.rectTransform.Stretch(16, 62, 35, 5);
            recommend.raycastTarget = true;
            var recommendButton = recommend.gameObject.AddComponent<Button>();
            recommendButton.targetGraphic = recommend;
            recommendButton.transition = Selectable.Transition.None;
            recommendButton.navigation = new Navigation { mode = Navigation.Mode.None };
            // 누르는 칸: 왼쪽 8 · 아래로 몸통 누르는 칸 끝(15)까지 · 위 4 (제목 줄은 보스 도전 몫) → 160x47 — 몸통 안의 보조 링크
            recommend.raycastPadding = new Vector4(-8f, -15f, 0f, -4f);

            // 반복 단추 44px (자동 보스 도전 켜고 끄기): 금 단추 위 어두운 원 + 순환 그림, 켜지면 오른쪽 위 24px 순환 배지
            var loop = Img(body, "LoopToggle", Alpha(UITheme.Panel, 0.92f), CircleSprite, true);
            loop.rectTransform.At(MiddleRight, new Vector2(-10, 0), new Vector2(44, 44));
            var loopButton = loop.gameObject.AddComponent<Button>();
            loopButton.targetGraphic = loop;
            loopButton.navigation = new Navigation { mode = Navigation.Mode.None };
            loop.gameObject.AddComponent<PressScale>().TapSound = false;
            // 누르는 칸 58x88 (권장 · 제목 줄과는 4px, 오른쪽은 몸통 가장자리까지 10px, 위아래는 몸통 누르는 칸 끝까지 22px)
            loop.HitPad(new Vector2(44, 44), 64f, UISize.MinHit, 4f, 10f, 22f, 22f);
            var loopIcon = Img(loop.transform, "Icon", UITheme.Cream, art.Auto);
            loopIcon.preserveAspect = true;
            loopIcon.rectTransform.At(Center, Vector2.zero, new Vector2(30, 30));
            var badge = Img(loop.transform, "Badge", Alpha(UITheme.Background, 0.92f), CircleSprite);
            badge.rectTransform.BattleAt(TopRight, Center, new Vector2(-2, -2), new Vector2(24, 24));
            Img(badge.transform, "Ring", UITheme.Lavender, CircleSprite).rectTransform.Stretch(-2, -2, -2, -2).SetAsFirstSibling();
            var badgeIcon = Img(badge.transform, "Loop", Color.white, art.Auto);
            badgeIcon.preserveAspect = true;
            badgeIcon.rectTransform.Stretch(3, 3, 3, 3);
            badge.gameObject.SetActive(false);

            new Binder(hud)
                .Set("_bossButtonRoot", root)
                .Set("_bossBody", body)
                .Set("_bossBodyGroup", bodyGroup)
                .Set("_bossButton", button)
                .Set("_bossTitle", title)
                .Set("_bossRecommend", recommend)
                .Set("_bossRecommendButton", recommendButton)
                .Set("_bossLoopButton", loopButton)
                .Set("_bossLoopIcon", loopIcon)
                .Set("_bossLoopBadge", badge.gameObject)
                .Set("_bossGlow", glow)
                .Apply();
        }

        /// <summary>
        /// 스테이지 고르기 'StageList' (M13 P2): 스테이지 판을 누르면 HUD 아래 가운데 600x222 판(TopCenter(0,−150) — 기준 화면 x 240~840 · y 280~502,
        /// 왼쪽 빠른 아이콘 · 오른쪽 바구니 · 권장 전투력 알약과 비켜 있다)에 최근 스테이지 10개(최고 스테이지부터, 5x2 칸 104x80 — 지금 스테이지는 금빛).
        /// 뒤의 투명한 'Blocker'(HUD 전체)를 누르면 닫힌다. 맨 마지막 HUD 자식이라 시너지 줄 · 보스 도전 버튼 위에 그려진다. 처음엔 꺼 둔다.
        /// </summary>
        static void BuildStageList(BattleHud hud, BattleArt art)
        {
            var hudRoot = (RectTransform)hud.transform;
            var root = NewRect("StageList", hudRoot).Stretch();
            var blocker = Img(root, "Blocker", Color.clear, null, true);
            blocker.rectTransform.Stretch();
            var blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = blocker;
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.navigation = new Navigation { mode = Navigation.Mode.None };

            var panel = Panel(root, "Panel", art, 1.6f, true);
            if (art.Panel == null) panel.color = Alpha(UITheme.Panel, 0.96f);
            panel.rectTransform.At(TopCenter, new Vector2(0, -150), new Vector2(600, 222));
            Txt(panel.transform, "Title", "다시 가 볼 스테이지", UIType.Hint, UITheme.SubText, TextAlignmentOptions.Center, true)
                .rectTransform.Top(10, 30, 20, 20);

            const int count = 10;
            const int columns = 5;
            var chips = new Object[count];
            var labels = new Object[count];
            var backs = new Object[count];
            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int line = i / columns;
                var back = Img(panel.transform, $"Stage{i}", Alpha(UITheme.PanelLight, 0.95f), RoundSprite, true);
                back.rectTransform.At(TopLeft, new Vector2(24 + column * 112, -(46 + line * 88)), new Vector2(104, 80));
                var chip = back.gameObject.AddComponent<Button>();
                chip.targetGraphic = back;
                chip.navigation = new Navigation { mode = Navigation.Mode.None };
                back.gameObject.AddComponent<PressScale>();
                // 누르는 칸 104x88 (칸 사이 8의 절반씩 위아래로)
                back.HitPad(new Vector2(104, 80), 0f, UISize.MinHit, 4f, 4f, 4f, 4f);
                var label = Txt(back.transform, "Label", "1-1", UIType.Body, UITheme.Cream, TextAlignmentOptions.Center, true);
                label.rectTransform.Stretch(4, 4, 0, 2);
                chips[i] = chip;
                labels[i] = label;
                backs[i] = back;
            }
            root.gameObject.SetActive(false);

            new Binder(hud)
                .Set("_stageList", root.gameObject)
                .Set("_stageListBlocker", blockerButton)
                .SetArray("_stageChips", chips)
                .SetArray("_stageChipLabels", labels)
                .SetArray("_stageChipBacks", backs)
                .Apply();
        }

        // ───────────── 배너 ─────────────

        static BattleBannerView BuildBanner(RectTransform root, BattleArt art)
        {
            var area = NewRect("BannerArea", root).Stretch(0, 0, 0, StripHeight);
            var view = area.gameObject.AddComponent<BattleBannerView>();

            var banner = NewRect("Banner", area).At(Center, Vector2.zero, new Vector2(860, 170));
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // 빛살: 힉스필드 흰 빛살 그림 (FX/rays, 색·회전은 BattleBannerView)
            var rays = Img(banner, "Rays", Alpha(UITheme.Lamp, 0f), art.Rays);
            rays.preserveAspect = true;
            rays.rectTransform.At(Center, Vector2.zero, new Vector2(700, 700));
            var glow = Img(banner, "Glow", Alpha(UITheme.Gold, 0f), art.Glow);
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(1100, 420));
            // 리본: 밤 리본 9-slice (ui_ribbon_night_9s, 테두리 L/R 200 · T/B 48 → 138px 높이에 배율 1.4) — 없으면 금테 밤하늘 판
            var ribbonArt = UiArt.RibbonNight9s;
            bool ribbonIsArt = UiArt.IsArt(ribbonArt) || art.Panel != null;
            Image ribbon;
            if (UiArt.IsArt(ribbonArt)) ribbon = UiArt.Panel(banner, "Ribbon", ribbonArt, 1.4f);
            else if (art.Panel != null) ribbon = Panel(banner, "Ribbon", art, 1.4f);
            else ribbon = Img(banner, "Ribbon", UITheme.Panel, RoundSprite);
            ribbon.rectTransform.Stretch(0, 0, 16, 16);
            // 예전 금 밑줄 (아트 리본은 자체 금실이 있어 감춘다 — BattleBannerView는 색만 바꾼다)
            var edgeLine = Img(banner, "RibbonEdge", UITheme.Gold);
            edgeLine.rectTransform.Bottom(22, 3, 120, 120);
            edgeLine.enabled = !ribbonIsArt;
            // 제목은 리본 꼬리 · 접힘 · 금별 장식 밖, 평평한 가운데 띠 안 (좌우 TitleSideInset 100 → 칸 660, 자동 크기 하한 PopupTitle 40 — 검증 rm2 r1:
            // 예전 좌우 30이면 긴 실패 문구의 '!'가 오른쪽 접힘 · 별에 걸리고 왼쪽 끝이 꼬리에 닿았다). BattleBannerView도 깨어날 때 같은 여백을 지킨다
            var title = Txt(banner, "Title", "스테이지 클리어!", 58, UITheme.Cream, TextAlignmentOptions.Center, true);
            title.rectTransform.Stretch(BattleBannerView.TitleSideInset, BattleBannerView.TitleSideInset, 18, 56);
            title.enableAutoSizing = true;
            title.fontSizeMin = BattleBannerView.TitleMinSize;
            title.fontSizeMax = 58;
            // 부제 받침 (검증 w3 r1 — 부제 칸 y 20~64가 리본 아래 금실(y 44~50) · 바느질 선에 걸쳐 선이 글자를 가로질렀다): 보상 줄과 같은
            // 어두운 바느질 알약을 리본 아래 끝 바로 밑에 매단다 (피벗 위 가운데, 위 끝 = 배너 아래에서 SubtitlePlateTop 40). 부제가 있을 때만 켜고
            // 폭(글 폭 + 48, 최대 828) · 높이(가운데 44 · 작은 리본 54)는 BattleBannerView가 맞춘다. 배너 띠(keep-out)에 들어간다
            var subtitlePlate = Pill(banner, "SubtitlePlate", BattleBannerView.SubtitlePlateHeight, Alpha(UITheme.Background, 0.78f));
            subtitlePlate.rectTransform.At(BottomCenter, new Vector2(0, BattleBannerView.SubtitlePlateTop), new Vector2(620, BattleBannerView.SubtitlePlateHeight));
            subtitlePlate.rectTransform.pivot = TopCenter;
            subtitlePlate.gameObject.SetActive(false);
            // 부제 (보상 한 줄 · 보스에 막힘 '강해지는 방법 보기 ›'): 받침 위 Cream 굵게 + 공유 외곽선, 받침과 같은 위 끝에 매달려 가운데가 맞는다 (칸 780 —
            // FitRibbonSubtitle이 이 폭에 맞춘다). 작은 리본이면 BattleBannerView가 37px(화면 약 22px)로 키운다.
            // 누를 수 있는 배너 동안만 BattleBannerView가 raycastTarget을 켠다 (누르는 칸 88 높이)
            var subtitle = Txt(banner, "Subtitle", string.Empty, 24, UITheme.Cream, TextAlignmentOptions.Center, true).Outlined();
            subtitle.rectTransform.At(BottomCenter, new Vector2(0, BattleBannerView.SubtitlePlateTop), new Vector2(780, BattleBannerView.SubtitlePlateHeight));
            subtitle.rectTransform.pivot = TopCenter;
            subtitle.HitPad(new Vector2(780, BattleBannerView.SubtitlePlateHeight), 0f, UISize.MinHit);
            var subtitleButton = subtitle.gameObject.AddComponent<Button>();
            subtitleButton.targetGraphic = subtitle;
            subtitleButton.transition = Selectable.Transition.None;
            subtitleButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // 보상 줄: 어두운 바느질 알약 + 보상 아이콘(36px, 재화·재료 그림) + 글자, 바탕째 페이드/팝
            const int lineCount = 4;
            var lines = new Object[lineCount];
            var lineGroups = new Object[lineCount];
            var lineIcons = new Object[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                var pill = Pill(banner, $"RewardLine{i}", 44f, Alpha(UITheme.Background, 0.78f));
                pill.rectTransform.At(TopCenter, new Vector2(0, -184 - i * 50), new Vector2(440, 44));
                var lineGroup = pill.gameObject.AddComponent<CanvasGroup>();
                lineGroup.blocksRaycasts = false;
                lineGroup.interactable = false;
                var lineIcon = Img(pill.transform, "Icon", Color.white, ArtLib.Currency(CurrencyType.Gold));
                lineIcon.preserveAspect = true;
                lineIcon.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(34, 0), new Vector2(36, 36));
                lineIcon.enabled = false;
                var text = Txt(pill.transform, "Text", "+ 단추 100", 26, UITheme.Gold, TextAlignmentOptions.Center, true);
                text.rectTransform.Stretch(58, 22, 0, 0);
                lines[i] = text;
                lineGroups[i] = lineGroup;
                lineIcons[i] = lineIcon;
                pill.gameObject.SetActive(false);
            }
            // 보상 아이콘: CurrencyType 순서의 재화 그림 + 재료 그림이 없을 때의 기본 보상 아이콘
            var currencyValues = (CurrencyType[])Enum.GetValues(typeof(CurrencyType));
            int currencyCount = 0;
            foreach (var value in currencyValues) currencyCount = Mathf.Max(currencyCount, (int)value + 1);
            var currencyIcons = new Object[currencyCount];
            foreach (var value in currencyValues) currencyIcons[(int)value] = ArtLib.Currency(value);

            new Binder(view)
                .Set("_banner", banner)
                .Set("_group", group)
                .Set("_ribbon", ribbon)
                .Set("_ribbonEdge", edgeLine)
                .Set("_glow", glow)
                .Set("_rays", rays)
                .Set("_title", title)
                .Set("_subtitle", subtitle)
                .Set("_subtitlePlate", subtitlePlate)
                .Set("_subtitleButton", subtitleButton)
                .SetArray("_lines", lines)
                .SetArray("_lineGroups", lineGroups)
                .SetArray("_lineIcons", lineIcons)
                .SetArray("_currencyIcons", currencyIcons)
                .SetIf("_rewardIcon", ArtLib.Icon("reward_generic"))
                .Apply();
            SetBool(view, "_ribbonIsArt", ribbonIsArt);
            return view;
        }

        // ───────────── 하단 띠 (잔향 / 덱 / 불 밝히기) ─────────────

        /// <summary>등불 단추 게이지 창 어둡기 (M37 — Cream 이름 · Lamp % 대비, 금 단추 테두리는 그대로 남는다)</summary>
        const float LampInnerAlpha = 0.68f;

        static RectTransform BuildStrip(RectTransform root, BattleArt art, BattleHud hud, out RectTransform deckPanel, out LampButtonView lamp)
        {
            // 아래 띠 바탕: 금테 밤하늘 9-slice (누름을 받아 전장으로 새지 않게)
            RectTransform strip;
            if (art.Panel != null) strip = Panel(root, "Strip", art, 1.6f, true).rectTransform.Bottom(0, StripHeight);
            else
            {
                strip = Img(root, "Strip", Alpha(UITheme.Background, 0.72f), null, true).rectTransform.Bottom(0, StripHeight);
                Img(strip, "TopLine", Alpha(UITheme.GoldAccent, 0.35f)).rectTransform.Top(0, 2);
            }
            // 자체 Canvas + 누름 판정: 덱 쿨다운 원 · 등불 게이지가 매 프레임 다시 그려도 전투 캔버스 전체를 다시 묶지 않고,
            // 시트가 다 열려 멈춰 있으면 BattleFrameDriver가 이 Canvas를 끈다 (UX-SPEC-IA §4.5 · CR-18). 코치 경로(Strip/…)는 그대로
            strip.gameObject.AddComponent<Canvas>();
            strip.gameObject.AddComponent<GraphicRaycaster>();

            BuildResonancePanel(strip, art, hud);

            var deck = Panel(strip, "DeckPanel", art);
            deck.rectTransform.At(BottomLeft, new Vector2(198, 10), new Vector2(634, 136));
            deckPanel = deck.rectTransform;
            // 덱 이름표 (M36): 어두운 바느질 알약 440x32 '탭 = 자동 소환 · 끌기 = 방향 지정' 20 굵게 Cream → 익숙해지면 160x32 '잔향이 N/M' 18 SubText
            // (EchoDeckView). 띠 위로 18px 솟아 가이드 퀘스트 줄 아래 끝에 닿는다. 누르는 칸 160x48 (아래 3 · 위 13 — 잔향이 N/M일 때만 편성 탭):
            // 아래로는 덱 카드 누르는 칸 위 끝(띠 y 139 = 10 + 7 + 122)에서 멈춘다 — 더 내려가면 뒤 형제인 이 알약이 카드 윗줄 누름을 가로챈다.
            // 위로 넘친 13px 중 가이드 퀘스트 줄과 겹치는 곳은 위에 그려지는 그쪽(MetaHud)이 받는다
            var tab = Pill(strip, "DeckTab", 32f, Alpha(UITheme.Panel, 0.86f), true);
            tab.rectTransform.At(BottomLeft, new Vector2(212, 142), new Vector2(440, 32));
            tab.raycastPadding = new Vector4(0f, -3f, 0f, -13f);
            tab.gameObject.AddComponent<CanvasGroup>();
            var tabButton = tab.gameObject.AddComponent<Button>();
            tabButton.targetGraphic = tab;
            tabButton.transition = Selectable.Transition.None;
            tabButton.navigation = new Navigation { mode = Navigation.Mode.None };
            Txt(tab.transform, "Label", "탭 = 자동 소환 · 끌기 = 방향 지정", UIType.Hint, UITheme.Cream, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor).rectTransform.Stretch(12, 12, 0, 0);

            lamp = BuildLampButton(strip, art);
            return strip;
        }

        /// <summary>잔향 판 180x136 (M36): 제목 없이 잔향 그림 64 + 숫자 52 Cream을 가운데(y +6)에 나란히, 아래 막대는 그대로</summary>
        static void BuildResonancePanel(RectTransform strip, BattleArt art, BattleHud hud)
        {
            var panel = Panel(strip, "ResonancePanel", art);
            panel.rectTransform.At(BottomLeft, new Vector2(10, 10), new Vector2(180, 136));
            var rt = panel.rectTransform;

            var glow = Img(rt, "IconGlow", Alpha(UITheme.Resonance, 0.4f), art.Glow);
            glow.rectTransform.BattleAt(Center, Center, new Vector2(-46, 6), new Vector2(108, 108));
            var icon = Img(rt, "Icon", art.ResonanceIsArt ? Color.white : UITheme.Resonance, art.Resonance);
            icon.preserveAspect = true;
            icon.rectTransform.BattleAt(Center, Center, new Vector2(-46, 6), new Vector2(64, 64));
            var count = Txt(rt, "Count", "10", 52, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(30f, 52f);
            count.rectTransform.BattleAt(Center, MiddleLeft, new Vector2(-10, 6), new Vector2(94, 60));

            var barBack = Bar(rt, "Bar", 14f, UITheme.Resonance, 0.1f, out var fill, out _);
            barBack.rectTransform.Bottom(14, 14, 18, 18);

            var floatText = Txt(rt, "Delta", "+1", 28, UITheme.Resonance, TextAlignmentOptions.Center, true);
            floatText.rectTransform.At(MiddleRight, new Vector2(-6, 30), new Vector2(70, 40));

            new Binder(hud)
                .Set("_resonanceText", count)
                .Set("_resonanceFill", fill)
                .Set("_resonanceIcon", icon.rectTransform)
                .Set("_resonanceGlow", glow)
                .Set("_resonanceFloat", floatText)
                .Apply();
        }

        /// <summary>
        /// 수선점 불 밝히기 단추 232x136 'LampButton' (코치 대상 Strip/LampButton, M37): 금 단추 그림 + 어두운 게이지 창(Background α0.68) +
        /// 아래에서 차오르는 게이지 + 등불 고리 MiddleLeft(12,12) · 등불 그림 4px 위. 이름 'Label' MiddleRight(−14,4) 110x104 (줄 간격 −10, 22~28 자동, Cream),
        /// '%' 'Percent' BottomLeft(12,10) 98x26 — 고리 밑 안쪽 창 안, 받침 알약 'PercentPlate' 위 (24 굵게 Lamp, 16~24 자동, 공유 외곽선 — M81). 누르면 바탕이 살짝 눌린다 (PressScale — 단추 맥박은 Visual이 따로).
        /// </summary>
        static LampButtonView BuildLampButton(RectTransform strip, BattleArt art)
        {
            var root = NewRect("LampButton", strip).At(BottomRight, new Vector2(-10, 10), new Vector2(232, 136));
            var view = root.gameObject.AddComponent<LampButtonView>();

            var outerGlow = Img(root, "OuterGlow", Alpha(UITheme.Lamp, 0f), art.Glow);
            outerGlow.rectTransform.At(Center, Vector2.zero, new Vector2(360, 270));
            outerGlow.enabled = false;

            var visual = NewRect("Visual", root).Stretch();
            Image background;
            if (art.GoldButton != null)
            {
                background = Img(visual, "Background", Color.white, art.GoldButton, true);
                background.pixelsPerUnitMultiplier = 1.4f;
            }
            else background = Img(visual, "Background", UITheme.GoldAccent, RoundSprite, true);
            background.rectTransform.Stretch();
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.8f, 0.86f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            button.colors = colors;
            // 누름 피드백 (M37) — 바탕만 줄어든다 (등불 맥박 · 흔들림은 LampButtonView가 Visual에).
            // 누름 소리는 끈다: 켜지면 전투 소리(LampLit), 못 켜면 토스트 소리가 따로 난다 (audio_hooks — Tap과 겹치지 않게)
            background.gameObject.AddComponent<PressScale>().TapSound = false;

            // 게이지 창: 어둡게 (M37 α0.68 — 금 단추 테두리는 둘레 8px에 그대로 남고, 안쪽은 Cream 이름 · Lamp %가 읽히는 밤빛)
            var inner = Img(background.transform, "Inner", Alpha(UITheme.Background, LampInnerAlpha), RoundSprite);
            inner.rectTransform.Stretch(8, 8, 8, 8);
            var mask = NewRect("GaugeMask", inner.transform).Stretch(3, 3, 3, 3);
            mask.gameObject.AddComponent<RectMask2D>();
            var gauge = Filled(Img(mask, "Gauge", Alpha(UITheme.Lamp, 0.35f)), Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom, true);
            gauge.rectTransform.Stretch();
            gauge.fillAmount = 0f;
            var shine = Img(mask, "Shine", Alpha(Color.white, 0.35f), art.Glow);
            shine.rectTransform.At(Center, Vector2.zero, new Vector2(50, 240));
            shine.rectTransform.localRotation = Quaternion.Euler(0, 0, -22);
            shine.gameObject.SetActive(false);

            var iconGlow = Img(background.transform, "IconGlow", Alpha(UITheme.Lamp, 0.4f), art.Glow);
            iconGlow.rectTransform.At(MiddleLeft, new Vector2(-6, 10), new Vector2(130, 130));
            var ringBack = Img(background.transform, "RingBack", Alpha(UITheme.Background, 0.6f), art.Ring);
            ringBack.rectTransform.At(MiddleLeft, new Vector2(12, 12), new Vector2(98, 98));
            var ringFill = Filled(Img(background.transform, "Ring", UITheme.Lamp, art.Ring), Image.FillMethod.Radial360, (int)Image.Origin360.Bottom, false);
            ringFill.rectTransform.At(MiddleLeft, new Vector2(12, 12), new Vector2(98, 98));
            ringFill.fillAmount = 0f;
            var icon = Img(background.transform, "Icon", Color.white, art.Lamp);
            icon.preserveAspect = true;
            icon.rectTransform.BattleAt(MiddleLeft, TopCenter, new Vector2(61, 46), new Vector2(62, 62));

            // '%'는 고리 밑 안쪽 창 안에 (검증 w3 r1 — BottomLeft(10,2)는 아래 8px 금테 · 게이지 띠에 걸려 아래가 잘려 보였다): 고리 폭 받침 알약
            // (어두운 바느질 α0.7, y 12~34 — 차오르는 Lamp 게이지 띠 위에서도 Lamp 글자가 읽히게) 위에 가운데 (61, 23). 고리 아래 끝 ≈ 36
            var percentPlate = Pill(background.transform, "PercentPlate", 22f, Alpha(UITheme.Background, 0.7f));
            if (UiArt.IsArt(percentPlate.sprite)) percentPlate.color = Alpha(Color.white, 0.7f);
            percentPlate.rectTransform.At(BottomLeft, new Vector2(12, 12), new Vector2(98, 22));
            var percent = Txt(background.transform, "Percent", "0%", 24, UITheme.Lamp, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor, 24f).Outlined();
            percent.rectTransform.At(BottomLeft, new Vector2(12, 10), new Vector2(98, 26));
            // 줄은 직접 나눈다: 자동 줄바꿈이면 한글이 음절 사이에서도 끊겨 "불 밝히 / 기"가 된다. 넘치면 글자를 줄여 맞춘다 (22~28)
            var label = Txt(background.transform, "Label", "수선점\n불 밝히기", 28, UITheme.Cream, TextAlignmentOptions.Center, true).FitFloor(22f, 28f);
            label.lineSpacing = -10;
            label.rectTransform.At(MiddleRight, new Vector2(-14, 4), new Vector2(110, 104));

            new Binder(view)
                .Set("_button", button)
                .Set("_visual", visual)
                .Set("_background", background)
                .Set("_gaugeFill", gauge)
                .Set("_ringFill", ringFill)
                .Set("_icon", icon.rectTransform)
                .Set("_iconGlow", iconGlow)
                .Set("_outerGlow", outerGlow)
                .Set("_shine", shine.rectTransform)
                .Set("_label", label)
                .Set("_percentText", percent)
                .Apply();
            if (art.GoldButton == null)
            {
                SetColor(view, "_idleTint", UITheme.Secondary);
                SetColor(view, "_readyTint", new Color(0.86f, 0.62f, 0.25f));
            }
            else
            {
                // 덜 찼을 때: 금 단추 그림에 옅은 라벤더만 (≤0.35 섞기 — 진한 회보라를 곱하면 갈색으로 탁해진다)
                SetColor(view, "_idleTint", Color.Lerp(Color.white, UITheme.Lavender, 0.35f));
                SetColor(view, "_drainTint", Color.Lerp(Color.white, new Color(0.42f, 0.32f, 0.62f), 0.55f));
            }
            return view;
        }

        // ───────────── 잔향이 덱 ─────────────

        /// <summary>덱 카드 소환 비용 원판 색 (M36 #4B2A8C — Cream 숫자 대비)</summary>
        static readonly Color CostBadgeFill = new Color32(0x4B, 0x2A, 0x8C, 0xFF);

        static EchoDeckView BuildDeck(RectTransform panel, SummonAimView aim, BattleArt art, SkillArtSet skillArt, SkillVfxLibrary library)
        {
            var deck = panel.gameObject.AddComponent<EchoDeckView>();
            var cards = new EchoCardView[FormationSystem.EchoSlotCount];
            for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(panel, deck, i, art, skillArt, library);
            var binder = new Binder(deck).SetArray("_cards", cards).Set("_aim", aim);
            // 덱 이름표 (BuildStrip이 Strip/DeckTab에 만든다 — 안내 → 잔향이 N/M, M36)
            var tab = panel.parent != null ? panel.parent.Find("DeckTab") as RectTransform : null;
            if (tab != null)
            {
                var tabLabel = tab.Find("Label");
                binder.Set("_tab", tab)
                    .Set("_tabGroup", tab.GetComponent<CanvasGroup>())
                    .Set("_tabLabel", tabLabel != null ? tabLabel.GetComponent<TextMeshProUGUI>() : null)
                    .Set("_tabButton", tab.GetComponent<Button>());
            }
            binder.Apply();
            return deck;
        }

        /// <summary>
        /// 덱 카드 98x122 'Card{n}' (코치 대상 Strip/DeckPanel/Card{n}). M36 · M07:
        ///   비용 배지 = 보라 원판 #4B2A8C 44px + Resonance 바느질 테 + 26 굵게 Cream (TopLeft(−8,8) — 모자라면 숫자만 CostShort, EchoCardView),
        ///   등급 별 18px은 어둡게 · 쿨다운 덮개 뒤에 만들어 그 위에 그린다 (공유 외곽선, 4성 이상 '★4'),
        ///   빈칸 = 빈 카드 그림 + '+'(넣을 잔향이가 있으면 숨쉰다 — EchoCardView, '비어 있음' 글자 없음),
        ///   잠긴 칸 = 자물쇠 + 짧은 스테이지 알약 72x28 'FormationSystem.EchoSlotUnlockCode'(2-9, 20 굵게 Gold).
        /// </summary>
        static EchoCardView BuildCard(RectTransform panel, EchoDeckView deck, int index, BattleArt art, SkillArtSet skillArt, SkillVfxLibrary library)
        {
            var hit = Img(panel, $"Card{index}", Color.clear, null, true);
            hit.rectTransform.At(BottomLeft, new Vector2(10 + index * (CardWidth + 6), 7), new Vector2(CardWidth, CardHeight));
            var card = hit.gameObject.AddComponent<EchoCardView>();

            var visual = NewRect("Visual", hit.transform).At(Center, Vector2.zero, new Vector2(CardWidth, CardHeight));
            var readyGlow = Img(visual, "ReadyGlow", Alpha(UITheme.EchoGlow, 0f), art.Glow);
            readyGlow.rectTransform.At(Center, Vector2.zero, new Vector2(CardWidth * 1.75f, CardHeight * 1.6f));
            readyGlow.enabled = false;

            Image frame;
            if (art.CardFrame != null)
            {
                frame = Img(visual, "Frame", Color.white, art.CardFrame);
                frame.pixelsPerUnitMultiplier = 2.4f;
            }
            else frame = Img(visual, "Frame", UITheme.GoldAccent, RoundSprite);
            frame.rectTransform.Stretch();

            // 내용 (초상화 / 비용 / 별 / 쿨다운 / 잔향 부족)
            var content = NewRect("Content", visual).Stretch();
            // 초상화 배경: 수선점 안 그림(UI/portrait_bg, 편성 카드와 같은 그림)을 카드 틀 안쪽(테 ≈ 9px, 모서리 반지름 ≈ 12px)의
            // 둥근 마스크 안에 비율대로 덮어 깐다 — 계보 색은 옅게만 (EchoCardView). 아트가 없으면 예전 계보 색 판
            var backdropArt = UiArt.PortraitBg;
            bool tintIsArt = UiArt.IsArt(backdropArt);
            Image tint;
            if (tintIsArt)
            {
                var backdropMask = Img(content, "BackdropMask", Color.white, RoundSprite);
                backdropMask.pixelsPerUnitMultiplier = 2f;
                backdropMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                backdropMask.rectTransform.Stretch(9, 9, 9, 9);
                tint = Img(backdropMask.transform, "Tint", Color.Lerp(Color.white, UITheme.Lavender, 0.3f), backdropArt);
                tint.type = Image.Type.Simple;
                tint.rectTransform.Stretch();
                var fitter = tint.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backdropArt.rect.width / Mathf.Max(1f, backdropArt.rect.height);
                // 별 아래 옅은 어둠 (밝은 배경 위에서 별이 보이게, 마스크가 아래 모서리를 둥글게 자른다)
                Img(backdropMask.transform, "Shade", Alpha(UITheme.Background, 0.45f)).rectTransform.Bottom(0, 26);
            }
            else
            {
                tint = Img(content, "Tint", Alpha(UITheme.Lavender, 0.55f), RoundSprite);
                tint.rectTransform.Stretch(5, 5, 5, 5);
                Img(content, "Shade", Alpha(UITheme.Background, 0.45f), RoundSprite).rectTransform.Bottom(5, 30, 5, 5);
            }
            var portraitMask = NewRect("PortraitMask", content).Stretch(5, 5, 5, 5);
            portraitMask.gameObject.AddComponent<RectMask2D>();
            var portrait = Img(portraitMask, "Portrait", Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.At(BottomCenter, new Vector2(0, 14), new Vector2(104, 104));
            var fallback = BuildToken(portraitMask, "Fallback", 60f, new Vector2(0, 38), out var initial);

            var dim = Img(content, "Dim", Alpha(UITheme.Background, 0.55f), RoundSprite);
            dim.rectTransform.Stretch(4, 4, 4, 4);
            dim.enabled = false;
            // 쿨다운 덮개 (기능용 어둠): 둥근 모서리라 카드 테 밖으로 네모 귀가 삐져나오지 않는다
            var cooldown = Filled(Img(content, "Cooldown", Alpha(UITheme.Background, 0.72f), RoundSprite), Image.FillMethod.Radial360, (int)Image.Origin360.Top, false);
            cooldown.rectTransform.Stretch(5, 5, 5, 5);
            cooldown.enabled = false;
            var cooldownText = Txt(content, "CooldownText", string.Empty, 34, UITheme.Cream, TextAlignmentOptions.Center, true);
            cooldownText.rectTransform.Stretch(0, 0, 0, 14);
            UiArt.Outline(cooldownText);
            // 등급 별 (M36): 어둡게 · 쿨다운 덮개 위에 그리도록 그 뒤에 만든다 — 힉스필드 별 그림(글자 속 스프라이트) 18px,
            // 4성 이상은 별 하나 + 숫자 '★4' (EchoCardView), 숫자는 공유 외곽선
            var stars = Txt(content, "Stars", UITheme.Stars(3), 18, Color.white, TextAlignmentOptions.Center, true).Outlined();
            stars.rectTransform.Bottom(4, 24, 4, 4);

            // 소환 비용 (M36): 보라 원판 44px + Resonance 바느질 테 + 26 굵게 Cream (모자라면 숫자만 CostShort — M07, EchoCardView)
            var costBadge = Img(content, "CostBadge", CostBadgeFill, CircleSprite);
            costBadge.rectTransform.At(TopLeft, new Vector2(-8, 8), new Vector2(44, 44));
            var costStitch = UiArt.RingStitch;
            Img(costBadge.transform, "Rim", UITheme.Resonance, UiArt.IsArt(costStitch) ? costStitch : art.Ring).rectTransform.Stretch(-4, -4, -4, -4);
            var costText = Txt(costBadge.transform, "Cost", "3", 26, UITheme.Cream, TextAlignmentOptions.Center, true);
            costText.rectTransform.Stretch(0, 0, 0, 2);

            // 고유 스킬 배지 (오른쪽 위, 비용 배지와 대칭): 어두운 메달 홈 + 스킬 아이콘 + 바느질 고리 (상시 패시브는 보랏빛, 발동 스킬은 금빛 — EchoCardView)
            var medallion = UiArt.MedallionDark;
            bool medallionArt = UiArt.IsArt(medallion);
            var skillBadge = Img(content, "SkillBadge", medallionArt ? Color.white : Alpha(UITheme.Panel, 0.94f), medallionArt ? medallion : CircleSprite);
            skillBadge.preserveAspect = true;
            skillBadge.rectTransform.At(TopRight, new Vector2(6, 6), new Vector2(34, 34));
            var stitch = UiArt.RingStitch;
            var skillRim = Img(skillBadge.transform, "Rim", UITheme.Gold, UiArt.IsArt(stitch) ? stitch : art.Ring);
            skillRim.rectTransform.Stretch(-5, -5, -5, -5);
            var skillIcon = Img(skillBadge.transform, "Icon", Color.white, skillArt.Motif(VfxMotif.Sparkle));
            skillIcon.preserveAspect = true;
            skillIcon.rectTransform.At(Center, Vector2.zero, medallionArt ? new Vector2(22, 22) : new Vector2(28, 28));
            skillBadge.gameObject.SetActive(false);

            // 빈칸: 빈 카드 그림(ui_card_empty, 3:4 비율 그대로) + 가운데 '+' (M36 — 넣을 잔향이가 있으면 숨쉬고 없으면 α0.3, EchoCardView).
            // 바느질 + 그림 44px, 없으면 40x8 · 8x40 막대 (예전 '비어 있음' 글자는 없앴다)
            var empty = NewRect("Empty", visual).Stretch();
            var emptyArt = UiArt.CardEmpty;
            if (UiArt.IsArt(emptyArt))
            {
                var emptyBack = Img(empty, "Back", Color.white, emptyArt);
                emptyBack.preserveAspect = true;
                emptyBack.rectTransform.Stretch(5, 5, 5, 5);
            }
            else Img(empty, "Back", Alpha(UITheme.Background, 0.6f), RoundSprite).rectTransform.Stretch(5, 5, 5, 5);
            Object[] plus;
            var plusIcon = ArtIcon(empty, "Plus", UiArt.SlotPlus, Center, Vector2.zero, 44f);
            if (plusIcon != null) plus = new Object[] { plusIcon };
            else
            {
                var barH = Img(empty, "Bar", Alpha(UITheme.Cream, 0.9f), RoundSprite);
                barH.rectTransform.At(Center, Vector2.zero, new Vector2(40, 8));
                var barV = Img(empty, "Bar", Alpha(UITheme.Cream, 0.9f), RoundSprite);
                barV.rectTransform.At(Center, Vector2.zero, new Vector2(8, 40));
                plus = new Object[] { barH, barV };
            }
            empty.gameObject.SetActive(false);

            // 잠긴 칸: 잠긴 카드 그림(ui_card_locked, 바느질 자물쇠가 그려져 있음 — 3:4 비율 그대로). 없으면 어두운 판 + 자물쇠 아이콘 46 (y +14)
            var locked = NewRect("Locked", visual).Stretch();
            var lockedArt = UiArt.CardLocked;
            if (UiArt.IsArt(lockedArt))
            {
                var lockedBack = Img(locked, "Back", Color.white, lockedArt);
                lockedBack.preserveAspect = true;
                lockedBack.rectTransform.Stretch(5, 5, 5, 5);
            }
            else
            {
                Img(locked, "Back", Alpha(UITheme.Background, 0.75f), RoundSprite).rectTransform.Stretch(5, 5, 5, 5);
                var lockIcon = Img(locked, "Lock", art.LockIsArt ? Color.white : UITheme.SubText, art.Lock);
                lockIcon.preserveAspect = true;
                lockIcon.rectTransform.At(Center, new Vector2(0, 14), new Vector2(46, 46));
            }
            // 짧은 스테이지 알약 (M36): 클리어해야 하는 스테이지 '2-9' — FormatStage(EchoSlotUnlockStage) = '2-10'이 아니다. 문장은 토스트 몫
            var lockPill = Pill(locked, "Pill", 28f, Alpha(UITheme.Background, 0.9f));
            lockPill.rectTransform.At(BottomCenter, new Vector2(0, 8), new Vector2(72, 28));
            Txt(lockPill.transform, "Label", FormationSystem.EchoSlotUnlockCode, 20, UITheme.Gold, TextAlignmentOptions.Center, true)
                .FitFloor(UIType.AutoSizeFloor, 20f).rectTransform.Stretch(4, 4, 0, 0);
            locked.gameObject.SetActive(false);

            new Binder(card)
                .Set("_deck", deck)
                .SetInt("_index", index)
                .Set("_visual", visual)
                .Set("_tint", tint)
                .Set("_portrait", portrait)
                .Set("_fallback", fallback)
                .Set("_fallbackInitial", initial)
                .Set("_costBadge", costBadge)
                .Set("_costText", costText)
                .Set("_starsText", stars)
                .Set("_cooldown", cooldown)
                .Set("_cooldownText", cooldownText)
                .Set("_dim", dim)
                .Set("_readyGlow", readyGlow)
                .Set("_content", content.gameObject)
                .Set("_empty", empty.gameObject)
                .Set("_locked", locked.gameObject)
                .SetArray("_emptyPlus", plus)
                .Set("_skillBadge", skillBadge.rectTransform)
                .Set("_skillIcon", skillIcon)
                .Set("_skillRim", skillRim)
                .Set("_library", library)
                .Apply();
            SetBool(card, "_tintIsArt", tintIsArt);
            return card;
        }

        // ───────────── 드래그 방향 지정 ─────────────

        static RectTransform BuildAimArrow(RectTransform ground, BattleArt art, out Image line, out Image head)
        {
            var arrow = NewRect("AimArrow", ground).At(Center, Vector2.zero, Vector2.zero);
            // 줄: 혜성 꼬리 그림(fx_trail, 4:1)을 그림 비율 그대로 (예전처럼 10:1로 늘이지 않는다, 밝은 머리가 화살촉 쪽)
            line = Img(arrow, "Line", Alpha(Color.white, 0f), art.Trail);
            if (UiArt.IsArt(art.Trail) && art.Trail.rect.width > 0f)
            {
                // 혜성 그림은 가로 23~77%에만 그려져 있으므로 360 폭을 x=80에 두어 보이는 꼬리(≈164~356)가 화살촉 바로 앞에서 끝나게 한다
                line.rectTransform.BattleAt(Center, new Vector2(0f, 0.5f), new Vector2(80, 0), new Vector2(360, 360f * art.Trail.rect.height / art.Trail.rect.width));
            }
            else line.rectTransform.BattleAt(Center, new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(318, 30));
            head = Img(arrow, "Head", Alpha(Color.white, 0f), art.Chevron);
            if (art.ChevronIsArt)
            {
                // 바느질 꺾쇠 그림은 이미 오른쪽(화살 방향)을 향한다. 그림의 가로 53% · 세로 96%만 차 있으므로 93x70 → 보이는 촉 ≈ 길이 49 · 높이 67 (예전 촉 크기)
                head.preserveAspect = true;
                head.rectTransform.At(Center, new Vector2(388, 0), new Vector2(93, 70));
            }
            else
            {
                head.rectTransform.At(Center, new Vector2(388, 0), new Vector2(70, 50));
                head.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            }
            arrow.gameObject.SetActive(false);
            return arrow;
        }

        static SummonAimView BuildAimOverlay(RectTransform root, BattleFieldMapper mapper, RectTransform strip, GuardianSlotView[] guardians,
            Image fan, RectTransform arrow, Image line, Image head, BattleArt art)
        {
            var overlay = NewRect("AimOverlay", root).Stretch();
            var view = overlay.gameObject.AddComponent<SummonAimView>();

            var ghost = NewRect("Ghost", overlay).At(Center, Vector2.zero, new Vector2(130, 130));
            var ghostGroup = ghost.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.interactable = false;
            Img(ghost, "Glow", Alpha(UITheme.EchoGlow, 0.7f), art.Glow).rectTransform.At(Center, Vector2.zero, new Vector2(210, 210));
            var portrait = Img(ghost, "Portrait", Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.At(Center, Vector2.zero, new Vector2(130, 130));
            var fallback = BuildToken(ghost, "Fallback", 76f, new Vector2(0, 28), out var initial);
            // 놓으면 취소: 빨간 배지 그림(ui_badge_red) + "X" 글자 (없으면 예전 붉은 원 + 막대)
            Image cancel;
            if (UiArt.IsArt(UiArt.BadgeRed))
            {
                cancel = UiArt.Badge(ghost, "Cancel", Center, new Vector2(46, 46), 44f, "X");
                // M23: 'X' 글자 자동 최소를 글자 바닥(16)으로 (UiArt.Badge 44px의 0.36배 = 15.8 — UiSceneLint 예외가 필요 없게)
                var x = cancel.GetComponentInChildren<TMP_Text>(true);
                if (x != null) x.fontSizeMin = Mathf.Max(x.fontSizeMin, UIType.AutoSizeFloor);
            }
            else
            {
                cancel = Img(ghost, "Cancel", UITheme.Danger, CircleSprite);
                cancel.rectTransform.At(Center, new Vector2(46, 46), new Vector2(44, 44));
                foreach (float angle in new[] { 45f, -45f })
                {
                    var bar = Img(cancel.transform, "Bar", UITheme.Text, RoundSprite);
                    bar.rectTransform.At(Center, Vector2.zero, new Vector2(26, 7));
                    bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
            cancel.gameObject.SetActive(false);

            // 안내 띠: 금테 밤하늘 9-slice (52px → 배율 2.5, 예전 색 밑줄 없이)
            var hint = Panel(overlay, "Hint", art, 2.5f);
            if (art.Panel == null) hint.color = Alpha(UITheme.Panel, 0.92f);
            // HUD 아래 가운데 (UX-SPEC-IA §8.3 — 기준 화면 330~750 x 360~412: 끄는 동안 권장 전투력 알약을 가리지 않고, 손가락에도 가리지 않는 위쪽)
            hint.rectTransform.At(TopCenter, new Vector2(0, -230), new Vector2(420, 52));
            var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            if (art.Panel == null) Img(hint.transform, "Edge", Alpha(UITheme.EchoGlow, 0.8f)).rectTransform.Bottom(5, 2, 30, 30);
            var hintText = Txt(hint.transform, "Text", "놓으면 이 방향으로 소환해요", 22, UITheme.Cream, TextAlignmentOptions.Center, true);
            hintText.rectTransform.Stretch(14, 14, 0, 2);
            hintText.enableAutoSizing = true;
            hintText.fontSizeMin = UIType.AutoSizeFloor;
            hintText.fontSizeMax = 22;

            new Binder(view)
                .Set("_mapper", mapper)
                .Set("_overlay", overlay)
                .Set("_strip", strip)
                .SetArray("_guardians", guardians)
                .Set("_fan", fan)
                .Set("_arrow", arrow)
                .Set("_arrowLine", line)
                .Set("_arrowHead", head)
                .Set("_ghost", ghost)
                .Set("_ghostGroup", ghostGroup)
                .Set("_ghostPortrait", portrait)
                .Set("_ghostFallback", fallback)
                .Set("_ghostInitial", initial)
                .Set("_cancelMark", cancel.gameObject)
                .Set("_hintRoot", hint.rectTransform)
                .Set("_hintGroup", hintGroup)
                .Set("_hintText", hintText)
                .Apply();
            return view;
        }

        // ───────────── 스킬 그림 묶음 ─────────────

        /// <summary>런타임이 쓰는 스킬/기믹/시너지 그림을 한곳에 굽는다 (ArtLib은 에디터 전용이라 씬에 참조로 남긴다)</summary>
        static SkillVfxLibrary BuildSkillLibrary(RectTransform root, SkillArtSet skillArt)
        {
            var holder = NewRect("SkillArt", root).Stretch();
            var library = holder.gameObject.AddComponent<SkillVfxLibrary>();

            var so = new SerializedObject(library);
            var vfx = so.FindProperty("_vfx");
            vfx.arraySize = SkillVfx.All.Length;
            for (int i = 0; i < SkillVfx.All.Length; i++)
            {
                string key = SkillVfx.All[i];
                var element = vfx.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = key;
                element.FindPropertyRelative("sprite").objectReferenceValue = skillArt.Vfx(key, out bool isArt);
                element.FindPropertyRelative("isArt").boolValue = isArt;
            }
            WriteIconEntries(so.FindProperty("_synergyIcons"), SkillArtSet.SynergyIcons());
            WriteIconEntries(so.FindProperty("_attackArt"), SkillArtSet.AttackArt());
            WriteIconEntries(so.FindProperty("_skillIcons"), SkillArtSet.SkillIcons());
            so.ApplyModifiedPropertiesWithoutUndo();

            new Binder(library)
                .SetArray("_motifs", skillArt.Motifs)
                .SetArray("_traitIcons", skillArt.TraitIcons)
                .SetArray("_styleIcons", skillArt.StyleIcons)
                .SetArray("_stageIcons", skillArt.StageIcons)
                .SetArray("_overlays", skillArt.Overlays)
                .Apply();
            return library;
        }

        static void WriteIconEntries(SerializedProperty array, List<KeyValuePair<string, Sprite>> entries)
        {
            array.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = entries[i].Key;
                element.FindPropertyRelative("sprite").objectReferenceValue = entries[i].Value;
            }
        }

        // ───────────── 스테이지 기믹 분위기 ─────────────

        static StageAmbienceView BuildAmbience(RectTransform layer, BattleArt art, SkillVfxLibrary library)
        {
            var view = layer.gameObject.AddComponent<StageAmbienceView>();
            var wash = Img(layer, "Wash", Alpha(Color.white, 0f));
            wash.rectTransform.Stretch();
            wash.enabled = false;
            var shade = Img(layer, "Shade", Alpha(Color.black, 0f), art.Vignette);
            shade.rectTransform.Stretch(-40, -40, -40, -40);
            shade.enabled = false;
            // 보름달: 덮개 그림(overlay_fullmoon)에 달이 그려져 있으면 런타임이 끈다. 덮개가 없을 때만 힉스필드 달 그림(UI/moon)
            var moonArt = UiArt.Moon;
            bool moonIsArt = UiArt.IsArt(moonArt);
            var moon = Img(layer, "Moon", Alpha(UITheme.Cream, 0f), moonIsArt ? moonArt : art.Glow);
            moon.preserveAspect = moonIsArt;
            moon.rectTransform.At(Center, new Vector2(330, 180), moonIsArt ? new Vector2(220, 220) : new Vector2(560, 560));
            moon.enabled = false;
            var overlayA = Img(layer, "OverlayA", Alpha(Color.white, 0f));
            overlayA.rectTransform.At(Center, Vector2.zero, new Vector2(1210, 907));
            overlayA.enabled = false;
            var overlayB = Img(layer, "OverlayB", Alpha(Color.white, 0f));
            overlayB.rectTransform.At(Center, Vector2.zero, new Vector2(1210, 907));
            overlayB.enabled = false;
            // StageAmbienceView.MaxMotes(96)만큼 미리 — 런타임이 Awake에서 56개를 복제하지 않게 (fx2 leftovers)
            var motes = new Object[96];
            for (int i = 0; i < motes.Length; i++)
            {
                var mote = Img(layer, "Mote", Alpha(Color.white, 0f), art.Sparkle);
                mote.rectTransform.At(Center, Vector2.zero, new Vector2(12, 12));
                mote.enabled = false;
                motes[i] = mote;
            }
            new Binder(view)
                .Set("_root", layer)
                .Set("_overlayA", overlayA)
                .Set("_overlayB", overlayB)
                .Set("_wash", wash)
                .Set("_shade", shade)
                .Set("_moon", moon)
                .SetArray("_motes", motes)
                .Set("_library", library)
                .Set("_streak", art.Trail)
                .Set("_soft", art.Soft)
                .Set("_sparkle", art.Sparkle)
                .Set("_glow", art.Glow)
                // 기믹별 날씨 입자 (힉스필드 흰색 그림, 그림 비율 그대로 — 없으면 위의 흰색 모양)
                .SetIf("_raindrop", ArtLib.FX("fx_mote_raindrop"))
                .SetIf("_petal", ArtLib.FX("fx_mote_petal"))
                .SetIf("_dustPuff", ArtLib.FX("fx_mote_dust"))
                .SetIf("_firefly", ArtLib.FX("fx_mote_firefly"))
                .SetIf("_twinkle", ArtLib.FX("fx_mote_twinkle"))
                .SetArray("_confetti", UiArt.Confetti)
                .Apply();
            return view;
        }

        // ───────────── 시전 말풍선 ─────────────

        /// <summary>
        /// 스킬 대사 말풍선: 크림 말풍선 9-slice(ui_bubble_9s, 46px → 배율 3.5) + 말풍선 꼬리 그림(ui_bubble_tail) — 스킬 색은 바탕에 옅게.
        /// 아트가 없으면 예전 크림 알약 + 스킬 색 띠 + 생각 풍선 꼬리 두 알. 위쪽 메타 HUD(권장 전투력 알약)에 가리지 않게 비킨다 (SkillCalloutView)
        /// </summary>
        static SkillCalloutView BuildCallouts(RectTransform layer, RectTransform battleRoot)
        {
            var view = layer.gameObject.AddComponent<SkillCalloutView>();
            var bubbleArt = UiArt.Bubble9s;
            var tailArt = UiArt.BubbleTail;
            bool bubbleIsArt = UiArt.IsArt(bubbleArt) && UiArt.IsArt(tailArt);
            const int count = 5;
            var bubbles = new Object[count];
            var groups = new Object[count];
            var texts = new Object[count];
            var backs = new Object[count];
            var accents = new Object[count];
            var tails = new Object[count];
            for (int i = 0; i < count; i++)
            {
                var bubble = NewRect($"Bubble{i}", layer).BattleAt(Center, BottomCenter, Vector2.zero, new Vector2(200, 64));
                var group = bubble.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                Image back;
                Image accent;
                if (bubbleIsArt)
                {
                    back = UiArt.Panel(bubble, "Back", bubbleArt, 3.5f);
                    back.rectTransform.BattleAt(BottomCenter, BottomCenter, new Vector2(0, 14), new Vector2(160, 46));
                    // 꼬리: 위쪽 가장자리(외곽선 없음)가 말풍선 아래 테에 겹친다. 끝이 시전자 머리(말풍선 발밑)를 가리킨다
                    var tail = Img(bubble, "Tail", Color.white, tailArt);
                    tail.preserveAspect = true;
                    tail.rectTransform.BattleAt(BottomCenter, TopCenter, new Vector2(-8, 18), new Vector2(24, 24));
                    tails[i] = tail;
                    // 스킬 색은 말풍선 바탕에 옅게 (SkillCalloutView._accentBlend)
                    accent = back;
                }
                else
                {
                    var tailBig = Img(bubble, "Tail", UITheme.Parchment, CircleSprite);
                    tailBig.rectTransform.BattleAt(BottomCenter, Center, new Vector2(-8, 9), new Vector2(15, 15));
                    var tailSmall = Img(bubble, "TailDot", UITheme.Parchment, CircleSprite);
                    tailSmall.rectTransform.BattleAt(BottomCenter, Center, new Vector2(-15, -1), new Vector2(8, 8));
                    back = Img(bubble, "Back", UITheme.Parchment, RoundSprite);
                    back.rectTransform.BattleAt(BottomCenter, BottomCenter, new Vector2(0, 14), new Vector2(160, 46));
                    accent = Img(back.transform, "Accent", UITheme.Gold, RoundSprite);
                    accent.rectTransform.Bottom(4, 3, 16, 16);
                }
                var text = Txt(back.transform, "Text", string.Empty, 22, UITheme.Ink, TextAlignmentOptions.Center, true);
                if (bubbleIsArt) text.rectTransform.Stretch(12, 12, 3, 5);
                else text.rectTransform.Stretch(10, 10, 2, 7);
                text.enableAutoSizing = true;
                // M41: 18~22pt (SkillCalloutView.FontMin / FontMax — 예전 15pt는 작은 기기에서 읽히지 않았다)
                text.fontSizeMin = SkillCalloutView.FontMin;
                text.fontSizeMax = SkillCalloutView.FontMax;
                bubble.gameObject.SetActive(false);
                bubbles[i] = bubble;
                groups[i] = group;
                texts[i] = text;
                backs[i] = back.rectTransform;
                accents[i] = accent;
            }
            new Binder(view)
                .Set("_area", layer)
                .SetArray("_bubbles", bubbles)
                .SetArray("_groups", groups)
                .SetArray("_texts", texts)
                .SetArray("_backs", backs)
                .SetArray("_accents", accents)
                .SetArray("_tails", tails)
                .Set("_avoidRoot", battleRoot)
                .Apply();
            // 아트 말풍선은 스킬 색을 바탕에 옅게만 (흰색 → 스킬 색 12%)
            SetFloat(view, "_accentBlend", bubbleIsArt ? 0.12f : 0f);
            return view;
        }

        // ───────────── 알림 띠 ─────────────

        /// <summary>
        /// 알림 띠 2칸 (큰 스킬 이름 / 스테이지 기믹 / 보스 분노). 위쪽 가운데는 북쪽 레인의 적과 지킴이 말풍선이 지나는 자리라 비워 두고,
        /// 동쪽 레인과 남동쪽 레인 사이의 빈 곳(수선점 오른쪽 아래, 전장 오른쪽 끝에 붙여)에 둔다.
        /// 세로 위치는 BattleNoticeView가 수선점 높이(월드 y)로 맞추므로 화면 비율이 달라도 레인 사이에 머문다. 바탕은 반투명, 누름은 통과.
        /// 줄 336x58: 제목 23 굵게(줄 상자 34 — Noto 줄 높이 1.448em이라 더 낮으면 자동 크기가 18로 줄인다) + 설명 18(하한 16, M23).
        /// 설명이 16에서도 한 줄(262)에 들지 않으면 BattleNoticeView가 그 줄만 84로 키워 두 줄로 접는다 (부모 높이를 따라 늘어나는 설명 칸).
        /// </summary>
        static BattleNoticeView BuildNotice(RectTransform root, BattleArt art, BattleFieldMapper mapper)
        {
            var area = NewRect("NoticeArea", root).Stretch(0, 0, 0, StripHeight);
            var view = area.gameObject.AddComponent<BattleNoticeView>();
            const int count = 2;
            const float rowHeight = 58f;
            const float tallRowHeight = 84f;
            const float spacing = rowHeight + 6f;
            const float rightMargin = 14f;
            const float worldTop = -0.2f;
            // 기준 화면에서의 첫 줄 윗변 (mapper가 없을 때)
            float top = RefWorldToField(new Vector2(0f, worldTop)).y;
            float offsetX = 540f - rightMargin;
            var rows = new Object[count];
            var groups = new Object[count];
            var icons = new Object[count];
            var glows = new Object[count];
            var titles = new Object[count];
            var subtitles = new Object[count];
            var edges = new Object[count];
            for (int i = 0; i < count; i++)
            {
                var row = NewRect($"Notice{i}", area).BattleAt(Center, TopRight, new Vector2(offsetX, top - i * spacing), new Vector2(336, rowHeight));
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                // 58px 줄이라 배율 2.2 (테두리 ≈ 22px — 1.6이면 위아래 테두리 60px가 줄 높이를 넘쳐 모서리가 찌그러진다)
                var back = Panel(row, "Back", art, 2.2f);
                back.color = art.Panel != null ? new Color(1f, 1f, 1f, 0.84f) : Alpha(UITheme.Panel, 0.82f);
                back.rectTransform.Stretch();
                // 예전 금 밑줄: 금테 판에서는 감춘다 (BattleNoticeView는 색만 바꾼다)
                var edge = Img(row, "Edge", UITheme.Gold);
                edge.rectTransform.Bottom(5, 3, 30, 30);
                edge.enabled = art.Panel == null;
                var glow = Img(row, "IconGlow", Alpha(UITheme.Lavender, 0.6f), art.Glow);
                glow.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(33, 0), new Vector2(80, 80));
                var icon = Img(row, "Icon", Color.white);
                icon.preserveAspect = true;
                icon.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(33, 0), new Vector2(42, 42));
                // 제목: 위에 붙은 34 높이 (23의 줄 상자 33.3이 들어간다 — 두 줄 띠에서도 제자리)
                var title = Txt(row, "Title", string.Empty, 23, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
                title.rectTransform.Top(0, 34, 62, 12);
                title.enableAutoSizing = true;
                title.fontSizeMin = UIType.AutoSizeFloor;
                title.fontSizeMax = 23;
                // 부제 18 (자동 하한 16 — M23, 예전 12~16): 줄 높이를 따라 늘어나는 칸 (58 줄 = 28 높이 한 줄 · 84 줄 = 54 높이 두 줄), 줄바꿈 켬
                var subtitle = Txt(row, "Subtitle", string.Empty, UIType.Micro, UITheme.SubText, TextAlignmentOptions.MidlineLeft, false, true);
                subtitle.rectTransform.Stretch(62, 12, 28, 2);
                subtitle.enableAutoSizing = true;
                subtitle.fontSizeMin = UIType.AutoSizeFloor;
                subtitle.fontSizeMax = UIType.Micro;
                row.gameObject.SetActive(false);
                rows[i] = row;
                groups[i] = group;
                icons[i] = icon;
                glows[i] = glow;
                titles[i] = title;
                subtitles[i] = subtitle;
                edges[i] = edge;
            }
            new Binder(view)
                .SetArray("_rows", rows)
                .SetArray("_groups", groups)
                .SetArray("_icons", icons)
                .SetArray("_iconGlows", glows)
                .SetArray("_titles", titles)
                .SetArray("_subtitles", subtitles)
                .SetArray("_edges", edges)
                .Set("_mapper", mapper)
                .Apply();
            SetFloat(view, "_rowSpacing", spacing);
            SetFloat(view, "_rowHeight", rowHeight);
            SetFloat(view, "_tallRowHeight", tallRowHeight);
            SetFloat(view, "_worldTop", worldTop);
            SetFloat(view, "_rightMargin", rightMargin);
            SetVector2(view, "_slide", new Vector2(40f, 0f));
            SetFloat(view, "_top", top);
            SetFloat(view, "_x", offsetX);
            return view;
        }

        // ───────────── 시너지 ─────────────

        /// <summary>
        /// 스테이지 칸 아래 줄 'SynergyRow' 64 높이 (TopLeft(16,−154) — 기준 화면 284~348, 시트가 열리면 SheetFade 0으로 사라진다):
        /// 스테이지 기믹 칩(60 높이, 누르면 한 줄 설명) + 시너지 요약 칩 'SynergySummary' 하나 (M43 — 예전 아이콘 8칸 대신, 누르면 시너지 상세 / 도감).
        /// 요약 칩: 바탕 판 60 높이(아트 없으면 Panel@0.94 + 2px GoldAccent 아래 테), 아이콘 3개 40px 원(3px 시너지 색 테, 가운데 x 30/58/86 — 0번이 맨 위),
        /// '시너지 N' 24 굵게 Cream (x 114부터, N 금빛), '›' 26 SubText (오른쪽 12). 폭은 SynergyHudView가 글자에 맞춰 200~280.
        /// 두 칩 다 누르는 칸 88 높이 (보스 도전 버튼이 뒤에 만들어져 겹치는 띠에서는 그쪽이 먼저 받는다). 줄 오른쪽 끝은 x 540 안 (기믹 이름이 가장 길 때도).
        /// </summary>
        static SynergyHudView BuildSynergyHud(RectTransform hudRoot, BattleArt art, SkillVfxLibrary library)
        {
            // 줄 사각형은 x 16~540 (§5.2 05: SynergyRow 오른쪽 끝 ≤ 540 — 칩은 왼쪽부터 놓이고 가장 넓어도 512에서 끝난다)
            var row = NewRect("SynergyRow", hudRoot).At(TopLeft, new Vector2(16, -154), new Vector2(524, 64));
            // 시트가 열리면 사라진다 — 올라간 전투의 위쪽 레인을 가리지 않게 (UX-SPEC-IA §1.6 · §8.3)
            SheetFade.Attach(row.gameObject, 0f);
            var view = row.gameObject.AddComponent<SynergyHudView>();

            var chip = NewRect("ModifierChip", row).BattleAt(MiddleLeft, MiddleLeft, Vector2.zero, new Vector2(190, 60));
            // 기믹 칩 바탕: 금테 밤하늘 9-slice (60px → 배율 2.4), 색은 흰색에 기믹 색을 옅게 (SynergyHudView)
            var chipBack = Panel(chip, "Back", art, 2.4f, true);
            if (art.Panel == null) chipBack.color = Alpha(UITheme.Panel, 0.92f);
            chipBack.rectTransform.Stretch();
            var chipButton = chipBack.gameObject.AddComponent<Button>();
            chipButton.targetGraphic = chipBack;
            chipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            chipBack.gameObject.AddComponent<PressScale>();
            chipBack.HitPad(new Vector2(190, 60), 0f, UISize.MinHit);
            var chipGlow = Img(chip, "Glow", Alpha(UITheme.Lavender, 0.4f), art.Glow);
            chipGlow.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(30, 0), new Vector2(84, 84));
            var chipIcon = Img(chip, "Icon", Color.white);
            chipIcon.preserveAspect = true;
            chipIcon.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(30, 0), new Vector2(40, 40));
            var chipLabel = Txt(chip, "Label", "비 오는 밤", UIType.Caption, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            chipLabel.rectTransform.Stretch(56, 14, 0, 2);
            if (art.Panel == null) Img(chip, "Edge", Alpha(UITheme.GoldAccent, 0.6f)).rectTransform.Bottom(3, 2, 16, 16);
            chip.gameObject.SetActive(false);

            // 시너지 요약 칩 (M43): 판 + 아이콘 3개(뒤에서부터 만들어 0번이 맨 위) + 반짝임 + '시너지 N' + '›'
            var summary = Panel(row, "SynergySummary", art, 2.4f, true);
            summary.color = art.Panel == null ? Alpha(UITheme.Panel, 0.94f) : Alpha(Color.white, 0.94f);
            var summaryRect = summary.rectTransform.BattleAt(MiddleLeft, MiddleLeft, Vector2.zero, new Vector2(200, 60));
            if (art.Panel == null) Img(summaryRect, "Edge", UITheme.GoldAccent).rectTransform.Bottom(0, 2, 12, 12);
            var summaryButton = summary.gameObject.AddComponent<Button>();
            summaryButton.targetGraphic = summary;
            summaryButton.navigation = new Navigation { mode = Navigation.Mode.None };
            summary.gameObject.AddComponent<PressScale>();
            summary.HitPad(new Vector2(200, 60), 0f, UISize.MinHit);
            const int iconCount = 3;
            var icons = new Object[iconCount];
            var iconImages = new Object[iconCount];
            var rims = new Object[iconCount];
            for (int i = iconCount - 1; i >= 0; i--)
            {
                var slot = NewRect($"Icon{i}", summaryRect).BattleAt(MiddleLeft, Center, new Vector2(30 + i * 28, 0), new Vector2(40, 40));
                // 3px 시너지 색 테 = 색 원 40 위에 어두운 원판 34 (SynergyHudView가 테 색을 입힌다)
                var rim = Img(slot, "Rim", UITheme.Gold, CircleSprite);
                rim.rectTransform.Stretch();
                Img(slot, "Back", UITheme.Inset, CircleSprite).rectTransform.Stretch(3, 3, 3, 3);
                var icon = Img(slot, "Icon", Color.white);
                icon.preserveAspect = true;
                icon.rectTransform.At(Center, Vector2.zero, new Vector2(36, 36));
                slot.gameObject.SetActive(false);
                icons[i] = slot;
                iconImages[i] = icon;
                rims[i] = rim;
            }
            var glint = Img(summaryRect, "Glint", Alpha(UITheme.Cream, 0f), art.Sparkle);
            glint.preserveAspect = true;
            glint.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(42, 12), new Vector2(30, 30));
            glint.enabled = false;
            var summaryLabel = Txt(summaryRect, "Label", "시너지 3", UIType.Label, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true);
            summaryLabel.rectTransform.Stretch(114, 34, 0, 2);
            var chevron = Txt(summaryRect, "Chevron", "›", 26, UITheme.SubText, TextAlignmentOptions.MidlineRight, true);
            chevron.rectTransform.At(MiddleRight, new Vector2(-12, 2), new Vector2(18, 40));
            summary.gameObject.SetActive(false);

            new Binder(view)
                .Set("_row", row)
                .Set("_modifierChip", chip)
                .Set("_modifierBack", chipBack)
                .Set("_modifierIcon", chipIcon)
                .Set("_modifierGlow", chipGlow)
                .Set("_modifierLabel", chipLabel)
                .Set("_modifierButton", chipButton)
                .Set("_summary", summaryRect)
                .Set("_summaryButton", summaryButton)
                .Set("_summaryLabel", summaryLabel)
                .SetArray("_summaryIcons", icons)
                .SetArray("_summaryIconImages", iconImages)
                .SetArray("_summaryRims", rims)
                .Set("_summaryGlint", glint)
                .Set("_library", library)
                .Apply();
            SetBool(view, "_modifierBackIsArt", art.Panel != null);
            return view;
        }

        /// <summary>스테이지 시작 "시너지 발동!" 띠 (전장 아래쪽, 덱 띠 바로 위)</summary>
        static SynergyBannerView BuildSynergyBanner(RectTransform root, BattleArt art, SkillVfxLibrary library)
        {
            var area = NewRect("SynergyBannerArea", root).Stretch(0, 0, 0, StripHeight);
            var view = area.gameObject.AddComponent<SynergyBannerView>();
            var banner = NewRect("SynergyBanner", area).At(BottomCenter, new Vector2(0, 44), new Vector2(1040, 108));
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var glow = Img(banner, "Glow", Alpha(UITheme.Lavender, 0f), art.Glow);
            glow.rectTransform.At(Center, new Vector2(0, -10), new Vector2(1240, 250));
            // 띠 바탕: 금테 밤하늘 9-slice (72px → 배율 2)
            var ribbon = Panel(banner, "Ribbon", art, 2f);
            if (art.Panel == null) ribbon.color = Alpha(UITheme.Background, 0.6f);
            ribbon.rectTransform.Stretch(40, 40, 36, 0);
            var title = Txt(banner, "Title", "시너지 발동!", 24, UITheme.Gold, TextAlignmentOptions.Center, true);
            title.rectTransform.Top(0, 34);
            var sweep = Img(banner, "Sweep", Alpha(Color.white, 0f), art.Glow);
            sweep.rectTransform.At(Center, new Vector2(0, -17), new Vector2(90, 96));
            sweep.rectTransform.localRotation = Quaternion.Euler(0, 0, -18);
            sweep.enabled = false;

            const int count = 8;
            var chips = new Object[count];
            var chipGroups = new Object[count];
            var backs = new Object[count];
            var icons = new Object[count];
            var iconGlows = new Object[count];
            var labels = new Object[count];
            var badges = new Object[count];
            var badgeTexts = new Object[count];
            var edges = new Object[count];
            for (int i = 0; i < count; i++)
            {
                var chip = NewRect($"Chip{i}", banner).BattleAt(Center, Center, new Vector2(0, -17), new Vector2(200, 58));
                var chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
                chipGroup.blocksRaycasts = false;
                // 칩: 어두운 바느질 알약 (58px → 캡슐 배율 ≈ 1.66), 색은 흰색에 시너지 색을 옅게 (SynergyBannerView)
                var back = Pill(chip, "Back", 58f, Alpha(UITheme.Panel, 0.95f));
                back.rectTransform.Stretch();
                bool chipArt = UiArt.IsArt(back.sprite);
                // 예전 색 밑줄: 아트 알약에서는 감춘다 (SynergyBannerView는 색만 바꾼다)
                var edge = Img(chip, "Edge", UITheme.Gold, RoundSprite);
                edge.rectTransform.Bottom(4, 3, 16, 16);
                edge.enabled = !chipArt;
                var iconGlow = Img(chip, "IconGlow", Alpha(UITheme.Lavender, 0.55f), art.Glow);
                iconGlow.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(36, 0), new Vector2(76, 76));
                var icon = Img(chip, "Icon", Color.white);
                icon.preserveAspect = true;
                icon.rectTransform.BattleAt(MiddleLeft, Center, new Vector2(36, 0), new Vector2(44, 44));
                var label = Txt(chip, "Label", "수선 듀오", 23, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis();
                label.rectTransform.BattleAt(MiddleLeft, MiddleLeft, new Vector2(66, 1), new Vector2(110, 40));
                // 단계 수 배지: 어두운 메달 홈(ui_medallion_dark, 금테) 위에 금색 숫자 (원판 + 빛 고리 대신)
                var medallion = UiArt.MedallionDark;
                bool medallionArt = UiArt.IsArt(medallion);
                var badge = Img(chip, "Badge", medallionArt ? Color.white : Alpha(UITheme.Background, 0.92f), medallionArt ? medallion : CircleSprite);
                badge.preserveAspect = true;
                badge.rectTransform.BattleAt(MiddleRight, Center, new Vector2(-27, 0), medallionArt ? new Vector2(36, 36) : new Vector2(32, 32));
                if (!medallionArt) Img(badge.transform, "Rim", Alpha(UITheme.Gold, 0.9f), art.Ring).rectTransform.Stretch(-5, -5, -5, -5);
                var badgeText = Txt(badge.transform, "Count", "2", 19, UITheme.Gold, TextAlignmentOptions.Center, true);
                badgeText.rectTransform.Stretch(0, 0, 0, 2);
                UiArt.Outline(badgeText);
                chip.gameObject.SetActive(false);
                chips[i] = chip;
                chipGroups[i] = chipGroup;
                backs[i] = back;
                icons[i] = icon;
                iconGlows[i] = iconGlow;
                labels[i] = label;
                badges[i] = badge.gameObject;
                badgeTexts[i] = badgeText;
                edges[i] = edge;
            }
            var more = Txt(banner, "More", "+1", 26, UITheme.SubText, TextAlignmentOptions.Center, true);
            more.rectTransform.BattleAt(Center, Center, new Vector2(0, -17), new Vector2(60, 40));
            more.gameObject.SetActive(false);

            new Binder(view)
                .Set("_root", banner)
                .Set("_group", group)
                .Set("_title", title)
                .Set("_glow", glow)
                .Set("_sweep", sweep)
                .SetArray("_chips", chips)
                .SetArray("_chipGroups", chipGroups)
                .SetArray("_chipBacks", backs)
                .SetArray("_chipIcons", icons)
                .SetArray("_chipIconGlows", iconGlows)
                .SetArray("_chipLabels", labels)
                .SetArray("_chipBadges", badges)
                .SetArray("_chipBadgeTexts", badgeTexts)
                .SetArray("_chipEdges", edges)
                .Set("_more", more)
                .Set("_library", library)
                .Apply();
            SetBool(view, "_chipBackIsArt", UiArt.IsArt(UiArt.PillNight9s));
            return view;
        }

        // ───────────── 헬퍼 ─────────────

        static Vector2 RefWorldToField(Vector2 world) => RefOrigin + new Vector2(world.x * RefPpu, world.y * RefPpu * RefSquash);

        /// <summary>보호막 하늘색 (대체 거품 그림에 입히는 색)</summary>
        static readonly Color ShieldTint = new(0.75f, 0.89f, 1f, 1f);
        /// <summary>분노 붉은 기운</summary>
        static readonly Color EnrageTint = new(1f, 0.42f, 0.52f, 1f);
        /// <summary>지속 피해 불꽃</summary>
        static readonly Color FlameTint = new(1f, 0.72f, 0.42f, 1f);

        /// <summary>부모 크기 비율로 늘어나는 배치 (offset 0)</summary>
        static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = Center;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>앵커와 pivot을 따로 지정하는 고정 크기 배치</summary>
        static RectTransform BattleAt(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// Filled 이미지 (방사 · 세로 채움). 스프라이트가 없으면 UIKit.WhiteSprite를 넣는다 (M02 — 스프라이트 없는 Filled는 fillAmount를 무시해
        /// 늘 가득 찬 채로 그려진다: 등불 게이지 등이 줄지 않던 까닭)
        /// </summary>
        // ───────────── 4차 (RM-2 P4-3b) ─────────────

        /// <summary>
        /// 패배 분석 고리 (CR-01 §7.2): 전투 화면 TopLeft(24,−310) — 고리 200 (방향 5칸 Filled Radial360, 칸 72° − 틈 4°, 0번이 위 · 반시계) + 가운데 '%' · '!' +
        /// 오른쪽 설명 판 280×200 (밤하늘 9-slice 0.92) 제목 28 · 자리 24 · 조언 20(두 줄) · 링크 '편성 바꾸기 ›' / '바로 다시 도전 ›' 26 (누름 88)
        /// </summary>
        static DefeatAnalysisView BuildDefeatAnalysis(RectTransform root, BattleArt art, SkillArtSet skillArt)
        {
            var area = NewRect("DefeatAnalysis", root).At(TopLeft, new Vector2(24, -310), new Vector2(480, 200));
            var group = area.gameObject.AddComponent<CanvasGroup>();
            var view = area.gameObject.AddComponent<DefeatAnalysisView>();
            var ring = Img(area, "Ring", Alpha(UITheme.Background, 0.55f), CircleSprite, true);
            ring.rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(200, 200));
            var ringButton = ring.gameObject.AddComponent<Button>();
            ringButton.targetGraphic = ring;
            ringButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var segments = new Object[DefeatAnalyzer.Slots];
            for (int i = 0; i < segments.Length; i++)
            {
                var seg = Filled(Img(ring.transform, $"Segment{i}", Alpha(UITheme.SubText, 0.55f), UiArt.RingStitch), Image.FillMethod.Radial360,
                    (int)Image.Origin360.Top, false);
                seg.fillAmount = (72f - 4f) / 360f;
                seg.rectTransform.At(Center, Vector2.zero, new Vector2(184, 184));
                // 칸 i는 위(0)에서 반시계로 i × 72° — 칸 가운데가 그 방향에 오도록 반쪽(34°)만큼 돌려 시작한다
                seg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 72f - 34f);
                segments[i] = seg;
            }
            var hole = Img(ring.transform, "Hole", Alpha(UITheme.Background, 0.9f), CircleSprite);
            hole.rectTransform.At(Center, Vector2.zero, new Vector2(118, 118));
            var exclaim = Img(ring.transform, "Exclaim", Color.white, skillArt.Motif(VfxMotif.Exclaim));
            exclaim.preserveAspect = true;
            exclaim.rectTransform.At(Center, new Vector2(0, 26), new Vector2(40, 40));
            var percent = Txt(ring.transform, "Percent", "0%", 32, UITheme.Cream, TextAlignmentOptions.Center, true).Outlined();
            percent.rectTransform.At(Center, new Vector2(0, -16), new Vector2(110, 40));

            var plate = Panel(area, "Caption", art, 1.6f);
            plate.color = Alpha(plate.color, 0.92f);
            plate.rectTransform.At(MiddleLeft, new Vector2(200, 0), new Vector2(280, 200));
            var title = Txt(plate.transform, "Title", "위쪽이 많이 뚫렸어요", 28, UITheme.Cream, TextAlignmentOptions.TopLeft, true).FitFloor(UIType.AutoSizeFloor, 28f);
            title.rectTransform.Top(12, 34, 14, 10);
            var slotLine = Txt(plate.transform, "Slot", "빈 자리", 24, UITheme.SubText, TextAlignmentOptions.TopLeft).FitFloor(UIType.AutoSizeFloor, 24f);
            slotLine.rectTransform.Top(48, 28, 14, 10);
            var advice = Txt(plate.transform, "Advice", "그 방향에 지킴이를 세우거나 강화해 보세요", 20, UITheme.Lavender, TextAlignmentOptions.TopLeft, false, true)
                .FitFloor(UIType.AutoSizeFloor, 20f);
            advice.rectTransform.Top(78, 52, 14, 10);
            var formation = Btn(plate.transform, "FormationLink", "편성 바꾸기 ›", Alpha(UITheme.Panel, 0f), 26, out var formationLabel);
            formationLabel.color = UITheme.Gold;
            formationLabel.FitFloor(UIType.AutoSizeFloor, 26f);
            formation.GetComponent<RectTransform>().At(TopLeft, new Vector2(8, -134), new Vector2(128, 56));
            ((Image)formation.targetGraphic).HitPad(new Vector2(128, 56), 128f, UISize.MinHit, 4f, 4f, 99f, 99f);
            var retry = Btn(plate.transform, "RetryLink", "바로 다시 도전 ›", Alpha(UITheme.Panel, 0f), 26, out var retryLabel);
            retryLabel.color = UITheme.Cream;
            retryLabel.FitFloor(UIType.AutoSizeFloor, 26f);
            retry.GetComponent<RectTransform>().At(TopLeft, new Vector2(138, -134), new Vector2(136, 56));
            ((Image)retry.targetGraphic).HitPad(new Vector2(136, 56), 136f, UISize.MinHit, 4f, 4f, 99f, 99f);

            new Binder(view)
                .Set("_group", group)
                .SetArray("_segments", segments)
                .Set("_percent", percent)
                .Set("_title", title)
                .Set("_slotLine", slotLine)
                .Set("_advice", advice)
                .Set("_ringButton", ringButton)
                .Set("_formationLink", formation)
                .Set("_retryLink", retry)
                .Apply();
            area.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// 길 잃은 반딧불 (NEW-03 §13.8 — 확장): 늘 켜진 빈 틀 'StrayFirefly'(누름을 받는 컴포넌트) 아래 꺼진 'Firefly' — 120 빛무리(glow_soft) + 56 금빛 반딧불 +
        /// 정전용 2px 크림 고리 + 120×120 투명 누름 자리 (그 그림만 입력). 전투 화면 위 가운데 기준 (반딧불 뷰가 y −250~−400을 고른다)
        /// </summary>
        static StrayFireflyView BuildStrayFirefly(RectTransform root, SummonAimView aim, BattleArt art)
        {
            var holder = NewRect("StrayFirefly", root).At(TopCenter, Vector2.zero, new Vector2(1080, 0));
            var view = holder.gameObject.AddComponent<StrayFireflyView>();
            var fly = NewRect("Firefly", holder).At(Center, Vector2.zero, new Vector2(120, 120));
            var group = fly.gameObject.AddComponent<CanvasGroup>();
            var glow = Img(fly, "Glow", Alpha(UITheme.Gold, 0.55f), art.Glow);
            glow.rectTransform.At(Center, Vector2.zero, new Vector2(120, 120));
            var ring = Img(fly, "Ring", Alpha(UITheme.Cream, 0.9f), UiArt.RingStitch);
            ring.rectTransform.At(Center, Vector2.zero, new Vector2(64, 64));
            ring.enabled = false;
            var body = Img(fly, "Body", UITheme.Gold, ArtLib.FX("fx_mote_firefly") ?? art.Glow);
            body.preserveAspect = true;
            body.rectTransform.At(Center, Vector2.zero, new Vector2(56, 56));
            var hit = Img(fly, "Hit", Alpha(Color.white, 0f), null, true);
            hit.canvasRenderer.cullTransparentMesh = true;
            hit.rectTransform.At(Center, Vector2.zero, new Vector2(120, 120));
            fly.gameObject.SetActive(false);
            new Binder(view).Set("_root", fly).Set("_group", group).Set("_glow", glow).Set("_ring", ring).Set("_hit", hit).Set("_aim", aim).Apply();
            return view;
        }

        /// <summary>
        /// 친구들의 응원 칩 (PRG-03 §13.2): TopRight(−132,−310) 112×48 — 밤하늘 알약 + 짝짝이 40 + 'm:ss' 22 굵게 (누름 88).
        /// SPEC4의 (−96,−310)은 IA §3.3 R1 칸(StatsButton 964–1068 × 436–552)과 겹쳐(872–984) 왼쪽으로 36 옮겼다 — 화면 836–948 × 440–488, R1과 16px (V2 R6)
        /// </summary>
        static CheerChipView BuildCheerChip(RectTransform root)
        {
            var chipRoot = NewRect("CheerChip", root).At(TopRight, new Vector2(CheerChipView.AnchorX, CheerChipView.AnchorY), new Vector2(112, 48));
            var view = chipRoot.gameObject.AddComponent<CheerChipView>();
            var back = Pill(chipRoot, "Back", 48f, Alpha(UITheme.Panel, 0.92f), true);
            back.rectTransform.Stretch();
            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            back.HitPad(new Vector2(112, 48), 112f, UISize.MinHit, 4f, 4f, 99f, 99f);
            var icon = Img(chipRoot, "Icon", Color.white, ArtLib.Costume("jjak_cheer"));
            icon.preserveAspect = true;
            icon.rectTransform.At(MiddleLeft, new Vector2(4, 0), new Vector2(40, 40));
            var time = Txt(chipRoot, "Time", "10:00", 22, UITheme.Cream, TextAlignmentOptions.MidlineRight, true).FitFloor(UIType.AutoSizeFloor, 22f);
            time.rectTransform.Stretch(44, 10, 0, 0);
            new Binder(view).Set("_root", chipRoot).Set("_time", time).Set("_button", button).Apply();
            return view;
        }

        /// <summary>
        /// 불 밝히기 방식 고르기 (CR-02 §7.3): 버튼 위 밤하늘 판 LampPolicyPicker.WindowWidth(440)×280 (줄 424×88 × 3: 아이콘 48 + 제목 28 + 설명 20,
        /// 고른 줄 금테 2px + 체크 28 — 설명 칸 306이라 가장 긴 설명도 16px 하한 안에서 칸에 든다) + 판 밖 투명 덮개,
        /// 버튼 오른쪽 위 32 아이콘(40 둥근 받침). 고르기 컴포넌트는 버튼(LampButton)에 붙는다 (길게 누르기).
        /// 층(LampPicker)은 여기서는 Battle의 중간 형제지만 열 때 LampPolicyPicker가 맨 뒤 형제로 올린다 — 메타 HUD(빠른 아이콘 줄)가 나중에 뒤 형제로 붙기 때문 (검증 rm2 r1)
        /// </summary>
        static void BuildLampPicker(RectTransform root, LampButtonView lamp, BattleArt art)
        {
            if (lamp == null) return;
            var lampRoot = (RectTransform)lamp.transform;
            var picker = lampRoot.gameObject.AddComponent<LampPolicyPicker>();
            new Binder(lamp).Set("_picker", picker).Apply();

            var layer = NewRect("LampPicker", root).Stretch();
            var catcher = Img(layer, "Catcher", Alpha(Color.black, 0f), null, true);
            catcher.rectTransform.Stretch();
            var catcherButton = catcher.gameObject.AddComponent<Button>();
            catcherButton.targetGraphic = catcher;
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var panel = Panel(layer, "Panel", art, 1.6f, true);
            panel.rectTransform.At(BottomRight, new Vector2(-10, 10 + 136 + 8), new Vector2(LampPolicyPicker.WindowWidth, LampPolicyPicker.WindowHeight));
            const float rowWidth = LampPolicyPicker.WindowWidth - 2f * LampPolicyPicker.WindowPad;
            Sprite[] icons = { ArtLib.Icon("auto"), ArtLib.Enemy("boss_shadow"), ArtLib.UI("tutorial_finger") };
            var rows = new Object[3];
            var outlines = new Object[3];
            var checks = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Img(panel.transform, $"Row{i}", Alpha(UITheme.Panel, 0.01f), RoundSprite, true);
                row.rectTransform.At(TopCenter, new Vector2(0, -LampPolicyPicker.WindowPad - i * LampPolicyPicker.RowHeight),
                    new Vector2(rowWidth, LampPolicyPicker.RowHeight));
                var rowButton = row.gameObject.AddComponent<Button>();
                rowButton.targetGraphic = row;
                rowButton.navigation = new Navigation { mode = Navigation.Mode.None };
                var outline = Img(row.transform, "Outline", UITheme.Gold, UiArt.SlotFrame9s);
                outline.type = Image.Type.Sliced;
                outline.rectTransform.Stretch(2, 2, 2, 2);
                outline.enabled = false;
                var icon = Img(row.transform, "Icon", Color.white, icons[i]);
                icon.preserveAspect = true;
                icon.rectTransform.At(MiddleLeft, new Vector2(14, 0), new Vector2(48, 48));
                var name = Txt(row.transform, "Title", LampPolicyPicker.Names[i], 28, UITheme.Cream, TextAlignmentOptions.TopLeft, true)
                    .FitFloor(UIType.AutoSizeFloor, 28f);
                name.rectTransform.Top(10, 34, LampPolicyPicker.DescLeft, LampPolicyPicker.DescRight);
                var desc = Txt(row.transform, "Desc", LampPolicyPicker.Descriptions[i], 20, UITheme.SubText, TextAlignmentOptions.TopLeft)
                    .FitFloor(UIType.AutoSizeFloor, 20f);
                desc.rectTransform.Top(46, 30, LampPolicyPicker.DescLeft, LampPolicyPicker.DescRight);
                var check = Img(row.transform, "Check", Color.white, ArtLib.Icon("check"));
                check.preserveAspect = true;
                check.rectTransform.At(MiddleRight, new Vector2(-12, 0), new Vector2(28, 28));
                check.enabled = false;
                rows[i] = rowButton;
                outlines[i] = outline;
                checks[i] = check;
            }
            layer.gameObject.SetActive(false);

            var corner = Img(lampRoot, "PolicyCorner", Color.white, UiArt.PlateRound);
            corner.rectTransform.At(TopRight, new Vector2(-6, -6), new Vector2(40, 40));
            var cornerIcon = Img(corner.transform, "Icon", Color.white, icons[1]);
            cornerIcon.preserveAspect = true;
            cornerIcon.rectTransform.At(Center, Vector2.zero, new Vector2(32, 32));
            corner.gameObject.SetActive(false);

            new Binder(picker)
                .Set("_panel", layer.gameObject)
                .Set("_window", panel.rectTransform)
                .Set("_catcher", catcherButton)
                .SetArray("_rows", rows)
                .SetArray("_outlines", outlines)
                .SetArray("_checks", checks)
                .Set("_corner", corner.gameObject)
                .Set("_cornerIcon", cornerIcon)
                .SetArray("_cornerSprites", icons[0], icons[1], icons[2])
                .Apply();
        }

        /// <summary>쓰다듬기 (CR-10 §6.3): 지킴이 몸 그림 × 0.8의 투명 PetHit (채워졌을 때만 입력) + 40px 하트 (Icons/affinity)</summary>
        static void BuildPetHits(GuardianSlotView[] guardians, SkillCalloutView callouts, SummonAimView aim)
        {
            if (guardians == null) return;
            var heartSprite = ArtLib.Icon("affinity");
            foreach (var guardian in guardians)
            {
                if (guardian == null) continue;
                var actor = (RectTransform)guardian.transform;
                var hit = Img(actor, "PetHit", Alpha(Color.white, 0f), null, false);
                // 투명 그림은 그리지 않고 누름만 받는다
                hit.canvasRenderer.cullTransparentMesh = true;
                hit.rectTransform.At(BottomCenter, new Vector2(0, GuardianSize * 0.1f), new Vector2(GuardianSize * 0.8f, GuardianSize * 0.8f));
                var heart = Img(hit.transform, "Heart", Color.white, heartSprite);
                heart.preserveAspect = true;
                heart.rectTransform.At(TopCenter, new Vector2(0, 0), new Vector2(40, 40));
                heart.enabled = false;
                var pet = hit.gameObject.AddComponent<GuardianPetView>();
                new Binder(pet).Set("_guardian", guardian).Set("_callouts", callouts).Set("_aim", aim).Set("_heart", heart).Apply();
                new Binder(guardian).Set("_petHit", hit).Apply();
            }
        }

        /// <summary>
        /// 전투 기록 창 (CR-03 §7.4): BuildPopup 900×900 '전투 기록' — [지난 60초][이번 스테이지] + 줄 820×64 × 11
        /// (초상 56 + 틀 · 이름 24 + 지/잔 칩 · 피해 막대 360×18 + 짧은 숫자 · 회복 / 보호막 작은 막대 140×10 · '처치 {n}' 22) + 왕관 · 빈 글
        /// </summary>
        static void BuildStatsPopup(SceneBuildContext ctx)
        {
            var popup = SceneParts.BuildPopup<BattleStatsPopup>(ctx.PopupRoot, "BattleStatsPopup", "전투 기록", new Vector2(900, 900), out var body, out _);
            var tabs = NewRect("Tabs", body).Top(0, 88);
            var windowTab = Btn(tabs, "WindowTab", "지난 60초", UITheme.TabSelected, 26, out var windowLabel);
            windowLabel.color = UITheme.Cream;
            windowTab.GetComponent<RectTransform>().At(TopLeft, new Vector2(0, 0), new Vector2(380, 88));
            var stageTab = Btn(tabs, "StageTab", "이번 스테이지", UITheme.TabNormal, 26, out var stageLabel);
            stageLabel.color = UITheme.Cream;
            stageTab.GetComponent<RectTransform>().At(TopRight, new Vector2(0, 0), new Vector2(380, 88));

            var list = NewRect("Rows", body).Stretch(0, 0, 100, 0);
            var rows = new Object[BattleStatsPopup.Rows];
            var portraits = new Object[rows.Length];
            var frames = new Object[rows.Length];
            var names = new Object[rows.Length];
            var roles = new Object[rows.Length];
            var bars = new Object[rows.Length];
            var values = new Object[rows.Length];
            var heals = new Object[rows.Length];
            var shields = new Object[rows.Length];
            var kills = new Object[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = NewRect($"Row{i}", list).At(TopCenter, new Vector2(0, -i * 64), new Vector2(820, 60));
                var frame = Img(row, "Frame", Color.white, UiArt.SlotFrame9s);
                frame.type = Image.Type.Sliced;
                frame.rectTransform.At(MiddleLeft, Vector2.zero, new Vector2(56, 56));
                var portrait = Img(frame.transform, "Portrait", Color.white);
                portrait.preserveAspect = true;
                portrait.rectTransform.Stretch(4, 4, 4, 4);
                var name = Txt(row, "Name", "친구", 24, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).Ellipsis().FitFloor(UIType.AutoSizeFloor, 24f);
                name.rectTransform.At(MiddleLeft, new Vector2(66, 12), new Vector2(170, 30));
                var role = Txt(row, "Role", "지", 18, UITheme.Lavender, TextAlignmentOptions.MidlineLeft, true);
                role.rectTransform.At(MiddleLeft, new Vector2(66, -14), new Vector2(40, 22));
                var track = Img(row, "DamageTrack", Alpha(UITheme.Background, 0.6f), RoundSprite);
                track.rectTransform.At(MiddleLeft, new Vector2(244, 8), new Vector2(360, 18));
                var bar = Filled(Img(track.transform, "Fill", UITheme.Lavender), Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left, true);
                bar.rectTransform.Stretch();
                var value = Txt(row, "Value", "0", 24, UITheme.Cream, TextAlignmentOptions.MidlineLeft, true).FitFloor(UIType.AutoSizeFloor, 24f);
                value.rectTransform.At(MiddleLeft, new Vector2(612, 8), new Vector2(100, 30));
                var heal = Filled(Img(row, "Heal", UITheme.Positive), Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left, true);
                heal.rectTransform.At(MiddleLeft, new Vector2(244, -16), new Vector2(140, 10));
                heal.fillAmount = 0f;
                var shield = Filled(Img(row, "Shield", UITheme.SoftBlue), Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left, true);
                shield.rectTransform.At(MiddleLeft, new Vector2(392, -16), new Vector2(140, 10));
                shield.fillAmount = 0f;
                var kill = Txt(row, "Kills", "처치 0", 22, UITheme.SubText, TextAlignmentOptions.MidlineRight).FitFloor(UIType.AutoSizeFloor, 22f);
                kill.rectTransform.At(MiddleRight, new Vector2(-4, 0), new Vector2(100, 30));
                row.gameObject.SetActive(false);
                rows[i] = row;
                portraits[i] = portrait;
                frames[i] = frame;
                names[i] = name;
                roles[i] = role;
                bars[i] = bar;
                values[i] = value;
                heals[i] = heal;
                shields[i] = shield;
                kills[i] = kill;
            }
            var crown = Img(list, "Crown", Color.white, ArtLib.Icon("skill_buff_crown"));
            crown.preserveAspect = true;
            crown.rectTransform.At(TopLeft, new Vector2(36, 6), new Vector2(32, 32));
            crown.gameObject.SetActive(false);
            var empty = Txt(list, "Empty", BattleStatsPopup.EmptyText, 26, UITheme.SubText, TextAlignmentOptions.Center, false, true);
            empty.rectTransform.Stretch(40, 40, 200, 200);

            new Binder(popup)
                .Set("_windowTab", windowTab)
                .Set("_stageTab", stageTab)
                .Set("_windowTabBack", windowTab.targetGraphic)
                .Set("_stageTabBack", stageTab.targetGraphic)
                .SetArray("_rows", rows)
                .SetArray("_portraits", portraits)
                .SetArray("_portraitFrames", frames)
                .SetArray("_names", names)
                .SetArray("_roles", roles)
                .SetArray("_damageBars", bars)
                .SetArray("_damageTexts", values)
                .SetArray("_healBars", heals)
                .SetArray("_shieldBars", shields)
                .SetArray("_kills", kills)
                .Set("_crown", crown)
                .Set("_empty", empty)
                .Apply();
            ctx.Popups.Add(popup);
        }

        static Image Filled(Image image, Image.FillMethod method, int origin, bool clockwise)
        {
            if (image.sprite == null) image.sprite = UIKit.WhiteSprite;
            image.type = Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = origin;
            image.fillClockwise = clockwise;
            image.fillAmount = 1f;
            return image;
        }

        /// <summary>
        /// 밤하늘 창 틀 (아트가 있으면 금테 9-slice panel_night_9s, 없으면 둥근 사각형). 기본은 클릭 통과.
        /// ppu = 9-slice 배율 (그려지는 테두리 = 48 / ppu): 큰 판 1.6, 54px 줄 2.2, 44px 칩 3
        /// </summary>
        static Image Panel(RectTransform parent, string name, BattleArt art, float ppu = 1.6f, bool raycast = false)
        {
            if (art.Panel == null) return Img(parent, name, Alpha(UITheme.Panel, 0.95f), RoundSprite, raycast);
            var image = Img(parent, name, Color.white, art.Panel, raycast);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ppu;
            return image;
        }

        /// <summary>어두운 알약 (ui_pill_night_9s 흰색 그대로, 캡슐 배율 = 96 / 높이). 아트가 없으면 예전 둥근 사각형 + fallback 색</summary>
        static Image Pill(Transform parent, string name, float height, Color fallback, bool raycast = false) =>
            UiArt.Panel(parent, name, UiArt.PillNight9s, UiArt.CapsulePpu(height), null, fallback, raycast);

        /// <summary>
        /// 가로 막대: 틀(ui_bar_frame_9s) + 채움(ui_bar_fill_9s) 둘 다 Sliced (UiArt.Bar). 채움 폭은 anchorMax.x로 정한다 (BattleEase.SetBar) —
        /// 둥근 그림에 Filled를 쓰면 끝이 렌즈 모양으로 깎인다. trailTint가 있으면 채움 바로 뒤에 같은 모양의 잔상(Trail)을 깐다.
        /// 처음엔 ratio만큼 찬 상태. 반환값은 틀 (크기·위치는 호출하는 쪽에서).
        /// </summary>
        static Image Bar(Transform parent, string name, float height, Color fillTint, float ratio, out Image fill, out Image trail, Color? trailTint = null)
        {
            var track = UiArt.Bar(parent, name, fillTint, out var fillRect, height);
            fill = fillRect.GetComponent<Image>();
            trail = null;
            if (trailTint.HasValue)
            {
                trail = Object.Instantiate(fill, track.transform);
                trail.name = "Trail";
                trail.color = trailTint.Value;
                trail.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());
                SetBarRatio(trail, ratio);
            }
            SetBarRatio(fill, ratio);
            return track;
        }

        /// <summary>막대 채움의 처음 비율 (anchorMax.x, 0이면 끈다)</summary>
        static void SetBarRatio(Image fill, float ratio)
        {
            var rect = fill.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), rect.anchorMax.y);
            fill.enabled = ratio > 0.001f;
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 아트 아이콘 하나 (비율 유지, 흰색, 터치 안 받음). 아트가 없으면 null — 호출하는 쪽이 예전 자리표시를 그린다
        /// </summary>
        static Image ArtIcon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, float size)
        {
            if (!UiArt.IsArt(sprite)) return null;
            return UiArt.Icon(parent, name, sprite, anchor, position, size);
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector2(Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
            prop.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetColor(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
            prop.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 전투 화면 스프라이트 묶음. Art/ 아래 힉스필드 아트가 있으면 쓰고, 없으면 BattleArtBaker가 그린 자리표시를 쓴다.
    ///   반짝임·꼬리·실 고리·뭉게구름·꺾쇠·빛살 = Art/FX/fx_*, rays + UI/ui_chevron (흰색, 색은 코드로).
    ///   circle_soft는 그림자에만, glow_soft는 빛 번짐에만. (비네트·부채꼴 원판은 모양이 없는 빛이라 절차적)
    /// </summary>
    internal sealed class BattleArt
    {
        /// <summary>링 스프라이트에서 고리가 지나는 반지름 비율 (자리표시 기준)</summary>
        public static float RingRadius = 0.84f;

        /// <summary>방위 슬롯 색 이름 (UITheme.SlotColor 순서) → Battle/guardian_pad_{이름}</summary>
        static readonly string[] SlotPadNames = { "pink", "mint", "lavender", "gold", "sky" };

        /// <summary>
        /// 세로로 늘린 바닥 그림 Battle/field_tall (UX-SPEC-IA §1.4 · §9.0): field.png(1080x810)를 y 619에 그대로 붙이고 위아래를 이어 그린 1080x2048.
        /// 이 크기만 받는다 — 링 값(TallRingCenter / TallRingRadius)이 이 그림 기준이다 (CR-23)
        /// </summary>
        public const string TallFieldName = "field_tall";
        public static readonly Vector2Int TallFieldSize = new(1080, 2048);
        /// <summary>field_tall 속 광장 링 중심 (uv, 왼쪽 아래 = 0) — 잰 값 그대로 (MeasurePlazaRing을 쓰지 않는다)</summary>
        public static readonly Vector2 TallRingCenter = new(0.49954f, 0.50708f);
        /// <summary>field_tall 속 광장 링 반지름 (uv 비율: 가로/그림 폭, 세로/그림 높이) — 328 x 238 px</summary>
        public static readonly Vector2 TallRingRadius = new(0.30370f, 0.11621f);

        public Sprite Glow, Soft, Puff, Ring, Sparkle, Trail, Chevron, Vignette, RangeDisc, Rays;
        public Sprite Field, Shop, Pad;
        /// <summary>방위 색 받침대 (슬롯 순서, 없는 칸은 null) / 빈칸 받침대</summary>
        public Sprite[] SlotPads;
        public Sprite PadEmpty;
        public Sprite Resonance, Lamp, Shield, Lock, Auto, Speed;
        public Sprite CardFrame, Panel, GoldButton;
        public bool FieldIsArt, PadIsArt, ResonanceIsArt, LockIsArt, SpeedIsArt;
        /// <summary>바닥 그림이 세로로 늘린 field_tall(1080x2048)인지 — 링은 TallRingCenter / TallRingRadius로 맞춘다</summary>
        public bool FieldIsTall;
        /// <summary>꺾쇠가 아트(오른쪽을 향함)인지 — 자리표시 꺾쇠는 위를 향한다</summary>
        public bool ChevronIsArt;

        public static BattleArt Load()
        {
            var art = new BattleArt
            {
                Glow = Or(ArtLib.FX("glow_soft"), BattleArtBaker.Glow),
                Soft = Or(ArtLib.FX("circle_soft"), BattleArtBaker.SoftCircle),
                Puff = ArtOr(UiArt.Puff, BattleArtBaker.SoftCircle),
                Sparkle = ArtOr(UiArt.Sparkle, BattleArtBaker.Sparkle),
                Trail = ArtOr(UiArt.Trail, BattleArtBaker.Trail),
                Vignette = BattleArtBaker.Vignette(),
                RangeDisc = BattleArtBaker.RangeDisc(),
                Rays = ArtOr(UiArt.Rays, BattleArtBaker.Rays),
                Field = ArtLib.Battle("field"),
                Shop = Or(ArtLib.Battle("shop"), BattleArtBaker.Shop),
                Pad = ArtLib.Battle("guardian_pad"),
                PadEmpty = ArtLib.Battle("guardian_pad_empty"),
                Resonance = ArtLib.Icon("resonance"),
                Lamp = Or(ArtLib.Icon("lamp"), BattleArtBaker.Lantern),
                Shield = Or(ArtLib.Icon("guardian"), BattleArtBaker.Shield),
                Lock = ArtLib.Icon("lock"),
                Auto = Or(ArtLib.Icon("auto"), BattleArtBaker.Sparkle),
                Speed = Or(ArtLib.Icon("speed"), BattleArtBaker.Chevron),
                CardFrame = ArtLib.UI("card_frame_9s"),
                Panel = ArtLib.UI("panel_night_9s"),
                GoldButton = ArtLib.UI("button_gold_9s"),
            };
            art.SlotPads = new Sprite[SlotPadNames.Length];
            for (int i = 0; i < SlotPadNames.Length; i++) art.SlotPads[i] = ArtLib.Battle("guardian_pad_" + SlotPadNames[i]);

            var chevron = UiArt.Chevron;
            art.ChevronIsArt = UiArt.IsArt(chevron);
            art.Chevron = art.ChevronIsArt ? chevron : BattleArtBaker.Chevron();

            // 링은 아트의 고리 반지름을 재서 방위 링(반지름 1)에 정확히 맞춘다 (실 고리 fx_ring_thread → 예전 ring_glow → 자리표시)
            var ringArt = UiArt.RingThread;
            if (!UiArt.IsArt(ringArt)) ringArt = null;
            art.Ring = ringArt != null ? ringArt : BattleArtBaker.Ring();
            RingRadius = ringArt != null ? BattleArtBaker.MeasureRingRadius(ringArt, UiArt.RingThreadRadius) : 0.84f;

            // 세로로 늘린 바닥 그림이 있으면 그것 (MAIN에서 전투가 세로 전체를 쓴다, UX-SPEC-IA §1.4). 1080x2048만 받는다 —
            // 다른 크기면 링 값이 맞지 않으므로 오류를 남기고 Battle/field.png(링을 잰다)로 (CR-23)
            var tall = ArtLib.Battle(TallFieldName);
            if (tall != null)
            {
                int tallWidth = Mathf.RoundToInt(tall.rect.width), tallHeight = Mathf.RoundToInt(tall.rect.height);
                if (tallWidth == TallFieldSize.x && tallHeight == TallFieldSize.y)
                {
                    art.Field = tall;
                    art.FieldIsTall = true;
                }
                else
                {
                    Debug.LogError($"[BattleSceneBuilder] Battle/{TallFieldName}.png가 {tallWidth}x{tallHeight}입니다 — {TallFieldSize.x}x{TallFieldSize.y}만 받습니다 "
                        + "(링 값이 그 그림 기준). Battle/field.png로 그립니다.");
                }
            }
            art.FieldIsArt = art.Field != null;
            if (!art.FieldIsArt) art.Field = BattleArtBaker.Field();
            art.PadIsArt = art.Pad != null;
            if (!art.PadIsArt) art.Pad = BattleArtBaker.Pad();
            art.ResonanceIsArt = art.Resonance != null;
            if (!art.ResonanceIsArt) art.Resonance = BattleArtBaker.Drop();
            art.LockIsArt = art.Lock != null;
            art.SpeedIsArt = ArtLib.Icon("speed") != null;
            if (!art.LockIsArt) art.Lock = BattleArtBaker.Lock();
            return art;
        }

        static Sprite Or(Sprite sprite, Func<Sprite> fallback) => sprite != null ? sprite : fallback();

        /// <summary>UiArt 조각이 진짜 아트면 그것, 자리표시(UIKit 원·사각형)면 전투용 자리표시를 굽는다</summary>
        static Sprite ArtOr(Sprite sprite, Func<Sprite> fallback) => UiArt.IsArt(sprite) ? sprite : fallback();
    }

    /// <summary>
    /// 스킬 연출용 그림 묶음: 흰색 모양(VfxMotif 순서, Art/VFX/motif_* 힉스필드 아트 — 없으면 절차적) + Art/VFX 스킬 그림(없으면 흰색 모양으로 대체)
    /// + 특성/역할/스테이지 기믹 아이콘 + 기믹 덮개 + 조합 시너지 아이콘. 없는 아트는 null (런타임이 자리표시로 대체).
    /// </summary>
    internal sealed class SkillArtSet
    {
        public Sprite[] Motifs;
        public Sprite[] TraitIcons;
        public Sprite[] StyleIcons;
        public Sprite[] StageIcons;
        public Sprite[] Overlays;

        public Sprite Motif(VfxMotif motif) => Motifs[(int)motif];

        /// <summary>연출 키 그림: Art/VFX/{key}.png, 없으면 키의 대체 모양</summary>
        public Sprite Vfx(string key, out bool isArt)
        {
            var sprite = ArtLib.Vfx(key);
            isArt = sprite != null;
            return isArt ? sprite : Motif(SkillVfxLook.Get(key).Fallback);
        }

        public static SkillArtSet Load(BattleArt art)
        {
            var set = new SkillArtSet { Motifs = new Sprite[SkillVfxLook.MotifCount] };
            set.Motifs[(int)VfxMotif.Sparkle] = art.Sparkle;
            set.Motifs[(int)VfxMotif.Glow] = art.Glow;
            // 눈에 보이는 퍽 입자는 뭉게구름 그림 (circle_soft는 그림자에만)
            set.Motifs[(int)VfxMotif.Soft] = art.Puff;
            set.Motifs[(int)VfxMotif.Ring] = art.Ring;
            set.Motifs[(int)VfxMotif.Trail] = art.Trail;
            // 연출 모양: Art/VFX/motif_{이름} (힉스필드, 흰색 + 옅은 외곽선 — 색은 런타임이 입힌다). 없으면 절차적 모양
            set.Motifs[(int)VfxMotif.Heart] = MotifArt("heart", BattleArtBaker.Heart);
            set.Motifs[(int)VfxMotif.Note] = MotifArt("note", BattleArtBaker.Note);
            set.Motifs[(int)VfxMotif.Drop] = MotifArt("drop", BattleArtBaker.Drop);
            set.Motifs[(int)VfxMotif.Star] = MotifArt("star", BattleArtBaker.Star);
            set.Motifs[(int)VfxMotif.Button] = MotifArt("button", BattleArtBaker.Button);
            set.Motifs[(int)VfxMotif.Crown] = MotifArt("crown", BattleArtBaker.Crown);
            set.Motifs[(int)VfxMotif.Flame] = MotifArt("flame", BattleArtBaker.Flame);
            set.Motifs[(int)VfxMotif.Keyhole] = MotifArt("keyhole", BattleArtBaker.Keyhole);
            set.Motifs[(int)VfxMotif.Shard] = MotifArt("shard", BattleArtBaker.Shard);
            set.Motifs[(int)VfxMotif.MagicCircle] = MotifArt("magic_circle", BattleArtBaker.MagicCircle);
            set.Motifs[(int)VfxMotif.Bubble] = MotifArt("bubble", BattleArtBaker.Bubble);
            set.Motifs[(int)VfxMotif.Swirl] = MotifArt("swirl", BattleArtBaker.Swirl);
            set.Motifs[(int)VfxMotif.DownArrow] = MotifArt("downarrow", BattleArtBaker.DownArrow);
            set.Motifs[(int)VfxMotif.Yarn] = MotifArt("yarn", BattleArtBaker.Yarn);
            set.Motifs[(int)VfxMotif.Gear] = MotifArt("gear", BattleArtBaker.Gear);
            set.Motifs[(int)VfxMotif.Bolt] = MotifArt("bolt", BattleArtBaker.Bolt);
            set.Motifs[(int)VfxMotif.Spring] = MotifArt("spring", BattleArtBaker.Spring);
            set.Motifs[(int)VfxMotif.WindKey] = MotifArt("windkey", BattleArtBaker.WindKey);
            set.Motifs[(int)VfxMotif.Exclaim] = MotifArt("exclaim", BattleArtBaker.Exclaim);
            set.Motifs[(int)VfxMotif.Tear] = MotifArt("tear", BattleArtBaker.Tear);
            set.Motifs[(int)VfxMotif.Splat] = MotifArt("splat", BattleArtBaker.Splat);
            set.Motifs[(int)VfxMotif.HexRing] = MotifArt("hexring", BattleArtBaker.HexRing);
            set.Motifs[(int)VfxMotif.Splinter] = MotifArt("splinter", BattleArtBaker.Splinter);
            set.Motifs[(int)VfxMotif.Scale] = MotifArt("scale", BattleArtBaker.Scale);

            set.TraitIcons = ByEnum<TraitType>(t => ArtLib.Trait(t));
            set.StyleIcons = ByEnum<CombatStyle>(s => ArtLib.Style(s));
            set.StageIcons = ByEnum<StageModifierType>(m => m == StageModifierType.None ? null : ArtLib.StageModifier(m));
            set.Overlays = ByEnum<StageModifierType>(m => m == StageModifierType.None ? null : ArtLib.Vfx(SkillVfx.Overlay(m)));
            return set;
        }

        /// <summary>Art/VFX/motif_{name} (힉스필드 흰색 모양), 없으면 절차적 모양을 굽는다</summary>
        static Sprite MotifArt(string name, Func<Sprite> baked)
        {
            var sprite = ArtLib.Vfx("motif_" + name);
            return sprite != null ? sprite : baked();
        }

        /// <summary>
        /// 배지 크기용 스킬 아이콘 Art/Icons/skill_{키에서 vfx_를 뺀 이름} (id = SkillVfx 키).
        /// 512px 연출 그림을 22~42px로 줄이면 뭉개지므로 SkillVfxLibrary.TryGetSkillIcon이 이것을 먼저 쓴다
        /// </summary>
        public static List<KeyValuePair<string, Sprite>> SkillIcons()
        {
            const string prefix = "vfx_";
            var result = new List<KeyValuePair<string, Sprite>>();
            foreach (string key in SkillVfx.All)
            {
                string name = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
                var sprite = ArtLib.Icon("skill_" + name);
                if (sprite != null) result.Add(new KeyValuePair<string, Sprite>(key, sprite));
            }
            return result;
        }

        /// <summary>Art/Synergy 폴더의 조합 시너지 아이콘 (파일 이름 = 시너지 ID)</summary>
        public static List<KeyValuePair<string, Sprite>> SynergyIcons()
        {
            var result = new List<KeyValuePair<string, Sprite>>();
            string folder = ArtLib.Root + "/Synergy";
            if (!AssetDatabase.IsValidFolder(folder)) return result;
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) result.Add(new KeyValuePair<string, Sprite>(Path.GetFileNameWithoutExtension(path), sprite));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        /// <summary>그림 이름이 이 접두어로 시작하면 기본 공격·적 공격·처치 그림 (Art/VFX)</summary>
        static readonly string[] AttackPrefixes = { "atk_", "enemy_atk_", "enemy_death_", "enemy_hit_" };

        /// <summary>
        /// Art/VFX의 기본 공격(atk_{계보}_shot/hit, atk_slash, atk_snipe_trail) · 적 공격(enemy_atk_{적}) · 처치(enemy_death_{갈래}) ·
        /// 수선점 피격(enemy_hit_shop) 그림. 이름 = 파일 이름, ArtLib.Vfx로 읽는다 (새 그림을 넣으면 다음 씬 생성 때 자동으로 들어간다)
        /// </summary>
        public static List<KeyValuePair<string, Sprite>> AttackArt()
        {
            var result = new List<KeyValuePair<string, Sprite>>();
            string folder = ArtLib.Root + "/VFX";
            if (!AssetDatabase.IsValidFolder(folder)) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (!IsAttackArt(name) || !seen.Add(name)) continue;
                var sprite = ArtLib.Vfx(name);
                if (sprite != null) result.Add(new KeyValuePair<string, Sprite>(name, sprite));
            }
            // ATK-08: 엉킨 실뭉치 처치 그림은 UiArt 접근자로 굽는다 (키 = EnemyFxCatalog.DeathKeyOverride("enemy_tangle") = 파일 이름).
            // 그림이 없는 지금은 null이라 아무것도 넣지 않고 런타임은 먼지 갈래 + 와인 틴트. 접두어 훑기가 같은 파일을 이미 넣었으면 한 번만
            string yarnKey = EnemyFxCatalog.DeathKeyOverride("enemy_tangle");
            var yarn = UiArt.EnemyDeathYarn;
            if (yarn != null && !string.IsNullOrEmpty(yarnKey) && seen.Add(yarnKey)) result.Add(new KeyValuePair<string, Sprite>(yarnKey, yarn));
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        static bool IsAttackArt(string name)
        {
            for (int i = 0; i < AttackPrefixes.Length; i++)
            {
                if (name.StartsWith(AttackPrefixes[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>enum 값(0부터 연속)을 인덱스로 하는 그림 배열</summary>
        static Sprite[] ByEnum<T>(Func<T, Sprite> load) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int size = 0;
            foreach (var value in values) size = Mathf.Max(size, Convert.ToInt32(value) + 1);
            var sprites = new Sprite[size];
            foreach (var value in values) sprites[Convert.ToInt32(value)] = load(value);
            return sprites;
        }
    }

    /// <summary>
    /// 아트가 없을 때 쓰는 전투 화면 스프라이트를 코드로 그린다 (Assets/_Project/Sprites/Battle/*.png, 이미 있으면 재사용).
    /// 흰색 + 알파로 그려 Image.color로 색을 입히는 것이 기본이고, 바닥/수선점/등불만 색을 가진다.
    /// </summary>
    internal static class BattleArtBaker
    {
        const string Dir = IdleProjectSetup.Root + "/Sprites/Battle";

        static readonly Color Clear = new(1f, 1f, 1f, 0f);

        // ───────────── 연출용 (흰색) ─────────────

        public static Sprite Glow() => Bake("fx_glow", 128, 128, (u, v) =>
        {
            float r = Radius(u, v);
            return White(Mathf.Exp(-r * r * 3.2f) * (1f - Smooth(0.8f, 1f, r)));
        });

        public static Sprite SoftCircle() => Bake("fx_soft_circle", 128, 128, (u, v) => White(1f - Smooth(0.7f, 1f, Radius(u, v))));

        public static Sprite Ring() => Bake("fx_ring", 256, 256, (u, v) =>
        {
            float r = Radius(u, v);
            float d = Mathf.Abs(r - 0.84f);
            float core = 1f - Smooth(0.012f, 0.032f, d);
            float glow = 0.55f * Mathf.Exp(-(d / 0.08f) * (d / 0.08f));
            return White(Mathf.Clamp01(Mathf.Max(core, glow)) * (1f - Smooth(0.97f, 1f, r)));
        });

        public static Sprite Sparkle() => Bake("fx_sparkle", 64, 64, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float star = Mathf.Max(Spike(x, y), Spike(y, x));
            float diagonal = 0.45f * Mathf.Max(Spike((x + y) * 0.7071f * 1.6f, (x - y) * 0.7071f), Spike((x - y) * 0.7071f * 1.6f, (x + y) * 0.7071f));
            float r2 = x * x + y * y;
            return White(Mathf.Clamp01(star + diagonal + 0.8f * Mathf.Exp(-r2 * 14f)));
        });

        public static Sprite Trail() => Bake("fx_trail", 128, 32, (u, v) =>
        {
            // 오른쪽(+x)이 빛나는 머리, 왼쪽으로 가늘게 흐려진다
            float y = (v - 0.5f) * 2f;
            float width = 0.16f + 0.3f * u;
            float body = Mathf.Pow(u, 1.8f) * Mathf.Exp(-(y / width) * (y / width));
            float hx = (u - 0.84f) / 0.08f, hy = y / 0.42f;
            float head = Mathf.Exp(-(hx * hx + hy * hy));
            return White(Mathf.Clamp01(body * 0.9f + head * 0.8f) * (1f - Smooth(0.95f, 1f, u)));
        });

        /// <summary>위를 가리키는 화살촉 (^)</summary>
        public static Sprite Chevron() => Bake("fx_chevron", 64, 48, (u, v) =>
        {
            var p = new Vector2((u - 0.5f) * 2f, (v - 0.5f) * 2f * 0.75f);
            float d = Mathf.Min(Segment(p, new Vector2(-0.68f, -0.32f), new Vector2(0f, 0.36f)),
                Segment(p, new Vector2(0f, 0.36f), new Vector2(0.68f, -0.32f))) - 0.15f;
            return White(Cover(d, 0.05f));
        }, 2);

        public static Sprite Vignette() => Bake("fx_vignette", 128, 128, (u, v) =>
        {
            float x = Mathf.Abs(u - 0.5f) * 2f, y = Mathf.Abs(v - 0.5f) * 2f;
            float r = Mathf.Pow(Mathf.Pow(x, 3f) + Mathf.Pow(y, 3f), 1f / 3f);
            return White(Mathf.Pow(Smooth(0.5f, 1.08f, r), 1.5f));
        });

        /// <summary>부채꼴 요격 범위용 원판: 안쪽은 옅고 가장자리로 갈수록 밝으며 테두리 선이 있다</summary>
        public static Sprite RangeDisc() => Bake("fx_range_disc", 256, 256, (u, v) =>
        {
            float r = Radius(u, v);
            float fill = (0.2f + 0.8f * Mathf.Pow(Smooth(0.3f, 0.96f, r), 1.6f)) * (1f - Smooth(0.965f, 1f, r));
            float rim = 0.6f * Mathf.Exp(-((r - 0.95f) / 0.022f) * ((r - 0.95f) / 0.022f));
            return White(Mathf.Clamp01(fill + rim));
        });

        public static Sprite Rays() => Bake("fx_rays", 256, 256, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float beams = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 12f), 5f);
            float falloff = (1f - Smooth(0.25f, 1f, r)) * Smooth(0.04f, 0.22f, r);
            return White(beams * falloff * 0.9f);
        });

        // ───────────── 아이콘 자리표시 ─────────────

        /// <summary>잔향 방울</summary>
        public static Sprite Drop() => Bake("icon_drop", 64, 64, (u, v) =>
        {
            var p = new Vector2(u, v);
            float body = Circle(p, new Vector2(0.5f, 0.38f), 0.28f);
            float tip = Triangle(p, new Vector2(0.255f, 0.47f), new Vector2(0.745f, 0.47f), new Vector2(0.5f, 0.94f));
            float shape = Mathf.Min(body, tip);
            float shine = Circle(p, new Vector2(0.4f, 0.4f), 0.08f);
            var color = Over(Shade(0.86f, Cover(shape, 0.03f)), Color.white, Cover(shine, 0.05f) * 0.9f);
            return color;
        }, 2);

        /// <summary>지킴이 방패</summary>
        public static Sprite Shield() => Bake("icon_shield", 64, 64, (u, v) =>
        {
            float x = Mathf.Abs(u - 0.5f);
            float halfWidth = v > 0.5f ? 0.34f : 0.34f * Mathf.Sqrt(Mathf.Clamp01((v - 0.08f) / 0.42f));
            float d = Mathf.Max(x - halfWidth, v - 0.88f, 0.08f - v);
            float inner = Mathf.Max(x - (halfWidth - 0.08f), v - 0.8f, 0.17f - v);
            float stripe = Mathf.Max(Mathf.Abs(u - 0.5f) - 0.035f, inner);
            var color = Shade(0.95f, Cover(d, 0.03f));
            color = Over(color, new Color(0.78f, 0.78f, 0.78f, 1f), Cover(inner, 0.03f));
            return Over(color, Color.white, Cover(stripe, 0.03f));
        }, 2);

        public static Sprite Lock() => Bake("icon_lock", 64, 64, (u, v) =>
        {
            var p = new Vector2(u, v);
            float body = Box(p, new Vector2(0.5f, 0.36f), new Vector2(0.27f, 0.21f), 0.06f);
            float shackle = Mathf.Abs(Circle(p, new Vector2(0.5f, 0.6f), 0.165f)) - 0.05f;
            float hole = Mathf.Min(Circle(p, new Vector2(0.5f, 0.4f), 0.05f), Box(p, new Vector2(0.5f, 0.31f), new Vector2(0.02f, 0.06f), 0f));
            var color = Shade(0.9f, Cover(shackle, 0.03f));
            color = Over(color, new Color(1f, 1f, 1f, 1f), Cover(body, 0.03f));
            return Over(color, new Color(0.35f, 0.35f, 0.35f, 1f), Cover(hole, 0.03f));
        }, 2);

        /// <summary>등불 (색 있음)</summary>
        public static Sprite Lantern() => Bake("icon_lantern", 64, 64, (u, v) =>
        {
            var p = new Vector2(u, v);
            var frame = Hex(0x3B2E6E);
            float handle = Mathf.Abs(Circle(p, new Vector2(0.5f, 0.86f), 0.08f)) - 0.022f;
            float cap = Box(p, new Vector2(0.5f, 0.76f), new Vector2(0.2f, 0.045f), 0.02f);
            float glass = Box(p, new Vector2(0.5f, 0.48f), new Vector2(0.19f, 0.24f), 0.07f);
            float glassInner = Box(p, new Vector2(0.5f, 0.48f), new Vector2(0.15f, 0.2f), 0.05f);
            float flame = Ellipse(p, new Vector2(0.5f, 0.46f), new Vector2(0.065f, 0.11f));
            float foot = Box(p, new Vector2(0.5f, 0.2f), new Vector2(0.2f, 0.045f), 0.02f);
            var color = Over(Clear, frame, Cover(handle, 0.03f));
            color = Over(color, frame, Cover(glass, 0.03f));
            color = Over(color, Hex(0xFFD98A), Cover(glassInner, 0.03f));
            color = Over(color, Hex(0xFFF4DC), Cover(flame, 0.03f));
            color = Over(color, frame, Cover(cap, 0.03f));
            return Over(color, frame, Cover(foot, 0.03f));
        }, 2);

        // ───────────── 전장 자리표시 (색 있음) ─────────────

        /// <summary>밤 광장 바닥: 가운데 둥근 자갈 광장, 가장자리는 어두운 밤거리와 따뜻한 창불</summary>
        public static Sprite Field() => Bake("field_fallback", 432, 312, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.57f) * 2f;
            float e = Mathf.Sqrt(x * x + y * y * 1.6f);
            var color = Color.Lerp(Hex(0x3A2E6B), Hex(0x140F29), Smooth(0.15f, 1.2f, e));

            // 자갈: 줄마다 반 칸씩 어긋난 둥근 돌
            float plaza = 1f - Smooth(0.8f, 1.02f, Mathf.Sqrt(x * x / 0.85f + y * y * 1.9f));
            color = Color.Lerp(color, Hex(0x43357A), plaza * 0.35f);
            float cx = u * 24f, cy = v * 24f * 312f / 432f * 1.7f;
            float row = Mathf.Floor(cy);
            cx += (row % 2f) * 0.5f;
            float fx = cx - Mathf.Floor(cx) - 0.5f, fy = cy - row - 0.5f;
            float stone = 1f - Smooth(0.3f, 0.47f, Mathf.Sqrt(fx * fx + fy * fy * 0.9f));
            float noise = Hash(Mathf.Floor(cx), row);
            float shade = plaza * (stone * (0.04f + 0.07f * noise) - (1f - stone) * 0.07f);
            color = new Color(color.r * (1f + shade * 2.2f), color.g * (1f + shade * 2.2f), color.b * (1f + shade * 1.8f), 1f);

            // 가장자리 창불
            float lights = 0f;
            for (int i = 0; i < 12; i++)
            {
                float lx = Hash(i, 3.1f), ly = Hash(i, 7.7f);
                var center = i % 2 == 0 ? new Vector2(lx, 0.86f + ly * 0.1f) : new Vector2(i % 4 == 1 ? lx * 0.12f : 0.88f + lx * 0.12f, 0.25f + ly * 0.6f);
                float dx = (u - center.x) * 432f / 312f, dy = v - center.y;
                lights += Mathf.Exp(-(dx * dx + dy * dy) / 0.0009f) * 0.9f + Mathf.Exp(-(dx * dx + dy * dy) / 0.006f) * 0.18f;
            }
            return Color.Lerp(color, Hex(0xFFD98A), Mathf.Clamp01(lights));
        });

        /// <summary>수선점 자리표시: 보라 지붕의 작은 가게, 따뜻한 창과 문</summary>
        public static Sprite Shop() => Bake("shop_fallback", 256, 256, (u, v) =>
        {
            var p = new Vector2(u, v);
            const float outline = 0.014f;
            const float aa = 0.006f;
            var ink = Hex(0x231A45);
            var color = Over(Clear, new Color(0f, 0f, 0f, 0.35f), Cover(Ellipse(p, new Vector2(0.5f, 0.2f), new Vector2(0.38f, 0.06f)), 0.03f));

            float chimney = Box(p, new Vector2(0.68f, 0.76f), new Vector2(0.035f, 0.08f), 0.005f);
            color = Over(color, ink, Cover(chimney - outline, aa));
            color = Over(color, Hex(0x2D2458), Cover(chimney, aa));

            float body = Box(p, new Vector2(0.5f, 0.39f), new Vector2(0.26f, 0.19f), 0.015f);
            color = Over(color, ink, Cover(body - outline, aa));
            color = Over(color, Hex(0x5A4A93), Cover(body, aa));
            float plank = Mathf.Abs(Mathf.Repeat(v * 22f, 1f) - 0.5f) < 0.06f ? 1f : 0f;
            color = Over(color, Hex(0x4C3E80), Cover(body, aa) * plank * 0.8f);

            float roof = Triangle(p, new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.55f), new Vector2(0.5f, 0.88f));
            color = Over(color, ink, Cover(roof - outline, aa));
            color = Over(color, Hex(0x3B2E6E), Cover(roof, aa));
            float eave = Box(p, new Vector2(0.5f, 0.56f), new Vector2(0.36f, 0.02f), 0.01f);
            color = Over(color, Hex(0x8A6BE0), Cover(eave, aa));

            float door = Mathf.Min(Box(p, new Vector2(0.5f, 0.28f), new Vector2(0.06f, 0.075f), 0f), Circle(p, new Vector2(0.5f, 0.355f), 0.06f));
            color = Over(color, ink, Cover(door - outline, aa));
            color = Over(color, Hex(0xFFD98A), Cover(door, aa));
            foreach (float wx in new[] { 0.35f, 0.65f })
            {
                float window = Box(p, new Vector2(wx, 0.42f), new Vector2(0.055f, 0.05f), 0.01f);
                color = Over(color, ink, Cover(window - outline, aa));
                color = Over(color, Hex(0xFFE7A8), Cover(window, aa));
                float bars = Mathf.Min(Mathf.Abs(u - wx), Mathf.Abs(v - 0.42f)) - 0.006f;
                color = Over(color, Hex(0xE8A95C), Cover(Mathf.Max(bars, window), aa));
            }
            float round = Circle(p, new Vector2(0.5f, 0.68f), 0.048f);
            color = Over(color, ink, Cover(round - outline, aa));
            color = Over(color, Hex(0xFFD98A), Cover(round, aa));
            float star = Circle(p, new Vector2(0.5f, 0.93f), 0.025f);
            return Over(color, Hex(0xF2C46B), Cover(star, aa));
        }, 2);

        /// <summary>방위 받침대: 낮은 원기둥 (밝은 중립색 → 슬롯 색으로 tint)</summary>
        public static Sprite Pad() => Bake("pad_fallback", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var radii = new Vector2(0.45f, 0.21f);
            float top = Ellipse(p, new Vector2(0.5f, 0.56f), radii);
            float bottom = Ellipse(p, new Vector2(0.5f, 0.45f), radii);
            float side = Mathf.Min(bottom, Box(p, new Vector2(0.5f, 0.505f), new Vector2(0.45f, 0.055f), 0f));
            float inset = Ellipse(p, new Vector2(0.5f, 0.57f), new Vector2(0.34f, 0.15f));
            var ink = new Color(0.32f, 0.3f, 0.36f, 1f);
            var color = Over(Clear, ink, Cover(side - 0.02f, 0.012f));
            color = Over(color, new Color(0.66f, 0.64f, 0.7f, 1f), Cover(side, 0.012f));
            color = Over(color, ink, Cover(top - 0.02f, 0.012f));
            color = Over(color, new Color(0.98f, 0.98f, 0.98f, 1f), Cover(top, 0.012f));
            return Over(color, new Color(0.86f, 0.85f, 0.9f, 1f), Cover(inset, 0.012f));
        }, 2);

        // ───────────── 스킬 연출 모양 (흰색 + 회색 외곽, 색은 코드로 입힌다) ─────────────

        /// <summary>통통한 하트 (표식·회복)</summary>
        public static Sprite Heart() => Bake("fx_heart", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            float lobes = Mathf.Min(Circle(p, new Vector2(0.335f, 0.6f), 0.205f), Circle(p, new Vector2(0.665f, 0.6f), 0.205f));
            float tip = Triangle(p, new Vector2(0.14f, 0.54f), new Vector2(0.5f, 0.1f), new Vector2(0.86f, 0.54f));
            float d = SoftMin(lobes, tip, 0.05f);
            var color = Solid(d);
            return Over(color, Color.white, Cover(Ellipse(p, new Vector2(0.33f, 0.66f), new Vector2(0.075f, 0.05f)), 0.02f) * 0.95f);
        }, 2);

        /// <summary>8분음표 ♪ (오르골)</summary>
        public static Sprite Note() => Bake("fx_note", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var q = Rotate(p - new Vector2(0.41f, 0.29f), 22f);
            float head = Ellipse(q, Vector2.zero, new Vector2(0.17f, 0.12f));
            float stem = Box(p, new Vector2(0.555f, 0.55f), new Vector2(0.035f, 0.27f), 0.02f);
            var f = Rotate(p - new Vector2(0.66f, 0.74f), -34f);
            float flag = Box(f, Vector2.zero, new Vector2(0.13f, 0.045f), 0.04f);
            float d = SoftMin(SoftMin(head, stem, 0.03f), flag, 0.03f);
            return Solid(d);
        }, 2);

        /// <summary>둥근 다섯 꼭지 별 (기절·치명타·별 폭발)</summary>
        public static Sprite Star() => Bake("fx_star", 128, 128, (u, v) =>
        {
            var p = new Vector2(u - 0.5f, v - 0.47f);
            float d = Star5(p, 0.4f, 0.48f) - 0.035f;
            var color = Solid(d);
            return Over(color, Color.white, Cover(Circle(new Vector2(u, v), new Vector2(0.43f, 0.55f), 0.06f), 0.02f) * 0.9f);
        }, 2);

        /// <summary>네 구멍 단추 (보호막·회전 단추)</summary>
        public static Sprite Button() => Bake("fx_button", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var c = new Vector2(0.5f, 0.5f);
            var color = Solid(Circle(p, c, 0.42f));
            color = Over(color, new Color(0.84f, 0.84f, 0.84f, 1f), Cover(Mathf.Abs(Circle(p, c, 0.33f)) - 0.014f, 0.012f));
            float holes = Mathf.Min(Mathf.Min(Circle(p, c + new Vector2(-0.09f, 0.09f), 0.052f), Circle(p, c + new Vector2(0.09f, 0.09f), 0.052f)),
                Mathf.Min(Circle(p, c + new Vector2(-0.09f, -0.09f), 0.052f), Circle(p, c + new Vector2(0.09f, -0.09f), 0.052f)));
            return Over(color, new Color(0.55f, 0.55f, 0.55f, 1f), Cover(holes, 0.012f));
        }, 2);

        /// <summary>작은 왕관 (강화)</summary>
        public static Sprite Crown() => Bake("fx_crown", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            float band = Box(p, new Vector2(0.5f, 0.3f), new Vector2(0.33f, 0.09f), 0.04f);
            float left = Triangle(p, new Vector2(0.17f, 0.34f), new Vector2(0.37f, 0.34f), new Vector2(0.22f, 0.7f));
            float middle = Triangle(p, new Vector2(0.35f, 0.34f), new Vector2(0.65f, 0.34f), new Vector2(0.5f, 0.78f));
            float right = Triangle(p, new Vector2(0.63f, 0.34f), new Vector2(0.83f, 0.34f), new Vector2(0.78f, 0.7f));
            float tips = Mathf.Min(Circle(p, new Vector2(0.22f, 0.72f), 0.055f), Mathf.Min(Circle(p, new Vector2(0.5f, 0.81f), 0.06f), Circle(p, new Vector2(0.78f, 0.72f), 0.055f)));
            float d = SoftMin(SoftMin(band, Mathf.Min(left, Mathf.Min(middle, right)), 0.03f), tips, 0.02f);
            var color = Solid(d);
            float gems = Mathf.Min(Circle(p, new Vector2(0.32f, 0.3f), 0.035f), Mathf.Min(Circle(p, new Vector2(0.5f, 0.3f), 0.045f), Circle(p, new Vector2(0.68f, 0.3f), 0.035f)));
            return Over(color, new Color(0.78f, 0.78f, 0.78f, 1f), Cover(gems, 0.012f));
        }, 2);

        /// <summary>촛불 불꽃 (안쪽이 더 밝다)</summary>
        public static Sprite Flame() => Bake("fx_flame", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            float outer = SoftMin(Circle(p, new Vector2(0.5f, 0.34f), 0.26f), Triangle(p, new Vector2(0.26f, 0.42f), new Vector2(0.74f, 0.42f), new Vector2(0.52f, 0.95f)), 0.12f);
            float inner = SoftMin(Circle(p, new Vector2(0.5f, 0.3f), 0.13f), Triangle(p, new Vector2(0.38f, 0.33f), new Vector2(0.62f, 0.33f), new Vector2(0.51f, 0.66f)), 0.07f);
            var color = Over(Shade(0.78f, Cover(outer, 0.015f)), new Color(0.88f, 0.88f, 0.88f, 1f), Cover(outer + 0.03f, 0.015f));
            return Over(color, Color.white, Cover(inner, 0.02f));
        }, 2);

        /// <summary>열쇠구멍 자물쇠판 (잠금·기절)</summary>
        public static Sprite Keyhole() => Bake("fx_keyhole", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var color = Solid(Circle(p, new Vector2(0.5f, 0.5f), 0.44f));
            float head = Circle(p, new Vector2(0.5f, 0.6f), 0.13f);
            float slot = Mathf.Min(Triangle(p, new Vector2(0.4f, 0.2f), new Vector2(0.6f, 0.2f), new Vector2(0.56f, 0.6f)),
                Triangle(p, new Vector2(0.4f, 0.2f), new Vector2(0.56f, 0.6f), new Vector2(0.44f, 0.6f)));
            return Over(color, new Color(0.36f, 0.36f, 0.36f, 1f), Cover(SoftMin(head, slot, 0.02f), 0.012f));
        }, 2);

        /// <summary>유리 조각 (보호막이 깨질 때)</summary>
        public static Sprite Shard() => Bake("fx_shard", 64, 64, (u, v) =>
        {
            var p = new Vector2(u, v);
            float d = Triangle(p, new Vector2(0.16f, 0.2f), new Vector2(0.84f, 0.36f), new Vector2(0.42f, 0.9f)) - 0.02f;
            var color = Over(Shade(0.75f, Cover(d, 0.02f)), new Color(0.92f, 0.92f, 0.92f, 1f), Cover(d + 0.05f, 0.02f));
            float shine = Mathf.Max(Segment(p, new Vector2(0.3f, 0.32f), new Vector2(0.44f, 0.66f)) - 0.025f, d + 0.06f);
            return Over(color, Color.white, Cover(shine, 0.02f));
        }, 2);

        /// <summary>소환 마법진: 이중 고리 + 별 문양 + 고리 위 점 열두 개</summary>
        public static Sprite MagicCircle() => Bake("fx_magic_circle", 256, 256, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float rings = Mathf.Max(Cover(Mathf.Abs(r - 0.9f) - 0.02f, 0.012f), Mathf.Max(Cover(Mathf.Abs(r - 0.72f) - 0.012f, 0.012f), Cover(Mathf.Abs(r - 0.36f) - 0.01f, 0.012f)));
            float step = Mathf.PI / 6f;
            float da = Mathf.Repeat(a + step * 0.5f, step) - step * 0.5f;
            float dot = Mathf.Sqrt((r - 0.81f) * (r - 0.81f) + (da * 0.81f) * (da * 0.81f)) - 0.03f;
            var p = new Vector2(x, y);
            float t1 = Mathf.Abs(Triangle(p, Polar(90f, 0.7f), Polar(210f, 0.7f), Polar(330f, 0.7f))) - 0.012f;
            float t2 = Mathf.Abs(Triangle(p, Polar(270f, 0.7f), Polar(30f, 0.7f), Polar(150f, 0.7f))) - 0.012f;
            float lines = Cover(Mathf.Min(t1, t2), 0.012f) * 0.85f;
            float fill = 0.12f * (1f - Smooth(0.86f, 0.92f, r));
            return White(Mathf.Max(Mathf.Max(rings, Cover(dot, 0.015f)), Mathf.Max(lines, fill)));
        });

        /// <summary>비눗방울 보호막: 부드러운 테 + 옅은 안쪽 + 왼쪽 위 반짝임</summary>
        public static Sprite Bubble() => Bake("fx_bubble", 256, 256, (u, v) =>
        {
            float r = Radius(u, v);
            float rim = 0.9f * Mathf.Exp(-((r - 0.86f) / 0.075f) * ((r - 0.86f) / 0.075f));
            float fill = 0.16f * (1f - Smooth(0.82f, 0.95f, r));
            var q = Rotate(new Vector2(u, v) - new Vector2(0.33f, 0.69f), 40f);
            float highlight = Cover(Ellipse(q, Vector2.zero, new Vector2(0.13f, 0.06f)), 0.03f) * 0.9f;
            float dot = Cover(Circle(new Vector2(u, v), new Vector2(0.24f, 0.56f), 0.03f), 0.02f) * 0.8f;
            return White(Mathf.Clamp01(rim + fill + highlight + dot) * (1f - Smooth(0.95f, 1f, r)));
        });

        /// <summary>두 갈래 소용돌이 (폭풍·리본·끌어당김)</summary>
        public static Sprite Swirl() => Bake("fx_swirl", 256, 256, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float arm = Mathf.Sin(2f * a + r * 9f);
            float band = Smooth(0.45f, 0.95f, arm);
            float fade = Smooth(0.06f, 0.28f, r) * (1f - Smooth(0.78f, 0.98f, r));
            return White(band * fade * (0.45f + 0.55f * r));
        });

        /// <summary>아래를 가리키는 겹화살 (약화)</summary>
        public static Sprite DownArrow() => Bake("fx_down_arrow", 128, 128, (u, v) =>
        {
            var p = new Vector2((u - 0.5f) * 2f, (v - 0.5f) * 2f);
            float upper = Mathf.Min(Segment(p, new Vector2(-0.5f, 0.55f), new Vector2(0f, 0.1f)), Segment(p, new Vector2(0f, 0.1f), new Vector2(0.5f, 0.55f)));
            float lower = Mathf.Min(Segment(p, new Vector2(-0.5f, 0.05f), new Vector2(0f, -0.4f)), Segment(p, new Vector2(0f, -0.4f), new Vector2(0.5f, 0.05f)));
            float d = (Mathf.Min(upper, lower) - 0.13f) * 0.5f;
            return Solid(d);
        }, 2);

        /// <summary>실타래 (감속·분열): 둥근 공 + 감긴 실 결 + 풀린 꼬리</summary>
        public static Sprite Yarn() => Bake("fx_yarn", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var c = new Vector2(0.46f, 0.52f);
            float ball = Circle(p, c, 0.36f);
            float tail = Mathf.Min(Segment(p, new Vector2(0.72f, 0.3f), new Vector2(0.84f, 0.17f)), Segment(p, new Vector2(0.84f, 0.17f), new Vector2(0.95f, 0.22f))) - 0.022f;
            var color = Solid(Mathf.Min(ball, tail));
            var q = p - c;
            float wraps1 = Mathf.Abs(Mathf.Sin((q.x * 0.77f + q.y * 0.64f) * 30f));
            float wraps2 = Mathf.Abs(Mathf.Sin((q.x * -0.5f + q.y * 0.87f) * 30f));
            float lines = q.x > -0.05f ? wraps1 : wraps2;
            float inside = Cover(ball + 0.05f, 0.015f);
            return Over(color, new Color(0.78f, 0.78f, 0.78f, 1f), inside * (1f - Smooth(0.15f, 0.4f, lines)));
        }, 2);

        // ───────────── 적 공격·처치 연출 모양 (ATK-03·04·11, 흰색 + 회색 외곽) ─────────────

        /// <summary>톱니바퀴 여덟 니 + 가운데 구멍 (쇠붙이 처치 파편·태엽 공격)</summary>
        public static Sprite Gear() => Bake("fx_gear", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var c = new Vector2(0.5f, 0.5f);
            float d = Circle(p, c, 0.29f);
            for (int k = 0; k < 8; k++)
            {
                var q = Rotate(p - c, -k * 45f);
                d = SoftMin(d, Box(q, new Vector2(0.33f, 0f), new Vector2(0.08f, 0.065f), 0.025f), 0.02f);
            }
            float hole = Circle(p, c, 0.1f);
            var color = Solid(Mathf.Max(d, -hole));
            float rim = Mathf.Abs(Circle(p, c, 0.2f)) - 0.012f;
            return Over(color, new Color(0.82f, 0.82f, 0.82f, 1f), Cover(Mathf.Max(rim, d + 0.04f), 0.012f));
        }, 2);

        /// <summary>나사: 둥근 머리 + 홈 + 나사산 결이 있는 몸통 (쇠붙이 처치 파편)</summary>
        public static Sprite Bolt() => Bake("fx_bolt", 128, 128, (u, v) =>
        {
            var p = Rotate(new Vector2(u, v) - new Vector2(0.5f, 0.5f), 25f) + new Vector2(0.5f, 0.5f);
            float head = Box(p, new Vector2(0.5f, 0.74f), new Vector2(0.21f, 0.1f), 0.06f);
            float shaft = Box(p, new Vector2(0.5f, 0.42f), new Vector2(0.075f, 0.25f), 0.03f);
            var color = Solid(SoftMin(head, shaft, 0.02f));
            float slot = Box(p, new Vector2(0.5f, 0.77f), new Vector2(0.11f, 0.018f), 0.01f);
            color = Over(color, new Color(0.5f, 0.5f, 0.5f, 1f), Cover(slot, 0.01f));
            float thread = Mathf.Abs(Mathf.Sin((p.y + p.x * 0.35f) * 70f));
            float inShaft = Cover(shaft + 0.02f, 0.01f) * (p.y < 0.62f ? 1f : 0f);
            return Over(color, new Color(0.7f, 0.7f, 0.7f, 1f), inShaft * (1f - Smooth(0.2f, 0.45f, thread)));
        }, 2);

        /// <summary>지그재그 용수철 (태엽쥐 공격·처치 '보잉')</summary>
        public static Sprite Spring() => Bake("fx_spring", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            float d = 10f;
            const int turns = 7;
            for (int i = 0; i < turns; i++)
            {
                float y0 = 0.14f + i * (0.72f / turns);
                float y1 = y0 + 0.72f / turns;
                var a = new Vector2(i % 2 == 0 ? 0.3f : 0.7f, y0);
                var b = new Vector2(i % 2 == 0 ? 0.7f : 0.3f, y1);
                d = Mathf.Min(d, Segment(p, a, b));
            }
            d = Mathf.Min(d, Segment(p, new Vector2(0.3f, 0.14f), new Vector2(0.3f, 0.06f)));
            d = Mathf.Min(d, Segment(p, new Vector2(0.7f, 0.86f), new Vector2(0.7f, 0.94f)));
            return Solid(d - 0.045f, 0.022f);
        }, 2);

        /// <summary>태엽 감개 열쇠: 나비 날개 손잡이 + 가운데 축 + 짧은 꽂이 (태엽쥐가 다른 적을 감아 줄 때)</summary>
        public static Sprite WindKey() => Bake("fx_windkey", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            float left = Ellipse(p, new Vector2(0.29f, 0.64f), new Vector2(0.19f, 0.14f));
            float right = Ellipse(p, new Vector2(0.71f, 0.64f), new Vector2(0.19f, 0.14f));
            float hub = Circle(p, new Vector2(0.5f, 0.6f), 0.09f);
            float shaft = Box(p, new Vector2(0.5f, 0.3f), new Vector2(0.05f, 0.22f), 0.02f);
            float d = SoftMin(SoftMin(left, right, 0.03f), SoftMin(hub, shaft, 0.03f), 0.03f);
            var color = Solid(d);
            float holes = Mathf.Min(Ellipse(p, new Vector2(0.27f, 0.64f), new Vector2(0.08f, 0.055f)), Ellipse(p, new Vector2(0.73f, 0.64f), new Vector2(0.08f, 0.055f)));
            return Over(color, new Color(0.55f, 0.55f, 0.55f, 1f), Cover(holes, 0.012f));
        }, 2);

        /// <summary>느낌표 (뚫린 방향 알림 — 붉은 원 위에 흰색으로)</summary>
        public static Sprite Exclaim() => Bake("fx_exclaim", 64, 128, (u, v) =>
        {
            var p = new Vector2(u, v * 2f);
            float bar = SoftMin(Box(p, new Vector2(0.5f, 1.22f), new Vector2(0.17f, 0.5f), 0.15f),
                Box(p, new Vector2(0.5f, 0.92f), new Vector2(0.11f, 0.3f), 0.1f), 0.08f);
            float dot = Circle(p, new Vector2(0.5f, 0.32f), 0.17f);
            return White(Cover(Mathf.Min(bar, dot), 0.03f));
        }, 2);

        /// <summary>갉아먹힌 천 구멍: 들쭉날쭉한 가장자리 + 어두운 안쪽 (좀나방 '갉음' 스티커)</summary>
        public static Sprite Tear() => Bake("fx_tear", 128, 128, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float edge = 0.78f + 0.08f * Mathf.Sin(a * 9f) + 0.04f * Mathf.Sin(a * 23f + 1.3f);
            float hole = 0.44f + 0.06f * Mathf.Sin(a * 7f + 0.5f);
            var color = Solid((r - edge) * 0.5f, 0.03f);
            return Over(color, new Color(0.42f, 0.42f, 0.42f, 1f), Cover((r - hole) * 0.5f, 0.012f));
        }, 2);

        static readonly float[] SplatAngles = { 20f, 75f, 140f, 200f, 255f, 320f };
        static readonly float[] SplatReach = { 0.3f, 0.34f, 0.29f, 0.33f, 0.3f, 0.35f };
        static readonly float[] SplatSizes = { 0.08f, 0.06f, 0.09f, 0.065f, 0.075f, 0.055f };

        /// <summary>몽글한 얼룩: 가운데 방울 + 튄 방울 여섯 (녹물·녹 얼룩)</summary>
        public static Sprite Splat() => Bake("fx_splat", 128, 128, (u, v) =>
        {
            var p = new Vector2(u, v);
            var c = new Vector2(0.5f, 0.5f);
            float d = Circle(p, c, 0.22f);
            for (int i = 0; i < SplatAngles.Length; i++)
            {
                d = SoftMin(d, Circle(p, c + Polar(SplatAngles[i], SplatReach[i]), SplatSizes[i]), 0.06f);
                d = Mathf.Min(d, Circle(p, c + Polar(SplatAngles[i] + 18f, SplatReach[i] + 0.12f), SplatSizes[i] * 0.45f));
            }
            var color = Solid(d);
            return Over(color, Color.white, Cover(Ellipse(p, new Vector2(0.42f, 0.58f), new Vector2(0.07f, 0.045f)), 0.02f) * 0.9f);
        }, 2);

        /// <summary>육각 물결 고리: 육각 테 + 옅은 안쪽 + 꼭짓점 점 (적 보호막에 막힌 명중)</summary>
        public static Sprite HexRing() => Bake("fx_hex_ring", 256, 256, (u, v) =>
        {
            var p = new Vector2(Mathf.Abs((u - 0.5f) * 2f), Mathf.Abs((v - 0.5f) * 2f));
            float hex = Mathf.Max(p.x * 0.866f + p.y * 0.5f, p.y) - 0.8f;
            float ring = Cover(Mathf.Abs(hex) - 0.035f, 0.02f);
            float inner = Cover(Mathf.Abs(hex + 0.14f) - 0.012f, 0.02f) * 0.55f;
            float fill = hex < 0f ? 0.12f : 0f;
            return White(Mathf.Max(ring, Mathf.Max(inner, fill)));
        });

        /// <summary>나뭇조각: 길쭉한 쐐기 + 나뭇결 (수선점 문이 맞을 때 튀는 파편)</summary>
        public static Sprite Splinter() => Bake("fx_splinter", 128, 64, (u, v) =>
        {
            var p = new Vector2(u, v);
            float d = Triangle(p, new Vector2(0.06f, 0.42f), new Vector2(0.95f, 0.5f), new Vector2(0.1f, 0.72f)) - 0.015f;
            var color = Over(Shade(0.74f, Cover(d, 0.02f)), new Color(0.94f, 0.94f, 0.94f, 1f), Cover(d + 0.05f, 0.02f));
            float grain = Mathf.Abs(Segment(p, new Vector2(0.14f, 0.55f), new Vector2(0.82f, 0.52f))) - 0.012f;
            return Over(color, new Color(0.7f, 0.7f, 0.7f, 1f), Cover(Mathf.Max(grain, d + 0.04f), 0.015f));
        }, 2);

        /// <summary>나방 비늘 가루: 끝이 뾰족한 작은 잎 + 가운데 맥 (날개 가루 휘두르기·나방 처치)</summary>
        public static Sprite Scale() => Bake("fx_scale", 64, 64, (u, v) =>
        {
            var p = new Vector2(u, v);
            float body = SoftMin(Circle(p, new Vector2(0.5f, 0.38f), 0.24f), Triangle(p, new Vector2(0.28f, 0.44f), new Vector2(0.72f, 0.44f), new Vector2(0.5f, 0.94f)), 0.08f);
            var color = Solid(body, 0.05f);
            float vein = Mathf.Max(Segment(p, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.78f)) - 0.02f, body + 0.06f);
            return Over(color, new Color(0.72f, 0.72f, 0.72f, 1f), Cover(vein, 0.02f));
        }, 2);

        // ───────────── 링 반지름 측정 ─────────────

        /// <summary>링 아트의 가로 중앙선에서 가장 밝은 지점으로 고리 반지름 비율을 잰다 (읽을 수 없으면 기본값)</summary>
        /// <summary>
        /// 바닥 그림에 그려진 광장 링(보라색 타원)을 잰다: 가운데 세로줄에서 위/아래로 가장 보라색인 줄,
        /// 그 높이의 가로줄에서 왼/오른쪽으로 가장 보라색인 칸. uv(왼쪽 아래 = 0) 중심과 반지름 비율.
        /// 그럴듯하지 않으면 false.
        /// </summary>
        public static bool MeasurePlazaRing(Sprite sprite, out Vector2 center, out Vector2 radius)
        {
            center = default;
            radius = default;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;
                int w = texture.width, h = texture.height;
                var pixels = texture.GetPixels32();
                float Purple(int x, int y)
                {
                    var c = pixels[y * w + x];
                    return (c.r + c.b) * 0.5f - c.g;
                }

                int ArgMax(int from, int to, Func<int, float> score)
                {
                    int best = from;
                    float bestScore = float.MinValue;
                    for (int i = from; i < to; i++)
                    {
                        float value = score(i);
                        if (value > bestScore)
                        {
                            bestScore = value;
                            best = i;
                        }
                    }
                    return best;
                }

                int cx = w / 2;
                int low = ArgMax(h / 10, h / 2, y => Purple(cx, y));
                int high = ArgMax(h / 2, h - h / 10, y => Purple(cx, y));
                int cy = (low + high) / 2;
                int left = ArgMax(w / 20, w / 2, x => Purple(x, cy));
                int right = ArgMax(w / 2, w - w / 20, x => Purple(x, cy));

                center = new Vector2((left + right + 1) * 0.5f / w, (low + high + 1) * 0.5f / h);
                radius = new Vector2((right - left) * 0.5f / w, (high - low) * 0.5f / h);
                float squash = radius.y * h / Mathf.Max(1f, radius.x * w);
                bool plausible = radius.x > 0.15f && radius.x < 0.48f && squash > 0.45f && squash < 0.95f
                    && Mathf.Abs(center.x - 0.5f) < 0.12f && Mathf.Abs(center.y - 0.5f) < 0.15f;
                return plausible;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static float MeasureRingRadius(Sprite sprite, float fallback)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return fallback;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return fallback;
                int y = texture.height / 2;
                int half = texture.width / 2;
                float best = 0f;
                int bestX = -1;
                for (int x = half; x < texture.width; x++)
                {
                    float a = texture.GetPixel(x, y).a;
                    if (a > best)
                    {
                        best = a;
                        bestX = x;
                    }
                }
                return bestX > half && best > 0.2f ? (bestX - half + 0.5f) / half : fallback;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // ───────────── 굽기 ─────────────

        static Sprite Bake(string name, int width, int height, Func<float, float, Color> shade, int supersample = 1)
        {
            string path = $"{Dir}/{name}.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color[width * height];
                float step = 1f / supersample;
                int samples = supersample * supersample;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        for (int sy = 0; sy < supersample; sy++)
                        {
                            for (int sx = 0; sx < supersample; sx++)
                            {
                                var c = shade((x + (sx + 0.5f) * step) / width, (y + (sy + 0.5f) * step) / height);
                                r += c.r * c.a;
                                g += c.g * c.a;
                                b += c.b * c.a;
                                a += c.a;
                            }
                        }
                        pixels[y * width + x] = a > 0f ? new Color(r / a, g / a, b / a, a / samples) : new Color(1f, 1f, 1f, 0f);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
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

        // ───────────── 도형 함수 (uv 0~1, 부호 있는 거리) ─────────────

        static Color White(float alpha) => new(1f, 1f, 1f, Mathf.Clamp01(alpha));

        static Color Shade(float gray, float alpha) => new(gray, gray, gray, Mathf.Clamp01(alpha));

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        static float Radius(float u, float v)
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            return Mathf.Sqrt(x * x + y * y);
        }

        static float Smooth(float edge0, float edge1, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        /// <summary>부호 있는 거리 → 덮임 정도 (경계에서 aa 폭으로 부드럽게)</summary>
        static float Cover(float distance, float aa) => Mathf.Clamp01(0.5f - distance / aa);

        static float Spike(float along, float across)
        {
            float t = Mathf.Clamp01(1f - Mathf.Abs(along));
            float width = 0.025f + 0.11f * t * t;
            return t * Mathf.Exp(-(across / width) * (across / width));
        }

        /// <summary>회색 외곽선 + 거의 흰 안쪽 (색을 입히면 같은 색의 진한 테두리가 생긴다)</summary>
        static Color Solid(float d, float rim = 0.035f)
        {
            var color = Shade(0.76f, Cover(d, 0.015f));
            return Over(color, new Color(0.97f, 0.97f, 0.97f, 1f), Cover(d + rim, 0.015f));
        }

        /// <summary>부드러운 합집합 (말랑한 모양)</summary>
        static float SoftMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        static Vector2 Polar(float degrees, float radius) => new(Mathf.Cos(degrees * Mathf.Deg2Rad) * radius, Mathf.Sin(degrees * Mathf.Deg2Rad) * radius);

        /// <summary>다섯 꼭지 별 (가운데 기준, r = 바깥 반지름, rf = 안쪽 비율)</summary>
        static float Star5(Vector2 p, float r, float rf)
        {
            var k1 = new Vector2(0.809016994f, -0.587785252f);
            var k2 = new Vector2(-k1.x, k1.y);
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r;
            var ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
        }

        static float Circle(Vector2 p, Vector2 center, float radius) => (p - center).magnitude - radius;

        static float Ellipse(Vector2 p, Vector2 center, Vector2 radii)
        {
            var q = new Vector2((p.x - center.x) / radii.x, (p.y - center.y) / radii.y);
            return (q.magnitude - 1f) * Mathf.Min(radii.x, radii.y);
        }

        static float Box(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            float dx = Mathf.Abs(p.x - center.x) - half.x + radius;
            float dy = Mathf.Abs(p.y - center.y) - half.y + radius;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        /// <summary>반시계 방향 삼각형 (근사 거리)</summary>
        static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Mathf.Max(EdgeOutside(p, a, b), Mathf.Max(EdgeOutside(p, b, c), EdgeOutside(p, c, a)));

        static float EdgeOutside(Vector2 p, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            float cross = edge.x * (p.y - a.y) - edge.y * (p.x - a.x);
            return -cross / edge.magnitude;
        }

        /// <summary>src를 coverage만큼 dst 위에 덮는다 (straight alpha)</summary>
        static Color Over(Color dst, Color src, float coverage)
        {
            float a = src.a * Mathf.Clamp01(coverage);
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return new Color(1f, 1f, 1f, 0f);
            float k = dst.a * (1f - a);
            return new Color((src.r * a + dst.r * k) / outA, (src.g * a + dst.g * k) / outA, (src.b * a + dst.b * k) / outA, outA);
        }

        static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
