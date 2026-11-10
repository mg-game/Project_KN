using System;
using System.Globalization;

namespace IdleGame
{
    /// <summary>
    /// 전투력 (PRG-01). 화면에 보이는 "강해진 정도".
    ///   · 유닛 = (공격 × (1 + 공%) / 공격 간격 × (1 + 공속%)) × 10 × (1 + 치확 × 치피) + 체력 × (1 + 체%)
    ///     (CharacterStats + 편성 시너지 BattleModifiers.For(계보), 치확/치피는 % 값을 비율로 바꿔 곱한다)
    ///   · 공격 몫 = Σ지킴이 × guardianWeight + Σ덱 잔향이 × echoWeight (MetaDatabase.power)
    ///   · 팀 = 공격 몫 + min(수선점 최대 내구도 × shopHpWeight, 공격 몫 × shopShareCap), 반올림 정수 (SPEC5 §4.3 S5-24 — 기본 1이면
    ///     수선점 몫은 전체의 절반까지. shopShareCap ≤ 0이면 상한 없음 = 옛 식. 그래서 빈 편성은 0)
    ///   · 권장 전투력 rec(s) = recommendedBase × g^(s−1), 세 구간 (SPEC4 §7.1 #1 + S5-24): recommendedTaperStage T > 0이면 T 뒤로는
    ///     recommendedGrowthLate, recommendedTaperStage2 T2 > 0이면 T2 뒤로는 recommendedGrowthLate2 (BattleConfig.TaperedScale과 같은 걸음 규칙).
    ///     비율 ≥ comfortableRatio 여유 / ≥ challengeRatio 도전 / 그 아래 어려움.
    ///   · 던전 층 권장 = dungeonRecommendedBase × dungeonRecommendedGrowth^(층−1), 잔향이 덱 전투력 EchoPower와 비교 (0 = 줄 없음)
    /// 다시 계산은 Formation.Changed / Characters.Changed / Synergy.Changed / Growth.LevelChanged / Costumes.Changed에서 더러움 표시만 하고,
    /// TeamPower를 읽거나 Refresh(MetaSession.Tick이 프레임마다 부름)할 때 한 번 계산한다 (매 프레임 계산·할당 없음).
    /// 시너지 보정은 SynergySystem.BuildModifiers(StageModifierType.None)를 쓴다 (스테이지 기믹 보정 제외).
    /// <para>
    /// 전투와 같은 규칙을 따른다: 편성 공명 보너스(FormationSystem.TeamAttackBonusPercent)는 모든 유닛 공격력 배율,
    /// 수선점 내구도는 시너지 ShopMaxHpPercent 반영. 치명타는 시너지 몫만 넣는다 (기본 치명타는 모두 같아 비교에 영향이 없다).
    /// 첫 계산은 기준값이라 Changed를 울리지 않는다 (부팅 때 "+12,345"가 뜨지 않게).
    /// </para>
    /// </summary>
    public class PowerSystem
    {
        /// <summary>(이전 팀 전투력, 새 팀 전투력) — 상단 바 "+1,234" 상승 연출(FX-04)용. Refresh에서 값이 바뀌었을 때만</summary>
        public event Action<long, long> Changed;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly CharacterSystem _characters;
        readonly FormationSystem _formation;
        readonly SynergySystem _synergy;
        readonly GrowthSystem _growth;
        readonly CostumeSystem _costumes;

        bool _dirty = true;
        long _team;
        long _guardians;
        long _echoes;
        long _reported;
        bool _hasReported;
        BattleModifiers _modifiers;
        float _attackMultiplier = 1f;

        readonly StatBlock _scratch = new();
        readonly string[] _guardianScratch = new string[FormationSystem.GuardianSlotCount];
        readonly string[] _echoScratch = new string[FormationSystem.EchoSlotCount];
        static readonly PowerConfig DefaultConfig = new();

        // PreviewPlace 한 칸 캐시: 같은 (역할, 칸, 형태)를 다시 물으면 다시 계산하지 않는다 (드래그 중 같은 칸 위에서 여러 번 불러도 할당 없음).
        // 다시 계산(EnsureComputed)하거나 더러움 표시(MarkDirty)가 되면 비운다
        bool _hasPreview;
        CharacterRole _previewRole;
        int _previewSlot;
        string _previewFormId;
        long _previewTeam;

