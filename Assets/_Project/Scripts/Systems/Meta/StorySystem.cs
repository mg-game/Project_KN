using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 동화책 (CR-05, SPEC4 §8.1): 챕터 보스(x-10) 클리어 뒤, 그 장에 콘텐츠가 있고 아직 펼쳐 보지 않았으면 MetaPopupRequest.StoryPage(장)
    /// (PopupQueue에서 챕터 상자 다음). 튜토리얼 중이면 ftue.done까지 미뤄 두었다가 넣는다 (1초마다 확인, 앱을 닫으면 다음 클리어에서 다시).
    /// 콘텐츠가 없는 장(6장부터)은 건너뛴다. 팝업이 열 때 MarkSeen(seenChapters — 도감 story:{n}이 이것을 본다), 닫을 때 TryClaimFirstRead로
    /// 처음 읽은 선물(firstReadRewards, 별조각 50)을 한 번 (rewardedChapters). 도감 이야기 탭에서 다시 읽으면 선물이 없다.
    /// </summary>
    public class StorySystem : IMetaModule
    {
        /// <summary>본 장 · 받은 선물이 바뀜</summary>
        public event Action Changed;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly BattleSystem _battle;
        readonly MetaRewardService _rewards;
        readonly PopupQueue _popups;

        bool _connected;
        /// <summary>튜토리얼이 끝나기를 기다리는 장 (0 = 없음)</summary>
        int _deferredChapter;
        float _deferTimer;

        public StorySystem(PlayerData data, MetaDatabase db, BattleSystem battle, MetaRewardService rewards, PopupQueue popups, TutorialSystem tutorial)
        {
            _data = data;
            _db = db;
            _battle = battle;
            _rewards = rewards;
            _popups = popups;
        }

        StorySave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.story ??= new StorySave();
                _data.meta.story.Sanitize();
                return _data.meta.story;
            }
        }

        bool FtueDone => _data?.meta?.ftue != null && _data.meta.ftue.done;

        /// <summary>그 장의 동화책 내용 (콘텐츠가 없으면 null — 6장부터는 건너뜀)</summary>
        public StoryChapterDef Get(int chapter) => chapter > 0 && _db != null ? _db.GetStoryChapter(chapter) : null;

        /// <summary>그 장을 펼쳐 봄으로 표시 (동화책 팝업이 열 때). 콘텐츠가 없는 장은 무시</summary>
        public void MarkSeen(int chapter)
        {
            if (Get(chapter) == null) return;
            var save = Save;
            if (save != null && save.seenChapters.Add(chapter)) Changed?.Invoke();
        }

        public bool IsSeen(int chapter)
        {
            var save = Save;
            return save != null && save.seenChapters.Contains(chapter);
        }

        /// <summary>처음 읽은 선물을 받는다 (팝업을 닫을 때, 한 번 — 펼쳐 본 것으로도 표시). 이미 받았거나 콘텐츠 · 선물이 없으면 null</summary>
        public List<MetaReward> TryClaimFirstRead(int chapter)
        {
            var def = Get(chapter);
            var save = Save;
            if (def == null || save == null || def.firstReadRewards == null || def.firstReadRewards.Count == 0) return null;
            if (save.rewardedChapters.Contains(chapter)) return null;
            save.seenChapters.Add(chapter);
            save.rewardedChapters.Add(chapter);
            var granted = _rewards != null ? _rewards.Grant(def.firstReadRewards) ?? new List<MetaReward>() : new List<MetaReward>(def.firstReadRewards);
            Changed?.Invoke();
            return granted;
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

        public void AfterStartup(bool boot)
        {
        }

        public void OnDayChanged(string dayKey)
        {
        }

        /// <summary>튜토리얼 때문에 미뤄 둔 장이 있으면 1초마다 ftue.done을 보고 넣는다 (할당 없음)</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_deferredChapter <= 0) return;
            _deferTimer += unscaledDeltaTime;
            if (_deferTimer < 1f) return;
            _deferTimer = 0f;
            if (!FtueDone) return;
            int chapter = _deferredChapter;
            _deferredChapter = 0;
            Request(chapter);
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>챕터 보스(x-10) 클리어 → 콘텐츠가 있고 아직 펼쳐 보지 않은 장이면 동화책 요청 (튜토리얼 중이면 미룸)</summary>
        internal void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (outcome != BattleOutcome.Cleared || _battle == null) return;
            var sim = _battle.Sim;
            int stage = sim != null ? Math.Max(1, sim.Level) : Math.Max(1, _battle.Stage);
            if (stage % 10 != 0) return;
            int chapter = stage / 10;
            if (Get(chapter) == null || IsSeen(chapter)) return;
            if (!FtueDone)
            {
                _deferredChapter = chapter;
                _deferTimer = 0f;
                return;
            }
            Request(chapter);
        }

        void Request(int chapter)
        {
            if (_popups == null || Get(chapter) == null || IsSeen(chapter)) return;
            _popups.Enqueue(MetaPopupRequest.StoryPage(chapter));
        }
    }
}
