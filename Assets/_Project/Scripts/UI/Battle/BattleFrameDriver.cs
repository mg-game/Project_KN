using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시트가 열린 만큼(p) 전투 화면을 맞춘다 (UX-SPEC-IA §6.3) — Battle 루트에 붙는다. UIFrame.SheetProgressChanged만 듣고 전환 사이에는 쉰다 (Update 없음).
    /// <para>
    /// p마다: _mapper.SetView(PanOpen · p) (전투 카메라 Field/Camera와 바닥 그림을 같이 올림, Version은 그대로) +
    /// _bandAreas(DangerVignette · BannerArea · SynergyBannerArea · NoticeArea)의 offsetMin = (0, max(156, 836·p)) — 배너 · 경고가 보이는 띠 안에만.
    /// 한 프레임 비용: mapper 2번 + 여백 4번 쓰기 (Recalculate · Version 올림 · 할당 없음).
    /// 줄이기(UIMotion.Reduced)는 SheetController가 p를 계단으로 알리므로(열기 시작에 1, 닫기 끝에 0) 따로 다루지 않아도 올림이 바로 붙는다.
    /// </para>
    /// <para>
    /// PanOpen (§1.3 규칙, StageArea 로컬 px, 위에서 잼): H = StageArea 높이, k = Ppu / 205, sheetTop = H − 836,
    /// mainShop = (H − 156)/2 − Origin.y (MAIN에서 수선점이 StageArea 위 끝에서 얼마나 아래인지 — 여기서는 전장 창에서 직접 잰다),
    /// shop = sheetTop ≥ 760k ? sheetTop − 348k : 146k + 0.35·sheetTop,
    /// PanOpen = round_px(max(0, mainShop − shop)), round_px(v) = Round(v·s)/s (s = 루트 캔버스 scaleFactor — 반 텍셀 흐림 방지, CR-09).
    /// 16:9 = 360 · 19.5:9 = 224 · 20:9 = 194 (인셋 100/50 가정 — 다른 화면은 늘 런타임에 잰다). 전투는 아래로 내려가지 않는다.
    /// mapper.Version · 높이 · scaleFactor가 바뀌거나 OnRectTransformDimensionsChange면 다시 계산하고, 시트가 열려 있으면 SetView(PanOpen·p)를 바로 다시 적용한다.
    /// </para>
    /// <para>
    /// 가려진 곳 그리지 않기 (UX-SPEC-IA §4.5 · CR-18, P2): 시트가 다 열려 멈춰 있으면(p = 1 · SheetSettled · SheetOpen)
    /// 전장 창(Field)의 RectMask2D 아래 여백을 680(= 836 − 156, 시트가 덮은 높이)으로 — 시트 밑 전장 ≈46%를 자른다 —
    /// 하단 띠(Strip)의 자체 Canvas와 GraphicRaycaster를 끈다 (덱 쿨다운 원 · 등불 게이지가 가려진 채 매 프레임 다시 그리지 않게,
    /// 가려진 카드 · 등불이 누름 판정 목록에 남지 않게).
    /// 닫기가 시작되면(SheetStateChanged false · p가 1에서 움직임) 바로 되돌린다. 논리(쿨다운 · 자동 소환)는 그대로 돈다.
    /// 코치 대상 경로(Strip/DeckPanel/Card{n} · Strip/LampButton)는 컴포넌트만 더해져 그대로다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BattleFrameDriver : MonoBehaviour
    {
        [Tooltip("전장 좌표 변환 (Battle/Field)")]
        [SerializeField] BattleFieldMapper _mapper;
        [Tooltip("아래 여백을 시트에 맞출 띠 영역 (DangerVignette, BannerArea, SynergyBannerArea, NoticeArea)")]
        [SerializeField] RectTransform[] _bandAreas;
        [Tooltip("하단 띠 Strip의 자체 Canvas — 시트가 다 열려 멈춰 있으면 끈다 (CR-18). 비면 끄지 않는다")]
        [SerializeField] Canvas _stripCanvas;
        [Tooltip("하단 띠 Strip의 GraphicRaycaster — Canvas와 같이 끄고 켠다 (CR-18). 비면 그대로 둔다")]
        [SerializeField] GraphicRaycaster _stripRaycaster;

        /// <summary>올림 규칙의 기준 Ppu (기준 화면 1080 폭 — 그림 링 = 월드 1.6일 때). k = Ppu / 이것</summary>
        const float RulePpu = 205f;
        /// <summary>시트 위 끝이 StageArea 위 끝에서 이만큼(× k) 이상 아래면 수선점을 시트 위 끝에서 RuleShopAboveSheet(× k)만큼 위에 둔다</summary>
        const float RuleRoomySheetTop = 760f;
        const float RuleShopAboveSheet = 348f;
        /// <summary>그보다 짧은 화면: 수선점 = RuleShortShopBase·k + RuleShortShopSlope·sheetTop (StageArea 위 끝에서)</summary>
        const float RuleShortShopBase = 146f;
        const float RuleShortShopSlope = 0.35f;
        /// <summary>
        /// 기기 픽셀 반올림 전에 맞추는 단위 (1/32px). 링 상수(5자리)에서 온 Ppu의 아주 작은 오차(16:9에서 ≈ −0.004px)가 .5 경계를 넘기지 않게 —
        /// 그래야 .5로 끝나는 규칙 값이 문서 값과 같다 (16:9 359.5 → 360 · 19.5:9 224.5 → 224 · 20:9 194.5 → 194, Mathf.Round는 .5를 짝수로)
        /// </summary>
        const float RuleSnap = 32f;

        /// <summary>시트가 다 열렸을 때 전장 창 아래쪽이 가려지는 높이 (836 − 156 = 680) — RectMask2D 아래 여백</summary>
        internal const float CoveredFieldHeight = UIFrame.SheetHeight - UIFrame.StripHeight;

        RectTransform _rect;
        Canvas _canvas;
        RectMask2D _fieldMask;
        bool _fieldMaskKnown;
        bool _culled;
        bool _subscribed;
        int _layoutVersion = -1;
        float _layoutHeight = -1f;
        float _layoutScale = -1f;

        /// <summary>시트가 다 열렸을 때 전투를 올리는 양 (기준 px, 기기 픽셀에 맞춰 반올림 — 16:9 = 360). 계산 전에는 0</summary>
        public float PanOpen { get; private set; }

        RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        void OnEnable()
        {
            if (!_subscribed)
            {
                UIFrame.SheetProgressChanged += OnSheetProgress;
                UIFrame.SheetStateChanged += OnSheetState;
                _subscribed = true;
            }
            Recompute();
            Apply(UIFrame.SheetProgress);
        }

        void OnDisable()
        {
            ApplyCull(false);
            if (!_subscribed) return;
            UIFrame.SheetProgressChanged -= OnSheetProgress;
            UIFrame.SheetStateChanged -= OnSheetState;
            _subscribed = false;
        }

        void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            Recompute();
            // 시트가 열려 있으면 새 올림을 바로 (닫혀 있으면 올림 0 · 여백 156이라 그대로)
            if (UIFrame.SheetProgress > 0f) Apply(UIFrame.SheetProgress);
        }

        void OnSheetProgress(float p)
        {
            if (IsStale()) Recompute();
            Apply(p);
        }

        /// <summary>닫기(또는 다른 목표)가 시작되면 가려진 곳 자르기를 바로 푼다 (다음 p를 기다리지 않는다)</summary>
        void OnSheetState(bool open)
        {
            if (!open) ApplyCull(false);
        }

        /// <summary>다 열려 멈춘 시트 밑은 그리지 않는다: 전장 창 아래 여백 680 + 하단 띠 Canvas 끄기 (바뀔 때만)</summary>
        void ApplyCull(bool cull)
        {
            if (cull == _culled) return;
            _culled = cull;
            if (!_fieldMaskKnown)
            {
                _fieldMaskKnown = true;
                if (_mapper != null) _fieldMask = _mapper.GetComponent<RectMask2D>();
            }
            if (_fieldMask != null)
            {
                var padding = _fieldMask.padding;
                padding.y = cull ? CoveredFieldHeight : 0f;
                _fieldMask.padding = padding;
            }
            if (_stripCanvas != null) _stripCanvas.enabled = !cull;
            if (_stripRaycaster != null) _stripRaycaster.enabled = !cull;
        }

        /// <summary>시트가 다 열려 멈춘 상태인지 (가려진 곳을 자를 때) — 순수 판정</summary>
        internal static bool ShouldCull(float p, bool settled, bool open) => open && settled && p >= 1f;

        /// <summary>p에 맞춰 전투 올림 + 띠 아래 여백 (mapper 2번 + 여백 4번 쓰기, 할당 없음)</summary>
        void Apply(float p)
        {
            ApplyCull(ShouldCull(p, UIFrame.SheetSettled, UIFrame.SheetOpen));
            if (_mapper != null) _mapper.SetView(PanOpen * p);
            if (_bandAreas == null) return;
            float inset = Mathf.Max(UIFrame.StripHeight, UIFrame.SheetHeight * p);
            for (int i = 0; i < _bandAreas.Length; i++)
            {
                var area = _bandAreas[i];
                if (area == null) continue;
                var offset = area.offsetMin;
                if (offset.y != inset) area.offsetMin = new Vector2(offset.x, inset);
            }
        }

        /// <summary>레이아웃(전장 · 높이 · 캔버스 배율)이 PanOpen을 잰 뒤 바뀌었는지</summary>
        bool IsStale() =>
            _mapper != null && (_mapper.Version != _layoutVersion || Rect.rect.height != _layoutHeight || ScaleFactor() != _layoutScale);

        /// <summary>PanOpen을 다시 잰다 (§1.3 규칙 + 기기 픽셀 반올림). 레이아웃이 바뀔 때만 — 움직이는 프레임마다가 아니다</summary>
        void Recompute()
        {
            if (_mapper == null)
            {
                PanOpen = 0f;
                return;
            }
            // 깨어나는 순서상 전장이 아직 자기 레이아웃을 안 쟀을 수 있다 — 지금 크기로 맞춘다 (크기가 같으면 바로 끝난다)
            _mapper.Recalculate();
            float height = Rect.rect.height;
            float scale = ScaleFactor();
            _layoutVersion = _mapper.Version;
            _layoutHeight = height;
            _layoutScale = scale;
            PanOpen = ComputePanOpen(height, MainShopDepth(), _mapper.Ppu / RulePpu, scale);
        }

        /// <summary>
        /// MAIN에서 수선점(전장 원점)이 StageArea(= Battle) 위 끝에서 얼마나 아래인지 (px).
        /// 전장 창이 Battle 위 끝에 붙은 Stretch(0, 0, 0, 156)이면 (H − 156)/2 − Origin.y — 창 자리를 직접 재서 창 여백이 바뀌어도 맞다.
        /// 창(Field) 기준이라 지금 올림 · 흔들림과 상관없다
        /// </summary>
        float MainShopDepth()
        {
            var field = _mapper.Rect;
            Vector3 shop = field.TransformPoint(field.rect.center + _mapper.Origin);
            return Rect.rect.yMax - Rect.InverseTransformPoint(shop).y;
        }

        /// <summary>
        /// §1.3 올림 규칙 (StageArea 로컬 px, 위 끝에서 아래로 잼): sheetTop = H − 836,
        /// shop = sheetTop ≥ 760k ? sheetTop − 348k : 146k + 0.35·sheetTop (시트가 열렸을 때 수선점이 올 자리),
        /// PanOpen = round_px(max(0, mainShop − shop)), round_px(v) = Round(v·s)/s (먼저 1/32px로 맞춘다 — RuleSnap).
        /// 전투는 아래로 내려가지 않는다 (음수는 0). 테스트에서 비율별 값을 확인할 수 있게 공개.
        /// </summary>
        /// <param name="stageHeight">StageArea 높이 H (기준 px)</param>
        /// <param name="mainShop">MAIN에서 수선점이 StageArea 위 끝에서 얼마나 아래인지 (기준 px)</param>
        /// <param name="k">Ppu / 205</param>
        /// <param name="scale">루트 캔버스 scaleFactor (기준 px 1 = 기기 픽셀 s개, 0 이하면 1)</param>
        public static float ComputePanOpen(float stageHeight, float mainShop, float k, float scale)
        {
            float sheetTop = stageHeight - UIFrame.SheetHeight;
            float shop = sheetTop >= RuleRoomySheetTop * k
                ? sheetTop - RuleShopAboveSheet * k
                : RuleShortShopBase * k + RuleShortShopSlope * sheetTop;
            float raw = Mathf.Round(Mathf.Max(0f, mainShop - shop) * RuleSnap) / RuleSnap;
            if (scale <= 0f) scale = 1f;
            return Mathf.Round(raw * scale) / scale;
        }

        /// <summary>루트 캔버스 scaleFactor (기준 px → 기기 픽셀). 캔버스를 못 찾으면 1</summary>
        float ScaleFactor()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            var root = _canvas != null ? _canvas.rootCanvas : null;
            return root != null && root.scaleFactor > 0f ? root.scaleFactor : 1f;
        }
    }
}
