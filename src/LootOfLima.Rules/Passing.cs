namespace LootOfLima.Rules;

/// <summary>
/// Setup token passing. Hand sizes never change; what changes is that each endpoint of a pass gains
/// private knowledge of a location a specific other player holds.
/// </summary>
public static class Passing
{
    /// <summary>
    /// Three-player games do not pass at all. Neither do official scenarios: the printed books
    /// replace passing with the "fewest terrain" hint, and their published answers describe the
    /// hands exactly as dealt, so passing tokens around would contradict the book.
    /// </summary>
    public static bool IsRequired(int playerCount, DealSourceKind source) =>
        playerCount >= 4 && source == DealSourceKind.Generated;

    /// <summary>
    /// Chooses which tokens travel. Four players pass one token straight across the table; five
    /// pass one to the right and then a second, different token to the left.
    /// </summary>
    /// <remarks>
    /// The five-player second pass deliberately never re-passes the token just received. The
    /// rulebook says to look at <em>another</em> of your tokens, and it is also what keeps the
    /// knowledge honest: the token you passed right is still in your right-hand neighbour's hand
    /// when the game starts.
    /// </remarks>
    public static IReadOnlyList<PassRecord> Plan(
        IReadOnlyList<IReadOnlyList<LocationId>> hands,
        IShuffleSource shuffle)
    {
        ArgumentNullException.ThrowIfNull(hands);
        ArgumentNullException.ThrowIfNull(shuffle);

        int seats = hands.Count;
        List<PassRecord> passes = [];

        if (seats == 4)
        {
            for (int seat = 0; seat < seats; seat++)
            {
                List<LocationId> candidates = [.. hands[seat]];
                passes.Add(new PassRecord(seat, (seat + 2) % seats, shuffle.Take(candidates), PassDirection.Across));
            }

            return passes;
        }

        if (seats != 5)
        {
            return passes;
        }

        List<List<LocationId>> remaining = [.. hands.Select(hand => new List<LocationId>(hand))];

        for (int seat = 0; seat < seats; seat++)
        {
            LocationId token = shuffle.Take(remaining[seat]);
            passes.Add(new PassRecord(seat, (seat + 1) % seats, token, PassDirection.Right));
        }

        for (int seat = 0; seat < seats; seat++)
        {
            LocationId token = shuffle.Take(remaining[seat]);
            passes.Add(new PassRecord(seat, (seat + seats - 1) % seats, token, PassDirection.Left));
        }

        return passes;
    }

    /// <summary>
    /// Moves the planned tokens. Applied in plan order, which is every right-pass before every
    /// left-pass, so a token is never moved twice.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<LocationId>> Apply(
        IReadOnlyList<IReadOnlyList<LocationId>> hands,
        IReadOnlyList<PassRecord> passes)
    {
        ArgumentNullException.ThrowIfNull(hands);
        ArgumentNullException.ThrowIfNull(passes);

        List<List<LocationId>> result = [.. hands.Select(hand => new List<LocationId>(hand))];

        foreach (PassRecord pass in passes)
        {
            if (!result[pass.GiverSeat].Remove(pass.Location))
            {
                throw new ArgumentException(
                    $"Seat {pass.GiverSeat} cannot pass {pass.Location}, which it does not hold.",
                    nameof(passes));
            }

            result[pass.ReceiverSeat].Add(pass.Location);
        }

        return [.. result.Select(hand => (IReadOnlyList<LocationId>)[.. hand.Order()])];
    }
}
