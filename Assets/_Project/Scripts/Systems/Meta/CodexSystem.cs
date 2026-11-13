using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>도감 칸 하나의 상태 (CodexSystem.GetEntries가 채운다, 목록 재사용 가능)</summary>
    public sealed class CodexEntryStatus
    {
        /// <summary>CodexSystem.Key(종류, ID)</summary>
        public string Key;
        public CodexCategory Category;
        public string Id;
        public bool Found;
        public bool Claimable;
        public bool Claimed;
        /// <summary>처음 찾은 보상 (없으면 null)</summary>
        public List<MetaReward> Reward;
        /// <summary>못 찾았을 때 힌트 (콘텐츠 CodexConfig.hints)</summary>
        public string Hint;
    }

    /// <summary>
    /// 도감 (Q-05, SPEC4 §6.2): 찾기 기록(설치 때부터) · 처음 찾은 보상(도감 1-10 개방 뒤) · 영구 모음 보너스(SynergySystem.ExtraTeamModifiers).
    /// <para>
    /// 찾기: 친구 형태(Characters.Granted 기본형 · Evolution.FormUnlocked · 그 밖의 길로 해금된 형태는 Characters.Changed 뒤 다음 Tick에 훑기) ·
    /// 의상(Costumes.CostumeUnlocked · 훑기) · 적(전투 첫 EnemyKilled, Battle.Sim.GetEnemyDefId — 이미 찾은 적은 할당 없이 바로 끝) ·
    /// 시너지(전투 SynergyActivated: 처음 켜짐 = synergy:{id}, 마지막 단계 = synergymax:{id} 보상만) · 재료(Inventory.ItemAdded) ·
    /// 이야기(동화책 story.seenChapters — 보상 없음, 동화책 쪽이 별조각 50).
    /// codex.backfillPending(버전 4 이전 세이브)이거나 찾은 것이 아직 없으면 Connect에서 이미 가진 형태 · 의상 · 재료, minStage &lt; 최고 스테이지인 적,
    /// 지금 켜진 시너지를 "찾음"으로 채운다 (받기는 자동 아님). 보상(별조각)은 CodexConfig 종류별 값이고 도감이 열린 뒤에만 받는다.
    /// </para>
    /// <para>
    /// 영구 모음 보너스는 도감이 열린 뒤 받기 없이 저절로 (SynergySystem.ExtraTeamModifiers에 더한다): 찾은 형태 수 → 공격력 · 체력 %(formTiers),
    /// 적을 모두 찾으면 수선점 최대 내구도 %, 켜 본 시너지 종류 수 → 불빛 충전 %(synergyTiers). 더해지는 값이 바뀌면 SynergySystem.MarkDirty →
    /// 전투력 · 다음 전투가 다시 계산. 수는 찾을 때마다 캐시를 고쳐 두므로 AddCollectionBonus는 할당 없이 더하기만 한다.
    /// 빨간 점 RedDotPaths.Codex = 받을 칸 수.
    /// </para>
    /// </summary>
    public class CodexSystem : IMetaModule
    {
        /// <summary>찾음 · 받음 상태가 바뀜</summary>
        public event Action Changed;

        const int CategoryCount = (int)CodexCategory.Story + 1;

        /// <summary>도감 항목 하나 (콘텐츠에서 한 번 만든다)</summary>
        sealed class EntryInfo
        {
            public string Key;
            public CodexCategory Category;
            public string Id;
            /// <summary>처음 찾은 보상 별조각 (0 = 없음)</summary>
            public int Gem;
            /// <summary>칸이 아니라 보상만 (synergymax:*)</summary>
            public bool RewardOnly;
            /// <summary>이야기 칸의 장 번호</summary>
            public int Chapter;
            public List<MetaReward> Reward;
        }

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly ProgressTracker _progress;
        readonly MetaRewardService _rewards;
        readonly FeatureGateSystem _features;
        readonly CharacterSystem _characters;
        readonly CostumeSystem _costumes;
        readonly EvolutionSystem _evolution;
        readonly SynergySystem _synergy;
        readonly BattleSystem _battle;
        readonly InventorySystem _inventory;

        static readonly CodexConfig DefaultConfig = new();

        readonly List<EntryInfo> _entries = new();
        readonly Dictionary<string, EntryInfo> _byKey = new();
        readonly Dictionary<string, EntryInfo>[] _byId = new Dictionary<string, EntryInfo>[CategoryCount];
        readonly Dictionary<string, string> _hints = new();
        readonly Dictionary<string, SynergyDef> _synergyDefs = new();
        /// <summary>이미 찾은 적 · 시너지 정의 ID (전투 이벤트 빠른 길 — 할당 없이 바로 끝)</summary>
        readonly HashSet<string> _knownEnemies = new();
        readonly HashSet<string> _knownSynergies = new();
        readonly HashSet<string> _knownSynergyMax = new();
        bool _indexed;

        readonly int[] _found = new int[CategoryCount];
        readonly int[] _total = new int[CategoryCount];
        readonly int[] _claimable = new int[CategoryCount];
        int _claimableTotal;
        int _foundTotal;
        int _entryTotal;
        bool _countsDirty = true;
        bool _countedUnlocked;
        int _storySeenCount = -1;

        bool _connected;
        bool _scanPending;
        Func<MetaCondition, long?> _previousCondition;
        float _slowTimer;
        RedDotSystem _dots;

        // 마지막으로 SynergySystem에 알린 보너스 (바뀌면 MarkDirty). 음수 = 아직 알리지 않음
        float _bonusForm = -1f;
        float _bonusShop = -1f;
        float _bonusLamp = -1f;

        public CodexSystem(PlayerData data, MetaDatabase db, ProgressTracker progress, MetaRewardService rewards, FeatureGateSystem features,
            CharacterSystem characters, CostumeSystem costumes, EvolutionSystem evolution, SynergySystem synergy, BattleSystem battle,
            InventorySystem inventory)
        {
            _data = data;
            _db = db;
            _progress = progress;
            _rewards = rewards;
            _features = features;
            _characters = characters;
            _costumes = costumes;
            _evolution = evolution;
            _synergy = synergy;
            _battle = battle;
            _inventory = inventory;
            for (int i = 0; i < CategoryCount; i++) _byId[i] = new Dictionary<string, EntryInfo>();
        }

        CodexConfig Config => _db != null && _db.codex != null ? _db.codex : DefaultConfig;

        CodexSave Save
        {
            get
            {
                if (_data == null) return null;
                _data.meta ??= new MetaSave();
                _data.meta.codex ??= new CodexSave();
                _data.meta.codex.Sanitize();
                return _data.meta.codex;
            }
        }

        /// <summary>도감 보상을 받을 수 있는지 (FeatureId.Codex 1-10, 정의가 없으면 열림). 찾기 기록은 잠겨 있어도 쌓인다</summary>
        public bool IsUnlocked
        {
            get
            {
                if (_features == null) return true;
                try
                {
                    return _features.IsUnlocked(FeatureId.Codex);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Codex] 기능 개방 확인 실패: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>전체 찾은 비율 0~100 (보상 전용 synergymax 제외, 이야기 포함)</summary>
        public float CompletionPercent
        {
            get
            {
                Recount();
                return _entryTotal <= 0 ? 0f : 100f * _foundTotal / _entryTotal;
            }
        }

        public int FoundCount(CodexCategory category)
        {
            Recount();
            int index = (int)category;
            return index >= 0 && index < CategoryCount ? _found[index] : 0;
        }

        public int TotalCount(CodexCategory category)
        {
            Recount();
            int index = (int)category;
            return index >= 0 && index < CategoryCount ? _total[index] : 0;
        }

        /// <summary>그 탭의 칸을 채운다 (into는 비우고 채움, 콘텐츠 순서). 화면을 열 때 부른다 (칸마다 상태를 새로 만든다)</summary>
        public void GetEntries(CodexCategory category, List<CodexEntryStatus> into)
        {
            if (into == null) return;
            into.Clear();
            Recount();
            var save = Save;
            bool unlocked = IsUnlocked;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Category != category || entry.RewardOnly) continue;
                bool found = IsFound(entry, save);
                bool claimed = save != null && save.claimed.Contains(entry.Key);
                into.Add(new CodexEntryStatus
                {
                    Key = entry.Key,
                    Category = entry.Category,
                    Id = entry.Id,
                    Found = found,
                    Claimed = claimed,
                    Claimable = unlocked && found && !claimed && entry.Gem > 0,
                    Reward = entry.Gem > 0 ? RewardOf(entry) : null,
                    Hint = found ? null : HintFor(entry),
                });
            }
        }

        /// <summary>받을 칸 수 (null = 모든 탭, 시너지 탭은 synergymax 보상 포함). 잠겨 있으면 0. 할당 없음</summary>
        public int ClaimableCount(CodexCategory? category = null)
        {
            Recount();
            if (!category.HasValue) return _claimableTotal;
            int index = (int)category.Value;
            return index >= 0 && index < CategoryCount ? _claimable[index] : 0;
        }

        /// <summary>한 칸(또는 synergymax:* 보상) 보상을 받는다. 잠겼거나 찾지 못했거나 이미 받았거나 보상이 없으면 null</summary>
        public List<MetaReward> TryClaim(string key)
        {
            if (string.IsNullOrEmpty(key) || !IsUnlocked) return null;
            Recount();
            var save = Save;
            if (save == null || !_byKey.TryGetValue(key, out var entry) || entry.Gem <= 0) return null;
            if (!IsFound(entry, save) || save.claimed.Contains(key)) return null;
            save.claimed.Add(key);
            var granted = Grant(RewardOf(entry));
            _progress?.Increment(CounterKey.CodexClaim);
            AfterClaimsChanged();
            return granted;
        }

        /// <summary>그 탭(null = 모두)의 받을 칸을 모두 받는다 (콘텐츠 순서, 받은 것이 없으면 빈 목록)</summary>
        public List<MetaReward> ClaimAll(CodexCategory? category = null)
        {
            if (!IsUnlocked) return new List<MetaReward>();
            Recount();
            var save = Save;
            if (save == null) return new List<MetaReward>();
            var rewards = new List<MetaReward>();
            int claimed = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Gem <= 0 || (category.HasValue && entry.Category != category.Value)) continue;
                if (!IsFound(entry, save) || save.claimed.Contains(entry.Key)) continue;
                save.claimed.Add(entry.Key);
                rewards.AddRange(RewardOf(entry));
                claimed++;
            }
            if (claimed == 0) return rewards;
            var granted = Grant(MetaRewardService.Merge(rewards));
            _progress?.Increment(CounterKey.CodexClaim, claimed);
            AfterClaimsChanged();
            return granted;
        }

        /// <summary>
        /// 영구 모음 보너스를 팀 능력치에 더한다 (도감이 열린 뒤에만, SynergySystem.ExtraTeamModifiers가 BuildModifiers 끝에서 부름). 할당 없음
        /// </summary>
        public void AddCollectionBonus(StatBlock team)
        {
            if (team == null || !IsUnlocked) return;
            Recount();
            ComputeBonus(out float form, out float shop, out float lamp);
            if (form != 0f)
            {
                team.Add(StatType.AttackPercent, form);
                team.Add(StatType.HpPercent, form);
            }
            if (shop != 0f) team.Add(StatType.ShopMaxHpPercent, shop);
            if (lamp != 0f) team.Add(StatType.LampGainPercent, lamp);
        }

        /// <summary>그 스킬을 본 적이 있는지 (스킬 이름 첫 표시 FX-06 — codex.seenSkills)</summary>
        public bool HasSeenSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            var save = Save;
            return save != null && save.seenSkills.Contains(skillId);
        }

        /// <summary>그 스킬을 봤다고 적는다 (세이브에 남는다, Changed 없음)</summary>
        public void MarkSkillSeen(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return;
            Save?.seenSkills.Add(skillId);
        }

        /// <summary>도감 항목 키 (form:{형태} · costume:{의상} · enemy:{적} · synergy:{시너지} · material:{재료} · story:{장})</summary>
        public static string Key(CodexCategory category, string id) => KeyPrefix(category) + id;

        /// <summary>시너지 최고 단계 보상 키 (synergymax:{id} — 칸이 아니라 보상만)</summary>
        public static string SynergyMaxKey(string synergyId) => SynergyMaxPrefix + synergyId;

        /// <summary>시너지 최고 단계 보상 키 머리</summary>
        public const string SynergyMaxPrefix = "synergymax:";

        /// <summary>종류별 키 머리 ("form:" …)</summary>
        public static string KeyPrefix(CodexCategory category) => category switch
        {
            CodexCategory.Friend => "form:",
            CodexCategory.Costume => "costume:",
            CodexCategory.Enemy => "enemy:",
            CodexCategory.Synergy => "synergy:",
            CodexCategory.Material => "material:",
            CodexCategory.Story => "story:",
            _ => "unknown:",
        };

        // ───────────────────────────── IMetaModule ─────────────────────────────

        /// <summary>구독 · 보너스 훅을 잇고(한 번만) 채우기(backfill) 또는 가진 것 훑기</summary>
        public void Connect()
        {
            if (_connected) return;
            _connected = true;
            if (_characters != null)
            {
                _characters.Granted += OnGranted;
                _characters.Changed += OnOwnedChanged;
            }
            if (_evolution != null) _evolution.FormUnlocked += OnFormUnlocked;
            if (_costumes != null)
            {
                _costumes.CostumeUnlocked += OnCostumeUnlocked;
                _costumes.Changed += OnOwnedChanged;
            }
            if (_inventory != null) _inventory.ItemAdded += OnItemAdded;
            if (_battle != null) _battle.Event += OnBattleEvent;
            if (_features != null) _features.Unlocked += OnFeatureUnlocked;
            if (_synergy != null) _synergy.ExtraTeamModifiers += AddCollectionBonus;
            if (_progress != null)
            {
                _previousCondition = _progress.ExtraConditionValue;
                _progress.ExtraConditionValue = AnswerCondition;
            }

            EnsureIndex();
            var save = Save;
            if (save != null && (save.backfillPending || save.found.Count == 0)) Backfill();
            else ScanOwned();
            CheckBonusChanged();
        }

        public void BeforeAwayApplied(long awaySeconds, bool boot)
        {
        }

        public void AfterStartup(bool boot)
        {
        }

        public void OnDayChanged(string dayKey)
        {
        }

        /// <summary>훑기 예약을 처리하고, 1초마다 동화책 장 수 · 도감 개방 바뀜을 본다 (할당 없음)</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_scanPending)
            {
                _scanPending = false;
                ScanOwned();
            }
            _slowTimer += unscaledDeltaTime;
            if (_slowTimer < 1f) return;
            _slowTimer = 0f;
            var seen = _data?.meta?.story?.seenChapters;
            int seenCount = seen != null ? seen.Count : 0;
            if (seenCount != _storySeenCount)
            {
                _storySeenCount = seenCount;
                if (SyncStoryFound(Save)) AfterFoundChanged();
            }
            if (_countedUnlocked != IsUnlocked)
            {
                _countsDirty = true;
                _dots?.MarkDirty(RedDotPaths.Codex);
                CheckBonusChanged();
                NotifyChanged();
            }
        }

        public void RegisterRedDots(RedDotSystem dots)
        {
            _dots = dots;
            dots?.RegisterCount(RedDotPaths.Codex, () => ClaimableCount());
        }

        /// <summary>진행 기록기 4차 조건: SynergyKindsSeen = 도감에서 켜 본 시너지 종류 수 (캐시, 할당 없음). 모르는 조건은 앞 모듈로</summary>
        long? AnswerCondition(MetaCondition condition)
        {
            if (condition != null && condition.type == MetaConditionType.SynergyKindsSeen) return FoundCount(CodexCategory.Synergy);
            return _previousCondition?.Invoke(condition);
        }

        // ───────────────────────────── 찾기 ─────────────────────────────

        void OnGranted(GrantResult result)
        {
            var lineage = result?.Lineage;
            if (lineage == null || string.IsNullOrEmpty(lineage.baseFormId)) return;
            if (Mark(CodexCategory.Friend, lineage.baseFormId)) AfterFoundChanged();
        }

        void OnFormUnlocked(CharacterFormDef form)
        {
            if (form != null && Mark(CodexCategory.Friend, form.id)) AfterFoundChanged();
        }

        void OnCostumeUnlocked(string costumeId)
        {
            if (Mark(CodexCategory.Costume, costumeId)) AfterFoundChanged();
        }

        void OnItemAdded(string itemId, int count)
        {
            if (count > 0 && Mark(CodexCategory.Material, itemId)) AfterFoundChanged();
        }

        /// <summary>계보 · 의상 데이터가 바뀜 → 다음 Tick에 가진 것을 훑는다 (다른 길로 해금된 형태 · 보상 의상)</summary>
        void OnOwnedChanged() => _scanPending = true;

        void OnFeatureUnlocked(FeatureId feature)
        {
            if (feature != FeatureId.Codex) return;
            _countsDirty = true;
            _dots?.MarkDirty(RedDotPaths.Codex);
            CheckBonusChanged();
            NotifyChanged();
        }

        /// <summary>전투 이벤트 (처치 · 시너지 켜짐). 처치는 초당 여러 번 오므로 이미 찾은 적이면 할당 없이 바로 끝난다</summary>
        internal void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.EnemyKilled:
                    var sim = _battle?.Sim;
                    if (sim != null) NoteEnemy(sim.GetEnemyDefId(e.TargetId));
                    break;
                case BattleEventType.SynergyActivated:
                    NoteSynergy(e.Key, (int)e.Value);
                    break;
            }
        }

        /// <summary>적 정의 하나를 처치했다 (처음이면 찾음). 이미 아는 적은 할당 없음</summary>
        internal void NoteEnemy(string enemyDefId)
        {
            if (string.IsNullOrEmpty(enemyDefId)) return;
            EnsureIndex();
            if (_knownEnemies.Contains(enemyDefId)) return;
            _knownEnemies.Add(enemyDefId);
            if (Mark(CodexCategory.Enemy, enemyDefId)) AfterFoundChanged();
        }

        /// <summary>시너지가 켜졌다 (처음이면 synergy:*, 마지막 단계면 synergymax:* 보상). 이미 아는 시너지는 할당 없음</summary>
        internal void NoteSynergy(string synergyId, int tierIndex)
        {
            if (string.IsNullOrEmpty(synergyId)) return;
            EnsureIndex();
            bool changed = false;
            if (!_knownSynergies.Contains(synergyId))
            {
                _knownSynergies.Add(synergyId);
                changed |= Mark(CodexCategory.Synergy, synergyId);
            }
            if (!_knownSynergyMax.Contains(synergyId) && _synergyDefs.TryGetValue(synergyId, out var def)
                && def.tiers != null && def.tiers.Count > 0 && tierIndex >= def.tiers.Count - 1)
            {
                _knownSynergyMax.Add(synergyId);
                changed |= MarkKey(SynergyMaxKey(synergyId));
            }
            if (changed) AfterFoundChanged();
        }

        /// <summary>
        /// 이미 가진 것을 "찾음"으로 (받기는 자동 아님): 형태 · 의상 · 재료, minStage &lt; 최고 스테이지인 적, 지금 켜진 시너지 (그 단계가 마지막이면 synergymax도).
        /// 여러 번 불러도 같다. backfillPending을 끈다
        /// </summary>
        internal void Backfill()
        {
            EnsureIndex();
            var save = Save;
            if (save == null) return;
            bool changed = ScanOwnedInternal();
            var enemies = _battle?.Config != null ? _battle.Config.enemies : null;
            int highest = _data?.battle != null ? _data.battle.highestStage : 1;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || string.IsNullOrEmpty(enemy.id) || enemy.minStage >= highest) continue;
                    _knownEnemies.Add(enemy.id);
                    changed |= Mark(CodexCategory.Enemy, enemy.id);
                }
            }
            if (_synergy != null)
            {
                List<SynergyStatus> active = null;
                try
                {
                    active = _synergy.GetActive();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Codex] 켜진 시너지를 읽지 못했어요: {e.Message}");
                }
                if (active != null)
                {
                    foreach (var status in active)
                    {
                        if (status?.Def == null || string.IsNullOrEmpty(status.Def.id)) continue;
                        _knownSynergies.Add(status.Def.id);
                        changed |= Mark(CodexCategory.Synergy, status.Def.id);
                        if (status.IsMaxed)
                        {
                            _knownSynergyMax.Add(status.Def.id);
                            changed |= MarkKey(SynergyMaxKey(status.Def.id));
                        }
                    }
                }
            }
            changed |= SyncStoryFound(save);
            save.backfillPending = false;
            if (changed) AfterFoundChanged();
        }

        void ScanOwned()
        {
            if (ScanOwnedInternal()) AfterFoundChanged();
        }

        /// <summary>가진 형태 · 의상 · 재료를 "찾음"으로 (할당 없음, 새로 찾은 것이 있으면 true)</summary>
        bool ScanOwnedInternal()
        {
            EnsureIndex();
            bool changed = false;
            var lineages = _characters?.Db != null ? _characters.Db.lineages : null;
            if (lineages != null)
            {
                for (int i = 0; i < lineages.Count; i++)
                {
                    var lineage = lineages[i];
                    if (lineage?.forms == null || !_characters.IsOwned(lineage.id)) continue;
                    for (int f = 0; f < lineage.forms.Count; f++)
                    {
                        var form = lineage.forms[f];
                        if (form != null && _characters.IsFormUnlocked(form.id)) changed |= Mark(CodexCategory.Friend, form.id);
                    }
                }
            }
            var costumes = _data?.unlockedCostumes;
            if (costumes != null)
            {
                foreach (var costumeId in costumes) changed |= Mark(CodexCategory.Costume, costumeId);
            }
            if (_inventory != null)
            {
                foreach (var pair in _byId[(int)CodexCategory.Material])
                {
                    if (_inventory.GetCount(pair.Key) > 0) changed |= MarkKey(pair.Value.Key);
                }
            }
            return changed;
        }

        /// <summary>그 종류의 그 ID를 찾음으로 (도감에 없는 ID면 false). 새로 찾았으면 true</summary>
        bool Mark(CodexCategory category, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureIndex();
            int index = (int)category;
            return index >= 0 && index < CategoryCount && _byId[index].TryGetValue(id, out var entry) && MarkKey(entry.Key);
        }

        bool MarkKey(string key)
        {
            var save = Save;
            if (save == null || string.IsNullOrEmpty(key) || !save.found.Add(key)) return false;
            _countsDirty = true;
            return true;
        }

        void AfterFoundChanged()
        {
            _countsDirty = true;
            _dots?.MarkDirty(RedDotPaths.Codex);
            CheckBonusChanged();
            NotifyChanged();
        }

        void AfterClaimsChanged()
        {
            _countsDirty = true;
            _dots?.MarkDirty(RedDotPaths.Codex);
            NotifyChanged();
        }

        /// <summary>펼쳐 본 동화책 장을 이야기 칸 "찾음"으로 (StorySystem.MarkSeen이 seenChapters에 적는다). 새로 찾았으면 true</summary>
        bool SyncStoryFound(CodexSave save)
        {
            EnsureIndex();
            var seen = _data?.meta?.story?.seenChapters;
            if (save == null || seen == null || seen.Count == 0) return false;
            bool changed = false;
            foreach (var pair in _byId[(int)CodexCategory.Story])
            {
                if (seen.Contains(pair.Value.Chapter)) changed |= MarkKey(pair.Value.Key);
            }
            return changed;
        }

        // ───────────────────────────── 보너스 ─────────────────────────────

        /// <summary>지금 수로 보너스 % (잠겨 있어도 계산만 — 더하는 쪽이 IsUnlocked를 본다)</summary>
        void ComputeBonus(out float form, out float shop, out float lamp)
        {
            var config = Config;
            form = TierPercent(config.formTiers, _found[(int)CodexCategory.Friend]);
            int enemyTotal = _total[(int)CodexCategory.Enemy];
            shop = enemyTotal > 0 && _found[(int)CodexCategory.Enemy] >= enemyTotal ? Math.Max(0f, config.allEnemiesShopHpPercent) : 0f;
            lamp = TierPercent(config.synergyTiers, _found[(int)CodexCategory.Synergy]);
        }

        /// <summary>count 이상인 단계 중 가장 높은 단계의 값 (그 단계의 합계 — 더하지 않음). 없으면 0</summary>
        static float TierPercent(List<CodexBonusTier> tiers, int count)
        {
            if (tiers == null) return 0f;
            int bestCount = 0;
            float best = 0f;
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier == null || tier.count <= 0 || count < tier.count || tier.count < bestCount) continue;
                bestCount = tier.count;
                best = tier.percent;
            }
            return best;
        }

        /// <summary>더해지는 보너스가 바뀌었으면 SynergySystem.MarkDirty (전투력 · 다음 전투가 다시 계산)</summary>
        void CheckBonusChanged()
        {
            Recount();
            float form = 0f, shop = 0f, lamp = 0f;
            if (IsUnlocked) ComputeBonus(out form, out shop, out lamp);
            if (form == _bonusForm && shop == _bonusShop && lamp == _bonusLamp) return;
            bool first = _bonusForm < 0f;
            _bonusForm = form;
            _bonusShop = shop;
            _bonusLamp = lamp;
            if (first && form == 0f && shop == 0f && lamp == 0f) return;
            try
            {
                _synergy?.MarkDirty();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Codex] 시너지 다시 계산 알림 실패: {e.Message}");
            }
        }

        // ───────────────────────────── 색인 · 수 ─────────────────────────────

        static bool IsFound(EntryInfo entry, CodexSave save) => save != null && save.found.Contains(entry.Key);

        void Recount()
        {
            EnsureIndex();
            bool unlocked = IsUnlocked;
            if (!_countsDirty && unlocked == _countedUnlocked) return;
            _countsDirty = false;
            _countedUnlocked = unlocked;
            Array.Clear(_found, 0, CategoryCount);
            Array.Clear(_total, 0, CategoryCount);
            Array.Clear(_claimable, 0, CategoryCount);
            _claimableTotal = 0;
            _foundTotal = 0;
            _entryTotal = 0;
            var save = Save;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                int c = (int)entry.Category;
                bool found = IsFound(entry, save);
                if (!entry.RewardOnly)
                {
                    _total[c]++;
                    _entryTotal++;
                    if (found)
                    {
                        _found[c]++;
                        _foundTotal++;
                    }
                }
                if (unlocked && found && entry.Gem > 0 && !save.claimed.Contains(entry.Key))
                {
                    _claimable[c]++;
                    _claimableTotal++;
                }
            }
        }

        void EnsureIndex()
        {
            if (_indexed) return;
            _indexed = true;
            var config = Config;
            var lineages = _characters?.Db != null ? _characters.Db.lineages : null;
            if (lineages != null)
            {
                foreach (var lineage in lineages)
                {
                    if (lineage?.forms == null) continue;
                    foreach (var form in lineage.forms)
                    {
                        if (form == null) continue;
                        int gem = form.tier <= 0 ? config.formBaseGem : form.tier == 1 ? config.formTier1Gem : config.formTier2Gem;
                        AddEntry(CodexCategory.Friend, form.id, gem, 0);
                    }
                }
                foreach (var lineage in lineages)
                {
                    if (lineage?.costumes == null) continue;
                    foreach (var costume in lineage.costumes)
                    {
                        if (costume != null) AddEntry(CodexCategory.Costume, costume.id, config.costumeGem, 0);
                    }
                }
            }
            var enemies = _battle?.Config != null ? _battle.Config.enemies : null;
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null) AddEntry(CodexCategory.Enemy, enemy.id, config.enemyGem, 0);
                }
            }
            var synergies = _synergy?.Db != null ? _synergy.Db.synergies : null;
            if (synergies != null)
            {
                foreach (var def in synergies)
                {
                    if (def == null || string.IsNullOrEmpty(def.id) || _synergyDefs.ContainsKey(def.id)) continue;
                    _synergyDefs[def.id] = def;
                    AddEntry(CodexCategory.Synergy, def.id, config.synergyFirstGem, 0);
                    AddRewardOnly(SynergyMaxKey(def.id), CodexCategory.Synergy, def.id, config.synergyMaxGem);
                }
            }
            var items = _inventory?.Db != null ? _inventory.Db.items : null;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item != null && item.category == ItemCategory.Material) AddEntry(CodexCategory.Material, item.id, config.materialGem, 0);
                }
            }
            var chapters = _db != null ? _db.storyChapters : null;
            if (chapters != null)
            {
                foreach (var chapter in chapters)
                {
                    if (chapter != null && chapter.chapter > 0)
                        AddEntry(CodexCategory.Story, chapter.chapter.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, chapter.chapter);
                }
            }
            if (config.hints != null)
            {
                foreach (var hint in config.hints)
                {
                    if (hint != null && !string.IsNullOrEmpty(hint.key) && !string.IsNullOrEmpty(hint.hint)) _hints[hint.key] = hint.hint;
                }
            }

            var save = Save;
            if (save != null)
            {
                foreach (var pair in _byId[(int)CodexCategory.Enemy])
                    if (save.found.Contains(pair.Value.Key)) _knownEnemies.Add(pair.Key);
                foreach (var pair in _byId[(int)CodexCategory.Synergy])
                    if (save.found.Contains(pair.Value.Key)) _knownSynergies.Add(pair.Key);
                foreach (var id in _synergyDefs.Keys)
                    if (save.found.Contains(SynergyMaxKey(id))) _knownSynergyMax.Add(id);
            }
            _countsDirty = true;
        }

        void AddEntry(CodexCategory category, string id, int gem, int chapter)
        {
            if (string.IsNullOrEmpty(id)) return;
            var byId = _byId[(int)category];
            if (byId.ContainsKey(id)) return;
            var entry = new EntryInfo { Key = Key(category, id), Category = category, Id = id, Gem = Math.Max(0, gem), Chapter = chapter };
            if (_byKey.ContainsKey(entry.Key)) return;
            byId[id] = entry;
            _byKey[entry.Key] = entry;
            _entries.Add(entry);
        }

        void AddRewardOnly(string key, CodexCategory category, string id, int gem)
        {
            if (string.IsNullOrEmpty(key) || _byKey.ContainsKey(key)) return;
            var entry = new EntryInfo { Key = key, Category = category, Id = id, Gem = Math.Max(0, gem), RewardOnly = true };
            _byKey[key] = entry;
            _entries.Add(entry);
        }

        static List<MetaReward> RewardOf(EntryInfo entry) => entry.Reward ??= new List<MetaReward> { MetaReward.Gem(entry.Gem) };

        List<MetaReward> Grant(List<MetaReward> rewards)
        {
            if (rewards == null || rewards.Count == 0) return new List<MetaReward>();
            if (_rewards == null) return new List<MetaReward>(rewards);
            return _rewards.Grant(rewards) ?? new List<MetaReward>();
        }

        /// <summary>못 찾은 칸 힌트: 콘텐츠(CodexConfig.hints) 먼저, 없으면 종류별 기본 문구 (해요체)</summary>
        string HintFor(EntryInfo entry)
        {
            if (_hints.TryGetValue(entry.Key, out var hint)) return hint;
            switch (entry.Category)
            {
                case CodexCategory.Friend:
                    var lineage = _characters?.Db?.GetLineageOfForm(entry.Id);
                    return lineage != null && _characters.IsOwned(lineage.id) ? "진화 기억도에서 단서를 찾아요" : "잃어버린 친구 찾기에서 만날 수 있어요";
                case CodexCategory.Costume:
                    return _costumes != null ? _costumes.GetProgressText(entry.Id) : string.Empty;
                case CodexCategory.Enemy:
                    var enemy = _battle?.Config != null ? _battle.Config.GetEnemy(entry.Id) : null;
                    return enemy != null ? $"스테이지 {BattleSystem.FormatStage(Math.Max(1, enemy.minStage))}부터 나타나요" : string.Empty;
                case CodexCategory.Synergy:
                    return SynergyHint(entry.Id);
                case CodexCategory.Material:
                    var item = _inventory?.Db != null ? _inventory.Db.Get(entry.Id) : null;
                    return item?.sources != null && item.sources.Count > 0 && item.sources[0] != null ? item.sources[0].label ?? string.Empty : string.Empty;
                case CodexCategory.Story:
                    return $"{entry.Chapter}챕터를 마치면 펼쳐져요";
                default:
                    return string.Empty;
            }
        }

        string SynergyHint(string synergyId)
        {
            if (!_synergyDefs.TryGetValue(synergyId, out var def)) return string.Empty;
            if (def.kind == SynergyKind.Combo && def.lineageIds != null && def.lineageIds.Count > 0)
            {
                var names = new List<string>();
                foreach (var id in def.lineageIds)
                {
                    var lineage = _characters?.Db?.GetLineage(id);
                    names.Add(lineage != null ? lineage.name : id);
                }
                return string.Join(" · ", names) + " 함께 세우면 켜져요";
            }
            return $"{def.name} 친구들을 함께 세우면 켜져요";
        }

        void NotifyChanged() => Changed?.Invoke();
    }
}
