using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스킬 아이콘 구슬: 어두운 메달 받침(ui_medallion_dark) + 스킬 연출 아트(Art/VFX/{키}) + 구슬 테(ui_skill_orb_frame).
    /// 받침과 테는 색이 있는 그림이라 흰색 그대로 두고, 테만 효과 계열 색을 30% 섞는다.
    /// 아트가 없으면 별 아이콘 + 스킬 이름 첫 글자로 대신한다.
    /// </summary>
    public class CharacterSkillOrb : MonoBehaviour
    {
        [SerializeField] Image _backdrop;
        [SerializeField] Image _ring;
        [SerializeField] Image _icon;
        [SerializeField] Image _fallback;
        [SerializeField] TMP_Text _letter;
        [SerializeField] Image _glow;
        [SerializeField] CharacterIconLibrary _icons;

        /// <summary>구슬 테에 섞는 효과 색 비율 (색 있는 아트는 0.35 이하로만 물들인다)</summary>
        const float RingTint = 0.3f;

        /// <param name="dim">미해금 형태의 스킬: 흐리게</param>
        public void Set(SkillDef skill, bool dim = false)
        {
            var accent = CharacterSkillUI.AccentColor(skill);
            var art = _icons != null ? _icons.Skill(skill) : null;
            bool hasArt = art != null;
            float alpha = dim ? 0.5f : 1f;

            // 받침 · 자리표시 색은 빌더가 정한 그대로 (아트 = 흰색) 두고 투명도만 바꾼다
            if (_backdrop != null) _backdrop.color = CharacterUI.WithAlpha(_backdrop.color, alpha);
            if (_ring != null) _ring.color = CharacterUI.WithAlpha(Color.Lerp(Color.white, accent, RingTint), alpha);
            if (_glow != null)
            {
                _glow.gameObject.SetActive(!dim);
                var pulse = _glow.GetComponent<SoftPulse>();
                if (pulse != null) pulse.SetColor(accent);
                else _glow.color = CharacterUI.WithAlpha(accent, _glow.color.a);
            }

            _icon.sprite = art;
            _icon.enabled = hasArt;
            _icon.color = CharacterUI.WithAlpha(Color.white, alpha);

            if (_fallback != null)
            {
                _fallback.gameObject.SetActive(!hasArt);
                _fallback.color = CharacterUI.WithAlpha(_fallback.color, alpha);
            }
            if (_letter != null)
            {
                _letter.gameObject.SetActive(!hasArt);
                _letter.text = CharacterSkillUI.HasSkill(skill) ? skill.name.Substring(0, 1) : "?";
                _letter.color = CharacterUI.WithAlpha(UITheme.Ink, alpha);
            }
        }
    }
}
