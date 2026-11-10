using System;
using System.Collections.Generic;
using System.Globalization;

namespace IdleGame
{
    /// <summary>
    /// 첫 10분 튜토리얼 (FTUE-01) 상태 기계. 화면(코치마크·손가락·말풍선·인트로)은 UI/Tutorial이 이 시스템을 보고 그린다.
    ///   · 단계 = MetaDatabase.ftueSteps 순서. meta.ftue.step = 지금 단계 인덱스(끝낸 단계 수). 끄고 다시 켜도 그 단계부터.
    ///   · 단계는 trigger가 맞으면 시작(StepStarted)하고, complete 조건(ProgressTracker.IsMet)이 충족되면 끝(StepCompleted) → 보상 → 다음.
    ///     완료 판정은 Progress.Changed(프레임당 최대 1번)와 Refresh에서. 단계 시작 때 action을 알린다 (UI/전투 쪽이 수행, SPEC3 참고).
    ///   · 마지막 단계가 끝나면 done + Finished + PopupQueue에 졸업 카드. 옛 세이브와 "튜토리얼 건너뛰기"는 바로 done.
    ///   · 건너뛰기(Skip): 튜토리얼 단계로 열리는 기능을 카드 없이 열고, "새로 열림" 점은 하나만 남긴다(가이드 퀘스트가 가리키는 기능,
    ///     없으면 소환 — M09). 졸업 선물(마지막 단계 보상)은 그대로 준다 (M74). 졸업 카드·출석부 넘김은 없다.
    ///   · 단계가 바뀌면 Progress.MarkDirty()를 불러 FtueStep/FtueDone 조건(기능 개방 등)이 다시 평가되게 한다.
    ///   · 원칙: 글 최대 2줄(한 줄 40자), 늘 손가락, 전투는 멈추지 않음, FTUE가 끝날 때까지 시작 공지를 띄우지 않음(IsActive를 발표자가 확인).
    /// 구현 메모:
    ///   · 시작된 단계는 meta.ftue.flags에 RunningFlag로 남긴다 → 스테이지 사이(BetweenStages)·지연(AfterDelay) 단계를
    ///     시작한 뒤 앱을 껐다 켜도 trigger를 다시 기다리지 않고 그 단계를 바로 다시 시작한다.
    ///   · 구독은 하지 않는다 (MetaSession.Start가 Progress.Changed → Refresh, Tick을 연결). 안전망으로 Tick이 0.5초마다 Refresh(false).
    ///   · 할당은 단계가 바뀔 때만 (trigger 인자 해석, 보상 지급). 매 프레임 경로(Tick/Refresh 대기 중)는 할당 없음.
    /// 담당: M4 (튜토리얼 & 팡파르).
    /// </summary>
    public class TutorialSystem
    {
        /// <summary>단계 시작 (코치마크 표시, action 수행)</summary>
        public event Action<FtueStepDef> StepStarted;
        /// <summary>단계 끝 (손가락 치우기, 보상 날아가기)</summary>
        public event Action<FtueStepDef> StepCompleted;
        /// <summary>졸업 또는 건너뜀</summary>
        public event Action Finished;
        /// <summary>단계/상태가 바뀜</summary>
        public event Action Changed;

        /// <summary>meta.ftue.flags 안의 "지금 단계가 시작된 상태" 표시 (재시작 이어 하기)</summary>
        const string RunningFlag = "ftue.running";
        /// <summary>안전망 Refresh 간격 (초). 완료 조건 대부분은 Progress.Changed로 바로 들어온다</summary>
        const float RecheckInterval = 0.5f;
        /// <summary>한 번의 Refresh에서 연달아 끝낼 수 있는 단계 수 상한 (잘못된 콘텐츠로 무한 반복 방지)</summary>
        const int MaxStepsPerRefresh = 64;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly ProgressTracker _progress;
        readonly FeatureGateSystem _features;
        readonly MetaRewardService _rewards;
        readonly PopupQueue _popups;
        /// <summary>건너뛸 때 "새로 열림" 점을 남길 기능을 고르는 데 쓴다 (없으면 소환)</summary>
        readonly GuideQuestSystem _guide;

