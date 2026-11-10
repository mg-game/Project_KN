using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>의상 도감 보기: 컨셉 시트 일러스트를 크게 보여준다. 아무 곳이나 탭하면 닫힌다.</summary>
    public class IllustrationPopup : UIPopup
    {
        [SerializeField] TMP_Text _titleText;
        [SerializeField] Image _image;
        [SerializeField] GameObject _emptyLabel;
        [SerializeField] Button _tapArea;

        protected override void Awake()
        {
            base.Awake();
            _tapArea.onClick.AddListener(Close);
        }

        public void Show(Sprite illustration, string title)
        {
            Open();
            _titleText.text = string.IsNullOrEmpty(title) ? "도감" : title;
            _image.sprite = illustration;
            _image.enabled = illustration != null;
            _emptyLabel.SetActive(illustration == null);
            if (illustration != null) StartCoroutine(CharacterMotion.Pop(_image.transform, 0.3f, 0.9f));
        }
    }
}
