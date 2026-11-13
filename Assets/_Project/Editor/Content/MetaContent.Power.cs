namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-01 전투력 가중치와 권장 전투력.
    ///   · 팀 전투력 = 공격 + min(수선점 최대 내구도 × 0.3, 공격 × 수선점 몫 상한 1), 공격 = Σ지킴이 × 1.0 + Σ덱 잔향이 × 0.6 (PowerSystem, SPEC5 §4.3)
    ///   · 권장 rec(s): 걸음 s→s+1의 증가율이 s &lt; 30이면 g, s &lt; 151이면 gL, 그 뒤 gL2 (적 성장 두 번 완만해짐과 같은 자리 — 이어진 곡선).
    ///     비율 1.0 이상 초록 "여유" / 0.8~1.0 노랑 "도전" / 0.8 미만 빨강 "어려움".
    ///   · 던전 층 권장 = dBase × dGrowth^(층−1), 잔향이 덱 전투력과 비교 (0 = 줄 없음).
    /// 권장값은 "IdleGame/전투력 보정" 메뉴(PowerCalibration)가 헤드리스 전투로 맞춰 아래 Calibrated* 상수를 고쳐 쓴다 (코드가 원본).
    /// 그 최소제곱 선은 실제 필요의 "가운데"를 지나 절반쯤은 필요보다 낮으므로, 여기서 위쪽 감싸기 배율을 곱해
    /// 초록 "여유"가 정말 안전한 싸움이 되게 한다 (RecommendedEnvelopeScale).
    /// 보정 전(0)이면 SPEC3 초기값 300 / 1.12를 쓴다. 권장값은 MetaDatabase.power에 둔다.
    /// 담당: M2 · P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        // ↓ PowerCalibration이 고쳐 쓰는 줄 (형식을 바꾸지 말 것). 0 = 아직 보정 전
        //   2026-11-05 헤드리스 보정 (RM-2 S-4, 세 구간 · 수선점 몫 상한, 표본 115스테이지 1-1~40-1 + 던전 1~30층, 5시드 중 4승):
        //   시작 편성 전투력 1,017, 1-1 필요 223 / 1-10 728 / 3-10 4,308 / 6-10 21,832 / 9-10 11만 / 16-1 320만 / 36-1 2.2억,
        //   1-10부터 이어진 세 구간 최소제곱 → 실제 필요 / 맞춤 = 2-1~5-10 0.79~1.23 (중앙값 1.02). 던전 1~30층 덱 필요 63~8,231.
        //   전투 수치가 바뀌면 메뉴를 다시 돌린다 (표: Logs/PowerCalibration.csv, Logs/PowerCalibrationDungeon.csv)
        internal const double CalibratedRecommendedBase = 244;
        internal const double CalibratedRecommendedGrowth = 1.1043;
        //   4차 구간 (SPEC4 §7.1 · SPEC5 §4.3): 30스테이지 뒤 · 151스테이지 뒤 증가율과 던전 권장 (던전 base에는 필요 / 맞춤의 90번째 백분위수 감싸기가 들어 있다).
        //   0 = 보정 전 → 늦은 구간도 위 증가율 하나를 쓴다
        internal const double CalibratedRecommendedGrowthLate = 1.0553;
        internal const double CalibratedRecommendedGrowthLate2 = 1.0228;
        internal const double CalibratedDungeonRecommendedBase = 83.7;
        internal const double CalibratedDungeonRecommendedGrowth = 1.177;
        // ↑

        /// <summary>
        /// 최소제곱 선을 위쪽 감싸기로 올리는 배율. 권장 base = 244 × 1.136 = 277 (증가율은 그대로).
        /// 기준 (다시 잼, 2026-11-05 세 구간 맞춤): 2-1~5-10 (40스테이지)의 필요 / 맞춤 비율 중 90번째 백분위수 = 1.136 →
        /// 36곳(90%)에서 권장 ≥ 실제 필요(5시드 중 4번 이기는 최소 전투력).
        ///   · 예전 값 1.264는 한 지수 맞춤(421 × 1.0741^(s−1))의 백분위수였다. 세 구간 맞춤은 2~5챕터를 더 가깝게 따라가므로
        ///     그 값을 그대로 쓰면 2~5챕터 필요 / 권장이 0.72~0.89로 낮아져 이길 팀이 노랑 · 빨강으로 보였다.
        ///   · 1챕터 위쪽 한계: 시작 편성 1,017 / 1-10 권장 677 = 1.50 "여유".
        ///   · 받아들임 (SPEC4 §7.1, 6-1~10-10 필요 / 권장 0.80~1.05): 23곳 중 17곳 안 (범위 0.64~1.07). 벗어난 곳은 스테이지 하나씩 튀는
        ///     기믹 · 시드 흔들림이다 (예전 한 지수: 0.52~0.83). 셋째 구간(151~400)은 표본이 10스테이지마다라 흔들림이 크다 (중앙값 0.96).
        /// PowerCalibration을 다시 돌리면 위 Calibrated* 줄만 바뀌고 이 배율은 남는다. 표가 크게 바뀌면 배율도 다시 잴 것
        /// (2-1~5-10의 필요 / 맞춤 비율 중 90번째 백분위수). 그 메뉴는 에셋 power에도 이 배율을 곱해 바로 쓴다.
        /// </summary>
        internal const double RecommendedEnvelopeScale = 1.136;

        /// <summary>보정 때 잰 시작 편성 전투력 (레벨 1·강화 없음). 검사: 1-10 권장 × 여유 비율이 이 값 이하여야 1챕터 내내 "여유"</summary>
        internal const double CalibrationStarterPower = 1017;

        /// <summary>시작 편성이 "여유"여야 하는 마지막 스테이지 (1-10 = 첫 챕터 보스)</summary>
        internal const int StarterComfortableThroughStage = 10;

        /// <summary>보정 전 초기값 (SPEC3 §4.7)</summary>
        const double DefaultRecommendedBase = 300;
        const double DefaultRecommendedGrowth = 1.12;

        /// <summary>PowerCalibration 결과가 들어 있는지 (0이면 보정 전)</summary>
        internal static bool IsPowerCalibrated => CalibratedRecommendedBase > 0 && CalibratedRecommendedGrowth >= 1.0;

        /// <summary>권장 전투력 둘째 구간 시작 (= BattleConfig.stageGrowthTaperStage, 적 체력 1.11 → 1.07)</summary>
        internal const int RecommendedTaperStage = 30;

        /// <summary>권장 전투력 셋째 구간 시작 (= BattleConfig.stageGrowthTaperStage2, 적 두 번째 완만해짐 — SPEC5 §4.3 · 델타 2)</summary>
        internal const int RecommendedTaperStage2 = 151;

        /// <summary>수선점 몫 상한 (전투력 = 공격 + min(수선점 × 0.3, 공격 × 이 값), SPEC5 §4.3 — PowerConfig 기본값과 같다)</summary>
        internal const float ShopShareCap = 1f;

        static partial void FillPower(MetaDatabase db, GameContent content)
        {
            bool calibrated = IsPowerCalibrated;
            double growth = calibrated ? CalibratedRecommendedGrowth : DefaultRecommendedGrowth;
            // 늦은 구간 증가율이 아직 없으면 앞 구간 것을 그대로 — 곡선이 이어지고 지금 계산과 같다 (P4-1의 구간 공식이 들어와도 그대로)
            double late = CalibratedRecommendedGrowthLate >= 1.0 ? CalibratedRecommendedGrowthLate : growth;
            double late2 = CalibratedRecommendedGrowthLate2 >= 1.0 ? CalibratedRecommendedGrowthLate2 : late;
            db.power = new PowerConfig
            {
                guardianWeight = 1f,
                echoWeight = 0.6f,
                shopHpWeight = 0.3f,
                recommendedBase = calibrated ? CalibratedRecommendedBase * RecommendedEnvelopeScale : DefaultRecommendedBase,
                recommendedGrowth = growth,
                comfortableRatio = 1f,
                challengeRatio = 0.8f,

                recommendedTaperStage = RecommendedTaperStage,
                recommendedGrowthLate = late,
                recommendedTaperStage2 = RecommendedTaperStage2,
                recommendedGrowthLate2 = late2,
                shopShareCap = ShopShareCap,
                // 0 = 던전 권장 줄 없음 (던전 보정 전)
                dungeonRecommendedBase = CalibratedDungeonRecommendedBase > 0 ? CalibratedDungeonRecommendedBase : 0,
                dungeonRecommendedGrowth = CalibratedDungeonRecommendedGrowth >= 1.0 ? CalibratedDungeonRecommendedGrowth : 1.1,
            };
        }
    }
}