        bool _begun;
        bool _running;
        bool _refreshing;
        /// <summary>Skip 도중 (기능 개방 알림이 Refresh를 불러 단계를 끝내지 않게 막는다)</summary>
        bool _skipping;
        /// <summary>trigger 인자를 해석해 둔 단계 인덱스 (-1 = 아직)</summary>
        int _preparedIndex = -1;
        bool _hasTriggerFeature;
        FeatureId _triggerFeature;
        float _delayTarget;
        float _delayElapsed;
        float _recheckTimer;
        /// <summary>BattleProtection 단계 인덱스 (-2 = 아직 안 찾음, -1 = 없음)</summary>
        int _protectionIndex = -2;

        public TutorialSystem(PlayerData data, MetaDatabase db, ProgressTracker progress, FeatureGateSystem features,
            MetaRewardService rewards, PopupQueue popups, GuideQuestSystem guide = null)
        {
            _data = data;
            _db = db;
            _progress = progress;
            _features = features;
            _rewards = rewards;
            _popups = popups;
            _guide = guide;
        }

        /// <summary>건너뛰기 토스트 기본 문구 (받은 선물이 없을 때)</summary>
        public const string SkipMessage = "튜토리얼을 건너뛰었어요";

        /// <summary>
        /// 마지막 Skip이 띄울 토스트 ("튜토리얼을 건너뛰었어요 · 별조각 300을 받았어요"). Skip 전에는 빈 문자열.
        /// 설정 팝업이 Skip 뒤에 이 문구로 토스트를 띄운다.
        /// </summary>
        public string LastSkipMessage { get; private set; } = string.Empty;

        FtueSave Save => _data.meta.ftue;

        /// <summary>졸업했거나 건너뜀 (단계 데이터가 없어도 true)</summary>
        public bool IsDone => Save.done || StepCount == 0;

        /// <summary>진행 중 (안 끝남 + 단계 데이터가 있음)</summary>
        public bool IsActive => !Save.done && StepCount > 0;

        /// <summary>지금 단계 인덱스</summary>
        public int StepIndex => _data.meta.ftue.step;

        /// <summary>튜토리얼 단계 수 (콘텐츠)</summary>
        public int StepCount => _db != null ? _db.ftueSteps.Count : 0;

        /// <summary>지금 단계 정의 (끝났으면 null)</summary>
        public FtueStepDef Current => IsActive ? _db.GetFtueStep(StepIndex) : null;

        /// <summary>지금 단계가 시작됨 (trigger 충족 뒤, 끝나기 전)</summary>
        public bool IsStepRunning => _running && IsActive;

        /// <summary>
        /// 1-1~1-3 패배 불가 보호가 켜져 있어야 하는지 (BattleProtection 단계부터 졸업 전까지).
        /// 전투 시스템 훅이 생기면 B1이 이 값을 읽는다.
        /// </summary>
        public bool WantsBattleProtection
        {
            get
            {
                if (!IsActive) return false;
                int index = ProtectionIndex;
                return index >= 0 && StepIndex >= index;
            }
        }

        int ProtectionIndex
        {
            get
            {
                if (_protectionIndex != -2) return _protectionIndex;
                _protectionIndex = -1;
                for (int i = 0; i < StepCount; i++)
                {
                    var step = _db.ftueSteps[i];
                    if (step != null && step.action == FtueAction.BattleProtection)
                    {
                        _protectionIndex = i;
                        break;
                    }
                }
                return _protectionIndex;
            }
        }

        /// <summary>부팅 때 한 번 (MetaSession.Start). 새 게임이면 0단계 시작, 진행 중이면 그 단계 다시 시작</summary>
        public void Begin()
        {
            if (_begun) return;
            _begun = true;
            if (!IsActive) return;

            var save = Save;
            if (save.step >= StepCount)
            {
                // 콘텐츠가 줄어 남은 단계가 없다 — 카드 없이 끝낸다
                Finish(graduated: false);
                return;
            }

            PrepareStep();
            // 시작된 채로 꺼졌던 단계는 trigger를 다시 기다리지 않는다
            if (save.flags.Contains(RunningFlag)) StartStep(Current);
            Refresh(false);
        }

