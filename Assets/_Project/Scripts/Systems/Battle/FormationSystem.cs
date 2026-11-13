using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 편성. 하나의 캐릭터 목록에서 형태를 골라 지킴이(방위 링 5칸) 또는 잔향이(덱)로 배치한다.
    /// 규칙: 같은 형태는 한쪽에만, 같은 계보는 지킴이 1종 + 잔향이 1종까지.
    /// 원정과 편성은 따로다: 원정 중인 계보도 편성할 수 있고 함께 싸운다 (PlaceResult.Dispatched는 옛 코드 호환용으로만 남김).
    /// 한 계보가 지킴이와 잔향이 양쪽에 모두 나가 있으면 "계보 공명" 시너지로 팀 공격력이 오른다.
    /// 플레이어가 직접 놓은 횟수(FormationSave.placementsDone)를 세어 사용법 안내 줄을 숨기는 데 쓴다.
    /// </summary>
    public class FormationSystem
    {
        public event Action Changed;

        public const int GuardianSlotCount = FormationSave.GuardianSlots;
        public const int EchoSlotCount = FormationSave.EchoSlots;
        /// <summary>처음부터 열려 있는 잔향이 덱 칸 수</summary>
        public const int BaseEchoSlots = 5;
        /// <summary>마지막 덱 칸이 열리는 최고 스테이지</summary>
        public const int EchoSlotUnlockStage = 20;
        /// <summary>계보 공명 시너지 공격력 증가(%) = 기본 + 공명 레벨 × 레벨당</summary>
        public const float SynergyBaseBonusPercent = 4f;
        public const float SynergyBonusPerResonance = 2f;

        /// <summary>자동 편성 시 지킴이를 채우는 방위 순서 (위 → 왼쪽 아래 → 오른쪽 아래 → 왼쪽 위 → 오른쪽 위, 고르게 퍼지도록)</summary>
        static readonly int[] AutoGuardianOrder = { 0, 2, 3, 1, 4 };

        readonly PlayerData _data;
        readonly CharacterSystem _characters;

        public FormationSystem(PlayerData data, CharacterSystem characters)
        {
            _data = data;
            _characters = characters;
        }

        FormationSave Save
        {
            get
            {
                _data.formation ??= new FormationSave();
                _data.formation.Sanitize();
                return _data.formation;
            }
        }

        /// <summary>열린 잔향이 덱 칸 수 (기본 5, 마지막 칸은 잠김 → 조건 충족 시 열림)</summary>
        public int UnlockedEchoSlots => (_data.battle?.highestStage ?? 1) >= EchoSlotUnlockStage ? EchoSlotCount : BaseEchoSlots;
        /// <summary>잠긴 덱 칸 카드 안내 두 줄 (예: "스테이지 2-9 클리어 시\n열려요"). 최고 스테이지 20 도달 = 2-9 클리어</summary>
        public string EchoSlotUnlockHint => _echoSlotUnlockHint ??= MetaText.UnlockShort(EchoSlotUnlockStage) + "\n열려요";
        /// <summary>잠긴 덱 칸 토스트 문장 (예: "스테이지 2-9를 클리어하면 열려요")</summary>
        public string EchoSlotUnlockSentence => EchoSlotUnlockSentenceText;
        /// <summary>
        /// 잠긴 덱 칸 아주 작은 자물쇠 글자 "2-9" (전투 덱 잠금 알약 등). 클리어해야 하는 스테이지라 FormatStage(EchoSlotUnlockStage) = "2-10"이 아니다 —
        /// 토스트·편성 칸·잠금 덮개(FeatureId.EchoSlot6 lockedHint)와 같은 클리어 기준 (M39).
        /// </summary>
        public static string EchoSlotUnlockCode => _echoSlotUnlockCode ??= BattleSystem.FormatStage(EchoSlotUnlockStage - 1);

        // 상수로 정해지는 문구라 한 번만 만든다
        static string _echoSlotUnlockHint;
        static string _echoSlotUnlockSentence;
        static string _echoSlotUnlockCode;
        static string _slotLockedText;
        static string EchoSlotUnlockSentenceText => _echoSlotUnlockSentence ??= MetaText.UnlockSentence(EchoSlotUnlockStage);
        /// <summary>Describe(SlotLocked): 다른 Describe 문구처럼 마침표로 끝난다 (토스트 문장 EchoSlotUnlockSentence는 마침표 없음)</summary>
        static string SlotLockedText => _slotLockedText ??= "잠긴 칸이에요. " + EchoSlotUnlockSentenceText + ".";

        /// <summary>플레이어가 칸에 직접 놓은 횟수 (TryPlace 성공, 자동 편성 제외). 편성 안내 줄은 3 미만일 때만</summary>
        public int PlacementsDone => Save.placementsDone;

        public bool IsEchoSlotUnlocked(int slot) => slot >= 0 && slot < UnlockedEchoSlots;

        public string GetGuardian(int slot) => slot >= 0 && slot < GuardianSlotCount ? Normalize(Save.guardians[slot]) : null;

        /// <summary>잠긴 칸은 데이터가 남아 있어도 null</summary>
        public string GetEcho(int slot) => IsEchoSlotUnlocked(slot) ? Normalize(Save.echoes[slot]) : null;

        static string Normalize(string formId) => string.IsNullOrEmpty(formId) ? null : formId;

        string[] SideArray(CharacterRole role) => role == CharacterRole.Guardian ? Save.guardians : Save.echoes;

        int SideCount(CharacterRole role) => role == CharacterRole.Guardian ? GuardianSlotCount : EchoSlotCount;

        // ───────────────────────────── 배치 ─────────────────────────────

        public PlaceResult CanPlace(CharacterRole role, int slot, string formId)
        {
            if (role != CharacterRole.Guardian && role != CharacterRole.Echo) return PlaceResult.InvalidSlot;
            if (slot < 0 || slot >= SideCount(role)) return PlaceResult.InvalidSlot;
            if (role == CharacterRole.Echo && !IsEchoSlotUnlocked(slot)) return PlaceResult.SlotLocked;

            var lineage = string.IsNullOrEmpty(formId) ? null : _characters.Db.GetLineageOfForm(formId);
            if (lineage == null || !_characters.IsOwned(lineage.id)) return PlaceResult.NotOwned;
            if (!_characters.IsFormUnlocked(formId)) return PlaceResult.FormLocked;
            // 원정 중이어도 편성할 수 있다 (M05 — 원정과 편성은 따로)

            var other = role == CharacterRole.Guardian ? CharacterRole.Echo : CharacterRole.Guardian;
            if (IndexOf(other, formId) >= 0) return PlaceResult.OnOtherSide;

            // 같은 쪽에 같은 계보의 "다른" 형태가 다른 칸에 있으면 불가 (같은 칸이면 교체, 같은 형태면 이동)
            var side = SideArray(role);
            for (int i = 0; i < side.Length; i++)
            {
                if (i == slot || string.IsNullOrEmpty(side[i]) || side[i] == formId) continue;
                if (role == CharacterRole.Echo && !IsEchoSlotUnlocked(i)) continue;
                if (_characters.Db.GetLineageOfForm(side[i])?.id == lineage.id) return PlaceResult.LineageLimit;
            }
            return PlaceResult.Ok;
        }

        /// <summary>
        /// 슬롯에 배치. 같은 쪽 다른 칸에 있던 같은 형태를 옮기면 두 칸의 형태를 맞바꾸고(빈칸이면 이동),
        /// 목록에서 새로 올리면 칸에 있던 형태는 빠진다. 실제로 바뀌면 직접 놓은 횟수(PlacementsDone)를 하나 센다.
        /// </summary>
        public PlaceResult TryPlace(CharacterRole role, int slot, string formId)
        {
            var result = CanPlace(role, slot, formId);
            if (result != PlaceResult.Ok) return result;

            var side = SideArray(role);
            if (side[slot] == formId) return PlaceResult.Ok;
            Apply(side, slot, IndexOf(role, formId), formId);
            var save = Save;
            if (save.placementsDone < int.MaxValue) save.placementsDone++;
            Changed?.Invoke();
            return PlaceResult.Ok;
        }

        /// <summary>TryPlace 규칙으로 배열에 적용: 같은 쪽 from칸에 있던 형태면 두 칸을 맞바꾸고(빈칸이면 이동), 아니면(from &lt; 0) 칸의 형태를 밀어낸다</summary>
        static void Apply(string[] side, int slot, int from, string formId)
        {
            if (from >= 0) side[from] = side[slot];
            side[slot] = formId;
        }

        // ── 미리보기 (전투력 등): 배치를 잠깐 적용했다가 되돌린다. 이벤트·횟수 없음 ──

        string[] _previewSide;
        int _previewSlot = -1;
        string _previewSlotForm;
        int _previewFrom = -1;
        string _previewFromForm;

        /// <summary>
        /// 미리보기용: TryPlace와 같은 규칙으로 배치를 세이브 배열에 잠깐 적용한다 (Changed를 울리지 않고 PlacementsDone도 세지 않는다).
        /// 바뀐 게 있으면 true — 그때는 같은 프레임 안에서 반드시 EndPreviewPlace로 되돌린다 (try/finally).
        /// 놓을 수 없거나 이미 그 칸에 있거나 다른 미리보기 중이면 false (되돌릴 것 없음).
        /// 그 사이 GetGuardian/GetEcho/TeamAttackBonusPercent/SynergySystem.BuildModifiers는 놓은 뒤의 편성을 본다.
        /// </summary>
        internal bool BeginPreviewPlace(CharacterRole role, int slot, string formId)
        {
            if (_previewSide != null || CanPlace(role, slot, formId) != PlaceResult.Ok) return false;
            var side = SideArray(role);
            if (side[slot] == formId) return false;

            _previewSide = side;
            _previewSlot = slot;
            _previewSlotForm = side[slot];
            int from = IndexOf(role, formId);
            _previewFrom = from;
            _previewFromForm = from >= 0 ? side[from] : null;
            Apply(side, slot, from, formId);
            return true;
        }

        /// <summary>BeginPreviewPlace로 적용한 배치를 되돌린다 (미리보기 중이 아니면 아무것도 하지 않음)</summary>
        internal void EndPreviewPlace()
        {
            var side = _previewSide;
            if (side == null) return;
            if (_previewFrom >= 0) side[_previewFrom] = _previewFromForm;
            side[_previewSlot] = _previewSlotForm;
            _previewSide = null;
            _previewSlot = -1;
            _previewSlotForm = null;
            _previewFrom = -1;
            _previewFromForm = null;
        }

        public void Remove(CharacterRole role, int slot)
        {
            if (role != CharacterRole.Guardian && role != CharacterRole.Echo) return;
            if (slot < 0 || slot >= SideCount(role)) return;
            var side = SideArray(role);
            if (string.IsNullOrEmpty(side[slot])) return;
            side[slot] = null;
            Changed?.Invoke();
        }

        /// <summary>편성을 모두 비운다</summary>
        public void ClearAll()
        {
            var save = Save;
            bool any = false;
            for (int i = 0; i < save.guardians.Length; i++) { any |= !string.IsNullOrEmpty(save.guardians[i]); save.guardians[i] = null; }
            for (int i = 0; i < save.echoes.Length; i++) { any |= !string.IsNullOrEmpty(save.echoes[i]); save.echoes[i] = null; }
            if (any) Changed?.Invoke();
        }

        int IndexOf(CharacterRole role, string formId)
        {
            if (string.IsNullOrEmpty(formId)) return -1;
            var side = SideArray(role);
            for (int i = 0; i < side.Length; i++)
            {
                if (side[i] != formId) continue;
                if (role == CharacterRole.Echo && !IsEchoSlotUnlocked(i)) continue;
                return i;
            }
            return -1;
        }

        public bool IsPlaced(string formId, out CharacterRole role, out int slot)
        {
            slot = IndexOf(CharacterRole.Guardian, formId);
            if (slot >= 0)
            {
                role = CharacterRole.Guardian;
                return true;
            }
            slot = IndexOf(CharacterRole.Echo, formId);
            role = CharacterRole.Echo;
            return slot >= 0;
        }

        public bool IsLineagePlaced(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId)) return false;
            return SideHasLineage(CharacterRole.Guardian, lineageId) || SideHasLineage(CharacterRole.Echo, lineageId);
        }

        bool SideHasLineage(CharacterRole role, string lineageId) => FindLineageForm(role, lineageId) != null;

        /// <summary>한쪽에 놓인 이 계보의 형태 ID (없으면 null)</summary>
        string FindLineageForm(CharacterRole role, string lineageId)
        {
            var side = SideArray(role);
            for (int i = 0; i < side.Length; i++)
            {
                if (string.IsNullOrEmpty(side[i])) continue;
                if (role == CharacterRole.Echo && !IsEchoSlotUnlocked(i)) continue;
                if (_characters.Db.GetLineageOfForm(side[i])?.id == lineageId) return side[i];
            }
            return null;
        }

        public bool IsDispatched(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId) || _data.expeditions == null) return false;
            foreach (var expedition in _data.expeditions)
            {
                if (expedition?.lineageIds != null && expedition.lineageIds.Contains(lineageId)) return true;
            }
            return false;
        }

        // ───────────────────────────── 자동 편성 ─────────────────────────────

        /// <summary>
        /// 전투력 순으로 규칙에 맞게 자동 편성. 먼저 추천 역할대로 채우고, 남은 칸은 반대 역할로 채운다.
        /// 원정 중인 계보도 넣는다 (원정 중에도 함께 싸운다). 직접 놓은 횟수(PlacementsDone)는 세지 않는다.
        /// 고르기는 BuildAutoFormation(세이브를 바꾸지 않음)이 하고, 여기서는 그 결과를 세이브에 쓴 뒤 바뀌었으면 Changed.
        /// </summary>
        public void AutoFormation()
        {
            var guardians = new string[GuardianSlotCount];
            var echoes = new string[EchoSlotCount];
            BuildAutoFormation(guardians, echoes);

            var save = Save;
            bool changed = false;
            for (int i = 0; i < GuardianSlotCount; i++)
            {
                changed |= Normalize(save.guardians[i]) != guardians[i];
                save.guardians[i] = guardians[i];
            }
            for (int i = 0; i < EchoSlotCount; i++)
            {
                changed |= Normalize(save.echoes[i]) != echoes[i];
                save.echoes[i] = echoes[i];
            }
            if (changed) Changed?.Invoke();
        }

        /// <summary>
        /// 자동 편성이 놓을 편성을 배열에 채운다 (AutoFormation과 같은 고르기 · 세이브와 이벤트는 그대로, RM-2 P4-1 — PowerSystem.PreviewAutoFormation용).
        /// guardians는 GuardianSlotCount칸, echoes는 EchoSlotCount칸 이상이어야 하며 모두 비우고 채운다 (잠긴 덱 칸은 null).
        /// 배열이 null이거나 짧으면 아무것도 하지 않는다. 고를 때 목록을 만들므로 버튼·미리보기 때만 부른다 (매 프레임 금지).
        /// </summary>
        public void BuildAutoFormation(string[] guardians, string[] echoes)
        {
            if (guardians == null || echoes == null || guardians.Length < GuardianSlotCount || echoes.Length < EchoSlotCount) return;
            for (int i = 0; i < guardians.Length; i++) guardians[i] = null;
            for (int i = 0; i < echoes.Length; i++) echoes[i] = null;

            var candidates = new List<(CharacterFormDef form, string lineageId, float power)>();
            foreach (var form in _characters.GetUnlockedForms())
            {
                var lineage = form != null ? _characters.Db.GetLineageOfForm(form.id) : null;
                if (lineage == null) continue;
                candidates.Add((form, lineage.id, _characters.GetStats(form.id).Power));
            }
            candidates.Sort((a, b) =>
            {
                int byPower = b.power.CompareTo(a.power);
                return byPower != 0 ? byPower : string.CompareOrdinal(a.form.id, b.form.id);
            });

            var usedForms = new HashSet<string>();
            var guardianLineages = new HashSet<string>();
            var echoLineages = new HashSet<string>();
            int guardianCount = 0;
            int echoCount = 0;
            int echoLimit = UnlockedEchoSlots;

            bool TryAdd((CharacterFormDef form, string lineageId, float power) c, CharacterRole role)
            {
                if (usedForms.Contains(c.form.id)) return false;
                if (role == CharacterRole.Guardian)
                {
                    if (guardianCount >= GuardianSlotCount || guardianLineages.Contains(c.lineageId)) return false;
                    guardians[AutoGuardianOrder[guardianCount++]] = c.form.id;
                    guardianLineages.Add(c.lineageId);
                }
                else
                {
                    if (echoCount >= echoLimit || echoLineages.Contains(c.lineageId)) return false;
                    echoes[echoCount++] = c.form.id;
                    echoLineages.Add(c.lineageId);
                }
                usedForms.Add(c.form.id);
                return true;
            }

            // 1) 추천 역할 우선
            foreach (var c in candidates) TryAdd(c, c.form.recommendedRole);
            // 2) 남은 칸은 반대 역할로 (지킴이 방위가 비어 있으면 먼저 채운다)
            foreach (var c in candidates) TryAdd(c, CharacterRole.Guardian);
            foreach (var c in candidates) TryAdd(c, CharacterRole.Echo);
        }

        // ───────────────────────────── 시너지 ─────────────────────────────

        /// <summary>현재 편성의 계보 공명 시너지 목록 (지킴이와 잔향이 양쪽에 모두 나간 계보)</summary>
        public List<SynergyInfo> GetSynergies()
        {
            var result = new List<SynergyInfo>();
            var guardians = Save.guardians;
            for (int i = 0; i < guardians.Length; i++)
            {
                var lineage = string.IsNullOrEmpty(guardians[i]) ? null : _characters.Db.GetLineageOfForm(guardians[i]);
                if (lineage == null || !SideHasLineage(CharacterRole.Echo, lineage.id)) continue;
                if (result.Exists(s => s.Lineage == lineage)) continue;
                int resonance = Math.Max(0, _characters.GetResonanceLevel(lineage.id));
                result.Add(new SynergyInfo
                {
                    Lineage = lineage,
                    ResonanceLevel = resonance,
                    AttackBonusPercent = SynergyBonus(resonance),
                    Label = $"{lineage.name} 계보 공명 Lv.{resonance}",
                });
            }
            return result;
        }

        static float SynergyBonus(int resonance) => SynergyBaseBonusPercent + SynergyBonusPerResonance * resonance;

        /// <summary>시너지 합계 공격력 증가(%) (전투 공격력 배율 = 1 + 값/100)</summary>
        public float TeamAttackBonusPercent
        {
            get
            {
                float total = 0f;
                var guardians = Save.guardians;
                for (int i = 0; i < guardians.Length; i++)
                {
                    var lineage = string.IsNullOrEmpty(guardians[i]) ? null : _characters.Db.GetLineageOfForm(guardians[i]);
                    if (lineage == null || !SideHasLineage(CharacterRole.Echo, lineage.id)) continue;
                    // 같은 계보 지킴이는 한 칸뿐이므로 중복 합산되지 않는다
                    total += SynergyBonus(Math.Max(0, _characters.GetResonanceLevel(lineage.id)));
                }
                return total;
            }
        }

        /// <summary>배치 실패 안내 문구 (예: "지금은 직접 나가 있어요")</summary>
        public static string Describe(PlaceResult result) => result switch
        {
            PlaceResult.Ok => "편성했어요.",
            PlaceResult.InvalidSlot => "놓을 수 없는 칸이에요.",
            PlaceResult.SlotLocked => SlotLockedText,
            PlaceResult.NotOwned => "아직 만나지 못한 친구예요.",
            PlaceResult.FormLocked => "아직 해금하지 않은 형태예요. 진화 제작으로 해금할 수 있어요.",
            PlaceResult.OnOtherSide => "지금은 직접 나가 있어요. 이미 반대편에 등록된 캐릭터예요.",
            PlaceResult.LineageLimit => "같은 계보는 지킴이 1종 + 잔향이 1종까지 편성할 수 있어요.",
            // 더 이상 나오지 않는 결과 (원정 중에도 편성할 수 있다). 옛 호출부 호환용 문구
            PlaceResult.Dispatched => "원정 파견 중이에요.",
            _ => string.Empty,
        };
    }
}
