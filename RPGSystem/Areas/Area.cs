using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players;

namespace RPGSystem.Areas
{
    public abstract class Area
    {
        protected static readonly Random areaRNG = new();
        public string Name { get; protected set; } = "";
        public int RequiredLevel { get; protected set; }
        public int Roll { get; protected set; }

        // Area Spawn Methods
        public bool PlayerCanEnter(Player player) => player.Level >= RequiredLevel;
        public void RollEnemy() => Roll = areaRNG.Next(1, 101);
        public abstract Enemy SpawnEnemy();
    }

}
