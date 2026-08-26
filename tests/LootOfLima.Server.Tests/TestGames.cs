using LootOfLima.Rules;

namespace LootOfLima.Server.Tests;

/// <summary>Games at a known starting point, so a store test spends its lines on the store.</summary>
internal static class TestGames
{
    public const int DefaultSeed = 20260826;

    /// <summary>A five-seat lobby, one human and the rest bots.</summary>
    public static GameState Lobby(string gameId = "game-1", string roomCode = "ABCD", int playerCount = 5)
    {
        IReadOnlyList<SeatSpec> seats =
        [
            .. Enumerable.Range(0, playerCount).Select(index => index == 0
                ? new SeatSpec(SeatKind.Human, "Ada", "player-1")
                : new SeatSpec(SeatKind.Bot, $"Bot {index + 1}")),
        ];

        Result<GameState> created = GameEngine.CreateGame(gameId, roomCode, seats, DefaultSeed);
        Assert.Null(created.Error);
        return created.Value;
    }
}
