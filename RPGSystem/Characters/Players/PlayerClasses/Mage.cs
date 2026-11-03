namespace RPGSystem.Characters.Players.PlayerClasses
{
    public class Mage : Player
    {
        public Mage(string? name)
        {
            Name = name;
            ImagePath = "Assets/Characters/Players/Mage.png";
            MaxHealth = 80;
            Health = MaxHealth;
            Attack = 10;
            Armour = 12;
            Skill = 18;
        }

        // Mage Skill Method
        public override int SkillAttack(Character defender, int additional = 0)
        {
            additional = ApplyFlames();

            return base.SkillAttack(defender, additional);
        }
        public static int ApplyFlames()
        {
            if (characterRNG.Next(1, 3) == 1)
            {
                return 7;
            }
            else
            {
                return 0;
            }
        }
    }
}