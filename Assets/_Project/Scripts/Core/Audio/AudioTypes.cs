using System;

namespace IdleGame
{
    /// <summary>코드 버스 (AudioMixer 에셋 없음 — AUDIO-SPEC §0.5, §3.3)</summary>
    public enum AudioBus : byte
    {
        /// <summary>UI 효과음 (효과음 볼륨, 소스 8)</summary>
        Ui = 0,
        /// <summary>전투 효과음 + 빌린 환경음 한 방 (효과음 볼륨, 소스 14)</summary>
        Sfx = 1,
        /// <summary>징글 (효과음 볼륨, 소스 1)</summary>
        Stinger = 2,
        /// <summary>배경음 (배경음 볼륨, 소스 2 — 교차 페이드 짝)</summary>
        Bgm = 3,
        /// <summary>환경음 (배경음 볼륨 × 0.9, 소스 4)</summary>
        Amb = 4,
        /// <summary>계속 도는 전투 소리: 심장 박동 · 보스 예비 동작 (효과음 볼륨, 소스 2)</summary>
        Loop = 5,
    }

    /// <summary>목소리 등급 — 빼앗는 순서 (Critical &gt; High &gt; Normal &gt; Low, 숫자가 작을수록 중요)</summary>
    public enum AudioVoiceClass : byte
    {
        Critical = 0,
        High = 1,
        Normal = 2,
        Low = 3,
    }

    /// <summary>음악 상태 (우선순위: Story 60 &gt; Gacha 50 &gt; Event 40 &gt; Dungeon 30 &gt; Boss 20 &gt; Main 10 &gt; Silent 0)</summary>
    public enum MusicState : byte
    {
        Silent = 0,
        Main = 1,
        Boss = 2,
        Dungeon = 3,
        Gacha = 4,
        Story = 5,
        Event = 6,
    }

    /// <summary>전장 집중 (시트·전체 화면 팝업이 전장을 덮을 때). 여러 곳이 요청하면 가장 강한 것</summary>
    public enum BattleAudioFocus : byte
    {
        /// <summary>전장이 다 보임 (기본)</summary>
        Full = 0,
        /// <summary>시트가 전장 40% 이상을 덮음: 전투 −6 dB · 환경음 −4 dB · 배경음 6 kHz 저역 통과</summary>
        Dimmed = 1,
        /// <summary>전체 화면 (소환 결과 · 진화 · 이야기): 새 전투 소리 없음 · 환경음 −12 dB</summary>
        Hidden = 2,
    }

    /// <summary>키 성질 (AUDIO-SPEC §2.0 Flags)</summary>
    [Flags]
    public enum AudioKeyFlags : ushort
    {
        None = 0,
        /// <summary>측정한 rootHz로 음높이를 맞춘다 (흔들기 없음)</summary>
        Tuned = 1 << 0,
        /// <summary>계속 도는 클립 (AudioSource.loop)</summary>
        Loop = 1 << 1,
        /// <summary>F 펜타토닉 세계 안에서 만든 소리</summary>
        InKey = 1 << 2,
        /// <summary>갈래 기본값을 바꾸는 소리 (예: death_tangle)</summary>
        Override = 1 << 3,
        /// <summary>환경음 한 방 (빌린 Sfx 목소리, 환경음 볼륨)</summary>
        Emitter = 1 << 4,
        /// <summary>작곡된 음악 (배경음 · 징글)</summary>
        Music = 1 << 5,
        Stereo = 1 << 6,
        /// <summary>음악 도입부 ({음악}_intro, 한 번만)</summary>
        Intro = 1 << 7,
        /// <summary>캐릭터 목소리 (AudioService.VoiceBarks가 켜져야 울림)</summary>
        Vox = 1 << 8,
        /// <summary>다른 키의 클립을 빌려 쓰는 별칭 (AliasOf)</summary>
        Alias = 1 << 9,
        /// <summary>아직 없는 기능용 예약 키 (P3)</summary>
        Reserved = 1 << 10,
    }

