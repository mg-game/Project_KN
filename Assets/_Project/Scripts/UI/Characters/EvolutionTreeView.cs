using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 기억도: 기본형을 맨 위에 두고 1차/2차 진화형을 부모 아래에 펼친 나무 그림.
    /// 지킴이/잔향이 필터로 추천 형태를 강조하고, 노드를 누르면 상태에 맞는 화면을 연다.
    /// (해금 → 형태 정보, 단서 발견 → 진화 레시피, 미발견 → 단서 조건 안내)
    /// <para>
    /// 아래 버튼 '진화 레시피 보기 ›' (보조 버튼, M54 · M75) — 단서 형태(제작 가능한 것 먼저)의 레시피를 연다:
    ///   · 미보유 계보: '진화 확인' (누를 수 없음)
    ///   · 제작 가능: '{단서} 진화 제작! 준비 완료' Ink + 금 덮개(ReadyArt) + 뒤 금빛
    ///   · 단서는 있지만 아직: '다음 진화 · {단서} 재료 k/n' Cream (재료가 다 모였으면 남은 조건 '조건 k/n')
    ///   · 단서 없음: '진화 단서 찾는 중' SubText (누르면 토스트로 단서 찾는 법)
    ///   · 진화 제작이 잠김 (F-03, 1-10 전): '스테이지 1-10 클리어 시 열려요' Cream — 나무는 그대로 볼 수 있고(가이드 23), 누르면 레시피를 볼 수 있다
    ///     (레시피의 제작 버튼이 잠금을 보여 준다). 잠긴 동안은 '준비 완료' 금 덮개 · 빛을 켜지 않는다.
    /// </para>
    /// 나무 선 · 미발견 고리는 글자가 아닌 선이라 InkLine (M29).
    /// </summary>
    public class EvolutionTreeView : MonoBehaviour
    {
        /// <summary>노드 프리팹 높이 (원 + 이름 + 상태 칩)</summary>
        const float NodeHeight = 180f;
        const float LineThickness = 4f;

        [SerializeField] RectTransform _area;
        [SerializeField] RectTransform _lineLayer;
        [SerializeField] Image _lineTemplate;
        [SerializeField] EvolutionNodeView _nodePrefab;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] Button[] _filterButtons;
        [SerializeField] TMP_Text[] _filterLabels;
        [SerializeField] Button _checkButton;
        [SerializeField] TMP_Text _checkLabel;
        [SerializeField] GameObject _checkGlow;
        [Tooltip("'준비 완료' 금 덮개 (확인 버튼의 맨 뒤 자식 — 제작 가능할 때만)")]
        [SerializeField] GameObject _checkReadyArt;
        [SerializeField] float _maxColumnWidth = 190f;
        [SerializeField] float _maxRowStep = 260f;

        struct Segment
        {
            public Vector2 From;
            public Vector2 To;
            public Color Color;
        }

        UIListPool<EvolutionNodeView> _nodes;
        UIListPool<Image> _lines;
        LineageDef _lineage;
        int _filter = -1;
        bool _initialized;

        readonly List<CharacterFormDef> _order = new();
        readonly Dictionary<string, List<CharacterFormDef>> _children = new();
        readonly Dictionary<string, int> _slots = new();
        readonly Dictionary<string, Vector2> _positions = new();
        readonly Dictionary<string, EvolutionState> _states = new();
        readonly List<Segment> _segments = new();

        void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            _lineTemplate.gameObject.SetActive(false);
            _nodes = new UIListPool<EvolutionNodeView>(_nodePrefab, _area);
            _lines = new UIListPool<Image>(_lineTemplate, _lineLayer);
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                int filter = i - 1;
                _filterButtons[i].onClick.AddListener(() => SetFilter(filter));
            }
            _checkButton.onClick.AddListener(OpenFirstClue);
        }

        public void Show(LineageDef lineage)
        {
            EnsureInit();
            if (lineage != _lineage)
            {
                _filter = -1;
                _lineage = lineage;
                BuildStructure();
            }
            Refresh();
        }

        /// <summary>새로 해금된 형태 노드를 반짝인다</summary>
        public void PlayUnlock(string formId)
        {
            if (_nodes == null) return;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Form != null && _nodes[i].Form.id == formId) _nodes[i].PlayUnlock();
            }
        }

        void Refresh()
        {
            if (_lineage == null) return;
            ReadStates();
            Layout();
            RefreshNodes();
            RefreshLines();
            RefreshFilterTabs();
            RefreshFooter();
        }

        // ───────────── 구조 / 배치 ─────────────

        /// <summary>기본형에서 닿는 형태만 계층 순서로 모으고, 자식 수로 가로 칸을 나눈다.</summary>
        void BuildStructure()
        {
            _order.Clear();
            _children.Clear();
            _slots.Clear();

            var root = CharacterUI.BaseForm(_lineage);
            if (root == null) return;
            foreach (var form in _lineage.forms)
            {
                if (form == null || string.IsNullOrEmpty(form.parentFormId)) continue;
                if (!_children.TryGetValue(form.parentFormId, out var list)) _children[form.parentFormId] = list = new List<CharacterFormDef>();
                list.Add(form);
            }
            CountSlots(root, 0);
            _order.Sort((a, b) => a.tier != b.tier ? a.tier.CompareTo(b.tier) : _lineage.forms.IndexOf(a).CompareTo(_lineage.forms.IndexOf(b)));
        }

        int CountSlots(CharacterFormDef form, int depth)
        {
            _order.Add(form);
            int sum = 0;
            if (depth < 8 && _children.TryGetValue(form.id, out var children))
            {
                foreach (var child in children) sum += CountSlots(child, depth + 1);
            }
            int slots = Mathf.Max(1, sum);
            _slots[form.id] = slots;
            return slots;
        }

        void Layout()
        {
            _positions.Clear();
            var root = CharacterUI.BaseForm(_lineage);
            if (root == null || !_slots.TryGetValue(root.id, out int totalSlots)) return;

            var rect = _area.rect;
            int depth = 0;
            foreach (var form in _order) depth = Mathf.Max(depth, Depth(form));

            float column = Mathf.Min(_maxColumnWidth, rect.width / Mathf.Max(1, totalSlots));
            float rowStep = depth > 0 ? Mathf.Min(_maxRowStep, (rect.height - NodeHeight) / depth) : 0f;
            float treeHeight = depth * rowStep + NodeHeight;
            float top = Mathf.Max(0f, (rect.height - treeHeight) * 0.5f);
            Place(root, -totalSlots * column * 0.5f, column, rowStep, top, 0);
        }

        void Place(CharacterFormDef form, float left, float column, float rowStep, float top, int depth)
        {
            float width = _slots[form.id] * column;
            _positions[form.id] = new Vector2(left + width * 0.5f, -top - depth * rowStep);
            if (!_children.TryGetValue(form.id, out var children)) return;
            float x = left;
            foreach (var child in children)
            {
                if (!_slots.ContainsKey(child.id)) continue;
                Place(child, x, column, rowStep, top, depth + 1);
                x += _slots[child.id] * column;
            }
        }

        int Depth(CharacterFormDef form)
        {
            int depth = 0;
            var db = Game.Characters.Db;
            for (var current = form; current != null && !string.IsNullOrEmpty(current.parentFormId) && depth < 8; depth++)
            {
                current = db.GetForm(current.parentFormId);
            }
            return depth;
        }

        // ───────────── 노드 / 선 ─────────────

        void ReadStates()
        {
            _states.Clear();
            foreach (var form in _order) _states[form.id] = Game.Evolution.GetState(form.id);
        }

        void RefreshNodes()
        {
            _nodes.SetCount(_order.Count);
            for (int i = 0; i < _order.Count; i++)
            {
                var form = _order[i];
                var node = _nodes[i];
                var rt = (RectTransform)node.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = _positions.TryGetValue(form.id, out var position) ? position : Vector2.zero;

                var state = _states[form.id];
                bool craftable = state == EvolutionState.ClueFound && CharacterUI.IsCraftable(form.id);
                node.Set(form, _lineage, state, craftable, OnNodeClicked);
                node.SetFilter(_filter);
            }
        }

        void RefreshLines()
        {
            _segments.Clear();
            foreach (var form in _order)
            {
                if (!_children.TryGetValue(form.id, out var children) || !_positions.TryGetValue(form.id, out var parent)) continue;
                AddConnector(parent, children);
            }

            _lines.SetCount(_segments.Count);
            for (int i = 0; i < _segments.Count; i++) ApplySegment(_lines[i], _segments[i]);
        }

        /// <summary>부모 아래 → 가운데 가로줄 → 각 자식 위로 꺾인 연결선</summary>
        void AddConnector(Vector2 parent, List<CharacterFormDef> children)
        {
            float parentBottom = parent.y - NodeHeight + 6f;
            float childTop = float.MinValue;
            float minX = parent.x;
            float maxX = parent.x;
            var best = EvolutionState.Undiscovered;
            int placed = 0;
            foreach (var child in children)
            {
                if (!_positions.TryGetValue(child.id, out var position)) continue;
                childTop = Mathf.Max(childTop, position.y);
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                if (_states[child.id] > best) best = _states[child.id];
                placed++;
            }
            if (placed == 0) return;

            float mid = (parentBottom + childTop) * 0.5f;
            var trunk = LineColor(best);
            _segments.Add(new Segment { From = new Vector2(parent.x, parentBottom), To = new Vector2(parent.x, mid), Color = trunk });
            if (maxX - minX > 1f) _segments.Add(new Segment { From = new Vector2(minX, mid), To = new Vector2(maxX, mid), Color = trunk });
            foreach (var child in children)
            {
                if (!_positions.TryGetValue(child.id, out var position)) continue;
                _segments.Add(new Segment { From = new Vector2(position.x, mid), To = new Vector2(position.x, position.y - 4f), Color = LineColor(_states[child.id]) });
            }
        }

        static Color LineColor(EvolutionState state) => state switch
        {
            EvolutionState.Unlocked => UITheme.GoldAccent,
            EvolutionState.ClueFound => Color.Lerp(UITheme.Lavender, UITheme.Primary, 0.35f),
            _ => CharacterUI.WithAlpha(UITheme.InkLine, 0.4f),
        };

        static void ApplySegment(Image line, Segment segment)
        {
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (segment.From + segment.To) * 0.5f;
            rt.sizeDelta = new Vector2(
                Mathf.Abs(segment.To.x - segment.From.x) + LineThickness,
                Mathf.Abs(segment.To.y - segment.From.y) + LineThickness);
            line.color = segment.Color;
        }

        // ───────────── 필터 / 하단 ─────────────

        void SetFilter(int filter)
        {
            _filter = _filter == filter ? -1 : filter;
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].SetFilter(_filter);
            RefreshFilterTabs();
        }

        void RefreshFilterTabs()
        {
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                bool selected = i - 1 == _filter;
                // '전체'는 글자 있는 보라 칠(PrimaryButton — 흰 글자 5:1), 역할은 역할 색 + Ink
                var color = i == 0 ? UITheme.PrimaryButton : CharacterUI.RoleColor((CharacterRole)(i - 1));
                _filterButtons[i].targetGraphic.color = selected ? color : UITheme.PanelLight;
                _filterLabels[i].color = selected ? (i == 0 ? Color.white : UITheme.Ink) : UITheme.SubText;
            }
        }

        void RefreshFooter()
        {
            bool owned = CharacterUI.IsOwned(_lineage);
            int unlocked = 0;
            int clues = 0;
            foreach (var form in _order)
            {
                var state = _states[form.id];
                if (state == EvolutionState.Unlocked) unlocked++;
                else if (state == EvolutionState.ClueFound) clues++;
            }
            _progressText.text = owned
                ? $"해금 <color={UITheme.ColorTag(UITheme.GoldAccent)}>{unlocked}</color>/{_order.Count}  ·  단서 {clues}"
                : $"진화 가능성 {_order.Count}종";

            var clue = owned ? FindClue(false) : null;
            bool craftable = false;
            int done = 0;
            int total = 0;
            bool conditionsLeft = false;
            if (clue != null)
            {
                var check = Game.Evolution.Check(clue.id);
                craftable = check != null && check.CanCraft;
                if (check != null && !craftable) CountReadiness(check, out done, out total, out conditionsLeft);
            }

            bool locked = owned && !CharacterUI.IsUnlocked(FeatureId.Evolution);
            if (locked) craftable = false;
            _checkButton.interactable = owned;
            _checkGlow.SetActive(craftable);
            if (_checkReadyArt != null && _checkReadyArt.activeSelf != craftable) _checkReadyArt.SetActive(craftable);
            _checkLabel.text = locked
                ? CharacterUI.LockedLong(CharacterUI.LockedHint(FeatureId.Evolution), CharacterUI.LockedSentence(FeatureId.Evolution))
                : CheckLabel(owned, clue != null ? clue.name : null, craftable, done, total, conditionsLeft);
            var color = !owned ? UITheme.Text : locked ? UITheme.Cream : craftable ? UITheme.Ink : clue != null ? UITheme.Cream : UITheme.SubText;
            // 누를 수 없으면 이름을 흐리게 (버튼 공장 ButtonLabelDim과 같은 0.5)
            color.a = owned ? 1f : 0.5f;
            _checkLabel.color = color;
        }

        /// <summary>
        /// 아직 만들 수 없는 단서의 준비 정도: 재료가 모자라면 재료 (갖춘 수 / 전체), 재료가 다 모였으면 조건 (채운 수 / 전체, conditionsLeft = true)
        /// </summary>
        static void CountReadiness(EvolutionCheck check, out int done, out int total, out bool conditionsLeft)
        {
            done = 0;
            total = check.Materials.Count;
            for (int i = 0; i < check.Materials.Count; i++)
            {
                if (check.Materials[i].Enough) done++;
            }
            conditionsLeft = false;
            if (done < total || check.Conditions.Count == 0) return;
            conditionsLeft = true;
            done = 0;
            total = check.Conditions.Count;
            for (int i = 0; i < check.Conditions.Count; i++)
            {
                if (check.Conditions[i].Done) done++;
            }
        }

        /// <summary>
        /// 진화 레시피 버튼 글 (M75): 미보유 '진화 확인' / 제작 가능 '{단서} 진화 제작! 준비 완료' / 단서만 '다음 진화 · {단서} 재료 k/n'(또는 '조건 k/n') /
        /// 단서 없음 '진화 단서 찾는 중'
        /// </summary>
        internal static string CheckLabel(bool owned, string clueName, bool craftable, int done, int total, bool conditionsLeft)
        {
            if (!owned) return "진화 확인";
            if (string.IsNullOrEmpty(clueName)) return "진화 단서 찾는 중";
            if (craftable) return $"{clueName} 진화 제작! <size=80%>준비 완료</size>";
            if (total <= 0) return $"다음 진화 · {clueName}";
            return $"다음 진화 · {clueName} <size=80%>{(conditionsLeft ? "조건" : "재료")} {done}/{total}</size>";
        }

        /// <summary>단서 발견 형태 중 첫 번째 (제작 가능한 것 우선)</summary>
        CharacterFormDef FindClue(bool craftableOnly)
        {
            CharacterFormDef first = null;
            foreach (var form in _order)
            {
                if (_states[form.id] != EvolutionState.ClueFound) continue;
                if (CharacterUI.IsCraftable(form.id)) return form;
                if (first == null) first = form;
            }
            return craftableOnly ? null : first;
        }

        void OpenFirstClue()
        {
            var form = FindClue(false);
            if (form == null)
            {
                CharacterUI.Toast("지금 확인할 진화 단서가 없어요. 계보 레벨을 올려 단서를 찾아보세요.");
                return;
            }
            UIManager.Instance.Get<EvolutionRecipePopup>().Show(form.id);
        }

        void OnNodeClicked(CharacterFormDef form) => CharacterUI.OpenForm(_lineage, form);
    }
}
