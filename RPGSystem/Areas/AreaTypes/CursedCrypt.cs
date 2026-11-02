using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;

namespace RPGSystem.Areas.AreaTypes
{
    public class CursedCrypt : Area
    {
        public CursedCrypt()
        {
            Name = "Cursed Crypt";
            RequiredLevel = 4;
        }

        public override Enemy SpawnEnemy()
        {
            RollEnemy();

            if (Roll <= 25)
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
