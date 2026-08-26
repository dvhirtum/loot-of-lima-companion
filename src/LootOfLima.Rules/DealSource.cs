using System.Globalization;

namespace LootOfLima.Rules;

public enum DealSourceKind
{
    /// <summary>Shuffled by the app from a seed.</summary>
    Generated = 0,

    /// <summary>One of the official printed scenarios.</summary>
    Scenario = 1,
}

/// <summary>Where a deal came from, and enough to reproduce it.</summary>
/// <param name="Kind">Generated or taken from a book.</param>
/// <param name="Reference">The seed, or the scenario id.</param>
public sealed record DealSource(DealSourceKind Kind, string Reference)
{
    public static DealSource Generated(int seed) =>
        new(DealSourceKind.Generated, seed.ToString(CultureInfo.InvariantCulture));

    public static DealSource Scenario(string scenarioId) => new(DealSourceKind.Scenario, scenarioId);

    public override string ToString() => $"{Kind} ({Reference})";
}
