using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;

namespace RPGSystem.Areas.AreaTypes
{
    public class DarkCitadel : Area
    {
        public DarkCitadel()
        {
            Name = "Dark Citadel";
            RequiredLevel = 5;
        }

        public override Enemy SpawnEnemy()
        {
            RollEnemy();

            if (Roll <= 40)
            {
                return new Necromancer();
            }
            else
            {
                return new Boss();
            }
        }
    }
}
