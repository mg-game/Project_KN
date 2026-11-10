using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 상세 (와이어프레임 01): 큰 초상화 / 이름(의상 반영) / 종족 / 별 / 역할·성향·특성 칩(누르면 설명 말풍선, M59),
    /// 계보 레벨업(단추), 호감도, 기억편, 계보 공명 + 탭 [진화 기억도 | 스킬·시너지 | 의상 | 정보] (쪽 탭 모양, M53). 창 왼쪽 위 단추 잔액 칩 (M47).
    /// <para>
    /// 레벨업 (M19 · M07 · M08): 비용 버튼 세 상태 (최대면 '최대 레벨'로 쓸 수 없음), 길게 누르면 반복 (HoldRepeatButton.Step = TryStep —
    /// 반복 중에는 토스트 없이 false). 모자라면 흔들기 + 단추 부족 토스트 한 번 (UIManager.ShowShortfall(Gold)). 길게 누르는 동안은 레벨 줄만
    /// 다시 그리고 탭 쪽은 손을 뗀 뒤 한 번 다시 그린다 (초당 20번 전체를 다시 그리지 않게).
    /// </para>
    /// <para>
    /// 계보 공명 (M54 · M60B): 보조 버튼 + '준비 완료' 금 덮개(ReadyArt — 공명할 수 있고 기억편이 넉넉할 때만, 글자 Ink), 기억편이 모자라면 비용 Negative.
    /// '계보 능력치 +5% → +10%' (최대면 '(MAX)'). 기억편은 재화가 아니라 쓰기 확인 대신 '공명' 확인 창.
    /// </para>
    /// 미보유 계보의 '소환하러 가기'는 돌아가기 칸 '‹ {이름} 상세로'를 넣고 팝업을 모두 닫은 뒤 소환 시트를 연다 (M72, UX-SPEC-IA §8.4).
    /// 새 직렬화 필드(_levelUpView · _levelUpHold · _resonanceReadyArt · _resonanceLabel)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// <para>
    /// 열린 탭 안의 잠금 (F-03, SPEC4 §5.1): 친구 레벨업이 잠긴 동안(1-1 전) 레벨업 버튼은 그대로 보이되 쓸 수 없음 + '1-1 클리어 시',
    /// 누르면 잠금 문장 토스트 + 약한 진동 (LockedTapRelay — 꺼진 버튼의 누름). 의상이 잠긴 동안(2-5 전) 의상 탭 이름 옆 자물쇠 24 (_costumeTabLock).
    /// 정보 탭 점 = 받을 호감도 단계 (CR-10, Affinity.HasClaimable — 정보 탭의 호감도 칸에서 받는다). 쪽 탭 그림은 TabStyle이 TabSkin을 따른다 (SX-2 #9).
    /// 호감도 Lv5 이상이면 머리 초상화에도 친구들 카드와 같은 하트 바느질 틀 (_portraitAffinityFrame, CR-10 §6.3 — CharacterUI.ShowsAffinityFrame).
    /// 호감도 · 기능 잠금이 바뀌어도(Affinity.Changed · Features.Changed) 다시 그린다. 새 직렬화 필드(_costumeTabLock · _portraitAffinityFrame)는 비어 있어도 된다.
    /// </para>
    /// </summary>
    public class CharacterDetailPopup : UIPopup
    {
        /// <summary>탭 번호 — UINav.OpenCharacter(계보 ID, 탭) · Show(계보 ID, 탭)에 넘긴다: 진화 기억도</summary>
        public const int TabTree = 0;
        /// <summary>탭 번호: 스킬 · 시너지</summary>
        public const int TabSkill = 1;
        /// <summary>탭 번호: 의상</summary>
        public const int TabCostume = 2;
        /// <summary>탭 번호: 정보</summary>
        public const int TabInfo = 3;

        [Header("머리")]
        [SerializeField] TMP_Text _titleText;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] Image _portraitGlow;
        [Tooltip("호감도 Lv5 하트 바느질 틀 — 머리 초상화 (CR-10 §6.3, 꺼 둠 — 없어도 됨)")]
        [SerializeField] GameObject _portraitAffinityFrame;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _speciesText;
        [SerializeField] TMP_Text _starsText;
        [SerializeField] CharacterChip _roleChip;
        [SerializeField] CharacterChip _styleChip;
        [SerializeField] CharacterChip[] _traitChips;

        [Header("보유 계보")]
        [SerializeField] GameObject _ownedGroup;
        [SerializeField] TMP_Text _levelText;
        [SerializeField] Button _levelUpButton;
        [SerializeField] TMP_Text _levelUpLabel;
        [SerializeField] TMP_Text _levelUpCostText;
        [SerializeField] TMP_Text _affinityText;
        [SerializeField] TMP_Text _shardsText;
        [SerializeField] TMP_Text _resonanceText;
        [SerializeField] TMP_Text _resonanceBonusText;
        [SerializeField] Button _resonanceButton;
        [SerializeField] TMP_Text _resonanceCostText;
        [SerializeField] GameObject _resonanceDot;
        [Tooltip("레벨업 비용 버튼 세 상태 (어두운 바탕 — 최대면 '최대 레벨')")]
        [SerializeField] CostButtonView _levelUpView;
        [Tooltip("레벨업 길게 누르기 (비면 레벨업 버튼에서 찾는다)")]
        [SerializeField] HoldRepeatButton _levelUpHold;
        [Tooltip("공명 '준비 완료' 금 덮개 (공명 버튼의 맨 뒤 자식)")]
        [SerializeField] GameObject _resonanceReadyArt;
        [Tooltip("공명 버튼 이름 ('공명' — 준비 완료면 Ink, 아니면 Cream)")]
        [SerializeField] TMP_Text _resonanceLabel;

        [Header("미보유 계보")]
        [SerializeField] GameObject _lockedGroup;
        [SerializeField] Button _gachaButton;

        [Header("탭")]
        [SerializeField] Button[] _tabButtons;
        [SerializeField] TMP_Text[] _tabLabels;
        [SerializeField] GameObject[] _tabPages;
        [SerializeField] GameObject[] _tabDots;
        [SerializeField] EvolutionTreeView _tree;
        [SerializeField] CharacterSkillView _skills;
        [SerializeField] CostumeListView _costumes;
        [SerializeField] CharacterInfoView _info;
        [Tooltip("의상 탭 자물쇠 24 (의상이 잠긴 동안만 — 없어도 됨)")]
        [SerializeField] GameObject _costumeTabLock;

        LineageDef _lineage;
        int _tab;
        bool _subscribed;
        bool _levelAffordable;
        bool _progressDirty;
        float _nextProgressRefresh;
        // 알림 묶음: 한 프레임에 여러 알림이 와도(계보 변경 → 시너지 변경, 레벨업 → 단추 변경 등) LateUpdate에서 한 번만 다시 그린다
        bool _dirty;
        bool _skillDirty;
        bool _levelDirty;
        /// <summary>길게 눌러 레벨업하는 동안 미룬 전체 다시 그리기 (손을 떼면 한 번)</summary>
        bool _holdDeferred;
        Coroutine _levelPunch;

        public LineageDef Lineage => _lineage;

        protected override void Awake()
        {
            base.Awake();
            _levelUpButton.onClick.AddListener(LevelUp);
            _resonanceButton.onClick.AddListener(Resonance);
            _gachaButton.onClick.AddListener(GoToGacha);
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
            if (_levelUpHold == null) _levelUpHold = _levelUpButton.GetComponent<HoldRepeatButton>();
            if (_levelUpHold != null)
            {
                _levelUpHold.Step = TryStep;
                _levelUpHold.Denied += OnLevelUpDenied;
            }
            LockedTapRelay.Attach(_levelUpButton, OnLevelUpLockedTap);
            // 머리 칩: 누르면 역할 · 성향 · 특성 설명 (M59)
            if (_roleChip != null) _roleChip.EnableTooltip();
            if (_styleChip != null) _styleChip.EnableTooltip();
            if (_traitChips != null)
            {
                foreach (var chip in _traitChips)
                {
                    if (chip != null) chip.EnableTooltip();
                }
            }

            Game.Characters.Changed += OnDataChanged;
            Game.Inventory.Changed += OnDataChanged;
            Game.Costumes.Changed += OnDataChanged;
            Game.Evolution.FormUnlocked += OnFormUnlocked;
            Game.Currency.Changed += OnCurrencyChanged;
            Game.Characters.ProgressChanged += OnProgressChanged;
            if (Game.Synergy != null) Game.Synergy.Changed += OnSynergyChanged;
            if (Game.Affinity != null) Game.Affinity.Changed += OnDataChanged;
            if (Game.Features != null) Game.Features.Changed += OnDataChanged;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (!_subscribed || !Game.IsReady) return;
            Game.Characters.Changed -= OnDataChanged;
            Game.Inventory.Changed -= OnDataChanged;
            Game.Costumes.Changed -= OnDataChanged;
            Game.Evolution.FormUnlocked -= OnFormUnlocked;
            Game.Currency.Changed -= OnCurrencyChanged;
            Game.Characters.ProgressChanged -= OnProgressChanged;
            if (Game.Synergy != null) Game.Synergy.Changed -= OnSynergyChanged;
            if (Game.Affinity != null) Game.Affinity.Changed -= OnDataChanged;
            if (Game.Features != null) Game.Features.Changed -= OnDataChanged;
        }

        /// <summary>
        /// 편성이 바뀌면 스킬·시너지 탭의 "발동 중" 표시가 달라진다.
        /// 계보 변경(Characters.Changed)도 시너지 변경을 함께 부르므로 바로 그리지 않고 표시만 해 둔다 (LateUpdate에서 한 번).
        /// </summary>
        void OnSynergyChanged()
        {
            if (IsOpen && _lineage != null && _tab == TabSkill) _skillDirty = true;
        }

        /// <summary>누적 기록(원정/잔향이 출격/처치)이 늘면 진화 조건 충족 여부가 바뀔 수 있다. 0.5초마다 한 번만 갱신.</summary>
        void OnProgressChanged(string lineageId, ProgressType type)
        {
            if (IsOpen && _lineage != null && _lineage.id == lineageId) _progressDirty = true;
        }

        void Update()
        {
            if (!_progressDirty || Time.unscaledTime < _nextProgressRefresh) return;
            _progressDirty = false;
            _nextProgressRefresh = Time.unscaledTime + 0.5f;
            if (IsOpen && _lineage != null) _dirty = true;
        }

        /// <summary>
        /// 이번 프레임에 쌓인 알림을 한 번에 반영한다 (전체 &gt; 스킬 탭 / 레벨 줄 순으로 겹치는 것은 한 번만).
        /// 레벨업을 길게 누르는 동안은 레벨 · 수치 줄만 그리고 전체는 손을 뗀 뒤 한 번 (반복은 초당 최대 20번).
        /// </summary>
        void LateUpdate()
        {
            bool holding = _levelUpHold != null && _levelUpHold.IsHolding;
            bool releasedAfterHold = _holdDeferred && !holding;
            if (!_dirty && !_skillDirty && !_levelDirty && !releasedAfterHold) return;
            bool full = _dirty || releasedAfterHold, skill = _skillDirty, level = _levelDirty;
            _dirty = _skillDirty = _levelDirty = false;
            if (!IsOpen || _lineage == null)
            {
                _holdDeferred = false;
                return;
            }
            if (full && holding)
            {
                _holdDeferred = true;
                RefreshLevel();
                RefreshStats();
                return;
            }
            _holdDeferred = false;
            if (full)
            {
                Refresh();
                return;
            }
            if (skill && _tab == TabSkill) _skills.Show(_lineage);
            if (level) RefreshLevel();
        }

        /// <summary>
        /// 계보 상세를 연다 (M73). tab(TabTree · TabSkill · TabCostume · TabInfo)을 주면 그 탭으로 연다 — 이미 열려 있어도 그 탭으로 바꾼다 (Open → OnOpen → SelectTab).
        /// -1(기본)이면 예전과 같다: 다른 계보로 바뀌면 진화 기억도 탭부터, 같은 계보면 보던 탭 그대로. 범위 밖 탭 번호는 -1처럼 다룬다.
        /// UINav.OpenCharacter(계보 ID, 탭)가 CharacterNavRouter를 거쳐 이것을 부른다 (MAIN · 시트 · 다른 팝업 위 어디서든).
        /// </summary>
        public void Show(string lineageId, int tab = -1)
        {
            var lineage = Game.Characters.Db.GetLineage(lineageId);
            if (lineage == null)
            {
                Debug.LogWarning($"[UI] 없는 계보: {lineageId}");
                return;
            }
            if (lineage != _lineage) _tab = TabTree;
            if (tab >= TabTree && tab <= TabInfo) _tab = tab;
            _lineage = lineage;
            Open();
        }

        /// <summary>진화 제작 직후 해당 노드를 반짝이며 보여준다.</summary>
        public void HighlightForm(string formId)
        {
            if (!IsOpen || _lineage == null) return;
            var owner = Game.Characters.Db.GetLineageOfForm(formId);
            if (owner != _lineage) return;
            if (_tab != TabTree) SelectTab(TabTree);
            _tree.PlayUnlock(formId);
        }

        protected override void OnOpen()
        {
            if (_lineage == null) return;
            SelectTab(_tab);
        }

        void OnDataChanged()
        {
            if (IsOpen && _lineage != null) _dirty = true;
        }

        void OnFormUnlocked(CharacterFormDef form) => OnDataChanged();

        /// <summary>방치 수익으로 단추가 자주 바뀌므로 비용 색(살 수 있는지)이 바뀔 때만 다시 그린다.</summary>
        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (type != CurrencyType.Gold || !IsOpen || _lineage == null || !CharacterUI.IsOwned(_lineage)) return;
            if (CanAffordLevelUp() != _levelAffordable) _levelDirty = true;
        }

        bool CanAffordLevelUp() => Game.Currency.Has(CurrencyType.Gold, Game.Characters.GetLevelUpCost(_lineage.id));

        /// <summary>탭 바꾸기 (쪽 탭 모양 M53: 고른 탭 = 양피지 + Ink 굵게, 나머지 = TabNormal + SubText)</summary>
        void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _tabPages.Length; i++) _tabPages[i].SetActive(i == index);
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                TabStyle.Apply(_tabButtons[i].targetGraphic, _tabLabels[i], i == index, TabVariant.Page);
            }
            Refresh();
        }

        void Refresh()
        {
            _dirty = _skillDirty = _levelDirty = false;
            RefreshHeader();
            RefreshPage();
        }

        void RefreshHeader()
        {
            var baseForm = CharacterUI.BaseForm(_lineage);
            bool owned = CharacterUI.IsOwned(_lineage);
            var gradeColor = UITheme.GradeColor(_lineage.grade);

            _titleText.text = $"{_lineage.name} 계보";
            _portrait.Set(CharacterUI.Portrait(baseForm), _lineage, _lineage.name, owned ? PortraitTone.Normal : PortraitTone.Silhouette);
            _portraitGlow.color = CharacterUI.WithAlpha(owned ? gradeColor : UITheme.Divider, _portraitGlow.color.a);
            if (_portraitAffinityFrame != null)
            {
                bool heart = CharacterUI.ShowsAffinityFrame(_lineage);
                if (_portraitAffinityFrame.activeSelf != heart) _portraitAffinityFrame.SetActive(heart);
            }
            _nameText.text = owned ? CharacterUI.DisplayName(baseForm) : _lineage.name;
            _speciesText.text = string.IsNullOrEmpty(_lineage.origin) ? _lineage.species : $"{_lineage.species} · {_lineage.origin}";
            _starsText.text = $"{UITheme.Stars(_lineage.grade)} <size=70%>{UITheme.GradeName(_lineage.grade)}</size>";
            _starsText.color = gradeColor;

            _roleChip.gameObject.SetActive(baseForm != null);
            _styleChip.gameObject.SetActive(baseForm != null);
            if (baseForm != null)
            {
                _roleChip.SetRole(baseForm.recommendedRole);
                _styleChip.SetStyle(baseForm.style);
            }
            CharacterUI.SetTraitChips(_traitChips, _lineage, false);

            _ownedGroup.SetActive(owned);
            _lockedGroup.SetActive(!owned);
            if (owned)
            {
                RefreshLevel();
                RefreshStats();
            }

            _tabDots[TabTree].SetActive(CharacterUI.CanCraftAny(_lineage));
            _tabDots[TabSkill].SetActive(false);
            _tabDots[TabCostume].SetActive(CharacterUI.CanUnlockCostume(_lineage));
            _tabDots[TabInfo].SetActive(owned && MetaUi.IsActive && Game.Affinity != null && Game.Affinity.HasClaimable(_lineage.id));
            if (_costumeTabLock != null)
            {
                bool costumeLocked = !CharacterUI.IsUnlocked(FeatureId.Costumes);
                if (_costumeTabLock.activeSelf != costumeLocked) _costumeTabLock.SetActive(costumeLocked);
            }
        }

        void RefreshLevel()
        {
            if (!CharacterUI.IsOwned(_lineage)) return;
            var characters = Game.Characters;
            int level = characters.GetLevel(_lineage.id);
            bool isMax = level >= characters.MaxLevel;

            _levelText.text = isMax
                ? $"Lv.{level} <size=55%><color={UITheme.ColorTag(UITheme.Gold)}>MAX</color></size>"
                : $"Lv.{level}<size=50%><color={UITheme.ColorTag(UITheme.SubText)}> / {characters.MaxLevel}</color></size>";

            double cost = characters.GetLevelUpCost(_lineage.id);
            _levelAffordable = CanAffordLevelUp();
            // F-03: 친구 레벨업이 잠긴 동안은 쓸 수 없음 + '1-1 클리어 시' (누르면 LockedTapRelay → 잠금 안내)
            bool gated = !isMax && !CharacterUI.IsUnlocked(FeatureId.CharacterLevelUp);
            string unavailable = gated ? CharacterUI.LockedShort(CharacterUI.LockedHint(FeatureId.CharacterLevelUp)) : "최대 레벨";
            if (_levelUpView != null)
            {
                // 비용 버튼 세 상태 (M07): 최대 = 쓸 수 없음('최대 레벨', 비용 줄 숨김), 모자람 = 누를 수 있는 부족 모양
                if (!isMax) _levelUpCostText.text = NumberFormatter.FormatCurrency(cost);
                _levelUpView.SetState(isMax || gated ? CostButtonView.State.Unavailable
                    : _levelAffordable ? CostButtonView.State.Affordable : CostButtonView.State.Short, unavailable);
                return;
            }
            // 다시 빌드하기 전 씬: 예전 모양
            _levelUpButton.interactable = !isMax && !gated;
            _levelUpLabel.text = isMax || gated ? unavailable : "레벨업";
            _levelUpCostText.text = isMax || gated ? "-" : NumberFormatter.FormatCurrency(cost);
            _levelUpCostText.color = isMax || gated || _levelAffordable ? UITheme.Text : UITheme.Negative;
        }

        void RefreshStats()
        {
            var characters = Game.Characters;
            string id = _lineage.id;
            int shards = characters.GetShards(id);
            _affinityText.text = characters.GetAffinity(id).ToString("N0");
            _shardsText.text = shards.ToString("N0");

            int resonance = characters.GetResonanceLevel(id);
            int cost = characters.GetResonanceCost(id);
            bool canRaise = cost > 0;
            bool ready = canRaise && shards >= cost;
            _resonanceText.text = $"계보 공명 <color={UITheme.ColorTag(UITheme.Lavender)}>Lv.{resonance}</color>";
            _resonanceBonusText.text = ResonanceLine(characters.GetResonanceBonusPercent(id), characters.GetResonanceBonusPercentAt(resonance + 1), canRaise);
            _resonanceButton.interactable = canRaise;
            _resonanceCostText.text = canRaise ? $"기억편 {cost:N0}" : "MAX";
            _resonanceDot.SetActive(ready);

            if (_resonanceReadyArt == null)
            {
                // 다시 빌드하기 전 씬 (금 버튼 위 Ink)
                _resonanceCostText.color = !canRaise || shards >= cost ? UITheme.Ink : CharacterUI.InkNegative;
                return;
            }
            // 보조 버튼 + 준비 완료 금 덮개 (M54): 준비 완료면 이름 · 비용 Ink, 아니면 Cream, 기억편이 모자라면 비용 Negative (어두운 바탕)
            if (_resonanceReadyArt.activeSelf != ready) _resonanceReadyArt.SetActive(ready);
            var labelColor = ready ? UITheme.Ink : UITheme.Cream;
            // 최대 공명이면 버튼이 꺼져 있다: 위 interactable 때 ButtonLabelDim이 흐린 이름을 이 색 쓰기가 덮으므로 같은 0.5로 직접 흐린다 (M25)
            if (_resonanceLabel != null) _resonanceLabel.color = canRaise ? labelColor : CharacterUI.WithAlpha(labelColor, 0.5f);
            _resonanceCostText.color = ready || !canRaise ? labelColor : UITheme.Negative;
        }

        /// <summary>
        /// 계보 공명 효과 줄 (M60B): '계보 능력치 <color=SubText>+5%</color> → <b>+10%</b>' (바탕 글자색 Positive — 어두운 칸),
        /// 최대면 '계보 능력치 +50% (MAX)'.
        /// </summary>
        internal static string ResonanceLine(float current, float next, bool canRaise)
        {
            if (!canRaise) return $"계보 능력치 +{current:0.#}% (MAX)";
            return $"계보 능력치 <color={UITheme.ColorTag(UITheme.SubText)}>+{current:0.#}%</color> → <b>+{next:0.#}%</b>";
        }

        void RefreshPage()
        {
            switch (_tab)
            {
                case TabTree: _tree.Show(_lineage); break;
                case TabSkill: _skills.Show(_lineage); break;
                case TabCostume: _costumes.Show(_lineage); break;
                default: _info.Show(_lineage); break;
            }
        }

        // ───────────── 동작 ─────────────

        /// <summary>레벨업 한 번 누름: 최대면 안내, 모자라면 흔들기 + 단추 부족 토스트 (M08 — 팝업 없음)</summary>
        void LevelUp()
        {
            if (_lineage == null) return;
            if (!CharacterUI.IsUnlocked(FeatureId.CharacterLevelUp))
            {
                CharacterUI.DenyLocked(FeatureId.CharacterLevelUp);
                return;
            }
            var characters = Game.Characters;
            if (characters.GetLevel(_lineage.id) >= characters.MaxLevel)
            {
                CharacterUI.Toast("이미 최대 레벨이에요.");
                return;
            }
            double cost = characters.GetLevelUpCost(_lineage.id);
            if (!characters.TryLevelUp(_lineage.id))
            {
                DenyLevelUp(cost);
                return;
            }
            OnLeveledUp();
        }

        /// <summary>꺼진 레벨업 버튼을 누름 (F-03): 친구 레벨업이 잠겼으면 잠금 문장 토스트 + 약한 진동 (최대 레벨이면 아무것도 하지 않는다)</summary>
        void OnLevelUpLockedTap()
        {
            if (_lineage != null && IsOpen && !CharacterUI.IsUnlocked(FeatureId.CharacterLevelUp)) CharacterUI.DenyLocked(FeatureId.CharacterLevelUp);
        }

        /// <summary>길게 누르는 동안 반복 한 번 (M19): 토스트 없이 성공 여부만. false면 누르기가 끝나고 Denied가 한 번 온다</summary>
        bool TryStep()
        {
            if (_lineage == null || !IsOpen || !CharacterUI.IsUnlocked(FeatureId.CharacterLevelUp)) return false;
            var characters = Game.Characters;
            if (characters.GetLevel(_lineage.id) >= characters.MaxLevel) return false;
            if (!characters.TryLevelUp(_lineage.id)) return false;
            OnLeveledUp();
            return true;
        }

        /// <summary>길게 누르다 멈춤 (한 번): 최대에 닿았으면 안내, 단추가 모자라면 흔들기 + 부족 토스트</summary>
        void OnLevelUpDenied()
        {
            if (_lineage == null) return;
            var characters = Game.Characters;
            if (characters.GetLevel(_lineage.id) >= characters.MaxLevel)
            {
                CharacterUI.Toast("최대 레벨이 되었어요!", ToastKind.Success);
                return;
            }
            DenyLevelUp(characters.GetLevelUpCost(_lineage.id));
        }

        void DenyLevelUp(double cost)
        {
            if (_levelUpView != null) _levelUpView.Deny();
            var ui = UIManager.Instance;
            if (ui != null) ui.ShowShortfall(CurrencyType.Gold, cost);
        }

        void OnLeveledUp()
        {
            if (_levelUpView != null) _levelUpView.Spent();
            if (_levelPunch != null) StopCoroutine(_levelPunch);
            _levelPunch = StartCoroutine(PunchLevel());
        }

        IEnumerator PunchLevel()
        {
            yield return CharacterMotion.Punch(_levelText.transform, 0.16f, 0.2f);
            _levelPunch = null;
        }

        /// <summary>계보 공명: 기억편은 재화가 아니라 쓰기 확인(비용 줄) 대신 동사 확인 창 '공명' (M55 결정)</summary>
        void Resonance()
        {
            var characters = Game.Characters;
            string id = _lineage.id;
            int cost = characters.GetResonanceCost(id);
            if (cost <= 0)
            {
                CharacterUI.Toast("계보 공명이 이미 최대예요.");
                return;
            }
            int shards = characters.GetShards(id);
            if (shards < cost)
            {
                CharacterUI.Toast($"기억편이 부족해요. (보유 {shards:N0} / 필요 {cost:N0})");
                return;
            }
            int level = characters.GetResonanceLevel(id);
            float current = characters.GetResonanceBonusPercent(id);
            float next = characters.GetResonanceBonusPercentAt(level + 1);
            UIManager.Instance.ShowConfirm("계보 공명",
                $"기억편 {cost:N0}개로 {_lineage.name} 계보의 공명을 높일까요?\n<size=85%>능력치 +{current:0.#}% → +{next:0.#}% · 모든 형태가 함께 강해져요</size>",
                () => DoResonance(id), "공명");
        }

        void DoResonance(string lineageId)
        {
            var characters = Game.Characters;
            if (!characters.TryResonance(lineageId))
            {
                CharacterUI.Toast("기억편이 부족해요.");
                return;
            }
            CharacterUI.Toast($"계보 공명 Lv.{characters.GetResonanceLevel(lineageId)} 달성! 능력치 +{characters.GetResonanceBonusPercent(lineageId):0.#}%", ToastKind.Success);
            if (IsOpen) StartCoroutine(CharacterMotion.Punch(_resonanceText.transform, 0.14f, 0.24f));
        }

        /// <summary>
        /// 미보유 계보 '소환하러 가기' (M72 · UX-SPEC-IA §8.4): 돌아가기 칸 '‹ {이름} 상세로'(지금 화면 틀과 탭을 기억) → 팝업을 모두 닫고 → 소환 시트.
        /// 칩은 소환 시트 머리에 보이고, 누르면 그 틀로 돌아가 이 상세를 같은 탭으로 다시 연다.
        /// 소환이 아직 잠겼으면(1-3 클리어 전) 칸도 넣지 않고 팝업도 닫지 않은 채 열리는 조건 토스트만 (닫고 나서 탭 잠금에 막히면 친구들 시트에 엉뚱한 칩이 남는다).
        /// </summary>
        void GoToGacha()
        {
            if (!CharacterUI.FeatureOpen(FeatureId.Gacha)) return;
            if (_lineage != null)
            {
                string id = _lineage.id;
                int tab = _tab;
                UINav.PushReturn($"‹ {_lineage.name} 상세로", () => UINav.OpenCharacter(id, tab));
            }
            CharacterUI.CloseAllPopups();
            UINav.GoToTab(MainTab.Gacha);
        }
    }
}
