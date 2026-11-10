using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IdleGame
{
    /// <summary>시너지 하나의 현재 상태 (편성 화면/도감/전투 시작 연출용)</summary>
    public sealed class SynergyStatus
    {
        public SynergyDef Def;
        /// <summary>현재 수 (Trait/Style: 해당 유닛 수, Combo: 편성된 조합 계보 수)</summary>
        public int Count;
        /// <summary>켜진 단계 인덱스 (-1 = 꺼짐)</summary>
        public int TierIndex = -1;
        /// <summary>다음 단계 (없으면 null) — "2/3" 같은 진행 표시용</summary>
        public SynergyTier NextTier;
        /// <summary>시너지에 기여하는 편성 형태 ID</summary>
        public List<string> MemberFormIds = new();

        /// <summary>다음 단계에 필요한 수 (다음 단계가 없으면 0). 조합은 조합 계보 수</summary>
        public int NextCount;
        /// <summary>가장 높은 단계에 필요한 수 (단계 눈금 표시용). 조합은 조합 계보 수</summary>
        public int MaxCount;
        /// <summary>시너지에 기여하는 계보 ID (중복 없음, 편성 순서)</summary>
        public List<string> MemberLineageIds = new();
        /// <summary>조합 전용: 아직 편성되지 않은 조합 계보 ID (Trait/Style은 비어 있음)</summary>
        public List<string> MissingLineageIds = new();
        /// <summary>
        /// (호환용, 지금은 항상 비어 있음) 구성원이 아닌데도 구성원 전용 효과를 함께 받는 편성 형태 ID.
        /// 예전에는 전투 능력치가 계보 단위라서 역할 시너지 구성원과 같은 계보의 다른 성향 형태(지킴이↔잔향이)도 함께 받았다.
        /// 이제 역할 시너지의 구성원 전용 효과는 형태 단위(BattleModifiers.Form / FormBonusSkills)로 들어가 구성원 형태만 받고,
        /// 특성/조합은 계보 단위로 세므로 계보의 형태가 모두 구성원이다. 그래서 효과를 받는 형태 = MemberFormIds 그대로다.
        /// </summary>
        public List<string> SharedFormIds = new();

        /// <summary>정렬용: 데이터베이스 안의 순서</summary>
        internal int Order;

        public bool IsActive => TierIndex >= 0;
        /// <summary>(호환용, 지금은 항상 false) 구성원이 아닌 형태도 구성원 전용 효과를 함께 받는지</summary>
        public bool HasSharedForms => SharedFormIds.Count > 0;
        public SynergyTier ActiveTier => IsActive && Def != null && TierIndex < Def.tiers.Count ? Def.tiers[TierIndex] : null;
        /// <summary>가장 높은 단계까지 켜졌는지</summary>
        public bool IsMaxed => IsActive && NextTier == null;
        /// <summary>다음 단계까지 남은 수 (최고 단계면 0)</summary>
        public int Remaining => NextTier == null ? 0 : Math.Max(0, NextCount - Count);
        /// <summary>진행 표시. 예: "2/3" (최고 단계면 "3/3")</summary>
        public string ProgressText => $"{Count}/{(NextTier != null ? NextCount : MaxCount)}";
    }

    /// <summary>
    /// 특성/성향/조합 시너지 계산. 편성(FormationSystem)을 보고 켜진 시너지와 전투 능력치(BattleModifiers)를 만든다.
    /// 유닛 수는 편성된 형태 단위로 센다 (같은 계보의 지킴이 + 잔향이 = 2). 잠긴 덱 칸은 세지 않는다.
    /// 계보 공명(FormationSystem.TeamAttackBonusPercent)은 지금처럼 BattleSetup.AttackMultiplier로 따로 들어가므로 여기서는 더하지 않는다.
    /// </summary>
    public class SynergySystem
    {
        /// <summary>편성/계보 변화로 시너지 상태가 바뀔 수 있을 때</summary>
        public event Action Changed;

        static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;
        static readonly Comparison<SynergyStatus> StatusOrder = CompareStatuses;

        readonly PlayerData _data;
        readonly SynergyDatabase _db;
        readonly CharacterSystem _characters;
        readonly FormationSystem _formation;

        // 계산용 재사용 버퍼 (편성 화면 드래그 미리보기가 자주 불러도 쓰레기가 덜 생기게)
        readonly string[] _guardians = new string[FormationSystem.GuardianSlotCount];
        readonly string[] _echoes = new string[FormationSystem.EchoSlotCount];
        readonly List<PlacedUnit> _placed = new();

        /// <summary>편성된 형태 하나 (특성은 비트 마스크로 중복 없이)</summary>
        readonly struct PlacedUnit
        {
            public readonly CharacterFormDef Form;
            public readonly LineageDef Lineage;
            public readonly int TraitMask;

            public PlacedUnit(CharacterFormDef form, LineageDef lineage, int traitMask)
            {
                Form = form;
                Lineage = lineage;
                TraitMask = traitMask;
            }
        }

        public SynergySystem(PlayerData data, SynergyDatabase db, CharacterSystem characters, FormationSystem formation)
        {
            _data = data;
            _db = db;
            _characters = characters;
            _formation = formation;
            if (_formation != null) _formation.Changed += NotifyChanged;
            if (_characters != null) _characters.Changed += NotifyChanged;
        }

        public SynergyDatabase Db => _db;

        /// <summary>
        /// 시너지 밖에서 팀 전체에 더하는 능력치 (4차 훅, SPEC4 §2.5 — 도감 영구 모음 보너스가 넣는다). BuildModifiers 끝에서 result.Team에 불린다
        /// (잔향이 던전용 echoesOnly 포함). null = 없음 (지금). 바뀌면 넣는 쪽이 MarkDirty를 불러 전투력 · 전투가 다시 계산되게 한다
        /// </summary>
        public Action<StatBlock> ExtraTeamModifiers;

        /// <summary>시너지 밖의 값(ExtraTeamModifiers)이 바뀌었음을 알린다 → Changed (전투력 더러움 · 다음 전투 구성에 반영)</summary>
        public void MarkDirty() => NotifyChanged();

        void NotifyChanged() => Changed?.Invoke();

        // ───────────────────────────── 상태 ─────────────────────────────

        /// <summary>모든 시너지 상태 (켜진 것 먼저, 그다음 진행도 높은 순). includeInactive=false면 켜진 것만</summary>
        public List<SynergyStatus> GetStatuses(bool includeInactive = true)
        {
            if (!CanEvaluate) return new List<SynergyStatus>();
            CopyFormation(includeGuardians: true);
            return Evaluate(includeInactive);
        }

        public List<SynergyStatus> GetActive() => GetStatuses(false);

        /// <summary>이 형태를 이 칸에 놓았다고 가정한 시너지 상태 (드래그 중 미리보기). 배치 불가면 현재 상태</summary>
        public List<SynergyStatus> PreviewPlace(CharacterRole role, int slot, string formId)
        {
            if (!CanEvaluate) return new List<SynergyStatus>();
            if (_formation.CanPlace(role, slot, formId) != PlaceResult.Ok) return GetStatuses();

            CopyFormation(includeGuardians: true);
            // FormationSystem.TryPlace와 같은 규칙: 같은 쪽에 이미 있으면 두 칸을 맞바꾸고(빈칸이면 이동), 아니면 칸의 형태를 밀어낸다
            var side = role == CharacterRole.Guardian ? _guardians : _echoes;
            if (side[slot] != formId)
            {
                int from = Array.IndexOf(side, formId);
                if (from >= 0) side[from] = side[slot];
                side[slot] = formId;
            }
            return Evaluate(includeInactive: true);
        }

        /// <summary>이 계보가 관여하는 시너지 (특성/성향/조합)</summary>
        public List<SynergyDef> GetSynergiesFor(string lineageId)
        {
            var lineage = HasDatabase && _characters?.Db != null && !string.IsNullOrEmpty(lineageId) ? _characters.Db.GetLineage(lineageId) : null;
            return CollectInvolved(lineage, null);
        }

        /// <summary>이 형태가 편성되면 기여하는 시너지 (계보 특성 + 이 형태의 성향 + 계보 조합). 캐릭터 카드/상세용</summary>
        public List<SynergyDef> GetSynergiesForForm(string formId)
        {
            var db = HasDatabase ? _characters?.Db : null;
            var form = db != null && !string.IsNullOrEmpty(formId) ? db.GetForm(formId) : null;
            return CollectInvolved(form != null ? db.GetLineageOfForm(formId) : null, form);
        }

        /// <summary>특성 → 역할 → 조합 순 (같은 종류 안에서는 데이터 순서). form이 null이면 계보의 모든 형태 성향을 본다</summary>
        List<SynergyDef> CollectInvolved(LineageDef lineage, CharacterFormDef form)
        {
            var result = new List<SynergyDef>();
            if (lineage == null) return result;

            int traitMask = TraitMaskOf(lineage);
            for (int kind = (int)SynergyKind.Trait; kind <= (int)SynergyKind.Combo; kind++)
            {
                foreach (var def in _db.synergies)
                {
                    if (!IsUsable(def) || (int)def.kind != kind) continue;
                    if (Involves(def, lineage, form, traitMask)) result.Add(def);
                }
            }
            return result;
        }

        static bool Involves(SynergyDef def, LineageDef lineage, CharacterFormDef onlyForm, int traitMask)
        {
            switch (def.kind)
            {
                case SynergyKind.Trait:
                    return (traitMask & TraitBit(def.trait)) != 0;
                case SynergyKind.Style:
                    if (onlyForm != null) return onlyForm.style == def.style;
                    if (lineage.forms == null) return false;
                    foreach (var form in lineage.forms)
                        if (form != null && form.style == def.style) return true;
                    return false;
                case SynergyKind.Combo:
                    return def.lineageIds.Contains(lineage.id);
                default:
                    return false;
            }
        }

        // ───────────────────────────── 전투 능력치 ─────────────────────────────

        /// <summary>
        /// 현재 편성으로 전투 능력치를 만든다. stageModifier가 시너지의 favoredModifier와 같으면 배율 적용.
        /// echoesOnly = true면 잔향이 덱만 센다 (잔향이 던전).
        /// membersOnly 단계: 특성/조합은 구성원 계보의 Lineage 칸(계보의 모든 형태가 구성원), 역할은 구성원 형태의 Form 칸
        /// (같은 계보라도 성향이 다른 반대편 형태는 받지 않는다). 유닛 하나의 값은 BattleModifiers.For(계보, 형태).
        /// bonusSkill은 배율과 상관없이 같은 범위로 한 번씩 들어간다: 특성/조합 → BonusSkills[계보], 역할 → FormBonusSkills[형태].
        /// </summary>
        public BattleModifiers BuildModifiers(StageModifierType stageModifier = StageModifierType.None, bool echoesOnly = false)
        {
            var result = new BattleModifiers();
            if (!CanEvaluate)
            {
                ExtraTeamModifiers?.Invoke(result.Team);
                return result;
            }

            CopyFormation(includeGuardians: !echoesOnly);
            var db = _characters.Db;
            var active = Evaluate(includeInactive: false);
            foreach (var status in active)
            {
                var tier = status.ActiveTier;
                if (tier == null) continue;
                result.ActiveSynergies.Add(new KeyValuePair<string, int>(status.Def.id, status.TierIndex));

                float multiplier = FavoredMultiplier(status.Def, stageModifier);
                if (tier.modifiers != null)
                {
                    foreach (var modifier in tier.modifiers)
                    {
                        if (!IsUsable(modifier)) continue;
                        float value = modifier.value * multiplier;
                        if (value == 0f) continue;
                        if (tier.membersOnly) AddMemberStat(db, result, status, modifier.stat, value);
                        else result.Team.Add(modifier.stat, value);
                    }
                }

                var skill = tier.bonusSkill;
                if (skill != null && !skill.IsEmpty) AddMemberSkill(db, result, status, skill);
            }
            ExtraTeamModifiers?.Invoke(result.Team);
            return result;
        }

        /// <summary>구성원 전용 효과가 형태 단위로 들어가는 시너지인지 (역할 시너지 — 성향은 형태마다 다르다)</summary>
        static bool IsFormScoped(SynergyDef def) => def != null && def.kind == SynergyKind.Style;

        /// <summary>
        /// 구성원 전용 능력치를 더한다: 역할 시너지는 구성원 형태의 Form 칸(형태마다 한 번), 특성/조합은 구성원 계보의 Lineage 칸(계보마다 한 번).
        /// </summary>
        static void AddMemberStat(CharacterDatabase db, BattleModifiers result, SynergyStatus status, StatType stat, float value)
        {
            if (IsFormScoped(status.Def))
            {
                var forms = status.MemberFormIds;
                for (int i = 0; i < forms.Count; i++)
                {
                    string formId = forms[i];
                    if (string.IsNullOrEmpty(formId) || forms.IndexOf(formId) < i) continue; // 같은 형태는 한 번만
                    result.GetOrCreateForm(formId, LineageIdOf(db, formId)).Add(stat, value);
                }
                return;
            }
            foreach (var lineageId in status.MemberLineageIds) result.GetOrCreate(lineageId).Add(stat, value);
        }

        /// <summary>보너스 스킬을 넣는다: 역할 시너지는 구성원 형태마다, 특성/조합은 구성원 계보마다 한 번 (계보에 형태가 둘이어도 한 번)</summary>
        static void AddMemberSkill(CharacterDatabase db, BattleModifiers result, SynergyStatus status, SkillDef skill)
        {
            if (IsFormScoped(status.Def))
            {
                foreach (var formId in status.MemberFormIds)
                {
                    if (string.IsNullOrEmpty(formId)) continue;
                    result.AddFormBonusSkill(formId, skill);
                    string lineageId = LineageIdOf(db, formId);
                    if (lineageId != null) result.FormLineage[formId] = lineageId;
                }
                return;
            }
            foreach (var lineageId in status.MemberLineageIds)
            {
                if (!result.BonusSkills.TryGetValue(lineageId, out var skills)) result.BonusSkills[lineageId] = skills = new List<SkillDef>();
                if (!skills.Contains(skill)) skills.Add(skill);
            }
        }

        static string LineageIdOf(CharacterDatabase db, string formId) => db != null ? db.GetLineageOfForm(formId)?.id : null;

        static bool IsUsable(StatModifier modifier) => modifier != null && (int)modifier.stat >= 0 && (int)modifier.stat < StatCount;

        /// <summary>이 스테이지 기믹에서 시너지 효과 배율 (favoredModifier가 같으면 favoredMultiplier, 아니면 1)</summary>
        public static float FavoredMultiplier(SynergyDef def, StageModifierType stageModifier)
        {
            if (def == null || stageModifier == StageModifierType.None || def.favoredModifier != stageModifier) return 1f;
            return def.favoredMultiplier > 0f ? def.favoredMultiplier : 1f;
        }

        // ───────────────────────────── 계산 ─────────────────────────────

        bool HasDatabase => _db != null && _db.synergies != null;

        bool CanEvaluate => HasDatabase && _formation != null && _characters?.Db != null;

        static bool IsUsable(SynergyDef def)
        {
            if (def == null || def.tiers == null || def.tiers.Count == 0) return false;
            if (def.kind == SynergyKind.Combo) return def.lineageIds != null && def.lineageIds.Exists(id => !string.IsNullOrEmpty(id)) && def.tiers[0] != null;
            foreach (var tier in def.tiers)
                if (tier != null) return true;
            return false;
        }

        /// <summary>편성을 버퍼에 복사한다 (잠긴 덱 칸은 FormationSystem.GetEcho가 null을 준다)</summary>
        void CopyFormation(bool includeGuardians)
        {
            for (int i = 0; i < _guardians.Length; i++) _guardians[i] = includeGuardians ? _formation.GetGuardian(i) : null;
            for (int i = 0; i < _echoes.Length; i++) _echoes[i] = _formation.GetEcho(i);
        }

        /// <summary>버퍼의 편성으로 상태 목록을 만든다</summary>
        List<SynergyStatus> Evaluate(bool includeInactive)
        {
            CollectPlaced();
            var result = new List<SynergyStatus>();
            var synergies = _db.synergies;
            for (int i = 0; i < synergies.Count; i++)
            {
                var def = synergies[i];
                if (!IsUsable(def)) continue;
                var status = def.kind == SynergyKind.Combo ? EvaluateCombo(def) : EvaluateCount(def);
                if (!includeInactive && !status.IsActive) continue;
                status.Order = i;
                result.Add(status);
            }
            result.Sort(StatusOrder);
            return result;
        }

        /// <summary>버퍼 → 실제로 전투에 나갈 수 있는 형태 목록 (보유 계보 + 해금 형태만, BattleSystem과 같은 기준)</summary>
        void CollectPlaced()
        {
            _placed.Clear();
            AddPlaced(_guardians);
            AddPlaced(_echoes);
        }

        void AddPlaced(string[] side)
        {
            var db = _characters.Db;
            foreach (var formId in side)
            {
                if (string.IsNullOrEmpty(formId)) continue;
                var form = db.GetForm(formId);
                var lineage = form != null ? db.GetLineageOfForm(formId) : null;
                if (lineage == null || !_characters.IsOwned(lineage.id) || !_characters.IsFormUnlocked(formId)) continue;
                _placed.Add(new PlacedUnit(form, lineage, TraitMaskOf(lineage)));
            }
        }

        /// <summary>특성/성향: 해당 유닛 수로 단계를 고른다</summary>
        SynergyStatus EvaluateCount(SynergyDef def)
        {
            var status = new SynergyStatus { Def = def };
            int traitBit = def.kind == SynergyKind.Trait ? TraitBit(def.trait) : 0;
            foreach (var unit in _placed)
            {
                bool member = def.kind == SynergyKind.Trait ? (unit.TraitMask & traitBit) != 0 : unit.Form.style == def.style;
                if (!member) continue;
                status.Count++;
                AddMember(status, unit);
            }
            ResolveTier(status);
            return status;
        }

        /// <summary>조합: lineageIds 계보가 모두 편성되면 tiers[0]이 켜진다 (지킴이든 잔향이든)</summary>
        SynergyStatus EvaluateCombo(SynergyDef def)
        {
            var status = new SynergyStatus { Def = def };
            var ids = def.lineageIds;
            int required = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                var lineageId = ids[i];
                if (string.IsNullOrEmpty(lineageId) || ids.IndexOf(lineageId) < i) continue; // 빈 ID / 중복 ID 무시
                required++;
                bool placed = false;
                foreach (var unit in _placed)
                {
                    if (unit.Lineage.id != lineageId) continue;
                    placed = true;
                    AddMember(status, unit);
                }
                if (placed) status.Count++;
                else status.MissingLineageIds.Add(lineageId);
            }

            status.MaxCount = required;
            if (required > 0 && status.Count >= required)
            {
                status.TierIndex = 0;
            }
            else
            {
                status.NextTier = def.tiers[0];
                status.NextCount = required;
            }
            return status;
        }

        static void AddMember(SynergyStatus status, PlacedUnit unit)
        {
            status.MemberFormIds.Add(unit.Form.id);
            if (!status.MemberLineageIds.Contains(unit.Lineage.id)) status.MemberLineageIds.Add(unit.Lineage.id);
        }

        /// <summary>count 이하인 단계 중 가장 높은 단계를 켜고, 그보다 높은 단계 중 가장 가까운 것을 다음 단계로 둔다 (단계 순서가 섞여 있어도 됨)</summary>
        static void ResolveTier(SynergyStatus status)
        {
            var tiers = status.Def.tiers;
            int bestNeed = int.MinValue;
            int nextIndex = -1;
            int nextNeed = int.MaxValue;
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier == null) continue;
                int need = Math.Max(1, tier.count);
                status.MaxCount = Math.Max(status.MaxCount, need);
                if (status.Count >= need)
                {
                    if (need > bestNeed)
                    {
                        bestNeed = need;
                        status.TierIndex = i;
                    }
                }
                else if (need < nextNeed)
                {
                    nextNeed = need;
                    nextIndex = i;
                }
            }
            if (nextIndex < 0) return;
            status.NextTier = tiers[nextIndex];
            status.NextCount = nextNeed;
        }

        /// <summary>켜진 것 먼저 → 다음 단계까지 남은 수가 적은 순(최고 단계 = 0) → 수가 많은 순 → 데이터 순서</summary>
        static int CompareStatuses(SynergyStatus a, SynergyStatus b)
        {
            if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
            int byRemaining = a.Remaining.CompareTo(b.Remaining);
            if (byRemaining != 0) return byRemaining;
            int byCount = b.Count.CompareTo(a.Count);
            return byCount != 0 ? byCount : a.Order.CompareTo(b.Order);
        }

        static int TraitBit(TraitType trait) => (int)trait >= 0 && (int)trait < 31 ? 1 << (int)trait : 0;

        static int TraitMaskOf(LineageDef lineage)
        {
            int mask = 0;
            if (lineage?.traits == null) return mask;
            foreach (var trait in lineage.traits) mask |= TraitBit(trait);
            return mask;
        }

        // ───────────────────────────── 표기 ─────────────────────────────

        /// <summary>예: "공격력 +15%", "시작 잔향 +5", "잔향이 소환 비용 -1", "수선점 받는 피해 -10%"</summary>
        public static string DescribeModifier(StatModifier modifier) => modifier == null ? string.Empty : DescribeModifier(modifier.stat, modifier.value);

        /// <summary>능력치와 값으로 한 줄 설명 (감소형 능력치는 값이 양수면 "-"로 보여 준다)</summary>
        public static string DescribeModifier(StatType stat, float value)
        {
            float shown = IsReduction(stat) ? -value : value;
            string sign = shown < 0f ? "-" : "+";
            string number = Math.Abs(shown).ToString("0.##", CultureInfo.InvariantCulture);
            return $"{StatName(stat)} {sign}{number}{(IsFlat(stat) ? string.Empty : "%")}";
        }

        /// <summary>단계의 능력치 설명을 이어 붙인다. 예: "공격력 +15%, 공격 속도 +10%" (multiplier = 스테이지 기믹 배율)</summary>
        public static string DescribeModifiers(SynergyTier tier, float multiplier = 1f, string separator = ", ")
        {
            if (tier?.modifiers == null || tier.modifiers.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var modifier in tier.modifiers)
            {
                if (modifier == null || modifier.value == 0f) continue;
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(DescribeModifier(modifier.stat, modifier.value * multiplier));
            }
            return sb.ToString();
        }

        /// <summary>적용 대상 표기: "구성원" 또는 "팀 전체"</summary>
        public static string ScopeName(SynergyTier tier) => tier != null && tier.membersOnly ? "구성원" : "팀 전체";

        /// <summary>
        /// 시너지 종류까지 본 적용 대상 표기 (호환용). 역할 시너지의 구성원 전용 단계도 이제 구성원 형태만 받으므로
        /// 종류와 상관없이 ScopeName(tier)와 같다.
        /// </summary>
        public static string ScopeName(SynergyDef def, SynergyTier tier) => ScopeName(tier);

        /// <summary>
        /// (호환용) 구성원 전용 효과를 함께 받는 비구성원 형태 안내. 예: "같은 계보라서 짝짝이, 똑단이도 효과를 함께 받아요"
        /// (SharedFormIds가 비어 있으면 빈 문자열 — 지금 계산 결과는 항상 비어 있다)
        /// </summary>
        public string DescribeShared(SynergyStatus status)
        {
            if (status == null || status.SharedFormIds.Count == 0) return string.Empty;
            var db = _characters?.Db;
            var sb = new StringBuilder("같은 계보라서 ");
            for (int i = 0; i < status.SharedFormIds.Count; i++)
            {
                string formId = status.SharedFormIds[i];
                var form = db != null && !string.IsNullOrEmpty(formId) ? db.GetForm(formId) : null;
                if (i > 0) sb.Append(", ");
                sb.Append(form != null && !string.IsNullOrEmpty(form.name) ? form.name : formId);
            }
            return sb.Append("도 효과를 함께 받아요").ToString();
        }

        /// <summary>전투 시작 연출 문구. 예: "'수선 듀오' 조합이 켜졌어요!", "천 특성 2단계가 켜졌어요!"</summary>
        public static string ActivatedText(SynergyDef def, int tierIndex)
        {
            if (def == null) return string.Empty;
            string name = string.IsNullOrEmpty(def.name) ? def.id : def.name;
            string kindWord = KindWord(def.kind);
            string label = def.kind == SynergyKind.Combo
                ? $"'{name}' {kindWord}"
                : $"{(name.Contains(kindWord) ? name : $"{name} {kindWord}")} {Math.Max(0, tierIndex) + 1}단계";
            return $"{KoreanText.WithSubject(label)} 켜졌어요!";
        }

        static string KindWord(SynergyKind kind) => kind switch
        {
            SynergyKind.Trait => "특성",
            SynergyKind.Style => "역할",
            SynergyKind.Combo => "조합",
            _ => "시너지",
        };

        /// <summary>예: "공격 속도"</summary>
        public static string StatName(StatType stat) => stat switch
        {
            StatType.AttackPercent => "공격력",
            StatType.AttackSpeedPercent => "공격 속도",
            StatType.HpPercent => "체력",
            StatType.RangePercent => "사거리",
            StatType.SkillCooldownReductionPercent => "스킬 쿨타임",
            StatType.HealPowerPercent => "회복량",
            StatType.ShieldPowerPercent => "보호막량",
            StatType.ShopMaxHpPercent => "수선점 최대 내구도",
            StatType.ShopDamageReductionPercent => "수선점 받는 피해",
            StatType.ShopRegenPercentPerSecond => "초당 수선점 회복",
            StatType.ResonanceRegenPercent => "잔향 회복 속도",
            StatType.StartResonanceFlat => "시작 잔향",
            StatType.EchoCostReductionFlat => "잔향이 소환 비용",
            StatType.EchoLifetimePercent => "잔향이 유지 시간",
            StatType.EchoMoveSpeedPercent => "잔향이 이동 속도",
            StatType.LampGainPercent => "불빛 게이지 충전",
            StatType.LampDamagePercent => "불 밝히기 피해",
            StatType.StunDurationPercent => "기절 시간",
            StatType.SlowPowerPercent => "감속 효과",
            StatType.BossDamagePercent => "보스에게 주는 피해",
            StatType.CritChancePercent => "치명타 확률",
            StatType.CritDamagePercent => "치명타 피해",
            StatType.GoldGainPercent => "단추 획득량",
            _ => stat.ToString(),
        };

        /// <summary>값이 클수록 줄어드는 능력치 (표기할 때 부호를 뒤집는다)</summary>
        public static bool IsReduction(StatType stat) =>
            stat == StatType.SkillCooldownReductionPercent || stat == StatType.ShopDamageReductionPercent || stat == StatType.EchoCostReductionFlat;

        /// <summary>% 가 아닌 고정값 능력치</summary>
        public static bool IsFlat(StatType stat) => stat == StatType.StartResonanceFlat || stat == StatType.EchoCostReductionFlat;
    }
}
