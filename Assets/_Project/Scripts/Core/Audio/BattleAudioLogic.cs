using System;
using System.Collections.Generic;
using IdleGame.UI;
using UnityEngine;

namespace IdleGame
{
    /// <summary>전투 감독이 읽는 적 하나 (값 복사)</summary>
    public struct BattleEnemyInfo
    {
        public int Id;
        public string DefId;
        public string MinionId;
        public bool IsBoss;
        public bool IsSplit;
        public bool Alive;
        public Vector2 Position;
        public float Shield;
        public float BurnTimer;
        public float SiegeWindup;
    }

    /// <summary>전투 감독이 읽는 지킴이 · 잔향이 하나</summary>
    public struct BattleUnitInfo
    {
        public string LineageId;
        public string FormId;
        public CombatStyle Style;
        public int FormTier;
        public bool IsToken;
        public bool Alive;
        public Vector2 Position;
    }

    /// <summary>
    /// 전투 상태 읽기 (BattleSystem/BattleSimulator/BattleFieldMapper 위 어댑터 — 테스트는 가짜).
    /// 시뮬레이터를 건드리는 값(Modifier 이하)은 SimReady일 때만 읽는다 (전투를 소리 쪽에서 시작하지 않는다 — §5.1)
    /// </summary>
    public interface IBattleAudioState
    {
        int Speed { get; }
        int Stage { get; }
        int HighestStage { get; }
        bool IsBetweenStages { get; }
        /// <summary>현재 스테이지 기믹 (시뮬레이터 없이도 읽힌다 — BattleSystem.CurrentStageModifier)</summary>
        StageModifierType CurrentModifier { get; }
        /// <summary>시뮬레이터를 읽어도 되는지 (이벤트·StageChanged를 받았거나 결과 대기 중)</summary>
        bool SimReady { get; }
        void MarkSimReady();
        float ShopHp { get; }
        float ShopMaxHp { get; }
        float ShopShield { get; }
        float LampGauge { get; }
        int Wave { get; }
        /// <summary>켜진 시너지 묶음의 지문 (키 + 단계) — 바뀌었을 때만 시너지 소리</summary>
        int SynergySignature { get; }
        bool TryGetEnemy(int id, out BattleEnemyInfo info);
        /// <summary>살아 있는 보스 (여럿이면 첫째)</summary>
        bool TryGetBoss(out BattleEnemyInfo boss);
        /// <summary>이 적이 살아 있는 보스가 부른 부하인지 (minionId가 같고 0.6 안)</summary>
        bool IsBossMinion(in BattleEnemyInfo spawned);
        bool TryGetGuardian(int slot, out BattleUnitInfo info);
        bool TryGetEcho(int id, out BattleUnitInfo info);
        SkillDef GetSkill(string skillId);
        float ActorScale { get; }
        float Ppu { get; }
        float Squash { get; }
        Vector2 WorldToField(Vector2 world);
        float HealEchoRadius { get; }
    }

    /// <summary>전투 감독의 출력 (AudioEngine 위 어댑터 — 테스트는 기록하는 가짜)</summary>
    public interface IBattleAudioSink
    {
        /// <summary>연출 시계 (초)</summary>
        double FxNow { get; }
        /// <summary>실제 시계 (초)</summary>
        double UiNow { get; }
        bool Has(AudioId id);
        void Schedule(PendingSound sound, double delay);
        void SetLoop(int slot, AudioId id, float volume, float pitch, float fade);
        void StopLoop(int slot, float fade);
        void Duck(AudioBus bus, float gainDb, float hold, float attack, float release, int reason, int exempt, double delay);
        void SetBossMusic(bool chapterBossAlive);
        void SetBossAlive(bool alive);
        void SetModifier(StageModifierType modifier, bool stageChanged);
        void FlushBattle();
        void SuppressEmitters(double seconds);
        bool IsAsleep { get; }
        bool IsJinglePlaying { get; }
        /// <summary>마지막 징글이 시작한 실제 시각과 키</summary>
        double JingleStartedAt { get; }
        AudioId CurrentJingle { get; }
        /// <summary>지금 곡의 옮김 (반음, 조율 키가 따라간다)</summary>
        int Transpose { get; }
    }

    /// <summary>
    /// 전투 이벤트 → 소리 (AUDIO-SPEC §5, 순수 C#). 시뮬레이터는 행동 하나의 결과를 같은 프레임에 차례로 보낸다
    /// (GuardianAttack → (CriticalHit) → (ShieldBroken) → EnemyHit → (EnemyKilled)) — BattleView.TrackContext와 같은 맥락 기계로 묶어,
    /// 명중 소리는 투사체가 닿는 순간 · 처치 소리는 처치 꽃이 터지는 순간 (BattleAudioTiming 거울)에 예약한다.
    /// 문자열 작업은 만들 때만 (계보 · 적 · 연출 키 → 번호 표). 이벤트 처리 중 할당 없음.
    /// </summary>
    public sealed class BattleAudioLogic
    {
        // ── 규칙 상수 (§1.5, §3.5, §5) ──
        public const float HurtGap = 0.25f;
        public const float HurtChance = 0.5f;
        public const float HurtChanceFast = 0.3f;
        public const float LaunchGap = 0.1f;
        public const float EchoHitGap = 0.12f;
        public const float StatusTargetGap = 0.9f;
        public const float StatusTypeGap = 0.2f;
        public const float BurnTickGap = 0.5f;
        public const float EchoHealGap = 0.55f;
        public const float RevealGap = 0.9f;
        public const float MendGap = 0.9f;
        public const float DrainGap = 0.6f;
        public const float BigSkillGap = 30f;
        public const float KillChainWindow = 0.8f;
        public const float KillChainReset = 1.2f;
        public const float MultiKillWindow = 1.0f;
        public const int MultiKillCount = 5;
        public const float NoChainAfterMulti = 0.5f;
        public const float DeathCrowdWindow = 0.08f;
        public const int DeathCrowdKeep = 2;
        public const float LaunchMuteAfterLamp = 0.8f;
        public const float HitsHalfAfterLamp = 1.0f;
        public const int LampNotes = 7;
        public const int SkillHitVoices = 3;
        public const float GoldChance = 0.35f;
        public const float ExtraChance = 0.5f;
        public const float BossMusicDelay = 1.0f;
        public const float BossJingleDelay = 0.2f;
        public const float BossDefeatedDelay = 0.3f;
        public const float BossDefeatedLength = 2.6f;
        public const float ChapterClearSkip = 1.5f;
        public const float StepBackDelay = 1.0f;
        public const float SynergyDelay = 0.3f;
        public const float ModifierDelay = 0.5f;
        public const float RefundDelay = 0.35f;
        public const float LampFullConfirm = 0.4f;
        public const float LampReminder = 12f;
        public const int LampReminders = 3;
        public const float LowHpEnter = EnemyFxCatalog.LowHpRatio;
        public const float LowHpExit = 0.35f;
        public const float WindupStart = 0.02f;

        const int Lineages = 8;
        const int GenericLineage = 7;

        struct LineageAudio
        {
            public AudioId Fire;
            public AudioId Hit;
            public AudioId Crit;
            public AudioId Extra;
            public float ExtraDelay;
            public bool Lullaby;
            public bool Umbrella;
        }

        struct EnemyAudio
        {
            public AudioId Strike;
            public AudioId Hurt;
            public AudioId Death;
        }

        enum Ctx : byte
        {
            None,
            Basic,
            EchoBasic,
            Cast,
            Lamp,
            Pulse,
        }

        /// <summary>작은 고리 게이트: (키, 시각) 몇 칸 — 개체별 간격</summary>
        sealed class EntityGate
        {
            readonly int[] _keys;
            readonly double[] _times;
            int _cursor;

            public EntityGate(int size)
            {
                _keys = new int[size];
                _times = new double[size];
                Clear();
            }

            public bool TryPass(int key, double now, double interval)
            {
                for (int i = 0; i < _keys.Length; i++)
                {
                    if (_keys[i] != key) continue;
                    if (now - _times[i] < interval - 1e-6) return false;
                    _times[i] = now;
                    return true;
                }
                _keys[_cursor] = key;
                _times[_cursor] = now;
                _cursor = (_cursor + 1) % _keys.Length;
                return true;
            }

            public void Clear()
            {
                for (int i = 0; i < _keys.Length; i++)
                {
                    _keys[i] = int.MinValue;
                    _times[i] = double.NegativeInfinity;
                }
                _cursor = 0;
            }
        }

        readonly IBattleAudioState _state;
        readonly IBattleAudioSink _sink;
        readonly Func<float> _random;

        // ── 번호 표 (만들 때 한 번) ──
        readonly string[] _lineageIds = AttackFxCatalog.KnownLineages;
        readonly LineageAudio[] _lineages = new LineageAudio[Lineages];
        readonly Dictionary<string, EnemyAudio> _enemyAudio = new(StringComparer.Ordinal);
        EnemyAudio _genericEnemy;
        EnemyAudio _genericBoss;
        readonly Dictionary<string, int> _vfxIndex = new(StringComparer.Ordinal);
        readonly AudioId[] _vfxCast = new AudioId[SkillVfx.All.Length];
        readonly AudioId[] _vfxHit = new AudioId[SkillVfx.All.Length];
        readonly AudioId[] _styleCast = new AudioId[12];
        readonly AudioId[] _statusByType = new AudioId[24];

        readonly AudioId _hitCrit, _layerSlash, _layerSnipe, _layerThread, _layerHeavy, _layerGold, _echoSlash, _echoTokenPop;
        readonly AudioId _hitSkill, _hitDot, _hitLamp, _shieldBlock, _rainBig, _musicboxRing, _musicboxCrit, _musicboxFinale;
        readonly AudioId _enemySpawn, _enemySplit, _enemyReveal, _enemyEnrage, _enemyGnaw, _enemyRust, _enemyMend;
        readonly AudioId _bossAppear, _bossWindup, _bossSiegeHit, _bossEnrage, _bossSummon, _bossLampDrain, _bossDeath, _bossKillImpact;
        readonly AudioId _shopHit, _shopHitBoss, _shopShieldHit, _shopHeal, _shopLowEnter, _shopHeartbeat;
        readonly AudioId _shieldButton, _shieldUmbrella, _shieldBubble, _shieldBreak, _pulseSupport, _pulseHealKey, _healTick;
        readonly AudioId _echoSummon, _echoDissolve, _echoHurt, _tokenSummon, _echoRefund, _killMote;
        readonly AudioId _lampTick, _lampFull, _lampLit, _lampWave;
        readonly AudioId _skillCharge, _skillCast, _skillBig, _skillGift, _skillFizzle, _statusDebuff, _statusChain;
        readonly AudioId _stageStart, _wave, _synergy, _stepBack, _killChain, _killMulti, _coinPop, _coinLand;
        readonly AudioId[] _modSounds = new AudioId[7];
        readonly AudioId _jStageClear, _jStageFail, _jBossAppear, _jBossDefeated, _jChapterClear;

        // ── 맥락 ──
        Ctx _ctx;
        int _ctxFrame = -1;
        int _frame;

        int _basicSlot = -1;
        int _basicTarget = -1;
        float _basicFlight;
        int _basicLineage = GenericLineage;
        CombatStyle _basicStyle;
        int _basicFormTier;
        string _basicFormId;
        float _basicPan;

