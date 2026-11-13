using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleGame.UI
{
    /// <summary>
    /// 시트 머리 끌기 (UX-SPEC-IA §4.3 · §5.4) — SheetHeader/DragArea · HandleHit에 하나씩 붙는다 (패널 안 스크롤은 시트를 움직이지 않는다).
    /// 아래로 끌면 p = p0 − Δy/836 (화면 델타를 rootCanvas.scaleFactor로 캔버스 px로 — ContentArea는 중첩 Canvas), 위로는 1을 넘지 않는다 (v1).
    /// 놓을 때 잡은 자리 p0에서 보이던 양의 SheetDismissFraction(1/4) 넘게 끌어내렸거나(p &lt; p0·0.75 — 다 열린 시트를 잡았으면 p &lt; 0.75, §1.5 그대로)
    /// 마지막 0.1초 아래로 1200px/s(SheetDismissVelocity) 이상이면 닫고, 아니면 다시 연다.
    /// 판단은 잡은 자리 기준이다: 열리는 중(p0 &lt; 1)에 잡아 위로 끌었다 놓으면 놓은 p가 0.75 아래여도 닫지 않는다 (위로 놓기는 닫기가 아니다).
    /// 포커스를 잃으면 끌기를 끝내고 열린 자리로 (M01). 뒤로가기 · 사냥 버튼은 SheetController.Close가 끌기를 판단 없이 끝내고 지금 p에서 닫는다 (CR-21).
    /// <para>
    /// 손가락은 누른 자리부터 따라간다 (끌기 문턱만큼 늦지 않게). 누른 자리가 너무 멀면(코드가 ExecuteEvents로 보낸 끌기 — AutoShot은 DragArea에
    /// begin/drag/end를 보낸다, 경로 고정 CR-04) 끌기 시작 자리부터. 속도는 최근 8개 표본(시각 · 화면 y)으로 잰다 (할당 없음).
    /// 끌기를 시작한 포인터 하나만 따른다.
    /// </para>
    /// </summary>
    public class SheetDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>속도 표본 수 · 속도를 재는 구간 (초)</summary>
        const int SampleCount = 8;
        const float SpeedWindow = 0.1f;
        /// <summary>누른 자리와 끌기 시작 자리가 이보다 멀면 누른 자리를 믿지 않는다 (화면 px)</summary>
        const float MaxPressCatchUp = 96f;

        [Tooltip("움직일 시트 (SafeArea/TabBar의 SheetController)")]
        [SerializeField] SheetController _sheet;

        readonly float[] _sampleTime = new float[SampleCount];
        readonly float[] _sampleY = new float[SampleCount];
        int _sampleNext;
        int _sampleCount;
        int _pointerId;
        float _startY;
        float _startP;
        float _scale = 1f;
        Canvas _canvas;

        /// <summary>끌고 있는지</summary>
        public bool IsDragging { get; private set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsDragging || _sheet == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (!_sheet.BeginDrag(this)) return;
            IsDragging = true;
            _pointerId = eventData.pointerId;
            _scale = CanvasScale();
            var now = eventData.position;
            var press = eventData.pressPosition;
            _startY = (now - press).sqrMagnitude <= MaxPressCatchUp * MaxPressCatchUp ? press.y : now.y;
            _startP = _sheet.Progress;
            _sampleNext = 0;
            _sampleCount = 0;
            Follow(now.y);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != _pointerId) return;
            Follow(eventData.position.y);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != _pointerId) return;
            Follow(eventData.position.y);
            IsDragging = false;
            if (_sheet == null) return;
            _sheet.EndDrag(this, ShouldDismiss(_sheet.Progress));
        }

        /// <summary>
        /// 놓을 때 닫을지: 잡은 자리 p0에서 보이던 양의 SheetDismissFraction(1/4) 넘게 끌어내렸거나(p &lt; p0·0.75) 마지막 0.1초 아래로 SheetDismissVelocity 이상.
        /// 다 열린 시트(p0 = 1)는 p &lt; 0.75 그대로다. 열리는 중에 잡았으면 잡은 자리 기준이라 위로 놓기는 닫지 않고, 끝까지(p = 0) 끌어내리면 닫는다
        /// </summary>
        bool ShouldDismiss(float p) =>
            p < _startP * (1f - UIMotion.SheetDismissFraction) || DownSpeed() >= UIMotion.SheetDismissVelocity;

        /// <summary>끌기를 놓기 판단 없이 끝낸다 (시트는 열린 자리로 — 닫기 쪽이 부르면 지금 p에서 닫힌다). 끌고 있지 않으면 아무 일 없다</summary>
        public void Cancel()
        {
            if (!IsDragging) return;
            IsDragging = false;
            if (_sheet != null) _sheet.OnDragCancelled(this);
        }

        void OnDisable() => Cancel();

        /// <summary>창 포커스를 잃으면 끌기를 끝내고 제자리로 (M01 — 알트탭 중 놓기를 놓쳐 시트가 손가락 없이 걸려 있지 않게)</summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused) Cancel();
        }

        void Follow(float screenY)
        {
            AddSample(screenY);
            if (_sheet == null) return;
            float down = (_startY - screenY) / _scale;
            _sheet.DragTo(this, Mathf.Clamp01(_startP - down / UIFrame.SheetHeight));
        }

        void AddSample(float screenY)
        {
            _sampleTime[_sampleNext] = Time.unscaledTime;
            _sampleY[_sampleNext] = screenY;
            _sampleNext = (_sampleNext + 1) % SampleCount;
            if (_sampleCount < SampleCount) _sampleCount++;
        }

        /// <summary>최근 0.1초 동안 아래로 움직인 속도 (캔버스 px/s, 위로면 음수). 표본이 모자라거나 같은 시각이면 0</summary>
        float DownSpeed()
        {
            if (_sampleCount < 2) return 0f;
            int newest = (_sampleNext - 1 + SampleCount) % SampleCount;
            float newestTime = _sampleTime[newest];
            int oldest = newest;
            for (int i = 1; i < _sampleCount; i++)
            {
                oldest = (newest - i + SampleCount) % SampleCount;
                if (newestTime - _sampleTime[oldest] >= SpeedWindow) break;
            }
            float dt = newestTime - _sampleTime[oldest];
            if (dt < 0.0001f) return 0f;
            return (_sampleY[oldest] - _sampleY[newest]) / dt / _scale;
        }

        /// <summary>화면 px → 캔버스 px 배율 (루트 캔버스 scaleFactor — 이 아래 캔버스는 중첩이라 루트를 본다)</summary>
        float CanvasScale()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            var root = _canvas != null ? _canvas.rootCanvas : null;
            float scale = root != null ? root.scaleFactor : 1f;
            return scale > 0.0001f ? scale : 1f;
        }
    }
}
