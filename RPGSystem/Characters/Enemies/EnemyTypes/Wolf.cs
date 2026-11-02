namespace RPGSystem.Characters.Enemies.EnemyTypes
{
    public class Wolf : Enemy
    {
        public Wolf()
        {
            Name = "Wolf";
            MaxHealth = 80;
            Health = MaxHealth;
            Attack = 7;
            Armour = 11;
            ExperienceReward = 50;
            GoldReward = 30;
        }
    }
}