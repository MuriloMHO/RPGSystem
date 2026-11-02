using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players;

namespace RPGSystem.Items
{
    public abstract class Item
    {
        public string? Name { get; protected set; }
        public int Value { get; protected set; }
        private int _cost;
        public int Cost { get => _cost;
            protected set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                
                _cost = value;
            }
        }

        public virtual void UseHealItem(Player player) => player.Heal(Value);
        public virtual int UseDamageItem(Player player, Enemy enemy) => player.DamageToTarget(player.HitTarget(enemy), Value);
    }
}
