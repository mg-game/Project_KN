using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>
    /// 메타 세션 (MetaSession): 부팅 순서(설치 날·접속 카운터·비운 시간 → 바구니·복귀 화면·출석부 요청), 새 게임 자동 전투 끄기
    /// (건너뛴 뒤 자동 전투가 열리면 다시 켜기), 일시정지/복귀(시계 되돌림 악용 막기·같은 공백 한 번만·짧은 복귀에 출석부 다시 안 띄움·
    /// 일시정지 중 프레임이 비운 시간을 깎거나 날 바뀜을 먼저 쓰지 않음),
    /// 접속 중 날 바뀜, 빨간 점 등록·갱신(편성 빈칸, HUD 토글 제외, 던전 입장권 05:00), 옛 세이브 조용한 개방, 접속 묶음 안전장치.
    /// </summary>
    public class MetaSessionTests : MetaSystemsTestBase
    {
        readonly List<long> _returned = new();

        /// <summary>NUnit은 픽스처 인스턴스 하나로 모든 테스트를 돌린다 — 기록은 테스트마다 비운다</summary>
        [SetUp]
        public void ClearReturned() => _returned.Clear();

        void Listen() => MetaSession.Returned += away => _returned.Add(away);

        void SetupAttendanceDays()
        {
            MetaDb.attendance = new List<AttendanceDayDef>
            {
                new AttendanceDayDef { track = AttendanceTrack.Starter, day = 1, rewards = new List<MetaReward> { MetaReward.Gem(300) } },
                new AttendanceDayDef { track = AttendanceTrack.Monthly, day = 1, rewards = new List<MetaReward> { MetaReward.Gem(50) } },
                new AttendanceDayDef { track = AttendanceTrack.Monthly, day = 2, rewards = new List<MetaReward> { MetaReward.Gem(50) } },
            };
        }

        [Test]
        public void Start_SetsInstallDay_CountsLoginOncePerDay()
        {
            SetUtc(2026, 11, 4, 9, 0);
            Listen();

            MetaSession.Start();
            MetaSession.Start();

            Assert.IsTrue(MetaSession.IsStarted);
            Assert.AreEqual("2026-11-04", Data.meta.clock.installDayKey);
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.Login));
            Assert.AreEqual(1, _returned.Count, "두 번째 Start는 무시");
            Assert.AreEqual(0, _returned[0], "기록이 없으면 비운 시간 0");

            SetUtc(2026, 11, 5, 5, 0);
            MetaSession.Tick(1.1f);
            Assert.AreEqual(1, Progress.GetDaily(CounterKey.Login), "접속 중 날이 바뀌면 새 날 접속");
            Assert.AreEqual(2, Progress.GetLifetime(CounterKey.Login));
        }

        [Test]
        public void Start_AwayTime_FillsBasket_AndQueuesReturnScreen()
        {
            SetUtc(2026, 11, 4, 9, 0);
            long now = NowUnix;
            Data.lastSaveUnix = now - 2 * 3600;
            Data.meta.clock.lastSessionEndUnix = now - 3 * 3600;
            Listen();

            MetaSession.Start();

            Assert.AreEqual(2 * 3600, MetaSession.LastAwaySeconds, "max(세션 끝, 마지막 저장) 기준");
            CollectionAssert.AreEqual(new[] { 2L * 3600 }, _returned);
            Assert.AreEqual(System.TimeSpan.FromHours(2), IdleChest.OfflineElapsed, "비운 시간 → 단추 줄");
            Assert.IsTrue(Popups.InStartupBatch);
            Assert.IsTrue(Popups.TryPeek(out var request));
            Assert.AreEqual(MetaPopupKind.Return, request.Kind);
            Assert.AreEqual(2 * 3600, request.Value);
        }

        [Test]
        public void Start_ShortAway_OnlyReturnsWithoutScreen()
        {
            SetUtc(2026, 11, 4, 9, 0);
            Data.lastSaveUnix = NowUnix - 120;
            Listen();

            MetaSession.Start();

            CollectionAssert.AreEqual(new[] { 120L }, _returned, "짧은 복귀는 토스트용 알림만");
            Assert.IsFalse(Popups.Contains(MetaPopupKind.Return));
        }

        [Test]
        public void Start_TamperedClock_HasNoAwayTime()
        {
            SetUtc(2026, 11, 4, 9, 0);
            Data.lastSaveUnix = NowUnix - 5 * 3600;
            Data.meta.clock.maxSeenUnix = NowUnix + 3600;

            MetaSession.Start();

            Assert.IsTrue(DailyClock.IsTampered);
            Assert.AreEqual(0, MetaSession.LastAwaySeconds);
            Assert.AreEqual(System.TimeSpan.Zero, IdleChest.OfflineElapsed);
        }

        [Test]
        public void Start_QueuesAttendance_WhenStampAvailable()
        {
            SetupAttendanceDays();
            Rebuild();

            MetaSession.Start();

            Assert.IsTrue(Popups.Contains(MetaPopupKind.Attendance));
            MetaSession.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TopAttendance), "오늘 도장 점");

            Attendance.CheckIn();
            MetaSession.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.TopAttendance));
        }

        [Test]
        public void FreshTutorial_TurnsAutoBattleOff_FinishedDoesNot()
        {
            MetaDb.ftueSteps = new List<FtueStepDef>
            {
                new FtueStepDef { id = "intro", text = "intro", complete = MetaCondition.Flag(MetaFlags.IntroSeen), action = FtueAction.StoryIntro },
            };
            Rebuild();
            Battle.AutoMode = true;

            MetaSession.Start();

            Assert.IsFalse(Battle.AutoMode, "새 게임은 직접 해 본 뒤 자동 전투 (G13)");
        }

        void SetupTutorialWithAutoBattleGate()
        {
            MetaDb.ftueSteps = new List<FtueStepDef>
            {
                new FtueStepDef { id = "intro", text = "intro", complete = MetaCondition.Flag(MetaFlags.IntroSeen), action = FtueAction.StoryIntro },
            };
            AddFeature(FeatureId.AutoBattle, MetaCondition.StageCleared(1, 2));
            Rebuild();
            Battle.AutoMode = true;
        }

        [Test]
        public void SkippedTutorial_TurnsAutoBattleOn_WhenItUnlocks()
        {
            SetupTutorialWithAutoBattleGate();
            MetaSession.Start();
            Assert.IsFalse(Battle.AutoMode);

            Tutorial.Skip(); // 설정에서 1-2 전에 건너뜀 (자동 전투는 아직 잠김)
            Assert.IsFalse(Features.IsUnlocked(FeatureId.AutoBattle));
            Assert.IsFalse(Battle.AutoMode);

            SetCleared(2); // 1-2 클리어 → 자동 전투 열림
            Progress.MarkDirty();
            MetaSession.Tick(0.016f);

            Assert.IsTrue(Features.IsUnlocked(FeatureId.AutoBattle));
            Assert.IsTrue(Battle.AutoMode, "건너뛴 뒤 자동 전투가 열리면 다시 켠다 (G13 끄기를 되돌림)");
        }

        [Test]
        public void RunningTutorial_LeavesAutoBattleToFtueStep()
        {
            SetupTutorialWithAutoBattleGate();
            MetaSession.Start();

            SetCleared(2);
            Progress.MarkDirty();
            MetaSession.Tick(0.016f);

            Assert.IsTrue(Features.IsUnlocked(FeatureId.AutoBattle));
            Assert.IsFalse(Battle.AutoMode, "튜토리얼 중에는 8단계(FtueAction.EnableAutoBattle)가 켠다");
        }

        [Test]
        public void FinishedTutorial_KeepsAutoBattle()
        {
            MetaDb.ftueSteps = new List<FtueStepDef>
            {
                new FtueStepDef { id = "intro", text = "intro", complete = MetaCondition.Flag(MetaFlags.IntroSeen) },
            };
            Rebuild();
            Data.meta.ftue.done = true;
            Battle.AutoMode = true;

            MetaSession.Start();

            Assert.IsTrue(Battle.AutoMode);
        }

        [Test]
        public void PauseAndResume_AddAwayTime_AndOpenBatchOnlyWhenNeeded()
        {
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            Popups.EndStartupBatch();
            Listen();

            MetaSession.OnPause();
            Assert.AreEqual(NowUnix, Data.meta.clock.lastSessionEndUnix);
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(30);
            MetaSession.OnResume();
            CollectionAssert.AreEqual(new[] { 30L }, _returned);
            Assert.IsFalse(Popups.InStartupBatch, "띄울 것이 없으면 접속 묶음을 열지 않는다");

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            Advance(1);
            MetaSession.OnResume();

            Assert.AreEqual(3600, _returned[1]);
            Assert.AreEqual(System.TimeSpan.FromSeconds(30 + 3600), IdleChest.OfflineElapsed, "비운 시간이 쌓인다");
            Assert.IsTrue(Popups.InStartupBatch, "복귀 화면이 있으면 접속 묶음");
            Assert.IsTrue(Popups.Contains(MetaPopupKind.Return));
        }

        [Test]
        public void OnQuit_RecordsSessionEnd()
        {
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            Advance(0.5);
            MetaSession.OnQuit();
            Assert.AreEqual(NowUnix, Data.meta.clock.lastSessionEndUnix);
        }

        [Test]
        public void ClockRolledBackThenRestored_DoesNotCountRollbackAsAway()
        {
            SetUtc(2026, 11, 4, 9, 0);
            long realNow = NowUnix;
            MetaSession.Start();
            Popups.EndStartupBatch();
            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix; // GameBootstrap.Save (기기 시각)

            // 1) 기기 시계를 하루 전으로 돌리고 복귀: 되돌림이라 비운 시간 0
            Advance(-24);
            MetaSession.OnResume();
            Assert.IsTrue(DailyClock.IsTampered);
            Assert.AreEqual(0, MetaSession.LastAwaySeconds);

            // 2) 되돌린 채로 나감: 저장 시각은 기기 시각(하루 전)으로 찍히지만 세션 끝은 유효 시각
            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            Assert.AreEqual(realNow, Data.meta.clock.lastSessionEndUnix, "세션 끝은 되돌린 시각이 아니라 유효 시각");

            // 3) 시계를 원래대로(+1분) 돌리고 복귀: 하루가 아니라 1분만 비운 것
            SetUtc(2026, 11, 4, 9, 1);
            MetaSession.OnResume();

            Assert.IsFalse(DailyClock.IsTampered);
            Assert.AreEqual(60, MetaSession.LastAwaySeconds, "되돌림 왕복으로 바구니를 채울 수 없다");
            Assert.AreEqual(System.TimeSpan.FromSeconds(60), IdleChest.OfflineElapsed);
            Assert.IsFalse(Popups.Contains(MetaPopupKind.Return));
        }

        [Test]
        public void Start_AwayFloor_IncludesMaxSeen()
        {
            SetUtc(2026, 11, 4, 9, 0);
            // 예전 판에서 되돌린 시계로 저장된 세이브: 세션 끝·저장은 하루 전이지만 실행 중 본 가장 늦은 시각은 1분 전
            Data.lastSaveUnix = NowUnix - 24 * 3600;
            Data.meta.clock.lastSessionEndUnix = NowUnix - 24 * 3600;
            Data.meta.clock.maxSeenUnix = NowUnix - 60;

            MetaSession.Start();

            Assert.AreEqual(60, MetaSession.LastAwaySeconds, "플레이어가 되돌릴 수 없는 maxSeen이 바닥");
            Assert.AreEqual(NowUnix, Data.meta.clock.lastSessionEndUnix, "쓴 공백은 표시해 둔다");
        }

        [Test]
        public void ResumeTwiceWithoutPause_CountsGapOnce()
        {
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            Popups.EndStartupBatch();
            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            Advance(1);

            MetaSession.OnResume();
            Assert.AreEqual(3600, MetaSession.LastAwaySeconds);
            AdvanceSeconds(5);
            MetaSession.OnResume(); // 일시정지 없이 복귀가 한 번 더 옴

            Assert.AreEqual(5, MetaSession.LastAwaySeconds, "같은 공백을 다시 세지 않는다");
            Assert.AreEqual(System.TimeSpan.FromSeconds(3605), IdleChest.OfflineElapsed);
        }

        [Test]
        public void PausedTick_DoesNotRaiseMaxSeen_AwayKeepsBackgroundTime()
        {
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            Popups.EndStartupBatch();
            Listen();
            long seenAtPause = NowUnix;

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix; // GameBootstrap.Save
            // 백그라운드에서도 프레임이 도는 경우 (runInBackground · 에디터): 80시간 뒤 1.5초짜리 프레임 하나
            Advance(80);
            MetaSession.Tick(1.5f);
            Assert.AreEqual(seenAtPause, Data.meta.clock.maxSeenUnix, "일시정지 중에는 시계를 돌리지 않는다 (RI-7)");

            MetaSession.OnResume();

            Assert.AreEqual(80 * 3600, MetaSession.LastAwaySeconds, "백그라운드 80시간이 그대로 비운 시간");
            CollectionAssert.AreEqual(new[] { 80L * 3600 }, _returned);
            Assert.AreEqual(NowUnix, Data.meta.clock.maxSeenUnix, "복귀의 Refresh가 maxSeen을 올린다");

            AdvanceSeconds(30);
            MetaSession.Tick(1.1f);
            Assert.AreEqual(NowUnix, Data.meta.clock.maxSeenUnix, "복귀 뒤에는 Tick이 다시 시계를 돌린다");
        }

        [Test]
        public void ShortResume_DoesNotReopenClosedAttendance_LongResumeDoes()
        {
            SetupAttendanceDays();
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            Assert.IsTrue(Popups.TryDequeue(false, false, out var shown));
            Assert.AreEqual(MetaPopupKind.Attendance, shown.Kind);
            Popups.EndStartupBatch(); // 도장 없이 닫음

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(5); // 잠금 화면 · 앱 전환 · 로그인/권한 창
            MetaSession.OnResume();

            Assert.IsFalse(Popups.Contains(MetaPopupKind.Attendance), "잠깐 나갔다 오면 출석부를 다시 띄우지 않는다");
            Assert.IsFalse(Popups.InStartupBatch);
            MetaSession.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TopAttendance), "빨간 점으로만 알린다");

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(20 * 60); // 오래 비움 = 새 접속
            MetaSession.OnResume();

            Assert.IsTrue(Popups.InStartupBatch);
            Assert.IsTrue(Popups.Contains(MetaPopupKind.Attendance));
        }

        [Test]
        public void ShortResumeAcrossDailyReset_ShowsAttendance()
        {
            SetupAttendanceDays();
            Rebuild();
            SetUtc(2026, 11, 5, 4, 59);
            MetaSession.Start();
            Assert.IsTrue(Popups.TryDequeue(false, false, out _));
            Assert.IsNotNull(Attendance.CheckIn());
            Popups.EndStartupBatch();

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(90); // 05:00:30 — 짧지만 날이 바뀜
            MetaSession.OnResume();

            Assert.IsTrue(Popups.InStartupBatch, "날이 바뀐 복귀는 새 접속으로 본다");
            Assert.IsTrue(Popups.TryDequeue(false, false, out var request));
            Assert.AreEqual(MetaPopupKind.Attendance, request.Kind);
        }

        [Test]
        public void ShortResumeAcrossDailyReset_PausedTick_StillShowsAttendance()
        {
            SetupAttendanceDays();
            Rebuild();
            SetUtc(2026, 11, 5, 4, 59);
            MetaSession.Start();
            Assert.IsTrue(Popups.TryDequeue(false, false, out _));
            Assert.IsNotNull(Attendance.CheckIn());
            Popups.EndStartupBatch();

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(90); // 05:00:30 — 짧지만 날이 바뀜
            // 백그라운드에서도 프레임이 도는 경우 (runInBackground · 에디터): 일시정지 중 Tick이 날 바뀜을 먼저 써 버리면
            // 복귀의 Refresh가 날 바뀜을 못 보고 짧은 복귀라 출석부가 안 뜬다 (RI-7)
            MetaSession.Tick(1.5f);
            MetaSession.OnResume();

            Assert.IsTrue(Popups.InStartupBatch, "일시정지 중 프레임이 있어도 날 바뀜은 복귀의 Refresh가 알린다");
            Assert.IsTrue(Popups.TryDequeue(false, false, out var request));
            Assert.AreEqual(MetaPopupKind.Attendance, request.Kind);
        }

        [Test]
        public void DayChangeDuringSession_QueuesAttendanceForNextStageBreak()
        {
            SetupAttendanceDays();
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            MetaSession.Start();
            Assert.IsTrue(Popups.TryDequeue(false, false, out var first));
            Assert.AreEqual(MetaPopupKind.Attendance, first.Kind);
            Assert.IsNotNull(Attendance.CheckIn());
            Popups.EndStartupBatch();

            SetUtc(2026, 11, 5, 5, 0);
            MetaSession.Tick(1.1f);

            Assert.IsTrue(Popups.Contains(MetaPopupKind.Attendance));
            Assert.IsFalse(Popups.TryDequeue(false, false, out _), "전투 중에는 기다린다");
            Assert.IsTrue(Popups.TryDequeue(true, false, out var second));
            Assert.AreEqual(MetaPopupKind.Attendance, second.Kind);
        }

        [Test]
        public void RedDots_RegisteredAndRefreshedByTick()
        {
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            Rebuild();
            MetaSession.Start();
            MetaSession.Tick(0.016f);

            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.TopJournal);
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.TopMail);
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.HudIdleChest);
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.LineageEvolution("sock"));
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.ExpeditionZone("exp_1_1"));
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.FeatureDot(FeatureId.Gacha));
            Assert.AreEqual(0, RedDots.GetCount(RedDotPaths.TopMail));

            Mail.Send("선물", "선물", new[] { RewardData.Currency(CurrencyType.Gem, 10) });
            MetaSession.Tick(0.016f);
            Assert.AreEqual(1, RedDots.GetCount(RedDotPaths.TopMail), "우편은 숫자");

            Assert.IsFalse(RedDots.IsOn(RedDotPaths.TabGacha));
            SetCleared(3);
            Progress.MarkDirty();
            MetaSession.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TabGacha), "새로 열린 기능 점 (탭까지)");
            Features.MarkSeen(FeatureId.Gacha);
            MetaSession.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.TabGacha), "들어가 보면 꺼진다");
        }

        /// <summary>Game.SetupMeta처럼 편성·(필요하면 일일 시계를 쓰는) 던전을 넘긴 세션</summary>
        MetaSession CreateFullSession(DungeonSystem dungeon = null) =>
            new MetaSession(Data, MetaDb, DailyClock, Progress, Power, RedDots, Popups, Features, GuideQuest, Missions, Attendance, IdleChest,
                Tutorial, MetaRewards, Battle, Notice, Mail, Expedition, dungeon ?? Dungeon, Evolution, Characters, Costumes, Formation);

        [Test]
        public void RedDot_FormationEmpty_WhenSlotEmptyAndIdleFriendOwned()
        {
            AddFeature(FeatureId.Formation, MetaCondition.StageCleared(1, 3));
            Rebuild();
            SetUtc(2026, 11, 4, 9, 0);
            var session = CreateFullSession();
            session.Start();
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.FormationEmpty);

            Characters.Grant("sock");
            session.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.FormationEmpty), "편성이 잠겨 있으면 꺼짐");

            SetCleared(3);
            Progress.MarkDirty();
            session.Tick(0.016f);
            Features.MarkSeen(FeatureId.Formation);
            session.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.FormationEmpty), "빈칸 + 편성에 없는 친구");
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TabFormation), "편성 탭까지 올라간다");

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base"));
            session.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.FormationEmpty), "모든 보유 친구가 편성에 있음 (Formation.Changed)");

            Characters.Grant("candle");
            session.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.FormationEmpty), "새 친구 (Characters.Changed)");

            // 원정 중인 친구도 편성할 수 있으므로(M05) 점은 그대로 — 원정은 이 점과 무관하다
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "candle" }));
            session.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.FormationEmpty), "원정 중인 친구도 빈칸에 설 수 있다 (M05)");
            Assert.IsTrue(Formation.IsDispatched("candle"));

            Assert.AreEqual(PlaceResult.Ok, Formation.TryPlace(CharacterRole.Echo, 0, "candle_base"), "원정 중에도 편성된다");
            session.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.FormationEmpty), "모든 보유 친구가 편성에 있음 (원정 중 포함)");
        }

        /// <summary>
        /// 옛 세이브 (M05 전에는 원정을 보내려면 편성에서 빼야 했다): 빈칸 + 편성에 없는 친구가 모두 원정 중이어도 점이 켜진다.
        /// 원정이 끝나 돌아와도 결과는 같다 — 원정은 이 점을 바꾸지 않는다.
        /// </summary>
        [Test]
        public void RedDot_FormationEmpty_CountsDispatchedFriends_ForMigratedSaves()
        {
            Rebuild();
            Characters.Grant("sock");
            Characters.Grant("candle");
            Assert.IsTrue(Expedition.TryStart("exp_1_1", new[] { "sock", "candle" }));
            var session = CreateFullSession();
            session.Start();
            session.Tick(0.016f);

            Assert.IsTrue(Formation.IsDispatched("sock"));
            Assert.IsFalse(Formation.IsLineagePlaced("sock"));
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.FormationEmpty), "빈칸 + 원정 중인(편성에 없는) 친구");
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TabFormation));

            Formation.AutoFormation();
            session.Tick(0.016f);
            Assert.IsTrue(Formation.IsLineagePlaced("sock"), "자동 편성도 원정 중인 친구를 넣는다");
            Assert.IsTrue(Formation.IsLineagePlaced("candle"));
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.FormationEmpty));
        }

        [Test]
        public void RedDot_FormationEmpty_OffWhenEverySlotIsFilled()
        {
            Rebuild();
            var session = CreateFullSession();
            session.Start();
            Characters.Grant("sock");
            Characters.Grant("candle");
            Characters.Grant("button");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            session.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.FormationEmpty));

            // 칸을 모두 채우면 편성에 없는 친구가 있어도 끔 (지킴이 5칸·잔향이 5칸을 같은 데이터로 채움)
            for (int i = 0; i < FormationSystem.GuardianSlotCount; i++) Data.formation.guardians[i] = "sock_base";
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++) Data.formation.echoes[i] = "button_base";
            RedDots.MarkDirty(RedDotPaths.FormationEmpty);
            session.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.FormationEmpty), "빈칸이 없으면 끔");
        }

        [Test]
        public void RedDots_HudToggles_HaveNoNewFeatureDot()
        {
            AddFeature(FeatureId.AutoBattle, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.Speed2x, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.LampPolicy, MetaCondition.StageCleared(1, 2));
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 2));
            Rebuild();
            MetaSession.Start();

            CollectionAssert.DoesNotContain(RedDots.Paths, RedDotPaths.FeatureDot(FeatureId.AutoBattle));
            CollectionAssert.DoesNotContain(RedDots.Paths, RedDotPaths.FeatureDot(FeatureId.Speed2x));
            CollectionAssert.DoesNotContain(RedDots.Paths, RedDotPaths.FeatureDot(FeatureId.LampPolicy));
            CollectionAssert.Contains(RedDots.Paths, RedDotPaths.FeatureDot(FeatureId.Gacha));

            SetCleared(2);
            Progress.MarkDirty();
            MetaSession.Tick(0.016f);

            Assert.IsTrue(Features.IsNew(FeatureId.AutoBattle), "기능은 새로 열림 (잠금 덮개가 첫 탭에 지운다)");
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.HudAuto), "HUD 토글에는 점이 없다 (M09)");
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.HudSpeed));
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.HudLamp));
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TabGacha), "탭 기능은 그대로");
            Assert.IsTrue(MetaSession.IsHudToggle(FeatureId.Speed2x));
            Assert.IsFalse(MetaSession.IsHudToggle(FeatureId.AutoChallenge));
        }

        [Test]
        public void RedDot_DungeonTickets_RefillAt0500DayKey()
        {
            SetUtc(2026, 11, 4, 4, 0); // UTC 시간대 = 날 키 2026-11-03
            var dungeon = new DungeonSystem(Data, BattleCfg, Characters, Formation, Synergy, Rewards, Clock, DailyClock);
            var session = CreateFullSession(dungeon);
            Data.dungeon.highestFloor = 1;
            Data.dungeon.ticketDate = "2026-11-03";
            Data.dungeon.ticketsUsed = dungeon.DailyTickets;
            session.Start();
            session.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.DungeonTickets), "오늘 입장권을 다 씀");

            SetUtc(2026, 11, 4, 4, 59, 58);
            session.Tick(1.1f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.DungeonTickets), "새벽 5시 전 (자정은 이미 지남)");

            SetUtc(2026, 11, 4, 5, 0, 1);
            session.Tick(1.1f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.DungeonTickets), "날 키가 바뀌면 다시 채워져 점");
        }

        [Test]
        public void RedDot_ResonanceReady_ShowsUnderCharacters()
        {
            AddFeature(FeatureId.Costumes, MetaCondition.StageCleared(2, 5)); // 의상 점(밤 방문 조건)이 기기 시간대에 흔들리지 않게
            AddFeature(FeatureId.Codex, MetaCondition.StageCleared(1, 10)); // 도감 받기 점(P4-1, 친구들 탭 아래)이 끼지 않게
            Rebuild();
            MetaSession.Start();
            Characters.Grant("sock");
            MetaSession.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.TabCharacters));

            Characters.AddShards("sock", 1000);
            MetaSession.Tick(0.016f);
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.LineageResonance("sock")));
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.TabCharacters));
        }

        [Test]
        public void LegacySave_BootsWithSilentUnlocks()
        {
            AddFeature(FeatureId.Gacha, MetaCondition.StageCleared(1, 3));
            AddFeature(FeatureId.Shop, MetaCondition.StageCleared(1, 8));
            Rebuild();
            Data.version = 2;
            Data.meta = null;
            Data.battle.highestStage = 6;
            Data.Sanitize();
            Rebuild();

            MetaSession.Start();

            Assert.IsTrue(Features.IsUnlocked(FeatureId.Gacha));
            Assert.IsFalse(Features.IsUnlocked(FeatureId.Shop));
            Assert.IsFalse(Popups.Contains(MetaPopupKind.FeatureUnlock), "옛 세이브는 카드 없이");
            Assert.IsTrue(Data.meta.ftue.done);
        }

        [Test]
        public void Tick_FlushesProgressAndRefreshesPower()
        {
            MetaSession.Start();
            MetaSession.Tick(0.016f); // 첫 전투력 = 기준값
            int progressChanged = 0;
            int powerChanged = 0;
            Progress.Changed += () => progressChanged++;
            Power.Changed += (_, _) => powerChanged++;

            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            MetaSession.Tick(0.016f);

            Assert.AreEqual(1, progressChanged);
            Assert.AreEqual(1, powerChanged);
            MetaSession.Tick(0.016f);
            Assert.AreEqual(1, progressChanged, "바뀐 것이 없으면 조용");
        }

        [Test]
        public void StartupBatch_ClosesAfterTimeout()
        {
            MetaSession.Start();
            Assert.IsTrue(Popups.InStartupBatch);

            for (int i = 0; i < 125; i++) MetaSession.Tick(1f);

            Assert.IsFalse(Popups.InStartupBatch, "발표자가 닫지 못해도 세션 모달이 막히지 않게");
        }

        [Test]
        public void TickBeforeStart_DoesNothing()
        {
            Assert.DoesNotThrow(() => MetaSession.Tick(0.1f));
            Assert.IsFalse(MetaSession.IsStarted);
            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.Login));
        }

        // ───────────── 4차 모듈 (SPEC4 §2.4) ─────────────

        /// <summary>부른 차례를 적는 모듈 (BeforeAwayApplied 때 바구니 단추 줄 · AfterStartup 때 큐 상태도 적는다)</summary>
        sealed class RecordingModule : IMetaModule
        {
            readonly MetaSessionTests _owner;
            public readonly List<string> Calls = new();
            public int Ticks;
            public bool ThrowOnTick;
            public MetaPopupRequest? EnqueueOnAfterStartup;

            public RecordingModule(MetaSessionTests owner) => _owner = owner;

            public void Connect() => Calls.Add("Connect");

            public void BeforeAwayApplied(long awaySeconds, bool boot) =>
                Calls.Add($"BeforeAway:{awaySeconds}:{boot}:offline={_owner.Data.meta.idleChest.offlineSeconds}");

            public void AfterStartup(bool boot)
            {
                Calls.Add($"AfterStartup:{boot}:return={_owner.Popups.Contains(MetaPopupKind.Return)}");
                if (EnqueueOnAfterStartup.HasValue) _owner.Popups.Enqueue(EnqueueOnAfterStartup.Value);
            }

            public void OnDayChanged(string dayKey) => Calls.Add("Day:" + dayKey);

            public void Tick(float unscaledDeltaTime)
            {
                if (ThrowOnTick) throw new System.InvalidOperationException("모듈 오류 시험");
                Ticks++;
            }

            public void RegisterRedDots(RedDotSystem dots) => Calls.Add("Dots");
        }

        [Test]
        public void Modules_CalledInContractOrder_OnStart()
        {
            SetUtc(2026, 11, 4, 9, 0);
            long now = NowUnix;
            Data.lastSaveUnix = now - 2 * 3600;
            var module = new RecordingModule(this);
            MetaSession.AddModule(module);
            MetaSession.AddModule(module); // 두 번 넣어도 하나

            MetaSession.Start();

            CollectionAssert.AreEqual(new[]
            {
                "Connect",
                "BeforeAway:7200:True:offline=0", // 바구니에 비운 시간을 넣기 전
                "AfterStartup:True:return=True",  // 접속 요청(복귀 화면)을 넣은 뒤
                "Dots",
            }, module.Calls);
            Assert.AreEqual(System.TimeSpan.FromHours(2), IdleChest.OfflineElapsed);

            MetaSession.Tick(0.016f);
            Assert.AreEqual(1, module.Ticks);
        }

        [Test]
        public void Modules_ResumeAndDayChange()
        {
            SetUtc(2026, 11, 4, 9, 0);
            var module = new RecordingModule(this);
            MetaSession.AddModule(module);
            MetaSession.Start();
            Popups.EndStartupBatch();
            module.Calls.Clear();

            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(30);
            MetaSession.OnResume();
            CollectionAssert.AreEqual(new[] { "BeforeAway:30:False:offline=0", "AfterStartup:False:return=False" }, module.Calls);
            Assert.IsFalse(Popups.InStartupBatch, "모듈이 접속 요청을 넣지 않으면 지금처럼 묶음을 열지 않는다");

            module.Calls.Clear();
            SetUtc(2026, 11, 5, 5, 0);
            MetaSession.Tick(1.1f);
            CollectionAssert.Contains(module.Calls, "Day:2026-11-05");
        }

        [Test]
        public void Modules_StartupRequestOnResume_OpensBatch()
        {
            SetUtc(2026, 11, 4, 9, 0);
            var module = new RecordingModule(this);
            MetaSession.AddModule(module);
            MetaSession.Start();
            Popups.EndStartupBatch();

            module.EnqueueOnAfterStartup = MetaPopupRequest.Comeback();
            MetaSession.OnPause();
            Data.lastSaveUnix = NowUnix;
            AdvanceSeconds(30);
            MetaSession.OnResume();

            Assert.IsTrue(Popups.InStartupBatch, "모듈이 넣은 접속 종류(다시 만난 날)는 접속 묶음으로");
            Assert.IsTrue(Popups.TryDequeue(false, false, out var request));
            Assert.AreEqual(MetaPopupKind.Comeback, request.Kind);
        }

        [Test]
        public void Modules_StartRetriedAfterFailure_ConnectsEachModuleOnce()
        {
            SetUtc(2026, 11, 4, 9, 0);
            var early = new RecordingModule(this);
            MetaSession.AddModule(early);
            // Start가 모듈 Connect 뒤(접속 카운터)에서 끊기게 한다 — IsStarted 전이라 다음 OnResume이 Start를 다시 부른다
            bool throwOnce = true;
            Progress.Incremented += (key, _) =>
            {
                if (key != CounterKey.Login || !throwOnce) return;
                throwOnce = false;
                throw new System.InvalidOperationException("Start 중단 시험");
            };

            Assert.Throws<System.InvalidOperationException>(() => MetaSession.Start());
            Assert.IsFalse(MetaSession.IsStarted);
            var late = new RecordingModule(this);
            MetaSession.AddModule(late); // 끊긴 Start와 다시 부른 Start 사이에 넣은 모듈

            MetaSession.OnResume();

            Assert.IsTrue(MetaSession.IsStarted);
            Assert.AreEqual(1, early.Calls.FindAll(c => c == "Connect").Count, "이미 이은 모듈은 다시 Connect하지 않는다 (두 번 구독 금지)");
            Assert.AreEqual(1, late.Calls.FindAll(c => c == "Connect").Count, "그 사이 넣은 모듈은 다시 부른 Start가 잇는다");
        }

        [Test]
        public void Start_AfterV4Migration_TopsUpPlayMinutes_ToTodaysInstallDays()
        {
            // 설치 9/1, 마지막 세션 9/2 (둘째 날까지만 하고 쉼) → 마이그레이션은 60분, 11/5에 돌아오면 설치 66일째 → 상한 270 (CR4-24)
            Data.meta.ftue.done = true;
            Data.meta.clock.installDayKey = "2026-09-01";
            Data.meta.clock.lastDayKey = "2026-09-02";
            Data.meta.MigrateToV4();
            Assert.AreEqual(2 * MetaSave.PlayMinutesSeedPerDay, Progress.GetLifetime(CounterKey.PlayMinutes));
            Assert.IsTrue(Data.meta.playMinutesSeedPending);

            SetUtc(2026, 11, 5, 12, 0);
            MetaSession.Start();

            Assert.AreEqual(66, DailyClock.InstallDay);
            Assert.AreEqual(MetaSave.PlayMinutesSeedCap, Progress.GetLifetime(CounterKey.PlayMinutes), "오늘까지 센 설치 일수 (InstallDays 조건과 같은 뜻)");
            Assert.IsFalse(Data.meta.playMinutesSeedPending, "한 번만");
        }

        [Test]
        public void Start_WithoutMigration_DoesNotSeedPlayMinutes()
        {
            Data.meta.ftue.done = true;
            Data.meta.clock.installDayKey = "2026-09-01";
            SetUtc(2026, 11, 5, 12, 0);

            MetaSession.Start();

            Assert.AreEqual(0, Progress.GetLifetime(CounterKey.PlayMinutes), "4차로 옮긴 세이브만 채운다 (새 세이브 · 이미 버전 4)");
        }

        [Test]
        public void Modules_ThrowingModuleIsDisabled_OthersKeepRunning()
        {
            var broken = new RecordingModule(this) { ThrowOnTick = true };
            var healthy = new RecordingModule(this);
            MetaSession.AddModule(broken);
            MetaSession.AddModule(healthy);
            MetaSession.Start();

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("모듈 오류 시험"));
            Assert.DoesNotThrow(() => MetaSession.Tick(0.016f));
            Assert.DoesNotThrow(() => MetaSession.Tick(0.016f));

            Assert.IsTrue(MetaSession.IsModuleDisabled(broken));
            Assert.IsFalse(MetaSession.IsModuleDisabled(healthy));
            Assert.AreEqual(2, healthy.Ticks, "다른 모듈 · 세션은 계속 돈다");
        }

        [Test]
        public void Meta4Stubs_AreRegistered_AndInert()
        {
            Assert.AreEqual(13, MetaSession.Modules.Count, "Game.SetupMeta4와 같은 13개 모듈 (쿠폰 · 반딧불 · 강해지기는 모듈이 아님)");
            Assert.AreSame(Codex, MetaSession.Modules[0], "넣은 순서 = SPEC4 §2.4 표 순서");
            Assert.AreSame(Cheer, MetaSession.Modules[11]);
            Assert.AreSame(BackupReminder, MetaSession.Modules[12], "백업 알림은 응원 다음 (RM-2 SB-1)");

            SetUtc(2026, 11, 4, 9, 0);
            Data.lastSaveUnix = NowUnix - 4 * 24 * 3600; // 다시 만난 날 조건(72시간)을 넘겨도 빈 콘텐츠면 아무것도 하지 않는다
            Assert.DoesNotThrow(() => MetaSession.Start());
            for (int i = 0; i < 5; i++) Assert.DoesNotThrow(() => MetaSession.Tick(1f));
            Assert.DoesNotThrow(() => MetaSession.OnPause());
            Assert.DoesNotThrow(() => MetaSession.OnResume());
            foreach (var module in MetaSession.Modules) Assert.IsFalse(MetaSession.IsModuleDisabled(module), module.GetType().Name);

            Assert.IsFalse(Popups.Contains(MetaPopupKind.Comeback));
            Assert.IsFalse(Popups.Contains(MetaPopupKind.EventStart));
            Assert.IsFalse(Popups.Contains(MetaPopupKind.ChapterChest));
            Assert.IsFalse(Popups.Contains(MetaPopupKind.StoryPage));
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.JournalRecords));
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.Codex));
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.ShopFree));

            // 빈 콘텐츠(MetaDatabase.CreateEmpty) = 꺼짐: 0 · 빈 목록 · null (예외 없음). 기능 정의가 없으면 열림 (FeatureGateSystem 규칙)
            Assert.IsTrue(Codex.IsUnlocked, "정의 없음 = 열림");
            Assert.AreEqual(0, Codex.ClaimableCount());
            Assert.IsNull(Codex.TryClaim(CodexSystem.Key(CodexCategory.Friend, "sock_base")));
            Assert.AreEqual("form:sock_base", CodexSystem.Key(CodexCategory.Friend, "sock_base"));
            Assert.AreEqual("synergymax:s1", CodexSystem.SynergyMaxKey("s1"));
            var team = new StatBlock();
            Codex.AddCollectionBonus(team);
            Assert.IsTrue(team.IsZero);
            Assert.IsTrue(Achievements.IsUnlocked, "정의 없음 = 열림");
            Assert.IsNull(Achievements.TryClaim("ach_stage_1"));
            Assert.IsEmpty(Achievements.ClaimAll());
            Assert.AreEqual(0, GachaMilestones.Pulls);
            Assert.IsNull(GachaMilestones.TryUseFriendTicket("sock"));
            Assert.AreEqual(1, Affinity.GetLevel("sock"));
            Assert.IsFalse(Affinity.TryPet("sock", out _, out bool gained));
            Assert.IsFalse(gained);
            Assert.IsFalse(ChapterChests.TryRollLostChest(5, new List<RewardData>()));
            Assert.IsNull(ChapterChests.OpenChapterChest(1));
            Assert.IsNull(Story.Get(1));
            Assert.AreEqual(1.0, Events.ExpeditionDropMultiplier);
            Assert.AreEqual(0, Events.BonusDungeonTickets);
            Assert.AreEqual(1.0, Events.StageCandyMultiplier);
            Assert.AreEqual(0.0, Events.NightBonusHours(0, 3600));
            Assert.IsFalse(Season.IsUnlocked);
            Assert.IsFalse(Comeback.IsActive);
            Assert.IsNull(Comeback.TryClaimToday());
            Assert.AreEqual(CouponResult.Invalid, Coupons.TryRedeem("ABCDEF"));
            Assert.IsFalse(StarterWeek.IsActive);
            Assert.IsFalse(DailyGift.CanClaim);
            Assert.IsEmpty(DailyGift.Today);
            Assert.IsFalse(Cheer.CanUse);
            Assert.IsFalse(Cheer.TryUse());
            Assert.AreEqual(0, BackupReminder.DaysSinceBackup);
            Assert.DoesNotThrow(() => BackupReminder.MarkBackedUp());
            Assert.IsNull(Data.meta.backup.lastReminderDayKey, "설치한 날 = 백업한 지 0일 → 알림 우편 없음");
            Assert.IsFalse(Fireflies.CanSpawn(false, false, false));
            Assert.IsNull(Fireflies.TryCatch());
            var rows = new List<StrengthenRow> { new StrengthenRow() };
            Strengthen.GetRows(rows);
            Assert.IsEmpty(rows);
            var plan = new List<PlannedNotification> { new PlannedNotification() };
            NotificationPlanner.Plan(new NotificationContext(), plan);
            Assert.IsEmpty(plan);
            Assert.AreEqual(NotificationPermission.Unknown, LocalNotifications.Permission, "백엔드 없음 (에디터)");
        }

        [Test]
        public void Meta4Hooks_ConnectedOnStart_ButInert()
        {
            MetaSession.Start();

            Assert.IsNotNull(Characters.FeatureResolver);
            Assert.IsNotNull(Evolution.FeatureResolver);
            Assert.IsNotNull(Costumes.FeatureResolver);
            Assert.IsNotNull(Dungeon.FeatureResolver);
            Assert.IsTrue(Dungeon.FeatureResolver(FeatureId.Dungeon), "정의 없음 = 열림");
            Assert.AreEqual(0, Dungeon.TicketBankCap(), "콘텐츠 0 = 모아 두기 끔");
            Assert.IsNotNull(Expedition.FeatureResolver, "모두 다시 보내기 잠금 (F-03, P4-2)");
            Assert.IsTrue(Expedition.IsBatchOpen, "정의 없음 = 열림");
            Assert.IsNotNull(Battle.ProtectionResolver);
            Assert.IsFalse(Battle.ProtectionResolver(1).Enabled, "튜토리얼 단계가 없으면 보호 없음");
            Assert.AreEqual(LampPolicy.Auto, Battle.LampPolicy);
            Assert.AreEqual(1f, Battle.PresentationScale);
            Assert.IsNull(Battle.ClearDropHook, "잃어버린 상자는 꺼짐 (확장, G3에 켤지 정함)");
            Assert.IsNotNull(Battle.TempAttackPercent, "응원(P4-1)이 훅을 건다");
            Assert.AreEqual(0f, Battle.TempAttackPercent(), 1e-6f, "응원 꺼짐 = 0");
            Assert.IsNotNull(Synergy.ExtraTeamModifiers, "도감 모음 보너스 (P4-1)");
        }

        [Test]
        public void RedDot_DungeonBlessing_ShowsWhilePickPending()
        {
            MetaDb.blessings = new List<BlessingDef>
            {
                new BlessingDef { id = "b_attack", name = "b_attack", desc = "b_attack", stat = StatType.AttackPercent, value = 15 },
                new BlessingDef { id = "b_shield", name = "b_shield", desc = "b_shield", stat = StatType.ShieldPowerPercent, value = 20 },
                new BlessingDef { id = "b_crit", name = "b_crit", desc = "b_crit", stat = StatType.CritChancePercent, value = 8 },
            };
            Data.dungeon.highestFloor = 3;
            MetaSession.Start(); // 이벤트 모듈 Connect가 축복 표(MetaDb.blessings)를 던전에 건다
            MetaSession.Tick(0.016f);
            Assert.AreEqual(1, Dungeon.BlessingPicksPending, "3층 돌파 = 고르기 1번");
            Assert.IsTrue(RedDots.IsOn(RedDotPaths.DungeonBlessing));

            var offer = Dungeon.GetBlessingOffer();
            Assert.IsTrue(Dungeon.TryPickBlessing(offer[0]));
            MetaSession.Tick(0.016f);
            Assert.IsFalse(RedDots.IsOn(RedDotPaths.DungeonBlessing), "고르면 꺼짐 (던전 Changed → DungeonTickets 마디가 자식 잎까지 다시 본다)");
        }
    }
}
