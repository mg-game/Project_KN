using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 별빛 산책 수첩 980×1500 (Q-06 스트레치, SPEC4 §8.4 — UINav.SeasonRequested를 MetaNavigator가 받아 연다, 이벤트 모아 보기의 수첩 칸에서도):
    ///   · 띠 Top(0, 300): Art/UI/season_banner (UiArt.SeasonBanner — 없으면 shop_event_banner).
    ///   · 머리: 'Lv.{n} · {xp % 90}/90' (32) + 막대 (열 때 차오르며 ProgressFill 소리 한 번) + '남은 기간 {d}일' (24) + '수선 일지 점수 1점 = 수첩 경험치 1' (Hint 20).
    ///   · 칸 목록 (30칸, 미션 줄 틀 ← JournalRowModel — 아이콘 자리에 칸 메달, '{n}단계', 그 칸 경험치 막대, 첫 보상 칩, [받기] / [완료] / 아직 (흐림)).
    ///     마지막 칸(30)이 의상을 주면 아이콘 자리에 그 의상 그림 (_tier30Icon = UiArt.StarwalkCostume, RM-3 R65 — 비었거나 대신 보상이면 메달).
    ///     강조 칸(5 · 10 · … · 30)은 이름 앞에 ★. 스트레치 기능이라 줄의 그림 · 크기는 미션 줄 그대로 (SPEC4의 860×110 메달 줄 대신 — 차이 기록).
    ///   · 아래 [모두 받기].
    /// 콘텐츠 칸이 비어 있으면(스트레치 꺼짐 — plan §3.2 #17) 수첩은 열리지 않는다 (IsUnlocked가 false). 열려 있는 동안 Changed만 받는다.
    /// </summary>
    public class SeasonPassPopup : UIPopup
    {
        public const string SourceLine = "수선 일지 점수 1점 = 수첩 경험치 1";

        [SerializeField] Image _banner;
        [SerializeField] TMP_Text _levelText;
        [SerializeField] Image _barFill;
        [SerializeField] TMP_Text _daysText;
        [SerializeField] TMP_Text _sourceText;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _listContent;
        [SerializeField] MissionRowView _rowTemplate;
        [Tooltip("칸 아이콘 (UiArt.MedallionDark)")]
        [SerializeField] Sprite _tierIcon;
        [Tooltip("마지막 칸(30)이 의상을 줄 때 그 칸 아이콘 (UiArt.StarwalkCostume — RM-3 R65). 비면 _tierIcon")]
        [SerializeField] Sprite _tier30Icon;
        [SerializeField] TMP_Text _emptyText;
        [SerializeField] Button _claimAllButton;
        [SerializeField] Badge _claimAllBadge;

        readonly List<SeasonTierStatus> _tiers = new();
        MetaViewPool<MissionRowView> _rows;
        Action<MissionRowView> _onRow;
        bool _subscribed;
        float _barTarget;
        float _barShown;

        protected override void Awake()
        {
            base.Awake();
            if (_rowTemplate != null && _listContent != null) _rows = new MetaViewPool<MissionRowView>(_rowTemplate, _listContent);
            _onRow = OnRow;
            if (_claimAllButton != null) _claimAllButton.onClick.AddListener(OnClaimAll);
            if (_sourceText != null) _sourceText.text = SourceLine;
        }

        protected override void OnOpen()
        {
            var season = MetaUi.IsActive ? Game.Season : null;
            if (season == null || !season.IsUnlocked)
            {
                Close();
                return;
            }
            if (!_subscribed)
            {
                season.Changed += Refresh;
                _subscribed = true;
            }
            _barShown = 0f;
            if (_barFill != null) MetaUi.SetBar(_barFill, 0f);
            Refresh();
            if (_barTarget > 0f) UISfx.Play(UISfxKey.ProgressFill);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnClose()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.IsReady && Game.Season != null) Game.Season.Changed -= Refresh;
        }

        void Update()
        {
            if (!IsOpen || _barFill == null || Mathf.Approximately(_barShown, _barTarget)) return;
            _barShown = UIMotion.Reduced ? _barTarget : Mathf.MoveTowards(_barShown, _barTarget, Time.unscaledDeltaTime * 1.2f);
            MetaUi.SetBar(_barFill, _barShown);
        }

        /// <summary>
        /// 칸 아이콘 (순수, RM-3 R65): 마지막 칸(tier ≥ tierCount)의 첫 보상이 의상이면 그 의상 그림(finalIcon), 아니면 칸 메달(tierIcon).
        /// 의상 그림이 비었거나(null) 이미 가져 대신 보상(별조각 · 기억편)을 주면 메달 그대로. 보상 칩은 늘 그 칸의 첫 보상
        /// </summary>
        public static Sprite TierIcon(int tier, int tierCount, MetaReward reward, Sprite tierIcon, Sprite finalIcon)
            => finalIcon != null && tierCount > 0 && tier >= tierCount && reward != null && reward.type == MetaRewardType.Costume ? finalIcon : tierIcon;

        /// <summary>'Lv.{n} · {xp % per}/{per}' (순수 — 마지막 칸이면 '최고 단계')</summary>
        public static string LevelLine(int level, long xp, int xpPerTier, int tierCount)
        {
            int per = Math.Max(1, xpPerTier);
            if (tierCount > 0 && level >= tierCount) return $"Lv.{level} · 최고 단계";
            return $"Lv.{Math.Max(0, level)} · {Math.Max(0, xp) % per}/{per}";
        }

        void Refresh()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            var season = Game.Season;
            if (season == null) return;
            var config = Game.MetaDb != null ? Game.MetaDb.season : null;
            int per = config != null ? Math.Max(1, config.xpPerTier) : 90;
            season.GetTiers(_tiers);
            if (_banner != null && config != null && config.banner != null) _banner.sprite = config.banner;
            if (_levelText != null) _levelText.text = LevelLine(season.Level, season.Xp, per, _tiers.Count);
            _barTarget = _tiers.Count > 0 && season.Level >= _tiers.Count ? 1f : Mathf.Clamp01((Math.Max(0, season.Xp) % per) / (float)per);
            if (_daysText != null) _daysText.text = $"남은 기간 {Math.Max(0, (int)Math.Ceiling(season.TimeLeft.TotalDays))}일";
            int shown = 0;
            int claimable = 0;
            for (int i = 0; i < _tiers.Count; i++)
            {
                var tier = _tiers[i];
                if (tier == null || _rows == null) continue;
                var state = tier.Claimed ? JournalRowState.Done : tier.CanClaim ? JournalRowState.Claimable : JournalRowState.InProgress;
                long start = (long)(tier.Tier - 1) * per;
                long value = Math.Clamp(season.Xp - start, 0, per);
                var reward = tier.Rewards != null && tier.Rewards.Count > 0 ? tier.Rewards[0] : null;
                bool highlight = tier.Def != null && tier.Def.highlight;
                var model = new JournalRowModel(tier.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    highlight ? $"★ {tier.Tier}단계" : $"{tier.Tier}단계", TierIcon(tier.Tier, _tiers.Count, reward, _tierIcon, _tier30Icon),
                    tier.Reached ? per : value, per, state, reward);
                _rows.Get(shown++).Bind(in model, _onRow);
                if (tier.CanClaim) claimable++;
            }
            _rows?.Trim(shown);
            if (_emptyText != null) _emptyText.gameObject.SetActive(shown == 0);
            if (_claimAllButton == null) return;
            bool show = claimable > 0;
            if (_claimAllButton.gameObject.activeSelf != show) _claimAllButton.gameObject.SetActive(show);
            if (_claimAllBadge == null) return;
            if (show) _claimAllBadge.SetCount(claimable);
            else _claimAllBadge.Hide();
        }

        void OnRow(MissionRowView row)
        {
            if (!MetaUi.IsActive || row == null || !row.HasModel || !row.Model.CanClaim) return;
            if (MetaUi.BlockIfTampered()) return;
            if (!int.TryParse(row.Model.Id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int tier)) return;
            var granted = Game.Season?.TryClaim(tier);
            if (granted == null) return;
            row.PlayStamp();
            UISfx.Play(UISfxKey.Claim);
            if (granted.Count > 0) MetaUi.FlyRewards(granted, row.Rect);
        }

        void OnClaimAll()
        {
            var season = MetaUi.IsActive ? Game.Season : null;
            if (season == null || MetaUi.BlockIfTampered()) return;
            var granted = season.ClaimAll();
            if (granted == null || granted.Count == 0)
            {
                MetaUi.Toast("지금 받을 수첩 선물이 없어요");
                return;
            }
            UISfx.Play(UISfxKey.Claim);
            MetaUi.FlyRewards(granted, (RectTransform)_claimAllButton.transform);
        }
    }
}
