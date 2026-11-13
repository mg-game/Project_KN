using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 호감도 쓸모 (CR-10, SPEC4 §6.3): 누적 호감도(LineageSave.affinity) 단계 Lv.1~10 (AffinityConfig.thresholds), 단계 보상 별조각 gemPerLevel × 단계,
    /// 짝수 단계(2/4/6/8/10)마다 기억 조각 이야기 한 줄, 전투 중 쓰다듬기 (계보마다 하루 petsPerDay번 +petAmount, 튜토리얼을 마친 뒤 —
    /// 그 뒤로도 몸짓과 말풍선은 나오지만 호감도는 오르지 않는다). 하루는 DailyClock 날 키(05:00) 기준.
    /// 빨간 점 RedDotPaths.LineageAffinity(계보) = 받을 단계가 있음. ProgressTracker.ExtraConditionValue로 MetaConditionType.AffinityLevelCount
    /// (targetId 단계 이상인 계보 수)를 답한다 (앞 모듈 답에 이어 부름).
    /// </summary>
    public class AffinitySystem : IMetaModule
    {
        /// <summary>받은 단계 · 쓰다듬기 횟수가 바뀜</summary>
        public event Action Changed;

        /// <summary>기억 조각 이야기 수 (짝수 단계마다 하나)</summary>
        public const int StoryLineCount = 5;

        static readonly AffinityConfig DefaultConfig = new();

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly CharacterSystem _characters;
        readonly MetaRewardService _rewards;
        readonly DailyClock _clock;
        readonly ProgressTracker _progress;

        bool _connected;
        int _petLine;
        Func<MetaCondition, long?> _previousCondition;
        RedDotSystem _dots;
        readonly Dictionary<string, string> _dotPaths = new();

        public AffinitySystem(PlayerData data, MetaDatabase db, CharacterSystem characters, MetaRewardService rewards, DailyClock clock,
            ProgressTracker progress)
        {
            _data = data;
            _db = db;
            _characters = characters;
            _rewards = rewards;
            _clock = clock;
            _progress = progress;
        }

        AffinityConfig Config => _db != null && _db.affinity != null ? _db.affinity : DefaultConfig;

        AffinitySave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.affinity ??= new AffinitySave();
                _data.meta.affinity.Sanitize();
                return _data.meta.affinity;
            }
        }

        /// <summary>최고 단계 (thresholds 길이, 없으면 1)</summary>
        public int MaxLevel
        {
            get
            {
                var thresholds = Config.thresholds;
                return thresholds != null && thresholds.Length > 0 ? thresholds.Length : 1;
            }
        }

        /// <summary>지금 호감도 단계 (1~최고). 미보유는 1</summary>
        public int GetLevel(string lineageId)
        {
            var save = _characters?.GetSave(lineageId);
            return save == null ? 1 : LevelOf(save.affinity);
        }

        /// <summary>누적 호감도 → 단계 (thresholds[단계−1] 이상이면 그 단계)</summary>
        public int LevelOf(long affinity)
        {
            var thresholds = Config.thresholds;
            if (thresholds == null || thresholds.Length == 0) return 1;
            int level = 1;
            for (int i = 1; i < thresholds.Length; i++)
            {
                if (affinity >= thresholds[i]) level = i + 1;
                else break;
            }
            return level;
        }

        /// <summary>보상을 받은 단계 (1 = 아직 없음)</summary>
        public int GetClaimedLevel(string lineageId)
        {
            var save = Save;
            if (save == null || string.IsNullOrEmpty(lineageId) || !save.claimedLevel.TryGetValue(lineageId, out int level)) return 1;
            return Math.Max(1, level);
        }

        /// <summary>그 단계가 시작되는 누적 호감도 (1단계 = 0)</summary>
        public long GetLevelStart(int level)
        {
            var thresholds = Config.thresholds;
            if (thresholds == null || thresholds.Length == 0) return 0;
            int index = Math.Max(0, Math.Min(thresholds.Length - 1, level - 1));
            return Math.Max(0, thresholds[index]);
        }

        /// <summary>다음 단계까지 필요한 누적 호감도 (최고 단계면 그 단계 시작 값)</summary>
        public long GetNextThreshold(string lineageId)
        {
            int level = GetLevel(lineageId);
            return level >= MaxLevel ? GetLevelStart(MaxLevel) : GetLevelStart(level + 1);
        }

        /// <summary>닿았지만 받지 않은 단계가 있는지 (보유 계보만)</summary>
        public bool HasClaimable(string lineageId) => _characters != null && _characters.IsOwned(lineageId) && GetLevel(lineageId) > GetClaimedLevel(lineageId);

        /// <summary>닿은 단계 보상을 모두 받는다 (단계마다 별조각 gemPerLevel × 단계). 받을 것이 없으면 null</summary>
        public List<MetaReward> TryClaim(string lineageId)
        {
            if (!HasClaimable(lineageId)) return null;
            var save = Save;
            if (save == null) return null;
            int from = GetClaimedLevel(lineageId);
            int to = GetLevel(lineageId);
            long gems = 0;
            int perLevel = Math.Max(0, Config.gemPerLevel);
            for (int level = from + 1; level <= to; level++) gems += (long)perLevel * level;
            save.claimedLevel[lineageId] = to;
            var granted = gems > 0 && _rewards != null
                ? _rewards.Grant(new List<MetaReward> { MetaReward.Gem(gems) }) ?? new List<MetaReward>()
                : gems > 0 ? new List<MetaReward> { MetaReward.Gem(gems) } : new List<MetaReward>();
            AfterChanged(lineageId);
            return granted;
        }

        /// <summary>오늘 남은 쓰다듬기 (호감도가 오르는 횟수). 미보유는 0</summary>
        public int PetsLeftToday(string lineageId)
        {
            if (_characters == null || !_characters.IsOwned(lineageId)) return 0;
            int perDay = Math.Max(0, Config.petsPerDay);
            var save = Save;
            if (save == null || !save.pets.TryGetValue(lineageId, out var day) || day == null || day.dayKey != TodayKey) return perDay;
            return Math.Max(0, perDay - day.count);
        }

        /// <summary>
        /// 쓰다듬기 (line = 말풍선 한 줄 — 콘텐츠가 없으면 null, gained = 호감도가 올랐는지). 미보유이거나 튜토리얼 전이면 false.
        /// 오늘 횟수를 다 썼으면 true + gained = false (몸짓 · 말풍선만)
        /// </summary>
        public bool TryPet(string lineageId, out string line, out bool gained)
        {
            line = null;
            gained = false;
            if (_characters == null || !_characters.IsOwned(lineageId)) return false;
            var ftue = _data?.meta?.ftue;
            if (ftue == null || !ftue.done) return false;
            line = NextPetLine(lineageId);
            if (PetsLeftToday(lineageId) <= 0) return true;

            var save = Save;
            if (save == null) return true;
            string today = TodayKey;
            if (!save.pets.TryGetValue(lineageId, out var day) || day == null || day.dayKey != today)
            {
                day = new PetDaySave { dayKey = today };
                save.pets[lineageId] = day;
            }
            day.count++;
            int amount = Math.Max(0, Config.petAmount);
            if (amount > 0) _characters.AddAffinity(lineageId, amount);
            gained = true;
            _progress?.Increment(CounterKey.AffinityPet);
            AfterChanged(lineageId);
            return true;
        }

        /// <summary>기억 조각 index번째 (0~4, 단계 2·4·6·8·10에 열림). 아직 잠겼거나 문구가 없으면 null</summary>
        public string GetStoryLine(string lineageId, int index)
        {
            if (index < 0 || index >= StoryLineCount || _characters == null || !_characters.IsOwned(lineageId)) return null;
            if (GetLevel(lineageId) < StoryUnlockLevel(index)) return null;
            var story = _db?.GetAffinityLines(lineageId)?.story;
            if (story == null || index >= story.Length || string.IsNullOrEmpty(story[index])) return null;
            return story[index];
        }

        /// <summary>기억 조각 index번째가 열리는 단계 (2, 4, 6, 8, 10)</summary>
        public static int StoryUnlockLevel(int index) => (index + 1) * 2;

        /// <summary>호감도가 level단계 이상인 보유 계보 수 (할당 없음)</summary>
        public int CountAtLevel(int level)
        {
            if (_data?.lineages == null) return 0;
            int count = 0;
            foreach (var pair in _data.lineages)
            {
                if (pair.Value != null && LevelOf(pair.Value.affinity) >= level) count++;
            }
            return count;
        }

        // ───────────────────────────── IMetaModule ─────────────────────────────

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_progress != null)
            {
                _previousCondition = _progress.ExtraConditionValue;
                _progress.ExtraConditionValue = AnswerCondition;
            }
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
        }

        public void OnDayChanged(string dayKey) => Changed?.Invoke();

        public void Tick(float unscaledDeltaTime)
        {
        }

        /// <summary>계보마다 잎 RedDotPaths.LineageAffinity(계보) = HasClaimable (계보 데이터가 바뀌면 MetaSession이 친구들 탭을 다시 평가한다)</summary>
        public void RegisterRedDots(RedDotSystem dots)
        {
            _dots = dots;
            var lineages = _characters?.Db != null ? _characters.Db.lineages : null;
            if (dots == null || lineages == null) return;
            foreach (var lineage in lineages)
            {
                if (lineage == null || string.IsNullOrEmpty(lineage.id)) continue;
                string id = lineage.id;
                if (!_dotPaths.TryGetValue(id, out string path))
                {
                    path = RedDotPaths.LineageAffinity(id);
                    _dotPaths[id] = path;
                }
                dots.Register(path, () => HasClaimable(id)); // 다시 불려도 같은 경로를 덮어쓴다
            }
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        string TodayKey => _clock != null ? _clock.DayKey : string.Empty;

        /// <summary>진행 기록기 4차 조건: AffinityLevelCount = 호감도 targetId단계 이상인 계보 수 (모르는 조건은 앞 모듈로)</summary>
        long? AnswerCondition(MetaCondition condition)
        {
            if (condition != null && condition.type == MetaConditionType.AffinityLevelCount)
            {
                int level = int.TryParse(condition.targetId, out int parsed) ? parsed : MaxLevel;
                return CountAtLevel(Math.Max(1, level));
            }
            return _previousCondition?.Invoke(condition);
        }

        /// <summary>계보 말풍선 7줄을 차례로 (콘텐츠가 없으면 null)</summary>
        string NextPetLine(string lineageId)
        {
            var lines = _db?.GetAffinityLines(lineageId)?.pet;
            if (lines == null || lines.Length == 0) return null;
            for (int attempt = 0; attempt < lines.Length; attempt++)
            {
                int index = (_petLine++ & int.MaxValue) % lines.Length;
                if (!string.IsNullOrEmpty(lines[index])) return lines[index];
            }
            return null;
        }

        void AfterChanged(string lineageId)
        {
            if (_dots != null && lineageId != null && _dotPaths.TryGetValue(lineageId, out var path)) _dots.MarkDirty(path);
            Changed?.Invoke();
        }
    }
}
