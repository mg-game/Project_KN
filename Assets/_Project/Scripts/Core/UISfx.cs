using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGame
{
    /// <summary>
    /// 소리 진입점 (M18 Phase B → AUD-01). 공개 API는 예전 그대로 (Table · Boot · SetTable · Play(UISfxKey, float) · ApplySettings · SfxGain · BgmGain).
    ///   · 부팅(GameBootstrap.Awake → Boot): 라이브러리(Resources/Audio/AudioLibrary) 또는 예전 표(Resources/Audio/UISfxTable)에 클립이 하나라도 있으면
    ///     DontDestroyOnLoad "[Audio]"를 만든다 — 코드 버스 엔진(AudioEngine, 소스 31개) + 전투 감독(BattleAudioDirector). 없으면 아무것도 만들지 않는다.
    ///   · Play(key, volume): (1) 라이브러리에 그 키의 클립이 있으면 엔진 (징글 5키는 Stinger 버스) (2) 없으면 예전 표 (3) 그것도 없으면 조용히.
    ///     제한은 예전 UISfxGate (같은 소리 40ms — 세는 소리 70 · 볼륨 막대 150 · 빨간 점 2000 · 쓰다듬기 120, 한 소리 3개, 음높이 ±5%, 단추 착지 +3%씩).
    ///   · 볼륨: 효과음 버스(Ui · Sfx · Stinger · Loop) = SfxVolume × 소리 켜짐, 배경음 = BgmVolume × 소리 켜짐, 환경음 = 배경음 × 0.9
    ///     (GameSettings.Apply → ApplySettings가 밀어 준다).
    ///   · 창 포커스를 잃어도 멈추거나 줄이지 않는다 (M01). 모바일 백그라운드(OnApplicationPause(true))에서만 배경음·환경음을 0.2초에 줄이고 멈춘다.
    ///   · 재생 모드가 아니면(편집 모드) 아무것도 하지 않는다 (사운드 시험장은 따로 미리 듣기 소스를 쓴다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UISfx : MonoBehaviour
    {
        /// <summary>예전 표 에셋 경로 (Resources 기준)</summary>
        public const string TableResourcePath = "Audio/UISfxTable";
        /// <summary>UI 효과음 소스 수 (Ui 버스)</summary>
        public const int SfxVoices = 8;
        /// <summary>백그라운드 진입 때 배경음을 줄이는 시간 (초)</summary>
        public const float BgmPauseFadeSeconds = 0.2f;

        static UISfx _instance;
        static UISfxTable _table;
        static bool _tableLoaded;
        static AudioLibrary _library;
        static bool _libraryLoaded;
        /// <summary>앱 종료 · 재생 모드 끝 (Application.quitting): 그 뒤 OnDisable/OnDestroy에서 부른 Play가 "[Audio]"를 새로 만들지 않게</summary>
        static bool _quitting;

        AudioEngine _engine;
        int _bootFrame;
        AudioSettings.AudioConfigurationChangeHandler _onAudioConfig;

        /// <summary>재생을 시작할 때마다 (도메인 다시 불러오기를 끈 에디터 포함): 종료 표시를 지우고 종료 알림을 한 번만 건다</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitOnPlay()
        {
            _quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting() => _quitting = true;

        /// <summary>지금 쓰는 예전 표 (처음 읽을 때 Resources에서 한 번 불러온다, 없으면 null)</summary>
        public static UISfxTable Table
        {
            get
            {
                if (!_tableLoaded)
                {
                    _tableLoaded = true;
                    _table = Resources.Load<UISfxTable>(TableResourcePath);
                }
                return _table;
            }
        }

        /// <summary>빌드된 소리 목록 (처음 읽을 때 Resources에서 한 번, 없으면 null)</summary>
        public static AudioLibrary Library
        {
            get
            {
                if (!_libraryLoaded)
                {
                    _libraryLoaded = true;
                    _library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
                }
                return _library;
            }
        }

        /// <summary>클립이 하나라도 있는지 (라이브러리 또는 예전 표)</summary>
        public static bool HasClips => ResolveBootMode(Library, Table) != AudioBootMode.Silent;

        /// <summary>부팅 방식 (순수 함수 — AudioBootTests): 클립 없음 = 조용 (오브젝트 없음) · 예전 표만 = 예전 경로 · 라이브러리 = 엔진</summary>
        public static AudioBootMode ResolveBootMode(AudioLibrary library, UISfxTable table)
        {
            if (library != null && library.HasAnyClip) return AudioBootMode.Engine;
            if (table != null && table.HasAnyClip) return AudioBootMode.Legacy;
            return AudioBootMode.Silent;
        }

        /// <summary>부팅 때 한 번 (GameBootstrap.Awake). 클립이 있으면 "[Audio]"를 만들고 0.5초 뒤 음악을 시작한다</summary>
        public static void Boot()
        {
            if (!HasClips) return;
            Ensure();
        }

        /// <summary>예전 표를 바꾼다 (null이면 예전 표만 끔 — 라이브러리 소리는 그대로)</summary>
        public static void SetTable(UISfxTable table)
        {
            _table = table;
            _tableLoaded = true;
            if (_instance != null) _instance._engine.SetLegacyTable(table);
            else if (table != null) Boot();
        }

        /// <summary>
        /// 효과음 하나 (volume = 0~1 배율, 예: Tap 0.5). 클립이 없거나 제한(UISfxGate)에 걸리면 조용히 넘어간다.
        /// 재생 모드가 아니면(편집 모드) 아무것도 하지 않는다.
        /// </summary>
        public static void Play(UISfxKey key, float volume = 1f)
        {
            if (!(volume > 0f)) return;
            var audio = _instance;
            if (audio == null)
            {
                if (!HasClips) return;
                audio = Ensure();
                if (audio == null) return;
            }
            audio._engine.PlayUi(key, volume);
        }

        /// <summary>설정 볼륨·소리 켜짐을 다시 읽는다 (GameSettings.Apply가 부름). "[Audio]"가 없으면 할 일 없음</summary>
        public static void ApplySettings()
        {
            if (_instance != null) _instance._engine.ApplySettings();
        }

        /// <summary>미리 듣기 등에서 쓸 효과음 볼륨 = SfxVolume × 소리 켜짐</summary>
        public static float SfxGain => GameSettings.SoundOn ? GameSettings.SfxVolume : 0f;

        /// <summary>배경음 볼륨 = BgmVolume × 소리 켜짐</summary>
        public static float BgmGain => GameSettings.SoundOn ? GameSettings.BgmVolume : 0f;

        /// <summary>라이브러리를 다시 읽는다 (null이면 Resources에서). 돌고 있는 엔진에도 바로 반영 (사운드 시험장 · 재생 모드)</summary>
        public static void ReloadLibrary(AudioLibrary library)
        {
            if (library == null)
            {
                _libraryLoaded = false;
                _library = null;
                library = Library;
            }
            else
            {
                _library = library;
                _libraryLoaded = true;
            }
            if (_instance != null) _instance._engine.ReloadLibrary(library);
            else if (Application.isPlaying && HasClips) Ensure();
        }

        static UISfx Ensure()
        {
            if (_instance != null) return _instance;
            // 종료 중에 새 GameObject를 만들면 에디터가 "Some objects were not cleaned up when closing the scene"을 낸다
            if (!Application.isPlaying || _quitting) return null;
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UISfx>();
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _bootFrame = Time.frameCount;
            var library = Library;
            if (Application.isEditor && library != null && library.WasRemapped)
                Debug.LogWarning("[Audio] 오디오 라이브러리가 카탈로그보다 오래됐어요 (이름으로 맞춰 씁니다) — IdleGame/Audio/라이브러리 다시 만들기");
            _engine = new AudioEngine(gameObject, library, Table);
            AudioService.Attach(_engine);
            _engine.ApplySettings();
            gameObject.AddComponent<BattleAudioDirector>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _onAudioConfig = OnAudioConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += _onAudioConfig;
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_onAudioConfig != null) AudioSettings.OnAudioConfigurationChanged -= _onAudioConfig;
            AudioService.Detach(_engine);
            _instance = null;
        }

        /// <summary>
        /// 이어폰 · 블루투스 · HDMI 연결/해제, AudioSettings.Reset: Unity가 울리던 소스를 모두 멈춘다 — 엔진이 다음 프레임에 음악 · 환경음 · 반복 소리를 이어 튼다
        /// </summary>
        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (_engine != null) _engine.NotifyAudioConfigurationChanged();
        }

        /// <summary>장면 다시 불러오기 (ResetData): 사라진 화면의 음악·집중 요청을 비우고 Main으로. 듣는 귀를 확인한다</summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_engine == null) return;
            _engine.CheckListener();
            if (Time.frameCount == _bootFrame || mode == LoadSceneMode.Additive) return;
            _engine.ResetForScene();
        }

        void LateUpdate()
        {
            if (_engine != null) _engine.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        /// <summary>모바일 백그라운드에서만 배경음을 줄이고 멈춘다. 포커스 잃음(OnApplicationFocus)은 일부러 받지 않는다</summary>
        void OnApplicationPause(bool paused)
        {
            if (_engine != null) _engine.OnPause(paused);
        }
    }

    /// <summary>부팅 방식 (UISfx.ResolveBootMode)</summary>
    public enum AudioBootMode : byte
    {
        /// <summary>클립이 없다: "[Audio]"를 만들지 않고 모든 호출이 조용히 지나간다 (오늘의 동작)</summary>
        Silent = 0,
        /// <summary>예전 표(UISfxTable)만 있다: 그 클립으로 UI 소리와 배경음 하나</summary>
        Legacy = 1,
        /// <summary>라이브러리가 있다: 코드 버스 엔진 전체</summary>
        Engine = 2,
    }

    /// <summary>
    /// UI 효과음 제한 (순수 C#, 시각·흔들기 값은 부르는 쪽이 준다): 같은 소리는 MinInterval(key) 안에 한 번 (기본 SameKeyInterval 40ms),
    /// 한 소리 MaxVoicesPerKey개까지 겹침, 음높이 ±PitchJitter. CoinLand는 CoinRiseWindow 안에 이어지면 CoinRiseStep씩 올라간다 (최대 CoinRiseMax, 흔들기 없음).
    /// 조율 키(CountTick)는 흔들지 않는다 (사다리 음높이는 엔진이 정한다).
    /// </summary>
    public sealed class UISfxGate
    {
        /// <summary>UISfxKey 개수 (배열 크기)</summary>
        public static readonly int KeyCount = Enum.GetValues(typeof(UISfxKey)).Length;

        public const float SameKeyInterval = 0.04f;
        public const int MaxVoicesPerKey = 3;
        public const float PitchJitter = 0.05f;
        public const float CoinRiseStep = 0.03f;
        public const float CoinRiseMax = 0.18f;
        public const float CoinRiseWindow = 0.3f;
        /// <summary>복귀 화면 숫자 소리 (70ms에 하나)</summary>
        public const float CountTickInterval = 0.07f;
        /// <summary>설정 볼륨 막대 미리 듣기 (150ms에 하나)</summary>
        public const float SliderTickInterval = 0.15f;
        /// <summary>빨간 점 (2초에 하나)</summary>
        public const float RedDotInterval = 2f;
        /// <summary>쓰다듬기 (120ms에 하나)</summary>
        public const float PetSqueakInterval = 0.12f;

        readonly float[] _lastPlay = new float[KeyCount];
        readonly float[] _busyUntil = new float[KeyCount * MaxVoicesPerKey];
        float _lastCoin = float.NegativeInfinity;
        int _coinStreak;

        public UISfxGate() => Reset();

        /// <summary>그 소리의 최소 간격 (초)</summary>
        public static float MinInterval(UISfxKey key) => key switch
        {
            UISfxKey.CountTick => CountTickInterval,
            UISfxKey.SliderTick => SliderTickInterval,
            UISfxKey.RedDotNew => RedDotInterval,
            UISfxKey.PetSqueak => PetSqueakInterval,
            _ => SameKeyInterval,
        };

        /// <summary>음높이 흔들기 폭 (조율 키는 0)</summary>
        public static float JitterFor(UISfxKey key) => key == UISfxKey.CountTick ? 0f : PitchJitter;

        /// <summary>
        /// 지금(now, 초) 그 소리를 내도 되면 true와 음높이. clipLength = 클립 길이(초, 겹침 계산용), jitter01 = 0~1 무작위 값.
        /// 통과하면 시각과 겹침 칸을 기록한다.
        /// </summary>
        public bool TryPlay(UISfxKey key, float now, float clipLength, float jitter01, out float pitch)
        {
            pitch = 1f;
            int k = (int)key;
            if (k < 0 || k >= KeyCount) return false;
            if (now - _lastPlay[k] < MinInterval(key)) return false;

            int voice = -1;
            for (int v = 0; v < MaxVoicesPerKey; v++)
            {
                if (_busyUntil[k * MaxVoicesPerKey + v] <= now)
                {
                    voice = v;
                    break;
                }
            }
            if (voice < 0) return false;

            if (key == UISfxKey.CoinLand)
            {
                _coinStreak = now - _lastCoin <= CoinRiseWindow ? _coinStreak + 1 : 0;
                _lastCoin = now;
                pitch = 1f + Math.Min(CoinRiseMax, CoinRiseStep * _coinStreak);
            }
            else
            {
                float jitter = Math.Max(0f, Math.Min(1f, jitter01));
                pitch = 1f + (jitter * 2f - 1f) * JitterFor(key);
            }

            _lastPlay[k] = now;
            _busyUntil[k * MaxVoicesPerKey + voice] = now + Math.Max(0f, clipLength) / pitch;
            return true;
        }

        public void Reset()
        {
            for (int i = 0; i < _lastPlay.Length; i++) _lastPlay[i] = float.NegativeInfinity;
            for (int i = 0; i < _busyUntil.Length; i++) _busyUntil[i] = float.NegativeInfinity;
            _lastCoin = float.NegativeInfinity;
            _coinStreak = 0;
        }
    }
}
