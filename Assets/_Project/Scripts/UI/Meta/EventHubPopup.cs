using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 이벤트 모아 보기 980×1500 (EVT-01, SPEC4 §8.3 — UINav.EventsRequested를 MetaNavigator가 받아 연다):
    ///   · 띠 Top(0, 354): 이벤트 그림 (EventDef.banner — 없으면 shop_event_banner) + 어둠 + 이름 (40) + 남은 시간 (24, 하루 안이면 시계 글꼴 '7:12:03').
    ///     보여 주는 이벤트 = 미션이나 도장이 있는 진행 중 회차 → 그 밖 진행 중 이벤트 → 없으면 '지금 열린 축제가 없어요'.
    ///   · 효과 줄 Top(362, 72): 이벤트마다 칩 (밤 알약 + 아이콘 32 + 24) — 진행 중이면 효과 ('원정 재료 ×2'), 아니면 다음 시작 ('토요일 새벽 5시부터', 22 SubText).
    ///     밤의 수선점 줄: 밤지기 짝짝이(jjak_nightkeeper)가 아직 없으면 그 아래 '밤 8시가 지나 들르면 밤지기 짝짝이가 찾아와요' (§13.5 i).
    ///   · 도장 줄 Top(466, 190): 7칸 120×180 (회차 도장 — 찍은 칸은 도장, 오늘 칸은 금빛 + 누르면 도장 찍기). 도장이 없는 이벤트면 숨고 목록이 올라간다.
    ///   · 미션 목록 (MissionRowView ← JournalRowModel): 아이콘 · 글 · '12/50' · 보상 칩 · [받기]/[완료]/진행 중.
    ///     아래 끝은 아래 줄 바로 위 (수첩 칸이 없으면 상점 버튼 위 16, 있으면 수첩 칸 위 12 — RM-3 RI-3), 그래서 줄은 스크롤로만 잘린다.
    ///   · 아래: [이벤트 상점 가기 ›] Primary 420×96 (왼쪽) · [모두 받기] 380×96 (오른쪽, 받을 것이 있을 때). 수첩이 열렸으면 그 위 수첩 칸 860×110
    ///     '별빛 산책 수첩 Lv.{n} ›' (UINav.OpenSeason).
    /// 열 때 보여 주는 회차를 본 것으로 (EventCalendarSystem.MarkSeen — top.event.new). 남은 시간은 1초마다 (글이 바뀔 때만 쓴다). 열려 있을 때만 Changed를 구독한다.
    /// </summary>
    public class EventHubPopup : UIPopup
    {
        public const int StampCells = 7;
        public const int EffectChips = 4;
        public const string NightKeeperCostume = "jjak_nightkeeper";
        public const string NightKeeperLine = "밤 8시가 지나 들르면 밤지기 짝짝이가 찾아와요";
        /// <summary>도장 칸 날 글 '{n}일' 색 (RM-3 RI-2): 칸 그림 위 라벤더 띠 위라 늘 Ink (5.4:1 이상 — InkSub는 2.2:1). 빌더도 이 값으로 만든다</summary>
        public static readonly Color StampDayColor = UITheme.Ink;
        /// <summary>아래 줄 (Body 기준, 아래에서 잰 값): [이벤트 상점 가기] · [모두 받기] 아래 끝 8 · 높이 96, 수첩 칸 860×110 아래 끝 118</summary>
        public const float FooterBottom = 8f, FooterHeight = 96f, SeasonTileBottom = 118f, SeasonTileHeight = 110f;
        /// <summary>미션 목록 아래 끝 (Body 기준, RM-3 RI-3): 수첩 칸이 없으면 상점 버튼 위 16 (120)</summary>
        public const float ListBottomNoSeason = FooterBottom + FooterHeight + 16f;
        /// <summary>미션 목록 아래 끝 — 수첩 칸이 보이면 그 칸 위 12 (240)</summary>
        public const float ListBottomWithSeason = SeasonTileBottom + SeasonTileHeight + 12f;

        [Header("띠")]
        [SerializeField] Image _banner;
        [SerializeField] Sprite _fallbackBanner;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _timeText;

        [Header("효과")]
        [SerializeField] GameObject[] _chips = Array.Empty<GameObject>();
        [SerializeField] Image[] _chipIcons = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _chipLabels = Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text _nightKeeperText;

        [Header("도장")]
        [SerializeField] GameObject _stampRow;
        [SerializeField] Button[] _stampCells = Array.Empty<Button>();
        [SerializeField] GameObject[] _stampGlows = Array.Empty<GameObject>();
        [SerializeField] GameObject[] _stampMarks = Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] _stampDays = Array.Empty<TMP_Text>();
        [SerializeField] MetaRewardChip[] _stampRewards = Array.Empty<MetaRewardChip>();

        [Header("미션")]
        [SerializeField] RectTransform _listRoot;
        [SerializeField] RectTransform _listContent;
        [SerializeField] MissionRowView _rowTemplate;
        [SerializeField] TMP_Text _emptyText;
        [Tooltip("도장 줄이 없을 때 목록 위 끝 (Body 기준) · 있을 때")]
        [SerializeField] float _listTopNoStamps = 466f;
        [SerializeField] float _listTopWithStamps = 664f;
        [Tooltip("수첩 칸이 없을 때 목록 아래 끝 (Body 기준 — 상점 버튼 위 16) · 있을 때 (수첩 칸 위 12)")]
        [SerializeField] float _listBottomNoSeason = ListBottomNoSeason;
        [SerializeField] float _listBottomWithSeason = ListBottomWithSeason;

        [Header("아래")]
        [SerializeField] Button _shopButton;
        [SerializeField] Button _claimAllButton;
        [SerializeField] Badge _claimAllBadge;
        [SerializeField] Button _seasonTile;
        [SerializeField] TMP_Text _seasonLabel;

        readonly List<ActiveEventStatus> _active = new();
        readonly List<EventMissionStatus> _missions = new();
        MetaViewPool<MissionRowView> _rows;
        Action<MissionRowView> _onRow;
        string _instanceKey;
        EventDef _featured;
        bool _subscribed;
        float _timer;
        TimeSpan _timeLeft;
        string _shownTime;

        protected override void Awake()
        {
            base.Awake();
            if (_rowTemplate != null && _listContent != null) _rows = new MetaViewPool<MissionRowView>(_rowTemplate, _listContent);
            _onRow = OnRow;
            for (int i = 0; i < _stampCells.Length; i++)
            {
                int index = i;
                if (_stampCells[i] != null) _stampCells[i].onClick.AddListener(() => OnStampCell(index));
            }
            if (_shopButton != null) _shopButton.onClick.AddListener(GoShop);
            if (_claimAllButton != null) _claimAllButton.onClick.AddListener(OnClaimAll);
            if (_seasonTile != null) _seasonTile.onClick.AddListener(OpenSeason);
        }

        protected override void OnOpen()
        {
            var events = MetaUi.IsActive ? Game.Events : null;
            if (events == null)
            {
                Close();
                return;
            }
            if (!_subscribed)
            {
                events.Changed += OnEventsChanged;
                _subscribed = true;
            }
            Refresh();
            if (!string.IsNullOrEmpty(_instanceKey)) events.MarkSeen(_instanceKey);
        }

        protected override void OnClose()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.IsReady && Game.Events != null) Game.Events.Changed -= OnEventsChanged;
        }

        void OnEventsChanged() => Refresh();

        void Refresh()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            var events = Game.Events;
            if (events == null) return;
            events.GetActive(_active);
            var featured = PickFeatured(_active);
            _featured = featured?.Def;
            _instanceKey = featured?.InstanceKey;
            _timeLeft = featured != null ? featured.TimeLeft : TimeSpan.Zero;
            _shownTime = null;
            if (_nameText != null) _nameText.text = featured != null ? featured.Name ?? featured.Def?.name ?? string.Empty : "지금 열린 축제가 없어요";
            if (_banner != null)
            {
                var sprite = _featured != null && _featured.banner != null ? _featured.banner : _fallbackBanner;
                _banner.sprite = sprite;
                _banner.enabled = sprite != null;
            }
            RefreshTime(0f);
            RefreshChips();
            bool stamps = RefreshStamps(events);
            RefreshMissions(events, stamps);
            RefreshSeason();
        }

        /// <summary>보여 줄 이벤트: 미션이나 도장이 있는 회차 → 진행 중 첫 이벤트 → null (순수 — 목록만 본다)</summary>
        public static ActiveEventStatus PickFeatured(IReadOnlyList<ActiveEventStatus> active)
        {
            if (active == null) return null;
            ActiveEventStatus first = null;
            for (int i = 0; i < active.Count; i++)
            {
                var status = active[i];
                if (status == null) continue;
                first ??= status;
                var def = status.Def;
                if (!string.IsNullOrEmpty(status.InstanceKey) && def != null && ((def.missions != null && def.missions.Count > 0) || (def.stamps != null && def.stamps.Count > 0)))
                    return status;
            }
            return first;
        }

        void Update()
        {
            if (!IsOpen) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 1f;
            RefreshTime(1f);
        }

        void RefreshTime(float elapsed)
        {
            if (_timeText == null) return;
            if (_featured == null)
            {
                if (_shownTime != string.Empty) _timeText.text = _shownTime = string.Empty;
                return;
            }
            if (elapsed > 0f) _timeLeft -= TimeSpan.FromSeconds(elapsed);
            if (_timeLeft < TimeSpan.Zero) _timeLeft = TimeSpan.Zero;
            string text = TimeLeftLabel(_timeLeft);
            if (text == _shownTime) return;
            _shownTime = text;
            _timeText.text = text;
        }

        /// <summary>남은 시간 글 (순수): 하루 이상 '{d}일 {h}시간 남았어요', 하루 안 '{h:mm:ss} 남았어요' (시계 글꼴은 빌더), 0이면 '곧 끝나요'</summary>
        public static string TimeLeftLabel(TimeSpan left)
        {
            if (left <= TimeSpan.Zero) return "곧 끝나요";
            if (left.TotalHours >= 24) return $"{(int)left.TotalDays}일 {left.Hours}시간 남았어요";
            return $"{MetaUi.Clock(left)} 남았어요";
        }

        void RefreshChips()
        {
            var db = Game.MetaDb;
            int count = 0;
            bool night = false;
            if (db != null && db.events != null)
            {
                foreach (var def in db.events)
                {
                    if (def == null || count >= _chips.Length || count >= EffectChips) continue;
                    var active = FindActive(def.id);
                    var chip = _chips[count];
                    if (chip != null) chip.SetActive(true);
                    var icon = count < _chipIcons.Length ? _chipIcons[count] : null;
                    if (icon != null)
                    {
                        icon.sprite = def.icon;
                        icon.enabled = def.icon != null;
                    }
                    var label = count < _chipLabels.Length ? _chipLabels[count] : null;
                    if (label != null)
                    {
                        label.text = active != null && !string.IsNullOrEmpty(active.EffectLabel) ? active.EffectLabel : NextStartLabel(def);
                        label.color = active != null ? UITheme.Cream : UITheme.SubText;
                    }
                    if (def.kind == EventKind.NightBonus) night = true;
                    count++;
                }
            }
            for (int i = count; i < _chips.Length; i++)
            {
                if (_chips[i] != null) _chips[i].SetActive(false);
            }
            if (_nightKeeperText == null) return;
            bool owned = Game.Costumes != null && Game.Costumes.IsUnlocked(NightKeeperCostume);
            bool show = night && !owned;
            _nightKeeperText.gameObject.SetActive(show);
            if (show) _nightKeeperText.text = NightKeeperLine;
        }

        ActiveEventStatus FindActive(string id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i]?.Def != null && _active[i].Def.id == id) return _active[i];
            }
            return null;
        }

        static readonly string[] WeekdayNames = { "월", "화", "수", "목", "금", "토", "일" };

        /// <summary>
        /// 다음 시작 글 (순수 — 쉬는 이벤트 칩): 요일 = '{첫 요일}요일 새벽 5시부터' (날 키는 새벽 5시에 바뀐다), 매달 = '매달 {n}일 새벽 5시부터',
        /// 시간대 = '밤 9시부터' · '아침 7시부터', 날짜 = '{m}월 {d}일부터'
        /// </summary>
        public static string NextStartLabel(EventDef def)
        {
            if (def == null) return string.Empty;
            switch (def.scheduleType)
            {
                case EventScheduleType.Weekdays:
                    for (int i = 0; i < 7; i++)
                    {
                        if ((def.weekdayMask & (1 << i)) != 0) return $"{WeekdayNames[i]}요일 새벽 5시부터";
                    }
                    return string.Empty;
                case EventScheduleType.MonthDays:
                    return $"매달 {Math.Max(1, def.fromDay)}일 새벽 5시부터";
                case EventScheduleType.DailyHours:
                    return $"{HourLabel(def.fromHour)}부터";
                case EventScheduleType.Dates:
                    return $"{Math.Max(1, def.fromMonth)}월 {Math.Max(1, def.fromDay)}일부터";
                default:
                    return string.Empty;
            }
        }

        /// <summary>'밤 9시' · '새벽 2시' · '아침 8시' · '낮 1시' · 0시 = '밤 12시' (0~23시)</summary>
        public static string HourLabel(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            if (hour == 0) return "밤 12시";
            string part = hour < 5 ? "새벽" : hour < 11 ? "아침" : hour < 17 ? "낮" : hour < 19 ? "저녁" : "밤";
            int h12 = hour % 12 == 0 ? 12 : hour % 12;
            return $"{part} {h12}시";
        }

        bool RefreshStamps(EventCalendarSystem events)
        {
            var stamps = _featured != null && !string.IsNullOrEmpty(_instanceKey) ? _featured.stamps : null;
            bool show = stamps != null && stamps.Count > 0 && _stampRow != null;
            if (_stampRow != null) _stampRow.SetActive(show);
            if (!show) return false;
            int taken = StampsTaken(events, _instanceKey);
            bool canStamp = events.CanStamp(_instanceKey);
            for (int i = 0; i < _stampCells.Length && i < StampCells; i++)
            {
                var def = i < stamps.Count ? stamps[i] : null;
                if (_stampCells[i] != null) _stampCells[i].gameObject.SetActive(def != null);
                if (def == null) continue;
                bool stamped = i < taken;
                bool today = canStamp && i == taken;
                if (i < _stampGlows.Length && _stampGlows[i] != null) _stampGlows[i].SetActive(today);
                if (i < _stampMarks.Length && _stampMarks[i] != null) _stampMarks[i].SetActive(stamped);
                if (i < _stampDays.Length && _stampDays[i] != null)
                {
                    _stampDays[i].text = today ? "도장 찍기" : $"{Math.Max(1, def.day)}일";
                    _stampDays[i].color = StampDayColor;
                }
                var chip = i < _stampRewards.Length ? _stampRewards[i] : null;
                if (chip == null) continue;
                var reward = def.rewards != null && def.rewards.Count > 0 ? def.rewards[0] : null;
                chip.gameObject.SetActive(reward != null);
                if (reward == null) continue;
                chip.Set(reward);
                chip.SetDimmed(stamped);
            }
            return true;
        }

        /// <summary>그 회차에서 찍은 도장 수 (EventCalendarSystem.GetStampsTaken — 0…MaxStamps, 화면 도장 칸 수로 한 번 더 자른다. 할당 없음)</summary>
        static int StampsTaken(EventCalendarSystem events, string instanceKey)
            => events != null ? Mathf.Clamp(events.GetStampsTaken(instanceKey), 0, StampCells) : 0;

        void RefreshMissions(EventCalendarSystem events, bool stamps)
        {
            if (_listRoot != null)
            {
                float top = stamps ? _listTopWithStamps : _listTopNoStamps;
                var offset = _listRoot.offsetMax;
                if (!Mathf.Approximately(offset.y, -top)) _listRoot.offsetMax = new Vector2(offset.x, -top);
            }
            int shown = 0;
            int claimable = 0;
            if (!string.IsNullOrEmpty(_instanceKey))
            {
                events.GetMissions(_instanceKey, _missions);
                var icon = _featured != null ? _featured.icon : null;
                for (int i = 0; i < _missions.Count; i++)
                {
                    var mission = _missions[i];
                    if (mission?.Def == null || _rows == null) continue;
                    var model = JournalRowModel.FromEventMission(mission, icon);
                    _rows.Get(shown++).Bind(in model, _onRow);
                    if (mission.CanClaim) claimable++;
                }
            }
            else _missions.Clear();
            _rows?.Trim(shown);
            if (_emptyText != null) _emptyText.gameObject.SetActive(shown == 0);
            if (_claimAllButton == null) return;
            bool show = claimable > 0;
            if (_claimAllButton.gameObject.activeSelf != show) _claimAllButton.gameObject.SetActive(show);
            if (_claimAllBadge == null) return;
            if (show) _claimAllBadge.SetCount(claimable);
            else _claimAllBadge.Hide();
        }

        void RefreshSeason()
        {
            var season = MetaUi.IsActive ? Game.Season : null;
            bool show = _seasonTile != null && season != null && season.IsUnlocked;
            PlaceListBottom(show);
            if (_seasonTile == null) return;
            _seasonTile.gameObject.SetActive(show);
            if (show && _seasonLabel != null) _seasonLabel.text = $"별빛 산책 수첩 Lv.{Math.Max(1, season.Level)} ›";
        }

        /// <summary>목록 아래 끝을 수첩 칸에 맞춘다 (값이 바뀔 때만 쓴다 — 할당 없음)</summary>
        void PlaceListBottom(bool seasonTile)
        {
            if (_listRoot == null) return;
            float bottom = ListBottom(seasonTile, _listBottomNoSeason, _listBottomWithSeason);
            var offset = _listRoot.offsetMin;
            if (!Mathf.Approximately(offset.y, bottom)) _listRoot.offsetMin = new Vector2(offset.x, bottom);
        }

        /// <summary>
        /// 미션 목록 아래 끝 (순수, Body 기준 — RM-3 RI-3): 수첩 칸이 보이면 그 칸 위(withSeason), 아니면 상점 버튼 위(noSeason).
        /// 수첩 쪽 값이 더 낮게 잡혀 있어도 버튼 쪽보다 내려가지 않는다
        /// </summary>
        public static float ListBottom(bool seasonTile, float noSeason, float withSeason) => seasonTile ? Mathf.Max(noSeason, withSeason) : noSeason;

        void OnRow(MissionRowView row)
        {
            if (!MetaUi.IsActive || row == null || !row.HasModel || string.IsNullOrEmpty(_instanceKey)) return;
            var model = row.Model;
            if (!model.CanClaim) return;
            if (MetaUi.BlockIfTampered()) return;
            var granted = Game.Events?.TryClaimMission(_instanceKey, model.Id);
            if (granted == null) return;
            row.PlayStamp();
            UISfx.Play(UISfxKey.Claim);
            if (granted.Count > 0) MetaUi.FlyRewards(granted, row.Rect);
        }

        void OnStampCell(int index)
        {
            var events = MetaUi.IsActive ? Game.Events : null;
            if (events == null || string.IsNullOrEmpty(_instanceKey)) return;
            int taken = StampsTaken(events, _instanceKey);
            if (index != taken || !events.CanStamp(_instanceKey))
            {
                MetaUi.Toast(index < taken ? "이미 찍은 도장이에요" : "하루에 한 칸씩 찍을 수 있어요");
                return;
            }
            if (MetaUi.BlockIfTampered()) return;
            var granted = events.TryStamp(_instanceKey);
            if (granted == null) return;
            UISfx.Play(UISfxKey.Stamp);
            var cell = index < _stampCells.Length && _stampCells[index] != null ? (RectTransform)_stampCells[index].transform : Window;
            if (granted.Count > 0) MetaUi.FlyRewards(granted, cell);
        }

        void OnClaimAll()
        {
            var events = MetaUi.IsActive ? Game.Events : null;
            if (events == null || string.IsNullOrEmpty(_instanceKey) || MetaUi.BlockIfTampered()) return;
            List<MetaReward> all = null;
            for (int i = 0; i < _rows.ActiveCount; i++)
            {
                var row = _rows.Items[i];
                if (row == null || !row.HasModel || !row.Model.CanClaim) continue;
                var granted = events.TryClaimMission(_instanceKey, row.Model.Id);
                if (granted == null) continue;
                row.PlayStamp();
                all ??= new List<MetaReward>(granted.Count + 2);
                all.AddRange(granted);
            }
            if (all == null)
            {
                MetaUi.Toast("지금 받을 미션이 없어요");
                return;
            }
            UISfx.Play(UISfxKey.Claim);
            MetaUi.FlyRewards(all, (RectTransform)_claimAllButton.transform);
        }

        void GoShop()
        {
            Close();
            UINav.GoToSource(ItemSourceType.Shop, ShopDatabase.EventShopId);
        }

        void OpenSeason()
        {
            Close();
            UINav.OpenSeason();
        }
    }
}
