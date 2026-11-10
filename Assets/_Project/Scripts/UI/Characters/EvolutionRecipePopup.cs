using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 레시피 / 조건 확인 (와이어프레임 02). 진화 결과는 랜덤이 아닌 확정 제작이다.
    /// 목록 순서 (M71): ① 필요 재료(기억편 대체) → ② 진화 조건(기억편 가속 · 줄을 누르면 채우는 곳으로) → ③ 획득처 → 진화하면 얻는 스킬.
    /// 열 때마다 맨 위부터 보여 준다 (같은 형태를 다시 열어도).
    /// <para>
    /// 재료 · 획득처 (M17 · M39 · M72): 기억편 대체 버튼이 없는 재료 카드는 '획득처 ›' 글자 링크 — 이동하지 않고 그 재료의 획득처 줄로 스크롤한다.
    /// 획득처 줄은 갈 수 있는 곳 먼저(스테이지는 그 맨 뒤), 잠긴 곳은 열리는 스테이지 순 (재료마다 SourceAvailability.SortForDisplay, 그다음 전체를 한 번 더).
    /// </para>
    /// <para>
    /// 아래 버튼: [재료 모으러 가기 (보라)] '{재료} {사러 가기|모으러 가기} · {목적지}' — 모자란 재료마다 처음으로 갈 수 있는 획득처 (SourceAvailability.FirstAvailable).
    /// 모자란 재료가 있으면 늘 누를 수 있다. 갈 곳이 모두 잠겼으면 팝업을 닫지 않고 '{재료}은(는) {이유}' 토스트, 스테이지면 안내 토스트만.
    /// 갈 때는 돌아가기 칸 '‹ {형태} 레시피로'를 넣는다 (CharacterUI.GoToSource).
    /// [진화 제작 시작 (금)] → 진화 제작 팝업을 열자마자 제작을 시작한다 (EvolutionCraftPopup.Show(formId, autoStart: true), M75).
    /// 진화 제작이 잠긴 동안 (F-03, 1-10 전): 레시피는 그대로 볼 수 있고, 제작 버튼은 누를 수 없음(비활성 그림) + '1-10 클리어 시',
    /// 위 안내 줄은 잠금 문장 ('스테이지 1-10을 클리어하면 열려요'). 제작 버튼 이름은 버튼 안 글자를 찾아 바꾼다 (새 직렬화 필드 없음).
    /// </para>
    /// 누적 기록(원정 · 출격 · 처치)이 늘면 0.5초마다 조건 · 아래 버튼만 다시 그린다 (재료 · 획득처 줄은 재료 · 계보가 바뀔 때만).
    /// 새 직렬화 필드(_collectLabel)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </summary>
    public class EvolutionRecipePopup : UIPopup
    {
        /// <summary>획득처 줄로 스크롤할 때 줄 위에 남기는 여백 (px)</summary>
        const float ScrollMargin = 24f;
        /// <summary>모자란 재료가 없을 때 '재료 모으러 가기' 자리 글</summary>
        public const string CollectDoneLabel = "재료를 모두 모았어요";

        [Header("요약")]
        [SerializeField] TMP_Text _titleText;
        [SerializeField] FriendPortrait _fromPortrait;
        [SerializeField] TMP_Text _fromNameText;
        [SerializeField] FriendPortrait _toPortrait;
        [SerializeField] TMP_Text _toNameText;
        [SerializeField] TMP_Text _toTagsText;
        [SerializeField] TMP_Text _shardsText;

        [Header("목록")]
        [SerializeField] ScrollRect _scroll;
        [SerializeField] TMP_Text _skillTitle;
        [SerializeField] CharacterSkillCard _skillCard;
        [SerializeField] RectTransform _materialContent;
        [SerializeField] RecipeMaterialView _materialPrefab;
        [SerializeField] RectTransform _conditionContent;
        [SerializeField] RecipeConditionView _conditionPrefab;
        [SerializeField] GameObject _conditionEmpty;
        [SerializeField] RectTransform _sourceContent;
        [SerializeField] RecipeSourceView _sourcePrefab;
        [SerializeField] GameObject _sourceEmpty;

        [Header("하단")]
        [SerializeField] Button _collectButton;
        [Tooltip("'재료 모으러 가기' 이름 ('{재료} 사러 가기 · {목적지}')")]
        [SerializeField] TMP_Text _collectLabel;
        [SerializeField] Button _craftButton;
        [SerializeField] TMP_Text _craftHintText;
        [SerializeField] GameObject _craftGlow;

        readonly struct SourceEntry
        {
            public readonly ItemDef Item;
            public readonly ItemSource Source;
            public readonly SourceCheck Check;
            public readonly bool Missing;
            /// <summary>표시 순서 키 (DisplayKey)</summary>
            public readonly long Key;

            public SourceEntry(ItemDef item, ItemSource source, SourceCheck check, bool missing, long key)
            {
                Item = item;
                Source = source;
                Check = check;
                Missing = missing;
                Key = key;
            }
        }

        static readonly Comparison<SourceEntry> s_byKey = (a, b) => a.Key.CompareTo(b.Key);
        static readonly Vector3[] s_corners = new Vector3[4];

        UIListPool<RecipeMaterialView> _materials;
        UIListPool<RecipeConditionView> _conditions;
        UIListPool<RecipeSourceView> _sources;
        readonly List<SourceEntry> _sourceEntries = new();
        readonly List<ItemSource> _scratchSources = new();
        readonly List<SourceCheck> _scratchChecks = new();
        Action<string> _onSourceLink;
        string _formId;
        string _skillFormId;
        bool _skillUnlocked;
        EvolutionCheck _check;
        bool _subscribed;
        bool _progressDirty;
        float _nextProgressRefresh;
        Coroutine _scrollRoutine;

        // 아래 버튼 글을 바뀔 때만 다시 쓰기 위한 마지막 상태
        int _hintState = -1;
        int _collectState = -1;
        TMP_Text _craftLabel;
        string _craftLabelText;
        ItemDef _collectItem;
        ItemSource _collectSource;

        protected override void Awake()
        {
            base.Awake();
            _materials = new UIListPool<RecipeMaterialView>(_materialPrefab, _materialContent);
            _conditions = new UIListPool<RecipeConditionView>(_conditionPrefab, _conditionContent);
            _sources = new UIListPool<RecipeSourceView>(_sourcePrefab, _sourceContent);
            _collectButton.onClick.AddListener(CollectMaterials);
            _craftButton.onClick.AddListener(OpenCraft);
            _craftLabel = _craftButton.GetComponentInChildren<TMP_Text>(true);
            _craftLabelText = _craftLabel != null ? _craftLabel.text : null;
            LockedTapRelay.Attach(_craftButton, OnCraftLockedTap);
            _onSourceLink = ScrollToSource;

            Game.Inventory.Changed += OnDataChanged;
            Game.Characters.Changed += OnDataChanged;
            Game.Evolution.FormUnlocked += OnFormUnlocked;
            Game.Characters.ProgressChanged += OnProgressChanged;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (!_subscribed || !Game.IsReady) return;
            Game.Inventory.Changed -= OnDataChanged;
            Game.Characters.Changed -= OnDataChanged;
            Game.Evolution.FormUnlocked -= OnFormUnlocked;
            Game.Characters.ProgressChanged -= OnProgressChanged;
        }

        /// <summary>원정 / 잔향이 출격 / 처치 기록은 전투 중 자주 늘어나므로 모아서 0.5초마다 한 번만 갱신한다.</summary>
        void OnProgressChanged(string lineageId, ProgressType type)
        {
            if (!IsOpen) return;
            var lineage = Game.Characters.Db.GetLineageOfForm(_formId);
            if (lineage != null && lineage.id == lineageId) _progressDirty = true;
        }

        void Update()
        {
            if (!_progressDirty || Time.unscaledTime < _nextProgressRefresh) return;
            _progressDirty = false;
            _nextProgressRefresh = Time.unscaledTime + 0.5f;
            if (IsOpen) RefreshProgress();
        }

        /// <summary>그 형태의 레시피를 연다 (AutoShot · 진화 기억도 노드 · 돌아가기 칩이 부른다). 언제나 맨 위부터 보여 준다</summary>
        public void Show(string formId)
        {
            _formId = formId;
            Open();
            ScrollToTop();
        }

        protected override void OnOpen() => Refresh();

        protected override void OnClose()
        {
            if (_scrollRoutine != null) StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }

        void OnDataChanged()
        {
            if (IsOpen) Refresh();
        }

        void OnFormUnlocked(CharacterFormDef form) => OnDataChanged();

        void Refresh()
        {
            var db = Game.Characters.Db;
            var form = db.GetForm(_formId);
            var lineage = db.GetLineageOfForm(_formId);
            _check = form != null ? Game.Evolution.Check(_formId) : null;
            if (_check == null || lineage == null) return;

            RefreshSummary(form, lineage);
            // 획득처 줄을 먼저 — 재료 카드의 '획득처 ›' 링크는 그 재료 줄이 있을 때만 켠다
            RefreshSources();
            RefreshMaterials();
            RefreshConditions();
            RefreshFooter();
        }

        /// <summary>누적 기록만 바뀌었을 때: 조건과 아래 버튼만</summary>
        void RefreshProgress()
        {
            if (string.IsNullOrEmpty(_formId) || Game.Characters.Db.GetForm(_formId) == null) return;
            _check = Game.Evolution.Check(_formId);
            if (_check == null) return;
            RefreshConditions();
            RefreshFooter();
        }

        void RefreshSummary(CharacterFormDef form, LineageDef lineage)
        {
            var parent = Game.Characters.Db.GetForm(form.parentFormId);
            _titleText.text = form.name;
            if (parent != null) _fromPortrait.Set(CharacterUI.Portrait(parent), lineage, parent.name, PortraitTone.Normal);
            _fromNameText.text = parent != null ? parent.name : lineage.name;

            bool unlocked = _check.State == EvolutionState.Unlocked;
            _toPortrait.Set(CharacterUI.Portrait(form), lineage, form.name, unlocked ? PortraitTone.Normal : PortraitTone.Clue);
            _toNameText.text = form.name;
            _toTagsText.text =
                $"{CharacterUI.TierName(form.tier)}  ·  <color={UITheme.ColorTag(UITheme.StyleColor(form.style))}>{UITheme.StyleName(form.style)}</color>" +
                $"  ·  <color={UITheme.ColorTag(CharacterUI.RoleColor(form.recommendedRole))}>{UITheme.RoleName(form.recommendedRole)} 추천</color>";
            _shardsText.text = $"보유 기억편 <b>{Game.Characters.GetShards(lineage.id):N0}</b>";

            // 형태 / 해금 상태가 바뀔 때만 스킬 카드를 채운다
            if (_skillFormId == form.id && _skillUnlocked == unlocked) return;
            _skillFormId = form.id;
            _skillUnlocked = unlocked;
            _skillTitle.text = unlocked ? "익힌 스킬" : "진화하면 얻는 스킬";
            _skillCard.Show(form.skill, unlocked ? $"{form.name}의 고유 스킬" : $"{CharacterUI.WithDirection(form.name)} 진화하면 새로 익혀요");
        }

        void RefreshMaterials()
        {
            _materials.SetCount(_check.Materials.Count);
            for (int i = 0; i < _check.Materials.Count; i++)
            {
                var status = _check.Materials[i];
                _materials[i].Set(_formId, status, HasSourceRow(status.ItemId), _onSourceLink);
            }
        }

        void RefreshConditions()
        {
            _conditions.SetCount(_check.Conditions.Count);
            for (int i = 0; i < _check.Conditions.Count; i++) _conditions[i].Set(_formId, i, _check.Conditions[i]);
            bool empty = _check.Conditions.Count == 0;
            if (_conditionEmpty.activeSelf != empty) _conditionEmpty.SetActive(empty);
        }

        /// <summary>획득처 줄: 재료마다 SortForDisplay(갈 수 있는 곳 → 스테이지 → 잠긴 곳) 뒤 전체를 DisplayKey 순으로</summary>
        void RefreshSources()
        {
            _sourceEntries.Clear();
            int order = 0;
            for (int m = 0; m < _check.Materials.Count; m++)
            {
                var material = _check.Materials[m];
                var item = Game.Items.Get(material.ItemId);
                if (item == null || item.sources == null) continue;
                SourceAvailability.SortForDisplay(item, _scratchSources, _scratchChecks);
                for (int i = 0; i < _scratchSources.Count && i < _scratchChecks.Count; i++)
                {
                    var source = _scratchSources[i];
                    var check = _scratchChecks[i];
                    long key = DisplayKey(check.Available, source.type == ItemSourceType.Stage, check.RequiredStage, order++);
                    _sourceEntries.Add(new SourceEntry(item, source, check, !material.Enough, key));
                }
            }
            _sourceEntries.Sort(s_byKey);

            _sources.SetCount(_sourceEntries.Count);
            for (int i = 0; i < _sourceEntries.Count; i++)
            {
                var entry = _sourceEntries[i];
                _sources[i].Set(entry.Item, entry.Source, entry.Missing, entry.Check, _formId);
            }
            bool empty = _sourceEntries.Count == 0;
            if (_sourceEmpty.activeSelf != empty) _sourceEmpty.SetActive(empty);
        }

        /// <summary>
        /// 획득처 줄 순서 키 (작을수록 위): 갈 수 있는 곳(0) → 갈 수 있는 스테이지(1) → 잠긴 곳(2, 열리는 스테이지가 이른 순 — 모르면 맨 뒤),
        /// 같은 묶음 · 같은 스테이지면 order(재료 순서 → 재료 안 SortForDisplay 순서).
        /// </summary>
        internal static long DisplayKey(bool available, bool stageSource, int requiredStage, int order)
        {
            long group = available ? (stageSource ? 1 : 0) : 2;
            long stage = available ? 0 : Math.Min(int.MaxValue, Math.Max(0, requiredStage));
            return (group << 52) | (stage << 20) | (long)Math.Min(0xFFFFF, Math.Max(0, order));
        }

        bool HasSourceRow(string itemId)
        {
            for (int i = 0; i < _sourceEntries.Count; i++)
            {
                var item = _sourceEntries[i].Item;
                if (item != null && item.id == itemId) return true;
            }
            return false;
        }

        void RefreshFooter()
        {
            bool unlocked = _check.State == EvolutionState.Unlocked;
            bool gated = !CharacterUI.IsUnlocked(FeatureId.Evolution);
            bool canCraft = _check.CanCraft && !gated;
            _craftButton.interactable = canCraft;
            if (_craftGlow.activeSelf != canCraft) _craftGlow.SetActive(canCraft);
            if (_craftLabel != null)
            {
                string craftText = gated && !unlocked ? CharacterUI.LockedShort(CharacterUI.LockedHint(FeatureId.Evolution)) : _craftLabelText;
                if (craftText != null && _craftLabel.text != craftText) _craftLabel.text = craftText;
            }

            var source = FindMissingSource(out var item, out _, out bool anyMissing);
            // 모자란 재료가 하나라도 있으면 누를 수 있다 (갈 곳이 잠겼으면 누를 때 이유를 알려 준다)
            _collectButton.interactable = anyMissing;
            int collectState = anyMissing ? 1 : 0;
            if (_collectLabel != null && (collectState != _collectState || item != _collectItem || source != _collectSource))
            {
                _collectState = collectState;
                _collectItem = item;
                _collectSource = source;
                _collectLabel.text = CollectButtonLabel(anyMissing, item != null ? item.name : null, source);
            }

            int hintState = unlocked ? 0 : gated ? 3 : canCraft ? 1 : 2;
            if (hintState == _hintState) return;
            _hintState = hintState;
            _craftHintText.text = hintState switch
            {
                0 => "이미 해금된 형태예요. 편성에서 바로 쓸 수 있어요.",
                1 => $"<color={UITheme.ColorTag(UITheme.Gold)}>모든 준비가 끝났어요!</color> 진화 결과는 랜덤이 아닌 확정 제작이에요.",
                3 => $"진화 제작은 {CharacterUI.LockedSentence(FeatureId.Evolution)}. 레시피는 미리 볼 수 있어요.",
                _ => "재료와 조건을 모두 채우면 확정으로 제작할 수 있어요.",
            };
        }

        /// <summary>
        /// '재료 모으러 가기' 글 (M17 · M72): 모자란 재료가 없으면 '재료를 모두 모았어요',
        /// 있으면 '{재료} {사러 가기|모으러 가기} · {목적지}' (갈 곳이 없으면 '{재료} 모으러 가기', 재료를 모르면 '재료 모으러 가기').
        /// </summary>
        internal static string CollectButtonLabel(bool anyMissing, string itemName, ItemSource source)
        {
            if (!anyMissing) return CollectDoneLabel;
            return CharacterUI.CollectLabel(string.IsNullOrEmpty(itemName) ? "재료" : itemName, source);
        }

        /// <summary>
        /// 모자란 재료마다 처음으로 갈 수 있는 획득처 (재료 순서대로, SourceAvailability.FirstAvailable — 스테이지도 갈 수 있는 곳으로 센다).
        /// 찾으면 item = 그 재료. 하나도 없으면 null, item / reason = 처음 모자란 재료와 그 첫 잠금 이유. anyMissing = 모자란 재료가 있는지.
        /// </summary>
        ItemSource FindMissingSource(out ItemDef item, out string reason, out bool anyMissing)
        {
            item = null;
            reason = string.Empty;
            anyMissing = false;
            if (_check == null) return null;
            ItemDef firstItem = null;
            string firstReason = string.Empty;
            for (int i = 0; i < _check.Materials.Count; i++)
            {
                var material = _check.Materials[i];
                if (material.Enough) continue;
                anyMissing = true;
                var def = Game.Items.Get(material.ItemId);
                if (def == null) continue;
                var source = SourceAvailability.FirstAvailable(def, out var firstLock);
                if (source != null)
                {
                    item = def;
                    return source;
                }
                if (firstItem == null)
                {
                    firstItem = def;
                    firstReason = firstLock;
                }
            }
            item = firstItem;
            reason = firstReason;
            return null;
        }

        /// <summary>재료 모으러 가기: 잠금은 팝업을 닫기 전에 본다 (갈 곳이 없으면 토스트만, 팝업 그대로)</summary>
        void CollectMaterials()
        {
            var source = FindMissingSource(out var item, out var reason, out bool anyMissing);
            if (!anyMissing)
            {
                CharacterUI.Toast("필요한 재료를 모두 모았어요!", ToastKind.Success);
                return;
            }
            if (source == null)
            {
                CharacterUI.Toast(CharacterUI.LockedSourceToast(item != null ? item.name : null, reason));
                return;
            }
            CharacterUI.GoToSource(item, source, _formId);
        }

        /// <summary>꺼진 제작 버튼을 누름 (F-03): 진화 제작이 잠겼으면 잠금 문장 토스트 + 약한 진동</summary>
        void OnCraftLockedTap()
        {
            if (IsOpen && !CharacterUI.IsUnlocked(FeatureId.Evolution)) CharacterUI.DenyLocked(FeatureId.Evolution);
        }

        /// <summary>진화 제작 시작 (M75): 제작 팝업을 열자마자 제작 ('곧 제작해요…' 0.35초 뒤). 진화 제작이 잠겼으면 잠금 안내만 (F-03)</summary>
        void OpenCraft()
        {
            if (!CharacterUI.IsUnlocked(FeatureId.Evolution))
            {
                CharacterUI.DenyLocked(FeatureId.Evolution);
                return;
            }
            if (_check == null || !_check.CanCraft)
            {
                CharacterUI.Toast("재료와 조건을 모두 채워야 제작할 수 있어요.");
                return;
            }
            UIManager.Instance.Get<EvolutionCraftPopup>().Show(_formId, autoStart: true);
        }

        // ───────────── 스크롤 ─────────────

        void ScrollToTop()
        {
            if (_scroll == null) return;
            if (_scrollRoutine != null) StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>재료 카드 '획득처 ›': 이동하지 않고 그 재료의 첫 획득처 줄이 보이게 스크롤한 뒤 줄을 한 번 튕긴다 (0.2초 OutCubic, 줄이기면 바로)</summary>
        void ScrollToSource(string itemId)
        {
            if (_scroll == null || _scroll.content == null || string.IsNullOrEmpty(itemId)) return;
            int index = -1;
            for (int i = 0; i < _sourceEntries.Count; i++)
            {
                var item = _sourceEntries[i].Item;
                if (item != null && item.id == itemId)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0 || index >= _sources.Count) return;

            var content = _scroll.content;
            var viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            Canvas.ForceUpdateCanvases();
            float scrollable = content.rect.height - viewport.rect.height;
            float target = 1f;
            if (scrollable > 0.5f)
            {
                ((RectTransform)_sources[index].transform).GetWorldCorners(s_corners);
                float rowTop = content.InverseTransformPoint(s_corners[1]).y;
                float fromTop = content.rect.yMax - rowTop;
                target = 1f - Mathf.Clamp(fromTop - ScrollMargin, 0f, scrollable) / scrollable;
            }

            if (_scrollRoutine != null) StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
            _scroll.StopMovement();
            if (UIMotion.Reduced || !isActiveAndEnabled) _scroll.verticalNormalizedPosition = target;
            else _scrollRoutine = StartCoroutine(ScrollTo(target));
            _sources[index].Punch();
        }

        IEnumerator ScrollTo(float target)
        {
            float from = _scroll.verticalNormalizedPosition;
            for (float t = 0f; t < UIMotion.ScrollToTop; t += Time.unscaledDeltaTime)
            {
                _scroll.verticalNormalizedPosition = Mathf.Lerp(from, target, UIMotion.OutCubic(t / UIMotion.ScrollToTop));
                yield return null;
            }
            _scroll.verticalNormalizedPosition = target;
            _scrollRoutine = null;
        }
    }
}
