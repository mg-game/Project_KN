using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 이벤트 시작 카드 840×620 (EVT-01, SPEC4 §8.3 — 기능 개방 카드와 같은 틀): 이벤트 그림 띠 (EventDef.banner — 없으면 빌더가 넣은 shop_event_banner) +
    /// '{이벤트 이름}가 시작됐어요!' (이/가는 받침으로) + 한 줄 (EventDef.shortDesc) + [나중에] Secondary · [가 보기 ›] Primary → UINav.OpenEvents (이벤트 모아 보기).
    /// 발표자(MetaPopupPresenter)가 EventStart(회차 키 {eventId}@{startDayKey}) 요청을 띄운다 — 접속 모달 상한을 넘으면 다음 스테이지 사이에 (PopupQueue).
    /// 이벤트를 찾지 못하면 띄우지 않는다. 열 때 그 회차를 본 것으로 (EventCalendarSystem.MarkSeen — top.event.new 점이 꺼진다).
    /// </summary>
    public class EventStartCard : UIPopup
    {
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _bodyText;
        [SerializeField] Image _banner;
        [Tooltip("이벤트에 그림이 없을 때 쓰는 띠 (빌더: UI/shop_event_banner)")]
        [SerializeField] Sprite _fallbackBanner;
        [SerializeField] Button _goButton;
        [SerializeField] Button _laterButton;
        [SerializeField] MetaSparkleBurst _sparkles;

        string _instanceKey;

        /// <summary>지금 보여 주는 회차 키</summary>
        public string InstanceKey => _instanceKey;

        protected override void Awake()
        {
            base.Awake();
            if (_goButton != null) _goButton.onClick.AddListener(Go);
            if (_laterButton != null) _laterButton.onClick.AddListener(Close);
        }

        /// <summary>회차 키의 이벤트 ID ({eventId}@{startDayKey} → eventId). 비었으면 null</summary>
        public static string EventIdOf(string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey)) return null;
            int at = instanceKey.IndexOf('@');
            return at < 0 ? instanceKey : at == 0 ? null : instanceKey.Substring(0, at);
        }

        /// <summary>이/가 (MetaUi.WithSubject — 받침 있으면 '이', 없으면 '가')</summary>
        public static string WithSubject(string word) => MetaUi.WithSubject(word);

        /// <summary>제목 (순수): '{이름}가 시작됐어요!'</summary>
        public static string TitleFor(string eventName) =>
            string.IsNullOrEmpty(eventName) ? "새 이벤트가 시작됐어요!" : $"{WithSubject(eventName)} 시작됐어요!";

        /// <summary>카드를 띄운다. 이벤트를 못 찾으면 false</summary>
        public bool Show(string instanceKey)
        {
            var def = FindEvent(EventIdOf(instanceKey));
            if (def == null) return false;
            _instanceKey = instanceKey;
            if (_titleText != null) _titleText.text = TitleFor(def.name);
            if (_bodyText != null) _bodyText.text = def.shortDesc ?? string.Empty;
            if (_banner != null)
            {
                var sprite = def.banner != null ? def.banner : _fallbackBanner;
                _banner.sprite = sprite;
                _banner.enabled = sprite != null;
            }
            Open();
            Game.Events?.MarkSeen(instanceKey);
            if (_sparkles != null && _banner != null) _sparkles.BurstAt(_banner.rectTransform, 12, UITheme.Lamp, 0.9f);
            UISfx.Play(UISfxKey.FeatureUnlock);
            return true;
        }

        static EventDef FindEvent(string id)
        {
            if (string.IsNullOrEmpty(id) || !MetaUi.IsActive) return null;
            var db = Game.MetaDb;
            if (db == null || db.events == null) return null;
            foreach (var def in db.events)
            {
                if (def != null && def.id == id) return def;
            }
            return null;
        }

        void Go()
        {
            Close();
            UINav.OpenEvents();
        }
    }
}
