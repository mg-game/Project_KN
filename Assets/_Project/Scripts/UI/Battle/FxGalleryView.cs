using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 공격 연출 시험장 (ATK-17, 개발 빌드 전용). 스테이지 이름표를 빠르게 5번 누르면 열린다 (FxGalleryTrigger).
    /// 시뮬레이터 없이 BattleFx만 돌려, 5방위에 세운 허수아비 적에게 모든 공격 연출을 1초 간격으로 차례로 재생한다:
    ///   지킴이 기본 공격 (계보 7 × 성향 6 × 단계 0/2) → 잔향이 공격 (계보 7 × 근접/원거리) → 적 공격 9종 (잔향이 / 수선점) → 보스 포위 예비 동작 →
    ///   처치 (적 9종 = 갈래 4) → 등장 (일반 / 보스 / 부하 소환) → 보호막 명중·깨짐 · 치명타 → 지원·힐 맥박 → 정전 빛 구멍 → 불 밝히기 → 스킬 연출 33종 →
    ///   (FX-JUICE) 치명타 처치 7 · 연속 처치 10 · 한꺼번에 5 · 스테이지 클리어 · 붐비는 전장 20 · 숫자 모두 → 설정 변형 (번쩍임 줄이기 · 흔들림 0 · 낮음 품질) →
    ///   예산 압박 (붐비는 전장 위에서 범위 스킬 + 치명타로 20마리 한꺼번에 처치 / 불 밝히기로 20마리 전멸 — 높음 · 보통 · 낮음).
    /// 열려 있는 동안 BattleView는 실제 전투의 적·잔향이를 그리지 않고 전투 연출도 쉰다 (시뮬레이션은 그대로 흐른다).
    /// 쏘는 쪽 몸짓도 실제 전투와 같은 길로 건다: 지킴이 PlayAttack(대상, LastWindup) · 치명타/처치 축하 Celebrate + CelebrationGlint · 스킬 PlayCast,
    /// 잔향이 PlayAttack / PlayHurt (잔향이 명중은 가벼운 잔향이 반응 echo). 재생 모드는 편성된 실제 지킴이(BattleView가 돌림)가 움직이고,
    /// Game이 없는 배치 캡처는 StandIns를 켜면 대역 지킴이 다섯 · 대역 잔향이를 세워 시험장이 직접 돌린다 (초상화는 에디터가 채운다 — StandInBound).
    /// ◀ ▶ 로 넘기고 ▶‖ 로 자동 재생을 멈추거나 이어 간다. 릴리스 빌드에서는 열리지 않는다.
    /// 배치 캡처(에디터 IdleGame.EditorTools.FxGallery.Capture)는 재생 없이 EntryCount / EntryKey / Stage / Play(index) / Advance / FocusPoint로
    /// 항목마다 여러 순간을 찍는다 (Play가 그 항목의 핵심 순간까지의 연출 시간을 돌려준다). 설정 변형은 BattleFx.DevOverride로만 (PlayerPrefs를 쓰지 않는다).
    /// </summary>
    public class FxGalleryView : MonoBehaviour
    {
        enum Kind : byte
        {
            GuardianShot,
            EchoAttack,
            StrikeEcho,
            StrikeShop,
            Siege,
            Death,
            Spawn,
            BossSpawn,
            Minion,
            ShieldHit,
            ShieldBreak,
            Crit,
            SupportPulse,
            HealPulse,
            LightHole,
            Lamp,
            Skill,
            CritKill,
            KillStreak,
            MultiKill,
            StageClear,
            Busy,
            Numbers,
            /// <summary>붐비는 전장(2배속) 0.6초 뒤 범위 스킬 + 치명타로 무리 20마리를 한꺼번에 처치</summary>
            MassKill,
            /// <summary>붐비는 전장(2배속) 0.6초 뒤 불 밝히기로 무리 20마리 전멸</summary>
            LampFull,
        }

        /// <summary>설정 변형 (BattleFx.DevOverride)</summary>
        enum Variant : byte
        {
            None,
            Reduce,
            Shake0,
            Low,
            Medium,
            /// <summary>보스를 보이는 띠 위 끝 − 40px에 세운다 (Wave 3 — 위쪽 보스 처치 당김 · 첫 프레임 잘림 확인, fx2 leftovers 비평 (a))</summary>
            Top,
            /// <summary>SHEET 흉내: 전투를 16:9 PanOpen(360)만큼 올리고 연출 바닥을 시트 위 끝으로 (BattleFx.DevSheetCover — 비평 (b))</summary>
            Sheet,
        }

        struct Entry
        {
            public Kind Kind;
            public string Id;
            public CombatStyle Style;
            public int Tier;
            public string Label;
            /// <summary>캡처 파일 이름 (verify_out/fx/{Key}.png, 목록을 만들 때 한 번)</summary>
            public string Key;
            public Variant Variant;
            /// <summary>화면 크기 연출 (캡처가 전장 전체 프레임도 찍는다)</summary>
            public bool ScreenScale;
        }

        sealed class Scarecrow
        {
            public EnemyView View;
            public EnemyState State;
            public int Slot;
            /// <summary>처치 연출 뒤 다시 세우기까지 남은 시간 (0 이하 = 서 있음)</summary>
            public float Respawn;
            public bool Exiting;
        }

        enum Scenario : byte
        {
            None,
            Busy,
            Streak,
            Multi,
            MassKill,
            LampFull,
        }

        [SerializeField] BattleFx _fx;
        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] EnemyViewPool _enemies;
        [SerializeField] ShopView _shop;
        [SerializeField] DirectionRingView _ring;
        [SerializeField] GuardianSlotView[] _guardians;
        [SerializeField] SkillVfxLibrary _library;
        [SerializeField] StageAmbienceView _ambience;
        [Tooltip("허수아비가 서는 레이어 (전장 액터 레이어 — 앞뒤 정렬을 함께 받는다)")]
        [SerializeField] RectTransform _stage;

        [Header("조작 패널")]
        [SerializeField] GameObject _panel;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _counter;
        [SerializeField] Button _prev;
        [SerializeField] Button _next;
        [SerializeField] Button _play;
        [SerializeField] TMP_Text _playLabel;
        [SerializeField] Button _close;
        [SerializeField] float _interval = 1f;

        const int IdBase = 990000;
        const int CrowdIdBase = IdBase + 100;
        /// <summary>허수아비가 서는 월드 반지름 (방위 링 밖, 부채꼴 안)</summary>
        const float ScarecrowRadius = 1.75f;
        /// <summary>보스 포위 예비 동작 시간 (BattleConfig.bossSiegeWindupSeconds 기본값)</summary>
        const float SiegeWindupTime = 0.35f;
        /// <summary>붐비는 전장·연속 처치에 세우는 허수아비 무리 최대 수</summary>
        const int CrowdMax = 20;
        const int BusyAttackers = 10;
        /// <summary>붐비는 전장: 1.5초, 2배속, 공격자 10명이 0.5초마다 (≈ 초당 20 도착), 0.6초에 범위 스킬, 0.9초에 치명타</summary>
        const float BusyLength = 1.5f;
        const float BusyInterval = 0.5f;
        const float BusySkillAt = 0.6f;
        const float BusyCritAt = 0.9f;
        /// <summary>예산 압박 장면: 붐비는 전투 MassLead초 뒤에 한꺼번에 처치 / 불 밝히기, 전체 MassLength초 (퇴장·다시 세우기까지)</summary>
        const float MassLead = 0.6f;
        const float MassLength = 2.4f;
        /// <summary>한꺼번에 처치의 범위 스킬 반지름 (월드 — 무리 세 겹 고리 전체를 덮는다) / 치명타는 무리 3마리에 하나</summary>
        const float MassRadius = 2.6f;
        const int MassCritEvery = 3;

        static readonly string[] Lineages = AttackFxCatalog.KnownLineages;
        static readonly CombatStyle[] Styles =
        {
            CombatStyle.Defense, CombatStyle.Assault, CombatStyle.Capture, CombatStyle.Snipe, CombatStyle.Support, CombatStyle.Heal,
        };
        static readonly CombatStyle[] BusyStyles = { CombatStyle.Defense, CombatStyle.Assault, CombatStyle.Snipe };

        readonly List<Entry> _entries = new();
        readonly Scarecrow[] _scarecrows = new Scarecrow[BattleGeometry.SlotCount];
        readonly Scarecrow[] _crowd = new Scarecrow[CrowdMax];
        readonly float[] _attackerTimer = new float[BusyAttackers];
        int _crowdCount;
        int _cursor;
        float _timer;
        bool _auto = true;
        bool _open;
        bool _blackout;
        StageModifierType _savedModifier;
        /// <summary>SHEET 변형 전의 전투 올림 (변형이 끝나면 되돌린다)</summary>
        float _savedPan;
        bool _sheetApplied;
        /// <summary>SHEET 변형 올림 (16:9 PanOpen — UX-SPEC-IA §1.3) / 위쪽 보스 변형의 발 높이 (보이는 띠 위 끝에서)</summary>
        const float SheetPan = 360f;
        const float TopBossInset = 40f;
        // 예약된 다음 동작 (보스 포위: 텔레그래프가 차오름 → 공격)
        float _pendingStrike = -1f;
        int _pendingSlot;
        string _pendingEnemy;
        Vector2 _pendingGround;
        float _pendingSize;
        /// <summary>마지막으로 재생한 항목의 볼 자리 (전장 로컬 — 캡처가 이 둘레를 자른다)</summary>
        Vector2 _focus;
        // 대본 (붐비는 전장 · 연속 처치 · 한꺼번에)
        Scenario _scenario;
        float _scenarioTime;
        float _scenarioLength;
        int _scenarioStep;
        int _busyShots;
        bool _busySkill;
        bool _busyCrit;
        bool _massDone;
        // 대역 (배치 캡처 — Game 없음)
        const int EchoStandInId = IdBase + 200;
        readonly GuardianState[] _standInGuardians = new GuardianState[BattleGeometry.SlotCount];
        readonly List<EchoState> _standInEchoes = new(1);
        EchoState _standInEcho;
        EchoViewPool _echoPool;
        /// <summary>받침대가 대역 지킴이로 묶여 있는지 (시험장이 돌린다)</summary>
        bool _standInsLive;

        /// <summary>시험장이 열려 있는지 (BattleView가 실제 전투 연출을 쉰다)</summary>
        public bool IsOpen => _open;

        /// <summary>Game이 준비되지 않았을 때(에디터 배치 캡처) 적 정의를 찾을 전투 설정</summary>
        public BattleConfig ConfigOverride { get; set; }

        /// <summary>
        /// 변형이 아닌 항목도 기본 설정(높음 품질 · 번쩍임 그대로 · 흔들림 100% · 숫자 모두)으로 돌린다 — 배치 캡처가 사용자 기기 설정과 상관없이 같은 그림을 찍게.
        /// 꺼져 있으면 (재생 모드에서 손으로 열 때) 변형이 아닌 항목은 기기 설정 그대로
        /// </summary>
        public bool ForceDefaults { get; set; }

        /// <summary>대본 길이를 바꾼다 (0 이하 = 기본, 캡처의 할당 확인은 길게 돌린다)</summary>
        public float ScenarioLengthOverride { get; set; }

        /// <summary>피해 숫자 배율 (1 = 보통). 할당 확인이 후반 큰 숫자(단위 표기 캐시 길)를 돌릴 때 키운다</summary>
        public float DamageScale { get; set; } = 1f;

        /// <summary>성능 대조: 입자 크기·색을 예전처럼 매 프레임 쓴다 (FxDevSettings.LegacyParticleWrites — 할당·성능 확인만)</summary>
        public bool LegacyParticleWrites { get; set; }

        /// <summary>허수아비를 (다시) 세운 횟수 — 할당 확인이 시험장 자신의 할당(새 EnemyState)이 든 프레임을 가려낸다</summary>
        public int RaiseCount { get; private set; }

        /// <summary>
        /// 대역 (배치 캡처 — Game 없음): 다섯 받침대에 대역 지킴이를, 잔향이 공격·잔향이 피격 항목에는 대역 잔향이를 세워 쏘는 쪽 몸짓
        /// (웅크림 → 놓음 → 반동 · 축하 통 뛰기 · 시전 · 잔향이 출렁 · 움찔)이 캡처에 보이게 한다. 끄면(기본) 예전처럼 빈 받침대 — 기준선 비교 프레임.
        /// 재생 모드(Game 준비됨)에서는 쓰지 않는다: 편성된 실제 지킴이를 BattleView가 묶고 돌리며, 시험장은 몸짓(PlayAttack · Celebrate · PlayCast)만 건다
        /// </summary>
        public bool StandIns { get; set; }

        /// <summary>Game이 준비되지 않았을 때(에디터 배치 캡처) 대역 지킴이·잔향이의 형태(초상화 · 역할 · 범위)를 찾을 캐릭터 데이터</summary>
        public CharacterDatabase CharactersOverride { get; set; }

        /// <summary>
        /// 대역을 묶은 직후 (지킴이 칸 GuardianSlotView 또는 잔향이 EchoView, 형태 ID). Game 없이는 BattleUnitArt가 초상화를 못 찾으므로
        /// 에디터 캡처가 데이터 에셋의 그림을 채운다. 형태 ID가 null이면 그 지킴이 칸의 대역을 방금 풀었다 (빈 받침대)
        /// </summary>
        public System.Action<Component, string> StandInBound { get; set; }

        /// <summary>항목 수 (목록이 없으면 만든다)</summary>
        public int EntryCount
        {
            get
            {
                if (_entries.Count == 0) BuildEntries();
                return _entries.Count;
            }
        }

        /// <summary>항목의 캡처 이름 (예: guardian_jjakjjak_assault_t2, death_enemy_moth, skill_vfx_star_burst, lamp@reduce)</summary>
        public string EntryKey(int index) => index >= 0 && index < EntryCount ? _entries[index].Key : null;

        /// <summary>항목의 화면 이름</summary>
        public string EntryLabel(int index) => index >= 0 && index < EntryCount ? _entries[index].Label : null;

        /// <summary>화면 크기 연출인지 (캡처가 전장 전체 프레임도 찍는다: 보스 처치 · 불 밝히기 · 치명타 · 스테이지 클리어 · 무리 · 큰 스킬 · 설정 변형)</summary>
        public bool EntryIsScreenScale(int index) => index >= 0 && index < EntryCount && _entries[index].ScreenScale;

        /// <summary>마지막으로 재생한 항목의 볼 자리 (전장 로컬, BattleFx 입자 레이어 기준)</summary>
        public Vector2 FocusPoint => _focus;

        /// <summary>
        /// 마지막으로 재생한 항목의 화면 연출 순간 (재생 뒤 연출 시계, 초): 보통은 핵심 순간(도착)과 같고, 불 밝히기는 터지는 순간(LampWave.Start).
        /// 캡처가 전장 전체 프레임을 이 순간 기준 실제 시간으로 찍는다 (전체 정지 동안에도 화면 효과가 보이게)
        /// </summary>
        public float ScreenMoment { get; private set; }

        /// <summary>전장 좌표 변환 (캡처가 볼 자리를 화면 좌표로 바꿀 때)</summary>
        public BattleFieldMapper Mapper => _mapper;

        /// <summary>개발 빌드(에디터 포함)에서만 쓸 수 있다</summary>
        public static bool Available => Debug.isDebugBuild;

        void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_prev != null) _prev.onClick.AddListener(() => Step(-1, true));
            if (_next != null) _next.onClick.AddListener(() => Step(1, true));
            if (_play != null) _play.onClick.AddListener(ToggleAuto);
            if (_close != null) _close.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            if (_open) Close();
        }

        // ───────────── 열기 / 닫기 ─────────────

        public void Open()
        {
            if (_open || !Available || _fx == null || _mapper == null) return;
            _open = true;
            if (_entries.Count == 0) BuildEntries();
            _cursor = 0;
            _timer = 0.6f;
            _auto = true;
            _blackout = false;
            _savedModifier = _ambience != null ? _ambience.Current : StageModifierType.None;
            _fx.ClearParticles();
            if (_panel != null) _panel.SetActive(true);
            for (int i = 0; i < _scarecrows.Length; i++) Raise(i, "enemy_dust", false);
            Refresh();
        }

        /// <summary>
        /// 항목을 재생할 무대만 세운다 (그 항목이 쓰는 허수아비를 미리 세우고 남은 연출을 지운다 — 배치 캡처가 허수아비가 다 나타난 뒤에 Play하도록).
        /// 자동 재생은 멈춘다. 설정 변형이면 BattleFx.DevOverride를 건다
        /// </summary>
        public void Stage(int index)
        {
            if (!_open || index < 0 || index >= _entries.Count) return;
            _auto = false;
            _pendingStrike = -1f;
            StopScenario();
            _fx.ClearParticles();
            RestoreAmbience();
            var entry = _entries[index];
            ApplyVariant(entry);
            int slot = index % BattleGeometry.SlotCount;
            switch (entry.Kind)
            {
                case Kind.StrikeEcho:
                case Kind.StrikeShop:
                case Kind.Siege:
                case Kind.Death:
                    HideCrowd();
                    Ensure(slot, entry.Id);
                    if (entry.Variant == Variant.Top) MoveToTop(slot);
                    break;
                case Kind.ShieldHit:
                case Kind.ShieldBreak:
                    HideCrowd();
                    Ensure(slot, "enemy_thimble");
                    break;
                case Kind.BossSpawn:
                case Kind.Minion:
                    HideCrowd();
                    Ensure(0, entry.Id);
                    break;
                case Kind.Busy:
                case Kind.MassKill:
                case Kind.LampFull:
                    SetSlotsVisible(false);
                    EnsureCrowd(CrowdMax);
                    break;
                case Kind.KillStreak:
                    SetSlotsVisible(false);
                    EnsureCrowd(10);
                    break;
                case Kind.MultiKill:
                    SetSlotsVisible(false);
                    EnsureCrowd(8);
                    break;
                case Kind.Numbers:
                case Kind.StageClear:
                    HideCrowd();
                    break;
                default:
                    HideCrowd();
                    Ensure(slot, "enemy_dust");
                    break;
            }
            SyncStandIns(entry, slot);
        }

        /// <summary>
        /// index번 항목을 지금 재생한다 (자동 재생은 멈춘다). 그 연출의 핵심 순간(투사체 도착 · 처치 그림이 터짐 · 포위 공격)까지의 연출 시간(초)을 돌려준다
        /// </summary>
        public float Play(int index)
        {
            if (!_open || index < 0 || index >= _entries.Count) return 0f;
            _auto = false;
            _cursor = index;
            float arrival = Play(_entries[index]);
            ScreenMoment = _entries[index].Kind switch
            {
                Kind.Lamp => LampWave.Start,
                Kind.LampFull => MassLead + LampWave.Start,
                _ => arrival,
            };
            Refresh();
            return arrival;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _pendingStrike = -1f;
            StopScenario();
            if (_fx != null)
            {
                _fx.EndSiegeTelegraph();
                _fx.DevOverride = default;
            }
            ApplySheet(false);
            if (_panel != null) _panel.SetActive(false);
            for (int i = 0; i < _scarecrows.Length; i++)
            {
                DestroyScarecrow(_scarecrows[i]);
                _scarecrows[i] = null;
            }
            for (int i = 0; i < _crowd.Length; i++)
            {
                DestroyScarecrow(_crowd[i]);
                _crowd[i] = null;
            }
            _crowdCount = 0;
            ReleaseStandIns();
            RestoreAmbience();
            if (_fx != null) _fx.ClearParticles();
        }

        static void DestroyScarecrow(Scarecrow scarecrow)
        {
            if (scarecrow?.View == null) return;
            // 에디터 배치 캡처(재생 안 함)에서는 바로 지운다
            if (Application.isPlaying) Destroy(scarecrow.View.gameObject);
            else DestroyImmediate(scarecrow.View.gameObject);
        }

        void ToggleAuto()
        {
            _auto = !_auto;
            _timer = 0.2f;
            Refresh();
        }

        void Step(int delta, bool manual)
        {
            if (_entries.Count == 0) return;
            if (manual) _auto = false;
            _cursor = (_cursor + delta + _entries.Count) % _entries.Count;
            Play(_entries[_cursor]);
            Refresh();
        }

        void Refresh()
        {
            if (_entries.Count == 0) return;
            if (_title != null) _title.text = _entries[_cursor].Label;
            if (_counter != null) _counter.SetText("{0} / {1}", _cursor + 1, _entries.Count);
            if (_playLabel != null) _playLabel.text = _auto ? "멈춤" : "자동";
        }

        /// <summary>설정 변형을 건다 (변형이 아니면 기본 설정 또는 기기 설정으로). 성능 대조(LegacyParticleWrites)는 기본 설정 위에</summary>
        void ApplyVariant(in Entry entry)
        {
            if (_fx == null) return;
            ApplySheet(entry.Variant == Variant.Sheet);
            // 위쪽 보스 · SHEET 변형은 자리만 바꾼다 — 설정은 기본 항목과 같게
            bool settingsVariant = entry.Variant != Variant.None && entry.Variant != Variant.Top && entry.Variant != Variant.Sheet;
            if (!settingsVariant && !ForceDefaults && !LegacyParticleWrites)
            {
                _fx.DevOverride = default;
                return;
            }
            var settings = FxDevSettings.Defaults;
            switch (entry.Variant)
            {
                case Variant.Reduce:
                    settings.ReduceFlash = true;
                    break;
                case Variant.Shake0:
                    settings.ShakeScale = 0f;
                    break;
                case Variant.Low:
                    settings.Quality = FxQuality.Low;
                    break;
                case Variant.Medium:
                    settings.Quality = FxQuality.Medium;
                    break;
            }
            settings.LegacyParticleWrites = LegacyParticleWrites;
            _fx.DevOverride = settings;
        }

        /// <summary>
        /// SHEET 변형: 전투를 SheetPan만큼 올리고(mapper.SetView — 바닥 그림 · 카메라) 연출 바닥을 시트 위 끝으로 흉내 낸다 (BattleFx.DevSheetCover = 836 − 156).
        /// UIFrame은 SheetController만 쓰므로 건드리지 않는다 — 날씨처럼 mapper.VisibleBottom을 직접 읽는 쪽은 MAIN 바닥 그대로. 말풍선 바닥 · 안내 줄 비키기와
        /// HUD 비키기 칸(BattleFx.KeepOut — SheetFade가 돌지 않으니 각 칸의 시트 알파로 센다)은 DevSheetCover를 읽어 SHEET처럼 잰다. 끄면 올림을 되돌린다
        /// </summary>
        void ApplySheet(bool sheet)
        {
            if (_mapper == null || _fx == null) return;
            if (sheet)
            {
                if (!_sheetApplied)
                {
                    _savedPan = _mapper.Pan;
                    _sheetApplied = true;
                }
                _mapper.SetView(SheetPan);
                _fx.DevSheetCover = UIFrame.SheetHeight - UIFrame.StripHeight;
                return;
            }
            if (!_sheetApplied) return;
            _sheetApplied = false;
            _mapper.SetView(_savedPan);
            _fx.DevSheetCover = 0f;
        }

        /// <summary>
        /// 위쪽 보스 변형: slot 허수아비의 발을 보이는 띠 위 끝 − 40px로 (가로는 그대로 — MAIN 전장 y 692). 이미 거기 서 있으면 그대로, 아니면 다시 묶어 그 자리에 세운다
        /// </summary>
        void MoveToTop(int slot)
        {
            var scarecrow = _scarecrows[slot];
            if (scarecrow?.View == null || scarecrow.State == null) return;
            var field = _mapper.WorldToField(scarecrow.State.Position);
            float top = _fx.FieldTop - TopBossInset;
            if (Mathf.Abs(field.y - top) < 1f) return;
            field.y = top;
            scarecrow.State.Position = _mapper.FieldToWorld(field);
            scarecrow.View.Bind(scarecrow.State, _mapper);
        }

        // ───────────── 갱신 ─────────────

        void Update()
        {
            if (!_open) return;
            Advance(Time.deltaTime * _fx.TimeScale);
            if (!_auto) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _interval;
            Play(_entries[_cursor]);
            Refresh();
            _cursor = (_cursor + 1) % _entries.Count;
        }

        /// <summary>허수아비·무리·대역과 예약된 동작(보스 포위)·대본을 연출 시간 dt만큼 진행한다 (Update가 부르고, 배치 캡처는 직접 일정한 간격으로)</summary>
        public void Advance(float dt)
        {
            if (!_open) return;
            TickScenario(dt);
            TickScarecrows(dt);
            TickCrowd(dt);
            TickSiege(dt);
            TickStandIns(dt);
        }

        /// <summary>보스 포위 예비 동작: 차오름(0→1)을 허수아비(몸 젖힘)와 텔레그래프에 매 프레임 넣고, 다 차면 덮친다 — 실제 전투와 같은 길</summary>
        void TickSiege(float dt)
        {
            if (_pendingStrike < 0f) return;
            _pendingStrike -= dt;
            var scarecrow = _scarecrows[_pendingSlot];
            if (_pendingStrike < 0f)
            {
                _pendingStrike = -1f;
                _fx.EndSiegeTelegraph();
                StrikeShop(_pendingSlot, _pendingEnemy, true);
                return;
            }
            float windup = Mathf.Clamp01(1f - _pendingStrike / SiegeWindupTime);
            if (scarecrow?.State != null) scarecrow.State.SiegeWindup = windup;
            _fx.SiegeTelegraph(_pendingGround, _pendingSize, windup);
        }

        void TickScarecrows(float dt)
        {
            for (int i = 0; i < _scarecrows.Length; i++)
            {
                var scarecrow = _scarecrows[i];
                if (scarecrow == null || scarecrow.View == null || !scarecrow.View.gameObject.activeSelf) continue;
                var state = scarecrow.State;
                state.AttackAnim = Mathf.Max(0f, state.AttackAnim - dt * 4f);
                if (scarecrow.Exiting)
                {
                    if (scarecrow.View.TickExit(dt)) continue;
                    scarecrow.Exiting = false;
                    scarecrow.View.Release();
                    scarecrow.Respawn = 0.25f;
                    continue;
                }
                if (scarecrow.Respawn > 0f)
                {
                    if ((scarecrow.Respawn -= dt) <= 0f) Raise(i, state.Def != null ? state.Def.id : "enemy_dust", true);
                    continue;
                }
                scarecrow.View.Tick(_mapper, dt);
            }
        }

        // ───────────── 허수아비 ─────────────

        /// <summary>slot 방향에 허수아비 적을 (다시) 세운다. spawnFx면 등장 연출과 함께</summary>
        void Raise(int slot, string enemyId, bool spawnFx)
        {
            RaiseCount++;
            var def = EnemyDef(enemyId);
            bool boss = def != null && def.isBoss;
            var scarecrow = _scarecrows[slot];
            if (scarecrow == null)
            {
                scarecrow = _scarecrows[slot] = new Scarecrow { Slot = slot };
                scarecrow.View = _enemies.CreateLoose(_stage);
            }
            float radius = boss ? ScarecrowRadius + 0.25f : ScarecrowRadius;
            scarecrow.State = new EnemyState
            {
                Id = IdBase + slot,
                Def = def,
                Position = BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot), radius),
                Hp = 100f,
                MaxHp = 100f,
                IsBoss = boss,
                Alive = true,
            };
            scarecrow.Exiting = false;
            scarecrow.Respawn = 0f;
            scarecrow.View.Bind(scarecrow.State, _mapper);
            if (spawnFx) _fx.EnemySpawn(scarecrow.View.Rect.anchoredPosition, scarecrow.View.Family, boss);
        }

        Scarecrow Target(int slot)
        {
            var scarecrow = _scarecrows[slot];
            if (scarecrow == null || scarecrow.View == null || !scarecrow.View.gameObject.activeSelf || scarecrow.Exiting || scarecrow.Respawn > 0f)
                Raise(slot, "enemy_dust", false);
            return _scarecrows[slot];
        }

        /// <summary>slot에 그 적이 서 있으면 그대로 두고 (다시 나타나는 페이드 없이), 아니면 세운다</summary>
        void Ensure(int slot, string enemyId)
        {
            var scarecrow = _scarecrows[slot];
            bool standing = scarecrow != null && scarecrow.View != null && scarecrow.View.gameObject.activeSelf && !scarecrow.Exiting && scarecrow.Respawn <= 0f &&
                            scarecrow.State != null && scarecrow.State.Alive &&
                            (scarecrow.State.Def != null ? scarecrow.State.Def.id == enemyId : enemyId == "enemy_dust");
            if (!standing) Raise(slot, enemyId, false);
        }

        /// <summary>5방위 허수아비를 숨긴다 (무리 장면) — 다시 쓸 때 Ensure/Target이 세운다</summary>
        void SetSlotsVisible(bool visible)
        {
            if (visible) return;
            for (int i = 0; i < _scarecrows.Length; i++)
            {
                var scarecrow = _scarecrows[i];
                if (scarecrow?.View == null || !scarecrow.View.gameObject.activeSelf) continue;
                scarecrow.Exiting = false;
                scarecrow.Respawn = 0f;
                scarecrow.View.Release();
            }
        }

        BattleConfig ActiveConfig => Game.IsReady && Game.Battle != null ? Game.Battle.Config : ConfigOverride;

        EnemyDef EnemyDef(string id)
        {
            var config = ActiveConfig;
            return config != null ? config.GetEnemy(id) : null;
        }

        // ───────────── 허수아비 무리 (붐비는 전장 · 연속 처치 · 한꺼번에) ─────────────

        /// <summary>무리 count마리를 세 겹 고리에 세운다 (이미 서 있으면 그대로)</summary>
        void EnsureCrowd(int count)
        {
            count = Mathf.Clamp(count, 0, CrowdMax);
            for (int i = 0; i < count; i++)
            {
                var member = _crowd[i];
                bool standing = member != null && member.View != null && member.View.gameObject.activeSelf && !member.Exiting && member.Respawn <= 0f;
                if (!standing) RaiseCrowd(i, count, false);
            }
            for (int i = count; i < CrowdMax; i++) HideCrowdMember(i);
            _crowdCount = count;
        }

        void RaiseCrowd(int index, int count, bool spawnFx)
        {
            RaiseCount++;
            var member = _crowd[index];
            if (member == null)
            {
                member = _crowd[index] = new Scarecrow { Slot = index };
                member.View = _enemies.CreateLoose(_stage);
            }
            string enemyId = index % 4 == 1 ? "enemy_moth" : index % 4 == 2 ? "enemy_rust" : index % 4 == 3 ? "enemy_wisp" : "enemy_dust";
            member.State = new EnemyState
            {
                Id = CrowdIdBase + index,
                Def = EnemyDef(enemyId),
                Position = CrowdPosition(index, Mathf.Max(1, count)),
                Hp = 100f,
                MaxHp = 100f,
                Alive = true,
            };
            member.Exiting = false;
            member.Respawn = 0f;
            member.View.Bind(member.State, _mapper);
            if (spawnFx) _fx.EnemySpawn(member.View.Rect.anchoredPosition, member.View.Family, false);
        }

        /// <summary>무리 자리: 반지름 1.45 / 1.83 / 2.21 세 겹, 고르게 돌려 놓고 전장 위아래 안쪽으로 (월드)</summary>
        static Vector2 CrowdPosition(int index, int count)
        {
            int ring = index % 3;
            float radius = 1.45f + 0.38f * ring;
            float angle = index / (float)count * Mathf.PI * 2f + ring * 0.35f + 0.2f;
            var position = BattleGeometry.FromAngle(angle, radius);
            position.y = Mathf.Clamp(position.y, -1.45f, 2.1f);
            return position;
        }

        void HideCrowdMember(int index)
        {
            var member = _crowd[index];
            if (member?.View == null || !member.View.gameObject.activeSelf) return;
            member.Exiting = false;
            member.Respawn = 0f;
            member.View.Release();
        }

        void HideCrowd()
        {
            for (int i = 0; i < CrowdMax; i++) HideCrowdMember(i);
            _crowdCount = 0;
        }

        void TickCrowd(float dt)
        {
            for (int i = 0; i < _crowdCount; i++)
            {
                var member = _crowd[i];
                if (member == null || member.View == null) continue;
                if (member.Exiting)
                {
                    if (member.View.TickExit(dt)) continue;
                    member.Exiting = false;
                    member.View.Release();
                    member.Respawn = 0.25f;
                    continue;
                }
                if (member.Respawn > 0f)
                {
                    if ((member.Respawn -= dt) <= 0f) RaiseCrowd(i, _crowdCount, true);
                    continue;
                }
                if (!member.View.gameObject.activeSelf) continue;
                member.State.AttackAnim = Mathf.Max(0f, member.State.AttackAnim - dt * 4f);
                member.View.Tick(_mapper, dt);
            }
        }

        /// <summary>무리 index번이 서 있으면 그 적 (아니면 null)</summary>
        EnemyView CrowdTarget(int index)
        {
            if (_crowdCount <= 0) return null;
            var member = _crowd[((index % _crowdCount) + _crowdCount) % _crowdCount];
            if (member == null || member.View == null || member.Exiting || member.Respawn > 0f || !member.View.gameObject.activeSelf) return null;
            return member.View;
        }

        // ───────────── 대역 (배치 캡처 — Game 없음) ─────────────

        /// <summary>Game이 없는 에디터 배치 캡처인지 (재생 모드에서는 BattleView가 실제 지킴이를 묶고 돌린다)</summary>
        static bool Standalone => !Game.IsReady;

        bool UseStandIns => StandIns && Standalone && CharactersOverride != null && _guardians != null && _guardians.Length > 0;

        /// <summary>
        /// 이 항목의 대역을 맞춘다: 다섯 받침대는 기본 줄(계보 순서 0~4, 기본형)이고, 쏘는 칸만 그 항목의 계보·단계·성향.
        /// 잔향이 공격 · 잔향이 피격 항목이면 그 방위에 대역 잔향이 하나 (나타나는 0.45초를 미리 돌려 다 나타난 몸으로)
        /// </summary>
        void SyncStandIns(in Entry entry, int slot)
        {
            if (!Standalone) return;
            if (!UseStandIns)
            {
                ReleaseStandIns();
                return;
            }
            for (int i = 0; i < _guardians.Length && i < BattleGeometry.SlotCount; i++)
            {
                StandInRole(entry, slot, i, out string lineage, out int tier, out CombatStyle style, out bool forced);
                BindGuardianStandIn(i, lineage, tier, style, forced);
            }
            _standInsLive = true;
            PlaceEchoStandIn(entry, slot);
        }

        /// <summary>칸 i의 대역 (기본 줄: 계보 i · 기본형 · 그 형태의 성향 / 쏘는 칸: 항목의 계보 · 단계 · 성향)</summary>
        static void StandInRole(in Entry entry, int slot, int i, out string lineage, out int tier, out CombatStyle style, out bool forced)
        {
            lineage = Lineages[i % Lineages.Length];
            tier = 0;
            style = CombatStyle.Defense;
            forced = false;
            if (i != slot) return;
            switch (entry.Kind)
            {
                case Kind.GuardianShot:
                    (lineage, tier, style, forced) = (entry.Id, entry.Tier, entry.Style, true);
                    break;
                case Kind.Death:
                    (lineage, style, forced) = ("jjakjjak", CombatStyle.Defense, true);
                    break;
                case Kind.ShieldHit:
                    (lineage, style, forced) = ("ttokdan", CombatStyle.Defense, true);
                    break;
                case Kind.ShieldBreak:
                    (lineage, style, forced) = ("chalkak", CombatStyle.Snipe, true);
                    break;
                case Kind.Crit:
                case Kind.CritKill:
                    (lineage, tier, style, forced) = (entry.Id, 2, CombatStyle.Assault, true);
                    break;
                case Kind.LightHole:
                    (lineage, style, forced) = (entry.Id, CombatStyle.Defense, true);
                    break;
                case Kind.SupportPulse:
                    (lineage, style, forced) = (entry.Id, CombatStyle.Support, true);
                    break;
                case Kind.HealPulse:
                    (lineage, style, forced) = ("mongdang", CombatStyle.Heal, true);
                    break;
            }
        }

        /// <summary>계보의 형태 중 단계 ≤ tier에서 가장 높은 단계 (같으면 성향이 맞는 쪽)</summary>
        CharacterFormDef StandInForm(string lineageId, int tier, CombatStyle style)
        {
            var lineage = CharactersOverride != null ? CharactersOverride.GetLineage(lineageId) : null;
            if (lineage == null || lineage.forms == null) return null;
            CharacterFormDef best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < lineage.forms.Count; i++)
            {
                var form = lineage.forms[i];
                if (form == null || form.tier > tier) continue;
                int score = form.tier * 10 + (form.style == style ? 1 : 0);
                if (score <= bestScore) continue;
                bestScore = score;
                best = form;
            }
            return best;
        }

        /// <summary>칸 i에 대역 지킴이를 묶는다 (같은 형태·성향이 이미 묶여 있으면 그대로 — 쏘는 몸짓이 끊기지 않게)</summary>
        void BindGuardianStandIn(int i, string lineage, int tier, CombatStyle style, bool forcedStyle)
        {
            var view = _guardians[i];
            if (view == null) return;
            var form = StandInForm(lineage, tier, style);
            if (form == null) return;
            var useStyle = forcedStyle ? style : form.style;
            var state = _standInGuardians[i] ??= new GuardianState();
            if (view.State == state && state.FormId == form.id && state.Stats.style == useStyle) return;
            state.Slot = i;
            state.FormId = form.id;
            state.LineageId = lineage;
            state.Stats = new CharacterStats
            {
                style = useStyle, attack = form.attack, hp = form.hp, attackInterval = form.attackInterval, range = form.range, arcDegrees = form.arcDegrees,
            };
            state.AttackAnim = 0f;
            view.Bind(state);
            StandInBound?.Invoke(view, form.id);
        }

        /// <summary>
        /// 대역 잔향이 자리 (월드 반지름, 방위 각에서 옆으로 비킨 라디안): 같은 방위의 지킴이(반지름 1)와 겹쳐 보이지 않게 옆으로 비켜 선다 —
        /// 근접 1.45 · +0.22 (허수아비와 0.46) / 원거리 1.2 · −0.40 / 적에게 맞는 자리 1.4 · +0.30. 대역이 없을 때(기준선 프레임)는 예전처럼 방위 선 위
        /// </summary>
        const float EchoMeleeRadius = 1.45f;
        const float EchoMeleeSide = 0.22f;
        const float EchoRangedRadius = 1.2f;
        const float EchoRangedSide = -0.40f;
        const float EchoStruckRadius = 1.4f;
        const float EchoStruckSide = 0.30f;

        /// <summary>
        /// 대역 잔향이: 잔향이 공격이면 그 공격이 나가는 자리(근접 / 원거리), 잔향이 피격이면 적이 덮치는 자리에, 허수아비를 바라보게
        /// (지킴이와 겹치지 않게 방위 선에서 옆으로 비켜 — 위 상수). 다른 항목이면 치운다 (풀에서 바로 돌려받는다 — 빛으로 풀리는 연출 없이)
        /// </summary>
        void PlaceEchoStandIn(in Entry entry, int slot)
        {
            bool want = entry.Kind == Kind.EchoAttack || entry.Kind == Kind.StrikeEcho;
            if (!want || !EnsureEchoPool())
            {
                ClearEchoStandIn();
                return;
            }
            string lineage = entry.Kind == Kind.EchoAttack ? entry.Id : "jjakjjak";
            var style = entry.Kind == Kind.EchoAttack ? entry.Style : CombatStyle.Assault;
            var form = StandInForm(lineage, 0, style);
            if (form == null)
            {
                ClearEchoStandIn();
                return;
            }
            float angle = BattleGeometry.SlotAngle(slot);
            bool ranged = AttackFxCatalog.Style(style).Ranged;
            float radius = entry.Kind != Kind.EchoAttack ? EchoStruckRadius : ranged ? EchoRangedRadius : EchoMeleeRadius;
            float side = entry.Kind != Kind.EchoAttack ? EchoStruckSide : ranged ? EchoRangedSide : EchoMeleeSide;
            var world = BattleGeometry.FromAngle(angle + side, radius);
            var toward = BattleGeometry.FromAngle(angle, ScarecrowRadius) - world;
            // 지난 대역은 풀로 바로 돌려받는다 (ID가 같아도 형태가 바뀌면 다시 묶는다)
            _echoPool.ClearAll();
            _standInEchoes.Clear();
            var state = _standInEcho ??= new EchoState();
            state.Id = EchoStandInId;
            state.DeckSlot = 0;
            state.FormId = form.id;
            state.LineageId = lineage;
            state.Stats = new CharacterStats { style = style, attack = form.attack, hp = form.hp, attackInterval = form.attackInterval, range = form.range };
            state.Position = world;
            state.Heading = Mathf.Atan2(toward.y, toward.x);
            state.Hp = state.MaxHp = 100f;
            state.Life = state.MaxLife = 60f;
            state.Alive = true;
            state.IsToken = false;
            state.AttackAnim = 0f;
            state.Shield = 0f;
            _standInEchoes.Add(state);
            // 묶고 (0초) → 나타나는 0.45초를 한 번에 돌린다 (캡처가 반쯤 나타난 몸을 찍지 않게)
            _echoPool.Sync(_standInEchoes, _mapper, 0f);
            _echoPool.Sync(_standInEchoes, _mapper, 0.46f);
            var view = EchoStandIn;
            if (view != null) StandInBound?.Invoke(view, form.id);
        }

        /// <summary>지금 서 있는 대역 잔향이 (없으면 null)</summary>
        EchoView EchoStandIn => _standInEchoes.Count > 0 && _echoPool != null && _echoPool.TryGet(EchoStandInId, out var view) ? view : null;

        bool EnsureEchoPool()
        {
            if (_echoPool == null) _echoPool = FindAnyObjectByType<EchoViewPool>(FindObjectsInactive.Include);
            return _echoPool != null;
        }

        void ClearEchoStandIn()
        {
            if (_standInEchoes.Count == 0) return;
            _standInEchoes.Clear();
            if (_echoPool != null) _echoPool.ClearAll();
        }

        /// <summary>
        /// 대역을 모두 치운다 (받침대는 빈칸으로). 몸짓·받침대 번쩍임을 먼저 가라앉히고(1초 진행) 풀어 준 뒤 StandInBound(칸, null)로 알린다 —
        /// 에디터 캡처가 기준선 프레임과 같은 모습(만든 그대로의 받침대)으로 되돌린다
        /// </summary>
        void ReleaseStandIns()
        {
            ClearEchoStandIn();
            if (!_standInsLive) return;
            _standInsLive = false;
            for (int i = 0; i < _guardians.Length && i < _standInGuardians.Length; i++)
            {
                var view = _guardians[i];
                if (view == null || view.State == null || view.State != _standInGuardians[i]) continue;
                view.Tick(1f, 0f);
                view.Bind(null);
                StandInBound?.Invoke(view, null);
            }
        }

        /// <summary>대역 지킴이·잔향이를 연출 시간 dt만큼 (실제 전투의 BattleView처럼 — 몸짓 · 숨쉬기 · 후광, 잔향이 둥실거림 · 빛 부스러기)</summary>
        void TickStandIns(float dt)
        {
            if (_standInsLive)
            {
                for (int i = 0; i < _guardians.Length; i++)
                {
                    var view = _guardians[i];
                    if (view != null && view.State != null) view.Tick(dt, 0f);
                }
            }
            if (_standInEchoes.Count > 0 && _echoPool != null) _echoPool.Sync(_standInEchoes, _mapper, dt);
        }

        /// <summary>쏜 지킴이 축하 (BattleView.CelebrateGuardian과 같다): delay 뒤 통 뛰고 머리 위 반짝임, 지킴이마다 2.5초에 한 번</summary>
        void CelebrateShooter(GuardianSlotView guardian, float delay)
        {
            if (guardian == null || guardian.State == null) return;
            if (guardian.Celebrate(delay)) _fx.CelebrationGlint(guardian.HeadPoint, delay);
        }

        /// <summary>slot의 지킴이 (없으면 null)</summary>
        GuardianSlotView Guardian(int slot) => _guardians != null && slot >= 0 && slot < _guardians.Length ? _guardians[slot] : null;

        // ───────────── 대본 ─────────────

        void StartScenario(Scenario scenario, float length)
        {
            _scenario = scenario;
            _scenarioTime = 0f;
            _scenarioLength = ScenarioLengthOverride > 0f ? ScenarioLengthOverride : length;
            _scenarioStep = 0;
            _busyShots = 0;
            _busySkill = false;
            _busyCrit = false;
            _massDone = false;
            for (int i = 0; i < _attackerTimer.Length; i++) _attackerTimer[i] = i * 0.05f;
        }

        void StopScenario()
        {
            bool fast = _scenario == Scenario.Busy || _scenario == Scenario.MassKill || _scenario == Scenario.LampFull;
            if (fast && _fx != null) _fx.SetBattlePace(1, StageModifierType.None);
            _scenario = Scenario.None;
        }

        void TickScenario(float dt)
        {
            if (_scenario == Scenario.None) return;
            _scenarioTime += dt;
            if (_scenarioTime > _scenarioLength)
            {
                StopScenario();
                return;
            }
            switch (_scenario)
            {
                case Scenario.Busy:
                    TickBusy(dt);
                    break;
                case Scenario.Streak:
                    // 0.1초마다 하나씩 10마리
                    while (_scenarioStep < 10 && _scenarioTime >= _scenarioStep * 0.1f) KillCrowd(_scenarioStep++);
                    break;
                case Scenario.Multi:
                    // 0.04초마다 하나씩 5마리 (0.2초 안)
                    while (_scenarioStep < 5 && _scenarioTime >= _scenarioStep * 0.04f) KillCrowd(_scenarioStep++);
                    break;
                case Scenario.MassKill:
                case Scenario.LampFull:
                    TickBusyAttackers(dt);
                    if (!_massDone && _scenarioTime >= MassLead)
                    {
                        _massDone = true;
                        if (_scenario == Scenario.MassKill) MassKillCast();
                        else LampSweep();
                    }
                    break;
            }
        }

        /// <summary>붐비는 전장: 지킴이 5 + 잔향이 5가 2배속으로 무리를 때리고, 0.6초에 범위 스킬(별 폭발), 0.9초에 치명타</summary>
        void TickBusy(float dt)
        {
            TickBusyAttackers(dt);
            if (!_busySkill && _scenarioTime >= BusySkillAt)
            {
                _busySkill = true;
                BusyAreaSkill();
            }
            if (!_busyCrit && _scenarioTime >= BusyCritAt)
            {
                _busyCrit = true;
                var target = CrowdTarget(0);
                var shooter = Guardian(0);
                if (target != null) CritOn(target, shooter != null ? shooter.MuzzlePosition : _mapper.Origin, "jjakjjak", shooter);
            }
        }

        /// <summary>지킴이 5 + 잔향이 5가 2배속으로 서 있는 무리를 0.5초마다 때린다 (퇴장 중인 적은 건너뛴다)</summary>
        void TickBusyAttackers(float dt)
        {
            _fx.SetBattlePace(2, StageModifierType.None);
            for (int a = 0; a < BusyAttackers; a++)
            {
                _attackerTimer[a] -= dt;
                if (_attackerTimer[a] > 0f) continue;
                _attackerTimer[a] += BusyInterval;
                var target = CrowdTarget(a * 7 + _busyShots);
                _busyShots++;
                if (target == null) continue;
                string lineage = Lineages[a % Lineages.Length];
                if (a < BattleGeometry.SlotCount && _guardians != null && a < _guardians.Length)
                {
                    var style = BusyStyles[(a + _busyShots) % BusyStyles.Length];
                    ShootAt(target, _guardians[a].MuzzlePosition, lineage, style, 0, HitTier.Normal, 96f, _guardians[a]);
                }
                else
                {
                    var from = Vector2.Lerp(target.HitPoint, _shop.FieldPosition, 0.22f);
                    EchoAt(target, from, lineage, (a + _busyShots) % 2 == 0 ? CombatStyle.Assault : CombatStyle.Snipe, 48f);
                }
            }
        }

        /// <summary>한꺼번에 처치 · 불 밝히기 전멸의 무리 가운데 (전장 로컬)</summary>
        Vector2 CrowdCenter()
        {
            var sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < _crowdCount; i++)
            {
                var view = CrowdTarget(i);
                if (view == null) continue;
                sum += view.HitPoint;
                count++;
            }
            return count > 0 ? sum / count : _mapper.WorldToField(new Vector2(0f, 1.2f));
        }

        /// <summary>한꺼번에 처치 범위 스킬이 이 적에 닿기까지 (가운데에서 100px마다 25ms — 실제 범위 스킬과 같은 물결)</summary>
        static float MassHitDelay(Vector2 offsetFromCenter) => 0.025f * offsetFromCenter.magnitude / 100f;

        /// <summary>
        /// 예산 압박 장면의 핵심 순간 (재생 뒤 연출 시간): 한꺼번에 처치는 가장 먼저 터지는 처치 꽃 (스킬이 닿는 순간 + 하얀 버팀),
        /// 불 밝히기 전멸은 빛 물결이 가장 가까운 적에 닿는 순간. 무리는 그 자리에 서 있으므로 지금 자리로 미리 잰다
        /// </summary>
        float MassArrival(Kind kind)
        {
            var center = CrowdCenter();
            float first = float.MaxValue;
            for (int i = 0; i < _crowdCount; i++)
            {
                var view = CrowdTarget(i);
                if (view == null) continue;
                float at;
                if (kind == Kind.LampFull) at = _fx.LampWaveDelay(view.Rect.anchoredPosition);
                else
                {
                    float delay = MassHitDelay(view.Rect.anchoredPosition - center);
                    at = EnemyFxCatalog.KillBlowHold(delay, delay, view.IsBoss);
                }
                first = Mathf.Min(first, at);
            }
            return MassLead + (first < float.MaxValue ? first : 0.2f);
        }

        /// <summary>
        /// 한꺼번에 처치 (실제 전투의 시전 맥락과 같은 순서): 범위 스킬(별 폭발)이 무리 전체를 덮고 가운데에서 바깥으로 닿는다 —
        /// 적마다 스킬 명중(코어 이음매 SkillHit) + 반응 + 숫자, 3마리에 하나는 치명타(치명타 겹침 · 킥 · 금빛 숫자), 모두 처치 (보류 = 스킬이 닿는 순간,
        /// 처치 등급 = 치명타 / 스킬 → 킥 · 인쇄 어긋남). 퇴장한 무리는 0.25초 뒤 다시 선다
        /// </summary>
        void MassKillCast()
        {
            string key = SkillVfx.StarBurst;
            var look = SkillVfxLook.Get(key);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(key, out sprite, out isArt);
            var center = CrowdCenter();
            float radiusPx = _mapper.Ppu * MassRadius;
            var caster = Guardian(2);
            var from = caster != null ? caster.MuzzlePosition : _mapper.Origin;
            if (caster != null) caster.PlayCast();
            _fx.SkillCast(new SkillFxRequest
            {
                Key = key,
                Look = look,
                Sprite = sprite,
                IsArt = isArt,
                From = from,
                To = center + Vector2.up * 40f,
                Center = center,
                Radius = new Vector2(radiusPx, radiusPx * _mapper.Squash),
                Power = 1.6f,
            });
            _fx.AddSkillTrauma(HitFeel.AreaSkillTrauma, Vector2.zero);
            for (int i = 0; i < _crowdCount; i++)
            {
                var member = _crowd[i];
                var target = CrowdTarget(i);
                if (target == null) continue;
                var offset = target.Rect.anchoredPosition - center;
                float delay = MassHitDelay(offset);
                var dir = offset.sqrMagnitude > 1f ? offset.normalized : Vector2.up;
                bool crit = i % MassCritEvery == 0;
                _fx.SkillHit(new SkillHitFx
                {
                    Point = target.HitPoint, Target = target, Main = look.Main, Accent = look.Accent, Style = look.Style, Shard = look.Particle, Direction = dir,
                    Delay = delay, Boss = target.IsBoss,
                });
                int number;
                if (crit)
                {
                    _fx.CritImpact(target, delay);
                    target.QueueImpact(delay, HitTier.Crit, dir, look.Accent);
                    _fx.Kick(dir, target.IsBoss ? HitFeel.KickBossCrit : HitFeel.KickCrit, KickChannel.Crit, delay);
                    number = _fx.CritNumber(target.Id, target.HitPoint + new Vector2(0f, 40f), 4096f * DamageScale, delay);
                }
                else
                {
                    target.QueueImpact(delay, HitTier.Skill, dir, look.Accent);
                    number = _fx.DamageNumber(target.Id, target.HitPoint + new Vector2(0f, 34f), 2222f * DamageScale, DamageNumberKind.Skill, delay, 1f, look.Accent);
                }
                _fx.AnchorNumber(number, target);
                member.State.Hp = 0f;
                KillView(target, delay, crit ? HitTier.Crit : HitTier.Skill, true);
                member.Exiting = true;
            }
        }

        /// <summary>
        /// 불 밝히기 전멸 (BattleView.OnLampLit · 불빛 피해 · 처치와 같은 순서): 점화 → 빛 물결이 적마다 닿는 순간 명중(LampHit) + 반응 + 불빛 숫자 →
        /// 모두 처치 (보류 = 물결이 닿는 순간 + 0.02, 처치 등급 = 불빛)
        /// </summary>
        void LampSweep()
        {
            _fx.LampBurst(_shop.DoorPosition);
            _fx.GlobalHitStop(0f, LampWave.IgniteScale, LampWave.IgniteTime);
            _shop.LampBurst();
            for (int i = 0; i < _crowdCount; i++)
            {
                var member = _crowd[i];
                var view = CrowdTarget(i);
                if (view == null) continue;
                float delay = _fx.LampWaveDelay(view.Rect.anchoredPosition);
                _fx.LampHit(view.HitPoint, delay, view);
                view.QueueImpact(delay, HitTier.Lamp, Direction(_shop.FieldPosition, view.HitPoint), UITheme.Lamp);
                int number = _fx.DamageNumber(view.Id, view.HitPoint + new Vector2(0f, 34f), 2400f * DamageScale, DamageNumberKind.Lamp, delay);
                _fx.AnchorNumber(number, view);
                member.State.Hp = 0f;
                KillView(view, delay + 0.02f, HitTier.Lamp, true);
                member.Exiting = true;
            }
        }

        /// <summary>
        /// 범위 스킬 (별 폭발): 무리 위쪽 가운데에서 터지고, 가운데에서 100px마다 25ms씩 늦게 닿는다 (적마다 스킬 명중 + 스킬 숫자 + 반응)
        /// </summary>
        void BusyAreaSkill()
        {
            string key = SkillVfx.StarBurst;
            var look = SkillVfxLook.Get(key);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(key, out sprite, out isArt);
            var center = _mapper.WorldToField(new Vector2(0f, 1.2f));
            float ppu = _mapper.Ppu;
            float radiusPx = ppu * 1.4f;
            var caster = Guardian(2);
            var from = caster != null ? caster.MuzzlePosition : _mapper.Origin;
            if (caster != null) caster.PlayCast();
            _fx.SkillCast(new SkillFxRequest
            {
                Key = key,
                Look = look,
                Sprite = sprite,
                IsArt = isArt,
                From = from,
                To = center + Vector2.up * 40f,
                Center = center,
                Radius = new Vector2(radiusPx, radiusPx * _mapper.Squash),
                Power = 1.3f,
            });
            _fx.AddSkillTrauma(HitFeel.AreaSkillTrauma, Vector2.zero);
            for (int i = 0; i < _crowdCount; i++)
            {
                var target = CrowdTarget(i);
                if (target == null) continue;
                var offset = target.Rect.anchoredPosition - center;
                if (offset.magnitude > radiusPx * 1.3f) continue;
                float delay = 0.025f * offset.magnitude / 100f;
                var dir = offset.sqrMagnitude > 1f ? offset.normalized : Vector2.up;
                target.QueueImpact(delay, HitTier.Skill, dir, look.Accent);
                _fx.SkillHit(new SkillHitFx
                {
                    Point = target.HitPoint, Target = target, Main = look.Main, Accent = look.Accent, Style = look.Style, Shard = look.Particle, Direction = dir,
                    Delay = delay, Boss = target.IsBoss,
                });
                int number = _fx.DamageNumber(target.Id, target.HitPoint + new Vector2(0f, 34f), 222f * DamageScale, DamageNumberKind.Skill, delay, 1f, look.Accent);
                _fx.AnchorNumber(number, target);
            }
        }

        /// <summary>무리 index번을 처치한다 (지킴이 일격이 닿을 때까지 버티다 처치 꽃 → 연속 처치를 센다)</summary>
        void KillCrowd(int index)
        {
            if (_crowdCount <= 0) return;
            var member = _crowd[index % _crowdCount];
            var view = CrowdTarget(index);
            if (view == null) return;
            int slot = index % BattleGeometry.SlotCount;
            var shooter = Guardian(slot);
            var from = shooter != null ? shooter.MuzzlePosition : _mapper.Origin;
            float flight = ShootAt(view, from, Lineages[index % Lineages.Length], CombatStyle.Defense, 0, HitTier.Normal, 128f, shooter);
            member.State.Hp = 0f;
            KillView(view, flight, HitTier.Normal);
            member.Exiting = true;
            // 기본 공격이 처치했다: 꽃이 터지는 순간 쏜 지킴이가 통 뛴다 (지킴이마다 2.5초에 한 번)
            CelebrateShooter(shooter, view.KillDelay);
        }

        // ───────────── 재생 ─────────────

        /// <summary>항목 하나 재생. 핵심 순간까지의 연출 시간을 돌려주고 볼 자리(_focus)를 남긴다</summary>
        float Play(Entry entry)
        {
            int slot = _cursor % BattleGeometry.SlotCount;
            if (entry.Kind != Kind.LightHole) RestoreAmbience();
            ApplyVariant(entry);
            if (entry.Variant == Variant.Top && entry.Kind == Kind.Death)
            {
                Ensure(slot, entry.Id);
                MoveToTop(slot);
            }
            switch (entry.Kind)
            {
                case Kind.GuardianShot:
                    return GuardianShot(slot, entry.Id, entry.Style, entry.Tier, HitTier.Normal);
                case Kind.EchoAttack:
                    return EchoAttack(slot, entry.Id, entry.Style);
                case Kind.StrikeEcho:
                    return StrikeEcho(slot, entry.Id);
                case Kind.StrikeShop:
                    return StrikeShop(slot, entry.Id, false);
                case Kind.Siege:
                    return Siege(slot, entry.Id);
                case Kind.Death:
                    return Death(slot, entry.Id);
                case Kind.Spawn:
                    Raise(slot, entry.Id, true);
                    _focus = _scarecrows[slot].View.Rect.anchoredPosition;
                    return 0.1f;
                case Kind.BossSpawn:
                    Raise(0, entry.Id, true);
                    _focus = _scarecrows[0].View.Rect.anchoredPosition;
                    return 0.15f;
                case Kind.Minion:
                    Ensure(0, entry.Id);
                    _focus = _scarecrows[0].View.Rect.anchoredPosition;
                    _fx.MinionSummon(_focus, EnemyFxCatalog.Family(entry.Id));
                    return 0.15f;
                case Kind.ShieldHit:
                    return ShieldHit(slot);
                case Kind.ShieldBreak:
                    return ShieldBreak(slot);
                case Kind.Crit:
                    return Crit(slot, entry.Id);
                case Kind.SupportPulse:
                    return SupportPulse(slot, entry.Id);
                case Kind.HealPulse:
                    return HealPulse(slot);
                case Kind.LightHole:
                    return LightHole(slot, entry.Id);
                case Kind.Lamp:
                    return Lamp();
                case Kind.Skill:
                    return Skill(slot, entry.Id);
                case Kind.CritKill:
                    return CritKill(slot, entry.Id);
                case Kind.KillStreak:
                    if (_crowdCount < 10) EnsureCrowd(10);
                    StartScenario(Scenario.Streak, 2.2f);
                    _focus = _mapper.WorldToField(new Vector2(0f, 0.3f));
                    // 5번째 처치(0.4초 + 처치 보류)가 터지는 무렵 — 알림이 뜨는 순간
                    return 0.55f;
                case Kind.MultiKill:
                    if (_crowdCount < 5) EnsureCrowd(8);
                    StartScenario(Scenario.Multi, 1.6f);
                    _focus = MultiFocus();
                    return 0.3f;
                case Kind.StageClear:
                    return StageClear();
                case Kind.Busy:
                    if (_crowdCount < CrowdMax) EnsureCrowd(CrowdMax);
                    StartScenario(Scenario.Busy, BusyLength);
                    _focus = _mapper.WorldToField(new Vector2(0f, 1.2f));
                    return BusySkillAt;
                case Kind.Numbers:
                    return NumbersAll();
                case Kind.MassKill:
                case Kind.LampFull:
                    if (_crowdCount < CrowdMax) EnsureCrowd(CrowdMax);
                    StartScenario(entry.Kind == Kind.MassKill ? Scenario.MassKill : Scenario.LampFull, MassLength);
                    _focus = CrowdCenter();
                    return MassArrival(entry.Kind);
            }
            return 0f;
        }

        Vector2 MultiFocus()
        {
            var sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < 5 && i < _crowdCount; i++)
            {
                var view = CrowdTarget(i);
                if (view == null) continue;
                sum += view.HitPoint;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        float GuardianShot(int slot, string lineageId, CombatStyle style, int tier, HitTier forced)
        {
            var target = Target(slot).View;
            var shooter = Guardian(slot);
            var from = shooter != null ? shooter.MuzzlePosition : _mapper.Origin;
            float flight = ShootAt(target, from, lineageId, style, tier, forced, 128f, shooter);
            _focus = target.HitPoint;
            return flight;
        }

        /// <summary>
        /// 지킴이 기본 공격 한 발 (실제 전투와 같은 순서: 투사체 → 쏜 지킴이 몸짓(웅크림 W → 놓음 → 반동) → 도착 예약 → 반응 → 숫자). 비행 시간을 돌려준다.
        /// shooter = 쏜 받침대 (BattleView.OnGuardianAttack처럼 PlayAttack(대상, LastWindup) — 묶인 지킴이만 움직인다)
        /// </summary>
        float ShootAt(EnemyView target, Vector2 from, string lineageId, CombatStyle style, int tier, HitTier forced, float damage,
            GuardianSlotView shooter = null)
        {
            float flight = _fx.GuardianShot(new AttackFxRequest
            {
                From = from,
                Target = target,
                LineageId = lineageId,
                Style = style,
                FormTier = tier,
                Mythic = lineageId == "lalabell",
            });
            // 웅크림 W 동안 몸을 낮췄다가 총구 불꽃과 같은 순간에 놓는다 (도착 시각은 그대로 flight)
            if (shooter != null) shooter.PlayAttack(target.HitPoint, _fx.LastWindup);
            var mod = AttackFxCatalog.Style(style);
            var tierHit = forced != HitTier.Normal ? forced : mod.Tier;
            target.ExpectImpact(flight);
            target.QueueImpact(flight, tierHit, Direction(from, target.HitPoint), AttackFxCatalog.Look(lineageId).Accent);
            if (forced != HitTier.Crit)
            {
                int number = _fx.DamageNumber(target.Id, target.HitPoint + new Vector2(0f, 34f), damage * DamageScale,
                    tierHit == HitTier.Heavy ? DamageNumberKind.Heavy : DamageNumberKind.Normal, flight);
                _fx.AnchorNumber(number, target);
            }
            return flight;
        }

        float EchoAttack(int slot, string lineageId, CombatStyle style)
        {
            var target = Target(slot).View;
            bool ranged = AttackFxCatalog.Style(style).Ranged;
            // 대역 잔향이가 서 있으면 그 몸 가운데에서 (BattleView.OnEchoAttack처럼 몸이 먼저 출렁인다), 아니면 같은 자리 계산
            var echo = EchoStandIn;
            Vector2 from;
            if (echo != null)
            {
                echo.PlayAttack(!ranged);
                from = echo.BodyPoint;
            }
            else
                from = _mapper.WorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot), ranged ? 1.05f : 1.45f)) + Vector2.up * (50f * _mapper.ActorScale);
            float delay = EchoAt(target, from, lineageId, style, 64f);
            _focus = target.HitPoint;
            return delay;
        }

        /// <summary>
        /// 잔향이 기본 공격 한 번 (근접 베기 / 작은 투사체). 닿기까지 시간을 돌려준다.
        /// 적 반응은 실제 잔향이 기본 공격과 같은 가벼운 반응 (allowFreeze, echo — 번쩍임 절반 · 넉백 0.6배 · 정지 없음, BattleView.OnEnemyHit EchoBasic)
        /// </summary>
        float EchoAt(EnemyView target, Vector2 from, string lineageId, CombatStyle style, float damage)
        {
            float delay = _fx.EchoAttack(new AttackFxRequest
            {
                From = from,
                Target = target,
                LineageId = lineageId,
                Style = style,
                Echo = true,
                Glow = UITheme.EchoGlow,
            });
            target.ExpectImpact(delay);
            target.QueueImpact(delay, HitTier.Normal, Direction(from, target.HitPoint), AttackFxCatalog.Look(lineageId).Accent, true, true);
            int number = _fx.DamageNumber(target.Id, target.HitPoint + new Vector2(0f, 34f), damage * DamageScale, DamageNumberKind.Normal, delay);
            _fx.AnchorNumber(number, target);
            return delay;
        }

        float StrikeEcho(int slot, string enemyId)
        {
            Ensure(slot, enemyId);
            var scarecrow = _scarecrows[slot];
            var view = scarecrow.View;
            // 대역 잔향이가 서 있으면 그 몸이 맞는다 (BattleView.OnEnemyStrikeEcho처럼 공격 그림이 몸을 따라가고 몸은 움찔 — 흰 번쩍임 50%)
            var echo = EchoStandIn;
            var to = echo != null
                ? echo.BodyPoint
                : _mapper.WorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot), ScarecrowRadius - 0.45f)) + Vector2.up * (50f * _mapper.ActorScale);
            scarecrow.State.AttackAnim = 1f;
            view.Strike(to - view.HitPoint);
            _fx.EnemyStrike(new EnemyStrikeRequest
            {
                EnemyId = enemyId,
                Boss = view.IsBoss,
                From = view.HitPoint,
                To = to,
                Echo = echo,
                Ground = echo != null ? echo.Rect.anchoredPosition.y : to.y - 50f * _mapper.ActorScale,
            });
            if (echo != null) echo.PlayHurt(0.5f);
            _focus = to;
            return 0f;
        }

        float StrikeShop(int slot, string enemyId, bool keep)
        {
            if (!keep) Ensure(slot, enemyId);
            var scarecrow = _scarecrows[slot];
            if (scarecrow == null || scarecrow.View == null) return 0f;
            var view = scarecrow.View;
            float unit = _mapper.ActorScale;
            var edgeWorld = BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot), BattleGeometry.ShopRadius);
            var side = (_mapper.WorldToField(edgeWorld) - _shop.FieldPosition).normalized;
            var edge = _shop.DoorPosition + side * (EnemyFxCatalog.ShopEdgeOffset * unit) + Vector2.up * (30f * unit);
            var look = EnemyFxCatalog.Look(enemyId, view.IsBoss);
            scarecrow.State.AttackAnim = 1f;
            scarecrow.State.SiegeWindup = 0f;
            view.Strike(edge - view.HitPoint);
            _fx.EnemyStrike(new EnemyStrikeRequest
            {
                EnemyId = enemyId,
                Boss = view.IsBoss,
                From = view.HitPoint,
                To = edge,
                Shop = true,
                Ground = _shop.FieldPosition.y + side.y * 24f * unit,
            });
            _fx.ShopImpact(edge, side, look.ShopHitSize, view.IsBoss, false);
            _fx.ShopDamaged(_shop.DoorPosition, look.Boss ? 11f : 5f);
            if (_ring != null) _ring.FlashSlot(slot, view.IsBoss);
            _shop.Hit();
            _focus = edge;
            // 나뭇조각 그림은 0.03초 뒤에 핀다
            return 0.03f;
        }

        float Siege(int slot, string bossId)
        {
            Ensure(slot, bossId);
            var scarecrow = _scarecrows[slot];
            // 예비 동작: 몸을 젖히고 문 앞 붉은 원이 0.35초 동안 차오른 뒤 덮친다 (차오름은 TickSiege가 매 프레임)
            scarecrow.State.SiegeWindup = 0.01f;
            _pendingGround = _mapper.WorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(slot), BattleGeometry.ShopRadius + 0.06f));
            _pendingSize = 170f * _mapper.ActorScale;
            _fx.SiegeTelegraph(_pendingGround, _pendingSize, 0.01f);
            _pendingStrike = SiegeWindupTime;
            _pendingSlot = slot;
            _pendingEnemy = bossId;
            _focus = _pendingGround;
            return SiegeWindupTime;
        }

        float Death(int slot, string enemyId)
        {
            Ensure(slot, enemyId);
            var scarecrow = _scarecrows[slot];
            var view = scarecrow.View;
            float flight = GuardianShot(slot, "jjakjjak", CombatStyle.Defense, 0, HitTier.Normal);
            scarecrow.State.Hp = 0f;
            KillView(view, flight, HitTier.Normal);
            scarecrow.Exiting = true;
            // 기본 공격이 처치했다: 꽃이 터지는 순간 쏜 지킴이가 통 뛴다 (BattleView.OnEnemyKilled → CelebrateGuardian)
            CelebrateShooter(Guardian(slot), view.KillDelay);
            _focus = view.HitPoint;
            return view.KillDelay;
        }

        /// <summary>
        /// 처치 (실제 전투와 같은 순서): 마지막 일격이 닿을 때까지 버티고(처치 보류) → 처치 꽃 (보스면 EnemyDeath → BossKillMoment) → 연속 처치를 센다.
        /// flight = 마지막 일격이 닿기까지 — 투사체면 처치 보류 규칙(KillHold), castHold면 그 시각 그대로 (스킬·불 밝히기 맥락).
        /// 치명타 처치는 꽃이 터지는 순간 몸에 인쇄 어긋남 (BattleView.OnEnemyKilled와 같다)
        /// </summary>
        void KillView(EnemyView view, float flight, HitTier byTier, bool castHold = false)
        {
            float hold = castHold ? Mathf.Max(0f, flight) : AttackFxCatalog.KillHold(flight);
            view.MarkKilled(hold, byTier);
            view.BeginExit(_shop.FieldPosition);
            _fx.EnemyDeath(new EnemyDeathRequest
            {
                EnemyId = view.State != null && view.State.Def != null ? view.State.Def.id : null,
                Point = view.HitPoint,
                Feet = view.Rect.anchoredPosition,
                BodyColor = view.BodyColor,
                Boss = view.IsBoss,
                Delay = view.KillDelay,
                Shop = _shop.DoorPosition,
                Body = view.Body,
                KilledBy = byTier,
            });
            _fx.NoteKill(view.Id, view.HitPoint, view.KillDelay, view.IsBoss, byTier);
            if (!view.IsBoss && byTier == HitTier.Crit) _fx.Misprint(view.Body, view.KillDelay, 3f, 0.05f, 0.4f);
        }

        float ShieldHit(int slot)
        {
            Ensure(slot, "enemy_thimble");
            var scarecrow = _scarecrows[slot];
            scarecrow.State.Shield = scarecrow.State.MaxShield = 30f;
            float flight = GuardianShot(slot, "ttokdan", CombatStyle.Defense, 0, HitTier.Normal);
            _fx.BlockNumber(scarecrow.View.Id, scarecrow.View.HitPoint + new Vector2(0f, 62f), 18f, flight);
            return flight;
        }

        float ShieldBreak(int slot)
        {
            Ensure(slot, "enemy_thimble");
            var scarecrow = _scarecrows[slot];
            var view = scarecrow.View;
            float flight = GuardianShot(slot, "chalkak", CombatStyle.Snipe, 0, HitTier.Heavy);
            scarecrow.State.Shield = 0f;
            scarecrow.State.MaxShield = 30f;
            view.ShieldBreak(flight);
            view.QueueImpact(flight, HitTier.Heavy, Vector2.zero);
            _fx.ShieldShatter(view.HitPoint, 100f, UITheme.SoftBlue, flight);
            return flight;
        }

        float Crit(int slot, string lineageId)
        {
            var view = Target(slot).View;
            var shooter = Guardian(slot);
            var from = shooter != null ? shooter.MuzzlePosition : _mapper.Origin;
            float flight = CritOn(view, from, lineageId, shooter);
            _focus = view.HitPoint;
            return flight;
        }

        /// <summary>
        /// 치명타 한 방 (실제 전투 순서: 기본 공격 → 같은 프레임 명중 키우기 → 치명타 겹침 → 금빛 숫자 → 킥 → 쏜 지킴이가 닿는 순간 통 뛴다)
        /// </summary>
        float CritOn(EnemyView view, Vector2 from, string lineageId, GuardianSlotView shooter = null)
        {
            float flight = ShootAt(view, from, lineageId, CombatStyle.Assault, 2, HitTier.Crit, 512f, shooter);
            _fx.PromoteImpact(view.Id);
            _fx.CritImpact(view, flight);
            int number = _fx.CritNumber(view.Id, view.HitPoint + new Vector2(0f, 40f), 512f * DamageScale, flight);
            _fx.AnchorNumber(number, view);
            _fx.Kick(Direction(from, view.HitPoint), view.IsBoss ? HitFeel.KickBossCrit : HitFeel.KickCrit, KickChannel.Crit, flight);
            // 기본 공격 치명타: 쏜 지킴이가 닿는 순간 기뻐한다 (BattleView.OnEnemyHit → CelebrateGuardian)
            CelebrateShooter(shooter, flight);
            return flight;
        }

        /// <summary>치명타 처치: 치명타 한 방이 닿는 순간까지 버티다 처치 꽃 (처치 등급 = 치명타 → 킥). 축하는 치명타 순간 한 번 (2.5초 간격)</summary>
        float CritKill(int slot, string lineageId)
        {
            var scarecrow = Target(slot);
            var view = scarecrow.View;
            var shooter = Guardian(slot);
            var from = shooter != null ? shooter.MuzzlePosition : _mapper.Origin;
            float flight = CritOn(view, from, lineageId, shooter);
            scarecrow.State.Hp = 0f;
            KillView(view, flight, HitTier.Crit);
            scarecrow.Exiting = true;
            CelebrateShooter(shooter, view.KillDelay);
            _focus = view.HitPoint;
            return view.KillDelay;
        }

        float SupportPulse(int slot, string lineageId)
        {
            if (_guardians == null || slot >= _guardians.Length) return 0f;
            var guardian = _guardians[slot];
            var accent = AttackFxCatalog.Look(lineageId).Accent;
            guardian.PlayPulse();
            _fx.SupportPulse(guardian.FeetPosition, guardian.MuzzlePosition, accent);
            for (int side = -1; side <= 1; side += 2)
            {
                int neighbor = (slot + side + BattleGeometry.SlotCount) % BattleGeometry.SlotCount;
                if (neighbor < _guardians.Length) _fx.SupportRibbon(guardian.MuzzlePosition, _guardians[neighbor].HeadPoint, accent);
            }
            _focus = guardian.FeetPosition;
            return 0.12f;
        }

        float HealPulse(int slot)
        {
            if (_guardians == null || slot >= _guardians.Length) return 0f;
            var guardian = _guardians[slot];
            guardian.PlayPulse();
            var config = ActiveConfig;
            float radius = (config != null ? config.healEchoRadius : 0.8f) * _mapper.Ppu;
            float delay = _fx.HealPulse(guardian.FeetPosition, guardian.MuzzlePosition, radius, _shop.DoorPosition);
            _fx.ShopHealed(_shop.DoorPosition, 6f, delay);
            _focus = guardian.FeetPosition;
            return 0.2f;
        }

        float LightHole(int slot, string lineageId)
        {
            if (_ambience != null && !_blackout)
            {
                _blackout = true;
                _ambience.SetModifier(StageModifierType.Blackout);
            }
            _fx.SetBattlePace(1, StageModifierType.Blackout);
            var target = Target(slot).View;
            float flight = GuardianShot(slot, lineageId, CombatStyle.Defense, 0, HitTier.Normal);
            if (lineageId != "mongdang" && lineageId != "lalabell") return flight;
            if (_ambience != null) _ambience.LightHole(target.HitPoint, EnemyFxCatalog.LightHoleRadius * _mapper.ActorScale, flight);
            _fx.LightPool(target.HitPoint, flight);
            return flight;
        }

        void RestoreAmbience()
        {
            if (!_blackout) return;
            _blackout = false;
            if (_ambience != null) _ambience.SetModifier(_savedModifier);
        }

        float Lamp()
        {
            _fx.LampBurst(_shop.DoorPosition);
            _fx.GlobalHitStop(0f, LampWave.IgniteScale, LampWave.IgniteTime);
            _shop.LampBurst();
            for (int i = 0; i < _scarecrows.Length; i++)
            {
                var view = Target(i).View;
                float delay = _fx.LampWaveDelay(view.Rect.anchoredPosition);
                _fx.LampHit(view.HitPoint, delay, view);
                view.QueueImpact(delay, HitTier.Lamp, Direction(_shop.FieldPosition, view.HitPoint), UITheme.Lamp);
                int number = _fx.DamageNumber(view.Id, view.HitPoint + new Vector2(0f, 34f), 240f, DamageNumberKind.Lamp, delay);
                _fx.AnchorNumber(number, view);
            }
            // 물결이 첫 허수아비(0번 방위)에 닿는 순간을 그 둘레에서
            var first = Target(0).View;
            _focus = first.HitPoint;
            return _fx.LampWaveDelay(first.Rect.anchoredPosition);
        }

        float Skill(int slot, string key)
        {
            var target = Target(slot).View;
            var look = SkillVfxLook.Get(key);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(key, out sprite, out isArt);
            int casterSlot = (slot + 2) % BattleGeometry.SlotCount;
            var caster = Guardian(casterSlot);
            var from = caster != null ? caster.MuzzlePosition : _mapper.Origin;
            // 시전: 위로 통 늘어나며 후광이 번진다 (BattleView.Skills처럼)
            if (caster != null) caster.PlayCast();
            float ppu = _mapper.Ppu;
            var radius = new Vector2(ppu * 0.7f, ppu * 0.7f * _mapper.Squash);
            var dir = Direction(from, target.HitPoint);
            float flight = _fx.SkillCast(new SkillFxRequest
            {
                Key = key,
                Look = look,
                Sprite = sprite,
                IsArt = isArt,
                From = from,
                To = target.HitPoint,
                Center = target.Rect.anchoredPosition,
                Radius = radius,
                BeamEnd = look.Style == SkillVfxStyle.Beam ? from + dir * ppu * 2.2f : Vector2.zero,
                Target = target,
                Power = 1f,
            });
            target.QueueImpact(flight, HitTier.Skill, dir, look.Accent);
            // 실제 전투의 시전 맥락 명중과 같은 길 (코어 기본 스킬 명중 + show 결 + 스킬 숫자)
            _fx.SkillHit(new SkillHitFx
            {
                Point = target.HitPoint, Target = target, Main = look.Main, Accent = look.Accent, Style = look.Style, Shard = look.Particle, Direction = dir,
                Delay = flight, Boss = target.IsBoss, Tick = look.Style == SkillVfxStyle.Ground,
            });
            int number = _fx.DamageNumber(target.Id, target.HitPoint + new Vector2(0f, 34f), 222f,
                look.Style == SkillVfxStyle.Ground ? DamageNumberKind.Dot : DamageNumberKind.Skill, flight, 1f, look.Accent);
            _fx.AnchorNumber(number, target);
            _focus = target.HitPoint;
            return flight;
        }

        /// <summary>스테이지 클리어 (BattleView.OnStageEnded와 같은 순서): 앱 전체 빛 번쩍임 + 리본 자리 보상 단추 + 수선점 불빛</summary>
        float StageClear()
        {
            var from = new Vector2(0f, _fx.FieldTop * (2f / 3f));
            _fx.ScreenFlash(UITheme.Cream, 0.3f, 0.6f);
            _fx.RewardShower(from, 5);
            _shop.LampBurst();
            _focus = from;
            return 0f;
        }

        /// <summary>숫자 뜻 모두를 한 화면에 (크기·색·모양 비교 — 4열 격자)</summary>
        float NumbersAll()
        {
            float halfWidth = _fx.FieldHalfWidth;
            float top = _fx.SafeTop - 60f;
            var kinds = (DamageNumberKind[])System.Enum.GetValues(typeof(DamageNumberKind));
            const int columns = 4;
            for (int i = 0; i < kinds.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                var at = new Vector2(-halfWidth + halfWidth * 2f * (column + 0.5f) / columns, top - row * 120f);
                float value = kinds[i] switch
                {
                    DamageNumberKind.Crit => 512f,
                    DamageNumberKind.Heavy => 256f,
                    DamageNumberKind.Boss => 300f,
                    DamageNumberKind.Mark => 64f,
                    DamageNumberKind.Dot => 12f,
                    DamageNumberKind.Heal => 40f,
                    DamageNumberKind.Block => 18f,
                    DamageNumberKind.ShopDamage => 5f,
                    DamageNumberKind.Skill => 222f,
                    DamageNumberKind.Lamp => 240f,
                    _ => 128f,
                };
                _fx.DamageNumber(BattleFx.NoMerge, at, value, kinds[i], 0.02f * i, 1f, UITheme.DustyPink);
            }
            _focus = new Vector2(0f, top - 120f);
            return 0f;
        }

        static Vector2 Direction(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            return delta.sqrMagnitude > 0.01f ? delta.normalized : Vector2.zero;
        }

        // ───────────── 목록 ─────────────

        void BuildEntries()
        {
            _entries.Clear();
            foreach (string lineage in Lineages)
            {
                string name = LineageName(lineage);
                foreach (var style in Styles)
                {
                    for (int tier = 0; tier <= 2; tier += 2)
                    {
                        _entries.Add(new Entry
                        {
                            Kind = Kind.GuardianShot, Id = lineage, Style = style, Tier = tier,
                            Label = "지킴이 공격 · " + name + " · " + UITheme.StyleName(style) + (tier >= 2 ? " · 2차 진화" : string.Empty),
                            Key = "guardian_" + lineage + "_" + style.ToString().ToLowerInvariant() + "_t" + tier,
                        });
                    }
                }
            }
            foreach (string lineage in Lineages)
            {
                string name = LineageName(lineage);
                _entries.Add(new Entry
                {
                    Kind = Kind.EchoAttack, Id = lineage, Style = CombatStyle.Assault, Label = "잔향이 공격 · " + name + " · 근접 베기", Key = "echo_" + lineage + "_melee",
                });
                _entries.Add(new Entry
                {
                    Kind = Kind.EchoAttack, Id = lineage, Style = CombatStyle.Snipe, Label = "잔향이 공격 · " + name + " · 원거리", Key = "echo_" + lineage + "_ranged",
                });
            }
            foreach (string enemy in EnemyFxCatalog.KnownEnemies)
            {
                _entries.Add(new Entry { Kind = Kind.StrikeEcho, Id = enemy, Label = "적 공격 → 잔향이 · " + EnemyName(enemy), Key = "strike_echo_" + enemy });
            }
            foreach (string enemy in EnemyFxCatalog.KnownEnemies)
            {
                _entries.Add(new Entry { Kind = Kind.StrikeShop, Id = enemy, Label = "적 공격 → 수선점 · " + EnemyName(enemy), Key = "strike_shop_" + enemy });
            }
            _entries.Add(new Entry { Kind = Kind.Siege, Id = "boss_shadow", Label = "보스 포위 예비 동작 · " + EnemyName("boss_shadow"), Key = "siege_boss_shadow" });
            _entries.Add(new Entry
            {
                Kind = Kind.Siege, Id = "boss_mothqueen", Label = "보스 포위 예비 동작 · " + EnemyName("boss_mothqueen"), Key = "siege_boss_mothqueen",
            });
            foreach (string enemy in EnemyFxCatalog.KnownEnemies)
            {
                bool boss = enemy.StartsWith("boss_", System.StringComparison.Ordinal);
                _entries.Add(new Entry
                {
                    Kind = Kind.Death, Id = enemy,
                    Label = "처치 · " + FamilyName(EnemyFxCatalog.Family(enemy)) + " · " + EnemyName(enemy),
                    Key = "death_" + enemy,
                    ScreenScale = boss,
                });
            }
            _entries.Add(new Entry { Kind = Kind.Spawn, Id = "enemy_dust", Label = "등장 · 먼지 소용돌이", Key = "spawn_enemy_dust" });
            _entries.Add(new Entry { Kind = Kind.Spawn, Id = "enemy_rust", Label = "등장 · 쇠붙이 소용돌이", Key = "spawn_enemy_rust" });
            _entries.Add(new Entry { Kind = Kind.Spawn, Id = "enemy_wisp", Label = "등장 · 그림자 소용돌이", Key = "spawn_enemy_wisp" });
            _entries.Add(new Entry { Kind = Kind.BossSpawn, Id = "boss_shadow", Label = "등장 · 보스 그림자 웅덩이", Key = "spawn_boss_shadow" });
            _entries.Add(new Entry { Kind = Kind.Minion, Id = "boss_mothqueen", Label = "등장 · 여왕이 부하를 부름", Key = "minion_boss_mothqueen" });
            _entries.Add(new Entry { Kind = Kind.ShieldHit, Label = "보호막 적 명중 · 막음", Key = "shield_hit" });
            _entries.Add(new Entry { Kind = Kind.ShieldBreak, Label = "보호막 깨짐 (강타)", Key = "shield_break" });
            foreach (string lineage in Lineages)
            {
                _entries.Add(new Entry { Kind = Kind.Crit, Id = lineage, Label = "치명타 · " + LineageName(lineage), Key = "crit_" + lineage, ScreenScale = lineage == "jjakjjak" });
            }
            _entries.Add(new Entry { Kind = Kind.SupportPulse, Id = "lalabell", Label = "지원 지킴이 맥박 · 리본과 왕관", Key = "pulse_support" });
            _entries.Add(new Entry { Kind = Kind.HealPulse, Label = "힐 지킴이 맥박 · 민트 고리와 방울", Key = "pulse_heal" });
            _entries.Add(new Entry { Kind = Kind.LightHole, Id = "mongdang", Label = "정전 · 빛 계보 명중 (몽당이) — 어둠에 구멍", Key = "blackout_mongdang" });
            _entries.Add(new Entry { Kind = Kind.LightHole, Id = "lalabell", Label = "정전 · 빛 계보 명중 (라라벨) — 어둠에 구멍", Key = "blackout_lalabell" });
            _entries.Add(new Entry { Kind = Kind.LightHole, Id = "chalkak", Label = "정전 · 빛이 아닌 계보 (찰칵이) — 구멍 없음", Key = "blackout_chalkak" });
            _entries.Add(new Entry { Kind = Kind.Lamp, Label = "수선점 불 밝히기 · 점화 → 빛 물결 → 여운", Key = "lamp", ScreenScale = true });
            foreach (string key in SkillVfx.All)
            {
                _entries.Add(new Entry
                {
                    Kind = Kind.Skill, Id = key, Label = "스킬 연출 · " + key, Key = "skill_" + key,
                    ScreenScale = key == SkillVfx.StarBurst || key == SkillVfx.CandleBurst,
                });
            }

            // ── FX-JUICE 새 항목 ──
            foreach (string lineage in Lineages)
            {
                _entries.Add(new Entry
                {
                    Kind = Kind.CritKill, Id = lineage, Label = "치명타 처치 · " + LineageName(lineage), Key = "crit_kill_" + lineage, ScreenScale = lineage == "jjakjjak",
                });
            }
            _entries.Add(new Entry { Kind = Kind.KillStreak, Label = "연속 처치 10 (1초) · 알림", Key = "kill_streak_10", ScreenScale = true });
            _entries.Add(new Entry { Kind = Kind.MultiKill, Label = "한꺼번에 5 (0.2초) · 알림 + 충격파", Key = "multikill_5", ScreenScale = true });
            _entries.Add(new Entry { Kind = Kind.StageClear, Label = "스테이지 클리어 · 빛 + 색종이 + 단추", Key = "stage_clear", ScreenScale = true });
            _entries.Add(new Entry { Kind = Kind.Busy, Label = "붐비는 전장 · 적 20 + 지킴이 5 + 잔향이 5 (2배속) + 범위 스킬 + 치명타", Key = "busy_20", ScreenScale = true });
            _entries.Add(new Entry { Kind = Kind.Numbers, Label = "피해 숫자 · 뜻 모두", Key = "numbers_all", ScreenScale = true });

            // ── 설정 변형 (BattleFx.DevOverride — PlayerPrefs를 쓰지 않는다) ──
            _entries.Add(new Entry { Kind = Kind.Lamp, Label = "불 밝히기 · 번쩍임 줄이기", Key = "lamp@reduce", Variant = Variant.Reduce, ScreenScale = true });
            _entries.Add(new Entry
            {
                Kind = Kind.Death, Id = "boss_shadow", Label = "보스 처치 · 번쩍임 줄이기", Key = "death_boss_shadow@reduce", Variant = Variant.Reduce, ScreenScale = true,
            });
            _entries.Add(new Entry
            {
                Kind = Kind.Death, Id = "boss_shadow", Label = "보스 처치 · 흔들림 0%", Key = "death_boss_shadow@shake0", Variant = Variant.Shake0, ScreenScale = true,
            });
            _entries.Add(new Entry { Kind = Kind.Busy, Label = "붐비는 전장 · 낮음 품질", Key = "busy_20@low", Variant = Variant.Low, ScreenScale = true });
            _entries.Add(new Entry
            {
                Kind = Kind.Crit, Id = "jjakjjak", Label = "치명타 · 짝짝이 · 번쩍임 줄이기", Key = "crit_jjakjjak@reduce", Variant = Variant.Reduce, ScreenScale = true,
            });

            // ── 예산 압박 (FX-JUICE §2.4 / §3.4 — 맨 뒤에 붙여 앞 항목들의 씨앗이 그대로) ──
            _entries.Add(new Entry
            {
                Kind = Kind.MassKill, Label = "한꺼번에 20 처치 · 범위 스킬 + 치명타 (붐비는 전장, 2배속)", Key = "mass_kill_20", ScreenScale = true,
            });
            _entries.Add(new Entry { Kind = Kind.LampFull, Label = "불 밝히기 · 적 20 전멸 (붐비는 전장, 2배속)", Key = "lamp_full_20", ScreenScale = true });
            _entries.Add(new Entry
            {
                Kind = Kind.MassKill, Label = "한꺼번에 20 처치 · 보통 품질", Key = "mass_kill_20@medium", Variant = Variant.Medium, ScreenScale = true,
            });
            _entries.Add(new Entry
            {
                Kind = Kind.LampFull, Label = "불 밝히기 · 적 20 전멸 · 보통 품질", Key = "lamp_full_20@medium", Variant = Variant.Medium, ScreenScale = true,
            });
            _entries.Add(new Entry
            {
                Kind = Kind.MassKill, Label = "한꺼번에 20 처치 · 낮음 품질", Key = "mass_kill_20@low", Variant = Variant.Low, ScreenScale = true,
            });
            _entries.Add(new Entry
            {
                Kind = Kind.LampFull, Label = "불 밝히기 · 적 20 전멸 · 낮음 품질", Key = "lamp_full_20@low", Variant = Variant.Low, ScreenScale = true,
            });

            // ── 앱 전체 빛 번쩍임 줄이기 확인 (r2 비평 — 맨 뒤에 붙여 앞 항목들의 씨앗이 그대로) ──
            _entries.Add(new Entry { Kind = Kind.StageClear, Label = "스테이지 클리어 · 번쩍임 줄이기", Key = "stage_clear@reduce", Variant = Variant.Reduce, ScreenScale = true });

            // ── 자리 변형 (Wave 3 · fx2 leftovers 비평 (a)(b) — 맨 뒤에 붙여 앞 항목들의 씨앗이 그대로) ──
            _entries.Add(new Entry
            {
                Kind = Kind.Death, Id = "boss_shadow", Label = "보스 처치 · 띠 위 끝 보스", Key = "death_boss_shadow@top", Variant = Variant.Top, ScreenScale = true,
            });
            _entries.Add(new Entry
            {
                Kind = Kind.Death, Id = "boss_shadow", Label = "보스 처치 · 시트 열림", Key = "death_boss_shadow@sheet", Variant = Variant.Sheet, ScreenScale = true,
            });
            _entries.Add(new Entry { Kind = Kind.StageClear, Label = "스테이지 클리어 · 시트 열림", Key = "stage_clear@sheet", Variant = Variant.Sheet, ScreenScale = true });
            _entries.Add(new Entry { Kind = Kind.Numbers, Label = "피해 숫자 · 뜻 모두 · 시트 열림", Key = "numbers_all@sheet", Variant = Variant.Sheet, ScreenScale = true });
            _entries.Add(new Entry { Kind = Kind.Lamp, Label = "불 밝히기 · 시트 열림", Key = "lamp@sheet", Variant = Variant.Sheet, ScreenScale = true });
        }

        static string LineageName(string id)
        {
            var lineage = BattleUnitArt.Lineage(id, null);
            return lineage != null && !string.IsNullOrEmpty(lineage.name) ? lineage.name : id;
        }

        string EnemyName(string id)
        {
            var def = EnemyDef(id);
            return def != null && !string.IsNullOrEmpty(def.name) ? def.name : id;
        }

        static string FamilyName(EnemyDeathFamily family) => family switch
        {
            EnemyDeathFamily.Moth => "나방",
            EnemyDeathFamily.Metal => "쇠붙이",
            EnemyDeathFamily.Shadow => "그림자",
            _ => "먼지",
        };
    }
}
