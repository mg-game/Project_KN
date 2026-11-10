using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면 총괄 (수선점 방어). 시뮬레이션은 GameBootstrap이 매 프레임 Game.Battle.Tick으로 돌리고,
    /// 이 뷰는 그 상태를 그리고 연출 이벤트를 각 하위 뷰에 나눠 준다.
    ///   전장: 수선점 / 방위 링 + 위험 표시 / 지킴이 5칸 / 적 / 잔향이 / 연출
    ///   UI: HUD(스테이지, 내구도, 잔향, AUTO, 배속) / 잔향이 덱 / 불 밝히기 / 배너
    /// 액터(지킴이·적·잔향이·수선점 건물)는 발 y 기준으로 앞뒤를 정렬한다.
    /// 기본 공격·피격 숫자·처치 보류는 BattleView.Attack.cs (계보별 투사체·명중, 잔향이 베기, 타격 등급, 숫자 문법),
    /// 적의 공격(잔향이·수선점)·보스 포위 예비 동작·지원/힐 맥박·부하 소환·정전 빛 구멍·자동 품질은 BattleView.Enemy.cs
    /// (적 종류별 그림, 아트가 없으면 흰색 모양에 적 색).
    /// 스킬·시너지·기믹 이벤트 연출은 BattleView.Skills.cs, 결과 연출 시간 맞추기(시전 맥락)는 BattleView.Cast.cs (같은 클래스).
    /// </summary>
    [DefaultExecutionOrder(-50)] // perf PF-2 F3: GameBootstrap(-1000, 시뮬레이션) → UIManager(-900) → 여기(액터 자리) → 기본 0(말풍선 · HUD) → BattleFx(+50, 따라가는 입자 · 숫자)
    public partial class BattleView : MonoBehaviour
    {
        const string Slogan = "지킴이로 방향을 지키고, 잔향이로 위기를 막아 수선점을 밝혀 주세요!";

        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] RectTransform _actorLayer;
        [SerializeField] ShopView _shop;
        [SerializeField] DirectionRingView _ring;
        [SerializeField] GuardianSlotView[] _guardians;
        [SerializeField] EnemyViewPool _enemies;
        [SerializeField] EchoViewPool _echoes;
        [SerializeField] EchoDeckView _deck;
        [SerializeField] LampButtonView _lamp;
        [SerializeField] BattleHud _hud;
        [SerializeField] BattleFx _fx;
        [SerializeField] BattleBannerView _banner;

        [Header("스킬 / 시너지 / 기믹")]
        [SerializeField] SkillVfxLibrary _library;
        [SerializeField] SkillCalloutView _callouts;
        [SerializeField] BattleNoticeView _notice;
        [SerializeField] SynergyBannerView _synergyBanner;
        [SerializeField] SynergyHudView _synergyHud;
        [SerializeField] StageAmbienceView _ambience;

        readonly float[] _danger = new float[BattleGeometry.SlotCount];
        RectTransform[] _sortBuffer = new RectTransform[64];
        float[] _sortKeys = new float[64];
        BattleSimulator _sim;
        bool _started;
        /// <summary>잠자기 모드 (AppSignals.SleepChanged)</summary>
        bool _asleep;

        void Start()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            // Sim을 처음 읽으면 전투가 시작되며 StageChanged가 바로 올 수 있으므로 구독 전에 먼저 깨운다
            _ = Game.Battle.Sim;
            Game.Battle.Event += OnBattleEvent;
            Game.Battle.StageChanged += OnStageChanged;
            Game.Battle.StageEnded += OnStageEnded;
            GameSettings.Changed += OnFxSettingsChanged;
            // 잠자기 모드 (IDLE-06): 늦게 붙어도 지금 상태부터
            AppSignals.SleepChanged += OnSleepChanged;
            PrepareCalloutGate();
            _started = true;
            Rebind(true);
            OnSleepChanged(AppSignals.IsAsleep);
            StartBattle4();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnFxSettingsChanged;
            AppSignals.SleepChanged -= OnSleepChanged;
            DestroyBattle4();
            if (!_started || !Game.IsReady || Game.Battle == null) return;
            Game.Battle.Event -= OnBattleEvent;
            Game.Battle.StageChanged -= OnStageChanged;
            Game.Battle.StageEnded -= OnStageEnded;
        }

        /// <summary>꺼질 때 연출 배율을 1로 (FX-01 — 다른 화면이 전투를 덮어도 시뮬레이션이 느린 채로 남지 않게)</summary>
        void OnDisable()
        {
            if (_started && Game.IsReady && Game.Battle != null) Game.Battle.PresentationScale = 1f;
        }

        /// <summary>잠자기 모드: 입자 예산 60 · 숫자 끔 (BattleFx.SetSleepMode). 깨면 소리 없이 되돌린다</summary>
        void OnSleepChanged(bool asleep)
        {
            _asleep = asleep;
            if (_fx != null) _fx.SetSleepMode(asleep);
        }

        void Update()
        {
            if (!_started || !Game.IsReady || Game.Battle == null) return;
            var sim = Game.Battle.Sim;
            if (sim != _sim) Rebind(_sim == null);

            float dt = Time.deltaTime;
            // 이번 프레임 GameBootstrap이 시뮬레이션을 돌린 배율 (지난 프레임에 넣은 값, 0.05~1)
            float simScale = Game.Battle.PresentationScale;
            // 연출 시계(멈칫 · 느린 화면)는 액터를 놓기 전에 한 번 옮긴다 — 액터 · 입자 · 숫자가 같은 프레임 배율 (perf PF-2 F3)
            float fxScale = _fx.BeginFrame(dt);
            // 연출 시계를 시뮬레이션에도 (FX-01 — 다음 Tick부터, GameBootstrap이 먼저 돈다)
            Game.Battle.PresentationScale = fxScale;
            // 뷰는 시뮬레이션이 덜 느려진 몫만 붙잡는다 (전체 정지 0 vs 시뮬레이션 바닥 0.05 · 배율이 바뀐 첫 프레임).
            // 1 − fxScale을 그대로 붙잡으면 느린 화면이 두 번 걸리고(0.3 × 0.3), 끝나면 따라잡으며 미끄러진다 (perf PF-2 F2)
            float holdScale = EnemyFxCatalog.HoldScale(fxScale, simScale);
            // 고정 진행 사이 보간 비율 (perf PF-2 F1 — 진행이 0번인 프레임도 2번인 프레임도 고르게)
            float alpha = _sim != null ? _sim.RenderAlpha : 1f;
            TickAttackPace();
            // 액터는 연출 시계로 (보스 처치·불 밝히기 전체 정지·느린 화면 동안 입자·숫자·피격 반응과 함께 느려진다)
            float actorDt = dt * fxScale;
            var guardians = _sim?.Guardians;
            float battleTime = _sim != null ? _sim.ElapsedTime : 0f;
            for (int i = 0; i < _guardians.Length; i++)
            {
                var state = guardians != null && i < guardians.Count ? guardians[i] : null;
                if (state != _guardians[i].State) _guardians[i].Bind(state);
                _danger[i] = _sim != null && i < BattleGeometry.SlotCount ? _sim.GetDanger(i) : 0f;
                _guardians[i].Tick(actorDt, _danger[i]);
                _guardians[i].TickPassive(battleTime, actorDt, _fx);
            }

            _shop.Tick(_sim, dt);
            _ring.Tick(_sim, _danger, dt);
            // 연출 시험장이 열려 있으면 실제 전투의 적·잔향이는 그리지 않는다 (허수아비만)
            bool gallery = SyncGallery();
            _enemies.Sync(gallery ? null : _sim?.Enemies, _mapper, actorDt, alpha, holdScale);
            _echoes.Sync(gallery ? null : _sim?.Echoes, _mapper, actorDt, alpha, holdScale);
            _deck.Tick(_sim, dt);
            _lamp.Tick(_sim, dt);
            _hud.Tick(_sim, dt);
            _hud.SetEmptyHint(!HasDefenders());
            TickSkillPresentation(dt);
            TickEnemyPresentation(dt);
            TickBattle4(dt);
            SortActors();
        }

        /// <summary>
        /// 새 전투(스테이지 시작/재시작)에 다시 연결. 시뮬레이터 인스턴스가 같아도 Start로 상태가 바뀌므로 매번 호출한다.
        /// 남은 적은 흐려지며 퇴장하고, 남은 잔향이는 빛으로 풀려 수선점으로 돌아간다.
        /// </summary>
        void Rebind(bool showIntro)
        {
            _sim = Game.Battle.Sim;
            _enemies.ExitAll();
            _echoes.DissolveAll();
            _deck.Bind(_sim);

            var guardians = _sim?.Guardians;
            for (int i = 0; i < _guardians.Length; i++)
            {
                _guardians[i].Bind(guardians != null && i < guardians.Count ? guardians[i] : null);
            }

            string label = Game.Battle.StageLabel;
            _hud.SetStage(label);
            if (showIntro && _sim != null) _banner.Show(StageTitle(label), Slogan, UITheme.Lavender, BattleBannerView.Style.Info, 1.1f);
            OnRebindSkills(showIntro);
            OnRebindEnemyFx();
        }

        // ───────────── 시스템 이벤트 ─────────────

        void OnStageChanged()
        {
            Rebind(true);
            OnStageChangedBattle4();
        }

        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            _banner.DropPendingInfo();
            if (outcome == BattleOutcome.Cleared)
            {
                // 전장 한가운데를 가리지 않게 위쪽 1/3의 작은 리본 0.9초 + 보상 단추가 상단 바로 날아간다 (UX-01)
                _banner.ShowWithRewards("스테이지 클리어!", "수선점이 한층 더 밝아졌어요", UITheme.Gold, BattleBannerView.Style.Ribbon, 0.9f, rewards);
                _fx.ScreenFlash(UITheme.Cream, 0.3f, 0.6f);
                _fx.RewardShower(new Vector2(0f, _fx.FieldTop * (2f / 3f)), 5);
                _shop.LampBurst();
            }
            else if (outcome == BattleOutcome.Failed)
            {
                _banner.ShowWithRewards("수선점이 위험해요… 다시 도전!", "지킴이를 강화하거나 편성을 바꿔 보세요", UITheme.DustyPink,
                    BattleBannerView.Style.Alert, 1.6f, rewards);
                _fx.ScreenFlash(UITheme.Danger, 0.3f, 0.6f);
                _fx.AddTrauma(HitFeel.FailTrauma);
            }
            OnStageEndedBattle4(outcome);
        }

        void OnBattleEvent(BattleEvent e)
        {
            if (!_started) return;
            if (Game.Battle.Sim != _sim) Rebind(false);
            TrackContext(e.Type);
            // 전투 기록(CR-03) · 패배 분석(CR-01) — 맥락(기본 공격 · 시전)을 맞춘 직후
            RecordBattle4(in e);
            // 개발용 연출 시험장이 열려 있으면 전투 연출은 쉰다 (시뮬레이션은 그대로 흐른다)
            if (GalleryOpen) return;

            switch (e.Type)
            {
                case BattleEventType.EnemySpawned:
                    OnEnemySpawned(e);
                    break;
                case BattleEventType.EnemyStrike:
                    OnEnemyStrike(e);
                    break;
                case BattleEventType.GuardianPulse:
                    OnGuardianPulse(e);
                    break;
                case BattleEventType.EnemyHit:
                    OnEnemyHit(e);
                    break;
                case BattleEventType.EnemyKilled:
                    // 투사체·스킬·빛 물결이 아직 닿기 전이면 닿을 때까지 버티다가 흩어진다
                    OnEnemyKilled(e);
                    break;
                case BattleEventType.BossSpawned:
                    OnBossSpawned();
                    break;
                case BattleEventType.ShopDamaged:
                    OnShopDamaged(e);
                    break;
                case BattleEventType.ShopHealed:
                    OnShopHealed(e);
                    break;
                case BattleEventType.GuardianAttack:
                    OnGuardianAttack(e);
                    break;
                case BattleEventType.EchoAttack:
                    OnEchoAttack(e);
                    break;
                case BattleEventType.EchoSummoned:
                    OnEchoSummoned(e);
                    break;
                case BattleEventType.EchoDissolved:
                    OnEchoDissolved(e);
                    break;
                case BattleEventType.LampLit:
                    OnLampLit();
                    break;
                default:
                    OnSkillEvent(e);
                    break;
            }
        }

        // ───────────── 전투 이벤트 연출 ─────────────

        /// <summary>
        /// 수선점 내구도가 깎임: 건물이 흔들리며 붉어지고 문 아래 "-N". 적 종류별 공격 그림·나뭇조각·링 번쩍임은 바로 앞의 EnemyStrike가 그렸다
        /// (EnemyStrike가 없던 공격이면 여기서 대신 그린다)
        /// </summary>
        void OnShopDamaged(BattleEvent e)
        {
            if (!StruckShopThisFrame(e.SourceId))
            {
                bool hasView = _enemies.TryGet(e.SourceId, out var attacker);
                var state = hasView ? attacker.State : FindEnemy(e.SourceId);
                string defId = state != null && state.Def != null ? state.Def.id : null;
                bool boss = hasView ? attacker.IsBoss : state != null && state.IsBoss;
                var from = hasView ? attacker.HitPoint : _mapper.WorldToField(e.Position);
                var world = state != null ? state.Position : e.Position;
                var edge = world.sqrMagnitude > 1e-6f ? world.normalized * BattleGeometry.ShopRadius : Vector2.zero;
                StrikeShop(e.SourceId, edge, e.Value, hasView ? attacker : null, defId, boss, from, hasView && attacker.IsHidden);
            }
            _shop.Hit();
            _fx.ShopDamaged(_shop.DoorPosition, e.Value);
            _hud.OnShopDamaged();
        }

        void OnEchoSummoned(BattleEvent e)
        {
            int deckSlot = Mathf.RoundToInt(e.Value);
            _deck.OnSummoned(deckSlot);
            var deck = _sim?.Deck;
            if (deck != null && deckSlot >= 0 && deckSlot < deck.Count && deck[deckSlot] != null) _hud.ShowResonanceDelta(-deck[deckSlot].Cost);
            _fx.Summon(_shop.DoorPosition, UITheme.EchoGlow);
            _shop.Absorb(0.4f);
        }

        void OnEchoDissolved(BattleEvent e)
        {
            int refund = Mathf.FloorToInt(e.Value + 0.001f);
            var position = e.Position != Vector2.zero ? _mapper.WorldToField(e.Position) : Vector2.zero;
            // 풀린 자리 기억 (뒤따르는 소멸 시 스킬의 시전 위치)
            if (position != Vector2.zero) RememberDissolve(e.SourceId, position);
            else if (_echoes.TryGet(e.SourceId, out var view)) RememberDissolve(e.SourceId, view.Rect.anchoredPosition);
            _echoes.Dissolve(e.SourceId, position, refund);
            if (refund > 0) _hud.ShowResonanceDelta(refund);
        }

        /// <summary>
        /// 불 밝히기 (ATK-05): 점화(연출 0.2배 0.12초) → 빛기둥 + 빛 물결 → 적마다 물결이 닿는 순간 명중·숫자 → 크림 별빛 여운.
        /// 번쩍임은 전장 안에서만. 배너는 처음 한 번만 크게, 그 뒤는 작은 리본
        /// </summary>
        void OnLampLit()
        {
            _fx.LampBurst(_shop.DoorPosition);
            // 튜토리얼 첫 불 밝히기 (FTUE-01): 0.2배 0.6초 + 늘 큰 배너 — PresentationScale로 시뮬레이션도 같이 느려진다
            bool firstBeat = AppSignals.FtueFirstLampBeat;
            if (firstBeat)
            {
                _fx.GlobalHitStop(0f, HitFeel.FirstLampIgniteScale, HitFeel.FirstLampIgniteTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Verify] first_lamp_beat scale={HitFeel.FirstLampIgniteScale:0.0} time={HitFeel.FirstLampIgniteTime:0.0}");
#endif
            }
            else _fx.GlobalHitStop(0f, LampWave.IgniteScale, LampWave.IgniteTime);
            _shop.LampBurst();
            _lamp.OnLampLit();
            // 뒤따르는 불빛 피해는 빛 물결이 그 적에 닿는 순간에 명중·숫자 (BattleView.Attack)
            BeginLampContext();
            ShowLampBanner(firstBeat);
        }

        void OnBossSpawned()
        {
            string bossName = null;
            var enemies = _sim?.Enemies;
            int count = enemies != null ? enemies.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsBoss || enemy.Def == null) continue;
                bossName = enemy.Def.name;
                break;
            }
            _banner.DropPendingInfo();
            _banner.Show("보스 등장!", string.IsNullOrEmpty(bossName) ? "수선점을 지켜 주세요!" : KoreanText.WithSubject(bossName) + " 다가와요!",
                UITheme.Danger, BattleBannerView.Style.Alert, 1.1f);
            _fx.AddTrauma(HitFeel.BossSpawnTrauma);
            _fx.ScreenFlash(UITheme.Danger, 0.22f, 0.5f);
            _hud.OnBossSpawned();
            OnBossSpawnedBattle4();
        }

        // ───────────── 앞뒤 정렬 ─────────────

        /// <summary>화면 위쪽(멀리) 액터가 먼저 그려지도록 발 y 내림차순 정렬 (삽입 정렬, 할당 없음). 수선점 건물도 문 앞 바닥 높이로 함께 정렬된다</summary>
        void SortActors()
        {
            int count = _actorLayer.childCount;
            if (_sortBuffer.Length < count)
            {
                _sortBuffer = new RectTransform[count * 2];
                _sortKeys = new float[count * 2];
            }
            for (int i = 0; i < count; i++)
            {
                var child = (RectTransform)_actorLayer.GetChild(i);
                float key = child.anchoredPosition.y;
                int j = i - 1;
                while (j >= 0 && _sortKeys[j] < key)
                {
                    _sortKeys[j + 1] = _sortKeys[j];
                    _sortBuffer[j + 1] = _sortBuffer[j];
                    j--;
                }
                _sortKeys[j + 1] = key;
                _sortBuffer[j + 1] = child;
            }
            for (int i = 0; i < count; i++)
            {
                if (_sortBuffer[i].GetSiblingIndex() != i) _sortBuffer[i].SetSiblingIndex(i);
                _sortBuffer[i] = null;
            }
        }

        /// <summary>지킴이나 잔향이가 하나라도 편성되어 있는지 (비어 있으면 적이 오지 않으므로 편성 안내)</summary>
        bool HasDefenders()
        {
            if (_sim == null) return true;
            var guardians = _sim.Guardians;
            for (int i = 0; guardians != null && i < guardians.Count; i++)
            {
                if (guardians[i] != null) return true;
            }
            var deck = _sim.Deck;
            for (int i = 0; deck != null && i < deck.Count; i++)
            {
                if (deck[i] != null && !string.IsNullOrEmpty(deck[i].FormId)) return true;
            }
            return false;
        }

        static string StageTitle(string label) => string.IsNullOrEmpty(label) ? "스테이지 시작" : "스테이지 " + label;

        /// <summary>적 대상 이벤트의 적 ID (TargetId 우선, 없으면 SourceId)</summary>
        static int EnemyId(BattleEvent e) => e.TargetId >= 0 ? e.TargetId : e.SourceId;
    }
}
