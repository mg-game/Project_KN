using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 제작 / 완성 (와이어프레임 03): Before → After, 소모 재료, 제작 진행 0→100%,
    /// TryCraft 후 "해금 완료" 축하 화면. 레벨 · 호감도는 계보가 공유한다 (CR-11: 장비는 다루지 않는다 — 연구 §17 목록).
    /// 진화 제작이 잠긴 동안 (F-03, 1-10 전): 제작 버튼은 누를 수 없음 + '1-10 클리어 시', 바로 제작도 하지 않는다.
    /// <para>
    /// 바로 제작 (M75): 레시피의 '진화 제작 시작'은 Show(formId, autoStart: true) — '곧 제작해요…'(누를 수 없음) 0.35초 뒤 StartCraft.
    /// 그때 만들 수 없게 됐으면 안내 토스트 뒤 손으로 누르는 '진화 제작'으로 돌아간다.
    /// </para>
    /// <para>
    /// 닫기 정책 (M49, X도 같은 길 — CloseButtonUsesDismiss): 진행 막대가 차는 동안은 무시, 축하 화면이 나오는 중이면 끝 모습으로 바로,
    /// 만든 뒤에는 확인(Confirm — 레시피를 닫고 상세의 새 노드를 반짝), 그 밖에는 닫기.
    /// </para>
    /// 진행 막대는 정해진 곡선을 CharacterUI.EaseFill로 따라간다 (M62 — 줄어들 때 · 다시 열 때는 바로). 퍼센트 글은 숫자가 바뀔 때만 쓴다.
    /// 소리 (AUDIO-SPEC §8.1): 제작 시작 EvolveCharge → 성공 EvolveBurst → 축하 EvolveReveal. 제작이 시작되면 전장 소리를 가린다(Hidden), 닫으면 푼다.
    /// 장면 경로 Popups/EvolutionCraftPopup은 그대로 (LevelUpFanfareView가 경로로 찾는다).
    /// </summary>
    public class EvolutionCraftPopup : UIPopup
    {
        /// <summary>바로 제작에서 '곧 제작해요…'를 보여 주는 시간 (초, realtime)</summary>
        public const float AutoStartDelay = 0.35f;
        const string CraftLabel = "진화 제작";
        const string AutoStartLabel = "곧 제작해요…";

        [Header("Before → After")]
        [SerializeField] FriendPortrait _beforePortrait;
        [SerializeField] TMP_Text _beforeNameText;
        [SerializeField] Image _beforeRoleChip;
        [SerializeField] TMP_Text _beforeRoleText;
        [SerializeField] FriendPortrait _afterPortrait;
        [SerializeField] TMP_Text _afterNameText;
        [SerializeField] Image _afterRoleChip;
        [SerializeField] TMP_Text _afterRoleText;
        [SerializeField] Image _afterGlow;
        [SerializeField] SparkleBurst _afterBurst;

        [Header("재료 / 진행")]
        [SerializeField] RectTransform _materialContent;
        [SerializeField] ItemSlotView _materialPrefab;
        [SerializeField] RectTransform _progressFill;
        [SerializeField] RectTransform _progressHead;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] SparkleBurst _progressSparks;
        [SerializeField] Button _craftButton;
        [SerializeField] TMP_Text _craftLabel;

        [Header("공유 안내")]
        [SerializeField] Button _infoButton;
        [SerializeField] Button _tooltip;

        [Header("해금 완료")]
        [SerializeField] CanvasGroup _celebration;
        [SerializeField] RectTransform _celebrationCard;
        [SerializeField] FriendPortrait _celebrationPortrait;
        [SerializeField] TMP_Text _celebrationText;
        [SerializeField] TMP_Text _celebrationSubText;
        [SerializeField] SparkleBurst _celebrationBurst;
        [SerializeField] SparkleBurst _celebrationAmbient;
        [SerializeField] Button _confirmButton;
        [SerializeField] Button _formationButton;

        [SerializeField] float _craftDuration = 1.8f;

        UIListPool<ItemSlotView> _materials;
        string _formId;
        bool _autoStart;
        Coroutine _routine;
        Coroutine _fade;
        /// <summary>제작 진행 중 (막대 → 성공 연출 → 축하가 끝날 때까지)</summary>
        bool _crafting;
        /// <summary>TryCraft가 성공했다</summary>
        bool _crafted;
        /// <summary>축하 화면이 끝 모습이 됐다 (건너뛰기 포함)</summary>
        bool _celebrated;
        bool _focusHeld;
        float _progressShown;
        int _percentShown = -1;

        protected override bool CloseButtonUsesDismiss => true;

        protected override void Awake()
        {
            base.Awake();
            _materials = new UIListPool<ItemSlotView>(_materialPrefab, _materialContent);
            _craftButton.onClick.AddListener(StartCraft);
            LockedTapRelay.Attach(_craftButton, OnCraftLockedTap);
            _infoButton.onClick.AddListener(ToggleTooltip);
            _tooltip.onClick.AddListener(ToggleTooltip);
            _confirmButton.onClick.AddListener(Confirm);
            _formationButton.onClick.AddListener(GoToFormation);
        }

        /// <summary>그 형태의 진화 제작 팝업을 연다 (장면 경로 Popups/EvolutionCraftPopup은 그대로 — LevelUpFanfareView가 경로로 찾는다).</summary>
        /// <param name="autoStart">
        /// true면 열자마자 제작을 시작한다 (레시피의 '진화 제작 시작', M75): '곧 제작해요…' 0.35초 뒤 StartCraft, 만들 수 없으면 손으로 누르게 되돌린다.
        /// false(기본)면 예전처럼 '진화 제작'을 눌러 시작한다.
        /// </param>
        public void Show(string formId, bool autoStart = false)
        {
            _formId = formId;
            _autoStart = autoStart;
            Open();
        }

        protected override void OnOpen()
        {
            StopRoutines();
            _crafting = false;
            _crafted = false;
            _celebrated = false;
            _celebration.gameObject.SetActive(false);
            _celebrationAmbient.SetAmbient(false, Color.white);
            _tooltip.gameObject.SetActive(false);
            SnapProgress(0f);
            Fill();

            bool auto = _autoStart;
            _autoStart = false;
            if (!CharacterUI.IsUnlocked(FeatureId.Evolution))
            {
                _craftButton.interactable = false;
                _craftLabel.text = CharacterUI.LockedShort(CharacterUI.LockedHint(FeatureId.Evolution));
                return;
            }
            if (auto && isActiveAndEnabled)
            {
                _craftButton.interactable = false;
                _craftLabel.text = AutoStartLabel;
                _routine = StartCoroutine(AutoStart());
                return;
            }
            _craftButton.interactable = true;
            _craftLabel.text = CraftLabel;
        }

        protected override void OnClose()
        {
            StopRoutines();
            _crafting = false;
            _celebrationAmbient.SetAmbient(false, Color.white);
            ReleaseFocus();
        }

        /// <summary>
        /// 어둡게 덮기 · 뒤로가기 · X (M49): 막대가 차는 동안 무시 / 축하가 나오는 중이면 끝 모습으로 / 만든 뒤 확인 / 그 밖에는 닫기
        /// </summary>
        protected override void OnDimOrBack()
        {
            if (_crafting && !_crafted) return;
            if (_crafted && !_celebrated)
            {
                SnapCelebration();
                return;
            }
            if (_crafted)
            {
                Confirm();
                return;
            }
            Close();
        }

        void StopRoutines()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_fade != null) StopCoroutine(_fade);
            _fade = null;
        }

        void Fill()
        {
            var db = Game.Characters.Db;
            var form = db.GetForm(_formId);
            var lineage = db.GetLineageOfForm(_formId);
            if (form == null || lineage == null) return;
            var parent = db.GetForm(form.parentFormId) ?? CharacterUI.BaseForm(lineage);

            if (parent != null)
            {
                _beforePortrait.Set(CharacterUI.Portrait(parent), lineage, parent.name, PortraitTone.Normal);
                _beforeNameText.text = parent.name;
                SetRole(_beforeRoleChip, _beforeRoleText, parent.recommendedRole);
            }
            _afterPortrait.Set(CharacterUI.Portrait(form), lineage, form.name, PortraitTone.Clue);
            _afterNameText.text = form.name;
            SetRole(_afterRoleChip, _afterRoleText, form.recommendedRole);
            _afterGlow.color = CharacterUI.WithAlpha(CharacterUI.RoleColor(form.recommendedRole), _afterGlow.color.a);

            var check = Game.Evolution.Check(_formId);
            int count = check != null ? check.Materials.Count : 0;
            _materials.SetCount(count);
            for (int i = 0; i < count; i++)
            {
                var status = check.Materials[i];
                var item = Game.Items.Get(status.ItemId);
                if (item != null) _materials[i].Set(item, status.Need, false);
                else _materials[i].Set(status.ItemId, ItemGrade.Common, null, status.Need, false);
            }
        }

        static void SetRole(Image chip, TMP_Text label, CharacterRole role)
        {
            chip.color = CharacterUI.RoleColor(role);
            label.text = UITheme.RoleName(role);
        }

        /// <summary>막대를 바로 맞춘다 (열 때 · 실패해 되돌릴 때 · 끝)</summary>
        void SnapProgress(float amount)
        {
            _progressShown = Mathf.Clamp01(amount);
            ApplyProgress();
        }

        /// <summary>정해진 곡선 값을 EaseFill로 따라간다 (늘 때만 부드럽게)</summary>
        void FollowProgress(float target, float dt)
        {
            _progressShown = CharacterUI.EaseFill(_progressShown, Mathf.Clamp01(target), dt);
            ApplyProgress();
        }

        void ApplyProgress()
        {
            float amount = _progressShown;
            CharacterUI.SetFill(_progressFill, amount);
            _progressHead.anchorMin = _progressHead.anchorMax = new Vector2(amount, 0.5f);
            bool head = amount > 0.001f;
            if (_progressHead.gameObject.activeSelf != head) _progressHead.gameObject.SetActive(head);
            int percent = Mathf.RoundToInt(amount * 100f);
            if (percent == _percentShown) return;
            _percentShown = percent;
            _progressText.SetText("{0}%", percent);
        }

        void ToggleTooltip() => _tooltip.gameObject.SetActive(!_tooltip.gameObject.activeSelf);

        // ───────────── 제작 ─────────────

        IEnumerator AutoStart()
        {
            yield return new WaitForSecondsRealtime(AutoStartDelay);
            _routine = null;
            _craftButton.interactable = true;
            _craftLabel.text = CraftLabel;
            StartCraft();
        }

        /// <summary>꺼진 제작 버튼을 누름 (F-03): 진화 제작이 잠겼으면 잠금 문장 토스트 + 약한 진동</summary>
        void OnCraftLockedTap()
        {
            if (IsOpen && !_crafting && !CharacterUI.IsUnlocked(FeatureId.Evolution)) CharacterUI.DenyLocked(FeatureId.Evolution);
        }

        void StartCraft()
        {
            if (_crafting || _crafted || _routine != null) return;
            if (!CharacterUI.IsUnlocked(FeatureId.Evolution))
            {
                CharacterUI.DenyLocked(FeatureId.Evolution);
                return;
            }
            if (!CharacterUI.IsCraftable(_formId))
            {
                CharacterUI.Toast("재료와 조건을 다시 확인해 주세요.");
                return;
            }
            _routine = StartCoroutine(CraftRoutine());
        }

        IEnumerator CraftRoutine()
        {
            _crafting = true;
            _craftButton.interactable = false;
            _craftLabel.text = "제작 중…";
            HoldFocus();
            UISfx.Play(UISfxKey.EvolveCharge);

            float nextSpark = 0f;
            for (float t = 0; t < _craftDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / _craftDuration;
                // 처음엔 빠르게, 끝에서 살짝 머뭇거리는 진행
                float curve = k < 0.85f ? Mathf.Lerp(0f, 0.92f, UIMotion.OutCubic(k / 0.85f)) : Mathf.Lerp(0.92f, 1f, (k - 0.85f) / 0.15f);
                FollowProgress(curve, Time.unscaledDeltaTime);
                if (t >= nextSpark)
                {
                    nextSpark = t + 0.12f;
                    _progressSparks.Play(UITheme.Lamp, 3, 0.5f);
                }
                yield return null;
            }
            SnapProgress(1f);

            if (!Game.Evolution.TryCraft(_formId))
            {
                CharacterUI.Toast("제작할 수 없어요. 재료와 조건을 다시 확인해 주세요.");
                _crafting = false;
                _craftButton.interactable = true;
                _craftLabel.text = CraftLabel;
                SnapProgress(0f);
                ReleaseFocus();
                _routine = null;
                yield break;
            }

            _crafted = true;
            _craftLabel.text = "제작 완료";
            var form = Game.Characters.Db.GetForm(_formId);
            var lineage = Game.Characters.Db.GetLineageOfForm(_formId);
            _afterPortrait.Set(CharacterUI.Portrait(form), lineage, form.name, PortraitTone.Normal);
            UISfx.Play(UISfxKey.EvolveBurst);
            _afterBurst.Play(UITheme.GoldAccent, CharacterMotion.BurstCount(16), 0.8f);
            if (!UIMotion.Reduced) yield return CharacterMotion.Punch(_afterPortrait.transform, 0.12f, 0.3f);
            yield return new WaitForSecondsRealtime(0.25f);

            yield return ShowCelebration(form, lineage);
            _celebrated = true;
            _crafting = false;
            _routine = null;
        }

        /// <summary>'{형태} 해금됐어요!' (M76)</summary>
        internal static string CelebrationTitle(string formName) => $"{KoreanText.WithSubject(formName)} 해금됐어요!";

        void FillCelebration(CharacterFormDef form, LineageDef lineage)
        {
            _celebrationPortrait.Set(CharacterUI.Portrait(form), lineage, form.name, PortraitTone.Normal);
            _celebrationText.text = CelebrationTitle(form.name);
            // 양피지 카드 위 글자 (M29): 성향 · 역할 색은 파스텔이라 InkTint로 진하게 (양피지 5.0:1 이상, 스킬 줄과 같은 규칙)
            string styleInk = UITheme.ColorTag(CharacterSkillUI.InkTint(UITheme.StyleColor(form.style)));
            string roleInk = UITheme.ColorTag(CharacterSkillUI.InkTint(CharacterUI.RoleColor(form.recommendedRole)));
            _celebrationSubText.text =
                $"<color={styleInk}><b>{UITheme.StyleName(form.style)}</b></color> 성향 · " +
                $"<color={roleInk}><b>{UITheme.RoleName(form.recommendedRole)}</b></color> 추천\n" +
                SkillLine(form.skill) +
                $"레벨 · 호감도는 {lineage.name} 계보와 함께 나눠요.";
        }

        IEnumerator ShowCelebration(CharacterFormDef form, LineageDef lineage)
        {
            FillCelebration(form, lineage);
            _celebration.gameObject.SetActive(true);
            _celebrationAmbient.SetAmbient(true, UITheme.Lamp);
            UISfx.Play(UISfxKey.EvolveReveal);
            if (UIMotion.Reduced)
            {
                _celebration.alpha = 1f;
                _celebrationCard.localScale = Vector3.one;
            }
            else
            {
                _celebration.alpha = 0f;
                _celebrationCard.localScale = Vector3.one * 0.6f;
                _fade = StartCoroutine(CharacterMotion.Fade(_celebration, 0f, 1f, 0.25f));
                yield return CharacterMotion.Pop(_celebrationCard, 0.4f, 0.6f);
            }
            _celebrationBurst.Play(UITheme.GoldAccent, CharacterMotion.BurstCount(22), 1.1f);
            _celebrationBurst.Play(UITheme.Lavender, CharacterMotion.BurstCount(12), 0.9f);
        }

        /// <summary>축하 화면을 끝 모습으로 바로 (성공 연출 · 등장 도중에 덮기 · 뒤로가기 · X)</summary>
        void SnapCelebration()
        {
            StopRoutines();
            var form = Game.Characters.Db.GetForm(_formId);
            var lineage = Game.Characters.Db.GetLineageOfForm(_formId);
            if (form != null && lineage != null)
            {
                _afterPortrait.Set(CharacterUI.Portrait(form), lineage, form.name, PortraitTone.Normal);
                _afterPortrait.transform.localScale = Vector3.one;
                if (!_celebration.gameObject.activeSelf)
                {
                    FillCelebration(form, lineage);
                    _celebration.gameObject.SetActive(true);
                    _celebrationAmbient.SetAmbient(true, UITheme.Lamp);
                    UISfx.Play(UISfxKey.EvolveReveal);
                }
            }
            _celebration.alpha = 1f;
            _celebrationCard.localScale = Vector3.one;
            _celebrated = true;
            _crafting = false;
        }

        /// <summary>새로 익힌 스킬 한 줄 (스킬이 없으면 빈 문자열). 예) 새 스킬 「천 덧대기」 · 8초마다</summary>
        static string SkillLine(SkillDef skill)
        {
            if (!CharacterSkillUI.HasSkill(skill)) return string.Empty;
            string accent = UITheme.ColorTag(CharacterSkillUI.InkTint(CharacterSkillUI.AccentColor(skill)));
            return $"새 스킬 <color={accent}><b>「{skill.name}」</b></color> · {CharacterSkillUI.TriggerText(skill)}\n";
        }

        void HoldFocus()
        {
            if (_focusHeld) return;
            _focusHeld = true;
            AudioService.SetBattleFocus(BattleAudioFocus.Hidden, GetInstanceID());
        }

        void ReleaseFocus()
        {
            if (!_focusHeld) return;
            _focusHeld = false;
            AudioService.ClearBattleFocus(GetInstanceID());
        }

        void Confirm()
        {
            string formId = _formId;
            Close();
            var recipe = UIManager.Instance.Get<EvolutionRecipePopup>();
            if (recipe != null && recipe.IsOpen) recipe.Close();
            var detail = UIManager.Instance.Get<CharacterDetailPopup>();
            if (detail != null) detail.HighlightForm(formId);
        }

        /// <summary>편성하러 가기 ›: 팝업을 모두 닫고 편성 시트를 연 뒤 그 친구 카드를 반짝인다 (FormationPanel.PulseRosterCard — 패널이 켜질 때까지 기다린다)</summary>
        void GoToFormation()
        {
            var lineage = string.IsNullOrEmpty(_formId) ? null : Game.Characters.Db.GetLineageOfForm(_formId);
            CharacterUI.CloseAllPopups();
            UINav.GoToTab(MainTab.Formation);
            if (lineage != null) FormationPanel.PulseRosterCard(lineage.id);
        }
    }
}
