using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 넓은 화면의 양옆 (aspect AF-2): 세로 놀이 틀(PlayFrame) 밖 왼쪽 · 오른쪽을 덮는 장식. 맨 색 띠 대신 —
    /// 밤 마을 그림(캔버스 Background 그림, 덮어 맞춤 · 좌우 대칭 · 곱하기로 어둡게) + 틀 쪽 그림자 + 금실 한 줄 (아트 바이블의 "꿰맨 실").
    /// <para>
    /// 캔버스 맨 뒤 형제(맨 위)라 틀 밖으로 넘친 것(덮어 맞춘 인트로 그림의 양옆 · 효과 · 날아가는 보상)을 가리고,
    /// 그림(Art)이 누름을 받아 버린다 — 틀 밖은 아무것도 눌리지 않는다 ("아무 곳이나 눌러 넘기기"처럼 Input을 직접 보는 쪽은 그대로 받는다).
    /// 팝업 · 덮개 · 잠자기 어둡게는 틀 안만 덮는다 (양옆은 이미 어둡다).
    /// </para>
    /// CanvasScalerAdapter가 처음 틀이 필요할 때 만든다 (폰은 만들지도 않는다). 그 뒤로는 기둥이 바뀔 때만 Apply — 매 프레임 일 없음.
    /// 옆 폭이 아주 좁으면(화면 12px 미만) 그림자 · 금실은 숨긴다.
    /// </summary>
    public class PlayFrameSides : MonoBehaviour
    {
        public const string ObjectName = "PlayFrameSides";

        /// <summary>그림 곱하기 색 (어둡게, 살짝 보라 — 틀 안이 언제나 가장 밝다)</summary>
        static readonly Color ArtTint = new(0.5f, 0.47f, 0.6f, 1f);
        /// <summary>그림 없을 때 판 색 (UITheme.Background)</summary>
        static readonly Color FallbackTint = new(0.09f, 0.07f, 0.18f, 1f);
        /// <summary>틀 쪽 그림자 (UITheme.Background 계열, 틀 가장자리에서 가장 진하다)</summary>
        static readonly Color ShadeColor = new(0.03f, 0.02f, 0.08f, 0.75f);
        /// <summary>금실 (UITheme.GoldAccent E8BE6A, 반투명)</summary>
        static readonly Color SeamColor = new(0.91f, 0.745f, 0.416f, 0.5f);
        const float ShadeWidth = 140f;
        const float SeamWidth = 3f;
        const float MinDecorPixels = 12f;
        /// <summary>왼쪽 그림의 자르는 자리 (오른쪽 = x 거울). x 0.85 = 시계탑 쪽 별하늘 (좁은 옆에서 달이 반쯤 잘리지 않게), y 0.3 = 아래 마을을 남김</summary>
        static readonly Vector2 LeftFocus = new(0.85f, 0.3f);

        [SerializeField] RectTransform _left;
        [SerializeField] RectTransform _right;
        [SerializeField] GameObject[] _decor;

        static Sprite _fade;

        /// <summary>캔버스 맨 뒤에 양옆을 만든다 (꺼진 채). art가 없으면 판 색만</summary>
        public static PlayFrameSides Create(RectTransform canvas, Sprite art)
        {
            var root = NewRect(ObjectName, canvas);
            Stretch(root);
            root.SetAsLastSibling();
            var sides = root.gameObject.AddComponent<PlayFrameSides>();
            var decor = new GameObject[4];
            sides._left = BuildSide(root, "Left", art, false, decor, 0);
            sides._right = BuildSide(root, "Right", art, true, decor, 2);
            sides._decor = decor;
            root.gameObject.SetActive(false);
            return sides;
        }

        /// <summary>기둥(화면 비율)에 맞춰 양옆을 놓는다. screenWidth = 화면 픽셀 폭 (좁은 옆 판단)</summary>
        public void Apply(Vector2 column, float screenWidth)
        {
            SetX(_left, 0f, column.x);
            SetX(_right, column.y, 1f);
            float sidePixels = Mathf.Min(column.x, 1f - column.y) * screenWidth;
            bool decor = sidePixels >= MinDecorPixels;
            if (_decor == null) return;
            foreach (var go in _decor)
            {
                if (go != null && go.activeSelf != decor) go.SetActive(decor);
            }
        }

        static void SetX(RectTransform rt, float min, float max)
        {
            if (rt == null) return;
            var aMin = rt.anchorMin;
            var aMax = rt.anchorMax;
            if (Mathf.Approximately(aMin.x, min) && Mathf.Approximately(aMax.x, max)) return;
            rt.anchorMin = new Vector2(min, 0f);
            rt.anchorMax = new Vector2(max, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 한쪽: RectMask2D 판(넘치는 그림을 자른다) + Art(덮어 맞춤, 누름을 받는다) + Shade(틀 쪽 그림자) + Seam(틀 가장자리 금실).
        /// 오른쪽은 그림만 localScale.x = −1로 뒤집고 자르는 자리도 x 거울 — 왼쪽과 정확히 대칭. 그림자는 같은 그라데이션을 뒤집어 쓴다.
        /// </summary>
        static RectTransform BuildSide(RectTransform root, string name, Sprite art, bool mirror, GameObject[] decor, int decorIndex)
        {
            var side = NewRect(name, root);
            side.anchorMin = new Vector2(mirror ? 1f : 0f, 0f);
            side.anchorMax = new Vector2(mirror ? 1f : 0f, 1f);
            side.offsetMin = Vector2.zero;
            side.offsetMax = Vector2.zero;
            side.gameObject.AddComponent<RectMask2D>();

            var image = NewRect("Art", side).gameObject.AddComponent<Image>();
            image.sprite = art;
            image.type = Image.Type.Simple;
            image.color = art != null ? ArtTint : FallbackTint;
            image.raycastTarget = true;
            var artRect = image.rectTransform;
            artRect.pivot = new Vector2(0.5f, 0.5f);
            Stretch(artRect);
            if (art != null)
            {
                if (mirror) artRect.localScale = new Vector3(-1f, 1f, 1f);
                var focus = mirror ? new Vector2(1f - LeftFocus.x, LeftFocus.y) : LeftFocus;
                artRect.gameObject.AddComponent<BackgroundFit>().Configure(BackgroundFitMode.Cover, focus);
            }

            // 틀 쪽 그림자: 가운데 피벗 + 배율로 뒤집기 (왼쪽 = 오른쪽 끝에 붙어 오른쪽으로 진해짐, 오른쪽 = 거울)
            var shade = NewRect("Shade", side).gameObject.AddComponent<Image>();
            shade.sprite = FadeSprite();
            shade.color = ShadeColor;
            shade.raycastTarget = false;
            var shadeRect = shade.rectTransform;
            float edge = mirror ? 0f : 1f;
            shadeRect.anchorMin = new Vector2(edge, 0f);
            shadeRect.anchorMax = new Vector2(edge, 1f);
            shadeRect.pivot = new Vector2(0.5f, 0.5f);
            shadeRect.sizeDelta = new Vector2(ShadeWidth, 0f);
            shadeRect.anchoredPosition = new Vector2(mirror ? ShadeWidth * 0.5f : -ShadeWidth * 0.5f, 0f);
            if (mirror) shadeRect.localScale = new Vector3(-1f, 1f, 1f);

            var seam = NewRect("Seam", side).gameObject.AddComponent<Image>();
            seam.color = SeamColor;
            seam.raycastTarget = false;
            var seamRect = seam.rectTransform;
            seamRect.anchorMin = new Vector2(edge, 0f);
            seamRect.anchorMax = new Vector2(edge, 1f);
            seamRect.pivot = new Vector2(edge, 0.5f);
            seamRect.sizeDelta = new Vector2(SeamWidth, 0f);
            seamRect.anchoredPosition = Vector2.zero;

            decor[decorIndex] = shade.gameObject;
            decor[decorIndex + 1] = seam.gameObject;
            return side;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>가로 그라데이션 (왼쪽 투명 → 오른쪽 불투명, smoothstep). 한 번만 만든다</summary>
        static Sprite FadeSprite()
        {
            if (_fade != null) return _fade;
            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = "PlayFrameSides_Fade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[width];
            for (int i = 0; i < width; i++)
            {
                float t = i / (width - 1f);
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * t * t * (3f - 2f * t)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _fade = Sprite.Create(texture, new Rect(0f, 0f, width, 1f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _fade.name = "PlayFrameSides_Fade";
            _fade.hideFlags = HideFlags.DontSave;
            return _fade;
        }
    }
}
