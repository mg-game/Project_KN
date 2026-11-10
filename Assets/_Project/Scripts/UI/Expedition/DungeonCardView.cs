using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잔향이 던전 카드 (원정 탭 던전 쪽). 다음 층, 남은 입장권(+ 덜 찼으면 '충전까지 7:38:48', M77), 층 보상, "도전"(결과 팝업) / "소탕"(층 고르기),
    /// 그리고 카드 아래 '층별 보상' 줄(다음 도전 뒤 세 층 — 층 데이터가 없으면 '매일 05:00 충전' 한 줄).
    /// 지킴이 없이 잔향이 덱만으로 싸운다. 글은 값이 바뀔 때만 다시 쓴다 (패널이 1초마다 Refresh를 부른다).
    ///   · 권장 줄 (PRG-01 #2, SPEC4 §7.1): '권장 12,345 · 잔향이 9,876' (Label 24) + 평가 칩 '여유' / '도전' / '어려움' (Micro 18, 평가 색) —
    ///     PowerSystem.GetDungeonRecommended(다음 층)이 0이면(보정 전) 줄을 숨긴다. 평가는 잔향이 덱 전투력(EchoPower)으로 (던전에는 지킴이가 없다).
    ///   · 입장권 모아 두기 (NEW-04 스트레치, SPEC4 §13.9 — DungeonSystem.IsTicketBankOn, 콘텐츠 상한 0 = 꺼짐): 켜지면 '입장권 {n}/{상한}' (상한 9) ·
    ///     덜 찼을 때 알약 '새벽 5시에 +3장' (모자란 만큼까지만, 하루 입장권 이하). 꺼져 있으면 지금처럼 '{n}/{하루}' · '충전까지 7:38:48'.
    /// </summary>
    public class DungeonCardView : MonoBehaviour
    {
        /// <summary>입장권을 다 썼을 때 (M77 — 언제 다시 생기는지까지)</summary>
        public const string NoTicketMessage = "오늘 입장권을 모두 썼어요. 새벽 5시에 다시 충전돼요.";
        const string ClearJingle = "jingle_dungeon_clear";

        [SerializeField] TMP_Text _floorText;
        [SerializeField] TMP_Text _bestText;
        [SerializeField] TMP_Text _ticketText;
        [Tooltip("'충전까지 7:38:48' 알약 (입장권이 덜 찼을 때만)")]
        [SerializeField] GameObject _ticketResetGroup;
        [SerializeField] TMP_Text _ticketResetText;
        [SerializeField] TMP_Text _rewardHeader;
        [SerializeField] RectTransform _rewardContent;
        [SerializeField] RewardSlotView _rewardPrefab;
        [SerializeField] Button _challengeButton;
        [SerializeField] Button _sweepButton;
        [SerializeField] Badge _badge;
        [SerializeField] Image _flash;

        [Header("층별 보상 (카드 아래, 던전 쪽)")]
        [SerializeField] GameObject[] _floorRows = System.Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] _floorTexts = System.Array.Empty<TMP_Text>();
        [SerializeField] RectTransform[] _floorContents = System.Array.Empty<RectTransform>();
        [Tooltip("층 데이터가 없을 때의 '매일 05:00 충전' 한 줄")]
        [SerializeField] GameObject _floorEmpty;

        [Header("권장 줄 (PRG-01 — 다시 빌드한 씬부터)")]
        [SerializeField] TMP_Text _recText;
        [Tooltip("평가 칩 (바탕 = 평가 색)")]
        [SerializeField] Image _ratingChip;
        [SerializeField] TMP_Text _ratingText;

        UIListPool<RewardSlotView> _rewards;
        UIListPool<RewardSlotView>[] _floorPools;
        Coroutine _flashRoutine;
        int _shownFloor = -1;
        int _shownTickets = -1;
        int _shownDaily = -1;
        int _shownBest = -1;
        long _shownResetSeconds = -1;
        long _shownRec = -1;
        long _shownEcho = -1;
        int _shownBankRefill = -1;

        public RectTransform Rect => (RectTransform)transform;

        void Awake() => EnsureInit();

        /// <summary>던전 쪽이 보이게 되면 바로 맞춘다 (숨어 있는 동안 건너뛴 '충전까지' 글 — 바뀐 글만 쓴다)</summary>
        void OnEnable()
        {
            if (Game.IsReady && Game.Dungeon != null) Refresh();
        }

        /// <summary>
        /// 목록 풀 · 버튼을 한 번 잇는다. 카드는 꺼져 있는 던전 쪽(DungeonPage)에 있어 Awake보다 패널의 Refresh가 먼저 올 수 있다 —
        /// 그래서 Refresh도 먼저 이것을 부른다.
        /// </summary>
        void EnsureInit()
        {
            if (_rewards != null) return;
            _rewards = new UIListPool<RewardSlotView>(_rewardPrefab, _rewardContent);
            int rows = Mathf.Min(_floorTexts.Length, _floorContents.Length);
            _floorPools = new UIListPool<RewardSlotView>[rows];
            for (int i = 0; i < rows; i++) _floorPools[i] = new UIListPool<RewardSlotView>(_rewardPrefab, _floorContents[i]);
            _challengeButton.onClick.AddListener(Challenge);
            _sweepButton.onClick.AddListener(OpenSweep);
            if (_flash != null) _flash.gameObject.SetActive(false);
        }

        /// <summary>던전 상태를 다시 읽는다 (이벤트 · 패널의 1초 틱). 바뀐 글만 다시 쓴다</summary>
        public void Refresh()
        {
            EnsureInit();
            var dungeon = Game.Dungeon;
            int next = dungeon.NextFloor;
            int tickets = dungeon.TicketsLeft;
            bool bank = dungeon.IsTicketBankOn;
            int daily = TicketMax(dungeon);
            int best = dungeon.HighestFloor;

            if (best != _shownBest)
            {
                _shownBest = best;
                _bestText.text = best > 0 ? $"최고 기록 {best}층" : "아직 돌파한 층이 없어요";
            }
            if (tickets != _shownTickets || daily != _shownDaily)
            {
                _shownTickets = tickets;
                _shownDaily = daily;
                _ticketText.text = TicketLabel(tickets, daily);
                _badge.SetCount(tickets > 0 ? tickets : 0);
            }
            RefreshReset(tickets, daily, bank);

            if (_shownFloor != next) FillRewards(next);
            RefreshRecommend(next);
        }

        /// <summary>권장 줄 '권장 12,345 · 잔향이 9,876' (테스트용 순수 함수)</summary>
        internal static string RecLine(long recommended, long echo) =>
            $"권장 {PowerSystem.Format(recommended)} · 잔향이 {PowerSystem.Format(echo)}";

        /// <summary>권장 줄 · 평가 칩 (권장이 0이면 숨김). 권장 · 잔향이 전투력이 바뀔 때만 다시 쓴다</summary>
        void RefreshRecommend(int floor)
        {
            if (_recText == null) return;
            var power = Game.Power;
            long rec = power != null ? power.GetDungeonRecommended(floor) : 0L;
            long echo = power != null && rec > 0 ? power.EchoPower : 0L;
            if (rec == _shownRec && echo == _shownEcho) return;
            _shownRec = rec;
            _shownEcho = echo;
            bool show = rec > 0;
            if (_recText.gameObject.activeSelf != show) _recText.gameObject.SetActive(show);
            if (_ratingChip != null && _ratingChip.gameObject.activeSelf != show) _ratingChip.gameObject.SetActive(show);
            if (!show) return;
            _recText.text = RecLine(rec, echo);
            var rating = power.GetRating(echo, rec);
            if (_ratingChip != null) _ratingChip.color = RecommendedPowerView.RatingColor(rating);
            if (_ratingText != null) _ratingText.text = PowerSystem.RatingName(rating);
        }

        /// <summary>'입장권 1/2' (남은 수 금빛, 0이면 Negative) — 원정 탭 머리 칩 · 소탕 창도 같은 글을 쓴다. daily = TicketMax (모아 두기면 상한)</summary>
        public static string TicketLabel(int tickets, int daily) =>
            $"입장권 <color={UITheme.ColorTag(tickets > 0 ? UITheme.Gold : UITheme.Negative)}>{tickets}</color>/{daily}";

        /// <summary>'입장권 {n}/{이 값}'의 뒤 수: 모아 두기(NEW-04)가 켜졌으면 상한 (콘텐츠 9), 아니면 하루 입장권 (테스트용 순수 함수)</summary>
        internal static int TicketMax(bool bankOn, int bankCap, int daily) => bankOn ? bankCap : daily;

        /// <summary>지금 던전의 '입장권 {n}/{이 값}' 뒤 수 (모아 두기 상한 또는 하루 입장권)</summary>
        public static int TicketMax(DungeonSystem dungeon) =>
            dungeon == null ? 0 : TicketMax(dungeon.IsTicketBankOn, dungeon.TicketBankCapValue, dungeon.DailyTickets);

        /// <summary>모아 두기: 새벽 5시에 더해지는 입장권 = min(하루 입장권, 상한 − 남은 수), 0 이상 (테스트용 순수 함수)</summary>
        internal static int BankRefill(int tickets, int bankCap, int daily) => Mathf.Max(0, Mathf.Min(daily, bankCap - tickets));

        /// <summary>모아 두기 알약 '새벽 5시에 +3장' (SPEC4 §13.9)</summary>
        internal static string BankRefillLine(int refill) => $"새벽 5시에 +{refill}장";

        /// <summary>
        /// '충전까지 7:38:48' — 입장권이 덜 찼을 때만, 초가 바뀔 때만 다시 쓴다.
        /// 카드가 안 보이면(원정 쪽 — 패널은 어느 쪽에서든 1초마다 Refresh를 부른다) 글을 만들지 않고, 다시 보일 때(OnEnable) 새로 쓴다.
        /// 모아 두기(bank)면 시계 대신 '새벽 5시에 +{n}장' (상한보다 모자랄 때만, 더해질 수가 바뀔 때만 다시 쓴다). daily = TicketMax
        /// </summary>
        void RefreshReset(int tickets, int daily, bool bank)
        {
            if (_ticketResetGroup == null || _ticketResetText == null) return;
            int refill = bank ? BankRefill(tickets, daily, Game.Dungeon.DailyTickets) : 0;
            bool show = tickets < daily && (!bank || refill > 0);
            if (_ticketResetGroup.activeSelf != show) _ticketResetGroup.SetActive(show);
            if (!show || !isActiveAndEnabled)
            {
                _shownResetSeconds = -1;
                _shownBankRefill = -1;
                return;
            }
            if (bank)
            {
                _shownResetSeconds = -1;
                if (refill == _shownBankRefill) return;
                _shownBankRefill = refill;
                _ticketResetText.text = BankRefillLine(refill);
                return;
            }
            _shownBankRefill = -1;
            var left = Game.Dungeon.TimeUntilTicketReset;
            long seconds = (long)left.TotalSeconds;
            if (seconds == _shownResetSeconds) return;
            _shownResetSeconds = seconds;
            _ticketResetText.text = "충전까지 " + UIFormat.ClockMono(left);
        }

        public void Highlight()
        {
            if (_flash == null || !isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _flashRoutine, FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            yield return MenuFx.Flash(_flash, new Color(UITheme.EchoGlow.r, UITheme.EchoGlow.g, UITheme.EchoGlow.b, 0.5f), 3, 0.45f);
            _flashRoutine = null;
        }

        void OnDisable()
        {
            if (_flash != null) _flash.gameObject.SetActive(false);
            _flashRoutine = null;
        }

        void FillRewards(int floor)
        {
            _shownFloor = floor;
            _floorText.text = $"다음 도전 <size=125%>{floor}</size>층";
            _rewardHeader.text = $"{floor}층 보상";
            FillFloorRewards(_rewards, floor);
            FillFloorList(floor);
        }

        /// <summary>카드 아래 '층별 보상': 다음 도전 뒤 세 층. 보상이 하나도 없으면 줄을 숨기고 '매일 05:00 충전' 한 줄</summary>
        void FillFloorList(int next)
        {
            if (_floorPools == null) return;
            bool any = false;
            for (int i = 0; i < _floorPools.Length; i++)
            {
                int floor = next + 1 + i;
                FillFloorRewards(_floorPools[i], floor);
                bool has = _floorPools[i].Count > 0;
                any |= has;
                if (i < _floorRows.Length && _floorRows[i] != null) _floorRows[i].SetActive(has);
                if (has) _floorTexts[i].text = $"{floor}층";
            }
            if (_floorEmpty != null) _floorEmpty.SetActive(!any);
        }

        /// <summary>층 확정 보상 + 확률 보상(달빛 조각, 확률 표시)</summary>
        public static void FillFloorRewards(UIListPool<RewardSlotView> pool, int floor)
        {
            var dungeon = Game.Dungeon;
            var rewards = RewardService.Merge(dungeon.GetRewards(floor));
            float bonusChance = dungeon.GetMoonlightChance(floor);
            var bonusItem = bonusChance > 0f ? Game.Items.Get(DungeonSystem.MoonlightItemId) : null;

            pool.SetCount(rewards.Count + (bonusItem != null ? 1 : 0));
            for (int i = 0; i < rewards.Count; i++) pool[i].SetWithInfo(rewards[i], true);
            if (bonusItem == null) return;
            var bonus = pool[rewards.Count];
            bonus.SetItem(bonusItem, $"{Mathf.RoundToInt(bonusChance * 100f)}%");
            bonus.SetItemInfoClick();
        }

        void Challenge()
        {
            var dungeon = Game.Dungeon;
            if (dungeon.TicketsLeft <= 0)
            {
                UIManager.Instance.ShowToast(NoTicketMessage);
                return;
            }
            if (!dungeon.HasDeck)
            {
                UIManager.Instance.ShowToast("잔향이 덱이 비어 있어요. 편성에서 잔향이를 배치해 주세요.");
                return;
            }
            var result = dungeon.Challenge();
            if (result == null)
            {
                UIManager.Instance.ShowToast("지금은 도전할 수 없어요. 입장권과 잔향이 덱을 확인해 주세요.");
                return;
            }
            ShowResult(result, false);
        }

        void OpenSweep()
        {
            if (Game.Dungeon.HighestFloor <= 0)
            {
                UIManager.Instance.ShowToast("먼저 1층을 돌파해 주세요.");
                return;
            }
            if (Game.Dungeon.TicketsLeft <= 0)
            {
                UIManager.Instance.ShowToast(NoTicketMessage);
                return;
            }
            UIManager.Instance.Get<DungeonSweepPopup>().Open();
        }

        /// <summary>여러 장 소탕 결과 (IDLE-05): '{층}층 {n}번 소탕 완료' / '잔향이들이 익숙한 길을 {n}번 다녀왔어요.' (한 장이면 ShowResult와 같다)</summary>
        public static void ShowSweepResult(DungeonResult result, int count)
        {
            if (count <= 1)
            {
                ShowResult(result, true);
                return;
            }
            UISfx.Play(UISfxKey.DungeonSweep);
            UIManager.Instance.Get<RewardPopup>().Show($"{result.Floor}층 {count}번 소탕 완료", $"잔향이들이 익숙한 길을 {count}번 다녀왔어요.", result.Rewards);
        }

        /// <summary>
        /// 도전/소탕 결과를 보상 팝업으로 보여준다 (확인을 누르면 보상이 상단 바로 날아간다 — 실패해도 얻은 보상이 있으면 날린다, M20).
        /// 소리: 소탕 = DungeonSweep, 돌파 = 던전 돌파 징글 (AUDIO-SPEC §8.1).
        /// </summary>
        public static void ShowResult(DungeonResult result, bool sweep)
        {
            var popup = UIManager.Instance.Get<RewardPopup>();
            if (sweep)
            {
                UISfx.Play(UISfxKey.DungeonSweep);
                popup.Show($"{result.Floor}층 소탕 완료", "잔향이들이 익숙한 길을 단숨에 지나왔어요.", result.Rewards);
            }
            else if (result.Cleared)
            {
                AudioService.PlayJingle(ClearJingle);
                popup.Show($"{result.Floor}층 돌파!", $"잔향이들이 {Mathf.CeilToInt(result.SurvivedSeconds)}초 만에 길을 밝혔어요.", result.Rewards);
            }
            else
            {
                popup.Show($"{result.Floor}층 도전 실패",
                    $"{Mathf.FloorToInt(result.SurvivedSeconds)}초 동안 버텼어요.\n잔향이 덱을 성장시켜 다시 도전해 보세요.", result.Rewards, null, false,
                    flyOnFail: true);
            }
        }
    }
}
