using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스킬 시전 대사 말풍선 (예: "꿰매 줄게!"). 시전자 머리 위에 톡 튀어나와(살짝 눌렸다 펴지며 기울었다 선다) 잠깐 머물다 떠오르며 사라진다.
    /// Say는 실제로 띄웠는지 돌려준다 (전투 화면이 연속 처치 알림을 잠깐 쉬게 — FX-JUICE E11).
    /// 잔향이는 움직이므로 따라다닌다. 같은 시전자는 일정 간격 안에 다시 말하지 않는다 (쿨타임 스킬이 자주 터져도 시끄럽지 않게).
    /// 말풍선은 보이는 전장 띠 안에 머문다 (M41 · UX-SPEC-IA §8.3): 바닥 _floorY = 전장 지도의 VisibleBottom + 24 (MAIN은 덱 띠 위 끝 + 덱 탭 18 + 여유 6,
    /// 시트가 열리면 시트 위 끝 + 24 — 시트 밑으로 들어가지 않는다), 천장 = VisibleTop − 70 (전투가 올라가도 상단 바 뒤로 가지 않는다).
    /// M41 나머지 (UX-SPEC §2.1.8 · Wave 3 WP-5B):
    ///   · 같은 대사 거르기: 8칸 고리(대사 + 시각) — 같은 대사가 떠 있거나 2.5초 안에 떴으면 버린다.
    ///   · 한꺼번에 _maxAlive(3)개까지, SHEET(시트가 다 열림 · 시험장 SHEET 변형)에서는 2개까지 (SheetMaxAlive — 줄어든 전장이 붐비지 않게, Wave 3 r1)
    ///     — 꽉 차면 새 대사를 버린다 (가장 오래된 말풍선을 빼앗지 않는다). 풀은 5개 그대로.
    ///   · 비키기 (가장 조금): HUD 칸(BattleFx.KeepOut — 스테이지 카드 · 배속/AUTO · 추천 알약 · 바구니 · 빠른 줄 · 시너지 줄)은 옆·아래로,
    ///     보스 자리(이름표 + HP 바 묶음 + 몸 그림 — BattleFx.PlateBodyKeepOut)는 위·옆, 안 되면 몸 아래 끝 밑으로 띄움 한도(112px) 안에서
    ///     (BattleKeepOut.BubbleBossClear — Wave 3 r2: SHEET에서 이름표 밑으로 밀린 말풍선이 보스 머리와 몸을 덮었다),
    ///     수선점 이름표('수선점' 알약 — BattleFx.ShopPlateKeepOut)는 위·옆으로 띄움 한도(112px) 안에서
    ///     (BattleKeepOut.BubblePlateClear — Wave 3 r1), 보이는 배너(가운데 배너 · 시너지 배너)는 위·아래로. 안내 줄(가이드 퀘스트 + 6px)은
    ///     MAIN에서만 그 위로 올린다 (SHEET에서는 시트 밑이라 바닥이 이미 시트 위 끝). 배너 · 수선점 이름표 · 보스 자리를 못 비키면 새 대사는 띄우지 않고
    ///     떠 있던 것은 일찍 사라진다 (보스 자리에 막히면 지난 자리에 멈춘 채).
    ///     말풍선 레이어는 전장(Field) 안이라 배너 · HUD보다 늘 아래에 그려진다 — 비키기는 배너 부제 · 보상 줄을 가리지 않게.
    ///     피해 숫자 레이어(Numbers)는 말풍선 위에 그려진다 (Wave 3 r2 — 빌더 순서, 예전 씬은 Prepare가 한 번 맞춘다).
    ///   · 겹침 띄움 (오래된 것부터 — 나이가 아니라 뜬 차례 Born로): 앞선 말풍선과 겹치면 그 위 끝 + 6px까지 (최대 2번, 합 112px — BattleKeepOut.BubbleLift).
    ///     그래도 겹치거나 천장을 넘거나, 올린 자리가 HUD 칸 · 보스 자리 · 수선점 이름표 · 보이는 배너에 들어가면 일찍 사라지기 시작한다 (나이 = 수명 × 0.75 —
    ///     HUD · 이름표 · 배너에 막히면 지금 띄움 그대로). 띄움(Lift)은 600px/s로 따라간다 (새 말풍선은 바로). 새 대사가 뜨자마자 막히면 띄우지 않는다 (false).
    ///   · 떠 있는 말풍선이 없으면 Update는 자리를 재지 않는다.
    ///   · 글자 18~22pt (자동 크기), 폭 96~400.
    /// 말풍선은 미리 만들어 둔 것을 돌려 쓴다 (매 프레임 할당 없음).
    /// </summary>
    public class SkillCalloutView : MonoBehaviour
    {
        [SerializeField] RectTransform _area;
        [SerializeField] RectTransform[] _bubbles;
        [SerializeField] CanvasGroup[] _groups;
        [SerializeField] TMP_Text[] _texts;
        [Tooltip("말풍선 바탕 (글자 길이에 맞춰 폭을 바꾼다)")]
        [SerializeField] RectTransform[] _backs;
        [Tooltip("스킬 색을 입히는 그림 (예전: 말풍선 아래 색 띠 / 아트 말풍선: 말풍선 바탕 자체에 옅게)")]
        [SerializeField] Image[] _accents;
        [Tooltip("0 = 예전 색 띠 (스킬 색 그대로), 0보다 크면 아트 말풍선 바탕에 흰색 → 스킬 색을 이만큼만 섞는다 (≤0.35)")]
        [SerializeField] float _accentBlend;
        [Tooltip("아트 말풍선 꼬리 (ui_bubble_tail, 말풍선마다 하나). 바탕과 같은 옅은 스킬 색을 입혀 이음매가 보이지 않게 한다. 없으면 무시")]
        [SerializeField] Image[] _tails;
        [Tooltip("말풍선이 가리면 안 되는 HUD (전투 화면 루트 기준 경로, 예: 권장 전투력 알약). 연출(BattleFx)이 없을 때만 — 있으면 연출의 HUD 칸 전부를 비킨다")]
        [SerializeField] string[] _avoidPaths = { "MetaHud/RecommendedPower" };
        [SerializeField] RectTransform _avoidRoot;
        [SerializeField] float _life = 1.6f;
        [Tooltip("같은 시전자가 다시 말하기까지 최소 간격(초)")]
        [SerializeField] float _minInterval = 3f;
        [SerializeField] float _charWidth = 21f;
        [SerializeField] float _padding = 36f;
        [Tooltip("한꺼번에 떠 있는 말풍선 수 (꽉 차면 새 대사를 버린다 — M41)")]
        [SerializeField] int _maxAlive = 3;

        struct Bubble
        {
            public bool Alive;
            public float Age;
            public GuardianSlotView Guardian;
            public EchoView Echo;
            public int EchoId;
            public Vector2 Last;
            /// <summary>톡 튀어나올 때 기우는 쪽 (±1)</summary>
            public float Tilt;
            /// <summary>대사 (같은 대사 거르기 — 부른 쪽 문자열 참조, 할당 없음)</summary>
            public string Text;
            /// <summary>이번 프레임의 제자리 (머리 위 + 띠 자르기 + 비키기, 발밑 pivot) / 폭</summary>
            public Vector2 Base;
            public float Width;
            /// <summary>겹침 띄움 (px, 위로 +) — 목표로 600px/s 따라간다</summary>
            public float Lift;
            /// <summary>막 뜬 말풍선 (첫 띄움은 바로 맞춘다)</summary>
            public bool Fresh;
            /// <summary>
            /// 뜬 차례 (Begin마다 하나씩 커진다) — 겹침 띄움은 이 차례로 오래된 것부터. 나이(Age)로 줄 세우면 일찍 사라지기(FadeEarly)가 나이를 올려
            /// 막 뜬 말풍선이 '가장 오래된 것'이 되어 아래 말풍선과 자리를 맞바꾼다
            /// </summary>
            public int Born;
            /// <summary>말하는 친구 (지킴이 칸 0~4 · 잔향이 ID · −1 = 모름 — 8초 규칙)</summary>
            public int Speaker;
        }

        const int EchoMemory = 16;
        /// <summary>바닥: 보이는 띠 아래 끝에서 이만큼 위 (M41 strip top + 18 + 6) / 천장: 보이는 띠 위 끝에서 이만큼 아래 (말풍선 64 + 6)</summary>
        const float FloorLift = 24f;
        const float CeilingDrop = 70f;
        /// <summary>같은 대사 거르기: 고리 칸 수 / 이 시간 안에 뜬 같은 대사는 버린다 (중요한 대사도)</summary>
        public const int DedupeSlots = 16;
        public const float DedupeWindow = 2.5f;
        /// <summary>
        /// 대사 도배 막기 (Wave 3 r3 → RM-2): 같은 대사는 15초에 한 번, 같은 말하는 친구(지킴이 칸 · 잔향이 ID)는 8초에 한 줄 — 같은 고리 칸에 함께 적는다.
        /// 중요한 대사(큰 스킬 · 불 밝히기 연계 · 쓰다듬기)는 이 둘을 건너뛴다 (2.5초 규칙은 그대로)
        /// </summary>
        public const float SameTextWindow = 15f;
        public const float SpeakerWindow = 8f;
        /// <summary>피해 숫자가 비켜 갈 말풍선 (SolidBubbleRects): 묶음 알파 이 이상 · 나이(수명 비율) 이 미만</summary>
        public const float SolidAlpha = 0.5f;
        public const float SolidAge = 0.75f;
        /// <summary>겹침 판정 말풍선 높이 / 띄움 틈 · 횟수 · 한 번 몫 (합 = 횟수 × 몫) / 띄움 따라가는 빠르기</summary>
        public const float BubbleHeight = 60f;
        public const float LiftGap = 6f;
        public const int LiftSteps = 2;
        public const float LiftStep = 56f;
        const float LiftSpeed = 600f;
        /// <summary>일찍 사라지기 시작하는 나이 (수명 비율 — 마지막 25%가 사라짐)</summary>
        const float EarlyFade = 0.75f;
        /// <summary>HUD · 이름표 · 배너와 띄우는 틈 / 안내 줄 위 여유</summary>
        const float KeepGap = 6f;
        const float GuidePad = 6f;
        /// <summary>말풍선 폭 / 글자 크기 (M41)</summary>
        public const float MinWidth = 96f;
        public const float MaxWidth = 400f;
        public const float FontMin = 18f;
        public const float FontMax = 22f;
        /// <summary>SHEET에서 한꺼번에 떠 있는 말풍선 수 (_maxAlive보다 작으면 이것 — Wave 3 r1) / 시트가 다 열렸다고 보는 SheetProgress</summary>
        public const int SheetMaxAlive = 2;
        const float SheetFull = 0.99f;
        /// <summary>수선점 이름표를 비키며 옮길 수 있는 거리 (= 띄움 한도 2 × 56px — 더 멀면 누가 말하는지 읽히지 않으니 띄우지 않는다)</summary>
        public const float ShopPlateMove = LiftSteps * LiftStep;
        /// <summary>보스 자리(이름표 묶음 + 몸 — BattleFx.PlateBodyKeepOut)를 비키며 옮길 수 있는 거리 (수선점 이름표와 같은 112px — 더 멀면 띄우지 않는다, Wave 3 r2)</summary>
        public const float BossMove = LiftSteps * LiftStep;
        /// <summary>
        /// 안내 줄 · 배너 경로 (전투 화면 루트 기준). 0번 칸 = 가운데 배너 · 작은 리본: WP-5A의 배너 띠(BattleBannerView.TryGetKeepOut — 배너 + 보상 줄 알약,
        /// 나타남 · 머무름 · 사라짐 동안)를 쓰고, 그 API가 없는 씬을 위해 경로도 남긴다. 1번 = 시너지 배너 (경로 + 묶음 알파)
        /// </summary>
        const string GuidePath = "MetaHud/GuideQuestBar";
        /// <summary>피해 숫자 레이어 이름 (말풍선 레이어의 형제 — 말풍선보다 뒤 형제라야 위에 그려진다)</summary>
        const string NumbersLayer = "Numbers";
        static readonly string[] BannerPaths = { "BannerArea/Banner", "SynergyBannerArea/SynergyBanner" };
        /// <summary>이 묶음 알파보다 옅은 배너는 비키지 않는다 (들어오는 중 · 나가는 중의 끝)</summary>
        const float BannerAlpha = 0.05f;

        /// <summary>전장 지도 (보이는 띠 VisibleBottom · VisibleTop — _area 위쪽에서 찾는다, 없으면 _area 전체)</summary>
        BattleFieldMapper _mapper;
        /// <summary>HUD 칸 · 보스 이름표 자리 (같은 ShakeRoot 아래 Fx 레이어 — 좌표가 같다). 없으면 _avoidPaths만</summary>
        BattleFx _fx;
        Bubble[] _state;
        /// <summary>이번 프레임에 살아 있는 말풍선 (오래된 것부터) / 앞선 말풍선 자리 (x, 발밑 y, 폭)</summary>
        int[] _order;
        Vector3[] _others;
        readonly float[] _guardianLast = new float[BattleGeometry.SlotCount];
        readonly int[] _echoIds = new int[EchoMemory];
        readonly float[] _echoLast = new float[EchoMemory];
        int _echoCursor;
        readonly string[] _recentText = new string[DedupeSlots];
        readonly float[] _recentAt = new float[DedupeSlots];
        readonly int[] _recentSpeaker = new int[DedupeSlots];
        int _recentCursor;
        /// <summary>피해 숫자 레이어 (SolidBubbleRects가 그 로컬로 바꿔 준다 — 같은 크기 형제면 그대로)</summary>
        RectTransform _numbers;
        bool _ready;
        RectTransform[] _avoid;
        readonly Vector3[] _corners = new Vector3[4];
        RectTransform _guide;
        readonly RectTransform[] _banners = new RectTransform[2];
        readonly CanvasGroup[] _bannerGroups = new CanvasGroup[2];
        /// <summary>이번 프레임의 안내 줄 · 배너 자리 (_area 로컬)</summary>
        Rect _guideRect;
        bool _guideOn;
        readonly Rect[] _bannerRects = new Rect[2];
        readonly bool[] _bannerOn = new bool[2];
        int _zoneStamp = -1;
        /// <summary>마지막으로 준 뜬 차례 (Bubble.Born) / 지금 떠 있는 말풍선 수 (없으면 Update가 자리를 재지 않고 바로 돌아간다)</summary>
        int _spawnSeq;
        int _alive;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_ready) return;
            _ready = true;
            // 말풍선 레이어(Field/Camera/ShakeRoot/Callouts)의 로컬 좌표 = 전장 카메라 로컬이라 보이는 띠를 그대로 쓴다
            _mapper = _area != null ? _area.GetComponentInParent<BattleFieldMapper>(true) : null;
            _fx = _area != null && _area.parent != null ? _area.parent.GetComponentInChildren<BattleFx>(true) : null;
            // 피해 숫자 레이어(같은 ShakeRoot 아래 Numbers)가 말풍선 위에 그려지게 (Wave 3 r2 — 말풍선이 막 뜬 숫자를 반쯤 가렸다).
            // 빌더는 Callouts를 Numbers 앞에 만든다 — 예전 씬(Callouts가 마지막)은 여기서 한 번 Numbers 앞으로 옮긴다
            if (_area != null && _area.parent != null)
            {
                var numbers = _area.parent.Find(NumbersLayer);
                if (numbers != null && numbers.GetSiblingIndex() < _area.GetSiblingIndex()) _area.SetSiblingIndex(numbers.GetSiblingIndex());
                _numbers = numbers as RectTransform;
            }
            int count = _bubbles != null ? _bubbles.Length : 0;
            _state = new Bubble[count];
            _order = new int[count];
            _others = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (_bubbles[i] != null) _bubbles[i].gameObject.SetActive(false);
                // M41: 글자 18~22pt (예전 씬은 15 — 빌더를 다시 돌리지 않아도 맞춘다)
                if (_texts != null && i < _texts.Length && _texts[i] != null)
                {
                    _texts[i].enableAutoSizing = true;
                    _texts[i].fontSizeMin = FontMin;
                    _texts[i].fontSizeMax = FontMax;
                }
            }
            for (int i = 0; i < _guardianLast.Length; i++) _guardianLast[i] = -99f;
            for (int i = 0; i < EchoMemory; i++)
            {
                _echoIds[i] = -1;
                _echoLast[i] = -99f;
            }
            for (int i = 0; i < DedupeSlots; i++)
            {
                _recentAt[i] = -99f;
                _recentSpeaker[i] = -1;
            }
            if (_avoidRoot != null)
            {
                _guide = _avoidRoot.Find(GuidePath) as RectTransform;
                for (int i = 0; i < BannerPaths.Length && i < _banners.Length; i++)
                {
                    _banners[i] = _avoidRoot.Find(BannerPaths[i]) as RectTransform;
                    _bannerGroups[i] = _banners[i] != null ? _banners[i].GetComponent<CanvasGroup>() : null;
                }
            }
        }

        /// <summary>
        /// 지킴이 대사 (간격 제한 · 같은 대사 · 개수에 걸리면 무시). important = 큰 스킬 · 불 밝히기 연계 (15초 · 8초 도배 규칙을 건너뛴다). 띄웠으면 true
        /// </summary>
        public bool Say(GuardianSlotView guardian, string text, Color accent, bool important = false)
        {
            if (guardian == null || string.IsNullOrEmpty(text)) return false;
            Prepare();
            int slot = guardian.Slot;
            bool tracked = slot >= 0 && slot < _guardianLast.Length;
            if (tracked && Time.time - _guardianLast[slot] < _minInterval) return false;
            int index = Take(text, tracked ? slot : -1, important);
            if (index < 0) return false;
            ref var b = ref _state[index];
            b.Guardian = guardian;
            b.Echo = null;
            b.EchoId = -1;
            b.Last = guardian.HeadPoint;
            b.Speaker = tracked ? slot : -1;
            if (!Begin(index, text, accent)) return false;
            if (tracked) _guardianLast[slot] = Time.time;
            return true;
        }

        /// <summary>
        /// 쓰다듬기 말 (CR-10 — GuardianPetView): 스킬 이름이 아니라 CalloutGate를 거치지 않고, 말하기 간격 · 도배 규칙(15초 · 8초)도 건너뛴다.
        /// 같은 대사 2.5초 · 떠 있는 수 · 비키기(M41)는 그대로. 띄웠으면 true
        /// </summary>
        public bool SayPet(GuardianSlotView guardian, string text, Color accent)
        {
            if (guardian == null || string.IsNullOrEmpty(text)) return false;
            Prepare();
            int slot = guardian.Slot;
            bool tracked = slot >= 0 && slot < _guardianLast.Length;
            int index = Take(text, tracked ? slot : -1, true);
            if (index < 0) return false;
            ref var b = ref _state[index];
            b.Guardian = guardian;
            b.Echo = null;
            b.EchoId = -1;
            b.Last = guardian.HeadPoint;
            b.Speaker = tracked ? slot : -1;
            if (!Begin(index, text, accent)) return false;
            if (tracked) _guardianLast[slot] = Time.time;
            return true;
        }

        /// <summary>잔향이 대사 (따라다닌다). important = 15초 · 8초 도배 규칙을 건너뛴다. 띄웠으면 true</summary>
        public bool Say(EchoView echo, string text, Color accent, bool important = false)
        {
            if (echo == null || string.IsNullOrEmpty(text)) return false;
            Prepare();
            if (!EchoReady(echo.Id)) return false;
            int index = Take(text, echo.Id, important);
            if (index < 0) return false;
            ref var b = ref _state[index];
            b.Guardian = null;
            b.Echo = echo;
            b.EchoId = echo.Id;
            b.Last = echo.HeadPoint;
            b.Speaker = echo.Id;
            if (!Begin(index, text, accent)) return false;
            NoteEcho(echo.Id);
            return true;
        }

        /// <summary>
        /// 그 자리에 남기는 대사 (빛으로 풀리며 스킬을 쓴 잔향이처럼 말할 몸이 이미 없을 때). head = 전장 로컬 머리 높이.
        /// 같은 잔향이(echoId)는 일정 간격 안에 다시 말하지 않는다. 띄웠으면 true
        /// </summary>
        public bool Say(Vector2 head, string text, Color accent, int echoId = -1, bool important = false)
        {
            if (string.IsNullOrEmpty(text)) return false;
            Prepare();
            if (echoId >= 0 && !EchoReady(echoId)) return false;
            int index = Take(text, echoId, important);
            if (index < 0) return false;
            ref var b = ref _state[index];
            b.Guardian = null;
            b.Echo = null;
            b.EchoId = -1;
            b.Last = head;
            b.Speaker = echoId;
            if (!Begin(index, text, accent)) return false;
            if (echoId >= 0) NoteEcho(echoId);
            return true;
        }

        /// <summary>남은 말풍선을 모두 닫는다 (스테이지 전환 — 잔향이 ID는 판마다 다시 매겨지므로 말하는 친구 기록도 지운다, 같은 대사 기록은 남긴다)</summary>
        public void Clear()
        {
            if (!_ready) return;
            for (int i = 0; i < _state.Length; i++) Hide(i);
            for (int i = 0; i < DedupeSlots; i++) _recentSpeaker[i] = -1;
        }

        /// <summary>
        /// 피해 숫자가 비켜 갈 단단한 말풍선 (Wave 3 r3 → RM-2: 회복 · 피해 숫자가 말하는 친구 자기 말풍선으로 떠올라 글자를 덮었다):
        /// 떠 있고 묶음 알파 0.5 이상 · 나이가 수명의 0.75 미만인 말풍선 바탕 상자를 피해 숫자 레이어 로컬로 into에 채운다 (발밑 + 띄움 ~ + 60px, 폭 그대로).
        /// 채운 수를 돌려준다 (into 길이까지, 할당 없음). 말풍선이 없으면 0
        /// </summary>
        public int SolidBubbleRects(Rect[] into)
        {
            if (!_ready || into == null || _alive <= 0 || _state == null) return 0;
            int count = 0;
            for (int i = 0; i < _state.Length && count < into.Length; i++)
            {
                ref var b = ref _state[i];
                if (!b.Alive) continue;
                float alpha = _groups != null && i < _groups.Length && _groups[i] != null ? _groups[i].alpha : 1f;
                if (!IsSolid(alpha, _life > 0f ? b.Age / _life : 1f)) continue;
                var r = BubbleRect(b.Base, b.Lift, b.Width);
                into[count++] = _numbers != null && _area != null && _numbers != _area ? ToNumbers(r) : r;
            }
            return count;
        }

        /// <summary>단단한 말풍선인지 (묶음 알파 ≥ 0.5 · 나이 비율 &lt; 0.75 — 사라지기 시작하면 숫자가 비키지 않는다). 순수 함수</summary>
        internal static bool IsSolid(float alpha, float ageFraction) => alpha >= SolidAlpha && ageFraction < SolidAge;

        /// <summary>말풍선 바탕 상자 (발밑 foot + 띄움 lift부터 위로 60px, 가운데 정렬 폭 width — _area 로컬). 순수 함수</summary>
        internal static Rect BubbleRect(Vector2 foot, float lift, float width) =>
            new(foot.x - width * 0.5f, foot.y + lift, width, BubbleHeight);

        Rect ToNumbers(Rect r)
        {
            Vector2 a = _numbers.InverseTransformPoint(_area.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
            Vector2 b = _numbers.InverseTransformPoint(_area.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>지금 떠 있는 말풍선 수 (시험장 · 캡처 확인용)</summary>
        public int AliveCount
        {
            get
            {
                if (!_ready) return 0;
                int alive = 0;
                for (int i = 0; i < _state.Length; i++)
                {
                    if (_state[i].Alive) alive++;
                }
                return alive;
            }
        }

        void Hide(int index)
        {
            ref var b = ref _state[index];
            if (b.Alive) _alive = Mathf.Max(0, _alive - 1);
            b.Alive = false;
            b.Echo = null;
            b.Guardian = null;
            b.Text = null;
            if (_bubbles[index] != null) _bubbles[index].gameObject.SetActive(false);
        }

        /// <summary>이 잔향이가 말할 수 있는지 (간격 — 기록은 띄운 뒤 NoteEcho)</summary>
        bool EchoReady(int id)
        {
            float now = Time.time;
            for (int i = 0; i < EchoMemory; i++)
            {
                if (_echoIds[i] == id) return now - _echoLast[i] >= _minInterval;
            }
            return true;
        }

        void NoteEcho(int id)
        {
            float now = Time.time;
            for (int i = 0; i < EchoMemory; i++)
            {
                if (_echoIds[i] != id) continue;
                _echoLast[i] = now;
                return;
            }
            _echoIds[_echoCursor] = id;
            _echoLast[_echoCursor] = now;
            _echoCursor = (_echoCursor + 1) % EchoMemory;
        }

        /// <summary>같은 대사가 떠 있거나 고리(16칸)에 걸리는지 — 2.5초 같은 대사 · 15초 같은 대사 · 8초 같은 친구 (RecentlySaid)</summary>
        bool Repeats(string text, int speaker, bool important)
        {
            for (int i = 0; i < _state.Length; i++)
            {
                if (_state[i].Alive && string.Equals(_state[i].Text, text)) return true;
            }
            return RecentlySaid(_recentText, _recentAt, _recentSpeaker, text, speaker, Time.time, important);
        }

        /// <summary>
        /// 고리 기록에 걸리는지 (순수 함수, 할당 없음): 같은 대사가 2.5초 안 (언제나), 중요하지 않으면 같은 대사 15초 안 또는 같은 친구(speaker ≥ 0) 8초 안
        /// </summary>
        internal static bool RecentlySaid(string[] texts, float[] times, int[] speakers, string text, int speaker, float now, bool important)
        {
            if (texts == null || times == null) return false;
            int n = Mathf.Min(texts.Length, times.Length);
            for (int i = 0; i < n; i++)
            {
                float age = now - times[i];
                if (age < 0f) continue;
                bool same = texts[i] != null && string.Equals(texts[i], text);
                if (same && age < DedupeWindow) return true;
                if (important) continue;
                if (same && age < SameTextWindow) return true;
                if (speaker >= 0 && speakers != null && i < speakers.Length && speakers[i] == speaker && age < SpeakerWindow) return true;
            }
            return false;
        }

        void Remember(string text, int speaker)
        {
            _recentText[_recentCursor] = text;
            _recentAt[_recentCursor] = Time.time;
            _recentSpeaker[_recentCursor] = speaker;
            _recentCursor = (_recentCursor + 1) % DedupeSlots;
        }

        /// <summary>빈 말풍선 (같은 대사 · 도배 규칙에 걸리면, 이미 AliveCap개면, 빈 칸이 없으면 −1 — 떠 있는 것을 빼앗지 않는다)</summary>
        int Take(string text, int speaker, bool important)
        {
            if (Repeats(text, speaker, important)) return -1;
            int alive = 0;
            int free = -1;
            for (int i = 0; i < _state.Length; i++)
            {
                if (_bubbles[i] == null) continue;
                if (_state[i].Alive) alive++;
                else if (free < 0) free = i;
            }
            bool sheet = UIFrame.SheetProgress >= SheetFull || (_fx != null && _fx.DevSheetCover > 0f);
            return alive >= AliveCap(_maxAlive, sheet) ? -1 : free;
        }

        /// <summary>
        /// 한꺼번에 떠 있을 수 있는 말풍선 수: 최소 1, SHEET(시트가 다 열림)이면 SheetMaxAlive(2)까지 (열리기 전에 떠 있던 말풍선은 그대로 둔다 — 새 대사만 막는다)
        /// </summary>
        internal static int AliveCap(int maxAlive, bool sheet)
        {
            int cap = Mathf.Max(1, maxAlive);
            return sheet ? Mathf.Min(cap, SheetMaxAlive) : cap;
        }

        /// <summary>
        /// 말풍선을 띄운다. 뜨자마자 막히면(배너 · 수선점 이름표를 못 비키거나, 앞선 말풍선 위로 올릴 자리가 없거나,
        /// 올린 자리가 HUD 칸 · 보스 이름표 · 수선점 이름표 · 배너에 들어가면) 띄우지 않고 false
        /// </summary>
        bool Begin(int index, string text, Color accent)
        {
            ref var b = ref _state[index];
            b.Age = 0f;
            b.Tilt = Random.value < 0.5f ? -1f : 1f;
            b.Lift = 0f;
            b.Fresh = true;
            b.Width = Mathf.Clamp(text.Length * _charWidth + _padding, MinWidth, MaxWidth);
            RefreshZones();
            var half = AreaHalf();
            VisibleBand(half, out float floorY, out float ceiling);
            b.Base = Place(b.Last, 0f, b.Width, half, floorY, ceiling, out bool stuck, out _);
            int count = 0;
            for (int i = 0; i < _state.Length; i++)
            {
                if (i == index || !_state[i].Alive) continue;
                ref var other = ref _state[i];
                _others[count++] = new Vector3(other.Base.x, other.Base.y + other.Lift, other.Width);
            }
            float lift = BattleKeepOut.BubbleLift(b.Base, b.Width, BubbleHeight, _others, count, LiftGap, LiftSteps, LiftSteps * LiftStep, ceiling,
                out bool blocked);
            if (stuck || blocked || LiftHitsKeepOut(b.Base, b.Width, lift))
            {
                b.Guardian = null;
                b.Echo = null;
                return false;
            }
            b.Alive = true;
            b.Born = ++_spawnSeq;
            _alive++;
            b.Text = text;
            b.Lift = lift;
            Remember(text, b.Speaker);

            var bubble = _bubbles[index];
            bubble.gameObject.SetActive(true);
            bubble.SetAsLastSibling();
            bubble.localScale = Vector3.zero;
            bubble.localRotation = Quaternion.identity;
            if (index < _texts.Length && _texts[index] != null) _texts[index].text = text;
            if (index < _backs.Length && _backs[index] != null) _backs[index].sizeDelta = new Vector2(b.Width, _backs[index].sizeDelta.y);
            if (index < _accents.Length && _accents[index] != null)
            {
                _accents[index].color = _accentBlend > 0f ? Color.Lerp(Color.white, accent, Mathf.Min(_accentBlend, 0.35f)) : BattleEase.WithAlpha(accent, 0.95f);
                // 아트 꼬리는 바탕과 같은 색 (꼬리만 흰색이면 바탕과의 이음매가 드러난다)
                if (_accentBlend > 0f && _tails != null && index < _tails.Length && _tails[index] != null) _tails[index].color = _accents[index].color;
            }
            bubble.anchoredPosition = new Vector2(b.Base.x, b.Base.y + b.Lift);
            return true;
        }

        void Update()
        {
            // 떠 있는 말풍선이 없으면 자리(안내 줄 · 배너 · HUD 칸)를 재지 않는다 — 대부분의 프레임 (Begin이 뜰 때 스스로 잰다)
            if (!_ready || _alive <= 0) return;
            float dt = Time.deltaTime;
            RefreshZones();
            var half = AreaHalf();
            VisibleBand(half, out float floorY, out float ceiling);
            int count = 0;
            for (int i = 0; i < _state.Length; i++)
            {
                ref var b = ref _state[i];
                if (!b.Alive) continue;
                b.Age += dt;
                float k = b.Age / _life;
                if (k >= 1f)
                {
                    Hide(i);
                    continue;
                }

                if (b.Echo != null)
                {
                    if (b.Echo.Id == b.EchoId && b.Echo.isActiveAndEnabled) b.Last = b.Echo.HeadPoint;
                    else b.Echo = null;
                }
                else if (b.Guardian != null) b.Last = b.Guardian.HeadPoint;

                // 톡 튀어나와(0.16초) 머물다가 마지막 25%에 떠오르며 사라진다. 튀어나올 때 살짝 눌렸다 펴지고(±7%) 기울었다 선다(±4°, 0.3초에 잦아든다)
                float pop = k < 0.1f ? BattleEase.OutBack(k / 0.1f, 2.8f) : 1f;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, k));
                float rise = 14f * BattleEase.OutCubic(Mathf.InverseLerp(0.7f, 1f, k)) + 3f * Mathf.Sin(b.Age * 4f);
                float wobble = b.Age < 0.3f ? Mathf.Sin(b.Age * 34f) * (1f - b.Age / 0.3f) : 0f;
                _bubbles[i].localScale = new Vector3(pop * (1f + 0.07f * wobble), pop * (1f - 0.07f * wobble), 1f);
                _bubbles[i].localRotation = Quaternion.Euler(0f, 0f, 4f * wobble * b.Tilt);
                if (i < _groups.Length && _groups[i] != null) _groups[i].alpha = fade;
                var placed = Place(b.Last, rise, b.Width, half, floorY, ceiling, out bool stuck, out bool hold);
                // 보스 자리(이름표 + 몸)에 막히면 지난 자리에 멈춘 채 사라진다 (따라가면 보스 머리 · 몸을 덮는다 — Wave 3 r2)
                if (!hold) b.Base = placed;
                if (stuck) FadeEarly(ref b);
                _order[count++] = i;
            }
            if (count == 0) return;
            SortOldestFirst(count);
            Separate(count, dt, ceiling);
        }

        /// <summary>일찍 사라지기 시작한다 (나이 = 수명 × 0.75 — 이미 지났으면 그대로)</summary>
        void FadeEarly(ref Bubble b)
        {
            float at = EarlyFade * _life;
            if (b.Age < at) b.Age = at;
        }

        /// <summary>
        /// 살아 있는 말풍선을 오래된 것부터 — 뜬 차례(Born)로 (나이는 일찍 사라지기가 올리므로 쓰지 않는다). 삽입 정렬, 5개 이하 — 할당 없음
        /// </summary>
        void SortOldestFirst(int count)
        {
            for (int i = 1; i < count; i++)
            {
                int index = _order[i];
                int born = _state[index].Born;
                int j = i - 1;
                while (j >= 0 && _state[_order[j]].Born > born)
                {
                    _order[j + 1] = _order[j];
                    j--;
                }
                _order[j + 1] = index;
            }
        }

        /// <summary>
        /// 겹침 띄움 (오래된 것부터): 앞선 말풍선들의 지금 자리(제자리 + 띄움)와 겹치면 BattleKeepOut.BubbleLift만큼 올린다 (600px/s로 따라감).
        /// 막히면(2번 올려도 겹침 · 112px 넘음 · 천장) 일찍 사라진다. 올릴 자리가 HUD 칸 · 보스 이름표 묶음 · 보이는 배너에 들어가도 막힌 것으로 —
        /// 그때는 지금 띄움 그대로 두고 일찍 사라진다 (HUD · 배너 밑으로 숨거나 이름표를 덮지 않게)
        /// </summary>
        void Separate(int count, float dt, float ceiling)
        {
            for (int o = 0; o < count; o++)
            {
                int i = _order[o];
                ref var b = ref _state[i];
                int n = 0;
                for (int p = 0; p < o; p++)
                {
                    ref var other = ref _state[_order[p]];
                    _others[n++] = new Vector3(other.Base.x, other.Base.y + other.Lift, other.Width);
                }
                float lift = BattleKeepOut.BubbleLift(b.Base, b.Width, BubbleHeight, _others, n, LiftGap, LiftSteps, LiftSteps * LiftStep, ceiling,
                    out bool blocked);
                if (!blocked && LiftHitsKeepOut(b.Base, b.Width, lift))
                {
                    blocked = true;
                    lift = b.Lift;
                }
                if (blocked) FadeEarly(ref b);
                // 움직임 줄이기(UIMotion.Reduced)면 띄움도 바로
                b.Lift = b.Fresh || UIMotion.Reduced ? lift : Mathf.MoveTowards(b.Lift, lift, LiftSpeed * dt);
                b.Fresh = false;
                _bubbles[i].anchoredPosition = new Vector2(b.Base.x, b.Base.y + b.Lift);
            }
        }

        /// <summary>
        /// 띄운 말풍선 상자(제자리 foot + lift, 폭 width · 높이 60)가 HUD 칸 · 보스 자리(이름표 묶음 + 몸) · 수선점 이름표 · 보이는 배너와 겹치는지
        /// (이번 프레임에 잰 자리 — 할당 없음). 띄움이 없으면 false (제자리는 Place가 이미 비켰다). 연출(BattleFx)이 없으면 false
        /// </summary>
        bool LiftHitsKeepOut(Vector2 foot, float width, float lift)
        {
            if (_fx == null || lift <= 0.5f) return false;
            var h = new Vector2(width * 0.5f, BubbleHeight * 0.5f);
            var c = new Vector2(foot.x, foot.y + lift + h.y);
            int hud = _fx.HudKeepOutCount;
            for (int i = 0; i < hud; i++)
            {
                if (BattleKeepOut.Overlaps(c, h, _fx.HudKeepOut(i), 0f)) return true;
            }
            int plates = _fx.PlateKeepOutCount;
            for (int i = 0; i < plates; i++)
            {
                if (BattleKeepOut.Overlaps(c, h, _fx.PlateBodyKeepOut(i), 0f)) return true;
            }
            if (_fx.ShopPlateKeepOut(out var shop) && BattleKeepOut.Overlaps(c, h, shop, 0f)) return true;
            for (int i = 0; i < _banners.Length; i++)
            {
                if (_bannerOn[i] && BattleKeepOut.Overlaps(c, h, _bannerRects[i], 0f)) return true;
            }
            return false;
        }

        Vector2 AreaHalf() => _area != null ? _area.rect.size * 0.5f : new Vector2(540f, 732f);

        /// <summary>
        /// 머리 위에 두되 보이는 전장 밖으로 나가지 않게 자른다: 가로는 전장 폭, 세로는 천장(VisibleTop − 70) 아래 · 바닥 _floorY(VisibleBottom + 24) 위.
        /// 그다음 HUD · 안내 줄(MAIN) · 보스 자리(이름표 + 몸) · 수선점 이름표 · 배너를 비키고 다시 띠 안으로 (시트 · 덱 탭 밑으로는 가지 않는다).
        /// stuck = 배너 · 수선점 이름표 · 보스 자리를 못 비켰다, hold = 그중 보스 자리 (떠 있던 말풍선은 지난 자리에 머문다)
        /// </summary>
        Vector2 Place(Vector2 head, float rise, float width, Vector2 half, float floorY, float ceiling, out bool stuck, out bool hold)
        {
            stuck = false;
            hold = false;
            var position = head + Vector2.up * (10f + rise);
            if (_area == null) return position;
            float limit = Mathf.Max(0f, half.x - width * 0.5f - 8f);
            position.x = Mathf.Clamp(position.x, -limit, limit);
            position.y = Mathf.Clamp(position.y, floorY, ceiling);
            position = _fx != null ? AvoidKeepOuts(position, width, floorY, ceiling, limit, out stuck, out hold) : AvoidHud(position, width, floorY, limit);
            position.y = Mathf.Clamp(position.y, floorY, ceiling);
            return position;
        }

        /// <summary>
        /// 말풍선 발밑(pivot)이 머물 세로 띠 (_area 로컬 = 전장 카메라 로컬, 가운데 원점): 바닥 _floorY = VisibleBottom + 24, 천장 = VisibleTop − 70.
        /// MAIN에서 천장은 예전(_area 반높이 − 70) 그대로. 지도가 없으면 _area 높이로.
        /// 연출 시험장의 SHEET 변형(BattleFx.DevSheetCover &gt; 0)이면 바닥도 흉내 낸 시트 위 끝으로 (숫자 바닥 FieldBottom과 같게)
        /// </summary>
        void VisibleBand(Vector2 half, out float floorY, out float ceiling)
        {
            float top = _mapper != null ? _mapper.VisibleTop : half.y;
            float bottom = (_mapper != null ? _mapper.VisibleBottom : -half.y) + (_fx != null ? _fx.DevSheetCover : 0f);
            floorY = bottom + FloorLift;
            ceiling = Mathf.Max(floorY, top - CeilingDrop);
        }

        /// <summary>
        /// 이번 프레임의 안내 줄 · 배너 자리 (_area 로컬, 한 프레임에 한 번). 안내 줄은 MAIN(UIFrame.SheetProgress ≤ 0 · 시험장 SHEET 변형 아님)에서
        /// 켜져 있을 때만, 배너는 켜져 있고 묶음 알파가 0.05 이상일 때만
        /// </summary>
        void RefreshZones()
        {
            int frame = Time.frameCount;
            if (_zoneStamp == frame && Application.isPlaying) return;
            _zoneStamp = frame;
            _guideOn = _area != null && _guide != null && UIFrame.SheetProgress <= 0f && (_fx == null || _fx.DevSheetCover <= 0f) &&
                       _guide.gameObject.activeInHierarchy;
            if (_guideOn) _guideRect = LocalRect(_guide);
            for (int i = 0; i < _banners.Length; i++)
            {
                // 가운데 배너: WP-5A 배너 띠 (보상 줄까지 — Fx 레이어 로컬 = 이 레이어 로컬)
                if (i == 0 && _fx != null)
                {
                    _bannerOn[0] = _fx.BannerKeepOut(out var keepOut);
                    _bannerRects[0] = keepOut;
                    continue;
                }
                var banner = _banners[i];
                bool on = _area != null && banner != null && banner.gameObject.activeInHierarchy &&
                          (_bannerGroups[i] == null || _bannerGroups[i].alpha >= BannerAlpha);
                _bannerOn[i] = on;
                if (on) _bannerRects[i] = LocalRect(banner);
            }
        }

        /// <summary>RectTransform의 네 귀퉁이를 _area 로컬 사각형으로 (배율 · 위치 애니메이션 포함)</summary>
        Rect LocalRect(RectTransform target)
        {
            target.GetWorldCorners(_corners);
            Vector2 a = _area.InverseTransformPoint(_corners[0]);
            Vector2 b = _area.InverseTransformPoint(_corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// 비키기 (말풍선 상자 = 발밑에서 위로 60px, 폭 width): HUD 칸은 옆·아래로 (두 번 훑는다), 안내 줄은 MAIN에서 그 위로,
        /// 보스 자리(이름표 묶음 + 몸)는 위·옆, 안 되면 몸 아래 끝 밑으로 112px 안에서, 수선점 이름표는 위·옆으로 112px 안에서, 보이는 배너는 위·아래로 — 모두 가장 조금.
        /// 배너나 수선점 이름표와 아직 겹치면 stuck (새 말풍선은 띄우지 않고, 떠 있던 것은 일찍 사라진다).
        /// 보스 자리와 아직 겹치면 stuck + hold (떠 있던 것은 지난 자리에 멈춘 채 사라진다 — 보스를 따라 덮지 않게)
        /// </summary>
        Vector2 AvoidKeepOuts(Vector2 foot, float width, float floorY, float ceiling, float limit, out bool stuck, out bool hold)
        {
            stuck = false;
            hold = false;
            var h = new Vector2(width * 0.5f, BubbleHeight * 0.5f);
            var c = new Vector2(foot.x, foot.y + h.y);
            float yMin = floorY + h.y;
            float yMax = ceiling + h.y;
            int hud = _fx.HudKeepOutCount;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < hud; i++) c = BattleKeepOut.PushOut(c, h, _fx.HudKeepOut(i), KeepGap, BattleKeepOut.Down | BattleKeepOut.Side, limit, yMin, yMax);
            }
            // 안내 줄 (MAIN만): 가로로 겹치면 말풍선 아래 끝을 안내 줄 위 + 6px로
            if (_guideOn && c.x + h.x > _guideRect.xMin - GuidePad && c.x - h.x < _guideRect.xMax + GuidePad)
            {
                float bottom = _guideRect.yMax + GuidePad + h.y;
                if (c.y < bottom) c.y = Mathf.Min(bottom, yMax);
            }
            // 보스 자리 (이름표 묶음 + 몸 — BattleFx.PlateBodyKeepOut, Wave 3 r2): 위·옆 → 몸 아래 끝 밑, 띄움 한도(112px) 안 · HUD 칸 밖에서. 못 비키면 stuck이고
            // 지난 자리에 머문다 (hold — 예전에는 어느 쪽으로든 밀어 SHEET에서 스테이지 카드 밑 이름표 아래로 내려간 말풍선이 보스 머리와 몸을 덮었다)
            int plates = _fx.PlateKeepOutCount;
            var hudRects = _fx.HudKeepOutRects;
            for (int i = 0; i < plates; i++)
            {
                c = BattleKeepOut.BubbleBossClear(c, h, _fx.PlateBodyKeepOut(i), KeepGap, BossMove, limit, yMin, yMax, hudRects, hud, out bool bossBlocked);
                if (bossBlocked) hold = true;
            }
            // 수선점 이름표 (SHEET에서 줄어든 전장의 앞줄 친구 말풍선이 덮던 자리): 위·옆으로만, 띄움 한도 안에서. 못 비키면 stuck
            bool shopOn = _fx.ShopPlateKeepOut(out var shopPlate);
            if (shopOn)
            {
                c = BattleKeepOut.BubblePlateClear(c, h, shopPlate, KeepGap, ShopPlateMove, limit, yMax, out bool shopBlocked);
                if (shopBlocked) stuck = true;
            }
            for (int i = 0; i < _banners.Length; i++)
            {
                if (!_bannerOn[i]) continue;
                c = BattleKeepOut.PushOut(c, h, _bannerRects[i], KeepGap, BattleKeepOut.Up | BattleKeepOut.Down, limit, yMin, yMax);
                if (BattleKeepOut.Overlaps(c, h, _bannerRects[i], 0f)) stuck = true;
            }
            // 배너를 비키다 이름표 위로 돌아왔으면 그것도 막힘
            if (shopOn && BattleKeepOut.Overlaps(c, h, shopPlate, 0f)) stuck = true;
            // 수선점 이름표 · 배너를 비키다 보스 자리로 돌아왔어도 막힘 (보스 머리 · 몸 위에는 어떤 말풍선도 두지 않는다)
            for (int i = 0; i < plates && !hold; i++)
            {
                if (BattleKeepOut.Overlaps(c, h, _fx.PlateBodyKeepOut(i), 0f)) hold = true;
            }
            if (hold) stuck = true;
            return new Vector2(c.x, c.y - h.y);
        }

        /// <summary>
        /// 연출(BattleFx)이 없을 때의 예전 비키기: 말풍선이 _avoidPaths HUD(권장 전투력 알약 등)에 가리지 않게 옆이나 아래로 가장 조금 비킨다 (아래는 바닥 floorY까지만).
        /// 말풍선 영역 = 발밑 pivot에서 위로 64px (꼬리 포함)
        /// </summary>
        Vector2 AvoidHud(Vector2 position, float width, float floorY, float limit)
        {
            if (_avoidRoot == null || _avoidPaths == null || _avoidPaths.Length == 0) return position;
            if (_avoid == null || _avoid.Length != _avoidPaths.Length) _avoid = new RectTransform[_avoidPaths.Length];
            const float height = 64f;
            const float gap = 6f;
            var h = new Vector2(width * 0.5f, height * 0.5f);
            for (int i = 0; i < _avoidPaths.Length; i++)
            {
                if (_avoid[i] == null && !string.IsNullOrEmpty(_avoidPaths[i])) _avoid[i] = _avoidRoot.Find(_avoidPaths[i]) as RectTransform;
                var target = _avoid[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                var c = new Vector2(position.x, position.y + h.y);
                c = BattleKeepOut.PushOut(c, h, LocalRect(target), gap, BattleKeepOut.Down | BattleKeepOut.Side, limit, floorY + h.y, float.MaxValue);
                position = new Vector2(c.x, c.y - h.y);
            }
            return position;
        }
    }
}
