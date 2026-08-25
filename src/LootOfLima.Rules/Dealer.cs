namespace LootOfLima.Rules;

/// <summary>Shuffles the island and deals it. The app always owns the deal; nobody types a hand in.</summary>
public static class Dealer
{
    /// <summary>
    /// The rulebook setup, in order: shuffle all 24 locations, set <see cref="DealTable.TreasureSize"/>
    /// aside unseen, deal the rest evenly, and flip whatever is left face up.
    /// </summary>
    /// <param name="seed">Recorded on the deal so the same deal can be reproduced later.</param>
    public static Deal Create(int playerCount, IShuffleSource shuffle, int seed)
    {
        ArgumentNullException.ThrowIfNull(shuffle);

        if (!DealTable.IsSupportedPlayerCount(playerCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerCount),
                playerCount,
                $"Loot of Lima is for {DealTable.MinPlayers}-{DealTable.MaxPlayers} players.");
        }

        List<LocationId> pile = [.. LocationId.All];
        shuffle.Shuffle(pile);

        int position = 0;
        List<LocationId> treasure = [.. pile.Skip(position).Take(DealTable.TreasureSize)];
        position += DealTable.TreasureSize;

        int handSize = DealTable.HandSize(playerCount);
        List<IReadOnlyList<LocationId>> hands = [];
        for (int seat = 0; seat < playerCount; seat++)
        {
            hands.Add([.. pile.Skip(position).Take(handSize)]);
            position += handSize;
        }

        List<LocationId> reveal = [.. pile.Skip(position)];

        return Deal.Create(hands, reveal, treasure, DealSource.Generated(seed));
    }

    /// <summary>Deals from a freshly seeded generator, so the seed alone reproduces the deal.</summary>
    public static Deal Create(int playerCount, int seed) =>
        Create(playerCount, new DeterministicShuffleSource(seed), seed);
}
