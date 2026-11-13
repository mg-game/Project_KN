using UnityEngine;

namespace IdleGame.UI
{
    // ═════════════════════════════════════════════════════════════════════════════
    // 디자인 토큰 (UX-SPEC §1). 색은 UITheme, 글자·간격·크기·움직임은 여기.
    // 새 빌더 코드와 런타임 뷰는 숫자를 직접 쓰지 않고 이 이름을 쓴다 (기존 코드는 손댈 때 옮긴다).
    // 기준 화면 세로 1080x1920 (CanvasScaler), 단위는 기준 px / 초.
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 글자 크기 단계 (M23). 표 (px · 쓰임):
    ///   Display 56 큰 제목(배너·축하) · H1 40 팝업/패널 제목, 상단 바 전투력 · H2 32 구역 제목, 큰 카드 제목 · Title 28 카드/줄 제목, 토스트 ·
    ///   Body 26 본문 · Label 24 칩, 작은 버튼, 보조 숫자 · Caption 22 SubText/InkSub 설명의 하한 · Hint 20 패널마다 한 줄인 안내(최소 18) ·
    ///   Micro 18 굵게만, 단단한 알약·배지 위(대비 4.5:1 이상).
    /// 규칙: 기본 글자 크기 18 이상, 자동 크기 최소 16 이상(UIKit.FitFloor), 중요한 숫자(재화·전투력·스테이지·비용·타이머)는 굵게,
    /// 매초 바뀌는 시계는 UIFormat.ClockMono. 굵게는 진짜 Bold 글꼴(NotoSansKR-Bold, M27)로 그려진다.
    /// </summary>
    public static class UIType
    {
        public const float Display = 56f;
        public const float H1 = 40f;
        public const float H2 = 32f;
        public const float Title = 28f;
        public const float Body = 26f;
        public const float Label = 24f;
        public const float Caption = 22f;
        /// <summary>패널마다 한 줄인 안내 글자 (M80, §3.17)</summary>
        public const float Hint = 20f;
        /// <summary>안내 글자 자동 크기 최소</summary>
        public const float HintMin = 18f;
        /// <summary>굵게만, 단단한 알약·배지 위에서만</summary>
        public const float Micro = 18f;

        /// <summary>모든 글자의 기본 크기 하한</summary>
        public const float Floor = 18f;
        /// <summary>자동 크기(enableAutoSizing) 최소 하한 — 이보다 작게 요청하면 UIKit.FitFloor가 올리고 경고한다</summary>
        public const float AutoSizeFloor = 16f;

        // ── 버튼 글자 (크기는 SceneParts.Button이 정하지 않는다) ──
        public const float ButtonXL = 36f;
        public const float ButtonL = 30f;
        public const float ButtonM = 28f;
        public const float ButtonS = 24f;

        /// <summary>팝업 제목 (BuildPopup)</summary>
        public const float PopupTitle = H1;
        /// <summary>탭 패널 머리 제목 (SceneParts.PanelHeader)</summary>
        public const float PanelTitle = 34f;
    }

    /// <summary>
    /// 간격 (M24). 단계 4 / 8 / 12 / 16 / 24 / 32 / 48 과 이름 붙은 값.
    /// 격자: Grid4 = 칸 240, 간격 16(가로·세로), 좌우 36 (36 + 4·240 + 3·16 + 36 = 1080 — 친구들·상점 첫 열 x 36) /
    /// Grid5Popup = 칸 164, 간격 17 (5·164 + 4·17 = 888 = 팝업 본문 폭). 소환 결과 170x250, 메타 7열 128x150, 강화 계보 줄은 그대로.
    /// </summary>
    public static class UISpace
    {
        public const float S4 = 4f;
        public const float S8 = 8f;
        public const float S12 = 12f;
        public const float S16 = 16f;
        public const float S24 = 24f;
        public const float S32 = 32f;
        public const float S48 = 48f;

