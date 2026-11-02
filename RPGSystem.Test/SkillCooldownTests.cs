using RPGSystem.Areas.AreaTypes;
using RPGSystem.Characters;
using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players;
using RPGSystem.Characters.Players.PlayerClasses;
using RPGSystem.Coordinators;
using Xunit;

namespace RPGSystem.Test;

public class SkillCooldownTests
{
    // Used only by these tests. Keeps combat rolls deterministic and counts damage rolls.
    private sealed class ControlledCombatRandom(int hitRoll, int flameRoll = 2) : Random
    {
        public int DamageRollCount { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
                return hitRoll;

            if (minValue == 1 && maxValue == 3)
                return flameRoll;

            DamageRollCount++;
            return minValue;
        }
    }

    private static void WithControlledRandom(Random random, Action test)
    {
        TestState.WithRandom(random, test);
    }

    private static EncounterCoordinator StartEncounter(Player player, int enemyHealth = 100)
    {
        var encounter = new EncounterCoordinator(player);
        Assert.True(encounter.Start(new GreenFields()));
        Assert.NotNull(encounter.Enemy);
        TestState.Apply(encounter.Enemy);

        TestState.Set(encounter.Enemy, nameof(Character.MaxHealth), Math.Max(100, enemyHealth));
        TestState.Set(encounter.Enemy, nameof(Character.Health), enemyHealth);
        return encounter;
    }

    [Fact]
    public void ArcherSkill_CriticalHit_DoublesDamageCalculatedOnce()
    {
        var rng = new ControlledCombatRandom(hitRoll: 20);
        WithControlledRandom(rng, () =>
        {
            var archer = TestState.Player(new Archer("Test Archer"));
            var enemy = TestState.Enemy(maxHealth: 100, health: 100, armour: 10);

            int damage = archer.SkillAttack(enemy);

            // Skill 8 -> min roll 5 -> critical 10 -> Archer doubles to 20.
            Assert.Equal(20, damage);
            Assert.Equal(1, rng.DamageRollCount);
            Assert.Equal(3, archer.SkillCooldown);
        });
    }

    [Theory]
    [InlineData(1, 44)] // Fire triggers: Skill (18 + 7), min roll 22, critical x2.
    [InlineData(2, 30)] // No fire: Skill 18, min roll 15, critical x2.
    public void MageFireball_CriticalHit_UsesFlameRoll(int flameRoll, int expectedDamage)
    {
        var rng = new ControlledCombatRandom(hitRoll: 20, flameRoll: flameRoll);
        WithControlledRandom(rng, () =>
        {
            var mage = TestState.Player(new Mage("Test Mage"));
            var enemy = TestState.Enemy(maxHealth: 100, health: 100, armour: 10);

            int damage = mage.SkillAttack(enemy);

            Assert.Equal(expectedDamage, damage);
            Assert.Equal(1, rng.DamageRollCount);
            Assert.Equal(3, mage.SkillCooldown);
        });
    }

    [Fact]
    public void MageSkill_OrdinaryMiss_DoesNoDamageButActivatesCooldown()
    {
        WithControlledRandom(new ControlledCombatRandom(hitRoll: 2, flameRoll: 1), () =>
        {
            var mage = TestState.Player(new Mage("Test Mage"));
            var encounter = StartEncounter(mage);
            Assert.NotNull(encounter.Enemy);

            // Make any enemy counterattack harmless; keep the encounter active.
            TestState.Set(encounter.Enemy, nameof(Character.Attack), 3);

            Assert.True(encounter.Skill());

            Assert.Equal(100, encounter.Enemy.Health);
            Assert.Equal(80, mage.Health);
            Assert.Equal(3, mage.SkillCooldown);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
        });
    }

