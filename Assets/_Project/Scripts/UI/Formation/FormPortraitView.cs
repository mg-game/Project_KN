using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 초상화 (장착 의상 반영). 아트가 없으면 둥근 받침(ui_plate_round)에 계보 색을 옅게 섞고 이름 첫 글자로 대신한다.
    /// 편성 카드, 방위 링/덱 칸, 원정 멤버, 강화 탭 친구 레벨 카드에서 같이 쓴다.
    /// </summary>
    public class FormPortraitView : MonoBehaviour
    {
        [SerializeField] Image _image;
        [SerializeField] TMP_Text _initialText;
        [SerializeField] Sprite _placeholder;

        float _alpha = 1f;
        Color _tint = Color.white;
        bool _hasArt;
        bool _silhouette;
        string _initial = "?";

        public Image Image => _image;

        /// <summary>형태 기준 (의상 이름/초상화 반영)</summary>
        public void SetForm(string formId)
        {
            var lineage = Game.Characters.Db.GetLineageOfForm(formId);
            Set(Game.Characters.GetPortrait(formId), lineage, Game.Characters.GetDisplayName(formId));
        }

        /// <summary>계보 기준 (기본형 + 장착 의상)</summary>
        public void SetLineage(LineageDef lineage)
        {
            Set(Game.Characters.GetPortrait(lineage.baseFormId), lineage, lineage.name);
        }

        public void Set(Sprite portrait, LineageDef lineage, string displayName)
        {
            bool hasArt = portrait != null;
            _hasArt = hasArt;
            _image.sprite = hasArt ? portrait : _placeholder;
            _image.preserveAspect = true;
            _tint = hasArt ? Color.white : Color.Lerp(Color.white, lineage != null ? lineage.mainColor : UITheme.Lavender, 0.4f);

            if (_initialText != null)
            {
                _initialText.gameObject.SetActive(!hasArt);
                _initial = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);
            }
            ApplyColor();
        }

        /// <summary>반투명 표시 (잔향이 칸, 파견 중 등)</summary>
        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
            ApplyColor();
        }

        /// <summary>아직 만나지 못한 친구: 어두운 실루엣 (첫 글자 대신 "?")</summary>
        public void SetSilhouette(bool silhouette)
        {
            _silhouette = silhouette;
            ApplyColor();
        }

        void ApplyColor()
        {
            var color = _silhouette ? (_hasArt ? Silhouette : UITheme.Secondary) : _tint;
            color.a *= _alpha;
            _image.color = color;
            if (_initialText != null)
            {
                var textColor = _silhouette ? UITheme.SubText : UITheme.Ink;
                textColor.a = _alpha;
                _initialText.color = textColor;
                _initialText.text = _silhouette ? "?" : _initial;
            }
        }

        static readonly Color Silhouette = new(0.2f, 0.15f, 0.36f, 0.92f);
    }
}