        /// <summary>전투 HUD 가장자리 여백</summary>
        public const float HudMargin = 16f;
        /// <summary>탭 콘텐츠 좌우 여백</summary>
        public const float ContentMargin = 24f;
        public const float PanelPad = 24f;
        /// <summary>팝업 창 안쪽 여백 (Body 좌우 36)</summary>
        public const float PopupPad = 36f;
        public const float RowPad = 20f;
        public const float ListGap = 12f;
        public const float GridGap = 16f;
        public const float SectionGap = 32f;

        // ── 격자 ──
        public const float Grid4Cell = 240f;
        public const float Grid4Gap = 16f;
        public const float Grid4Pad = 36f;
        public const int Grid4Columns = 4;
        public const float Grid5PopupCell = 164f;
        public const float Grid5PopupGap = 17f;
        public const int Grid5PopupColumns = 5;

        // ── 탭 패널 머리 (SceneParts.PanelHeader, M51) ──
        /// <summary>머리 줄 높이</summary>
        public const float HeaderHeight = 88f;
        /// <summary>머리 아래 내용이 시작하는 y (위에서)</summary>
        public const float HeaderContentTop = 96f;
        /// <summary>머리 오른쪽 칸 폭 (모두 받기 240x64 · 정렬 180x64)</summary>
        public const float HeaderRightSlot = 240f;
        /// <summary>머리 제목 최대 폭</summary>
        public const float HeaderTitleMax = 220f;
    }

    /// <summary>
    /// 크기 (M22 · M09 · M47 · C-22). 누르는 자리는 MinHit 88이 하한(검사기가 경고), 96이 권장 — 상단 바·HUD는 88.
    /// 누르는 자리 넓히기는 자식 없이 Graphic.raycastPadding으로 (UIKit.HitPad). 잠금 덮개는 대상의 raycastPadding을 그대로 복사한다.
    /// </summary>
    public static class UISize
    {
        /// <summary>누르는 자리 하한 (가로·세로)</summary>
        public const float MinHit = 88f;
        /// <summary>권장 누르는 자리</summary>
        public const float HitTarget = 96f;

        // ── 아이콘 (안내값) ──
        /// <summary>글자 속 비용 아이콘</summary>
        public const float IconS = 32f;
        /// <summary>칩 아이콘</summary>
        public const float IconM = 44f;
        /// <summary>큰 양 표시 아이콘</summary>
        public const float IconL = 64f;
        /// <summary>칸(타일) 아이콘</summary>
        public const float IconXL = 104f;

        // ── 배지 (M09, 모두 raycastTarget=false) ──
        /// <summary>숫자 없는 점</summary>
        public const float BadgeDot = 30f;
        /// <summary>점 테두리</summary>
        public const float BadgeDotOutline = 3f;
        /// <summary>숫자 배지 높이·최소 폭</summary>
        public const float BadgeCount = 40f;
        /// <summary>숫자 배지 글자 ('99+'도 이 크기, 굵게)</summary>
        public const float BadgeCountText = 22f;
        /// <summary>배지 중심 = 누르는 자리 오른쪽 위 모서리에서 안쪽으로 (-8, -8)</summary>
        public const float BadgeInset = 8f;
        public const float BadgeNewWidth = 52f;
        public const float BadgeNewHeight = 26f;
        public const float BadgeNewText = 17f;
        /// <summary>상단 바(약 120px 버튼)의 작은 NEW</summary>
        public const float BadgeNewSmallWidth = 44f;
        public const float BadgeNewSmallHeight = 22f;
        public const float BadgeNewSmallText = 15f;

        // ── 팝업 ──
        /// <summary>닫기 버튼 그림 크기 (누르는 자리는 raycastPadding -12 → 100x100)</summary>
        public const float CloseButton = 76f;
        public const float CloseHitPad = 12f;
        /// <summary>쓰는 팝업의 잔액 칩 BalanceChip 210x56, 창 왼쪽 위 (30, -30)</summary>
        public const float BalanceChipWidth = 210f;
        public const float BalanceChipHeight = 56f;
        public const float BalanceChipInset = 30f;