    [Theory]
    [InlineData(1, 69)] // Fire bonus: 22 / 2 = 11 self-damage.
    [InlineData(2, 73)] // No fire: 15 / 2 = 7 self-damage (integer division).
    public void MageSkill_CriticalFailure_SelfDamageIncludesFireBonus(
        int flameRoll, int expectedHealth)
    {
        WithControlledRandom(new ControlledCombatRandom(hitRoll: 1, flameRoll: flameRoll), () =>
        {
            var mage = TestState.Player(new Mage("Test Mage"));
            var enemy = TestState.Enemy(maxHealth: 100, health: 100, armour: 10);

            int damageToEnemy = mage.SkillAttack(enemy);

            Assert.Equal(0, damageToEnemy);
            Assert.Equal(expectedHealth, mage.Health);
            Assert.Equal(3, mage.SkillCooldown);
        });
    }

    [Fact]
    public void NormalAttack_FinishingEnemy_StillReducesSkillCooldown()
    {
        WithControlledRandom(new ControlledCombatRandom(hitRoll: 20), () =>
        {
            var knight = TestState.Player(new Knight("Test Knight"));
            var encounter = StartEncounter(knight);
            Assert.NotNull(encounter.Enemy);
            TestState.Set(encounter.Enemy, nameof(Character.Attack), 3);

            Assert.True(encounter.Skill());
            Assert.Equal(3, knight.SkillCooldown);
            Assert.Equal(EncounterStatus.Active, encounter.Status);

            TestState.Set(encounter.Enemy, nameof(Character.Health), 1);
            Assert.True(encounter.Attack());

            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(2, encounter.TurnCount);
            Assert.Equal(EncounterStatus.Victory, encounter.Status);

            Assert.False(encounter.Attack());
            Assert.Equal(2, knight.SkillCooldown);
            Assert.Equal(2, encounter.TurnCount);
        });
    }

    [Fact]
    public void Skill_Victory_AwardsRewardsOnlyOnce_AndRejectsLaterSkills()
    {
        WithControlledRandom(new ControlledCombatRandom(hitRoll: 20), () =>
        {
            var knight = TestState.Player(new Knight("Test Knight"));
            knight.GainExperience(75);
            knight.GainGold(20);

            var encounter = StartEncounter(knight, enemyHealth: 1);
            Assert.NotNull(encounter.Enemy);
            TestState.SetRewards(encounter.Enemy, 50, 30);

            Assert.True(encounter.Skill());

            Assert.Equal(EncounterStatus.Victory, encounter.Status);
            Assert.Equal(0, encounter.Enemy.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(3, knight.SkillCooldown);
            Assert.Equal(25, knight.Experience);
            Assert.Equal(50, knight.Gold);
            Assert.Equal(2, knight.Level);

            Assert.False(encounter.Skill());
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(3, knight.SkillCooldown);
            Assert.Equal(25, knight.Experience);
            Assert.Equal(50, knight.Gold);
            Assert.Equal(2, knight.Level);
            Assert.Equal(EncounterStatus.Victory, encounter.Status);
        });
    }

    [Fact]
    public void Skill_CriticalFailure_DefeatsPlayerWithoutRewards_AndRejectsLaterSkills()
    {
        WithControlledRandom(new ControlledCombatRandom(hitRoll: 1, flameRoll: 1), () =>
        {
            var mage = TestState.Player(new Mage("Test Mage"), health: 1);
            var encounter = StartEncounter(mage);
            Assert.NotNull(encounter.Enemy);
            TestState.SetRewards(encounter.Enemy, 50, 30);

            Assert.True(encounter.Skill());

            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Assert.Equal(0, mage.Health);
            Assert.Equal(100, encounter.Enemy.Health);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(3, mage.SkillCooldown);
            Assert.Equal(0, mage.Experience);
            Assert.Equal(0, mage.Gold);

            Assert.False(encounter.Skill());
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(3, mage.SkillCooldown);
            Assert.Equal(0, mage.Experience);
            Assert.Equal(0, mage.Gold);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
        });
    }
}
