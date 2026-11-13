using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using IdleGame.EditorTools;
using IdleGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace IdleGame.Verify
{
    /// <summary>
    /// 장면 정적 검사 (UX-SPEC §4.3 WP-9 UiSceneLintTests · §5.3, UX-SPEC-IA §5.8 · §8.7). Main.unity(+ UI 프리팹)를 열어 규칙마다 실패 목록을 만든다.
    /// 쓰는 곳: EditMode UiSceneLintTests(이 어셈블리를 참조할 수 없어 Run(규칙)을 이름으로 부른다) · BatchVerify.RunStaticChecks(lint.txt).
    /// <para>
    /// 규칙: targets = 코치 · FTUE 대상 경로(CoachTargets 상수 · 덱 칸 · MetaContent.Targets · 메타 콘텐츠의 focus/coach 대상)가 Canvas 아래에 있다 /
    /// names = 고정 이름 · 경로(IA §5.8, S §0.2 — 시트 머리 · ≡ 메뉴 칸 · 전장 · 스모크 테스트와 AutoShot이 찾는 이름) /
    /// filled = Filled Image에 그림이 있다 (M02 — 그림 없는 Filled는 줄지 않는다) /
    /// font = TMP 글자 기본 크기 ≥ 16, 자동 크기 최소 ≥ 16 (18 아래는 세기만 — §1.3) /
    /// hit = 버튼 누르는 자리(그림 + raycastPadding) ≥ 88 x 88 (§1.5, 기준 1080x1920 앵커로 잰다 — 레이아웃 그룹이 정하는 크기는 건너뜀) /
    /// copy = 화면 글에 '개방' · '습니다' · '하시겠'이 없다 (§1.10) / glyph = 화면 글의 모든 글자가 글꼴(Noto Sans KR + 대신 글꼴)에 있다.
    /// </para>
    /// <para>
    /// SPEC4 정적 검사 (RM-2 V2, SPEC4 §11.4 · §13.3 · §13.5 f): gear = 플레이어가 보는 글(Scripts/UI · Scripts/Systems · Editor/Builders · Editor/Content의
    /// 문자열 상수 — 주석은 뺀다)에 '장비'가 없다 (짝짝이 이야기 줄만 남긴다) / reflect = Scripts/UI 코드에 'GetField('가 없다 (FtueBattleGuard는 S-9까지 빚).
    /// 둘 다 소스 파일을 읽는다 (장면 글은 빌더 소스에서 나온다).
    /// </para>
    /// <para>
    /// 알려진 예외는 Exemptions 한 곳에만 둔다 (구조 예외 = 늘 빼는 것, 빚 = 지금 장면에 남은 알려진 문제 + 고칠 담당). 예외가 더는 걸리지 않으면
    /// 메모에 'stale'로 적는다 — 고쳐졌으면 목록에서 지운다. 빈 글(실행 중에 채움) · 피해 숫자 틀 · 대신 글자는 글 규칙에서 뺀다 (IdleSceneBuilder 검사와 같다).
    /// </para>
    /// </summary>
    public static class UiSceneLint
    {
        public const string RuleTargets = "targets";
        public const string RuleNames = "names";
        public const string RuleFilled = "filled";
        public const string RuleFont = "font";
        public const string RuleHit = "hit";
        public const string RuleCopy = "copy";
        public const string RuleGlyph = "glyph";
        public const string RuleGear = "gear";
        public const string RuleReflect = "reflect";

        /// <summary>모든 규칙 (lint.txt 순서)</summary>
        public static readonly string[] Rules = { RuleTargets, RuleNames, RuleFilled, RuleFont, RuleHit, RuleCopy, RuleGlyph, RuleGear, RuleReflect };

        /// <summary>
        /// gear 규칙이 읽는 소스 폴더 (Assets/_Project 아래 — 끝이 "/*"면 그 폴더 바로 아래 파일만) · 쓰지 않는 말. V2 r4: 장면 글을 만드는 Editor 바로 아래 파일
        /// (IdleSceneBuilder · UIKit · SceneParts · IdleDataBuilder · MetaDataBuilder …)과 Scripts/Core · Data · Account · Platform을 더했다
        /// </summary>
        static readonly string[] GearFolders =
        {
            "Scripts/UI", "Scripts/Systems", "Scripts/Core", "Scripts/Data", "Scripts/Account", "Scripts/Platform", "Editor/Builders", "Editor/Content", "Editor/*",
        };
        const string GearWord = "장비";
        /// <summary>reflect 규칙이 읽는 소스 폴더 · 찾는 말 (SPEC4 §13.3 'no GetField( under Scripts/UI')</summary>
        static readonly string[] ReflectFolders = { "Scripts/UI" };
        const string ReflectWord = "GetField(";

        const string PrefabDir = IdleProjectSetup.Root + "/Prefabs/UI";
        /// <summary>검사 기준 캔버스 (CanvasScaler 기준 해상도)</summary>
        static readonly Vector2 ReferenceCanvas = new(1080f, 1920f);

        /// <summary>알려진 예외 한 줄: 규칙 · 경로 조각(Canvas 기준 경로가 이것으로 끝나거나 이것 + '/'를 품으면 걸림) · 이유 · 담당</summary>
        public sealed class Exemption
        {
            public readonly string Rule;
            public readonly string Path;
            public readonly string Reason;
            /// <summary>true = 구조 예외 (늘 뺀다, stale을 보지 않는다) · false = 빚 (지금 남은 문제)</summary>
            public readonly bool Structural;
            /// <summary>
            /// 소스 규칙(gear · reflect)에서 그 파일의 이 글만 뺀다 (null = 파일 전체). ExactText면 걸린 문자열 상수가 이것과 같아야, 아니면 이것을 품어야 한다
            /// (V2 r4: 파일 전체 예외는 그 파일에 새로 넣은 '장비'까지 조용히 뺐다)
            /// </summary>
            public readonly string Text;
            public readonly bool ExactText;

            public Exemption(string rule, string path, string reason, bool structural)
            {
                Rule = rule;
                Path = path;
                Reason = reason;
                Structural = structural;
            }

            public Exemption(string rule, string path, string text, bool exactText, string reason, bool structural) : this(rule, path, reason, structural)
            {
                Text = text;
                ExactText = exactText;
            }

            public bool Matches(string rule, string path) =>
                rule == Rule && !string.IsNullOrEmpty(path) &&
                (path == Path || path.EndsWith("/" + Path, StringComparison.Ordinal) || path.EndsWith(":" + Path, StringComparison.Ordinal) ||
                 path.Contains(Path + "/") || path.StartsWith(Path + "/", StringComparison.Ordinal));

            /// <summary>소스 규칙 한 줄: 파일이 맞고, Text가 있으면 걸린 글(found)도 맞는다</summary>
            public bool MatchesHit(string rule, string path, string found) =>
                Matches(rule, path) && (Text == null || (found != null && (ExactText ? found == Text : found.IndexOf(Text, StringComparison.Ordinal) >= 0)));
        }

        /// <summary>
        /// 알려진 예외 (한 곳 — 2026-11-05 Wave 3 빌더 기준: BattleSceneBuilder · MetaSceneBuilder · MenuSceneBuilder를 규칙대로 다시 잼).
        /// 빚 줄의 담당이 고치면 지운다 (stale 메모 · 규칙 머리 줄의 stale=N이 알려 준다). Wave 3에서 고쳐진 줄은 지웠다 — AUTO 140x104 · x1 120x104 ·
        /// 두 잠금 덮개(같은 raycastPadding) · EmptyHint 560x88 · 끌기 안내 자동 16~22 · 잠금 덮개 NEW 알약 17 — 이제 규칙이 그대로 본다.
        /// </summary>
        public static readonly Exemption[] Exemptions =
        {
            // ── 구조 예외 ──
            new(RuleHit, "Battle/FxGallery", "개발 빌드 전용 FX 갤러리 (Debug.isDebugBuild)", true),
            // (FxGallery의 font · copy · glyph 구조 예외 셋은 지웠다 — 갤러리 글이 18 이상 · 화살표 기호 없음이라 V2_6 · p46_6 lint에서 한 번도 안 쓰였다, V2 part B)
            // ── 일부러 작은 누르는 자리 (Wave 3, WP-5A — 큰 누르는 칸 안의 보조 칸 · 이웃에 막힌 칸. 담당이 88로 넓히면 stale 메모가 알려 준다) ──
            new(RuleHit, "Battle/Hud/BossChallengeButton/Body/Recommend",
                "160x47 — 보스 도전 몸통(230x68, 누르는 칸 230x88) 안의 보조 링크 '권장 ›'(강해지기 창, C-12). 위는 제목 줄 · 오른쪽은 반복 단추라 더 못 넓힌다 — 둘레는 몸통이 받는다 (WP-5A M13)", false),
            new(RuleHit, "Battle/Hud/BossChallengeButton/Body/LoopToggle",
                "58x88 — 보스 도전 몸통 안의 44px 반복 단추 (높이는 몸통 누르는 칸 끝까지, 옆은 권장 · 제목 줄과 4 · 몸통 끝까지 10 — 더 넓히면 그쪽 누름을 가로챈다, WP-5A M13)", false),
            new(RuleHit, "Battle/Strip/DeckTab",
                "440x48 — 덱 이름표 알약 32 높이 (아래 3 · 위 13: 아래는 덱 카드 누르는 칸 위 끝, 위는 가이드 퀘스트 줄에 막힌다 — 잔향이 N/M일 때만 편성 탭, WP-5A M36)", false),
            new(RuleHit, "GachaPanel/GemSourceLink", "420x39 — 별조각 얻는 곳 링크 32 높이 (위 3 · 아래 4: 위는 소환 버튼, 아래는 누르는 소환 마일스톤 줄 MilestoneRow(88)에 막힌다 — 더 넓히면 뒤 형제인 링크가 줄의 막대 · 단계 점 누름을 가로챈다, P4-5 PRG-04)", false),
            // ── 빚: 누르는 자리 (M22) — RM-2: ReturnChip/CloseButton (KIT 줄 88 폭) · FormationPanel/UndoButton은 stale로 나와 지웠다 (verify_out_rm2_kit_2) ──
            // ── 빚: 글자 하한 (M23) — AimOverlay/Ghost/Cancel/Count는 P4-3b가 fontSizeMin 16으로 올려 stale (verify_out_rm2_V2_1) — 지웠다 ──
            // ── SPEC4 '장비' (§11.4 critic · §13.5 f) ──
            new(RuleGear, "Editor/Content/SkillContent.Jjakjjak.cs", "모두의 장비를 꼼꼼히 손봐요", false,
                "SPEC4 §11.4가 남기라고 한 짝짝이 이야기 줄 '모두의 장비를 꼼꼼히 손봐요' (이 줄만 — 같은 파일의 다른 '장비'는 걸린다)", true),
            new(RuleGear, "Editor/MetaDataBuilder.cs", GearWord, true,
                "MetaDataBuilder.BannedCopy의 금지어 목록 칸 \"장비\" (문구 검사기가 찾는 말 — 플레이어 글이 아니다, 이 상수 하나만)", true),
            // (CharacterSceneBuilder · EvolutionCraftPopup의 §13.5 f 글은 P4-5가 RM-2에 고쳤다 — 빚 줄을 지웠다)
            // ── SPEC4 리플렉션 (§13.3) — FtueBattleGuard.cs 빚 줄은 P4-6 B1(S-9 전투 훅)로 GetField가 빠져 stale — 지웠다 (P4-6 요청, V2 r4) ──
        };

        /// <summary>규칙 하나의 결과</summary>
        public sealed class Result
        {
            public readonly string Rule;
            public readonly List<string> Failures = new();
            public readonly List<string> Notes = new();
            public int Checked;
            /// <summary>이번에 걸린 것이 없는 빚 예외 수 (고쳐졌거나 이름이 바뀜 — Exemptions에서 지운다). 머리 줄에 stale=N으로 보인다</summary>
            public int Stale;

            public Result(string rule) => Rule = rule;

            public bool Ok => Failures.Count == 0;

            public string Report()
            {
                var text = new StringBuilder();
                text.Append(Ok ? "PASS " : "FAIL ").Append(Rule).Append(" checked=").Append(Checked).Append(" failures=").Append(Failures.Count);
                if (Stale > 0) text.Append(" stale=").Append(Stale);
                text.Append('\n');
                foreach (var line in Failures) text.Append("  FAIL ").Append(line).Append('\n');
                foreach (var line in Notes) text.Append("  NOTE ").Append(line).Append('\n');
                return text.ToString();
            }
        }

        // ═════════════════════════ 진입점 ═════════════════════════

        /// <summary>
        /// 테스트용 다리 (EditMode 테스트 어셈블리는 이 어셈블리를 참조하지 않아 이름으로 부른다): 규칙 하나를 돌려
        /// { 실패 목록(줄마다 하나, 통과면 ""), 메모 + 'checked=N' } 을 돌려준다
        /// </summary>
        public static string[] Run(string rule)
        {
            var result = RunRule(rule);
            return new[] { string.Join("\n", result.Failures), $"checked={result.Checked}\n" + string.Join("\n", result.Notes) };
        }

        /// <summary>
        /// 테스트용 다리 (장면을 한 번만 연다): 규칙마다 두 칸씩 { 실패 목록, 메모 } — 결과 길이 = 2 × rules.Length (Run과 같은 모양)
        /// </summary>
        public static string[] RunMany(string[] rules)
        {
            var output = new string[(rules?.Length ?? 0) * 2];
            if (rules == null) return output;
            using var scope = SceneScope.Open();
            for (int i = 0; i < rules.Length; i++)
            {
                var result = Evaluate(rules[i], scope);
                output[i * 2] = string.Join("\n", result.Failures);
                output[i * 2 + 1] = $"checked={result.Checked}\n" + string.Join("\n", result.Notes);
            }
            return output;
        }

        public static Result RunRule(string rule)
        {
            using var scope = SceneScope.Open();
            return Evaluate(rule, scope);
        }

        /// <summary>모든 규칙 (장면을 한 번만 연다)</summary>
        public static List<Result> RunAll()
        {
            var results = new List<Result>();
            using var scope = SceneScope.Open();
            foreach (var rule in Rules) results.Add(Evaluate(rule, scope));
            return results;
        }

        static Result Evaluate(string rule, SceneScope scope)
        {
            var result = new Result(rule);
            if (scope.Canvas == null)
            {
                result.Failures.Add($"{IdleSceneBuilder.ScenePath}: Canvas 없음 ({scope.Error})");
                return result;
            }
            try
            {
                switch (rule)
                {
                    case RuleTargets: Targets(scope.Canvas, result); break;
                    case RuleNames: Names(scope.Canvas, result); break;
                    case RuleFilled: Filled(scope, result); break;
                    case RuleFont: Font(scope, result); break;
                    case RuleHit: Hit(scope.Canvas, result); break;
                    case RuleCopy: Copy(scope, result); break;
                    case RuleGlyph: Glyph(scope, result); break;
                    case RuleGear: SourceWords(RuleGear, GearFolders, GearWord, true, result); break;
                    case RuleReflect: SourceWords(RuleReflect, ReflectFolders, ReflectWord, false, result); break;
                    default: result.Failures.Add($"모르는 규칙 '{rule}' (있는 규칙: {string.Join(", ", Rules)})"); break;
                }
            }
            catch (Exception e)
            {
                result.Failures.Add($"검사 중 예외 {e.GetType().Name}: {e.Message}");
            }
            if (rule != RuleTargets && rule != RuleNames) NoteStale(rule, result);
            return result;
        }

        // ═════════════════════════ targets · names ═════════════════════════

        /// <summary>코치 · FTUE 대상 경로 (MetaDataBuilder.ValidateTargetsInScene과 같은 모음 + MetaContent.Targets 상수 전부)</summary>
        static void Targets(Transform canvas, Result result)
        {
            var paths = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Add(string path, string user)
            {
                if (string.IsNullOrEmpty(path)) return;
                paths[path] = paths.TryGetValue(path, out var users) ? users + ", " + user : user;
            }
            AddConstants(typeof(CoachTargets), "CoachTargets.", Add);
            for (int i = 0; i < FormationSystem.EchoSlotCount; i++) Add(CoachTargets.DeckCard(i), $"CoachTargets.DeckCard({i})");
            var targets = typeof(MetaDataBuilder).Assembly.GetType("IdleGame.EditorTools.MetaContent+Targets");
            if (targets != null) AddConstants(targets, "MetaContent.Targets.", Add);
            else result.Notes.Add("MetaContent.Targets 형식을 못 찾음 (이름이 바뀌었으면 고친다)");
            var db = AssetDatabase.LoadAssetAtPath<MetaDatabase>(MetaDataBuilder.MetaPath);
            if (db != null)
            {
                foreach (var quest in db.guideQuests) Add(quest?.focusTarget, "quest " + quest?.id);
                foreach (var mission in db.missions) Add(mission?.focusTarget, "mission " + mission?.id);
                foreach (var step in db.ftueSteps) Add(step?.coachTarget, "ftue " + step?.id);
            }
            else result.Notes.Add("MetaDatabase 없음: " + MetaDataBuilder.MetaPath);

            foreach (var pair in paths)
            {
                result.Checked++;
                if (canvas.Find(pair.Key) == null) result.Failures.Add($"{pair.Key} (쓰는 곳: {pair.Value})");
            }
        }

        static void AddConstants(Type type, string prefix, Action<string, string> add)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string)) add((string)field.GetRawConstantValue(), prefix + field.Name);
            }
        }

        /// <summary>고정 경로 (IA §5.8 · S §0.2 · AutoShot · 스모크 테스트가 찾는 자리)</summary>
        public static readonly string[] FrozenPaths =
        {
            "SafeArea/ContentArea/SheetHeader",
            "SafeArea/ContentArea/SheetHeader/DragArea",
            "SafeArea/ContentArea/SheetHeader/HandleHit",
            "SafeArea/ContentArea/SheetHeader/ReturnChip",
            "Popups/MenuPopup/Window/Body/Grid/JournalTile",
            "Popups/MenuPopup/Window/Body/Grid/AttendanceTile",
            "Popups/MenuPopup/Window/Body/Grid/EventTile",
            "Popups/MenuPopup/Window/Body/Grid/BagTile",
            "Popups/MenuPopup/Window/Body/Grid/NoticeTile",
            "Popups/MenuPopup/Window/Body/Grid/SettingsTile",
            "SafeArea/StageArea/Battle/Field",
            "SafeArea/StageArea/Battle/SynergyBannerArea/SynergyBanner",
            "SafeArea/StageArea/Battle/BannerArea/Banner",
            "SafeArea/StageArea/Battle/Background/FieldArt",
            "SafeArea/StageArea/Battle/Hud/SynergyRow",
            "SafeArea/StageArea/Battle/Hud/EmptyHint",
            "SafeArea/StageArea/Battle/MetaHud/RecommendedPower",
            "SafeArea/StageArea/Battle/MetaHud/QuickRail_L/EventButton/Visual/LabelPill/Label",
            "SafeArea/StageArea/Battle/MetaHud/QuickRail_L/AttendanceButton/Visual/LabelPill/Label",
            "SafeArea/StageArea/Battle/MetaHud/QuickRail_R/JournalButton/Visual/LabelPill/Label",
            "SafeArea/TabBar/Indicator",
            "SafeArea/TabBar/Tab_Main/Glow",
            "SafeArea/SleepMode/Overlay/Frame/Card",
            "SafeArea/TopBar/LevelText",
            "SafeArea/TopBar/PowerChip",
            "SafeArea/TopBar/PowerChip/PowerText",
            "SafeArea/TopBar/GoldChip",
            "SafeArea/TopBar/GoldPerSecText",
            "SafeArea/TopBar/GemChip",
            "Popups/EvolutionCraftPopup",
            "Popups/IdleChestPopup/Window/Body",
            "Popups/JournalPopup/Window/Body",
            "Popups/AttendancePopup/Window/Body",
            "Popups/CharacterDetailPopup/Window/Body",
            "Popups/SynergyCodexPopup/Window/Body",
            "Popups/MailPopup/Window/Body/ClaimAllButton",
        };

        /// <summary>이름으로 찾는 것 (경로 앞부분 아래 어딘가에 그 이름) — 스모크 테스트 · TSB · MetaNavigator</summary>
        public static readonly (string root, string name)[] FrozenNames =
        {
            ("Popups/SettingsPopup", "Tab0"),
            ("Popups/SettingsPopup", "Tab1"),
            ("Popups/SettingsPopup", "Tab2"),
            ("Popups/SettingsPopup", "Tab3"),
            ("Popups/AttendancePopup", "CheckInButton"),
            ("SafeArea/ContentArea/ExpeditionPanel", "ClaimAllButton"),
            ("SafeArea/ContentArea/GrowthPanel", "LevelUp1Button"),
        };

        static void Names(Transform canvas, Result result)
        {
            foreach (var path in FrozenPaths)
            {
                result.Checked++;
                if (canvas.Find(path) == null) result.Failures.Add(path);
            }
            foreach (var (root, name) in FrozenNames)
            {
                result.Checked++;
                var under = canvas.Find(root);
                if (under == null || FindDeep(under, name) == null) result.Failures.Add($"{root}/…/{name}");
            }
            // 상단 바에 이름에 "Power"가 든 자식 (PowerUpFloatView가 찾는다, IA §5.8)
            result.Checked++;
            var bar = canvas.Find(CoachTargets.TopBar);
            bool power = false;
            for (int i = 0; bar != null && i < bar.childCount; i++)
            {
                if (bar.GetChild(i).name.Contains("Power")) power = true;
            }
            if (!power) result.Failures.Add($"{CoachTargets.TopBar}: 이름에 'Power'가 든 자식 없음");
        }

        static Transform FindDeep(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ═════════════════════════ filled ═════════════════════════

        static void Filled(SceneScope scope, Result result)
        {
            foreach (var (root, prefix) in scope.Roots())
            {
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.type != Image.Type.Filled) continue;
                    result.Checked++;
                    if (image.sprite != null) continue;
                    string path = prefix + PathOf(image.transform, root);
                    if (IsExempt(RuleFilled, path, out var reason)) result.Notes.Add($"예외 {path} — {reason}");
                    else result.Failures.Add($"{path} (Filled · 그림 없음 — 채움이 줄지 않는다, M02: Filled()에서 WhiteSprite)");
                }
            }
        }

        // ═════════════════════════ font ═════════════════════════

        static void Font(SceneScope scope, Result result)
        {
            int below18 = 0;
            foreach (var (root, prefix) in scope.Roots())
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (string.IsNullOrWhiteSpace(label.text) || IsTextStructural(label.transform)) continue;
                    result.Checked++;
                    string path = prefix + PathOf(label.transform, root);
                    string problem = null;
                    if (label.enableAutoSizing)
                    {
                        if (label.fontSizeMin < UIType.AutoSizeFloor - 0.01f) problem = $"자동 {label.fontSizeMin:0.#}~{label.fontSizeMax:0.#} (최소 < {UIType.AutoSizeFloor:0})";
                        else if (label.fontSizeMax < UIType.Floor) below18++;
                    }
                    else if (label.fontSize < UIType.AutoSizeFloor - 0.01f) problem = $"{label.fontSize:0.#} (< {UIType.AutoSizeFloor:0})";
                    else if (label.fontSize < UIType.Floor) below18++;
                    if (problem == null) continue;
                    if (IsExempt(RuleFont, path, out var reason)) result.Notes.Add($"예외 {path} {problem} — {reason}");
                    else result.Failures.Add($"{path} {problem}");
                }
            }
            result.Notes.Add($"16 이상 18 미만 (경고만, §1.3): {below18}개");
        }

        // ═════════════════════════ hit ═════════════════════════

        static void Hit(Transform canvas, Result result)
        {
            int skipped = 0;
            var root = canvas as RectTransform;
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                var graphic = button.targetGraphic;
                if (graphic == null || !graphic.raycastTarget || IsHitStructural(button.transform)) continue;
                string path = PathOf(button.transform, canvas);
                if (!TryReferenceSize(graphic.rectTransform, root, out var size))
                {
                    skipped++;
                    continue;
                }
                result.Checked++;
                var hit = VerifyMath.HitSize(size, graphic.raycastPadding);
                if (hit.x >= UISize.MinHit - 0.5f && hit.y >= UISize.MinHit - 0.5f) continue;
                string problem = $"{hit.x:0}x{hit.y:0} (< {UISize.MinHit:0})";
                if (IsExempt(RuleHit, path, out var reason)) result.Notes.Add($"예외 {path} {problem} — {reason}");
                else result.Failures.Add($"{path} {problem}");
            }
            result.Notes.Add($"크기를 앵커로 잴 수 없어(레이아웃 그룹 · 크기 맞춤) 건너뜀: {skipped}개");
        }

        /// <summary>누르는 자리 구조 예외: 스크롤 목록 안 칸(간격에 묶임) · 예전 시너지 줄 칸(M43 요약 칩 SynergySummary는 본다)</summary>
        static bool IsHitStructural(Transform button)
        {
            if (button.GetComponentInParent<ScrollRect>(true) != null) return true;
            bool summary = false;
            for (var t = button; t != null; t = t.parent)
            {
                if (t.name == "SynergySummary") summary = true;
                if (t.name == "SynergyRow" && !summary) return true;
            }
            return false;
        }

        /// <summary>기준 캔버스(1080x1920)에서의 크기 — 앵커 · sizeDelta로 잰다 (IdleSceneBuilder.LintHitTargets와 같은 방식). 레이아웃이 정하면 false</summary>
        static bool TryReferenceSize(RectTransform rt, RectTransform canvas, out Vector2 size)
        {
            size = default;
            if (rt == null) return false;
            if (rt == canvas)
            {
                size = ReferenceCanvas;
                return true;
            }
            if (rt.GetComponent<ContentSizeFitter>() != null || rt.GetComponent<AspectRatioFitter>() != null || rt.GetComponent<BackgroundFit>() != null) return false;
            if (!(rt.parent is RectTransform parent) || IsLayoutDriven(rt, parent)) return false;
            bool stretchX = !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);
            bool stretchY = !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);
            var parentSize = Vector2.zero;
            if ((stretchX || stretchY) && !TryReferenceSize(parent, canvas, out parentSize)) return false;
            size = new Vector2(
                stretchX ? parentSize.x * (rt.anchorMax.x - rt.anchorMin.x) + rt.sizeDelta.x : rt.sizeDelta.x,
                stretchY ? parentSize.y * (rt.anchorMax.y - rt.anchorMin.y) + rt.sizeDelta.y : rt.sizeDelta.y);
            return size.x > 0f && size.y > 0f;
        }

        static bool IsLayoutDriven(RectTransform rt, RectTransform parent)
        {
            if (rt.TryGetComponent<LayoutElement>(out var element) && element.ignoreLayout) return false;
            if (parent.GetComponent<GridLayoutGroup>() != null) return true;
            var group = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            return group != null && (group.childControlWidth || group.childControlHeight);
        }

        // ═════════════════════════ copy ═════════════════════════

        static void Copy(SceneScope scope, Result result)
        {
            foreach (var (root, prefix) in scope.Roots())
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (string.IsNullOrWhiteSpace(label.text) || IsTextStructural(label.transform)) continue;
                    result.Checked++;
                    string word = VerifyMath.FindForbiddenCopy(label.text);
                    if (word == null) continue;
                    string path = prefix + PathOf(label.transform, root);
                    string problem = $"'{word}' in \"{Short(label.text)}\"";
                    if (IsExempt(RuleCopy, path, out var reason)) result.Notes.Add($"예외 {path} {problem} — {reason}");
                    else result.Failures.Add($"{path} {problem}");
                }
            }
        }

        // ═════════════════════════ glyph ═════════════════════════

        static void Glyph(SceneScope scope, Result result)
        {
            s_face = null;
            var cache = new Dictionary<(int font, uint code), bool>();
            var seen = new HashSet<TMP_FontAsset>();
            var missing = new StringBuilder();
            foreach (var (root, prefix) in scope.Roots())
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (string.IsNullOrWhiteSpace(label.text) || IsTextStructural(label.transform)) continue;
                    result.Checked++;
                    var font = label.font;
                    if (font == null)
                    {
                        result.Failures.Add($"{prefix}{PathOf(label.transform, root)}: 글꼴 없음");
                        continue;
                    }
                    string plain = label.richText ? VerifyMath.StripRichText(label.text) : label.text;
                    missing.Clear();
                    for (int i = 0; i < plain.Length; i++)
                    {
                        uint code = plain[i];
                        if (char.IsHighSurrogate(plain[i]) && i + 1 < plain.Length && char.IsLowSurrogate(plain[i + 1]))
                        {
                            code = (uint)char.ConvertToUtf32(plain[i], plain[i + 1]);
                            i++;
                        }
                        if (code < 0x20 || code == 0x200B || code == 0xFEFF) continue;
                        var key = (font.GetInstanceID(), code);
                        if (!cache.TryGetValue(key, out bool has))
                        {
                            seen.Clear();
                            has = FontHas(font, code, seen);
                            cache[key] = has;
                        }
                        if (!has && missing.ToString().IndexOf(char.ConvertFromUtf32((int)code), StringComparison.Ordinal) < 0)
                            missing.Append(char.ConvertFromUtf32((int)code));
                    }
                    if (missing.Length == 0) continue;
                    string path = prefix + PathOf(label.transform, root);
                    string problem = $"없는 글자 '{missing}' (U+{(int)char.ConvertToUtf32(missing.ToString(), 0):X4}…) in \"{Short(plain)}\" ({font.name})";
                    if (IsExempt(RuleGlyph, path, out var reason)) result.Notes.Add($"예외 {path} {problem} — {reason}");
                    else result.Failures.Add($"{path} {problem}");
                }
            }
        }

        /// <summary>
        /// 그 글꼴(+ 대신 글꼴 표 + TMP Settings 대신 글꼴)에 그 글자가 있는지. 동적 글꼴은 아틀라스에 아직 없는 글자도 원본 글꼴 파일(FontEngine)에서 찾는다 —
        /// 아틀라스에 넣지 않는다 (애셋을 바꾸지 않게)
        /// </summary>
        static bool FontHas(TMP_FontAsset font, uint code, HashSet<TMP_FontAsset> seen)
        {
            if (font == null || !seen.Add(font)) return false;
            if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(code)) return true;
            var source = font.sourceFontFile;
            if (source != null && LoadFace(source) &&
                FontEngine.TryGetGlyphWithUnicodeValue(code, GlyphLoadFlags.LOAD_NO_BITMAP, out var glyph) && glyph != null && glyph.index != 0)
                return true;
            if (font.fallbackFontAssetTable != null)
            {
                foreach (var fallback in font.fallbackFontAssetTable)
                {
                    if (FontHas(fallback, code, seen)) return true;
                }
            }
            var global = TMP_Settings.fallbackFontAssets;
            if (global != null)
            {
                foreach (var fallback in global)
                {
                    if (FontHas(fallback, code, seen)) return true;
                }
            }
            return false;
        }

        /// <summary>FontEngine에 지금 올려 둔 글꼴 (글자마다 다시 읽지 않게 — 바뀔 때만 LoadFontFace)</summary>
        static Font s_face;

        static bool LoadFace(Font source)
        {
            if (s_face == source) return true;
            if (FontEngine.LoadFontFace(source) != FontEngineError.Success)
            {
                s_face = null;
                return false;
            }
            s_face = source;
            return true;
        }

        // ═════════════════════════ gear · reflect (소스) ═════════════════════════

        /// <summary>
        /// 소스 검사: folders(Assets/_Project 아래)의 .cs를 줄마다 읽어 주석(// · /* */)을 빼고, inLiterals면 문자열 상수 안에서만, 아니면 코드(문자열 밖)에서 word를 찾는다.
        /// 걸린 곳은 파일 하나에 한 줄 ("Editor/…/X.cs" + 줄 번호들 · 첫 글) — 예외는 파일 경로로 맞춘다. checked = 읽은 파일 수
        /// </summary>
        static void SourceWords(string rule, string[] folders, string word, bool inLiterals, Result result)
        {
            string root = System.IO.Path.GetFullPath(IdleProjectSetup.Root);
            foreach (var folder in folders)
            {
                // "폴더/*" = 그 폴더 바로 아래 파일만 (Editor 바로 아래 — Builders · Content는 따로 훑는다)
                bool topOnly = folder.EndsWith("/*", StringComparison.Ordinal);
                string sub = topOnly ? folder.Substring(0, folder.Length - 2) : folder;
                string dir = System.IO.Path.Combine(root, sub);
                if (!System.IO.Directory.Exists(dir))
                {
                    result.Notes.Add($"폴더 없음 {folder}");
                    continue;
                }
                var files = System.IO.Directory.GetFiles(dir, "*.cs", topOnly ? System.IO.SearchOption.TopDirectoryOnly : System.IO.SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var file in files)
                {
                    result.Checked++;
                    string path = file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
                    var lines = System.IO.File.ReadAllLines(file);
                    // 줄마다 예외를 본다 (예외가 그 글만 뺄 수 있다 — Exemption.Text)
                    var hits = new StringBuilder();
                    var exempt = new StringBuilder();
                    string first = null, firstExempt = null, reason = null;
                    bool block = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string found = FindInLine(lines[i], word, inLiterals, ref block);
                        if (found == null) continue;
                        if (IsExemptHit(rule, path, found, out var why))
                        {
                            if (exempt.Length > 0) exempt.Append(',');
                            exempt.Append(i + 1);
                            firstExempt ??= found;
                            reason ??= why;
                            continue;
                        }
                        if (hits.Length > 0) hits.Append(',');
                        hits.Append(i + 1);
                        first ??= found;
                    }
                    if (exempt.Length > 0) result.Notes.Add($"예외 {path}:{exempt} '{word}' in \"{Short(firstExempt)}\" — {reason}");
                    if (hits.Length > 0) result.Failures.Add($"{path}:{hits} '{word}' in \"{Short(first)}\"");
                }
            }
        }

        /// <summary>
        /// 한 줄에서 주석을 뺀 코드를 훑는다 (block = 여러 줄 /* */ 안인지, 다음 줄로 넘긴다). inLiterals면 "…" · @"…" · $"…" 문자열 안에서 word를 찾아 그 문자열을,
        /// 아니면 문자열 · 문자 상수 밖 코드에서 찾아 그 줄을 돌려준다. 없으면 null (단순 훑기 — 여러 줄 문자열 · 보간 안 중첩 문자열은 대충 본다)
        /// </summary>
        static string FindInLine(string line, string word, bool inLiterals, ref bool block)
        {
            var code = new StringBuilder(line.Length);
            var literal = new StringBuilder();
            bool inString = false, verbatim = false, inChar = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';
                if (block)
                {
                    if (c == '*' && next == '/')
                    {
                        block = false;
                        i++;
                    }
                    continue;
                }
                if (inString)
                {
                    if (!verbatim && c == '\\')
                    {
                        literal.Append(c).Append(next);
                        i++;
                        continue;
                    }
                    if (c == '"')
                    {
                        if (verbatim && next == '"')
                        {
                            literal.Append('"');
                            i++;
                            continue;
                        }
                        inString = false;
                        if (inLiterals && literal.ToString().IndexOf(word, StringComparison.Ordinal) >= 0) return literal.ToString();
                        literal.Clear();
                        continue;
                    }
                    literal.Append(c);
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') i++;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && next == '/') break;
                if (c == '/' && next == '*')
                {
                    block = true;
                    i++;
                    continue;
                }
                if (c == '"')
                {
                    inString = true;
                    verbatim = i > 0 && (line[i - 1] == '@' || (i > 1 && line[i - 1] == '$' && line[i - 2] == '@'));
                    continue;
                }
                if (c == '\'')
                {
                    inChar = true;
                    continue;
                }
                code.Append(c);
            }
            if (inLiterals) return inString && literal.ToString().IndexOf(word, StringComparison.Ordinal) >= 0 ? literal.ToString() : null;
            return code.ToString().IndexOf(word, StringComparison.Ordinal) >= 0 ? line.Trim() : null;
        }

        // ═════════════════════════ 예외 · 도우미 ═════════════════════════

        /// <summary>소스 규칙 한 줄의 예외 (파일 + 걸린 글 — Exemption.MatchesHit)</summary>
        static bool IsExemptHit(string rule, string path, string found, out string reason)
        {
            foreach (var exemption in Exemptions)
            {
                if (!exemption.MatchesHit(rule, path, found)) continue;
                reason = exemption.Structural ? "구조 예외: " + exemption.Reason : "빚: " + exemption.Reason;
                return true;
            }
            reason = null;
            return false;
        }

        static bool IsExempt(string rule, string path, out string reason)
        {
            foreach (var exemption in Exemptions)
            {
                if (!exemption.Matches(rule, path)) continue;
                reason = exemption.Structural ? "구조 예외: " + exemption.Reason : "빚: " + exemption.Reason;
                return true;
            }
            reason = null;
            return false;
        }

        /// <summary>
        /// 빚 예외 중 이번에 걸린 것이 없으면 메모 + Result.Stale (고쳐졌거나 이름이 바뀜 — 목록에서 지운다). 실패로 보지는 않는다:
        /// 예외 목록은 WP-9 몫이고 고치는 쪽은 다른 담당이라, 같은 웨이브에 빚을 갚으면 이 검사가 빨개져 버린다 — 머리 줄 stale=N으로 남긴다
        /// </summary>
        static void NoteStale(string rule, Result result)
        {
            foreach (var exemption in Exemptions)
            {
                if (exemption.Rule != rule) continue;
                bool used = false;
                foreach (var note in result.Notes)
                {
                    if (note.StartsWith("예외 ", StringComparison.Ordinal) && note.Contains(exemption.Path)) used = true;
                }
                if (used) continue;
                // 구조 예외는 세지 않고 메모만 (V2 part B — 안 쓰이는 구조 예외도 눈에 띄게: 장면이 바뀌어 필요 없어졌으면 지운다)
                if (exemption.Structural)
                {
                    result.Notes.Add($"안 쓰인 구조 예외 (실패 아님 — 필요 없으면 Exemptions에서 지운다): {rule} {exemption.Path}");
                    continue;
                }
                result.Stale++;
                result.Notes.Add($"stale 예외 (이제 통과하거나 없음 — Exemptions에서 지운다): {rule} {exemption.Path}");
            }
        }

        /// <summary>글 규칙 구조 예외: 피해 숫자 틀 · 대신 글자(그림이 없을 때만 보임) · Damage* 풀 (IdleSceneBuilder.IsTextLintExempt와 같다)</summary>
        static bool IsTextStructural(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                string name = t.name;
                if (name == "NumberTemplate" || name == "Fallback" || name.StartsWith("Damage", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static string PathOf(Transform target, Transform root)
        {
            var path = new StringBuilder(target.name);
            for (var t = target.parent; t != null && t != root; t = t.parent) path.Insert(0, t.name + "/");
            return target == root ? target.name : path.ToString();
        }

        static string Short(string text)
        {
            string flat = text.Replace('\n', ' ');
            return flat.Length <= 40 ? flat : flat.Substring(0, 40) + "…";
        }

        /// <summary>
        /// Main.unity를 쓰는 동안: 이미 열려 있으면 그대로 쓰고, 아니면 덧붙여 열었다가 끝나면 닫는다 (사용자 장면 구성을 바꾸지 않는다).
        /// UI 프리팹도 함께 넘긴다 (글 규칙)
        /// </summary>
        sealed class SceneScope : IDisposable
        {
            public Transform Canvas;
            public string Error;
            Scene _scene;
            bool _opened;
            readonly List<(Transform root, string prefix)> _prefabs = new();

            public static SceneScope Open()
            {
                var scope = new SceneScope();
                try
                {
                    var scene = SceneManager.GetSceneByPath(IdleSceneBuilder.ScenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(IdleSceneBuilder.ScenePath, OpenSceneMode.Additive);
                        scope._opened = true;
                    }
                    scope._scene = scene;
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        if (go.name == "Canvas") scope.Canvas = go.transform;
                    }
                    if (scope.Canvas == null) scope.Error = "루트에 Canvas 없음";
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null) scope._prefabs.Add((prefab.transform, System.IO.Path.GetFileName(path) + ":"));
                    }
                }
                catch (Exception e)
                {
                    scope.Error = e.Message;
                }
                return scope;
            }

            /// <summary>장면 Canvas(경로 앞 없음) + UI 프리팹 루트("이름.prefab:" 앞)</summary>
            public IEnumerable<(Transform root, string prefix)> Roots()
            {
                if (Canvas != null) yield return (Canvas, string.Empty);
                foreach (var prefab in _prefabs) yield return prefab;
            }

            public void Dispose()
            {
                if (_opened && _scene.IsValid() && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(_scene, true);
            }
        }
    }
}
