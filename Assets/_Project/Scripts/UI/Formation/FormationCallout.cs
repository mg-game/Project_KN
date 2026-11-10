using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleGame.UI
{
    /// <summary>
    /// "동시 등록 차단" 안내 말풍선. 반대편(지킴이↔잔향이)에 이미 등록된 친구를 놓으려 할 때 잠깐 떠오른다.
    /// 누르면 바로 닫힌다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FormationCallout : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] FormPortraitView _portrait;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _bodyText;
        [SerializeField] float _duration = 2.8f;

        CanvasGroup _group;
        Coroutine _routine;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
        }

        /// <summary>
        /// 떠 있는 중에 탭을 바꾸면 패널과 함께 꺼지면서 Run 코루틴이 끊겨 스스로 닫히지 못한다.
        /// 다시 켜졌을 때 말풍선이 남아 잔향이 덱 칸의 탭·드롭을 가로채지 않도록, 보이지 않고 입력도 받지 않는 상태로 되돌린다.
        /// (활성 전환 중에는 SetActive를 부를 수 없어 알파·레이캐스트로 숨긴다)
        /// </summary>
        void OnDisable()
        {
            _routine = null;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }
            transform.localScale = Vector3.one;
        }

        public void Show(string formId, string title, string body)
        {
            gameObject.SetActive(true);
            // 패널이 꺼져 있으면 띄우지 않는다 (코루틴이 돌지 않아 닫히지 못한다)
            if (!isActiveAndEnabled)
            {
                gameObject.SetActive(false);
                return;
            }
            _group.blocksRaycasts = true;
            transform.SetAsLastSibling();
            _portrait.SetForm(formId);
            _titleText.text = title;
            _bodyText.text = body;
            MenuFx.Restart(this, ref _routine, Run());
        }

        public void Hide()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData) => Hide();

        IEnumerator Run()
        {
            StartCoroutine(MenuFx.PopIn(transform, 0.25f));
            yield return MenuFx.Fade(_group, 0f, 1f, 0.15f);
            yield return new WaitForSecondsRealtime(_duration);
            yield return MenuFx.Fade(_group, 1f, 0f, 0.3f);
            _routine = null;
            gameObject.SetActive(false);
        }
    }
}