        // 자동 편성 미리보기 캐시 (PreviewAutoFormation): 더러워지면 잊는다. 고를 때 FormationSystem.BuildAutoFormation이 목록을 만들므로
        // 강해지기 줄 · 편성 화면이 여러 번 물어도 다시 고르지 않게 한다
        bool _hasAutoPreview;
        long _autoPreviewTeam;
        readonly string[] _autoGuardians = new string[FormationSystem.GuardianSlotCount];
        readonly string[] _autoEchoes = new string[FormationSystem.EchoSlotCount];
        readonly string[] _keptGuardians = new string[FormationSystem.GuardianSlotCount];
        readonly string[] _keptEchoes = new string[FormationSystem.EchoSlotCount];

        public PowerSystem(PlayerData data, MetaDatabase db, CharacterSystem characters, FormationSystem formation, SynergySystem synergy,
            GrowthSystem growth, CostumeSystem costumes)
        {
            _data = data;
            _db = db;
            _characters = characters;
            _formation = formation;
            _synergy = synergy;
            _growth = growth;
            _costumes = costumes;

            if (_formation != null) _formation.Changed += MarkDirty;
            if (_characters != null) _characters.Changed += MarkDirty;
            if (_synergy != null) _synergy.Changed += MarkDirty;
            if (_growth != null) _growth.LevelChanged += OnLevelChanged;
            if (_costumes != null) _costumes.Changed += MarkDirty;
        }

        PowerConfig Config => _db != null && _db.power != null ? _db.power : DefaultConfig;

        /// <summary>지금 편성의 팀 전투력 (캐시, 더러우면 다시 계산)</summary>
        public long TeamPower
        {
            get
            {
                EnsureComputed();
                return _team;
            }
        }

        /// <summary>지금 편성의 지킴이 합 (가중치 적용 전)</summary>
        public long GuardianPower
        {
            get
            {
                EnsureComputed();
                return _guardians;
            }
        }

        /// <summary>지금 덱 잔향이 합 (가중치 적용 전)</summary>
        public long EchoPower
        {
            get
            {
                EnsureComputed();
                return _echoes;
            }
        }

        /// <summary>
        /// 형태 하나의 전투력. 편성 시너지(팀 + 그 계보 몫)를 반영한다 (편성에 없어도 지금 켜진 팀 시너지는 반영).
        /// 미보유/미해금 형태는 0.
        /// </summary>
        public long GetUnitPower(string formId)
        {
            if (!IsUsable(formId, out string lineageId)) return 0;
            EnsureComputed();
            return Round(UnitPower(formId, lineageId, _modifiers, _attackMultiplier));
        }

        /// <summary>시너지 없이 형태 능력치(레벨·공명·의상 반영)만으로 본 전투력 (편성 카드 PowerText, 목록 카드, 가챠 결과). 할당 없음</summary>
        public long GetFormPower(string formId)
        {
            var db = _characters?.Db;
            if (db == null || string.IsNullOrEmpty(formId) || db.GetForm(formId) == null) return 0;
            return Round(ComputeUnitPower(_characters.GetStats(formId), null));
        }

        /// <summary>계보 전투력 = 해금한 형태 중 가장 높은 GetFormPower</summary>
        public long GetLineagePower(string lineageId)
        {
            var db = _characters?.Db;
            var lineage = db != null ? db.GetLineage(lineageId) : null;
            if (lineage == null || !_characters.IsOwned(lineageId)) return 0;
            long best = 0;
            foreach (var form in lineage.forms)
            {
                if (form == null || !_characters.IsFormUnlocked(form.id)) continue;
                long power = GetFormPower(form.id);
                if (power > best) best = power;
            }
            return best;
        }

