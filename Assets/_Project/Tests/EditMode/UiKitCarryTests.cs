using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleGame.Tests
{
    /// <summary>
    /// UX Wave 3 동결 키트 이월 (RM-2 KIT) — 장면 없이 본다.
    ///   · (1) 확인 창 ConfirmOpts.onCancelButton: 취소 *버튼*을 눌렀을 때만, 닫힘 알림(onCancel → onClosed) 뒤에 한 번.
    ///         덮개 · 뒤로 · X · 확인 · 다른 확인이 넘겨받음에는 부르지 않는다 (종료 확인 — 덮개 · 뒤로는 머물기, UX-SPEC 부록 D M15).
    ///         꺼진 부모 아래 진짜 MessagePopup (열고 닫는 연출 코루틴 없이 바로 닫힌다). 종료 확인은 진짜 UIManager.OnBack으로 연다.
    ///   · (2) 토스트: MAIN 빠른 아이콘 줄이 보이면 팝업 없는 토스트 x 128–952 (이벤트 칸 12–116을 덮지 않게), 시트 · 팝업 위에서는 100–980.
    ///         줄 찾기(UIFrame.StageRect 아래 QuickRail_L · R의 켜진 QuickIconView 칸)도 코치 대상 경로로 만든 자리에서 본다.
    ///   · (3) 10레벨 고비 칩: 강화 카드 글 끝 오른쪽 · 내구도 줄 높이 (틈이 좁으면 좁힘, 그래도 없으면 카드 오른쪽 끝에 떠오르지 않고) —
    ///         60 떠오르는 동안에도 'Lv.N' 글에 닿지 않고, 카드 아래 강화 버튼 줄로 내려가지 않는다.
    ///   · (4) 수선권 '+N' 받침: 바구니 그림 옆 — '가득!' 꼬리표 · 시간 알약(MetaSceneBuilder 자리)과 겹치지 않는다 (WP-P 수정, 다시 확인).
    /// </summary>
    public class UiKitCarryTests
    {
        // ═════════════════════════ (1) 확인 창 취소 버튼 ═════════════════════════

        const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        sealed class PopupRig : IDisposable
        {
            public GameObject Root;
            public MessagePopup Popup;
            public UnityEvent Ok;
            public UnityEvent Cancel;
            /// <summary>제목 · 확인 이름 · 취소 이름 (TMP — 글은 TextOf로 읽는다)</summary>
            public Component Title;
            public Component OkLabel;
            public Component CancelLabel;

            public void Dispose()
            {
                if (Root != null) Object.DestroyImmediate(Root);
            }
        }

        /// <summary>
        /// 꺼진 부모 아래 MessagePopup (제목 · 본문 TMP + 확인 · 취소 Button, 버튼마다 이름 TMP 자식 — Awake가 찾아 _okLabel · _cancelLabel로 쓴다) — Awake를 직접 부른다.
        /// 시험 어셈블리는 UnityEngine.UI · TMP를 참조하지 않으므로 형식은 직렬화 필드에서 얻는다.
        /// </summary>
        static PopupRig NewPopup()
        {
            var root = new GameObject("KitPopupRoot");
            root.SetActive(false);
            var go = new GameObject("MessagePopup", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var popup = go.AddComponent<MessagePopup>();
            var buttonType = FieldOf("_okButton").FieldType;
            var textType = FieldOf("_titleText").FieldType.Assembly.GetType("TMPro.TextMeshProUGUI");
            Assert.IsNotNull(textType, "TMPro.TextMeshProUGUI");
            var title = Child(go, "Title", textType);
            FieldOf("_titleText").SetValue(popup, title);
            FieldOf("_bodyText").SetValue(popup, Child(go, "Body", textType));
            var ok = Child(go, "OkButton", buttonType);
            var cancel = Child(go, "CancelButton", buttonType);
            var okLabel = Child(ok.gameObject, "Label", textType);
            var cancelLabel = Child(cancel.gameObject, "Label", textType);
            FieldOf("_okButton").SetValue(popup, ok);
            FieldOf("_cancelButton").SetValue(popup, cancel);
            var awake = typeof(MessagePopup).GetMethod("Awake", NonPublicInstance);
            Assert.IsNotNull(awake, "MessagePopup.Awake");
            awake.Invoke(popup, null);
            return new PopupRig
            {
                Root = root, Popup = popup, Ok = ClickOf(ok), Cancel = ClickOf(cancel),
                Title = title, OkLabel = okLabel, CancelLabel = cancelLabel,
            };
        }

        static FieldInfo FieldOf(string name)
        {
            var info = typeof(MessagePopup).GetField(name, NonPublicInstance);
            Assert.IsNotNull(info, $"MessagePopup.{name}");
            return info;
        }

        static Component Child(GameObject parent, string name, Type type)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent(type);
        }

        static UnityEvent ClickOf(Component button) => (UnityEvent)button.GetType().GetProperty("onClick").GetValue(button);

        static string TextOf(Component text) => (string)text.GetType().GetProperty("text").GetValue(text);

        static object GetPrivate(object target, string name)
        {
            var info = target.GetType().GetField(name, NonPublicInstance);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{name}");
            return info.GetValue(target);
        }

        static void SetPrivate(object target, string name, object value)
        {
            var info = target.GetType().GetField(name, NonPublicInstance);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{name}");
            info.SetValue(target, value);
        }

        [Test]
        public void CancelButton_RunsOnCancelButton_AfterClosedNotices()
        {
            using var rig = NewPopup();
            var log = new List<string>();
            rig.Popup.Show("종료", "본문", () => log.Add("ok"), new ConfirmOpts
            {
                onCancel = () => log.Add("cancel"),
                onClosed = () => log.Add("closed"),
                onCancelButton = () => log.Add("button"),
            });
            Assert.IsTrue(rig.Popup.IsOpen);

            rig.Cancel.Invoke();
            Assert.IsFalse(rig.Popup.IsOpen, "취소 버튼은 창을 닫는다");
            CollectionAssert.AreEqual(new[] { "cancel", "closed", "button" }, log, "닫힘 알림(취소 → 닫힘) 뒤에 버튼 알림 한 번");

            rig.Cancel.Invoke();
            Assert.AreEqual(3, log.Count, "닫힌 뒤에 다시 눌러도 더 부르지 않는다");
        }

        [Test]
        public void DimBackAndClose_DoNotRunOnCancelButton()
        {
            using var rig = NewPopup();
            int button = 0, cancel = 0, closed = 0;
            var opts = new ConfirmOpts { onCancel = () => cancel++, onClosed = () => closed++, onCancelButton = () => button++ };

            rig.Popup.Show("종료", "본문", null, opts);
            rig.Popup.RequestDismiss();
            Assert.IsFalse(rig.Popup.IsOpen, "덮개 · 뒤로는 닫는다 (기본 OnDimOrBack)");
            Assert.AreEqual(0, button, "덮개 · 뒤로는 버튼 누름이 아니다 (종료 확인 = 머물기)");
            Assert.AreEqual(1, cancel);
            Assert.AreEqual(1, closed);

            rig.Popup.Show("종료", "본문", null, opts);
            rig.Popup.Close();
            Assert.AreEqual(0, button, "X · 코드 닫기도 버튼 누름이 아니다");
            Assert.AreEqual(2, cancel);
            Assert.AreEqual(2, closed);

            // 같은 창의 다음 확인에서 취소 버튼을 누르면 그 확인의 것만
            rig.Popup.Show("종료", "본문", null, opts);
            rig.Cancel.Invoke();
            Assert.AreEqual(1, button);
        }

        [Test]
        public void OkButton_DoesNotRunOnCancelButton()
        {
            using var rig = NewPopup();
            int ok = 0, button = 0, cancel = 0, closed = 0;
            rig.Popup.Show("종료", "본문", () => ok++,
                new ConfirmOpts { onCancel = () => cancel++, onClosed = () => closed++, onCancelButton = () => button++ });
            rig.Ok.Invoke();
            Assert.IsFalse(rig.Popup.IsOpen);
            Assert.AreEqual(1, ok, "확인 ('더 지킬래요' 자리)");
            Assert.AreEqual(0, cancel);
            Assert.AreEqual(1, closed);
            Assert.AreEqual(0, button, "확인은 취소 버튼 누름이 아니다");
        }

        [Test]
        public void ReplacedConfirm_DropsItsCancelButtonAction()
        {
            using var rig = NewPopup();
            int firstButton = 0, firstCancel = 0, secondButton = 0;
            rig.Popup.Show("앞", "본문", null, new ConfirmOpts { onCancel = () => firstCancel++, onCancelButton = () => firstButton++ });
            rig.Popup.Show("뒤", "본문", null, new ConfirmOpts { onCancelButton = () => secondButton++ });
            Assert.AreEqual(1, firstCancel, "넘겨받힌 앞 확인은 취소로 끝난다");
            Assert.AreEqual(0, firstButton, "넘겨받힘은 버튼 누름이 아니다");

            rig.Cancel.Invoke();
            Assert.AreEqual(0, firstButton, "앞 확인의 버튼 알림은 버렸다");
            Assert.AreEqual(1, secondButton, "지금 확인의 것만");
        }

        [Test]
        public void PlainConfirm_CancelButtonStillJustCloses()
        {
            using var rig = NewPopup();
            int cancel = 0, closed = 0;
            rig.Popup.Show("확인", "본문", null, new ConfirmOpts { onCancel = () => cancel++, onClosed = () => closed++ });
            rig.Cancel.Invoke();
            Assert.IsFalse(rig.Popup.IsOpen, "onCancelButton이 없으면 예전처럼 닫기만");
            Assert.AreEqual(1, cancel);
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void ThrowingCancelButtonAction_IsLogged_AndPopupStillCloses()
        {
            using var rig = NewPopup();
            int closed = 0;
            rig.Popup.Show("종료", "본문", null, new ConfirmOpts
            {
                onClosed = () => closed++,
                onCancelButton = () => throw new InvalidOperationException("kit cancel boom"),
            });
            LogAssert.Expect(LogType.Exception, new Regex("kit cancel boom"));
            Assert.DoesNotThrow(() => rig.Cancel.Invoke(), "알림 예외는 기록만 (누름 처리는 끊기지 않는다)");
            Assert.IsFalse(rig.Popup.IsOpen);
            Assert.AreEqual(1, closed, "닫힘 알림은 버튼 알림보다 먼저 이미 불렸다");
        }

        /// <summary>
        /// 진짜 UIManager.OnBack → ⑤ ShowExitConfirm (뒤로 처리기 · 말풍선 · 팝업 · 돌아가기 칩 · 시트가 없다): 이름 자리 · 알림 자리를 보고,
        /// 끄기 알림을 셈으로 바꿔 (진짜 Quit은 부르지 않는다) 뒤로 · 덮개 · '더 지킬래요'는 머물고 '닫기' 버튼만 끄는지 본다
        /// </summary>
        [Test]
        public void ExitConfirm_FromBack_QuitsOnlyOnTheCancelButton()
        {
            using var rig = NewPopup();
            var host = new GameObject("KitUIManager");
            host.transform.SetParent(rig.Root.transform, false);
            var ui = host.AddComponent<UIManager>();
            SetPrivate(ui, "_popups", new UIPopup[] { rig.Popup });
            // 돌아가기 칩은 장면에서 찾지 않는다 (③ 건너뜀 — 칩 없음)
            SetPrivate(ui, "_returnChipSearched", true);
            var instance = typeof(UIManager).GetProperty(nameof(UIManager.Instance), BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(instance, "UIManager.Instance");
            var previous = UIManager.Instance;
            if (InfoTooltipView.IsOpen) InfoTooltipView.Hide();
            try
            {
                // Instance = ui (팝업 장부 — 열린 확인 창 위에서 다시 누른 뒤로가 맨 위 팝업으로 간다)
                var awake = typeof(UIManager).GetMethod("Awake", NonPublicInstance);
                Assert.IsNotNull(awake, "UIManager.Awake");
                awake.Invoke(ui, null);
                Assert.AreSame(ui, UIManager.Instance);

                ui.OnBack();
                Assert.IsTrue(rig.Popup.IsOpen, "아무것도 없으면 뒤로 = 종료 확인");
                Assert.AreSame(rig.Popup, ui.TopPopup);
                Assert.AreEqual(UIManager.ExitTitle, TextOf(rig.Title));
                Assert.AreEqual("더 지킬래요", TextOf(rig.OkLabel), "확인 자리(오른쪽 Primary) = 머물기");
                Assert.AreEqual("닫기", TextOf(rig.CancelLabel), "취소 자리(왼쪽 Secondary) = 끄기");
                Assert.IsNull(GetPrivate(rig.Popup, "_onOk"), "'더 지킬래요'는 닫기만");
                Assert.IsNull(GetPrivate(rig.Popup, "_onCancel"), "덮개 · 뒤로 · X로 끝나도 할 일 없음 (머물기)");
                Assert.IsNull(GetPrivate(rig.Popup, "_onClosed"));
                var pressed = GetPrivate(rig.Popup, "_onCancelButton") as Action;
                Assert.IsNotNull(pressed, "끄기는 취소 버튼 알림(onCancelButton)에만");
                Assert.AreEqual(typeof(UIManager), pressed.Method.DeclaringType);
                Assert.AreEqual("Quit", pressed.Method.Name, "취소 버튼 = UIManager.Quit");

                int quits = 0;
                Action probe = () => quits++;

                // 열린 종료 확인에서 뒤로 한 번 더 = 닫힘 (머물기)
                SetPrivate(rig.Popup, "_onCancelButton", probe);
                ui.OnBack();
                Assert.IsFalse(rig.Popup.IsOpen, "뒤로 = 닫힘");
                Assert.AreEqual(0, quits, "뒤로는 끄지 않는다");
                Assert.AreEqual(MessagePopup.DefaultOkLabel, TextOf(rig.OkLabel), "닫히면 이름은 '확인' / '취소'로");
                Assert.AreEqual(MessagePopup.DefaultCancelLabel, TextOf(rig.CancelLabel));

                // 덮개 누름 (RequestDismiss) = 머물기
                ui.OnBack();
                Assert.IsTrue(rig.Popup.IsOpen);
                SetPrivate(rig.Popup, "_onCancelButton", probe);
                rig.Popup.RequestDismiss();
                Assert.IsFalse(rig.Popup.IsOpen);
                Assert.AreEqual(0, quits, "덮개 누름은 끄지 않는다");

                // '더 지킬래요' (확인 자리) = 머물기
                ui.OnBack();
                SetPrivate(rig.Popup, "_onCancelButton", probe);
                rig.Ok.Invoke();
                Assert.IsFalse(rig.Popup.IsOpen);
                Assert.AreEqual(0, quits, "'더 지킬래요'는 끄지 않는다");

                // '닫기' (취소 버튼) = 끈다 — 창을 닫은 뒤 한 번
                ui.OnBack();
                SetPrivate(rig.Popup, "_onCancelButton", probe);
                rig.Cancel.Invoke();
                Assert.IsFalse(rig.Popup.IsOpen);
                Assert.AreEqual(1, quits, "'닫기' 버튼만 끈다");
                Assert.IsFalse(ui.HasOpenPopup);
            }
            finally
            {
                instance.GetSetMethod(true).Invoke(null, new object[] { previous });
            }
        }

        // ═════════════════════════ (2) 토스트 × 빠른 아이콘 줄 ═════════════════════════

        [Test]
        public void Toast_MainWidth_ClearsTheRailCells()
        {
            Assert.AreEqual(128f, ToastView.RailInset, 0.01f, "줄 끝 12 + 칸 104 + 틈 12");
            float width = ToastView.MainWidth(1080f, true);
            Assert.AreEqual(824f, width, 0.01f);
            Assert.AreEqual(128f, (1080f - width) * 0.5f, 0.01f, "왼쪽 끝 x 128");
            Assert.AreEqual(952f, (1080f + width) * 0.5f, 0.01f, "오른쪽 끝 x 952");
            Assert.Greater((1080f - width) * 0.5f, UISize.RailEdge + UISize.RailCellWidth, "이벤트 칸(12–116)과 겹치지 않는다");
            Assert.AreEqual(UISize.ToastWidth, ToastView.MainWidth(1080f, false), 0.01f, "줄이 없으면 880 (100–980)");
            Assert.GreaterOrEqual(ToastView.MainWidth(500f, true), 440f, "아주 좁아도 440 아래로는 줄이지 않는다");
            Assert.LessOrEqual(ToastView.MainWidth(1600f, true), UISize.ToastWidth, "넓은 화면에서도 880을 넘지 않는다");
        }

        [Test]
        public void Toast_RailsShown_OnlyWhileTheSheetIsNotFullyOpen()
        {
            Assert.IsTrue(ToastView.RailsShown(0f, true), "MAIN + 켜진 칸");
            Assert.IsTrue(ToastView.RailsShown(0.5f, true), "시트가 움직이는 중 (줄이 아직 보인다)");
            Assert.IsFalse(ToastView.RailsShown(1f, true), "SHEET (다 열림) — 줄은 SheetFade로 사라졌다");
            Assert.IsFalse(ToastView.RailsShown(0f, false), "켜진 칸이 없다 (기능 잠김 · 메타 꺼짐)");
        }

        [Test]
        public void Toast_Place_InsetsOnlyWithoutAPopup()
        {
            var layer = new GameObject("ToastLayer", typeof(RectTransform));
            try
            {
                var area = (RectTransform)layer.transform;
                area.sizeDelta = new Vector2(1080f, 1920f);
                var go = new GameObject("Toast", typeof(RectTransform));
                go.transform.SetParent(area, false);
                var toast = go.AddComponent<ToastView>();
                var rect = (RectTransform)go.transform;
                bool rails = true;
                toast.RailsVisible = () => rails;

                toast.Place(null);
                AssertSpan(area, rect, 128f, 952f, "MAIN + 빠른 아이콘 줄");
                Assert.AreEqual(-UISize.ToastTop, rect.anchoredPosition.y, 0.01f, "높이는 그대로 (안전 영역 위에서 480)");

                rails = false;
                toast.Place(null);
                AssertSpan(area, rect, 100f, 980f, "SHEET (줄이 사라짐)");

                rails = true;
                var window = new GameObject("Window", typeof(RectTransform));
                window.transform.SetParent(area, false);
                ((RectTransform)window.transform).sizeDelta = new Vector2(900f, 600f);
                toast.Place((RectTransform)window.transform);
                AssertSpan(area, rect, 100f, 980f, "팝업 위 (창 위가 넉넉함 — 줄과 상관없이 880)");
            }
            finally
            {
                Object.DestroyImmediate(layer);
            }
        }

        /// <summary>
        /// 진짜 줄 찾기 (RailsVisible 덮어쓰기 없음): UIFrame.StageRect 아래 'Battle/MetaHud/QuickRail_L|_R' — 자리는 코치 대상 경로
        /// (CoachTargets.EventButton · JournalButton에서 'SafeArea/StageArea/'를 뗀 것)로 만든다. 줄 바로 아래 켜진 QuickIconView 칸만 센다
        /// </summary>
        [Test]
        public void Toast_Place_FindsTheQuickRailsUnderTheStage()
        {
            const string stagePrefix = "SafeArea/StageArea/";
            StringAssert.StartsWith(stagePrefix, CoachTargets.EventButton);
            StringAssert.StartsWith(stagePrefix, CoachTargets.JournalButton);
            var stageGo = new GameObject("StageArea", typeof(RectTransform));
            var layer = new GameObject("ToastLayer", typeof(RectTransform));
            try
            {
                var eventCell = MakePath(stageGo.transform, CoachTargets.EventButton.Substring(stagePrefix.Length));
                var journalCell = MakePath(stageGo.transform, CoachTargets.JournalButton.Substring(stagePrefix.Length));
                eventCell.gameObject.AddComponent<QuickIconView>();
                journalCell.gameObject.AddComponent<QuickIconView>();
                var leftRail = eventCell.parent;
                var hud = leftRail.parent;
                Assert.AreEqual("QuickRail_L", leftRail.name);
                Assert.AreEqual("QuickRail_R", journalCell.parent.name);
                Assert.AreSame(hud, journalCell.parent.parent, "두 줄은 같은 MetaHud 아래");
                // 아이콘 칸이 아닌 자식 (장식 등)은 세지 않는다
                MakePath(leftRail, "Backdrop");

                var area = (RectTransform)layer.transform;
                area.sizeDelta = new Vector2(1080f, 1920f);
                var go = new GameObject("Toast", typeof(RectTransform));
                go.transform.SetParent(area, false);
                var toast = go.AddComponent<ToastView>();
                var rect = (RectTransform)go.transform;
                Assert.IsNull(toast.RailsVisible, "덮어쓰기 없음 — 스스로 본다");

                UIFrame.Bind(null, (RectTransform)stageGo.transform);
                toast.Place(null);
                AssertSpan(area, rect, 128f, 952f, "MAIN — 두 줄에 켜진 칸");

                journalCell.gameObject.SetActive(false);
                toast.Place(null);
                AssertSpan(area, rect, 128f, 952f, "왼쪽 줄(이벤트)만");

                eventCell.gameObject.SetActive(false);
                journalCell.gameObject.SetActive(true);
                toast.Place(null);
                AssertSpan(area, rect, 128f, 952f, "오른쪽 줄(수선 일지)만");

                journalCell.gameObject.SetActive(false);
                toast.Place(null);
                AssertSpan(area, rect, 100f, 980f, "켜진 아이콘 칸 없음 (장식 자식은 세지 않는다)");

                eventCell.gameObject.SetActive(true);
                hud.gameObject.SetActive(false);
                toast.Place(null);
                AssertSpan(area, rect, 100f, 980f, "메타 HUD가 꺼짐");

                hud.gameObject.SetActive(true);
                UIFrame.SetProgress(1f, true);
                toast.Place(null);
                AssertSpan(area, rect, 100f, 980f, "SHEET (다 열림)");

                UIFrame.SetProgress(0.5f, false);
                toast.Place(null);
                AssertSpan(area, rect, 128f, 952f, "시트가 움직이는 중 (줄이 아직 보인다)");

                UIFrame.Unbind();
                toast.Place(null);
                AssertSpan(area, rect, 100f, 980f, "StageArea가 없는 장면");
            }
            finally
            {
                UIFrame.Unbind();
                Object.DestroyImmediate(layer);
                Object.DestroyImmediate(stageGo);
            }
        }

        /// <summary>'a/b/c' 경로대로 자식을 만든다 (있으면 그대로 쓴다) — 마지막 것을 돌려준다</summary>
        static Transform MakePath(Transform root, string path)
        {
            var node = root;
            foreach (var name in path.Split('/'))
            {
                var next = node.Find(name);
                if (next == null)
                {
                    next = new GameObject(name, typeof(RectTransform)).transform;
                    next.SetParent(node, false);
                }
                node = next;
            }
            return node;
        }

        static void AssertSpan(RectTransform area, RectTransform toast, float left, float right, string what)
        {
            var box = UIFind.RectIn(toast, area, new Vector3[4]);
            Assert.AreEqual(left, box.xMin - area.rect.xMin, 0.5f, what + ": 왼쪽 끝");
            Assert.AreEqual(right, box.xMax - area.rect.xMin, 0.5f, what + ": 오른쪽 끝");
        }

        // ═════════════════════════ (3) 10레벨 고비 칩 ═════════════════════════

        // 강화 카드 (MenuSceneBuilder.BuildGrowthPanel): ShopCard Top(96, 210, 24, 24) — 이 층 좌표로 x 24–1056, 아래 1000 · 위 1210 (y 위로).
        // LevelText TopLeft (250, −24) 750x50 · StatText (250, −78) 750x42 · HpText (250, −124) 750x38, 모두 왼쪽 정렬.
        // 강화 버튼 줄 Buttons Top(322, 150, 24, 24) — 패널 위 끝 1306(카드 위 + 96)에서 322 아래 → y 834–984 (카드 아래 끝 1000과 16 틈).
        static readonly Rect Card = new(24f, 1000f, 1032f, 210f);
        static readonly Rect GrowthButtons = new(24f, 834f, 1032f, 150f);
        /// <summary>'수선점 Lv.10' 38 굵게 ≈ 230 폭 (글 칸 274–1024 × 1136–1186의 왼쪽)</summary>
        static readonly Rect LevelInk = new(274f, 1136f, 230f, 50f);
        /// <summary>'초당 단추 1.85 → 1.96' 30 ≈ 290 폭</summary>
        static readonly Rect StatInk = new(274f, 1090f, 290f, 42f);
        /// <summary>'수선점 내구도 426 → 444' 26 ≈ 280 폭 (줄 가운데 1067)</summary>
        static readonly Rect HpInk = new(274f, 1048f, 280f, 38f);
        /// <summary>칩 높이 (TutorialSceneBuilder.BuildLevelUpFanfare — 밤 알약 600x66) · 원래 폭</summary>
        const float ChipHeight = 66f;
        const float ChipBaseWidth = 600f;
        /// <summary>칩 글 자동 크기 최소 / 최대 (TutorialSceneBuilder Pill: 26의 70% = 18.2 ~ 26)</summary>
        const float ChipShrink = 0.7f;

        static float InkRight => Mathf.Max(LevelInk.xMax, Mathf.Max(StatInk.xMax, HpInk.xMax));

        static LevelUpFanfareView.ChipPlace Place(float inkRight, float textWidth)
        {
            float width = LevelUpFanfareView.FitChipWidth(textWidth, ChipBaseWidth);
            float narrowest = LevelUpFanfareView.NarrowChipWidth(textWidth, ChipShrink, width);
            return LevelUpFanfareView.PlaceMilestoneChip(Card, inkRight, HpInk.center.y, width, narrowest, ChipHeight);
        }

        static Rect PathOf(LevelUpFanfareView.ChipPlace place) =>
            LevelUpFanfareView.ChipPath(place.Center, new Vector2(place.Width, ChipHeight), place.Rise);

        [Test]
        public void MilestoneChip_BesideTheCardText_NeverOverTheLevel()
        {
            // '10레벨 동안 내구도 +126' 26 ≈ 280 폭 → 칩 344
            var place = Place(InkRight, 280f);
            var path = PathOf(place);

            Assert.AreEqual(344f, place.Width, 0.01f, "글에 맞춘 폭 그대로 (틈이 넉넉하다)");
            Assert.AreEqual(LevelUpFanfareView.ChipRise, place.Rise, 0.01f, "떠오른다");
            Assert.AreEqual(HpInk.center.y, place.Center.y, 0.01f, "내구도 줄 높이");
            Assert.GreaterOrEqual(path.xMin, InkRight + 20f, "카드 글 끝 오른쪽 (틈 24)");
            Assert.LessOrEqual(path.xMax, Card.xMax - 16f + 0.01f, "카드 안");
            Assert.IsFalse(path.Overlaps(LevelInk), "60 떠오르는 동안에도 'Lv.10'을 가리지 않는다");
            Assert.IsFalse(path.Overlaps(StatInk), "초당 줄도");
            Assert.IsFalse(path.Overlaps(HpInk), "내구도 줄도");
            Assert.IsFalse(path.Overlaps(GrowthButtons), "강화 버튼 줄도");
            // 처음 0.2초 OutBack(≈1.1배)으로 커져도 글에 닿지 않는다
            Assert.Greater(path.xMin - place.Width * 0.05f, InkRight);
        }

        [Test]
        public void MilestoneChip_OldPlacements_CoveredTheLevelOrTheButtons()
        {
            // 예전: 내구도 글 칸 가운데 + (60, 34), 폭 600 — Wave 3 r1 35_focus_hold ('Lv.10'이 가려짐). 이 시험의 자리 값이 맞는지도 함께 본다
            var hpCell = new Rect(274f, 1048f, 750f, 38f);
            var old = LevelUpFanfareView.ChipPath(hpCell.center + new Vector2(60f, 34f), new Vector2(ChipBaseWidth, ChipHeight));
            Assert.IsTrue(old.Overlaps(LevelInk), "예전 자리는 레벨 글을 덮었다");
            // RM-2 KIT 첫 판의 '자리가 없으면 카드 아래' (위 끝 = 카드 아래 − 16): 강화 x1 · x10 · 최대 버튼 위였다
            var below = LevelUpFanfareView.ChipPath(new Vector2(Card.center.x, Card.yMin - 16f - ChipHeight * 0.5f), new Vector2(344f, ChipHeight));
            Assert.IsTrue(below.Overlaps(GrowthButtons), "카드 아래 자리는 강화 버튼 줄을 덮었다");
        }

        [Test]
        public void MilestoneChip_TightRoom_NarrowsBesideTheText()
        {
            // 초당 줄이 길어 (474 폭 → 끝 748) 글 끝 + 24 ~ 1040 틈이 268: 글에 맞춘 344는 넘치고 가장 좁은 260(글 18.2)은 들어간다
            var longStat = new Rect(274f, 1090f, 474f, 42f);
            var place = Place(longStat.xMax, 280f);
            var path = PathOf(place);
            Assert.AreEqual(268f, place.Width, 0.01f, "틈만큼 좁힌다");
            Assert.AreEqual(LevelUpFanfareView.ChipRise, place.Rise, 0.01f, "글 오른쪽이라 그대로 떠오른다");
            Assert.AreEqual(longStat.xMax + 24f, path.xMin, 0.01f);
            Assert.LessOrEqual(path.xMax, Card.xMax - 16f + 0.01f, "카드 안");
            Assert.IsFalse(path.Overlaps(longStat), "초당 줄을 가리지 않는다");
            Assert.IsFalse(path.Overlaps(LevelInk), "'Lv.10'도");
            Assert.IsFalse(path.Overlaps(GrowthButtons), "강화 버튼 줄도");
        }

        [Test]
        public void MilestoneChip_NoRoomBeside_StaysInTheCardWithoutRising()
        {
            float narrowest = LevelUpFanfareView.NarrowChipWidth(280f, ChipShrink, LevelUpFanfareView.FitChipWidth(280f, ChipBaseWidth));
            var longLevel = new Rect(274f, 1136f, 700f, 50f);
            foreach (float ink in new[] { longLevel.xMax, 1100f, float.NegativeInfinity, float.NaN })
            {
                var place = Place(ink, 280f);
                var path = PathOf(place);
                Assert.AreEqual(0f, place.Rise, 0.01f, $"ink {ink}: 떠오르지 않는다");
                Assert.AreEqual(narrowest, place.Width, 0.01f, $"ink {ink}: 가장 좁게 (가리는 글을 줄인다)");
                Assert.AreEqual(HpInk.center.y, place.Center.y, 0.01f, $"ink {ink}: 내구도 줄 높이");
                Assert.AreEqual(Card.xMax - 16f, path.xMax, 0.01f, $"ink {ink}: 카드 오른쪽 끝");
                Assert.GreaterOrEqual(path.yMin, Card.yMin, $"ink {ink}: 카드 아래 끝 위");
                Assert.IsFalse(path.Overlaps(GrowthButtons), $"ink {ink}: 강화 버튼 줄 위에는 오지 않는다");
                Assert.LessOrEqual(path.yMax, LevelInk.yMin, $"ink {ink}: 레벨 줄 높이에 닿지 않는다");
                Assert.IsFalse(path.Overlaps(longLevel), $"ink {ink}: 긴 레벨 글 위에도 오지 않는다");
            }
        }

        [Test]
        public void MilestoneChip_AnyInkAndText_NeverOverTheLevelOrTheButtons()
        {
            // 레벨 글 끝(504)부터 카드 밖까지 글 끝 · 짧은 글부터 원래 폭을 넘는 글까지 — 언제나 카드 안 · 'Lv.10' 밖 · 버튼 줄 밖
            foreach (float text in new[] { 120f, 280f, 420f, 700f })
            {
                for (float ink = LevelInk.xMax; ink <= 1100f; ink += 7f)
                {
                    var place = Place(ink, text);
                    var path = PathOf(place);
                    string what = $"text {text} ink {ink}";
                    Assert.Greater(place.Width, 0f, what);
                    Assert.GreaterOrEqual(path.xMin, Card.xMin + 16f - 0.01f, what + ": 카드 왼쪽 안");
                    Assert.LessOrEqual(path.xMax, Card.xMax - 16f + 0.01f, what + ": 카드 오른쪽 안");
                    Assert.GreaterOrEqual(path.yMin, Card.yMin, what + ": 카드 아래 끝 위");
                    Assert.LessOrEqual(path.yMax, Card.yMax, what + ": 카드 위 끝 아래");
                    Assert.IsFalse(path.Overlaps(LevelInk), what + ": 'Lv.10'");
                    Assert.IsFalse(path.Overlaps(GrowthButtons), what + ": 강화 버튼 줄");
                    if (place.Rise > 0f) Assert.GreaterOrEqual(path.xMin, ink + 24f - 0.01f, what + ": 떠오르는 칩은 글 끝 오른쪽");
                }
            }
        }

        [Test]
        public void MilestoneChip_FitWidth_IsClamped()
        {
            Assert.AreEqual(344f, LevelUpFanfareView.FitChipWidth(280f, ChipBaseWidth), 0.01f, "글 + 좌우 32");
            Assert.AreEqual(240f, LevelUpFanfareView.FitChipWidth(20f, ChipBaseWidth), 0.01f, "최소 240");
            Assert.AreEqual(ChipBaseWidth, LevelUpFanfareView.FitChipWidth(900f, ChipBaseWidth), 0.01f, "원래 폭까지");
            Assert.AreEqual(200f, LevelUpFanfareView.FitChipWidth(20f, 200f), 0.01f, "원래 폭이 240보다 좁으면 원래 폭");
            Assert.AreEqual(260f, LevelUpFanfareView.NarrowChipWidth(280f, ChipShrink, 344f), 0.01f, "글 × 0.7 + 좌우 32");
            Assert.AreEqual(344f, LevelUpFanfareView.NarrowChipWidth(280f, 1f, 344f), 0.01f, "자동 크기가 아니면 줄지 않는다");
            Assert.AreEqual(344f, LevelUpFanfareView.NarrowChipWidth(280f, 1.5f, 344f), 0.01f, "비율은 1까지");
            Assert.AreEqual(ChipBaseWidth, LevelUpFanfareView.NarrowChipWidth(900f, ChipShrink, ChipBaseWidth), 0.01f, "원래 폭까지");
            Assert.AreEqual(60f, LevelUpFanfareView.ChipRise, 0.01f, "떠오름 (StepChips)");
            var size = new Vector2(300f, ChipHeight);
            Assert.AreEqual(ChipHeight + 60f, LevelUpFanfareView.ChipPath(Vector2.zero, size).height, 0.01f, "길 = 칩 + 떠오름");
            Assert.AreEqual(ChipHeight, LevelUpFanfareView.ChipPath(Vector2.zero, size, 0f).height, 0.01f, "떠오르지 않으면 칩 그대로");
        }

        // ═════════════════════════ (4) 수선권 '+N' × '가득!' ═════════════════════════

        // 수선 바구니 위젯 (MetaSceneBuilder.BuildIdleChestWidget): TopRight (−24, −120) 150x170 — 1080 폭에서 x 906–1056.
        // Basket TopCenter 136x128 · FullTag '가득!' Bottom(15, 34, 22, 22) · TimePill BottomCenter (0, 16) 104x32.
        static readonly Rect Widget = new(906f, 1500f, 150f, 170f);
        static readonly Rect BasketArt = new(Widget.center.x - 68f, Widget.yMax - 128f, 136f, 128f);
        static readonly Rect FullTag = new(Widget.xMin + 22f, Widget.yMin + 15f, Widget.width - 44f, 34f);
        static readonly Rect TimePill = new(Widget.center.x - 52f, Widget.yMin + 16f, 104f, 32f);
        /// <summary>'+N' 받침 높이 (RewardFlyer LabelHeight)</summary>
        const float PillHeight = 40f;

        [Test]
        public void TicketPill_BesideBasket_ClearOfFullTagAndTime()
        {
            var anchor = RewardFlyer.BesidePlateAnchor(BasketArt, 540f, out float side);
            Assert.AreEqual(-1f, side, "오른쪽 위젯 → 화면 가운데(왼쪽)로 자란다");
            foreach (float width in new[] { 72f, 120f, 200f })
            {
                float centre = RewardFlyer.LabelCenterX(anchor.x, side, width);
                var pill = new Rect(centre - width * 0.5f, anchor.y - PillHeight * 0.5f, width, PillHeight);
                Assert.IsFalse(pill.Overlaps(FullTag), $"폭 {width}: '가득!' 꼬리표를 덮지 않는다");
                Assert.IsFalse(pill.Overlaps(TimePill), $"폭 {width}: 시간 알약을 덮지 않는다");
                Assert.IsFalse(pill.Overlaps(BasketArt), $"폭 {width}: 바구니 그림을 덮지 않는다");
                Assert.GreaterOrEqual(pill.yMin, FullTag.yMax, $"폭 {width}: '가득!'보다 위 높이");
            }
        }
    }
}
