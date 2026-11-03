namespace RPGSystem.Characters.Players.PlayerClasses
{
    public class Archer : Player
    {
        public Archer(string? name)
        {
            Name = name;
            MaxHealth = 90;
            Health = MaxHealth;
            Attack = 8;
            Armour = 10;
            Skill = 8;
        }

        // Archer Passive
        public override void TakeDamage(int damage)
        {
            if (characterRNG.Next(1, 6) == 1)
            {
                damage = 0;
            }

            base.TakeDamage(damage);
        }

        // Archer Skill Method
        public override int SkillAttack(Character defender, int additional = 0)
        {
            return DoubleDamage(base.SkillAttack(defender, additional));
        }
        public static int DoubleDamage(int damage) => damage * 2;
    }
}