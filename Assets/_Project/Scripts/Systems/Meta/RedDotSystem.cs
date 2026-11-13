using System;
using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>
    /// 빨간 점 경로. 점(.)으로 나눈 트리이고 부모는 자식 중 하나라도 켜지면 켜진다 ("tab.characters"는 "tab.characters.lineage.jjakjjak.evolution"을 포함).
    /// 규칙 (F-04): 누르면 무언가 받을 수 있을 때만 켠다 — ① 받을 보상 ② 새로 열렸는데 안 가 봄 ③ 무료 행동이 남음 ④ 드물게 준비되는 일.
    /// "단추로 강화할 수 있음"에는 켜지 않는다. 숫자는 우편과 받을 미션 개수에만 쓴다. 공지는 숫자 대신 점.
    /// </summary>
    public static class RedDotPaths
    {
        // ── 하단 탭 ──
        public const string TabGrowth = "tab.growth";
        public const string TabFormation = "tab.formation";
        public const string TabCharacters = "tab.characters";
        public const string TabExpedition = "tab.expedition";
        public const string TabGacha = "tab.gacha";
        public const string TabShop = "tab.shop";

        // ── 상단 바 ──
        /// <summary>수선 일지 (숫자 = 받을 미션 + 상자)</summary>
        public const string TopJournal = "top.journal";
        /// <summary>출석부 (오늘 도장)</summary>
        public const string TopAttendance = "top.attendance";
        /// <summary>우편 (숫자)</summary>
        public const string TopMail = "top.mail";
        /// <summary>설정 (공지 안 읽음이 여기로 올라온다)</summary>
        public const string TopSettings = "top.settings";
        public const string TopSettingsNotice = "top.settings.notice";
        public const string TopEvent = "top.event";

        // ── 전투 화면 ──
        /// <summary>수선 바구니 (1시간 이상 쌓임 / 가득). 무료 빠른 수선은 점이 아니라 위젯 "무료" 표 (IdleChestSystem.ShowsFreeTag)</summary>
        public const string HudIdleChest = "hud.idlechest";
        /// <summary>가이드 퀘스트 받기</summary>
        public const string HudGuideQuest = "hud.guidequest";
        public const string HudAuto = "hud.auto";
        public const string HudSpeed = "hud.speed";
        public const string HudLamp = "hud.lamp";
        public const string HudStage = "hud.stage";

        // ── 탭 아래 ──
        /// <summary>
        /// 편성 빈칸 (M16): 열린 지킴이·잔향이 칸이 비어 있고, 편성에 없는 보유 친구가 있음 (원정 중인 친구도 센다 — 원정 중에도 편성할 수 있다, M05).
        /// 편성·친구가 바뀔 때만 다시 본다 (MetaSession). 받을 것이 아니라 "할 일"이지만 전투력이 바로 오르는 빈칸이라 점으로 알린다.
        /// </summary>
        public const string FormationEmpty = TabFormation + ".empty";
        public const string ExpeditionDone = TabExpedition + ".done";
        public const string DungeonTickets = TabExpedition + ".dungeon";
        /// <summary>무료 소환 (결정: 등록하지 않는다 — 무료 소환이 없다, SPEC4 §2.7)</summary>
        public const string GachaFree = TabGacha + ".free";
        /// <summary>오늘의 작은 선물을 아직 안 받음 (NEW-02, DailyGiftSystem이 등록)</summary>
        public const string ShopFree = TabShop + ".free";

        // ── 4차 (SPEC4 §2.7, 모듈이 RegisterRedDots에서 등록) ──
        /// <summary>받을 수선점 기록이 있음 (일지 버튼은 미션 숫자를 그대로 쓰고, 숫자가 0이면 이 잎이 점으로 보인다)</summary>
        public const string JournalRecords = TopJournal + ".records";
        /// <summary>첫 주가 진행 중이고 받을 과제 · 상자가 있음 (JournalRecords처럼 미션 숫자가 0이면 점)</summary>
        public const string JournalStarter = TopJournal + ".starter";
        /// <summary>도감이 열렸고 받을 칸이 있음</summary>
        public const string Codex = TabCharacters + ".codex";
        /// <summary>다음 소환 마일스톤에 닿았는데 안 받음, 또는 친구 선택권이 있음</summary>
        public const string GachaMilestone = TabGacha + ".milestone";
        /// <summary>진행 중인 이벤트에 받을 미션이 있거나 오늘 도장을 안 찍음</summary>
        public const string EventClaim = TopEvent + ".claim";
        /// <summary>아직 열어 보지 않은 이벤트 회차가 있음 (모아 보기를 열면 꺼짐)</summary>
        public const string EventNew = TopEvent + ".new";
        /// <summary>닿았는데 안 받은 수첩 칸이 있음</summary>
        public const string Season = TopEvent + ".season";
        /// <summary>고를 잔향 축복이 남음</summary>
        public const string DungeonBlessing = DungeonTickets + ".blessing";

        /// <summary>"새로 열린 기능" 잎 경로의 표시 마디 — 이 마디가 든 잎은 받을 것이 아니다 (RedDotSystem.IsClaimOn이 건너뛴다)</summary>
        public const string FeatureSegment = ".feature.";

        /// <summary>"새로 열린 기능" 점 (기능이 속한 탭 밑에 둔다, 예: tab.expedition.feature.Expedition)</summary>
        public static string Feature(string tabPath, FeatureId feature) => $"{tabPath}{FeatureSegment}{feature}";

        /// <summary>기능의 "새로 열림" 점 경로 = Feature(ParentOf(기능), 기능). 부모가 없으면(전투) null</summary>
        public static string FeatureDot(FeatureId feature)
        {
            string parent = ParentOf(feature);
            return parent == null ? null : Feature(parent, feature);
        }

        /// <summary>기능이 속한 화면의 점 경로 (탭 / 상단 바 버튼 / 전투 HUD). 탭 잠금 표시도 이 표를 따른다</summary>
        public static string ParentOf(FeatureId feature) => feature switch
        {
            FeatureId.ShopUpgrade => TabGrowth,
            FeatureId.CharacterLevelUp => TabGrowth,
            FeatureId.Formation => TabFormation,
            FeatureId.Synergy => TabFormation,
            FeatureId.EchoSlot6 => TabFormation,
            FeatureId.Characters => TabCharacters,
            FeatureId.Evolution => TabCharacters,
            FeatureId.Codex => TabCharacters,
            FeatureId.Costumes => TabCharacters,
            FeatureId.Expedition => TabExpedition,
            FeatureId.Dungeon => TabExpedition,
            FeatureId.ExpeditionBatch => TabExpedition,
            FeatureId.DungeonBatchSweep => TabExpedition,
            FeatureId.Gacha => TabGacha,
            FeatureId.Shop => TabShop,
            FeatureId.Missions => TopJournal,
            FeatureId.Achievements => TopJournal,
            FeatureId.Attendance => TopAttendance,
            FeatureId.Events => TopEvent,
            FeatureId.IdleChest => HudIdleChest,
            FeatureId.QuickIdle => HudIdleChest,
            FeatureId.AutoBattle => HudAuto,
            FeatureId.Speed2x => HudSpeed,
            FeatureId.LampPolicy => HudLamp,
            FeatureId.AutoChallenge => HudStage,
            _ => null,
        };

        /// <summary>계보별 (진화 제작 가능 / 공명 가능 / 의상 해금 가능)</summary>
        public static string LineageEvolution(string lineageId) => $"{TabCharacters}.lineage.{lineageId}.evolution";
        public static string LineageResonance(string lineageId) => $"{TabCharacters}.lineage.{lineageId}.resonance";
        public static string LineageCostume(string lineageId) => $"{TabCharacters}.lineage.{lineageId}.costume";
        public static string Lineage(string lineageId) => $"{TabCharacters}.lineage.{lineageId}";
        /// <summary>계보에 받을 호감도 단계가 있음 (CR-10, 호감도 모듈이 등록)</summary>
        public static string LineageAffinity(string lineageId) => $"{TabCharacters}.lineage.{lineageId}.affinity";

        /// <summary>원정 구역 완료 (원정 탭 아래)</summary>
        public static string ExpeditionZone(string zoneId) => $"{ExpeditionDone}.{zoneId}";
    }

    /// <summary>
    /// 빨간 점 트리 (F-04). 잎(leaf) 경로마다 평가 함수를 등록하고, IsOn(경로)는 그 경로와 자식 전체를 합친다.
    /// 평가는 매 프레임이 아니라 MarkDirty된 뒤 Refresh(MetaSession.Tick이 프레임마다 부름, 깨끗하면 바로 반환)에서 한 번 한다.
    /// 시스템 Changed 이벤트를 받는 쪽(MetaSession.RegisterRedDots)이 MarkDirty를 부른다. 값이 바뀐 잎이 있으면 Changed.
    /// 뷰(RedDotView)는 Changed에서 IsOn / GetCount를 읽는다. 경로 문자열은 등록 때 한 번 만들고 다시 만들지 않는다.
    /// <para>
    /// 등록할 때 경로의 모든 조상 마디("a", "a.b", "a.b.c")에 잎을 붙여 두므로 IsOn / GetCount / MarkDirty(경로)는
    /// 사전 조회 한 번 + 그 마디의 잎 목록 훑기로 끝난다 (할당 없음). 평가 함수가 예외를 내면 로그를 남기고 이전 값을 유지한다.
    /// </para>
    /// </summary>
    public sealed class RedDotSystem
    {
        /// <summary>점 상태가 하나라도 바뀜 (Refresh에서)</summary>
        public event Action Changed;

        sealed class Leaf
        {
            public string Path;
            public Func<bool> IsOn;
            public Func<int> Count;
            /// <summary>함수 없이 Set으로만 켜고 끄는 잎</summary>
            public bool Manual;
            /// <summary>"새로 열림" 잎 (경로에 RedDotPaths.FeatureSegment) — 만들 때 한 번 정한다. IsClaimOn이 세지 않는다</summary>
            public bool IsFeature;
            public bool On;
            public int Value;
            public bool Dirty = true;
            /// <summary>이 잎을 품은 마디들 (자기 경로 포함) — 지울 때 떼어 낸다</summary>
            public readonly List<List<Leaf>> Nodes = new();
        }

        readonly Dictionary<string, Leaf> _leaves = new();
        readonly List<Leaf> _leafList = new();
        /// <summary>마디 경로 → 그 경로와 자식 잎</summary>
        readonly Dictionary<string, List<Leaf>> _nodes = new();

        bool _allDirty;
        bool _anyDirty;
        bool _changedPending;

        /// <summary>잎 경로에 켜짐/꺼짐 평가 함수를 등록한다 (같은 경로는 덮어씀)</summary>
        public void Register(string path, Func<bool> isOn)
        {
            var leaf = GetOrCreateLeaf(path);
            if (leaf == null) return;
            leaf.IsOn = isOn;
            leaf.Count = null;
            leaf.Manual = false;
            MarkLeafDirty(leaf);
        }

        /// <summary>잎 경로에 개수 평가 함수를 등록한다 (0이면 꺼짐). 숫자 배지(우편, 미션)용</summary>
        public void RegisterCount(string path, Func<int> count)
        {
            var leaf = GetOrCreateLeaf(path);
            if (leaf == null) return;
            leaf.IsOn = null;
            leaf.Count = count;
            leaf.Manual = false;
            MarkLeafDirty(leaf);
        }

        public void Unregister(string path)
        {
            if (string.IsNullOrEmpty(path) || !_leaves.TryGetValue(path, out var leaf)) return;
            _leaves.Remove(path);
            _leafList.Remove(leaf);
            foreach (var node in leaf.Nodes) node.Remove(leaf);
            leaf.Nodes.Clear();
            if (leaf.On || leaf.Value != 0) _changedPending = true;
        }

        /// <summary>함수 없이 직접 켜고 끄는 잎 (예: 새 공지)</summary>
        public void Set(string path, bool on)
        {
            var leaf = GetOrCreateLeaf(path);
            if (leaf == null) return;
            leaf.IsOn = null;
            leaf.Count = null;
            leaf.Manual = true;
            leaf.Dirty = false;
            if (leaf.On == on && leaf.Value == 0) return;
            leaf.On = on;
            leaf.Value = 0;
            _changedPending = true;
        }

        /// <summary>그 경로 또는 자식이 하나라도 켜짐</summary>
        public bool IsOn(string path)
        {
            if (path == null || !_nodes.TryGetValue(path, out var leaves)) return false;
            for (int i = 0; i < leaves.Count; i++)
            {
                if (leaves[i].On) return true;
            }
            return false;
        }

        /// <summary>
        /// 받을 것만: IsOn과 같지만 "새로 열림" 잎(RedDotPaths.Feature — 경로에 ".feature.")은 세지 않는다 (UX-SPEC-IA §5.9, CR-06 · CR-07).
        /// 사냥 버튼 모아 보기(RedDotAnyView) · 더보기 숫자가 쓴다 — 막 열린 빠른 아이콘의 NEW가 사냥 버튼에 점을 남기지 않게.
        /// NEW와 점 고르기(M09: 받을 자식이 없는 새로 열림 = NEW)는 IsOn(경로) &amp;&amp; !IsClaimOn(경로). 할당 없음.
        /// </summary>
        public bool IsClaimOn(string path)
        {
            if (path == null || !_nodes.TryGetValue(path, out var leaves)) return false;
            for (int i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                if (leaf.On && !leaf.IsFeature) return true;
            }
            return false;
        }

        /// <summary>그 경로와 자식의 개수 합 (개수 잎만 센다. 켜짐/꺼짐 잎은 0)</summary>
        public int GetCount(string path)
        {
            if (path == null || !_nodes.TryGetValue(path, out var leaves)) return 0;
            long sum = 0;
            for (int i = 0; i < leaves.Count; i++) sum += leaves[i].Value;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>모든 잎을 다시 평가하게 표시</summary>
        public void MarkDirty()
        {
            _allDirty = true;
            _anyDirty = true;
        }

        /// <summary>그 경로와 자식 잎만 다시 평가하게 표시</summary>
        public void MarkDirty(string path)
        {
            if (path == null || !_nodes.TryGetValue(path, out var leaves)) return;
            for (int i = 0; i < leaves.Count; i++) MarkLeafDirty(leaves[i]);
        }

        /// <summary>더러운 잎을 평가하고 바뀌었으면 Changed. 깨끗하면 할당 없이 바로 반환</summary>
        public void Refresh()
        {
            if (!_anyDirty && !_changedPending) return;
            bool changed = _changedPending;
            bool all = _allDirty;
            _changedPending = false;
            _allDirty = false;
            _anyDirty = false;

            for (int i = 0; i < _leafList.Count; i++)
            {
                var leaf = _leafList[i];
                if (!all && !leaf.Dirty) continue;
                leaf.Dirty = false;
                if (leaf.Manual) continue;
                if (Evaluate(leaf)) changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        /// <summary>등록된 잎 경로 (디버그용)</summary>
        public IReadOnlyCollection<string> Paths => _leaves.Keys;

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>잎을 평가해 값이 바뀌었으면 true</summary>
        static bool Evaluate(Leaf leaf)
        {
            bool on;
            int value;
            try
            {
                if (leaf.Count != null)
                {
                    value = Math.Max(0, leaf.Count());
                    on = value > 0;
                }
                else
                {
                    on = leaf.IsOn != null && leaf.IsOn();
                    value = 0;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(new InvalidOperationException($"[RedDot] '{leaf.Path}' 평가 실패: {e.Message}", e));
                return false;
            }
            if (on == leaf.On && value == leaf.Value) return false;
            leaf.On = on;
            leaf.Value = value;
            return true;
        }

        void MarkLeafDirty(Leaf leaf)
        {
            leaf.Dirty = true;
            _anyDirty = true;
        }

        Leaf GetOrCreateLeaf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_leaves.TryGetValue(path, out var leaf)) return leaf;

            leaf = new Leaf { Path = path, IsFeature = path.IndexOf(RedDotPaths.FeatureSegment, StringComparison.Ordinal) >= 0 };
            _leaves[path] = leaf;
            _leafList.Add(leaf);
            // 조상 마디마다 잎을 붙인다: "a.b.c" → "a", "a.b", "a.b.c"
            int start = 0;
            while (true)
            {
                int dot = path.IndexOf('.', start);
                string nodePath = dot < 0 ? path : path.Substring(0, dot);
                if (nodePath.Length > 0)
                {
                    if (!_nodes.TryGetValue(nodePath, out var node)) _nodes[nodePath] = node = new List<Leaf>();
                    if (!node.Contains(leaf))
                    {
                        node.Add(leaf);
                        leaf.Nodes.Add(node);
                    }
                }
                if (dot < 0) break;
                start = dot + 1;
            }
            return leaf;
        }
    }
}
