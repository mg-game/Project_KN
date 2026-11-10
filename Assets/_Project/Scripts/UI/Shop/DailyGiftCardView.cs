using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 오늘의 작은 선물 카드 (NEW-02, SPEC4 §13.7 — 860x120, 상점 탭 '수선점 상점' 쪽 맨 위, 상품 목록은 132 내려간다. 보이기 · 목록 자리는 ShopPanel이 정한다).
    /// 상자 88 (UI/journal_chest_closed — 받은 뒤 journal_chest_open) + '오늘의 작은 선물' (Title 28 Cream) + 한 줄 + 보상 칸 (MetaRewardChip 64) + [받기] Gold 180x80.
    ///   · 받기 전: 한 줄 '하루에 한 번 무료로 받아요', [받기] → Game.DailyGift.TryClaim → Claim 소리 + 보상 날아가기 (MetaUi.FlyRewards). 되돌림 중이면 안내 토스트.
    ///   · 받은 뒤: 상자 열림, [받기] 숨김, 보상 칸 흐리게, 한 줄 '내일 새벽 5시에 새 선물이 와요' (자정 뒤면 '새벽 5시에 …') —
    ///     1시간이 안 남으면 '새 선물까지 12:34' (UIFormat.ClockMono, 1초마다 · 초가 바뀔 때만), 마지막 3 · 2 · 1초는 Countdown 소리.
    ///   · 빨간 점 tab.shop.free는 DailyGiftSystem이 단다 (상점 탭 점 — 이 카드에는 따로 달지 않는다).
    /// Game.DailyGift가 없거나 메타가 꺼져 있으면 아무것도 하지 않는다. 시스템 호출은 예외를 기록만 한다 (메타 안전장치를 건드리지 않게).
    /// Update는 받은 뒤에만 1초마다 일하고, 1시간이 넘게 남았으면 미리 만든 문자열만 쓴다.
    /// </summary>
    public class DailyGiftCardView : MonoBehaviour
    {
        /// <summary>카드 높이 · 카드가 보일 때 상품 목록이 내려가는 거리 (카드 120 + 틈 12)</summary>
        public const float CardHeight = 120f;
        public const float ListShift = 132f;

        public const string TitleText = "오늘의 작은 선물";
        public const string ReadyLine = "하루에 한 번 무료로 받아요";
        internal const string NextLineTomorrow = "내일 새벽 5시에 새 선물이 와요";
        internal const string NextLineToday = "새벽 5시에 새 선물이 와요";
        internal const string CountdownPrefix = "새 선물까지 ";
        /// <summary>이만큼 안 남으면 시계로 보인다 (초)</summary>
        internal const double CountdownSeconds = 3600d;
        /// <summary>마지막 몇 초에 Countdown 소리 (3 · 2 · 1)</summary>
        internal const int CountdownTicks = 3;

        [SerializeField] Image _chest;
        [SerializeField] Sprite _chestClosed;
        [SerializeField] Sprite _chestOpen;
        [Tooltip("제목 아래 한 줄 (받기 전 안내 / 받은 뒤 다음 선물 · 시계)")]
        [SerializeField] TMP_Text _lineText;
        [SerializeField] MetaRewardChip _rewardChip;
        [SerializeField] Button _claimButton;

        bool _claimedShown;
        float _tick;
        long _shownSeconds = -1;

        /// <summary>카드를 보일지: 메타 켜짐 · 상점 열림(1-8) · 오늘 선물 내용이 있음 (콘텐츠가 없으면 숨긴다)</summary>
        public static bool ShouldShow
        {
            get
            {
                if (!MetaUi.IsActive || Game.DailyGift == null) return false;
                try
                {
                    var today = Game.DailyGift.Today;
                    return Game.DailyGift.IsUnlocked && today != null && today.Count > 0;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DailyGift] 선물 상태를 읽지 못했어요: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 받은 뒤 한 줄 (테스트용 순수 함수): 1시간이 안 남으면 '새 선물까지 12:34', 5시간 안이면(자정이 지난 새벽) '새벽 5시에 새 선물이 와요',
        /// 아니면 '내일 새벽 5시에 새 선물이 와요'. 1시간 넘게 남으면 미리 만든 문자열이라 할당이 없다
        /// </summary>
        internal static string NextLine(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            if (left.TotalSeconds < CountdownSeconds) return CountdownPrefix + UIFormat.ClockMono(left);
            return left.TotalHours <= DailyClock.ResetHour ? NextLineToday : NextLineTomorrow;
        }

        /// <summary>마지막 3 · 2 · 1초인지 (Countdown 소리, 테스트용 순수 함수)</summary>
        internal static bool IsCountdownTick(long secondsLeft) => secondsLeft >= 1 && secondsLeft <= CountdownTicks;

        void Awake()
        {
            if (_claimButton != null) _claimButton.onClick.AddListener(OnClaim);
        }

        void OnEnable() => Refresh();

        /// <summary>상태를 다시 그린다 (ShopPanel이 보일 때 · DailyGift.Changed에 부른다)</summary>
        public void Refresh()
        {
            bool claimed = false;
            MetaReward reward = null;
            if (MetaUi.IsActive && Game.DailyGift != null)
            {
                try
                {
                    var gift = Game.DailyGift;
                    claimed = gift.ClaimedToday;
                    var today = gift.Today;
                    reward = today != null && today.Count > 0 ? today[0] : null;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DailyGift] 선물 상태를 읽지 못했어요: {e.Message}");
                }
            }

            if (_chest != null)
            {
                var sprite = claimed && _chestOpen != null ? _chestOpen : _chestClosed;
                if (sprite != null && _chest.sprite != sprite) _chest.sprite = sprite;
            }
            if (_rewardChip != null)
            {
                _rewardChip.Set(reward);
                if (reward != null) _rewardChip.SetDimmed(claimed);
            }
            if (_claimButton != null && _claimButton.gameObject.activeSelf == claimed) _claimButton.gameObject.SetActive(!claimed);

            _claimedShown = claimed;
            _shownSeconds = -1;
            _tick = 0f;
            if (_lineText == null) return;
            if (claimed) WriteNextLine();
            else if (_lineText.text != ReadyLine) _lineText.text = ReadyLine;
        }

        /// <summary>받은 뒤에만: 1초마다 남은 시간 (초가 바뀔 때만 글을 쓴다), 마지막 3초 소리</summary>
        void Update()
        {
            if (!_claimedShown) return;
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0f) return;
            _tick = 1f;
            WriteNextLine();
        }

        void WriteNextLine()
        {
            var left = TimeUntilNext();
            long seconds = (long)left.TotalSeconds;
            if (seconds == _shownSeconds) return;
            bool first = _shownSeconds < 0;
            _shownSeconds = seconds;
            if (!first && IsCountdownTick(seconds)) UISfx.Play(UISfxKey.Countdown);
            if (_lineText == null) return;
            // 0이 되면 '새 선물까지 00:00'에 머문다 — 새벽 5시 날 넘김(MetaSession → DailyGift.OnDayChanged → Changed)에 ShopPanel이 다시 그린다
            string line = NextLine(left);
            if (_lineText.text != line) _lineText.text = line;
        }

        static TimeSpan TimeUntilNext()
        {
            if (Game.DailyGift == null) return TimeSpan.Zero;
            try
            {
                return Game.DailyGift.TimeUntilNext;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DailyGift] 남은 시간을 읽지 못했어요: {e.Message}");
                return TimeSpan.Zero;
            }
        }

        /// <summary>[받기]: 되돌림 중이면 안내, 받으면 Claim 소리 + 보상 날아가기, 못 받으면 Deny 소리</summary>
        void OnClaim()
        {
            if (!MetaUi.IsActive || Game.DailyGift == null) return;
            if (MetaUi.BlockIfTampered()) return;
            List<MetaReward> granted = null;
            try
            {
                granted = Game.DailyGift.TryClaim();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DailyGift] 선물을 받지 못했어요: {e.Message}");
            }
            if (granted == null)
            {
                UISfx.Play(UISfxKey.Deny);
                Refresh();
                return;
            }
            UISfx.Play(UISfxKey.Claim);
            MetaUi.FlyRewards(granted, _rewardChip != null ? _rewardChip.IconRect : (RectTransform)transform);
            Refresh();
        }
    }
}
