using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>초상화 표시 상태</summary>
    public enum PortraitTone
    {
        /// <summary>원래 색</summary>
        Normal = 0,
        /// <summary>단서 발견 / 미해금: 흐릿하게</summary>
        Clue = 1,
        /// <summary>미보유 / 미발견: 검은 실루엣</summary>
        Silhouette = 2,
    }

    /// <summary>초상화 뒤 배경(portrait_bg / ui_plate_round 아트) 물들이기 방식. 아트가 밝으므로 계보 색은 살짝만 섞는다.</summary>
    public enum PortraitBackdrop
    {
        None = 0,
        /// <summary>밤 패널 위: 계보 메인 색을 35% 섞고 조금 어둡게 (x0.85)</summary>
        Night = 1,
        /// <summary>크림 카드 위: 계보 메인 색을 25% 섞어 밝게</summary>
        Light = 2,
    }

    /// <summary>
    /// 캐릭터 초상화. 계보 색으로 살짝 물들인 배경 아트(네모 = portrait_bg, 원 = ui_plate_round) 위에 아트를 그리고,
    /// 아트가 아직 없으면 계보 색으로 물들인 둥근 받침 + 이름 첫 글자로 대신한다.
    /// 배경 아트는 밝은 그림이므로 흰색 쪽으로만 물들이고 어두운 패널 색과 섞지 않는다 (섞으면 그림이 탁해진다).
    /// 실루엣 받침 · 배경은 글자가 아닌 칠이라 InkLine으로 가라앉힌다 (M29 — InkSub가 진해져도 실루엣 무게는 그대로).
    /// </summary>
    public class FriendPortrait : MonoBehaviour
    {
        [SerializeField] Image _image;
        [SerializeField] Image _backdrop;
        [SerializeField] Image _placeholder;
        [SerializeField] TMP_Text _initial;
        [SerializeField] PortraitBackdrop _backdropStyle = PortraitBackdrop.Night;

        public Image Image => _image;

        public void Set(Sprite sprite, LineageDef lineage, string displayName, PortraitTone tone)
        {
            var main = lineage != null ? lineage.mainColor : UITheme.Lavender;
            bool hasArt = sprite != null;

            _image.sprite = sprite;
            _image.enabled = hasArt;
            _image.color = tone switch
            {
                PortraitTone.Silhouette => CharacterUI.SilhouetteTint,
                PortraitTone.Clue => CharacterUI.ClueTint,
                _ => Color.white,
            };

            if (_placeholder != null)
            {
                _placeholder.gameObject.SetActive(!hasArt);
                _placeholder.color = tone switch
                {
                    PortraitTone.Silhouette => Shade(Color.Lerp(Color.white, UITheme.InkLine, 0.5f), 0.55f),
                    PortraitTone.Clue => Shade(Color.Lerp(Color.white, main, 0.25f), 0.85f),
                    _ => Shade(Color.Lerp(Color.white, main, 0.45f), 1f),
                };
            }
            if (_initial != null)
            {
                _initial.gameObject.SetActive(!hasArt);
                _initial.text = tone == PortraitTone.Silhouette || string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);
                _initial.color = tone == PortraitTone.Silhouette ? UITheme.SubText : UITheme.Ink;
            }
            if (_backdrop != null) _backdrop.color = BackdropColor(main, tone);
        }

        /// <summary>배경 아트 색: 흰색 → 계보 색으로 조금만 (밤 35% x0.85 / 크림 25%). 단서 = 더 옅게, 실루엣 = 회보라로 가라앉힌다.</summary>
        Color BackdropColor(Color main, PortraitTone tone)
        {
            switch (_backdropStyle)
            {
                case PortraitBackdrop.Light:
                    return tone switch
                    {
                        PortraitTone.Silhouette => Shade(Color.Lerp(Color.white, UITheme.InkLine, 0.3f), 0.9f),
                        PortraitTone.Clue => Shade(Color.Lerp(Color.white, main, 0.15f), 0.95f),
                        _ => Shade(Color.Lerp(Color.white, main, 0.25f), 1f),
                    };
                case PortraitBackdrop.Night:
                    return tone switch
                    {
                        PortraitTone.Silhouette => Shade(Color.Lerp(Color.white, UITheme.InkLine, 0.35f), 0.72f),
                        PortraitTone.Clue => Shade(Color.Lerp(Color.white, main, 0.2f), 0.75f),
                        _ => Shade(Color.Lerp(Color.white, main, 0.35f), 0.85f),
                    };
                default:
                    return Color.clear;
            }
        }

        /// <summary>밝기만 곱하고 알파는 1로 (Color * float는 알파까지 곱한다)</summary>
        static Color Shade(Color color, float brightness) =>
            new(color.r * brightness, color.g * brightness, color.b * brightness, 1f);
    }
}
