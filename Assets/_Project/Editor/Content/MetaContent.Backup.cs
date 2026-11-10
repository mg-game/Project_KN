namespace IdleGame.EditorTools
{
    /// <summary>
    /// SAVE-01 백업 알림 (델타 17 · RM-2 SB-1): 세이브 코드를 내보내거나 공유한 뒤 날 키 14개가 지나도록 다시 하지 않으면 우편 한 통
    /// (규칙 BackupReminderSystem — P4-2, 우편 MailSystem). 우편은 보상 없이 안내만 한다. days ≤ 0 = 끔.
    /// 문구는 날 수도 "이미 저장했다"도 말하지 않는다: 한 번도 내보내지 않은 세이브는 설치한 날부터 세고(첫 우편은 백업한 적 없는 사람에게 간다),
    /// 뒤 우편은 마지막 알림부터 다시 14일마다 가므로 "저장한 지 14일"은 대부분 틀린 말이 된다.
    /// 설정 경로는 SPEC4 §2.12 (설정 › 계정·데이터).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>마지막 백업(없으면 설치한 날) 뒤 알림 우편까지 날 수 · 다음 알림까지 날 수</summary>
        internal const int BackupReminderDays = 14;

        static partial void FillBackup(MetaDatabase db, GameContent content)
        {
            db.backupReminder = new BackupReminderConfig
            {
                days = BackupReminderDays,
                mailTitle = "세이브 코드를 한 번 챙겨 둘까요?",
                mailBody = "세이브 코드를 한동안 챙기지 않았어요.\n" +
                           "설정 › 계정·데이터에서 세이브 코드를 복사해 두면, 기기를 바꿔도 수선점을 그대로 이어 갈 수 있어요.",
            };
        }
    }
}
