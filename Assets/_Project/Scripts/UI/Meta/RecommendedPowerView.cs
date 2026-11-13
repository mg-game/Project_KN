using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스테이지 패널 옆 전투력 비교 알약 (PRG-01 · M12, UX-SPEC §2.1.4). 루트 300×80은 투명한 누르는 자리 (시트가 열리면 사라진다 — SheetFade 0),
    /// 그 안 알약 300×52 (Panel@0.88 + 평가 색 2px 테두리 @0.6):
    ///   · 평가 점 14 · 전투력 그림(Icons/guardian) 28 · 글 한 줄 '<b>{내 전투력}</b> / 권장 {권장}' (내 전투력은 Cream 굵게, 나머지는 평가 색, 맞춤 16 — 6자리 두 수도 칸에 든다) ·
    ///     오른쪽 평가 낱말 칩 '여유' / '도전' / '어려움' (20 굵게, 평가 색 — 색만으로 구분하지 않게 꼭 붙인다).
    ///   · 평가 = PowerSystem.GetRating (여유 Positive / 도전 Gold / 어려움 Negative). 전투력 · 권장 · 평가가 바뀔 때만 다시 쓴다.
    /// 누르면: 평가 '어려움'이면 강해지기 시트 (StrengthenPopup, M13) → 강화 탭 (시트가 없을 때) / 튜토리얼 중이면 토스트만 —
    /// 그 밖에는 강화 탭으로 (UINav.GoToTab(Growth)). 강화가 잠겨 있으면 예전 토스트 "내 전투력 12,345 · 권장 12,000 (여유)".
    /// 누름 크기(PressScale)는 루트에 있다 (누름은 루트의 투명 판이 받는다).
    /// </summary>
    public class RecommendedPowerView : MonoBehaviour
    {
        [Tooltip("알약 (메타가 꺼져 있으면 숨긴다)")]
        [SerializeField] GameObject _root;
        [Tooltip("'{내 전투력} / 권장 {권장}' 한 줄 (리치 텍스트)")]
        [SerializeField] TMP_Text _text;
        [Tooltip("앞의 전투력(지킴이) 그림 (색을 바꾸지 않는다)")]
        [SerializeField] Image _dot;
        [SerializeField] Button _button;
        [Tooltip("평가 색 점 14")]
        [SerializeField] Image _ratingDot;
        [Tooltip("알약 테두리 (평가 색 @0.6)")]
        [SerializeField] Image _edge;
        [Tooltip("오른쪽 평가 낱말 칩 바탕 (평가 색을 옅게)")]
        [SerializeField] Image _ratingChip;
        [SerializeField] TMP_Text _ratingText;

        /// <summary>권장 부분 글자 크기 (내 전투력 대비 85% — 맞춤이 둘을 같이 줄이게 상대 크기로)</summary>
        const string RestSize = "<size=85%>";

        bool _subscribed;
        long _shownPower = -1;
        long _shownRecommended = -1;
        PowerRating _shownRating = (PowerRating)(-1);

        /// <summary>앞의 전투력 그림 (연출에서 흔들 때 등)</summary>
        public Image Icon => _dot;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnClick);
        }

        void Start()
        {
            if (MetaUi.IsActive)
            {
                Game.Battle.StageChanged += Refresh;
                Game.Power.Changed += OnPowerChanged;
                _subscribed = true;
            }
            Refresh();
        }

        void OnDestroy()
        {
            if (!_subscribed || !Game.IsReady || Game.Power == null) return;
            Game.Battle.StageChanged -= Refresh;
            Game.Power.Changed -= OnPowerChanged;
        }

        void OnPowerChanged(long oldPower, long newPower) => Refresh();

        void Refresh()
        {
            bool visible = MetaUi.IsActive;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;

            var power = Game.Power;
            int stage = Game.Battle.Stage;
            long recommended = power.GetRecommendedPower(stage);
            long mine = power.TeamPower;
            var rating = power.GetRating(mine, recommended);
            bool ratingChanged = rating != _shownRating;
            if (ratingChanged)
            {
                _shownRating = rating;
                var color = RatingColor(rating);
                if (_ratingDot != null) _ratingDot.color = color;
                if (_edge != null) _edge.color = MetaUi.Alpha(color, 0.6f);
                if (_ratingChip != null) _ratingChip.color = MetaUi.Alpha(color, 0.2f);
                if (_ratingText != null)
                {
                    _ratingText.text = PowerSystem.RatingName(rating);
                    _ratingText.color = color;
                }
            }
            if (!ratingChanged && mine == _shownPower && recommended == _shownRecommended) return;
            _shownPower = mine;
            _shownRecommended = recommended;
            _text.text = PillText(mine, recommended, rating);
        }

        /// <summary>'<b>12,345</b> &lt;size=85%&gt;&lt;color=평가&gt;/ 권장 12,000&lt;/color&gt;&lt;/size&gt;' (바뀔 때만 만든다)</summary>
        internal static string PillText(long mine, long recommended, PowerRating rating) =>
            $"<b>{PowerSystem.Format(mine)}</b> {RestSize}<color={RatingTag(rating)}>/ 권장 {PowerSystem.Format(recommended)}</color></size>";

        static readonly string[] RatingTags = new string[3];

        static string RatingTag(PowerRating rating)
        {
            int index = Mathf.Clamp((int)rating, 0, RatingTags.Length - 1);
            return RatingTags[index] ??= UITheme.ColorTag(RatingColor(rating));
        }

        void OnClick()
        {
            if (!MetaUi.IsActive) return;
            int stage = Game.Battle.Stage;
            var power = Game.Power;
            long recommended = power.GetRecommendedPower(stage);
            long mine = power.TeamPower;
            var rating = power.GetRating(mine, recommended);
            bool tutorial = Game.Tutorial != null && Game.Tutorial.IsActive;
            if (!tutorial)
            {
                // 어려움: 어디서 강해질지 모아 보여 준다 (시트가 없는 예전 장면이면 강화 탭으로)
                if (rating == PowerRating.Hard && StrengthenPopup.TryOpen()) return;
                if (Game.Features.IsUnlocked(FeatureId.ShopUpgrade))
                {
                    UINav.GoToTab(MainTab.Growth);
                    return;
                }
            }
            MetaUi.Toast($"내 전투력 {PowerSystem.Format(mine)} · 권장 {PowerSystem.Format(recommended)} ({PowerSystem.RatingName(rating)})");
        }

        /// <summary>평가 색 (여유 초록 / 도전 노랑 / 어려움 빨강 — 어두운 판 위 글자 색)</summary>
        public static Color RatingColor(PowerRating rating) => rating switch
        {
            PowerRating.Comfortable => UITheme.Positive,
            PowerRating.Challenge => UITheme.Gold,
            _ => UITheme.Negative,
        };
    }
}
