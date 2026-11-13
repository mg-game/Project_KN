using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IdleGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGame
{
    /// <summary>
    /// 게임 시작점. 세이브 로드 → 시스템 초기화 → 메타 세션 시작 → 공지 로드 → 시작 팝업 순서로 진행하고,
    /// 방치 수익 지급과 자동 저장을 담당한다.
    /// 메타(MetaSession)가 켜져 있으면 비운 시간 보상은 수선 바구니가 맡고(복귀 화면), 꺼져 있으면 예전 오프라인 보상 흐름을 쓴다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>테스트 등에서 세이브 경로를 바꿀 때 사용</summary>
        public static string SavePathOverride;

        public static GameBootstrap Instance { get; private set; }

        /// <summary>
        /// 개발용 시계 바꾸기 (V2 가짜 시계 캡처: 달의 3일 · 둘째 날 · 80시간 비움). Awake 전에 넣으면 Game.Setup이 이 시계를 쓴다.
        /// null = 실제 기기 시계 (기본 — 출시 빌드에서는 아무도 넣지 않는다)
        /// </summary>
        public static ITimeProvider ClockOverride;

        [SerializeField] GameConfig _config;
        [SerializeField] ItemDatabase _itemDatabase;
        [SerializeField] CharacterDatabase _characterDatabase;
        [SerializeField] GachaTable[] _gachaTables;
        [SerializeField] BattleConfig _battleConfig;
        [SerializeField] ExpeditionDatabase _expeditionDatabase;
        [SerializeField] ShopDatabase _shopDatabase;
        [SerializeField] SynergyDatabase _synergyDatabase;
        [SerializeField] MetaDatabase _metaDatabase;
        [SerializeField] UIManager _ui;

        /// <summary>방치 단추는 프레임마다 모아 두었다가 이 간격 이상 지나고 1개 이상 모였을 때 한 번에 지급한다 (Currency.Changed가 매 프레임 울리지 않게)</summary>
        const float IdleGoldMinInterval = 0.2f;
        /// <summary>1개가 다 모이지 않아도 이 간격이 지나면 지급한다</summary>
        const float IdleGoldMaxInterval = 1f;
        /// <summary>켜 둔 채로 밤 8시를 넘겨도 야간 방문(밤지기 의상 조건)이 인정되도록 다시 확인하는 간격(초)</summary>
        const float NightVisitCheckInterval = 30f;

        /// <summary>세이브를 바꾼 뒤 다음 부팅에 한 번 띄우는 토스트 (SAVE-01 §4.2, ReloadAfterReplace)</summary>
        public const string ReplacedToast = "진행을 옮겨 왔어요";
        /// <summary>ReplacedToast를 다음 부팅에 띄울지 (PlayerPrefs 한 번짜리 표시)</summary>
        const string ReplacedToastKey = "save.replaced_toast";
        /// <summary>
        /// 종료 때 백그라운드 자동 저장(작업 스레드)을 기다리는 한계 (ms, RI-8). 보통 이미 끝나 있어 0 ms —
        /// 넘으면 경고 한 줄만 (파일 순서는 저장소의 저장 번호가 지킨다)
        /// </summary>
        public const int QuitFlushTimeoutMs = 500;

        ISaveStorage _storage;
        SaveLoadResult _loadResult;

        /// <summary>세이브 저장소 (가져오기 · 백업 복구 화면이 쓴다, SAVE-01). Awake 전에는 null</summary>
        public ISaveStorage Storage => _storage;
        float _autoSaveTimer;
        double _idleGold;
        float _idleGoldTimer;
        float _nightVisitTimer;
        /// <summary>백그라운드 진입 때 미리 지급한 오프라인 보상 (복귀 시 알림용)</summary>
        double _offlineClaimedOnPause;
        bool _wasPaused;
        bool _resetting;
        /// <summary>날마다 백업을 마지막으로 확인한 날 키 (그 날 첫 저장 뒤에 Storage.SnapshotDaily, SAVE-01)</summary>
        string _snapshotDayKey;
        /// <summary>플레이 기록 구독 (CR-09). Awake에서 Game.Setup 뒤에 연결, OnDestroy에서 푼다</summary>
        PlayLogRecorder _playLog;
        /// <summary>알림 계획 목록 (일시정지 · 종료 때 다시 채운다 — 재사용, NOTI-01)</summary>
        readonly List<PlannedNotification> _notificationPlan = new(NotificationPlanner.MaxPending + 1);

        void Awake()
        {
            Instance = this;
            // 창이 포커스를 잃어도(에디터 플레이 / PC 빌드) 전투·방치 진행이 멈추지 않게 한다. 모바일은 OS가 백그라운드에서 멈춘다
            Application.runInBackground = true;
            // 목표 프레임(기본 60)과 소리는 기기 설정에서 적용한다
            GameSettings.Apply();
            // UI 효과음·배경음 표가 있으면 "[Audio]"를 만든다 (아직 소리 파일이 없으면 아무것도 만들지 않는다 — M18)
            UISfx.Boot();

            // 개발용 시계(ClockOverride)가 있으면 게임과 저장소(백업 이름 · 목록의 오늘)가 같은 시계를 쓴다
            ITimeProvider clock = ClockOverride ?? new LocalTimeProvider();
            string savePath = SavePathOverride ?? Path.Combine(Application.persistentDataPath, "player.json");
            _storage = new LocalSaveStorage(savePath, clock);
            var data = _storage.Load();
            // 읽지 못한 세이브는 저장소가 격리해 두었으므로 새로 시작해도 원본이 덮이지 않는다. 시작 팝업에서 안내한다
            _loadResult = _storage.LastLoadResult;
            data ??= PlayerData.CreateNew(_config);
            var content = new GameContent
            {
                Config = _config,
                Items = _itemDatabase,
                Characters = _characterDatabase,
                GachaTables = _gachaTables,
                Battle = _battleConfig,
                Expeditions = _expeditionDatabase,
                Shops = _shopDatabase,
                Synergies = _synergyDatabase,
                Meta = _metaDatabase,
            };
            Game.Setup(data, content, clock, new System.Random());

            GrantStarterCharacters();
            Game.Mail.RemoveExpired();
            Game.Mail.SendOnce("welcome",
                "수선점에 오신 것을 환영해요!",
                "잊힌 물건들이 살아 움직이는 밤거리의 작은 수선점.\n지킴이로 방향을 지키고, 잔향이로 위기를 막아 수선점을 밝혀 주세요!\n시작을 돕는 선물을 보내드려요.",
                new[] { RewardData.Currency(CurrencyType.Gem, 1000), RewardData.Currency(CurrencyType.Gold, 500) },
                expireDays: 0);
            Game.Costumes.CheckNightVisit();
            RunMeta(MetaStep.Start);
            if (!Game.IsMetaActive) Game.OfflineReward.Calculate(data.lastSaveUnix);
            // 첫 직렬화(JIT · 계약 만들기 30~225 ms)를 첫 자동 저장(30초 — 대개 첫 전투 · 튜토리얼 중) 대신 부팅 프레임에 (perf PF-3 H2).
            // 작업 스레드(Task.Run 첫 호출)와 날 키 확인도 함께 데운다 — 첫 자동 저장도 1.5 ms 안에 (perf A5(a))
            if (_storage is LocalSaveStorage local) local.Warm(Game.Data);
            // 야간 방문 확인은 자동 저장과 다른 프레임에 (둘 다 30초 간격이라 같은 프레임에 겹쳤다)
            _nightVisitTimer = NightVisitCheckInterval * 0.5f;
            StartPlayLog(Path.GetDirectoryName(Path.GetFullPath(savePath)), clock);
            // 켜 둔 동안에는 예약 알림을 두지 않고, 받은 알림 · 배지는 지운다 (권한은 기기 설정에 맞춘다)
            LocalNotifications.Initialize();
            LocalNotifications.SyncSavedPermission();
            LocalNotifications.CancelAll();
            LocalNotifications.ClearDelivered();
        }

        /// <summary>플레이 기록을 세이브 폴더에 열고 구독한다 (CR-09). 열지 못해도 게임은 그대로 (경고 한 줄)</summary>
        void StartPlayLog(string directory, ITimeProvider clock)
        {
            try
            {
                PlayLog.Open(directory, clock);
                _playLog = PlayLogRecorder.FromGame();
                _playLog.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayLog] 플레이 기록을 시작하지 못했어요: {e.Message}");
            }
        }

        IEnumerator Start()
        {
            yield return NoticeLoader.Load(_config.noticeUrl, Game.Notice.SetNotices);
            QueueSaveLoadMessage();
            if (_ui == null) yield break;
            _ui.ShowStartupPopups();
            ShowReplacedToast();
        }

        /// <summary>세이브를 바꾸고 다시 시작했으면 '진행을 옮겨 왔어요'를 한 번 띄운다 (표시는 바로 지운다)</summary>
        void ShowReplacedToast()
        {
            if (PlayerPrefs.GetInt(ReplacedToastKey, 0) != 1) return;
            PlayerPrefs.DeleteKey(ReplacedToastKey);
            PlayerPrefs.Save();
            _ui.ShowToast(ReplacedToast, ToastKind.Success);
        }

        /// <summary>세이브를 백업에서 불러왔거나 읽지 못했으면 접속 팝업 맨 앞에 안내를 띄운다.</summary>
        void QueueSaveLoadMessage()
        {
            switch (_loadResult)
            {
                case SaveLoadResult.RecoveredFromBackup:
                    _ui.QueueStartupMessage("세이브 데이터 복구",
                        "최근 저장 파일을 읽지 못해 직전 백업에서 불러왔어요.\n마지막 몇십 초 동안의 진행이 빠졌을 수 있어요.");
                    break;
                case SaveLoadResult.Unreadable:
                    _ui.QueueStartupMessage("세이브 데이터를 불러오지 못했어요",
                        "저장된 진행 상황을 읽을 수 없어 처음부터 시작해요.\n기존 세이브 파일은 지우지 않고 기기에 따로 보관해 두었어요.");
                    break;
            }
        }

        /// <summary>새 게임이면 시작 캐릭터를 지급하고 자동 편성한다.</summary>
        static void GrantStarterCharacters()
        {
            if (Game.Data.lineages.Count > 0) return;
            foreach (var lineageId in Game.Config.starterLineages) Game.Characters.Grant(lineageId);
            Game.Formation.AutoFormation();
            Game.Battle.Restart();
        }

        void Update()
        {
            if (!Game.IsReady) return;

            PlayLog.Tick(Time.unscaledDeltaTime);

            // 화면 주사율이 바뀌면 프레임 목표를 다시 맞춘다 (1초에 한 번 확인, 모바일 — perf PF-1)
            FramePacing.Tick(Time.unscaledDeltaTime);
            AccrueIdleGold(Time.deltaTime);
            Game.Battle.Tick(Time.deltaTime);
            if (Game.IsMetaActive) RunMeta(MetaStep.Tick);

            _nightVisitTimer += Time.unscaledDeltaTime;
            if (_nightVisitTimer >= NightVisitCheckInterval)
            {
                _nightVisitTimer = 0f;
                Game.Costumes.CheckNightVisit();
            }

            // 일시정지(OnApplicationPause(true)부터 복귀까지)에는 자동 저장 타이머도 멈춘다 (RI-7): 일시정지 때 Save()로 이미 저장했고,
            // 그사이 Update가 도는 기기(또는 가짜 시계 캡처)에서 lastSaveUnix가 앞당겨져 비운 시간이 0으로 읽히지 않게
            if (ShouldAutoSave(ref _autoSaveTimer, Time.unscaledDeltaTime, _config.autoSaveInterval, _wasPaused)) AutoSave();
        }

        /// <summary>
        /// 자동 저장 타이머를 한 프레임만큼 진행하고 저장할 차례인지 돌려준다 (순수 — SaveStorageTests가 고정한다).
        /// 일시정지 중이면 타이머를 멈추고 false (RI-7). 0 이하 · NaN dt는 더하지 않는다. 타이머를 0으로 되돌리는 것은 AutoSave · Save
        /// </summary>
        public static bool ShouldAutoSave(ref float timer, float dt, float interval, bool paused)
        {
            if (paused) return false;
            if (dt > 0f) timer += dt;
            return timer >= interval;
        }

        static readonly Unity.Profiling.ProfilerMarker s_autoSaveMarker = new Unity.Profiling.ProfilerMarker("LL.AutoSave");

        /// <summary>
        /// 30초 자동 저장 (perf PF-3 H1): 진행을 메인 스레드에서 JSON으로 찍고 파일 쓰기는 백그라운드로 (LocalSaveStorage.SaveInBackground).
        /// 측정: 동기 저장 5.5~7.9 ms · 첫 저장 55~83 ms · 디스크가 바쁠 때 1,364 ms 멈춤 → 메인 스레드는 직렬화만 (0.2~1 ms).
        /// 일시정지 · 종료 · 설정 화면 저장은 Save()로 그대로 동기 — 저장 번호가 가장 새 진행을 지킨다
        /// </summary>
        void AutoSave()
        {
            using (s_autoSaveMarker.Auto())
            {
                if (!(_storage is LocalSaveStorage local))
                {
                    Save();
                    return;
                }
                _autoSaveTimer = 0;
                if (!Game.IsReady || _resetting) return;
                CommitIdleGold();
                Game.Data.lastSaveUnix = TimeUtil.ToUnix(Game.Clock.UtcNow);
                local.SaveInBackground(Game.Data, NextDailyKey());
            }
        }

        void AccrueIdleGold(float dt)
        {
            _idleGold += Game.Growth.GoldPerSecond * dt;
            _idleGoldTimer += dt;
            if (_idleGoldTimer >= IdleGoldMaxInterval || (_idleGoldTimer >= IdleGoldMinInterval && _idleGold >= 1d)) CommitIdleGold();
        }

        /// <summary>모아 둔 방치 단추를 지급한다. 저장 직전에도 불러 저장에서 빠지지 않게 한다.</summary>
        void CommitIdleGold()
        {
            _idleGoldTimer = 0f;
            double gold = _idleGold;
            _idleGold = 0;
            Game.Currency.Add(CurrencyType.Gold, gold); // 0 이하/NaN은 Add가 무시한다
        }

        void OnApplicationPause(bool paused)
        {
            if (!Game.IsReady) return;

            if (paused)
            {
                _wasPaused = true;
                RunMeta(MetaStep.Pause);
                // 백그라운드에서 강제 종료될 수 있으므로 받지 않은 오프라인 보상은 지급 후 저장 (복귀 시 알린다)
                if (Game.OfflineReward.HasPending) _offlineClaimedOnPause += Game.OfflineReward.Claim();
                Save();
                // 전투 중 한 번짜리 표시(팁 · 자동 품질)는 PlayerPrefs.Save 없이 바꾸므로 여기서 디스크에 (perf PF-6 H5)
                PlayerPrefs.Save();
                ScheduleNotifications();
                _playLog?.OnPause();
                PlayLog.Flush();
            }
            else if (_wasPaused)
            {
                _wasPaused = false;
                LocalNotifications.CancelAll();
                LocalNotifications.ClearDelivered();
                LocalNotifications.SyncSavedPermission();
                _playLog?.OnResume();
                Game.Costumes.CheckNightVisit();
                _nightVisitTimer = 0f;
                // 메타가 켜져 있으면 비운 시간은 수선 바구니에 쌓이고 복귀 화면은 PopupQueue로 뜬다
                RunMeta(MetaStep.Resume);
                if (Game.IsMetaActive) return;
                Game.OfflineReward.Calculate(Game.Data.lastSaveUnix);

                var offline = _ui.Get<OfflineRewardPopup>();
                if (_offlineClaimedOnPause > 0)
                {
                    offline.NotifyClaimedInBackground(_offlineClaimedOnPause);
                    _offlineClaimedOnPause = 0;
                }
                if (Game.OfflineReward.HasPending) offline.Open();
            }
        }

        void OnApplicationQuit()
        {
            if (!Game.IsReady) return;
            RunMeta(MetaStep.Quit);
            if (Game.OfflineReward.HasPending) Game.OfflineReward.Claim();
            Save();
            FlushBackgroundSave();
            ScheduleNotifications();
            _playLog?.OnQuit();
            PlayLog.Flush();
        }

        /// <summary>
        /// 줄 서 있거나 쓰는 중인 자동 저장(작업 스레드 — 날마다 백업 복사 · 부팅 예열 포함)이 끝날 때까지 QuitFlushTimeoutMs까지 기다린다 (RI-8):
        /// 종료 중에 파일을 만지는 스레드가 남지 않게. 동기 Save() 뒤라 본 파일은 이미 가장 새 진행이다. 예외를 내지 않는다
        /// </summary>
        void FlushBackgroundSave()
        {
            if (_storage is LocalSaveStorage local && !local.FlushPending(QuitFlushTimeoutMs))
                Debug.LogWarning($"[Save] 종료 전에 자동 저장이 {QuitFlushTimeoutMs} ms 안에 끝나지 않았어요 (진행은 방금 저장했어요)");
        }

        /// <summary>
        /// 지금 상태로 알림 계획을 세워 예약을 바꾼다 (일시정지 · 종료, NOTI-01 §4.3: CancelAll 뒤 예약). 백엔드가 없으면(에디터 · PC) 아무것도 하지 않는다.
        /// 권한(게임 안에서 허락) · 설정 스위치 · 튜토리얼은 NotificationContext.Capture · Planner가 본다. 예외를 내지 않는다
        /// </summary>
        void ScheduleNotifications()
        {
            if (!LocalNotifications.HasBackend) return;
            try
            {
                var context = NotificationContext.Capture(Game.Clock, Game.DailyClock?.TimeZone, Game.IsMetaActive, Game.Tutorial, Game.IdleChest,
                    Game.Expedition, Game.Attendance, Game.Dungeon, Game.DailyClock);
                NotificationPlanner.Plan(in context, _notificationPlan);
                LocalNotifications.Reschedule(_notificationPlan);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Notify] 알림 계획 실패: {e.Message}");
            }
        }

        enum MetaStep
        {
            Start,
            Tick,
            Pause,
            Resume,
            Quit,
        }

        /// <summary>
        /// 메타 세션 호출. 메타 시스템이 아직 구현 중(NotImplementedException)이면 메타를 끄고 예전 흐름으로 돌아간다 —
        /// 3차 구현이 끝나기 전에 씬을 돌려도 게임이 멈추지 않게 하는 안전장치. 다른 예외는 그대로 올린다.
        /// </summary>
        void RunMeta(MetaStep step)
        {
            if (!Game.IsMetaActive) return;
            try
            {
                var session = Game.MetaSession;
                switch (step)
                {
                    case MetaStep.Start:
                        session.Start();
                        break;
                    case MetaStep.Tick:
                        session.Tick(Time.unscaledDeltaTime);
                        break;
                    case MetaStep.Pause:
                        session.OnPause();
                        break;
                    case MetaStep.Resume:
                        session.OnResume();
                        break;
                    case MetaStep.Quit:
                        session.OnQuit();
                        break;
                }
            }
            catch (NotImplementedException e)
            {
                Game.IsMetaActive = false;
                Debug.LogWarning($"[Meta] 메타 기능이 아직 구현되지 않아 끕니다 ({step}): {e.Message}");
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            // 다시 불러온 장면이 지난 장면의 잠자기 · 튜토리얼 연출 표시를 이어받지 않게 (RM-1 요청 7)
            AppSignals.ResetState();
            _playLog?.Disconnect();
            _playLog = null;
            PlayLog.Close();
            Game.Shutdown();
        }

        public void Save()
        {
            _autoSaveTimer = 0;
            if (!Game.IsReady || _resetting) return;
            CommitIdleGold();
            Game.Data.lastSaveUnix = TimeUtil.ToUnix(Game.Clock.UtcNow);
            _storage.Save(Game.Data);
            SnapshotDailyIfNewDay();
        }

        /// <summary>그 날 첫 저장이면 방금 저장한 세이브를 날마다 백업으로 남긴다 (새것 3개, 저장소가 정리). 같은 날은 문자열 비교 한 번</summary>
        void SnapshotDailyIfNewDay()
        {
            string dayKey = NextDailyKey();
            if (dayKey != null) _storage.SnapshotDaily(dayKey);
        }

        /// <summary>그 날 첫 저장이면 날 키를 돌려주고 기억한다 (날마다 백업을 남길 차례), 아니면 null</summary>
        string NextDailyKey()
        {
            string dayKey = Game.DailyClock?.DayKey;
            if (string.IsNullOrEmpty(dayKey) || dayKey == _snapshotDayKey) return null;
            _snapshotDayKey = dayKey;
            return dayKey;
        }

        /// <summary>세이브를 지우고 씬을 다시 불러온다.</summary>
        public void ResetData()
        {
            _resetting = true;
            _storage.Delete();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// 저장소의 세이브를 바꾼 뒤(Storage.Replace · RestoreBackup이 true일 때) 씬을 다시 불러온다: 지금 메모리의 진행으로 덮어쓰지 않게
        /// 자동 저장 · 일시정지 · 종료 저장을 건너뛰고(_resetting), 다음 부팅에 '진행을 옮겨 왔어요'를 한 번 띄운다 (SAVE-01 §4.2).
        /// 두 번 불러도 한 번만 다시 불러온다
        /// </summary>
        public void ReloadAfterReplace()
        {
            if (_resetting) return;
            _resetting = true;
            PlayerPrefs.SetInt(ReplacedToastKey, 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
