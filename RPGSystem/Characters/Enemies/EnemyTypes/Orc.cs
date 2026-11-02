namespace RPGSystem.Characters.Enemies.EnemyTypes
{
    public class Orc : Enemy
    {
        public Orc()
        {
            Name = "Orc";
            MaxHealth = 105;
            Health = MaxHealth;
            Attack = 8;
            Armour = 12;
            ExperienceReward = 75;
            GoldReward = 45;
        }
    }
}