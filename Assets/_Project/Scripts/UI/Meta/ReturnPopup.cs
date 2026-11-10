using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 복귀 화면 "다녀오셨어요!" 920×(1054~1240) (IDLE-03 · M31 · M49, UX-SPEC §2.11). 10분 넘게 비웠다 돌아오면 발표자가 띄운다.
    ///   머리 그림(UI/return_header) → 제목 → "6시간 12분 동안 친구들이 수선점을 지켰어요"(비운 시간 — 마음을 여는 줄이라 늘 둔다)
    ///   → 바구니 줄 '바구니 6시간 12분 / 8시간 (· 가득)' + 상한 막대 (가득이면 "바구니가 가득 찼어요! …") → 보상 칸 0.08초 시차·0.8초 카운트업
    ///   → 계보 성격 한 줄 (가진 계보 중 무작위, 이름 색 = Lerp(계보 색, Ink, 0.55) — 크림 말풍선 위 4.5:1 미만이면 Ink)
    ///   → 버튼: 두 배가 되면 [그냥 받기](Secondary 320×110, 왼쪽) + [두 배로 받기](금 500×120, 오른쪽, '하루 한 번' / '두 배 수선권 1장 (보유 N)',
    ///     오른쪽 위 빨간 알약 '무료' / '×2' — '무료'는 알약 하나만). 두 배가 안 되면 두 배 버튼과 안내 줄을 숨기고 [받기] 하나 (금 520×120, 가운데).
    ///   격자 · 창 높이는 보상 줄 수에 맞춘다 (Layout — 한 줄이면 창 1054). 칸은 줄마다 가운데로 모은다 (덜 찬 마지막 줄도 — PlaceChips).
    /// 받으면 수선 바구니를 받는 것과 같다 (IdleChest.Claim). 닫기만 하면 바구니에 그대로 남는다:
    ///   어둡게 덮기 · X · 뒤로 = 받는 중에는 무시, 아니면 닫고 담긴 게 있었으면 '선물은 수선 바구니에 그대로 있어요' 토스트 (M49).
    ///   받는 도중 억지로 닫혀도(화면 이동 · 모두 닫기) 받은 보상은 OnClose에서 마저 날린다 (_flown).
    /// 소리: 카운트업 CountTick(70ms에 하나) + 끝 CountFinish, 받기 Claim / 두 배 RewardDouble.
    /// 다시 만난 날(CR-06, SPEC4 §8.2 — RM-2 P4-6): 한 번짜리 바구니 상한(IdleChest.BonusCap, 72시간 넘게 비움 → 24시간)이 걸려 있으면
    ///   가득 안내 자리에 '오랜만이라 바구니를 더 채워 뒀어요' (상한 줄은 그대로 '바구니 24시간 / 24시간 · 가득').
    /// </summary>
    public class ReturnPopup : UIPopup
    {
        /// <summary>다시 만난 날 한 번짜리 바구니 상한이 걸렸을 때의 안내 줄 (SPEC4 §8.2)</summary>
        public const string ComebackBasketLine = "오랜만이라 바구니를 더 채워 뒀어요";

        const float Stagger = 0.08f;
        const float CountUpTime = 0.8f;
        const float CountUpStep = 0.04f;

        // ── 격자 · 창 크기 (M31) ──
        /// <summary>보상 칸 격자 위 끝 (창 위에서)</summary>
        public const float GridTop = 614f;
        /// <summary>보상 칸 높이 · 줄 간격 · 한 줄 칸 수</summary>
        public const float ChipHeight = 170f;
        public const float ChipGap = 16f;
        public const int ChipsPerRow = 6;
        /// <summary>보상 칸 너비 (장면 격자가 없을 때의 기본 — 장면 격자는 128×170 · 간격 16)</summary>
        public const float ChipWidth = 128f;
        /// <summary>칸 내용(아이콘 84 · 양 · 이름)이 차지하는 위쪽 높이 — 그 아래는 비어 있다 (LineTuck만큼 올라온 말풍선과 12 이상 띈다)</summary>
        public const float ChipContentHeight = 150f;
        /// <summary>두 줄일 때(예전 고정 크기)의 격자 높이 · 창 높이</summary>
        public const float GridTwoRows = 2 * ChipHeight + ChipGap;
        public const float WindowTwoRows = 1240f;
        /// <summary>계보 줄이 격자 아래 끝 위로 올라가는 만큼 (칸의 이름 아래 빈자리 — 칸 내용은 위 150px 안에 있다)</summary>
        public const float LineTuck = 8f;

        static readonly Vector2 PairClaimPos = new(40f, 64f);
        static readonly Vector2 PairClaimSize = new(320f, 110f);
        static readonly Vector2 SoloClaimPos = new(0f, 64f);
        static readonly Vector2 SoloClaimSize = new(520f, 120f);
        const float PairClaimFont = 34f;
        const float SoloClaimFont = 40f;
        const string KeptToast = "선물은 수선 바구니에 그대로 있어요";

        [SerializeField] MetaIcons _icons;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _awayText;
        [SerializeField] TMP_Text _capText;
        [SerializeField] Image _capFill;
        [SerializeField] TMP_Text _fullText;
        [SerializeField] RectTransform _grid;
        [SerializeField] MetaRewardChip _chipTemplate;
        [SerializeField] TMP_Text _emptyText;
        [Header("계보 한 줄")]
        [SerializeField] GameObject _lineRoot;
        [SerializeField] Image _linePortrait;
        [Tooltip("초상화 뒤 밝은 받침 (계보 색을 옅게 섞는다)")]
        [SerializeField] Image _linePortraitBack;
        [SerializeField] TMP_Text _lineName;
        [SerializeField] TMP_Text _lineText;
        [Tooltip("이름 앞 10px 계보 색 점 (없어도 됨)")]
        [SerializeField] Image _lineDot;
        [Header("버튼")]
        [SerializeField] Button _claimButton;
        [SerializeField] TMP_Text _claimLabel;
        [SerializeField] Button _doubleButton;
        [SerializeField] TMP_Text _doubleLabel;
        [SerializeField] TMP_Text _doubleSubLabel;
        [Tooltip("두 배 버튼 오른쪽 위 빨간 알약 ('무료' / '×2')")]
        [SerializeField] GameObject _doubleBadge;
        [SerializeField] TMP_Text _doubleBadgeText;
        [SerializeField] TMP_Text _noteText;
        [Tooltip("[받기] 혼자일 때의 금 그림 / [그냥 받기]일 때의 보조 그림 (없으면 그림을 바꾸지 않는다)")]
        [SerializeField] Sprite _claimGoldSprite;
        [SerializeField] Sprite _claimSecondarySprite;
        [SerializeField] MetaSparkleBurst _sparkles;

        readonly List<MetaReward> _pending = new();
        readonly List<MetaReward> _shown = new();
        MetaViewPool<MetaRewardChip> _chips;
        Coroutine _reveal;
        bool _claiming;
        /// <summary>받은 보상을 이미 날렸는지 (받는 도중 억지로 닫혀도 한 번은 날린다)</summary>
        bool _flown;
        IdleChestClaimResult _result;
        bool _portraitBackRead;
        Color _portraitBackBase = Color.white;
        /// <summary>버튼 배치 (두 배 버튼이 있는 짝 / [받기] 혼자). 처음에는 모름</summary>
        int _buttonLayout = -1;
        /// <summary>두 배 버튼 글을 마지막으로 쓴 상태 (무료 = −1, 아니면 수선권 수 — 처음에는 모름)</summary>
        int _shownDoubleKey = int.MinValue;
        /// <summary>장면 격자의 GridLayoutGroup (칸 크기 · 간격만 읽고 끈다 — 줄 가운데 맞춤은 PlaceChips가 한다)</summary>
        GridLayoutGroup _gridLayout;
        bool _gridLayoutRead;

        /// <summary>초상화 받침에 섞는 계보 색 비율</summary>
        const float PortraitBackTint = 0.35f;
        /// <summary>말풍선 이름 색 = Lerp(계보 색, Ink, 0.55)</summary>
        const float SpeakerInkMix = 0.55f;
        /// <summary>계보 이름 글자 (22 굵게)의 최소 대비 (§1.2 — 24px 이하 4.5:1)</summary>
        const float SpeakerMinContrast = 4.5f;

        /// <summary>X도 닫기 정책을 거친다 (받는 중 무시 · 남은 선물 안내)</summary>
        protected override bool CloseButtonUsesDismiss => true;

        protected override void Awake()
        {
            base.Awake();
            EnsurePool();
            _claimButton.onClick.AddListener(() => OnClaim(false));
            _doubleButton.onClick.AddListener(() => OnClaim(true));
        }

        /// <summary>
        /// 보상 칸 풀을 한 번만 만든다. 빌더가 팝업을 꺼 둔 채 저장해서, 처음 Show가 불릴 때는 Awake가 아직 돌지 않았다
        /// (Show가 칸을 채운 뒤 Open → SetActive에서 Awake가 돈다). 그래서 Show도 여기서 풀을 만들고, Awake는 이미 있으면 그대로 쓴다.
        /// </summary>
        void EnsurePool()
        {
            _chips ??= new MetaViewPool<MetaRewardChip>(_chipTemplate, _grid);
        }

        /// <summary>
        /// 보상 칸 수 → 줄 수 · 격자 높이 · 계보 줄 위 끝 · 창 높이 (M31: rows = max(1, ceil(n/6)), h = rows·170 + (rows−1)·16,
        /// 창 = 1240 − (356 − h) → 한 줄 1054 · 두 줄 1240). 계보 줄은 격자 바로 아래 (칸 이름 밑 빈자리로 8px 올라가) 버튼과 겹치지 않는다
        /// (명세의 '−614 − h − 40 − 41'은 새 120px 버튼(아래 64)과 36px 겹쳐 쓰지 않는다 — 창 높이 공식은 그대로).
        /// </summary>
        internal static void Layout(int count, out int rows, out float gridHeight, out float lineTop, out float windowHeight)
        {
            rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, count) / (float)ChipsPerRow));
            gridHeight = rows * ChipHeight + (rows - 1) * ChipGap;
            windowHeight = WindowTwoRows - (GridTwoRows - gridHeight);
            lineTop = GridTop + gridHeight - LineTuck;
        }

        /// <summary>비운 시간(초)으로 복귀 화면을 연다</summary>
        public void Show(long awaySeconds)
        {
            if (!MetaUi.IsActive) return;
            EnsurePool();
            _claiming = false;
            _flown = false;
            _result = null;
            var chest = Game.IdleChest;
            _titleText.text = "다녀오셨어요!";
            _awayText.text = awaySeconds > 0
                ? $"{MetaUi.Duration(awaySeconds)} 동안 친구들이 수선점을 지켰어요"
                : "친구들이 수선점을 지키고 있었어요";

            // 바구니 줄은 이름을 붙여 비운 시간과 헷갈리지 않게 (바구니는 비우기 전부터 차 있었을 수 있다)
            var elapsed = chest.Elapsed;
            bool full = chest.IsFull;
            _capText.text = full
                ? $"바구니 {MetaUi.Duration(elapsed)} / {MetaUi.Duration(chest.Cap)} · 가득"
                : $"바구니 {MetaUi.Duration(elapsed)} / {MetaUi.Duration(chest.Cap)}";
            MetaUi.SetBar(_capFill, chest.FillRatio);
            _capFill.color = full ? UITheme.Gold : UITheme.Lavender;
            bool bonus = chest.BonusCap.Ticks > 0;
            _fullText.gameObject.SetActive(full || bonus);
            if (bonus) _fullText.text = ComebackBasketLine;
            else if (full) _fullText.text = IdleChestPopup.FullMessage();

            chest.GetPending(_pending);
            _shown.Clear();
            foreach (var reward in _pending)
            {
                if (reward != null && reward.amount > 0) _shown.Add(reward.Clone());
            }
            for (int i = 0; i < _shown.Count; i++)
            {
                var chip = _chips.Get(i);
                chip.Set(_shown[i]);
                chip.SetAmountText(string.Empty);
                chip.SetAmountColor(UITheme.Cream);
            }
            _chips.Trim(_shown.Count);
            _emptyText.gameObject.SetActive(_shown.Count == 0);
            ApplyLayout(_shown.Count);

            ApplyLineageLine();
            RefreshButtons();
            Open();
        }

        /// <summary>격자 높이 · 계보 줄 · 창 높이를 보상 줄 수에 맞춘다 (Show에서 한 번)</summary>
        void ApplyLayout(int count)
        {
            Layout(count, out _, out float gridHeight, out float lineTop, out float windowHeight);
            if (_grid != null) _grid.sizeDelta = new Vector2(_grid.sizeDelta.x, gridHeight);
            if (_lineRoot != null && _lineRoot.transform is RectTransform line)
                line.anchoredPosition = new Vector2(line.anchoredPosition.x, -lineTop);
            if (_emptyText != null)
            {
                var empty = _emptyText.rectTransform;
                empty.anchoredPosition = new Vector2(empty.anchoredPosition.x, -(GridTop + gridHeight * 0.5f - empty.sizeDelta.y * 0.5f));
            }
            if (_window != null) _window.sizeDelta = new Vector2(_window.sizeDelta.x, windowHeight);
            PlaceChips(count);
        }

        /// <summary>
        /// 칸 자리 (순수): index번째 칸의 왼쪽 위 (격자 왼쪽 위에서 x는 오른쪽, y는 아래로). 한 줄 ChipsPerRow칸, 줄마다 칸을 가운데로 모은다 —
        /// 덜 찬 마지막 줄 (축제 사탕까지 7칸 = 6 + 1, 늦은 스테이지 9칸 = 6 + 3)도 가운데 (GridLayoutGroup은 덜 찬 줄을 왼쪽에 붙인다 — r1)
        /// </summary>
        internal static Vector2 ChipOffset(int index, int count, float gridWidth, Vector2 cell, Vector2 gap)
        {
            int row = Mathf.Max(0, index) / ChipsPerRow;
            int column = Mathf.Max(0, index) % ChipsPerRow;
            int inRow = Mathf.Clamp(count - row * ChipsPerRow, 1, ChipsPerRow);
            float rowWidth = inRow * cell.x + (inRow - 1) * gap.x;
            return new Vector2((gridWidth - rowWidth) * 0.5f + column * (cell.x + gap.x), row * (cell.y + gap.y));
        }

        /// <summary>
        /// 칸을 줄마다 가운데로 놓는다 (Show에서 한 번 — 매 프레임 아님). 장면 격자의 GridLayoutGroup은 칸 크기 · 간격만 읽고 끈다
        /// (덜 찬 줄을 가운데로 모으지 못한다). 격자 그룹이 없으면 128×170 · 간격 16
        /// </summary>
        void PlaceChips(int count)
        {
            if (_grid == null || _chips == null) return;
            if (!_gridLayoutRead)
            {
                _gridLayoutRead = true;
                _gridLayout = _grid.GetComponent<GridLayoutGroup>();
            }
            var cell = _gridLayout != null ? _gridLayout.cellSize : new Vector2(ChipWidth, ChipHeight);
            var gap = _gridLayout != null ? _gridLayout.spacing : new Vector2(ChipGap, ChipGap);
            if (_gridLayout != null && _gridLayout.enabled) _gridLayout.enabled = false;
            float width = _grid.rect.width;
            var topLeft = new Vector2(0f, 1f);
            int placed = Mathf.Min(count, _chips.ActiveCount);
            for (int i = 0; i < placed; i++)
            {
                var rect = _chips.Items[i].Rect;
                var offset = ChipOffset(i, placed, width, cell, gap);
                rect.anchorMin = topLeft;
                rect.anchorMax = topLeft;
                rect.sizeDelta = cell;
                rect.anchoredPosition = new Vector2(offset.x + cell.x * rect.pivot.x, -(offset.y + cell.y * (1f - rect.pivot.y)));
            }
        }

        protected override void OnOpen()
        {
            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = StartCoroutine(Reveal());
        }

        protected override void OnClose()
        {
            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = null;
            // 받는 도중 억지로 닫힘 (화면 이동 · 모두 닫기): 받은 보상은 이미 들어왔으니 날아가는 모습만 마저 보인다
            if (_claiming && !_flown && _result != null) FlyResult();
            if (_chips == null) return;
            for (int i = 0; i < _chips.ActiveCount; i++) _chips.Items[i].Rect.localScale = Vector3.one;
        }

        /// <summary>어둡게 덮기 · X · 뒤로 (M49): 받는 중이면 무시, 아니면 닫고 남은 선물이 바구니에 있다고 알린다</summary>
        protected override void OnDimOrBack()
        {
            if (_claiming) return;
            bool kept = _shown.Count > 0 && MetaUi.IsActive && Game.IdleChest.CanClaim;
            Close();
            if (kept) MetaUi.Toast(KeptToast);
        }

        void RefreshButtons()
        {
            // 받는 중에는 배치 · 글자를 그대로 두고 누르지만 못 하게 한다: 오늘 무료 · 마지막 수선권으로 두 배를 받으면 CanDouble이 바로 꺼지는데,
            // 그때 다시 배치하면 방금 누른 [두 배로 받기]가 사라지고 [받기]가 가운데로 옮겨진 채 받기 연출이 끝날 때까지 남는다
            if (_claiming)
            {
                _claimButton.interactable = false;
                _doubleButton.interactable = false;
                return;
            }
            var chest = Game.IdleChest;
            bool hasAny = _shown.Count > 0;
            bool canDouble = hasAny && chest.CanDouble;
            ApplyButtonLayout(canDouble);
            _claimButton.interactable = hasAny;
            _doubleButton.interactable = canDouble;
            if (!canDouble) return;
            bool free = chest.IsDoubleFree;
            int tickets = Game.MetaRewards.DoubleTickets;
            // 글은 무료 여부 · 수선권 수가 바뀔 때만 다시 쓴다
            int doubleKey = free ? -1 : tickets;
            if (doubleKey != _shownDoubleKey)
            {
                _shownDoubleKey = doubleKey;
                _doubleLabel.text = "두 배로 받기";
                _doubleSubLabel.text = DoubleSubText(free, tickets);
                if (_doubleBadgeText != null) _doubleBadgeText.text = DoubleBadgeText(free);
            }
            if (_doubleBadge != null && !_doubleBadge.activeSelf) _doubleBadge.SetActive(true);
            bool excludesGem = Game.MetaDb == null || Game.MetaDb.idleChest.doubleExcludesGem;
            _noteText.text = excludesGem ? "두 배는 단추와 재료에만 적용돼요 (별조각 제외)" : "두 배는 모든 선물에 적용돼요";
        }

        /// <summary>
        /// [두 배로 받기] 아래 작은 줄: 무료면 '하루 한 번' ('무료'는 오른쪽 위 빨간 알약 하나만 — Wave 2 r2: 알약 '무료' + '오늘 무료'로 두 번 보였다),
        /// 아니면 '두 배 수선권 1장 (보유 N)'
        /// </summary>
        internal static string DoubleSubText(bool free, int tickets) => free ? "하루 한 번" : $"두 배 수선권 1장 (보유 {tickets})";

        /// <summary>[두 배로 받기] 오른쪽 위 빨간 알약: 무료 '무료' / 수선권 '×2'</summary>
        internal static string DoubleBadgeText(bool free) => free ? "무료" : UIFormat.Times + "2";

        /// <summary>
        /// 버튼 배치 (바뀔 때만): 두 배가 되면 [그냥 받기](Secondary, 왼쪽 320×110, 34 Cream) + [두 배로 받기](금, 오른쪽) + 안내 줄,
        /// 아니면 [받기] 혼자 (금, 가운데 520×120, 40 Ink — 한 화면에 금은 하나) + 두 배 버튼 · 안내 줄 숨김
        /// </summary>
        void ApplyButtonLayout(bool canDouble)
        {
            int layout = canDouble ? 1 : 0;
            if (layout == _buttonLayout) return;
            _buttonLayout = layout;
            _doubleButton.gameObject.SetActive(canDouble);
            if (_noteText != null) _noteText.gameObject.SetActive(canDouble);
            var rect = (RectTransform)_claimButton.transform;
            var anchor = canDouble ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = canDouble ? PairClaimPos : SoloClaimPos;
            rect.sizeDelta = canDouble ? PairClaimSize : SoloClaimSize;
            var sprite = canDouble ? _claimSecondarySprite : _claimGoldSprite;
            if (sprite != null && _claimButton.targetGraphic is Image skin) skin.sprite = sprite;
            if (_claimLabel != null)
            {
                _claimLabel.text = canDouble ? "그냥 받기" : "받기";
                _claimLabel.fontSize = canDouble ? PairClaimFont : SoloClaimFont;
                if (_claimLabel.enableAutoSizing) _claimLabel.fontSizeMax = _claimLabel.fontSize;
                SetRgb(_claimLabel, canDouble ? UITheme.Cream : UITheme.Ink);
            }
        }

        /// <summary>색만 바꾼다 (알파는 ButtonLabelDim이 정한 비활성 흐림 그대로)</summary>
        static void SetRgb(TMP_Text text, Color color)
        {
            color.a = text.color.a;
            text.color = color;
        }

        /// <summary>가진 계보 중 무작위 한 명의 성격 한 줄 (계보마다 7줄)</summary>
        void ApplyLineageLine()
        {
            var db = Game.MetaDb;
            var owned = Game.Characters.GetOwnedLineages();
            LineageDef pick = null;
            string line = null;
            if (db != null && owned.Count > 0)
            {
                int start = UnityEngine.Random.Range(0, owned.Count);
                for (int i = 0; i < owned.Count && line == null; i++)
                {
                    var lineage = owned[(start + i) % owned.Count];
                    var lines = db.GetReturnLines(lineage.id);
                    if (lines == null || lines.lines.Count == 0) continue;
                    pick = lineage;
                    line = lines.lines[UnityEngine.Random.Range(0, lines.lines.Count)];
                }
            }
            _lineRoot.SetActive(line != null);
            if (line == null) return;
            _lineName.text = pick.name;
            _lineName.color = SpeakerColor(pick.mainColor);
            if (_lineDot != null) _lineDot.color = MetaUi.Alpha(pick.mainColor, 1f);
            _lineText.text = line;
            var portrait = Game.Characters.GetPortrait(pick.baseFormId);
            _linePortrait.enabled = portrait != null;
            _linePortrait.sprite = portrait;
            _linePortrait.color = Color.white;
            _linePortrait.preserveAspect = true;
            // 받침 그림에는 계보 색을 옅게만 섞는다
            if (_linePortraitBack != null)
            {
                if (!_portraitBackRead)
                {
                    _portraitBackRead = true;
                    _portraitBackBase = _linePortraitBack.color;
                }
                _linePortraitBack.color = MetaUi.Alpha(Color.Lerp(_portraitBackBase, pick.mainColor, PortraitBackTint), _portraitBackBase.a);
            }
        }

        /// <summary>
        /// 크림 말풍선 위 계보 이름 색 (M31): Lerp(계보 색, Ink, 0.55) — 말풍선(Parchment)과의 대비가 4.5:1보다 낮으면 Ink.
        /// 대비는 WCAG 상대 휘도로 잰다 (sRGB)
        /// </summary>
        internal static Color SpeakerColor(Color mainColor)
        {
            var mixed = Color.Lerp(mainColor, UITheme.Ink, SpeakerInkMix);
            mixed.a = 1f;
            return Contrast(mixed, UITheme.Parchment) >= SpeakerMinContrast ? mixed : UITheme.Ink;
        }

        /// <summary>두 색의 대비 비율 (WCAG 2: (밝은 L + 0.05) / (어두운 L + 0.05), 1~21)</summary>
        internal static float Contrast(Color a, Color b)
        {
            float la = Luminance(a);
            float lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        static float Luminance(Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        static float Linear(float channel) => channel <= 0.04045f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        /// <summary>
        /// 보상 칸이 0.08초 시차로 튀어나오며 0.8초 동안 0에서 세어 올라간다 (숫자를 쓸 때마다 CountTick — 70ms에 하나, 끝에 CountFinish).
        /// 칸 크기 0은 여기(OnOpen — 팝업이 켜진 뒤)에서 맞춘다: 꺼진 팝업에서 처음 만든 칸을 Show에서 0으로 줄여 두면, 켜질 때 칸의 PressScale이
        /// Awake에서 원래 크기를 0으로 읽어 한 번 누른 칸이 0배(안 보임)로 남는다. 루트 알파가 0에서 시작하고 첫 그리기 전이라 깜빡이지 않는다
        /// </summary>
        IEnumerator Reveal()
        {
            EnsurePool();
            int shownCount = _chips.ActiveCount;
            for (int i = 0; i < shownCount; i++) _chips.Items[i].Rect.localScale = Vector3.zero;
            yield return null;
            int count = _chips.ActiveCount;
            float total = Stagger * Mathf.Max(0, count - 1) + CountUpTime;
            float step = 0f;
            for (float t = 0f; t <= total + 0.05f; t += Time.unscaledDeltaTime)
            {
                step -= Time.unscaledDeltaTime;
                bool write = step <= 0f;
                if (write) step = CountUpStep;
                bool ticked = false;
                for (int i = 0; i < count; i++)
                {
                    float local = t - i * Stagger;
                    var chip = _chips.Items[i];
                    chip.Rect.localScale = Vector3.one * (local <= 0f ? 0f : MetaUi.EaseOutBack(local / 0.3f, 2f));
                    if (!write || local <= 0f || local > CountUpTime) continue;
                    chip.SetAmountText(FormatPartial(_shown[i], MetaUi.EaseOutCubic(local / CountUpTime)));
                    ticked = true;
                }
                if (ticked) UISfx.Play(UISfxKey.CountTick);
                yield return null;
            }
            for (int i = 0; i < count; i++)
            {
                _chips.Items[i].Rect.localScale = Vector3.one;
                _chips.Items[i].SetAmountText(MetaUi.ShortAmount(_shown[i]));
            }
            if (count > 0) UISfx.Play(UISfxKey.CountFinish);
            _reveal = null;
        }

        static string FormatPartial(MetaReward reward, float k)
        {
            double value = Math.Floor(reward.amount * k);
            return reward.type == MetaRewardType.Currency
                ? RewardService.FormatAmountCompact(reward.currency, value)
                : UIFormat.TimesSmall + MetaUi.Int(value);
        }

        // ───────────── 받기 ─────────────

        void OnClaim(bool doubled)
        {
            if (!MetaUi.IsActive || _claiming) return;
            if (MetaUi.BlockIfTampered()) return;
            var result = Game.IdleChest.Claim(doubled);
            if (result == null)
            {
                MetaUi.Toast("아직 담긴 선물이 없어요");
                Close();
                return;
            }
            _claiming = true;
            _flown = false;
            _result = result;
            UISfx.Play(result.Doubled ? UISfxKey.RewardDouble : UISfxKey.Claim);
            RefreshButtons();
            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = StartCoroutine(Finish(result));
        }

        /// <summary>두 배면 칸마다 "×2"로 바뀌는 연출 뒤, 보상을 날리고 닫는다</summary>
        IEnumerator Finish(IdleChestClaimResult result)
        {
            for (int i = 0; i < _chips.ActiveCount; i++) _chips.Items[i].Rect.localScale = Vector3.one;
            if (result.Doubled)
            {
                // 받은 목록(두 배 적용)과 같은 종류 칸의 숫자를 바꾸며 튀긴다
                for (int i = 0; i < _chips.ActiveCount; i++)
                {
                    var chip = _chips.Items[i];
                    var granted = FindSameKind(result.Rewards, _shown[i]);
                    if (granted == null) continue;
                    chip.SetAmountText(MetaUi.ShortAmount(granted));
                    if (granted.amount > _shown[i].amount + 1e-6) chip.SetAmountColor(UITheme.Gold);
                    if (_sparkles != null) _sparkles.BurstAt(chip.Rect, 5, UITheme.Lavender, 0.6f);
                    StartCoroutine(Punch(chip.Rect));
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                yield return new WaitForSecondsRealtime(0.35f);
            }
            else if (_sparkles != null)
            {
                _sparkles.BurstAt(_grid, 14, UITheme.Lamp, 1.1f);
            }
            FlyResult();
            yield return new WaitForSecondsRealtime(0.3f);
            _reveal = null;
            Close();
        }

        /// <summary>받은 보상을 상단 칩으로 한 번만 날린다</summary>
        void FlyResult()
        {
            if (_flown || _result == null) return;
            _flown = true;
            MetaUi.FlyRewards(_result.Rewards, _grid);
        }

        static MetaReward FindSameKind(List<MetaReward> list, MetaReward reward)
        {
            if (list == null || reward == null) return null;
            foreach (var item in list)
            {
                if (item != null && item.IsSameKind(reward)) return item;
            }
            return null;
        }

        static IEnumerator Punch(RectTransform rect)
        {
            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                rect.localScale = Vector3.one * (1f + 0.25f * MetaUi.Punch(t / duration));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }
    }
}
