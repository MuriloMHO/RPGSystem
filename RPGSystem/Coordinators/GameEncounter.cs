using RPGSystem.Areas;
using RPGSystem.Characters.Players;
using RPGSystem.Interfaces;
using RPGSystem.Items;
using RPGSystem.Items.ItemTypes;

namespace RPGSystem.Coordinators
{
    public class GameEncounter(Player player)
    {
        public Player Player { get; } = player;
        public EncounterCoordinator? CurrentEncounter { get; private set; }
        public IReadOnlyList<Item> ItemsToSell => [
                new HealthPotion(),
                new GreaterHealthPotion(),
                new DamageScroll(),
                new GreaterDamageScroll(),
                new Elixir()
                ];

        public bool NewEncounter(Area area)
        {
            if (Player.Health == 0)
            {
                return false;
            }
            
            if (CurrentEncounter != null)
            {
                if (CurrentEncounter.Status == EncounterStatus.Active)
                {
                    return false;
                }

                if (CurrentEncounter.Status == EncounterStatus.Defeat)
                {
                    return false;
                }
            }

            EncounterCoordinator encount = new(Player);

            if (!encount.Start(area))
            {
                return false;
            }

            CurrentEncounter = encount;

            return true;
        }

        public bool UseItemOutOfBattle(Item item)
        {
            if (Player.Health == 0)
            {
                return false;
            }

            if (CurrentEncounter != null)
            {
                if (CurrentEncounter.Status == EncounterStatus.Active)
                {
                    return false;
                }
                if (CurrentEncounter.Status == EncounterStatus.Defeat)
                {
                    return false;
                }
            }

            if (!Player.TryGetItem(item, out Item? itemToUse))
            {
                return false;
            }

            if (itemToUse == null)
            {
                return false;
            }

            if (itemToUse is not IItemHealAble)
            {
                return false;
            }

            itemToUse.UseHealItem(Player);

            Player.RemoveFromInventory(itemToUse);

            return true;
        }

        public bool BuyFromShop(Item? item)
        {
            if (Player.Health == 0)
            {
                return false;
            }

            if (item == null)
            {
                return false;
            }

            if (CurrentEncounter != null)
            {
                if (CurrentEncounter.Status == EncounterStatus.Active)
                {
                    return false;
                }
                if (CurrentEncounter.Status == EncounterStatus.Defeat)
                {
                    return false;
                }
            }

            foreach (Item itemFromStock in ItemsToSell)
            {
                if (itemFromStock.GetType() == item.GetType())
                {
                    if (Player.TrySpendGold(itemFromStock.Cost))
                    {
                        Player.AddToInventory(itemFromStock);

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
