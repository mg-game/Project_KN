using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 상세: 큰 아이콘 · 종류 · 지금 상태와 진행 막대 · 설명 · 강해지는 스테이지 기믹 ·
    /// 단계별 효과(설명 + 능력치 + 보너스 스킬) · 관여하는 친구(조합은 필요한 친구, 보유/미보유/편성 상태).
    /// 아래 버튼: 시너지 도감 / 편성하러 가기.
    /// 열려 있는 동안 시너지 변화(SynergySystem.Changed = 편성 + 친구)와 스테이지 기믹 변화를 듣고 LateUpdate에서 한 번만 다시 그린다.
    /// </summary>
    public class SynergyDetailPopup : UIPopup
    {
        [SerializeField] TMP_Text _titleText;
        [SerializeField] ScrollRect _scroll;

        [Header("머리")]
        [SerializeField] Image _iconGlow;
        [SerializeField] Image _iconRing;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconFallback;
        [SerializeField] Image _kindChip;
        [SerializeField] TMP_Text _kindText;
        [SerializeField] TMP_Text _subtitleText;
        [SerializeField] TMP_Text _statusText;
        [SerializeField] SynergyProgressBar _progress;
        [SerializeField] GameObject _progressGroup;
        [SerializeField] TMP_Text _descText;

        [Header("스테이지 기믹")]
        [SerializeField] GameObject _favoredGroup;
        [SerializeField] Image _favoredBg;
        [SerializeField] Image _favoredIcon;
        [SerializeField] TMP_Text _favoredFallback;
        [SerializeField] TMP_Text _favoredText;
        [SerializeField] GameObject _favoredNowTag;

        [Header("단계 / 친구")]
        [SerializeField] RectTransform _tierContent;
        [SerializeField] SynergyTierRowView _tierPrefab;
        [SerializeField] TMP_Text _membersTitle;
        [SerializeField] TMP_Text _membersHint;
        [SerializeField] RectTransform _memberContent;
        [SerializeField] SynergyMemberCellView _memberPrefab;

        [Header("버튼")]
        [SerializeField] Button _codexButton;
        [SerializeField] Button _formationButton;

        readonly List<SynergyMember> _memberList = new();
        readonly StringBuilder _sb = new();
        UIListPool<SynergyTierRowView> _tiers;
        UIListPool<SynergyMemberCellView> _members;
        string _synergyId;
        bool _subscribed;
        /// <summary>다시 그려야 함 (변경 신호를 모아 LateUpdate에서 한 번)</summary>
        bool _dirty;
        /// <summary>마지막으로 그린 스테이지 기믹</summary>
        StageModifierType _drawnStage;

        protected override void Awake()
        {
            base.Awake();
            _tiers = new UIListPool<SynergyTierRowView>(_tierPrefab, _tierContent);
            _members = new UIListPool<SynergyMemberCellView>(_memberPrefab, _memberContent);
            _codexButton.onClick.AddListener(OpenCodex);
            _formationButton.onClick.AddListener(GoToFormation);
        }

        public void Show(string synergyId)
        {
            var def = Game.Synergy.Db != null ? Game.Synergy.Db.Get(synergyId) : null;
            if (def == null) return;
            _synergyId = synergyId;
            Open();
            Draw(def);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>열려 있는 동안 편성/친구/스테이지 기믹이 바뀌면 다시 그린다 (이미 열린 채 Open이 또 불려도 한 번만 구독)</summary>
        protected override void OnOpen() => Subscribe(true);

        protected override void OnClose()
        {
            Subscribe(false);
            _dirty = false;
        }

        void OnDestroy() => Subscribe(false);

        void Subscribe(bool on)
        {
            if (on == _subscribed || !Game.IsReady) return;
            _subscribed = on;
            if (on)
            {
                Game.Synergy.Changed += MarkDirty;
                Game.Battle.StageChanged += OnStageChanged;
            }
            else
            {
                Game.Synergy.Changed -= MarkDirty;
                Game.Battle.StageChanged -= OnStageChanged;
            }
        }

        void MarkDirty() => _dirty = true;

        /// <summary>전투는 팝업 뒤에서도 계속된다 — 기믹이 바뀐 스테이지로 넘어가면 "지금 발동 중!"과 ×1.5 단계 수치를 고친다</summary>
        void OnStageChanged()
        {
            if (SynergyUI.CurrentStageModifier() != _drawnStage) _dirty = true;
        }

        void LateUpdate()
        {
            if (_dirty && IsOpen) Redraw();
        }

        void Redraw()
        {
            _dirty = false;
            var def = Game.Synergy.Db != null ? Game.Synergy.Db.Get(_synergyId) : null;
            if (IsOpen && def != null) Draw(def);
        }

        void Draw(SynergyDef def)
        {
            _dirty = false;
            var status = FindStatus(def);
            var stage = SynergyUI.CurrentStageModifier();
            _drawnStage = stage;
            var accent = SynergyUI.AccentColor(def);
            bool active = status.IsActive;

            _titleText.text = SynergyUI.DisplayName(def);

            _iconGlow.color = WithAlpha(accent, active ? 0.85f : 0.35f);
            _iconRing.color = active ? Color.white : WithAlpha(Color.white, 0.7f);
            SynergyIcons.ApplyInRing(_icon, _iconFallback, def, 26f, 6f);
            _kindChip.color = SynergyUI.KindColor(def.kind);
            _kindText.text = UITheme.SynergyKindName(def.kind);
            _subtitleText.text = SynergyUI.Subtitle(def);
            _statusText.text = StatusLine(status);

            bool combo = def.kind == SynergyKind.Combo;
            _progressGroup.SetActive(!combo);
            if (!combo) _progress.Set(status);

            _descText.text = Description(def);

            DrawFavored(def, stage);
            DrawTiers(status, SynergyUI.IsFavoredNow(def, stage) ? def.favoredMultiplier : 1f);
            DrawMembers(def, status);
        }

        // ───────────── 머리 ─────────────

        string StatusLine(SynergyStatus status)
        {
            var def = status.Def;
            string gold = UITheme.ColorTag(UITheme.Gold);
            string sub = UITheme.ColorTag(UITheme.SubText);

            if (def.kind == SynergyKind.Combo)
            {
                if (status.IsActive) return $"<color={gold}>조합 완성!</color> 효과가 발동 중이에요";
                var missing = SynergyUI.MissingNames(def, _sb);
                int need = SynergyUI.RequiredCount(def);
                return string.IsNullOrEmpty(missing)
                    ? $"편성 {status.Count}/{need}"
                    : $"편성 <color={gold}>{status.Count}</color>/{need}  <size=85%><color={sub}>{MenuText.Object(missing)} 함께 편성해 주세요</color></size>";
            }

            if (status.IsActive)
            {
                string line = $"지금 <color={gold}>{status.Count}명</color> · <color={gold}>{status.TierIndex + 1}단계</color> 발동 중";
                if (status.NextTier != null)
                {
                    int more = Mathf.Max(1, status.NextTier.count - status.Count);
                    line += $"\n<size=85%><color={sub}>{more}명 더 모이면 {status.TierIndex + 2}단계</color></size>";
                }
                else line += $"\n<size=85%><color={sub}>가장 높은 단계예요</color></size>";
                return line;
            }

            int first = SynergyUI.RequiredCount(def);
            return $"지금 {status.Count}명 <size=85%><color={sub}>· {first}명부터 발동해요</color></size>";
        }

        static string Description(SynergyDef def)
        {
            // 능력치 이름과 수치 · '×1.5'는 앞말과 붙여 둔다 (M28)
            string desc = SynergyUI.KeepStatNumbers(string.IsNullOrEmpty(def.description) ? DefaultDescription(def) : def.description);
            string sub = UITheme.ColorTag(UITheme.SubText);
            return def.kind switch
            {
                SynergyKind.Combo => $"{desc}\n<size=86%><color={sub}>지킴이든 잔향이든 상관없이, 친구들이 모두 편성되면 발동해요.</color></size>",
                _ => $"{desc}\n<size=86%><color={sub}>같은 계보의 지킴이와 잔향이는 각각 한 명으로 세요. 잠긴 덱 칸은 세지 않아요.</color></size>",
            };
        }

        static string DefaultDescription(SynergyDef def) => def.kind switch
        {
            SynergyKind.Trait => $"{MenuText.Object(UITheme.TraitName(def.trait) + " 특성")} 가진 친구를 함께 편성할수록 강해져요.",
            SynergyKind.Style => $"{UITheme.StyleName(def.style)} 역할 친구를 함께 편성할수록 강해져요.",
            _ => "특별한 사이의 친구들이 함께하면 생기는 조합이에요.",
        };

        void DrawFavored(SynergyDef def, StageModifierType stage)
        {
            bool has = def.favoredModifier != StageModifierType.None;
            _favoredGroup.SetActive(has);
            if (!has) return;

            bool now = SynergyUI.IsFavoredNow(def, stage);
            var color = UITheme.StageModifierColor(def.favoredModifier);
            // 보라 바느질 판 그림: 지금 발동 중이면 기믹 색을 살짝 섞고, 아니면 조금 투명하게
            _favoredBg.color = now ? Color.Lerp(Color.white, color, 0.35f) : WithAlpha(Color.white, 0.75f);
            SynergyIcons.ApplyStage(_favoredIcon, _favoredFallback, def.favoredModifier);
            string name = UITheme.StageModifierName(def.favoredModifier);
            _favoredText.text = $"<color={UITheme.ColorTag(color)}>{name}</color> 스테이지에서 효과 <color={UITheme.ColorTag(UITheme.Gold)}>{SynergyUI.Multiplier(def)}</color>";
            _favoredNowTag.SetActive(now);
        }

        // ───────────── 단계 / 친구 ─────────────

        void DrawTiers(SynergyStatus status, float multiplier)
        {
            int count = SynergyUI.TierCount(status.Def);
            _tiers.SetCount(count);
            for (int i = 0; i < count; i++)
            {
                if (status.Def.tiers[i] == null)
                {
                    _tiers[i].gameObject.SetActive(false);
                    continue;
                }
                _tiers[i].Set(status, i, multiplier, _sb);
            }
        }

        void DrawMembers(SynergyDef def, SynergyStatus status)
        {
            SynergyUI.CollectMembers(def, _memberList);
            _members.SetCount(_memberList.Count);
            // 보유 = 지금 편성할 수 있는 것 (편성 중 + 보유). 진화 제작 전 형태(칸에 "진화 전")는 따로 센다
            int placed = 0, owned = 0, notEvolved = 0;
            for (int i = 0; i < _memberList.Count; i++)
            {
                var member = _memberList[i];
                _members[i].Set(member);
                switch (member.State)
                {
                    case SynergyMemberState.Placed:
                        placed++;
                        owned++;
                        break;
                    case SynergyMemberState.Owned:
                        owned++;
                        break;
                    case SynergyMemberState.FormLocked:
                        notEvolved++;
                        break;
                }
            }

            string gold = UITheme.ColorTag(UITheme.Gold);
            switch (def.kind)
            {
                case SynergyKind.Combo:
                    _membersTitle.text = "필요한 친구";
                    _membersHint.text = $"보유 <color={gold}>{owned}</color>/{_memberList.Count} · 편성 <color={gold}>{placed}</color>/{_memberList.Count}"
                        + (owned < _memberList.Count ? "  <size=88%>미보유 친구는 소환에서 만날 수 있어요</size>" : string.Empty);
                    break;
                case SynergyKind.Trait:
                    _membersTitle.text = $"{UITheme.TraitName(def.trait)} 특성 친구";
                    _membersHint.text = $"편성 중인 형태 <color={gold}>{status.Count}</color> · 친구를 누르면 상세를 볼 수 있어요";
                    break;
                default:
                    _membersTitle.text = $"{UITheme.StyleName(def.style)} 역할 형태";
                    _membersHint.text = $"편성 중 <color={gold}>{placed}</color> · 보유 {owned}/{_memberList.Count} 형태"
                        + (notEvolved > 0
                            ? $"  <size=88%><color={UITheme.ColorTag(UITheme.SubText)}>진화 전 {notEvolved} · 진화 제작으로 해금</color></size>"
                            : string.Empty);
                    break;
            }
        }

        static SynergyStatus FindStatus(SynergyDef def)
        {
            var statuses = Game.Synergy.GetStatuses(true);
            if (statuses != null)
            {
                foreach (var status in statuses)
                {
                    if (status?.Def != null && status.Def.id == def.id) return status;
                }
            }
            return new SynergyStatus { Def = def };
        }

        // ───────────── 버튼 ─────────────

        void OpenCodex()
        {
            var codex = UIManager.Instance.Get<SynergyCodexPopup>();
            var def = Game.Synergy.Db != null ? Game.Synergy.Db.Get(_synergyId) : null;
            Close();
            if (codex == null) return;
            if (def != null) codex.Show(def.kind);
            else codex.ShowFront();
        }

        void GoToFormation()
        {
            // 화면 이동: 열린 팝업을 위에서부터 모두 닫는다 (닫기 규칙 없이 — CloseTop을 되풀이하면 막는 팝업에서 멈춘다)
            UIManager.Instance.CloseAll();
            UINav.GoToTab(MainTab.Formation);
        }

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
