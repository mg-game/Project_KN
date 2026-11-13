using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 여러 칸 중 하나를 고르는 버튼 줄 (설정: 잠자기 끄기/1/2/3/5분, 흔들림 0/50/100%, 피해 숫자, 품질 등).
    /// 모양은 M53 고르기 칸(TabStyle Option): 고른 칸 = PrimaryButton 칠 + 2px Cream@0.6 테두리(칸 바탕의 Outline) + Cream 굵게,
    /// 안 고른 칸 = Secondary 칠 + SubText 보통 — 색만으로 고르지 않게 테두리 · 굵기도 바뀐다. 칸 바탕은 흰색 틴트용 알약(빌더 MetaSceneBuilder.Segmented).
    /// Changed(새 인덱스)는 사용자가 눌렀을 때만 울린다. 공개 API(Changed · Index · Count · SetIndex)는 설정 팝업이 쓴다 (계약 C-11).
    /// </summary>
    public class MetaSegmentedControl : MonoBehaviour
    {
        [SerializeField] Button[] _buttons = Array.Empty<Button>();
        [SerializeField] Image[] _backgrounds = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _labels = Array.Empty<TMP_Text>();

        /// <summary>사용자가 칸을 골랐음 (새 인덱스)</summary>
        public event Action<int> Changed;

        public int Index { get; private set; } = -1;

        public int Count => _buttons.Length;

        void Awake()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                _buttons[i].onClick.AddListener(() => Select(index));
            }
        }

        void Select(int index)
        {
            if (index == Index) return;
            SetIndex(index);
            Changed?.Invoke(index);
        }

        /// <summary>알리지 않고 고른 칸만 바꾼다 (설정 값으로 맞출 때)</summary>
        public void SetIndex(int index)
        {
            Index = index;
            for (int i = 0; i < _buttons.Length; i++)
            {
                var background = i < _backgrounds.Length ? _backgrounds[i] : null;
                var label = i < _labels.Length ? _labels[i] : null;
                TabStyle.Apply(background, label, i == index, TabVariant.Option);
            }
        }
    }
}
