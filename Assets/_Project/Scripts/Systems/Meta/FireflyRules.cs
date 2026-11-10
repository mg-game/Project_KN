using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 길 잃은 반딧불 규칙 (NEW-03, SPEC4 §13.8 — 확장 항목, 순수 · 모듈이 아님): 튜토리얼 뒤 앞에서 보는 스테이지 전투 중
    /// (잠자기 · 팝업 · 보스 · 소환 조준 중 아님) 360~720초마다 (날 키 + 횟수로 정해진 값) 한 마리, 하루 6번까지, 8초 머문다.
    /// 보상은 정해진 굴림 (단추 10분치 / 잃어버린 상자 재료 하나 / 별조각 10). 되돌림 중에는 날아다녀도 보상이 없다.
    /// <para>
    /// 콘텐츠 끄개: firefly.perDay ≤ 0 이거나 rewards가 비면 늘 나타나지 않는다 (확장 항목은 콘텐츠로 끈 채 들어간다).
    /// 굴림은 날 키 + 오늘 잡은 수로 만든 정해진 수(FNV 해시)라 같은 날 같은 순서면 같은 결과다. 잃어버린 상자 재료(fromLostChestTable)는
    /// 최고 스테이지의 잃어버린 상자 표(MetaDatabase.GetLostChestTable)에서 하나 (표가 없으면 그 칸은 굴림에서 빠진다).
    /// 보기는 P4-3b StrayFireflyView — CanSpawn으로 띄우고, NextDelay만큼 앞에서 본 뒤 다시 묻고, 탭하면 TryCatch.
    /// </para>
    /// </summary>
    public class FireflyRules
    {
        /// <summary>처음 잡은 반딧불 플래그 (PlayerData.flags — 첫 토스트 '길 잃은 반딧불이 선물을 두고 갔어요')</summary>
        public const string FirstCatchFlag = "firefly.first_catch";

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly MetaRewardService _rewards;
        readonly BattleSystem _battle;
        readonly TutorialSystem _tutorial;

        public FireflyRules(PlayerData data, MetaDatabase db, DailyClock clock, MetaRewardService rewards, BattleSystem battle, TutorialSystem tutorial)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _rewards = rewards;
            _battle = battle;
            _tutorial = tutorial;
        }

        FireflyConfig Cfg => _db != null ? _db.firefly : null;

        FireflyDaySave Save => (_data.meta ??= new MetaSave()).firefly ??= new FireflyDaySave();

        /// <summary>반딧불이 켜져 있는지 (하루 상한 &gt; 0 · 보상 칸이 있음 · 일일 시계가 있음)</summary>
        public bool IsEnabled
        {
            get
            {
                var cfg = Cfg;
                return cfg != null && cfg.perDay > 0 && cfg.rewards != null && cfg.rewards.Count > 0 && _clock != null;
            }
        }

        /// <summary>하루 최대 잡기 (꺼져 있으면 0)</summary>
        public int PerDay => IsEnabled ? Cfg.perDay : 0;

        /// <summary>오늘 잡은 수 (날 키가 바뀌면 0)</summary>
        public int CaughtToday
        {
            get
            {
                var save = Save;
                return _clock != null && save.dayKey != null && save.dayKey == _clock.DayKey ? save.count : 0;
            }
        }

        /// <summary>한 마리가 머무는 시간 (초)</summary>
        public float LifeSeconds => Cfg != null && Cfg.lifeSeconds > 0f ? Cfg.lifeSeconds : 8f;

        /// <summary>마지막 TryCatch가 이 세이브의 첫 반딧불이었는지 (첫 토스트용)</summary>
        public bool LastCatchWasFirst { get; private set; }

        bool TutorialDone => _tutorial != null ? _tutorial.IsDone : _data.meta?.ftue != null && _data.meta.ftue.done;

        /// <summary>
        /// 지금 반딧불을 띄워도 되는지 (보기가 아는 상태를 넘긴다: 잠자기 · 팝업 · 소환 조준). 규칙 쪽은 켜짐 · 튜토리얼 · 스테이지 전투 중(결과 대기 아님) ·
        /// 보스가 살아 있지 않음 · 하루 상한을 본다. 할당 없음
        /// </summary>
        public bool CanSpawn(bool asleep, bool popupOpen, bool aiming)
        {
            if (asleep || popupOpen || aiming || !IsEnabled || !TutorialDone) return false;
            if (CaughtToday >= PerDay) return false;
            if (_battle == null || _battle.IsBetweenStages) return false;
            var sim = _battle.Sim;
            return sim == null || sim.Boss == null;
        }

        /// <summary>다음 반딧불까지 앞에서 보는 시간 (초, minInterval~maxInterval 사이 — 날 키 + 오늘 잡은 수로 정해짐)</summary>
        public float NextDelay()
        {
            var cfg = Cfg;
            float min = cfg != null ? cfg.minInterval : 360f;
            float max = cfg != null ? cfg.maxInterval : 720f;
            if (max < min) max = min;
            double u = Roll(_clock?.DayKey, CaughtToday, 1);
            return (float)(min + (max - min) * u);
        }

        /// <summary>반딧불을 잡는다 (보상 지급 + 하루 횟수). 잡을 수 없으면(꺼짐 · 상한 · 되돌림) null — 되돌림이면 날아다녀도 보상이 없다</summary>
        public List<MetaReward> TryCatch()
        {
            LastCatchWasFirst = false;
            if (!IsEnabled || _clock.IsTampered) return null;
            int caught = CaughtToday;
            if (caught >= PerDay) return null;
            string today = _clock.DayKey;
            var reward = RollReward(today, caught);

            var save = Save;
            save.dayKey = today;
            save.count = caught + 1;
            _data.flags ??= new HashSet<string>();
            LastCatchWasFirst = _data.flags.Add(FirstCatchFlag);

            if (reward == null) return new List<MetaReward>();
            var list = new List<MetaReward> { reward };
            return _rewards != null ? _rewards.Grant(list) : list;
        }

        /// <summary>그 날 caught번째 반딧불의 보상 (지급하지 않음 — 미리보기 · 시험용). 굴릴 칸이 없으면 null</summary>
        public MetaReward RollReward(string dayKey, int caught)
        {
            var rewards = Cfg?.rewards;
            if (rewards == null) return null;
            int highest = System.Math.Max(1, _data.battle?.highestStage ?? 1);
            var table = _db.GetLostChestTable(highest);
            int total = 0;
            for (int i = 0; i < rewards.Count; i++) total += WeightOf(rewards[i], table);
            if (total <= 0) return null;

            int pick = (int)(Roll(dayKey, caught, 2) * total);
            for (int i = 0; i < rewards.Count; i++)
            {
                int weight = WeightOf(rewards[i], table);
                if (weight <= 0) continue;
                if (pick < weight) return Resolve(rewards[i], table, dayKey, caught);
                pick -= weight;
            }
            return null;
        }

        static int WeightOf(FireflyRewardDef def, LostChestTable table)
        {
            if (def == null || def.weight <= 0) return 0;
            if (def.fromLostChestTable) return table != null && TableWeight(table) > 0 ? def.weight : 0;
            return def.reward != null && def.reward.amount > 0 ? def.weight : 0;
        }

        static int TableWeight(LostChestTable table)
        {
            int total = 0;
            if (table?.entries == null) return 0;
            for (int i = 0; i < table.entries.Count; i++)
            {
                var entry = table.entries[i];
                if (entry != null && entry.weight > 0 && !string.IsNullOrEmpty(entry.itemId)) total += entry.weight;
            }
            return total;
        }

        static MetaReward Resolve(FireflyRewardDef def, LostChestTable table, string dayKey, int caught)
        {
            if (!def.fromLostChestTable) return def.reward.Clone();
            int total = TableWeight(table);
            int pick = (int)(Roll(dayKey, caught, 3) * total);
            for (int i = 0; i < table.entries.Count; i++)
            {
                var entry = table.entries[i];
                if (entry == null || entry.weight <= 0 || string.IsNullOrEmpty(entry.itemId)) continue;
                if (pick < entry.weight) return MetaReward.Item(entry.itemId, 1);
                pick -= entry.weight;
            }
            return null;
        }

        /// <summary>날 키 · 횟수 · 용도로 정해진 [0, 1) 값 (FNV-1a + 섞기, 할당 없음 — 기기 · 실행과 무관)</summary>
        static double Roll(string dayKey, int caught, int salt)
        {
            unchecked
            {
                uint hash = 2166136261;
                if (dayKey != null)
                {
                    for (int i = 0; i < dayKey.Length; i++) hash = (hash ^ dayKey[i]) * 16777619;
                }
                hash = (hash ^ (uint)(caught + 1)) * 16777619;
                hash = (hash ^ (uint)salt) * 16777619;
                // 마지막 섞기 (murmur 마무리)
                hash ^= hash >> 16;
                hash *= 0x85ebca6b;
                hash ^= hash >> 13;
                hash *= 0xc2b2ae35;
                hash ^= hash >> 16;
                return hash / 4294967296.0;
            }
        }
    }
}
