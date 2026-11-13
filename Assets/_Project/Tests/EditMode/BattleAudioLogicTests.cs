using System.Collections.Generic;
using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// 전투 소리 감독 규칙 (AUDIO-SPEC §5): 같은 프레임 묶음(GuardianAttack → CriticalHit → EnemyHit → EnemyKilled)이 맞는 키를 맞는 시각에 낸다
    /// (쏘기 = 웅크림 W, 명중 = 비행 시간, 처치 = 처치 꽃), 2배속 규칙, 라라벨 7번째 쉼표와 피날레의 F, 불빛 물결 사다리는 도착 순서,
    /// 챕터 보스(x-10)만 보스 음악, 처치 사다리, 적 공격 간격, 보스 처치 징글 · 챕터 징글, 수선점 피격 갈래, 잠, 게이지 음.
    /// 가짜 상태 · 출력으로 돌린다 (Unity 소리 없음).
    /// </summary>
    public class BattleAudioLogicTests
    {
        sealed class FakeState : IBattleAudioState
        {
            public readonly Dictionary<int, BattleEnemyInfo> Enemies = new();
            public readonly BattleUnitInfo?[] Guardians = new BattleUnitInfo?[5];
            public readonly Dictionary<int, BattleUnitInfo> Echoes = new();
            public int Speed { get; set; } = 1;
            public int Stage { get; set; } = 1;
            public int HighestStage { get; set; } = 1;
            public bool IsBetweenStages { get; set; }
            public StageModifierType CurrentModifier { get; set; }
            public bool SimReady { get; set; } = true;
            public void MarkSimReady() => SimReady = true;
            public float ShopHp { get; set; } = 100f;
            public float ShopMaxHp { get; set; } = 100f;
            public float ShopShield { get; set; }
            public float LampGauge { get; set; }
            public int Wave { get; set; }
            public int SynergySignature { get; set; }

            public bool TryGetEnemy(int id, out BattleEnemyInfo info) => Enemies.TryGetValue(id, out info);

            public bool TryGetBoss(out BattleEnemyInfo boss)
            {
                foreach (var e in Enemies.Values)
                {
                    if (!e.IsBoss || !e.Alive) continue;
                    boss = e;
                    return true;
                }
                boss = default;
                return false;
            }

            public bool IsBossMinion(in BattleEnemyInfo spawned) => false;

            public bool TryGetGuardian(int slot, out BattleUnitInfo info)
            {
                info = slot >= 0 && slot < 5 && Guardians[slot].HasValue ? Guardians[slot].Value : default;
                return slot >= 0 && slot < 5 && Guardians[slot].HasValue;
            }

            public bool TryGetEcho(int id, out BattleUnitInfo info) => Echoes.TryGetValue(id, out info);
            public SkillDef GetSkill(string skillId) => null;
            public float ActorScale => 1f;
            public float Ppu => 205f;
            public float Squash => 0.72f;
            public Vector2 WorldToField(Vector2 world) => new(world.x * 205f, world.y * 205f * 0.72f);
            public float HealEchoRadius => 0.8f;
        }

        struct Played
        {
            public string Key;
            public double Delay;
            public double At;
            public PendingSound Sound;
        }

        sealed class FakeSink : IBattleAudioSink
        {
            public readonly List<Played> Scheduled = new();
            public readonly HashSet<string> Silent = new();
            public readonly Dictionary<int, string> Loops = new();
            public double FxNow { get; set; }
            public double UiNow { get; set; }
            public bool BossMusic;
            public bool BossAlive;
            public StageModifierType Modifier;
            public bool IsAsleep { get; set; }
            public bool IsJinglePlaying { get; set; }
            public double JingleStartedAt => 0;
            public AudioId CurrentJingle => AudioId.None;
            public int Transpose => 0;

            public bool Has(AudioId id) => id.IsValid && !Silent.Contains(AudioCatalog.Keys[id.Index].Key);

            public void Schedule(PendingSound sound, double delay) =>
                Scheduled.Add(new Played { Key = AudioCatalog.Keys[sound.Key].Key, Delay = delay, At = FxNow + delay, Sound = sound });

            public void SetLoop(int slot, AudioId id, float volume, float pitch, float fade) => Loops[slot] = AudioCatalog.Keys[id.Index].Key;
            public void StopLoop(int slot, float fade) => Loops.Remove(slot);
            public void Duck(AudioBus bus, float gainDb, float hold, float attack, float release, int reason, int exempt, double delay) { }
            public void SetBossMusic(bool chapterBossAlive) => BossMusic = chapterBossAlive;
            public void SetBossAlive(bool alive) => BossAlive = alive;
            public void SetModifier(StageModifierType modifier, bool stageChanged) => Modifier = modifier;
            public void FlushBattle() { }
            public void SuppressEmitters(double seconds) { }

            public List<Played> Of(string key)
            {
                var list = new List<Played>();
                foreach (var p in Scheduled)
                {
                    if (p.Key == key) list.Add(p);
                }
                return list;
            }
        }

        FakeState _state;
        FakeSink _sink;
        BattleAudioLogic _logic;
        int _frame = 10;

        [SetUp]
        public void SetUp()
        {
            _state = new FakeState();
            _sink = new FakeSink();
            // random 0.99: 맞는 소리층(50%) · 곁들임(50%) · 금빛(35%)이 나오지 않게 (결정적)
            _logic = new BattleAudioLogic(_state, _sink, () => 0.99f);
            _logic.OnStageChanged(_frame);
            _sink.Scheduled.Clear();
        }

        static BattleEvent Ev(BattleEventType type, int source = -1, int target = -1, Vector2 position = default, float value = 0f, string key = null) =>
            new(type, source, target, position, value, key);

        void Guardian(int slot, string lineage, CombatStyle style = CombatStyle.Defense, string form = null) =>
            _state.Guardians[slot] = new BattleUnitInfo
            {
                LineageId = lineage, FormId = form ?? lineage + "_base", Style = style, Alive = true, Position = BattleGeometry.GuardianPosition(slot),
            };

        void Enemy(int id, Vector2 position, string def = "enemy_dust", bool boss = false, float shield = 0f) =>
            _state.Enemies[id] = new BattleEnemyInfo { Id = id, DefId = def, IsBoss = boss, Alive = true, Position = position, Shield = shield };

        float ExpectedFlight(int slot, string lineage, CombatStyle style, int enemyId, int speed)
        {
            var from = _state.WorldToField(BattleGeometry.GuardianPosition(slot)) + Vector2.up * BattleAudioTiming.MuzzleLift;
            var e = _state.Enemies[enemyId];
            var to = _state.WorldToField(e.Position) + Vector2.up * BattleAudioTiming.EnemyHitLift(e.IsBoss, e.IsSplit, 1f);
            return BattleAudioTiming.GuardianFlight(lineage, style, (to - from).magnitude, speed, 1f);
        }

        void Next(double seconds = 1.0)
        {
            _frame++;
            _sink.FxNow += seconds;
            _sink.UiNow += seconds;
        }

        void Attack(int slot, int target, bool crit = false, bool kill = false, float damage = 10f)
        {
            _logic.OnEvent(Ev(BattleEventType.GuardianAttack, slot, target), _frame);
            if (crit) _logic.OnEvent(Ev(BattleEventType.CriticalHit, slot, target, default, damage), _frame);
            _logic.OnEvent(Ev(BattleEventType.EnemyHit, slot, target, default, damage), _frame);
            if (kill) _logic.OnEvent(Ev(BattleEventType.EnemyKilled, slot, target), _frame);
        }

        [Test]
        public void BasicAttack_LaunchesAtTheMuzzle_AndHitsAtArrival()
        {
            Guardian(0, "jjakjjak");
            Enemy(5, new Vector2(0.3f, 2.0f));
            Attack(0, 5);
            float flight = ExpectedFlight(0, "jjakjjak", CombatStyle.Defense, 5, 1);
            var fire = _sink.Of("fire_needle");
            var hit = _sink.Of("atk_needle");
            Assert.AreEqual(1, fire.Count);
            Assert.AreEqual(1, hit.Count);
            Assert.AreEqual(BattleAudioTiming.GuardianWindup(flight, 1), fire[0].Delay, 1e-5, "총구 불꽃과 같은 순간");
            Assert.AreEqual(flight, hit[0].Delay, 1e-5, "투사체가 닿는 순간");
            Assert.IsTrue(hit[0].Sound.Has(AudioPlayFlags.Hit) && hit[0].Sound.Has(AudioPlayFlags.Predicted) && hit[0].Sound.Has(AudioPlayFlags.Battle));
            Assert.AreEqual(AudioCategory.Hit, hit[0].Sound.Category);
            Assert.AreEqual(BattleAudioTiming.Pan(0.3f), hit[0].Sound.Pan, 1e-5f, "적 자리로 좌우");
        }

        [Test]
        public void Crit_PlaysTheLineageCritAndTheUniversalTing()
        {
            Guardian(0, "ttokdan");
            Enemy(5, new Vector2(0f, 1.8f));
            Attack(0, 5, crit: true);
            float flight = ExpectedFlight(0, "ttokdan", CombatStyle.Defense, 5, 1);
            Assert.AreEqual(0, _sink.Of("atk_button").Count, "치명타면 보통 명중은 없다");
            Assert.AreEqual(1, _sink.Of("atk_button_crit").Count);
            var ting = _sink.Of("hit_crit");
            Assert.AreEqual(1, ting.Count);
            Assert.AreEqual(flight, ting[0].Delay, 1e-5);
            Assert.IsTrue(ting[0].Sound.Has(AudioPlayFlags.MicroDuck), "치명타는 다른 Sfx를 60ms 누른다");
            Assert.IsTrue(_sink.Of("atk_button_crit")[0].Sound.Has(AudioPlayFlags.Kept), "군중 묶음에서도 남는다");
            // 같은 프레임 다른 명중은 치명타가 아니다
            _logic.OnEvent(Ev(BattleEventType.EnemyHit, 0, 5, default, 5f), _frame);
            Assert.AreEqual(1, _sink.Of("hit_crit").Count);
        }

        [Test]
        public void ShieldThatAbsorbsEverything_PlaysBlock_NotCrit()
        {
            Guardian(0, "chalkak");
            Enemy(5, new Vector2(0f, 1.8f), shield: 20f);
            Attack(0, 5, crit: true);
            Assert.AreEqual(1, _sink.Of("shield_block").Count);
            Assert.AreEqual(0, _sink.Of("hit_crit").Count);
            Assert.AreEqual(0, _sink.Of("atk_key").Count + _sink.Of("atk_key_crit").Count);
        }

        [Test]
        public void Kill_SoundsAtTheBloom()
        {
            Guardian(0, "jjakjjak");
            Enemy(5, new Vector2(0f, 2.2f));
            Attack(0, 5, kill: true);
            float flight = ExpectedFlight(0, "jjakjjak", CombatStyle.Defense, 5, 1);
            var death = _sink.Of("death_dust");
            Assert.AreEqual(1, death.Count);
            Assert.AreEqual(BattleAudioTiming.KillPop(BattleAudioTiming.KillHold(flight), flight, false), death[0].Delay, 1e-4);
            Assert.AreEqual(flight + 0.08f, death[0].Delay, 1e-3, "마지막 일격 + 하얀 버팀 0.08");
            Assert.AreEqual(1, _sink.Of("kill_mote").Count, "기억 조각이 문에 닿는 소리");
            Assert.AreEqual(death[0].Delay + BattleAudioTiming.KillMoteDelay, _sink.Of("kill_mote")[0].Delay, 1e-4);
        }

        [Test]
        public void TwoX_UsesFastFlights_AndSkipsEveryOtherLaunch()
        {
            _state.Speed = 2;
            Guardian(0, "mongdang");
            Enemy(5, new Vector2(0f, 2.0f));
            Attack(0, 5);
            float flight = ExpectedFlight(0, "mongdang", CombatStyle.Defense, 5, 2);
            Assert.AreEqual(flight, _sink.Of("atk_candle")[0].Delay, 1e-5);
            Assert.Less(flight, ExpectedFlight(0, "mongdang", CombatStyle.Defense, 5, 1), "2배속 비행 0.6배");
            Next(0.5);
            Attack(0, 5);
            Next(0.5);
            Attack(0, 5);
            Next(0.5);
            Attack(0, 5);
            Assert.AreEqual(2, _sink.Of("fire_candle").Count, "2배속: 지킴이마다 쏘는 소리 하나 걸러");
            Assert.AreEqual(4, _sink.Of("atk_candle").Count, "명중은 모두");
        }

        [Test]
        public void Lullaby_RestsOnTheSeventh_AndOnlyTheFinaleSingsIt()
        {
            Guardian(1, "lalabell");
            Enemy(5, new Vector2(-0.5f, 2.0f));
            for (int i = 0; i < 7; i++)
            {
                Attack(1, 5);
                Next();
            }
            var notes = _sink.Of("atk_musicbox");
            Assert.AreEqual(6, notes.Count, "7번째는 쉼표");
            float[] song = { 1046.5f, 880f, 698.46f, 783.99f, 880f, 783.99f };
            for (int i = 0; i < 6; i++) Assert.AreEqual(song[i], notes[i].Sound.TargetHz, 0.5f, "C6 A5 F5 G5 A5 G5 — " + i);
            Assert.AreEqual(7, _sink.Of("atk_musicbox_ring").Count, "소리 고리는 쉼표에도");
            Assert.AreEqual(0, _sink.Of("atk_musicbox_finale").Count);

            // 피날레 형태: 7번째에 마지막 음 F5
            SetUp();
            Guardian(1, "lalabell", CombatStyle.Snipe, "lala_finale");
            Enemy(5, new Vector2(-0.5f, 2.0f));
            for (int i = 0; i < 7; i++)
            {
                Attack(1, 5);
                Next();
            }
            var finale = _sink.Of("atk_musicbox_finale");
            Assert.AreEqual(1, finale.Count);
            Assert.AreEqual(698.46f, finale[0].Sound.TargetHz, 0.5f, "노래가 드디어 끝난다 (F5)");

            // 치명타는 F 화음이고 순서를 넘기지 않는다
            int before = _logic.LullabyIndex(1);
            Attack(1, 5, crit: true);
            Assert.AreEqual(before, _logic.LullabyIndex(1));
            Assert.AreEqual(1, _sink.Of("atk_musicbox_crit").Count);
        }

        [Test]
        public void LampWave_NotesFollowArrivalOrder()
        {
            Enemy(1, new Vector2(2.0f, 0f));
            Enemy(2, new Vector2(0f, 0.8f));
            Enemy(3, new Vector2(-1.4f, 0f));
            _logic.OnEvent(Ev(BattleEventType.LampLit), _frame);
            _logic.OnEvent(Ev(BattleEventType.EnemyHit, -1, 1, default, 50f), _frame);
            _logic.OnEvent(Ev(BattleEventType.EnemyHit, -1, 2, default, 50f), _frame);
            _logic.OnEvent(Ev(BattleEventType.EnemyHit, -1, 3, default, 50f), _frame);
            _logic.EndFrame(_frame);
            Assert.AreEqual(1, _sink.Of("lamp_lit").Count);
            Assert.AreEqual(LampWave.Start, _sink.Of("lamp_wave")[0].Delay, 1e-6);
            var notes = _sink.Of("hit_lamp");
            Assert.AreEqual(3, notes.Count);
            Assert.AreEqual(BattleAudioTiming.LampArrival(new Vector2(0f, 0.8f)), notes[0].Delay, 1e-5, "가까운 적(0.8)이 먼저");
            Assert.AreEqual(523.25f, notes[0].Sound.TargetHz, 0.5f, "C5");
            Assert.AreEqual(698.46f, notes[1].Sound.TargetHz, 0.5f, "F5");
            Assert.AreEqual(880f, notes[2].Sound.TargetHz, 0.5f, "A5");
            Assert.Less(notes[1].Delay, notes[2].Delay);
            Assert.AreEqual(AudioVoiceClass.High, notes[0].Sound.Class, "불빛 음은 High");

            // 불 밝힌 직후 0.8초는 쏘는 소리 없음
            Guardian(0, "jjakjjak");
            Next(0.3);
            Attack(0, 2);
            Assert.AreEqual(0, _sink.Of("fire_needle").Count);
        }

        [Test]
        public void BossMusic_OnlyForChapterBosses()
        {
            _state.Stage = 9;
            Enemy(9, new Vector2(0f, 2.4f), "boss_shadow", boss: true);
            _logic.OnEvent(Ev(BattleEventType.EnemySpawned, 9, -1, default, 1f), _frame);
            _logic.OnEvent(Ev(BattleEventType.BossSpawned, 9, -1, default, 500f), _frame);
            Assert.AreEqual(1, _sink.Of("boss_appear").Count);
            Assert.AreEqual(BattleAudioLogic.BossJingleDelay, _sink.Of("jingle_boss_appear")[0].Delay, 1e-6);
            Next(1.5);
            _logic.Poll(0.016f, _frame);
            Assert.IsTrue(_sink.BossAlive, "보스 층 환경음");
            Assert.IsFalse(_sink.BossMusic, "x-9 보스는 음악을 바꾸지 않는다");

            SetUp();
            _state.Stage = 10;
            Enemy(9, new Vector2(0f, 2.4f), "boss_mothqueen", boss: true);
            _logic.OnEvent(Ev(BattleEventType.BossSpawned, 9, -1, default, 500f), _frame);
            Next(0.5);
            _logic.Poll(0.016f, _frame);
            Assert.IsFalse(_sink.BossMusic, "1초 뒤에");
            Next(0.6);
            _logic.Poll(0.016f, _frame);
            Assert.IsTrue(_sink.BossMusic, "챕터 보스 (x-10)");
        }

        [Test]
        public void KillChain_ClimbsThePentatonicLadder()
        {
            Guardian(0, "rouge");
            for (int i = 0; i < 3; i++)
            {
                Enemy(20 + i, new Vector2(0.2f * i, 1.6f));
                Attack(0, 20 + i, kill: true);
                Next(0.3);
            }
            var chain = _sink.Of("kill_chain");
            Assert.AreEqual(2, chain.Count, "두 번째 처치부터");
            Assert.AreEqual(698.46f, chain[0].Sound.TargetHz, 0.5f, "F5");
            Assert.AreEqual(783.99f, chain[1].Sound.TargetHz, 0.5f, "G5");
            Assert.AreEqual(_sink.Of("death_dust")[1].Delay, chain[0].Delay, 1e-6, "처치 꽃과 같은 순간");
        }

        [Test]
        public void EnemyStrikes_OnEchoes_FollowEachEnemysRhythm()
        {
            Enemy(5, new Vector2(1f, 1f), "enemy_moth");
            _state.Echoes[1000] = new BattleUnitInfo { LineageId = "rouge", Style = CombatStyle.Assault, Alive = true, Position = new Vector2(1f, 0.8f) };
            _logic.OnEvent(Ev(BattleEventType.EnemyStrike, 5, 1000, new Vector2(1f, 0.9f), 3f, "enemy_moth"), _frame);
            Next(0.3);
            _logic.OnEvent(Ev(BattleEventType.EnemyStrike, 5, 1000, new Vector2(1f, 0.9f), 3f, "enemy_moth"), _frame);
            Next(0.35);
            _logic.OnEvent(Ev(BattleEventType.EnemyStrike, 5, 1000, new Vector2(1f, 0.9f), 3f, "enemy_moth"), _frame);
            Assert.AreEqual(2, _sink.Of("enemy_flutter").Count, "좀나방은 0.6초마다");
            Assert.AreEqual(2, _sink.Of("echo_hurt").Count);
        }

        [Test]
        public void ShopStrikes_PickTheRightLayer()
        {
            Enemy(5, new Vector2(0f, 0.4f), "enemy_rust");
            _logic.OnEvent(Ev(BattleEventType.EnemyStrike, 5, -1, new Vector2(0f, 0.32f), 6f, "enemy_rust"), _frame);
            _logic.OnEvent(Ev(BattleEventType.ShopDamaged, 5, -1, new Vector2(0f, 0.4f), 6f), _frame);
            Assert.AreEqual(1, _sink.Of("enemy_splat").Count, "ShopDamaged는 같은 공격을 두 번 내지 않는다");
            Assert.AreEqual(1, _sink.Of("shop_hit").Count);
            Assert.IsTrue(_sink.Of("shop_hit")[0].Sound.Has(AudioPlayFlags.BypassBudget), "수선점 피격은 예산에 걸리지 않는다");

            Next();
            _state.ShopShield = 50f;
            _logic.OnEvent(Ev(BattleEventType.EnemyStrike, 5, -1, new Vector2(0f, 0.32f), 6f, "enemy_rust"), _frame);
            Assert.AreEqual(1, _sink.Of("shop_shield_hit").Count, "보호막이 다 받으면 '부웅'");

            Next();
            _state.ShopShield = 0f;
            Enemy(9, new Vector2(0f, 0.4f), "boss_shadow", boss: true);
            _logic.OnEvent(Ev(BattleEventType.EnemyStrike, 9, -1, new Vector2(0f, 0.32f), 20f, "boss_shadow"), _frame);
            Assert.AreEqual(1, _sink.Of("shop_hit_boss").Count);
            Assert.AreEqual(1, _sink.Of("boss_siege_hit").Count);
            Assert.AreEqual(1, _sink.Of("boss_wave").Count);
        }

        [Test]
        public void BossKill_ThenClear_PlaysBossDefeated_NotStageClear()
        {
            _state.Stage = 5;
            Guardian(0, "jjakjjak");
            Enemy(9, new Vector2(0f, 1.5f), "boss_shadow", boss: true);
            Attack(0, 9);
            _logic.OnEvent(Ev(BattleEventType.EnemyKilled, 0, 9, default, 1f), _frame);
            Assert.AreEqual(1, _sink.Of("boss_kill_impact").Count);
            Assert.AreEqual(1, _sink.Of("boss_defeat").Count, "보스 처치는 SPEC4 이름 (= boss_death 클립)");
            float pop = (float)_sink.Of("boss_defeat")[0].Delay;
            _logic.OnStageEnded(BattleOutcome.Cleared);
            Assert.AreEqual(0, _sink.Of("jingle_stage_clear").Count, "보스 처치 징글이 곧 울리면 클리어 징글은 쉰다");
            Next(pop + 0.31);
            _logic.Poll(0.016f, _frame);
            Assert.AreEqual(1, _sink.Of("jingle_boss_defeated").Count);
        }

        /// <summary>
        /// 보스 처치 소리는 SPEC4 §3.5 이름 boss_defeat 하나로만 울린다 (옛 키 boss_death는 따로 울리지 않는다). 별칭은 boss_death와 같은 버스 · 등급 ·
        /// 크기 · 게이트라 소리는 그대로이고, 화면 쪽이 같은 키를 몇 프레임 앞뒤로 한 번 더 불러도 자기 게이트(1목소리 · 0.5초)가 삼킨다 (RM-2 AUD 리뷰)
        /// </summary>
        [Test]
        public void BossKill_PlaysTheSpec4Name_AndItsGateSwallowsAStraySecondCall()
        {
            Guardian(0, "jjakjjak");
            Enemy(9, new Vector2(0f, 1.5f), "boss_shadow", boss: true);
            Attack(0, 9);
            _logic.OnEvent(Ev(BattleEventType.EnemyKilled, 0, 9, default, 1f), _frame);
            Assert.AreEqual(1, _sink.Of("boss_defeat").Count);
            Assert.AreEqual(0, _sink.Of("boss_death").Count, "옛 키와 별칭이 겹쳐 울리지 않는다");

            var id = AudioCatalog.Find("boss_defeat");
            var def = AudioCatalog.Get(id);
            var old = AudioCatalog.Get(AudioCatalog.ClipSource(id));
            Assert.AreEqual("boss_death", old.Key, "클립은 boss_death 것");
            Assert.AreEqual(old.Bus, def.Bus);
            Assert.AreEqual(old.Class, def.Class);
            Assert.AreEqual(old.UnityPriority, def.UnityPriority);
            Assert.AreEqual(old.TargetLufs, def.TargetLufs);
            Assert.AreEqual(old.MinInterval, def.MinInterval, 1e-6f);
            Assert.AreEqual(old.MaxVoices, def.MaxVoices);
            Assert.AreEqual(1, def.MaxVoices, "보스 처치는 한 번에 하나");

            var gate = new AudioGate(AudioCatalog.Count);
            double popAt = 100.0 + _sink.Of("boss_defeat")[0].Delay;
            Assert.IsTrue(gate.TryPass(id.Index, def.MinInterval, def.MaxVoices, popAt, def.Length), "감독의 한 번");
            Assert.IsFalse(gate.TryPass(id.Index, def.MinInterval, def.MaxVoices, popAt - 0.05, def.Length), "몇 프레임 앞서 부른 한 번 더");
            Assert.IsFalse(gate.TryPass(id.Index, def.MinInterval, def.MaxVoices, popAt + 0.1, def.Length), "몇 프레임 뒤에 부른 한 번 더");
        }

        [Test]
        public void ChapterFirstClear_PlaysTheChapterFanfare()
        {
            _state.Stage = 10;
            _state.HighestStage = 10;
            _logic.OnStageChanged(++_frame);
            _sink.Scheduled.Clear();
            Guardian(0, "jjakjjak");
            Enemy(9, new Vector2(0f, 1.5f), "boss_mothqueen", boss: true);
            Attack(0, 9);
            _logic.OnEvent(Ev(BattleEventType.EnemyKilled, 0, 9, default, 1f), _frame);
            _state.HighestStage = 11;
            _logic.OnStageEnded(BattleOutcome.Cleared);
            Next(2.0);
            _logic.Poll(0.016f, _frame);
            Assert.AreEqual(1, _sink.Of("jingle_chapter_clear").Count);
            Assert.AreEqual(0, _sink.Of("jingle_boss_defeated").Count, "챕터 징글이 그 자리를 대신한다");
        }

        [Test]
        public void StageFlow_JinglesAndStart()
        {
            _logic.OnStageChanged(++_frame);
            Assert.AreEqual(1, _sink.Of("battle_stage_start").Count);
            _logic.OnStageEnded(BattleOutcome.Failed);
            Assert.AreEqual(1, _sink.Of("jingle_stage_fail").Count);
            _logic.OnStepBack();
            Assert.AreEqual(BattleAudioLogic.StepBackDelay, _sink.Of("battle_step_back")[0].Delay, 1e-6);
            _logic.OnStageChanged(++_frame);
            _logic.OnStageEnded(BattleOutcome.Cleared);
            Assert.AreEqual(1, _sink.Of("jingle_stage_clear").Count);
        }

        [Test]
        public void Sleep_MutesLaunches()
        {
            _sink.IsAsleep = true;
            Guardian(0, "udukeoni");
            Enemy(5, new Vector2(0f, 2f));
            Attack(0, 5);
            Assert.AreEqual(0, _sink.Of("fire_rain").Count, "잠: 쏘는 소리 없음");
            Assert.AreEqual(1, _sink.Of("atk_rain").Count, "명중은 그대로 (덕 −8 dB는 엔진이)");
        }

        [Test]
        public void LampGauge_TicksUpTheGaugeLadder()
        {
            _state.LampGauge = 0.3f;
            _logic.Poll(0.016f, ++_frame);
            _state.LampGauge = 0.8f;
            _logic.Poll(0.016f, ++_frame);
            var ticks = _sink.Of("lamp_tick");
            Assert.AreEqual(3, ticks.Count);
            Assert.AreEqual(698.46f, ticks[0].Sound.TargetHz, 0.5f, "25% F5");
            Assert.AreEqual(880f, ticks[1].Sound.TargetHz, 0.5f, "50% A5");
            Assert.AreEqual(1046.5f, ticks[2].Sound.TargetHz, 0.5f, "75% C6");
            Assert.Greater(ticks[2].Delay, ticks[1].Delay, "한 프레임에 두 칸이면 차례로");
        }

        [Test]
        public void ShopLowHp_StartsTheHeartbeat_AndStopsWhenSafe()
        {
            _state.ShopHp = 25f;
            _logic.Poll(0.016f, ++_frame);
            Assert.AreEqual(1, _sink.Of("shop_low_enter").Count);
            Assert.AreEqual("shop_heartbeat", _sink.Loops[0]);
            _state.ShopHp = 40f;
            _logic.Poll(0.016f, ++_frame);
            Assert.IsFalse(_sink.Loops.ContainsKey(0), "35% 넘으면 멈춤");
        }

        [Test]
        public void MissingClips_AreSilent()
        {
            _sink.Silent.Add("atk_needle");
            _sink.Silent.Add("fire_needle");
            Guardian(0, "jjakjjak");
            Enemy(5, new Vector2(0f, 2f));
            Attack(0, 5);
            Assert.AreEqual(0, _sink.Of("atk_needle").Count + _sink.Of("fire_needle").Count, "클립이 없는 키는 예약하지 않는다");
        }
    }
}
