using RPGSystem.Areas.AreaTypes;
using RPGSystem.Characters;
using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Players.PlayerClasses;
using RPGSystem.Coordinators;
using Xunit;

namespace RPGSystem.Test;

public class EncounterCoordinatorTest
{
    private static (EncounterCoordinator Encounter, Enemy Enemy) StartEncounter(
        Knight knight, int? enemyHealth = null)
    {
        var encounter = new EncounterCoordinator(knight);
        Assert.True(encounter.Start(new GreenFields()));

        Enemy enemy = TestState.Apply(Assert.IsAssignableFrom<Enemy>(encounter.Enemy));

        if (enemyHealth is int health)
        {
            // Current Health is separate from MaxHealth; set the cap first.
            TestState.Set(enemy, nameof(Character.MaxHealth), Math.Max(enemy.MaxHealth, health));
            TestState.Set(enemy, nameof(Character.Health), health);
        }

        return (encounter, enemy);
    }

    private static void WithControlledRandom(Random random, Action test)
    {
        TestState.WithRandom(random, test);
    }

    [Fact]
    public void Start_StartedActive_ReturnsFalse()
    {
        var encounter = new EncounterCoordinator(TestState.Player(new Knight("Sir Test")));
        var area = new GreenFields();

        Assert.True(encounter.Start(area));
        Enemy enemy = Assert.IsAssignableFrom<Enemy>(encounter.Enemy);

        Assert.False(encounter.Start(area));
        Assert.Same(enemy, encounter.Enemy);
        Assert.Same(area, encounter.Area);
        Assert.Equal(EncounterStatus.Active, encounter.Status);
    }

    [Fact]
    public void Attack_ValidAttack_IncreasesTurnCount()
    {
        var (encounter, _) = StartEncounter(TestState.Player(new Knight("Sir Test")));

        Assert.Equal(0, encounter.TurnCount);

        Assert.True(encounter.Attack());
        Assert.Equal(1, encounter.TurnCount);
    }

    [Fact]
    public void Attack_NotStarted_DoesNotIncreaseTurnCount()
    {
        var encounter = new EncounterCoordinator(TestState.Player(new Knight("Sir Test")));

        Assert.False(encounter.Attack());
        Assert.Equal(0, encounter.TurnCount);
        Assert.Equal(EncounterStatus.NotStarted, encounter.Status);
    }

    [Fact]
    public void Attack_FinishingAttack_PreservesTurnCount()
    {
        WithControlledRandom(new CriticalHitRandom(), () =>
        {
            var (encounter, enemy) = StartEncounter(TestState.Player(new Knight("Sir Test")), enemyHealth: 1);

            Assert.True(encounter.Attack());
            Assert.Equal(0, enemy.Health);
            Assert.Equal(EncounterStatus.Victory, encounter.Status);
            Assert.Equal(1, encounter.TurnCount);

            Assert.False(encounter.Attack());
            Assert.Equal(EncounterStatus.Victory, encounter.Status);
            Assert.Equal(1, encounter.TurnCount);
        });
    }

    [Fact]
    public void Attack_Victory_AwardsRewardsAndLevelsUp()
    {
        WithControlledRandom(new CriticalHitRandom(), () =>
        {
            var knight = TestState.Player(new Knight("Sir Test"));
            knight.GainExperience(75);
            knight.GainGold(20);

            var (encounter, enemy) = StartEncounter(knight, enemyHealth: 1);
            TestState.SetRewards(enemy, 50, 30);

            Assert.True(encounter.Attack());
            Assert.Equal(EncounterStatus.Victory, encounter.Status);
            Assert.Equal(25, knight.Experience);
            Assert.Equal(50, knight.Gold);
            Assert.Equal(2, knight.Level);

            Assert.False(encounter.Attack());
            Assert.Equal(25, knight.Experience);
            Assert.Equal(50, knight.Gold);
            Assert.Equal(2, knight.Level);
            Assert.Equal(EncounterStatus.Victory, encounter.Status);
        });
    }

