using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 빌드된 소리 목록 (Resources/Audio/AudioLibrary.asset — AudioDataBuilder가 Assets/_Project/Audio의 {키}_{NN}.wav에서 만든다, AUDIO-SPEC §4.3).
    /// 키마다 켜 둔 테이크만 참조하므로 끈 테이크는 빌드에 들어가지 않는다. 손으로 고치지 않는다 (빌드할 때마다 덮어쓴다 — 선택은 AudioSelection).
    /// 카탈로그와 키 순서가 다르면(옛 라이브러리) 처음 읽을 때 이름으로 한 번 다시 맞춘다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "IdleGame/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/AudioLibrary";

        /// <summary>빌드할 때의 AudioCatalog.Version</summary>
        public int catalogVersion;
        /// <summary>카탈로그 키마다 하나 (카탈로그 순서)</summary>
        public Entry[] entries = Array.Empty<Entry>();

        [Serializable]
        public sealed class Entry
        {
            public string key;
            /// <summary>귀로 정한 볼륨 줄임 (AudioSelection, −12…0 dB)</summary>
            public float trimDb;
            /// <summary>켜 둔 테이크만, 테이크 번호 순</summary>
            public Take[] takes = Array.Empty<Take>();
            /// <summary>음악 키만 (bpm이 0이면 음악 정보 없음) — 첫 테이크의 정보</summary>
            public MusicInfo music = new();
            /// <summary>
            /// 테이크별 음악 정보 (takes와 같은 순서). 돌림 곡처럼 한 키의 테이크가 서로 다른 곡일 때 (bgm_battle_night 01/02/03 = 92 / 132 / 100 BPM)
            /// </summary>
            public MusicInfo[] takeMusic = Array.Empty<MusicInfo>();

            public bool HasClips
            {
                get
                {
                    if (takes == null) return false;
                    for (int i = 0; i < takes.Length; i++)
                    {
                        if (takes[i].clip != null) return true;
                    }
                    return false;
                }
            }

            public bool HasMusicInfo => music != null && music.bpm > 0f;
        }

        [Serializable]
        public struct Take
        {
            public AudioClip clip;
            /// <summary>파일 이름의 테이크 번호</summary>
            public byte number;
            /// <summary>조율 키의 측정 근음 (QC, Hz) — 0이면 조율하지 않은 소리</summary>
            public float rootHz;
            /// <summary>QC가 정한 테이크별 보정 (≤ 0 dB)</summary>
            public float gainDb;
        }

        [Serializable]
        public sealed class MusicInfo
        {
            public float bpm;
            public int beatsPerBar = 4;
            /// <summary>첫 박까지 (초)</summary>
            public float beatOffset;
            public int bars;
            /// <summary>F 펜타토닉 세계 대비 옮김 (반음) — 조율 키가 따라간다. 지금 곡은 모두 0</summary>
            public int transposeSemitones;
            public float loudnessLufs;
        }

        [NonSerialized] Entry[] _byCatalog;
        [NonSerialized] int _hasAnyClip = -1;
        [NonSerialized] bool _remapped;

        /// <summary>클립이 하나라도 있는지 (처음 읽을 때 한 번 센다)</summary>
        public bool HasAnyClip
        {
            get
            {
                if (_hasAnyClip < 0)
                {
                    _hasAnyClip = 0;
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Length; i++)
                        {
                            if (entries[i] != null && entries[i].HasClips)
                            {
                                _hasAnyClip = 1;
                                break;
                            }
                        }
                    }
                }
                return _hasAnyClip == 1;
            }
        }

        /// <summary>카탈로그 순서와 달라 이름으로 다시 맞췄는지 (에디터 경고: "오디오 라이브러리를 다시 만드세요")</summary>
        public bool WasRemapped
        {
            get
            {
                EnsureIndex();
                return _remapped;
            }
        }

        /// <summary>그 키의 칸 (없으면 null)</summary>
        public Entry Get(AudioId id)
        {
            if (!id.IsValid) return null;
            EnsureIndex();
            return id.Index < _byCatalog.Length ? _byCatalog[id.Index] : null;
        }

        /// <summary>그 키의 켜 둔 테이크 수</summary>
        public int TakeCount(AudioId id)
        {
            var entry = Get(id);
            return entry != null && entry.takes != null ? entry.takes.Length : 0;
        }

        /// <summary>그 키에 클립이 있는지</summary>
        public bool Has(AudioId id)
        {
            var entry = Get(id);
            return entry != null && entry.HasClips;
        }

        /// <summary>내용을 코드로 바꾼 뒤 (빌더 · 시험장) 캐시를 비운다</summary>
        public void Invalidate()
        {
            _byCatalog = null;
            _hasAnyClip = -1;
            _remapped = false;
        }

        void OnEnable() => Invalidate();

        void EnsureIndex()
        {
            if (_byCatalog != null && _byCatalog.Length == AudioCatalog.Count) return;
            var keys = AudioCatalog.Keys;
            _byCatalog = new Entry[keys.Length];
            _remapped = false;
            if (entries == null) return;
            bool aligned = catalogVersion == AudioCatalog.Version;
            for (int i = 0; aligned && i < keys.Length; i++)
            {
                if (i >= entries.Length) break;
                if (entries[i] == null || !string.Equals(entries[i].key, keys[i].Key, StringComparison.Ordinal)) aligned = false;
            }
            if (aligned)
            {
                for (int i = 0; i < keys.Length && i < entries.Length; i++) _byCatalog[i] = entries[i];
                return;
            }
            // 옛 라이브러리 (키 순서가 다름): 이름으로 한 번 맞춘다
            _remapped = true;
            var byName = new Dictionary<string, Entry>(entries.Length, StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key) || byName.ContainsKey(entry.key)) continue;
                byName.Add(entry.key, entry);
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (byName.TryGetValue(keys[i].Key, out var entry)) _byCatalog[i] = entry;
            }
        }
    }
}
