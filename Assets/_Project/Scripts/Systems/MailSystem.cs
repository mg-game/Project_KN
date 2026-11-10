using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace IdleGame
{
    [Serializable]
    public class MailData
    {
        public string id;
        public string title;
        public string body;
        public List<RewardData> rewards = new();
        public long sentAt;
        /// <summary>만료 시각(Unix 초). 0이면 무기한</summary>
        public long expireAt;

        [JsonIgnore] public bool HasRewards => rewards != null && rewards.Count > 0;
    }

    /// <summary>
    /// 우편함. 현재는 로컬 저장 방식이다.
    /// 서버를 붙일 때는 서버에서 받은 우편을 Send로 넣고, Claim에서 서버 검증 후 지급하도록 바꾼다.
    /// </summary>
    public class MailSystem
    {
        public event Action Changed;
        /// <summary>(받은 우편 수) 보상 있는 우편을 받았을 때 (Changed 다음에). 진행 기록기(MailClaim)용</summary>
        public event Action<int> Claimed;

        readonly PlayerData _data;
        readonly RewardService _rewards;
        readonly ITimeProvider _clock;
        readonly int _defaultExpireDays;

        public MailSystem(PlayerData data, RewardService rewards, ITimeProvider clock, int defaultExpireDays)
        {
            _data = data;
            _rewards = rewards;
            _clock = clock;
            _defaultExpireDays = defaultExpireDays;
        }

        /// <summary>최신순 우편 목록</summary>
        public IReadOnlyList<MailData> Mails => _data.mails;
        public int Count => _data.mails.Count;
        public bool HasClaimableRewards => _data.mails.Exists(m => m.HasRewards);

        long Now => TimeUtil.ToUnix(_clock.UtcNow);

        /// <param name="expireDays">보관 일수. 음수면 기본값, 0이면 무기한</param>
        public MailData Send(string title, string body, IEnumerable<RewardData> rewards = null, int expireDays = -1)
        {
            long now = Now;
            int days = expireDays < 0 ? _defaultExpireDays : expireDays;
            var mail = new MailData
            {
                id = Guid.NewGuid().ToString("N"),
                title = title,
                body = body,
                rewards = rewards?.ToList() ?? new List<RewardData>(),
                sentAt = now,
                expireAt = days > 0 ? now + days * 86400L : 0,
            };
            _data.mails.Insert(0, mail);
            Changed?.Invoke();
            return mail;
        }

        /// <summary>key 당 한 번만 발송한다. (환영 우편, 레벨 보상 등)</summary>
        public bool SendOnce(string key, string title, string body, IEnumerable<RewardData> rewards = null, int expireDays = -1)
        {
            if (!_data.sentOnceMailKeys.Add(key)) return false;
            Send(title, body, rewards, expireDays);
            return true;
        }

        /// <summary>
        /// 우편 하나를 수령하고 지급한 보상을 반환한다. 보상 없는 우편은 삭제만 된다.
        /// 없거나 만료된 우편이면 null.
        /// </summary>
        public List<RewardData> Claim(string mailId)
        {
            int index = _data.mails.FindIndex(m => m.id == mailId);
            if (index < 0) return null;

            var mail = _data.mails[index];
            _data.mails.RemoveAt(index);
            if (IsExpired(mail, Now))
            {
                Changed?.Invoke();
                return null;
            }

            _rewards.Grant(mail.rewards);
            Changed?.Invoke();
            if (mail.HasRewards) Claimed?.Invoke(1);
            return mail.rewards;
        }

        /// <summary>보상이 있는 우편을 모두 수령하고 합산된 보상 목록을 반환한다.</summary>
        public List<RewardData> ClaimAll()
        {
            RemoveExpired();
            var claimed = _data.mails.Where(m => m.HasRewards).ToList();
            if (claimed.Count == 0) return new List<RewardData>();

            _data.mails.RemoveAll(m => m.HasRewards);
            var rewards = claimed.SelectMany(m => m.rewards).ToList();
            _rewards.Grant(rewards);
            Changed?.Invoke();
            Claimed?.Invoke(claimed.Count);
            return RewardService.Merge(rewards);
        }

        public int RemoveExpired()
        {
            long now = Now;
            int removed = _data.mails.RemoveAll(m => IsExpired(m, now));
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        public TimeSpan GetRemainTime(MailData mail) =>
            mail.expireAt <= 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(Math.Max(0, mail.expireAt - Now));

        static bool IsExpired(MailData mail, long now) => mail.expireAt > 0 && mail.expireAt <= now;
    }
}