        // ── 버튼 (안내값, C-17) ──
        /// <summary>팝업 하나짜리 주 버튼 520x112</summary>
        public const float PopupCtaWidth = 520f;
        public const float PopupCtaHeight = 112f;
        /// <summary>두 버튼 사이 (폭은 같게, 20~24)</summary>
        public const float PairGap = 20f;
        /// <summary>줄 안 버튼 180x80 (글자 28)</summary>
        public const float RowActionWidth = 180f;
        public const float RowActionHeight = 80f;

        // ── 비용 버튼 (M07) ──
        /// <summary>부족할 때 비용 뒤에 까는 어두운 알약 높이</summary>
        public const float CostPillHeight = 40f;
        /// <summary>비용 알약 좌우 여백</summary>
        public const float CostPillPad = 12f;

        // ── 모두 받기 (M51) ──
        public const float ClaimAllHeaderWidth = 240f;
        public const float ClaimAllHeaderHeight = 64f;
        public const float ClaimAllHeaderText = 28f;
        public const float ClaimAllFooterWidth = 380f;
        public const float ClaimAllFooterHeight = 96f;
        public const float ClaimAllFooterText = 32f;

        // ── 상단 바 메뉴 버튼 (M10) ──
        public const float MenuButtonWidth = 120f;
        public const float MenuButtonHeight = 80f;
        public const float MenuButtonText = 30f;
        /// <summary>아이콘 메뉴 버튼의 아이콘 · 이름표</summary>
        public const float MenuIcon = 44f;
        public const float MenuIconLabel = 20f;

        // ── 토스트 (M14) ──
        public const float ToastWidth = 880f;
        public const float ToastMinHeight = 88f;
        public const float ToastMaxHeight = 140f;
        public const float ToastPadX = 28f;
        public const float ToastPadY = 18f;
        public const float ToastKindIcon = 40f;
        /// <summary>팝업이 없을 때 토스트 위 끝 = 안전 영역 위에서 480</summary>
        public const float ToastTop = 480f;

        // ── 안내 말풍선 (M59 InfoTooltipView) ──
        public const float TooltipMinWidth = 360f;
        public const float TooltipMaxWidth = 520f;
        public const float TooltipPadX = 20f;
        public const float TooltipPadY = 16f;
        public const float TooltipArrow = 16f;
        /// <summary>안전 영역 가장자리에서 띄우는 거리 (위로 24 안쪽이면 아래로 뒤집는다)</summary>
        public const float TooltipFlipMargin = 24f;
        public const float TooltipEdgeMargin = 16f;

        // ── 화면 틀 (UX-SPEC-IA §7.3 — 상단 바 · 탭 바 · 시트 높이는 UIFrame) ──

        /// <summary>상단 바 아이콘 버튼 (우편 · ≡ 메뉴) 96x96, 이름표 20 굵게 아래</summary>
        public const float TopIconButton = 96f;
        /// <summary>상단 바 아이콘 (버튼 위 가운데 (0, −2))</summary>
        public const float TopIcon = 64f;

        /// <summary>하단 탭 옆 칸 폭 (6 · 148 + 192 = 1080, 강화 · 편성 · 친구들 | 사냥 | 원정 · 소환 · 상점)</summary>
        public const float TabSideWidth = 148f;
        /// <summary>가운데 사냥 칸(Tab_Main) 폭 — 누르는 자리 192x170</summary>
        public const float TabCentreWidth = 192f;
        /// <summary>사냥 메달 (위 가운데 (0, −2))</summary>
        public const float MainMedallion = 150f;
        /// <summary>사냥 빛 (메달 가운데, 탭 바 위로 23px 솟음 — 누름 없음, CR-12)</summary>
        public const float MainGlow = 200f;
        /// <summary>사냥 아이콘 (수선점 그림, 위 가운데 (0, −21), 비율 유지)</summary>
        public const float MainIcon = 112f;
        /// <summary>사냥 이름 받침 112x36 (아래 가운데 (0, 10), 밤 알약 + 2px 금 테두리)</summary>
        public const float MainLabelPlateWidth = 112f;
        public const float MainLabelPlateHeight = 36f;

