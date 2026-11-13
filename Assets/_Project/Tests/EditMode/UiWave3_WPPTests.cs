using System;
using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-P (UX Wave 3 — Wave 2 다듬기) 순수 규칙 — 장면 없이 도우미 함수만 본다.
    ///   · (a) 고른 옆 탭 이름 = Cream (금색 아님), 흑백으로도 고른 탭이 더 밝다. 읽힘은 어두운 외곽선이 맡는다 — 글자 면 ↔ 외곽선 ≥4.5:1,
    ///         외곽선 ↔ 보라 받침 ≥3:1 (받침 위 Cream 면만으로는 ≈2.8:1 — 외곽선 머티리얼이 꼭 있어야 한다).
    ///   · (b) '초당 +N' 비켜 주기 (단추가 떠 있는 동안 0, 0이 된 뒤 1.1초 지나 되돌림, 움직임 줄이기면 바로) ·
    ///         빠른 아이콘 칸 "+N" 받침은 메달 판 옆 (이름 알약 위로 내려가지 않는다).
    ///   · (c) 종료 확인 이름표 ('더 지킬래요'가 주 버튼 · '닫기'). 취소 버튼 누름에서만 닫는 규칙은 UiKitCarryTests (ConfirmOpts.onCancelButton).
    ///   · (d) 흐린 최대 버튼: 금 바탕 그림만 α0.65 · 글자 α1 — 그리는 순서대로 섞어 흐린 금 위 Ink '최대' ≥3:1.
    ///         최대 레벨에서는 몇 번을 다시 그려도 최대 버튼을 누를 수 없다 (진짜 Button · CostButtonView, 리플렉션).
    ///   · (e) 여러 낱말 능력치 이름 + 수치는 줄바꿈 없는 빈칸으로 한 덩어리.
    ///   · (g) MAIN 배지 예산: 빠른 아이콘이 이긴 NEW는 시트 밑에서, 그리고 줄 안 받을 것이 2개 이상이면 쉰다
    ///         (받을 것 점 · 숫자는 줄이지 않는다 — 셋이 켜지면 묶음 표시도 셋).
    ///   · (h) 잠금 덮개 표준 NEW 알약 글자 ≥16. (i) '무료'는 한 번만 (리본 · 빨간 알약).
    /// </summary>
    public class UiWave3_WPPTests
    {
        /// <summary>글자 외곽선 머티리얼 색 (Editor UiArt.Outline — Generated/UiTextOutline.mat, 너비 0.22)</summary>
        static readonly Color TextOutline = new(0.16f, 0.08f, 0.24f, 1f);
        /// <summary>고른 탭 받침 ui_tab_selected의 이름 자리 보라 — 평균 (Wave 2 r2 z15 ≈179,130,180) · 가장 어두운 곳 (그림 ≈145,103,193)</summary>
        static readonly Color PlateLilac = new(179f / 255f, 130f / 255f, 180f / 255f, 1f);
        static readonly Color PlateLilacDark = new(145f / 255f, 103f / 255f, 193f / 255f, 1f);
        /// <summary>받침 아래 가운데 따뜻한 빛 (ui_tab_selected ≈252,214,159 — 이름 글자 바로 밑)</summary>
        static readonly Color PlateGlow = new(252f / 255f, 214f / 255f, 159f / 255f, 1f);

        // ───────────── (a) 고른 탭 이름 ─────────────

        [Test]
        public void SelectedTabLabel_IsCreamNotGold()
        {
            var selected = MainTabController.SideLabelColor(true);
            Assert.AreEqual(UITheme.Cream, selected, "고른 탭 이름은 Cream");
            Assert.AreNotEqual(UITheme.Gold, selected, "금색은 막대 · 빛에만");
            Assert.AreEqual(UITheme.SubText, MainTabController.SideLabelColor(false));
        }

        /// <summary>
        /// 외곽선 글자 모형만 본다 (받침 위 글자 면 대비가 아니다): 글자 면 ↔ 외곽선 ≥4.5:1, 외곽선 ↔ 받침(보라 · 아래 빛) ≥3:1.
        /// 받침 위 Cream 면만으로는 ≈2.8:1 (빛 위는 ≈1.3:1)이라 외곽선 머티리얼이 읽힘을 맡는다 — 받침 틴트 0.75(IdleSceneBuilder) 요청 ·
        /// Verify가 받침 위 실제 화소로 잰다.
        /// </summary>
        [Test]
        public void SelectedTabLabel_OutlineCarriesContrast_OutlineOnlyCheck()
        {
            var selected = MainTabController.SideLabelColor(true);
            Assert.GreaterOrEqual(ReturnPopup.Contrast(selected, TextOutline), 4.5f, "글자 면 ↔ 외곽선 (외곽선만 보는 시험)");
            Assert.GreaterOrEqual(ReturnPopup.Contrast(TextOutline, PlateLilac), 3f, "외곽선 ↔ 보라 받침");
            Assert.GreaterOrEqual(ReturnPopup.Contrast(TextOutline, PlateLilacDark), 3f, "외곽선 ↔ 가장 어두운 보라");
            Assert.GreaterOrEqual(ReturnPopup.Contrast(TextOutline, PlateGlow), 3f, "외곽선 ↔ 아래 빛");
        }

        [Test]
        public void SelectedTabLabel_BrighterThanIdleInGrayscale()
        {
            float on = Gray(MainTabController.SideLabelColor(true));
            float off = Gray(MainTabController.SideLabelColor(false));
            Assert.Greater(on - off, 0.1f, "흑백에서도 고른 탭 이름이 더 밝다");
        }

        // ───────────── (b) '초당 +N' 비켜 주기 · 빠른 아이콘 받침 ─────────────

        [Test]
        public void PerSec_HiddenWhilePending_RestoredAfterDelay()
        {
            Assert.AreEqual(0f, TopBarView.PerSecTarget(true, 0f));
            Assert.AreEqual(0f, TopBarView.PerSecTarget(false, 0.5f), "받침이 아직 남아 있다");
            Assert.AreEqual(0f, TopBarView.PerSecTarget(false, TopBarView.PerSecRestoreDelay - 0.01f));
            Assert.AreEqual(1f, TopBarView.PerSecTarget(false, TopBarView.PerSecRestoreDelay));
            Assert.GreaterOrEqual(TopBarView.PerSecRestoreDelay, 0.75f + 0.3f, "받침 머묾 0.75 + 사라짐 0.3 뒤");
        }

        [Test]
        public void PerSec_FadesOverTokenTime_SnapsWhenReduced()
        {
            float half = TopBarView.PerSecFadeTime * 0.5f;
            Assert.AreEqual(0.5f, TopBarView.PerSecAlphaStep(1f, true, 0f, half, false), 1e-4f, "0.12초에 1 → 0");
            Assert.AreEqual(0f, TopBarView.PerSecAlphaStep(1f, true, 0f, 0.016f, true), "움직임 줄이기면 바로");
            Assert.AreEqual(1f, TopBarView.PerSecAlphaStep(0f, false, 5f, 0.016f, true));
            Assert.AreEqual(0f, TopBarView.PerSecAlphaStep(0f, false, 0.2f, 0.016f, false), "기다리는 동안은 그대로 0");
        }

        [Test]
        public void RailPill_BesidePlate_TowardScreenCentre()
        {
            const float screenCentre = 540f;
            // 왼쪽 줄 칸 (104x116, 가운데 x 80)
            var left = new Rect(28f, 1000f, UISize.RailCellWidth, UISize.RailCellHeight);
            var anchor = RewardFlyer.RailLabelAnchor(left, screenCentre, out float side);
            Assert.AreEqual(1f, side, "왼쪽 줄은 오른쪽(화면 가운데)으로");
            Assert.AreEqual(left.center.x + UISize.RailPlate * 0.5f + 10f, anchor.x, 0.01f, "판 끝에서 10 띄운 가까운 끝");
            Assert.AreEqual(left.yMax - UISize.RailPlate * 0.5f, anchor.y, 0.01f, "판 가운데 높이");

            float width = 96f;
            float centre = RewardFlyer.LabelCenterX(anchor.x, side, width);
            Assert.GreaterOrEqual(centre - width * 0.5f, left.center.x + UISize.RailPlate * 0.5f, "받침이 판을 덮지 않는다");
            float pillBottom = anchor.y - 20f;
            Assert.Greater(pillBottom, left.yMin + UISize.RailLabelHeight, "받침이 이름 알약('이벤트')까지 내려가지 않는다");

            // 오른쪽 줄 칸
            var right = new Rect(1080f - 28f - UISize.RailCellWidth, 1000f, UISize.RailCellWidth, UISize.RailCellHeight);
            var rightAnchor = RewardFlyer.RailLabelAnchor(right, screenCentre, out float rightSide);
            Assert.AreEqual(-1f, rightSide, "오른쪽 줄은 왼쪽으로");
            Assert.LessOrEqual(RewardFlyer.LabelCenterX(rightAnchor.x, rightSide, width) + width * 0.5f,
                right.center.x - UISize.RailPlate * 0.5f, "오른쪽 줄 받침도 판을 덮지 않는다");
        }

        [Test]
        public void LabelCenter_CentredAnchorUnchanged()
        {
            Assert.AreEqual(300f, RewardFlyer.LabelCenterX(300f, 0f, 120f), "칩 아래 받침은 가운데 기준 그대로");
        }

        // ───────────── (c) 종료 확인 ─────────────

        /// <summary>
        /// 이름표만 본다. '닫기'가 취소 버튼 누름에서만 오는 규칙은 ConfirmOpts.onCancelButton으로 옮겨 UiKitCarryTests가 고정한다
        /// (KIT R1 — 예전 프레임 비교 도우미는 이 시험이 더 부르지 않는다)
        /// </summary>
        [Test]
        public void ExitConfirm_StayIsPrimary_Labels()
        {
            Assert.AreEqual("더 지킬래요", UIManager.ExitCancelLabel);
            Assert.AreEqual("닫기", UIManager.ExitOkLabel);
        }

        // ───────────── (d) 흐린 최대 버튼 ─────────────

        /// <summary>강화 시트 누빔 바탕 (Wave 2 r2 z_tall_max — 평균 ≈34,25,70 · 가장 어두운 솔기 ≈13,5,26)</summary>
        static readonly Color Quilt = new(34f / 255f, 25f / 255f, 70f / 255f, 1f);
        static readonly Color QuiltSeam = new(13f / 255f, 5f / 255f, 26f / 255f, 1f);

        [Test]
        public void MaxButtonDim_LabelStaysReadable()
        {
            Assert.AreEqual(1f, GrowthPanel.MaxSkinAlpha(false));
            float alpha = GrowthPanel.MaxSkinAlpha(true);
            Assert.AreEqual(GrowthPanel.MaxDimAlpha, alpha);
            Assert.AreEqual(0.65f, alpha, 1e-4f);
            // '최대' 줄 밑 금 그림의 가장 어두운 곳 (button_gold_9s 가운데 ≈237,190,97 — 위로 갈수록 밝다)
            var gold = new Color(237f / 255f, 190f / 255f, 97f / 255f, 1f);
            foreach (var backdrop in new[] { UITheme.Background, UITheme.Panel, Quilt, QuiltSeam })
            {
                // 그리는 순서대로: 바탕 위에 α로 흐린 금, 그 위에 α1 Ink 글자 (글자는 흐리지 않는다 — 묶음 CanvasGroup α0.6은 ≈2:1이었다)
                var plate = Color.Lerp(backdrop, gold, alpha);
                Assert.GreaterOrEqual(ReturnPopup.Contrast(UITheme.Ink, plate), 3f, $"흐린 금 위 '최대' ≥3:1 (바탕 {backdrop})");
                Assert.GreaterOrEqual(ReturnPopup.Contrast(gold, plate), 1.8f, $"밝은 금과 흐린 금이 구별된다 (바탕 {backdrop})");
            }
        }

        [Test]
        public void MaxLevel_MaxButtonStaysOff_OnEveryRefresh()
        {
            foreach (bool dimmedBefore in new[] { false, true })
            {
                var go = NewMaxButton(out var button, out var skin, out var view, out var sprite);
                try
                {
                    var swap = Transition(button, "SpriteSwap");
                    // 레벨 안 (RefreshMax): 늘 넉넉함 + 흐림 켜기 · 끄기
                    view.SetState(CostButtonView.State.Affordable);
                    CallGrowth("ApplyMaxDim", button, swap, dimmedBefore);
                    Assert.AreEqual(!dimmedBefore, (bool)Prop(button, "interactable"));
                    Assert.AreEqual(GrowthPanel.MaxSkinAlpha(dimmedBefore), ((Color)Prop(skin, "color")).a, 1e-4f, "흐림은 바탕 그림 알파로");

                    // 최대 레벨 (RefreshShop 최대 갈래 — 켜질 때 · 스테이지를 깰 때 · 가방이 바뀔 때마다 다시 온다)
                    for (int i = 1; i <= 3; i++)
                    {
                        CallGrowth("ShowMaxLevel", button, swap, view);
                        string when = $"{(dimmedBefore ? "흐린 채" : "누를 수 있는 채")} 최대 레벨, {i}번째 그리기";
                        Assert.IsFalse((bool)Prop(button, "interactable"), when + " — 누를 수 없다");
                        Assert.AreEqual(swap, Prop(button, "transition"), when + " — 전환을 되돌려 x1 · x10과 같은 비활성 그림");
                        Assert.AreEqual(1f, ((Color)Prop(skin, "color")).a, 1e-4f, when + " — 흐림을 걷고 '보통' 색을 기억한다");
                        Assert.AreEqual(CostButtonView.State.Unavailable, view.CurrentState, when);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    UnityEngine.Object.DestroyImmediate(sprite);
                }
            }
        }

        [Test]
        public void MaxDim_ReleaseRestoresPressAndTransition()
        {
            var go = NewMaxButton(out var button, out var skin, out var view, out var sprite);
            try
            {
                var swap = Transition(button, "SpriteSwap");
                view.SetState(CostButtonView.State.Affordable);
                CallGrowth("ApplyMaxDim", button, swap, true);
                Assert.AreEqual(Transition(button, "None"), Prop(button, "transition"), "흐린 동안 비활성 그림으로 바뀌지 않게");
                CallGrowth("ApplyMaxDim", button, swap, false);
                Assert.IsTrue((bool)Prop(button, "interactable"));
                Assert.AreEqual(swap, Prop(button, "transition"));
                Assert.AreEqual(1f, ((Color)Prop(skin, "color")).a, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(sprite);
            }
        }

        // 시험 어셈블리는 UnityEngine.UI를 참조하지 않는다 — Button · Image 형식은 GrowthPanel · CostButtonView 직렬화 필드에서 얻고 리플렉션으로 만진다

        const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>장면의 LevelUpMaxButton처럼: Image(바탕) + Button(스프라이트 교체, 비활성 그림 있음) + CostButtonView</summary>
        static GameObject NewMaxButton(out Component button, out Component skin, out CostButtonView view, out Sprite disabled)
        {
            var buttonType = typeof(GrowthPanel).GetField("_levelUpMaxButton", Fields).FieldType;
            var imageType = typeof(CostButtonView).GetField("_skin", Fields).FieldType;
            var go = new GameObject("LevelUpMaxButton", typeof(RectTransform));
            skin = go.AddComponent(imageType);
            button = go.AddComponent(buttonType);
            SetProp(button, "targetGraphic", skin);
            disabled = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            var stateProp = buttonType.GetProperty("spriteState");
            object spriteState = stateProp.GetValue(button);
            spriteState.GetType().GetProperty("disabledSprite").SetValue(spriteState, disabled);
            stateProp.SetValue(button, spriteState);
            SetProp(button, "transition", Transition(button, "SpriteSwap"));
            view = go.AddComponent<CostButtonView>();
            return go;
        }

        static object Transition(Component button, string name) => Enum.Parse(button.GetType().GetProperty("transition").PropertyType, name);

        static object Prop(Component component, string name) => component.GetType().GetProperty(name).GetValue(component);

        static void SetProp(Component component, string name, object value) => component.GetType().GetProperty(name).SetValue(component, value);

        static void CallGrowth(string method, params object[] args)
        {
            var info = typeof(GrowthPanel).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(info, $"GrowthPanel.{method}");
            info.Invoke(null, args);
        }

        // ───────────── (e) 능력치 이름 묶기 ─────────────

        [Test]
        public void KeepStatNumbers_BindsKnownMultiWordLabels()
        {
            string nb = UIFormat.Nbsp;
            Assert.AreEqual("스킬" + nb + "쿨타임" + nb + "-8%", SynergyUI.KeepStatNumbers("스킬 쿨타임 -8%"));
            Assert.AreEqual("공격력" + nb + "+15%, 공격" + nb + "속도" + nb + "+10%", SynergyUI.KeepStatNumbers("공격력 +15%, 공격 속도 +10%"),
                "쉼표 뒤 빈칸은 그대로 (줄은 능력치 사이에서 바뀐다)");
            Assert.AreEqual("치명타" + nb + "피해" + nb + "+20% · 치명타" + nb + "확률" + nb + "+5%",
                SynergyUI.KeepStatNumbers("치명타 피해 +20% · 치명타 확률 +5%"));
            Assert.AreEqual("유지" + nb + "시간" + nb + "+15%", SynergyUI.KeepStatNumbers("유지 시간 +15%"));
        }

        [Test]
        public void KeepStatNumbers_LeavesLabelsWithoutNumbersAndPartialWords()
        {
            const string prose = "공격 속도가 빨라져요";
            Assert.AreSame(prose, SynergyUI.KeepStatNumbers(prose), "수치가 없으면 그대로 (할당 없음)");
            string nb = UIFormat.Nbsp;
            Assert.AreEqual("반공격 속도" + nb + "+5%", SynergyUI.KeepStatNumbers("반공격 속도 +5%"), "낱말 가운데서 시작하는 이름은 묶지 않는다");
        }

        [Test]
        public void KeepStatNumbers_EveryModifierLineIsOneUnit()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                string line = SynergySystem.DescribeModifier(stat, 5f);
                string kept = SynergyUI.KeepStatNumbers(line);
                Assert.IsFalse(kept.Contains(" "), $"{stat}: '{line}' 안에 줄이 바뀔 빈칸이 남았다");
            }
        }

        [Test]
        public void MultiWordStatLabels_LongestFirst()
        {
            var labels = SynergyUI.MultiWordStatLabels;
            CollectionAssert.Contains(labels, "스킬 쿨타임");
            CollectionAssert.Contains(labels, "유지 시간");
            for (int i = 1; i < labels.Length; i++) Assert.GreaterOrEqual(labels[i - 1].Length, labels[i].Length, "긴 이름부터");
        }

        // ───────────── (g) MAIN 배지 예산 ─────────────

        [Test]
        public void RailNew_RestsUnderSheet_WithoutPassingOn()
        {
            Assert.AreEqual(2, MainBadgeBudget.MaxRailMarks);
            Assert.IsTrue(MainBadgeBudget.WinnerShows(MainBadgeRole.Rail, true, 0, 0, false),
                "시트 밑 빠른 아이콘이 이기면 그대로 (줄이 SheetFade 0 — 화면에 NEW 없음, x1로 넘기지 않는다)");
            Assert.IsTrue(MainBadgeBudget.WinnerShows(MainBadgeRole.Rail, false, 1, 0, true), "묶음이 자리를 얻었으면 줄 NEW도");
            Assert.IsFalse(MainBadgeBudget.WinnerShows(MainBadgeRole.Rail, false, 2, 3, true),
                "줄 안 받을 것이 둘 이상이면 줄 NEW는 쉰다 (받을 것 점 · 숫자는 그대로 — 셋이면 묶음 표시도 셋)");
            Assert.IsFalse(MainBadgeBudget.WinnerShows(MainBadgeRole.Rail, false, 3, 3, true));
            Assert.IsFalse(MainBadgeBudget.WinnerShows(MainBadgeRole.Rail, true, 2, 0, false));
        }

        [Test]
        public void OtherNew_NeedsSeatInMain()
        {
            Assert.IsTrue(MainBadgeBudget.WinnerShows(MainBadgeRole.None, true, 0, 0, false), "시트가 열리면 잠금 덮개 NEW는 자리와 상관없이");
            Assert.IsFalse(MainBadgeBudget.WinnerShows(MainBadgeRole.None, false, 0, 0, false), "MAIN에 자리가 없으면 쉰다");
            Assert.IsTrue(MainBadgeBudget.WinnerShows(MainBadgeRole.Tab, false, 0, 1, false));
            Assert.IsFalse(MainBadgeBudget.WinnerShows(MainBadgeRole.Tab, false, 0, 0, true), "줄 묶음 자리는 줄 NEW만 쓴다");
        }

        // ───────────── (h) · (i) NEW 알약 · '무료' 한 번 ─────────────

        [Test]
        public void StandardNewPill_TextAtOrAboveFloor()
        {
            Assert.GreaterOrEqual(UISize.BadgeNewText, 16f, "잠금 덮개도 표준 NEW 알약 (자동 크기 바닥 16)");
        }

        [Test]
        public void QuickIdleLabel_FreeSaidOnceByRibbon()
        {
            Assert.AreEqual(IdleChestPopup.QuickState.Free, IdleChestPopup.QuickStateOf(3, 0d));
            Assert.AreEqual(IdleChestPopup.QuickState.Paid, IdleChestPopup.QuickStateOf(2, 50d));
            Assert.AreEqual(IdleChestPopup.QuickState.UsedUp, IdleChestPopup.QuickStateOf(0, 0d));
            Assert.AreEqual(IdleChestPopup.QuickState.UsedUp, IdleChestPopup.QuickStateOf(2, -1d));
            string free = IdleChestPopup.QuickCostText(IdleChestPopup.QuickState.Free, 3, 0d);
            Assert.AreEqual("오늘 3회 남음", free);
            StringAssert.DoesNotContain("무료", free, "'무료'는 금 리본 하나만");
            StringAssert.Contains("오늘 2회 남음", IdleChestPopup.QuickCostText(IdleChestPopup.QuickState.Paid, 2, 50d));
            Assert.AreEqual("오늘 모두 썼어요", IdleChestPopup.QuickCostText(IdleChestPopup.QuickState.UsedUp, 0, 0d));
        }

        [Test]
        public void DoubleButton_FreeSaidOnceByPill()
        {
            Assert.AreEqual("무료", ReturnPopup.DoubleBadgeText(true));
            StringAssert.DoesNotContain("무료", ReturnPopup.DoubleSubText(true, 0), "'무료'는 빨간 알약 하나만");
            StringAssert.Contains("보유 3", ReturnPopup.DoubleSubText(false, 3));
            StringAssert.Contains("2", ReturnPopup.DoubleBadgeText(false));
        }

        static float Gray(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
