using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 보상/재료 칸 (재화·재료·의상). 큰 칸(보상 팝업, 가방)과 작은 칩(원정·던전 드롭 목록)이 같은 컴포넌트를 쓴다.
    /// 아트가 없으면 흐린 별(자리표시) + 이름 첫 글자로 대신한다. 테두리는 흰 칸 틀 그림이라 등급 색을 그대로 입힌다.
    ///   · 수량은 '×N' (M30 — 작은 칩은 작은 곱하기 UIFormat.TimesSmall + 줄임 표기 3K, 큰 칸은 ×와 콤마).
    ///   · 등급 표시 (M83): 큰 칸은 왼쪽 위 등급 알약(레어 이상, 재화 없음), 작은 칩은 위쪽 3px 등급 띠(에픽 이상).
    /// </summary>
    public class RewardSlotView : MonoBehaviour
    {
        [SerializeField] Image _frame;
        [SerializeField] Image _glow;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _fallbackText;
        [SerializeField] TMP_Text _amountText;
        [Tooltip("수량 글자 뒤 받침 (없으면 글자만 켜고 끔)")]
        [SerializeField] GameObject _amountBox;
        [Tooltip("큰 칸에서만 사용 (없어도 됨)")]
        [SerializeField] TMP_Text _nameText;
        [SerializeField] GameObject _dim;
        [SerializeField] Button _button;
        [SerializeField] Sprite _placeholder;
        [Tooltip("CurrencyType 순서 (단추, 별조각, 별빛 사탕). 비어 있으면 색 원으로 대신")]
        [SerializeField] Sprite[] _currencyIcons;
        [Header("등급 표시 (M83, 없어도 됨)")]
        [Tooltip("큰 칸 왼쪽 위 등급 알약 (레어 이상 · 재화 아님)")]
        [SerializeField] GameObject _gradePill;
        [SerializeField] Image _gradePillImage;
        [SerializeField] TMP_Text _gradeName;
        [Tooltip("작은 칩 위쪽 3px 등급 띠 (에픽 이상)")]
        [SerializeField] Image _gradeBar;

        Action _onClick;
        Color _amountBaseColor;
        bool _amountColorKnown;

        /// <summary>재료 칸이면 그 아이템 (재화/의상이면 null)</summary>
        public ItemDef Item { get; private set; }

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke());
            RememberAmountColor();
        }

        /// <param name="compact">작은 칩 (줄임 표기 · 작은 곱하기)</param>
        public void Set(RewardData reward, bool compact = false)
        {
            if (reward.type == RewardType.Currency)
            {
                SetCurrency(reward.currency, compact
                    ? RewardService.FormatAmountCompact(reward.currency, reward.amount)
                    : RewardService.FormatAmount(reward.currency, reward.amount));
                return;
            }
            string amount = CountLabel(reward.amount, compact);
            var def = Game.Items.Get(reward.itemId);
            if (def != null) SetItem(def, amount);
            else SetRaw(reward.itemId, ItemGrade.Common, null, amount);
        }

        /// <summary>재료 수량 '×5' (작은 칩은 작은 곱하기 + 줄임 표기 '×1.2K')</summary>
        public static string CountLabel(double amount, bool compact) => compact
            ? UIFormat.TimesSmall + NumberFormatter.FormatCompact(Math.Floor(amount))
            : UIFormat.Times + NumberFormatter.FormatInt(Math.Floor(amount));

        /// <summary>보상을 표시하고, 재료면 누를 때 재료 정보(획득처)를 연다</summary>
        public void SetWithInfo(RewardData reward, bool compact = false)
        {
            Set(reward, compact);
            SetItemInfoClick();
        }

        /// <summary>재료 칸이면 누를 때 재료 정보 팝업을 연다 (재화/의상은 누를 수 없음)</summary>
        public void SetItemInfoClick()
        {
            var item = Item;
            SetClick(item != null ? () => UIManager.Instance.Get<ItemInfoPopup>().Show(item) : null);
        }

        public void SetItem(ItemDef def, string amount)
        {
            SetRaw(def.name, def.grade, def.icon, amount);
            Item = def;
        }

        public void SetCurrency(CurrencyType type, string amount)
        {
            var color = UITheme.CurrencyColor(type);
            var sprite = _currencyIcons != null && (int)type < _currencyIcons.Length ? _currencyIcons[(int)type] : null;
            Apply(RewardService.CurrencyName(type), color, sprite, amount, null);
            // 실타래 임시 그림은 흰 모양 재료라 산호를 곱한다 (T2, UITheme.CurrencyIconTint)
            if (sprite != null) _icon.color = UITheme.CurrencyIconTint(type);
            Item = null;
        }

        /// <summary>의상 상품/보상 (초상화)</summary>
        public void SetCostume(CostumeDef costume, ItemGrade grade)
        {
            Apply(costume.name, UITheme.GradeColor(grade), costume.portrait, "의상", grade);
            Item = null;
        }

        public void SetDimmed(bool dimmed)
        {
            if (_dim != null) _dim.SetActive(dimmed);
        }

        /// <summary>수량 글자 색 (가방: 0개 = SubText, 가진 수 = 원래 색). null이면 원래 색으로</summary>
        public void SetAmountColor(Color? color)
        {
            RememberAmountColor();
            var target = color ?? _amountBaseColor;
            if (_amountText != null && _amountText.color != target) _amountText.color = target;
        }

        /// <summary>누르면 실행할 동작 (null이면 누를 수 없음)</summary>
        public void SetClick(Action onClick)
        {
            _onClick = onClick;
            if (_button != null) _button.interactable = onClick != null;
        }

        void RememberAmountColor()
        {
            if (_amountColorKnown || _amountText == null) return;
            _amountColorKnown = true;
            _amountBaseColor = _amountText.color;
        }

        void SetRaw(string displayName, ItemGrade grade, Sprite icon, string amount)
        {
            Apply(displayName, UITheme.GradeColor(grade), icon, amount, grade);
            Item = null;
        }

        void Apply(string displayName, Color color, Sprite icon, string amount, ItemGrade? grade)
        {
            if (_frame != null) _frame.color = color;
            if (_glow != null) _glow.color = new Color(color.r, color.g, color.b, 0.45f);

            bool hasIcon = icon != null;
            _icon.sprite = hasIcon ? icon : _placeholder;
            _icon.color = hasIcon ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (_fallbackText != null)
            {
                _fallbackText.gameObject.SetActive(!hasIcon);
                _fallbackText.text = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);
            }

            if (_nameText != null) _nameText.text = displayName;
            bool hasAmount = !string.IsNullOrEmpty(amount);
            (_amountBox != null ? _amountBox : _amountText.gameObject).SetActive(hasAmount);
            _amountText.text = amount;
            ApplyGrade(grade);
            SetDimmed(false);
        }

        /// <summary>등급 알약(레어 이상) · 등급 띠(에픽 이상). 재화(grade 없음)와 보통 등급은 숨긴다</summary>
        void ApplyGrade(ItemGrade? grade)
        {
            bool pill = grade.HasValue && grade.Value > ItemGrade.Common;
            if (_gradePill != null)
            {
                if (_gradePill.activeSelf != pill) _gradePill.SetActive(pill);
                if (pill)
                {
                    if (_gradePillImage != null) _gradePillImage.color = UITheme.GradeColor(grade.Value);
                    if (_gradeName != null) _gradeName.text = UITheme.GradeName(grade.Value);
                }
            }
            if (_gradeBar != null)
            {
                bool bar = grade.HasValue && grade.Value >= ItemGrade.Epic;
                if (_gradeBar.gameObject.activeSelf != bar) _gradeBar.gameObject.SetActive(bar);
                if (bar) _gradeBar.color = UITheme.GradeColor(grade.Value);
            }
        }
    }
}
