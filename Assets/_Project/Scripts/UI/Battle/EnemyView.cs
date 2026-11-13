using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 적 하나 (풀링). 아트가 없으면 둥근 그림자 덩어리 + 눈으로 그린다.
    /// 걷기 흔들림, 감속(푸른 기·실타래)/기절(별 회전), HP 바(맞은 뒤 표시, 보스는 항상 + 이름표 — 깎인 만큼 하얀 잔상이 버텼다가 줄어든다),
    /// 처치 시 '풀려나며' 사라지고, 수선점에 닿으면 빨려 들어가듯 사라진다.
    /// 피격 손맛 v2 (FX-JUICE E9): 투사체가 닿는 순간(QueueImpact로 예약) 등급 표(HitFeel.Feel — 보스·잔향이·보호막 보정)대로
    ///   실루엣이 크림빛으로 번쩍여 2~3프레임 버티다 InQuad로 꺼지고 (UI/WhiteFlash 덧그림 — RectMask2D에 잘림, 치명타는 그 뒤 공격자 강조색으로 60ms 물듦.
    ///   번쩍임 줄이기면 번쩍임·처치 버팀의 크림빛 × 0.75, 물듦 없음),
    ///   몸이 말랑하게 눌렸다가 용수철(5Hz · 감쇠 0.45)로 한 번 되튀고, 강타·치명타·스킬은 이 적만 멈칫하며 떨고(정지 예산: 어느 0.5초에도 0.15초까지,
    ///   걷는 갈래는 좌우 · 나는 갈래는 위아래), 멈춤이 끝나는 순간 맞은 방향으로 밀렸다(40ms OutExpo) 돌아온다(120ms OutCubic). 멈춘 동안 눌림은 꼭대기에 머문다.
    /// 처치 = '풀려남' (E10 / E13): 마지막 일격이 닿을 때까지 버티고(ExpectImpact / IncomingRemaining, ATK-09), 닿으면 크림빛으로 하얗게 버티며
    ///   떨고(±4px, 보스 ±6) 1.30 / 0.72(보스 1.35 / 0.70)까지 눌렸다가, 터지는 순간 40ms 동안 고르게 1.15배로 톡 부풀고 갈래별로 사라진다:
    ///   먼지 = 살짝 부풀었다 쏙(InBack) · 나방 = 날개 그림이 대신하는 동안 빨리 스밈 · 쇠붙이 = 납작하게 찌그러짐 · 그림자 = 위로 풀림.
    /// 기믹 표시: 보호막(거품 + 하늘색 바, 깨지면 번쩍), 표식(하트), 지속 피해(작은 불꽃), 분노(붉은 기운),
    /// 숨은 그림자(반투명 아른거림 → 드러나면 톡), 분열로 생긴 작은 실뭉치(작게), 주변 회복/가속 오라(바닥 고리).
    /// 적의 공격 (ATK-03 / E9 비대칭): 공격할 때(EnemyState.AttackAnim) 맞는 쪽으로 몸을 16px(보스 30) 휙 내밀었다(OutExpo) 천천히 돌아오고
    /// (Strike로 방향을 받는다), 수선점에 붙은 보스는 다음 공격 전 예비 동작(SiegeWindup) 동안 몸을 뒤로 젖히며 붉어진다.
    /// 보호막에 막힌 명중은 밀리지 않는다 (ATK-11). 등장은 0.2초(보스 0.6초) 동안 또렷해진다 (ATK-15).
    /// 보스 처치·불 밝히기의 전체 정지·느린 화면 동안은 몸 자리도 붙잡는다 — 시뮬레이터는 계속 걸으므로 그만큼을 쌓아 두었다가 풀리면 따라잡는다 (FX-01).
    /// 매 프레임 할당 없음.
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        enum Phase
        {
            Alive,
            Killed,
            ReachedShop,
            /// <summary>스테이지가 바뀌어 제자리에서 흐려지며 사라짐</summary>
            Vanish,
        }

        static readonly Color EnrageRed = new(1f, 0.42f, 0.52f, 1f);
        static readonly Color HiddenTint = new(0.78f, 0.74f, 1f, 1f);
        /// <summary>처치 버팀 동안 몸 그림에 남는 옅은 분홍 (덧그림이 없는 기기에서 처치를 알리는 색)</summary>
        static readonly Color KilledPale = new(1f, 0.6f, 0.66f, 1f);

        [SerializeField] RectTransform _body;
        [SerializeField] Image _sprite;
        [Tooltip("흰 번쩍임 덧그림 (같은 그림, UI/WhiteFlash 머티리얼). 머티리얼이 없으면 옅은 틴트로 대신한다")]
        [SerializeField] Image _flash;
        [SerializeField] Image _blob;
        [SerializeField] Image _shadow;
        [SerializeField] Image _aura;
        [SerializeField] CanvasGroup _group;

        [Header("상태 표시")]
        [SerializeField] RectTransform _hpRoot;
        [Tooltip("보스 HP 바의 분노 눈금 (PRG-02 §13.1 — 2×10, enrageThreshold 자리, 분노하면 Danger + 톡). 없으면 그리지 않는다")]
        [SerializeField] Image _enrageTick;
        [SerializeField] Image _hpFill;
        [SerializeField] Image _hpTrail;
        [SerializeField] GameObject _bossPlate;
        [SerializeField] TMP_Text _bossName;
        [SerializeField] Image _slowRing;
        [SerializeField] RectTransform _stunOrbit;

        [Header("기믹 / 상태 이상")]
        [Tooltip("보호막 바 (HP 바 위)")]
        [SerializeField] RectTransform _shieldRoot;
        [SerializeField] Image _shieldFill;
        [Tooltip("몸을 감싸는 보호막 거품")]
        [SerializeField] Image _shieldBubble;
        [Tooltip("표식 하트 (HP 바 오른쪽)")]
        [SerializeField] Image _markIcon;
        [Tooltip("지속 피해 불꽃 (몸 위)")]
        [SerializeField] Image[] _burnFlames;
        [Tooltip("분노 붉은 기운 (몸 뒤)")]
        [SerializeField] Image _enrageAura;
        [Tooltip("주변 회복/가속 오라 (바닥 고리)")]
        [SerializeField] Image _gimmickRing;

        [Header("크기")]
        [SerializeField] float _normalSize = 78f;
        [SerializeField] float _bossSize = 156f;
        [SerializeField] float _normalBarWidth = 62f;
        [SerializeField] float _bossBarWidth = 170f;
        [Tooltip("분열로 생긴 작은 적의 크기 배율")]
        [SerializeField] float _splitScale = 0.66f;

        Phase _phase;
        float _age;
        float _exitAge;
        float _hit;
        float _trail = 1f;
        float _walkPhase;
        float _size;
        float _unitScale = 1f;
        bool _hpVisible;
        bool _killed;
        /// <summary>처치 후 사라지기 전 버티는 시간 (마지막 일격이 닿고 하얗게 버틴 뒤 터진다 — 처치 꽃도 이만큼 늦게)</summary>
        float _killHold = KillHold;
        /// <summary>처치 보류 안에서 마지막 일격이 닿는 순간 (퇴장 시작 기준, 이때부터 하얗게 버틴다)</summary>
        float _killBlowAt;
        /// <summary>MarkKilled를 받은 이 적의 시계 (퇴장이 늦게 시작되면 그만큼 보류를 줄인다)</summary>
        float _killMarkClock;
        /// <summary>퇴장이 시작될 때의 투명도 (숨은 그림자는 반투명한 채로 풀린다)</summary>
        float _exitAlpha = 1f;
        Vector2 _exitFrom;
        Vector2 _exitTo;
        /// <summary>마지막 걷기 자세 (몸이 통통 튄 높이 — 처치 버팀이 그 자리에서 이어진다)</summary>
        Vector2 _bodyRest;
        Color _slowBase = new(0.58f, 0.78f, 0.94f, 1f);
        bool _slowBaseCaptured;

        float _shieldLevel;
        float _shieldCrack;
        float _mark;
        float _burn;
        float _enrage;
        float _reveal = 1f;
        float _revealPop;
        bool _phasing;
        Vector2 _hopFrom;
        float _hopAge = 1f;
        Color _gimmickColor;

        // ── 피격 손맛 v2 ──
        struct PendingImpact
        {
            public float At;
            public HitTier Tier;
            public Vector2 Direction;
            /// <summary>보호막에 막힌 명중 (밀리지 않고 번쩍임도 약하게, ATK-11)</summary>
            public bool Guarded;
            /// <summary>공격자 강조색 (치명타 뒤 물듦, 없으면 알파 0)</summary>
            public Color Accent;
            /// <summary>멈춰도 되는 명중 (여러 번 닿는 스킬의 가운데 틱은 false)</summary>
            public bool AllowFreeze;
            /// <summary>잔향이의 공격 (HitFeel.ForEcho — 번쩍임 절반 · 넉백 0.6배 · 멈춤 없음)</summary>
            public bool Echo;
        }

        const int ImpactSlots = 4;
        /// <summary>이만큼 가까운 시각에 닿는 명중은 하나로 합친다 (센 등급이 이긴다)</summary>
        const float ImpactMerge = 0.03f;
        /// <summary>이보다 짧게 남은 정지 예산은 쓰지 않는다 (한 프레임도 안 되는 멈춤은 떨림만 남는다)</summary>
        const float MinFreeze = 0.01f;
        /// <summary>눌림 용수철이 사실상 멈추는 나이 (이 뒤로는 계산하지 않는다)</summary>
        const float SpringRest = 0.45f;
        static readonly Color MarkedTint = new(1f, 0.82f, 0.88f, 1f);
        readonly PendingImpact[] _impacts = new PendingImpact[ImpactSlots];
        int _impactCount;
        /// <summary>HP 바 틀 (_hpRoot의 Image, 높이 맞춤용 캐시)</summary>
        Image _hpTrack;
        bool _flashArt;
        // 번쩍임: 세기로 FlashHold 버티고 FlashLife까지 InQuad로 꺼진다 (+ 치명타 물듦)
        float _flashAge = 9f;
        float _flashHold;
        float _flashLife = 0.07f;
        float _flashStrength;
        bool _blush;
        Color _blushColor;
        // 눌림 용수철: X = 1 + _springX·f, Y = 1 − _springY·f (f = 감쇠 용수철 변위)
        float _springAge = 9f;
        float _springX;
        float _springY;
        bool _springKick;
        // 대상 정지(남은 시간 / 이번 정지 길이) · 떨림 폭 · 정지 예산(새는 양동이) · 프레임마다 뒤집는 떨림 방향
        float _stop;
        float _stopTotal;
        float _shiver;
        float _stopLoad;
        float _shiverSign = 1f;
        // 넉백: _knockAt(이 적의 시계)에 _knockFrom에서 출발해 방향 × 거리로 밀렸다 돌아온다 (그 전에는 _knockFrom에 머문다 = 정지)
        Vector2 _knockDirection;
        float _knockDistance;
        float _knockAt = -9999f;
        Vector2 _knockFrom;
        // HP 잔상
        float _ghostFrom = 1f;
        float _ghostAge;
        float _lastRatio = 1f;
        float _incomingUntil;
        /// <summary>마지막으로 명중 반응이 닿은 이 적의 시계 (ApplyImpact — kill_without_arrival 계수)</summary>
        float _lastArrival = -9999f;
        /// <summary>이번 퇴장에서 처치 꽃 순간을 이미 셌는지</summary>
        bool _bloomNoted;
        float _shieldSeen;
        float _shieldBreakAt = -1f;
        /// <summary>이 적의 연출 시계 (Tick/TickExit dt 합 — 전체 정지·느린 화면이 걸리면 함께 느려진다)</summary>
        float _clock;
        // 전체 정지·느린 화면 동안 자리 붙잡기 (FX-01): 연출 시간만 멈추고 시뮬레이터는 계속 걷는다 — 보이는 자리 = 시뮬레이터 자리 + _holdOffset
        Vector2 _holdOffset;
        /// <summary>지난 갱신 때의 시뮬레이터 자리 (전장 px — 이번 프레임에 시뮬레이터가 움직인 양을 잰다)</summary>
        Vector2 _simField;
        bool _simFieldValid;

        // ── 공격 / 처치 갈래 ──
        /// <summary>마지막 공격 방향 (전장 로컬, 단위 벡터 — 없으면 수선점 쪽)</summary>
        Vector2 _strikeDir;
        EnemyDeathFamily _family;

        // ── 상태 표시 묶음 비키기 (Wave 3 WP-5B: 이름표 · HP 바 · 보호막 바 · 표식이 HUD 밑으로 들어가지 않게) ──
        /// <summary>묶음 오프셋이 목표로 미끄러지는 빠르기 (전장 px/s — 옆·아래 고르기가 바뀌어도 툭 튀지 않게)</summary>
        const float StatusSlideSpeed = 900f;
        /// <summary>HUD 비키기를 물어볼 연출 (풀이 넣어 준다 — 없으면 보이는 띠 위 끝만 지킨다)</summary>
        internal BattleFx KeepOut;
        RectTransform _plateRect;
        /// <summary>Bind 때 정한 기준 자리 (루트 로컬): HP 바 · 이름표 · 보호막 바 높이, 표식 x</summary>
        float _barY;
        float _plateY;
        float _shieldY;
        float _markX;
        /// <summary>묶음 범위 (루트 로컬, 발 기준): 위 끝 · 아래 끝 · 반폭</summary>
        float _statusTop;
        float _statusBottom;
        float _statusHalf;
        /// <summary>지금 그린 묶음 오프셋 (루트 로컬) / 마지막으로 쓴 값 / 다음 갱신은 바로 맞춘다</summary>
        Vector2 _statusOffset;
        Vector2 _statusApplied;
        bool _statusSnap = true;
        /// <summary>지금 보이는 보스 이름표 묶음 (입자 레이어 로컬 — 말풍선 · 숫자가 비킨다)</summary>
        Rect _statusBox;
        bool _statusBoxValid;
        /// <summary>
        /// 배너 밑 흐림 (WP-5A 배너 띠와 겹치면 0.12초에 0으로, 벗어나면 다시 1): 이름표 · HP 바 · 보호막 바 묶음 알파 (Awake에서 한 번 붙인다 — 빌더가 붙여 두면 그대로).
        /// 표식 하트는 TickGimmicks가 쓰는 색 알파에 같이 곱한다 (묶음이 함께 사라진다 — 보호막 바 · 하트만 배너 가장자리에 떠 있지 않게)
        /// </summary>
        const float StatusFadeTime = 0.12f;
        CanvasGroup _plateGroup;
        CanvasGroup _hpGroup;
        CanvasGroup _shieldGroup;
        float _statusFade = 1f;
        /// <summary>
        /// 위 끝에 눌려 내려온 보스 이름표 묶음이 보스 몸을 덮으면 이름표 받침(BossPlate 그림 + 밑줄 Edge)만 원래 알파 × 이 값으로
        /// (UX Wave 3 이월 d — SHEET에서 이름표 받침이 보스 머리를 가렸다). 보스 이름 글자는 그대로 (묶음 CanvasGroup은 배너 밑 흐림 _statusFade만)
        /// </summary>
        public const float PlateOverBodyAlpha = 0.75f;
        /// <summary>이름표가 몸을 덮었다고 볼 겹침 (가로 · 세로 모두 이만큼 px 이상)</summary>
        public const float PlateOverBodyMin = 12f;
        float _plateDim = 1f;
        /// <summary>이름표 받침 그림 · 밑줄 (BossPlate의 Image · 자식 'Edge' — 처음 한 번 찾고 원래 알파를 적어 둔다, 없어도 된다)</summary>
        Graphic _plateBack;
        Graphic _plateEdge;
        float _plateBackAlpha = 1f;
        float _plateEdgeAlpha = 1f;
        bool _plateBackFound;

        public int Id { get; private set; } = -1;
        public EnemyState State { get; private set; }
        public bool IsBoss { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        /// <summary>투사체/피해 숫자가 향하는 몸 가운데 (전장 로컬 좌표)</summary>
        public Vector2 HitPoint => Rect.anchoredPosition + Vector2.up * (_size * 0.45f * Rect.localScale.y);
        /// <summary>머리 위 (기절 별·표식이 뜨는 높이, 전장 로컬 좌표)</summary>
        public Vector2 HeadPoint => Rect.anchoredPosition + Vector2.up * ((_size * 0.98f + 18f) * Rect.localScale.y);
        public bool IsTargetable => isActiveAndEnabled && _phase == Phase.Alive;
        public Color BodyColor { get; private set; } = UITheme.Enemy;
        /// <summary>처치 연출 갈래 (먼지·나방·쇠붙이·그림자)</summary>
        public EnemyDeathFamily Family => _family;
        /// <summary>숨어 있는 그림자 (공격 그림도 반투명하게)</summary>
        public bool IsHidden => _phasing && State != null && !State.Revealed;
        /// <summary>시뮬레이터 목록에서 마지막으로 본 프레임 (풀이 사라진 적을 찾을 때 사용)</summary>
        internal int SeenStamp;

        void Awake()
        {
            CaptureSlowColor();
            PrepareStatusGroups();
        }

        /// <summary>이름표 · HP 바 · 보호막 바의 묶음 알파 (배너 밑 흐림용 — 없으면 한 번 붙인다, 풀 객체마다 처음 한 번만 할당)</summary>
        void PrepareStatusGroups()
        {
            if (_bossPlate != null && _plateGroup == null && !_bossPlate.TryGetComponent(out _plateGroup)) _plateGroup = _bossPlate.AddComponent<CanvasGroup>();
            if (_hpRoot != null && _hpGroup == null && !_hpRoot.TryGetComponent(out _hpGroup)) _hpGroup = _hpRoot.gameObject.AddComponent<CanvasGroup>();
            if (_shieldRoot != null && _shieldGroup == null && !_shieldRoot.TryGetComponent(out _shieldGroup))
                _shieldGroup = _shieldRoot.gameObject.AddComponent<CanvasGroup>();
            if (_shieldGroup != null)
            {
                _shieldGroup.blocksRaycasts = false;
                _shieldGroup.interactable = false;
            }
            if (_plateGroup != null)
            {
                _plateGroup.blocksRaycasts = false;
                _plateGroup.interactable = false;
            }
            if (_hpGroup != null)
            {
                _hpGroup.blocksRaycasts = false;
                _hpGroup.interactable = false;
            }
            if (_bossPlate != null && !_plateBackFound)
            {
                _plateBackFound = true;
                if (_bossPlate.TryGetComponent(out _plateBack)) _plateBackAlpha = _plateBack.color.a;
                var edge = _bossPlate.transform.Find("Edge");
                if (edge != null && edge.TryGetComponent(out _plateEdge)) _plateEdgeAlpha = _plateEdge.color.a;
            }
        }

        void CaptureSlowColor()
        {
            if (_slowBaseCaptured || _slowRing == null) return;
            _slowBaseCaptured = true;
            _slowBase = _slowRing.color;
            _slowBase.a = 1f;
        }

        bool _tickEnraged;
        float _tickPop = -1f;
        static readonly Color EnrageTickCream = new(UITheme.Cream.r, UITheme.Cream.g, UITheme.Cream.b, 0.85f);

        /// <summary>보스만: 분노 눈금을 enrageThreshold 자리에 Cream 0.85로 (분노한 채 다시 그려지면 Danger)</summary>
        void BindEnrageTick()
        {
            if (_enrageTick == null) return;
            _enrageTick.enabled = IsBoss;
            _tickEnraged = State != null && State.Enraged;
            _tickPop = -1f;
            if (!IsBoss) return;
            float threshold = Game.IsReady && Game.Battle != null && Game.Battle.Config != null ? Mathf.Clamp01(Game.Battle.Config.enrageThreshold) : 0.5f;
            var rt = _enrageTick.rectTransform;
            rt.anchorMin = new Vector2(threshold, rt.anchorMin.y);
            rt.anchorMax = new Vector2(threshold, rt.anchorMax.y);
            rt.localScale = Vector3.one;
            _enrageTick.color = _tickEnraged ? UITheme.Danger : EnrageTickCream;
        }

        /// <summary>분노 순간: 눈금이 Danger로 바뀌며 한 번 톡 (말로는 분노 알림이 이미 알린다). 할당 없음</summary>
        void TickEnrageTick(float dt)
        {
            if (_enrageTick == null || !IsBoss || State == null) return;
            if (State.Enraged && !_tickEnraged)
            {
                _tickEnraged = true;
                _enrageTick.color = UITheme.Danger;
                _tickPop = UIMotion.Reduced ? -1f : 0f;
            }
            if (_tickPop < 0f) return;
            _tickPop += dt;
            float k = Mathf.Clamp01(_tickPop / 0.3f);
            float s = 1f + 0.6f * BattleEase.Punch(k);
            _enrageTick.rectTransform.localScale = new Vector3(s, s, 1f);
            if (k >= 1f)
            {
                _tickPop = -1f;
                _enrageTick.rectTransform.localScale = Vector3.one;
            }
        }

        public void Bind(EnemyState state, BattleFieldMapper mapper)
        {
            CaptureSlowColor();
            State = state;
            Id = state.Id;
            IsBoss = state.IsBoss || (state.Def != null && state.Def.isBoss);
            _phase = Phase.Alive;
            _age = 0f;
            _exitAge = 0f;
            _hit = 0f;
            _trail = 1f;
            _killed = false;
            _killHold = KillHold;
            _killBlowAt = 0f;
            _killMarkClock = 0f;
            _exitAlpha = 1f;
            _bodyRest = Vector2.zero;
            KilledBy = HitTier.Normal;
            _walkPhase = Random.Range(0f, Mathf.PI * 2f);
            _size = IsBoss ? _bossSize : _normalSize;
            _unitScale = state.IsSplit && !IsBoss ? _splitScale : 1f;
            _hpVisible = IsBoss;
            _shieldLevel = state.Shield > 0f ? 1f : 0f;
            _shieldCrack = 0f;
            _mark = 0f;
            _burn = 0f;
            _enrage = state.Enraged ? 1f : 0f;
            var gimmicks = state.Def != null ? state.Def.gimmicks : EnemyGimmick.None;
            _phasing = (gimmicks & EnemyGimmick.Phasing) != 0;
            _reveal = state.Revealed ? 1f : 0f;
            _revealPop = 0f;
            _hopAge = 1f;
            _hopFrom = Vector2.zero;
            _strikeDir = Vector2.zero;
            _family = EnemyFxCatalog.Family(state.Def != null ? state.Def.id : null);
            ResetFeel(state);

            var sprite = state.Def != null ? state.Def.sprite : null;
            BodyColor = BlobColor(state.Def != null ? state.Def.id : null, IsBoss);
            _sprite.sprite = sprite;
            _sprite.enabled = sprite != null;
            BindFlash(sprite);
            _blob.gameObject.SetActive(sprite == null);
            _blob.color = BodyColor;

            _body.sizeDelta = new Vector2(_size, _size);
            _body.localScale = Vector3.zero;
            _body.localRotation = Quaternion.identity;
            _body.anchoredPosition = Vector2.zero;
            _shadow.rectTransform.sizeDelta = new Vector2(_size * 0.75f, _size * 0.22f);

            float barWidth = IsBoss ? _bossBarWidth : _normalBarWidth;
            float barY = _size * 0.98f + 8f;
            float barHeight = IsBoss ? 16f : 10f;
            _hpRoot.sizeDelta = new Vector2(barWidth, barHeight);
            FitHpBar(barHeight);
            _hpRoot.anchoredPosition = new Vector2(0f, barY);
            _hpRoot.gameObject.SetActive(_hpVisible);
            BindEnrageTick();
            BattleEase.SetBar(_hpFill, 1f);
            BattleEase.SetBar(_hpTrail, 1f);

            _plateY = _size * 0.98f + 24f;
            if (_bossPlate != null)
            {
                _bossPlate.SetActive(IsBoss);
                _plateRect = (RectTransform)_bossPlate.transform;
                _plateRect.anchoredPosition = new Vector2(0f, _plateY);
            }
            if (_bossName != null) _bossName.text = IsBoss && state.Def != null ? state.Def.name : string.Empty;
            if (_aura != null)
            {
                _aura.gameObject.SetActive(IsBoss);
                _aura.rectTransform.sizeDelta = new Vector2(_size * 1.6f, _size * 1.6f);
                _aura.rectTransform.anchoredPosition = new Vector2(0f, _size * 0.45f);
            }
            if (_slowRing != null) _slowRing.enabled = false;
            if (_stunOrbit != null)
            {
                _stunOrbit.gameObject.SetActive(false);
                _stunOrbit.anchoredPosition = new Vector2(0f, _size * 0.92f);
            }
            BindGimmicks(state, mapper, barWidth, barY, gimmicks);
            BindStatus(barWidth, barY, barHeight);
            _group.alpha = 0f;
            _simFieldValid = false;
            _holdOffset = Vector2.zero;
            Place(state.Position, mapper, 1f, 0f);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 상태 표시 묶음(HP 바 · 보스 이름표 · 보호막 바 · 표식)의 기준 자리와 범위를 적어 둔다 (루트 로컬, 발 기준).
        /// 보스: HP 바 아래 끝 ~ 이름표 위 끝(약 153 ~ 213, 반폭 110) · 일반: HP 바 아래 끝 ~ 보호막 바 위 끝, 반폭 = 바 반폭 + 표식 28
        /// </summary>
        void BindStatus(float barWidth, float barY, float barHeight)
        {
            _barY = barY;
            _shieldY = barY + (IsBoss ? 14f : 10f);
            _markX = barWidth * 0.5f + 14f;
            _statusBottom = barY - barHeight * 0.5f;
            _statusTop = Mathf.Max(barY + barHeight * 0.5f, _shieldY + 3.5f);
            _statusHalf = barWidth * 0.5f + 28f;
            if (IsBoss && _plateRect != null)
            {
                var plate = _plateRect.sizeDelta;
                _statusTop = Mathf.Max(_statusTop, _plateY + plate.y);
                _statusHalf = Mathf.Max(_statusHalf, plate.x * 0.5f);
            }
            _statusOffset = Vector2.zero;
            _statusSnap = true;
            _statusBoxValid = false;
            ApplyStatus(Vector2.zero);
            PrepareStatusGroups();
            _plateDim = 1f;
            SetStatusFade(1f);
        }

        void SetStatusFade(float fade)
        {
            _statusFade = fade;
            if (_plateGroup != null) _plateGroup.alpha = fade;
            ApplyPlateDim(_plateBack, _plateBackAlpha, _plateEdge, _plateEdgeAlpha, _plateDim);
            if (_hpGroup != null) _hpGroup.alpha = fade;
            if (_shieldGroup != null) _shieldGroup.alpha = fade;
            // 표식 하트: 다음 TickGimmicks가 _mark × 흐림으로 다시 쓴다 — 하트가 꺼져 있으면(_mark 0) 건드리지 않는다
            if (_markIcon != null && _markIcon.enabled) _markIcon.color = BattleEase.WithAlpha(_markIcon.color, _mark * fade);
        }

        /// <summary>묶음 오프셋(루트 로컬)을 HP 바 · 이름표 · 보호막 바 · 표식에 쓴다</summary>
        void ApplyStatus(Vector2 offset)
        {
            _statusApplied = offset;
            _hpRoot.anchoredPosition = new Vector2(offset.x, _barY + offset.y);
            if (_plateRect != null) _plateRect.anchoredPosition = new Vector2(offset.x, _plateY + offset.y);
            if (_shieldRoot != null) _shieldRoot.anchoredPosition = new Vector2(offset.x, _shieldY + offset.y);
            if (_markIcon != null) _markIcon.rectTransform.anchoredPosition = new Vector2(_markX + offset.x, _barY + offset.y);
        }

        /// <summary>
        /// 상태 표시 묶음을 HUD 밑으로 들어가지 않게 (Wave 3 WP-5B · Wave 2 r2 battle 항목 — SHEET에서 '망각의 큰그림자' 이름표가 배속/AUTO 밑, HP 바가 바구니 뒤):
        /// 묶음이 보이면 전장 로컬 상자로 재서 BattleFx.KeepStatusClear(보이는 띠 위 끝 − 8px 아래, HUD 칸 밖 — 아래나 옆으로 가장 조금)에 맞추고,
        /// 오프셋은 900px/s로 미끄러진다 (처음 보일 때는 바로). 묶음이 안 보이면 0으로 돌려 둔다. 보스 이름표 자리는 말풍선 · 숫자가 비키도록 남긴다
        /// </summary>
        void PlaceStatus(BattleFieldMapper mapper, float dt)
        {
            bool plate = _plateRect != null && _bossPlate.activeSelf;
            bool shown = plate || _hpRoot.gameObject.activeSelf || (_shieldRoot != null && _shieldRoot.gameObject.activeSelf) ||
                         (_markIcon != null && _markIcon.enabled);
            float scale = Mathf.Max(0.01f, Rect.localScale.y);
            var target = Vector2.zero;
            var center = Vector2.zero;
            var half = Vector2.zero;
            if (shown)
            {
                var feet = Rect.anchoredPosition;
                center = new Vector2(feet.x, feet.y + (_statusTop + _statusBottom) * 0.5f * scale);
                half = new Vector2(_statusHalf * scale, (_statusTop - _statusBottom) * 0.5f * scale);
                Vector2 clear;
                if (KeepOut != null) clear = KeepOut.KeepStatusClear(center, half);
                else
                {
                    clear = center;
                    float top = mapper != null ? mapper.VisibleTop - BattleFx.StatusGap - half.y : float.MaxValue;
                    if (clear.y > top) clear.y = top;
                }
                target = (clear - center) / scale;
            }
            // 움직임 줄이기(UIMotion.Reduced)면 미끄러지지 않고 바로
            if (_statusSnap || !shown || UIMotion.Reduced) _statusOffset = target;
            else _statusOffset = Vector2.MoveTowards(_statusOffset, target, StatusSlideSpeed * dt / scale);
            _statusSnap = !shown;
            if (_statusOffset != _statusApplied) ApplyStatus(_statusOffset);
            _statusBoxValid = plate && shown;
            if (_statusBoxValid) _statusBox = new Rect(center + _statusOffset * scale - half, half * 2f);
            // 배너 밑에서는 흐린다 (배너가 위에 그려진다 — 이름표가 배너 둘레로 삐져나와 어수선하지 않게 · WP-5A 배너 띠 약속)
            bool underBanner = shown && KeepOut != null && KeepOut.BannerKeepOut(out var banner) &&
                               BattleKeepOut.Overlaps(center + _statusOffset * scale, half, banner, 0f);
            float fade = Mathf.MoveTowards(_statusFade, underBanner ? 0f : 1f, dt / StatusFadeTime);
            if (!shown) fade = 1f;
            // 위 끝에 눌려 내려온 이름표가 몸을 덮으면 이름표 받침만 0.75배로 (몸이 비친다 — 이월 d, 보스 이름 글자는 그대로)
            bool overBody = _statusBoxValid && _statusOffset.y < -0.5f && BodyRect(out var body) && PlateCoversBody(_statusBox, body);
            float dim = overBody ? PlateOverBodyAlpha : 1f;
            if (dim != _plateDim)
            {
                _plateDim = dim;
                SetStatusFade(fade);
            }
            else if (fade != _statusFade) SetStatusFade(fade);
        }

        /// <summary>이름표 받침(그림 · 밑줄)만 원래 알파 × dim으로 (글자는 건드리지 않는다 — 값이 같으면 Graphic이 다시 그리지 않는다)</summary>
        internal static void ApplyPlateDim(Graphic back, float backAlpha, Graphic edge, float edgeAlpha, float dim)
        {
            if (back != null) back.color = BattleEase.WithAlpha(back.color, backAlpha * dim);
            if (edge != null) edge.color = BattleEase.WithAlpha(edge.color, edgeAlpha * dim);
        }

        /// <summary>이름표 묶음 상자가 몸 상자와 가로 · 세로 모두 PlateOverBodyMin 이상 겹치는지 (같은 레이어 로컬, 순수)</summary>
        public static bool PlateCoversBody(Rect status, Rect body)
        {
            float width = Mathf.Min(status.xMax, body.xMax) - Mathf.Max(status.xMin, body.xMin);
            float height = Mathf.Min(status.yMax, body.yMax) - Mathf.Max(status.yMin, body.yMin);
            return width >= PlateOverBodyMin && height >= PlateOverBodyMin;
        }

        /// <summary>보이는 보스 이름표 묶음 (이름표 + HP 바, 부모 레이어 로컬 — 말풍선 · 숫자가 비킨다). 살아 있는 보스만</summary>
        public bool StatusRect(out Rect rect)
        {
            rect = _statusBox;
            return _statusBoxValid && _phase == Phase.Alive && isActiveAndEnabled;
        }

        /// <summary>
        /// 몸 그림 자리 (쉬는 자세의 몸 상자 _size × 배율 — 발밑 pivot 기준, 걷기 흔들림 · 눌림 · 넉백은 넣지 않는다 — 부모 레이어 로컬).
        /// 살아 있을 때만 true. 보스 이름표 묶음과 합쳐 말풍선이 몸도 비킨다 (Wave 3 r2 — SHEET에서 이름표 밑으로 밀린 말풍선이 보스 머리와 몸을 덮었다).
        /// 할당 없음
        /// </summary>
        public bool BodyRect(out Rect rect)
        {
            var scale = Rect.localScale;
            float width = _size * Mathf.Abs(scale.x);
            float height = _size * Mathf.Abs(scale.y);
            var pivot = _body != null ? _body.pivot : new Vector2(0.5f, 0f);
            var feet = Rect.anchoredPosition;
            rect = new Rect(feet.x - pivot.x * width, feet.y - pivot.y * height, width, height);
            return _phase == Phase.Alive && isActiveAndEnabled && width >= 1f && height >= 1f;
        }

        /// <summary>
        /// HP 바 높이가 바뀌면(보스 16px / 일반 10px — 같은 풀 템플릿) 캡슐 틀 · 채움 · 잔상의 9-slice 배율도 맞춘다.
        /// 빌더 배율은 10px 기준이라 그대로 두면 보스 바 끝이 각진 둥근 사각형이 된다. 채움 높이 = 틀 높이 - 위아래 여백
        /// </summary>
        void FitHpBar(float height)
        {
            if (_hpTrack == null) _hpTrack = _hpRoot.GetComponent<Image>();
            BattleEase.FitCapsule(_hpTrack, height);
            FitBarFill(_hpFill, height);
            FitBarFill(_hpTrail, height);
        }

        static void FitBarFill(Image fill, float trackHeight)
        {
            if (fill == null) return;
            var rect = fill.rectTransform;
            BattleEase.FitCapsule(fill, trackHeight * (rect.anchorMax.y - rect.anchorMin.y) - rect.offsetMin.y + rect.offsetMax.y);
        }

        void BindGimmicks(EnemyState state, BattleFieldMapper mapper, float barWidth, float barY, EnemyGimmick gimmicks)
        {
            if (_shieldRoot != null)
            {
                _shieldRoot.sizeDelta = new Vector2(barWidth, 7f);
                _shieldRoot.anchoredPosition = new Vector2(0f, barY + (IsBoss ? 14f : 10f));
                _shieldRoot.gameObject.SetActive(state.Shield > 0f);
            }
            if (_shieldBubble != null)
            {
                _shieldBubble.rectTransform.sizeDelta = new Vector2(_size * 1.35f, _size * 1.35f);
                _shieldBubble.rectTransform.anchoredPosition = new Vector2(0f, _size * 0.45f);
                _shieldBubble.enabled = state.Shield > 0f;
            }
            if (_markIcon != null)
            {
                _markIcon.rectTransform.anchoredPosition = new Vector2(barWidth * 0.5f + 14f, barY);
                _markIcon.enabled = false;
            }
            if (_burnFlames != null)
            {
                for (int i = 0; i < _burnFlames.Length; i++)
                {
                    if (_burnFlames[i] == null) continue;
                    float x = (i - (_burnFlames.Length - 1) * 0.5f) * _size * 0.32f;
                    _burnFlames[i].rectTransform.anchoredPosition = new Vector2(x, _size * (0.25f + 0.12f * (i % 2)));
                    _burnFlames[i].enabled = false;
                }
            }
            if (_enrageAura != null)
            {
                _enrageAura.rectTransform.sizeDelta = new Vector2(_size * 1.9f, _size * 1.9f);
                _enrageAura.rectTransform.anchoredPosition = new Vector2(0f, _size * 0.45f);
                _enrageAura.enabled = false;
            }
            if (_gimmickRing != null)
            {
                float radius = state.Def != null ? state.Def.auraRadius : 0f;
                bool mender = (gimmicks & EnemyGimmick.Mender) != 0;
                bool hastener = (gimmicks & EnemyGimmick.Hastener) != 0;
                bool show = radius > 0.01f && (mender || hastener);
                _gimmickRing.enabled = show;
                if (show)
                {
                    float scale = Mathf.Max(0.01f, mapper.ActorScale * _unitScale);
                    float diameter = radius * 2f * mapper.Ppu / scale;
                    _gimmickRing.rectTransform.sizeDelta = new Vector2(diameter, diameter * mapper.Squash);
                    _gimmickColor = mender ? UITheme.Positive : UITheme.Gold;
                }
            }
        }

        /// <summary>살아 있는 동안 매 프레임 갱신 (연출 시간 배율을 모르는 곳 — 시험장 허수아비: dt가 0이면 멈춘 것으로 본다)</summary>
        public void Tick(BattleFieldMapper mapper, float dt) => Tick(mapper, dt, dt > 0f ? 1f : 0f);

        /// <summary>
        /// 살아 있는 동안 매 프레임 갱신. dt = 연출 시계, timeScale = 연출 시간 배율 (BattleFx.TimeScale — 보스 처치 전체 정지 0 · 느린 화면 0.3 ·
        /// 불 밝히기 점화 0.2 · 보통 1). 1보다 작으면 시뮬레이터가 그동안 걸은 만큼 몸 자리를 붙잡아 걷기 흔들림·눌림·입자와 함께 멈추고
        /// (정지가 미끄러짐으로 보이지 않게), 다시 흐르면 0.17초 남짓에 따라잡는다 (EnemyFxCatalog.HoldOffset)
        /// </summary>
        /// <param name="alpha">고정 진행 사이 보간 비율 (BattleSimulator.RenderAlpha). 1 = 시뮬레이터 자리 그대로 (시험장 허수아비 — Prev 값을 읽지 않는다)</param>
        public void Tick(BattleFieldMapper mapper, float dt, float timeScale, float alpha = 1f)
        {
            if (State == null) return;
            _age += dt;
            if (_hopAge < 1f) _hopAge += dt / 0.38f;
            // 보간한 자리 (perf PF-2 F1): 프레임에 진행이 0 · 2번이어도 몸이 고르게 걷는다
            Place(alpha >= 1f ? State.Position : Vector2.LerpUnclamped(State.PrevPosition, State.Position, alpha), mapper, timeScale, dt);
            _shieldSeen = State.Shield;
            TickFeel(dt);

            float spawn = Mathf.Clamp01(_age / 0.35f);
            _hit = Mathf.Max(0f, _hit - dt * 5f);
            // 피격 펀치는 투사체가 닿는 순간(예약된 명중)에 — 시뮬레이터 HitAnim은 쏘는 순간이라 쓰지 않는다
            float hit = _hit;
            bool stunned = State.StunTimer > 0f;
            bool slowed = State.SlowTimer > 0f;
            _enrage = BattleEase.Damp(_enrage, State.Enraged ? 1f : 0f, 6f, dt);
            TickEnrageTick(dt);
            _reveal = BattleEase.Damp(_reveal, State.Revealed ? 1f : 0f, 8f, dt);
            _revealPop = Mathf.Max(0f, _revealPop - dt * 3f);

            // 공격: 맞는 쪽으로 휙 내밀었다 천천히 돌아온다 (AttackAnim 1 → 0, 0.25초 — 앞 20% OutExpo, 나머지 OutCubic) /
            // 보스 예비 동작: 뒤로 젖히며 붉어진다
            // 내밀기 자세도 보간 (perf PF-2 F4): 새 공격(값이 올라감)은 바로 1부터, 줄어드는 동안은 진행 사이를 고르게
            float strike = Mathf.Clamp01(alpha >= 1f || State.AttackAnim > State.PrevAttackAnim
                ? State.AttackAnim
                : Mathf.LerpUnclamped(State.PrevAttackAnim, State.AttackAnim, alpha));
            float lunge = strike > 0f ? EnemyFxCatalog.LungeProfile(1f - strike) : 0f;
            float windup = Mathf.Clamp01(State.SiegeWindup);
            var strikeDir = StrikeDirection(mapper);

            // 걷기: 좌우로 뒤뚱이며 통통 튄다 (감속이면 느리게, 기절이면 멈춤, 분노면 빠르게, 공격 중·맞고 멈춘 동안엔 멈칫)
            float walkSpeed = stunned || _stop > 0f || lunge > 0.1f || windup > 0f ? 0f : slowed ? 4f : 8.5f + 4f * _enrage;
            _walkPhase += dt * walkSpeed;
            float step = Mathf.Sin(_walkPhase);
            float bounce = Mathf.Abs(step);
            float appear = BattleEase.OutBack(spawn, 2.2f);
            float hop = _hopAge < 1f ? Mathf.Sin(Mathf.Clamp01(_hopAge) * Mathf.PI) * 40f : 0f;
            float revealPunch = BattleEase.Punch(1f - _revealPop) * 0.25f;
            // 맞으면 말랑하게 눌렸다가 용수철로 되튀고 (멈춘 동안은 꼭대기), 공격하는 순간 앞으로 늘었다가 눌리고, 예비 동작 동안은 납작하게 웅크린다
            float spring = SpringNow();
            float stretch = 0.1f * lunge;
            _body.localScale = new Vector3(appear * (1f + _springX * spring - 0.05f * bounce + revealPunch - 0.04f * lunge + 0.08f * windup),
                appear * (1f - _springY * spring + 0.05f * bounce + revealPunch + stretch - 0.1f * windup), 1f);
            Vector2 jitter;
            if (_stop > 0f) jitter = ShiverOffset();
            else
            {
                float x = stunned ? Mathf.Sin(_age * 40f) * 2f
                    : _enrage > 0.5f ? Mathf.Sin(_age * 55f) * 1.5f * _enrage
                    : windup > 0.6f ? Mathf.Sin(_age * 70f) * 1.5f * windup : 0f;
                jitter = new Vector2(x, 0f);
            }
            _bodyRest = new Vector2(0f, bounce * (IsBoss ? 6f : 4f) + hop);
            _body.anchoredPosition = jitter + _bodyRest + KnockOffset() + StrikeOffset(strikeDir, lunge, windup);
            float lean = (IsBoss ? 10f : 14f) * (lunge - 0.8f * windup) * -Mathf.Sign(strikeDir.x == 0f ? 1f : strikeDir.x);
            _body.localRotation = Quaternion.Euler(0f, 0f, step * (IsBoss ? 3f : 5f) + lean);

            // 등장: 0.2초(보스는 그림자 웅덩이에서 0.6초) 동안 또렷해진다 / 숨은 그림자: 반투명하게 아른거리다가 드러나면 또렷해진다
            float fade = Mathf.Clamp01(_age / (IsBoss ? EnemyFxCatalog.BossPoolLife : EnemyFxCatalog.SpawnFadeTime));
            float hidden = _phasing ? 1f - _reveal : 0f;
            float shimmer = 0.3f + 0.08f * Mathf.Sin(_age * 3.4f);
            _group.alpha = fade * Mathf.Lerp(1f, shimmer, hidden);

            // 흰 번쩍임 덧그림이 없을 때만 옛 붉은 펀치 틴트로 대신한다. 분홍 기는 이제 약화(표식) 표시
            var tint = _flashArt ? Color.white : Color.Lerp(Color.white, new Color(1f, 0.5f, 0.58f), hit * 0.85f);
            if (_mark > 0.02f) tint *= Color.Lerp(Color.white, MarkedTint, _mark);
            if (slowed) tint *= new Color(0.72f, 0.86f, 1f);
            if (stunned) tint *= new Color(1f, 0.95f, 0.7f);
            if (_enrage > 0.01f) tint = Color.Lerp(tint, tint * new Color(1f, 0.7f, 0.74f), _enrage * (0.6f + 0.4f * Mathf.Sin(_age * 9f)));
            if (windup > 0.01f) tint = Color.Lerp(tint, tint * new Color(1f, 0.62f, 0.68f), windup * 0.7f);
            if (hidden > 0.01f) tint = Color.Lerp(tint, tint * HiddenTint, hidden);
            if (_sprite.enabled) _sprite.color = tint;
            else _blob.color = Color.Lerp(BodyColor * tint, Color.white, DrawnFlash * 0.85f);
            DrawFlash(1f - hidden * 0.6f);

            if (_slowRing != null)
            {
                _slowRing.enabled = slowed;
                if (slowed)
                {
                    _slowRing.color = BattleEase.WithAlpha(_slowBase, 0.45f + 0.25f * Mathf.Sin(_age * 6f));
                    _slowRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _age * 25f);
                }
            }
            if (_stunOrbit != null)
            {
                if (_stunOrbit.gameObject.activeSelf != stunned) _stunOrbit.gameObject.SetActive(stunned);
                if (stunned) _stunOrbit.localRotation = Quaternion.Euler(0f, 0f, _age * 360f);
            }
            if (_aura != null && IsBoss)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_age * (3f + 5f * _enrage));
                _aura.color = BattleEase.WithAlpha(Color.Lerp(UITheme.Danger, EnrageRed, _enrage), 0.25f + 0.2f * pulse + 0.25f * _enrage);
                float scale = 1f + 0.18f * _enrage * pulse;
                _aura.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            TickGimmicks(dt, hit, hidden);
            UpdateHp(dt, hidden);
            PlaceStatus(mapper, dt);
        }

        void TickGimmicks(float dt, float hit, float hidden)
        {
            float shield = State.Shield;
            bool shielded = shield > 0.01f;
            _shieldLevel = BattleEase.Damp(_shieldLevel, shielded ? 1f : 0f, 10f, dt);
            _shieldCrack = Mathf.Max(0f, _shieldCrack - dt * 2.5f);
            if (_shieldRoot != null)
            {
                bool show = shielded && hidden < 0.5f;
                if (_shieldRoot.gameObject.activeSelf != show) _shieldRoot.gameObject.SetActive(show);
                if (show && _shieldFill != null) BattleEase.SetBar(_shieldFill, State.MaxShield > 0f ? Mathf.Clamp01(shield / State.MaxShield) : 1f);
            }
            if (_shieldBubble != null)
            {
                float alpha = _shieldLevel * (0.32f + 0.08f * Mathf.Sin(_age * 3f) + 0.35f * hit) + _shieldCrack * 0.9f;
                _shieldBubble.enabled = alpha > 0.01f;
                if (_shieldBubble.enabled)
                {
                    _shieldBubble.color = BattleEase.WithAlpha(Color.Lerp(UITheme.SoftBlue, Color.white, _shieldCrack + 0.3f * hit), Mathf.Clamp01(alpha));
                    float scale = 1f + 0.03f * Mathf.Sin(_age * 2.4f) + 0.35f * (1f - _shieldCrack) * (_shieldCrack > 0f ? 1f : 0f);
                    _shieldBubble.rectTransform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            _mark = BattleEase.Damp(_mark, State.MarkTimer > 0f ? 1f : 0f, 10f, dt);
            if (_markIcon != null)
            {
                bool show = _mark > 0.02f;
                _markIcon.enabled = show;
                if (show)
                {
                    float beat = 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(_age * 7f));
                    float pop = BattleEase.OutBack(_mark, 2.4f) * beat;
                    _markIcon.rectTransform.localScale = new Vector3(pop, pop, 1f);
                    // 배너 밑 흐림(_statusFade)도 같이 — 이름표 · HP 바 · 보호막 바와 함께 사라진다
                    _markIcon.color = BattleEase.WithAlpha(_markIcon.color, _mark * _statusFade);
                }
            }

            _burn = BattleEase.Damp(_burn, State.BurnTimer > 0f ? 1f : 0f, 8f, dt);
            if (_burnFlames != null)
            {
                bool show = _burn > 0.02f;
                for (int i = 0; i < _burnFlames.Length; i++)
                {
                    var flame = _burnFlames[i];
                    if (flame == null) continue;
                    flame.enabled = show;
                    if (!show) continue;
                    float flicker = Mathf.Sin(_age * (13f + i * 3f) + i * 1.7f);
                    flame.rectTransform.localScale = new Vector3((0.9f + 0.08f * flicker) * _burn, (1f + 0.18f * flicker) * _burn, 1f);
                    flame.color = BattleEase.WithAlpha(flame.color, _burn * (0.75f + 0.25f * flicker));
                }
            }

            if (_enrageAura != null)
            {
                bool show = _enrage > 0.02f && !IsBoss;
                _enrageAura.enabled = show;
                if (show)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(_age * 8f);
                    _enrageAura.color = BattleEase.WithAlpha(EnrageRed, _enrage * (0.3f + 0.25f * pulse));
                    float scale = 0.95f + 0.12f * pulse;
                    _enrageAura.rectTransform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            if (_gimmickRing != null && _gimmickRing.enabled)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_age * 2.2f);
                _gimmickRing.color = BattleEase.WithAlpha(_gimmickColor, (0.18f + 0.16f * pulse) * (1f - hidden));
            }
        }

        /// <summary>지금 맞음 (일반 등급 반응을 바로)</summary>
        public void Hit() => QueueImpact(0f, HitTier.Normal, Vector2.zero);

        /// <param name="hold">
        /// 마지막 일격(투사체·연쇄)이 닿기까지 남은 시간. 그때까지 버티다가 — 닿으면 하얗게 버티며 떨고 눌린 뒤(KillFreeze) — 터진다.
        /// 실제 보류(KillDelay)는 이 값과 '예약된 마지막 명중 + KillFreeze' 중 긴 쪽 (0.14 ~ 0.9초)
        /// </param>
        public void MarkKilled(float hold = 0f)
        {
            _killed = true;
            float blow = LastBlowRemaining();
            _killBlowAt = blow;
            _killHold = EnemyFxCatalog.KillBlowHold(hold, blow, IsBoss);
            _killMarkClock = _clock;
        }

        /// <summary>지금 그려지는 몸 그림 (그림이 있으면 _sprite, 없으면 둥근 _blob) — 보스 처치 순간 크림 실루엣이 이 모양을 따른다</summary>
        public Graphic Body => _sprite != null && _sprite.enabled ? _sprite : _blob;

        /// <summary>처치한 일격의 등급 (치명타·스킬 처치는 화면 킥)</summary>
        public HitTier KilledBy { get; private set; }

        /// <summary>처치 표시 + 처치한 일격의 등급</summary>
        public void MarkKilled(float hold, HitTier byTier)
        {
            KilledBy = byTier;
            MarkKilled(hold);
        }

        /// <summary>수선점을 들이받는 순간 (수선점 앞에 버티는 보스): 몸이 쿵 눌렸다 되튄다</summary>
        public void Lunge()
        {
            _hit = Mathf.Max(_hit, 0.6f);
            Impulse(0.06f, 0.08f);
        }

        /// <summary>
        /// 공격 방향을 알려 준다 (EnemyStrike, 전장 로컬). 몸을 내미는 동작 자체는 시뮬레이터의 AttackAnim이 이끈다 —
        /// 이벤트와 같은 진행에서 1이 되어 0.25초 동안 줄어든다.
        /// </summary>
        public void Strike(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f) _strikeDir = direction.normalized;
        }

        /// <summary>보호막이 깨지는 순간 (거품이 하얗게 번쩍이며 터진다). delay = 깨는 일격이 닿는 순간까지</summary>
        public void ShieldBreak(float delay = 0f)
        {
            if (delay <= 0.001f)
            {
                _shieldCrack = 1f;
                _hit = 1f;
                return;
            }
            float at = _clock + delay;
            _shieldBreakAt = _shieldBreakAt >= 0f ? Mathf.Min(_shieldBreakAt, at) : at;
        }

        /// <summary>숨어 있던 그림자가 드러남 (톡 부풀며 또렷해진다)</summary>
        public void Reveal() => _revealPop = 1f;

        /// <summary>분열로 생긴 적: 원래 자리에서 통 튀어나와 제자리로 떨어진다</summary>
        public void HopFrom(Vector2 fieldOrigin)
        {
            _hopFrom = fieldOrigin - Rect.anchoredPosition;
            _hopAge = 0f;
        }

        /// <summary>
        /// 목록에서 사라졌을 때: 처치면 마지막 일격까지 버텼다가 풀려나고, 아니면 수선점으로 빨려 들어간다.
        /// vanish면 (스테이지 전환) 처치가 아닌 적은 제자리에서 흐려진다.
        /// </summary>
        public void BeginExit(Vector2 shopFieldPosition, bool vanish = false)
        {
            bool killed = _killed || (State != null && State.Hp <= 0f);
            if (killed && !_killed) MarkKilled(0f);
            _phase = killed ? Phase.Killed : vanish ? Phase.Vanish : Phase.ReachedShop;
            _exitAge = 0f;
            _bloomNoted = false;
            _exitFrom = Rect.anchoredPosition;
            _exitTo = shopFieldPosition;
            _exitAlpha = _group != null ? _group.alpha : 1f;
            if (killed)
            {
                // 처치 표시와 퇴장 사이에 흐른 만큼 보류를 줄인다 (처치 꽃이 마지막 일격 + 하얀 버팀 뒤에 정확히 터지게)
                float since = Mathf.Max(0f, _clock - _killMarkClock);
                if (since > 0f)
                {
                    _killBlowAt = Mathf.Max(0f, _killBlowAt - since);
                    _killHold = Mathf.Max(_killBlowAt + 0.02f, _killHold - since);
                }
            }
            // 처치는 버티는 동안 마지막 일격의 번쩍임을 보여주고, 수선점 도달·사라짐은 바로 끈다
            if (_phase != Phase.Killed && _flash != null) _flash.enabled = false;
            _hpRoot.gameObject.SetActive(false);
            if (_stunOrbit != null) _stunOrbit.gameObject.SetActive(false);
            if (_slowRing != null) _slowRing.enabled = false;
            if (_shieldRoot != null) _shieldRoot.gameObject.SetActive(false);
            if (_shieldBubble != null) _shieldBubble.enabled = false;
            if (_markIcon != null) _markIcon.enabled = false;
            if (_enrageAura != null) _enrageAura.enabled = false;
            if (_gimmickRing != null) _gimmickRing.enabled = false;
            if (_burnFlames != null)
            {
                for (int i = 0; i < _burnFlames.Length; i++)
                {
                    if (_burnFlames[i] != null) _burnFlames[i].enabled = false;
                }
            }
        }

        public bool WasKilled => _phase == Phase.Killed;

        /// <summary>처치 연출에서 사라지기 전 잠깐 버티는 최소 시간 (지킴이 기본 투사체가 닿는 시간과 맞춤)</summary>
        public const float KillHold = 0.14f;

        /// <summary>터진 뒤 몸이 사라지는 시간 (갈래별: 나방은 날개 그림이 대신하므로 빨리, 그림자는 천천히 풀린다 — EnemyFxCatalog.ExitTime)</summary>
        public static float ExitTime(EnemyDeathFamily family) => EnemyFxCatalog.ExitTime(family);

        /// <summary>이번 처치에서 버티는 시간 = 처치 꽃이 터지는 순간 (마지막 일격 + 하얀 버팀)</summary>
        public float KillDelay => _killHold;

        /// <summary>퇴장 연출. 끝나면 false</summary>
        public bool TickExit(float dt)
        {
            _exitAge += dt;
            if (_phase == Phase.Killed)
            {
                TickFeel(dt);
                if (_exitAge < _killHold)
                {
                    HoldKilled();
                    return true;
                }
                if (!_bloomNoted) NoteBloom();
                float t = _exitAge - _killHold;
                if (t < HitFeel.KillPop)
                {
                    PopKilled(t / HitFeel.KillPop);
                    return true;
                }
                float k = Mathf.Clamp01((t - HitFeel.KillPop) / ExitTime(_family));
                ExitKilled(k);
                return k < 1f;
            }
            if (_phase == Phase.Vanish)
            {
                float k = Mathf.Clamp01(_exitAge / 0.35f);
                _body.localScale = new Vector3(1f - 0.3f * k, 1f - 0.3f * k, 1f);
                _group.alpha = Mathf.Min(_group.alpha, 1f - k);
                return k < 1f;
            }
            else
            {
                float k = Mathf.Clamp01(_exitAge / 0.3f);
                Rect.anchoredPosition = Vector2.Lerp(_exitFrom, _exitTo, BattleEase.InCubic(k));
                float scale = 1f - 0.7f * k;
                _body.localScale = new Vector3(scale, scale, 1f);
                _group.alpha = Mathf.Min(_group.alpha, 1f - k * k);
                return k < 1f;
            }
        }

        /// <summary>
        /// 처치 보류: 마지막 일격이 아직 날아오는 동안은 앞선 명중의 눌림·밀림·번쩍임만 (걷지 않는다).
        /// 일격이 닿으면 크림빛 1.0으로 덮인 채 떨림(±4px, 보스 ±6 — 줄어든다)과 함께 1.30 / 0.72(보스 1.35 / 0.70)까지 OutQuad로 눌린다
        /// </summary>
        void HoldKilled()
        {
            var knock = KnockOffset();
            if (_exitAge < _killBlowAt)
            {
                float spring = SpringNow();
                _body.localScale = new Vector3(1f + _springX * spring, 1f - _springY * spring, 1f);
                _body.anchoredPosition = _bodyRest + knock;
                if (_sprite.enabled) _sprite.color = Color.white;
                else _blob.color = Color.Lerp(BodyColor, Color.white, DrawnFlash * 0.85f);
                DrawFlash(1f);
                return;
            }
            float k = Mathf.Clamp01((_exitAge - _killBlowAt) / Mathf.Max(0.001f, _killHold - _killBlowAt));
            float build = JuiceEase.OutQuad(k);
            KillSquash(out float squashX, out float squashY);
            _body.localScale = new Vector3(Mathf.Lerp(1f, squashX, build), Mathf.Lerp(1f, squashY, build), 1f);
            float shiver = (IsBoss ? EnemyFxCatalog.BossKillShiver : HitFeel.KillShiver) * (1f - k) * _shiverSign;
            var axis = EnemyFxCatalog.ShiversVertically(_family) ? new Vector2(0f, shiver) : new Vector2(shiver, 0f);
            _body.anchoredPosition = _bodyRest + axis + knock;
            // 덧그림이 없는 기기: 질린 분홍이 흰빛으로 바래 가는 것으로 처치를 알린다
            var pale = Color.Lerp(KilledPale, Color.white, k);
            if (_sprite.enabled) _sprite.color = _flashArt ? Color.white : pale;
            else _blob.color = Color.Lerp(BodyColor, Color.white, 0.85f * FlashGate);
            DrawSolidFlash(HitFeel.KillFlashAlpha);
        }

        /// <summary>터지는 순간 (HitFeel.KillPop 40ms): 눌린 몸이 고르게 1.15배로 톡 (OutQuad), 크림빛 1.0 그대로</summary>
        void PopKilled(float u)
        {
            float e = JuiceEase.OutQuad(u);
            KillSquash(out float squashX, out float squashY);
            float pop = EnemyFxCatalog.KillPopScale;
            _body.localScale = new Vector3(Mathf.Lerp(squashX, pop, e), Mathf.Lerp(squashY, pop, e), 1f);
            _body.anchoredPosition = Vector2.Lerp(_bodyRest + KnockOffset(), Vector2.zero, e);
            _body.localRotation = Quaternion.identity;
            if (!_sprite.enabled) _blob.color = Color.Lerp(BodyColor, Color.white, 0.85f * FlashGate);
            DrawSolidFlash(HitFeel.KillFlashAlpha);
        }

        /// <summary>
        /// 갈래별 퇴장 (k 0 → 1, ExitTime): 먼지 = 살짝 부풀었다 쏙(InBack) · 나방 = 살짝 들리며 빨리 스밈(날개 그림이 대신) ·
        /// 쇠붙이 = 납작하게(1.4, 0.4) 찌그러짐 · 그림자 = 실처럼 위로 풀림. 크림 덧그림은 앞 60% 동안 걷히고 몸은 크림(그림자는 등불색)으로 바랜다
        /// </summary>
        void ExitKilled(float k)
        {
            float pop = EnemyFxCatalog.KillPopScale;
            float eased = BattleEase.OutCubic(k);
            float alpha;
            switch (_family)
            {
                case EnemyDeathFamily.Moth:
                    _body.localScale = new Vector3(pop * (1f + 0.1f * eased), pop * (1f - 0.25f * eased), 1f);
                    _body.anchoredPosition = new Vector2(0f, 8f * eased);
                    alpha = 1f - JuiceEase.OutQuad(k);
                    break;
                case EnemyDeathFamily.Metal:
                    _body.localScale = new Vector3(Mathf.Lerp(pop, 1.4f, eased), Mathf.Lerp(pop, 0.4f, eased), 1f);
                    _body.anchoredPosition = Vector2.zero;
                    alpha = 1f - JuiceEase.InQuad(k);
                    break;
                case EnemyDeathFamily.Shadow:
                    _body.localScale = new Vector3(pop * (1f - 0.6f * eased), pop * (1f + 0.75f * eased), 1f);
                    _body.anchoredPosition = new Vector2(0f, 26f * eased);
                    alpha = 1f - k;
                    break;
                default:
                    float shrink = Mathf.Max(0f, pop * (1f - JuiceEase.InBack(k)));
                    _body.localScale = new Vector3(shrink, shrink, 1f);
                    _body.anchoredPosition = Vector2.zero;
                    alpha = 1f - JuiceEase.InQuad(k);
                    break;
            }
            _group.alpha = _exitAlpha * Mathf.Clamp01(alpha);
            var fadeTo = _family == EnemyDeathFamily.Shadow ? UITheme.Lamp : UITheme.Cream;
            if (_sprite.enabled) _sprite.color = Color.Lerp(Color.white, fadeTo, k);
            else _blob.color = Color.Lerp(BodyColor, fadeTo, 0.5f + 0.5f * k);
            DrawSolidFlash(HitFeel.KillFlashAlpha * (1f - JuiceEase.OutQuad(Mathf.Clamp01(k / 0.6f))));
        }

        /// <summary>처치 버팀의 눌림 꼭대기 (일반 1.30 / 0.72, 보스 1.35 / 0.70)</summary>
        void KillSquash(out float x, out float y)
        {
            x = IsBoss ? EnemyFxCatalog.BossKillSquashX : HitFeel.KillSquashX;
            y = IsBoss ? EnemyFxCatalog.BossKillSquashY : HitFeel.KillSquashY;
        }

        public void Release()
        {
            State = null;
            Id = -1;
            _phase = Phase.Alive;
            _impactCount = 0;
            if (_flash != null) _flash.enabled = false;
            gameObject.SetActive(false);
        }

        // ───────────── 피격 손맛 (FX-JUICE E9) ─────────────

        /// <summary>명중 반응 예약 (예전 호출 — 강조색 없음, 멈춤 허용, 지킴이 공격)</summary>
        public void QueueImpact(float delay, HitTier tier, Vector2 direction) => QueueImpact(delay, tier, direction, default, true, false);

        /// <summary>
        /// 명중 반응 예약 v2: delay 뒤(투사체·연출이 닿는 순간) 등급 표(HitFeel.Feel)대로 크림 번쩍임 · 눌림 용수철 · 대상 정지(예산 안에서) ·
        /// 정지가 끝난 뒤 넉백(direction 쪽으로). 거의 같은 순간에 닿는 명중은 하나로 합쳐 센 등급이 이긴다 (치명타가 같은 일격의 일반 피격보다 먼저 와도 치명타로).
        /// accent = 치명타 뒤 물드는 공격자 강조색 (알파 0이면 금색), allowFreeze = false면 멈추지 않는다 (여러 번 닿는 스킬의 가운데 틱),
        /// echo = 잔향이의 공격 (번쩍임 절반 · 넉백 0.6배 · 멈춤 없음 — HitFeel.ForEcho)
        /// </summary>
        public void QueueImpact(float delay, HitTier tier, Vector2 direction, Color accent, bool allowFreeze = true, bool echo = false)
        {
            // 지금 보호막이 남아 있으면 이 피해는 보호막이 받아 냈다 (깨는 일격은 ShieldBroken이 강타로 따로 온다)
            bool guarded = State != null && State.Shield > 0.01f;
            var impact = new PendingImpact
            {
                At = _clock + Mathf.Max(0f, delay), Tier = tier, Direction = direction, Guarded = guarded, Accent = accent, AllowFreeze = allowFreeze, Echo = echo,
            };
            if (delay <= 0.001f)
            {
                ApplyImpact(in impact);
                return;
            }
            for (int i = 0; i < _impactCount; i++)
            {
                ref var pending = ref _impacts[i];
                if (Mathf.Abs(pending.At - impact.At) > ImpactMerge) continue;
                if (Rank(tier) > Rank(pending.Tier))
                {
                    pending.Tier = tier;
                    if (accent.a > 0.01f) pending.Accent = accent;
                }
                else if (pending.Accent.a <= 0.01f && accent.a > 0.01f) pending.Accent = accent;
                if (direction != Vector2.zero) pending.Direction = direction;
                pending.Guarded &= guarded;
                pending.AllowFreeze |= allowFreeze;
                pending.Echo &= echo;
                return;
            }
            if (_impactCount >= _impacts.Length)
            {
                // 가장 먼저 닿을 반응을 지금 터뜨리고 자리를 비운다
                var first = _impacts[0];
                RemoveImpact(0);
                ApplyImpact(in first);
            }
            _impacts[_impactCount++] = impact;
        }

        /// <summary>
        /// 처치 꽃이 터지는 순간 (한 번): 그 앞 0.30초 안에 닿은 명중이 없었는지 센다 (SPEC4 §13.5 j, ATK-09 — KillArrivalProbe. 정수 두 개라 늘 세고,
        /// 로그는 개발 빌드만)
        /// </summary>
        void NoteBloom()
        {
            _bloomNoted = true;
            KillArrivalProbe.Note(_clock - _lastArrival);
        }

        /// <summary>이 적에게 투사체가 날아오는 중 (delay 뒤 도착). 처치 보류에 쓴다</summary>
        public void ExpectImpact(float delay)
        {
            if (delay > 0f) _incomingUntil = Mathf.Max(_incomingUntil, _clock + delay);
        }

        /// <summary>날아오는 투사체 중 가장 늦게 닿는 것까지 남은 시간 (없으면 0)</summary>
        public float IncomingRemaining => Mathf.Max(0f, _incomingUntil - _clock);

        /// <summary>마지막 일격이 닿기까지 남은 시간: 날아오는 투사체와 예약된 명중 중 가장 늦은 것 (없으면 0)</summary>
        float LastBlowRemaining()
        {
            float latest = IncomingRemaining;
            for (int i = 0; i < _impactCount; i++) latest = Mathf.Max(latest, _impacts[i].At - _clock);
            return latest;
        }

        /// <summary>
        /// 이번 피해 중 보호막이 막은 양 (EnemyHit의 Value는 보호막 흡수분 포함). 지난 갱신 때 본 보호막에서 지금 보호막을 뺀 만큼을
        /// 이 피해들이 차례로 나눠 가진다 — '막음 N' 숫자용.
        /// </summary>
        public float ConsumeShield(float total)
        {
            if (State == null || !(total > 0f)) return 0f;
            float absorbed = Mathf.Clamp(_shieldSeen - Mathf.Max(0f, State.Shield), 0f, total);
            _shieldSeen -= absorbed;
            return absorbed;
        }

        /// <summary>지속 피해(촛불·반딧불)로 타고 있는지 (방금 꺼진 마지막 틱 포함)</summary>
        public bool IsBurning => _burn > 0.05f || (State != null && State.BurnTimer > 0f);

        void ResetFeel(EnemyState state)
        {
            _clock = 0f;
            _impactCount = 0;
            _flashAge = 9f;
            _flashHold = 0f;
            _flashLife = 0.07f;
            _flashStrength = 0f;
            _blush = false;
            _springAge = 9f;
            _springX = 0f;
            _springY = 0f;
            _springKick = false;
            _stop = 0f;
            _stopTotal = 0f;
            _shiver = 0f;
            _stopLoad = 0f;
            _knockAt = -9999f;
            _knockFrom = Vector2.zero;
            _knockDistance = 0f;
            _knockDirection = Vector2.zero;
            _ghostFrom = 1f;
            _ghostAge = 0f;
            _lastRatio = 1f;
            _incomingUntil = 0f;
            _lastArrival = -9999f;
            _bloomNoted = false;
            _shieldSeen = state.Shield;
            _shieldBreakAt = -1f;
        }

        void BindFlash(Sprite sprite)
        {
            if (_flash == null)
            {
                _flashArt = false;
                return;
            }
            var material = _flash.material;
            _flashArt = sprite != null && material != null && material != Graphic.defaultGraphicMaterial;
            _flash.sprite = sprite;
            _flash.enabled = false;
        }

        void TickFeel(float dt)
        {
            _clock += dt;
            float now = _clock;
            for (int i = 0; i < _impactCount;)
            {
                if (_impacts[i].At > now)
                {
                    i++;
                    continue;
                }
                var due = _impacts[i];
                RemoveImpact(i);
                ApplyImpact(in due);
            }
            if (_shieldBreakAt >= 0f && now >= _shieldBreakAt)
            {
                _shieldBreakAt = -1f;
                _shieldCrack = 1f;
                _hit = 1f;
            }
            _flashAge += dt;
            _stopLoad = Mathf.Max(0f, _stopLoad - dt * EnemyFxCatalog.FreezeLeak);
            bool frozen = _stop > 0f;
            _stop = Mathf.Max(0f, _stop - dt);
            // 멈춘 동안 눌림은 꼭대기에 머물고, 방금 맞은 프레임도 꼭대기를 보여 준다 (첫 프레임이 가장 크다)
            if (!frozen && !_springKick && _springAge < SpringRest) _springAge += dt;
            _springKick = false;
            // 떨림 방향은 연출 시계로 (perf PF-2 F10 — 프레임마다 뒤집으면 30 fps에서 15 Hz, 144 fps에서 72 Hz). 60 fps에서는 예전과 같다
            if (dt > 0f) _shiverSign = ((int)(_clock * 60f) & 1) == 0 ? 1f : -1f;
        }

        void ApplyImpact(in PendingImpact impact)
        {
            _lastArrival = _clock;
            var tier = impact.Tier;
            var feel = HitFeel.Feel(tier);
            if (impact.Echo) feel = HitFeel.ForEcho(in feel);
            if (IsBoss) feel = HitFeel.ForBoss(in feel);
            if (impact.Guarded) feel = HitFeel.ForGuarded(in feel);
            _hit = Mathf.Max(_hit, tier == HitTier.Tick ? 0.35f : impact.Guarded ? 0.6f : 1f);

            // 번쩍임: 지금 번쩍임보다 세거나 같을 때만 다시 건다 (치명타는 끝난 뒤 공격자 강조색으로 물든다 — 번쩍임 줄이기면 없음)
            if (feel.FlashAlpha > 0f && feel.FlashAlpha >= FlashAlpha)
            {
                _flashAge = 0f;
                _flashHold = feel.FlashHold;
                _flashLife = Mathf.Max(0.02f, feel.FlashLife);
                _flashStrength = feel.FlashAlpha;
                _blush = tier == HitTier.Crit && !impact.Guarded && !BattleFx.ReduceFlashNow;
                if (_blush) _blushColor = impact.Accent.a > 0.01f ? impact.Accent : UITheme.Gold;
            }
            Impulse(feel.SquashX - 1f, 1f - feel.SquashY);

            // 처치된 몸은 하얀 버팀(HoldKilled)이 멈춤·떨림을 맡고, 터질 자리에서 밀리지 않는다
            if (_phase == Phase.Killed) return;

            // 대상 정지 (새는 양동이 예산 안에서) + 줄어드는 떨림
            float granted = 0f;
            if (impact.AllowFreeze && feel.Freeze > 0f)
            {
                granted = EnemyFxCatalog.FreezeGrant(_stopLoad, feel.Freeze);
                if (granted >= MinFreeze)
                {
                    _stopLoad += granted;
                    if (granted >= _stop)
                    {
                        _stop = granted;
                        _stopTotal = granted;
                        _shiver = feel.Shiver;
                    }
                    else _shiver = Mathf.Max(_shiver, feel.Shiver);
                }
                else granted = 0f;
            }
            // 넉백: 지금 자리에서 멈춤이 끝나는 순간 밀려 나간다 (보호막에 막힌 명중은 ForGuarded가 0 — 거품만 출렁인다)
            float push = feel.Knockback;
            if (push > 0f)
            {
                _knockFrom = KnockRaw();
                _knockDirection = impact.Direction != Vector2.zero ? impact.Direction.normalized : Vector2.up;
                _knockDistance = impact.Direction != Vector2.zero ? push : push * 0.4f;
                _knockAt = _clock + Mathf.Max(granted, _stop);
            }
            if (!_hpVisible && _hpRoot != null && _phase == Phase.Alive)
            {
                _hpVisible = true;
                _hpRoot.gameObject.SetActive(true);
            }
        }

        void RemoveImpact(int index)
        {
            for (int j = index + 1; j < _impactCount; j++) _impacts[j - 1] = _impacts[j];
            _impactCount--;
        }

        /// <summary>센 순서: 지속 피해 &lt; 일반·불빛 &lt; 강타·스킬 &lt; 치명타</summary>
        static int Rank(HitTier tier) => tier switch
        {
            HitTier.Tick => 0,
            HitTier.Heavy => 2,
            HitTier.Skill => 2,
            HitTier.Crit => 3,
            _ => 1,
        };

        /// <summary>눌림 충격: 지금 남은 눌림보다 클 때만 용수철을 다시 건다 (aX = 가로로 퍼지는 양, aY = 세로로 눌리는 양)</summary>
        void Impulse(float ax, float ay)
        {
            float peak = Mathf.Max(ax, ay);
            if (peak <= 0.001f) return;
            float current = Mathf.Max(_springX, _springY) * Mathf.Abs(SpringNow());
            if (peak <= current) return;
            _springX = ax;
            _springY = ay;
            _springAge = 0f;
            _springKick = true;
        }

        /// <summary>눌림 용수철 변위 (1 → 한 번 반대로 되튐 → 0)</summary>
        float SpringNow() => _springAge >= SpringRest ? 0f : JuiceEase.SpringDisplacement(_springAge, EnemyFxCatalog.SquashHz, EnemyFxCatalog.SquashZeta);

        /// <summary>멈춘 동안의 떨림 (몸 기준 px): 폭 × 남은 비율, 프레임마다 방향이 뒤집힌다. 걷는 갈래는 좌우, 나는 갈래는 위아래</summary>
        Vector2 ShiverOffset()
        {
            if (_stop <= 0f || _stopTotal <= 0f || _shiver <= 0f) return Vector2.zero;
            float amplitude = _shiver * Mathf.Clamp01(_stop / _stopTotal) * _shiverSign;
            return EnemyFxCatalog.ShiversVertically(_family) ? new Vector2(0f, amplitude) : new Vector2(amplitude, 0f);
        }

        /// <summary>넉백 (전장 px): 출발 전(멈춤)엔 그 자리, 40ms OutExpo로 밀리고 120ms OutCubic으로 돌아온다</summary>
        Vector2 KnockRaw()
        {
            float t = _clock - _knockAt;
            if (t < 0f) return _knockFrom;
            var target = _knockDirection * _knockDistance;
            if (t < HitFeel.KnockOut) return Vector2.LerpUnclamped(_knockFrom, target, JuiceEase.OutExpo(t / HitFeel.KnockOut));
            float back = t - HitFeel.KnockOut;
            return back < HitFeel.KnockReturn ? target * (1f - JuiceEase.OutCubic(back / HitFeel.KnockReturn)) : Vector2.zero;
        }

        /// <summary>넉백을 몸 기준 좌표로 (루트 배율을 나눈다)</summary>
        Vector2 KnockOffset()
        {
            var raw = KnockRaw();
            if (raw.sqrMagnitude < 1e-6f) return Vector2.zero;
            float scale = Mathf.Max(0.01f, Rect.localScale.x);
            return raw / scale;
        }

        /// <summary>공격 방향: 마지막 Strike 방향, 없으면 수선점(전장 원점) 쪽</summary>
        Vector2 StrikeDirection(BattleFieldMapper mapper)
        {
            if (_strikeDir != Vector2.zero) return _strikeDir;
            var toShop = mapper.Origin - Rect.anchoredPosition;
            return toShop.sqrMagnitude > 1f ? toShop.normalized : Vector2.down;
        }

        /// <summary>공격 내밀기(일반 16 / 보스 30px) / 예비 동작 젖히기 (몸 기준 좌표 — 루트 배율을 나눈다)</summary>
        Vector2 StrikeOffset(Vector2 direction, float lunge, float windup)
        {
            if (lunge <= 0f && windup <= 0f) return Vector2.zero;
            float scale = Mathf.Max(0.01f, Rect.localScale.x);
            float reach = (IsBoss ? EnemyFxCatalog.BossLungeReach : EnemyFxCatalog.LungeReach) * lunge - (IsBoss ? 14f : 8f) * windup;
            return direction * (reach / scale);
        }

        /// <summary>지금 번쩍임 세기: FlashHold 동안 그대로, FlashLife까지 InQuad로 꺼진다</summary>
        float FlashAlpha
        {
            get
            {
                if (_flashAge >= _flashLife || _flashStrength <= 0f) return 0f;
                if (_flashAge <= _flashHold) return _flashStrength;
                float u = (_flashAge - _flashHold) / Mathf.Max(0.001f, _flashLife - _flashHold);
                return _flashStrength * (1f - u * u);
            }
        }

        /// <summary>
        /// 번쩍임 줄이기면 몸의 크림빛 × 0.75 (EnemyFxCatalog.ReducedBodyFlash — 치명타 1.0 → 0.75 · 일반 0.85 → 0.64 · 처치 버팀 1.0 → 0.75).
        /// 그릴 때만 줄인다 — 새 번쩍임을 다시 걸지 비교하는 FlashAlpha는 원래 세기 그대로
        /// </summary>
        static float FlashGate => EnemyFxCatalog.BodyFlash(1f, BattleFx.ReduceFlashNow);

        /// <summary>지금 그리는 번쩍임 세기 (FlashAlpha × 번쩍임 줄이기)</summary>
        float DrawnFlash => FlashAlpha * FlashGate;

        /// <summary>치명타 물듦 중인지 (번쩍임이 끝난 뒤 60ms)</summary>
        bool Blushing => _blush && _flashAge >= _flashLife && _flashAge < _flashLife + EnemyFxCatalog.BlushTime;

        /// <summary>피격 번쩍임(크림, 번쩍임 줄이기면 × 0.75) 또는 치명타 물듦(강조색 0.35)을 덧그림에. visibility = 숨은 그림자 흐림</summary>
        void DrawFlash(float visibility)
        {
            if (_flash == null) return;
            float alpha = 0f;
            var color = JuiceColors.CoreCream;
            if (_flashArt && _sprite.enabled)
            {
                alpha = DrawnFlash;
                if (alpha <= 0.01f && Blushing)
                {
                    alpha = EnemyFxCatalog.BlushAlpha;
                    color = _blushColor;
                }
                alpha *= visibility;
            }
            bool show = alpha > 0.01f;
            if (_flash.enabled != show) _flash.enabled = show;
            if (show) _flash.color = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>처치 버팀·터짐: 크림 덧그림을 alpha로 고정 (번쩍임 줄이기면 × 0.75)</summary>
        void DrawSolidFlash(float alpha)
        {
            if (_flash == null) return;
            alpha *= FlashGate;
            bool show = _flashArt && _sprite.enabled && alpha > 0.01f;
            if (_flash.enabled != show) _flash.enabled = show;
            if (show)
            {
                var cream = JuiceColors.CoreCream;
                _flash.color = new Color(cream.r, cream.g, cream.b, alpha);
            }
        }

        /// <summary>시뮬레이터 자리(월드)로 — 전체 정지·느린 화면 동안은 그 사이 걸은 만큼을 붙잡았다가 풀리면 따라잡는다 (dt = 연출 시계)</summary>
        void Place(Vector2 world, BattleFieldMapper mapper, float timeScale, float dt)
        {
            var sim = mapper.WorldToField(world);
            _holdOffset = _simFieldValid ? EnemyFxCatalog.HoldOffset(_holdOffset, sim - _simField, timeScale, dt) : Vector2.zero;
            _simField = sim;
            _simFieldValid = true;
            var position = sim + _holdOffset;
            if (_hopAge < 1f) position += _hopFrom * (1f - BattleEase.OutCubic(Mathf.Clamp01(_hopAge)));
            Rect.anchoredPosition = position;
            float scale = mapper.ActorScale * _unitScale;
            Rect.localScale = new Vector3(scale, scale, 1f);
        }

        void UpdateHp(float dt, float hidden)
        {
            if (!_hpVisible || State == null) return;
            bool show = hidden < 0.5f;
            if (_hpRoot.gameObject.activeSelf != show) _hpRoot.gameObject.SetActive(show);
            float ratio = State.MaxHp > 0f ? Mathf.Clamp01(State.Hp / State.MaxHp) : 0f;
            BattleEase.SetBar(_hpFill, ratio);
            // 깎인 만큼 하얀 잔상이 버텼다가 (일반 150ms / 보스 400ms) OutCubic으로 줄어든다 — 새로 깎이면 지금 잔상에서 다시 버틴다
            if (ratio >= _trail)
            {
                _trail = ratio;
                _ghostFrom = ratio;
                _ghostAge = 0f;
            }
            else
            {
                if (ratio < _lastRatio - 0.0001f)
                {
                    _ghostFrom = _trail;
                    _ghostAge = 0f;
                }
                _ghostAge += dt;
                _trail = EnemyFxCatalog.GhostRatio(_ghostFrom, ratio, _ghostAge, IsBoss);
            }
            _lastRatio = ratio;
            BattleEase.SetBar(_hpTrail, _trail);
            _hpFill.color = IsBoss ? UITheme.Danger : Color.Lerp(UITheme.Negative, UITheme.DustyPink, ratio);
        }

        /// <summary>아트가 없을 때 적 종류별 덩어리 색</summary>
        public static Color BlobColor(string enemyId, bool boss)
        {
            switch (enemyId)
            {
                case "enemy_dust": return new Color(0.55f, 0.51f, 0.66f);
                case "enemy_moth": return new Color(0.66f, 0.55f, 0.79f);
                case "enemy_rust": return new Color(0.73f, 0.54f, 0.42f);
                case "enemy_tangle": return new Color(0.82f, 0.55f, 0.69f);
                case "enemy_wisp": return new Color(0.37f, 0.33f, 0.56f);
                case "enemy_thimble": return new Color(0.78f, 0.76f, 0.86f);
                case "enemy_windup": return new Color(0.62f, 0.7f, 0.82f);
                case "boss_shadow": return new Color(0.29f, 0.23f, 0.49f);
                case "boss_mothqueen": return new Color(0.56f, 0.38f, 0.66f);
                default: return boss ? new Color(0.3f, 0.22f, 0.45f) : UITheme.Enemy;
            }
        }
    }
}
