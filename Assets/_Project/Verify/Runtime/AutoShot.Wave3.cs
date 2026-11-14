using System;
using System.Collections;
using System.Collections.Generic;
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
    /// AutoShot Wave 3 장면 (UX-SPEC §5.2 Wave 2 줄 · §8.7 재기준 · WP-5 받아들임 — WP-9). 순서는 AutoShot.RunFull이 정하고, 장면마다
    /// "[Verify] PASS|FAIL|SKIP 아이디 장면: …" 줄을 남긴다. 아이디: S-xx = UX-SPEC §5.2 줄 번호 · WP5-* = WP-5 받아들임 · S-badges = MAIN 배지 묶음.
    /// <para>
    /// 대상이 아직 없으면(다른 Wave 3 묶음이 만들기 전 — BossChallengeButton · SynergySummary 등) FAIL이 아니라 SKIP과 이유를 적는다.
    /// 튜토리얼 · 재화 · 세이브를 바꾼 장면은 끝에 되돌린다 (단추 · 별조각 · 파밍 상태 · 배속 · '오늘 다시 묻지 않기').
    /// 명암비는 찍는 그 프레임 화면(CaptureScreenshotAsTexture)에서 글자 사각형 아래를 재어 본다 (§5.1 Contrast — VerifyMath).
    /// </para>
    /// </summary>
    public partial class AutoShot
    {
        // ───────────── 경로 · 자리 (기준 px, 화면 y는 위에서 — UX-SPEC-IA §3.2) ─────────────

        const string GrowthLevelUp1Path = CoachTargets.GrowthPanel + "/Buttons/LevelUp1Button";
        const string GrowthLevelUp10Path = CoachTargets.GrowthPanel + "/Buttons/LevelUp10Button";
        const string GachaPull10Path = CoachTargets.GachaPanel + "/Buttons/Pull10Button";
        const string GachaGemLinkPath = CoachTargets.GachaPanel + "/GemSourceLink";
        const string GachaPityBarPath = CoachTargets.GachaPanel + "/Pity/PityBar";
        const string GachaTitlePath = CoachTargets.GachaPanel + "/Banner/TitleText";
        const string GachaScrimPath = CoachTargets.GachaPanel + "/Banner/TextScrim";
        const string RecommendedPowerPath = MetaHudPath + "/RecommendedPower";
        const string SynergyRowPath = HudPath + "/SynergyRow";
        const string BannerPath = BattlePath + "/BannerArea/Banner";
        const string BossButtonName = "BossChallengeButton";
        const string SynergySummaryName = "SynergySummary";
        const string LevelUpMaxName = "LevelUpMaxButton";
        /// <summary>AUTO · x1 누르는 높이 (Wave 3 WP-5A: AUTO 140x104 · x1 120x104 — W3 요청 '104 tall')</summary>
        const float HudHitHeight = 104f;
        /// <summary>안내 띠가 화면 가장자리에서 띄우는 최소 여백 (px)</summary>
        const float NoticeEdge = 8f;

        /// <summary>보스 도전 버튼 (StageArea 위에서: 226~294 → 16:9 화면 356~424, x 16~246 — IA §3.2 · S §4.4 #11)</summary>
        const float BossTop = 226f, BossBottom = 294f, BossLeft = 16f, BossRight = 246f;
        /// <summary>스테이지 클리어 리본 (StageArea 위에서 242~344 → 화면 372~474, 가운데 폭 516 — IA §3.2 · §8.3 · §8.7)</summary>
        const float RibbonTop = 242f, RibbonBottom = 344f, RibbonWidth = 516f;
        /// <summary>리본이 나타난 뒤 찍는 때 (§5.2 11c "0.3초 뒤" — 들어옴 0.26초가 끝난 뒤)</summary>
        const float RibbonShotDelay = 0.3f;
        /// <summary>11c-drop: 클리어가 연 창이 리본을 막으면 창을 닫고 다음 스테이지에서 다시 (모두 몇 번)</summary>
        const int ClearDropAttempts = 2;
        /// <summary>
        /// 찍으려고 띄우는 리본의 부제 — 실제 클리어 리본(BattleView.OnStageEnded: 보상 한 줄 '+ 단추 120  ·  + 별빛 사탕 1' 또는 '수선점이 한층 더 밝아졌어요')과
        /// 모양은 같고 글은 겹치지 않는 보상 한 줄. 이것이 보이면 이 요청이 도는 중이다
        /// </summary>
        const string RibbonShotSubtitle = "+ 단추 1,234  ·  + 별빛 사탕 3";
        /// <summary>MAIN 배지 묶음 상한 (M09 ≤4 → IA §8.5 MAIN ≤5) · 한 묶음으로 보는 거리</summary>
        const int BadgeClusterLimit = 5;
        const float BadgeClusterRadius = 160f;
        /// <summary>대상 없는 코치 말풍선: MAIN 전투 영역 아래 끝에서 282 위가 말풍선 아래 변 (IA §8.6 — 화면 y 1468)</summary>
        const float FreeBubbleLift = 282f;
        /// <summary>짝짝이 Lv.1→5 (FTUE 7단계 — M06: 6단계 보상만으로 낼 수 있어야 한다)</summary>
        const string FtueFriend = "jjakjjak";

        static readonly List<float> s_luma = new(4096);

        // ═════════════════════════ 환경 (§5.3) ═════════════════════════

        /// <summary>S-env (§5.3 · M01): Application.runInBackground == true (창 포커스를 잃어도 게임이 돈다 — 아니면 FAIL). 화면 크기는 기록만</summary>
        void CheckEnvironment()
        {
            Check("S-env", Application.runInBackground,
                $"startup: runInBackground {Application.runInBackground} (기대 true — M01) · 화면 {Screen.width}x{Screen.height} · 캔버스 {CanvasWidth:0.#}x{CanvasHeight:0.#}");
        }

        // ═════════════════════════ FTUE (02b · 03 · 04b) ═════════════════════════

        /// <summary>02b ftue_intro_back (M15): 인트로 중 뒤로 → 종료 확인이 열리지 않는다 (인트로가 먹는다 — 쪽을 넘기거나 글을 다 보인다)</summary>
        IEnumerator IntroBack(StoryIntroView intro)
        {
            var message = UIManager.Instance.Get<MessagePopup>();
            if (intro == null || !intro.IsPlaying || message == null)
            {
                SkipCheck("S-02b", "ftue_intro_back: 인트로 안 돌거나 확인 팝업 없음");
                yield break;
            }
            UIManager.Instance.OnBack();
            yield return Frames(3);
            yield return Shot("ftue_intro_back", () =>
                Check("S-02b", !message.IsOpen, $"ftue_intro_back: 뒤로 뒤 종료 확인 {OpenText(message.IsOpen)} · 인트로 {(intro.IsPlaying ? "도는 중" : "끝남")} (기대: 확인 닫힘)"));
            if (message.IsOpen) CloseAll();
        }

        /// <summary>
        /// 03 ftue_coach (IA §8.6 · §8.7): 대상 없는 말풍선이면 MAIN 아래 띠 — 아래 변 = StageArea 아래 − 282 (화면 1468), 틀 기둥 가운데.
        /// 보이는 카드(SpeechBubbleView._root, 너비 720)를 잰다 — 컴포넌트 자리는 화면 전체 틀이다 (aspect CAPTURE: 0–W x 0–1920을 읽었다)
        /// </summary>
        void CheckCoachBubble(string shot)
        {
            var coach = FindAnyObjectByType<CoachMarkView>(FindObjectsInactive.Include);
            if (coach == null || !coach.IsActive || coach.Suppressed)
            {
                SkipCheck("S-03", $"{shot}: 코치마크가 안 보인다 ({StepLabel()})");
                return;
            }
            if (coach.Target != null && coach.Target.gameObject.activeInHierarchy)
            {
                SkipCheck("S-03", $"{shot}: 대상이 있는 단계 ({PathOf(coach.Target)}) — 빈 말풍선 자리는 대상 없는 단계에서만 본다");
                return;
            }
            var p = new Probe();
            var bubble = Field<SpeechBubbleView>(coach, "_bubble");
            if (bubble == null) p.Expect(false, "CoachMarkView._bubble 없음");
            else
            {
                var card = Field<RectTransform>(bubble, "_root");
                bool visibleCard = card != null && card != bubble.transform;
                if (card == null) card = (RectTransform)bubble.transform;
                var rect = ScreenRect(card);
                float bottom = ScreenRect(Find(StagePath)).yMax - FreeBubbleLift;
                p.Near("말풍선 아래 변", rect.yMax, bottom, 6f);
                p.Near("말풍선 가운데 x", rect.center.x, FrameCenterX, 6f);
                p.Info($"말풍선 {(visibleCard ? "카드 _root" : "뿌리 (카드 _root 없음)")} {R(rect)} · {StepLabel()}");
            }
            Report("S-03", shot, p);
        }

        /// <summary>
        /// 04b ftue_step6_reward (M06): FTUE를 실제 진행으로 6단계(강화 x1 세 번)까지 몬다 — 덱 첫 칸 소환 · 불 밝히기는 대신 하고,
        /// 첫 보스는 배속 2로 기다린다 (최대 75초). 6단계가 끝난 그 순간 단추 ≥ 짝짝이 Lv.1→5 비용 (230)이고, 보상이 강화 x1 버튼에서 날아갔다 (RewardFlyer).
        /// 강화 비용이 모자라면 모자란 만큼만 채운다 (보상 크기를 가리지 않게). 시간 안에 못 가면 SKIP과 그때 단계
        /// </summary>
        IEnumerator FtueStep6()
        {
            var tutorial = Game.Tutorial;
            var db = Game.MetaDb;
            int stepIndex = -1;
            if (db != null && db.ftueSteps != null)
            {
                for (int i = 0; i < db.ftueSteps.Count; i++)
                {
                    if (db.ftueSteps[i] != null && db.ftueSteps[i].coachTarget == GrowthLevelUp1Path)
                    {
                        stepIndex = i;
                        break;
                    }
                }
            }
            if (!Game.IsMetaActive || tutorial == null || !tutorial.IsActive || stepIndex < 0 || Game.Battle == null)
            {
                SkipCheck("S-04b", $"ftue_step6_reward: 튜토리얼 · 강화 x1 단계 없음 ({StepLabel()})");
                yield break;
            }
            if (tutorial.StepIndex > stepIndex)
            {
                SkipCheck("S-04b", $"ftue_step6_reward: 이미 지남 ({StepLabel()})");
                yield break;
            }

            int speedWas = Game.Battle.Speed;
            bool flew = false;
            void OnPending(CurrencyType currency)
            {
                if (currency == CurrencyType.Gold && RewardFlyer.PendingOf(CurrencyType.Gold) > 0d) flew = true;
            }
            try
            {
                Game.Battle.Speed = BattleSystem.MaxSpeed;
                float limit = Time.realtimeSinceStartup + 75f;
                while (tutorial.IsActive && tutorial.StepIndex < stepIndex && Time.realtimeSinceStartup < limit)
                {
                    var step = tutorial.Current;
                    if (step != null && tutorial.IsStepRunning && !step.hintOnly)
                    {
                        if (step.coachTarget == CoachTargets.DeckCard0) Game.Battle.TrySummon(0);
                        else if (step.coachTarget == CoachTargets.LampButton) Game.Battle.TryLightLamp();
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (!tutorial.IsActive || tutorial.StepIndex != stepIndex)
                {
                    SkipCheck("S-04b", $"ftue_step6_reward: 75초 안에 6단계에 닿지 못했다 ({StepLabel()} · 스테이지 {Game.Battle.StageLabel})");
                    yield break;
                }
                float start = Time.realtimeSinceStartup + 6f;
                while (!tutorial.IsStepRunning && Time.realtimeSinceStartup < start) yield return null;
                Game.Battle.Speed = speedWas;
                CloseAll();
                _tabs.Select(MainTab.Growth);
                yield return WaitSettled();
                yield return new WaitForSecondsRealtime(0.3f);

                var button = Comp<Button>(GrowthLevelUp1Path);
                if (button == null)
                {
                    Check("S-04b", false, $"ftue_step6_reward: {GrowthLevelUp1Path} 없음");
                    yield break;
                }
                var coach = FindAnyObjectByType<CoachMarkView>(FindObjectsInactive.Include);
                string coachTarget = coach != null ? PathOf(coach.Target) : "(코치 없음)";
                double need = Game.Growth.GetLevelUpCost(Game.Growth.Level, 3);
                double have = Game.Currency.Get(CurrencyType.Gold);
                if (have < need) Game.Currency.Add(CurrencyType.Gold, need - have + 1d);
                RewardFlyer.PendingChanged += OnPending;
                for (int i = 0; i < 3 && tutorial.IsActive && tutorial.StepIndex == stepIndex; i++)
                {
                    button.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.15f);
                }
                float done = Time.realtimeSinceStartup + 2f;
                while (tutorial.IsActive && tutorial.StepIndex == stepIndex && Time.realtimeSinceStartup < done) yield return null;
                // 7단계 비용은 유실자 레벨업 재화(실타래)로 낸다 (결정 2 안 D: 6단계 선물 = 단추 125 · 실타래 250)
                double gold = Game.Currency.Get(Game.Characters.LevelUpCurrency);
                bool pending = RewardFlyer.PendingOf(CurrencyType.Gold) > 0d || RewardFlyer.PendingOf(CurrencyType.LineageXp) > 0d;
                double friendCost = FriendCost(1, 5);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("ftue_step6_reward", () =>
                {
                    var p = new Probe();
                    p.Expect(!tutorial.IsActive || tutorial.StepIndex > stepIndex, $"강화 x1 세 번으로 6단계가 끝났다 ({StepLabel()})");
                    p.Expect(gold >= friendCost, $"6단계 직후 실타래 {gold:0} (기대 ≥ 짝짝이 Lv.1→5 {friendCost:0}, M06)");
                    p.Expect(flew || pending, "보상이 RewardFlyer로 날아갔다 (단추 · 실타래 떠 있는 양 > 0)");
                    p.Expect(coachTarget.EndsWith("LevelUp1Button", StringComparison.Ordinal), $"손가락 · 날아가기 출발 = 강화 x1 (코치 대상 {coachTarget})");
                    Report("S-04b", "ftue_step6_reward", p);
                });
            }
            finally
            {
                RewardFlyer.PendingChanged -= OnPending;
                if (Game.Battle != null) Game.Battle.Speed = speedWas;
            }
            CloseAll();
            UINav.GoToMain(true);
        }

        /// <summary>bag_fresh (remaining_issues FR-4): 튜토리얼 중 새 세이브의 가방 — 잠긴 던전 획득처 칸 등 (검사 없이 찍기만, 사람이 본다)</summary>
        IEnumerator BagFresh()
        {
            var bag = UIManager.Instance.Get<BagPopup>();
            if (bag == null)
            {
                SkipCheck("S-21-fresh", "bag_fresh: BagPopup 없음");
                yield break;
            }
            bag.Open();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("bag_fresh", () => Record("INFO", "S-21-fresh", $"bag_fresh: 새 세이브 가방 {OpenText(bag.IsOpen)} ({StepLabel()}) — 잠긴 칸은 사람이 본다"));
            CloseAll();
        }

        /// <summary>짝짝이 레벨 from → to 비용 합 (CharacterSystem.GetLevelUpCost와 같은 식 — 레벨마다 올림)</summary>
        static double FriendCost(int from, int to)
        {
            var config = Game.Config;
            if (config == null) return double.NaN;
            double total = 0d;
            for (int level = from; level < to; level++)
            {
                double raw = config.characterLevelUpBaseCost * Math.Pow(config.characterLevelUpCostGrowth, level - 1);
                total += Math.Max(0d, Math.Ceiling(raw - 1e-6));
            }
            return total;
        }

        // ═════════════════════════ MAIN 검사 (05 main · 29 battle_later) ═════════════════════════

        /// <summary>IA-guide (§3.4 · §8.7, 예전 INFO 줄): GuideQuestBar = x 16~484 (틀 기둥 왼쪽부터 — AS-6) · StageArea 아래 − 258 ~ − 174 (16:9 화면 1492~1576) ±2</summary>
        void CheckGuideBar(string shot)
        {
            var guide = Find(CoachTargets.GuideQuestBar);
            if (guide == null)
            {
                Check("IA-guide", false, $"{shot}: {CoachTargets.GuideQuestBar} 없음");
                return;
            }
            if (!guide.gameObject.activeInHierarchy)
            {
                SkipCheck("IA-guide", $"{shot}: 가이드 퀘스트 줄 꺼짐 (보일 퀘스트 없음)");
                return;
            }
            var rect = ScreenRect(guide);
            float bottom = ScreenRect(Find(StagePath)).yMax - 174f;
            var expected = FrameRect(16f, bottom - 84f, 484f, bottom);
            Check("IA-guide", VerifyMath.Near(rect, expected, 2f), $"{shot}: GuideQuestBar {R(rect)} (기대 {R(expected)})");
        }

        /// <summary>
        /// S-badges (M09 · IA §8.5): MAIN에 보이는 배지(Badge — 점 · 숫자 · NEW)를 160px 안에서 이어지는 묶음으로 세어 ≤ 5. 묶음 · 표시 수는 기록한다
        /// </summary>
        void CheckBadgeClusters(string shot)
        {
            var points = new List<Vector2>();
            var names = new StringBuilder();
            var safe = Find("SafeArea");
            if (safe == null)
            {
                Check("S-badges", false, $"{shot}: SafeArea 없음");
                return;
            }
            foreach (var badge in safe.GetComponentsInChildren<Badge>(false))
            {
                if (!badge.gameObject.activeInHierarchy || !IsVisible(badge.transform)) continue;
                var center = ScreenRect((RectTransform)badge.transform).center;
                points.Add(center);
                if (names.Length > 0) names.Append(", ");
                names.Append(badge.transform.parent != null ? badge.transform.parent.name : badge.name).Append($"({center.x:0},{center.y:0})");
            }
            int clusters = VerifyMath.CountClusters(points, BadgeClusterRadius);
            Check("S-badges", clusters <= BadgeClusterLimit,
                $"{shot}: 배지 묶음 {clusters}개 · 표시 {points.Count}개 (기대 묶음 ≤ {BadgeClusterLimit}, {BadgeClusterRadius:0}px 안 = 한 묶음) — {names}");
        }

        /// <summary>
        /// WP-5 받아들임 (MAIN): WP5-hud AUTO · x1 누르는 자리 ≥ 88 x 88 (M22) · WP5-synergy 시너지 요약 칩 오른쪽 끝 ≤ 화면 가운데 (M43, 1080 폭 = 540) ·
        /// WP5-boss 보스 도전 버튼이 보이면 그 자리. 아직 없는 대상은 SKIP
        /// </summary>
        void CheckWave5Hud(string shot)
        {
            var p = new Probe();
            ExpectHit(p, CoachTargets.AutoButton, HudHitHeight);
            ExpectHit(p, CoachTargets.SpeedButton, HudHitHeight);
            Report("WP5-hud", shot, p);
            CheckSynergyRowEdge(shot);

            var chip = FindDeep(Find(HudPath), SynergySummaryName);
            if (chip == null) SkipCheck("WP5-synergy", $"{shot}: Hud 아래 {SynergySummaryName} 없음 (WP-5A M43 칩 전)");
            else if (!chip.gameObject.activeInHierarchy) SkipCheck("WP5-synergy", $"{shot}: {SynergySummaryName} 꺼짐 (켜진 시너지 없음)");
            else
            {
                var rect = ScreenRect(chip);
                Check("WP5-synergy", rect.xMax <= FrameCenterX + 0.5f, $"{shot}: {SynergySummaryName} {R(rect)} 오른쪽 끝 {rect.xMax:0.#} (기대 ≤ 기둥 가운데 {FrameCenterX:0})");
            }

            var boss = FindDeep(Find(BattlePath), BossButtonName);
            if (boss == null) SkipCheck("WP5-boss", $"{shot}: {BossButtonName} 없음 (WP-5A M13 전)");
            else if (!boss.gameObject.activeInHierarchy) Record("INFO", "WP5-boss", $"{shot}: 파밍 아님 — 보스 도전 버튼 꺼짐 (battle_farm에서 본다)");
            else
            {
                var q = new Probe();
                ExpectBossButton(q, boss);
                Report("WP5-boss", shot, q);
            }
        }

        void ExpectHit(Probe p, string path, float height = 0f)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            var button = Comp<Button>(path);
            if (button == null || button.targetGraphic == null)
            {
                p.Expect(false, $"{name} 버튼 없음");
                return;
            }
            var graphic = button.targetGraphic;
            var hit = VerifyMath.HitSize(ScreenRect(graphic.rectTransform).size, graphic.raycastPadding);
            // 맞춘 기대도 PASS 줄에 남는다 (Probe) — 따로 기록하지 않는다
            p.Expect(hit.x >= UISize.MinHit - 0.5f && hit.y >= UISize.MinHit - 0.5f, $"{name} 누르는 자리 {hit.x:0}x{hit.y:0} (기대 ≥ {UISize.MinHit:0}x{UISize.MinHit:0}, M22)");
            if (height > 0f) p.Near($"{name} 누르는 높이", hit.y, height, 1f);
        }

        /// <summary>
        /// WP5-synergyrow (W3 요청 — M43): 시너지 줄(Hud/SynergyRow)에서 보이는 칸(제 그래픽이 보이는 자식)의 오른쪽 끝 ≤ 화면 가운데 (1080 폭 = 540).
        /// 줄이 꺼졌거나 보이는 칸이 없으면 SKIP
        /// </summary>
        void CheckSynergyRowEdge(string shot)
        {
            var row = Find(SynergyRowPath);
            if (row == null || !row.gameObject.activeInHierarchy || EffectiveAlpha(row) < 0.05f)
            {
                SkipCheck("WP5-synergyrow", $"{shot}: SynergyRow {(row == null ? "없음" : "꺼짐")}");
                return;
            }
            float right = float.NegativeInfinity;
            string edge = null;
            int cells = 0;
            foreach (var graphic in row.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || graphic.color.a <= 0.05f || EffectiveAlpha(graphic.transform) < 0.05f) continue;
                if (graphic is TMP_Text text && string.IsNullOrEmpty(text.text)) continue;
                var rect = ScreenRect(graphic.rectTransform);
                if (rect.width < 1f || rect.height < 1f) continue;
                cells++;
                if (rect.xMax <= right) continue;
                right = rect.xMax;
                edge = PathOf(graphic.transform);
            }
            if (cells == 0)
            {
                SkipCheck("WP5-synergyrow", $"{shot}: SynergyRow에 보이는 칸 없음");
                return;
            }
            Check("WP5-synergyrow", right <= FrameCenterX + 0.5f,
                $"{shot}: SynergyRow 보이는 그래픽 {cells}개 오른쪽 끝 {right:0.#} ({edge}) (기대 ≤ 기둥 가운데 {FrameCenterX:0})");
        }

        /// <summary>
        /// S-railnew (W3 요청 — 배지 규칙): 빠른 줄(QuickRail_L · R)에서 받을 것(점 · 숫자 배지)이 둘 이상 켜져 있으면 NEW 배지는 하나도 보이지 않는다
        /// </summary>
        void CheckRailNew(string shot)
        {
            if (!Game.IsMetaActive)
            {
                SkipCheck("S-railnew", $"{shot}: 메타 꺼짐 (빠른 줄 없음)");
                return;
            }
            var lit = new StringBuilder();
            var fresh = new StringBuilder();
            int claims = 0, news = 0, rails = 0;
            var missing = new StringBuilder();
            foreach (var path in new[] { RailLeftPath, RailRightPath })
            {
                var rail = Find(path);
                if (rail == null)
                {
                    missing.Append(missing.Length > 0 ? ", " : string.Empty).Append(path);
                    continue;
                }
                rails++;
                foreach (var badge in rail.GetComponentsInChildren<Badge>(false))
                {
                    if (!badge.gameObject.activeInHierarchy || !IsVisible(badge.transform)) continue;
                    var icon = badge.GetComponentInParent<QuickIconView>();
                    string owner = icon != null ? icon.name : badge.transform.parent != null ? badge.transform.parent.name : badge.name;
                    var into = badge.IsNew ? fresh : lit;
                    if (badge.IsNew) news++;
                    else claims++;
                    if (into.Length > 0) into.Append(", ");
                    into.Append(owner);
                }
            }
            string measured = $"{shot}: 빠른 줄 받을 것 {claims}개 ({lit}) · NEW {news}개 ({fresh})";
            if (rails == 0)
            {
                Check("S-railnew", false, $"{shot}: 빠른 줄 경로를 하나도 못 찾았다 ({missing})");
                return;
            }
            if (missing.Length > 0) measured += $" · 없는 줄 {missing}";
            // 받을 것이 둘 미만이면 규칙(받을 것 ≥ 2 → NEW 0)이 걸리지 않는다 — 잰 값만 남기고 SKIP
            if (claims < 2)
            {
                SkipCheck("S-railnew", measured + " — 규칙을 재지 못함 (받을 것 < 2)");
                return;
            }
            Check("S-railnew", news == 0, measured + " (기대: 받을 것 ≥ 2이면 NEW 0)");
        }

        /// <summary>보스 도전 버튼 자리 ±2 (x는 틀 기둥 왼쪽부터 — AS-6) · 시너지 줄 · 요약 칩 · 리본 자리와 겹치지 않음</summary>
        void ExpectBossButton(Probe p, RectTransform boss)
        {
            var stage = ScreenRect(Find(StagePath));
            var rect = ScreenRect(boss);
            var expected = FrameRect(BossLeft, stage.yMin + BossTop, BossRight, stage.yMin + BossBottom);
            p.Expect(VerifyMath.Near(rect, expected, 2f), $"BossChallengeButton {R(rect)} (기대 {R(expected)})");
            var row = Find(SynergyRowPath);
            if (row != null && row.gameObject.activeInHierarchy && VerifyMath.Overlaps(rect, ScreenRect(row)))
                p.Expect(false, $"SynergyRow {R(ScreenRect(row))}와 겹침");
            var chip = FindDeep(Find(HudPath), SynergySummaryName);
            if (chip != null && chip.gameObject.activeInHierarchy && VerifyMath.Overlaps(rect, ScreenRect(chip)))
                p.Expect(false, $"{SynergySummaryName} {R(ScreenRect(chip))}와 겹침");
            var ribbon = RibbonZone();
            if (VerifyMath.Overlaps(rect, ribbon)) p.Expect(false, $"리본 자리 {R(ribbon)}와 겹침");
        }

        /// <summary>리본 자리 (StageArea 위 + 242~344, 틀 기둥 가운데 516 폭)</summary>
        Rect RibbonZone()
        {
            var stage = ScreenRect(Find(StagePath));
            float left = FrameMinX + (FrameWidth - RibbonWidth) * 0.5f;
            return Rect.MinMaxRect(left, stage.yMin + RibbonTop, left + RibbonWidth, stage.yMin + RibbonBottom);
        }

        // ═════════════════════════ 첫 만남 (16 앞) ═════════════════════════

        /// <summary>
        /// 소환 탭을 덮는 첫 만남 카드를 먼저 받는다 (remaining_issues — 18 tab_Gacha가 카드에 가려 천장 막대 · 제목 상자(M63/M84)를 못 찍었다):
        /// 카드가 뜨면 first_meeting_card → 문 열기 → first_meeting_result → 모두 닫고 MAIN
        /// </summary>
        IEnumerator FirstMeeting()
        {
            var card = FindAnyObjectByType<FirstMeetingCardView>(FindObjectsInactive.Include);
            if (!Game.IsMetaActive || card == null)
            {
                Record("INFO", "S-16-meeting", "첫 만남 카드 없음 (메타 꺼짐)");
                yield break;
            }
            CloseAll();
            _tabs.Select(MainTab.Gacha);
            yield return WaitSettled();
            yield return new WaitForSecondsRealtime(0.4f);
            if (!card.IsShown)
            {
                Record("INFO", "S-16-meeting", $"첫 만남 카드가 뜨지 않았다 (받음 {Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned)})");
                UINav.GoToMain(true);
                yield break;
            }
            yield return Shot("first_meeting_card");
            var open = card.OpenButton != null ? card.OpenButton.GetComponent<Button>() : null;
            if (open != null) open.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("first_meeting_result", () =>
                Check("S-16-meeting", Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned) && !card.IsShown,
                    $"first_meeting_result: 확정 소환 받음 {Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned)} · 카드 {(card.IsShown ? "남음" : "닫힘")}"));
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
        }

        /// <summary>16 tab_Gacha (M63 · M84): 첫 만남 카드 없음 · 천장 막대 바탕이 보인다(0이어도) · 18 높이 · 제목 상자 ≤ 540 폭 · 글 받침</summary>
        void CheckGachaTab(string shot)
        {
            var p = new Probe();
            var card = FindAnyObjectByType<FirstMeetingCardView>(FindObjectsInactive.Include);
            p.Expect(card == null || !card.IsShown, "첫 만남 카드가 소환 시트를 덮지 않는다");
            var track = Comp<Image>(GachaPityBarPath);
            if (track == null) p.Expect(false, $"{GachaPityBarPath} 없음");
            else
            {
                p.Expect(track.isActiveAndEnabled && track.color.a > 0.5f && EffectiveAlpha(track.transform) > 0.5f, $"천장 막대 바탕이 보인다 (색 α {track.color.a:0.00})");
                p.Near("천장 막대 높이", track.rectTransform.rect.height, 18f, 1f);
            }
            var title = Comp<TMP_Text>(GachaTitlePath);
            float width = title != null ? title.rectTransform.rect.width : -1f;
            p.Expect(title != null && width <= 540.5f, $"제목 상자 폭 {width:0} (기대 ≤ 540)");
            var scrim = Find(GachaScrimPath);
            p.Expect(scrim != null && scrim.gameObject.activeInHierarchy, "글 받침 Banner/TextScrim");
            Report("S-16", shot, p);
        }

        /// <summary>시트 탭: 고른 탭에는 배지가 보이지 않는다 (M09 — 고른 탭의 점은 그 화면 안에서 본다)</summary>
        void CheckSelectedTabBadge(string shot, MainTab tab)
        {
            var button = _tabs.GetTabButton(tab);
            if (button == null)
            {
                Check("S-seltab", false, $"{shot}: 탭 버튼 없음");
                return;
            }
            var shown = new StringBuilder();
            foreach (var badge in button.GetComponentsInChildren<Badge>(false))
            {
                if (badge.gameObject.activeInHierarchy && IsVisible(badge.transform)) shown.Append(PathOf(badge.transform)).Append(' ');
            }
            Check("S-seltab", shown.Length == 0, $"{shot}: 고른 탭 배지 {(shown.Length == 0 ? "없음" : shown.ToString())}");
        }

        /// <summary>
        /// S-seltab-text (W3 요청): 고른 탭 이름 글자와 그 아래 실제로 그려진 판 픽셀의 명암 ≥ 4.5 (찍는 프레임 화면에서 — 글자 색 · 판 그림을 따로 믿지 않는다).
        /// 판 그림 이름은 기록만
        /// </summary>
        void CheckSelectedTabLabel(string shot, MainTab tab, Frame frame)
        {
            var button = _tabs.GetTabButton(tab);
            TMP_Text label = null;
            if (button != null)
            {
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (!text.isActiveAndEnabled || string.IsNullOrEmpty(ShownText(text))) continue;
                    label = text;
                    break;
                }
            }
            if (label == null)
            {
                Check("S-seltab-text", false, $"{shot}: 고른 탭 이름 글자 없음");
                return;
            }
            float ratio = ContrastOf(label, frame);
            // 판 = 고른 탭의 받침 SelectedPlate (그림이 있으면 0.75 틴트 — KIT R2, G3 다시 빌드 뒤). 명암은 그려진 픽셀로 재므로 틴트가 그대로 들어간다 — 그림 · 색은 기록만
            var plateRect = FindDeep(button.transform, "SelectedPlate");
            var plate = plateRect != null ? plateRect.GetComponent<Image>() : button.targetGraphic as Image;
            string sprite = plate != null && plate.sprite != null ? plate.sprite.name : "(그림 없음)";
            string tint = plate != null ? $" 색 ({plate.color.r:0.00}, {plate.color.g:0.00}, {plate.color.b:0.00})" : string.Empty;
            Check("S-seltab-text", !float.IsNaN(ratio) && ratio >= VerifyMath.ContrastText,
                $"{shot}: 고른 탭 '{ShownText(label)}' 글자 vs 판 픽셀 명암 {ratio:0.00} (기대 ≥ {VerifyMath.ContrastText}) · 판 {(plateRect != null ? "SelectedPlate " : string.Empty)}{sprite}{tint}");
        }

        // ═════════════════════════ 전투 (11b · 11c · 11d · 11e) ═════════════════════════

        /// <summary>
        /// 11b battle_farm (M13): 막힌 스테이지 = 지금 + 1로 두고 지금 스테이지를 다시 시작(StageChanged) → 파밍. 제목에 '반복 중',
        /// BossChallengeButton이 자리(16~246 × 356~424)에 켜지고 시너지 줄 · 리본 자리와 겹치지 않는다. 자동 보스 도전은 찍는 동안 끈다. 끝나면 되돌린다
        /// </summary>
        IEnumerator BattleFarm()
        {
            var battle = Game.Battle;
            var save = Game.Data?.battle;
            if (battle == null || save == null)
            {
                SkipCheck("S-11b", "battle_farm: 전투 없음");
                yield break;
            }
            var boss = FindDeep(Find(BattlePath), BossButtonName);
            if (boss == null)
            {
                SkipCheck("S-11b", $"battle_farm: {BossButtonName} 없음 (WP-5A M13 HUD 전)");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            int stage = battle.Stage;
            int highestWas = save.highestStage;
            int targetWas = save.challengeTarget;
            bool autoWas = battle.AutoChallenge;
            try
            {
                save.highestStage = Math.Max(save.highestStage, stage + 1);
                save.challengeTarget = stage + 1;
                battle.AutoChallenge = false;
                battle.GoToStage(stage);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("battle_farm", () =>
                {
                    var p = new Probe();
                    p.Expect(battle.IsFarming, $"파밍 중 (막힌 {save.challengeTarget} · 지금 {battle.Stage} · 자동 보스 도전 열림 {battle.IsAutoChallengeUnlocked})");
                    string title = FindText(Find(CoachTargets.StagePanel), "반복 중");
                    p.Expect(title != null, "스테이지 판 제목에 '반복 중'");
                    if (title != null) p.Info($"제목 '{title}'");
                    p.Expect(boss.gameObject.activeInHierarchy, "BossChallengeButton 켜짐");
                    if (boss.gameObject.activeInHierarchy) ExpectBossButton(p, boss);
                    Report("S-11b", "battle_farm", p);
                });
            }
            finally
            {
                save.challengeTarget = targetWas;
                // 찍으려고 올린 최고 스테이지만 되돌린다 (그사이 실제로 더 갔으면 그대로)
                if (save.highestStage == stage + 1 && highestWas < stage + 1) save.highestStage = highestWas;
                battle.AutoChallenge = autoWas;
            }
            battle.GoToStage(Math.Min(stage, battle.HighestStage));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>
        /// 11c battle_ribbon (M67 · IA §8.7): 스테이지 클리어 리본을 띄워(BattleBannerView.Show Ribbon — 실제 클리어와 같은 길) 나타난 뒤 0.3초에
        /// 찍는다 (들어옴이 끝난 크기). 리본 = 화면 372~474(StageArea 위 + 242~344) 가운데, StagePanel · RecommendedPower · SynergyRow(+ 요약 칩 · 보스 버튼)와 겹치지 않음.
        /// 실제 클리어 리본도 제목이 같아서(BattleView.OnStageEnded) 제목으로 기다리면 앞에 돌던 리본의 유지 끝 · 퇴장(1.06배 · 위로 30)을 잴 수 있다 —
        /// 앞 배너 줄이 다 빠질 때까지(배너 꺼짐, 늦어도 8초) 기다렸다가 띄우고, 이 요청에만 있는 부제(RibbonShotSubtitle)가 보인 때부터 0.3초를 잰다
        /// </summary>
        IEnumerator BattleRibbon()
        {
            var view = FindAnyObjectByType<BattleBannerView>();
            var banner = Find(BannerPath);
            if (view == null || banner == null)
            {
                SkipCheck("S-11c", $"battle_ribbon: BattleBannerView · {BannerPath} 없음");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return null;
            // 앞에 도는 배너(스테이지 시작 · 실제 클리어 리본 등)가 다 끝나 BattleBannerView.Run이 배너를 끌 때까지
            float drain = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < drain && banner.gameObject.activeInHierarchy) yield return null;
            bool drained = !banner.gameObject.activeInHierarchy;
            const string title = "스테이지 클리어!";
            view.Show(title, RibbonShotSubtitle, UITheme.Gold, BattleBannerView.Style.Ribbon, 0.9f);
            // 이 리본(부제로 알아본다)이 나타날 때까지 (늦어도 6초), 나타난 뒤 0.3초에 찍는다
            float limit = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < limit && !RibbonShotVisible(banner)) yield return null;
            yield return new WaitForSecondsRealtime(RibbonShotDelay);
            bool shown = RibbonShotVisible(banner) && FindText(banner, title) != null;
            yield return Shot("battle_ribbon", () =>
            {
                var p = new Probe();
                if (!drained) p.Info("8초 안에 앞 배너가 다 빠지지 않았다 (줄 뒤에서 기다렸다가 잰다)");
                p.Expect(shown, "6초 안에 이 리본이 나타났고 0.3초 뒤에도 떠 있다");
                if (shown)
                {
                    var rect = ScreenRect(banner);
                    var zone = RibbonZone();
                    p.Near("리본 위", rect.yMin, zone.yMin, 4f);
                    p.Near("리본 아래", rect.yMax, zone.yMax, 4f);
                    p.Near("리본 가운데 x", rect.center.x, zone.center.x, 4f);
                    ExpectClear(p, rect, CoachTargets.StagePanel);
                    ExpectClear(p, rect, RecommendedPowerPath);
                    ExpectClear(p, rect, SynergyRowPath);
                    var chip = FindDeep(Find(HudPath), SynergySummaryName);
                    if (chip != null) ExpectClear(p, rect, PathOf(chip));
                    var boss = FindDeep(Find(BattlePath), BossButtonName);
                    if (boss != null) ExpectClear(p, rect, PathOf(boss));
                    p.Info($"리본 {R(rect)}");
                }
                Report("S-11c", "battle_ribbon", p);
            });
        }

        /// <summary>찍으려고 띄운 리본(부제 RibbonShotSubtitle)이 배너에 실렸는지 (기다리는 시작 신호 — 날 글, 보이는지는 FindText로 따로 본다)</summary>
        static bool RibbonShotVisible(Transform banner) =>
            banner.gameObject.activeInHierarchy && FindRawText(banner, RibbonShotSubtitle) != null;

        void ExpectClear(Probe p, Rect rect, string path)
        {
            var other = Find(path);
            if (other == null || !other.gameObject.activeInHierarchy || EffectiveAlpha(other) < 0.05f) return;
            var otherRect = ScreenRect(other);
            if (VerifyMath.Overlaps(rect, otherRect)) p.Expect(false, $"{other.name} {R(otherRect)}와 겹침");
        }

        /// <summary>
        /// 11d toast_stack (M14): 같은 오류 토스트 1초 안에 세 번 + 안내 하나 → 오류 하나만 보이고(합침) 안내는 줄에 하나, 위 끝 = 안전 영역 위 + 480 ±4,
        /// 종류 아이콘이 보인다. 팝업 없음 (자리 규칙 '팝업 없음')
        /// </summary>
        IEnumerator ToastStack()
        {
            var toast = FindAnyObjectByType<ToastView>(FindObjectsInactive.Include);
            if (toast == null)
            {
                SkipCheck("S-11d", "toast_stack: ToastView 없음");
                yield break;
            }
            CloseAll();
            toast.Clear();
            const string error = "단추가 모자라요";
            const string info = "친구가 수선점에 놀러 왔어요";
            UIManager.Instance.ShowToast(error, ToastKind.Error);
            yield return new WaitForSecondsRealtime(0.3f);
            UIManager.Instance.ShowToast(error, ToastKind.Error);
            yield return new WaitForSecondsRealtime(0.3f);
            UIManager.Instance.ShowToast(error, ToastKind.Error);
            yield return new WaitForSecondsRealtime(0.2f);
            UIManager.Instance.ShowToast(info, ToastKind.Info);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Shot("toast_stack", () =>
            {
                var p = new Probe();
                p.Expect(toast.IsShowing && toast.CurrentText == error && toast.CurrentKind == ToastKind.Error, $"보이는 토스트 '{toast.CurrentText}' {toast.CurrentKind} (기대 오류 하나)");
                p.Expect(toast.QueuedCount == 1, $"줄 {toast.QueuedCount}개 (기대 1 — 같은 오류는 합치고 안내만 기다린다)");
                var rect = ScreenRect((RectTransform)toast.transform);
                var layer = toast.transform.parent as RectTransform;
                float top = (layer != null ? ScreenRect(layer).yMin : 0f) + UISize.ToastTop;
                p.Near("토스트 위 끝", rect.yMin, top, 4f);
                // KIT 줄 (RM-2): 팝업 없이 MAIN 빠른 줄이 보이면 x 128~952 (빠른 줄 칸과 겹치지 않게), 시트 · 팝업 밑이면 100~980 — 틀 기둥 가장자리부터 (AS-6)
                bool rails = !UIFrame.SheetOpen && UIFrame.SheetProgress <= 0f && Game.IsMetaActive && Find(RailLeftPath) is RectTransform railL && railL.gameObject.activeInHierarchy;
                if (IsFrame1080 && rails) p.Expect(rect.xMin >= FrameMinX + 128f - 1f && rect.xMax <= FrameMaxX - 128f + 1f,
                    $"토스트 {R(rect)} (기대 MAIN 빠른 줄 옆 x {FrameMinX + 128f:0.#}~{FrameMaxX - 128f:0.#} = 기둥 128~952 — KIT 줄)");
                else p.Info($"토스트 {R(rect)} · 빠른 줄 {(rails ? "보임" : "안 보임")}");
                var icon = Field<Image>(toast, "_kindIcon");
                p.Expect(icon != null && icon.isActiveAndEnabled && icon.sprite != null, "종류 아이콘이 보인다");
                Report("S-11d", "toast_stack", p);
            });
            toast.Clear();
        }

        /// <summary>
        /// 11e focus_hold (M01 · M19): 강화 x1을 1초 길게 누른 뒤(반복) 모든 컴포넌트에 OnApplicationFocus(false) → 1초 동안 레벨이 그대로 · 단추가 줄지 않음 ·
        /// 누름 크기 1, 포커스를 돌려받고 떼어도 한 번 더 오르지 않는다.
        /// S-11e-chip (KIT R2): 누르기 전 레벨을 10레벨 고비 2 앞(x8)으로 올려 길게 누르는 동안 고비를 넘게 하고, 2초 동안 프레임마다 켜진 LevelUpFanfare/Chip*가
        /// ShopCard/LevelText 글과 겹치지 않고 · GrowthPanel/Buttons와 겹치지 않고 · ShopCard 안에 있는지 잰다 (뒤 둘은 OutBack 1.1배를 빼고 잰 크기). 칩이 안 뜨면 INFO
        /// </summary>
        IEnumerator FocusHold()
        {
            CloseAll();
            Game.Currency.Add(CurrencyType.Gold, 1e7);
            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            var button = Comp<Button>(GrowthLevelUp1Path);
            if (button == null)
            {
                Check("S-11e", false, $"focus_hold: {GrowthLevelUp1Path} 없음");
                yield break;
            }
            // 길게 누르는 1초 안에 10레벨 고비를 넘도록 x8까지 (같은 십 단위 안 — 이 올림은 고비 칩을 띄우지 않는다)
            int toMark = Game.Growth.Level / 10 * 10 + 8 - Game.Growth.Level;
            if (toMark > 0 && Game.Growth.TryLevelUp(toMark)) yield return new WaitForSecondsRealtime(0.3f);
            var chips = new FanfareChipWatch(FindAnyObjectByType<LevelUpFanfareView>());
            int start = Game.Growth.Level;
            var data = Press(button);
            float holdEnd = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < holdEnd)
            {
                WatchFanfareChips(chips);
                yield return null;
            }
            int held = Game.Growth.Level;
            BroadcastFocus(false);
            int atLoss = Game.Growth.Level;
            double goldAtLoss = Game.Currency.Get(CurrencyType.Gold);
            float lossEnd = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < lossEnd)
            {
                WatchFanfareChips(chips);
                yield return null;
            }
            int afterLoss = Game.Growth.Level;
            double goldAfter = Game.Currency.Get(CurrencyType.Gold);
            float scale = button.transform.localScale.x;
            yield return Shot("focus_hold", () =>
            {
                BroadcastFocus(true);
                Release(button, data);
                var p = new Probe();
                p.Expect(held > start, $"길게 누르는 1초 동안 반복했다 (레벨 {start} → {held})");
                p.Expect(afterLoss == atLoss, $"포커스를 잃은 뒤 1초 레벨 {atLoss} → {afterLoss} (기대 그대로)");
                p.Expect(goldAfter >= goldAtLoss - 0.5d, $"포커스를 잃은 뒤 단추 {goldAtLoss:0} → {goldAfter:0} (줄지 않음 — 방치 수입은 늘어도 된다)");
                p.Near("누름 크기", scale, 1f, 0.02f);
                p.Expect(Game.Growth.Level == afterLoss, $"늦게 온 떼기 뒤 레벨 {Game.Growth.Level} (기대 {afterLoss} — 한 번 더 오르지 않는다)");
                Report("S-11e", "focus_hold", p);
                bool crossed = held / 10 > start / 10;
                if (chips.Samples == 0)
                {
                    Record("INFO", "S-11e-chip", $"focus_hold: 켜진 고비 칩 없음 — 칩 자리 규칙을 재지 못함 (레벨 {start} → {held}, 10레벨 고비 {(crossed ? "넘음" : "안 넘음")}" +
                                                 $"{(chips.Layer == null ? " · LevelUpFanfareView 없음" : string.Empty)})");
                    return;
                }
                var q = new Probe();
                q.Expect(chips.Bad == null, chips.Bad ?? $"칩 {chips.Seen}번 · {chips.Samples}프레임 — ShopCard/LevelText 글 · GrowthPanel/Buttons와 안 겹침, ShopCard 안");
                q.Info($"처음 잰 칩 {chips.First} · 레벨 {start} → {held}");
                Report("S-11e-chip", "focus_hold", q);
            });
        }

        /// <summary>S-11e-chip: 강화 팡파르 칩을 프레임마다 잰 결과 (KIT R2)</summary>
        sealed class FanfareChipWatch
        {
            public readonly Transform Layer;
            public int Samples;
            public int Seen;
            public string Bad;
            public string First;
            public readonly bool[] WasOn = new bool[8];

            public FanfareChipWatch(Component view) => Layer = view != null ? view.transform : null;
        }

        /// <summary>
        /// 켜진(알파 &gt; 0.05) LevelUpFanfare/Chip* 하나하나: 지금 크기 그대로 ShopCard/LevelText 글 사각형(textBounds)과 겹치면 안 되고, OutBack(최대 1.1배)을 뺀 크기로
        /// GrowthPanel/Buttons와 겹치지 않고 ShopCard 안 (0.5px 여유). 첫 어긋남 하나만 남긴다
        /// </summary>
        void WatchFanfareChips(FanfareChipWatch watch)
        {
            if (watch.Layer == null) return;
            var card = Find(CoachTargets.GrowthPanel + "/ShopCard");
            if (card == null || !card.gameObject.activeInHierarchy) return;
            var cardRect = ScreenRect(card);
            var levelRect = FindDeep(card, "LevelText");
            var level = levelRect != null ? levelRect.GetComponent<TMP_Text>() : null;
            var ink = level != null && level.isActiveAndEnabled ? InkRect(level) : new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
            var buttons = Find(CoachTargets.GrowthPanel + "/Buttons");
            var buttonsRect = buttons != null && buttons.gameObject.activeInHierarchy ? ScreenRect(buttons) : new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
            int slot = 0;
            for (int i = 0; i < watch.Layer.childCount; i++)
            {
                var chip = watch.Layer.GetChild(i) as RectTransform;
                if (chip == null || !chip.name.StartsWith("Chip", StringComparison.Ordinal)) continue;
                bool on = chip.gameObject.activeInHierarchy && EffectiveAlpha(chip) > 0.05f;
                if (slot < watch.WasOn.Length)
                {
                    if (on && !watch.WasOn[slot]) watch.Seen++;
                    watch.WasOn[slot] = on;
                }
                slot++;
                if (!on) continue;
                watch.Samples++;
                var rect = ScreenRect(chip);
                float scale = chip.localScale.x;
                var unscaled = ScaleAbout(rect, 1f / Mathf.Max(1f, scale));
                watch.First ??= $"{chip.name} {R(rect)} 크기 {scale:0.00} '{FindText(chip, string.Empty)}'";
                if (watch.Bad != null) continue;
                string what = null;
                if (!float.IsNaN(ink.x) && Overlaps(rect, ink)) what = $"ShopCard/LevelText 글 '{ShownText(level)}' {R(ink)}와 겹침";
                else if (!float.IsNaN(buttonsRect.x) && Overlaps(unscaled, buttonsRect)) what = $"GrowthPanel/Buttons {R(buttonsRect)}와 겹침";
                else if (unscaled.xMin < cardRect.xMin - 0.5f || unscaled.xMax > cardRect.xMax + 0.5f || unscaled.yMin < cardRect.yMin - 0.5f || unscaled.yMax > cardRect.yMax + 0.5f)
                    what = $"ShopCard {R(cardRect)} 밖 (1.1배를 뺀 {R(unscaled)})";
                if (what != null) watch.Bad = $"{chip.name} {R(rect)} 크기 {scale:0.00} '{FindText(chip, string.Empty)}' — {what}";
            }
        }

        /// <summary>TMP 글이 그려진 사각형 (textBounds — 기준 px). 비었으면 글 상자</summary>
        Rect InkRect(TMP_Text label)
        {
            var bounds = label.textBounds;
            var rt = label.rectTransform;
            if (bounds.size.x < 1f || bounds.size.y < 1f) return ScreenRect(rt);
            var a = RefPoint(rt.TransformPoint(bounds.min));
            var b = RefPoint(rt.TransformPoint(bounds.max));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>가운데를 두고 k배 한 사각형</summary>
        static Rect ScaleAbout(Rect rect, float k)
        {
            var center = rect.center;
            var half = rect.size * (0.5f * k);
            return Rect.MinMaxRect(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y);
        }

        PointerEventData Press(Button button)
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

        /// <summary>§5.1 BroadcastFocus: 켜진 모든 MonoBehaviour에 OnApplicationFocus(focused) (받는 쪽이 없어도 된다)</summary>
        static void BroadcastFocus(bool focused)
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.isActiveAndEnabled && !(behaviour is AutoShot))
                    behaviour.SendMessage("OnApplicationFocus", focused, SendMessageOptions.DontRequireReceiver);
            }
        }

        // ═════════════════════════ 모자람 (12b · 16b · 16c) ═════════════════════════

        /// <summary>12b tab_Growth_short (M07): 단추 0 → 강화 x10 Short — 비용 알약이 보이고 비용 명암 ≥ 4.5, 버튼 그림(스킨)은 그대로. 끝나면 단추를 되돌린다</summary>
        IEnumerator GrowthShort()
        {
            CloseAll();
            double gold = Game.Currency.Get(CurrencyType.Gold);
            Game.Currency.TrySpend(CurrencyType.Gold, gold);
            try
            {
                _tabs.Select(MainTab.Growth);
                yield return WaitSettled();
                yield return new WaitForSecondsRealtime(0.4f);
                var view = FindCostView(GrowthLevelUp10Path);
                yield return ShotWithFrame("tab_Growth_short", frame =>
                {
                    var p = new Probe();
                    ExpectShortCost(p, view, frame, "강화 x10");
                    Report("S-12b", "tab_Growth_short", p);
                });
            }
            finally
            {
                Game.Currency.Add(CurrencyType.Gold, gold);
            }
        }

        const string LineageXpChipPath = CoachTargets.GrowthPanel + "/LineageXpChip";
        const string LineageRatePath = CoachTargets.GrowthPanel + "/ShopCard/LineageRateText";
        const string LineageTagPath = CoachTargets.TabGrowth + "/LineageXpTag/Pill";

        /// <summary>
        /// 실타래 화면 (결정 2 안 D, T4 · T10): 강화 탭을 열고 잔액 칩(숫자) · 수선점 카드 "초당 실타래" 줄 · 탭 꼬리표가 켜졌는지,
        /// 친구 카드 비용 그림이 실타래인지 본다 (growth_lineage_xp). 그다음 실타래를 날려 칩이 도착지인지(떠 있는 양 &gt; 0, 칩 숫자 = 잔액 − 떠 있는 양)
        /// 찍고 (growth_lineage_xp_fly), 시트를 닫은 MAIN에서 꼬리표가 보이는지 찍는다 (main_lineage_tag)
        /// </summary>
        IEnumerator GrowthLineageXp()
        {
            CloseAll();
            Game.Currency.Add(CurrencyType.LineageXp, 12345);
            _tabs.Select(MainTab.Growth);
            yield return WaitSettled();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("growth_lineage_xp", () =>
            {
                var p = new Probe();
                var chip = Find(LineageXpChipPath);
                var chipAmount = chip != null ? chip.Find("Amount")?.GetComponent<TMP_Text>() : null;
                p.Expect(chip != null && chip.gameObject.activeInHierarchy, "잔액 칩 켜짐");
                p.Expect(chipAmount != null && !string.IsNullOrEmpty(chipAmount.text), $"잔액 칩 숫자 '{chipAmount?.text}'");
                var rate = Comp<TMP_Text>(LineageRatePath);
                p.Expect(rate != null && rate.gameObject.activeInHierarchy && rate.text.Contains("초당 실타래"), $"수선점 카드 줄 '{rate?.text}'");
                var tag = Find(LineageTagPath);
                p.Expect(tag != null && tag.gameObject.activeInHierarchy, "강화 탭 꼬리표 켜짐 (유실자 레벨업 열림)");
                var card = Find(CoachTargets.GrowthPanel + "/LineageList");
                var costIcon = card != null ? card.GetComponentInChildren<GrowthLineageCardView>(true)?.transform.Find("LevelUpButton/Cost/LineageXpIcon") : null;
                p.Expect(costIcon != null, "친구 카드 비용 그림 = 실타래");
                Report("S-LXP-growth", "growth_lineage_xp", p);
            });

            double before = RewardFlyer.PendingOf(CurrencyType.LineageXp);
            RewardFlyer.FlyCurrency(CurrencyType.LineageXp, 500, null);
            double pending = RewardFlyer.PendingOf(CurrencyType.LineageXp);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot("growth_lineage_xp_fly", () =>
            {
                var p = new Probe();
                p.Expect(pending > before, $"날아가는 실타래 {pending:0} (잔액 칩이 보이면 칩으로 간다)");
                p.Info($"보이는 숫자 {LineageXpBalanceView.Text(CurrencyType.LineageXp, false)} · 잔액 {Game.Currency.Get(CurrencyType.LineageXp):0}");
                Report("S-LXP-fly", "growth_lineage_xp_fly", p);
            });
            yield return new WaitForSecondsRealtime(1.2f);

            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("main_lineage_tag", () =>
            {
                var p = new Probe();
                var tag = Find(LineageTagPath);
                var amount = tag != null ? tag.Find("Amount")?.GetComponent<TMP_Text>() : null;
                p.Expect(tag != null && tag.gameObject.activeInHierarchy, "MAIN에서 강화 탭 꼬리표 보임");
                p.Expect(amount != null && !string.IsNullOrEmpty(amount.text), $"꼬리표 숫자 '{amount?.text}'");
                Report("S-LXP-main", "main_lineage_tag", p);
            });
        }

        /// <summary>16b tab_Gacha_short (M07 · M08): 별조각 0 → 별조각 얻는 곳 링크가 켜지고 10회 소환 비용 명암 ≥ 4.5. 끝나면 되돌린다</summary>
        IEnumerator GachaShort()
        {
            CloseAll();
            double gems = Game.Currency.Get(CurrencyType.Gem);
            Game.Currency.TrySpend(CurrencyType.Gem, gems);
            try
            {
                _tabs.Select(MainTab.Gacha);
                yield return WaitSettled();
                yield return new WaitForSecondsRealtime(0.4f);
                var view = FindCostView(GachaPull10Path);
                yield return ShotWithFrame("tab_Gacha_short", frame =>
                {
                    var p = new Probe();
                    var link = Find(GachaGemLinkPath);
                    p.Expect(link != null && link.gameObject.activeInHierarchy, "GemSourceLink 켜짐 (모자랄 때만)");
                    ExpectShortCost(p, view, frame, "10회 소환");
                    Report("S-16b", "tab_Gacha_short", p);
                });
            }
            finally
            {
                Game.Currency.Add(CurrencyType.Gem, gems);
            }
        }

        CostButtonView FindCostView(string path)
        {
            var target = Find(path);
            if (target == null) return null;
            var view = target.GetComponent<CostButtonView>();
            return view != null ? view : target.GetComponentInParent<CostButtonView>();
        }

        void ExpectShortCost(Probe p, CostButtonView view, Frame frame, string name)
        {
            if (view == null)
            {
                p.Expect(false, $"{name} CostButtonView 없음");
                return;
            }
            p.Expect(view.CurrentState == CostButtonView.State.Short, $"{name} 상태 {view.CurrentState} (기대 Short)");
            var pill = Field<Image>(view, "_costPill");
            p.Expect(pill != null && pill.isActiveAndEnabled && pill.color.a > 0.3f, $"{name} 비용 알약이 보인다 (CostPillBack)");
            var skin = Field<Image>(view, "_skin");
            var unavailable = Field<Sprite>(view, "_unavailableSprite");
            p.Expect(skin != null && skin.sprite != null && skin.sprite != unavailable, $"{name} 버튼 그림 그대로 ({(skin != null && skin.sprite != null ? skin.sprite.name : "없음")})");
            var cost = Field<TMP_Text>(view, "_costText");
            float ratio = ContrastOf(cost, frame);
            p.Expect(!float.IsNaN(ratio) && ratio >= VerifyMath.ContrastText, $"{name} 비용 '{(cost != null ? cost.text : "")}' 명암 {ratio:0.00} (기대 ≥ {VerifyMath.ContrastText})");
        }

        /// <summary>
        /// 16c gacha_confirm (M55): 10회 소환 버튼 onClick → 쓰기 확인 (비용 줄 켜짐 · 확인 글 '소환'), 아직 쓰지 않음. 취소로 닫는다.
        /// '오늘 다시 묻지 않기'(PlayerPrefs)는 찍는 동안만 비우고 끝나면 처음 모양으로 되돌린다 (있었으면 그 값, 없었으면 지움 —
        /// GetString(키, null)은 null 기본값이 ""로 바뀌어 올 수 있어 '있었는지'를 알 수 없다 → HasKey로 본다)
        /// </summary>
        IEnumerator GachaConfirm()
        {
            CloseAll();
            Game.Currency.Add(CurrencyType.Gem, 1000);
            bool skipHad = PlayerPrefs.HasKey(GachaPanel.Pull10SkipKey);
            string skipWas = skipHad ? PlayerPrefs.GetString(GachaPanel.Pull10SkipKey) : null;
            if (skipHad) PlayerPrefs.DeleteKey(GachaPanel.Pull10SkipKey);
            try
            {
                _tabs.Select(MainTab.Gacha);
                yield return WaitSettled();
                var button = Comp<Button>(GachaPull10Path);
                var message = UIManager.Instance.Get<MessagePopup>();
                if (button == null || message == null)
                {
                    Check("S-16c", false, "gacha_confirm: Pull10Button · MessagePopup 없음");
                    yield break;
                }
                double gems = Game.Currency.Get(CurrencyType.Gem);
                button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("gacha_confirm", () =>
                {
                    var p = new Probe();
                    p.Expect(message.IsOpen, "10회 버튼은 쓰기 확인을 연다");
                    var row = Field<RectTransform>(message, "_costRow");
                    p.Expect(row != null && row.gameObject.activeInHierarchy, "비용 줄(CostRow) 켜짐");
                    var ok = Field<TMP_Text>(message, "_okLabel");
                    p.Expect(ok != null && ok.text == "소환", $"확인 글 '{(ok != null ? ok.text : "")}' (기대 '소환')");
                    p.Near("확인 전 별조각 변화", (float)(Game.Currency.Get(CurrencyType.Gem) - gems), 0f, 0.5f);
                    Report("S-16c", "gacha_confirm", p);
                });
            }
            finally
            {
                CloseAll();
                if (skipHad) PlayerPrefs.SetString(GachaPanel.Pull10SkipKey, skipWas);
                else if (PlayerPrefs.HasKey(GachaPanel.Pull10SkipKey)) PlayerPrefs.DeleteKey(GachaPanel.Pull10SkipKey);
            }
        }

        // ═════════════════════════ 보상 두 버튼 · 의상 탭 (18b) · 소환 결과 덮개 (20b) ═════════════════════════

        /// <summary>
        /// reward_two_button (C-04 · 의상 구매): RewardPopup 보조 버튼 '바로 입혀 보기' + 확인 — 같은 폭(380) · 사이 24 · 보조가 왼쪽.
        /// 보조 버튼 → character_costume_nav (18b, M73): 친구 상세가 의상 탭(TabCostume)으로 열린다
        /// </summary>
        IEnumerator RewardTwoButton()
        {
            CloseAll();
            var popup = UIManager.Instance.Get<RewardPopup>();
            var lineage = Game.Characters?.Db?.GetLineage(FtueFriend);
            var costume = lineage != null && lineage.costumes != null && lineage.costumes.Count > 0 ? lineage.costumes[0] : null;
            if (popup == null || costume == null)
            {
                SkipCheck("S-reward2", $"reward_two_button: RewardPopup · {FtueFriend} 의상 없음");
                yield break;
            }
            popup.Show("의상 획득!", $"{MenuText.Object(costume.name)} 손에 넣었어요.", null, costume, true, "바로 입혀 보기",
                () => UINav.OpenCharacter(FtueFriend, CharacterDetailPopup.TabCostume));
            yield return new WaitForSecondsRealtime(1.2f);
            var action = Field<Button>(popup, "_actionButton");
            var confirm = Field<Button>(popup, "_confirmButton");
            yield return Shot("reward_two_button", () =>
            {
                var p = new Probe();
                p.Expect(action != null && action.gameObject.activeInHierarchy, "보조 버튼 '바로 입혀 보기' 켜짐");
                p.Expect(confirm != null && confirm.gameObject.activeInHierarchy, "확인 버튼 켜짐 (이름 ConfirmButton)");
                if (action != null && confirm != null && action.gameObject.activeInHierarchy)
                {
                    var a = ScreenRect((RectTransform)action.transform);
                    var c = ScreenRect((RectTransform)confirm.transform);
                    p.Near("보조 폭", a.width, 380f, 2f);
                    p.Near("확인 폭", c.width, 380f, 2f);
                    p.Near("사이", c.xMin - a.xMax, 24f, 2f);
                    p.Near("같은 줄", a.center.y - c.center.y, 0f, 1f);
                }
                Report("S-reward2", "reward_two_button", p);
            });
            if (action == null || !action.gameObject.activeInHierarchy)
            {
                CloseAll();
                yield break;
            }
            action.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.2f);
            var detail = UIManager.Instance.Get<CharacterDetailPopup>();
            yield return Shot("character_costume_nav", () =>
            {
                var p = new Probe();
                p.Expect(!popup.IsOpen, "보상 창은 닫혔다");
                p.Expect(detail != null && detail.IsOpen, "친구 상세가 열렸다");
                var pages = detail != null ? Field<GameObject[]>(detail, "_tabPages") : null;
                bool costumePage = pages != null && pages.Length > CharacterDetailPopup.TabCostume && pages[CharacterDetailPopup.TabCostume] != null &&
                                   pages[CharacterDetailPopup.TabCostume].activeInHierarchy;
                p.Expect(costumePage, "의상 탭 쪽이 켜져 있다 (TabCostume)");
                Report("S-18b", "character_costume_nav", p);
            });
            CloseAll();
        }

        /// <summary>20b gacha_result_dim (M49): 10회 결과를 연 그 프레임에 RequestDismiss → 닫히지 않는다(연출 건너뛰기) · 한 번 더 → 닫힌다</summary>
        IEnumerator GachaResultDim()
        {
            CloseAll();
            var panel = FindAnyObjectByType<GachaPanel>();
            var result = UIManager.Instance.Get<GachaResultPopup>();
            if (panel == null || result == null)
            {
                SkipCheck("S-20b", "gacha_result_dim: 소환 화면 · 결과 창 없음 (소환 시트가 열려 있어야 한다)");
                yield break;
            }
            Game.Currency.Add(CurrencyType.Gem, 1000);
            panel.Pull(10);
            bool opened = result.IsOpen;
            result.RequestDismiss();
            bool afterFirst = result.IsOpen;
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("gacha_result_dim", () =>
            {
                result.RequestDismiss();
                Check("S-20b", opened && afterFirst && !result.IsOpen,
                    $"gacha_result_dim: 열림 {opened} · 첫 닫기 뒤 {OpenText(afterFirst)} (기대 열림 — 건너뛰기) · 두 번째 닫기 뒤 {OpenText(result.IsOpen)} (기대 닫힘)");
            });
            CloseAll();
        }

        // ═════════════════════════ 메타 (27b) ═════════════════════════

        /// <summary>
        /// 27b unlock_decline_fly (M78): MAIN에서 수선 일지 개방 카드를 열고 '나중에' → 0.3초 — 카드 그림이 기능 자리(빠른 아이콘 수선 일지)로 날아가는 중
        /// (FeatureUnlockCard.IsFlying · 그림이 그 자리로 다가간다). 시트가 열려 있으면 자리가 사냥 버튼이 되므로 먼저 MAIN으로 간다 (§5.6 FeatureSlot)
        /// </summary>
        IEnumerator UnlockDeclineFly()
        {
            var card = UIManager.Instance.Get<FeatureUnlockCard>();
            if (card == null)
            {
                SkipCheck("S-27b", "unlock_decline_fly: FeatureUnlockCard 없음");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            card.Show(FeatureId.Missions);
            yield return new WaitForSecondsRealtime(1.2f);
            if (!card.IsOpen)
            {
                SkipCheck("S-27b", "unlock_decline_fly: 개방 카드가 열리지 않았다");
                yield break;
            }
            var later = Field<Button>(card, "_laterButton");
            var art = Field<Image>(card, "_illustration");
            string slotPath = MetaNavigator.FeatureSlot(FeatureId.Missions);
            var slot = Find(slotPath);
            if (later == null || slot == null)
            {
                Check("S-27b", false, $"unlock_decline_fly: 나중에 버튼 · 자리 {slotPath} 없음");
                yield break;
            }
            var sprite = art != null ? art.sprite : null;
            later.onClick.Invoke();
            yield return null;
            float first = FlightDistance(sprite, slot);
            yield return new WaitForSecondsRealtime(0.28f);
            yield return Shot("unlock_decline_fly", () =>
            {
                var p = new Probe();
                p.Expect(slotPath == CoachTargets.JournalButton, $"날아갈 자리 {slotPath} (기대 {CoachTargets.JournalButton})");
                p.Expect(card.IsFlying, "카드가 날아가는 중 (IsFlying)");
                float now = FlightDistance(sprite, slot);
                p.Expect(!float.IsNaN(now) && !float.IsNaN(first) && now < first - 1f, $"그림 → 수선 일지 거리 {first:0} → {now:0} (줄어든다)");
                Report("S-27b", "unlock_decline_fly", p);
            });
        }

        /// <summary>RewardFlyer 층에서 지금 날고 있는(켜진 · 그림 있는) 아이콘 수 — 재화가 아닌 보상(재료)도 센다 (PendingChanged는 재화만)</summary>
        static int ActiveFlightIcons()
        {
            var flyer = FindAnyObjectByType<RewardFlyer>();
            if (flyer == null) return 0;
            int count = 0;
            for (int i = 0; i < flyer.transform.childCount; i++)
            {
                var child = flyer.transform.GetChild(i);
                if (child.gameObject.activeSelf && child.TryGetComponent<Image>(out var image) && image.sprite != null && image.enabled) count++;
            }
            return count;
        }

        /// <summary>RewardFlyer 층에서 그 그림을 단 켜진 아이콘 중 자리와 가장 가까운 것의 거리 (기준 px, 없으면 NaN)</summary>
        float FlightDistance(Sprite sprite, RectTransform slot)
        {
            var flyer = FindAnyObjectByType<RewardFlyer>();
            if (flyer == null || slot == null) return float.NaN;
            var target = ScreenRect(slot).center;
            float best = float.NaN;
            foreach (var image in flyer.GetComponentsInChildren<Image>(false))
            {
                if (sprite != null && image.sprite != sprite) continue;
                float distance = Vector2.Distance(ScreenRect(image.rectTransform).center, target);
                if (float.IsNaN(best) || distance < best) best = distance;
            }
            return best;
        }

        // ═════════════════════════ 원정 (04c · 15b) ═════════════════════════

        /// <summary>
        /// 04c expedition_setup (M05 — 튜토리얼 13단계 대신 튜토리얼 뒤 첫 원정에서): 첫 열린 구역 '보내기' → 준비 팝업이 미리 채워져 고를 수 있는 칸 ≥ 1 ·
        /// 출발 누를 수 있음. 15b expedition_resend (M57 · M20): 출발 → 끝 시각을 당겨 받기 → 보상 창 확인이 날아가기를 시작하고 카드 글이 '다시 보내기'
        /// </summary>
        IEnumerator Expedition()
        {
            CloseAll();
            _tabs.Select(MainTab.Expedition);
            yield return WaitSettled();
            var panel = FindAnyObjectByType<ExpeditionPanel>();
            var seg = panel != null ? Field<Button>(panel, "_expeditionSeg") : null;
            if (seg != null) seg.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            ExpeditionZoneCardView card = null;
            Button send = null;
            if (panel != null)
            {
                foreach (var candidate in panel.GetComponentsInChildren<ExpeditionZoneCardView>(false))
                {
                    var button = Field<Button>(candidate, "_sendButton");
                    if (candidate.Zone == null || button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                    if (Game.Expedition.GetActive(candidate.Zone.id) != null) continue;
                    card = candidate;
                    send = button;
                    break;
                }
            }
            if (card == null)
            {
                SkipCheck("S-04c", "expedition_setup: 보낼 수 있는 구역 카드 없음");
                yield break;
            }
            string zoneId = card.Zone.id;
            var setup = UIManager.Instance.Get<ExpeditionSetupPopup>();
            send.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.8f);
            var start = setup != null ? Field<Button>(setup, "_startButton") : null;
            yield return Shot("expedition_setup", () =>
            {
                var p = new Probe();
                p.Expect(setup != null && setup.IsOpen, "원정 준비 팝업 열림");
                int selectable = 0;
                var content = setup != null ? Field<RectTransform>(setup, "_memberContent") : null;
                if (content != null)
                {
                    foreach (var cell in content.GetComponentsInChildren<ExpeditionMemberCellView>(false))
                    {
                        var cellButton = Field<Button>(cell, "_button");
                        if (cellButton != null && cellButton.interactable) selectable++;
                    }
                }
                p.Expect(selectable >= 1, $"고를 수 있는 친구 칸 {selectable}개 (기대 ≥ 1)");
                p.Expect(start != null && start.interactable, "미리 채워져 출발을 누를 수 있다");
                Report("S-04c", "expedition_setup", p);
            });
            if (setup == null || !setup.IsOpen || start == null || !start.interactable)
            {
                CloseAll();
                yield break;
            }

            start.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            var active = Game.Expedition.GetActive(zoneId);
            if (active == null)
            {
                Check("S-15b", false, $"expedition_resend: 출발 뒤 {zoneId} 원정이 없다");
                yield break;
            }
            active.endUnix = active.startUnix;
            yield return new WaitForSecondsRealtime(1.5f);
            var claim = Field<Button>(card, "_claimButton");
            if (claim == null || !claim.gameObject.activeInHierarchy)
            {
                Check("S-15b", false, "expedition_resend: 끝난 원정의 받기 버튼이 보이지 않는다");
                yield break;
            }
            claim.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.8f);
            var reward = UIManager.Instance.Get<RewardPopup>();
            var confirm = reward != null ? Field<Button>(reward, "_confirmButton") : null;
            bool rewardOpen = reward != null && reward.IsOpen;
            bool flew = false;
            void OnPending(CurrencyType currency) => flew = true;
            RewardFlyer.PendingChanged += OnPending;
            try
            {
                if (confirm != null && rewardOpen) confirm.onClick.Invoke();
                yield return null;
                if (ActiveFlightIcons() > 0) flew = true;
                yield return new WaitForSecondsRealtime(0.6f);
                var label = Field<TMP_Text>(card, "_sendLabel");
                yield return Shot("expedition_resend", () =>
                {
                    var p = new Probe();
                    p.Expect(rewardOpen, "받기 → 보상 창 '원정 완료'");
                    p.Expect(flew, "보상 창 확인이 보상을 날렸다 (RewardFlyer)");
                    p.Expect(label != null && label.text == "다시 보내기", $"카드 글 '{(label != null ? label.text : "")}' (기대 '다시 보내기')");
                    Report("S-15b", "expedition_resend", p);
                });
            }
            finally
            {
                RewardFlyer.PendingChanged -= OnPending;
            }
            CloseAll();
        }

        // ═════════════════════════ 팝업 (28b · 28c · 28d) ═════════════════════════

        /// <summary>28b shortfall (M08): 별조각 350에서 ShowShortfall(별조각, 900) → '550 더 필요해요', 받지 않은 별조각 우편이 있으면 첫 줄이 우편함. 되돌린다</summary>
        IEnumerator Shortfall()
        {
            CloseAll();
            double gems = Game.Currency.Get(CurrencyType.Gem);
            if (gems > 350d) Game.Currency.TrySpend(CurrencyType.Gem, gems - 350d);
            else Game.Currency.Add(CurrencyType.Gem, 350d - gems);
            try
            {
                UIManager.Instance.ShowShortfall(CurrencyType.Gem, 900d);
                yield return new WaitForSecondsRealtime(0.8f);
                var popup = UIManager.Instance.Get<ShortfallPopup>();
                yield return Shot("shortfall", () =>
                {
                    var p = new Probe();
                    p.Expect(popup != null && popup.IsOpen, "부족 팝업 열림");
                    var gap = popup != null ? Field<TMP_Text>(popup, "_gapText") : null;
                    p.Expect(gap != null && gap.gameObject.activeInHierarchy && gap.text == "550 더 필요해요", $"모자란 줄 '{(gap != null ? gap.text : "")}' (기대 '550 더 필요해요')");
                    var sources = CurrencySources.For(CurrencyType.Gem);
                    bool mail = CurrencySources.UnclaimedMailAmount(CurrencyType.Gem) > 0d;
                    if (mail) p.Expect(sources.Count > 0 && sources[0].Kind == CurrencySourceKind.Mail, $"받지 않은 별조각 우편이 있으면 첫 줄이 우편함 (첫 줄 {(sources.Count > 0 ? sources[0].Kind.ToString() : "없음")})");
                    else p.Info("받지 않은 별조각 우편 없음 — 첫 줄은 보지 않는다");
                    Report("S-28b", "shortfall", p);
                });
            }
            finally
            {
                CloseAll();
                double now = Game.Currency.Get(CurrencyType.Gem);
                if (gems > now) Game.Currency.Add(CurrencyType.Gem, gems - now);
            }
        }

        /// <summary>28c popup_stack (M48 · M15): 설정 위에 확인 창 → 위 덮개 α ≈ 0.4 · 아래 덮개 1 · 확인 창은 SystemPopups(Popups 위) 층</summary>
        IEnumerator PopupStack()
        {
            CloseAll();
            var settings = UIManager.Instance.Get<SettingsPopup>();
            var message = UIManager.Instance.Get<MessagePopup>();
            if (settings == null || message == null)
            {
                SkipCheck("S-28c", "popup_stack: 설정 · 확인 팝업 없음");
                yield break;
            }
            settings.Open();
            yield return new WaitForSecondsRealtime(0.5f);
            UIManager.Instance.ShowConfirm("잠깐만요", "겹친 팝업 덮개를 확인해요", null);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("popup_stack", () =>
            {
                var p = new Probe();
                p.Expect(settings.IsOpen && message.IsOpen, "설정 + 확인 창 열림");
                var upper = Field<CanvasGroup>(message, "_dimGroup");
                var lower = Field<CanvasGroup>(settings, "_dimGroup");
                if (upper != null) p.Near("위 덮개 α", upper.alpha, UIMotion.DimStacked, 0.03f);
                else p.Expect(false, "확인 창 덮개 CanvasGroup 없음");
                if (lower != null) p.Near("아래 덮개 α", lower.alpha, UIMotion.DimSingle, 0.03f);
                var system = message.transform.parent;
                var popups = settings.transform.parent;
                p.Expect(system != null && popups != null && system.name == "SystemPopups" && system.GetSiblingIndex() > popups.GetSiblingIndex(),
                    $"확인 창 층 {(system != null ? system.name : "?")} #{(system != null ? system.GetSiblingIndex() : -1)} · 팝업 층 {(popups != null ? popups.name : "?")} #{(popups != null ? popups.GetSiblingIndex() : -1)} (기대 SystemPopups가 위)");
                Report("S-28c", "popup_stack", p);
            });
            CloseAll();
        }

        /// <summary>28d mail (M20 · M51 · M76): 우편함 — 모두 받기 Gold 380x96, 한 줄 받기가 날아가기를 시작하고, 비면 '…없어요'</summary>
        IEnumerator Mail()
        {
            CloseAll();
            var popup = UIManager.Instance.Get<MailPopup>();
            if (popup == null)
            {
                SkipCheck("S-28d", "mail: MailPopup 없음");
                yield break;
            }
            popup.Open();
            yield return new WaitForSecondsRealtime(0.8f);
            var claimAll = Field<Button>(popup, "_claimAllButton");
            var p = new Probe();
            if (claimAll != null && claimAll.gameObject.activeInHierarchy)
            {
                var rect = ScreenRect((RectTransform)claimAll.transform);
                p.Near("모두 받기 폭", rect.width, 380f, 2f);
                p.Near("모두 받기 높이", rect.height, 96f, 2f);
                var skin = claimAll.targetGraphic as Image;
                bool gold = skin != null && ((skin.sprite != null && skin.sprite.name.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (skin.sprite == null && Mathf.Abs(skin.color.r - UITheme.Gold.r) < 0.05f && Mathf.Abs(skin.color.g - UITheme.Gold.g) < 0.05f));
                p.Expect(gold, $"모두 받기는 Gold ({(skin != null && skin.sprite != null ? skin.sprite.name : "그림 없음")})");
            }
            else p.Info("모두 받기 꺼짐 (받을 우편이 하나 이하)");
            MailItemView row = null;
            Button rowClaim = null;
            foreach (var item in popup.GetComponentsInChildren<MailItemView>(false))
            {
                var button = Field<Button>(item, "_claimButton");
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                row = item;
                rowClaim = button;
                break;
            }
            bool flew = false;
            void OnPending(CurrencyType currency) => flew = true;
            RewardFlyer.PendingChanged += OnPending;
            try
            {
                if (rowClaim != null) rowClaim.onClick.Invoke();
                yield return null;
                if (rowClaim != null && ActiveFlightIcons() > 0) flew = true;
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("mail", () =>
                {
                    if (row == null) p.Info("받을 우편 줄 없음");
                    else p.Expect(flew, "한 줄 받기가 보상을 날렸다 (RewardFlyer — 그 줄에서)");
                    var empty = Field<GameObject>(popup, "_emptyLabel");
                    if (Game.Mail.Count == 0)
                    {
                        string text = empty != null ? FindText(empty.transform, "없어요") : null;
                        p.Expect(empty != null && empty.activeInHierarchy && text != null, $"빈 우편함 글 '{text}' (기대 '…없어요')");
                    }
                    else p.Info($"남은 우편 {Game.Mail.Count}통");
                    Report("S-28d", "mail", p);
                });
            }
            finally
            {
                RewardFlyer.PendingChanged -= OnPending;
            }
            CloseAll();
        }

        // ═════════════════════════ Wave 3 남은 장면 (전체 판 끝 — RM-2 V2) ═════════════════════════

        /// <summary>전체 판 맨 끝 (battle_later 뒤): 앞 장면 번호를 그대로 두고 W3 요청 장면을 붙인다</summary>
        IEnumerator Wave3Tail()
        {
            yield return Safe("battle_clear_drop", BattleClearDrop());
            yield return Safe("battle_notice", BattleNoticeLong());
            yield return Safe("battle_lamp_sum", BattleLampSum());
            yield return Safe("growth_max", GrowthMaxLevel());
        }

        /// <summary>
        /// battle_clear_drop (W3 요청 — 11c 실제 클리어 · 보상 넷): 천 · 리본 확률을 잠깐 1로 두고 지금 스테이지를 실제로 클리어시킨다 (BattleSimulator.Clear — 비공개).
        /// StageEnded 보상이 단추 · 별빛 사탕 · 천 · 리본 넷(사탕이 0이면 셋)이고, 클리어 리본이 자리(화면 372~474 · 가운데 516)에 떠
        /// StagePanel · 추천 · 시너지 줄 · 보스 버튼과 겹치지 않는다. 확률은 끝에 되돌린다 (스테이지는 실제로 넘어간다 — 판 끝이라 그대로 둔다)
        /// </summary>
        IEnumerator BattleClearDrop()
        {
            var battle = Game.Battle;
            var config = battle != null ? battle.Config : null;
            var clear = typeof(BattleSimulator).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
            var banner = Find(BannerPath);
            if (battle == null || config == null || clear == null || banner == null)
            {
                SkipCheck("S-11c-drop", $"battle_clear_drop: 전투 · BattleSimulator.Clear · {BannerPath} 없음");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            int stage = 0;
            int expected = 0;
            List<RewardData> got = null;
            void OnEnded(BattleOutcome outcome, List<RewardData> rewards)
            {
                if (outcome == BattleOutcome.Cleared && rewards != null) got = new List<RewardData>(rewards);
            }
            string popupBefore = null, popupAfter = null, retried = null;
            for (int attempt = 0; attempt < ClearDropAttempts; attempt++)
            {
                float drain = Time.realtimeSinceStartup + 8f;
                while (Time.realtimeSinceStartup < drain && (banner.gameObject.activeInHierarchy || battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running)) yield return null;
                if (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running)
                {
                    SkipCheck("S-11c-drop", $"battle_clear_drop: 8초 안에 스테이지가 돌지 않았다 ({battle.Sim.Outcome}){(retried != null ? $" · {retried}" : string.Empty)}");
                    yield break;
                }
                stage = battle.Stage;
                expected = battle.GetClearRewards(stage).Count + 2;
                float cloth = config.stageClothChance, ribbon = config.stageRibbonChance;
                got = null;
                // 리본은 팝업이 열려 있으면 버려진다 (BattleBannerView: Ribbon && PopupOpen → 띄우지 않음) — 기다리는 동안 뜬 창(이벤트 시작 카드 등)을 클리어 바로 앞에 닫는다
                // (V2 r4: 이벤트 시작 카드가 떠 있는 채 클리어해 리본이 없었는데, 예전 날 글 검사는 PASS였다)
                popupBefore = UIManager.Instance != null && UIManager.Instance.TopPopup != null ? UIManager.Instance.TopPopup.GetType().Name : null;
                CloseAll();
                battle.StageEnded += OnEnded;
                try
                {
                    config.stageClothChance = 1f;
                    config.stageRibbonChance = 1f;
                    clear.Invoke(battle.Sim, null);
                    float limit = Time.realtimeSinceStartup + 2f;
                    while (got == null && Time.realtimeSinceStartup < limit) yield return null;
                }
                finally
                {
                    battle.StageEnded -= OnEnded;
                    config.stageClothChance = cloth;
                    config.stageRibbonChance = ribbon;
                }
                // 게임이 띄우는 리본이라 보이는(그려진 · 알파) 글로 기다린다 — 날 글은 앞 리본의 낡은 제목을 잡을 수 있다
                float show = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < show && !(banner.gameObject.activeInHierarchy && FindText(banner, "클리어") != null)) yield return null;
                popupAfter = UIManager.Instance != null && UIManager.Instance.TopPopup != null ? UIManager.Instance.TopPopup.GetType().Name : null;
                bool ribbonUp = banner.gameObject.activeInHierarchy && FindText(banner, "클리어") != null;
                if (ribbonUp || popupAfter == null || attempt == ClearDropAttempts - 1) break;
                // 이 클리어가 연 창(기능 열림 · 이벤트 시작 카드)이 리본을 막았다 — 게임 규칙(창이 열려 있으면 리본을 버린다)이라 창을 닫고 다음 스테이지에서 한 번 더 (V2 r6)
                retried = $"{BattleSystem.FormatStage(stage)} 클리어 뒤 {popupAfter}가 떠 리본이 버려짐 → 창을 닫고 다음 스테이지에서 다시";
                CloseAll();
                yield return WaitSettled();
            }
            yield return new WaitForSecondsRealtime(RibbonShotDelay);
            yield return Shot("battle_clear_drop", () =>
            {
                var p = new Probe();
                if (retried != null) p.Info(retried);
                if (popupBefore != null) p.Info($"클리어 앞에 닫은 창 {popupBefore}");
                if (popupAfter != null) p.Info($"클리어 뒤 열린 창 {popupAfter} (리본을 막는다)");
                var names = new StringBuilder();
                if (got != null)
                {
                    foreach (var reward in got) names.Append(names.Length > 0 ? ", " : string.Empty).Append(reward.type == RewardType.Currency ? reward.currency.ToString() : reward.itemId).Append(' ').Append(reward.amount);
                }
                p.Expect(got != null && got.Count == expected, $"스테이지 {BattleSystem.FormatStage(stage)} 클리어 보상 {(got != null ? got.Count : -1)}개 (기대 {expected} — 확정 + 천 + 리본) — {names}");
                bool shown = banner.gameObject.activeInHierarchy && FindText(banner, "클리어") != null;
                p.Expect(shown, "클리어 리본이 떠 있다");
                if (shown)
                {
                    var rect = ScreenRect(banner);
                    var zone = RibbonZone();
                    p.Near("리본 위", rect.yMin, zone.yMin, 4f);
                    p.Near("리본 아래", rect.yMax, zone.yMax, 4f);
                    p.Near("리본 가운데 x", rect.center.x, zone.center.x, 4f);
                    ExpectClear(p, rect, CoachTargets.StagePanel);
                    ExpectClear(p, rect, RecommendedPowerPath);
                    ExpectClear(p, rect, SynergyRowPath);
                    var boss = FindDeep(Find(BattlePath), BossButtonName);
                    if (boss != null) ExpectClear(p, rect, PathOf(boss));
                    var lines = new StringBuilder();
                    foreach (var text in banner.GetComponentsInChildren<TMP_Text>(false))
                    {
                        string line = ShownText(text);
                        if (!string.IsNullOrEmpty(line)) lines.Append(lines.Length > 0 ? " / " : string.Empty).Append(line);
                    }
                    p.Info($"리본 {R(rect)} '{lines}'");
                }
                Report("S-11c-drop", "battle_clear_drop", p);
            });
        }

        /// <summary>
        /// battle_notice (W3 요청): 가장 긴 스테이지 기믹 안내 + 좀나방 안내를 잇달아 띄운다 (BattleNoticeView.Show — 실제 안내와 같은 길).
        /// 두 띠가 보이고, 서로 겹치지 않고, 화면 안(가장자리 8 이상)이고, 제목 · 설명이 잘리지 않는다 (isTextTruncated). HUD 칸과 겹침은 기록만
        /// </summary>
        IEnumerator BattleNoticeLong()
        {
            var view = FindAnyObjectByType<BattleNoticeView>();
            if (view == null)
            {
                SkipCheck("S-notice", "battle_notice: BattleNoticeView 없음");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            // 스테이지 시작 안내(기믹 · 보스)가 띠 칸을 가져가지 않게: 판이 돌기 시작하고(앞 장면이 클리어를 만들었다) 시작 안내가 지나간 뒤에 띄운다
            var battle = Game.Battle;
            float running = Time.realtimeSinceStartup + 8f;
            while (battle != null && Time.realtimeSinceStartup < running && (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running)) yield return null;
            yield return new WaitForSecondsRealtime(3.5f);
            view.Clear();
            var longest = StageModifierType.Rain;
            foreach (StageModifierType modifier in Enum.GetValues(typeof(StageModifierType)))
            {
                if (modifier == StageModifierType.None) continue;
                if (SkillVfxLook.ModifierDescription(modifier).Length > SkillVfxLook.ModifierDescription(longest).Length) longest = modifier;
            }
            string modifierTitle = UITheme.StageModifierName(longest);
            view.Show(modifierTitle, SkillVfxLook.ModifierDescription(longest), null, Color.white, UITheme.Gold, 3f);
            yield return new WaitForSecondsRealtime(0.15f);
            view.Show("좀나방은 천을 갉아요!", "천으로 된 친구는 좀나방에게 약해요. 멀리서 먼저 쓰러뜨려 주세요", null, Color.white, UITheme.Danger, 3f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("battle_notice", () =>
            {
                var p = new Probe();
                var rows = Field<RectTransform[]>(view, "_rows");
                var groups = Field<CanvasGroup[]>(view, "_groups");
                var titles = Field<TMP_Text[]>(view, "_titles");
                var subtitles = Field<TMP_Text[]>(view, "_subtitles");
                if (rows == null || titles == null || subtitles == null)
                {
                    p.Expect(false, "BattleNoticeView._rows · _titles · _subtitles 없음");
                    Report("S-notice", "battle_notice", p);
                    return;
                }
                var shown = new List<(string name, Rect rect)>();
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] == null || !rows[i].gameObject.activeInHierarchy) continue;
                    if (groups != null && i < groups.Length && groups[i] != null && groups[i].alpha < 0.5f)
                    {
                        p.Info($"띠 {i} '{(i < titles.Length ? ShownText(titles[i]) : string.Empty)}' 흐림 α {groups[i].alpha:0.00}");
                        continue;
                    }
                    var rect = ScreenRect(rows[i]);
                    string title = i < titles.Length ? ShownText(titles[i]) : string.Empty;
                    shown.Add(($"띠 {i} '{title}'", rect));
                    p.Expect(rect.xMin >= FrameMinX + NoticeEdge - 0.5f && rect.xMax <= FrameMaxX - NoticeEdge + 0.5f, $"띠 {i} {R(rect)} 틀 기둥 안 (가장자리 ≥ {NoticeEdge:0})");
                    if (i < titles.Length && titles[i] != null) p.Expect(!titles[i].isTextTruncated, $"띠 {i} 제목 '{title}' 안 잘림");
                    if (i < subtitles.Length && subtitles[i] != null && subtitles[i].isActiveAndEnabled)
                        p.Expect(!subtitles[i].isTextTruncated, $"띠 {i} 설명 '{ShownText(subtitles[i])}' 안 잘림 (글자 {subtitles[i].fontSize:0.#})");
                }
                p.Expect(shown.Count >= 2, $"보이는 안내 띠 {shown.Count}개 (기대 ≥ 2 — '{modifierTitle}' · 좀나방)");
                for (int a = 0; a < shown.Count; a++)
                {
                    for (int b = a + 1; b < shown.Count; b++)
                    {
                        if (VerifyMath.Overlaps(shown[a].rect, shown[b].rect)) p.Expect(false, $"{shown[a].name} {R(shown[a].rect)} · {shown[b].name} {R(shown[b].rect)} 겹침");
                    }
                }
                foreach (var (cell, cellRect) in VisibleHudCells())
                {
                    foreach (var (name, rect) in shown)
                    {
                        if (VerifyMath.Overlaps(rect, cellRect)) p.Info($"{name}이 HUD 칸 {cell} {R(cellRect)}와 겹침 (기록만)");
                    }
                }
                Report("S-notice", "battle_notice", p);
            });
            view.Clear();
        }

        /// <summary>
        /// battle_lamp_sum (W3 요청 — '+합계' 판을 말풍선 겹침 검사에): 적이 여덟 넘게 보일 때(늦어도 12초) 불빛을 채워 밝힌다 (BattleSimulator._lamp — 비공개,
        /// FtueBattleGuard와 같은 필드). 합계 판이 뜨면(3초 안) 찍고 말풍선 규칙(S-06…S-11과 같은 + 합계 판 비킴)을 본다. 판이 안 뜨면 SKIP (빛 물결 숫자 8개 미만)
        /// </summary>
        IEnumerator BattleLampSum()
        {
            var battle = Game.Battle;
            var lamp = typeof(BattleSimulator).GetField("_lamp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (battle == null || battle.Sim == null || lamp == null)
            {
                SkipCheck("S-lampsum", "battle_lamp_sum: 전투 · BattleSimulator._lamp 없음");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            float crowd = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < crowd && (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running || TargetableEnemies() < 8)) yield return null;
            if (battle.IsBetweenStages || battle.Sim.Outcome != BattleOutcome.Running)
            {
                SkipCheck("S-lampsum", $"battle_lamp_sum: 스테이지가 돌지 않는다 ({battle.Sim.Outcome})");
                yield break;
            }
            int enemies = TargetableEnemies();
            lamp.SetValue(battle.Sim, 1f);
            bool lit = battle.TryLightLamp();
            float wait = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < wait && LampSumPlates().Count == 0) yield return null;
            if (LampSumPlates().Count == 0)
            {
                SkipCheck("S-lampsum", $"battle_lamp_sum: 불 밝힘 {lit} · 적 {enemies} — 3초 안에 합계 판이 뜨지 않았다 (빛 물결 숫자 8개 미만)");
                yield break;
            }
            // 합계 판 비킴은 단단한 말풍선이 있어야 잰다 (V2 r4 — 0개로 PASS였다): 없으면 판에 가까운 잔향이가 한 번 말한다
            var firstPlate = LampSumPlates();
            yield return EnsureSolidBubble(firstPlate.Count > 0 ? firstPlate[0].rect.center : new Vector2(FrameCenterX, CanvasHeight / 3f));
            yield return Shot("battle_lamp_sum", () =>
            {
                var view = FindAnyObjectByType<SkillCalloutView>();
                var list = view != null ? CollectBubbles(view) : null;
                int solid = SolidCount(list), plates = LampSumPlates().Count;
                if (view != null && list != null && (solid == 0 || plates == 0))
                {
                    SkipCheck("S-lampsum", $"battle_lamp_sum: 불 밝힘 · 적 {enemies} · 단단한 말풍선 {solid}개 · 합계 판 {plates}개 (마련 {_bubbleSource}) — 판 비킴 규칙을 재지 못함");
                    return;
                }
                var p = new Probe();
                p.Info($"불 밝힘 · 적 {enemies} · 말풍선 마련 {_bubbleSource}");
                ExpectBubbles(p);
                Report("S-lampsum", "battle_lamp_sum", p);
            });
        }

        static int TargetableEnemies()
        {
            int count = 0;
            foreach (var enemy in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                if (enemy != null && enemy.IsTargetable) count++;
            }
            return count;
        }

        /// <summary>
        /// growth_max (W3 요청): 수선점을 최대 레벨로 두고 강화 시트를 연 뒤 단추를 크게 더해도 LevelUpMaxButton은 누를 수 없다 (interactable false 그대로) ·
        /// 눌러도 레벨이 그대로. 레벨 · 단추는 끝에 되돌린다
        /// </summary>
        IEnumerator GrowthMaxLevel()
        {
            var growth = Game.Growth;
            var data = Game.Data;
            var button = FindDeep(Find(CoachTargets.GrowthPanel), LevelUpMaxName)?.GetComponent<Button>();
            if (growth == null || data == null || button == null)
            {
                SkipCheck("S-maxlevel", $"growth_max: {CoachTargets.GrowthPanel}/…/{LevelUpMaxName} 없음");
                yield break;
            }
            int levelWas = data.level;
            double goldWas = Game.Currency.Get(CurrencyType.Gold);
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            try
            {
                data.level = growth.MaxLevel;
                _tabs.Select(MainTab.Growth);
                yield return WaitSettled();
                bool before = button.interactable;
                Game.Currency.Add(CurrencyType.Gold, 1e12);
                yield return new WaitForSecondsRealtime(0.4f);
                bool after = button.interactable;
                if (button.gameObject.activeInHierarchy) button.onClick.Invoke();
                yield return null;
                int level = data.level;
                yield return Shot("growth_max", () =>
                    Check("S-maxlevel", !before && !after && level == growth.MaxLevel,
                        $"growth_max: 최대 레벨 {growth.MaxLevel} — 최대 버튼 누름 {before} → 단추 더한 뒤 {after} (기대 false 그대로) · 누른 뒤 레벨 {level}"));
            }
            finally
            {
                data.level = levelWas;
                double now = Game.Currency.Get(CurrencyType.Gold);
                if (now > goldWas) Game.Currency.TrySpend(CurrencyType.Gold, now - goldWas);
                UINav.GoToMain(true);
            }
        }

        // ═════════════════════════ 도우미 ═════════════════════════

        /// <summary>찍는 프레임 화면 픽셀 (명암비용 — CaptureScreenshotAsTexture, 바로 버린다)</summary>
        sealed class Frame
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;
        }

        /// <summary>찍기 + 그 프레임 화면 픽셀로 검사 (명암비). 텍스처는 검사 뒤 바로 지운다</summary>
        IEnumerator ShotWithFrame(string name, Action<Frame> atCapture)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                var frame = new Frame { Pixels = texture.GetPixels32(), Width = texture.width, Height = texture.height };
                atCapture(frame);
            }
            catch (Exception e)
            {
                Check("IA-run", false, $"{name}: 검사 중 예외 {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                if (texture != null) Destroy(texture);
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_dir, $"{++_index:00}_{name}.png"));
            yield return null;
        }

        /// <summary>
        /// 글자 명암비 (§5.1): 글 사각형(textBounds) 아래 화면 픽셀의 휘도로 바탕을 어림하고(VerifyMath.EstimateBackground) 글자 색과 견준다.
        /// 반투명 글자(글자 α × 조상 CanvasGroup α &lt; 1 — '보스까지' Cream@0.8 등)는 바탕 위에 섞인 색으로 본다 (VerifyMath.BlendedLuminance).
        /// 글이 안 보이거나 비면 NaN
        /// </summary>
        static float ContrastOf(TMP_Text label, Frame frame)
        {
            if (!SampleTextLuma(label, frame)) return float.NaN;
            float text = VerifyMath.RelativeLuminance(label.color);
            float background = VerifyMath.EstimateBackground(s_luma, text);
            if (float.IsNaN(background)) return float.NaN;
            float face = VerifyMath.BlendedLuminance(label.color, label.color.a * EffectiveAlpha(label.transform), background);
            return VerifyMath.ContrastRatio(face, background);
        }

        /// <summary>글 사각형(textBounds) 아래 화면 픽셀의 상대 휘도를 s_luma에 채운다. 글이 안 보이거나 비거나 화면 밖이면 false</summary>
        static bool SampleTextLuma(TMP_Text label, Frame frame)
        {
            if (label == null || frame == null || !label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text)) return false;
            var bounds = label.textBounds;
            if (bounds.size.x < 1f || bounds.size.y < 1f) return false;
            var rt = label.rectTransform;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(bounds.min));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(bounds.max));
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x)), 0, frame.Width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x)), 0, frame.Width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y)), 0, frame.Height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y)), 0, frame.Height - 1);
            if (x1 <= x0 || y1 <= y0) return false;
            s_luma.Clear();
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++) s_luma.Add(VerifyMath.RelativeLuminance(frame.Pixels[y * frame.Width + x]));
            }
            return true;
        }

        /// <summary>
        /// 그 아래 보이는 TMP 글 중 이 조각을 품은 첫 글 — 화면에 그려진 글(ShownText: GetParsedText, 꾸밈 태그 뺌)로 보고, 켜진 글 · IsVisible ·
        /// 글 알파 × 조상 알파 ≥ 0.05만 (V2 r4: .text는 SetText 뒤 빌더 자리 글로 남을 수 있고, 꺼진 · 투명한 글도 잡았다). part가 비면 첫 빈 아닌 글. 없으면 null
        /// </summary>
        static string FindText(Transform root, string part)
        {
            if (root == null) return null;
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!label.enabled || label.color.a * EffectiveAlpha(label.transform) < 0.05f || !IsVisible(label.transform)) continue;
                string shown = ShownText(label);
                if (string.IsNullOrEmpty(shown)) continue;
                if (shown.IndexOf(part, StringComparison.Ordinal) >= 0) return shown;
            }
            return null;
        }

        /// <summary>
        /// 날 글(.text) 그대로 찾기 — 이 도구가 방금 띄운 요청(리본 부제 등)이 배너에 실렸는지 기다리는 곳에만 쓴다 (그리기 전 · 들어오는 중에도 잡아야 재는 시각이 맞다).
        /// 보이는지 · 그려진 글을 판정하는 곳은 FindText
        /// </summary>
        static string FindRawText(Transform root, string part)
        {
            if (root == null) return null;
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (label.text != null && label.text.IndexOf(part, StringComparison.Ordinal) >= 0) return label.text;
            }
            return null;
        }

        /// <summary>조상 CanvasGroup 알파의 곱 (ignoreParentGroups에서 멈춘다)</summary>
        static float EffectiveAlpha(Transform target)
        {
            float alpha = 1f;
            for (var t = target; t != null; t = t.parent)
            {
                bool stop = false;
                foreach (var group in t.GetComponents<CanvasGroup>())
                {
                    if (!group.enabled) continue;
                    alpha *= group.alpha;
                    if (group.ignoreParentGroups) stop = true;
                }
                if (stop || alpha <= 0f) break;
            }
            return alpha;
        }

        /// <summary>보인다: 켜짐 · 조상 알파 &gt; 0.05 · 켜진 그래픽(알파 &gt; 0.05)이 하나 이상</summary>
        static bool IsVisible(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy || EffectiveAlpha(target) <= 0.05f) return false;
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(false))
            {
                if (graphic.enabled && graphic.color.a > 0.05f) return true;
            }
            return false;
        }

        /// <summary>비공개 직렬화 필드 (빌더가 이름으로 묶는 것 — 이름은 바뀌지 않는다, S §0.2.3). 바탕 형식까지 찾는다</summary>
        static T Field<T>(object owner, string name) where T : class
        {
            if (owner == null) return null;
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null) return field.GetValue(owner) as T;
            }
            return null;
        }
    }
}
