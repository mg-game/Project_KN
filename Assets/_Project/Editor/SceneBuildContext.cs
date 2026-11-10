using System.Collections.Generic;
using IdleGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 씬 빌더들이 공유하는 생성 컨텍스트. IdleSceneBuilder가 기본 틀(캔버스/영역)을 만들고
    /// 각 화면 빌더(BattleSceneBuilder / CharacterSceneBuilder / MenuSceneBuilder)에 넘긴다.
    /// 화면 빌더는 만든 팝업을 Popups에 추가한다.
    /// </summary>
    internal sealed class SceneBuildContext
    {
        // 세로 1080x1920 기준 레이아웃 (화면 틀 UX-SPEC-IA §1.1 — 값은 UIFrame 하나에서 온다)
        /// <summary>짧은 상단 바 130 (MAIN · SHEET 같음)</summary>
        public const float TopBarHeight = UIFrame.TopBarHeight;
        /// <summary>하단 탭 바 170 (3 + 사냥 + 3)</summary>
        public const float TabBarHeight = UIFrame.TabBarHeight;
        /// <summary>탭 내용 = ContentArea 780 (시트가 열리면 지금의 970~1750 자리 그대로 — 패널 배치는 바뀌지 않는다)</summary>
        public const float ContentHeight = UIFrame.SheetContentHeight;
        /// <summary>시트 머리 56 (ContentArea 위 끝에 붙는다 — 손잡이 · 돌아가기 칩)</summary>
        public const float SheetHeaderHeight = UIFrame.SheetHeaderHeight;
        /// <summary>시트 전체 836 = 머리 56 + 내용 780 (탭 여섯 모두 같은 높이)</summary>
        public const float SheetHeight = UIFrame.SheetHeight;
        /// <summary>전투 아래 잔향이 덱 띠 156 (전장 아래 끝 = 이 띠 위 끝)</summary>
        public const float StripHeight = UIFrame.StripHeight;

        public GameContent Content;
        public GameConfig Config => Content.Config;
        public ItemDatabase Items => Content.Items;
        public CharacterDatabase Characters => Content.Characters;

        public RectTransform Canvas;
        public RectTransform SafeArea;
        /// <summary>상단 바와 탭 바 사이의 전투 화면 영역 (130~1750, MAIN · SHEET 모두 세로 전체 — 시트가 열리면 아래쪽을 덮는다)</summary>
        public RectTransform StageArea;
        /// <summary>
        /// 탭 패널들이 들어가는 영역 = 시트 (높이 ContentHeight, 열리면 탭 바 바로 위). 중첩 Canvas + GraphicRaycaster + CanvasGroup,
        /// 처음엔 꺼져 있다 (MAIN — SheetController가 연다). 패널은 이것의 바로 아래 자식 "*Panel"
        /// </summary>
        public RectTransform ContentArea;
        /// <summary>시트 머리 "ContentArea/SheetHeader" (1080x56, 내용 위 끝 — DragArea · Handle · HandleHit · ReturnChip · RightSlot). 틀을 만들 때 채운다</summary>
        public RectTransform SheetHeader = null;
        /// <summary>탭 시트 (TabBar의 SheetController — 탭 바를 만들 때 채운다)</summary>
        public SheetController Sheet = null;
        public RectTransform PopupRoot;

        // ── 캔버스 층 (UX-SPEC §3.1, 아래 → 위): Background → SafeArea → Popups → TooltipLayer → TutorialOverlay → FanfareLayer
        //    → SystemPopups → ToastLayer. IdleSceneBuilder가 만들고 채운다 (만들기 전에는 null) ──

        /// <summary>안내 말풍선 층 (Popups 바로 위, 안전 영역). InfoTooltip이 여기 산다</summary>
        public RectTransform TooltipLayer = null;
        /// <summary>시스템 팝업 층 (확인 MessagePopup · 부족 ShortfallPopup — 튜토리얼 · 팡파르 위). 이 층의 팝업도 Popups 목록에 넣는다</summary>
        public RectTransform SystemPopupRoot = null;
        /// <summary>토스트 층 (맨 위, 안전 영역)</summary>
        public RectTransform ToastLayer = null;
        /// <summary>안내 말풍선 (SceneParts.BuildInfoTooltip(TooltipLayer)의 결과)</summary>
        public InfoTooltipView InfoTooltip = null;

        // ── 메타 빌더용 (IdleSceneBuilder가 해당 화면을 만든 뒤 채운다) ──

        /// <summary>상단 바 (FrameSceneBuilder.BuildTopBar 뒤에 채움 — UX-SPEC-IA §3.1)</summary>
        public RectTransform TopBar;
        /// <summary>전투 화면 루트 "Battle" (BattleSceneBuilder.Build 뒤에 채움). 메타 HUD는 여기 "MetaHud" 아래에 만든다</summary>
        public RectTransform BattleRoot;
        /// <summary>하단 탭 바 (옆 탭 버튼 이름 "Tab_{패널 이름}", 가운데 "Tab_Main" — 강화 · 편성 · 친구들 | 사냥 | 원정 · 소환 · 상점)</summary>
        public RectTransform TabBar;
        /// <summary>하단 옆 탭 버튼 6개 (순서 = MainTab — 가운데 사냥은 TabMain)</summary>
        public Button[] TabButtons;
        /// <summary>
        /// 가운데 사냥 버튼 "Tab_Main" (UX-SPEC-IA §2.2 — 7칸 탭 바의 가운데 192x170). TabButtons(6개, 순서 = MainTab)와 TabGate 배열에는 넣지 않는다.
        /// 탭 바를 만들 때 채운다 (그 전 · 예전 6칸 탭 바에서는 null — 쓰는 쪽은 null을 확인한다. 메타는 여기에 RedDotAnyView 점을 붙인다)
        /// </summary>
        public Button TabMain = null;
        public MainTabController TabController;
        /// <summary>
        /// 캔버스의 UIManager. 메타 발표자는 이 참조를 직렬화해 Awake에서 StartupHandler를 등록한다
        /// (UIManager.Instance는 Awake 순서에 따라 아직 없을 수 있다)
        /// </summary>
        public UIManager UI;

        /// <summary>공용 아이템 칸 프리팹 (ItemSlotView)</summary>
        public ItemSlotView ItemSlotPrefab;

        public readonly List<UIPopup> Popups = new();
    }
}
