using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 열린 에디터에서 프로젝트를 스스로 세팅한다 (메뉴를 직접 누르지 않아도 Play만 누르면 되게).
    /// - 컴파일이 끝날 때마다 Play 버튼이 어느 씬에서든 Main 씬부터 시작하도록 playModeStartScene을 지정한다.
    /// - SceneVersion이 마지막으로 만든 버전과 다르면(빌더/데이터 코드가 바뀜) 아트 임포트를 마무리하고
    ///   데이터 + UI + 메인 씬을 다시 만든 뒤 Main 씬을 연다.
    /// 배치 모드(검증용 복사본)에서는 아무것도 하지 않는다 — 배치는 IdleProjectSetup.BatchBuildAll을 직접 부른다.
    /// </summary>
    [InitializeOnLoad]
    internal static class IdleAutoSetup
    {
        /// <summary>
        /// 씬/데이터를 다시 만들어야 하는 변경이 들어오면 올린다
        /// (2: 새 화면 프레임 WP-IA + FX juice v2 · 3: UX Wave 2+3 (메뉴·캐릭터·메타·튜토리얼·전투 HUD) + FX round 2 decals)
        /// </summary>
        public const int SceneVersion = 3;

        /// <summary>마지막으로 자동 생성에 성공한 버전 (이 컴퓨터의 프로젝트별, 버전 관리 밖)</summary>
        const string StampPath = "Library/IdleGameAutoSetup.txt";
        /// <summary>이번 에디터 세션에서 이미 시도한 버전 (취소 뒤 같은 세션에서 반복하지 않게 — 실패하면 지우고 다음 컴파일 뒤 다시 시도)</summary>
        const string TriedKey = "IdleGame.AutoSetup.Tried";
        /// <summary>
        /// 이 파일이 있으면 자동 세팅을 미룬다: 여러 작업이 스크립트와 빌더를 고치는 도중에 반쯤 바뀐 빌더로 씬을 만들지 않게.
        /// 작업이 끝나 검증을 통과하면 지우고 SceneVersion을 올린다 (그 컴파일 뒤 한 번 다시 만든다)
        /// </summary>
        const string HoldPath = "Library/IdleGameAutoSetup.hold";
        const string HoldLoggedKey = "IdleGame.AutoSetup.HoldLogged";
        const string ProgressTitle = "LOST & LOVELY 자동 세팅";

        static double _nextCheck;

        static IdleAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.update += OnUpdate;
        }

        [MenuItem("IdleGame/Setup/0. 자동 세팅 지금 실행", priority = 0)]
        static void RunNow()
        {
            SessionState.EraseInt(TriedKey);
            if (File.Exists(StampPath)) File.Delete(StampPath);
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            // 컴파일/임포트/플레이 중에는 기다린다 (1초마다 다시 확인)
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorApplication.update -= OnUpdate;
            PointPlayButtonAtMain();

            if (ReadStamp() == SceneVersion || SessionState.GetInt(TriedKey, 0) == SceneVersion) return;
            if (File.Exists(HoldPath))
            {
                // 다음 컴파일(도메인 리로드) 때 다시 확인한다. 같은 세션에서는 한 번만 알린다
                if (SessionState.GetInt(HoldLoggedKey, 0) != SceneVersion)
                {
                    SessionState.SetInt(HoldLoggedKey, SceneVersion);
                    Debug.Log($"[IdleGame] 자동 세팅 보류 중 (v{SceneVersion}): 프로젝트를 고치는 작업이 끝나면 저절로 다시 만들어요. 지금 Main 씬은 마지막으로 만든 그대로예요.");
                }
                return;
            }
            SessionState.SetInt(TriedKey, SceneVersion);
            Rebuild();
        }

        /// <summary>Play를 누르면 열려 있는 씬과 상관없이 Main 씬부터 시작</summary>
        static void PointPlayButtonAtMain()
        {
            var main = AssetDatabase.LoadAssetAtPath<SceneAsset>(IdleSceneBuilder.ScenePath);
            if (main != null && EditorSceneManager.playModeStartScene != main) EditorSceneManager.playModeStartScene = main;
        }

        static void Rebuild()
        {
            if (!IdleProjectSetup.HasTmpResources)
            {
                // TMP 패키지 임포트는 비동기로 끝나므로, 임포트 뒤 다음 도메인 리로드에서 다시 시도한다
                Debug.Log("[IdleGame] 자동 세팅: TMP 기본 리소스를 먼저 임포트합니다.");
                SessionState.EraseInt(TriedKey);
                IdleProjectSetup.SetupProject();
                return;
            }
            if (HasDirtyScene() && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[IdleGame] 자동 세팅을 취소했어요. 메뉴 IdleGame/Setup/0 으로 다시 실행할 수 있어요.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, "아트 임포트 규칙 적용 중…", 0.1f);
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar(ProgressTitle, "데이터 + UI + 메인 씬 생성 중…", 0.4f);
                IdleProjectSetup.Build(interactive: false);

                if (SceneManager.GetActiveScene().path != IdleSceneBuilder.ScenePath)
                    EditorSceneManager.OpenScene(IdleSceneBuilder.ScenePath, OpenSceneMode.Single);
                PointPlayButtonAtMain();

                File.WriteAllText(StampPath, SceneVersion.ToString());
                Debug.Log($"[IdleGame] 자동 세팅 완료 (v{SceneVersion}). Play를 누르면 Main 씬부터 시작해요.");
            }
            catch (Exception e)
            {
                // 스크립트가 아직 바뀌는 중일 수 있다: 다음 컴파일(도메인 리로드) 때 다시 시도한다
                SessionState.EraseInt(TriedKey);
                Debug.LogError($"[IdleGame] 자동 세팅 실패: {e.Message}\n다음 컴파일 뒤 자동으로 다시 시도하고, 메뉴 IdleGame/Setup/0 으로 바로 다시 실행할 수도 있어요.\n{e}");
                // 반쯤 만든 새 씬 대신 마지막으로 저장된 Main 씬으로 돌아간다 (실패한 빌드는 저장하지 않는다)
                try
                {
                    if (File.Exists(IdleSceneBuilder.ScenePath) && SceneManager.GetActiveScene().path != IdleSceneBuilder.ScenePath)
                        EditorSceneManager.OpenScene(IdleSceneBuilder.ScenePath, OpenSceneMode.Single);
                }
                catch (Exception reopen)
                {
                    Debug.LogWarning($"[IdleGame] Main 씬을 다시 열지 못했어요: {reopen.Message}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static bool HasDirtyScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty) return true;
            }
            return false;
        }

        static int ReadStamp()
        {
            try
            {
                return File.Exists(StampPath) && int.TryParse(File.ReadAllText(StampPath).Trim(), out int version) ? version : 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }
    }
}
