using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 상세의 정보 탭: (보유 계보) 호감도 칸 (CR-10, AffinitySectionView — 맨 위) / 이야기(원래 사물·성격) / 프로필(종족·체형·특성·컬러) / 기본형 능력치.
    /// 새 직렬화 필드(_affinity)는 비어 있어도 된다 (다시 빌드하기 전 씬 — 호감도 칸 없이 예전 그대로).
    /// </summary>
    public class CharacterInfoView : MonoBehaviour
    {
        [Tooltip("호감도 칸 (CR-10 — 없어도 됨)")]
        [SerializeField] AffinitySectionView _affinity;
        [SerializeField] TMP_Text _storyText;
        [SerializeField] TMP_Text _profileText;
        [SerializeField] Image[] _swatches;
        [SerializeField] TMP_Text _statsTitle;
        [SerializeField] TMP_Text _statsText;

        readonly StringBuilder _sb = new();

        public void Show(LineageDef lineage)
        {
            bool owned = CharacterUI.IsOwned(lineage);
            if (_affinity != null) _affinity.Show(lineage);
            _storyText.text = string.IsNullOrEmpty(lineage.origin)
                ? lineage.personality
                : $"<b>{lineage.origin}</b>\n{lineage.personality}";

            _sb.Clear();
            Row("종족", lineage.species);
            Row("체형", CharacterUI.BodyTypeName(lineage.bodyType));
            Row("특성", CharacterUI.TraitList(lineage, true));
            Row("등급", $"{CharacterUI.StarsText(lineage.grade)} {UITheme.GradeName(lineage.grade)}");
            if (owned)
            {
                Row("계보 공명", $"Lv.{Game.Characters.GetResonanceLevel(lineage.id)}  (능력치 +{Game.Characters.GetResonanceBonusPercent(lineage.id):0.#}%)");
                Row("진화", $"{CharacterUI.CountUnlockedForms(lineage)} / {lineage.forms.Count} 해금");
            }
            else
            {
                Row("상태", "아직 만나지 못했어요");
            }
            _profileText.text = _sb.ToString();

            var colors = new[] { lineage.mainColor, lineage.subColor, lineage.accentColor, lineage.neutralColor };
            for (int i = 0; i < _swatches.Length; i++)
            {
                _swatches[i].gameObject.SetActive(i < colors.Length);
                if (i < colors.Length) _swatches[i].color = CharacterUI.WithAlpha(colors[i], 1f);
            }

            ShowStats(lineage, owned);
        }

        void ShowStats(LineageDef lineage, bool owned)
        {
            var form = CharacterUI.BaseForm(lineage);
            if (form == null)
            {
                _statsTitle.text = "능력치";
                _statsText.text = string.Empty;
                return;
            }
            var stats = Game.Characters.GetStats(form.id);
            _statsTitle.text = owned ? $"{form.name} 능력치 · Lv.{Game.Characters.GetLevel(lineage.id)}" : $"{form.name} 능력치 · Lv.1 기준";

            _sb.Clear();
            Pair("공격력", stats.attack.ToString("N0"), "체력", stats.hp.ToString("N0"));
            Pair("공격 간격", $"{stats.attackInterval:0.##}초", "전투력", stats.Power.ToString("N0"));
            Pair("요격 반경", $"{stats.range:0.##}", "요격 각도", $"{stats.arcDegrees:0}°");
            Pair("소환 비용", $"잔향 {stats.summonCost}", "유지 시간", $"{stats.lifetime:0.#}초");
            _sb.Append($"<color={UITheme.ColorTag(UITheme.InkSub)}><size=85%>{CharacterUI.StyleHint(form.style)}</size></color>");
            _statsText.text = _sb.ToString();
        }

        void Row(string label, string value) =>
            _sb.Append($"<color={UITheme.ColorTag(UITheme.InkSub)}>{label}</color><pos=26%><b>{value}</b>\n");

        void Pair(string labelA, string valueA, string labelB, string valueB)
        {
            string sub = UITheme.ColorTag(UITheme.InkSub);
            _sb.Append($"<color={sub}>{labelA}</color><pos=22%><b>{valueA}</b><pos=52%><color={sub}>{labelB}</color><pos=74%><b>{valueB}</b>\n");
        }
    }
}