        /// <summary>trigger와 complete 조건을 다시 본다 (Progress.Changed / 스테이지 사이 / 기능 개방에서). 프레임당 여러 번 불러도 됨</summary>
        public void Refresh(bool betweenStages)
        {
            // Begin보다 먼저 불려도 이어 하기(RunningFlag) 처리를 먼저 한다
            if (!_begun) Begin();
            if (_refreshing || _skipping || !IsActive) return;
            _refreshing = true;
            try
            {
                for (int guard = 0; guard < MaxStepsPerRefresh && IsActive; guard++)
                {
                    var step = Current;
                    if (step == null)
                    {
                        // 단계 인덱스가 콘텐츠를 넘었다
                        Finish(graduated: false);
                        break;
                    }
                    if (_preparedIndex != StepIndex) PrepareStep();

                    if (!_running)
                    {
                        if (!IsTriggerMet(step, betweenStages)) break;
                        StartStep(step);
                        // StepStarted를 받은 쪽이 Skip 등으로 상태를 바꿨을 수 있다
                        if (!IsActive || Current != step) continue;
                    }

                    if (!_progress.IsMet(step.complete)) break;
                    CompleteStep(step);
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>AfterDelay 단계 시간 재기 (실제 시간 dt). MetaSession.Tick이 부른다</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!_begun || !IsActive || !(unscaledDeltaTime > 0f)) return;

            if (!_running && _preparedIndex == StepIndex)
            {
                var step = Current;
                if (step != null && step.trigger == FtueTrigger.AfterDelay)
                {
                    _delayElapsed += unscaledDeltaTime;
                    if (_delayElapsed >= _delayTarget)
                    {
                        Refresh(false);
                        return;
                    }
                }
            }

            _recheckTimer += unscaledDeltaTime;
            if (_recheckTimer < RecheckInterval) return;
            _recheckTimer = 0f;
            // 안전망: Progress.Changed를 놓친 조건(편성·플래그 등)도 늦어도 0.5초 안에 넘어간다.
            // 스테이지 사이 trigger는 전투 상태를 아는 쪽(MetaSession / FtueDirector)이 Refresh(true)로 연다.
            Refresh(false);
        }

        /// <summary>UI가 알림 (예: 인트로 끝, 시너지 상세 열어 봄) — ProgressTracker.SetFlag(flag) 후 Refresh</summary>
        public void Signal(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            _progress.SetFlag(flag);
            if (_begun) Refresh(false);
        }

        /// <summary>튜토리얼 안에서 한 번만 보여 줄 힌트 등 (meta.ftue.flags)</summary>
        public bool HasHintFlag(string flag) => !string.IsNullOrEmpty(flag) && Save.flags.Contains(flag);

        public void SetHintFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) Save.flags.Add(flag);
        }

        /// <summary>
        /// 졸업 선물 = 마지막 단계 보상 (건너뛰어도 받는다). 설정 확인 창 "졸업 선물(별조각 300)은 그대로 받아요"용 문구, 없으면 빈 문자열.
        /// 이미 끝났으면(졸업·건너뜀) 빈 문자열.
        /// </summary>
        public string GraduationGiftText
        {
            get
            {
                if (!IsActive || _rewards == null) return string.Empty;
                var rewards = GraduationRewards;
                return rewards != null && rewards.Count > 0 ? _rewards.Describe(rewards) : string.Empty;
            }
        }

        /// <summary>마지막 단계 보상 목록 (콘텐츠, 없으면 null)</summary>
        List<MetaReward> GraduationRewards => StepCount > 0 ? _db.ftueSteps[StepCount - 1]?.rewards : null;

        /// <summary>
        /// 설정 "튜토리얼 건너뛰기": 남은 단계의 기능을 열고(조건이 FtueStep인 기능) done. 받은 졸업 선물을 돌려준다 (없으면 빈 목록).
        ///   · 기능은 카드 없이 연다. "새로 열림" 점은 가이드 퀘스트가 지금 가리키는 기능만 남기고(없으면 소환) 나머지는 봤음 처리 (M09).
        ///   · 졸업 선물(마지막 단계 보상)을 지급한다 (M74). 졸업 카드와 출석부 넘김은 없다. 토스트 문구는 LastSkipMessage.
        /// </summary>
        public List<MetaReward> Skip()
        {
            var granted = new List<MetaReward>();
            if (Save.done) return granted;

            _skipping = true;
            try
            {
                if (_db != null && _features != null)
                {
                    var forced = new List<FeatureId>();
                    foreach (var def in _db.features)
                    {
                        if (def == null || !HasFtueStepCondition(def) || _features.IsUnlocked(def.feature)) continue;
                        // 건너뛰기는 조용히: 카드 없이 연다
                        _features.ForceUnlock(def.feature, false);
                        forced.Add(def.feature);
                    }
                    // 한꺼번에 열린 기능마다 점이 켜지면 화면이 점투성이가 된다 — 다음에 갈 곳 하나만 남긴다
                    var keep = KeepNewAfterSkip();
                    for (int i = 0; i < forced.Count; i++)
                    {
                        if (forced[i] != keep) _features.MarkSeen(forced[i]);
                    }
                }

                var gift = GraduationRewards;
                if (gift != null && gift.Count > 0 && _rewards != null) granted = _rewards.Grant(gift);
                LastSkipMessage = SkipToast(granted);

                Save.skipped = true;
            }
            finally
            {
                _skipping = false;
            }
            Finish(graduated: false);
            return granted;
        }

