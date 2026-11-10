using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>시너지 도감/상세에서 친구 한 칸의 상태</summary>
    public enum SynergyMemberState
    {
        /// <summary>지금 편성돼 있음</summary>
        Placed = 0,
        /// <summary>보유 (편성 안 함)</summary>
        Owned = 1,
        /// <summary>계보는 보유했지만 이 형태는 아직 진화 제작 전</summary>
        FormLocked = 2,
        /// <summary>아직 만나지 못한 친구</summary>
        Unowned = 3,
    }

    /// <summary>시너지에 관여하는 친구 한 칸 (특성·조합은 계보 단위, 역할은 형태 단위)</summary>
    public struct SynergyMember
    {
        public LineageDef Lineage;
        /// <summary>보여줄 형태 (편성 중이면 편성된 형태, 아니면 기본형/해당 형태)</summary>
        public string FormId;
        public SynergyMemberState State;
        /// <summary>편성된 형태 수 (계보 단위면 지킴이 + 잔향이 = 최대 2)</summary>
        public int PlacedCount;
        /// <summary>편성돼 있다면 그 역할 (형태 단위 칸에서만 의미 있음)</summary>
        public CharacterRole Role;
    }

    /// <summary>드래그 미리보기에서 시너지가 어떻게 바뀌는지</summary>
    public enum SynergyDelta
    {
        None = 0,
        /// <summary>꺼져 있다가 새로 켜짐</summary>
        Activated = 1,
        /// <summary>더 높은 단계로</summary>
        Upgraded = 2,
        /// <summary>단계는 그대로, 수만 늘어남</summary>
        CountUp = 3,
        /// <summary>단계는 그대로, 수만 줄어듦</summary>
        CountDown = 4,
        /// <summary>낮은 단계로</summary>
        Downgraded = 5,
        /// <summary>켜져 있다가 꺼짐</summary>
        Lost = 6,
    }

    /// <summary>
    /// 시너지 화면(편성 시너지 칸, 상세, 도감) 공용 표기/색/집계 도우미.
    /// 시너지 계산 자체는 SynergySystem이 하고, 여기서는 보여주는 방법만 정한다.
    /// </summary>
    public static class SynergyUI
    {
        /// <summary>계보 공명 (지킴이 + 잔향이 같은 계보) 행 강조색</summary>
        public static readonly Color ResonanceColor = UITheme.Gold;

        // ───────────── 색 / 이름 ─────────────

        public static Color KindColor(SynergyKind kind) => kind switch
        {
            SynergyKind.Trait => UITheme.Lavender,
            SynergyKind.Style => UITheme.SoftBlue,
            SynergyKind.Combo => UITheme.DustyPink,
            _ => UITheme.SubText,
        };

        /// <summary>시너지 고유 강조색 (특성 색 / 역할 색 / 조합은 더스티 핑크)</summary>
        public static Color AccentColor(SynergyDef def)
        {
            if (def == null) return UITheme.Lavender;
            return def.kind switch
            {
                SynergyKind.Trait => UITheme.TraitColor(def.trait),
                SynergyKind.Style => UITheme.StyleColor(def.style),
                _ => UITheme.DustyPink,
            };
        }

        /// <summary>진행 표기 앞에 붙는 짧은 이름: 천 / 방어 / (조합은 시너지 이름)</summary>
        public static string ShortName(SynergyDef def)
        {
            if (def == null) return string.Empty;
            return def.kind switch
            {
                SynergyKind.Trait => UITheme.TraitName(def.trait),
                SynergyKind.Style => UITheme.StyleName(def.style),
                _ => def.name,
            };
        }

        public static string DisplayName(SynergyDef def)
        {
            if (def == null) return string.Empty;
            if (!string.IsNullOrEmpty(def.name)) return def.name;
            return def.kind == SynergyKind.Combo ? def.id : ShortName(def);
        }

        /// <summary>예: "특성 시너지 · 천"</summary>
        public static string Subtitle(SynergyDef def)
        {
            if (def == null) return string.Empty;
            string kind = UITheme.SynergyKindName(def.kind) + " 시너지";
            return def.kind switch
            {
                SynergyKind.Trait => $"{kind} · {UITheme.TraitName(def.trait)}",
                SynergyKind.Style => $"{kind} · {UITheme.StyleName(def.style)}",
                _ => $"{kind} · 친구 {RequiredCount(def)}명",
            };
        }

        /// <summary>아이콘이 없을 때 원 안에 넣을 글자</summary>
        public static string Initial(SynergyDef def)
        {
            var name = ShortName(def);
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
        }

        // ───────────── 단계 / 진행 ─────────────

        public static int TierCount(SynergyDef def) => def?.tiers != null ? def.tiers.Count : 0;

        public static int TierThreshold(SynergyDef def, int index)
        {
            if (def?.tiers == null || index < 0 || index >= def.tiers.Count || def.tiers[index] == null) return 0;
            return def.kind == SynergyKind.Combo ? RequiredCount(def) : Mathf.Max(1, def.tiers[index].count);
        }

        /// <summary>처음 켜지는 수 (조합은 필요한 계보 수)</summary>
        public static int RequiredCount(SynergyDef def)
        {
            if (def == null) return 0;
            if (def.kind == SynergyKind.Combo) return def.lineageIds != null ? def.lineageIds.Count : 0;
            return TierCount(def) > 0 ? TierThreshold(def, 0) : 0;
        }

        /// <summary>가장 높은 단계의 수 (진행 막대 끝)</summary>
        public static int MaxCount(SynergyDef def)
        {
            if (def == null) return 1;
            if (def.kind == SynergyKind.Combo) return Mathf.Max(1, RequiredCount(def));
            int max = 1;
            for (int i = 0; i < TierCount(def); i++) max = Mathf.Max(max, TierThreshold(def, i));
            return max;
        }

        /// <summary>켜져 있고 더 올라갈 단계가 없음</summary>
        public static bool IsMaxTier(SynergyStatus status) => status != null && status.IsActive && status.NextTier == null;

        /// <summary>
        /// 진행 표기. 특성/역할: "천 3/4" (최고 단계면 "천 4 최대"), 조합: "2/3" (완성이면 "완성").
        /// rich면 숫자에 색을 입힌다.
        /// </summary>
        public static string ProgressLabel(SynergyStatus status, bool rich = true, bool withName = true)
        {
            if (status?.Def == null) return string.Empty;
            var def = status.Def;
            string gold = UITheme.ColorTag(UITheme.Gold);
            if (def.kind == SynergyKind.Combo)
            {
                if (status.IsActive) return rich ? $"<color={gold}>완성</color>" : "완성";
                string countText = rich ? $"<color={gold}>{status.Count}</color>" : status.Count.ToString();
                return $"{countText}/{RequiredCount(def)}";
            }

            string prefix = withName ? ShortName(def) + " " : string.Empty;
            string count = rich && status.Count > 0 ? $"<color={gold}>{status.Count}</color>" : status.Count.ToString();
            if (IsMaxTier(status)) return $"{prefix}{count} 최대";
            int next = status.NextTier != null ? Mathf.Max(1, status.NextTier.count) : RequiredCount(def);
            return $"{prefix}{count}/{next}";
        }

        /// <summary>아직 꺼져 있지만 한 명만 더 있으면 켜지는 시너지</summary>
        public static bool IsClose(SynergyStatus status)
        {
            if (status?.Def == null || status.IsActive || status.Count <= 0) return false;
            return RequiredCount(status.Def) - status.Count <= 1;
        }

        /// <summary>"2 · 3 · 4" — 도달한 단계 수는 금색, 지금 단계는 굵게</summary>
        public static string Thresholds(SynergyDef def, int activeTier, StringBuilder sb)
        {
            sb.Clear();
            int count = TierCount(def);
            string gold = UITheme.ColorTag(UITheme.Gold);
            string dim = UITheme.ColorTag(UITheme.SubText);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append($"<color={dim}> · </color>");
                int threshold = TierThreshold(def, i);
                if (i == activeTier) sb.Append($"<b><color={gold}>{threshold}</color></b>");
                else if (i < activeTier) sb.Append($"<color={gold}>{threshold}</color>");
                else sb.Append($"<color={dim}>{threshold}</color>");
            }
            return sb.ToString();
        }

        /// <summary>단계 효과 줄: "· 공격력 +15%" (DescribeModifier). 효과가 없으면 빈 문자열</summary>
        public static string ModifierLines(SynergyTier tier, StringBuilder sb, float multiplier = 1f)
        {
            sb.Clear();
            if (tier?.modifiers == null) return string.Empty;
            foreach (var modifier in tier.modifiers)
            {
                if (modifier == null) continue;
                var shown = modifier;
                if (!Mathf.Approximately(multiplier, 1f)) shown = new StatModifier { stat = modifier.stat, value = modifier.value * multiplier };
                var line = SynergySystem.DescribeModifier(shown);
                if (string.IsNullOrEmpty(line)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("· ").Append(line);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 능력치 이름과 수치 사이 빈칸을 줄바꿈 없는 빈칸(UIFormat.Nbsp)으로 바꾼다 (M28): "공격력 +8%" · "스킬 쿨타임 -8%" · "효과 ×1.5".
        /// 줄이 넘칠 때 '+8%'만 다음 줄로 떨어지지 않고 '공격력 +8%'가 함께 넘어간다 (도감 줄 · 단계 설명 · 상세 설명).
        /// 수치 바로 앞이 여러 낱말 능력치 이름(MultiWordStatLabels — '스킬 쿨타임' · '공격 속도' · '치명타 피해' · '치명타 확률' · '유지 시간' …)이면
        /// 그 이름 안 빈칸도 묶는다 ('스킬' / '쿨타임 -8%'로 갈라지지 않게, Wave 2 r2). 이름 앞 낱말 경계가 맞을 때만 (낱말 가운데는 묶지 않는다).
        /// 묶을 때(Set) 한 번만 부른다 — 매 프레임 아님. 바꿀 것이 없으면 받은 문자열을 그대로 돌려준다 (할당 없음).
        /// </summary>
        public static string KeepStatNumbers(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            char[] chars = null;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] != ' ' || !IsStatNumberLead(text[i + 1])) continue;
                chars ??= text.ToCharArray();
                chars[i] = UIFormat.NbspChar;
                BindStatLabelBefore(text, i, chars);
            }
            return chars == null ? text : new string(chars);
        }

        /// <summary>수치 앞 글자: '+' · '-' · '−'(U+2212) · '×'(U+00D7)</summary>
        static bool IsStatNumberLead(char c) => c == '+' || c == '-' || c == '−' || c == '×';

        /// <summary>
        /// 수치 앞에서 함께 묶는 여러 낱말 능력치 이름 (긴 것부터 — '잔향이 유지 시간'이 '유지 시간'보다 먼저 맞는다).
        /// 기본 다섯 + 시너지 능력치 이름표(SynergySystem.StatName) 중 빈칸이 든 것. 처음 쓸 때 한 번 만든다
        /// </summary>
        internal static readonly string[] MultiWordStatLabels = BuildMultiWordStatLabels();

        static string[] BuildMultiWordStatLabels()
        {
            var labels = new List<string> { "스킬 쿨타임", "공격 속도", "치명타 피해", "치명타 확률", "유지 시간" };
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                string name = SynergySystem.StatName(stat);
                if (!string.IsNullOrEmpty(name) && name.IndexOf(' ') > 0 && !labels.Contains(name)) labels.Add(name);
            }
            labels.Sort((a, b) => b.Length != a.Length ? b.Length.CompareTo(a.Length) : string.CompareOrdinal(a, b));
            return labels.ToArray();
        }

        /// <summary>수치 앞 빈칸(gap) 바로 앞이 여러 낱말 능력치 이름으로 끝나면 (이름 앞이 글 처음 · 빈칸 · 문장부호 · 태그 끝) 그 이름 안 빈칸을 묶는다</summary>
        static void BindStatLabelBefore(string text, int gap, char[] chars)
        {
            for (int l = 0; l < MultiWordStatLabels.Length; l++)
            {
                string label = MultiWordStatLabels[l];
                int start = gap - label.Length;
                if (start < 0 || string.CompareOrdinal(text, start, label, 0, label.Length) != 0) continue;
                if (start > 0 && char.IsLetterOrDigit(text[start - 1])) continue;
                for (int k = start; k < gap; k++)
                {
                    if (text[k] == ' ') chars[k] = UIFormat.NbspChar;
                }
                return;
            }
        }

        /// <summary>미리보기 전후 비교 (before가 null이면 0명·꺼짐으로 본다)</summary>
        public static SynergyDelta Compare(SynergyStatus before, SynergyStatus after)
        {
            int beforeTier = before != null ? before.TierIndex : -1;
            int afterTier = after != null ? after.TierIndex : -1;
            int beforeCount = before != null ? before.Count : 0;
            int afterCount = after != null ? after.Count : 0;

            if (beforeTier < 0 && afterTier >= 0) return SynergyDelta.Activated;
            if (beforeTier >= 0 && afterTier < 0) return SynergyDelta.Lost;
            if (afterTier > beforeTier) return SynergyDelta.Upgraded;
            if (afterTier < beforeTier) return SynergyDelta.Downgraded;
            if (afterCount > beforeCount) return SynergyDelta.CountUp;
            if (afterCount < beforeCount) return SynergyDelta.CountDown;
            return SynergyDelta.None;
        }

        public static bool IsGain(SynergyDelta delta) => delta == SynergyDelta.Activated || delta == SynergyDelta.Upgraded;

        public static bool IsLoss(SynergyDelta delta) => delta == SynergyDelta.Lost || delta == SynergyDelta.Downgraded;

        // ───────────── 관여 / 친구 목록 ─────────────

        /// <summary>이 형태(계보)가 이 시너지의 수에 들어가는지</summary>
        public static bool Involves(SynergyDef def, LineageDef lineage, CharacterFormDef form)
        {
            if (def == null || lineage == null) return false;
            return def.kind switch
            {
                SynergyKind.Trait => lineage.traits != null && lineage.traits.Contains(def.trait),
                SynergyKind.Style => form != null && form.style == def.style,
                SynergyKind.Combo => def.lineageIds != null && def.lineageIds.Contains(lineage.id),
                _ => false,
            };
        }

        public static bool Involves(SynergyDef def, string formId)
        {
            if (def == null || string.IsNullOrEmpty(formId) || !Game.IsReady) return false;
            var db = Game.Characters.Db;
            return Involves(def, db.GetLineageOfForm(formId), db.GetForm(formId));
        }

        /// <summary>
        /// 시너지에 관여하는 친구 목록. 특성/조합 = 계보 단위(조합은 데이터 순서), 역할 = 그 역할을 가진 형태 단위.
        /// 편성 중 → 보유 → 미해금 → 미보유 순으로 정렬한다 (조합은 순서 유지).
        /// </summary>
        public static void CollectMembers(SynergyDef def, List<SynergyMember> into)
        {
            into.Clear();
            if (def == null || !Game.IsReady) return;
            var db = Game.Characters.Db;

            switch (def.kind)
            {
                case SynergyKind.Combo:
                    if (def.lineageIds == null) return;
                    foreach (var lineageId in def.lineageIds)
                    {
                        var lineage = db.GetLineage(lineageId);
                        if (lineage != null) into.Add(LineageMember(lineage));
                    }
                    return;
                case SynergyKind.Trait:
                    foreach (var lineage in db.lineages)
                    {
                        if (lineage?.traits != null && lineage.traits.Contains(def.trait)) into.Add(LineageMember(lineage));
                    }
                    break;
                case SynergyKind.Style:
                    foreach (var lineage in db.lineages)
                    {
                        if (lineage?.forms == null) continue;
                        foreach (var form in lineage.forms)
                        {
                            if (form != null && form.style == def.style) into.Add(FormMember(lineage, form));
                        }
                    }
                    break;
            }
            SortByState(into);
        }

        static SynergyMember LineageMember(LineageDef lineage)
        {
            var member = new SynergyMember { Lineage = lineage, FormId = lineage.baseFormId, State = SynergyMemberState.Unowned };
            if (!Game.Characters.IsOwned(lineage.id)) return member;

            member.State = SynergyMemberState.Owned;
            if (lineage.forms == null) return member;
            foreach (var form in lineage.forms)
            {
                if (form == null || !Game.Formation.IsPlaced(form.id, out var role, out _)) continue;
                // 지킴이로 나간 형태를 먼저 보여준다
                if (member.PlacedCount == 0 || role == CharacterRole.Guardian)
                {
                    member.FormId = form.id;
                    member.Role = role;
                }
                member.PlacedCount++;
            }
            if (member.PlacedCount > 0) member.State = SynergyMemberState.Placed;
            return member;
        }

        static SynergyMember FormMember(LineageDef lineage, CharacterFormDef form)
        {
            var member = new SynergyMember { Lineage = lineage, FormId = form.id, State = SynergyMemberState.Unowned };
            if (Game.Formation.IsPlaced(form.id, out var role, out _))
            {
                member.State = SynergyMemberState.Placed;
                member.PlacedCount = 1;
                member.Role = role;
            }
            else if (Game.Characters.IsFormUnlocked(form.id)) member.State = SynergyMemberState.Owned;
            else if (Game.Characters.IsOwned(lineage.id)) member.State = SynergyMemberState.FormLocked;
            return member;
        }

        /// <summary>상태 순 안정 정렬 (같은 상태끼리는 데이터 순서 유지)</summary>
        static void SortByState(List<SynergyMember> members)
        {
            for (int i = 1; i < members.Count; i++)
            {
                var item = members[i];
                int j = i - 1;
                while (j >= 0 && members[j].State > item.State)
                {
                    members[j + 1] = members[j];
                    j--;
                }
                members[j + 1] = item;
            }
        }

        public static Color MemberStateColor(SynergyMemberState state) => state switch
        {
            SynergyMemberState.Placed => UITheme.Gold,
            SynergyMemberState.Owned => UITheme.Lavender,
            SynergyMemberState.FormLocked => UITheme.Divider,
            _ => UITheme.Secondary,
        };

        public static string MemberStateName(SynergyMember member) => member.State switch
        {
            SynergyMemberState.Placed => member.PlacedCount > 1 ? $"편성 중 ×{member.PlacedCount}" : $"{UITheme.RoleName(member.Role)} 편성",
            SynergyMemberState.Owned => "보유",
            SynergyMemberState.FormLocked => "진화 전",
            _ => "미보유",
        };

        /// <summary>조합에서 아직 편성되지 않은 친구 이름. 예) "우두커니와 라라벨" (없으면 빈 문자열)</summary>
        public static string MissingNames(SynergyDef def, StringBuilder sb)
        {
            sb.Clear();
            NameBuffer.Clear();
            if (def?.lineageIds == null || !Game.IsReady) return string.Empty;
            foreach (var lineageId in def.lineageIds)
            {
                if (Game.Formation.IsLineagePlaced(lineageId)) continue;
                var lineage = Game.Characters.Db.GetLineage(lineageId);
                if (lineage != null) NameBuffer.Add(lineage.name);
            }
            // "A", "A와 B", "A, B와 C"
            for (int i = 0; i < NameBuffer.Count; i++)
            {
                bool beforeLast = i == NameBuffer.Count - 2;
                if (beforeLast) sb.Append(MenuText.And(NameBuffer[i])).Append(' ');
                else
                {
                    sb.Append(NameBuffer[i]);
                    if (i < NameBuffer.Count - 1) sb.Append(", ");
                }
            }
            return sb.ToString();
        }

        static readonly List<string> NameBuffer = new();

        public static int OwnedComboFriends(SynergyDef def)
        {
            if (def?.lineageIds == null || !Game.IsReady) return 0;
            int owned = 0;
            foreach (var lineageId in def.lineageIds)
            {
                if (Game.Characters.IsOwned(lineageId)) owned++;
            }
            return owned;
        }

        // ───────────── 스테이지 기믹 ─────────────

        /// <summary>지금 전투 중인 스테이지의 기믹 (없으면 None)</summary>
        public static StageModifierType CurrentStageModifier()
        {
            if (!Game.IsReady || Game.Battle == null) return StageModifierType.None;
            var sim = Game.Battle.Sim;
            return sim != null ? sim.StageModifier : StageModifierType.None;
        }

        public static bool IsFavoredNow(SynergyDef def, StageModifierType current) =>
            def != null && def.favoredModifier != StageModifierType.None && def.favoredModifier == current;

        public static string Multiplier(SynergyDef def) => def != null ? $"×{def.favoredMultiplier:0.##}" : string.Empty;
    }
}
