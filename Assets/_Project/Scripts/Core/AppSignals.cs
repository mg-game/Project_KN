using System;

namespace IdleGame
{
    /// <summary>
    /// 패키지 사이 실행 중 신호 (SPEC4 §2.6, UI에 기대지 않는 Core). 보내는 쪽과 받는 쪽이 서로의 형식을 몰라도 되게 한다.
    ///   · SleepChanged: 잠자기 모드 들어감/깸 (SleepModeView가 보냄 → BattleView 연출 줄이기, 소리 엔진 줄이기 — AUDIO-SPEC §8.2 #4)
    ///   · GachaRevealHigh: 높은 등급 소환 공개 (GachaResultPopup이 보냄, 인자 = ItemGrade 숫자 → 소리 엔진이 배경음을 줄인다)
    ///   · FtueFirstLampBeat / FtueFirstImpression: 튜토리얼 연출 표시 (FtueDirector가 켜고 끔 → BattleView가 읽음, §13.3)
    /// 보내는 곳 · 받는 곳이 아직 없으면 아무 일도 일어나지 않는다. 받는 쪽은 장면이 내려갈 때 구독을 푼다 (정적 이벤트).
    /// 매 프레임 보내지 않는다 (바뀔 때만).
    /// </summary>
    public static class AppSignals
    {
        /// <summary>잠자기 모드 (true = 잠듦). RaiseSleep이 보낸다</summary>
        public static event Action<bool> SleepChanged;

        /// <summary>높은 등급 소환 공개 (인자 = ItemGrade 숫자, 전설 이상). RaiseGachaRevealHigh가 보낸다</summary>
        public static event Action<int> GachaRevealHigh;

        /// <summary>마지막으로 보낸 잠자기 상태 (늦게 구독한 쪽이 지금 상태를 읽는다)</summary>
        public static bool IsAsleep { get; private set; }

        /// <summary>
        /// 튜토리얼 4단계 손가락이 불 밝히기를 가리키는 동안 true — 첫 불 밝히기만 0.2배로 0.6초 느리게 + 큰 배너 (FtueDirector가 켜고 끈다)
        /// </summary>
        public static bool FtueFirstLampBeat;

        /// <summary>튜토리얼 1단계 동안 true — 지킴이 명중 연출 ×1.3 + 반짝임 2개 (숫자 · 흔들림은 그대로)</summary>
        public static bool FtueFirstImpression;

        /// <summary>잠자기 모드가 바뀜을 알린다 (받는 쪽은 같은 값이 두 번 와도 견뎌야 한다 — 장면을 다시 읽으면 처음부터 다시 보낸다)</summary>
        public static void RaiseSleep(bool asleep)
        {
            IsAsleep = asleep;
            SleepChanged?.Invoke(asleep);
        }

        /// <summary>높은 등급 소환 공개를 알린다 (grade = (int)ItemGrade)</summary>
        public static void RaiseGachaRevealHigh(int grade) => GachaRevealHigh?.Invoke(grade);

        /// <summary>
        /// 상태 표시를 처음으로 되돌린다 (장면을 다시 읽거나 테스트가 끝날 때). 구독은 받는 쪽이 푸므로 건드리지 않는다
        /// </summary>
        public static void ResetState()
        {
            IsAsleep = false;
            FtueFirstLampBeat = false;
            FtueFirstImpression = false;
        }
    }
}
