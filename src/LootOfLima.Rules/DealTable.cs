namespace LootOfLima.Rules;

/// <summary>The setup table from the rulebook, held in one place so nothing can disagree with it.</summary>
public static class DealTable
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 5;

    /// <summary>Locations set aside unseen at setup. These are what the game is played to find.</summary>
    public const int TreasureSize = 2;

    public static bool IsSupportedPlayerCount(int playerCount) =>
        playerCount is >= MinPlayers and <= MaxPlayers;

    /// <summary>Tokens per hand: 7 at three players, 5 at four, 4 at five.</summary>
    public static int HandSize(int playerCount) => playerCount switch
    {
        3 => 7,
        4 => 5,
        5 => 4,
        _ => throw new ArgumentOutOfRangeException(
            nameof(playerCount), playerCount, $"Loot of Lima is for {MinPlayers}-{MaxPlayers} players."),
    };

    /// <summary>
    /// What is left over after the treasure and the hands, and is therefore flipped face up.
    /// Derived rather than tabulated so it cannot drift from <see cref="HandSize"/>: 1 at three
    /// players, 2 at four and five.
    /// </summary>
    public static int RevealCount(int playerCount) =>
        LocationId.TotalCount - TreasureSize - (playerCount * HandSize(playerCount));
}
