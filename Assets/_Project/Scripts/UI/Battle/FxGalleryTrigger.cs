using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 공격 연출 시험장을 여는 숨은 손짓 (ATK-17): 개발 빌드에서 스테이지 이름표를 2초 안에 5번 누르면 FxGalleryView가 열린다.
    /// 릴리스 빌드에서는 스스로 꺼지고 이름표가 입력을 받지 않는다 (설정 팝업의 개발 메뉴 대신 — 다른 화면을 건드리지 않는다).
    /// 5번째가 아닌 누름은 부모(스테이지 판 StagePanel 단추 — 스테이지 고르기 M13 P2)로 넘긴다 (Wave 3 r1: 이름표가 누름을 삼켜 개발 빌드에서만
    /// 이름 자리를 눌러도 스테이지 고르기가 열리지 않았다). 고르기 판이 열리면 그 뒤 투명 덮개가 다음 누름을 받아 닫으므로, 손짓은 이름표를 다섯 번
    /// (덮개 누름은 세지 않는다 — 이름표 누름 사이가 2초 안이면 된다). 시험장이 열려 있는 동안에는 넘기지 않는다 (이름표 누름은 닫기 손짓만).
    /// </summary>
    public class FxGalleryTrigger : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] FxGalleryView _gallery;
        [Tooltip("이 시간(초) 안에 이어서 눌러야 센다")]
        [SerializeField] float _window = 2f;
        [SerializeField] int _taps = 5;

        int _count;
        float _last;

        void Awake()
        {
            if (FxGalleryView.Available && _gallery != null) return;
            // 릴리스 빌드: 이름표는 그냥 글자 (입력을 막지 않게)
            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = false;
            enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!enabled || _gallery == null)
            {
                Forward(eventData);
                return;
            }
            float now = Time.unscaledTime;
            _count = now - _last <= _window ? _count + 1 : 1;
            _last = now;
            if (_count < _taps)
            {
                if (!_gallery.IsOpen) Forward(eventData);
                return;
            }
            _count = 0;
            if (_gallery.IsOpen) _gallery.Close();
            else _gallery.Open();
        }

        /// <summary>누름을 부모 쪽 첫 누름 받는 곳(스테이지 판 단추)으로 넘긴다 — 입력 모듈은 이름표에만 누름을 보낸다</summary>
        void Forward(PointerEventData eventData)
        {
            var parent = transform.parent;
            if (parent != null && eventData != null) ExecuteEvents.ExecuteHierarchy(parent.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }
    }
}
