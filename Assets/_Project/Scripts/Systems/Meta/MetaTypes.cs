namespace IdleGame
{
    /// <summary>
    /// 메타 조건(MetaConditionType.Flag)이 읽는 1회성 플래그 이름. PlayerData.flags에 저장된다.
    /// UI가 "열어 봄" 같은 일을 알릴 때 ProgressTracker.SetFlag(MetaFlags.X)를 부른다.
    /// </summary>
    public static class MetaFlags
    {
        /// <summary>동화 인트로를 봄</summary>
        public const string IntroSeen = "meta.intro_seen";
        /// <summary>친구를 끌어 다른 방위에 세워 봄 (편성 끌어 놓기)</summary>
        public const string FormationDragged = "meta.formation_dragged";
        /// <summary>시너지 상세 팝업을 열어 봄</summary>
        public const string SynergyDetailViewed = "meta.synergy_detail_viewed";
        /// <summary>진화 기억도(친구 상세)를 열어 봄</summary>
        public const string EvolutionTreeViewed = "meta.evolution_tree_viewed";
        /// <summary>도감을 열어 봄</summary>
        public const string CodexOpened = "meta.codex_opened";
        /// <summary>잔향이를 끌어서 방향 소환해 봄</summary>
        public const string DirectionalSummon = "meta.directional_summon";
        /// <summary>첫 만남 확정 소환을 받음 (FTUE 10단계, 한 번만)</summary>
        public const string FirstMeetingSummoned = "meta.first_meeting_summoned";
        /// <summary>첫 원정 짧게 보내기를 씀 (FTUE 13단계, 한 번만)</summary>
        public const string ShortFirstExpeditionUsed = "meta.short_first_expedition_used";
        /// <summary>FTUE 바구니 미리 채우기를 씀 (FTUE 9단계, 한 번만)</summary>
        public const string IdleChestPrefilled = "meta.idle_chest_prefilled";

        /// <summary>
        /// 덱 안내 줄 "탭 = 자동 소환 · 끌기 = 방향 지정"을 거둘 조건 하나 (M36): 잔향이를 끌어서 방향을 정해 소환하는 데 성공함.
        /// SummonAimView가 끌어 소환에 성공하면 ProgressTracker.SetFlag로 세운다 (PlayerData.flags에 저장, 옛 세이브는 꺼진 채로 시작).
        /// 안내 줄은 CounterKey.EchoSummon ≥ 10 이고 이 플래그가 서 있으면 사라진다.
        /// </summary>
        public const string DeckDragHint = "hint.deck.drag";

        // ── 4차 (SPEC4 §2.1 PlayerData.flags — 세이브 구조는 그대로) ──

        /// <summary>첫 불 밝히기 큰 배너를 봄 (지금은 PlayerPrefs 같은 이름 키 — 세이브로 옮기는 것은 P4-3b, SPEC4 §13.5(d))</summary>
        public const string LampIntroSeen = "fx.lamp_intro_seen";

        /// <summary>보스 힌트 첫 만남 플래그 머리 (hint.boss.{보스 적 ID}, PRG-02 §13.1)</summary>
        public const string BossHintPrefix = "hint.boss.";
    }

    /// <summary>
    /// 코치마크/손가락이 가리킬 화면 대상의 경로 (Canvas 기준 Transform.Find 경로).
    /// 씬 빌더가 만드는 이름을 그대로 쓴다. 이름이 바뀌면 여기만 고친다.
    /// 메타 UI가 새로 만드는 대상(가이드 퀘스트 줄, 바구니 등)은 MetaSceneBuilder가 이 이름으로 만든다.
    /// 화면 틀(UX-SPEC-IA §5.8) 이후 고정 — 이름을 바꾸거나 다른 부모로 옮기지 않는다: 전투 HUD · 덱 · 램프 · 빠른 아이콘(QuickRail_L/R) ·
    /// 하단 탭 7칸(Tab_Main 포함) · 상단 바 우편 · ≡ 메뉴. 가방 · 설정은 ≡ 메뉴 안으로 가서 상단 바 경로가 없다 (MetaDataBuilder.ValidateTargetsInScene가 모두 확인).
    /// </summary>
    public static class CoachTargets
    {
        const string Battle = "SafeArea/StageArea/Battle";

        /// <summary>잔향이 덱 n번째 칸 (0부터)</summary>
        public static string DeckCard(int index) => $"{Battle}/Strip/DeckPanel/Card{index}";
        public const string DeckCard0 = Battle + "/Strip/DeckPanel/Card0";
        public const string LampButton = Battle + "/Strip/LampButton";
        public const string AutoButton = Battle + "/Hud/AutoButton";
        public const string SpeedButton = Battle + "/Hud/SpeedButton";
        public const string StagePanel = Battle + "/Hud/StagePanel";

        /// <summary>하단 탭 버튼 (IdleSceneBuilder: "Tab_{패널 이름}")</summary>
        public const string TabGrowth = "SafeArea/TabBar/Tab_GrowthPanel";
        public const string TabFormation = "SafeArea/TabBar/Tab_FormationPanel";
        public const string TabCharacters = "SafeArea/TabBar/Tab_CharacterPanel";
        public const string TabExpedition = "SafeArea/TabBar/Tab_ExpeditionPanel";
        public const string TabGacha = "SafeArea/TabBar/Tab_GachaPanel";
        public const string TabShop = "SafeArea/TabBar/Tab_ShopPanel";
        /// <summary>
        /// 가운데 사냥 버튼 (UX-SPEC-IA §2.2 · §5.8 — 누르면 MAIN). 시트 때문에 안 보이는 대상(시트 밑 덱 · 램프 · 가이드 퀘스트 줄,
        /// 시트가 열리면 사라지는 빠른 아이콘) 대신 손가락이 가는 곳 (CoachTargetResolver — UIFrame.IsUnderSheet · SheetFade)
        /// </summary>
        public const string TabMain = "SafeArea/TabBar/Tab_Main";

        /// <summary>탭 패널 (내부 버튼은 각 화면 담당이 확인해서 덧붙인다)</summary>
        public const string GrowthPanel = "SafeArea/ContentArea/GrowthPanel";
        public const string FormationPanel = "SafeArea/ContentArea/FormationPanel";
        public const string CharacterPanel = "SafeArea/ContentArea/CharacterPanel";
        public const string ExpeditionPanel = "SafeArea/ContentArea/ExpeditionPanel";
        public const string GachaPanel = "SafeArea/ContentArea/GachaPanel";
        public const string ShopPanel = "SafeArea/ContentArea/ShopPanel";

        /// <summary>상단 바 (UX-SPEC-IA §3.1 — 130px: 왼쪽 레벨 · 전투력, 오른쪽 단추 · 별조각 · 우편 · ≡ 메뉴)</summary>
        public const string TopBar = "SafeArea/TopBar";
        /// <summary>상단 바 우편 버튼 (이름 · 경로 그대로)</summary>
        public const string MailButton = TopBar + "/MailButton";
        /// <summary>상단 바 ≡ 메뉴 버튼 (UX-SPEC-IA §3.1 — 가방 · 공지 · 설정이 이 메뉴 안으로 갔다, 아이템 날아가기 도착지)</summary>
        public const string MenuButton = TopBar + "/MenuButton";

        // ── 메타 UI가 만드는 대상 (MetaSceneBuilder가 이 이름으로 만든다) ──

        /// <summary>가이드 퀘스트 줄 (전투 화면 왼쪽 아래, 덱 위)</summary>
        public const string GuideQuestBar = Battle + "/MetaHud/GuideQuestBar";
        /// <summary>수선 바구니 위젯 (전투 화면 오른쪽, 배속·AUTO 아래)</summary>
        public const string IdleChestWidget = Battle + "/MetaHud/IdleChestWidget";
        /// <summary>전투 화면 빠른 아이콘 수선 일지 (UX-SPEC-IA §3.3, 오른쪽 아래 주머니 — 시트가 열리면 사라진다)</summary>
        public const string JournalButton = Battle + "/MetaHud/QuickRail_R/JournalButton";
        /// <summary>전투 화면 빠른 아이콘 출석부 (UX-SPEC-IA §3.3, 왼쪽 아래 주머니 — 시트가 열리면 사라진다)</summary>
        public const string AttendanceButton = Battle + "/MetaHud/QuickRail_L/AttendanceButton";
        /// <summary>전투 화면 빠른 아이콘 이벤트 (UX-SPEC-IA §3.3, 왼쪽 위 주머니 — 별빛 사탕 날아가기 도착지, 가려지면 상점 탭으로)</summary>
        public const string EventButton = Battle + "/MetaHud/QuickRail_L/EventButton";
    }
}
