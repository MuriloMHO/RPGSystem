using RPGSystem.Interfaces;

namespace RPGSystem.Items.ItemTypes
{
    public class GreaterDamageScroll : Item, IItemDamageAble
    {
        public GreaterDamageScroll()
        {
            Name = "Greater Damage Scroll";
            Value = 50;
            Cost = 45;
        }
    }
}