using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 캐릭터 화면(친구들 / 진화 기억도 / 의상 / 소환) 공용 표시 도우미.
    /// 초상화·이름 조회, 빨간 점 판정, 한국어 조사, 크림 카드 위 글자색을 모아 둔다.
    /// </summary>
    public static class CharacterUI
    {
        /// <summary>크림 카드 위 "충족" 글자색 (M29: UITheme.PositiveInk와 같은 색 — 양피지 6.08:1)</summary>
        public static readonly Color InkPositive = UITheme.PositiveInk;
        /// <summary>크림 카드 위 "부족" 글자색 (M29: UITheme.NegativeInk와 같은 색 — 양피지 5.60:1)</summary>
        public static readonly Color InkNegative = UITheme.NegativeInk;
        /// <summary>
        /// 크림 카드 위 감정 문구 색 (더스티 핑크를 진하게, M29 4.5:1): 양피지 #F6EDDA 위 5.6:1, 스킬 카드 분홍 말풍선(#F3D5DD 합성) 위 4.8:1.
        /// 예전 #B05686은 3.99:1 / 3.41:1이라 24px 이하 글자에 모자랐다.
        /// </summary>
        public static readonly Color InkPink = new Color32(0x93, 0x40, 0x6E, 0xFF);
        /// <summary>미보유 / 미발견 실루엣</summary>
        public static readonly Color SilhouetteTint = new Color(0.1f, 0.07f, 0.19f, 1f);
        /// <summary>단서 발견 (흐릿한 초상화)</summary>
        public static readonly Color ClueTint = new Color(0.62f, 0.58f, 0.76f, 0.92f);

        static CharacterDatabase Db => Game.Characters.Db;

        public static CharacterFormDef BaseForm(LineageDef lineage) => lineage == null ? null : Db.GetForm(lineage.baseFormId);

        public static bool IsOwned(LineageDef lineage) => lineage != null && Game.Characters.IsOwned(lineage.id);

        /// <summary>보유 계보는 장착 의상을 반영한 초상화, 미보유면 형태 기본 초상화 (없으면 null)</summary>
        public static Sprite Portrait(CharacterFormDef form)
        {
            if (form == null) return null;
            return IsOwned(Db.GetLineageOfForm(form.id)) ? Game.Characters.GetPortrait(form.id) : form.portrait;
        }

        /// <summary>보유 계보는 장착 의상 이름("밤지기 짝짝이"), 미보유면 형태 이름</summary>
        public static string DisplayName(CharacterFormDef form)
        {
            if (form == null) return string.Empty;
            return IsOwned(Db.GetLineageOfForm(form.id)) ? Game.Characters.GetDisplayName(form.id) : form.name;
        }

        public static int CountUnlockedForms(LineageDef lineage)
        {
            if (!IsOwned(lineage)) return 0;
            int count = 0;
            foreach (var form in lineage.forms)
            {
                if (form != null && Game.Characters.IsFormUnlocked(form.id)) count++;
            }
            return count;
        }

        /// <summary>제작할 수 있는 진화가 있는지 (진화 제작이 잠겼으면 false — F-03: 잠긴 동안 '진화 제작 가능' 점 · 칩을 켜지 않는다)</summary>
        public static bool CanCraftAny(LineageDef lineage) =>
            lineage != null && IsFeatureUnlocked(FeatureId.Evolution) && Game.Evolution.CountCraftable(lineage.id) > 0;

        /// <summary>그 형태를 지금 제작할 수 있는지 (단서 발견 + 재료 · 조건 충족, 진화 제작이 잠겼으면 false)</summary>
        public static bool IsCraftable(string formId)
        {
            if (!IsFeatureUnlocked(FeatureId.Evolution)) return false;
            if (Game.Evolution.GetState(formId) != EvolutionState.ClueFound) return false;
            var check = Game.Evolution.Check(formId);
            return check != null && check.CanCraft;
        }

        /// <summary>기억편으로 계보 공명 레벨을 올릴 수 있는지</summary>
        public static bool CanResonate(LineageDef lineage) => lineage != null && Game.Characters.CanResonance(lineage.id);

        /// <summary>조건을 채워 바로 해금할 수 있는 의상이 있는지</summary>
        public static bool CanUnlockCostume(LineageDef lineage) =>
            lineage != null && IsFeatureUnlocked(FeatureId.Costumes) && Game.Costumes.CountUnlockable(lineage.id) > 0;

        /// <summary>조건형 의상이 지금 해금 가능한지 (이벤트 상점 의상·보유 의상·미보유 계보는 false, 의상이 잠겼으면 false — F-03)</summary>
        public static bool IsCostumeUnlockable(CostumeDef costume) => IsFeatureUnlocked(FeatureId.Costumes) && Game.Costumes.CanUnlock(costume.id);

        /// <summary>받을 호감도 단계가 있는지 (CR-10 — 친구들 탭 점의 계보 잎 RedDotPaths.LineageAffinity와 같은 조건, 메타가 꺼져 있으면 false)</summary>
        public static bool HasAffinityClaim(LineageDef lineage) =>
            lineage != null && MetaUi.IsActive && Game.Affinity != null && Game.Affinity.HasClaimable(lineage.id);

        /// <summary>호감도 하트 바느질 틀을 켜는지 (CR-10 §6.3: 보유 + 호감도 Lv5 이상 — 친구들 카드 · 상세 머리 초상화가 같은 규칙)</summary>
        public static bool ShowsAffinityFrame(LineageDef lineage) =>
            IsOwned(lineage) && MetaUi.IsActive && Game.Affinity != null && Game.Affinity.GetLevel(lineage.id) >= CharacterCardView.AffinityFrameLevel;

        /// <summary>
        /// 친구들 카드 빨간 점: 진화 제작 가능 / 공명 가능 / 의상 해금 가능 / 받을 호감도 단계 — 친구들 탭 점을 켜는 계보 잎 넷과 같다
        /// (MetaSession의 진화 · 공명 · 의상 잎 + AffinitySystem.RegisterRedDots의 호감도 잎). 탭 점이 켜졌는데 어느 카드에도 점이 없는 일이 없게.
        /// </summary>
        public static bool HasAlert(LineageDef lineage) =>
            CardAlert(CanCraftAny(lineage), CanResonate(lineage), CanUnlockCostume(lineage), HasAffinityClaim(lineage));

        /// <summary>카드 점 규칙 (순수 — UiWave2_WP6Tests): 계보 잎 넷 중 하나라도 켜지면 켠다</summary>
        internal static bool CardAlert(bool craft, bool resonate, bool costume, bool affinity) => craft || resonate || costume || affinity;

        /// <summary>보유 의상의 공격력 보너스 합계(%)</summary>
        public static float CostumeBonusPercent(LineageDef lineage) => Game.Characters.GetCostumeAttackBonusPercent(lineage.id);

        // ───────────── 표기 ─────────────

        public static Color RoleColor(CharacterRole role) => role == CharacterRole.Guardian ? UITheme.Guardian : UITheme.EchoGlow;

        public static string TierName(int tier) => tier <= 0 ? "기본형" : $"{tier}차 진화";

        public static string BodyTypeName(BodyType type) => type switch
        {
            BodyType.Mascot => "마스코트형",
            BodyType.SemiHuman => "준인간형",
            BodyType.Humanoid => "인간형",
            _ => type.ToString(),
        };

        public static string StyleHint(CombatStyle style) => style switch
        {
            CombatStyle.Defense => "범위 안의 적을 느리게 붙잡아 둬요.",
            CombatStyle.Assault => "가까운 적에게 큰 피해를 줘요.",
            CombatStyle.Capture => "적을 잠깐 묶어 움직이지 못하게 해요.",
            CombatStyle.Snipe => "멀리 있는 적 하나를 강하게 노려요.",
            CombatStyle.Support => "친구를 북돋고 적에게 표식을 남겨요.",
            CombatStyle.Heal => "수선점과 친구들을 치료하고 보호막을 씌워요.",
            _ => string.Empty,
        };

        public static string RoleHint(CharacterRole role) => role == CharacterRole.Guardian
            ? "방위 링에 서서 한 방향을 지켜요."
            : "잔향을 모아 소환하면 위험한 곳으로 달려가요.";

        /// <summary>등급 색으로 칠한 별 문자열</summary>
        public static string StarsText(ItemGrade grade) => $"<color={UITheme.ColorTag(UITheme.GradeColor(grade))}>{UITheme.Stars(grade)}</color>";

        public static string SourceLabel(ItemSource source)
        {
            if (source == null) return string.Empty;
            if (!string.IsNullOrEmpty(source.label)) return source.label;
            return source.type switch
            {
                ItemSourceType.Stage => "스테이지 보상",
                ItemSourceType.Expedition => "원정",
                ItemSourceType.Dungeon => $"잔향이 던전 {source.targetId}층",
                ItemSourceType.Shop => "상점",
                ItemSourceType.Event => "이벤트",
                _ => source.type.ToString(),
            };
        }

        // ───────────── 조사 ─────────────

        /// <summary>목적격 조사. 예) 짝짝이를, 단추를, 별조각을</summary>
        public static string WithObject(string word) => Attach(word, "을", "를", "을(를)");

        /// <summary>보조사. 예) 짝짝이는, 별조각은</summary>
        public static string WithTopic(string word) => Attach(word, "은", "는", "은(는)");

        /// <summary>방향 조사. ㄹ 받침과 받침 없음은 "로". 예) 밤지기 짝짝이로, 별빛 세탁소로</summary>
        public static string WithDirection(string word)
        {
            if (!TryGetFinal(word, out int final)) return word + "(으)로";
            return word + (final == 0 || final == 8 ? "로" : "으로");
        }

        /// <summary>
        /// 받침에 맞춰 조사를 붙인다. 한글이 아니면 숫자 · 로마자는 읽는 소리로 (MetaText.HasFinalConsonant — '원정 2-4는', '1-3을'),
        /// 그 밖의 글자(기호 등)는 '을(를)'처럼 둘 다 쓴다.
        /// </summary>
        static string Attach(string word, string withFinal, string withoutFinal, string unknown)
        {
            if (string.IsNullOrEmpty(word)) return word;
            if (TryGetFinal(word, out int final)) return word + (final != 0 ? withFinal : withoutFinal);
            char last = word[word.Length - 1];
            bool readable = last < 0x80 && char.IsLetterOrDigit(last);
            if (!readable) return word + unknown;
            return word + (MetaText.HasFinalConsonant(last) ? withFinal : withoutFinal);
        }

        /// <summary>마지막 한글 음절의 종성 인덱스 (0 = 받침 없음, 8 = ㄹ)</summary>
        static bool TryGetFinal(string word, out int final)
        {
            final = 0;
            if (string.IsNullOrEmpty(word)) return false;
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return false;
            final = (last - 0xAC00) % 28;
            return true;
        }

        // ───────────── 특성 / 시너지 ─────────────

        /// <summary>계보 특성 칩 채우기 (특성 수보다 많은 칩은 숨김). glass = 초상화/밤 패널 위 반투명 칩</summary>
        public static void SetTraitChips(CharacterChip[] chips, LineageDef lineage, bool glass, float alpha = 1f)
        {
            if (chips == null) return;
            int count = lineage != null && lineage.traits != null ? lineage.traits.Count : 0;
            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] == null) continue;
                bool visible = i < count;
                chips[i].gameObject.SetActive(visible);
                if (!visible) continue;
                chips[i].SetTrait(lineage.traits[i], glass);
                if (alpha < 1f) chips[i].SetAlpha(alpha);
            }
        }

        /// <summary>특성 이름 나열 (특성 색). 예) "천 · 밤"</summary>
        public static string TraitList(LineageDef lineage, bool onParchment)
        {
            if (lineage == null || lineage.traits == null || lineage.traits.Count == 0) return "-";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lineage.traits.Count; i++)
            {
                if (i > 0) sb.Append("  ·  ");
                var color = UITheme.TraitColor(lineage.traits[i]);
                if (onParchment) color = CharacterSkillUI.InkTint(color);
                sb.Append("<color=").Append(UITheme.ColorTag(color)).Append('>').Append(UITheme.TraitName(lineage.traits[i])).Append("</color>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 계보가 관여하는 시너지를 켜진 것 먼저 모은다. styleFilter가 있으면 다른 성향의 역할 시너지는 뺀다 (형태 정보용).
        /// statuses에는 현재 편성 기준 상태를 ID별로 채운다. 화면 갱신 때만 부른다.
        /// </summary>
        public static void CollectSynergies(string lineageId, CombatStyle? styleFilter, List<SynergyDef> result, Dictionary<string, SynergyStatus> statuses)
        {
            result.Clear();
            statuses.Clear();
            var synergy = Game.Synergy;
            if (synergy == null || string.IsNullOrEmpty(lineageId)) return;

            var all = synergy.GetStatuses(true);
            if (all != null)
            {
                foreach (var status in all)
                {
                    if (status?.Def != null && !string.IsNullOrEmpty(status.Def.id)) statuses[status.Def.id] = status;
                }
            }

            var defs = synergy.GetSynergiesFor(lineageId);
            if (defs == null) return;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantActive = pass == 0;
                foreach (var def in defs)
                {
                    if (def == null) continue;
                    if (styleFilter.HasValue && def.kind == SynergyKind.Style && def.style != styleFilter.Value) continue;
                    bool active = statuses.TryGetValue(def.id ?? string.Empty, out var status) && status.IsActive;
                    if (active == wantActive) result.Add(def);
                }
            }
        }

        // ───────────── 공용 동작 ─────────────

        /// <summary>
        /// 형태를 눌렀을 때 (진화 기억도 노드 / 형태별 스킬 줄 공용):
        /// 해금 → 형태 정보, 단서 발견 → 진화 레시피, 미발견 → 단서 조건 안내.
        /// </summary>
        public static void OpenForm(LineageDef lineage, CharacterFormDef form)
        {
            if (lineage == null || form == null) return;
            if (!IsOwned(lineage))
            {
                Toast($"소환에서 {WithObject(lineage.name)} 만나면 진화 기억도가 열려요.");
                return;
            }
            switch (Game.Evolution.GetState(form.id))
            {
                case EvolutionState.Unlocked:
                    UIManager.Instance.Get<FormInfoPopup>().Show(form.id);
                    break;
                case EvolutionState.ClueFound:
                    UIManager.Instance.Get<EvolutionRecipePopup>().Show(form.id);
                    break;
                default:
                    Toast(UndiscoveredHint(lineage, form));
                    break;
            }
        }

        public static string UndiscoveredHint(LineageDef lineage, CharacterFormDef form)
        {
            var parent = Db.GetForm(form.parentFormId);
            if (parent != null && !Game.Characters.IsFormUnlocked(parent.id))
                return $"{WithObject(parent.name)} 먼저 해금하고 계보 Lv.{form.clueLevel}에 도달하면 단서가 나타나요.";
            int level = Game.Characters.GetLevel(lineage.id);
            return $"계보 Lv.{form.clueLevel}에 도달하면 {form.name}의 단서가 나타나요. (현재 Lv.{level})";
        }

        /// <summary>
        /// 열린 팝업을 모두 닫는다 (다른 탭/화면으로 이동하기 전에). UIManager.CloseAll — 닫기 연출은 있고 닫기 정책(소환 결과 건너뛰기 ·
        /// 제작 중 무시)은 거치지 않는다 (CloseTop을 되풀이하면 막는 팝업에서 멈춘다).
        /// </summary>
        public static void CloseAllPopups()
        {
            var ui = UIManager.Instance;
            if (ui != null) ui.CloseAll();
        }

        /// <summary>토스트 (종류는 글자로 짐작 — '부족' · '잠겨' 등은 Error)</summary>
        public static void Toast(string message)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowToast(message);
        }

        /// <summary>토스트 (종류를 정해서 — 해금 · 획득 같은 좋은 소식은 Success)</summary>
        public static void Toast(string message, ToastKind kind)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowToast(message, kind);
        }

        /// <summary>
        /// 진행 막대 채움을 목표로 옮긴다 (M62 — UIMotion.EaseFill: 늘어날 때만 부드럽게 따라가고, 줄어들거나 거의 같으면 바로 목표).
        /// dt는 unscaled. 다른 대상으로 다시 묶을 때 · 켜질 때는 부르는 쪽이 바로 맞춘다 (진화 제작 진행 · 레시피 조건 막대).
        /// </summary>
        public static float EaseFill(float current, float target, float dt) => UIMotion.EaseFill(current, target, dt);

        // ───────────── 재료 모으기 (M17) ─────────────

        /// <summary>획득처 종류에 맞는 동사: 상점 · 이벤트 = '사러 가기', 그 밖(원정 · 던전 · 전투) = '모으러 가기'</summary>
        internal static string CollectVerb(ItemSourceType type) =>
            type == ItemSourceType.Shop || type == ItemSourceType.Event ? "사러 가기" : "모으러 가기";

        /// <summary>
        /// "재료 모으러 가기" 버튼 글 (M17 · M72): '{재료} {사러 가기|모으러 가기} · {목적지}' — 예) '수선 천 사러 가기 · 수선점 상점'.
        /// 목적지는 획득처 표시 이름(SourceLabel). 갈 곳이 없으면(source null) '{재료} 모으러 가기'만.
        /// </summary>
        internal static string CollectLabel(string itemName, ItemSource source)
        {
            if (source == null) return $"{itemName} 모으러 가기";
            return $"{itemName} {CollectVerb(source.type)} · {SourceLabel(source)}";
        }

        /// <summary>잠긴 획득처 토스트: '{재료}은(는) {이유}' — 예) '수선 천은 스테이지 2-9를 클리어하면 열려요'. 이유가 없으면 '지금은 얻을 곳이 없어요'</summary>
        internal static string LockedSourceToast(string itemName, string reason)
        {
            if (string.IsNullOrEmpty(reason)) reason = "지금은 얻을 곳이 없어요";
            return string.IsNullOrEmpty(itemName) ? reason : $"{WithTopic(itemName)} {reason}";
        }

        /// <summary>
        /// 재료 획득처로 간다 (레시피 '재료 모으러 가기' · 획득처 줄 '가기 ›' 공용, M17 · M72). 잠금은 팝업을 닫기 전에 본다:
        ///   · 잠김 → '{재료}은(는) {이유}' 토스트, 팝업은 그대로
        ///   · 스테이지 → '전투(스테이지)에서 가끔 얻을 수 있어요' 토스트, 이동 없음
        ///   · 갈 수 있음 → 돌아가기 칸 '‹ {형태} 레시피로' → 팝업을 모두 닫고 → UINav.GoToSource (그 시트가 열린다)
        /// 이동했으면 true.
        /// </summary>
        public static bool GoToSource(ItemDef item, ItemSource source, string formId)
        {
            if (source == null) return false;
            var check = SourceAvailability.Check(source, item != null ? item.id : null);
            if (!check.Available)
            {
                Toast(LockedSourceToast(item != null ? item.name : null, check.Reason));
                return false;
            }
            if (source.type == ItemSourceType.Stage)
            {
                Toast(SourceAvailability.StageHint);
                return false;
            }
            PushRecipeReturn(formId);
            CloseAllPopups();
            UINav.GoToSource(source);
            return true;
        }

        /// <summary>
        /// 진화 조건 줄 '›' (M71): 조건을 채우는 곳으로 간다. 잠긴 기능이면 팝업을 닫지 않고 이유 토스트.
        ///   · 계보 레벨 → 레시피를 닫고 바로 아래 친구 상세 (레벨업 버튼이 있는 곳)
        ///   · 원정 N회 → 원정 시트 / 던전 N층 → 원정 시트의 잔향이 던전 (돌아가기 칸 '‹ {형태} 레시피로')
        ///   · 잔향이 출격 · 지킴이 처치 → 그 쪽(잔향이 덱 · 지킴이 칸)에 편성되지 않은 친구면 편성 시트 + 그 카드 반짝 (돌아가기 칸), 편성돼 있으면 전투(MAIN)
        ///   · 스테이지 도달 → 전투(MAIN)
        /// MAIN으로 갈 때는 돌아가기 칸을 넣지 않는다 (MAIN에는 칩이 없고 MAIN이 칸을 비운다).
        /// </summary>
        internal static void GoToCondition(string formId, ProgressType type)
        {
            var lineage = string.IsNullOrEmpty(formId) ? null : Db.GetLineageOfForm(formId);
            switch (type)
            {
                case ProgressType.LineageLevel:
                {
                    var ui = UIManager.Instance;
                    if (ui != null && ui.TryGet(out EvolutionRecipePopup recipe) && recipe.IsOpen) recipe.Close();
                    if (lineage != null) UINav.OpenCharacter(lineage.id, CharacterDetailPopup.TabTree);
                    Toast("레벨업 버튼으로 계보 레벨을 올려 보세요.");
                    return;
                }
                case ProgressType.ExpeditionCount:
                    if (!FeatureOpen(FeatureId.Expedition)) return;
                    PushRecipeReturn(formId);
                    CloseAllPopups();
                    UINav.GoToTab(MainTab.Expedition);
                    return;
                case ProgressType.DungeonFloor:
                    if (!FeatureOpen(FeatureId.Dungeon)) return;
                    PushRecipeReturn(formId);
                    CloseAllPopups();
                    UINav.GoToSource(ItemSourceType.Dungeon, null);
                    return;
                case ProgressType.EchoDeployCount:
                case ProgressType.GuardianKillCount:
                {
                    // 기록은 맞는 쪽에서만 센다 (BattleSystem: 잔향이 출격 = 잔향이 덱, 지킴이 처치 = 지킴이 칸) — 반대쪽에만 있으면 편성 시트로
                    var side = type == ProgressType.EchoDeployCount ? CharacterRole.Echo : CharacterRole.Guardian;
                    if (lineage != null && Game.Formation != null && !IsLineageOnSide(lineage.id, side) && IsFeatureUnlocked(FeatureId.Formation))
                    {
                        PushRecipeReturn(formId);
                        CloseAllPopups();
                        UINav.GoToTab(MainTab.Formation);
                        FormationPanel.PulseRosterCard(lineage.id);
                        Toast(side == CharacterRole.Echo
                            ? $"{WithObject(lineage.name)} 잔향이 덱에 편성하면 전투에서 조건이 채워져요."
                            : $"{WithObject(lineage.name)} 지킴이로 편성하면 전투에서 조건이 채워져요.");
                        return;
                    }
                }
                    CloseAllPopups();
                    UINav.GoToMain();
                    Toast(type == ProgressType.EchoDeployCount ? "전투에서 이 친구를 잔향이로 소환해 보세요." : "전투에서 지킴이로 적을 물리쳐 보세요.");
                    return;
                default:
                    CloseAllPopups();
                    UINav.GoToMain();
                    Toast("전투에서 스테이지를 올려 보세요.");
                    return;
            }
        }

        static bool IsFeatureUnlocked(FeatureId feature) => Game.Features == null || Game.Features.IsUnlocked(feature);

        /// <summary>그 계보의 형태가 한쪽(지킴이 칸 · 열린 잔향이 덱 칸)에 놓여 있는지 (누를 때만 부른다)</summary>
        static bool IsLineageOnSide(string lineageId, CharacterRole side)
        {
            var formation = Game.Formation;
            if (formation == null || string.IsNullOrEmpty(lineageId)) return false;
            bool guardian = side == CharacterRole.Guardian;
            int count = guardian ? FormationSystem.GuardianSlotCount : formation.UnlockedEchoSlots;
            for (int i = 0; i < count; i++)
            {
                string placed = guardian ? formation.GetGuardian(i) : formation.GetEcho(i);
                if (placed != null && Db.GetLineageOfForm(placed)?.id == lineageId) return true;
            }
            return false;
        }

        // ───────────── 열린 탭 안의 잠금 (F-03, SPEC4 §5.1) ─────────────

        /// <summary>기능이 열렸는지 (토스트 없음 — 게임 · 잠금 정보가 없으면 열림)</summary>
        internal static bool IsUnlocked(FeatureId feature) => IsFeatureUnlocked(feature);

        /// <summary>스테이지 꼴 잠금 표시인지 ("1-10" · "2-5" — FeatureGateSystem.GetLockedHint)</summary>
        internal static bool IsStageHint(string hint) =>
            !string.IsNullOrEmpty(hint) && char.IsDigit(hint[0]) && hint.IndexOf('-') > 0;

        /// <summary>작은 버튼 자리 잠금 글 (M39 짧은 꼴): 스테이지면 '{1-10} 클리어 시', 아니면 '잠겨 있어요'</summary>
        internal static string LockedShort(string hint) => IsStageHint(hint) ? $"{hint} 클리어 시" : "잠겨 있어요";

        /// <summary>넓은 버튼 자리 잠금 글: 스테이지면 '스테이지 {1-10} 클리어 시 열려요', 아니면 sentence (없으면 '아직 열리지 않았어요')</summary>
        internal static string LockedLong(string hint, string sentence) =>
            IsStageHint(hint) ? $"스테이지 {hint} 클리어 시 열려요" : string.IsNullOrEmpty(sentence) ? "아직 열리지 않았어요" : sentence;

        /// <summary>그 기능의 짧은 잠금 표시 ("1-10", 열렸거나 모르면 빈 문자열)</summary>
        internal static string LockedHint(FeatureId feature) => Game.Features != null ? Game.Features.GetLockedHint(feature) : string.Empty;

        /// <summary>잠금 안내 문장 (M39 — FeatureGateSystem.GetRequirementText, 없으면 '아직 열리지 않았어요')</summary>
        internal static string LockedSentence(FeatureId feature)
        {
            string text = Game.Features != null ? Game.Features.GetRequirementText(feature) : string.Empty;
            return string.IsNullOrEmpty(text) ? "아직 열리지 않았어요" : text;
        }

        /// <summary>잠긴 기능을 눌렀을 때 (F-03): 안내 문장 토스트(Error) + 약한 진동 (M18)</summary>
        internal static void DenyLocked(FeatureId feature)
        {
            Toast(LockedSentence(feature), ToastKind.Error);
            Haptics.Play(HapticKind.Light);
        }

        /// <summary>기능이 열렸는지. 잠겼으면 열리는 조건 토스트 (팝업은 그대로)</summary>
        internal static bool FeatureOpen(FeatureId feature)
        {
            if (IsFeatureUnlocked(feature)) return true;
            Toast(Game.Features.GetRequirementText(feature));
            return false;
        }

        /// <summary>돌아가기 칸 '‹ {형태} 레시피로' — 누르면 그 계보 상세(진화 기억도) 위에 이 레시피를 다시 연다 (M72)</summary>
        static void PushRecipeReturn(string formId)
        {
            var form = string.IsNullOrEmpty(formId) ? null : Db.GetForm(formId);
            var lineage = form != null ? Db.GetLineageOfForm(formId) : null;
            if (form == null || lineage == null) return;
            string lineageId = lineage.id;
            UINav.PushReturn($"‹ {form.name} 레시피로", () => ReopenRecipe(lineageId, formId));
        }

        static void ReopenRecipe(string lineageId, string formId)
        {
            UINav.OpenCharacter(lineageId, CharacterDetailPopup.TabTree);
            var ui = UIManager.Instance;
            if (ui != null && ui.TryGet(out EvolutionRecipePopup recipe)) recipe.Show(formId);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// 앵커로 채우는 막대 (0~1). 둥근 9-slice 스프라이트도 모서리가 찌그러지지 않는다.
        /// 0이거나 테두리 안쪽 여백보다 짧아지면 채움 그림을 끈다 (폭이 0 이하인 9-slice가 뒤집혀 그려지지 않게).
        /// </summary>
        public static void SetFill(RectTransform fill, float amount)
        {
            if (fill == null) return;
            amount = Mathf.Clamp01(amount);
            fill.anchorMin = new Vector2(0f, fill.anchorMin.y);
            fill.anchorMax = new Vector2(amount, fill.anchorMax.y);
            if (!fill.TryGetComponent<Graphic>(out var graphic)) return;
            bool visible = amount > 0.001f;
            // 부모 크기가 아직 정해지지 않았으면(레이아웃 전) 폭 검사는 건너뛴다
            if (visible && fill.parent is RectTransform track && track.rect.width > 0f) visible = fill.rect.width > 0.5f;
            graphic.enabled = visible;
        }

        /// <summary>
        /// 아이템 아이콘. 받침(ui_plate_round)은 흰색 → 등급 색으로 45%만 물들인다 (아트 색을 유지).
        /// 아트가 없으면 받침 위에 이름 첫 글자로 대신한다.
        /// </summary>
        public static void SetItemIcon(Image icon, Image background, TMP_Text letter, ItemDef item)
        {
            var grade = item != null ? item.grade : ItemGrade.Common;
            bool hasArt = item != null && item.icon != null;
            if (background != null) background.color = WithAlpha(Color.Lerp(Color.white, UITheme.GradeColor(grade), 0.45f), hasArt ? 0.9f : 1f);
            if (icon != null)
            {
                icon.enabled = hasArt;
                icon.sprite = hasArt ? item.icon : null;
            }
            if (letter != null)
            {
                letter.gameObject.SetActive(!hasArt);
                letter.text = item != null && !string.IsNullOrEmpty(item.name) ? item.name.Substring(0, 1) : "?";
            }
        }
    }
}
