namespace IdleGame.EditorTools
{
    /// <summary>
    /// Q-03 수선점 출석부. 하루 한 번 도장이 새싹(7일, 한 번만)과 정규(28일, 반복)에 함께 찍힌다. 누적 일수라 빠진 날이 있어도 이어진다.
    ///   · 새싹 7일: 별조각 1,000 + 수선권·재료, 7일째 루즈리(영웅) 확정 — 시작 친구와 조합 2개(화장대의 비밀: 똑단이,
    ///     열쇠와 자물쇠: 찰칵이)가 바로 켜지는 "7일째 돌아올 강한 이유". 이미 있으면 MetaRewardType.Lineage가 기억편 20으로 바꾼다.
    ///   · 정규 28일: 평일 칸은 단추 N시간치 → 재료 → 별조각 50을 돌아가며, 주마다 조금씩 커진다.
    ///     7일 별조각 200 / 14일 선택 기억편 30 / 21일 달빛 조각 2 / 28일 별조각 500 + 오르골 태엽 2 (큰 칸).
    /// 담당: M2.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillAttendance(MetaDatabase db, GameContent content)
        {
            var a = db.attendance;

            // ── 새싹 출석 7일 ──
            a.Add(Day(AttendanceTrack.Starter, 1, Gem(300)));
            a.Add(Day(AttendanceTrack.Starter, 2, MetaReward.QuickIdleTicket(3)));
            a.Add(Day(AttendanceTrack.Starter, 3, Mat(MaterialIds.Cloth, 15), Mat(MaterialIds.Ribbon, 10)));
            a.Add(Day(AttendanceTrack.Starter, 4, Gem(300)));
            a.Add(Day(AttendanceTrack.Starter, 5, Mat(MaterialIds.Thread, 5)));
            a.Add(Day(AttendanceTrack.Starter, 6, Gem(400)));
            a.Add(Big(AttendanceTrack.Starter, 7, "루즈리 확정!", MetaReward.Lineage(StarterDay7Lineage)));

            // ── 정규 출석 28일 (1주: 단추 1~2시간 / 2주: 2~3시간 / 3주: 2~3시간 / 4주: 3~4시간) ──
            var t = AttendanceTrack.Monthly;
            a.Add(Day(t, 1, GoldHours(1)));
            a.Add(Day(t, 2, Mat(MaterialIds.Cloth, 10)));
            a.Add(Day(t, 3, Gem(50)));
            a.Add(Day(t, 4, GoldHours(2)));
            a.Add(Day(t, 5, Mat(MaterialIds.Lace, 5)));
            a.Add(Day(t, 6, Gem(50)));
            a.Add(Big(t, 7, null, Gem(200)));

            a.Add(Day(t, 8, GoldHours(2)));
            a.Add(Day(t, 9, Mat(MaterialIds.Ribbon, 15)));
            a.Add(Day(t, 10, Gem(50)));
            a.Add(Day(t, 11, GoldHours(3)));
            a.Add(Day(t, 12, Mat(MaterialIds.Wax, 10)));
            a.Add(Day(t, 13, Gem(50)));
            a.Add(Big(t, 14, null, MetaReward.SelectShards(30)));

            a.Add(Day(t, 15, GoldHours(2)));
            a.Add(Day(t, 16, Mat(MaterialIds.Thread, 3)));
            a.Add(Day(t, 17, Gem(50)));
            a.Add(Day(t, 18, GoldHours(3)));
            a.Add(Day(t, 19, Mat(MaterialIds.Rib, 5)));
            a.Add(Day(t, 20, Gem(50)));
            a.Add(Big(t, 21, null, Mat(MaterialIds.Moonlight, 2)));

            a.Add(Day(t, 22, GoldHours(3)));
            a.Add(Day(t, 23, Mat(MaterialIds.Cloth, 20)));
            a.Add(Day(t, 24, Gem(50)));
            a.Add(Day(t, 25, GoldHours(4)));
            a.Add(Day(t, 26, Mat(MaterialIds.Thread, 5)));
            a.Add(Day(t, 27, Gem(50)));
            a.Add(Big(t, 28, "한 바퀴 완성!", Gem(500), Mat(MaterialIds.Spring, 2)));
        }

        /// <summary>새싹 출석 7일째 확정 친구 (영웅 루즈리)</summary>
        const string StarterDay7Lineage = "rouge";

        static AttendanceDayDef Day(AttendanceTrack track, int day, params MetaReward[] rewards) => new()
        {
            track = track,
            day = day,
            rewards = Rewards(rewards),
        };

        /// <summary>큰 칸 (7일째 친구 확정, 정규 7/14/21/28일)</summary>
        static AttendanceDayDef Big(AttendanceTrack track, int day, string label, params MetaReward[] rewards)
        {
            var def = Day(track, day, rewards);
            def.highlight = true;
            def.label = label;
            return def;
        }
    }
}
