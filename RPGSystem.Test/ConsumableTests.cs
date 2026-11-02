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

public class ConsumableTests
{
    // A fixed enemy removes area-spawn randomness from these UseItem tests.
    private sealed class FixedEnemyArea(Enemy enemy) : Area
    {
        public override Enemy SpawnEnemy() => enemy;
    }

    // Represents an inventory item that does not implement either consumable interface.
    private sealed class UnsupportedItem : Item { }

    // The first combat roll belongs to the item (for a scroll); the next belongs
    // to the enemy counterattack. Healing items have only the counterattack roll.
    // Damage calculations always receive the valid minimum of their range.
    private sealed class ScriptedRandom(params int[] hitRolls) : Random
    {
        private readonly Queue<int> _hitRolls = new(hitRolls);
        public int HitRollCount { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                if (_hitRolls.Count == 0)
                    throw new InvalidOperationException("An unexpected attack/counterattack occurred.");

                int result = _hitRolls.Dequeue();
                if (result < minValue || result >= maxValue)
                    throw new InvalidOperationException("An invalid scripted hit roll was supplied.");

                HitRollCount++;
                return result;
            }

            return minValue;
        }
    }

    private static void WithControlledRandom(Random random, Action test)
    {
        TestState.WithRandom(random, test);
    }

    private static (EncounterCoordinator Encounter, Enemy Enemy) StartEncounter(
        Knight knight, int enemyHealth = 200, int enemyAttack = 6)
    {
        var enemy = TestState.Enemy(maxHealth: Math.Max(200, enemyHealth), health: enemyHealth, armour: 10, attack: enemyAttack, experienceReward: 50, goldReward: 30);

        var encounter = new EncounterCoordinator(knight);
        Assert.True(encounter.Start(new FixedEnemyArea(enemy)));
        Assert.Same(enemy, encounter.Enemy);
        return (encounter, enemy);
    }

    private static void AssertConsumedExactlyOnce(Player player, Item selected, Item[] before)
    {
        Assert.Equal(before.Length - 1, player.Inventory.Count);
        int currentIndex = 0;
        foreach (Item original in before)
        {
            if (ReferenceEquals(original, selected))
                continue;

            Assert.Same(original, player.Inventory[currentIndex]);
            currentIndex++;
        }

        Assert.Equal(player.Inventory.Count, currentIndex);
        Assert.DoesNotContain(selected, player.Inventory);
    }

    private static void AssertInventoryUnchanged(Player player, Item[] before)
    {
        Assert.Equal(before.Length, player.Inventory.Count);
        for (int index = 0; index < before.Length; index++)
            Assert.Same(before[index], player.Inventory[index]);
    }

    [Theory]
    [InlineData(false, 50, 75)]
    [InlineData(false, 120, 130)]
    [InlineData(true, 50, 100)]
    [InlineData(true, 120, 130)]
    public void HealingPotion_RestoresCorrectAmountWithoutExceedingMaxHealth(
        bool greater, int startingHealth, int expectedHealth)
    {
        var rng = new ScriptedRandom(2); // Enemy attempts a counterattack but misses.
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Potion Test"), health: startingHealth, cooldown: 2);
            Item selected = greater ? new GreaterHealthPotion() : new HealthPotion();
            TestState.AddItem(knight, selected);
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.UseItem(selected));

            Assert.Equal(expectedHealth, knight.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            Assert.Equal(200, enemy.Health);
            Assert.Equal(1, rng.HitRollCount); // Enemy had an opportunity to respond.
            AssertConsumedExactlyOnce(knight, selected, before);
        });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(130)]
    public void Elixir_RestoresFullHealthAndIsConsumed(int startingHealth)
    {
        var rng = new ScriptedRandom(2);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Elixir Test"), health: startingHealth);
            var elixir = new Elixir();
            TestState.AddItem(knight, elixir);
            Item[] before = knight.Inventory.ToArray();
            var (encounter, _) = StartEncounter(knight);

            Assert.True(encounter.UseItem(elixir));

            Assert.Equal(knight.MaxHealth, knight.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(0, knight.SkillCooldown); // A zero cooldown stays at zero.
            Assert.Equal(1, rng.HitRollCount);
            AssertConsumedExactlyOnce(knight, elixir, before);
        });
    }

    [Fact]
    public void HealingPotion_SecondPotion_ConsumesOnlyTheSelectedInstance()
    {
        var rng = new ScriptedRandom(2);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Selected Item Test"), health: 80);
            Item firstPotion = knight.Inventory[0];
            Item secondPotion = knight.Inventory[1];
            Item[] before = knight.Inventory.ToArray();
            var (encounter, _) = StartEncounter(knight);

            Assert.True(encounter.UseItem(secondPotion));

            Assert.Same(firstPotion, knight.Inventory[0]);
            AssertConsumedExactlyOnce(knight, secondPotion, before);
            Assert.Equal(105, knight.Health);
            Assert.Equal(1, encounter.TurnCount);
        });
    }

    [Theory]
    [InlineData(false, 8, 22)]  // 8 + Knight Attack/2 (2) meets Armour 10.
    [InlineData(false, 20, 44)] // Critical hit doubles min damage (25 - 3).
    [InlineData(false, 2, 0)]   // Ordinary miss.
    [InlineData(true, 8, 47)]  // Greater Scroll (50 - 3).
    [InlineData(true, 20, 94)] // Greater Scroll critical hit.
    public void DamageScroll_RespectsHitAndDamageRules(
        bool greater, int itemHitRoll, int expectedDamage)
    {
        var rng = new ScriptedRandom(itemHitRoll, 2); // Enemy counterattack misses.
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Scroll Test"), cooldown: 2);
            Item selected = greater ? new GreaterDamageScroll() : new DamageScroll();
            TestState.AddItem(knight, selected);
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.UseItem(selected));

            Assert.Equal(200 - expectedDamage, enemy.Health);
            Assert.Equal(knight.MaxHealth, knight.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            Assert.Equal(2, rng.HitRollCount); // Item roll and enemy response.
            AssertConsumedExactlyOnce(knight, selected, before);
        });
    }

    [Fact]
    public void DamageScroll_CriticalFailure_HurtsPlayerButNotEnemy()
    {
        var rng = new ScriptedRandom(1, 2);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Critical Failure Test"), health: 100);
            Item selected = knight.Inventory[2];
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.UseItem(selected));

            // Critical failure: 22 / 2 = 11 raw self-damage; Knight reduces it by 3.
            Assert.Equal(92, knight.Health);
            Assert.Equal(200, enemy.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(2, rng.HitRollCount); // Player fails, enemy still responds.
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            AssertConsumedExactlyOnce(knight, selected, before);
        });
    }

    [Theory]
    [InlineData(false)] // Potion first heals, then the enemy counterattacks.
    [InlineData(true)]  // Damage Scroll first damages, then the enemy counterattacks.
    public void AcceptedItem_TriggersEnemyCounterattackAndReducesCooldown(bool damageScroll)
    {
        var rng = damageScroll ? new ScriptedRandom(8, 20) : new ScriptedRandom(20);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Counterattack Test"), health: damageScroll ? 130 : 100, cooldown: 2);
            var (encounter, enemy) = StartEncounter(knight);
            Item selected = damageScroll ? knight.Inventory[2] : knight.Inventory[0];
            Item[] before = knight.Inventory.ToArray();

            Assert.True(encounter.UseItem(selected));

            // Enemy Attack 6 -> min damage 3, critical 6; Knight reduces by 3.
            Assert.Equal(damageScroll ? 127 : 122, knight.Health);
            Assert.Equal(damageScroll ? 178 : 200, enemy.Health);
            Assert.Equal(damageScroll ? 2 : 1, rng.HitRollCount);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            AssertConsumedExactlyOnce(knight, selected, before);
        });
    }

    [Fact]
    public void DamageScroll_FinishingHit_AwardsRewardsOnceAndSkipsEnemyResponse()
    {
        var rng = new ScriptedRandom(8); // Only the item should roll to hit.
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Victory Test"), cooldown: 2);
            knight.GainExperience(75);
            knight.GainGold(20);
            Item selected = knight.Inventory[2];
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight, enemyHealth: 10);

            Assert.True(encounter.UseItem(selected));

            Assert.Equal(0, enemy.Health);
            Assert.Equal(EncounterStatus.Victory, encounter.Status);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown); // Even a finishing item reduces cooldown.
            Assert.Equal(25, knight.Experience);    // 75 + 50 - 100 level-up cost.
            Assert.Equal(50, knight.Gold);
            Assert.Equal(2, knight.Level);
            Assert.Equal(1, rng.HitRollCount); // No enemy counterattack after victory.
            AssertConsumedExactlyOnce(knight, selected, before);

            Item[] afterVictory = knight.Inventory.ToArray();
            Item remainingItem = knight.Inventory[0];
            int healthAfterVictory = knight.Health;
            Assert.False(encounter.UseItem(remainingItem));
            AssertInventoryUnchanged(knight, afterVictory);
            Assert.Equal(healthAfterVictory, knight.Health);
            Assert.Equal(25, knight.Experience);
            Assert.Equal(50, knight.Gold);
            Assert.Equal(2, knight.Level);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, rng.HitRollCount);
        });
    }

    [Fact]
    public void DamageScroll_LethalCriticalFailure_ConsumesItemAndCausesDefeatWithoutRewards()
    {
        var rng = new ScriptedRandom(1); // No counterattack if the player defeats itself.
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Self-Defeat Test"), health: 8, cooldown: 2);
            knight.GainExperience(75);
            knight.GainGold(20);
            Item selected = knight.Inventory[2];
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.UseItem(selected));

            Assert.Equal(0, knight.Health);
            Assert.Equal(200, enemy.Health);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Assert.Equal(75, knight.Experience);
            Assert.Equal(20, knight.Gold);
            Assert.Equal(1, knight.Level);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(1, rng.HitRollCount);
            AssertConsumedExactlyOnce(knight, selected, before);

            Item[] afterDefeat = knight.Inventory.ToArray();
            Item remainingItem = knight.Inventory[0];
            Assert.False(encounter.UseItem(remainingItem));
            AssertInventoryUnchanged(knight, afterDefeat);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(0, knight.Health);
            Assert.Equal(200, enemy.Health);
            Assert.Equal(1, rng.HitRollCount);
            Assert.Equal(75, knight.Experience);
            Assert.Equal(20, knight.Gold);
        });
    }

    [Fact]
    public void HealingPotion_EnemyCounterattackCanDefeatPlayerAfterConsumption()
    {
        var rng = new ScriptedRandom(20);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Healing Defeat Test"), health: 1, cooldown: 2);
            Item selected = knight.Inventory[0];
            Item[] before = knight.Inventory.ToArray();
            var (encounter, _) = StartEncounter(knight, enemyAttack: 20);

            Assert.True(encounter.UseItem(selected));

            // Heal to 26; enemy critical hit deals (20 - 3) * 2 - 3 armour passive = 31.
            Assert.Equal(0, knight.Health);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(1, knight.SkillCooldown);
            Assert.Equal(1, rng.HitRollCount);
            AssertConsumedExactlyOnce(knight, selected, before);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UseItem_UnownedItem_DoesNotChangeState(bool damageItem)
    {
        var rng = new ScriptedRandom();
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Unowned Item Test"), health: 90, cooldown: 2);
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);
            Item notOwned = damageItem ? new DamageScroll() : new HealthPotion();

            Assert.False(encounter.UseItem(notOwned));

            Assert.Equal(0, encounter.TurnCount);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(90, knight.Health);
            Assert.Equal(200, enemy.Health);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            AssertInventoryUnchanged(knight, before);
            Assert.Equal(0, rng.HitRollCount);
        });
    }

    [Fact]
    public void UseItem_UnsupportedItem_DoesNotChangeState()
    {
        var rng = new ScriptedRandom();
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Unsupported Item Test"), health: 90, cooldown: 2);
            Item unsupported = new UnsupportedItem();
            TestState.AddItem(knight, unsupported);
            Item[] before = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.False(encounter.UseItem(unsupported));

            Assert.Equal(0, encounter.TurnCount);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(90, knight.Health);
            Assert.Equal(200, enemy.Health);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            AssertInventoryUnchanged(knight, before);
            Assert.Equal(0, rng.HitRollCount);
        });
    }

    [Fact]
    public void UseItem_BeforeEncounterStarts_IsRejectedWithoutChangingState()
    {
        var rng = new ScriptedRandom();
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Not Started Test"), health: 90, cooldown: 2);
            Item[] before = knight.Inventory.ToArray();
            Item selected = knight.Inventory[0];
            var encounter = new EncounterCoordinator(knight);

            Assert.False(encounter.UseItem(selected));

            Assert.Equal(EncounterStatus.NotStarted, encounter.Status);
            Assert.Equal(0, encounter.TurnCount);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(90, knight.Health);
            Assert.Null(encounter.Enemy);
            AssertInventoryUnchanged(knight, before);
            Assert.Equal(0, rng.HitRollCount);
        });
    }
}
