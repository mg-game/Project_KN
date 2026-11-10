using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 의상 한 줄: 초상화 / 이름 / 감정 변화("외로움 → 위로") / 획득 방법 / 진행도 / 해금·장착·도감 보기.
    /// 행동 버튼은 상태마다 버튼 그림(보라 / 금 / 꺼짐)을 바꿔 끼우고, 색은 흰색 또는 옅은 틴트(35% 이하)만 쓴다 (그림이 없으면 PrimaryButton 단색).
    /// 갈아입으면 CostumeEquip 소리 (해금 소리는 팡파르 쪽 CostumeUnlock — AUDIO-SPEC §8.1).
    /// 의상이 잠긴 동안 (F-03, 2-5 전): 목록은 그대로 보이고, 장착 · 해금 버튼은 꺼진 그림 + '2-5 클리어 시' — 누르면 잠금 문장 토스트 + 약한 진동.
    /// </summary>
    public class CostumeItemView : MonoBehaviour
    {
        enum CostumeAction
        {
            None = 0,
            Equip = 1,
            Equipped = 2,
            Unlock = 3,
            Shop = 4,
            Locked = 5,
            /// <summary>의상 기능이 잠김 (FeatureId.Costumes — 누르면 잠금 안내)</summary>
            Gated = 6,
        }

        const string EventShopId = "shop_event";

        [SerializeField] Image _equippedFrame;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _emotionText;
        [SerializeField] TMP_Text _acquisitionText;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] Button _actionButton;
        [SerializeField] TMP_Text _actionLabel;
        [SerializeField] Button _galleryButton;
        [SerializeField] GameObject _equippedTag;
        [SerializeField] GameObject _lockIcon;
        [SerializeField] GameObject _alertDot;
        [SerializeField] SparkleBurst _burst;
        /// <summary>장착 / 해금 / 상점 버튼 그림 (button_primary_9s)</summary>
        [SerializeField] Sprite _primarySprite;
        /// <summary>장착 중 버튼 그림 (button_gold_9s)</summary>
        [SerializeField] Sprite _goldSprite;
        /// <summary>잠김 / 미보유 버튼 그림 (ui_button_disabled_9s)</summary>
        [SerializeField] Sprite _disabledSprite;

        LineageDef _lineage;
        CostumeDef _costume;
        CostumeAction _action;

        void Awake()
        {
            _actionButton.onClick.AddListener(OnAction);
            _galleryButton.onClick.AddListener(OnGallery);
        }

        /// <summary>기본 외형 (의상 벗기)</summary>
        public void SetDefault(LineageDef lineage)
        {
            _lineage = lineage;
            _costume = null;
            bool owned = CharacterUI.IsOwned(lineage);
            bool equipped = owned && string.IsNullOrEmpty(Game.Costumes.GetEquipped(lineage.id));
            var baseForm = CharacterUI.BaseForm(lineage);

            _portrait.Set(baseForm != null ? baseForm.portrait : null, lineage, lineage.name, owned ? PortraitTone.Normal : PortraitTone.Silhouette);
            _nameText.text = $"기본 {lineage.name}";
            _emotionText.text = "처음 만난 그 모습";
            _acquisitionText.text = string.IsNullOrEmpty(lineage.origin) ? "언제든 처음 모습으로 돌아갈 수 있어요." : $"{lineage.origin}. 언제든 처음 모습으로 돌아갈 수 있어요.";
            _progressText.text = owned ? "보유 중" : $"{lineage.name} 획득 필요";
            _galleryButton.gameObject.SetActive(false);
            _lockIcon.SetActive(!owned);
            _alertDot.SetActive(false);
            SetAction(!owned ? CostumeAction.None : equipped ? CostumeAction.Equipped : Gate(CostumeAction.Equip));
        }

        /// <summary>장착 · 해금은 의상 기능이 열린 뒤에만 (잠겼으면 Gated)</summary>
        static CostumeAction Gate(CostumeAction action) =>
            (action == CostumeAction.Equip || action == CostumeAction.Unlock) && !CharacterUI.IsUnlocked(FeatureId.Costumes) ? CostumeAction.Gated : action;

        public void Set(LineageDef lineage, CostumeDef costume)
        {
            _lineage = lineage;
            _costume = costume;
            bool owned = CharacterUI.IsOwned(lineage);
            bool unlocked = Game.Costumes.IsUnlocked(costume.id);
            bool equipped = owned && Game.Costumes.GetEquipped(lineage.id) == costume.id;
            bool canUnlock = owned && CharacterUI.IsCostumeUnlockable(costume);

            var baseForm = CharacterUI.BaseForm(lineage);
            var sprite = costume.portrait != null ? costume.portrait : baseForm != null ? baseForm.portrait : null;
            _portrait.Set(sprite, lineage, costume.name, unlocked ? PortraitTone.Normal : PortraitTone.Clue);

            _nameText.text = costume.name;
            _emotionText.text = costume.emotion;
            _acquisitionText.text = costume.acquisition;
            string progress = Game.Costumes.GetProgressText(costume.id);
            _progressText.text = costume.attackBonusPercent > 0f
                ? $"{progress}  <color={UITheme.ColorTag(CharacterUI.InkPositive)}>· 공격력 +{costume.attackBonusPercent:0.#}%</color>"
                : progress;
            _galleryButton.gameObject.SetActive(costume.illustration != null);
            _lockIcon.SetActive(!unlocked);
            _alertDot.SetActive(canUnlock);

            CostumeAction action;
            if (!owned) action = CostumeAction.None;
            else if (equipped) action = CostumeAction.Equipped;
            else if (unlocked) action = CostumeAction.Equip;
            else if (canUnlock) action = CostumeAction.Unlock;
            else if (costume.unlockType == CostumeUnlockType.EventShop) action = CostumeAction.Shop;
            else action = CostumeAction.Locked;
            SetAction(Gate(action));
        }

        void SetAction(CostumeAction action)
        {
            _action = action;
            _equippedTag.SetActive(action == CostumeAction.Equipped);
            _equippedFrame.gameObject.SetActive(action == CostumeAction.Equipped);

            var disabledText = CharacterUI.WithAlpha(UITheme.Text, 0.85f);
            // (글자, 버튼 그림, 그림 틴트, 그림 위 글자색, 그림이 없을 때 바탕색, 그때 글자색)
            (string label, Sprite art, Color tint, Color text, Color fallback, Color fallbackText) = action switch
            {
                CostumeAction.Equip => ("장착", _primarySprite, Color.white, Color.white, UITheme.PrimaryButton, Color.white),
                CostumeAction.Equipped => ("장착 중", _goldSprite, Color.white, UITheme.Ink, UITheme.GoldAccent, UITheme.Ink),
                CostumeAction.Unlock => ("해금", _primarySprite, Color.Lerp(Color.white, UITheme.DustyPink, 0.35f), Color.white, UITheme.DustyPink, UITheme.Ink),
                CostumeAction.Shop => ("상점 가기 ›", _primarySprite, Color.Lerp(Color.white, UITheme.EventToken, 0.35f), Color.white, UITheme.EventToken, UITheme.Ink),
                CostumeAction.Locked => ("잠김", _disabledSprite, Color.white, disabledText, UITheme.ParchmentDark, UITheme.InkSub),
                CostumeAction.Gated => (CharacterUI.LockedShort(CharacterUI.LockedHint(FeatureId.Costumes)), _disabledSprite, Color.white, disabledText,
                    UITheme.ParchmentDark, UITheme.InkSub),
                _ => ("미보유", _disabledSprite, Color.white, disabledText, UITheme.ParchmentDark, UITheme.InkSub),
            };
            _actionLabel.text = label;
            var image = _actionButton.targetGraphic as Image;
            if (image != null && art != null)
            {
                image.sprite = art;
                image.color = tint;
                _actionLabel.color = text;
            }
            else
            {
                _actionButton.targetGraphic.color = fallback;
                _actionLabel.color = fallbackText;
            }
            _actionButton.interactable = action != CostumeAction.None && action != CostumeAction.Locked;
        }

        void OnAction()
        {
            switch (_action)
            {
                case CostumeAction.Equip:
                    if (Game.Costumes.TryEquip(_lineage.id, _costume?.id))
                    {
                        string name = _costume != null ? _costume.name : $"기본 {_lineage.name}";
                        CharacterUI.Toast($"{CharacterUI.WithDirection(name)} 갈아입었어요!", ToastKind.Success);
                        UISfx.Play(UISfxKey.CostumeEquip);
                        _burst.Play(UITheme.Lavender, 10, 0.7f);
                    }
                    break;
                case CostumeAction.Equipped:
                    CharacterUI.Toast("지금 입고 있는 외형이에요.");
                    break;
                case CostumeAction.Unlock:
                    if (Game.Costumes.TryUnlock(_costume.id))
                    {
                        CharacterUI.Toast($"{KoreanText.WithSubject(_costume.name)} 해금됐어요! 바로 장착할 수 있어요.", ToastKind.Success);
                        _burst.Play(UITheme.GoldAccent, 16, 0.9f);
                    }
                    else
                    {
                        CharacterUI.Toast(Game.Costumes.GetProgressText(_costume.id));
                    }
                    break;
                case CostumeAction.Shop:
                    CharacterUI.CloseAllPopups();
                    UINav.GoToSource(ItemSourceType.Shop, EventShopId);
                    break;
                case CostumeAction.Gated:
                    CharacterUI.DenyLocked(FeatureId.Costumes);
                    break;
            }
        }

        void OnGallery()
        {
            if (_costume == null || _costume.illustration == null) return;
            UIManager.Instance.Get<IllustrationPopup>().Show(_costume.illustration, _costume.name);
        }
    }
}
