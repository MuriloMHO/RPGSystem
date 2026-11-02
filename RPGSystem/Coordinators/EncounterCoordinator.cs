using RPGSystem.Characters.Players;
using RPGSystem.Characters.Enemies;
using RPGSystem.Areas;
using RPGSystem.Items;
using RPGSystem.Interfaces;

namespace RPGSystem.Coordinators
{
    public class EncounterCoordinator(Player player)
    {
        public Player Player { get; } = player;
        public Area? Area { get; private set; }
        public Enemy? Enemy { get; private set; }
        public int TurnCount { get; private set; } = 0;
        public EncounterStatus Status { get; private set; } = EncounterStatus.NotStarted;

        public bool Start(Area area)
        {
            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return false;
            }

            if (Status != EncounterStatus.NotStarted)
            {
                return false;
            }

            if (!area.PlayerCanEnter(Player))
            {
                return false;
            }

            Area = area;

            Enemy = area.SpawnEnemy();

            Status = EncounterStatus.Active;

            return true;
        }

        public bool Attack()
        {
            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return false;
            }

            if (Status != EncounterStatus.Active)
            {
                return false;
            }

            if (Enemy == null)
            {
                return false;
            }

            TurnCount++;
            Player.ReduceCooldown();

            Enemy.TakeDamage(Player.NormalAttack(Enemy));

            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return true;
            }

            if (Enemy.Health == 0)
            {
                CompleteVictory();

                return true;
            }

            Player.TakeDamage(Enemy.NormalAttack(Player));

            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return true;
            }

            if (Enemy.Health == 0)
            {
                CompleteVictory();

                return true;
            }

            return true;
        }
        private void CompleteVictory()
        {
            Player.GainExperience(Enemy!.ExperienceReward);
            Player.GainGold(Enemy!.GoldReward);
            Player.LevelUp();

            Status = EncounterStatus.Victory;
        }

        public bool Skill()
        {
            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return false;
            }

            if (Status != EncounterStatus.Active)
            {
                return false;
            }

            if (Enemy == null)
            {
                return false;
            }

            if (Player.SkillCooldown > 0)
            {
                return false;
            }

            TurnCount++;

            Enemy.TakeDamage(Player.SkillAttack(Enemy));

            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return true;
            }

            if (Enemy.Health == 0)
            {
                CompleteVictory();

                return true;
            }

            Player.TakeDamage(Enemy.NormalAttack(Player));

            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return true;
            }

            if (Enemy.Health == 0)
            {
                CompleteVictory();

                return true;
            }

            return true;
        }

        public bool UseItem(Item item)
        {
            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return false;
            }

            if (Status != EncounterStatus.Active)
            {
                return false;
            }

            if (Enemy == null)
            {
                return false;
            }

            if (!Player.TryGetItem(item, out Item? itemToUse))
            {
                return false;
            }

            if (itemToUse == null)
            {
                return false;
            }

            if (itemToUse is not IItemHealAble && item is not IItemDamageAble)
            {
                return false;
            }

            TurnCount++;
            Player.ReduceCooldown();

            if (itemToUse is IItemDamageAble)
            {
                Enemy.TakeDamage(itemToUse.UseDamageItem(Player, Enemy));

                Player.RemoveFromInventory(itemToUse);

                if (Player.Health == 0)
                {
                    Status = EncounterStatus.Defeat;

                    return true;
                }

                if (Enemy.Health == 0)
                {
                    CompleteVictory();

                    return true;
                }
            }

            if (itemToUse is IItemHealAble)
            {
                itemToUse.UseHealItem(Player);

                Player.RemoveFromInventory(itemToUse);
            }

            Player.TakeDamage(Enemy.NormalAttack(Player));

            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return true;
            }

            if (Enemy.Health == 0)
            {
                CompleteVictory();

                return true;
            }

            return true;
        }
        public bool Flee()
        {
            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return false;
            }

            if (Status != EncounterStatus.Active)
            {
                return false;
            }

            if (Enemy == null)
            {
                return false;
            }

            TurnCount++;
            Player.ReduceCooldown();

            Player.TakeDamage(Enemy.NormalAttack(Player));

            if (Player.Health == 0)
            {
                Status = EncounterStatus.Defeat;

                return true;
            }

            Status = EncounterStatus.Escaped;

            return true;
        }
    }
}