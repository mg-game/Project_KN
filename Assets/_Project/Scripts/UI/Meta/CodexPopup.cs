using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 친구 도감 980×1500 (Q-05, SPEC4 §6.2 — UINav.CodexRequested를 MetaNavigator가 받아 연다):
    ///   · 머리 Top(0, 150): '도감 {pct}%' (40 Gold 굵게) + 막대 640×22 + 고른 탭의 보너스 한 줄 (22 SubText)
    ///     ('모은 형태 20종 · 친구 공격력·체력 +7%' / '적 9종 모두 만나면 수선점 내구도 +5%' / '켜 본 시너지 12종 · 불빛 충전 +5%').
    ///     친구 탭에서는 막대 위 보너스 점 6개 (5/10/15/20/25/29종 자리, '+1%'…'+10%' 18 — 넘으면 Gold).
    ///   · 탭 Top(160, 64): [친구][의상][적][시너지][재료][이야기] (쪽 탭, 6×144, 받을 수 있는 수 배지).
    ///   · 격자 (스크롤, 5열 164×212 — CodexCellView): 받기 · 못 찾은 칸은 힌트 말풍선 · 찾은 적은 도감 문구 · 이야기 칸은 동화책 다시 읽기 (보상 없음).
    ///   · 아래 오른쪽 [모두 받기] 380×96 (고른 탭만, 받을 것이 있을 때만).
    /// 받기 소리 CodexDiscover. 도감(Game.Codex)이 바뀌면 다시 그린다 (열려 있을 때만 구독). 도감이 아직 잠겨 있으면 받기 대신 잠금 토스트.
    /// </summary>
    public class CodexPopup : UIPopup
    {
        public const int TabCount = 6;
        public static readonly string[] TabNames = { "친구", "의상", "적", "시너지", "재료", "이야기" };
        /// <summary>친구 탭 보너스 점 자리 (찾은 형태 수)</summary>
        public static readonly int[] FormPipCounts = { 5, 10, 15, 20, 25, 29 };

        [Header("머리")]
        [SerializeField] TMP_Text _percentText;
        [SerializeField] Image _barFill;
        [SerializeField] RectTransform _bar;
        [SerializeField] TMP_Text _bonusText;
        [SerializeField] GameObject _pipRoot;
        [SerializeField] Image[] _pips = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _pipLabels = Array.Empty<TMP_Text>();

        [Header("탭")]
        [SerializeField] Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] Image[] _tabImages = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _tabLabels = Array.Empty<TMP_Text>();
        [SerializeField] Badge[] _tabBadges = Array.Empty<Badge>();

        [Header("격자")]
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _gridContent;
        [SerializeField] CodexCellView _cellTemplate;
        [SerializeField] TMP_Text _emptyText;
        [Tooltip("이야기 칸 그림 (동화책 그림이 없을 때)")]
        [SerializeField] Sprite _storyIcon;

        [Header("아래")]
        [SerializeField] Button _claimAllButton;
        [SerializeField] Badge _claimAllBadge;

        readonly List<CodexEntryStatus> _entries = new();
        MetaViewPool<CodexCellView> _cells;
        Action<CodexCellView> _onCell;
        CodexCategory _tab;
        bool _subscribed;

        /// <summary>지금 고른 탭</summary>
        public CodexCategory Tab => _tab;

        protected override void Awake()
        {
            base.Awake();
            if (_cellTemplate != null && _gridContent != null) _cells = new MetaViewPool<CodexCellView>(_cellTemplate, _gridContent);
            _onCell = OnCell;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var tab = (CodexCategory)i;
                if (_tabButtons[i] != null) _tabButtons[i].onClick.AddListener(() => SelectTab(tab));
            }
            if (_claimAllButton != null) _claimAllButton.onClick.AddListener(OnClaimAll);
        }

        /// <summary>도감을 연다 (tab = 처음 탭, null이면 받을 것이 있는 첫 탭 → 친구)</summary>
        public void Show(CodexCategory? tab)
        {
            _tab = tab ?? PickTab();
            Open();
        }

        static CodexCategory PickTab()
        {
            var codex = MetaUi.IsActive ? Game.Codex : null;
            if (codex == null) return CodexCategory.Friend;
            for (int i = 0; i < TabCount; i++)
            {
                if (codex.ClaimableCount((CodexCategory)i) > 0) return (CodexCategory)i;
            }
            return CodexCategory.Friend;
        }

        protected override void OnOpen()
        {
            if (!MetaUi.IsActive || Game.Codex == null)
            {
                Close();
                return;
            }
            if (!_subscribed)
            {
                Game.Codex.Changed += OnCodexChanged;
                _subscribed = true;
            }
            if (Game.Features != null && Game.Features.IsNew(FeatureId.Codex)) Game.Features.MarkSeen(FeatureId.Codex);
            SelectTab(_tab);
        }

        protected override void OnClose()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (Game.IsReady && Game.Codex != null) Game.Codex.Changed -= OnCodexChanged;
        }

        void OnCodexChanged() => Refresh();

        void SelectTab(CodexCategory tab)
        {
            _tab = tab;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var image = i < _tabImages.Length ? _tabImages[i] : null;
                var label = i < _tabLabels.Length ? _tabLabels[i] : null;
                TabStyle.Apply(image, label, i == (int)tab, TabVariant.Page);
            }
            Refresh();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        void Refresh()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            var codex = Game.Codex;
            if (codex == null) return;
            float percent = Mathf.Clamp(codex.CompletionPercent, 0f, 100f);
            if (_percentText != null) _percentText.text = $"도감 {Mathf.FloorToInt(percent)}%";
            if (_barFill != null) MetaUi.SetBar(_barFill, percent / 100f);
            for (int i = 0; i < _tabBadges.Length && i < TabCount; i++)
            {
                if (_tabBadges[i] == null) continue;
                int count = codex.ClaimableCount((CodexCategory)i);
                if (count > 0) _tabBadges[i].SetCount(count);
                else _tabBadges[i].Hide();
            }
            RefreshBonus(codex);
            codex.GetEntries(_tab, _entries);
            int shown = 0;
            if (_cells != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    if (entry == null) continue;
                    Resolve(entry, out var sprite, out var name, out var tint);
                    _cells.Get(shown++).Bind(entry, sprite, name, tint, _onCell);
                }
                _cells.Trim(shown);
            }
            if (_emptyText != null) _emptyText.gameObject.SetActive(shown == 0);
            int claimable = codex.ClaimableCount(_tab);
            if (_claimAllButton != null)
            {
                bool show = claimable > 0 && codex.IsUnlocked;
                if (_claimAllButton.gameObject.activeSelf != show) _claimAllButton.gameObject.SetActive(show);
                if (_claimAllBadge != null)
                {
                    if (show) _claimAllBadge.SetCount(claimable);
                    else _claimAllBadge.Hide();
                }
            }
        }

        void RefreshBonus(CodexSystem codex)
        {
            var config = Game.MetaDb != null ? Game.MetaDb.codex : null;
            int found = codex.FoundCount(_tab);
            int total = codex.TotalCount(_tab);
            if (_bonusText != null) _bonusText.text = BonusLine(_tab, found, total, config);
            bool pips = _tab == CodexCategory.Friend && _pipRoot != null;
            if (_pipRoot != null) _pipRoot.SetActive(pips);
            if (!pips) return;
            int formTotal = Mathf.Max(1, total > 0 ? total : FormPipCounts[FormPipCounts.Length - 1]);
            float width = _bar != null ? _bar.rect.width : 0f;
            for (int i = 0; i < _pips.Length && i < FormPipCounts.Length; i++)
            {
                var pip = _pips[i];
                if (pip == null) continue;
                bool reached = found >= FormPipCounts[i];
                pip.color = reached ? UITheme.Gold : MetaUi.Alpha(UITheme.SubText, 0.5f);
                var rect = pip.rectTransform;
                rect.anchoredPosition = new Vector2(width * Mathf.Clamp01(FormPipCounts[i] / (float)formTotal), rect.anchoredPosition.y);
                var label = i < _pipLabels.Length ? _pipLabels[i] : null;
                if (label == null) continue;
                label.text = $"+{FormatPercent(TierPercent(config != null ? config.formTiers : null, FormPipCounts[i]))}%";
                label.color = reached ? UITheme.Gold : UITheme.SubText;
            }
        }

        /// <summary>
        /// 고른 탭의 보너스 한 줄 (순수 — 테스트용): 친구 = '모은 형태 {n}종 · 친구 공격력·체력 +{p}%', 적 = '적 {total}종 모두 만나면 수선점 내구도 +{p}%',
        /// 시너지 = '켜 본 시너지 {n}종 · 불빛 충전 +{p}%', 그 밖 = '처음 찾으면 별조각을 받아요'
        /// </summary>
        public static string BonusLine(CodexCategory tab, int found, int total, CodexConfig config)
        {
            switch (tab)
            {
                case CodexCategory.Friend:
                    return $"모은 형태 {found}종 · 친구 공격력·체력 +{FormatPercent(TierPercent(config != null ? config.formTiers : null, found))}%";
                case CodexCategory.Enemy:
                    return $"적 {Math.Max(total, found)}종 모두 만나면 수선점 내구도 +{FormatPercent(config != null ? config.allEnemiesShopHpPercent : 5f)}%";
                case CodexCategory.Synergy:
                    return $"켜 본 시너지 {found}종 · 불빛 충전 +{FormatPercent(TierPercent(config != null ? config.synergyTiers : null, found))}%";
                default:
                    return "처음 찾으면 별조각을 받아요";
            }
        }

        /// <summary>그 수에서 받는 보너스 % (count ≤ n인 가장 높은 단계의 percent, 없으면 0)</summary>
        public static float TierPercent(List<CodexBonusTier> tiers, int count)
        {
            if (tiers == null) return 0f;
            float best = 0f;
            int bestCount = -1;
            foreach (var tier in tiers)
            {
                if (tier == null || tier.count > count || tier.count < bestCount) continue;
                best = tier.percent;
                bestCount = tier.count;
            }
            return best;
        }

        static string FormatPercent(float value) =>
            Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>칸 그림 · 이름 · 등급 색 (열 때만 — 이름은 콘텐츠 그대로라 새 글을 만들지 않는다, 이야기만 짧게 만든다)</summary>
        void Resolve(CodexEntryStatus entry, out Sprite sprite, out string name, out Color tint)
        {
            sprite = null;
            name = null;
            tint = Color.white;
            var content = Game.Content;
            string id = entry.Id;
            switch (entry.Category)
            {
                case CodexCategory.Friend:
                {
                    var form = content?.Characters?.GetForm(id);
                    sprite = form?.portrait;
                    name = form?.name;
                    var lineage = content?.Characters?.GetLineageOfForm(id);
                    if (lineage != null) tint = UITheme.GradeColor(lineage.grade);
                    break;
                }
                case CodexCategory.Costume:
                {
                    var costume = content?.Characters?.GetCostume(id);
                    sprite = costume?.portrait;
                    name = costume?.name;
                    var lineage = content?.Characters?.GetLineageOfCostume(id);
                    if (lineage != null) tint = UITheme.GradeColor(lineage.grade);
                    break;
                }
                case CodexCategory.Enemy:
                {
                    var enemy = FindEnemy(id);
                    sprite = enemy?.sprite;
                    name = enemy?.name;
                    if (enemy != null && enemy.isBoss) tint = UITheme.Gold;
                    break;
                }
                case CodexCategory.Synergy:
                {
                    var synergy = content?.Synergies?.Get(id);
                    sprite = synergy?.icon;
                    name = synergy?.name;
                    break;
                }
                case CodexCategory.Material:
                {
                    var item = Game.Items?.Get(id);
                    sprite = item?.icon;
                    name = item?.name;
                    if (item != null) tint = UITheme.GradeColor(item.grade);
                    break;
                }
                default:
                {
                    int chapter = ParseChapter(id);
                    var def = chapter > 0 && Game.Story != null ? Game.Story.Get(chapter) : null;
                    sprite = def != null && def.panels != null && def.panels.Count > 0 && def.panels[0] != null ? def.panels[0].art : null;
                    if (sprite == null) sprite = _storyIcon;
                    name = def != null && !string.IsNullOrEmpty(def.title) ? $"{chapter}장 {def.title}" : chapter > 0 ? $"동화책 {chapter}장" : null;
                    break;
                }
            }
        }

        static EnemyDef FindEnemy(string id)
        {
            var config = Game.Battle != null ? Game.Battle.Config : null;
            if (config == null || config.enemies == null || string.IsNullOrEmpty(id)) return null;
            foreach (var def in config.enemies)
            {
                if (def != null && def.id == id) return def;
            }
            return null;
        }

        static int ParseChapter(string id) =>
            int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int chapter) ? chapter : 0;

        void OnCell(CodexCellView cell)
        {
            if (!MetaUi.IsActive || cell == null || cell.Status == null) return;
            var codex = Game.Codex;
            var status = cell.Status;
            if (codex == null) return;
            if (status.Claimable)
            {
                if (!codex.IsUnlocked)
                {
                    // 기록은 처음부터 모이고, 받기는 도감이 열린 뒤 (SPEC4 §2.11 #8)
                    if (Game.Features != null && !Game.Features.TryUse(FeatureId.Codex, out var locked)) MetaUi.Toast(locked);
                    return;
                }
                if (MetaUi.BlockIfTampered()) return;
                var granted = codex.TryClaim(status.Key);
                if (granted == null) return;
                UISfx.Play(UISfxKey.CodexDiscover);
                if (granted.Count > 0) MetaUi.FlyRewards(granted, cell.Rect);
                return;
            }
            if (!status.Found)
            {
                string hint = string.IsNullOrEmpty(status.Hint) ? "아직 만나지 못했어요" : status.Hint;
                if (!InfoTooltipView.Show(cell.Rect, "???", hint)) MetaUi.Toast(hint);
                return;
            }
            switch (status.Category)
            {
                case CodexCategory.Enemy:
                {
                    string text = EnemyText(status.Id);
                    var enemy = FindEnemy(status.Id);
                    if (!string.IsNullOrEmpty(text) && !InfoTooltipView.Show(cell.Rect, enemy != null ? enemy.name : "적", text)) MetaUi.Toast(text);
                    break;
                }
                case CodexCategory.Story:
                {
                    var ui = UIManager.Instance;
                    int chapter = ParseChapter(status.Id);
                    if (ui != null && chapter > 0 && ui.TryGet<StoryPagePopup>(out var page) && page.ShowReread(chapter)) break;
                    MetaUi.Toast("이 이야기는 아직 그림책에 없어요");
                    break;
                }
            }
        }

        static string EnemyText(string enemyId)
        {
            var config = Game.MetaDb != null ? Game.MetaDb.codex : null;
            if (config == null || config.enemyTexts == null) return null;
            foreach (var entry in config.enemyTexts)
            {
                if (entry != null && entry.enemyId == enemyId) return entry.text;
            }
            return null;
        }

        void OnClaimAll()
        {
            if (!MetaUi.IsActive || MetaUi.BlockIfTampered()) return;
            var codex = Game.Codex;
            if (codex == null || !codex.IsUnlocked) return;
            var granted = codex.ClaimAll(_tab);
            if (granted == null || granted.Count == 0)
            {
                MetaUi.Toast("지금 받을 도감 선물이 없어요");
                return;
            }
            UISfx.Play(UISfxKey.CodexDiscover);
            MetaUi.FlyRewards(granted, _claimAllButton != null ? (RectTransform)_claimAllButton.transform : Window);
        }
    }
}
