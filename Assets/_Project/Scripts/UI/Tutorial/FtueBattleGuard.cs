using System;

namespace IdleGame.UI
{
    /// <summary>
    /// FTUE 1-1~1-3 "질 수 없는 전투" (FTUE-01, SPEC4 §13.3) — 전투 쪽 정식 훅으로 넘긴다 (RM-2 S-9: P4-3b 보호 · 게이지 채우기가 들어온 뒤).
    ///   · 보호(적 체력 60% · 수선점 최소 1)는 시뮬레이터가 건다: BattleSystem.ProtectionResolver → BattleSetup.Protection
    ///     (새로 나오는 적의 최대 체력 × 0.6 — 보스 · 분열체 포함 한 번만, 수선점은 실패 판정 전에 바닥 1). 게임에서는 MetaSession.Connect가
    ///     같은 규칙의 resolver를 넣는다. 붙을 때 resolver가 비어 있으면(메타 연결 전 · 테스트) 이 가드가 같은 규칙의 것을 넣고, 뗄 때 자기 것만 뺀다.
    ///   · 켜지는 조건 (IsProtecting): 메타 켜짐 + (Tutorial.WantsBattleProtection 또는 인트로 중) + 스테이지 ≤ 3 — 던전은 시뮬레이터가 늘 끈다.
    ///   · 첫 불 밝히기(FTUE 4단계 ForceLampFull): BattleSystem.ForceLampFull (메인 스테이지 · 전투 중 · 게이지 &lt; 1일 때만).
    /// 리플렉션 · 전투 이벤트 안에서 적 체력과 수선점 최대 내구도를 고치던 임시 경로는 지웠다 (정식 보호와 겹쳐 적이 두 번 약해지던 것도 끝).
    /// 공개 API는 예전 그대로 둔다 (SPEC4 §13.3). 할당 없음 (resolver 대리자는 만들 때 한 번).
    /// </summary>
    public sealed class FtueBattleGuard
    {
        /// <summary>FTUE 동안 적 체력 배율 (BattleProtection.Tutorial과 같은 값)</summary>
        public const float EnemyHpScale = 0.6f;
        /// <summary>보호하는 마지막 스테이지 (1-3, MetaSession.TutorialProtectionMaxStage와 같은 값)</summary>
        public const int MaxProtectedStage = 3;
        /// <summary>보호 중 수선점 최소 내구도</summary>
        public const float ShopFloorHp = 1f;

        BattleSystem _battle;
        TutorialSystem _tutorial;
        readonly Func<int, BattleProtection> _resolver;

        public FtueBattleGuard() => _resolver = Resolve;

        /// <summary>끄면 보호를 요청하지 않고 게이지도 채우지 않는다</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>메타가 켜져 있을 때만 (테스트는 Game 없이 돌리므로 끈다)</summary>
        public bool RequireMetaActive { get; set; } = true;

        bool MetaOk => !RequireMetaActive || Game.IsMetaActive;

        /// <summary>
        /// 지금 보호가 켜져 있는지. 인트로(0단계)를 보는 동안에도 뒤에서 1-1이 돌므로 함께 지킨다
        /// (WantsBattleProtection은 BattleProtection 단계부터라는 계약 그대로 두고, 여기서만 넓힌다 — MetaSession의 resolver와 같은 규칙).
        /// </summary>
        public bool IsProtecting => _battle != null && Wants(_battle.Stage);

        /// <summary>붙을 때 resolver가 비어 있어 이 가드가 넣은 것을 쓰는 중인지 (메타 연결이 넣었으면 false)</summary>
        public bool OwnsResolver => _battle != null && ReferenceEquals(_battle.ProtectionResolver, _resolver);

        bool Wants(int stage) =>
            Enabled && _tutorial != null && MetaOk && stage <= MaxProtectedStage
            && (_tutorial.WantsBattleProtection || (_tutorial.IsActive && _tutorial.StepIndex == 0));

        /// <summary>이 가드의 resolver (스테이지 번호 → 보호). BattleSystem이 판을 짤 때와 매 Tick 부른다 — 할당 없음</summary>
        BattleProtection Resolve(int stage) =>
            Wants(stage) ? new BattleProtection { Enabled = true, EnemyHpScale = EnemyHpScale, ShopFloorHp = ShopFloorHp } : BattleProtection.Off;

        /// <summary>전투에 붙는다 (FtueDirector.Start). 전투에 보호 resolver가 없으면 같은 규칙의 것을 넣는다</summary>
        public void Attach(BattleSystem battle, TutorialSystem tutorial)
        {
            Detach();
            _battle = battle;
            _tutorial = tutorial;
            if (_battle != null && _battle.ProtectionResolver == null) _battle.ProtectionResolver = _resolver;
        }

        /// <summary>떼어 낸다 (이 가드가 넣은 resolver만 뺀다 — 메타 연결이 넣은 것은 그대로)</summary>
        public void Detach()
        {
            if (OwnsResolver) _battle.ProtectionResolver = null;
            _battle = null;
            _tutorial = null;
        }

        /// <summary>게이지를 채울 수 있는 빌드인지 — 전투 훅(BattleSystem.ForceLampFull)이 있어 늘 그렇다 (예전 API 그대로)</summary>
        public static bool CanFillLamp => true;

        /// <summary>
        /// 첫 불 밝히기 안내용: 메인 스테이지 전투 중이고 아직 게이지가 덜 찼으면 가득 채운다 (BattleSystem.ForceLampFull). 채웠으면 true
        /// </summary>
        public bool TryFillLamp()
        {
            if (!Enabled || _battle == null || !MetaOk) return false;
            var sim = _battle.Sim;
            if (sim == null || sim.Mode != BattleMode.Stage || sim.Outcome != BattleOutcome.Running || sim.CanLightLamp) return false;
            _battle.ForceLampFull();
            return sim.CanLightLamp;
        }
    }
}
