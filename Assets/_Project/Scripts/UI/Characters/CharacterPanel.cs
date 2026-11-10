using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 친구들 탭: 데이터베이스의 모든 계보 도감. 보유 계보가 먼저, 등급순/레벨순 정렬.
    /// 카드를 누르면 캐릭터 상세(진화 기억도)를 연다.
    /// 머리 줄(PanelHeader, M51): 도감 알약 · '진화 제작 가능 N' 칩 · 정렬 '⇅ 등급순 / ⇅ 레벨순' (보조 180x64). 격자는 Grid4 (M24).
    /// 시트가 열릴 때마다 패널이 켜지므로(UX-SPEC-IA CR-13) 켜질 때는 꺼져 있는 동안 무언가 바뀌었을 때만 목록을 다시 그린다.
    /// 알림 없이 바뀌는 조건 입력(최고 스테이지 · 던전 최고 층 · 야간 방문 — 진화 조건 StageReached · DungeonFloor, 의상 Stage · NightVisit)은
    /// 그릴 때 값을 적어 두고 켜질 때 · 켜져 있는 동안 1초마다 비교해, 달라졌으면 다시 그린다 (빨간 점 · '진화 제작 가능 N'이 낡지 않게).
    /// <para>
    /// 도감 입구 (Q-05, SPEC4 §6.2): 머리의 도감 알약이 버튼 CodexButton — 도감이 열리면(Codex 1-10) '도감 n%' + 처음 열렸을 때 NEW 알약,
    /// 누르면 UINav.OpenCodex() (봤음 처리). 열리기 전에는 예전처럼 '도감 보유/전체'이고 누르면 잠금 문장 토스트. 빨간 점(RedDotPaths.Codex)은 빌더가 붙인다.
    /// 카드의 전투력(PRG-01)은 PowerSystem.Changed에도 다시 그린다. 도감 · 호감도 · 기능 알림(Codex.Changed · Affinity.Changed · Features.Changed)도 묶어서 한 번.
    /// 새 직렬화 필드(_codexButton · _codexNew)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </para>
    /// </summary>
    public class CharacterPanel : MonoBehaviour
    {
        [SerializeField] RectTransform _gridContent;
        [SerializeField] CharacterCardView _cardPrefab;
        [SerializeField] TMP_Text _collectionText;
        [SerializeField] RectTransform _collectionFill;
        [SerializeField] Button _sortButton;
        [SerializeField] TMP_Text _sortLabel;
        [SerializeField] GameObject _alertChip;
        [SerializeField] TMP_Text _alertText;
        [Header("도감 입구 (Q-05 — 없어도 됨)")]
        [Tooltip("도감 알약 버튼 (누르면 도감 · 잠겼으면 안내)")]
        [SerializeField] Button _codexButton;
        [Tooltip("처음 열렸을 때 'NEW' 알약")]
        [SerializeField] GameObject _codexNew;

        UIListPool<CharacterCardView> _pool;
        readonly List<LineageDef> _sorted = new();
        bool _sortByLevel;
        bool _started;
        /// <summary>마지막으로 그린 뒤 계보 · 재료 · 의상 · 진화 · 기록이 바뀌었다 (꺼져 있는 동안 쌓아 두었다가 켜질 때 한 번)</summary>
        bool _dirty = true;
        bool _progressDirty;
        float _nextProgressRefresh;
        float _nextPassiveCheck;
        /// <summary>마지막으로 그릴 때의 알림 없는 입력 (최고 스테이지 · 던전 최고 층 · 야간 방문)</summary>
        int _seenStage = -1;
        int _seenFloor = -1;
        bool _seenNight;
        /// <summary>데이터 알림을 모아 LateUpdate에 한 번 (perf PF-6 H6 — 레벨업을 누르고 있으면 초당 20번 · 처치 보상마다 전체 다시 그리기였다)</summary>
        bool _dataDirty;
        float _nextDataRefresh;
        /// <summary>데이터 알림으로 다시 그리는 최소 간격 (초)</summary>
        const float DataRefreshInterval = 0.1f;
        /// <summary>정렬 비교 (한 번 만든 대리자 — 매번 람다 · 캡처를 만들지 않는다) · 데이터베이스 순서 (IndexOf 대신)</summary>
        System.Comparison<LineageDef> _compare;
        readonly Dictionary<LineageDef, int> _dbIndex = new();
        int _dbIndexCount = -1;

        void Start()
        {
            _pool = new UIListPool<CharacterCardView>(_cardPrefab, _gridContent);
            _sortButton.onClick.AddListener(ToggleSort);
            Game.Characters.Changed += OnDataChanged;
            Game.Inventory.Changed += OnDataChanged;
            Game.Costumes.Changed += OnDataChanged;
            Game.Evolution.FormUnlocked += OnFormUnlocked;
            Game.Characters.ProgressChanged += OnProgressChanged;
            if (_codexButton != null) _codexButton.onClick.AddListener(OnCodexClicked);
            if (Game.Power != null) Game.Power.Changed += OnPowerChanged;
            if (Game.Codex != null) Game.Codex.Changed += OnDataChanged;
            if (Game.Affinity != null) Game.Affinity.Changed += OnDataChanged;
            if (Game.Features != null) Game.Features.Changed += OnDataChanged;
            _started = true;
            Refresh();
        }

        void OnEnable()
        {
            if (_started && (_dirty || PassiveInputsChanged(true))) Refresh();
        }

        void OnDestroy()
        {
            if (!_started || !Game.IsReady) return;
            Game.Characters.Changed -= OnDataChanged;
            Game.Inventory.Changed -= OnDataChanged;
            Game.Costumes.Changed -= OnDataChanged;
            Game.Evolution.FormUnlocked -= OnFormUnlocked;
            Game.Characters.ProgressChanged -= OnProgressChanged;
            if (Game.Power != null) Game.Power.Changed -= OnPowerChanged;
            if (Game.Codex != null) Game.Codex.Changed -= OnDataChanged;
            if (Game.Affinity != null) Game.Affinity.Changed -= OnDataChanged;
            if (Game.Features != null) Game.Features.Changed -= OnDataChanged;
        }

        void OnDataChanged()
        {
            _dirty = true;
            _dataDirty = true;
        }

        void LateUpdate()
        {
            if (!_dataDirty || !_started) return;
            float now = Time.unscaledTime;
            if (now < _nextDataRefresh) return;
            _dataDirty = false;
            _nextDataRefresh = now + DataRefreshInterval;
            Refresh();
        }

        /// <summary>팀 전투력이 바뀜 (레벨업 · 공명 · 편성): 카드 전투력이 낡지 않게 — 켜져 있으면 1초 묶음으로, 꺼져 있으면 켜질 때</summary>
        void OnPowerChanged(long before, long after)
        {
            _dirty = true;
            _progressDirty = true;
        }

        void OnFormUnlocked(CharacterFormDef form) => OnDataChanged();

        /// <summary>전투 중 자주 오는 누적 기록 알림은 모아서 1초마다 한 번만 빨간 점을 갱신한다 (꺼져 있으면 켜질 때 한 번).</summary>
        void OnProgressChanged(string lineageId, ProgressType type)
        {
            _progressDirty = true;
            _dirty = true;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextPassiveCheck)
            {
                // 시트가 열린 채 뒤에서 전투가 스테이지를 넘겨도 알림이 맞게 (값 비교만 — 할당 없음, 시계는 켜질 때만 본다)
                _nextPassiveCheck = now + 1f;
                if (_started && PassiveInputsChanged(false)) _progressDirty = true;
            }
            if (!_progressDirty || now < _nextProgressRefresh) return;
            _progressDirty = false;
            _nextProgressRefresh = now + 1f;
            Refresh();
        }

        /// <summary>마지막으로 그린 뒤 알림 없는 입력이 바뀌었는지. withClock = 지금이 야간인지도 본다 (켜질 때만)</summary>
        bool PassiveInputsChanged(bool withClock)
        {
            if (!Game.IsReady) return false;
            if (CurrentStage() != _seenStage || CurrentFloor() != _seenFloor) return true;
            var costumes = Game.Costumes;
            if (costumes == null) return false;
            bool night = costumes.HasVisitedAtNight || (withClock ? costumes.IsNightNow : _seenNight);
            return night != _seenNight;
        }

        static int CurrentStage() => Game.Battle != null ? Game.Battle.HighestStage : 0;

        static int CurrentFloor() => Game.Dungeon != null ? Game.Dungeon.HighestFloor : 0;

        /// <summary>야간 방문 충족 (CostumeSystem과 같은 규칙: 기록된 방문 또는 지금이 야간)</summary>
        static bool CurrentNight()
        {
            var costumes = Game.Costumes;
            return costumes != null && (costumes.HasVisitedAtNight || costumes.IsNightNow);
        }

        void ToggleSort()
        {
            _sortByLevel = !_sortByLevel;
            OnDataChanged();
        }

        void Refresh()
        {
            if (!_started || !isActiveAndEnabled) return;
            _dirty = false;
            _seenStage = CurrentStage();
            _seenFloor = CurrentFloor();
            _seenNight = CurrentNight();

            BuildSortedList();
            _pool.SetCount(_sorted.Count);
            int owned = 0;
            int alerts = 0;
            for (int i = 0; i < _sorted.Count; i++)
            {
                var lineage = _sorted[i];
                _pool[i].Set(lineage, OpenDetail);
                if (!CharacterUI.IsOwned(lineage)) continue;
                owned++;
                if (CharacterUI.CanCraftAny(lineage)) alerts++;
            }

            var codex = MetaUi.IsActive ? Game.Codex : null;
            bool codexOpen = codex != null && codex.IsUnlocked;
            if (codexOpen)
            {
                float percent = Mathf.Clamp(codex.CompletionPercent, 0f, 100f);
                _collectionText.text = CodexLabel(percent);
                CharacterUI.SetFill(_collectionFill, percent / 100f);
            }
            else
            {
                _collectionText.text = $"도감 <b>{owned}</b><size=80%>/{_sorted.Count}</size>";
                CharacterUI.SetFill(_collectionFill, _sorted.Count > 0 ? (float)owned / _sorted.Count : 0f);
            }
            if (_codexNew != null)
            {
                bool isNew = codexOpen && Game.Features != null && Game.Features.IsNew(FeatureId.Codex);
                if (_codexNew.activeSelf != isNew) _codexNew.SetActive(isNew);
            }
            _sortLabel.text = _sortByLevel ? "⇅ 레벨순" : "⇅ 등급순";
            _alertChip.SetActive(alerts > 0);
            _alertText.text = $"진화 제작 가능 {alerts}";
        }

        void BuildSortedList()
        {
            _sorted.Clear();
            var all = Game.Characters.Db.lineages;
            foreach (var lineage in all)
            {
                if (lineage != null && !string.IsNullOrEmpty(lineage.id)) _sorted.Add(lineage);
            }
            if (_dbIndexCount != all.Count)
            {
                _dbIndex.Clear();
                for (int i = 0; i < all.Count; i++)
                {
                    var lineage = all[i];
                    if (lineage != null && !_dbIndex.ContainsKey(lineage)) _dbIndex[lineage] = i;
                }
                _dbIndexCount = all.Count;
            }
            _compare ??= CompareCards;
            _sorted.Sort(_compare);
        }

        /// <summary>보유 먼저 → (레벨순이면) 레벨 높은 순 → 등급 높은 순 → 데이터베이스 순서 (예전 람다와 같은 순서)</summary>
        int CompareCards(LineageDef a, LineageDef b)
        {
            bool ownedA = CharacterUI.IsOwned(a);
            bool ownedB = CharacterUI.IsOwned(b);
            if (ownedA != ownedB) return ownedA ? -1 : 1;
            if (_sortByLevel && ownedA)
            {
                int levelA = Game.Characters.GetLevel(a.id);
                int levelB = Game.Characters.GetLevel(b.id);
                if (levelA != levelB) return levelB.CompareTo(levelA);
            }
            if (a.grade != b.grade) return b.grade.CompareTo(a.grade);
            return DbIndex(a).CompareTo(DbIndex(b));
        }

        int DbIndex(LineageDef lineage) => _dbIndex.TryGetValue(lineage, out int index) ? index : int.MaxValue;

        static void OpenDetail(LineageDef lineage) => UIManager.Instance.Get<CharacterDetailPopup>().Show(lineage.id);

        /// <summary>도감 알약 글 (도감이 열린 뒤): '도감 n%' (내림 정수)</summary>
        internal static string CodexLabel(float percent) => $"도감 <b>{Mathf.FloorToInt(Mathf.Clamp(percent, 0f, 100f))}</b>%";

        /// <summary>도감 입구 (Q-05): 열렸으면 봤음 처리 후 도감, 아니면 잠금 문장 토스트 + 약한 진동</summary>
        void OnCodexClicked()
        {
            var codex = MetaUi.IsActive ? Game.Codex : null;
            if (codex == null || !codex.IsUnlocked)
            {
                CharacterUI.DenyLocked(FeatureId.Codex);
                return;
            }
            if (Game.Features != null) Game.Features.MarkSeen(FeatureId.Codex);
            UINav.OpenCodex();
        }
    }
}
