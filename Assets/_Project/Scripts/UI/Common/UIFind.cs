using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 다른 화면이 만든 UI를 이름으로 찾는 도우미 (튜토리얼 손가락·팡파르가 남의 파일을 고치지 않고 붙을 때 쓴다).
    /// Transform.name은 읽을 때마다 문자열을 만들므로 매 프레임 부르지 말고, 찾은 결과를 캐시해 둔다.
    /// 실행 중에 만드는 작은 UI(연속 알약 · "+N" 받침)가 빌더 없이 같은 힉스필드 아트를 입도록 이미 올라온 스프라이트도 이름으로 찾는다 (LoadedSprite).
    /// </summary>
    public static class UIFind
    {
        // ───────────── 실행 중에 찾는 공용 아트 (Art/UI 파일 이름 = 스프라이트 이름) ─────────────

        /// <summary>흰 바느질 알약 (192x96 캡슐, 틴트용 — NEW 알약 · 연속 알약은 Gold로 물들인다)</summary>
        public const string ArtPillWhite = "ui_pill_white_9s";
        /// <summary>어두운 바느질 알약 (금실 테두리 — 상단 재화 칩 · 수량 받침, 흰색 그대로)</summary>
        public const string ArtPillNight = "ui_pill_night_9s";
        /// <summary>금 버튼 (흰색 그대로)</summary>
        public const string ArtButtonGold = "button_gold_9s";

        /// <summary>찾아본 스프라이트 (없었으면 진짜 null을 넣어 다시 훑지 않는다)</summary>
        static readonly Dictionary<string, Sprite> LoadedSprites = new();

        /// <summary>root 아래(자신 제외)에서 이름이 같은 첫 활성 오브젝트를 깊이 우선으로 찾는다</summary>
        public static RectTransform Deep(Transform root, string name, bool activeOnly = true)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (activeOnly && !child.gameObject.activeSelf) continue;
                if (child.name == name) return child as RectTransform;
                var found = Deep(child, name, activeOnly);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>root 아래에서 이름에 part가 들어간 첫 활성 오브젝트</summary>
        public static RectTransform DeepContains(Transform root, string part, bool activeOnly = true)
        {
            if (root == null || string.IsNullOrEmpty(part)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (activeOnly && !child.gameObject.activeSelf) continue;
                if (child.name.IndexOf(part, System.StringComparison.Ordinal) >= 0) return child as RectTransform;
                var found = DeepContains(child, part, activeOnly);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Canvas 기준 경로 (CoachTargets 상수). 없으면 null</summary>
        public static RectTransform Path(Transform canvas, string path) =>
            canvas == null || string.IsNullOrEmpty(path) ? null : canvas.Find(path) as RectTransform;

        /// <summary>화면에 실제로 보이는지 (켜져 있고 크기가 있음). 매 프레임 불러도 된다</summary>
        public static bool IsShown(RectTransform rt) =>
            rt != null && rt.gameObject.activeInHierarchy && rt.rect.width > 1f && rt.rect.height > 1f;

        /// <summary>대상의 사각형을 space 좌표계로 옮긴다 (corners는 길이 4 재사용 버퍼)</summary>
        public static Rect RectIn(RectTransform target, RectTransform space, Vector3[] corners)
        {
            target.GetWorldCorners(corners);
            var min = (Vector2)space.InverseTransformPoint(corners[0]);
            var max = min;
            for (int i = 1; i < 4; i++)
            {
                var p = (Vector2)space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// RectIn과 같지만 대상 자신의 localScale(팝업 창의 열고 닫는 0.90 → 1 · 1 → 0.94 배율 등)을 뺀 원래 크기로 돌려준다 —
        /// 크기 변화의 중심인 피벗을 기준으로 되돌린다. 대상 위 부모들의 배율은 그대로 따른다 (corners는 길이 4 재사용 버퍼).
        /// </summary>
        public static Rect UnscaledRectIn(RectTransform target, RectTransform space, Vector3[] corners)
        {
            var box = RectIn(target, space, corners);
            var scale = target.localScale;
            float sx = Mathf.Abs(scale.x);
            float sy = Mathf.Abs(scale.y);
            if (sx < 1e-4f || sy < 1e-4f || (Mathf.Approximately(sx, 1f) && Mathf.Approximately(sy, 1f))) return box;
            var pivot = (Vector2)space.InverseTransformPoint(target.position);
            return Rect.MinMaxRect(
                pivot.x + (box.xMin - pivot.x) / sx, pivot.y + (box.yMin - pivot.y) / sy,
                pivot.x + (box.xMax - pivot.x) / sx, pivot.y + (box.yMax - pivot.y) / sy);
        }

        /// <summary>대상 중심을 space 좌표계로</summary>
        public static Vector2 CenterIn(RectTransform target, RectTransform space) =>
            space.InverseTransformPoint(target.TransformPoint(target.rect.center));

        // ───────────── 실행 중 아트 ─────────────

        /// <summary>
        /// 이미 메모리에 올라온 스프라이트를 이름으로 찾는다 (장면이 쓰는 아트는 장면과 함께 올라와 있다 — 꺼진 오브젝트가 쓰는 것도).
        /// 이름마다 처음 한 번만 전체를 훑고(할당 있음) 결과를 기억한다 — 매 프레임 부르지 말고 만들 때 한 번만 부른다. 없으면 null.
        /// </summary>
        public static Sprite LoadedSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (LoadedSprites.TryGetValue(name, out var cached))
            {
                if (cached != null) return cached;
                if (ReferenceEquals(cached, null)) return null; // 찾아봤는데 없었다
                // 있었는데 장면과 함께 내려갔다 → 다시 찾는다
            }
            Sprite found = null;
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite == null || sprite.name != name) continue;
                found = sprite;
                break;
            }
            LoadedSprites[name] = found;
            return found;
        }

        /// <summary>
        /// 캡슐(둥근 끝) 모양으로 9-slice를 입힌다: 그려지는 위아래 테두리 합 = 높이 (UiArt.CapsulePpu와 같은 계산 — 192x96 알약은 96 / 높이).
        /// 스프라이트가 없으면 단색(color). 테두리 없는 스프라이트는 Simple.
        /// </summary>
        public static void SkinCapsule(Image image, Sprite sprite, float height, Color color)
        {
            if (image == null) return;
            image.sprite = sprite;
            bool sliced = sprite != null && sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = sliced ? Mathf.Max(0.01f, (sprite.border.y + sprite.border.w) / Mathf.Max(1f, height)) : 1f;
            image.color = color;
        }
    }
}