        /// <summary>
        /// 그 칸에 formId를 놓았을 때의 팀 전투력 미리보기 (편성 끌어 놓기 "전투력 +312" / "−80"). 놓을 수 없거나 그대로면 지금 팀 전투력.
        /// FormationSystem.TryPlace와 같은 규칙(맞바꾸기/밀어내기)으로 편성을 잠깐 바꿔 시너지(SynergySystem.BuildModifiers)와
        /// 계보 공명 공격력 배율까지 다시 계산하므로, 실제로 놓은 뒤의 TeamPower와 같다 (근사값 아님).
        /// 편성 데이터는 바로 되돌리고 이벤트는 울리지 않는다. 시너지 계산이 할당하므로 드래그 중 놓을 칸이 바뀔 때 한 번만 부른다 (매 프레임 금지).
        /// 마지막 결과 하나를 기억해 같은 (역할, 칸, 형태)를 다시 물으면 계산·할당 없이 돌려준다 — 편성·능력치가 바뀌어 다시 계산하면 잊는다.
        /// </summary>
        public long PreviewPlace(CharacterRole role, int slot, string formId)
        {
            EnsureComputed();
            if (_hasPreview && _previewRole == role && _previewSlot == slot && string.Equals(_previewFormId, formId, StringComparison.Ordinal))
                return _previewTeam;
            if (_formation == null || !_formation.BeginPreviewPlace(role, slot, formId)) return _team;
            long team;
            try
            {
                var modifiers = BuildSynergyModifiers();
                float attackMultiplier = TeamAttackMultiplier();
                CopyFormation();
                ComputeTeam(_guardianScratch, _echoScratch, modifiers, attackMultiplier, CurrentShopHp(), out team, out _, out _);
            }
            finally
            {
                _formation.EndPreviewPlace();
            }
            _hasPreview = true;
            _previewRole = role;
            _previewSlot = slot;
            _previewFormId = formId;
            _previewTeam = team;
            return team;
        }

        /// <summary>
        /// 계보 레벨 +1 했을 때 팀 전투력 증가 (레벨업 버튼·계보 카드 "전투력 +N"). 편성에 없는 계보, 최대 레벨, 미보유는 0.
        /// 레벨은 시너지 단계와 무관하므로 지금 시너지 보정으로 정확하다. 할당 없음 (카드 Set마다 불러도 된다).
        /// </summary>
        public long PreviewLevelUp(string lineageId) => PreviewLevelUp(lineageId, 1);

        /// <summary>
        /// 계보 레벨 +levels 했을 때 팀 전투력 증가 (강해지기 LineageLevel 줄 "+5레벨", SPEC4 §13.2). 최대 레벨에서 멈춘다.
        /// 편성에 없는 계보 · 미보유 · 최대 레벨 · levels ≤ 0은 0. 할당 없음
        /// </summary>
        public long PreviewLevelUp(string lineageId, int levels)
        {
            var save = _characters?.GetSave(lineageId);
            if (save == null || levels <= 0 || save.level >= _characters.MaxLevel) return 0;
            int add = Math.Min(levels, _characters.MaxLevel - save.level);
            EnsureComputed();
            CopyFormation();
            long before = _team;
            save.level += add;
            try
            {
                ComputeTeam(_guardianScratch, _echoScratch, _modifiers, _attackMultiplier, CurrentShopHp(), out long after, out _, out _);
                return Math.Max(0, after - before);
            }
            finally
            {
                save.level -= add;
            }
        }

        /// <summary>
        /// 계보 공명 +1 했을 때 팀 전투력 증가 (강해지기 ResonanceReady 줄). 공명은 계보 능력치와 계보 공명 공격력 배율을 함께 올리므로
        /// 시너지 · 배율까지 다시 계산한다 (이벤트 없음, 되돌림). 최대 공명 · 미보유 · 편성에 없으면 0
        /// </summary>
        public long PreviewResonance(string lineageId)
        {
            var save = _characters?.GetSave(lineageId);
            if (save == null || save.resonance >= _characters.MaxResonanceLevel) return 0;
            EnsureComputed();
            long before = _team;
            save.resonance++;
            try
            {
                var modifiers = BuildSynergyModifiers();
                float attackMultiplier = TeamAttackMultiplier();
                CopyFormation();
                ComputeTeam(_guardianScratch, _echoScratch, modifiers, attackMultiplier, CurrentShopHp(), out long after, out _, out _);
                return Math.Max(0, after - before);
            }
            finally
            {
                save.resonance--;
            }
        }

