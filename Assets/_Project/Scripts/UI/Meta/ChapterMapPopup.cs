using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 챕터 지도 940×1300 (CR-05, SPEC4 §8.1 — 스테이지 패널을 누르면 MetaNavigator가 UINav.ChapterMapRequested로 연다, 튜토리얼 중에는 열지 않는다).
    ///   · 제목 '{n}장 · {장소}' (창 제목 자리) + 양옆 ‹ › 챕터 넘기기 (지금까지 간 챕터까지, 누르는 자리 88).
    ///   · 지도 그림 (Art/Story/map_bg — 없으면 밤 바탕, 880×1000에 덮어 맞춤) 위에 칸 10개 (MapNodeLayout — 정규화 자리, 96×96):
    ///     칸 이름 '{1-7}'은 칸 아래 밤 알약 위 20 굵게 (Cream · 지금 칸 Gold · 잠긴 칸 SubText — 모두 4.5:1 넘음, 판 아래 끝 + 24 위 — LabelLift).
    ///     깬 칸 = 불 켜진 가로등 · 지금 칸 = 불 켜짐 + 1.6초 숨 쉬는 빛 (허용된 반복 둘 중 하나) · 잠긴 칸 = 불 꺼짐 α0.55.
    ///     10번째(보스) 칸 = 보스 고리 + 보스 초상화 (잠겼으면 α0.85 + 자물쇠 — 흐리게 지우지 않는다), 그 옆 상자 표시 (누르면 '챕터 상자: …' 말풍선 —
    ///     보스를 깼는데 아직 안 연 상자면 그 자리에서 ChapterChestPopup을 연다: 상자 창이 메타 꺼짐 · 되돌림으로 받지 않고 닫혔거나 4차 이전 세이브).
    ///     기능이 그 칸 클리어로 열리면 작은 아이콘 40 (누르면 '여기서 열려요: {기능}').
    ///   · 깬 칸을 누르면 Battle.GoToStage + 토스트 '{1-7}을 다시 지켜요' 후 닫힘 (깬 칸에서 다시 지키는 중에 맨 앞 칸을 누르면 '{1-8}에 도전해요'),
    ///     지금 칸을 누르면 그냥 닫힘, 잠긴 칸은 토스트.
    ///   · 아래 '{n}장 진행 {k}/10' (Caption 22 굵게 Cream).
    /// 메타가 꺼져 있어도 전투 정보만으로 열린다 (동화책 · 상자 · 기능 아이콘만 빠진다). 열려 있는 동안 숨 쉬는 빛만 매 프레임 (할당 없음).
    /// 장면을 다시 만들기 전(G3)에는 이 팝업이 없다 — 그때 MetaNavigator는 ChapterMapRequested를 구독하지 않고 전투 화면은 예전 스테이지 목록을 쓴다 (UINav.HasChapterMap).
    /// </summary>
    public class ChapterMapPopup : UIPopup
    {
        /// <summary>챕터 하나의 칸 수</summary>
        public const int NodeCount = 10;
        /// <summary>숨 쉬는 빛 한 번 (초)</summary>
        public const float BreathSeconds = 1.6f;
        /// <summary>잠긴 칸 알파</summary>
        public const float LockedAlpha = 0.55f;
        /// <summary>잠긴 보스 칸 · 보스 초상 알파 (흐리게 지우지 않고 자물쇠를 얹는다 — r1: 0.55면 보스 표시가 거의 안 읽혔다)</summary>
        public const float BossLockedAlpha = 0.85f;
        /// <summary>칸 크기 (빌더 · 이름표 자리 셈)</summary>
        public const float NodeSize = 96f;
        /// <summary>칸 이름표 밤 알약 높이</summary>
        public const float LabelPillHeight = 34f;
        /// <summary>이름표 알약이 칸 아래 테두리와 겹치는 만큼</summary>
        public const float LabelOverlap = 4f;
        /// <summary>이름표 알약 아래 끝이 지도 판 안쪽 아래 끝에서 띄울 최소 거리</summary>
        public const float LabelMinBottom = 24f;

        /// <summary>
        /// 칸 자리 (지도 판 안의 정규화 좌표, 왼쪽 아래 = 0,0). 아래에서 위로 굽이치는 길, 10번째가 보스.
        /// StoryChapterDef에는 자리가 없어 모든 챕터가 이 표를 같이 쓴다 (SPEC4 §8.1). 빌더가 같은 표로 칸을 놓는다
        /// </summary>
        public static readonly Vector2[] MapNodeLayout =
        {
            new(0.22f, 0.10f), new(0.50f, 0.15f), new(0.78f, 0.21f), new(0.64f, 0.33f), new(0.34f, 0.38f),
            new(0.18f, 0.51f), new(0.42f, 0.60f), new(0.74f, 0.66f), new(0.62f, 0.79f), new(0.38f, 0.88f),
        };

        [Header("머리")]
        [SerializeField] TMP_Text _titleText;
        [SerializeField] Button _prevButton;
        [SerializeField] Button _nextButton;

        [Header("지도")]
        [SerializeField] Image _mapArt;
        [Tooltip("칸 버튼 10개 (MapNodeLayout 순서)")]
        [SerializeField] Button[] _nodeButtons = Array.Empty<Button>();
        [SerializeField] Image[] _nodeImages = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _nodeLabels = Array.Empty<TMP_Text>();
        [Tooltip("지금 칸 뒤에서 숨 쉬는 빛 (칸을 따라 옮긴다)")]
        [SerializeField] RectTransform _currentGlow;
        [Tooltip("보스 칸 안 보스 초상화 (없어도 됨)")]
        [SerializeField] Image _bossPortrait;
        [Tooltip("잠긴 보스 칸 위 자물쇠 (없어도 됨 — 장면을 다시 만들기 전)")]
        [SerializeField] GameObject _bossLock;
        [Tooltip("보스 칸 옆 챕터 상자 표시")]
        [SerializeField] Button _chestButton;
        [SerializeField] Image _chestImage;
        [Tooltip("칸마다 그 칸 클리어로 열리는 기능의 작은 아이콘 (칸의 자식, 칸 순서 — 둘 이상이면 첫 기능 그림, 말풍선에 모두)")]
        [SerializeField] Button[] _featureButtons = Array.Empty<Button>();
        [SerializeField] Image[] _featureIcons = Array.Empty<Image>();

        [Header("칸 그림 (빌더가 UiArt.MapNode로 넣는다)")]
        [SerializeField] Sprite _nodeOff;
        [SerializeField] Sprite _nodeOn;
        [SerializeField] Sprite _nodeBoss;
        [SerializeField] Sprite _nodeChest;
        [SerializeField] Sprite _chestOpen;

        [Header("아래")]
        [SerializeField] TMP_Text _footerText;

        int _chapter = 1;
        int _maxChapter = 1;
        int _currentNode = -1;
        float _breath;
        /// <summary>칸마다 열리는 첫 기능 (없으면 Battle) · 그 칸에서 열리는 기능 수</summary>
        readonly FeatureId[] _featureAt = new FeatureId[NodeCount];
        readonly int[] _featureCountAt = new int[NodeCount];
        int _featureCount;
        /// <summary>지도 상자 표시로 연 상자 창 (닫히면 상자 표시를 다시 그린다)</summary>
        ChapterChestPopup _chestPopup;
        Action _onChestPopupClosed;

        /// <summary>지금 보여 주는 챕터</summary>
        public int Chapter => _chapter;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < _nodeButtons.Length; i++)
            {
                int index = i;
                if (_nodeButtons[i] != null) _nodeButtons[i].onClick.AddListener(() => OnNode(index));
            }
            for (int i = 0; i < _featureButtons.Length; i++)
            {
                int index = i;
                if (_featureButtons[i] != null) _featureButtons[i].onClick.AddListener(() => OnFeatureIcon(index));
            }
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Flip(-1));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Flip(1));
            if (_chestButton != null) _chestButton.onClick.AddListener(OnChest);
            _onChestPopupClosed = OnChestPopupClosed;
        }

        /// <summary>
        /// 지도를 연다 (chapter 0 = 지금 스테이지의 챕터). 튜토리얼 중이거나 전투가 없으면 열지 않는다 (false).
        /// 챕터는 1 ~ 지금까지 간 챕터로 맞춘다
        /// </summary>
        public bool Show(int chapter)
        {
            if (!CanOpen()) return false;
            var battle = Game.Battle;
            _maxChapter = ChapterOf(battle.HighestStage);
            _chapter = Mathf.Clamp(chapter <= 0 ? ChapterOf(battle.Stage) : chapter, 1, _maxChapter);
            Open();
            return true;
        }

        /// <summary>지도를 열 수 있는지: 게임 준비 + 전투 있음 + 튜토리얼 밖 (M13 규칙)</summary>
        public static bool CanOpen()
        {
            if (!Game.IsReady || Game.Battle == null) return false;
            var tutorial = Game.Tutorial;
            return tutorial == null || !tutorial.IsActive;
        }

        /// <summary>스테이지 번호의 챕터 (10스테이지 = 1챕터)</summary>
        public static int ChapterOf(int stage) => (Math.Max(1, stage) - 1) / NodeCount + 1;

        /// <summary>챕터 chapter의 k번째(1~10) 칸 스테이지</summary>
        public static int StageOf(int chapter, int node) => (Math.Max(1, chapter) - 1) * NodeCount + Mathf.Clamp(node, 1, NodeCount);

        /// <summary>칸 상태 (순수): 깸 = stage &lt; highest, 열림(도전 중) = stage == highest, 지금 = 친구들이 있는 스테이지</summary>
        public enum NodeState
        {
            Locked = 0,
            Cleared = 1,
            Frontier = 2,
        }

        /// <summary>칸 상태 (순수 — 테스트용). highest = BattleSystem.HighestStage (아직 깨지 않은 맨 앞 스테이지)</summary>
        public static NodeState StateOf(int stage, int highest) =>
            stage < highest ? NodeState.Cleared : stage == highest ? NodeState.Frontier : NodeState.Locked;

        /// <summary>칸을 눌러 그 스테이지로 갈 때의 토스트 (순수): 깬 칸 '{1-7}을 다시 지켜요', 도전 중인 맨 앞 칸 '{1-8}에 도전해요'</summary>
        public static string NodeToast(int stage, NodeState state) =>
            state == NodeState.Cleared
                ? $"{MetaUi.WithObject(BattleSystem.FormatStage(stage))} 다시 지켜요"
                : $"{BattleSystem.FormatStage(stage)}에 도전해요";

        /// <summary>지도 상자 표시가 상자 창을 열지 (순수): 그 장 보스(x-10)를 깼고 상자를 아직 열지 않음</summary>
        public static bool ChestOpensFromMap(int chapter, int highest, bool opened) =>
            !opened && StateOf(StageOf(chapter, NodeCount), highest) == NodeState.Cleared;

        /// <summary>
        /// 칸 이름표 알약의 위 끝 자리 (순수, 칸 아래 끝에서 위로 +): 보통 LabelOverlap (칸 아래 테두리에 조금 겹침),
        /// 알약 아래 끝이 지도 판 안쪽 아래 끝 + LabelMinBottom보다 내려가면 그만큼 올린다 (1번 칸 — r1: '2-1'이 판 테두리에 걸렸다).
        /// nodesHeight = 칸을 놓는 판 높이 (빌더: 지도 1000 − 안쪽 여백 12)
        /// </summary>
        public static float LabelLift(int node, float nodesHeight)
        {
            if (node < 0 || node >= MapNodeLayout.Length) return LabelOverlap;
            float bottom = MapNodeLayout[node].y * nodesHeight - NodeSize * 0.5f + LabelOverlap - LabelPillHeight;
            return LabelOverlap + Mathf.Max(0f, LabelMinBottom - bottom);
        }

        /// <summary>칸 알파 (순수): 잠긴 칸 LockedAlpha, 잠긴 보스 칸 BossLockedAlpha (자물쇠와 함께), 그 밖 1</summary>
        public static float NodeAlpha(NodeState state, bool boss) =>
            state != NodeState.Locked ? 1f : boss ? BossLockedAlpha : LockedAlpha;

        /// <summary>칸 이름표 글자 색 (순수, 밤 알약 위 — 모두 4.5:1 넘음): 지금 칸 Gold, 잠긴 칸 SubText, 그 밖 Cream</summary>
        public static Color LabelColor(NodeState state, bool current) =>
            current ? UITheme.Gold : state == NodeState.Locked ? UITheme.SubText : UITheme.Cream;

        /// <summary>그 챕터에서 깬 칸 수 (0~10, 순수)</summary>
        public static int ClearedCount(int chapter, int highest)
        {
            int first = StageOf(chapter, 1);
            return Mathf.Clamp(highest - first, 0, NodeCount);
        }

        protected override void OnOpen()
        {
            _breath = 0f;
            Refresh();
        }

        void Flip(int delta)
        {
            int next = Mathf.Clamp(_chapter + delta, 1, _maxChapter);
            if (next == _chapter) return;
            _chapter = next;
            UISfx.Play(UISfxKey.Tab);
            Refresh();
        }

        void Refresh()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            var battle = Game.Battle;
            int highest = battle.HighestStage;
            _maxChapter = ChapterOf(highest);
            int now = battle.Stage;
            var story = MetaUi.IsActive ? Game.Story : null;
            var def = story?.Get(_chapter);
            if (_titleText != null)
            {
                _titleText.text = def != null && !string.IsNullOrEmpty(def.place) ? $"{_chapter}장 · {def.place}" : $"{_chapter}장";
            }
            if (_prevButton != null) _prevButton.gameObject.SetActive(_chapter > 1);
            if (_nextButton != null) _nextButton.gameObject.SetActive(_chapter < _maxChapter);

            _currentNode = -1;
            int count = Mathf.Min(NodeCount, _nodeButtons.Length);
            for (int i = 0; i < count; i++)
            {
                int stage = StageOf(_chapter, i + 1);
                var state = StateOf(stage, highest);
                bool boss = i == NodeCount - 1;
                bool current = stage == now && state != NodeState.Locked;
                if (current) _currentNode = i;
                var image = i < _nodeImages.Length ? _nodeImages[i] : null;
                if (image != null)
                {
                    var sprite = boss ? _nodeBoss : state == NodeState.Locked ? _nodeOff : _nodeOn;
                    if (sprite != null) image.sprite = sprite;
                    image.color = new Color(1f, 1f, 1f, NodeAlpha(state, boss));
                }
                var label = i < _nodeLabels.Length ? _nodeLabels[i] : null;
                if (label != null)
                {
                    label.text = BattleSystem.FormatStage(stage);
                    label.color = LabelColor(state, current);
                }
            }
            RefreshGlow();
            RefreshBoss(highest);
            RefreshFeatures();
            if (_footerText != null) _footerText.text = $"{_chapter}장 진행 {ClearedCount(_chapter, highest)}/{NodeCount}";
        }

        void RefreshGlow()
        {
            if (_currentGlow == null) return;
            bool show = _currentNode >= 0 && _currentNode < _nodeButtons.Length && _nodeButtons[_currentNode] != null;
            _currentGlow.gameObject.SetActive(show);
            if (!show) return;
            var node = (RectTransform)_nodeButtons[_currentNode].transform;
            _currentGlow.anchorMin = node.anchorMin;
            _currentGlow.anchorMax = node.anchorMax;
            _currentGlow.anchoredPosition = node.anchoredPosition;
            _currentGlow.localScale = Vector3.one;
        }

        void RefreshBoss(int highest)
        {
            int bossStage = StageOf(_chapter, NodeCount);
            var bossState = StateOf(bossStage, highest);
            if (_bossPortrait != null)
            {
                var boss = BossFor(bossStage);
                _bossPortrait.sprite = boss != null ? boss.sprite : null;
                _bossPortrait.enabled = _bossPortrait.sprite != null;
                _bossPortrait.color = new Color(1f, 1f, 1f, NodeAlpha(bossState, true));
            }
            bool bossLocked = bossState == NodeState.Locked;
            if (_bossLock != null && _bossLock.activeSelf != bossLocked) _bossLock.SetActive(bossLocked);
            if (_chestButton == null) return;
            var chests = MetaUi.IsActive ? Game.ChapterChests : null;
            var contents = chests?.PeekChapterChest(_chapter);
            bool show = contents != null && contents.Count > 0;
            _chestButton.gameObject.SetActive(show);
            if (!show || _chestImage == null) return;
            bool opened = chests.IsChapterChestOpened(_chapter);
            var sprite = opened && _chestOpen != null ? _chestOpen : _nodeChest;
            if (sprite != null) _chestImage.sprite = sprite;
            _chestImage.color = opened ? new Color(1f, 1f, 1f, 0.7f) : Color.white;
        }

        /// <summary>그 스테이지에 나올 수 있는 보스 중 가장 늦게 나오는 보스 (지도 초상화용, 없으면 null). 열 때만 부른다</summary>
        static EnemyDef BossFor(int stage)
        {
            var config = Game.Battle != null ? Game.Battle.Config : null;
            if (config == null || config.enemies == null) return null;
            EnemyDef best = null;
            foreach (var def in config.enemies)
            {
                if (def == null || !def.isBoss || def.minStage > stage || def.sprite == null) continue;
                if (best == null || def.minStage > best.minStage) best = def;
            }
            return best;
        }

        void RefreshFeatures()
        {
            _featureCount = 0;
            for (int i = 0; i < NodeCount; i++)
            {
                _featureAt[i] = FeatureId.Battle;
                _featureCountAt[i] = 0;
            }
            var db = MetaUi.IsActive ? Game.MetaDb : null;
            if (db != null && db.features != null)
            {
                int first = StageOf(_chapter, 1);
                int last = StageOf(_chapter, NodeCount);
                foreach (var def in db.features)
                {
                    if (def == null || def.icon == null) continue;
                    int stage = StageCondition(def);
                    if (stage < first || stage > last) continue;
                    int node = stage - first;
                    if (_featureCountAt[node] == 0)
                    {
                        _featureAt[node] = def.feature;
                        _featureCount++;
                    }
                    _featureCountAt[node]++;
                }
            }
            for (int i = 0; i < _featureButtons.Length && i < NodeCount; i++)
            {
                var button = _featureButtons[i];
                if (button == null) continue;
                bool used = _featureCountAt[i] > 0;
                if (button.gameObject.activeSelf != used) button.gameObject.SetActive(used);
                if (!used) continue;
                var icon = i < _featureIcons.Length ? _featureIcons[i] : null;
                var def = Game.Features != null ? Game.Features.GetDef(_featureAt[i]) : null;
                if (icon != null && def != null && def.icon != null) icon.sprite = def.icon;
            }
        }

        /// <summary>기능의 '스테이지 클리어' 조건 스테이지 (없으면 0)</summary>
        static int StageCondition(FeatureUnlockDef def)
        {
            if (def.conditions == null) return 0;
            foreach (var condition in def.conditions)
            {
                if (condition != null && condition.type == MetaConditionType.StageCleared && condition.target > 0) return (int)Math.Min(int.MaxValue, condition.target);
            }
            return 0;
        }

        void Update()
        {
            if (!IsOpen || _currentGlow == null || !_currentGlow.gameObject.activeSelf) return;
            if (UIMotion.Reduced)
            {
                if (_currentGlow.localScale != Vector3.one) _currentGlow.localScale = Vector3.one;
                return;
            }
            _breath += Time.unscaledDeltaTime;
            float k = 0.5f - 0.5f * Mathf.Cos(_breath * Mathf.PI * 2f / BreathSeconds);
            _currentGlow.localScale = Vector3.one * (0.92f + 0.16f * k);
        }

        void OnNode(int index)
        {
            if (!Game.IsReady || Game.Battle == null) return;
            var battle = Game.Battle;
            int stage = StageOf(_chapter, index + 1);
            var state = StateOf(stage, battle.HighestStage);
            UISfx.Play(UISfxKey.MapNode);
            if (state == NodeState.Locked)
            {
                MetaUi.Toast($"{MetaUi.WithObject(BattleSystem.FormatStage(stage - 1))} 클리어하면 열려요");
                return;
            }
            if (stage == battle.Stage)
            {
                Close();
                return;
            }
            battle.GoToStage(stage);
            Close();
            MetaUi.Toast(NodeToast(stage, state));
        }

        void OnChest()
        {
            var chests = MetaUi.IsActive ? Game.ChapterChests : null;
            var contents = chests?.PeekChapterChest(_chapter);
            if (contents == null || contents.Count == 0 || _chestButton == null) return;
            bool opened = chests.IsChapterChestOpened(_chapter);
            bool beaten = Game.Battle != null && ChestOpensFromMap(_chapter, Game.Battle.HighestStage, opened);
            // 보스는 깼는데 아직 안 연 상자 → 상자 창을 지도 위에 연다 (받으면 상자 표시를 다시 그린다)
            if (beaten && OpenChestPopup()) return;
            string body = opened ? "이미 받은 상자예요" : beaten ? $"{_chapter}장 보스를 물리치고 찾은 상자예요" : $"{_chapter}장 보스를 처음 물리치면 받아요";
            if (!InfoTooltipView.Show((RectTransform)_chestButton.transform, $"챕터 상자: {MetaUi.DescribeData(contents, " · ")}", body))
            {
                MetaUi.Toast($"챕터 상자: {MetaUi.DescribeData(contents, " · ")}");
            }
        }

        /// <summary>그 장 상자 창을 연다 (장면에 창이 없거나 띄울 수 없으면 false)</summary>
        bool OpenChestPopup()
        {
            var ui = UIManager.Instance;
            if (ui == null || !ui.TryGet<ChapterChestPopup>(out var popup) || popup == null || !popup.Show(_chapter)) return false;
            if (_chestPopup != null) _chestPopup.Closed -= _onChestPopupClosed;
            _chestPopup = popup;
            popup.Closed += _onChestPopupClosed;
            return true;
        }

        void OnChestPopupClosed()
        {
            if (_chestPopup != null) _chestPopup.Closed -= _onChestPopupClosed;
            _chestPopup = null;
            if (IsOpen) Refresh();
        }

        /// <summary>기능 아이콘을 누름: '여기서 열려요: {기능}' (그 칸에서 열리는 기능을 모두 ' · '로). 누를 때만 글을 만든다</summary>
        void OnFeatureIcon(int node)
        {
            if (node < 0 || node >= NodeCount || _featureCountAt[node] <= 0 || node >= _featureButtons.Length || _featureButtons[node] == null) return;
            var names = new System.Text.StringBuilder(32);
            var db = MetaUi.IsActive ? Game.MetaDb : null;
            int stage = StageOf(_chapter, node + 1);
            if (db != null && db.features != null)
            {
                foreach (var def in db.features)
                {
                    if (def == null || def.icon == null || StageCondition(def) != stage) continue;
                    if (names.Length > 0) names.Append(" · ");
                    names.Append(MetaText.FeatureName(def.feature));
                }
            }
            if (names.Length == 0) names.Append(MetaText.FeatureName(_featureAt[node]));
            string text = $"여기서 열려요: {names}";
            if (!InfoTooltipView.Show((RectTransform)_featureButtons[node].transform, text, null)) MetaUi.Toast(text);
        }

        /// <summary>테스트 · 도구: 지금 숨 쉬는 빛이 붙은 칸 (−1 = 없음)</summary>
        public int CurrentNode => _currentNode;

        /// <summary>테스트 · 도구: 지금 보이는 기능 아이콘 수</summary>
        public int FeatureIconCount => _featureCount;

        /// <summary>그 칸들에 놓일 기능들 (테스트 · 도구, 순수): 기능 정의 중 '스테이지 클리어' 조건이 챕터 안에 있는 것</summary>
        public static int FeaturesInChapter(IReadOnlyList<FeatureUnlockDef> features, int chapter, List<FeatureId> into)
        {
            into?.Clear();
            if (features == null) return 0;
            int first = StageOf(chapter, 1);
            int last = StageOf(chapter, NodeCount);
            int count = 0;
            foreach (var def in features)
            {
                if (def == null) continue;
                int stage = StageCondition(def);
                if (stage < first || stage > last) continue;
                into?.Add(def.feature);
                count++;
            }
            return count;
        }
    }
}
