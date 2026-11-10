using System;

namespace IdleGame
{
    /// <summary>
    /// 백업 알림 (SAVE-01 · 델타 17): 마지막 백업(세이브 코드 내보내기) 뒤 MetaDatabase.backupReminder.days일(날 키)이 지나면
    /// 우편 한 통 (MailSystem, 로컬 — 서버 없음). meta.backup에 마지막 내보내기 · 알림 날 키를 둔다.
    /// <para>
    /// 규칙: days ≤ 0이면 꺼짐. 백업한 적이 없으면 설치한 날부터 센다. 지난 날 수 ≥ days이고, 마지막 알림도 days일 이상 지났으면(또는 없으면)
    /// Mail.SendOnce("backup:" + 오늘 날 키, 제목, 본문) — 날 키로 한 번만, 보상 없음, 빨간 점 없음 (우편함 숫자만). 되돌림 중에는 보내지 않는다.
    /// 부팅(Connect) · 날 바뀜에서 확인한다. 문구는 콘텐츠(P4-8, backupReminder.mailTitle/mailBody), 비어 있으면 기본 문구.
    /// 설정 화면이 세이브 코드를 복사 · 공유한 뒤 MarkBackedUp을 부른다 (P4-4).
    /// </para>
    /// </summary>
    public class BackupReminderSystem : IMetaModule
    {
        /// <summary>콘텐츠가 비었을 때의 우편 제목</summary>
        public const string DefaultMailTitle = "세이브 코드를 챙겨 둘 때예요";
        /// <summary>콘텐츠가 비었을 때의 우편 본문</summary>
        public const string DefaultMailBody = "설정 › 계정·데이터에서 세이브 코드를 복사해 두면 기기를 바꿔도 수선점을 그대로 이어 갈 수 있어요.";
        /// <summary>SendOnce 키 머리 ("backup:" + 날 키)</summary>
        public const string MailKeyPrefix = "backup:";

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly DailyClock _clock;
        readonly MailSystem _mail;

        public BackupReminderSystem(PlayerData data, MetaDatabase db, DailyClock clock, MailSystem mail)
        {
            _data = data;
            _db = db;
            _clock = clock;
            _mail = mail;
        }

        BackupSave Save => (_data.meta ??= new MetaSave()).backup ??= new BackupSave();

        int Days => _db?.backupReminder != null ? _db.backupReminder.days : 0;

        /// <summary>알림이 켜져 있는지 (backupReminder.days &gt; 0)</summary>
        public bool IsEnabled => Days > 0 && _clock != null;

        /// <summary>마지막 백업 뒤 지난 날 수 (날 키 기준, 백업한 적이 없으면 설치한 날부터). 시계가 없으면 0</summary>
        public int DaysSinceBackup
        {
            get
            {
                if (_clock == null) return 0;
                string from = Save.lastExportDayKey;
                if (string.IsNullOrEmpty(from)) from = _data.meta?.clock?.installDayKey;
                if (string.IsNullOrEmpty(from)) return 0;
                return Math.Max(0, DailyClock.DaysBetween(from, _clock.DayKey));
            }
        }

        /// <summary>세이브 코드를 내보냈다 (복사 · 공유 뒤 설정 화면이 부른다) — 오늘 날 키를 백업한 날로 적는다</summary>
        public void MarkBackedUp()
        {
            if (_clock == null) return;
            Save.lastExportDayKey = _clock.DayKey;
        }

        /// <summary>지금 알림 우편을 보내야 하는지 (상태를 바꾸지 않음)</summary>
        public bool IsReminderDue
        {
            get
            {
                if (!IsEnabled || _mail == null || _clock.IsTampered) return false;
                int days = Days;
                if (DaysSinceBackup < days) return false;
                string last = Save.lastReminderDayKey;
                return string.IsNullOrEmpty(last) || DailyClock.DaysBetween(last, _clock.DayKey) >= days;
            }
        }

        // ── IMetaModule ──

        public void Connect() => CheckReminder();

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
        }

        public void OnDayChanged(string dayKey) => CheckReminder();

        public void Tick(float unscaledDeltaTime)
        {
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            // 빨간 점 없음 (조용한 알림 — 우편함 숫자만)
        }

        /// <summary>때가 됐으면 알림 우편을 한 통 보낸다 (날 키로 한 번)</summary>
        void CheckReminder()
        {
            if (!IsReminderDue) return;
            string today = _clock.DayKey;
            var cfg = _db.backupReminder;
            string title = string.IsNullOrEmpty(cfg.mailTitle) ? DefaultMailTitle : cfg.mailTitle;
            string body = string.IsNullOrEmpty(cfg.mailBody) ? DefaultMailBody : cfg.mailBody;
            _mail.SendOnce(MailKeyPrefix + today, title, body);
            Save.lastReminderDayKey = today;
        }
    }
}