        /// <summary>
        /// 스테이지 권장 전투력. 세 구간 복리 (SPEC4 §7.1 #1, SPEC5 §4.3 #2 — BattleConfig.TaperedScale과 같은 걸음 규칙):
        /// 스테이지 s → s+1 한 걸음의 배율은 s &lt; T면 recommendedGrowth, s &lt; T2면 recommendedGrowthLate, 그 뒤로는 recommendedGrowthLate2.
        /// 그래서 rec(T) = base × g^(T−1), s &gt; T면 base × g^(T−1) × gL^(s−T) (T에서 이어진다).
        /// T = recommendedTaperStage ≤ 0이면 지수 하나 (옛 식, T2도 무시). T2 = recommendedTaperStage2 ≤ 0이면 두 구간.
        /// T2 ≤ T면 T부터 바로 gL2. 성장률이 0 이하(콘텐츠가 비움)면 앞 구간 성장률을 그대로 쓴다 — 값을 비워도 숫자가 바뀌지 않는다.
        /// </summary>
        public long GetRecommendedPower(int stage)
        {
            var config = Config;
            double growth = Math.Max(1.0, config.recommendedGrowth);
            int steps = Math.Max(1, stage) - 1;
            int early = steps;
            int late = 0;
            int late2 = 0;
            double lateGrowth = growth;
            double lateGrowth2 = growth;
            if (config.recommendedTaperStage > 0)
            {
                lateGrowth = config.recommendedGrowthLate > 0 ? Math.Max(1.0, config.recommendedGrowthLate) : growth;
                lateGrowth2 = lateGrowth;
                int taper = config.recommendedTaperStage - 1;
                early = Math.Min(steps, taper);
                late = steps - early;
                if (config.recommendedTaperStage2 > 0)
                {
                    lateGrowth2 = config.recommendedGrowthLate2 > 0 ? Math.Max(1.0, config.recommendedGrowthLate2) : lateGrowth;
                    int taper2 = Math.Max(taper, config.recommendedTaperStage2 - 1);
                    late2 = Math.Max(0, steps - Math.Max(early, taper2));
                    late -= late2;
                }
            }
            double value = Math.Max(0, config.recommendedBase) * Math.Pow(growth, early);
            if (late > 0) value *= Math.Pow(lateGrowth, late);
            if (late2 > 0) value *= Math.Pow(lateGrowth2, late2);
            return Round(value);
        }

        /// <summary>
        /// 던전 층 권장 전투력 (PRG-01, SPEC4 §7.1 #2): dungeonRecommendedBase × dungeonRecommendedGrowth^(층−1).
        /// 잔향이 덱 전투력 EchoPower(가중치 전 · 지킴이는 던전에서 싸우지 않는다)와 비교한다 — GetDungeonRating.
        /// 0 = 권장 줄 없음 (dungeonRecommendedBase ≤ 0일 때, 지금 콘텐츠).
        /// </summary>
        public long GetDungeonRecommended(int floor)
        {
            var config = Config;
            if (!(config.dungeonRecommendedBase > 0)) return 0L;
            return Round(config.dungeonRecommendedBase * Math.Pow(Math.Max(1.0, config.dungeonRecommendedGrowth), Math.Max(1, floor) - 1));
        }

        /// <summary>던전 층 평가: 잔향이 덱 전투력 EchoPower ÷ GetDungeonRecommended (같은 1.0 / 0.8 경계, 권장 0이면 여유 — 줄은 숨긴다)</summary>
        public PowerRating GetDungeonRating(int floor) => GetRating(EchoPower, GetDungeonRecommended(floor));

        /// <summary>
        /// 수선점 레벨을 n번 올렸을 때 늘어날 팀 전투력 (강해지기 ShopUpgrade 줄, SPEC4 §13.2). 최대 레벨에서 멈춘다.
        /// 지금 편성 · 시너지 그대로 수선점 내구도만 바꿔 계산한다 (S5-24 상한 포함 — 상한에 걸려 있으면 0). 0 이하 = 늘지 않음,
        /// 미리보기라 Changed 없음 · 할당 없음.
        /// </summary>
        public long PreviewShopLevels(int n)
        {
            if (n <= 0) return 0L;
            int level = _growth != null ? _growth.Level : Math.Max(1, _data?.level ?? 1);
            long target = (long)level + n;
            if (_growth != null) target = Math.Min(target, Math.Max(level, _growth.MaxLevel));
            if (target <= level) return 0L;
            EnsureComputed();
            CopyFormation();
            ComputeTeam(_guardianScratch, _echoScratch, _modifiers, _attackMultiplier, GrowthSystem.CalcShopMaxHp((int)Math.Min(target, int.MaxValue)),
                out long after, out _, out _);
            return Math.Max(0L, after - _team);
        }

