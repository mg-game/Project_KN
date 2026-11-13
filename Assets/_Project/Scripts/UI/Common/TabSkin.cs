using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 쪽 탭 그림 한 쌍 (켜짐 · 꺼짐, SPEC4 SX-2 · ART-UPGRADE §4.3 #9). 탭 바탕 Image에 붙이면 TabStyle.Apply(Page)가 색 대신 스프라이트를 바꾸고
    /// 흰색으로 둔다. 붙이지 않았거나 한 쌍이 다 있지 않으면(같은 그림 두 장 포함) 지금처럼 색으로 칠한다 — 그래서 아트가 없으면 아무것도 바뀌지 않는다.
    /// 빌더는 전용 아트가 둘 다 있을 때만(UiArt.HasTabSkin) 붙인다 (Binder로 _on · _off를 넣거나 Set을 부른다). 프레임마다 아무것도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabSkin : MonoBehaviour
    {
        [SerializeField] Sprite _on;
        [SerializeField] Sprite _off;

        /// <summary>선택된 탭 그림 (UiArt.TabOn9s)</summary>
        public Sprite On => _on;

        /// <summary>선택 안 된 탭 그림 (UiArt.TabOff9s)</summary>
        public Sprite Off => _off;

        /// <summary>스프라이트를 바꿔 쓸 수 있는지 (두 장이 다 있고 서로 다름 — 틴트용 대체 그림 한 장이면 false)</summary>
        public bool IsUsable => _on != null && _off != null && _on != _off;

        /// <summary>그림 한 쌍을 넣는다 (빌더)</summary>
        public void Set(Sprite on, Sprite off)
        {
            _on = on;
            _off = off;
        }
    }
}