        int _echoSource = -1;
        int _echoTarget = -1;
        float _echoDelay;
        int _echoLineage = GenericLineage;
        bool _echoToken;
        string _echoFormId;

        int _castSource = -1;
        string _castSkill;
        string _castVfx;
        SkillVfxStyle _castStyle;
        float _castFlight;
        bool _castSegment;
        bool _castProjectile;
        bool _castStagger;
        bool _castSplash;
        Vector2 _castFrom;
        Vector2 _castEnd;
        Vector2 _castCenter;
        float _castRadiusPx;
        int _castPrimary = -1;
        bool _castAllyGroup;
        int _castRecipients;
        int _castHits;
        bool _castStatusDone;
        float _castPan;
        int _chainHops;
        float _chainTime;
        readonly int[] _delayIds = new int[32];
        readonly float[] _delayTimes = new float[32];
        int _delayCount;
        readonly string[] _castStampSkill = new string[16];
        readonly Vector2[] _castStampCenter = new Vector2[16];
        int _castStampCount;
        int _castStampFrame = -1;
        int _shopShieldFrame = -1;

        readonly int[] _lampIds = new int[48];
        readonly float[] _lampDelays = new float[48];
        readonly float[] _lampPans = new float[48];
        int _lampCount;

        int _pulseSlot = -1;
        bool _pulseHeal;

        int _critFrame = -1;
        int _critTarget = -1;
        int _shopStrikeFrame = -1;
        int _shopStrikeSource = -1;
        int _minionFrame = -1;
        int _synergyFrame = -1;

        // ── 처치 시간 · 사다리 ──
        readonly int[] _incomingIds = new int[32];
        readonly double[] _incomingDue = new double[32];
        int _incomingCursor;
        readonly double[] _deathPops = new double[8];
        int _deathCursor;
        readonly double[] _killPops = new double[16];
        int _killCursor;
        int _streak;
        double _lastKillPop = double.NegativeInfinity;
        double _noChainUntil = double.NegativeInfinity;
        int _rewardKills;

        // 라라벨 자장가 (소스 16개)
        readonly int[] _lullabySources = new int[16];
        readonly int[] _lullabyIndex = new int[16];
        int _lullabyCursor;

        // ── 개체별 게이트 · 예산 ──
        readonly EntityGate _hurtGate = new(32);
        readonly EntityGate _echoHitGate = new(16);
        readonly EntityGate _statusGate = new(48);
        readonly EntityGate _typeGate = new(24);
        readonly EntityGate _strikeGate = new(32);
        readonly EntityGate _miscGate = new(32);
        readonly double[] _launchLast = new double[BattleGeometry.SlotCount];
        readonly bool[] _launchSkip = new bool[BattleGeometry.SlotCount];
        TokenBucket _dotBucket = new(4f, 2f);
        TokenBucket _strayBucket = new(4f, 2f);
        TokenBucket _echoHealBucket = new(3f, 2f);
        readonly string[] _bigSkills = new string[12];
        readonly double[] _bigTimes = new double[12];
        int _bigCursor;

        // ── 불 밝히기 · 보스 · 수선점 ──
        double _launchMuteUntil = double.NegativeInfinity;
        double _hitsHalfUntil = double.NegativeInfinity;
        double _lastDrain = double.NegativeInfinity;
        double _bossKillUntil = double.NegativeInfinity;
        double _bossMusicAt = double.NegativeInfinity;
        double _bossMusicHoldUntil = double.NegativeInfinity;
        bool _bossMusicOn;
        double _lastBossDefeated = double.NegativeInfinity;
        double _bossJingleAt = double.NegativeInfinity;
        bool _bossJingleChapter;
        bool _windupOn;
        bool _lowHp;
        float _lastLamp;
        int _lampSteps;
        bool _lampWasFull;
        double _lampFullAt = double.NegativeInfinity;
        double _lampLitAt = double.NegativeInfinity;
        double _lampReminderAt = double.NegativeInfinity;
        int _lampRemindersLeft;
        int _lastWave;
        int _stageHighest;
        int _lastSynergySignature;
        bool _firstStage = true;
        bool _bossAlive;

        // 연출 시간 거울 (BattleFx가 없을 때 — 보스 처치 순간 · 불 밝히기 점화)
        FxSlowMotion _mirror;
        double _mirrorStopAt = double.NegativeInfinity;

        public BattleAudioLogic(IBattleAudioState state, IBattleAudioSink sink, Func<float> random01)
        {
            _state = state;
            _sink = sink;
            _random = random01 ?? (() => 0.5f);
            _mirror.Clear();

            for (int i = 0; i < Lineages; i++)
            {
                string lineage = i < _lineageIds.Length ? _lineageIds[i] : null;
                var look = AttackFxCatalog.Look(lineage);
                var hit = look.Sfx;
                var audio = new LineageAudio
                {
                    Hit = AudioCatalog.Find(hit),
                    Fire = AudioCatalog.Find(AudioCatalog.FireKeyOf(hit)),
                    Crit = AudioCatalog.Find(AudioCatalog.CritKeyOf(hit)),
                    Lullaby = look.Extra == AttackExtra.SoundRing,
                    Umbrella = look.Extra == AttackExtra.Ripple,
                };
                switch (look.Extra)
                {
                    case AttackExtra.Buttons:
                        audio.Extra = AudioCatalog.Find("atk_button_bounce");
                        audio.ExtraDelay = 0.05f;
                        break;
                    case AttackExtra.Wax:
                        audio.Extra = AudioCatalog.Find("atk_candle_drip");
                        audio.ExtraDelay = 0.12f;
                        break;
                }
                _lineages[i] = audio;
            }

            for (int i = 0; i < EnemyFxCatalog.KnownEnemies.Length; i++)
            {
                string id = EnemyFxCatalog.KnownEnemies[i];
                bool boss = id.StartsWith("boss_", StringComparison.Ordinal);
                _enemyAudio[id] = MakeEnemy(id, boss);
            }
            _genericEnemy = MakeEnemy(null, false);
            _genericBoss = MakeEnemy(null, true);

            for (int i = 0; i < SkillVfx.All.Length; i++)
            {
                string vfx = SkillVfx.All[i];
                _vfxIndex[vfx] = i;
                string name = vfx.StartsWith("vfx_", StringComparison.Ordinal) ? vfx.Substring(4) : vfx;
                _vfxCast[i] = AudioCatalog.Find("skill_" + name);
                _vfxHit[i] = AudioCatalog.Find("skill_" + name + "_hit");
            }
            string[] styles = { "projectile", "beam", "burst", "ring", "ground", "dome", "rise", "overhead", "orbit", "rain" };
            for (int i = 0; i < styles.Length && i < _styleCast.Length; i++) _styleCast[i] = AudioCatalog.Find("skill_style_" + styles[i]);

            SetStatus(SkillEffectType.Stun, "status_stun");
            SetStatus(SkillEffectType.Slow, "status_slow");
            SetStatus(SkillEffectType.Mark, "status_mark");
            SetStatus(SkillEffectType.DamageOverTime, "status_burn");
            SetStatus(SkillEffectType.Knockback, "status_knockback");
            SetStatus(SkillEffectType.Pull, "status_pull");
            SetStatus(SkillEffectType.BuffAttack, "status_buff");
            SetStatus(SkillEffectType.BuffAttackSpeed, "status_buff");
            SetStatus(SkillEffectType.Reveal, "status_reveal");
            SetStatus(SkillEffectType.Cleanse, "status_cleanse");
            SetStatus(SkillEffectType.ResonanceGain, "status_resonance");
            SetStatus(SkillEffectType.LampGain, "status_lamp_gain");
            SetStatus(SkillEffectType.Heal, "heal_tick");
            SetStatus(SkillEffectType.HealOverTime, "heal_tick");

            _hitCrit = AudioCatalog.Find("hit_crit");
            _layerSlash = AudioCatalog.Find("atk_layer_slash");
            _layerSnipe = AudioCatalog.Find("atk_layer_snipe");
            _layerThread = AudioCatalog.Find("atk_layer_thread");
            _layerHeavy = AudioCatalog.Find("atk_layer_heavy");
            _layerGold = AudioCatalog.Find("atk_layer_gold");
            _echoSlash = AudioCatalog.Find("echo_slash");
            _echoTokenPop = AudioCatalog.Find("echo_token_pop");
            _hitSkill = AudioCatalog.Find("hit_skill");
            _hitDot = AudioCatalog.Find("hit_dot");
            _hitLamp = AudioCatalog.Find("hit_lamp");
            _shieldBlock = AudioCatalog.Find("shield_block");
            _rainBig = AudioCatalog.Find("atk_rain_big");
            _musicboxRing = AudioCatalog.Find("atk_musicbox_ring");
            _musicboxCrit = AudioCatalog.Find("atk_musicbox_crit");
            _musicboxFinale = AudioCatalog.Find("atk_musicbox_finale");
            _enemySpawn = AudioCatalog.Find("enemy_spawn");
            _enemySplit = AudioCatalog.Find("enemy_split");
            _enemyReveal = AudioCatalog.Find("enemy_reveal");
            _enemyEnrage = AudioCatalog.Find("enemy_enrage");
            _enemyGnaw = AudioCatalog.Find("enemy_gnaw");
            _enemyRust = AudioCatalog.Find("enemy_rust");
            _enemyMend = AudioCatalog.Find("enemy_mend");
            _bossAppear = AudioCatalog.Find("boss_appear");
            _bossWindup = AudioCatalog.Find("boss_windup");
            _bossSiegeHit = AudioCatalog.Find("boss_siege_hit");
            _bossEnrage = AudioCatalog.Find("boss_enrage");
            _bossSummon = AudioCatalog.Find("boss_summon");
            _bossLampDrain = AudioCatalog.Find("boss_lamp_drain");
            // SPEC4 §3.5 이름 boss_defeat (= boss_death의 클립 · 같은 버스 · 등급 · 크기 · 1목소리 / 0.5초 게이트): 소리는 그대로이고,
            // 화면 쪽이 같은 키를 몇 프레임 앞뒤로 한 번 더 불러도 겹쳐 울리지 않는다 (RM-2 AUD 리뷰)
            _bossDeath = AudioCatalog.Find("boss_defeat");
            _bossKillImpact = AudioCatalog.Find("boss_kill_impact");
            _shopHit = AudioCatalog.Find("shop_hit");
            _shopHitBoss = AudioCatalog.Find("shop_hit_boss");
            _shopShieldHit = AudioCatalog.Find("shop_shield_hit");
            _shopHeal = AudioCatalog.Find("shop_heal");
            _shopLowEnter = AudioCatalog.Find("shop_low_enter");
            _shopHeartbeat = AudioCatalog.Find("shop_heartbeat");
            _shieldButton = AudioCatalog.Find("shield_gain_button");
            _shieldUmbrella = AudioCatalog.Find("shield_gain_umbrella");
            _shieldBubble = AudioCatalog.Find("shield_gain_bubble");
            _shieldBreak = AudioCatalog.Find("shield_break");
            _pulseSupport = AudioCatalog.Find("pulse_support");
            _pulseHealKey = AudioCatalog.Find("pulse_heal");
            _healTick = AudioCatalog.Find("heal_tick");
            _echoSummon = AudioCatalog.Find("echo_summon");
            _echoDissolve = AudioCatalog.Find("echo_dissolve");
            _echoHurt = AudioCatalog.Find("echo_hurt");
            _tokenSummon = AudioCatalog.Find("token_summon");
            _echoRefund = AudioCatalog.Find("echo_refund");
            _killMote = AudioCatalog.Find("kill_mote");
            _lampTick = AudioCatalog.Find("lamp_tick");
            _lampFull = AudioCatalog.Find("lamp_full");
            _lampLit = AudioCatalog.Find("lamp_lit");
            _lampWave = AudioCatalog.Find("lamp_wave");
            _skillCharge = AudioCatalog.Find("skill_charge");
            _skillCast = AudioCatalog.Find("skill_cast");
            _skillBig = AudioCatalog.Find("skill_big");
            _skillGift = AudioCatalog.Find("skill_gift");
            _skillFizzle = AudioCatalog.Find("skill_fizzle");
            _statusDebuff = AudioCatalog.Find("status_debuff");
            _statusChain = AudioCatalog.Find("status_chain");
            _stageStart = AudioCatalog.Find("battle_stage_start");
            _wave = AudioCatalog.Find("battle_wave");
            _synergy = AudioCatalog.Find("battle_synergy");
            _stepBack = AudioCatalog.Find("battle_step_back");
            _killChain = AudioCatalog.Find("kill_chain");
            _killMulti = AudioCatalog.Find("kill_multi");
            _coinPop = AudioCatalog.Find("coin_pop");
            _coinLand = AudioCatalog.Find("ui_coin_land");
            _modSounds[(int)StageModifierType.Rain] = AudioCatalog.Find("mod_rain");
            _modSounds[(int)StageModifierType.Wind] = AudioCatalog.Find("mod_wind");
            _modSounds[(int)StageModifierType.Blackout] = AudioCatalog.Find("mod_blackout");
            _modSounds[(int)StageModifierType.FullMoon] = AudioCatalog.Find("mod_fullmoon");
            _modSounds[(int)StageModifierType.DustStorm] = AudioCatalog.Find("mod_dust");
            _modSounds[(int)StageModifierType.Festival] = AudioCatalog.Find("mod_festival");
            _jStageClear = AudioCatalog.Find("jingle_stage_clear");
            _jStageFail = AudioCatalog.Find("jingle_stage_fail");
            _jBossAppear = AudioCatalog.Find("jingle_boss_appear");
            _jBossDefeated = AudioCatalog.Find("jingle_boss_defeated");
            _jChapterClear = AudioCatalog.Find("jingle_chapter_clear");
            Reset();
        }

