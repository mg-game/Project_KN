using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 튜토리얼 졸업 카드 (FTUE 16단계): "이제 수선점 주인이에요!" + 함께한 친구들 + 졸업 보상(이미 지급됨, 보여 주기만) + 축하 연출.
    ///   · PopupQueue의 FtueGraduation 요청으로 뜬다 (스테이지 사이, 다른 팝업이 없을 때). FtueDirector가 꺼내 Present를 부르고,
    ///     메타 발표자(M3)가 먼저 꺼냈다면 GraduationCardPopup.Present()를 부르면 된다.
    ///   · [좋아요!]를 누르면 보상 아이콘이 상단 칩으로 날아가고, 닫힌 뒤 출석부를 처음 연다 (F-05: 졸업 카드 → 출석부 첫 도장).
    ///   · 보상 목록은 졸업 action 단계(MetaDatabase.ftueSteps)의 rewards를 풀어서 보여 준다.
    ///   · 열릴 때 졸업 징글(UISfxKey.Graduation — 피치카토 · 글로켄슈필 마침). 친구들이 차례로 콩콩 뛴다 (움직임 줄이기면 제자리).
    /// </summary>
    public class GraduationCardPopup : UIPopup
    {
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _bodyText;
        [SerializeField] GameObject[] _rewardSlots;
        [SerializeField] Image[] _rewardIcons;
        [SerializeField] TMP_Text[] _rewardTexts;
        [SerializeField] Image[] _friends;
        [SerializeField] Button _confirmButton;
        [SerializeField] CelebrationFx _celebration;

        [Header("아이콘")]
        [SerializeField] Sprite _goldIcon;
        [SerializeField] Sprite _gemIcon;
        [SerializeField] Sprite _eventIcon;
        [Tooltip("실타래 (임시 그림, 산호 틴트)")]
        [SerializeField] Sprite _lineageXpIcon;
        [SerializeField] Sprite _ticketIcon;
        [SerializeField] Sprite _shardsIcon;

        readonly List<MetaReward> _rewards = new();
        readonly List<LineageDef> _owned = new();
        Vector2[] _friendBase;
        bool _flown;
        float _time;

        /// <summary>졸업 카드를 띄운다 (등록돼 있지 않으면 false)</summary>
        public static bool Present()
        {
            var ui = UIManager.Instance;
            if (ui == null) return false;
            var popup = ui.Get<GraduationCardPopup>();
            if (popup == null) return false;
            popup.Open();
            return true;
        }

        protected override void Awake()
        {
            base.Awake();
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
            Closed += OnClosed;
            _friendBase = new Vector2[_friends != null ? _friends.Length : 0];
            for (int i = 0; i < _friendBase.Length; i++) _friendBase[i] = _friends[i].rectTransform.anchoredPosition;
        }

        protected override void OnOpen()
        {
            _flown = false;
            _time = 0f;
            FillRewards();
            FillFriends();
            if (_celebration != null) _celebration.Play();
            UISfx.Play(UISfxKey.Graduation);
        }

        void FillRewards()
        {
            _rewards.Clear();
            if (Game.IsReady && Game.IsMetaActive && Game.MetaDb != null)
            {
                foreach (var step in Game.MetaDb.ftueSteps)
                {
                    if (step == null || step.action != FtueAction.Graduation || step.rewards == null) continue;
                    Game.MetaRewards.ResolveAll(step.rewards, _rewards);
                    break;
                }
            }

            for (int i = 0; i < _rewardSlots.Length; i++)
            {
                bool show = i < _rewards.Count;
                _rewardSlots[i].SetActive(show);
                if (!show) continue;
                var reward = _rewards[i];
                var icon = IconOf(reward);
                _rewardIcons[i].sprite = icon;
                _rewardIcons[i].color = MetaIcons.RewardTint(reward);
                _rewardIcons[i].enabled = icon != null;
                _rewardTexts[i].text = Game.MetaRewards.Describe(reward);
            }
        }

        Sprite IconOf(MetaReward reward)
        {
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    return reward.currency switch
                    {
                        CurrencyType.Gold => _goldIcon,
                        CurrencyType.Gem => _gemIcon,
                        CurrencyType.LineageXp => _lineageXpIcon,
                        _ => _eventIcon,
                    };
                case MetaRewardType.Item:
                    return Game.Items?.Get(reward.itemId)?.icon;
                case MetaRewardType.GoldMinutes:
                    return _goldIcon;
                case MetaRewardType.QuickIdleTicket:
                case MetaRewardType.DoubleTicket:
                    return _ticketIcon;
                case MetaRewardType.SelectShards:
                    return _shardsIcon;
                case MetaRewardType.Lineage:
                {
                    var db = Game.Content?.Characters;
                    var lineage = db?.GetLineage(reward.itemId);
                    return lineage != null ? db.GetForm(lineage.baseFormId)?.portrait : null;
                }
                default:
                    return null;
            }
        }

        void FillFriends()
        {
            if (_friends == null) return;
            _owned.Clear();
            if (Game.IsReady)
            {
                var owned = Game.Characters.GetOwnedLineages();
                if (owned != null) _owned.AddRange(owned);
                // 높은 등급 친구부터 (자리가 모자라면 낮은 등급이 빠진다)
                _owned.Sort((a, b) => b.grade.CompareTo(a.grade));
            }
            var db = Game.IsReady ? Game.Content?.Characters : null;
            for (int i = 0; i < _friends.Length; i++)
            {
                bool show = i < _owned.Count;
                _friends[i].gameObject.SetActive(show);
                if (!show) continue;
                var portrait = db?.GetForm(_owned[i].baseFormId)?.portrait;
                // 그림이 없으면 그 자리를 비워 둔다 (색 동그라미 자리표시는 쓰지 않는다)
                _friends[i].sprite = portrait;
                _friends[i].color = Color.white;
                _friends[i].preserveAspect = true;
                _friends[i].enabled = portrait != null;
            }
        }

        void Confirm()
        {
            FlyRewards();
            Close();
        }

        void FlyRewards()
        {
            if (_flown) return;
            _flown = true;
            if (_rewards.Count > 0) RewardFlyer.Fly(_rewards, _confirmButton != null ? (RectTransform)_confirmButton.transform : _window);
        }

        void OnClosed()
        {
            FlyRewards();
            // 졸업 다음은 출석부 첫 도장 (F-05)
            if (Game.IsReady && Game.IsMetaActive && Game.Attendance != null && Game.Attendance.CanCheckIn) UINav.OpenAttendance();
        }

        void Update()
        {
            if (!IsOpen || _friends == null) return;
            _time += Time.unscaledDeltaTime;
            bool reduced = UIMotion.Reduced;
            for (int i = 0; i < _friends.Length; i++)
            {
                if (!_friends[i].gameObject.activeSelf) continue;
                // 친구들이 차례로 콩콩 뛴다 (움직임 줄이기면 제자리)
                float phase = _time * 3.2f - i * 0.55f;
                float hop = reduced ? 0f : Mathf.Max(0f, Mathf.Sin(phase));
                _friends[i].rectTransform.anchoredPosition = _friendBase[i] + new Vector2(0f, 18f * hop * hop);
            }
        }
    }
}
