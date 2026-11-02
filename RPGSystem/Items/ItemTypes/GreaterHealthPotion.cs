using RPGSystem.Interfaces;

namespace RPGSystem.Items.ItemTypes
{
    public class GreaterHealthPotion : Item, IItemHealAble
    {
        public GreaterHealthPotion()
        {
            Name = "Greater Health Potion";
            Value = 50;
            Cost = 35;
        }
    }
}