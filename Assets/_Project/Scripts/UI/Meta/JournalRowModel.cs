using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>일지 줄 상태 (JournalRowModel — MissionRowView 버튼 모양)</summary>
    public enum JournalRowState : byte
    {
        /// <summary>진행 중 (이동할 곳 없음 — '진행 중' 회색)</summary>
        InProgress = 0,
        /// <summary>받을 수 있음 ([받기] Gold)</summary>
        Claimable = 1,
        /// <summary>진행 중 + 이동할 곳 있음 ([가기 ›] Primary)</summary>
        Go = 2,
        /// <summary>받음 ([완료], 흐림 + 도장)</summary>
        Done = 3,
        /// <summary>기능이 잠김 (M39 자물쇠 알약, 나중에 받을 수 있음 — 첫 주)</summary>
        Locked = 4,
    }

    /// <summary>
    /// 수선 일지 · 이벤트 모아 보기 줄 하나 (SPEC4 §6.1 · §8.3 · §13.6): MissionRowView.Bind(in JournalRowModel, …)가 미션 말고도 기록 · 이벤트 미션 · 첫 주 과제를
    /// 같은 줄 모양으로 그린다 (미션은 예전 Bind(MissionStatus)를 그대로 쓴다). 값 타입 — 줄마다 새 객체를 만들지 않는다.
    /// Id = 받기 키 (기록 ID · 미션 ID · 과제 ID), Text = 한 줄 (숨은 기록은 '???'), Hint = 숨은 기록 힌트 (Hint 20 Lavender — 없으면 null),
    /// Value / Target = 진행 (Target ≤ 0이면 막대를 숨긴다), PointsText = 점수 알약 ('+5' — 비면 숨김), Reward = 첫 보상 칩 (없으면 숨김),
    /// TierCount / TierReached = 기록 단계 점 (0이면 없음), Nav · Focus = [가기 ›] 이동, LockText = 잠김 알약 글 ('2-3').
    /// </summary>
    public readonly struct JournalRowModel
    {
        public readonly string Id;
        public readonly string Text;
        public readonly string Hint;
        public readonly Sprite Icon;
        public readonly long Value;
        public readonly long Target;
        public readonly JournalRowState State;
        public readonly string PointsText;
        public readonly MetaReward Reward;
        public readonly int TierCount;
        public readonly int TierReached;
        public readonly FeatureId Nav;
        public readonly string Focus;
        public readonly string LockText;

        public JournalRowModel(string id, string text, Sprite icon, long value, long target, JournalRowState state, MetaReward reward,
            FeatureId nav = FeatureId.Battle, string focus = null, string pointsText = null, string hint = null,
            int tierCount = 0, int tierReached = 0, string lockText = null)
        {
            Id = id;
            Text = text;
            Hint = hint;
            Icon = icon;
            Value = value;
            Target = target;
            State = state;
            PointsText = pointsText;
            Reward = reward;
            TierCount = tierCount;
            TierReached = tierReached;
            Nav = nav;
            Focus = focus;
            LockText = lockText;
        }

        /// <summary>진행 비율 0~1 (받음이면 1, 목표가 없으면 0)</summary>
        public float Ratio => State == JournalRowState.Done ? 1f : Target <= 0 ? 0f : Mathf.Clamp01((float)((double)Value / Target));

        public bool CanClaim => State == JournalRowState.Claimable;

        public bool IsDone => State == JournalRowState.Done;

        /// <summary>진행 중 줄의 상태: 이동할 곳이 있으면 Go, 아니면 InProgress</summary>
        public static JournalRowState Pending(FeatureId nav, string focus) =>
            nav != FeatureId.Battle || !string.IsNullOrEmpty(focus) ? JournalRowState.Go : JournalRowState.InProgress;

        static MetaReward First(System.Collections.Generic.List<MetaReward> rewards) =>
            rewards != null && rewards.Count > 0 ? rewards[0] : null;

        /// <summary>수선점 기록 한 줄 (AchievementSystem.GetLines): 받을 수 있는 가장 낮은 단계, 다 받았으면 마지막 단계</summary>
        public static JournalRowModel FromAchievement(AchievementLineStatus line, Sprite hiddenIcon = null)
        {
            var def = line?.Current;
            if (def == null) return default;
            JournalRowState state = line.AllDone ? JournalRowState.Done : line.Claimable ? JournalRowState.Claimable : Pending(def.navFeature, def.focusTarget);
            bool hidden = line.HiddenUnrevealed;
            int reached = line.AllDone ? line.TierCount : Mathf.Clamp(line.Claimable ? line.TierIndex + 1 : line.TierIndex, 0, line.TierCount);
            return new JournalRowModel(def.id, hidden ? "???" : def.title, hidden && hiddenIcon != null ? hiddenIcon : def.icon,
                line.Value, line.Target, state, First(def.rewards), def.navFeature, def.focusTarget,
                hint: hidden ? def.hint : null, tierCount: line.TierCount, tierReached: reached);
        }

        /// <summary>이벤트 미션 한 줄 (EventCalendarSystem.GetMissions)</summary>
        public static JournalRowModel FromEventMission(EventMissionStatus status, Sprite icon = null)
        {
            var def = status?.Def;
            if (def == null) return default;
            var state = status.IsClaimed ? JournalRowState.Done : status.CanClaim ? JournalRowState.Claimable : JournalRowState.InProgress;
            return new JournalRowModel(def.id, def.text, icon, status.Progress, status.Target > 0 ? status.Target : def.target, state, First(def.rewards));
        }

        /// <summary>첫 주 과제 한 줄 (StarterWeekSystem.GetTasks) — 점수 알약 '+5'</summary>
        public static JournalRowModel FromStarterTask(StarterTaskStatus status, string lockText = null)
        {
            var def = status?.Def;
            if (def == null) return default;
            JournalRowState state = status.Claimed ? JournalRowState.Done
                : status.Locked ? JournalRowState.Locked
                : status.CanClaim ? JournalRowState.Claimable
                : Pending(def.navFeature, def.focusTarget);
            return new JournalRowModel(def.id, def.text, null, status.Value, status.Target, state, null, def.navFeature, def.focusTarget,
                pointsText: $"+{def.points}", lockText: lockText);
        }
    }
}
