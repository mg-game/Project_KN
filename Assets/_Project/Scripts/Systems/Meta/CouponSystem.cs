using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace IdleGame
{
    /// <summary>쿠폰 입력 결과 (EVT-02)</summary>
    public enum CouponResult
    {
        /// <summary>받음 — 보상은 우편으로</summary>
        Ok = 0,
        /// <summary>없는 번호 (5번 틀리면 잠시 잠금)</summary>
        Invalid = 1,
        AlreadyUsed = 2,
        OutOfPeriod = 3,
        /// <summary>너무 많이 틀려 잠시 막힘</summary>
        Locked = 4,
        /// <summary>기기 시간이 되돌려짐 (DailyClock.TamperedMessage)</summary>
        Tampered = 5,
    }

    /// <summary>
    /// 오프라인 쿠폰 (EVT-02, SPEC4 §4.5 — 확장 항목, 모듈이 아님): 번호를 다듬어(앞뒤 공백 · 가운데 공백 · '-' 빼고 대문자, ^[A-Z0-9]{6,16}$)
    /// 소문자 16진수 SHA256(UTF8(couponSalt + ":" + 번호)) 해시로 찾고, 맞으면 우편(Mail.Send, 30일)으로 보상을 보낸다. 번호는 저장하지 않는다 (해시만).
    /// <para>
    /// 순서: 잠김(lockUntilUnix) → Locked, 되돌림 → Tampered, 형식이 틀렸거나 없는 번호 → Invalid (failStreak++ · 5번이면 60초 잠금 후 0),
    /// 이미 씀 → AlreadyUsed, 사용 기간(날 키, 비면 제한 없음) 밖 → OutOfPeriod, 맞으면 Ok (redeemed에 해시, failStreak = 0).
    /// 보상은 재화 · 재료(RewardData)만 — 우편은 메타 보관함 보상을 못 싣는다. 쿠폰 목록이 비어 있으면 모든 번호가 Invalid (콘텐츠 끄개).
    /// </para>
    /// </summary>
    public class CouponSystem
    {
        /// <summary>이만큼 연속으로 틀리면 잠근다</summary>
        public const int MaxFails = 5;
        /// <summary>잠금 시간 (초)</summary>
        public const int LockSeconds = 60;
        /// <summary>쿠폰 우편 보관 일수</summary>
        public const int MailExpireDays = 30;
        public const int MinLength = 6;
        public const int MaxLength = 16;
        /// <summary>쿠폰에 제목이 없을 때의 우편 제목</summary>
        public const string DefaultMailTitle = "쿠폰 선물이 도착했어요";

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly MailSystem _mail;

        public CouponSystem(PlayerData data, MetaDatabase db, DailyClock clock, MailSystem mail)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _mail = mail;
        }

        CouponSave Save
        {
            get
            {
                var save = (_data.meta ??= new MetaSave()).coupons ??= new CouponSave();
                save.redeemed ??= new HashSet<string>();
                return save;
            }
        }

        long Now => _clock != null ? _clock.EffectiveUnix : 0;

        /// <summary>잠금이 풀리기까지 남은 시간 (잠기지 않았으면 0)</summary>
        public TimeSpan LockRemaining => TimeSpan.FromSeconds(Math.Max(0, Save.lockUntilUnix - Now));

        /// <summary>쿠폰 번호를 입력한다 (위 순서). 예외 없음</summary>
        public CouponResult TryRedeem(string input)
        {
            var save = Save;
            long now = Now;
            if (save.lockUntilUnix > now) return CouponResult.Locked;
            if (_clock != null && _clock.IsTampered) return CouponResult.Tampered;

            string code = Normalize(input);
            var def = code != null && _db != null ? _db.GetCoupon(Hash(_db.couponSalt, code)) : null;
            if (def == null)
            {
                save.failStreak++;
                if (save.failStreak >= MaxFails)
                {
                    save.failStreak = 0;
                    save.lockUntilUnix = now + LockSeconds;
                }
                return CouponResult.Invalid;
            }

            string hash = def.hash.ToLowerInvariant();
            if (save.redeemed.Contains(hash)) return CouponResult.AlreadyUsed;
            if (!InPeriod(def)) return CouponResult.OutOfPeriod;

            save.redeemed.Add(hash);
            save.failStreak = 0;
            _mail?.Send(string.IsNullOrEmpty(def.mailTitle) ? DefaultMailTitle : def.mailTitle, def.mailBody ?? string.Empty,
                def.rewards ?? new List<RewardData>(), MailExpireDays);
            return CouponResult.Ok;
        }

        /// <summary>
        /// 번호 다듬기: 앞뒤 공백을 자르고, 가운데 공백 · '-'를 빼고, 대문자로. 영문 대문자 · 숫자 6~16자가 아니면 null
        /// </summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            var builder = new StringBuilder(input.Length);
            string trimmed = input.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == '-' || char.IsWhiteSpace(c)) continue;
                if (c >= 'a' && c <= 'z') c = (char)(c - 'a' + 'A');
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return null;
                builder.Append(c);
            }
            return builder.Length >= MinLength && builder.Length <= MaxLength ? builder.ToString() : null;
        }

        /// <summary>소문자 16진수 SHA256(UTF8(salt + ":" + code)) — 콘텐츠의 쿠폰 해시와 같은 식 (편집기 '쿠폰 해시 만들기'도 이것을 쓴다)</summary>
        public static string Hash(string salt, string code)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((salt ?? string.Empty) + ":" + (code ?? string.Empty)));
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        /// <summary>사용 기간 안인지 (날 키 "yyyy-MM-dd" 문자열 비교, 비면 그쪽 제한 없음)</summary>
        bool InPeriod(CouponDef def)
        {
            string today = _clock?.DayKey;
            if (string.IsNullOrEmpty(today)) return string.IsNullOrEmpty(def.validFromDayKey) && string.IsNullOrEmpty(def.validToDayKey);
            if (!string.IsNullOrEmpty(def.validFromDayKey) && string.CompareOrdinal(today, def.validFromDayKey) < 0) return false;
            if (!string.IsNullOrEmpty(def.validToDayKey) && string.CompareOrdinal(today, def.validToDayKey) > 0) return false;
            return true;
        }
    }
}
