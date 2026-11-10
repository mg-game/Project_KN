using System.Text;
using TMPro;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 스킬 카드 (크림 카드): 스킬 아이콘 구슬 / 이름 / 발동 시점 칩(+ 잔향이 전용) / 설명 / 대상·효과 요약 / 시전 대사.
    /// 캐릭터 상세(스킬 탭), 형태 정보, 진화 레시피("진화하면 얻는 스킬")에서 쓴다.
    /// </summary>
    public class CharacterSkillCard : MonoBehaviour
    {
        [SerializeField] CharacterSkillOrb _orb;
        [SerializeField] TMP_Text _captionText;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] CharacterChip _triggerChip;
        [SerializeField] CharacterChip _echoOnlyChip;
        [SerializeField] TMP_Text _descText;
        [SerializeField] TMP_Text _effectsText;
        [SerializeField] GameObject _quote;
        [SerializeField] TMP_Text _quoteText;

        readonly StringBuilder _sb = new();

        /// <param name="caption">카드 머리 문구. 예) "짝짝이의 고유 스킬", "진화하면 얻는 스킬"</param>
        /// <param name="dim">아직 쓸 수 없는 스킬 (미해금 형태): 흐리게</param>
        public void Show(SkillDef skill, string caption, bool dim = false)
        {
            _captionText.text = caption;
            _orb.Set(skill, dim);

            if (!CharacterSkillUI.HasSkill(skill))
            {
                _nameText.text = "아직 알려지지 않은 스킬";
                _nameText.color = UITheme.InkSub;
                _triggerChip.gameObject.SetActive(false);
                _echoOnlyChip.gameObject.SetActive(false);
                _descText.text = "이 친구의 스킬 이야기는 곧 채워질 거예요.";
                _effectsText.text = string.Empty;
                _quote.SetActive(false);
                return;
            }

            _nameText.text = skill.name;
            _nameText.color = dim ? UITheme.InkSub : UITheme.Ink;

            _triggerChip.gameObject.SetActive(true);
            _triggerChip.Set(CharacterSkillUI.TriggerText(skill), CharacterSkillUI.TriggerColor(skill.trigger), UITheme.Ink);
            bool echoOnly = CharacterSkillUI.IsEchoOnly(skill.trigger);
            _echoOnlyChip.gameObject.SetActive(echoOnly);
            if (echoOnly) _echoOnlyChip.SetRole(CharacterRole.Echo, " 전용");

            _descText.text = string.IsNullOrEmpty(skill.description) ? string.Empty : skill.description;
            _descText.color = dim ? UITheme.InkSub : UITheme.Ink;
            _effectsText.text = CharacterSkillUI.EffectsLine(skill, _sb);

            string quote = CharacterSkillUI.Quote(skill);
            _quote.SetActive(quote.Length > 0);
            _quoteText.text = quote;
        }
    }
}
