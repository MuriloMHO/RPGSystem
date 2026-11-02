using RPGSystem.Interfaces;

namespace RPGSystem.Items.ItemTypes
{
    // Item Types
    public class HealthPotion : Item, IItemHealAble
    {
        public HealthPotion()
        {
            Name = "Health Potion";
            Value = 25;
            Cost = 20;
        }
    }
}