        /// <summary>
        /// 자동 편성(FormationSystem.AutoFormation)이 놓을 편성의 팀 전투력 (시너지 · 계보 공명 포함, 세이브는 바꾸지 않음, Changed 없음).
        /// FormationSystem.BuildAutoFormation으로 같은 고르기를 하고, PreviewPlace처럼 편성 배열에 잠깐 넣어 계산한 뒤 바로 되돌린다.
        /// 결과는 전투력이 더러워질 때까지 기억한다. 강해지기 AutoFormation 줄은 이 값 ≥ 1.05 × TeamPower일 때만 (SPEC4 §13.2).
        /// 계산하지 못하면 TeamPower (= 늘지 않음).
        /// </summary>
        public long PreviewAutoFormation()
        {
            EnsureComputed();
            if (_hasAutoPreview) return _autoPreviewTeam;
            long team = _team;
            try
            {
                if (_formation != null)
                {
                    _formation.GetGuardian(0); // 세이브 배열 크기를 맞춘다 (FormationSave.Sanitize)
                    var save = _data?.formation;
                    if (save != null && save.guardians != null && save.echoes != null
                        && save.guardians.Length == FormationSystem.GuardianSlotCount && save.echoes.Length == FormationSystem.EchoSlotCount)
                    {
                        _formation.BuildAutoFormation(_autoGuardians, _autoEchoes);
                        team = ComputeWith(save, _autoGuardians, _autoEchoes);
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Power] 자동 편성 미리보기를 계산하지 못했어요: {e.Message}");
                team = _team;
            }
            _hasAutoPreview = true;
            _autoPreviewTeam = team;
            return team;
        }

        /// <summary>
        /// 자동 편성을 누르면 늘어날 전투력 = max(0, PreviewAutoFormation − TeamPower) (시너지 포함 · 미리보기라 Changed 없음).
        /// 0 = 늘지 않음 → 부르는 쪽은 '자동 편성 +N' 줄 · 자동 편성 반짝임 · 손가락을 숨긴다 (UX W2 C-21 FR-1: 옛 W2ContractShims 확장을 대신한다).
        /// </summary>
        public long PreviewAutoFormationGain() => Math.Max(0L, PreviewAutoFormation() - TeamPower);

        /// <summary>팀 전투력 / 권장 전투력</summary>
        public float GetRatio(int stage)
        {
            long recommended = GetRecommendedPower(stage);
            return recommended <= 0 ? float.PositiveInfinity : (float)((double)TeamPower / recommended);
        }

        public PowerRating GetRating(int stage) => GetRating(TeamPower, GetRecommendedPower(stage));

        /// <summary>비율 → 평가 (MetaDatabase.power 경계)</summary>
        public PowerRating GetRating(long power, long recommended)
        {
            if (recommended <= 0) return PowerRating.Comfortable;
            var config = Config;
            // 경계값은 float라 0.8f = 0.80000001 — 정확히 80%가 "어려움"이 되지 않게 아주 작은 여유를 둔다
            const double epsilon = 1e-6;
            double ratio = (double)power / recommended;
            if (ratio >= config.comfortableRatio - epsilon) return PowerRating.Comfortable;
            if (ratio >= config.challengeRatio - epsilon) return PowerRating.Challenge;
            return PowerRating.Hard;
        }

        /// <summary>평가 문구 (여유 / 도전 / 어려움)</summary>
        public static string RatingName(PowerRating rating) => rating switch
        {
            PowerRating.Comfortable => "여유",
            PowerRating.Challenge => "도전",
            _ => "어려움",
        };

        /// <summary>더러우면 다시 계산하고, 값이 바뀌었으면 Changed. MetaSession.Tick이 프레임마다 부른다 (깨끗하면 바로 반환)</summary>
        public void Refresh()
        {
            if (!_dirty && _hasReported && _reported == _team) return;
            EnsureComputed();
            if (!_hasReported)
            {
                _hasReported = true;
                _reported = _team;
                return;
            }
            if (_reported == _team) return;
            long old = _reported;
            _reported = _team;
            Changed?.Invoke(old, _team);
        }

        /// <summary>다음 Refresh에서 다시 계산하게 표시 (PreviewPlace 캐시도 잊는다)</summary>
        public void MarkDirty()
        {
            _dirty = true;
            ForgetPreview();
        }

        void ForgetPreview()
        {
            _hasPreview = false;
            _previewFormId = null;
            _hasAutoPreview = false;
        }

        /// <summary>유닛 전투력 공식. modifiers가 null이면 보정 없음</summary>
        public static double ComputeUnitPower(CharacterStats stats, StatBlock modifiers) => ComputeUnitPower(stats, modifiers, 1f);

        /// <summary>표기: 100만 미만은 콤마 정수, 이상은 NumberFormatter.Format</summary>
        public static string Format(long power) =>
            Math.Abs(power) < 1_000_000 ? power.ToString("N0", Invariant) : NumberFormatter.Format(power);

        // ───────────────────────────── 계산 ─────────────────────────────

        /// <summary>공식 본체. attackMultiplier = 편성 공명 보너스 (전투의 AttackMultiplier)</summary>
        static double ComputeUnitPower(CharacterStats stats, StatBlock modifiers, float attackMultiplier)
        {
            double atkPct = modifiers != null ? modifiers[StatType.AttackPercent] / 100.0 : 0;
            double speedPct = modifiers != null ? modifiers[StatType.AttackSpeedPercent] / 100.0 : 0;
            double hpPct = modifiers != null ? modifiers[StatType.HpPercent] / 100.0 : 0;
            double critChance = modifiers != null ? Math.Max(0, Math.Min(1, modifiers[StatType.CritChancePercent] / 100.0)) : 0;
            double critDamage = modifiers != null ? Math.Max(0, modifiers[StatType.CritDamagePercent] / 100.0) : 0;

            double attack = Math.Max(0, stats.attack) * Math.Max(0, attackMultiplier) * Math.Max(0.1, 1 + atkPct);
            double interval = Math.Max(0.1, stats.attackInterval);
            double speed = Math.Max(0.2, 1 + speedPct);
            double offense = attack / interval * speed * 10.0 * (1 + critChance * critDamage);
            double defense = Math.Max(0, stats.hp) * Math.Max(0.1, 1 + hpPct);
            double total = offense + defense;
            return double.IsNaN(total) || double.IsInfinity(total) ? 0 : total;
        }

        static long Round(double value) =>
            double.IsNaN(value) || value <= 0 ? 0 : value >= long.MaxValue ? long.MaxValue : (long)Math.Round(value, MidpointRounding.AwayFromZero);

        void OnLevelChanged(int oldLevel, int newLevel) => MarkDirty();

        void EnsureComputed()
        {
            if (!_dirty) return;
            _dirty = false;
            ForgetPreview();
            _modifiers = BuildSynergyModifiers();
            _attackMultiplier = TeamAttackMultiplier();
            CopyFormation();
            ComputeTeam(_guardianScratch, _echoScratch, _modifiers, _attackMultiplier, CurrentShopHp(), out _team, out _guardians, out _echoes);
        }

        /// <summary>
        /// 편성 배열(세이브)에 다른 편성을 잠깐 넣고 시너지 · 공명 배율까지 다시 계산한 팀 전투력 (바로 되돌림, 이벤트 없음).
        /// save 배열 길이는 슬롯 수와 같아야 한다 (부르는 쪽이 확인)
        /// </summary>
        long ComputeWith(FormationSave save, string[] guardians, string[] echoes)
        {
            Array.Copy(save.guardians, _keptGuardians, FormationSystem.GuardianSlotCount);
            Array.Copy(save.echoes, _keptEchoes, FormationSystem.EchoSlotCount);
            try
            {
                Array.Copy(guardians, save.guardians, FormationSystem.GuardianSlotCount);
                Array.Copy(echoes, save.echoes, FormationSystem.EchoSlotCount);
                var modifiers = BuildSynergyModifiers();
                float attackMultiplier = TeamAttackMultiplier();
                CopyFormation();
                ComputeTeam(_guardianScratch, _echoScratch, modifiers, attackMultiplier, CurrentShopHp(), out long team, out _, out _);
                return team;
            }
            finally
            {
                Array.Copy(_keptGuardians, save.guardians, FormationSystem.GuardianSlotCount);
                Array.Copy(_keptEchoes, save.echoes, FormationSystem.EchoSlotCount);
                for (int i = 0; i < _keptGuardians.Length; i++) _keptGuardians[i] = null;
                for (int i = 0; i < _keptEchoes.Length; i++) _keptEchoes[i] = null;
            }
        }

        /// <summary>지금 수선점 최대 내구도 (시너지 보정 전)</summary>
        double CurrentShopHp() => _growth != null ? _growth.ShopMaxHp : GrowthSystem.CalcShopMaxHp(_data?.level ?? 1);

        /// <summary>지금 편성의 시너지 보정 (스테이지 기믹 제외). 읽지 못하면 경고 후 null(보정 없음)</summary>
        BattleModifiers BuildSynergyModifiers()
        {
            try
            {
                return _synergy != null ? _synergy.BuildModifiers(StageModifierType.None) : null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Power] 시너지 보정을 읽지 못했어요: {e.Message}");
                return null;
            }
        }

