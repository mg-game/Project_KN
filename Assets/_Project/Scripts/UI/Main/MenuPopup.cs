using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// ≡ 메뉴 팝업 (UX-SPEC-IA §3.6, Popups/MenuPopup) — 상단 바 MenuButton(TopBarView)이 연다.
    /// 창 900 x (300 + 220·줄) (v1 두 줄 = 740, SPEC4 칸까지 네 줄 = 1180), 제목 '메뉴'. Window/Body/Grid (3열, 칸 240x196, 간격 24, 가운데, 창 위에서 176 (Body 136 + 격자 40))에
    /// JournalTile 수선 일지 · AttendanceTile 출석부 · EventTile 이벤트 ·
    /// [SPEC4 칸, UX-SPEC-IA §8.8 · 로드맵 델타 36] EventHubTile 이벤트(모음) · SeasonTile 산책 수첩 · CodexTile 도감 · AchievementsTile 수선점 기록 · CouponTile 쿠폰 ·
    /// BagTile 가방 · NoticeTile 공지 · SettingsTile 설정 —
    /// 잠긴 칸은 숨기고 격자가 다시 흐른다 (창 높이도 보이는 줄 수에 맞춘다). 칸을 누르면 이 팝업을 닫고 그 화면을 연다
    /// (일지 · 출석 · 이벤트는 빠른 아이콘과 같은 길 — 시트가 아이콘을 가린 동안에도 닿게 일부러 겹친다. 새로 열린 기능이면 먼저 본 것으로 표시 — CR-06).
    /// SPEC4 칸의 문: 이벤트 모음 = Events.IsUnlocked (이벤트 콘텐츠 + 3-1), 수첩 = Season.IsUnlocked (스트레치 — 콘텐츠가 꺼져 있으면 숨김),
    /// 도감 = Codex(1-10), 수선점 기록 = Achievements(2-3), 쿠폰 = 튜토리얼이 끝난 뒤. 받는 쪽: UINav.OpenEvents · OpenSeason · OpenCodex ·
    /// GoToFeature(Achievements) (P4-6 MetaNavigator), 쿠폰은 CouponPopup.
    /// EventTile 길: 이벤트 모음 칸이 보이면 이벤트 상점(이름표 '이벤트 상점' — 두 칸이 같은 곳으로 가지 않게), 이벤트 모음 칸이 없는 예전 장면이면
    /// 이벤트 모음이 열린 뒤 UINav.OpenEvents (아니면 지금처럼 이벤트 상점).
    /// 배지: 일지 숫자 · 출석 점 · 이벤트 점 · 수첩 점 · 도감 점 · 기록 점은 칸의 RedDotView가, 공지 숫자(안 읽은 공지 수)는 이 팝업이 쓴다.
    /// 이벤트 점(top.event)은 EventTile과 EventHubTile 둘에 붙고, 모음 칸이 보이는 동안 EventTile 쪽이 쉰다 (_eventTileDot.Suppressed).
    /// 열려 있는 동안만 기능 열림 · 공지 · 이벤트 바뀜을 듣는다 (닫혀 있으면 하는 일 없음).
    /// </summary>
    public class MenuPopup : UIPopup
    {
        /// <summary>한 줄 칸 수</summary>
        public const int Columns = 3;
        /// <summary>창 높이 = WindowBaseHeight + WindowRowHeight × 줄 (§3.6: 300 + 220·줄)</summary>
        public const float WindowBaseHeight = 300f;
        /// <summary>줄마다 늘어나는 창 높이 (칸 196 + 간격 24)</summary>
        public const float WindowRowHeight = 220f;

        /// <summary>칸 이름표 (빌더와 같이 쓴다 — EventTile은 이벤트 모음 칸이 보이면 '이벤트 상점')</summary>
        public const string EventTileLabel = "이벤트";
        public const string EventShopTileLabel = "이벤트 상점";
        public const string EventHubTileLabel = "이벤트";
        public const string SeasonTileLabel = "산책 수첩";
        public const string CodexTileLabel = "도감";
        public const string AchievementsTileLabel = "수선점 기록";
        public const string CouponTileLabel = "쿠폰";

        [SerializeField] Button _journalTile;
        [SerializeField] Button _attendanceTile;
        [SerializeField] Button _eventTile;
        [SerializeField] Button _bagTile;
        [SerializeField] Button _noticeTile;
        [SerializeField] Button _settingsTile;
        [Tooltip("공지 칸 숫자 배지 (안 읽은 공지 수)")]
        [SerializeField] Badge _noticeBadge;

        [Header("SPEC4 칸 (UX-SPEC-IA §8.8 — 다시 빌드한 장면에만, 없으면 건너뛴다)")]
        [SerializeField] Button _eventHubTile;
        [SerializeField] Button _seasonTile;
        [SerializeField] Button _codexTile;
        [SerializeField] Button _achievementsTile;
        [SerializeField] Button _couponTile;
        [Tooltip("EventTile 이름표 ('이벤트' / 이벤트 모음 칸이 보이면 '이벤트 상점')")]
        [SerializeField] TMP_Text _eventTileLabel;
        [Tooltip("EventTile의 이벤트 점(top.event) — 이벤트 모음 칸이 보이는 동안은 그 칸이 점을 맡으므로 쉰다")]
        [SerializeField] RedDotView _eventTileDot;

        bool _subscribed;
        /// <summary>EventTile이 이벤트 상점으로 가는지 (Refresh가 정한다 — 처음은 지금처럼 이벤트 상점)</summary>
        bool _eventTileIsShop = true;

        /// <summary>칸이 rows줄일 때의 창 높이 (줄은 1 이상으로 센다)</summary>
        public static float WindowHeight(int rows) => WindowBaseHeight + WindowRowHeight * Mathf.Max(1, rows);

        /// <summary>보이는 칸 수 → 줄 수 (⌈칸/3⌉, 칸이 없어도 1)</summary>
        public static int RowsFor(int tiles) => Mathf.Max(1, (tiles + Columns - 1) / Columns);

        /// <summary>
        /// EventTile의 길 (테스트용 순수 함수): 이벤트 모음 칸이 보이면 이벤트 상점 (true — 두 칸이 같은 곳으로 가지 않게),
        /// 이벤트 모음 칸이 없는 장면에서 이벤트 모음이 열렸으면 모음 (false), 이벤트 모음이 아직 없으면 이벤트 상점 (true)
        /// </summary>
        internal static bool EventTileGoesToShop(bool hubTileShown, bool hubOpen) => hubTileShown || !hubOpen;

        /// <summary>EventTile의 이벤트 점을 쉬게 하는지 (테스트용): 이벤트 모음 칸이 보일 때만 (그 칸이 같은 점 top.event를 보여 준다)</summary>
        internal static bool EventTileDotSuppressed(bool hubTileShown) => hubTileShown;

        protected override void Awake()
        {
            base.Awake();
            Listen(_journalTile, OnJournal);
            Listen(_attendanceTile, OnAttendance);
            Listen(_eventTile, OnEvent);
            Listen(_bagTile, OnBag);
            Listen(_noticeTile, OnNotice);
            Listen(_settingsTile, OnSettings);
            Listen(_eventHubTile, OnEventHub);
            Listen(_seasonTile, OnSeason);
            Listen(_codexTile, OnCodex);
            Listen(_achievementsTile, OnAchievements);
            Listen(_couponTile, OnCoupon);
        }

        /// <summary>닫기 연출이 끝나 꺼질 때 (OnClose를 거치지 않고 꺼지는 길 — ForceHidden 등 — 도 여기서 듣기를 멈춘다)</summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            Unsubscribe();
        }

        void OnDestroy() => Unsubscribe();

        protected override void OnOpen()
        {
            Subscribe();
            Refresh();
        }

        protected override void OnClose() => Unsubscribe();

        static void Listen(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        // ───────────── 칸 보이기 · 배지 ─────────────

        void Subscribe()
        {
            if (_subscribed || !Game.IsReady) return;
            _subscribed = true;
            if (Game.Features != null) Game.Features.Changed += Refresh;
            if (Game.Notice != null) Game.Notice.Changed += RefreshNoticeBadge;
            if (Game.Events != null) Game.Events.Changed += Refresh;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (!Game.IsReady) return;
            if (Game.Features != null) Game.Features.Changed -= Refresh;
            if (Game.Notice != null) Game.Notice.Changed -= RefreshNoticeBadge;
            if (Game.Events != null) Game.Events.Changed -= Refresh;
        }

        /// <summary>잠긴 칸을 숨기고(격자가 다시 흐른다) 창 높이를 보이는 줄 수에 맞춘 뒤 공지 숫자를 쓴다</summary>
        void Refresh()
        {
            bool meta = MetaUi.IsActive;
            int visible = 0;
            visible += ShowTile(_journalTile, meta && Game.Features.IsUnlocked(FeatureId.Missions));
            // 출석부는 튜토리얼이 끝난 뒤에만 (빠른 아이콘 · 예전 상단 바와 같은 문)
            visible += ShowTile(_attendanceTile, meta && Game.Features.IsUnlocked(FeatureId.Attendance) && !IsTutorialActive);
            visible += ShowTile(_eventTile, meta && Game.Features.IsUnlocked(FeatureId.Events));
            // SPEC4 칸 (예전 장면에는 없다 — ShowTile이 0을 돌려준다)
            bool hubOpen = meta && IsHubOpen;
            int hubTile = ShowTile(_eventHubTile, hubOpen);
            visible += hubTile;
            visible += ShowTile(_seasonTile, meta && IsSeasonOpen);
            visible += ShowTile(_codexTile, meta && Game.Features.IsUnlocked(FeatureId.Codex));
            visible += ShowTile(_achievementsTile, meta && Game.Features.IsUnlocked(FeatureId.Achievements));
            visible += ShowTile(_couponTile, meta && Game.Coupons != null && !IsTutorialActive);
            SetEventTileRoute(EventTileGoesToShop(hubTile > 0, hubOpen), hubTile > 0);
            visible += ShowTile(_bagTile, true);
            visible += ShowTile(_noticeTile, true);
            visible += ShowTile(_settingsTile, true);
            FitWindow(visible);
            RefreshNoticeBadge();
        }

        static bool IsTutorialActive => Game.Tutorial != null && Game.Tutorial.IsActive;

        /// <summary>이벤트 모음이 열렸는지 (EVT-01: 이벤트 콘텐츠가 있고 3-1 · 설치 3일째)</summary>
        static bool IsHubOpen => Game.Events != null && Game.Events.IsUnlocked;

        /// <summary>별빛 산책 수첩이 열렸는지 (Q-06 스트레치: 수첩 칸 콘텐츠가 없으면 닫힘)</summary>
        static bool IsSeasonOpen => Game.Season != null && Game.Season.IsUnlocked;

        /// <summary>
        /// EventTile 길 · 이름표 · 점 (이름표는 바뀔 때만 쓴다 — 미리 만든 문자열이라 할당 없음).
        /// 점: 이벤트 모음 칸이 보이면 그 칸이 top.event를 맡으므로 EventTile 점은 쉬고, 모음 칸이 숨은 동안(이벤트 콘텐츠 전 · 3-1 전)은
        /// EventTile이 이벤트 새로 열림(NEW) · 받을 것 점을 보여 준다 (≡ 메뉴 어딘가에 늘 이벤트 점이 있게)
        /// </summary>
        void SetEventTileRoute(bool toShop, bool hubTileShown)
        {
            _eventTileIsShop = toShop;
            if (_eventTileDot != null) _eventTileDot.Suppressed = EventTileDotSuppressed(hubTileShown);
            if (_eventTileLabel == null) return;
            string label = hubTileShown ? EventShopTileLabel : EventTileLabel;
            if (_eventTileLabel.text != label) _eventTileLabel.text = label;
        }

        static int ShowTile(Button tile, bool visible)
        {
            if (tile == null) return 0;
            if (tile.gameObject.activeSelf != visible) tile.gameObject.SetActive(visible);
            return visible ? 1 : 0;
        }

        /// <summary>창 높이 = 300 + 220 × 보이는 줄 (v1 여섯 칸 두 줄 740, SPEC4 칸까지 열리면 네 줄 1180)</summary>
        void FitWindow(int visibleTiles)
        {
            if (_window == null) return;
            float height = WindowHeight(RowsFor(visibleTiles));
            var size = _window.sizeDelta;
            if (!Mathf.Approximately(size.y, height)) _window.sizeDelta = new Vector2(size.x, height);
        }

        void RefreshNoticeBadge()
        {
            if (_noticeBadge == null) return;
            _noticeBadge.SetCount(Game.IsReady && Game.Notice != null ? Game.Notice.UnreadCount : 0);
        }

        // ───────────── 칸 누름 (이 팝업을 닫고 그 화면으로) ─────────────

        void OnJournal()
        {
            Leave(FeatureId.Missions);
            UINav.OpenJournal();
        }

        void OnAttendance()
        {
            Leave(FeatureId.Attendance);
            UINav.OpenAttendance();
        }

        /// <summary>
        /// 이벤트: 이벤트 모음 칸이 보이면 이벤트 상점 (별빛 사탕), 이벤트 모음 칸이 없는 예전 장면이면 빠른 아이콘과 같은 길 —
        /// 이벤트 모음이 열렸으면 UINav.OpenEvents (P4-6, 모음 팝업이 없으면 받는 쪽이 이벤트 상점으로), 아니면 이벤트 상점
        /// </summary>
        void OnEvent()
        {
            Leave(FeatureId.Events);
            if (_eventTileIsShop) UINav.GoToSource(ItemSourceType.Shop, ShopDatabase.EventShopId);
            else UINav.OpenEvents();
        }

        /// <summary>이벤트 모음 (EVT-01 EventHubPopup, P4-6 받는 쪽)</summary>
        void OnEventHub()
        {
            Leave(FeatureId.Events);
            UINav.OpenEvents();
        }

        /// <summary>별빛 산책 수첩 (Q-06 SeasonPassPopup, P4-6 받는 쪽)</summary>
        void OnSeason()
        {
            Close();
            UINav.OpenSeason();
        }

        /// <summary>도감 (Q-05 CodexPopup, P4-6 받는 쪽)</summary>
        void OnCodex()
        {
            Leave(FeatureId.Codex);
            UINav.OpenCodex();
        }

        /// <summary>수선점 기록 (Q-04 — MetaNavigator가 수선 일지 기록 탭으로)</summary>
        void OnAchievements()
        {
            Leave(FeatureId.Achievements);
            UINav.GoToFeature(FeatureId.Achievements);
        }

        /// <summary>쿠폰 입력 (EVT-02 CouponPopup — 설정 › 계정·데이터의 '입력'과 같은 팝업)</summary>
        void OnCoupon()
        {
            Close();
            if (TryGetPopup<CouponPopup>(out var coupon)) coupon.Open();
        }

        void OnBag()
        {
            Close();
            if (TryGetPopup<BagPopup>(out var bag)) bag.Open();
        }

        void OnNotice()
        {
            Close();
            if (TryGetPopup<NoticePopup>(out var notice)) notice.OpenList();
        }

        void OnSettings()
        {
            Close();
            if (TryGetPopup<SettingsPopup>(out var settings)) settings.Open();
        }

        /// <summary>이 팝업을 닫고, 그 기능이 새로 열림(NEW)이면 본 것으로 표시한다 (CR-06 — 이벤트는 따로 지워 주는 곳이 없다)</summary>
        void Leave(FeatureId feature)
        {
            Close();
            if (MetaUi.IsActive && Game.Features.IsNew(feature)) Game.Features.MarkSeen(feature);
        }

        static bool TryGetPopup<T>(out T popup) where T : UIPopup
        {
            popup = null;
            var ui = UIManager.Instance;
            return ui != null && ui.TryGet(out popup);
        }
    }
}
