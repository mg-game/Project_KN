using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잠자기 모드 (IDLE-06, UX-SPEC-IA §4.9 — 지도가 보인다). 아무 입력 없이 설정 시간(GameSettings.SleepMinutes, 기본 2분)이 지나면
    /// 전투 화면을 반만 어둡게(Dim 0.5) 덮고 위아래 띠(상단 바 · 위 HUD / 빠른 아이콘 · 가이드 줄 · 덱 띠 · 탭 바)를 가린 뒤,
    /// 아래 상태 카드에 "쉿, 친구들이 수선점을 지키고 있어요"와 실시간 숫자(물리친 잊힘 · 모은 단추 · 지금 스테이지)를 띄운다 — 싸움은 그대로 보인다.
    /// (잔향 = 우리 편 잔향이라 적 수는 '잊힘'으로 센다.)
    /// 자는 동안은 30fps로 낮추고, 화면을 누르면 깨어나 원래 프레임으로 돌아간다. 팝업이 열려 있거나 튜토리얼 중이면 잠들지 않는다.
    /// 전투는 그대로 진행된다 (폰을 세워 두는 방치 플레이).
    /// 시트가 열려 있으면 먼저 내리고(UINav.GoToMain, 움직임 있음) 다 내려간 뒤에 어두워지기 시작한다 — 시간이 다 됐을 때도, SleepNow도 같은 길.
    /// 기다리는 동안 입력이 있거나 잠들 수 없게 되면(팝업 · 튜토리얼) 그만둔다 (SleepNow는 끝까지 기다린다). 시트가 다시 열리면 그만둔다.
    /// 깨우는 손가락이 아래 UI(덱 카드·소환 버튼·탭)를 누르지 않게, 덮개는 다 사라지고 손을 뗄 때까지 입력을 계속 받는다
    /// (EventSystem과 이 Update의 순서가 정해져 있지 않아, 누른 그 프레임에 덮개를 풀면 아래 버튼이 눌릴 수 있다).
    /// 뒤로(M15): 자는 중이면 깨우고 먹는다, 덮개가 걷히는 중에도 먹는다 (켜질 때 UIManager에 뒤로 처리기를 넣는다).
    /// PC·에디터 창이 포커스를 잃으면 잠들지 않고, 잠들어 있었으면 바로 깬다 (M01: 다른 창을 보는 동안에도 게임이 돌아가는 모습이 그대로 보여야 한다).
    /// 모바일은 그대로다 (백그라운드에서는 OS가 앱을 멈춘다).
    /// 소리 (AUDIO-SPEC §8.1): 잠들 때 AudioService.SetSleep(true) (잠드는 소리 · 배경음 낮춤), 깰 때 SetSleep(false) (깨는 소리 · 되돌림).
    /// 잠 신호 (SPEC4 §3.6 IDLE-06): 덮개가 다 짙어지면 AppSignals.RaiseSleep(true) (전투 연출 줄이기 · 소리 줄이기가 받는다), 깨면 RaiseSleep(false).
    /// 다 짙어지기 전에 깨면 아무것도 보내지 않는다 (보낸 true에는 꼭 false 한 번 — 꺼질 때도).
    /// 전투 위 메타 HUD(빠른 아이콘 줄 · 바구니 · 가이드 줄 · 잠금 덮개 — Battle/MetaHud의 CanvasGroup, _hudGroups)는 덮개가 짙어지는 만큼 함께 사라지고
    /// 깨면 돌아온다 (알파 = 1 − 덮개 알파 — 밝은 NEW · 빨간 점 · '가득!'이 띠 흐림 사이로 비치지 않게, "싸움만 남는다"). 누름은 Dim이 막는다.
    /// 그 묶음은 이 뷰만 쓴다 (SheetFade는 각 HUD 루트의 자기 묶음 — 곱해진다). 덮개 알파가 바뀌는 동안만 쓴다.
    /// </summary>
    public class SleepModeView : MonoBehaviour
    {
        const float FadeSpeed = 2.5f;
        const float StatInterval = 1f;
        /// <summary>시트가 다 내려가길 기다리는 최대 시간 (그 뒤에도 닫히는 중이면 그냥 잠든다 — 시트가 멈추지 않는 경우 대비)</summary>
        const float SheetWaitLimit = 1.5f;

        [SerializeField] GameObject _overlay;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Button _wakeButton;
        [Tooltip("숨 쉬는 수선점 그림 (예전 가운데 그림 — 없어도 됨, 지도가 보이는 새 덮개에는 없다)")]
        [SerializeField] RectTransform _shop;
        [SerializeField] TMP_Text _killsText;
        [SerializeField] TMP_Text _goldText;
        [SerializeField] TMP_Text _stageText;
        [Tooltip("떠오르는 Zzz (없어도 됨)")]
        [SerializeField] TMP_Text _zzz;
        [Tooltip("자는 동안 함께 사라지는 HUD 묶음 (Battle/MetaHud — 이 뷰만 쓰는 곱하기 묶음, 없어도 됨)")]
        [SerializeField] CanvasGroup[] _hudGroups = Array.Empty<CanvasGroup>();

        float _idle;
        bool _asleep;
        float _statTimer;
        float _time;
        long _killsAtSleep;
        double _goldAtSleep;
        long _shownKills = -1;
        double _shownGold = -1;
        string _shownStage;
        Vector3 _lastMouse;
        Vector2 _zzzBase;

        /// <summary>시트가 내려가길 기다리는 중 (그 뒤 잠든다)</summary>
        bool _sleepPending;
        /// <summary>SleepNow가 시작한 기다림 (입력 · 팝업에 그만두지 않는다)</summary>
        bool _pendingForced;
        /// <summary>기다리는 동안 시트 목표가 한 번이라도 닫힘이었는지 (그 뒤 다시 열리면 그만둔다)</summary>
        bool _sheetClosed;
        float _pendingTime;

        Func<bool> _backHandler;
        bool _backRegistered;
        /// <summary>HUD 묶음에 마지막으로 쓴 알파 (같으면 다시 쓰지 않는다)</summary>
        float _hudAlpha = 1f;
        /// <summary>이번 잠에 AppSignals.RaiseSleep(true)를 보냈는지 (깰 때 false를 한 번 보낸다)</summary>
        bool _sleepSignaled;

        /// <summary>지금 자는 중인지</summary>
        public bool IsAsleep => _asleep;

        void Awake()
        {
            _backHandler = OnBack;
            if (_wakeButton != null) _wakeButton.onClick.AddListener(Wake);
            if (_zzz != null) _zzzBase = _zzz.rectTransform.anchoredPosition;
            _overlay.SetActive(false);
            _group.alpha = 0f;
        }

        /// <summary>잠들기까지 분 (GameSettings.SleepMinutes는 PlayerPrefs — 매 프레임 읽지 않고 Changed에서 다시 읽는다, perf PF-6 L1)</summary>
        int _sleepMinutes;

        void OnEnable()
        {
            _sleepMinutes = GameSettings.SleepMinutes;
            GameSettings.Changed += OnSettingsChanged;
            RegisterBack();
        }

        void OnSettingsChanged() => _sleepMinutes = GameSettings.SleepMinutes;

        void Start() => RegisterBack();

        void Update()
        {
            if (!Game.IsReady) return;
            float dt = Time.unscaledDeltaTime;
            bool input = HasInput(ref _lastMouse);

            if (_asleep)
            {
                if (input)
                {
                    Wake();
                    return;
                }
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, dt * FadeSpeed);
                SetHudAlpha(1f - _group.alpha);
                // 덮개가 다 짙어졌다 → 잠 신호 (한 번)
                if (!_sleepSignaled && _group.alpha >= 1f) SignalSleep(true);
                Animate(dt);
                _statTimer -= dt;
                if (_statTimer <= 0f) RefreshStats();
                return;
            }

            if (_overlay.activeSelf)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, dt * FadeSpeed * 2f);
                SetHudAlpha(1f - _group.alpha);
                // 깨운 누름은 덮개가 끝까지 받는다: 다 사라졌고 손가락·마우스를 뗐을 때만 덮개를 걷는다
                if (_group.alpha <= 0f && !IsPointerHeld())
                {
                    _group.blocksRaycasts = false;
                    _overlay.SetActive(false);
                }
            }

            if (_sleepPending && WaitForSheet(dt, input)) return;

            int minutes = _sleepMinutes;
            if (input || minutes <= 0 || !CanSleep())
            {
                _idle = 0f;
                return;
            }
            _idle += dt;
            if (_idle >= minutes * 60f) BeginSleep(false);
        }

        /// <summary>지금 누르고 있는 손가락·마우스 단추가 있는지</summary>
        static bool IsPointerHeld() =>
            Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);

        /// <summary>
        /// 이번 프레임에 입력이 있었는지 — 손가락이 닿아 있음 · 마우스 왼쪽 단추를 누르고 있음 · 아무 키나 눌림 · 휠 · 마우스가 2px 넘게 움직임.
        /// 잠자기 시계와 시트 자동 복귀 시계(SheetController, UX-SPEC-IA §4.8)가 같은 정의를 쓴다 (두 시계가 어긋나지 않게 — 강화 x1 길게 누르기도 입력).
        /// lastMouse = 부르는 쪽이 들고 있는 지난 마우스 위치 (여기서 지금 위치로 바꾼다). 할당 없음.
        /// </summary>
        public static bool HasInput(ref Vector3 lastMouse)
        {
            var mouse = Input.mousePosition;
            bool moved = (mouse - lastMouse).sqrMagnitude > 4f;
            lastMouse = mouse;
            return Input.touchCount > 0 || Input.anyKeyDown || Input.GetMouseButton(0) || Input.mouseScrollDelta.sqrMagnitude > 0f || moved;
        }

        /// <summary>
        /// 지금 잠든다 (도구 · 테스트 — AutoShot 'sleep' 장면, UX-SPEC-IA §4.9 · CR-04). 시간이 다 된 것과 같은 길:
        /// 시트가 열려 있으면 먼저 내리고(UINav.GoToMain) 다 내려간 뒤에 어두워지기 시작한다.
        /// 잠자기 설정 시간 · 팝업 · 튜토리얼과 상관없이 잠들고, 시트를 기다리는 동안 입력에도 그만두지 않는다 (시트가 다시 열리면 그만둔다).
        /// </summary>
        public void SleepNow()
        {
            if (_asleep || !Game.IsReady) return;
            BeginSleep(true);
        }

        /// <summary>자는 동안 사라지는 HUD 묶음의 알파 (바뀔 때만 쓴다 — 덮개가 짙어지거나 걷히는 동안)</summary>
        void SetHudAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (Mathf.Approximately(alpha, _hudAlpha)) return;
            _hudAlpha = alpha;
            for (int i = 0; i < _hudGroups.Length; i++)
            {
                if (_hudGroups[i] != null) _hudGroups[i].alpha = alpha;
            }
        }

        static bool CanSleep()
        {
            // 포커스 없는 PC 창(다른 모니터에서 지켜보는 중)은 어둡게 덮지 않는다 (M01)
            if (!Application.isMobilePlatform && !Application.isFocused) return false;
            if (UIManager.Instance != null && UIManager.Instance.HasOpenPopup) return false;
            if (!MetaUi.IsActive) return true;
            return !Game.Tutorial.IsActive;
        }

        /// <summary>잠들기 시작: 시트가 열려 있거나 아직 움직이면 먼저 내리고 기다린다, 아니면 바로 잠든다</summary>
        void BeginSleep(bool forced)
        {
            _idle = 0f;
            if (!SheetDown())
            {
                _sleepPending = true;
                _pendingForced = forced;
                _pendingTime = 0f;
                _sheetClosed = false;
                if (UIFrame.SheetOpen) UINav.GoToMain();
                return;
            }
            Sleep();
        }

        /// <summary>시트가 닫혀 멈췄는지 (MAIN)</summary>
        static bool SheetDown() => !UIFrame.SheetOpen && UIFrame.SheetSettled && UIFrame.SheetProgress <= 0f;

        /// <summary>
        /// 시트가 내려가길 기다린다. 이번 프레임을 여기서 끝냈으면 true (잠듦 · 계속 기다림), 그만뒀으면 false (보통 시계로 돌아간다).
        /// 다 내려가 멈추면 잠든다 (닫히는 중인 채 SheetWaitLimit가 지나도 잠든다). 한 번 닫혔다가 다시 열렸거나(탭 · 길 안내)
        /// 끝내 닫히지 않으면 그만둔다. 시간으로 시작한 기다림은 입력 · 팝업 · 튜토리얼에도 그만둔다
        /// </summary>
        bool WaitForSheet(float dt, bool input)
        {
            _pendingTime += dt;
            if (!_pendingForced && (input || !CanSleep()))
            {
                CancelPending();
                return false;
            }
            if (UIFrame.SheetOpen)
            {
                if (_sheetClosed || _pendingTime >= SheetWaitLimit)
                {
                    CancelPending();
                    return false;
                }
                return true;
            }
            _sheetClosed = true;
            if (SheetDown() || _pendingTime >= SheetWaitLimit)
            {
                CancelPending();
                Sleep();
            }
            return true;
        }

        void CancelPending()
        {
            _sleepPending = false;
            _pendingForced = false;
            _sheetClosed = false;
            _pendingTime = 0f;
            _idle = 0f;
        }

        void Sleep()
        {
            _asleep = true;
            _idle = 0f;
            _overlay.SetActive(true);
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _killsAtSleep = Game.Data.battle != null ? Game.Data.battle.totalKills : 0;
            _goldAtSleep = Game.Currency.Get(CurrencyType.Gold);
            _shownKills = -1;
            _shownGold = -1;
            _shownStage = null;
            RefreshStats();
            // 30 fps (perf PF-1 FramePacing — PC에서도 실제로 30, 설정 화면의 Apply가 60으로 되돌리지 않는다)
            FramePacing.Sleeping = true;
            AudioService.SetSleep(true);
        }

        /// <summary>
        /// 깨운다: 프레임을 되돌리고 덮개를 걷기 시작한다. 덮개는 입력을 계속 막고(blocksRaycasts 유지) Update에서
        /// 다 사라지고 손을 뗀 뒤에야 꺼진다 — 깨운 누름은 덮개의 버튼이 받아 아래 UI로 새지 않는다.
        /// </summary>
        void Wake()
        {
            if (!_asleep) return;
            _asleep = false;
            _idle = 0f;
            FramePacing.Sleeping = false;
            GameSettings.Apply();
            AudioService.SetSleep(false);
            if (_sleepSignaled) SignalSleep(false);
        }

        /// <summary>AppSignals 잠 신호 (true는 덮개가 다 짙어졌을 때, false는 그 뒤 깰 때 · 꺼질 때 한 번). 받는 쪽 예외는 여기서 막는다</summary>
        void SignalSleep(bool asleep)
        {
            _sleepSignaled = asleep;
            try
            {
                AppSignals.RaiseSleep(asleep);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>뒤로 (M15): 자는 중이면 깨우고 먹는다. 덮개가 걷히는 중에도 먹는다 (아래 화면에 닿지 않게)</summary>
        bool OnBack()
        {
            if (_asleep)
            {
                Wake();
                return true;
            }
            return _overlay != null && _overlay.activeSelf;
        }

        void RegisterBack()
        {
            if (_backRegistered) return;
            var ui = UIManager.Instance;
            if (ui == null) return;
            ui.PushBackHandler(_backHandler);
            _backRegistered = true;
        }

        void UnregisterBack()
        {
            if (!_backRegistered) return;
            _backRegistered = false;
            var ui = UIManager.Instance;
            if (ui != null) ui.RemoveBackHandler(_backHandler);
        }

        /// <summary>
        /// 창 포커스가 바뀜 (M01). 잠들기까지 센 시간을 처음부터 다시 세고, 돌아올 때 마우스가 튄 거리를 입력으로 세지 않게 위치를 새로 잡는다.
        /// 시트를 기다리던 잠들기는 그만둔다. PC·에디터에서 잠든 채로 포커스를 잃으면 깨운다 — 덮개는 Update에서 걷히고 프레임 목표도 되돌아간다.
        /// 모바일은 그대로 둔다
        /// </summary>
        void OnApplicationFocus(bool focused)
        {
            _idle = 0f;
            _lastMouse = Input.mousePosition;
            if (_sleepPending && !_pendingForced) CancelPending();
            if (!focused && _asleep && !Application.isMobilePlatform) Wake();
        }

        void Animate(float dt)
        {
            _time += dt;
            if (_shop != null) _shop.localScale = new Vector3(1f + 0.015f * Mathf.Sin(_time * 1.3f), 1f - 0.02f * Mathf.Sin(_time * 1.3f), 1f);
            if (_zzz != null)
            {
                float k = Mathf.Repeat(_time / 3f, 1f);
                _zzz.rectTransform.anchoredPosition = _zzzBase + new Vector2(30f * k, 60f * k);
                _zzz.alpha = Mathf.Sin(k * Mathf.PI);
            }
        }

        void RefreshStats()
        {
            _statTimer = StatInterval;
            long kills = (Game.Data.battle != null ? Game.Data.battle.totalKills : 0) - _killsAtSleep;
            if (kills != _shownKills)
            {
                _shownKills = kills;
                if (_killsText != null) _killsText.text = $"물리친 잊힘 {MetaUi.Int(System.Math.Max(0, kills))}";
            }
            double gold = System.Math.Max(0, Game.Currency.Get(CurrencyType.Gold) - _goldAtSleep);
            double goldKey = System.Math.Floor(gold);
            if (goldKey != _shownGold)
            {
                _shownGold = goldKey;
                if (_goldText != null) _goldText.text = $"모은 단추 {RewardService.FormatAmount(CurrencyType.Gold, gold)}";
            }
            string stage = Game.Battle.StageLabel;
            if (stage != _shownStage)
            {
                _shownStage = stage;
                if (_stageText != null) _stageText.text = $"스테이지 {stage}";
            }
        }

        void OnDisable()
        {
            UnregisterBack();
            GameSettings.Changed -= OnSettingsChanged;
            if (_asleep)
            {
                FramePacing.Sleeping = false;
                GameSettings.Apply();
                AudioService.SetSleep(false);
            }
            if (_sleepSignaled) SignalSleep(false);
            _asleep = false;
            _sleepPending = false;
            // 꺼지는 중에는 SetActive를 부르지 않는다 — 투명·통과로만 두면 다시 켜질 때 Update가 덮개를 마저 끈다
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }
            // HUD는 바로 돌려놓는다
            _hudAlpha = -1f;
            SetHudAlpha(1f);
        }
    }
}
