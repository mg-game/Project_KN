using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선 바구니 팝업 (IDLE-01/02 · M32 · M28 · M30 · M08 · M55, UX-SPEC §2.11). 바구니 그림 + 쌓인 시간/상한 막대, 줄마다 "담긴 양 + 시간당 양",
    /// "다음 스테이지 클리어 시 +2%", [받기](금 — 이 화면의 금은 하나)와 [빠른 수선](Primary) + [빠른 수선권 사용].
    ///   · 빠른 수선 아래 줄: 무료면 '오늘 {남은}회 남음' ('무료'는 버튼 왼쪽 위 금 리본 하나만) / '[별조각] {비용} · 오늘 {남은}회 남음' / '오늘 모두 썼어요'.
    ///     버튼 줄 글은 상태가 바뀔 때만 다시 쓴다 (1초마다 읽는다).
    ///     돈을 쓰는 빠른 수선은 ShowSpendConfirm(별조각, '수선') — 모자라면 확인 창 대신 부족 팝업 (M55 · M08).
    ///   · 칸 리듬 (M32): 줄 수 = max(1, ceil(줄/6)), 격자 높이 = 줄 × 170 + (줄 − 1) × 16 → 각주 · 빠른 수선 판 · 창 높이가 따라 움직인다
    ///     (빠른 수선권 버튼이 없으면 판 180). 받기는 판 아래 24px. 한 줄이면 창 1320 → 약 1108. 다시 계산은 Refresh(전체) · 줄 수가 바뀔 때만.
    ///   · 받기에 성공했는데 남은 할 일(무료 빠른 수선 · 빠른 수선권)이 없으면 축하 0.8초 뒤 저절로 닫힌다 (화면 어디든 누르면 그만둔다, 빠른 수선 뒤에는 없음).
    ///   · 칸 숫자는 줄인 표기(FormatCompact), 글 속 숫자는 FormatCurrency, 시간당은 1,000 미만 소수 1~2자리 · 이상 FormatCurrency, 꺼 둔 동안만 쌓이는 줄의 '*'는 70%.
    ///     보상 이름과 양은 줄바꿈 없는 빈칸으로 묶는다 ('단추 3.60A'가 두 줄로 갈라지지 않게, M28).
    /// 열려 있는 동안 1초마다 다시 읽는다 (담긴 양이 늘어난다). 목록·칸은 재사용한다. 소리: 받기 BasketClaim · 빠른 수선 QuickIdle.
    /// </summary>
    public class IdleChestPopup : UIPopup
    {
        const float PollInterval = 1f;
        const string QuickIdleDoneMessage = "친구들이 밤새 할 일을 지금 바로 끝냈어요!";
        /// <summary>받은 뒤 저절로 닫히기까지 (실시간 초)</summary>
        const float AutoCloseDelay = 0.8f;

        // ── 칸 리듬 (M32, 창 위에서 · Body 기준) ──
        public const float BodyTop = 136f;
        public const float BodyBottom = 36f;
        public const float GridTop = 364f;
        public const float ChipHeight = 170f;
        public const float ChipGap = 16f;
        public const int ChipsPerRow = 6;
        public const float FootnoteGap = 14f;
        public const float QuickAfterFootnote = 44f;
        public const float QuickHeightWithTicket = 204f;
        public const float QuickHeightNoTicket = 180f;
        public const float ClaimGap = 24f;
        public const float ClaimHeight = 116f;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [SerializeField] MetaIcons _icons;

        [Header("바구니")]
        [SerializeField] RectTransform _basketRoot;
        [SerializeField] Image _basket;
        [SerializeField] GameObject _basketPlaceholder;
        [SerializeField] Image[] _placeholderFill = Array.Empty<Image>();
        [SerializeField] Image _basketGlow;
        [SerializeField] TMP_Text _elapsedText;
        [SerializeField] TMP_Text _capText;
        [SerializeField] Image _capFill;
        [SerializeField] TMP_Text _statusText;
        [SerializeField] TMP_Text _bonusText;

        [Header("줄")]
        [SerializeField] RectTransform _grid;
        [SerializeField] MetaRewardChip _chipTemplate;
        [SerializeField] TMP_Text _footnote;
        [SerializeField] TMP_Text _emptyText;

        [Header("빠른 수선")]
        [Tooltip("빠른 수선 판 (칸 리듬에 따라 자리 · 높이가 바뀐다)")]
        [SerializeField] RectTransform _quickSection;
        [SerializeField] TMP_Text _quickPreviewText;
        [SerializeField] Button _quickButton;
        [SerializeField] TMP_Text _quickTitle;
        [SerializeField] TMP_Text _quickCost;
        [SerializeField] Image _quickCostIcon;
        [Tooltip("무료일 때 빠른 수선 버튼 왼쪽 위 금 '무료' 리본")]
        [SerializeField] GameObject _freeRibbon;
        [SerializeField] Button _ticketButton;
        [SerializeField] TMP_Text _ticketLabel;
        [SerializeField] GameObject _quickLocked;
        [SerializeField] TMP_Text _quickLockedText;

        [Header("받기")]
        [SerializeField] Button _claimButton;
        [SerializeField] TMP_Text _claimLabel;
        [SerializeField] MetaSparkleBurst _sparkles;

        readonly List<IdleChestRate> _rates = new();
        readonly List<MetaReward> _pending = new();
        readonly List<MetaReward> _preview = new();
        MetaViewPool<MetaRewardChip> _chips;
        double[] _shownAmounts = Array.Empty<double>();
        float _pollTimer;
        int _shownMinutes = -1;
        int _shownStage = -1;
        int _shownRateStage = -1;
        bool _subscribed;
        Coroutine _bounce;
        /// <summary>칸 리듬을 마지막으로 맞춘 줄 수 · 빠른 수선권 버튼 보임 (바뀔 때만 다시 맞춘다)</summary>
        int _layoutLines = -1;
        bool _layoutTicket;
        /// <summary>저절로 닫힐 시각 (실시간, 0 이하 = 없음)</summary>
        float _autoCloseAt = -1f;
        /// <summary>버튼 줄을 마지막으로 쓴 상태 (1초마다 읽어도 바뀔 때만 다시 쓴다)</summary>
        bool _buttonsKnown;
        bool _shownCanClaim;
        bool _shownQuickUnlocked;
        int _shownQuickLeft;
        double _shownQuickCost;
        int _shownTickets;
        int _shownPreviewStage;

        protected override void Awake()
        {
            base.Awake();
            _chips = new MetaViewPool<MetaRewardChip>(_chipTemplate, _grid);
            _claimButton.onClick.AddListener(OnClaim);
            _quickButton.onClick.AddListener(OnQuickIdle);
            _ticketButton.onClick.AddListener(OnTicket);
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
                Game.IdleChest.Changed += OnChanged;
                Game.MetaRewards.WalletChanged += OnChanged;
                _subscribed = true;
            }
            var features = Game.Features;
            if (features.IsNew(FeatureId.IdleChest)) features.MarkSeen(FeatureId.IdleChest);
            if (features.IsNew(FeatureId.QuickIdle)) features.MarkSeen(FeatureId.QuickIdle);
            _shownRateStage = -1;
            _shownMinutes = -1;
            _shownStage = -1;
            _layoutLines = -1;
            _autoCloseAt = -1f;
            Refresh(true);
        }

        protected override void OnClose()
        {
            _autoCloseAt = -1f;
            if (!_subscribed) return;
            _subscribed = false;
            if (!Game.IsReady || Game.IdleChest == null) return;
            Game.IdleChest.Changed -= OnChanged;
            Game.MetaRewards.WalletChanged -= OnChanged;
        }

        void OnChanged() => Refresh(true);

        void Update()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            if (_autoCloseAt > 0f)
            {
                // 어디든 누르면 저절로 닫기를 그만둔다 (빠른 수선을 더 하려는 손가락)
                if (PointerPressedThisFrame())
                {
                    _autoCloseAt = -1f;
                }
                else if (Time.unscaledTime >= _autoCloseAt)
                {
                    _autoCloseAt = -1f;
                    Close();
                    return;
                }
            }
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer <= 0f) Refresh(false);
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

        // ───────────── 표시 ─────────────

        void Refresh(bool full)
        {
            _pollTimer = PollInterval;
            if (!MetaUi.IsActive) return;
            var chest = Game.IdleChest;

            var elapsed = chest.Elapsed;
            int minutes = (int)elapsed.TotalMinutes;
            bool isFull = chest.IsFull;
            if (full || minutes != _shownMinutes)
            {
                _shownMinutes = minutes;
                _elapsedText.text = MetaUi.Duration(elapsed);
                _capText.text = $"{MetaUi.Duration(chest.Cap)} 중";
                // 가득 차기까지 남은 시간 (분 단위로만 다시 쓴다)
                _statusText.text = isFull
                    ? FullMessage()
                    : $"가득 차기까지 {MetaUi.Duration(chest.Cap - elapsed)} · 자리를 비워도 친구들이 선물을 모아 와요";
                _statusText.color = isFull ? UITheme.Gold : UITheme.SubText;
            }
            MetaUi.SetBar(_capFill, chest.FillRatio);
            _capFill.color = isFull ? UITheme.Gold : UITheme.Lavender;
            if (_basketGlow != null) _basketGlow.color = MetaUi.Alpha(isFull ? UITheme.Gold : UITheme.Lamp, isFull ? 0.6f : 0.3f);

            int stage = chest.ArtStage;
            if (full || stage != _shownStage)
            {
                _shownStage = stage;
                var sprite = _icons != null ? _icons.Basket(stage) : null;
                _basket.enabled = sprite != null;
                if (sprite != null) _basket.sprite = sprite;
                if (_basketPlaceholder != null) _basketPlaceholder.SetActive(sprite == null);
                for (int i = 0; i < _placeholderFill.Length; i++)
                {
                    if (_placeholderFill[i] != null) _placeholderFill[i].enabled = i < stage;
                }
            }

            if (full)
            {
                // 스테이지 성장 ×1.0025 = +0.25% — 소수 둘째 자리까지 ('0.#'이면 +0.3%로 반올림돼 틀린 수가 된다).
                // 별조각 줄이 천장(×1.5)에 닿았으면 '재료'만 자란다고 쓴다 (SPEC4 §13.4 · C.6)
                _bonusText.text = GrowthLine(chest.NextStageBonusPercent, chest.AnyLineAtStageCap);
            }

            RefreshLines(full);
            RefreshButtons(full);
            ApplyRhythm(full);
        }

        /// <summary>성장 한 줄 (순수): '다음 스테이지를 클리어하면 시간당 +0.25%' — 천장에 닿은 줄이 있으면 '…클리어하면 재료 시간당 +0.25%' (SPEC4 C.6)</summary>
        public static string GrowthLine(float percent, bool capped)
        {
            string value = percent.ToString("0.##", CultureInfo.InvariantCulture);
            return capped ? $"다음 스테이지를 클리어하면 재료 시간당 +{value}%" : $"다음 스테이지를 클리어하면 시간당 +{value}%";
        }

        /// <summary>상한 안내 (순수, SPEC4 C.6): '수선점 Lv.200이 되면 바구니가 16시간까지 늘어나요'</summary>
        public static string ShopLevelCapNote(int shopLevel, float capHours) =>
            $"수선점 {MetaUi.WithSubject($"Lv.{shopLevel}")} 되면 바구니가 {MetaUi.Hours(capHours)}까지 늘어나요";

        /// <summary>
        /// 가득 찼을 때 문구 — 다음 상한 단계가 있으면 그 스테이지를 알려 주고, 스테이지 단계를 다 넘었으면 수선점 레벨 단계를 알려 준다
        /// (16시간 단계 — '수선점 Lv.200이 되면 바구니가 16시간까지 늘어나요', SPEC4 §13.4). 복귀 화면도 같은 글을 쓴다
        /// </summary>
        public static string FullMessage()
        {
            var config = Game.MetaDb != null ? Game.MetaDb.idleChest : null;
            int cleared = Game.Battle != null ? Game.Battle.HighestStage - 1 : 0;
            IdleCapStep next = null;
            if (config != null)
            {
                foreach (var step in config.capSteps)
                {
                    if (step == null || step.stageCleared <= cleared) continue;
                    if (next == null || step.stageCleared < next.stageCleared) next = step;
                }
            }
            if (next != null)
                return $"바구니가 가득 찼어요! {MetaUi.WithObject(BattleSystem.FormatStage(next.stageCleared))} 클리어하면 {MetaUi.Hours(next.capHours)}까지 늘어나요";
            var chest = Game.IdleChest;
            if (chest != null && chest.TryGetNextShopLevelCap(out int shopLevel, out float capHours))
                return $"바구니가 가득 찼어요! {ShopLevelCapNote(shopLevel, capHours)}";
            return "바구니가 가득 찼어요! 자주 들러서 선물을 받아 주세요";
        }

        void RefreshLines(bool full)
        {
            var chest = Game.IdleChest;
            int highest = Game.Battle.HighestStage;
            if (full || highest != _shownRateStage)
            {
                _shownRateStage = highest;
                chest.GetRates(_rates);
                int count = 0;
                for (int i = 0; i < _rates.Count; i++)
                {
                    if (_rates[i].PerHour <= 0) continue;
                    var chip = _chips.Get(count);
                    chip.Set(ToReward(_rates[i], 0));
                    chip.SetSubText(RateText(_rates[i].PerHour, _rates[i].OfflineOnly));
                    count++;
                }
                _chips.Trim(count);
                bool offlineLine = false;
                foreach (var rate in _rates) offlineLine |= rate.OfflineOnly && rate.PerHour > 0;
                _footnote.gameObject.SetActive(offlineLine);
                if (_shownAmounts.Length < count) _shownAmounts = new double[Math.Max(count, 12)];
                for (int i = 0; i < _shownAmounts.Length; i++) _shownAmounts[i] = -1;
                _emptyText.gameObject.SetActive(count == 0);
            }

            chest.GetPending(_pending);
            int index = 0;
            for (int i = 0; i < _rates.Count; i++)
            {
                if (_rates[i].PerHour <= 0) continue;
                double amount = FindPending(_rates[i]);
                if (index < _shownAmounts.Length && amount != _shownAmounts[index])
                {
                    _shownAmounts[index] = amount;
                    var chip = _chips.Items[index];
                    chip.SetAmountText(amount > 0 ? ChipAmount(_rates[i], amount) : "0");
                    chip.SetDimmed(amount <= 0);
                }
                index++;
            }
        }

        /// <summary>
        /// 받기 · 빠른 수선 · 수선권 버튼. 1초마다 불리지만 상태(받을 수 있음 · 빠른 수선 열림 · 남은 횟수 · 비용 · 수선권 · 최고 스테이지)가
        /// 그대로면 아무것도 쓰지 않는다 (full = 열기 · 바뀜 알림이면 늘 다시 쓴다).
        /// 무료면 '무료'는 버튼 왼쪽 위 금 리본 하나만 — 아래 줄은 '오늘 3회 남음' (Wave 2 r2: 리본 + '무료 · 오늘 3회 남음'으로 두 번 보였다)
        /// </summary>
        void RefreshButtons(bool full)
        {
            var chest = Game.IdleChest;
            bool canClaim = chest.CanClaim;
            bool quickUnlocked = chest.IsQuickIdleUnlocked;
            int left = quickUnlocked ? chest.QuickIdleLeftToday : 0;
            double cost = quickUnlocked ? chest.NextQuickIdleCost : 0d;
            int tickets = quickUnlocked ? chest.QuickIdleTickets : 0;
            int stage = Game.Battle != null ? Game.Battle.HighestStage : 0;
            if (!full && _buttonsKnown && canClaim == _shownCanClaim && quickUnlocked == _shownQuickUnlocked && left == _shownQuickLeft
                && cost.Equals(_shownQuickCost) && tickets == _shownTickets && stage == _shownPreviewStage)
            {
                return;
            }
            _buttonsKnown = true;
            _shownCanClaim = canClaim;
            _shownQuickUnlocked = quickUnlocked;
            _shownQuickLeft = left;
            _shownQuickCost = cost;
            _shownTickets = tickets;
            _shownPreviewStage = stage;

            _claimButton.interactable = canClaim;
            _claimLabel.text = canClaim ? "받기" : "아직 담긴 선물이 없어요";

            _quickLocked.SetActive(!quickUnlocked);
            _quickButton.gameObject.SetActive(quickUnlocked);
            if (!quickUnlocked)
            {
                _quickLockedText.text = Game.Features.GetRequirementText(FeatureId.QuickIdle);
                _ticketButton.gameObject.SetActive(false);
                _quickPreviewText.text = "바구니 2시간치 선물을 바로 받을 수 있어요";
                return;
            }

            _quickTitle.text = "빠른 수선";
            var state = QuickStateOf(left, cost);
            _quickCost.text = QuickCostText(state, left, cost);
            _quickCostIcon.gameObject.SetActive(state == QuickState.Paid);
            _quickButton.interactable = state != QuickState.UsedUp;
            bool free = state == QuickState.Free;
            if (_freeRibbon != null && _freeRibbon.activeSelf != free) _freeRibbon.SetActive(free);

            _ticketButton.gameObject.SetActive(tickets > 0);
            if (tickets > 0) _ticketLabel.text = $"빠른 수선권 사용 ({tickets}장)";

            chest.GetQuickIdlePreview(_preview);
            _quickPreviewText.text = _preview.Count > 0
                ? $"{MetaUi.Hours(QuickHours())}치: {MetaUi.DescribeKeep(_preview)}"
                : "바구니 2시간치 선물을 바로 받을 수 있어요";
        }

        /// <summary>빠른 수선 버튼 상태: 오늘 다 씀 / 무료 / 별조각</summary>
        internal enum QuickState
        {
            UsedUp = 0,
            Free = 1,
            Paid = 2,
        }

        internal static QuickState QuickStateOf(int left, double cost) =>
            left <= 0 || cost < 0 ? QuickState.UsedUp : cost <= 0 ? QuickState.Free : QuickState.Paid;

        /// <summary>
        /// 빠른 수선 버튼 아래 줄: 다 씀 '오늘 모두 썼어요' / 무료 '오늘 3회 남음' ('무료'는 금 리본이 말한다) / 별조각 '50 · 오늘 2회 남음' (앞에 별조각 아이콘)
        /// </summary>
        internal static string QuickCostText(QuickState state, int left, double cost) => state switch
        {
            QuickState.UsedUp => "오늘 모두 썼어요",
            QuickState.Free => $"오늘 {left}회 남음",
            _ => $"{MetaUi.Int(cost)} · 오늘 {left}회 남음",
        };

        /// <summary>
        /// 칸 리듬 (M32): 줄 수 → 격자 높이 · 각주 위 끝 · 빠른 수선 판 위 끝 · 판 높이 · 창 높이 (Body 기준 위에서).
        /// 각주 = 364 + 격자 + 14, 판 = 각주 + 44, 판 높이 = 빠른 수선권 버튼이 있으면 204 · 없으면 180, 받기는 판 아래 24 (116 높이), 창 = 136 + 받기 아래 끝 + 36
        /// </summary>
        internal static void Rhythm(int lines, bool ticket, out float gridHeight, out float footnoteTop, out float quickTop, out float quickHeight,
            out float windowHeight)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, lines) / (float)ChipsPerRow));
            gridHeight = rows * ChipHeight + (rows - 1) * ChipGap;
            footnoteTop = GridTop + gridHeight + FootnoteGap;
            quickTop = footnoteTop + QuickAfterFootnote;
            quickHeight = ticket ? QuickHeightWithTicket : QuickHeightNoTicket;
            windowHeight = BodyTop + quickTop + quickHeight + ClaimGap + ClaimHeight + BodyBottom;
        }

        /// <summary>칸 리듬을 맞춘다 (전체 갱신이거나 줄 수 · 빠른 수선권 버튼이 바뀔 때만 — 매초 갱신에는 그대로)</summary>
        void ApplyRhythm(bool full)
        {
            int lines = _chips != null ? _chips.ActiveCount : 0;
            bool ticket = _ticketButton != null && _ticketButton.gameObject.activeSelf;
            if (!full && lines == _layoutLines && ticket == _layoutTicket) return;
            _layoutLines = lines;
            _layoutTicket = ticket;
            Rhythm(lines, ticket, out float gridHeight, out float footnoteTop, out float quickTop, out float quickHeight, out float windowHeight);
            SetTop(_grid, GridTop, gridHeight);
            if (_footnote != null) SetTop(_footnote.rectTransform, footnoteTop, _footnote.rectTransform.rect.height);
            if (_emptyText != null) SetTop(_emptyText.rectTransform, GridTop + gridHeight * 0.5f - 30f, 60f);
            if (_quickSection != null)
            {
                SetTop(_quickSection, quickTop, quickHeight);
                // 판 안: 미리보기는 판 높이에 맞춰 (아래 14 여백), 잠김 덮개는 위아래 16 여백
                if (_quickPreviewText != null)
                {
                    var preview = _quickPreviewText.rectTransform;
                    preview.sizeDelta = new Vector2(preview.sizeDelta.x, Mathf.Max(40f, quickHeight - 80f));
                }
                if (_quickLocked != null && _quickLocked.transform is RectTransform locked)
                    locked.sizeDelta = new Vector2(locked.sizeDelta.x, quickHeight - 32f);
            }
            if (_window != null) _window.sizeDelta = new Vector2(_window.sizeDelta.x, windowHeight);
        }

        /// <summary>위쪽 고정 · 가로 늘림 칸의 위 끝 · 높이만 바꾼다 (좌우 여백은 그대로, 값이 같으면 쓰지 않는다)</summary>
        static void SetTop(RectTransform rect, float top, float height)
        {
            if (rect == null) return;
            var min = new Vector2(rect.offsetMin.x, -top - height);
            var max = new Vector2(rect.offsetMax.x, -top);
            if (rect.offsetMin != min) rect.offsetMin = min;
            if (rect.offsetMax != max) rect.offsetMax = max;
        }

        static float QuickHours() => Game.MetaDb != null ? Game.MetaDb.idleChest.quickIdleHours : 2f;

        double FindPending(IdleChestRate rate)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var reward = _pending[i];
                if (reward == null) continue;
                if (rate.Type == MetaRewardType.Currency
                        ? reward.type == MetaRewardType.Currency && reward.currency == rate.Currency
                        : reward.type == MetaRewardType.Item && reward.itemId == rate.ItemId)
                {
                    return reward.amount;
                }
            }
            return 0;
        }

        static MetaReward ToReward(IdleChestRate rate, double amount) => rate.Type == MetaRewardType.Currency
            ? MetaReward.Currency(rate.Currency, amount)
            : MetaReward.Item(rate.ItemId, amount);

        /// <summary>칸 아래 담긴 양 (재화 = 줄인 표기 '12.3A', 재료 = '×5')</summary>
        static string ChipAmount(IdleChestRate rate, double amount) => rate.Type == MetaRewardType.Currency
            ? RewardService.FormatAmountCompact(rate.Currency, amount)
            : UIFormat.TimesSmall + MetaUi.Int(Math.Floor(amount));

        /// <summary>
        /// 시간당 줄 '+3.12/시간' · '+45.6/시간' · '+1,800/시간' · '+12.3A/시간' — 꺼 둔 동안만 쌓이는 줄은 70% 크기 '*'.
        /// 1,000 미만은 소수 1~2자리, 이상은 칸 위 담긴 양(ChipAmount)과 같은 칸 표기 FormatCompact (1만 미만 콤마, 그 위 '12.3A') —
        /// 128 폭 금테 칸 안에서 '+123,456/시간*'처럼 길어지지 않는다 (M30 칸 표기)
        /// </summary>
        internal static string RateText(double perHour, bool offlineOnly)
        {
            string rate = FormatRate(perHour);
            return offlineOnly ? $"+{rate}/시간<size=70%>*</size>" : $"+{rate}/시간";
        }

        internal static string FormatRate(double perHour)
        {
            if (!(perHour >= 0)) return "0";
            if (perHour >= 1000) return NumberFormatter.FormatCompact(perHour);
            if (perHour >= 10) return (Math.Floor(perHour * 10 + 1e-9) / 10).ToString("0.#", Invariant);
            return (Math.Floor(perHour * 100 + 1e-9) / 100).ToString("0.##", Invariant);
        }

        // ───────────── 받기 / 빠른 수선 ─────────────

        void OnClaim()
        {
            if (!MetaUi.IsActive || MetaUi.BlockIfTampered()) return;
            var chest = Game.IdleChest;
            var result = chest.Claim(false);
            if (result == null)
            {
                MetaUi.Toast("아직 담긴 선물이 없어요");
                return;
            }
            UISfx.Play(UISfxKey.BasketClaim);
            Celebrate(result.Rewards);
            // 이 화면에서 더 할 일(무료 빠른 수선 · 빠른 수선권)이 없으면 축하를 보여 주고 저절로 닫는다 (M32)
            bool moreToDo = chest.HasFreeQuickIdle || chest.QuickIdleTickets > 0;
            _autoCloseAt = moreToDo ? -1f : Time.unscaledTime + AutoCloseDelay;
        }

        void OnQuickIdle()
        {
            if (!MetaUi.IsActive) return;
            _autoCloseAt = -1f;
            var chest = Game.IdleChest;
            if (!chest.CanQuickIdle(false, out var reason))
            {
                MetaUi.Toast(reason);
                return;
            }
            double cost = chest.NextQuickIdleCost;
            if (cost > 0)
            {
                // 별조각을 쓰기 전 확인 (모자라면 확인 창 대신 부족 팝업 — UIManager가 먼저 본다, M55 · M08)
                var ui = UIManager.Instance;
                if (ui != null)
                {
                    ui.ShowSpendConfirm("빠른 수선", $"바구니 {MetaUi.Hours(QuickHours())}치 선물을 지금 바로 받을까요?",
                        CurrencyType.Gem, cost, "수선", () => DoQuickIdle(false));
                }
                return;
            }
            DoQuickIdle(false);
        }

        void OnTicket()
        {
            if (!MetaUi.IsActive) return;
            _autoCloseAt = -1f;
            if (!Game.IdleChest.CanQuickIdle(true, out var reason))
            {
                MetaUi.Toast(reason);
                return;
            }
            DoQuickIdle(true);
        }

        void DoQuickIdle(bool useTicket)
        {
            if (!MetaUi.IsActive) return;
            _autoCloseAt = -1f;
            var result = Game.IdleChest.TryQuickIdle(useTicket);
            if (result == null) return;
            UISfx.Play(UISfxKey.QuickIdle);
            MetaUi.Toast(QuickIdleDoneMessage, ToastKind.Success);
            Celebrate(result.Rewards);
        }

        void Celebrate(List<MetaReward> rewards)
        {
            MetaUi.FlyRewards(rewards, _grid);
            if (_sparkles != null) _sparkles.BurstAt(_basketRoot, 16, UITheme.Lamp, 1.2f);
            if (_bounce != null) StopCoroutine(_bounce);
            _bounce = StartCoroutine(Bounce());
            Refresh(true);
        }

        IEnumerator Bounce()
        {
            const float duration = 0.4f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = MetaUi.Punch(t / duration);
                _basketRoot.localScale = new Vector3(1f + 0.16f * k, 1f - 0.1f * k, 1f);
                yield return null;
            }
            _basketRoot.localScale = Vector3.one;
            _bounce = null;
        }
    }
}
