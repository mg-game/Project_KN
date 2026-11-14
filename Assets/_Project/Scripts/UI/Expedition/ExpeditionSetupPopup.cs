using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 원정 준비: 보낼 친구(계보)를 최대 인원까지 고르고 예상 획득을 확인한 뒤 출발한다.
    ///   · 열 때 (M57): 이 구역에 지난번 보낸 친구가 모두 지금 보낼 수 있으면 그대로 채우고, 아니면 조용히 자동 선택 — 열자마자 '출발'을 누를 수 있다.
    ///     자동 선택 버튼은 그대로 두고, 고른 친구가 0명일 때만 토스트를 띄운다.
    ///   · 편성 중인 친구도 보낼 수 있다 (원정 중에도 함께 싸운다, M05 — 칸에 '지킴이' / '잔향이' 알약). 다른 원정에 나간 친구만 사유와 함께 흐리게.
    ///     보낼 수 있는 친구가 하나도 없으면 안내 줄이 '보낼 수 있는 친구가 모두 원정 중이에요'.
    ///   · 자동 선택 순서: 원정에 나가 있지 않음 → 진화 조건 '원정 N회'가 많이 남은 친구 → 전투력 → ID (AutoPickKey.Compare).
    ///   · 다녀올 시간 (IDLE-04, SPEC4 §5.3): 칸 고르기 3칸(240x88 — 1줄 시간 '30분' / '1시간 30분' / '3시간', 2줄 '보상 ×1' / '×2.8' / '×5.4').
    ///     고를 수 없는 단계(12시간 넘음 · 튜토리얼 중 — ExpeditionSystem.IsTierAllowed)는 숨기고, 고를 수 있는 단계가 하나뿐이면 줄을 숨긴다 (예전 모양).
    ///     처음 칸 = 그 구역의 지난번 단계(ExpeditionSystem.GetLastTier, 고를 수 없으면 기본). 출발은 TryStart(구역, 친구, 단계), 버튼 · 토스트 시간도 그 단계의 시간.
    /// </summary>
    public class ExpeditionSetupPopup : UIPopup
    {
        /// <summary>안내 줄 (M05 — 탭 머리의 안내는 M58로 지웠고 이 팝업으로 옮겼다)</summary>
        const string HintDefault = "친구를 보내 재료를 모아요 · 원정 중에도 함께 싸워요";
        const string HintAllAway = "보낼 수 있는 친구가 모두 원정 중이에요";

        [SerializeField] TMP_Text _titleText;
        [SerializeField] Image _art;
        [SerializeField] TMP_Text _artFallbackText;
        [SerializeField] TMP_Text _descText;
        [SerializeField] TMP_Text _infoText;
        [SerializeField] TMP_Text _selectCountText;
        [SerializeField] RectTransform _memberContent;
        [SerializeField] ExpeditionMemberCellView _memberPrefab;
        [SerializeField] GameObject _noMemberLabel;
        [SerializeField] RectTransform _dropContent;
        [SerializeField] RewardSlotView _dropPrefab;
        [SerializeField] Button _autoButton;
        [SerializeField] Button _startButton;
        [SerializeField] TMP_Text _startLabel;
        [Tooltip("안내 줄 (보통 '친구를 보내 재료를 모아요 · …', 보낼 친구가 없으면 '모두 원정 중이에요')")]
        [SerializeField] TMP_Text _hintText;
        [Header("다녀올 시간 (IDLE-04 — 다시 빌드한 씬부터)")]
        [Tooltip("'다녀올 시간' 줄 (고를 수 있는 단계가 둘 이상일 때만)")]
        [SerializeField] GameObject _tierRow;
        [Tooltip("단계 칸 (기본 · 길게 · 밤새 순서)")]
        [SerializeField] Button[] _tierButtons = System.Array.Empty<Button>();
        [SerializeField] TMP_Text[] _tierTimeTexts = System.Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text[] _tierScaleTexts = System.Array.Empty<TMP_Text>();

        readonly List<string> _selected = new();
        readonly List<LineageDef> _lineages = new();
        readonly List<LineageDef> _candidates = new();
        readonly List<AutoPickKey> _keys = new();
        UIListPool<ExpeditionMemberCellView> _members;
        UIListPool<RewardSlotView> _drops;
        ExpeditionZoneDef _zone;
        /// <summary>고른 다녀올 시간 단계 (0 기본 · 1 길게 · 2 밤새)</summary>
        int _tier;

        /// <summary>단계 이름 (구역 카드 진행 중 칩 · 테스트)</summary>
        internal static string TierLabel(int tier) => tier switch
        {
            1 => "길게",
            2 => "밤새",
            _ => "기본",
        };

        /// <summary>단계 보상 줄 '보상 ×1' / '보상 ×2.8' / '보상 ×5.4' (테스트용 순수 함수)</summary>
        internal static string TierScaleLabel(int tier) =>
            "보상 " + UIFormat.Times + ExpeditionSystem.RewardScale(tier).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// 처음 고를 단계 (테스트용 순수 함수): 지난번 단계가 고를 수 있으면 그것, 아니면 고를 수 있는 가장 낮은 단계 (없으면 0).
        /// allowedMask = 고를 수 있는 단계 비트 (1 &lt;&lt; 단계)
        /// </summary>
        internal static int DefaultTier(int lastTier, int allowedMask)
        {
            if (lastTier >= 0 && lastTier < ExpeditionSystem.DurationTierCount && (allowedMask & (1 << lastTier)) != 0) return lastTier;
            for (int tier = 0; tier < ExpeditionSystem.DurationTierCount; tier++)
            {
                if ((allowedMask & (1 << tier)) != 0) return tier;
            }
            return 0;
        }

        /// <summary>고를 수 있는 단계 비트 (1 &lt;&lt; 단계)</summary>
        static int AllowedMask(ExpeditionZoneDef zone)
        {
            int mask = 0;
            for (int tier = 0; tier < ExpeditionSystem.DurationTierCount; tier++)
            {
                if (Game.Expedition.IsTierAllowed(zone, tier)) mask |= 1 << tier;
            }
            return mask;
        }

        /// <summary>지금 고른 단계의 원정 시간 (분)</summary>
        int TierMinutes => _zone == null ? 0 : Game.Expedition.GetDurationMinutes(_zone, _tier);

        /// <summary>자동 선택 정렬 키 (테스트용 — 순수 비교)</summary>
        internal readonly struct AutoPickKey
        {
            public readonly bool Dispatched;
            /// <summary>진화 조건 '원정 N회' 중 가장 많이 남은 횟수 (없으면 0)</summary>
            public readonly int ExpeditionNeed;
            public readonly long Power;
            public readonly string Id;

            public AutoPickKey(bool dispatched, int expeditionNeed, long power, string id)
            {
                Dispatched = dispatched;
                ExpeditionNeed = expeditionNeed;
                Power = power;
                Id = id ?? string.Empty;
            }

            /// <summary>나가 있지 않은 친구 → '원정 N회'가 많이 남은 친구 → 전투력 높은 친구 → ID</summary>
            public static int Compare(AutoPickKey a, AutoPickKey b)
            {
                if (a.Dispatched != b.Dispatched) return a.Dispatched ? 1 : -1;
                if (a.ExpeditionNeed != b.ExpeditionNeed) return b.ExpeditionNeed.CompareTo(a.ExpeditionNeed);
                if (a.Power != b.Power) return b.Power.CompareTo(a.Power);
                return string.CompareOrdinal(a.Id, b.Id);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _members = new UIListPool<ExpeditionMemberCellView>(_memberPrefab, _memberContent);
            _drops = new UIListPool<RewardSlotView>(_dropPrefab, _dropContent);
            _autoButton.onClick.AddListener(() => AutoSelect(false));
            _startButton.onClick.AddListener(StartExpedition);
            for (int i = 0; i < _tierButtons.Length; i++)
            {
                int tier = i;
                if (_tierButtons[i] != null) _tierButtons[i].onClick.AddListener(() => SelectTier(tier));
            }
        }

        public void Show(ExpeditionZoneDef zone, Sprite art, Color fallbackColor)
        {
            _zone = zone;
            _selected.Clear();
            Open();

            _titleText.text = zone.name;
            _art.sprite = art;
            _art.color = art != null ? Color.white : fallbackColor;
            _artFallbackText.text = zone.place;
            _descText.text = string.IsNullOrEmpty(zone.description)
                ? $"{zone.place}에서 잊힌 물건들의 흔적을 찾아요."
                : zone.description;
            _infoText.text = $"{zone.place} · 소요 {MenuText.Minutes(zone.durationMinutes)} · 최대{UIFormat.Nbsp}{zone.maxMembers}명";
            _tier = DefaultTier(Game.Expedition.GetLastTier(zone.id), AllowedMask(zone));
            RefreshTiers();

            FillDrops();
            CollectLineages();
            // 지난번 친구 그대로 (모두 지금 갈 수 있을 때만), 아니면 조용히 자동 선택
            if (!PrefillLast()) AutoSelect(true);
            else RefreshMembers();
        }

        protected override void OnClose() => _zone = null;

        /// <summary>지난번 이 구역에 보낸 친구를 채운다. 한 명이라도 못 가면 채우지 않고 false</summary>
        bool PrefillLast()
        {
            var last = Game.Expedition.GetLastMembers(_zone.id);
            if (last.Count == 0 || last.Count > _zone.maxMembers) return false;
            for (int i = 0; i < last.Count; i++)
            {
                if (!CanSelect(last[i])) return false;
                for (int j = 0; j < i; j++)
                {
                    if (last[j] == last[i]) return false;
                }
            }
            for (int i = 0; i < last.Count; i++) _selected.Add(last[i]);
            return true;
        }

        /// <summary>보낼 수 있는 친구 먼저 (그 안의 순서는 보유 목록 순서 그대로) — 열 때 한 번</summary>
        void CollectLineages()
        {
            _lineages.Clear();
            var owned = Game.Characters.GetOwnedLineages();
            foreach (var lineage in owned)
            {
                if (CanSelect(lineage.id)) _lineages.Add(lineage);
            }
            foreach (var lineage in owned)
            {
                if (!_lineages.Contains(lineage)) _lineages.Add(lineage);
            }
        }

        void RefreshMembers()
        {
            int selectable = 0;
            _members.SetCount(_lineages.Count);
            for (int i = 0; i < _lineages.Count; i++)
            {
                var lineage = _lineages[i];
                bool canSelect = Game.Expedition.CanSelect(lineage.id, out var reason);
                if (canSelect) selectable++;
                _members[i].Set(lineage, canSelect, reason, _selected.IndexOf(lineage.id) + 1, PlacedLabel(lineage.id), Toggle);
            }
            _noMemberLabel.SetActive(_lineages.Count == 0);
            if (_hintText != null)
            {
                bool allAway = _lineages.Count > 0 && selectable == 0;
                _hintText.text = allAway ? HintAllAway : HintDefault;
                _hintText.color = allAway ? UITheme.Lamp : UITheme.SubText;
            }

            int max = _zone.maxMembers;
            _selectCountText.text = $"<color={UITheme.ColorTag(UITheme.Gold)}>{_selected.Count}</color>/{max}명";
            _startButton.interactable = _selected.Count > 0;
            _startLabel.text = _selected.Count > 0 ? $"출발 ({MenuText.Minutes(TierMinutes)})" : "친구를 골라 주세요";
        }

        /// <summary>다녀올 시간 칸 (열 때 · 고를 때만): 고를 수 없는 단계는 숨기고, 고를 수 있는 단계가 하나뿐이면 줄을 숨긴다</summary>
        void RefreshTiers()
        {
            if (_tierRow == null || _zone == null) return;
            int mask = AllowedMask(_zone);
            int allowed = 0;
            for (int tier = 0; tier < ExpeditionSystem.DurationTierCount; tier++)
            {
                if ((mask & (1 << tier)) != 0) allowed++;
            }
            bool show = allowed >= 2;
            if (_tierRow.activeSelf != show) _tierRow.SetActive(show);
            if (!show) return;
            for (int i = 0; i < _tierButtons.Length; i++)
            {
                var button = _tierButtons[i];
                if (button == null) continue;
                bool on = (mask & (1 << i)) != 0;
                if (button.gameObject.activeSelf != on) button.gameObject.SetActive(on);
                if (!on) continue;
                bool selected = i == _tier;
                var time = i < _tierTimeTexts.Length ? _tierTimeTexts[i] : null;
                if (time != null) time.text = MenuText.Minutes(Game.Expedition.GetDurationMinutes(_zone, i));
                var scale = i < _tierScaleTexts.Length ? _tierScaleTexts[i] : null;
                if (scale != null)
                {
                    scale.text = TierScaleLabel(i);
                    scale.color = selected ? UITheme.Cream : UITheme.SubText;
                }
                TabStyle.Apply(button.targetGraphic, time, selected, TabVariant.Option);
            }
        }

        void SelectTier(int tier)
        {
            if (_zone == null || tier == _tier || !Game.Expedition.IsTierAllowed(_zone, tier)) return;
            _tier = tier;
            UISfx.Play(UISfxKey.Tab);
            RefreshTiers();
            RefreshMembers();
        }

        /// <summary>편성 중이면 '지킴이' / '잔향이' (둘 다면 '지킴이·잔향이'), 아니면 null</summary>
        static string PlacedLabel(string lineageId)
        {
            var formation = Game.Formation;
            var db = Game.Characters.Db;
            bool guardian = false;
            bool echo = false;
            for (int i = 0; i < BattleGeometry.SlotCount && !guardian; i++)
            {
                var form = formation.GetGuardian(i);
                guardian = form != null && db.GetLineageOfForm(form)?.id == lineageId;
            }
            for (int i = 0; i < FormationSave.EchoSlots && !echo; i++)
            {
                var form = formation.GetEcho(i);
                echo = form != null && db.GetLineageOfForm(form)?.id == lineageId;
            }
            if (guardian && echo) return "지킴이·잔향이";
            if (guardian) return "지킴이";
            return echo ? "잔향이" : null;
        }

        void Toggle(string lineageId)
        {
            if (_zone == null) return;
            if (_selected.Remove(lineageId))
            {
                RefreshMembers();
                return;
            }
            if (!Game.Expedition.CanSelect(lineageId, out var reason))
            {
                UIManager.Instance.ShowToast(string.IsNullOrEmpty(reason) ? "지금은 보낼 수 없어요." : reason);
                return;
            }
            if (_selected.Count >= _zone.maxMembers)
            {
                UIManager.Instance.ShowToast($"이 원정에는 최대 {_zone.maxMembers}명까지 갈 수 있어요.");
                return;
            }
            _selected.Add(lineageId);
            RefreshMembers();
        }

        /// <summary>
        /// 남은 자리를 보낼 수 있는 친구로 채운다 (AutoPickKey 순서). silent면 토스트 없이 (열 때),
        /// 버튼으로 눌렀는데 아무도 못 고르면 토스트 하나.
        /// </summary>
        void AutoSelect(bool silent)
        {
            if (_zone == null) return;
            _candidates.Clear();
            _keys.Clear();
            foreach (var lineage in _lineages)
            {
                if (_selected.Contains(lineage.id) || !CanSelect(lineage.id)) continue;
                _candidates.Add(lineage);
                _keys.Add(KeyOf(lineage));
            }
            // 키와 함께 정렬 (후보는 많아야 수십 명 — 삽입 정렬)
            for (int i = 1; i < _candidates.Count; i++)
            {
                var key = _keys[i];
                var lineage = _candidates[i];
                int j = i - 1;
                while (j >= 0 && AutoPickKey.Compare(_keys[j], key) > 0)
                {
                    _keys[j + 1] = _keys[j];
                    _candidates[j + 1] = _candidates[j];
                    j--;
                }
                _keys[j + 1] = key;
                _candidates[j + 1] = lineage;
            }
            foreach (var lineage in _candidates)
            {
                if (_selected.Count >= _zone.maxMembers) break;
                _selected.Add(lineage.id);
            }
            if (_selected.Count == 0 && !silent) UIManager.Instance.ShowToast(HintAllAway);
            RefreshMembers();
        }

        static AutoPickKey KeyOf(LineageDef lineage) =>
            new(Game.Formation.IsDispatched(lineage.id), ExpeditionNeed(lineage), Game.Power.GetLineagePower(lineage.id), lineage.id);

        /// <summary>아직 열지 않은 형태의 진화 조건 '원정 N회' 중 가장 많이 남은 횟수 (원정을 보내면 채워지는 것)</summary>
        static int ExpeditionNeed(LineageDef lineage)
        {
            if (lineage.forms == null || Game.Evolution == null) return 0;
            int need = 0;
            foreach (var form in lineage.forms)
            {
                if (form == null || form.conditions == null || Game.Evolution.GetState(form.id) == EvolutionState.Unlocked) continue;
                for (int i = 0; i < form.conditions.Count; i++)
                {
                    var condition = form.conditions[i];
                    if (condition == null || condition.type != ProgressType.ExpeditionCount) continue;
                    need = Mathf.Max(need, condition.target - Game.Evolution.GetConditionProgress(form.id, i));
                }
            }
            return need;
        }

        void StartExpedition()
        {
            if (_zone == null) return;
            if (_selected.Count == 0)
            {
                UIManager.Instance.ShowToast("함께 갈 친구를 골라 주세요.");
                return;
            }
            var zone = _zone;
            int minutes = TierMinutes;
            // 단계 0은 예전 TryStart와 같다 (사다리가 생기기 전 스텁도 0단계는 그대로 보낸다)
            if (!Game.Expedition.TryStart(zone.id, _selected, _tier))
            {
                UIManager.Instance.ShowToast("지금은 출발할 수 없어요. 진행 중인 원정과 친구 상태를 확인해 주세요.");
                return;
            }
            Close();
            UISfx.Play(UISfxKey.ExpeditionDepart);
            UIManager.Instance.ShowToast($"{MenuText.Toward(zone.place)} 친구들이 출발했어요! ({MenuText.Minutes(minutes)})", ToastKind.Success);
        }

        void FillDrops()
        {
            int dropCount = _zone.drops != null ? _zone.drops.Count : 0;
            bool hasGold = _zone.goldReward > 0;
            _drops.SetCount(dropCount + (hasGold ? 2 : 0));
            int index = 0;
            for (int i = 0; i < dropCount; i++)
            {
                var def = Game.Items.Get(_zone.drops[i].itemId);
                if (def == null) continue;
                _drops[index].SetItem(def, ExpeditionZoneCardView.DropLabel(_zone.drops[i]));
                _drops[index].SetItemInfoClick();
                index++;
            }
            if (hasGold)
            {
                index = ExpeditionZoneCardView.SetIncomeDrops(_drops, index, _zone.goldReward);
            }
            _drops.SetCount(index);
        }

        static bool CanSelect(string lineageId) => Game.Expedition.CanSelect(lineageId, out _);
    }
}
