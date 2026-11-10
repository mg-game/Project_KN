using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IdleGame.Verify
{
    /// <summary>
    /// 임시 검증 도구 (배치 모드 -executeMethod 전용).
    /// 라이선스 문제로 -runTests를 못 쓰므로 TestRunnerApi를 동기로 돌려 결과를 파일로 남긴다.
    /// <para>
    /// RunStaticChecks (-quit와 함께): MetaDataBuilder.ValidateTargetsInScene(코치 · FTUE 대상) → targets.txt, UiSceneLint 모든 규칙 → lint.txt.
    /// RunPlayModeTests (-quit 없이): PlayMode 테스트(스모크 · UiFrameTests · UiBehaviourTests)는 플레이 모드에 들어가야 해서 동기로 못 돈다 —
    /// 실행을 걸고 돌아온 뒤 RunFinished 알림에서 playmode.txt를 쓰고 EditorApplication.Exit(실패 &gt; 0 ? 1 : 하나도 안 돎 ? 3 : 0)로 끝낸다.
    /// 플레이 모드에 들어갈 때 도메인이 다시 읽히므로(알림 등록이 사라진다) 진행 표시를 SessionState에 두고 [InitializeOnLoadMethod]에서 다시 등록한다.
    /// 끝 시간(PlayModeTimeoutMinutes)이 지나면 TIMEOUT을 쓰고 2로 끝낸다. 라이선스 문제로 에디터가 먼저 꺼지면(198) verify2.ps1이 SKIP으로 적는다.
    /// </para>
    /// <para>
    /// RM-2 V2: editmode.txt · playmode.txt에 묶음(테스트 클래스)마다 "FIXTURE 이름: 통과/실패[/건너뜀]" 줄 (게이트 증거를 바로 읽게 — G1_prep §2).
    /// -testFilter A,B (클래스 이름 앞부분, 쉼표)는 보고만 줄인다: 전체를 그대로 돌리고(1분 안), TOTAL 첫 줄은 전체, 둘째 줄 "FILTER …"와 실패 · 묶음 줄은 고른 것만
    /// (plan §5.3). 앞부분이 묶음을 하나도 못 고르면 "FAIL FILTER matched 0 fixtures" 줄 + 끝 코드 6. CompileAndroidScripts (-quit와 함께): PlayerBuildInterface.CompilePlayerScripts(Android)로
    /// 플레이어 스크립트를 컴파일해 android.txt ("TOTAL android ok= assemblies= errors= platform=" + 어셈블리 · 오류)를 쓴다 — P4-7 IdleGame.Platform 같은 Android 전용
    /// asmdef를 실제로 본다 (ok는 IdleGame.Platform.dll이 나와야 True, 아니면 끝 코드 1).
    /// </para>
    /// </summary>
    public static class BatchVerify
    {
        static string OutDir => Arg("-verifyOut") ?? Path.GetFullPath("Temp/verify");

        /// <summary>테스트가 하나도 돌지 않았을 때(통과 + 실패 = 0 — 어셈블리 · 필터가 아무것도 고르지 않음, 빈 결과로 끝남)의 끝 코드</summary>
        const int NoTestsExit = 3;
        /// <summary>-testFilter 앞부분이 묶음(fixture)을 하나도 고르지 않았을 때의 끝 코드 (전체는 돌았다 — 보고만 비었다)</summary>
        const int FilterEmptyExit = 6;

        /// <summary>
        /// EditMode 테스트를 동기로 돌려 editmode.txt ("TOTAL passed= failed= skipped=" 첫 줄 + 실패마다 이름 · 메시지 · 스택 4줄)를 쓴다.
        /// 하나도 돌지 않았거나(통과 + 실패 = 0) 끝 알림이 오지 않았으면 첫 줄에 그 사실을 적고 FAIL 로그 + 끝 코드 3 (거짓 PASS가 되지 않게)
        /// </summary>
        public static void RunEditModeTests()
        {
            Directory.CreateDirectory(OutDir);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var collector = new Collector(FilterPrefixes());
            api.RegisterCallbacks(collector);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }) { runSynchronously = true });
            string path = Path.Combine(OutDir, "editmode.txt");
            File.WriteAllText(path, collector.Text());
            Debug.Log($"[Verify] {(collector.Ok ? "PASS" : "FAIL")} editmode {collector.Totals()} — {path}");
            if (collector.Ran == 0 || !collector.Finished) EditorApplication.Exit(NoTestsExit);
            // -testFilter가 묶음을 하나도 못 고르면(오타) 빈 보고가 PASS처럼 보인다 — FAIL 줄 + 끝 코드 (V2 r4)
            else if (collector.FilterMatchedNothing)
            {
                Debug.LogError($"[Verify] FAIL editmode -testFilter {string.Join(",", collector.Prefixes)} matched 0 fixtures");
                EditorApplication.Exit(FilterEmptyExit);
            }
        }

        // ═════════════════════════ 복사본 스크립트 다시 가져오기 (scripts.txt) ═════════════════════════

        /// <summary>스크립트 폴더 (Assets/_Project 아래 — 다시 가져오는 곳)</summary>
        static readonly string[] ScriptFolders = { "Scripts", "Editor", "Verify", "Tests" };

        /// <summary>클래스를 잃었는지 보는 대표 스크립트 (IdleGame 데이터 · 부트스트랩 — BatchBuildAll이 만드는 ScriptableObject)</summary>
        static readonly string[] ScriptProbes =
        {
            "Scripts/Data/CharacterDatabase.cs", "Scripts/Data/GameConfig.cs", "Scripts/Data/MetaDatabase.cs", "Scripts/Data/BattleConfig.cs", "Scripts/Core/GameBootstrap.cs",
        };

        /// <summary>
        /// 검증 복사본(-quit와 함께, BatchBuildAll 앞에 따로 한 세션): 열린 사용자 에디터의 Library를 복사한 프로젝트는 IdleGame MonoScript가 클래스를 잃을 수 있다
        /// (MonoScript.GetClass() = null → BatchBuildAll 'No script asset for …' ×12 · 장면에 스크립트 없음 — 2026-11-05 ≈17:13부터 rm2_proj 복사본마다, V2 r4 진단).
        /// 스크립트 폴더를 ForceUpdate로 다시 가져오면 다음 세션부터 클래스가 돌아온다 (같은 세션에서는 아직 null). scripts.txt 첫 줄
        /// "TOTAL scripts reimported folders=N probes=M missingClassBefore=K" — 사용자 프로젝트에서는 부르지 않는다 (다시 가져오기만 할 뿐 바꾸는 것은 없지만 오래 걸린다)
        /// </summary>
        public static void ReimportScripts()
        {
            Directory.CreateDirectory(OutDir);
            int missing = 0;
            var text = new StringBuilder();
            foreach (var probe in ScriptProbes)
            {
                string path = IdleGame.EditorTools.IdleProjectSetup.Root + "/" + probe;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                bool lost = script == null || script.GetClass() == null;
                if (lost) missing++;
                text.Append($"PROBE {probe} script={script != null} class={(lost ? "null" : script.GetClass().FullName)}\n");
            }
            int folders = 0;
            foreach (var folder in ScriptFolders)
            {
                string path = IdleGame.EditorTools.IdleProjectSetup.Root + "/" + folder;
                if (!AssetDatabase.IsValidFolder(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                folders++;
            }
            string total = $"TOTAL scripts reimported folders={folders} probes={ScriptProbes.Length} missingClassBefore={missing}";
            File.WriteAllText(Path.Combine(OutDir, "scripts.txt"), total + "\n" + text);
            Debug.Log($"[Verify] {total}");
        }

        public static void BuildPlayer()
        {
            var path = Path.GetFullPath("Builds/Win/LostAndLovely.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { IdleGame.EditorTools.IdleSceneBuilder.ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "build.txt"), $"{report.summary.result} {report.summary.totalErrors} errors\n{path}");
            Debug.Log($"[Verify] 빌드 결과: {report.summary.result}");
        }

        // ═════════════════════════ Android 스크립트 컴파일 (android.txt) ═════════════════════════

        /// <summary>
        /// 플레이어 스크립트를 Android로 컴파일한다 (빌드 대상을 바꾸지 않는다 — PlayerBuildInterface.CompilePlayerScripts, 결과는 Temp/verify_android).
        /// android.txt 첫 줄 "TOTAL android ok=True|False assemblies=N errors=M platform=True|False" (platform = IdleGame.Platform.dll이 나왔는지), 이어서
        /// 어셈블리 이름 · 'error CS' 줄 (최대 80). 실패면 끝 코드 1 (EditorApplication.Exit — -quit 전에)
        /// </summary>
        public static void CompileAndroidScripts()
        {
            Directory.CreateDirectory(OutDir);
            string target = Path.GetFullPath("Temp/verify_android");
            var errors = new List<string>();
            void OnLog(string condition, string stackTrace, LogType type)
            {
                if ((type == LogType.Error || type == LogType.Exception) && errors.Count < 80) errors.Add(condition.Replace('\n', ' '));
            }
            Application.logMessageReceived += OnLog;
            List<string> assemblies = null;
            string failure = null;
            try
            {
                Directory.CreateDirectory(target);
                var settings = new ScriptCompilationSettings { target = BuildTarget.Android, group = BuildTargetGroup.Android, options = ScriptCompilationOptions.None };
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, target);
                assemblies = result.assemblies != null ? new List<string>(result.assemblies) : new List<string>();
            }
            catch (Exception e)
            {
                failure = $"{e.GetType().Name}: {e.Message}";
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            assemblies ??= new List<string>();
            int compileErrors = 0;
            foreach (var line in errors)
            {
                if (line.Contains("error CS")) compileErrors++;
            }
            bool platform = assemblies.Exists(name => name.StartsWith("IdleGame.Platform", StringComparison.Ordinal));
            // IdleGame.Platform이 나오는지가 이 단계의 목적이다 (plan 델타 21) — 빠지면(asmdef 제외 · 이름 바뀜) 실패 (V2 r4)
            bool ok = failure == null && assemblies.Count > 0 && compileErrors == 0 && platform;
            var text = new StringBuilder();
            text.Append($"TOTAL android ok={ok} assemblies={assemblies.Count} errors={compileErrors} platform={platform}\n");
            if (failure != null) text.Append("EXCEPTION ").Append(failure).Append('\n');
            foreach (var name in assemblies) text.Append("ASSEMBLY ").Append(name).Append('\n');
            foreach (var line in errors) text.Append("LOG ").Append(line).Append('\n');
            File.WriteAllText(Path.Combine(OutDir, "android.txt"), text.ToString());
            Debug.Log($"[Verify] {(ok ? "PASS" : "FAIL")} android 어셈블리 {assemblies.Count} · 오류 {compileErrors} · IdleGame.Platform {platform}");
            if (!ok) EditorApplication.Exit(1);
        }

        // ═════════════════════════ 정적 검사 (targets.txt · lint.txt) ═════════════════════════

        /// <summary>
        /// 장면을 만든 뒤(BatchBuildAll) 부른다: 코치 대상 확인(MetaDataBuilder.ValidateTargetsInScene — 없는 경로는 오류 로그도 남긴다) → targets.txt
        /// ("TOTAL missing=N" 첫 줄 + 없는 경로), UiSceneLint 규칙 일곱 → lint.txt ("TOTAL rules=7 failed=N" 첫 줄 + 규칙마다 PASS|FAIL · 실패 · 메모).
        /// 두 파일 모두 [Verify] 한 줄로도 로그에 남긴다
        /// </summary>
        public static void RunStaticChecks()
        {
            Directory.CreateDirectory(OutDir);
            var targets = new StringBuilder();
            try
            {
                var missing = IdleGame.EditorTools.MetaDataBuilder.ValidateTargetsInScene();
                targets.Append("TOTAL missing=").Append(missing.Count).Append('\n');
                foreach (var path in missing) targets.Append("MISSING ").Append(path).Append('\n');
                Debug.Log($"[Verify] {(missing.Count == 0 ? "PASS" : "FAIL")} targets ValidateTargetsInScene: 없는 경로 {missing.Count}개");
            }
            catch (Exception e)
            {
                targets.Append("TOTAL missing=-1\nERROR ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n');
                Debug.LogException(e);
            }
            File.WriteAllText(Path.Combine(OutDir, "targets.txt"), targets.ToString());

            var lint = new StringBuilder();
            try
            {
                var results = UiSceneLint.RunAll();
                int failed = 0;
                foreach (var result in results)
                {
                    if (!result.Ok) failed++;
                    Debug.Log($"[Verify] {(result.Ok ? "PASS" : "FAIL")} lint-{result.Rule} 검사 {result.Checked} · 실패 {result.Failures.Count}" +
                              (result.Stale > 0 ? $" · stale 예외 {result.Stale}" : string.Empty));
                }
                lint.Append("TOTAL rules=").Append(results.Count).Append(" failed=").Append(failed).Append('\n');
                foreach (var result in results) lint.Append(result.Report());
            }
            catch (Exception e)
            {
                lint.Append("TOTAL rules=0 failed=-1\nERROR ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n');
                Debug.LogException(e);
            }
            File.WriteAllText(Path.Combine(OutDir, "lint.txt"), lint.ToString());
        }

        // ═════════════════════════ PlayMode (playmode.txt) ═════════════════════════

        const string PlayModeRunKey = "IdleGame.Verify.PlayModeRun";
        const string PlayModeOutKey = "IdleGame.Verify.PlayModeOut";
        const string PlayModeDeadlineKey = "IdleGame.Verify.PlayModeDeadline";
        const string PlayModeExitKey = "IdleGame.Verify.PlayModeExit";
        /// <summary>PlayMode 실행 상한 (분) — 넘으면 TIMEOUT을 쓰고 끝낸다 (verify2.ps1도 따로 프로세스를 끊는다)</summary>
        const float PlayModeTimeoutMinutes = 20f;

        static PlayModeCollector s_playMode;

        /// <summary>
        /// PlayMode 테스트를 건다 (배치 -executeMethod, -quit 없이). 결과는 playmode.txt
        /// ("TOTAL passed= failed= skipped=" 첫 줄 + 실패마다 이름 · 메시지 · 스택 4줄), 끝나면 EditorApplication.Exit(실패 &gt; 0 ? 1 : 하나도 안 돎 ? 3 : 0)
        /// </summary>
        public static void RunPlayModeTests()
        {
            Directory.CreateDirectory(OutDir);
            SessionState.SetBool(PlayModeRunKey, true);
            SessionState.SetString(PlayModeOutKey, OutDir);
            SessionState.SetFloat(PlayModeDeadlineKey, (float)EditorApplication.timeSinceStartup + PlayModeTimeoutMinutes * 60f);
            SessionState.EraseInt(PlayModeExitKey);
            File.WriteAllText(Path.Combine(OutDir, "playmode.txt"), "TOTAL running (결과 전 — 이 줄이 남으면 실행이 끝나지 못했다)\n");
            RegisterPlayMode();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
            Debug.Log("[Verify] PlayMode 테스트 시작 (끝나면 playmode.txt를 쓰고 에디터를 닫는다)");
        }

        /// <summary>도메인을 다시 읽은 뒤(플레이 모드 들어감 · 나옴) 알림 · 감시를 다시 건다 — PlayMode 검증 실행 중일 때만</summary>
        [InitializeOnLoadMethod]
        static void ResumePlayModeRun()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(PlayModeRunKey, false)) return;
            RegisterPlayMode();
        }

        static void RegisterPlayMode()
        {
            if (s_playMode == null)
            {
                s_playMode = new PlayModeCollector();
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(s_playMode);
            }
            EditorApplication.update -= WatchPlayMode;
            EditorApplication.update += WatchPlayMode;
        }

        /// <summary>끝났으면 플레이 모드를 다 나온 뒤 닫는다 · 상한을 넘으면 TIMEOUT</summary>
        static void WatchPlayMode()
        {
            int exit = SessionState.GetInt(PlayModeExitKey, -1);
            if (exit >= 0)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.timeSinceStartup < SessionState.GetFloat(PlayModeDeadlineKey, 0f)) return;
                Finish(exit);
                return;
            }
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(PlayModeDeadlineKey, float.MaxValue)) return;
            WritePlayMode($"TOTAL timeout after {PlayModeTimeoutMinutes:0} min (결과 알림이 오지 않았다)\n");
            Finish(2);
        }

        static void Finish(int exit)
        {
            EditorApplication.update -= WatchPlayMode;
            SessionState.EraseBool(PlayModeRunKey);
            SessionState.EraseInt(PlayModeExitKey);
            Debug.Log($"[Verify] PlayMode 끝 — exit {exit}");
            EditorApplication.Exit(exit);
        }

        static void WritePlayMode(string text)
        {
            string dir = SessionState.GetString(PlayModeOutKey, OutDir);
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "playmode.txt"), text);
            }
            catch (IOException e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// 결과 모으기 — 도메인이 다시 읽혀도 되게 TestFinished에 쌓지 않고 RunFinished의 결과 나무 전체를 훑는다
        /// </summary>
        sealed class PlayModeCollector : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(PlayModeRunKey, false)) return;
                var counts = new int[3];
                var failures = new StringBuilder();
                var fixtures = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
                Walk(result, counts, failures, fixtures);
                // 하나도 돌지 않았으면(어셈블리 · 필터가 아무것도 고르지 않음, 빈 결과 나무로 끝남) 거짓 PASS가 되지 않게 FAIL + 끝 코드 3
                bool none = counts[0] + counts[1] == 0;
                string total = TotalLine(counts[0], counts[1], counts[2], true);
                WritePlayMode($"{total}\n{failures}{FixtureLines(fixtures, null)}");
                Debug.Log($"[Verify] {(counts[1] == 0 && !none ? "PASS" : "FAIL")} playmode {total.Substring("TOTAL ".Length)}");
                SessionState.SetInt(PlayModeExitKey, counts[1] > 0 ? 1 : none ? NoTestsExit : 0);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            static void Walk(ITestResultAdaptor node, int[] counts, StringBuilder failures, SortedDictionary<string, int[]> fixtures)
            {
                if (node == null) return;
                if (node.HasChildren)
                {
                    foreach (var child in node.Children) Walk(child, counts, failures, fixtures);
                    return;
                }
                int slot = node.TestStatus == TestStatus.Passed ? 0 : node.TestStatus == TestStatus.Failed ? 1 : 2;
                counts[slot]++;
                CountFixture(fixtures, FixtureOf(node.Test, node.FullName), slot);
                if (slot == 1) failures.Append($"FAIL {node.FullName}\n  {node.Message?.Trim()}\n  {Collector.FirstLines(node.StackTrace, 4)}\n");
            }
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

        /// <summary>-testFilter A,B → 앞부분 목록 (없으면 null = 모두)</summary>
        static string[] FilterPrefixes()
        {
            string raw = Arg("-testFilter");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var list = new List<string>();
            foreach (var part in raw.Split(',', ';'))
            {
                string prefix = part.Trim();
                if (prefix.Length > 0) list.Add(prefix);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        /// <summary>테스트 클래스 이름이 앞부분 중 하나로 시작하는지 (전체 이름 'IdleGame.Tests.X' 앞부분도 받는다). 목록이 없으면 늘 true</summary>
        static bool Matches(string fixture, string fullName, string[] prefixes)
        {
            if (prefixes == null) return true;
            foreach (var prefix in prefixes)
            {
                if (fixture.StartsWith(prefix, StringComparison.Ordinal) || (fullName != null && fullName.StartsWith(prefix, StringComparison.Ordinal))) return true;
            }
            return false;
        }

        /// <summary>테스트가 든 클래스 이름 (TypeInfo, 없으면 전체 이름에서 괄호 앞 끝에서 두 번째 조각)</summary>
        static string FixtureOf(ITestAdaptor test, string fullName)
        {
            NUnit.Framework.Interfaces.ITypeInfo type = test?.TypeInfo;
            if (type != null && !string.IsNullOrEmpty(type.Name)) return type.Name;
            string name = fullName ?? test?.FullName ?? string.Empty;
            int paren = name.IndexOf('(');
            if (paren >= 0) name = name.Substring(0, paren);
            var parts = name.Split('.');
            return parts.Length >= 2 ? parts[parts.Length - 2] : name;
        }

        static void CountFixture(SortedDictionary<string, int[]> fixtures, string fixture, int slot)
        {
            if (!fixtures.TryGetValue(fixture, out var counts)) fixtures[fixture] = counts = new int[3];
            counts[slot]++;
        }

        /// <summary>"FIXTURE 이름: 통과/실패" 줄 (건너뜀이 있으면 '/건너뜀') — 머리 줄 하나 + 묶음마다 한 줄 (prefixes가 있으면 고른 것만)</summary>
        static string FixtureLines(SortedDictionary<string, int[]> fixtures, string[] prefixes)
        {
            var text = new StringBuilder("FIXTURES (class: passed/failed[/skipped])\n");
            foreach (var pair in fixtures)
            {
                if (!Matches(pair.Key, null, prefixes)) continue;
                var c = pair.Value;
                text.Append("FIXTURE ").Append(pair.Key).Append(": ").Append(c[0]).Append('/').Append(c[1]);
                if (c[2] > 0) text.Append('/').Append(c[2]);
                text.Append('\n');
            }
            return text.ToString();
        }

        /// <summary>
        /// 결과 첫 줄 "TOTAL passed= failed= skipped=" — 통과 + 실패 = 0이면 끝에 ' (no tests ran)', 끝 알림이 없었으면 ' (run did not finish)'.
        /// verify2.ps1은 '^TOTAL passed='로 끝난 실행을 알아보므로 앞부분 모양은 그대로 둔다
        /// </summary>
        static string TotalLine(int passed, int failed, int skipped, bool finished)
        {
            string line = $"TOTAL passed={passed} failed={failed} skipped={skipped}";
            if (passed + failed == 0) line += " (no tests ran)";
            if (!finished) line += " (run did not finish)";
            return line;
        }

        sealed class Collector : ICallbacks
        {
            readonly List<(string fixture, string fullName, string text)> _failures = new();
            readonly SortedDictionary<string, int[]> _fixtures = new(StringComparer.Ordinal);
            readonly string[] _prefixes;
            int _passed, _failed, _skipped;

            public Collector(string[] prefixes) => _prefixes = prefixes;

            /// <summary>-testFilter 앞부분 (없으면 빈 배열)</summary>
            public string[] Prefixes => _prefixes ?? Array.Empty<string>();

            /// <summary>-testFilter가 있는데 돈 묶음 중 하나도 고르지 않았다 (오타 — 빈 보고)</summary>
            public bool FilterMatchedNothing => _prefixes != null && Finished && MatchedFixtures() == 0;

            int MatchedFixtures()
            {
                int count = 0;
                foreach (var pair in _fixtures)
                {
                    if (Matches(pair.Key, null, _prefixes)) count++;
                }
                return count;
            }

            /// <summary>RunFinished가 왔는지 (runSynchronously면 Execute가 돌아오기 전에 온다)</summary>
            public bool Finished { get; private set; }

            /// <summary>실제로 돈 테스트 (통과 + 실패 — 건너뜀은 세지 않는다)</summary>
            public int Ran => _passed + _failed;

            public bool Ok => Finished && Ran > 0 && _failed == 0;

            /// <summary>첫 줄에서 'TOTAL '을 뺀 것 (로그 한 줄용)</summary>
            public string Totals() => TotalLine(_passed, _failed, _skipped, Finished).Substring("TOTAL ".Length);

            /// <summary>
            /// editmode.txt: TOTAL(전체) · [FILTER 고른 것 합] · 실패(고른 것 — 밖의 실패는 수만) · FIXTURES 묶음 줄 (고른 것). verify2.ps1은 첫 80줄을 보이므로 실패를 묶음 줄보다 먼저
            /// </summary>
            public string Text()
            {
                var text = new StringBuilder(TotalLine(_passed, _failed, _skipped, Finished)).Append('\n');
                if (_prefixes != null)
                {
                    int fixtures = 0;
                    var sum = new int[3];
                    foreach (var pair in _fixtures)
                    {
                        if (!Matches(pair.Key, null, _prefixes)) continue;
                        fixtures++;
                        for (int i = 0; i < 3; i++) sum[i] += pair.Value[i];
                    }
                    text.Append($"FILTER prefixes={string.Join(",", _prefixes)} fixtures={fixtures} passed={sum[0]} failed={sum[1]} skipped={sum[2]}\n");
                    if (fixtures == 0) text.Append($"FAIL FILTER matched 0 fixtures (prefixes={string.Join(",", _prefixes)} — class name typo? exit {FilterEmptyExit})\n");
                }
                // 고른 묶음 밖의 실패는 이름만 한 줄에 (V2 r6: 예전에는 수만 적어 어느 테스트인지 몰랐다 — 메시지 · 스택은 거르지 않은 판에서)
                var outside = new List<string>();
                foreach (var (fixture, fullName, line) in _failures)
                {
                    if (Matches(fixture, fullName, _prefixes)) text.Append(line).Append('\n');
                    else outside.Add(fullName);
                }
                if (outside.Count > 0) text.Append($"OTHER FAILURES {outside.Count} (outside -testFilter, names only): {string.Join(" · ", outside)}\n");
                text.Append(FixtureLines(_fixtures, _prefixes));
                return text.ToString();
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result) => Finished = true;

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                string fixture = FixtureOf(result.Test, result.FullName);
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        _passed++;
                        CountFixture(_fixtures, fixture, 0);
                        break;
                    case TestStatus.Failed:
                        _failed++;
                        CountFixture(_fixtures, fixture, 1);
                        _failures.Add((fixture, result.FullName, $"FAIL {result.FullName}\n  {result.Message?.Trim()}\n  {FirstLines(result.StackTrace, 4)}"));
                        break;
                    default:
                        _skipped++;
                        CountFixture(_fixtures, fixture, 2);
                        break;
                }
            }

            internal static string FirstLines(string text, int count)
            {
                if (string.IsNullOrEmpty(text)) return string.Empty;
                var lines = text.Split('\n');
                return string.Join("\n  ", lines, 0, Math.Min(count, lines.Length));
            }
        }
    }
}
