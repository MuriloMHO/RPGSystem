using RPGSystem.Interfaces;

namespace RPGSystem.Characters
{
    public abstract class Character : IDamageAble, IAttackAble, IHealAble
    {
        protected static readonly Random characterRNG = new();
        public string? Name { get; protected set; }
        public int MaxHealth { get; protected set; }
        public int Attack { get; protected set; }
        public int Skill { get; protected set; }
        public int SkillCooldown { get; private set; } = 0;
        private int _health;
        private int _armour;
        public int Health
        {
            get => _health;
            protected set
            {
                if (value < 0)
                {
                    _health = 0;
                }
                else if (value > MaxHealth)
                {
                    _health = MaxHealth;
                }
                else
                {
                    _health = value;
                }
            }
        }
        public int Armour
        {
            get => _armour;
            protected set
            {
                if (value > 25)
                {
                    _armour = 25;
                }
                else if (value < 10)
                {
                    _armour = 10;
                }
                else
                {
                    _armour = value;
                }
            }
        }


        // Character Fight Methods
        public virtual void TakeDamage(int damage)
        {
            if (Health > 0 && damage > 0)
            {
                Health -= damage;
            }
        }
        public int DamageToTarget(int succeded, int damage)
        {
            int damageDealt = characterRNG.Next(damage - 3, damage + 1);

            if (succeded == 3)
            {
                return damageDealt * 2;
            }
            else if (succeded == 2)
            {
                TakeDamage(damageDealt / 2);

                return 0;
            }
            else if (succeded == 1)
            {
                return damageDealt;
            }
            else
            {
                return 0;
            }
        }
        public int HitTarget(Character defender)
        {
            int hit = characterRNG.Next(1, 21);

            if (hit == 20)
            {
                return 3;
            }
            else if (hit == 1)
            {
                return 2;
            }
            else if (hit + (Attack / 2) >= defender.Armour)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
        public int NormalAttack(Character defender) => DamageToTarget(HitTarget(defender), Attack);
        public virtual int SkillAttack(Character defender, int additional = 0)
        {
            SkillCooldown = 3;

            return DamageToTarget(HitTarget(defender), Skill + additional);
        }
        public void ReduceCooldown()
        {
            if (SkillCooldown > 0)
            {
                SkillCooldown--;
            }
        }

        // Character Heal Mathod
        public void Heal(int heal)
        {
            if (Health > 0 && heal > 0)
            {
                Health += heal;
            }
        }
    }
}