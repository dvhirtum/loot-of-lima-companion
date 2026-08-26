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
        DealSource source,
        IReadOnlyList<Terrain?>? prescribedFewestTerrain)
    {
        Hands = hands;
        PublicReveal = publicReveal;
        Treasure = treasure;
        Source = source;
        PrescribedFewestTerrain = prescribedFewestTerrain;
    }

    /// <summary>One hand per seat, in seat order, each sorted by location index.</summary>
    public IReadOnlyList<IReadOnlyList<LocationId>> Hands { get; }

    /// <summary>The leftovers, flipped face up at setup. Public to everybody, all game.</summary>
    public IReadOnlyList<LocationId> PublicReveal { get; }

    /// <summary>The two hidden locations. Never projected to anyone before the game is over.</summary>
    public IReadOnlyList<LocationId> Treasure { get; }

    public DealSource Source { get; }

    /// <summary>
    /// What each seat announces at setup, when the deal already settles it. Null for a generated
    /// deal, where the players do the announcing themselves.
    /// </summary>
    /// <remarks>
    /// The printed scenarios make this choice on the players' behalf, and it genuinely cannot be
    /// recomputed: over half of the books' hands hold two terrains tied at the minimum, and the
    /// rulebook leaves that choice to the player. Recomputing would silently disagree with the
    /// booklet the player is holding.
    /// </remarks>
    public IReadOnlyList<Terrain?>? PrescribedFewestTerrain { get; }

    public int PlayerCount => Hands.Count;

    /// <summary>
    /// Validates and normalises a deal. Every one of the 24 locations must appear exactly once
    /// across the hands, the reveal and the treasure — the deal is a partition of the island.
    /// </summary>
    /// <param name="prescribedFewestTerrain">
    /// One entry per seat when the deal settles the setup announcement itself, as the printed
    /// scenarios do. Each non-null entry must be a terrain that seat truthfully holds fewest of.
    /// </param>
    public static Deal Create(
        IReadOnlyList<IReadOnlyList<LocationId>> hands,
        IReadOnlyList<LocationId> publicReveal,
        IReadOnlyList<LocationId> treasure,
        DealSource source,
        IReadOnlyList<Terrain?>? prescribedFewestTerrain = null)
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

        if (prescribedFewestTerrain is not null)
        {
            // Checked before truthfulness, because on a generated deal truthfulness now is no
            // evidence of truthfulness later: those hands still pass tokens before anybody
            // announces, so an announcement fixed at deal time could be a lie by the time it is
            // made. Only the books, which never pass, can settle an announcement this early.
            if (source.Kind != DealSourceKind.Scenario)
            {
                throw new ArgumentException(
                    "Only a scenario deal prescribes the setup announcements; "
                    + $"this deal is {source.Kind}.",
                    nameof(prescribedFewestTerrain));
            }

            if (prescribedFewestTerrain.Count != playerCount)
            {
                throw new ArgumentException(
                    $"The deal prescribes {prescribedFewestTerrain.Count} announcements for {playerCount} seats.",
                    nameof(prescribedFewestTerrain));
            }

            for (int seat = 0; seat < playerCount; seat++)
            {
                if (prescribedFewestTerrain[seat] is { } announced
                    && !FewestTerrainRule.IsTruthful(hands[seat], announced))
                {
                    throw new ArgumentException(
                        $"Seat {seat} is prescribed to announce {announced}, which is not a terrain it holds "
                        + "fewest of.",
                        nameof(prescribedFewestTerrain));
                }
            }
        }

        return new Deal(
            hands: [.. hands.Select(hand => (IReadOnlyList<LocationId>)[.. hand.Order()])],
            publicReveal: [.. publicReveal.Order()],
            treasure: [.. treasure.Order()],
            source: source,
            prescribedFewestTerrain: prescribedFewestTerrain is null ? null : [.. prescribedFewestTerrain]);
    }
}
