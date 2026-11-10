using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 적 쪽 이벤트 연출 (ATK-03 / ATK-04 / ATK-10 / ATK-14 / ATK-15 / ATK-06 자동 품질):
    ///   EnemyStrike(잔향이): 적 종류별 공격 그림이 맞은 잔향이 몸에서 터지고 적은 그쪽으로 몸을 내민다, 잔향이는 흰 번쩍임 50%로 움찔 (숫자 없음).
    ///     공격 그림은 적마다 제 주기(EnemyAttackLook.Interval ÷ 배속)보다 자주 그리지 않는다 — 시뮬레이터는 막힌 적마다 0.5초로 똑같이 알린다.
    ///   EnemyStrike(수선점): 적이 들어온 방향의 문 가장자리에 공격 그림 + 나뭇조각(첫 프레임 ×1.25, 바닥에 튀는 조각) + 문 쪽 킥·흔들림 + 건물 용수철 눌림
    ///     (보스 포위는 더 크게, FX-JUICE E14), 그 방향 링 칸이 0.6초 빨갛게 번쩍 + "!" — 보호막이 다 막으면 돔이 출렁.
    ///     뒤따르는 ShopDamaged는 건물 흔들림·"-N"만 (공격 그림은 여기서 이미 그렸다).
    ///   보스 포위 예비 동작(EnemyState.SiegeWindup): 문 앞 바닥에 붉은 텔레그래프 원(매 프레임 차오름을 따라간다) + 몸을 뒤로 젖힘(EnemyView).
    ///   GuardianPulse: 지원 = 리본·왕관·잔향이 별 / 힐 = 민트 고리·방울 (뒤따르는 회복 숫자는 방울·고리가 닿는 순간에).
    ///   EnemySpawned: 보스가 부른 부하면 보스 발밑 날개가루 소용돌이 (일반 등장 소용돌이·보스 그림자 웅덩이는 EnemyViewPool이 그릴 때).
    ///   정전: 빛 특성 계보의 기본 공격이 닿는 자리에 빛 구멍 (StageAmbienceView.LightHole + 빛 웅덩이).
    ///   수선점 내구도 30% 미만이면 문에서 불씨가 피어오른다. 처치 5마리마다 단추가 상단 바로 날아간다.
    ///   프레임이 계속 느리면 이펙트 품질을 한 단계 낮춘다 (토스트 한 번). 일부러 낮춘 프레임 목표(30fps·잠자기)는 느림으로 세지 않는다.
    /// </summary>
    public partial class BattleView
    {
        [Header("공격 연출 시험장 (개발 빌드)")]
        [SerializeField] FxGalleryView _gallery;

        const float EmberInterval = 0.18f;
        const string QualityToast = "화면이 부드럽도록 이펙트를 조금 줄였어요";
        /// <summary>첫 불 밝히기 큰 배너를 본 세이브 표시 (§13.5 d — 예전 PlayerPrefs 키와 같은 글자, 이제 PlayerData.flags에 세이브마다)</summary>
        const string LampIntroKey = MetaFlags.LampIntroSeen;

        static bool _qualityToastShown;

        readonly FxQualityMonitor _quality = new();
        int _shopStrikeFrame = -1;
        int _shopStrikeSource = -1;
        int _pulseFrame = -1;
        int _pulseSlot = -1;
        float _pulseHealDelay;
        float _pulseRadius;
        int _windupBoss = -1;
        int _minionFrame = -1;
        // 적 공격 그림 주기 (적 ID별 마지막으로 그린 시각, 작은 고리 버퍼)
        const int StrikeMemory = 32;
        readonly int[] _strikeIds = new int[StrikeMemory];
        readonly float[] _strikeTimes = new float[StrikeMemory];
        int _strikeCount;
        float _emberTimer;
        int _killCount;

        bool _galleryShown;

        /// <summary>개발용 연출 시험장이 열려 있으면 전투 이벤트 연출을 멈춘다 (시험장 연출만 보이게)</summary>
        bool GalleryOpen => _gallery != null && _gallery.IsOpen;

        /// <summary>시험장이 열리는 순간 실제 전투의 적·잔향이를 흐려 치운다. 열려 있으면 true</summary>
        bool SyncGallery()
        {
            bool open = GalleryOpen;
            if (open == _galleryShown) return open;
            _galleryShown = open;
            if (open)
            {
                _enemies.ExitAll();
                _echoes.DissolveAll();
            }
            return open;
        }

        void OnRebindEnemyFx()
        {
            _killCount = 0;
            _windupBoss = -1;
            _fx.EndSiegeTelegraph();
            _pulseFrame = -1;
            _shopStrikeFrame = -1;
            _strikeCount = 0;
        }

        /// <summary>프레임 목표·이펙트 품질 설정이 바뀌면 자동 품질 판정을 처음부터 다시 센다</summary>
        void OnFxSettingsChanged()
        {
            _quality.Reset();
            OnSettingsChangedBattle4();
        }

        // ───────────── 매 프레임 ─────────────

        void TickEnemyPresentation(float dt)
        {
            if (_galleryShown)
            {
                _windupBoss = -1;
                return;
            }
            TickSiegeTelegraph();
            TickShopEmbers(dt);
            TickQuality();
        }

        /// <summary>
        /// 보스 예비 동작: 문 앞 바닥의 붉은 원이 매 프레임 시뮬레이터의 차오름(SiegeWindup 0→1)을 그대로 따라간다.
        /// 분노(빨라짐)·기절·넉백(차오름이 0으로)도 그대로 보이고, 공격하거나 끊기면 원이 바로 걷힌다 (원은 하나만)
        /// </summary>
        void TickSiegeTelegraph()
        {
            if (_sim == null) return;
            EnemyView winding = null;
            foreach (var view in _enemies.Active)
            {
                if (!view.IsBoss || view.State == null || !view.IsTargetable || view.State.SiegeWindup <= 0f) continue;
                // 이미 그리던 보스가 계속 차오르면 그 보스를 (보스가 둘이어도 원이 이리저리 옮겨 다니지 않게)
                if (winding == null || view.Id == _windupBoss) winding = view;
            }
            if (winding == null)
            {
                if (_windupBoss >= 0) _fx.EndSiegeTelegraph();
                _windupBoss = -1;
                return;
            }
            if (winding.Id != _windupBoss) _fx.EndSiegeTelegraph();
            _windupBoss = winding.Id;
            var position = winding.State.Position;
            var outward = position.sqrMagnitude > 1e-6f ? position.normalized : Vector2.down;
            var ground = _mapper.WorldToField(outward * (BattleGeometry.ShopRadius + 0.06f));
            _fx.SiegeTelegraph(ground, 170f * _mapper.ActorScale, winding.State.SiegeWindup);
        }

        /// <summary>수선점 내구도 30% 미만: 문에서 불씨가 피어오른다</summary>
        void TickShopEmbers(float dt)
        {
            if (_sim == null || _sim.ShopMaxHp <= 0f || _sim.ShopHp / _sim.ShopMaxHp >= EnemyFxCatalog.LowHpRatio || _sim.ShopHp <= 0f) return;
            _emberTimer -= dt;
            if (_emberTimer > 0f) return;
            _emberTimer = EmberInterval;
            _fx.ShopEmber(_shop.DoorPosition);
        }

        /// <summary>
        /// 5초 평균 프레임이 느린 기준(22ms, 프레임 목표를 낮췄으면 목표 × 1.25)을 넘는 일이 60초 안에 두 번이면
        /// 이펙트 품질 한 단계 낮춤 (자동 판정 중일 때만).
        /// 포커스 없는 창은 OS·에디터가 프레임을 늦추므로 세지 않고 처음부터 다시 센다 (M01: 알트탭 동안 품질이 떨어지거나 토스트가 뜨지 않게)
        /// </summary>
        void TickQuality()
        {
            if (!Application.isFocused) { _quality.Reset(); return; }
            if (GalleryOpen) return;
            if (!_quality.Sample(Time.unscaledDeltaTime, FrameTarget())) return;
            if (!GameSettings.LowerFxQualityAuto()) return;
            if (_qualityToastShown || UIManager.Instance == null) return;
            _qualityToastShown = true;
            UIManager.Instance.ShowToast(QualityToast);
        }

        /// <summary>지금 프레임 목표 (설정 30/60 · 잠자기 30). 목표를 두지 않았으면 모바일 기본 30 / 그 밖 60</summary>
        static int FrameTarget()
        {
            // FramePacing이 실제로 맞추는 값 (90 Hz 화면 90 · 잠자기 30 — perf PF-1 S9: 60을 주고 45로 돌 때 느림으로 세지 않게).
            // 옛 경로가 targetFrameRate를 더 낮게 써 두었으면(FramePacing으로 옮기기 전 잠자기 · 30 설정) 그쪽
            int paced = FramePacing.TargetFps;
            int requested = Application.targetFrameRate;
            return requested > 0 && requested < paced ? requested : paced;
        }

        // ───────────── 적의 공격 ─────────────

        void OnEnemyStrike(BattleEvent e)
        {
            bool hasView = _enemies.TryGet(e.SourceId, out var enemy);
            var state = hasView ? enemy.State : FindEnemy(e.SourceId);
            string enemyId = !string.IsNullOrEmpty(e.Key) ? e.Key : state != null && state.Def != null ? state.Def.id : null;
            bool boss = hasView ? enemy.IsBoss : state != null && state.IsBoss;
            Vector2 from;
            if (hasView) from = enemy.HitPoint;
            else if (state != null) from = _mapper.WorldToField(state.Position) + Vector2.up * (40f * _mapper.ActorScale);
            else from = _mapper.WorldToField(e.Position);
            bool hidden = hasView && enemy.IsHidden;

            if (e.TargetId >= 0) StrikeEcho(e.SourceId, e.TargetId, e.Position, hasView ? enemy : null, enemyId, boss, from, hidden);
            else StrikeShop(e.SourceId, e.Position, e.Value, hasView ? enemy : null, enemyId, boss, from, hidden);
        }

        /// <param name="sourceId">때린 적의 시뮬레이터 ID (공격 그림 주기를 적마다 잰다)</param>
        void StrikeEcho(int sourceId, int echoId, Vector2 position, EnemyView enemy, string enemyId, bool boss, Vector2 from, bool hidden)
        {
            bool hasEcho = _echoes.TryGet(echoId, out var echo);
            Vector2 to;
            float ground;
            if (hasEcho)
            {
                to = echo.BodyPoint;
                ground = echo.Rect.anchoredPosition.y;
            }
            else
            {
                var state = FindEcho(echoId);
                var feet = state != null ? _mapper.WorldToField(state.Position) : _mapper.WorldToField(position);
                to = feet + Vector2.up * (EchoBodyLift * _mapper.ActorScale);
                ground = feet.y;
            }
            if (enemy != null) enemy.Strike(to - from);
            // 좀나방(천 갉아먹기)이 천 특성 잔향이를 물면 분홍빨강 구멍 스티커 (처음 한 번은 안내도)
            if (hasEcho && echo.State != null && IsFabricEater(enemy, enemyId) && HasTrait(echo.State.LineageId, echo.State.FormId, TraitType.Fabric))
            {
                _fx.GnawMark(to, echo);
                ShowGnawTip();
            }
            // 공격 그림만 적마다 제 주기로 (움찔·몸 내밀기는 매번 — 맞고 있다는 건 늘 보인다)
            if (StrikeArtDue(sourceId, EnemyFxCatalog.Look(enemyId, boss).Interval))
            {
                _fx.EnemyStrike(new EnemyStrikeRequest
                {
                    EnemyId = enemyId,
                    Boss = boss,
                    From = from,
                    To = to,
                    Echo = hasEcho ? echo : null,
                    Hidden = hidden,
                    Ground = ground,
                });
            }
            // 반투명한 빛의 존재라 흰 번쩍임은 약하게, 피해 숫자는 띄우지 않는다
            if (hasEcho) echo.PlayHurt(0.5f);
        }

        /// <summary>
        /// 이 적의 잔향이 공격 그림을 지금 그려도 되는지 (ATK-03 주기: 먼지 1.0 · 나방 0.6 · 녹 1.4 · 실뭉치 0.9 · 그림자 0.8 · 골무 1.2 · 태엽 1.0 · 보스 2.0초,
        /// 배속만큼 짧게). 그리면 시각을 남긴다. 기억은 적 32마리까지 (넘치면 가장 오래된 기억을 덮는다)
        /// </summary>
        bool StrikeArtDue(int enemyId, float interval)
        {
            float now = Time.time;
            int speed = Game.Battle != null ? Mathf.Max(1, Game.Battle.Speed) : 1;
            // 시뮬레이터가 알리는 간격(0.5초)과 프레임 오차로 한 번 더 건너뛰지 않게 조금 너그럽게
            float gap = interval / speed - 0.03f;
            int oldest = 0;
            for (int i = 0; i < _strikeCount; i++)
            {
                if (_strikeIds[i] == enemyId)
                {
                    if (now - _strikeTimes[i] < gap) return false;
                    _strikeTimes[i] = now;
                    return true;
                }
                if (_strikeTimes[i] < _strikeTimes[oldest]) oldest = i;
            }
            int slot = _strikeCount < StrikeMemory ? _strikeCount++ : oldest;
            _strikeIds[slot] = enemyId;
            _strikeTimes[slot] = now;
            return true;
        }

        /// <param name="edgeWorld">맞은 자리 (적이 들어온 방향의 수선점 가장자리, 월드)</param>
        void StrikeShop(int enemyId, Vector2 edgeWorld, float damage, EnemyView enemy, string defId, bool boss, Vector2 from, bool hidden)
        {
            float unit = _mapper.ActorScale;
            var shop = _shop.FieldPosition;
            if (edgeWorld.sqrMagnitude < 1e-6f) edgeWorld = _mapper.FieldToWorld(from);
            var side = _mapper.WorldToField(edgeWorld) - shop;
            side = side.sqrMagnitude > 1f ? side.normalized : Vector2.down;
            var edge = _shop.DoorPosition + side * (EnemyFxCatalog.ShopEdgeOffset * unit) + Vector2.up * (30f * unit);
            // 보호막이 이 공격을 다 받아 내면 (EnemyStrike는 흡수 전에 온다) 나뭇조각 대신 돔이 출렁인다
            bool absorbed = _sim != null && _sim.ShopShield > 0.01f && _sim.ShopShield >= damage - 0.01f;
            var look = EnemyFxCatalog.Look(defId, boss);

            if (enemy != null)
            {
                enemy.Strike(edge - from);
                if (boss) enemy.Lunge();
            }
            _fx.EnemyStrike(new EnemyStrikeRequest
            {
                EnemyId = defId,
                Boss = boss,
                From = from,
                To = edge,
                Shop = true,
                Hidden = hidden,
                Ground = shop.y + side.y * 24f * unit,
            });
            _fx.ShopImpact(edge, side, look.ShopHitSize, boss, absorbed);
            _ring.FlashSlot(EnemyFxCatalog.ShopStrikeSlot(edgeWorld), boss);
            if (absorbed) _shop.DomeHit();
            _shopStrikeFrame = Time.frameCount;
            _shopStrikeSource = enemyId;
        }

        /// <summary>ShopDamaged 앞에 같은 적의 EnemyStrike가 왔는지 (공격 그림을 두 번 그리지 않게)</summary>
        bool StruckShopThisFrame(int enemyId) => _shopStrikeFrame == Time.frameCount && _shopStrikeSource == enemyId;

        // ───────────── 지원 / 힐 맥박 (ATK-10) ─────────────

        void OnGuardianPulse(BattleEvent e)
        {
            int slot = e.SourceId;
            if (slot < 0 || slot >= _guardians.Length) return;
            var guardian = _guardians[slot];
            if (guardian.State == null) return;
            var config = Game.Battle != null ? Game.Battle.Config : null;
            bool heal = e.Key == BattleEventKeys.PulseHeal || (e.Key == null && Mathf.RoundToInt(e.Value) == (int)CombatStyle.Heal);
            _pulseFrame = Time.frameCount;
            _pulseSlot = slot;
            guardian.PlayPulse();

            if (heal)
            {
                _pulseRadius = (config != null ? config.healEchoRadius : 0.8f) * _mapper.Ppu;
                _pulseHealDelay = _fx.HealPulse(guardian.FeetPosition, guardian.MuzzlePosition, _pulseRadius, _shop.DoorPosition);
                return;
            }

            _pulseHealDelay = 0f;
            var accent = AttackFxCatalog.Look(guardian.State.LineageId).Accent;
            _fx.SupportPulse(guardian.FeetPosition, guardian.MuzzlePosition, accent);
            for (int side = -1; side <= 1; side += 2)
            {
                int neighbor = (slot + side + BattleGeometry.SlotCount) % BattleGeometry.SlotCount;
                if (neighbor >= _guardians.Length || _guardians[neighbor].State == null) continue;
                _fx.SupportRibbon(guardian.MuzzlePosition, _guardians[neighbor].HeadPoint, accent);
            }
            float radius = config != null ? config.supportEchoBuffRadius : 0.8f;
            var center = BattleGeometry.GuardianPosition(slot);
            var echoes = _sim?.Echoes;
            int count = echoes != null ? echoes.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var echo = echoes[i];
                if (echo == null || !echo.Alive || (echo.Position - center).sqrMagnitude > radius * radius) continue;
                if (_echoes.TryGet(echo.Id, out var view)) _fx.SupportSpark(view, BattleFx.RibbonTravel * 0.6f);
            }
        }

        /// <summary>이 회복(ShopHealed / EchoHealed)이 방금 온 지킴이 맥박의 결과인지 (시전 맥락의 회복은 아니다)</summary>
        bool IsPulseResult(int sourceId) => !InCast && _pulseFrame == Time.frameCount && sourceId == _pulseSlot;

        /// <summary>힐 맥박 고리가 이 잔향이에게 닿기까지 (고리는 0.4초 동안 반경까지 퍼진다)</summary>
        float PulseRingDelay(EchoView echo)
        {
            if (_pulseSlot < 0 || _pulseSlot >= _guardians.Length || _pulseRadius <= 1f) return 0f;
            var delta = echo.Rect.anchoredPosition - _guardians[_pulseSlot].FeetPosition;
            // 바닥 고리는 세로로 눌려 있으므로 세로 거리를 펴서 잰다
            delta.y /= Mathf.Max(0.1f, _mapper.Squash);
            return BattleFx.HealRingTime * Mathf.Clamp01(delta.magnitude / _pulseRadius);
        }

        // ───────────── 등장 (ATK-15) ─────────────

        /// <summary>보스가 부른 부하 (minionId, 보스 가까이에서 생김): 보스 발밑에 날개가루 소용돌이 — 같은 프레임에 여럿이어도 한 번</summary>
        void OnEnemySpawned(BattleEvent e)
        {
            if (_sim == null || e.Value >= 0.5f || _minionFrame == Time.frameCount) return;
            var spawned = FindEnemy(e.SourceId);
            if (spawned == null || spawned.IsSplit || spawned.IsBoss || spawned.Def == null) return;
            var enemies = _sim.Enemies;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var boss = enemies[i];
                if (boss == null || !boss.Alive || !boss.IsBoss || boss.Def == null || boss.Def.minionId != spawned.Def.id) continue;
                if ((boss.Position - spawned.Position).sqrMagnitude > 0.36f) continue;
                _minionFrame = Time.frameCount;
                var feet = _enemies.TryGet(boss.Id, out var view) ? view.Rect.anchoredPosition : _mapper.WorldToField(boss.Position);
                _fx.MinionSummon(feet, EnemyFxCatalog.Family(boss.Def.id));
                return;
            }
        }

        // ───────────── 정전 빛 구멍 (ATK-14) ─────────────

        /// <summary>정전 스테이지에서 빛 특성 계보의 기본 공격: 명중하는 순간 그 자리의 어둠에 구멍이 열린다</summary>
        void TryLightHole(string lineageId, string formId, EnemyView target, float delay)
        {
            if (_sim == null || target == null || _sim.StageModifier != StageModifierType.Blackout) return;
            if (!HasTrait(lineageId, formId, TraitType.Light)) return;
            var at = target.HitPoint;
            if (_ambience != null) _ambience.LightHole(at, EnemyFxCatalog.LightHoleRadius * _mapper.ActorScale, delay);
            _fx.LightPool(at, delay);
        }

        /// <summary>계보에 이 특성이 있는지 (빛 = 정전 빛 구멍, 천 = 좀나방 갉음)</summary>
        static bool HasTrait(string lineageId, string formId, TraitType trait)
        {
            var lineage = BattleUnitArt.Lineage(lineageId, formId);
            var traits = lineage != null ? lineage.traits : null;
            if (traits == null) return false;
            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i] == trait) return true;
            }
            return false;
        }

        /// <summary>천을 갉아먹는 적인지 (FabricEater: 좀나방·좀나방 여왕)</summary>
        static bool IsFabricEater(EnemyView enemy, string enemyId)
        {
            var def = enemy != null && enemy.State != null ? enemy.State.Def : null;
            if (def != null) return (def.gimmicks & EnemyGimmick.FabricEater) != 0;
            return enemyId == "enemy_moth" || enemyId == "boss_mothqueen";
        }

        // ───────────── 처치 ─────────────

        /// <summary>
        /// 일반 적 처치 보상 반짝: 5마리째마다 단추가 상단 바로 — 처치 꽃이 터진 직후에 (view.KillDelay = 마지막 일격이 닿고 몸이 하얗게 버틴 뒤,
        /// MarkKilled가 정한다)
        /// </summary>
        void RewardKill(EnemyView view, float hold)
        {
            if (view == null) return;
            _killCount++;
            if (_killCount % EnemyFxCatalog.RewardEvery == 0) _fx.KillCoin(view.HitPoint, Mathf.Max(hold, view.KillDelay) + 0.08f);
        }

        /// <summary>보스 처치: 작은 리본 배너 (처치 연출 자체는 EnemyViewPool → BattleFx.EnemyDeath)</summary>
        void ShowBossDefeated(EnemyView view)
        {
            string name = view != null && view.State != null && view.State.Def != null ? view.State.Def.name : null;
            _banner.Show("보스 처치!", string.IsNullOrEmpty(name) ? "수선점이 다시 환해졌어요" : name, UITheme.Gold, BattleBannerView.Style.Ribbon, 0.9f);
        }

        // ───────────── 불 밝히기 배너 ─────────────

        /// <summary>
        /// 처음 불 밝히기(이 세이브에서 처음 — PlayerData.flags, §13.5 d)만 큰 배너, 그 뒤는 작은 리본만 (ATK-05).
        /// force = 튜토리얼 첫 불 밝히기 (AppSignals.FtueFirstLampBeat — 늘 큰 배너)
        /// </summary>
        void ShowLampBanner(bool force = false)
        {
            var flags = Game.Data != null ? Game.Data.flags : null;
            bool seen = flags != null && flags.Contains(LampIntroKey);
            if (force || !seen)
            {
                if (flags != null && !seen) flags.Add(LampIntroKey);
                _banner.Show("수선점에 불이 밝혀졌어요!", "빛이 닿은 잊힘들이 모두 흩어져요", UITheme.Lamp, BattleBannerView.Style.Celebrate, 0.9f);
                return;
            }
            _banner.Show("불 밝히기!", null, UITheme.Lamp, BattleBannerView.Style.Ribbon, 0.45f);
        }
    }
}
