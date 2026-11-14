using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 상점 상품 카드 (UX-SPEC §2.9 M56): 아이콘(재료 '×5' 배지 / 의상 초상화), 이름(개수 없이), 남은 횟수 + 보유 수('오늘 2/3 · 보유 15'), 가격 버튼(CostButtonView).
    /// 사기 (OnBuy):
    ///   1. 먼저 확인 (ShopSystem.CanBuy). 모자라면 단추 = 부족 토스트 + 흔들기, 별조각 · 별빛 사탕 = 부족 팝업. 다른 이유(품절 등)는 그 문장 토스트.
    ///   2. 보상 하나짜리: 별조각 가격은 쓰기 확인(오늘 묻지 않기 없음), 단추 가격은 바로. 사면 아이콘에서 상단 바로 날리고 카드를 1.06배 튕기고
    ///      '{이름} ×{n} 획득 (보유 {수})' 토스트, 남은 횟수를 바로 고친다. 0.15초 동안은 다시 눌리지 않는다 (연타로 두 번 사지 않게).
    ///   3. 묶음: 보상 팝업 '구매 완료' (확인을 누르면 날아간다).
    ///   4. 의상: 쓰기 확인 → 보상 팝업 '의상 획득!' — 그 친구가 있으면 '바로 입혀 보기'(입히고 친구 상세 의상 탭), 없으면 '…을(를) 만나면 입힐 수 있어요'.
    /// 품절 덮개는 품절이 되는 순간 0.2초에 나타난다.
    /// </summary>
    public class ShopProductCardView : MonoBehaviour
    {
        /// <summary>산 뒤 다시 누를 수 없는 시간 (초, 실제 시간)</summary>
        const float TapLock = 0.15f;
        const float PunchScale = 1.06f;
        const float PunchUp = 0.08f;
        const float PunchDown = 0.14f;
        const float SoldOutFade = 0.2f;

        [SerializeField] RewardSlotView _icon;
        [SerializeField] Image _iconGlow;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _limitText;
        [SerializeField] Button _buyButton;
        [SerializeField] Image _priceIcon;
        [SerializeField] TMP_Text _priceText;
        [Tooltip("CurrencyType 순서 (단추, 별조각, 별빛 사탕)")]
        [SerializeField] Sprite[] _currencyIcons;
        [SerializeField] Sprite _priceFallback;
        [SerializeField] GameObject _soldOut;
        [SerializeField] TMP_Text _soldOutText;
        [Tooltip("품절 덮개 나타남 (0.2초)")]
        [SerializeField] CanvasGroup _soldOutGroup;

        ShopDef _shop;
        ShopProductDef _product;
        CostumeDef _costume;
        CostButtonView _cost;
        bool _locked;
        bool _shownSoldOut;
        Coroutine _punch;
        Coroutine _lock;
        Coroutine _fade;

        void Awake()
        {
            _buyButton.onClick.AddListener(OnBuy);
            _cost = _buyButton.GetComponent<CostButtonView>();
        }

        void OnDisable()
        {
            // 꺼지면 코루틴이 멈춘다 — 크기 · 잠금 · 덮개를 끝 모양으로
            _punch = null;
            _lock = null;
            _fade = null;
            _locked = false;
            transform.localScale = Vector3.one;
            if (_soldOutGroup != null) _soldOutGroup.alpha = 1f;
        }

        public void Set(ShopDef shop, ShopProductDef product)
        {
            bool same = _product == product;
            _shop = shop;
            _product = product;
            _costume = string.IsNullOrEmpty(product.costumeId) ? null : Game.Characters.Db.GetCostume(product.costumeId);
            if (!same) _shownSoldOut = false;

            _nameText.text = product.name;
            SetIcon();
            SetPrice();
            RefreshLimit(!same);
            RefreshAffordable();
        }

        /// <summary>재화가 바뀔 때마다 버튼 상태만 (넉넉함 / 부족함 — 같은 상태면 CostButtonView가 아무것도 하지 않는다)</summary>
        public void RefreshAffordable()
        {
            if (_product == null || _shownSoldOut) return;
            bool affordable = Game.Currency.Has(_product.currency, _product.price);
            if (_cost != null) _cost.SetAffordable(affordable);
            else _priceText.color = affordable ? UITheme.Text : UITheme.Negative;
        }

        void SetIcon()
        {
            ItemGrade grade = ItemGrade.Common;
            if (_costume != null)
            {
                var lineage = Game.Characters.Db.GetLineageOfCostume(_costume.id);
                grade = lineage != null ? lineage.grade : ItemGrade.Epic;
                _icon.SetCostume(_costume, grade);
                _icon.SetClick(null);
            }
            else if (_product.rewards != null && _product.rewards.Count > 0)
            {
                // 수량 배지 '×5' (상품 이름에는 개수를 쓰지 않는다)
                _icon.SetWithInfo(_product.rewards[0]);
                if (_icon.Item != null) grade = _icon.Item.grade;
            }
            var glow = UITheme.GradeColor(grade);
            _iconGlow.color = new Color(glow.r, glow.g, glow.b, 0.45f);
        }

        void SetPrice()
        {
            var type = _product.currency;
            var sprite = _currencyIcons != null && (int)type < _currencyIcons.Length ? _currencyIcons[(int)type] : null;
            _priceIcon.sprite = sprite != null ? sprite : _priceFallback;
            _priceIcon.color = sprite != null ? UITheme.CurrencyIconTint(type) : UITheme.CurrencyColor(type);
            _priceText.text = RewardService.FormatAmount(type, _product.price);
        }

        /// <param name="instant">처음 채울 때 (품절 덮개를 서서히 띄우지 않는다)</param>
        void RefreshLimit(bool instant)
        {
            int remaining = Game.Shop.GetRemaining(_product);
            bool owned = _costume != null && Game.Costumes.IsUnlocked(_costume.id);

            if (owned) _limitText.text = "보유 중";
            else if (_costume != null) _limitText.text = "의상 · 1회 한정";
            else if (_product.dailyLimit > 0) _limitText.text = $"오늘 {Mathf.Max(0, remaining)}/{_product.dailyLimit}{OwnedSuffix()}";
            else if (_product.totalLimit > 0) _limitText.text = $"한정 {Mathf.Max(0, remaining)}/{_product.totalLimit}{OwnedSuffix()}";
            else _limitText.text = "언제든 구매" + OwnedSuffix();

            bool soldOut = owned || remaining == 0;
            bool appear = soldOut && !_shownSoldOut && !instant;
            _shownSoldOut = soldOut;
            _soldOut.SetActive(soldOut);
            _soldOutText.text = owned ? "보유 중" : "품절";
            if (_cost != null)
            {
                if (soldOut) _cost.SetState(CostButtonView.State.Unavailable);
                else _cost.SetAffordable(Game.Currency.Has(_product.currency, _product.price));
            }
            else _buyButton.interactable = !soldOut;

            if (_soldOutGroup == null) return;
            if (appear && !UIMotion.Reduced && isActiveAndEnabled) MenuFx.Restart(this, ref _fade, FadeSoldOut());
            else if (_fade == null) _soldOutGroup.alpha = 1f;
        }

        /// <summary>' · 보유 15' (재료 상품만 — 첫 보상의 가방 수)</summary>
        string OwnedSuffix()
        {
            var item = _icon != null ? _icon.Item : null;
            return item != null ? $" · 보유 {NumberFormatter.FormatInt(Game.Inventory.GetCount(item.id))}" : string.Empty;
        }

        IEnumerator FadeSoldOut()
        {
            yield return MenuFx.Fade(_soldOutGroup, 0f, 1f, SoldOutFade);
            _fade = null;
        }

        // ───────────── 사기 (M56) ─────────────

        void OnBuy()
        {
            if (_product == null || _locked) return;
            var product = _product;

            // 1. 먼저 확인 — 모자라면 부족 안내, 다른 이유는 그 문장
            if (!Game.Shop.CanBuy(product))
            {
                if (!Game.Currency.Has(product.currency, product.price))
                {
                    if (_cost != null) _cost.Deny();
                    // 단추 = 토스트 (1.5초에 한 번), 별조각 · 별빛 사탕 = 부족 팝업
                    UIManager.Instance.ShowShortfall(product.currency, product.price);
                    return;
                }
                bool owned = _costume != null && Game.Costumes.IsUnlocked(_costume.id);
                var reason = owned ? ShopBuyResult.AlreadyOwned
                    : Game.Shop.GetRemaining(product) == 0 ? ShopBuyResult.SoldOut : ShopBuyResult.NotFound;
                UIManager.Instance.ShowToast(ShopSystem.Describe(reason, product));
                return;
            }

            // 4. 의상: 쓰기 확인 → 의상 획득
            if (_costume != null)
            {
                Lock();
                UIManager.Instance.ShowSpendConfirm("의상 구매", $"{MenuText.Object(_costume.name)} 구매할까요?", product.currency, product.price, "구매",
                    BuyCostume, null, null, Unlock);
                return;
            }

            bool single = product.rewards != null && product.rewards.Count == 1;
            System.Action buy = single ? BuySingle : BuyBundle;
            // 별조각 가격은 한 번 더 묻는다 (오늘 묻지 않기 없음), 단추 · 별빛 사탕은 바로
            if (product.currency == CurrencyType.Gem)
            {
                Lock();
                UIManager.Instance.ShowSpendConfirm("구매 확인", $"{MenuText.Object(product.name)} 구매할까요?", product.currency, product.price, "구매",
                    buy, null, null, Unlock);
                return;
            }
            buy();
        }

        /// <summary>2. 보상 하나짜리: 팝업 없이 날리기 + 튕기기 + 토스트 + 바로 고치기 + 0.15초 잠금</summary>
        void BuySingle()
        {
            var product = _product;
            if (!TryBuy(product)) return;
            var reward = product.rewards[0];
            var iconRect = (RectTransform)_icon.transform;
            RewardFlyer.Fly(product.rewards, iconRect);
            UISfx.Play(UISfxKey.Purchase);
            UIManager.Instance.ShowToast($"{product.name} {RewardSlotView.CountLabel(reward.amount, false)} 획득 (보유 {HaveText(reward)})", ToastKind.Success);
            if (isActiveAndEnabled)
            {
                RefreshLimit(false);
                if (!UIMotion.Reduced) MenuFx.Restart(this, ref _punch, Punch());
                Lock();
                MenuFx.Restart(this, ref _lock, UnlockLater());
            }
        }

        /// <summary>3. 묶음: 보상 팝업 '구매 완료' (확인을 누르면 날아간다)</summary>
        void BuyBundle()
        {
            var product = _product;
            if (!TryBuy(product)) return;
            UISfx.Play(UISfxKey.Purchase);
            UIManager.Instance.Get<RewardPopup>().Show("구매 완료", product.name, product.rewards);
        }

        /// <summary>4. 의상: 보상 팝업 '의상 획득!' + (친구가 있으면) '바로 입혀 보기'</summary>
        void BuyCostume()
        {
            var product = _product;
            var costume = _costume;
            if (costume == null || !TryBuy(product)) return;
            UISfx.Play(UISfxKey.Purchase);
            var lineage = Game.Characters.Db.GetLineageOfCostume(costume.id);
            var popup = UIManager.Instance.Get<RewardPopup>();
            if (lineage != null && Game.Characters.IsOwned(lineage.id))
            {
                string lineageId = lineage.id;
                string costumeId = costume.id;
                popup.Show("의상 획득!", $"{MenuText.Object(costume.name)} 손에 넣었어요.", null, costume, true, "바로 입혀 보기",
                    () => TryOn(lineageId, costumeId));
            }
            else
            {
                string friend = lineage != null ? lineage.name : "그 친구";
                popup.Show("의상 획득!", $"{MenuText.Object(costume.name)} 손에 넣었어요.\n{MenuText.Object(friend)} 만나면 입힐 수 있어요.", null, costume);
            }
        }

        /// <summary>'바로 입혀 보기': 입히고 그 친구 상세의 의상 탭을 연다 (보상 팝업은 이미 닫혔다)</summary>
        static void TryOn(string lineageId, string costumeId)
        {
            Game.Costumes.TryEquip(lineageId, costumeId);
            UINav.OpenCharacter(lineageId, CharacterDetailPopup.TabCostume);
        }

        bool TryBuy(ShopProductDef product)
        {
            if (product == null || _shop == null) return false;
            var result = Game.Shop.TryBuy(_shop.id, product.id);
            if (result == ShopBuyResult.Ok) return true;
            if (result == ShopBuyResult.NotEnoughCurrency)
            {
                if (_cost != null) _cost.Deny();
                UIManager.Instance.ShowShortfall(product.currency, product.price);
            }
            else UIManager.Instance.ShowToast(ShopSystem.Describe(result, product));
            return false;
        }

        /// <summary>받은 뒤 보유 수 (재료 = 가방, 재화 = 지금 양)</summary>
        static string HaveText(RewardData reward) => reward.type == RewardType.Currency
            ? RewardService.FormatAmount(reward.currency, Game.Currency.Get(reward.currency))
            : NumberFormatter.FormatInt(Game.Inventory.GetCount(reward.itemId));

        void Lock() => _locked = true;

        void Unlock()
        {
            _locked = false;
            _lock = null;
        }

        IEnumerator UnlockLater()
        {
            yield return new WaitForSecondsRealtime(TapLock);
            Unlock();
        }

        /// <summary>카드 튕기기 1 → 1.06 (0.08초) → 1 (0.14초)</summary>
        IEnumerator Punch()
        {
            for (float t = 0f; t < PunchUp; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(1f, PunchScale, UIMotion.OutQuad(t / PunchUp));
                yield return null;
            }
            for (float t = 0f; t < PunchDown; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(PunchScale, 1f, UIMotion.OutCubic(t / PunchDown));
                yield return null;
            }
            transform.localScale = Vector3.one;
            _punch = null;
        }
    }
}
