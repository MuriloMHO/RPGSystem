using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;

namespace RPGSystem.Areas.AreaTypes
{
    public class OrcCamp : Area
    {
        public OrcCamp()
        {
            Name = "Orc Camp";
            RequiredLevel = 2;
        }

        public override Enemy SpawnEnemy()
        {
            RollEnemy();

            if (Roll <= 15)
            {
                return new Wolf();
            }
            else if (Roll <= 75)
            {
                return new Orc();
            }
            else
            {
                return new Troll();

            }
        }
    }
}