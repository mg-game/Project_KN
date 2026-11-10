using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 귀로 고른 테이크 · 볼륨 손질 · 별점 · 메모 (AUDIO-SPEC §4.4). 사운드 시험장만 쓰고, 라이브러리 빌더는 읽기만 한다 (절대 덮어쓰지 않는다).
    /// 키의 실제 테이크 = custom이면 activeTakes ∩ 있는 파일, 아니면 QC 추천 → QC 통과 → 전부 (AudioLibraryRules.EffectiveTakes).
    /// </summary>
    public sealed class AudioSelection : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Audio/AudioSelection.asset";
        public const float MinTrimDb = -12f;

        [Serializable]
        public sealed class KeyChoice
        {
            public string key;
            /// <summary>false = QC 추천을 따른다</summary>
            public bool custom;
            /// <summary>custom일 때 켜 둔 테이크 번호</summary>
            public byte[] activeTakes = Array.Empty<byte>();
            /// <summary>귀로 정한 볼륨 줄임 (−12…0 dB)</summary>
            public float trimDb;
            /// <summary>별점: 테이크 번호 → ★1~5 (나란한 배열)</summary>
            public byte[] ratedTakes = Array.Empty<byte>();
            public byte[] ratings = Array.Empty<byte>();
            /// <summary>메모 ("너무 밝음", "2배속에만" …)</summary>
            public string note = string.Empty;

            public bool IsActive(int take)
            {
                if (activeTakes == null) return false;
                for (int i = 0; i < activeTakes.Length; i++)
                {
                    if (activeTakes[i] == take) return true;
                }
                return false;
            }

            public int Rating(int take)
            {
                if (ratedTakes == null || ratings == null) return 0;
                for (int i = 0; i < ratedTakes.Length && i < ratings.Length; i++)
                {
                    if (ratedTakes[i] == take) return ratings[i];
                }
                return 0;
            }

            public void SetRating(int take, int stars)
            {
                stars = Mathf.Clamp(stars, 0, 5);
                ratedTakes ??= Array.Empty<byte>();
                ratings ??= Array.Empty<byte>();
                for (int i = 0; i < ratedTakes.Length && i < ratings.Length; i++)
                {
                    if (ratedTakes[i] != take) continue;
                    ratings[i] = (byte)stars;
                    return;
                }
                if (stars == 0) return;
                int n = Mathf.Min(ratedTakes.Length, ratings.Length);
                var newTakes = new byte[n + 1];
                var newRatings = new byte[n + 1];
                Array.Copy(ratedTakes, newTakes, n);
                Array.Copy(ratings, newRatings, n);
                newTakes[n] = (byte)Mathf.Clamp(take, 0, 255);
                newRatings[n] = (byte)stars;
                ratedTakes = newTakes;
                ratings = newRatings;
            }

            /// <summary>별점 평균 (없으면 0)</summary>
            public float AverageRating
            {
                get
                {
                    if (ratings == null || ratings.Length == 0) return 0f;
                    int sum = 0;
                    int count = 0;
                    for (int i = 0; i < ratings.Length; i++)
                    {
                        if (ratings[i] == 0) continue;
                        sum += ratings[i];
                        count++;
                    }
                    return count > 0 ? (float)sum / count : 0f;
                }
            }

            public void SetActive(int take, bool on)
            {
                var list = new List<byte>(activeTakes ?? Array.Empty<byte>());
                list.Remove((byte)take);
                if (on) list.Add((byte)take);
                list.Sort();
                activeTakes = list.ToArray();
            }
        }

        public List<KeyChoice> keys = new();

        public KeyChoice Find(string key)
        {
            if (string.IsNullOrEmpty(key) || keys == null) return null;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && keys[i].key == key) return keys[i];
            }
            return null;
        }

        public KeyChoice GetOrAdd(string key)
        {
            var found = Find(key);
            if (found != null) return found;
            found = new KeyChoice { key = key };
            keys.Add(found);
            return found;
        }

        /// <summary>선택 에셋을 읽는다 (없고 create면 만든다 — 빌더는 create=false)</summary>
        public static AudioSelection Load(bool create)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AudioSelection>(AssetPath);
            if (asset != null || !create) return asset;
            asset = CreateInstance<AudioSelection>();
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
