using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// audio_qc.json 읽기 (AUDIO-SPEC §4.5 — 파일마다 측정값 · 경계 통과 · 추천 · 근음 · 보정 · 음악 정보 · 만든 경로).
    /// 키는 Assets/_Project/Audio 기준 상대 경로 ("Sfx/Battle/atk_needle_03.wav"). 경로가 달라도 파일 이름으로 다시 찾는다 (폴더는 사람용).
    /// </summary>
    public sealed class AudioQcData
    {
        public const string FileName = "audio_qc.json";

        public sealed class FileQc
        {
            public string Path;
            public string Key;
            public int Take;
            public float Duration;
            public float? LufsMmax;
            public float? LufsS;
            public float? LufsI;
            public float? TruePeak;
            public float? AttackMs;
            public float? CentroidHz;
            public float? Harsh;
            public float? PhoneLossLu;
            public float? SpeechScore;
            public float? Score;
            public float? RootHz;
            public float? GainDb;
            public bool Passes = true;
            public bool HasPasses;
            public bool Recommended;
            public bool HasRecommended;
            public string[] FailReasons = Array.Empty<string>();
            public string Source;
            public string Recipe;
            public string Variation;
            public string Master;
            public float? Bpm;
            public int? BeatsPerBar;
            public float? BeatOffset;
            public int? Bars;
            public int? TransposeSemitones;
            public float? LoudnessLufs;

            /// <summary>시험장 한 줄: 길이 · 음량 · 최대 · 어택 · 무게중심 · 점수</summary>
            public string Summary()
            {
                var parts = new List<string>(8) { Duration.ToString("0.00") + "s" };
                if (LufsMmax.HasValue) parts.Add("M " + LufsMmax.Value.ToString("0.0"));
                if (LufsS.HasValue && !LufsMmax.HasValue) parts.Add("S " + LufsS.Value.ToString("0.0"));
                if (LufsI.HasValue) parts.Add("I " + LufsI.Value.ToString("0.0"));
                if (TruePeak.HasValue) parts.Add("TP " + TruePeak.Value.ToString("0.0"));
                if (AttackMs.HasValue) parts.Add("어택 " + AttackMs.Value.ToString("0.0") + "ms");
                if (CentroidHz.HasValue) parts.Add(CentroidHz.Value.ToString("0") + "Hz");
                if (RootHz.HasValue) parts.Add("근음 " + RootHz.Value.ToString("0.0"));
                if (Score.HasValue) parts.Add("점수 " + Score.Value.ToString("0"));
                if (HasRecommended) parts.Add(Recommended ? "추천" : "-");
                if (!Passes) parts.Add("⚠ " + string.Join(",", FailReasons));
                return string.Join(" · ", parts);
            }
        }

        readonly Dictionary<string, FileQc> _byPath = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, FileQc> _byName = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _byPath.Count;
        public string Generated { get; private set; }
        public IEnumerable<FileQc> Files => _byPath.Values;

        /// <summary>audioRoot(예: Assets/_Project/Audio)의 audio_qc.json. 없거나 읽지 못하면 빈 데이터 (경고 한 줄)</summary>
        public static AudioQcData Load(string audioRoot)
        {
            var data = new AudioQcData();
            string path = System.IO.Path.Combine(audioRoot, FileName);
            if (!File.Exists(path)) return data;
            try
            {
                data.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] {path}을(를) 읽지 못했어요: {e.Message}");
            }
            return data;
        }

        public void Parse(string json)
        {
            var root = JObject.Parse(json);
            Generated = (string)root["generated"];
            if (!(root["files"] is JObject files)) return;
            foreach (var property in files.Properties())
            {
                if (!(property.Value is JObject o)) continue;
                string path = Normalize(property.Name);
                var qc = new FileQc
                {
                    Path = path,
                    Key = (string)o["key"],
                    Take = Int(o["take"]) ?? 0,
                    Duration = Float(o["duration"]) ?? 0f,
                    LufsMmax = Float(o["lufsMmax"]),
                    LufsS = Float(o["lufsS"]),
                    LufsI = Float(o["lufsI"]),
                    TruePeak = Float(o["truePeak"]),
                    AttackMs = Float(o["attackMs"]),
                    CentroidHz = Float(o["centroidHz"]),
                    Harsh = Float(o["harsh"]),
                    PhoneLossLu = Float(o["phoneLossLu"]),
                    SpeechScore = Float(o["speechScore"]),
                    Score = Float(o["score"]),
                    RootHz = Float(o["rootHz"]),
                    GainDb = Float(o["gainDb"]),
                    Source = (string)o["source"],
                    Recipe = (string)o["recipe"],
                    Variation = (string)o["variation"],
                    Master = (string)o["master"],
                    Bpm = Float(o["bpm"]),
                    BeatsPerBar = Int(o["beatsPerBar"]),
                    BeatOffset = Float(o["beatOffset"]),
                    Bars = Int(o["bars"]),
                    TransposeSemitones = Int(o["transposeSemitones"]),
                    LoudnessLufs = Float(o["loudnessLufs"]),
                };
                var passes = o["passes"];
                if (passes != null && passes.Type == JTokenType.Boolean)
                {
                    qc.Passes = (bool)passes;
                    qc.HasPasses = true;
                }
                var recommended = o["recommended"];
                if (recommended != null && recommended.Type == JTokenType.Boolean)
                {
                    qc.Recommended = (bool)recommended;
                    qc.HasRecommended = true;
                }
                if (o["failReasons"] is JArray fails)
                {
                    var reasons = new List<string>(fails.Count);
                    foreach (var f in fails) reasons.Add(f.ToString());
                    qc.FailReasons = reasons.ToArray();
                }
                _byPath[path] = qc;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(name)) _byName[name] = qc;
            }
        }

        /// <summary>그 파일의 QC (상대 경로 → 없으면 파일 이름). 없으면 null</summary>
        public FileQc Find(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (_byPath.TryGetValue(Normalize(relativePath), out var qc)) return qc;
            return _byName.TryGetValue(System.IO.Path.GetFileNameWithoutExtension(relativePath), out qc) ? qc : null;
        }

        static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

        static float? Float(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) return (float)token;
            return float.TryParse(token.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                ? v
                : (float?)null;
        }

        static int? Int(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type == JTokenType.Integer) return (int)token;
            if (token.Type == JTokenType.Float) return Mathf.RoundToInt((float)token);
            return int.TryParse(token.ToString(), out int v) ? v : (int?)null;
        }
    }
}
