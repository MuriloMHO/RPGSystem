using System.Reflection;
using RPGSystem.Characters;
using RPGSystem.Characters.Enemies;
using EnemyModel = RPGSystem.Characters.Enemies.Enemy;
using RPGSystem.Characters.Players;
using RPGSystem.Items;

namespace RPGSystem.Test;

/// <summary>Encapsulated test setup; gameplay setters remain nonpublic.</summary>
internal static class TestState
{
    private static readonly FieldInfo RngField = typeof(Character).GetField(
        "characterRNG", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(Character), "characterRNG");

    private static readonly MethodInfo AddItemMethod = typeof(Player).GetMethod(
        "AddToInventory", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(Player), "AddToInventory");

    internal static void WithRandom(Random random, Action test)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(test);

        // Do not silently try to overwrite a static readonly field on .NET 10.
        // Apply the one-word, nonpublic RNG testability patch in the README first.
        if (RngField.IsInitOnly)
            throw new InvalidOperationException(
                "Character.characterRNG is static readonly. Apply the documented " +
                "minimal testability patch before running deterministic tests.");

        Random previous = (Random)(RngField.GetValue(null)
            ?? throw new InvalidOperationException("Combat RNG was null."));
        try
        {
            RngField.SetValue(null, random);
            test();
        }
        finally
        {
            RngField.SetValue(null, previous);
        }
    }

    // RNG is shared/static in the current source, so it is replaced in
    // WithRandom, not on each Character instance.
    internal static T Apply<T>(T character) where T : Character => character;

    internal static T Player<T>(T player, int? health = null, int? cooldown = null)
        where T : Player
    {
        if (health.HasValue) Set(player, nameof(Character.Health), health.Value);
        if (cooldown.HasValue) Set(player, nameof(Character.SkillCooldown), cooldown.Value);
        return player;
    }

    internal static Enemy Enemy(int maxHealth = 100, int? health = null,
        int attack = 10, int armour = 10,
        int experienceReward = 50, int goldReward = 30)
    {
        var enemy = new Enemy();
        Set(enemy, nameof(Character.MaxHealth), maxHealth);
        Set(enemy, nameof(Character.Health), health ?? maxHealth);
        Set(enemy, nameof(Character.Attack), attack);
        Set(enemy, nameof(Character.Armour), armour);
        SetRewards(enemy, experienceReward, goldReward);
        return enemy;
    }

    // Test-only setup: Enemy reward setters are protected, not public.
    // PropertyInfo.SetValue allows us to configure either a concrete Enemy or
    // an area-spawned enemy without changing the production API.
    internal static void SetRewards(Enemy enemy, int experienceReward, int goldReward)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        PropertyInfo experienceProperty = typeof(Enemy).GetProperty(
            nameof(EnemyModel.ExperienceReward), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(nameof(Enemy), nameof(EnemyModel.ExperienceReward));
        PropertyInfo goldProperty = typeof(Enemy).GetProperty(
            nameof(EnemyModel.GoldReward), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(nameof(Enemy), nameof(EnemyModel.GoldReward));

        experienceProperty.SetValue(enemy, experienceReward);
        goldProperty.SetValue(enemy, goldReward);
    }

    internal static void Set(Character character, string property, int value)
    {
        PropertyInfo info = typeof(Character).GetProperty(property,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(nameof(Character), property);
        info.SetValue(character, value);
    }

    internal static void AddItem(Player player, Item item)
    {
        AddItemMethod.Invoke(player, new object?[] { item });
    }

    internal static void SetItemCostForSpoofTest(Item item, int cost)
    {
        FieldInfo field = typeof(Item).GetField("_cost",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(Item), "_cost");
        field.SetValue(item, cost);
    }
}