        /// <summary>지금 편성의 계보 공명 공격력 배율 (전투의 AttackMultiplier와 같은 식)</summary>
        float TeamAttackMultiplier() => _formation != null ? 1f + Math.Max(0f, _formation.TeamAttackBonusPercent) / 100f : 1f;

        /// <summary>지금 편성을 계산용 배열로 복사 (잠긴 덱 칸은 비움)</summary>
        void CopyFormation()
        {
            for (int i = 0; i < _guardianScratch.Length; i++) _guardianScratch[i] = _formation?.GetGuardian(i);
            for (int i = 0; i < _echoScratch.Length; i++) _echoScratch[i] = _formation?.GetEcho(i);
        }

        /// <summary>
        /// 편성 배열 + 시너지 보정 + 공명 배율 + 수선점 내구도(보정 전 rawShopHp)로 팀 전투력 (modifiers가 null이면 보정 없음).
        /// 팀 = 공격 몫 + min(수선점 몫, 공격 몫 × shopShareCap) — shopShareCap ≤ 0이면 상한 없음 (S5-24)
        /// </summary>
        void ComputeTeam(string[] guardians, string[] echoes, BattleModifiers modifiers, float attackMultiplier, double rawShopHp,
            out long team, out long guardianSum, out long echoSum)
        {
            double g = 0;
            for (int i = 0; i < guardians.Length; i++)
            {
                if (IsUsable(guardians[i], out string lineageId)) g += UnitPower(guardians[i], lineageId, modifiers, attackMultiplier);
            }
            double e = 0;
            for (int i = 0; i < echoes.Length; i++)
            {
                if (IsUsable(echoes[i], out string lineageId)) e += UnitPower(echoes[i], lineageId, modifiers, attackMultiplier);
            }

            var config = Config;
            double shopHp = Math.Max(0, rawShopHp);
            if (modifiers != null) shopHp *= Math.Max(0.1, 1 + BattleSimulator.SumGlobal(modifiers, StatType.ShopMaxHpPercent) / 100.0);

            guardianSum = Round(g);
            echoSum = Round(e);
            double offense = g * config.guardianWeight + e * config.echoWeight;
            double shopPart = shopHp * config.shopHpWeight;
            if (config.shopShareCap > 0) shopPart = Math.Min(shopPart, Math.Max(0, offense) * config.shopShareCap);
            team = Round(offense + shopPart);
        }

        double UnitPower(string formId, string lineageId, BattleModifiers modifiers, float attackMultiplier)
        {
            StatBlock mods = null;
            if (modifiers != null)
            {
                modifiers.FillFor(_scratch, lineageId, formId);
                mods = _scratch;
            }
            return ComputeUnitPower(_characters.GetStats(formId), mods, attackMultiplier);
        }

        /// <summary>보유 계보의 해금된 형태인지 (계보 ID도 돌려준다)</summary>
        bool IsUsable(string formId, out string lineageId)
        {
            lineageId = null;
            if (string.IsNullOrEmpty(formId) || _characters?.Db == null) return false;
            var lineage = _characters.Db.GetLineageOfForm(formId);
            if (lineage == null || !_characters.IsOwned(lineage.id) || !_characters.IsFormUnlocked(formId)) return false;
            lineageId = lineage.id;
            return true;
        }
    }
}
