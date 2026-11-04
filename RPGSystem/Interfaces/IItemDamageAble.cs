using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players;

namespace RPGSystem.Interfaces
{
    public interface IItemDamageAble
    {
        public int UseDamageItem(Player player, Enemy enemy);
    }
}
