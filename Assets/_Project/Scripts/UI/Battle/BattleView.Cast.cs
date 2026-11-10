using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 이벤트 맥락: 시뮬레이터는 시전/공격 하나의 결과(피격·치명타·상태·처치·보호막·회복)를 같은 프레임에 연달아 보낸다.
    /// 뷰는 그 묶음이 어느 시전/공격에서 왔는지 기억해, 결과 연출을 그 시전 연출의 시간표에 맞춘다.
    ///   지킴이·잔향이 기본 공격: 투사체가 닿는 순간(거리·성향·배속으로 정한 비행 시간, BattleView.Attack)에 치명타·숫자·흰 번쩍임이 터진다
    ///   불 밝히기: 빛 물결이 그 적에 닿는 순간
    ///   투사체 스킬: 비행 시간 뒤 / 레인 관통·광선: 바늘·빛이 그 적을 지나가는 순간 / 전체 공격: 물결이 그 적에 닿는 순간
    ///   범위 스킬 (FX-JUICE E7): 폭발은 가운데에서 100px마다 25ms 늦게 물결처럼, 퍼지는 고리는 고리 반지름이 그 적에 닿는 순간,
    ///     회전은 도는 그림이 그 적을 지나는 순간, 비는 그 적 머리 위 빗방울이 떨어지는 순간 (BattleFx.SkillReachDelay — 20마리를 한꺼번에 쳐도 흰 덩어리가 아니라 물결)
    ///   연쇄: 앞 대상에서 다음 대상으로 빛이 튀어 가는 순서대로 (적마다 hop 시간만큼 늦게)
    ///   아군 전체 스킬(잔향이 모두·지킴이 모두): 시전자에게서 받는 친구마다 선물이 날아가 닿을 때 연출, 아무도 못 받으면 헛손질
    /// 이 밖에 같은 프레임 같은 스킬 묶기(시너지 보너스 스킬을 여러 친구가 동시에 쓸 때), 큰 스킬 알림 간격 제한,
    /// 아직 그려지지 않은(방금 소환된)·이미 풀린(소멸 시) 잔향이 시전자 처리도 여기 있다. 모두 고정 크기 배열 (할당 없음).
    /// </summary>
    public partial class BattleView
    {
        enum Context : byte
        {
            None,
            /// <summary>지킴이 기본 공격 (GuardianAttack 뒤)</summary>
            Basic,
            /// <summary>스킬 시전 (SkillCast 뒤)</summary>
            Cast,
            /// <summary>잔향이 기본 공격 (EchoAttack 뒤 — 피격 SourceId는 -1이라 대상 ID로 맞춘다)</summary>
            EchoBasic,
            /// <summary>불 밝히기 (LampLit 뒤 — 빛 물결이 적마다 닿는 순간)</summary>
            Lamp,
        }

        struct CastStamp
        {
            public string Skill;
            public Vector2 Center;
        }

        struct PendingCaster
        {
            public int Id;
            public string Text;
            public Color Accent;
            public float Expire;
        }

        /// <summary>시전자 위치 (전장 로컬)</summary>
        struct CasterPoint
        {
            /// <summary>가슴 높이 (기 모으기·투사체 시작)</summary>
            public Vector2 From;
            public Vector2 Feet;
            public Vector2 Head;
            public string FormId;
            /// <summary>방금 소환돼 아직 뷰가 없는 잔향이 (다음 Sync에서 그려진다)</summary>
            public bool Pending;
            /// <summary>이미 빛으로 풀린 잔향이 (소멸 시 스킬)</summary>
            public bool Dissolved;
        }

        const int DelaySlots = 32;
        const int CastMemory = 16;
        const int BigMemory = 12;
        const int PendingSlots = 8;
        const int DissolveMemory = 8;
        const int GiftSlots = 16;
        /// <summary>연쇄가 적 하나에서 다음 적으로 튀어 가는 시간</summary>
        const float HopTime = 0.09f;
        /// <summary>투사체가 아닌 연쇄 스킬의 첫 대상까지 (터진 자리에서 첫 적으로)</summary>
        const float FirstHop = 0.08f;
        /// <summary>아군 전체 스킬 선물이 날아가는 시간</summary>
        const float GiftTravel = 0.3f;
        /// <summary>같은 큰 스킬의 이름 알림·번쩍임·흔들림 최소 간격(초, 실제 시간)</summary>
        const float BigRepeat = 30f;
        /// <summary>같은 프레임 같은 스킬을 '같은 자리'로 보는 거리(px)</summary>
        const float SharedRadius = 40f;
        /// <summary>전체 공격 물결 그림은 위쪽 입자가 이만큼 남아 있을 때만 (다른 연출 몫을 남긴다)</summary>
        const int SplashReserve = 90;
        /// <summary>처형 입맞춤 연출 간격 기록용 상태 번호 (SkillEffectType 값과 겹치지 않게)</summary>
        const int ExecuteStamp = 500;
        /// <summary>잔향이 발밑 → 몸 가운데 / 머리 위 높이 (EchoView 기준 크기, ActorScale 곱함)</summary>
        const float EchoBodyLift = 50f;
        const float EchoHeadLift = 110f;
        /// <summary>선물 받는 쪽 구분 키: 수선점 / 지킴이(-10 - 슬롯) / 잔향이(ID)</summary>
        const int ShopRecipient = -1;

        Context _context;
        int _contextFrame = -1;
        int _basicSlot = -1;
        int _basicTarget = -1;

        int _castSource = -1;
        string _castSkill;
        SkillLook _castLook;
        Sprite _castSprite;
        bool _castIsArt;
        SkillVfxStyle _castStyle;
        bool _castProjectile;
        /// <summary>레인 관통 투사체·광선: 시작 → 끝 선분을 지나가는 순간으로 적마다 시간을 정한다</summary>
        bool _castSegment;
        Vector2 _castFrom;
        Vector2 _castEnd;
        /// <summary>주 대상(곧은 투사체는 레인 끝)에 닿기까지 (BattleFx.SkillCast가 돌려준 값)</summary>
        float _castFlight;
        /// <summary>범위 스킬(폭발·고리·회전·비)의 효과 중심(바닥)과 반경 — 적마다 닿는 순간을 BattleFx.SkillReachDelay로 정한다</summary>
        bool _castStagger;
        Vector2 _castCenter;
        Vector2 _castRadius;
        int _castPrimary = -1;
        Vector2 _castBody;
        bool _castAllyGroup;
        int _castRecipients;
        Vector2 _castCasterHead;
        EchoView _castEcho;

        int _chainHops;
        Vector2 _chainLast;
        float _chainTime;

        readonly int[] _delayIds = new int[DelaySlots];
        readonly float[] _delayTimes = new float[DelaySlots];
        int _delayCount;
        string _delaySkill;
        readonly int[] _giftKeys = new int[GiftSlots];
        int _giftCount;

        readonly CastStamp[] _castStamps = new CastStamp[CastMemory];
        int _castStampCount;
        int _castStampFrame = -1;

        readonly string[] _bigKeys = new string[BigMemory];
        readonly float[] _bigTimes = new float[BigMemory];
        int _bigCursor;

        readonly PendingCaster[] _pendingCasters = NewPendingCasters();
        readonly int[] _dissolvedIds = NewIds(DissolveMemory);
        readonly Vector2[] _dissolvedAt = new Vector2[DissolveMemory];
        int _dissolvedCursor;
        int _shopShieldFrame = -1;

        static PendingCaster[] NewPendingCasters()
        {
            var slots = new PendingCaster[PendingSlots];
            for (int i = 0; i < slots.Length; i++) slots[i].Id = -1;
            return slots;
        }

        static int[] NewIds(int count)
        {
            var ids = new int[count];
            for (int i = 0; i < ids.Length; i++) ids[i] = int.MinValue;
            return ids;
        }

        bool InCast => _context == Context.Cast && _contextFrame == Time.frameCount;

        void ResetCastState()
        {
            _context = Context.None;
            _contextFrame = -1;
            _delayCount = 0;
            _delaySkill = null;
            _castStampCount = 0;
            _castStampFrame = -1;
            _castEcho = null;
            _castSprite = null;
            for (int i = 0; i < _pendingCasters.Length; i++) _pendingCasters[i] = new PendingCaster { Id = -1 };
            for (int i = 0; i < _dissolvedIds.Length; i++) _dissolvedIds[i] = int.MinValue;
        }

        // ───────────── 맥락 ─────────────

        /// <summary>
        /// 이벤트가 올 때마다: 시전/공격의 결과로 이어서 오는 종류가 아니면(또는 프레임이 바뀌었으면) 이전 맥락을 닫는다.
        /// </summary>
        void TrackContext(BattleEventType type)
        {
            if (_context == Context.None) return;
            if (_contextFrame != Time.frameCount)
            {
                EndContext();
                return;
            }
            switch (type)
            {
                case BattleEventType.EnemyHit:
                case BattleEventType.EnemyKilled:
                case BattleEventType.CriticalHit:
                case BattleEventType.StatusApplied:
                case BattleEventType.ShieldGained:
                case BattleEventType.ShieldBroken:
                case BattleEventType.EchoHealed:
                case BattleEventType.ShopHealed:
                case BattleEventType.TokenSummoned:
                case BattleEventType.EnemySplit:
                // 분열로 생긴 작은 적의 등장 (EnemySplit 바로 앞에 온다) — 같은 시전의 결과가 이어진다
                case BattleEventType.EnemySpawned:
                case BattleEventType.EnemyRevealed:
                case BattleEventType.EnemyEnraged:
                case BattleEventType.BossSpawned:
                    return;
            }
            EndContext();
        }

        /// <summary>맥락을 닫는다. 받을 친구가 아무도 없던 아군 전체 스킬은 여기서 헛손질 연출</summary>
        void EndContext()
        {
            if (_context == Context.Cast && _castAllyGroup && _castRecipients == 0 && _fx != null)
            {
                _fx.SkillFizzle(_castCasterHead, _castLook, _castEcho != null && _castEcho.isActiveAndEnabled ? _castEcho : null);
            }
            _context = Context.None;
            _castEcho = null;
        }

        void BeginBasic(int slot, int target)
        {
            EndContext();
            _context = Context.Basic;
            _contextFrame = Time.frameCount;
            _basicSlot = slot;
            _basicTarget = target;
        }

        /// <param name="shared">같은 프레임에 같은 스킬이 같은 자리에 이미 그려졌음 → 그 시간표(적별 지연)를 그대로 쓴다</param>
        void BeginCast(in BattleEvent e, in SkillLook look, Sprite sprite, bool isArt, in SkillFxRequest request, bool allyGroup, in CasterPoint caster,
            EchoView echo, bool shared)
        {
            EndContext();
            _context = Context.Cast;
            _contextFrame = Time.frameCount;
            _castSource = e.SourceId;
            _castSkill = e.Key;
            _castLook = look;
            _castSprite = sprite;
            _castIsArt = isArt;
            _castStyle = look.Style;
            _castProjectile = !allyGroup && look.Style == SkillVfxStyle.Projectile;
            _castSegment = !allyGroup && (look.Style == SkillVfxStyle.Beam || request.Straight);
            _castFrom = request.From;
            _castEnd = request.BeamEnd != Vector2.zero ? request.BeamEnd : request.To;
            _castPrimary = e.TargetId;
            _castBody = request.Center + Vector2.up * 40f;
            _castStagger = !allyGroup && Staggers(look.Style);
            _castCenter = request.Center;
            _castRadius = request.Radius;
            _castAllyGroup = allyGroup;
            _castRecipients = 0;
            _castCasterHead = caster.Head;
            _castEcho = echo;
            _chainHops = 0;
            _chainTime = 0f;
            _giftCount = 0;
            bool keep = shared && string.Equals(_delaySkill, e.Key, StringComparison.Ordinal);
            if (keep) return;
            _delayCount = 0;
            _delaySkill = e.Key;
            _castFlight = 0f;
        }

        /// <summary>
        /// 같은 프레임에 같은 스킬이 이미 그려졌는지 기억/확인한다 (시너지 보너스 스킬은 구성원마다 한 번씩 시전된다).
        /// shared = 같은 자리(또는 적 전체·수선점·아군 전체 대상)라 큰 연출을 다시 그릴 필요가 없음.
        /// </summary>
        bool RememberCast(string skillId, Vector2 center, SkillTargetType type, out bool shared)
        {
            int frame = Time.frameCount;
            if (_castStampFrame != frame)
            {
                _castStampFrame = frame;
                _castStampCount = 0;
            }
            shared = false;
            bool repeat = false;
            bool global = IsSharedTarget(type);
            for (int i = 0; i < _castStampCount; i++)
            {
                ref var stamp = ref _castStamps[i];
                if (!string.Equals(stamp.Skill, skillId, StringComparison.Ordinal)) continue;
                repeat = true;
                if (global || (stamp.Center - center).sqrMagnitude < SharedRadius * SharedRadius)
                {
                    shared = true;
                    break;
                }
            }
            if (!shared && _castStampCount < _castStamps.Length) _castStamps[_castStampCount++] = new CastStamp { Skill = skillId, Center = center };
            return repeat;
        }

        static bool IsSharedTarget(SkillTargetType type) => type switch
        {
            SkillTargetType.AllEnemies => true,
            SkillTargetType.Shop => true,
            SkillTargetType.AllEchoes => true,
            SkillTargetType.AllGuardians => true,
            SkillTargetType.AllAllies => true,
            _ => false,
        };

        /// <summary>큰 스킬 알림·번쩍임을 이번에 보여도 되는지 (스킬마다 BigRepeat초에 한 번)</summary>
        bool AllowBig(string skillId)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _bigKeys.Length; i++)
            {
                if (!string.Equals(_bigKeys[i], skillId, StringComparison.Ordinal)) continue;
                if (now - _bigTimes[i] < BigRepeat) return false;
                _bigTimes[i] = now;
                return true;
            }
            _bigKeys[_bigCursor] = skillId;
            _bigTimes[_bigCursor] = now;
            _bigCursor = (_bigCursor + 1) % _bigKeys.Length;
            return true;
        }

        // ───────────── 결과 연출 시간 ─────────────

        void RegisterDelay(int enemyId, float delay)
        {
            for (int i = 0; i < _delayCount; i++)
            {
                if (_delayIds[i] != enemyId) continue;
                _delayTimes[i] = delay;
                return;
            }
            if (_delayCount >= _delayIds.Length) return;
            _delayIds[_delayCount] = enemyId;
            _delayTimes[_delayCount] = delay;
            _delayCount++;
        }

        /// <summary>
        /// 지금 시전의 결과가 이 적에게 닿는 시간 (정해 둔 값 → 레인/광선 통과 순간 → 범위 스킬 물결(폭발·고리·회전·비) → 투사체 비행 시간 → 0)
        /// </summary>
        float CastDelay(int enemyId)
        {
            for (int i = 0; i < _delayCount; i++)
            {
                if (_delayIds[i] == enemyId) return _delayTimes[i];
            }
            if (_castSegment && _enemies.TryGet(enemyId, out var view))
            {
                var segment = _castEnd - _castFrom;
                float lengthSq = segment.sqrMagnitude;
                float along = lengthSq > 1f ? Mathf.Clamp01(Vector2.Dot(view.HitPoint - _castFrom, segment) / lengthSq) : 1f;
                return _castStyle == SkillVfxStyle.Beam ? 0.03f + BattleFx.BeamReach * along : _castFlight * along;
            }
            if (_castStagger && _enemies.TryGet(enemyId, out var target))
            {
                // 같은 적은 같은 시전 안에서 늘 같은 순간 (피해 숫자·반응·처치 보류·상태가 한 박자에)
                float reach = _fx.SkillReachDelay(_castStyle, _castCenter, _castRadius, target.Rect.anchoredPosition);
                RegisterDelay(enemyId, reach);
                return reach;
            }
            return _castProjectile ? _castFlight : 0f;
        }

        /// <summary>적마다 닿는 순간이 다른 범위 스킬 방식 (폭발·고리·회전·비)</summary>
        static bool Staggers(SkillVfxStyle style) =>
            style == SkillVfxStyle.Burst || style == SkillVfxStyle.Ring || style == SkillVfxStyle.Orbit || style == SkillVfxStyle.Rain;

        /// <summary>이 이벤트(SourceId = 공격자/시전자)의 결과 연출을 늦출 시간</summary>
        float ContextDelay(int sourceId, int enemyId)
        {
            if (_contextFrame != Time.frameCount) return 0f;
            switch (_context)
            {
                case Context.Basic:
                    return sourceId == _basicSlot && enemyId == _basicTarget ? _basicFlight : 0f;
                case Context.EchoBasic:
                    return (sourceId == _echoSource || sourceId < 0) && enemyId == _echoTarget ? _echoFlight : 0f;
                case Context.Cast:
                    // 스킬 피해·보호막 깨짐은 SourceId가 -1로 온다 (같은 프레임 시전 맥락 안이면 그 시전의 결과)
                    return sourceId == _castSource || sourceId < 0 ? CastDelay(enemyId) : 0f;
                case Context.Lamp:
                    return _enemies.TryGet(enemyId, out var view) ? _fx.LampWaveDelay(view.Rect.anchoredPosition) : 0f;
            }
            return 0f;
        }

        /// <summary>지금 맥락이 이 적을 노린 기본 공격(지킴이·잔향이)인지 — 뒤따르는 치명타가 예약된 명중 그림을 키운다</summary>
        bool IsBasicHitOn(int enemyId)
        {
            if (_contextFrame != Time.frameCount) return false;
            return (_context == Context.Basic && enemyId == _basicTarget) || (_context == Context.EchoBasic && enemyId == _echoTarget);
        }

        /// <summary>
        /// 지금 맥락이 이 적을 노린 잔향이 기본 공격인지 (OnEnemyHit의 잔향이 갈래와 같은 판단) — 그 치명타도 잔향이 반응으로
        /// (번쩍임 절반 · 넉백 0.6배 · 멈춤 없음, HitFeel.ForEcho). 치명타가 같은 일격의 피격보다 먼저 와서 EnemyView가 반응을 합칠 때
        /// echo를 AND로 묶으므로, 치명타 쪽도 echo여야 잔향이 치명타가 66ms 멈추지 않는다
        /// </summary>
        bool IsEchoBasicOn(int enemyId) => _contextFrame == Time.frameCount && _context == Context.EchoBasic && enemyId == _echoTarget;

        /// <summary>
        /// 연쇄 한 번: 시뮬레이터는 맞는 순서대로 적마다 StatusApplied(Chain) → 피해를 보낸다.
        /// 앞 대상(첫 번째는 터진 자리 또는 투사체)에서 이 적까지 빛이 튀어 가는 연출을 그리고, 이 적의 피해·처치를 닿는 순간까지 늦춘다.
        /// </summary>
        void OnChainHop(in BattleEvent e, Sprite sprite, bool isArt, in SkillLook look)
        {
            Vector2 to;
            bool visible = _enemies.TryGet(e.TargetId, out var enemy);
            if (visible) to = enemy.HitPoint;
            else if (e.Position != Vector2.zero) to = _mapper.WorldToField(e.Position) + Vector2.up * 40f;
            else to = _chainLast;

            float start;
            float travel;
            bool link = true;
            var from = _chainLast;
            if (_chainHops == 0)
            {
                start = 0f;
                if (_castProjectile)
                {
                    // 투사체가 첫 대상으로 날아간다: 주 대상이면 투사체가 곧 연결선
                    travel = _castFlight;
                    link = e.TargetId != _castPrimary;
                    from = _castFrom;
                }
                else
                {
                    travel = FirstHop;
                    from = _castBody;
                }
            }
            else
            {
                start = _chainTime;
                travel = HopTime;
            }

            float arrive = start + travel;
            if (visible && (link || _chainHops > 0)) _fx.ChainHop(from, enemy, sprite, isArt, look, start, travel, link);
            RegisterDelay(e.TargetId, arrive);
            _chainLast = to;
            _chainTime = arrive;
            _chainHops++;
        }

        // ───────────── 아군 전체 스킬 선물 ─────────────

        /// <summary>
        /// 아군 전체 스킬이면 받은 친구로 세고, 시전자에게서 이 친구에게 선물을 보낸 뒤(받는 친구마다 한 번) 받는 쪽 연출을 늦출 시간을 돌려준다. 아니면 0.
        /// </summary>
        /// <param name="show">받는 쪽 연출을 지금 띄울 때만 선물을 보낸다 (같은 상태가 방금 떠서 생략하면 받은 것으로만 센다)</param>
        float AllyGift(int sourceId, int recipient, Vector2 to, bool show = true)
        {
            if (!InCast || !_castAllyGroup || sourceId != _castSource) return 0f;
            _castRecipients++;
            if (!show) return 0f;
            // 시전자 자신(강화를 받을 친구가 없어 스스로 받는 경우)에게는 날려 보낼 거리가 없다
            float delay = (to - _castFrom).sqrMagnitude < GiftMinDistance * GiftMinDistance ? 0f : GiftTravel;
            for (int i = 0; i < _giftCount; i++)
            {
                if (_giftKeys[i] == recipient) return delay;
            }
            if (_giftCount < _giftKeys.Length) _giftKeys[_giftCount++] = recipient;
            if (delay > 0f) _fx.Gift(_castFrom, to, _castSprite, _castIsArt, _castLook, GiftTravel);
            return delay;
        }

        /// <summary>이보다 가까우면 선물을 날리지 않는다 (px)</summary>
        const float GiftMinDistance = 60f;

        static int GuardianRecipient(int slot) => -10 - slot;

        // ───────────── 시전자 위치 ─────────────

        /// <summary>
        /// 시전자 위치: 지킴이·잔향이 뷰 → 방금 소환돼 아직 그려지지 않은 잔향이(시뮬레이터 위치) → 이미 빛으로 풀린 잔향이(풀린 자리)
        /// → 효과 자리 → 수선점 순.
        /// </summary>
        CasterPoint ResolveCasterPoint(in BattleEvent e, GuardianSlotView guardian, EchoView echo)
        {
            var point = new CasterPoint();
            if (guardian != null)
            {
                point.From = guardian.MuzzlePosition;
                point.Feet = guardian.FeetPosition;
                point.Head = guardian.HeadPoint;
                point.FormId = guardian.State != null ? guardian.State.FormId : null;
                return point;
            }
            if (echo != null)
            {
                point.From = echo.BodyPoint;
                point.Feet = echo.Rect.anchoredPosition;
                point.Head = echo.HeadPoint;
                point.FormId = echo.State != null ? echo.State.FormId : null;
                return point;
            }
            float scale = _mapper.ActorScale;
            if (e.SourceId >= BattleSimulator.EchoIdBase)
            {
                var state = FindEcho(e.SourceId);
                bool known = false;
                if (state != null)
                {
                    point.Feet = _mapper.WorldToField(state.Position);
                    point.FormId = state.FormId;
                    point.Pending = state.Alive;
                    point.Dissolved = !state.Alive;
                    known = true;
                }
                else if (TryDissolvedAt(e.SourceId, out var at))
                {
                    point.Feet = at;
                    point.Dissolved = true;
                    known = true;
                }
                if (known)
                {
                    point.From = point.Feet + Vector2.up * (EchoBodyLift * scale);
                    point.Head = point.Feet + Vector2.up * (EchoHeadLift * scale);
                    return point;
                }
            }
            if (e.SourceId >= 0 && e.Position != Vector2.zero)
            {
                point.Feet = _mapper.WorldToField(e.Position);
                point.From = point.Feet + Vector2.up * 40f;
                point.Head = point.Feet + Vector2.up * 100f;
                return point;
            }
            point.From = _shop.DoorPosition + Vector2.up * 60f;
            point.Feet = _shop.FieldPosition;
            point.Head = _shop.DomeCenter + Vector2.up * 60f;
            return point;
        }

        /// <summary>방금 소환된 잔향이의 시전 연출(통 튀기 + 대사)을 뷰가 생길 때까지 맡아 둔다</summary>
        void QueueCaster(int echoId, string text, Color accent)
        {
            int slot = -1;
            float oldest = float.MaxValue;
            for (int i = 0; i < _pendingCasters.Length; i++)
            {
                ref var p = ref _pendingCasters[i];
                if (p.Id == echoId || p.Id < 0)
                {
                    slot = i;
                    break;
                }
                if (p.Expire < oldest)
                {
                    oldest = p.Expire;
                    slot = i;
                }
            }
            if (slot < 0) return;
            ref var pending = ref _pendingCasters[slot];
            // 같은 잔향이가 소환 시 스킬을 여러 개(형태 + 보너스) 쓰면 첫 대사를 남긴다
            string keep = pending.Id == echoId && !string.IsNullOrEmpty(pending.Text) ? pending.Text : text;
            pending = new PendingCaster { Id = echoId, Text = keep, Accent = accent, Expire = Time.time + 0.75f };
        }

        /// <summary>EchoViewPool.Sync 뒤: 맡아 둔 시전 연출을 새로 그려진 잔향이에게 넘긴다</summary>
        void FlushPendingCasters()
        {
            for (int i = 0; i < _pendingCasters.Length; i++)
            {
                ref var p = ref _pendingCasters[i];
                if (p.Id < 0) continue;
                if (_echoes.TryGet(p.Id, out var view))
                {
                    view.PlayCast();
                    if (_callouts != null && !string.IsNullOrEmpty(p.Text) && _callouts.Say(view, p.Text, p.Accent)) _fx.NoteSkillNotice();
                    p = new PendingCaster { Id = -1 };
                }
                else if (Time.time > p.Expire) p = new PendingCaster { Id = -1 };
            }
        }

        void RememberDissolve(int echoId, Vector2 fieldPosition)
        {
            _dissolvedIds[_dissolvedCursor] = echoId;
            _dissolvedAt[_dissolvedCursor] = fieldPosition;
            _dissolvedCursor = (_dissolvedCursor + 1) % _dissolvedIds.Length;
        }

        bool TryDissolvedAt(int echoId, out Vector2 at)
        {
            for (int i = 0; i < _dissolvedIds.Length; i++)
            {
                if (_dissolvedIds[i] != echoId) continue;
                at = _dissolvedAt[i];
                return true;
            }
            at = default;
            return false;
        }

        // ───────────── 상시 패시브 (처형) ─────────────

        /// <summary>벨벳 마무리: 처형 패시브를 가진 친구의 기본 공격이 약해진(30% 이하) 적에게 닿으면 입술 자국 (적마다 짧은 간격)</summary>
        void TryExecuteKiss(EnemyView target, in UnitPassive passive, float delay)
        {
            if (target == null || !passive.HasExecute) return;
            var state = target.State;
            if (state == null || state.MaxHp <= 0f || state.Hp > state.MaxHp * UnitPassive.ExecuteRatio) return;
            if (!AllowStatus(target.Id, ExecuteStamp)) return;
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(passive.Vfx, out sprite, out isArt);
            _fx.ExecuteKiss(target, sprite, isArt, SkillVfxLook.Get(passive.Vfx), delay);
        }

        // ───────────── 스킬 성질 ─────────────

        /// <summary>적에게 가는 효과가 하나라도 있는지 (없으면 아군 전체 스킬은 수선점에 큰 그림을 그리지 않는다)</summary>
        static bool HasEnemyEffect(SkillDef skill)
        {
            var effects = skill != null ? skill.effects : null;
            if (effects == null) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) continue;
                switch (effects[i].type)
                {
                    case SkillEffectType.Damage:
                    case SkillEffectType.Chain:
                    case SkillEffectType.Slow:
                    case SkillEffectType.Stun:
                    case SkillEffectType.Knockback:
                    case SkillEffectType.Pull:
                    case SkillEffectType.Mark:
                    case SkillEffectType.DamageOverTime:
                    case SkillEffectType.Reveal:
                        return true;
                }
            }
            return false;
        }

        /// <summary>잔향이 모두 / 지킴이 모두: 받는 친구들이 흩어져 있어 한 자리에 그릴 수 없는 대상</summary>
        static bool IsAllyGroup(SkillTargetType type) => type == SkillTargetType.AllEchoes || type == SkillTargetType.AllGuardians;
    }
}
