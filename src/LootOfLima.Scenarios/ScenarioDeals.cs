using LootOfLima.Rules;

namespace LootOfLima.Scenarios;

/// <summary>Turns a printed scenario into a deal the engine can start a game from.</summary>
public static class ScenarioDeals
{
    /// <summary>
    /// Builds the deal. It is validated exactly like a generated one, so a scenario cannot enter
    /// the engine by a side door.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resulting deal is marked <see cref="DealSourceKind.Scenario"/>, which is what suppresses
    /// setup token passing: the books have no passing, and their published answers describe the
    /// hands precisely as dealt.
    /// </para>
    /// <para>
    /// The book's printed "fewest terrain" letters become the setup announcements. They cannot be
    /// recomputed — most hands hold two terrains tied at the minimum, and the rulebook leaves that
    /// choice to the player, so the book has already made it on their behalf.
    /// </para>
    /// </remarks>
    public static Deal ToDeal(this Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return Deal.Create(
            hands: scenario.Hands,
            publicReveal: scenario.PublicReveal,
            treasure: scenario.Treasure,
            source: DealSource.Scenario(scenario.Id),
            prescribedFewestTerrain: scenario.FewestTerrain);
    }
}
