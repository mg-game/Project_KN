using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleGame
{
    [Serializable]
    public class NoticeData
    {
        public string id;
        public string title;
        public string body;
        /// <summary>표시용 날짜 (yyyy-MM-dd)</summary>
        public string date;
        /// <summary>접속 시 팝업으로 띄울지</summary>
        public bool popup;
        /// <summary>정렬 우선순위 (높을수록 위)</summary>
        public int order;
    }

    /// <summary>
    /// 공지 목록 (정렬: order 높은 순 → 날짜 최신 순), 읽음 기록, "오늘 하루 보지 않기".
    /// 접속 때 띄우는 공지는 한 번에 하나 (M50 — GetStartupNoticeToShow): 읽지 않은 첫 팝업 공지. 나머지는 공지 목록·점으로만 알린다.
    /// </summary>
    public class NoticeSystem
    {
        public event Action Changed;

        readonly PlayerData _data;
        readonly ITimeProvider _clock;
        readonly List<NoticeData> _notices = new();

        public NoticeSystem(PlayerData data, ITimeProvider clock)
        {
            _data = data;
            _clock = clock;
        }

        public IReadOnlyList<NoticeData> Notices => _notices;
        public bool IsLoaded { get; private set; }
        /// <summary>안 읽은 공지 수 (할당 없이 훑기)</summary>
        public int UnreadCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _notices.Count; i++)
                {
                    if (!IsRead(_notices[i])) count++;
                }
                return count;
            }
        }
        public bool IsHiddenToday => _data.noticeHiddenDate == Today;

        string Today => _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd");

        public void SetNotices(IEnumerable<NoticeData> notices)
        {
            _notices.Clear();
            if (notices != null) _notices.AddRange(notices.Where(n => n != null && !string.IsNullOrEmpty(n.id)));
            _notices.Sort((a, b) =>
            {
                int byOrder = b.order.CompareTo(a.order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(b.date, a.date);
            });
            IsLoaded = true;
            Changed?.Invoke();
        }

        public bool IsRead(NoticeData notice) => _data.readNoticeIds.Contains(notice.id);

        public void MarkRead(NoticeData notice)
        {
            if (_data.readNoticeIds.Add(notice.id)) Changed?.Invoke();
        }

        /// <summary>접속 시 띄울 첫 공지. 오늘 하루 숨김 상태거나 팝업 공지가 없으면 null</summary>
        public NoticeData GetStartupNotice() => IsHiddenToday ? null : _notices.FirstOrDefault(n => n.popup);

        /// <summary>
        /// 이번 접속에 띄울 공지 하나 (M50): 오늘 하루 숨김이면 null, 아니면 정렬 순서에서 처음 나오는 "읽지 않은" 팝업 공지.
        /// 읽은 공지로 되돌아가지 않는다 — 다 읽었으면 null (같은 날 다시 켜도 공지가 또 뜨지 않는다).
        /// 시작 모달은 복귀 → 출석부 → 공지 1개까지라 발표자가 이것이 null이면 공지 자리를 비운다.
        /// </summary>
        public NoticeData GetStartupNoticeToShow()
        {
            if (IsHiddenToday) return null;
            for (int i = 0; i < _notices.Count; i++)
            {
                var notice = _notices[i];
                if (notice.popup && !IsRead(notice)) return notice;
            }
            return null;
        }

        /// <summary>접속 시 차례로 띄울 팝업 공지 전부 (정렬 순서). 오늘 하루 숨김 상태면 비어 있다 (목록·테스트용 — 접속 팝업은 GetStartupNoticeToShow)</summary>
        public List<NoticeData> GetStartupNotices()
        {
            var result = new List<NoticeData>();
            if (IsHiddenToday) return result;
            foreach (var notice in _notices)
            {
                if (notice.popup) result.Add(notice);
            }
            return result;
        }

        public void HideForToday() => _data.noticeHiddenDate = Today;
    }
}
