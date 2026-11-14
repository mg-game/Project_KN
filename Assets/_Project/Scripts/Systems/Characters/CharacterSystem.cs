using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 캐릭터 계보 보유/레벨/기억편/계보 공명/호감도/누적 기록 관리.
    /// 레벨·호감도는 계보 공유, 형태(진화형)는 전투 성향과 기본 능력치만 다르다.
    /// </summary>
    public class CharacterSystem
    {
        /// <summary>보유/레벨/공명/기억편/해금 형태/의상/호감도 등 계보 데이터가 바뀔 때</summary>
        public event Action Changed;
        /// <summary>
        /// (계보 ID, 기록 종류) 누적 기록(원정/잔향이 출격/지킴이 처치)이 늘었을 때.
        /// 전투 중 자주 일어나고 능력치와 무관하므로 Changed와 분리했다.
        /// </summary>
        public event Action<string, ProgressType> ProgressChanged;
        /// <summary>(계보 ID, 새 레벨) 계보 레벨이 올랐을 때 (Changed 다음에). 진행 기록기(LineageLevelUp)·팡파르용</summary>
        public event Action<string, int> LevelUp;
        /// <summary>(계보 ID, 새 공명 레벨) 계보 공명이 올랐을 때 (Changed 다음에)</summary>
        public event Action<string, int> ResonanceUp;
        /// <summary>계보를 얻었을 때 (새 계보 또는 중복 → 기억편, Changed 다음에)</summary>
        public event Action<GrantResult> Granted;

        /// <summary>
        /// 기능 개방 확인 (4차 훅, SPEC4 §5.1 F-03): 친구 레벨업(CharacterLevelUp 1-1)이 잠겼으면 TryLevelUp이 거절하고 CanLevelUp은 false.
        /// MetaSession.Connect가 FeatureGateSystem.IsUnlocked를 넣는다. null = 열림 (시스템만 만든 테스트). 확인이 실패하면 열림으로 본다
        /// </summary>
        public Func<FeatureId, bool> FeatureResolver;

        /// <summary>친구 레벨업이 열렸는지 (FeatureId.CharacterLevelUp, F-03). 잠긴 동안 버튼은 '1-1' 알약으로 남고 시스템은 거절한다</summary>
        public bool IsLevelUpUnlocked => IsFeatureOpen(FeatureId.CharacterLevelUp);

        readonly PlayerData _data;
        readonly GameConfig _config;
        readonly CharacterDatabase _db;
        readonly CurrencySystem _currency;
        readonly ITimeProvider _clock;

        public CharacterSystem(PlayerData data, GameConfig config, CharacterDatabase db, CurrencySystem currency, ITimeProvider clock)
        {
            _data = data;
            _config = config;
            _db = db;
            _currency = currency;
            _clock = clock;
        }

        public CharacterDatabase Db => _db;

        public bool IsOwned(string lineageId) => GetSave(lineageId) != null;

        /// <summary>보유 계보 목록 (등급 높은 순 → ID 순)</summary>
        public List<LineageDef> GetOwnedLineages()
        {
            var result = new List<LineageDef>();
            GetOwnedLineages(result);
            return result;
        }

        /// <summary>보유 계보 목록을 into에 채운다 (비우고 채움, GetOwnedLineages()와 같은 순서). 할당 없음 — 1초마다 부르는 강해지기 조언(StrengthenAdvisor)용</summary>
        public void GetOwnedLineages(List<LineageDef> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var lineageId in _data.lineages.Keys)
            {
                var lineage = _db.GetLineage(lineageId);
                if (lineage != null) into.Add(lineage);
            }
            into.Sort(LineageOrder);
        }

        /// <summary>보유하지 않았으면 null</summary>
        public LineageSave GetSave(string lineageId) =>
            lineageId != null && _data.lineages.TryGetValue(lineageId, out var save) ? save : null;

        /// <summary>보유 계보의 기본형이거나 진화 제작으로 해금한 형태인지</summary>
        public bool IsFormUnlocked(string formId)
        {
            var lineage = _db.GetLineageOfForm(formId);
            var save = lineage != null ? GetSave(lineage.id) : null;
            return save != null && (formId == lineage.baseFormId || save.unlockedForms.Contains(formId));
        }

        /// <summary>보유 계보의 해금된 형태 전체 (편성 목록용). 계보 순서 → 데이터상 형태 순서</summary>
        public List<CharacterFormDef> GetUnlockedForms()
        {
            var result = new List<CharacterFormDef>();
            foreach (var lineage in GetOwnedLineages())
            {
                foreach (var form in lineage.forms)
                {
                    if (form != null && IsFormUnlocked(form.id)) result.Add(form);
                }
            }
            return result;
        }

        /// <summary>
        /// 새 계보면 보유 + 기본형 해금(+ 기본 의상 지급), 이미 있으면 등급별 기억편으로 변환.
        /// 존재하지 않는 계보 ID면 null.
        /// </summary>
        public GrantResult Grant(string lineageId)
        {
            var lineage = _db.GetLineage(lineageId);
            if (lineage == null)
            {
                Debug.LogWarning($"[Characters] 존재하지 않는 계보: {lineageId}");
                return null;
            }

            var result = new GrantResult { Lineage = lineage };
            var save = GetSave(lineage.id);
            if (save == null)
            {
                // PRG-05: 새 친구는 친구들 평균의 80%에서 시작한다 (넣기 전에 잰다 — 새 친구는 평균에 들지 않는다). LevelUp은 울리지 않는다
                int startLevel = CatchUpLevel();
                var created = CreateSave(lineage);
                created.level = startLevel;
                _data.lineages[lineage.id] = created;
                result.IsNew = true;
                result.StartLevel = startLevel;
            }
            else
            {
                result.ShardsGained = GetDuplicateShards(lineage.grade);
                save.shards += result.ShardsGained;
            }
            NotifyChanged();
            Granted?.Invoke(result);
            return result;
        }

        /// <summary>중복 획득 시 변환되는 기억편 (등급별)</summary>
        public int GetDuplicateShards(ItemGrade grade) => _config.GetDuplicateShards(grade);

        /// <summary>새 친구 따라잡기 비율 (PRG-05: 80% = 4/5)</summary>
        const int CatchUpNumerator = 4;
        const int CatchUpDenominator = 5;

        /// <summary>
        /// 새 친구가 시작할 레벨 (PRG-05, SPEC4 §6.5) = max(1, floor(0.8 × 보유 계보 레벨 상위 3개의 평균)), 최대 레벨까지.
        /// 셋보다 적게 가졌으면 가진 것만 평균, 하나도 없으면 1 (시작 친구들은 Lv.1). 소환 · 출석 7일째 · 첫 만남 · 친구 초대권 ·
        /// MetaReward.Lineage 모두 Grant를 거치므로 같은 규칙이다. 할당 없음
        /// </summary>
        public int CatchUpLevel()
        {
            int first = 0, second = 0, third = 0, count = 0;
            foreach (var pair in _data.lineages)
            {
                var save = pair.Value;
                if (save == null || _db.GetLineage(pair.Key) == null) continue;
                int level = Math.Max(1, save.level);
                count++;
                if (level > first)
                {
                    third = second;
                    second = first;
                    first = level;
                }
                else if (level > second)
                {
                    third = second;
                    second = level;
                }
                else if (level > third)
                {
                    third = level;
                }
            }
            if (count <= 0) return 1;
            int used = Math.Min(3, count);
            long sum = (long)first + (used > 1 ? second : 0) + (used > 2 ? third : 0);
            long start = sum * CatchUpNumerator / ((long)CatchUpDenominator * used);
            return (int)Math.Max(1L, Math.Min(MaxLevel, start));
        }

        /// <summary>형태를 해금 상태로 만든다 (진화 제작 완료 시 EvolutionSystem이 호출). 미보유 계보면 무시</summary>
        public void UnlockForm(string formId)
        {
            var lineage = _db.GetLineageOfForm(formId);
            var save = lineage != null ? GetSave(lineage.id) : null;
            if (save != null && save.unlockedForms.Add(formId)) NotifyChanged();
        }

        // ── 레벨 (계보 공유) ──

        public int MaxLevel => Math.Max(1, _config.characterMaxLevel);

        /// <summary>계보 공유 레벨. 보유하지 않았으면 0</summary>
        public int GetLevel(string lineageId) => GetSave(lineageId)?.level ?? 0;

        public bool IsMaxLevel(string lineageId) => GetLevel(lineageId) >= MaxLevel;

        /// <summary>다음 레벨 비용(단추). 미보유이거나 최대 레벨이면 0</summary>
        public double GetLevelUpCost(string lineageId)
        {
            int level = GetLevel(lineageId);
            if (level <= 0 || level >= MaxLevel) return 0;
            return GetCostAtLevel(level);
        }

        /// <summary>fromLevel → fromLevel+1 비용 (정수 단추, 계보와 상관없음). 플레이 기록의 지출 줄(spend)도 이것으로 센다. 할당 없음</summary>
        public double GetCostAtLevel(int fromLevel)
        {
            if (fromLevel <= 0) return 0;
            double raw = _config.characterLevelUpBaseCost * Math.Pow(_config.characterLevelUpCostGrowth, fromLevel - 1);
            // 부동소수 오차(55.000000001 → 56)를 막고 정수 단추로 맞춘다
            return Math.Max(0, Math.Ceiling(raw - 1e-6));
        }

        /// <summary>지금 레벨에서 levels번 올리는 총 비용 (단추, 최대 레벨에서 멈춤). 미보유 · 최대 레벨 · levels ≤ 0이면 0. 할당 없음</summary>
        public double GetLevelUpCost(string lineageId, int levels)
        {
            int level = GetLevel(lineageId);
            if (level <= 0 || levels <= 0) return 0;
            double total = 0;
            int last = Math.Min(MaxLevel, level + levels);
            for (int from = level; from < last; from++) total += GetCostAtLevel(from);
            return total;
        }

        /// <summary>친구 레벨업에 쓰는 재화 (GameConfig.lineageLevelUpCurrency, 기본 단추: SYS_Economy §3 #8 E안 이음매)</summary>
        public CurrencyType LevelUpCurrency => _config.lineageLevelUpCurrency;

        /// <summary>
        /// 지금 가진 레벨업 재화 (강해지기 줄의 "모으는 데 걸리는 시간" 계산용. StrengthenAdvisor가 재화 시스템을 받지 않으므로 여기서 읽어 준다).
        /// 이름은 예전 그대로 (기본값이 단추). 레벨업 재화를 단추가 아닌 것으로 바꾸면 강해지기의 시간 계산(초당 단추 기준)도 같이 바꿔야 한다
        /// </summary>
        public double AvailableGold => _currency != null ? _currency.Get(LevelUpCurrency) : 0;

        /// <summary>지금 가진 레벨업 재화로 레벨업할 수 있는지 (빨간 점 표시용)</summary>
        public bool CanLevelUp(string lineageId) =>
            IsOwned(lineageId) && !IsMaxLevel(lineageId) && IsLevelUpUnlocked && _currency.Has(LevelUpCurrency, GetLevelUpCost(lineageId));

        /// <summary>단추로 레벨 +1. 미보유 · 최대 레벨 · 단추 부족 · 친구 레벨업 잠김(F-03, 1-1 전)이면 false</summary>
        public bool TryLevelUp(string lineageId)
        {
            if (!IsLevelUpUnlocked) return false;
            var save = GetSave(lineageId);
            if (save == null || save.level >= MaxLevel) return false;
            if (!_currency.TrySpend(LevelUpCurrency, GetLevelUpCost(lineageId))) return false;
            save.level++;
            NotifyChanged();
            LevelUp?.Invoke(lineageId, save.level);
            return true;
        }

        // ── 기억편 / 계보 공명 ──

        public int GetShards(string lineageId) => GetSave(lineageId)?.shards ?? 0;

        /// <summary>기억편을 소모한다. 미보유, 0 이하, 부족하면 아무것도 하지 않고 false</summary>
        public bool TrySpendShards(string lineageId, int amount)
        {
            var save = GetSave(lineageId);
            if (save == null || amount <= 0 || save.shards < amount) return false;
            save.shards -= amount;
            NotifyChanged();
            return true;
        }

        /// <summary>기억편을 더한다 (선택 기억편 넣기, 메타 보상). 미보유이거나 0 이하면 false</summary>
        public bool AddShards(string lineageId, int amount)
        {
            var save = GetSave(lineageId);
            if (save == null || amount <= 0) return false;
            save.shards += amount;
            NotifyChanged();
            return true;
        }

        public int MaxResonanceLevel => Math.Max(0, _config.maxResonanceLevel);

        public int GetResonanceLevel(string lineageId) => GetSave(lineageId)?.resonance ?? 0;

        /// <summary>다음 공명 레벨에 필요한 기억편. 최대면 0 (미보유도 0)</summary>
        public int GetResonanceCost(string lineageId)
        {
            var save = GetSave(lineageId);
            if (save == null || save.resonance >= MaxResonanceLevel) return 0;
            return Math.Max(1, _config.resonanceCostPerLevel * (save.resonance + 1));
        }

        /// <summary>지금 기억편으로 공명 레벨을 올릴 수 있는지 (빨간 점 표시용)</summary>
        public bool CanResonance(string lineageId)
        {
            int cost = GetResonanceCost(lineageId);
            return cost > 0 && GetShards(lineageId) >= cost;
        }

        public bool TryResonance(string lineageId)
        {
            if (!CanResonance(lineageId)) return false;
            var save = GetSave(lineageId);
            save.shards -= GetResonanceCost(lineageId);
            save.resonance++;
            NotifyChanged();
            ResonanceUp?.Invoke(lineageId, save.resonance);
            return true;
        }

        /// <summary>공명 레벨에 따른 계보 능력치 증가(%)</summary>
        public float GetResonanceBonusPercent(string lineageId) => GetResonanceLevel(lineageId) * _config.resonanceBonusPercent;

        /// <summary>
        /// 공명 레벨 level일 때의 계보 능력치 증가(%) (0 ~ 최대 공명 레벨로 맞춤). 상세 창 "+5% → +10%" 미리보기:
        /// GetResonanceBonusPercentAt(GetResonanceLevel(id) + 1). 최대 레벨이면 지금 값과 같다 (UI는 "(MAX)").
        /// </summary>
        public float GetResonanceBonusPercentAt(int level) => Math.Clamp(level, 0, MaxResonanceLevel) * _config.resonanceBonusPercent;

        // ── 호감도 ──

        public int GetAffinity(string lineageId) => GetSave(lineageId)?.affinity ?? 0;

        /// <summary>호감도를 올린다. 미보유이거나 0 이하면 무시</summary>
        public void AddAffinity(string lineageId, int amount)
        {
            var save = GetSave(lineageId);
            if (save == null || amount <= 0) return;
            save.affinity += amount;
            NotifyChanged();
        }

        // ── 누적 기록 (진화 조건) ──

        /// <summary>저장되는 누적 기록인지 (원정/잔향이 출격/지킴이 처치). 레벨/스테이지/던전은 현재 값으로 판정한다.</summary>
        public static bool IsCounter(ProgressType type) =>
            type == ProgressType.ExpeditionCount || type == ProgressType.EchoDeployCount || type == ProgressType.GuardianKillCount;

        /// <summary>
        /// 진화 조건 진행도. 계보 레벨은 현재 레벨, 스테이지/던전은 전역 최고 기록, 나머지는 계보별 누적 기록.
        /// </summary>
        public int GetProgress(string lineageId, ProgressType type)
        {
            switch (type)
            {
                case ProgressType.LineageLevel:
                    return GetLevel(lineageId);
                case ProgressType.StageReached:
                    return _data.battle.highestStage;
                case ProgressType.DungeonFloor:
                    return _data.dungeon.highestFloor;
                default:
                    var save = GetSave(lineageId);
                    return save != null && save.progress.TryGetValue(type, out var count) ? count : 0;
            }
        }

        /// <summary>누적 기록을 늘린다 (ProgressChanged 알림). 미보유, 0 이하, 누적 기록이 아닌 종류면 무시</summary>
        public void AddProgress(string lineageId, ProgressType type, int amount = 1)
        {
            var save = GetSave(lineageId);
            if (save == null || amount <= 0) return;
            if (!IsCounter(type))
            {
                Debug.LogWarning($"[Characters] {type}은(는) 누적 기록이 아니라 현재 값으로 판정합니다.");
                return;
            }
            save.progress[type] = GetProgress(lineageId, type) + amount;
            ProgressChanged?.Invoke(lineageId, type);
        }

        // ── 표시 / 능력치 ──

        /// <summary>
        /// 레벨/공명/의상 보유 보너스를 반영한 능력치. 없는 형태면 default.
        /// 공격력 = 기본 × (1 + 레벨당% × (레벨-1)) × (1 + 공명% + 보유 의상%),
        /// 체력 = 기본 × (1 + 레벨당% × (레벨-1)) × (1 + 공명%). 나머지는 형태 값 그대로.
        /// 미보유 계보는 레벨 1 기준 (도감 미리보기용).
        /// </summary>
        public CharacterStats GetStats(string formId)
        {
            var form = _db.GetForm(formId);
            var lineage = _db.GetLineageOfForm(formId);
            if (form == null || lineage == null) return default;

            int levelSteps = Math.Max(1, GetLevel(lineage.id)) - 1;
            float resonance = GetResonanceBonusPercent(lineage.id);
            float costume = GetCostumeAttackBonusPercent(lineage.id);
            return new CharacterStats
            {
                style = form.style,
                attack = form.attack * (1f + _config.attackPerLevelPercent * levelSteps / 100f) * (1f + (resonance + costume) / 100f),
                hp = form.hp * (1f + _config.hpPerLevelPercent * levelSteps / 100f) * (1f + resonance / 100f),
                attackInterval = form.attackInterval,
                range = form.range,
                arcDegrees = form.arcDegrees,
                moveSpeed = form.moveSpeed,
                summonCost = form.summonCost,
                lifetime = form.lifetime,
            };
        }

        /// <summary>이 계보의 보유 의상 공격력 보너스 합계(%)</summary>
        public float GetCostumeAttackBonusPercent(string lineageId)
        {
            var lineage = _db.GetLineage(lineageId);
            if (lineage == null) return 0f;
            float sum = 0f;
            foreach (var costume in lineage.costumes)
            {
                if (costume != null && costume.id != null && _data.unlockedCostumes.Contains(costume.id)) sum += costume.attackBonusPercent;
            }
            return sum;
        }

        /// <summary>기본형에 의상을 입혔으면 "밤지기 짝짝이"처럼 의상 이름, 아니면 형태 이름 (없는 형태면 ID 그대로)</summary>
        public string GetDisplayName(string formId)
        {
            var form = _db.GetForm(formId);
            if (form == null) return formId ?? string.Empty;
            var costume = GetWornCostume(form);
            return costume != null && !string.IsNullOrEmpty(costume.name) ? costume.name : form.name;
        }

        /// <summary>기본형에 의상을 입혔으면 의상 초상화, 아니면 형태 초상화 (없으면 null)</summary>
        public Sprite GetPortrait(string formId)
        {
            var form = _db.GetForm(formId);
            if (form == null) return null;
            var costume = GetWornCostume(form);
            return costume != null && costume.portrait != null ? costume.portrait : form.portrait;
        }

        /// <summary>장착 의상. 기본 외형이거나 저장값이 무효(미보유/다른 계보)면 null</summary>
        public CostumeDef GetEquippedCostume(string lineageId)
        {
            var costumeId = GetSave(lineageId)?.equippedCostume;
            if (string.IsNullOrEmpty(costumeId) || !_data.unlockedCostumes.Contains(costumeId)) return null;
            var owner = _db.GetLineageOfCostume(costumeId);
            return owner != null && owner.id == lineageId ? _db.GetCostume(costumeId) : null;
        }

        /// <summary>장착 의상 변경 (CostumeSystem이 보유 여부 검증 후 호출). null/빈 문자열 = 기본 외형, 다른 계보 의상은 무시</summary>
        public void SetEquippedCostume(string lineageId, string costumeId)
        {
            var save = GetSave(lineageId);
            if (save == null) return;
            if (string.IsNullOrEmpty(costumeId)) costumeId = null;
            else if (_db.GetLineageOfCostume(costumeId)?.id != lineageId) return;
            if (save.equippedCostume == costumeId) return;
            save.equippedCostume = costumeId;
            NotifyChanged();
        }

        /// <summary>보유 의상이 늘면 의상 보너스로 능력치가 바뀌므로 CostumeSystem이 알린다.</summary>
        internal void NotifyCostumesChanged() => NotifyChanged();

        /// <summary>새 계보 저장 데이터. 기본형 해금 + 처음부터 보유하는 의상(Default) 지급</summary>
        LineageSave CreateSave(LineageDef lineage)
        {
            var save = new LineageSave { obtainedUnix = TimeUtil.ToUnix(_clock.UtcNow) };
            if (!string.IsNullOrEmpty(lineage.baseFormId)) save.unlockedForms.Add(lineage.baseFormId);
            foreach (var costume in lineage.costumes)
            {
                if (costume != null && !string.IsNullOrEmpty(costume.id) && costume.unlockType == CostumeUnlockType.Default)
                    _data.unlockedCostumes.Add(costume.id);
            }
            return save;
        }

        /// <summary>기본형이 입고 있는 의상. 진화형이거나 기본 외형이면 null</summary>
        CostumeDef GetWornCostume(CharacterFormDef form)
        {
            var lineage = _db.GetLineageOfForm(form.id);
            return lineage != null && form.id == lineage.baseFormId ? GetEquippedCostume(lineage.id) : null;
        }

        /// <summary>FeatureResolver로 기능이 열렸는지 (null = 열림, 확인 중 예외 = 열림으로 보고 경고만 — 예외를 내지 않는다)</summary>
        bool IsFeatureOpen(FeatureId feature)
        {
            var resolver = FeatureResolver;
            if (resolver == null) return true;
            try
            {
                return resolver(feature);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Characters] 기능 개방 확인 실패 ({feature}): {e.Message}");
                return true;
            }
        }

        /// <summary>보유 계보 순서 (한 번 만든 비교자 — 정렬할 때 대리자 · 비교자를 새로 만들지 않는다)</summary>
        static readonly IComparer<LineageDef> LineageOrder = Comparer<LineageDef>.Create(CompareLineages);

        static int CompareLineages(LineageDef a, LineageDef b) =>
            a.grade != b.grade ? ((int)b.grade).CompareTo((int)a.grade) : string.CompareOrdinal(a.id, b.id);

        void NotifyChanged() => Changed?.Invoke();
    }
}