        /// <summary>빠른 아이콘 칸 104x116 (누르는 자리 = 칸 전체)</summary>
        public const float RailCellWidth = 104f;
        public const float RailCellHeight = 116f;
        /// <summary>칸 위 가운데 어두운 메달 판 · 그 가운데 아이콘</summary>
        public const float RailPlate = 88f;
        public const float RailIcon = 62f;
        /// <summary>칸 아래 이름 알약 100x28 (이름 20 굵게 Cream)</summary>
        public const float RailLabelWidth = 100f;
        public const float RailLabelHeight = 28f;
        /// <summary>화면 옆 끝에서 칸까지</summary>
        public const float RailEdge = 12f;
        /// <summary>위 주머니 칸: StageArea 위 끝에서 306 아래 (16:9 화면 y 436~552)</summary>
        public const float RailTopOffset = 306f;
        /// <summary>아래 맨 끝 칸(3칸): StageArea 아래 끝에서 288 위, 그 위 칸(2칸)은 + RailPitch (412)</summary>
        public const float RailBottomOffset = 288f;
        /// <summary>아래 칸 사이 · 더보기 쟁반 줄 간격</summary>
        public const float RailPitch = 124f;

        /// <summary>시트 머리 손잡이 120x40 (위 가운데 (0, −8), 아래를 가리키는 꺾쇠)</summary>
        public const float SheetHandleWidth = 120f;
        public const float SheetHandleHeight = 40f;
        /// <summary>손잡이 누르는 자리 200x80, 피벗 (0.5, 1) — 시트 위 끝에서 24 위부터 머리 아래 끝까지 (24 + 56)</summary>
        public const float SheetHandleHitWidth = 200f;
        public const float SheetHandleHitHeight = 80f;
        public const float SheetHandleHitRise = 24f;
        /// <summary>시트 머리 칩 자리 300x48 (왼쪽 돌아가기 칩 · 오른쪽 예약 칸, 가장자리 16)</summary>
        public const float HeaderChipWidth = 300f;
        public const float HeaderChipHeight = 48f;

        /// <summary>≡ 메뉴 칸 240x196, 간격 24 (3열, 창 900 x (300 + 220·줄))</summary>
        public const float MenuTileWidth = 240f;
        public const float MenuTileHeight = 196f;
        public const float MenuTileGap = 24f;
    }

    /// <summary>
    /// 움직임 (M24 · M48 · M61 · M14 · M52 · M09 · M19 · M35 · M62 · M63 · M72 · M78 · M56). 모든 UI 움직임은 unscaled 시간으로 돈다.
    /// 이징은 여기 하나 (BattleEase와 같은 식 — MenuFx/CharacterMotion/BattleEase는 이것을 부르는 한 줄로 바꾼다).
    /// 줄이기(Reduced = 설정 '번쩍임 줄이기')면 팝업은 크기 변화 없이 짧은 페이드만, 반복 연출은 쉬는 모양으로.
    /// 규칙(C-08): 한 화면에 반복되는 움직임은 둘까지, "지금 할 수 있는 일"에만.
    /// </summary>
    public static class UIMotion
    {
        // ───────────── 이징 (t = 0~1, 범위 밖은 자른다) ─────────────

        public static float OutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float OutQuad(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u;
        }

        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;

        /// <summary>목표를 살짝 지나쳤다가 돌아오는 곡선. 넘치는 양은 움직이는 구간의 약 10% (s = 1.70158) / 약 5.3% (s = 1.2)</summary>
        public static float OutBack(float t, float s = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((s + 1f) * t + s);
        }

        // ───────────── 줄이기 (M64) ─────────────

        static bool _reduced;
        static bool _reducedKnown;
        static bool _listening;

