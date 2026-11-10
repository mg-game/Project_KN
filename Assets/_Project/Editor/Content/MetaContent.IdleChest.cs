using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// IDLE-01 수선 바구니 · IDLE-02 빠른 수선 · IDLE-03 복귀 화면 두 배 받기 설정.
    ///   · 줄(1스테이지 시간당, 최고 스테이지 1단계마다 ×1.0025 복리 — 150스테이지 ×1.45): 별조각 3 / 리본 조각 2 / 수선 천 1.5 / 레이스 조각 1 (s≥3) /
    ///     촛농 방울 1 (s≥5) / 기억의 실 0.15 (s≥10) / 우산살 0.5 (s≥11) / 오르골 태엽 0.05 (s≥25) / 별빛 사탕 2 (이벤트 중).
    ///     단추 줄은 앱을 닫아 둔 동안만 초당 단추 × 0.5 (켜 둔 동안은 이미 초당 단추를 받는다).
    ///     스테이지 배율은 CR-07 재조정 ×1.02 → ×1.0025 (×1.02면 150스테이지 ×19라 별조각 수입이 주당 1.3만으로 부풀었다 — PROGRESSION-SIM §6.2).
    ///   · 별조각 줄 천장 (SPEC4 §13.4 C-01): 스테이지 배율을 ×1.5에서 멈춘다 (maxStageMultiplier) → 시간당 최대 4.5, 하루 108 + 빠른 수선.
    ///     재료 · 단추 줄은 계속 자란다. 빠른 수선 · 복귀 두 배 · 다시 만난 날 24시간도 같은 시간당 양을 읽어 천장을 따른다.
    ///   · 상한 8시간 → 2-1 클리어 12시간 → 수선점 Lv.200 16시간 (IDLE-01 P2, SPEC4 §13.4: 0이 아닌 조건이 모두 맞는 단계 중 가장 긴 상한).
    ///     16시간 단계에도 2-1 클리어를 함께 둔다 — 수선점 Lv.200은 2-1보다 한참 뒤라 뜻은 같고, 수선점 레벨 조건을 아직 모르는
    ///     옛 규칙(가장 높은 클리어 단계 하나)에서도 12시간 단계가 먼저 잡혀 16시간이 일찍 열리지 않는다 (P4-2와 착지 순서가 상관없게).
    ///   · 경제: 하루 두 번 꽉 채워 받으면 별조각 약 50~105 (1~150스테이지, 스테이지·상한에 따라 — 주당 약 500~730, Q-02 경제 점검),
    ///     재료는 원정을 보조할 만큼. 별조각 줄 예산: 어느 스테이지에서든 하루 140 이하 (SPEC4 §11.2, ContentMetaTests).
    /// 기본 상한·단추 비율·최소 시간은 GameConfig(offlineMaxHours / offlineRewardRate / offlineMinSeconds)와 맞춘다 —
    /// 콘텐츠가 있으면 그 값을 그대로 읽고, 없으면 리서치 값(8시간 / 0.5 / 60초)을 쓴다.
    /// 담당: M2.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillIdleChest(MetaDatabase db, GameContent content)
        {
            var config = content?.Config;
            db.idleChest = new IdleChestConfig
            {
                baseCapHours = config != null && config.offlineMaxHours > 0f ? config.offlineMaxHours : 8f,
                capSteps = new List<IdleCapStep>
                {
                    // 순서를 지킨다: 12시간 단계가 먼저 (옛 규칙은 같은 스테이지 단계 중 앞의 것을 쓴다)
                    new() { stageCleared = MetaCondition.StageIndex(2, 1), capHours = 12f },
                    new() { stageCleared = MetaCondition.StageIndex(2, 1), shopLevel = CapShopLevel, capHours = 16f },
                },
                lines = new List<IdleChestLine>
                {
                    Line(Gem(3), maxStageMultiplier: GemLineStageCap),
                    Line(Mat(MaterialIds.Ribbon, 2)),
                    Line(Mat(MaterialIds.Cloth, 1.5)),
                    Line(Mat(MaterialIds.Lace, 1), minStage: 3),
                    Line(Mat(MaterialIds.Wax, 1), minStage: 5),
                    Line(Mat(MaterialIds.Thread, 0.15), minStage: 10),
                    Line(Mat(MaterialIds.Rib, 0.5), minStage: 11),
                    Line(Mat(MaterialIds.Spring, 0.05), minStage: 25),
                    Line(MetaReward.Currency(CurrencyType.EventToken, 2), eventOnly: true),
                },
                stageGrowth = 1.0025,
                offlineGoldRate = config != null ? config.offlineRewardRate : 0.5f,
                minClaimSeconds = config != null && config.offlineMinSeconds > 0 ? config.offlineMinSeconds : 60,

                artStageThresholds = new[] { 0.25f, 0.6f, 1f },
                redDotHours = 1f,

                quickIdleHours = 2f,
                quickIdleGemCosts = new[] { 0, 50, 100 },
                maxQuickIdleTickets = 20,

                returnPopupMinSeconds = 600,
                freeDoublePerDay = 1,
                doubleExcludesGem = true,
            };
        }

        /// <summary>16시간 상한이 열리는 수선점 레벨 (SPEC4 §13.4, 부록 C.6 '수선점 Lv.200이 되면 바구니가 16시간까지 늘어나요')</summary>
        internal const int CapShopLevel = 200;

        /// <summary>별조각 줄 스테이지 배율 천장 (C-01). 경제가 넘치면 1.25 → 1.0으로 낮춘다 (델타 30 CR4-02)</summary>
        internal const double GemLineStageCap = 1.5;

        static IdleChestLine Line(MetaReward reward, int minStage = 1, bool eventOnly = false, double maxStageMultiplier = 0) => new()
        {
            reward = reward,
            minStage = minStage,
            eventOnly = eventOnly,
            maxStageMultiplier = maxStageMultiplier,
        };
    }
}
