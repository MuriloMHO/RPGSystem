using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;

namespace RPGSystem.Areas.AreaTypes
{
    public class DarkForest : Area
    {
        public DarkForest()
        {
            Name = "Dark Forest";
            RequiredLevel = 1;
        }

        public override Enemy SpawnEnemy()
        {
            RollEnemy();

            if (Roll <= 20)
            {
                return new Wolf();
            }
            else if (Roll <= 70)
            {
                return new Goblin();
            }
            else
            {
                return new Orc();

            }
        }
    }
}
