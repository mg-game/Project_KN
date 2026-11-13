using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 기억도의 형태 노드. 해금 = 원래 색 + "해금", 단서 발견 = 흐릿한 초상화 + 빛 + "단서 발견",
    /// 미발견 = 검은 실루엣 + 자물쇠 + "미발견".
    /// 고리(ui_ring_stitch)와 상태 칩(ui_pill_white_9s)은 흰색 틴트용 아트라 상태 색을 그대로 입힌다.
    /// 미발견 고리 · 칩 바탕은 글자가 아닌 선 · 칠이라 InkLine (M29), 글자는 InkSub. '해금' 칩은 흰 글자가 읽히는 PrimaryButton.
    /// </summary>
    public class EvolutionNodeView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] CanvasGroup _group;
        [SerializeField] RectTransform _body;
        [SerializeField] Image _glow;
        [SerializeField] Image _ring;
        [SerializeField] Image _focusRing;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] GameObject _lock;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Image _stateChip;
        [SerializeField] TMP_Text _stateText;
        [SerializeField] Image _roleBadge;
        [SerializeField] Image _roleIcon;
        [SerializeField] TMP_Text _roleLetter;
        [SerializeField] Sprite _guardianIcon;
        [SerializeField] Sprite _echoIcon;
        [SerializeField] GameObject _alertDot;
        [SerializeField] SparkleBurst _burst;

        CharacterFormDef _form;
        Action<CharacterFormDef> _onClick;
        Coroutine _anim;

        public CharacterFormDef Form => _form;

        void Awake() => _button.onClick.AddListener(OnClick);

        void OnDisable()
        {
            _anim = null;
            if (_body != null) _body.localScale = Vector3.one;
        }

        public void Set(CharacterFormDef form, LineageDef lineage, EvolutionState state, bool craftable, Action<CharacterFormDef> onClick)
        {
            _form = form;
            _onClick = onClick;

            var tone = state switch
            {
                EvolutionState.Unlocked => PortraitTone.Normal,
                EvolutionState.ClueFound => PortraitTone.Clue,
                _ => PortraitTone.Silhouette,
            };
            _portrait.Set(CharacterUI.Portrait(form), lineage, form.name, tone);
            _lock.SetActive(state == EvolutionState.Undiscovered);

            _nameText.text = form.name;
            _nameText.color = state == EvolutionState.Undiscovered ? UITheme.InkSub : UITheme.Ink;
            _ring.color = state switch
            {
                EvolutionState.Unlocked => UITheme.GoldAccent,
                EvolutionState.ClueFound => craftable ? UITheme.GoldAccent : UITheme.Lavender,
                _ => CharacterUI.WithAlpha(UITheme.InkLine, 0.45f),
            };

            bool glowing = state == EvolutionState.ClueFound;
            _glow.gameObject.SetActive(glowing);
            if (glowing) _glow.color = CharacterUI.WithAlpha(craftable ? UITheme.GoldAccent : UITheme.Lavender, _glow.color.a);

            SetChip(form, state, craftable);

            // 역할 아이콘 아트가 있으면 그대로(흰색) 그리고, 없을 때만 역할 색 받침 + 첫 글자로 대신한다
            var role = form.recommendedRole;
            var icon = role == CharacterRole.Guardian ? _guardianIcon : _echoIcon;
            bool hasIcon = icon != null;
            _roleBadge.enabled = !hasIcon;
            _roleBadge.color = CharacterUI.RoleColor(role);
            _roleIcon.sprite = icon;
            _roleIcon.color = Color.white;
            _roleIcon.enabled = hasIcon;
            _roleLetter.gameObject.SetActive(!hasIcon);
            _roleLetter.text = role == CharacterRole.Guardian ? "지" : "잔";

            _alertDot.SetActive(craftable);
        }

        void SetChip(CharacterFormDef form, EvolutionState state, bool craftable)
        {
            string text;
            Color background;
            Color foreground = UITheme.Ink;
            if (form.tier == 0)
            {
                text = "기본";
                background = UITheme.SoftBlue;
            }
            else if (craftable)
            {
                text = "제작 가능";
                background = UITheme.GoldAccent;
            }
            else
            {
                text = UITheme.EvolutionStateName(state);
                switch (state)
                {
                    case EvolutionState.Unlocked:
                        background = UITheme.PrimaryButton;
                        foreground = Color.white;
                        break;
                    case EvolutionState.ClueFound:
                        background = UITheme.DustyPink;
                        break;
                    default:
                        background = CharacterUI.WithAlpha(UITheme.InkLine, 0.22f);
                        foreground = UITheme.InkSub;
                        break;
                }
            }
            _stateText.text = text;
            _stateText.color = foreground;
            _stateChip.color = background;
        }

        /// <summary>역할 필터 (-1 = 전체). 추천 역할이 다르면 흐리게, 맞으면 역할 색 테두리로 강조한다.</summary>
        public void SetFilter(int filter)
        {
            bool active = filter >= 0;
            bool match = !active || (int)_form.recommendedRole == filter;
            _group.alpha = match ? 1f : 0.28f;
            _focusRing.gameObject.SetActive(active && match);
            if (active && match) _focusRing.color = CharacterUI.WithAlpha(CharacterUI.RoleColor(_form.recommendedRole), _focusRing.color.a);
        }

        /// <summary>새로 해금된 노드 연출</summary>
        public void PlayUnlock()
        {
            if (!isActiveAndEnabled) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(UnlockRoutine());
        }

        IEnumerator UnlockRoutine()
        {
            _burst.Play(UITheme.GoldAccent, 14, 0.8f);
            yield return CharacterMotion.Pop(_body, 0.36f, 0.4f);
            _burst.Play(UITheme.Lavender, 8, 0.7f);
            yield return CharacterMotion.Punch(_body, 0.08f, 0.3f);
            _anim = null;
        }

        void OnClick()
        {
            if (_form != null) _onClick?.Invoke(_form);
        }
    }
}
