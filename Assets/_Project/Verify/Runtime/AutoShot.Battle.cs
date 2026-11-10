using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.Verify
{
    /// <summary>
    /// AutoShot 전투 검사 (UX-SPEC §5.2 05 · 06–11 · 29 + WP-5B 보스 이름표 요청 — WP-9 Wave 3 r1 수정). 순서는 AutoShot.RunFull이 정한다.
    /// <para>
    /// S-05-contrast (main · tall_main): 스테이지 판 진행 숫자(ProgressText) · '보스까지'(BossLabel) · 수선점 내구도 글자(ShopHpBar/Text) 명암 ≥ 4.5 —
    /// 찍는 그 프레임 화면에서 글 사각형 아래 바탕을 어림해 글자 색(알파는 섞어서)과 견준다 (ContrastOf). 재질에 외곽선이 있으면 실제로 그려진 외곽선과도
    /// 견주어(OutlineContrastOf — §2.1.3 M81 외곽선 재질) 둘 중 하나가 4.5 이상이면 PASS. 두 값 모두 줄에 남긴다.
    /// S-06…S-11 (battle_fx_0..5, M41 · M02): 보이는 말풍선끼리 겹치지 않음 · 같은 대사 둘 없음 · 가이드 퀘스트 줄(MAIN) · 덱 이름표(DeckTab)와 겹치지 않음 ·
    /// 보이는 적 HP 막대는 잔상 ≥ 채움. S-06-hp: 여섯 컷(못 봤으면 3초 더 프레임마다)에서 깎인 막대(채움 &lt; 1)와 남은 잔상(잔상 &gt; 채움)을 본다.
    /// battle_boss · battle_boss_sheet: 보스를 바로 불러(처치 목표를 기다리지 않는다) MAIN과 강화 시트에서 보스 이름표 묶음(이름표 + HP 바 + 보호막 바)이
    /// 보이는 Hud · MetaHud 칸(조상 알파 ≥ 0.2)과 겹치지 않고 위 끝이 보이는 띠 위 끝 + 8 아래인지 본다 (S-boss-main · S-boss-sheet).
    /// S-29 (battle_later) · S-boss-bar: 진행 줄이 시뮬레이터를 따른다 — 막대가 감쇠로 따라가므로 1.5초 안에 맞는 프레임을 찾는다.
    /// </para>
    /// 비공개 필드는 빌더가 이름으로 묶는 이름 그대로 읽는다 (Field — 이름은 바뀌지 않는다). 검사만 하고 장면 상태는 끝에 되돌린다 (보스 → 스테이지 다시 시작).
    /// </summary>
    public partial class AutoShot
    {
        // ───────────── 경로 · 기준 ─────────────

        const string StageProgressTextPath = CoachTargets.StagePanel + "/ProgressText";
        const string StageBossLabelPath = CoachTargets.StagePanel + "/BossLabel";
        const string ShopHpTextPath = CoachTargets.StagePanel + "/ShopHpBar/Text";
        const string DeckTabPath = BattlePath + "/Strip/DeckTab";

        /// <summary>연속 컷 수 (battle_fx_0..5 = §5.2 06–11)</summary>
        const int FxShots = 6;
        /// <summary>막대 비율 비교 여유 (anchorMax · fillAmount 반올림)</summary>
        const float BarEpsilon = 0.002f;
        /// <summary>컷에서 깎인 막대 · 잔상을 못 봤을 때 더 지켜보는 시간 (잔상은 맞은 뒤 0.15 + 0.2초만 남는다 — EnemyFxCatalog.GhostHold · GhostDrain)</summary>
        const float FxBarWatchSeconds = 3f;
        /// <summary>이 알파 아래 말풍선은 안 보인다고 본다 (나타남 · 사라짐 끝)</summary>
        const float BubbleVisibleAlpha = 0.05f;
        /// <summary>둘 다 이 알파 이상일 때만 말풍선 겹침을 FAIL로 (막혀서 일찍 사라지는 중인 말풍선은 잠깐 겹칠 수 있다 — SkillCalloutView.FadeEarly)</summary>
        const float BubbleSolidAlpha = 0.5f;
        /// <summary>말풍선끼리 겹침으로 보는 최소 깊이 (px — 안티에일리어싱 · 흔들림 반올림)</summary>
        const float BubbleOverlapSlack = 2f;
        /// <summary>억지 대사 뒤 말풍선이 튀어나와 단단해질 때까지 (SkillCalloutView 들어옴 ≈ 0.2초 + 여유)</summary>
        const float ForcedBubbleSettle = 0.35f;
        /// <summary>
        /// 억지 대사를 해 보는 수 · 대사마다 단단한 말풍선이 될 때까지 기다리는 상한 (초 — 대사부터). r1: 보스에 가장 가까운 잔향이의 말풍선이 보스 자리에 막혀
        /// 곧 사라지기 시작해(단단한 것 0) S-boss-bubble이 SKIP — 다음으로 가까운 잔향이가 다시 말한다 (V2 r1 fix)
        /// </summary>
        const int ForcedBubbleTries = 3;
        const float ForcedBubbleWait = 1f;

        /// <summary>EnsureSolidBubble이 말풍선을 마련한 길 (저절로 · 억지 대사 · 없음 — 검사 줄에 남긴다)</summary>
        string _bubbleSource = string.Empty;
        int _forcedBubbles;
        /// <summary>보스 이름표가 비켜야 하는 Hud · MetaHud 칸의 조상 알파 하한 (WP-5B 요청 — BattleFx.KeepOut과 같은 0.2)</summary>
        const float PlateCellAlpha = 0.2f;
        /// <summary>보스 이름표 묶음 위 끝 ≥ 보이는 띠 위 끝 + 8 (WP-5B 요청 — 16:9 MAIN · SHEET 화면 y 138)</summary>
        const float PlateTopGap = 8f;
        /// <summary>16:9 보스 이름표 묶음 위 끝 하한 (화면 y — 보이는 띠 위 130 + 8)</summary>
        const float PlateTopScreen169 = 138f;
        /// <summary>보스 치명타 숫자를 지켜보는 시간 · 몸 폭 여유 (px)</summary>
        const float BossCritWatchSeconds = 3f;
        const float BossCritSlack = 2f;
        /// <summary>보스 진행 줄이 맞는 프레임을 찾는 시간 (막대 감쇠 초당 10 — 1.5초면 끝까지 따라간다)</summary>
        const float BossBarWatchSeconds = 1.5f;
        /// <summary>보스 전 막대 = 처치/목표 ± / 보스 중 막대 = 남은 체력 ± (맞는 프레임 기준 — 따라가는 중인 프레임은 넘긴다)</summary>
        const float KillBarTolerance = 0.02f;
        const float BossBarTolerance = 0.03f;

        // battle_fx 여섯 컷을 모은 HP 막대 기록 (S-06-hp)
        int _fxBarShots;
        int _fxDepletedShots;
        int _fxLagShots;
        int _fxBarBad;
        string _fxDepletedAt;
        string _fxLagAt;

        // ═════════════════════════ 05 main: 스테이지 판 글자 명암 ═════════════════════════

        /// <summary>S-05-contrast (§5.2 05 · M11 · M81): 진행 숫자 · '보스까지' · 수선점 내구도 글자 명암 ≥ 4.5. 꺼진 글(파밍 중 숫자)은 기록만</summary>
        void CheckHudContrast(string shot, Frame frame)
        {
            var p = new Probe();
            ExpectContrast(p, frame, StageProgressTextPath, "진행 숫자");
            ExpectContrast(p, frame, StageBossLabelPath, "보스까지");
            ExpectContrast(p, frame, ShopHpTextPath, "수선점 내구도");
            Report("S-05-contrast", shot, p);
        }

        void ExpectContrast(Probe p, Frame frame, string path, string name)
        {
            var label = Comp<TMP_Text>(path);
            if (label == null)
            {
                p.Expect(false, $"{name} {path} 없음");
                return;
            }
            string shown = ShownText(label);
            if (!label.gameObject.activeInHierarchy || string.IsNullOrEmpty(shown))
            {
                p.Info($"{name} 꺼짐");
                return;
            }
            float ratio = ContrastOf(label, frame);
            float stroke = OutlineContrastOf(label, frame);
            bool ok = (!float.IsNaN(ratio) && ratio >= VerifyMath.ContrastText) || (!float.IsNaN(stroke) && stroke >= VerifyMath.ContrastText);
            string outline = float.IsNaN(stroke) ? "외곽선 없음" : $"외곽선 {stroke:0.00}";
            p.Expect(ok, $"{name} '{shown}' 바탕 명암 {ratio:0.00} · {outline} (기대 바탕 또는 그려진 외곽선 ≥ {VerifyMath.ContrastText}, " +
                         $"글자 α {label.color.a * EffectiveAlpha(label.transform):0.00})");
        }

        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        /// <summary>
        /// 그려진 외곽선과 글자의 명암 (§2.1.3 · M81 — 판 위 맨글자는 외곽선으로 읽힌다): 글 재질에 외곽선(_OutlineWidth ≥ 0.1 · _OutlineColor α ≥ 0.5)이 있을 때만,
        /// 글 사각형 아래 화면 휘도의 글자 반대쪽 끝 10번째 백분위(VerifyMath.EstimateStroke — 실제로 그려진 외곽선)와 글자 색을 견준다.
        /// 재질에 외곽선이 없으면 NaN (막대 틀 · 깎인 칸 같은 어두운 바탕을 외곽선으로 잘못 세지 않게). 외곽선이 얇거나 옅게 그려지면 낮게 나온다
        /// </summary>
        static float OutlineContrastOf(TMP_Text label, Frame frame)
        {
            var material = label != null ? label.fontSharedMaterial : null;
            if (material == null || !material.HasProperty(OutlineWidthId) || !material.HasProperty(OutlineColorId)) return float.NaN;
            if (material.GetFloat(OutlineWidthId) < 0.1f || material.GetColor(OutlineColorId).a < 0.5f) return float.NaN;
            if (!SampleTextLuma(label, frame)) return float.NaN;
            float stroke = VerifyMath.EstimateStroke(s_luma, VerifyMath.RelativeLuminance(label.color));
            if (float.IsNaN(stroke)) return float.NaN;
            float face = VerifyMath.BlendedLuminance(label.color, label.color.a * EffectiveAlpha(label.transform), stroke);
            return VerifyMath.ContrastRatio(face, stroke);
        }

        // ═════════════════════════ 06–11 battle_fx: 말풍선 · 적 HP 막대 ═════════════════════════

        /// <summary>여섯 컷 기록을 비운다 (battle_fx_0 앞)</summary>
        void ResetFxBars()
        {
            _fxBarShots = _fxDepletedShots = _fxLagShots = _fxBarBad = 0;
            _fxDepletedAt = _fxLagAt = null;
        }

        /// <summary>S-06…S-11 (battle_fx_i): 말풍선 규칙 (M41) + 보이는 적 HP 막대 잔상 ≥ 채움 (M02). 깎임 · 잔상은 S-06-hp가 여섯 컷을 모아 본다</summary>
        void CheckBattleFx(string id, string shot)
        {
            var p = new Probe();
            ExpectBubbles(p);
            var sample = new StringBuilder();
            int bad = MeasureEnemyBars(p, sample, out int shown, out int depleted, out int lagging);
            _fxBarBad += bad;
            if (shown > 0) _fxBarShots++;
            if (depleted > 0)
            {
                _fxDepletedShots++;
                _fxDepletedAt ??= shot;
            }
            if (lagging > 0)
            {
                _fxLagShots++;
                _fxLagAt ??= shot;
            }
            p.Info($"적 HP 막대 {shown}개 · 깎임 {depleted} · 잔상 남음 {lagging}{(sample.Length > 0 ? " — " + sample : string.Empty)}");
            Report(id, shot, p);
        }

        /// <summary>
        /// 말풍선 (§2.1.8 M41 받아들임): 보이는(알파 ≥ 0.05) 말풍선 바탕(Back) 상자끼리 겹치지 않고, 같은 대사가 둘 보이지 않고,
        /// MAIN이면 가이드 퀘스트 줄 · 늘 덱 이름표(DeckTab) 사각형에 들어가지 않는다. 말풍선마다 대사 · 자리 · 알파 · 나이를 기록.
        /// 겹침은 둘 다 사라지는 단계 전(나이 &lt; 수명 × 0.75)이고 알파 ≥ 0.5일 때만 FAIL — 막혀서 일찍 사라지는 말풍선은 사양대로 잠깐 겹칠 수 있어 기록만.
        /// 상자는 돌지 않은 상자(BoxOf — 튀어나올 때 ±4° 기울어 둘레 상자가 부푸는 것을 빼고)
        /// </summary>
        void ExpectBubbles(Probe p)
        {
            var view = FindAnyObjectByType<SkillCalloutView>();
            if (view == null)
            {
                p.Expect(false, "말풍선 레이어(SkillCalloutView) 없음 — 전투 장면에 늘 있어야 한다");
                return;
            }
            var list = CollectBubbles(view);
            if (list == null)
            {
                p.Expect(false, "SkillCalloutView._bubbles · _backs · _texts 없음");
                return;
            }
            var shown = new StringBuilder();
            foreach (var bubble in list)
            {
                if (shown.Length > 0) shown.Append(", ");
                shown.Append($"{bubble.Name} '{bubble.Text}' {R(bubble.Rect)} α {bubble.Alpha:0.00} 나이 {bubble.Age:0.00}");
            }
            p.Info($"말풍선 {list.Count}개 (살아 있음 {view.AliveCount}){(shown.Length > 0 ? " — " + shown : string.Empty)}");

            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (!string.IsNullOrEmpty(a.Text) && a.Text == b.Text) p.Expect(false, $"같은 대사 둘 '{a.Text}' ({a.Name} · {b.Name})");
                    if (!VerifyMath.Overlaps(a.Rect, b.Rect, BubbleOverlapSlack)) continue;
                    if (a.Solid && b.Solid) p.Expect(false, $"{a.Name} {R(a.Rect)} · {b.Name} {R(b.Rect)} 겹침 (둘 다 사라지는 단계 전)");
                    else p.Info($"사라지는 중 겹침 {a.Name} 나이 {a.Age:0.00} α {a.Alpha:0.00} · {b.Name} 나이 {b.Age:0.00} α {b.Alpha:0.00}");
                }
            }

            // 가이드 퀘스트 줄은 MAIN에서만 비킨다 (시트가 열리면 시트 밑)
            var guide = Find(CoachTargets.GuideQuestBar);
            if (UIFrame.SheetProgress <= 0f && guide != null && IsVisible(guide)) ExpectBubblesClear(p, list, guide, "GuideQuestBar");
            var deck = Find(DeckTabPath);
            if (deck != null && IsVisible(deck)) ExpectBubblesClear(p, list, deck, "DeckTab");
            // 불 밝히기 '+합계' 판도 비킨다 (WP-5B 요청 — 말풍선 겹침 검사에 넣는다): 사라지는 단계 전 말풍선만 FAIL
            foreach (var (plateName, plate) in LampSumPlates()) ExpectSolidClear(p, list, plate, plateName);
        }

        /// <summary>
        /// 보이는(바탕 알파 ≥ 0.05) 말풍선 목록 — 대사(꾸밈 태그 뺀 보이는 글) · 돌지 않은 상자 · 알파 · 나이. SkillCalloutView 필드가 없으면 null
        /// </summary>
        List<Bubble> CollectBubbles(SkillCalloutView view)
        {
            var bubbles = Field<RectTransform[]>(view, "_bubbles");
            var backs = Field<RectTransform[]>(view, "_backs");
            var texts = Field<TMP_Text[]>(view, "_texts");
            if (bubbles == null || backs == null || texts == null) return null;
            var list = new List<Bubble>();
            for (int i = 0; i < bubbles.Length; i++)
            {
                if (bubbles[i] == null || !bubbles[i].gameObject.activeInHierarchy) continue;
                var back = i < backs.Length && backs[i] != null ? backs[i] : bubbles[i];
                float alpha = EffectiveAlpha(back);
                if (alpha < BubbleVisibleAlpha) continue;
                string text = i < texts.Length && texts[i] != null ? ShownText(texts[i]) : string.Empty;
                list.Add(new Bubble { Name = bubbles[i].name, Rect = BoxOf(back), Text = text, Alpha = alpha, Age = BubbleAge(view, i) });
            }
            return list;
        }

        /// <summary>단단한(Solid — 사라지는 단계 전 · 알파 ≥ 0.5) 말풍선 수</summary>
        static int SolidCount(List<Bubble> list)
        {
            int count = 0;
            if (list == null) return count;
            foreach (var bubble in list)
            {
                if (bubble.Solid) count++;
            }
            return count;
        }

        /// <summary>
        /// 말풍선 비킴 검사 전에 단단한 말풍선을 하나 마련한다 (V2 r4: 말풍선이 0개면 비킴 검사가 아무것도 재지 않고 PASS였다): 이미 있으면 그대로(저절로),
        /// 없으면 near(기준 px 화면 점)에 가까운 잔향이부터 실제 대사 길(SkillCalloutView.Say(EchoView, …, important) — 비키기 · 도배 규칙은 게임 그대로)로 말하게 하고
        /// ForcedBubbleSettle 뒤 ForcedBubbleWait초(대사부터)까지 단단한 말풍선을 기다린다. 막혀 일찍 사라지면 다음으로 가까운 잔향이가 다시 — ForcedBubbleTries번까지
        /// (V2 r1 fix). 대사를 받는 잔향이가 ForcedBubbleWait초 안에 없거나 끝내 단단해지지 않으면 '없음' (검사는 SKIP). 마련한 길은 _bubbleSource
        /// </summary>
        IEnumerator EnsureSolidBubble(Vector2 near)
        {
            _bubbleSource = "없음";
            var view = FindAnyObjectByType<SkillCalloutView>();
            if (view == null)
            {
                _bubbleSource = "없음 (SkillCalloutView 없음)";
                yield break;
            }
            if (view.AliveCount > 0 && SolidCount(CollectBubbles(view)) > 0)
            {
                _bubbleSource = "저절로";
                yield break;
            }
            var echoes = new List<(float distance, EchoView echo)>();
            foreach (var echo in FindObjectsByType<EchoView>(FindObjectsSortMode.None))
            {
                if (echo == null || !echo.isActiveAndEnabled || echo.Id < 0) continue;
                var center = ScreenRect(echo.Rect).center;
                if (float.IsNaN(center.x)) continue;
                echoes.Add(((center - near).sqrMagnitude, echo));
            }
            echoes.Sort((a, b) => a.distance.CompareTo(b.distance));
            int tried = 0, said = 0, next = 0;
            for (int attempt = 1; attempt <= ForcedBubbleTries && echoes.Count > 0; attempt++)
            {
                // 다음으로 가까운 잔향이부터 (같은 잔향이는 간격 안에 다시 말하지 않는다 · 사라지는 말풍선이 자리를 쥐고 있으면 다음 프레임에 다시)
                int speakerId = -1, rank = 0;
                float sayLimit = Time.realtimeSinceStartup + ForcedBubbleWait;
                while (true)
                {
                    for (int k = 0; k < echoes.Count && speakerId < 0; k++)
                    {
                        int index = (next + k) % echoes.Count;
                        var echo = echoes[index].echo;
                        if (echo == null || !echo.isActiveAndEnabled) continue;
                        tried++;
                        if (!view.Say(echo, $"확인 말 {++_forcedBubbles}", UITheme.Cream, true)) continue;
                        speakerId = echo.Id;
                        rank = index + 1;
                        next = index + 1;
                    }
                    if (speakerId >= 0 || Time.realtimeSinceStartup >= sayLimit) break;
                    yield return null;
                }
                if (speakerId < 0) break;
                said++;
                float saidAt = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(ForcedBubbleSettle);
                while (true)
                {
                    if (SolidCount(CollectBubbles(view)) > 0)
                    {
                        _bubbleSource = $"억지 대사 (잔향이 {speakerId}, {rank}번째 가까운 것{(attempt > 1 ? $", {attempt}번째 대사" : string.Empty)})";
                        yield break;
                    }
                    if (Time.realtimeSinceStartup - saidAt >= ForcedBubbleWait) break;
                    yield return null;
                }
            }
            _bubbleSource = said > 0
                ? $"없음 (억지 대사 {said}번 — 대사마다 {ForcedBubbleWait:0.#}초 안에 단단한 말풍선이 되지 않음 · 잔향이 {echoes.Count}명 · 대사 시도 {tried}번)"
                : $"없음 (잔향이 {echoes.Count}명 — 대사 {tried}번 거절)";
        }

        /// <summary>사라지는 단계 전(Solid) 말풍선이 그 자리와 2px 넘게 겹치면 FAIL, 사라지는 중이면 기록만</summary>
        void ExpectSolidClear(Probe p, List<Bubble> list, Rect zone, string zoneName)
        {
            int hits = 0;
            foreach (var bubble in list)
            {
                if (!VerifyMath.Overlaps(bubble.Rect, zone, BubbleOverlapSlack)) continue;
                hits++;
                if (bubble.Solid) p.Expect(false, $"{bubble.Name} '{bubble.Text}' {R(bubble.Rect)}이 {zoneName} {R(zone)}와 겹침 (α {bubble.Alpha:0.00} · 나이 {bubble.Age:0.00})");
                else p.Info($"사라지는 중 {bubble.Name} 나이 {bubble.Age:0.00} α {bubble.Alpha:0.00}이 {zoneName}와 겹침");
            }
            if (hits == 0) p.Info($"{zoneName} {R(zone)} 비킴");
        }

        /// <summary>떠 있는 불 밝히기 합계 판 (BattleFx._calloutPlates 중 _calloutSum 칸 — 켜짐 · 보이는 알파 ≥ 0.05). 화면 사각형</summary>
        List<(string name, Rect rect)> LampSumPlates()
        {
            var list = new List<(string name, Rect rect)>();
            var fx = FindAnyObjectByType<BattleFx>();
            var plates = fx != null ? Field<Image[]>(fx, "_calloutPlates") : null;
            var sums = fx != null ? Field<bool[]>(fx, "_calloutSum") : null;
            if (plates == null || sums == null) return list;
            for (int i = 0; i < plates.Length && i < sums.Length; i++)
            {
                var plate = plates[i];
                if (!sums[i] || plate == null || !plate.isActiveAndEnabled) continue;
                if (plate.color.a * EffectiveAlpha(plate.transform) < BubbleVisibleAlpha) continue;
                list.Add(($"불 밝히기 합계 판 {i}", ScreenRect(plate.rectTransform)));
            }
            return list;
        }

        /// <summary>
        /// 화면에 보이는 글 (TMP가 마지막으로 그린 글 — SetText 뒤 .text는 빌더 자리 글('0/20' · '100 / 100')로 남을 수 있다, W3 r3): GetParsedText에서
        /// 꾸밈 태그를 뺀 것. 아직 한 번도 그리지 않아 비면 .text에서
        /// </summary>
        static string ShownText(TMP_Text label)
        {
            if (label == null) return string.Empty;
            string parsed = label.GetParsedText();
            return VerifyMath.StripRichText(string.IsNullOrEmpty(parsed) ? label.text : parsed);
        }

        /// <summary>보이는 말풍선 하나 (검사용 — Age = 나이 / 수명, 모르면 NaN)</summary>
        sealed class Bubble
        {
            public string Name;
            public Rect Rect;
            public string Text;
            public float Alpha;
            public float Age;

            /// <summary>사라지는 단계 전(나이 &lt; 0.75, 모르면 알파로) · 알파 ≥ 0.5 — 겹치면 안 되는 말풍선</summary>
            public bool Solid => Alpha >= BubbleSolidAlpha && (float.IsNaN(Age) || Age < BubbleFadeStart - 1e-3f);
        }

        /// <summary>말풍선이 사라지기 시작하는 나이 비율 (SkillCalloutView: 마지막 25%에 떠오르며 사라진다 · 막히면 나이를 여기로 당긴다)</summary>
        const float BubbleFadeStart = 0.75f;

        /// <summary>SkillCalloutView._state[index].Age / _life (비공개 — 이름으로 읽는다). 못 읽으면 NaN</summary>
        static float BubbleAge(SkillCalloutView view, int index)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (typeof(SkillCalloutView).GetField("_state", flags)?.GetValue(view) is not System.Array state || index >= state.Length) return float.NaN;
            var life = typeof(SkillCalloutView).GetField("_life", flags)?.GetValue(view);
            var entry = state.GetValue(index);
            var age = entry?.GetType().GetField("Age", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            return life is float l && l > 0f && age is float a ? a / l : float.NaN;
        }

        /// <summary>
        /// 대상의 돌지 않은 상자 (기준 px): 네 귀퉁이의 가운데를 중심으로, 아래 변 · 왼 변 길이를 폭 · 높이로 (배율은 그대로).
        /// 말풍선이 튀어나올 때 ±4° 기울면 둘레 상자(ScreenRect)는 폭 300이면 위아래로 약 20px 부풀어 6px 틈의 이웃과 겹쳐 보인다
        /// </summary>
        Rect BoxOf(RectTransform target)
        {
            target.GetWorldCorners(Corners);
            var bottomLeft = RefPoint(Corners[0]);
            var topLeft = RefPoint(Corners[1]);
            var topRight = RefPoint(Corners[2]);
            var bottomRight = RefPoint(Corners[3]);
            var center = (bottomLeft + topLeft + topRight + bottomRight) * 0.25f;
            float width = Vector2.Distance(bottomLeft, bottomRight);
            float height = Vector2.Distance(bottomLeft, topLeft);
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        void ExpectBubblesClear(Probe p, List<Bubble> list, RectTransform zone, string zoneName)
        {
            var rect = ScreenRect(zone);
            int hits = 0;
            foreach (var bubble in list)
            {
                if (!VerifyMath.Overlaps(bubble.Rect, rect)) continue;
                hits++;
                p.Expect(false, $"{bubble.Name} '{bubble.Text}' {R(bubble.Rect)}이 {zoneName} {R(rect)} 안 (α {bubble.Alpha:0.00})");
            }
            if (hits == 0) p.Info($"{zoneName} {R(rect)} 비킴");
        }

        /// <summary>
        /// 보이는 적 HP 막대를 잰다 (살아 있는 적 · HP 바 켜짐): 수 · 깎인 수(채움 &lt; 1) · 잔상이 남은 수(잔상 &gt; 채움). 잔상 &lt; 채움(어긋남)은 probe에 FAIL로 적고 그 수를 돌려준다.
        /// sample에는 앞 네 마리의 체력 · 채움 · 잔상 (probe · sample은 없어도 된다 — 지켜보기)
        /// </summary>
        int MeasureEnemyBars(Probe p, StringBuilder sample, out int shown, out int depleted, out int lagging)
        {
            shown = depleted = lagging = 0;
            int bad = 0;
            foreach (var enemy in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.IsTargetable) continue;
                var root = Field<RectTransform>(enemy, "_hpRoot");
                var fill = Field<Image>(enemy, "_hpFill");
                var trail = Field<Image>(enemy, "_hpTrail");
                if (root == null || fill == null || trail == null || !root.gameObject.activeInHierarchy) continue;
                float f = BattleEase.BarRatio(fill);
                float t = BattleEase.BarRatio(trail);
                shown++;
                if (f < 1f - BarEpsilon) depleted++;
                if (t > f + BarEpsilon) lagging++;
                string tag = $"적 {enemy.Id}{(enemy.IsBoss ? "(보스)" : string.Empty)}";
                if (t < f - BarEpsilon)
                {
                    bad++;
                    p?.Expect(false, $"{tag} 잔상 {t:0.000} < 채움 {f:0.000} (기대 잔상 ≥ 채움)");
                }
                if (sample != null && shown <= 4)
                {
                    var state = enemy.State;
                    float hp = state != null && state.MaxHp > 0f ? state.Hp / state.MaxHp : float.NaN;
                    if (sample.Length > 0) sample.Append(", ");
                    sample.Append($"{tag} 체력 {hp:0.00} · 채움 {f:0.00} · 잔상 {t:0.00}");
                }
            }
            return bad;
        }

        /// <summary>
        /// S-06-hp (§5.2 06–11 · M02): 여섯 컷에서 깎인 막대와 남은 잔상을 봤는지 — 컷에서 못 봤으면 3초 동안 프레임마다 더 본다
        /// (잔상은 맞은 뒤 0.35초만 남아 컷 사이로 지나갈 수 있다). 잔상 &lt; 채움 어긋남은 한 번도 없어야 한다. 막대가 하나도 안 보였으면 SKIP (맞은 적이 없다)
        /// </summary>
        IEnumerator FxBarsSeen()
        {
            int watched = 0;
            bool seen = _fxBarShots > 0;
            float limit = Time.realtimeSinceStartup + FxBarWatchSeconds;
            while ((_fxDepletedAt == null || _fxLagAt == null) && Time.realtimeSinceStartup < limit)
            {
                yield return null;
                watched++;
                _fxBarBad += MeasureEnemyBars(null, null, out int shown, out int depleted, out int lagging);
                if (shown > 0) seen = true;
                if (depleted > 0) _fxDepletedAt ??= $"컷 뒤 {watched}프레임";
                if (lagging > 0) _fxLagAt ??= $"컷 뒤 {watched}프레임";
            }
            if (!seen)
            {
                SkipCheck("S-06-hp", $"battle_fx: 컷 {FxShots}개 + {FxBarWatchSeconds:0}초 동안 보이는 적 HP 막대 없음 (맞은 적이 없다)");
                yield break;
            }
            var p = new Probe();
            p.Expect(_fxDepletedAt != null, $"깎인 막대(채움 < 1) 처음 본 곳 {_fxDepletedAt ?? "없음"}");
            p.Expect(_fxLagAt != null, $"남은 잔상(잔상 > 채움) 처음 본 곳 {_fxLagAt ?? "없음"}");
            p.Expect(_fxBarBad == 0, $"잔상 < 채움 어긋남 {_fxBarBad}번 (기대 0)");
            p.Info($"막대가 보인 컷 {_fxBarShots}/{FxShots} · 깎임 컷 {_fxDepletedShots} · 잔상 컷 {_fxLagShots} · 더 본 프레임 {watched}");
            Report("S-06-hp", "battle_fx", p);
        }

        // ═════════════════════════ battle_boss: 보스 이름표 (WP-5B 요청) ═════════════════════════

        /// <summary>
        /// battle_boss (MAIN) → battle_boss_sheet (강화 시트): 지금 스테이지에 보스를 바로 부른다 (BattleSimulator.SpawnBoss — 비공개, 처치 목표 대신).
        /// 보스 뷰가 뜨고 '보스 등장!' 배너가 빠질 때까지(이름표는 배너 밑에서 흐려진다, 늦어도 5초) 기다렸다가 찍는다. MAIN에서는 진행 줄(보스 체력 %)도 본다.
        /// 끝나면 MAIN으로 돌아가 같은 스테이지를 다시 시작한다 (보스를 치우고 뒤 장면을 그대로 — 그사이 보스를 잡아 넘어갔어도 처음 스테이지로)
        /// </summary>
        IEnumerator BattleBoss()
        {
            var battle = Game.Battle;
            var sim = battle != null ? battle.Sim : null;
            var spawn = typeof(BattleSimulator).GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sim == null || spawn == null || !sim.IsStarted)
            {
                SkipCheck("S-boss-main", $"battle_boss: 전투 · BattleSimulator.SpawnBoss 없음 (시뮬레이터 {(sim != null ? "있음" : "없음")})");
                yield break;
            }
            CloseAll();
            UINav.GoToMain(true);
            yield return WaitSettled();
            // 스테이지 사이(결과 배너 · 다음 스테이지 준비)면 다음 스테이지가 돌 때까지 (늦어도 6초) — 끝난 판에 보스를 넣지 않는다
            float ready = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < ready && battle.Sim.Outcome != BattleOutcome.Running) yield return null;
            sim = battle.Sim;
            if (sim.Outcome != BattleOutcome.Running)
            {
                SkipCheck("S-boss-main", $"battle_boss: 6초 안에 스테이지가 다시 돌지 않았다 ({sim.Outcome})");
                yield break;
            }
            int stage = battle.Stage;
            try
            {
                if (!sim.BossSpawned) spawn.Invoke(sim, null);
                var banner = Find(BannerPath);
                float limit = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < limit)
                {
                    if (FindBoss() != null && (banner == null || !banner.gameObject.activeInHierarchy)) break;
                    yield return null;
                }
                // 이름표가 배너에서 풀려 다시 보이는 시간 (흐림 0.12초) + 여유
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("battle_boss", () => CheckBossPlate("S-boss-main", "battle_boss"));
                // 말풍선 비킴 (S-boss-bubble): 찍은 뒤 단단한 말풍선을 마련해(없으면 보스에 가까운 잔향이가 말한다) 같은 자리 규칙을 잰다 — 새 컷 없음 (컷 번호 그대로)
                yield return EnsureSolidBubble(BossCenter());
                CheckBossBubbles("S-boss-bubble", "battle_boss");
                yield return BossBarSettles("S-boss-bar", "battle_boss");
                // 보스가 맞은 치명타 숫자는 보스 몸 폭 안에서 뜬다 (W3 요청 — 찍지 않고 3초 지켜본다)
                yield return BossCrits("S-boss-crit", "battle_boss");

                _tabs.Select(MainTab.Growth);
                yield return WaitSettled();
                // 올림이 끝난 뒤 이름표 묶음이 새 자리로 미끄러지는 시간 (900px/s)
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("battle_boss_sheet", () => CheckBossPlate("S-boss-sheet", "battle_boss_sheet"));
                yield return EnsureSolidBubble(BossCenter());
                CheckBossBubbles("S-boss-bubble-sheet", "battle_boss_sheet");
            }
            finally
            {
                UINav.GoToMain(true);
                if (Game.Battle != null) Game.Battle.GoToStage(Mathf.Min(stage, Game.Battle.HighestStage));
            }
            yield return WaitSettled();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>살아 있는 보스 몸의 화면 가운데 (기준 px — 없으면 화면 위 3분의 1 가운데)</summary>
        Vector2 BossCenter()
        {
            var boss = FindBoss();
            var center = boss != null ? ScreenRect((RectTransform)boss.transform).center : new Vector2(float.NaN, float.NaN);
            return float.IsNaN(center.x) ? new Vector2(CanvasWidth * 0.5f, CanvasHeight / 3f) : center;
        }

        static EnemyView FindBoss()
        {
            foreach (var enemy in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                if (enemy != null && enemy.IsBoss && enemy.IsTargetable) return enemy;
            }
            return null;
        }

        /// <summary>
        /// 보스 이름표 묶음 (WP-5B 요청): 이름표(_bossPlate) + HP 바(_hpRoot) + 보호막 바(_shieldRoot, 켜졌으면)의 화면 사각형이
        /// 보이는 Hud · MetaHud 칸(조상 알파 ≥ 0.2 — VisibleHudCells)과 겹치지 않고, 위 끝 ≥ 보이는 띠 위 끝(mapper.VisibleTop의 화면 y) + 8.
        /// 살아 있는 보스가 없으면 · 이름표가 배너 밑에서 흐리면(α &lt; 0.2) SKIP과 이유
        /// </summary>
        void CheckBossPlate(string id, string shot)
        {
            var boss = FindBoss();
            if (boss == null)
            {
                SkipCheck(id, $"{shot}: 살아 있는 보스 없음 (벌써 쓰러졌거나 스테이지가 끝났다)");
                return;
            }
            var plate = Field<GameObject>(boss, "_bossPlate");
            if (plate == null || !plate.activeInHierarchy)
            {
                Check(id, false, $"{shot}: 보스 {boss.Id} 이름표(_bossPlate) {(plate == null ? "없음" : "꺼짐")}");
                return;
            }
            var plateRect = (RectTransform)plate.transform;
            var box = ScreenRect(plateRect);
            var hp = Field<RectTransform>(boss, "_hpRoot");
            if (hp != null && hp.gameObject.activeInHierarchy) box = Union(box, ScreenRect(hp));
            var shield = Field<RectTransform>(boss, "_shieldRoot");
            if (shield != null && shield.gameObject.activeInHierarchy) box = Union(box, ScreenRect(shield));
            float alpha = EffectiveAlpha(plateRect);
            float top = RefPoint(_mapper.FieldToCanvasWorld(new Vector2(0f, _mapper.VisibleTop))).y;
            var name = Field<TMP_Text>(boss, "_bossName");
            string label = $"보스 {boss.Id} '{(name != null ? name.text : string.Empty)}' 묶음 {R(box)} · α {alpha:0.00} · 보이는 띠 위 {top:0.#} · 시트 {OpenText(UIFrame.SheetOpen)}";
            if (alpha < PlateCellAlpha)
            {
                SkipCheck(id, $"{shot}: 이름표가 흐리다 (배너 밑) — {label}");
                return;
            }
            var p = new Probe();
            p.Info(label);
            p.Expect(box.yMin >= top + PlateTopGap - 1f, $"묶음 위 끝 {box.yMin:0.#} (기대 ≥ 보이는 띠 위 {top:0.#} + {PlateTopGap:0})");
            // 16:9에서는 문서 숫자 그대로 (W3 요청: MAIN · SHEET 모두 화면 y 138 아래)
            if (Is169) p.Expect(box.yMin >= PlateTopScreen169 - 1f, $"묶음 위 끝 {box.yMin:0.#} (기대 ≥ 화면 y {PlateTopScreen169:0}, 16:9)");
            // 시트가 열려 있으면 시트 위 끝 아래는 가려진다 — 그 아래의 묶음 · 칸은 겹침을 보지 않는다
            float sheetTop = UIFrame.SheetOpen ? CanvasHeight - SpecTabBar - SpecSheet : float.MaxValue;
            if (box.yMin >= sheetTop)
            {
                p.Info($"묶음이 시트(위 끝 {sheetTop:0}) 밑 — 안 보여 칸 겹침은 보지 않는다");
                Report(id, shot, p);
                return;
            }
            var cells = VisibleHudCells();
            int hits = 0, checkedCells = 0;
            foreach (var (cell, rect) in cells)
            {
                if (rect.yMin >= sheetTop) continue;
                checkedCells++;
                if (!VerifyMath.Overlaps(box, rect)) continue;
                hits++;
                p.Expect(false, $"{cell} {R(rect)}와 겹침");
            }
            if (hits == 0) p.Info($"보이는 Hud · MetaHud 칸 {checkedCells}개(α ≥ {PlateCellAlpha:0.0})와 겹치지 않음");
            Report(id, shot, p);
        }

        /// <summary>
        /// 보이는 Hud · MetaHud 칸: 켜져 있고 조상 알파 ≥ 0.2인 것 중 제 그래픽이 보이면(알파 &gt; 0.05, 빈 글 아님) 그 사각형, 제 그래픽이 없거나 투명한 묶음
        /// (시너지 줄 · 빠른 줄 · 추천 알약 누름 판 · 가이드 퀘스트 줄)은 자식으로 내려간다 (3단계까지). 전투 화면을 거의 다 덮는 판(서랍 바깥 누름 판 등)은 칸이 아니다
        /// </summary>
        List<(string name, Rect rect)> VisibleHudCells()
        {
            var list = new List<(string name, Rect rect)>();
            var battle = Find(BattlePath);
            if (battle == null) return list;
            var area = ScreenRect(battle);
            AddVisibleCells(Find(HudPath), area, list, 0);
            AddVisibleCells(Find(MetaHudPath), area, list, 0);
            return list;
        }

        void AddVisibleCells(Transform parent, Rect area, List<(string name, Rect rect)> list, int depth)
        {
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i) is not RectTransform child || !child.gameObject.activeInHierarchy) continue;
                if (EffectiveAlpha(child) < PlateCellAlpha) continue;
                var rect = ScreenRect(child);
                bool cover = rect.width >= area.width * 0.9f && rect.height >= area.height * 0.9f;
                if (!cover && rect.width >= 1f && rect.height >= 1f && OwnGraphicVisible(child)) list.Add((PathOf(child), rect));
                else if (depth < 3) AddVisibleCells(child, area, list, depth + 1);
            }
        }

        /// <summary>그 칸 자신의 그래픽이 보인다 (켜짐 · 알파 &gt; 0.05 · 글이면 빈 글이 아님)</summary>
        static bool OwnGraphicVisible(Transform target)
        {
            if (!target.TryGetComponent<Graphic>(out var graphic) || !graphic.enabled || graphic.color.a <= 0.05f) return false;
            return graphic is not TMP_Text text || !string.IsNullOrEmpty(text.text);
        }

        /// <summary>
        /// S-boss-bubble (W3 r3 요청 · WP-5B r2 medium): 보스 몸 자리(EnemyView.BodyRect) ∪ 이름표 묶음(StatusRect)을 BattleFx.BodyKeepPad만큼 넓힌 사각형과
        /// 사라지는 단계 전(Solid) 말풍선 바탕이 2px 넘게 겹치면 FAIL, 사라지는 중이면 INFO. 부르기 전에 EnsureSolidBubble이 말풍선을 마련한다.
        /// 말풍선 레이어가 없으면 FAIL, 살아 있는 보스가 없거나 단단한 말풍선이 하나도 없으면(규칙을 재지 못함) SKIP
        /// </summary>
        void CheckBossBubbles(string id, string shot)
        {
            var boss = FindBoss();
            var view = FindAnyObjectByType<SkillCalloutView>();
            if (view == null)
            {
                Check(id, false, $"{shot}: 말풍선 레이어(SkillCalloutView) 없음 — 전투 장면에 늘 있어야 한다");
                return;
            }
            if (boss == null)
            {
                SkipCheck(id, $"{shot}: 살아 있는 보스 없음");
                return;
            }
            bool hasStatus = boss.StatusRect(out var status);
            bool hasBody = boss.BodyRect(out var body);
            if (!hasStatus && !hasBody)
            {
                SkipCheck(id, $"{shot}: 보스 {boss.Id} 몸 · 이름표 자리를 못 읽었다 (쓰러지는 중)");
                return;
            }
            var local = hasStatus && hasBody ? Union(status, body) : hasStatus ? status : body;
            float pad = BattleFx.BodyKeepPad;
            local = Rect.MinMaxRect(local.xMin - pad, local.yMin - pad, local.xMax + pad, local.yMax + pad);
            var keep = EnemyLocalToScreen(boss, local);
            var list = CollectBubbles(view);
            if (list == null)
            {
                Check(id, false, $"{shot}: SkillCalloutView._bubbles · _backs · _texts 없음");
                return;
            }
            int solid = SolidCount(list);
            string head = $"{shot}: 보스 {boss.Id} 몸{(hasBody ? string.Empty : "(없음)")} ∪ 이름표{(hasStatus ? string.Empty : "(없음)")} +{pad:0} = {R(keep)} · " +
                          $"보이는 말풍선 {list.Count}개 (단단한 것 {solid}) · 마련 {_bubbleSource}";
            if (solid == 0)
            {
                // 단단한 말풍선이 없으면 비킴 규칙이 아무것도 재지 않는다 (V2 r4 — 예전에는 0개로 PASS)
                SkipCheck(id, head + " — 보이는 단단한 말풍선 없음, 비킴 규칙을 재지 못함");
                return;
            }
            var p = new Probe();
            p.Info(head.Substring(shot.Length + 2));
            ExpectSolidClear(p, list, keep, "보스 몸 · 이름표");
            Report(id, shot, p);
        }

        /// <summary>
        /// EnemyView의 '부모 레이어 로컬' 사각형(BodyRect · StatusRect — anchoredPosition 기준) → 화면 사각형 (기준 px). anchoredPosition과 localPosition의 차이를 더해
        /// 부모 로컬로 옮긴 뒤 부모 TransformPoint
        /// </summary>
        Rect EnemyLocalToScreen(EnemyView enemy, Rect local)
        {
            var rt = (RectTransform)enemy.transform;
            var space = rt.parent;
            if (space == null) return local;
            var shift = (Vector2)rt.localPosition - rt.anchoredPosition;
            var a = RefPoint(space.TransformPoint(new Vector3(local.xMin + shift.x, local.yMin + shift.y, 0f)));
            var b = RefPoint(space.TransformPoint(new Vector3(local.xMax + shift.x, local.yMax + shift.y, 0f)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// S-boss-crit (W3 요청): 3초 동안 프레임마다 보스(TargetId = 보스 Id)가 맞은 치명타 숫자(BattleFx._numbers — Alive · Shown · Crit)가 처음 보인 프레임의
        /// 화면 가운데 x가 보스 몸 가운데 ± 몸 폭 절반(+2) 안인지. 하나라도 밖이면 FAIL (처음 어긋난 값), 치명타를 못 봤으면 SKIP
        /// </summary>
        IEnumerator BossCrits(string id, string shot)
        {
            var fx = FindAnyObjectByType<BattleFx>();
            var field = typeof(BattleFx).GetField("_numbers", BindingFlags.Instance | BindingFlags.NonPublic);
            var element = field != null ? field.FieldType.GetElementType() : null;
            const BindingFlags member = BindingFlags.Instance | BindingFlags.Public;
            var alive = element?.GetField("Alive", member);
            var shownField = element?.GetField("Shown", member);
            var crit = element?.GetField("Crit", member);
            var target = element?.GetField("TargetId", member);
            var rectField = element?.GetField("Rect", member);
            var spawned = element?.GetField("Spawned", member);
            if (fx == null || alive == null || shownField == null || crit == null || target == null || rectField == null)
            {
                SkipCheck(id, $"{shot}: BattleFx._numbers (Alive · Shown · Crit · TargetId · Rect) 없음");
                yield break;
            }
            int seen = 0, outside = 0;
            string first = null;
            // 숫자 칸 i를 마지막으로 센 '만든 시각' (같은 숫자가 떠 있는 동안 다시 세지 않는다)
            var counted = new Dictionary<int, float>();
            float limit = Time.realtimeSinceStartup + BossCritWatchSeconds;
            while (Time.realtimeSinceStartup < limit)
            {
                var boss = FindBoss();
                if (boss == null) break;
                if (field.GetValue(fx) is System.Array numbers && boss.BodyRect(out var bodyLocal))
                {
                    var body = EnemyLocalToScreen(boss, bodyLocal);
                    for (int i = 0; i < numbers.Length; i++)
                    {
                        var n = numbers.GetValue(i);
                        if (!(alive.GetValue(n) is bool on && on) || !(shownField.GetValue(n) is bool vis && vis) || !(crit.GetValue(n) is bool isCrit && isCrit)) continue;
                        if (!(target.GetValue(n) is int owner) || owner != boss.Id || rectField.GetValue(n) is not RectTransform rect) continue;
                        float born = spawned != null && spawned.GetValue(n) is float t ? t : 0f;
                        if (counted.TryGetValue(i, out float last) && Mathf.Approximately(last, born)) continue;
                        counted[i] = born;
                        seen++;
                        float x = ScreenRect(rect).center.x;
                        float gap = Mathf.Abs(x - body.center.x) - body.width * 0.5f;
                        if (gap <= BossCritSlack) continue;
                        outside++;
                        first ??= $"치명타 x {x:0.#} · 몸 {body.xMin:0.#}–{body.xMax:0.#} (밖 {gap:0.#}px)";
                    }
                }
                yield return null;
            }
            if (seen == 0)
            {
                SkipCheck(id, $"{shot}: {BossCritWatchSeconds:0}초 동안 보스가 맞은 치명타 숫자 없음");
                yield break;
            }
            Check(id, outside == 0, $"{shot}: 보스 치명타 {seen}개 중 몸 폭 밖 {outside}개 (기대 0, ±{BossCritSlack:0}px){(first != null ? " — 처음 " + first : string.Empty)}");
        }

        static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        // ═════════════════════════ 29 battle_later · 보스 진행 줄 ═════════════════════════

        /// <summary>
        /// S-29 · S-boss-bar (§5.2 29 '보스 막대가 처치를 따른다' · M02 · M11): 스테이지 판 진행 줄이 시뮬레이터를 따른다.
        /// 보스 전 = 글 '{처치}/{목표}' 그대로 · 막대(BarRatio) = 처치/목표 ±0.02, 보스 중 = 글 '{남은 체력 % 올림}%' ±1 · 막대 = 남은 체력 ±0.03.
        /// 막대는 감쇠로 따라가므로(BattleHud — 초당 10) 1.5초 안에 맞는 프레임이 하나라도 있으면 PASS, 없으면 마지막으로 어긋난 프레임 값으로 FAIL.
        /// 판단할 수 없는 프레임(보스가 쓰러짐 · 스테이지 끝)만 있었으면 SKIP, 파밍 중(진행 줄 숨김)도 SKIP
        /// </summary>
        IEnumerator BossBarSettles(string id, string shot)
        {
            var hud = FindAnyObjectByType<BattleHud>();
            var fill = hud != null ? Field<Image>(hud, "_progressFill") : null;
            var text = hud != null ? Field<TMP_Text>(hud, "_progressText") : null;
            var label = hud != null ? Field<TMP_Text>(hud, "_bossLabel") : null;
            if (fill == null || text == null || Game.Battle == null)
            {
                SkipCheck(id, $"{shot}: BattleHud._progressFill · _progressText · 전투 없음");
                yield break;
            }
            string mismatch = null;
            string unknown = null;
            int frames = 0;
            float limit = Time.realtimeSinceStartup + BossBarWatchSeconds;
            while (true)
            {
                if (!text.gameObject.activeInHierarchy)
                {
                    SkipCheck(id, $"{shot}: 진행 줄 숨김 — 파밍 중 '{(label != null ? label.text : string.Empty)}'");
                    yield break;
                }
                int verdict = BossBarMatches(Game.Battle.Sim, fill, text, out string detail);
                if (verdict > 0)
                {
                    Check(id, true, $"{shot}: {detail} · {frames}프레임 뒤");
                    yield break;
                }
                if (verdict == 0) mismatch = detail;
                else unknown = detail;
                if (Time.realtimeSinceStartup >= limit) break;
                frames++;
                yield return null;
            }
            if (mismatch != null) Check(id, false, $"{shot}: {BossBarWatchSeconds:0.#}초({frames}프레임) 동안 맞는 프레임 없음 — 마지막 {mismatch}");
            else SkipCheck(id, $"{shot}: 판단할 수 없는 상태만 ({unknown})");
        }

        /// <summary>진행 줄 한 프레임: 1 = 맞음, 0 = 어긋남, −1 = 판단할 수 없음 (스테이지가 끝났거나 보스가 쓰러짐). detail = 잰 값 · 기대</summary>
        static int BossBarMatches(BattleSimulator sim, Image fill, TMP_Text text, out string detail)
        {
            float bar = BattleEase.BarRatio(fill);
            string shown = ShownText(text);
            if (sim == null || sim.Outcome != BattleOutcome.Running)
            {
                detail = $"스테이지 끝 ({(sim != null ? sim.Outcome.ToString() : "시뮬레이터 없음")}) · 글 '{shown}' · 막대 {bar:0.000}";
                return -1;
            }
            if (sim.BossSpawned)
            {
                var boss = sim.Boss;
                if (boss == null)
                {
                    detail = $"보스 쓰러짐 · 글 '{shown}' · 막대 {bar:0.000}";
                    return -1;
                }
                float ratio = boss.MaxHp > 0f ? Mathf.Clamp01(boss.Hp / boss.MaxHp) : 0f;
                int percent = Mathf.Clamp(Mathf.CeilToInt(ratio * 100f - 0.0001f), 0, 100);
                bool textOk = shown.EndsWith("%") && int.TryParse(shown.Substring(0, shown.Length - 1), out int value) && Mathf.Abs(value - percent) <= 1;
                bool barOk = Mathf.Abs(bar - ratio) <= BossBarTolerance;
                detail = $"보스 체력 {ratio:0.000} → 글 '{shown}' (기대 '{percent}%' ±1) · 막대 {bar:0.000} (기대 ±{BossBarTolerance:0.00})";
                return textOk && barOk ? 1 : 0;
            }
            int kills = sim.Kills;
            int total = Mathf.Max(1, sim.KillsToBoss);
            string expected = $"{Mathf.Min(kills, total)}/{total}";
            float target = Mathf.Clamp01(kills / (float)total);
            bool match = shown == expected && Mathf.Abs(bar - target) <= KillBarTolerance;
            detail = $"처치 {kills}/{total} → 글 '{shown}' (기대 '{expected}') · 막대 {bar:0.000} (기대 {target:0.000} ±{KillBarTolerance:0.00})";
            return match ? 1 : 0;
        }
    }
}
