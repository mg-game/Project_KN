using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전장 위 HUD 비키기 자리 (Wave 3 WP-5B · UX-SPEC-IA §3.2 · §1.6). 전투 화면 루트(Battle) 아래 HUD 칸의 사각형을 입자 레이어 로컬
    /// (= 카메라 · ShakeRoot 로컬 — 액터 · 숫자 · 말풍선 레이어와 같은 좌표)로 한 프레임에 한 번만 잰다.
    /// 켜져 있고 제 묶음 알파(SheetFade)가 KeepOutAlpha 이상인 칸만 센다: SHEET에서 사라지는 추천 알약 · 시너지 줄 · 빠른 줄은 빠지고,
    /// 반투명(0.5)으로 남는 바구니 · 보스 도전 버튼은 남는다. 경로가 '/*'로 끝나면 켜진 자식 칸마다 하나 (빠른 줄 칸 · 시너지 줄의 칩).
    /// 보스 이름표 자리(이름표 + HP 바 묶음)는 EnemyViewPool이 알려 준 살아 있는 적에서 같은 프레임에 한 번 모은다
    /// (말풍선용으로는 그 보스의 몸 자리까지 합친 사각형도 — PlateBodyKeepOut, Wave 3 r2).
    /// 수선점 이름표('수선점' 알약)도 같은 방식으로 (ShopPlateKeepOut — 말풍선만 읽는다).
    /// 읽는 쪽: 적 이름표 · HP 바 묶음(EnemyView → KeepStatusClear), 말풍선(SkillCalloutView), 피해 숫자(옆 빠른 줄 · 보스 이름표).
    /// 할당 없음 (경로는 처음 쓸 때 한 번 찾는다). 재생이 아닐 때(연출 시험장 배치 캡처)는 부를 때마다 다시 잰다.
    /// 시험장 SHEET 변형(DevSheetCover &gt; 0)은 SheetFade가 돌지 않으므로 각 칸의 '시트가 다 열렸을 때 알파'(SheetFade.SheetAlpha)로 센다.
    /// </summary>
    public partial class BattleFx
    {
        /// <summary>
        /// HUD 칸 경로 (Battle 기준 — 코치 · 캡처가 쓰는 이름 그대로). 없는 경로는 건너뛴다 (예전 씬 · 아직 없는 보스 도전 버튼).
        /// 화면 자리 (16:9): 스테이지 카드 14~344 × 142~276 · 배속 794~914 / AUTO 926~1066 × 144~220 · 추천 알약 352~652 × 142~222 (MAIN) ·
        /// 시너지 줄 16~540 × 284~348 (MAIN) · 보스 도전 16~246 × 356~424 · 바구니 906~1056 × 250~420 · 빠른 줄 칸 x ≤ 116 / ≥ 964 (MAIN) ·
        /// 응원 칩 836~948 × 440~488 (PRG-03, 응원이 이어지는 동안만 켜짐 — 꺼져 있으면 건너뛴다. 패배 분석 고리는 결과 대기 중이라 넣지 않는다)
        /// </summary>
        internal static readonly string[] HudKeepOutPaths =
        {
            "Hud/StagePanel",
            "Hud/SpeedButton",
            "Hud/AutoButton",
            "Hud/BossChallengeButton",
            "MetaHud/BossChallengeButton",
            "Hud/SynergyRow/*",
            "MetaHud/RecommendedPower",
            "MetaHud/IdleChestWidget",
            "MetaHud/QuickRail_L/*",
            "MetaHud/QuickRail_R/*",
            "CheerChip",
        };

        const int MaxHudKeepOuts = 24;
        const int MaxPlateKeepOuts = 6;
        /// <summary>이 묶음 알파보다 옅은 HUD 칸은 비키지 않는다 (SheetFade 0으로 사라지는 중)</summary>
        const float KeepOutAlpha = 0.2f;
        /// <summary>자식 칸으로 셀 최대 크기 (빠른 줄 칸 104x116 · 서랍 128 폭 · 시너지 칩 ≤ 280 — 이보다 크면 덮개로 보고 뺀다)</summary>
        const float MaxCellWidth = 560f;
        const float MaxCellHeight = 640f;
        /// <summary>적 이름표 · HP 바 묶음이 HUD · 보이는 띠 위 끝에서 띄우는 틈 (px)</summary>
        public const float StatusGap = 8f;
        /// <summary>보스 몸 자리를 이름표 묶음에 합칠 때 사방으로 넓히는 여유 (px — 말풍선 틈 KeepGap과 같은 6)</summary>
        public const float BodyKeepPad = 6f;

        RectTransform[] _hudTargets;
        CanvasGroup[] _hudGroups;
        SheetFade[] _hudFades;
        bool[] _hudChildren;
        bool _hudLookedUp;
        readonly Rect[] _hudRects = new Rect[MaxHudKeepOuts];
        int _hudRectCount;
        int _hudStamp = -1;
        readonly Vector3[] _keepCorners = new Vector3[4];

        EnemyViewPool _enemyPool;
        readonly Rect[] _plateRects = new Rect[MaxPlateKeepOuts];
        /// <summary>이름표 묶음 주인 (EnemyView.Id = 숫자의 TargetId — 그 보스가 맞은 숫자는 옆으로 밀지 않고 이름표 위 · 아래로)</summary>
        readonly int[] _plateOwners = new int[MaxPlateKeepOuts];
        /// <summary>이름표 묶음 + 몸 자리 (말풍선용 — PlateBodyKeepOut)</summary>
        readonly Rect[] _plateBodyRects = new Rect[MaxPlateKeepOuts];
        int _plateCount;
        int _plateStamp = -1;
        Rect _bannerRect;
        bool _bannerOn;
        int _bannerStamp = -1;
        RectTransform _shopPlate;
        bool _shopPlateLookedUp;
        Rect _shopPlateRect;
        bool _shopPlateOn;
        int _shopPlateStamp = -1;

        /// <summary>적 풀이 깨어날 때 알린다 (보스 이름표 자리를 모으려고 — 직렬화하지 않는다)</summary>
        internal void BindEnemies(EnemyViewPool pool) => _enemyPool = pool;

        /// <summary>지금 비킬 HUD 칸 수 (이번 프레임에 처음 물으면 잰다)</summary>
        public int HudKeepOutCount
        {
            get
            {
                RefreshHudKeepOuts();
                return _hudRectCount;
            }
        }

        /// <summary>i번째 HUD 칸 (입자 레이어 로컬). HudKeepOutCount를 먼저 읽는다</summary>
        public Rect HudKeepOut(int index) => _hudRects[index];

        /// <summary>HUD 칸 배열 그대로 (앞의 HudKeepOutCount개만 이번 프레임 값 — 읽기만, 할당 없음. 말풍선이 보스 자리를 비킬 후보를 거른다)</summary>
        internal Rect[] HudKeepOutRects => _hudRects;

        /// <summary>지금 보이는 보스 이름표 묶음 수 (이름표 + HP 바, 입자 레이어 로컬)</summary>
        public int PlateKeepOutCount
        {
            get
            {
                RefreshPlates();
                return _plateCount;
            }
        }

        public Rect PlateKeepOut(int index) => _plateRects[index];

        /// <summary>
        /// i번째 보스 이름표 묶음 + 그 보스의 몸 자리 (EnemyView.BodyRect를 사방 BodyKeepPad만큼 넓혀 합친 사각형, 입자 레이어 로컬 — PlateKeepOutCount를 먼저 읽는다).
        /// 말풍선만 읽는다 (Wave 3 r2 — SHEET에서 이름표 밑으로 밀린 말풍선이 '망각의 큰그림자'의 머리와 몸을 덮었다).
        /// 피해 숫자는 PlateKeepOut 그대로 (그 보스가 맞은 숫자는 몸 위에서 떠야 한다)
        /// </summary>
        public Rect PlateBodyKeepOut(int index) => _plateBodyRects[index];

        /// <summary>i번째 이름표 묶음의 주인 적 ID (PlateKeepOutCount를 먼저 읽는다)</summary>
        public int PlateKeepOutOwner(int index) => _plateOwners[index];

        /// <summary>
        /// 떠 있는 전장 배너(가운데 배너 · 작은 리본 + 보상 줄 — WP-5A BattleBannerView.TryGetKeepOut)의 자리 (입자 레이어 로컬, 한 프레임에 한 번).
        /// 배너가 없으면 false. 배너는 전장보다 위에 그려지므로 이름표 · 체력 막대는 그 밑에서 흐려지고 말풍선은 비킨다
        /// </summary>
        public bool BannerKeepOut(out Rect rect)
        {
            int frame = Time.frameCount;
            if (_bannerStamp != frame || !Application.isPlaying)
            {
                _bannerStamp = frame;
                _bannerOn = _layer != null && BattleBannerView.TryGetKeepOut(_layer, out _bannerRect);
            }
            rect = _bannerRect;
            return _bannerOn;
        }

        /// <summary>
        /// 수선점 이름표('수선점' 알약 — ShopView.NamePlate, 처음 물을 때 한 번 찾는다)의 자리 (입자 레이어 로컬, 한 프레임에 한 번).
        /// 이름표가 없거나 꺼져 있으면 false. 말풍선이 비킨다 (Wave 3 r1 — SHEET에서 줄어든 전장의 앞줄 친구 말풍선이 이름표를 덮었다)
        /// </summary>
        public bool ShopPlateKeepOut(out Rect rect)
        {
            int frame = Time.frameCount;
            if (_shopPlateStamp != frame || !Application.isPlaying)
            {
                _shopPlateStamp = frame;
                if (!_shopPlateLookedUp)
                {
                    _shopPlateLookedUp = true;
                    _shopPlate = _shop != null ? _shop.NamePlate : null;
                }
                _shopPlateOn = false;
                if (_layer != null && _shopPlate != null && _shopPlate.gameObject.activeInHierarchy)
                {
                    _shopPlate.GetWorldCorners(_keepCorners);
                    Vector2 a = _layer.InverseTransformPoint(_keepCorners[0]);
                    Vector2 b = _layer.InverseTransformPoint(_keepCorners[2]);
                    _shopPlateRect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                    _shopPlateOn = _shopPlateRect.width >= 1f && _shopPlateRect.height >= 1f;
                }
            }
            rect = _shopPlateRect;
            return _shopPlateOn;
        }

        /// <summary>
        /// 적 이름표 · HP 바 묶음(가운데 center, 반크기 half — 입자 레이어 로컬)을 보이는 띠 위 끝 − 8px 아래, HUD 칸 밖으로 옮긴 가운데.
        /// 아래나 옆(전장 안)으로 가장 조금 — SHEET에서 배속/AUTO 밑으로 들어가던 보스 이름표, 바구니 뒤의 HP 바 (Wave 2 r2 battle 항목).
        /// 위로는 옮기지 않는다 (HUD는 모두 위쪽이라 위로 비키면 띠 밖으로 나간다)
        /// </summary>
        public Vector2 KeepStatusClear(Vector2 center, Vector2 half)
        {
            RefreshHudKeepOuts();
            return BattleKeepOut.StatusClear(center, half, _hudRects, _hudRectCount, FieldTop, FieldBottom, FieldHalfWidth, StatusGap);
        }

        /// <summary>HUD 칸 사각형을 다시 잰다 (한 프레임에 한 번)</summary>
        void RefreshHudKeepOuts()
        {
            int frame = Time.frameCount;
            // 배치 캡처(재생 아님)는 한 프레임 안에서 연출 시계를 여러 번 돌리므로 그때는 늘 다시 잰다
            if (_hudStamp == frame && Application.isPlaying) return;
            _hudStamp = frame;
            _hudRectCount = 0;
            if (_layer == null) return;
            if (!_hudLookedUp) LookUpHud();
            if (_hudTargets == null) return;
            for (int i = 0; i < _hudTargets.Length; i++)
            {
                var target = _hudTargets[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                // 시트 흐림(SheetFade — 전용 CanvasGroup은 켜질 때 붙기도 한다)이 있으면 그 알파, 없으면 칸의 CanvasGroup
                var fade = _hudFades[i];
                var group = _hudGroups[i];
                float alpha = fade != null ? fade.Alpha : group != null ? group.alpha : 1f;
                // 연출 시험장의 SHEET 변형(DevSheetCover)은 UIFrame을 건드리지 않아 SheetFade가 돌지 않는다 — 시트가 다 열렸을 때 알파로 흉내 낸다
                if (DevSheetCover > 0f && fade != null) alpha = Mathf.Min(alpha, fade.SheetAlpha);
                if (alpha < KeepOutAlpha) continue;
                if (!_hudChildren[i])
                {
                    AddHudRect(target, false);
                    continue;
                }
                for (int c = 0; c < target.childCount; c++)
                {
                    if (target.GetChild(c) is RectTransform child && child.gameObject.activeInHierarchy) AddHudRect(child, true);
                }
            }
        }

        /// <summary>칸 하나를 더한다. cell이면(빠른 줄 · 시너지 줄의 자식) 칸보다 큰 것 — 열린 서랍의 바깥 누름 판(TrayBlocker, 전체 덮개) 같은 — 은 건너뛴다</summary>
        void AddHudRect(RectTransform target, bool cell)
        {
            if (_hudRectCount >= MaxHudKeepOuts) return;
            target.GetWorldCorners(_keepCorners);
            Vector2 a = _layer.InverseTransformPoint(_keepCorners[0]);
            Vector2 b = _layer.InverseTransformPoint(_keepCorners[2]);
            var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (rect.width < 1f || rect.height < 1f) return;
            if (cell && (rect.width > MaxCellWidth || rect.height > MaxCellHeight)) return;
            _hudRects[_hudRectCount++] = rect;
        }

        /// <summary>HUD 칸을 경로로 한 번 찾는다 (Battle 루트 = BattleView, 없으면 전장 지도의 부모)</summary>
        void LookUpHud()
        {
            _hudLookedUp = true;
            Transform root = null;
            var view = GetComponentInParent<BattleView>(true);
            if (view != null) root = view.transform;
            else if (_mapper != null) root = _mapper.transform.parent;
            if (root == null) return;
            int count = HudKeepOutPaths.Length;
            _hudTargets = new RectTransform[count];
            _hudGroups = new CanvasGroup[count];
            _hudFades = new SheetFade[count];
            _hudChildren = new bool[count];
            for (int i = 0; i < count; i++)
            {
                string path = HudKeepOutPaths[i];
                bool children = path.EndsWith("/*");
                if (children) path = path.Substring(0, path.Length - 2);
                var target = root.Find(path) as RectTransform;
                _hudTargets[i] = target;
                _hudChildren[i] = children;
                if (target == null) continue;
                if (target.TryGetComponent(out SheetFade fade)) _hudFades[i] = fade;
                if (target.TryGetComponent(out CanvasGroup group)) _hudGroups[i] = group;
            }
        }

        /// <summary>살아 있는 보스의 이름표 묶음 자리를 모은다 (한 프레임에 한 번, 풀의 값 목록을 struct 열거자로 — 할당 없음)</summary>
        void RefreshPlates()
        {
            int frame = Time.frameCount;
            if (_plateStamp == frame && Application.isPlaying) return;
            _plateStamp = frame;
            _plateCount = 0;
            if (_enemyPool == null) return;
            foreach (var view in _enemyPool.Active)
            {
                if (_plateCount >= MaxPlateKeepOuts) break;
                if (view == null || !view.StatusRect(out var rect)) continue;
                _plateOwners[_plateCount] = view.Id;
                _plateBodyRects[_plateCount] = view.BodyRect(out var body) ? BattleKeepOut.Union(rect, body, BodyKeepPad) : rect;
                _plateRects[_plateCount++] = rect;
            }
        }
    }
}
