using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 강해지기 한 줄 (StrengthenAdvisor.GetRows가 채운다, 목록 재사용 가능). GetRows는 into에 이미 들어 있던 줄 객체를 다시 쓴다 —
    /// 줄의 값은 그 목록으로 다음 GetRows를 부를 때까지 유효하다 (필요한 값은 복사해 둔다)
    /// </summary>
    public sealed class StrengthenRow
    {
        public StrengthenKind Kind;
        /// <summary>대상 (계보 ID / 형태 ID / 시너지 ID / 재료 ID · 약한 방향은 방향 이름 '왼쪽 위' 등 — 종류마다 다름, 없으면 null)</summary>
        public string TargetId;
        /// <summary>오르는 전투력 (0 = 전투력과 무관한 안내 줄)</summary>
        public long Gain;
        /// <summary>지금 단추 수입으로 모으는 데 걸리는 시간 (초, 0 = 바로)</summary>
        public double CostSeconds;
        /// <summary>[가기] 이동 기능</summary>
        public FeatureId Nav;
        /// <summary>이동한 뒤 손가락을 띄울 대상 (CoachTargets, 없으면 null)</summary>
        public string Focus;
        /// <summary>줄 글의 수 (ShopUpgrade = 수선점 레벨 수 n '수선점 {n}번 강화', LineageLevel = 올릴 레벨 수 '레벨 +{n}', WeakDirection = 그 방향 몫 %, 그 밖 0)</summary>
        public int Amount;
        /// <summary>약한 방향 칸 (WeakDirection: 0 위 · 1 왼쪽 위 · 2 왼쪽 아래 · 3 오른쪽 아래 · 4 오른쪽 위 → UINav.PulseFormationSlot, 그 밖 −1)</summary>
        public int Slot = -1;
        /// <summary>세울 자리의 쪽 (FillSlot = 그 빈 칸의 쪽 '빈 {지킴이|잔향이} 자리 채우기', SynergyOneShort = 고른 놓을 자리의 쪽 — 그 밖은 기본값 Guardian, 읽지 않는다)</summary>
        public CharacterRole Role;
        /// <summary>세울 형태 ID (FillSlot · SynergyOneShort: '{이름}{을/를} 세우면 …'의 이름 — 그 밖 null)</summary>
        public string CandidateId;
    }

    /// <summary>
    /// 강해지는 방법 고르기 (PRG-03, SPEC4 §13.2 — 순수, 모듈이 아님). 후보:
    /// ShopUpgrade(120초 수입 안에 살 수 있는 수선점 레벨 1~10, PowerSystem.PreviewShopLevels) · LineageLevel(편성한 계보마다 +5 또는 살 수 있는 만큼, PreviewLevelUp) ·
    /// FillSlot(빈 지킴이 · 덱 칸 + 안 쓴 가장 센 형태, PreviewPlace — Role = 그 칸의 쪽, CandidateId = 그 형태) ·
    /// SynergyOneShort(한 명만 더 있으면 다음 단계인 시너지, 그 밖의 가장 약한 칸과 맞바꿈 — TargetId = 시너지, Role · CandidateId = 고른 자리의 쪽 · 세울 형태) ·
    /// ResonanceReady(공명을 올릴 수 있음, PreviewResonance) · AutoFormation(PreviewAutoFormation ≥ 1.05 × 팀 전투력) ·
    /// EvolutionReady(지금 제작할 수 있는 진화 — 편성한 계보면 형태 전투력 차이) · MissingMaterial(가장 가까운 레시피에 재료 한 종만 모자람) ·
    /// WeakDirection(마지막 실패에 패배 분석 자료가 있음 — CR-01 LastDefeat, 클리어하면 비워진다. 편성 탭 + 그 칸 반짝).
    /// 순서: 전투력이 오르는 줄을 얻는 전투력 ÷ (1 + 비용 초 / 60) 큰 순, 그다음 안내 줄(진화 가능 → 약한 방향 → 모자란 재료). 팀 전투력의 0.5% 미만인 줄은 뺀다. 최대 max줄.
    /// <para>
    /// 시트가 열릴 때와 열린 동안 1초마다 불린다. 조언자 자신은 할당하지 않는다: 후보는 구조체 목록 · 보유 계보 / 해금 형태는 GetRows마다 한 번 채우는 재사용 목록 ·
    /// 정렬은 삽입 정렬(대리자 없음) · 줄 객체는 into에 있던 것을 다시 쓰고 모자랄 때만 만든다 (첫 호출 · 줄 수가 늘 때).
    /// 아직 할당하는 곳 (1초마다, 시트가 열린 동안만 — 받아들인 차이, RM-2 P4-1 핸드오프): 시너지를 다시 계산하는 미리보기
    /// (PowerSystem.PreviewPlace — 마지막 결과 하나만 기억 · PreviewResonance), SynergySystem.GetStatuses, EvolutionSystem.Check 결과.
    /// </para>
    /// </summary>
    public class StrengthenAdvisor
    {
        /// <summary>수선점 레벨 후보: 이 초 동안 버는 단추로 살 수 있는 만큼 (1~MaxShopLevels)</summary>
        public const double ShopIncomeWindowSeconds = 120;
        public const int MaxShopLevels = 10;
        /// <summary>계보 레벨 후보 최대 레벨 수</summary>
        public const int LineageLevels = 5;
        /// <summary>자동 편성 줄 조건: 미리보기 ≥ 이 배수 × 팀 전투력</summary>
        public const double AutoFormationRatio = 1.05;
        /// <summary>이 비율 미만의 전투력 줄은 뺀다</summary>
        public const double MinGainRatio = 0.005;

        /// <summary>편성 탭 자동 편성 버튼 · 강화 x1 버튼 (계약 C-05 — StrengthenPopup과 같은 경로)</summary>
        public static readonly string FormationAutoPath = CoachTargets.FormationPanel + "/TeamSynergy/AutoButton";
        public static readonly string GrowthLevelUp1Path = CoachTargets.GrowthPanel + "/Buttons/LevelUp1Button";

        /// <summary>후보 한 줄 (구조체 — 고르는 동안 할당하지 않는다. 고른 줄만 into의 StrengthenRow로 옮긴다)</summary>
        struct Candidate
        {
            public StrengthenKind Kind;
            public string TargetId;
            public long Gain;
            public double CostSeconds;
            public FeatureId Nav;
            public string Focus;
            public int Amount;
            public int Slot;
            public CharacterRole Role;
            public string CandidateId;

            public Candidate(StrengthenKind kind, string targetId, long gain, double costSeconds, FeatureId nav, string focus = null, int amount = 0, int slot = -1,
                CharacterRole role = CharacterRole.Guardian, string candidateId = null)
            {
                Kind = kind;
                TargetId = targetId;
                Gain = gain;
                CostSeconds = costSeconds;
                Nav = nav;
                Focus = focus;
                Amount = amount;
                Slot = slot;
                Role = role;
                CandidateId = candidateId;
            }
        }

        readonly PowerSystem _power;
        readonly GrowthSystem _growth;
        readonly CharacterSystem _characters;
        readonly FormationSystem _formation;
        readonly SynergySystem _synergy;
        readonly EvolutionSystem _evolution;

        readonly List<Candidate> _gainRows = new();
        readonly List<Candidate> _infoRows = new();
        /// <summary>into에 있던 줄 객체 (다시 쓴다)</summary>
        readonly List<StrengthenRow> _reuse = new();
        /// <summary>GetRows마다 한 번 채우는 보유 계보 (CharacterSystem.GetOwnedLineages와 같은 순서)</summary>
        readonly List<LineageDef> _owned = new();
        /// <summary>처음 필요할 때 한 번 채우는 해금 형태 (CharacterSystem.GetUnlockedForms와 같은 순서)</summary>
        readonly List<CharacterFormDef> _forms = new();
        bool _formsReady;
        long _minGain;

        public StrengthenAdvisor(PowerSystem power, GrowthSystem growth, CharacterSystem characters, FormationSystem formation, SynergySystem synergy,
            EvolutionSystem evolution, BattleSystem battle, InventorySystem inventory, CheerSystem cheer)
        {
            _power = power;
            _growth = growth;
            _characters = characters;
            _formation = formation;
            _synergy = synergy;
            _evolution = evolution;
            // battle · inventory · cheer: 받지 않는다 — 약한 방향은 전투 화면이 남기는 LastDefeat(CR-01)를 읽고, 응원은 시트 머리 단추(P4-6)가 직접 쓴다
        }

        /// <summary>
        /// 강해지기 줄을 채운다 (into는 비우고 채움, 최대 max줄 — into에 있던 줄 객체를 다시 쓴다). 계산하지 못하면 빈 목록 (예외 없음)
        /// </summary>
        public void GetRows(List<StrengthenRow> into, int max = 4)
        {
            if (into == null) return;
            _reuse.Clear();
            for (int i = 0; i < into.Count; i++)
                if (into[i] != null) _reuse.Add(into[i]);
            into.Clear();
            _gainRows.Clear();
            _infoRows.Clear();
            try
            {
                if (max <= 0 || _power == null || _characters == null) return;
                long team = _power.TeamPower;
                _minGain = (long)Math.Ceiling(Math.Max(0, team) * MinGainRatio);
                _characters.GetOwnedLineages(_owned);
                _formsReady = false;
                AddShopRow();
                AddLineageRows();
                AddFillSlotRows();
                AddSynergyOneShortRow();
                AddResonanceRows();
                AddAutoFormationRow(team);
                AddEvolutionRows();
                AddWeakDirectionRow();
                AddMissingMaterialRow();

                InsertionSort(_gainRows, info: false);
                InsertionSort(_infoRows, info: true);
                for (int i = 0; i < _gainRows.Count && into.Count < max; i++) into.Add(ToRow(_gainRows[i]));
                for (int i = 0; i < _infoRows.Count && into.Count < max; i++) into.Add(ToRow(_infoRows[i]));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Strengthen] 줄을 만들지 못했어요: {e.Message}");
                into.Clear();
            }
            finally
            {
                _gainRows.Clear();
                _infoRows.Clear();
                _reuse.Clear();
                _owned.Clear();
                _forms.Clear();
                _formsReady = false;
            }
        }

        /// <summary>순위 점수 = 얻는 전투력 ÷ (1 + 비용 초 / 60)</summary>
        public static double Score(StrengthenRow row) => row == null ? 0 : Score(row.Gain, row.CostSeconds);

        static double Score(long gain, double costSeconds) => gain / (1.0 + Math.Max(0, costSeconds) / 60.0);

        static int CompareByScore(in Candidate a, in Candidate b)
        {
            int byScore = Score(b.Gain, b.CostSeconds).CompareTo(Score(a.Gain, a.CostSeconds));
            return byScore != 0 ? byScore : ((int)a.Kind).CompareTo((int)b.Kind);
        }

        static int InfoOrder(StrengthenKind kind) => kind switch
        {
            StrengthenKind.EvolutionReady => 0,
            StrengthenKind.WeakDirection => 1,
            StrengthenKind.MissingMaterial => 2,
            _ => 3,
        };

        static int CompareInfo(in Candidate a, in Candidate b)
        {
            int byKind = InfoOrder(a.Kind).CompareTo(InfoOrder(b.Kind));
            return byKind != 0 ? byKind : string.CompareOrdinal(a.TargetId, b.TargetId);
        }

        /// <summary>삽입 정렬 (안정 · 할당 없음 — 후보는 많아야 십여 개)</summary>
        static void InsertionSort(List<Candidate> list, bool info)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0)
                {
                    var prev = list[j];
                    int order = info ? CompareInfo(prev, item) : CompareByScore(prev, item);
                    if (order <= 0) break;
                    list[j + 1] = prev;
                    j--;
                }
                list[j + 1] = item;
            }
        }

        /// <summary>후보를 줄 객체로 옮긴다 (into에 있던 줄을 다시 쓰고, 모자라면 만든다)</summary>
        StrengthenRow ToRow(in Candidate c)
        {
            StrengthenRow row;
            int last = _reuse.Count - 1;
            if (last >= 0)
            {
                row = _reuse[last];
                _reuse.RemoveAt(last);
            }
            else row = new StrengthenRow();
            row.Kind = c.Kind;
            row.TargetId = c.TargetId;
            row.Gain = c.Gain;
            row.CostSeconds = c.CostSeconds;
            row.Nav = c.Nav;
            row.Focus = c.Focus;
            row.Amount = c.Amount;
            row.Slot = c.Slot;
            row.Role = c.Role;
            row.CandidateId = c.CandidateId;
            return row;
        }

        /// <summary>전투력 줄 후보 (0 이하 · 팀 전투력의 0.5% 미만은 넣지 않는다)</summary>
        void AddGain(in Candidate c)
        {
            if (c.Gain <= 0 || c.Gain < _minGain) return;
            _gainRows.Add(c);
        }

        /// <summary>해금 형태 (처음 필요할 때 보유 계보 순서 → 데이터상 형태 순서로 한 번 채운다)</summary>
        List<CharacterFormDef> UnlockedForms()
        {
            if (_formsReady) return _forms;
            _formsReady = true;
            _forms.Clear();
            for (int i = 0; i < _owned.Count; i++)
            {
                var forms = _owned[i]?.forms;
                if (forms == null) continue;
                for (int k = 0; k < forms.Count; k++)
                {
                    var form = forms[k];
                    if (form != null && _characters.IsFormUnlocked(form.id)) _forms.Add(form);
                }
            }
            return _forms;
        }

        double Gold => _characters.AvailableGold;

        double GoldPerSecond => _growth != null ? Math.Max(1.0, _growth.GoldPerSecond) : 1.0;

        double CostSeconds(double cost) => Math.Max(0, cost - Gold) / GoldPerSecond;

        // ───────────────────────────── 후보 ─────────────────────────────

        void AddShopRow()
        {
            if (_growth == null || _growth.IsMaxLevel) return;
            int room = Math.Min(MaxShopLevels, _growth.RemainLevels);
            if (room <= 0) return;
            double budget = Gold + ShopIncomeWindowSeconds * GoldPerSecond;
            int n = 1;
            for (int k = 2; k <= room; k++)
            {
                if (_growth.GetLevelUpCost(_growth.Level, k) > budget) break;
                n = k;
            }
            long gain = _power.PreviewShopLevels(n);
            if (gain <= 0) return;
            AddGain(new Candidate(StrengthenKind.ShopUpgrade, null, gain, CostSeconds(_growth.GetLevelUpCost(_growth.Level, n)), FeatureId.ShopUpgrade,
                GrowthLevelUp1Path, amount: n));
        }

        void AddLineageRows()
        {
            if (_formation == null || !_characters.IsLevelUpUnlocked) return;
            for (int i = 0; i < _owned.Count; i++)
            {
                var lineage = _owned[i];
                if (lineage == null || !_formation.IsLineagePlaced(lineage.id) || _characters.IsMaxLevel(lineage.id)) continue;
                int levels = AffordableLevels(lineage.id);
                long gain = _power.PreviewLevelUp(lineage.id, levels);
                if (gain <= 0) continue;
                AddGain(new Candidate(StrengthenKind.LineageLevel, lineage.id, gain, CostSeconds(_characters.GetLevelUpCost(lineage.id, levels)),
                    FeatureId.ShopUpgrade, amount: levels));
            }
        }

        /// <summary>+5 레벨 또는 지금 살 수 있는 만큼 (하나도 못 사면 1)</summary>
        int AffordableLevels(string lineageId)
        {
            double gold = Gold;
            int affordable = 0;
            for (int k = 1; k <= LineageLevels; k++)
            {
                double cost = _characters.GetLevelUpCost(lineageId, k);
                if (cost <= 0 || cost > gold) break;
                affordable = k;
            }
            return affordable > 0 ? affordable : 1;
        }

        void AddFillSlotRows()
        {
            if (_formation == null) return;
            int emptyGuardian = -1;
            for (int i = 0; i < FormationSystem.GuardianSlotCount && emptyGuardian < 0; i++)
                if (_formation.GetGuardian(i) == null) emptyGuardian = i;
            int emptyEcho = -1;
            for (int i = 0; i < FormationSystem.EchoSlotCount && emptyEcho < 0; i++)
                if (_formation.IsEchoSlotUnlocked(i) && _formation.GetEcho(i) == null) emptyEcho = i;
            if (emptyGuardian < 0 && emptyEcho < 0) return;

            var forms = UnlockedForms();
            if (emptyGuardian >= 0) AddFillSlot(forms, CharacterRole.Guardian, emptyGuardian);
            if (emptyEcho >= 0) AddFillSlot(forms, CharacterRole.Echo, emptyEcho);
        }

        /// <summary>그 빈 칸에 놓을 수 있는 안 쓴 형태 중 형태 전투력이 가장 높은 것 (같으면 추천 역할 우선)</summary>
        void AddFillSlot(List<CharacterFormDef> forms, CharacterRole role, int slot)
        {
            CharacterFormDef best = null;
            long bestPower = -1;
            for (int i = 0; i < forms.Count; i++)
            {
                var form = forms[i];
                if (form == null || _formation.IsPlaced(form.id, out _, out _) || _formation.CanPlace(role, slot, form.id) != PlaceResult.Ok) continue;
                long power = _power.GetFormPower(form.id) + (form.recommendedRole == role ? 1 : 0);
                if (power > bestPower)
                {
                    bestPower = power;
                    best = form;
                }
            }
            if (best == null) return;
            long gain = _power.PreviewPlace(role, slot, best.id) - _power.TeamPower;
            if (gain <= 0) return;
            AddGain(new Candidate(StrengthenKind.FillSlot, best.id, gain, 0, FeatureId.Formation, role: role, candidateId: best.id));
        }

        /// <summary>한 명만 더 있으면 다음 단계인 시너지: 그 시너지에 들어갈 안 쓴 형태를 빈 칸(없으면 그 시너지 밖의 가장 약한 칸)에 — 가장 크게 오르는 하나</summary>
        void AddSynergyOneShortRow()
        {
            if (_synergy == null || _formation == null) return;
            var statuses = _synergy.GetStatuses(includeInactive: true);
            if (statuses == null || statuses.Count == 0) return;
            List<CharacterFormDef> forms = null;
            bool found = false;
            var best = default(Candidate);
            foreach (var status in statuses)
            {
                if (status?.Def == null || status.NextTier == null || status.Remaining != 1) continue;
                forms ??= UnlockedForms();
                for (int i = 0; i < forms.Count; i++)
                {
                    var form = forms[i];
                    if (form == null || _formation.IsPlaced(form.id, out _, out _) || !WouldJoin(status, form)) continue;
                    var role = form.recommendedRole == CharacterRole.Echo ? CharacterRole.Echo : CharacterRole.Guardian;
                    int slot = PickSlot(role, status, form.id);
                    if (slot < 0)
                    {
                        role = role == CharacterRole.Guardian ? CharacterRole.Echo : CharacterRole.Guardian;
                        slot = PickSlot(role, status, form.id);
                    }
                    if (slot < 0) continue;
                    long gain = _power.PreviewPlace(role, slot, form.id) - _power.TeamPower;
                    if (gain <= 0 || (found && gain <= best.Gain)) continue;
                    found = true;
                    best = new Candidate(StrengthenKind.SynergyOneShort, status.Def.id, gain, 0, FeatureId.Formation, role: role, candidateId: form.id);
                }
            }
            if (found) AddGain(best);
        }

        /// <summary>그 형태가 들어가면 그 시너지 구성원이 하나 늘어나는지 (조합 = 빠진 계보, 특성 · 역할 = 그 형태가 기여하는 시너지)</summary>
        bool WouldJoin(SynergyStatus status, CharacterFormDef form)
        {
            var lineage = _characters.Db?.GetLineageOfForm(form.id);
            if (lineage == null) return false;
            if (status.Def.kind == SynergyKind.Combo) return status.MissingLineageIds != null && status.MissingLineageIds.Contains(lineage.id);
            if (status.MemberLineageIds != null && status.MemberLineageIds.Contains(lineage.id) && status.Def.kind == SynergyKind.Trait) return false;
            var involved = _synergy.GetSynergiesForForm(form.id);
            return involved != null && involved.Contains(status.Def);
        }

        /// <summary>그 쪽의 빈 칸, 없으면 그 시너지 구성원이 아닌 가장 약한 칸 (놓을 수 없으면 -1)</summary>
        int PickSlot(CharacterRole role, SynergyStatus status, string formId)
        {
            int count = role == CharacterRole.Guardian ? FormationSystem.GuardianSlotCount : FormationSystem.EchoSlotCount;
            int weakest = -1;
            long weakestPower = long.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (role == CharacterRole.Echo && !_formation.IsEchoSlotUnlocked(i)) continue;
                string placed = role == CharacterRole.Guardian ? _formation.GetGuardian(i) : _formation.GetEcho(i);
                if (_formation.CanPlace(role, i, formId) != PlaceResult.Ok) continue;
                if (placed == null) return i;
                if (status.MemberFormIds != null && status.MemberFormIds.Contains(placed)) continue;
                long power = _power.GetUnitPower(placed);
                if (power < weakestPower)
                {
                    weakestPower = power;
                    weakest = i;
                }
            }
            return weakest;
        }

        void AddResonanceRows()
        {
            if (_formation == null) return;
            for (int i = 0; i < _owned.Count; i++)
            {
                var lineage = _owned[i];
                if (lineage == null || !_characters.CanResonance(lineage.id) || !_formation.IsLineagePlaced(lineage.id)) continue;
                long gain = _power.PreviewResonance(lineage.id);
                if (gain <= 0) continue;
                AddGain(new Candidate(StrengthenKind.ResonanceReady, lineage.id, gain, 0, FeatureId.Characters));
            }
        }

        void AddAutoFormationRow(long team)
        {
            long preview = _power.PreviewAutoFormation();
            if (team <= 0 ? preview <= 0 : preview < AutoFormationRatio * team) return;
            long gain = preview - team;
            if (gain <= 0) return;
            AddGain(new Candidate(StrengthenKind.AutoFormation, null, gain, 0, FeatureId.Formation, FormationAutoPath));
        }

        void AddEvolutionRows()
        {
            if (_evolution == null || !_evolution.IsCraftUnlocked) return;
            for (int i = 0; i < _owned.Count; i++)
            {
                var lineage = _owned[i];
                if (lineage?.forms == null) continue;
                for (int k = 0; k < lineage.forms.Count; k++)
                {
                    var form = lineage.forms[k];
                    if (form == null || _evolution.GetState(form.id) != EvolutionState.ClueFound) continue;
                    var check = _evolution.Check(form.id);
                    if (check == null || !check.CanCraft) continue;
                    long gain = 0;
                    if (_formation != null && _formation.IsLineagePlaced(lineage.id))
                    {
                        long current = PlacedFormPower(lineage);
                        gain = Math.Max(0, _power.GetFormPower(form.id) - current);
                    }
                    var row = new Candidate(StrengthenKind.EvolutionReady, form.id, gain, 0, FeatureId.Characters);
                    if (gain > 0) AddGain(row);
                    else _infoRows.Add(row);
                }
            }
        }

        /// <summary>편성에 놓인 그 계보 형태 중 가장 높은 형태 전투력</summary>
        long PlacedFormPower(LineageDef lineage)
        {
            long best = 0;
            for (int i = 0; i < lineage.forms.Count; i++)
            {
                var form = lineage.forms[i];
                if (form == null || !_formation.IsPlaced(form.id, out _, out _)) continue;
                best = Math.Max(best, _power.GetFormPower(form.id));
            }
            return best;
        }

        /// <summary>
        /// 약한 방향 보강 (CR-01, SPEC4 §7.2 · §13.2): 마지막 실패에 수선점 피해 자료가 있으면 그 방향으로 가는 안내 줄 하나 —
        /// TargetId = 방향 이름(LastDefeat.Direction, '약한 방향 보강 · {방향}'), Slot = 칸 0~4, Amount = 그 칸 몫 %, [가기] = 편성 탭 (+ UINav.PulseFormationSlot(Slot)).
        /// 전투 화면이 실패마다 채우고 클리어하면 비운다 (저장 안 함 — 앱을 다시 켜면 없다). 할당 없음 (방향 이름은 상수 글)
        /// </summary>
        void AddWeakDirectionRow()
        {
            if (!LastDefeat.HasData) return;
            int slot = LastDefeat.Slot;
            if (slot < 0 || slot >= BattleGeometry.SlotCount) return;
            _infoRows.Add(new Candidate(StrengthenKind.WeakDirection, LastDefeat.Direction, 0, 0, FeatureId.Formation,
                amount: Math.Max(0, Math.Min(100, LastDefeat.Percent)), slot: slot));
        }

        /// <summary>단서를 찾은 레시피 중 모자란 재료 종류가 가장 적은 것이 한 종만 모자라면 그 재료로 가는 줄 (UINav.GoToSource(TargetId))</summary>
        void AddMissingMaterialRow()
        {
            if (_evolution == null) return;
            string bestItem = null;
            int bestMissing = int.MaxValue;
            for (int i = 0; i < _owned.Count; i++)
            {
                var lineage = _owned[i];
                if (lineage?.forms == null) continue;
                for (int k = 0; k < lineage.forms.Count; k++)
                {
                    var form = lineage.forms[k];
                    if (form == null || _evolution.GetState(form.id) != EvolutionState.ClueFound) continue;
                    var check = _evolution.Check(form.id);
                    if (check == null || check.CanCraft) continue;
                    int missing = 0;
                    string item = null;
                    foreach (var material in check.Materials)
                    {
                        if (material.Enough) continue;
                        missing++;
                        item = material.ItemId;
                    }
                    if (missing == 0 || missing >= bestMissing) continue;
                    bestMissing = missing;
                    bestItem = item;
                }
            }
            if (bestItem == null || bestMissing != 1) return;
            _infoRows.Add(new Candidate(StrengthenKind.MissingMaterial, bestItem, 0, 0, FeatureId.Battle));
        }
    }
}
