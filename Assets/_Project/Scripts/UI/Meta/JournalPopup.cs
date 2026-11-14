using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선 일지 940×1500 (Q-02 · M60A · M51 · M53 · M62 · M72 · M61). 쪽 탭(TabStyle Page) [오늘][이번 주][수선점 기록(업적 개방 뒤)],
    /// "초기화까지 7:12:03", 점수 막대 + 상자 5개(20/40/60/80/100, 상자 위 보상 이름표), 미션 줄 860×150 ([가기 ›]/[받기]/[완료]),
    /// 아래 오른쪽 [모두 받기] 380×96 (금, 받을 수가 배지 숫자 — 이름에 '(N)'을 붙이지 않는다, 받을 것이 없으면 숨김 · 0에서 생길 때 0.85 → 1 튀어나옴, M51).
    /// 미션 목록·상자 상태는 MissionSystem이 채우는 목록을 재사용하고, 줄 순서는 열 때 한 번 정한다
    /// (받을 수 있음 → 진행 중 → 완료). 받는 동안 줄이 뛰어다니지 않게 열려 있는 동안은 순서를 유지한다.
    /// [가기 ›]가 탭으로 가면 시트 머리에 '‹ 수선 일지로' (누르면 그 쪽 탭으로 일지를 다시 연다 — UINav가 떠날 때의 틀을 기억한다, M72 · CR-05).
    /// 상자를 받으면 MissionChest 소리.
    /// 4차 (SPEC4 §6.1 · §13.6, RM-2 P4-6):
    ///   · [수선점 기록] (업적 2-3 개방 뒤): 요약 카드 (메달 · '모은 기록 {c}/{t}' · 막대 · '받을 수 있는 기록 {n}개') + [진행 중][완료] + 기록 줄
    ///     (MissionRowView ← JournalRowModel: 단계 점 · 숨은 기록 '???' + 힌트). [모두 받기]가 기록을 모두 받는다 (AchievementClaim 소리), 탭 점 = _tabBadges[2].
    ///   · [첫 주] (StarterWeekSystem.IsActive일 때 맨 앞에 끼운다, 받을 것이 있으면 이 탭부터): 머리 '새내기 수선공의 첫 주' + 'D-{n}' 알약 + 점수 막대와
    ///     상자 5개 (30/60/90/120/150) + 날 칩 7개 ('1일'…'7일' — 잠긴 날은 자물쇠 · '{d}일째에 열려요', 받을 것이 있는 날은 점) + 과제 줄 ('+5' 알약 · 잠김 알약).
    ///     [모두 받기]가 열린 날의 과제 · 상자를 모두 받는다, 탭 점 = _tabBadges[3].
    ///   · 탭이 4개면 폭 150으로 줄여 한 줄에 놓는다 (초기화 시간 자리는 그대로). 새 부품은 장면을 다시 만들기 전(G3)에는 없어도 된다 — 그때는 예전 3탭 그대로.
    /// 탭 자동 이동 (UI_InGame §4.3 공통 규칙, 공용 부품 AutoTabRunner):
    ///   · [모두 받기]로 지금 탭의 받을 것이 0이 되고 다른 탭에 남아 있으면, UIMotion.AutoTabDelay(0.9초, 움직임 줄이기 0.4초) 뒤
    ///     화면 순서(첫 주 → 오늘 → 이번 주 → 기록)로 오른쪽의 받을 탭으로 한 번 넘어간다. 맨 끝이면 처음으로 돈다. 도착 탭 글자가 한 번 톡 (줄이기면 없음).
    ///   · 넘기지 않는 경우: 줄의 [받기] (골라 받는 중), 기다리는 동안 화면을 누름, 튜토리얼 중, 팝업이 닫힘, 다른 팝업이 위에 뜸, 잠기거나 숨은 탭.
    ///   · 열 때도 같은 순서로 받을 것이 있는 첫 탭부터 연다 (돌아가기 칩 · OpenRecords · OpenMissions로 정한 쪽은 그대로).
    /// </summary>
    public class JournalPopup : UIPopup
    {
        enum Tab
        {
            Daily = 0,
            Weekly = 1,
            Records = 2,
            Starter = 3,
        }

        [Header("탭")]
        [SerializeField] Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] Image[] _tabImages = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _tabLabels = Array.Empty<TMP_Text>();
        [Tooltip("[오늘] [이번 주] 탭의 받을 수 있는 개수")]
        [SerializeField] Badge[] _tabBadges = Array.Empty<Badge>();
        [SerializeField] TMP_Text _resetText;

        [Header("점수")]
        [SerializeField] GameObject _missionsPanel;
        [SerializeField] TMP_Text _scoreLabel;
        [SerializeField] TMP_Text _scoreText;
        [SerializeField] TMP_Text _scoreMaxText;
        [SerializeField] RectTransform _bar;
        [SerializeField] Image _barFill;
        [SerializeField] RectTransform _chestRoot;
        [SerializeField] MissionChestView _chestTemplate;

        [Header("목록")]
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _listContent;
        [SerializeField] MissionRowView _rowTemplate;
        [SerializeField] TMP_Text _emptyText;

        [Header("아래")]
        [SerializeField] Button _claimAllButton;
        [SerializeField] TMP_Text _claimAllLabel;
        [Tooltip("모두 받기 개수 배지 (UIKit.ClaimAllButton)")]
        [SerializeField] Badge _claimAllBadge;
        [SerializeField] TMP_Text _summaryText;

        [Header("수선점 기록")]
        [SerializeField] GameObject _recordsPanel;
        [SerializeField] TMP_Text _recordsCountText;
        [SerializeField] Image _recordsBarFill;
        [SerializeField] TMP_Text _recordsClaimableText;
        [Tooltip("[진행 중][완료]")]
        [SerializeField] MetaSegmentedControl _recordsSegments;
        [SerializeField] ScrollRect _recordsScroll;
        [SerializeField] RectTransform _recordsListContent;
        [SerializeField] TMP_Text _recordsEmptyText;
        [Tooltip("숨은 기록 아이콘 (UiArt.Medal(Hidden))")]
        [SerializeField] Sprite _hiddenRecordIcon;
        [Tooltip("준비 중 카드 (예전 자리표시 — 새 부품이 없을 때만 보인다)")]
        [SerializeField] GameObject _recordsPlaceholder;

        [Header("첫 주")]
        [Tooltip("[첫 주] 쪽 (StarterWeekPanelView — 없으면 첫 주 탭을 보이지 않는다)")]
        [SerializeField] StarterWeekPanelView _starterView;

        [Header("연출")]
        [SerializeField] RectTransform _floatLayer;
        [SerializeField] TMP_Text _pointsFloat;
        [SerializeField] MetaSparkleBurst _sparkles;

        /// <summary>[가기 ›]로 탭에 갈 때 시트 머리 돌아가기 칩 이름 (M72)</summary>
        const string ReturnLabel = "‹ 수선 일지로";

        readonly List<MissionStatus> _statuses = new();
        readonly List<MissionChestStatus> _chests = new();
        readonly List<string> _orderIds = new();
        readonly List<MissionStatus> _ordered = new();
        readonly List<MissionStatus> _claimableBefore = new();
        /// <summary>기록 [모두 받기] 앞에 받을 수 있던 줄 (기록 줄 ID — 받으면 줄이 다음 단계로 바뀌고 순서도 바뀐다)</summary>
        readonly List<string> _stampKeys = new();
        MetaViewPool<MissionRowView> _rows;
        MetaViewPool<MissionChestView> _chestViews;
        Action<MissionRowView> _onRow;
        Action<MissionChestView> _onChest;
        Tab _tab;
        bool _subscribed;
        float _timer;
        float _barTarget;
        /// <summary>점수 막대가 지금 보여 주는 비율 (목표까지 천천히 따라간다)</summary>
        float _barShown;
        int _shownPoints = -1;
        Coroutine _float;
        Coroutine _claimAllPop;
        /// <summary>모두 받기에 마지막으로 보인 수 (0 → 생길 때만 튀어나온다)</summary>
        int _shownClaimable = -1;
        /// <summary>돌아가기 칩으로 다시 열 때 고를 쪽 (−1 = 받을 것 기준으로 고른다)</summary>
        int _reopenTab = -1;

        // ── 4차 ──
        /// <summary>첫 주 날 수 (StarterWeekPanelView.Days — 빌더가 쓴다)</summary>
        public const int StarterDays = StarterWeekPanelView.Days;
        readonly List<AchievementLineStatus> _lines = new();
        MetaViewPool<MissionRowView> _recordRows;
        Action<MissionRowView> _onRecordRow;
        bool _recordsSubscribed;
        bool _starterSubscribed;
        /// <summary>기록 탭 [진행 중](0) / [완료](1)</summary>
        int _recordsSegment;
        /// <summary>탭 줄에 마지막으로 놓은 보이는 탭 수 (바뀔 때만 다시 놓는다)</summary>
        int _laidOutTabs = -1;

        // ── 탭 자동 이동 (UI_InGame §4.3 공통 규칙) ──
        const int TabCount = 4;
        /// <summary>화면 탭 순서 (LayoutTabs와 같다): 첫 주 → 오늘 → 이번 주 → 기록</summary>
        static readonly int[] TabOrder = { (int)Tab.Starter, (int)Tab.Daily, (int)Tab.Weekly, (int)Tab.Records };
        /// <summary>탭마다 보이는지 · 받을 수 (Tab 값으로 찾는다, FillTabState가 채운다)</summary>
        readonly bool[] _tabVisible = new bool[TabCount];
        readonly int[] _tabClaimable = new int[TabCount];
        /// <summary>탭 자동 이동 공용 부품 (Awake에서 만든다)</summary>
        AutoTabRunner _autoTab;

        MissionPeriod Period => _tab == Tab.Weekly ? MissionPeriod.Weekly : MissionPeriod.Daily;

        protected override void Awake()
        {
            base.Awake();
            _autoTab = new AutoTabRunner(this);
            _rows = new MetaViewPool<MissionRowView>(_rowTemplate, _listContent);
            _chestViews = new MetaViewPool<MissionChestView>(_chestTemplate, _chestRoot);
            _onRow = OnRowButton;
            _onChest = OnChest;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var tab = (Tab)i;
                _tabButtons[i].onClick.AddListener(() => SelectTab(tab));
            }
            _claimAllButton.onClick.AddListener(OnClaimAll);
            if (_pointsFloat != null) _pointsFloat.gameObject.SetActive(false);
            _onRecordRow = OnRecordRow;
            if (_rowTemplate != null && _recordsListContent != null) _recordRows = new MetaViewPool<MissionRowView>(_rowTemplate, _recordsListContent);
            if (_recordsSegments != null) _recordsSegments.Changed += OnRecordsSegment;
            if (_starterView != null) _starterView.GoRequested += GoFromRow;
        }

        protected override void OnOpen()
        {
            if (!MetaUi.IsActive)
            {
                Close();
                return;
            }
            if (!_subscribed)
            {
                Game.Missions.Changed += OnMissionsChanged;
                _subscribed = true;
            }
            if (!_recordsSubscribed && Game.Achievements != null)
            {
                Game.Achievements.Changed += OnMissionsChanged;
                _recordsSubscribed = true;
            }
            if (!_starterSubscribed && Game.StarterWeek != null)
            {
                Game.StarterWeek.Changed += OnMissionsChanged;
                _starterSubscribed = true;
            }
            if (Game.Features.IsNew(FeatureId.Missions)) Game.Features.MarkSeen(FeatureId.Missions);
            _shownClaimable = -1;
            _laidOutTabs = -1;
            // 돌아가기 칩 · OpenRecords · OpenMissions로 정한 쪽이 있으면 그 쪽, 아니면 화면 순서(첫 주 → 오늘 → 이번 주 → 기록)로 받을 것이 있는 첫 탭 (없으면 오늘)
            Tab tab;
            if (_reopenTab >= 0)
            {
                tab = (Tab)_reopenTab;
            }
            else
            {
                FillTabState();
                tab = (Tab)FirstClaimTab(_tabVisible, _tabClaimable);
                if (tab == Tab.Records) _recordsSegment = 0;
            }
            _reopenTab = -1;
            SelectTab(tab);
        }

        protected override void OnClose()
        {
            _autoTab?.Stop();
            if (_float != null) StopCoroutine(_float);
            _float = null;
            _claimAllPop = null;
            if (_claimAllButton != null) _claimAllButton.transform.localScale = Vector3.one;
            if (_pointsFloat != null) _pointsFloat.gameObject.SetActive(false);
            if (_recordsSubscribed)
            {
                _recordsSubscribed = false;
                if (Game.IsReady && Game.Achievements != null) Game.Achievements.Changed -= OnMissionsChanged;
            }
            if (_starterSubscribed)
            {
                _starterSubscribed = false;
                if (Game.IsReady && Game.StarterWeek != null) Game.StarterWeek.Changed -= OnMissionsChanged;
            }
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.IsReady && Game.Missions != null) Game.Missions.Changed -= OnMissionsChanged;
        }

        /// <summary>첫 주 탭을 보일지 (탭 · 쪽 부품이 있고 첫 주가 진행 중)</summary>
        bool StarterVisible() =>
            _tabButtons.Length > (int)Tab.Starter && _tabButtons[(int)Tab.Starter] != null && _starterView != null && StarterWeekPanelView.IsAvailable;

        /// <summary>기록 탭의 새 부품이 있는지 (다시 만들기 전 장면은 예전 준비 중 카드)</summary>
        bool RecordsBuilt => _recordRows != null;

        void OnMissionsChanged() => Refresh(false);

        void SelectTab(Tab tab)
        {
            _autoTab?.Stop();
            if (tab == Tab.Records && !Game.Features.IsUnlocked(FeatureId.Achievements)) return;
            if (tab == Tab.Starter && !StarterVisible()) tab = Tab.Daily;
            _tab = tab;
            // 쪽 탭 (M53 Page): 고른 탭 양피지 + Ink 굵게, 아닌 탭 TabNormal + SubText
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var image = i < _tabImages.Length ? _tabImages[i] : null;
                var label = i < _tabLabels.Length ? _tabLabels[i] : null;
                TabStyle.Apply(image, label, i == (int)tab, TabVariant.Page);
            }
            if (tab == Tab.Records && Game.Features.IsNew(FeatureId.Achievements)) Game.Features.MarkSeen(FeatureId.Achievements);
            _shownPoints = -1;
            _orderIds.Clear();
            Refresh(true);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            if (tab == Tab.Records && _recordsScroll != null) _recordsScroll.verticalNormalizedPosition = 1f;
            // 첫 주 쪽: 받을 것이 있는 첫 열린 날(없으면 오늘)을 고른다 — 열 때 · 탭을 누를 때
            if (tab == Tab.Starter) _starterView.Show();
        }

        void Update()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            // 자동 이동을 기다리는 동안 화면을 누르면 지금 탭에 머문다 (UI_InGame §4.3)
            _autoTab?.Tick();
            float dt = Time.unscaledDeltaTime;
            if (_barFill != null && !Mathf.Approximately(_barShown, _barTarget))
            {
                _barShown = Mathf.MoveTowards(_barShown, _barTarget, dt * 1.6f);
                MetaUi.SetBar(_barFill, _barShown);
            }
            _timer -= dt;
            if (_timer <= 0f) RefreshTimer();
        }

        void RefreshTimer()
        {
            _timer = 1f;
            if (_tab == Tab.Records || _tab == Tab.Starter)
            {
                _resetText.text = string.Empty;
                return;
            }
            _resetText.text = MetaUi.ResetIn(Game.Missions.GetTimeUntilReset(Period));
        }

        // ───────────── 표시 ─────────────

        void Refresh(bool snapBar)
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            var missions = Game.Missions;
            bool recordsVisible = Game.Features.IsUnlocked(FeatureId.Achievements);
            bool starterVisible = StarterVisible();
            if (_tab == Tab.Starter && !starterVisible) _tab = Tab.Daily;
            if (_tabButtons.Length > (int)Tab.Records) _tabButtons[(int)Tab.Records].gameObject.SetActive(recordsVisible);
            if (_tabButtons.Length > (int)Tab.Starter && _tabButtons[(int)Tab.Starter] != null)
                _tabButtons[(int)Tab.Starter].gameObject.SetActive(starterVisible);
            LayoutTabs(recordsVisible, starterVisible);
            for (int i = 0; i < _tabBadges.Length && i < 2; i++)
            {
                if (_tabBadges[i] != null) _tabBadges[i].SetCount(missions.GetClaimableCount((MissionPeriod)i));
            }
            // 기록 · 첫 주 탭 점 (받을 수 있을 때만 — 점, 숫자 없음)
            var achievements = Game.Achievements;
            if (_tabBadges.Length > (int)Tab.Records && _tabBadges[(int)Tab.Records] != null)
                _tabBadges[(int)Tab.Records].SetDot(recordsVisible && achievements != null && achievements.ClaimableCount > 0);
            if (_tabBadges.Length > (int)Tab.Starter && _tabBadges[(int)Tab.Starter] != null)
                _tabBadges[(int)Tab.Starter].SetDot(starterVisible && Game.StarterWeek.HasClaimable);
            RefreshTimer();

            bool records = _tab == Tab.Records;
            bool starter = _tab == Tab.Starter;
            _missionsPanel.SetActive(!records && !starter);
            _recordsPanel.SetActive(records);
            if (_starterView != null && _starterView.gameObject.activeSelf != starter) _starterView.gameObject.SetActive(starter);
            _summaryText.gameObject.SetActive(!records && !starter);
            if (records)
            {
                RefreshRecords();
                return;
            }
            if (starter)
            {
                _starterView.Refresh();
                RefreshClaimAll(_starterView.ClaimableCount);
                return;
            }

            var period = Period;
            missions.GetMissions(period, _statuses);
            missions.GetChests(period, _chests);
            RefreshScore(period, snapBar);
            RefreshRows();
            RefreshChests(period);

            RefreshClaimAll(missions.GetClaimableCount(period));
            int max = missions.GetMaxPoints(period);
            _summaryText.text = period == MissionPeriod.Daily
                ? $"오늘 얻을 수 있는 점수 {max}점"
                : $"이번 주 얻을 수 있는 점수 {max}점";
        }

        /// <summary>
        /// 탭 줄 (보이는 탭이 바뀔 때만): 첫 주 탭 부품이 있으면 [첫 주][오늘][이번 주][수선점 기록] 순서로 왼쪽부터, 4개면 폭 150 (간격 10), 아니면 200 (간격 10).
        /// 첫 주 탭 부품이 없는 예전 장면은 빌더 자리 그대로 둔다
        /// </summary>
        void LayoutTabs(bool recordsVisible, bool starterVisible)
        {
            if (_tabButtons.Length <= (int)Tab.Starter || _tabButtons[(int)Tab.Starter] == null) return;
            int visible = 2 + (recordsVisible ? 1 : 0) + (starterVisible ? 1 : 0);
            if (visible == _laidOutTabs) return;
            _laidOutTabs = visible;
            float width = visible >= 4 ? 150f : 200f;
            float x = 0f;
            Place(Tab.Starter, starterVisible, width, ref x);
            Place(Tab.Daily, true, width, ref x);
            Place(Tab.Weekly, true, width, ref x);
            Place(Tab.Records, recordsVisible, width, ref x);
        }

        void Place(Tab tab, bool visible, float width, ref float x)
        {
            if (!visible) return;
            var button = _tabButtons[(int)tab];
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            x += width + 10f;
        }

        /// <summary>
        /// [모두 받기] (M51 · §3.9): 받을 것이 있을 때만 보인다 (SetActive — 누를 수 없는 회색 버튼으로 두지 않는다), 수는 배지.
        /// 0에서 생기면 0.85 → 1 (UIMotion.ClaimAllPop, 움직임 줄이기면 없음)
        /// </summary>
        void RefreshClaimAll(int claimable)
        {
            bool show = claimable > 0;
            bool appeared = show && _shownClaimable == 0;
            _shownClaimable = claimable;
            if (_claimAllButton.gameObject.activeSelf != show) _claimAllButton.gameObject.SetActive(show);
            _claimAllButton.interactable = show;
            _claimAllLabel.text = "모두 받기";
            if (_claimAllBadge != null)
            {
                if (show) _claimAllBadge.SetCount(claimable);
                else _claimAllBadge.Hide();
            }
            if (!appeared || UIMotion.Reduced || !isActiveAndEnabled) return;
            if (_claimAllPop != null) StopCoroutine(_claimAllPop);
            _claimAllPop = StartCoroutine(PopClaimAll());
        }

        IEnumerator PopClaimAll()
        {
            var rect = _claimAllButton.transform;
            for (float t = 0f; t < UIMotion.ClaimAllPop; t += Time.unscaledDeltaTime)
            {
                float k = UIMotion.OutBack(t / UIMotion.ClaimAllPop, 1.2f);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(UIMotion.ClaimAllPopFrom, 1f, k);
                yield return null;
            }
            rect.localScale = Vector3.one;
            _claimAllPop = null;
        }

        void RefreshScore(MissionPeriod period, bool snap)
        {
            var missions = Game.Missions;
            int points = missions.GetPoints(period);
            int barMax = Math.Max(1, missions.GetBarMax(period));
            _scoreLabel.text = period == MissionPeriod.Daily ? "오늘 일지 점수" : "이번 주 일지 점수";
            if (points != _shownPoints)
            {
                _shownPoints = points;
                _scoreText.text = points.ToString();
                _scoreMaxText.text = $"/ {barMax}";
            }
            _barTarget = Mathf.Clamp01(points / (float)barMax);
            if (!snap) return;
            _barShown = _barTarget;
            MetaUi.SetBar(_barFill, _barShown);
        }

        /// <summary>줄 순서: 열 때(또는 보이는 미션이 바뀔 때) 받을 수 있음 → 진행 중 → 완료로 정하고 유지한다</summary>
        void RefreshRows()
        {
            if (!SameMissionSet()) Reorder();
            _ordered.Clear();
            foreach (var id in _orderIds)
            {
                var status = FindStatus(id);
                if (status != null) _ordered.Add(status);
            }
            for (int i = 0; i < _ordered.Count; i++) _rows.Get(i).Bind(_ordered[i], _onRow);
            _rows.Trim(_ordered.Count);
            _emptyText.gameObject.SetActive(_ordered.Count == 0);
        }

        bool SameMissionSet()
        {
            if (_orderIds.Count != _statuses.Count) return false;
            foreach (var status in _statuses)
            {
                if (!_orderIds.Contains(status.Def.id)) return false;
            }
            return true;
        }

        void Reorder()
        {
            _orderIds.Clear();
            for (int rank = 0; rank < 3; rank++)
            {
                foreach (var status in _statuses)
                {
                    if (Rank(status) == rank) _orderIds.Add(status.Def.id);
                }
            }
        }

        static int Rank(MissionStatus status) => status.CanClaim ? 0 : status.IsClaimed ? 2 : 1;

        MissionStatus FindStatus(string id)
        {
            foreach (var status in _statuses)
            {
                if (status.Def != null && status.Def.id == id) return status;
            }
            return null;
        }

        void RefreshChests(MissionPeriod period)
        {
            int barMax = Math.Max(1, Game.Missions.GetBarMax(period));
            float width = _bar.rect.width;
            for (int i = 0; i < _chests.Count; i++)
            {
                var view = _chestViews.Get(i);
                view.Bind(_chests[i], _onChest);
                var rect = (RectTransform)view.transform;
                float x = width * Mathf.Clamp01(_chests[i].Def.points / (float)barMax);
                rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            }
            _chestViews.Trim(_chests.Count);
        }

        // ───────────── 받기 ─────────────

        void OnRowButton(MissionRowView row)
        {
            if (!MetaUi.IsActive) return;
            var status = row.Status;
            if (status == null || status.IsClaimed) return;
            var def = status.Def;
            if (status.CanClaim)
            {
                if (MetaUi.BlockIfTampered()) return;
                if (!Game.Missions.TryClaim(def.id, out var granted)) return;
                row.PlayStamp();
                FlyPoints(row.PointsPill, def.points);
                if (granted != null && granted.Count > 0) MetaUi.FlyRewards(granted, row.Rect);
                return;
            }

            // 가기
            if (def.navFeature != FeatureId.Battle && !Game.Features.TryUse(def.navFeature, out var locked))
            {
                MetaUi.Toast(locked);
                return;
            }
            var from = _tab;
            Close();
            // 탭으로 가면 시트 머리에 '‹ 수선 일지로' — 누르면 떠나기 전 틀로 돌아가 같은 쪽을 다시 연다 (M72, UINav가 틀을 기억 — CR-05)
            if (NavOpensTab(def.navFeature)) UINav.PushReturn(ReturnLabel, () => ReopenAt(from));
            // 이동이 다른 팝업(바구니·출석부)을 열면 손가락은 그 팝업 안 버튼으로 (아래에 묻힌 대상 대신)
            MetaNavigator.GoAndFocus(def.navFeature, def.focusTarget);
        }

        /// <summary>
        /// [수선점 기록] 쪽으로 연다 (MetaNavigator — FeatureId.Achievements 이동, SPEC4 §6.1 Q-04). 기록이 아직 잠겼으면 보통처럼 연다
        /// </summary>
        public void OpenRecords()
        {
            if (!MetaUi.IsActive) return;
            _reopenTab = Game.Features != null && Game.Features.IsUnlocked(FeatureId.Achievements) ? (int)Tab.Records : -1;
            Open();
        }

        /// <summary>
        /// 미션 쪽([오늘] / [이번 주])으로 연다 (MetaNavigator — FeatureId.Missions 이동: 가이드 퀘스트 '수선 일지 미션 2개 받기'의 [가기] · 재화 출처 '미션 상자').
        /// 그냥 열기(Open — 빠른 아이콘 · 메뉴 칸)와 달리 받을 것이 있어도 [첫 주]부터 열지 않는다: 손가락이 가리키는 [모두 받기]가 미션을 받아야
        /// 미션 받기 횟수(CounterKey.MissionClaim)가 오른다 (첫 주 과제는 세지 않는다)
        /// </summary>
        public void OpenMissions()
        {
            if (!MetaUi.IsActive) return;
            var missions = Game.Missions;
            _reopenTab = missions != null
                ? MissionsTab(missions.GetClaimableCount(MissionPeriod.Daily), missions.GetClaimableCount(MissionPeriod.Weekly))
                : (int)Tab.Daily;
            Open();
        }

        /// <summary>미션 쪽 고르기 (순수): 오늘 받을 것이 없고 이번 주에 있으면 [이번 주](1), 아니면 [오늘](0)</summary>
        internal static int MissionsTab(int dailyClaimable, int weeklyClaimable) =>
            dailyClaimable == 0 && weeklyClaimable > 0 ? (int)Tab.Weekly : (int)Tab.Daily;

        /// <summary>돌아가기 칩: 그 쪽 탭으로 일지를 다시 연다</summary>
        void ReopenAt(Tab tab)
        {
            if (!MetaUi.IsActive) return;
            _reopenTab = (int)tab;
            Open();
        }

        /// <summary>
        /// [가기 ›]가 탭(시트)으로 가는 이동인지 (MetaNavigator.OnFeature 기준) — 그때만 돌아가기 칩을 단다.
        /// 전투 화면 · HUD 버튼 · 팝업(일지 · 출석부 · 바구니)으로 가는 이동은 아니다
        /// </summary>
        internal static bool NavOpensTab(FeatureId nav) => nav switch
        {
            FeatureId.Battle => false,
            FeatureId.Missions or FeatureId.Achievements => false,
            FeatureId.Attendance => false,
            FeatureId.IdleChest or FeatureId.QuickIdle => false,
            FeatureId.AutoBattle or FeatureId.Speed2x or FeatureId.LampPolicy or FeatureId.AutoChallenge => false,
            _ => true,
        };

        void OnChest(MissionChestView view)
        {
            if (!MetaUi.IsActive) return;
            var status = view.Status;
            if (status == null) return;
            if (status.CanClaim)
            {
                if (MetaUi.BlockIfTampered()) return;
                if (!Game.Missions.TryClaimChest(Period, status.Index, out var granted)) return;
                UISfx.Play(UISfxKey.MissionChest);
                view.PlayOpen();
                if (_sparkles != null) _sparkles.BurstAt(view.Visual, 12, UITheme.Lamp, 0.9f);
                MetaUi.FlyRewards(granted, view.Visual);
                return;
            }
            if (status.IsClaimed)
            {
                MetaUi.Toast("이미 연 상자예요");
                return;
            }
            MetaUi.Toast($"{status.Def.points}점이 되면 열 수 있어요: {MetaUi.DescribeData(status.Def.rewards)}");
        }

        void OnClaimAll()
        {
            if (!MetaUi.IsActive || MetaUi.BlockIfTampered()) return;
            if (_tab == Tab.Records)
            {
                ClaimAllRecords();
                return;
            }
            if (_tab == Tab.Starter)
            {
                if (_starterView == null) return;
                // 받을 것이 없으면 패널이 토스트만 띄운다. 그때는 넘기지 않는다
                bool hadClaimable = Game.StarterWeek != null && Game.StarterWeek.HasClaimable;
                _starterView.ClaimAll((RectTransform)_claimAllButton.transform);
                if (hadClaimable) ScheduleAutoTab(Tab.Starter);
                return;
            }
            var period = Period;
            _claimableBefore.Clear();
            foreach (var status in _statuses)
            {
                if (status.CanClaim) _claimableBefore.Add(status);
            }
            bool chestReady = false;
            foreach (var chest in _chests) chestReady |= chest.CanClaim;
            if (_claimableBefore.Count == 0 && !chestReady)
            {
                MetaUi.Toast("지금 받을 일지가 없어요");
                return;
            }

            // 받기 전 상태를 기억해 두었다가 도장·상자 연출을 붙인다 (받은 뒤 상태 객체는 다시 채워진다)
            int gainedPoints = 0;
            var stampIds = new List<string>(_claimableBefore.Count);
            foreach (var status in _claimableBefore)
            {
                gainedPoints += status.Def.points;
                stampIds.Add(status.Def.id);
            }
            var rewards = Game.Missions.ClaimAll(period);

            for (int i = 0; i < _rows.ActiveCount; i++)
            {
                var row = _rows.Items[i];
                if (row.Status != null && stampIds.Contains(row.Status.Def.id)) row.PlayStamp();
            }
            for (int i = 0; i < _chestViews.ActiveCount; i++)
            {
                var view = _chestViews.Items[i];
                if (view.Status != null && view.Status.IsClaimed) view.PlayOpen();
            }
            if (gainedPoints > 0) FlyPoints(_claimAllButton.transform as RectTransform, gainedPoints);
            if (rewards != null && rewards.Count > 0) MetaUi.FlyRewards(rewards, _bar);
            ScheduleAutoTab(period == MissionPeriod.Weekly ? Tab.Weekly : Tab.Daily);
        }

        // ───────────── 수선점 기록 (Q-04) ─────────────

        void OnRecordsSegment(int index)
        {
            _recordsSegment = Mathf.Clamp(index, 0, 1);
            Refresh(false);
            if (_recordsScroll != null) _recordsScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>기록 탭: 요약 카드 + [진행 중](다 받지 않은 줄) / [완료](다 받은 줄) + 모두 받기. 새 부품이 없으면 예전 준비 중 카드</summary>
        void RefreshRecords()
        {
            var achievements = Game.Achievements;
            bool built = RecordsBuilt && achievements != null;
            if (_recordsPlaceholder != null) _recordsPlaceholder.SetActive(!built);
            if (!built)
            {
                _claimAllButton.gameObject.SetActive(false);
                _shownClaimable = 0;
                return;
            }
            achievements.GetLines(_lines);
            int claimed = 0;
            int total = 0;
            foreach (var line in _lines)
            {
                if (line == null) continue;
                total += Math.Max(0, line.TierCount);
                claimed += line.AllDone ? Math.Max(0, line.TierCount) : Mathf.Clamp(line.TierIndex, 0, Math.Max(0, line.TierCount));
            }
            int claimable = achievements.ClaimableCount;
            if (_recordsCountText != null) _recordsCountText.text = $"모은 기록 {claimed}/{total}";
            if (_recordsBarFill != null) MetaUi.SetBar(_recordsBarFill, total > 0 ? claimed / (float)total : 0f);
            if (_recordsClaimableText != null) _recordsClaimableText.text = claimable > 0 ? $"받을 수 있는 기록 {claimable}개" : string.Empty;
            if (_recordsSegments != null && _recordsSegments.Index != _recordsSegment) _recordsSegments.SetIndex(_recordsSegment);
            bool doneTab = _recordsSegment == 1;
            int shown = 0;
            foreach (var line in _lines)
            {
                if (line?.Current == null || line.AllDone != doneTab) continue;
                var model = JournalRowModel.FromAchievement(line, _hiddenRecordIcon);
                _recordRows.Get(shown++).Bind(in model, _onRecordRow);
            }
            _recordRows.Trim(shown);
            if (_recordsEmptyText != null)
            {
                _recordsEmptyText.gameObject.SetActive(shown == 0);
                if (shown == 0) _recordsEmptyText.text = doneTab ? "아직 다 모은 기록이 없어요" : "모든 기록을 다 모았어요!";
            }
            RefreshClaimAll(claimable);
        }

        void OnRecordRow(MissionRowView row)
        {
            if (!MetaUi.IsActive || row == null || !row.HasModel) return;
            var model = row.Model;
            if (model.CanClaim)
            {
                if (MetaUi.BlockIfTampered()) return;
                var granted = Game.Achievements?.TryClaim(model.Id);
                if (granted == null) return;
                row.PlayStamp();
                UISfx.Play(UISfxKey.AchievementClaim);
                if (granted.Count > 0) MetaUi.FlyRewards(granted, row.Rect);
                return;
            }
            if (model.State == JournalRowState.Go) GoFromRow(model.Nav, model.Focus);
        }

        void ClaimAllRecords()
        {
            var achievements = Game.Achievements;
            if (achievements == null || achievements.ClaimableCount <= 0)
            {
                MetaUi.Toast("지금 받을 기록이 없어요");
                return;
            }
            // 받기 전에 받을 수 있던 줄을 기억한다 — ClaimAll의 Changed가 곧바로 줄을 다시 쓴다 (받은 단계 → 다음 단계 · 다 받은 줄은 [완료]로, 순서도 바뀐다).
            // 줄은 단계 ID가 바뀌므로 기록 줄 ID(lineId)로 맞춘다 — 다 받아 [완료]로 옮긴 줄은 이 목록에 없어 도장이 없다
            _stampKeys.Clear();
            for (int i = 0; _recordRows != null && i < _recordRows.ActiveCount; i++)
            {
                var row = _recordRows.Items[i];
                if (row != null && row.HasModel && row.Model.CanClaim) _stampKeys.Add(RecordLineKey(row.Model.Id));
            }
            var rewards = achievements.ClaimAll();
            for (int i = 0; _recordRows != null && i < _recordRows.ActiveCount; i++)
            {
                var row = _recordRows.Items[i];
                if (row != null && row.HasModel && _stampKeys.Contains(RecordLineKey(row.Model.Id))) row.PlayStamp();
            }
            UISfx.Play(UISfxKey.AchievementClaim);
            if (rewards != null && rewards.Count > 0) MetaUi.FlyRewards(rewards, (RectTransform)_claimAllButton.transform);
            ScheduleAutoTab(Tab.Records);
        }

        /// <summary>기록 단계 ID의 줄 ID (콘텐츠 lineId, 없으면 단계 ID 그대로). 누를 때만 부른다</summary>
        static string RecordLineKey(string achievementId)
        {
            var def = !string.IsNullOrEmpty(achievementId) && Game.MetaDb != null ? Game.MetaDb.GetAchievement(achievementId) : null;
            return def != null && !string.IsNullOrEmpty(def.lineId) ? def.lineId : achievementId;
        }

        /// <summary>[가기 ›] (기록 · 첫 주): 잠긴 기능이면 토스트, 탭으로 가면 '‹ 수선 일지로' 칩 (M72)</summary>
        void GoFromRow(FeatureId nav, string focus)
        {
            if (nav != FeatureId.Battle && !Game.Features.TryUse(nav, out var locked))
            {
                MetaUi.Toast(locked);
                return;
            }
            var from = _tab;
            Close();
            if (NavOpensTab(nav)) UINav.PushReturn(ReturnLabel, () => ReopenAt(from));
            MetaNavigator.GoAndFocus(nav, focus);
        }

        // ───────────── 탭 자동 이동 (UI_InGame §4.3 공통 규칙) ─────────────

        /// <summary>
        /// 처음 열 탭 (순수): 화면 순서(첫 주 → 오늘 → 이번 주 → 기록)로 보이고 받을 것이 있는 첫 탭, 없으면 [오늘].
        /// 배열은 Tab 값으로 찾는다 (길이 4)
        /// </summary>
        internal static int FirstClaimTab(bool[] visible, int[] claimable) =>
            AutoTabRunner.FirstTab(TabOrder, visible, claimable, (int)Tab.Daily);

        /// <summary>
        /// [모두 받기] 뒤 넘어갈 탭 (순수): 지금 탭에 받을 것이 남았으면 −1 (머문다).
        /// 아니면 화면 순서로 지금 탭 오른쪽부터 한 바퀴 돌며 보이고 받을 것이 있는 첫 탭, 없으면 −1. 배열은 Tab 값으로 찾는다 (길이 4)
        /// </summary>
        internal static int NextClaimTab(int current, bool[] visible, int[] claimable) =>
            AutoTabRunner.NextTab(current, TabOrder, visible, claimable);

        /// <summary>
        /// 탭마다 보이는지 · 받을 수 (Refresh 탭 점과 같은 기준). 첫 주는 받을 것이 있으면 1.
        /// 기록은 새 부품이 있을 때만 대상 (다시 만들기 전 장면은 기록 쪽이 준비 중 카드라 받을 수 없다)
        /// </summary>
        void FillTabState()
        {
            var missions = Game.Missions;
            var achievements = Game.Achievements;
            bool starterVisible = StarterVisible();
            _tabVisible[(int)Tab.Daily] = true;
            _tabVisible[(int)Tab.Weekly] = true;
            _tabVisible[(int)Tab.Records] = RecordsBuilt && Game.Features.IsUnlocked(FeatureId.Achievements);
            _tabVisible[(int)Tab.Starter] = starterVisible;
            _tabClaimable[(int)Tab.Daily] = missions != null ? missions.GetClaimableCount(MissionPeriod.Daily) : 0;
            _tabClaimable[(int)Tab.Weekly] = missions != null ? missions.GetClaimableCount(MissionPeriod.Weekly) : 0;
            _tabClaimable[(int)Tab.Records] = achievements != null ? achievements.ClaimableCount : 0;
            _tabClaimable[(int)Tab.Starter] = starterVisible && Game.StarterWeek != null && Game.StarterWeek.HasClaimable ? 1 : 0;
        }

        /// <summary>
        /// [모두 받기]로 받은 뒤: 공용 부품(AutoTabRunner)이 기다렸다가 AdvanceFrom을 부른다 (줄의 [받기]는 부르지 않는다).
        /// from = 받은 탭 (받는 사이 Refresh가 탭을 바꾸면 넘기지 않는다, 예: 첫 주가 끝나 [오늘]로 돌아감)
        /// </summary>
        void ScheduleAutoTab(Tab from) => _autoTab?.Schedule(() => AdvanceFrom(from));

        /// <summary>기다림이 끝난 뒤 (팝업이 열려 있고 맨 위이며 튜토리얼 중이 아님은 AutoTabRunner가 확인했다)</summary>
        void AdvanceFrom(Tab from)
        {
            if (_tab != from) return;
            FillTabState();
            int next = NextClaimTab((int)from, _tabVisible, _tabClaimable);
            if (next < 0) return;
            // 기록 탭은 받을 줄이 [진행 중]에 있다
            if (next == (int)Tab.Records) _recordsSegment = 0;
            SelectTab((Tab)next);
            _autoTab?.Pulse(next < _tabLabels.Length ? _tabLabels[next] : null);
        }

        // ───────────── 점수 날아가기 ─────────────

        /// <summary>"+20" 글자가 줄에서 점수 숫자로 날아가 닿으면 숫자가 튄다</summary>
        void FlyPoints(RectTransform from, int points)
        {
            if (_pointsFloat == null || _floatLayer == null || from == null || !isActiveAndEnabled) return;
            if (_float != null) StopCoroutine(_float);
            _float = StartCoroutine(FloatPoints(from, points));
        }

        IEnumerator FloatPoints(RectTransform from, int points)
        {
            var label = _pointsFloat.rectTransform;
            _pointsFloat.text = $"+{points}";
            _pointsFloat.gameObject.SetActive(true);
            Vector2 start = LocalInLayer(from);
            Vector2 end = LocalInLayer(_scoreText.rectTransform);
            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = MetaUi.EaseInCubic(t / duration);
                var position = Vector2.Lerp(start, end, k) + new Vector2(0f, 90f * Mathf.Sin(k * Mathf.PI));
                label.anchoredPosition = position;
                label.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.8f, k);
                yield return null;
            }
            _pointsFloat.gameObject.SetActive(false);
            if (_sparkles != null) _sparkles.BurstAt(_scoreText.rectTransform, 8, UITheme.Gold, 0.6f);
            var score = _scoreText.rectTransform;
            const float punch = 0.25f;
            for (float t = 0f; t < punch; t += Time.unscaledDeltaTime)
            {
                score.localScale = Vector3.one * (1f + 0.3f * MetaUi.Punch(t / punch));
                yield return null;
            }
            score.localScale = Vector3.one;
            _float = null;
        }

        Vector2 LocalInLayer(RectTransform target)
        {
            var world = target.TransformPoint(target.rect.center);
            Vector2 local = _floatLayer.InverseTransformPoint(world);
            return local - _floatLayer.rect.center;
        }
    }
}
