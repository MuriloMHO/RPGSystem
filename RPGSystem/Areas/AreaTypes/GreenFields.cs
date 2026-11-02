using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;

namespace RPGSystem.Areas.AreaTypes
{
    public class GreenFields : Area
    {
        public GreenFields()
        {
            Name = "Green Fields";
            RequiredLevel = 1;
        }

        public override Enemy SpawnEnemy()
        {
            RollEnemy();

            if (Roll <= 70)
            {
                return new Goblin();
            }
            else if (Roll <= 95)
            {
                return new Wolf();
            }
            else
            {
                return new Orc();
            }
        }
    }
}
