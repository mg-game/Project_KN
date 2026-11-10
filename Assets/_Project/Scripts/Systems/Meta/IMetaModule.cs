namespace IdleGame
{
    /// <summary>
    /// 4차 메타 모듈 (도감 · 기록 · 이벤트 …, SPEC4 §2.4). MetaSession이 Game.SetupMeta가 넣은 순서대로 부른다. 구현은 예외를 던지지 않는다 —
    /// 그래도 예외가 나면 MetaSession이 그 모듈만 끄고 한 번 기록한다 (메타 전체 킬 스위치를 건드리지 않게).
    /// 부팅 순서: MetaSession.Connect → Connect → (기능 개방 평가 · 접속 카운터 · 비운 시간 계산) → BeforeAwayApplied(비운 초, true)
    ///   → 바구니에 비운 시간 넣기 → 접속 요청(복귀 · 출석) → AfterStartup(true) → 튜토리얼 → RegisterRedDots.
    /// 복귀(OnResume)도 같은 차례에 boot = false. 날이 바뀌면 OnDayChanged, 매 프레임 Tick(진행 기록 Flush 뒤, 빨간 점 Refresh 앞).
    /// </summary>
    public interface IMetaModule
    {
        /// <summary>Start에서 한 번 (다른 시스템 이벤트 구독 · 훅 넣기는 여기서)</summary>
        void Connect();

        /// <summary>비운 시간을 바구니에 넣기 직전 (다시 만난 날의 24시간 상한 등). boot = 부팅이면 true, 복귀면 false</summary>
        void BeforeAwayApplied(long awaySeconds, bool boot);

        /// <summary>접속 요청(복귀 화면 · 출석부)을 큐에 넣은 직후 (다시 만난 날 / 이벤트 시작 / 동화책 …)</summary>
        void AfterStartup(bool boot);

        /// <summary>DailyClock.DayChanged (MetaSession이 한 곳에서 나눠 준다, 접속 중에만)</summary>
        void OnDayChanged(string dayKey);

        /// <summary>매 프레임 (실제 시간), 할당 없이. 1초 묶음은 모듈이 스스로 센다</summary>
        void Tick(float unscaledDeltaTime);

        /// <summary>MetaSession.RegisterRedDots 안에서 자기 잎을 등록한다 (RedDotPaths 4차 상수)</summary>
        void RegisterRedDots(RedDotSystem dots);
    }
}
