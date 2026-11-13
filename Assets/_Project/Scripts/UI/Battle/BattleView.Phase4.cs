using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면의 4차 연출 (SPEC4 — RM-2 P4-3b). 다른 partial은 훅 한 줄씩만 부르고 상태는 여기에 둔다.
    ///   · FX-06 스킬 이름 · 대사 들여보내기 (CalloutGate): 설정 '스킬 이름'을 따르고, 창 안에서 기다린 높은 우선순위 대사를 0.6초 안에 띄운다.
    ///     첫 본 스킬은 도감(Codex.HasSeenSkill / MarkSkillSeen — 메타가 켜져 있을 때만)에 적는다.
    /// 새 [SerializeField] 참조는 모두 없어도 된다 (사용자 씬은 G3까지 다시 만들지 않는다).
    /// </summary>
    public partial class BattleView
    {
        [Header("4차 (RM-2 P4-3b — 없어도 된다)")]
        [Tooltip("패배 분석 고리 (CR-01)")]
        [SerializeField] DefeatAnalysisView _defeatView;
        [Tooltip("친구들의 응원 칩 (PRG-03)")]
        [SerializeField] CheerChipView _cheerChip;
        [Tooltip("길 잃은 반딧불 (NEW-03 — 확장, 콘텐츠로 꺼짐)")]
        [SerializeField] StrayFireflyView _firefly;

        /// <summary>패배 분석 고리가 결과 대기를 늘리는 전체 길이 (초, 실패 순간부터 — 비평 C-05)</summary>
        public const float DefeatEndPause = 4.8f;
        /// <summary>실패 배너가 끝나는 때 (연속 실패로 내려가는 M13 배너 1.2초, 아니면 1.6초)</summary>
        public const float DefeatRingAfterStepBack = 1.2f;
        public const float DefeatRingAfter = 1.6f;

        readonly DefeatAnalyzer _defeat = new();
        readonly BattleStatsRecorder _stats = new();
        /// <summary>전투 기록의 피격 주인 찾기 (불붙은 적 → 붙인 친구)</summary>
        readonly HitCredit _credit = new();
        /// <summary>맥락을 연 이벤트(기본 공격 · 시전 · 불 밝히기)가 온 시뮬레이터 진행 번호 — 2배속에서 앞 진행의 맥락이 다음 진행의 지속 피해를 가져가지 않게</summary>
        int _creditStep = -1;
        BattleSimulator _creditSim;
        float _defeatShowAt = -1f;
        float _bossHintAt = -1f;
        string _bossHintId;
        bool _battle4Started;

        readonly CalloutGate _gate = new();
        /// <summary>플레이 기록 stage_end dir[5] 채우기 (P4-7 PlayLogRecorder.DirectionDamage — 한 번 만든 대리자)</summary>
        System.Action<int[]> _fillDirections;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>kill_without_arrival 로그 (SPEC4 §13.5 j): 마지막으로 남긴 값 · 다음 확인 · 다음 정기 줄 (실제 시간)</summary>
        int _kwaLogged = -1;
        float _kwaCheckAt;
        float _kwaBeatAt;
        /// <summary>정기 줄 간격 (초) — 개발 플레이어 · 배치에서만 (에디터 콘솔은 값이 바뀔 때만)</summary>
        public const float KillArrivalBeat = 60f;
#endif
        /// <summary>설정 '스킬 이름' (GameSettings.Changed 때만 읽는다)</summary>
        bool _skillNames = true;
        int _gateToken;

        /// <summary>들여보내기 대기 칸에 들어간 대사 (CalloutGate는 하나만 기다리게 하므로 하나)</summary>
        struct GatedLine
        {
            public int Token;
            public int Slot;
            public int EchoId;
            public Vector2 Head;
            public bool HasHead;
            public string Text;
            public Color Accent;
            public bool Important;
        }

        GatedLine _gated;

        /// <summary>잠자기 모드인지 (쓰다듬기 · 응원 칩 · 반딧불이 쉰다)</summary>
        internal bool Asleep => _asleep;

        static readonly System.Func<string, bool> CodexHasSeen = id => Game.IsMetaActive && Game.Codex != null && Game.Codex.HasSeenSkill(id);
        static readonly System.Action<string> CodexMarkSeen = id =>
        {
            if (Game.IsMetaActive && Game.Codex != null) Game.Codex.MarkSkillSeen(id);
        };

        void PrepareCalloutGate()
        {
            _skillNames = GameSettings.SkillNames;
            _gate.Enabled = _skillNames;
            _gate.HasSeen = CodexHasSeen;
            _gate.MarkSeen = CodexMarkSeen;
        }

        void OnSettingsChangedBattle4()
        {
            _skillNames = GameSettings.SkillNames;
            _gate.Enabled = _skillNames;
        }

        void StartBattle4()
        {
            _battle4Started = true;
            BattleStatsPopup.Source = _stats;
            // 전투 기록 창 받는 곳 (R1 칸 StatsButton → UINav.OpenBattleStats — 칸은 P4-6)
            UINav.BattleStatsRequested += OpenBattleStats;
            // 플레이 기록 stage_end의 방향별 수선점 피해 (P4-7 요청 — StageEnded는 StageChanged의 비우기보다 먼저 온다)
            _fillDirections ??= FillDirectionDamage;
            PlayLogRecorder.DirectionDamage = _fillDirections;
        }

        void DestroyBattle4()
        {
            if (!_battle4Started) return;
            _battle4Started = false;
            UINav.BattleStatsRequested -= OpenBattleStats;
            if (BattleStatsPopup.Source == _stats) BattleStatsPopup.Source = null;
            if (_fillDirections != null && PlayLogRecorder.DirectionDamage == _fillDirections) PlayLogRecorder.DirectionDamage = null;
        }

        /// <summary>이번 스테이지 방향별 수선점 피해 (0 = 위쪽, 반시계 — DefeatAnalyzer 칸 순서). 할당 없음, 예외 없음</summary>
        void FillDirectionDamage(int[] slots)
        {
            if (slots == null) return;
            int count = Mathf.Min(slots.Length, DefeatAnalyzer.Slots);
            for (int i = 0; i < count; i++)
            {
                float value = _defeat.SlotDamage(i);
                slots[i] = !(value > 0f) ? 0 : value >= int.MaxValue ? int.MaxValue : (int)System.Math.Round(value);
            }
        }

        static void OpenBattleStats() => BattleStatsPopup.TryOpen();

        void OnStageChangedBattle4()
        {
            _gate.Reset();
            _gated = default;
            _defeat.Reset();
            _stats.ResetStage();
            _credit.Clear();
            _creditStep = -1;
            _defeatShowAt = -1f;
            _bossHintAt = -1f;
            _bossHintId = null;
            if (_defeatView != null) _defeatView.Hide();
            if (_shop != null) _shop.ResetCracks();
        }

        /// <summary>
        /// 실패 (CR-01): 튜토리얼이 끝났고 수선점 피해 자료가 있고 이 스테이지에서 10분 안에 안 보였으면 결과 대기를 4.8초로 늘리고,
        /// 실패 배너가 끝나는 때(1.2 / 1.6초) 고리를 띄운다. 고리가 없는 씬(빌더를 다시 돌리기 전)에서는 대기를 늘리지 않는다
        /// </summary>
        void OnStageEndedBattle4(BattleOutcome outcome)
        {
            if (!Game.IsReady || Game.Battle == null) return;
            int stage = _sim != null ? _sim.Level : Game.Battle.Stage;
            // 강해지기 '약한 방향 보강' 줄 (P4-1)이 읽는 마지막 실패 기록 — 클리어하면 비운다
            if (outcome == BattleOutcome.Cleared) LastDefeat.Clear();
            else if (outcome == BattleOutcome.Failed)
            {
                if (_defeat.HasData) LastDefeat.Record(stage, _defeat.TopSlot, _defeat.TopPercent);
                else LastDefeat.Clear();
            }
            if (outcome != BattleOutcome.Failed || _defeatView == null) return;
            float now = Time.unscaledTime;
            if (!_defeat.ShouldShow(FtueDone, stage, now)) return;
            _defeat.MarkShown(stage, now);
            Game.Battle.ExtendEndPause(DefeatEndPause);
            _defeatShowAt = now + (Game.Battle.LastEndSteppedBack ? DefeatRingAfterStepBack : DefeatRingAfter);
        }

        static bool FtueDone
        {
            get
            {
                var ftue = Game.Data != null && Game.Data.meta != null ? Game.Data.meta.ftue : null;
                return ftue == null || ftue.done || ftue.skipped;
            }
        }

        /// <summary>
        /// 보스 안내 줄 (PRG-02 §13.1): 그 보스에 안내가 있고 처음 만남이거나 다시 도전이면 보스 등장 배너 1초 뒤 알림 띠에 2.4초 (BossHintRule).
        /// 튜토리얼 중에는 없다. 처음 만남 표시는 보여 줄 때 PlayerData.flags에 적는다
        /// </summary>
        void OnBossSpawnedBattle4()
        {
            _bossHintAt = -1f;
            _bossHintId = null;
            if (_notice == null || _sim == null || !Game.IsReady || !Game.IsMetaActive || Game.MetaDb == null || Game.Battle == null) return;
            // BossSpawned는 시뮬레이터가 보스를 기억하기 전에 온다 (Sim.Boss가 아직 비어 있다) — 목록에서 찾는다
            var boss = FindBossState();
            string id = boss != null && boss.Def != null ? boss.Def.id : null;
            var hint = id != null ? Game.MetaDb.GetBossHint(id) : null;
            var flags = Game.Data != null ? Game.Data.flags : null;
            string key = BossHintRule.FlagKey(id);
            bool seen = flags != null && key != null && flags.Contains(key);
            bool tutorial = Game.Tutorial != null && Game.Tutorial.IsActive;
            if (!BossHintRule.ShouldShow(hint != null && !string.IsNullOrEmpty(hint.hint), seen, Game.Battle.ConsecutiveFails, tutorial)) return;
            _bossHintId = id;
            _bossHintAt = Time.unscaledTime + BossHintRule.Delay;
        }

        void ShowBossHint()
        {
            string id = _bossHintId;
            _bossHintId = null;
            if (_notice == null || id == null || Game.MetaDb == null || _sim == null || !_sim.BossSpawned) return;
            var boss = FindBossState();
            if (boss == null || boss.Def == null || boss.Def.id != id) return;
            var hint = Game.MetaDb.GetBossHint(id);
            if (hint == null || string.IsNullOrEmpty(hint.hint)) return;
            _notice.Show(boss.Def.name, hint.hint, boss.Def.sprite, Color.white, UITheme.Danger, BossHintRule.Duration);
            var flags = Game.Data != null ? Game.Data.flags : null;
            string key = BossHintRule.FlagKey(id);
            if (flags != null && key != null) flags.Add(key);
        }

        void TickBattle4(float dt)
        {
            float now = Time.unscaledTime;
            if (_gate.HasPending && _gate.TryTakePending(now, out var pending) && pending.Token == _gated.Token) SayGated();
            if (_defeatShowAt >= 0f && now >= _defeatShowAt)
            {
                _defeatShowAt = -1f;
                if (_defeatView != null && Game.Battle != null && Game.Battle.IsBetweenStages) _defeatView.Show(_defeat, GuardianName(_defeat.TopSlot));
            }
            if (_bossHintAt >= 0f && now >= _bossHintAt)
            {
                _bossHintAt = -1f;
                ShowBossHint();
            }
            if (_cheerChip != null) _cheerChip.Tick(_asleep);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TickKillArrivalLog(now);
#endif
            if (_firefly != null)
            {
                bool popup = UIManager.Instance != null && UIManager.Instance.HasOpenPopup;
                bool running = Game.Battle != null && !Game.Battle.IsBetweenStages && _sim != null && _sim.Boss == null && !UIFrame.SheetOpen;
                _firefly.Tick(running ? dt : 0f, _asleep, popup || !running, _sim != null && _sim.StageModifier == StageModifierType.Blackout);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// '[Verify] kill_without_arrival=N kills=M' (SPEC4 §13.5 j — V2 soak가 마지막 값을 읽는다): 1초마다 보고 값이 바뀌면 바로,
        /// 개발 플레이어 · 배치에서는 첫 처치와 그 뒤 60초마다 한 줄 더. 줄을 쓸 때만 글자를 만든다
        /// </summary>
        void TickKillArrivalLog(float now)
        {
            if (now < _kwaCheckAt) return;
            _kwaCheckAt = now + 1f;
            int kills = KillArrivalProbe.Kills;
            if (kills <= 0) return;
            int missed = KillArrivalProbe.WithoutArrival;
            bool beat = false;
#if !UNITY_EDITOR
            beat = now >= _kwaBeatAt;
#endif
            if (Application.isBatchMode && now >= _kwaBeatAt) beat = true;
            if (missed == _kwaLogged && !beat) return;
            _kwaLogged = missed;
            _kwaBeatAt = now + KillArrivalBeat;
            Debug.Log($"[Verify] kill_without_arrival={missed} kills={kills}");
        }
#endif

        /// <summary>그 칸 지킴이 이름 (비었으면 null → 빈 자리)</summary>
        string GuardianName(int slot)
        {
            var guardians = _sim != null ? _sim.Guardians : null;
            var state = guardians != null && slot >= 0 && slot < guardians.Count ? guardians[slot] : null;
            return state != null ? BattleUnitArt.DisplayName(state.FormId) : null;
        }

        /// <summary>
        /// 전투 기록 · 패배 분석에 이벤트를 넣는다 (BattleView.OnBattleEvent가 맥락을 맞춘 직후, 시험장이 열려 있어도). 할당 없음
        /// </summary>
        void RecordBattle4(in BattleEvent e)
        {
            if (_sim == null) return;
            if (!ReferenceEquals(_creditSim, _sim))
            {
                // 다른 시뮬레이터 (적 ID가 1부터 다시) — 불붙은 적 기록을 버린다
                _creditSim = _sim;
                _credit.Clear();
                _creditStep = -1;
            }
            float now = Time.unscaledTime;
            switch (e.Type)
            {
                case BattleEventType.EnemyStrike:
                    if (e.TargetId == -1) _defeat.AddShopStrike(e.Position, e.Value, e.Key);
                    break;
                // 맥락을 여는 이벤트 — 이 진행 번호 안에서만 맥락을 믿는다 (2배속 한 프레임에 진행 둘)
                case BattleEventType.GuardianAttack:
                case BattleEventType.EchoAttack:
                case BattleEventType.SkillCast:
                case BattleEventType.LampLit:
                    _creditStep = _sim.StepCount;
                    break;
                case BattleEventType.StatusApplied:
                    if (e.TargetId >= 0 && Mathf.RoundToInt(e.Value) == (int)SkillEffectType.DamageOverTime) _credit.NoteBurn(e.TargetId, e.SourceId);
                    break;
                case BattleEventType.EnemyHit:
                {
                    int source = _credit.Resolve(e.SourceId, e.TargetId, CreditContext, _sim.StepCount == _creditStep, _echoSource, _echoTarget, _castSource,
                        out bool burned);
                    if (ResolveUnit(source, out string hitForm, out bool hitEcho)) _stats.AddDamage(hitForm, hitEcho, e.Value, now);
                    // 불이 꺼진 틱(마지막 틱)까지 붙인 친구에게 — 그 뒤 맥락 없는 피격은 남의 몫이 아니다
                    if (burned && !IsBurning(e.TargetId)) _credit.Forget(e.TargetId);
                    break;
                }
                case BattleEventType.ShopHealed:
                case BattleEventType.EchoHealed:
                    if (ResolveUnit(e.SourceId, out string healForm, out bool healEcho)) _stats.AddHeal(healForm, healEcho, e.Value, now);
                    break;
                case BattleEventType.ShieldGained:
                    if (ResolveUnit(e.SourceId, out string shieldForm, out bool shieldEcho)) _stats.AddShield(shieldForm, shieldEcho, e.Value, now);
                    break;
                case BattleEventType.EnemyKilled:
                    if (ResolveUnit(e.SourceId, out string killForm, out bool killEcho)) _stats.AddKill(killForm, killEcho, now);
                    _credit.Forget(e.TargetId);
                    break;
                case BattleEventType.EnemyEnraged:
                    if (_hud != null && IsBossId(e.TargetId)) _hud.OnBossEnraged();
                    break;
            }
        }

        EnemyState FindBossState()
        {
            var enemies = _sim != null ? _sim.Enemies : null;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var enemy = enemies[i];
                if (enemy != null && enemy.Alive && enemy.IsBoss) return enemy;
            }
            return null;
        }

        bool IsBossId(int enemyId)
        {
            var enemies = _sim.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].Id == enemyId) return enemies[i].IsBoss;
            }
            return false;
        }

        /// <summary>
        /// 전투 기록이 보는 맥락 (TrackContext가 프레임이 바뀐 맥락은 이미 닫았다 — 진행 번호는 HitCredit.Resolve의 sameStep이 따로 본다).
        /// 피격의 주인: SourceId 칸(지킴이 기본 공격) → 그 칸, 잔향이 기본 공격의 그 대상 → 그 잔향이, 시전 → 시전자, 불 밝히기 → 없음, 지속 피해 → 불을 붙인 친구
        /// </summary>
        HitCreditContext CreditContext => _context switch
        {
            Context.Basic => HitCreditContext.Basic,
            Context.EchoBasic => HitCreditContext.EchoBasic,
            Context.Cast => HitCreditContext.Cast,
            Context.Lamp => HitCreditContext.Lamp,
            _ => HitCreditContext.None,
        };

        /// <summary>그 적이 아직 살아서 불타고 있는지 (불붙은 적 기록을 지울 때만 — 목록을 한 번 돈다)</summary>
        bool IsBurning(int enemyId)
        {
            var enemies = _sim.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var s = enemies[i];
                if (s != null && s.Id == enemyId) return s.Alive && s.BurnTimer > 0f;
            }
            return false;
        }

        /// <summary>출처 ID → 형태 (지킴이 칸 0~4 · 잔향이 ID — 토큰 · 빛으로 풀린 잔향이 · 수선점(−1)은 false)</summary>
        bool ResolveUnit(int id, out string formId, out bool echo)
        {
            formId = null;
            echo = false;
            if (id < 0 || _sim == null) return false;
            if (id < BattleSimulator.EchoIdBase)
            {
                var guardians = _sim.Guardians;
                var g = id < guardians.Count ? guardians[id] : null;
                formId = g != null ? g.FormId : null;
                return formId != null;
            }
            var echoes = _sim.Echoes;
            for (int i = 0; i < echoes.Count; i++)
            {
                var s = echoes[i];
                if (s == null || s.Id != id) continue;
                if (s.IsToken) return false;
                formId = s.FormId;
                echo = true;
                return formId != null;
            }
            return false;
        }

        /// <summary>
        /// 시전 대사를 들여보낸다 (FX-06). true = 지금 말해도 된다. 창 안의 높은 우선순위(큰 스킬 · 전설 이상 · 처음 보는 스킬)는 대기 칸에 맡기고 false
        /// (0.6초 안에 창이 열리면 TickBattle4가 띄운다). 설정이 꺼졌거나 같은 스킬 10초 안 · 낮은 우선순위면 false
        /// </summary>
        bool AdmitCastLine(in BattleEvent e, string line, bool big, GuardianSlotView guardian, EchoView echo, in CasterPoint caster, Color accent)
        {
            bool p0 = big || IsLegendaryCaster(guardian, echo, caster.FormId);
            int priority = _gate.PriorityFor(e.Key, p0);
            int token = ++_gateToken;
            if (_gate.TryAdmit(e.Key, priority, Time.unscaledTime, token, out bool queued)) return true;
            if (!queued) return false;
            _gated = new GatedLine
            {
                Token = token,
                Slot = guardian != null ? guardian.Slot : -1,
                EchoId = guardian == null ? e.SourceId : -1,
                Head = caster.Head,
                HasHead = guardian == null && echo == null && caster.Dissolved,
                Text = line,
                Accent = accent,
                Important = big,
            };
            return false;
        }

        /// <summary>대기 칸에서 꺼낸 대사를 지금 띄운다 (말할 몸이 없어졌으면 풀린 자리에, 그것도 없으면 버린다)</summary>
        void SayGated()
        {
            var g = _gated;
            _gated = default;
            if (_callouts == null || string.IsNullOrEmpty(g.Text)) return;
            bool said = false;
            if (g.Slot >= 0 && g.Slot < _guardians.Length && _guardians[g.Slot] != null && _guardians[g.Slot].State != null)
                said = _callouts.Say(_guardians[g.Slot], g.Text, g.Accent, g.Important);
            else if (g.EchoId >= 0 && _echoes.TryGet(g.EchoId, out var view))
            {
                if (!view.IsToken) said = _callouts.Say(view, g.Text, g.Accent, g.Important);
            }
            else if (g.HasHead) said = _callouts.Say(g.Head, g.Text, g.Accent, g.EchoId, g.Important);
            if (said) _fx.NoteSkillNotice();
        }
    }

    /// <summary>
    /// ATK-09 실행 증명 계수 (SPEC4 §13.5 j — P4-3b 계수, P4-9 검사): 처치 꽃이 터지는 순간(EnemyView.TickExit)마다 그 적에게 앞 Window(0.30초,
    /// 그 적의 연출 시계) 안에 닿은 명중 반응(EnemyView.ApplyImpact)이 없었으면 WithoutArrival을 센다 — 투사체가 닿기 전에 처치 연출이 먼저 터진 것.
    /// BattleView가 개발 빌드에서 '[Verify] kill_without_arrival=N' 줄로 남긴다. 순수 정수 계수 (할당 없음, 세션 동안 쌓인다)
    /// </summary>
    public static class KillArrivalProbe
    {
        public const float Window = 0.30f;
        /// <summary>실수 덧셈 오차 (창 끝에 딱 맞게 닿은 명중은 닿은 것으로)</summary>
        const float Epsilon = 0.0005f;

        /// <summary>센 처치 수 (처치 꽃이 터진 수)</summary>
        public static int Kills { get; private set; }
        /// <summary>앞 0.30초 안에 닿은 명중이 없던 처치 수 (기대 0)</summary>
        public static int WithoutArrival { get; private set; }

        /// <summary>마지막 명중이 sinceArrival초 전이면 '닿지 않은 처치'인지 (NaN · 음수 아닌 큰 값 = 닿은 적 없음)</summary>
        public static bool IsWithoutArrival(float sinceArrival) => !(sinceArrival <= Window + Epsilon);

        /// <summary>처치 꽃 하나 (sinceArrival = 그 순간 − 마지막 명중 반응, 그 적의 시계)</summary>
        public static void Note(float sinceArrival)
        {
            Kills++;
            if (IsWithoutArrival(sinceArrival)) WithoutArrival++;
        }

        public static void Reset()
        {
            Kills = 0;
            WithoutArrival = 0;
        }
    }
}
