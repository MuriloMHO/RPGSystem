using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using RPGSystem.Areas;
using RPGSystem.Characters;
using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players;
using RPGSystem.Characters.Players.PlayerClasses;
using RPGSystem.Coordinators;
using RPGSystem.Items;
using RPGSystem.Items.ItemTypes;
using Xunit;

namespace RPGSystem.Test;

public class Phase1FinalRegressionTests
{
    private sealed class FixedArea(Enemy enemy) : Area
    {
        public override Enemy SpawnEnemy() => enemy;
    }

    private sealed class UnsupportedItem : Item { }

    // Allows one or more deterministic hit rolls. Other rolls get a valid minimum.
    private sealed class HitSequenceRandom(params int[] hits) : Random
    {
        private readonly Queue<int> _hits = new(hits);
        public int HitRolls { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                if (_hits.Count == 0)
                    throw new InvalidOperationException("Unexpected extra combat roll.");
                HitRolls++;
                int hit = _hits.Dequeue();
                if (hit < minValue || hit >= maxValue)
                    throw new InvalidOperationException("Invalid test hit roll.");
                return hit;
            }
            return minValue;
        }
    }

    private sealed class HighRollRandom : Random
    {
        public override int Next(int minValue, int maxValue) => maxValue - 1;
    }

    private sealed class LowRollRandom : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
    }

    private static void AssertSameInventory(Player player, Item[] before)
    {
        Assert.Equal(before.Length, player.Inventory.Count);
        for (int i = 0; i < before.Length; i++)
            Assert.Same(before[i], player.Inventory[i]);
    }

    private static Player NewPlayer(int characterType) => characterType switch
    {
        0 => TestState.Player(new Knight("Knight")),
        1 => TestState.Player(new Archer("Archer")),
        2 => TestState.Player(new Mage("Mage")),
        _ => throw new ArgumentOutOfRangeException(nameof(characterType))
    };

    private static Item OfferedItem(int type) => type switch
    {
        0 => new HealthPotion(),
        1 => new GreaterHealthPotion(),
        2 => new DamageScroll(),
        3 => new GreaterDamageScroll(),
        4 => new Elixir(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    [Fact]
    public void PermanentDefeat_DeadPlayerCannotStartShopOrUseOwnedItemOutsideCombat()
    {
        var knight = TestState.Player(new Knight("Dead Hero"));
        var game = new GameEncounter(knight);
        knight.GainGold(150);
        knight.TakeDamage(1000);
        Item owned = knight.Inventory[0];
        Item[] before = knight.Inventory.ToArray();
        var area = new FixedArea(TestState.Enemy());

        Assert.Equal(0, knight.Health);
        Assert.False(game.NewEncounter(area));
        Assert.False(game.BuyFromShop(new HealthPotion()));
        Assert.False(game.UseItemOutOfBattle(owned));
        Assert.Equal(150, knight.Gold);
        Assert.Equal(0, knight.Health);
        Assert.Null(game.CurrentEncount);
        AssertSameInventory(knight, before);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PermanentDefeat_NeitherHealNorElixirRevivesDeadCharacter(int characterType)
    {
        TestState.WithRandom(new HighRollRandom(), () =>
        {
            Player player = NewPlayer(characterType);
            player.TakeDamage(player.MaxHealth + 100);
            Assert.Equal(0, player.Health);

            player.Heal(1000);
            new Elixir().UseHealItem(player);
            Assert.Equal(0, player.Health);
        });
    }

    [Fact]
    public void EncounterCoordinator_CannotStartWithDeadPlayer()
    {
        var knight = TestState.Player(new Knight("Dead Hero"));
        knight.TakeDamage(1000);
        var encounter = new EncounterCoordinator(knight);

        Assert.False(encounter.Start(new FixedArea(TestState.Enemy())));
        Assert.Equal(EncounterStatus.NotStarted, encounter.Status);
        Assert.Null(encounter.Enemy);
    }

    [Fact]
    public void DeadPlayer_DoesNotConsumeCombatItemWhenAlreadyAtZeroHealth()
    {
        var knight = TestState.Player(new Knight("No Revival"));
        var encounter = new EncounterCoordinator(knight);
        var enemy = TestState.Enemy();
        Assert.True(encounter.Start(new FixedArea(enemy)));
        Item potion = knight.Inventory[0];
        Item[] before = knight.Inventory.ToArray();
        knight.TakeDamage(1000); // The player dies while encounter state is still Active.

        Assert.False(encounter.UseItem(potion));
        Assert.Equal(0, encounter.TurnCount);
        Assert.Equal(0, knight.Health);
        AssertSameInventory(knight, before);
        Assert.Equal(100, enemy.Health);
    }

    [Theory]
    [InlineData("Health")]
    [InlineData("MaxHealth")]
    [InlineData("Attack")]
    [InlineData("Armour")]
    [InlineData("Skill")]
    [InlineData("SkillCooldown")]
    [InlineData("Name")]
    public void CharacterCoreProperties_DoNotExposePublicSetters(string propertyName)
    {
        PropertyInfo property = typeof(Character).GetProperty(propertyName)!;
        Assert.NotNull(property);
        MethodInfo? setter = property.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        Assert.False(setter.IsPublic);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Value")]
    [InlineData("Cost")]
    public void ItemProperties_DoNotExposePublicSetters(string propertyName)
    {
        PropertyInfo property = typeof(Item).GetProperty(propertyName)!;
        Assert.NotNull(property);
        Assert.False(property.GetSetMethod(nonPublic: true)!.IsPublic);
    }

    [Fact]
    public void PlayerInventory_ReturnsReadOnlyWrapperThatCannotBeMutated()
    {
        var player = TestState.Player(new Knight("ReadOnly Bag"));
        Item[] before = player.Inventory.ToArray();
        IReadOnlyList<Item> view = player.Inventory;
        Assert.IsType<ReadOnlyCollection<Item>>(view);

        var attemptedMutation = Assert.IsAssignableFrom<IList<Item>>(view);
        Assert.Throws<NotSupportedException>(() => attemptedMutation.Add(new HealthPotion()));
        Assert.Throws<NotSupportedException>(() => attemptedMutation.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => attemptedMutation[0] = new Elixir());
        AssertSameInventory(player, before);
    }

    [Theory]
    [InlineData("AddToInventory")]
    [InlineData("RemoveFromInventory")]
    public void PlayerInventoryMutationMethods_AreInternalNotPublic(string methodName)
    {
        MethodInfo method = typeof(Player).GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.NotNull(method);
        Assert.True(method.IsAssembly);
        Assert.False(method.IsPublic);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NegativeTakeDamage_CannotHealAnInjuredCharacter(int characterType)
    {
        TestState.WithRandom(new HighRollRandom(), () =>
        {
            Player player = NewPlayer(characterType);
            TestState.Set(player, nameof(Character.Health), 50);
            player.TakeDamage(-10);
            Assert.Equal(50, player.Health);
        });
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    public void Heal_WithNonpositiveAmount_DoesNotChangeHealth(int amount)
    {
        var knight = TestState.Player(new Knight("Heal Guard"), health: 50);
        knight.Heal(amount);
        Assert.Equal(50, knight.Health);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GainGold_NegativeValuesThrowWithoutChangingGold(int amount)
    {
        var knight = TestState.Player(new Knight("Gold Guard"));
        knight.GainGold(20);
        Assert.Throws<ArgumentOutOfRangeException>(() => knight.GainGold(amount));
        Assert.Equal(20, knight.Gold);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GainExperience_NegativeValuesThrowWithoutChangingExperience(int amount)
    {
        var knight = TestState.Player(new Knight("XP Guard"));
        knight.GainExperience(20);
        Assert.Throws<ArgumentOutOfRangeException>(() => knight.GainExperience(amount));
        Assert.Equal(20, knight.Experience);
    }

    [Fact]
    public void NegativeDamageCalculation_CannotIncreaseHealthOnCriticalFailure()
    {
        // Direct public combat method: negative input must not turn self-damage into healing.
        TestState.WithRandom(new HighRollRandom(), () =>
        {
            var knight = TestState.Player(new Knight("Damage Guard"), health: 50);
            Assert.Throws<ArgumentOutOfRangeException>(() => knight.DamageToTarget(2, -20));
            Assert.Equal(50, knight.Health);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SmallNonnegativeDamage_CriticalFailureNeverHealsPlayer(int damage)
    {
        TestState.WithRandom(new LowRollRandom(), () =>
        {
            var mage = TestState.Player(new Mage("Small Damage"), health: 50);
            int damageToEnemy = mage.DamageToTarget(2, damage);
            Assert.Equal(0, damageToEnemy);
            Assert.True(mage.Health <= 50);
        });
    }

    [Fact]
    public void AfterVictory_DeadPlayerCannotBypassPermanentDefeatRestrictions()
    {
        TestState.WithRandom(new HitSequenceRandom(20), () =>
        {
            var knight = TestState.Player(new Knight("Fallen Victor"));
            knight.GainGold(100);
            var game = new GameEncounter(knight);
            var enemy = TestState.Enemy(health: 1);
            Assert.True(game.NewEncounter(new FixedArea(enemy)));
            Assert.True(game.CurrentEncount!.Attack());
            Assert.Equal(EncounterStatus.Victory, game.CurrentEncount.Status);

            knight.TakeDamage(1000);
            Assert.Equal(0, knight.Health);
            Item[] before = knight.Inventory.ToArray();
            int goldBefore = knight.Gold;
            int xpBefore = knight.Experience;
            EncounterCoordinator previous = game.CurrentEncount;

            Assert.False(game.NewEncounter(new FixedArea(TestState.Enemy())));
            Assert.False(game.BuyFromShop(new HealthPotion()));
            Assert.False(game.UseItemOutOfBattle(knight.Inventory[0]));
            Assert.Same(previous, game.CurrentEncount);
            Assert.Equal(goldBefore, knight.Gold);
            Assert.Equal(xpBefore, knight.Experience);
            Assert.Equal(0, knight.Health);
            AssertSameInventory(knight, before);
        });
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 35)]
    [InlineData(2, 25)]
    [InlineData(3, 45)]
    [InlineData(4, 100)]
    public void OfficialShop_UsesExactlyFiveSupportedItemTypesAndPrices(int itemType, int officialPrice)
    {
        var knight = TestState.Player(new Knight("Catalogue"));
        knight.GainGold(200);
        var game = new GameEncounter(knight);
        Item request = OfferedItem(itemType);
        int oldCount = knight.Inventory.Count;

        Assert.True(game.BuyFromShop(request));
        Assert.Equal(200 - officialPrice, knight.Gold);
        Assert.Equal(oldCount + 1, knight.Inventory.Count);
        Item bought = knight.Inventory[^1];
        Assert.Equal(request.GetType(), bought.GetType());
        Assert.Equal(officialPrice, bought.Cost);
        Assert.NotSame(request, bought);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void OfficialShop_IgnoresCallerSpoofedPrice(int spoofedPrice)
    {
        var knight = TestState.Player(new Knight("Fair Pricing"));
        knight.GainGold(20);
        Item request = new HealthPotion();
        TestState.SetItemCostForSpoofTest(request, spoofedPrice);
        var game = new GameEncounter(knight);

        Assert.True(game.BuyFromShop(request));
        Assert.Equal(0, knight.Gold);
        Assert.Equal(20, knight.Inventory[^1].Cost);
        Assert.NotSame(request, knight.Inventory[^1]);
    }

    [Fact]
    public void OfficialShop_RejectsUnsupportedItemsAndDoesNotSpendGold()
    {
        var knight = TestState.Player(new Knight("Unsupported Shop Item"));
        knight.GainGold(200);
        var game = new GameEncounter(knight);
        Item[] before = knight.Inventory.ToArray();

        Assert.False(game.BuyFromShop(new UnsupportedItem()));
        Assert.Equal(200, knight.Gold);
        AssertSameInventory(knight, before);
    }

    [Fact]
    public void OfficialShop_EachPurchaseCreatesDistinctInventoryItem()
    {
        var knight = TestState.Player(new Knight("Multiple Purchases"));
        knight.GainGold(60);
        var game = new GameEncounter(knight);
        var request = new HealthPotion();

        Assert.True(game.BuyFromShop(request));
        Item first = knight.Inventory[^1];
        Assert.True(game.BuyFromShop(request));
        Item second = knight.Inventory[^1];

        Assert.IsType<HealthPotion>(first);
        Assert.IsType<HealthPotion>(second);
        Assert.NotSame(first, second);
        Assert.NotSame(request, first);
        Assert.NotSame(request, second);
        Assert.Equal(20, knight.Gold);
    }

    [Fact]
    public void OfficialShop_InsufficientFundsLeavesInventoryAndGoldUnchanged()
    {
        var knight = TestState.Player(new Knight("Budget Shopper"));
        knight.GainGold(19);
        Item[] before = knight.Inventory.ToArray();
        var game = new GameEncounter(knight);

        Assert.False(game.BuyFromShop(new HealthPotion()));
        Assert.Equal(19, knight.Gold);
        AssertSameInventory(knight, before);
    }

    [Fact]
    public void SkillCooldown_PersistsAcrossEscapeAndNewEncounter()
    {
        var random = new HitSequenceRandom(2, 2, 2);
        TestState.WithRandom(random, () =>
        {
            var knight = TestState.Player(new Knight("Cooldown Persist"));
            var game = new GameEncounter(knight);
            Assert.True(game.NewEncounter(new FixedArea(TestState.Enemy(attack: 3))));
            EncounterCoordinator previous = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);

            Assert.True(previous.Skill());
            Assert.Equal(3, knight.SkillCooldown);
            Assert.True(previous.Flee());
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Escaped, previous.Status);

            Assert.True(game.NewEncounter(new FixedArea(TestState.Enemy(attack: 3))));
            Assert.NotSame(previous, game.CurrentEncount);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Same(knight, game.CurrentEncount!.Player);
            Assert.Equal(3, random.HitRolls);
        });
    }

    [Fact]
    public void Knight_ExtremeNegativeDamageCannotHarmPlayer()
    {
        var knight = TestState.Player(new Knight("Underflow Guard"), health: 50);

        // A negative input must not wrap when the Knight subtracts its passive reduction.
        knight.TakeDamage(int.MinValue);

        Assert.Equal(50, knight.Health);
    }

    [Fact]
    public void DeadArcher_NegativeDamageCannotRestoreHealth()
    {
        TestState.WithRandom(new HighRollRandom(), () =>
        {
            var archer = TestState.Player(new Archer("Dead Archer"));
            archer.TakeDamage(1000);
            Assert.Equal(0, archer.Health);

            archer.TakeDamage(-10);

            Assert.Equal(0, archer.Health);
        });
    }

    // The current Player.LevelUp guards against Health == 0.
    [Fact]
    public void DeadPlayer_LevelUpCannotUndoPermanentDefeat()
    {
        var knight = TestState.Player(new Knight("Defeated Adventurer"));
        knight.GainExperience(100);
        knight.TakeDamage(1000);
        Assert.Equal(0, knight.Health);

        knight.LevelUp();

        Assert.Equal(0, knight.Health);
        Assert.Equal(1, knight.Level);
        Assert.Equal(100, knight.Experience);
    }

    [Theory]
    [InlineData(0)] // Normal attack
    [InlineData(1)] // Skill
    [InlineData(2)] // Flee
    public void AlreadyDeadPlayer_CannotPerformActionsInStaleActiveEncounter(int action)
    {
        var random = new HitSequenceRandom(20, 20);
        TestState.WithRandom(random, () =>
        {
            // The skill case must have zero cooldown, otherwise rejection could
            // happen for the wrong reason and hide a missing death guard.
            int startingCooldown = action == 1 ? 0 : 2;
            var knight = TestState.Player(new Knight("Posthumous Action"), cooldown: startingCooldown);
            knight.GainGold(12);
            knight.GainExperience(7);
            Item[] before = knight.Inventory.ToArray();
            var enemy = TestState.Enemy();
            var encounter = new EncounterCoordinator(knight);
            Assert.True(encounter.Start(new FixedArea(enemy)));

            // Public damage methods can be called independently of the coordinator.
            // This leaves an Active encounter with an already-dead player.
            knight.TakeDamage(1000);
            Assert.Equal(0, knight.Health);

            bool accepted = action switch
            {
                0 => encounter.Attack(),
                1 => encounter.Skill(),
                2 => encounter.Flee(),
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };

            Assert.False(accepted);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            Assert.Equal(0, encounter.TurnCount);
            Assert.Equal(0, knight.Health);
            Assert.Equal(startingCooldown, knight.SkillCooldown);
            Assert.Equal(100, enemy.Health);
            Assert.Equal(12, knight.Gold);
            Assert.Equal(7, knight.Experience);
            Assert.Equal(0, random.HitRolls);
            AssertSameInventory(knight, before);
        });
    }
}
