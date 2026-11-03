namespace RPGSystem.Characters.Players.PlayerClasses
{
    public class Knight : Player
    {
        public Knight(string? name)
        {
            Name = name;
            MaxHealth = 130;
            Health = MaxHealth;
            Attack = 5;
            Armour = 18;
            Skill = 15;
        }

        // Knight Passive
        public override void TakeDamage(int damage)
        {
            if (damage > 3)
            {
                damage -= 3;

                base.TakeDamage(damage);
            }

        }
    }
}