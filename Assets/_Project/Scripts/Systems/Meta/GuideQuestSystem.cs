using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 가이드 퀘스트 (Q-01). 전투 화면 왼쪽 아래 한 줄로 "이제 뭐 하지?"에 답한다.
    ///   · MetaDatabase.guideQuests를 순서대로 하나씩 (meta.guide.index). 진행도는 ProgressTracker(조건) — 퀘스트가 시작되기 전 진행도 인정
    ///     (1~3단계는 FTUE에서 이미 끝나 줄이 나타나자마자 "받기" 상태 — 의도된 즉시 보상).
    ///   · 받으면 보상 지급(MetaRewardService) + GuideQuestClaim 카운터 + 다음 퀘스트.
    ///   · 콘텐츠 목록이 끝나면 반복 단계(guideRepeat): "스테이지 X 클리어"(최고 + stageInterval), "수선점 Lv +N", "친구 레벨 +N"을 돌아가며.
    ///     반복 단계 정의는 이 시스템이 만든 GuideQuestDef 한 개를 재사용한다 (Current가 같은 인스턴스, 내용만 바뀜).
    ///     기준값(meta.guide.repeatBaseline)은 그 단계에 처음 들어갈 때 정한다.
    ///   · 줄은 guideVisible 조건(FTUE 첫 보스 뒤)을 넘어야 보인다.
    ///   · Progress.Changed(프레임당 최대 1번)에서 완료 여부를 다시 보고, 바뀌었으면 Changed.
    /// <para>
    /// 반복 단계: 순서 = (index − 콘텐츠 수) % 3 → 스테이지 / 수선점 레벨 / 친구(가장 높은 계보) 레벨.
    /// 진행 값·목표 값은 기준값을 뺀 값이다 (예: "수선점 Lv.25 달성" 3/10). 보상은 단추 goldMinutes분치 + 별조각 gem.
    /// Claimed에 넘기는 반복 단계 정의는 받은 순간의 복사본이다 (다음 단계로 내용이 바뀌어도 연출이 흔들리지 않게).
    /// </para>
    /// </summary>
    public class GuideQuestSystem
    {
        /// <summary>지금 퀘스트, 진행도, 완료 여부, 보임 여부가 바뀜</summary>
        public event Action Changed;
        /// <summary>(받은 퀘스트, 받은 보상) — 보상 날아가기 / 다음 퀘스트 뒤집기 연출</summary>
        public event Action<GuideQuestDef, List<MetaReward>> Claimed;

        /// <summary>반복 단계 종류 수 (스테이지 / 수선점 레벨 / 친구 레벨)</summary>
        const int RepeatKinds = 3;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;
        readonly GrowthSystem _growth;
        readonly CharacterSystem _characters;

        // 반복 단계 정의 (재사용)
        readonly GuideQuestDef _repeat = new() { condition = new MetaCondition(), rewards = new List<MetaReward>() };
        int _repeatBuiltIndex = -1;
        long _repeatBuiltBaseline = long.MinValue;

        // 지난 Refresh 상태 (바뀌었을 때만 Changed)
        bool _hasSnapshot;
        int _lastIndex;
        bool _lastVisible;
        bool _lastComplete;
        long _lastValue;
        long _lastTarget;

        public GuideQuestSystem(PlayerData data, MetaDatabase db, ProgressTracker progress, MetaRewardService rewards,
            FeatureGateSystem features, GrowthSystem growth, CharacterSystem characters)
        {
            _data = data;
            _db = db;
            _progress = progress;
            _rewards = rewards;
            _features = features;
            _growth = growth;
            _characters = characters;

            if (_progress != null) _progress.Changed += Refresh;
        }

        GuideQuestSave Save => (_data.meta ??= new MetaSave()).guide ??= new GuideQuestSave();

        GuideRepeatConfig RepeatConfig => _db != null ? _db.guideRepeat : null;

        /// <summary>줄을 보여 줄지 (guideVisible 충족 + 퀘스트가 있음)</summary>
        public bool IsVisible => Current != null && (_db == null || _progress == null || _progress.IsMet(_db.guideVisible));

        /// <summary>지금 위치 (0부터, = 받은 퀘스트 수)</summary>
        public int Index => _data.meta.guide.index;

        /// <summary>콘텐츠 목록 길이</summary>
        public int ContentCount => _db != null ? _db.guideQuests.Count : 0;

        /// <summary>콘텐츠 목록을 넘은 반복 단계인지</summary>
        public bool IsRepeat => Index >= ContentCount && RepeatConfig != null && RepeatConfig.enabled;

        /// <summary>지금 퀘스트 (없으면 null — 목록이 끝났고 반복도 꺼짐)</summary>
        public GuideQuestDef Current
        {
            get
            {
                if (_db == null) return null;
                var save = Save;
                // 빈 항목은 건너뛴 것으로 본다 (콘텐츠 실수로 줄이 멈추지 않게)
                while (save.index < ContentCount && _db.guideQuests[save.index] == null) save.index++;
                if (save.index < ContentCount) return _db.guideQuests[save.index];
                if (!IsRepeat) return null;
                EnsureRepeat();
                return _repeat;
            }
        }

        /// <summary>지금 퀘스트 진행 값 / 목표 값 (반복 단계는 기준값을 뺀 값)</summary>
        public long CurrentValue
        {
            get
            {
                var def = Current;
                if (def == null) return 0;
                if (IsRepeat) return Math.Max(0, RepeatRawValue(RepeatKind) - Save.repeatBaseline);
                return _progress != null ? Math.Max(0, _progress.GetValue(def.condition)) : 0;
            }
        }

        public long CurrentTarget
        {
            get
            {
                var def = Current;
                if (def == null) return 0;
                if (IsRepeat) return RepeatStep(RepeatKind);
                return _progress != null ? _progress.GetTarget(def.condition) : 1;
            }
        }

        public float CurrentRatio
        {
            get
            {
                var def = Current;
                if (def == null) return 0f;
                if (IsComplete) return 1f;
                long target = CurrentTarget;
                if (target <= 0) return 1f;
                return (float)Math.Max(0, Math.Min(1, (double)CurrentValue / target));
            }
        }

        /// <summary>"2/3"</summary>
        public string ProgressText
        {
            get
            {
                var def = Current;
                if (def == null) return string.Empty;
                if (!IsRepeat && _progress != null) return _progress.FormatProgress(def.condition);
                long target = CurrentTarget;
                long value = IsComplete ? target : Math.Min(target, CurrentValue);
                return $"{MetaText.Comma(value)}/{MetaText.Comma(target)}";
            }
        }

        /// <summary>받을 수 있음 (금색 반짝임 + "받기")</summary>
        public bool IsComplete
        {
            get
            {
                var def = Current;
                if (def == null) return false;
                if (IsRepeat) return CurrentValue >= CurrentTarget;
                return def.condition == null || (_progress != null && _progress.IsMet(def.condition));
            }
        }

        /// <summary>
        /// 지금 퀘스트 보상을 받고 다음으로 넘어간다. 완료가 아니면 false.
        /// granted = 실제로 받은 보상 (보상 날아가기용).
        /// </summary>
        public bool TryClaim(out List<MetaReward> granted)
        {
            granted = null;
            var def = Current;
            if (def == null || !IsComplete) return false;

            bool repeat = IsRepeat;
            var claimedDef = repeat ? CloneRepeat() : def;
            granted = _rewards != null ? _rewards.Grant(def.rewards) : new List<MetaReward>();

            var save = Save;
            save.index++;
            save.repeatBaseline = -1;
            _progress?.Increment(CounterKey.GuideQuestClaim);
            _progress?.MarkDirty();

            Claimed?.Invoke(claimedDef, granted);
            Refresh(force: true);
            return true;
        }

        /// <summary>완료 여부를 다시 보고 바뀌었으면 Changed (Progress.Changed에서 부름)</summary>
        public void Refresh() => Refresh(force: false);

        // ───────────────────────────── 내부 ─────────────────────────────

        void Refresh(bool force)
        {
            int index = Index;
            bool visible = IsVisible;
            bool complete = IsComplete;
            long value = CurrentValue;
            long target = CurrentTarget;
            bool changed = force || !_hasSnapshot || index != _lastIndex || visible != _lastVisible || complete != _lastComplete
                           || value != _lastValue || target != _lastTarget;
            _hasSnapshot = true;
            _lastIndex = index;
            _lastVisible = visible;
            _lastComplete = complete;
            _lastValue = value;
            _lastTarget = target;
            if (changed) Changed?.Invoke();
        }

        int RepeatKind => Math.Max(0, Index - ContentCount) % RepeatKinds;

        /// <summary>반복 단계 종류별 지금 값 (스테이지 = 클리어한 최고 스테이지, 수선점 레벨, 가장 높은 친구 레벨)</summary>
        long RepeatRawValue(int kind)
        {
            switch (kind)
            {
                case 0:
                    return Math.Max(0, Math.Max(1, _data.battle?.highestStage ?? 1) - 1);
                case 1:
                    return _growth != null ? _growth.Level : Math.Max(1, _data.level);
                default:
                {
                    long best = 0;
                    if (_data.lineages != null)
                    {
                        foreach (var pair in _data.lineages)
                        {
                            if (pair.Value != null && pair.Value.level > best) best = pair.Value.level;
                        }
                    }
                    return best;
                }
            }
        }

        /// <summary>
        /// 반복 단계 목표 (기준값에서 더 올릴 양). 수선점·친구 레벨은 최대 레벨에서 자른다 —
        /// 이미 최대면 0이 되어 바로 받을 수 있다 (오를 수 없는 목표로 줄이 멈추지 않게).
        /// </summary>
        long RepeatStep(int kind)
        {
            var config = RepeatConfig;
            if (config == null) return 1;
            long baseline = Math.Max(0, Save.repeatBaseline);
            switch (kind)
            {
                case 0:
                    return Math.Max(1, config.stageInterval);
                case 1:
                {
                    long step = Math.Max(1, config.shopLevelStep);
                    return _growth != null ? Math.Max(0, Math.Min(step, _growth.MaxLevel - baseline)) : step;
                }
                default:
                {
                    long step = Math.Max(1, config.lineageLevelStep);
                    return _characters != null ? Math.Max(0, Math.Min(step, _characters.MaxLevel - baseline)) : step;
                }
            }
        }

        /// <summary>반복 단계 기준값을 정하고(처음 들어갈 때) 재사용 정의 내용을 맞춘다 (바뀔 때만 문구를 다시 만든다)</summary>
        void EnsureRepeat()
        {
            var save = Save;
            int kind = RepeatKind;
            if (save.repeatBaseline < 0) save.repeatBaseline = RepeatRawValue(kind);
            if (_repeatBuiltIndex == save.index && _repeatBuiltBaseline == save.repeatBaseline) return;
            _repeatBuiltIndex = save.index;
            _repeatBuiltBaseline = save.repeatBaseline;

            var config = RepeatConfig;
            long step = RepeatStep(kind);
            long goal = save.repeatBaseline + step;
            var condition = _repeat.condition ??= new MetaCondition();
            condition.targetId = null;
            condition.counter = CounterKey.Login;
            condition.target = goal;
            switch (kind)
            {
                case 0:
                    condition.type = MetaConditionType.StageCleared;
                    _repeat.text = $"스테이지 {BattleSystem.FormatStage((int)Math.Min(int.MaxValue, Math.Max(1, goal)))} 클리어";
                    _repeat.navFeature = FeatureId.Battle;
                    _repeat.focusTarget = CoachTargets.StagePanel;
                    break;
                case 1:
                    condition.type = MetaConditionType.ShopLevel;
                    _repeat.text = $"수선점 Lv.{MetaText.Comma(goal)} 달성";
                    _repeat.navFeature = FeatureId.ShopUpgrade;
                    _repeat.focusTarget = CoachTargets.TabGrowth;
                    break;
                default:
                    condition.type = MetaConditionType.LineageLevel;
                    _repeat.text = $"친구 레벨 {MetaText.Comma(goal)} 달성";
                    _repeat.navFeature = FeatureId.CharacterLevelUp;
                    _repeat.focusTarget = CoachTargets.TabGrowth;
                    break;
            }
            _repeat.id = $"gq_repeat_{save.index - ContentCount + 1}";
            _repeat.icon = config?.icon;

            var rewards = _repeat.rewards ??= new List<MetaReward>();
            rewards.Clear();
            if (config != null && config.goldMinutes > 0) rewards.Add(MetaReward.GoldMinutes(config.goldMinutes));
            if (config != null && config.gem > 0) rewards.Add(MetaReward.Gem(config.gem));
        }

        GuideQuestDef CloneRepeat() => new()
        {
            id = _repeat.id,
            text = _repeat.text,
            condition = _repeat.condition?.Clone(),
            rewards = MetaRewardService.Merge(_repeat.rewards),
            navFeature = _repeat.navFeature,
            focusTarget = _repeat.focusTarget,
            icon = _repeat.icon,
        };
    }
}