    private sealed class PlayerMissEnemyCriticalRandom : Random
    {
        private int _hitCount;
        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                _hitCount++;
                if (_hitCount == 1) return 1;  // Player critical failure.
                if (_hitCount == 2) return 20; // Enemy lethal critical hit.
                throw new InvalidOperationException("Unexpected additional attack.");
            }
            return minValue;
        }
    }

    [Fact]
    public void Attack_Defeat_DoesNotAwardRewards()
    {
        WithControlledRandom(new PlayerMissEnemyCriticalRandom(), () =>
        {
            var knight = TestState.Player(new Knight("Sir Test"));
            knight.GainExperience(75);
            knight.GainGold(20);
            TestState.Set(knight, nameof(Character.Health), 1);

            var (encounter, enemy) = StartEncounter(knight);
            TestState.Set(enemy, nameof(Character.Attack), 20);
            int enemyHealthBefore = enemy.Health;

            Assert.True(encounter.Attack());
            Assert.Equal(0, knight.Health);
            Assert.Equal(enemyHealthBefore, enemy.Health);
            Assert.Equal(EncounterStatus.Defeat, encounter.Status);
            Assert.Equal(75, knight.Experience);
            Assert.Equal(20, knight.Gold);
            Assert.Equal(1, knight.Level);
        });
    }

    [Fact]
    public void Skill_Accepted_SetsCooldownToThree()
    {
        WithControlledRandom(new CriticalHitRandom(), () =>
        {
            var knight = TestState.Player(new Knight("Sir Test"));
            var (encounter, enemy) = StartEncounter(knight, enemyHealth: 100);

            Assert.Equal(0, knight.SkillCooldown);
            Assert.True(encounter.Skill());

            // Skill 15, minimum damage 12, critical hit x2 = 24.
            Assert.Equal(76, enemy.Health);
            Assert.Equal(3, knight.SkillCooldown);
            Assert.Equal(1, encounter.TurnCount);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
            Assert.False(encounter.Skill());
        });
    }

    [Fact]
    public void Attack_AfterSkill_ReducesCooldown()
    {
        WithControlledRandom(new CriticalHitRandom(), () =>
        {
            var knight = TestState.Player(new Knight("Sir Test"));
            var (encounter, enemy) = StartEncounter(knight, enemyHealth: 100);
            TestState.Set(enemy, nameof(Character.Attack), 3); // Harmless counterattacks keep focus on cooldown progression.

            Assert.True(encounter.Skill());
            Assert.Equal(3, knight.SkillCooldown);

            for (int expectedCooldown = 2; expectedCooldown >= 0; expectedCooldown--)
            {
                Assert.True(encounter.Attack());
                Assert.Equal(expectedCooldown, knight.SkillCooldown);
                Assert.Equal(EncounterStatus.Active, encounter.Status);
            }

            Assert.True(encounter.Attack());
            Assert.Equal(0, knight.SkillCooldown); // No negative cooldown.
            Assert.Equal(EncounterStatus.Active, encounter.Status);

            Assert.True(encounter.Skill());
            Assert.Equal(3, knight.SkillCooldown); // Ready to use again.
        });
    }

    [Fact]
    public void Skill_OnCooldown_DoesNotChangeEncounterState()
    {
        WithControlledRandom(new CriticalHitRandom(), () =>
        {
            var knight = TestState.Player(new Knight("Sir Test"));
            var (encounter, enemy) = StartEncounter(knight, enemyHealth: 100);

            Assert.True(encounter.Skill());
            Assert.Equal(3, knight.SkillCooldown);

            int turnCountBefore = encounter.TurnCount;
            int cooldownBefore = knight.SkillCooldown;
            int playerHealthBefore = knight.Health;
            int enemyHealthBefore = enemy.Health;

            Assert.False(encounter.Skill());

            Assert.Equal(turnCountBefore, encounter.TurnCount);
            Assert.Equal(cooldownBefore, knight.SkillCooldown);
            Assert.Equal(playerHealthBefore, knight.Health);
            Assert.Equal(enemyHealthBefore, enemy.Health);
            Assert.Equal(EncounterStatus.Active, encounter.Status);
        });
    }
}
