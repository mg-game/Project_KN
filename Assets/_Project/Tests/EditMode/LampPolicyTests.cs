using System.Collections.Generic;
using System.Reflection;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 자동 불 밝히기 방식 (CR-02, SPEC4 §7.3 — P4-3b): 방식별 판단표(적 수 · 보스 · 수선점 24/26/34/36%), 잠김 → Auto,
    /// 저장 왕복, 진행 중 바로 적용 · 다시 시작할 때 구성에 넣기, Manual은 AUTO여도 쓰지 않지만 소환은 그대로
    /// </summary>
    public class LampPolicyTests : SystemsTestBase
    {
        const int Min = 5;

        [Test]
        public void Auto_DecisionTable()
        {
            var p = LampPolicy.Auto;
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 0, true, 0.1f, Min), "적이 없으면 언제나 아낀다");
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 4, false, 1f, Min));
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 5, false, 1f, Min), "적 5 이상");
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 1, true, 1f, Min), "보스");
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 1, false, 0.34f, Min), "수선점 34%");
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 1, false, 0.36f, Min), "수선점 36%");
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 1, false, 0.24f, Min));
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 1, false, 0.26f, Min));
        }

        [Test]
        public void SaveForBoss_DecisionTable()
        {
            var p = LampPolicy.SaveForBoss;
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 0, true, 0.1f, Min));
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 12, false, 1f, Min), "적이 많아도 보스까지 아낀다");
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 1, true, 1f, Min), "보스");
            Assert.IsTrue(BattleSimulator.AutoLampDecision(p, 1, false, 0.24f, Min), "수선점 24%");
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 1, false, 0.26f, Min), "수선점 26%");
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 1, false, 0.34f, Min));
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 1, false, 0.36f, Min));
        }

        [Test]
        public void Manual_NeverDecides()
        {
            var p = LampPolicy.Manual;
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 12, true, 0.01f, Min));
            Assert.IsFalse(BattleSimulator.AutoLampDecision(p, 5, false, 1f, Min));
        }

        [Test]
        public void Locked_AlwaysAuto_UnlockedUsesSaved()
        {
            Battle.FeatureResolver = f => false;
            Battle.LampPolicy = LampPolicy.Manual;
            Assert.AreEqual(LampPolicy.Manual, Battle.LampPolicy, "저장은 그대로");
            Assert.AreEqual(LampPolicy.Auto, Battle.EffectiveLampPolicy, "잠겨 있으면 Auto");
            Assert.AreEqual(LampPolicy.Auto, Battle.Sim.LampPolicy);

            bool open = true;
            Battle.FeatureResolver = f => open && f == FeatureId.LampPolicy;
            Assert.IsTrue(Battle.IsLampPolicyUnlocked);
            Battle.LampPolicy = LampPolicy.Manual;
            Assert.AreEqual(LampPolicy.Manual, Battle.Sim.LampPolicy, "진행 중인 전투에 바로");

            Battle.FeatureResolver = null;
            Assert.IsFalse(Battle.IsLampPolicyUnlocked, "훅이 없으면 세이브의 열린 기능 목록");
            Data.meta.features.unlocked.Add(FeatureId.LampPolicy);
            Assert.IsTrue(Battle.IsLampPolicyUnlocked);
        }

        [Test]
        public void Save_RoundTrip_AndClamp()
        {
            Battle.LampPolicy = LampPolicy.SaveForBoss;
            Assert.AreEqual(1, Data.battle.lampPolicy);

            var json = JsonUtility.ToJson(Data.battle);
            var loaded = JsonUtility.FromJson<BattleSave>(json);
            Assert.AreEqual(1, loaded.lampPolicy);

            Data.battle.lampPolicy = 9;
            Assert.AreEqual(LampPolicy.Manual, Battle.LampPolicy, "범위 밖 값은 잘라 읽는다");
            Data.battle.lampPolicy = -3;
            Assert.AreEqual(LampPolicy.Auto, Battle.LampPolicy);
        }

        [Test]
        public void Restart_CopiesPolicyIntoSetup()
        {
            Battle.FeatureResolver = f => true;
            Characters.Grant("sock");
            Formation.TryPlace(CharacterRole.Guardian, 0, "sock_base");
            Battle.LampPolicy = LampPolicy.SaveForBoss;
            Battle.Tick(0.1f);
            Assert.AreEqual(LampPolicy.SaveForBoss, Battle.Sim.Setup.LampPolicy);

            Battle.LampPolicy = LampPolicy.Manual;
            Battle.Restart();
            Assert.AreEqual(LampPolicy.Manual, Battle.Sim.Setup.LampPolicy);
        }

        static void SetUpCrowd(BattleConfig cfg, int count)
        {
            var mite = SimKit.Enemy("mite", hp: 1e6f, speed: 0.01f, damage: 1f);
            SimKit.OnlyEnemies(cfg, count, mite);
            cfg.autoLampMinEnemies = Min;
            cfg.autoSummonInterval = 0.2f;
            cfg.startResonance = 50f;
        }

        static BattleSimulator StartAuto(BattleConfig cfg, LampPolicy policy, List<BattleEvent> log)
        {
            var setup = SimKit.Setup();
            setup.LampPolicy = policy;
            setup.Deck.Add(SimKit.Unit("e", SimKit.Stats(CombatStyle.Assault, attack: 0f, lifetime: 30f)));
            var sim = SimKit.Start(cfg, setup, log);
            sim.AutoMode = true;
            return sim;
        }

        [Test]
        public void Manual_DoesNotAutoFire_ButStillAutoSummons()
        {
            SetUpCrowd(BattleCfg, 6);
            var log = new List<BattleEvent>();
            var sim = StartAuto(BattleCfg, LampPolicy.Manual, log);
            SimKit.Advance(sim, 1f);
            sim.FillLamp();
            SimKit.Advance(sim, 2f);

            Assert.AreEqual(0, SimKit.Count(log, BattleEventType.LampLit), "직접: AUTO여도 쓰지 않는다");
            Assert.IsTrue(sim.CanLightLamp, "게이지는 그대로 가득");
            Assert.Greater(SimKit.Count(log, BattleEventType.EchoSummoned), 0, "소환은 자동 그대로");

            sim.LampPolicy = LampPolicy.Auto;
            SimKit.Advance(sim, 0.5f);
            Assert.AreEqual(1, SimKit.Count(log, BattleEventType.LampLit), "Auto로 바꾸면 적 6에 바로 쓴다");
        }

        [Test]
        public void SaveForBoss_HoldsLampAgainstACrowd()
        {
            SetUpCrowd(BattleCfg, 6);
            var log = new List<BattleEvent>();
            var sim = StartAuto(BattleCfg, LampPolicy.SaveForBoss, log);
            SimKit.Advance(sim, 1f);
            sim.FillLamp();
            SimKit.Advance(sim, 2f);
            Assert.AreEqual(0, SimKit.Count(log, BattleEventType.LampLit), "보스가 없고 수선점이 멀쩡하면 아낀다");
        }
    }

    /// <summary>
    /// 불 밝히기 방식 고르기 판 (검증 rm2 r1 — spec4_early/04): 오른쪽 빠른 아이콘 줄('수선 일지' 칸, 메타 HUD — Battle 아래 나중 형제)이
    /// 판 위에 그려져 '알아서' 설명을 자르고 줄 누름을 가로챘다 → 열 때 고르기 층을 Battle 맨 뒤 형제로 올린다. 판 폭은 가장 긴 설명이
    /// 16px 하한에서도 설명 칸 안에 들게 440 (설명 칸 306)
    /// </summary>
    public class LampPolicyPickerTests
    {
        [Test]
        public void RaiseToTop_MovesLayerAboveLaterSiblings()
        {
            var root = new GameObject("Battle", typeof(RectTransform));
            try
            {
                var layer = new GameObject("LampPicker", typeof(RectTransform));
                var hud = new GameObject("MetaHud", typeof(RectTransform));
                layer.transform.SetParent(root.transform, false);
                hud.transform.SetParent(root.transform, false);
                Assert.IsTrue(LampPolicyPicker.RaiseToTop(layer.transform), "나중에 붙은 메타 HUD 뒤로");
                Assert.AreEqual(1, layer.transform.GetSiblingIndex());
                Assert.AreEqual(0, hud.transform.GetSiblingIndex());
                Assert.IsFalse(LampPolicyPicker.RaiseToTop(layer.transform), "이미 맨 뒤면 그대로 (캔버스를 다시 짜지 않는다)");
                Assert.IsFalse(LampPolicyPicker.RaiseToTop(null));
                Assert.IsFalse(LampPolicyPicker.RaiseToTop(root.transform), "부모가 없으면 그대로");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Open_RaisesLayer_ExposesWindow_NeverThrows()
        {
            var root = new GameObject("Battle", typeof(RectTransform));
            try
            {
                var layer = new GameObject("LampPicker", typeof(RectTransform));
                layer.transform.SetParent(root.transform, false);
                var window = new GameObject("Panel", typeof(RectTransform));
                window.transform.SetParent(layer.transform, false);
                new GameObject("MetaHud", typeof(RectTransform)).transform.SetParent(root.transform, false);
                var lamp = new GameObject("LampButton", typeof(RectTransform));
                lamp.transform.SetParent(root.transform, false);
                var picker = lamp.AddComponent<LampPolicyPicker>();
                var panelField = typeof(LampPolicyPicker).GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(panelField, "_panel (V2 S4-39가 읽는다)");
                panelField.SetValue(picker, layer);
                layer.SetActive(false);

                Assert.DoesNotThrow(picker.Open, "게임이 준비되지 않아도 (검증이 바로 연다)");
                Assert.IsTrue(picker.IsOpen);
                Assert.AreEqual(root.transform.childCount - 1, layer.transform.GetSiblingIndex(), "메타 HUD · 램프보다 뒤 형제 = 위에 그린다");
                Assert.AreSame(window.transform, picker.Window, "_window가 비면 층 아래 'Panel' (보이는 판)");
                picker.Close();
                Assert.IsFalse(picker.IsOpen);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Window_FitsLongestDescription_AndClearsGuideBar()
        {
            // r1 캡처: '보스가 나오거나 수선점이 위험할 때만 써요'는 16px 하한에서 ≈ 270px (예전 설명 칸 226을 넘쳐 체크 · 판 테두리에 닿았다)
            Assert.GreaterOrEqual(LampPolicyPicker.DescWidth, 286f, "가장 긴 설명 + 16 여유");
            Assert.AreEqual(3f * LampPolicyPicker.RowHeight + 2f * LampPolicyPicker.WindowPad, LampPolicyPicker.WindowHeight, 1e-4f);
            Assert.AreEqual(280f, LampPolicyPicker.WindowHeight, 1e-4f, "높이는 SPEC4 §7.3 그대로 (3 × 88 + 16)");
            // 판 = 화면 오른쪽에서 10 안쪽 · 폭 WindowWidth → 왼쪽 끝이 가이드 퀘스트 줄(x 16~484) 오른쪽 + 16 이상
            Assert.GreaterOrEqual(1080f - 10f - LampPolicyPicker.WindowWidth, 484f + 16f);
            foreach (var name in LampPolicyPicker.Names) Assert.IsFalse(string.IsNullOrEmpty(name));
            Assert.AreEqual(3, LampPolicyPicker.Descriptions.Length);
        }
    }
}
