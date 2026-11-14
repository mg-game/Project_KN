using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 원정(파견). 보유한 계보를 구역에 보내 시간이 지나면 재료/단추를 받는다.
    /// 완료 시 참여 계보의 "원정 N회" 기록과 호감도가 오른다.
    /// 원정과 편성은 따로다: 편성에 있는 계보도 보낼 수 있고, 원정 중에도 편성에서 빠지지 않고 함께 싸운다 (M05).
    /// 제한은 시간과 자리뿐이다 — 한 계보는 한 번에 하나의 원정에만, 구역마다 한 번에 하나의 원정만,
    /// 동시에 진행할 수 있는 원정은 MaxConcurrent개다. 완료된 원정도 보상을 받기 전까지는 진행 중으로 센다.
    /// 구역마다 마지막으로 보낸 친구와 다녀올 시간 단계를 기억해(저장됨) "다시 보내기"와 준비 창 미리 채우기에 쓴다 (M57).
    /// <para>
    /// 다녀올 시간 사다리 (IDLE-04, SPEC4 §5.3): 0 '기본' (구역 시간, 보상 ×1, 호감도 2) · 1 '길게' (×3, 보상 ×2.8, 호감도 4) ·
    /// 2 '밤새' (×6, 보상 ×5.4, 호감도 6). 단계 시간이 12시간(720분)을 넘으면 그 단계는 없고, 1 · 2단계는 튜토리얼을 마친 뒤에만.
    /// 보상 배율: 단추 × 배율, 재료는 줄마다 floor(배율)번 굴리고 frac(배율) 확률로 한 번 더 굴린다 (인원 확률 보너스는 그대로).
    /// 진화 "원정 N회" 기록은 단계와 무관하게 +1 (짧게 여러 번이 진화에는 빠르다 — 의도). 받을 때 재료 개수에 DropMultiplier
    /// (이벤트 '주말 원정 축제' ×2)를 곱한다. 0단계의 난수 사용 순서는 사다리 전과 같다 (재료 줄마다 한 번).
    /// </para>
    /// <para>
    /// 모두 다시 보내기 (ResendAll, ExpeditionBatch 2-1 — 버튼과 숨김은 화면 P4-4): 다시 보낼 수 있는 구역을 구역 순서대로
    /// 마지막 친구 · 마지막 단계(지금 고를 수 없으면 0단계)로 MaxConcurrent까지 다시 보낸다.
    /// 시스템도 잠금을 본다 (F-03, SPEC4 §5.1): FeatureResolver(ExpeditionBatch)가 false면 ResendAll · CountResendable = 0
    /// (화면 길이 새어도 거절). 구역 하나 다시 보내기(TryResend)는 잠금과 무관하다.
    /// </para>
    /// <para>
    /// 시간 되돌림 방지 (H-1): Game.Setup은 DailyClock을 넣는다. 그러면 원정 시각은 기기 시각이 아니라 유효 시각
    /// (DailyClock.EffectiveUnix = max(기기 시각, 본 적 있는 가장 늦은 시각))이고, 시계가 시작 시각보다 뒤로 가면
    /// 시작/종료를 당기지 않고 시작 시각에 멈춘다. 되돌림(IsTampered) 중에는 받기를 막는다 (다른 메타 보상과 같은 규칙).
    /// 당기면 "앞으로 → 받기 → 다시 보내기 → 뒤로 → 앞으로"가 실제 시간 없이 보상을 무한히 만든다.
    /// DailyClock이 없으면(옛 연결 · 시스템 단위 테스트) 기기 시각을 그대로 쓰고, 되돌림 때 시작/종료를 당긴다.
    /// </para>
    /// </summary>
    public class ExpeditionSystem
    {
        public event Action Changed;

        /// <summary>동시에 진행 가능한 원정 수</summary>
        public const int MaxConcurrentExpeditions = 3;
        /// <summary>원정 완료 시 참여 계보마다 오르는 호감도 (0단계 — 단계별 값은 GetAffinityPerMember)</summary>
        public const int CompleteAffinity = 2;
        /// <summary>참여 인원이 1명 늘 때마다 단추 보상 증가 비율</summary>
        public const double ExtraMemberGoldBonus = 0.25;
        /// <summary>참여 인원이 1명 늘 때마다 재료 획득 확률 증가 비율 (확률 × (1 + 값))</summary>
        public const float ExtraMemberDropChanceBonus = 0.15f;

        readonly PlayerData _data;
        readonly ExpeditionDatabase _db;
        readonly CharacterSystem _characters;
        readonly FormationSystem _formation;
        readonly RewardService _rewards;
        readonly ITimeProvider _clock;
        readonly System.Random _random;
        readonly DailyClock _dailyClock;

        /// <param name="dailyClock">시간 되돌림 방지 시계 (Game.Setup이 넣는다). null이면 기기 시각을 그대로 쓴다</param>
        public ExpeditionSystem(PlayerData data, ExpeditionDatabase db, CharacterSystem characters, FormationSystem formation,
            RewardService rewards, ITimeProvider clock, System.Random random, DailyClock dailyClock = null)
        {
            _data = data;
            _db = db;
            _characters = characters;
            _formation = formation;
            _rewards = rewards;
            _clock = clock;
            _random = random;
            _dailyClock = dailyClock;
        }

        /// <summary>시계 되돌림 중 (DailyClock.IsTampered). 이 동안 Claim · ClaimAll은 아무것도 주지 않는다</summary>
        public bool IsClockTampered => _dailyClock != null && _dailyClock.IsTampered;

        public ExpeditionDatabase Db => _db;
        /// <summary>동시에 진행 가능한 원정 수</summary>
        public int MaxConcurrent => MaxConcurrentExpeditions;

        // ───────────── 다녀올 시간 사다리 · 모두 다시 보내기 (IDLE-04, SPEC4 §2.5 · §5.3) ─────────────

        /// <summary>다녀올 시간 단계 수 (0 기본 · 1 길게 · 2 밤새)</summary>
        public const int DurationTierCount = 3;
        /// <summary>단계가 허용되는 가장 긴 원정 (분, 12시간)</summary>
        public const int MaxTierDurationMinutes = 720;

        static readonly int[] TierDurationFactor = { 1, 3, 6 };
        static readonly double[] TierRewardScale = { 1.0, 2.8, 5.4 };
        static readonly int[] TierAffinity = { CompleteAffinity, 4, 6 };

        /// <summary>원정 재료 배율 (이벤트 '주말 원정 축제' ×2, 받을 때 재료 개수에 곱한다 — EventCalendarSystem이 넣는다). null 또는 1 이하 = 1</summary>
        public Func<double> DropMultiplier;

        /// <summary>기능 개방 확인 (ExpeditionBatch 잠김이면 모두 다시 보내기를 거절, MetaSession.Connect가 넣는다). null = 열림</summary>
        public Func<FeatureId, bool> FeatureResolver;

        /// <summary>모두 다시 보내기(ExpeditionBatch, 2-1)가 열렸는지 (FeatureResolver가 없으면 열림)</summary>
        public bool IsBatchOpen
        {
            get
            {
                var resolver = FeatureResolver;
                return resolver == null || resolver(FeatureId.ExpeditionBatch);
            }
        }

        /// <summary>1 · 2단계를 고를 수 있는지 (튜토리얼을 마침 — 튜토리얼의 첫 원정은 0단계로 짧게)</summary>
        public bool IsLadderOpen => _data.meta?.ftue != null && _data.meta.ftue.done;

        /// <summary>
        /// 단계를 골라 원정을 보낸다 (IDLE-04). 고를 수 없는 단계면 false. 그 밖의 조건은 TryStart(zone, ids)와 같고,
        /// 성공하면 구역의 마지막 친구 · 마지막 단계로 기억한다
        /// </summary>
        public bool TryStart(string zoneId, IReadOnlyList<string> lineageIds, int tier)
        {
            var zone = _db != null ? _db.Get(zoneId) : null;
            if (zone == null || !IsTierAllowed(zone, tier) || !IsZoneUnlocked(zoneId) || GetActive(zoneId) != null) return false;
            if (Expeditions.Count >= MaxConcurrent) return false;
            if (lineageIds == null || lineageIds.Count == 0 || lineageIds.Count > Math.Max(1, zone.maxMembers)) return false;

            var members = new List<string>(lineageIds.Count);
            foreach (var lineageId in lineageIds)
            {
                if (members.Contains(lineageId) || !CanSelect(lineageId, out _)) return false;
                members.Add(lineageId);
            }

            long now = NowUnix;
            Expeditions.Add(new ExpeditionSave
            {
                zoneId = zone.id,
                lineageIds = members,
                startUnix = now,
                endUnix = now + GetDurationMinutes(zone, tier) * 60L,
                durationTier = tier,
            });
            RememberMembers(zone.id, members, tier);
            Changed?.Invoke();
            return true;
        }

        /// <summary>그 단계의 원정 시간 (분) = 구역 시간 × 1 / 3 / 6</summary>
        public int GetDurationMinutes(ExpeditionZoneDef zone, int tier)
        {
            if (zone == null) return 0;
            return Math.Max(0, zone.durationMinutes) * TierDurationFactor[ClampTier(tier)];
        }

        /// <summary>단계 보상 배율 (×1 / ×2.8 / ×5.4)</summary>
        public static double RewardScale(int tier) => TierRewardScale[ClampTier(tier)];

        /// <summary>단계별 원정 완료 호감도 (참여 계보마다 2 / 4 / 6)</summary>
        public static int GetAffinityPerMember(int tier) => TierAffinity[ClampTier(tier)];

        /// <summary>
        /// 그 구역에서 그 단계를 고를 수 있는지: 0단계는 늘, 1 · 2단계는 튜토리얼을 마쳤고 단계 시간 ≤ 12시간일 때
        /// (exp_2_4 180분은 0 · 1단계만). 범위 밖 단계는 false
        /// </summary>
        public bool IsTierAllowed(ExpeditionZoneDef zone, int tier)
        {
            if (zone == null || tier < 0 || tier >= DurationTierCount) return false;
            if (tier == 0) return true;
            return IsLadderOpen && GetDurationMinutes(zone, tier) <= MaxTierDurationMinutes;
        }

        /// <summary>구역에 마지막으로 보낸 단계 (준비 창 기본값, 기억이 없으면 0)</summary>
        public int GetLastTier(string zoneId)
        {
            var entry = FindLastMembers(zoneId);
            return entry != null ? ClampTier(entry.lastTier) : 0;
        }

        /// <summary>
        /// 다시 보낼 수 있는 구역을 모두 마지막 친구 · 단계로 다시 보낸다 (구역 순서, 동시 원정 상한까지, 기억한 친구 중 하나라도 못 보내는 구역은 건너뜀).
        /// 보낸 구역 수 (ExpeditionBatch가 잠겼으면 0 — 아무것도 보내지 않는다)
        /// </summary>
        public int ResendAll()
        {
            var zones = _db != null ? _db.zones : null;
            if (zones == null || !IsBatchOpen) return 0;
            int sent = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || string.IsNullOrEmpty(zone.id)) continue;
                if (Expeditions.Count >= MaxConcurrent) break;
                if (TryResend(zone.id)) sent++;
            }
            return sent;
        }

        /// <summary>
        /// 지금 ResendAll이 보낼 구역 수 (모두 다시 보내기 버튼 표시). 같은 친구를 두 구역에 기억하고 있으면 앞 구역만 센다. 처음 몇 번 뒤로는 할당 없음.
        /// ExpeditionBatch가 잠겼으면 0
        /// </summary>
        public int CountResendable()
        {
            var zones = _db != null ? _db.zones : null;
            if (zones == null || !IsBatchOpen) return 0;
            int free = MaxConcurrent - Expeditions.Count;
            if (free <= 0) return 0;
            _resendScratch.Clear();
            int count = 0;
            for (int i = 0; i < zones.Count && count < free; i++)
            {
                var zone = zones[i];
                if (zone == null || !CanResend(zone.id)) continue;
                var last = GetLastMembers(zone.id);
                bool clash = false;
                for (int m = 0; m < last.Count && !clash; m++) clash = _resendScratch.Contains(last[m]);
                if (clash) continue;
                for (int m = 0; m < last.Count; m++) _resendScratch.Add(last[m]);
                count++;
            }
            _resendScratch.Clear();
            return count;
        }

        readonly List<string> _resendScratch = new();

        static int ClampTier(int tier) => tier < 0 ? 0 : tier >= DurationTierCount ? DurationTierCount - 1 : tier;

        /// <summary>다시 보낼 단계: 마지막 단계를 지금 고를 수 있으면 그것, 아니면 0단계</summary>
        int ResendTier(ExpeditionZoneDef zone)
        {
            int tier = GetLastTier(zone?.id);
            return IsTierAllowed(zone, tier) ? tier : 0;
        }

        /// <summary>원정 기준 시각 (유닉스 초). DailyClock이 있으면 유효 시각이라 시계를 되돌려도 뒤로 가지 않는다</summary>
        long NowUnix => _dailyClock != null ? _dailyClock.EffectiveUnix : TimeUtil.ToUnix(_clock.UtcNow);
        int HighestStage => Math.Max(1, _data.battle?.highestStage ?? 1);
        List<ExpeditionSave> Expeditions => _data.expeditions ??= new List<ExpeditionSave>();
        List<ExpeditionMembersSave> LastMembers => _data.expeditionLastMembers ??= new List<ExpeditionMembersSave>();

        static readonly string[] NoMembers = Array.Empty<string>();

        public bool IsZoneUnlocked(string zoneId)
        {
            var zone = _db != null ? _db.Get(zoneId) : null;
            return zone != null && HighestStage >= zone.requiredStage;
        }

        public IReadOnlyList<ExpeditionSave> Active => Expeditions;

        public ExpeditionSave GetActive(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            foreach (var expedition in Expeditions)
            {
                if (expedition != null && expedition.zoneId == zoneId) return expedition;
            }
            return null;
        }

        /// <summary>
        /// 원정에 보낼 수 있는지 (보유, 다른 원정에 나가 있지 않음). 불가 사유 문구 반환.
        /// 편성에 있는지는 보지 않는다 — 원정 중에도 편성에서 함께 싸운다.
        /// </summary>
        public bool CanSelect(string lineageId, out string reason)
        {
            if (string.IsNullOrEmpty(lineageId) || !_characters.IsOwned(lineageId))
            {
                reason = "아직 만나지 못한 친구예요.";
                return false;
            }
            if (_formation.IsDispatched(lineageId))
            {
                reason = "원정 파견 중이에요.";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// 이 구역에 마지막으로 보낸 친구 (보낸 순서). 없으면 빈 목록. 지금 보낼 수 있는지는 CanSelect / CanResend로 따로 확인한다.
        /// 돌려준 목록은 세이브 데이터 그대로이므로 고치지 말 것 (할당 없음).
        /// </summary>
        public IReadOnlyList<string> GetLastMembers(string zoneId)
        {
            var entry = FindLastMembers(zoneId);
            return entry != null && entry.lineageIds != null ? entry.lineageIds : NoMembers;
        }

        /// <summary>
        /// 마지막 친구 그대로 바로 다시 보낼 수 있는지 (구역 카드 "다시 보내기"). 구역이 열려 있고 비어 있으며 자리가 있고,
        /// 기억한 친구가 1~maxMembers명이며 모두 지금 보낼 수 있어야 한다. 한 명이라도 안 되면 false (일부만 몰래 보내지 않는다).
        /// </summary>
        public bool CanResend(string zoneId)
        {
            var zone = _db != null ? _db.Get(zoneId) : null;
            if (zone == null || !IsZoneUnlocked(zoneId) || GetActive(zoneId) != null) return false;
            if (Expeditions.Count >= MaxConcurrent) return false;

            var last = GetLastMembers(zoneId);
            if (last.Count == 0 || last.Count > Math.Max(1, zone.maxMembers)) return false;
            for (int i = 0; i < last.Count; i++)
            {
                if (!CanSelect(last[i], out _)) return false;
                for (int j = 0; j < i; j++)
                {
                    if (last[j] == last[i]) return false;
                }
            }
            return true;
        }

        /// <summary>마지막 친구 · 마지막 단계 그대로 다시 보낸다 (CanResend가 아니면 아무것도 하지 않고 false). 마지막 단계를 지금 고를 수 없으면 0단계</summary>
        public bool TryResend(string zoneId)
        {
            if (!CanResend(zoneId)) return false;
            var zone = _db.Get(zoneId);
            return TryStart(zoneId, GetLastMembers(zoneId), ResendTier(zone));
        }

        ExpeditionMembersSave FindLastMembers(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            var list = LastMembers;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].zoneId == zoneId) return list[i];
            }
            return null;
        }

        /// <summary>구역의 마지막 친구 · 단계를 바꾼다 (보낸 목록은 복사해 둔다 — 진행 중 원정 기록과 따로)</summary>
        void RememberMembers(string zoneId, List<string> members, int tier)
        {
            var entry = FindLastMembers(zoneId);
            if (entry == null)
            {
                entry = new ExpeditionMembersSave { zoneId = zoneId };
                LastMembers.Add(entry);
            }
            entry.lineageIds = new List<string>(members);
            entry.lastTier = ClampTier(tier);
        }

        /// <summary>
        /// 원정 시작 (0단계 '기본'). 구역이 열려 있고 비어 있어야 하며, 1~maxMembers명의 서로 다른 보낼 수 있는 계보가 필요하다.
        /// 성공하면 그 구역의 마지막 친구(GetLastMembers)로 기억한다.
        /// </summary>
        public bool TryStart(string zoneId, IReadOnlyList<string> lineageIds) => TryStart(zoneId, lineageIds, 0);

        /// <summary>
        /// 현재 시각(유닉스 초). 시각이 시작 시각보다 뒤로 갔을 때:
        ///   · DailyClock이 있으면 시작 시각을 돌려준다. 진행이 멈추고, 시작/종료는 그대로다 (H-1). 저장된 maxSeen이
        ///     아직 오르기 전(Refresh 전)에 시계를 되돌려도 당겨지지 않는다. 표시(남은 시간 = 소요 시간)와 완료 판정은 같은 값을 본다.
        ///   · 없으면 시작/종료 시각을 지금 기준으로 당겨 남은 시간이 원래 소요 시간보다 길어지지 않게 한다. 그러지 않으면 표시는
        ///     소요 시간에서 멈춰 있는데 실제 완료는 시간이 뒤로 간 만큼 더 늦어진다. 바뀐 값은 다음 저장에 함께 저장된다.
        /// </summary>
        long Now(ExpeditionSave expedition)
        {
            long now = NowUnix;
            if (now < expedition.startUnix)
            {
                if (_dailyClock != null) return expedition.startUnix;
                long shift = expedition.startUnix - now;
                expedition.startUnix = now;
                expedition.endUnix -= shift;
            }
            return now;
        }

        public TimeSpan GetRemaining(ExpeditionSave expedition)
        {
            if (expedition == null) return TimeSpan.Zero;
            long remain = expedition.endUnix - Now(expedition);
            long duration = Math.Max(0, expedition.endUnix - expedition.startUnix);
            return TimeSpan.FromSeconds(Math.Clamp(remain, 0, duration));
        }

        public bool IsComplete(ExpeditionSave expedition) => expedition != null && Now(expedition) >= expedition.endUnix;

        /// <summary>진행률 0~1 (게이지 표시용)</summary>
        public float GetProgress(ExpeditionSave expedition)
        {
            if (expedition == null) return 0f;
            long now = Now(expedition);
            long duration = expedition.endUnix - expedition.startUnix;
            if (duration <= 0) return 1f;
            return (float)Math.Clamp((double)(now - expedition.startUnix) / duration, 0, 1);
        }

        /// <summary>완료되어 받을 수 있는 원정 수 (빨간 점)</summary>
        public int CompletedCount
        {
            get
            {
                int count = 0;
                foreach (var expedition in Expeditions) if (IsComplete(expedition)) count++;
                return count;
            }
        }

        /// <summary>완료된 원정 보상 지급 (단계 배율 · 재료 배율 반영). 완료 전이거나 시계 되돌림 중이면 null</summary>
        public ExpeditionResult Claim(string zoneId)
        {
            if (IsClockTampered) return null;
            var expedition = GetActive(zoneId);
            if (expedition == null || !IsComplete(expedition)) return null;

            var zone = _db != null ? _db.Get(zoneId) : null;
            int tier = ClampTier(expedition.durationTier);
            var result = new ExpeditionResult { Zone = zone };
            foreach (var lineageId in expedition.lineageIds)
            {
                if (!string.IsNullOrEmpty(lineageId) && !result.LineageIds.Contains(lineageId)) result.LineageIds.Add(lineageId);
            }
            if (zone != null) RollRewards(zone, result.LineageIds.Count, tier, result.Rewards);

            Expeditions.Remove(expedition);
            _rewards.Grant(result.Rewards);
            int affinity = GetAffinityPerMember(tier);
            foreach (var lineageId in result.LineageIds)
            {
                if (!_characters.IsOwned(lineageId)) continue;
                _characters.AddProgress(lineageId, ProgressType.ExpeditionCount, 1);
                _characters.AddAffinity(lineageId, affinity);
            }
            Changed?.Invoke();
            return result;
        }

        /// <summary>완료된 원정을 모두 받는다. 시계 되돌림 중이면 빈 목록</summary>
        public List<ExpeditionResult> ClaimAll()
        {
            if (IsClockTampered) return new List<ExpeditionResult>();
            var completed = new List<string>();
            foreach (var expedition in Expeditions)
            {
                if (IsComplete(expedition)) completed.Add(expedition.zoneId);
            }
            var results = new List<ExpeditionResult>();
            foreach (var zoneId in completed)
            {
                var result = Claim(zoneId);
                if (result != null) results.Add(result);
            }
            return results;
        }

        /// <summary>원정 취소 (보상 없음, 계보 복귀)</summary>
        public bool TryCancel(string zoneId)
        {
            var expedition = GetActive(zoneId);
            if (expedition == null) return false;
            Expeditions.Remove(expedition);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 구역 보상 굴림. 인원이 많을수록 단추와 재료 확률이 오른다. 단계 배율: 단추 × 배율, 재료 줄마다 floor(배율)번 + frac(배율) 확률로 한 번 더.
        /// 같은 재료는 한 줄로 합치고, 개수에 DropMultiplier를 곱한다 (내림)
        /// </summary>
        void RollRewards(ExpeditionZoneDef zone, int members, int tier, List<RewardData> rewards)
        {
            int extra = Math.Max(0, members - 1);
            double scale = RewardScale(tier);
            // 기준 양 B = 구역 단추 × 인원 보너스 × 단계 배율, 단추 · 실타래는 B를 나눈 몫 (안 D, IncomeSplit)
            double income = zone.goldReward * (1 + ExtraMemberGoldBonus * extra) * scale;
            IncomeSplit.AddCurrencyLines(rewards, Math.Floor(income + 1e-9));
            if (zone.drops == null) return;

            int wholeRolls = (int)Math.Floor(scale + 1e-9);
            double fraction = scale - wholeRolls;
            double dropMultiplier = DropMultiplier != null ? DropMultiplier() : 1.0;
            if (!(dropMultiplier > 1.0) || double.IsInfinity(dropMultiplier)) dropMultiplier = 1.0;

            foreach (var drop in zone.drops)
            {
                if (drop == null || string.IsNullOrEmpty(drop.itemId)) continue;
                float chance = Math.Min(1f, drop.chance * (1f + ExtraMemberDropChanceBonus * extra));
                int rolls = wholeRolls;
                if (fraction > 1e-9 && _random.NextDouble() < fraction) rolls++;
                int total = 0;
                for (int r = 0; r < rolls; r++)
                {
                    if (_random.NextDouble() >= chance) continue;
                    int min = Math.Max(0, Math.Min(drop.min, drop.max));
                    int max = Math.Max(drop.min, drop.max);
                    total += _random.Next(min, max + 1);
                }
                if (total <= 0) continue;
                int count = (int)Math.Min(int.MaxValue, Math.Floor(total * dropMultiplier + 1e-9));
                AddItem(rewards, drop.itemId, count);
            }
        }

        static void AddItem(List<RewardData> rewards, string itemId, int count)
        {
            if (count <= 0) return;
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward != null && reward.type == RewardType.Item && reward.itemId == itemId)
                {
                    reward.amount += count; // 이 굴림에서 만든 항목이라 고쳐도 된다
                    return;
                }
            }
            rewards.Add(RewardData.Item(itemId, count));
        }
    }
}
