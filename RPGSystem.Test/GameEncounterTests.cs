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

public class GameEncounterTests
{
    private sealed class FixedArea : Area
    {
        private readonly Enemy _enemy;

        public FixedArea(Enemy enemy, int requiredLevel = 1)
        {
            _enemy = enemy;
            RequiredLevel = requiredLevel;
            Name = "Fixed Test Area";
        }

        public int SpawnCount { get; private set; }

        public override Enemy SpawnEnemy()
        {
            SpawnCount++;
            return _enemy;
        }
    }

    // Throws if an unplanned combat roll occurs. Other damage rolls use
    // the valid lower bound, so damage is predictable.
    private sealed class ScriptedRandom(params int[] hitRolls) : Random
    {
        private readonly Queue<int> _hitRolls = new(hitRolls);
        public int HitCount { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                if (_hitRolls.Count == 0)
                    throw new InvalidOperationException("Unexpected combat attack.");

                int hit = _hitRolls.Dequeue();
                if (hit < minValue || hit >= maxValue)
                    throw new InvalidOperationException("Scripted hit is out of range.");

                HitCount++;
                return hit;
            }

            return minValue;
        }
    }

    private sealed class UnsupportedItem : Item { }

    private static void WithRandom(Random controlledRandom, Action test)
    {
        TestState.WithRandom(controlledRandom, test);
    }

    private static Enemy CreateEnemy(int health = 100, int attack = 10) =>
        TestState.Enemy(maxHealth: Math.Max(100, health), health: health, attack: attack);

    private static (GameEncounter Game, Knight Knight, EncounterCoordinator Encounter, Enemy Enemy)
        StartGame(int enemyHealth = 100, int enemyAttack = 10)
    {
        var knight = TestState.Player(new Knight("Coordinator Test"));
        var game = new GameEncounter(knight);
        var enemy = CreateEnemy(enemyHealth, enemyAttack);
        var area = new FixedArea(enemy);

        Assert.True(game.NewEncounter(area));
        Assert.Equal(1, area.SpawnCount);
        EncounterCoordinator encounter = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);
        Assert.Same(knight, encounter.Player);
        Assert.Same(enemy, encounter.Enemy);
        return (game, knight, encounter, enemy);
    }

    private static void AssertInventorySame(Player player, Item[] expected)
    {
        Assert.Equal(expected.Length, player.Inventory.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.Same(expected[i], player.Inventory[i]);
    }

    private static void AssertRemovedOnlySelected(Player player, Item[] before, Item selected)
    {
        Assert.Equal(before.Length - 1, player.Inventory.Count);
        int index = 0;
        foreach (Item original in before)
        {
            if (ReferenceEquals(original, selected))
                continue;

            Assert.Same(original, player.Inventory[index]);
            index++;
        }
        Assert.DoesNotContain(selected, player.Inventory);
    }

    [Fact]
    public void NewEncounter_FirstEncounter_SetsActiveEncounterWithSamePlayer()
    {
        var knight = TestState.Player(new Knight("Persistent Knight"));
        var game = new GameEncounter(knight);
        var enemy = CreateEnemy();
        var area = new FixedArea(enemy);

        Assert.Null(game.CurrentEncount);
        Assert.True(game.NewEncounter(area));

        EncounterCoordinator encounter = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);
        Assert.Same(knight, game.Player);
        Assert.Same(knight, encounter.Player);
        Assert.Same(area, encounter.Area);
        Assert.Same(enemy, encounter.Enemy);
        Assert.Equal(EncounterStatus.Active, encounter.Status);
        Assert.Equal(0, encounter.TurnCount);
        Assert.Equal(1, area.SpawnCount);
    }

    [Fact]
    public void NewEncounter_AfterVictory_PreservesPlayerStateAndCreatesFreshEncounter()
    {
        var rng = new ScriptedRandom(20); // Player wins immediately; no counterattack.
        WithRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Returning Victor"), health: 80, cooldown: 3);
            knight.GainGold(40);
            knight.GainExperience(20);
            Item extra = new HealthPotion();
            TestState.AddItem(knight, extra);
            Item[] originalInventory = knight.Inventory.ToArray();
            var game = new GameEncounter(knight);
            var firstEnemy = CreateEnemy(health: 1);
            Assert.True(game.NewEncounter(new FixedArea(firstEnemy)));
            EncounterCoordinator first = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);

            Assert.True(first.Attack());
            Assert.Equal(EncounterStatus.Victory, first.Status);
            Assert.Equal(70, knight.Experience); // 20 prior + 50 victory; no level-up.
            Assert.Equal(70, knight.Gold); // 40 prior + 30 victory.
            Assert.Equal(2, knight.SkillCooldown);

            var secondEnemy = CreateEnemy();
            var secondArea = new FixedArea(secondEnemy);
            Assert.True(game.NewEncounter(secondArea));

            EncounterCoordinator second = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);
            Assert.NotSame(first, second);
            Assert.Equal(EncounterStatus.Active, second.Status);
            Assert.Equal(0, second.TurnCount);
            Assert.Same(knight, second.Player);
            Assert.Same(secondEnemy, second.Enemy);
            Assert.Same(secondArea, second.Area);
            Assert.Equal(80, knight.Health);
            Assert.Equal(70, knight.Experience);
            Assert.Equal(70, knight.Gold);
            Assert.Equal(2, knight.SkillCooldown);
            AssertInventorySame(knight, originalInventory);
            Assert.Contains(extra, knight.Inventory);
            Assert.Equal(1, rng.HitCount);
        });
    }

    [Fact]
    public void NewEncounter_AfterEscape_PreservesHealthInventoryGoldExperienceAndCooldown()
    {
        var rng = new ScriptedRandom(2); // Enemy misses the final flee attack.
        WithRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Returning Escapee"), health: 77, cooldown: 2);
            knight.GainGold(29);
            knight.GainExperience(12);
            Item extra = new Elixir();
            TestState.AddItem(knight, extra);
            Item[] before = knight.Inventory.ToArray();
            var game = new GameEncounter(knight);
            Assert.True(game.NewEncounter(new FixedArea(CreateEnemy())));
            EncounterCoordinator first = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);

            Assert.True(first.Flee());
            Assert.Equal(EncounterStatus.Escaped, first.Status);
            Assert.Equal(1, knight.SkillCooldown);

            var nextEnemy = CreateEnemy();
            Assert.True(game.NewEncounter(new FixedArea(nextEnemy)));
            EncounterCoordinator next = Assert.IsType<EncounterCoordinator>(game.CurrentEncount);

            Assert.NotSame(first, next);
            Assert.Same(knight, next.Player);
            Assert.Same(nextEnemy, next.Enemy);
            Assert.Equal(EncounterStatus.Active, next.Status);
            Assert.Equal(0, next.TurnCount);
            Assert.Equal(77, knight.Health);
            Assert.Equal(29, knight.Gold);
            Assert.Equal(12, knight.Experience);
            Assert.Equal(1, knight.SkillCooldown);
            AssertInventorySame(knight, before);
            Assert.Equal(1, rng.HitCount);
        });
    }

    [Fact]
    public void NewEncounter_WhileActive_RejectsWithoutReplacingCurrentEncounter()
    {
        var (game, knight, current, enemy) = StartGame();
        var nextArea = new FixedArea(CreateEnemy());
        Item[] before = knight.Inventory.ToArray();

        Assert.False(game.NewEncounter(nextArea));

        Assert.Same(current, game.CurrentEncount);
        Assert.Equal(0, nextArea.SpawnCount);
        Assert.Equal(EncounterStatus.Active, current.Status);
        Assert.Equal(0, current.TurnCount);
        Assert.Equal(100, enemy.Health);
        Assert.Equal(knight.MaxHealth, knight.Health);
        Assert.Equal(0, knight.Gold);
        Assert.Equal(0, knight.Experience);
        AssertInventorySame(knight, before);
    }

    [Fact]
    public void NewEncounter_AfterDefeat_RejectsWithoutReplacingCurrentEncounter()
    {
        var rng = new ScriptedRandom(20); // Lethal enemy attack during flee.
        WithRandom(rng, () =>
        {
            var (game, knight, current, _) = StartGame(enemyAttack: 20);
            TestState.Set(knight, nameof(Character.Health), 1);
            knight.GainGold(17);
            Item[] before = knight.Inventory.ToArray();
            Assert.True(current.Flee());
            Assert.Equal(EncounterStatus.Defeat, current.Status);
            int turns = current.TurnCount;
            int cooldown = knight.SkillCooldown;
            var nextArea = new FixedArea(CreateEnemy());

            Assert.False(game.NewEncounter(nextArea));
            Assert.Same(current, game.CurrentEncount);
            Assert.Equal(0, nextArea.SpawnCount);
            Assert.Equal(turns, current.TurnCount);
            Assert.Equal(cooldown, knight.SkillCooldown);
            Assert.Equal(0, knight.Health);
            Assert.Equal(17, knight.Gold);
            Assert.Equal(0, knight.Experience);
            AssertInventorySame(knight, before);
            Assert.Equal(1, rng.HitCount);
        });
    }

    [Fact]
    public void NewEncounter_InitialAreaIneligible_KeepsCurrentEncounterNull()
    {
        var knight = TestState.Player(new Knight("Underleveled"));
        var game = new GameEncounter(knight);
        var forbiddenArea = new FixedArea(CreateEnemy(), requiredLevel: 2);

        Assert.False(game.NewEncounter(forbiddenArea));
        Assert.Null(game.CurrentEncount);
        Assert.Equal(0, forbiddenArea.SpawnCount);
        Assert.Same(knight, game.Player);
    }

    [Theory]
    [InlineData(EncounterStatus.Victory)]
    [InlineData(EncounterStatus.Escaped)]
    public void NewEncounter_IneligibleAreaAfterTerminalState_KeepsPreviousEncounter(EncounterStatus terminal)
    {
        var rng = new ScriptedRandom(terminal == EncounterStatus.Victory ? 20 : 2);
        WithRandom(rng, () =>
        {
            var (game, knight, previous, enemy) = StartGame(enemyHealth: 1);
            if (terminal == EncounterStatus.Victory)
                Assert.True(previous.Attack());
            else
                Assert.True(previous.Flee());

            Assert.Equal(terminal, previous.Status);
            int gold = knight.Gold;
            int experience = knight.Experience;
            int health = knight.Health;
            int cooldown = knight.SkillCooldown;
            Item[] before = knight.Inventory.ToArray();
            var forbidden = new FixedArea(CreateEnemy(), requiredLevel: 10);

            Assert.False(game.NewEncounter(forbidden));
            Assert.Same(previous, game.CurrentEncount);
            Assert.Same(enemy, previous.Enemy);
            Assert.Equal(0, forbidden.SpawnCount);
            Assert.Equal(terminal, previous.Status);
            Assert.Equal(1, previous.TurnCount);
            Assert.Equal(gold, knight.Gold);
            Assert.Equal(experience, knight.Experience);
            Assert.Equal(health, knight.Health);
            Assert.Equal(cooldown, knight.SkillCooldown);
            AssertInventorySame(knight, before);
            Assert.Equal(1, rng.HitCount);
        });
    }

    [Theory]
    [InlineData(0, 90, 115)] // Standard potion heals 25.
    [InlineData(0, 125, 130)] // Healing respects MaxHealth.
    [InlineData(1, 90, 130)] // Greater potion heals 50, capped.
    [InlineData(2, 20, 130)] // Elixir restores full health.
    [InlineData(2, 130, 130)] // Elixir at full health remains full.
    public void UseItemOutOfBattle_BeforeFirstEncounter_HealsAndConsumesExactlyOnce(
        int itemKind, int startingHealth, int expectedHealth)
    {
        var knight = TestState.Player(new Knight("Out Of Combat"), health: startingHealth, cooldown: 2);
        var game = new GameEncounter(knight);
        Item selected = itemKind switch
        {
            0 => knight.Inventory[0],
            1 => new GreaterHealthPotion(),
            2 => new Elixir(),
            _ => throw new ArgumentOutOfRangeException(nameof(itemKind))
        };
        if (itemKind != 0)
            TestState.AddItem(knight, selected);

        Item[] before = knight.Inventory.ToArray();
        Assert.True(game.UseItemOutOfBattle(selected));
        Assert.Equal(expectedHealth, knight.Health);
        Assert.Equal(2, knight.SkillCooldown);
        Assert.Null(game.CurrentEncount);
        AssertRemovedOnlySelected(knight, before, selected);
        Assert.False(game.UseItemOutOfBattle(selected));
        Assert.Equal(expectedHealth, knight.Health);
    }

    [Theory]
    [InlineData(EncounterStatus.Victory)]
    [InlineData(EncounterStatus.Escaped)]
    public void UseItemOutOfBattle_AfterTerminalNonDefeat_HealsWithoutChangingEncounter(
        EncounterStatus terminal)
    {
        var rng = new ScriptedRandom(terminal == EncounterStatus.Victory ? 20 : 2);
        WithRandom(rng, () =>
        {
            var (game, knight, previous, _) = StartGame(enemyHealth: 1);
            if (terminal == EncounterStatus.Victory)
                Assert.True(previous.Attack());
            else
                Assert.True(previous.Flee());

            Assert.Equal(terminal, previous.Status);
            TestState.Set(knight, nameof(Character.Health), 80);
            int turns = previous.TurnCount;
            int gold = knight.Gold;
            int xp = knight.Experience;
            Item selected = knight.Inventory[1]; // Distinguish second matching potion.
            Item[] before = knight.Inventory.ToArray();

            Assert.True(game.UseItemOutOfBattle(selected));
            Assert.Equal(105, knight.Health);
            AssertRemovedOnlySelected(knight, before, selected);
            Assert.Same(previous, game.CurrentEncount);
            Assert.Equal(terminal, previous.Status);
            Assert.Equal(turns, previous.TurnCount);
            Assert.Equal(gold, knight.Gold);
            Assert.Equal(xp, knight.Experience);
            Assert.Equal(1, rng.HitCount); // No new combat occurs during healing.
        });
    }

    [Fact]
    public void UseItemOutOfBattle_UnownedHealingItem_IsRejectedWithoutChanges()
    {
        var knight = TestState.Player(new Knight("Not Owned"), health: 50);
        var game = new GameEncounter(knight);
        Item[] before = knight.Inventory.ToArray();
        var unowned = new HealthPotion(); // Identical type/name, different object.

        Assert.False(game.UseItemOutOfBattle(unowned));
        Assert.Equal(50, knight.Health);
        AssertInventorySame(knight, before);
    }

    [Fact]
    public void UseItemOutOfBattle_OwnedDamageItem_IsRejectedWithoutChanges()
    {
        var knight = TestState.Player(new Knight("Scroll Rejected"), health: 50);
        var game = new GameEncounter(knight);
        Item[] before = knight.Inventory.ToArray();

        Assert.False(game.UseItemOutOfBattle(knight.Inventory[2]));
        Assert.Equal(50, knight.Health);
        AssertInventorySame(knight, before);
    }

    [Fact]
    public void UseItemOutOfBattle_UnsupportedOwnedItem_IsRejectedWithoutChanges()
    {
        var knight = TestState.Player(new Knight("Unsupported"), health: 50);
        TestState.AddItem(knight, new UnsupportedItem());
        var game = new GameEncounter(knight);
        Item[] before = knight.Inventory.ToArray();

        Assert.False(game.UseItemOutOfBattle(knight.Inventory[^1]));
        Assert.Equal(50, knight.Health);
        AssertInventorySame(knight, before);
    }

    [Fact]
    public void UseItemOutOfBattle_WhileActive_IsRejectedWithoutChanges()
    {
        var rng = new ScriptedRandom();
        WithRandom(rng, () =>
        {
            var (game, knight, encounter, enemy) = StartGame();
            TestState.Set(knight, nameof(Character.Health), 80);
            TestState.Set(knight, nameof(Character.SkillCooldown), 2);
            Item[] before = knight.Inventory.ToArray();

            Assert.False(game.UseItemOutOfBattle(knight.Inventory[0]));
            Assert.Equal(80, knight.Health);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(100, enemy.Health);
            Assert.Equal(0, encounter.TurnCount);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            AssertInventorySame(knight, before);
            Assert.Equal(0, rng.HitCount);
        });
    }

    [Fact]
    public void UseItemOutOfBattle_AfterDefeat_IsRejectedWithoutChanges()
    {
        var rng = new ScriptedRandom(20);
        WithRandom(rng, () =>
        {
            var (game, knight, encounter, _) = StartGame(enemyAttack: 20);
            TestState.Set(knight, nameof(Character.Health), 1);
            Assert.True(encounter.Flee());
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Item[] before = knight.Inventory.ToArray();

            Assert.False(game.UseItemOutOfBattle(knight.Inventory[0]));
            Assert.Equal(0, knight.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            AssertInventorySame(knight, before);
            Assert.Equal(1, rng.HitCount);
        });
    }

    [Theory]
    [InlineData(20, true, 0)] // Exact affordability.
    [InlineData(25, true, 5)]
    [InlineData(19, false, 19)] // Insufficient funds.
    [InlineData(0, false, 0)]
    public void BuyFromShop_ChargesOnlyWhenAffordableAndAddsOwnedInstance(
        int startingGold, bool expectedSuccess, int expectedGold)
    {
        var knight = TestState.Player(new Knight("Shop Customer"));
        knight.GainGold(startingGold);
        var game = new GameEncounter(knight);
        Item item = new HealthPotion(); // Cost 20.
        Item[] before = knight.Inventory.ToArray();

        Assert.Equal(expectedSuccess, game.BuyFromShop(item));
        Assert.Equal(expectedGold, knight.Gold);
        Assert.Null(game.CurrentEncount);
        Assert.Equal(before.Length + (expectedSuccess ? 1 : 0), knight.Inventory.Count);
        for (int index = 0; index < before.Length; index++)
            Assert.Same(before[index], knight.Inventory[index]);

        if (expectedSuccess)
        {
            Assert.IsType<HealthPotion>(knight.Inventory[^1]);
            Assert.Equal(20, knight.Inventory[^1].Cost);
            Assert.NotSame(item, knight.Inventory[^1]);
        }
        else
            Assert.DoesNotContain(item, knight.Inventory);
    }

    [Fact]
    public void BuyFromShop_SpoofedZeroPrice_UsesOfficialCostAndFreshItem()
    {
        var knight = TestState.Player(new Knight("Official Price Test"));
        knight.GainGold(25);
        var game = new GameEncounter(knight);
        Item offered = new HealthPotion();
        TestState.SetItemCostForSpoofTest(offered, 0);

        Assert.True(game.BuyFromShop(offered));
        Assert.Equal(5, knight.Gold); // Official Health Potion price: 20.
        Assert.IsType<HealthPotion>(knight.Inventory[^1]);
        Assert.Equal(20, knight.Inventory[^1].Cost);
        Assert.NotSame(offered, knight.Inventory[^1]);
    }

    [Theory]
    [InlineData(EncounterStatus.Victory)]
    [InlineData(EncounterStatus.Escaped)]
    public void BuyFromShop_AfterTerminalNonDefeat_IsAllowed(EncounterStatus terminal)
    {
        var rng = new ScriptedRandom(terminal == EncounterStatus.Victory ? 20 : 2);
        WithRandom(rng, () =>
        {
            var (game, knight, encounter, _) = StartGame(enemyHealth: 1);
            knight.GainGold(20);
            if (terminal == EncounterStatus.Victory)
                Assert.True(encounter.Attack());
            else
                Assert.True(encounter.Flee());

            Assert.Equal(terminal, encounter.Status);
            int goldBefore = knight.Gold;
            Item item = new HealthPotion();
            int inventoryCount = knight.Inventory.Count;

            Assert.True(game.BuyFromShop(item));
            Assert.Equal(goldBefore - item.Cost, knight.Gold);
            Assert.Equal(inventoryCount + 1, knight.Inventory.Count);
            Assert.IsType<HealthPotion>(knight.Inventory[^1]);
            Assert.NotSame(item, knight.Inventory[^1]);
            Assert.Same(encounter, game.CurrentEncount);
            Assert.Equal(terminal, encounter.Status);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, rng.HitCount);
        });
    }

    [Fact]
    public void BuyFromShop_WhileActive_IsRejectedWithoutChanges()
    {
        var rng = new ScriptedRandom();
        WithRandom(rng, () =>
        {
            var (game, knight, encounter, enemy) = StartGame();
            knight.GainGold(100);
            TestState.Set(knight, nameof(Character.Health), 80);
            TestState.Set(knight, nameof(Character.SkillCooldown), 2);
            Item[] before = knight.Inventory.ToArray();
            var offered = new HealthPotion();

            Assert.False(game.BuyFromShop(offered));
            Assert.Equal(100, knight.Gold);
            Assert.Equal(80, knight.Health);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(0, encounter.TurnCount);
            Assert.Equal(100, enemy.Health);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            AssertInventorySame(knight, before);
            Assert.DoesNotContain(offered, knight.Inventory);
            Assert.Equal(0, rng.HitCount);
        });
    }

    [Fact]
    public void BuyFromShop_AfterDefeat_IsRejectedWithoutChanges()
    {
        var rng = new ScriptedRandom(20);
        WithRandom(rng, () =>
        {
            var (game, knight, encounter, _) = StartGame(enemyAttack: 20);
            knight.GainGold(100);
            TestState.Set(knight, nameof(Character.Health), 1);
            Assert.True(encounter.Flee());
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Item[] before = knight.Inventory.ToArray();
            var offered = new HealthPotion();

            Assert.False(game.BuyFromShop(offered));
            Assert.Equal(100, knight.Gold);
            Assert.Equal(0, knight.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            AssertInventorySame(knight, before);
            Assert.DoesNotContain(offered, knight.Inventory);
            Assert.Equal(1, rng.HitCount);
        });
    }
}
