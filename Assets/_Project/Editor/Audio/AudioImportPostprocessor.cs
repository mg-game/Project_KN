using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// Assets/_Project/Audio/** 임포트 규칙 (AUDIO-SPEC §4.7). 규칙은 파일 이름의 키 → 카탈로그(버스 · 묶음 · 길이)로 정하고, 모르는 키만 폴더로 정한다.
    ///   SfxHot   전투 짧은 소리(≤ 0.6초, 아군 · 적 · 상태 · 흐름 · 수선점 한 방) + ui_tap · ui_coin_land · ui_count_tick : CompressedInMemory · ADPCM · 모노 · 미리 올림
    ///   Sfx      그 밖 Ui · Sfx · Loop 키                                          : CompressedInMemory · ADPCM · 모노 · 미리 올림
    ///   (SfxHot은 버전 2부터 Sfx와 같다: ADPCM은 재생 중 풀기가 아주 싸서 시작 지연이 DecompressOnLoad와 같고, 풀어 올리면 PCM으로 3.5배 메모리
    ///    — 전투 짧은 소리 약 315개 · 100초 ≈ 8.4 MiB → 2.4 MiB — 에 부팅 때 라이브러리를 읽으며 동기 디코딩까지 든다. 기기 시험용 되돌림: HotSfxDecompressOnLoad)
    ///   Jingle   Stinger 버스                                                      : CompressedInMemory · Vorbis 0.70 · 스테레오 · 미리 올림
    ///   Ambience 환경음 반복 (AmbienceStreaming이면 Streaming)                       : CompressedInMemory · Vorbis 0.50 · 스테레오 · 미리 올리지 않음 · 백그라운드 · 44.1 kHz
    ///   Emitter  amb_emit_*                                                        : CompressedInMemory · ADPCM · 모노 · 미리 올리지 않음 · 백그라운드
    ///   Voice    vox_*                                                             : CompressedInMemory · Vorbis 0.70 · 모노 · 미리 올리지 않음 · 백그라운드
    ///   Music    Bgm 버스 (도입부 포함)                                             : Streaming · Vorbis 0.60 · 스테레오 · 백그라운드 · 44.1 kHz
    /// 정규화는 늘 끈다 (모노로 접을 때 Unity가 최대값 정규화로 음량 단계를 무너뜨린다). 앰비소닉 끔. 플랫폼별 덮어쓰기 없음.
    /// 이 폴더에서 무엇이 들어오거나 지워지면 1초 뒤(컴파일 · 임포트가 끝난 뒤) 라이브러리를 다시 만들고, 재생 중이면 바로 다시 읽는다.
    /// </summary>
    public sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        const string Root = AudioDataBuilder.AudioRoot + "/";
        /// <summary>기기에서 메모리가 모자라면 true로 (환경음을 스트리밍으로)</summary>
        public const bool AmbienceStreaming = false;
        /// <summary>기기 시험에서 재생 중 ADPCM 풀기 부담이 보이면 true로 (전투 짧은 소리 SfxHot만 예전처럼 DecompressOnLoad) — 바꾼 뒤 GetVersion을 올린다</summary>
        public const bool HotSfxDecompressOnLoad = false;
        const double RebuildDelay = 1.0;

        public enum Preset
        {
            SfxHot,
            Sfx,
            Jingle,
            Ambience,
            Emitter,
            Voice,
            Music,
        }

        /// <summary>규칙이 바뀌면 올린다 (그 폴더의 소리가 다시 임포트된다). 2 = SfxHot을 CompressedInMemory로</summary>
        public override uint GetVersion() => 2;

        static bool InRoot(string path) => !string.IsNullOrEmpty(path) && path.Replace('\\', '/').StartsWith(Root, StringComparison.OrdinalIgnoreCase);

        /// <summary>파일 경로의 규칙 (키가 카탈로그에 있으면 그 키로, 아니면 폴더로)</summary>
        public static Preset PresetFor(string assetPath, out bool knownKey)
        {
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            knownKey = false;
            if (AudioCatalog.ParseFileName(stem, out string key, out _))
            {
                var id = AudioCatalog.Find(key);
                if (id.IsValid)
                {
                    knownKey = true;
                    return PresetFor(AudioCatalog.Keys[id.Index]);
                }
            }
            string folder = (Path.GetDirectoryName(assetPath) ?? string.Empty).Replace('\\', '/');
            if (folder.Contains("/Bgm")) return folder.Contains("Jingle") ? Preset.Jingle : Preset.Music;
            if (folder.Contains("/Amb")) return stem.StartsWith("amb_emit_", StringComparison.Ordinal) ? Preset.Emitter : Preset.Ambience;
            if (folder.Contains("/Vox")) return Preset.Voice;
            if (folder.Contains("/Stinger") && stem.StartsWith("jingle_", StringComparison.Ordinal)) return Preset.Jingle;
            return Preset.Sfx;
        }

        public static Preset PresetFor(in AudioKeyDef def)
        {
            if (def.Has(AudioKeyFlags.Vox)) return Preset.Voice;
            switch (def.Bus)
            {
                case AudioBus.Bgm: return Preset.Music;
                case AudioBus.Stinger: return Preset.Jingle;
                case AudioBus.Amb: return def.Has(AudioKeyFlags.Emitter) ? Preset.Emitter : Preset.Ambience;
            }
            bool battleGroup = def.Group == AudioGroup.Ally || def.Group == AudioGroup.Enemy || def.Group == AudioGroup.Status ||
                def.Group == AudioGroup.Flow || def.Group == AudioGroup.Defense;
            bool hot = (battleGroup && def.Bus == AudioBus.Sfx && def.Length <= 0.6f && !def.Has(AudioKeyFlags.Loop)) ||
                def.Key == "ui_tap" || def.Key == "ui_coin_land" || def.Key == "ui_count_tick";
            return hot ? Preset.SfxHot : Preset.Sfx;
        }

        void OnPreprocessAudio()
        {
            if (!InRoot(assetPath)) return;
            var importer = assetImporter as AudioImporter;
            if (importer == null) return;
            var preset = PresetFor(assetPath, out bool known);
            if (!known && AudioCatalog.ParseFileName(Path.GetFileNameWithoutExtension(assetPath), out string key, out _))
                Debug.LogWarning($"[Audio] 카탈로그에 없는 키예요 (폴더 규칙 {preset}): {assetPath} — 키 '{key}'");

            var settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            switch (preset)
            {
                case Preset.SfxHot:
                case Preset.Sfx:
                    // 짧은 모노 소리: 압축한 채 메모리에 (ADPCM 풀기는 재생 중에도 아주 싸다 — 시작 지연 · PlayScheduled 정밀도는 그대로)
                    settings.loadType = preset == Preset.SfxHot && HotSfxDecompressOnLoad
                        ? AudioClipLoadType.DecompressOnLoad
                        : AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.ADPCM;
                    settings.preloadAudioData = true;
                    importer.forceToMono = true;
                    importer.loadInBackground = false;
                    break;
                case Preset.Jingle:
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.7f;
                    settings.preloadAudioData = true;
                    importer.forceToMono = false;
                    importer.loadInBackground = false;
                    break;
                case Preset.Ambience:
                    settings.loadType = AmbienceStreaming ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.5f;
                    settings.preloadAudioData = false;
                    settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    settings.sampleRateOverride = 44100;
                    importer.forceToMono = false;
                    importer.loadInBackground = true;
                    break;
                case Preset.Emitter:
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.ADPCM;
                    settings.preloadAudioData = false;
                    importer.forceToMono = true;
                    importer.loadInBackground = true;
                    break;
                case Preset.Voice:
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.7f;
                    settings.preloadAudioData = false;
                    importer.forceToMono = true;
                    importer.loadInBackground = true;
                    break;
                default:
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.6f;
                    settings.preloadAudioData = false;
                    settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    settings.sampleRateOverride = 44100;
                    importer.forceToMono = false;
                    importer.loadInBackground = true;
                    break;
            }
            importer.defaultSampleSettings = settings;
            importer.ambisonic = false;
            DisableNormalize(importer);
        }

        /// <summary>정규화 끔 (공개 API가 없어 직렬화 속성으로 — 이름이 바뀌어도 조용히 넘어간다)</summary>
        static void DisableNormalize(AudioImporter importer)
        {
            var so = new SerializedObject(importer);
            var property = so.FindProperty("m_Normalize") ?? so.FindProperty("normalize");
            if (property == null || property.propertyType != SerializedPropertyType.Boolean || !property.boolValue) return;
            property.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void OnPostprocessAudio(AudioClip clip)
        {
            if (!InRoot(assetPath) || clip == null) return;
            if (!AudioCatalog.ParseFileName(Path.GetFileNameWithoutExtension(assetPath), out string key, out _))
            {
                Debug.LogWarning("[Audio] 파일 이름을 읽을 수 없어요 ({키}_{NN}.wav): " + assetPath);
                return;
            }
            var id = AudioCatalog.Find(key);
            if (!id.IsValid) return;
            var def = AudioCatalog.Keys[id.Index];
            if (def.Length > 0f && clip.length > def.Length * 2f + 0.05f)
                Debug.LogWarning($"[Audio] 목표 길이의 2배가 넘어요 (다듬지 않은 파일?): {assetPath} {clip.length:0.00}s / 목표 {def.Length:0.00}s");
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom)) ScheduleRebuild();
        }

        static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            for (int i = 0; i < paths.Length; i++)
            {
                if (!InRoot(paths[i])) continue;
                string extension = Path.GetExtension(paths[i]);
                if (string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase)) continue;
                return true;
            }
            return false;
        }

        // ───────────── 1초 묶음 다시 만들기 ─────────────

        static double _rebuildAt;
        static bool _scheduled;

        /// <summary>
        /// 에디터를 열거나 스크립트를 다시 불러온 뒤 (재생 모드 진입 포함): 소리 파일은 있는데 라이브러리가 없으면 다시 만든다 —
        /// 처음 설치 때 1초 예약이 도메인 다시 불러오기(재생 버튼)로 사라져도 게임이 조용하게 남지 않게. 배치 모드는 BuildBatch가 직접 한다
        /// </summary>
        [InitializeOnLoadMethod]
        static void BuildIfMissing()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(AudioDataBuilder.LibraryPath) || !AssetDatabase.IsValidFolder(AudioDataBuilder.AudioRoot)) return;
                if (AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDataBuilder.AudioRoot }).Length == 0) return;
                ScheduleRebuild();
            };
        }

        static void ScheduleRebuild()
        {
            _rebuildAt = EditorApplication.timeSinceStartup + RebuildDelay;
            if (_scheduled) return;
            _scheduled = true;
            EditorApplication.update += TickRebuild;
        }

        static void TickRebuild()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _rebuildAt) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _rebuildAt = now + 0.5;
                return;
            }
            EditorApplication.update -= TickRebuild;
            _scheduled = false;
            try
            {
                AudioDataBuilder.Build();
                AssetDatabase.SaveAssets();
                var report = AudioDataBuilder.LastReport;
                if (report != null && report.HasWarnings) Debug.LogWarning(report.ToText());
                if (EditorApplication.isPlaying) AudioService.ReloadLibrary();
                AudioAuditionWindow.NotifyRebuilt();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [MenuItem("IdleGame/Audio/오디오 임포트 규칙 다시 적용", priority = 2)]
        public static void Reimport()
        {
            if (!AssetDatabase.IsValidFolder(AudioDataBuilder.AudioRoot)) return;
            AssetDatabase.ImportAsset(AudioDataBuilder.AudioRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
    }
}
