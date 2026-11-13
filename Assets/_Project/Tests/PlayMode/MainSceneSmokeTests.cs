using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdleGame.Tests
{
    /// <summary>
    /// 메인 씬을 실제로 띄워서 부팅/우편/소환/강화/전투 흐름이 에러 없이 도는지 확인한다.
    /// 탭 화면은 시트다 (UX-SPEC-IA): 부팅은 MAIN이라 패널이 모두 꺼져 있다 — 패널을 쓰는 테스트는 먼저 그 탭을 고른다.
    /// 시트 자동 복귀(GameSettings.SheetAutoReturn, PlayerPrefs)는 SetUp에서 끄고 TearDown에서 원래 값으로 되돌린다 (CR-04).
    /// </summary>
    public class MainSceneSmokeTests
    {
        string _savePath;
        bool _autoReturnWas;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 열린 시트가 시간 따라 닫히지 않게 (장면을 읽기 전에 — SheetController가 Awake에서 읽는다)
            _autoReturnWas = GameSettings.SheetAutoReturn;
            if (_autoReturnWas) GameSettings.SheetAutoReturn = false;
            _savePath = Path.Combine(Application.temporaryCachePath, "smoke_test_player.json");
            DeleteSave();
            GameBootstrap.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            // Start 코루틴(공지 로드 → 접속 팝업)이 돌 시간
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameSettings.SheetAutoReturn != _autoReturnWas) GameSettings.SheetAutoReturn = _autoReturnWas;
            GameBootstrap.SavePathOverride = null;
            DeleteSave();
            yield return null;
        }

        /// <summary>
        /// 새 게임 부팅. 메타가 켜져 있고 튜토리얼 중이면 접속 공지를 띄우지 않는다 (FTUE-01: 공지 대신 손가락 안내).
        /// 메타가 꺼져 있거나 튜토리얼 데이터가 없으면 예전처럼 접속 공지가 열린다.
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_InitializesGame_AndShowsStartupNotice()
        {
            Assert.IsTrue(Game.IsReady);
            Assert.AreEqual(1, Game.Mail.Count, "환영 우편");
            Assert.IsTrue(Game.Notice.IsLoaded);
            if (TutorialSuppressesNotices())
            {
                Assert.IsFalse(UIManager.Instance.Get<NoticePopup>().IsOpen, "튜토리얼 중에는 접속 공지를 띄우지 않는다");
            }
            else
            {
                Assert.IsTrue(UIManager.Instance.Get<NoticePopup>().IsOpen, "접속 공지 팝업");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MailPopup_ClaimAll_GrantsWelcomeRewards()
        {
            yield return CloseStartupPopups();
            double gem = Game.Currency.Get(CurrencyType.Gem);

            var popup = UIManager.Instance.Get<MailPopup>();
            popup.Open();
            yield return null;
            FindButton(popup, "ClaimAllButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(gem + 1000, Game.Currency.Get(CurrencyType.Gem), 1e-9);
            Assert.AreEqual(0, Game.Mail.Count);
        }

        /// <summary>
        /// 10회 소환: 결과 창이 열리고 패널 표의 10회 값(GachaSystem.GetCost)만큼 빠진다.
        /// 새 세이브는 별조각 GameConfig.startGem으로 시작하므로 0을 가정하지 않고 넣은 뒤 잔액을 기준으로, 값은 표에서 읽는다 (값이 바뀌어도 그대로).
        /// 비용은 Pull 안에서 바로 빠지므로 그 자리에서 잰다 (결과 연출 · 전투 수입과 섞이지 않게)
        /// </summary>
        [UnityTest]
        public IEnumerator GachaPanel_TenPull_ShowsResultPopup()
        {
            yield return CloseStartupPopups();
            // 새 게임은 소환 탭이 1-3 클리어 전까지 잠겨 있다 (F-03)
            if (Game.IsMetaActive) Game.Features.ForceUnlock(FeatureId.Gacha, false);
            int ownedBefore = Game.Characters.GetOwnedLineages().Count;

            Object.FindAnyObjectByType<MainTabController>().Select(MainTab.Gacha);
            yield return null;
            var panel = Object.FindAnyObjectByType<GachaPanel>();
            Assert.IsNotNull(panel, "소환 탭을 고르면 시트 안 소환 화면이 켜진다");
            // 패널이 쓰는 표 (빌더가 이름으로 묶는 비공개 직렬화 필드 _table)
            var table = typeof(GachaPanel).GetField("_table", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(panel) as GachaTable;
            Assert.IsNotNull(table, "GachaPanel._table");
            double cost = Game.Gacha.GetCost(table, 10);
            Assert.Greater(cost, 0d, "10회 소환 값");

            Game.Currency.Add(table.costCurrency, cost + 10000);
            double before = Game.Currency.Get(table.costCurrency);
            panel.Pull(10);
            Assert.AreEqual(before - cost, Game.Currency.Get(table.costCurrency), 1e-9, "10회 소환은 표의 10회 값만큼 쓴다");
            yield return new WaitForSecondsRealtime(1f);

            Assert.IsTrue(UIManager.Instance.Get<GachaResultPopup>().IsOpen);
            Assert.GreaterOrEqual(Game.Characters.GetOwnedLineages().Count, ownedBefore);
        }

        [UnityTest]
        public IEnumerator Boot_GrantsStarterCharacters_AndRunsBattle()
        {
            yield return CloseStartupPopups();
            Assert.AreEqual(Game.Config.starterLineages.Length, Game.Characters.GetOwnedLineages().Count);
            Assert.IsNotNull(Game.Formation.GetGuardian(0), "시작 캐릭터 자동 편성");

            yield return new WaitForSecondsRealtime(3f);
            Assert.IsTrue(Game.Battle.Sim.IsStarted);
            Assert.Greater(Game.Battle.Sim.ElapsedTime, 0f);
        }

        [UnityTest]
        public IEnumerator GrowthPanel_LevelUpButton_RaisesLevel()
        {
            yield return CloseStartupPopups();
            Game.Currency.Add(CurrencyType.Gold, 1000);

            // 부팅은 MAIN (패널이 모두 꺼져 있다) — 강화 시트를 열고 다 올라올 때까지 (열기 0.30초, UX-SPEC-IA §7.2 A8)
            Object.FindAnyObjectByType<MainTabController>().Select(MainTab.Growth);
            yield return new WaitForSecondsRealtime(0.35f);

            var panel = Object.FindAnyObjectByType<GrowthPanel>();
            Assert.IsNotNull(panel, "강화 탭을 고르면 시트 안 강화 화면이 켜진다");
            FindButton(panel, "LevelUp1Button").onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, Game.Growth.Level);
        }

        /// <summary>
        /// 접속 공지는 접속마다 많아야 한 장 (M50 — NoticeSystem.GetStartupNoticeToShow):
        /// (a) 첫 접속은 읽지 않은 첫 팝업 공지 하나만 열고, 닫아도 같은 접속에서 다음 공지가 이어 뜨지 않는다.
        /// (b) 다음 접속(같은 날)마다 그다음 읽지 않은 공지 하나, 다 읽은 뒤에는 없다.
        /// 다음 접속은 접속 공지 차례(UIManager.ShowStartupNotices — 발표자가 접속마다 한 번 부른다)를 다시 돌려 흉내 낸다 (읽음 표시는 세이브에 있다).
        /// 튜토리얼 중인 새 게임은 접속 때 공지가 없으므로 (a)도 그 차례를 직접 돌린다.
        /// </summary>
        [UnityTest]
        public IEnumerator StartupNotices_ShowFirstUnreadOnly_ThenNextUnreadPerLaunch()
        {
            var expected = Game.Notice.GetStartupNotices();
            Assume.That(expected.Count, Is.GreaterThanOrEqualTo(2), "번들 공지에 팝업 공지가 2개 이상 있어야 의미 있는 테스트");
            var popup = UIManager.Instance.Get<NoticePopup>();
            if (TutorialSuppressesNotices())
            {
                // 튜토리얼 중이라 접속 때 공지가 없었다 → 튜토리얼이 끝난 뒤 공지 차례(UIManager.ShowStartupNotices)를 직접 돌린다
                Assert.IsFalse(popup.IsOpen);
                yield return CloseStartupPopups();
                UIManager.Instance.StartCoroutine(UIManager.Instance.ShowStartupNotices());
                yield return null;
            }
            else
            {
                // 접속 팝업 차례(복귀 · 출석부 뒤)에 공지가 열린다 — 늦어도 3초
                float end = Time.realtimeSinceStartup + 3f;
                while (!popup.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            }

            // (a) 첫 접속: 읽지 않은 첫 공지 하나
            Assert.IsTrue(popup.IsOpen, "첫 접속: 읽지 않은 첫 팝업 공지");
            Assert.IsTrue(Game.Notice.IsRead(expected[0]), expected[0].id);
            Assert.IsFalse(Game.Notice.IsRead(expected[1]), "다음 공지는 아직 열리지 않음");
            popup.Close();
            // 접속 쉼(UIMotion.StartupGap 0.18초)보다 오래 기다려도 다음 공지가 이어 뜨지 않는다
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(popup.IsOpen, "같은 접속에서는 공지를 한 장만 띄운다 (M50)");
            Assert.IsFalse(Game.Notice.IsRead(expected[1]), "다음 공지는 다음 접속까지 읽지 않은 채");

            // (b) 다음 접속마다 그다음 읽지 않은 공지 하나, 다 읽으면 없음
            for (int launch = 1; launch <= expected.Count; launch++)
            {
                UIManager.Instance.StartCoroutine(UIManager.Instance.ShowStartupNotices());
                yield return null;
                if (launch == expected.Count)
                {
                    Assert.IsFalse(popup.IsOpen, "팝업 공지를 다 읽은 뒤의 접속에는 공지가 없다");
                    break;
                }
                Assert.IsTrue(popup.IsOpen, $"{launch + 1}번째 접속: {expected[launch].id}");
                Assert.IsTrue(Game.Notice.IsRead(expected[launch]), expected[launch].id);
                if (launch + 1 < expected.Count) Assert.IsFalse(Game.Notice.IsRead(expected[launch + 1]), "그다음 공지는 아직");
                popup.Close();
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsFalse(popup.IsOpen, "한 접속에 한 장");
            }
        }

        [UnityTest]
        public IEnumerator IdleGold_IsGrantedInBatches_NotEveryFrame()
        {
            yield return CloseStartupPopups();
            int goldEvents = 0;
            void OnChanged(CurrencyType type, double value)
            {
                if (type == CurrencyType.Gold) goldEvents++;
            }

            double before = Game.Currency.Get(CurrencyType.Gold);
            Game.Currency.Changed += OnChanged;
            int frames = 0;
            float end = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < end)
            {
                frames++;
                yield return null;
            }
            Game.Currency.Changed -= OnChanged;

            Assert.Greater(Game.Currency.Get(CurrencyType.Gold), before, "방치 단추가 쌓인다");
            // 최소 0.2초 간격으로 모아서 지급 → 2초 동안 많아야 10번 (+ 스테이지 보상 여유)
            Assert.LessOrEqual(goldEvents, 13, $"{frames}프레임 동안 단추 변경 이벤트 {goldEvents}회");
        }

        /// <summary>
        /// 메타 UI가 씬에 만들어져 있고(가이드 퀘스트 줄·바구니·전투 화면 빠른 아이콘 일지/출석), 잠긴 탭은 막고,
        /// 수선 일지·출석부·수선 바구니·설정 팝업이 열리는지.
        /// </summary>
        [UnityTest]
        public IEnumerator MetaUi_IsBuilt_AndPopupsOpen()
        {
            yield return CloseStartupPopups();
            var canvas = UIManager.Instance.transform;
            Assert.IsNotNull(canvas.Find(CoachTargets.GuideQuestBar), "가이드 퀘스트 줄");
            Assert.IsNotNull(canvas.Find(CoachTargets.IdleChestWidget), "수선 바구니 위젯");
            Assert.IsNotNull(canvas.Find(CoachTargets.JournalButton), "빠른 아이콘 수선 일지 (QuickRail_R)");
            Assert.IsNotNull(canvas.Find(CoachTargets.AttendanceButton), "빠른 아이콘 출석부 (QuickRail_L)");

            var settings = UIManager.Instance.Get<SettingsPopup>();
            settings.Open();
            yield return null;
            for (int i = 0; i < 4; i++)
            {
                FindButton(settings, $"Tab{i}").onClick.Invoke();
                yield return null;
            }
            settings.Close();

            if (!Game.IsMetaActive) yield break;

            var tabs = Object.FindAnyObjectByType<MainTabController>();
            if (!Game.Features.IsUnlocked(FeatureId.Shop))
            {
                int before = tabs.SelectedIndex;
                tabs.Select(MainTab.Shop);
                yield return null;
                Assert.AreEqual(before, tabs.SelectedIndex, "잠긴 상점 탭은 열리지 않는다");
            }

            foreach (FeatureId feature in System.Enum.GetValues(typeof(FeatureId)))
            {
                if (!Game.Features.IsUnlocked(feature)) Game.Features.ForceUnlock(feature, false);
            }
            yield return null;

            UINav.OpenJournal();
            yield return null;
            Assert.IsTrue(UIManager.Instance.Get<JournalPopup>().IsOpen, "수선 일지");
            yield return CloseStartupPopups();
            UINav.OpenAttendance();
            yield return null;
            Assert.IsTrue(UIManager.Instance.Get<AttendancePopup>().IsOpen, "출석부");
            yield return CloseStartupPopups();
            UINav.OpenIdleChest();
            yield return null;
            Assert.IsTrue(UIManager.Instance.Get<IdleChestPopup>().IsOpen, "수선 바구니");
            yield return CloseStartupPopups();
            tabs.Select(MainTab.Shop);
            yield return null;
            Assert.AreEqual((int)MainTab.Shop, tabs.SelectedIndex, "열린 상점 탭");
        }

        /// <summary>메타가 켜져 있고 새 게임 튜토리얼이 진행 중이면 접속 공지를 띄우지 않는다</summary>
        static bool TutorialSuppressesNotices() => Game.IsMetaActive && Game.Tutorial != null && Game.Tutorial.IsActive;

        /// <summary>
        /// 접속 팝업을 모두 닫는다 (UX-SPEC §4.3 WP-9 — UIManager.CloseAllImmediate, 연출 · 닫기 정책 없이): 다음 접속 창은 앞 창이 닫힌 뒤
        /// 0.18초(UIMotion.StartupGap) 쉬고 열리므로 0.3초 동안 아무것도 열리지 않을 때까지 되풀이한다 (늦어도 5초 — while(CloseTop())은 막는 팝업에서 멈출 수 있다).
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

        static Button FindButton(Component root, string name) =>
            root.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        void DeleteSave()
        {
            foreach (var path in new[] { _savePath, _savePath + ".bak", _savePath + ".tmp" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