        static EnemyAudio MakeEnemy(string id, bool boss)
        {
            var family = EnemyFxCatalog.Family(id);
            if (id == null && boss) family = EnemyDeathFamily.Shadow;
            string familyKey = EnemyFxCatalog.FamilyKey(family);
            string death = "death_" + familyKey;
            if (id == "enemy_tangle") death = "death_tangle";
            else if (id == "enemy_windup") death = "death_windup";
            return new EnemyAudio
            {
                Strike = AudioCatalog.Find(EnemyFxCatalog.Look(id, boss).Sfx),
                Hurt = AudioCatalog.Find("hurt_" + familyKey),
                Death = AudioCatalog.Find(death),
            };
        }

        void SetStatus(SkillEffectType type, string key)
        {
            int index = (int)type;
            if (index >= 0 && index < _statusByType.Length) _statusByType[index] = AudioCatalog.Find(key);
        }

        // ───────────── 상태 ─────────────

        /// <summary>새 전투 인스턴스 / 처음부터 (모든 기억을 비운다)</summary>
        public void Reset()
        {
            _ctx = Ctx.None;
            _ctxFrame = -1;
            _lampCount = 0;
            _delayCount = 0;
            _critFrame = -1;
            for (int i = 0; i < _incomingIds.Length; i++)
            {
                _incomingIds[i] = int.MinValue;
                _incomingDue[i] = double.NegativeInfinity;
            }
            for (int i = 0; i < _deathPops.Length; i++) _deathPops[i] = double.NegativeInfinity;
            for (int i = 0; i < _killPops.Length; i++) _killPops[i] = double.NegativeInfinity;
            for (int i = 0; i < _lullabySources.Length; i++) _lullabySources[i] = int.MinValue;
            for (int i = 0; i < _launchLast.Length; i++) _launchLast[i] = double.NegativeInfinity;
            _hurtGate.Clear();
            _echoHitGate.Clear();
            _statusGate.Clear();
            _typeGate.Clear();
            _strikeGate.Clear();
            _miscGate.Clear();
            ResetStage();
        }

        void ResetStage()
        {
            _streak = 0;
            _lastKillPop = double.NegativeInfinity;
            _noChainUntil = double.NegativeInfinity;
            _rewardKills = 0;
            _lampSteps = 0;
            _lastLamp = 0f;
            _lampWasFull = false;
            _lampFullAt = double.NegativeInfinity;
            _lampReminderAt = double.NegativeInfinity;
            _lampRemindersLeft = 0;
            _lastWave = 0;
            _lowHp = false;
            _windupOn = false;
            _sink.StopLoop(0, 0.5f);
            _sink.StopLoop(1, 0.04f);
        }

        /// <summary>BattleFx가 없을 때 쓰는 연출 시간 배율 (보스 처치 · 불 밝히기 정지 거울)</summary>
        public float TickMirror(float realDt)
        {
            if (_sink.FxNow >= _mirrorStopAt && _mirrorStopAt > double.NegativeInfinity)
            {
                _mirrorStopAt = double.NegativeInfinity;
                _mirror.Trigger(HitFeel.GlobalStop, HitFeel.SlowScale, HitFeel.SlowTime);
            }
            return _mirror.Tick(realDt);
        }

        public bool BossAlive => _bossAlive;
        public int Streak => _streak;

        // ───────────── 소리 하나 예약 ─────────────

        bool Sfx(AudioId id, double delay, float gainDb = 0f, float pitch = 1f, float pan = 0f, AudioCategory category = AudioCategory.None,
            AudioPlayFlags flags = AudioPlayFlags.None, float importance = 0f, int enemyId = -1, float targetHz = 0f, int classOverride = -1)
        {
            if (!id.IsValid || !_sink.Has(id)) return false;
            var def = AudioCatalog.Keys[id.Index];
            var voiceClass = classOverride >= 0 ? (AudioVoiceClass)classOverride : def.Class;
            var sound = new PendingSound
            {
                Key = id.Index,
                Volume = 1f,
                GainDb = gainDb,
                Pitch = pitch,
                Pan = pan,
                Category = category,
                Flags = flags | AudioPlayFlags.Battle | (delay > 0 ? AudioPlayFlags.Predicted : AudioPlayFlags.None),
                Class = voiceClass,
                Importance = importance,
                EnemyId = enemyId,
                TargetHz = targetHz,
            };
            _sink.Schedule(sound, Math.Max(0, delay));
            return true;
        }

        double Now => _sink.FxNow;

        float Random() => _random();

        int Speed => Math.Max(1, _state.Speed);

        static float St(float semitones) => AudioCatalog.SemitoneRatio(semitones);