    /// <summary>키 묶음 (§2 표, 사운드 시험장 필터)</summary>
    public enum AudioGroup : byte
    {
        Ui = 0,
        Ally = 1,
        Enemy = 2,
        Defense = 3,
        Skill = 4,
        Status = 5,
        Flow = 6,
        Amb = 7,
        Emit = 8,
        Bgm = 9,
        Jingle = 10,
        Vox = 11,
        Reserved = 12,
        Alias = 13,
    }

    /// <summary>음량 단계 (§1.8)</summary>
    public enum AudioTier : byte
    {
        S = 0,
        A = 1,
        B = 2,
        C = 3,
        /// <summary>전투 반복 소리 (심장 박동 · 보스 예비 동작)</summary>
        L = 4,
        /// <summary>징글 (짧은 구간 최대 −13)</summary>
        J = 5,
        /// <summary>전투 배경음 (통합 −18)</summary>
        M = 6,
        /// <summary>메뉴 배경음 (통합 −16, 잠 −22)</summary>
        MPlus = 7,
        /// <summary>환경음 반복 (통합 −26)</summary>
        Amb = 8,
        /// <summary>환경음 한 방 (−24)</summary>
        E = 9,
    }

    /// <summary>
    /// 소리 키 번호 (AudioCatalog.Keys 인덱스). 기본값(default)은 무효 — 초기화 안 된 필드가 실수로 0번 키를 울리지 않게.
    /// 문자열 → 번호는 시작할 때 한 번만 (AudioService.Resolve), 매 프레임 찾지 않는다.
    /// </summary>
    public readonly struct AudioId : IEquatable<AudioId>
    {
        readonly short _plusOne;

        public AudioId(int index) => _plusOne = (short)(index < 0 || index >= short.MaxValue ? 0 : index + 1);

        /// <summary>키 배열 인덱스 (무효면 −1)</summary>
        public short Index => (short)(_plusOne - 1);
        public bool IsValid => _plusOne > 0;
        public static AudioId None => default;

        public bool Equals(AudioId other) => _plusOne == other._plusOne;
        public override bool Equals(object obj) => obj is AudioId other && Equals(other);
        public override int GetHashCode() => _plusOne;
        public static bool operator ==(AudioId a, AudioId b) => a._plusOne == b._plusOne;
        public static bool operator !=(AudioId a, AudioId b) => a._plusOne != b._plusOne;
        public override string ToString() => IsValid && Index < AudioCatalog.Count ? AudioCatalog.Keys[Index].Key : "(none)";
    }

    /// <summary>개발용 통계 (목소리 사용 · 버린 소리 · 예산 초과 · 없는 키). 할당 없이 숫자만 센다</summary>
    public sealed class AudioStats
    {
        public const int BusCount = 6;

        /// <summary>지금 울리는 목소리 수 (버스별)</summary>
        public readonly int[] VoicesInUse = new int[BusCount];
        /// <summary>목소리가 모자라 버린 소리 (버스별)</summary>
        public readonly int[] Drops = new int[BusCount];
        /// <summary>빼앗긴 목소리 (버스별)</summary>
        public readonly int[] Steals = new int[BusCount];
        /// <summary>범주 예산(토큰 통)에 걸려 버린 소리</summary>
        public int BudgetHits;
        /// <summary>게이트(간격·동시 수)에 걸린 소리</summary>
        public int GateBlocks;
        /// <summary>군중 묶음으로 접힌 명중 소리</summary>
        public int Folded;
        /// <summary>예약했다가 시작한 소리 / 넘쳐서 버린 예약</summary>
        public int Committed;
        public int QueueOverflow;
        /// <summary>최근 1초 동안 새로 시작한 전투 소리 수 (§8.4: 28 이하)</summary>
        public int BattleStartsLastSecond;
        /// <summary>클립이 없는 키를 부른 횟수</summary>
        public int Missing;

        public void Clear()
        {
            Array.Clear(VoicesInUse, 0, BusCount);
            Array.Clear(Drops, 0, BusCount);
            Array.Clear(Steals, 0, BusCount);
            BudgetHits = 0;
            GateBlocks = 0;
            Folded = 0;
            Committed = 0;
            QueueOverflow = 0;
            BattleStartsLastSecond = 0;
            Missing = 0;
        }
    }
}