        /// <summary>
        /// 움직임 줄이기 (설정 '번쩍임 줄이기' 하나로 같이 켠다 — GameSettings.ReduceFlash).
        /// 매 프레임 불러도 되게 값을 기억해 두고, 설정이 바뀌면(GameSettings.Changed) 다시 읽는다 (PlayerPrefs를 매번 읽지 않는다).
        /// </summary>
        public static bool Reduced
        {
            get
            {
                if (!_reducedKnown)
                {
                    if (!_listening)
                    {
                        GameSettings.Changed += ForgetReduced;
                        _listening = true;
                    }
                    _reduced = GameSettings.ReduceFlash;
                    _reducedKnown = true;
                }
                return _reduced;
            }
        }

        static void ForgetReduced() => _reducedKnown = false;

        // ───────────── 누르기 (M61) ─────────────

        /// <summary>누름: 0.06초 OutCubic으로 0.94배</summary>
        public const float PressDown = 0.06f;
        public const float PressScale = 0.94f;
        /// <summary>안에서 뗌: 0.16초 OutBack(ReleaseOvershoot) — 살짝 넘쳤다가 1 (목표 정점 약 1.025~1.03, PressScale이 정한다)</summary>
        public const float ReleaseInside = 0.16f;
        public const float ReleaseOvershoot = 1.2f;
        /// <summary>밖에서 뗌 0.10초 / 누른 채 밖으로 나감 0.08초 (OutCubic, 클릭 없음)</summary>
        public const float ReleaseOutside = 0.10f;
        public const float PressExit = 0.08f;

        // ───────────── 팝업 (M48) ─────────────

        /// <summary>열기: 전체 알파 0.12초 OutQuad</summary>
        public const float PopupRootIn = 0.12f;
        /// <summary>열기: 어둡게 덮기 0.16초 OutCubic</summary>
        public const float PopupDimIn = 0.16f;
        /// <summary>열기: 창 0.20초 OutBack(PopupOvershoot 1.2), 0.90배 → 1</summary>
        public const float PopupWindowIn = 0.20f;
        public const float PopupWindowFrom = 0.90f;
        public const float PopupOvershoot = 1.2f;
        /// <summary>닫기: 0.12초 InCubic, 창 1 → 0.94배 · 알파 → 0 · 덮개 → 0, 그다음 SetActive(false)</summary>
        public const float PopupOut = 0.12f;
        public const float PopupWindowTo = 0.94f;
        /// <summary>줄이기: 크기 그대로, 알파만 0.08초 열고 0.06초 닫기</summary>
        public const float PopupReducedIn = 0.08f;
        public const float PopupReducedOut = 0.06f;
        /// <summary>덮개 목표 알파: 팝업 하나 1, 겹친 팝업 0.4 (아래 덮개는 그대로)</summary>
        public const float DimSingle = 1f;
        public const float DimStacked = 0.4f;
        /// <summary>접속 팝업 사이 쉼 (닫힘 확인 뒤 WaitForSecondsRealtime)</summary>
        public const float StartupGap = 0.18f;

        // ───────────── 토스트 (M14) ─────────────

        /// <summary>들어옴 0.15초 / 나감 0.20초 (OutCubic, 알파 + y -24 → 0)</summary>
        public const float ToastIn = 0.15f;
        public const float ToastOut = 0.20f;
        public const float ToastRise = 24f;
        /// <summary>같은 토스트가 다시 오면 1 → 1.06 → 1 (0.18초 OutBack)</summary>
        public const float ToastPunch = 0.18f;
        public const float ToastPunchScale = 1.06f;

        // ───────────── 탭 (M52) ─────────────

        /// <summary>선택 표시 막대 x 이동 0.18초 OutCubic</summary>
        public const float TabIndicator = 0.18f;
        /// <summary>선택 아이콘 0.20초 OutBack으로 1.12배 + 6px 올림 / 풀림 0.12초 OutCubic</summary>
        public const float TabIconSelect = 0.20f;
        public const float TabIconDeselect = 0.12f;
        public const float TabIconScale = 1.12f;
        public const float TabIconLift = 6f;
        /// <summary>패널 바꿈 · 쪽 바꿈 알파 0.12초 (선형, 미끄러짐·순차 등장 없음)</summary>
        public const float PanelFade = 0.12f;
        /// <summary>같은 탭 다시 누름 → 맨 위로 0.20초 OutCubic</summary>
        public const float ScrollToTop = 0.20f;

