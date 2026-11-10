using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 진화 기억도. 가챠 → 진화 단서 발견 → 재료 수집 → 진화 제작(확정) → 편성.
    /// 진화 결과는 랜덤이 아닌 확정 제작이며, 이전 형태도 계속 쓸 수 있다.
    /// 기억편으로 희귀 재료 대체 / 진화 가속(조건 채우기)이 가능하다.
    /// 두 사용처 모두 실제 가방/누적 기록은 건드리지 않고 형태별 기록(LineageSave.conditionBonus)에만 남겨,
    /// 기억편(계보 전용 자원)이 다른 레시피나 다른 계보의 진화에 쓰이지 않게 한다.
    /// </summary>
    public class EvolutionSystem
    {
        /// <summary>진화 제작으로 형태가 해금됐을 때</summary>
        public event Action<CharacterFormDef> FormUnlocked;

        /// <summary>
        /// 기능 개방 확인 (4차 훅, SPEC4 §5.1 F-03): 진화 제작(Evolution 1-10)이 잠겼으면 TryCraft · TrySubstitute · TryAccelerate가 거절하고
        /// CountCraftable(빨간 점)은 0. 진화 기억도와 레시피(Check · GetState)는 그대로 보인다 (가이드 퀘스트 23이 본다).
        /// MetaSession.Connect가 FeatureGateSystem.IsUnlocked를 넣는다. null = 열림. 확인이 실패하면 열림으로 본다
        /// </summary>
        public Func<FeatureId, bool> FeatureResolver;

        /// <summary>진화 제작이 열렸는지 (FeatureId.Evolution, F-03)</summary>
        public bool IsCraftUnlocked
        {
            get
            {
                var resolver = FeatureResolver;
                if (resolver == null) return true;
                try
                {
                    return resolver(FeatureId.Evolution);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Evolution] 기능 개방 확인 실패: {e.Message}");
                    return true;
                }
            }
        }

        readonly PlayerData _data;
        readonly GameConfig _config;
        readonly CharacterSystem _characters;
        readonly InventorySystem _inventory;
        readonly ItemDatabase _items;

        public EvolutionSystem(PlayerData data, GameConfig config, CharacterSystem characters, InventorySystem inventory, ItemDatabase items)
        {
            _data = data;
            _config = config;
            _characters = characters;
            _inventory = inventory;
            _items = items;
        }

        CharacterDatabase Db => _characters.Db;

        /// <summary>
        /// 해금 = 기본형 또는 제작 완료, 단서 발견 = 부모 형태 해금 + 계보 레벨 ≥ 단서 레벨, 그 외(미보유 계보 포함) 미발견
        /// </summary>
        public EvolutionState GetState(string formId)
        {
            var form = Db.GetForm(formId);
            var lineage = Db.GetLineageOfForm(formId);
            if (form == null || lineage == null || !_characters.IsOwned(lineage.id)) return EvolutionState.Undiscovered;
            if (_characters.IsFormUnlocked(formId)) return EvolutionState.Unlocked;

            bool parentUnlocked = string.IsNullOrEmpty(form.parentFormId) || _characters.IsFormUnlocked(form.parentFormId);
            return parentUnlocked && _characters.GetLevel(lineage.id) >= form.clueLevel
                ? EvolutionState.ClueFound
                : EvolutionState.Undiscovered;
        }

        /// <summary>
        /// 재료/조건 진행 상황과 제작 가능 여부. 없는 형태면 null.
        /// 재료의 Have는 가방 보유량 + 이 레시피에 기억편으로 채운 양이다.
        /// Conditions의 인덱스는 형태 데이터의 조건 인덱스와 같다 (GetConditionProgress / TryAccelerate에 그대로 사용).
        /// </summary>
        public EvolutionCheck Check(string formId)
        {
            var form = Db.GetForm(formId);
            var lineage = Db.GetLineageOfForm(formId);
            if (form == null || lineage == null) return null;

            var check = new EvolutionCheck { Form = form, Lineage = lineage, State = GetState(formId) };
            foreach (var cost in form.materials)
            {
                if (cost == null || string.IsNullOrEmpty(cost.itemId) || cost.count <= 0) continue;
                // 같은 재료가 여러 줄이면 한 줄로 합쳐 실제 소모량과 맞춘다
                var status = check.Materials.Find(m => m.ItemId == cost.itemId);
                if (status != null) status.Need += cost.count;
                else check.Materials.Add(new MaterialStatus { ItemId = cost.itemId, Have = GetAvailable(lineage.id, formId, cost.itemId), Need = cost.count });
            }
            for (int i = 0; i < form.conditions.Count; i++)
            {
                var condition = form.conditions[i];
                check.Conditions.Add(new ConditionStatus
                {
                    Condition = condition,
                    Progress = GetConditionProgress(formId, i),
                    Label = FormatCondition(condition),
                });
            }
            check.CanCraft = check.State == EvolutionState.ClueFound
                && check.Materials.TrueForAll(m => m.Enough)
                && check.Conditions.TrueForAll(c => c.Done);
            return check;
        }

        /// <summary>조건 진행도 (가속분 포함, 0 ~ 목표치). 없는 형태/조건이면 0</summary>
        public int GetConditionProgress(string formId, int conditionIndex)
        {
            var condition = GetCondition(formId, conditionIndex, out var lineage);
            if (condition == null) return 0;
            long progress = (long)_characters.GetProgress(lineage.id, condition.type) + GetBonus(lineage.id, formId, conditionIndex);
            return (int)Math.Max(0, Math.Min(condition.target, progress));
        }

        /// <summary>
        /// 재료를 소모하고 형태를 해금한다. 단서 발견 상태 + 재료/조건 충족이어야 한다.
        /// 기억편으로 채운 양만큼은 가방에서 빼지 않는다.
        /// </summary>
        public bool TryCraft(string formId)
        {
            if (!IsCraftUnlocked) return false;
            var check = Check(formId);
            if (check == null || !check.CanCraft) return false;

            // Check에서 (가방 + 대체분) ≥ 합산 필요량을 확인했으므로 소모는 실패하지 않는다
            foreach (var material in check.Materials)
            {
                int fromBag = Math.Max(0, material.Need - GetSubstituted(check.Lineage.id, formId, material.ItemId));
                if (fromBag > 0) _inventory.TryConsume(material.ItemId, fromBag);
            }
            // 가속분과 재료 대체분을 함께 정리한다
            ClearBonus(check.Lineage.id, formId);
            _characters.UnlockForm(formId);
            FormUnlocked?.Invoke(check.Form);
            return true;
        }

        // ── 기억편 사용처: 희귀 재료 대체 ──

        /// <summary>재료 1개를 기억편으로 대체하는 비용. 대체 불가(일반 등급/재료 아님/없는 아이템)면 0</summary>
        public int GetSubstituteCost(string itemId)
        {
            var item = _items.Get(itemId);
            if (item == null || item.category != ItemCategory.Material || item.grade < ItemGrade.Rare) return 0;
            return _config.GetSubstituteShardCost(item.grade);
        }

        /// <summary>레시피 기준으로 모자란 재료 수 (가방 보유량 + 기억편 대체분 기준). 레시피에 없는 재료면 0</summary>
        public int GetShortfall(string formId, string itemId)
        {
            var form = Db.GetForm(formId);
            var lineage = Db.GetLineageOfForm(formId);
            if (form == null || lineage == null || string.IsNullOrEmpty(itemId)) return 0;
            int need = 0;
            foreach (var cost in form.materials)
            {
                if (cost != null && cost.itemId == itemId) need += Math.Max(0, cost.count);
            }
            return Math.Max(0, need - GetAvailable(lineage.id, formId, itemId));
        }

        /// <summary>이 형태의 레시피에 기억편으로 채워 둔 재료 수 (가방에는 들어가지 않는다). 없으면 0</summary>
        public int GetSubstituted(string formId, string itemId)
        {
            var lineage = Db.GetLineageOfForm(formId);
            return lineage != null ? GetSubstituted(lineage.id, formId, itemId) : 0;
        }

        /// <summary>
        /// 형태가 속한 계보의 기억편으로 이 레시피의 부족한 재료를 count개 채운다.
        /// 단서 발견 상태에서, 레시피의 부족분 이하로만 가능하다.
        /// 채운 양은 가방에 넣지 않고 이 형태의 레시피 전용으로 기록해, 다른 레시피(다른 계보 포함)에는 쓸 수 없다.
        /// 기록은 제작할 때 재료 대신 쓰이고 사라진다.
        /// </summary>
        public bool TrySubstitute(string formId, string itemId, int count)
        {
            if (!IsCraftUnlocked) return false;
            if (count <= 0 || GetState(formId) != EvolutionState.ClueFound) return false;
            if (count > GetShortfall(formId, itemId)) return false;
            int unitCost = GetSubstituteCost(itemId);
            if (unitCost <= 0) return false;

            long total = (long)unitCost * count;
            var lineage = Db.GetLineageOfForm(formId);
            if (total > int.MaxValue || _characters.GetShards(lineage.id) < total) return false;

            // 대체분을 먼저 기록해 기억편 소모의 Changed를 받는 쪽이 갱신된 보유량을 보게 한다
            var bonus = _data.lineages[lineage.id].conditionBonus;
            bonus[SubstituteKey(formId, itemId)] = GetSubstituted(lineage.id, formId, itemId) + count;
            _characters.TrySpendShards(lineage.id, (int)total);
            return true;
        }

        // ── 기억편 사용처: 진화 가속 ──

        /// <summary>조건을 기억편으로 채울 수 있는 종류인지 (원정/잔향이 출격/지킴이 처치). 레벨/스테이지/던전은 직접 달성해야 한다</summary>
        public bool CanAccelerate(ProgressType type) => GetAccelerateUnitCost(type) > 0;

        /// <summary>조건의 남은 진행도를 기억편으로 채우는 비용 (올림). 이미 충족했거나 가속 불가 조건이거나 단서 발견 전이면 0</summary>
        public int GetAccelerateCost(string formId, int conditionIndex)
        {
            var condition = GetCondition(formId, conditionIndex, out _);
            if (condition == null || GetState(formId) != EvolutionState.ClueFound) return 0;
            double unitCost = GetAccelerateUnitCost(condition.type);
            int remaining = condition.target - GetConditionProgress(formId, conditionIndex);
            if (!(unitCost > 0) || remaining <= 0) return 0;

            // 부동소수 오차(0.2 × 100 = 20.000000004 → 21)를 막고 올림
            double cost = Math.Ceiling(unitCost * remaining - 1e-6);
            return cost >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)cost);
        }

        /// <summary>남은 진행도를 기억편으로 한 번에 채운다. 실제 누적 기록은 그대로 두고 가속분만 따로 저장한다.</summary>
        public bool TryAccelerate(string formId, int conditionIndex)
        {
            if (!IsCraftUnlocked) return false;
            int cost = GetAccelerateCost(formId, conditionIndex);
            if (cost <= 0) return false;
            var condition = GetCondition(formId, conditionIndex, out var lineage);
            if (_characters.GetShards(lineage.id) < cost) return false;

            int remaining = condition.target - GetConditionProgress(formId, conditionIndex);
            var bonus = _data.lineages[lineage.id].conditionBonus;
            // 진행도를 먼저 채워 기억편 소모의 Changed를 받는 쪽이 갱신된 진행도를 보게 한다
            bonus[BonusKey(formId, conditionIndex)] = GetBonus(lineage.id, formId, conditionIndex) + remaining;
            _characters.TrySpendShards(lineage.id, cost);
            return true;
        }

        // ── 빨간 점 ──

        /// <summary>지금 제작 가능한 진화 수 (빨간 점 표시용)</summary>
        public int CountCraftable()
        {
            if (!IsCraftUnlocked) return 0;
            int count = 0;
            foreach (var lineage in _characters.GetOwnedLineages()) count += CountCraftable(lineage);
            return count;
        }

        /// <summary>한 계보에서 지금 제작 가능한 진화 수 (캐릭터 카드 빨간 점용)</summary>
        public int CountCraftable(string lineageId)
        {
            var lineage = Db.GetLineage(lineageId);
            return lineage != null && _characters.IsOwned(lineageId) && IsCraftUnlocked ? CountCraftable(lineage) : 0;
        }

        /// <summary>예: "원정 10회", "잔향이 출격 30회", "캐릭터 레벨 20 달성", "스테이지 2-10 도달"</summary>
        public static string FormatCondition(EvolutionCondition condition)
        {
            if (condition == null) return string.Empty;
            int n = condition.target;
            return condition.type switch
            {
                ProgressType.ExpeditionCount => $"원정 {n}회",
                ProgressType.EchoDeployCount => $"잔향이 출격 {n}회",
                ProgressType.LineageLevel => $"캐릭터 레벨 {n} 달성",
                // 스테이지는 다른 화면과 같은 "챕터-번호" 표기 (25 → 3-5)
                ProgressType.StageReached => $"스테이지 {BattleSystem.FormatStage(n)} 도달",
                ProgressType.DungeonFloor => $"잔향이 던전 {n}층 돌파",
                ProgressType.GuardianKillCount => $"지킴이 처치 {n}회",
                _ => $"{condition.type} {n}",
            };
        }

        int CountCraftable(LineageDef lineage)
        {
            int count = 0;
            foreach (var form in lineage.forms)
            {
                if (form != null && GetState(form.id) == EvolutionState.ClueFound && Check(form.id).CanCraft) count++;
            }
            return count;
        }

        double GetAccelerateUnitCost(ProgressType type) => type switch
        {
            ProgressType.ExpeditionCount => _config.accelerateExpeditionCost,
            ProgressType.EchoDeployCount => _config.accelerateEchoDeployCost,
            ProgressType.GuardianKillCount => _config.accelerateGuardianKillCost,
            _ => 0,
        };

        EvolutionCondition GetCondition(string formId, int index, out LineageDef lineage)
        {
            var form = Db.GetForm(formId);
            lineage = Db.GetLineageOfForm(formId);
            if (form == null || lineage == null || index < 0 || index >= form.conditions.Count) return null;
            return form.conditions[index];
        }

        /// <summary>진화 가속으로 채운 진행도 (계보 저장 데이터의 conditionBonus)</summary>
        int GetBonus(string lineageId, string formId, int conditionIndex) =>
            _data.lineages.TryGetValue(lineageId, out var save) && save.conditionBonus.TryGetValue(BonusKey(formId, conditionIndex), out var bonus)
                ? bonus
                : 0;

        /// <summary>제작에 쓸 수 있는 재료 수 = 가방 보유량 + 이 레시피의 기억편 대체분</summary>
        int GetAvailable(string lineageId, string formId, string itemId)
        {
            long available = (long)_inventory.GetCount(itemId) + GetSubstituted(lineageId, formId, itemId);
            return (int)Math.Min(int.MaxValue, available);
        }

        /// <summary>기억편으로 채운 재료 수 (계보 저장 데이터의 conditionBonus, 키 = SubstituteKey)</summary>
        int GetSubstituted(string lineageId, string formId, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_data.lineages.TryGetValue(lineageId, out var save)) return 0;
            // 기록이 없으면 키 문자열을 만들지 않는다 (빨간 점 갱신마다 불린다)
            if (save.conditionBonus.Count == 0) return 0;
            return save.conditionBonus.TryGetValue(SubstituteKey(formId, itemId), out var count) ? Math.Max(0, count) : 0;
        }

        /// <summary>제작이 끝난 형태의 가속/재료 대체 기록은 더 쓰이지 않으므로 지운다.</summary>
        void ClearBonus(string lineageId, string formId)
        {
            if (!_data.lineages.TryGetValue(lineageId, out var save)) return;
            string prefix = formId + "/";
            var keys = new List<string>();
            foreach (var key in save.conditionBonus.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal)) keys.Add(key);
            }
            foreach (var key in keys) save.conditionBonus.Remove(key);
        }

        static string BonusKey(string formId, int conditionIndex) => $"{formId}/{conditionIndex}";

        /// <summary>재료 대체 기록 키. 조건 인덱스(숫자) 키와 겹치지 않고, 같은 "{formId}/" 접두라 제작 시 함께 지워진다</summary>
        static string SubstituteKey(string formId, string itemId) => $"{formId}/item:{itemId}";
    }
}
