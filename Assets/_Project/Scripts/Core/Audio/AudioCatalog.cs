using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>키 하나의 규칙 (버스 · 등급 · 음량 단계 · 게이트 · 플래그 · 사다리). 표는 AudioCatalog.Keys.cs</summary>
    public readonly struct AudioKeyDef
    {
        public readonly string Key;
        public readonly AudioGroup Group;
        public readonly AudioBus Bus;
        public readonly AudioVoiceClass Class;
        /// <summary>AudioSource.priority (0 = 가장 중요)</summary>
        public readonly byte UnityPriority;
        public readonly AudioTier Tier;
        /// <summary>음량 단계 목표 (문서 · 시험장 표시용, 파일은 이미 이 값으로 맞춰져 있다)</summary>
        public readonly sbyte TargetLufs;
        /// <summary>목표 길이 (초, 반복은 반복 길이)</summary>
        public readonly float Length;
        /// <summary>내보낼 테이크 수 (Var)</summary>
        public readonly byte Takes;
        /// <summary>같은 키 최소 간격 (초)</summary>
        public readonly float MinInterval;
        /// <summary>같은 키 동시 목소리</summary>
        public readonly byte MaxVoices;
        /// <summary>음높이 흔들기 (± 비율)</summary>
        public readonly float PitchJitter;
        /// <summary>볼륨 흔들기 (± dB)</summary>
        public readonly float VolumeJitterDb;
        public readonly AudioKeyFlags Flags;
        /// <summary>0 = 없음, 그 밖 = AudioCatalog.Ladders 인덱스</summary>
        public readonly byte Ladder;
        /// <summary>조율 키의 기준 음 (QC rootHz가 없을 때 쓰는 렌더 근음, Hz)</summary>
        public readonly float TargetRootHz;
        /// <summary>별칭이면 클립을 빌려 오는 키 인덱스, 아니면 −1</summary>
        public readonly short AliasOf;
        /// <summary>0~3 = P0~P3</summary>
        public readonly byte Stage;
        /// <summary>UISfxKey 값 (없으면 −1)</summary>
        public readonly short UiKey;

        public AudioKeyDef(string key, AudioGroup group, AudioBus bus, AudioVoiceClass voiceClass, int unityPriority, AudioTier tier, int targetLufs,
            float length, int takes, float minInterval, int maxVoices, float pitchJitter, float volumeJitterDb, AudioKeyFlags flags, int ladder,
            float targetRootHz, int aliasOf, int stage, int uiKey)
        {
            Key = key;
            Group = group;
            Bus = bus;
            Class = voiceClass;
            UnityPriority = (byte)Mathf.Clamp(unityPriority, 0, 255);
            Tier = tier;
            TargetLufs = (sbyte)Mathf.Clamp(targetLufs, -60, 0);
            Length = length;
            Takes = (byte)Mathf.Clamp(takes, 0, 255);
            MinInterval = Mathf.Max(0f, minInterval);
            MaxVoices = (byte)Mathf.Clamp(maxVoices, 1, 16);
            PitchJitter = Mathf.Max(0f, pitchJitter);
            VolumeJitterDb = Mathf.Max(0f, volumeJitterDb);
            Flags = flags;
            Ladder = (byte)Mathf.Clamp(ladder, 0, 255);
            TargetRootHz = targetRootHz;
            AliasOf = (short)aliasOf;
            Stage = (byte)Mathf.Clamp(stage, 0, 3);
            UiKey = (short)uiKey;
        }

        public bool Has(AudioKeyFlags flag) => (Flags & flag) != 0;
        public bool IsAlias => AliasOf >= 0;
        public bool IsTuned => (Flags & AudioKeyFlags.Tuned) != 0;
        public bool IsMusic => Bus == AudioBus.Bgm;
        public bool IsJingle => Bus == AudioBus.Stinger;
    }

    /// <summary>
    /// 조율 사다리 (§1.3). 음 = RootHz × 2^(Steps[i]/12). Window 안에 이어지면 한 칸 오르고(맨 위는 유지), Reset만큼 조용하면 처음으로.
    /// Window 0 = 부르는 쪽이 칸을 정한다 (자장가 · 불빛 물결 도착 순서 · 게이지 · 연쇄)
    /// </summary>
    public readonly struct PitchLadderDef
    {
        public readonly string Name;
        public readonly float RootHz;
        public readonly sbyte[] Steps;
        public readonly float Window;
        public readonly float Reset;

        public PitchLadderDef(string name, float rootHz, sbyte[] steps, float window, float reset)
        {
            Name = name;
            RootHz = rootHz;
            Steps = steps ?? Array.Empty<sbyte>();
            Window = window;
            Reset = reset;
        }

        public int Count => Steps.Length;

        /// <summary>칸 step의 음 (Hz). 범위를 넘으면 맨 위 / 맨 아래 칸</summary>
        public float StepHz(int step, int transposeSemitones = 0)
        {
            if (Steps.Length == 0) return RootHz;
            int i = Mathf.Clamp(step, 0, Steps.Length - 1);
            return RootHz * AudioCatalog.SemitoneRatio(Steps[i] + transposeSemitones);
        }
    }

    /// <summary>
    /// 소리 키 목록 (코드가 기준 — AUDIO-SPEC §4.2). Keys 순서가 AudioId이고 끝에만 더한다 (Version을 올린다).
    /// 라이브러리(AudioLibrary)는 키 이름으로 다시 맞추므로 순서가 바뀐 옛 라이브러리도 읽힌다.
    /// 파일 이름 규칙: {키}_{NN}.wav|.ogg (NN 01~99) · {키}.wav (테이크 1) · {키}_{N}.wav. 키는 숫자 토큰으로 끝나지 않는다.
    /// </summary>
    public static partial class AudioCatalog
    {
        /// <summary>키를 더하거나 순서를 바꾸면 올린다 (2 = RM-2: SPEC4 별칭 6개를 끝에 더함)</summary>
        public const int Version = 2;
        /// <summary>내보내는 키 수 (§2.13 — 첫 306개는 순서가 고정이다, AudioCatalogTests가 해시로 지킨다)</summary>
        public const int ShippingKeyCount = 306;

        public const int LadderNone = 0;
        public const int LadderLullaby = 1;
        public const int LadderKill = 2;
        public const int LadderLamp = 3;
        public const int LadderCount = 4;
        public const int LadderGauge = 5;
        public const int LadderChain = 6;

        /// <summary>라라벨 자장가 7번째 음을 내는 형태 (그 밖의 형태는 7번째가 쉼표)</summary>
        public static readonly string[] FinaleFormIds = { "lala_finale" };

        /// <summary>모든 키 (순서 = AudioId.Index)</summary>
        public static readonly AudioKeyDef[] Keys;
        /// <summary>UISfxKey 값 → 키 (길이 = UISfxKey 개수)</summary>
        public static readonly AudioId[] UiMap;
        /// <summary>사다리 (인덱스 = AudioKeyDef.Ladder)</summary>
        public static readonly PitchLadderDef[] Ladders;

        static readonly Dictionary<string, short> ByName;

        static AudioCatalog()
        {
            Keys = KeyTable;
            ByName = new Dictionary<string, short>(Keys.Length, StringComparer.Ordinal);
            for (short i = 0; i < Keys.Length; i++)
            {
                if (!string.IsNullOrEmpty(Keys[i].Key) && !ByName.ContainsKey(Keys[i].Key)) ByName.Add(Keys[i].Key, i);
            }
            Ladders = new[]
            {
                new PitchLadderDef("none", 0f, Array.Empty<sbyte>(), 0f, 0f),
                // C6 A5 F5 G5 A5 G5 — 7번째(F5)는 쉼표, lala_finale만 낸다
                new PitchLadderDef("lullaby", 698.46f, new sbyte[] { 7, 4, 0, 2, 4, 2 }, 0f, 0f),
                // 0.8초 안의 처치마다 한 칸, 1.2초 조용하면 처음, 맨 위 유지
                new PitchLadderDef("kill", 698.46f, new sbyte[] { 0, 2, 4, 7, 9, 12, 14, 16 }, 0.8f, 1.2f),
                // C5 F5 A5 C6 F6 A6 C7 — 빛 물결이 닿는 순서, 최대 7음
                new PitchLadderDef("lamp", 523.25f, new sbyte[] { 0, 5, 9, 12, 17, 21, 24 }, 0f, 0f),
                // 숫자 올라감 한 칸마다 (70ms에 하나), 맨 위 유지, 0.5초 쉬면 처음
                new PitchLadderDef("count", 1046.5f, new sbyte[] { 0, 2, 5, 7, 9, 12 }, 0.5f, 0.5f),
                // 불빛 게이지 25 / 50 / 75% → F5 A5 C6
                new PitchLadderDef("gauge", 698.46f, new sbyte[] { 0, 4, 7 }, 0f, 0f),
                // 시전 하나 안의 연쇄 한 번마다
                new PitchLadderDef("chain", 1046.5f, new sbyte[] { 0, 2, 5, 7, 9 }, 0f, 0f),
            };
            UiMap = BuildUiMap();
        }

        public static int Count => Keys.Length;

        /// <summary>키 이름 → 번호 (없으면 무효). 시작할 때만 부른다 (사전 조회)</summary>
        public static AudioId Find(string key)
        {
            if (string.IsNullOrEmpty(key) || ByName == null) return AudioId.None;
            return ByName.TryGetValue(key, out short index) ? new AudioId(index) : AudioId.None;
        }

        /// <summary>그 번호의 규칙 (무효면 빈 값 — Key가 null)</summary>
        public static AudioKeyDef Get(AudioId id) => id.IsValid && id.Index < Keys.Length ? Keys[id.Index] : default;

        /// <summary>UISfxKey → 키 번호</summary>
        public static AudioId Ui(UISfxKey key)
        {
            int index = (int)key;
            return index >= 0 && index < UiMap.Length ? UiMap[index] : AudioId.None;
        }

        /// <summary>클립을 가져올 키 (별칭이면 대상, 아니면 자기 자신)</summary>
        public static AudioId ClipSource(AudioId id)
        {
            if (!id.IsValid || id.Index >= Keys.Length) return AudioId.None;
            short alias = Keys[id.Index].AliasOf;
            return alias >= 0 && alias < Keys.Length ? new AudioId(alias) : id;
        }

        /// <summary>반음 → 배율 (2^(n/12))</summary>
        public static float SemitoneRatio(float semitones) => Mathf.Pow(2f, semitones / 12f);

        /// <summary>두 음 사이 반음 수 (to/from)</summary>
        public static float Semitones(float fromHz, float toHz) =>
            fromHz > 0f && toHz > 0f ? 12f * Mathf.Log(toHz / fromHz, 2f) : 0f;

        /// <summary>사다리 칸의 음 (Hz). 사다리가 없으면 0</summary>
        public static float LadderHz(int ladder, int step, int transposeSemitones = 0)
        {
            if (ladder <= 0 || ladder >= Ladders.Length) return 0f;
            return Ladders[ladder].StepHz(step, transposeSemitones);
        }

        /// <summary>
        /// 파일 이름(확장자 없이) → 키 + 테이크. 마지막 '_' 뒤가 모두 숫자면 테이크 번호(1~99), 아니면 테이크 1.
        /// atk_needle_03 → (atk_needle, 3) · atk_needle → (atk_needle, 1) · ui_tap_2 → (ui_tap, 2) · bgm_gacha_intro_01 → (bgm_gacha_intro, 1)
        /// </summary>
        public static bool ParseFileName(string stem, out string key, out int take)
        {
            key = null;
            take = 0;
            if (string.IsNullOrEmpty(stem)) return false;
            stem = stem.Trim();
            int us = stem.LastIndexOf('_');
            if (us > 0 && us < stem.Length - 1)
            {
                bool digits = true;
                int value = 0;
                for (int i = us + 1; i < stem.Length; i++)
                {
                    char c = stem[i];
                    if (c < '0' || c > '9')
                    {
                        digits = false;
                        break;
                    }
                    value = value * 10 + (c - '0');
                    if (value > 999) break;
                }
                if (digits)
                {
                    if (value < 1 || value > 99) return false;
                    key = stem.Substring(0, us);
                    take = value;
                    return key.Length > 0;
                }
            }
            key = stem;
            take = 1;
            return true;
        }

        /// <summary>계보 명중 키(AttackFxCatalog.Look(id).Sfx, 예: atk_needle) → 쏘는 소리 키 (fire_needle)</summary>
        public static string FireKeyOf(string hitKey) =>
            !string.IsNullOrEmpty(hitKey) && hitKey.StartsWith("atk_", StringComparison.Ordinal) ? "fire_" + hitKey.Substring(4) : null;

        /// <summary>명중 키 → 치명타 키 (atk_needle → atk_needle_crit)</summary>
        public static string CritKeyOf(string hitKey) => string.IsNullOrEmpty(hitKey) ? null : hitKey + "_crit";

        /// <summary>그 키가 클립을 빌려 쓰는 대상까지 포함해 실제 키 목록에 있는지</summary>
        public static bool IsKnown(string key) => Find(key).IsValid;
    }
}
