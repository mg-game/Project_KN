using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선점 출석부 980×1300 (Q-03 · M69 · M59 · M53 · M18, UX-SPEC §2.11). 머리 그림(짝짝이 도장) + [새싹 출석][정규 출석] 쪽 탭(TabStyle Page) + 칸 7열.
    ///   · 새싹: 7일째 큰 보상(루즈리 확정)을 위에 크게 보여 주고 아래 7칸 128×184. 정규: 28칸 128×150 (7/14/21/28 강조).
    ///     새싹 판은 정규 28칸 판보다 짧아 그만큼(_starterTrim, 빌더가 잰다 — 144) 창 높이를 줄인다: 980×1156 — [도장 찍기] 위에 빈자리가 남지 않게 (칸 크기는 그대로).
    ///   · 오늘 칸이 빛나고, 오늘 칸이나 [도장 찍기](금)를 누르면 두 출석부에 함께 도장이 찍히고(AttendanceSystem.CheckIn) 보상이 날아간다
    ///     (도장 소리 Stamp — 마지막 날(7 · 28)은 StampSpecial).
    ///   · 다른 날 칸을 누르면 토스트 대신 작은 'N일차 보상' 카드 (보상 칸 96×96 — 누르면 재료 정보 · 재화 안내, 아무 데나 누르면 닫힘).
    ///   · 아래 안내 줄: 오늘 찍을 수 있으면 숨김, 찍었으면 '내일 또 만나요 · 다음 도장까지 7:12:03'(고정폭 시계), 시간 되돌림이면 그 안내.
    ///   · 자동 도장 (OpenAuto — 접속할 때 발표자가 부른다): 여는 연출 + 0.5초 뒤 아직 찍을 수 있으면 도장. 그 사이 화면을 누르면 바로 찍는다.
    ///     자동으로 찍은 뒤에는 [도장 찍기] 자리가 [확인](Primary)이 되어 누르면 닫힌다 (이름 CheckInButton 그대로).
    ///     빠른 아이콘 · ≡ 메뉴 · 길 안내는 Open (UINav.OpenAttendance — 자동 도장 없음).
    ///   · 누적 일수라 하루 빠져도 이어서 찍힌다. 도장 연출 중에 억지로 닫혀도 보상은 OnClose에서 마저 날린다.
    /// </summary>
    public class AttendancePopup : UIPopup
    {
        /// <summary>자동 도장: 여는 연출이 끝난 뒤 이만큼(실시간 초) 기다렸다 찍는다</summary>
        const float AutoStampDelay = 0.5f;

        [SerializeField] MetaIcons _icons;

        [Header("머리")]
        [SerializeField] TMP_Text _headerTitle;
        [SerializeField] TMP_Text _headerSub;

        [Header("탭")]
        [SerializeField] Button _starterTab;
        [SerializeField] Button _monthlyTab;
        [SerializeField] Image _starterTabImage;
        [SerializeField] Image _monthlyTabImage;
        [SerializeField] TMP_Text _starterTabLabel;
        [SerializeField] TMP_Text _monthlyTabLabel;

        [Header("새싹 출석")]
        [SerializeField] GameObject _starterPanel;
        [SerializeField] Image _heroPortrait;
        [SerializeField] TMP_Text _heroTitle;
        [SerializeField] TMP_Text _heroSub;
        [SerializeField] RectTransform _starterGrid;
        [SerializeField] AttendanceCellView _starterTemplate;

        [Header("정규 출석")]
        [SerializeField] GameObject _monthlyPanel;
        [SerializeField] RectTransform _monthlyGrid;
        [SerializeField] AttendanceCellView _monthlyTemplate;

        [Header("아래")]
        [SerializeField] Button _checkInButton;
        [SerializeField] TMP_Text _checkInLabel;
        [Tooltip("[도장 찍기] 금 그림 / 자동 도장 뒤 [확인] 보라 그림 (없으면 그림을 바꾸지 않는다)")]
        [SerializeField] Sprite _checkInGoldSprite;
        [SerializeField] Sprite _checkInConfirmSprite;
        [SerializeField] TMP_Text _statusText;
        [SerializeField] MetaSparkleBurst _sparkles;
        [SerializeField] RectTransform _shakeRoot;
        [Tooltip("새싹 출석을 볼 때 창을 줄이는 높이 (정규 28칸 판보다 짧은 만큼, 0이면 줄이지 않는다)")]
        [SerializeField] float _starterTrim;

        [Header("N일차 보상 카드 (다른 날 칸을 누르면)")]
        [SerializeField] GameObject _dayCard;
        [SerializeField] Button _dayCardCatcher;
        [SerializeField] TMP_Text _dayCardTitle;
        [SerializeField] TMP_Text _dayCardState;
        [SerializeField] RectTransform _dayCardRow;
        [SerializeField] MetaRewardChip _dayCardChip;

        MetaViewPool<AttendanceCellView> _starterCells;
        MetaViewPool<AttendanceCellView> _monthlyCells;
        MetaViewPool<MetaRewardChip> _dayChips;
        /// <summary>그날 보상을 푼 목록 (단추 · N분치는 단추 · 실타래 두 칩)</summary>
        readonly System.Collections.Generic.List<MetaReward> _dayPreview = new();
        Action<AttendanceCellView> _onCell;
        AttendanceTrack _track;
        bool _subscribed;
        bool _busy;
        float _timer;
        /// <summary>다음 Open이 자동 도장을 할지 (OpenAuto가 켠다)</summary>
        bool _autoPending;
        /// <summary>자동 도장 기다리는 중 (누르면 바로 찍는다)</summary>
        bool _autoWaiting;
        float _autoAt;
        /// <summary>자동으로 찍은 뒤 — 버튼이 [확인]이 된다</summary>
        bool _confirmMode;
        /// <summary>도장 연출 중인 결과 (억지로 닫혀도 보상을 한 번은 날린다)</summary>
        AttendanceCheckInResult _playing;
        /// <summary>빌더가 만든 창 높이 (정규 출석 판 기준)</summary>
        float _fullHeight = -1f;

        protected override void Awake()
        {
            base.Awake();
            if (Window != null) _fullHeight = Window.sizeDelta.y;
            _starterCells = new MetaViewPool<AttendanceCellView>(_starterTemplate, _starterGrid);
            _monthlyCells = new MetaViewPool<AttendanceCellView>(_monthlyTemplate, _monthlyGrid);
            if (_dayCardChip != null && _dayCardRow != null) _dayChips = new MetaViewPool<MetaRewardChip>(_dayCardChip, _dayCardRow);
            _onCell = OnCell;
            _starterTab.onClick.AddListener(() => SelectTrack(AttendanceTrack.Starter));
            _monthlyTab.onClick.AddListener(() => SelectTrack(AttendanceTrack.Monthly));
            _checkInButton.onClick.AddListener(OnCheckInButton);
            if (_dayCardCatcher != null) _dayCardCatcher.onClick.AddListener(HideDayCard);
            if (_dayCard != null) _dayCard.SetActive(false);
        }

        /// <summary>
        /// 접속 때 여는 출석부 (M69 — MetaPopupPresenter): 여는 연출 + 0.5초 뒤 아직 찍을 수 있으면 저절로 도장을 찍는다.
        /// 그 사이 화면을 누르면 기다리지 않고 바로 찍는다. 찍은 뒤 [도장 찍기] 자리는 [확인]이 된다
        /// </summary>
        public void OpenAuto()
        {
            _autoPending = true;
            Open();
        }

        protected override void OnOpen()
        {
            bool auto = _autoPending;
            _autoPending = false;
            if (!MetaUi.IsActive)
            {
                Close();
                return;
            }
            if (!_subscribed)
            {
                Game.Attendance.Changed += Refresh;
                _subscribed = true;
            }
            if (Game.Features.IsNew(FeatureId.Attendance)) Game.Features.MarkSeen(FeatureId.Attendance);
            _busy = false;
            _confirmMode = false;
            _playing = null;
            HideDayCard();
            SelectTrack(Game.Attendance.IsStarterComplete ? AttendanceTrack.Monthly : AttendanceTrack.Starter);
            _autoWaiting = auto && Game.Attendance.CanCheckIn;
            _autoAt = Time.unscaledTime + (UIMotion.Reduced ? UIMotion.PopupReducedIn : UIMotion.PopupWindowIn) + AutoStampDelay;
        }

        protected override void OnClose()
        {
            _busy = false;
            _autoWaiting = false;
            // 도장 연출 도중 닫힘: 받은 보상(이미 들어왔다)이 날아가는 모습만 마저
            if (_playing != null)
            {
                MetaUi.FlyRewards(_playing.Rewards, (RectTransform)_checkInButton.transform);
                _playing = null;
            }
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.IsReady && Game.Attendance != null) Game.Attendance.Changed -= Refresh;
        }

        void SelectTrack(AttendanceTrack track)
        {
            _track = track;
            bool starter = track == AttendanceTrack.Starter;
            // 쪽 탭 (M53 Page): 고른 탭 양피지 + Ink 굵게, 아닌 탭 TabNormal + SubText
            TabStyle.Apply(_starterTabImage, _starterTabLabel, starter, TabVariant.Page);
            TabStyle.Apply(_monthlyTabImage, _monthlyTabLabel, !starter, TabVariant.Page);
            ApplyWindowHeight(starter);
            HideDayCard();
            Refresh();
        }

        /// <summary>새싹 판은 짧은 만큼 창을 줄이고, 정규 판은 빌더 높이 그대로 (가운데 기준 — 바뀔 때만)</summary>
        void ApplyWindowHeight(bool starter)
        {
            var window = Window;
            if (window == null || _fullHeight <= 0f || _starterTrim <= 0f) return;
            float height = starter ? _fullHeight - _starterTrim : _fullHeight;
            var size = window.sizeDelta;
            if (Mathf.Approximately(size.y, height)) return;
            window.sizeDelta = new Vector2(size.x, height);
        }

        void Update()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            if (_autoWaiting)
            {
                // 누르면 기다리지 않고 바로 (그 누름이 [도장 찍기]여도 두 번 찍히지 않는다 — _busy)
                if (Time.unscaledTime >= _autoAt || PointerPressedThisFrame()) AutoStamp();
            }
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f) RefreshStatus();
        }

        static bool PointerPressedThisFrame()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            }
            return false;
        }

        void AutoStamp()
        {
            _autoWaiting = false;
            if (_busy || !Game.Attendance.CanCheckIn) return;
            if (CheckIn()) _confirmMode = true;
        }

        // ───────────── 표시 ─────────────

        void Refresh()
        {
            if (!IsOpen || !MetaUi.IsActive || _busy) return;
            var attendance = Game.Attendance;
            bool starterDone = attendance.IsStarterComplete;
            _starterTabLabel.text = starterDone ? "새싹 출석 (완료)" : "새싹 출석";
            _monthlyTabLabel.text = attendance.MonthlyCycle > 0 ? $"정규 출석 {attendance.MonthlyCycle + 1}회차" : "정규 출석";
            _headerTitle.text = "매일 도장을 찍으면 선물을 받아요";
            int total = Game.Data.meta.attendance.totalDays;
            _headerSub.text = total > 0 ? $"지금까지 {total}번 찍었어요 · 하루 빠져도 이어서 찍혀요" : "하루 빠져도 이어서 찍혀요";

            bool starter = _track == AttendanceTrack.Starter;
            _starterPanel.SetActive(starter);
            _monthlyPanel.SetActive(!starter);
            if (starter) BindTrack(AttendanceTrack.Starter, _starterCells);
            else BindTrack(AttendanceTrack.Monthly, _monthlyCells);
            if (starter) BindHero();
            RefreshStatus();
        }

        void BindTrack(AttendanceTrack track, MetaViewPool<AttendanceCellView> cells)
        {
            var attendance = Game.Attendance;
            int length = attendance.GetLength(track);
            for (int day = 1; day <= length; day++)
            {
                var cell = cells.Get(day - 1);
                cell.Bind(day, attendance.GetDay(track, day), attendance.IsStamped(track, day), attendance.IsToday(track, day), _onCell);
            }
            cells.Trim(length);
        }

        /// <summary>새싹 출석 7일째 큰 보상 (루즈리 확정)</summary>
        void BindHero()
        {
            var attendance = Game.Attendance;
            int length = attendance.GetLength(AttendanceTrack.Starter);
            var def = attendance.GetDay(AttendanceTrack.Starter, length);
            var reward = MetaUi.FirstPreview(def != null ? def.rewards : null, out _);
            var sprite = reward != null && _icons != null ? _icons.Reward(reward) : null;
            _heroPortrait.sprite = sprite != null ? sprite : _icons != null ? _icons.Star : null;
            _heroPortrait.color = Color.white;
            _heroPortrait.preserveAspect = true;
            string name = reward != null ? MetaUi.NameOf(reward) : "특별한 선물";
            _heroTitle.text = def != null && !string.IsNullOrEmpty(def.label) ? $"{length}일째 {def.label}" : $"{length}일째 {name}";
            int days = attendance.StarterDays;
            _heroSub.text = attendance.IsStarterComplete
                ? "새싹 출석을 모두 채웠어요!"
                : $"{days}/{length}일 · {Math.Max(0, length - days)}번 더 찍으면 만날 수 있어요";
        }

        /// <summary>
        /// 아래 버튼 · 안내 (1초마다 — 시계가 바뀌므로): 찍을 수 있으면 [도장 찍기](금) + 안내 숨김, 찍었으면 안내 '내일 또 만나요 · 다음 도장까지 {시계}'
        /// (자동 도장 뒤에는 버튼이 [확인]), 시간 되돌림이면 그 안내
        /// </summary>
        void RefreshStatus()
        {
            _timer = 1f;
            if (!MetaUi.IsActive) return;
            var attendance = Game.Attendance;
            bool can = attendance.CanCheckIn;
            bool done = attendance.HasCheckedInToday;
            bool confirm = _confirmMode && done && !_busy;
            _checkInButton.gameObject.SetActive(can || !done || confirm);
            _checkInButton.interactable = confirm || (can && !_busy);
            ApplyCheckInLook(confirm);
            bool showStatus = !can;
            if (_statusText.gameObject.activeSelf != showStatus) _statusText.gameObject.SetActive(showStatus);
            if (!showStatus) return;
            if (done) _statusText.text = "내일 또 만나요 · 다음 도장까지 " + UIFormat.ClockMono(Game.DailyClock.TimeUntilDailyReset);
            else if (Game.DailyClock.IsTampered) _statusText.text = DailyClock.TamperedMessage;
            else _statusText.text = "지금은 도장을 찍을 수 없어요";
        }

        bool _lookConfirm;
        bool _lookKnown;

        /// <summary>[도장 찍기](금, Ink) ↔ [확인](보라, 흰 글자) — 바뀔 때만</summary>
        void ApplyCheckInLook(bool confirm)
        {
            if (_lookKnown && confirm == _lookConfirm) return;
            _lookKnown = true;
            _lookConfirm = confirm;
            _checkInLabel.text = confirm ? "확인" : "도장 찍기";
            var color = confirm ? UITheme.Text : UITheme.Ink;
            color.a = _checkInLabel.color.a;
            _checkInLabel.color = color;
            var sprite = confirm ? _checkInConfirmSprite : _checkInGoldSprite;
            if (sprite != null && _checkInButton.targetGraphic is Image skin) skin.sprite = sprite;
        }

        // ───────────── 도장 ─────────────

        void OnCheckInButton()
        {
            if (_confirmMode && Game.Attendance.HasCheckedInToday && !_busy)
            {
                Close();
                return;
            }
            CheckIn();
        }

        void OnCell(AttendanceCellView cell)
        {
            if (!MetaUi.IsActive || _busy) return;
            var attendance = Game.Attendance;
            if (attendance.IsToday(_track, cell.Day) && attendance.CanCheckIn)
            {
                _autoWaiting = false;
                CheckIn();
                return;
            }
            ShowDayCard(cell);
        }

        /// <summary>'N일차 보상' 카드: 그날 보상을 모두 (보상 칸 96 — 누르면 재료 정보 · 재화 안내) + 받았는지. 아무 데나 누르면 닫힌다 (M59)</summary>
        void ShowDayCard(AttendanceCellView cell)
        {
            var def = cell.Def;
            if (def == null || def.rewards == null || def.rewards.Count == 0) return;
            if (_dayCard == null || _dayChips == null)
            {
                // 카드가 없는 예전 장면: 예전 토스트
                string old = Game.Attendance.IsStamped(_track, cell.Day) ? "받았어요" : "도장을 찍으면 받아요";
                MetaUi.Toast($"{cell.Day}일째: {MetaUi.DescribeData(def.rewards)} — {old}");
                return;
            }
            _dayCardTitle.text = $"{cell.Day}일차 보상";
            // 단추 · N분치는 실제 지급과 같게 단추 · 실타래 두 칩 (MetaUi.Preview)
            MetaUi.Preview(def.rewards, _dayPreview);
            int count = Mathf.Min(_dayPreview.Count, 4);
            for (int i = 0; i < count; i++) _dayChips.Get(i).Set(_dayPreview[i]);
            _dayChips.Trim(count);
            bool stamped = Game.Attendance.IsStamped(_track, cell.Day);
            _dayCardState.text = stamped ? "받았어요" : def.highlight ? "이날 도장을 찍으면 꼭 받아요" : "이날 도장을 찍으면 받아요";
            _dayCardState.color = stamped ? UITheme.Positive : UITheme.SubText;
            _dayCard.SetActive(true);
        }

        void HideDayCard()
        {
            if (_dayCard != null && _dayCard.activeSelf) _dayCard.SetActive(false);
        }

        /// <summary>
        /// 뒤로가기 · 어둡게 덮기 (UX-SPEC §3.2 · IA §4.7 안쪽부터 한 겹씩): 'N일차 보상' 카드가 떠 있으면 카드만 닫고,
        /// 아니면 출석부를 닫는다 (기본 = Close — 도장 연출 중이어도 OnClose가 보상을 마저 날린다)
        /// </summary>
        protected override void OnDimOrBack()
        {
            if (_dayCard != null && _dayCard.activeSelf)
            {
                HideDayCard();
                return;
            }
            base.OnDimOrBack();
        }

        /// <summary>도장 (찍었으면 true). 찍을 수 없으면 까닭을 토스트</summary>
        bool CheckIn()
        {
            if (!MetaUi.IsActive || _busy) return false;
            var attendance = Game.Attendance;
            if (!attendance.CanCheckIn)
            {
                if (MetaUi.BlockIfTampered()) return false;
                MetaUi.Toast(attendance.HasCheckedInToday ? "오늘은 이미 도장을 찍었어요" : "지금은 도장을 찍을 수 없어요");
                return false;
            }
            HideDayCard();
            // 친구 확정 칸이면 이미 함께인지 미리 봐 둔다 (이미 있으면 기억편으로 바뀐다)
            string lineageId = FindLineageReward(attendance, AttendanceTrack.Starter) ?? FindLineageReward(attendance, AttendanceTrack.Monthly);
            bool ownedBefore = lineageId != null && Game.Characters.IsOwned(lineageId);
            int starterLength = attendance.GetLength(AttendanceTrack.Starter);
            int monthlyLength = attendance.GetLength(AttendanceTrack.Monthly);
            // 도장 연출이 끝나기 전에 Changed로 칸이 미리 바뀌지 않게 먼저 잠근다
            _busy = true;
            var result = attendance.CheckIn();
            if (result == null)
            {
                _busy = false;
                return false;
            }
            bool special = (result.StarterDay > 0 && result.StarterDay == starterLength) || (result.MonthlyDay > 0 && result.MonthlyDay == monthlyLength);
            UISfx.Play(special ? UISfxKey.StampSpecial : UISfxKey.Stamp);
            _playing = result;
            StartCoroutine(PlayCheckIn(result, ownedBefore));
            return true;
        }

        static string FindLineageReward(AttendanceSystem attendance, AttendanceTrack track)
        {
            int next = attendance.GetNextDay(track);
            var def = next > 0 ? attendance.GetDay(track, next) : null;
            if (def == null) return null;
            foreach (var reward in def.rewards)
            {
                if (reward != null && reward.type == MetaRewardType.Lineage) return reward.itemId;
            }
            return null;
        }

        /// <summary>보이는 출석부의 그 칸에 도장 → 흔들림 → 보상 날아가기 → 다시 그리기</summary>
        IEnumerator PlayCheckIn(AttendanceCheckInResult result, bool lineageOwnedBefore)
        {
            _checkInButton.interactable = false;
            int day = _track == AttendanceTrack.Starter ? result.StarterDay : result.MonthlyDay;
            // 새싹이 이미 끝났는데 새싹 탭을 보고 있었다면 정규 칸으로 옮겨 보여 준다
            if (day <= 0 && _track == AttendanceTrack.Starter)
            {
                _busy = false;
                SelectTrack(AttendanceTrack.Monthly);
                _busy = true;
                day = result.MonthlyDay;
            }
            var cells = _track == AttendanceTrack.Starter ? _starterCells : _monthlyCells;
            AttendanceCellView cell = day > 0 && day <= cells.ActiveCount ? cells.Items[day - 1] : null;
            if (cell != null)
            {
                cell.PlayStamp(_sparkles);
                yield return new WaitForSecondsRealtime(0.25f);
                yield return Shake();
                _playing = null;
                MetaUi.FlyRewards(result.Rewards, cell.Visual);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            else
            {
                _playing = null;
                MetaUi.FlyRewards(result.Rewards, (RectTransform)_checkInButton.transform);
            }

            foreach (var reward in result.Rewards)
            {
                if (reward == null || reward.type != MetaRewardType.Lineage) continue;
                MetaUi.Toast(lineageOwnedBefore
                    ? $"{KoreanText.WithSubject(MetaUi.NameOf(reward))} 이미 함께라서 기억편으로 바꿔 드렸어요"
                    : $"{KoreanText.WithSubject(MetaUi.NameOf(reward))} 수선점에 찾아왔어요!", ToastKind.Success);
            }
            _busy = false;
            Refresh();
        }

        IEnumerator Shake()
        {
            if (_shakeRoot == null) yield break;
            const float duration = 0.16f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                _shakeRoot.anchoredPosition = new Vector2(0f, -6f * MetaUi.Punch(k));
                yield return null;
            }
            _shakeRoot.anchoredPosition = Vector2.zero;
        }
    }
}
