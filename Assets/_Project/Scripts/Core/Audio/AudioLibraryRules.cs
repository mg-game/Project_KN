using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>테이크 후보 하나 (파일이 있는 테이크 + 그 QC 결과)</summary>
    public struct AudioTakeCandidate
    {
        public int Number;
        /// <summary>audio_qc.json에 이 파일이 있는지</summary>
        public bool HasQc;
        public bool Recommended;
        public bool Passes;
    }

    /// <summary>
    /// 라이브러리 빌드 규칙 (순수 C# — 에디터 빌더와 AudioLibraryTests가 같이 쓴다, AUDIO-SPEC §4.4).
    /// 키의 실제 테이크: (1) 귀로 고름(custom) → 켜 둔 테이크 ∩ 있는 파일 (2) QC 추천 (3) QC 경계 통과 (4) 전부.
    /// 결과는 테이크 번호 순.
    /// </summary>
    public static class AudioLibraryRules
    {
        public const float MinTrimDb = -12f;
        public const float MaxTrimDb = 0f;

        public static void EffectiveTakes(IReadOnlyList<AudioTakeCandidate> takes, bool custom, byte[] activeTakes, List<int> result)
        {
            result.Clear();
            if (takes == null || takes.Count == 0) return;
            if (custom)
            {
                if (activeTakes == null) return;
                for (int i = 0; i < takes.Count; i++)
                {
                    if (Contains(activeTakes, takes[i].Number)) result.Add(takes[i].Number);
                }
                result.Sort();
                return;
            }
            for (int i = 0; i < takes.Count; i++)
            {
                if (takes[i].HasQc && takes[i].Recommended) result.Add(takes[i].Number);
            }
            if (result.Count == 0)
            {
                for (int i = 0; i < takes.Count; i++)
                {
                    if (takes[i].HasQc && takes[i].Passes) result.Add(takes[i].Number);
                }
            }
            if (result.Count == 0)
            {
                for (int i = 0; i < takes.Count; i++) result.Add(takes[i].Number);
            }
            result.Sort();
        }

        static bool Contains(byte[] values, int number)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == number) return true;
            }
            return false;
        }

        /// <summary>귀로 정한 볼륨 손질 범위 (−12…0 dB)</summary>
        public static float ClampTrim(float db) => Mathf.Clamp(db, MinTrimDb, MaxTrimDb);

        /// <summary>QC 테이크 보정 (≤ 0 dB만 — 런타임은 볼륨을 올리지 못한다)</summary>
        public static float ClampTakeGain(float db) => Mathf.Clamp(db, -24f, 0f);
    }
}
