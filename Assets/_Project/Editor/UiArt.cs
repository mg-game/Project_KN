using System.Collections.Generic;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 힉스필드로 그린 공용 UI 조각(Art/UI/ui_*, Art/FX/fx_*, 아이콘 일부)과 그것을 입히는 도우미.
    /// 모든 화면 빌더가 같이 쓴다 (이 파일은 메뉴 담당만 고친다 — 다른 빌더는 부르기만).
    ///   - 아트가 없으면 예전 자리표시(UIKit.CircleSprite / RoundSprite)로 대신하고, 이름마다 경고를 한 번 남긴다.
    ///     IsArt(sprite)로 진짜 아트인지 확인해 자리표시일 때만 예전 색을 칠한다.
    ///   - 색 규칙: 색이 있는 그림(배지 · 메달 · 받침 · 버튼 · 패널 · 리본)은 흰색 그대로 (알파 또는 Lerp(white, c, ≤0.35)까지).
    ///     흰색 틴트용 조각(pip · pill_white · ring_stitch · slot_frame · tab · bar_fill · bar_frame · chevron · FX)만 상태 색을 그대로 입힌다.
    ///   - 9-slice 배율(pixelsPerUnitMultiplier): 그려지는 테두리 = 48 / 배율.
    ///     패널 1.6~2, 테두리 덮개 2~2.5, 버튼 높이 90~110px 2 · 60~70px 2.8 · 40~50px 3.5, 캡슐(192x96)은 96 / 높이.
    /// </summary>
    internal static class UiArt
    {
        const string GeneratedDir = IdleProjectSetup.Root + "/Generated";
        const string OutlineMaterialPath = GeneratedDir + "/UiTextOutline.mat";

        /// <summary>경고를 이미 남긴 경로 (한 번만)</summary>
        static readonly HashSet<string> Warned = new();
        static Material _outlineMaterial;

        // ───────────── 둥근 조각 ─────────────

        /// <summary>빨간 알림 배지 (모든 알림 점 · 숫자 배지)</summary>
        public static Sprite BadgeRed => UI("ui_badge_red", CircleSprite);
        /// <summary>흰 단추 점 (단계 점 · 페이지 점 · 눈금 · 단계 수 배지 · 색 견본 · 손잡이 · 순서 표시). 틴트용</summary>
        public static Sprite PipButton => UI("ui_pip_button", CircleSprite);
        /// <summary>밝은 둥근 받침 (초상화 · 아이콘 · 아이템 뒤, 동그란 초상화 마스크). 옅은 틴트만</summary>
        public static Sprite PlateRound => UI("ui_plate_round", CircleSprite);
        /// <summary>어두운 메달 홈 (스킬 보석 · 역할 아이콘 · 잠금 받침). 흰색 그대로</summary>
        public static Sprite MedallionDark => UI("ui_medallion_dark", CircleSprite);
        /// <summary>바느질 고리 (상태 고리 · 체크 고리 · 쿨다운 고리). 틴트용</summary>
        public static Sprite RingStitch => UI("ui_ring_stitch", CircleSprite);
        /// <summary>빈 칸 "+" (바늘 꽂힌 천 십자)</summary>
        public static Sprite SlotPlus => UI("ui_slot_plus", null);
        /// <summary>닫기 단추 (X 바느질)</summary>
        public static Sprite Close => UI("ui_button_close", null);
        /// <summary>말풍선 꼬리 (위쪽이 말풍선에 겹친다)</summary>
        public static Sprite BubbleTail => UI("ui_bubble_tail", null);
        /// <summary>오른쪽 꺾쇠 (돌려서 쓴다). 틴트용</summary>
        public static Sprite Chevron => UI("ui_chevron", null);
        /// <summary>하단 탭 선택 받침 (위가 둥근 보라 판)</summary>
        public static Sprite TabSelected => UI("ui_tab_selected", RoundSprite);
        /// <summary>스킬 구슬 테두리 (가운데 빈 고리)</summary>
        public static Sprite SkillOrbFrame => UI("ui_skill_orb_frame", CircleSprite);
        /// <summary>도장 고리 (가운데 비어 있음)</summary>
        public static Sprite StampRing => UI("ui_stamp_ring", CircleSprite);
        /// <summary>달 (없으면 VFX/vfx_moon_glow)</summary>
        public static Sprite Moon => First("UI/moon.png", "VFX/vfx_moon_glow.png", CircleSprite);

        // ───────────── 화면 틀 (UX-SPEC-IA — 전용 아트는 따로 그리는 중이라 없을 수 있다: 없으면 기존 아트로) ─────────────
        // dedicated = 전용 아트를 받았는지. 대신 받은 아트는 지금 쓰는 방식 그대로 입힌다 (9-slice 배율 · 뒤집기 · 덧그림을 dedicated로 고른다).

        /// <summary>짧은 상단 바 틀: 전용 Art/UI/topbar_9s, 없으면 BarVelvet9s (금 솔기가 아래 — 지금 상단 바 그대로)</summary>
        public static Sprite TopBar9s(out bool dedicated) => Dedicated("topbar_9s", BarVelvet9s, out dedicated);

        /// <summary>7칸 하단 탭 바 틀: 전용 Art/UI/navbar_9s, 없으면 BarVelvet9s (대신 받았으면 세로로 뒤집어 금 솔기를 위로 — 지금 탭 바처럼)</summary>
        public static Sprite NavBar9s(out bool dedicated) => Dedicated("navbar_9s", BarVelvet9s, out dedicated);

        /// <summary>가운데 사냥 버튼 메달 (150px): 전용 Art/UI/nav_main_button, 없으면 MedallionDark</summary>
        public static Sprite NavMainButton(out bool dedicated) => Dedicated("nav_main_button", MedallionDark, out dedicated);

        /// <summary>시트 틀: 전용 Art/UI/sheet_frame_9s, 없으면 PanelNight9s (시트 머리 벨벳은 §4.2대로 BarVelvet9s를 뒤집어 쓴다)</summary>
        public static Sprite SheetFrame9s(out bool dedicated) => Dedicated("sheet_frame_9s", PanelNight9s, out dedicated);

        /// <summary>전용 아트 Art/UI/{name}.png가 있으면 그것, 없으면 fallback (그리는 중인 아트라 없다는 경고는 남기지 않는다)</summary>
        static Sprite Dedicated(string name, Sprite fallback, out bool dedicated)
        {
            var sprite = ArtLib.UI(name);
            dedicated = sprite != null;
            return dedicated ? sprite : fallback;
        }

        // ───────────── 9-slice ─────────────

        public static Sprite ButtonPrimary9s => UI("button_primary_9s", RoundSprite);
        public static Sprite ButtonGold9s => UI("button_gold_9s", RoundSprite);
        public static Sprite ButtonSecondary9s => UI("ui_button_secondary_9s", RoundSprite);
        public static Sprite ButtonDisabled9s => UI("ui_button_disabled_9s", RoundSprite);
        /// <summary>어두운 알약 (칩 · 이름표 · 수량 받침). 흰색 그대로</summary>
        public static Sprite PillNight9s => UI("ui_pill_night_9s", RoundSprite);
        /// <summary>흰 알약 (색 칩). 틴트용</summary>
        public static Sprite PillWhite9s => UI("ui_pill_white_9s", RoundSprite);
        /// <summary>안쪽 패널 (보라 바느질 판). 흰색 그대로</summary>
        public static Sprite PanelInset9s => UI("ui_panel_inset_9s", RoundSprite);
        /// <summary>탭 · 필터 (흰 바느질 판). 틴트용</summary>
        public static Sprite Tab9s => UI("ui_tab_9s", RoundSprite);
        /// <summary>테두리만 있는 칸 틀 (등급 · 강조색 틴트용)</summary>
        public static Sprite SlotFrame9s => UI("ui_slot_frame_9s", RoundSprite);
        /// <summary>막대 틀 (캡슐)</summary>
        public static Sprite BarFrame9s => UI("ui_bar_frame_9s", RoundSprite);
        /// <summary>막대 채움 (흰 캡슐, 틴트용)</summary>
        public static Sprite BarFill9s => UI("ui_bar_fill_9s", RoundSprite);
        /// <summary>벨벳 띠 (아래에 금 솔기 — 상단 바. 하단 탭 바는 세로로 뒤집어 쓴다)</summary>
        public static Sprite BarVelvet9s => UI("ui_bar_velvet_9s", RoundSprite);
        public static Sprite Bubble9s => UI("ui_bubble_9s", RoundSprite);
        public static Sprite TutorialHighlight9s => UI("tutorial_highlight_9s", RoundSprite);
        public static Sprite PanelNight9s => UI("panel_night_9s", RoundSprite);
        public static Sprite PanelParchment9s => UI("panel_parchment_9s", RoundSprite);
        public static Sprite CardFrame9s => UI("card_frame_9s", RoundSprite);

        // ───────────── 가로로 긴 조각 ─────────────

        /// <summary>금 리본 (테두리 L/R 140, T/B 40 — 높이 = 160 / 배율)</summary>
        public static Sprite RibbonGold9s => UI("ui_ribbon_gold_9s", RoundSprite);
        public static Sprite RibbonLavender9s => UI("ui_ribbon_lavender_9s", RoundSprite);
        /// <summary>밤 리본 (테두리 L/R 200, T/B 48)</summary>
        public static Sprite RibbonNight9s => UI("ui_ribbon_night_9s", RoundSprite);
        public static Sprite TagSale => UI("ui_tag_sale", RoundSprite);
        public static Sprite StampSoldout => UI("ui_stamp_soldout", RoundSprite);
        /// <summary>금실 구분선 (768x48, 비율 고정으로 그린다)</summary>
        public static Sprite DividerGold => UI("ui_divider_gold", null);

        // ───────────── 배경 ─────────────

        /// <summary>네모 초상화 배경 (수선점 안 · 1024x1024)</summary>
        public static Sprite PortraitBg => UI("portrait_bg", RoundSprite);
        /// <summary>빈 카드 칸 (3:4 — 비율대로 그린다)</summary>
        public static Sprite CardEmpty => UI("ui_card_empty", RoundSprite);
        /// <summary>잠긴 카드 칸 (3:4 — 비율대로 그린다)</summary>
        public static Sprite CardLocked => UI("ui_card_locked", RoundSprite);

        // ───────────── 아이콘 ─────────────

        /// <summary>별 (자리표시 아이콘 · 편성 점 · 기절)</summary>
        public static Sprite Star => Get("Icons/star.png", CircleSprite);
        public static Sprite Check => Get("Icons/check.png", CircleSprite);

        // ───────────── FX (흰색, 틴트용) ─────────────

        public static Sprite Sparkle => First("FX/fx_sparkle.png", "FX/sparkle.png", CircleSprite);
        public static Sprite Puff => First("FX/fx_puff.png", "FX/circle_soft.png", CircleSprite);
        public static Sprite Trail => First("FX/fx_trail.png", "FX/trail.png", RoundSprite);
        /// <summary>햇살 (없으면 null)</summary>
        public static Sprite Rays => Get("FX/rays.png", null);
        /// <summary>실 고리 (고리 중심선 = 반지름의 0.8418)</summary>
        public static Sprite RingThread => First("FX/fx_ring_thread.png", "FX/ring_glow.png", CircleSprite);
        /// <summary>RingThread 그림에서 실 중심선 반지름 / 그림 절반 크기</summary>
        public const float RingThreadRadius = 0.8418f;

        /// <summary>색종이 4종 (리본 · 단추 · 별 · 실). 없는 것은 빠진다</summary>
        public static Sprite[] Confetti
        {
            get
            {
                var list = new List<Sprite>();
                foreach (var name in new[] { "fx_confetti_ribbon", "fx_confetti_button", "fx_confetti_star", "fx_confetti_thread" })
                {
                    var sprite = Get($"FX/{name}.png", null);
                    if (sprite != null) list.Add(sprite);
                }
                return list.ToArray();
            }
        }

        // ───────────── 계획된 아트 (PHASE4 RD-5 · SPEC4 §9.3 · ART-UPGRADE §4.3 #7) ─────────────
        // 아직 그리지 않았거나(크레딧 충전 대기) 나중에 들어올 아트. 파일이 있으면 그것, 없으면 지금 쓰는 아트로 대신한다
        // (null을 돌려주는 것은 summary에 적었다 — 부르는 쪽이 그 장식을 건너뛴다). 예외를 던지지 않고, 없다는 경고도 남기지 않는다(Dedicated와 같은 규칙).
        // 경로 문자열은 리터럴 · 미리 만든 표라 부를 때마다 새로 만들지 않는다. 그림이 들어오면 다음 빌드가 코드 수정 없이 집어 간다.

        /// <summary>작은 크림 버튼 (SceneParts.ButtonStyle.Soft): 전용 Art/UI/ui_button_soft_9s, 없으면 PanelParchment9s. 흰색 그대로, 배율 ButtonPpu(높이)</summary>
        public static Sprite ButtonSoft9s => Planned("UI/ui_button_soft_9s.png", out var sprite) ? sprite : PanelParchment9s;

        /// <summary>금 꼬리표 (받기 · 가득 · 점수 알약, 192x96 캡슐): 전용 Art/UI/ui_tag_gold_9s, 없으면 ButtonGold9s. 흰색 그대로, 배율 CapsulePpu(높이)</summary>
        public static Sprite TagGold9s => Planned("UI/ui_tag_gold_9s.png", out var sprite) ? sprite : ButtonGold9s;

        /// <summary>선택된 페이지 탭: 전용 Art/UI/ui_tab_on_9s, 없으면 Tab9s(틴트용). 전용 한 쌍이 다 있을 때(HasTabSkin)만 흰색 + 스프라이트 교체, 아니면 지금처럼 색으로 칠한다</summary>
        public static Sprite TabOn9s => Planned("UI/ui_tab_on_9s.png", out var sprite) ? sprite : Tab9s;

        /// <summary>선택 안 된 페이지 탭: 전용 Art/UI/ui_tab_off_9s, 없으면 Tab9s(틴트용)</summary>
        public static Sprite TabOff9s => Planned("UI/ui_tab_off_9s.png", out var sprite) ? sprite : Tab9s;

        /// <summary>탭 켜짐 · 꺼짐 전용 아트가 둘 다 있는지 (TabSkin을 붙일지 — 하나만 있으면 지금처럼 틴트)</summary>
        public static bool HasTabSkin => Planned("UI/ui_tab_on_9s.png", out _) && Planned("UI/ui_tab_off_9s.png", out _);

        /// <summary>켜진 스위치 길 (192x96 캡슐): 전용 Art/UI/ui_switch_on_9s, 없으면 BarFrame9s. 전용 한 쌍이 없으면(HasSwitchSkin == false) MetaSwitch는 지금처럼 TrackColor로 칠한다</summary>
        public static Sprite SwitchOn9s => Planned("UI/ui_switch_on_9s.png", out var sprite) ? sprite : BarFrame9s;

        /// <summary>꺼진 스위치 길 (192x96 캡슐): 전용 Art/UI/ui_switch_off_9s, 없으면 BarFrame9s</summary>
        public static Sprite SwitchOff9s => Planned("UI/ui_switch_off_9s.png", out var sprite) ? sprite : BarFrame9s;

        /// <summary>스위치 켜짐 · 꺼짐 전용 아트가 둘 다 있는지 (MetaSwitch에 _trackOn/_trackOff를 넣을지 — 아니면 null을 넣어 틴트)</summary>
        public static bool HasSwitchSkin => Planned("UI/ui_switch_on_9s.png", out _) && Planned("UI/ui_switch_off_9s.png", out _);

        /// <summary>출석 칸 (192x256, 테두리 L44 B44 R44 T88): 전용 Art/UI/ui_attendance_cell_9s, 없으면 PanelParchment9s. 배율 2.4</summary>
        public static Sprite AttendanceCell9s => Planned("UI/ui_attendance_cell_9s.png", out var sprite) ? sprite : PanelParchment9s;

        /// <summary>출석 특별한 날 칸 (금 머리띠): 전용 Art/UI/ui_attendance_cell_gold_9s, 없으면 PanelParchment9s (그때는 지금처럼 Gold로 칠한다 — HasAttendanceCellGold9s)</summary>
        public static Sprite AttendanceCellGold9s => Planned("UI/ui_attendance_cell_gold_9s.png", out var sprite) ? sprite : PanelParchment9s;

        /// <summary>출석 칸 전용 아트가 있는지</summary>
        public static bool HasAttendanceCell9s => Planned("UI/ui_attendance_cell_9s.png", out _);

        /// <summary>출석 특별한 날 전용 아트가 있는지 (있으면 스프라이트 교체, 없으면 Gold 틴트)</summary>
        public static bool HasAttendanceCellGold9s => Planned("UI/ui_attendance_cell_gold_9s.png", out _);

        /// <summary>이름표 캡슐 ('수선점' · 보스 이름, 192x96): 전용 Art/UI/ui_nameplate_9s, 없으면 PillNight9s. 흰색 그대로, 배율 CapsulePpu(높이)</summary>
        public static Sprite Nameplate9s => Planned("UI/ui_nameplate_9s.png", out var sprite) ? sprite : PillNight9s;

        /// <summary>가방 머리 그림 (1200x400): 전용 Art/UI/bag_header, 없으면 null (가방 창은 머리 띠를 건너뛴다)</summary>
        public static Sprite BagHeader => Planned("UI/bag_header.png", out var sprite) ? sprite : null;

        /// <summary>전투력 아이콘 (M10 PowerChip): Art/Icons/power, 없으면 Icons/guardian, 그것도 없으면 Star</summary>
        public static Sprite PowerIcon => Planned("Icons/power.png", out var sprite) ? sprite : Get("Icons/guardian.png", Star);

        /// <summary>수선점 금 간 단계 수 (shop_crack_1..3)</summary>
        public const int ShopCrackStages = 3;
        static readonly string[] ShopCrackPaths = { "Battle/shop_crack_1.png", "Battle/shop_crack_2.png", "Battle/shop_crack_3.png" };

        /// <summary>
        /// 수선점 금 간 덮개 (768², Battle/shop.png와 같은 자리): stage 1~3 = Art/Battle/shop_crack_{stage}.
        /// 없거나 stage가 범위 밖이면 null (ShopView는 덮개 없이 지금의 붉은 틴트 · 불티만 쓴다)
        /// </summary>
        public static Sprite ShopCrack(int stage) =>
            stage >= 1 && stage <= ShopCrackPaths.Length && Planned(ShopCrackPaths[stage - 1], out var sprite) ? sprite : null;

        /// <summary>동화책 한 장의 그림 칸 수 (ch{n}_1..3)</summary>
        public const int StoryPanelsPerChapter = 3;
        /// <summary>동화책 그림 경로 (chapter * 10 + panel → 경로). 처음 부를 때 한 번만 만든다</summary>
        static readonly Dictionary<int, string> StoryPanelPaths = new();

        /// <summary>
        /// 동화책 그림 (940x380으로 덮어 그린다): Art/Story/ch{chapter}_{panel} (chapter ≥ 1, panel 1~9).
        /// 없거나 범위 밖이면 null (StoryPagePopup은 PanelInset9s 카드 + 장 색 틴트, 글만 보여 준다)
        /// </summary>
        public static Sprite StoryPanel(int chapter, int panel)
        {
            if (chapter < 1 || chapter > 9999 || panel < 1 || panel > 9) return null;
            int key = chapter * 10 + panel;
            if (!StoryPanelPaths.TryGetValue(key, out var path))
            {
                path = $"Story/ch{chapter}_{panel}.png";
                StoryPanelPaths[key] = path;
            }
            return Planned(path, out var sprite) ? sprite : null;
        }

        /// <summary>챕터 지도 바탕 (1024x1536, 880x1000에 덮어 맞춘다): 전용 Art/Story/map_bg, 없으면 UI/bg_night (그것도 없으면 null)</summary>
        public static Sprite MapBg => Planned("Story/map_bg.png", out var sprite) ? sprite : Get("UI/bg_night.png", null);

        /// <summary>챕터 지도 칸 그림 종류 (값 = MapNode(int)의 번호)</summary>
        public enum MapNodeKind
        {
            /// <summary>불 꺼진 가로등 (잠긴 칸)</summary>
            Off = 0,
            /// <summary>불 켜진 가로등 (깬 칸 · 지금 칸)</summary>
            On = 1,
            /// <summary>보스 고리 (가운데에 보스 초상화)</summary>
            Boss = 2,
            /// <summary>챕터 상자 표시</summary>
            Chest = 3,
        }

        static readonly string[] MapNodePaths = { "UI/map_node_off.png", "UI/map_node_on.png", "UI/map_node_boss.png", "UI/map_node_chest.png" };

        /// <summary>챕터 지도 칸 (96x96): 0 꺼짐 · 1 켜짐 · 2 보스 · 3 상자 (범위 밖은 가까운 값으로). 없으면 MapNode(MapNodeKind)의 대신 그림</summary>
        public static Sprite MapNode(int kind) => MapNode((MapNodeKind)Mathf.Clamp(kind, 0, MapNodePaths.Length - 1));

        /// <summary>
        /// 챕터 지도 칸 (96x96): 전용 Art/UI/map_node_{off,on,boss,chest}. 없으면
        /// 꺼짐 · 켜짐 = Icons/lamp (잠긴 칸은 부르는 쪽이 α0.55), 보스 = SkillOrbFrame (빈 고리 — 보스 초상화가 안에 들어간다), 상자 = UI/journal_chest_closed.
        /// </summary>
        public static Sprite MapNode(MapNodeKind kind)
        {
            int index = Mathf.Clamp((int)kind, 0, MapNodePaths.Length - 1);
            if (Planned(MapNodePaths[index], out var sprite)) return sprite;
            return (MapNodeKind)index switch
            {
                MapNodeKind.Boss => SkillOrbFrame,
                MapNodeKind.Chest => Get("UI/journal_chest_closed.png", Star),
                _ => Get("Icons/lamp.png", Star),
            };
        }

        /// <summary>분실물 꾸러미 상태 (값 = GachaBundle(int)의 번호)</summary>
        public enum BundleState
        {
            /// <summary>묶인 꾸러미 (떨어지고 흔들릴 때)</summary>
            Closed = 0,
            /// <summary>빛나는 꾸러미 (높은 등급 기대)</summary>
            Glow = 1,
            /// <summary>열린 꾸러미</summary>
            Open = 2,
        }

        static readonly string[] GachaBundlePaths = { "UI/gacha_bundle_closed.png", "UI/gacha_bundle_glow.png", "UI/gacha_bundle_open.png" };

        /// <summary>분실물 꾸러미 (300x300 · 열 번 뽑기 150x150): 0 묶임 · 1 빛남 · 2 열림 (범위 밖은 가까운 값으로). 대신 그림은 GachaBundle(BundleState)</summary>
        public static Sprite GachaBundle(int state) => GachaBundle((BundleState)Mathf.Clamp(state, 0, GachaBundlePaths.Length - 1));

        /// <summary>
        /// 분실물 꾸러미: 전용 Art/UI/gacha_bundle_{closed,glow,open}. 그 상태 그림이 없으면 묶인 꾸러미, 그것도 없으면 UI/gacha_card_back (카드 뒷면).
        /// </summary>
        public static Sprite GachaBundle(BundleState state)
        {
            int index = Mathf.Clamp((int)state, 0, GachaBundlePaths.Length - 1);
            if (Planned(GachaBundlePaths[index], out var sprite)) return sprite;
            if (index != (int)BundleState.Closed && Planned(GachaBundlePaths[(int)BundleState.Closed], out sprite)) return sprite;
            return UI("gacha_card_back", RoundSprite);
        }

        /// <summary>수선점 기록 메달 종류 (값 = Medal(int)의 번호)</summary>
        public enum MedalKind
        {
            Bronze = 0,
            Silver = 1,
            Gold = 2,
            /// <summary>숨은 기록 '?' 메달</summary>
            Hidden = 3,
        }

        static readonly string[] MedalPaths = { "UI/medal_bronze.png", "UI/medal_silver.png", "UI/medal_gold.png", "UI/medal_hidden.png" };

        /// <summary>펠트 메달 (256): 0 동 · 1 은 · 2 금 · 3 숨은 기록 (범위 밖은 가까운 값으로). 대신 그림은 Medal(MedalKind)</summary>
        public static Sprite Medal(int kind) => Medal((MedalKind)Mathf.Clamp(kind, 0, MedalPaths.Length - 1));

        /// <summary>펠트 메달: 전용 Art/UI/medal_{bronze,silver,gold,hidden}. 없으면 동 · 은 · 금 = Icons/journal, 숨은 기록 = Icons/lock</summary>
        public static Sprite Medal(MedalKind kind)
        {
            int index = Mathf.Clamp((int)kind, 0, MedalPaths.Length - 1);
            if (Planned(MedalPaths[index], out var sprite)) return sprite;
            return (MedalKind)index == MedalKind.Hidden ? Get("Icons/lock.png", Star) : Get("Icons/journal.png", Star);
        }

        /// <summary>
        /// 호감도 Lv5 하트 바느질 카드 틀 (192, 테두리 48): 전용 Art/UI/affinity_frame_9s(흰색 그대로), 없으면 SlotFrame9s(틴트용 —
        /// HasAffinityFrame9s가 false면 부르는 쪽이 DustyPink로 칠한다)
        /// </summary>
        public static Sprite AffinityFrame9s => Planned("UI/affinity_frame_9s.png", out var sprite) ? sprite : SlotFrame9s;

        /// <summary>호감도 카드 틀 전용 아트가 있는지</summary>
        public static bool HasAffinityFrame9s => Planned("UI/affinity_frame_9s.png", out _);

        /// <summary>호감도 배지 (128): 전용 Art/UI/affinity_badge, 없으면 Icons/affinity (하트)</summary>
        public static Sprite AffinityBadge => Planned("UI/affinity_badge.png", out var sprite) ? sprite : Get("Icons/affinity.png", Star);

        /// <summary>쓰다듬기 하트 터짐 (256): 전용 Art/UI/fx_heart_burst, 없으면 VFX/motif_heart, 그것도 없으면 Icons/affinity</summary>
        public static Sprite HeartBurst => Planned("UI/fx_heart_burst.png", out var sprite) || Planned("VFX/motif_heart.png", out sprite) ? sprite : Get("Icons/affinity.png", Star);

        /// <summary>
        /// 호감도 후광 (256): 전용 Art/UI/affinity_halo(색 그림, 흰색 그대로), 없으면 FX/glow_soft (흰 글로우 — 부르는 쪽이 DustyPink로 칠한다).
        /// 어느 쪽인지는 HasAffinityHalo로 고른다 (IsArt는 둘 다 true)
        /// </summary>
        public static Sprite AffinityHalo => Planned("UI/affinity_halo.png", out var sprite) ? sprite : Get("FX/glow_soft.png", CircleSprite);

        /// <summary>호감도 후광 전용 아트가 있는지 (false일 때만 AffinityHalo를 DustyPink로 칠한다 — true면 흰색 그대로)</summary>
        public static bool HasAffinityHalo => Planned("UI/affinity_halo.png", out _);

        /// <summary>다시 만난 날 머리 그림 (1536x560 → 840x300에 덮어 맞춘다): 전용 Art/UI/comeback_header, 없으면 UI/return_header (그것도 없으면 null)</summary>
        public static Sprite ComebackHeader => Planned("UI/comeback_header.png", out var sprite) ? sprite : Get("UI/return_header.png", null);

        /// <summary>별빛 산책 수첩 띠 그림 (1024x384): 전용 Art/UI/season_banner, 없으면 UI/shop_event_banner (그것도 없으면 null)</summary>
        public static Sprite SeasonBanner => Planned("UI/season_banner.png", out var sprite) ? sprite : Get("UI/shop_event_banner.png", null);

        /// <summary>엉킨 실뭉치 적 처치 연출 (256): Art/VFX/enemy_death_yarn, 없으면 null (지금의 먼지 + 와인색 틴트)</summary>
        public static Sprite EnemyDeathYarn => Planned("VFX/enemy_death_yarn.png", out var sprite) ? sprite : null;

        /// <summary>녹 얼룩 스티커 (64, 받침에 붙는다): Art/VFX/fx_rust_stain, 없으면 null (지금의 코드로 그린 RustStain)</summary>
        public static Sprite RustStain => Planned("VFX/fx_rust_stain.png", out var sprite) ? sprite : null;

        /// <summary>좀벌레 구멍 스티커 (64, 천 메아리에 붙는다): Art/VFX/fx_moth_hole, 없으면 null (지금의 코드로 그린 GnawMark)</summary>
        public static Sprite MothHole => Planned("VFX/fx_moth_hole.png", out var sprite) ? sprite : null;

        /// <summary>호감도 Lv10 자세 그림 경로 (기본 형태 이름 → 경로). 처음 부를 때 한 번만 만든다</summary>
        static readonly Dictionary<string, string> Pose10Paths = new();

        /// <summary>
        /// 호감도 Lv10 자세 (A11, 기본 형태 그림과 같은 1024 캔버스 · 같은 발 위치): Art/Characters/{baseFormId}_pose10 (jjak_base → jjak_base_pose10).
        /// baseFormId = LineageDef.baseFormId. 없거나 이름이 비었거나 이름에 영문 · 숫자 · '_' · '-' 밖의 글자가 있으면 null
        /// (부르는 쪽은 지금처럼 기본 형태 그림 + 코드 흔들림 ±3° · 2.4초 + 하트 반짝임)
        /// </summary>
        public static Sprite Pose10(string baseFormId)
        {
            if (!IsPlainArtName(baseFormId)) return null;
            if (!Pose10Paths.TryGetValue(baseFormId, out var path))
            {
                path = $"Characters/{baseFormId}_pose10.png";
                Pose10Paths[baseFormId] = path;
            }
            return Planned(path, out var sprite) ? sprite : null;
        }

        /// <summary>그 기본 형태의 Lv10 자세 그림이 있는지 (false면 코드 흔들림)</summary>
        public static bool HasPose10(string baseFormId) => Pose10(baseFormId) != null;

        /// <summary>
        /// 별빛 산책 수첩 30칸 의상 (A14, 다른 Costumes/jjak_*와 같은 1024 캔버스 · 기준점): Art/Costumes/jjak_starwalk, 없으면 null
        /// (그림이 없으면 30칸은 의상 대신 다른 보상을 준다 — SPEC4 A.8 / critic C-06, 수첩 칸은 보상 칩만 보여 준다)
        /// </summary>
        public static Sprite StarwalkCostume => Planned("Costumes/jjak_starwalk.png", out var sprite) ? sprite : null;

        /// <summary>별빛 산책 의상 그림이 있는지 (콘텐츠가 30칸 보상을 의상으로 둘지 정할 때)</summary>
        public static bool HasStarwalkCostume => Planned("Costumes/jjak_starwalk.png", out _);

        /// <summary>파일 이름 한 조각으로 쓸 수 있는 이름인지 (비지 않고 64자 이하, 영문 · 숫자 · '_' · '-'만 — 경로 구분자 · 점 · 공백이 섞이면 false)</summary>
        static bool IsPlainArtName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64) return false;
            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>계획된 아트 Art/{relativePath}가 있으면 true + 그 그림. 없으면 false + null (경고는 남기지 않는다 — 대신 그림은 부르는 쪽이 고른다)</summary>
        static bool Planned(string relativePath, out Sprite sprite)
        {
            sprite = ArtLib.Load(relativePath);
            if (sprite != null) return true;
            sprite = null;
            return false;
        }

        // ───────────── 찾기 ─────────────

        /// <summary>Art/UI/{name}.png. 없으면 fallback (경고 한 번)</summary>
        public static Sprite UI(string name, Sprite fallback) => Get($"UI/{name}.png", fallback);

        /// <summary>Art/{relativePath}. 없으면 fallback (경고 한 번)</summary>
        public static Sprite Get(string relativePath, Sprite fallback)
        {
            var sprite = ArtLib.Load(relativePath);
            if (sprite != null) return sprite;
            Warn(relativePath);
            return fallback;
        }

        /// <summary>앞의 아트 → 뒤의 아트 → fallback 순서</summary>
        static Sprite First(string preferred, string second, Sprite fallback)
        {
            var sprite = ArtLib.Load(preferred);
            if (sprite != null) return sprite;
            sprite = ArtLib.Load(second);
            if (sprite != null) return sprite;
            Warn(preferred);
            return fallback;
        }

        static void Warn(string relativePath)
        {
            if (Warned.Add(relativePath)) Debug.LogWarning($"[UiArt] Art/{relativePath} 아트가 없어 자리표시로 대신합니다.");
        }

        /// <summary>자리표시(UIKit 원 · 둥근 사각형)가 아닌 진짜 아트인지</summary>
        public static bool IsArt(Sprite sprite) => sprite != null && sprite != RoundSprite && sprite != CircleSprite;

        // ───────────── 입히기 ─────────────

        /// <summary>
        /// 아트를 입힌다. 9-slice면 Sliced + 배율(ppu), 아니면 Simple. 색은 아트면 tint(없으면 흰색),
        /// 자리표시면 fallbackColor(없으면 tint, 그것도 없으면 PanelLight).
        /// </summary>
        public static Image Skin9(Image image, Sprite sprite, float ppu, Color? tint = null, Color? fallbackColor = null)
        {
            image.sprite = sprite;
            bool sliced = sprite != null && sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = sliced ? Mathf.Max(0.01f, ppu) : 1f;
            image.color = IsArt(sprite) ? tint ?? Color.white : fallbackColor ?? tint ?? UITheme.PanelLight;
            return image;
        }

        /// <summary>9-slice 판 하나 만들기 (Img + Skin9)</summary>
        public static Image Panel(Transform parent, string name, Sprite sprite, float ppu, Color? tint = null, Color? fallbackColor = null,
            bool raycast = false)
        {
            var image = Img(parent, name, Color.white, sprite, raycast);
            return Skin9(image, sprite, ppu, tint, fallbackColor);
        }

        /// <summary>그림 아이콘: 비율 유지, 흰색, 터치 안 받음</summary>
        public static Image Icon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, float size)
        {
            var image = Img(parent, name, Color.white, sprite);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.At(anchor, position, new Vector2(size, size));
            return image;
        }

        /// <summary>
        /// 빨간 알림 배지 (ui_badge_red, 흰색, 비율 유지). count가 있으면 크림색 숫자(어두운 외곽선)를 얹는다 — 숫자는 첫 번째 자식 "Count".
        /// 점만 쓰면 30px, 숫자 배지는 40px 정도.
        /// </summary>
        public static Image Badge(Transform parent, string name, Vector2 anchor, Vector2 position, float size = 30f, string count = null)
        {
            var art = BadgeRed;
            var badge = Img(parent, name, IsArt(art) ? Color.white : UITheme.RedDot, art);
            badge.type = Image.Type.Simple;
            badge.preserveAspect = true;
            badge.raycastTarget = false;
            badge.rectTransform.At(anchor, position, new Vector2(size, size));
            if (count != null) BadgeCount(badge.transform, count, size);
            return badge;
        }

        /// <summary>배지 위 숫자 (크림색 + 어두운 외곽선)</summary>
        public static TextMeshProUGUI BadgeCount(Transform badge, string count, float size)
        {
            var label = Txt(badge, "Count", count, Mathf.Max(14f, size * 0.56f), UITheme.Cream, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch(-6, -6, 0, size * 0.04f);
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(10f, size * 0.36f);
            label.fontSizeMax = label.fontSize;
            return Outline(label);
        }

        /// <summary>
        /// 가로 막대: 틀(ui_bar_frame_9s) + 채움(ui_bar_fill_9s) 둘 다 Sliced. 채움 너비는 fill.anchorMax.x(0~1)로 정하고 0이면 숨긴다.
        /// Image.Type.Filled는 둥근 9-slice에 쓰지 않는다 (끝이 렌즈 모양으로 잘린다).
        /// height는 막대 높이 (캡슐 배율 = 96 / 높이). 반환값은 틀, 크기는 호출하는 쪽에서 잡는다.
        /// </summary>
        public static Image Bar(Transform parent, string name, Color fillTint, out RectTransform fill, float height = 20f)
        {
            var frameArt = BarFrame9s;
            var track = Panel(parent, name, frameArt, CapsulePpu(height), null, MenuAlpha(UITheme.Background, 0.85f));
            float inset = IsArt(frameArt) ? Mathf.Max(2f, Mathf.Round(height * 0.16f)) : 0f;
            var fillImage = Panel(track.transform, "Fill", BarFill9s, CapsulePpu(Mathf.Max(4f, height - inset * 2f)), fillTint, fillTint);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(inset, inset);
            fill.offsetMax = new Vector2(-inset, -inset);
            return track;
        }

        /// <summary>
        /// 버튼에 9-slice 아트를 입힌다: 흰색, 색 변화 대신 스프라이트 교체(SpriteSwap), 비활성 = ui_button_disabled_9s.
        /// (예전 ColorTint 비활성 색을 아트에 곱하면 갈색으로 탁해진다) 아트가 없으면 아무것도 바꾸지 않는다.
        /// </summary>
        public static void SetButtonArt(Button button, Sprite normal, float ppu)
        {
            if (!IsArt(normal)) return;
            var image = (Image)button.targetGraphic;
            Skin9(image, normal, ppu);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
            var disabled = ButtonDisabled9s;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { disabledSprite = IsArt(disabled) ? disabled : null };
        }

        /// <summary>버튼 높이에 맞는 9-slice 배율 (90~110px → 2, 60~70px → 2.8, 40~50px → 3.5, 더 작으면 테두리 ≈ 높이의 ¼)</summary>
        public static float ButtonPpu(float height)
        {
            if (height >= 88f) return 2f;
            if (height >= 58f) return 2.8f;
            if (height >= 40f) return 3.5f;
            return Mathf.Max(3.5f, 190f / Mathf.Max(1f, height));
        }

        /// <summary>캡슐(192x96 알약 · 막대) 배율: 96 / 높이 (48px → 2, 34px → 2.8, 20px → 4.8)</summary>
        public static float CapsulePpu(float height) => 96f / Mathf.Max(1f, height);

        // ───────────── 글자 외곽선 ─────────────

        /// <summary>
        /// 어두운 외곽선 글자 (배지 숫자 등). 프리팹에도 남도록 공유 머티리얼 에셋(Generated/UiTextOutline.mat)을 쓴다
        /// (TMP outlineWidth는 인스턴스 머티리얼을 만들어서 프리팹에 저장되지 않는다).
        /// </summary>
        public static TextMeshProUGUI Outline(TextMeshProUGUI label)
        {
            var material = OutlineMaterial();
            if (material != null) label.fontSharedMaterial = material;
            return label;
        }

        static Material OutlineMaterial()
        {
            if (_outlineMaterial != null) return _outlineMaterial;
            var font = DefaultFont;
            if (font == null || font.material == null) return null;

            _outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (_outlineMaterial == null)
            {
                EnsureGeneratedFolder();
                _outlineMaterial = new Material(font.material) { name = "UiTextOutline" };
                AssetDatabase.CreateAsset(_outlineMaterial, OutlineMaterialPath);
            }
            else
            {
                _outlineMaterial.shader = font.material.shader;
                _outlineMaterial.SetTexture(ShaderUtilities.ID_MainTex, font.material.GetTexture(ShaderUtilities.ID_MainTex));
            }
            _outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            _outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            _outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.16f, 0.08f, 0.24f, 1f));
            _outlineMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            ShaderUtilities.UpdateShaderRatios(_outlineMaterial);
            EditorUtility.SetDirty(_outlineMaterial);
            return _outlineMaterial;
        }

        /// <summary>Assets/_Project/Generated 폴더 (빌드가 만드는 에셋)</summary>
        public static string EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedDir)) AssetDatabase.CreateFolder(IdleProjectSetup.Root, "Generated");
            return GeneratedDir;
        }

        static Color MenuAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
