using System.Collections;
using System.IO;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdleGame.Tests
{
    /// <summary>
    /// 화면 틀 테스트 (UX-SPEC-IA §7.5 UiFrameTests (a)~(l)) — 메인 씬을 실제로 띄워 MAIN(전투를 크게, 시트 없음) ↔ SHEET(탭 시트)를 확인한다:
    /// 부팅 상태 · 열기와 전투 올림 · 손으로 다시 누름 · 코드 이동(GoToTab) · 사냥 · 손잡이 · 뒤로 순서 · 머리 끌어 내림 · HUD 흐림(SheetFade) ·
    /// 코치 대상 넘김 · 자동 복귀 · 장면 다시 읽기 뒤 초기화 · 돌아가기 칸의 틀 되돌리기 · 같은 프레임 IsUnderSheet · 튜토리얼 중 바구니.
    /// <para>
    /// 거의 모든 테스트는 튜토리얼을 건너뛰고 기능을 모두 연 MAIN에서 시작한다 (EnterMain). (a) 부팅 · (l) 튜토리얼 중 바구니만 새 게임 그대로 본다.
    /// 자동 복귀(GameSettings.SheetAutoReturn — PlayerPrefs)는 SetUp에서 끄고 TearDown에서 원래 값으로 되돌린다. (h)만 켜고
    /// 시간은 SheetController.AutoReturnSecondsForTests로 줄인다 (CR-04).
    /// </para>
    /// <para>
    /// 시트는 unscaled 시간으로 움직이므로 기다림은 모두 실제 시간이다. 시트 시계는 켠 뒤 두 프레임을 1/30초로 자르므로(CR-13) 느린 프레임이면
    /// 0.30초보다 조금 늦게 멈출 수 있다 — WaitSettled가 SettleLimit까지 더 기다린다. 줄이기(번쩍임 줄이기 설정)면 p가 계단처럼 바뀌므로
    /// 중간 p를 보는 줄은 줄이기가 아닐 때만 확인한다. 입력은 onClick · ExecuteEvents로 흉내 낸다
    /// (실제 마우스가 움직이면 자동 복귀 시계가 처음부터 다시 센다 — 배치 실행 기준).
    /// </para>
    /// <para>
    /// Game 창 비율: 가로로 넓은 창(Free Aspect · 1920x1080 등)은 CanvasScalerAdapter가 세로 기준으로 바꿔 캔버스 높이가 1920 그대로이고
    /// (1920x1080 → 3413x1920), 길쭉한 창은 더 높아진다 — 시트 위 끝(SafeArea 아래 + 1006) · 덱 띠 · 스테이지 칸 판정과 HUD 흐림은 어느 창에서나 같다.
    /// 비율 따라 바뀌는 전투 올림만 (b)에서 StageArea가 PanStageLimit 안일 때 0보다 큰지 본다.
    /// </para>
    /// </summary>
    public class UiFrameTests
    {
        const string StagePath = "SafeArea/StageArea";
        const string BattlePath = StagePath + "/Battle";
        const string FieldPath = BattlePath + "/Field";
        const string MetaHudPath = BattlePath + "/MetaHud";
        const string ContentPath = "SafeArea/ContentArea";
        const string DragAreaPath = ContentPath + "/SheetHeader/DragArea";
        const string HandleHitPath = ContentPath + "/SheetHeader/HandleHit";
        const string RecommendedPowerPath = MetaHudPath + "/RecommendedPower";
        const string SynergyRowPath = BattlePath + "/Hud/SynergyRow";
        const string RailLeftPath = MetaHudPath + "/QuickRail_L";
        const string RailRightPath = MetaHudPath + "/QuickRail_R";
        const string MainGlowPath = CoachTargets.TabMain + "/Glow";

        /// <summary>시트 열기(0.30초)를 기다리는 시간 (§7.5 (b) "0.35초 뒤")</summary>
        const float OpenWait = 0.35f;
        /// <summary>시트 닫기(0.24초)를 기다리는 시간</summary>
        const float CloseWait = 0.3f;
        /// <summary>느린 프레임에서 멈출 때까지 더 기다리는 상한 (실제 시간)</summary>
        const float SettleLimit = 1.5f;
        /// <summary>
        /// 시트가 열리면 전투가 꼭 올라가야 하는 StageArea 높이 상한 (기준 px) = 21:9 Game 창(1080x2520, 인셋 없음 — 올림 ≈ 88).
        /// 가로로 넓은 창은 언제나 이 안이다 (StageArea 1620). §1.3 규칙은 훨씬 길쭉한 창(StageArea ≈ 2420 이상, 약 9:22.7 —
        /// 좁고 긴 Free Aspect 창)에서 0이다 (전투는 아래로 내려가지 않는다)
        /// </summary>
        const float PanStageLimit = 2220f;

        /// <summary>탭 패널 (순서 = MainTab)</summary>
        static readonly string[] PanelPaths =
        {
            CoachTargets.GrowthPanel, CoachTargets.FormationPanel, CoachTargets.CharacterPanel,
            CoachTargets.ExpeditionPanel, CoachTargets.GachaPanel, CoachTargets.ShopPanel,
        };

        string _savePath;
        bool _autoReturnWas;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 자동 복귀는 (h)만 켠다 — 장면을 읽기 전에 (SheetController가 Awake에서 읽는다)
            _autoReturnWas = GameSettings.SheetAutoReturn;
            if (_autoReturnWas) GameSettings.SheetAutoReturn = false;
            SheetController.AutoReturnSecondsForTests = 0f;

            _savePath = Path.Combine(Application.temporaryCachePath, "ui_frame_test_player.json");
            DeleteSave();
            GameBootstrap.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SheetController.AutoReturnSecondsForTests = 0f;
            if (GameSettings.SheetAutoReturn != _autoReturnWas) GameSettings.SheetAutoReturn = _autoReturnWas;
            GameBootstrap.SavePathOverride = null;
            DeleteSave();
            yield return null;
        }

        // ═════════════════════════ (a) 부팅 ═════════════════════════

        /// <summary>(a) 부팅은 언제나 MAIN: 선택 −1, ContentArea · 패널 꺼짐, 틀 상태가 이 장면 시트에 이어진 처음 값, 사냥 빛 켜짐</summary>
        [UnityTest]
        public IEnumerator Boot_StartsInMain_WithSheetClosed()
        {
            var tabs = Tabs;
            Assert.IsNotNull(tabs, "하단 탭 바 (MainTabController)");
            Assert.IsTrue(tabs.Sheet != null, "탭 시트 (SheetController)");
            Assert.IsTrue(tabs.MainButton != null, "가운데 사냥 버튼 (Tab_Main)");
            Assert.IsTrue(tabs.IsMain, "부팅은 언제나 MAIN");
            AssertMain(tabs, "부팅");
            Assert.IsTrue(UIFrame.SheetRect == Find(ContentPath), "UIFrame이 이 장면의 ContentArea에 이어져 있다");
            Assert.IsTrue(UIFrame.StageRect == Find(StagePath), "UIFrame이 이 장면의 StageArea에 이어져 있다");
            var glow = Find(MainGlowPath);
            if (glow != null) Assert.IsTrue(glow.gameObject.activeInHierarchy, "MAIN에서는 사냥 빛이 켜져 있다");
            yield return null;
            AssertMain(tabs, "부팅 다음 프레임 (시작에 Selected · 열기 없음)");
        }

        // ═════════════════════════ (b) 열기 · 전투 올림 ═════════════════════════

        /// <summary>(b) Select(강화) → 0.35초 뒤 p 1, 전투 올림 = PanOpen (기기 픽셀 정수, CR-09). 닫으면 올림 0</summary>
        [UnityTest]
        public IEnumerator Select_OpensSheet_AndPansBattleUp()
        {
            yield return EnterMain();
            var tabs = Tabs;
            var mapper = Get<BattleFieldMapper>(FieldPath);
            var driver = Get<BattleFrameDriver>(BattlePath);
            Assert.IsTrue(mapper != null && driver != null, "전장(BattleFieldMapper) · 올림(BattleFrameDriver)");
            Assert.AreEqual(0f, mapper.Pan, 1e-3f, "MAIN 올림 0");

            tabs.Select(MainTab.Growth);
            Assert.IsTrue(UIFrame.SheetOpen, "목표 상태는 고른 그 프레임에 열림");
            Assert.AreEqual((int)MainTab.Growth, UIFrame.SheetTab);
            Assert.IsTrue(Find(ContentPath).gameObject.activeSelf, "고르면 ContentArea가 바로 켜진다 (패널 OnEnable)");
            Assert.IsTrue(Find(CoachTargets.GrowthPanel).gameObject.activeSelf, "강화 패널이 바로 켜진다");

            yield return WaitSettled(OpenWait);
            AssertSheet(tabs, MainTab.Growth, "열기 0.35초 뒤");
            // 올림은 화면 비율마다 런타임에 잰다 (§1.3 — 16:9 360 · 20:9 194). 아주 길쭉한 Game 창이면 규칙상 0이라 그때는 내려가지 않는지만 본다
            Assert.GreaterOrEqual(driver.PanOpen, 0f, "전투는 아래로 내려가지 않는다");
            float stageHeight = ((RectTransform)driver.transform).rect.height;
            if (stageHeight <= PanStageLimit) Assert.Greater(driver.PanOpen, 0f, $"시트가 열리면 전투를 위로 올린다 (StageArea {stageHeight:0})");
            float device = driver.PanOpen * Root.GetComponent<Canvas>().scaleFactor;
            Assert.AreEqual(Mathf.Round(device), device, 0.01f, $"올림 {driver.PanOpen}은 기기 픽셀 정수 (CR-09)");
            Assert.AreEqual(UIFrame.TabBarHeight, Find(ContentPath).anchoredPosition.y, 0.01f, "열린 자리 = 탭 바 바로 위 (970~1750)");

            tabs.ShowMain();
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "닫은 뒤");
        }

        // ═════════════════════════ (c) 손으로 다시 누름 ═════════════════════════

        /// <summary>(c) 열린 탭을 손으로 다시 누르면 Reselected 한 번 → 닫기 (Selected 없음, MainShown 한 번)</summary>
        [UnityTest]
        public IEnumerator Retap_OpenTab_FiresReselectedOnce_ThenCloses()
        {
            yield return EnterMain();
            var tabs = Tabs;
            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            AssertSheet(tabs, MainTab.Growth, "강화 시트");

            using (var events = new TabEvents(tabs))
            {
                tabs.GetTabButton(MainTab.Growth).onClick.Invoke();
                Assert.AreEqual(1, events.Reselected, "다시 누름 = Reselected 한 번");
                Assert.AreEqual(MainTab.Growth, events.LastReselected);
                Assert.AreEqual(0, events.Selected, "다시 누름은 Selected를 부르지 않는다");
                Assert.AreEqual(1, events.MainShown, "MAIN이 됨 한 번");
                Assert.AreEqual(-1, tabs.SelectedIndex, "목표 상태는 누른 그 프레임에 MAIN");
                Assert.IsFalse(UIFrame.SheetOpen);

                yield return WaitSettled(CloseWait);
                AssertMain(tabs, "다시 눌러 닫은 뒤");
                Assert.AreEqual(1, events.Reselected, "Reselected는 한 번만");
            }
        }

        // ═════════════════════════ (d) 코드 이동 ═════════════════════════

        /// <summary>(d) UINav.GoToTab(강화)를 두 번 불러도 닫히지 않는다 (열리는 중 · 다 열린 뒤). 다른 탭은 미끄러짐 없이 바꾼다</summary>
        [UnityTest]
        public IEnumerator GoToTab_Twice_NeverCloses_ButSwitchesTabs()
        {
            yield return EnterMain();
            var tabs = Tabs;
            UINav.GoToTab(MainTab.Growth);
            yield return null;
            UINav.GoToTab(MainTab.Growth);
            Assert.IsTrue(UIFrame.SheetOpen, "열리는 중에 한 번 더 — 닫지 않는다");
            Assert.AreEqual((int)MainTab.Growth, tabs.SelectedIndex);

            yield return WaitSettled(OpenWait);
            AssertSheet(tabs, MainTab.Growth, "두 번 부른 뒤");
            UINav.GoToTab(MainTab.Growth);
            yield return null;
            AssertSheet(tabs, MainTab.Growth, "다 열린 뒤 한 번 더 — 그대로");

            UINav.GoToTab(MainTab.Characters);
            yield return null;
            AssertSheet(tabs, MainTab.Characters, "열린 채 다른 탭으로 (미끄러짐 · 올림 변화 없음)");
        }

        // ═════════════════════════ (e) 뒤로 순서 ═════════════════════════

        /// <summary>(e) 뒤로: ① 맨 위 팝업 (시트는 그대로) → ② 시트 → ③ (내려가는 중에 또) 종료 확인 → ④ 종료 확인 닫기 (§4.7)</summary>
        [UnityTest]
        public IEnumerator Back_ClosesPopup_ThenSheet_ThenAsksToExit()
        {
            yield return EnterMain();
            var tabs = Tabs;
            var ui = UIManager.Instance;
            var settings = ui.Get<SettingsPopup>();
            var message = ui.Get<MessagePopup>();
            Assert.IsTrue(settings != null && message != null, "설정 · 확인 팝업");

            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            settings.Open();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(settings.IsOpen, "시트 위 설정 팝업");
            Assert.IsFalse(message.IsOpen);

            ui.OnBack();
            yield return null;
            Assert.IsFalse(settings.IsOpen, "① 맨 위 팝업부터 닫는다");
            Assert.IsTrue(UIFrame.SheetOpen, "① 시트는 그대로");
            Assert.AreEqual((int)MainTab.Growth, tabs.SelectedIndex);

            yield return null;
            yield return null;
            ui.OnBack();
            Assert.IsFalse(UIFrame.SheetOpen, "② 팝업이 없으면 시트를 내린다");
            Assert.AreEqual(-1, tabs.SelectedIndex);
            Assert.IsFalse(message.IsOpen, "② 종료 확인은 아직");

            yield return null;
            ui.OnBack();
            yield return null;
            Assert.IsTrue(message.IsOpen, "③ 시트가 내려가는 중에 또 누르면 바로 종료 확인 (두 번 눌러 끄기 없음)");
            Assert.IsTrue(HasText(message, UIManager.ExitTitle), $"③ 종료 확인 제목 '{UIManager.ExitTitle}'");

            ui.OnBack();
            yield return null;
            Assert.IsFalse(message.IsOpen, "④ 열린 종료 확인은 뒤로를 한 번 더 누르면 닫힌다");
            ui.CloseAllImmediate();
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "뒤로 순서 뒤");
        }

        // ═════════════════════════ 사냥 · 손잡이 · 뒤로로 닫기 ═════════════════════════

        /// <summary>사냥(MAIN이면 누름만) · 머리 손잡이 · 뒤로 — 모두 MAIN으로 닫는다. 닫기가 끝나면 패널도 꺼진다</summary>
        [UnityTest]
        public IEnumerator MainButton_Handle_AndBack_CloseTheSheet()
        {
            yield return EnterMain();
            var tabs = Tabs;
            var ui = UIManager.Instance;

            tabs.MainButton.onClick.Invoke();
            yield return null;
            AssertMain(tabs, "MAIN에서 사냥 — 누름 표시만");

            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            tabs.MainButton.onClick.Invoke();
            Assert.IsFalse(UIFrame.SheetOpen, "사냥 = 닫기");
            Assert.AreEqual(-1, tabs.SelectedIndex);
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "사냥으로 닫은 뒤");

            tabs.Select(MainTab.Formation);
            yield return WaitSettled(OpenWait);
            var handle = Get<Button>(HandleHitPath);
            Assert.IsTrue(handle != null, "머리 손잡이 (SheetHeader/HandleHit)");
            handle.onClick.Invoke();
            Assert.IsFalse(UIFrame.SheetOpen, "손잡이 = 닫기");
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "손잡이로 닫은 뒤");

            tabs.Select(MainTab.Characters);
            yield return WaitSettled(OpenWait);
            ui.OnBack();
            Assert.IsFalse(UIFrame.SheetOpen, "팝업이 없으면 뒤로 = 시트 닫기");
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "뒤로로 닫은 뒤");
            Assert.IsFalse(ui.Get<MessagePopup>().IsOpen, "시트를 닫은 뒤로는 종료 확인을 띄우지 않는다");
        }

        // ═════════════════════════ 머리 끌기 ═════════════════════════

        /// <summary>
        /// 머리 DragArea 끌기 (§4.3): 짧고 느리게(100px · 0.5초, p ≈ 0.88) 놓으면 다시 열리고, 끄는 동안 전투 올림도 p를 따른다.
        /// 빠르게 길게(300px · 0.2초, p ≈ 0.64 · 1500px/s) 놓으면 MAIN으로 닫힌다
        /// </summary>
        [UnityTest]
        public IEnumerator HeaderSwipe_ShortSlowReopens_FastLongDismisses()
        {
            yield return EnterMain();
            var tabs = Tabs;
            var sheet = tabs.Sheet;
            var mapper = Get<BattleFieldMapper>(FieldPath);
            var driver = Get<BattleFrameDriver>(BattlePath);
            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            Assert.IsTrue(Find(DragAreaPath) != null, "머리 끌기 자리 (SheetHeader/DragArea — 경로 고정)");

            yield return DragHeader(100f, 0.5f, () =>
            {
                Assert.IsTrue(sheet.IsDragging, "끄는 중");
                Assert.AreEqual(1f - 100f / UIFrame.SheetHeight, UIFrame.SheetProgress, 0.03f, "시트가 손가락을 따라간다");
                Assert.AreEqual(driver.PanOpen * UIFrame.SheetProgress, mapper.Pan, 0.5f, "끄는 동안 전투 올림 = PanOpen·p");
            });
            Assert.IsTrue(UIFrame.SheetOpen, "짧고 느린 끌기는 다시 연다 (p ≥ 0.75, 1200px/s 아래)");
            yield return WaitSettled(0.1f);
            AssertSheet(tabs, MainTab.Growth, "다시 열린 뒤");

            yield return DragHeader(300f, 0.2f, null);
            Assert.IsFalse(UIFrame.SheetOpen, "빠르게 길게 끌어 놓으면 닫는다");
            Assert.AreEqual(-1, tabs.SelectedIndex);
            yield return WaitSettled(0.1f);
            AssertMain(tabs, "끌어 내린 뒤");
        }

        // ═════════════════════════ (f) HUD 흐림 ═════════════════════════

        /// <summary>
        /// (f) 시트가 다 열리면 권장 전투력 알약 · 시너지 줄 · 빠른 아이콘 줄 알파 0 (누름 끔), 수선 바구니 0.5 (누를 수 있음).
        /// 빠른 아이콘은 MAIN에서만 보인다 — 닫으면 모두 1로 돌아온다. 흐려진 이벤트 아이콘은 날아가기 도착지가 아니다 (CR-03)
        /// </summary>
        [UnityTest]
        public IEnumerator SheetFade_HidesRailsAndPill_DimsChest_OnlyWhileOpen()
        {
            Assume.That(Game.IsMetaActive, "메타 UI(빠른 아이콘 · 바구니 · 권장 전투력)가 있어야 한다");
            yield return EnterMain();
            var tabs = Tabs;
            var pill = Get<SheetFade>(RecommendedPowerPath);
            var synergy = Get<SheetFade>(SynergyRowPath);
            var left = Get<SheetFade>(RailLeftPath);
            var right = Get<SheetFade>(RailRightPath);
            var chest = Get<SheetFade>(CoachTargets.IdleChestWidget);
            var all = new[] { pill, synergy, left, right, chest };
            foreach (var fade in all) Assert.IsTrue(fade != null, "SheetFade (권장 전투력 · 시너지 줄 · 빠른 아이콘 줄 둘 · 바구니)");

            foreach (var fade in all) Assert.AreEqual(1f, fade.Alpha, 1e-3f, $"{fade.name}: MAIN에서는 그대로");
            Assert.IsTrue(Raycasts(left) && Raycasts(right), "MAIN: 빠른 아이콘을 누를 수 있다");
            Assert.IsTrue(Find(CoachTargets.EventButton).gameObject.activeSelf, "MAIN 빠른 아이콘: 이벤트");
            Assert.IsTrue(Find(CoachTargets.AttendanceButton).gameObject.activeSelf, "MAIN 빠른 아이콘: 출석부");
            Assert.IsTrue(Find(CoachTargets.JournalButton).gameObject.activeSelf, "MAIN 빠른 아이콘: 수선 일지");
            Assert.IsFalse(UIFrame.IsFeedbackHidden(Find(CoachTargets.EventButton)), "MAIN: 이벤트 아이콘은 날아가기 도착지");

            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            Assert.AreEqual(1f, UIFrame.SheetProgress, 1e-4f);
            Assert.AreEqual(0f, pill.Alpha, 1e-3f, "권장 전투력 알약은 사라진다");
            Assert.AreEqual(0f, synergy.Alpha, 1e-3f, "시너지 줄은 사라진다");
            Assert.AreEqual(0f, left.Alpha, 1e-3f, "왼쪽 빠른 아이콘 줄은 사라진다");
            Assert.AreEqual(0f, right.Alpha, 1e-3f, "오른쪽 빠른 아이콘 줄은 사라진다");
            Assert.AreEqual(0.5f, chest.Alpha, 1e-3f, "바구니는 반투명 (튜토리얼을 건너뛰었으므로 0.5 — (l))");
            Assert.IsFalse(Raycasts(left) || Raycasts(right) || Raycasts(pill), "사라진 HUD는 누름도 받지 않는다");
            Assert.IsTrue(Raycasts(chest), "반투명 바구니는 그대로 누를 수 있다");
            Assert.IsTrue(UIFrame.IsFeedbackHidden(Find(CoachTargets.EventButton)), "흐려진 이벤트 아이콘은 날아가기 도착지가 아니다 (CR-03)");

            tabs.ShowMain();
            yield return WaitSettled(CloseWait);
            foreach (var fade in all) Assert.AreEqual(1f, fade.Alpha, 1e-3f, $"{fade.name}: MAIN으로 돌아오면 다시 1");
            Assert.IsTrue(Raycasts(left) && Raycasts(right), "MAIN: 빠른 아이콘을 다시 누를 수 있다");
        }

        // ═════════════════════════ (g) 코치 대상 ═════════════════════════

        /// <summary>
        /// (g) 시트가 열린(목표 상태) 동안 시트 밑 대상(덱 첫 칸)과 사라지는 HUD(빠른 아이콘) 대상은 사냥 버튼으로 넘긴다 (RedirectedToMain).
        /// 탭 바 대상은 넘기지 않는다. MAIN으로 가면(닫기를 부른 그 프레임) 원래 대상
        /// </summary>
        [UnityTest]
        public IEnumerator CoachResolver_SendsHiddenTargets_ToMainButton_WhileSheetOpen()
        {
            yield return EnterMain();
            var tabs = Tabs;
            var resolver = new CoachTargetResolver(Root, Find("Popups"));
            var deckStep = Step(CoachTargets.DeckCard0, MetaCondition.Count(CounterKey.EchoSummon, 1));
            var tabStep = Step(CoachTargets.TabGacha, MetaCondition.Always());
            var deck = Find(CoachTargets.DeckCard0);
            var main = Find(CoachTargets.TabMain);
            Assert.IsTrue(deck != null && main != null, "덱 첫 칸 · 사냥 버튼");

            Assert.IsTrue(resolver.Resolve(deckStep, false, out _) == deck, "MAIN: 덱 칸 그대로");
            Assert.IsFalse(resolver.RedirectedToMain);

            tabs.Select(MainTab.Growth);
            Assert.IsTrue(resolver.Resolve(deckStep, false, out var dragTo) == main, "고른 그 프레임 — 시트 밑 덱 칸 대신 사냥 버튼 (CR-02)");
            Assert.IsTrue(resolver.RedirectedToMain, "RedirectedToMain");
            Assert.IsNull(dragTo, "끌어 놓을 곳은 비운다");
            Assert.IsTrue(resolver.Resolve(tabStep, false, out _) == Find(CoachTargets.TabGacha), "탭 바 대상은 넘기지 않는다");
            Assert.IsFalse(resolver.RedirectedToMain, "Resolve마다 처음부터");
            if (Game.IsMetaActive)
            {
                var railStep = Step(CoachTargets.EventButton, MetaCondition.Always());
                Assert.IsTrue(resolver.Resolve(railStep, false, out _) == main, "시트가 열리면 사라지는 빠른 아이콘(SheetFade 0)도 사냥 버튼으로");
                Assert.IsTrue(resolver.RedirectedToMain);
            }

            yield return WaitSettled(OpenWait);
            Assert.IsTrue(resolver.Resolve(deckStep, false, out _) == main, "다 열린 뒤에도 사냥 버튼");

            tabs.ShowMain();
            Assert.IsTrue(resolver.Resolve(deckStep, false, out _) == deck, "닫기를 부른 그 프레임 — 원래 대상");
            Assert.IsFalse(resolver.RedirectedToMain);
        }

        // ═════════════════════════ (h) 자동 복귀 ═════════════════════════

        /// <summary>(h) 자동 복귀를 켜면 시트가 열려 멈춘 뒤 정해 둔 시간(테스트 고리 0.6초) 동안 손대지 않으면 MAIN으로. 끄면 그대로</summary>
        [UnityTest]
        public IEnumerator AutoReturn_ClosesSheet_AfterConfiguredSeconds()
        {
            yield return EnterMain();
            var tabs = Tabs;
            const float seconds = 0.6f;
            SheetController.AutoReturnSecondsForTests = seconds;
            GameSettings.SheetAutoReturn = true;

            // 시계는 시트가 멈춘 뒤부터 한 프레임에 실제 시간 이하만 센다 — 고른 때부터 잰 시간이 하한이다
            float selectedAt = Time.realtimeSinceStartup;
            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            AssertSheet(tabs, MainTab.Growth, "자동 복귀 켜고 연 시트");
            yield return new WaitForSecondsRealtime(seconds * 0.5f);
            Assert.IsTrue(UIFrame.SheetOpen, "정해 둔 시간 전에는 닫지 않는다");

            float limit = selectedAt + seconds + 5f;
            while (UIFrame.SheetOpen && Time.realtimeSinceStartup < limit) yield return null;
            float closedAfter = Time.realtimeSinceStartup - selectedAt;
            Assert.IsFalse(UIFrame.SheetOpen, $"손대지 않고 {seconds}초가 지나면 MAIN으로 (팝업 · 튜토리얼 · 코치 · 입력 없음)");
            Assert.GreaterOrEqual(closedAfter, seconds, $"고른 뒤 {closedAfter:0.00}초 만에 닫혔다 — 정해 둔 시간보다 일찍 닫지 않는다");
            Assert.AreEqual(-1, tabs.SelectedIndex);
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "자동 복귀 뒤");

            GameSettings.SheetAutoReturn = false;
            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            yield return new WaitForSecondsRealtime(seconds * 2f);
            Assert.IsTrue(UIFrame.SheetOpen, "설정을 끄면 자동 복귀 없음");
        }

        // ═════════════════════════ (i) 장면 다시 읽기 ═════════════════════════

        /// <summary>(i) 시트가 열린(+ 돌아가기 칸이 찬) 채 장면을 다시 읽으면 UIFrame이 처음 값(닫힘, −1, 0, 멈춤)으로 새 장면 시트에 이어진다 (CR-20)</summary>
        [UnityTest]
        public IEnumerator SceneReload_ResetsFrameState()
        {
            yield return EnterMain();
            Tabs.Select(MainTab.Formation);
            UINav.PushReturn("‹ 테스트로", () => { });
            yield return null;
            Assert.IsTrue(UIFrame.SheetOpen && UINav.HasReturn, "열린 시트 + 찬 돌아가기 칸");
            var oldContent = UIFrame.SheetRect;

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            Assert.IsFalse(UIFrame.SheetOpen, "장면을 다시 읽으면 닫힘");
            Assert.AreEqual(-1, UIFrame.SheetTab, "탭 −1");
            Assert.AreEqual(0f, UIFrame.SheetProgress, "p 0");
            Assert.IsTrue(UIFrame.SheetSettled, "멈춤");
            Assert.IsTrue(oldContent == null, "옛 장면의 시트는 부서졌다");
            Assert.IsTrue(UIFrame.SheetRect != null, "새 장면의 시트에 다시 이어진다 (옛 장면 OnDestroy가 새 잇기를 풀지 않는다)");
            Assert.IsTrue(UIFrame.SheetRect == Find(ContentPath), "새 장면의 ContentArea");
            Assert.IsFalse(UINav.HasReturn, "돌아가기 칸도 비운다");
            AssertMain(Tabs, "다시 읽은 장면");
        }

        // ═════════════════════════ (j) 돌아가기 칸의 틀 ═════════════════════════

        /// <summary>
        /// (j) MAIN에서 연 일지 → 칸 넣기 + GoToTab(강화) → TryPopReturn → 시트를 바로 내리고 MAIN 위에 일지가 다시 열린다 (CR-05).
        /// 넣을 때 틀(−1)을 기억하고, 코드 이동(GoToTab)은 칸을 비우지 않는다
        /// </summary>
        [UnityTest]
        public IEnumerator TryPopReturn_FromMain_ClosesSheet_ThenReopensJournal()
        {
            Assume.That(Game.IsMetaActive, "수선 일지(메타)가 있어야 한다");
            yield return EnterMain();
            var tabs = Tabs;
            var ui = UIManager.Instance;
            var journal = ui.Get<JournalPopup>();
            Assert.IsTrue(journal != null, "수선 일지 팝업");

            UINav.OpenJournal();
            yield return null;
            Assert.IsTrue(journal.IsOpen, "MAIN에서 연 일지");
            // 일지 [가기]: 돌아갈 곳을 넣고(지금 틀 = MAIN) 팝업을 닫은 뒤 강화 시트로
            UINav.PushReturn("‹ 수선 일지로", UINav.OpenJournal);
            Assert.AreEqual(-1, UINav.ReturnTab, "넣을 때의 틀 = MAIN");
            ui.CloseAllImmediate();
            UINav.GoToTab(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            AssertSheet(tabs, MainTab.Growth, "가기로 연 강화 시트");
            Assert.IsTrue(UINav.HasReturn, "코드 이동(GoToTab)은 칸을 비우지 않는다");

            Assert.IsTrue(UINav.TryPopReturn(), "돌아가기");
            AssertMain(tabs, "MAIN에서 넣은 칸 → 시트를 바로 내린다");
            Assert.IsTrue(journal.IsOpen, "MAIN 위에 일지가 다시 열린다");
            Assert.IsFalse(UINav.HasReturn, "칸은 비었다");
            yield return null;
            Assert.IsTrue(journal.IsOpen);
            AssertMain(tabs, "다음 프레임");
        }

        /// <summary>
        /// (j) 짝: 다른 탭에서 넣은 칸은 그 탭으로 되돌린 뒤 다시 열고, 지금 탭에서 넣은 칸은 틀을 그대로 둔다.
        /// 다른 탭 버튼을 손으로 누르거나 사냥으로 MAIN에 가면 칸을 비운다 (M72 · CR-05)
        /// </summary>
        [UnityTest]
        public IEnumerator TryPopReturn_RestoresPushedTab_AndUserNavigationClears()
        {
            yield return EnterMain();
            var tabs = Tabs;
            int reopened = 0;
            tabs.Select(MainTab.Formation);
            yield return WaitSettled(OpenWait);

            UINav.PushReturn("‹ 편성으로", () => reopened++);
            Assert.AreEqual((int)MainTab.Formation, UINav.ReturnTab, "넣을 때의 틀 = 편성");
            UINav.GoToTab(MainTab.Characters);
            yield return null;
            Assert.IsTrue(UINav.TryPopReturn());
            Assert.AreEqual(1, reopened);
            yield return WaitSettled(OpenWait);
            AssertSheet(tabs, MainTab.Formation, "다른 탭에서 넣은 칸 → 그 탭으로 되돌린다");

            UINav.PushReturn("‹ 편성으로", () => reopened++);
            Assert.IsTrue(UINav.TryPopReturn());
            Assert.AreEqual(2, reopened);
            AssertSheet(tabs, MainTab.Formation, "지금 탭에서 넣은 칸 → 틀은 그대로");

            UINav.PushReturn("‹ 편성으로", () => reopened++);
            tabs.GetTabButton(MainTab.Characters).onClick.Invoke();
            Assert.IsFalse(UINav.HasReturn, "다른 탭을 손으로 누르면 칸을 비운다");
            yield return WaitSettled(OpenWait);

            UINav.PushReturn("‹ 친구들로", () => reopened++);
            tabs.MainButton.onClick.Invoke();
            Assert.IsFalse(UINav.HasReturn, "사냥(MAIN으로 가는 모든 길)은 칸을 비운다");
            Assert.IsFalse(UINav.TryPopReturn());
            Assert.AreEqual(2, reopened, "비운 칸은 다시 열지 않는다");
            yield return WaitSettled(CloseWait);
            AssertMain(tabs, "사냥 뒤");
        }

        // ═════════════════════════ (k) IsUnderSheet ═════════════════════════

        /// <summary>
        /// (k) IsUnderSheet는 목표 상태를 본다: Select(강화)와 같은 프레임(p는 아직 0)에 덱 첫 칸이 시트 밑, ShowMain과 같은 프레임에 풀린다.
        /// 탭 바(소환 탭) · 시트 안 · 전투 위쪽 HUD(스테이지 칸) 대상은 시트가 열려 있어도 false (CR-02)
        /// </summary>
        [UnityTest]
        public IEnumerator IsUnderSheet_FollowsTargetState_InTheSameFrame()
        {
            yield return EnterMain();
            var tabs = Tabs;
            var deck = Find(CoachTargets.DeckCard0);
            var gachaTab = Find(CoachTargets.TabGacha);
            var stagePanel = Find(CoachTargets.StagePanel);
            var growthPanel = Find(CoachTargets.GrowthPanel);
            Assert.IsTrue(deck != null && gachaTab != null && stagePanel != null && growthPanel != null, "대상 경로");
            Assert.IsFalse(UIFrame.IsUnderSheet(deck), "MAIN: 시트 없음");

            tabs.Select(MainTab.Growth);
            if (!UIMotion.Reduced) Assert.AreEqual(0f, UIFrame.SheetProgress, 1e-4f, "고른 그 프레임 — 아직 p 0");
            Assert.IsTrue(UIFrame.IsUnderSheet(deck), "같은 프레임에 덱 첫 칸이 시트 밑 (목표 상태)");
            Assert.IsFalse(UIFrame.IsUnderSheet(gachaTab), "탭 바 대상은 시트 밑이 아니다");
            Assert.IsFalse(UIFrame.IsUnderSheet(stagePanel), "전투 위쪽 HUD는 시트 위");
            Assert.IsFalse(UIFrame.IsUnderSheet(growthPanel), "시트 안 대상은 시트 밑이 아니다");

            yield return WaitSettled(OpenWait);
            Assert.IsTrue(UIFrame.IsUnderSheet(deck), "다 열린 뒤에도");
            Assert.IsFalse(UIFrame.IsUnderSheet(gachaTab), "다 열린 뒤에도 탭 바는 아니다");

            tabs.ShowMain();
            Assert.IsFalse(UIFrame.IsUnderSheet(deck), "닫기를 부른 그 프레임에 풀린다");
        }

        // ═════════════════════════ (l) 튜토리얼 중 바구니 ═════════════════════════

        /// <summary>(l) 튜토리얼 중에는 시트가 열려도 바구니를 흐리지 않고(FTUE 9단계가 가리킨다) 다른 흐림은 그대로, 튜토리얼이 끝나면 바로 0.5</summary>
        [UnityTest]
        public IEnumerator ChestFade_IgnoredDuringTutorial_DimsOnceSkipped()
        {
            Assume.That(Game.IsMetaActive && Game.Tutorial != null && Game.Tutorial.IsActive, "새 게임 튜토리얼 중이어야 의미 있는 테스트");
            yield return CloseStartupPopups();
            var tabs = Tabs;
            var chest = Get<SheetFade>(CoachTargets.IdleChestWidget);
            var pill = Get<SheetFade>(RecommendedPowerPath);
            Assert.IsTrue(chest != null && pill != null, "바구니 · 권장 전투력 SheetFade");

            tabs.Select(MainTab.Growth);
            yield return WaitSettled(OpenWait);
            Assert.AreEqual(1f, UIFrame.SheetProgress, 1e-4f, "튜토리얼 중에도 강화 시트는 열린다");
            Assert.AreEqual(1f, chest.Alpha, 1e-3f, "튜토리얼 중에는 바구니를 흐리지 않는다");
            Assert.AreEqual(0f, pill.Alpha, 1e-3f, "다른 흐림은 튜토리얼과 상관없다");

            // 끝남 알림(Tutorial.Finished)에 SheetFade가 그 자리에서 다시 맞춘다 — 같은 프레임(p는 그대로 1)에 본다
            Game.Tutorial.Skip();
            Assert.IsFalse(Game.Tutorial.IsActive, "건너뛰면 튜토리얼 끝");
            Assert.AreEqual(1f, UIFrame.SheetProgress, 1e-4f);
            Assert.AreEqual(0.5f, chest.Alpha, 1e-3f, "튜토리얼이 끝나면 바로 0.5 (시트가 다 열린 채)");
            yield return null;
        }

        // ═════════════════════════ 도우미 ═════════════════════════

        static RectTransform Root => (RectTransform)UIManager.Instance.transform;

        static MainTabController Tabs => Object.FindAnyObjectByType<MainTabController>();

        static RectTransform Find(string path) => Root.Find(path) as RectTransform;

        static T Get<T>(string path) where T : Component
        {
            var target = Root.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        static FtueStepDef Step(string target, MetaCondition complete) => new() { id = "ia_test_step", coachTarget = target, complete = complete };

        /// <summary>그 오브젝트의 CanvasGroup이 모두 누름을 받는지 (하나라도 끄면 그 아래는 눌리지 않는다)</summary>
        static bool Raycasts(Component target)
        {
            foreach (var group in target.GetComponents<CanvasGroup>())
            {
                if (!group.blocksRaycasts) return false;
            }
            return true;
        }

        /// <summary>그 아래에 이 글을 가진 글자가 있는지 (이 어셈블리는 TextMeshPro를 참조하지 않아 Graphic의 text 속성을 이름으로 읽는다)</summary>
        static bool HasText(Component root, string text)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                System.Reflection.PropertyInfo property;
                try
                {
                    property = graphic.GetType().GetProperty("text", typeof(string));
                }
                catch (System.Reflection.AmbiguousMatchException)
                {
                    continue;
                }
                if (property != null && property.GetValue(graphic) as string == text) return true;
            }
            return false;
        }

        /// <summary>MAIN에 멈춰 있다: 선택 −1 · 목표 닫힘 · p 0 · ContentArea와 패널 꺼짐 · 전투 올림 0</summary>
        static void AssertMain(MainTabController tabs, string when)
        {
            Assert.AreEqual(-1, tabs.SelectedIndex, $"{when}: 선택 −1 (MAIN)");
            Assert.IsFalse(UIFrame.SheetOpen, $"{when}: 시트 목표 닫힘");
            Assert.AreEqual(-1, UIFrame.SheetTab, $"{when}: 시트 탭 −1");
            Assert.AreEqual(0f, UIFrame.SheetProgress, 1e-4f, $"{when}: p 0");
            Assert.IsTrue(UIFrame.SheetSettled, $"{when}: 멈춤");
            Assert.IsFalse(Find(ContentPath).gameObject.activeSelf, $"{when}: ContentArea 꺼짐");
            foreach (var path in PanelPaths) Assert.IsFalse(Find(path).gameObject.activeSelf, $"{when}: {path} 꺼짐");
            var mapper = Get<BattleFieldMapper>(FieldPath);
            if (mapper != null) Assert.AreEqual(0f, mapper.Pan, 1e-3f, $"{when}: 전투 올림 0");
        }

        /// <summary>그 탭 시트가 다 열려 멈춰 있다: 선택 · 목표 · p 1 · 그 패널만 켜짐 · 전투 올림 = PanOpen</summary>
        static void AssertSheet(MainTabController tabs, MainTab tab, string when)
        {
            Assert.AreEqual((int)tab, tabs.SelectedIndex, $"{when}: 선택 {tab}");
            Assert.IsTrue(UIFrame.SheetOpen, $"{when}: 시트 목표 열림");
            Assert.AreEqual((int)tab, UIFrame.SheetTab, $"{when}: 시트 탭 {tab}");
            Assert.AreEqual(1f, UIFrame.SheetProgress, 1e-4f, $"{when}: p 1");
            Assert.IsTrue(UIFrame.SheetSettled, $"{when}: 멈춤");
            Assert.IsTrue(Find(ContentPath).gameObject.activeSelf, $"{when}: ContentArea 켜짐");
            for (int i = 0; i < PanelPaths.Length; i++)
                Assert.AreEqual(i == (int)tab, Find(PanelPaths[i]).gameObject.activeSelf, $"{when}: {PanelPaths[i]} {(i == (int)tab ? "켜짐" : "꺼짐")}");
            var mapper = Get<BattleFieldMapper>(FieldPath);
            var driver = Get<BattleFrameDriver>(BattlePath);
            if (mapper != null && driver != null) Assert.AreEqual(driver.PanOpen, mapper.Pan, 0.01f, $"{when}: 전투 올림 = PanOpen");
        }

        /// <summary>시트가 멈출 때까지 기다린다 (최소 minSeconds, 늦어도 SettleLimit — 첫 두 프레임 시계 제한 때문에 느린 프레임이면 조금 더 걸린다)</summary>
        static IEnumerator WaitSettled(float minSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - start;
                if (elapsed >= SettleLimit || (elapsed >= minSeconds && UIFrame.SheetSettled)) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// 시트 머리 DragArea를 아래로 distance(캔버스 px)만큼 seconds(실제 시간) 동안 끈다 — ExecuteEvents begin · drag(매 프레임) · end.
        /// beforeRelease는 마지막 위치에서 놓기 직전에 부른다
        /// </summary>
        static IEnumerator DragHeader(float distance, float seconds, System.Action beforeRelease)
        {
            var area = Find(DragAreaPath);
            float scale = Root.GetComponent<Canvas>().scaleFactor;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(area.rect.center));
            Vector2 end = start - new Vector2(0f, distance * scale);
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
                t = Mathf.Clamp01((Time.realtimeSinceStartup - begin) / seconds);
                var position = Vector2.Lerp(start, end, t);
                data.delta = position - data.position;
                data.position = position;
                ExecuteEvents.Execute(area.gameObject, data, ExecuteEvents.dragHandler);
            }
            beforeRelease?.Invoke();
            ExecuteEvents.Execute(area.gameObject, data, ExecuteEvents.endDragHandler);
        }

        /// <summary>튜토리얼을 건너뛰고 기능을 모두 연 MAIN으로 (팝업 없음, 시트 닫힘 — 빠른 아이콘이 다시 놓일 몇 프레임까지)</summary>
        static IEnumerator EnterMain()
        {
            yield return CloseStartupPopups();
            if (Game.IsMetaActive)
            {
                if (Game.Tutorial != null && Game.Tutorial.IsActive) Game.Tutorial.Skip();
                foreach (FeatureId feature in System.Enum.GetValues(typeof(FeatureId)))
                {
                    if (!Game.Features.IsUnlocked(feature)) Game.Features.ForceUnlock(feature, false);
                }
            }
            yield return null;
            UIManager.Instance.CloseAllImmediate();
            UINav.GoToMain(true);
            yield return null;
            yield return null;
        }

        /// <summary>
        /// 접속 팝업을 모두 닫는다 (UX-SPEC §4.3 WP-9 — UIManager.CloseAllImmediate): 다음 접속 창은 앞 창이 닫힌 뒤 0.18초(UIMotion.StartupGap) 쉬고
        /// 열리므로 0.3초 동안 아무것도 열리지 않을 때까지 되풀이한다 (늦어도 5초)
        /// </summary>
        static IEnumerator CloseStartupPopups()
        {
            float quietSince = Time.realtimeSinceStartup;
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end && Time.realtimeSinceStartup - quietSince < 0.3f)
            {
                if (UIManager.Instance.HasOpenPopup)
                {
                    UIManager.Instance.CloseAllImmediate();
                    quietSince = Time.realtimeSinceStartup;
                }
                yield return null;
            }
        }

        void DeleteSave()
        {
            foreach (var path in new[] { _savePath, _savePath + ".bak", _savePath + ".tmp" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        /// <summary>탭 바 알림 세기 (Selected · Reselected · MainShown) — using이 끝나면 구독을 푼다</summary>
        sealed class TabEvents : System.IDisposable
        {
            readonly MainTabController _tabs;
            public int Selected;
            public int Reselected;
            public int MainShown;
            public MainTab LastReselected = (MainTab)(-1);

            public TabEvents(MainTabController tabs)
            {
                _tabs = tabs;
                tabs.Selected += OnSelected;
                tabs.Reselected += OnReselected;
                tabs.MainShown += OnMainShown;
            }

            void OnSelected(MainTab tab) => Selected++;

            void OnReselected(MainTab tab)
            {
                Reselected++;
                LastReselected = tab;
            }

            void OnMainShown() => MainShown++;

            public void Dispose()
            {
                if (_tabs == null) return;
                _tabs.Selected -= OnSelected;
                _tabs.Reselected -= OnReselected;
                _tabs.MainShown -= OnMainShown;
            }
        }
    }
}
