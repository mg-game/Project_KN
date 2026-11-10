using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 공지 팝업. 목록 ↔ 상세 화면을 전환한다.
    /// 접속 시 자동으로 열릴 때는 "오늘 하루 보지 않기"를 함께 보여준다.
    /// 공지는 원격에서 늦게 올 수 있다 (M65): 목록이 비어 있는 동안 불러오는 중이면 '공지를 불러오는 중이에요…', 다 불러왔는데 없으면
    /// '아직 등록된 공지가 없어요'. 불러오기가 끝나면 Notice.Changed로 목록이 다시 그려진다.
    /// </summary>
    public class NoticePopup : UIPopup
    {
        [Header("목록")]
        [SerializeField] GameObject _listView;
        [SerializeField] RectTransform _listContent;
        [SerializeField] NoticeItemView _itemPrefab;
        [SerializeField] GameObject _emptyLabel;
        [Tooltip("빈 목록 문구 (비면 _emptyLabel에서 찾는다)")]
        [SerializeField] TMP_Text _emptyText;

        [Header("상세")]
        [SerializeField] GameObject _detailView;
        [SerializeField] Button _backButton;
        [SerializeField] TMP_Text _detailTitle;
        [SerializeField] TMP_Text _detailDate;
        [SerializeField] TMP_Text _detailBody;
        [SerializeField] ScrollRect _detailScroll;

        [Header("접속 팝업")]
        [SerializeField] Toggle _hideTodayToggle;

        public const string LoadingText = "공지를 불러오는 중이에요…";
        public const string EmptyText = "아직 등록된 공지가 없어요";

        UIListPool<NoticeItemView> _pool;
        bool _startupMode;

        protected override void Awake()
        {
            base.Awake();
            _pool = new UIListPool<NoticeItemView>(_itemPrefab, _listContent);
            _backButton.onClick.AddListener(ShowList);
            if (_emptyText == null && _emptyLabel != null) _emptyText = _emptyLabel.GetComponent<TMP_Text>();
        }

        public void OpenList()
        {
            _startupMode = false;
            Open();
            ShowList();
        }

        public void OpenStartup(NoticeData notice)
        {
            _startupMode = true;
            Open();
            ShowDetail(notice);
        }

        protected override void OnOpen()
        {
            Game.Notice.Changed -= RefreshList;
            Game.Notice.Changed += RefreshList;
            _hideTodayToggle.isOn = false;
            _hideTodayToggle.gameObject.SetActive(_startupMode);
        }

        protected override void OnClose()
        {
            if (!Game.IsReady) return;
            Game.Notice.Changed -= RefreshList;
            if (_startupMode && _hideTodayToggle.isOn) Game.Notice.HideForToday();
        }

        void ShowList()
        {
            _listView.SetActive(true);
            _detailView.SetActive(false);
            RefreshList();
        }

        void ShowDetail(NoticeData notice)
        {
            Game.Notice.MarkRead(notice);
            _listView.SetActive(false);
            _detailView.SetActive(true);
            _detailTitle.text = notice.title;
            _detailDate.text = notice.date;
            _detailBody.text = notice.body;
            _detailScroll.verticalNormalizedPosition = 1f;
        }

        void RefreshList()
        {
            if (!_listView.activeSelf) return;

            var notices = Game.Notice.Notices;
            _pool.SetCount(notices.Count);
            for (int i = 0; i < notices.Count; i++) _pool[i].Set(notices[i], Game.Notice.IsRead(notices[i]), ShowDetail);

            bool empty = notices.Count == 0;
            _emptyLabel.SetActive(empty);
            if (empty && _emptyText != null)
            {
                string text = Game.Notice.IsLoaded ? EmptyText : LoadingText;
                if (_emptyText.text != text) _emptyText.text = text;
            }
        }
    }
}
