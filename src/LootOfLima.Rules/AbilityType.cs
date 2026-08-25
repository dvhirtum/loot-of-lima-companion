namespace LootOfLima.Rules;

/// <summary>The three ability tokens every player starts with, each spendable once per game.</summary>
/// <remarks>
/// Only <see cref="Pistol"/> has in-app mechanics. The app deliberately does not model the dice
/// roll, so <see cref="Barrel"/> (re-roll) and <see cref="Shovel"/> (treat a die as another
/// terrain) act purely on the physical dice; the engine only records that they were spent.
/// </remarks>
public enum AbilityType
{
    Barrel = 0,
    Shovel = 1,
    Pistol = 2,
}

public static class Abilities
{
    /// <summary>The full set every seat starts the game holding.</summary>
    public static readonly IReadOnlyList<AbilityType> All =
        [AbilityType.Barrel, AbilityType.Shovel, AbilityType.Pistol];
}
