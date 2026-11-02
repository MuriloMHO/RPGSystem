using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players;

namespace RPGSystem.Interfaces
{
    internal interface IItemDamageAble
    {
        public int UseDamageItem(Player player, Enemy enemy);
    }
}
