using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 소리 목록 빌더 (AUDIO-SPEC §4.6): Assets/_Project/Audio/**/{키}_{NN}.wav → Resources/Audio/AudioLibrary.asset (파일 · GUID 유지, 내용만 덮어씀).
    ///   1) 폴더 안 AudioClip을 모두 찾아 파일 이름으로 키 · 테이크를 나눈다 (폴더는 사람용 — 키는 파일 이름에서만)
    ///   2) AudioSelection(귀로 고른 것)과 audio_qc.json(측정값)을 읽는다
    ///   3) 카탈로그 순서대로 키마다 칸: 실제 테이크(고름 → QC 추천 → QC 통과 → 전부), QC 근음 · 보정, 고른 손질, 음악 정보.
    ///      별칭은 대상의 테이크를 빌려 오고 손질만 자기 것.
    ///   4) 보고: 모르는 키 파일 · 빠진 P0 키 (조용한 것도 정상) · 테이크가 모자란 키 · QC로 빠진 테이크
    ///      · THIRD_PARTY_AUDIO.md 대조 (QC 출처에 적힌 바깥 파일마다 그 테이크 줄이 있어야 함 — 없으면 경고, §7.2 · §8.4 #6)
    /// 저장(SaveAssets)은 부른 쪽이 한다 (IdleDataBuilder.BuildContent, 메뉴, 자동 다시 만들기).
    /// </summary>
    public static class AudioDataBuilder
    {
        public const string AudioRoot = IdleProjectSetup.Root + "/Audio";
        public const string LibraryPath = IdleProjectSetup.Root + "/Resources/Audio/AudioLibrary.asset";

        /// <summary>찾은 파일 하나</summary>
        public struct FoundTake
        {
            public int Number;
            public AudioClip Clip;
            public string Path;
            public string RelativePath;
        }

        /// <summary>빌드 보고 (시험장 · 콘솔)</summary>
        public sealed class Report
        {
            public int Files;
            public int KeysWithClips;
            public int ActiveTakes;
            public readonly List<string> UnknownKeys = new();
            public readonly List<string> Unparsable = new();
            public readonly List<string> MissingP0 = new();
            public readonly List<string> FewTakes = new();
            public readonly List<string> ExcludedByQc = new();
            public readonly List<string> Duplicates = new();
            /// <summary>바깥 파일(CC0 층 · 생성 음성)이 들어간 테이크 수 (THIRD_PARTY_AUDIO.md 대조 대상)</summary>
            public int ExternalTakes;
            /// <summary>THIRD_PARTY_AUDIO.md에 줄이 없는 바깥 파일 ("테이크: 이유") — 경고</summary>
            public readonly List<string> ThirdPartyMissing = new();
            /// <summary>대상 키에 켜 둔 테이크가 없어 조용한 별칭 ("별칭 → 대상") — 알림만 (경고 아님: 대상이 아직 없는 것도 정상)</summary>
            public readonly List<string> SilentAliases = new();

            public string ToText()
            {
                var sb = new StringBuilder();
                sb.Append("[Audio] 라이브러리: 파일 ").Append(Files).Append(" · 소리 있는 키 ").Append(KeysWithClips).Append('/').Append(AudioCatalog.Count)
                    .Append(" · 켜 둔 테이크 ").Append(ActiveTakes)
                    .Append(" · 바깥 파일 대조 ").Append(ExternalTakes).Append("개 테이크, 빠짐 ").Append(ThirdPartyMissing.Count);
                Append(sb, "모르는 키 (파일 이름 확인)", UnknownKeys);
                Append(sb, "읽을 수 없는 이름", Unparsable);
                Append(sb, "같은 키·테이크 파일이 둘", Duplicates);
                Append(sb, "THIRD_PARTY_AUDIO.md에 없는 바깥 파일", ThirdPartyMissing);
                Append(sb, "아직 없는 P0 키 (조용히 지나감)", MissingP0);
                Append(sb, "소리 없는 별칭 (대상에 테이크가 없음)", SilentAliases);
                Append(sb, "테이크가 목표보다 적은 키", FewTakes);
                Append(sb, "QC/선택으로 빠진 테이크", ExcludedByQc);
                return sb.ToString();
            }

            public bool HasWarnings => UnknownKeys.Count > 0 || Unparsable.Count > 0 || Duplicates.Count > 0 || ThirdPartyMissing.Count > 0;

            static void Append(StringBuilder sb, string title, List<string> items)
            {
                if (items.Count == 0) return;
                sb.Append("\n  ").Append(title).Append(" (").Append(items.Count).Append("): ");
                for (int i = 0; i < items.Count && i < 40; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(items[i]);
                }
                if (items.Count > 40) sb.Append(" …");
            }
        }

        /// <summary>마지막 빌드 보고</summary>
        public static Report LastReport { get; private set; }

        [MenuItem("IdleGame/Audio/라이브러리 다시 만들기", priority = 1)]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            if (EditorApplication.isPlaying) AudioService.ReloadLibrary();
            var report = LastReport;
            if (report == null) return;
            if (report.HasWarnings) Debug.LogWarning(report.ToText());
            else Debug.Log(report.ToText());
        }

        /// <summary>배치 빌드: -executeMethod IdleGame.EditorTools.AudioDataBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build();
            AssetDatabase.SaveAssets();
            if (LastReport != null) Debug.Log(LastReport.ToText());
        }

        /// <summary>키 → 찾은 테이크 (테이크 번호 순). 보고에 이름 문제를 적는다</summary>
        public static Dictionary<string, List<FoundTake>> FindTakes(Report report)
        {
            var result = new Dictionary<string, List<FoundTake>>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(AudioRoot)) return result;
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                string stem = Path.GetFileNameWithoutExtension(path);
                if (!AudioCatalog.ParseFileName(stem, out string key, out int take))
                {
                    report?.Unparsable.Add(path);
                    continue;
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (report != null) report.Files++;
                if (!AudioCatalog.Find(key).IsValid)
                {
                    report?.UnknownKeys.Add(Path.GetFileName(path));
                    continue;
                }
                if (!result.TryGetValue(key, out var list)) result[key] = list = new List<FoundTake>(4);
                bool duplicate = false;
                for (int t = 0; t < list.Count; t++)
                {
                    if (list[t].Number != take) continue;
                    duplicate = true;
                    report?.Duplicates.Add(Path.GetFileName(path) + " = " + Path.GetFileName(list[t].Path));
                    break;
                }
                if (duplicate) continue;
                list.Add(new FoundTake { Number = take, Clip = clip, Path = path, RelativePath = Relative(path) });
            }
            foreach (var list in result.Values) list.Sort((a, b) => a.Number.CompareTo(b.Number));
            return result;
        }

        /// <summary>Assets/_Project/Audio 기준 상대 경로</summary>
        public static string Relative(string assetPath)
        {
            string root = AudioRoot + "/";
            return assetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? assetPath.Substring(root.Length) : assetPath;
        }

        public static AudioLibrary Build()
        {
            var report = new Report();
            var found = FindTakes(report);
            var selection = AudioSelection.Load(false);
            var qc = AudioQcData.Load(AudioRoot);
            var keys = AudioCatalog.Keys;
            var entries = new AudioLibrary.Entry[keys.Length];
            var candidates = new List<AudioTakeCandidate>(8);
            var effective = new List<int>(8);

            for (int i = 0; i < keys.Length; i++)
            {
                var def = keys[i];
                var choice = selection != null ? selection.Find(def.Key) : null;
                var entry = new AudioLibrary.Entry
                {
                    key = def.Key,
                    trimDb = choice != null ? AudioLibraryRules.ClampTrim(choice.trimDb) : 0f,
                };
                entries[i] = entry;
                if (def.IsAlias) continue;
                if (!found.TryGetValue(def.Key, out var takes) || takes.Count == 0)
                {
                    entry.takes = Array.Empty<AudioLibrary.Take>();
                    if (def.Stage == 0) report.MissingP0.Add(def.Key);
                    continue;
                }

                candidates.Clear();
                for (int t = 0; t < takes.Count; t++)
                {
                    var file = qc.Find(takes[t].RelativePath);
                    candidates.Add(new AudioTakeCandidate
                    {
                        Number = takes[t].Number,
                        HasQc = file != null,
                        Recommended = file != null && file.HasRecommended && file.Recommended,
                        Passes = file == null || file.Passes,
                    });
                }
                AudioLibraryRules.EffectiveTakes(candidates, choice != null && choice.custom, choice?.activeTakes, effective);

                var built = new List<AudioLibrary.Take>(effective.Count);
                var music = new List<AudioLibrary.MusicInfo>(effective.Count);
                for (int t = 0; t < takes.Count; t++)
                {
                    var take = takes[t];
                    var file = qc.Find(take.RelativePath);
                    if (!effective.Contains(take.Number))
                    {
                        report.ExcludedByQc.Add(Path.GetFileName(take.Path));
                        continue;
                    }
                    built.Add(new AudioLibrary.Take
                    {
                        clip = take.Clip,
                        number = (byte)Mathf.Clamp(take.Number, 0, 255),
                        rootHz = def.IsTuned ? (file != null && file.RootHz.HasValue ? file.RootHz.Value : 0f) : 0f,
                        gainDb = file != null && file.GainDb.HasValue ? AudioLibraryRules.ClampTakeGain(file.GainDb.Value) : 0f,
                    });
                    music.Add(MusicInfoOf(file, def));
                }
                entry.takes = built.ToArray();
                if (def.Has(AudioKeyFlags.Music))
                {
                    entry.takeMusic = music.ToArray();
                    if (entry.takeMusic.Length > 0) entry.music = entry.takeMusic[0];
                }
                if (entry.takes.Length > 0) report.KeysWithClips++;
                report.ActiveTakes += entry.takes.Length;
                if (entry.takes.Length < def.Takes && !def.Has(AudioKeyFlags.Reserved)) report.FewTakes.Add(def.Key + " " + entry.takes.Length + "/" + def.Takes);
            }

            // 별칭: 대상의 테이크를 빌려 온다 (손질 · 게이트는 자기 것)
            for (int i = 0; i < keys.Length; i++)
            {
                var def = keys[i];
                if (!def.IsAlias || def.AliasOf < 0 || def.AliasOf >= entries.Length) continue;
                var target = entries[def.AliasOf];
                entries[i].takes = target.takes != null ? (AudioLibrary.Take[])target.takes.Clone() : Array.Empty<AudioLibrary.Take>();
                if (entries[i].takes.Length > 0) report.KeysWithClips++;
                else report.SilentAliases.Add(def.Key + " → " + keys[def.AliasOf].Key);
            }

            CheckThirdParty(found, qc, report);

            var library = LoadOrCreate();
            library.catalogVersion = AudioCatalog.Version;
            library.entries = entries;
            library.Invalidate();
            EditorUtility.SetDirty(library);
            LastReport = report;
            return library;
        }

        static AudioLibrary.MusicInfo MusicInfoOf(AudioQcData.FileQc file, in AudioKeyDef def)
        {
            if (!def.Has(AudioKeyFlags.Music)) return null;
            var info = new AudioLibrary.MusicInfo();
            if (file == null) return info;
            info.bpm = file.Bpm ?? 0f;
            info.beatsPerBar = Mathf.Max(1, file.BeatsPerBar ?? 4);
            info.beatOffset = file.BeatOffset ?? 0f;
            info.bars = file.Bars ?? 0;
            info.transposeSemitones = file.TransposeSemitones ?? 0;
            info.loudnessLufs = file.LoudnessLufs ?? file.LufsI ?? file.LufsS ?? 0f;
            return info;
        }

        // ── THIRD_PARTY_AUDIO.md 대조 (AUDIO-SPEC §7.2 "빌더가 경고" · §8.4 #6) ──

        public const string ThirdPartyFileName = "THIRD_PARTY_AUDIO.md";

        /// <summary>QC 출처의 바깥 파일 표시: "cc0:팩/파일.ogg" (여러 개 가능)</summary>
        static readonly Regex Cc0File = new(@"cc0:([^\s+]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>표 첫 칸이 테이크 한 개("키_NN")인 줄만 대조에 쓴다 (묶음 줄 · 출처 표는 건너뜀)</summary>
        static readonly Regex TakeStem = new(@"^[a-z0-9_]+_\d{1,3}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// THIRD_PARTY_AUDIO.md의 표 줄 → 테이크("atk_needle_01") → 그 테이크에 쓴 원본 파일 이름들.
        /// 줄 모양: "| 키_NN | 팩 | 원본 파일 | 주소 | 라이선스 | 손질 |". 글이 없거나 비면 빈 표 (예외 없음)
        /// </summary>
        public static Dictionary<string, HashSet<string>> ParseThirdPartyRows(string text)
        {
            var rows = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return rows;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length < 3 || line[0] != '|') continue;
                var cells = line.Trim('|').Split('|');
                if (cells.Length < 3) continue;
                string stem = cells[0].Trim();
                if (!TakeStem.IsMatch(stem)) continue;
                if (!rows.TryGetValue(stem, out var files)) rows[stem] = files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string file = FileNameOf(cells[2].Trim());
                if (file.Length > 0) files.Add(file);
            }
            return rows;
        }

        /// <summary>
        /// 테이크 한 개 대조. 바깥 파일이 섞인 테이크면 true (대조 대상).
        ///   · 출처에 "cc0:팩/파일"이 있으면 → 그 테이크 줄에 같은 파일 이름이 모두 있어야 한다
        ///   · 파일 이름 없이 "CC0"만 적힌 출처(음악 쪽 "HYB: … + CC0 layers") → 그 테이크 줄이 하나라도 있어야 한다
        ///   · 목소리(vox_*) 테이크 → 생성 음성 기록 줄(모델 · 목소리 · 대사)이 있어야 한다 (§7.9, CC0 아님)
        /// 빠진 것은 missing에 "테이크: 이유"로 더한다
        /// </summary>
        public static bool CheckThirdPartyTake(string stem, string source, Dictionary<string, HashSet<string>> rows, List<string> missing)
        {
            if (string.IsNullOrEmpty(stem)) return false;
            source ??= string.Empty;
            rows ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var matches = Cc0File.Matches(source);
            bool vox = stem.StartsWith("vox_", StringComparison.Ordinal);
            bool external = matches.Count > 0 || vox || source.IndexOf("CC0", StringComparison.Ordinal) >= 0;
            if (!external) return false;
            if (!rows.TryGetValue(stem, out var listed))
            {
                missing?.Add(stem + ": THIRD_PARTY_AUDIO.md에 이 테이크 줄이 없음" + (vox ? " (생성 음성: 모델 · 목소리 · 대사)" : string.Empty));
                return true;
            }
            for (int i = 0; i < matches.Count; i++)
            {
                string file = FileNameOf(matches[i].Groups[1].Value);
                if (file.Length > 0 && !listed.Contains(file)) missing?.Add(stem + ": '" + file + "' 줄이 없음");
            }
            return true;
        }

        /// <summary>디스크의 audio_qc.json + THIRD_PARTY_AUDIO.md만으로 돌린 대조 (테스트 · 배치). QC 파일이 없으면 null</summary>
        public static List<string> ThirdPartyWarningsOnDisk()
        {
            if (!File.Exists(Path.Combine(AudioRoot, AudioQcData.FileName))) return null;
            var qc = AudioQcData.Load(AudioRoot);
            var rows = ParseThirdPartyRows(ReadThirdParty());
            var missing = new List<string>();
            foreach (var file in qc.Files)
            {
                if (file == null || string.IsNullOrEmpty(file.Path) || !File.Exists(Path.Combine(AudioRoot, file.Path))) continue;
                CheckThirdPartyTake(Path.GetFileNameWithoutExtension(file.Path), file.Source, rows, missing);
            }
            return missing;
        }

        /// <summary>빌드 때: 프로젝트에 있는 모든 테이크(켜 둔 것 · 꺼 둔 것)를 QC 출처로 대조해 보고에 적는다</summary>
        static void CheckThirdParty(Dictionary<string, List<FoundTake>> found, AudioQcData qc, Report report)
        {
            var rows = ParseThirdPartyRows(ReadThirdParty());
            foreach (var list in found.Values)
            {
                for (int t = 0; t < list.Count; t++)
                {
                    var file = qc.Find(list[t].RelativePath);
                    if (CheckThirdPartyTake(Path.GetFileNameWithoutExtension(list[t].Path), file?.Source, rows, report.ThirdPartyMissing))
                        report.ExternalTakes++;
                }
            }
        }

        /// <summary>THIRD_PARTY_AUDIO.md 글 (없거나 못 읽으면 null — 대조에서 모든 바깥 테이크가 빠짐으로 나온다)</summary>
        static string ReadThirdParty()
        {
            string path = Path.Combine(AudioRoot, ThirdPartyFileName);
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] {path}을(를) 읽지 못했어요: {e.Message}");
                return null;
            }
        }

        static string FileNameOf(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName)) return string.Empty;
            int slash = Math.Max(pathOrName.LastIndexOf('/'), pathOrName.LastIndexOf('\\'));
            return (slash >= 0 ? pathOrName.Substring(slash + 1) : pathOrName).Trim();
        }

        static AudioLibrary LoadOrCreate()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library != null) return library;
            library = ScriptableObject.CreateInstance<AudioLibrary>();
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
            AssetDatabase.CreateAsset(library, LibraryPath);
            return library;
        }
    }
}
