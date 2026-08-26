using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class CreateGameTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_open_in_the_lobby_when_a_game_is_created(int playerCount)
    {
        // Arrange
        IReadOnlyList<SeatSpec> seats = TestGame.Seats(playerCount, humans: 1);

        // Act
        GameState state = TestGame.Unwrap(Sut.CreateGame("game-1", "ROOM", seats, TestGame.DefaultSeed));

        // Assert
        Assert.Equal(GamePhase.Lobby, state.Phase);
        Assert.Equal(playerCount, state.PlayerCount);
        Assert.Null(state.Deal);
        Assert.Null(state.ActiveSeat);
        Assert.Equal(0, state.TurnNumber);
        Assert.Empty(state.History);
    }

    [Fact]
    public void Should_give_every_seat_all_three_abilities_when_a_game_is_created()
    {
        // Arrange
        // Act
        GameState state = TestGame.Lobby(5);

        // Assert
        Assert.All(state.Seats, seat => Assert.Equal(Abilities.All, seat.AbilitiesRemaining));
    }

    [Fact]
    public void Should_number_the_seats_in_order_when_a_game_is_created()
    {
        // Arrange
        // Act
        GameState state = TestGame.Lobby(5, humans: 2);

        // Assert
        Assert.Equal([0, 1, 2, 3, 4], state.Seats.Select(seat => seat.Index));
        Assert.Equal(2, state.HumanSeats.Count());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Should_reject_the_game_when_the_table_is_the_wrong_size(int playerCount)
    {
        // Arrange
        IReadOnlyList<SeatSpec> seats =
            [.. Enumerable.Range(0, playerCount).Select(index => new SeatSpec(SeatKind.Bot, $"Bot {index}"))];

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.CreateGame("game-1", "ROOM", seats, TestGame.DefaultSeed));

        // Assert
        Assert.Equal(GameErrorCode.UnsupportedPlayerCount, code);
    }
}
