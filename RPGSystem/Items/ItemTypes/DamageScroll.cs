using RPGSystem.Characters.Players;
using RPGSystem.Interfaces;

namespace RPGSystem.Items.ItemTypes
{
    public class DamageScroll : Item, IItemDamageAble
    {
        public DamageScroll()
        {
            Name = "Damage Scroll";
            Value = 25;
            Cost = 25;
        }
    }
}