        // ───────────── 시트 (UX-SPEC-IA §1.5 · §4.8) ─────────────

        /// <summary>시트 열기 0.30초 OutCubic — p 0 → 1 (시트 위치 · 전투 올림 · 띠 여백 · HUD 흐림이 모두 같은 p를 따른다)</summary>
        public const float SheetOpen = 0.30f;
        /// <summary>시트 닫기 0.24초 OutCubic (지금 p에서)</summary>
        public const float SheetClose = 0.24f;
        /// <summary>도중에 방향을 바꾸거나 끌기를 놓은 뒤: 남은 몫 × 열기/닫기 시간, 최소 0.10초</summary>
        public const float SheetSettleMin = 0.10f;
        /// <summary>끌기를 놓을 때 이만큼 넘게 내려왔으면(p &lt; 1 − 0.25 = 0.75) 닫는다, 아니면 다시 연다</summary>
        public const float SheetDismissFraction = 0.25f;
        /// <summary>끌기를 놓을 때 아래로 이 속도(캔버스 px/초, 마지막 0.1초) 이상이면 닫는다</summary>
        public const float SheetDismissVelocity = 1200f;
        /// <summary>줄이기(Reduced): 미끄러짐 없이 시트 알파만 선형 0.10초 열기 / 0.08초 닫기 (전투 올림은 열 때 처음 · 닫을 때 끝에 한 번에)</summary>
        public const float SheetReducedIn = 0.10f;
        public const float SheetReducedOut = 0.08f;
        /// <summary>사냥 빛 켜고 끄기 0.12초 OutCubic (알파만 — 빛은 멈춰 있고 반복하지 않는다)</summary>
        public const float MainGlowFade = 0.12f;
        /// <summary>시트를 연 채 손대지 않으면 이만큼(초) 뒤 MAIN으로 (설정 GameSettings.SheetAutoReturn)</summary>
        public const float SheetAutoReturnSeconds = 60f;
        /// <summary>ContentArea를 켠 뒤 첫 SheetFirstFrames 프레임은 시트 시계를 이보다 크게 넘기지 않는다 (무거운 패널의 긴 첫 프레임이 미끄러짐 앞부분을 건너뛰지 않게, CR-13)</summary>
        public const float SheetFirstFrameMaxStep = 1f / 30f;
        /// <summary>시계 제한을 거는 첫 프레임 수</summary>
        public const int SheetFirstFrames = 2;
        /// <summary>자동 복귀 시계가 한 프레임에 더하는 최대 시간 (백그라운드에서 몇 분 만에 돌아온 첫 프레임에 바로 닫히지 않게, CR-25)</summary>
        public const float SheetIdleMaxStep = 0.1f;

        // ───────────── 숫자 · 채움 (M21 · M10 · M62 · M63) ─────────────

        /// <summary>재화 숫자 올라감 0.35초 OutCubic</summary>
        public const float CurrencyCount = 0.35f;
        /// <summary>재화 줄어듦 0.20초 + Negative → Text 색 0.30초</summary>
        public const float CurrencyDecrease = 0.20f;
        public const float CurrencyDecreaseTint = 0.30f;
        /// <summary>상단 바 전투력 숫자 0.5초</summary>
        public const float PowerCount = 0.5f;
        /// <summary>천장 막대 0.35초 + 반짝 0.25초 (Cream α0.6 → 0)</summary>
        public const float PityFill = 0.35f;
        public const float PityFlash = 0.25f;

        // ───────────── 펀치 · 배지 (M24 · M09) ─────────────

