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

public class FleeTests
{
    // Avoid area-spawn randomness: each test knows the exact enemy it faces.
    private sealed class FixedEnemyArea(Enemy enemy) : Area
    {
        public override Enemy SpawnEnemy() => enemy;
    }

    // One scripted hit roll is supplied per accepted combat action.
    // If an unexpected additional attack occurs, the test fails immediately.
    private sealed class OneAttackRandom(int hitRoll) : Random
    {
        public int HitRollCount { get; private set; }
        public int DamageRollCount { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                HitRollCount++;
                if (HitRollCount != 1)
                    throw new InvalidOperationException("An unexpected additional attack occurred.");
                if (hitRoll < minValue || hitRoll >= maxValue)
                    throw new InvalidOperationException("The scripted hit roll is out of range.");
                return hitRoll;
            }

            DamageRollCount++;
            if (DamageRollCount != 1)
                throw new InvalidOperationException("An unexpected additional damage calculation occurred.");
            return minValue;
        }
    }

    private static void WithControlledRandom(Random rng, Action test)
    {
        TestState.WithRandom(rng, test);
    }

    private static (EncounterCoordinator Encounter, Enemy Enemy) StartEncounter(Knight knight)
    {
        var enemy = TestState.Enemy(maxHealth: 100, health: 100, attack: 10, armour: 10, experienceReward: 50, goldReward: 30);

        var encounter = new EncounterCoordinator(knight);
        Assert.True(encounter.Start(new FixedEnemyArea(enemy)));
        Assert.Same(enemy, encounter.Enemy);
        return (encounter, enemy);
    }

    private static void AssertInventoryUnchanged(Player player, Item[] before)
    {
        Assert.Equal(before.Length, player.Inventory.Count);
        for (int index = 0; index < before.Length; index++)
            Assert.Same(before[index], player.Inventory[index]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    public void Flee_Survives_CountsTurnReducesCooldownAndTakesOneFinalHit(
        int initialCooldown, int expectedCooldown)
    {
        var rng = new OneAttackRandom(20); // Guaranteed enemy critical hit.
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Escape Test"), cooldown: initialCooldown);
            knight.GainExperience(12);
            knight.GainGold(7);
            Item[] inventoryBefore = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.Flee());

            // Enemy Attack 10: minimum damage 7, critical hit 14;
            // Knight's passive subtracts 3, so the player loses 11 HP.
            Assert.Equal(119, knight.Health);
            Assert.Equal(100, enemy.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(expectedCooldown, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Escaped, encounter.Status);
            Assert.Equal(1, rng.HitRollCount);
            Assert.Equal(1, rng.DamageRollCount);
            Assert.Equal(12, knight.Experience);
            Assert.Equal(7, knight.Gold);
            Assert.Equal(1, knight.Level);
            AssertInventoryUnchanged(knight, inventoryBefore);
        });
    }

    [Fact]
    public void Flee_WhenFinalEnemyAttackMisses_StillEscapes()
    {
        var rng = new OneAttackRandom(2); // Ordinary miss against Knight's armour 18.
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Dodge Test"));
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.Flee());

            Assert.Equal(knight.MaxHealth, knight.Health);
            Assert.Equal(100, enemy.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(0, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Escaped, encounter.Status);
            Assert.Equal(1, rng.HitRollCount);
            Assert.Equal(1, rng.DamageRollCount); // Damage is rolled even on a miss.
        });
    }

    [Fact]
    public void Flee_FinalEnemyAttackKillsPlayer_EndsInDefeatWithoutRewards()
    {
        var rng = new OneAttackRandom(20);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Last Stand"), health: 1, cooldown: 3);
            knight.GainExperience(12);
            knight.GainGold(7);
            Item[] inventoryBefore = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            Assert.True(encounter.Flee());

            Assert.Equal(0, knight.Health);
            Assert.Equal(100, enemy.Health);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(12, knight.Experience);
            Assert.Equal(7, knight.Gold);
            Assert.Equal(1, knight.Level);
            Assert.Equal(1, rng.HitRollCount);
            Assert.Equal(1, rng.DamageRollCount);
            AssertInventoryUnchanged(knight, inventoryBefore);
        });
    }

    [Fact]
    public void Flee_BeforeStart_IsRejectedWithoutAnyStateChanges()
    {
        var rng = new OneAttackRandom(20);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Not Started"), health: 90, cooldown: 2);
            knight.GainExperience(12);
            knight.GainGold(7);
            Item[] inventoryBefore = knight.Inventory.ToArray();
            var encounter = new EncounterCoordinator(knight);

            Assert.False(encounter.Flee());

            Assert.Equal(EncounterStatus.NotStarted, encounter.Status);
            Assert.Equal(0, encounter.TurnCount);
            Assert.Null(encounter.Enemy);
            Assert.Equal(90, knight.Health);
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(12, knight.Experience);
            Assert.Equal(7, knight.Gold);
            Assert.Equal(0, rng.HitRollCount);
            Assert.Equal(0, rng.DamageRollCount);
            AssertInventoryUnchanged(knight, inventoryBefore);
        });
    }

    [Theory]
    [InlineData(EncounterStatus.Victory)]
    [InlineData(EncounterStatus.Defeat)]
    [InlineData(EncounterStatus.Escaped)]
    public void Flee_AfterEncounterEnds_IsRejectedWithoutChanges(EncounterStatus finalStatus)
    {
        int roll = 20; // One critical hit is enough for each terminal-case setup.
        var rng = new OneAttackRandom(roll);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("Finished Encounter"), cooldown: 2);
            knight.GainExperience(12);
            knight.GainGold(7);
            Item[] inventoryBefore = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);

            // Reach each terminal status by a real accepted action.
            switch (finalStatus)
            {
                case EncounterStatus.Victory:
                    TestState.Set(enemy, nameof(Character.Health), 1);
                    Assert.True(encounter.Attack());
                    break;
                case EncounterStatus.Defeat:
                    TestState.Set(knight, nameof(Character.Health), 1);
                    Assert.True(encounter.Flee()); // Lethal final counterattack.
                    break;
                case EncounterStatus.Escaped:
                    Assert.True(encounter.Flee());
                    break;
                default:
                    throw new InvalidOperationException("Unexpected terminal status.");
            }

            Assert.Equal(finalStatus, encounter.Status);
            int turnBefore = encounter.TurnCount;
            int cooldownBefore = knight.SkillCooldown;
            int playerHealthBefore = knight.Health;
            int enemyHealthBefore = enemy.Health;
            int experienceBefore = knight.Experience;
            int goldBefore = knight.Gold;
            int levelBefore = knight.Level;
            int hitsBefore = rng.HitRollCount;
            int damageRollsBefore = rng.DamageRollCount;

            Assert.False(encounter.Flee());

            Assert.Equal(finalStatus, encounter.Status);
            Assert.Equal(turnBefore, encounter.TurnCount);
            Assert.Equal(cooldownBefore, knight.SkillCooldown);
            Assert.Equal(playerHealthBefore, knight.Health);
            Assert.Equal(enemyHealthBefore, enemy.Health);
            Assert.Equal(experienceBefore, knight.Experience);
            Assert.Equal(goldBefore, knight.Gold);
            Assert.Equal(levelBefore, knight.Level);
            Assert.Equal(hitsBefore, rng.HitRollCount);
            Assert.Equal(damageRollsBefore, rng.DamageRollCount);
            AssertInventoryUnchanged(knight, inventoryBefore);
        });
    }

    [Fact]
    public void AfterEscape_AllCombatActionsAreRejectedAndLeaveStateUnchanged()
    {
        var rng = new OneAttackRandom(20);
        WithControlledRandom(rng, () =>
        {
            var knight = TestState.Player(new Knight("No More Turns"), cooldown: 2);
            knight.GainExperience(12);
            knight.GainGold(7);
            Item[] inventoryBefore = knight.Inventory.ToArray();
            var (encounter, enemy) = StartEncounter(knight);
            Assert.True(encounter.Flee());

            int turnBefore = encounter.TurnCount;
            int cooldownBefore = knight.SkillCooldown;
            int playerHealthBefore = knight.Health;
            int enemyHealthBefore = enemy.Health;
            int experienceBefore = knight.Experience;
            int goldBefore = knight.Gold;
            int levelBefore = knight.Level;

            Assert.False(encounter.Flee());
            Assert.False(encounter.Attack());
            Assert.False(encounter.Skill());
            Assert.False(encounter.UseItem(new HealthPotion()));

            Assert.Equal(EncounterStatus.Escaped, encounter.Status);
            Assert.Equal(turnBefore, encounter.TurnCount);
            Assert.Equal(cooldownBefore, knight.SkillCooldown);
            Assert.Equal(playerHealthBefore, knight.Health);
            Assert.Equal(enemyHealthBefore, enemy.Health);
            Assert.Equal(experienceBefore, knight.Experience);
            Assert.Equal(goldBefore, knight.Gold);
            Assert.Equal(levelBefore, knight.Level);
            Assert.Equal(1, rng.HitRollCount); // No additional enemy attacks.
            Assert.Equal(1, rng.DamageRollCount);
            AssertInventoryUnchanged(knight, inventoryBefore);
        });
    }
}
