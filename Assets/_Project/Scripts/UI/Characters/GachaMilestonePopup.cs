using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 마일스톤 창 (PRG-04, SPEC4 §6.4 — BuildPopup 820×1000, '소환 마일스톤'). 소환 탭의 마일스톤 줄을 누르면 열린다.
    ///   · 머리: '누적 소환 n회' + [모두 받기] (받을 단계가 있을 때만).
    ///   · 친구 선택권 줄: '친구 선택권 k장' + [고르기 ›] → FriendTicketPopup (선택권이 있을 때만).
    ///   · 단계 줄 (740×110, 빌더가 미리 만든 줄을 다시 쓴다): '{10}회 · 별조각 100' + 상태 [받기] / '완료' / '{k}회 남음'.
    ///     받기는 순서대로 한 단계씩 (GachaMilestones.TryClaimNext) — 어느 [받기]를 눌러도 가장 앞 단계를 받는다.
    ///   · 아래 안내 (Hint 20): '소환 확률과 천장은 그대로예요. 누적 소환은 첫 만남 소환을 세지 않아요.' + 예전 소환 탭 안내(중복 → 기억편).
    /// 받으면 보상이 날아가고(MetaUi.FlyRewards) 단계 오름 소리(UISfxKey.TierUp). 열려 있는 동안 GachaMilestones.Changed에 다시 그린다.
    /// 첫 받지 않은 단계 둘 앞부터 보여 준다 (오래 소환해 받은 되풀이 단계가 많아도 줄 수는 그대로).
    /// </summary>
    public class GachaMilestonePopup : UIPopup
    {
        /// <summary>창 아래 안내 (M80 — 소환 탭에서 옮긴 기억편 안내 포함)</summary>
        public const string FooterHint = "소환 확률과 천장은 그대로예요. 누적 소환은 첫 만남 소환을 세지 않아요.\n<size=90%>중복으로 만난 친구는 <color=#C4B2F2>기억편</color>으로 바뀌어요</size>";

        [SerializeField] TMP_Text _pullsText;
        [SerializeField] Button _claimAllButton;
        [Header("친구 선택권")]
        [SerializeField] GameObject _ticketRow;
        [SerializeField] TMP_Text _ticketText;
        [SerializeField] Button _ticketButton;
        [Header("단계 줄 (같은 순서)")]
        [SerializeField] GameObject[] _rows;
        [SerializeField] TMP_Text[] _rowLabels;
        [SerializeField] TMP_Text[] _rowStates;
        [SerializeField] Button[] _rowClaims;
        [SerializeField] MetaRewardChip[] _rowChips;
        [SerializeField] TMP_Text _footerText;

        readonly List<GachaMilestoneStep> _steps = new();
        bool _subscribed;

        protected override void Awake()
        {
            base.Awake();
            if (_claimAllButton != null) _claimAllButton.onClick.AddListener(ClaimAll);
            if (_ticketButton != null) _ticketButton.onClick.AddListener(OpenTicket);
            if (_rowClaims != null)
            {
                for (int i = 0; i < _rowClaims.Length; i++)
                {
                    if (_rowClaims[i] == null) continue;
                    int index = i;
                    _rowClaims[i].onClick.AddListener(() => ClaimRow(index));
                }
            }
            if (_footerText != null) _footerText.text = FooterHint;
        }

        void OnDestroy() => Subscribe(false);

        public void Show()
        {
            Open();
            Subscribe(true);
            Refresh();
        }

        protected override void OnClose() => Subscribe(false);

        void Subscribe(bool on)
        {
            if (on == _subscribed) return;
            var milestones = MetaUi.IsActive ? Game.GachaMilestones : null;
            if (on && milestones == null) return;
            _subscribed = on;
            if (milestones == null) return;
            if (on) milestones.Changed += Refresh;
            else milestones.Changed -= Refresh;
        }

        /// <summary>단계 줄 글: '{10}회 · 별조각 100'</summary>
        internal static string RowLabel(int pulls, string rewards) =>
            string.IsNullOrEmpty(rewards) ? $"{pulls:N0}회" : $"{pulls:N0}회 · {rewards}";

        /// <summary>남은 횟수 글: '{k}회 남음'</summary>
        internal static string RemainLabel(int remain) => $"{System.Math.Max(1, remain):N0}회 남음";

        /// <summary>보여 줄 첫 단계: 처음 받지 않은 단계 둘 앞 (0 아래로는 가지 않는다)</summary>
        internal static int FirstVisible(IReadOnlyList<GachaMilestoneStep> steps, int lead = 2)
        {
            if (steps == null) return 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && !steps[i].Claimed) return Mathf.Max(0, i - lead);
            }
            return Mathf.Max(0, steps.Count - 1 - lead);
        }

        void Refresh()
        {
            if (!IsOpen) return;
            var milestones = MetaUi.IsActive ? Game.GachaMilestones : null;
            if (milestones != null) milestones.GetSteps(_steps, 2);
            else _steps.Clear();
            int pulls = milestones != null ? milestones.Pulls : 0;
            if (_pullsText != null) _pullsText.text = $"누적 소환 <b>{pulls:N0}</b>회";
            bool any = milestones != null && milestones.HasClaimable;
            if (_claimAllButton != null && _claimAllButton.gameObject.activeSelf != any) _claimAllButton.gameObject.SetActive(any);

            int tickets = milestones != null ? milestones.FriendTickets : 0;
            if (_ticketRow != null)
            {
                bool show = tickets > 0;
                if (_ticketRow.activeSelf != show) _ticketRow.SetActive(show);
                if (show && _ticketText != null) _ticketText.text = $"친구 선택권 <color={UITheme.ColorTag(UITheme.Gold)}>{tickets}</color>장";
            }

            int rowCount = _rows != null ? _rows.Length : 0;
            int first = FirstVisible(_steps);
            for (int i = 0; i < rowCount; i++)
            {
                int index = first + i;
                var step = index < _steps.Count ? _steps[index] : null;
                bool on = step != null;
                if (_rows[i] != null && _rows[i].activeSelf != on) _rows[i].SetActive(on);
                if (!on) continue;
                SetRow(i, step, pulls);
            }
        }

        void SetRow(int row, GachaMilestoneStep step, int pulls)
        {
            var label = At(_rowLabels, row);
            if (label != null) label.text = RowLabel(step.Pulls, MetaUi.DescribeData(step.Rewards));
            var state = At(_rowStates, row);
            var claim = At(_rowClaims, row);
            bool canClaim = step.CanClaim;
            if (claim != null && claim.gameObject.activeSelf != canClaim) claim.gameObject.SetActive(canClaim);
            if (state != null)
            {
                bool showState = !canClaim;
                if (state.gameObject.activeSelf != showState) state.gameObject.SetActive(showState);
                if (showState)
                {
                    state.text = step.Claimed ? "완료" : RemainLabel(step.Pulls - pulls);
                    state.color = step.Claimed ? UITheme.SubText : UITheme.Cream;
                }
            }
            var chip = At(_rowChips, row);
            if (chip != null)
            {
                var reward = step.Rewards != null && step.Rewards.Count > 0 ? step.Rewards[0] : null;
                if (reward != null)
                {
                    chip.Set(reward);
                    chip.SetDimmed(step.Claimed);
                }
                else if (chip.gameObject.activeSelf) chip.gameObject.SetActive(false);
            }
        }

        static T At<T>(T[] items, int index) where T : class => items != null && index >= 0 && index < items.Length ? items[index] : null;

        void ClaimRow(int row)
        {
            var milestones = MetaUi.IsActive ? Game.GachaMilestones : null;
            if (milestones == null) return;
            var rewards = milestones.TryClaimNext();
            if (rewards == null || rewards.Count == 0)
            {
                Refresh();
                return;
            }
            var claim = At(_rowClaims, row);
            OnClaimed(rewards, claim != null ? (RectTransform)claim.transform : null);
        }

        void ClaimAll()
        {
            var milestones = MetaUi.IsActive ? Game.GachaMilestones : null;
            if (milestones == null) return;
            var rewards = milestones.ClaimAll();
            if (rewards == null || rewards.Count == 0)
            {
                Refresh();
                return;
            }
            OnClaimed(rewards, _claimAllButton != null ? (RectTransform)_claimAllButton.transform : null);
        }

        void OnClaimed(List<MetaReward> rewards, RectTransform from)
        {
            UISfx.Play(UISfxKey.TierUp);
            if (from != null) MetaUi.FlyRewards(rewards, from);
            Refresh();
        }

        void OpenTicket()
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.TryGet(out FriendTicketPopup popup)) popup.Show();
        }
    }
}
