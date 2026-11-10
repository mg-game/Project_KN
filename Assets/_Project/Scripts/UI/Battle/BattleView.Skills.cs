using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면의 스킬 · 시너지 · 기믹 이벤트 연출.
    ///   스킬 시전: 시전자가 통 튀며 빛을 모으고(120ms) → 키별 연출(투사체/광선/폭발/고리/장판/돔/떠오름/머리 위/회전/내림) + 대사 말풍선.
    ///     큰 스킬·전설 이상 시전자의 적 스킬은 조명 (전장 0.15 어둡게, 2초에 한 번 — 스킬 빛은 밝게 남는다).
    ///     위기 한 방·불 밝히기 연계·적 전체 같은 드문 큰 스킬만 이름 알림 + 빛 번쩍임(스크린 블렌드 0.18) (스킬마다 30초에 한 번, 전장 안에서만).
    ///     범위 스킬(폭발·고리·비)은 첫 명중 순간에 스킬 채널 흔들림 0.4 (초당 0.5까지)
    ///   결과 연출 시간 맞추기·연쇄·아군 전체 선물·같은 스킬 묶기·소환/소멸 시전자: BattleView.Cast.cs
    ///   보호막/회복/상태 이상: 대상마다 작은 연출 (지속 표시는 각 뷰가 상태 값으로 그린다)
    ///   기믹: 분열·드러남·분노·토큰 소환·치명타·불빛 흡수
    ///   스테이지 시작: 기믹 분위기 + 기믹 칩/알림 + 시너지 띠/아이콘 (전투가 구독 전에 시작될 수 있으므로 이벤트가 아니라 시뮬레이터 상태로 그린다)
    /// 자주 오는 회복 이벤트는 대상별로 모아 짧은 간격으로 한 번에 보여준다.
    /// </summary>
    public partial class BattleView
    {
        const float HealInterval = 0.55f;
        const float DrainInterval = 0.6f;
        const int HealSlots = 16;
        const int StatusMemory = 48;
        /// <summary>같은 적에게 같은 상태 연출을 다시 띄우기까지 최소 간격 (지속 효과가 틱마다 다시 걸려도 시끄럽지 않게)</summary>
        const float StatusRepeat = 0.9f;
        /// <summary>스킬 조명 (E7): 전장 어둡게 0.15 · 60ms에 들어와 첫 명중까지(최소 120ms, 최대 0.5초) 머물고 200ms에 걷힌다</summary>
        const float SpotlightDim = 0.15f;
        const float SpotlightIn = 0.06f;
        const float SpotlightHold = 0.12f;
        const float SpotlightHoldMax = 0.5f;
        const float SpotlightOut = 0.2f;
        /// <summary>큰 스킬 빛 번쩍임 (스크린 블렌드) 세기 / 길이</summary>
        const float BigSkillLight = 0.18f;
        const float BigSkillLightTime = 0.5f;

        struct StatusKey
        {
            /// <summary>0 = 적, 1 = 잔향이, 2 = 지킴이, 3 = 수선점</summary>
            public byte Kind;
            public string Vfx;
        }

        struct StatusStamp
        {
            public int Id;
            public int Type;
            public float Time;
        }

        struct HealSlot
        {
            public int Id;
            public float Accum;
            public float Timer;
        }

        readonly Dictionary<string, StatusKey> _statusKeys = new(StringComparer.Ordinal);
        readonly List<KeyValuePair<string, int>> _lastSynergies = new();
        readonly HealSlot[] _echoHeals = NewHealSlots();
        readonly StatusStamp[] _statusStamps = new StatusStamp[StatusMemory];
        int _statusCursor;

        bool _introPending;
        bool _introBanner;
        bool _introShownOnce;

        int _critFrame = -1;
        int _critTarget = -1;
        /// <summary>마지막 치명타 / 피격 피해 (실수 그대로 — 후반 피해는 int 범위를 넘는다)</summary>
        float _critValue;
        int _hitFrame = -1;
        int _hitTarget = -1;
        float _hitValue;
        int _hitNumber = -1;

        float _shopHealAccum;
        float _shopHealTimer;
        /// <summary>다음 수선점 회복 연출을 늦출 시간 (힐 맥박 방울이 문에 닿는 순간까지)</summary>
        float _shopHealDelay;
        float _drainTimer;

        static HealSlot[] NewHealSlots()
        {
            var slots = new HealSlot[HealSlots];
            for (int i = 0; i < slots.Length; i++) slots[i].Id = -1;
            return slots;
        }

        // ───────────── 갱신 / 연결 ─────────────

        void OnRebindSkills(bool showIntro)
        {
            _introPending = true;
            _introBanner |= showIntro;
            _shopHealAccum = 0f;
            _shopHealTimer = 0f;
            for (int i = 0; i < _echoHeals.Length; i++) _echoHeals[i] = new HealSlot { Id = -1 };
            for (int i = 0; i < _statusStamps.Length; i++) _statusStamps[i] = new StatusStamp { Id = int.MinValue };
            ResetCastState();
        }

        void TickSkillPresentation(float dt)
        {
            if (_introPending) ShowIntro();
            // 지난 프레임의 시전 맥락은 여기서 닫는다 (받을 친구가 없던 아군 전체 스킬의 헛손질 연출)
            if (_context != Context.None && _contextFrame != Time.frameCount) EndContext();
            FlushPendingCasters();

            _drainTimer = Mathf.Max(0f, _drainTimer - dt);
            _shopHealTimer -= dt;
            if (_shopHealTimer <= 0f && _shopHealAccum > 0f) FlushShopHeal();

            for (int i = 0; i < _echoHeals.Length; i++)
            {
                ref var slot = ref _echoHeals[i];
                if (slot.Id < 0) continue;
                slot.Timer -= dt;
                if (slot.Timer > 0f) continue;
                if (slot.Accum > 0f)
                {
                    ShowEchoHeal(slot.Id, slot.Accum, 0f);
                    slot.Accum = 0f;
                    slot.Timer = HealInterval;
                }
                else if (slot.Timer < -2f) slot.Id = -1;
            }
        }

        /// <summary>스테이지 시작 연출: 기믹 분위기·칩·알림, 시너지 아이콘·띠. 배너는 새 스테이지(showIntro)일 때만</summary>
        void ShowIntro()
        {
            _introPending = false;
            bool banner = _introBanner;
            _introBanner = false;
            if (_sim == null) return;

            var modifier = _sim.StageModifier;
            var setup = _sim.Setup;
            IReadOnlyList<KeyValuePair<string, int>> active = setup != null && setup.Modifiers != null ? setup.Modifiers.ActiveSynergies : null;
            if (_ambience != null) _ambience.SetModifier(modifier);
            if (_synergyHud != null)
            {
                _synergyHud.SetModifier(modifier);
                _synergyHud.SetSynergies(active);
            }
            if (!banner) return;

            if (modifier != StageModifierType.None)
            {
                var color = UITheme.StageModifierColor(modifier);
                if (_notice != null)
                {
                    var icon = _library != null ? _library.StageIcon(modifier) : null;
                    var tint = Color.white;
                    if (icon == null && _library != null)
                    {
                        icon = _library.Motif(SynergyHudView.FallbackMotif(modifier));
                        tint = color;
                    }
                    _notice.Show(UITheme.StageModifierName(modifier), SkillVfxLook.ModifierDescription(modifier), icon, tint, color, 2.4f);
                }
                _fx.FieldFlash(color, 0.18f, 0.8f);
            }

            if (active != null && active.Count > 0)
            {
                if (!_introShownOnce || SynergiesChanged(active))
                {
                    if (_synergyBanner != null) _synergyBanner.Show(active, 0.55f);
                    _lastSynergies.Clear();
                    for (int i = 0; i < active.Count; i++) _lastSynergies.Add(active[i]);
                }
                else if (_synergyHud != null) _synergyHud.Shimmer();
                _introShownOnce = true;
            }
            else _lastSynergies.Clear();
        }

        bool SynergiesChanged(IReadOnlyList<KeyValuePair<string, int>> active)
        {
            if (active.Count != _lastSynergies.Count) return true;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Key != _lastSynergies[i].Key || active[i].Value != _lastSynergies[i].Value) return true;
            }
            return false;
        }

        // ───────────── 이벤트 분배 ─────────────

        void OnSkillEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.SkillCast:
                    OnSkillCast(e);
                    break;
                case BattleEventType.ShieldGained:
                    OnShieldGained(e);
                    break;
                case BattleEventType.ShieldBroken:
                    OnShieldBroken(e);
                    break;
                case BattleEventType.EchoHealed:
                    OnEchoHealed(e);
                    break;
                case BattleEventType.StatusApplied:
                    OnStatusApplied(e);
                    break;
                case BattleEventType.EnemySplit:
                    OnEnemySplit(e);
                    break;
                case BattleEventType.EnemyRevealed:
                    OnEnemyRevealed(e);
                    break;
                case BattleEventType.EnemyEnraged:
                    OnEnemyEnraged(e);
                    break;
                case BattleEventType.TokenSummoned:
                    OnTokenSummoned(e);
                    break;
                case BattleEventType.SynergyActivated:
                case BattleEventType.StageModifierStarted:
                    _introPending = true;
                    break;
                case BattleEventType.CriticalHit:
                    OnCriticalHit(e);
                    break;
                case BattleEventType.LampDrained:
                    OnLampDrained(e);
                    break;
            }
        }

        // ───────────── 스킬 시전 ─────────────

        void OnSkillCast(BattleEvent e)
        {
            if (_sim == null) return;
            var skill = _sim.GetSkill(e.Key);
            string key = skill != null ? skill.vfx : null;
            var look = SkillVfxLook.Get(key);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(key, out sprite, out isArt);

            ResolveCaster(e.SourceId, out var guardian, out var echo);
            var caster = ResolveCasterPoint(e, guardian, echo);

            EnemyView target = null;
            if (e.TargetId >= 0 && _enemies.TryGet(e.TargetId, out var found) && found.IsTargetable) target = found;
            var targetType = skill != null ? skill.target : SkillTargetType.EnemiesInRange;
            var center = ResolveCenter(e, targetType, caster.Feet, target);
            float radiusWorld = e.Value > 0.01f ? e.Value : skill != null ? skill.radius : 0.6f;
            float ppu = _mapper.Ppu;
            bool big = IsBigSkill(skill);
            // 잔향이 모두·지킴이 모두에게 주는 스킬은 수선점에 큰 그림을 그리지 않고 시전자에게서 받는 친구마다 선물을 보낸다
            bool allyGroup = IsAllyGroup(targetType) && !HasEnemyEffect(skill);
            // 같은 프레임에 같은 스킬을 여러 친구가 쓰면 (시너지 보너스 스킬) 시전자마다 기 모으기만, 같은 자리 큰 연출·알림·대사는 한 번
            bool repeat = RememberCast(e.Key, center, targetType, out bool shared);

            var request = new SkillFxRequest
            {
                Key = key,
                Look = look,
                Sprite = sprite,
                IsArt = isArt,
                From = caster.From,
                To = target != null ? target.HitPoint : center + Vector2.up * 40f,
                Center = center,
                Radius = new Vector2(radiusWorld * ppu, radiusWorld * ppu * _mapper.Squash),
                Target = target,
                Power = big ? 1.3f : 1f,
            };
            AdjustForStyle(ref request, look.Style, targetType, guardian, echo, caster.Feet, e.Position);

            if (guardian != null) guardian.PlayCast();
            if (echo != null) echo.PlayCast();
            _fx.CastCharge(caster.From, caster.Feet, look.Main, guardian != null ? 1f : 0.75f);

            BeginCast(e, look, sprite, isArt, request, allyGroup, caster, echo, shared);
            if (allyGroup) _fx.CasterEmblem(caster.Head, sprite, isArt, look, echo);
            else if (!shared)
            {
                _castFlight = _fx.SkillCast(request);
                if (targetType == SkillTargetType.AllEnemies) SplashAllEnemies(request);
            }

            bool hitsEnemies = !allyGroup && HasEnemyEffect(skill);
            // 범위 스킬(폭발·고리·비)은 첫 명중 순간에 스킬 채널 흔들림 0.4 (명중마다 OnSkillHit도 같은 시전으로 한 번만 예약한다)
            if (hitsEnemies && !shared && IsAreaTarget(targetType) && ShakesArea(look.Style))
                _fx.AreaSkillShake(look.Style == SkillVfxStyle.Rain ? BattleFx.RainFirstReach : 0f, Direction(caster.From, request.To));

            // 스킬 조명: 큰 스킬이나 전설 이상 시전자의 적 스킬 — 전장이 잠깐 어두워지고 스킬 빛만 밝게 (2초에 한 번)
            if (!repeat && !shared && (big || (hitsEnemies && IsLegendaryCaster(guardian, echo, caster.FormId))) && _fx.TrySpotlight())
                _fx.FieldDim(SpotlightDim, SpotlightIn, Mathf.Clamp(_castFlight, SpotlightHold, SpotlightHoldMax), SpotlightOut);

            // 위기 한 방은 전투에 한 번뿐이라 늘 보여주고, 나머지 큰 스킬은 스킬마다 30초에 한 번
            if (big && !repeat && (skill.trigger == SkillTrigger.OnShopLowHp || AllowBig(e.Key)))
            {
                // 빛 번쩍임 (스크린 블렌드 — 우유빛 막이 아니라 밝힌다, 전장 번쩍임 1초 규칙)
                _fx.LightFlash(look.Main, BigSkillLight, BigSkillLightTime);
                // FX-06: 설정 '스킬 이름'이 꺼지면 이름 알림을 띄우지 않는다 (번쩍임은 그대로). 알림도 들여보내기 창을 쓴다
                if (_notice != null && !string.IsNullOrEmpty(skill.name) && CalloutGate.AllowsNotice(true, _skillNames))
                {
                    string name = BattleUnitArt.DisplayName(caster.FormId);
                    // 알림 띠 아이콘(42px)은 배지 크기용 스킬 아이콘 (없으면 연출 그림)
                    var icon = sprite;
                    bool iconIsArt = isArt;
                    if (_library != null && _library.TryGetSkillIcon(key, out var skillIcon, out bool skillIconIsArt))
                    {
                        icon = skillIcon;
                        iconIsArt = skillIconIsArt;
                    }
                    _notice.Show(skill.name, name, icon, iconIsArt ? Color.white : look.Main, look.Main, 1.5f);
                    // 스킬 이름 알림 뒤 1.2초는 연속 처치 알림을 쉰다
                    _fx.NoteSkillNotice();
                    _gate.NoteNotice(e.Key, Time.unscaledTime);
                }
            }

            string line = skill != null && !repeat ? skill.castText : null;
            // FX-06 들여보내기 (M41 말풍선 규칙보다 먼저): 1.2초에 하나 · 같은 스킬 10초 · 우선순위 (큰 스킬 · 전설 이상 > 처음 보는 스킬 > 나머지).
            // 창 안의 높은 우선순위는 0.6초까지 기다렸다 뜬다 (TickBattle4). 설정이 꺼져 있으면 대사도 없다
            if (!string.IsNullOrEmpty(line) && !AdmitCastLine(e, line, big, guardian, echo, caster, look.Main)) line = null;
            // 방금 소환돼 아직 그려지지 않은 잔향이: 그려지는 순간 통 튀며 말한다
            if (caster.Pending)
            {
                QueueCaster(e.SourceId, line, look.Main);
                return;
            }
            if (_callouts == null || string.IsNullOrEmpty(line)) return;
            bool said = false;
            // 큰 스킬 대사는 도배 규칙(같은 대사 15초 · 같은 친구 8초)을 건너뛴다
            if (guardian != null) said = _callouts.Say(guardian, line, look.Main, big);
            else if (echo != null)
            {
                if (!echo.IsToken) said = _callouts.Say(echo, line, look.Main, big);
            }
            // 빛으로 풀리며 쓴 스킬: 풀린 자리에 대사를 남긴다
            else if (caster.Dissolved) said = _callouts.Say(caster.Head, line, look.Main, e.SourceId, big);
            // 말풍선 뒤 1.2초는 연속 처치 알림을 쉰다
            if (said) _fx.NoteSkillNotice();
        }

        /// <summary>첫 명중 순간에 범위 흔들림을 거는 방식 (E7: 폭발·고리·비 — 투사체·광선은 킥, 장판·돔·떠오름·머리 위·회전은 흔들지 않는다)</summary>
        static bool ShakesArea(SkillVfxStyle style) => style == SkillVfxStyle.Burst || style == SkillVfxStyle.Ring || style == SkillVfxStyle.Rain;

        /// <summary>시전자 계보가 전설 이상인지 (스킬 조명)</summary>
        static bool IsLegendaryCaster(GuardianSlotView guardian, EchoView echo, string formId)
        {
            string lineageId = guardian != null && guardian.State != null ? guardian.State.LineageId : echo != null && echo.State != null ? echo.State.LineageId : null;
            var lineage = BattleUnitArt.Lineage(lineageId, formId);
            return lineage != null && lineage.grade >= ItemGrade.Legendary;
        }

        /// <summary>
        /// 방식별로 중심/대상을 다듬는다: 돔은 수선점·시전자 위, 회전은 시전자 둘레,
        /// 광선과 레인 투사체는 레인 끝까지 (레인 투사체는 대상을 따라가지 않고 곧게 꿰뚫는다)
        /// </summary>
        void AdjustForStyle(ref SkillFxRequest request, SkillVfxStyle style, SkillTargetType targetType, GuardianSlotView guardian, EchoView echo, Vector2 feet,
            Vector2 laneOrigin)
        {
            switch (style)
            {
                case SkillVfxStyle.Dome:
                    if (targetType == SkillTargetType.Shop)
                    {
                        request.Center = _shop.FieldPosition;
                        request.Radius = new Vector2(_shop.DomeWidth * 0.5f, _shop.DomeWidth * 0.5f * _mapper.Squash);
                    }
                    else if (targetType == SkillTargetType.Self)
                    {
                        request.Center = feet;
                        request.AnchorEcho = echo;
                        float width = guardian != null ? 90f : 64f;
                        request.Radius = new Vector2(width, width * _mapper.Squash);
                    }
                    break;
                case SkillVfxStyle.Orbit:
                    request.Center = feet;
                    break;
                case SkillVfxStyle.Beam:
                    request.BeamEnd = targetType == SkillTargetType.Lane ? LaneEnd(guardian, echo, laneOrigin, request.To) : request.To;
                    break;
                case SkillVfxStyle.Projectile:
                    if (targetType == SkillTargetType.Lane)
                    {
                        request.BeamEnd = LaneEnd(guardian, echo, laneOrigin, request.To);
                        request.Straight = true;
                    }
                    break;
                case SkillVfxStyle.OverHead:
                case SkillVfxStyle.Rise:
                    if (request.Target != null || !IsAllyTarget(targetType)) break;
                    if (targetType == SkillTargetType.Self || targetType == SkillTargetType.AlliesNearSelf)
                    {
                        if (echo != null) request.AnchorEcho = echo;
                        request.Center = feet;
                        request.To = guardian != null ? guardian.HeadPoint + Vector2.down * 60f : echo != null ? echo.HeadPoint + Vector2.down * 60f : request.To;
                    }
                    break;
            }
        }

        /// <summary>
        /// 전체 공격 스킬: 화면의 적마다 같은 그림이 물결처럼 차례로 톡톡 피어나고, 그 적의 피해 숫자도 물결이 닿을 때 뜬다.
        /// 그림은 입자가 넉넉할 때만 (모자라면 숫자 시간만 맞춘다)
        /// </summary>
        void SplashAllEnemies(in SkillFxRequest request)
        {
            int count = 0;
            foreach (var view in _enemies.Active)
            {
                if (view == null || !view.IsTargetable) continue;
                float delay = 0.08f + Mathf.Min(0.45f, (view.HitPoint - request.Center).magnitude / 1400f);
                RegisterDelay(view.Id, delay);
                if (count >= 14 || _fx.FreeParticles < SplashReserve) continue;
                _fx.SkillSplash(request, view, delay);
                count++;
            }
        }

        Vector2 ResolveCenter(BattleEvent e, SkillTargetType type, Vector2 casterFeet, EnemyView target)
        {
            if (type == SkillTargetType.Shop) return _shop.FieldPosition;
            if (e.Position != Vector2.zero) return _mapper.WorldToField(e.Position);
            switch (type)
            {
                case SkillTargetType.AllEnemies:
                case SkillTargetType.AllAllies:
                case SkillTargetType.AllEchoes:
                case SkillTargetType.AllGuardians:
                    return _shop.FieldPosition;
            }
            return target != null ? target.Rect.anchoredPosition : casterFeet;
        }

        /// <summary>
        /// 레인 스킬 끝: 지킴이는 자기 방위, 잔향이는 수선점 → 시전 위치 방향 (시뮬레이터 CollectLane과 같은 방향)으로 전장 가장자리 가까이.
        /// 시전 위치가 수선점 바로 위면 시뮬레이터는 지키는 방향을 쓰는데 뷰는 모르므로 진행 방향으로 대신한다
        /// </summary>
        Vector2 LaneEnd(GuardianSlotView guardian, EchoView echo, Vector2 origin, Vector2 fallback)
        {
            if (guardian != null) return _mapper.WorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(guardian.Slot), 2.3f)) + Vector2.up * 40f;
            Vector2 direction;
            if (origin.sqrMagnitude > 0.0004f) direction = origin.normalized;
            else if (echo != null && echo.State != null) direction = BattleGeometry.FromAngle(echo.State.Heading, 1f);
            else return fallback;
            var end = origin + direction * Mathf.Max(0.6f, 2.3f - origin.magnitude);
            return _mapper.WorldToField(end) + Vector2.up * 40f;
        }

        void ResolveCaster(int sourceId, out GuardianSlotView guardian, out EchoView echo)
        {
            guardian = null;
            echo = null;
            if (sourceId >= BattleSimulator.EchoIdBase)
            {
                if (_echoes.TryGet(sourceId, out var view)) echo = view;
            }
            else if (sourceId >= 0 && sourceId < _guardians.Length && _guardians[sourceId].State != null) guardian = _guardians[sourceId];
        }

        /// <summary>여러 적을 한꺼번에 치는 범위 스킬 (T3: 이것만 흔든다)</summary>
        static bool IsAreaTarget(SkillTargetType type) => type switch
        {
            SkillTargetType.AllEnemies => true,
            SkillTargetType.EnemiesInRange => true,
            SkillTargetType.EnemiesNearSelf => true,
            SkillTargetType.EnemiesNearTarget => true,
            SkillTargetType.Lane => true,
            _ => false,
        };

        static bool IsAllyTarget(SkillTargetType type) => type switch
        {
            SkillTargetType.Self => true,
            SkillTargetType.AlliesNearSelf => true,
            SkillTargetType.AllEchoes => true,
            SkillTargetType.AdjacentGuardians => true,
            SkillTargetType.AllGuardians => true,
            SkillTargetType.AllAllies => true,
            _ => false,
        };

        /// <summary>
        /// 이름 알림·전장 번쩍임을 띄울 만큼 드문 큰 스킬: 위기 한 방(수선점 위기), 불 밝히기 연계, 적 전체·아군 전체 대상.
        /// 평범한 쿨타임 스킬은 쿨타임이 길어도 아니다 (시작 친구의 16초 소환이 계속 화면을 번쩍이게 하지 않도록)
        /// </summary>
        static bool IsBigSkill(SkillDef skill)
        {
            if (skill == null) return false;
            switch (skill.trigger)
            {
                case SkillTrigger.OnShopLowHp:
                case SkillTrigger.OnLampLit:
                    return true;
                case SkillTrigger.OnBattleStart:
                case SkillTrigger.Passive:
                    return false;
            }
            return skill.target == SkillTargetType.AllEnemies || skill.target == SkillTargetType.AllAllies;
        }

        // ───────────── 보호막 / 회복 ─────────────

        void OnShieldGained(BattleEvent e)
        {
            // 돔 모양 그림(단추·우산·거품)만 돔으로 씌운다. 회복 천 패치처럼 돔이 아닌 그림의 스킬이 준 보호막은 거품으로
            string key = SkillVfxLook.IsKnown(e.Key) && SkillVfxLook.StyleOf(e.Key) == SkillVfxStyle.Dome ? e.Key : SkillVfx.ShieldBubble;
            var look = SkillVfxLook.Get(key);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(key, out sprite, out isArt);
            if (e.TargetId < 0)
            {
                // 같은 프레임에 여러 겹 (겹겹 방벽처럼 여러 친구가 동시에): 씌우는 연출은 한 번
                bool first = _shopShieldFrame != Time.frameCount;
                float delay = AllyGift(e.SourceId, ShopRecipient, _shop.DomeCenter, first);
                _shop.ShieldUp(sprite, isArt, look.Main);
                if (!first) return;
                _shopShieldFrame = Time.frameCount;
                float lift = (_shop.DomeCenter.y - _shop.FieldPosition.y) / Mathf.Max(1f, _shop.DomeWidth);
                _fx.ShieldPop(_shop.FieldPosition, _shop.DomeWidth, sprite, isArt, look.Main, null, e.Value, lift, delay);
            }
            else if (_echoes.TryGet(e.TargetId, out var echo))
            {
                bool allowed = AllowStatus(e.TargetId, AllyCode(SkillEffectType.Shield, 1));
                float delay = AllyGift(e.SourceId, e.TargetId, echo.BodyPoint, allowed);
                if (!allowed) return;
                _fx.ShieldPop(echo.Rect.anchoredPosition, 120f * echo.Rect.localScale.x, sprite, isArt, look.Main, echo, e.Value, 0.34f, delay);
            }
        }

        void OnShieldBroken(BattleEvent e)
        {
            if (e.TargetId < 0)
            {
                // 수선점 돔 깨짐 (E15): 깨지는 조각 + 흔들림 0.35 + 소프트 블루 빛 번쩍임 0.15 (알림 글자·킥 없이)
                _shop.ShieldBreak();
                _fx.ShieldShatter(_shop.DomeCenter, _shop.DomeWidth * 0.7f, UITheme.SoftBlue, 0f, Vector2.zero, false, false);
                _fx.AddTrauma(HitFeel.ShopShieldBreakTrauma);
                _fx.LightFlash(UITheme.SoftBlue, 0.15f, 0.4f);
                return;
            }
            if (TryEcho(e.TargetId, out var echo))
            {
                _fx.ShieldShatter(echo.BodyPoint, 90f, UITheme.SoftBlue, 0f, Vector2.zero, false, false);
                return;
            }
            if (TryEnemy(e.TargetId, out var enemy))
            {
                // 보호막 깨짐은 강타(T1): 깨는 일격이 닿는 순간 거품이 터지고 대상이 잠깐 멈췄다 밀린다 + "깨짐!" + 킥 2px (보스 3px).
                // 깨짐 연출은 그 적에 붙인다 — 비행·시전(delay) 동안 걸어간 몸 자리에서 거품(EnemyView.ShieldBreak)과 함께 터진다
                float delay = ContextDelay(e.SourceId, e.TargetId);
                var direction = CritDirection(e.SourceId, enemy);
                enemy.ShieldBreak(delay);
                enemy.QueueImpact(delay, HitTier.Heavy, direction);
                _fx.ShieldShatter(enemy.HitPoint, enemy.IsBoss ? 180f : 100f, UITheme.SoftBlue, delay, direction, enemy.IsBoss, true, enemy);
            }
        }

        void OnShopHealed(BattleEvent e)
        {
            AllyGift(e.SourceId >= 0 ? e.SourceId : e.TargetId, ShopRecipient, _shop.DomeCenter);
            _shopHealAccum += Mathf.Max(0f, e.Value);
            // 지킴이 맥박의 회복: 힐 맥박 방울이 문에 닿는 순간에 바로 보여준다 (모으지 않는다)
            if (IsPulseResult(e.SourceId))
            {
                _shopHealDelay = _pulseHealDelay;
                FlushShopHeal();
                return;
            }
            if (_shopHealTimer <= 0f) FlushShopHeal();
        }

        void FlushShopHeal()
        {
            float amount = _shopHealAccum;
            float delay = _shopHealDelay;
            _shopHealAccum = 0f;
            _shopHealDelay = 0f;
            _shopHealTimer = HealInterval;
            _shop.Heal();
            _fx.ShopHealed(_shop.DoorPosition, amount, delay);
        }

        void OnEchoHealed(BattleEvent e)
        {
            float amount = Mathf.Max(0f, e.Value);
            int free = -1;
            for (int i = 0; i < _echoHeals.Length; i++)
            {
                ref var slot = ref _echoHeals[i];
                if (slot.Id == e.TargetId)
                {
                    slot.Accum += amount;
                    bool now = slot.Timer <= 0f;
                    float delay = EchoHealGift(e, now);
                    if (now)
                    {
                        ShowEchoHeal(slot.Id, slot.Accum, delay);
                        slot.Accum = 0f;
                        slot.Timer = HealInterval;
                    }
                    return;
                }
                if (free < 0 && slot.Id < 0) free = i;
            }
            if (free < 0) free = (e.TargetId & 0x7fffffff) % _echoHeals.Length;
            _echoHeals[free] = new HealSlot { Id = e.TargetId, Accum = 0f, Timer = HealInterval };
            ShowEchoHeal(e.TargetId, amount, EchoHealGift(e, true));
        }

        /// <summary>
        /// 아군 전체 회복이면 시전자에게서 선물이 날아가 닿을 때 보여준다 (지금 보여주지 않는 회복은 받은 것으로만 센다).
        /// 힐 지킴이 맥박의 회복은 민트 고리가 그 잔향이에게 닿는 순간에
        /// </summary>
        float EchoHealGift(in BattleEvent e, bool show)
        {
            if (!_echoes.TryGet(e.TargetId, out var target)) return 0f;
            if (IsPulseResult(e.SourceId)) return PulseRingDelay(target);
            return AllyGift(e.SourceId, e.TargetId, target.BodyPoint, show);
        }

        void ShowEchoHeal(int id, float amount, float delay)
        {
            if (!_echoes.TryGet(id, out var echo)) return;
            echo.PlayHeal();
            _fx.EchoHealed(echo, amount, delay);
        }

        // ───────────── 상태 이상 ─────────────

        void OnStatusApplied(BattleEvent e)
        {
            var type = (SkillEffectType)Mathf.RoundToInt(e.Value);
            var parsed = ParseStatusKey(e.Key);
            string key = SkillVfxLook.IsKnown(parsed.Vfx) ? parsed.Vfx : SkillVfxLook.StatusKey(type);
            var look = SkillVfxLook.Get(key);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(key, out sprite, out isArt);
            bool debuff = key == SkillVfx.DebuffMark;

            switch (parsed.Kind)
            {
                case 1:
                {
                    if (!_echoes.TryGet(e.TargetId, out var echo)) break;
                    bool allowed = AllowStatus(e.TargetId, AllyCode(type, 1));
                    float delay = AllyGift(e.SourceId, e.TargetId, echo.BodyPoint, allowed);
                    if (allowed) _fx.AllyStatus(type, echo.HeadPoint, echo, sprite, isArt, look, debuff, delay);
                    if (allowed && IsEnemyDebuff(e)) EnemyDebuffSticker(type, echo, echo.BodyPoint, echo.State != null ? echo.State.DebuffTimer : 1f);
                    break;
                }
                case 2:
                {
                    int slot = e.TargetId;
                    if (slot < 0 || slot >= _guardians.Length || _guardians[slot].State == null) break;
                    var head = _guardians[slot].HeadPoint;
                    bool allowed = AllowStatus(slot, AllyCode(type, 2));
                    float delay = AllyGift(e.SourceId, GuardianRecipient(slot), head + Vector2.down * 50f, allowed);
                    if (allowed) _fx.AllyStatus(type, head, null, sprite, isArt, look, debuff, delay);
                    // 녹이 슨 지킴이: 발판에 녹 얼룩 (ATK-03)
                    if (allowed && IsEnemyDebuff(e) && type == SkillEffectType.Slow)
                        _fx.RustStain(_guardians[slot].FeetPosition, _guardians[slot].State.DebuffTimer, null, true);
                    break;
                }
                case 3:
                {
                    bool allowed = AllowStatus(-1, AllyCode(type, 3));
                    float delay = AllyGift(e.SourceId, ShopRecipient, _shop.DomeCenter, allowed);
                    if (!allowed) break;
                    _shop.Heal();
                    _fx.AllyStatus(type, _shop.DomeCenter + Vector2.up * 50f, null, sprite, isArt, look, debuff, delay);
                    break;
                }
                default:
                {
                    // 연쇄: 앞 대상에서 이 적으로 빛이 튀어 가는 순서대로 (같은 시전 안에서만)
                    if (type == SkillEffectType.Chain && InCast && e.SourceId == _castSource)
                    {
                        OnChainHop(e, sprite, isArt, look);
                        break;
                    }
                    if (!_enemies.TryGet(e.TargetId, out var enemy)) break;
                    if (type == SkillEffectType.Reveal) enemy.Reveal();
                    // 태엽쥐가 곁의 적을 감아 고쳐 줌 (Mender): 그 적 머리 위 태엽 열쇠 + 노란 고리 (회복 표시는 틱마다라 간격을 둔다)
                    if (type == SkillEffectType.Heal && IsMenderSource(e.SourceId))
                    {
                        if (AllowStatus(e.TargetId, MenderStamp)) _fx.WindUp(enemy);
                        break;
                    }
                    // 투사체·레인·물결이 아직 닿지 않았으면 닿는 순간에 맞춰 띄운다
                    float delay = ContextDelay(e.SourceId, e.TargetId);
                    if (AllowStatus(e.TargetId, (int)type)) _fx.EnemyStatus(type, enemy, sprite, isArt, look, delay);
                    break;
                }
            }
        }

        /// <summary>아군 상태 연출 간격 기록 번호 (적 상태 번호와 겹치지 않게, 공격력·공속 강화는 같은 왕관이라 하나로)</summary>
        static int AllyCode(SkillEffectType type, int kind)
        {
            if (type == SkillEffectType.BuffAttackSpeed) type = SkillEffectType.BuffAttack;
            return 1000 * kind + (int)type;
        }

        /// <summary>같은 대상·같은 상태 연출이 방금 떴으면 false (적 회복 오라처럼 틱마다 오는 상태용)</summary>
        bool AllowStatus(int id, int type)
        {
            float now = Time.time;
            for (int i = 0; i < _statusStamps.Length; i++)
            {
                ref var stamp = ref _statusStamps[i];
                if (stamp.Id != id || stamp.Type != type) continue;
                if (now - stamp.Time < StatusRepeat) return false;
                stamp.Time = now;
                return true;
            }
            _statusStamps[_statusCursor] = new StatusStamp { Id = id, Type = type, Time = now };
            _statusCursor = (_statusCursor + 1) % _statusStamps.Length;
            return true;
        }

        /// <summary>"echo:키" / "guardian:키" / "키" 해석 (결과를 캐시해 문자열을 매번 자르지 않는다)</summary>
        StatusKey ParseStatusKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return default;
            if (_statusKeys.TryGetValue(key, out var parsed)) return parsed;
            parsed = default;
            if (key.StartsWith(EchoPrefix, StringComparison.Ordinal))
            {
                parsed.Kind = 1;
                parsed.Vfx = key.Length > EchoPrefix.Length ? key.Substring(EchoPrefix.Length) : null;
            }
            else if (key.StartsWith(GuardianPrefix, StringComparison.Ordinal))
            {
                parsed.Kind = 2;
                parsed.Vfx = key.Length > GuardianPrefix.Length ? key.Substring(GuardianPrefix.Length) : null;
            }
            else if (key.StartsWith(ShopPrefix, StringComparison.Ordinal))
            {
                parsed.Kind = 3;
                parsed.Vfx = key.Length > ShopPrefix.Length ? key.Substring(ShopPrefix.Length) : null;
            }
            else parsed.Vfx = key;
            if (_statusKeys.Count < 256) _statusKeys[key] = parsed;
            return parsed;
        }

        /// <summary>태엽 감기 연출 간격 기록용 상태 번호 (SkillEffectType 값과 겹치지 않게)</summary>
        const int MenderStamp = 501;

        /// <summary>적 기믹이 건 약화 (녹 = 감속, 갉아먹힘 = 표식, 시전자 없음 + 약화 표식 키)</summary>
        static bool IsEnemyDebuff(in BattleEvent e) => e.SourceId < 0 && e.Key != null && e.Key.EndsWith(SkillVfx.DebuffMark, StringComparison.Ordinal);

        /// <summary>회복을 준 쪽이 곁의 적을 고쳐 주는 적(태엽쥐, Mender)인지</summary>
        bool IsMenderSource(int sourceId) =>
            _enemies.TryGet(sourceId, out var source) && source.State != null && source.State.Def != null && (source.State.Def.gimmicks & EnemyGimmick.Mender) != 0;

        /// <summary>적 기믹 약화 스티커: 녹(감속) = 주황 녹 얼룩이 약화 동안, 갉아먹힘(표식) = 분홍빨강 구멍 0.4초 + 처음 한 번 안내</summary>
        void EnemyDebuffSticker(SkillEffectType type, EchoView echo, Vector2 body, float duration)
        {
            if (type == SkillEffectType.Slow)
            {
                _fx.RustStain(body, duration, echo, false);
                return;
            }
            if (type != SkillEffectType.Mark) return;
            _fx.GnawMark(body, echo);
            ShowGnawTip();
        }

        const string GnawTipKey = "fx.tip.moth_gnaw";

        /// <summary>좀나방이 천 잔향이를 처음 갉을 때 한 번만: "좀나방은 천을 갉아요!" (기기당 한 번)</summary>
        void ShowGnawTip()
        {
            if (_notice == null || PlayerPrefs.GetInt(GnawTipKey, 0) != 0) return;
            // 디스크에는 일시정지 · 종료 때 (perf PF-5 H5 — 전투 한가운데 동기 쓰기를 하지 않는다)
            PlayerPrefs.SetInt(GnawTipKey, 1);
            var moth = _sim != null && Game.Battle != null && Game.Battle.Config != null ? Game.Battle.Config.GetEnemy("enemy_moth") : null;
            _notice.Show("좀나방은 천을 갉아요!", "천으로 된 친구는 좀나방에게 약해요. 멀리서 먼저 쓰러뜨려 주세요", moth != null ? moth.sprite : null, Color.white,
                UITheme.DustyPink, 2.4f);
        }

        const string EchoPrefix = "echo:";
        const string GuardianPrefix = "guardian:";
        const string ShopPrefix = "shop:";

        // ───────────── 기믹 ─────────────

        void OnEnemySplit(BattleEvent e)
        {
            var spawned = FindEnemy(e.TargetId);
            if (spawned == null) return;
            Vector2 origin;
            if (_enemies.TryGet(e.SourceId, out var source)) origin = source.Rect.anchoredPosition;
            else if (e.Position != Vector2.zero) origin = _mapper.WorldToField(e.Position);
            else origin = _mapper.WorldToField(spawned.Position);
            var to = _mapper.WorldToField(spawned.Position);
            _fx.SplitPop(origin + Vector2.up * 30f, to + Vector2.up * 24f, SkillVfxLook.Get(SkillVfx.SplitYarn).Main);
            _enemies.RegisterSplit(e.TargetId, origin);
        }

        void OnEnemyRevealed(BattleEvent e)
        {
            if (!_enemies.TryGet(e.TargetId, out var view)) return;
            view.Reveal();
            // 드러냄 스킬의 상태 연출과 같은 순간에 겹치면 한 번만
            if (AllowStatus(e.TargetId, (int)SkillEffectType.Reveal)) _fx.RevealFlash(view);
        }

        void OnEnemyEnraged(BattleEvent e)
        {
            if (!_enemies.TryGet(e.TargetId, out var view)) return;
            _fx.EnrageBurst(view);
            if (!view.IsBoss) return;
            _fx.AddTrauma(HitFeel.BossEnrageTrauma);
            _hud.OnBossSpawned();
            if (_notice == null || view.State == null || view.State.Def == null) return;
            var def = view.State.Def;
            // 보스 분노는 드물게 한 번 오므로 문장을 그때 만든다
            _notice.Show(KoreanText.WithSubject(def.name) + " 화가 났어요!", "더 빠르고 아프게 몰아쳐요", def.sprite, Color.white, UITheme.Danger, 1.8f);
        }

        void OnTokenSummoned(BattleEvent e)
        {
            Vector2 feet;
            var token = FindEcho(e.TargetId);
            if (token != null) feet = _mapper.WorldToField(token.Position);
            else
            {
                ResolveCaster(e.SourceId, out var guardian, out var echo);
                feet = guardian != null ? guardian.FeetPosition : echo != null ? echo.Rect.anchoredPosition : _shop.FieldPosition;
            }
            var look = SkillVfxLook.Get(SkillVfx.SummonCircle);
            Sprite sprite = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(SkillVfx.SummonCircle, out sprite, out isArt);
            _fx.TokenPop(feet, look.Main, sprite, isArt);
        }

        /// <summary>
        /// 치명타 (T2 / FX-JUICE E5): CriticalHit은 같은 공격의 EnemyHit보다 먼저 온다. 기본 공격이면 이미 예약된 명중 그림을 치명타 그림으로 키우고,
        /// 명중이 닿는 순간 금빛 치명타 겹침 + 금색 쾅 숫자(치명타 전용, 별 배지·광택 — 보이는 적이면 머리 위에서: 숫자를 그 적에 붙이면
        /// BattleFx.AnchorNumber가 머리 높이를 재 두고 뜨는 순간 0~70px 올린다, 여기서 더하지 않는다) + 대상 정지(잔향이 공격은 멈춤 없이 가볍게)·공격자 강조색 붉힘 +
        /// 맞은 쪽으로 한 번 밀리는 킥 3px (보스 2px, 0.5초에 한 번 — 흔들림 trauma는 쓰지 않는다).
        /// 뒤따르는 EnemyHit은 같은 값이면 숫자를 다시 띄우지 않는다 (IsCritEcho).
        /// 보호막 적: CriticalHit은 흡수 전에 오므로 보호막이 받아 낼 몫을 빼고 금색 숫자를 띄운다 (막은 몫은 EnemyHit의 "막음 N").
        /// 보호막이 다 받아 내면 금색 숫자·별 폭발·흔들림 없이 "막음 N"만 — 체력이 줄지 않은 일격을 치명타로 보이지 않게 (FX-03 / ATK-11).
        /// </summary>
        void OnCriticalHit(BattleEvent e)
        {
            int id = EnemyId(e);
            Vector2 point;
            bool visible = _enemies.TryGet(id, out var view);
            if (visible) point = view.HitPoint;
            else if (e.Position != Vector2.zero) point = _mapper.WorldToField(e.Position) + Vector2.up * 40f;
            else return;

            float value = e.Value;
            int frame = Time.frameCount;
            // 이번 일격 중 보호막이 받아 낼 몫 (시뮬레이터: 흡수 = min(보호막, 피해))
            var state = visible ? view.State : FindEnemy(id);
            float shield = state != null ? Mathf.Max(0f, state.Shield) : 0f;
            float landed = e.Value - Mathf.Min(shield, e.Value);
            _critFrame = frame;
            _critTarget = id;
            _critValue = value;
            if (landed < 0.5f) return;

            // 지킴이·잔향이 기본 공격은 투사체가, 스킬은 그 연출(투사체·레인·연쇄·물결)이 닿는 순간에 맞춘다
            float delay = ContextDelay(e.SourceId, id);
            var hitDirection = visible ? CritDirection(e.SourceId, view) : Vector2.zero;
            if (visible)
            {
                if (IsBasicHitOn(id)) _fx.PromoteImpact(id);
                _fx.CritImpact(view, delay);
                // 잔향이 기본 공격의 치명타는 잔향이 반응 (멈춤 없음 — 뒤따르는 피격도 echo라 합쳐도 그대로)
                view.QueueImpact(delay, HitTier.Crit, hitDirection, CritAccent(e.SourceId), true, IsEchoBasicOn(id));
            }
            else _fx.CritBurst(point, false, delay);
            var kick = hitDirection.sqrMagnitude > 1e-4f ? hitDirection : point - _shop.FieldPosition;
            _fx.Kick(kick, visible && view.IsBoss ? HitFeel.KickBossCrit : HitFeel.KickCrit, KickChannel.Crit, delay);
            // 피격 → 치명타 순서로 왔으면 이미 띄운 숫자를 치명타 모양으로 바꾸고, 아니면 새로 띄운다 (닿는 순간까지 적을 따라가고 머리 위로 올라 뜬다 —
            // 바꾼 숫자도 피격 때 이 적에 붙여 두었으니 같은 높이로)
            if (_hitFrame == frame && _hitTarget == id && SameDamage(_hitValue, value) && _hitNumber >= 0) _fx.UpgradeToCrit(_hitNumber);
            else _fx.AnchorNumber(_fx.CritNumber(id, point + new Vector2(0f, 40f), landed, delay), visible ? view : null);
        }

        /// <summary>치명타 넉백 방향: 지금 맥락의 공격이 날아온 쪽 (모르면 0 = 제자리 흔들림)</summary>
        Vector2 CritDirection(int sourceId, EnemyView view)
        {
            if (_contextFrame != Time.frameCount) return Vector2.zero;
            switch (_context)
            {
                case Context.Basic: return sourceId == _basicSlot ? _basicDir : Vector2.zero;
                case Context.EchoBasic: return _echoDir;
                case Context.Cast: return Direction(_castFrom, view.HitPoint);
            }
            return Vector2.zero;
        }

        /// <summary>치명타 붉힘 색: 지금 맥락 공격자의 계보 강조색 (지킴이·잔향이 기본 공격) / 시전 스킬 강조색 / 모르면 크림</summary>
        Color CritAccent(int sourceId)
        {
            if (_contextFrame != Time.frameCount) return UITheme.Cream;
            switch (_context)
            {
                case Context.Basic:
                    if (sourceId >= 0 && sourceId < _guardians.Length && _guardians[sourceId].State != null) return AttackFxCatalog.Look(_guardians[sourceId].State.LineageId).Accent;
                    break;
                case Context.EchoBasic:
                    if (_echoes.TryGet(_echoSource, out var echo) && echo.State != null) return AttackFxCatalog.Look(echo.State.LineageId).Accent;
                    break;
                case Context.Cast:
                    return _castLook.Accent;
            }
            return UITheme.Cream;
        }

        /// <summary>같은 프레임·같은 적·같은 피해의 치명타 숫자가 이미 떠 있는지</summary>
        bool IsCritEcho(int enemyId, float value) =>
            _critFrame == Time.frameCount && _critTarget == enemyId && SameDamage(_critValue, value);

        /// <summary>같은 일격의 피해인지 (1 이내, 아주 큰 값은 실수 오차만큼 — int로 바꾸지 않는다)</summary>
        static bool SameDamage(float a, float b) => Mathf.Abs(a - b) <= Mathf.Max(1f, Mathf.Abs(a) * 1e-5f);

        void RememberHitNumber(int enemyId, float value, int number)
        {
            _hitFrame = Time.frameCount;
            _hitTarget = enemyId;
            _hitValue = value;
            _hitNumber = number;
        }

        void OnLampDrained(BattleEvent e)
        {
            if (_drainTimer > 0f) return;
            _drainTimer = DrainInterval;
            bool hasTarget = _enemies.TryGet(e.SourceId, out var view) || _enemies.TryGet(e.TargetId, out view);
            _fx.LampDrain(_shop.DoorPosition, hasTarget ? view.HitPoint : Vector2.zero, hasTarget, LampButtonPoint());
            _lamp.OnDrained();
        }

        /// <summary>불 밝히기 버튼 자리 (전장 로컬 — 버튼은 전장 아래 띠에 있으므로 전장 아래 가장자리 바깥)</summary>
        Vector2 LampButtonPoint()
        {
            if (_lamp == null || _actorLayer == null) return Vector2.zero;
            var local = _actorLayer.InverseTransformPoint(_lamp.transform.position);
            return new Vector2(local.x, local.y);
        }

        // ───────────── 조회 ─────────────

        bool TryEcho(int id, out EchoView echo)
        {
            if (id >= BattleSimulator.EchoIdBase) return _echoes.TryGet(id, out echo);
            echo = null;
            return false;
        }

        bool TryEnemy(int id, out EnemyView enemy)
        {
            if (_enemies.TryGet(id, out enemy)) return true;
            enemy = null;
            return false;
        }

        EnemyState FindEnemy(int id)
        {
            var enemies = _sim?.Enemies;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (enemies[i] != null && enemies[i].Id == id) return enemies[i];
            }
            return null;
        }

        EchoState FindEcho(int id)
        {
            var echoes = _sim?.Echoes;
            int count = echoes != null ? echoes.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (echoes[i] != null && echoes[i].Id == id) return echoes[i];
            }
            return null;
        }
    }
}
