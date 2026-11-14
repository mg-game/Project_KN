using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 플레이 기록 구독 (CR-09, SPEC4 §4.4): 게임 사건을 PlayLog 한 줄씩으로 남긴다. GameBootstrap이 Game.Setup 뒤에 만들어 Connect하고,
    /// 멈춤 · 복귀 · 종료 때 OnPause · OnResume · OnQuit, 장면이 내려갈 때 Disconnect한다.
    ///   · session_start {v, build, stage, power} / session_end {sec}
    ///   · ftue_step {n, id, t} (t = 이번 세션 시작부터 초)
    ///   · stage_end {stage, res(clear|fail), pow, rec, sec, hp(수선점 남은 %), dir[5](DirectionDamage가 있을 때)}
    ///   · gacha {n, grades[]} · quest_claim {id} · basket_claim {h, double} · quick_idle {paid, ticket} · feature_unlock {id}
    ///   · spend {sink(shop|lineage), cur, amt, from · to (수선점) | id · lv (친구)}: 레벨업에 쓴 재화를 소비처별로 (SYS_Economy §3 #8 E안, 나눌지 판정 근거)
    /// mission_box · event_claim · daily_gift는 그 시스템에 사건이 없어 그 시스템 주인이 PlayLog.Begin(…)으로 직접 쓴다 (P4-2 요청).
    /// 처리기는 예외를 내지 않는다 (시스템 사건 흐름을 끊지 않게 — 경고 한 줄). 사건 때만 돌고, 숫자 · 글자 쓰기는 할당하지 않는다
    /// (feature_unlock의 이름 · 소환 결과 목록 순회 정도).
    /// </summary>
    public sealed class PlayLogRecorder
    {
        /// <summary>
        /// 스테이지 방향별 수선점 피해 (CR-01 패배 분석, P4-3b DefeatAnalyzer가 넣는다): 길이 5 배열을 이번 스테이지 값으로 채운다
        /// (0 = 위쪽, 반시계). null이면 stage_end에 dir을 쓰지 않는다. StageEnded 때 부르므로 StageChanged에서 비우기 전 값이다
        /// </summary>
        public static Action<int[]> DirectionDamage;

        public const int DirectionSlots = 5;

        readonly BattleSystem _battle;
        readonly PowerSystem _power;
        readonly GachaSystem _gacha;
        readonly GuideQuestSystem _quests;
        readonly IdleChestSystem _idleChest;
        readonly FeatureGateSystem _features;
        readonly TutorialSystem _tutorial;
        readonly MetaDatabase _metaDb;
        readonly Func<float> _realtime;
        readonly GrowthSystem _growth;
        readonly CharacterSystem _characters;

        readonly int[] _directions = new int[DirectionSlots];
        readonly List<int> _grades = new(10);

        bool _connected;
        bool _inSession;
        bool _warned;
        float _sessionStart;
        float _stageStart;
        float _stageElapsedAtPause;

        /// <param name="realtime">초 단위 실제 시각 (null = Time.realtimeSinceStartup) — 시험용</param>
        public PlayLogRecorder(BattleSystem battle, PowerSystem power, GachaSystem gacha, GuideQuestSystem quests, IdleChestSystem idleChest,
            FeatureGateSystem features, TutorialSystem tutorial, MetaDatabase metaDb, Func<float> realtime = null,
            GrowthSystem growth = null, CharacterSystem characters = null)
        {
            _growth = growth;
            _characters = characters;
            _battle = battle;
            _power = power;
            _gacha = gacha;
            _quests = quests;
            _idleChest = idleChest;
            _features = features;
            _tutorial = tutorial;
            _metaDb = metaDb;
            _realtime = realtime ?? (() => Time.realtimeSinceStartup);
        }

        /// <summary>지금 Game의 시스템으로 만든다 (Game.Setup 뒤)</summary>
        public static PlayLogRecorder FromGame() => new(Game.Battle, Game.Power, Game.Gacha, Game.GuideQuest, Game.IdleChest, Game.Features,
            Game.Tutorial, Game.MetaDb, growth: Game.Growth, characters: Game.Characters);

        public bool IsConnected => _connected;

        /// <summary>사건을 구독하고 session_start를 남긴다 (두 번 불러도 한 번)</summary>
        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            try
            {
                if (_battle != null)
                {
                    _battle.StageChanged += OnStageChanged;
                    _battle.StageEnded += OnStageEnded;
                }
                if (_gacha != null) _gacha.Pulled += OnPulled;
                if (_quests != null) _quests.Claimed += OnQuestClaimed;
                if (_idleChest != null) _idleChest.Claimed += OnBasketClaimed;
                if (_features != null) _features.Unlocked += OnFeatureUnlocked;
                if (_tutorial != null) _tutorial.StepCompleted += OnStepCompleted;
                if (_growth != null) _growth.LevelChanged += OnShopLevelChanged;
                if (_characters != null) _characters.LevelUp += OnLineageLevelUp;
            }
            catch (Exception e)
            {
                Warn(e);
            }
            _stageStart = Now;
            StartSession();
        }

        /// <summary>구독을 푼다 (장면이 내려갈 때). 세션이 열려 있으면 session_end도 남긴다</summary>
        public void Disconnect()
        {
            if (!_connected) return;
            EndSession();
            _connected = false;
            try
            {
                if (_battle != null)
                {
                    _battle.StageChanged -= OnStageChanged;
                    _battle.StageEnded -= OnStageEnded;
                }
                if (_gacha != null) _gacha.Pulled -= OnPulled;
                if (_quests != null) _quests.Claimed -= OnQuestClaimed;
                if (_idleChest != null) _idleChest.Claimed -= OnBasketClaimed;
                if (_features != null) _features.Unlocked -= OnFeatureUnlocked;
                if (_tutorial != null) _tutorial.StepCompleted -= OnStepCompleted;
                if (_growth != null) _growth.LevelChanged -= OnShopLevelChanged;
                if (_characters != null) _characters.LevelUp -= OnLineageLevelUp;
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        /// <summary>백그라운드로 감: session_end, 스테이지 시간은 멈춰 둔다</summary>
        public void OnPause()
        {
            if (!_connected) return;
            _stageElapsedAtPause = Math.Max(0f, Now - _stageStart);
            EndSession();
        }

        /// <summary>돌아옴: 새 session_start, 스테이지 시간은 멈춘 곳부터</summary>
        public void OnResume()
        {
            if (!_connected || _inSession) return;
            _stageStart = Now - _stageElapsedAtPause;
            StartSession();
        }

        /// <summary>앱 종료: session_end</summary>
        public void OnQuit()
        {
            if (!_connected) return;
            EndSession();
        }

        float Now => _realtime();

        void StartSession()
        {
            _inSession = true;
            _sessionStart = Now;
            try
            {
                string build = Application.isEditor ? "editor" : Debug.isDebugBuild ? "dev" : "release";
                PlayLog.Begin("session_start")
                    .Str("v", Application.version)
                    .Str("build", build)
                    .Int("stage", _battle != null ? _battle.HighestStage : 0)
                    .Int("power", _power != null ? _power.TeamPower : 0)
                    .End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void EndSession()
        {
            if (!_inSession) return;
            _inSession = false;
            try
            {
                PlayLog.Begin("session_end").Num("sec", Math.Max(0f, Now - _sessionStart), 0).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void OnStageChanged() => _stageStart = Now;

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            try
            {
                int stage = _battle.Stage;
                var line = PlayLog.Begin("stage_end")
                    .Int("stage", stage)
                    .Str("res", outcome == BattleOutcome.Cleared ? "clear" : outcome == BattleOutcome.Failed ? "fail" : "run")
                    .Int("pow", _power != null ? _power.TeamPower : 0)
                    .Int("rec", _power != null ? _power.GetRecommendedPower(stage) : 0)
                    .Num("sec", Math.Max(0f, Now - _stageStart), 1);
                var sim = _battle.Sim;
                float maxHp = sim != null ? sim.ShopMaxHp : 0f;
                line = line.Int("hp", maxHp > 0f ? (long)Math.Round(Math.Max(0f, sim.ShopHp) / maxHp * 100f) : 0);
                var fill = DirectionDamage;
                if (fill != null)
                {
                    Array.Clear(_directions, 0, _directions.Length);
                    fill(_directions);
                    line = line.Ints("dir", _directions);
                }
                line.End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void OnPulled(GachaTable table, IReadOnlyList<GachaResult> results)
        {
            try
            {
                _grades.Clear();
                if (results != null)
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        var lineage = results[i]?.Lineage;
                        _grades.Add(lineage != null ? (int)lineage.grade : -1);
                    }
                }
                PlayLog.Begin("gacha").Int("n", _grades.Count).Ints("grades", _grades).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        /// <summary>수선점 강화 지출 (LevelChanged는 TryLevelUp에서만 난다): 올린 레벨 구간의 비용 합</summary>
        void OnShopLevelChanged(int from, int to)
        {
            if (_growth == null || to <= from) return;
            try
            {
                PlayLog.Begin("spend").Str("sink", "shop").Str("cur", CurrencyKey(_growth.LevelUpCurrency))
                    .Num("amt", _growth.GetLevelUpCost(from, to - from), 0).Int("from", from).Int("to", to).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        /// <summary>친구 레벨업 지출 (LevelUp은 TryLevelUp에서만 난다): level − 1 → level 비용</summary>
        void OnLineageLevelUp(string lineageId, int level)
        {
            if (_characters == null || level <= 1) return;
            try
            {
                PlayLog.Begin("spend").Str("sink", "lineage").Str("cur", CurrencyKey(_characters.LevelUpCurrency))
                    .Num("amt", _characters.GetCostAtLevel(level - 1), 0).Str("id", lineageId).Int("lv", level).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        /// <summary>기록용 재화 이름 (할당 없음)</summary>
        internal static string CurrencyKey(CurrencyType type) => type switch
        {
            CurrencyType.Gold => "gold",
            CurrencyType.Gem => "gem",
            CurrencyType.EventToken => "event",
            _ => "other",
        };

        void OnQuestClaimed(GuideQuestDef quest, List<MetaReward> rewards)
        {
            try
            {
                PlayLog.Begin("quest_claim").Str("id", quest?.id).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void OnBasketClaimed(IdleChestClaimResult result)
        {
            if (result == null) return;
            try
            {
                if (result.IsQuickIdle)
                {
                    PlayLog.Begin("quick_idle").Bool("paid", result.GemSpent > 0).Bool("ticket", result.UsedQuickTicket).End();
                    return;
                }
                PlayLog.Begin("basket_claim").Num("h", result.Duration.TotalHours, 1).Bool("double", result.Doubled).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void OnFeatureUnlocked(FeatureId feature)
        {
            try
            {
                PlayLog.Begin("feature_unlock").Str("id", feature.ToString()).End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void OnStepCompleted(FtueStepDef step)
        {
            try
            {
                int index = step != null && _metaDb != null && _metaDb.ftueSteps != null ? _metaDb.ftueSteps.IndexOf(step) : -1;
                PlayLog.Begin("ftue_step")
                    .Int("n", index + 1)
                    .Str("id", step?.id)
                    .Num("t", Math.Max(0f, Now - _sessionStart), 1)
                    .End();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        void Warn(Exception e)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[PlayLog] 기록 구독 오류 (이후 같은 경고는 생략): {e.Message}");
        }
    }
}
