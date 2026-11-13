using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 코치마크 어둠: 화면 전체를 덮되 대상 자리에 둥근 구멍이 뚫리고, 구멍 가장자리는 부드럽게 번진다 (메시로 직접 그림).
    ///   · 구멍 밖 입력 막기(BlockOutside)일 때만 레이캐스트를 받고, 구멍 안은 ICanvasRaycastFilter로 통과시켜 아래 버튼이 눌린다.
    ///   · 구멍 밖을 누르면 Tapped가 울린다 (손가락이 "여기예요" 하고 툭 튐).
    ///   · 구멍이 바뀔 때만 메시를 다시 만든다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class HoleDimGraphic : MaskableGraphic, ICanvasRaycastFilter, IPointerClickHandler
    {
        const int CornerSegments = 8;

        [SerializeField] float _feather = 22f;

        Rect _hole;
        float _radius = 24f;
        bool _hasHole;

        /// <summary>구멍 밖을 눌렀음 (입력 막기 중)</summary>
        public event System.Action Tapped;

        /// <summary>구멍 밖 입력을 막는다</summary>
        public bool BlockOutside { get; set; }

        /// <summary>구멍이 있음</summary>
        public bool HasHole => _hasHole;

        /// <summary>구멍 사각형 (로컬)</summary>
        public Rect Hole => _hole;

        /// <summary>구멍 위치 (이 그래픽의 로컬 좌표)</summary>
        public void SetHole(Rect hole, float radius)
        {
            radius = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(hole.width, hole.height) * 0.5f));
            if (_hasHole && hole == _hole && Mathf.Approximately(radius, _radius)) return;
            _hole = hole;
            _radius = radius;
            _hasHole = true;
            SetVerticesDirty();
        }

        /// <summary>구멍 없이 화면 전체를 덮는다</summary>
        public void ClearHole()
        {
            if (!_hasHole) return;
            _hasHole = false;
            SetVerticesDirty();
        }

        /// <summary>구멍 밖 입력 막기일 때만 구멍 바깥을 받는다</summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!BlockOutside) return false;
            if (!_hasHole) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return true;
            return !_hole.Contains(local);
        }

        /// <summary>막힌 곳을 누름 → Tapped</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (BlockOutside) Tapped?.Invoke();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var screen = GetPixelAdjustedRect();
            var solid = (Color32)color;
            var clear = new Color32(solid.r, solid.g, solid.b, 0);

            if (!_hasHole)
            {
                AddQuad(vh, screen.min, screen.max, solid);
                return;
            }

            // 바깥 상자 = 구멍 + 번짐. 상자 밖 네 띠는 가득, 상자 모서리와 둥근 테두리 사이는 부채꼴, 테두리 안쪽 번짐은 투명으로.
            float f = Mathf.Max(0.5f, _feather);
            var inner = _hole;
            var outer = Rect.MinMaxRect(inner.xMin - f, inner.yMin - f, inner.xMax + f, inner.yMax + f);
            float oxMin = Mathf.Max(outer.xMin, screen.xMin), oxMax = Mathf.Min(outer.xMax, screen.xMax);
            float oyMin = Mathf.Max(outer.yMin, screen.yMin), oyMax = Mathf.Min(outer.yMax, screen.yMax);

            if (oyMax < screen.yMax) AddQuad(vh, new Vector2(screen.xMin, oyMax), screen.max, solid);
            if (oyMin > screen.yMin) AddQuad(vh, screen.min, new Vector2(screen.xMax, oyMin), solid);
            if (oxMin > screen.xMin && oyMax > oyMin) AddQuad(vh, new Vector2(screen.xMin, oyMin), new Vector2(oxMin, oyMax), solid);
            if (oxMax < screen.xMax && oyMax > oyMin) AddQuad(vh, new Vector2(oxMax, oyMin), new Vector2(screen.xMax, oyMax), solid);

            // 모서리 4개 (오른쪽 위 → 왼쪽 위 → 왼쪽 아래 → 오른쪽 아래, 반시계)
            float r = _radius;
            var centers = new Vector4(inner.xMax - r, inner.yMax - r, inner.xMin + r, inner.yMin + r);
            AddCorner(vh, new Vector2(centers.x, centers.y), new Vector2(outer.xMax, outer.yMax), 0f, r, f, solid, clear);
            AddCorner(vh, new Vector2(centers.z, centers.y), new Vector2(outer.xMin, outer.yMax), 90f, r, f, solid, clear);
            AddCorner(vh, new Vector2(centers.z, centers.w), new Vector2(outer.xMin, outer.yMin), 180f, r, f, solid, clear);
            AddCorner(vh, new Vector2(centers.x, centers.w), new Vector2(outer.xMax, outer.yMin), 270f, r, f, solid, clear);

            // 곧은 변의 번짐 (모서리 사이)
            AddFeatherEdge(vh, new Vector2(centers.z, inner.yMax), new Vector2(centers.x, inner.yMax), Vector2.up, f, solid, clear);
            AddFeatherEdge(vh, new Vector2(centers.x, inner.yMin), new Vector2(centers.z, inner.yMin), Vector2.down, f, solid, clear);
            AddFeatherEdge(vh, new Vector2(inner.xMin, centers.w), new Vector2(inner.xMin, centers.y), Vector2.left, f, solid, clear);
            AddFeatherEdge(vh, new Vector2(inner.xMax, centers.y), new Vector2(inner.xMax, centers.w), Vector2.right, f, solid, clear);
        }

        /// <summary>둥근 모서리 하나: 안쪽 호(투명) → 바깥 호(가득) 번짐 띠 + 바깥 호와 상자 모서리 사이 부채꼴(가득)</summary>
        static void AddCorner(VertexHelper vh, Vector2 center, Vector2 boxCorner, float startDeg, float r, float f, Color32 solid, Color32 clear)
        {
            int cornerIndex = vh.currentVertCount;
            vh.AddVert(boxCorner, solid, Vector4.zero);
            int first = vh.currentVertCount;
            for (int i = 0; i <= CornerSegments; i++)
            {
                float a = (startDeg + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(center + dir * r, clear, Vector4.zero);
                vh.AddVert(center + dir * (r + f), solid, Vector4.zero);
            }
            for (int i = 0; i < CornerSegments; i++)
            {
                int innerA = first + i * 2, outerA = innerA + 1, innerB = innerA + 2, outerB = innerA + 3;
                vh.AddTriangle(innerA, outerA, outerB);
                vh.AddTriangle(innerA, outerB, innerB);
                vh.AddTriangle(cornerIndex, outerB, outerA);
            }
        }

        static void AddFeatherEdge(VertexHelper vh, Vector2 from, Vector2 to, Vector2 normal, float f, Color32 solid, Color32 clear)
        {
            if ((to - from).sqrMagnitude < 0.01f) return;
            int i = vh.currentVertCount;
            vh.AddVert(from, clear, Vector4.zero);
            vh.AddVert(to, clear, Vector4.zero);
            vh.AddVert(to + normal * f, solid, Vector4.zero);
            vh.AddVert(from + normal * f, solid, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color32 color)
        {
            if (max.x <= min.x || max.y <= min.y) return;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(min.x, min.y), color, Vector4.zero);
            vh.AddVert(new Vector3(min.x, max.y), color, Vector4.zero);
            vh.AddVert(new Vector3(max.x, max.y), color, Vector4.zero);
            vh.AddVert(new Vector3(max.x, min.y), color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
