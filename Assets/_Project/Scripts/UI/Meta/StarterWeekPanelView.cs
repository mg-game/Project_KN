using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선 일지 [첫 주] 쪽 (NEW-01, SPEC4 §13.6 · C.2 — JournalPopup의 StarterPanel에 붙는다):
    ///   · 머리 '새내기 수선공의 첫 주' + 'D-{n}' 알약 (Micro 18 Gold) + '일주일 동안 매일 새 할 일이 열려요'.
    ///   · 점수 카드: '{점수}점' + 막대 (끝 = 150점) 위 상자 5개 (30/60/90/120/150 — 일지 점수 상자 틀 MissionChestView, 받은 상자는 저장 값 meta.starterWeek.chests).
    ///   · 날 칩 7개 '1일'…'7일' (Option 칩): 아직 안 열린 날은 자물쇠 24 + 누르면 '{d}일째에 열려요', 받을 것이 있는 날은 점.
    ///   · 과제 줄 (MissionRowView ← JournalRowModel.FromStarterTask: '+5' 알약 · 잠긴 기능은 잠김 알약 '1-7' · [받기] / [가기 ›] / [완료]).
    /// [모두 받기]는 JournalPopup의 버튼 (ClaimableCount · ClaimAll). [가기 ›]는 GoRequested로 JournalPopup에 넘긴다 (일지를 닫고 돌아가기 칩을 단다).
    /// 열 때 · 바뀔 때만 다시 쓴다 (StarterWeekSystem.Changed는 JournalPopup이 받는다). 장면을 다시 만들기 전(G3)에는 이 쪽이 없다.
    /// </summary>
    public class StarterWeekPanelView : MonoBehaviour
    {
        public const int Days = 7;
        /// <summary>점수 막대 끝 (마지막 상자 150점)</summary>
        public const int BarMax = 150;

        [SerializeField] TMP_Text _daysText;
        [SerializeField] TMP_Text _pointsText;
        [SerializeField] RectTransform _bar;
        [SerializeField] Image _barFill;
        [SerializeField] RectTransform _chestRoot;
        [Tooltip("상자 틀 (일지 점수 상자와 같은 것)")]
        [SerializeField] MissionChestView _chestTemplate;
        [Tooltip("줄 틀 (일지 미션 줄과 같은 것)")]
        [SerializeField] MissionRowView _rowTemplate;
        [SerializeField] Button[] _dayButtons = Array.Empty<Button>();
        [SerializeField] Image[] _dayImages = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _dayLabels = Array.Empty<TMP_Text>();
        [SerializeField] GameObject[] _dayLocks = Array.Empty<GameObject>();
        [SerializeField] GameObject[] _dayDots = Array.Empty<GameObject>();
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _listContent;
        [SerializeField] TMP_Text _emptyText;
        [SerializeField] MetaSparkleBurst _sparkles;

        readonly List<StarterTaskStatus> _tasks = new();
        readonly List<MissionChestStatus> _chests = new();
        /// <summary>[모두 받기] 앞에 받을 수 있던 과제 ID (받은 뒤 도장을 찍을 줄 — 받기의 Changed가 곧바로 줄을 다시 쓴다)</summary>
        readonly List<string> _stampIds = new();
        MetaViewPool<MissionRowView> _rows;
        MetaViewPool<MissionChestView> _chestViews;
        Action<MissionRowView> _onRow;
        Action<MissionChestView> _onChest;
        int _day = 1;
        bool _ready;

        /// <summary>[가기 ›] (기능, 손가락 대상) — JournalPopup이 일지를 닫고 간다</summary>
        public event Action<FeatureId, string> GoRequested;

        /// <summary>지금 고른 날 (1~7)</summary>
        public int Day => _day;

        /// <summary>첫 주가 진행 중인지 (메타 켜짐 + StarterWeekSystem.IsActive)</summary>
        public static bool IsAvailable => MetaUi.IsActive && Game.StarterWeek != null && Game.StarterWeek.IsActive;

        /// <summary>받을 수 있는 과제 + 상자 수 (열린 날만, 모두 받기 배지)</summary>
        public int ClaimableCount { get; private set; }

        void Awake() => EnsureReady();

        void EnsureReady()
        {
            if (_ready) return;
            _ready = true;
            _onRow = OnRow;
            _onChest = OnChest;
            if (_rowTemplate != null && _listContent != null) _rows = new MetaViewPool<MissionRowView>(_rowTemplate, _listContent);
            if (_chestTemplate != null && _chestRoot != null) _chestViews = new MetaViewPool<MissionChestView>(_chestTemplate, _chestRoot);
            for (int i = 0; i < _dayButtons.Length; i++)
            {
                int day = i + 1;
                if (_dayButtons[i] != null) _dayButtons[i].onClick.AddListener(() => SelectDay(day));
            }
        }

        /// <summary>쪽을 처음 보일 때: 받을 것이 있는 첫 열린 날 → 오늘을 고르고 다시 쓴다</summary>
        public void Show()
        {
            EnsureReady();
            _day = PickDay();
            Refresh();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 열린 날 수 (1~7, 순수). dayIndex = StarterWeekSystem.DayIndex — 1부터 센다 (시작한 날 = 1, 7일째 뒤로는 7, 진행 중이 아니면 0 → 1).
        /// StarterWeekSystem.IsDayOpen(day)과 같은 규칙 (day ≤ DayIndex)
        /// </summary>
        public static int OpenDays(int dayIndex) => Mathf.Clamp(dayIndex, 1, Days);

        int PickDay()
        {
            var starter = Game.StarterWeek;
            if (starter == null) return 1;
            int open = OpenDays(starter.DayIndex);
            for (int day = 1; day <= open; day++)
            {
                starter.GetTasks(day, _tasks);
                foreach (var task in _tasks)
                {
                    if (task != null && task.CanClaim) return day;
                }
            }
            return open;
        }

        void SelectDay(int day)
        {
            var starter = Game.StarterWeek;
            if (starter == null) return;
            if (day > OpenDays(starter.DayIndex))
            {
                var anchor = day - 1 < _dayButtons.Length && _dayButtons[day - 1] != null ? (RectTransform)_dayButtons[day - 1].transform : null;
                string text = LockedDayText(day);
                if (anchor == null || !InfoTooltipView.Show(anchor, text, null)) MetaUi.Toast(text);
                return;
            }
            _day = day;
            UISfx.Play(UISfxKey.Tab);
            Refresh();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>'{d}일째에 열려요' (C.2)</summary>
        public static string LockedDayText(int day) => $"{day}일째에 열려요";

        /// <summary>다시 쓴다 (열 때 · StarterWeekSystem이 바뀔 때). 줄 · 상자 수만큼만 글을 만든다</summary>
        public void Refresh()
        {
            EnsureReady();
            var starter = MetaUi.IsActive ? Game.StarterWeek : null;
            ClaimableCount = 0;
            if (starter == null) return;
            int open = OpenDays(starter.DayIndex);
            _day = Mathf.Clamp(_day, 1, open);
            if (_daysText != null) _daysText.text = $"D-{Math.Max(0, starter.DaysLeft)}";
            int points = Math.Max(0, starter.Points);
            if (_pointsText != null) _pointsText.text = $"{points}점";
            if (_barFill != null) MetaUi.SetBar(_barFill, Mathf.Clamp01(points / (float)BarMax));
            RefreshChests(points);

            int claimable = 0;
            for (int day = 1; day <= Days; day++)
            {
                bool unlocked = day <= open;
                bool dayClaimable = false;
                if (unlocked)
                {
                    starter.GetTasks(day, _tasks);
                    foreach (var task in _tasks)
                    {
                        if (task == null || !task.CanClaim) continue;
                        dayClaimable = true;
                        claimable++;
                    }
                }
                int i = day - 1;
                if (i < _dayLocks.Length && _dayLocks[i] != null) _dayLocks[i].SetActive(!unlocked);
                if (i < _dayDots.Length && _dayDots[i] != null) _dayDots[i].SetActive(dayClaimable);
                var image = i < _dayImages.Length ? _dayImages[i] : null;
                var label = i < _dayLabels.Length ? _dayLabels[i] : null;
                TabStyle.Apply(image, label, day == _day, TabVariant.Option);
                if (label != null) label.alpha = unlocked ? 1f : 0.55f;
            }
            foreach (var chest in _chests)
            {
                if (chest.CanClaim) claimable++;
            }
            ClaimableCount = claimable;

            starter.GetTasks(_day, _tasks);
            int shown = 0;
            foreach (var task in _tasks)
            {
                if (task?.Def == null || _rows == null) continue;
                var model = JournalRowModel.FromStarterTask(task, task.Locked ? LockHint(task.Def.requiredFeature) : null);
                _rows.Get(shown++).Bind(in model, _onRow);
            }
            _rows?.Trim(shown);
            if (_emptyText != null) _emptyText.gameObject.SetActive(shown == 0);
        }

        /// <summary>잠긴 기능의 짧은 안내 ('1-7' — 기능 정의 lockedHint, 없으면 '잠김')</summary>
        static string LockHint(FeatureId feature)
        {
            var def = Game.Features != null ? Game.Features.GetDef(feature) : null;
            return def != null && !string.IsNullOrEmpty(def.lockedHint) ? def.lockedHint : "잠김";
        }

        /// <summary>상자 5개 (콘텐츠 starterWeek.chests, 받은 상자 = 저장 값 meta.starterWeek.chests — 읽기만). 상자 상태는 처음 한 번 만들어 다시 쓴다</summary>
        void RefreshChests(int points)
        {
            var db = Game.MetaDb;
            var defs = db != null && db.starterWeek != null ? db.starterWeek.chests : null;
            var claimed = Game.Data?.meta?.starterWeek?.chests;
            int count = defs != null ? defs.Count : 0;
            while (_chests.Count < count) _chests.Add(new MissionChestStatus { Def = new MissionChestDef() });
            if (_chests.Count > count) _chests.RemoveRange(count, _chests.Count - count);
            for (int i = 0; i < count; i++)
            {
                var def = defs[i];
                var status = _chests[i];
                status.Index = i;
                status.Def.points = def != null ? def.points : 0;
                status.Def.rewards = def != null ? def.rewards : null;
                status.IsReached = def != null && points >= def.points;
                status.IsClaimed = claimed != null && claimed.Contains(i);
            }
            if (_chestViews == null) return;
            float width = _bar != null ? _bar.rect.width : 0f;
            for (int i = 0; i < count; i++)
            {
                var view = _chestViews.Get(i);
                view.Bind(_chests[i], _onChest);
                var rect = (RectTransform)view.transform;
                rect.anchoredPosition = new Vector2(width * Mathf.Clamp01(_chests[i].Def.points / (float)BarMax), rect.anchoredPosition.y);
            }
            _chestViews.Trim(count);
        }

        void OnRow(MissionRowView row)
        {
            if (!MetaUi.IsActive || row == null || !row.HasModel) return;
            var model = row.Model;
            switch (model.State)
            {
                case JournalRowState.Claimable:
                {
                    if (MetaUi.BlockIfTampered()) return;
                    var granted = Game.StarterWeek?.TryClaimTask(model.Id);
                    if (granted == null) return;
                    row.PlayStamp();
                    UISfx.Play(UISfxKey.Claim);
                    if (granted.Count > 0) MetaUi.FlyRewards(granted, row.Rect);
                    break;
                }
                case JournalRowState.Go:
                    GoRequested?.Invoke(model.Nav, model.Focus);
                    break;
                case JournalRowState.Locked:
                {
                    var def = Game.Features != null ? Game.Features.GetDef(FeatureOf(model.Id)) : null;
                    MetaUi.Toast(def != null && !string.IsNullOrEmpty(def.lockedToast) ? def.lockedToast : "아직 열리지 않았어요. 열리면 받을 수 있어요");
                    break;
                }
            }
        }

        FeatureId FeatureOf(string taskId)
        {
            foreach (var task in _tasks)
            {
                if (task?.Def != null && task.Def.id == taskId) return task.Def.requiredFeature;
            }
            return FeatureId.Battle;
        }

        void OnChest(MissionChestView view)
        {
            if (!MetaUi.IsActive || view == null || view.Status == null) return;
            var status = view.Status;
            if (status.CanClaim)
            {
                if (MetaUi.BlockIfTampered()) return;
                var granted = Game.StarterWeek?.TryClaimChest(status.Index);
                if (granted == null) return;
                UISfx.Play(UISfxKey.MissionChest);
                view.PlayOpen();
                if (_sparkles != null) _sparkles.BurstAt(view.Visual, 12, UITheme.Lamp, 0.9f);
                if (granted.Count > 0) MetaUi.FlyRewards(granted, view.Visual);
                return;
            }
            MetaUi.Toast(status.IsClaimed ? "이미 연 상자예요" : $"{status.Def.points}점이 되면 열 수 있어요: {MetaUi.Describe(status.Def.rewards)}");
        }

        /// <summary>[모두 받기]: 열린 날의 과제 · 상자를 모두 받는다 (줄마다 도장)</summary>
        public void ClaimAll(RectTransform from)
        {
            var starter = MetaUi.IsActive ? Game.StarterWeek : null;
            if (starter == null || !starter.HasClaimable)
            {
                MetaUi.Toast("지금 받을 첫 주 선물이 없어요");
                return;
            }
            // 받기 전에 받을 수 있던 과제 · 상자를 기억한다 — ClaimAll의 Changed로 JournalPopup이 곧바로 이 쪽을 다시 쓰므로 받은 뒤에는 CanClaim이 모두 false
            _stampIds.Clear();
            for (int i = 0; _rows != null && i < _rows.ActiveCount; i++)
            {
                var row = _rows.Items[i];
                if (row != null && row.HasModel && row.Model.CanClaim) _stampIds.Add(row.Model.Id);
            }
            int chestsBefore = 0;
            for (int i = 0; i < _chests.Count && i < 31; i++)
            {
                if (_chests[i].CanClaim) chestsBefore |= 1 << i;
            }
            var rewards = starter.ClaimAll();
            for (int i = 0; _rows != null && i < _rows.ActiveCount; i++)
            {
                var row = _rows.Items[i];
                if (row != null && row.HasModel && _stampIds.Contains(row.Model.Id)) row.PlayStamp();
            }
            for (int i = 0; _chestViews != null && i < _chestViews.ActiveCount && i < 31; i++)
            {
                var view = _chestViews.Items[i];
                if (view != null && (chestsBefore & (1 << i)) != 0 && view.Status != null && view.Status.IsClaimed) view.PlayOpen();
            }
            UISfx.Play(chestsBefore != 0 ? UISfxKey.MissionChest : UISfxKey.Claim);
            if (rewards != null && rewards.Count > 0) MetaUi.FlyRewards(rewards, from);
        }
    }
}
