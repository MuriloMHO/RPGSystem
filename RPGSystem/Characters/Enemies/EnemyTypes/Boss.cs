namespace RPGSystem.Characters.Enemies.EnemyTypes
{
    public class Boss : Enemy
    {
        public Boss()
        {
            Name = "Dark Lord";
            MaxHealth = 200;
            Health = MaxHealth;
            Attack = 12;
            Armour = 15;
            ExperienceReward = 300;
            GoldReward = 120;
        }
    }
}