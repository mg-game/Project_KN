namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-02 챕터 보스 힌트 (SPEC4 §13.1 · 부록 C.1): 보스를 처음 만날 때와 진 뒤 다시 도전할 때 '보스 등장!' 1초 뒤 한 줄
    /// (BattleNoticeView, 플래그 hint.boss.{보스 ID} — MetaFlags.BossHintPrefix). 기믹을 말로 알려 준다.
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillBossHints(MetaDatabase db, GameContent content)
        {
            var list = db.bossHints;
            // 망각의 큰그림자 (분노 + 불빛 빼앗기)
            list.Add(new BossHintDef
            {
                enemyId = "boss_shadow",
                hint = "화가 나면 빨라지고 불빛을 빼앗아요. 불 밝히기는 분노하기 전에 써 봐요",
            });
            // 좀나방 여왕 (분노 + 천 갉아 먹기 + 부르기)
            list.Add(new BossHintDef
            {
                enemyId = "boss_mothqueen",
                hint = "좀나방을 불러내요. 범위 공격 친구와 방어 지킴이가 막아 줘요",
            });
        }
    }
}
