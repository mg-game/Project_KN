using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 잔향이 던전. 지킴이 없이 잔향이 덱만으로 층을 돌파한다 (전투는 화면 없이 시뮬레이션).
    /// 하루 입장권 제한(DailyClock 날 키 = 현지 새벽 5시에 다시 채워짐 — 상점·출석과 같은 시각, M77), 돌파한 층은 소탕 가능.
    /// 3층 이상에서 기억의 실 등을 얻는다. 일일 시계가 없으면(편집 모드 테스트) 현지 자정 기준.
    /// 시너지는 덱만 세고(지킴이 쪽 계보 공명 공격력도 붙지 않음), 덱 시너지의 단추 획득량 증가는 도전·소탕 단추 보상에 반영된다.
    /// <para>
    /// 도전은 층마다 고정된 시드로 돌아가므로 같은 덱이면 결과가 같다 (운으로 다시 굴리기 불가 → 강화해서 다시 도전).
    /// 그래서 입장권은 돌파에 성공했을 때만 쓴다. 소탕은 항상 입장권 1장.
    /// </para>
    /// <para>
    /// 도전 중 잔향이 소환은 덱 계보의 "잔향이 출격 N회" 기록으로 센다. 실패한 도전은 무료로 같은 결과를 반복할 수 있으므로
    /// 돌파에 성공했을 때만 반영한다. 소탕은 전투가 없으므로 세지 않는다.
    /// </para>
    /// <para>
    /// 4차 (SPEC4 §5.1 · §5.2 · §8.6 · §13.9):
    ///   · 잠금 (F-03): FeatureResolver(Dungeon)이 false면 도전 · 소탕 · 한 번에 소탕은 null. 한 번에 소탕은 DungeonBatchSweep(던전 5층)도 열려야 한다.
    ///   · 한 번에 소탕 (IDLE-05): SweepMany(층, n)은 입장권 n장(남은 입장권에서 자름), 보상을 합쳐 한 번에 주고 Changed는 한 번.
    ///   · 수요 던전 데이 (EVT-01): BonusTickets(+2)를 그날 입장권에 더한다.
    ///   · 입장권 모아 두기 (NEW-04, 확장 — TicketBankCap &gt; 0일 때만, 콘텐츠 0 = 지금처럼 날마다 초기화): 그날 처음 볼 때
    ///     bank = min(상한, bank + 하루 입장권 × 지난 날 수) + 그날 더하는 입장권 (수요일 +2는 한 번, 그날만 상한 11). ticketBank = -1(옛 세이브)은
    ///     처음 볼 때 그날 남은 입장권이 된다. 실패한 도전은 지금처럼 입장권을 쓰지 않는다. 진행 기록 추론용 ticketsUsed · ticketDate는 그대로 센다.
    ///   · 잔향 축복 (PRG-07, 확장 — BlessingPool이 비어 있으면 꺼짐): 돌파한 최고 층 3층마다 고르기 1번 (최대 10개),
    ///     제안 3개는 정해진 굴림으로 dungeon.blessingOffer에 저장해 다시 열어도 같다 (이미 2겹인 축복은 빠진다). 고른 축복은 던전 덱 능력치(Team)에
    ///     값을 더한다. 주에 한 번(주 키) 무료로 모두 비우고 다시 고를 수 있다.
    /// </para>
    /// </summary>
    public class DungeonSystem
    {
        public event Action Changed;

        /// <summary>시뮬레이션을 한 번에 진행하는 시간(초)</summary>
        const float SimChunkSeconds = 0.5f;

        public const string ThreadItemId = "mat_thread";
        public const string SpringItemId = "mat_spring";
        public const string MoonlightItemId = "mat_moonlight";

        /// <summary>축복 고르기 1번이 생기는 층 간격 (3층마다)</summary>
        public const int BlessingFloorInterval = 3;
        /// <summary>한 번에 제안하는 축복 수</summary>
        public const int BlessingOfferSize = 3;

        readonly PlayerData _data;
        readonly BattleConfig _config;
        readonly CharacterSystem _characters;
        readonly FormationSystem _formation;
        readonly SynergySystem _synergy;
        readonly RewardService _rewards;
        readonly ITimeProvider _clock;
        /// <summary>"오늘"의 기준 (null이면 현지 자정 기준)</summary>
        readonly DailyClock _dailyClock;
        readonly System.Random _random;

        public DungeonSystem(PlayerData data, BattleConfig config, CharacterSystem characters, FormationSystem formation,
            SynergySystem synergy, RewardService rewards, ITimeProvider clock, DailyClock dailyClock = null)
        {
            _synergy = synergy;
            _data = data;
            _config = config != null ? config : CreateDefaultConfig();
            _characters = characters;
            _formation = formation;
            _rewards = rewards;
            _clock = clock;
            _dailyClock = dailyClock;
            // 낮은 확률 보상(달빛 조각) 굴림용
            _random = new System.Random(unchecked((int)(clock != null ? clock.UtcNow.Ticks : DateTime.UtcNow.Ticks)));
        }

        static BattleConfig CreateDefaultConfig()
        {
            var config = ScriptableObject.CreateInstance<BattleConfig>();
            config.ApplyDefaults();
            return config;
        }

        DungeonSave Save => _data.dungeon ??= new DungeonSave();

        /// <summary>오늘 날 키 "yyyy-MM-dd" (DailyClock = 현지 05:00 기준, 같은 날이면 할당 없음). 시계가 없으면 현지 날짜</summary>
        string Today => _dailyClock != null ? _dailyClock.DayKey : _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>이번 주 키 (DailyClock 월요일 05:00, 시계가 없으면 현지 시각 기준)</summary>
        string WeekKey => _dailyClock != null ? _dailyClock.WeekKey : DailyClock.WeekKeyOf(_clock.UtcNow.ToLocalTime());

        /// <summary>입장권이 다시 채워질 때까지 남은 시간 (던전 카드 "충전까지 7:38:48"). 시계가 없으면 다음 현지 자정까지</summary>
        public TimeSpan TimeUntilTicketReset
        {
            get
            {
                if (_dailyClock != null) return _dailyClock.TimeUntilDailyReset;
                var now = _clock.UtcNow;
                var next = now.ToLocalTime().Date.AddDays(1).ToUniversalTime();
                return next > now ? next - now : TimeSpan.Zero;
            }
        }

        public int HighestFloor => Math.Max(0, Save.highestFloor);
        public int NextFloor => HighestFloor + 1;
        public int DailyTickets => Math.Max(0, _config.dungeonDailyTickets);

        /// <summary>
        /// 지금 쓸 수 있는 입장권. 모아 두기가 꺼져 있으면 그날 (하루 입장권 + 그날 더하는 입장권) − 쓴 수,
        /// 켜져 있으면 모아 둔 입장권 (그날 처음 볼 때 채운다)
        /// </summary>
        public int TicketsLeft
        {
            get
            {
                if (IsTicketBankOn)
                {
                    EnsureBank();
                    return Math.Max(0, Save.ticketBank);
                }
                return LegacyTicketsLeft;
            }
        }

        /// <summary>도전 가능한 잔향이 덱이 있는지</summary>
        public bool HasDeck
        {
            get
            {
                for (int slot = 0; slot < FormationSystem.EchoSlotCount; slot++)
                {
                    if (MakeUnit(_formation.GetEcho(slot)) != null) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 층 확정 보상 (단추 + 3층부터 기억의 실 + 5층부터 오르골 태엽). 달빛 조각(10층~)은 확률로 추가된다.
        /// 단추는 지금 잔향이 덱 시너지의 단추 획득량 증가(GoldBonusPercent)를 반영한다 — 도전·소탕 모두 같은 값.
        /// </summary>
        public List<RewardData> GetRewards(int floor) => GetRewards(floor, GoldBonusPercent);

        /// <summary>
        /// 잔향이 덱 시너지의 단추 획득량 증가(%) — 던전 전투처럼 덱만 센다 (StatType.GoldGainPercent, 시너지 전체 합계 규칙 SumGlobal).
        /// 스테이지 기믹이 없으므로 축제 보너스는 붙지 않는다.
        /// </summary>
        public float GoldBonusPercent => GoldBonusOf(BuildModifiers());

        static float GoldBonusOf(BattleModifiers modifiers) =>
            Mathf.Max(-90f, BattleSimulator.SumGlobal(modifiers, StatType.GoldGainPercent));

        List<RewardData> GetRewards(int floor, float goldBonusPercent)
        {
            floor = Math.Max(1, floor);
            var rewards = new List<RewardData>();
            double gold = Math.Floor(Math.Max(0, _config.dungeonGoldBase) * Math.Pow(Math.Max(1, _config.dungeonGoldGrowth), floor - 1)
                                     * (1.0 + goldBonusPercent / 100.0));
            if (gold > 0) rewards.Add(RewardData.Currency(CurrencyType.Gold, gold));
            if (floor >= _config.dungeonThreadFloor)
                rewards.Add(RewardData.Item(ThreadItemId, 1 + (floor - _config.dungeonThreadFloor) / 3));
            if (floor >= _config.dungeonSpringFloor)
                rewards.Add(RewardData.Item(SpringItemId, 1 + (floor - _config.dungeonSpringFloor) / 5));
            return rewards;
        }

        /// <summary>달빛 조각 확률 (해당 층 미만이면 0)</summary>
        public float GetMoonlightChance(int floor) =>
            floor < _config.dungeonMoonlightFloor ? 0f
                : Mathf.Clamp01(_config.dungeonMoonlightChance + 0.01f * (floor - _config.dungeonMoonlightFloor));

        /// <summary>다음 층 도전 (입장권 1장). 던전이 잠겼거나 잔향이 덱이 비어 있거나 입장권이 없으면 null</summary>
        public DungeonResult Challenge()
        {
            if (!IsOpen(FeatureId.Dungeon) || TicketsLeft <= 0) return null;
            int floor = NextFloor;
            var setup = CreateSetup(floor);
            if (setup == null) return null;

            // 덱 슬롯별 잔향이 소환 횟수 (돌파 시 출격 기록으로 반영)
            var summons = new int[setup.Deck.Count];
            void OnSimEvent(BattleEvent e)
            {
                if (e.Type != BattleEventType.EchoSummoned) return;
                int slot = (int)e.Value;
                if (slot >= 0 && slot < summons.Length) summons[slot]++;
            }

            var sim = new BattleSimulator(_config, FloorSeed(floor)) { AutoMode = true };
            sim.Event += OnSimEvent;
            sim.Start(setup);
            float limit = Mathf.Max(1f, _config.dungeonTimeLimit);
            int maxTicks = Mathf.CeilToInt(limit / SimChunkSeconds) + 2;
            for (int i = 0; i < maxTicks && sim.Outcome == BattleOutcome.Running && sim.ElapsedTime < limit; i++)
                sim.Tick(SimChunkSeconds);
            sim.Event -= OnSimEvent;

            var result = new DungeonResult
            {
                Floor = floor,
                Cleared = sim.Outcome == BattleOutcome.Cleared,
                SurvivedSeconds = Mathf.Min(sim.ElapsedTime, limit),
            };
            if (result.Cleared)
            {
                UseTicket();
                Save.highestFloor = Math.Max(Save.highestFloor, floor);
                result.Rewards.AddRange(GetRewards(floor, GoldBonusOf(setup.Modifiers)));
                RollBonus(floor, result.Rewards);
                _rewards.Grant(result.Rewards);
                RecordEchoDeploys(setup, summons);
                SyncBlessingPicks();
            }
            Changed?.Invoke();
            return result;
        }

        /// <summary>돌파한 도전에서 소환한 잔향이 수를 덱 계보의 "잔향이 출격" 기록에 반영</summary>
        void RecordEchoDeploys(BattleSetup setup, int[] summons)
        {
            for (int slot = 0; slot < summons.Length && slot < setup.Deck.Count; slot++)
            {
                var lineageId = setup.Deck[slot]?.LineageId;
                if (summons[slot] > 0 && !string.IsNullOrEmpty(lineageId) && _characters.IsOwned(lineageId))
                    _characters.AddProgress(lineageId, ProgressType.EchoDeployCount, summons[slot]);
            }
        }

        // ───────────── 4차 훅 (SPEC4 §2.5 · §5.2 · §8.6 · §13.9) ─────────────

        static readonly List<string> NoBlessings = new();

        /// <summary>오늘 더해지는 입장권 (이벤트 '수요 던전 데이' +2, EventCalendarSystem이 넣는다). null = 0</summary>
        public Func<int> BonusTickets;

        /// <summary>기능 개방 확인 (Dungeon · DungeonBatchSweep 잠김이면 도전 · 소탕을 거절, MetaSession.Connect가 넣는다). null = 열림</summary>
        public Func<FeatureId, bool> FeatureResolver;

        /// <summary>입장권 모아 두기 상한 (NEW-04, MetaDatabase.dungeonTicketBankCap — MetaSession.Connect가 넣는다). null 또는 0 = 끔 (날마다 초기화 = 지금)</summary>
        public Func<int> TicketBankCap;

        /// <summary>
        /// 잔향 축복 표 (PRG-07, MetaDatabase.blessings — 던전은 메타 데이터를 모르므로 EventCalendarSystem.Connect가 넣는다).
        /// null 또는 비어 있으면 축복은 꺼짐 (고르기 0 · 능력치 없음 — 확장 항목의 콘텐츠 끄개)
        /// </summary>
        public List<BlessingDef> BlessingPool;

        /// <summary>입장권 모아 두기가 켜져 있는지 (TicketBankCap &gt; 0)</summary>
        public bool IsTicketBankOn => TicketBankCapValue > 0;

        /// <summary>모아 둘 수 있는 입장권 (꺼져 있으면 0). 수요 던전 데이에는 그날만 이보다 많을 수 있다</summary>
        public int TicketBankCapValue
        {
            get
            {
                var hook = TicketBankCap;
                return hook != null ? Math.Max(0, hook()) : 0;
            }
        }

        /// <summary>
        /// 돌파한 층을 count번 한 번에 소탕 (IDLE-05): 입장권 count장 (남은 입장권에서 자름), 보상 합침, Changed 한 번.
        /// 던전 · 한 번에 소탕(DungeonBatchSweep)이 잠겼거나, 돌파하지 않은 층이거나, count ≤ 0이거나, 입장권이 없으면 null
        /// </summary>
        public DungeonResult SweepMany(int floor, int count)
        {
            if (!IsOpen(FeatureId.Dungeon) || !IsOpen(FeatureId.DungeonBatchSweep)) return null;
            if (floor < 1 || floor > HighestFloor || count <= 0) return null;
            int n = Math.Min(count, TicketsLeft);
            if (n <= 0) return null;

            var result = new DungeonResult { Floor = floor, Cleared = true };
            var single = GetRewards(floor);
            for (int i = 0; i < single.Count; i++)
            {
                var reward = single[i];
                if (reward == null) continue;
                reward.amount *= n;
                result.Rewards.Add(reward);
            }
            int moonlight = 0;
            float chance = GetMoonlightChance(floor);
            for (int i = 0; i < n; i++)
            {
                UseTicket();
                if (chance > 0f && _random.NextDouble() < chance) moonlight++;
            }
            if (moonlight > 0) result.Rewards.Add(RewardData.Item(MoonlightItemId, moonlight));
            _rewards.Grant(result.Rewards);
            Changed?.Invoke();
            return result;
        }

        /// <summary>고른 잔향 축복 ID (던전 전용 영구 능력치, PRG-07)</summary>
        public IReadOnlyList<string> Blessings => Save.blessings ?? NoBlessings;

        /// <summary>축복이 켜져 있는지 (표가 있고 던전이 열림)</summary>
        public bool IsBlessingOn => BlessingPool != null && BlessingPool.Count > 0 && IsOpen(FeatureId.Dungeon);

        /// <summary>아직 고르지 않은 축복 수 = min(10, 최고 층 / 3) − 고른 수 (꺼져 있으면 0)</summary>
        public int BlessingPicksPending
        {
            get
            {
                if (!IsBlessingOn) return 0;
                int owed = Math.Min(DungeonSave.MaxBlessings, HighestFloor / BlessingFloorInterval);
                return Math.Max(0, owed - Blessings.Count);
            }
        }

        /// <summary>
        /// 지금 고를 축복 (최대 3개, 다시 열어도 같게 저장된 값). 고를 것이 없으면 빈 목록. 저장된 제안이 없거나
        /// 더는 맞지 않으면(이미 2겹이 됨 · 표에서 빠짐) 정해진 굴림으로 새로 만든다
        /// </summary>
        public IReadOnlyList<string> GetBlessingOffer()
        {
            if (BlessingPicksPending <= 0) return NoBlessings;
            var save = Save;
            save.blessingOffer ??= new List<string>();
            if (!IsOfferValid(save.blessingOffer)) RollOffer(save.blessingOffer);
            return save.blessingOffer;
        }

        /// <summary>제안된 축복 하나를 고른다 (고르기가 남아 있고 지금 제안에 있어야). 제안은 비워 다음 고르기 때 새로 굴린다</summary>
        public bool TryPickBlessing(string blessingId)
        {
            if (string.IsNullOrEmpty(blessingId) || BlessingPicksPending <= 0) return false;
            var offer = GetBlessingOffer();
            bool offered = false;
            for (int i = 0; i < offer.Count && !offered; i++) offered = offer[i] == blessingId;
            if (!offered || !CanStack(blessingId)) return false;
            var save = Save;
            (save.blessings ??= new List<string>()).Add(blessingId);
            save.blessingOffer.Clear();
            SyncBlessingPicks();
            Changed?.Invoke();
            return true;
        }

        /// <summary>이번 주 무료 다시 고르기를 쓸 수 있는지 (축복이 켜져 있고, 고른 축복이 있고, 이번 주에 아직 안 씀)</summary>
        public bool CanRespec => IsBlessingOn && Blessings.Count > 0 && Save.respecWeekKey != WeekKey;

        /// <summary>축복을 모두 비우고 고르기를 돌려받는다 (= 최고 층 / 3, 주에 한 번 무료). 쓸 수 없으면 false</summary>
        public bool Respec()
        {
            if (!CanRespec) return false;
            var save = Save;
            save.blessings.Clear();
            save.blessingOffer?.Clear();
            save.respecWeekKey = WeekKey;
            SyncBlessingPicks();
            Changed?.Invoke();
            return true;
        }

        /// <summary>그 축복을 몇 겹 골랐는지</summary>
        public int GetBlessingStacks(string blessingId)
        {
            if (string.IsNullOrEmpty(blessingId)) return 0;
            var list = Blessings;
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == blessingId) count++;
            }
            return count;
        }

        /// <summary>표에서 축복 정의 찾기 (없으면 null, 할당 없음)</summary>
        public BlessingDef GetBlessingDef(string blessingId)
        {
            var pool = BlessingPool;
            if (pool == null || string.IsNullOrEmpty(blessingId)) return null;
            for (int i = 0; i < pool.Count; i++)
            {
                var def = pool[i];
                if (def != null && def.id == blessingId) return def;
            }
            return null;
        }

        /// <summary>돌파한 층 소탕 (입장권 1장, 즉시 보상). 던전이 잠겼거나, 돌파하지 않은 층이거나, 입장권이 없으면 null</summary>
        public DungeonResult Sweep(int floor)
        {
            if (!IsOpen(FeatureId.Dungeon)) return null;
            if (floor < 1 || floor > HighestFloor || TicketsLeft <= 0) return null;
            UseTicket();
            var result = new DungeonResult { Floor = floor, Cleared = true };
            result.Rewards.AddRange(GetRewards(floor));
            RollBonus(floor, result.Rewards);
            _rewards.Grant(result.Rewards);
            Changed?.Invoke();
            return result;
        }

        bool IsOpen(FeatureId feature)
        {
            var resolver = FeatureResolver;
            return resolver == null || resolver(feature);
        }

        int BonusToday
        {
            get
            {
                var hook = BonusTickets;
                return hook != null ? Math.Max(0, hook()) : 0;
            }
        }

        /// <summary>모아 두기가 꺼졌을 때의 남은 입장권 (그날 하루 입장권 + 더하는 입장권 − 쓴 수)</summary>
        int LegacyTicketsLeft
        {
            get
            {
                var save = Save;
                int daily = DailyTickets + BonusToday;
                return save.ticketDate == Today ? Math.Max(0, daily - save.ticketsUsed) : daily;
            }
        }

        /// <summary>
        /// 모아 두기: 그날 처음 볼 때 채운다 (위 규칙). 같은 날이면 날 키 비교만 (할당 없음).
        /// 날이 거꾸로 가면(되돌림은 DailyClock이 막는다) 채우지 않고 날 키만 맞춘다
        /// </summary>
        void EnsureBank()
        {
            var save = Save;
            string today = Today;
            if (save.ticketBank >= 0 && save.bankDayKey == today) return;
            int cap = TicketBankCapValue;
            int bonus = BonusToday;
            if (save.ticketBank < 0)
            {
                save.ticketBank = Math.Min(LegacyTicketsLeft, cap + bonus);
            }
            else
            {
                int days = string.IsNullOrEmpty(save.bankDayKey) ? 1 : DailyClock.DaysBetween(save.bankDayKey, today);
                if (days > 0)
                {
                    // 수요일에 상한을 넘긴 몫(+2)은 다음 날 상한으로 접힌다: min(상한, bank + 하루 × 날 수) + 그날 더하는 입장권
                    long refilled = Math.Min((long)cap, save.ticketBank + (long)DailyTickets * days);
                    save.ticketBank = (int)Math.Min(DungeonSave.MaxTicketBank, refilled + bonus);
                }
            }
            if (save.ticketBank > DungeonSave.MaxTicketBank) save.ticketBank = DungeonSave.MaxTicketBank;
            save.bankDayKey = today;
        }

        void UseTicket()
        {
            var save = Save;
            if (IsTicketBankOn)
            {
                EnsureBank();
                if (save.ticketBank > 0) save.ticketBank--;
            }
            // 진행 기록(ProgressTracker)이 입장권 사용을 이 두 칸으로 추론하므로 모아 두기를 켜도 그대로 센다
            string today = Today;
            if (save.ticketDate != today)
            {
                save.ticketDate = today;
                save.ticketsUsed = 0;
            }
            save.ticketsUsed++;
        }

        void RollBonus(int floor, List<RewardData> rewards)
        {
            float chance = GetMoonlightChance(floor);
            if (chance > 0f && _random.NextDouble() < chance) rewards.Add(RewardData.Item(MoonlightItemId, 1));
        }

        static int FloorSeed(int floor) => unchecked(floor * 7919 + 17);

        // ── 축복 내부 ──

        /// <summary>저장 칸 blessingPicksPending을 지금 값으로 맞춘다 (화면 · 옛 코드가 읽을 수 있게)</summary>
        void SyncBlessingPicks()
        {
            var save = Save;
            save.blessingPicksPending = BlessingPicksPending;
        }

        /// <summary>그 축복을 한 겹 더 고를 수 있는지 (표에 있고 maxStacks 미만)</summary>
        bool CanStack(string blessingId)
        {
            var def = GetBlessingDef(blessingId);
            return def != null && GetBlessingStacks(blessingId) < Math.Max(1, def.maxStacks);
        }

        /// <summary>저장된 제안이 지금도 맞는지: 비어 있지 않고, 서로 다르고, 모두 한 겹 더 고를 수 있고, 고를 수 있는 축복 수만큼(최대 3)</summary>
        bool IsOfferValid(List<string> offer)
        {
            if (offer == null || offer.Count == 0) return false;
            if (offer.Count != Math.Min(BlessingOfferSize, CountStackable())) return false;
            for (int i = 0; i < offer.Count; i++)
            {
                if (!CanStack(offer[i])) return false;
                for (int j = 0; j < i; j++)
                {
                    if (offer[j] == offer[i]) return false;
                }
            }
            return true;
        }

        int CountStackable()
        {
            var pool = BlessingPool;
            if (pool == null) return 0;
            int count = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                var def = pool[i];
                if (def != null && !string.IsNullOrEmpty(def.id) && GetBlessingDef(def.id) == def && CanStack(def.id)) count++;
            }
            return count;
        }

        /// <summary>
        /// 새 제안 (정해진 굴림: 고른 수 · 최고 층 · 다시 고르기 주로 시드를 만든다 — 같은 상태면 같은 제안). 고를 수 있는 축복 중 서로 다른 3개
        /// </summary>
        void RollOffer(List<string> into)
        {
            into.Clear();
            var pool = BlessingPool;
            if (pool == null) return;
            var candidates = new List<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                var def = pool[i];
                if (def == null || string.IsNullOrEmpty(def.id) || candidates.Contains(def.id) || !CanStack(def.id)) continue;
                candidates.Add(def.id);
            }
            int seed = unchecked(Blessings.Count * 7919 + HighestFloor * 131 + StableHash(Save.respecWeekKey) + 977);
            var random = new System.Random(seed);
            while (into.Count < BlessingOfferSize && candidates.Count > 0)
            {
                int index = random.Next(candidates.Count);
                into.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        /// <summary>문자열의 정해진 해시 (실행마다 같다 — string.GetHashCode는 보장되지 않는다)</summary>
        static int StableHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < text.Length; i++) hash = (hash ^ text[i]) * 16777619;
                return hash;
            }
        }

        /// <summary>
        /// floor층 도전 전투 구성 (미리보기/시험용으로도 쓴다): 지킴이 없이 잔향이 덱만, 시너지도 덱만 센다, 스테이지 기믹 없음.
        /// 계보 공명 공격력(FormationSystem.TeamAttackBonusPercent)은 지킴이와 잔향이 양쪽에 나간 계보의 보너스라
        /// 지킴이가 싸우지 않는 던전에는 붙지 않는다 (AttackMultiplier = 1). 덱이 비어 있으면 null.
        /// 고른 잔향 축복은 덱 전체 능력치(Modifiers.Team)에 더한다.
        /// </summary>
        public BattleSetup CreateSetup(int floor)
        {
            var setup = new BattleSetup
            {
                Mode = BattleMode.EchoDungeon,
                Level = Math.Max(1, floor),
                ShopMaxHp = GrowthSystem.CalcShopMaxHp(_data.level),
                AttackMultiplier = 1f,
                StageModifier = StageModifierType.None,
            };
            bool any = false;
            for (int slot = 0; slot < FormationSystem.EchoSlotCount; slot++)
            {
                var unit = MakeUnit(_formation.GetEcho(slot));
                any |= unit != null;
                setup.Deck.Add(unit);
            }
            if (!any) return null;
            setup.Modifiers = BuildModifiers();
            return setup;
        }

        /// <summary>잔향이 덱만 센 시너지 능력치 + 고른 잔향 축복 (던전 전투·단추 보상 공용)</summary>
        BattleModifiers BuildModifiers()
        {
            var modifiers = _synergy != null ? _synergy.BuildModifiers(StageModifierType.None, echoesOnly: true) ?? new BattleModifiers() : new BattleModifiers();
            AddBlessings(modifiers.Team);
            return modifiers;
        }

        /// <summary>고른 축복마다 value를 그 능력치에 더한다 (축복이 꺼져 있으면 아무것도 하지 않음)</summary>
        void AddBlessings(StatBlock team)
        {
            if (team == null || BlessingPool == null || BlessingPool.Count == 0) return;
            var list = Blessings;
            for (int i = 0; i < list.Count; i++)
            {
                var def = GetBlessingDef(list[i]);
                if (def != null && def.value != 0f) team.Add(def.stat, def.value);
            }
        }

        UnitSetup MakeUnit(string formId) => BattleSystem.MakeUnit(_characters, formId);
    }
}
