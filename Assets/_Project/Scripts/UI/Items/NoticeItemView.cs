using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    public class NoticeItemView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _dateText;
        [SerializeField] GameObject _unreadDot;

        NoticeData _notice;
        Action<NoticeData> _onClick;

        void Awake()
        {
            _button.onClick.AddListener(() => _onClick?.Invoke(_notice));
        }

        public void Set(NoticeData notice, bool isRead, Action<NoticeData> onClick)
        {
            _notice = notice;
            _onClick = onClick;
            _titleText.text = notice.title;
            _dateText.text = notice.date;
            _unreadDot.SetActive(!isRead);
        }
    }
}
