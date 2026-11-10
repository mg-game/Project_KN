using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 기본 공격 · 피격 · 처치 이벤트 연출 (ATK-01 / ATK-02 / ATK-09 / FX-01 / FX-03 / FX-JUICE E1~E8).
    ///   지킴이 공격: 계보 투사체를 쏘고 비행 시간(거리·성향·배속)을 맥락에 남긴다 → 뒤따르는 치명타·피격 숫자·흰 번쩍임·넉백·처치는 그 시간에 맞춘다.
    ///      몸은 BattleFx.LastWindup(W)만큼 웅크렸다가 총구 불꽃과 같은 순간에 놓는다 (GuardianSlotView.PlayAttack(target, W)).
    ///   잔향이 공격: 근접 베기(바로) 또는 작은 투사체 (같은 맥락 규칙). 몸은 눌렸다가 한 번 출렁인다.
    ///   피격: 등급(일반/강타/치명타/스킬/불빛/지속 피해)별 반응을 적에게 예약하고 (공격자 강조색 · 여러 번 닿는 스킬은 첫 명중만 멈춤 ·
    ///      잔향이 기본 공격은 가벼운 반응 HitFeel.ForEcho),
    ///      숫자 문법(BattleFx.Numbers)으로 숫자를 띄운다 — 뜻은 치명타 &gt; 표식 &gt; 스킬 &gt; 불 밝히기 &gt; 보스 &gt; 강타 &gt; 일반 (AttackFxCatalog.NumberKind),
    ///      리듬 강조 명중(BattleFx.LastShotAccent)의 숫자는 ×1.2. 맥락 없는 명중도 보이는 적을 넘겨 같은 대상 90ms 규칙을 탄다.
    ///      스킬 명중은 코어 이음매 BattleFx.SkillHit (코어 기본 명중 + show 방식별 결).
    ///   처치: 적에게 아직 날아오는 투사체가 있으면 가장 늦은 도착 + 0.02초까지 버틴다 (0.14~0.30초) — 마지막 일격이 허공을 치지 않게.
    ///      처치한 일격의 등급을 적에 남기고(MarkKilled) 연속·한꺼번에 처치를 센다(NoteKill). 기본 공격으로 처치하거나 치명타를 낸 지킴이는 통 뛰며 기뻐한다.
    /// 시뮬레이터 순서: GuardianAttack/EchoAttack → (CriticalHit) → (ShieldBroken) → EnemyHit → (EnemyKilled), 모두 같은 프레임.
    /// </summary>
    public partial class BattleView
    {
        /// <summary>지킴이 기본 공격 맥락: 투사체 비행 시간 / 명중 등급 / 날아온 방향 / 계보 강조색 (치명타 물듦) / 리듬 강조 명중 (숫자 ×1.2)</summary>
        float _basicFlight;
        HitTier _basicTier;
        Vector2 _basicDir;
        Color _basicAccent;
        bool _basicRhythm;

        /// <summary>잔향이 기본 공격 맥락 (피격 SourceId가 -1이라 대상 ID로 맞춘다)</summary>
        int _echoSource = -1;
        int _echoTarget = -1;
        float _echoFlight;
        Vector2 _echoDir;
        Color _echoAccent;
        bool _echoRhythm;

        /// <summary>이번 프레임 마지막 일격 (처치 등급 = 처치한 일격의 등급, 치명타면 Crit)</summary>
        int _blowFrame = -1;
        int _blowTarget = -1;
        HitTier _blowTier;

        /// <summary>시전 하나에서 이미 맞은 적 (여러 번 닿는 스킬은 첫 명중만 대상을 멈춘다 — E6)</summary>
        readonly int[] _castHitIds = new int[32];
        int _castHitCount;
        int _castHitFrame = -1;
        int _castHitSource = int.MinValue;
        string _castHitSkill;

        /// <summary>매 프레임: 배속·스테이지 기믹을 연출에 알린다 (2배속 비행 0.6배, 비/정전 연출)</summary>
        void TickAttackPace()
        {
            _fx.SetBattlePace(Game.Battle != null ? Game.Battle.Speed : 1, _sim != null ? _sim.StageModifier : StageModifierType.None);
        }

        // ───────────── 공격 ─────────────

        void OnGuardianAttack(BattleEvent e)
        {
            int slot = e.SourceId;
            if (slot < 0 || slot >= _guardians.Length) return;
            BeginBasic(slot, e.TargetId);
            _basicFlight = 0f;
            _basicTier = HitTier.Normal;
            _basicDir = Vector2.zero;
            _basicAccent = UITheme.Cream;
            _basicRhythm = false;
            var guardian = _guardians[slot];
            if (guardian.State == null || !_enemies.TryGet(e.TargetId, out var target)) return;

            var style = guardian.State.Stats.style;
            var from = guardian.MuzzlePosition;
            var request = new AttackFxRequest
            {
                From = from,
                Target = target,
                LineageId = guardian.State.LineageId,
                Style = style,
                FormTier = guardian.FormTier,
                Mythic = guardian.IsMythic,
                // 튜토리얼 첫 전투 첫인상 (FTUE-01): 명중 ×1.3 + 반짝임 2 (숫자 · 흔들림 그대로)
                Impression = AppSignals.FtueFirstImpression,
            };
            float flight = _fx.GuardianShot(request);
            // 웅크림 W 동안 몸을 낮췄다가 총구 불꽃과 같은 순간에 놓는다 (도착 시각은 그대로 flight)
            guardian.PlayAttack(target.HitPoint, _fx.LastWindup);
            _basicRhythm = _fx.LastShotAccent;
            _basicFlight = flight;
            _basicTier = AttackFxCatalog.Style(style).Tier;
            _basicDir = Direction(from, target.HitPoint);
            _basicAccent = AttackFxCatalog.Look(guardian.State.LineageId).Accent;
            target.ExpectImpact(flight);
            // 공격 이벤트는 피해 전에 오므로 지금 체력이 맞기 전 체력 (처형 판정과 같다)
            TryExecuteKiss(target, guardian.Passive, flight);
            // 정전: 빛 특성 계보가 닿는 자리의 어둠에 구멍 (ATK-14)
            TryLightHole(guardian.State.LineageId, guardian.State.FormId, target, flight);
        }

        void OnEchoAttack(BattleEvent e)
        {
            BeginEchoBasic(e.SourceId, e.TargetId);
            bool hasView = _echoes.TryGet(e.SourceId, out var echo);
            var state = hasView ? echo.State : FindEcho(e.SourceId);
            var style = state != null ? state.Stats.style : CombatStyle.Assault;
            bool token = state != null && state.IsToken;
            if (hasView) echo.PlayAttack(token || !AttackFxCatalog.Style(style).Ranged);
            if (!_enemies.TryGet(e.TargetId, out var target)) return;

            Vector2 from;
            if (hasView) from = echo.BodyPoint;
            else if (state != null) from = _mapper.WorldToField(state.Position) + Vector2.up * (EchoBodyLift * _mapper.ActorScale);
            else from = target.HitPoint;

            string lineage = token ? "jjakjjak" : state != null ? state.LineageId : null;
            var request = new AttackFxRequest
            {
                From = from,
                Target = target,
                LineageId = state != null ? state.LineageId : null,
                Style = style,
                FormTier = hasView ? echo.FormTier : 0,
                Mythic = hasView && echo.IsMythic,
                Echo = true,
                Token = token,
                Glow = hasView ? echo.GlowColor : UITheme.EchoGlow,
            };
            float delay = _fx.EchoAttack(request);
            _echoRhythm = _fx.LastShotAccent;
            _echoFlight = delay;
            _echoDir = Direction(from, target.HitPoint);
            _echoAccent = AttackFxCatalog.Look(lineage).Accent;
            target.ExpectImpact(delay);
            if (hasView) TryExecuteKiss(target, echo.Passive, delay);
            if (state != null && !token) TryLightHole(state.LineageId, state.FormId, target, delay);
        }

        void BeginEchoBasic(int echoId, int target)
        {
            EndContext();
            _context = Context.EchoBasic;
            _contextFrame = Time.frameCount;
            _echoSource = echoId;
            _echoTarget = target;
            _echoFlight = 0f;
            _echoDir = Vector2.zero;
            _echoAccent = UITheme.Cream;
            _echoRhythm = false;
        }

        // ───────────── 피격 ─────────────

        void OnEnemyHit(BattleEvent e)
        {
            int id = EnemyId(e);
            bool visible = _enemies.TryGet(id, out var view);
            Vector2 point;
            if (visible) point = view.HitPoint;
            else if (e.Position != Vector2.zero) point = _mapper.WorldToField(e.Position) + Vector2.up * 40f;
            else return;
            bool boss = visible && view.IsBoss;

            // 결과가 닿는 순간과 등급: 기본 공격(투사체·베기 — 명중 그림은 이미 예약됨) / 스킬(그 연출이 닿는 순간, 코어 이음매 SkillHit) /
            // 불 밝히기(빛 물결이 닿는 순간) / 지속 피해·그 밖(바로)
            float delay;
            HitTier tier;
            Vector2 direction;
            var accent = UITheme.Cream;
            var numberTint = default(Color);
            bool allowFreeze = true;
            bool basic = false;
            // 잔향이 기본 공격: 적 반응도 가볍게 (번쩍임 절반 · 넉백 0.6배 · 멈춤 없음 — HitFeel.ForEcho, 명중 그림의 0.65배와 짝)
            bool echo = false;
            // 리듬 강조 명중(사슬 3번째마다 풀 명중)은 숫자도 1.2배로 툭 (E6)
            float numberScale = 1f;
            bool fresh = _contextFrame == Time.frameCount;
            if (fresh && _context == Context.Basic && e.SourceId == _basicSlot && id == _basicTarget)
            {
                delay = _basicFlight;
                tier = _basicTier;
                direction = _basicDir;
                accent = _basicAccent;
                basic = true;
                if (_basicRhythm) numberScale = AttackFxCatalog.RhythmNumberScale;
            }
            else if (fresh && _context == Context.EchoBasic && id == _echoTarget)
            {
                delay = _echoFlight;
                tier = HitTier.Normal;
                direction = _echoDir;
                accent = _echoAccent;
                echo = true;
                if (_echoRhythm) numberScale = AttackFxCatalog.RhythmNumberScale;
            }
            else if (InCast)
            {
                delay = CastDelay(id);
                bool tick = _castStyle == SkillVfxStyle.Ground;
                tier = tick ? HitTier.Tick : HitTier.Skill;
                direction = Direction(_castFrom, point);
                accent = CastAccent();
                numberTint = accent;
                allowFreeze = FirstCastHit(id);
                _fx.SkillHit(new SkillHitFx
                {
                    Point = point,
                    Target = visible ? view : null,
                    Main = _castLook.Main,
                    Accent = _castLook.Accent,
                    Style = _castStyle,
                    Shard = _castLook.Particle,
                    Direction = direction,
                    Delay = delay,
                    Boss = boss,
                    Tick = tick,
                });
            }
            else if (fresh && _context == Context.Lamp)
            {
                delay = _fx.LampWaveDelay(visible ? view.Rect.anchoredPosition : point);
                tier = HitTier.Lamp;
                direction = Direction(_shop.FieldPosition, point);
                accent = UITheme.Lamp;
                _fx.LampHit(point, delay, visible ? view : null);
            }
            else
            {
                delay = 0f;
                bool dot = visible && view.IsBurning;
                tier = dot ? HitTier.Tick : HitTier.Normal;
                direction = Vector2.zero;
                // 보이는 적이면 넘긴다: 같은 대상 90ms 규칙(TryFullImpact)이 맥락 없는 명중에도 걸리고 명중 층이 그 몸을 따라간다
                if (!dot) _fx.Hit(point, UITheme.Cream, boss, 0f, visible ? view : null);
            }

            if (visible) view.QueueImpact(delay, tier, direction, accent, allowFreeze, echo);

            // 숫자: 적 보호막이 막은 만큼은 "막음 N"(소프트 블루), 나머지는 뜻별 색 (치명타는 이미 금색으로 떴다).
            // 늦게 닿는 숫자는 닿는 순간까지 적을 따라가 그 자리에서 뜬다 (살아남은 적이 그새 걸어가도 몸 위에)
            float blocked = visible ? view.ConsumeShield(e.Value) : 0f;
            if (blocked >= 0.5f) _fx.AnchorNumber(_fx.BlockNumber(id, point + new Vector2(0f, 62f), blocked, delay), visible ? view : null);
            bool crit = IsCritEcho(id, e.Value);
            RememberBlow(id, crit ? HitTier.Crit : tier);
            if (crit)
            {
                // 기본 공격 치명타: 쏜 지킴이가 닿는 순간 통 뛰며 기뻐한다 (E8d)
                if (basic) CelebrateGuardian(_basicSlot, delay);
                return;
            }
            bool marked = visible && view.State != null && view.State.MarkTimer > 0f;
            var kind = AttackFxCatalog.NumberKind(tier, marked, boss);
            int number = _fx.DamageNumber(id, point + new Vector2(0f, 34f), e.Value - blocked, kind, delay, numberScale,
                kind == DamageNumberKind.Skill ? numberTint : default);
            _fx.AnchorNumber(number, visible ? view : null);
            RememberHitNumber(id, e.Value, number);
        }

        /// <summary>이번 프레임 이 적에게 마지막으로 닿은 일격의 등급 (처치 등급)</summary>
        void RememberBlow(int enemyId, HitTier tier)
        {
            _blowFrame = Time.frameCount;
            _blowTarget = enemyId;
            _blowTier = tier;
        }

        /// <summary>처치한 일격의 등급: 같은 프레임 마지막 일격 → 지금 맥락 (스킬 · 불 밝히기 · 기본 공격) → 일반</summary>
        HitTier KillingBlow(int enemyId)
        {
            if (_blowFrame == Time.frameCount && _blowTarget == enemyId) return _blowTier;
            if (InCast) return HitTier.Skill;
            if (_contextFrame != Time.frameCount) return HitTier.Normal;
            switch (_context)
            {
                case Context.Lamp: return HitTier.Lamp;
                case Context.Basic: return enemyId == _basicTarget ? _basicTier : HitTier.Normal;
            }
            return HitTier.Normal;
        }

        /// <summary>시전자 계보 강조색 (스킬 숫자 물듦 · 스킬 명중 반응). 모르면 스킬 강조색</summary>
        Color CastAccent()
        {
            string lineage = null;
            if (_castSource >= BattleSimulator.EchoIdBase)
            {
                if (_echoes.TryGet(_castSource, out var echo) && echo.State != null) lineage = echo.State.LineageId;
            }
            else if (_castSource >= 0 && _castSource < _guardians.Length && _guardians[_castSource].State != null) lineage = _guardians[_castSource].State.LineageId;
            return AttackFxCatalog.HasLook(lineage) ? AttackFxCatalog.Look(lineage).Accent : _castLook.Accent;
        }

        /// <summary>이번 시전에서 이 적이 처음 맞는지 (빗방울·연쇄·궤도처럼 여러 번 닿으면 첫 명중만 대상 정지)</summary>
        bool FirstCastHit(int enemyId)
        {
            if (_castHitFrame != _contextFrame || _castHitSource != _castSource || !ReferenceEquals(_castHitSkill, _castSkill))
            {
                _castHitFrame = _contextFrame;
                _castHitSource = _castSource;
                _castHitSkill = _castSkill;
                _castHitCount = 0;
            }
            for (int i = 0; i < _castHitCount; i++)
            {
                if (_castHitIds[i] == enemyId) return false;
            }
            if (_castHitCount < _castHitIds.Length) _castHitIds[_castHitCount++] = enemyId;
            return true;
        }

        /// <summary>지킴이 축하 (E8d, 지킴이마다 2.5초에 한 번): delay 뒤 6px 통 뛰고 머리 위 반짝임</summary>
        void CelebrateGuardian(int slot, float delay)
        {
            if (slot < 0 || slot >= _guardians.Length) return;
            var guardian = _guardians[slot];
            if (guardian == null || guardian.State == null) return;
            if (guardian.Celebrate(delay)) _fx.CelebrationGlint(guardian.HeadPoint, delay);
        }

        // ───────────── 처치 ─────────────

        /// <summary>
        /// 처치 보류 (ATK-09): 기본 공격 투사체가 아직 날아오면 가장 늦은 도착 + 0.02초(0.14~0.30), 스킬은 그 연출이 닿는 순간,
        /// 불 밝히기는 빛 물결이 닿는 순간까지 버틴 뒤 처치 그림이 터진다. 보스 처치는 그 터지는 순간에 연출 전체 정지 + 흔들림 + 전장 번쩍임
        /// (EnemyViewPool → BattleFx.EnemyDeath → BossKillMoment — 날아가는 투사체가 아니라 닿는 순간에 멈춘다).
        /// 처치한 일격의 등급(치명타·스킬 처치는 화면 킥)을 적에 남기고, 연속·한꺼번에 처치를 센다 (BattleFx.NoteKill — 알림·마지막 일격 숫자).
        /// 지킴이 기본 공격이 처치했으면 그 지킴이가 처치 꽃이 터지는 순간 통 뛴다.
        /// </summary>
        void OnEnemyKilled(BattleEvent e)
        {
            int id = EnemyId(e);
            float hold = 0f;
            bool boss = e.Value >= 0.5f;
            bool visible = _enemies.TryGet(id, out var view);
            var byTier = KillingBlow(id);
            if (visible)
            {
                float incoming = view.IncomingRemaining;
                if (incoming > 0f) hold = AttackFxCatalog.KillHold(incoming);
                if (InCast) hold = Mathf.Max(hold, CastDelay(id));
                else if (_contextFrame == Time.frameCount && _context == Context.Lamp) hold = Mathf.Max(hold, _fx.LampWaveDelay(view.Rect.anchoredPosition) + 0.02f);
                boss |= view.IsBoss;
            }
            _enemies.MarkKilled(id, hold, byTier);
            _hud.OnKill();

            // 처치를 센다 (처치 꽃이 터지는 순간 = 버티는 시간 뒤)
            float bloom = visible ? view.KillDelay : Mathf.Max(hold, EnemyView.KillHold);
            Vector2 at;
            if (visible) at = view.HitPoint;
            else at = e.Position != Vector2.zero ? _mapper.WorldToField(e.Position) + Vector2.up * 40f : Vector2.zero;
            _fx.NoteKill(id, at, bloom, boss, byTier);
            if (_contextFrame == Time.frameCount && _context == Context.Basic && id == _basicTarget) CelebrateGuardian(_basicSlot, bloom);
            // 치명타 처치 (P2): 꽃이 터지는 순간 몸에 분홍/하늘 인쇄 어긋남 ±3px 50ms (1초에 한 번 · 번쩍임 줄이기 · 낮음 품질은 없음)
            if (visible && !boss && byTier == HitTier.Crit) _fx.Misprint(view.Body, bloom, 3f, 0.05f, 0.4f);

            if (!boss)
            {
                // 처치 보상 반짝: 5마리째마다 단추가 상단 바로 (분열로 생긴 작은 적은 세지 않는다)
                if (visible && view.State != null && !view.State.IsSplit) RewardKill(view, Mathf.Max(hold, EnemyView.KillHold));
                return;
            }
            // 보스 처치 (T5): 처치 그림·별 폭발과 같은 순간에 전체 정지 + 느린 화면 + 흔들림 + 전장 번쩍임 —
            // 보이는 보스는 처치 연출(BattleFx.EnemyDeath)이 그 순간을 예약하고, 안 보이면 바로. 작은 리본은 지금
            if (!visible) _fx.BossKillMoment(0f);
            ShowBossDefeated(visible ? view : null);
        }

        // ───────────── 불 밝히기 ─────────────

        /// <summary>불 밝히기 뒤 같은 프레임의 피해는 빛 물결 맥락 (적마다 물결이 닿는 순간에 명중)</summary>
        void BeginLampContext()
        {
            EndContext();
            _context = Context.Lamp;
            _contextFrame = Time.frameCount;
        }

        static Vector2 Direction(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            return delta.sqrMagnitude > 0.01f ? delta.normalized : Vector2.zero;
        }
    }
}
