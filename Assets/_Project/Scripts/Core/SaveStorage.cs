using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace IdleGame
{
    /// <summary>마지막 Load 결과</summary>
    public enum SaveLoadResult
    {
        /// <summary>아직 Load를 부르지 않음</summary>
        None,
        /// <summary>세이브 파일이 없음 (첫 실행)</summary>
        NotFound,
        Loaded,
        /// <summary>최신 세이브를 읽지 못해 직전 백업(.bak)에서 불러옴</summary>
        RecoveredFromBackup,
        /// <summary>세이브와 백업을 모두 읽지 못함. 읽지 못한 파일은 격리해 두고 null을 돌려준다</summary>
        Unreadable,
    }

    /// <summary>세이브 코드 읽기 결과 (SAVE-01, SaveCode.TryParse)</summary>
    public enum SaveCodeError
    {
        None,
        /// <summary>빈 코드 (null · 공백뿐 — 클립보드가 비었을 때)</summary>
        Empty,
        /// <summary>형식이 틀림 (머리말 · 점 · 체크섬 16진 · base64)</summary>
        BadFormat,
        /// <summary>체크섬이 맞지 않음 (잘렸거나 바뀜)</summary>
        Checksum,
        /// <summary>풀어 보니 4 MB를 넘음</summary>
        TooLarge,
        /// <summary>더 새 세이브 버전에서 만든 코드 (PlayerData.CurrentVersion보다 큼, Sanitize 전에 본다)</summary>
        NewerVersion,
        /// <summary>체크섬은 맞지만 풀거나 세이브로 읽지 못함</summary>
        Corrupt,
    }

    /// <summary>세이브 한 벌 요약 (가져오기 미리보기 · 백업 목록 줄). 기본값 = 빈 요약</summary>
    public struct SaveSummary
    {
        public int highestStage;
        public int shopLevel;
        /// <summary>가진 친구(계보) 수</summary>
        public int lineages;
        /// <summary>마지막 저장 시각 (유닉스 초)</summary>
        public long savedUnix;
        /// <summary>계정 ID 앞 8자 (없으면 빈 문자열)</summary>
        public string accountIdShort;

        /// <summary>계정 ID 앞에서 자르는 길이</summary>
        public const int AccountIdShortLength = 8;

        /// <summary>세이브의 요약을 만든다 (data가 null이면 빈 요약). 읽기만 한다</summary>
        public static SaveSummary From(PlayerData data)
        {
            if (data == null) return default;
            string id = data.accountId ?? string.Empty;
            return new SaveSummary
            {
                highestStage = Math.Max(1, data.battle != null ? data.battle.highestStage : 1),
                shopLevel = Math.Max(1, data.level),
                lineages = data.lineages != null ? data.lineages.Count : 0,
                savedUnix = data.lastSaveUnix,
                accountIdShort = id.Length > AccountIdShortLength ? id.Substring(0, AccountIdShortLength) : id,
            };
        }
    }

    /// <summary>백업 파일 종류 (ISaveStorage.ListBackups)</summary>
    public enum SaveBackupKind
    {
        /// <summary>그 날 첫 저장 때 남긴 날마다 백업 ("{경로}.day-{날 키}.bak", 새것 3개)</summary>
        Daily = 0,
        /// <summary>세이브 코드 가져오기 · 백업 복구 직전 상태 ("{경로}.{이유}-{유닉스}.bak")</summary>
        BeforeReplace = 1,
        /// <summary>저장할 때마다 남는 바로 앞 세이브 ("{경로}.bak")</summary>
        LastSave = 2,
    }

    /// <summary>복구할 수 있는 백업 파일 하나 (ISaveStorage.ListBackups, 새것부터)</summary>
    public sealed class SaveBackupInfo
    {
        /// <summary>백업 파일 전체 경로 (RestoreBackup에 그대로 넘긴다)</summary>
        public string Path;
        /// <summary>목록 이름 ('오늘' / '어제' / '그저께' / 'M월 d일' / '가져오기 전' / '마지막 저장 직전')</summary>
        public string Label;
        /// <summary>그 백업의 저장 시각 (유닉스 초 — 세이브의 lastSaveUnix, 없으면 파일 시각)</summary>
        public long SavedUnix;
        public SaveSummary Summary;
        public SaveBackupKind Kind;
    }

    /// <summary>세이브 저장소. 클라우드 세이브를 붙일 때 이 인터페이스로 구현한다.</summary>
    public interface ISaveStorage
    {
        /// <summary>마지막 Load 결과 (복구/실패 안내용)</summary>
        SaveLoadResult LastLoadResult { get; }

        PlayerData Load();
        void Save(PlayerData data);

        /// <summary>세이브와 이 저장소의 백업(직전 · 날마다 · 바꾸기 전)을 모두 지운다 (데이터 초기화). 격리해 둔 손상 파일만 남긴다</summary>
        void Delete();

        // ── SAVE-01 가져오기 · 백업 (SPEC4 §4.2) ──

        /// <summary>
        /// 지금 세이브를 "{경로}.{reason}-{유닉스}.bak"으로 남긴 뒤 data로 바꿔 저장한다 (세이브 코드 가져오기: reason = "import").
        /// 바꿔 저장했으면 true. 지금 세이브를 백업하지 못하면 바꾸지 않고 false (예외를 내지 않는다 — 부르는 쪽은 false면 다시 불러오지 않는다)
        /// </summary>
        bool Replace(PlayerData data, string reason);

        /// <summary>날 키마다 첫 저장 때 "{경로}.day-{dayKey}.bak"을 남기고 새것 3개만 둔다 (같은 날 두 번째부터는 아무것도 하지 않음)</summary>
        void SnapshotDaily(string dayKey);

        /// <summary>복구할 수 있는 백업 (날마다 · 가져오기 전 · 직전 .bak), 새것부터. 읽지 못한 파일은 뺀다. 없으면 빈 목록</summary>
        IReadOnlyList<SaveBackupInfo> ListBackups();

        /// <summary>지금 상태를 가져오기 백업으로 남기고 path의 백업으로 되돌린다. 못 하면 false (씬 다시 불러오기는 부르는 쪽)</summary>
        bool RestoreBackup(string path);
    }

    /// <summary>
    /// JSON 로컬 파일 저장. 임시 파일에 먼저 쓰고 교체하며, 직전 세이브를 .bak으로 보관한다.
    /// 읽지 못한 파일은 "{경로}.corrupt-{유닉스 시각}"으로 옮겨 두어, 이후 저장이 원본 진행 상황을 덮어쓰지 않게 한다.
    /// SAVE-01: 날마다 백업("{경로}.day-{날 키}.bak", 새것 3개) · 바꾸기 전 백업("{경로}.{이유}-{유닉스}.bak", 이유마다 새것 3개) ·
    /// 목록 · 복구. 새 기능(Replace · SnapshotDaily · ListBackups · RestoreBackup)은 예외를 내지 않고 경고 한 줄로 끝낸다.
    /// </summary>
    public sealed class LocalSaveStorage : ISaveStorage
    {
        /// <summary>날마다 백업을 몇 개 남기는지</summary>
        public const int DailySnapshotsKept = 3;
        /// <summary>바꾸기 전 백업을 이유마다 몇 개 남기는지</summary>
        public const int ReplaceBackupsKept = 3;
        /// <summary>세이브 코드 가져오기 · 백업 복구가 남기는 백업 이유</summary>
        public const string ImportReason = "import";

        public const string LabelToday = "오늘";
        public const string LabelYesterday = "어제";
        public const string LabelTwoDaysAgo = "그저께";
        public const string LabelBeforeImport = "가져오기 전";
        public const string LabelBeforeReplace = "바꾸기 전";
        public const string LabelLastSave = "마지막 저장 직전";

        /// <summary>FlushPending의 기본 대기 한계 (ms — 시험 · 진단). 종료는 GameBootstrap.QuitFlushTimeoutMs</summary>
        public const int DefaultFlushTimeoutMs = 2000;
        /// <summary>FlushPending이 받는 가장 긴 대기 (ms). 음수는 0으로 — 끝없이 기다리지 않는다</summary>
        public const int MaxFlushTimeoutMs = 10000;
        /// <summary>WarmContracts가 따라가는 형식 수 한계 (세이브 형식은 지금 수십 개)</summary>
        internal const int WarmContractsMaxTypes = 512;

        const string DayPrefix = "day-";
        const string BakSuffix = ".bak";
        const string DayKeyFormat = "yyyy-MM-dd";
        /// <summary>Warm이 날 키 확인(DateTime.TryParseExact)을 데울 때 쓰는 값</summary>
        const string WarmDayKey = "2026-01-01";

        /// <summary>
        /// 열거형 키를 모르는 이름으로 저장한 세이브(다른 버전)도 나머지는 읽히게 한다. 세이브 코드 가져오기(SaveCode.TryParse)도 같은 설정으로 읽는다
        /// </summary>
        internal static readonly JsonSerializerSettings ReadSettings = new()
        {
            Converters = { new TolerantEnumKeyDictionaryConverter() },
        };

        readonly string _path;
        readonly ITimeProvider _clock;
        /// <summary>마지막으로 받은 오늘 날 키 (백업 목록 이름 '어제' · '그저께'의 기준). 없으면 기기 시각으로 정한다</summary>
        string _todayKey;

        // ── 백그라운드 자동 저장 (perf PF-3 H1 · L2) ──
        // 직렬화는 메인 스레드에서 (게임이 매 프레임 PlayerData를 바꾼다), 파일 쓰기만 작업 스레드에서. 파일을 만지는 모든 길은 _fileLock 안에서,
        // 저장 번호(_seq)가 더 새 저장보다 오래된 사진을 버린다 — 일시정지 · 종료 동기 저장 · 가져오기(Replace) · 지우기 뒤에 줄 서 있던 자동 저장이 덮지 않게.

        /// <summary>JsonConvert.SerializeObject(data)와 같은 기본 설정 (DefaultSettings → CreateDefault)</summary>
        static readonly JsonSerializer s_serializer = JsonSerializer.CreateDefault();
        /// <summary>직렬화 버퍼 (다시 쓴다 — 5 KB 세이브에서 할당 36.5 KB → 결과 문자열만, L2). 메인 스레드 전용</summary>
        readonly StringBuilder _json = new StringBuilder(16 * 1024);
        /// <summary>줄 서 있는 사진 · 쓰는 중 표시만 지킨다 (아주 짧게)</summary>
        readonly object _queueLock = new object();
        /// <summary>임시 → .bak → 본 파일 순서 · 날마다 백업 · 바꾸기 백업 — 파일을 만지는 쪽만 잡는다</summary>
        readonly object _fileLock = new object();
        /// <summary>메인 스레드가 매긴 마지막 저장 번호</summary>
        long _seq;
        /// <summary>디스크에 있는 가장 새 저장 번호 (_fileLock 안에서만)</summary>
        long _writtenSeq;
        string _queuedJson;
        long _queuedSeq;
        string _queuedDaily;
        bool _running;
        Task _writer;

        string BackupPath => _path + BakSuffix;
        string TempPath => _path + ".tmp";

        public LocalSaveStorage(string path) : this(path, null)
        {
        }

        /// <param name="clock">백업 이름의 유닉스 시각 · 목록 이름의 오늘 (null = 기기 시계)</param>
        public LocalSaveStorage(string path, ITimeProvider clock)
        {
            _path = path;
            _clock = clock;
        }

        public SaveLoadResult LastLoadResult { get; private set; }

        public PlayerData Load()
        {
            lock (_fileLock) return LoadCore();
        }

        PlayerData LoadCore()
        {
            bool mainExists = File.Exists(_path);
            var data = TryRead(_path);
            if (data != null)
            {
                LastLoadResult = SaveLoadResult.Loaded;
                return data;
            }
            // 읽지 못한 최신 세이브가 다음 저장 때 .bak으로 복사되어 정상 백업을 덮지 않도록 먼저 격리한다
            if (mainExists) Quarantine(_path);

            bool backupExists = File.Exists(BackupPath);
            data = TryRead(BackupPath);
            if (data != null)
            {
                LastLoadResult = SaveLoadResult.RecoveredFromBackup;
                return data;
            }
            if (backupExists) Quarantine(BackupPath);

            LastLoadResult = mainExists || backupExists ? SaveLoadResult.Unreadable : SaveLoadResult.NotFound;
            return null;
        }

        /// <summary>
        /// 지금 진행을 바로 쓴다 (일시정지 · 종료 · 설정 화면 · 시험 — 예전과 같은 동기 저장). 쓰는 중인 백그라운드 저장이 있으면 그것이 끝난 뒤 쓰고,
        /// 줄 서 있던 더 오래된 자동 저장은 버린다
        /// </summary>
        public void Save(PlayerData data)
        {
            string json = Serialize(data);
            long seq = ++_seq;
            lock (_fileLock)
            {
                WriteMain(json);
                _writtenSeq = Math.Max(_writtenSeq, seq);
            }
        }

        /// <summary>JsonConvert.SerializeObject(data)와 같은 글자 (같은 기본 설정 · 들여쓰기 없음). 메인 스레드 전용 — 버퍼를 다시 쓴다</summary>
        internal string Serialize(PlayerData data)
        {
            _json.Clear();
            using (var sw = new StringWriter(_json, CultureInfo.InvariantCulture))
            using (var jw = new JsonTextWriter(sw) { Formatting = s_serializer.Formatting })
                s_serializer.Serialize(jw, data);
            return _json.ToString();
        }

        /// <summary>
        /// 첫 직렬화 비용(JIT · 계약 만들기 — 불러온 뒤 30~42 ms, 새 플레이어 190~225 ms, perf 측정 H2)을 부팅 프레임에 미리 낸다.
        /// perf A5(a): 첫 자동 저장의 나머지 첫 비용도 — 날 키 확인(DateTime.TryParseExact)과 작업 스레드(Task.Run 첫 호출 = 스레드 풀 시작).
        /// 작업 스레드는 세이브 형식 계약(지금 비어 있는 목록 · 사전의 항목 형식까지, WarmContracts)을 만들고 끝난다.
        /// GameBootstrap.Awake가 한 번 부른다. 파일은 쓰지 않는다. 예외를 내지 않는다
        /// </summary>
        public void Warm(PlayerData data)
        {
            try
            {
                if (data != null) Serialize(data);
                TryParseDayKey(WarmDayKey, out _);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 예열을 건너뛰어요 (저장에는 영향 없음): {e.Message}");
            }
            WarmWriter();
        }

        /// <summary>
        /// 작업 스레드를 미리 띄운다 (Warm): 계약을 만든 뒤 WriteLoop로 — 그사이 줄 선 자동 저장이 있으면 이어서 쓰고, 없으면 바로 끝난다.
        /// 이미 돌고 있으면 아무것도 하지 않는다. _writer가 되므로 FlushPending(종료)이 이것도 기다린다
        /// </summary>
        void WarmWriter()
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            lock (_queueLock)
            {
                if (_running) return;
                _running = true;
            }
            try
            {
                _writer = Task.Run(WarmThenWriteLoop);
            }
            catch (Exception e)
            {
                lock (_queueLock) _running = false;
                Debug.LogWarning($"[Save] 저장 작업 스레드를 미리 띄우지 못했어요 (첫 자동 저장 때 띄워요): {e.Message}");
            }
#endif
        }

        void WarmThenWriteLoop()
        {
            WarmContracts(typeof(PlayerData));
            WriteLoop();
        }

        /// <summary>
        /// root에서 닿는 모든 형식(속성 · 목록 항목 · 사전 키와 값)의 Json.NET 계약을 미리 만든다 — 부팅 때 비어 있던 목록 · 사전의 항목 형식도
        /// 첫 자동 저장 전에 (perf A5(a)). 계약 캐시는 스레드 안전이라 작업 스레드에서 불러도 된다. 찾은 형식 수를 돌려준다
        /// (maxTypes에서 멈춘다). 예외를 내지 않는다 (경고 한 줄)
        /// </summary>
        internal static int WarmContracts(Type root, int maxTypes = WarmContractsMaxTypes)
        {
            if (root == null || maxTypes <= 0) return 0;
            var seen = new HashSet<Type>();
            var pending = new Stack<Type>();
            pending.Push(root);
            try
            {
                var resolver = s_serializer.ContractResolver;
                while (pending.Count > 0 && seen.Count < maxTypes)
                {
                    var type = pending.Pop();
                    if (type == null || !seen.Add(type)) continue;
                    switch (resolver.ResolveContract(type))
                    {
                        case JsonObjectContract obj:
                            foreach (var property in obj.Properties)
                            {
                                if (!property.Ignored) pending.Push(property.PropertyType);
                            }
                            break;
                        case JsonDictionaryContract dictionary:
                            pending.Push(dictionary.DictionaryKeyType);
                            pending.Push(dictionary.DictionaryValueType);
                            break;
                        case JsonArrayContract array:
                            pending.Push(array.CollectionItemType);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 세이브 형식을 미리 읽지 못했어요 (저장에는 영향 없음): {e.Message}");
            }
            return seen.Count;
        }

        /// <summary>
        /// 자동 저장 (perf PF-3 H1): 지금 진행을 JSON으로 찍고(메인 스레드, PC 0.2~1 ms) 파일 쓰기(임시 → .bak → 본 파일, 5~22 ms · 디스크가 바쁘면
        /// 1초 넘게 — 측정 c 1,364 ms)는 작업 스레드에서. 줄 서 있는 사진은 가장 새것 하나만 남는다. dailyKey가 있으면 쓴 뒤 그 날 첫 백업도 남긴다.
        /// 메인 스레드는 디스크를 기다리지 않는다
        /// </summary>
        public void SaveInBackground(PlayerData data, string dailyKey = null)
        {
            string json = Serialize(data);
            long seq = ++_seq;
            if (dailyKey != null && TryParseDayKey(dailyKey, out _)) _todayKey = dailyKey;
            else dailyKey = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            WriteSnapshot(json, seq, dailyKey); // 스레드가 없다
#else
            lock (_queueLock)
            {
                _queuedJson = json;
                _queuedSeq = seq;
                if (dailyKey != null) _queuedDaily = dailyKey;
                if (_running) return; // 돌고 있는 작업이 이어서 쓴다
                _running = true;
            }
            _writer = Task.Run(WriteLoop);
#endif
        }

        /// <summary>쓰는 중이거나 줄 서 있는 자동 저장이 있는지</summary>
        public bool IsWriting
        {
            get
            {
                var writer = _writer;
                return writer != null && !writer.IsCompleted;
            }
        }

        /// <summary>
        /// 줄 서 있거나 쓰는 중인 자동 저장(부팅 예열 포함)을 timeoutMs까지 기다린다 (보통 몇 ms). 다 끝났거나 없으면 true, 한계를 넘으면 false.
        /// 종료(GameBootstrap.OnApplicationQuit, RI-8)는 500 ms, 시험 · 진단은 기본 2초 — 한계는 멈춤 방지일 뿐, 넘어도 파일 순서는
        /// _fileLock · 저장 번호가 지킨다. timeoutMs는 0~MaxFlushTimeoutMs로 자른다 (음수 = 기다리지 않고 지금 상태). 예외를 내지 않는다
        /// </summary>
        public bool FlushPending(int timeoutMs = DefaultFlushTimeoutMs)
        {
            var writer = _writer;
            if (writer == null || writer.IsCompleted) return true;
            int wait = timeoutMs < 0 ? 0 : timeoutMs > MaxFlushTimeoutMs ? MaxFlushTimeoutMs : timeoutMs;
            try
            {
                return writer.Wait(wait);
            }
            catch (Exception)
            {
                // WriteLoop가 예외를 잡아 경고로 남긴다 — 여기까지 오면 작업은 끝난 것
                return writer.IsCompleted;
            }
        }

        void WriteLoop()
        {
            while (true)
            {
                string json;
                string daily;
                long seq;
                lock (_queueLock)
                {
                    json = _queuedJson;
                    daily = _queuedDaily;
                    seq = _queuedSeq;
                    _queuedJson = null;
                    _queuedDaily = null;
                    // 끝내는 판단을 잠금 안에서 — 그 사이 들어온 사진을 잃지 않는다
                    if (json == null)
                    {
                        _running = false;
                        return;
                    }
                }
                try
                {
                    WriteSnapshot(json, seq, daily);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] 자동 저장을 쓰지 못했어요 (다음 저장 때 다시 써요): {e.Message}");
                }
            }
        }

        /// <summary>사진 하나를 쓴다. 그사이 더 새 저장(동기 저장 · 바꾸기 · 지우기)이 있었으면 본 파일은 건너뛴다 (날마다 백업은 지금 본 파일로)</summary>
        void WriteSnapshot(string json, long seq, string daily)
        {
            lock (_fileLock)
            {
                if (seq > _writtenSeq)
                {
                    WriteMain(json);
                    _writtenSeq = seq;
                }
                if (daily != null) SnapshotDailyCore(daily);
            }
        }

        /// <summary>
        /// 임시 파일에 쓴 뒤 본 파일과 바꾼다 (지금 본 파일은 .bak으로 — 복사 대신 이름 바꾸기: 55 KB에서 17.5 → 5 ms, 남는 파일은 같다).
        /// 중간에 멈추면 본 파일이 없고 .bak = 직전 세이브 — 예전 Delete → Move 사이와 같은 상태이고 Load가 .bak으로 읽는다. _fileLock 안에서만
        /// </summary>
        void WriteMain(string json)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(TempPath, json);
            if (File.Exists(_path))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(_path, BackupPath);
            }
            File.Move(TempPath, _path);
        }

        /// <summary>
        /// 세이브/백업/임시 파일을 지운다 (데이터 초기화 — '모든 진행 상황이 삭제되고 … 되돌릴 수 없어요'). 날마다 · 바꾸기 전 백업도 함께 지워
        /// 초기화 뒤 '백업에서 복구'에 지난 진행이 남지 않게 한다. 격리해 둔 손상 파일(.corrupt-*)만 복구용으로 남긴다.
        /// 백업을 지우지 못하면 경고 한 줄로 넘어간다 (예외 없음)
        /// </summary>
        public void Delete()
        {
            // 줄 서 있던 자동 저장은 버린다 (perf PF-3: 저장 번호 — 초기화 뒤 본 파일을 다시 만들지 않게)
            long seq = ++_seq;
            lock (_fileLock)
            {
                _writtenSeq = Math.Max(_writtenSeq, seq);
                foreach (var path in new[] { _path, BackupPath, TempPath })
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                DeleteBackups();
            }
        }

        /// <summary>이 저장소가 만든 날마다 · 바꾸기 전 백업을 모두 지운다 (Delete). 예외를 내지 않는다</summary>
        void DeleteBackups()
        {
            try
            {
                string dir = SaveDir;
                if (!System.IO.Directory.Exists(dir)) return;
                foreach (var file in System.IO.Directory.GetFiles(dir, FileName + ".*" + BakSuffix))
                {
                    if (TryClassify(file, out var kind, out _) && kind != SaveBackupKind.LastSave) TryDelete(file);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 백업을 지우지 못했어요: {e.Message}");
            }
        }

        // ── SAVE-01 가져오기 · 백업 (SPEC4 §4.2) ──

        public bool Replace(PlayerData data, string reason)
        {
            if (data == null) return false;
            try
            {
                lock (_fileLock)
                {
                    // 지금 진행을 먼저 남긴다. 남기지 못하면 바꾸지 않는다 (진행을 잃지 않게). Save가 저장 번호를 올려 줄 서 있던 자동 저장이 덮지 않는다
                    if (File.Exists(_path)) BackupBeforeReplace(SafeReason(reason));
                    Save(data);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 세이브를 바꾸지 못했어요: {e.Message}");
                return false;
            }
        }

        public void SnapshotDaily(string dayKey)
        {
            if (!TryParseDayKey(dayKey, out _)) return;
            _todayKey = dayKey;
            lock (_fileLock) SnapshotDailyCore(dayKey);
        }

        /// <summary>SnapshotDaily의 파일 일 (_fileLock 안 — 메인 스레드 또는 자동 저장 작업 스레드). 예외를 내지 않는다</summary>
        void SnapshotDailyCore(string dayKey)
        {
            try
            {
                if (!File.Exists(_path)) return;
                string target = DailyPath(dayKey);
                if (File.Exists(target)) return;
                File.Copy(_path, target, false);
                PruneDaily();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 날마다 백업을 남기지 못했어요: {e.Message}");
            }
        }

        public IReadOnlyList<SaveBackupInfo> ListBackups()
        {
            var list = new List<SaveBackupInfo>();
            lock (_fileLock)
            try
            {
                string dir = SaveDir;
                if (!System.IO.Directory.Exists(dir)) return list;
                string todayKey = TodayKey();
                foreach (var file in System.IO.Directory.GetFiles(dir, FileName + "*" + BakSuffix))
                {
                    if (!TryClassify(file, out var kind, out string key)) continue;
                    var data = TryRead(file, quiet: true);
                    if (data == null) continue;
                    long saved = data.lastSaveUnix > 0 ? data.lastSaveUnix : FileUnix(file);
                    list.Add(new SaveBackupInfo
                    {
                        Path = file,
                        Kind = kind,
                        Label = LabelOf(kind, key, todayKey),
                        SavedUnix = saved,
                        Summary = SaveSummary.From(data),
                    });
                }
                list.Sort(CompareNewestFirst);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 백업 목록을 읽지 못했어요: {e.Message}");
            }
            return list;
        }

        public bool RestoreBackup(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                // 이 저장소가 만든 백업만 (다른 경로 · 격리 파일 · 임시 파일은 거절)
                if (!TryClassify(full, out _, out _) || !File.Exists(full)) return false;
                long seq = ++_seq;
                lock (_fileLock)
                {
                    string json = File.ReadAllText(full);
                    if (Parse(json, full, quiet: true) == null) return false;
                    if (File.Exists(_path)) BackupBeforeReplace(ImportReason);
                    WriteMain(json);
                    // 줄 서 있던 자동 저장이 되돌린 세이브를 덮지 않게
                    _writtenSeq = Math.Max(_writtenSeq, seq);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 백업으로 되돌리지 못했어요: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 날마다 백업의 목록 이름: 오늘 날 키와 같으면 '오늘', 하루 전 '어제', 이틀 전 '그저께', 그보다 전은 'M월 d일'
        /// (날 키를 읽지 못하면 그 날 키 그대로)
        /// </summary>
        public static string DayLabel(string dayKey, string todayKey)
        {
            if (!TryParseDayKey(dayKey, out var day)) return dayKey ?? string.Empty;
            if (TryParseDayKey(todayKey, out var today))
            {
                int diff = (int)Math.Round((today - day).TotalDays);
                if (diff == 0) return LabelToday;
                if (diff == 1) return LabelYesterday;
                if (diff == 2) return LabelTwoDaysAgo;
            }
            return $"{day.Month}월 {day.Day}일";
        }

        string SaveDir
        {
            get
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_path));
                return string.IsNullOrEmpty(dir) ? "." : dir;
            }
        }

        string FileName => Path.GetFileName(_path);

        string DailyPath(string dayKey) => _path + "." + DayPrefix + dayKey + BakSuffix;

        long NowUnix => _clock != null ? TimeUtil.ToUnix(_clock.UtcNow) : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>오늘 날 키: SnapshotDaily가 받은 마지막 값, 없으면 (기기 현지 시각 − 05:00)의 날짜 (DailyClock과 같은 규칙)</summary>
        string TodayKey()
        {
            if (!string.IsNullOrEmpty(_todayKey)) return _todayKey;
            var utc = _clock != null ? _clock.UtcNow : DateTime.UtcNow;
            var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
            return local.AddHours(-DailyClock.ResetHour).ToString(DayKeyFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>지금 본 세이브를 "{경로}.{reason}-{유닉스}.bak"으로 복사하고, 그 이유의 백업은 새것 ReplaceBackupsKept개만 둔다. 실패하면 예외</summary>
        void BackupBeforeReplace(string reason)
        {
            string stem = $"{_path}.{reason}-{NowUnix.ToString(CultureInfo.InvariantCulture)}";
            string target = stem + BakSuffix;
            for (int i = 1; File.Exists(target); i++) target = $"{stem}-{i.ToString(CultureInfo.InvariantCulture)}{BakSuffix}";
            File.Copy(_path, target, false);
            PruneReason(reason);
        }

        void PruneDaily()
        {
            var keys = new List<string>();
            foreach (var file in System.IO.Directory.GetFiles(SaveDir, FileName + "." + DayPrefix + "*" + BakSuffix))
            {
                if (TryClassify(file, out var kind, out string key) && kind == SaveBackupKind.Daily) keys.Add(key);
            }
            if (keys.Count <= DailySnapshotsKept) return;
            // 날 키 "yyyy-MM-dd"는 글자 순서가 곧 날짜 순서다
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count - DailySnapshotsKept; i++) TryDelete(DailyPath(keys[i]));
        }

        void PruneReason(string reason)
        {
            var files = new List<(long unix, string path)>();
            foreach (var file in System.IO.Directory.GetFiles(SaveDir, FileName + "." + reason + "-*" + BakSuffix))
            {
                if (TryClassify(file, out var kind, out string key) && kind == SaveBackupKind.BeforeReplace && ReasonOf(key) == reason)
                    files.Add((UnixOf(key), file));
            }
            if (files.Count <= ReplaceBackupsKept) return;
            files.Sort((a, b) => a.unix != b.unix ? a.unix.CompareTo(b.unix) : string.CompareOrdinal(a.path, b.path));
            for (int i = 0; i < files.Count - ReplaceBackupsKept; i++) TryDelete(files[i].path);
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 오래된 백업을 지우지 못했어요: {path}\n{e.Message}");
            }
        }

        /// <summary>
        /// 이 저장소의 백업 파일 이름인지 보고 종류를 정한다. key = 날마다는 날 키, 바꾸기 전은 "{이유}-{유닉스}[-n]", 직전 .bak은 빈 문자열
        /// </summary>
        bool TryClassify(string file, out SaveBackupKind kind, out string key)
        {
            kind = SaveBackupKind.LastSave;
            key = string.Empty;
            if (string.IsNullOrEmpty(file)) return false;
            string full;
            try
            {
                full = Path.GetFullPath(file);
            }
            catch (Exception)
            {
                return false;
            }
            if (!string.Equals(Path.GetDirectoryName(full), SaveDir, StringComparison.OrdinalIgnoreCase)) return false;
            string name = Path.GetFileName(full);
            string main = FileName;
            if (!name.EndsWith(BakSuffix, StringComparison.Ordinal) || !name.StartsWith(main, StringComparison.Ordinal)) return false;
            if (name.Length == main.Length + BakSuffix.Length) return true; // "{이름}.bak"
            if (name[main.Length] != '.') return false;
            string middle = name.Substring(main.Length + 1, name.Length - main.Length - 1 - BakSuffix.Length);
            if (middle.StartsWith(DayPrefix, StringComparison.Ordinal))
            {
                string dayKey = middle.Substring(DayPrefix.Length);
                if (!TryParseDayKey(dayKey, out _)) return false;
                kind = SaveBackupKind.Daily;
                key = dayKey;
                return true;
            }
            if (string.IsNullOrEmpty(ReasonOf(middle)) || UnixOf(middle) <= 0) return false;
            kind = SaveBackupKind.BeforeReplace;
            key = middle;
            return true;
        }

        /// <summary>"{이유}-{유닉스}[-n]"의 이유 (형식이 틀리면 빈 문자열)</summary>
        static string ReasonOf(string key)
        {
            int dash = key.IndexOf('-');
            if (dash <= 0) return string.Empty;
            string reason = key.Substring(0, dash);
            return SafeReason(reason) == reason ? reason : string.Empty;
        }

        /// <summary>"{이유}-{유닉스}[-n]"의 유닉스 초 (형식이 틀리면 0)</summary>
        static long UnixOf(string key)
        {
            int dash = key.IndexOf('-');
            if (dash <= 0) return 0;
            string rest = key.Substring(dash + 1);
            int second = rest.IndexOf('-');
            if (second >= 0)
            {
                if (!int.TryParse(rest.Substring(second + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _)) return 0;
                rest = rest.Substring(0, second);
            }
            return long.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out long unix) ? unix : 0;
        }

        /// <summary>파일 이름에 쓸 수 있는 이유 (영문 소문자 · 숫자 · '_'만, 비거나 틀리면 "import")</summary>
        static string SafeReason(string reason)
        {
            if (string.IsNullOrEmpty(reason) || reason.Length > 24) return ImportReason;
            foreach (char c in reason)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return ImportReason;
            }
            return reason;
        }

        static bool TryParseDayKey(string dayKey, out DateTime day) =>
            DateTime.TryParseExact(dayKey, DayKeyFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

        static string LabelOf(SaveBackupKind kind, string key, string todayKey) => kind switch
        {
            SaveBackupKind.Daily => DayLabel(key, todayKey),
            SaveBackupKind.BeforeReplace => ReasonOf(key) == ImportReason ? LabelBeforeImport : LabelBeforeReplace,
            _ => LabelLastSave,
        };

        /// <summary>새것부터 (저장 시각이 같으면 직전 저장 → 바꾸기 전 → 날마다 순)</summary>
        static int CompareNewestFirst(SaveBackupInfo a, SaveBackupInfo b)
        {
            int byTime = b.SavedUnix.CompareTo(a.SavedUnix);
            if (byTime != 0) return byTime;
            int byKind = ((int)b.Kind).CompareTo((int)a.Kind);
            return byKind != 0 ? byKind : string.CompareOrdinal(a.Path, b.Path);
        }

        static long FileUnix(string file)
        {
            try
            {
                return new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        static PlayerData TryRead(string path, bool quiet = false)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return Parse(File.ReadAllText(path), path, quiet);
            }
            catch (Exception e)
            {
                if (!quiet) Debug.LogWarning($"[Save] 세이브 읽기 실패: {path}\n{e}");
                return null;
            }
        }

        /// <summary>세이브 JSON을 읽고 Sanitize한다 (비었으면 null). 읽기 오류는 예외로 올린다 — quiet면 null</summary>
        static PlayerData Parse(string json, string path, bool quiet)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<PlayerData>(json, ReadSettings);
                if (data == null)
                {
                    if (!quiet) Debug.LogWarning($"[Save] 세이브 내용이 비어 있습니다: {path}");
                    return null;
                }
                data.Sanitize();
                return data;
            }
            catch (Exception) when (quiet)
            {
                return null;
            }
        }

        /// <summary>읽지 못한 파일을 저장 경로 밖 이름으로 옮긴다. 옮기지 못하면 사본이라도 남긴다.</summary>
        static void Quarantine(string path)
        {
            string target = NextQuarantinePath(path);
            try
            {
                File.Move(path, target);
                Debug.LogWarning($"[Save] 읽지 못한 세이브를 보관했습니다: {target}");
            }
            catch (Exception moveError)
            {
                try
                {
                    File.Copy(path, target, false);
                    Debug.LogWarning($"[Save] 읽지 못한 세이브를 옮기지 못해 사본을 보관했습니다: {target}\n{moveError}");
                }
                catch (Exception copyError)
                {
                    Debug.LogWarning($"[Save] 읽지 못한 세이브를 보관하지 못했습니다: {path}\n{copyError}");
                }
            }
        }

        static string NextQuarantinePath(string path)
        {
            string stem = $"{path}.corrupt-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            string candidate = stem;
            for (int i = 1; File.Exists(candidate); i++) candidate = $"{stem}-{i}";
            return candidate;
        }
    }
}