        public const float PunchS = 0.08f;
        public const float PunchM = 0.12f;
        public const float PunchL = 0.25f;
        /// <summary>펀치 시간 (OutBack)</summary>
        public const float PunchDuration = 0.22f;
        /// <summary>배지 나타남 0 → 1.2 → 1 (0.22초 OutBack) — 꺼졌다 켜질 때만</summary>
        public const float BadgeAppear = 0.22f;
        public const float BadgeAppearPeak = 1.2f;
        /// <summary>숫자 배지 숨쉬기: 3초마다 0.5초 동안 1 → 1.06 (전투 화면이 맨 앞일 때만, 공용 틱 하나)</summary>
        public const float BadgePulse = 0.5f;
        public const float BadgePulseInterval = 3f;
        public const float BadgePulseScale = 1.06f;
        /// <summary>모두 받기 버튼이 0 → 생길 때 0.85 → 1 (0.18초)</summary>
        public const float ClaimAllPop = 0.18f;
        public const float ClaimAllPopFrom = 0.85f;

        // ───────────── 막힘 흔들기 (M07 · M19 · M56) ─────────────

        /// <summary>막힘 흔들기 시간 (7/0.28, 10/0.25, 8/0.25 변형을 이것 하나로)</summary>
        public const float Deny = 0.26f;
        /// <summary>막힘 흔들기 폭 (px)</summary>
        public const float DenyAmplitude = 8f;

        /// <summary>
        /// 막힘 흔들기 x 오프셋: sin(t·40)·8·(1−t). t = 지난 시간 / Deny (0~1, 1 이상이면 0).
        /// 글자·비용 묶음에만 더한다 (버튼 루트에 쓰면 PressScale·레이아웃과 싸운다).
        /// </summary>
        public static float DenyOffset(float t)
        {
            if (!(t < 1f) || t < 0f) return 0f;
            return Mathf.Sin(t * 40f) * DenyAmplitude * (1f - t);
        }

        /// <summary>부족한 단추 안내 토스트 간격 (§3.8)</summary>
        public const float ShortfallToastThrottle = 1.5f;

        // ───────────── 길게 누르기 (M19) ─────────────

        /// <summary>반복 시작까지 0.35초 (계보 카드는 0.45초)</summary>
        public const float HoldDelay = 0.35f;
        public const float HoldDelayCard = 0.45f;
        /// <summary>반복 간격 0.17초 → 1.8초에 걸쳐 0.05초까지 (OutCubic)</summary>
        public const float HoldIntervalStart = 0.17f;
        public const float HoldIntervalEnd = 0.05f;
        public const float HoldRamp = 1.8f;
        /// <summary>연속 알약 펀치 1.15 (0.12초), 뗀 뒤 0.4초 머물고 0.25초 사라짐</summary>
        public const float ComboPunchScale = 1.15f;
        public const float ComboPunch = 0.12f;
        public const float ComboHold = 0.4f;
        public const float ComboFade = 0.25f;

        /// <summary>누른 지 held초일 때 반복 간격: Lerp(0.17, 0.05, OutCubic(held / 1.8))</summary>
        public static float HoldInterval(float held) =>
            Mathf.Lerp(HoldIntervalStart, HoldIntervalEnd, OutCubic(held / HoldRamp));

        // ───────────── 반짝 · 숨쉬기 (M35 · M36) ─────────────

        /// <summary>살 수 있게 됨 반짝 0.45초 (α 0 → 0.30 → 0), x10은 4초마다 한 번, 쓴 지 1.5초 안이면 쉰다</summary>
        public const float ShineSweep = 0.45f;
        public const float ShineAlpha = 0.30f;
        public const float ShineAmbientInterval = 4f;
        public const float ShineSpendQuiet = 1.5f;
        /// <summary>숨쉬는 빛 주기 1.6초 (탭 빛 α 0.30↔0.55, 덱 '+' 0.35↔0.8)</summary>
        public const float BreathePeriod = 1.6f;

