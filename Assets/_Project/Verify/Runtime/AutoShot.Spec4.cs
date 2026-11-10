using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.Verify
{
    /// <summary>
    /// SPEC4 §11.4 장면 (RM-2 V2 — PHASE4-ROADMAP RM-2 plan §5.3). 플레이어를 -autoshot &lt;폴더&gt; -autoshot-spec4 &lt;시나리오&gt;로 띄우면 새 세이브에서
    /// 그 시나리오만 돈다 (전체 판의 번호를 밀지 않는다 — 실행마다 따로). 시나리오: ftue · early · chapter · calendar · comeback · idle · soak.
    /// 장면 아이디는 "S4-&lt;SPEC4 번호&gt;" (UX-SPEC §5.2의 "S-xx"와 섞이지 않게), 줄 모양은 전체 판과 같다 ("[Verify] PASS|FAIL|SKIP|INFO 아이디 장면: …").
    /// <para>
    /// 아직 들어오지 않은 기능(형식 · 이름 · 콘텐츠가 없음)과 꺼 둔 스트레치(콘텐츠 0 — plan §3.2 #17)는 FAIL이 아니라 SKIP과 이유(담당 묶음)를 적는다.
    /// V2 part B (RM-2 끝): 들어온 기능의 §11.4 단언을 모두 잰다 — 그림을 늘리지 않는 덧검사는 "-retry · -due · -last · -bgm · -tap · -reward · hud-stats" 아이디로 (그림 번호 그대로).
    /// </para>
    /// <para>
    /// 시드 (in-run): FinishFtue(인트로 넘김 + 튜토리얼 건너뛰기) · SeedCleared(n)(최고 스테이지 = n+1, 조용한 기능 동기화 — 카드 없음) ·
    /// ForceOutcome(실제 클리어 · 실패 — BattleSimulator.Clear/Fail, 비공개) · AwayFor(일시정지 → 시계 앞으로 → 복귀, MetaSession.OnResume 전).
    /// 시계: -autoshot-spec4가 있으면 BeforeSceneLoad에 GameBootstrap.ClockOverride = 옮길 수 있는 시계 (calendar = 이번 달 3일 12:00 현지,
    /// -autoshot-clock yyyy-MM-ddTHH:mm 현지로 바꿀 수 있다). 시드 세이브: -autoshot-fixture &lt;파일&gt;이 지운 세이브 자리에 복사된다 (AutoShot.UseScratchSave).
    /// </para>
    /// <para>
    /// -autoshot-migrate + -autoshot-save &lt;v3 복사본&gt; (G1_prep §3 B): 옛 세이브 부팅 판 — MIG-1 v4로 읽힘 · 메타 켜짐 · 최고 스테이지 그대로,
    /// 60초 전투 + 성장 시트 · 수선 일지, MIG-2 모듈 꺼짐 · [Meta] · 예외 줄 0, MIG-3 다시 저장 = v4 · codex 있음 · 다시 읽어도 두 번 옮기지 않음.
    /// </para>
    /// 게임이 남기는 "[Verify] 키=값" 줄(first_lamp_beat · sleep_budget · kill_without_arrival …)은 WatchGameLog가 모아 검사에 쓴다.
    /// </summary>
    public partial class AutoShot
    {
        // ───────────── 명령줄 ─────────────

        const string Spec4Arg = "-autoshot-spec4";
        const string MigrateArg = "-autoshot-migrate";
        const string FixtureArg = "-autoshot-fixture";
        const string SaveSeedArg = "-autoshot-save";
        /// <summary>시계를 이 현지 시각으로 (yyyy-MM-ddTHH:mm) — calendar의 '3일'보다 먼저 본다</summary>
        const string ClockArg = "-autoshot-clock";
        /// <summary>soak 길이 (분, 기본 20)</summary>
        const string SoakMinutesArg = "-autoshot-soak-minutes";
        /// <summary>P4-3b가 읽는 개발 플래그 (반딧불을 바로 띄운다 — 이 도구는 있는지만 본다)</summary>
        const string ForceFireflyArg = "-forceFirefly";

        /// <summary>시나리오 이름 (plan §5.3 표 순서)</summary>
        public static readonly string[] Spec4Scenarios = { "ftue", "early", "chapter", "calendar", "comeback", "idle", "soak" };

        /// <summary>calendar 시나리오의 날 (이번 달 3일 — SPEC4 §11.4 31 '달의 3일') · 시각 (현지 12시 — 05:00 초기화와 멀게)</summary>
        const int CalendarDay = 3;
        const int CalendarHour = 12;
        /// <summary>comeback: 비운 시간 (SPEC4 §11.4 33 '−80 h')</summary>
        const float ComebackHours = 80f;
        /// <summary>idle: 잠들기를 기다리는 상한 (초 — 기본 잠자기 2분 + 여유)</summary>
        const float IdleSleepWait = 210f;
        /// <summary>
        /// 22c 응원 칩 자리 (16:9 화면 — 다시 만든 장면 836~948 × 440~488, P4-3b · BattleFx.HudKeepOutPaths 'CheerChip'. SPEC4 §11.4의 872~984 × 500~548은
        /// IA 틀 전 숫자 — 기록만) · 다른 칸과 띄움
        /// </summary>
        static readonly Rect CheerChipZone = Rect.MinMaxRect(836f, 440f, 948f, 488f);
        const float CheerChipClearance = 8f;
        /// <summary>BattleFx가 비키는 HUD 칸 이름 (응원 칩 — BattleFx.HudKeepOutPaths, internal이라 이름으로 읽는다)</summary>
        const string CheerChipKeepOut = "CheerChip";
        /// <summary>창이 다 열렸다고 볼 루트 CanvasGroup 알파 · 기다리는 상한 (초 — UIPopup 여는 연출 ≈ 0.4초)</summary>
        const float PopupShownAlpha = 0.99f;
        const float PopupShownWait = 1.2f;
        /// <summary>37 고리(DefeatAnalysisView._group) · 33 다시 만난 날 카드(루트 CanvasGroup)가 다 나타날 때까지 기다리는 상한 (초 — FadeIn 0.2초 · 여는 연출 ≈ 0.4초, V2 r1 fix)</summary>
        const float ViewShownWait = 1.0f;
        /// <summary>36 어둡게 덮기 누름(RequestDismiss) → 받기 연출(≈ 1.1초) 뒤 1장 상자가 연 것으로 적힐 때까지 기다리는 상한 (초)</summary>
        const float ChestClaimWait = 3f;
        /// <summary>34 지금 판을 끝낸(스테이지 사이) 뒤 이야기 쪽이 뜰 때까지 기다리는 상한 (초)</summary>
        const float StoryNextStageWait = 6f;
        /// <summary>SPEC4 §11.4 23c 첫 주 날 칩 7 · 상자 5, 35 지도 마디 10, 33 도장 칸 3 · 바구니 상한 24시간</summary>
        const int StarterWeekDayChips = 7;
        const int StarterWeekChests = 5;
        const int ChapterMapNodes = 10;
        const int ComebackCells = 3;
        const string ComebackCapText = "/ 24시간";
        /// <summary>04c 큰 FTUE 배너 제목 조각 (BattleView.ShowLampBanner Celebrate '수선점에 불이 밝혀졌어요!') · 큰 배너로 볼 폭 (860의 0.8 — 작은 리본은 516)</summary>
        const string FirstLampBannerPart = "불이 밝혀졌";
        static readonly float FirstLampBannerMinWidth = BattleBannerView.BannerSize.x * 0.8f;
        /// <summary>07b 보스 단서 줄이 떠 보일 때까지 기다리는 상한 (BossHintRule.Delay 1초 + 들어옴 0.3초 + 여유)</summary>
        const float BossHintWait = 3f;
        /// <summary>07b 단서 줄이 '보인다'로 칠 띠 알파</summary>
        const float BossHintAlpha = 0.5f;
        /// <summary>36 첫 1-10 클리어 뒤 챕터 상자 · 이야기 쪽을 기다리는 상한 (초)</summary>
        const float ChapterPopupWait = 12f;
        /// <summary>37 '바로 다시 도전 ›'을 누른 뒤 다음 판이 시작해야 하는 상한 (초 — SPEC4 §11.4 37 updated)</summary>
        const float RetryRestartMax = 0.4f;
        /// <summary>SPEC4 §11.4 정적 줄: StatsButton ↔ 지킴이 받침 ≥ 16, 응원 칩 ↔ 받침 ≥ 8</summary>
        const float StatsPadClearance = 16f;
        /// <summary>34 이야기 쪽 세 칸이 다 보일 때까지 기다리는 상한 (초 — 칸마다 자동 1.2초 + 나타나기 0.25초)</summary>
        const float StoryRevealWait = 6f;
        /// <summary>20c 10회 공개가 끝날 때까지 지켜보는 상한 (초)</summary>
        const float GachaFinishWait = 8f;
        /// <summary>40 잠자기 배경음 덕 (AudioEngine.SleepBgmDb — 잠 곡이 없을 때 버스를 −9 dB)</summary>
        const string SleepBgmDuckDb = "-9";
        /// <summary>42 반딧불 띠 (StrayFireflyView.BandTop/BandBottom ± 흔들림 Amplitude — 부모 기준 y)</summary>
        const float FireflyWait = 12f;
        /// <summary>42 반딧불이 옆에서 들어와 틀 기둥 안에 다 들어올 때까지 (RI-5 (RM-3 P4-9) — 140px/s, 화면 밖 60px에서 시작)</summary>
        const float FireflyInsideWait = 4f;

        static string s_seedPath;
        static bool s_seedCopied;
        static ShiftClock s_clock;

        string _spec4;
        bool _migrate;
        /// <summary>ForceOutcome이 결과를 실제로 만들었는지 (끝난 판 사이로 넘어감)</summary>
        bool _forced;
        /// <summary>S4-40이 잠든 길 (저절로 n초 · SleepNow — 검사 줄에 남긴다)</summary>
        string _sleepPath = string.Empty;

        // 로그 지켜보기 (OnLog → WatchGameLog)
        int _errorLines;
        int _exceptionLines;
        int _metaLines;
        int _killSwitchLines;
        /// <summary>게임 경고 줄 (OnLog — LogType.Warning, 이 도구의 '[Verify] ' 줄은 뺀다) · 모으는 상한 (soak에서 끝없이 늘지 않게)</summary>
        readonly List<string> _warnings = new();
        const int MaxWarnings = 512;
        readonly Dictionary<string, string> _gameValues = new();

        /// <summary>옮길 수 있는 시계 (UtcNow = 기기 시계 + Offset) — GameBootstrap.ClockOverride로 넣는다</summary>
        sealed class ShiftClock : ITimeProvider
        {
            public TimeSpan Offset;
            public DateTime UtcNow => DateTime.UtcNow + Offset;
        }

        /// <summary>SPEC4 시나리오면 장면이 깨어나기 전에 시계를 넣는다 (Game.Setup · DailyClock이 이 시계를 쓴다)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void UseSpec4Clock()
        {
            s_clock = null;
            if (Arg("-autoshot") == null) return;
            string scenario = Arg(Spec4Arg);
            string at = Arg(ClockArg);
            if (scenario == null && at == null) return;
            var clock = new ShiftClock();
            if (at != null && DateTime.TryParseExact(at, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
                clock.Offset = local.ToUniversalTime() - DateTime.UtcNow;
            else if (scenario == "calendar") clock.Offset = OffsetToLocal(CalendarDay, CalendarHour);
            s_clock = clock;
            GameBootstrap.ClockOverride = clock;
        }

        /// <summary>지금 → 이번 달 day일 hour시 (현지)까지의 차이</summary>
        static TimeSpan OffsetToLocal(int day, int hour)
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, Mathf.Clamp(day, 1, DateTime.DaysInMonth(now.Year, now.Month)), hour, 0, 0, DateTimeKind.Local);
            return target.ToUniversalTime() - DateTime.UtcNow;
        }

        /// <summary>
        /// 게임 로그 지켜보기: [Meta] 줄 · 킬 스위치 줄 · 경고 · 게임이 남긴 "[Verify] 키=값" 줄(이 도구의 PASS/FAIL/SKIP/INFO · 요약 줄은 뺀다)
        /// </summary>
        void WatchGameLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (line.StartsWith("[Meta]", StringComparison.Ordinal))
            {
                _metaLines++;
                if (line.Contains("끕니다")) _killSwitchLines++;
            }
            if (!line.StartsWith("[Verify] ", StringComparison.Ordinal)) return;
            string rest = line.Substring("[Verify] ".Length);
            if (rest.StartsWith("PASS ", StringComparison.Ordinal) || rest.StartsWith("FAIL ", StringComparison.Ordinal) ||
                rest.StartsWith("SKIP ", StringComparison.Ordinal) || rest.StartsWith("INFO ", StringComparison.Ordinal) || rest.Contains("요약")) return;
            foreach (var token in rest.Split(' '))
            {
                int eq = token.IndexOf('=');
                if (eq > 0 && eq < token.Length - 1) _gameValues[token.Substring(0, eq).TrimEnd('<', '>')] = token.Substring(eq + 1);
            }
        }

        // ═════════════════════════ 시나리오 ═════════════════════════

        IEnumerator RunSpec4()
        {
            yield return Wait(3f);
            Bind();
            var local = Game.DailyClock != null ? Game.DailyClock.ToLocal(Game.DailyClock.NowUnix) : DateTime.Now;
            Record("INFO", "S4-env", $"spec4 {_spec4}: 화면 {Screen.width}x{Screen.height} · 캔버스 {CanvasWidth:0.#}x{CanvasHeight:0.#} · 시계 {local:yyyy-MM-dd HH:mm} " +
                                     $"(옮김 {(s_clock != null ? s_clock.Offset.TotalHours : 0d):0.##}시간) · 날 키 {Game.DailyClock?.DayKey} · 메타 {Game.IsMetaActive} · " +
                                     $"반딧불 플래그 {HasFlag(ForceFireflyArg)}");
            if (!Game.IsReady || _tabs == null)
            {
                Check("S4-run", false, $"spec4 {_spec4}: 게임 · 탭이 준비되지 않았다");
                yield break;
            }
            switch (_spec4)
            {
                case "ftue": yield return Safe("ftue", Spec4Ftue()); break;
                case "early": yield return Safe("early", Spec4Early()); break;
                case "chapter": yield return Safe("chapter", Spec4Chapter()); break;
                case "calendar": yield return Safe("calendar", Spec4Calendar()); break;
                case "comeback": yield return Safe("comeback", Spec4Comeback()); break;
                case "idle": yield return Safe("idle", Spec4Idle()); break;
                case "soak": yield return Safe("soak", Spec4Soak()); break;
                default:
                    Check("S4-run", false, $"모르는 시나리오 '{_spec4}' (있는 것: {string.Join(" · ", Spec4Scenarios)})");
                    break;
            }
            Record("INFO", "S4-log", $"spec4 {_spec4}: 오류 줄 {_errorLines} · 예외 {_exceptionLines} · [Meta] {_metaLines} · 킬 스위치 {_killSwitchLines} · 게임 [Verify] 값 {_gameValues.Count}개");
        }

        /// <summary>ftue: 새 세이브 — 04b 잠긴 계보 카드(2단계) · 04c 첫 불 밝히기(4단계)</summary>
        IEnumerator Spec4Ftue()
        {
            yield return SkipIntro();
            yield return Safe("ftue_levelup_locked", FtueLevelUpLocked());
            yield return Safe("ftue_first_lamp", FtueFirstLamp());
        }

        /// <summary>
        /// early: 튜토리얼 끝 · 스테이지를 차례로 시드 — 37 1-4 실패 · 16b 1-5 · 38/39 1-7 · 27b 1-8 · 22b/22c 1-10 두 번 실패 · 30 1-10 · 17b/17c · 05b 2-1 ·
        /// 23b 2-3 · 20b/20c · 41 · 28e/28f · IA-menu (모든 기능 열림)
        /// </summary>
        IEnumerator Spec4Early()
        {
            yield return FinishFtue();
            yield return SeedCleared(3);
            yield return Safe("battle_defeat_ring", DefeatRing());
            yield return SeedCleared(5);
            yield return Safe("dungeon_locked", DungeonLocked());
            yield return SeedCleared(7);
            yield return Safe("battle_stats", BattleStats());
            yield return Safe("lamp_picker", LampPicker());
            yield return SeedCleared(8);
            yield return Safe("shop_daily_gift", ShopDailyGift());
            yield return SeedCleared(9);
            yield return Safe("strengthen_sheet", StrengthenSheet());
            yield return Safe("codex_entry_locked", CodexEntry(false));
            yield return SeedCleared(10);
            yield return Safe("codex_entry_open", CodexEntry(true));
            yield return Safe("codex_friends", CodexFriends());
            yield return SeedCleared(11, 11);
            yield return Safe("expedition_ladder", ExpeditionLadder());
            yield return Safe("danger_arrow_vs_guide", DangerArrowGuide());
            yield return SeedCleared(13);
            yield return Safe("journal_records", JournalRecords());
            yield return Safe("gacha_reveal", GachaReveal());
            yield return Safe("affinity_section", AffinitySection());
            yield return Safe("settings_rows", SettingsRows());
            UnlockAll();
            CloseAll();
            UINav.GoToMain(true);
            yield return Wait(1f);
            yield return Safe("menu", Menu());
        }

        /// <summary>chapter: 첫 1-10 — 07b 보스 단서 · 실제 클리어 → 36 챕터 상자 → 34 이야기 쪽 → StagePanel → 35 챕터 지도 (없으면 예전 스테이지 목록) · 16c</summary>
        IEnumerator Spec4Chapter()
        {
            yield return FinishFtue();
            yield return SeedCleared(9);
            yield return Safe("boss_hint", BossHint());
            yield return Safe("chapter_clear", ChapterClear());
            yield return Safe("stage_panel_route", StagePanelRoute());
            yield return Safe("dungeon_rec", DungeonRec());
        }

        /// <summary>
        /// calendar: 시계 = 이번 달 3일 · 1-7 · 이벤트 열림 · 설치 날 = 어제 → 첫 주를 실제 길로 연다(StartStarterWeek) → 첫 주 시작 날도 어제 (DayIndex 2) —
        /// S4-day2 · 23c 첫 주 · 31 이벤트 모아 보기 · 32 수첩
        /// </summary>
        IEnumerator Spec4Calendar()
        {
            var local = Game.DailyClock.ToLocal(Game.DailyClock.NowUnix);
            bool explicitClock = Arg(ClockArg) != null;
            if (explicitClock) Record("INFO", "S4-clock", $"calendar: 시계 {local:yyyy-MM-dd HH:mm} (-autoshot-clock)");
            else Check("S4-clock", local.Day == CalendarDay, $"calendar: 시계 {local:yyyy-MM-dd HH:mm} · 날 키 {Game.DailyClock.DayKey} (기대 이번 달 {CalendarDay}일)");
            yield return FinishFtue();
            yield return SeedCleared(7);
            if (Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Events)) Game.Features.ForceUnlock(FeatureId.Events, false);
            // 23c '둘째 날': 설치 날을 하루 앞으로 (새 세이브는 오늘 설치) — 첫 주를 열기 전에 (TryStart가 설치 일수 ≤ maxInstallDays를 본다)
            var meta = Game.Data.meta ??= new MetaSave();
            var clock = meta.clock ??= new MetaClockSave();
            string yesterday = DailyClock.DayKeyOf(local.AddDays(-1));
            clock.installDayKey = yesterday;
            yield return StartStarterWeek();
            // 첫 주가 시작됐으면 시작 날도 어제로 — DayIndex는 starterWeek.startDayKey에서 센다 (설치 날이 아니다, V2 r4)
            var week = Game.StarterWeek;
            if (week != null && week.IsStarted && meta.starterWeek != null) meta.starterWeek.startDayKey = yesterday;
            string weekText = week == null ? "첫 주 시스템 없음" : !StarterWeekHasTasks() ? "첫 주 콘텐츠 0" :
                $"첫 주 시작 {week.IsStarted} · 진행 {week.IsActive} · {week.DayIndex}일째";
            Check("S4-day2", Game.DailyClock.InstallDay == 2 && (week == null || !StarterWeekHasTasks() || week.DayIndex == 2),
                $"calendar: 설치 {clock.installDayKey} → 설치 {Game.DailyClock.InstallDay}일째 · {weekText} (기대 설치 2일째 · 첫 주 DayIndex 2)");
            yield return Wait(1.5f);
            CloseAll();
            yield return Safe("journal_first_week", JournalFirstWeek());
            yield return Safe("event_hub", EventHub());
            yield return Safe("season", Season());
        }

        /// <summary>comeback: 1-7 · 일시정지 → 시계 +80시간 → 복귀 (MetaSession.OnResume) — 복귀 화면 · 33 다시 만난 날 카드</summary>
        IEnumerator Spec4Comeback()
        {
            yield return FinishFtue();
            yield return SeedCleared(7);
            yield return Wait(2f);
            CloseAll();
            yield return Safe("comeback_card", Comeback());
        }

        /// <summary>idle: 손대지 않고 2분 → 잠자기 (40) · 반딧불 (42, -forceFirefly · 스트레치)</summary>
        IEnumerator Spec4Idle()
        {
            yield return FinishFtue();
            UnlockAll();
            CloseAll();
            UINav.GoToMain(true);
            yield return Safe("sleep_mode_fx", SleepModeFx());
            yield return Safe("firefly", Firefly());
        }

        /// <summary>soak (I-3만): 배속 2 · 자동 — 기본 20분 (-autoshot-soak-minutes). 예외 · 킬 스위치 · 꺼진 모듈 0, kill_without_arrival = 0 (P4-3b 카운터)</summary>
        IEnumerator Spec4Soak()
        {
            yield return FinishFtue();
            UnlockAll();
            CloseAll();
            UINav.GoToMain(true);
            yield return Safe("soak", Soak());
        }

        // ═════════════════════════ ftue (04b · 04c) ═════════════════════════

        /// <summary>
        /// 04b ftue_levelup_locked (F-03, P4-4): 튜토리얼 2단계에서 강화 시트의 계보 카드가 '1-1' 알약을 보이고, 누르면 오류 토스트 · 레벨 그대로.
        /// 알약이 아직 없으면 SKIP
        /// </summary>
        IEnumerator FtueLevelUpLocked()
        {
            const string id = "S4-04b";
            var tutorial = Game.Tutorial;
            if (!Game.IsMetaActive || tutorial == null || !tutorial.IsActive)
            {
                SkipCheck(id, $"ftue_levelup_locked: 튜토리얼이 돌지 않는다 ({StepLabel()})");
                yield break;
            }
            float limit = Time.realtimeSinceStartup + 20f;
            while (tutorial.IsActive && tutorial.StepIndex < 1 && Time.realtimeSinceStartup < limit) yield return null;
            string step = StepLabel();
            CloseAll();
            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            yield return Wait(0.3f);
            GrowthLineageCardView card = null;
            foreach (var candidate in FindObjectsByType<GrowthLineageCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (candidate.isActiveAndEnabled && FindText(candidate.transform, "1-1") != null)
                {
                    card = candidate;
                    break;
                }
            }
            if (card == null)
            {
                SkipCheck(id, $"ftue_levelup_locked: 아직 없음 — 계보 카드 '1-1' 알약 (P4-4 F-03) · 시트 {OpenText(UIFrame.SheetOpen)} · {step}");
                UINav.GoToMain(true);
                yield break;
            }
            int levels = LineageLevelSum();
            var toast = FindAnyObjectByType<ToastView>(FindObjectsInactive.Include);
            if (toast != null) toast.Clear();
            var button = card.GetComponentInChildren<Button>(false);
            if (button != null) button.onClick.Invoke();
            yield return Wait(0.3f);
            yield return Shot("ftue_levelup_locked", () =>
            {
                var p = new Probe();
                p.Info($"{step} · 카드 {card.name}");
                p.Expect(button != null, "카드 누름 단추");
                p.Expect(toast != null && toast.IsShowing && toast.CurrentKind == ToastKind.Error, $"오류 토스트 '{(toast != null ? toast.CurrentText : "")}' {(toast != null ? toast.CurrentKind.ToString() : "")}");
                p.Expect(LineageLevelSum() == levels, $"계보 레벨 합 {levels} → {LineageLevelSum()} (그대로)");
                Report(id, "ftue_levelup_locked", p);
            });
            UINav.GoToMain(true);
        }

        static int LineageLevelSum()
        {
            int sum = 0;
            if (Game.Data?.lineages == null) return sum;
            foreach (var pair in Game.Data.lineages) sum += pair.Value != null ? pair.Value.level : 0;
            return sum;
        }

        /// <summary>
        /// 04c ftue_first_lamp (§13.3 FTUE-01, P4-6 + P4-3b): 튜토리얼을 불 밝히기 단계(코치 대상 = LampButton)까지 몬다 (덱 첫 칸은 대신 소환, 배속 2, 75초까지).
        /// AppSignals.FtueFirstLampBeat가 켜지면 불을 밝히고 1.5초 동안 BattleFx.TimeScale을 프레임마다 잰다 — 판정은 늘 잰 값 (기대 가장 느린 배율 0.2 ±0.05 ·
        /// 0.5배 아래 0.6 ±0.15초), 게임의 "[Verify] first_lamp_beat" 줄(HitFeel 상수)은 기록만. 큰 FTUE 배너(제목 '…불이 밝혀졌어요!' · 폭 ≥ 688)가 뜬다,
        /// 게임 경고 줄(OnLog Warning)에 리플렉션 · FtueBattleGuard 없음. 신호가 3초 안에 안 켜지면 SKIP (FtueDirector가 아직 안 켠다)
        /// </summary>
        IEnumerator FtueFirstLamp()
        {
            const string id = "S4-04c";
            var tutorial = Game.Tutorial;
            if (!Game.IsMetaActive || tutorial == null || !tutorial.IsActive || Game.Battle == null)
            {
                SkipCheck(id, $"ftue_first_lamp: 튜토리얼이 돌지 않는다 ({StepLabel()})");
                yield break;
            }
            UINav.GoToMain(true);
            int speedWas = Game.Battle.Speed;
            try
            {
                Game.Battle.Speed = BattleSystem.MaxSpeed;
                float limit = Time.realtimeSinceStartup + 75f;
                while (tutorial.IsActive && Time.realtimeSinceStartup < limit)
                {
                    var step = tutorial.Current;
                    if (step != null && tutorial.IsStepRunning && step.coachTarget == CoachTargets.LampButton) break;
                    if (step != null && tutorial.IsStepRunning && !step.hintOnly && step.coachTarget == CoachTargets.DeckCard0) Game.Battle.TrySummon(0);
                    yield return Wait(0.25f);
                }
            }
            finally
            {
                if (Game.Battle != null) Game.Battle.Speed = speedWas;
            }
            var current = tutorial.Current;
            if (!tutorial.IsActive || current == null || current.coachTarget != CoachTargets.LampButton)
            {
                SkipCheck(id, $"ftue_first_lamp: 75초 안에 불 밝히기 단계에 닿지 못했다 ({StepLabel()})");
                yield break;
            }
            float signal = Time.realtimeSinceStartup + 3f;
            while (!AppSignals.FtueFirstLampBeat && Time.realtimeSinceStartup < signal) yield return null;
            if (!AppSignals.FtueFirstLampBeat)
            {
                SkipCheck(id, $"ftue_first_lamp: 아직 없음 — AppSignals.FtueFirstLampBeat가 켜지지 않는다 (P4-6 FtueDirector · P4-3b HitFeel.FirstLampIgniteScale) · {StepLabel()}");
                yield break;
            }
            var fx = FindAnyObjectByType<BattleFx>();
            var banner = Find(BannerPath);
            var bannerView = FindAnyObjectByType<BattleBannerView>();
            var bannerTitle = bannerView != null ? Field<TMP_Text>(bannerView, "_title") : null;
            var lamp = typeof(BattleSimulator).GetField("_lamp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (Game.Battle.Sim != null && !Game.Battle.Sim.CanLightLamp && lamp != null) lamp.SetValue(Game.Battle.Sim, 1f);
            int warningsBefore = _warnings.Count;
            // 게임 줄 '[Verify] first_lamp_beat scale= time='은 HitFeel 상수를 적는다 (잰 값이 아니다) — 이번 불 밝히기의 줄만 기록으로 남긴다
            _gameValues.Remove("scale");
            _gameValues.Remove("time");
            bool lit = Game.Battle.TryLightLamp();
            float minScale = 1f, slow = 0f, start = Time.realtimeSinceStartup;
            bool bigSeen = false;
            string bannerText = null;
            float bannerWidth = 0f;
            while (Time.realtimeSinceStartup - start < 1.5f)
            {
                float scale = fx != null ? fx.TimeScale : 1f;
                minScale = Mathf.Min(minScale, scale);
                if (scale < 0.5f) slow += Time.unscaledDeltaTime;
                if (banner != null && banner.gameObject.activeInHierarchy && EffectiveAlpha(banner) > 0.05f)
                {
                    // 큰 FTUE 배너 = 첫 불 제목(Celebrate, 860 폭) — 작은 리본('불 밝히기!' 516 폭)이 아니다
                    string title = bannerTitle != null ? ShownText(bannerTitle) : FindText(banner, FirstLampBannerPart);
                    float width = ScreenRect(banner).width;
                    if (!string.IsNullOrEmpty(title))
                    {
                        bannerText = title;
                        bannerWidth = Mathf.Max(bannerWidth, width);
                        if (title.Contains(FirstLampBannerPart) && width >= FirstLampBannerMinWidth) bigSeen = true;
                    }
                }
                yield return null;
            }
            bool fromGame = _gameValues.TryGetValue("scale", out var gameScale) & _gameValues.TryGetValue("time", out var gameTime);
            bool reflection = false;
            for (int i = warningsBefore; i < _warnings.Count; i++)
            {
                if (_warnings[i].IndexOf("reflection", StringComparison.OrdinalIgnoreCase) >= 0 || _warnings[i].Contains("FtueBattleGuard")) reflection = true;
            }
            foreach (var warning in _warnings)
            {
                if (warning.Contains("FtueBattleGuard")) reflection = true;
            }
            yield return Shot("ftue_first_lamp", () =>
            {
                var p = new Probe();
                p.Expect(lit, "불을 밝혔다");
                // 판정은 늘 이 도구가 1.5초 동안 프레임마다 잰 BattleFx.TimeScale (V2 r4 — 예전에는 게임이 적은 상수로 판정해 늘 PASS)
                p.Near("가장 느린 배율 (잰 BattleFx.TimeScale)", minScale, 0.2f, 0.05f);
                p.Near("0.5배 아래 시간(초, 잰 값)", slow, 0.6f, 0.15f);
                p.Expect(bigSeen, $"큰 FTUE 배너 '{bannerText}' 가장 넓을 때 {bannerWidth:0}px (기대 '…{FirstLampBannerPart}…' · ≥ {FirstLampBannerMinWidth:0}px — 작은 리본 아님)");
                p.Expect(!reflection, $"리플렉션(FtueBattleGuard) 경고 없음 (이번에 모은 경고 {_warnings.Count - warningsBefore}줄)");
                p.Info(fromGame ? $"게임 로그 first_lamp_beat scale={gameScale} time={gameTime} (HitFeel 상수 — 판정에 쓰지 않음)" : "게임 로그 first_lamp_beat 줄 없음");
                Report(id, "ftue_first_lamp", p);
            });
        }

        // ═════════════════════════ early ═════════════════════════

        /// <summary>
        /// 37 battle_defeat_ring (CR-01, P4-3b): 1-4에서 실제로 진다 — 수선점 체력을 1로 두고 적이 문을 쳐서 지게 한다 (패배 분석은 ShopDamaged 기록이 있어야 뜬다,
        /// 25초 안에 안 지면 BattleSimulator.Fail) → 고리가 뜨고(3초 안) ≥ 3.0초 보이며, 그동안 다음 판이 시작하지 않고, 가이드 퀘스트 줄과 겹치지 않는다.
        /// DefeatAnalysisView가 아직 없으면 SKIP (실패는 그대로 만든다)
        /// </summary>
        IEnumerator DefeatRing()
        {
            const string id = "S4-37";
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            yield return ShopFail();
            if (!_forced)
            {
                Check(id, false, $"battle_defeat_ring: {Game.Battle.StageLabel} 실패를 만들지 못했다");
                yield break;
            }
            if (!Landed(id, "battle_defeat_ring", "P4-3b CR-01", "DefeatAnalysisView")) yield break;
            var view = FindView("DefeatAnalysisView");
            float limit = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < limit && (view == null || !IsVisible(view.transform))) yield return null;
            bool shown = view != null && IsVisible(view.transform);
            // 보인 시간은 처음 보인 프레임부터 잰다 (V2 r4 — 예전에는 찍은 뒤부터 + 0.5초 상수라 ≈0.45초 길게 쟀다)
            float firstSeen = Time.realtimeSinceStartup;
            bool restartedWhileShown = false;
            // 나타나기(_group 알파 0 → 1, FadeIn 0.2초)가 끝난 뒤 찍는다 — firstSeen은 그대로 (V2 r1 fix: 처음 보인 프레임은 알파 ≈ 0.08이라 그림에 고리가 없었다)
            var group = shown ? Field<CanvasGroup>(view, "_group") : null;
            float fadeLimit = firstSeen + ViewShownWait;
            while (group != null && group.alpha < PopupShownAlpha && IsVisible(view.transform) && Time.realtimeSinceStartup < fadeLimit)
            {
                if (!Game.Battle.IsBetweenStages) restartedWhileShown = true;
                yield return null;
            }
            yield return Shot("battle_defeat_ring", () =>
            {
                var p = new Probe();
                p.Expect(shown, "패배 분석 고리가 보인다");
                if (shown)
                {
                    float alpha = group != null ? group.alpha : EffectiveAlpha(view.transform);
                    p.Expect(alpha >= PopupShownAlpha,
                        $"찍을 때 고리 알파 {alpha:0.00}{(group != null ? string.Empty : " (_group 없음 — 조상 알파)")} (기대 ≥ {PopupShownAlpha:0.00} — 나타나기 끝, 처음 보인 뒤 {Time.realtimeSinceStartup - firstSeen:0.00}초)");
                    var rect = ScreenRect((RectTransform)view.transform);
                    var guide = Find(CoachTargets.GuideQuestBar);
                    if (guide != null && IsVisible(guide) && VerifyMath.Overlaps(rect, ScreenRect(guide))) p.Expect(false, $"고리 {R(rect)}가 가이드 줄 {R(ScreenRect(guide))}와 겹침");
                    p.Info($"고리 {R(rect)}");
                    // CR-01 설명 제목 (P4-3b: DefeatAnalyzer.Title = KoreanText.WithSubject — '왼쪽 위가 많이 뚫렸어요')
                    var title = Field<TMP_Text>(view, "_title");
                    string caption = title != null ? ShownText(title) : null;
                    if (string.IsNullOrEmpty(caption)) p.Info("설명 제목 DefeatAnalysisView._title 없음 · 빈 글 — 조사 규칙을 재지 못함");
                    else p.Expect(DefeatCaptionOk(caption, out string why), $"설명 제목 '{caption}' — {why}");
                }
                Report(id, "battle_defeat_ring", p);
            });
            if (!shown) yield break;
            while (IsVisible(view.transform) && Time.realtimeSinceStartup - firstSeen < 8f)
            {
                if (!Game.Battle.IsBetweenStages) restartedWhileShown = true;
                yield return null;
            }
            float visible = Time.realtimeSinceStartup - firstSeen;
            var q = new Probe();
            q.Expect(visible >= 3.0f, $"고리 보인 시간 ≈{visible:0.0}초 (기대 ≥ 3.0)");
            q.Expect(!restartedWhileShown, "고리가 보이는 동안 다음 판이 시작하지 않았다");
            Report(id + "-time", "battle_defeat_ring", q);
            yield return DefeatRetry(view);
        }

        /// <summary>
        /// S4-37-retry (SPEC4 §11.4 37 updated): 한 번 더 진다 → 고리가 보이면 '바로 다시 도전 ›'(DefeatAnalysisView._retryLink → Game.Battle.SkipEndPause)을 눌러
        /// 다음 판이 RetryRestartMax(0.4초) 안에 시작하고 고리가 숨는다. 그림은 찍지 않는다 (early 판 그림 번호를 밀지 않게)
        /// </summary>
        IEnumerator DefeatRetry(Component view)
        {
            const string id = "S4-37-retry";
            var link = Field<Button>(view, "_retryLink");
            if (link == null)
            {
                Check(id, false, "battle_defeat_ring: DefeatAnalysisView._retryLink 없음 (필드 이름이 바뀌었나 — V2에 알린다)");
                yield break;
            }
            // 같은 스테이지에서는 10분 안에 고리를 다시 띄우지 않는다 (DefeatAnalyzer.ShouldShow — r9: 1-4에서 두 번 지면 두 번째는 안 떴다) → 한 칸 앞 스테이지에서 진다
            int stageWas0 = Game.Battle.Stage;
            if (stageWas0 > 1)
            {
                Game.Battle.GoToStage(stageWas0 - 1);
                yield return Wait(1f);
                CloseAll();
            }
            yield return ShopFail();
            if (!_forced)
            {
                SkipCheck(id, $"battle_defeat_ring: 두 번째 실패를 만들지 못했다 ({Game.Battle.StageLabel}) — 다시 도전 링크를 재지 못함");
                yield break;
            }
            float limit = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < limit && !IsVisible(view.transform)) yield return null;
            if (!IsVisible(view.transform))
            {
                SkipCheck(id, $"battle_defeat_ring: 두 번째 실패({Game.Battle.StageLabel}) 뒤 3초 안에 고리가 안 보였다 — 다시 도전 링크를 재지 못함");
                yield break;
            }
            // 나타나기(DefeatAnalysisView.FadeIn 0.2초)가 끝난 뒤 누른다
            yield return Wait(DefeatAnalysisView.FadeIn + 0.2f);
            string label = FindText(link.transform, "다시 도전");
            bool usable = link.gameObject.activeInHierarchy && link.interactable;
            string stageWas = Game.Battle.StageLabel;
            float pressed = Time.realtimeSinceStartup;
            link.onClick.Invoke();
            float restartLimit = pressed + 2f;
            while (Game.Battle.IsBetweenStages && Time.realtimeSinceStartup < restartLimit) yield return null;
            float after = Time.realtimeSinceStartup - pressed;
            bool restarted = !Game.Battle.IsBetweenStages;
            var p = new Probe();
            p.Expect(label != null, $"링크 글 '{label ?? FindText(link.transform, string.Empty)}' (기대 '바로 다시 도전 ›')");
            p.Expect(usable, "링크가 켜져 있고 누를 수 있다");
            p.Expect(restarted && after <= RetryRestartMax,
                $"누르고 {after:0.00}초에 {(restarted ? $"다음 판 {Game.Battle.StageLabel} 시작" : "다음 판이 시작하지 않음")} (기대 ≤ {RetryRestartMax:0.0}초 — {stageWas} 결과 대기를 건너뜀)");
            p.Expect(!IsVisible(view.transform), "누른 뒤 고리 숨김");
            Report(id, "battle_defeat_ring", p);
        }

        /// <summary>
        /// CR-01 설명 제목 '{방향}이/가 많이 뚫렸어요' — 받침에 맞는 조사 하나 (받침 있음 '이', 없음 '가'). 두 조사를 함께 쓴 꼴('이/가' · '(이)가')은 false.
        /// '… 많이 뚫렸어요' 꼴이 아니거나 앞 글자가 한글 음절이 아니면 잴 수 없어 true (why에 적는다)
        /// </summary>
        static bool DefeatCaptionOk(string caption, out string why)
        {
            if (caption.Contains("이/가") || caption.Contains("가/이") || caption.Contains("(이)"))
            {
                why = "조사를 둘 다 썼다 (기대 받침에 맞는 하나)";
                return false;
            }
            var match = Regex.Match(caption, @"^(.+?)(이|가) 많이 뚫렸어요");
            string word = match.Success ? match.Groups[1].Value.TrimEnd() : string.Empty;
            if (word.Length == 0)
            {
                why = "'… 많이 뚫렸어요' 꼴이 아님 — 조사 규칙을 재지 못함";
                return true;
            }
            char last = word[word.Length - 1];
            if (last < '가' || last > '힣')
            {
                why = $"'{last}'가 한글 음절이 아님 — 조사 규칙을 재지 못함";
                return true;
            }
            string want = (last - 0xAC00) % 28 != 0 ? "이" : "가";
            string got = match.Groups[2].Value;
            why = $"'{word}' 뒤 조사 '{got}' (기대 '{want}')";
            return got == want;
        }

        /// <summary>
        /// 16b dungeon_locked (F-03, P4-4): 1-5 · 던전 잠김 — 원정 시트의 던전 칸 흐림(_dungeonSegGroup) α ≈ 0.45 + 자물쇠 덮개(_dungeonLock = ExpeditionPanel/DungeonLock,
        /// 칸의 형제)가 보이고, 눌러도 쪽이 바뀌지 않는다. 던전이 이미 열렸거나 원정 탭이 잠겼으면 SKIP, 필드가 비었으면(옛 장면) SKIP, 잠겼는데 덮개 · α가 틀리면 FAIL
        /// </summary>
        IEnumerator DungeonLocked()
        {
            const string id = "S4-16b";
            if (Game.Features != null && Game.Features.IsUnlocked(FeatureId.Dungeon))
            {
                SkipCheck(id, "dungeon_locked: 1-5에서 던전이 이미 열림 — 잠긴 칸을 볼 수 없다 (콘텐츠 기준 확인)");
                yield break;
            }
            CloseAll();
            _tabs.Select(MainTab.Expedition);
            yield return WaitSettled();
            var panel = FindAnyObjectByType<ExpeditionPanel>();
            if (!UIFrame.SheetOpen || panel == null)
            {
                SkipCheck(id, $"dungeon_locked: 원정 시트가 열리지 않는다 (원정 잠김 {(Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Expedition))})");
                UINav.GoToMain(true);
                yield break;
            }
            var seg = Field<Button>(panel, "_dungeonSeg");
            var dungeonPage = Field<CanvasGroup>(panel, "_dungeonPage");
            var zonePage = Field<CanvasGroup>(panel, "_zonePage");
            // 자물쇠는 칸의 자식이 아니라 머리 줄의 형제 덮개 ExpeditionPanel/DungeonLock (P4-4 F-03 — 칸 흐림 α0.45는 _dungeonSegGroup, 덮개는 흐리지 않는다)
            var lockMark = Field<GameObject>(panel, "_dungeonLock");
            var segGroup = Field<CanvasGroup>(panel, "_dungeonSegGroup");
            var lockText = Field<TMP_Text>(panel, "_dungeonLockText");
            if (seg == null || lockMark == null || segGroup == null)
            {
                SkipCheck(id, $"dungeon_locked: 옛 장면 — ExpeditionPanel {(seg == null ? "_dungeonSeg " : string.Empty)}{(lockMark == null ? "_dungeonLock " : string.Empty)}" +
                              $"{(segGroup == null ? "_dungeonSegGroup " : string.Empty)}비어 있음 (P4-4 F-03 장면 다시 만들기 전)");
                UINav.GoToMain(true);
                yield break;
            }
            bool locked = DungeonLockedNow();
            if (!locked)
            {
                SkipCheck(id, "dungeon_locked: ExpeditionPanel.DungeonLocked false — 잠긴 칸을 볼 수 없다");
                UINav.GoToMain(true);
                yield break;
            }
            float alpha = segGroup.alpha;
            bool lockShown = lockMark.activeInHierarchy && IsVisible(lockMark.transform);
            string lockLabel = lockText != null ? ShownText(lockText) : string.Empty;
            seg.onClick.Invoke();
            yield return Wait(0.4f);
            yield return Shot("dungeon_locked", () =>
            {
                var p = new Probe();
                p.Near("던전 칸 α (_dungeonSegGroup)", alpha, ExpeditionLockedAlpha, 0.05f);
                p.Expect(lockShown, $"자물쇠 덮개 {lockMark.name} 보임 · 알약 '{lockLabel}'");
                p.Expect(lockMark.activeInHierarchy, $"누른 뒤에도 자물쇠 {lockMark.name} 켜짐");
                bool stayed = (dungeonPage == null || !dungeonPage.gameObject.activeInHierarchy || dungeonPage.alpha < 0.5f) &&
                              (zonePage == null || (zonePage.gameObject.activeInHierarchy && zonePage.alpha > 0.5f));
                p.Expect(stayed, $"눌러도 원정 쪽 그대로 (던전 쪽 α {(dungeonPage != null ? dungeonPage.alpha : -1f):0.00})");
                Report(id, "dungeon_locked", p);
            });
            UINav.GoToMain(true);
        }

        /// <summary>SPEC4 F-03 잠긴 던전 칸 알파</summary>
        const float ExpeditionLockedAlpha = 0.45f;

        /// <summary>ExpeditionPanel.DungeonLocked (internal static — 이름으로 읽는다, 없으면 메타 켜짐 · 던전 잠김으로 어림)</summary>
        static bool DungeonLockedNow()
        {
            var property = typeof(ExpeditionPanel).GetProperty("DungeonLocked", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (property != null && property.GetValue(null) is bool locked) return locked;
            return Game.IsMetaActive && Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Dungeon);
        }

        /// <summary>
        /// 38 battle_stats (CR-03, P4-3b): 1-7 — 전투 기록 창. 기록이 둘 이상 쌓인 뒤(늦어도 10초) 열고, 창의 목록(_list — BattleStatsRecorder.Fill)이 피해 많은 순 ·
        /// 켜진 줄 수 = 목록 수. 열기 전에 S4-hud-stats (StatsButton ↔ 지킴이 받침 ≥ 16, SPEC4 §11.4 정적 줄). 창이 없으면 SKIP
        /// </summary>
        IEnumerator BattleStats()
        {
            const string id = "S4-38";
            if (!Landed(id, "battle_stats", "P4-3b CR-03", "BattleStatsPopup")) yield break;
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            CheckStatsButtonPads();
            // 줄이 하나뿐이면 정렬을 잴 수 없다 — 기록에 친구가 둘 이상 들어올 때까지 (지금 판이 돈다)
            float limit = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < limit && (BattleStatsPopup.Source == null || BattleStatsPopup.Source.UnitCount < 2)) yield return null;
            UINav.OpenBattleStats();
            yield return Wait(1.2f);
            var popup = FindPopup("BattleStatsPopup");
            yield return Shot("battle_stats", () =>
            {
                var p = new Probe();
                p.Expect(popup != null && popup.IsOpen, "전투 기록 창 열림 (UINav.OpenBattleStats)");
                if (popup != null) CheckStatsRows(p, popup);
                Report(id, "battle_stats", p);
            });
            CloseAll();
        }

        /// <summary>38 줄 정렬 (P4-3b 이름): 창의 _list가 피해 내림차순, 켜진 _rows 수 = 목록 수 (줄 수만큼). 목록이 둘 미만이면 정렬은 기록만</summary>
        static void CheckStatsRows(Probe p, UIPopup popup)
        {
            var list = Field<List<BattleStatsRecorder.UnitTotals>>(popup, "_list");
            var rows = Field<RectTransform[]>(popup, "_rows");
            if (list == null || rows == null)
            {
                p.Expect(false, $"BattleStatsPopup {(list == null ? "_list " : string.Empty)}{(rows == null ? "_rows " : string.Empty)}없음 (필드 이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            int active = 0;
            foreach (var row in rows)
            {
                if (row != null && row.gameObject.activeSelf) active++;
            }
            p.Expect(active == Math.Min(list.Count, rows.Length), $"켜진 줄 {active}개 · 목록 {list.Count}명 (줄 {rows.Length}칸)");
            if (list.Count < 2)
            {
                p.Info($"목록 {list.Count}명 — 정렬 규칙을 재지 못함");
                return;
            }
            int bad = -1;
            var order = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i < 5) order.Append(i > 0 ? " ≥ " : string.Empty).Append(list[i].Damage.ToString("0", CultureInfo.InvariantCulture));
                if (i > 0 && bad < 0 && list[i].Damage > list[i - 1].Damage) bad = i;
            }
            p.Expect(bad < 0, $"피해 많은 순 {order}{(list.Count > 5 ? " …" : string.Empty)}{(bad >= 0 ? $" — {bad + 1}번째 줄이 앞 줄보다 큼" : string.Empty)}");
        }

        /// <summary>
        /// S4-hud-stats (SPEC4 §11.4 정적 줄 'StatsButton vs pads ≥ 16', CR-03 · IA R1 칸): 보이는 전투 기록 단추 StatsButton과 지킴이 받침(GuardianSlotView._pad) 전부 사이 ≥ 16.
        /// 단추가 안 보이면(1-7 전 · 시트) SKIP. 그림 없음
        /// </summary>
        void CheckStatsButtonPads()
        {
            const string id = "S4-hud-stats";
            var button = FindDeep(_root, "StatsButton");
            if (button == null || !IsVisible(button))
            {
                SkipCheck(id, $"hud_stats_vs_pads: StatsButton {(button == null ? "없음" : "안 보임")} — 받침과의 띄움을 재지 못함");
                return;
            }
            var rect = ScreenRect(button);
            var p = new Probe();
            int pads = ExpectPadClearance(p, rect, StatsPadClearance, "StatsButton");
            p.Expect(pads > 0, $"잰 지킴이 받침 {pads}개");
            p.Info($"StatsButton {R(rect)} · 띄움 ≥ {StatsPadClearance:0}px");
            Report(id, "hud_stats_vs_pads", p);
        }

        /// <summary>켜진 지킴이 받침(GuardianSlotView._pad) 전부와 rect 사이가 clearance 이상인지 (어긋나면 Expect false) — 잰 받침 수</summary>
        int ExpectPadClearance(Probe p, Rect rect, float clearance, string what)
        {
            int count = 0;
            foreach (var slot in FindObjectsByType<GuardianSlotView>(FindObjectsSortMode.None))
            {
                var pad = Field<RectTransform>(slot, "_pad");
                if (pad == null || !pad.gameObject.activeInHierarchy) continue;
                count++;
                var padRect = ScreenRect(pad);
                var padded = Rect.MinMaxRect(padRect.xMin - clearance, padRect.yMin - clearance, padRect.xMax + clearance, padRect.yMax + clearance);
                if (VerifyMath.Overlaps(rect, padded)) p.Expect(false, $"{what} {R(rect)}가 받침 {slot.name} {R(padRect)}와 {clearance:0}px 안");
            }
            return count;
        }

        /// <summary>39 lamp_picker (P4-3b): 불 밝히기 정책 고르기 — 세 줄이 램프 버튼 위. 고르기 창이 없거나 열 길이 없으면 SKIP</summary>
        IEnumerator LampPicker()
        {
            const string id = "S4-39";
            if (!Landed(id, "lamp_picker", "P4-3b", "LampPolicyPicker")) yield break;
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            var picker = FindView("LampPolicyPicker");
            if (!TryInvoke(picker, "Open") && !TryInvoke(picker, "Show"))
            {
                SkipCheck(id, "lamp_picker: LampPolicyPicker에 매개 없는 Open/Show 없음 — 여는 길은 part B");
                yield break;
            }
            yield return Wait(0.6f);
            yield return Shot("lamp_picker", () =>
            {
                var p = new Probe();
                var panel = Field<GameObject>(picker, "_panel");
                var rows = Field<Button[]>(picker, "_rows");
                p.Expect(panel != null && panel.activeInHierarchy, "고르기 판(LampPolicyPicker._panel) 열림");
                int shown = 0;
                var lampButton = Find(CoachTargets.LampButton);
                float lampTop = lampButton != null ? ScreenRect(lampButton).yMin : float.NaN;
                // 램프 버튼이 없으면 '줄이 버튼 위' 규칙을 잴 수 없다 — 조용히 넘기지 않고 FAIL (V2 r4)
                if (float.IsNaN(lampTop)) p.Expect(false, $"램프 버튼 {CoachTargets.LampButton} 없음 — 줄 자리 규칙을 재지 못함");
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        if (row == null || !row.gameObject.activeInHierarchy) continue;
                        shown++;
                        var rect = ScreenRect((RectTransform)row.transform);
                        if (!float.IsNaN(lampTop) && rect.yMax > lampTop + 0.5f) p.Expect(false, $"{row.name} {R(rect)}가 램프 버튼 위 끝 {lampTop:0.#} 아래로 내려옴");
                    }
                }
                p.Expect(shown == 3, $"줄 {shown}개 (기대 3 — 알아서 · 보스까지 아끼기 · 직접)");
                if (panel != null) p.Info($"판 {R(ScreenRect((RectTransform)panel.transform))} · 램프 위 끝 {lampTop:0.#}");
                Report(id, "lamp_picker", p);
            });
            CloseAll();
        }

        /// <summary>27b shop_daily_gift (NEW-02, P4-4): 1-8 — 상점 시트 상품 격자 위 '오늘의 작은 선물' 카드, 받은 뒤 남은 시간 줄. 카드가 없으면 SKIP</summary>
        IEnumerator ShopDailyGift()
        {
            const string id = "S4-27b";
            if (!Landed(id, "shop_daily_gift", "P4-4 NEW-02", "DailyGiftCardView")) yield break;
            CloseAll();
            _tabs.Select(MainTab.Shop);
            yield return WaitSettled();
            var card = FindView("DailyGiftCardView");
            // 받기 단추 = DailyGiftCard/ClaimButton (첫 Button 자식은 보상 칩 Reward — 누르면 아이템 정보 창, P4-4 요청 1c)
            var claimRect = card != null ? FindDeep(card.transform, "ClaimButton") : null;
            var claim = claimRect != null ? claimRect.GetComponent<Button>() : null;
            var lineRect = card != null ? FindDeep(card.transform, "Line") : null;
            bool visible = card != null && IsVisible(card.transform);
            string before = lineRect != null ? FindText(lineRect, string.Empty) : null;
            var dots = Game.RedDots;
            bool dotBefore = dots != null && dots.IsOn(RedDotPaths.ShopFree);
            bool claimable = claim != null && claim.interactable && claim.gameObject.activeInHierarchy;
            if (claimable) claim.onClick.Invoke();
            yield return Wait(0.8f);
            // 상품 목록 윗변 = ShopPanel._listRoot (선물 카드가 보이면 132 내려간다 — DailyGiftCardView.ListShift)
            var shop = FindAnyObjectByType<ShopPanel>();
            var listRoot = shop != null ? Field<RectTransform>(shop, "_listRoot") : null;
            yield return Shot("shop_daily_gift", () =>
            {
                var p = new Probe();
                p.Expect(visible, "선물 카드가 상점 시트에 보인다");
                p.Expect(claimable, $"받기 단추 DailyGiftCard/ClaimButton {(claim == null ? "없음" : claimable ? "누름" : "누를 수 없음")}");
                if (card != null && listRoot != null)
                {
                    var cardRect = ScreenRect((RectTransform)card.transform);
                    var listRect = ScreenRect(listRoot);
                    p.Expect(cardRect.yMax <= listRect.yMin + 0.5f, $"카드 {R(cardRect)}가 상품 목록 {R(listRect)} 위 (카드 아래 끝 ≤ 목록 위 끝)");
                }
                else p.Expect(false, $"자리를 잴 수 없다 — 카드 {(card != null ? "있음" : "없음")} · ShopPanel._listRoot {(listRoot != null ? "있음" : "없음")}");
                string after = lineRect != null ? FindText(lineRect, string.Empty) : null;
                bool countdown = after != null && after != DailyGiftCardView.ReadyLine && after.Contains("새 선물");
                p.Expect(countdown, $"받은 뒤 줄 '{before}' → '{after}' (기대 '…새 선물이 와요' · '새 선물까지 m:ss')");
                bool dotAfter = dots != null && dots.IsOn(RedDotPaths.ShopFree);
                p.Expect(dotBefore && !dotAfter, $"점 {RedDotPaths.ShopFree} 받기 전 {OnText(dotBefore)} → 뒤 {OnText(dotAfter)} (기대 켜짐 → 꺼짐)");
                p.Info($"상점 탭 점 {RedDotPaths.TabShop} {(dots != null && dots.IsOn(RedDotPaths.TabShop) ? "켜짐 (다른 잎)" : "꺼짐")}");
                Report(id, "shop_daily_gift", p);
            });
            UINav.GoToMain(true);
        }

        /// <summary>
        /// 22b strengthen_sheet · 22c cheer_chip (PRG-03, P4-1/P4-6/P4-3b): 1-10에서 두 번 진다 → 강해지기 창 (_rows 필드가 있고 보이는 줄 1~4 · 응원 단추). 응원 → CheerChip이
        /// 보이고 보이는 Hud · MetaHud 칸 전부와 ≥ 8 띄움 · BattleFx.HudKeepOutPaths에 'CheerChip' (자리 836~948 × 440~488은 기록만 — SPEC4의 872~984 × 500~548은 IA 전 숫자).
        /// 응원 칩이 없으면 22c SKIP
        /// </summary>
        IEnumerator StrengthenSheet()
        {
            const string id = "S4-22b";
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            int stage = Game.Battle.Stage;
            for (int i = 0; i < 2; i++)
            {
                yield return ForceOutcome(false);
                if (!_forced)
                {
                    Check(id, false, $"strengthen_sheet: {BattleSystem.FormatStage(stage)} 실패 {i + 1}번째를 만들지 못했다");
                    yield break;
                }
                float next = Time.realtimeSinceStartup + 10f;
                while (Game.Battle.IsBetweenStages && Time.realtimeSinceStartup < next) yield return null;
                CloseAll();
            }
            Record("INFO", id, $"strengthen_sheet: {BattleSystem.FormatStage(stage)} 두 번 실패 → 지금 {Game.Battle.StageLabel} · 막힌 {Game.Battle.ChallengeTarget}");
            bool opened = StrengthenPopup.TryOpen();
            yield return Wait(1f);
            var popup = UIManager.Instance.Get<StrengthenPopup>();
            Button cheer = null;
            yield return Shot("strengthen_sheet", () =>
            {
                var p = new Probe();
                p.Expect(opened && popup != null && popup.IsOpen, "강해지기 창 열림 (StrengthenPopup.TryOpen)");
                var rows = popup != null ? Field<GameObject[]>(popup, "_rows") : null;
                int active = 0;
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        if (row != null && row.activeInHierarchy) active++;
                    }
                }
                // 빈 창 · 이름이 바뀐 필드가 PASS하지 않게 (V2 r4): 필드가 있어야 하고 1~4줄
                if (popup != null && rows == null) p.Expect(false, "StrengthenPopup._rows 없음 (필드 이름이 바뀌었나 — V2에 알린다)");
                else
                {
                    string empty = popup != null && active == 0 ? FindText(popup.transform, string.Empty) : null;
                    p.Expect(active >= 1 && active <= 4, $"보이는 줄 {active}개 / {rows?.Length ?? 0}칸 (기대 1~4 — 두 번 진 뒤){(active == 0 ? $" · 빈 창 글 '{empty}'" : string.Empty)}");
                }
                if (popup != null) CheckAdviceOrder(p, popup);
                if (popup != null) cheer = FindButtonWithText(popup.transform, "응원");
                // 응원은 들어왔다 (P4-1 CheerSystem · P4-6 단추): 시스템이 있고 튜토리얼이 끝났으면 단추가 있어야 한다
                bool cheerDue = Game.Cheer != null && (Game.Tutorial == null || Game.Tutorial.IsDone);
                if (cheerDue) p.Expect(cheer != null, $"응원 단추 {(cheer != null ? $"{cheer.name} '{FindText(cheer.transform, "응원")}'" : "없음")} (Cheer.CanUse {Game.Cheer.CanUse})");
                else p.Info($"응원 단추 {(cheer != null ? cheer.name : "없음")} — 응원 시스템 {(Game.Cheer != null ? "있음 · 튜토리얼 중" : "없음")}");
                Report(id, "strengthen_sheet", p);
            });
            if (cheer == null || !Landed("S4-22c", "cheer_chip", "P4-3b · P4-1 PRG-03", "CheerChipView"))
            {
                if (cheer == null) SkipCheck("S4-22c", "cheer_chip: 아직 없음 — 강해지기 창 응원 단추 (P4-1 · P4-6)");
                CloseAll();
                yield break;
            }
            cheer.onClick.Invoke();
            yield return Wait(0.8f);
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            var chip = FindView("CheerChipView");
            yield return Shot("cheer_chip", () =>
            {
                var p = new Probe();
                bool visible = chip != null && IsVisible(chip.transform);
                p.Expect(visible, "응원 칩이 보인다");
                if (visible)
                {
                    var rect = ScreenRect((RectTransform)chip.transform);
                    // 자리는 기록만: 다시 만든 장면 836~948 × 440~488 (P4-3b — SPEC4 §11.4의 872~984 × 500~548은 IA 틀 전 숫자, UX-SPEC-IA §8.8: StatsButton이 R1 칸으로 옮겼다).
                    // 띄움(≥ 8)과 BattleFx 비킴 칸 등록(Wave-3 HUD keep-out — FX가 칩을 덮지 않는다)은 검사
                    p.Info($"칩 {chip.name} {R(rect)} · 장면 자리 {R(CheerChipZone)} (P4-3b — 기록만)");
                    var keepOuts = typeof(BattleFx).GetField("HudKeepOutPaths", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) as string[];
                    if (keepOuts == null) p.Info("BattleFx.HudKeepOutPaths를 읽지 못함 — 비킴 칸 등록은 재지 못함");
                    else p.Expect(Array.IndexOf(keepOuts, CheerChipKeepOut) >= 0, $"BattleFx.HudKeepOutPaths에 '{CheerChipKeepOut}' ({keepOuts.Length}칸 — FX가 응원 칩을 비킨다)");
                    CheckCheerChipClock(p, chip);
                    // SPEC4 §11.4 비평 정적 줄: 응원 칩 ↔ 지킴이 받침 ≥ 8 (IdleChestWidget · StatsButton은 아래 HUD 칸에 든다)
                    int pads = ExpectPadClearance(p, rect, CheerChipClearance, "응원 칩");
                    p.Info($"띄움을 잰 지킴이 받침 {pads}개");
                    // 다른 HUD 칸 = 보이는 Hud · MetaHud 칸 전부 (VisibleHudCells — 칩 자신 · 조상 · 자손은 뺀다; V2 r4: 예전에는 이름 붙인 넷만 봤다)
                    string chipPath = PathOf(chip.transform);
                    int tested = 0;
                    foreach (var (cell, otherRect) in VisibleHudCells())
                    {
                        if (cell == chipPath || cell.StartsWith(chipPath + "/", StringComparison.Ordinal) || chipPath.StartsWith(cell + "/", StringComparison.Ordinal)) continue;
                        tested++;
                        var padded = Rect.MinMaxRect(otherRect.xMin - CheerChipClearance, otherRect.yMin - CheerChipClearance, otherRect.xMax + CheerChipClearance, otherRect.yMax + CheerChipClearance);
                        if (VerifyMath.Overlaps(rect, padded)) p.Expect(false, $"칩이 {cell} {R(otherRect)}와 {CheerChipClearance:0}px 안");
                    }
                    p.Expect(tested > 0, $"띄움을 잰 HUD 칸 {tested}개");
                }
                Report("S4-22c", "cheer_chip", p);
            });
        }

        /// <summary>
        /// 22b 점수 정렬 (P4-1 StrengthenAdvisor.GetRows · P4-6 이름 StrengthenPopup._advice): 전투력 줄(Gain &gt; 0)이 먼저 StrengthenAdvisor.Score 내림차순,
        /// 안내 줄(Gain 0 — 진화 준비 · 약한 방향 · 모자란 재료)은 그 뒤. 줄이 둘 미만이면 기록만
        /// </summary>
        static void CheckAdviceOrder(Probe p, UIPopup popup)
        {
            var advice = Field<List<StrengthenRow>>(popup, "_advice");
            if (advice == null)
            {
                p.Expect(false, "StrengthenPopup._advice 없음 (필드 이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            var order = new StringBuilder();
            string bad = null;
            bool infoSeen = false;
            double last = double.MaxValue;
            for (int i = 0; i < advice.Count; i++)
            {
                var row = advice[i];
                if (row == null) continue;
                double score = StrengthenAdvisor.Score(row);
                bool gain = row.Gain > 0;
                order.Append(order.Length > 0 ? " · " : string.Empty).Append(row.Kind).Append(gain ? $" {score:0.#}" : " (안내)");
                if (bad != null) continue;
                if (gain && infoSeen) bad = $"{i + 1}번째 전투력 줄 {row.Kind}이 안내 줄 뒤";
                else if (gain && score > last + 1e-6) bad = $"{i + 1}번째 줄 {row.Kind} 점수 {score:0.#} > 앞 줄 {last:0.#}";
                if (gain) last = score;
                else infoSeen = true;
            }
            if (advice.Count < 2) p.Info($"조언 줄 {advice.Count}개 ({order}) — 정렬 규칙을 재지 못함");
            else p.Expect(bad == null, $"점수 정렬 {order}{(bad != null ? $" — {bad}" : string.Empty)} (기대 전투력 줄 점수 내림차순, 안내 줄은 뒤)");
        }

        /// <summary>22c 칩 글 (P4-3b CheerChipView._time 'm:ss' — 짝짝이 그림이 '응원'을 맡는다): m:ss 꼴, 남은 응원 시간(Cheer.Remaining)과 ±2초, 응원 길이 이하</summary>
        static void CheckCheerChipClock(Probe p, Component chip)
        {
            var label = Field<TMP_Text>(chip, "_time");
            string shown = label != null ? ShownText(label) : null;
            var match = shown != null ? Regex.Match(shown, @"^(\d+):(\d\d)$") : Match.Empty;
            float remaining = Game.Cheer != null ? Game.Cheer.Remaining : -1f;
            int duration = Game.MetaDb != null && Game.MetaDb.cheer != null ? Game.MetaDb.cheer.durationSeconds : 600;
            if (!match.Success)
            {
                p.Expect(false, $"칩 시계 CheerChipView._time '{shown ?? "없음"}' (기대 'm:ss' — 예: '9:59')");
                return;
            }
            int seconds = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            p.Expect(seconds <= duration && Mathf.Abs(seconds - remaining) <= 2f,
                $"칩 시계 '{shown}' = {seconds}초 · 남은 응원 {remaining:0.#}초 · 응원 길이 {duration}초 (기대 ±2초 · 길이 이하)");
        }

        /// <summary>
        /// S4-30-entry · S4-30-entry-open (Q-05, P4-5 요청 #10a): 친구들 탭 머리의 CodexButton 글 — 도감이 잠긴 1-10 클리어 전에는 예전 '도감 보유/전체',
        /// 1-10을 깨면(콘텐츠 AtStage(Codex, 1, 10) · CodexSystem.IsUnlocked가 따른다) '도감 n%'. 그림은 찍지 않는다 (early 판의 그림 번호를 밀지 않게)
        /// </summary>
        IEnumerator CodexEntry(bool open)
        {
            string id = open ? "S4-30-entry-open" : "S4-30-entry";
            CloseAll();
            _tabs.Select(MainTab.Characters);
            yield return WaitSettled();
            var panel = UIFrame.SheetOpen ? FindAnyObjectByType<CharacterPanel>() : null;
            var button = panel != null ? FindDeep(panel.transform, "CodexButton") : null;
            string label = button != null ? FindText(button, "도감") : null;
            bool unlocked = Game.Codex != null && Game.Codex.IsUnlocked;
            string state = $"최고 {Game.Battle.HighestStage} ({BattleSystem.FormatStage(Math.Max(1, Game.Battle.HighestStage - 1))}까지 깸) · Codex.IsUnlocked {unlocked}";
            if (panel == null) SkipCheck(id, $"codex_entry: 친구들 시트가 열리지 않는다 · {state}");
            else if (button == null) Check(id, false, $"codex_entry: 친구들 탭 CodexButton 없음 · {state}");
            else if (open) Check(id, unlocked && label != null && Regex.IsMatch(label, @"^도감 \d+%$"), $"codex_entry: 1-10을 깬 뒤 CodexButton '{label}' (기대 '도감 n%') · {state}");
            else Check(id, !unlocked && label != null && Regex.IsMatch(label, @"^도감 \d+\s*/\s*\d+$"),
                $"codex_entry: 1-10 클리어 전 CodexButton '{label}' (기대 예전 '도감 보유/전체' — '도감 n%'가 아님) · {state}");
            UINav.GoToMain(true);
        }

        /// <summary>
        /// 30 codex_friends (P4-6): 1-10 — 도감 친구 쪽 (% 머리 · 5열 격자 · 못 찾은 실루엣 · 받을 리본). 칸 = CodexCellView: 못 찾은 칸은 그림 색 SilhouetteColor ·
        /// 이름 '???', 받을 수 있는 칸만 리본(Cell/Ribbon) 켜짐. 창이 없으면 SKIP
        /// </summary>
        IEnumerator CodexFriends()
        {
            const string id = "S4-30";
            if (!Landed(id, "codex_friends", "P4-6", "CodexPopup")) yield break;
            CloseAll();
            UINav.OpenCodex();
            yield return Wait(1.2f);
            var popup = FindPopup("CodexPopup");
            yield return Shot("codex_friends", () =>
            {
                var p = new Probe();
                p.Expect(popup != null && popup.IsOpen, "도감 열림 (UINav.OpenCodex)");
                if (popup != null)
                {
                    p.Expect(FindText(popup.transform, "%") != null, $"% 머리 '{FindText(popup.transform, "%")}'");
                    var grid = popup.GetComponentInChildren<GridLayoutGroup>(false);
                    p.Expect(grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount == 5,
                        $"격자 {(grid != null ? $"{grid.constraint} {grid.constraintCount}" : "없음")} (기대 5열)");
                    CheckCodexCells(p, popup.transform);
                }
                Report(id, "codex_friends", p);
            });
            CloseAll();
        }

        /// <summary>도감 칸 (P4-6 이름): 못 찾은 칸 = 실루엣 색 · '???', 리본은 받을 수 있는 칸에만. 못 찾은 칸이 하나도 없으면(1-10에 다 모음) 실루엣은 기록만</summary>
        static void CheckCodexCells(Probe p, Transform root)
        {
            var cells = root.GetComponentsInChildren<CodexCellView>(false);
            int unfound = 0, badSilhouette = 0, claimable = 0, ribbons = 0, badRibbon = 0;
            string firstBad = null;
            foreach (var cell in cells)
            {
                var status = cell.Status;
                bool found = status != null && status.Found;
                bool canClaim = status != null && status.Claimable;
                var portrait = Field<Image>(cell, "_portrait");
                var nameText = Field<TMP_Text>(cell, "_nameText");
                var ribbon = Field<GameObject>(cell, "_ribbon");
                bool ribbonOn = ribbon != null && ribbon.activeInHierarchy;
                if (!found)
                {
                    unfound++;
                    bool dark = portrait == null || !portrait.enabled || ColorNear(portrait.color, CodexCellView.SilhouetteColor, 0.02f);
                    bool hidden = nameText == null || ShownText(nameText) == "???";
                    if (!dark || !hidden)
                    {
                        badSilhouette++;
                        firstBad ??= $"{cell.name} 그림 색 {(portrait != null ? portrait.color.ToString() : "없음")} · 이름 '{(nameText != null ? ShownText(nameText) : "없음")}'";
                    }
                }
                if (canClaim) claimable++;
                if (ribbonOn) ribbons++;
                if (ribbonOn != canClaim)
                {
                    badRibbon++;
                    firstBad ??= $"{cell.name} 리본 {OpenText(ribbonOn)} · 받기 {canClaim}";
                }
            }
            p.Expect(cells.Length > 0, $"도감 칸 {cells.Length}개 (CodexCellView)");
            if (unfound > 0) p.Expect(badSilhouette == 0, $"못 찾은 칸 {unfound}개 — 실루엣 · '???' 아닌 칸 {badSilhouette}개");
            else p.Info("못 찾은 칸 0 — 실루엣 규칙은 기록만 (다 모음)");
            p.Expect(badRibbon == 0, $"리본 {ribbons}개 · 받을 칸 {claimable}개 — 어긋난 칸 {badRibbon}개 (리본은 받을 칸에만)");
            if (firstBad != null) p.Info($"첫 어긋남: {firstBad}");
        }

        static string OnText(bool on) => on ? "켜짐" : "꺼짐";

        static bool ColorNear(Color a, Color b, float tolerance) =>
            Mathf.Abs(a.r - b.r) <= tolerance && Mathf.Abs(a.g - b.g) <= tolerance && Mathf.Abs(a.b - b.b) <= tolerance && Mathf.Abs(a.a - b.a) <= tolerance;

        /// <summary>
        /// 17b expedition_ladder · 17c resend_all (IDLE-04, P4-4): 2-1 — 원정 준비의 단계 칸 셋(exp_2_4는 둘) · 기본 = 지난 단계; 모두 받기 뒤 같은 자리에 ResendAllButton,
        /// ClaimAllButton 숨김. 단계 칸 · 다시 보내기 단추가 아직 없으면 SKIP
        /// </summary>
        IEnumerator ExpeditionLadder()
        {
            CloseAll();
            _tabs.Select(MainTab.Expedition);
            yield return WaitSettled();
            var panel = FindAnyObjectByType<ExpeditionPanel>();
            if (!UIFrame.SheetOpen || panel == null)
            {
                SkipCheck("S4-17b", "expedition_ladder: 원정 시트가 열리지 않는다");
                SkipCheck("S4-17c", "resend_all: 원정 시트가 열리지 않는다");
                UINav.GoToMain(true);
                yield break;
            }
            var seg = Field<Button>(panel, "_expeditionSeg");
            if (seg != null) seg.onClick.Invoke();
            yield return Wait(0.4f);
            ExpeditionZoneCardView card = null;
            Button send = null;
            foreach (var candidate in panel.GetComponentsInChildren<ExpeditionZoneCardView>(false))
            {
                var button = Field<Button>(candidate, "_sendButton");
                if (candidate.Zone == null || button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (Game.Expedition.GetActive(candidate.Zone.id) != null) continue;
                card = candidate;
                send = button;
                break;
            }
            var setup = UIManager.Instance.Get<ExpeditionSetupPopup>();
            if (card == null || setup == null)
            {
                SkipCheck("S4-17b", "expedition_ladder: 보낼 수 있는 구역 카드 · 준비 팝업 없음");
            }
            else
            {
                send.onClick.Invoke();
                yield return Wait(0.8f);
                var tiers = new List<Button>();
                foreach (var button in setup.GetComponentsInChildren<Button>(false))
                {
                    if (button.name.StartsWith("Tier", StringComparison.Ordinal) || button.name.StartsWith("Seg_Tier", StringComparison.Ordinal)) tiers.Add(button);
                }
                if (tiers.Count == 0)
                {
                    SkipCheck("S4-17b", $"expedition_ladder: 아직 없음 — 원정 단계 칸 Tier* (P4-4 IDLE-04) · 구역 {card.Zone.id}");
                }
                else
                {
                    string zone = card.Zone.id;
                    yield return Shot("expedition_ladder", () =>
                    {
                        var p = new Probe();
                        int expected = zone == "exp_2_4" ? 2 : 3;
                        p.Expect(tiers.Count == expected, $"구역 {zone} 단계 칸 {tiers.Count}개 (기대 {expected})");
                        ExpectDefaultTier(p, setup, card.Zone);
                        Report("S4-17b", "expedition_ladder", p);
                    });
                    // 1단계로 보낸다 — 17c 뒤 같은 구역 준비 창의 첫 칸이 그 단계인지 본다 (S4-17b-last: 기본 = 지난 단계)
                    var tierButtons = Field<Button[]>(setup, "_tierButtons");
                    if (Game.Expedition.IsTierAllowed(card.Zone, 1) && tierButtons != null && tierButtons.Length > 1 && tierButtons[1] != null && tierButtons[1].gameObject.activeInHierarchy)
                    {
                        tierButtons[1].onClick.Invoke();
                        _ladderZone = card.Zone;
                        yield return Wait(0.3f);
                    }
                }
                var start = Field<Button>(setup, "_startButton");
                if (setup.IsOpen && start != null && start.interactable) start.onClick.Invoke();
                yield return Wait(0.5f);
                CloseAll();
            }

            var resend = FindDeep(Find(CoachTargets.ExpeditionPanel), "ResendAllButton");
            var claimAll = Field<Button>(panel, "_claimAllButton");
            if (resend == null || claimAll == null)
            {
                SkipCheck("S4-17c", "resend_all: 아직 없음 — ResendAllButton (P4-4 IDLE-04)");
                UINav.GoToMain(true);
                yield break;
            }
            // 보낸 원정을 모두 끝난 것으로 → 모두 받기
            int finished = 0;
            foreach (var candidate in panel.GetComponentsInChildren<ExpeditionZoneCardView>(false))
            {
                var active = candidate.Zone != null ? Game.Expedition.GetActive(candidate.Zone.id) : null;
                if (active == null) continue;
                active.endUnix = active.startUnix;
                finished++;
            }
            yield return Wait(1.5f);
            var claimRect = ScreenRect((RectTransform)claimAll.transform);
            if (claimAll.gameObject.activeInHierarchy && claimAll.interactable) claimAll.onClick.Invoke();
            yield return Wait(0.8f);
            CloseAll();
            yield return Wait(0.4f);
            yield return Shot("resend_all", () =>
            {
                var p = new Probe();
                p.Info($"끝낸 원정 {finished}개");
                p.Expect(resend.gameObject.activeInHierarchy, "ResendAllButton 보임");
                p.Expect(!claimAll.gameObject.activeInHierarchy, "ClaimAllButton 숨김");
                var rect = ScreenRect(resend);
                p.Expect(VerifyMath.Near(rect, claimRect, 2f), $"ResendAllButton {R(rect)} (기대 ClaimAll 자리 {R(claimRect)})");
                Report("S4-17c", "resend_all", p);
            });
            yield return LadderLastTier(panel);
            UINav.GoToMain(true);
        }

        /// <summary>17b에서 1단계로 보낸 구역 (S4-17b-last가 다시 연다 — 없으면 null)</summary>
        ExpeditionZoneDef _ladderZone;

        /// <summary>
        /// 17b '기본 = 지난 단계' (IDLE-04, P4-4 이름 ExpeditionSetupPopup._tier · _tierButtons): 고른 단계 = 지난 단계(ExpeditionSystem.GetLastTier)를 지금 고를 수 있으면 그것,
        /// 아니면 고를 수 있는 첫 단계 (ExpeditionSetupPopup.DefaultTier와 같은 규칙)
        /// </summary>
        static void ExpectDefaultTier(Probe p, ExpeditionSetupPopup setup, ExpeditionZoneDef zone)
        {
            var field = typeof(ExpeditionSetupPopup).GetField("_tier", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || zone == null)
            {
                p.Expect(false, "ExpeditionSetupPopup._tier 없음 (필드 이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            int chosen = (int)field.GetValue(setup);
            int last = Game.Expedition.GetLastTier(zone.id);
            int expected = -1;
            if (Game.Expedition.IsTierAllowed(zone, last)) expected = last;
            for (int tier = 0; expected < 0 && tier < ExpeditionSystem.DurationTierCount; tier++)
            {
                if (Game.Expedition.IsTierAllowed(zone, tier)) expected = tier;
            }
            p.Expect(chosen == expected, $"처음 고른 단계 {chosen} · 지난 단계 {last} (기대 {expected} — 지난 단계를 고를 수 있으면 그것)");
        }

        /// <summary>
        /// S4-17b-last: 17b에서 1단계로 보낸 구역을 (모두 받기 뒤) 다시 열면 첫 칸이 1단계다 (ExpeditionSystem.GetLastTier — 기본 = 지난 단계). 그림 없음.
        /// 1단계를 고를 수 없었으면(사다리 잠김) SKIP
        /// </summary>
        IEnumerator LadderLastTier(ExpeditionPanel panel)
        {
            const string id = "S4-17b-last";
            var zone = _ladderZone;
            _ladderZone = null;
            if (zone == null)
            {
                SkipCheck(id, "expedition_ladder: 17b에서 1단계로 보내지 못했다 (사다리 잠김 · 단계 칸 없음) — 지난 단계 기본을 재지 못함");
                yield break;
            }
            ExpeditionZoneCardView card = null;
            foreach (var candidate in panel.GetComponentsInChildren<ExpeditionZoneCardView>(false))
            {
                if (candidate.Zone != null && candidate.Zone.id == zone.id) card = candidate;
            }
            var setup = UIManager.Instance.Get<ExpeditionSetupPopup>();
            if (card == null || setup == null || Game.Expedition.GetActive(zone.id) != null)
            {
                SkipCheck(id, $"expedition_ladder: 구역 {zone.id}를 다시 열 수 없다 (카드 {(card != null ? "있음" : "없음")} · 진행 중 {Game.Expedition.GetActive(zone.id) != null}) — 지난 단계 기본을 재지 못함");
                yield break;
            }
            // 카드의 지난 단계 칩 (P4-4: 지난번이 1단계 이상이면 카드에 '…' 칩) — 기록
            var chip = Field<GameObject>(card, "_tierChip");
            var chipText = Field<TMP_Text>(card, "_tierChipText");
            string chipLine = chip == null ? "칩 없음" : chip.activeInHierarchy ? $"칩 '{(chipText != null ? ShownText(chipText) : string.Empty)}'" : "칩 꺼짐";
            // 카드 단추는 지난 친구로 바로 다시 보낸다 ('다시 보내기', M57 — r9: 준비 창이 안 열렸다) → 준비 창은 게임의 다른 길 ExpeditionPanel.OpenSetup으로 연다
            panel.OpenSetup(card);
            yield return Wait(0.8f);
            var p = new Probe();
            p.Expect(setup.IsOpen, $"구역 {zone.id} 준비 창 다시 열림 (ExpeditionPanel.OpenSetup)");
            p.Info($"지난 단계 GetLastTier {Game.Expedition.GetLastTier(zone.id)} (17b에서 1단계로 보냄) · 카드 {chipLine}");
            ExpectDefaultTier(p, setup, zone);
            p.Expect(Game.Expedition.GetLastTier(zone.id) == 1, $"지난 단계 기억 {Game.Expedition.GetLastTier(zone.id)} (기대 1)");
            Report(id, "expedition_ladder", p);
            CloseAll();
        }

        /// <summary>
        /// 05b danger_arrow_vs_guide (§13.5 e, P4-3b): 2-1 MAIN — 10초 동안 프레임마다 보이는 위험 화살표(DangerArrowView 빛 · 갈매기)가 가이드 퀘스트 줄 + 8px와 겹치지 않는다.
        /// DangerArrowView에 가이드 줄 비키기(이름에 guide가 든 멤버)가 아직 없으면 잰 값만 남기고 SKIP. 화살표가 한 번도 안 보이면 SKIP
        /// </summary>
        IEnumerator DangerArrowGuide()
        {
            const string id = "S4-05b";
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            var guide = Find(CoachTargets.GuideQuestBar);
            if (guide == null || !IsVisible(guide))
            {
                SkipCheck(id, "danger_arrow_vs_guide: 가이드 퀘스트 줄이 안 보인다");
                yield break;
            }
            var arrows = FindObjectsByType<DangerArrowView>(FindObjectsSortMode.None);
            int frames = 0, seen = 0, hits = 0;
            string first = null;
            float limit = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < limit)
            {
                frames++;
                var bar = ScreenRect(guide);
                var padded = Rect.MinMaxRect(bar.xMin - 8f, bar.yMin - 8f, bar.xMax + 8f, bar.yMax + 8f);
                bool any = false;
                foreach (var arrow in arrows)
                {
                    if (arrow == null || !arrow.isActiveAndEnabled) continue;
                    if (!ArrowRect(arrow, out var rect)) continue;
                    any = true;
                    if (!VerifyMath.Overlaps(rect, padded)) continue;
                    hits++;
                    first ??= $"{arrow.name} {R(rect)} · 줄+8 {R(padded)}";
                }
                if (any) seen++;
                yield return null;
            }
            yield return Shot("danger_arrow_vs_guide");
            string measured = $"danger_arrow_vs_guide: {frames}프레임 중 화살표 보인 {seen} · 줄 8px 안 {hits}{(first != null ? " — 처음 " + first : string.Empty)}";
            if (seen == 0)
            {
                SkipCheck(id, measured + " (화살표가 한 번도 안 보였다)");
                yield break;
            }
            if (!HasMemberLike(typeof(DangerArrowView), "guide"))
            {
                SkipCheck(id, measured + " — 아직 없음: DangerArrowView 가이드 줄 비키기 (P4-3b §13.5 e)");
                yield break;
            }
            Check(id, hits == 0, measured + " (기대 0)");
        }

        /// <summary>위험 화살표의 보이는 그래픽(빛 · 갈매기) 합친 화면 사각형. 안 보이면 false</summary>
        bool ArrowRect(DangerArrowView arrow, out Rect rect)
        {
            rect = default;
            bool any = false;
            foreach (var graphic in arrow.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || graphic.color.a * EffectiveAlpha(graphic.transform) < 0.1f) continue;
                var r = ScreenRect(graphic.rectTransform);
                rect = any ? Union(rect, r) : r;
                any = true;
            }
            return any;
        }

        /// <summary>23b journal_records (P4-6 · P4-1): 2-3 — 수선 일지 기록 쪽 (받을 것 먼저 · 모두 받기 · 탭 점). 기록 쪽이 아직 없으면 SKIP</summary>
        IEnumerator JournalRecords()
        {
            const string id = "S4-23b";
            if (!Landed(id, "journal_records", "P4-6 · P4-1", "JournalRowModel")) yield break;
            CloseAll();
            UINav.GoToFeature(FeatureId.Achievements);
            yield return Wait(1.2f);
            var journal = UIManager.Instance.Get<JournalPopup>();
            yield return Shot("journal_records", () =>
            {
                var p = new Probe();
                p.Expect(journal != null && journal.IsOpen, "수선 일지 열림 (GoToFeature Achievements → 기록 쪽)");
                if (journal != null) CheckJournalRecords(p, journal);
                Report(id, "journal_records", p);
            });
            CloseAll();
        }

        /// <summary>
        /// 23b 기록 쪽 (P4-6 이름 JournalPopup._recordsPanel · _recordsListContent · _claimAllButton · _tabBadges[2]): 기록 쪽이 보이고, 켜진 줄(MissionRowView ← JournalRowModel)은
        /// 받을 것(CanClaim)이 먼저, [모두 받기]는 받을 기록이 있을 때만(Achievements.ClaimableCount), 기록 탭 점도 그때만. 받을 기록이 0이면 정렬은 기록만
        /// </summary>
        static void CheckJournalRecords(Probe p, JournalPopup journal)
        {
            var panel = Field<GameObject>(journal, "_recordsPanel");
            var content = Field<RectTransform>(journal, "_recordsListContent");
            var claimAll = Field<Button>(journal, "_claimAllButton");
            var badges = Field<Badge[]>(journal, "_tabBadges");
            p.Expect(panel != null && panel.activeInHierarchy, $"기록 쪽 _recordsPanel {(panel != null ? OpenText(panel.activeInHierarchy) : "없음")}");
            int claimable = Game.Achievements != null ? Game.Achievements.ClaimableCount : -1;
            if (content == null)
            {
                p.Expect(false, "JournalPopup._recordsListContent 없음 (필드 이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            int rows = 0, claimRows = 0;
            string bad = null;
            bool otherSeen = false;
            for (int i = 0; i < content.childCount; i++)
            {
                var row = content.GetChild(i).GetComponent<MissionRowView>();
                if (row == null || !row.gameObject.activeSelf || !row.HasModel) continue;
                rows++;
                bool can = row.Model.CanClaim;
                if (can) claimRows++;
                if (can && otherSeen) bad ??= $"{rows}번째 줄 '{row.Model.Text}'(받기)이 받을 것 아닌 줄 뒤";
                if (!can) otherSeen = true;
            }
            p.Expect(rows > 0, $"기록 줄 {rows}개 · 받을 줄 {claimRows}개 · 받을 기록 {claimable}개");
            if (claimRows == 0) p.Info("받을 줄 0 — '받을 것 먼저' 규칙을 재지 못함");
            else p.Expect(bad == null, $"받을 것 먼저{(bad != null ? $" — {bad}" : string.Empty)}");
            bool claimAllOn = claimAll != null && claimAll.gameObject.activeInHierarchy;
            p.Expect(claimAll != null && claimAllOn == claimable > 0, $"[모두 받기] {(claimAll != null ? OpenText(claimAllOn) : "없음")} (기대 받을 기록이 있을 때만 — {claimable}개)");
            // 기록 탭 = JournalPopup.Tab.Records (2 — 비공개 열거라 수로 쓴다)
            const int recordsTab = 2;
            var dot = badges != null && badges.Length > recordsTab ? badges[recordsTab] : null;
            bool dotOn = dot != null && dot.gameObject.activeInHierarchy;
            p.Expect(dot != null && dotOn == claimable > 0, $"기록 탭 점 {(dot != null ? OnText(dotOn) : "없음")} (기대 받을 기록이 있을 때만)");
        }

        /// <summary>
        /// 20b gacha_reveal_1 · 20c gacha_reveal_10 (P4-5): Pull(1) 0.5초 · Pull(10) 1.0초에 찍는다 — 꾸러미가 보이고, 건너뛰기 단추는 0.4초에 없고 0.6초에 있다,
        /// ≥4★ 없는 10회는 4초 안에 끝난다. GachaRevealTiming이 아직 없으면 SKIP
        /// </summary>
        IEnumerator GachaReveal()
        {
            if (!Landed("S4-20b", "gacha_reveal_1", "P4-5", "GachaRevealTiming"))
            {
                SkipCheck("S4-20c", "gacha_reveal_10: 아직 없음 — GachaRevealTiming (P4-5)");
                yield break;
            }
            CloseAll();
            Game.Currency.Add(CurrencyType.Gem, 5000);
            _tabs.Select(MainTab.Gacha);
            yield return WaitSettled();
            var panel = FindAnyObjectByType<GachaPanel>();
            var result = UIManager.Instance.Get<GachaResultPopup>();
            if (panel == null || result == null)
            {
                SkipCheck("S4-20b", "gacha_reveal_1: 소환 시트 · 결과 창 없음");
                SkipCheck("S4-20c", "gacha_reveal_10: 소환 시트 · 결과 창 없음");
                yield break;
            }
            panel.Pull(1);
            yield return Wait(0.5f);
            yield return Shot("gacha_reveal_1", () =>
            {
                var p = new Probe();
                p.Expect(result.IsOpen, "결과 창 열림");
                var bundle = FindNamePart(result.transform, "Bundle");
                p.Expect(bundle != null && IsVisible(bundle), $"꾸러미 {(bundle != null ? bundle.name : "없음")} 보임");
                Report("S4-20b", "gacha_reveal_1", p);
            });
            CloseAll();
            yield return Wait(0.3f);
            float start = Time.realtimeSinceStartup;
            panel.Pull(10);
            // 공개가 끝나는 때(GachaResultPopup.Finish → _buttonsVisible)를 프레임마다 따로 지켜본다 — 찍는 동안에도 놓치지 않게
            _gachaFinished = -1f;
            var watch = StartCoroutine(WatchGachaFinish(result, start));
            // 건너뛰기 칩 = GachaResultPopup._skipChip (GachaRevealTiming.SkipAppear 뒤에 켠다 — 화면 전체 누름 _skipButton과 다르다)
            var chip = Field<Button>(result, "_skipChip");
            yield return Wait(0.4f);
            bool earlyShown = chip != null && chip.gameObject.activeInHierarchy;
            yield return Wait(0.2f);
            bool lateShown = chip != null && chip.gameObject.activeInHierarchy;
            float wait = 1.0f - (Time.realtimeSinceStartup - start);
            if (wait > 0f) yield return Wait(wait);
            yield return Shot("gacha_reveal_10");
            float limit = start + GachaFinishWait;
            while (_gachaFinished < 0f && Time.realtimeSinceStartup < limit) yield return null;
            if (watch != null) StopCoroutine(watch);
            var results = Field<List<GachaResult>>(result, "_results");
            int high = 0, count = results != null ? results.Count : 0;
            if (results != null)
            {
                foreach (var r in results)
                {
                    if (r?.Lineage != null && GachaRevealTiming.IsHigh(r.Lineage.grade)) high++;
                }
            }
            var p = new Probe();
            p.Expect(chip != null, "GachaResultPopup._skipChip");
            p.Expect(!earlyShown, "0.4초: 건너뛰기 칩 없음");
            p.Expect(lateShown, "0.6초: 건너뛰기 칩 있음");
            p.Expect(results != null && count == 10, $"결과 {count}장 (GachaResultPopup._results)");
            float model = GachaRevealTiming.MultiDuration(count, high);
            string took = _gachaFinished >= 0f ? $"{_gachaFinished:0.00}초" : $"{GachaFinishWait:0}초 안에 안 끝남";
            if (high == 0) p.Expect(_gachaFinished >= 0f && _gachaFinished <= GachaRevealTiming.MaxTenWithoutHigh, $"★4 이상 없는 10회 공개 {took} (기대 ≤ {GachaRevealTiming.MaxTenWithoutHigh:0.#}초 · 모형 {model:0.00}초)");
            else p.Expect(_gachaFinished >= 0f && _gachaFinished <= model + 0.5f, $"★4 이상 {high}장 — 공개 {took} (≤4초 규칙은 ★4 이상 없을 때만 · 기대 모형 {model:0.00}초 + 0.5)");
            Report("S4-20c", "gacha_reveal_10", p);
            CloseAll();
            UINav.GoToMain(true);
        }

        /// <summary>20c: 10회 공개가 끝난 시각 (Pull부터 초, 아직이면 −1)</summary>
        float _gachaFinished = -1f;

        /// <summary>결과 창의 _buttonsVisible(Finish가 켠다)이 켜지는 첫 프레임까지 지켜본다 (늦어도 GachaFinishWait초)</summary>
        IEnumerator WatchGachaFinish(GachaResultPopup result, float start)
        {
            var field = typeof(GachaResultPopup).GetField("_buttonsVisible", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) yield break;
            // Pull 직후에는 이전 공개의 값이 남아 있을 수 있다 — 한 번 꺼진(새 공개가 시작된) 뒤부터 센다
            bool started = false;
            while (Time.realtimeSinceStartup - start < GachaFinishWait)
            {
                bool visible = field.GetValue(result) is bool on && on;
                if (!visible) started = true;
                else if (started)
                {
                    _gachaFinished = Time.realtimeSinceStartup - start;
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>41 affinity_section (P4-5 · P4-1): 친구 상세 정보 탭의 호감도 칸 (Lv · 막대 · 이야기 줄 · 받을 때 받기 단추). 칸이 아직 없으면 SKIP</summary>
        IEnumerator AffinitySection()
        {
            const string id = "S4-41";
            if (!Landed(id, "affinity_section", "P4-5", "AffinitySectionView")) yield break;
            CloseAll();
            UINav.OpenCharacter(FtueFriend, CharacterDetailPopup.TabInfo);
            yield return Wait(1.2f);
            var view = FindView("AffinitySectionView");
            yield return Shot("affinity_section", () =>
            {
                var p = new Probe();
                p.Expect(view != null && IsVisible(view.transform), "호감도 칸이 정보 탭에 보인다");
                if (view != null) p.Info($"Lv 글 '{FindText(view.transform, "Lv")}' (이름 · 글 고정은 part B)");
                // 머리 초상화의 Lv5 하트 바느질 틀 (P4-5 #4 · 요청 #10b): CharacterDetailPopup/…/PortraitFrame/AffinityFrame — 호감도 Lv5 아래에서는 꺼짐
                var detail = UIManager.Instance.Get<CharacterDetailPopup>();
                var frame = detail != null ? Field<GameObject>(detail, "_portraitAffinityFrame") : null;
                int level = Game.Affinity != null ? Game.Affinity.GetLevel(FtueFriend) : -1;
                bool wantFrame = Game.IsMetaActive && level >= CharacterCardView.AffinityFrameLevel;
                p.Expect(frame != null, $"머리 초상화 하트 틀 CharacterDetailPopup._portraitAffinityFrame {(frame != null ? PathOf(frame.transform) : "없음")}");
                if (frame != null)
                    p.Expect(frame.activeSelf == wantFrame, $"하트 틀 {OpenText(frame.activeSelf)} · 호감도 Lv{level} (기대 Lv{CharacterCardView.AffinityFrameLevel} 이상에서만 켜짐)");
                if (view != null) CheckAffinitySection(p, view, level);
                Report(id, "affinity_section", p);
            });
            CloseAll();
            yield return AffinityDue();
        }

        /// <summary>
        /// 41 호감도 칸 (P4-5 이름 AffinitySectionView._levelText · _barFill · _storyLines · _claimButton): 'Lv.n 호감도', 막대가 켜짐, 기억 조각 5줄 —
        /// 단계 2·4·6·8·10에 닿은 줄만 열리고(이야기 글) 나머지는 'Lv.n에 들을 수 있어요', [받기]는 받을 단계가 있을 때만 (Affinity.HasClaimable)
        /// </summary>
        static void CheckAffinitySection(Probe p, Component view, int level)
        {
            var levelText = Field<TMP_Text>(view, "_levelText");
            var bar = Field<RectTransform>(view, "_barFill");
            var lines = Field<TMP_Text[]>(view, "_storyLines");
            var claim = Field<Button>(view, "_claimButton");
            string shownLevel = levelText != null ? ShownText(levelText) : null;
            p.Expect(shownLevel == $"Lv.{level} 호감도", $"단계 글 '{shownLevel ?? "없음"}' (기대 'Lv.{level} 호감도')");
            p.Expect(bar != null && bar.gameObject.activeInHierarchy, $"막대 _barFill {(bar != null ? OpenText(bar.gameObject.activeInHierarchy) : "없음")}");
            if (lines == null || lines.Length < AffinitySectionView.StoryCount)
                p.Expect(false, $"기억 조각 줄 {lines?.Length ?? 0}개 (기대 {AffinitySectionView.StoryCount})");
            else
            {
                int open = 0;
                string bad = null;
                for (int i = 0; i < AffinitySectionView.StoryCount; i++)
                {
                    string text = lines[i] != null ? ShownText(lines[i]) : string.Empty;
                    bool locked = text.Contains("에 들을 수 있어요");
                    bool want = level >= AffinitySystem.StoryUnlockLevel(i);
                    if (!locked && !string.IsNullOrEmpty(text)) open++;
                    if (bad == null && (string.IsNullOrEmpty(text) || locked == want)) bad = $"{i + 1}번째 줄 '{text}' (Lv.{AffinitySystem.StoryUnlockLevel(i)} — 기대 {(want ? "열림" : "잠김")})";
                }
                p.Expect(bad == null, $"기억 조각 열린 줄 {open}/{AffinitySectionView.StoryCount} · Lv{level}{(bad != null ? $" — {bad}" : string.Empty)}");
            }
            bool due = Game.Affinity != null && Game.Affinity.HasClaimable(FtueFriend);
            bool claimOn = claim != null && claim.gameObject.activeInHierarchy;
            p.Expect(claim != null && claimOn == due, $"[받기] {(claim != null ? OpenText(claimOn) : "없음")} (기대 받을 단계가 있을 때만 — {(due ? "있음" : "없음")})");
        }

        /// <summary>
        /// S4-41-due (CR-10 '받을 때 받기 단추'): 짝짝이 호감도를 Lv5 시작 값으로 올리고 상세를 다시 연다 → 받기 단추 · Lv5 · 기억 조각 2줄 · 하트 틀이 켜지고,
        /// [받기]를 누르면 받을 단계가 없어지고 단추가 숨는다. 그림 없음 (early 판 번호를 밀지 않게)
        /// </summary>
        IEnumerator AffinityDue()
        {
            const string id = "S4-41-due";
            const int dueLevel = 5;
            var affinity = Game.IsMetaActive ? Game.Affinity : null;
            var save = Game.Characters != null ? Game.Characters.GetSave(FtueFriend) : null;
            if (affinity == null || save == null || affinity.MaxLevel < dueLevel)
            {
                SkipCheck(id, $"affinity_section: 호감도 시스템 · {FtueFriend} 세이브 없음 — 받을 단계를 만들 수 없다");
                yield break;
            }
            long start = affinity.GetLevelStart(dueLevel);
            if (start <= 0 || start > int.MaxValue)
            {
                SkipCheck(id, $"affinity_section: Lv{dueLevel} 시작 값 {start} — 받을 단계를 만들 수 없다");
                yield break;
            }
            save.affinity = Math.Max(save.affinity, (int)start);
            UINav.OpenCharacter(FtueFriend, CharacterDetailPopup.TabInfo);
            yield return Wait(1.2f);
            var view = FindView("AffinitySectionView");
            var detail = UIManager.Instance.Get<CharacterDetailPopup>();
            var frame = detail != null ? Field<GameObject>(detail, "_portraitAffinityFrame") : null;
            int level = affinity.GetLevel(FtueFriend);
            var p = new Probe();
            p.Expect(view != null && IsVisible(view.transform), "호감도 칸이 보인다");
            p.Expect(level == dueLevel, $"호감도 Lv{level} (시드 Lv{dueLevel} 시작 값 {start})");
            if (view != null) CheckAffinitySection(p, view, level);
            p.Expect(frame != null && frame.activeSelf == (level >= CharacterCardView.AffinityFrameLevel), $"하트 틀 {(frame != null ? OpenText(frame.activeSelf) : "없음")} · Lv{level}");
            var claim = view != null ? Field<Button>(view, "_claimButton") : null;
            if (claim != null && claim.gameObject.activeInHierarchy && claim.interactable)
            {
                claim.onClick.Invoke();
                yield return Wait(0.6f);
                bool stillDue = affinity.HasClaimable(FtueFriend);
                p.Expect(!stillDue && !claim.gameObject.activeInHierarchy, $"[받기]를 누른 뒤 받을 단계 {(stillDue ? "남음" : "없음")} · 단추 {OpenText(claim.gameObject.activeInHierarchy)} (기대 없음 · 닫힘)");
            }
            Report(id, "affinity_section", p);
            CloseAll();
        }

        /// <summary>
        /// 28e settings_account · 28f settings_notify (SET-01, P4-4 · P4-7): 설정 — BackupRow + CouponRow가 창 안, 알림 상태 줄이 권한과 맞는다.
        /// 줄이 아직 없으면 SKIP
        /// </summary>
        IEnumerator SettingsRows()
        {
            var settings = UIManager.Instance.Get<SettingsPopup>();
            if (settings == null)
            {
                SkipCheck("S4-28e", "settings_account: SettingsPopup 없음");
                SkipCheck("S4-28f", "settings_notify: SettingsPopup 없음");
                yield break;
            }
            CloseAll();
            settings.Open();
            yield return Wait(0.8f);
            var backup = FindDeep(settings.transform, "BackupRow");
            var coupon = FindDeep(settings.transform, "CouponRow");
            if (backup == null || coupon == null) SkipCheck("S4-28e", $"settings_account: 아직 없음 — {(backup == null ? "BackupRow " : string.Empty)}{(coupon == null ? "CouponRow" : string.Empty)} (P4-4 SET-01)");
            else
            {
                yield return OpenSettingsTabWith(settings, backup);
                yield return Shot("settings_account", () =>
                {
                    var p = new Probe();
                    var window = FindDeep(settings.transform, "Window");
                    var box = window != null ? ScreenRect(window) : ScreenRect((RectTransform)settings.transform);
                    foreach (var row in new[] { backup, coupon })
                    {
                        var rect = ScreenRect(row);
                        p.Expect(row.gameObject.activeInHierarchy, $"{row.name} 보임");
                        p.Expect(rect.yMin >= box.yMin - 0.5f && rect.yMax <= box.yMax + 0.5f && rect.xMin >= box.xMin - 0.5f && rect.xMax <= box.xMax + 0.5f,
                            $"{row.name} {R(rect)}이 창 {R(box)} 안");
                    }
                    Report("S4-28e", "settings_account", p);
                });
            }
            var status = FindDeep(settings.transform, "NotifyStatus");
            if (status == null) SkipCheck("S4-28f", "settings_notify: 아직 없음 — NotifyStatus (P4-4 SET-01)");
            else
            {
                yield return OpenSettingsTabWith(settings, status);
                yield return Shot("settings_notify", () =>
                {
                    var p = new Probe();
                    string line = FindText(status, string.Empty);
                    p.Expect(status.gameObject.activeInHierarchy && !string.IsNullOrEmpty(line), $"알림 상태 줄 '{line}'");
                    CheckNotifyStatus(p, settings);
                    Report("S4-28f", "settings_notify", p);
                });
            }
            CloseAll();
        }

        /// <summary>
        /// 28f 알림 상태 줄이 권한과 맞는다 (P4-4 · P4-7 NOTI-01): 지금 권한 = LocalNotifications.Effective (예약 계획과 같은 규칙), 글 = SettingsPopup.NotifyStatusText(권한)
        /// (internal — 이름으로 부른다), 단추(_notifyStatusButton)는 켜져 있으면 숨고 아니면 보인다
        /// </summary>
        static void CheckNotifyStatus(Probe p, SettingsPopup settings)
        {
            var label = Field<TMP_Text>(settings, "_notifyStatusText");
            var button = Field<Button>(settings, "_notifyStatusButton");
            var permission = LocalNotifications.Effective;
            var textOf = typeof(SettingsPopup).GetMethod("NotifyStatusText", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(NotificationPermission) }, null);
            string want = textOf != null ? textOf.Invoke(null, new object[] { permission }) as string : null;
            string shown = label != null ? ShownText(label) : null;
            if (label == null || want == null)
            {
                p.Expect(false, $"알림 상태 줄을 잴 수 없다 — SettingsPopup {(label == null ? "_notifyStatusText " : string.Empty)}{(want == null ? "NotifyStatusText(권한) " : string.Empty)}없음 (이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            p.Expect(shown == want, $"상태 줄 '{shown}' · 권한 {permission} (기기 {LocalNotifications.Permission} · 저장 {GameSettings.NotifyPermission}) — 기대 '{want}'");
            if (button != null)
            {
                bool on = button.gameObject.activeInHierarchy;
                bool wantButton = permission != NotificationPermission.Granted;
                p.Expect(on == wantButton, $"상태 단추 {OpenText(on)} '{FindText(button.transform, string.Empty)}' (기대 {(wantButton ? "보임 — 허용하기 · 설정 열기" : "숨김 — 켜져 있음")})");
            }
            else p.Info("상태 단추 _notifyStatusButton 없음 — 단추 규칙은 기록만");
        }

        /// <summary>설정의 Tab0..Tab3을 차례로 눌러 그 줄이 보이는 탭을 연다 (못 찾으면 그대로)</summary>
        IEnumerator OpenSettingsTabWith(SettingsPopup settings, RectTransform row)
        {
            for (int i = 0; i < 4 && !row.gameObject.activeInHierarchy; i++)
            {
                var tab = FindDeep(settings.transform, "Tab" + i);
                var button = tab != null ? tab.GetComponent<Button>() : null;
                if (button == null) continue;
                button.onClick.Invoke();
                yield return Wait(0.3f);
            }
        }

        // ═════════════════════════ chapter ═════════════════════════

        /// <summary>
        /// 07b boss_hint (§13.1, P4-3b · P4-8): 첫 1-10 보스를 부른 뒤 BossHintRule.Delay + 3초까지 — 제목 = 보스 이름 · 설명 = MetaDb.GetBossHint 글 · 띠 α ≥ 0.5인
        /// 안내 줄이 보이고(다른 안내 줄은 치지 않는다), Delay 전에는 뜨지 않으며, StagePanel 화남 눈금이 보인다. 그 보스에 단서 콘텐츠가 없으면 SKIP, 줄이 없으면 FAIL
        /// </summary>
        IEnumerator BossHint()
        {
            const string id = "S4-07b";
            if (!Landed(id, "boss_hint", "P4-3b §13.1", "BossHintRule")) yield break;
            var sim = Game.Battle.Sim;
            var spawn = typeof(BattleSimulator).GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic);
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            float ready = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < ready && (Game.Battle.IsBetweenStages || Game.Battle.Sim.Outcome != BattleOutcome.Running)) yield return null;
            if (spawn == null || sim == null || Game.Battle.Sim.Outcome != BattleOutcome.Running)
            {
                SkipCheck(id, "boss_hint: 보스를 부를 수 없다 (SpawnBoss · 스테이지)");
                yield break;
            }
            if (!Game.Battle.Sim.BossSpawned) spawn.Invoke(Game.Battle.Sim, null);
            float spawnedAt = Time.realtimeSinceStartup;
            // 그 보스의 단서 (콘텐츠 BossHintDef) — 없으면 볼 줄이 없다
            yield return null;
            var bossDef = Game.Battle.Sim.Boss != null ? Game.Battle.Sim.Boss.Def : null;
            var hint = bossDef != null && Game.MetaDb != null ? Game.MetaDb.GetBossHint(bossDef.id) : null;
            if (bossDef == null || hint == null || string.IsNullOrEmpty(hint.hint))
            {
                SkipCheck(id, $"boss_hint: {Game.Battle.StageLabel} 보스 {(bossDef != null ? bossDef.id : "없음")}에 단서 콘텐츠(BossHintDef) 없음 (P4-8)");
                yield break;
            }
            string wantTitle = VerifyMath.StripRichText(bossDef.name);
            string wantHint = VerifyMath.StripRichText(hint.hint);
            // 단서 줄 = 제목이 보스 이름 · 설명이 단서 글 · 띠 알파 ≥ 0.5 인 줄 (다른 안내 — 스테이지 꾸밈 · 좀나방 — 는 치지 않는다, V2 r4)
            var notice = FindAnyObjectByType<BattleNoticeView>();
            int row = -1;
            float appearedAfter = float.NaN;
            float limit = spawnedAt + BossHintRule.Delay + BossHintWait;
            while (Time.realtimeSinceStartup < limit)
            {
                row = BossHintRow(notice, wantTitle, wantHint, out _);
                if (row >= 0)
                {
                    appearedAfter = Time.realtimeSinceStartup - spawnedAt;
                    break;
                }
                yield return null;
            }
            yield return Shot("boss_hint", () =>
            {
                var p = new Probe();
                int now = BossHintRow(notice, wantTitle, wantHint, out string shownRows);
                p.Expect(notice != null, "BattleNoticeView 있음");
                p.Expect(row >= 0 && now >= 0, $"단서 줄 '{wantTitle}' · '{wantHint}' (α ≥ {BossHintAlpha:0.0}) — 보이는 안내 {shownRows}");
                if (row >= 0) p.Expect(appearedAfter >= BossHintRule.Delay - 0.15f, $"보스가 나오고 {appearedAfter:0.00}초에 보임 (기대 ≥ {BossHintRule.Delay:0.0}초 — 등장 배너 뒤)");
                var tick = FindNamePart(Find(CoachTargets.StagePanel), "Enrage");
                p.Expect(tick != null && IsVisible(tick), $"StagePanel 화남 눈금 {(tick != null ? tick.name : "없음")}");
                CheckEnrageTick(p);
                Report(id, "boss_hint", p);
            });
        }

        /// <summary>
        /// 07b 화남 눈금 자리 (PRG-02 §13.1, P4-3b 이름 BattleHud._enrageTick — StagePanel 진행 막대 ProgressBar 안): 눈금 가운데가 막대 폭의
        /// BattleConfig.enrageThreshold(0.5) 자리 ±0.03, 눈금이 켜져 있다 (보스 단계)
        /// </summary>
        void CheckEnrageTick(Probe p)
        {
            var hud = FindAnyObjectByType<BattleHud>();
            var tick = hud != null ? Field<Image>(hud, "_enrageTick") : null;
            var bar = tick != null ? tick.rectTransform.parent as RectTransform : null;
            if (tick == null || bar == null)
            {
                p.Expect(false, $"BattleHud._enrageTick {(tick == null ? "없음" : "부모 막대 없음")} (이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            float threshold = Game.Battle != null && Game.Battle.Config != null ? Mathf.Clamp01(Game.Battle.Config.enrageThreshold) : 0.5f;
            var tickRect = ScreenRect(tick.rectTransform);
            var barRect = ScreenRect(bar);
            float at = barRect.width > 0f ? (tickRect.center.x - barRect.xMin) / barRect.width : float.NaN;
            p.Expect(tick.enabled, $"눈금 {(tick.enabled ? "켜짐" : "꺼짐")} (보스 단계에 켠다)");
            p.Expect(!float.IsNaN(at) && Mathf.Abs(at - threshold) <= 0.03f,
                $"눈금 {R(tickRect)}이 막대 {bar.name} {R(barRect)}의 {at:0.000} 자리 (기대 enrageThreshold {threshold:0.00} ±0.03)");
        }

        /// <summary>BattleNoticeView에서 제목 = title · 설명 = hint · 띠 알파 ≥ BossHintAlpha인 줄 번호 (없으면 −1). shown = 보이는 줄 글 (기록용)</summary>
        static int BossHintRow(BattleNoticeView notice, string title, string hint, out string shown)
        {
            shown = "없음";
            if (notice == null) return -1;
            var rows = Field<RectTransform[]>(notice, "_rows");
            var groups = Field<CanvasGroup[]>(notice, "_groups");
            var titles = Field<TMP_Text[]>(notice, "_titles");
            var subtitles = Field<TMP_Text[]>(notice, "_subtitles");
            if (rows == null || titles == null || subtitles == null) return -1;
            var text = new StringBuilder();
            int found = -1;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || !rows[i].gameObject.activeInHierarchy) continue;
                float alpha = groups != null && i < groups.Length && groups[i] != null ? groups[i].alpha : EffectiveAlpha(rows[i]);
                string t = i < titles.Length && titles[i] != null ? ShownText(titles[i]) : string.Empty;
                string s = i < subtitles.Length && subtitles[i] != null && subtitles[i].isActiveAndEnabled ? ShownText(subtitles[i]) : string.Empty;
                text.Append(text.Length > 0 ? ", " : string.Empty).Append($"{i} '{t}' · '{s}' α {alpha:0.00}");
                if (found < 0 && alpha >= BossHintAlpha && t == title && s == hint) found = i;
            }
            if (text.Length > 0) shown = text.ToString();
            return found;
        }

        /// <summary>
        /// 첫 1-10 실제 클리어 (BattleSimulator.Clear) → 36 chapter_chest가 34 story_page보다 먼저 뜬다 (P4-6 · P4-1). 열린 팝업 순서는 12초 동안 프레임마다 보고,
        /// 다른 창(이벤트 시작 카드 등)이 줄 앞을 잡으면 닫는다. 창 형식이 없으면 SKIP, 1장 상자가 이미 연 것으로 적혔으면 SKIP, 그 밖에 안 뜨면 FAIL (까닭 · 줄 상태를 적는다).
        /// 두 창 모두 여는 연출이 끝난 뒤(알파 ≥ 0.99) 찍는다. S4-36-dim: 상자 창의 어둡게 덮기 누름(RequestDismiss)이 상자를 받는다 (1장 상자가 연 것으로 적힘, P4-6 C1)
        /// </summary>
        IEnumerator ChapterClear()
        {
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            int stage = Game.Battle.Stage;
            yield return ForceOutcome(true);
            if (!_forced)
            {
                Check("S4-36", false, $"chapter_chest: {BattleSystem.FormatStage(stage)} 클리어를 만들지 못했다");
                yield break;
            }
            var order = new List<string>();
            var others = new List<string>();
            bool queuedSeen = false;
            float limit = Time.realtimeSinceStartup + ChapterPopupWait;
            UIPopup chest = null, story = null;
            while (Time.realtimeSinceStartup < limit)
            {
                chest ??= FindPopup("ChapterChestPopup");
                story ??= FindPopup("StoryPagePopup");
                if (chest != null && chest.IsOpen && !order.Contains("ChapterChestPopup")) order.Add("ChapterChestPopup");
                if (story != null && story.IsOpen && !order.Contains("StoryPagePopup")) order.Add("StoryPagePopup");
                if (order.Count > 0) break;
                if (Game.Popups != null && Game.Popups.Contains(MetaPopupKind.ChapterChest)) queuedSeen = true;
                // 다른 창(이벤트 시작 카드 · 기능 카드 …)이 줄 앞을 잡고 있으면 닫는다 — 줄이 넘어가야 상자가 뜬다 (V2 r4: 예전에는 8초 그대로 기다렸다)
                var top = UIManager.Instance != null ? UIManager.Instance.TopPopup : null;
                if (top != null && top != chest && top != story)
                {
                    if (!others.Contains(top.GetType().Name)) others.Add(top.GetType().Name);
                    CloseAll();
                }
                yield return null;
            }
            bool chestOpened = Game.ChapterChests != null && Game.ChapterChests.IsChapterChestOpened(1);
            bool queued = Game.Popups != null && Game.Popups.Contains(MetaPopupKind.ChapterChest);
            string diag = $"1장 상자 열림 표시 {chestOpened} · 줄에 ChapterChest {(queued ? "남음" : queuedSeen ? "있었다가 빠짐" : "없음")} · 닫은 다른 창 {(others.Count > 0 ? string.Join(", ", others) : "없음")}";
            Record("INFO", "S4-36", $"chapter_chest: {BattleSystem.FormatStage(stage)} 클리어 → 최고 {Game.Battle.HighestStage} · 처음 뜬 창 {(order.Count > 0 ? string.Join(" → ", order) : "없음")} · {diag}");
            if (order.Count == 0)
            {
                // 창 형식 · ChapterChestSystem은 들어왔다 (P4-1 · P4-6) — 안 뜬 까닭을 적는다: 이미 연 것으로 적힘(시드 · S-3 옛 세이브 표시)은 SKIP, 그 밖은 FAIL (V2 r4: 예전 '아직 없음' SKIP은 낡은 이유)
                if (GameType("ChapterChestPopup") == null) Landed("S4-36", "chapter_chest", "P4-6 · P4-1 C-04", "ChapterChestPopup");
                else if (chestOpened) SkipCheck("S4-36", $"chapter_chest: 1장 상자가 이미 연 것으로 적혀 있어 뜨지 않는다 (S-3 MarkLegacyChapterOne · 가이드 22 — 새 세이브 시드 탓인지 확인) · {diag}");
                else Check("S4-36", false, $"chapter_chest: 첫 1-10 클리어 뒤 {ChapterPopupWait:0}초 안에 챕터 상자 창이 뜨지 않았다 · {diag}");
                if (GameType("StoryPagePopup") == null) Landed("S4-34", "story_page", "P4-6", "StoryPagePopup");
                else SkipCheck("S4-34", "story_page: 챕터 상자가 먼저 떠야 이야기 쪽 순서를 본다 (S4-36 참고)");
                CloseAll();
                yield break;
            }
            string storyPath = null;
            if (Landed("S4-36", "chapter_chest", "P4-6 · P4-1 C-04", "ChapterChestPopup"))
            {
                // 여는 연출(루트 알파 0 → 1)이 끝난 뒤 찍는다 (P4-6 R-7c — 예전에는 연 첫 프레임이라 그림에 창이 없었다)
                yield return WaitPopupShown(chest);
                yield return Shot("chapter_chest", () =>
                {
                    var p = new Probe();
                    p.Expect(chest != null && chest.IsOpen, "챕터 상자 창 열림");
                    p.Expect(order.Count > 0 && order[0] == "ChapterChestPopup", $"처음 뜬 창 {(order.Count > 0 ? order[0] : "없음")} (기대 챕터 상자가 이야기 쪽보다 먼저)");
                    float alpha = PopupAlpha(chest);
                    p.Expect(alpha >= PopupShownAlpha, $"찍을 때 창 알파 {alpha:0.00} (기대 ≥ {PopupShownAlpha:0.00} — 여는 연출 끝)");
                    Report("S4-36", "chapter_chest", p);
                });
                // 어둡게 덮기 누름 = 받기 (P4-6 C1: OnDimOrBack → Claim, 받기 연출 ≈ 1.1초 뒤 창이 닫힌다) → 1장 상자가 연 것으로 적힌다
                var chests = Game.ChapterChests;
                bool openedBefore = chests != null && chests.IsChapterChestOpened(1);
                float dimAt = Time.realtimeSinceStartup;
                if (chest != null && chest.IsOpen) chest.RequestDismiss();
                float claimLimit = dimAt + ChestClaimWait;
                while (Time.realtimeSinceStartup < claimLimit && chests != null && !chests.IsChapterChestOpened(1)) yield return null;
                bool claimed = chests != null && chests.IsChapterChestOpened(1);
                float claimedAfter = Time.realtimeSinceStartup - dimAt;
                while (Time.realtimeSinceStartup < claimLimit && chest != null && chest.IsOpen) yield return null;
                bool stillOpen = chest != null && chest.IsOpen;
                Check("S4-36-dim", !openedBefore && claimed,
                    $"chapter_chest: 어둡게 덮기 누름(RequestDismiss) → 1장 상자 {(claimed ? $"연 것으로 적힘 ({claimedAfter:0.00}초)" : $"{ChestClaimWait:0}초 안에 안 적힘")} · " +
                    $"누르기 전 {openedBefore} · 창 {OpenText(stillOpen)} (기대 누르기 전 false → 받음 — 어느 길로 닫아도 받는다, P4-6 C1)");
                if (chest != null && chest.IsOpen) chest.Close();
                float wait = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < wait && (story == null || !story.IsOpen))
                {
                    story ??= FindPopup("StoryPagePopup");
                    yield return null;
                }
                // 이야기 쪽은 MetaPopupPresenter가 '스테이지 사이 · 열린 창 없음'일 때만 꺼낸다 (PopupQueue.TryDequeue) — 받기 연출(≈ 1.1초) 동안 다음 판이 시작됐으면
                // 그 판이 끝날 때 뜬다 (V2 r7: 2-1은 배속 2로 50초에 14/21 — 한 판 늦게). 3초 안에 안 뜨고 줄에 StoryPage가 있으면 지금 판을 실제로 끝내(ForceOutcome 클리어)
                // 스테이지 사이를 만들고 그 뒤 StoryNextStageWait초 기다린다 (다른 창은 닫는다). 예전 즉시 Close는 아직 판 사이라 바로 떴다
                if ((story == null || !story.IsOpen) && Game.Popups != null && Game.Popups.Contains(MetaPopupKind.StoryPage) && Game.Battle != null)
                {
                    string stageWas = Game.Battle.StageLabel;
                    bool betweenWas = Game.Battle.IsBetweenStages;
                    yield return ForceOutcome(true);
                    float endedAt = Time.realtimeSinceStartup;
                    var closed = new List<string>();
                    while (_forced && Time.realtimeSinceStartup < endedAt + StoryNextStageWait && (story == null || !story.IsOpen))
                    {
                        story ??= FindPopup("StoryPagePopup");
                        var top = UIManager.Instance != null ? UIManager.Instance.TopPopup : null;
                        if (top != null && top != story)
                        {
                            if (!closed.Contains(top.GetType().Name)) closed.Add(top.GetType().Name);
                            CloseAll();
                        }
                        yield return null;
                    }
                    storyPath = $"상자 뒤 3초 안에 안 뜸 ({stageWas} 판 사이 {betweenWas} — 다음 판이 이미 시작) · 줄에 StoryPage → {stageWas} 클리어 {(_forced ? "만듦" : "못 만듦")}, " +
                                $"{Time.realtimeSinceStartup - endedAt:0.0}초 뒤 {(story != null && story.IsOpen ? "이야기 쪽 열림" : "안 열림")}" +
                                $"{(closed.Count > 0 ? $" · 닫은 창 {string.Join(", ", closed)}" : string.Empty)}";
                }
            }
            if (Landed("S4-34", "story_page", "P4-6", "StoryPagePopup"))
            {
                if (story == null || !story.IsOpen)
                {
                    bool queuedStory = Game.Popups != null && Game.Popups.Contains(MetaPopupKind.StoryPage);
                    SkipCheck("S4-34", $"story_page: 챕터 상자 뒤 이야기 쪽이 뜨지 않았다 (줄에 StoryPage {(queuedStory ? "남음" : "없음")}{(storyPath != null ? $" · {storyPath}" : string.Empty)}, P4-6 · P4-1 Story)");
                    CloseAll();
                    yield break;
                }
                yield return WaitPopupShown(story);
                // 세 칸이 다 보일 때까지 (StoryPagePopup.FullyRevealed — 칸마다 자동 1.2초, 늦어도 StoryRevealWait초) 기다린 뒤 찍는다
                var page = story as StoryPagePopup;
                float revealStart = Time.realtimeSinceStartup;
                while (page != null && page.IsOpen && !page.FullyRevealed && Time.realtimeSinceStartup - revealStart < StoryRevealWait) yield return null;
                float revealAfter = Time.realtimeSinceStartup - revealStart;
                yield return Shot("story_page", () =>
                {
                    var p = new Probe();
                    p.Expect(story != null && story.IsOpen, "이야기 쪽 열림");
                    float alpha = PopupAlpha(story);
                    p.Expect(alpha >= PopupShownAlpha, $"찍을 때 창 알파 {alpha:0.00} (기대 ≥ {PopupShownAlpha:0.00} — 여는 연출 끝)");
                    if (storyPath != null) p.Info(storyPath);
                    if (page != null) CheckStoryPanels(p, page, revealAfter);
                    else p.Expect(false, $"이야기 쪽 형식 {story?.GetType().Name ?? "없음"} (기대 StoryPagePopup)");
                    Report("S4-34", "story_page", p);
                });
            }
            CloseAll();
        }

        /// <summary>
        /// 34 '그림 셋 + 글' (P4-6 이름 StoryPagePopup._panels · _panelText · _titleText): 다 보임(FullyRevealed), 세 칸 모두 켜지고 알파 ≥ 0.99, 칸마다 글이 있고 제목이 있다.
        /// 그림이 없는 칸은 대신 판 색(_panelFallback)이라 그림은 기록만 (아트 대기 — P4-10)
        /// </summary>
        static void CheckStoryPanels(Probe p, StoryPagePopup page, float revealAfter)
        {
            p.Expect(page.FullyRevealed, $"세 칸 다 보임 (FullyRevealed {page.FullyRevealed} · {revealAfter:0.0}초 기다림, 상한 {StoryRevealWait:0}초)");
            var panels = Field<CanvasGroup[]>(page, "_panels");
            var texts = Field<TMP_Text[]>(page, "_panelText");
            var art = Field<Image[]>(page, "_panelArt");
            var title = Field<TMP_Text>(page, "_titleText");
            if (panels == null || texts == null)
            {
                p.Expect(false, $"StoryPagePopup {(panels == null ? "_panels " : string.Empty)}{(texts == null ? "_panelText " : string.Empty)}없음 (이름이 바뀌었나 — V2에 알린다)");
                return;
            }
            int shown = 0, withText = 0, withArt = 0;
            var words = new StringBuilder();
            for (int i = 0; i < StoryPagePopup.PanelCount && i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].gameObject.activeInHierarchy && panels[i].alpha >= PopupShownAlpha) shown++;
                string text = i < texts.Length && texts[i] != null ? ShownText(texts[i]) : string.Empty;
                if (!string.IsNullOrWhiteSpace(text)) withText++;
                if (art != null && i < art.Length && art[i] != null && art[i].enabled && art[i].sprite != null) withArt++;
                words.Append(i > 0 ? " / " : string.Empty).Append(text.Length > 14 ? text.Substring(0, 14) + "…" : text);
            }
            p.Expect(shown == StoryPagePopup.PanelCount, $"보이는 칸 {shown}/{StoryPagePopup.PanelCount} (알파 ≥ {PopupShownAlpha:0.00})");
            p.Expect(withText == StoryPagePopup.PanelCount, $"글 있는 칸 {withText}/{StoryPagePopup.PanelCount} — '{words}'");
            string titleText = title != null ? ShownText(title) : null;
            p.Expect(!string.IsNullOrWhiteSpace(titleText), $"제목 '{titleText ?? "없음"}'");
            p.Info($"그림 있는 칸 {withArt}/{StoryPagePopup.PanelCount} (없으면 판 색 — 아트 대기)");
        }

        /// <summary>
        /// S-7 (plan §4.2): StagePanel을 누르면 — 챕터 지도가 구독돼 있으면(UINav.HasChapterMap) 35 chapter_map (마디 10 · 보스 마디 · 상자 표시),
        /// 아니면 예전 스테이지 목록(BattleHud._stageList)이 대신 열린다 (S4-35는 SKIP, 목록은 S4-stagelist로 본다). MAIN과 SHEET(시트가 먼저 내려간다) 둘 다
        /// </summary>
        IEnumerator StagePanelRoute()
        {
            var button = Comp<Button>(CoachTargets.StagePanel);
            var hud = FindAnyObjectByType<BattleHud>();
            var list = hud != null ? Field<GameObject>(hud, "_stageList") : null;
            if (button == null)
            {
                Check("S4-stagelist", false, $"stage_panel: {CoachTargets.StagePanel} 단추 없음");
                yield break;
            }
            bool hasMap = UINav.HasChapterMap;
            for (int pass = 0; pass < 2; pass++)
            {
                bool fromSheet = pass == 1;
                string shot = fromSheet ? "stage_panel_sheet" : "stage_panel_main";
                CloseAll();
                UINav.GoToMain(true);
                yield return WaitSettled();
                if (list != null && list.activeSelf) button.onClick.Invoke();
                yield return null;
                if (fromSheet)
                {
                    _tabs.Select(MainTab.Growth);
                    yield return WaitSettled();
                }
                button.onClick.Invoke();
                yield return Wait(fromSheet ? 1.4f : 0.6f);
                var map = FindPopup("ChapterMapPopup");
                yield return Shot(shot, () =>
                {
                    bool mapOpen = map != null && map.IsOpen;
                    bool listOpen = list != null && list.activeInHierarchy;
                    string mapId = fromSheet ? "S4-35-sheet" : "S4-35";
                    if (mapOpen)
                    {
                        var p = new Probe();
                        p.Expect(true, $"챕터 지도 열림 ({(fromSheet ? "SHEET → 시트가 내려간 뒤" : "MAIN")})");
                        p.Expect(!UIFrame.SheetOpen, "시트 닫힘");
                        p.Expect(!listOpen, "예전 스테이지 목록은 닫힘");
                        CheckChapterMapNodes(p, map);
                        Report(mapId, shot, p);
                        return;
                    }
                    // 지도가 안 열렸으면: 받는 쪽이 없거나(HasChapterMap false) 스테이지 판 길(P4-3b, S-7)이 아직 — 예전 목록이 대신 열려야 한다
                    SkipCheck(mapId, hasMap
                        ? $"chapter_map: 아직 없음 — HasChapterMap true인데 StagePanel이 지도를 열지 않는다 (P4-3b StagePanel 길, S-7) · 목록 {OpenText(listOpen)}"
                        : "chapter_map: 아직 없음 — UINav.HasChapterMap false (P4-6 ChapterMapPopup 구독 전)");
                    Check(fromSheet ? "S4-stagelist-sheet" : "S4-stagelist", listOpen && !UIFrame.SheetOpen,
                        $"{shot}: StagePanel → 스테이지 목록 {OpenText(listOpen)} · 시트 {OpenText(UIFrame.SheetOpen)} (기대 목록 열림 · 시트 닫힘 — 챕터 지도 길 전 대신 길)");
                });
                if (list != null && list.activeSelf) button.onClick.Invoke();
                CloseAll();
            }
            UINav.GoToMain(true);
        }

        /// <summary>
        /// 35 지도 마디 (P4-6 이름): …/MapFrame/Nodes/Node1…Node10 켜짐 · 보스 마디 Node10/Boss · 상자 표시 Nodes/ChestMarker는 그 장의 상자 보상이 있을 때만 켜짐
        /// (ChapterMapPopup.RefreshBoss — PeekChapterChest(장)). 지도가 보여 주는 장은 _chapter (첫 1-10 클리어 뒤에는 지금 스테이지의 장)
        /// </summary>
        static void CheckChapterMapNodes(Probe p, UIPopup map)
        {
            var nodes = FindDeep(map.transform, "Nodes");
            if (nodes == null)
            {
                p.Expect(false, "지도 마디 묶음 Nodes 없음 (P4-6 MapFrame/Nodes)");
                return;
            }
            int shown = 0;
            string missing = null;
            for (int i = 1; i <= ChapterMapNodes; i++)
            {
                var node = nodes.Find("Node" + i);
                if (node != null && node.gameObject.activeInHierarchy) shown++;
                else missing ??= "Node" + i;
            }
            p.Expect(shown == ChapterMapNodes, $"마디 Node1~Node{ChapterMapNodes} 중 켜진 것 {shown}개{(missing != null ? $" (첫 빠짐 {missing})" : string.Empty)}");
            var boss = nodes.Find($"Node{ChapterMapNodes}/Boss");
            var bossImage = boss != null ? boss.GetComponent<Image>() : null;
            p.Expect(boss != null, $"보스 마디 Node{ChapterMapNodes}/Boss {(boss != null ? "있음" : "없음")}");
            if (boss != null) p.Info($"보스 초상 {(bossImage != null && bossImage.enabled && bossImage.sprite != null ? bossImage.sprite.name : "그림 없음 (보스 그림이 없으면 꺼 둔다)")}");
            int chapter = map.GetType().GetField("_chapter", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(map) is int c ? c : -1;
            var chests = Game.IsMetaActive ? Game.ChapterChests : null;
            var contents = chests != null && chapter > 0 ? chests.PeekChapterChest(chapter) : null;
            bool wantMarker = contents != null && contents.Count > 0;
            var marker = nodes.Find("ChestMarker");
            bool markerOn = marker != null && marker.gameObject.activeInHierarchy;
            p.Expect(marker != null && markerOn == wantMarker,
                $"상자 표시 Nodes/ChestMarker {(marker != null ? OpenText(markerOn) : "없음")} · {chapter}장 상자 보상 {(contents != null ? contents.Count : 0)}개 (기대 보상이 있을 때만 켜짐)" +
                $"{(wantMarker && chests != null ? $" · 연 것으로 적힘 {chests.IsChapterChestOpened(chapter)}" : string.Empty)}");
        }

        /// <summary>16c dungeon_rec (P4-4 · P4-1): 1-10 — 던전 카드 '권장 … · 잔향이 …' 줄 + 평가 말. 권장 값이 0(A-1 스텁)이면 SKIP</summary>
        IEnumerator DungeonRec()
        {
            const string id = "S4-16c";
            if (Game.Power == null || Game.Power.GetDungeonRecommended(1) <= 0)
            {
                SkipCheck(id, "dungeon_rec: 아직 없음 — Power.GetDungeonRecommended = 0 (P4-1 A-1 스텁)");
                yield break;
            }
            if (Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Dungeon)) Game.Features.ForceUnlock(FeatureId.Dungeon, false);
            CloseAll();
            _tabs.Select(MainTab.Expedition);
            yield return WaitSettled();
            var panel = FindAnyObjectByType<ExpeditionPanel>();
            var seg = panel != null ? Field<Button>(panel, "_dungeonSeg") : null;
            if (seg != null) seg.onClick.Invoke();
            yield return Wait(0.6f);
            var card = panel != null ? Field<DungeonCardView>(panel, "_dungeonCard") : null;
            yield return Shot("dungeon_rec", () =>
            {
                var p = new Probe();
                string line = card != null ? FindText(card.transform, "권장") : null;
                p.Expect(line != null && line.Contains("잔향"), $"권장 줄 '{line}' (기대 '권장 … · 잔향이 …')");
                // 평가 말 (DungeonCardView._ratingText — 칩 '여유' · '도전' · '어려움'): 잔향이 전투력 대 다음 층 권장으로 PowerSystem이 정한 평가와 같다
                var rating = card != null ? Field<TMP_Text>(card, "_ratingText") : null;
                string word = rating != null && rating.gameObject.activeInHierarchy ? ShownText(rating) : null;
                int floor = Game.Dungeon != null ? Game.Dungeon.NextFloor : 1;
                long rec = Game.Power.GetDungeonRecommended(floor);
                string want = PowerSystem.RatingName(Game.Power.GetRating(Game.Power.EchoPower, rec));
                p.Expect(word == want, $"평가 말 '{word ?? "없음"}' (기대 '{want}' — 잔향이 {Game.Power.EchoPower} / {floor}층 권장 {rec})");
                Report(id, "dungeon_rec", p);
            });
            UINav.GoToMain(true);
        }

        // ═════════════════════════ calendar ═════════════════════════

        /// <summary>23c journal_first_week (NEW-01, P4-6 · P4-2): 둘째 날 — 수선 일지 '첫 주' 탭 · 날 칩 7 (3~7일 잠김) · 상자 5 · 수선 일지 단추 점. 쪽이 아직 없으면 SKIP</summary>
        IEnumerator JournalFirstWeek()
        {
            const string id = "S4-23c";
            if (!Landed(id, "journal_first_week", "P4-6 NEW-01", "StarterWeekPanelView")) yield break;
            // 첫 주 시스템 · 콘텐츠가 없을 때만 SKIP. 콘텐츠가 있는데 탭이 안 보이면(IsAvailable false — 첫 주가 시작되지 않음) FAIL (V2 r4: 예전 SKIP은 이 도구의 시드 탓이었다)
            var week = Game.StarterWeek;
            if (week == null || !StarterWeekHasTasks())
            {
                SkipCheck(id, $"journal_first_week: 첫 주 {(week == null ? "시스템(Game.StarterWeek) 없음" : "콘텐츠(starterWeek.tasks) 0")} — 볼 쪽이 없다");
                yield break;
            }
            var available = GameType("StarterWeekPanelView")?.GetProperty("IsAvailable", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            if (available is bool on && !on)
            {
                Check(id, false, $"journal_first_week: 첫 주 콘텐츠 {Game.MetaDb.starterWeek.tasks.Count}과제가 있는데 StarterWeekPanelView.IsAvailable false — " +
                                 $"시작 {week.IsStarted} · 진행 {week.IsActive} · 시작 날 {Game.Data.meta?.starterWeek?.startDayKey ?? "없음"} · 설치 {Game.DailyClock.InstallDay}일째");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            var journalButton = Find(CoachTargets.JournalButton);
            bool dot = false;
            if (journalButton != null)
            {
                foreach (var badge in journalButton.GetComponentsInChildren<Badge>(false))
                {
                    if (IsVisible(badge.transform)) dot = true;
                }
            }
            UINav.OpenJournal();
            yield return Wait(1f);
            var journal = UIManager.Instance.Get<JournalPopup>();
            var tab = journal != null ? FindButtonWithText(journal.transform, "첫 주") : null;
            if (tab != null) tab.onClick.Invoke();
            yield return Wait(0.6f);
            yield return Shot("journal_first_week", () =>
            {
                var p = new Probe();
                p.Expect(dot, "수선 일지 빠른 아이콘 점");
                p.Expect(tab != null, "'첫 주' 탭");
                var panelView = FindView("StarterWeekPanelView");
                p.Expect(panelView != null && IsVisible(panelView.transform), "첫 주 쪽이 보인다");
                CheckStarterWeekChips(p, panelView != null ? panelView.transform : null, journal != null ? journal.transform : null, week.DayIndex);
                Report(id, "journal_first_week", p);
            });
            CloseAll();
        }

        /// <summary>
        /// 23c 날 칩 · 상자 (P4-6 이름): Day1…Day7의 자식 Lock은 열린 날(1 ~ DayIndex, 1부터 세는 날 — StarterWeekPanelView.OpenDays = Clamp(DayIndex, 1, 7))에는 꺼지고
        /// 그 뒤 날에는 켜진다 (둘째 날: Day1 · Day2 열림, Day3~Day7 잠김). 상자 = 켜진 MissionChestView 5개. 칩은 첫 주 쪽 아래에서 찾고, 없으면 수선 일지 창 아래에서
        /// </summary>
        static void CheckStarterWeekChips(Probe p, Transform panel, Transform journal, int dayIndex)
        {
            int open = Mathf.Clamp(dayIndex, 1, StarterWeekDayChips);
            int found = 0, wrong = 0;
            string missing = null, firstWrong = null;
            var locked = new StringBuilder();
            for (int d = 1; d <= StarterWeekDayChips; d++)
            {
                var chip = (panel != null ? FindDeep(panel, "Day" + d) : null) ?? (journal != null ? FindDeep(journal, "Day" + d) : null);
                var lockMark = chip != null ? chip.Find("Lock") : null;
                if (chip == null || lockMark == null)
                {
                    missing ??= chip == null ? "Day" + d : $"Day{d}/Lock";
                    continue;
                }
                found++;
                bool isLocked = lockMark.gameObject.activeSelf;
                if (isLocked) locked.Append(locked.Length > 0 ? "," : string.Empty).Append(d);
                if (isLocked != d > open)
                {
                    wrong++;
                    firstWrong ??= $"Day{d} 자물쇠 {(isLocked ? "켜짐" : "꺼짐")}";
                }
            }
            p.Expect(found == StarterWeekDayChips, $"날 칩 Day1~Day{StarterWeekDayChips} + Lock {found}개{(missing != null ? $" (첫 빠짐 {missing})" : string.Empty)}");
            p.Expect(wrong == 0, $"DayIndex {dayIndex} → 열린 날 1~{open} · 잠긴 날 [{locked}] (기대 {(open < StarterWeekDayChips ? $"{open + 1}~{StarterWeekDayChips}" : "없음")}){(firstWrong != null ? $" · 첫 어긋남 {firstWrong}" : string.Empty)}");
            int chests = 0;
            if (panel != null)
            {
                foreach (var chest in panel.GetComponentsInChildren<MissionChestView>(false)) chests++;
            }
            p.Expect(chests == StarterWeekChests, $"점수 상자 {chests}개 (기대 {StarterWeekChests} — 30/60/90/120/150)");
        }

        /// <summary>31 event_hub (EVT-01, P4-6 · P4-2): 이번 달 3일 — 이벤트 모아 보기 (띠 그림 · 효과 줄 '별빛 사탕 ×2' · 도장 · 미션). 창이 아직 없으면 SKIP</summary>
        IEnumerator EventHub()
        {
            const string id = "S4-31";
            if (!Landed(id, "event_hub", "P4-6 EVT-01", "EventHubPopup")) yield break;
            CloseAll();
            UINav.OpenEvents();
            yield return Wait(1.2f);
            var popup = FindPopup("EventHubPopup");
            yield return Shot("event_hub", () =>
            {
                var p = new Probe();
                p.Expect(popup != null && popup.IsOpen, "이벤트 모아 보기 열림 (UINav.OpenEvents)");
                if (popup != null) p.Expect(FindText(popup.transform, "별빛 사탕 ×2") != null, $"효과 줄 '별빛 사탕 ×2' ('{FindText(popup.transform, "×2")}')");
                if (popup != null) CheckEventHub(p, popup);
                Report(id, "event_hub", p);
            });
            CloseAll();
        }

        /// <summary>
        /// 31 띠 · 도장 · 미션 (P4-6 이름 EventHubPopup._banner · _nameText · _timeText · _chips · _stampRow · _stampCells · _listContent): 띠 그림(대신 그림 포함)과 이름 · 남은 시간,
        /// 효과 칩 ≥ 1, 도장 줄 7칸 (이달 3일의 회차는 도장이 있다), 미션 줄(MissionRowView) ≥ 1
        /// </summary>
        static void CheckEventHub(Probe p, UIPopup popup)
        {
            var banner = Field<Image>(popup, "_banner");
            var name = Field<TMP_Text>(popup, "_nameText");
            var time = Field<TMP_Text>(popup, "_timeText");
            var chips = Field<GameObject[]>(popup, "_chips");
            var stampRow = Field<GameObject>(popup, "_stampRow");
            var cells = Field<Button[]>(popup, "_stampCells");
            var list = Field<RectTransform>(popup, "_listContent");
            p.Expect(banner != null && banner.enabled && banner.sprite != null && banner.gameObject.activeInHierarchy,
                $"띠 그림 {(banner != null && banner.sprite != null ? banner.sprite.name : "없음")}");
            string nameText = name != null ? ShownText(name) : null;
            string timeText = time != null ? ShownText(time) : null;
            p.Expect(!string.IsNullOrWhiteSpace(nameText) && !string.IsNullOrWhiteSpace(timeText), $"이벤트 이름 '{nameText}' · 남은 시간 '{timeText}'");
            int chipsOn = 0;
            if (chips != null)
            {
                foreach (var chip in chips)
                {
                    if (chip != null && chip.activeInHierarchy) chipsOn++;
                }
            }
            p.Expect(chipsOn >= 1, $"효과 칩 {chipsOn}개");
            int cellsOn = 0;
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    if (cell != null && cell.gameObject.activeInHierarchy) cellsOn++;
                }
            }
            p.Expect(stampRow != null && stampRow.activeInHierarchy && cellsOn == 7, $"도장 줄 {(stampRow != null ? OpenText(stampRow.activeInHierarchy) : "없음")} · 칸 {cellsOn}개 (기대 7)");
            int rows = 0;
            if (list != null)
            {
                foreach (var row in list.GetComponentsInChildren<MissionRowView>(false))
                {
                    if (row.HasModel) rows++;
                }
            }
            p.Expect(rows >= 1, $"미션 줄 {rows}개 (MissionRowView)");
        }

        /// <summary>32 season (Q-06 스트레치, P4-6 · P4-2): 수첩 (Lv 줄 · 30칸 · 바닥 줄). 콘텐츠 칸이 0이면 SKIP (stretch off), 창이 없으면 SKIP</summary>
        IEnumerator Season()
        {
            const string id = "S4-32";
            int tiers = Game.MetaDb != null && Game.MetaDb.season != null && Game.MetaDb.season.tiers != null ? Game.MetaDb.season.tiers.Count : 0;
            if (tiers == 0)
            {
                SkipCheck(id, "season: SKIP (stretch off) — MetaDatabase.season.tiers 0칸 (plan §3.2 #17, G3에서 켠다)");
                yield break;
            }
            if (!Landed(id, "season", "P4-6 Q-06", "SeasonPassPopup")) yield break;
            CloseAll();
            UINav.OpenSeason();
            yield return Wait(1.2f);
            var popup = FindPopup("SeasonPassPopup");
            yield return Shot("season", () =>
            {
                var p = new Probe();
                p.Expect(popup != null && popup.IsOpen, "수첩 열림 (UINav.OpenSeason)");
                if (popup != null)
                {
                    // Lv 줄 · 칸(MissionRowView — 콘텐츠 칸 수, SPEC4 30) · 바닥 줄(남은 날 · 얻는 곳) — P4-6 이름 SeasonPassPopup._levelText · _listContent · _daysText · _sourceText
                    var level = Field<TMP_Text>(popup, "_levelText");
                    var list = Field<RectTransform>(popup, "_listContent");
                    var days = Field<TMP_Text>(popup, "_daysText");
                    var source = Field<TMP_Text>(popup, "_sourceText");
                    string levelText = level != null ? ShownText(level) : null;
                    p.Expect(levelText != null && levelText.Contains("Lv"), $"Lv 줄 '{levelText ?? "없음"}'");
                    int rows = list != null ? list.GetComponentsInChildren<MissionRowView>(false).Length : 0;
                    p.Expect(rows == tiers, $"칸 {rows}개 (기대 콘텐츠 {tiers}칸 — SPEC4 30)");
                    string footer = $"{(days != null ? ShownText(days) : string.Empty)} {(source != null ? ShownText(source) : string.Empty)}".Trim();
                    p.Expect(footer.Length > 0, $"바닥 줄 '{footer}'");
                }
                Report(id, "season", p);
            });
            CloseAll();
        }

        // ═════════════════════════ comeback ═════════════════════════

        /// <summary>
        /// 복귀 (33): 일시정지 → 시계 +80시간 → 복귀. 복귀 화면(ReturnPopup)이 뜨고 비운 시간 ≈ 80시간 (S4-33-return — 지금 있는 것, 시계 시드 확인).
        /// 그 뒤 다시 만난 날 카드 (도장 칸 Cells/Cell1~Cell3 · 복귀 화면 CapText의 바구니 상한 '/ 24시간', P4-6 이름) — 카드가 아직 없으면 SKIP
        /// </summary>
        IEnumerator Comeback()
        {
            if (s_clock == null || GameBootstrap.Instance == null)
            {
                Check("S4-33-return", false, "comeback: 옮길 수 있는 시계가 없다 (-autoshot-spec4가 BeforeSceneLoad에 넣는다)");
                yield break;
            }
            yield return AwayFor(TimeSpan.FromHours(ComebackHours));
            var ret = UIManager.Instance.Get<ReturnPopup>();
            bool returned = false;
            float limit = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < limit && !returned)
            {
                if (ret != null && ret.IsOpen) returned = true;
                else
                {
                    // 출석부가 먼저 뜨면 닫고 복귀 화면을 기다린다
                    var attendance = FindPopup("AttendancePopup");
                    if (attendance != null && attendance.IsOpen) attendance.Close();
                }
                yield return null;
            }
            long away = Game.Data.meta?.returnScreen != null ? Game.Data.meta.returnScreen.lastAwaySeconds : -1;
            yield return Wait(0.6f);
            // 바구니 상한 줄은 카드가 아니라 복귀 화면의 CapText ('바구니 24시간 / 24시간 · 가득' — P4-6) — 닫기 전에 읽어 둔다 (S4-33에서 본다)
            string capLine = null;
            yield return Shot("return_after_80h", () =>
            {
                var p = new Probe();
                p.Expect(returned, "복귀 화면(ReturnPopup)이 떴다");
                p.Near("비운 시간(시간)", away / 3600f, ComebackHours, 0.1f);
                var cap = ret != null ? FindDeep(ret.transform, "CapText") : null;
                var capLabel = cap != null ? cap.GetComponent<TMP_Text>() : null;
                capLine = capLabel != null ? ShownText(capLabel) : null;
                p.Info($"바구니 줄 CapText '{capLine ?? "없음"}' · 상한 {(Game.IdleChest != null ? Game.IdleChest.Cap.TotalHours : -1d):0.#}시간");
                Report("S4-33-return", "return_after_80h", p);
            });
            if (!Landed("S4-33", "comeback_card", "P4-6 · P4-2 CR-06", "ComebackCard")) yield break;
            if (ret != null && ret.IsOpen) ret.Close();
            var card = FindView("ComebackCard");
            float wait = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < wait && (card == null || !IsVisible(card.transform)))
            {
                card ??= FindView("ComebackCard");
                yield return null;
            }
            // 여는 연출(루트 알파 0 → 1)이 끝난 뒤 찍는다 (V2 r1 fix — 처음 보인 프레임은 알파 ≈ 0.25라 카드가 전투 위에 비쳐 도장 칸 대비를 볼 수 없었다)
            var cardPopup = card as UIPopup;
            yield return WaitPopupShown(cardPopup, ViewShownWait);
            yield return Shot("comeback_card", () =>
            {
                var p = new Probe();
                p.Expect(card != null && IsVisible(card.transform), "다시 만난 날 카드가 복귀 화면 뒤에 보인다");
                if (card != null)
                {
                    float alpha = cardPopup != null ? PopupAlpha(cardPopup) : EffectiveAlpha(card.transform);
                    p.Expect(alpha >= PopupShownAlpha, $"찍을 때 카드 알파 {alpha:0.00} (기대 ≥ {PopupShownAlpha:0.00} — 여는 연출 끝)");
                    var cells = FindDeep(card.transform, "Cells");
                    int cellCount = 0;
                    if (cells != null)
                    {
                        for (int i = 0; i < cells.childCount; i++)
                        {
                            var cell = cells.GetChild(i);
                            if (cell.gameObject.activeInHierarchy && cell.name.StartsWith("Cell", StringComparison.Ordinal)) cellCount++;
                        }
                    }
                    p.Expect(cellCount == ComebackCells, $"도장 칸 Cells/Cell* {cellCount}개 (기대 {ComebackCells}){(cells == null ? " — Cells 없음" : string.Empty)}");
                }
                p.Expect(capLine != null && capLine.Contains(ComebackCapText), $"복귀 화면 바구니 줄 '{capLine ?? "없음"}' (기대 상한 '{ComebackCapText}')");
                Report("S4-33", "comeback_card", p);
            });
            CloseAll();
        }

        // ═════════════════════════ idle ═════════════════════════

        /// <summary>
        /// 40 sleep_mode_fx (IDLE-06, P4-6 · P4-3b · AUD): 창 포커스가 있으면 손대지 않고 기다려 잠든다 (잠자기 분 = 2, IdleSleepWait 210초까지 — r9: 기다리는 동안 닫은 이벤트 시작 카드가 시계를 다시 세게 해 150초를 넘겼다; 안 되면 SleepNow),
        /// 포커스가 없으면(CanSleep M01) S4-40-natural SKIP 후 바로 SleepNow. 잠든 동안
        /// "[Verify] sleep_budget=60 numbers=off" (게임이 남긴 값) · 배경음 줄임. 전투가 SleepChanged를 아직 안 들으면 SKIP
        /// </summary>
        IEnumerator SleepModeFx()
        {
            const string id = "S4-40";
            var sleep = FindAnyObjectByType<SleepModeView>(FindObjectsInactive.Include);
            if (sleep == null)
            {
                SkipCheck(id, "sleep_mode_fx: SleepModeView 없음");
                yield break;
            }
            int minutesWas = GameSettings.SleepMinutes;
            if (minutesWas != 2) GameSettings.SleepMinutes = 2;
            float start = Time.realtimeSinceStartup;
            try
            {
                // SleepModeView.CanSleep은 PC에서 창이 포커스를 잃으면 false (M01) — Start-Process로 띄운 플레이어는 보통 포커스가 없어 저절로 잠들 수 없다:
                // 그때는 IdleSleepWait초를 기다리지 않고 저절로 잠들기를 SKIP으로 남긴 뒤 SleepNow (V2 r4)
                bool focused = Application.isFocused;
                // 열린 팝업도 잠자기를 막는다 (CanSleep: HasOpenPopup) — 기다리는 동안 게임이 띄운 창(이벤트 시작 카드 등)은 닫고 이름을 적는다 (V2 r4)
                var closed = new List<string>();
                if (focused)
                {
                    while (!sleep.IsAsleep && Time.realtimeSinceStartup - start < IdleSleepWait)
                    {
                        var top = UIManager.Instance != null ? UIManager.Instance.TopPopup : null;
                        if (top != null)
                        {
                            if (!closed.Contains(top.GetType().Name)) closed.Add(top.GetType().Name);
                            CloseAll();
                        }
                        yield return Wait(0.5f);
                    }
                }
                bool natural = sleep.IsAsleep;
                float idle = Time.realtimeSinceStartup - start;
                string popups = closed.Count > 0 ? $" · 기다리는 동안 닫은 창 {string.Join(", ", closed)}" : string.Empty;
                if (!focused) SkipCheck(id + "-natural", "sleep_mode_fx: 저절로 잠들기 SKIP — 창 포커스 없음 (Application.isFocused false · SleepModeView.CanSleep M01) → SleepNow");
                else if (!natural) Record("INFO", id + "-natural", $"sleep_mode_fx: 포커스 있는데 {IdleSleepWait:0}초 안에 저절로 안 잠듦 (입력이 들어왔나){popups} → SleepNow");
                else if (closed.Count > 0) Record("INFO", id + "-natural", $"sleep_mode_fx: 저절로 잠듦{popups}");
                if (!natural)
                {
                    sleep.SleepNow();
                    float limit = Time.realtimeSinceStartup + 3f;
                    while (!sleep.IsAsleep && Time.realtimeSinceStartup < limit) yield return null;
                }
                _sleepPath = natural ? $"저절로 ({idle:0}초)" : "SleepNow";
                Record("INFO", id, $"sleep_mode_fx: {(natural ? $"손대지 않고 {idle:0}초에 잠듦" : "SleepNow로 재움")} · 창 포커스 {focused} · 잠듦 {sleep.IsAsleep} · AppSignals.IsAsleep {AppSignals.IsAsleep}");
                yield return Wait(2f);
                yield return Shot("sleep_mode_fx", () =>
                {
                    string battleListeners = SleepListeners();
                    bool logged = _gameValues.TryGetValue("sleep_budget", out var budget);
                    _gameValues.TryGetValue("numbers", out var numbers);
                    if (!logged && string.IsNullOrEmpty(battleListeners))
                    {
                        SkipCheck(id, $"sleep_mode_fx: 아직 없음 — 전투 화면이 AppSignals.SleepChanged를 듣지 않는다 (P4-3b 잠자기 FX) · 잠듦 {sleep.IsAsleep}");
                        return;
                    }
                    if (!logged)
                    {
                        SkipCheck(id, $"sleep_mode_fx: 듣는 쪽 {battleListeners} — '[Verify] sleep_budget=' 줄 없음 (재는 길은 part B)");
                        return;
                    }
                    var p = new Probe();
                    p.Expect(sleep.IsAsleep, $"잠든 상태 (길: {_sleepPath})");
                    p.Expect(budget == "60", $"sleep_budget={budget} (기대 60)");
                    p.Expect(numbers == "off", $"numbers={numbers} (기대 off)");
                    Report(id, "sleep_mode_fx", p);
                    CheckSleepBgm();
                });
            }
            finally
            {
                if (GameSettings.SleepMinutes != minutesWas) GameSettings.SleepMinutes = minutesWas;
            }
            UIManager.Instance.OnBack();
            yield return Wait(0.8f);
        }

        /// <summary>
        /// S4-40-bgm (SPEC4 §11.4 40 'BGM duck logged', AUD 줄 — 잠들 때마다 한 번 "[Verify] bgm_duck=&lt;dB&gt; [bgm_lpf=&lt;Hz&gt;] bgm_sleep=&lt;duck|track|kept&gt; bgm_state=&lt;음악 상태&gt;"):
        /// PASS = (bgm_sleep=duck · bgm_duck=−9) 또는 (bgm_sleep=track · bgm_duck &lt; 0 — 사용자 라이브러리 −4). bgm_sleep=kept = 잠든 동안 배경음이 그대로 (보스 곡 —
        /// AUD 발견 F1, 오케스트레이터 R9 결정 전이라 INFO로 둔다), bgm_duck=n/a = 라이브러리에 LUFS 값이 없어 잴 수 없음 (INFO). 줄이 없으면 FAIL
        /// </summary>
        void CheckSleepBgm()
        {
            const string id = "S4-40-bgm";
            _gameValues.TryGetValue("bgm_duck", out var duck);
            _gameValues.TryGetValue("bgm_sleep", out var path);
            _gameValues.TryGetValue("bgm_state", out var state);
            _gameValues.TryGetValue("bgm_lpf", out var lpf);
            string seen = $"bgm_duck={duck ?? "없음"}{(lpf != null ? $" bgm_lpf={lpf}" : string.Empty)} bgm_sleep={path ?? "없음"} bgm_state={state ?? "없음"}";
            if (duck == null || path == null)
            {
                Check(id, false, $"sleep_mode_fx: 배경음 줄 '[Verify] bgm_duck=… bgm_sleep=…' 없음 — {seen} (AUD: 에디터 · 개발 빌드는 잠들 때마다 쓴다)");
                return;
            }
            if (path == "kept")
            {
                Record("INFO", id, $"sleep_mode_fx: {seen} — 잠든 동안 배경음이 줄지 않았다 (지금 곡 {state} 그대로). AUD 발견 F1 · 오케스트레이터 R9 결정 전이라 INFO (결정되면 FAIL)");
                return;
            }
            if (duck == "n/a")
            {
                Record("INFO", id, $"sleep_mode_fx: {seen} — 잠 곡으로 바꿨지만 라이브러리에 LUFS 값이 없어 줄어든 dB를 잴 수 없다");
                return;
            }
            bool number = float.TryParse(duck, NumberStyles.Float, CultureInfo.InvariantCulture, out float db);
            bool ok = path == "duck" ? duck == SleepBgmDuckDb : path == "track" && number && db < 0f;
            Check(id, ok, $"sleep_mode_fx: {seen} (기대 duck → bgm_duck={SleepBgmDuckDb} · track → bgm_duck < 0)");
        }

        /// <summary>AppSignals.SleepChanged를 듣는 전투(IdleGame.UI 중 이름에 Battle · Fx · Enemy가 든) 형식 이름들 (없으면 "")</summary>
        static string SleepListeners()
        {
            var field = typeof(AppSignals).GetField("SleepChanged", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field?.GetValue(null) is not Delegate handler) return string.Empty;
            var names = new StringBuilder();
            foreach (var target in handler.GetInvocationList())
            {
                var type = target.Method.DeclaringType;
                string name = type != null ? type.Name : string.Empty;
                if (name.Contains("Battle") || name.Contains("Fx") || name.Contains("Enemy")) names.Append(names.Length > 0 ? ", " : string.Empty).Append(name);
            }
            return names.ToString();
        }

        /// <summary>
        /// 42 firefly (NEW-03 스트레치, P4-3b · P4-2): 개발 플래그 -forceFirefly는 콘텐츠(perDay)와 상관없이 3초 뒤 반딧불을 띄운다 (StrayFireflyView.Forced) —
        /// 전장 띠(부모 기준 y BandBottom − Amplitude … BandTop + Amplitude)에 보이고, 누르면(OnPointerClick) 숨고 게임이 "[Verify] firefly_catch count=1/6 reward=n"을 쓴다.
        /// 보상(누르면 날아가는 선물)은 콘텐츠가 켜졌을 때만 — perDay 0이면 S4-42-reward SKIP (stretch off, G3에서 켠다). 플래그가 없으면 SKIP (스트레치 꺼짐이면 그 이유)
        /// </summary>
        IEnumerator Firefly()
        {
            const string id = "S4-42";
            int perDay = Game.MetaDb != null && Game.MetaDb.firefly != null ? Game.MetaDb.firefly.perDay : 0;
            if (!HasFlag(ForceFireflyArg))
            {
                SkipCheck(id, perDay <= 0 ? "firefly: SKIP (stretch off) — MetaDatabase.firefly.perDay 0 · 명령줄에 -forceFirefly 없음 (plan §3.2 #17, G3에서 켠다)"
                                          : $"firefly: 명령줄에 {ForceFireflyArg} 없음");
                yield break;
            }
            if (!Landed(id, "firefly", "P4-3b NEW-03", "StrayFireflyView")) yield break;
            CloseAll();
            UINav.GoToMain(true);
            var view = FindAnyObjectByType<StrayFireflyView>(FindObjectsInactive.Include);
            var root = view != null ? Field<RectTransform>(view, "_root") : null;
            float limit = Time.realtimeSinceStartup + FireflyWait;
            while (Time.realtimeSinceStartup < limit && (view == null || !view.IsFlying || root == null || !IsVisible(root))) yield return null;
            bool flying = view != null && view.IsFlying && root != null && IsVisible(root);
            // RI-5: 띠(y)만이 아니라 가로도 — 옆 화면 밖에서 들어오므로 ≤ 4초 기다려 틀 기둥(폰 0~1080 · 틀 창 FrameMinX~FrameMaxX) 안에 다 들어온 뒤 양쪽 끝을 본다
            bool InsideColumn(Rect r) => r.xMin >= FrameMinX - 0.5f && r.xMax <= FrameMaxX + 0.5f;
            Rect rect = flying ? ScreenRect(root) : new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
            float insideWaited = 0f;
            if (flying)
            {
                float started = Time.realtimeSinceStartup;
                float insideLimit = started + FireflyInsideWait;
                while (Time.realtimeSinceStartup < insideLimit && view.IsFlying && !InsideColumn(rect))
                {
                    yield return null;
                    rect = ScreenRect(root);
                }
                insideWaited = Time.realtimeSinceStartup - started;
                flying = view.IsFlying && IsVisible(root);
            }
            bool insideX = flying && InsideColumn(rect);
            float y = root != null ? root.anchoredPosition.y : float.NaN;
            bool inBand = flying && y >= StrayFireflyView.BandBottom - StrayFireflyView.Amplitude - 1f && y <= StrayFireflyView.BandTop + StrayFireflyView.Amplitude + 1f;
            yield return Shot("firefly", () =>
            {
                var p = new Probe();
                p.Expect(flying, $"반딧불이 {FireflyWait:0}초 안에 보인다 (-forceFirefly — 3초 뒤)");
                if (flying)
                {
                    p.Expect(inBand, $"전장 띠 안 y {y:0.#} (기대 {StrayFireflyView.BandBottom - StrayFireflyView.Amplitude:0} … {StrayFireflyView.BandTop + StrayFireflyView.Amplitude:0}) · 화면 {R(ScreenRect(root))}");
                    p.Expect(insideX, $"틀 기둥 안 x {rect.xMin:0.#} … {rect.xMax:0.#} (기대 {FrameMinX:0.#} … {FrameMaxX:0.#} — 들어오기 {insideWaited:0.0}초 기다림, 상한 {FireflyInsideWait:0}초)");
                }
                Report(id, "firefly", p);
            });
            if (!flying) yield break;
            _gameValues.Remove("count");
            _gameValues.Remove("reward");
            if (view.IsFlying) view.OnPointerClick(null);
            float logLimit = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < logLimit && !_gameValues.ContainsKey("count")) yield return null;
            _gameValues.TryGetValue("count", out var count);
            _gameValues.TryGetValue("reward", out var reward);
            var q = new Probe();
            q.Expect(!view.IsFlying, "누른 뒤 반딧불이 숨었다");
            q.Expect(count == "1/6", $"게임 줄 firefly_catch count={count ?? "없음"} (기대 1/6)");
            Report(id + "-tap", "firefly", q);
            int rewards = reward != null && int.TryParse(reward, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
            if (perDay <= 0) SkipCheck(id + "-reward", $"firefly: SKIP (stretch off) — MetaDatabase.firefly.perDay 0 → 보상 없음 (게임 줄 reward={reward ?? "없음"}, G3에서 켠다)");
            else Check(id + "-reward", rewards > 0, $"firefly: 누르면 보상 {reward ?? "없음"}개 (기대 ≥ 1 · 하루 {perDay})");
        }

        // ═════════════════════════ soak ═════════════════════════

        /// <summary>
        /// soak: 배속 2 · 자동 · 잠자기 끔 — 기본 20분. 1분마다 INFO (스테이지 · 프레임 · 예외), 끝에 S4-soak (예외 · 킬 스위치 · 꺼진 모듈 0) ·
        /// S4-soak-kwa (게임이 남긴 kill_without_arrival = 0 — 카운터가 아직 없으면 SKIP) · S4-soak-flash (flash_outside_field — 없으면 SKIP)
        /// </summary>
        IEnumerator Soak()
        {
            float minutes = 20f;
            string arg = Arg(SoakMinutesArg);
            if (arg != null && float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && parsed > 0f) minutes = parsed;
            var battle = Game.Battle;
            int speedWas = battle.Speed;
            bool autoWas = battle.AutoMode;
            int sleepWas = GameSettings.SleepMinutes;
            int errorsAt = _errorLines, exceptionsAt = _exceptionLines, killAt = _killSwitchLines;
            int highestAt = battle.HighestStage;
            try
            {
                battle.Speed = BattleSystem.MaxSpeed;
                battle.AutoMode = true;
                GameSettings.SleepMinutes = 0;
                yield return Shot("soak_start");
                float start = Time.realtimeSinceStartup;
                float end = start + minutes * 60f;
                float note = start + 60f;
                int frames = 0, minuteFrames = 0;
                float worst = 0f;
                while (Time.realtimeSinceStartup < end)
                {
                    frames++;
                    minuteFrames++;
                    worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                    if (Time.realtimeSinceStartup >= note)
                    {
                        Record("INFO", "S4-soak", $"soak {(note - start) / 60f:0}분: 스테이지 {battle.StageLabel} (최고 {battle.HighestStage}) · 프레임 {minuteFrames / 60f:0.#}/초 · 가장 긴 프레임 {worst * 1000f:0}ms · 예외 {_exceptionLines - exceptionsAt}");
                        note += 60f;
                        minuteFrames = 0;
                        worst = 0f;
                        CloseAll();
                    }
                    yield return null;
                }
                yield return Shot("soak_end");
                var p = new Probe();
                p.Info($"{minutes:0.#}분 · 스테이지 {highestAt} → {battle.HighestStage} · 평균 {frames / (minutes * 60f):0.#} 프레임/초");
                p.Expect(_exceptionLines - exceptionsAt == 0, $"예외 {_exceptionLines - exceptionsAt}번 (기대 0)");
                p.Expect(_errorLines - errorsAt == 0, $"오류 줄 {_errorLines - errorsAt}번 (기대 0)");
                p.Expect(_killSwitchLines - killAt == 0 && Game.IsMetaActive, $"메타 켜짐 {Game.IsMetaActive} · 킬 스위치 · 모듈 끔 줄 {_killSwitchLines - killAt} (기대 0)");
                ExpectModulesOn(p);
                Report("S4-soak", "soak", p);
                if (_gameValues.TryGetValue("kill_without_arrival", out var kwa)) Check("S4-soak-kwa", kwa == "0", $"soak: kill_without_arrival={kwa} (기대 0, ATK-09)");
                else SkipCheck("S4-soak-kwa", "soak: 아직 없음 — '[Verify] kill_without_arrival=' 줄 (P4-3b ATK-09 카운터)");
                if (_gameValues.TryGetValue("flash_outside_field", out var flash)) Record("INFO", "S4-soak-flash", $"soak: flash_outside_field={flash}");
                else SkipCheck("S4-soak-flash", "soak: 아직 없음 — '[Verify] flash_outside_field' 줄 (P4-9 ATK-12)");
            }
            finally
            {
                battle.Speed = speedWas;
                battle.AutoMode = autoWas;
                GameSettings.SleepMinutes = sleepWas;
            }
        }

        void ExpectModulesOn(Probe p)
        {
            var session = Game.MetaSession;
            if (session == null)
            {
                p.Expect(false, "MetaSession 없음");
                return;
            }
            var off = new StringBuilder();
            foreach (var module in session.Modules)
            {
                if (module != null && session.IsModuleDisabled(module)) off.Append(off.Length > 0 ? ", " : string.Empty).Append(module.GetType().Name);
            }
            p.Expect(off.Length == 0, $"모듈 {session.Modules.Count}개 중 꺼진 것 {(off.Length == 0 ? "없음" : off.ToString())}");
        }

        // ═════════════════════════ migrate (G1_prep §3 B) ═════════════════════════

        /// <summary>
        /// 옛 세이브 부팅 판: -autoshot-save &lt;v3 복사본&gt;이 지운 세이브 자리에 복사된 뒤 게임이 그 세이브로 깬다.
        /// MIG-1 v4로 읽힘 · 메타 켜짐 · 최고 스테이지 = 원본 · PlayMinutes 시드 끝 (도감 채우기 예약은 기록 — CodexSystem 스텁이면 true로 남는다) →
        /// 60초 전투 → mig_main · 성장 시트 mig_growth · 수선 일지 mig_journal → MIG-2 꺼진 모듈 · [Meta] · 예외 줄 0 →
        /// MIG-3 GameBootstrap.Save → 파일 v4 · "codex" 있음 · 최고 스테이지 ≥ 원본, 복사본을 새 저장소로 다시 읽으면 Loaded · v4 · 두 번 옮기지 않음
        /// </summary>
        IEnumerator RunMigrate()
        {
            yield return Wait(3f);
            Bind();
            int seedVersion = -1, seedStage = -1;
            if (s_seedPath != null && File.Exists(s_seedPath))
            {
                string json = File.ReadAllText(s_seedPath);
                seedVersion = JsonInt(json, "version");
                seedStage = JsonInt(json, "highestStage");
            }
            yield return Wait(2f);
            CloseAll();
            var data = Game.Data;
            var p = new Probe();
            p.Expect(s_seedCopied, $"시드 세이브 복사 ({s_seedPath ?? "-autoshot-save 없음"})");
            p.Expect(seedVersion == 3, $"원본 version {seedVersion} (기대 3)");
            p.Expect(data != null && data.version == PlayerData.CurrentVersion, $"읽힌 version {(data != null ? data.version : -1)} (기대 {PlayerData.CurrentVersion})");
            p.Expect(Game.IsMetaActive, "메타 켜짐");
            p.Expect(data != null && data.battle != null && data.battle.highestStage == seedStage, $"최고 스테이지 {(data?.battle != null ? data.battle.highestStage : -1)} (기대 원본 {seedStage})");
            p.Expect(data?.meta != null && !data.meta.playMinutesSeedPending, "PlayMinutes 시드 끝 (playMinutesSeedPending false)");
            p.Info($"도감 채우기 예약 {data?.meta?.codex?.backfillPending} (CodexSystem 스텁이면 true · 채운 뒤 false)");
            Report("MIG-1", "migrate_boot", p);

            UINav.GoToMain(true);
            // 접속 묶음(출석부 → 공지 → 이벤트 시작 카드)은 창 하나가 닫히면 다음 것을 띄운다 — 기다리는 동안 · 찍기 전에 게임이 띄운 창을 닫는다
            // (V2 r1 fix: 01_mig_main은 출석부, 02_mig_growth는 공지 창이었다). 닫은 창 · 찍을 때 맨 위 창은 MIG-2 기록에
            var closed = new List<string>();
            string mainTop = null, growthTop = null, journalTop = null;
            yield return CloseGamePopups(closed, 60f);
            yield return Shot("mig_main", () => mainTop = TopPopupName());
            CloseAll();
            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            yield return CloseGamePopups(closed);
            yield return Shot("mig_growth", () => growthTop = TopPopupName());
            UINav.GoToMain(true);
            yield return WaitSettled();
            yield return CloseGamePopups(closed);
            UINav.OpenJournal();
            yield return Wait(1.2f);
            yield return Shot("mig_journal", () => journalTop = TopPopupName());
            CloseAll();

            var q = new Probe();
            q.Info($"찍기 전에 닫은 게임 창 {(closed.Count > 0 ? string.Join(", ", closed) : "없음")} · 찍을 때 맨 위 창 mig_main {mainTop ?? "없음"} · mig_growth {growthTop ?? "없음"} · mig_journal {journalTop ?? "없음"} (기대 없음 · 없음 · JournalPopup)");
            ExpectModulesOn(q);
            q.Expect(_metaLines == 0, $"[Meta] 줄 {_metaLines}개 (기대 0 — 킬 스위치 · 모듈 끔 없음)");
            q.Expect(_exceptionLines == 0, $"예외 {_exceptionLines}번 (기대 0)");
            q.Info($"오류 줄 {_errorLines}");
            Report("MIG-2", "migrate_play", q);

            var r = new Probe();
            string path = GameBootstrap.SavePathOverride;
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Save();
            yield return null;
            string saved = path != null && File.Exists(path) ? File.ReadAllText(path) : null;
            r.Expect(saved != null, $"저장 파일 {path}");
            if (saved != null)
            {
                r.Expect(JsonInt(saved, "version") == PlayerData.CurrentVersion, $"저장 version {JsonInt(saved, "version")} (기대 {PlayerData.CurrentVersion})");
                r.Expect(saved.Contains("\"codex\""), "meta.codex 있음");
                int stage = JsonInt(saved, "highestStage");
                r.Expect(stage >= seedStage, $"저장 최고 스테이지 {stage} (기대 ≥ {seedStage})");
                string copy = Path.Combine(_dir, "mig_resave_check.json");
                try
                {
                    File.Copy(path, copy, true);
                    var storage = new LocalSaveStorage(copy);
                    var again = storage.Load();
                    r.Expect(storage.LastLoadResult == SaveLoadResult.Loaded && again != null && again.version == PlayerData.CurrentVersion,
                        $"다시 읽기 {storage.LastLoadResult} · version {(again != null ? again.version : -1)}");
                    r.Expect(again?.meta != null && !again.meta.playMinutesSeedPending, "다시 읽어도 PlayMinutes 시드를 다시 걸지 않는다 (두 번 옮기지 않음)");
                }
                catch (Exception e)
                {
                    r.Expect(false, $"다시 읽기 예외 {e.GetType().Name}: {e.Message}");
                }
            }
            Report("MIG-3", "migrate_resave", r);
        }

        /// <summary>JSON 글에서 처음 나오는 "키":정수 (없으면 −1) — 원본 v3 · 저장본의 최상위 version은 맨 앞, highestStage는 battle 안 하나뿐</summary>
        static int JsonInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return -1;
            var match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;
        }

        // ═════════════════════════ 시드 · 도우미 ═════════════════════════

        static WaitForSecondsRealtime Wait(float seconds) => new(seconds);

        /// <summary>새 세이브의 이야기 인트로를 넘긴다 (쪽 넘기기 — 늦어도 8번)</summary>
        IEnumerator SkipIntro()
        {
            yield return Wait(1f);
            var intro = FindAnyObjectByType<StoryIntroView>();
            for (int i = 0; i < 8 && intro != null && intro.isActiveAndEnabled; i++)
            {
                intro.OnPointerClick(null);
                yield return Wait(0.6f);
            }
            yield return Wait(1f);
        }

        /// <summary>FTUE를 끝낸다: 인트로를 넘기고 튜토리얼 건너뛰기 (보상 · 시작 팝업은 닫는다)</summary>
        IEnumerator FinishFtue()
        {
            yield return SkipIntro();
            if (Game.IsMetaActive && Game.Tutorial != null && Game.Tutorial.IsActive) Game.Tutorial.Skip();
            yield return Wait(0.8f);
            CloseAll();
            UINav.GoToMain(true);
            yield return Wait(0.5f);
            CloseAll();
        }

        /// <summary>
        /// 스테이지 cleared까지 깬 것으로 둔다: 최고 스테이지 = cleared + 1 (StageCleared 조건 = highestStage − 1), 막힌 곳 없음 → 조용한 기능 동기화
        /// (카드 · 점 없이 그 스테이지까지의 기능을 연다 — FeatureGateSystem.Evaluate의 silentSyncPending) → stage(기본 cleared + 1)를 다시 시작
        /// </summary>
        IEnumerator SeedCleared(int cleared, int stage = 0)
        {
            var battle = Game.Battle;
            var save = Game.Data?.battle;
            if (battle == null || save == null) yield break;
            save.highestStage = Math.Max(save.highestStage, cleared + 1);
            save.challengeTarget = 0;
            SilentSync();
            battle.GoToStage(stage > 0 ? stage : cleared + 1);
            yield return Wait(1f);
            CloseAll();
            Record("INFO", "S4-seed", $"{BattleSystem.FormatStage(cleared)}까지 깸 → 지금 {battle.StageLabel} · 최고 {battle.HighestStage}");
        }

        static void SilentSync()
        {
            if (!Game.IsMetaActive || Game.Features == null || Game.Data == null) return;
            var features = (Game.Data.meta ??= new MetaSave()).features ??= new FeatureSave();
            features.silentSyncPending = true;
            Game.Features.Evaluate();
        }

        /// <summary>
        /// 지금 스테이지를 실제로 끝낸다 (BattleSimulator.Clear / Fail — 비공개 · 다음 BattleSystem.Tick이 HandleEnd: 보상 · 최고 스테이지 · StageEnded).
        /// 스테이지가 돌 때까지 늦어도 8초 기다린다. 끝난 판 사이로 넘어가면 _forced = true
        /// </summary>
        IEnumerator ForceOutcome(bool clear)
        {
            _forced = false;
            var battle = Game.Battle;
            var method = typeof(BattleSimulator).GetMethod(clear ? "Clear" : "Fail", BindingFlags.Instance | BindingFlags.NonPublic);
            if (battle == null || method == null) yield break;
            float ready = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < ready && (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running)) yield return null;
            if (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running) yield break;
            method.Invoke(battle.Sim, null);
            float end = Time.realtimeSinceStartup + 2f;
            while (!battle.IsBetweenStages && Time.realtimeSinceStartup < end) yield return null;
            _forced = battle.IsBetweenStages;
        }

        /// <summary>
        /// 수선점이 맞아서 지는 실패: 스테이지가 돌 때 BattleSimulator._shopHp를 1로 (비공개) 두고 배속 2로 적이 문을 칠 때까지 (늦어도 25초) 기다린다.
        /// 그래도 안 지면 ForceOutcome(false). 끝난 판 사이로 넘어가면 _forced = true
        /// </summary>
        IEnumerator ShopFail()
        {
            _forced = false;
            var battle = Game.Battle;
            var hp = typeof(BattleSimulator).GetField("_shopHp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (battle == null) yield break;
            float ready = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < ready && (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running)) yield return null;
            int speedWas = battle.Speed;
            try
            {
                if (hp != null && battle.Sim.Outcome == BattleOutcome.Running)
                {
                    battle.Speed = BattleSystem.MaxSpeed;
                    hp.SetValue(battle.Sim, 1f);
                    float limit = Time.realtimeSinceStartup + 25f;
                    while (Time.realtimeSinceStartup < limit && !battle.IsBetweenStages)
                    {
                        if (battle.Sim.Outcome == BattleOutcome.Running && battle.Sim.ShopHp > 1f) hp.SetValue(battle.Sim, 1f);
                        yield return null;
                    }
                }
            }
            finally
            {
                battle.Speed = speedWas;
            }
            if (battle.IsBetweenStages)
            {
                _forced = battle.LastOutcome == BattleOutcome.Failed;
                Record("INFO", "S4-seed", $"{battle.StageLabel} 수선점이 맞아서 짐 ({battle.LastOutcome})");
                yield break;
            }
            Record("INFO", "S4-seed", $"{battle.StageLabel} 25초 안에 문이 안 맞았다 — BattleSimulator.Fail로 진다 (분석 기록 없음)");
            yield return ForceOutcome(false);
        }

        /// <summary>첫 주 콘텐츠(MetaDatabase.starterWeek.tasks)가 있는지</summary>
        static bool StarterWeekHasTasks()
        {
            var db = Game.MetaDb;
            return db != null && db.starterWeek != null && db.starterWeek.tasks != null && db.starterWeek.tasks.Count > 0;
        }

        /// <summary>
        /// 첫 주를 게임의 실제 길로 연다 (StarterWeekSystem.TryStart는 Missions 열림 알림과 AfterStartup에서만 돈다 — SeedCleared의 조용한 동기화는 알림을 내지 않는다):
        /// 수선 일지(Missions)가 잠겼으면 ForceUnlock(알림 → TryStart), 이미 열렸으면 AwayFor(0) — 일시정지 → 복귀(MetaSession.OnResume → AfterStartup → TryStart)
        /// </summary>
        IEnumerator StartStarterWeek()
        {
            var week = Game.StarterWeek;
            if (week == null || week.IsStarted || !StarterWeekHasTasks()) yield break;
            string path;
            if (Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Missions))
            {
                Game.Features.ForceUnlock(FeatureId.Missions, false);
                path = "Missions ForceUnlock (Unlocked 알림)";
                yield return Wait(0.3f);
            }
            else
            {
                path = "AwayFor(0) — 복귀 AfterStartup";
                yield return AwayFor(TimeSpan.Zero);
            }
            Record("INFO", "S4-seed", $"첫 주 열기: {path} → 시작 {week.IsStarted} · 진행 {week.IsActive} · 시작 날 {Game.Data.meta?.starterWeek?.startDayKey ?? "없음"}");
        }

        /// <summary>AwayFor: 복귀가 잰 비운 시간과 옮긴 시간의 허용 차 (초 — 일시정지 뒤 한 프레임 + 유닉스 초 내림)</summary>
        const int AwayToleranceSeconds = 2;

        /// <summary>
        /// 앱을 내렸다가(OnApplicationPause true — MetaSession.OnPause · 저장) 시계를 away만큼 앞으로 옮기고 다시 올린다 (OnResume 전에 시계가 바뀐다).
        /// 시계 옮기기와 복귀는 같은 프레임 — 그 사이에 Update가 돌면 MetaSession.Tick(DailyClock.Tick 1초마다 Refresh → maxSeenUnix)이나
        /// GameBootstrap.AutoSave(lastSaveUnix)가 새 시각을 먼저 적어 비운 시간이 0이 된다 (r2 comeback FAIL). 기기에서도 일시정지와 복귀 사이에는 Update가 없다.
        /// away > 0이면 복귀가 잰 비운 시간(meta.returnScreen.lastAwaySeconds)이 away ±AwayToleranceSeconds인지 보고, 아니면 INFO 'AwayFor race' 줄을 남긴다
        /// </summary>
        IEnumerator AwayFor(TimeSpan away)
        {
            var boot = GameBootstrap.Instance;
            if (boot == null || s_clock == null) yield break;
            boot.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);
            yield return null;
            // 아래 두 줄 사이에 yield를 넣지 않는다 (위 설명)
            s_clock.Offset += away;
            boot.SendMessage("OnApplicationPause", false, SendMessageOptions.DontRequireReceiver);
            string measured = "";
            if (away > TimeSpan.Zero && Game.IsMetaActive)
            {
                var returnSave = Game.Data?.meta?.returnScreen;
                long got = returnSave != null ? returnSave.lastAwaySeconds : -1;
                double want = away.TotalSeconds;
                measured = $" · 복귀가 잰 비운 시간 {got}초";
                if (Math.Abs(got - want) > AwayToleranceSeconds)
                    Record("INFO", "S4-seed", $"AwayFor race: 복귀가 잰 비운 시간 {got}초 (기대 {want:0}초 ±{AwayToleranceSeconds}) — " +
                                              "시계를 옮긴 뒤 복귀 전에 시각을 적은 것이 있다 (meta.clock.maxSeenUnix · lastSaveUnix · lastSessionEndUnix)");
            }
            yield return Wait(1f);
            Record("INFO", "S4-seed", $"비움 {away.TotalHours:0.#}시간 → 시계 {Game.DailyClock?.ToLocal(Game.DailyClock.NowUnix):yyyy-MM-dd HH:mm}{measured}");
        }

        /// <summary>게임 어셈블리의 형식 (IdleGame.UI.이름 → IdleGame.이름). 없으면 null — 아직 들어오지 않은 기능</summary>
        static Type GameType(string name)
        {
            var assembly = typeof(Game).Assembly;
            return assembly.GetType("IdleGame.UI." + name) ?? assembly.GetType("IdleGame." + name);
        }

        /// <summary>형식이 모두 있으면 true, 하나라도 없으면 SKIP "아직 없음 — 형식 (담당)"을 적고 false</summary>
        bool Landed(string id, string shot, string owner, params string[] types)
        {
            foreach (var name in types)
            {
                if (GameType(name) != null) continue;
                SkipCheck(id, $"{shot}: 아직 없음 — {name} ({owner})");
                return false;
            }
            return true;
        }

        /// <summary>그 형식 이름의 컴포넌트 (꺼진 것도 — 첫 번째). 형식이 없으면 null</summary>
        static Component FindView(string typeName)
        {
            var type = GameType(typeName);
            return type != null ? FindAnyObjectByType(type, FindObjectsInactive.Include) as Component : null;
        }

        static UIPopup FindPopup(string typeName) => FindView(typeName) as UIPopup;

        /// <summary>창 루트 CanvasGroup 알파 (UIPopup 여는 · 닫는 연출이 움직인다). 창이 없으면 0, CanvasGroup이 없으면 열림 = 1</summary>
        static float PopupAlpha(UIPopup popup)
        {
            if (popup == null) return 0f;
            var group = popup.GetComponent<CanvasGroup>();
            return group != null ? group.alpha : popup.IsOpen ? 1f : 0f;
        }

        /// <summary>창의 여는 연출이 끝날 때까지 기다린다 (루트 알파 ≥ PopupShownAlpha, 늦어도 max초 — 기본 PopupShownWait, 찍은 그림에 창이 보이게, P4-6 R-7c)</summary>
        static IEnumerator WaitPopupShown(UIPopup popup, float max = PopupShownWait)
        {
            float limit = Time.realtimeSinceStartup + max;
            while (popup != null && popup.IsOpen && PopupAlpha(popup) < PopupShownAlpha && Time.realtimeSinceStartup < limit) yield return null;
        }

        /// <summary>그 컴포넌트에 매개 없는 공개 메서드 name이 있으면 부르고 true</summary>
        static bool TryInvoke(Component target, string name)
        {
            if (target == null) return false;
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null) return false;
            method.Invoke(target, null);
            return true;
        }

        /// <summary>형식에 이름에 part가 든(대소문자 무시) 필드 · 메서드 · 속성이 있는지 (기능이 들어왔는지 어림)</summary>
        static bool HasMemberLike(Type type, string part)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(flags))
            {
                if (member.Name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>그 아래(켜진 것)에서 이름에 part가 든 첫 RectTransform</summary>
        static RectTransform FindNamePart(Transform root, string part)
        {
            if (root == null) return null;
            foreach (var child in root.GetComponentsInChildren<RectTransform>(false))
            {
                if (child != root && child.name.IndexOf(part, StringComparison.Ordinal) >= 0) return child;
            }
            return null;
        }

        /// <summary>그 아래 켜진 것 중 이름이 part로 시작하는 수</summary>
        static int CountNamed(Transform root, string part)
        {
            int count = 0;
            if (root == null) return count;
            foreach (var child in root.GetComponentsInChildren<RectTransform>(false))
            {
                if (child != root && child.name.StartsWith(part, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        /// <summary>그 아래 켜진 버튼 중 글에 part가 든 첫 것</summary>
        static Button FindButtonWithText(Transform root, string part)
        {
            if (root == null) return null;
            foreach (var button in root.GetComponentsInChildren<Button>(false))
            {
                if (FindText(button.transform, part) != null) return button;
            }
            return null;
        }
    }
}
