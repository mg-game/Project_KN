using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 보상 한 칸 (아이콘 + 양, 선택적으로 이름). 수선 일지 상자·미션 보상 칩, 출석부 칸, 바구니·복귀 화면 보상 칸에 같이 쓴다.
    /// 아이콘 아트가 없으면 선물 상자 그림(MetaIcons.RewardGeneric)으로 대신한다.
    /// 칸 버튼이 있으면(빌더 MetaSceneBuilder.BuildRewardChip — 버튼 + PressScale) 누르면 무엇인지 알려 준다 (M59, UX-SPEC §3.6):
    /// 재료 → 아이템 정보 팝업(ItemInfoPopup), 재화 · 단추 N분치 → 재화 안내 말풍선(InfoTooltipView + CurrencySources), 수선권 · 친구 · 의상 → 짧은 안내 말풍선.
    /// 출석부 칸처럼 칸 버튼 안에 든 보상은 버튼이 없어 칸이 누름을 받는다.
    /// </summary>
    public class MetaRewardChip : MonoBehaviour
    {
        [SerializeField] MetaIcons _icons;
        [Tooltip("누르면 보상 안내 (없어도 됨 — 칸 안에 든 보상)")]
        [SerializeField] Button _button;
        [SerializeField] Image _frame;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _amountText;
        [Tooltip("없어도 됨")]
        [SerializeField] TMP_Text _nameText;
        [Tooltip("양 아래 작은 줄 (바구니 \"+3.1/시간\" 등). 없어도 됨")]
        [SerializeField] TMP_Text _subText;

        /// <summary>테두리 아트에 섞는 등급 색 비율</summary>
        const float FrameTint = 0.35f;

        RectTransform _rect;

        public MetaReward Reward { get; private set; }

        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public RectTransform IconRect => _icon != null ? _icon.rectTransform : Rect;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(ShowInfo);
        }

        /// <summary>
        /// 이 보상이 무엇인지 (M59): 재료 = 아이템 정보 팝업, 재화 = 재화 설명 · 쓰는 곳 · 얻는 곳 말풍선, 그 밖 = 이름 + 한 줄 말풍선.
        /// 누를 때만 (말풍선 글은 그때 만든다)
        /// </summary>
        public void ShowInfo()
        {
            var reward = Reward;
            if (reward == null || !MetaUi.IsActive) return;
            switch (reward.type)
            {
                case MetaRewardType.Item:
                {
                    var def = Game.Items != null ? Game.Items.Get(reward.itemId) : null;
                    var ui = UIManager.Instance;
                    if (def != null && ui != null && ui.TryGet<ItemInfoPopup>(out var info))
                    {
                        info.Show(def);
                        return;
                    }
                    InfoTooltipView.Show(Rect, MetaUi.NameOf(reward), "수선 재료예요", null);
                    return;
                }
                case MetaRewardType.Currency:
                case MetaRewardType.GoldMinutes:
                {
                    var type = reward.type == MetaRewardType.GoldMinutes ? CurrencyType.Gold : reward.currency;
                    InfoTooltipView.Show(Rect, CurrencySources.Title(type), CurrencySources.Body(type), CurrencySources.SourceLine(type));
                    return;
                }
                default:
                    InfoTooltipView.Show(Rect, MetaUi.NameOf(reward), InfoBody(reward), null);
                    return;
            }
        }

        /// <summary>재화 · 재료가 아닌 보상의 한 줄 안내</summary>
        static string InfoBody(MetaReward reward)
        {
            switch (reward.type)
            {
                case MetaRewardType.QuickIdleTicket:
                    float hours = Game.MetaDb != null ? Game.MetaDb.idleChest.quickIdleHours : 2f;
                    return $"수선 바구니에서 쓰면 {MetaUi.Hours(hours)}치 선물을 바로 받아요";
                case MetaRewardType.DoubleTicket:
                    return "다녀오셨어요 화면에서 바구니 선물을 두 배로 받아요";
                case MetaRewardType.SelectShards:
                    return "원하는 친구의 기억편으로 바꿀 수 있어요";
                case MetaRewardType.Lineage:
                    return "새 친구가 수선점에 찾아와요 (이미 함께라면 기억편)";
                case MetaRewardType.Costume:
                    return "친구에게 입힐 수 있는 의상이에요";
                default:
                    return string.Empty;
            }
        }

        public void Set(MetaReward reward)
        {
            Reward = reward;
            if (reward == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            var sprite = _icons != null ? _icons.Reward(reward) : null;
            // 아이콘이 없는 보상은 선물 상자 그림 (없으면 별), 흰색 그대로
            if (sprite == null && _icons != null) sprite = _icons.RewardGeneric != null ? _icons.RewardGeneric : _icons.Star;
            if (_icon != null)
            {
                _icon.enabled = sprite != null;
                _icon.sprite = sprite;
                _icon.color = Color.white;
                _icon.preserveAspect = true;
            }
            // 테두리 아트는 등급 색을 살짝만 섞는다 (진한 색으로 덮으면 금테가 탁해진다)
            if (_frame != null) _frame.color = MetaUi.Alpha(Color.Lerp(Color.white, MetaUi.RewardFrameColor(reward), FrameTint), _frame.color.a);
            if (_amountText != null) _amountText.text = MetaUi.ShortAmount(reward);
            if (_nameText != null) _nameText.text = MetaUi.NameOf(reward);
        }

        /// <summary>양 글자만 바꾼다 (카운트업)</summary>
        public void SetAmountText(string text)
        {
            if (_amountText != null) _amountText.text = text;
        }

        public void SetAmountColor(Color color)
        {
            if (_amountText != null) _amountText.color = color;
        }

        /// <summary>작은 줄 (없으면 무시)</summary>
        public void SetSubText(string text)
        {
            if (_subText == null) return;
            _subText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _subText.text = text;
        }

        /// <summary>아이콘을 흐리게 (아직 안 쌓인 줄, 받은 칸)</summary>
        public void SetDimmed(bool dimmed)
        {
            if (_icon != null)
            {
                var color = _icon.color;
                color.a = dimmed ? 0.4f : 1f;
                _icon.color = color;
            }
        }
    }

    /// <summary>
    /// 템플릿 하나로 같은 뷰를 필요한 만큼 만들어 재사용하는 풀 (목록 행, 보상 칸). 남는 것은 끈다.
    /// 템플릿은 씬 안의 꺼진 오브젝트나 프리팹이다.
    /// </summary>
    public sealed class MetaViewPool<T> where T : Component
    {
        readonly T _template;
        readonly Transform _parent;
        readonly List<T> _items = new();

        public MetaViewPool(T template, Transform parent)
        {
            _template = template;
            _parent = parent;
            if (_template != null && _template.transform.parent == parent) _template.gameObject.SetActive(false);
        }

        public int ActiveCount { get; private set; }

        public IReadOnlyList<T> Items => _items;

        /// <summary>index번째 뷰 (없으면 만들고 켬)</summary>
        public T Get(int index)
        {
            while (_items.Count <= index)
            {
                var item = Object.Instantiate(_template, _parent, false);
                item.name = _template.name + _items.Count;
                _items.Add(item);
            }
            var view = _items[index];
            if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);
            return view;
        }

        /// <summary>count개만 켜 두고 나머지는 끈다</summary>
        public void Trim(int count)
        {
            ActiveCount = count;
            for (int i = count; i < _items.Count; i++)
            {
                if (_items[i].gameObject.activeSelf) _items[i].gameObject.SetActive(false);
            }
        }
    }
}
