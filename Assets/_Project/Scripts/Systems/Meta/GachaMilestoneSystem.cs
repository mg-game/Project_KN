using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>소환 마일스톤 한 단계의 상태 (GachaMilestoneSystem.GetSteps가 채운다, 목록 재사용 가능)</summary>
    public sealed class GachaMilestoneStep
    {
        /// <summary>단계 순서 (0부터, 되풀이 단계도 이어서 센다 — GachaMilestoneSave.claimedSteps와 같은 기준)</summary>
        public int Index;
        /// <summary>이 단계에 필요한 누적 소환 수</summary>
        public int Pulls;
        public List<MetaReward> Rewards;
        public bool Reached;
        public bool Claimed;
        /// <summary>마지막 콘텐츠 단계 뒤의 되풀이 단계 (gachaRepeat)</summary>
        public bool IsRepeat;
        public bool CanClaim => Reached && !Claimed;
    }

    /// <summary>
    /// 소환 마일스톤 (PRG-04, SPEC4 §6.4): 누적 소환(Gacha.Pulled에서 GachaResult.IsGuaranteed가 아닌 것 — 첫 만남 확정 소환은 세지 않음)이
    /// 콘텐츠 단계(10/30/50/100/200)에 닿으면 순서대로 하나씩 받는다. 마지막 단계 뒤로는 gachaRepeat.interval마다 같은 보상이 되풀이된다.
    /// 확률과 천장은 건드리지 않는다 (공정함). 친구 선택권(MetaRewardType.FriendSelectTicket → meta.wallet)은 전설 이하 계보 하나를 골라
    /// MetaRewards.Grant(Lineage)로 받는다 (중복이면 기억편) — 고르기 전에는 줄지 않는다 (SPEC4 §2.11 #4).
    /// 빨간 점 RedDotPaths.GachaMilestone = 받을 단계가 있거나 선택권이 남음.
    /// </summary>
    public class GachaMilestoneSystem : IMetaModule
    {
        /// <summary>누적 소환 · 받은 단계 · 선택권이 바뀜</summary>
        public event Action Changed;

        /// <summary>친구 선택권으로 고를 수 있는 가장 높은 등급 (SPEC4 §2.11 #4)</summary>
        public const ItemGrade FriendTicketMaxGrade = ItemGrade.Legendary;

        /// <summary>ClaimAll 한 번에 받는 최대 단계 수 (콘텐츠가 이상해도 끝나게)</summary>
        const int MaxClaimsPerCall = 1000;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly GachaSystem _gacha;
        readonly MetaRewardService _rewards;
        readonly CharacterSystem _characters;

        bool _connected;
        RedDotSystem _dots;
        static readonly List<MetaReward> NoRewards = new();

        public GachaMilestoneSystem(PlayerData data, MetaDatabase db, GachaSystem gacha, MetaRewardService rewards, CharacterSystem characters)
        {
            _data = data;
            _db = db;
            _gacha = gacha;
            _rewards = rewards;
            _characters = characters;
        }

        GachaMilestoneSave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.gachaMilestones ??= new GachaMilestoneSave();
                _data.meta.gachaMilestones.Sanitize();
                return _data.meta.gachaMilestones;
            }
        }

        IReadOnlyList<GachaMilestoneDef> Steps => _db != null ? _db.GetGachaMilestones() : null;

        /// <summary>누적 소환 수 (첫 만남 확정 소환은 세지 않음)</summary>
        public int Pulls => Save?.pulls ?? 0;

        /// <summary>받은 단계 수</summary>
        public int ClaimedSteps => Save?.claimedSteps ?? 0;

        /// <summary>다음에 받을(또는 닿을) 단계. 콘텐츠가 없거나 되풀이 없이 끝났으면 false</summary>
        public bool TryGetNext(out GachaMilestoneStep step)
        {
            step = null;
            int index = ClaimedSteps;
            if (!TryGetStepAt(index, out int pulls, out var rewards, out bool repeat)) return false;
            step = new GachaMilestoneStep { Index = index, Pulls = pulls, Rewards = rewards, Reached = Pulls >= pulls, Claimed = false, IsRepeat = repeat };
            return true;
        }

        /// <summary>단계 목록 (콘텐츠 단계 전부 + 다음에 받을 단계부터 앞으로 되풀이 repeatsAhead개). into는 비우고 채움</summary>
        public void GetSteps(List<GachaMilestoneStep> into, int repeatsAhead = 2)
        {
            if (into == null) return;
            into.Clear();
            var steps = Steps;
            int contentCount = steps != null ? steps.Count : 0;
            int claimed = ClaimedSteps;
            int pulls = Pulls;
            int last = Math.Max(contentCount, claimed) + Math.Max(0, repeatsAhead);
            for (int index = 0; index < last; index++)
            {
                if (!TryGetStepAt(index, out int need, out var rewards, out bool repeat)) break;
                into.Add(new GachaMilestoneStep { Index = index, Pulls = need, Rewards = rewards, Reached = pulls >= need, Claimed = index < claimed, IsRepeat = repeat });
            }
        }

        /// <summary>다음 단계에 닿아 받을 수 있는지</summary>
        public bool HasClaimable => TryGetStepAt(ClaimedSteps, out int pulls, out _, out _) && Pulls >= pulls;

        /// <summary>다음 단계 하나를 받는다. 닿지 않았으면 null</summary>
        public List<MetaReward> TryClaimNext()
        {
            var save = Save;
            if (save == null || !TryGetStepAt(save.claimedSteps, out int pulls, out var rewards, out _) || save.pulls < pulls) return null;
            save.claimedSteps++;
            var granted = Grant(rewards);
            AfterChanged();
            return granted;
        }

        /// <summary>닿은 단계를 순서대로 모두 받는다 (보상은 합쳐서 한 번에, 받은 것이 없으면 빈 목록)</summary>
        public List<MetaReward> ClaimAll()
        {
            var rewards = new List<MetaReward>();
            var save = Save;
            if (save == null) return rewards;
            int claimed = 0;
            while (claimed < MaxClaimsPerCall && TryGetStepAt(save.claimedSteps, out int pulls, out var stepRewards, out _) && save.pulls >= pulls)
            {
                save.claimedSteps++;
                rewards.AddRange(stepRewards);
                claimed++;
            }
            if (claimed == 0) return rewards;
            var granted = Grant(MetaRewardService.Merge(rewards));
            AfterChanged();
            return granted;
        }

        /// <summary>가진 친구 선택권 수 (meta.wallet.friendSelectTickets)</summary>
        public int FriendTickets => _data?.meta?.wallet != null ? Math.Max(0, _data.meta.wallet.friendSelectTickets) : 0;

        /// <summary>선택권으로 고를 수 있는 계보인지 (전설 이하)</summary>
        public bool CanPickWithTicket(string lineageId)
        {
            var lineage = _characters?.Db != null ? _characters.Db.GetLineage(lineageId) : null;
            return lineage != null && lineage.grade <= FriendTicketMaxGrade;
        }

        /// <summary>
        /// 친구 선택권으로 그 계보를 받는다 (MetaRewards.Grant(Lineage) — 중복이면 기억편). 선택권이 없거나 전설보다 높은 등급이거나 없는 계보면 null
        /// (선택권은 그대로). 받지 못했으면 선택권을 되돌린다
        /// </summary>
        public GrantResult TryUseFriendTicket(string lineageId)
        {
            if (FriendTickets <= 0 || !CanPickWithTicket(lineageId) || _rewards == null || _characters == null) return null;
            if (!_rewards.TrySpendFriendTicket()) return null;
            GrantResult captured = null;
            void Capture(GrantResult result)
            {
                if (result?.Lineage != null && result.Lineage.id == lineageId) captured = result;
            }
            _characters.Granted += Capture;
            try
            {
                _rewards.Grant(new List<MetaReward> { MetaReward.Lineage(lineageId) });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[GachaMilestones] 친구 선택권 지급 실패: {e.Message}");
            }
            finally
            {
                _characters.Granted -= Capture;
            }
            if (captured == null) _rewards.RefundFriendTicket();
            AfterChanged();
            return captured;
        }

        // ───────────────────────────── IMetaModule ─────────────────────────────

        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_gacha != null) _gacha.Pulled += OnPulled;
            if (_rewards != null) _rewards.WalletChanged += OnWalletChanged;
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

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            _dots = dots;
            dots?.Register(RedDotPaths.GachaMilestone, () => HasClaimable || FriendTickets > 0);
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>소환 결과 중 확정 소환이 아닌 것만 센다</summary>
        internal void OnPulled(GachaTable table, IReadOnlyList<GachaResult> results)
        {
            if (results == null) return;
            int counted = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] != null && !results[i].IsGuaranteed) counted++;
            }
            if (counted <= 0) return;
            var save = Save;
            if (save == null) return;
            save.pulls = (int)Math.Min(int.MaxValue, (long)save.pulls + counted);
            AfterChanged();
        }

        void OnWalletChanged()
        {
            _dots?.MarkDirty(RedDotPaths.GachaMilestone);
        }

        /// <summary>
        /// 단계 index의 필요 소환 수 · 보상 (콘텐츠 단계, 그 뒤로 gachaRepeat.interval마다 되풀이). 없으면 false (콘텐츠가 비면 늘 false)
        /// </summary>
        bool TryGetStepAt(int index, out int pulls, out List<MetaReward> rewards, out bool repeat)
        {
            pulls = 0;
            rewards = NoRewards;
            repeat = false;
            var steps = Steps;
            if (index < 0 || steps == null || steps.Count == 0) return false;
            if (index < steps.Count)
            {
                var def = steps[index];
                pulls = def != null ? Math.Max(0, def.pulls) : 0;
                rewards = def?.rewards ?? NoRewards;
                return def != null;
            }
            var repeatDef = _db.gachaRepeat;
            if (repeatDef == null || repeatDef.interval <= 0) return false;
            var lastDef = steps[steps.Count - 1];
            long need = (long)(lastDef != null ? Math.Max(0, lastDef.pulls) : 0) + (long)(index - steps.Count + 1) * repeatDef.interval;
            if (need > int.MaxValue) return false;
            pulls = (int)need;
            rewards = repeatDef.rewards ?? NoRewards;
            repeat = true;
            return true;
        }

        List<MetaReward> Grant(List<MetaReward> rewards)
        {
            if (rewards == null || rewards.Count == 0) return new List<MetaReward>();
            if (_rewards == null) return new List<MetaReward>(rewards);
            return _rewards.Grant(rewards) ?? new List<MetaReward>();
        }

        void AfterChanged()
        {
            _dots?.MarkDirty(RedDotPaths.GachaMilestone);
            Changed?.Invoke();
        }
    }
}
