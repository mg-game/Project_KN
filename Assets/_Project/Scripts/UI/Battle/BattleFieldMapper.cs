using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전장 좌표(수선점 원점, 방위 링 반지름 1) ↔ 화면 좌표 변환.
    /// 가로는 Ppu(월드 1당 픽셀), 세로는 원근감을 위해 Squash만큼 눌러 타원으로 그린다.
    /// 전장 레이어들은 이 RectTransform을 꽉 채우고 자식은 가운데 기준(anchor 0.5)으로 배치한다.
    /// 바닥 평면(GroundPlane)은 scale (1, Squash)라서 그 안에서는 월드 * Ppu로 원/부채꼴을 그리면 자동으로 눌린다.
    ///
    /// 바닥 그림(Battle/field_tall, 없으면 Battle/field)이 있으면 그림을 전투 화면에 꽉 채우고, 그림에 그려진 광장 링이
    /// 월드 반지름 _backdropRingWorld(기본 요격 범위 1.6)에 오도록 원점/배율/눌림을 그림에 맞춘다.
    /// 덮개 배율에는 상한이 있어서(Ppu 상한에서 그림 링 = 월드 링) 길쭉한 화면에서도 그림 링과 월드 링이 어긋나지 않는다 (UX-SPEC-IA §1.4).
    /// 그림이 없으면 화면 크기만으로 정한다.
    /// <para>
    /// 시트 올림 (UX-SPEC-IA §6.2): 이 Rect(Field)는 전장 창이다 — 자르기(RectMask2D)와 화면 점 판정은 창 기준.
    /// 전장 레이어는 그 안의 카메라(_view = Field/Camera, Stretch, ShakeRoot의 부모) 아래에 있고, 시트가 열리면 SetView(pan)가
    /// 카메라와 바닥 그림을 같이 올린다. 전장 로컬 좌표(WorldToField · Origin · Ppu · Squash)는 올림과 상관없다 —
    /// 카메라 밖에서 전장 위치에 겹쳐 그리는 쪽(알림 띠 · 가이드 퀘스트 줄)만 FieldToCanvasWorld를 거친다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BattleFieldMapper : MonoBehaviour
    {
        [SerializeField] RectTransform _groundPlane;
        [Tooltip("전투 카메라 Field/Camera (Stretch, ShakeRoot의 부모). 시트가 열리면 SetView로 올린다 — 비면 올림 없음 (예전 씬)")]
        [SerializeField] RectTransform _view;
        [Tooltip("가로로 보여야 하는 월드 반폭. 적 생성 반지름(2.5)보다 살짝 작게 잡아 적이 화면 가장자리에서 걸어 들어오게 한다")]
        [SerializeField] float _extentX = 2.35f;
        [Tooltip("세로로 보여야 하는 월드 반높이 (그림이 없을 때)")]
        [SerializeField] float _extentY = 2.1f;
        [SerializeField] float _minSquash = 0.66f;
        [SerializeField] float _maxSquash = 0.76f;
        [Tooltip("위쪽 HUD에 가려지지 않게 남기는 여백(px, 그림이 없을 때)")]
        [SerializeField] float _topMargin = 64f;
        [SerializeField] float _bottomMargin = 24f;
        [SerializeField] float _maxPpu = 290f;

        [Header("바닥 그림 맞춤 (선택)")]
        [Tooltip("바닥 그림이 덮는 영역 (전투 화면 전체)")]
        [SerializeField] RectTransform _backdropFrame;
        [Tooltip("바닥 그림 (영역 가운데 기준, 크기는 여기서 덮도록 정한다)")]
        [SerializeField] RectTransform _backdrop;
        [SerializeField] Vector2 _backdropSize = new(1080f, 810f);
        [Tooltip("그림 속 광장 링 중심 (uv, 왼쪽 아래 = 0)")]
        [SerializeField] Vector2 _backdropRingCenter = new(0.4995f, 0.5185f);
        [Tooltip("그림 속 광장 링 반지름 (uv 비율: 가로/그림 폭, 세로/그림 높이)")]
        [SerializeField] Vector2 _backdropRingRadius = new(0.3037f, 0.2938f);
        [Tooltip("그림 속 광장 링이 나타내는 월드 반지름")]
        [SerializeField] float _backdropRingWorld = 1.6f;
        [Tooltip("덮개 배율 상한에 걸려 그림이 전투 화면보다 낮을 때(아주 길쭉한 화면 ≥ 약 2.5:1)의 세로 자리. 1 = 위에 붙임 — 남는 띠가 아래 덱 띠 밑으로 간다 (aspect AF-3), 0.5 = 가운데 (예전)")]
        [Range(0f, 1f)] [SerializeField] float _capAlignY = 1f;

        RectTransform _rect;
        /// <summary>상한에 걸렸을 때 그림을 위로 붙인 만큼 (기준 px, 위로 +). 상한에 안 걸리면 0</summary>
        float _capOffsetY;
        Vector2 _appliedSize = new(-1f, -1f);
        Vector2 _appliedFrame = new(-1f, -1f);
        /// <summary>ViewVersion 판단용: 마지막 SetView 때의 시트 목표 열림 · 멈춤</summary>
        bool _viewOpen;
        bool _viewSettled = true;

        /// <summary>월드 1당 가로 픽셀</summary>
        public float Ppu { get; private set; } = ReferencePpu;
        /// <summary>세로 눌림 비율 (1 = 바로 위에서, 작을수록 비스듬히)</summary>
        public float Squash { get; private set; } = 0.72f;
        /// <summary>전장 원점(수선점)의 로컬 위치 (Rect 가운데 기준)</summary>
        public Vector2 Origin { get; private set; }
        /// <summary>레이아웃이 바뀔 때마다 증가 (정적 요소 재배치 판단용). 시트 올림(SetView)은 올리지 않는다</summary>
        public int Version { get; private set; }
        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;
        /// <summary>기준 화면(가로 1080, Ppu 약 205) 대비 캐릭터/적 크기 배율</summary>
        public float ActorScale => Mathf.Clamp(Ppu / ReferencePpu, 0.75f, 1.3f);

        const float ReferencePpu = 205f;

        // ───────────── 시트 올림 (UX-SPEC-IA §6.2 — BattleFrameDriver가 쓴다) ─────────────

        /// <summary>지금 전투 올림 (기준 px, 위로 +). 0 = MAIN. SetView만 바꾼다 — 전장 로컬 좌표(WorldToField · Origin · Ppu · Squash)는 그대로</summary>
        public float Pan { get; private set; }

        /// <summary>
        /// 보이는 띠가 바뀜 — 올림 목표가 바뀔 때(시트 열림/닫힘 목표가 뒤집힘), 움직임이 멈출 때, 멈춘 채 올림이 바뀔 때(화면 크기),
        /// 레이아웃이 바뀔 때(Version과 함께) 증가. 열고 닫는 도중의 매 프레임에는 오르지 않는다.
        /// Version과 따로: 정적 요소 재배치는 Version을, 보이는 띠(VisibleTop · VisibleBottom)에 맞춰 다시 놓는 쪽은 이것을 읽는다.
        /// </summary>
        public int ViewVersion { get; private set; }

        /// <summary>보이는 띠 위 끝 (카메라 로컬 y — 가운데 원점, 위로 +) = h/2 − Pan. MAIN에서는 h/2 (예전 BattleFx.FieldTop과 같다)</summary>
        public float VisibleTop => Rect.rect.height * 0.5f - Pan;

        /// <summary>
        /// 보이는 띠 아래 끝 (카메라 로컬 y) = −h/2 + max(0, 836·p − 156) − Pan (p = UIFrame.SheetProgress):
        /// MAIN은 덱 띠 위 끝(−h/2), 시트는 시트 위 끝.
        /// </summary>
        public float VisibleBottom => -Rect.rect.height * 0.5f + SheetCover - Pan;

        /// <summary>시트가 전장 창 아래쪽을 덮는 높이 (px, 창 아래 끝 = 덱 띠 위 끝에서 잼) = max(0, 836·p − 156)</summary>
        static float SheetCover => Mathf.Max(0f, UIFrame.SheetHeight * UIFrame.SheetProgress - UIFrame.StripHeight);

        /// <summary>
        /// 전투를 pan만큼 올린다: _view(Field/Camera)와 _backdrop(바닥 그림)의 anchoredPosition = (0, pan). Version은 올리지 않는다
        /// (정적 요소 재배치 · Recalculate 없음). BattleFrameDriver가 p마다 부른다.
        /// 카메라가 없으면(예전 씬) 올리지 않는다 — 그림만 올라가면 그림 링과 전장이 어긋난다.
        /// ViewVersion: 시트 목표가 뒤집혔거나, 멈춘 채로 부를 때 처음 멈췄거나 올림이 바뀌었으면 올린다.
        /// </summary>
        public void SetView(float pan)
        {
            if (_view == null) pan = 0f;
            bool open = UIFrame.SheetOpen;
            bool settled = UIFrame.SheetSettled;
            if (open != _viewOpen || (settled && (!_viewSettled || pan != Pan))) ViewVersion++;
            _viewOpen = open;
            _viewSettled = settled;
            Pan = pan;
            var at = new Vector2(0f, pan);
            if (_view != null) _view.anchoredPosition = at;
            if (_backdrop != null) _backdrop.anchoredPosition = new Vector2(0f, pan + _capOffsetY);
        }

        /// <summary>
        /// 전장 로컬 좌표(가운데 기준, WorldToField 결과) → 캔버스 월드 위치. 카메라 밖 겹침(알림 줄 · 가이드 퀘스트 적 가까움)이 쓴다:
        /// 카메라(_view)가 있으면 그 기준, 없으면 Field 기준 — 올림이 반영된다.
        /// </summary>
        public Vector3 FieldToCanvasWorld(Vector2 fieldLocal)
        {
            // ?? 대신 유니티 null 비교 (부서진 오브젝트도 null로 본다)
            RectTransform view = _view != null ? _view : Rect;
            return view.TransformPoint(view.rect.center + fieldLocal);
        }

        void Awake() => Recalculate();

        void OnEnable() => Recalculate();

        void OnRectTransformDimensionsChange() => Recalculate();

        /// <summary>월드 좌표 → 전장 로컬 좌표 (가운데 기준 anchoredPosition)</summary>
        public Vector2 WorldToField(Vector2 world) => new(Origin.x + world.x * Ppu, Origin.y + world.y * Ppu * Squash);

        public Vector2 FieldToWorld(Vector2 field) =>
            new((field.x - Origin.x) / Ppu, (field.y - Origin.y) / (Ppu * Squash));

        /// <summary>화면 좌표 → 월드 좌표. 카메라(_view) 기준이라 시트가 열려 전투가 올라가 있어도 드래그 조준이 정확하다 (카메라가 없으면 Field 기준)</summary>
        public bool ScreenToWorld(Vector2 screen, Camera eventCamera, out Vector2 world)
        {
            // ?? 대신 유니티 null 비교
            RectTransform view = _view != null ? _view : Rect;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, eventCamera, out var local))
            {
                world = default;
                return false;
            }
            // 로컬 좌표는 pivot 기준이므로 Rect 가운데 기준으로 보정
            local -= view.rect.center;
            world = FieldToWorld(local);
            return true;
        }

        /// <summary>
        /// 화면 점이 보이는 전장 위인지: 전장 창(Field) 안이고, 시트가 덮은 아래쪽(VisibleBottom 아래)이 아니다 (CR-26).
        /// 카메라는 올림만큼 창보다 위로 옮겨져 있어서 카메라 사각형으로 재면 시트 밑까지 들어가므로 창으로 잰다.
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screen, Camera eventCamera)
        {
            var rect = Rect;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screen, eventCamera)) return false;
            float cover = SheetCover;
            if (cover <= 0f) return true;
            // 창 아래 끝에서 잰 높이가 시트가 덮은 높이 이상 (= 카메라 로컬 VisibleBottom 이상)
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, eventCamera, out var local)
                   && local.y - rect.rect.yMin >= cover;
        }

        public void Recalculate()
        {
            var size = Rect.rect.size;
            if (size.x <= 1f || size.y <= 1f) return;
            var frame = _backdropFrame != null ? _backdropFrame.rect.size : Vector2.zero;
            if (size == _appliedSize && frame == _appliedFrame) return;
            _appliedSize = size;
            _appliedFrame = frame;

            if (!FitToBackdrop(size, frame)) FitToRect(size);

            if (_groundPlane != null)
            {
                _groundPlane.anchoredPosition = Origin;
                _groundPlane.localScale = new Vector3(1f, Squash, 1f);
            }
            Version++;
            // 창 크기가 바뀌면 보이는 띠(VisibleTop · VisibleBottom)도 바뀐다
            ViewVersion++;
        }

        /// <summary>
        /// 바닥 그림을 화면에 꽉 채우고(cover), 그림 속 광장 링에 전장을 맞춘다.
        /// 덮개 배율 상한 (UX-SPEC-IA §1.4): ppuCap = min(가로/2 ÷ _extentX, _maxPpu)에서 그림 링이 월드 _backdropRingWorld가 되는 배율까지 —
        /// 그보다 키우면 Ppu가 상한에 막혀 그림 링이 월드 링보다 커진다 (예전 길쭉한 폰 문제, 세로가 모자란 옛 그림이면 2배 흐림).
        /// 상한에 걸리면 그림은 가운데에 놓이고 위아래가 빈다. 그림 위치는 지금 올림 (0, Pan) — 시트가 열린 동안 다시 맞춰도 그림과 전장이 어긋나지 않는다.
        /// </summary>
        bool FitToBackdrop(Vector2 size, Vector2 frame)
        {
            if (_backdrop == null || _backdropFrame == null || frame.x <= 1f || frame.y <= 1f) return false;
            if (_backdropSize.x <= 0f || _backdropSize.y <= 0f || _backdropRingWorld <= 0f) return false;

            // 길쭉한 폰에서 배율이 너무 커지면 적 생성 위치가 화면 밖으로 너무 나가므로 Ppu에 상한을 둔다
            float ppuCap = Mathf.Min(size.x * 0.5f / _extentX, _maxPpu);
            float cover = Mathf.Max(frame.x / _backdropSize.x, frame.y / _backdropSize.y);
            if (_backdropRingRadius.x > 0f) cover = Mathf.Min(cover, ppuCap * _backdropRingWorld / (_backdropRingRadius.x * _backdropSize.x));
            var shown = _backdropSize * cover;
            // 아주 길쭉한 화면 (aspect AF-3): 상한에 걸려 위아래가 비면 그림을 위에 붙여 남는 띠를 아래(덱 띠 밑)로 보낸다 — 상단 바 밑 하늘 띠 없음.
            // 링 중심도 같은 만큼 옮겨 재므로 그림 링 = 월드 링은 그대로다
            _capOffsetY = shown.y < frame.y - 0.5f ? (frame.y - shown.y) * (_capAlignY - 0.5f) : 0f;
            _backdrop.anchorMin = _backdrop.anchorMax = new Vector2(0.5f, 0.5f);
            _backdrop.sizeDelta = shown;
            _backdrop.anchoredPosition = new Vector2(0f, Pan + _capOffsetY);

            // 그림 속 링 중심 → 전장 로컬 좌표 (두 Rect가 같은 캔버스 아래라 배율과 무관). 올림 전 자리(영역 가운데)로 잰다 —
            // 카메라와 그림이 같이 올라가므로 카메라 로컬로는 그대로다
            var ringInFrame = _backdropFrame.rect.center + new Vector2((_backdropRingCenter.x - 0.5f) * shown.x, (_backdropRingCenter.y - 0.5f) * shown.y + _capOffsetY);
            var world = _backdropFrame.TransformPoint(ringInFrame);
            var local = (Vector2)Rect.InverseTransformPoint(world) - Rect.rect.center;

            float radiusX = _backdropRingRadius.x * shown.x;
            float radiusY = _backdropRingRadius.y * shown.y;
            // 덮개 배율 상한 덕분에 radiusX / 링 월드 ≤ ppuCap — 그림 링 = 월드 링이 그대로 남는다
            Ppu = Mathf.Clamp(radiusX / _backdropRingWorld, 40f, ppuCap);
            Squash = Mathf.Clamp(radiusY / Mathf.Max(1f, radiusX), 0.4f, 0.9f);
            Origin = local;
            return true;
        }

        /// <summary>그림이 없을 때: 화면 크기만으로 배율/눌림을 정한다</summary>
        void FitToRect(Vector2 size)
        {
            float halfHeight = size.y * 0.5f - (_topMargin + _bottomMargin) * 0.5f;
            float ppu = Mathf.Min(size.x * 0.5f / _extentX, halfHeight / (_extentY * _minSquash));
            Ppu = Mathf.Clamp(ppu, 40f, _maxPpu);
            // 세로가 넉넉한 폰은 덜 눌러서 위에서 내려다보는 느낌으로 공간을 채운다
            Squash = Mathf.Clamp(halfHeight / (_extentY * Ppu), _minSquash, _maxSquash);
            Origin = new Vector2(0f, (_bottomMargin - _topMargin) * 0.5f);
        }
    }
}
