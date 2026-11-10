using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 편성 화면 오른쪽 "팀 시너지" 칸. 켜진 시너지(특성/역할/조합) + 계보 공명 + 한 명만 더 있으면 켜지는 시너지(흐리게).
    /// 카드를 끌어 칸 위에 올리면 놓았을 때의 시너지를 미리 보여주고(SynergySystem.PreviewPlace),
    /// 새로 켜지거나 강해지는 시너지는 금빛으로, 꺼지는 시너지는 붉게 표시한다.
    /// 계보 공명은 SynergySystem이 계산하지 않으므로(FormationSystem 담당) 같은 배치 규칙으로 여기서 따로 미리 본다.
    /// 칸 위가 아니면 끌고 있는 친구가 들어가는 시너지에 금빛 테두리를 둘러 준다.
    /// 배치 후 새로 켜진 시너지는 잠깐 "발동" 배지와 함께 톡 튄다.
    /// 계산(Recalculate)과 그리기(ShowFocus/ShowPreview)는 FormationPanel이 부를 때만 한다 (매 프레임 아님).
    /// </summary>
    public class SynergyPanelView : MonoBehaviour
    {
        /// <summary>꺼진 시너지는 이만큼만 (한 명만 더 있으면 켜지는 것)</summary>
        const int MaxCloseRows = 3;

        [SerializeField] TMP_Text _titleText;
        [SerializeField] Button _codexButton;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        [SerializeField] SynergyRowView _rowPrefab;
        [SerializeField] TMP_Text _emptyText;
        [Tooltip("미리보기 중 칸 테두리 발광")]
        [SerializeField] GameObject _previewGlow;

        struct Entry
        {
            public SynergyStatus Status;
            public SynergyStatus Before;
            public SynergyDelta Delta;
            public bool Related;
            public SynergyInfo Resonance;
        }

        readonly List<Entry> _entries = new();
        readonly List<Entry> _gains = new();
        readonly Dictionary<string, SynergyStatus> _currentById = new();
        readonly HashSet<string> _lastActive = new();
        readonly HashSet<string> _justActivated = new();
        bool _hasLast;
        /// <summary>현재 상태 사본 (시스템이 목록/객체를 재사용해도 미리보기와 비교할 수 있게 값을 복사해 둔다)</summary>
        readonly List<SynergyStatus> _current = new();
        readonly List<SynergyStatus> _statusPool = new();
        List<SynergyInfo> _resonance = new();
        /// <summary>지난 계산의 계보 공명 계보 ID (배치 후 새로 생긴 공명 축하용)</summary>
        readonly HashSet<string> _lastResonance = new();
        readonly HashSet<string> _justResonance = new();
        // 계보 공명 미리보기용 재사용 버퍼 (놓았다고 가정한 편성 사본 + 놓은 뒤 공명 계보)
        readonly string[] _previewGuardians = new string[FormationSystem.GuardianSlotCount];
        readonly string[] _previewEchoes = new string[FormationSystem.EchoSlotCount];
        readonly List<LineageDef> _previewResonance = new();
        /// <summary>미리보기에서 새로 생기는 공명 줄 (계보 ID → 재사용 객체)</summary>
        readonly Dictionary<string, SynergyInfo> _previewInfos = new();
        UIListPool<SynergyRowView> _rows;
        string _focusForm;

        /// <summary>지금 켜진 특성 시너지 (비트 = TraitType) — 편성 카드 특성 칩을 밝힐 때 쓴다</summary>
        public int ActiveTraitMask { get; private set; }

        public int ActiveCount { get; private set; }

        /// <summary>마지막 미리보기에서 새로 켜지거나 강해지는 시너지 수 (미리보기가 아니면 0)</summary>
        public int PreviewGains { get; private set; }

        /// <summary>마지막 미리보기에서 꺼지거나 약해지는 시너지 수</summary>
        public int PreviewLosses { get; private set; }

        void Awake()
        {
            _rows = new UIListPool<SynergyRowView>(_rowPrefab, _content);
            _codexButton.onClick.AddListener(() => UINav.OpenSynergy(null));
            if (_previewGlow != null) _previewGlow.SetActive(false);
        }

        // ───────────── 외부 호출 ─────────────

        /// <summary>현재 편성으로 다시 계산한다. 그리기는 이어서 ShowFocus/ShowPreview로.</summary>
        public void Recalculate()
        {
            if (!Game.IsReady) return;
            Snapshot(Game.Synergy.GetStatuses(true));
            _resonance = Game.Formation.GetSynergies() ?? new List<SynergyInfo>();

            _currentById.Clear();
            _justActivated.Clear();
            ActiveTraitMask = 0;
            ActiveCount = 0;
            foreach (var status in _current)
            {
                if (status?.Def == null) continue;
                _currentById[status.Def.id] = status;
                if (!status.IsActive) continue;
                ActiveCount++;
                if (status.Def.kind == SynergyKind.Trait) ActiveTraitMask |= 1 << (int)status.Def.trait;
                if (_hasLast && !_lastActive.Contains(status.Def.id)) _justActivated.Add(status.Def.id);
            }

            _lastActive.Clear();
            foreach (var status in _current)
            {
                if (status?.Def != null && status.IsActive) _lastActive.Add(status.Def.id);
            }
            TrackResonance();
            _hasLast = true;
        }

        /// <summary>새로 생긴 계보 공명을 기억해 둔다 (배치 직후 시너지와 같이 톡 튀도록)</summary>
        void TrackResonance()
        {
            _justResonance.Clear();
            foreach (var info in _resonance)
            {
                var id = info?.Lineage?.id;
                if (id != null && _hasLast && !_lastResonance.Contains(id)) _justResonance.Add(id);
            }
            _lastResonance.Clear();
            foreach (var info in _resonance)
            {
                var id = info?.Lineage?.id;
                if (id != null) _lastResonance.Add(id);
            }
        }

        void Snapshot(List<SynergyStatus> source)
        {
            _current.Clear();
            if (source == null) return;
            foreach (var src in source)
            {
                if (src?.Def == null) continue;
                if (_statusPool.Count <= _current.Count) _statusPool.Add(new SynergyStatus());
                var copy = _statusPool[_current.Count];
                copy.Def = src.Def;
                copy.Count = src.Count;
                copy.TierIndex = src.TierIndex;
                copy.NextTier = src.NextTier;
                copy.MemberFormIds.Clear();
                if (src.MemberFormIds != null) copy.MemberFormIds.AddRange(src.MemberFormIds);
                _current.Add(copy);
            }
        }

        /// <summary>이 칸에 이 친구를 놓았을 때의 시너지를 보여준다</summary>
        public void ShowPreview(CharacterRole role, int slot, string formId)
        {
            if (!Game.IsReady) return;
            _focusForm = formId;
            var preview = Game.Synergy.PreviewPlace(role, slot, formId) ?? _current;

            _entries.Clear();
            _gains.Clear();
            int closeRows = 0;
            int gained = 0;
            int lost = 0;
            foreach (var status in preview)
            {
                if (status?.Def == null) continue;
                _currentById.TryGetValue(status.Def.id, out var before);
                var delta = SynergyUI.Compare(before, status);
                bool close = SynergyUI.IsClose(status);
                if (!status.IsActive && delta == SynergyDelta.None && !(close && closeRows < MaxCloseRows)) continue;
                if (!status.IsActive && close) closeRows++;
                var entry = new Entry { Status = status, Before = before, Delta = delta, Related = IsRelated(status.Def) };
                // 새로 켜지거나 강해지는 시너지를 맨 위로
                if (SynergyUI.IsGain(delta))
                {
                    _gains.Add(entry);
                    gained++;
                }
                else
                {
                    if (SynergyUI.IsLoss(delta)) lost++;
                    _entries.Add(entry);
                }
            }
            // 꺼지는 시너지도 보여준다 (미리보기 목록에 아예 없을 때)
            foreach (var before in _current)
            {
                if (before?.Def == null || !before.IsActive || ContainsDef(preview, before.Def.id)) continue;
                _entries.Add(new Entry { Status = new SynergyStatus { Def = before.Def }, Before = before, Delta = SynergyDelta.Lost });
                lost++;
            }

            // 계보 공명: 놓은 뒤 지킴이 + 잔향이 양쪽에 있는 계보와 지금을 비교
            CollectPreviewResonance(role, slot, formId);
            foreach (var lineage in _previewResonance)
            {
                if (FindResonance(lineage) != null) continue;
                _gains.Add(new Entry { Resonance = PreviewInfo(lineage), Delta = SynergyDelta.Activated, Related = IsRelatedLineage(lineage) });
                gained++;
            }
            _entries.InsertRange(0, _gains);
            lost += AppendResonance(true);
            PreviewGains = gained;
            PreviewLosses = lost;

            _titleText.text = PreviewTitle(gained, lost);
            if (_previewGlow != null) _previewGlow.SetActive(true);
            Draw(true);
        }

        /// <summary>"놓으면 시너지 +2 · -1" / "놓으면 변화 없음"</summary>
        static string PreviewTitle(int gained, int lost)
        {
            string gold = UITheme.ColorTag(UITheme.Gold);
            string head = $"<color={gold}>놓으면</color> <size=78%>";
            if (gained <= 0 && lost <= 0) return head + $"<color={UITheme.ColorTag(UITheme.SubText)}>변화 없음</color></size>";
            string red = UITheme.ColorTag(UITheme.Negative);
            if (gained > 0 && lost > 0) return head + $"<color={gold}>시너지 +{gained}</color> <color={red}>-{lost}</color></size>";
            return gained > 0
                ? head + $"<color={gold}>시너지 +{gained}</color></size>"
                : head + $"<color={red}>시너지 -{lost}</color></size>";
        }

        // ───────────── 계보 공명 미리보기 ─────────────

        /// <summary>
        /// 이 칸에 놓았다고 가정한 편성에서 계보 공명이 생기는 계보를 모은다.
        /// FormationSystem.TryPlace와 같은 규칙: 같은 쪽에 이미 있으면 두 칸을 맞바꾸고(빈칸이면 이동), 아니면 칸의 형태를 밀어낸다.
        /// 배치할 수 없는 칸이면 지금 편성 그대로 (SynergySystem.PreviewPlace와 같은 기준).
        /// 공명 판정은 FormationSystem.GetSynergies와 같다 (잠긴 덱 칸은 GetEcho가 null이라 세지 않는다).
        /// </summary>
        void CollectPreviewResonance(CharacterRole role, int slot, string formId)
        {
            _previewResonance.Clear();
            var formation = Game.Formation;
            for (int i = 0; i < _previewGuardians.Length; i++) _previewGuardians[i] = formation.GetGuardian(i);
            for (int i = 0; i < _previewEchoes.Length; i++) _previewEchoes[i] = formation.GetEcho(i);

            if (formation.CanPlace(role, slot, formId) == PlaceResult.Ok)
            {
                var side = role == CharacterRole.Guardian ? _previewGuardians : _previewEchoes;
                if (side[slot] != formId)
                {
                    int from = System.Array.IndexOf(side, formId);
                    if (from >= 0) side[from] = side[slot];
                    side[slot] = formId;
                }
            }

            var db = Game.Characters.Db;
            foreach (var guardian in _previewGuardians)
            {
                var lineage = string.IsNullOrEmpty(guardian) ? null : db.GetLineageOfForm(guardian);
                if (lineage == null || _previewResonance.Contains(lineage) || !EchoesHave(db, lineage.id)) continue;
                _previewResonance.Add(lineage);
            }
        }

        bool EchoesHave(CharacterDatabase db, string lineageId)
        {
            foreach (var echo in _previewEchoes)
            {
                if (!string.IsNullOrEmpty(echo) && db.GetLineageOfForm(echo)?.id == lineageId) return true;
            }
            return false;
        }

        bool PreviewHasResonance(LineageDef lineage) => lineage != null && _previewResonance.Contains(lineage);

        /// <summary>지금 켜진 공명 중 이 계보 (없으면 null)</summary>
        SynergyInfo FindResonance(LineageDef lineage)
        {
            foreach (var info in _resonance)
            {
                if (info?.Lineage != null && info.Lineage.id == lineage.id) return info;
            }
            return null;
        }

        /// <summary>놓으면 새로 생기는 공명 줄 (FormationSystem.GetSynergies와 같은 수치, 객체는 계보마다 재사용)</summary>
        SynergyInfo PreviewInfo(LineageDef lineage)
        {
            if (!_previewInfos.TryGetValue(lineage.id, out var info))
            {
                info = new SynergyInfo();
                _previewInfos[lineage.id] = info;
            }
            int level = Mathf.Max(0, Game.Characters.GetResonanceLevel(lineage.id));
            info.Lineage = lineage;
            info.ResonanceLevel = level;
            info.AttackBonusPercent = FormationSystem.SynergyBaseBonusPercent + FormationSystem.SynergyBonusPerResonance * level;
            info.Label = $"{lineage.name} 계보 공명 Lv.{level}";
            return info;
        }

        /// <summary>미리보기 없이, 이 친구가 들어가는 시너지만 금빛 테두리로 강조 (null = 강조 없음)</summary>
        public void ShowFocus(string formId)
        {
            _focusForm = formId;
            PreviewGains = 0;
            PreviewLosses = 0;
            ShowCurrent();
        }

        public void ClearPreview() => ShowFocus(null);

        // ───────────── 그리기 ─────────────

        void ShowCurrent()
        {
            _entries.Clear();
            int closeRows = 0;
            foreach (var status in _current)
            {
                if (status?.Def == null || !status.IsActive) continue;
                _entries.Add(new Entry { Status = status, Related = IsRelated(status.Def) });
            }
            AppendResonance(false);
            foreach (var status in _current)
            {
                if (closeRows >= MaxCloseRows) break;
                if (status?.Def == null || !SynergyUI.IsClose(status)) continue;
                closeRows++;
                _entries.Add(new Entry { Status = status, Related = IsRelated(status.Def) });
            }

            int total = ActiveCount + _resonance.Count;
            string countText = total > 0 ? $" <size=80%><color={UITheme.ColorTag(UITheme.Gold)}>{total}</color></size>" : string.Empty;
            _titleText.text = "팀 시너지" + countText;
            if (_previewGlow != null) _previewGlow.SetActive(false);
            Draw(false);

            // 방금 새로 켜진 시너지 / 계보 공명 축하 (한 번만)
            if (_justActivated.Count == 0 && _justResonance.Count == 0) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var def = entry.Status?.Def;
                var lineageId = entry.Resonance?.Lineage?.id;
                bool fresh = def != null ? _justActivated.Contains(def.id) : lineageId != null && _justResonance.Contains(lineageId);
                if (fresh) _rows[i].Celebrate();
            }
            _justActivated.Clear();
            _justResonance.Clear();
        }

        /// <summary>지금 켜진 계보 공명 줄을 붙인다. preview면 놓았을 때 사라지는 공명을 붉게 표시하고 그 수를 돌려준다</summary>
        int AppendResonance(bool preview)
        {
            int lost = 0;
            foreach (var info in _resonance)
            {
                if (info?.Lineage == null) continue;
                bool gone = preview && !PreviewHasResonance(info.Lineage);
                if (gone) lost++;
                _entries.Add(new Entry
                {
                    Resonance = info,
                    Delta = gone ? SynergyDelta.Lost : SynergyDelta.None,
                    Related = IsRelatedLineage(info.Lineage),
                });
            }
            return lost;
        }

        void Draw(bool resetScroll)
        {
            var stage = SynergyUI.CurrentStageModifier();
            _rows.SetCount(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Resonance != null) _rows[i].SetResonance(entry.Resonance, entry.Delta, entry.Related);
                else _rows[i].Set(entry.Status, entry.Before, entry.Delta, entry.Related, stage);
            }

            bool empty = _entries.Count == 0;
            _emptyText.gameObject.SetActive(empty);
            if (empty)
            {
                _emptyText.text = _focusForm != null
                    ? "이 친구를 놓아도 아직 켜지는 시너지가 없어요."
                    : "같은 특성이나 역할의 친구를 함께 편성하면 시너지가 켜져요.\n<size=85%>같은 계보를 지킴이와 잔향이로 함께 두면 계보 공명!</size>";
            }
            if (resetScroll && _scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        bool IsRelated(SynergyDef def) => _focusForm != null && SynergyUI.Involves(def, _focusForm);

        /// <summary>끌고 있는/고른 친구가 이 계보인지 (계보 공명 줄 강조용)</summary>
        bool IsRelatedLineage(LineageDef lineage)
        {
            if (_focusForm == null || lineage == null || !Game.IsReady) return false;
            return Game.Characters.Db.GetLineageOfForm(_focusForm)?.id == lineage.id;
        }

        static bool ContainsDef(List<SynergyStatus> statuses, string id)
        {
            foreach (var status in statuses)
            {
                if (status?.Def != null && status.Def.id == id) return true;
            }
            return false;
        }
    }
}
