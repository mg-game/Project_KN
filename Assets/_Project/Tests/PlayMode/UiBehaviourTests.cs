using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// UI 행동 테스트 (UX-SPEC §4.3 WP-9 UiBehaviourTests — 메인 씬을 실제로 띄운다):
    ///   · 강화 x1 길게 누르기(반복 1번 이상) 뒤 떼어도 한 번 더 오르지 않는다 (M19 — 첫 반복이 클릭 자격을 없앤다).
    ///   · 누르는 중에 창 포커스를 잃으면 반복이 멈춘다 (M01 — 알트탭 중 떼기를 놓쳐도 단추를 계속 쓰지 않는다).
    ///   · 동화 인트로 중 뒤로는 종료 확인을 열지 않는다 (M15 — 인트로의 뒤로 처리기가 먹는다).
    ///   · 10회 소환 *버튼*은 쓰기 확인을 연다 (비용 줄 · '소환'), GachaPanel.Pull(10)은 확인 없이 바로 결과 (M55 — 스모크 · AutoShot이 Pull을 직접 부른다).
    ///   · 같은 토스트는 1.5초 안에 하나로 합친다 (M14 — 줄에 쌓이지 않는다), 다른 글은 줄에 든다.
    ///   · 겹친 팝업의 덮개는 0.4 (M48 — 아래 팝업 덮개는 그대로 1).
    /// 입력은 ExecuteEvents · onClick으로 흉내 낸다. 자동 복귀(GameSettings.SheetAutoReturn)는 SetUp에서 끄고 TearDown에서 되돌린다 (CR-04).
    /// 이 어셈블리는 TextMeshPro를 참조하지 않아 글은 Graphic의 text 속성을 이름으로 읽는다.
    /// </summary>
    public class UiBehaviourTests
    {
        const string GrowthLevelUp1 = CoachTargets.GrowthPanel + "/Buttons/LevelUp1Button";
        const string GachaPull10 = CoachTargets.GachaPanel + "/Buttons/Pull10Button";

        string _savePath;
        bool _autoReturnWas;
        bool _pull10SkipHad;
        string _pull10SkipWas;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _autoReturnWas = GameSettings.SheetAutoReturn;
            if (_autoReturnWas) GameSettings.SheetAutoReturn = false;
            // '오늘 다시 묻지 않기'(PlayerPrefs — 에디터 PlayerPrefs는 회사 · 제품 이름이 같은 사용자 에디터와 같은 자리)가 켜져 있으면 10회 확인이 뜨지 않는다 —
            // 테스트 동안만 비우고 TearDown에서 처음 모양으로 (있었으면 그 값, 없었으면 지움). GetString(키, null)은 null 기본값이 ""로 바뀌어 올 수 있어 HasKey로 본다
            _pull10SkipHad = PlayerPrefs.HasKey(GachaPanel.Pull10SkipKey);
            _pull10SkipWas = _pull10SkipHad ? PlayerPrefs.GetString(GachaPanel.Pull10SkipKey) : null;
            if (_pull10SkipHad) PlayerPrefs.DeleteKey(GachaPanel.Pull10SkipKey);
            _savePath = Path.Combine(Application.temporaryCachePath, "ui_behaviour_test_player.json");
            DeleteSave();
            GameBootstrap.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameSettings.SheetAutoReturn != _autoReturnWas) GameSettings.SheetAutoReturn = _autoReturnWas;
            if (_pull10SkipHad) PlayerPrefs.SetString(GachaPanel.Pull10SkipKey, _pull10SkipWas);
            else if (PlayerPrefs.HasKey(GachaPanel.Pull10SkipKey)) PlayerPrefs.DeleteKey(GachaPanel.Pull10SkipKey);
            GameBootstrap.SavePathOverride = null;
            DeleteSave();
            yield return null;
        }

        // ═════════════════════════ 길게 누르기 (M19 · M01) ═════════════════════════

        [UnityTest]
        public IEnumerator HoldRepeat_ThenRelease_DoesNotAddAnExtraLevel()
        {
            yield return EnterMain();
            Game.Currency.Add(CurrencyType.Gold, 1e9);
            yield return OpenGrowth();
            var button = Find(GrowthLevelUp1).GetComponent<Button>();
            var data = Press(button);
            int start = Game.Growth.Level;
            yield return WaitUntil(() => Game.Growth.Level >= start + 2, 3f);
            Assert.GreaterOrEqual(Game.Growth.Level, start + 2, "길게 누르면 0.35초 뒤부터 반복해 오른다");

            int beforeRelease = Game.Growth.Level;
            Release(button, data);
            yield return null;
            yield return null;
            Assert.AreEqual(beforeRelease, Game.Growth.Level, "반복한 누름을 떼도 클릭으로 한 번 더 오르지 않는다");
            Assert.IsFalse(data.eligibleForClick, "첫 반복이 그 누름의 클릭 자격을 없앤다");
        }

        [UnityTest]
        public IEnumerator FocusLoss_DuringHold_StopsSpending()
        {
            yield return EnterMain();
            Game.Currency.Add(CurrencyType.Gold, 1e9);
            yield return OpenGrowth();
            var button = Find(GrowthLevelUp1).GetComponent<Button>();
            var data = Press(button);
            int start = Game.Growth.Level;
            yield return WaitUntil(() => Game.Growth.Level >= start + 1, 3f);
            Assert.Greater(Game.Growth.Level, start, "반복이 시작됐다");

            BroadcastFocus(false);
            int atFocusLoss = Game.Growth.Level;
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(atFocusLoss, Game.Growth.Level, "포커스를 잃은 뒤 1초 동안 더 오르지 않는다 (떼기를 놓쳐도)");

            BroadcastFocus(true);
            Release(button, data);
            yield return null;
            Assert.AreEqual(atFocusLoss, Game.Growth.Level, "늦게 온 떼기도 한 번 더 올리지 않는다");
            Assert.AreEqual(1f, button.transform.localScale.x, 0.02f, "누름 크기가 되돌아온다");
        }

        // ═════════════════════════ 뒤로 (M15) ═════════════════════════

        [UnityTest]
        public IEnumerator Back_DuringStoryIntro_DoesNotOpenExitConfirm()
        {
            var intro = Object.FindAnyObjectByType<StoryIntroView>(FindObjectsInactive.Include);
            // 새 게임이면 부팅 직후 인트로가 시작된다 (첫 단계 StoryIntro) — 늦어도 4초
            yield return WaitUntil(() => intro != null && intro.IsPlaying, 4f);
            if (intro == null || !intro.IsPlaying) Assert.Ignore("새 게임 동화 인트로가 돌지 않는다 (메타 꺼짐 · 콘텐츠 없음)");
            var message = UIManager.Instance.Get<MessagePopup>();
            UIManager.Instance.OnBack();
            yield return null;
            yield return null;
            Assert.IsFalse(message.IsOpen, "인트로 중 뒤로는 인트로가 먹는다 (종료 확인 없음)");
            Assert.IsFalse(HasText(message, UIManager.ExitTitle) && message.IsOpen);
        }

        // ═════════════════════════ 10회 소환 확인 (M55) ═════════════════════════

        [UnityTest]
        public IEnumerator TenPullButton_AsksSpendConfirm_ButPullTenDoesNot()
        {
            yield return EnterMain();
            Game.Currency.Add(CurrencyType.Gem, 10000);
            Object.FindAnyObjectByType<MainTabController>().Select(MainTab.Gacha);
            yield return new WaitForSecondsRealtime(0.4f);
            UIManager.Instance.CloseAllImmediate();
            var message = UIManager.Instance.Get<MessagePopup>();
            var result = UIManager.Instance.Get<GachaResultPopup>();
            double gems = Game.Currency.Get(CurrencyType.Gem);

            Find(GachaPull10).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(message.IsOpen, "10회 버튼은 쓰기 확인을 연다");
            Assert.IsFalse(result.IsOpen, "확인 전에는 소환하지 않는다");
            Assert.AreEqual(gems, Game.Currency.Get(CurrencyType.Gem), 1e-9, "확인 전에는 쓰지 않는다");
            var costRow = Private<RectTransform>(message, "_costRow");
            Assert.IsTrue(costRow != null && costRow.gameObject.activeSelf, "비용 줄(아이콘 · 비용 · 보유 → 남음)이 보인다");
            Assert.IsTrue(HasText(message, "소환"), "확인 버튼 글 '소환'");

            UIManager.Instance.CloseAllImmediate();
            yield return null;
            Object.FindAnyObjectByType<GachaPanel>().Pull(10);
            yield return null;
            Assert.IsFalse(message.IsOpen, "Pull(10)은 확인 없이 바로 소환한다");
            Assert.IsTrue(result.IsOpen, "결과 창");
        }

        // ═════════════════════════ 토스트 (M14) ═════════════════════════

        [UnityTest]
        public IEnumerator Toast_SameMessageWithin1_5s_IsMergedNotQueued()
        {
            yield return EnterMain();
            var toast = Object.FindAnyObjectByType<ToastView>(FindObjectsInactive.Include);
            Assert.IsNotNull(toast, "ToastView");
            toast.Clear();
            const string error = "단추가 모자라요";
            UIManager.Instance.ShowToast(error, ToastKind.Error);
            yield return new WaitForSecondsRealtime(0.4f);
            UIManager.Instance.ShowToast(error, ToastKind.Error);
            yield return new WaitForSecondsRealtime(0.4f);
            UIManager.Instance.ShowToast(error, ToastKind.Error);
            yield return null;
            Assert.AreEqual(error, toast.CurrentText, "보이는 토스트는 그 하나");
            Assert.AreEqual(ToastKind.Error, toast.CurrentKind);
            Assert.AreEqual(0, toast.QueuedCount, "1.5초 안의 같은 글 · 같은 종류는 줄에 넣지 않는다");

            UIManager.Instance.ShowToast("새 친구가 왔어요", ToastKind.Info);
            yield return null;
            Assert.AreEqual(1, toast.QueuedCount, "다른 글은 줄에 든다");
            Assert.AreEqual(error, toast.CurrentText, "보이는 토스트는 그대로");
            toast.Clear();
        }

        // ═════════════════════════ 겹친 팝업 덮개 (M48) ═════════════════════════

        [UnityTest]
        public IEnumerator StackedPopup_DimIsPointFour_AndLowerDimStays()
        {
            yield return EnterMain();
            var settings = UIManager.Instance.Get<SettingsPopup>();
            settings.Open();
            yield return new WaitForSecondsRealtime(0.5f);
            var lowerDim = Private<CanvasGroup>(settings, "_dimGroup");
            Assert.IsNotNull(lowerDim, "설정 팝업 덮개 CanvasGroup");
            Assert.AreEqual(UIMotion.DimSingle, lowerDim.alpha, 0.02f, "팝업 하나면 덮개 1");

            UIManager.Instance.ShowConfirm("확인", "겹친 팝업 덮개", null);
            var message = UIManager.Instance.Get<MessagePopup>();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(message.IsOpen && settings.IsOpen);
            var upperDim = Private<CanvasGroup>(message, "_dimGroup");
            Assert.IsNotNull(upperDim, "확인 팝업 덮개 CanvasGroup");
            Assert.AreEqual(UIMotion.DimStacked, upperDim.alpha, 0.02f, "겹친 팝업 덮개는 0.4");
            Assert.AreEqual(UIMotion.DimSingle, lowerDim.alpha, 0.02f, "아래 팝업 덮개는 그대로");
            var systemLayer = message.transform.parent;
            var popupLayer = settings.transform.parent;
            Assert.Greater(systemLayer.GetSiblingIndex(), popupLayer.GetSiblingIndex(), "확인 창 층(SystemPopups)이 Popups 위");
            UIManager.Instance.CloseAllImmediate();
        }

        // ═════════════════════════ 도우미 ═════════════════════════

        static RectTransform Root => (RectTransform)UIManager.Instance.transform;

        static RectTransform Find(string path)
        {
            var target = Root.Find(path) as RectTransform;
            Assert.IsNotNull(target, path);
            return target;
        }

        static T Private<T>(object owner, string field) where T : class
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (info != null) return info.GetValue(owner) as T;
            }
            return null;
        }

        static PointerEventData Press(Button button)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                eligibleForClick = true,
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
            };
            data.pressPosition = data.position;
            data.pointerPress = button.gameObject;
            data.rawPointerPress = button.gameObject;
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            return data;
        }

        /// <summary>EventSystem처럼 뗀다: pointerUp, 클릭 자격이 남아 있으면 pointerClick</summary>
        static void Release(Button button, PointerEventData data)
        {
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            if (data.eligibleForClick) ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
            data.eligibleForClick = false;
            data.pointerPress = null;
        }

        /// <summary>장면의 모든 MonoBehaviour에 OnApplicationFocus(focused)를 보낸다 (§5.1 BroadcastFocus)</summary>
        static void BroadcastFocus(bool focused)
        {
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.isActiveAndEnabled) behaviour.SendMessage("OnApplicationFocus", focused, SendMessageOptions.DontRequireReceiver);
            }
        }

        static IEnumerator OpenGrowth()
        {
            Object.FindAnyObjectByType<MainTabController>().Select(MainTab.Growth);
            yield return new WaitForSecondsRealtime(0.45f);
            UIManager.Instance.CloseAllImmediate();
            yield return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        static bool HasText(Component root, string text)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                PropertyInfo property;
                try
                {
                    property = graphic.GetType().GetProperty("text", typeof(string));
                }
                catch (AmbiguousMatchException)
                {
                    continue;
                }
                if (property != null && property.GetValue(graphic) as string == text) return true;
            }
            return false;
        }

        /// <summary>튜토리얼을 건너뛰고 기능을 모두 연 MAIN (팝업 없음)</summary>
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
                // 첫 만남 카드가 소환 시트를 덮지 않게 (확정 소환을 받은 것으로)
                Game.Progress.SetFlag(MetaFlags.FirstMeetingSummoned);
            }
            yield return null;
            UIManager.Instance.CloseAllImmediate();
            UINav.GoToMain(true);
            yield return null;
            yield return null;
        }

        /// <summary>접속 팝업을 모두 닫는다 — CloseAllImmediate를 0.3초 동안 아무것도 열리지 않을 때까지 (늦어도 5초, 접속 쉼 0.18초보다 길게)</summary>
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
    }
}