        /// <summary>숨쉬기 값 (time = unscaled 초): min ↔ max를 period마다 사인으로 오간다. 줄이기면 min과 max의 가운데에 멈춘다</summary>
        public static float Breathe(float time, float min, float max, float period = BreathePeriod)
        {
            if (Reduced) return (min + max) * 0.5f;
            float k = 0.5f + 0.5f * Mathf.Sin(time * (2f * Mathf.PI / Mathf.Max(0.01f, period)));
            return Mathf.Lerp(min, max, k);
        }

        // ───────────── 날아가기 · 돌아가기 · 상점 (M78 · M72 · M56) ─────────────

        /// <summary>새 기능 카드 → 자리로 날아가기 0.55초 InOutSine, 베지어 96px, 1 → 0.7배, 도착 펀치 1.15 (0.2초)</summary>
        public const float FlyToSlot = 0.55f;
        public const float FlyToSlotArc = 96f;
        public const float FlyToSlotEndScale = 0.7f;
        public const float FlyToSlotPunchScale = 1.15f;
        public const float FlyToSlotPunch = 0.2f;
        /// <summary>돌아가기 칩: 0.2초 페이드 + 한 번 1.06 (반복 없음)</summary>
        public const float ReturnChipFade = 0.2f;
        public const float ReturnChipPulseScale = 1.06f;
        /// <summary>상점 구매: 카드 펀치 1.06 (0.08초 올라감, 0.14초 내려옴), 품절 0.2초 페이드, 0.15초 누르기 잠금</summary>
        public const float BuyPunchScale = 1.06f;
        public const float BuyPunchUp = 0.08f;
        public const float BuyPunchDown = 0.14f;
        public const float SoldOutFade = 0.2f;
        public const float BuyTapLock = 0.15f;

        // ───────────── 안내 말풍선 (M59) ─────────────

        /// <summary>버튼이 있는 대상은 0.35초 길게 눌러야 안내가 뜬다</summary>
        public const float TooltipLongPress = 0.35f;
        /// <summary>안내는 3초 뒤 저절로 닫힌다 (아무 곳이나 눌러도 닫힘)</summary>
        public const float TooltipAutoHide = 3f;
        public const float TooltipFadeIn = 0.12f;
        public const float TooltipFadeOut = 0.10f;

        // ───────────── 채움 (M62) ─────────────

        /// <summary>
        /// 진행 막대 채움을 목표로 옮긴다 (한 칸 정도는 약 0.2~0.3초, 빈 막대 → 가득은 약 0.43초 — 큰 차이일수록 빨리 출발). 줄어들거나 거의 같으면 바로 목표.
        /// MoveTowards(current, target, dt · max(1.2, (target − current) · 6)). dt는 unscaled. 다시 묶을 때·켜질 때는 부르는 쪽이 바로 맞춘다.
        /// </summary>
        public static float EaseFill(float current, float target, float dt)
        {
            if (target < current || Mathf.Abs(target - current) < 0.002f) return target;
            return Mathf.MoveTowards(current, target, dt * Mathf.Max(1.2f, (target - current) * 6f));
        }
    }

    /// <summary>토스트 종류 (M14): Info = 라벤더 점, Success = Positive 체크, Error = Negative '!' + 왼쪽 막대 · 막힘 소리 · 약한 진동</summary>
    public enum ToastKind
    {
        Info = 0,
        Success = 1,
        Error = 2,
    }

    /// <summary>비용 글자가 놓인 바탕 (M07, UITheme.CostShort): 어두운 버튼(보라·보조) / 금 버튼 / 양피지</summary>
    public enum CostSurface
    {
        Dark = 0,
        Gold = 1,
        Parchment = 2,
    }

    /// <summary>
    /// 탭 모양 (M53, TabStyle.Apply): Page = 쪽 탭 (선택 양피지 + Ink 굵게, 아님 TabNormal + SubText) /
    /// Option = 고르기 칸 (선택 PrimaryButton + Cream 굵게 + 2px Cream@0.6 테두리, 아님 Secondary + SubText)
    /// </summary>
    public enum TabVariant
    {
        Page = 0,
        Option = 1,
    }
}
