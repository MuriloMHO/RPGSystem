namespace RPGSystem.Characters.Enemies.EnemyTypes
{
    public class Necromancer : Enemy
    {
        public Necromancer()
        {
            Name = "Necromancer";
            MaxHealth = 185;
            Health = MaxHealth;
            Attack = 12;
            Armour = 14;
            ExperienceReward = 175;
            GoldReward = 80;
        }
    }
}