using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 외형 변경(의상). 같은 캐릭터도 상황과 이야기로 다른 매력을 얻는다.
    /// 보유하면 계보 공격력 보너스, 장착하면 기본형의 이름/초상화가 바뀐다.
    /// 조건형 의상은 조건을 채운 뒤 TryUnlock으로 받고(빨간 점), 이벤트 상점 의상은 구매로만 얻는다.
    /// 시즌 의상(CostumeUnlockType.Season, 별빛 산책 수첩 보상)은 보상(MetaReward.Costume → Unlock)으로만 얻고 저절로 풀리지 않는다.
    /// </summary>
    public class CostumeSystem
    {
        /// <summary>수선집 야간 방문 플래그 (PlayerData.flags)</summary>
        public const string NightVisitFlag = "night_visit";
        /// <summary>야간 시작 (현지 시각, 포함)</summary>
        public const int NightStartHour = 20;
        /// <summary>야간 끝 (현지 시각, 미포함). 20:00 ~ 05:59가 야간</summary>
        public const int NightEndHour = 6;

        /// <summary>보유/장착 의상이 바뀌거나 야간 방문을 기록했을 때</summary>
        public event Action Changed;
        /// <summary>(의상 ID) 의상을 새로 얻었을 때 (Changed 다음에). 진행 기록기(CostumeUnlock)용</summary>
        public event Action<string> CostumeUnlocked;
        /// <summary>(계보 ID, 의상 ID 또는 null = 기본 외형) 의상을 갈아입었을 때</summary>
        public event Action<string, string> CostumeEquipped;

        /// <summary>
        /// 기능 개방 확인 (4차 훅, SPEC4 §5.1 F-03): 의상(Costumes 2-5)이 잠겼으면 TryEquip · TryUnlock이 거절하고 CountUnlockable(빨간 점)은 0.
        /// 목록과 조건(CanUnlock · GetProgressText)은 그대로 보인다. CheckNightVisit은 잠긴 동안에도 방문을 기록하므로, 2-5 뒤 처음 볼 때
        /// 야간 방문 의상을 받을 수 있다. 보상 · 상점 구매(Unlock)는 막지 않는다.
        /// MetaSession.Connect가 FeatureGateSystem.IsUnlocked를 넣는다. null = 열림. 확인이 실패하면 열림으로 본다
        /// </summary>
        public Func<FeatureId, bool> FeatureResolver;

        /// <summary>의상 기능이 열렸는지 (FeatureId.Costumes, F-03)</summary>
        public bool IsFeatureUnlocked
        {
            get
            {
                var resolver = FeatureResolver;
                if (resolver == null) return true;
                try
                {
                    return resolver(FeatureId.Costumes);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Costumes] 기능 개방 확인 실패: {e.Message}");
                    return true;
                }
            }
        }

        readonly PlayerData _data;
        readonly CharacterDatabase _db;
        readonly CharacterSystem _characters;
        readonly ITimeProvider _clock;

        public CostumeSystem(PlayerData data, CharacterDatabase db, CharacterSystem characters, ITimeProvider clock)
        {
            _data = data;
            _db = db;
            _characters = characters;
            _clock = clock;
        }

        /// <summary>의상 데이터. 없으면 null</summary>
        public CostumeDef Get(string costumeId) => _db.GetCostume(costumeId);

        public bool IsUnlocked(string costumeId) => costumeId != null && _data.unlockedCostumes.Contains(costumeId);

        public bool HasVisitedAtNight => _data.flags.Contains(NightVisitFlag);

        /// <summary>지금 현지 시각이 야간인지 (접속 중인 지금이 곧 야간 방문)</summary>
        public bool IsNightNow => IsNight(_clock.UtcNow.ToLocalTime());

        /// <summary>
        /// 야간 방문 조건 충족 여부. 기록된 방문이 있거나, 지금이 야간이면 충족이다.
        /// 저녁에 접속해 그대로 밤까지 플레이해도(접속/복귀 시점 검사를 놓쳐도) 바로 받을 수 있다.
        /// </summary>
        bool NightVisitMet => HasVisitedAtNight || IsNightNow;

        /// <summary>
        /// 조건형 의상의 조건 충족 여부 (EventShop 의상은 상점 구매로만 해금).
        /// 이미 보유했거나 계보를 보유하지 않았으면 false.
        /// </summary>
        public bool CanUnlock(string costumeId)
        {
            var costume = _db.GetCostume(costumeId);
            var lineage = _db.GetLineageOfCostume(costumeId);
            if (costume == null || lineage == null || IsUnlocked(costumeId) || !_characters.IsOwned(lineage.id)) return false;

            switch (costume.unlockType)
            {
                case CostumeUnlockType.Default:
                    return true;
                case CostumeUnlockType.NightVisit:
                    return NightVisitMet;
                case CostumeUnlockType.StageReached:
                case CostumeUnlockType.ExpeditionCount:
                case CostumeUnlockType.Resonance:
                    return GetCurrent(costume, lineage) >= costume.unlockTarget;
                case CostumeUnlockType.FormUnlocked:
                    return _characters.IsFormUnlocked(costume.unlockTargetId);
                case CostumeUnlockType.Season:
                    // 별빛 산책 수첩 30칸 보상으로만 (MetaReward.Costume) — 조건으로 받지 않는다
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>예: "스테이지 1-3 / 2-5", "원정 3/5회", "이벤트 상점에서 구매", "보유 중". 없는 의상이면 빈 문자열</summary>
        public string GetProgressText(string costumeId)
        {
            var costume = _db.GetCostume(costumeId);
            var lineage = _db.GetLineageOfCostume(costumeId);
            if (costume == null || lineage == null) return string.Empty;
            if (IsUnlocked(costumeId)) return "보유 중";
            if (!_characters.IsOwned(lineage.id)) return $"{lineage.name} 획득 필요";

            int target = costume.unlockTarget;
            int current = Math.Min(GetCurrent(costume, lineage), target);
            switch (costume.unlockType)
            {
                case CostumeUnlockType.Default:
                    return "기본 의상";
                case CostumeUnlockType.NightVisit:
                    if (HasVisitedAtNight) return "야간 방문 완료";
                    return IsNightNow ? "지금 야간 방문 중" : $"밤 {NightStartHour - 12}시~새벽 {NightEndHour}시에 수선점 방문";
                case CostumeUnlockType.EventShop:
                    return "'별이 머문 밤 산책' 이벤트 상점에서 구매";
                case CostumeUnlockType.Season:
                    return "별빛 산책 수첩 보상";
                case CostumeUnlockType.StageReached:
                    // 다른 화면과 같은 "챕터-번호" 표기. 진행도는 도달 기준 (지금 도달 / 목표 도달) —
                    // 획득 방법 문구는 같은 목표를 클리어 기준으로 쓴다 (도달 2-5 = "스테이지 2-4를 클리어하면", M39)
                    return $"스테이지 {BattleSystem.FormatStage(current)} / {BattleSystem.FormatStage(target)}";
                case CostumeUnlockType.ExpeditionCount:
                    return $"원정 {current}/{target}회";
                case CostumeUnlockType.Resonance:
                    return $"계보 공명 Lv.{current}/{target}";
                case CostumeUnlockType.FormUnlocked:
                    var form = _db.GetForm(costume.unlockTargetId);
                    string formName = form != null ? form.name : costume.unlockTargetId;
                    return _characters.IsFormUnlocked(costume.unlockTargetId) ? $"{formName} 해금 완료" : $"{formName} 진화 해금 필요";
                default:
                    return string.Empty;
            }
        }

        /// <summary>조건 충족 시 해금</summary>
        public bool TryUnlock(string costumeId)
        {
            if (!IsFeatureUnlocked || !CanUnlock(costumeId)) return false;
            // 지금이 야간이라 충족한 경우에도 방문 기록을 남긴다 (Changed는 Unlock에서 한 번만)
            if (_db.GetCostume(costumeId).unlockType == CostumeUnlockType.NightVisit) _data.flags.Add(NightVisitFlag);
            Unlock(costumeId);
            return true;
        }

        /// <summary>조건 없이 해금 (상점 구매 등). 없는 의상이거나 이미 보유하면 무시</summary>
        public void Unlock(string costumeId)
        {
            if (_db.GetCostume(costumeId) == null)
            {
                Debug.LogWarning($"[Costumes] 존재하지 않는 의상: {costumeId}");
                return;
            }
            if (!_data.unlockedCostumes.Add(costumeId)) return;
            Changed?.Invoke();
            // 보유 의상 보너스로 계보 공격력이 바뀐다
            _characters.NotifyCostumesChanged();
            CostumeUnlocked?.Invoke(costumeId);
        }

        /// <summary>costumeId가 null이면 기본 외형. 보유 계보의, 보유한 그 계보 의상만 장착할 수 있다</summary>
        public bool TryEquip(string lineageId, string costumeId)
        {
            if (!IsFeatureUnlocked) return false;
            var save = _characters.GetSave(lineageId);
            if (save == null) return false;
            if (string.IsNullOrEmpty(costumeId)) costumeId = null;
            else if (_db.GetLineageOfCostume(costumeId)?.id != lineageId || !IsUnlocked(costumeId)) return false;

            if (save.equippedCostume == costumeId) return true;
            _characters.SetEquippedCostume(lineageId, costumeId);
            Changed?.Invoke();
            CostumeEquipped?.Invoke(lineageId, costumeId);
            return true;
        }

        /// <summary>장착 의상 ID. 기본 외형이면 null</summary>
        public string GetEquipped(string lineageId) => _characters.GetEquippedCostume(lineageId)?.id;

        /// <summary>접속/복귀 시 호출. 현지 20:00~05:59면 "night_visit" 플래그를 세운다 (수선집 야간 방문)</summary>
        public void CheckNightVisit()
        {
            if (!IsNight(_clock.UtcNow.ToLocalTime())) return;
            if (_data.flags.Add(NightVisitFlag)) Changed?.Invoke();
        }

        /// <summary>현지 시각이 야간(20:00 ~ 05:59)인지</summary>
        public static bool IsNight(DateTime localTime) => localTime.Hour >= NightStartHour || localTime.Hour < NightEndHour;

        /// <summary>조건을 새로 충족한 의상 수 (빨간 점 표시용)</summary>
        public int CountUnlockable()
        {
            if (!IsFeatureUnlocked) return 0;
            int count = 0;
            foreach (var lineage in _characters.GetOwnedLineages()) count += CountUnlockable(lineage);
            return count;
        }

        /// <summary>한 계보에서 조건을 새로 충족한 의상 수 (캐릭터 카드 빨간 점용)</summary>
        public int CountUnlockable(string lineageId)
        {
            var lineage = _db.GetLineage(lineageId);
            return lineage != null && IsFeatureUnlocked ? CountUnlockable(lineage) : 0;
        }

        int CountUnlockable(LineageDef lineage)
        {
            int count = 0;
            foreach (var costume in lineage.costumes)
            {
                if (costume != null && CanUnlock(costume.id)) count++;
            }
            return count;
        }

        /// <summary>수치형 조건(스테이지/원정/공명)의 현재 값. 그 외 종류는 0</summary>
        int GetCurrent(CostumeDef costume, LineageDef lineage) => costume.unlockType switch
        {
            CostumeUnlockType.StageReached => _data.battle.highestStage,
            CostumeUnlockType.ExpeditionCount => _characters.GetProgress(lineage.id, ProgressType.ExpeditionCount),
            CostumeUnlockType.Resonance => _characters.GetResonanceLevel(lineage.id),
            _ => 0,
        };
    }
}
