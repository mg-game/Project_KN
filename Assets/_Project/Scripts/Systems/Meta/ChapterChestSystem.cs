using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 잃어버린 상자 + 챕터 상자 (PRG-06, SPEC4 §8.5).
    /// <para>
    /// 챕터 상자 (P1 핵심, 가이드 퀘스트 22의 상자 내용이 여기로 옮겨 온다 — SPEC4 §2.11 #7): 챕터 보스(x-10)를 클리어했고 그 챕터 상자를 아직 열지 않았으면
    /// PopupQueue에 MetaPopupRequest.ChapterChest(챕터)를 넣는다 (스테이지 사이, 동화책보다 먼저). 내용은 MetaDatabase.GetChapterChest(챕터) —
    /// 줄이 없으면 chapter 0 기본 줄(6장부터), 그것도 없으면 상자 없음. 팝업의 [받기]가 OpenChapterChest를 부르고 chapterChests.opened가 되풀이를 막는다.
    /// 처음 클리어가 아닌 다시 클리어도 아직 열지 않은 상자면 넣는다 (4차 이전 세이브가 지난 챕터 보스를 다시 잡으면 받는다).
    /// 단 1장은 상자 줄이 없던 옛 콘텐츠에서 가이드 퀘스트 22단계가 상자 내용을 함께 줬다 — 상자 줄이 있는 콘텐츠로 처음 돌 때 22단계를 이미 받은
    /// 세이브는 1장 상자를 연 것으로 적어 (MarkLegacyChapterOne, PlayerData.flags 표시로 한 번만) 1-10을 다시 깨도 두 번 받지 않는다 (S-3).
    /// </para>
    /// <para>
    /// 잃어버린 상자 (확장 항목, 콘텐츠로 꺼 둠 — lostChest.dropChance = 0): TryRollLostChest는 튜토리얼 뒤 · 확률 &gt; 0 · 그 스테이지 표가 있을 때만
    /// 기본 재료 굴림을 대신한다 (dropChance 확률로 표의 가중치 한 칸, 개수 min~max). 그 밖에는 false = 지금 굴림 그대로.
    /// 아직 BattleSystem.ClearDropHook에 잇지 않는다 (G3에서 켤 때 P4-0 MetaSessionTests 단언 · CounterKey.LostChestOpen용 진행 기록기와 함께 — 핸드오프 요청).
    /// </para>
    /// </summary>
    public class ChapterChestSystem : IMetaModule
    {
        static readonly List<MetaReward> NoRewards = new();

        /// <summary>
        /// S-3 옛 세이브 확인을 마쳤다는 표시 (PlayerData.flags): 상자 줄이 있는 콘텐츠로 처음 돌 때 세운다 (MarkLegacyChapterOne).
        /// 세운 뒤에 22단계를 받는 세이브(별조각 100만 받음)는 1장 상자를 그대로 받는다
        /// </summary>
        public const string LegacyChapterOneFlag = "chapter_chest.quest22_checked";

        /// <summary>가이드 퀘스트 22단계(첫 챕터 보스 1-10)를 받은 세이브 = 받은 퀘스트 수(MetaSave.guide.index) ≥ 22</summary>
        public const int GuideQuestChapterOneClaimed = 22;

        /// <summary>열린 상자가 바뀜</summary>
        public event Action Changed;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly BattleSystem _battle;
        readonly MetaRewardService _rewards;
        readonly PopupQueue _popups;
        readonly System.Random _random;

        bool _connected;

        public ChapterChestSystem(PlayerData data, MetaDatabase db, BattleSystem battle, MetaRewardService rewards, PopupQueue popups,
            ItemDatabase items, TutorialSystem tutorial)
        {
            _data = data;
            _db = db;
            _battle = battle;
            _rewards = rewards;
            _popups = popups;
            _random = new System.Random(unchecked(Environment.TickCount * 31 + 7));
            // items · tutorial: 표의 재료는 RewardData.Item으로 넣으므로 아이템 표를 따로 보지 않는다
        }

        ChapterChestSave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.chapterChests ??= new ChapterChestSave();
                _data.meta.chapterChests.Sanitize();
                return _data.meta.chapterChests;
            }
        }

        /// <summary>
        /// 클리어 보상에 잃어버린 상자를 굴려 넣는다 (BattleSystem.ClearDropHook 모양). true = 굴렸음(기본 재료 굴림을 건너뛴다).
        /// 확장 항목이라 아직 늘 false (지금 재료 굴림 그대로)
        /// </summary>
        public bool TryRollLostChest(int stage, List<RewardData> rewards) => TryRollLostChest(stage, rewards, _random);

        /// <summary>TryRollLostChest (난수를 받는 판 — 시험용). true = 기본 굴림을 대신했다 (확률에 걸리지 않아 재료가 없을 때도)</summary>
        internal bool TryRollLostChest(int stage, List<RewardData> rewards, System.Random random)
        {
            if (rewards == null || random == null || _db?.lostChest == null) return false;
            var ftue = _data?.meta?.ftue;
            if (ftue == null || !ftue.done) return false;
            float chance = _db.lostChest.dropChance;
            if (!(chance > 0f)) return false;
            var table = _db.GetLostChestTable(stage);
            if (table?.entries == null) return false;
            int total = 0;
            for (int i = 0; i < table.entries.Count; i++)
            {
                var entry = table.entries[i];
                if (entry != null && !string.IsNullOrEmpty(entry.itemId) && entry.weight > 0) total += entry.weight;
            }
            if (total <= 0) return false;
            if (random.NextDouble() >= chance) return true;
            int pick = random.Next(total);
            for (int i = 0; i < table.entries.Count; i++)
            {
                var entry = table.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.weight <= 0) continue;
                if (pick >= entry.weight)
                {
                    pick -= entry.weight;
                    continue;
                }
                int min = Math.Max(1, entry.min);
                int max = Math.Max(min, entry.max);
                rewards.Add(RewardData.Item(entry.itemId, random.Next(min, max + 1)));
                break;
            }
            return true;
        }

        /// <summary>그 챕터 상자를 연다 (한 번만). 이미 열었거나 상자가 없으면 null</summary>
        public List<MetaReward> OpenChapterChest(int chapter)
        {
            MarkLegacyChapterOne();
            var rewards = ChestRewards(chapter);
            var save = Save;
            if (save == null || rewards.Count == 0 || save.opened.Contains(chapter)) return null;
            save.opened.Add(chapter);
            List<MetaReward> granted;
            if (_rewards == null) granted = new List<MetaReward>(rewards);
            else granted = _rewards.Grant(rewards) ?? new List<MetaReward>();
            Changed?.Invoke();
            return granted;
        }

        /// <summary>열기 전에 상자 내용을 보여 주려고 — 그 챕터 상자의 보상 (없으면 빈 목록, 지급하지 않음)</summary>
        public IReadOnlyList<MetaReward> PeekChapterChest(int chapter) => ChestRewards(chapter);

        /// <summary>그 챕터 상자를 이미 열었는지 (chapterChests.opened)</summary>
        public bool IsChapterChestOpened(int chapter)
        {
            var save = Save;
            return save != null && save.opened.Contains(chapter);
        }

        // ───────────────────────────── IMetaModule ─────────────────────────────

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_battle != null) _battle.StageEnded += OnStageEnded;
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot) => MarkLegacyChapterOne();

        public void OnDayChanged(string dayKey)
        {
        }

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>챕터 보스(x-10) 클리어 → 아직 열지 않은 상자면 팝업 요청 (같은 요청은 PopupQueue가 한 번만 둔다)</summary>
        internal void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (outcome != BattleOutcome.Cleared || _battle == null || _popups == null) return;
            var sim = _battle.Sim;
            int stage = sim != null ? Math.Max(1, sim.Level) : Math.Max(1, _battle.Stage);
            if (stage % 10 != 0) return;
            int chapter = stage / 10;
            if (chapter == 1) MarkLegacyChapterOne();
            if (IsChapterChestOpened(chapter) || ChestRewards(chapter).Count == 0) return;
            _popups.Enqueue(MetaPopupRequest.ChapterChest(chapter));
        }

        /// <summary>
        /// S-3 옛 세이브 (한 번만, 할당 없음): 상자 줄이 없던 옛 콘텐츠에서는 가이드 퀘스트 22단계가 1장 상자 내용을 함께 줬다.
        /// 1장 상자 줄이 있는 콘텐츠로 처음 돌 때 표시(LegacyChapterOneFlag)를 세우고, 그때 22단계를 이미 받은 세이브면 1장 상자를 연 것으로 적는다.
        /// 1장 상자 줄이 없는 콘텐츠(22단계가 아직 상자 내용을 줌)에서는 아무것도 하지 않고 기다린다. 시작(AfterStartup) · 1-10 클리어 · 상자 열기 앞에서 부른다
        /// </summary>
        internal void MarkLegacyChapterOne()
        {
            if (_data == null) return;
            if (_data.flags != null && _data.flags.Contains(LegacyChapterOneFlag)) return;
            if (ChestRewards(1).Count == 0) return;
            var save = Save;
            if (save == null) return;
            _data.flags ??= new HashSet<string>();
            _data.flags.Add(LegacyChapterOneFlag);
            var guide = _data.meta?.guide;
            if (guide != null && guide.index >= GuideQuestChapterOneClaimed && save.opened.Add(1)) Changed?.Invoke();
        }

        /// <summary>그 챕터 상자 보상 (콘텐츠 줄 → chapter 0 기본 줄 → 없음)</summary>
        List<MetaReward> ChestRewards(int chapter)
        {
            if (chapter <= 0 || _db == null) return NoRewards;
            var def = _db.GetChapterChest(chapter);
            return def?.rewards != null ? def.rewards : NoRewards;
        }
    }
}
