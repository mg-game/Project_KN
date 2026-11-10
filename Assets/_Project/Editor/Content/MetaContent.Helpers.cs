using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 메타 콘텐츠 공용 도구: 탭 패널 안쪽 화면 경로, 보상 줄임말, 스테이지 표기와 조사, 아트 고르기.
    /// 담당: M2.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>
        /// 코치마크·손가락이 가리킬 탭 패널 안쪽 대상 (Canvas 기준 경로). CoachTargets에 없는 것만 둔다.
        /// 이름은 씬 빌더에서 확인했다 (MenuSceneBuilder.BuildGrowthPanel / BuildFormationPanel / BuildSynergyPanel /
        /// BuildExpeditionPanel / BuildShopPanel, CharacterSceneBuilder.BuildGachaPanel / BuildCharacterPanel).
        /// 빌더 이름이 바뀌면 여기만 고치고, "IdleGame/Setup/메타 화면 대상 확인" 메뉴로 다시 확인한다.
        /// 대상이 없거나 꺼져 있으면 코치마크는 말풍선만 띄우므로 진행이 막히지는 않는다.
        /// </summary>
        internal static class Targets
        {
            /// <summary>강화 탭 "강화 x1" 버튼</summary>
            public const string GrowthLevelUp1 = CoachTargets.GrowthPanel + "/Buttons/LevelUp1Button";
            /// <summary>강화 탭 친구 레벨 목록</summary>
            public const string GrowthLineageList = CoachTargets.GrowthPanel + "/LineageList";
            /// <summary>편성 탭 친구 카드 목록 (끌어 놓기 출발점)</summary>
            public const string FormationCardList = CoachTargets.FormationPanel + "/CardList";
            /// <summary>편성 탭 "팀 시너지" 패널</summary>
            public const string FormationSynergy = CoachTargets.FormationPanel + "/TeamSynergy";
            /// <summary>소환 탭 "1회 소환" 버튼</summary>
            public const string GachaPull1 = CoachTargets.GachaPanel + "/Buttons/Pull1Button";
            /// <summary>원정 탭 구역 목록</summary>
            public const string ExpeditionZoneList = CoachTargets.ExpeditionPanel + "/ZoneList";
            /// <summary>상점 탭 상품 목록</summary>
            public const string ShopProductList = CoachTargets.ShopPanel + "/ProductList";
            /// <summary>친구들 탭 카드 격자</summary>
            public const string CharacterGrid = CoachTargets.CharacterPanel + "/Grid";

            // ── 팝업 (IdleSceneBuilder: Canvas/"Popups", SceneParts.BuildPopup: {이름}/Window/Body) ──
            // 이동(navFeature)이 팝업을 여는 퀘스트·미션은 손가락을 그 팝업 안 버튼에 둔다. 코치마크는 팝업 위에 그려지지만
            // 팝업 밖 대상(상단 바 버튼, 전투 화면 바구니)은 팝업의 어둠 아래에 깔려 가리켜도 누를 수 없다 (MetaDataBuilder가 확인).

            /// <summary>팝업 루트 (Canvas 바로 아래)</summary>
            public const string PopupRoot = "Popups";
            /// <summary>수선 바구니 팝업 (MetaSceneBuilder.BuildIdleChestPopup) — IdleChest / QuickIdle 이동이 연다</summary>
            public const string IdleChestPopup = PopupRoot + "/IdleChestPopup";
            /// <summary>수선 일지 팝업 (MetaSceneBuilder.BuildJournalPopup) — Missions / Achievements 이동이 연다</summary>
            public const string JournalPopup = PopupRoot + "/JournalPopup";
            /// <summary>출석부 팝업 (MetaSceneBuilder.BuildAttendancePopup) — Attendance 이동이 연다</summary>
            public const string AttendancePopup = PopupRoot + "/AttendancePopup";
            /// <summary>
            /// 친구 상세 팝업 — Evolution(진화 제작 가능한 계보, 기억도 탭) / Costumes(해금 가능한 의상이 있는 계보, 의상 탭) 이동이 연다.
            /// Codex 이동은 팝업 없이 친구들 탭(도감 목록)에 머문다 (M73 — MetaNavigator.PickLineage)
            /// </summary>
            public const string CharacterDetailPopup = PopupRoot + "/CharacterDetailPopup";
            /// <summary>시너지 도감 팝업 — Synergy 이동이 연다 (SynergyNavigator)</summary>
            public const string SynergyCodexPopup = PopupRoot + "/SynergyCodexPopup";

            /// <summary>수선 바구니 팝업 [받기]</summary>
            public const string IdleChestClaim = IdleChestPopup + "/Window/Body/ClaimButton";
            /// <summary>수선 바구니 팝업 [빠른 수선] (받기 버튼과 나란히 있어 손가락이 꼭 필요하다)</summary>
            public const string IdleChestQuick = IdleChestPopup + "/Window/Body/QuickSection/QuickButton";
            /// <summary>수선 일지 팝업 [모두 받기]</summary>
            public const string JournalClaimAll = JournalPopup + "/Window/Body/ClaimAllButton";
        }

        // ───────────── 보상 줄임말 ─────────────

        static MetaReward Gem(double amount) => MetaReward.Gem(amount);

        static MetaReward Gold(double amount) => MetaReward.Gold(amount);

        static MetaReward Mat(string itemId, double count) => MetaReward.Item(itemId, count);

        /// <summary>단추 N시간치 (출석부 평일 칸)</summary>
        static MetaReward GoldHours(double hours) => MetaReward.GoldMinutes(hours * 60);

        static List<MetaReward> Rewards(params MetaReward[] rewards) => new(rewards);

        /// <summary>같은 종류(MetaReward.IsSameKind)는 양을 더하고, 새 종류는 끝에 붙인다 (보상 칸이 겹쳐 보이지 않게)</summary>
        static void MergeInto(List<MetaReward> into, IEnumerable<MetaReward> rewards)
        {
            foreach (var reward in rewards)
            {
                var same = into.Find(x => x.IsSameKind(reward));
                if (same != null) same.amount += reward.amount;
                else into.Add(reward.Clone());
            }
        }

        // ───────────── 스테이지 표기 ─────────────

        /// <summary>"1-4" (BattleSystem.FormatStage와 같은 표기)</summary>
        static string StageLabel(int chapter, int number) => $"{chapter}-{number}";

        /// <summary>
        /// 숫자를 소리 내어 읽었을 때 받침에 맞춘 목적격 조사. 1-4"를" / 1-3"을" / 1-10"을".
        /// 끝자리 0(십·백)·1(일)·3(삼)·6(육)·7(칠)·8(팔)은 받침이 있어 "을", 2(이)·4(사)·5(오)·9(구)는 "를".
        /// </summary>
        static string ObjectParticle(int number)
        {
            int digit = System.Math.Abs(number) % 10;
            return digit == 2 || digit == 4 || digit == 5 || digit == 9 ? "를" : "을";
        }

        /// <summary>"스테이지 1-4를 클리어하면 열려요"</summary>
        static string StageClearToast(int chapter, int number) =>
            $"스테이지 {StageLabel(chapter, number)}{ObjectParticle(number)} 클리어하면 열려요";

        /// <summary>"스테이지 1-4 클리어" (퀘스트 한 줄)</summary>
        static string StageClearText(int chapter, int number) => $"스테이지 {StageLabel(chapter, number)} 클리어";

        // ───────────── 아트 ─────────────

        /// <summary>메타 전용 그림 Art/Meta/{name}.png (아직 없으면 null)</summary>
        static Sprite MetaArt(string name) => ArtLib.Load($"Meta/{name}.png");

        /// <summary>후보 중 처음으로 있는 그림. 모두 없으면 null → UI가 자리표시를 그린다</summary>
        static Sprite Pick(params Sprite[] candidates)
        {
            foreach (var sprite in candidates)
            {
                if (sprite != null) return sprite;
            }
            return null;
        }

        /// <summary>조합 시너지 그림 Art/Synergy/{시너지 ID}.png (SynergyNavigator와 같은 자리, 없으면 null)</summary>
        static Sprite SynergyArt(string synergyId) => ArtLib.Load($"Synergy/{synergyId}.png");

        /// <summary>계보 기본형 그림 (짝짝이 → jjak_base). 계보를 모르면 null</summary>
        static Sprite LineageArt(GameContent content, string lineageId)
        {
            var lineage = content?.Characters != null ? content.Characters.GetLineage(lineageId) : null;
            return lineage != null && !string.IsNullOrEmpty(lineage.baseFormId) ? ArtLib.Character(lineage.baseFormId) : null;
        }
    }
}
