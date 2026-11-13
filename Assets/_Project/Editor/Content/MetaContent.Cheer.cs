namespace IdleGame.EditorTools
{
    /// <summary>
    /// PRG-03 친구들의 응원 (SPEC4 §13.2 · 부록 C.5): 튜토리얼 뒤 장마다 한 번, 10분(실제 시간) 동안 공격력 +20%.
    /// 전투력(PowerSystem)에는 넣지 않는다. 강해지기 줄 · 응원 버튼 · 칩 문구는 화면(P4-6 · P4-3b)이 쓴다 (부록 C.5).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        static partial void FillCheer(MetaDatabase db, GameContent content)
        {
            db.cheer = new CheerConfig
            {
                attackPercent = 20f,
                durationSeconds = 600,
            };
        }
    }
}
