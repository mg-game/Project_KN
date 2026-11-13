namespace IdleGame.UI
{
    /// <summary>
    /// 챕터 보스 안내 줄 (PRG-02, SPEC4 §13.1 — 순수 규칙): 메인 스테이지 보스가 나오면 그 보스의 안내(BossHintDef)를 보스 등장 배너 1초 뒤 한 줄로.
    /// 보여 주는 때: 처음 만남(PlayerData.flags에 'hint.boss.{id}'가 없음 — 보여 줄 때 적는다) 또는 다시 도전(이 스테이지 연속 실패 ≥ 1).
    /// 튜토리얼 단계가 전투를 가리키는 동안에는 보이지 않는다
    /// </summary>
    public static class BossHintRule
    {
        /// <summary>보스 등장 배너 뒤 안내 줄까지 (초)</summary>
        public const float Delay = 1f;
        /// <summary>안내 줄이 머무는 시간 (초)</summary>
        public const float Duration = 2.4f;

        /// <summary>처음 만남 표시 키 (MetaFlags.BossHintPrefix + 적 ID)</summary>
        public static string FlagKey(string enemyId) => string.IsNullOrEmpty(enemyId) ? null : MetaFlags.BossHintPrefix + enemyId;

        /// <summary>안내 줄을 보여 줄지</summary>
        /// <param name="hasHint">그 보스에 안내 문구가 있다</param>
        /// <param name="seenBefore">처음 만남 표시가 이미 있다</param>
        /// <param name="consecutiveFails">이 스테이지 연속 실패 수</param>
        /// <param name="ftueBattleStep">튜토리얼 단계가 전투를 가리키는 중</param>
        public static bool ShouldShow(bool hasHint, bool seenBefore, int consecutiveFails, bool ftueBattleStep)
        {
            if (!hasHint || ftueBattleStep) return false;
            return !seenBefore || consecutiveFails >= 1;
        }
    }
}
