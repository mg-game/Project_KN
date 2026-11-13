using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>메타 팝업 요청 하나 (PopupQueue). 값 타입이라 큐에 넣고 빼도 할당이 없다</summary>
    public readonly struct MetaPopupRequest
    {
        public readonly MetaPopupKind Kind;
        /// <summary>FeatureUnlock일 때 열린 기능</summary>
        public readonly FeatureId Feature;
        /// <summary>종류별 키 (Notice = 공지 ID, EventStart = 이벤트 ID 등). 같은 종류+키는 큐에 하나만</summary>
        public readonly string Key;
        /// <summary>종류별 값 (Return = 비운 초, ChapterChest = 챕터)</summary>
        public readonly long Value;

        public MetaPopupRequest(MetaPopupKind kind, FeatureId feature = FeatureId.Battle, string key = null, long value = 0)
        {
            Kind = kind;
            Feature = feature;
            Key = key;
            Value = value;
        }

        public static MetaPopupRequest Return(long awaySeconds) => new(MetaPopupKind.Return, value: awaySeconds);
        public static MetaPopupRequest Attendance() => new(MetaPopupKind.Attendance);
        public static MetaPopupRequest Notice(string noticeId) => new(MetaPopupKind.Notice, key: noticeId);
        public static MetaPopupRequest FeatureUnlock(FeatureId feature) => new(MetaPopupKind.FeatureUnlock, feature, feature.ToString());
        public static MetaPopupRequest FtueGraduation() => new(MetaPopupKind.FtueGraduation);

        // ── 4차 (SPEC4 §2.9) ──
        /// <summary>다시 만난 날 카드 (접속 종류, 복귀 화면 바로 다음)</summary>
        public static MetaPopupRequest Comeback() => new(MetaPopupKind.Comeback);
        /// <summary>
        /// 이벤트 시작 카드 (접속 종류, key = 회차 키 {eventId}@{startDayKey}). 접속 모달 상한을 넘어도 버리지 않고 남겨,
        /// 접속 묶음이 끝난 뒤 다음 스테이지 사이에 띄운다 (SPEC4 §2.9)
        /// </summary>
        public static MetaPopupRequest EventStart(string instanceKey) => new(MetaPopupKind.EventStart, key: instanceKey);
        /// <summary>챕터 상자 (세션 종류, 스테이지 사이). key = 챕터 문자열 (챕터마다 따로 큐에 남게), value = 챕터</summary>
        public static MetaPopupRequest ChapterChest(int chapter) => new(MetaPopupKind.ChapterChest, key: ChapterKey(chapter), value: chapter);
        /// <summary>동화책 한 쪽 (세션 종류, 같은 챕터의 상자 다음). key = 챕터 문자열, value = 챕터</summary>
        public static MetaPopupRequest StoryPage(int chapter) => new(MetaPopupKind.StoryPage, key: ChapterKey(chapter), value: chapter);
        /// <summary>알림 권한 미리 묻기 (세션 종류, 스테이지 사이)</summary>
        public static MetaPopupRequest NotificationAsk() => new(MetaPopupKind.NotificationAsk);

        /// <summary>챕터 요청 키 (큐에 넣을 때만 만든다 — 드묾)</summary>
        static string ChapterKey(int chapter) => chapter.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>같은 요청으로 볼지 (종류 + 기능 + 키)</summary>
        public bool SameAs(MetaPopupRequest other) => Kind == other.Kind && Feature == other.Feature && Key == other.Key;
    }

    /// <summary>
    /// 메타 팝업 순서 (F-05). 순수 로직이고, 화면에 띄우는 것은 메타 UI의 발표자(MetaPopupPresenter, M3)가 한다.
    ///   · 순서 = MetaPopupKind 값 (복귀 → 다시 만난 날 → 출석부 → 공지 → 이벤트 → 기능 개방 → 졸업 → 챕터 상자 → 동화책 → 알림 묻기),
    ///     같은 종류는 넣은 순서 (챕터 보스의 상자가 늘 그 동화책보다 먼저).
    ///     공지(Notice)는 부팅 뒤 비동기로 불러오므로 보통 큐에 넣지 않고, 발표자가 출석부 다음 차례에 UIManager.ShowStartupNotices로 띄운다
    ///     (그 공지들도 접속 모달 상한에 1개로 센다). 세션 중 새 공지를 따로 띄울 때만 Notice 요청을 쓴다.
    ///   · 같은 요청(SameAs)은 하나만 남긴다. 복귀는 새 값으로 덮는다(가장 최근 비운 시간).
    ///   · 접속 묶음(BeginStartupBatch ~ EndStartupBatch)에서 모달은 최대 MaxStartupModals개. 넘친 것은 버리고 점으로만 (각 시스템의 빨간 점).
    ///     이벤트 시작 카드만은 버리지 않고 남겨, 묶음이 끝난 뒤 다음 스테이지 사이에 띄운다 (SPEC4 §2.9).
    ///   · 세션 도중 모달(기능 개방, 졸업, 챕터 상자, 동화책, 알림 묻기)은 스테이지 사이에만, 그리고 다른 팝업이 없을 때만 꺼낸다 (보스 HP 막대를 가리지 않게).
    ///     전투 중에는 토스트만 쓴다.
    /// <para>
    /// 꺼내는 규칙 (TryDequeue):
    ///   · 접속 묶음 중에는 접속 종류(IsStartupKind)만 꺼낸다 — 전투 상태와 상관없이 바로. 세션 종류는 묶음이 끝날 때까지 기다린다.
    ///   · 접속 묶음 밖에서는 어떤 요청이든 모달이므로 스테이지 사이에만 꺼낸다 (세션 중 날이 바뀌어 들어온 출석부 등).
    /// </para>
    /// </summary>
    public sealed class PopupQueue
    {
        /// <summary>접속 때 한 번에 띄우는 모달 최대 수</summary>
        public const int MaxStartupModals = 3;

        /// <summary>요청이 새로 들어옴 (발표자가 깨어나 TryDequeue를 시도)</summary>
        public event Action Enqueued;

        readonly List<MetaPopupRequest> _items = new();

        public int Count => _items.Count;

        public bool IsEmpty => _items.Count == 0;

        /// <summary>접속 묶음 중인지</summary>
        public bool InStartupBatch { get; private set; }

        /// <summary>이번 접속 묶음에서 꺼낸 모달 수</summary>
        public int StartupShown { get; private set; }

        /// <summary>요청을 넣는다 (순서대로 끼우고 중복은 합침)</summary>
        public void Enqueue(MetaPopupRequest request)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].SameAs(request)) continue;
                // 복귀는 가장 최근 비운 시간으로 덮는다. 다른 종류는 먼저 들어온 것을 그대로 둔다
                if (request.Kind == MetaPopupKind.Return && _items[i].Value != request.Value)
                {
                    _items[i] = request;
                    Enqueued?.Invoke();
                }
                return;
            }

            int index = _items.Count;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Kind > request.Kind)
                {
                    index = i;
                    break;
                }
            }
            _items.Insert(index, request);
            Enqueued?.Invoke();
        }

        public bool Contains(MetaPopupKind kind)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Kind == kind) return true;
            }
            return false;
        }

        public bool Remove(MetaPopupKind kind)
        {
            bool removed = false;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Kind != kind) continue;
                _items.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        /// <summary>맨 앞 요청 (빼지 않음)</summary>
        public bool TryPeek(out MetaPopupRequest request)
        {
            if (_items.Count == 0)
            {
                request = default;
                return false;
            }
            request = _items[0];
            return true;
        }

        /// <summary>
        /// 지금 띄울 수 있는 맨 앞 요청을 꺼낸다.
        /// betweenStages: BattleSystem.IsBetweenStages, popupOpen: UIManager.HasOpenPopup.
        /// 다른 팝업이 열려 있으면 false. 세션 도중 모달은 betweenStages일 때만. 접속 묶음 상한을 넘으면 남은 접속 요청을 버린다.
        /// </summary>
        public bool TryDequeue(bool betweenStages, bool popupOpen, out MetaPopupRequest request)
        {
            request = default;
            if (popupOpen || _items.Count == 0) return false;

            if (InStartupBatch)
            {
                int index = IndexOfStartupKind();
                if (index < 0) return false;
                if (StartupShown >= MaxStartupModals)
                {
                    DropStartupKinds();
                    return false;
                }
                request = _items[index];
                _items.RemoveAt(index);
                StartupShown++;
                return true;
            }

            if (!betweenStages) return false;
            request = _items[0];
            _items.RemoveAt(0);
            return true;
        }

        /// <summary>스테이지 사이에만 띄울 종류인지 (기능 개방 / 졸업 / 챕터 상자 / 동화책 / 알림 묻기)</summary>
        public static bool RequiresBetweenStages(MetaPopupKind kind) =>
            kind == MetaPopupKind.FeatureUnlock || kind == MetaPopupKind.FtueGraduation || kind == MetaPopupKind.ChapterChest
            || kind == MetaPopupKind.StoryPage || kind == MetaPopupKind.NotificationAsk;

        /// <summary>큐에 든 접속 종류 요청 수 (MetaSession이 복귀 때 모듈이 접속 요청을 넣었는지 보려고 쓴다, 할당 없음)</summary>
        public int CountStartupKinds()
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (IsStartupKind(_items[i].Kind)) count++;
            }
            return count;
        }

        /// <summary>접속 때 묶음으로 띄우는 종류인지 (복귀 / 다시 만난 날 / 출석부 / 공지 / 이벤트)</summary>
        public static bool IsStartupKind(MetaPopupKind kind) => kind <= MetaPopupKind.EventStart;

        /// <summary>접속 묶음 시작 (부팅·복귀 때 MetaSession이 부른다)</summary>
        public void BeginStartupBatch()
        {
            InStartupBatch = true;
            StartupShown = 0;
        }

        /// <summary>
        /// 큐 밖에서 띄우는 접속 모달(시작 공지 묶음) 한 자리를 잡는다. 접속 묶음 상한이 남았으면 true (StartupShown + 1).
        /// 접속 묶음이 아니면 항상 true.
        /// </summary>
        public bool TryReserveStartupModal()
        {
            if (!InStartupBatch) return true;
            if (StartupShown >= MaxStartupModals) return false;
            StartupShown++;
            return true;
        }

        /// <summary>접속 묶음 끝 (발표자가 접속 요청을 다 띄웠을 때)</summary>
        public void EndStartupBatch()
        {
            InStartupBatch = false;
        }

        public void Clear()
        {
            _items.Clear();
            InStartupBatch = false;
            StartupShown = 0;
        }

        int IndexOfStartupKind()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (IsStartupKind(_items[i].Kind)) return i;
            }
            return -1;
        }

        /// <summary>
        /// 상한을 넘은 접속 요청을 버린다 (각 시스템의 빨간 점만 남음). 이벤트 시작 카드는 버리지 않고 남겨,
        /// 접속 묶음이 끝난 뒤 다음 스테이지 사이에 띄운다 (SPEC4 §2.9)
        /// </summary>
        void DropStartupKinds()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var kind = _items[i].Kind;
                if (IsStartupKind(kind) && kind != MetaPopupKind.EventStart) _items.RemoveAt(i);
            }
        }
    }
}
