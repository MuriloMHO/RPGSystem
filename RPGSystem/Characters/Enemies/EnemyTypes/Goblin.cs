namespace RPGSystem.Characters.Enemies.EnemyTypes
{
    // Enemy Types
    public class Goblin : Enemy
    {
        public Goblin()
        {
            Name = "Goblin";
            MaxHealth = 60;
            Health = MaxHealth;
            Attack = 5;
            Armour = 11;
            ExperienceReward = 35;
            GoldReward = 25;
        }
    }
}