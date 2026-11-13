using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 기능 개방 (F-03). 첫 세션에 정보가 쏟아지지 않게 기능을 스테이지·튜토리얼 진행에 맞춰 연다.
    ///   · 개방 조건 = MetaDatabase.features[기능].conditions 중 하나라도 충족 (ProgressTracker.IsMet). 정의가 없거나 조건이 비면 처음부터 열림.
    ///   · 한 번 열린 기능은 닫히지 않는다 (meta.features.unlocked). 조건이 비어 처음부터 열린 기능은 카드·점 없이 연다.
    ///   · 새로 열리면 Unlocked → showCard면 PopupQueue에 FeatureUnlock 카드 (스테이지 사이에 뜸), 보상(rewards)이 있으면 지급, 빨간 점 "새로 열림".
    ///     MarkSeen(그 화면에 들어감)으로 점이 꺼진다.
    ///   · 옛 세이브(meta.features.silentSyncPending): 첫 Evaluate에서 이미 조건을 넘은 기능은 카드 없이 열고 봤음 처리.
    ///   · 생성자에서 Progress.Changed를 구독해 Evaluate한다 (스테이지 클리어는 StageClear 카운터로, 튜토리얼 단계 변화는
    ///     Tutorial이 부르는 Progress.MarkDirty로 들어온다). MetaSession.Start도 한 번 부른다. 할당은 새로 열릴 때만.
    ///   · FeatureId.EchoSlot6은 FormationSystem의 기존 규칙(2-10 도달)을 그대로 따르도록 콘텐츠가 같은 조건을 쓴다.
    /// UI가 잠긴 기능을 누르면 TryUse로 확인하고 false면 토스트를 띄운다 ("스테이지 1-4를 클리어하면 열려요").
    /// <para>
    /// 한 기능이 열려 다른 기능의 FeatureUnlocked 조건이 충족되면 같은 Evaluate 안에서 이어서 연다.
    /// 문구가 없을 때 안내는 조건 중 스테이지 조건을 먼저 쓴다 (편성처럼 "튜토리얼 또는 1-3"인 기능도 "스테이지 1-3을 클리어하면 열려요").
    /// </para>
    /// <para>
    /// 개방 속도 조절 (SPEC5 §2.8 — 4차 P4-0, 로드맵 Q2):
    ///   · alsoRequire (AND): conditions 중 하나 + alsoRequire 전부가 맞아야 열린다 (예: 앱을 켜 둔 시간 PlayMinutes ≥ 20).
    ///     ForceUnlock과 옛 세이브의 조용한 동기화는 alsoRequire를 보지 않는다. 비어 있으면 지금과 같다.
    ///   · 카드 간격: 개방 카드는 마지막 카드를 닫은 뒤 앞에서 보는 시간(일시정지 제외)이 CardSpacingSeconds(180초) 지나야 띄운다.
    ///     같은 평가에서 함께 열린 카드는 이어서 띄운다. 발표자가 FeatureUnlock 요청을 꺼내기 전에 ShouldDropCard(이미 들어가 봄 → 버림)와
    ///     CardReady를 묻고, 카드를 닫으면 NotifyCardClosed를 부른다. 시간은 MetaSession.Tick이 TickCards로 넣는다.
    ///     발표자가 묻지 않으면 지금처럼 바로 띄운다 (속도 조절은 콘텐츠 · 발표자가 켤 때부터).
    /// </para>
    /// </summary>
    public class FeatureGateSystem
    {
        /// <summary>기능이 새로 열림 (조건 충족, 강제 개방 포함. 조용한 동기화와 처음부터 열린 기능은 제외)</summary>
        public event Action<FeatureId> Unlocked;
        /// <summary>열림/봤음 상태가 바뀜</summary>
        public event Action Changed;

        /// <summary>개방 카드 사이 최소 간격 (앞에서 보는 시간, 초 — SPEC5 §2.8)</summary>
        public const float CardSpacingSeconds = 180f;
        /// <summary>스테이지 조건은 넘었는데 alsoRequire(앱을 켜 둔 시간 등)만 남았을 때 잠김 안내 (SPEC5 §2.8)</summary>
        public const string PlayFloorMessage = "조금만 더 수선점을 돌보면 열려요";

        static readonly FeatureId[] AllFeatures = (FeatureId[])Enum.GetValues(typeof(FeatureId));

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly ProgressTracker _progress;
        readonly PopupQueue _popups;
        readonly MetaRewardService _rewards;

        bool _evaluating;
        readonly List<FeatureUnlockDef> _justOpened = new();

        // ── 카드 간격 (저장 안 함 — 앱을 새로 켜면 첫 카드는 바로) ──
        /// <summary>마지막 카드를 닫은 뒤 앞에서 본 시간 (초). 처음에는 간격만큼 차 있어 첫 카드는 바로 뜬다</summary>
        float _cardClock = CardSpacingSeconds;
        /// <summary>카드를 큐에 넣을 때마다 묶음 번호 (같은 평가 = 같은 번호)</summary>
        int _cardBatch;
        /// <summary>마지막으로 닫힌 카드의 묶음 번호 (0 = 아직 없음)</summary>
        int _lastClosedBatch;
        /// <summary>기능별 카드 묶음 번호 (FeatureId 값 → 번호, 0 = 없음)</summary>
        readonly int[] _cardBatchOf = new int[MaxFeatureValue() + 1];

        public FeatureGateSystem(PlayerData data, MetaDatabase db, ProgressTracker progress, PopupQueue popups, MetaRewardService rewards)
        {
            _data = data;
            _db = db;
            _progress = progress;
            _popups = popups;
            _rewards = rewards;

            if (_progress != null)
            {
                _progress.FeatureResolver = IsUnlocked;
                _progress.Changed += OnProgressChanged;
            }
        }

        FeatureSave Save => (_data.meta ??= new MetaSave()).features ??= new FeatureSave();

        /// <summary>기능이 열렸는지 (정의 없음 = 열림)</summary>
        public bool IsUnlocked(FeatureId feature)
        {
            var def = GetDef(feature);
            if (def == null || def.conditions == null || def.conditions.Count == 0) return true;
            return Save.unlocked.Contains(feature);
        }

        public FeatureUnlockDef GetDef(FeatureId feature) => _db != null ? _db.GetFeature(feature) : null;

        /// <summary>잠긴 탭/버튼 아래 작은 글자 (예: "1-4"). 열렸으면 빈 문자열</summary>
        public string GetLockedHint(FeatureId feature)
        {
            if (IsUnlocked(feature)) return string.Empty;
            var def = GetDef(feature);
            // 스테이지 조건은 넘었고 alsoRequire(앱을 켜 둔 시간 등)만 남음: 지난 스테이지를 보여 주지 않는다
            if (WaitsOnlyForAlsoRequire(def)) return string.Empty;
            if (!string.IsNullOrEmpty(def.lockedHint)) return def.lockedHint;
            var condition = PrimaryCondition(def);
            if (condition == null) return string.Empty;
            switch (condition.type)
            {
                case MetaConditionType.StageCleared:
                    return BattleSystem.FormatStage((int)Math.Max(1, Math.Min(int.MaxValue, condition.target)));
                case MetaConditionType.InstallDays:
                    return $"{condition.target}일째";
                case MetaConditionType.ShopLevel:
                    return $"Lv.{condition.target}";
                case MetaConditionType.FtueStep:
                case MetaConditionType.FtueDone:
                    return "튜토리얼";
                default:
                    return string.Empty;
            }
        }

        /// <summary>잠겼을 때 안내 문구 (예: "스테이지 1-4를 클리어하면 열려요"). 콘텐츠 문구가 없으면 첫 조건으로 만든다</summary>
        public string GetRequirementText(FeatureId feature)
        {
            if (IsUnlocked(feature)) return string.Empty;
            var def = GetDef(feature);
            if (WaitsOnlyForAlsoRequire(def)) return AlsoRequireText(def);
            if (!string.IsNullOrEmpty(def.lockedToast)) return def.lockedToast;
            var condition = PrimaryCondition(def);
            if (condition == null) return "아직 열리지 않았어요";
            switch (condition.type)
            {
                case MetaConditionType.StageCleared:
                {
                    string stage = BattleSystem.FormatStage((int)Math.Max(1, Math.Min(int.MaxValue, condition.target)));
                    return $"스테이지 {MetaText.WithObject(stage)} 클리어하면 열려요";
                }
                case MetaConditionType.FtueStep:
                    return "튜토리얼을 조금 더 진행하면 열려요";
                case MetaConditionType.FtueDone:
                    return "튜토리얼을 마치면 열려요";
                case MetaConditionType.InstallDays:
                    return $"수선점을 연 지 {condition.target}일째에 열려요";
                case MetaConditionType.ShopLevel:
                    return $"수선점 Lv.{condition.target}에 열려요";
                default:
                {
                    string text = _progress != null ? _progress.Describe(condition) : string.Empty;
                    return string.IsNullOrEmpty(text) ? "아직 열리지 않았어요" : $"{text} 달성 시 열려요";
                }
            }
        }

        /// <summary>열렸는데 아직 안 가 봄 (빨간 점 "새로 열림")</summary>
        public bool IsNew(FeatureId feature)
        {
            var save = Save;
            return save.unlocked.Contains(feature) && !save.seen.Contains(feature);
        }

        /// <summary>그 기능 화면에 들어갔음 → "새로 열림" 점 끄기</summary>
        public void MarkSeen(FeatureId feature)
        {
            if (!IsUnlocked(feature)) return;
            if (!Save.seen.Add(feature)) return;
            Changed?.Invoke();
        }

        /// <summary>
        /// 잠긴 기능을 쓰려 할 때 확인. 열렸으면 true. 잠겼으면 false와 토스트 문구(GetRequirementText).
        /// 예: if (!Game.Features.TryUse(FeatureId.Gacha, out var toast)) UIManager.Instance.ShowToast(toast);
        /// </summary>
        public bool TryUse(FeatureId feature, out string lockedMessage)
        {
            if (IsUnlocked(feature))
            {
                lockedMessage = string.Empty;
                return true;
            }
            lockedMessage = GetRequirementText(feature);
            return false;
        }

        // ───────────────────────────── 카드 간격 (SPEC5 §2.8) ─────────────────────────────

        /// <summary>
        /// 앞에서 보낸 시간을 더한다 (초, 일시정지 · 복귀 첫 프레임 제외 — MetaSession.Tick이 앱을 켜 둔 시간과 같은 값으로 부른다). 할당 없음
        /// </summary>
        public void TickCards(float foregroundSeconds)
        {
            if (!(foregroundSeconds > 0f) || float.IsInfinity(foregroundSeconds)) return;
            // 아주 오래 켜 두어도 float가 커지기만 하지 않게 간격의 두 배에서 멈춘다
            _cardClock = Math.Min(_cardClock + foregroundSeconds, CardSpacingSeconds * 2f);
        }

        /// <summary>
        /// 그 기능의 개방 카드를 지금 띄워도 되는지 (발표자가 FeatureUnlock 요청을 꺼내기 전에 묻는다):
        /// 마지막 카드를 닫은 뒤 CardSpacingSeconds가 지났거나, 마지막 카드와 같은 평가에서 함께 열린 카드면 true.
        /// 먼저 ShouldDropCard를 볼 것 (이미 들어가 본 기능의 카드는 버린다)
        /// </summary>
        public bool CardReady(FeatureId feature)
        {
            if (_cardClock >= CardSpacingSeconds) return true;
            int index = (int)feature;
            int batch = index >= 0 && index < _cardBatchOf.Length ? _cardBatchOf[index] : 0;
            return batch != 0 && batch == _lastClosedBatch;
        }

        /// <summary>큐에 든 그 기능의 카드를 버려야 하는지 (플레이어가 이미 그 화면에 들어가 봄 — IsNew가 꺼짐)</summary>
        public bool ShouldDropCard(FeatureId feature) => !IsNew(feature);

        /// <summary>다음 카드까지 남은 시간 (초, 0 = 지금 띄울 수 있음)</summary>
        public float SecondsUntilNextCard => Math.Max(0f, CardSpacingSeconds - _cardClock);

        /// <summary>개방 카드가 닫혔다 (발표자가 부른다) → 간격을 다시 센다. 같은 평가에서 함께 열린 카드는 이어서 띄울 수 있다</summary>
        public void NotifyCardClosed(FeatureId feature)
        {
            int index = (int)feature;
            _lastClosedBatch = index >= 0 && index < _cardBatchOf.Length ? _cardBatchOf[index] : 0;
            _cardClock = 0f;
        }

        /// <summary>
        /// 잠긴 기능 중 조건을 넘은 것을 연다. 새로 연 수를 돌려준다.
        /// silentSyncPending이면 이번에 연 것은 카드·점 없이 연다 (그 뒤 플래그를 끈다).
        /// </summary>
        public int Evaluate()
        {
            if (_data == null || _evaluating) return 0;
            var save = Save;
            bool silent = save.silentSyncPending;
            if (_db == null || _db.features == null || _db.features.Count == 0)
            {
                if (silent)
                {
                    save.silentSyncPending = false;
                    Changed?.Invoke();
                }
                return 0;
            }

            _evaluating = true;
            int opened = 0;
            bool changed = false;
            _justOpened.Clear();
            try
            {
                // 한 기능이 열리면 FeatureUnlocked 조건이 걸린 다른 기능도 이어서 열릴 수 있어 바뀌지 않을 때까지 돈다
                bool any = true;
                for (int pass = 0; any && pass < 8; pass++)
                {
                    any = false;
                    var features = _db.features;
                    for (int i = 0; i < features.Count; i++)
                    {
                        var def = features[i];
                        if (def == null || save.unlocked.Contains(def.feature)) continue;

                        if (def.conditions == null || def.conditions.Count == 0)
                        {
                            // 처음부터 열린 기능: 카드·점 없이
                            save.unlocked.Add(def.feature);
                            save.seen.Add(def.feature);
                            changed = true;
                            continue;
                        }
                        if (!AnyMet(def.conditions)) continue;
                        // 속도 조절 AND 조건 (옛 세이브의 조용한 동기화는 보지 않는다 — 이미 지난 기능은 그대로 연다)
                        if (!silent && !AllMet(def.alsoRequire)) continue;

                        save.unlocked.Add(def.feature);
                        changed = true;
                        any = true;
                        opened++;
                        if (silent)
                        {
                            save.seen.Add(def.feature);
                            save.cardShown.Add(def.feature);
                        }
                        else
                        {
                            _justOpened.Add(def);
                        }
                    }
                }
                if (silent)
                {
                    save.silentSyncPending = false;
                    changed = true;
                }
            }
            finally
            {
                _evaluating = false;
            }

            // 같은 평가에서 열린 카드는 한 묶음 (카드 간격: 이어서 띄움)
            if (_justOpened.Count > 0) _cardBatch++;
            for (int i = 0; i < _justOpened.Count; i++) Announce(_justOpened[i], showCard: true);
            _justOpened.Clear();
            if (changed)
            {
                _progress?.MarkDirty();
                Changed?.Invoke();
            }
            return opened;
        }

        /// <summary>조건과 상관없이 연다 (튜토리얼 / 개발자 메뉴, alsoRequire도 보지 않는다). showCard면 카드도 큐에 넣는다</summary>
        public void ForceUnlock(FeatureId feature, bool showCard)
        {
            var save = Save;
            if (!save.unlocked.Add(feature)) return;
            var def = GetDef(feature);
            if (def == null || def.conditions == null || def.conditions.Count == 0)
            {
                // 원래 열려 있던 기능: 기록만
                save.seen.Add(feature);
                Changed?.Invoke();
                return;
            }
            _cardBatch++;
            Announce(def, showCard);
            _progress?.MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>새로 열렸지만 아직 안 가 본 기능 (into는 비우고 채움)</summary>
        public void GetNewFeatures(List<FeatureId> into)
        {
            if (into == null) return;
            into.Clear();
            for (int i = 0; i < AllFeatures.Length; i++)
            {
                if (IsNew(AllFeatures[i])) into.Add(AllFeatures[i]);
            }
        }

        /// <summary>
        /// 다음에 열릴 기능 (아직 잠긴 것 중 콘텐츠 순서가 가장 앞). 없으면 false.
        /// 스테이지 HUD "다음 개방: 원정 (1-4)" 같은 안내용
        /// </summary>
        public bool TryGetNextLocked(out FeatureUnlockDef def)
        {
            def = null;
            if (_db == null || _db.features == null) return false;
            foreach (var candidate in _db.features)
            {
                if (candidate == null || candidate.conditions == null || candidate.conditions.Count == 0) continue;
                if (IsUnlocked(candidate.feature)) continue;
                def = candidate;
                return true;
            }
            return false;
        }

        /// <summary>기능 이름 (콘텐츠 이름, 없으면 기본 이름)</summary>
        public string GetName(FeatureId feature)
        {
            var def = GetDef(feature);
            return def != null && !string.IsNullOrEmpty(def.name) ? def.name : MetaText.FeatureName(feature);
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        void OnProgressChanged() => Evaluate();

        bool AnyMet(List<MetaCondition> conditions)
        {
            if (_progress == null) return false;
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition != null && _progress.IsMet(condition)) return true;
            }
            return false;
        }

        /// <summary>alsoRequire 전부 충족 (비었거나 null이면 true — 지금 콘텐츠). 진행 기록기가 없으면 false</summary>
        bool AllMet(List<MetaCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0) return true;
            if (_progress == null) return false;
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition != null && !_progress.IsMet(condition)) return false;
            }
            return true;
        }

        /// <summary>conditions 중 하나는 맞는데 alsoRequire 때문에 아직 잠김</summary>
        bool WaitsOnlyForAlsoRequire(FeatureUnlockDef def) =>
            def?.alsoRequire != null && def.alsoRequire.Count > 0 && def.conditions != null && def.conditions.Count > 0
            && AnyMet(def.conditions) && !AllMet(def.alsoRequire);

        /// <summary>alsoRequire만 남았을 때 안내: 앱을 켜 둔 시간(PlayMinutes)이 남았으면 PlayFloorMessage, 아니면 남은 첫 조건 설명</summary>
        string AlsoRequireText(FeatureUnlockDef def)
        {
            foreach (var condition in def.alsoRequire)
            {
                if (condition == null || _progress == null || _progress.IsMet(condition)) continue;
                if (condition.counter == CounterKey.PlayMinutes && condition.type == MetaConditionType.Counter) return PlayFloorMessage;
                string text = _progress.Describe(condition);
                return string.IsNullOrEmpty(text) ? PlayFloorMessage : $"{text} 달성 시 열려요";
            }
            return PlayFloorMessage;
        }

        static int MaxFeatureValue()
        {
            int max = 0;
            foreach (var feature in AllFeatures) max = Math.Max(max, (int)feature);
            return max;
        }

        /// <summary>새로 열림 알림: 카드(한 번만) → 보상 → Unlocked</summary>
        void Announce(FeatureUnlockDef def, bool showCard)
        {
            var save = Save;
            int index = (int)def.feature;
            if (index >= 0 && index < _cardBatchOf.Length) _cardBatchOf[index] = _cardBatch;
            if (showCard && def.showCard && save.cardShown.Add(def.feature)) _popups?.Enqueue(MetaPopupRequest.FeatureUnlock(def.feature));
            if (def.rewards != null && def.rewards.Count > 0 && _rewards != null) _rewards.Grant(def.rewards);
            Unlocked?.Invoke(def.feature);
        }

        /// <summary>안내에 쓸 조건: 스테이지 조건이 있으면 그것, 없으면 첫 조건</summary>
        static MetaCondition PrimaryCondition(FeatureUnlockDef def)
        {
            if (def?.conditions == null || def.conditions.Count == 0) return null;
            foreach (var condition in def.conditions)
            {
                if (condition != null && condition.type == MetaConditionType.StageCleared) return condition;
            }
            foreach (var condition in def.conditions)
            {
                if (condition != null) return condition;
            }
            return null;
        }
    }
}