        /// <summary>"튜토리얼을 건너뛰었어요 · 별조각 300을 받았어요" (받은 것이 없으면 앞부분만)</summary>
        string SkipToast(IReadOnlyList<MetaReward> granted)
        {
            string gifts = granted != null && granted.Count > 0 && _rewards != null ? _rewards.Describe(granted) : string.Empty;
            return string.IsNullOrEmpty(gifts) ? SkipMessage : $"{SkipMessage} · {MetaText.WithObject(gifts)} 받았어요";
        }

        /// <summary>건너뛴 뒤 "새로 열림" 점을 남길 기능: 가이드 퀘스트의 이동 기능, 없으면(전투·퀘스트 없음) 소환</summary>
        FeatureId KeepNewAfterSkip()
        {
            var quest = _guide?.Current;
            return quest != null && quest.navFeature != FeatureId.Battle ? quest.navFeature : FeatureId.Gacha;
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        static bool HasFtueStepCondition(FeatureUnlockDef def)
        {
            if (def.conditions == null) return false;
            foreach (var condition in def.conditions)
            {
                if (condition != null && condition.type == MetaConditionType.FtueStep) return true;
            }
            return false;
        }

        /// <summary>지금 단계의 trigger 인자를 한 번만 해석한다 (단계가 바뀔 때만 할당)</summary>
        void PrepareStep()
        {
            _preparedIndex = StepIndex;
            _delayElapsed = 0f;
            _delayTarget = 0f;
            _hasTriggerFeature = false;

            var step = Current;
            if (step == null) return;
            switch (step.trigger)
            {
                case FtueTrigger.FeatureUnlocked:
                    _hasTriggerFeature = !string.IsNullOrEmpty(step.triggerArg)
                                         && Enum.TryParse(step.triggerArg.Trim(), true, out _triggerFeature);
                    break;
                case FtueTrigger.AfterDelay:
                    if (!float.TryParse(step.triggerArg, NumberStyles.Float, CultureInfo.InvariantCulture, out _delayTarget) || _delayTarget < 0f)
                        _delayTarget = 0f;
                    break;
            }
        }

        bool IsTriggerMet(FtueStepDef step, bool betweenStages)
        {
            switch (step.trigger)
            {
                case FtueTrigger.BetweenStages:
                    return betweenStages;
                case FtueTrigger.FeatureUnlocked:
                    // 인자가 잘못됐으면 막히지 않게 바로 시작
                    return !_hasTriggerFeature || _features == null || _features.IsUnlocked(_triggerFeature);
                case FtueTrigger.AfterDelay:
                    return _delayElapsed >= _delayTarget;
                default:
                    return true;
            }
        }

        void StartStep(FtueStepDef step)
        {
            if (step == null) return;
            _running = true;
            _recheckTimer = 0f;
            Save.flags.Add(RunningFlag);
            StepStarted?.Invoke(step);
            Changed?.Invoke();
        }

        void CompleteStep(FtueStepDef step)
        {
            _running = false;
            var save = Save;
            save.flags.Remove(RunningFlag);
            if (step.rewards != null && step.rewards.Count > 0 && _rewards != null) _rewards.Grant(step.rewards);

            save.step = Math.Min(save.step + 1, StepCount);
            _preparedIndex = -1;
            StepCompleted?.Invoke(step);
            _progress.MarkDirty();

            if (save.step >= StepCount && !save.done) Finish(graduated: true);
            else Changed?.Invoke();
        }

        void Finish(bool graduated)
        {
            var save = Save;
            if (save.done) return;
            save.done = true;
            save.flags.Remove(RunningFlag);
            _running = false;
            _preparedIndex = -1;
            if (graduated) _popups?.Enqueue(MetaPopupRequest.FtueGraduation());
            _progress?.MarkDirty();
            Finished?.Invoke();
            Changed?.Invoke();
        }
    }
}
