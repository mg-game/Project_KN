using System.Collections.Generic;

namespace IdleGame
{
    /// <summary>Game.Setup에 넘기는 콘텐츠 에셋 묶음</summary>
    public sealed class GameContent
    {
        public GameConfig Config;
        public ItemDatabase Items;
        public CharacterDatabase Characters;
        public IReadOnlyList<GachaTable> GachaTables;
        public BattleConfig Battle;
        public ExpeditionDatabase Expeditions;
        public ShopDatabase Shops;
        public SynergyDatabase Synergies;
        /// <summary>메타 기능 (기능 개방 / 가이드 퀘스트 / 수선 일지 / 출석부 / 수선 바구니 / 튜토리얼 / 전투력). null이면 빈 데이터로 돈다</summary>
        public MetaDatabase Meta;
    }
}
