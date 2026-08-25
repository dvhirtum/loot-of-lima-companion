namespace LootOfLima.Rules;

/// <summary>
/// A validated setup: who holds what, what is face up, and the two locations nobody has seen.
/// Constructing one is the only way into the engine, so an illegal deal cannot exist.
/// </summary>
public sealed record Deal
{
    private Deal(
        IReadOnlyList<IReadOnlyList<LocationId>> hands,
        IReadOnlyList<LocationId> publicReveal,
        IReadOnlyList<LocationId> treasure,
        DealSource source)
    {
        Hands = hands;
        PublicReveal = publicReveal;
        Treasure = treasure;
        Source = source;
    }

    /// <summary>One hand per seat, in seat order, each sorted by location index.</summary>
    public IReadOnlyList<IReadOnlyList<LocationId>> Hands { get; }

    /// <summary>The leftovers, flipped face up at setup. Public to everybody, all game.</summary>
    public IReadOnlyList<LocationId> PublicReveal { get; }

    /// <summary>The two hidden locations. Never projected to anyone before the game is over.</summary>
    public IReadOnlyList<LocationId> Treasure { get; }

    public DealSource Source { get; }

    public int PlayerCount => Hands.Count;

    /// <summary>
    /// Validates and normalises a deal. Every one of the 24 locations must appear exactly once
    /// across the hands, the reveal and the treasure — the deal is a partition of the island.
    /// </summary>
    public static Deal Create(
        IReadOnlyList<IReadOnlyList<LocationId>> hands,
        IReadOnlyList<LocationId> publicReveal,
        IReadOnlyList<LocationId> treasure,
        DealSource source)
    {
        ArgumentNullException.ThrowIfNull(hands);
        ArgumentNullException.ThrowIfNull(publicReveal);
        ArgumentNullException.ThrowIfNull(treasure);
        ArgumentNullException.ThrowIfNull(source);

        int playerCount = hands.Count;
        if (!DealTable.IsSupportedPlayerCount(playerCount))
        {
            throw new ArgumentException(
                $"A deal must be for {DealTable.MinPlayers}-{DealTable.MaxPlayers} players, not {playerCount}.",
                nameof(hands));
        }

        int handSize = DealTable.HandSize(playerCount);
        for (int seat = 0; seat < playerCount; seat++)
        {
            if (hands[seat].Count != handSize)
            {
                throw new ArgumentException(
                    $"Seat {seat} holds {hands[seat].Count} locations but a {playerCount}-player hand is {handSize}.",
                    nameof(hands));
            }
        }

        int revealCount = DealTable.RevealCount(playerCount);
        if (publicReveal.Count != revealCount)
        {
            throw new ArgumentException(
                $"A {playerCount}-player deal reveals {revealCount} locations, not {publicReveal.Count}.",
                nameof(publicReveal));
        }

        if (treasure.Count != DealTable.TreasureSize)
        {
            throw new ArgumentException(
                $"The treasure is {DealTable.TreasureSize} locations, not {treasure.Count}.",
                nameof(treasure));
        }

        bool[] seen = new bool[LocationId.TotalCount];
        foreach (LocationId location in hands.SelectMany(hand => hand).Concat(publicReveal).Concat(treasure))
        {
            if (!location.IsValid)
            {
                throw new ArgumentException($"{location} is not one of the 24 island locations.", nameof(hands));
            }

            if (seen[location.Index])
            {
                throw new ArgumentException($"{location} is dealt more than once.", nameof(hands));
            }

            seen[location.Index] = true;
        }

        for (int index = 0; index < LocationId.TotalCount; index++)
        {
            if (!seen[index])
            {
                throw new ArgumentException(
                    $"{LocationId.FromIndex(index)} is not dealt to anybody, revealed, or set aside.",
                    nameof(hands));
            }
        }

        return new Deal(
            hands: [.. hands.Select(hand => (IReadOnlyList<LocationId>)[.. hand.Order()])],
            publicReveal: [.. publicReveal.Order()],
            treasure: [.. treasure.Order()],
            source: source);
    }
}
