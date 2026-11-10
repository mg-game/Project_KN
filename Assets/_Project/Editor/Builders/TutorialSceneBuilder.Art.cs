using System;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 튜토리얼 & 팡파르 빌더의 아트 묶음. 모두 Higgsfield 아트(Art/UI, Art/FX, Art/Icons)를 UiArt로 찾는다.
    /// 아트가 아직 임포트되지 않았으면 UiArt가 경고를 한 번 남기고 단순 도형(CircleSprite/RoundSprite)으로 대신한다 (빌드가 깨지지 않게).
    /// </summary>
    internal static partial class TutorialSceneBuilder
    {
        /// <summary>빌더가 쓰는 스프라이트 (없으면 경고 + 단순 도형)</summary>
        sealed class TutorialArt
        {
            /// <summary>UI/tutorial_finger: 손끝이 그림 위쪽 가운데</summary>
            public Sprite Finger;
            /// <summary>UI/tutorial_highlight_9s: 코치마크 테두리 (흰 크림, Lamp로 물들인다)</summary>
            public Sprite Outline;
            /// <summary>FX/rays: 축하 빛살 (흰색)</summary>
            public Sprite Rays;
            /// <summary>FX/fx_sparkle: 별빛 (흰색)</summary>
            public Sprite Spark;
            /// <summary>FX/fx_ring_thread: 레벨업 고리 (흰 크림)</summary>
            public Sprite Ring;
            /// <summary>UI/ui_ring_stitch: 손가락 물결 고리 (흰색)</summary>
            public Sprite Ripple;
            /// <summary>FX/glow_soft: 부드러운 빛 번짐</summary>
            public Sprite Glow;
            /// <summary>FX/fx_trail: 끌기 안내 별똥 꼬리 (머리가 오른쪽)</summary>
            public Sprite Trail;
            /// <summary>FX/fx_confetti_*: 색종이 (흰색, 팔레트로 물들인다)</summary>
            public Sprite[] Confetti;
            /// <summary>UI/ui_plate_round: 초상화·아이콘 받침</summary>
            public Sprite Plate;
            /// <summary>UI/ui_bubble_tail: 말풍선 꼬리 (아래를 가리킴, 위쪽 변에 외곽선 없음)</summary>
            public Sprite BubbleTail;
            /// <summary>UI/ui_pip_button: 장 표시 점 (흰색)</summary>
            public Sprite Pip;
            public Sprite Gold;
            public Sprite Gem;
            public Sprite EventToken;
            public Sprite QuickTicket;
            public Sprite DoubleTicket;
            public Sprite Shards;
            public Sprite Star;
            public Sprite Shop;
            public Sprite Night;
            public Sprite Jjak;

            public static TutorialArt Load()
            {
                var confetti = UiArt.Confetti;
                return new TutorialArt
                {
                    Finger = UiArt.UI("tutorial_finger", CircleSprite),
                    Outline = UiArt.UI("tutorial_highlight_9s", RoundSprite),
                    Rays = UiArt.Get("FX/rays.png", CircleSprite),
                    Spark = UiArt.Sparkle,
                    Ring = UiArt.RingThread,
                    Ripple = UiArt.UI("ui_ring_stitch", CircleSprite),
                    Glow = Or(ArtLib.FX("glow_soft"), CircleSprite),
                    Trail = UiArt.Trail,
                    // 색종이 4종 (리본·단추·별·실). 하나도 없으면 둥근 사각 하나
                    Confetti = confetti != null && confetti.Length > 0 ? confetti : new[] { RoundSprite },
                    Plate = UiArt.UI("ui_plate_round", CircleSprite),
                    BubbleTail = UiArt.UI("ui_bubble_tail", null),
                    Pip = UiArt.UI("ui_pip_button", CircleSprite),
                    Gold = ArtLib.Currency(CurrencyType.Gold),
                    Gem = ArtLib.Currency(CurrencyType.Gem),
                    EventToken = ArtLib.Currency(CurrencyType.EventToken),
                    QuickTicket = UiArt.Get("Icons/ticket_quick.png", ArtLib.Icon("lamp")),
                    DoubleTicket = UiArt.Get("Icons/ticket_double.png", ArtLib.Icon("star")),
                    Shards = ArtLib.Icon("shards"),
                    Star = UiArt.Get("Icons/star.png", CircleSprite),
                    Shop = ArtLib.Battle("shop"),
                    Night = ArtLib.UI("bg_night"),
                    Jjak = ArtLib.Character("jjak_base"),
                };
            }
        }

        static Sprite Or(Sprite first, Sprite second) => first != null ? first : second;

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        // ───────────────────────────── 작은 조각 ─────────────────────────────

        /// <summary>
        /// 9-slice 아트 한 장 (흰색 그대로). ppu = 테두리 배율 (그려지는 테두리 = 48 / ppu).
        /// 아트가 없으면 경고하고 fallbackColor의 둥근 사각형.
        /// </summary>
        static Image SlicedArt(Transform parent, string name, string uiName, float ppu, Color fallbackColor, bool raycast = false)
        {
            var art = UiArt.UI(uiName, RoundSprite);
            if (!UiArt.IsArt(art)) return Img(parent, name, fallbackColor, RoundSprite, raycast);
            var image = Img(parent, name, Color.white, art, raycast);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ppu;
            return image;
        }

        /// <summary>
        /// 금색 버튼 (M25 — 버튼 공장 SceneParts.SkinButton(Gold) 한 줄): button_gold_9s 9-slice, 비활성 그림 교체 · 이름 흐림(ButtonLabelDim)까지 공장과 같다.
        /// 아트가 없으면 UITheme.Gold 단색. 테두리 배율은 높이에 맞춘다 (90~120 → 2, 60~90 → 2.8, 그 아래 3.5). 이름은 Ink 굵게 (StyleLabel)
        /// </summary>
        static Button GoldButton(Transform parent, string name, string label, Vector2 size, float fontSize = 36)
        {
            var button = Btn(parent, name, label, SceneParts.FlatColor(SceneParts.ButtonStyle.Gold), fontSize, out var text);
            SceneParts.SkinButton(button, SceneParts.ButtonStyle.Gold, size.y >= 90f ? 2f : size.y >= 60f ? 2.8f : 3.5f);
            SceneParts.StyleLabel(text, SceneParts.ButtonStyle.Gold);
            ((RectTransform)button.transform).sizeDelta = size;
            return button;
        }

        /// <summary>그림 한 장 (비율 유지). 그림이 없으면 이미지를 꺼 둔다 (런타임이 채우면 다시 켠다)</summary>
        static Image Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = Img(parent, name, Color.white, sprite);
            image.preserveAspect = true;
            image.enabled = sprite != null;
            image.rectTransform.At(Center, position, size);
            return image;
        }

        /// <summary>둥근 받침 (ui_plate_round, 흰색 그대로)</summary>
        static Image Plate(Transform parent, string name, TutorialArt art)
        {
            var plate = Img(parent, name, UiArt.IsArt(art.Plate) ? Color.white : WithAlpha(UITheme.Lavender, 0.55f), art.Plate);
            plate.preserveAspect = true;
            return plate;
        }

        /// <summary>
        /// 동화 인트로 그림의 꼭 남길 기둥 (uv): 19.5:9 화면에서 보이는 가운데 58% — 아주 길쭉한 화면(2.4:1 · 폴드 겉화면)에서도
        /// intro_1의 바깥 두 친구가 잘리지 않는다 (aspect AF-5, D8). 남는 위아래는 StoryIntro 뒤판(밤색)
        /// </summary>
        static readonly Rect IntroKeep = new(0.21f, 0f, 0.58f, 1f);

        /// <summary>화면을 꽉 채우는 배경 그림 (비율 유지, 넘치는 부분은 잘림 — BackgroundFit Cover, keep = 꼭 남길 uv 영역)</summary>
        static Image Backdrop(Transform parent, string name, Sprite sprite, Color tint, Rect keep = default)
        {
            var image = Img(parent, name, sprite != null ? tint : UITheme.Background, sprite);
            image.rectTransform.Stretch();
            if (sprite != null)
            {
                image.gameObject.AddComponent<BackgroundFit>()
                    .Configure(BackgroundFitMode.Cover, new Vector2(0.5f, 0.5f), sprite.rect.width / Mathf.Max(1f, sprite.rect.height))
                    .SetKeep(keep);
            }
            return image;
        }

        /// <summary>띠 모양: 밤색 캡슐(칩), 금색·라벤더 리본(제목)</summary>
        enum PillStyle
        {
            /// <summary>ui_pill_night_9s (192x96 캡슐, ppu = 96 / 높이)</summary>
            Night,
            /// <summary>ui_ribbon_gold_9s (512x160, ppu = 160 / 높이)</summary>
            GoldRibbon,
            /// <summary>ui_ribbon_lavender_9s (512x160, ppu = 160 / 높이)</summary>
            LavenderRibbon,
        }

        /// <summary>
        /// 띠 + 글자 (리본·칩). 글자는 띠의 앞면 안쪽에 놓인다. 자동 크기 fontSize의 70% ~ fontSize, 하한 16 (M23 — UIKit.FitFloor)
        /// </summary>
        static Image Pill(Transform parent, string name, string text, float fontSize, PillStyle style, Color textColor,
            Vector2 position, Vector2 size, out TextMeshProUGUI label)
        {
            var bg = PillBackground(parent, name, style, size.y);
            bg.rectTransform.At(Center, position, size);
            label = Txt(PillFace(bg, style, size.y), "Text", text, fontSize, textColor, TextAlignmentOptions.Center, true);
            label.rectTransform.Stretch();
            label.FitFloor(Mathf.Max(UIType.AutoSizeFloor, fontSize * 0.7f), fontSize);
            return bg;
        }

        /// <summary>띠 그림만 (크기·위치는 부르는 쪽에서). height = 그려질 높이 (ppu 계산용)</summary>
        static Image PillBackground(Transform parent, string name, PillStyle style, float height)
        {
            height = Mathf.Max(1f, height);
            return style switch
            {
                PillStyle.GoldRibbon => SlicedArt(parent, name, "ui_ribbon_gold_9s", 160f / height, UITheme.Gold),
                PillStyle.LavenderRibbon => SlicedArt(parent, name, "ui_ribbon_lavender_9s", 160f / height, WithAlpha(UITheme.Lavender, 0.9f)),
                _ => SlicedArt(parent, name, "ui_pill_night_9s", 96f / height, new Color(0.09f, 0.07f, 0.2f, 0.94f)),
            };
        }

        /// <summary>
        /// 글자를 놓을 앞면 (띠의 자식). 리본은 양옆 꼬리와 아래 말림을 피한다:
        /// 앞면 = 위 6% ~ 아래 24%, 옆은 꼬리(아트 80px) 안쪽.
        /// </summary>
        static RectTransform PillFace(Image background, PillStyle style, float height)
        {
            var face = NewRect("Face", background.transform);
            if (style == PillStyle.Night || !UiArt.IsArt(background.sprite))
            {
                face.Stretch(Mathf.Max(14f, height * 0.4f), Mathf.Max(14f, height * 0.4f), 0, 0);
                return face;
            }
            float side = 80f / (160f / height) + 6f;
            face.Stretch(side, side, height * 0.06f, height * 0.24f);
            return face;
        }

        /// <summary>bool 직렬화 필드 (Binder는 참조/정수만 받는다)</summary>
        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
