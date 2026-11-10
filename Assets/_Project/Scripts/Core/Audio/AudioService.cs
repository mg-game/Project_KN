using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 소리 공개 API (AUDIO-SPEC §3.2). "[Audio]"(UISfx.Boot — 클립이 하나라도 있을 때만)가 없으면 모두 조용히 지나간다 (예외 · 로그 없음).
    ///   · 키 → 번호는 시작할 때 한 번 (Resolve), 재생은 번호로 (매 프레임 문자열 없음). Play(string)은 드문 UI 순간용 편의 함수다.
    ///   · 음악은 주인 스택: 패널이 열릴 때 PushMusic(상태, GetInstanceID()), 닫힐 때 PopMusic(같은 ID). 가장 높은 우선순위가 울린다.
    ///   · 전장 집중: 시트가 전장을 덮으면 SetBattleFocus(Dimmed, 주인), 전체 화면이면 Hidden, 닫으면 ClearBattleFocus(주인).
    ///   · 잠: SetSleep(true/false) — 잠드는/깨는 소리(ui_sleep_enter / ui_sleep_wake)도 여기서 낸다.
    ///   · 소환: 하이라이트마다 OnGachaReveal(등급) — 전설 이상이면 배경음을 줄이고 결과 징글을 고른다.
    /// 전투 소리는 BattleAudioDirector가 Game.Battle 이벤트를 듣고 스스로 낸다 (전투 화면 코드는 부를 것이 없다).
    /// </summary>
    public static class AudioService
    {
        static AudioEngine _engine;
        static bool _voiceBarks;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        static bool _latencyComp = true;
#else
        static bool _latencyComp;
#endif
        static readonly AudioStats EmptyStats = new();
        static System.Collections.Generic.HashSet<string> _warnedKeys;

        internal static AudioEngine Engine => _engine;

        // AppSignals 구독 (AUDIO-SPEC §0.3 · §8.2 #4, PHASE4 delta 14): 처리기는 한 번만 만들어 두고 엔진이 붙을 때 걸고 떨어질 때 푼다 (할당 없음)
        static readonly System.Action<bool> SleepSignalHandler = OnSleepSignal;
        static readonly System.Action<int> GachaRevealHighHandler = OnGachaRevealHighSignal;
        static bool _signalsHooked;

        internal static void Attach(AudioEngine engine)
        {
            _engine = engine;
            if (engine != null) engine.LatencyCompensation = _latencyComp;
            HookSignals(engine != null);
        }

        internal static void Detach(AudioEngine engine)
        {
            if (_engine != engine) return;
            _engine = null;
            HookSignals(false);
        }

        /// <summary>
        /// AppSignals를 듣는다 (엔진이 있을 때만).
        ///   · SleepChanged → SetSleep: 엔진이 같은 상태면 무시하므로 SleepModeView가 SetSleep을 직접 불러도 두 번 울리지 않는다.
        ///   · GachaRevealHigh → 배경음 덕(−8 dB, 1.8초, OnGachaReveal의 징글 덕과 같은 이유 번호라 겹치면 갱신될 뿐)만.
        ///     징글은 OnGachaReveal이 낸다 — 같은 공개에 신호와 직접 호출이 함께 와도 징글이 다시 시작되지 않게.
        /// 늦게 붙어도 지금 잠 상태(AppSignals.IsAsleep)를 따라가지 않는다: "[Audio]"는 게임 시작 때 한 번 생기고, 잠드는 소리를 엉뚱하게 내지 않기 위해.
        /// </summary>
        static void HookSignals(bool on)
        {
            if (on == _signalsHooked) return;
            _signalsHooked = on;
            if (on)
            {
                AppSignals.SleepChanged += SleepSignalHandler;
                AppSignals.GachaRevealHigh += GachaRevealHighHandler;
            }
            else
            {
                AppSignals.SleepChanged -= SleepSignalHandler;
                AppSignals.GachaRevealHigh -= GachaRevealHighHandler;
            }
        }

        static void OnSleepSignal(bool asleep) => SetSleep(asleep);

        static void OnGachaRevealHighSignal(int grade)
        {
            if (_engine == null || grade < (int)ItemGrade.Legendary) return;
            _engine.Duck(AudioBus.Bgm, -8f, 1.8f, 0.05f, 0.8f, DuckReason.GachaHigh);
        }

        /// <summary>클립이 하나라도 있는지 (라이브러리 또는 예전 표) — 설정의 볼륨 막대를 살리고 '준비 중' 표시를 끈다</summary>
        public static bool HasClips => UISfx.HasClips;

        /// <summary>"[Audio]"가 돌고 있는지</summary>
        public static bool IsRunning => _engine != null;

        /// <summary>키 이름 → 번호 (모르는 키면 무효). 시작할 때 한 번만 부르고 번호를 들고 있는다</summary>
        public static AudioId Resolve(string key) => AudioCatalog.Find(key);

        /// <summary>한 번 울린다 (volume 0~1, pan −1~1, pitch 배율). 음악 키는 무시, 징글 키는 PlayJingle과 같다</summary>
        public static void Play(AudioId id, float volume = 1f, float pan = 0f, float pitch = 1f)
        {
            if (_engine != null) _engine.Play(id, volume, pan, pitch);
        }

        /// <summary>
        /// 캐릭터 목소리(vox_* 키) 한 번 — 울렸으면 true. "[Audio]"가 없거나, 목소리 설정(VoiceBarks)이 꺼졌거나, 그 키(별칭이면 대상)에 클립이 없거나,
        /// 목소리 공통 간격(AudioEngine.BarkInterval 6초 — 다른 목소리가 막 울렸으면)이 아직이면 아무것도 울리지 않고 false: 부른 쪽이 대신 소리를 낸다
        /// (GuardianPetView: if (!AudioService.TryPlayBark(bark)) UISfx.Play(UISfxKey.PetSqueak)). vox 키가 아니면 false. 예외 · 할당 없음
        /// </summary>
        public static bool TryPlayBark(AudioId id, float volume = 1f) => _engine != null && _engine.TryPlayBark(id, volume);

        /// <summary>키 이름으로 한 번 (드문 UI 순간용 — 사전 조회가 한 번 들어간다)</summary>
        public static void Play(string key, float volume = 1f, float pan = 0f)
        {
            if (_engine == null) return;
            var id = AudioCatalog.Find(key);
            if (!id.IsValid) WarnUnknown(key);
            _engine.Play(id, volume, pan, 1f);
        }

        /// <summary>모르는 키: 에디터 · 개발 빌드에서만 키마다 한 번 경고 (§3.15 — 소리는 조용히 지나간다)</summary>
        static void WarnUnknown(string key)
        {
            if (!Debug.isDebugBuild || string.IsNullOrEmpty(key)) return;
            _warnedKeys ??= new System.Collections.Generic.HashSet<string>();
            if (_warnedKeys.Add(key)) Debug.LogWarning("[Audio] 카탈로그에 없는 소리 키예요 (조용히 지나감): " + key);
        }

        /// <summary>UISfx.Play와 같다 (UI 게이트 · 예전 표 포함)</summary>
        public static void PlayUi(UISfxKey key, float volume = 1f) => UISfx.Play(key, volume);

        /// <summary>seconds초 뒤 (실제 시간) 한 번</summary>
        public static void PlayAfter(AudioId id, float seconds, float volume = 1f, float pan = 0f)
        {
            if (_engine != null) _engine.PlayAfter(id, seconds, volume, pan);
        }

        /// <summary>음악 상태를 요청한다 (owner = 부르는 쪽 GetInstanceID(), 같은 주인은 갱신). 가장 높은 우선순위가 울린다</summary>
        public static void PushMusic(MusicState state, int owner)
        {
            if (_engine != null) _engine.PushMusic(state, owner);
        }

        public static void PopMusic(int owner)
        {
            if (_engine != null) _engine.PopMusic(owner);
        }

        /// <summary>바닥 음악 상태 (기본 Main). 주인 스택이 비었을 때 울리는 상태 — 예: 제목 화면이 Story를 바닥으로</summary>
        public static void PlayBgm(MusicState state)
        {
            if (_engine != null) _engine.SetBaseMusic(state);
        }

        /// <summary>지금 울리는 음악 상태</summary>
        public static MusicState Music => _engine != null ? _engine.Music.State : MusicState.Silent;

        /// <summary>환경음 기믹 층을 바꾼다 (전투 감독이 StageChanged마다 부른다 — 다른 화면에서 분위기를 바꿀 때도)</summary>
        public static void SetAmbience(StageModifierType modifier)
        {
            if (_engine != null) _engine.SetModifier(modifier, false);
        }

        /// <summary>징글 (Stinger 버스 + 그 징글의 배경음 덕)</summary>
        public static void PlayJingle(AudioId id)
        {
            if (_engine != null) _engine.PlayJingle(id);
        }

        public static void PlayJingle(string key)
        {
            if (_engine == null) return;
            var id = AudioCatalog.Find(key);
            if (!id.IsValid) WarnUnknown(key);
            _engine.PlayJingle(id);
        }

        /// <summary>잠자기 모드 (배경음 ×0.35 + 1.2 kHz 저역 통과 또는 bgm_sleep, 전투 −8 dB, 예산 ×0.5, 쏘는 소리 없음) + 잠드는/깨는 소리</summary>
        public static void SetSleep(bool asleep)
        {
            if (_engine != null) _engine.SetSleep(asleep);
        }

        public static bool IsAsleep => _engine != null && _engine.IsAsleep;

        /// <summary>전장 집중 (주인별, 가장 강한 것): Dimmed = 시트가 전장 40% 이상, Hidden = 전체 화면</summary>
        public static void SetBattleFocus(BattleAudioFocus focus, int owner)
        {
            if (_engine != null) _engine.SetBattleFocus(focus, owner);
        }

        public static void ClearBattleFocus(int owner)
        {
            if (_engine != null) _engine.ClearBattleFocus(owner);
        }

        public static BattleAudioFocus Focus => _engine != null ? _engine.Focus : BattleAudioFocus.Full;

        /// <summary>
        /// 소환 결과 하이라이트 (카드마다): 전설 → jingle_gacha_legend, 신화 → jingle_gacha_mythic (둘 다 배경음 −8 dB 1.8초).
        /// 그보다 낮으면 아무것도 하지 않는다 (공개 효과음은 GachaRevealCommon/Rare/Epic UISfx 키)
        /// </summary>
        public static void OnGachaReveal(ItemGrade grade)
        {
            if (_engine == null) return;
            if (grade >= ItemGrade.Mythic) _engine.PlayJingle(AudioCatalog.Find("jingle_gacha_mythic"));
            else if (grade >= ItemGrade.Legendary) _engine.PlayJingle(AudioCatalog.Find("jingle_gacha_legend"));
        }

        /// <summary>불 밝히기 합계 판의 소리 키 (ui_count_finish 별칭: 작은 종 + 단추 딸깍 + 반짝, 0.5초 간격 · 동시 1)</summary>
        public const string LampSumKey = "lamp_sum";
        const float LampSumVolume = 0.8f;
        static AudioId _lampSum;

        /// <summary>
        /// 불 밝히기 합계 판이 뜬 순간 (BattleFx.ShowLampSum이 판을 띄운 뒤 한 번 — WP-5B 요청, RM-2): 작은 종을 한 번 울린다.
        /// 잠자기 중이거나 전장이 가려졌으면(Hidden) 조용히. "[Audio]"가 없으면 아무것도 하지 않는다 (예외 없음 · 할당 없음 — 키 번호는 처음 한 번만 찾는다)
        /// </summary>
        public static void OnLampSumShown()
        {
            if (_engine == null || _engine.IsAsleep || _engine.Focus == BattleAudioFocus.Hidden) return;
            if (!_lampSum.IsValid) _lampSum = AudioCatalog.Find(LampSumKey);
            _engine.Play(_lampSum, LampSumVolume, 0f, 1f);
        }

        /// <summary>버스 덕 (gainDb ≤ 0, hold초 유지). 같은 reason은 다시 걸면 갱신된다 — 겹치면 가장 깊은 것</summary>
        public static void Duck(AudioBus bus, float gainDb, float hold, float attack = 0.05f, float release = 0.4f, int reason = 0)
        {
            if (_engine != null) _engine.Duck(bus, gainDb, hold, attack, release, reason);
        }

        /// <summary>예측한 전투 소리(투사체 도착 등)를 출력 지연만큼 일찍 예약한다 (기본: Android/iOS 빌드에서 켬, 최대 60ms)</summary>
        public static bool LatencyCompensation
        {
            get => _latencyComp;
            set
            {
                _latencyComp = value;
                if (_engine != null) _engine.LatencyCompensation = value;
            }
        }

        /// <summary>라이브러리를 다시 읽는다 (사운드 시험장 · 테스트). null이면 Resources에서</summary>
        public static void ReloadLibrary(AudioLibrary library = null) => UISfx.ReloadLibrary(library);

        /// <summary>
        /// 캐릭터 목소리 (§7.9): 설정 패키지가 GameSettings.VoiceBarks를 만들기 전까지 false — 그동안 vox_* 키는 울리지 않는다.
        /// 켜져 있어도 6초에 한 번까지
        /// </summary>
        public static bool VoiceBarks
        {
            get => _voiceBarks;
            set => _voiceBarks = value;
        }

        /// <summary>개발 HUD용 통계 (목소리 · 버린 소리 · 예산 · 없는 키)</summary>
        public static AudioStats Stats => _engine != null ? _engine.Stats : EmptyStats;
    }
}
