using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잔향이 던전 소탕: 돌파한 층 중 하나를 골라 입장권으로 즉시 보상을 받는다.
    ///   · 여러 장 소탕 (IDLE-05, SPEC4 §5.2 — DungeonBatchSweep, 던전 5층): 층 줄 아래 '[-] ×{n}장 [+]' (n = 1..남은 입장권, 처음은 모두),
    ///     버튼 '{n}장 소탕' → DungeonSystem.SweepMany(층, n) → 보상 팝업 '{층}층 {n}번 소탕 완료'. 잠겼으면 줄을 숨기고 아래 칸을 그만큼 올려
    ///     예전 모양 ('소탕', 1장). SweepMany가 거절하면(null — 던전 · 여러 장 소탕 잠김, 돌파하지 않은 층, 입장권 없음) 토스트만 —
    ///     한 장씩 나눠 소탕하는 우회는 두지 않는다 (SPEC4 §5.1: UI 길이 새어도 시스템이 거절한다).
    ///   · 입장권 줄의 뒤 수는 DungeonCardView.TicketMax (입장권 모아 두기 NEW-04가 켜지면 상한).
    /// </summary>
    public class DungeonSweepPopup : UIPopup
    {
        /// <summary>콘텐츠에 열림 조건이 아직 없을 때 쓰는 여러 장 소탕 층 (SPEC4 §5.2: DungeonFloor ≥ 5)</summary>
        internal const int BatchFallbackFloor = 5;

        [SerializeField] TMP_Text _floorText;
        [SerializeField] TMP_Text _ticketText;
        [SerializeField] Button _minButton;
        [SerializeField] Button _prevButton;
        [SerializeField] Button _nextButton;
        [SerializeField] Button _maxButton;
        [SerializeField] Button _sweepButton;
        [SerializeField] Button _cancelButton;
        [SerializeField] TMP_Text _rewardHeader;
        [SerializeField] RectTransform _rewardContent;
        [SerializeField] RewardSlotView _rewardPrefab;

        [Header("여러 장 소탕 (IDLE-05 — 다시 빌드한 씬부터)")]
        [Tooltip("'[-] ×{n}장 [+]' 줄 (잠기면 숨김)")]
        [SerializeField] GameObject _countRow;
        [SerializeField] Button _countMinus;
        [SerializeField] Button _countPlus;
        [SerializeField] TMP_Text _countText;
        [Tooltip("소탕 버튼 글 ('{n}장 소탕' / '소탕')")]
        [SerializeField] TMP_Text _sweepLabel;
        [Tooltip("줄 아래 칸 (구분선 · 보상 · 입장권) — 줄을 숨기면 _countShift만큼 올린다")]
        [SerializeField] RectTransform _details;
        [SerializeField] float _countShift = 100f;

        UIListPool<RewardSlotView> _rewards;
        int _floor;
        int _count = 1;
        bool _batch;

        protected override void Awake()
        {
            base.Awake();
            _rewards = new UIListPool<RewardSlotView>(_rewardPrefab, _rewardContent);
            _minButton.onClick.AddListener(() => SetFloor(1));
            _prevButton.onClick.AddListener(() => SetFloor(_floor - 1));
            _nextButton.onClick.AddListener(() => SetFloor(_floor + 1));
            _maxButton.onClick.AddListener(() => SetFloor(Game.Dungeon.HighestFloor));
            _sweepButton.onClick.AddListener(Sweep);
            _cancelButton.onClick.AddListener(Close);
            if (_countMinus != null) _countMinus.onClick.AddListener(() => SetCount(_count - 1));
            if (_countPlus != null) _countPlus.onClick.AddListener(() => SetCount(_count + 1));
        }

        protected override void OnOpen()
        {
            // 여러 장은 장 수 줄이 있는 (다시 빌드한) 씬에서만 — 예전 씬은 늘 한 장 (보이지 않는 장 수로 쓰지 않게)
            _batch = _countRow != null && BatchUnlocked();
            if (_countRow != null) _countRow.SetActive(_batch);
            if (_details != null && _countRow != null) _details.anchoredPosition = new Vector2(_details.anchoredPosition.x, _batch ? 0f : _countShift);
            _count = Mathf.Max(1, Game.Dungeon.TicketsLeft);
            SetFloor(Game.Dungeon.HighestFloor);
        }

        /// <summary>여러 장 소탕이 열렸는지: 기능이 열렸고, 콘텐츠에 조건이 없으면 5층 이상 (메타가 꺼져 있으면 한 장씩 — 예전과 같다)</summary>
        static bool BatchUnlocked()
        {
            var features = Game.IsMetaActive ? Game.Features : null;
            if (features == null || !features.IsUnlocked(FeatureId.DungeonBatchSweep)) return false;
            return features.GetDef(FeatureId.DungeonBatchSweep) != null || Game.Dungeon.HighestFloor >= BatchFallbackFloor;
        }

        /// <summary>소탕할 장 수 (테스트용 순수 함수): 잠겼거나 입장권이 없으면 1, 아니면 1..남은 입장권</summary>
        internal static int ClampCount(int count, int ticketsLeft, bool batch) => !batch || ticketsLeft <= 1 ? 1 : Mathf.Clamp(count, 1, ticketsLeft);

        /// <summary>소탕 버튼 글 (테스트용): 여러 장이면 '{n}장 소탕', 아니면 '소탕'</summary>
        internal static string SweepLabel(int count, bool batch) => batch ? $"{Mathf.Max(1, count)}장 소탕" : "소탕";

        void SetCount(int count)
        {
            _count = ClampCount(count, Game.Dungeon.TicketsLeft, _batch);
            RefreshCount();
        }

        void RefreshCount()
        {
            int tickets = Game.Dungeon.TicketsLeft;
            _count = ClampCount(_count, tickets, _batch);
            if (_countText != null) _countText.text = $"{UIFormat.Times}{_count}장";
            if (_countMinus != null) _countMinus.interactable = _count > 1;
            if (_countPlus != null) _countPlus.interactable = _count < tickets;
            if (_sweepLabel != null) _sweepLabel.text = SweepLabel(_count, _batch);
            _ticketText.text = $"{DungeonCardView.TicketLabel(tickets, DungeonCardView.TicketMax(Game.Dungeon))} · {_count}장 사용";
        }

        void SetFloor(int floor)
        {
            int highest = Mathf.Max(1, Game.Dungeon.HighestFloor);
            _floor = Mathf.Clamp(floor, 1, highest);

            _floorText.text = $"{_floor}층";
            _minButton.interactable = _prevButton.interactable = _floor > 1;
            _nextButton.interactable = _maxButton.interactable = _floor < highest;

            int tickets = Game.Dungeon.TicketsLeft;
            _sweepButton.interactable = tickets > 0 && Game.Dungeon.HighestFloor > 0;
            RefreshCount();

            _rewardHeader.text = $"{_floor}층 소탕 보상";
            DungeonCardView.FillFloorRewards(_rewards, _floor);
        }

        void Sweep()
        {
            var dungeon = Game.Dungeon;
            int count = ClampCount(_count, dungeon.TicketsLeft, _batch);
            DungeonResult result = count > 1 ? dungeon.SweepMany(_floor, count) : dungeon.Sweep(_floor);
            // 시스템이 거절하면 (null) 토스트만 — 한 장씩 나눠 소탕하지 않는다
            if (result == null)
            {
                UIManager.Instance.ShowToast("지금은 소탕할 수 없어요. 입장권을 확인해 주세요.");
                return;
            }
            Close();
            DungeonCardView.ShowSweepResult(result, count);
        }
    }
}