        int LineageIndex(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId)) return GenericLineage;
            for (int i = 0; i < _lineageIds.Length && i < GenericLineage; i++)
            {
                if (string.Equals(_lineageIds[i], lineageId, StringComparison.Ordinal)) return i;
            }
            return GenericLineage;
        }

        EnemyAudio EnemySounds(string defId, bool boss)
        {
            if (defId != null && _enemyAudio.TryGetValue(defId, out var audio)) return audio;
            return boss ? _genericBoss : _genericEnemy;
        }

        float EnemyPan(in BattleEnemyInfo enemy) => BattleAudioTiming.Pan(enemy.Position.x, enemy.IsBoss);

        static float GuardianPan(int slot) => BattleAudioTiming.Pan(BattleGeometry.GuardianPosition(slot).x);

        float HitImportance(in BattleEnemyInfo enemy, bool crit) =>
            (crit ? 4f : 0f) + (enemy.IsBoss ? 2f : 0f) + (1f - Mathf.Clamp01(enemy.Position.magnitude / BattleGeometry.SpawnRadius));

        void NoteIncoming(int enemyId, double due)
        {
            for (int i = 0; i < _incomingIds.Length; i++)
            {
                if (_incomingIds[i] != enemyId) continue;
                if (due > _incomingDue[i]) _incomingDue[i] = due;
                return;
            }
            _incomingIds[_incomingCursor] = enemyId;
            _incomingDue[_incomingCursor] = due;
            _incomingCursor = (_incomingCursor + 1) % _incomingIds.Length;
        }

        float IncomingRemaining(int enemyId)
        {
            for (int i = 0; i < _incomingIds.Length; i++)
            {
                if (_incomingIds[i] == enemyId) return (float)Math.Max(0, _incomingDue[i] - Now);
            }
            return 0f;
        }

        // ───────────── 맥락 ─────────────

        bool Fresh => _ctxFrame == _frame;
        bool InCast => _ctx == Ctx.Cast && Fresh;

        void Track(BattleEventType type)
        {
            if (_ctx == Ctx.None) return;
            if (!Fresh)
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
                case BattleEventType.EnemySpawned:
                case BattleEventType.EnemyRevealed:
                case BattleEventType.EnemyEnraged:
                case BattleEventType.BossSpawned:
                    return;
            }
            EndContext();
        }

        void Begin(Ctx ctx)
        {
            EndContext();
            _ctx = ctx;
            _ctxFrame = _frame;
        }

        /// <summary>맥락을 닫는다: 불빛 물결 음은 도착 순서로 내고, 아무도 못 받은 아군 전체 스킬은 헛손질</summary>
        void EndContext()
        {
            if (_ctx == Ctx.Lamp) FlushLampNotes();
            if (_ctx == Ctx.Cast && _castAllyGroup && _castRecipients == 0) Sfx(_skillFizzle, 0, 0f, 1f, _castPan, AudioCategory.Status);
            _ctx = Ctx.None;
        }

        /// <summary>프레임 끝 (감독 Update): 지난 프레임의 맥락을 닫는다</summary>
        public void EndFrame(int frame)
        {
            _frame = frame;
            if (_ctx != Ctx.None && _ctxFrame != frame) EndContext();
            else if (_ctx == Ctx.Lamp) EndContext();
        }

        // ───────────── 이벤트 ─────────────

        public void OnEvent(in BattleEvent e, int frame)
        {
            _frame = frame;
            if (!_state.SimReady) _state.MarkSimReady();
            Track(e.Type);
            switch (e.Type)
            {
                case BattleEventType.GuardianAttack: OnGuardianAttack(in e); break;
                case BattleEventType.EchoAttack: OnEchoAttack(in e); break;
                case BattleEventType.CriticalHit:
                    _critFrame = _frame;
                    _critTarget = e.TargetId;
                    break;
                case BattleEventType.EnemyHit: OnEnemyHit(in e); break;
                case BattleEventType.ShieldBroken: OnShieldBroken(in e); break;
                case BattleEventType.EnemyKilled: OnEnemyKilled(in e); break;
                case BattleEventType.EnemySpawned: OnEnemySpawned(in e); break;
                case BattleEventType.BossSpawned: OnBossSpawned(in e); break;
                case BattleEventType.EnemyStrike: OnEnemyStrike(in e); break;
                case BattleEventType.ShopDamaged:
                    if (!(_shopStrikeFrame == _frame && _shopStrikeSource == e.SourceId)) StrikeShop(e.SourceId, e.Position, e.Value, null);
                    break;
                case BattleEventType.ShopHealed: OnShopHealed(in e); break;
                case BattleEventType.EchoHealed: OnEchoHealed(in e); break;
                case BattleEventType.GuardianPulse: OnGuardianPulse(in e); break;
                case BattleEventType.ShieldGained: OnShieldGained(in e); break;
                case BattleEventType.StatusApplied: OnStatusApplied(in e); break;
                case BattleEventType.SkillCast: OnSkillCast(in e); break;
                case BattleEventType.TokenSummoned: Sfx(_tokenSummon, 0, 0f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Spawn); break;
                case BattleEventType.EchoSummoned: Sfx(_echoSummon, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget); break;
                case BattleEventType.EchoDissolved:
                    Sfx(_echoDissolve, 0, 0f, 1f, BattleAudioTiming.Pan(e.Position.x) * 0.5f);
                    if (e.Value >= 0.999f) Sfx(_echoRefund, RefundDelay, 0f, 1f, 0f);
                    break;
                case BattleEventType.LampLit: OnLampLit(); break;
                case BattleEventType.LampDrained:
                    if (Now - _lastDrain >= DrainGap)
                    {
                        _lastDrain = Now;
                        Sfx(_bossLampDrain, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptBoss);
                    }
                    break;
                case BattleEventType.EnemySplit:
                    Sfx(_enemySplit, 0, 0f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Spawn);
                    break;
                case BattleEventType.EnemyRevealed:
                    if (_miscGate.TryPass(1000000 + e.TargetId, Now, RevealGap))
                        Sfx(_enemyReveal, ContextDelay(e.SourceId, e.TargetId), 0f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Status);
                    break;
                case BattleEventType.EnemyEnraged: OnEnemyEnraged(in e); break;
                case BattleEventType.SynergyActivated: OnSynergy(); break;
                case BattleEventType.StageModifierStarted:
                {
                    var modifier = (StageModifierType)Mathf.Clamp(Mathf.RoundToInt(e.Value), 0, 6);
                    if ((int)modifier < _modSounds.Length) Sfx(_modSounds[(int)modifier], ModifierDelay, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                    _sink.SetModifier(modifier, false);
                    break;
                }
            }
        }

        // ── 지킴이 · 잔향이 공격 ──

        void OnGuardianAttack(in BattleEvent e)
        {
            int slot = e.SourceId;
            Begin(Ctx.Basic);
            _basicSlot = slot;
            _basicTarget = e.TargetId;
            _basicFlight = 0f;
            _basicLineage = GenericLineage;
            _basicStyle = CombatStyle.Defense;
            _basicFormTier = 0;
            _basicFormId = null;
            if (slot < 0 || slot >= BattleGeometry.SlotCount || !_state.TryGetGuardian(slot, out var guardian)) return;
            if (!_state.TryGetEnemy(e.TargetId, out var enemy)) return;
            float scale = _state.ActorScale;
            var from = _state.WorldToField(BattleGeometry.GuardianPosition(slot)) + Vector2.up * (BattleAudioTiming.MuzzleLift * scale);
            var to = _state.WorldToField(enemy.Position) + Vector2.up * BattleAudioTiming.EnemyHitLift(enemy.IsBoss, enemy.IsSplit, scale);
            float flight = BattleAudioTiming.GuardianFlight(guardian.LineageId, guardian.Style, (to - from).magnitude, Speed, scale);
            float windup = BattleAudioTiming.GuardianWindup(flight, Speed);
            _basicFlight = flight;
            _basicLineage = LineageIndex(guardian.LineageId);
            _basicStyle = guardian.Style;
            _basicFormTier = guardian.FormTier;
            _basicFormId = guardian.FormId;
            _basicPan = EnemyPan(in enemy);
            NoteIncoming(e.TargetId, Now + flight);

            // 쏘는 소리: 총구 불꽃과 같은 순간 (웅크림 W 뒤). 잠 · 불 밝히기 직후 · 2배속 하나 걸러 · 지킴이마다 0.1초
            if (_sink.IsAsleep || Now < _launchMuteUntil) return;
            if (Speed >= 2)
            {
                _launchSkip[slot] = !_launchSkip[slot];
                if (_launchSkip[slot]) return;
            }
            if (Now - _launchLast[slot] < LaunchGap) return;
            _launchLast[slot] = Now;
            float pan = GuardianPan(slot);
            Sfx(_lineages[_basicLineage].Fire, windup, 0f, 1f, pan, AudioCategory.Launch);
            if (guardian.Style == CombatStyle.Snipe) Sfx(_layerSnipe, windup, 0f, 1f, pan, AudioCategory.Launch);
        }

        void OnEchoAttack(in BattleEvent e)
        {
            Begin(Ctx.EchoBasic);
            _echoSource = e.SourceId;
            _echoTarget = e.TargetId;
            _echoDelay = 0f;
            _echoToken = false;
            _echoFormId = null;
            if (!_state.TryGetEcho(e.SourceId, out var echo)) echo = new BattleUnitInfo { Style = CombatStyle.Assault, Alive = true };
            _echoToken = echo.IsToken;
            _echoLineage = LineageIndex(echo.IsToken ? "jjakjjak" : echo.LineageId);
            _echoFormId = echo.FormId;
            bool ranged = !echo.IsToken && AttackFxCatalog.Style(echo.Style).Ranged;
            if (echo.IsToken) _echoDelay = 0f;
            else _echoDelay = ranged ? BattleAudioTiming.EchoRangedHit(Speed) : BattleAudioTiming.EchoMeleeHit(Speed);
            NoteIncoming(e.TargetId, Now + _echoDelay);
            if (_sink.IsAsleep || Now < _launchMuteUntil) return;
            float pan = BattleAudioTiming.Pan(echo.Position.x);
            if (echo.IsToken) return;
            if (ranged) Sfx(_lineages[_echoLineage].Fire, 0, -6f, St(3f), pan, AudioCategory.Launch);
            else Sfx(_echoSlash, 0, 0f, 1f, pan, AudioCategory.Launch);
        }

        // ── 명중 ──

        void OnEnemyHit(in BattleEvent e)
        {
            int id = e.TargetId >= 0 ? e.TargetId : e.SourceId;
            bool known = _state.TryGetEnemy(id, out var enemy);
            if (!known) enemy = new BattleEnemyInfo { Id = id, Position = e.Position };
            bool crit = _critFrame == _frame && _critTarget == id;
            // 치명타 표시는 바로 뒤 그 적의 명중 하나에만 (같은 프레임 다른 명중이 치명타 소리를 내지 않게)
            if (crit) _critFrame = -1;
            if (Fresh && _ctx == Ctx.Basic && e.SourceId == _basicSlot && id == _basicTarget)
            {
                BasicHit(in enemy, crit);
                return;
            }
            if (Fresh && _ctx == Ctx.EchoBasic && id == _echoTarget)
            {
                EchoHit(in enemy, crit);
                return;
            }
            if (InCast)
            {
                float delay = CastDelay(id, in enemy);
                NoteIncoming(id, Now + delay);
                float pan = EnemyPan(in enemy);
                if (_castHits < SkillHitVoices) Sfx(_hitSkill, delay, -3f, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit, HitImportance(in enemy, crit), id);
                _castHits++;
                if (crit) Sfx(_hitCrit, delay, 0f, 1f, pan, AudioCategory.None, AudioPlayFlags.ExemptCrit | AudioPlayFlags.MicroDuck, 0f, id);
                return;
            }
            if (Fresh && _ctx == Ctx.Lamp)
            {
                float delay = BattleAudioTiming.LampArrival(enemy.Position);
                NoteIncoming(id, Now + delay);
                if (_lampCount < _lampIds.Length)
                {
                    _lampIds[_lampCount] = id;
                    _lampDelays[_lampCount] = delay;
                    _lampPans[_lampCount] = EnemyPan(in enemy);
                    _lampCount++;
                }
                return;
            }
            // 맥락 없는 명중: 지속 피해 틱 / 그 밖 (반사 등)
            float strayPan = EnemyPan(in enemy);
            if (known && enemy.BurnTimer > 0f)
            {
                if (_miscGate.TryPass(2000000 + id, Now, BurnTickGap) && _dotBucket.TryTake(_sink.UiNow))
                    Sfx(_hitDot, 0, -3f, 1f, strayPan, AudioCategory.Hurt, AudioPlayFlags.None, 0f, id);
                return;
            }
            if (_strayBucket.TryTake(_sink.UiNow)) Sfx(_hitSkill, 0, -6f, 1f, strayPan, AudioCategory.Hit, AudioPlayFlags.Hit, HitImportance(in enemy, crit), id);
        }

        void BasicHit(in BattleEnemyInfo enemy, bool crit)
        {
            float delay = _basicFlight;
            var lineage = _lineages[_basicLineage];
            float pan = EnemyPan(in enemy);
            float importance = HitImportance(in enemy, crit);
            var mod = _basicStyle;
            float styleDb = mod == CombatStyle.Assault ? 1f : mod == CombatStyle.Snipe ? 2f : mod == CombatStyle.Support || mod == CombatStyle.Heal ? -2f : 0f;
            float stylePitch = mod == CombatStyle.Support || mod == CombatStyle.Heal ? St(1f) : 1f;
            float lampDb = Now < _hitsHalfUntil ? -6f : 0f;
            NoteIncoming(enemy.Id, Now + delay);

            // 적 보호막이 다 막았다: 계보 명중 대신 '붕' (치명타 소리도 없음)
            if (enemy.Shield > 0.0001f)
            {
                Sfx(_shieldBlock, delay, lampDb, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit, importance, enemy.Id);
                return;
            }

            if (lineage.Lullaby) LullabyHit(lineage.Hit, _basicSlot, _basicFormId, delay, pan, crit, styleDb + lampDb, importance, enemy.Id);
            else if (crit)
            {
                bool hasCrit = Sfx(lineage.Crit, delay, styleDb + lampDb, 1f, pan, AudioCategory.Hit,
                    AudioPlayFlags.Hit | AudioPlayFlags.Crit | AudioPlayFlags.ExemptCrit | AudioPlayFlags.Kept, importance, enemy.Id);
                if (!hasCrit) Sfx(lineage.Hit, delay, styleDb + lampDb, stylePitch, pan, AudioCategory.Hit, AudioPlayFlags.Hit | AudioPlayFlags.Kept, importance, enemy.Id);
            }
            else
            {
                var hit = lineage.Umbrella && _state.CurrentModifier == StageModifierType.Rain && _sink.Has(_rainBig) ? _rainBig : lineage.Hit;
                Sfx(hit, delay, styleDb + lampDb, stylePitch, pan, AudioCategory.Hit, AudioPlayFlags.Hit, importance, enemy.Id);
            }
            if (crit) Sfx(_hitCrit, delay, lampDb, 1f, pan, AudioCategory.None, AudioPlayFlags.ExemptCrit | AudioPlayFlags.MicroDuck, 0f, enemy.Id);

            // 성향 층 (§1.5): 돌격 베기 · 포획 실 · 저격 무게
            if (mod == CombatStyle.Assault) Sfx(_layerSlash, delay, lampDb, 1f, pan, AudioCategory.Hit);
            else if (mod == CombatStyle.Capture) Sfx(_layerThread, delay, lampDb, 1f, pan, AudioCategory.Hit);
            else if (mod == CombatStyle.Snipe) Sfx(_layerHeavy, delay, lampDb, 1f, pan, AudioCategory.Hit);

            // 계보 곁들임 (단추 또르르 · 촛농 똑): 절반만
            if (lineage.Extra.IsValid && Random() < ExtraChance) Sfx(lineage.Extra, delay + lineage.ExtraDelay, lampDb, 1f, pan, AudioCategory.Hit);
            // 2단 형태 금빛 반짝 (35%, −10 dB)
            if (_basicFormTier >= 2 && Random() < GoldChance) Sfx(_layerGold, delay, -10f + lampDb, 1f, pan, AudioCategory.Hit);
            HurtLayer(in enemy, delay);
        }

        void EchoHit(in BattleEnemyInfo enemy, bool crit)
        {
            float delay = _echoDelay;
            float pan = EnemyPan(in enemy);
            float importance = HitImportance(in enemy, crit);
            float lampDb = Now < _hitsHalfUntil ? -6f : 0f;
            NoteIncoming(enemy.Id, Now + delay);
            if (!_echoHitGate.TryPass(_echoSource, Now, EchoHitGap) && !crit) return;
            if (_echoToken)
            {
                Sfx(_echoTokenPop, delay, lampDb, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit, importance, enemy.Id);
                return;
            }
            if (enemy.Shield > 0.0001f)
            {
                Sfx(_shieldBlock, delay, -3f + lampDb, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit, importance, enemy.Id);
                return;
            }
            var lineage = _lineages[_echoLineage];
            if (lineage.Lullaby) LullabyHit(lineage.Hit, _echoSource, _echoFormId, delay, pan, crit, -3f + lampDb, importance, enemy.Id);
            else
            {
                var key = crit && _sink.Has(lineage.Crit) ? lineage.Crit : lineage.Hit;
                Sfx(key, delay, -3f + lampDb, St(2f), pan, AudioCategory.Hit, AudioPlayFlags.Hit | (crit ? AudioPlayFlags.Crit | AudioPlayFlags.Kept : 0), importance, enemy.Id);
            }
            if (crit) Sfx(_hitCrit, delay, -3f + lampDb, 1f, pan, AudioCategory.None, AudioPlayFlags.ExemptCrit | AudioPlayFlags.MicroDuck, 0f, enemy.Id);
            HurtLayer(in enemy, delay);
        }

        /// <summary>
        /// 라라벨 자장가 (§5.8): 소스마다 0~5번째 = C6 A5 F5 G5 A5 G5, 6번째 = 쉼표 (소리 고리만) — lala_finale만 F5를 낸다 (마지막 음).
        /// 치명타는 F 화음 (순서를 넘기지 않는다). 소리 고리(atk_musicbox_ring)는 음마다 깔린다
        /// </summary>
        void LullabyHit(AudioId note, int source, string formId, float delay, float pan, bool crit, float gainDb, float importance, int enemyId)
        {
            int transpose = _sink.Transpose;
            Sfx(_musicboxRing, delay, gainDb, 1f, pan, AudioCategory.Hit);
            if (crit)
            {
                Sfx(_musicboxCrit, delay, gainDb, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit | AudioPlayFlags.Crit | AudioPlayFlags.ExemptCrit | AudioPlayFlags.Kept,
                    importance, enemyId, 698.46f * St(transpose));
                return;
            }
            int slot = LullabySlot(source);
            int index = _lullabyIndex[slot];
            _lullabyIndex[slot] = (index + 1) % 7;
            if (index < 6)
            {
                float hz = AudioCatalog.LadderHz(AudioCatalog.LadderLullaby, index, transpose);
                Sfx(note, delay, gainDb, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit, importance, enemyId, hz);
                return;
            }
            // 7번째: 마지막 음은 피날레 형태만
            if (IsFinaleForm(formId))
                Sfx(_musicboxFinale, delay, gainDb, 1f, pan, AudioCategory.Hit, AudioPlayFlags.Hit | AudioPlayFlags.Kept, importance + 1f, enemyId,
                    698.46f * St(transpose));
        }

        static bool IsFinaleForm(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return false;
            var ids = AudioCatalog.FinaleFormIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], formId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        int LullabySlot(int source)
        {
            for (int i = 0; i < _lullabySources.Length; i++)
            {
                if (_lullabySources[i] == source) return i;
            }
            int slot = _lullabyCursor;
            _lullabyCursor = (_lullabyCursor + 1) % _lullabySources.Length;
            _lullabySources[slot] = source;
            _lullabyIndex[slot] = 0;
            return slot;
        }

        /// <summary>자장가 소스의 다음 순서 (테스트용)</summary>
        public int LullabyIndex(int source)
        {
            for (int i = 0; i < _lullabySources.Length; i++)
            {
                if (_lullabySources[i] == source) return _lullabyIndex[i];
            }
            return 0;
        }

        void HurtLayer(in BattleEnemyInfo enemy, float delay)
        {
            if (_sink.IsAsleep) return;
            float chance = Speed >= 2 ? HurtChanceFast : HurtChance;
            if (Random() >= chance) return;
            if (!_hurtGate.TryPass(enemy.Id, Now, HurtGap)) return;
            var sounds = EnemySounds(enemy.DefId, enemy.IsBoss);
            Sfx(sounds.Hurt, delay, -6f + BattleAudioTiming.ProximityDb(enemy.Position.magnitude), 1f, EnemyPan(in enemy), AudioCategory.Hurt);
        }

        void OnShieldBroken(in BattleEvent e)
        {
            if (e.TargetId < 0)
            {
                Sfx(_shieldBreak, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                return;
            }
            if (_state.TryGetEnemy(e.TargetId, out var enemy))
            {
                float delay = ContextDelay(e.SourceId, e.TargetId);
                float pan = EnemyPan(in enemy);
                Sfx(_shieldBreak, delay, -3f, 1f, pan, AudioCategory.Hit);
                Sfx(_layerHeavy, delay, 0f, 1f, pan, AudioCategory.Hit);
                return;
            }
            Sfx(_shieldBreak, 0, -5f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Status);
        }

        /// <summary>이 결과(SourceId = 공격자/시전자)가 닿는 시간 (BattleView.Cast.ContextDelay 거울)</summary>
        float ContextDelay(int sourceId, int enemyId)
        {
            if (!Fresh) return 0f;
            switch (_ctx)
            {
                case Ctx.Basic:
                    return sourceId == _basicSlot && enemyId == _basicTarget ? _basicFlight : 0f;
                case Ctx.EchoBasic:
                    return (sourceId == _echoSource || sourceId < 0) && enemyId == _echoTarget ? _echoDelay : 0f;
                case Ctx.Cast:
                    if (sourceId != _castSource && sourceId >= 0) return 0f;
                    return _state.TryGetEnemy(enemyId, out var enemy) ? CastDelay(enemyId, in enemy) : 0f;
                case Ctx.Lamp:
                    return _state.TryGetEnemy(enemyId, out var target) ? BattleAudioTiming.LampArrival(target.Position) : 0f;
            }
            return 0f;
        }

        // ── 처치 ──

        void OnEnemyKilled(in BattleEvent e)
        {
            int id = e.TargetId >= 0 ? e.TargetId : e.SourceId;
            bool known = _state.TryGetEnemy(id, out var enemy);
            if (!known) enemy = new BattleEnemyInfo { Id = id, Position = e.Position, IsBoss = e.Value >= 0.5f };
            bool boss = e.Value >= 0.5f || enemy.IsBoss;
            float incoming = IncomingRemaining(id);
            float hold = incoming > 0f ? BattleAudioTiming.KillHold(incoming) : 0f;
            if (InCast) hold = Mathf.Max(hold, CastDelay(id, in enemy));
            else if (Fresh && _ctx == Ctx.Lamp) hold = Mathf.Max(hold, BattleAudioTiming.LampArrival(enemy.Position) + 0.02f);
            float pop = BattleAudioTiming.KillPop(hold, incoming, boss);
            double popAt = Now + pop;
            float pan = EnemyPan(in enemy);

            if (boss)
            {
                BossKilled(pop, pan);
                return;
            }

            // 처치 사다리 · 한꺼번에 처치
            int recent = 0;
            for (int i = 0; i < _killPops.Length; i++)
            {
                if (_killPops[i] > popAt - MultiKillWindow && _killPops[i] <= popAt + 1e-6) recent++;
            }
            _killPops[_killCursor] = popAt;
            _killCursor = (_killCursor + 1) % _killPops.Length;
            recent++;
            double gap = popAt - _lastKillPop;
            if (gap <= KillChainWindow) _streak++;
            else if (gap > KillChainReset) _streak = 1;
            if (popAt > _lastKillPop) _lastKillPop = popAt;
            var flags = Now < _bossKillUntil ? AudioPlayFlags.ExemptBoss : AudioPlayFlags.None;
            if (recent >= MultiKillCount && popAt >= _noChainUntil)
            {
                if (Sfx(_killMulti, pop, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget | flags)) _noChainUntil = popAt + NoChainAfterMulti;
            }
            else if (_streak >= 2 && popAt >= _noChainUntil)
            {
                int step = Mathf.Min(_streak - 2, AudioCatalog.Ladders[AudioCatalog.LadderKill].Count - 1);
                float hz = AudioCatalog.LadderHz(AudioCatalog.LadderKill, step, _sink.Transpose);
                Sfx(_killChain, pop, 0f, 1f, pan, AudioCategory.None, flags, 0f, id, hz);
            }

            // 처치 소리: 같은 80ms에 셋 이상이면 둘만 (한꺼번에 처치가 대신 말한다)
            int crowd = 0;
            for (int i = 0; i < _deathPops.Length; i++)
            {
                if (Math.Abs(_deathPops[i] - popAt) <= DeathCrowdWindow * 0.5) crowd++;
            }
            if (crowd < DeathCrowdKeep)
            {
                var sounds = EnemySounds(enemy.DefId, false);
                if (Sfx(sounds.Death, pop, BattleAudioTiming.ProximityDb(enemy.Position.magnitude), 1f, pan, AudioCategory.Death, AudioPlayFlags.None, 0f, id))
                {
                    _deathPops[_deathCursor] = popAt;
                    _deathCursor = (_deathCursor + 1) % _deathPops.Length;
                }
            }

            // 처치 단추 (5마리째마다, 분열 적 제외) · 기억 조각이 문에 닿는 소리
            if (!enemy.IsSplit)
            {
                _rewardKills++;
                if (_rewardKills % EnemyFxCatalog.RewardEvery == 0)
                {
                    float coin = pop + BattleAudioTiming.KillCoinLead;
                    Sfx(_coinPop, coin, 0f, 1f, pan, AudioCategory.None);
                    Sfx(_coinLand, coin + EnemyFxCatalog.RewardCoinFlight, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                }
            }
            Sfx(_killMote, pop + BattleAudioTiming.KillMoteDelay, -14f, 1f, 0f, AudioCategory.None);
        }

        void BossKilled(float pop, float pan)
        {
            var flags = AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptBoss | AudioPlayFlags.NoDrag;
            Sfx(_bossKillImpact, pop, 0f, 1f, pan, AudioCategory.None, flags);
            Sfx(_bossDeath, pop, 0f, 1f, pan, AudioCategory.None, flags);
            // 전체 정지 거울 · 덕 (Bgm −10 · Sfx −6, 보스 소리 빼고) · 징글 +0.3초
            _mirrorStopAt = Now + pop;
            _sink.Duck(AudioBus.Bgm, -10f, 1.2f, 0.03f, 1.0f, DuckReason.BossKill, DuckExempt.None, pop);
            _sink.Duck(AudioBus.Sfx, -6f, 1.2f, 0.03f, 1.0f, DuckReason.BossKill, DuckExempt.Boss, pop);
            _sink.Duck(AudioBus.Bgm, -6f, HitFeel.GlobalStop, 0.02f, 0.5f, DuckReason.SlowDrag, DuckExempt.None, pop);
            _sink.SuppressEmitters(pop + 1.5f);
            _bossKillUntil = Now + pop + 0.8f;
            // 보스 처치 징글은 +0.3초 (같은 프레임의 StageEnded가 챕터 첫 클리어면 그 자리에 챕터 징글)
            _bossJingleAt = Now + pop + BossDefeatedDelay;
            _bossJingleChapter = false;
            _lastBossDefeated = _sink.UiNow + pop + BossDefeatedDelay;
            // 챕터 보스 음악은 징글이 끝난 뒤 Main으로
            if (_bossMusicOn) _bossMusicHoldUntil = _sink.UiNow + pop + BossDefeatedDelay + BossDefeatedLength;
            _sink.StopLoop(1, 0.04f);
            _windupOn = false;
        }

        // ── 적 등장 · 공격 ──

        void OnEnemySpawned(in BattleEvent e)
        {
            if (e.Value >= 0.5f) return;
            if (!_state.TryGetEnemy(e.SourceId, out var spawned) || spawned.IsSplit || spawned.IsBoss) return;
            if (_minionFrame != _frame && _state.IsBossMinion(in spawned))
            {
                _minionFrame = _frame;
                Sfx(_bossSummon, 0, 0f, 1f, EnemyPan(in spawned), AudioCategory.None, AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptBoss);
                return;
            }
            if (_minionFrame == _frame) return;
            Sfx(_enemySpawn, 0, -4f + BattleAudioTiming.ProximityDb(spawned.Position.magnitude), 1f, EnemyPan(in spawned), AudioCategory.Spawn);
        }

        void OnBossSpawned(in BattleEvent e)
        {
            _state.TryGetEnemy(e.SourceId, out var boss);
            var flags = AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptBoss | AudioPlayFlags.NoDrag;
            Sfx(_bossAppear, 0, 0f, 1f, BattleAudioTiming.Pan(boss.Position.x, true), AudioCategory.None, flags);
            Sfx(_jBossAppear, BossJingleDelay, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
            _bossAlive = true;
            _sink.SetBossAlive(true);
            if (IsChapterStage(_state.Stage)) _bossMusicAt = _sink.UiNow + BossMusicDelay;
        }

        static bool IsChapterStage(int stage) => stage > 0 && stage % 10 == 0;

        void OnEnemyStrike(in BattleEvent e)
        {
            bool known = _state.TryGetEnemy(e.SourceId, out var enemy);
            string defId = !string.IsNullOrEmpty(e.Key) ? e.Key : known ? enemy.DefId : null;
            bool boss = known && enemy.IsBoss;
            if (e.TargetId >= 0)
            {
                // 잔향이를 때림: 적마다 제 주기 (그림과 같은 간격)
                if (!_strikeGate.TryPass(e.SourceId, Now, Math.Max(0.05f, BattleAudioTiming.StrikeGap(defId, boss, Speed)))) return;
                var sounds = EnemySounds(defId, boss);
                float pan = known ? EnemyPan(in enemy) : BattleAudioTiming.Pan(e.Position.x, boss);
                float near = known ? BattleAudioTiming.ProximityDb(enemy.Position.magnitude) : 0f;
                Sfx(sounds.Strike, 0, -3f + near, 1f, pan, AudioCategory.Strike, boss ? AudioPlayFlags.ExemptBoss : 0);
                Sfx(_echoHurt, 0, -6f, 1f, pan, AudioCategory.Hurt);
                return;
            }
            StrikeShop(e.SourceId, e.Position, e.Value, defId);
        }

        void StrikeShop(int enemyId, Vector2 edge, float damage, string defId)
        {
            bool known = _state.TryGetEnemy(enemyId, out var enemy);
            if (defId == null && known) defId = enemy.DefId;
            bool boss = known && enemy.IsBoss;
            _shopStrikeFrame = _frame;
            _shopStrikeSource = enemyId;
            float pan = BattleAudioTiming.Pan(edge.x, boss);
            var sounds = EnemySounds(defId, boss);
            var flags = AudioPlayFlags.BypassBudget | (boss ? AudioPlayFlags.ExemptBoss : 0);
            Sfx(sounds.Strike, 0, 0f, 1f, pan, AudioCategory.Strike, boss ? AudioPlayFlags.ExemptBoss : 0);
            bool absorbed = _state.SimReady && _state.ShopShield > 0.01f && _state.ShopShield >= damage - 0.01f;
            if (absorbed) Sfx(_shopShieldHit, 0, 0f, 1f, pan, AudioCategory.None, flags);
            else if (boss)
            {
                Sfx(_shopHitBoss, 0, 0f, 1f, pan, AudioCategory.None, flags | AudioPlayFlags.NoDrag);
                Sfx(_bossSiegeHit, 0, 0f, 1f, pan, AudioCategory.None, flags | AudioPlayFlags.NoDrag);
            }
            else Sfx(_shopHit, 0, 0f, 1f, pan, AudioCategory.None, flags);
            if (boss)
            {
                _sink.StopLoop(1, 0.04f);
                _windupOn = false;
            }
        }

        // ── 수선점 · 잔향이 회복 / 보호막 / 맥박 ──

        void OnShopHealed(in BattleEvent e)
        {
            bool pulse = !InCast && Fresh && _ctx == Ctx.Pulse && e.SourceId == _pulseSlot;
            if (pulse) Sfx(_healTick, BattleAudioTiming.PulseShopDrop, 0f, 1f, 0f, AudioCategory.Status);
            else Sfx(_shopHeal, 0, 0f, 1f, 0f, AudioCategory.Status);
        }

        void OnEchoHealed(in BattleEvent e)
        {
            if (!_miscGate.TryPass(3000000 + e.TargetId, Now, EchoHealGap)) return;
            if (!_echoHealBucket.TryTake(_sink.UiNow)) return;
            float delay = 0f;
            if (Fresh && _ctx == Ctx.Pulse && _pulseHeal && _state.TryGetEcho(e.TargetId, out var echo))
            {
                float radius = Mathf.Max(0.01f, _state.HealEchoRadius);
                delay = BattleAudioTiming.HealRingDelay((echo.Position - BattleGeometry.GuardianPosition(_pulseSlot)).magnitude / radius);
            }
            else if (InCast) delay = Gift(e.SourceId, e.TargetId);
            Sfx(_healTick, delay, -3f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Status);
        }

        void OnGuardianPulse(in BattleEvent e)
        {
            Begin(Ctx.Pulse);
            _pulseSlot = e.SourceId;
            _pulseHeal = e.Key == BattleEventKeys.PulseHeal || (e.Key == null && Mathf.RoundToInt(e.Value) == (int)CombatStyle.Heal);
            Sfx(_pulseHeal ? _pulseHealKey : _pulseSupport, 0, 0f, 1f, GuardianPan(Mathf.Clamp(e.SourceId, 0, 4)), AudioCategory.Status);
        }


        void OnShieldGained(in BattleEvent e)
        {
            var key = e.Key == SkillVfx.ButtonShield ? _shieldButton : e.Key == SkillVfx.UmbrellaShield ? _shieldUmbrella : _shieldBubble;
            if (e.TargetId < 0)
            {
                bool first = _shopShieldFrame != _frame;
                float delay = InCast ? Gift(e.SourceId, -1) : 0f;
                if (!first) return;
                _shopShieldFrame = _frame;
                Sfx(key, delay, 0f, 1f, 0f, AudioCategory.Status);
                return;
            }
            float echoDelay = InCast ? Gift(e.SourceId, e.TargetId) : 0f;
            if (!_statusGate.TryPass(e.TargetId * 64 + (int)SkillEffectType.Shield, Now, StatusTargetGap)) return;
            Sfx(key, echoDelay, -3f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Status);
        }

        /// <summary>아군 전체 스킬 선물 (받은 친구로 세고, 멀면 0.3초 뒤)</summary>
        float Gift(int sourceId, int recipient)
        {
            if (!InCast || !_castAllyGroup || sourceId != _castSource) return 0f;
            _castRecipients++;
            Vector2 to;
            if (recipient < 0) to = _state.WorldToField(Vector2.zero);
            else if (_state.TryGetEcho(recipient, out var echo)) to = _state.WorldToField(echo.Position);
            else to = _castFrom;
            float delay = (to - _castFrom).sqrMagnitude < BattleAudioTiming.GiftMinDistance * BattleAudioTiming.GiftMinDistance ? 0f : BattleAudioTiming.GiftTravel;
            // 받는 친구마다 선물 소리 (시전 하나에 3개까지, 선물이 닿는 순간)
            if (_castRecipients <= 3) Sfx(_skillGift, delay, 0f, 1f, _castPan, AudioCategory.Status);
            return delay;
        }

        // ── 상태 ──

        void OnStatusApplied(in BattleEvent e)
        {
            var type = (SkillEffectType)Mathf.Clamp(Mathf.RoundToInt(e.Value), 0, 63);
            string key = e.Key;
            bool allyTarget = key != null && (key.StartsWith("echo:", StringComparison.Ordinal) || key.StartsWith("guardian:", StringComparison.Ordinal));
            float pan = BattleAudioTiming.Pan(e.Position.x);

            // 적이 아군에게 거는 약화 (녹 · 갉음)
            if (allyTarget && e.SourceId < 0 && key.EndsWith(SkillVfx.DebuffMark, StringComparison.Ordinal))
            {
                var sound = type == SkillEffectType.Slow ? _enemyRust : type == SkillEffectType.Mark ? _enemyGnaw : AudioId.None;
                if (_statusGate.TryPass(e.TargetId * 64 + (int)type + 32, Now, StatusTargetGap)) Sfx(sound, 0, 0f, 1f, pan, AudioCategory.Status);
                return;
            }
            // 고치는 적 (Mender)이 다른 적을 고침
            if (type == SkillEffectType.Heal && !allyTarget && key == SkillVfx.PatchHeal && _state.TryGetEnemy(e.TargetId, out _) && _state.TryGetEnemy(e.SourceId, out _))
            {
                if (_miscGate.TryPass(4000000 + e.SourceId, Now, MendGap)) Sfx(_enemyMend, 0, 0f, 1f, pan, AudioCategory.Status);
                return;
            }
            // 연쇄: 시전 하나 안에서 튈 때마다 사다리 한 칸, 닿는 순간에
            if (type == SkillEffectType.Chain && InCast)
            {
                float arrive = ChainHop(e.TargetId);
                int step = Mathf.Min(_chainHops - 1, AudioCatalog.Ladders[AudioCatalog.LadderChain].Count - 1);
                Sfx(_statusChain, arrive, 0f, 1f, pan, AudioCategory.Status, AudioPlayFlags.None, 0f, e.TargetId,
                    AudioCatalog.LadderHz(AudioCatalog.LadderChain, step, _sink.Transpose));
                return;
            }

            int index = (int)type;
            var id = index < _statusByType.Length ? _statusByType[index] : AudioId.None;
            if (!allyTarget && key == SkillVfx.DebuffMark && type != SkillEffectType.Mark) id = _statusDebuff;
            if (!id.IsValid) return;
            float gainDb = 0f;
            float delay = 0f;
            if (InCast)
            {
                // 시전의 첫 대상만, 그리고 상태 그림이 시전 그림과 다를 때만 (같으면 시전 소리가 이미 말했다)
                if (_castStatusDone || SameVfx(key, _castVfx)) return;
                _castStatusDone = true;
                gainDb = -4f;
                delay = allyTarget || e.TargetId < 0 ? Gift(e.SourceId, -1) : ContextDelay(e.SourceId, e.TargetId);
            }
            else if (!allyTarget && e.TargetId >= 0) delay = ContextDelay(e.SourceId, e.TargetId);
            if (!_statusGate.TryPass(e.TargetId * 64 + index, Now, StatusTargetGap)) return;
            if (!_typeGate.TryPass(index, Now, StatusTypeGap)) return;
            Sfx(id, delay, gainDb, 1f, pan, AudioCategory.Status);
        }

        /// <summary>상태 키("echo:vfx_x" 같은 접두 포함)의 그림이 시전 그림과 같은지 (문자열을 만들지 않고 비교)</summary>
        static bool SameVfx(string statusKey, string castVfx)
        {
            if (statusKey == null || castVfx == null) return false;
            int colon = statusKey.IndexOf(':');
            int start = colon >= 0 ? colon + 1 : 0;
            int length = statusKey.Length - start;
            return length == castVfx.Length && string.CompareOrdinal(statusKey, start, castVfx, 0, length) == 0;
        }

        float ChainHop(int enemyId)
        {
            float start;
            float travel;
            if (_chainHops == 0)
            {
                start = 0f;
                travel = _castProjectile ? _castFlight : BattleAudioTiming.FirstHop;
            }
            else
            {
                start = _chainTime;
                travel = BattleAudioTiming.HopTime;
            }
            float arrive = start + travel;
            RegisterDelay(enemyId, arrive);
            _chainTime = arrive;
            _chainHops++;
            return arrive;
        }

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

        // ── 스킬 ──

        void OnSkillCast(in BattleEvent e)
        {
            var skill = _state.GetSkill(e.Key);
            string vfx = skill != null ? skill.vfx : null;
            var look = SkillVfxLook.Get(vfx);
            var targetType = skill != null ? skill.target : SkillTargetType.EnemiesInRange;
            bool allyGroup = (targetType == SkillTargetType.AllEchoes || targetType == SkillTargetType.AllGuardians) && !HasEnemyEffect(skill);

            // 시전자 자리
            Vector2 casterWorld;
            bool echoCaster = e.SourceId >= BattleSimulator.EchoIdBase;
            float scale = _state.ActorScale;
            Vector2 from;
            if (!echoCaster && e.SourceId >= 0 && e.SourceId < BattleGeometry.SlotCount)
            {
                casterWorld = BattleGeometry.GuardianPosition(e.SourceId);
                from = _state.WorldToField(casterWorld) + Vector2.up * (BattleAudioTiming.MuzzleLift * scale);
            }
            else if (echoCaster && _state.TryGetEcho(e.SourceId, out var echo))
            {
                casterWorld = echo.Position;
                from = _state.WorldToField(casterWorld) + Vector2.up * (BattleAudioTiming.EchoBodyLift * scale);
            }
            else
            {
                casterWorld = e.Position;
                from = _state.WorldToField(casterWorld) + Vector2.up * 40f;
            }
            float pan = BattleAudioTiming.Pan(casterWorld.x);

            // 같은 프레임 같은 스킬이 같은 자리에 이미 → 소리 없이 시간표만 (시너지 보너스 스킬)
            var center = targetType == SkillTargetType.Shop ? _state.WorldToField(Vector2.zero) : _state.WorldToField(e.Position);
            bool shared = RememberCast(e.Key, center, targetType);

            Begin(Ctx.Cast);
            _castSource = e.SourceId;
            _castSkill = e.Key;
            _castVfx = vfx;
            _castStyle = look.Style;
            _castAllyGroup = allyGroup;
            _castRecipients = 0;
            _castHits = 0;
            _castStatusDone = false;
            _castPan = pan;
            _castPrimary = e.TargetId;
            _castFrom = from;
            _castCenter = center;
            float ppu = _state.Ppu;
            float radiusWorld = e.Value > 0.01f ? e.Value : skill != null ? skill.radius : 0.6f;
            _castRadiusPx = Mathf.Clamp(radiusWorld * ppu, ppu * 0.28f, ppu * 2.4f);
            _castProjectile = !allyGroup && look.Style == SkillVfxStyle.Projectile;
            bool lane = targetType == SkillTargetType.Lane;
            _castSegment = !allyGroup && (look.Style == SkillVfxStyle.Beam || (lane && look.Style == SkillVfxStyle.Projectile));
            _castStagger = !allyGroup && (look.Style == SkillVfxStyle.Burst || look.Style == SkillVfxStyle.Ring || look.Style == SkillVfxStyle.Orbit ||
                look.Style == SkillVfxStyle.Rain);
            _castSplash = !allyGroup && targetType == SkillTargetType.AllEnemies;
            _chainHops = 0;
            _chainTime = 0f;
            _delayCount = 0;
            _castEnd = LaneEnd(e.SourceId, echoCaster, e.Position, e.TargetId);
            _castFlight = 0f;
            if (_castProjectile) _castFlight = BattleAudioTiming.SkillFlight(vfx, lane, from, _castEnd);
            else if (look.Style == SkillVfxStyle.Beam) _castFlight = BattleFx.BeamReach;
            else if (_castStagger && _state.TryGetEnemy(e.TargetId, out var primary))
                _castFlight = BattleAudioTiming.AreaDelay(look.Style, _state.WorldToField(primary.Position) - center, _castRadiusPx, _state.Squash);

            if (shared) return;
            float echoDb = echoCaster ? -4f : 0f;
            float echoPitch = echoCaster ? St(2f) : 1f;
            Sfx(_skillCharge, 0, echoDb, echoPitch, pan, AudioCategory.Status);
            int vfxIndex = vfx != null && _vfxIndex.TryGetValue(vfx, out int found) ? found : -1;
            var cast = vfxIndex >= 0 && _sink.Has(_vfxCast[vfxIndex]) ? _vfxCast[vfxIndex] : AudioId.None;
            if (!cast.IsValid)
            {
                int style = (int)look.Style;
                cast = style >= 0 && style < _styleCast.Length && _sink.Has(_styleCast[style]) ? _styleCast[style] : _skillCast;
            }
            Sfx(cast, 0, echoDb, 1f, pan, AudioCategory.Status);
            if (vfxIndex >= 0 && _vfxHit[vfxIndex].IsValid) Sfx(_vfxHit[vfxIndex], _castFlight, 0f, 1f, pan, AudioCategory.Status);
            if (IsBigSkill(skill) && AllowBig(e.Key))
            {
                Sfx(_skillBig, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                _sink.Duck(AudioBus.Bgm, -4f, 0.8f, 0.05f, 0.6f, DuckReason.SkillBig, DuckExempt.None, 0);
            }
        }

        Vector2 LaneEnd(int sourceId, bool echoCaster, Vector2 origin, int targetId)
        {
            if (!echoCaster && sourceId >= 0 && sourceId < BattleGeometry.SlotCount)
                return _state.WorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(sourceId), 2.3f)) + Vector2.up * 40f;
            if (origin.sqrMagnitude > 0.0004f)
            {
                var end = origin + origin.normalized * Mathf.Max(0.6f, 2.3f - origin.magnitude);
                return _state.WorldToField(end) + Vector2.up * 40f;
            }
            return _state.TryGetEnemy(targetId, out var target) ? _state.WorldToField(target.Position) + Vector2.up * 40f : _castFrom;
        }

        /// <summary>지금 시전의 결과가 그 적에 닿는 시간 (BattleView.Cast.CastDelay 거울)</summary>
        float CastDelay(int enemyId, in BattleEnemyInfo enemy)
        {
            for (int i = 0; i < _delayCount; i++)
            {
                if (_delayIds[i] == enemyId) return _delayTimes[i];
            }
            float scale = _state.ActorScale;
            var feet = _state.WorldToField(enemy.Position);
            var hitPoint = feet + Vector2.up * BattleAudioTiming.EnemyHitLift(enemy.IsBoss, enemy.IsSplit, scale);
            if (_castSplash)
            {
                float splash = BattleAudioTiming.SplashDelay((hitPoint - _castCenter).magnitude);
                RegisterDelay(enemyId, splash);
                return splash;
            }
            if (_castSegment)
            {
                var segment = _castEnd - _castFrom;
                float lengthSq = segment.sqrMagnitude;
                float along = lengthSq > 1f ? Mathf.Clamp01(Vector2.Dot(hitPoint - _castFrom, segment) / lengthSq) : 1f;
                return _castStyle == SkillVfxStyle.Beam ? BattleAudioTiming.BeamDelay(along) : _castFlight * along;
            }
            if (_castStagger)
            {
                float reach = BattleAudioTiming.AreaDelay(_castStyle, feet - _castCenter, _castRadiusPx, _state.Squash);
                RegisterDelay(enemyId, reach);
                return reach;
            }
            return _castProjectile ? _castFlight : 0f;
        }

        bool RememberCast(string skillId, Vector2 center, SkillTargetType type)
        {
            if (_castStampFrame != _frame)
            {
                _castStampFrame = _frame;
                _castStampCount = 0;
            }
            bool global = type == SkillTargetType.AllEnemies || type == SkillTargetType.Shop || type == SkillTargetType.AllEchoes ||
                type == SkillTargetType.AllGuardians || type == SkillTargetType.AllAllies;
            for (int i = 0; i < _castStampCount; i++)
            {
                if (!string.Equals(_castStampSkill[i], skillId, StringComparison.Ordinal)) continue;
                if (global || (_castStampCenter[i] - center).sqrMagnitude < 40f * 40f) return true;
            }
            if (_castStampCount < _castStampSkill.Length)
            {
                _castStampSkill[_castStampCount] = skillId;
                _castStampCenter[_castStampCount] = center;
                _castStampCount++;
            }
            return false;
        }

        bool AllowBig(string skillId)
        {
            double now = _sink.UiNow;
            for (int i = 0; i < _bigSkills.Length; i++)
            {
                if (!string.Equals(_bigSkills[i], skillId, StringComparison.Ordinal)) continue;
                if (now - _bigTimes[i] < BigSkillGap) return false;
                _bigTimes[i] = now;
                return true;
            }
            _bigSkills[_bigCursor] = skillId;
            _bigTimes[_bigCursor] = now;
            _bigCursor = (_bigCursor + 1) % _bigSkills.Length;
            return true;
        }

        /// <summary>이름 알림이 뜰 만큼 드문 큰 스킬 (BattleView.Skills.IsBigSkill 거울)</summary>
        public static bool IsBigSkill(SkillDef skill)
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

        // ── 불 밝히기 ──

        void OnLampLit()
        {
            var flags = AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptLamp | AudioPlayFlags.NoDrag;
            Sfx(_lampLit, 0, 0f, 1f, 0f, AudioCategory.None, flags);
            Sfx(_lampWave, LampWave.Start, 0f, 1f, 0f, AudioCategory.None, flags);
            _mirror.Trigger(0f, LampWave.IgniteScale, LampWave.IgniteTime);
            _sink.Duck(AudioBus.Bgm, -8f, 1.0f, 0.05f, 0.9f, DuckReason.LampLit, DuckExempt.None, 0);
            _sink.Duck(AudioBus.Sfx, -3f, 1.0f, 0.05f, 0.9f, DuckReason.LampLit, DuckExempt.Lamp, 0);
            _sink.Duck(AudioBus.Bgm, -6f, LampWave.IgniteTime, 0.02f, 0.5f, DuckReason.SlowDrag, DuckExempt.None, 0);
            _launchMuteUntil = Now + LaunchMuteAfterLamp;
            _hitsHalfUntil = Now + HitsHalfAfterLamp;
            _lampLitAt = Now;
            _lampFullAt = double.NegativeInfinity;
            _lampRemindersLeft = 0;
            Begin(Ctx.Lamp);
            _lampCount = 0;
        }

        /// <summary>불빛 물결이 닿는 순서대로 C5 F5 A5 C6 F6 A6 C7 (최대 7음, 나머지는 조용히)</summary>
        void FlushLampNotes()
        {
            int count = _lampCount;
            _lampCount = 0;
            if (count == 0) return;
            // 도착 순서로 (삽입 정렬, 48칸)
            for (int i = 1; i < count; i++)
            {
                float d = _lampDelays[i];
                int id = _lampIds[i];
                float p = _lampPans[i];
                int j = i - 1;
                while (j >= 0 && _lampDelays[j] > d)
                {
                    _lampDelays[j + 1] = _lampDelays[j];
                    _lampIds[j + 1] = _lampIds[j];
                    _lampPans[j + 1] = _lampPans[j];
                    j--;
                }
                _lampDelays[j + 1] = d;
                _lampIds[j + 1] = id;
                _lampPans[j + 1] = p;
            }
            int notes = Math.Min(count, LampNotes);
            int transpose = _sink.Transpose;
            for (int i = 0; i < notes; i++)
            {
                float hz = AudioCatalog.LadderHz(AudioCatalog.LadderLamp, i, transpose);
                Sfx(_hitLamp, _lampDelays[i], 0f, 1f, _lampPans[i], AudioCategory.None,
                    AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptLamp, 0f, _lampIds[i], hz, (int)AudioVoiceClass.High);
            }
        }

        void OnEnemyEnraged(in BattleEvent e)
        {
            bool boss = _state.TryGetEnemy(e.TargetId, out var enemy) && enemy.IsBoss;
            if (boss)
            {
                Sfx(_bossEnrage, 0, 0f, 1f, EnemyPan(in enemy), AudioCategory.None, AudioPlayFlags.BypassBudget | AudioPlayFlags.ExemptBoss);
                _sink.Duck(AudioBus.Bgm, -4f, 1f, 0.05f, 0.5f, DuckReason.BossEnrage, DuckExempt.None, 0);
                return;
            }
            Sfx(_enemyEnrage, 0, 0f, 1f, BattleAudioTiming.Pan(e.Position.x), AudioCategory.Status);
        }

        void OnSynergy()
        {
            if (_synergyFrame == _frame) return;
            _synergyFrame = _frame;
            int signature = _state.SimReady ? _state.SynergySignature : 0;
            bool changed = signature != _lastSynergySignature;
            _lastSynergySignature = signature;
            if (!changed && !_firstStage) return;
            Sfx(_synergy, SynergyDelay, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
        }

        // ───────────── 시스템 이벤트 ─────────────

        public void OnStageChanged(int frame)
        {
            _frame = frame;
            _state.MarkSimReady();
            EndContext();
            _sink.FlushBattle();
            ResetStage();
            _stageHighest = _state.HighestStage;
            _sink.SetModifier(_state.CurrentModifier, true);
            float gain = _sink.IsJinglePlaying ? -6f : 0f;
            Sfx(_stageStart, 0, gain, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
            _bossAlive = false;
            _sink.SetBossAlive(false);
            _bossMusicAt = double.NegativeInfinity;
            if (_bossMusicOn && _sink.UiNow >= _bossMusicHoldUntil)
            {
                _bossMusicOn = false;
                _sink.SetBossMusic(false);
            }
            if (_firstStage) _lastSynergySignature = int.MinValue;
        }

        public void OnStageEnded(BattleOutcome outcome)
        {
            EndContext();
            _sink.StopLoop(0, 0.5f);
            _sink.StopLoop(1, 0.2f);
            _lowHp = false;
            _windupOn = false;
            _firstStage = false;
            if (outcome == BattleOutcome.Cleared)
            {
                int stage = _state.Stage;
                bool chapterFirst = IsChapterStage(stage) && _state.HighestStage > _stageHighest;
                bool bossJinglePending = _bossJingleAt > double.NegativeInfinity;
                if (chapterFirst)
                {
                    // 챕터 첫 클리어: 보스 처치 징글 자리에 챕터 징글 (없으면 바로)
                    if (bossJinglePending) _bossJingleChapter = true;
                    else Sfx(_jChapterClear, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                }
                else if (!bossJinglePending && !(_lastBossDefeated > _sink.UiNow - ChapterClearSkip))
                {
                    // 보스 처치 징글이 방금(1.5초 안) 울렸거나 울릴 참이면 클리어 징글은 쉰다
                    Sfx(_jStageClear, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                }
            }
            else if (outcome == BattleOutcome.Failed)
            {
                Sfx(_jStageFail, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                _bossMusicOn = false;
                _bossMusicAt = double.NegativeInfinity;
                _sink.SetBossMusic(false);
            }
        }

        public void OnStepBack() => Sfx(_stepBack, StepBackDelay, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);

        // ───────────── 매 프레임 상태 살피기 (§5.5) ─────────────

        public void Poll(float dt, int frame)
        {
            EndFrame(frame);
            double ui = _sink.UiNow;
            if (_bossJingleAt > double.NegativeInfinity && Now >= _bossJingleAt)
            {
                _bossJingleAt = double.NegativeInfinity;
                Sfx(_bossJingleChapter ? _jChapterClear : _jBossDefeated, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                _bossJingleChapter = false;
            }
            if (_bossMusicAt > double.NegativeInfinity && ui >= _bossMusicAt)
            {
                _bossMusicAt = double.NegativeInfinity;
                if (_bossAlive)
                {
                    _bossMusicOn = true;
                    _sink.SetBossMusic(true);
                }
            }
            if (_bossMusicOn && !_bossAlive && ui >= _bossMusicHoldUntil)
            {
                _bossMusicOn = false;
                _sink.SetBossMusic(false);
            }
            if (!_state.SimReady)
            {
                if (_state.IsBetweenStages) _state.MarkSimReady();
                else return;
            }
            if (_state.IsBetweenStages) return;

            // 보스 살아 있음 → amb_boss · 쉼 없음
            bool bossAlive = _state.TryGetBoss(out var boss);
            if (bossAlive != _bossAlive)
            {
                _bossAlive = bossAlive;
                _sink.SetBossAlive(bossAlive);
            }

            // 보스 포위 예비 동작 (반복: 볼륨 w^1.3 × 0.8, 음높이 0.9 + 0.3w)
            float w = bossAlive ? boss.SiegeWindup : 0f;
            if (w > WindupStart)
            {
                _sink.SetLoop(1, _bossWindup, Mathf.Pow(w, 1.3f) * 0.8f, 0.9f + 0.3f * w, _windupOn ? 0.05f : 0.03f);
                _windupOn = true;
            }
            else if (_windupOn)
            {
                _sink.StopLoop(1, 0.04f);
                _windupOn = false;
            }

            // 수선점 위험 (30% 아래: 경고 + 심장 박동 — 볼륨 −14 → −6 dB, 빠르기 1 → 1.25)
            float max = _state.ShopMaxHp;
            float hp = _state.ShopHp;
            if (max > 0f)
            {
                float r = Mathf.Clamp01(hp / max);
                if (!_lowHp && r < LowHpEnter && hp > 0f)
                {
                    _lowHp = true;
                    Sfx(_shopLowEnter, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                }
                else if (_lowHp && (r >= LowHpExit || hp <= 0f))
                {
                    _lowHp = false;
                    _sink.StopLoop(0, 0.5f);
                }
                if (_lowHp)
                {
                    float k = 1f - Mathf.Clamp01(r / LowHpEnter);
                    _sink.SetLoop(0, _shopHeartbeat, AudioGate.DbToGain(Mathf.Lerp(-14f, -6f, k)), Mathf.Lerp(1f, 1.25f, k), 0.3f);
                }
            }

            // 불빛 게이지 25 / 50 / 75% → F5 A5 C6, 가득 → lamp_full (곧 불을 켜면 생략), 켜지 않고 두면 12초마다 −8 dB 세 번
            float lamp = Mathf.Clamp01(_state.LampGauge);
            if (lamp < _lastLamp - 0.2f)
            {
                _lampSteps = 0;
                _lampWasFull = false;
            }
            int crossed = 0;
            while (_lampSteps < 3 && lamp >= 0.25f * (_lampSteps + 1))
            {
                // 한 프레임에 여러 칸을 넘으면 0.09초 간격으로 차례로 (같은 순간이면 게이트에 막힌다)
                float hz = AudioCatalog.LadderHz(AudioCatalog.LadderGauge, _lampSteps, _sink.Transpose);
                Sfx(_lampTick, crossed * 0.09, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget, 0f, -1, hz);
                _lampSteps++;
                crossed++;
            }
            if (lamp >= 0.999f && !_lampWasFull)
            {
                _lampWasFull = true;
                _lampFullAt = Now + LampFullConfirm;
            }
            if (_lampFullAt > double.NegativeInfinity && Now >= _lampFullAt)
            {
                _lampFullAt = double.NegativeInfinity;
                if (_lampLitAt < Now - LampFullConfirm - 0.05 && lamp >= 0.999f)
                {
                    Sfx(_lampFull, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                    _lampReminderAt = ui + LampReminder;
                    _lampRemindersLeft = LampReminders;
                }
            }
            if (_lampRemindersLeft > 0 && ui >= _lampReminderAt)
            {
                if (lamp >= 0.999f)
                {
                    Sfx(_lampFull, 0, -8f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                    _lampRemindersLeft--;
                    _lampReminderAt = ui + LampReminder;
                }
                else _lampRemindersLeft = 0;
            }
            if (lamp < 0.999f) _lampWasFull = false;
            _lastLamp = lamp;

            // 웨이브
            int wave = _state.Wave;
            if (wave > _lastWave)
            {
                if (wave > 1 && _lastWave > 0) Sfx(_wave, 0, 0f, 1f, 0f, AudioCategory.None, AudioPlayFlags.BypassBudget);
                _lastWave = wave;
            }
        }
    }
}
