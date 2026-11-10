using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-01 권장 전투력 보정. 헤드리스 BattleSimulator로 시작 편성(시작 친구 기본형, 추천 역할대로)을 배율 k로 키우며
    /// 스테이지마다 5개 시드 중 4번(80%) 이상 이기는 가장 작은 전투력을 찾고, 첫 챕터 보스(1-10)부터의 표본으로
    /// 세 구간 rec(s)를 한꺼번에 맞춘다 (SPEC4 §7.1 · SPEC5 §4.3 · S-4):
    ///   ln rec(s) = ln a + ln g · min(s, T) − 1 … 걸음 s→s+1의 증가율이 s &lt; T면 g, s &lt; T2면 gL, 그 뒤 gL2 (BattleConfig.TaperedScale · PowerSystem과 같은 걸음 규칙).
    ///   T = MetaContent.RecommendedTaperStage (30, 적 첫 완만해짐) · T2 = MetaContent.RecommendedTaperStage2 (151, 두 번째 완만해짐) — 이어진 곡선(연속).
    ///   · 전투력 = 공격 + min(수선점 최대 내구도 × 0.3, 공격 × 수선점 몫 상한), 공격 = Σ지킴이 × 1.0 + Σ덱 × 0.6 (PowerSystem과 같다 — P4-1 S5-24),
    ///     유닛 = 공격/간격 × 10 + 체력. k는 공격·체력·수선점 내구도에 똑같이 곱하므로 전투력은 k에 정비례한다.
    ///   · 시너지·치명타는 넣지 않는다 (편성 시너지는 실제 전투력과 실제 강함을 함께 올리므로 비율은 크게 변하지 않는다).
    ///   · 던전 (SPEC4 §7.1 #2): 시작 덱만(지킴이 없음) 1~30층, 제한 시간 안에 이기는 가장 작은 덱 전투력(EchoPower, 가중치 전)으로
    ///     rec(f) = a × g^(f−1)을 맞추고, 필요 / 맞춤의 90번째 백분위수로 위쪽을 감싼다 (스테이지의 RecommendedEnvelopeScale과 같은 뜻).
    ///   · 결과: MetaContent.Power.cs의 Calibrated* 상수 줄만 고쳐 쓰고(코드가 원본, BOM 없이 · 줄바꿈 그대로),
    ///     MetaDatabase 에셋의 power도 바로 갱신한다 (권장 base에는 감싸기 배율을 곱한다). 표는 Logs/PowerCalibration.csv · Logs/PowerCalibrationDungeon.csv.
    /// 메뉴 "IdleGame/전투력 보정"(스테이지 + 던전), 배치: -executeMethod IdleGame.EditorTools.PowerCalibration.RunBatch.
    /// 던전만 다시 맞추기 (SPEC4 §7.1 #2, 스테이지 권장값은 그대로): 메뉴 "IdleGame/던전 전투력 보정", 배치 …PowerCalibration.RunDungeonBatch
    /// 담당: M2 · P4-8. (BattleSimulator·BattleConfig는 읽기만 한다)
    /// </summary>
    public static class PowerCalibration
    {
        const int Seeds = 5;
        const int RequiredWins = 4;
        const float TickSeconds = 0.5f;
        const float MaxBattleSeconds = 600f;
        const int BisectSteps = 6;
        const double SearchFactor = 1.6;
        const double MinScale = 0.02;
        const double MaxScale = 1e10;

        /// <summary>
        /// 표본 스테이지: 1-1 ~ 6-10 전부, 61~150은 3스테이지마다, 151~400은 10스테이지마다 (셋째 구간을 맞추려고 151 뒤로 늘렸다)
        /// </summary>
        static readonly int[] SampleStages = Enumerable.Range(1, 60)
            .Concat(Enumerable.Range(0, 30).Select(i => 63 + 3 * i))
            .Concat(Enumerable.Range(0, 26).Select(i => 151 + 10 * i))
            .Where(s => s <= 400).Distinct().OrderBy(s => s).ToArray();

        /// <summary>던전 표본 층 (1~30)</summary>
        static readonly int[] SampleFloors = Enumerable.Range(1, 30).ToArray();

        /// <summary>
        /// 이 스테이지부터 맞춘다. 1챕터는 시작 편성이 늘 넉넉히 이겨서(필요 전투력이 거의 평평) 지수로 맞추면
        /// 중후반 벽에서 권장값이 크게 낮아진다 — 평가가 의미 있는 구간(첫 챕터 보스부터)에 맞춘다.
        /// 앞쪽 스테이지는 권장값이 실제 필요보다 높게 나오지만 시작 편성은 그래도 "여유"다.
        /// </summary>
        const int FitFromStage = 10;

        static readonly string PowerSourcePath = IdleProjectSetup.Root + "/Editor/Content/MetaContent.Power.cs";
        const string ReportPath = "Logs/PowerCalibration.csv";
        const string DungeonReportPath = "Logs/PowerCalibrationDungeon.csv";

        [MenuItem("IdleGame/전투력 보정", priority = 40)]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("전투력 보정",
                    $"표본 스테이지 {SampleStages.Length}개 + 던전 {SampleFloors.Length}층 × 시드 {Seeds}개로 헤드리스 전투를 돌려 권장 전투력을 맞춥니다.\n" +
                    "몇 분 걸릴 수 있어요. 끝나면 MetaContent.Power.cs와 MetaDatabase가 갱신됩니다.", "시작", "취소")) return;
            Calibrate(true, true);
        }

        /// <summary>배치 모드 진입점 (확인창·진행 막대 없음)</summary>
        public static void RunBatch() => Calibrate(false, true);

        /// <summary>던전만 다시 맞춘다 (SPEC4 §7.1 #2): 스테이지 표본은 돌리지 않고, 원본의 던전 두 줄과 에셋의 던전 권장만 고친다</summary>
        [MenuItem("IdleGame/던전 전투력 보정", priority = 42)]
        public static void RunDungeon()
        {
            if (!EditorUtility.DisplayDialog("던전 전투력 보정",
                    $"던전 {SampleFloors.Length}층 × 시드 {Seeds}개로 시작 덱만 헤드리스 전투를 돌려 던전 권장 전투력을 맞춰요.\n" +
                    "스테이지 권장값은 그대로 두고 MetaContent.Power.cs의 던전 두 줄과 MetaDatabase의 던전 권장만 고쳐요.", "시작", "취소")) return;
            Calibrate(true, false);
        }

        /// <summary>배치 모드 진입점 — 던전만 (확인창·진행 막대 없음)</summary>
        public static void RunDungeonBatch() => Calibrate(false, false);

        sealed class Sample
        {
            public int Stage;
            public double Scale;
            public double Power;
        }

        /// <summary>세 구간 맞춤 결과 (a = 1스테이지 권장, g / gL / gL2 = 구간 증가율)</summary>
        struct Fit3
        {
            public double A, G, GLate, GLate2;
            public double At(int s) => A * Math.Pow(G, Math.Min(s, Taper) - 1)
                                         * (s > Taper ? Math.Pow(GLate, Math.Min(s, Taper2) - Taper) : 1.0)
                                         * (s > Taper2 ? Math.Pow(GLate2, s - Taper2) : 1.0);
        }

        static int Taper => MetaContent.RecommendedTaperStage;
        static int Taper2 => MetaContent.RecommendedTaperStage2;

        /// <param name="interactive">메뉴에서 (진행 막대 · 취소)</param>
        /// <param name="withStages">false = 던전만 (스테이지 맞춤 · 표 · 원본의 스테이지 줄 · 에셋의 스테이지 권장은 그대로)</param>
        static void Calibrate(bool interactive, bool withStages)
        {
            string title = withStages ? "전투력 보정" : "던전 전투력 보정";
            int stageCount = withStages ? SampleStages.Length : 0;
            int total = stageCount + SampleFloors.Length;
            var battle = LoadFirst<BattleConfig>();
            var characters = LoadFirst<CharacterDatabase>();
            var config = LoadFirst<GameConfig>();
            if (battle == null || characters == null || config == null)
            {
                Debug.LogError("[전투력 보정] BattleConfig / CharacterDatabase / GameConfig 에셋이 없습니다. 먼저 콘텐츠를 만드세요.");
                return;
            }
            var weights = new PowerConfig { shopShareCap = MetaContent.ShopShareCap };
            var team = BuildStarterTeam(characters, config);
            if (team.guardians.Count == 0 && team.echoes.Count == 0)
            {
                Debug.LogError("[전투력 보정] 시작 친구로 편성을 만들 수 없습니다 (GameConfig.starterLineages).");
                return;
            }
            double unitPower = TeamPower(team, 1.0, weights);
            var started = DateTime.Now;

            var samples = new List<Sample>();
            var floors = new List<Sample>();
            try
            {
                double scale = 0.5;
                for (int i = 0; i < stageCount; i++)
                {
                    int stage = SampleStages[i];
                    if (interactive && EditorUtility.DisplayCancelableProgressBar(title,
                            $"스테이지 {StageText(stage)} ({i + 1}/{SampleStages.Length})", (float)i / total))
                    {
                        Debug.LogWarning("[전투력 보정] 취소했습니다. 아무것도 바꾸지 않았어요.");
                        return;
                    }
                    double found = FindMinScale(s => Wins(battle, team, stage, s), Math.Max(MinScale, scale));
                    if (double.IsNaN(found))
                    {
                        Debug.LogWarning($"[전투력 보정] 스테이지 {StageText(stage)}: 배율 {MaxScale:0}까지 이길 수 없어 건너뜀");
                        continue;
                    }
                    scale = found;
                    samples.Add(new Sample { Stage = stage, Scale = found, Power = unitPower * found });
                    if (!interactive && i % 10 == 0) Debug.Log($"[전투력 보정] 스테이지 {StageText(stage)} 필요 {unitPower * found:0} ({(DateTime.Now - started).TotalSeconds:0}초)");
                }

                double deckUnit = DeckPower(team, 1.0);
                if (deckUnit > 0)
                {
                    double scale2 = 0.5;
                    for (int i = 0; i < SampleFloors.Length; i++)
                    {
                        int floor = SampleFloors[i];
                        if (interactive && EditorUtility.DisplayCancelableProgressBar(title,
                                $"던전 {floor}층 ({i + 1}/{SampleFloors.Length})", (float)(stageCount + i) / total))
                        {
                            Debug.LogWarning("[전투력 보정] 취소했습니다. 아무것도 바꾸지 않았어요.");
                            return;
                        }
                        double found = FindMinScale(s => WinsDungeon(battle, team, floor, s), Math.Max(MinScale, scale2));
                        if (double.IsNaN(found))
                        {
                            Debug.LogWarning($"[전투력 보정] 던전 {floor}층: 배율 {MaxScale:0}까지 이길 수 없어 건너뜀");
                            continue;
                        }
                        scale2 = found;
                        floors.Add(new Sample { Stage = floor, Scale = found, Power = deckUnit * found });
                    }
                }
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
            }

            if (withStages && samples.Count < 6)
            {
                Debug.LogError("[전투력 보정] 표본이 6개보다 적어 맞출 수 없습니다.");
                return;
            }
            if (!withStages && floors.Count < 3)
            {
                Debug.LogError($"[전투력 보정] 던전 표본이 {floors.Count}층뿐이라 맞출 수 없어요 (3층 이상). 아무것도 바꾸지 않았어요.");
                return;
            }

            Fit3? stageFit = null;
            if (withStages)
            {
                var three = FitThree(samples);
                three.A = RoundSignificant(three.A, 3);
                three.G = Math.Round(three.G, 4);
                three.GLate = Math.Round(three.GLate, 4);
                three.GLate2 = Math.Round(three.GLate2, 4);
                stageFit = three;
            }

            double dungeonBase = 0, dungeonGrowth = 0;
            if (floors.Count >= 3)
            {
                FitOne(floors, 1, out dungeonBase, out dungeonGrowth);
                // 위쪽 감싸기: 필요 / 맞춤의 90번째 백분위수 (1 미만이면 1)
                var ratios = floors.Select(f => f.Power / (dungeonBase * Math.Pow(dungeonGrowth, f.Stage - 1))).OrderBy(x => x).ToList();
                double envelope = Math.Max(1.0, ratios[Math.Min(ratios.Count - 1, (int)Math.Ceiling(0.9 * ratios.Count) - 1)]);
                dungeonBase = RoundSignificant(dungeonBase * envelope, 3);
                dungeonGrowth = Math.Round(dungeonGrowth, 4);
            }

            if (stageFit.HasValue) WriteReport(samples, stageFit.Value);
            WriteDungeonReport(floors, dungeonBase, dungeonGrowth);
            WriteSource(stageFit, dungeonBase, dungeonGrowth);
            UpdateAsset(stageFit, dungeonBase, dungeonGrowth);
            if (interactive) AssetDatabase.ImportAsset(PowerSourcePath);
            if (stageFit.HasValue)
            {
                var fit = stageFit.Value;
                Debug.Log($"[전투력 보정] 권장 전투력 rec(s) = {fit.A:0.###} × {fit.G:0.####}^(s−1) → {Taper}스테이지 뒤 {fit.GLate:0.####} → {Taper2}스테이지 뒤 {fit.GLate2:0.####} " +
                          $"(표본 {samples.Count}개, 시작 편성 전투력 {unitPower:0}) · 던전 rec(f) = {dungeonBase:0.###} × {dungeonGrowth:0.####}^(f−1) (표본 {floors.Count}층) " +
                          $"· {(DateTime.Now - started).TotalMinutes:0.0}분 → {PowerSourcePath}, {ReportPath}, {DungeonReportPath}");
                LogAcceptance(samples, fit);
            }
            else
            {
                Debug.Log($"[전투력 보정] 던전만: rec(f) = {dungeonBase:0.###} × {dungeonGrowth:0.####}^(f−1) (표본 {floors.Count}층, 시작 덱 전투력 {DeckPower(team, 1.0):0}) " +
                          $"· {(DateTime.Now - started).TotalMinutes:0.0}분 → {PowerSourcePath} (던전 두 줄), {DungeonReportPath}");
            }
        }

        /// <summary>SPEC4 §7.1 받아들임: 6-1 … 10-10의 필요 / 권장(감싸기 포함)이 0.80~1.05 안인지 로그로 남긴다</summary>
        static void LogAcceptance(List<Sample> samples, Fit3 fit)
        {
            double envelope = MetaContent.RecommendedEnvelopeScale;
            var band = samples.Where(s => s.Stage >= 51 && s.Stage <= 100).Select(s => s.Power / (fit.At(s.Stage) * envelope)).ToList();
            if (band.Count == 0) return;
            int inside = band.Count(x => x >= 0.80 && x <= 1.05);
            Debug.Log($"[전투력 보정] 6-1~10-10 필요/권장 {band.Min():0.00}~{band.Max():0.00} (0.80~1.05 안 {inside}/{band.Count})");
        }

        // ───────────── 편성 ─────────────

        struct Team
        {
            public List<UnitSetup> guardians;
            public List<UnitSetup> echoes;
        }

        /// <summary>시작 친구의 기본형을 추천 역할대로 편성 (FormationSystem.AutoFormation 1단계와 같은 규칙)</summary>
        static Team BuildStarterTeam(CharacterDatabase characters, GameConfig config)
        {
            var team = new Team { guardians = new List<UnitSetup>(), echoes = new List<UnitSetup>() };
            foreach (var lineageId in config.starterLineages ?? Array.Empty<string>())
            {
                var lineage = characters.GetLineage(lineageId);
                var form = lineage != null ? characters.GetForm(lineage.baseFormId) : null;
                if (form == null) continue;
                var unit = new UnitSetup
                {
                    FormId = form.id,
                    LineageId = lineage.id,
                    Stats = BaseStats(form),
                    Skill = form.skill != null && !form.skill.IsEmpty ? form.skill : null,
                };
                if (lineage.traits != null) unit.Traits.AddRange(lineage.traits);
                if (form.recommendedRole == CharacterRole.Guardian && team.guardians.Count < FormationSystem.GuardianSlotCount) team.guardians.Add(unit);
                else if (team.echoes.Count < FormationSystem.EchoSlotCount) team.echoes.Add(unit);
            }
            // 높은 전투력부터 위쪽 방위 (AutoFormation과 비슷하게)
            team.guardians.Sort((x, y) => y.Stats.Power.CompareTo(x.Stats.Power));
            return team;
        }

        /// <summary>레벨 1, 공명·의상 없음 능력치 (CharacterSystem.GetStats와 같은 필드)</summary>
        static CharacterStats BaseStats(CharacterFormDef form) => new()
        {
            style = form.style,
            attack = form.attack,
            hp = form.hp,
            attackInterval = form.attackInterval,
            range = form.range,
            arcDegrees = form.arcDegrees,
            moveSpeed = form.moveSpeed,
            summonCost = form.summonCost,
            lifetime = form.lifetime,
        };

        static readonly int[] GuardianSlotOrder = { 0, 1, 4, 2, 3 };

        static BattleSetup MakeSetup(BattleConfig battle, Team team, int stage, double scale)
        {
            var setup = new BattleSetup
            {
                Mode = BattleMode.Stage,
                Level = stage,
                ShopMaxHp = (float)(GrowthSystem.CalcShopMaxHp(1) * scale),
                AttackMultiplier = 1f,
                Modifiers = new BattleModifiers(),
                StageModifier = battle.GetStageModifier(stage),
            };
            for (int i = 0; i < team.guardians.Count && i < GuardianSlotOrder.Length && GuardianSlotOrder[i] < setup.Guardians.Length; i++)
                setup.Guardians[GuardianSlotOrder[i]] = Scaled(team.guardians[i], scale);
            foreach (var echo in team.echoes) setup.Deck.Add(Scaled(echo, scale));
            return setup;
        }

        /// <summary>던전 (DungeonSystem.CreateSetup과 같은 모양: 덱만, 기믹 없음). 수선점 내구도도 k배 (팀 전체가 k배 강하다는 가정)</summary>
        static BattleSetup MakeDungeonSetup(Team team, int floor, double scale)
        {
            var setup = new BattleSetup
            {
                Mode = BattleMode.EchoDungeon,
                Level = Math.Max(1, floor),
                ShopMaxHp = (float)(GrowthSystem.CalcShopMaxHp(1) * scale),
                AttackMultiplier = 1f,
                Modifiers = new BattleModifiers(),
                StageModifier = StageModifierType.None,
            };
            foreach (var echo in team.echoes) setup.Deck.Add(Scaled(echo, scale));
            return setup;
        }

        static UnitSetup Scaled(UnitSetup unit, double scale)
        {
            var stats = unit.Stats;
            stats.attack = (float)(stats.attack * scale);
            stats.hp = (float)(stats.hp * scale);
            var copy = new UnitSetup { FormId = unit.FormId, LineageId = unit.LineageId, Stats = stats, Skill = unit.Skill };
            copy.Traits.AddRange(unit.Traits);
            return copy;
        }

        /// <summary>PowerSystem.TeamPower와 같은 식: 공격 + min(수선점 몫, 공격 × 상한) (상한 ≤ 0 = 상한 없음)</summary>
        static double TeamPower(Team team, double scale, PowerConfig weights)
        {
            double guardians = team.guardians.Sum(u => (double)u.Stats.Power);
            double echoes = team.echoes.Sum(u => (double)u.Stats.Power);
            double offense = scale * (guardians * weights.guardianWeight + echoes * weights.echoWeight);
            double shop = scale * GrowthSystem.CalcShopMaxHp(1) * weights.shopHpWeight;
            return offense + (weights.shopShareCap > 0 ? Math.Min(shop, offense * weights.shopShareCap) : shop);
        }

        /// <summary>던전 평가에 쓰는 덱 전투력 (PowerSystem.EchoPower — 가중치 전 유닛 합)</summary>
        static double DeckPower(Team team, double scale) => scale * team.echoes.Sum(u => (double)u.Stats.Power);

        // ───────────── 탐색 ─────────────

        /// <summary>80% 이상 이기는 가장 작은 배율 (못 찾으면 NaN). guess에서 시작해 늘리거나 줄인 뒤 로그 이분 탐색</summary>
        static double FindMinScale(Func<double, bool> wins, double guess)
        {
            double lo, hi;
            if (wins(guess))
            {
                hi = guess;
                lo = guess / SearchFactor;
                while (lo > MinScale && wins(lo))
                {
                    hi = lo;
                    lo /= SearchFactor;
                }
                if (lo <= MinScale) return hi;
            }
            else
            {
                lo = guess;
                hi = guess * SearchFactor;
                while (!wins(hi))
                {
                    lo = hi;
                    hi *= SearchFactor;
                    if (hi > MaxScale) return double.NaN;
                }
            }
            for (int i = 0; i < BisectSteps; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                if (wins(mid)) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        /// <summary>시드 Seeds개 중 RequiredWins번 이상 클리어하는지 (결과가 정해지면 일찍 멈춤)</summary>
        static bool Wins(BattleConfig battle, Team team, int stage, double scale) =>
            WinsWith(battle, seed => 7919 * stage + seed, () => MakeSetup(battle, team, stage, scale), MaxBattleSeconds);

        /// <summary>던전: 제한 시간(BattleConfig.dungeonTimeLimit) 안에 클리어 (DungeonSystem.Challenge와 같은 판정)</summary>
        static bool WinsDungeon(BattleConfig battle, Team team, int floor, double scale) =>
            WinsWith(battle, seed => unchecked(floor * 7919 + 17 + seed * 104729), () => MakeDungeonSetup(team, floor, scale), Math.Max(1f, battle.dungeonTimeLimit));

        static bool WinsWith(BattleConfig battle, Func<int, int> seedOf, Func<BattleSetup> setup, float limit)
        {
            int wins = 0, losses = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var sim = new BattleSimulator(battle, seedOf(seed)) { AutoMode = true };
                sim.Start(setup());
                float time = 0f;
                while (sim.Outcome == BattleOutcome.Running && time < limit)
                {
                    sim.Tick(TickSeconds);
                    time += TickSeconds;
                }
                if (sim.Outcome == BattleOutcome.Cleared) wins++;
                else losses++;
                if (wins >= RequiredWins) return true;
                if (losses > Seeds - RequiredWins) return false;
            }
            return wins >= RequiredWins;
        }

        // ───────────── 맞춤 / 기록 ─────────────

        /// <summary>
        /// 세 구간 이어진 최소제곱: ln P = c0 + c1·x1 + c2·x2 + c3·x3,
        /// x1 = min(s, T) − 1, x2 = clamp(s − T, 0, T2 − T), x3 = max(0, s − T2) (FitFromStage 이상 표본).
        /// 한 구간에 표본이 2개보다 적으면 그 구간 증가율은 앞 구간 것을 쓴다
        /// </summary>
        static Fit3 FitThree(List<Sample> samples)
        {
            var used = samples.Where(x => x.Stage >= FitFromStage).ToList();
            if (used.Count < 6) used = samples;
            int late = used.Count(s => s.Stage > Taper && s.Stage <= Taper2);
            int late2 = used.Count(s => s.Stage > Taper2);
            int dims = late2 >= 2 ? 4 : late >= 2 ? 3 : 2;

            var ata = new double[dims, dims];
            var atb = new double[dims];
            foreach (var s in used)
            {
                var x = new double[4];
                x[0] = 1.0;
                x[1] = Math.Min(s.Stage, Taper) - 1;
                x[2] = Math.Max(0, Math.Min(s.Stage, Taper2) - Taper);
                x[3] = Math.Max(0, s.Stage - Taper2);
                // 구간이 빠지면 그 열을 앞 구간에 합친다 (같은 증가율)
                if (dims < 4) x[2] += x[3];
                if (dims < 3) x[1] += x[2];
                double y = Math.Log(s.Power);
                for (int i = 0; i < dims; i++)
                {
                    atb[i] += x[i] * y;
                    for (int j = 0; j < dims; j++) ata[i, j] += x[i] * x[j];
                }
            }
            var c = Solve(ata, atb, dims);
            var fit = new Fit3
            {
                A = Math.Exp(c[0]),
                G = Math.Max(1.0, Math.Exp(c[1])),
            };
            fit.GLate = dims >= 3 ? Math.Max(1.0, Math.Exp(c[2])) : fit.G;
            fit.GLate2 = dims >= 4 ? Math.Max(1.0, Math.Exp(c[3])) : fit.GLate;
            return fit;
        }

        /// <summary>가우스 소거 (작은 정규 방정식)</summary>
        static double[] Solve(double[,] a, double[] b, int n)
        {
            var m = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) m[i, j] = a[i, j];
                m[i, n] = b[i];
            }
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col])) pivot = row;
                }
                for (int j = 0; j <= n; j++) (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
                double div = Math.Abs(m[col, col]) < 1e-12 ? 1e-12 : m[col, col];
                for (int j = col; j <= n; j++) m[col, j] /= div;
                for (int row = 0; row < n; row++)
                {
                    if (row == col) continue;
                    double factor = m[row, col];
                    for (int j = col; j <= n; j++) m[row, j] -= factor * m[col, j];
                }
            }
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = m[i, n];
            return x;
        }

        /// <summary>ln P = ln a + (s−1) ln g 최소제곱 (from 이상 표본)</summary>
        static void FitOne(List<Sample> samples, int from, out double a, out double g)
        {
            var used = samples.Where(x => x.Stage >= from).ToList();
            if (used.Count < 3) used = samples;
            double n = used.Count, sx = 0, sy = 0, sxx = 0, sxy = 0;
            foreach (var s in used)
            {
                double x = s.Stage - 1, y = Math.Log(s.Power);
                sx += x;
                sy += y;
                sxx += x * x;
                sxy += x * y;
            }
            double slope = (n * sxy - sx * sy) / Math.Max(1e-9, n * sxx - sx * sx);
            double intercept = (sy - slope * sx) / n;
            a = Math.Exp(intercept);
            g = Math.Max(1.0, Math.Exp(slope));
        }

        static void WriteReport(List<Sample> samples, Fit3 fit)
        {
            double envelope = MetaContent.RecommendedEnvelopeScale;
            var sb = new StringBuilder("stage,label,scale,min_power,fit_power,fit_ratio,rec_power,need_over_rec\n");
            foreach (var s in samples)
            {
                double value = fit.At(s.Stage);
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.####},{3:0},{4:0},{5:0.###},{6:0},{7:0.###}\n",
                    s.Stage, StageText(s.Stage), s.Scale, s.Power, value, s.Power / value, value * envelope, s.Power / (value * envelope)));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(true));
        }

        static void WriteDungeonReport(List<Sample> floors, double a, double g)
        {
            var sb = new StringBuilder("floor,scale,min_echo_power,rec_power,need_over_rec\n");
            foreach (var f in floors)
            {
                double rec = a > 0 ? a * Math.Pow(Math.Max(1.0, g), f.Stage - 1) : 0;
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.####},{2:0},{3:0},{4:0.###}\n",
                    f.Stage, f.Scale, f.Power, rec, rec > 0 ? f.Power / rec : 0));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(DungeonReportPath)!);
            File.WriteAllText(DungeonReportPath, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// MetaContent.Power.cs의 보정 상수 줄만 바꾼다 (형식: internal const double CalibratedX = 숫자;).
        /// 원본의 인코딩(BOM 없음)과 줄바꿈은 그대로 둔다 (바이트로 읽고 쓴다)
        /// </summary>
        static void WriteSource(Fit3? stageFit, double dungeonBase, double dungeonGrowth)
        {
            if (!File.Exists(PowerSourcePath))
            {
                Debug.LogError("[전투력 보정] 원본 파일이 없습니다: " + PowerSourcePath);
                return;
            }
            byte[] raw = File.ReadAllBytes(PowerSourcePath);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
            string updated = text;
            if (stageFit.HasValue)
            {
                var fit = stageFit.Value;
                updated = Replace(updated, "CalibratedRecommendedBase", fit.A.ToString("0.###", CultureInfo.InvariantCulture));
                updated = Replace(updated, "CalibratedRecommendedGrowth", fit.G.ToString("0.####", CultureInfo.InvariantCulture));
                updated = Replace(updated, "CalibratedRecommendedGrowthLate", fit.GLate.ToString("0.####", CultureInfo.InvariantCulture));
                updated = Replace(updated, "CalibratedRecommendedGrowthLate2", fit.GLate2.ToString("0.####", CultureInfo.InvariantCulture));
            }
            updated = Replace(updated, "CalibratedDungeonRecommendedBase", dungeonBase.ToString("0.###", CultureInfo.InvariantCulture));
            updated = Replace(updated, "CalibratedDungeonRecommendedGrowth", dungeonGrowth.ToString("0.####", CultureInfo.InvariantCulture));
            if (updated == text) Debug.LogWarning("[전투력 보정] 원본 상수를 찾지 못했거나 값이 같습니다: " + PowerSourcePath);
            else File.WriteAllBytes(PowerSourcePath, new UTF8Encoding(false).GetBytes(updated));
        }

        static string Replace(string text, string name, string value) =>
            Regex.Replace(text, $@"(internal const double {name} = )[^;]+;", m => m.Groups[1].Value + value + ";");

        /// <summary>에셋 power도 바로 갱신 (MetaContent.FillPower와 같은 값 — 권장 base에 감싸기 배율을 곱한다)</summary>
        static void UpdateAsset(Fit3? stageFit, double dungeonBase, double dungeonGrowth)
        {
            var db = AssetDatabase.LoadAssetAtPath<MetaDatabase>(MetaDataBuilder.MetaPath);
            if (db == null) return;
            db.power ??= new PowerConfig();
            if (stageFit.HasValue)
            {
                var fit = stageFit.Value;
                db.power.recommendedBase = fit.A * MetaContent.RecommendedEnvelopeScale;
                db.power.recommendedGrowth = fit.G;
                db.power.recommendedTaperStage = Taper;
                db.power.recommendedGrowthLate = fit.GLate;
                db.power.recommendedTaperStage2 = Taper2;
                db.power.recommendedGrowthLate2 = fit.GLate2;
                db.power.shopShareCap = MetaContent.ShopShareCap;
            }
            db.power.dungeonRecommendedBase = dungeonBase;
            db.power.dungeonRecommendedGrowth = dungeonGrowth >= 1.0 ? dungeonGrowth : 1.1;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        static double RoundSignificant(double value, int digits)
        {
            if (value <= 0) return value;
            double scale = Math.Pow(10, Math.Floor(Math.Log10(value)) + 1 - digits);
            return Math.Round(value / scale) * scale;
        }

        static string StageText(int stage) => $"{(stage - 1) / 10 + 1}-{(stage - 1) % 10 + 1}";

        static T LoadFirst<T>() where T : UnityEngine.Object
        {
            string guid = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { IdleProjectSetup.Root }).FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }
    }
}
