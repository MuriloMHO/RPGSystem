using RPGSystem.Characters.Players;
using RPGSystem.Interfaces;

namespace RPGSystem.Items.ItemTypes
{
    public class Elixir : Item, IItemHealAble
    {
        public Elixir()
        {
            Name = "Elixir";
            Cost = 100;
        }

        public override void UseHealItem(Player player) => player.Heal(player.MaxHealth);
    }
}