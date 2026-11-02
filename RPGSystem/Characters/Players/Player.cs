using RPGSystem.Items;
using RPGSystem.Items.ItemTypes;
using System.Diagnostics.CodeAnalysis;

namespace RPGSystem.Characters.Players
{
    public abstract class Player : Character
    {
        public int Level { get; private set; } = 1;
        public int LevelIncrease { get; private set; } = 100;
        public int Experience { get; private set; }
        private int _gold;
        public int Gold
        {
            get => _gold;
            private set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                _gold = value;
            }
        }
        private List<Item> _inventory = [
            new HealthPotion(),
            new HealthPotion(),
            new DamageScroll()
            ];
        public IReadOnlyList<Item> Inventory { get => _inventory.AsReadOnly(); }

        // Player Inventory Methods
        internal void AddToInventory(Item item) => _inventory.Add(item);
        internal bool RemoveFromInventory(Item item) => _inventory.Remove(item);
        public bool IsValidItem(Item item) => _inventory.Contains(item);
        public bool TryGetItem(Item itemToCheck, [NotNullWhen(true)] out Item? item)
        {
            if (IsValidItem(itemToCheck))
            {
                item = itemToCheck;

                return true;
            }
            else
            {
                item = null;

                return false;
            }
        }

        // Player Progression
        public void GainGold(int gold)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(gold);

            Gold += gold;
        }
        private void DecreaseGold(int gold) => Gold -= gold;
        public bool TrySpendGold(int amount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(amount);

            if(Gold < amount)
            {
                return false;
            }

            DecreaseGold(amount);

            return true;
        }
        public void GainExperience(int experience)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(experience);

            Experience += experience;
        }
        private void IncreaseStats()
        {
            Experience -= LevelIncrease;
            LevelIncrease += LevelIncrease / 2;
            MaxHealth += 10;
            Health = MaxHealth;
            Attack += 1;
            Level += 1;
            Skill += 1;
    }
        public void LevelUp()
        {
            if(Experience >= LevelIncrease && Level <= 5 && Health > 0)
            {
                IncreaseStats();
                LevelUp();
            }
        }
    }
}