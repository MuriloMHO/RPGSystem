namespace RPGSystem.Characters.Enemies.EnemyTypes
{
    public class Troll : Enemy
    {
        public Troll()
        {
            Name = "Troll";
            MaxHealth = 140;
            Health = MaxHealth;
            Attack = 10;
            Armour = 13;
            ExperienceReward = 115;
            GoldReward = 60;
        }
    }
}