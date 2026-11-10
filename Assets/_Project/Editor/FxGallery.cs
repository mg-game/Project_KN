using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using IdleGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 공격 연출 시험장 배치 캡처 v2 (ATK-17 / FX-JUICE §7.1). 재생 모드 없이 에디터에서 한 번에 돈다 —
    /// <c>-executeMethod IdleGame.EditorTools.FxGallery.Capture [-verifyOut 폴더]</c> (scratchpad/fx_capture.ps1이 fx_proj 복사본에서 부른다).
    ///   ① 메인 씬을 열고 화면 캔버스를 임시 카메라(1080x1920 렌더 텍스처)에 그리도록 바꾼다 (씬은 저장하지 않는다). 전장을 덮는 HUD·팝업 그림
    ///      (새 화면 틀의 위 띠 HUD · 빠른 아이콘 · 가이드 퀘스트 · 덱 탭 · 시트 …)과 빈 지킴이 초상화는 숨긴다 (실제 전투처럼 — probe.txt에 숨긴 수와 남긴 바닥 그림).
    ///      설정은 BattleFx.DevOverride 기본값 (기기 PlayerPrefs와 상관없이 같은 그림). 전장 크기 확인: "[Verify] PASS|FAIL fx_extents" (B8 · IA-11 — MAIN에서
    ///      FieldTop = VisibleTop = h/2, FieldBottom = VisibleBottom = −h/2, SafeTop 화면 y ≥ 250)
    ///   두 띠 (새 화면 틀은 전장이 1080x1464 — 기준선·tune1은 1080x624):
    ///      · 기준 띠 = 전장 로컬 y −312 ~ +312 (1080x624, 기준선과 같은 자리 — 수선점·광장 링이 띠의 같은 픽셀, 수선점 = 띠 위에서 375.5px).
    ///        넓이 % 지표가 기준선과 비교되게 probe2 와 full/ 은 이 띠로, 대역 없이 (예전처럼 빈 받침대)
    ///      · 보이는 전장 띠 = 전장 창 전체 (MAIN 화면 y 130~1594) — full_view/ (대역을 세운 새 화면 그대로: 위에서 내리는 별비 · 스테이지 클리어 등불 …)
    ///   ② 항목마다 Random.InitState(1234 + 순번) → 무대 세우기(Stage) → 0.4초 → 재생(Play) → 핵심 순간(도착) 둘레를 1/60초씩 BattleFx·시험장을 직접 진행하며
    ///      (대역을 세운다 — 데이터 에셋이 있으면 다섯 받침대에 대역 지킴이 · 잔향이 항목에는 대역 잔향이, 쏘는 몸짓까지 보인다):
    ///      {폴더}/fx/{key}.png — 도착 +50ms, 볼 자리(FocusPoint) 둘레 200x200 (예전과 같다)
    ///      {폴더}/fx/strips/{key}.png — 한 줄 12칸 300x300: 도착 −50 · 0 · 17 · 33 · 50 · 83 · 117 · 167 · 250 · 400 · 600 · 900ms (연출 시계)
    ///      {폴더}/fx/full/{key}_{tag}.jpg — 화면 크기 항목(보스 처치 · 불 밝히기 · 치명타 · 스테이지 클리어 · 무리 · 큰 스킬 · 설정 변형)만, 기준 띠 · 대역 없이:
    ///        clean(재생 전) · m100(화면 연출 순간 전, 연출 시계) · p000(그 순간) · p033 · p100 · p250 · p500 · p900 (그 순간 뒤 실제 시간)
    ///        — 화면 연출 순간 = FxGalleryView.ScreenMoment (불 밝히기는 터지는 순간, 나머지는 도착)
    ///      {폴더}/fx/full_view/{key}_{tag}.jpg — 같은 순간들, 보이는 전장 띠 · 대역을 세운 채로
    ///      {폴더}/fx/metrics.tsv — 항목마다 재생 뒤 최대 흔들림(px, trauma + 킥만) · 확대 · 어둡게 · 빛 · 틴트 · 위쪽/바닥 입자 · 보이는 숫자 · 실루엣 ·
    ///        SafeTop · 줌 중심 보정 이동(px) · 예산 탈락 합(위쪽 / 그중 영웅 층 / 바닥) · 버린 화면 큐 · 품질 (대역 몸짓의 반짝임 몫이 입자 수에 든다)
    ///   ③ 기준 비교용 probe2 프레임 ({폴더}/probe2/{key}__{tag}.jpg + probe.txt, FX-JUICE 기준선 19개, 기준 띠 · 대역 없이 —
    ///      fxj/strips.py · peaks.py가 그대로 읽는다: 띠 가운데 = 전장 로컬 (0, 0), cy = 312 − fy)
    ///   ④ 할당 확인 (BattleFx.Advance + 시험장 Advance — 모든 연출 호출이 시험장 Advance 안에서 나간다. 시험장이 허수아비를 다시 세운 프레임은 시험장 몫을 뺀다):
    ///      busy_20 60프레임 데운 뒤 120프레임 → "[Verify] fx_alloc_bytes=N" (0이어야 한다) · 후반 큰 숫자(×12,345, 단위 표기 캐시)로 한 번 돌려 캐시를 채운 뒤
    ///      다시 → "fx_alloc_bytes_late" · mass_kill_20 (처치 20 · 퇴장 · 다시 세우기)도 같은 두 번 → "fx_alloc_bytes_masskill".
    ///      성능 대조 (데스크톱 대용): busy_20 120프레임의 fx.Advance · Canvas.ForceUpdateCanvases(그림 다시 만들기) · 카메라 그리기 평균 ms —
    ///      지금 방식(배율·렌더러 색) vs 예전 방식(매 프레임 sizeDelta · Image.color) → "[Verify] fx_perf …"
    ///   ⑤ 높음 품질 항목에서 영웅 층(별빛 층이 아닌 입자)이 예산에 막히면 "[Verify] FAIL hero_rejected" 를 남기고 배치를 코드 3으로 끝낸다.
    ///   ⑥ 끝나면 씬을 다시 열어 바뀐 것을 버린다.
    /// 목록은 {폴더}/fx/index.txt (key · 이름 · 도착 시간). 실패해도 예외로 배치를 멈추지 않고 [Verify] 로그를 남긴다.
    /// </summary>
    public static class FxGallery
    {
        const float Step = 1f / 60f;
        const float PreRoll = 0.4f;
        /// <summary>정지·느린 화면이 길게 걸려도 한 항목을 이 실제 시간 넘게 돌리지 않는다</summary>
        const float MaxRealTime = 4f;
        const int CropSize = 200;
        const int StripCell = 300;
        const int SingleMs = 50;
        const int Width = 1080;
        const int Height = 1920;
        const int JpgQuality = 92;
        const string ConfigPath = IdleProjectSetup.Root + "/Data/BattleConfig.asset";
        /// <summary>대역 지킴이·잔향이의 형태·초상화 (Game 없이 BattleUnitArt가 그림을 못 찾으므로 여기서 채운다)</summary>
        const string CharactersPath = IdleProjectSetup.Root + "/Data/CharacterDatabase.asset";
        /// <summary>기준 띠 반높이 (전장 로컬 y −312 ~ +312 = 기준선·tune1의 1080x624 전장 띠, fxj 스크립트의 HALF_H)</summary>
        const float RefBandHalfHeight = 312f;
        /// <summary>기준선의 수선점 전장 로컬 y (mapper.Origin.y) — 다르면 기준 띠 속 광장 자리가 달라 넓이 %를 그대로 비교할 수 없다 (경고)</summary>
        const float BaselineOriginY = -63.5f;
        /// <summary>IA-11: SafeTop이 닿아야 하는 화면 y (위에서, 1080x1920 기준)</summary>
        const float SafeTopMinScreenY = 249.5f;
        const string AllocKey = "busy_20";
        const string MassAllocKey = "mass_kill_20";
        /// <summary>후반 큰 숫자 확인의 피해 배율 (96 → 1.19M · 512 → 6.32M — 단위 표기 캐시 길)</summary>
        const float LateDamageScale = 12345.678f;
        /// <summary>배치 끝의 종료 코드 (높음 품질에서 영웅 층이 예산에 막힘)</summary>
        const int HeroRejectedExitCode = 3;

        /// <summary>이번 캡처에서 높음 품질 영웅 층이 막힌 항목 (key=수)</summary>
        static readonly List<string> HeroRejections = new();

        /// <summary>띠 한 줄의 순간 (도착 기준 ms)</summary>
        static readonly int[] StripMs = { -50, 0, 17, 33, 50, 83, 117, 167, 250, 400, 600, 900 };
        /// <summary>화면 크기 항목의 전장 전체 순간 (도착 기준 ms)</summary>
        static readonly int[] FullMs = { -100, 0, 33, 100, 250, 500, 900 };
        /// <summary>probe2 순간 (FxjProbe와 같다)</summary>
        static readonly int[] ProbeMs = { -60, -17, 0, 17, 33, 50, 83, 117, 150, 200, 300, 450, 600, 900 };

        /// <summary>FX-JUICE 기준선 19개 (이름을 바꾸지 않는다)</summary>
        static readonly string[] ProbeKeys =
        {
            "guardian_jjakjjak_defense_t0", "guardian_ttokdan_assault_t0", "guardian_chalkak_snipe_t0", "guardian_mongdang_defense_t0",
            "guardian_lalabell_support_t0", "echo_jjakjjak_melee", "echo_rouge_ranged", "crit_jjakjjak", "shield_break",
            "death_enemy_dust", "death_enemy_rust", "death_enemy_moth", "death_boss_shadow", "strike_shop_enemy_dust",
            "strike_echo_enemy_rust", "lamp", "skill_vfx_star_burst", "skill_vfx_candle_burst", "skill_vfx_key_beam",
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>재생 뒤 최대값 · 합 (metrics.tsv 한 줄)</summary>
        struct EntryMetrics
        {
            public float Shake;
            public float ZoomShift;
            public float Zoom;
            public float Dim;
            public float Light;
            public float Tint;
            public int Upper;
            public int Ground;
            public int Numbers;
            public int Silhouettes;
            /// <summary>예산 탈락 합 (위쪽 전체 / 그중 영웅 층 / 바닥) · 버린 화면 큐 합</summary>
            public int RejectedUpper;
            public int RejectedHero;
            public int RejectedGround;
            public int CuesDropped;
            public FxQuality Quality;
            /// <summary>예산에 막힌 영웅 층 그림 이름 (진단, 처음 본 순서)</summary>
            public List<string> RejectedArt;

            public void Sample(in FxFrameStats s)
            {
                if (s.LastRejectedHero != null)
                {
                    RejectedArt ??= new List<string>();
                    string name = s.LastRejectedHero.name;
                    if (!RejectedArt.Contains(name)) RejectedArt.Add(name);
                }
                Shake = Mathf.Max(Shake, s.ShakeOffset);
                ZoomShift = Mathf.Max(ZoomShift, s.ZoomShift);
                Zoom = Mathf.Max(Zoom, s.Zoom);
                Dim = Mathf.Max(Dim, s.Dim);
                Light = Mathf.Max(Light, s.Light);
                Tint = Mathf.Max(Tint, s.Tint);
                Upper = Mathf.Max(Upper, s.LiveUpper);
                Ground = Mathf.Max(Ground, s.LiveGround);
                Numbers = Mathf.Max(Numbers, s.VisibleNumbers);
                Silhouettes = Mathf.Max(Silhouettes, s.Silhouettes);
                RejectedUpper += s.RejectedUpper;
                RejectedHero += s.RejectedHero;
                RejectedGround += s.RejectedGround;
                CuesDropped += s.CuesDropped;
            }
        }

        /// <summary>한 항목에서 찍을 순간 하나 (도착 기준 ms, 어떤 그림을 찍는지)</summary>
        struct Moment
        {
            public int Ms;
            public int StripColumn;
            public bool Single;
        }

        sealed class Rig
        {
            public Camera Camera;
            public RenderTexture Texture;
            /// <summary>보이는 전장 띠 (전장 창 전체 — full_view/)</summary>
            public RectInt Band;
            /// <summary>기준 띠 (전장 로컬 y ±312, 1080x624 — probe2 · full/)</summary>
            public RectInt RefBand;
            public BattleFx Fx;
            public FxGalleryView Gallery;
            /// <summary>보기 프레임(단일 · 띠 · full_view)에 대역을 세우는지 (캐릭터 데이터가 있을 때)</summary>
            public bool StandIns;
        }

        [MenuItem("IdleGame/Verify/공격 연출 시험장 캡처", priority = 40)]
        public static void Capture()
        {
            string root = Arg("-verifyOut") ?? Path.GetFullPath("verify_out");
            string outDir = Path.Combine(root, "fx");
            // 에디터에서 메뉴로 부를 때: 저장 안 한 씬이 있으면 먼저 묻는다 (취소하면 아무것도 하지 않는다 — 씬을 다시 열어 버리지 않게)
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            HeroRejections.Clear();
            try
            {
                int count = Run(root, outDir);
                Debug.Log($"[Verify] FxGallery: {count}장 → {outDir}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Verify] FxGallery 캡처 실패: {e}");
            }
            finally
            {
                // 캔버스 모드·허수아비 등 바뀐 것을 버린다 (저장하지 않는다)
                if (File.Exists(IdleSceneBuilder.ScenePath)) EditorSceneManager.OpenScene(IdleSceneBuilder.ScenePath, OpenSceneMode.Single);
            }
            if (HeroRejections.Count == 0) return;
            // 높음 품질에서 공격이 읽히는 층(투사체 머리 · 명중 그림 · 처치 꽃 · 코어 별 …)이 예산에 막혔다 — 그림은 다 찍었지만 배치는 실패로 끝낸다
            Debug.LogError("[Verify] FAIL hero_rejected (high quality): " + string.Join(", ", HeroRejections));
            if (Application.isBatchMode) EditorApplication.Exit(HeroRejectedExitCode);
        }

        static int Run(string root, string outDir)
        {
            EditorSceneManager.OpenScene(IdleSceneBuilder.ScenePath, OpenSceneMode.Single);
            var gallery = Object.FindAnyObjectByType<FxGalleryView>(FindObjectsInactive.Include);
            var fx = Object.FindAnyObjectByType<BattleFx>(FindObjectsInactive.Include);
            if (gallery == null || fx == null) throw new InvalidOperationException("씬에 FxGalleryView / BattleFx가 없습니다 (씬을 다시 만드세요).");

            Directory.CreateDirectory(outDir);
            string stripDir = Path.Combine(outDir, "strips");
            string fullDir = Path.Combine(outDir, "full");
            string viewDir = Path.Combine(outDir, "full_view");
            string probeDir = Path.Combine(root, "probe2");
            Directory.CreateDirectory(stripDir);
            Directory.CreateDirectory(fullDir);
            Directory.CreateDirectory(viewDir);
            Directory.CreateDirectory(probeDir);
            ActivateParents(gallery.transform);
            ActivateParents(fx.transform);
            var canvas = fx.GetComponentInParent<Canvas>(true);
            if (canvas == null) throw new InvalidOperationException("BattleFx가 캔버스 아래에 없습니다.");
            canvas = canvas.rootCanvas;
            var characters = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharactersPath);

            var cameraObject = new GameObject("FxCaptureCamera");
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "FxCapture" };
            var crop = new Texture2D(CropSize, CropSize, TextureFormat.RGBA32, false);
            var strip = new Texture2D(StripCell * StripMs.Length, StripCell, TextureFormat.RGB24, false);
            Texture2D band = null;
            Texture2D view = null;
            var probeLog = new StringBuilder();
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UITheme.Background;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000f;
                camera.targetTexture = texture;
                camera.transform.position = new Vector3(0f, 0f, -500f);

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    // 기준 화면(1080x1920)과 같은 비율 — 다시 켜서 배율을 지금 카메라 크기로 맞춘다
                    scaler.matchWidthOrHeight = 0f;
                    scaler.enabled = false;
                    scaler.enabled = true;
                }
                Settle(camera);
                if (gallery.Mapper != null) gallery.Mapper.Recalculate();

                gallery.ConfigOverride = AssetDatabase.LoadAssetAtPath<BattleConfig>(ConfigPath);
                gallery.CharactersOverride = characters;
                gallery.StandInBound = (target, formId) => ApplyStandInArt(target, characters, formId);
                gallery.StandIns = false;
                gallery.ForceDefaults = true;
                gallery.Open();
                if (!gallery.IsOpen) throw new InvalidOperationException("시험장이 열리지 않습니다 (개발 빌드 전용 / 필드 연결 확인).");

                var full = new RectInt(0, 0, Width, Height);
                var rig = new Rig { Camera = camera, Texture = texture, Fx = fx, Gallery = gallery, Band = full, RefBand = full, StandIns = characters != null };
                var field = gallery.Mapper != null ? gallery.Mapper.transform as RectTransform : null;
                WriteProbeHeader(probeLog, canvas, fx, gallery, camera);
                if (field != null)
                {
                    HideOverlays(canvas, field, camera, probeLog);
                    rig.Band = ScreenBand(field, camera);
                    rig.RefBand = ReferenceBand(rig);
                    probeLog.Append("field band=").Append(rig.Band).Append(" (full_view/ — the whole visible field window)\n");
                    probeLog.Append("ref band=").Append(rig.RefBand).Append(" (probe2/, full/ — field-local y −")
                        .Append(RefBandHalfHeight.ToString("0", Inv)).Append("…+").Append(RefBandHalfHeight.ToString("0", Inv))
                        .Append(", the baseline/tune1 1080x624 field band; shop at band y ")
                        .Append((RefBandHalfHeight - (gallery.Mapper != null ? gallery.Mapper.Origin.y : BaselineOriginY)).ToString("0.0", Inv)).Append(")\n");
                }
                probeLog.Append("stand-ins=").Append(rig.StandIns ? "on (strips, crops, full_view)" : "off (no CharacterDatabase)").Append(", off for probe2 + full\n");
                CheckExtents(rig, probeLog);
                band = new Texture2D(Mathf.Max(1, rig.RefBand.width), Mathf.Max(1, rig.RefBand.height), TextureFormat.RGB24, false);
                view = new Texture2D(Mathf.Max(1, rig.Band.width), Mathf.Max(1, rig.Band.height), TextureFormat.RGB24, false);

                var index = new StringBuilder();
                var metrics = new StringBuilder("key\tarrival\tshake_max_px\tzoom_max\tdim_max\tlight_max\ttint_max\tupper_max\tground_max\tnumbers_max\tsilhouettes_max\tsafe_top"
                                                + "\tzoom_shift_max_px\trejected_upper\trejected_hero\trejected_ground\tcues_dropped\tquality\thero_rejected_art\n");
                var stripIndex = new StringBuilder("key\tms\n");
                string stripTimes = string.Join(",", StripMs);
                int captured = 0;
                int total = gallery.EntryCount;
                for (int i = 0; i < total; i++)
                {
                    string key = gallery.EntryKey(i);
                    if (string.IsNullOrEmpty(key)) continue;
                    float arrival = CaptureEntry(rig, i, key, outDir, stripDir, fullDir, viewDir, crop, strip, band, view, out var m);
                    index.Append(key).Append('\t').Append(gallery.EntryLabel(i)).Append('\t').Append(arrival.ToString("0.000", Inv)).Append('\n');
                    metrics.Append(key).Append('\t').Append(arrival.ToString("0.000", Inv)).Append('\t')
                        .Append(m.Shake.ToString("0.00", Inv)).Append('\t').Append(m.Zoom.ToString("0.0000", Inv)).Append('\t')
                        .Append(m.Dim.ToString("0.000", Inv)).Append('\t').Append(m.Light.ToString("0.000", Inv)).Append('\t')
                        .Append(m.Tint.ToString("0.000", Inv)).Append('\t').Append(m.Upper).Append('\t').Append(m.Ground).Append('\t')
                        .Append(m.Numbers).Append('\t').Append(m.Silhouettes).Append('\t').Append(fx.SafeTop.ToString("0.0", Inv)).Append('\t')
                        .Append(m.ZoomShift.ToString("0.00", Inv)).Append('\t').Append(m.RejectedUpper).Append('\t').Append(m.RejectedHero).Append('\t')
                        .Append(m.RejectedGround).Append('\t').Append(m.CuesDropped).Append('\t').Append(m.Quality).Append('\t')
                        .Append(m.RejectedArt != null ? string.Join("|", m.RejectedArt) : "-").Append('\n');
                    stripIndex.Append(key).Append('\t').Append(stripTimes).Append('\n');
                    if (m.Quality == FxQuality.High && m.RejectedHero > 0) HeroRejections.Add(key + "=" + m.RejectedHero);
                    captured++;
                }
                File.WriteAllText(Path.Combine(outDir, "index.txt"), index.ToString());
                File.WriteAllText(Path.Combine(outDir, "metrics.tsv"), metrics.ToString());
                File.WriteAllText(Path.Combine(stripDir, "index.tsv"), stripIndex.ToString());

                // 기준선 비교 프레임과 할당 확인은 예전처럼 대역 없이 (빈 받침대)
                gallery.StandIns = false;
                CaptureProbe(rig, probeDir, band, probeLog);
                CheckAllocations(rig);
                gallery.Close();
                return captured;
            }
            finally
            {
                File.WriteAllText(Path.Combine(probeDir, "probe.txt"), probeLog.ToString());
                gallery.ForceDefaults = false;
                gallery.ScenarioLengthOverride = 0f;
                gallery.StandIns = false;
                gallery.StandInBound = null;
                gallery.CharactersOverride = null;
                canvas.worldCamera = null;
                Object.DestroyImmediate(crop);
                Object.DestroyImmediate(strip);
                if (band != null) Object.DestroyImmediate(band);
                if (view != null) Object.DestroyImmediate(view);
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        // ───────────── 항목 하나 ─────────────

        /// <summary>
        /// 항목 하나를 찍는다: ① 대역을 세우고 단일 자르기(+50ms) + 띠 12칸 (연출 시계 — 도착 기준) ② 화면 크기면 같은 씨앗으로 다시 재생해
        /// 전장 프레임 두 벌 (화면 연출 순간 기준 실제 시간 — 전체 정지 동안에도 어둡게·실루엣·빛 번쩍임이 보이게): 대역 없이 기준 띠 → full/,
        /// 대역을 세우고 보이는 전장 띠 → full_view/. 도착 시간을 돌려준다
        /// </summary>
        static float CaptureEntry(Rig rig, int i, string key, string outDir, string stripDir, string fullDir, string viewDir, Texture2D crop, Texture2D strip,
            Texture2D band, Texture2D view, out EntryMetrics metrics)
        {
            var gallery = rig.Gallery;
            var fx = rig.Fx;
            metrics = default;
            string safe = SafeName(key);
            gallery.StandIns = rig.StandIns;
            UnityEngine.Random.InitState(1234 + i);
            gallery.Stage(i);
            Roll(fx, gallery, PreRoll, null);

            float arrival = Mathf.Max(0f, gallery.Play(i));
            metrics.Quality = fx.Quality;
            var moments = Moments();
            ClearStrip(strip);
            double fxTime = 0.0;
            float realTime = 0f;
            for (int m = 0; m < moments.Count; m++)
            {
                var moment = moments[m];
                double target = Math.Max(0.0, arrival + moment.Ms / 1000.0);
                RollTo(fx, gallery, target, ref fxTime, ref realTime, ref metrics);
                Render(rig);
                var focus = gallery.FocusPoint;
                if (moment.StripColumn >= 0) ReadCrop(rig, focus, StripCell, strip, moment.StripColumn * StripCell);
                if (moment.Single)
                {
                    ReadCrop(rig, focus, CropSize, crop, 0);
                    crop.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, safe + ".png"), crop.EncodeToPNG());
                }
            }
            strip.Apply();
            File.WriteAllBytes(Path.Combine(stripDir, safe + ".png"), strip.EncodeToPNG());

            if (gallery.EntryIsScreenScale(i))
            {
                // 기준선 비교 (대역 없이 · 기준 띠) → 새 화면 보기 (대역 · 보이는 전장 띠)
                gallery.StandIns = false;
                CaptureFull(rig, i, safe, fullDir, rig.RefBand, band);
                gallery.StandIns = rig.StandIns;
                CaptureFull(rig, i, safe, viewDir, rig.Band, view);
            }
            return arrival;
        }

        /// <summary>
        /// 전장 프레임 (화면 크기 항목): 같은 씨앗으로 다시 세워 clean → 화면 연출 순간 100ms 전(연출 시계) → 순간 그 프레임(p000) →
        /// 그 뒤로는 실제 시간 +33 · +100 · +250 · +500 · +900ms. rect = 읽을 띠 (기준 띠 또는 보이는 전장 띠), band = 그 크기의 텍스처
        /// </summary>
        static void CaptureFull(Rig rig, int i, string safe, string fullDir, RectInt rect, Texture2D band)
        {
            var gallery = rig.Gallery;
            var fx = rig.Fx;
            UnityEngine.Random.InitState(1234 + i);
            gallery.Stage(i);
            Roll(fx, gallery, PreRoll, null);
            Render(rig);
            SaveBand(rig, band, rect, Path.Combine(fullDir, safe + "_clean.jpg"));

            gallery.Play(i);
            float moment = Mathf.Max(0f, gallery.ScreenMoment);
            double fxTime = 0.0;
            float realTime = 0f;
            EntryMetrics ignored = default;
            int before = Mathf.Min(-FullMs[0], Mathf.RoundToInt(moment * 1000f));
            if (before > 0)
            {
                RollTo(fx, gallery, moment - before / 1000.0, ref fxTime, ref realTime, ref ignored);
                Render(rig);
                SaveBand(rig, band, rect, Path.Combine(fullDir, safe + "_" + Tag(-before) + ".jpg"));
            }
            RollTo(fx, gallery, moment, ref fxTime, ref realTime, ref ignored);
            float momentReal = realTime;
            for (int k = 0; k < FullMs.Length; k++)
            {
                int ms = FullMs[k];
                if (ms < 0) continue;
                RollToReal(fx, gallery, momentReal + ms / 1000f, ref fxTime, ref realTime);
                Render(rig);
                SaveBand(rig, band, rect, Path.Combine(fullDir, safe + "_" + Tag(ms) + ".jpg"));
            }
        }

        /// <summary>띠·단일 자르기 순간들 (도착 기준 ms 오름차순, 같은 순간은 한 번 그려 여러 그림을 읽는다)</summary>
        static List<Moment> Moments()
        {
            var moments = new List<Moment>();
            for (int i = 0; i < StripMs.Length; i++)
            {
                int k = Find(moments, StripMs[i]);
                var m = moments[k];
                m.StripColumn = i;
                moments[k] = m;
            }
            int single = Find(moments, SingleMs);
            var s = moments[single];
            s.Single = true;
            moments[single] = s;
            moments.Sort((a, b) => a.Ms.CompareTo(b.Ms));
            return moments;
        }

        /// <summary>그 ms의 순간 번호 (없으면 새로 넣는다)</summary>
        static int Find(List<Moment> moments, int ms)
        {
            for (int i = 0; i < moments.Count; i++)
            {
                if (moments[i].Ms == ms) return i;
            }
            moments.Add(new Moment { Ms = ms, StripColumn = -1 });
            return moments.Count - 1;
        }

        /// <summary>도착 기준 ms → 이름표 (m100 / p000 / p033 …)</summary>
        static string Tag(int ms) => (ms < 0 ? "m" : "p") + Math.Abs(ms).ToString("000", Inv);

        static string SafeName(string key) => key.Replace('@', '-');

        // ───────────── 기준선 probe2 (FxjProbe와 같은 이름·로그) ─────────────

        static void CaptureProbe(Rig rig, string probeDir, Texture2D band, StringBuilder log)
        {
            var gallery = rig.Gallery;
            var fx = rig.Fx;
            int total = gallery.EntryCount;
            foreach (string key in ProbeKeys)
            {
                int index = -1;
                for (int i = 0; i < total; i++)
                {
                    if (gallery.EntryKey(i) != key) continue;
                    index = i;
                    break;
                }
                if (index < 0)
                {
                    log.Append("missing ").Append(key).Append('\n');
                    continue;
                }
                UnityEngine.Random.InitState(1234 + index);
                gallery.Stage(index);
                Roll(fx, gallery, PreRoll, null);
                Render(rig);
                SaveBand(rig, band, rig.RefBand, Path.Combine(probeDir, key + "__clean.jpg"));
                float arrival = Mathf.Max(0f, gallery.Play(index));
                var focus = gallery.FocusPoint;
                var screen = RectTransformUtility.WorldToScreenPoint(rig.Camera, FieldToWorld(fx, focus));
                log.Append(key).Append(" arrival=").Append(arrival.ToString("0.000", Inv))
                    .Append(" focus=(").Append(focus.x.ToString("F2", Inv)).Append(", ").Append(focus.y.ToString("F2", Inv)).Append(')')
                    .Append(" screen=(").Append(screen.x.ToString("F2", Inv)).Append(", ").Append(screen.y.ToString("F2", Inv)).Append(")\n");
                double fxTime = 0.0;
                float realTime = 0f;
                EntryMetrics ignored = default;
                foreach (int ms in ProbeMs)
                {
                    double target = arrival + ms / 1000.0;
                    if (target < 0.0) continue;
                    RollTo(fx, gallery, target, ref fxTime, ref realTime, ref ignored);
                    Render(rig);
                    string tag = Tag(ms);
                    SaveBand(rig, band, rig.RefBand, Path.Combine(probeDir, key + "__" + tag + ".jpg"));
                    log.Append("  ").Append(tag).Append(" fx=").Append(fxTime.ToString("0.000", Inv)).Append(" real=").Append(realTime.ToString("0.000", Inv))
                        .Append(" timeScale=").Append(fx.TimeScale.ToString("0.00", Inv)).Append('\n');
                }
            }
        }

        static void WriteProbeHeader(StringBuilder log, Canvas canvas, BattleFx fx, FxGalleryView gallery, Camera camera)
        {
            log.Append("canvas size=").Append(((RectTransform)canvas.transform).rect.size).Append(" scaleFactor=").Append(canvas.scaleFactor).Append('\n');
            var layer = fx.ParticleLayer;
            if (layer != null)
            {
                var corners = new Vector3[4];
                layer.GetWorldCorners(corners);
                log.Append("fxLayer rect=").Append(layer.rect).Append(" screenBL=").Append(RectTransformUtility.WorldToScreenPoint(camera, corners[0]))
                    .Append(" screenTR=").Append(RectTransformUtility.WorldToScreenPoint(camera, corners[2])).Append('\n');
            }
            var mapper = gallery.Mapper;
            if (mapper != null)
                log.Append("mapper ppu=").Append(mapper.Ppu).Append(" squash=").Append(mapper.Squash).Append(" actorScale=").Append(mapper.ActorScale)
                    .Append(" origin=").Append(mapper.Origin).Append('\n');
            log.Append("premul=").Append(fx.PremulOn).Append(" safeTop=").Append(fx.SafeTop.ToString("0.0", Inv)).Append(" juice=").Append(fx.Juice != null).Append('\n');
        }

        // ───────────── 할당 확인 (§3.5) ─────────────

        /// <summary>
        /// 할당 확인 (§3.5) — 매 프레임 쓰레기만 잰다 (처음 보는 숫자 문자열 캐시 채우기·시험장 자신의 허수아비 세우기는 뺀다):
        ///   ① busy_20 60프레임 데운 뒤 120프레임, BattleFx.Advance + 시험장 Advance(공격·스킬·치명타·숫자 연출 호출이 모두 그 안) → fx_alloc_bytes
        ///   ② 같은 장면을 후반 큰 숫자(×12,345)로 한 번 돌려 단위 표기 캐시를 채운 뒤 같은 씨앗으로 다시 → fx_alloc_bytes_late
        ///   ③ mass_kill_20 (범위 스킬 + 치명타로 20 처치 · 퇴장 · 다시 세우기)을 한 번 돌린 뒤 같은 씨앗으로 다시 → fx_alloc_bytes_masskill
        ///   ④ 성능 대조: busy_20 120프레임 평균 ms (fx.Advance · Canvas.ForceUpdateCanvases · 카메라 그리기) 지금 방식 vs 예전 방식 → fx_perf
        /// </summary>
        static void CheckAllocations(Rig rig)
        {
            var gallery = rig.Gallery;
            int busy = EntryIndex(gallery, AllocKey);
            if (busy < 0)
            {
                Debug.LogWarning("[Verify] fx_alloc_bytes=? (" + AllocKey + " 항목이 없습니다)");
                return;
            }
            // 숫자 문자열 캐시(BattleEase — 처음 보는 값만 한 번 만든다)를 미리 채운다: 재는 것은 매 프레임 쓰레기이지 캐시 채우기가 아니다
            for (int v = 0; v < 10000; v++)
            {
                BattleEase.Int(v);
                BattleEase.Crit(v);
            }
            try
            {
                gallery.ScenarioLengthOverride = 4f;
                var probe = Measure(rig, busy, 60, 120, false);
                Debug.Log("[Verify] fx_alloc_bytes=" + probe.Bytes + " (busy_20, 120 frames, fx.Advance + gallery.Advance, live upper " + rig.Fx.Stats.LiveUpper
                          + ", excluded raise frames " + probe.Excluded + ")");

                gallery.DamageScale = LateDamageScale;
                Measure(rig, busy, 60, 120, false);
                probe = Measure(rig, busy, 60, 120, false);
                gallery.DamageScale = 1f;
                Debug.Log("[Verify] fx_alloc_bytes_late=" + probe.Bytes + " (busy_20 × " + LateDamageScale.ToString("0.###", Inv)
                          + " damage, second pass, 120 frames)");

                gallery.ScenarioLengthOverride = 0f;
                int mass = EntryIndex(gallery, MassAllocKey);
                if (mass >= 0)
                {
                    Measure(rig, mass, 0, 150, false);
                    probe = Measure(rig, mass, 0, 150, false);
                    Debug.Log("[Verify] fx_alloc_bytes_masskill=" + probe.Bytes + " (mass_kill_20 second pass, 150 frames from Play, excluded raise frames "
                              + probe.Excluded + ")");
                }
                else Debug.LogWarning("[Verify] fx_alloc_bytes_masskill=? (" + MassAllocKey + " 항목이 없습니다)");

                gallery.ScenarioLengthOverride = 4f;
                var now = Measure(rig, busy, 60, 120, true);
                gallery.LegacyParticleWrites = true;
                var legacy = Measure(rig, busy, 60, 120, true);
                gallery.LegacyParticleWrites = false;
                Debug.Log("[Verify] fx_perf busy_20 (120 frames, ms/frame) now: " + now.Describe() + " | legacy per-frame sizeDelta+color: " + legacy.Describe());
            }
            finally
            {
                gallery.LegacyParticleWrites = false;
                gallery.DamageScale = 1f;
                gallery.ScenarioLengthOverride = 0f;
            }
        }

        static int EntryIndex(FxGalleryView gallery, string key)
        {
            for (int i = 0; i < gallery.EntryCount; i++)
            {
                if (gallery.EntryKey(i) == key) return i;
            }
            return -1;
        }

        /// <summary>한 번 재기의 결과 (할당 바이트 · 뺀 프레임 · 평균 ms)</summary>
        struct Probe
        {
            public long Bytes;
            public int Excluded;
            public int Frames;
            public long AdvanceTicks;
            public long RebuildTicks;
            public long RenderTicks;
            public int MaxUpper;

            static double Ms(long ticks, int frames) => frames > 0 ? ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames : 0.0;

            public string Describe() =>
                "advance=" + Ms(AdvanceTicks, Frames).ToString("0.000", Inv) + " rebuild=" + Ms(RebuildTicks, Frames).ToString("0.000", Inv)
                + " render=" + Ms(RenderTicks, Frames).ToString("0.000", Inv) + " upper_max=" + MaxUpper;
        }

        /// <summary>
        /// 항목 하나를 같은 씨앗으로 세워 재생하고 skip프레임 뒤 frames프레임 동안 잰다: 할당 = fx.Advance + 시험장 Advance
        /// (시험장이 그 프레임에 허수아비를 다시 세웠으면 시험장 몫은 뺀다). timing이면 그 프레임마다 그림 다시 만들기·카메라 그리기 시간도 잰다
        /// </summary>
        static Probe Measure(Rig rig, int index, int skip, int frames, bool timing)
        {
            var gallery = rig.Gallery;
            var fx = rig.Fx;
            UnityEngine.Random.InitState(1234 + index);
            gallery.Stage(index);
            Roll(fx, gallery, PreRoll, null);
            gallery.Play(index);
            for (int i = 0; i < skip; i++)
            {
                fx.Advance(Step);
                gallery.Advance(Step * fx.TimeScale);
            }
            var probe = new Probe { Frames = frames };
            for (int i = 0; i < frames; i++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                long before = GC.GetAllocatedBytesForCurrentThread();
                fx.Advance(Step);
                long mid = GC.GetAllocatedBytesForCurrentThread();
                probe.AdvanceTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                int raised = gallery.RaiseCount;
                gallery.Advance(Step * fx.TimeScale);
                long after = GC.GetAllocatedBytesForCurrentThread();
                probe.Bytes += mid - before;
                if (gallery.RaiseCount == raised) probe.Bytes += after - mid;
                else probe.Excluded++;
                probe.MaxUpper = Mathf.Max(probe.MaxUpper, fx.Stats.LiveUpper);
                if (!timing) continue;
                long r0 = System.Diagnostics.Stopwatch.GetTimestamp();
                Canvas.ForceUpdateCanvases();
                long r1 = System.Diagnostics.Stopwatch.GetTimestamp();
                rig.Camera.Render();
                probe.RebuildTicks += r1 - r0;
                probe.RenderTicks += System.Diagnostics.Stopwatch.GetTimestamp() - r1;
            }
            return probe;
        }

        // ───────────── 진행 / 그리기 / 읽기 ─────────────

        /// <summary>연출 시간으로 duration(초)만큼 1/60초씩 진행한다 (정지·느린 화면이면 실제 시간이 더 걸린다 — MaxRealTime까지)</summary>
        static void Roll(BattleFx fx, FxGalleryView gallery, float duration, Action<FxFrameStats> sample)
        {
            float fxTime = 0f;
            float realTime = 0f;
            while (fxTime < duration - Step * 0.5f && realTime < MaxRealTime)
            {
                fx.Advance(Step);
                float dt = Step * fx.TimeScale;
                gallery.Advance(dt);
                fxTime += dt;
                realTime += Step;
                sample?.Invoke(fx.Stats);
            }
        }

        /// <summary>
        /// 재생 뒤 연출 시계가 target(초)에 닿는 첫 프레임까지 진행하며 최대값을 담는다 (그 순간에 터지는 연출이 찍힌 프레임 — 반 프레임 앞에서 멈추지 않는다)
        /// </summary>
        static void RollTo(BattleFx fx, FxGalleryView gallery, double target, ref double fxTime, ref float realTime, ref EntryMetrics metrics)
        {
            while (fxTime < target - 1e-4 && realTime < MaxRealTime)
            {
                fx.Advance(Step);
                float dt = Step * fx.TimeScale;
                gallery.Advance(dt);
                fxTime += dt;
                realTime += Step;
                metrics.Sample(fx.Stats);
            }
        }

        /// <summary>실제 시간이 target(초, 재생 뒤)에 닿는 첫 프레임까지 진행한다 (전체 정지 동안에도 화면 효과는 실제 시간으로 흐른다)</summary>
        static void RollToReal(BattleFx fx, FxGalleryView gallery, float target, ref double fxTime, ref float realTime)
        {
            while (realTime < target - 1e-4f && realTime < MaxRealTime)
            {
                fx.Advance(Step);
                float dt = Step * fx.TimeScale;
                gallery.Advance(dt);
                fxTime += dt;
                realTime += Step;
            }
        }

        static void Render(Rig rig)
        {
            Canvas.ForceUpdateCanvases();
            rig.Camera.Render();
        }

        /// <summary>볼 자리(전장 로컬) 둘레 size x size를 dest의 (destX, 0)에 읽는다 (화면 밖이면 안쪽으로 당긴다)</summary>
        static void ReadCrop(Rig rig, Vector2 focus, int size, Texture2D dest, int destX)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(rig.Camera, FieldToWorld(rig.Fx, focus));
            int x = Mathf.Clamp(Mathf.RoundToInt(screen.x) - size / 2, 0, Width - size);
            int y = Mathf.Clamp(Mathf.RoundToInt(screen.y) - size / 2, 0, Height - size);
            var previous = RenderTexture.active;
            RenderTexture.active = rig.Texture;
            dest.ReadPixels(new Rect(x, y, size, size), destX, 0);
            RenderTexture.active = previous;
        }

        /// <summary>렌더 텍스처의 rect 띠(픽셀, 아래 원점)를 band(같은 크기)로 읽어 JPG로 쓴다</summary>
        static void SaveBand(Rig rig, Texture2D band, RectInt rect, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rig.Texture;
            band.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
            band.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, band.EncodeToJPG(JpgQuality));
        }

        static void ClearStrip(Texture2D strip)
        {
            var clear = new Color32[strip.width * strip.height];
            var fill = new Color32(10, 8, 20, 255);
            for (int i = 0; i < clear.Length; i++) clear[i] = fill;
            strip.SetPixels32(clear);
        }

        /// <summary>캔버스 크기·배치를 한 번 굳힌다 (재생 루프가 없으므로 직접 갱신하고 한 번 그려 본다)</summary>
        static void Settle(Camera camera)
        {
            for (int i = 0; i < 2; i++)
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
            }
        }

        /// <summary>전장 로컬 좌표(입자 레이어 가운데 기준 anchoredPosition) → 월드 좌표</summary>
        static Vector3 FieldToWorld(BattleFx fx, Vector2 field)
        {
            var layer = fx.ParticleLayer != null ? fx.ParticleLayer : fx.transform as RectTransform;
            return layer != null ? layer.TransformPoint(layer.rect.center + field) : (Vector3)field;
        }

        /// <summary>
        /// 기준 띠 (렌더 텍스처 픽셀, 아래 원점): 전장 로컬 y −312 ~ +312 × 전장 폭 전체 = 기준선·tune1의 1080x624 전장 띠와 같은 자리
        /// (새 화면 틀에서도 전장 로컬 좌표·수선점 Origin이 그대로라 광장 링·수선점·허수아비가 띠의 같은 픽셀에 온다 — MAIN 화면 y 550~1174).
        /// 수선점 자리가 기준선(Origin.y −63.5)과 다르면 넓이 %를 그대로 비교할 수 없어 경고한다
        /// </summary>
        static RectInt ReferenceBand(Rig rig)
        {
            var fx = rig.Fx;
            float half = fx.FieldHalfWidth;
            var bl = RectTransformUtility.WorldToScreenPoint(rig.Camera, FieldToWorld(fx, new Vector2(-half, -RefBandHalfHeight)));
            var tr = RectTransformUtility.WorldToScreenPoint(rig.Camera, FieldToWorld(fx, new Vector2(half, RefBandHalfHeight)));
            int x0 = Mathf.Clamp(Mathf.RoundToInt(bl.x), 0, Width);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(bl.y), 0, Height);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(tr.x), 0, Width);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(tr.y), 0, Height);
            if (x1 - x0 < 8 || y1 - y0 < 8) return rig.Band;
            var mapper = rig.Gallery.Mapper;
            if (mapper != null && Mathf.Abs(mapper.Origin.y - BaselineOriginY) > 1f)
                Debug.LogWarning("[Verify] WARN fx_ref_band: shop field y " + mapper.Origin.y.ToString("0.0", Inv) + " ≠ baseline " + BaselineOriginY.ToString("0.0", Inv)
                                 + " — area % in probe2/full are offset from baseline/tune1");
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>
        /// 전장 크기 확인 (UX-SPEC-IA §6.5 B8 · IA-11, 캡처는 MAIN — 시트 닫힘): BattleFx.FieldTop = mapper.VisibleTop = 입자 레이어 h/2,
        /// FieldBottom = mapper.VisibleBottom = −h/2 (MAIN에서는 예전 값 그대로), SafeTop이 화면 y ≥ 250 → "[Verify] PASS|FAIL fx_extents …"
        /// </summary>
        static void CheckExtents(Rig rig, StringBuilder log)
        {
            var fx = rig.Fx;
            var mapper = rig.Gallery.Mapper;
            var layer = fx.ParticleLayer;
            if (mapper == null || layer == null)
            {
                Debug.LogWarning("[Verify] fx_extents=? (mapper / particle layer 없음)");
                return;
            }
            float half = layer.rect.height * 0.5f;
            float top = fx.FieldTop;
            float bottom = fx.FieldBottom;
            var safe = RectTransformUtility.WorldToScreenPoint(rig.Camera, FieldToWorld(fx, new Vector2(0f, fx.SafeTop)));
            float safeY = Height - safe.y;
            bool ok = Mathf.Abs(top - mapper.VisibleTop) <= 1f && Mathf.Abs(bottom - mapper.VisibleBottom) <= 1f && Mathf.Abs(top - half) <= 1f &&
                      Mathf.Abs(bottom + half) <= 1f && safeY >= SafeTopMinScreenY;
            string line = "fx_extents field_top=" + top.ToString("0.0", Inv) + " visible_top=" + mapper.VisibleTop.ToString("0.0", Inv) + " field_bottom="
                          + bottom.ToString("0.0", Inv) + " visible_bottom=" + mapper.VisibleBottom.ToString("0.0", Inv) + " layer_half=" + half.ToString("0.0", Inv)
                          + " pan=" + mapper.Pan.ToString("0.0", Inv) + " safe_top=" + fx.SafeTop.ToString("0.0", Inv) + " safe_top_screen_y=" + safeY.ToString("0.0", Inv)
                          + " (IA-11 ≥ 250)";
            log.Append(line).Append('\n');
            if (ok) Debug.Log("[Verify] PASS " + line);
            else Debug.LogError("[Verify] FAIL " + line);
        }

        // ───────────── 대역 그림 (Game 없이 — 데이터 에셋의 초상화를 비공개 필드에 직접) ─────────────

        const System.Reflection.BindingFlags PrivateInstance = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        static readonly HashSet<string> MissingFields = new();

        /// <summary>
        /// 대역을 묶은 칸의 그림 (FxGalleryView.StandInBound): Game 없이는 BattleUnitArt가 초상화를 못 찾으므로 형태 그림을 직접 넣는다.
        /// 지킴이 칸 — 초상화를 켜고 대체 토큰을 끄며, 처음에 숨긴(HideOverlays) 후광·배지를 다시 켠다. formId가 null이면 (대역을 푼 칸)
        /// 만든 그대로의 빈 받침대로 되돌린다 (기준선 프레임과 같게: 채운 받침대 그림 + "+", 몸 칸의 흰 상자는 숨김).
        /// 잔향이 — 초상화와 맞았을 때의 흰 번쩍임 덧그림(_flash · _flashArt)을 채운다. 비공개 필드 이름이 바뀌면 그 그림은 건너뛴다 (경고 한 번)
        /// </summary>
        static void ApplyStandInArt(Component target, CharacterDatabase characters, string formId)
        {
            if (target is GuardianSlotView slot)
            {
                var portrait = PrivateField<Image>(slot, "_portrait");
                var fallback = PrivateField<Image>(slot, "_fallback");
                var halo = PrivateField<Image>(slot, "_halo");
                var badge = PrivateField<Image>(slot, "_badge");
                if (formId == null)
                {
                    var body = PrivateField<RectTransform>(slot, "_body");
                    if (body != null) body.gameObject.SetActive(true);
                    if (portrait != null) portrait.enabled = false;
                    if (fallback != null) fallback.enabled = false;
                    if (halo != null) halo.enabled = false;
                    if (badge != null) badge.enabled = false;
                    var pad = PrivateField<Image>(slot, "_padImage");
                    var filled = PrivateField<Sprite>(slot, "_padFilledSprite");
                    if (pad != null && filled != null) pad.sprite = filled;
                    return;
                }
                var sprite = PortraitOf(characters, formId);
                if (portrait != null)
                {
                    portrait.sprite = sprite;
                    portrait.enabled = sprite != null;
                }
                // 그림이 있으면 대체 토큰(계보 색 원 + "?")은 끈다
                if (fallback != null) fallback.gameObject.SetActive(sprite == null);
                if (halo != null) halo.enabled = true;
                if (badge != null) badge.enabled = true;
                return;
            }
            if (target is EchoView echo && formId != null)
            {
                var sprite = PortraitOf(characters, formId);
                if (sprite == null) return;
                var portrait = PrivateField<Image>(echo, "_portrait");
                var fallback = PrivateField<Image>(echo, "_fallback");
                var flash = PrivateField<Image>(echo, "_flash");
                if (portrait != null)
                {
                    portrait.sprite = sprite;
                    portrait.enabled = true;
                }
                if (fallback != null) fallback.gameObject.SetActive(false);
                if (flash == null) return;
                flash.sprite = sprite;
                var material = flash.material;
                SetPrivateField(echo, "_flashArt", material != null && material != Graphic.defaultGraphicMaterial);
            }
        }

        static Sprite PortraitOf(CharacterDatabase characters, string formId)
        {
            var form = characters != null ? characters.GetForm(formId) : null;
            return form != null ? form.portrait : null;
        }

        static T PrivateField<T>(object target, string name) where T : class
        {
            var info = target.GetType().GetField(name, PrivateInstance);
            if (info != null) return info.GetValue(target) as T;
            WarnMissing(target, name);
            return null;
        }

        static void SetPrivateField(object target, string name, object value)
        {
            var info = target.GetType().GetField(name, PrivateInstance);
            if (info != null) info.SetValue(target, value);
            else WarnMissing(target, name);
        }

        static void WarnMissing(object target, string name)
        {
            string key = target.GetType().Name + "." + name;
            if (MissingFields.Add(key)) Debug.LogWarning("[Verify] WARN fx_standin: " + key + " 필드가 없습니다 (대역 그림 일부를 건너뜀)");
        }

        /// <summary>RectTransform의 화면 픽셀 띠 (렌더 텍스처 안으로 자른다)</summary>
        static RectInt ScreenBand(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bl = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var tr = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(bl.x), 0, Width);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(bl.y), 0, Height);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(tr.x), 0, Width);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(tr.y), 0, Height);
            if (x1 - x0 < 8 || y1 - y0 < 8) return new RectInt(0, 0, Width, Height);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>
        /// 에디터 씬에는 만들어 둔 덮개(클리어 리본 · 첫 실행 팝업 · HUD 판 · 빈 지킴이 초상화의 흰 상자)가 다 보인다.
        /// 전장보다 뒤에 그려지면서 전장과 겹치는 그림과 빈 지킴이 초상화·배지를 숨겨 실제 전투처럼 찍는다 (fxj/FxjProbe와 같은 규칙)
        /// </summary>
        static void HideOverlays(Canvas root, RectTransform field, Camera camera, StringBuilder log)
        {
            var order = new Dictionary<Transform, int>();
            int counter = 0;
            Walk(root.transform, order, ref counter);
            if (!order.TryGetValue(field, out int fieldStart)) return;
            int fieldEnd = fieldStart;
            foreach (var pair in order)
            {
                if (pair.Key.IsChildOf(field) && pair.Value > fieldEnd) fieldEnd = pair.Value;
            }
            var fieldRect = ScreenRect(field, camera);
            int hidden = 0;
            int kept = 0;
            var keptNames = new StringBuilder();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy) continue;
                if (graphic.transform.IsChildOf(field)) continue;
                if (!order.TryGetValue(graphic.transform, out int index)) continue;
                if (!ScreenRect(graphic.rectTransform, camera).Overlaps(fieldRect)) continue;
                if (index <= fieldEnd)
                {
                    // 전장보다 먼저 그려지는 바닥 (밤거리 그림 · 가장자리 그늘) — 남긴다 (진단용 이름)
                    if (kept++ < 6) keptNames.Append(kept > 1 ? ", " : string.Empty).Append(graphic.name);
                    continue;
                }
                graphic.enabled = false;
                hidden++;
            }
            log.Append("kept ").Append(kept).Append(" graphics drawn under the field (").Append(keptNames).Append(kept > 6 ? ", …" : string.Empty).Append(")\n");
            int slots = 0;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (var slot in field.GetComponentsInChildren<GuardianSlotView>(true))
            {
                foreach (var name in new[] { "_portrait", "_fallback", "_badge", "_halo" })
                {
                    var info = typeof(GuardianSlotView).GetField(name, flags);
                    if (info == null) continue;
                    if (info.GetValue(slot) is Graphic image && image != null && image.enabled)
                    {
                        image.enabled = false;
                        slots++;
                    }
                }
            }
            log.Append("hid ").Append(hidden).Append(" overlay graphics, ").Append(slots).Append(" guardian slot graphics").Append('\n');
        }

        static void Walk(Transform t, Dictionary<Transform, int> order, ref int counter)
        {
            order[t] = counter++;
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), order, ref counter);
        }

        static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);
            var a = RectTransformUtility.WorldToScreenPoint(camera, c[0]);
            var b = RectTransformUtility.WorldToScreenPoint(camera, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static void ActivateParents(Transform transform)
        {
            for (var t = transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
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
    }
}
