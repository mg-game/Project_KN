using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 형태 고유 스킬 원본: 형태 29종마다 하나씩 (ID = "skill_{formId}").
    /// 사물의 구조와 결함이 스킬이 된다 (짝을 잃은 양말 → 가족 부르기, 녹는 촛불 → 녹을수록 밝아짐 …).
    /// 진화형은 기본형 스킬을 강화하거나 갈래를 나눈다 (가족 부르기 → 식구 열차 / 꼭 붙잡기 → 단추 방패·회전 단추 …).
    /// 계보별 표는 SkillContent.Jjakjjak.cs(짝짝이 11종) / SkillContent.Lineages.cs(나머지 18종)에 있다.
    ///
    /// 발동 시점 분포: 쿨타임 14 · 패시브 1 · 소환 시 2 · 소멸 시 2 · 처치 시 3 · 불 밝힐 때 2 · 피격 시 2 · 위기 시 2 · 전투 시작 1
    /// (시너지 보너스 스킬이 전투 시작/처치 시/소멸 시를 더 채운다).
    /// 연출 키는 시너지 보너스 스킬까지 합쳐 SkillVfx 33개를 모두 "실제로 시전되는" 스킬에 쓴다 —
    /// 패시브는 전투에서 시전(SkillCast) 이벤트가 없어 연출·대사가 나오지 않으므로(캐릭터 화면 아이콘/대사로만 보인다) 세지 않는다.
    /// </summary>
    internal static partial class SkillContent
    {
        public const string IdPrefix = "skill_";

        public static string IdFor(string formId) => IdPrefix + formId;

        /// <summary>형태 ID → 고유 스킬. 부를 때마다 새 인스턴스를 만든다.</summary>
        public static Dictionary<string, SkillDef> Build()
        {
            var skills = new Dictionary<string, SkillDef>();
            Add(skills, Jjakjjak());
            Add(skills, Ttokdan());
            Add(skills, Chalkak());
            Add(skills, Mongdang());
            Add(skills, Udukeoni());
            Add(skills, Rouge());
            Add(skills, Lalabell());
            return skills;
        }

        static void Add(Dictionary<string, SkillDef> skills, IEnumerable<SkillDef> list)
        {
            foreach (var skill in list)
            {
                string formId = skill.id.Substring(IdPrefix.Length);
                if (!skills.TryAdd(formId, skill)) UnityEngine.Debug.LogError($"[SkillContent] 형태 '{formId}'의 스킬이 두 번 정의되었습니다.");
            }
        }

        /// <summary>형태 고유 스킬 (ID = "skill_{formId}")</summary>
        static SkillDef Form(string formId, string name, string vfx, string castText, SkillKit.When when, SkillKit.Aim aim,
            string story, params SkillEffectDef[] effects) =>
            SkillKit.Make(IdFor(formId), name, vfx, castText, when, aim, story, effects);
    }
}
