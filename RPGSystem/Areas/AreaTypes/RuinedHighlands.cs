using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;

namespace RPGSystem.Areas.AreaTypes
{
    public class RuinedHighlands : Area
    {
        public RuinedHighlands()
        {
            Name = "Ruined Highlands";
            RequiredLevel = 3;
        }

        public override Enemy SpawnEnemy()
        {
            RollEnemy();

            if (Roll <= 20)
            {
                return new Orc();
            }
            else if (Roll <= 75)
            {
                return new Troll();
            }
            else
            {
                return new Necromancer();

            }
        }
    }
}
