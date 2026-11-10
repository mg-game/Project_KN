using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.Verify
{
    /// <summary>
    /// 임시 검증 도구. 플레이어를 -autoshot &lt;폴더&gt; 로 실행하면 화면을 순서대로 열어 스크린샷을 남기고 종료한다.
    /// <para>
    /// 화면 틀 (UX-SPEC-IA §7.5 · A8): 탭은 이제 전투 아래쪽을 덮는 시트를 연다. MAIN(전투를 크게) · 시트 열림 중간 · 탭 시트 여섯 · 닫힘 중간 ·
    /// 뒤로 순서 · 머리 끌어 내림 · ≡ 메뉴 · 잠자기 장면을 기존 순서 사이에 끼워 넣고, 장면마다 "[Verify] PASS|FAIL|SKIP IA-x 장면: …" 줄을
    /// 플레이어 로그와 &lt;폴더&gt;/checks.txt에 남긴다 (INFO 줄은 세지 않는다, 끝에 요약 한 줄).
    /// 좌표는 1080x1920 기준 px · 화면 y는 위에서 잰다 (§1.1). 16:9에만 맞는 고정 숫자는 캔버스가 1080x1920일 때만 보고, 나머지는 캔버스 크기에서 계산한다.
    /// 장면은 이름으로 비교한다 (끼워 넣을 때마다 번호가 밀린다, §7.4). 예전 05 battle은 main이다 (§8.7).
    /// </para>
    /// <para>
    /// 시트 자동 복귀(GameSettings.SheetAutoReturn — PlayerPrefs)는 도는 동안 끄고 끝날 때 원래 값으로 되돌린다 (CR-04: 시트가 열린 채 팝업 없이 60초가 지나면
    /// 시간에 따라 장면이 달라진다). battle_later는 GoToMain(true)로 MAIN을 만든 뒤 20초 기다린다.
    /// -autoshot-tall을 함께 주면 길쭉한 화면 판만 돈다 (MAIN → 강화 시트: 그림 덮개 ≤ 1 · 링 비율 · 올림 규칙 ±4, §7.5 tall) —
    /// 예: -autoshot &lt;폴더&gt;\tall -autoshot-tall -screen-width 540 -screen-height 1170 -screen-fullscreen 0 (창이 모니터보다 크면 OS가 줄일 수 있다 — 실제 크기는 IA-env 줄).
    /// </para>
    /// <para>
    /// Wave 3 (WP-9, AutoShot.Wave3.cs): UX-SPEC §5.2의 빠진 장면 — 02b 인트로 뒤로 · 03 빈 말풍선 자리 · 04b FTUE 6단계 보상 · 04c 원정 준비 ·
    /// 11b 파밍 · 11c 리본 · 11d 토스트 줄 · 11e 길게 누르기 + 포커스 · 12b/16b 모자람 · 16c 10회 확인 · 15b 다시 보내기 · 18b 의상 탭 · 20b 결과 덮개 ·
    /// 27b 개방 카드 날아가기 · 28b 부족 · 28c 겹친 팝업 · 28d 우편 + 보상 두 버튼(의상 구매). 소환 탭을 덮는 첫 만남 카드는 탭 장면 전에 받는다.
    /// MAIN 검사에 가이드 줄 자리(IA-guide PASS/FAIL) · 배지 묶음 ≤ 5 · WP-5 받아들임(AUTO/x1 ≥ 88 · 시너지 칩 ≤ 540 · 보스 버튼 자리)을 더했다.
    /// 아직 없는 대상은 SKIP과 이유를 적는다 (FAIL이 아니다).
    /// </para>
    /// <para>
    /// Wave 3 r1 수정 (AutoShot.Battle.cs): 05 스테이지 판 글자 명암(S-05-contrast) · 06–11 battle_fx 컷마다 말풍선 · 적 HP 막대(S-06…S-11, 모아서 S-06-hp) ·
    /// battle_boss / battle_boss_sheet 보스 이름표 묶음 자리(S-boss-main · S-boss-sheet) · 29 보스 진행 줄(S-29 · S-boss-bar). PASS 줄에도 잰 값을 남긴다 (Probe).
    /// </para>
    /// <para>
    /// RM-2 V2 (AutoShot.Spec4.cs): -autoshot-spec4 &lt;시나리오&gt;는 SPEC4 §11.4 장면을 따로 돈다 (이 전체 판의 번호를 밀지 않게 — 실행마다 새 세이브).
    /// -autoshot-migrate + -autoshot-save &lt;v3 복사본&gt;은 옛 세이브 부팅 판 (MIG-1..3). -autoshot-fixture &lt;파일&gt;은 지운 세이브 자리에 복사해 넣는다.
    /// Wave 3 남은 검사(보스 몸 vs 말풍선 · 리본 보상 넷 · 긴 안내 · 불 밝히기 합계 · 보스 치명타 · 빠른 줄 NEW · 고른 탭 글 · 최대 레벨)는 이 판 끝(battle_later 뒤)에 붙여 앞 번호를 그대로 둔다.
    /// </para>
    /// 단계마다 예외를 잡아 FAIL 한 줄로 남기고 다음 단계로 간다 (Safe) — 도구가 중간에 멈춰 시간 초과로 죽지 않게.
    /// </summary>
    public partial class AutoShot : MonoBehaviour
    {
        // ───────────── 경로 (Canvas 기준, CoachTargets와 같은 틀) ─────────────

        const string StagePath = "SafeArea/StageArea";
        const string BattlePath = StagePath + "/Battle";
        const string FieldPath = BattlePath + "/Field";
        const string BackdropPath = BattlePath + "/Background/FieldArt";
        const string HudPath = BattlePath + "/Hud";
        const string MetaHudPath = BattlePath + "/MetaHud";
        const string RailLeftPath = MetaHudPath + "/QuickRail_L";
        const string RailRightPath = MetaHudPath + "/QuickRail_R";
        const string ContentPath = "SafeArea/ContentArea";
        const string HeaderPath = ContentPath + "/SheetHeader";
        const string DragAreaPath = HeaderPath + "/DragArea";
        const string IndicatorPath = "SafeArea/TabBar/Indicator";
        const string MainGlowPath = CoachTargets.TabMain + "/Glow";
        const string SleepCardPath = "SafeArea/SleepMode/Overlay/Frame/Card";
        const string RailLabelPath = "Visual/LabelPill/Label";

        // ───────────── UX-SPEC-IA 숫자 (기준 px — 코드 상수 대신 문서 값을 그대로 둔다) ─────────────

        const float SpecTopBar = 130f;
        const float SpecTabBar = 170f;
        const float SpecStrip = 156f;
        const float SpecHeader = 56f;
        const float SpecContent = 780f;
        const float SpecSheet = SpecHeader + SpecContent;
        const float SpecRingWorld = 1.6f;
        /// <summary>field_tall 1080x2048 링 반지름 (uv, 가로 — §1.4)</summary>
        const float SpecTallRingRadiusX = 0.30370f;
        /// <summary>16:9 옆 탭 가운데 x (§2.3 — 강화 · 편성 · 친구들 · 원정 · 소환 · 상점)</summary>
        static readonly float[] SpecTabCenters = { 74f, 222f, 370f, 710f, 858f, 1006f };
        /// <summary>16:9 MAIN 생성 점 (반지름 2.5, 길 0~4 — §1.3)</summary>
        static readonly Vector2[] SpecSpawns = { new(539.5f, 553.6f), new(52.1f, 810.6f), new(238.3f, 1226.4f), new(840.7f, 1226.4f), new(1026.9f, 810.6f) };
        /// <summary>상단 바 칸 이름과 1080 폭 사각형 (§3.1 — x 왼쪽, y 위에서: xMin, yMin, xMax, yMax)</summary>
        static readonly string[] TopBarItems = { "LevelText", "PowerChip", "GoldChip", "GoldPerSecText", "GemChip", "MailButton", "MenuButton" };
        static readonly Rect[] SpecTopBarRects =
        {
            Rect.MinMaxRect(24, 10, 316, 50), Rect.MinMaxRect(16, 54, 316, 116), Rect.MinMaxRect(394, 14, 634, 74), Rect.MinMaxRect(394, 80, 634, 108),
            Rect.MinMaxRect(646, 14, 856, 74), Rect.MinMaxRect(868, 17, 964, 113), Rect.MinMaxRect(972, 17, 1068, 113),
        };
        /// <summary>
        /// ≡ 메뉴 칸 순서 (§3.6 + SPEC4 3줄 칸 — UX-SPEC-IA §8.8 · 로드맵 델타 36 · CR4-09: 활동 칸 뒤, 가방 · 공지 · 설정 앞).
        /// 빌더가 만든 칸만 이 순서대로 보이면 된다 (P4-4가 3줄 칸을 넣기 전에는 처음 여섯 — 줄 수 = ⌈칸/3⌉, 창 높이 300 + 220·줄)
        /// </summary>
        const string MenuTileOrder = "JournalTile · AttendanceTile · EventTile · EventHubTile · SeasonTile · CodexTile · AchievementsTile · CouponTile · BagTile · NoticeTile · SettingsTile";
        /// <summary>SPEC4 전부터 있는 여섯 칸 (늘 있어야 한다)</summary>
        const string MenuTileBase = "JournalTile · AttendanceTile · EventTile · BagTile · NoticeTile · SettingsTile";
        /// <summary>SPEC4 3줄 칸 (P4-4 FrameSceneBuilder — 이름 · MenuPopup 필드). 하나라도 묶인 장면이면 다섯 다 묶여야 하고, 콘텐츠가 켜진 칸은 보여야 한다</summary>
        static readonly (string tile, string field)[] MenuSpec4Tiles =
        {
            ("EventHubTile", "_eventHubTile"), ("SeasonTile", "_seasonTile"), ("CodexTile", "_codexTile"), ("AchievementsTile", "_achievementsTile"), ("CouponTile", "_couponTile"),
        };

        /// <summary>sheet_drag: 머리를 끌어 내리는 거리 (캔버스 px) · 시간 (§7.5 — 300px 0.2초 = p ≈ 0.64, 1500px/s)</summary>
        const float DragDistance = 300f;
        const float DragSeconds = 0.2f;

        /// <summary>IA-sleep 조용한 프레임 (RI-1): 불 밝히기 뒤 이만큼 지나야 잰다 · 기다리는 상한 · 재는 수 · 사이 (초)</summary>
        const float QuietLampSeconds = 2.5f;
        const float QuietLampWait = 6f;
        const int QuietSamples = 5;
        const float QuietSampleGap = 0.15f;

        /// <summary>gacha_result (RI-4): 공개 끝을 기다리는 상한 · 끝난 뒤 찍기까지 (강조 판 페이드 0.3초 + 여유) · 결과 창을 못 찾으면 예전 고정 대기 (초)</summary>
        const float GachaResultWait = 10f;
        const float GachaResultSettle = 0.6f;
        const float GachaResultFallback = 4.5f;

        static readonly Vector3[] Corners = new Vector3[4];

        string _dir;
        int _index;
        bool _tall;
        int _pass;
        int _fail;
        int _skip;
        bool _autoReturnWas;
        bool _autoReturnOff;
        /// <summary>main 장면 가운데 띠 밝기 (sleep 장면과 비교해 기록만 한다)</summary>
        float _mainLuma = -1f;

        RectTransform _root;
        Canvas _canvas;
        MainTabController _tabs;
        BattleFieldMapper _mapper;
        BattleFrameDriver _driver;

        string ChecksPath => Path.Combine(_dir, "checks.txt");

        /// <summary>
        /// 검증 실행은 전용 세이브로 매번 새 게임 (에디터/실제 세이브를 건드리지 않음). -autoshot-fixture &lt;파일&gt; (또는 -autoshot-save &lt;파일&gt;)을 주면
        /// 지운 뒤 그 파일을 복사해 넣는다 — 원본은 읽기만 한다 (V2: 옛 세이브 부팅 · 시드 세이브). 원본이 없으면 새 게임으로 가고 Start에서 FAIL 한 줄
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void UseScratchSave()
        {
            var dir = Arg("-autoshot");
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "autoshot_player.json");
            foreach (var file in new[] { path, path + ".bak", path + ".tmp" })
            {
                if (File.Exists(file)) File.Delete(file);
            }
            s_seedPath = Arg(FixtureArg) ?? Arg(SaveSeedArg);
            s_seedCopied = false;
            if (s_seedPath != null)
            {
                try
                {
                    if (File.Exists(s_seedPath) && !string.Equals(Path.GetFullPath(s_seedPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(s_seedPath, path, true);
                        s_seedCopied = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Verify] 시드 세이브 복사 실패 {s_seedPath}: {e.Message}");
                }
            }
            GameBootstrap.SavePathOverride = path;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var dir = Arg("-autoshot");
            if (dir == null) return;
            var go = new GameObject("[AutoShot]");
            DontDestroyOnLoad(go);
            var shot = go.AddComponent<AutoShot>();
            shot._dir = dir;
            shot._tall = HasFlag("-autoshot-tall");
            shot._spec4 = Arg(Spec4Arg);
            shot._migrate = HasFlag(MigrateArg);
        }

        /// <summary>이번 실행 이름 (checks.txt 요약 · 예외 줄): migrate · spec4-&lt;시나리오&gt; · tall · full</summary>
        string RunName => _migrate ? "migrate" : _spec4 != null ? "spec4-" + _spec4 : _tall ? "tall" : "full";

        IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(ChecksPath, string.Empty);
            Application.logMessageReceived += OnLog;
            TurnOffAutoReturn();
            RecordSeed();
            IEnumerator routine = _migrate ? RunMigrate() : _spec4 != null ? RunSpec4() : _tall ? RunTall() : RunFull();
            yield return Safe(RunName, routine);
            Finish();
        }

        /// <summary>시드 세이브를 받았으면 그 결과 한 줄 (복사 못 했으면 FAIL — 새 게임으로 돌았다)</summary>
        void RecordSeed()
        {
            if (s_seedPath == null) return;
            if (s_seedCopied) Record("INFO", "S4-seed", $"시드 세이브 {s_seedPath} → {GameBootstrap.SavePathOverride} (원본은 읽기만)");
            else Check("S4-seed", false, $"시드 세이브 {s_seedPath}를 복사하지 못했다 (없음 · 같은 경로) — 새 게임으로 돈다");
        }

        // ═════════════════════════ 전체 순서 ═════════════════════════

        IEnumerator RunFull()
        {
            yield return new WaitForSecondsRealtime(3f);
            Bind();
            yield return Shot("startup", CheckEnvironment);

            // 새 게임 = 튜토리얼: 이야기 인트로 → 첫 코치마크 → 잠긴 탭이 보이는 첫 전투
            var intro = FindAnyObjectByType<StoryIntroView>();
            if (intro != null && intro.isActiveAndEnabled)
            {
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("ftue_intro_2");
                yield return Safe("ftue_intro_back", IntroBack(intro));
                for (int i = 0; i < 6 && intro.isActiveAndEnabled; i++)
                {
                    intro.OnPointerClick(null);
                    yield return new WaitForSecondsRealtime(1.2f);
                }
            }
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("ftue_coach", () => CheckCoachBubble("ftue_coach"));
            yield return Safe("ftue_coach_sheet", FtueCoachSheet());
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("ftue_battle_locked_tabs");
            // 새 세이브의 가방 (잠긴 던전 칸 — Wave 2 r1 수정, 찍기만) → FTUE 6단계(강화 x1)까지 실제로 몰아 보상 · 날아가기를 본다 (04b)
            yield return Safe("bag_fresh", BagFresh());
            yield return Safe("ftue_step6_reward", FtueStep6());

            // 나머지 화면은 튜토리얼을 건너뛰고 모든 기능을 연 MAIN에서 본다 (§7.5 main)
            UnlockAll();
            CloseAll();
            UINav.GoToMain(true);
            yield return new WaitForSecondsRealtime(4f);
            // MAIN 검사 + 스테이지 판 글자 명암 (§5.2 05 — 찍는 프레임 화면에서, AutoShot.Battle)
            yield return ShotWithFrame("main", frame =>
            {
                CheckMain("main");
                CheckHudContrast("main", frame);
            });
            yield return MeasureLuma(value => _mainLuma = value);
            // 공격 이펙트 연속 컷 (발사체/타격/피해 숫자) — 컷마다 말풍선 · 적 HP 막대 (§5.2 06–11 = S-06…S-11), 모아서 S-06-hp
            ResetFxBars();
            for (int i = 0; i < FxShots; i++)
            {
                yield return new WaitForSecondsRealtime(0.18f);
                string fxShot = $"battle_fx_{i}";
                string fxId = $"S-{6 + i:00}";
                yield return Shot(fxShot, () => CheckBattleFx(fxId, fxShot));
            }
            yield return Safe("battle_fx_hp", FxBarsSeen());
            // 보스를 불러 이름표 묶음 자리를 MAIN · 강화 시트에서 (WP-5B 요청) + 보스 중 진행 줄
            yield return Safe("battle_boss", BattleBoss());

            // Wave 3 전투 장면 (11b 파밍 · 11c 리본 · 11d 토스트 줄) + 소환 탭을 덮는 첫 만남 카드를 먼저 받는다 (16 천장 · 제목 상자)
            yield return Safe("battle_farm", BattleFarm());
            yield return Safe("battle_ribbon", BattleRibbon());
            yield return Safe("toast_stack", ToastStack());
            yield return Safe("first_meeting", FirstMeeting());

            // 탭 시트: 열리는 중간 → 여섯 탭 (열린 채 바꾸기) → 상점 탭 다시 누름으로 닫기 → 닫힌 뒤
            yield return Safe("sheet_opening", SheetOpening());
            foreach (MainTab tab in Enum.GetValues(typeof(MainTab)))
            {
                _tabs.Select(tab);
                yield return new WaitForSecondsRealtime(1.2f);
                var current = tab;
                yield return ShotWithFrame($"tab_{tab}", frame =>
                {
                    CheckSheetTab($"tab_{current}", current);
                    CheckSelectedTabBadge($"tab_{current}", current);
                    CheckSelectedTabLabel($"tab_{current}", current, frame);
                    if (current == MainTab.Gacha) CheckGachaTab($"tab_{current}");
                });
            }
            yield return Safe("sheet_closing", SheetClosing());
            yield return Safe("back_order", BackOrder());
            yield return Safe("sheet_drag", SheetDrag());
            yield return Safe("menu", Menu());

            // Wave 3: 길게 누르기 + 포커스 잃음 (11e) · 모자람 (12b · 16b) · 10회 소환 확인 (16c)
            yield return Safe("focus_hold", FocusHold());
            yield return Safe("tab_Growth_short", GrowthShort());
            yield return Safe("tab_Gacha_short", GachaShort());
            yield return Safe("gacha_confirm", GachaConfirm());

            // 짝짝이 레벨 5 → 1차 진화 단서 발견 상태로 진화 기억도/레시피 확인
            Game.Currency.Add(CurrencyType.Gold, 1e6);
            for (int i = 0; i < 4; i++) Game.Characters.TryLevelUp("jjakjjak");
            UINav.OpenCharacter("jjakjjak");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("character_jjakjjak");
            UIManager.Instance.Get<EvolutionRecipePopup>().Show("jjak_caravan");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("recipe_caravan");
            CloseAll();
            // 보상 두 버튼(의상 구매 '바로 입혀 보기') → 친구 상세 의상 탭 (18b)
            yield return Safe("reward_two_button", RewardTwoButton());

            Game.Currency.Add(CurrencyType.Gem, 5000);
            _tabs.Select(MainTab.Gacha);
            yield return new WaitForSecondsRealtime(0.5f);
            FindAnyObjectByType<GachaPanel>()?.Pull(10);
            // 고정 4.5초 대신 공개가 끝날 때(GachaResultPopup._buttonsVisible — Finish가 켠다)까지 · 늦어도 10초, 그다음 강조 판 페이드만큼 (RI-4)
            float revealed = -1f;
            yield return WaitGachaButtons(GachaResultWait, seconds => revealed = seconds);
            Record("INFO", "S-16c-wait", revealed >= 0f
                ? $"gacha_result: Pull(10) 뒤 공개 끝 {revealed:0.00}초 → +{GachaResultSettle:0.0}초에 찍는다"
                : $"gacha_result: {GachaResultWait:0}초 안에 공개 끝(_buttonsVisible)을 못 봤다 — 그대로 찍는다");
            yield return new WaitForSecondsRealtime(GachaResultSettle);
            yield return Shot("gacha_result", () =>
            {
                // Pull(10)은 확인 없이 바로 결과 (M55 — 확인은 버튼만, 16c)
                var result = UIManager.Instance.Get<GachaResultPopup>();
                var message = UIManager.Instance.Get<MessagePopup>();
                Check("S-16c-direct", result != null && result.IsOpen && (message == null || !message.IsOpen),
                    $"gacha_result: Pull(10) → 결과 {OpenText(result != null && result.IsOpen)} · 확인 창 {OpenText(message != null && message.IsOpen)} (기대 결과만)");
            });
            CloseAll();
            yield return Safe("gacha_result_dim", GachaResultDim());

            UIManager.Instance.Get<BagPopup>().Open();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("bag");
            CloseAll();

            _tabs.Select(MainTab.Formation);
            yield return new WaitForSecondsRealtime(0.8f);
            UINav.OpenSynergy(null);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("synergy_codex");
            CloseAll();

            if (Game.IsMetaActive)
            {
                UINav.OpenJournal();
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("meta_journal");
                CloseAll();

                UINav.OpenAttendance();
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("meta_attendance");
                CloseAll();

                long cap = (long)Game.IdleChest.Cap.TotalSeconds;
                Game.Data.meta.idleChest.lastClaimUnix = Game.DailyClock.NowUnix - cap;
                Game.IdleChest.AddOfflineTime(cap);
                UINav.OpenIdleChest();
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("meta_idle_chest");
                CloseAll();

                UIManager.Instance.Get<ReturnPopup>()?.Show(3 * 3600 + 25 * 60);
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("meta_return");
                CloseAll();

                UIManager.Instance.Get<FeatureUnlockCard>()?.Show(FeatureId.Missions);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("meta_unlock_card");
                CloseAll();
                yield return Safe("unlock_decline_fly", UnlockDeclineFly());
            }

            UIManager.Instance.Get<SettingsPopup>()?.Open();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("settings");
            CloseAll();

            // Wave 3: 원정 준비 · 다시 보내기 (04c · 15b) · 부족 (28b) · 겹친 팝업 (28c) · 우편 (28d)
            yield return Safe("expedition", Expedition());
            yield return Safe("shortfall", Shortfall());
            yield return Safe("popup_stack", PopupStack());
            yield return Safe("mail", Mail());

            yield return Safe("sleep", SleepShot());

            // 마지막 장면은 MAIN에서 (편성 시트가 열린 채 남아 있던 예전 순서 — CR-04)
            CloseAll();
            UINav.GoToMain(true);
            // 기다리는 20초 동안 게임이 띄운 창(이벤트 시작 카드 · 기능 열림 카드(FeatureUnlockCard) — 스테이지 사이에 꺼낸다)은 닫는다 (V2 r1 fix: 축제 기간에 60_battle_later가 이벤트 시작 카드였다)
            var laterClosed = new List<string>();
            yield return CloseGamePopups(laterClosed, 20f);
            if (laterClosed.Count > 0) Record("INFO", "IA-later-popups", $"battle_later: 기다리는 동안 닫은 게임 창 {string.Join(", ", laterClosed)}");
            yield return Shot("battle_later", CheckLater);
            // §5.2 29 '보스 막대가 처치를 따른다' (막대가 따라가는 중일 수 있어 맞는 프레임을 1.5초 찾는다 — AutoShot.Battle)
            yield return Safe("battle_later_bar", BossBarSettles("S-29", "battle_later"));

            // Wave 3 남은 검사 (RM-2 V2 — 앞 장면 번호를 밀지 않게 맨 끝에)
            yield return Safe("wave3_tail", Wave3Tail());
        }

        // ═════════════════════════ 길쭉한 화면 판 (-autoshot-tall) ═════════════════════════

        /// <summary>
        /// 짧은 판 (§7.5 tall): MAIN — 그림 덮개 ≤ 1.0 · 링 비율 1 ±0.02 + 틀 · 생성 점 · 상단 바 · 빠른 아이콘, 강화 시트 — 올림 = 규칙 ±4 + 시트 자리.
        /// 규칙은 MAIN에서 잰 수선점 깊이 · StageArea 높이 · Ppu · 캔버스 배율로 여기서 따로 계산한다 (BattleFrameDriver 코드를 쓰지 않는다)
        /// </summary>
        IEnumerator RunTall()
        {
            yield return new WaitForSecondsRealtime(3f);
            Bind();
            UnlockAll();
            CloseAll();
            UINav.GoToMain(true);
            yield return new WaitForSecondsRealtime(3f);
            float mainShop = float.NaN;
            yield return ShotWithFrame("tall_main", frame =>
            {
                CheckMain("tall_main");
                CheckTallCover("tall_main");
                mainShop = ShopScreenY() - ScreenRect(Find(StagePath)).yMin;
                CheckHudContrast("tall_main", frame);
            });
            _tabs.Select(MainTab.Growth);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("tall_sheet", () =>
            {
                CheckSheetTab("tall_sheet", MainTab.Growth);
                CheckTallPan("tall_sheet", mainShop);
            });
        }

        // ═════════════════════════ IA 장면 ═════════════════════════

        /// <summary>
        /// ftue_coach_sheet (§7.5, ftue_coach 바로 뒤): 첫 소환 단계(덱 첫 칸 — 시트 밑 대상)에서 강화 시트를 열면 0.6초 안에 코치 대상이
        /// 사냥 버튼(Tab_Main)이 되고 손가락이 보인다 → 찍고 GoToMain(true). 단계 1(첫 전투, 대상 없음)이면 단계 2까지 기다린다 (12초까지)
        /// </summary>
        IEnumerator FtueCoachSheet()
        {
            var coach = FindAnyObjectByType<CoachMarkView>(FindObjectsInactive.Include);
            if (!Game.IsMetaActive || coach == null || _tabs == null)
            {
                SkipCheck("IA-coach", "ftue_coach_sheet: 튜토리얼 · 코치마크 없음");
                yield break;
            }
            float limit = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < limit && !CoachOnLowerTarget(coach)) yield return null;
            bool ready = CoachOnLowerTarget(coach);
            string before = PathOf(coach.Target);
            string step = StepLabel();

            _tabs.Select(MainTab.Growth);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("ftue_coach_sheet", () =>
            {
                if (!ready)
                {
                    SkipCheck("IA-coach", $"ftue_coach_sheet: 12초 안에 코치가 시트 밑 대상을 가리키지 않았다 (단계 {step}, 대상 {before})");
                    return;
                }
                var p = new Probe();
                var main = Find(CoachTargets.TabMain);
                p.Expect(coach.Target != null && coach.Target == main, $"코치 대상 {PathOf(coach.Target)} (기대 {CoachTargets.TabMain})");
                p.Expect(coach.IsActive && !coach.Suppressed, "코치마크 안내가 보여야 한다");
                var finger = Finger(coach);
                p.Expect(finger != null && finger.IsVisible && finger.gameObject.activeInHierarchy, "손가락이 보여야 한다");
                p.Info($"단계 {step} · 열기 전 대상 {before}");
                Report("IA-coach", "ftue_coach_sheet", p);
            });
            UINav.GoToMain(true);
        }

        /// <summary>sheet_opening (§7.5): MAIN에서 강화 시트를 열고 0.12초 뒤 — 0.2 &lt; p &lt; 0.9, 올림 = PanOpen·p ±2</summary>
        IEnumerator SheetOpening()
        {
            CloseAll();
            UINav.GoToMain(true);
            yield return null;
            _tabs.Select(MainTab.Growth);
            yield return new WaitForSecondsRealtime(0.12f);
            yield return Shot("sheet_opening", () =>
            {
                if (UIMotion.Reduced)
                {
                    SkipCheck("IA-opening", "sheet_opening: 줄이기 설정 — p가 계단으로 바뀐다 (미끄러짐 없음)");
                    return;
                }
                var p = new Probe();
                float progress = UIFrame.SheetProgress;
                p.Expect(progress > 0.2f && progress < 0.9f, $"p {progress:0.000} (기대 0.2 < p < 0.9)");
                p.Near("올림 − PanOpen·p", _mapper.Pan - _driver.PanOpen * progress, 0f, 2f);
                p.Expect(UIFrame.SheetOpen && UIFrame.SheetTab == (int)MainTab.Growth, "목표 상태 = 강화 시트 열림");
                Report("IA-opening", "sheet_opening", p);
            });
        }

        /// <summary>
        /// sheet_closing (§7.5): 상점 시트가 열린 채 상점 탭을 손으로 다시 누름(onClick) → 0.10초 뒤 p가 내려가는 중 · Reselected 한 번.
        /// main_after_close: +0.4초 — p 0, ContentArea 꺼짐, 올림 0
        /// </summary>
        IEnumerator SheetClosing()
        {
            if (_tabs.SelectedIndex != (int)MainTab.Shop)
            {
                _tabs.Select(MainTab.Shop);
                yield return WaitSettled();
            }
            int reselected = 0;
            int selected = 0;
            void OnReselected(MainTab tab) => reselected++;
            void OnSelected(MainTab tab) => selected++;
            _tabs.Reselected += OnReselected;
            _tabs.Selected += OnSelected;
            try
            {
                _tabs.GetTabButton(MainTab.Shop).onClick.Invoke();
                float tapped = UIFrame.SheetProgress;
                yield return new WaitForSecondsRealtime(0.05f);
                float middle = UIFrame.SheetProgress;
                yield return new WaitForSecondsRealtime(0.05f);
                yield return Shot("sheet_closing", () =>
                {
                    var p = new Probe();
                    float progress = UIFrame.SheetProgress;
                    p.Expect(!UIFrame.SheetOpen && _tabs.SelectedIndex == -1, "목표 상태 = MAIN (누른 그 프레임부터)");
                    p.Expect(reselected == 1, $"Reselected {reselected}번 (기대 1)");
                    p.Expect(selected == 0, $"Selected {selected}번 (기대 0 — 다시 누름은 고르기가 아니다)");
                    if (UIMotion.Reduced) p.Info("줄이기 — p 중간값은 보지 않는다");
                    else p.Expect(progress > 0f && progress < middle && middle <= tapped, $"p {tapped:0.00} → {middle:0.00} → {progress:0.00} (내려가는 중)");
                    Report("IA-closing", "sheet_closing", p);
                });
            }
            finally
            {
                _tabs.Reselected -= OnReselected;
                _tabs.Selected -= OnSelected;
            }

            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("main_after_close", () => ReportMain("IA-closed", "main_after_close"));
        }

        /// <summary>
        /// back_order (§7.5 · §4.7): 강화 시트 → 설정 팝업 → 뒤로 ×3 (몇 프레임 사이). ① 팝업 닫힘 · 시트 열림 ② 시트 닫힘 ③ (닫히는 중) 종료 확인 → 모두 닫기
        /// </summary>
        IEnumerator BackOrder()
        {
            var ui = UIManager.Instance;
            var settings = ui.Get<SettingsPopup>();
            var message = ui.Get<MessagePopup>();
            if (settings == null || message == null)
            {
                SkipCheck("IA-back", "back_order: 설정 · 확인 팝업 없음");
                yield break;
            }
            CloseAll();
            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            settings.Open();
            yield return new WaitForSecondsRealtime(0.5f);

            ui.OnBack();
            yield return Frames(3);
            Check("IA-back1", !settings.IsOpen && UIFrame.SheetOpen && _tabs.SelectedIndex == (int)MainTab.Growth,
                $"back_order ①: 설정 {OpenText(settings.IsOpen)} · 시트 {OpenText(UIFrame.SheetOpen)} (기대: 팝업 닫힘, 시트 열림)");
            ui.OnBack();
            yield return Frames(3);
            Check("IA-back2", !UIFrame.SheetOpen && _tabs.SelectedIndex == -1 && !message.IsOpen,
                $"back_order ②: 시트 {OpenText(UIFrame.SheetOpen)} · 종료 확인 {OpenText(message.IsOpen)} (기대: 시트 닫힘, 확인 아직)");
            ui.OnBack();
            yield return Frames(3);
            yield return Shot("back_order", () =>
            {
                bool exit = message.IsOpen && HasText(message.transform, UIManager.ExitTitle);
                Check("IA-back3", exit, $"back_order ③: 종료 확인 {OpenText(message.IsOpen)}{(exit ? $" '{UIManager.ExitTitle}'" : string.Empty)} (기대: 열림)");
            });
            CloseAll();
            UINav.GoToMain(true);
        }

        /// <summary>
        /// sheet_drag (§7.5): 강화 시트 머리 SheetHeader/DragArea에 ExecuteEvents begin · drag · end — 0.2초 동안 300px 아래로.
        /// 놓기 직전에 찍는다 (시트가 손가락을 따라가고 올림도 p를 따른다), 놓으면 닫힌다
        /// </summary>
        IEnumerator SheetDrag()
        {
            CloseAll();
            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            var area = Find(DragAreaPath);
            if (area == null)
            {
                Check("IA-drag", false, "sheet_drag: SheetHeader/DragArea 없음 (경로 고정, CR-04)");
                yield break;
            }
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(area.rect.center));
            Vector2 end = start - new Vector2(0f, DragDistance * Scale);
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                position = start,
                pressPosition = start,
            };
            ExecuteEvents.Execute(area.gameObject, data, ExecuteEvents.beginDragHandler);
            float begin = Time.realtimeSinceStartup;
            float t = 0f;
            while (t < 1f)
            {
                yield return null;
                t = Mathf.Clamp01((Time.realtimeSinceStartup - begin) / DragSeconds);
                var position = Vector2.Lerp(start, end, t);
                data.delta = position - data.position;
                data.position = position;
                ExecuteEvents.Execute(area.gameObject, data, ExecuteEvents.dragHandler);
            }
            yield return Shot("sheet_drag", () =>
            {
                var p = new Probe();
                float progress = UIFrame.SheetProgress;
                p.Expect(_tabs.Sheet != null && _tabs.Sheet.IsDragging, "끄는 중");
                p.Near("p (손가락을 따라감)", progress, 1f - DragDistance / SpecSheet, 0.05f);
                p.Near("올림 − PanOpen·p", _mapper.Pan - _driver.PanOpen * progress, 0f, 2f);
                Report("IA-drag-mid", "sheet_drag", p);
            });
            ExecuteEvents.Execute(area.gameObject, data, ExecuteEvents.endDragHandler);
            bool released = !UIFrame.SheetOpen && _tabs.SelectedIndex == -1;
            yield return new WaitForSecondsRealtime(0.5f);
            var q = new Probe();
            q.Expect(released, "놓은 그 프레임에 목표 상태 MAIN (p < 0.75 또는 1200px/s 이상)");
            q.Expect(!Find(ContentPath).gameObject.activeSelf && UIFrame.SheetProgress <= 0f, "닫혀 멈춤 (ContentArea 꺼짐, p 0)");
            q.Near("올림", _mapper.Pan, 0f, 0.01f);
            Report("IA-drag", "sheet_drag", q);
        }

        /// <summary>menu (§7.5 · §3.6): ≡ 메뉴 팝업 — 칸 여섯(모든 기능 열림) · 3x2 격자 · 창 900x740</summary>
        IEnumerator Menu()
        {
            CloseAll();
            UINav.GoToMain(true);
            var menu = UIManager.Instance.Get<MenuPopup>();
            if (menu == null)
            {
                SkipCheck("IA-menu", "menu: MenuPopup 없음");
                yield break;
            }
            menu.Open();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("menu", () => CheckMenu(menu));
            CloseAll();
        }

        /// <summary>
        /// sleep (§7.5 · §4.9): 강화 시트를 연 채 SleepModeView.SleepNow() — 시트가 먼저 내려가고, 카드는 아래 띠, 화면 가운데 밝기는 잠들기 바로 전 MAIN의 40~65%.
        /// 비교 기준은 같은 전투 · 같은 기믹인 잠들기 몇 초 전 MAIN이다 (main 장면은 50초쯤 앞이라 스테이지 기믹 · 날씨가 다를 수 있다 — main 대비 값은 기록만).
        /// 밝기는 틀 기둥 안만 잰다 (넓은 화면의 어둡게 안 되는 양옆을 빼고 — aspect R2) · 두 번 다 불 밝히기 터짐 뒤 조용한 프레임에서 5번 잰 가운데 값 (RI-1, MeasureQuietLuma).
        /// 찍은 뒤 뒤로(M15)로 깨운다
        /// </summary>
        IEnumerator SleepShot()
        {
            var sleep = FindAnyObjectByType<SleepModeView>(FindObjectsInactive.Include);
            if (sleep == null)
            {
                SkipCheck("IA-sleep", "sleep: SleepModeView 없음");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return new WaitForSecondsRealtime(0.5f);
            float reference = -1f;
            string referenceNote = null;
            yield return MeasureQuietLuma((value, note) =>
            {
                reference = value;
                referenceNote = note;
            });

            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            sleep.SleepNow();
            bool closesFirst = !UIFrame.SheetOpen && !sleep.IsAsleep;
            float limit = Time.realtimeSinceStartup + 3f;
            while (!sleep.IsAsleep && Time.realtimeSinceStartup < limit) yield return null;
            bool settledFirst = sleep.IsAsleep && UIFrame.SheetProgress <= 0f && !Find(ContentPath).gameObject.activeSelf;
            // 덮개가 다 어두워질 때까지 (0.4초)
            yield return new WaitForSecondsRealtime(0.9f);
            float asleep = -1f;
            string asleepNote = null;
            yield return MeasureQuietLuma((value, note) =>
            {
                asleep = value;
                asleepNote = note;
            });
            yield return Shot("sleep", () =>
            {
                var p = new Probe();
                p.Expect(closesFirst, "SleepNow: 시트를 먼저 내린다 (부른 그 프레임 목표 MAIN, 아직 잠들지 않음)");
                p.Expect(settledFirst, "시트가 다 내려간 뒤에 잠든다");
                p.Expect(sleep.IsAsleep, "잠든 상태");
                var card = Find(SleepCardPath);
                if (card == null) p.Expect(false, "상태 카드 SleepMode/Overlay/Frame/Card 없음");
                else
                {
                    var rect = ScreenRect(card);
                    p.Near("카드 위", rect.yMin, CanvasHeight - 236f - 136f, 4f);
                    p.Near("카드 아래", rect.yMax, CanvasHeight - 236f, 4f);
                }
                float ratio = reference > 0f && asleep >= 0f ? asleep / reference : -1f;
                p.Expect(ratio >= 0.40f && ratio <= 0.65f, $"가운데 밝기 {asleep:0.000} / 잠들기 전 MAIN {reference:0.000} = {ratio:0.00} (기대 0.40~0.65)");
                if (_mainLuma > 0f && asleep >= 0f) p.Info($"main 장면 대비 {asleep / _mainLuma:0.00}");
                p.Info($"잰 곳: 틀 기둥 {FrameMinX:0.#}+{FrameWidth:0.#} 안 가로 10~90% · 세로 40~60%");
                p.Info($"잠들기 전 MAIN {referenceNote}");
                p.Info($"잠듦 {asleepNote}");
                Report("IA-sleep", "sleep", p);
            });
            UIManager.Instance.OnBack();
            yield return new WaitForSecondsRealtime(0.6f);
            Check("IA-wake", !sleep.IsAsleep, $"sleep: 뒤로로 깨움 → {(sleep.IsAsleep ? "아직 잠듦" : "깨어남")}");
        }

        // ═════════════════════════ 검사 ═════════════════════════

        /// <summary>main (§7.5): IA-1 틀 · IA-2 영역 · IA-3 전장 · IA-4 링 · IA-5 생성 점 · IA-6 상단 바 · IA-7 빠른 아이콘</summary>
        void CheckMain(string shot)
        {
            Record("INFO", "IA-env", $"{shot}: 화면 {Screen.width}x{Screen.height} · 캔버스 {CanvasWidth:0.#}x{CanvasHeight:0.#} · scale {Scale:0.###} · 줄이기 {UIMotion.Reduced} · 틀 기둥 {FrameMinX:0.#}+{FrameWidth:0.#}");
            ReportMain("IA-1", shot);

            var p = new Probe();
            var stage = ScreenRect(Find(StagePath));
            var field = ScreenRect(_mapper.Rect);
            p.Near("StageArea 위", stage.yMin, SpecTopBar, 1f);
            p.Near("StageArea 아래", stage.yMax, CanvasHeight - SpecTabBar, 1f);
            p.Near("Field 위", field.yMin, SpecTopBar, 1f);
            p.Near("Field 아래", field.yMax, CanvasHeight - SpecTabBar - SpecStrip, 1f);
            Report("IA-2", shot, p);

            p = new Probe();
            var backdrop = Comp<Image>(BackdropPath);
            string sprite = backdrop != null && backdrop.sprite != null ? backdrop.sprite.name : "(없음)";
            p.Expect(sprite == "field_tall", $"바닥 그림 {sprite} (기대 field_tall)");
            p.Near("Squash", _mapper.Squash, 0.7256f, 0.01f);
            // 넓은 화면의 틀 기둥도 캔버스 단위 1080x1920 (aspect AF-2) — 문서 숫자를 그대로 본다
            if (IsFrame169)
            {
                p.Near("Ppu", _mapper.Ppu, 205f, 5f);
                p.Near("수선점 화면 y", ShopScreenY(), 925.5f, 8f);
                if (backdrop != null && backdrop.sprite != null) p.Near("그림 배율", backdrop.rectTransform.rect.height / backdrop.sprite.rect.height, 1f, 0.001f);
            }
            else p.Info($"16:9 아님 — Ppu {_mapper.Ppu:0.0} · 수선점 화면 y {ShopScreenY():0.0} (기록만)");
            Report("IA-3", shot, p);

            p = new Probe();
            float ring = RingRadiusPx(backdrop);
            p.Near("그림 링 / (1.6·Ppu)", ring / (SpecRingWorld * _mapper.Ppu), 1f, 0.02f);
            Report("IA-4", shot, p);

            CheckSpawnClear(shot);
            CheckTopBar(shot);
            CheckRails(shot);

            CheckGuideBar(shot);
            CheckBadgeClusters(shot);
            CheckRailNew(shot);
            CheckWave5Hud(shot);
        }

        /// <summary>MAIN에 멈춰 있다 (IA-1 · main_after_close · battle_later): 선택 −1 · 목표 닫힘 · p 0 · ContentArea 꺼짐 · 올림 0 · 사냥 빛 켜짐</summary>
        void ReportMain(string id, string shot)
        {
            var p = new Probe();
            p.Expect(_tabs != null && _tabs.SelectedIndex == -1, $"SelectedIndex {(_tabs != null ? _tabs.SelectedIndex : -99)} (기대 −1)");
            p.Expect(!UIFrame.SheetOpen, "시트 목표 닫힘");
            p.Near("p", UIFrame.SheetProgress, 0f, 1e-4f);
            var content = Find(ContentPath);
            p.Expect(content != null && !content.gameObject.activeSelf, "ContentArea 꺼짐");
            p.Near("올림", _mapper.Pan, 0f, 0.01f);
            var glow = Find(MainGlowPath);
            p.Expect(glow != null && glow.gameObject.activeInHierarchy && GraphicAlpha(glow) > 0.05f, "사냥 빛(Tab_Main/Glow) 켜짐");
            Report(id, shot, p);
        }

        /// <summary>IA-5: MAIN 생성 점(반지름 2.5, 길 다섯)이 상단 바 · Hud · MetaHud · 빠른 아이콘 사각형 안에 없다 (16:9면 §1.3 자리 ±8도)</summary>
        void CheckSpawnClear(string shot)
        {
            var p = new Probe();
            var blockers = HudRects();
            for (int lane = 0; lane < BattleGeometry.SlotCount; lane++)
            {
                var world = BattleGeometry.FromAngle(BattleGeometry.SlotAngle(lane), BattleGeometry.SpawnRadius);
                var point = RefPoint(_mapper.FieldToCanvasWorld(_mapper.WorldToField(world)));
                if (IsFrame169)
                {
                    p.Near($"길 {lane} x", point.x - FrameMinX, SpecSpawns[lane].x, 8f);
                    p.Near($"길 {lane} y", point.y, SpecSpawns[lane].y, 8f);
                }
                else p.Info($"길 {lane} ({point.x:0.#}, {point.y:0.#})");
                foreach (var (name, rect) in blockers)
                {
                    if (rect.Contains(point)) p.Expect(false, $"길 {lane} 생성 점 ({point.x:0.#}, {point.y:0.#})이 {name} {R(rect)} 안");
                }
            }
            p.Info($"HUD 사각형 {blockers.Count}개");
            Report("IA-5", shot, p);
        }

        /// <summary>IA-6: 상단 바 칸(레벨 · 전투력 · 단추 · 초당 · 별조각 · 우편 · 메뉴)이 서로 겹치지 않고 바 안에 있다 (틀 기둥 폭 1080이면 §3.1 자리 ±2, 기둥 왼쪽부터)</summary>
        void CheckTopBar(string shot)
        {
            var p = new Probe();
            var rects = new Rect[TopBarItems.Length];
            var found = new bool[TopBarItems.Length];
            for (int i = 0; i < TopBarItems.Length; i++)
            {
                var item = Find($"{CoachTargets.TopBar}/{TopBarItems[i]}");
                found[i] = item != null && item.gameObject.activeInHierarchy;
                // 없음 · 바 밖은 어긋날 때만 적는다 (맞춘 기대는 PASS 줄에도 남으므로 자리 줄이 잰 값을 보인다)
                if (!found[i])
                {
                    p.Expect(false, $"{TopBarItems[i]} 없음");
                    continue;
                }
                rects[i] = ScreenRect(item);
                bool inBar = rects[i].yMin >= -0.5f && rects[i].yMax <= SpecTopBar + 0.5f;
                if (!inBar) p.Expect(false, $"{TopBarItems[i]} {R(rects[i])}이 바(0~130) 밖");
                if (IsFrame1080)
                {
                    var spec = SpecTopBarRects[i];
                    var expected = FrameRect(spec.xMin, spec.yMin, spec.xMax, spec.yMax);
                    p.Expect(Close(rects[i], expected, 2f), $"{TopBarItems[i]} {R(rects[i])} (기대 {R(expected)})");
                }
                else if (inBar) p.Info($"{TopBarItems[i]} {R(rects[i])}");
            }
            int pairs = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                for (int j = i + 1; j < rects.Length; j++)
                {
                    if (!found[i] || !found[j]) continue;
                    pairs++;
                    if (Overlaps(rects[i], rects[j])) p.Expect(false, $"{TopBarItems[i]} {R(rects[i])} · {TopBarItems[j]} {R(rects[j])} 겹침");
                }
            }
            p.Info($"칸 짝 {pairs}개 겹침 확인");
            Report("IA-6", shot, p);
        }

        /// <summary>
        /// IA-7: 빠른 아이콘이 §3.3 칸에 있고 이름이 보인다 — 이벤트 L1 · 출석부 L3 · 수선 일지 R3 (칸 104x116, StageArea 기준).
        /// x는 틀 기둥 가장자리부터 (왼쪽 FrameMinX + 12, 오른쪽 FrameMaxX − 116 — 폰은 0 · 1080, AS-6)
        /// </summary>
        void CheckRails(string shot)
        {
            if (!Game.IsMetaActive)
            {
                SkipCheck("IA-7", $"{shot}: 메타 꺼짐 (빠른 아이콘 없음)");
                return;
            }
            var p = new Probe();
            var stage = ScreenRect(Find(StagePath));
            var rail = Comp<QuickRailView>(RailLeftPath);
            bool shortStage = rail != null && rail.IsShortStage;
            float top1 = stage.yMin + 306f;
            float bottom3 = stage.yMax - 288f;
            if (!shortStage) ExpectRailCell(p, CoachTargets.EventButton, "이벤트", FrameRect(12f, top1, 116f, top1 + 116f));
            else p.Info("짧은 화면 — 이벤트는 아래 묶음 (자리는 보지 않는다)");
            ExpectRailCell(p, CoachTargets.AttendanceButton, "출석부", shortStage ? (Rect?)null : FrameRect(12f, bottom3 - 116f, 116f, bottom3));
            ExpectRailCell(p, CoachTargets.JournalButton, "수선 일지", Rect.MinMaxRect(FrameMaxX - 116f, bottom3 - 116f, FrameMaxX - 12f, bottom3));
            Report("IA-7", shot, p);
        }

        void ExpectRailCell(Probe p, string path, string label, Rect? expected)
        {
            var cell = Find(path);
            string name = path.Substring(path.LastIndexOf('/') + 1);
            if (cell == null || !cell.gameObject.activeInHierarchy)
            {
                p.Expect(false, $"{name} 안 보임 (모든 기능 열림 · 튜토리얼 끝)");
                return;
            }
            var rect = ScreenRect(cell);
            if (expected.HasValue) p.Expect(Close(rect, expected.Value, 2f), $"{name} {R(rect)} (기대 {R(expected.Value)})");
            else p.Info($"{name} {R(rect)}");
            var text = cell.Find(RailLabelPath) is RectTransform labelRect ? labelRect.GetComponent<TMP_Text>() : null;
            p.Expect(text != null && text.text == label && text.gameObject.activeInHierarchy, $"{name} 이름 '{(text != null ? text.text : "(없음)")}' (기대 '{label}')");
        }

        /// <summary>
        /// 탭 시트 (§7.5 tab_*): IA-8 ContentArea 970~1750 · 머리 914~970 · 올림 = PanOpen (16:9면 360 ±1), 기기 픽셀 정수 (CR-09) ·
        /// IA-9 SheetFade 알파 (§1.6) · IA-10 선택 막대가 탭 가운데 · IA-11 FX 범위 (B8 뒤에만 — 그 전에는 SKIP + 지금 값)
        /// </summary>
        void CheckSheetTab(string shot, MainTab tab)
        {
            var p = new Probe();
            p.Expect(_tabs.SelectedIndex == (int)tab && UIFrame.SheetOpen && UIFrame.SheetTab == (int)tab, $"선택 {_tabs.SelectedIndex} · 시트 탭 {UIFrame.SheetTab} (기대 {(int)tab})");
            p.Near("p", UIFrame.SheetProgress, 1f, 1e-4f);
            p.Expect(UIFrame.SheetSettled, "멈춤");
            float bottom = CanvasHeight - SpecTabBar;
            var content = ScreenRect(Find(ContentPath));
            var header = ScreenRect(Find(HeaderPath));
            p.Near("ContentArea 위", content.yMin, bottom - SpecContent, 1f);
            p.Near("ContentArea 아래", content.yMax, bottom, 1f);
            p.Near("머리 위", header.yMin, bottom - SpecSheet, 1f);
            p.Near("머리 아래", header.yMax, bottom - SpecContent, 1f);
            p.Near("올림 − PanOpen", _mapper.Pan - _driver.PanOpen, 0f, 0.01f);
            if (IsFrame169) p.Near("올림 (16:9)", _mapper.Pan, 360f, 1f);
            float device = _mapper.Pan * Scale;
            p.Near("올림 기기 px − 정수", device - Mathf.Round(device), 0f, 0.01f);
            Report("IA-8", shot, p);

            p = new Probe();
            ExpectFade(p, MetaHudPath + "/RecommendedPower", 0f, Game.IsMetaActive);
            ExpectFade(p, HudPath + "/SynergyRow", 0f, true);
            ExpectFade(p, RailLeftPath, 0f, Game.IsMetaActive);
            ExpectFade(p, RailRightPath, 0f, Game.IsMetaActive);
            ExpectFade(p, CoachTargets.IdleChestWidget, 0.5f, Game.IsMetaActive);
            // 편성이 비었을 때만 켜진다 · 보스 도전 버튼(M13)의 0.5는 WP-5가 붙인다
            ExpectFade(p, HudPath + "/EmptyHint", 0f, false);
            var boss = FindDeep(Find(BattlePath), "BossChallengeButton");
            if (boss != null && boss.GetComponent<SheetFade>() != null) ExpectFade(p, PathOf(boss), 0.5f, false);
            else p.Info("BossChallengeButton SheetFade 없음 (WP-5 M13)");
            Report("IA-9", shot, p);

            p = new Probe();
            var indicator = Find(IndicatorPath);
            var button = _tabs.GetTabButton(tab);
            if (indicator == null || button == null) p.Expect(false, "선택 막대(TabBar/Indicator) · 탭 버튼");
            else
            {
                float x = ScreenRect(indicator).center.x;
                p.Near("막대 가운데 − 탭 가운데", x - ScreenRect((RectTransform)button.transform).center.x, 0f, 2f);
                if (IsFrame1080) p.Near("막대 가운데 x (기둥 왼쪽부터)", x - FrameMinX, SpecTabCenters[(int)tab], 2f);
                p.Expect(GraphicAlpha(indicator) >= 0.9f, $"막대 알파 {GraphicAlpha(indicator):0.00} (기대 ≥ 0.9 — 시트에서 보인다)");
            }
            Report("IA-10", shot, p);

            CheckFxExtents(shot);
        }

        /// <summary>
        /// IA-11 (§6.5 CR-01, Track B8 뒤): BattleFx.FieldTop = mapper.VisibleTop ±1, SafeTop이 화면 y ≥ 250. B8(BattleFx.FieldBottom)이 아직이면 SKIP과 지금 값만
        /// </summary>
        void CheckFxExtents(string shot)
        {
            var fx = FindAnyObjectByType<BattleFx>();
            if (fx == null)
            {
                SkipCheck("IA-11", $"{shot}: BattleFx 없음");
                return;
            }
            float safeY = RefPoint(_mapper.FieldToCanvasWorld(new Vector2(0f, fx.SafeTop))).y;
            string now = $"FieldTop {fx.FieldTop:0.#} · VisibleTop {_mapper.VisibleTop:0.#} · SafeTop 화면 y {safeY:0.#}";
            bool merged = typeof(BattleFx).GetProperty("FieldBottom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
            if (!merged)
            {
                SkipCheck("IA-11", $"{shot}: B8 전 (BattleFx.FieldBottom 없음 — FX 병합 뒤 Track B8) · {now}");
                return;
            }
            var p = new Probe();
            p.Near("FieldTop − VisibleTop", fx.FieldTop - _mapper.VisibleTop, 0f, 1f);
            p.Expect(safeY >= 249.5f, $"SafeTop 화면 y {safeY:0.0} (기대 ≥ 250)");
            Report("IA-11", shot, p);
        }

        void ExpectFade(Probe p, string path, float alpha, bool required)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            var target = Find(path);
            var fade = target != null ? target.GetComponent<SheetFade>() : null;
            if (fade == null)
            {
                if (required) p.Expect(false, $"{name}: SheetFade 없음");
                return;
            }
            if (!fade.isActiveAndEnabled)
            {
                p.Info($"{name} 꺼짐");
                return;
            }
            p.Near(name, fade.Alpha, alpha, 0.01f);
        }

        /// <summary>
        /// menu (모든 기능 열림): 기대 칸(ExpectedMenuTiles — 처음 여섯 + 묶이고 콘텐츠가 켜진 SPEC4 칸)이 모두 보이고, 기대 밖 칸은 안 보이며, 보이는 칸이
        /// MenuTileOrder 순서 그대로 (모르는 칸 이름은 FAIL — 순서를 여기에 더한다) · 3열 x ⌈칸/3⌉줄 · 창 900 x (300 + 220·줄) — 여섯 칸이면 2줄 740,
        /// SPEC4 칸이 다 켜지면 4줄 1180 (로드맵 델타 36) · 창이 화면 안. V2 r4: 예전에는 보이는 칸만으로 기대를 만들어 SPEC4 칸이 사라져도 PASS였다
        /// </summary>
        void CheckMenu(MenuPopup menu)
        {
            var p = new Probe();
            var window = menu.Window;
            p.Expect(menu.IsOpen, "메뉴 팝업 열림");
            p.Near("창 폭", window.rect.width, 900f, 1f);
            var grid = window.Find("Body/Grid");
            if (grid == null) p.Expect(false, "Window/Body/Grid 없음");
            else
            {
                var columns = new List<float>();
                var rows = new List<float>();
                var shown = new List<string>();
                for (int i = 0; i < grid.childCount; i++)
                {
                    if (grid.GetChild(i) is not RectTransform tile || !tile.gameObject.activeSelf || !tile.name.EndsWith("Tile", StringComparison.Ordinal)) continue;
                    shown.Add(tile.name);
                    var center = ScreenRect(tile).center;
                    AddDistinct(columns, center.x);
                    AddDistinct(rows, center.y);
                }
                int count = shown.Count;
                var order = new List<string>(MenuTileOrder.Split(new[] { " · " }, StringSplitOptions.None));
                var wanted = ExpectedMenuTiles(menu, p, out string why);
                foreach (var name in wanted)
                {
                    if (!shown.Contains(name)) p.Expect(false, $"{name} 없음 (기대 칸 — {why})");
                }
                var inOrder = new List<string>();
                foreach (var name in order)
                {
                    if (shown.Contains(name)) inOrder.Add(name);
                }
                foreach (var name in shown)
                {
                    if (!order.Contains(name)) p.Expect(false, $"모르는 칸 {name} (AutoShot.MenuTileOrder에 순서를 더한다)");
                    else if (!wanted.Contains(name)) p.Expect(false, $"기대 밖 칸 {name}이 보인다 ({why})");
                }
                string actual = string.Join(" · ", shown);
                p.Expect(actual == string.Join(" · ", inOrder), $"칸 순서 {actual} (기대 {string.Join(" · ", inOrder)})");
                p.Info($"기대 칸 {wanted.Count}개 {string.Join(" · ", wanted)}");
                int rowCount = Mathf.Max(1, Mathf.CeilToInt(count / 3f));
                p.Expect(columns.Count == Mathf.Min(3, count) && rows.Count == rowCount, $"격자 {columns.Count}열 x {rows.Count}줄 · 칸 {count}개 (기대 {Mathf.Min(3, count)}x{rowCount})");
                p.Near("창 높이", window.rect.height, 300f + 220f * rowCount, 1f);
                var box = ScreenRect(window);
                p.Expect(box.yMin >= -0.5f && box.yMax <= CanvasHeight + 0.5f, $"창 {R(box)}이 화면(0~{CanvasHeight:0}) 안");
                if (count > 6) p.Info($"SPEC4 칸 {count - 6}개");
            }
            Report("IA-menu", "menu", p);
        }

        /// <summary>
        /// menu 기대 칸 (MenuTileOrder 순서): 처음 여섯은 늘. SPEC4 칸은 MenuPopup 필드가 묶였고 콘텐츠가 켜졌을 때 — EventHubTile = 이벤트 모음 열림
        /// (Events.IsUnlocked), SeasonTile = 수첩 칸 &gt; 0 (stretch), CodexTile · AchievementsTile = 그 기능 열림, CouponTile = 쿠폰 시스템 있음 · 튜토리얼 끝.
        /// SPEC4 칸이 하나라도 묶인 장면에서 안 묶인 필드는 FAIL (빌더 · 묶기가 칸을 잃음). why = 칸마다 이유 (검사 줄에 남는다)
        /// </summary>
        static List<string> ExpectedMenuTiles(MenuPopup menu, Probe p, out string why)
        {
            var separator = new[] { " · " };
            var on = new HashSet<string>(MenuTileBase.Split(separator, StringSplitOptions.None));
            var reasons = new StringBuilder("처음 여섯");
            bool spec4Scene = false;
            foreach (var (_, field) in MenuSpec4Tiles)
            {
                if (Field<Button>(menu, field) != null) spec4Scene = true;
            }
            bool tutorial = Game.Tutorial != null && Game.Tutorial.IsActive;
            var db = Game.MetaDb;
            int tiers = db != null && db.season != null && db.season.tiers != null ? db.season.tiers.Count : 0;
            foreach (var (tile, field) in MenuSpec4Tiles)
            {
                if (Field<Button>(menu, field) == null)
                {
                    if (spec4Scene) p.Expect(false, $"MenuPopup.{field} 비어 있음 (SPEC4 칸이 묶인 장면인데 {tile}만 빠졌다)");
                    continue;
                }
                bool content;
                string note;
                switch (tile)
                {
                    case "EventHubTile":
                        content = Game.Events != null && Game.Events.IsUnlocked;
                        note = content ? "이벤트 모음 열림" : "이벤트 모음 닫힘";
                        break;
                    case "SeasonTile":
                        content = tiers > 0;
                        note = tiers > 0 ? $"수첩 칸 {tiers}" : "수첩 칸 0 (stretch off)";
                        break;
                    case "CodexTile":
                        content = Game.Features != null && Game.Features.IsUnlocked(FeatureId.Codex);
                        note = content ? "도감 열림" : "도감 잠김";
                        break;
                    case "AchievementsTile":
                        content = Game.Features != null && Game.Features.IsUnlocked(FeatureId.Achievements);
                        note = content ? "수선점 기록 열림" : "수선점 기록 잠김";
                        break;
                    default:
                        content = Game.Coupons != null && !tutorial;
                        note = $"쿠폰 {(Game.Coupons != null ? "있음" : "없음")}{(tutorial ? " · 튜토리얼 중" : string.Empty)}";
                        break;
                }
                reasons.Append(" · ").Append(tile).Append(' ').Append(note);
                if (content) on.Add(tile);
            }
            if (!spec4Scene) reasons.Append(" · SPEC4 칸 없음 (옛 장면)");
            why = reasons.ToString();
            var list = new List<string>();
            foreach (var name in MenuTileOrder.Split(separator, StringSplitOptions.None))
            {
                if (on.Contains(name)) list.Add(name);
            }
            return list;
        }

        void CheckLater()
        {
            ReportMain("IA-later", "battle_later");
            CheckBadgeClusters("battle_later");
        }

        /// <summary>tall: 그림 덮개 ≤ 1.0 (그림을 키우지 않는다) — 링 비율은 IA-4가 본다</summary>
        void CheckTallCover(string shot)
        {
            var p = new Probe();
            var backdrop = Comp<Image>(BackdropPath);
            if (backdrop == null || backdrop.sprite == null) p.Expect(false, "바닥 그림 없음");
            else
            {
                float cover = backdrop.rectTransform.rect.height / backdrop.sprite.rect.height;
                p.Expect(cover <= 1.001f, $"그림 덮개 {cover:0.000} (기대 ≤ 1.0)");
                p.Info($"그림 {backdrop.sprite.name} {backdrop.rectTransform.rect.width:0}x{backdrop.rectTransform.rect.height:0}");
            }
            Report("IA-tall", shot, p);
        }

        /// <summary>
        /// tall: 올림 = §1.3 규칙 ±4 — H = StageArea 높이, k = Ppu/205, sheetTop = H − 836, mainShop = MAIN에서 잰 수선점 깊이,
        /// shop = sheetTop ≥ 760k ? sheetTop − 348k : 146k + 0.35·sheetTop, PanOpen = round_px(max(0, mainShop − shop))
        /// </summary>
        void CheckTallPan(string shot, float mainShop)
        {
            var p = new Probe();
            if (float.IsNaN(mainShop))
            {
                p.Expect(false, "MAIN 수선점 깊이를 못 쟀다");
                Report("IA-tall-pan", shot, p);
                return;
            }
            float height = ScreenRect(Find(StagePath)).height;
            float k = _mapper.Ppu / 205f;
            float sheetTop = height - SpecSheet;
            float shop = sheetTop >= 760f * k ? sheetTop - 348f * k : 146f * k + 0.35f * sheetTop;
            float rule = Mathf.Round(Mathf.Max(0f, mainShop - shop) * Scale) / Scale;
            p.Near("올림 − 규칙", _mapper.Pan - rule, 0f, 4f);
            p.Info($"H {height:0.#} · mainShop {mainShop:0.#} · k {k:0.###} · 규칙 {rule:0.##} · 올림 {_mapper.Pan:0.##}");
            Report("IA-tall-pan", shot, p);
        }

        // ═════════════════════════ 기록 ═════════════════════════

        /// <summary>
        /// 한 검사의 기대 · 기록 묶음 (못 맞춘 기대가 하나라도 있으면 FAIL). 맞춘 기대(Expect의 글 — 잰 값 · 기대가 들어 있다)도 모아
        /// PASS 줄에 기록 뒤로 붙인다 (Wave 3 r1: 값 없는 PASS 줄은 감사할 수 없었다). FAIL 줄은 어긋난 것 | 기록
        /// </summary>
        sealed class Probe
        {
            readonly StringBuilder _bad = new();
            readonly StringBuilder _info = new();
            readonly StringBuilder _met = new();

            public bool Ok => _bad.Length == 0;

            /// <summary>기대 하나 — what은 맞든 어긋나든 읽히게 잰 값과 기대를 함께 쓴다 ('X 없음'처럼 어긋남만 뜻하는 글은 Expect(false, …)로만)</summary>
            public void Expect(bool ok, string what) => Add(ok ? _met : _bad, what);

            public void Info(string text) => Add(_info, text);

            public void Near(string what, float actual, float expected, float tolerance)
            {
                if (!float.IsNaN(actual) && Mathf.Abs(actual - expected) <= tolerance) Add(_info, $"{what} {actual:0.###}");
                else Add(_bad, $"{what} {actual:0.###} (기대 {expected:0.###} ±{tolerance:0.###})");
            }

            public override string ToString()
            {
                if (!Ok) return _info.Length > 0 ? $"{_bad} | {_info}" : _bad.ToString();
                if (_met.Length == 0) return _info.ToString();
                return _info.Length > 0 ? $"{_info}; {_met}" : _met.ToString();
            }

            static void Add(StringBuilder text, string part)
            {
                if (text.Length > 0) text.Append("; ");
                text.Append(part);
            }
        }

        void Report(string id, string shot, Probe probe) => Check(id, probe.Ok, $"{shot}: {probe}");

        void Check(string id, bool ok, string detail) => Record(ok ? "PASS" : "FAIL", id, detail);

        void SkipCheck(string id, string reason) => Record("SKIP", id, reason);

        /// <summary>"[Verify] PASS|FAIL|SKIP|INFO 아이디 내용" 한 줄 — 로그(FAIL은 경고) + checks.txt. INFO는 세지 않는다</summary>
        void Record(string verdict, string id, string detail)
        {
            if (verdict == "PASS") _pass++;
            else if (verdict == "FAIL") _fail++;
            else if (verdict == "SKIP") _skip++;
            string line = $"[Verify] {verdict} {id} {detail}";
            if (verdict == "FAIL") Debug.LogWarning(line);
            else Debug.Log(line);
            try
            {
                File.AppendAllText(ChecksPath, line + "\n");
            }
            catch (IOException)
            {
            }
        }

        void Finish()
        {
            // 예전 판(full · tall)은 요약 이름 그대로 (verify2.ps1 · 사람이 'IA 요약'을 찾는다)
            string label = _migrate ? "MIG" : _spec4 != null ? $"SPEC4 {_spec4}" : "IA";
            string summary = $"[Verify] {label} 요약 PASS {_pass} · FAIL {_fail} · SKIP {_skip}";
            Debug.Log(summary);
            try
            {
                File.AppendAllText(ChecksPath, summary + "\n");
            }
            catch (IOException)
            {
            }
            RestoreAutoReturn();
            Application.logMessageReceived -= OnLog;
            Application.Quit();
        }

        // ═════════════════════════ 자동 복귀 설정 (CR-04) ═════════════════════════

        void TurnOffAutoReturn()
        {
            _autoReturnWas = GameSettings.SheetAutoReturn;
            if (!_autoReturnWas) return;
            GameSettings.SheetAutoReturn = false;
            _autoReturnOff = true;
        }

        void RestoreAutoReturn()
        {
            if (!_autoReturnOff) return;
            _autoReturnOff = false;
            GameSettings.SheetAutoReturn = _autoReturnWas;
        }

        /// <summary>도중에 꺼져도(창 닫기) 기기 설정을 되돌린다</summary>
        void OnApplicationQuit() => RestoreAutoReturn();

        // ═════════════════════════ 도우미 ═════════════════════════

        void Bind()
        {
            _root = UIManager.Instance != null ? (RectTransform)UIManager.Instance.transform : null;
            var canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            _canvas = canvas != null ? canvas.rootCanvas : null;
            _tabs = FindAnyObjectByType<MainTabController>();
            _mapper = Comp<BattleFieldMapper>(FieldPath);
            _driver = Comp<BattleFrameDriver>(BattlePath);
        }

        static void UnlockAll()
        {
            if (!Game.IsMetaActive) return;
            Game.Tutorial.Skip();
            foreach (FeatureId feature in Enum.GetValues(typeof(FeatureId)))
            {
                if (!Game.Features.IsUnlocked(feature)) Game.Features.ForceUnlock(feature, false);
            }
        }

        /// <summary>모든 팝업을 연출 없이 바로 닫는다 (UIManager.CloseAllImmediate — 시트는 건드리지 않는다, §5.5)</summary>
        static void CloseAll()
        {
            if (UIManager.Instance != null) UIManager.Instance.CloseAllImmediate();
        }

        /// <summary>CloseGamePopups: 창 없이 이만큼 지나면 끝 · 기다린 뒤 더 닫는 상한 (초 — 접속 묶음은 창이 닫히고 UIMotion.StartupGap 0.18초 뒤 다음 창)</summary>
        const float PopupDrainQuiet = 1f;
        const float PopupDrainMax = 8f;

        /// <summary>
        /// seconds 동안 기다리며 게임이 스스로 띄운 창(접속 묶음 출석부 · 공지 · 이벤트 시작 카드, 스테이지 사이 기능 열림 카드(FeatureUnlockCard) 등)을 뜨는 대로 닫고 이름을 closed에 모은다.
        /// 기다린 뒤에도 창 없이 PopupDrainQuiet초가 지날 때까지 (늦어도 PopupDrainMax초 더) — 찍을 화면을 창이 가리지 않게 (V2 r1 fix: 60_battle_later · 02_mig_growth)
        /// </summary>
        static IEnumerator CloseGamePopups(List<string> closed, float seconds = 0f)
        {
            float start = Time.realtimeSinceStartup;
            float quietSince = start;
            while (true)
            {
                float now = Time.realtimeSinceStartup;
                var top = UIManager.Instance != null ? UIManager.Instance.TopPopup : null;
                if (top != null)
                {
                    string name = top.GetType().Name;
                    if (closed != null && !closed.Contains(name)) closed.Add(name);
                    CloseAll();
                    quietSince = now;
                }
                if (now - start >= seconds && (now - quietSince >= PopupDrainQuiet || now - start >= seconds + PopupDrainMax)) yield break;
                yield return null;
            }
        }

        /// <summary>맨 위 열린 창의 형식 이름 (없으면 null)</summary>
        static string TopPopupName()
        {
            var top = UIManager.Instance != null ? UIManager.Instance.TopPopup : null;
            return top != null ? top.GetType().Name : null;
        }

        /// <summary>
        /// 단계 하나를 돌린다 — 그 안(안에서 기다리는 코루틴 포함)에서 예외가 나면 FAIL 한 줄 + 예외 로그를 남기고 그 단계만 끝낸다
        /// (finally를 돌리도록 Dispose) — 다음 단계는 그대로 간다
        /// </summary>
        IEnumerator Safe(string step, IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Check("IA-run", false, $"{step}: 예외 {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e);
                    (routine as IDisposable)?.Dispose();
                    yield break;
                }
                yield return current is IEnumerator nested ? Safe(step, nested) : current;
            }
        }

        /// <summary>찍기: 그 프레임 끝에 atCapture(검사)를 부르고 같은 프레임을 저장한다 (검사 예외는 FAIL 한 줄로)</summary>
        IEnumerator Shot(string name, Action atCapture = null)
        {
            yield return new WaitForEndOfFrame();
            if (atCapture != null)
            {
                try
                {
                    atCapture();
                }
                catch (Exception e)
                {
                    Check("IA-run", false, $"{name}: 검사 중 예외 {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e);
                }
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"{++_index:00}_{name}.png"));
            yield return null;
        }

        /// <summary>그 프레임 틀 기둥(PlayFrame.Column — 폰은 화면 전체) 가운데 띠의 밝기를 잰다 (MidLuma — 저장하지 않는다)</summary>
        static IEnumerator MeasureLuma(Action<float> done)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = null;
            float luma = -1f;
            try
            {
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                luma = MidLuma(frame, PlayFrame.Column);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (frame != null) Destroy(frame);
            }
            done(luma);
        }

        /// <summary>
        /// 틀 기둥 가운데 띠(세로 40~60% · 기둥 가로 10~90%, 잠자기 위아래 가림 밖)의 평균 밝기 0~1 — Rec.709 luma를 감마 값 그대로.
        /// column = 화면 비율 기둥 (PlayFrame.Column, 폰 (0, 1) = 예전 화면 가로 10~90%와 같은 픽셀). 넓은 화면의 양옆(어둡게 안 됨)은 넣지 않는다 (aspect R2).
        /// 프로젝트가 Gamma 색 공간이라 UI 덮개는 감마 값에서 섞인다 (덮개 0.5 → 그 아래 밝기의 약 50% + 덮개 색)
        /// </summary>
        static float MidLuma(Texture2D frame, Vector2 column)
        {
            var pixels = frame.GetPixels32();
            int width = frame.width;
            int height = frame.height;
            float columnMin = Mathf.Clamp01(column.x) * width;
            float columnWidth = Mathf.Max(0f, Mathf.Clamp01(column.y) - Mathf.Clamp01(column.x)) * width;
            if (columnWidth < 1f)
            {
                columnMin = 0f;
                columnWidth = width;
            }
            int x0 = Mathf.Clamp((int)(columnMin + columnWidth * 0.1f), 0, width);
            int x1 = Mathf.Clamp((int)(columnMin + columnWidth * 0.9f), x0, width);
            int y0 = (int)(height * 0.4f), y1 = (int)(height * 0.6f);
            double sum = 0d;
            int count = 0;
            for (int y = y0; y < y1; y += 2)
            {
                for (int x = x0; x < x1; x += 2)
                {
                    var c = pixels[y * width + x];
                    sum += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
                    count++;
                }
            }
            return count > 0 ? (float)(sum / count / 255d) : -1f;
        }

        /// <summary>
        /// IA-sleep 밝기 (RI-1): 불 밝히기 터짐(BattleFx.SinceLamp)이 화면을 밝히는 프레임을 피한다 — SinceLamp ≥ 2.5초가 될 때까지 (늦어도 6초) 기다린 뒤
        /// 0.15초 사이로 5번 재어 가운데 값. done(가운데 값 · 못 재면 −1, 잰 조건 한 줄 — Info)
        /// </summary>
        static IEnumerator MeasureQuietLuma(Action<float, string> done)
        {
            var fx = FindAnyObjectByType<BattleFx>();
            float start = Time.realtimeSinceStartup;
            while (fx != null && fx.SinceLamp < QuietLampSeconds && Time.realtimeSinceStartup - start < QuietLampWait) yield return null;
            float waited = Time.realtimeSinceStartup - start;
            string since = fx != null ? $"SinceLamp {fx.SinceLamp:0.0}초" : "SinceLamp ? (BattleFx 없음)";
            var samples = new List<float>(QuietSamples);
            for (int i = 0; i < QuietSamples; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(QuietSampleGap);
                float value = -1f;
                yield return MeasureLuma(v => value = v);
                if (value >= 0f) samples.Add(value);
            }
            float median = MedianOf(samples);
            var text = new StringBuilder();
            foreach (var sample in samples) text.Append(text.Length > 0 ? " " : string.Empty).Append(sample.ToString("0.000"));
            string capped = waited >= QuietLampWait ? $" — 상한 {QuietLampWait:0}초" : string.Empty;
            done(median, $"{since} (기다림 {waited:0.0}초{capped}) · {samples.Count}번 [{text}] → 가운데 {median:0.000}");
        }

        /// <summary>가운데 값 (짝수면 가운데 둘의 평균, 비면 −1)</summary>
        static float MedianOf(List<float> values)
        {
            if (values == null || values.Count == 0) return -1f;
            var sorted = new List<float>(values);
            sorted.Sort();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }

        /// <summary>
        /// gacha_result (RI-4): 결과 창의 _buttonsVisible(공개가 끝나면 Finish가 켠다)이 켜질 때까지, 늦어도 limit초. done(Pull 뒤 걸린 초, 못 보면 −1).
        /// 결과 창 · 필드가 없으면 예전처럼 4.5초 기다린다. Pull은 Show를 바로 불러 값을 끈 뒤 돌아오므로 첫 프레임의 켜짐은 이번 공개의 끝이다
        /// </summary>
        static IEnumerator WaitGachaButtons(float limit, Action<float> done)
        {
            float start = Time.realtimeSinceStartup;
            var result = UIManager.Instance != null ? UIManager.Instance.Get<GachaResultPopup>() : null;
            var field = typeof(GachaResultPopup).GetField("_buttonsVisible", BindingFlags.Instance | BindingFlags.NonPublic);
            if (result == null || field == null)
            {
                yield return new WaitForSecondsRealtime(GachaResultFallback);
                done(-1f);
                yield break;
            }
            while (Time.realtimeSinceStartup - start < limit)
            {
                if (result.IsOpen && field.GetValue(result) is bool shown && shown)
                {
                    done(Time.realtimeSinceStartup - start);
                    yield break;
                }
                yield return null;
            }
            done(-1f);
        }

        /// <summary>시트가 멈출 때까지 (최소 minSeconds, 늦어도 maxSeconds — 첫 두 프레임 시계 제한 때문에 느린 프레임이면 조금 더 걸린다)</summary>
        static IEnumerator WaitSettled(float minSeconds = 0.4f, float maxSeconds = 1.5f)
        {
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - start;
                if (elapsed >= maxSeconds || (elapsed >= minSeconds && UIFrame.SheetSettled)) yield break;
                yield return null;
            }
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>코치가 전투 영역 안 대상 중 다 열린 시트 위 끝(아래에서 170 + 836)보다 아래 것을 가리키는지 (시트를 열면 가려질 대상 — 덱 · 램프)</summary>
        bool CoachOnLowerTarget(CoachMarkView coach)
        {
            if (coach == null || !coach.IsActive || coach.Suppressed) return false;
            var target = coach.Target;
            var stage = Find(StagePath);
            if (target == null || stage == null || !target.IsChildOf(stage) || !target.gameObject.activeInHierarchy) return false;
            return ScreenRect(target).center.y > CanvasHeight - SpecTabBar - SpecSheet;
        }

        static FingerPointerView Finger(CoachMarkView coach)
        {
            var finger = coach.GetComponentInChildren<FingerPointerView>(true);
            return finger != null ? finger : FindAnyObjectByType<FingerPointerView>(FindObjectsInactive.Include);
        }

        static string StepLabel()
        {
            var tutorial = Game.Tutorial;
            if (tutorial == null || !tutorial.IsActive) return "(튜토리얼 끝)";
            var step = tutorial.Current;
            return $"{tutorial.StepIndex} {(step != null ? step.id : "?")}";
        }

        /// <summary>
        /// 생성 점을 가리면 안 되는 HUD 사각형: 상단 바 전체 + Hud · MetaHud의 켜진 자식. 전투 화면을 거의 다 덮는 묶음(빠른 아이콘 줄 · 쟁반 막 등 Stretch)은
        /// 그 안의 켜진 칸을 본다 (2단계까지)
        /// </summary>
        List<(string name, Rect rect)> HudRects()
        {
            var list = new List<(string name, Rect rect)>();
            var bar = Find(CoachTargets.TopBar);
            if (bar != null) list.Add(("TopBar", ScreenRect(bar)));
            var battle = Find(BattlePath);
            if (battle == null) return list;
            var area = ScreenRect(battle);
            AddHudChildren(Find(HudPath), area, list, 0);
            AddHudChildren(Find(MetaHudPath), area, list, 0);
            return list;
        }

        void AddHudChildren(Transform parent, Rect area, List<(string name, Rect rect)> list, int depth)
        {
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i) is not RectTransform child || !child.gameObject.activeInHierarchy) continue;
                var rect = ScreenRect(child);
                if (rect.width < 1f || rect.height < 1f) continue;
                bool container = rect.width >= area.width * 0.9f && rect.height >= area.height * 0.9f;
                if (!container) list.Add((child.name, rect));
                else if (depth < 2) AddHudChildren(child, area, list, depth + 1);
            }
        }

        /// <summary>바닥 그림 링 가로 반지름 (캔버스 px): field_tall이면 §1.4 값 0.30370 × 그림 폭, 아니면 전장에 넣은 값(_backdropRingRadius)</summary>
        float RingRadiusPx(Image backdrop)
        {
            if (backdrop == null || backdrop.sprite == null) return float.NaN;
            float width = backdrop.rectTransform.rect.width;
            if (backdrop.sprite.name == "field_tall") return SpecTallRingRadiusX * width;
            var field = typeof(BattleFieldMapper).GetField("_backdropRingRadius", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && field.GetValue(_mapper) is Vector2 radius ? radius.x * width : float.NaN;
        }

        /// <summary>MAIN에서 수선점(전장 원점)의 화면 y (기준 px, 위에서)</summary>
        float ShopScreenY() => RefPoint(_mapper.FieldToCanvasWorld(_mapper.Origin)).y;

        RectTransform Find(string path) => _root != null && !string.IsNullOrEmpty(path) ? _root.Find(path) as RectTransform : null;

        T Comp<T>(string path) where T : Component
        {
            var target = Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        static RectTransform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child as RectTransform;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>캔버스 기준 경로 (진단 글)</summary>
        string PathOf(Transform target)
        {
            if (target == null) return "(없음)";
            var path = new StringBuilder(target.name);
            for (var t = target.parent; t != null && t != _root; t = t.parent) path.Insert(0, t.name + "/");
            return path.ToString();
        }

        float CanvasWidth => _root != null ? _root.rect.width : 0f;

        /// <summary>
        /// 세로 놀이 틀(PlayFrame, aspect AF-2) 기둥의 왼쪽 x (기준 px, ScreenRect와 같은 좌표 — 왼쪽부터). PlayFrame.ColumnScreenRect를 캔버스 단위로 옮긴 값
        /// (= Column.x × CanvasWidth): 폰(9:16 이하) = 0, 넓은 화면 = 옆 폭 (16:9 창 1166.2). 기둥 안 배치는 폰과 똑같다 — 폰의 x는 FrameMinX + x로 본다 (AS-6)
        /// </summary>
        float FrameMinX => PlayFrame.Column.x * CanvasWidth;

        /// <summary>기둥 폭 (기준 px): 폰 = CanvasWidth (1080 — 길쭉한 폰도 1080), 넓은 화면 = 1080 ±1</summary>
        float FrameWidth => PlayFrame.Fraction * CanvasWidth;

        /// <summary>기둥 오른쪽 x (기준 px) · 가운데 x</summary>
        float FrameMaxX => FrameMinX + FrameWidth;

        float FrameCenterX => FrameMinX + FrameWidth * 0.5f;

        /// <summary>기둥 폭이 기준 1080 (±2) — 폭 1080의 고정 숫자를 기둥 기준(FrameMinX + x)으로 볼 수 있다. 폰에서는 IsWidth1080과 같다</summary>
        bool IsFrame1080 => Mathf.Abs(FrameWidth - 1080f) < 2f;

        /// <summary>틀 기둥이 기준 1080x1920 — 9:16 폰 (= Is169) 또는 넓은 화면의 기둥 (캔버스 높이 1920). 길쭉한 폰은 아니다</summary>
        bool IsFrame169 => IsFrame1080 && Mathf.Abs(CanvasHeight - 1920f) < 2f;

        /// <summary>폰 기준 사각형 (기둥 왼쪽부터 x, 화면 위에서 y) → 기준 px 사각형</summary>
        Rect FrameRect(float xMin, float yMin, float xMax, float yMax) => Rect.MinMaxRect(FrameMinX + xMin, yMin, FrameMinX + xMax, yMax);

        float CanvasHeight => _root != null ? _root.rect.height : 0f;

        float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

        /// <summary>캔버스가 기준 1080x1920 (16:9) — 문서의 고정 숫자를 그대로 볼 수 있다</summary>
        bool Is169 => Mathf.Abs(CanvasWidth - 1080f) < 2f && Mathf.Abs(CanvasHeight - 1920f) < 2f;

        bool IsWidth1080 => Mathf.Abs(CanvasWidth - 1080f) < 2f;

        /// <summary>캔버스 월드 점 → 기준 px (왼쪽 위 원점, y는 아래로 — §1.1 표의 화면 좌표)</summary>
        Vector2 RefPoint(Vector3 world)
        {
            var local = (Vector2)_root.InverseTransformPoint(world);
            var area = _root.rect;
            return new Vector2(local.x - area.xMin, area.yMax - local.y);
        }

        /// <summary>대상 사각형 (기준 px — xMin/xMax 왼쪽부터, yMin = 위 끝 · yMax = 아래 끝, 화면 위에서 잼)</summary>
        Rect ScreenRect(RectTransform target)
        {
            if (target == null) return new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
            target.GetWorldCorners(Corners);
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var point = RefPoint(Corners[i]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static float GraphicAlpha(Component target) => target != null && target.TryGetComponent<Graphic>(out var graphic) ? graphic.color.a : 0f;

        static bool Close(Rect a, Rect b, float tolerance) =>
            Mathf.Abs(a.xMin - b.xMin) <= tolerance && Mathf.Abs(a.xMax - b.xMax) <= tolerance &&
            Mathf.Abs(a.yMin - b.yMin) <= tolerance && Mathf.Abs(a.yMax - b.yMax) <= tolerance;

        /// <summary>두 사각형이 0.5px 넘게 겹친다 (맞닿음은 겹침이 아니다)</summary>
        static bool Overlaps(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0.5f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0.5f;

        static void AddDistinct(List<float> values, float value)
        {
            foreach (var known in values)
            {
                if (Mathf.Abs(known - value) < 2f) return;
            }
            values.Add(value);
        }

        /// <summary>그 아래에 이 글을 가진 TMP 글자가 있는지 (확인 팝업 제목 — 필드가 비공개라 글로 찾는다)</summary>
        static bool HasText(Transform root, string text)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text == text) return true;
            }
            return false;
        }

        static string R(Rect rect) => $"{rect.xMin:0.#}–{rect.xMax:0.#} x {rect.yMin:0.#}–{rect.yMax:0.#}";

        static string OpenText(bool open) => open ? "열림" : "닫힘";

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errorLines++;
                if (type == LogType.Exception) _exceptionLines++;
                File.AppendAllText(Path.Combine(_dir, "errors.txt"), $"[{type}] {condition}\n{stackTrace}\n");
            }
            // 게임 경고 (S4-04c '리플렉션 · FtueBattleGuard 경고 없음'이 본다 — 이 도구의 FAIL 줄은 빼고, 상한까지만)
            else if (type == LogType.Warning && condition != null && _warnings.Count < MaxWarnings && !condition.StartsWith("[Verify] ", StringComparison.Ordinal))
                _warnings.Add(condition);
            WatchGameLog(condition);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        static bool HasFlag(string name)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg == name) return true;
            }
            return false;
        }
    }
}
