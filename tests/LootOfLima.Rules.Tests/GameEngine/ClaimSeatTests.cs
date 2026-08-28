using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class ClaimSeatTests
{
    [Fact]
    public void Should_seat_the_player_when_the_seat_is_still_a_bot()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        GameState claimed = TestGame.Unwrap(Sut.ClaimSeat(state, 3, "player-abc", "Ada"));

        // Assert
        Assert.Equal(SeatKind.Human, claimed.SeatAt(3).Kind);
        Assert.Equal("Ada", claimed.SeatAt(3).DisplayName);
        Assert.Equal("player-abc", claimed.SeatAt(3).PlayerId);
    }

    [Fact]
    public void Should_leave_the_other_seats_alone_when_one_is_claimed()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        GameState claimed = TestGame.Unwrap(Sut.ClaimSeat(state, 3, "player-abc", "Ada"));

        // Assert
        Assert.Single(claimed.HumanSeats);
    }

    [Fact]
    public void Should_reject_the_claim_when_the_seat_is_already_taken()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 1);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, 0, "player-abc", "Ada"));

        // Assert
        Assert.Equal(GameErrorCode.SeatAlreadyClaimed, code);
    }

    [Fact]
    public void Should_name_the_occupant_and_no_seat_number_when_the_seat_is_already_taken()
    {
        // Arrange
        // Two people tapping the same card at once is ordinary, so this message is read by somebody
        // who has done nothing wrong -- and it is shown to them verbatim. `seatIndex` is the
        // engine's zero-based numbering, so "Seat 0" means the first card: telling a player that is
        // telling them something false in the only units they can check it in.
        GameState state = TestGame.Lobby(5, humans: 1);

        // Act
        Result<GameState> refused = Sut.ClaimSeat(state, 0, "player-abc", "Ada");

        // Assert
        Assert.NotNull(refused.Error);
        Assert.Equal("That seat has already been taken by Player 1.", refused.Error.Message);
    }

    [Fact]
    public void Should_say_how_to_move_and_name_no_credential_when_the_player_already_holds_a_seat()
    {
        // Arrange
        // A player id is a bearer credential nobody has ever seen, so putting one in front of a
        // player says nothing they can act on. What they can act on is leaving the seat they have.
        GameState state = TestGame.Lobby(5, humans: 0);
        state = TestGame.Unwrap(Sut.ClaimSeat(state, 0, "player-abc", "Ada"));

        // Act
        Result<GameState> refused = Sut.ClaimSeat(state, 1, "player-abc", "Ada");

        // Assert
        Assert.NotNull(refused.Error);
        Assert.DoesNotContain("player-abc", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(
            "You are already sitting at this table. Leave your seat before taking another.",
            refused.Error.Message);
    }

    [Fact]
    public void Should_reject_the_claim_when_the_player_already_holds_another_seat()
    {
        // Arrange
        // Two seats for one identity would mean two full-hand views of the same game for one
        // person, which is the hidden-information design undone by opening a second browser tab.
        GameState state = TestGame.Lobby(5, humans: 0);
        state = TestGame.Unwrap(Sut.ClaimSeat(state, 0, "player-abc", "Ada"));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, 1, "player-abc", "Ada again"));

        // Assert
        Assert.Equal(GameErrorCode.PlayerAlreadySeated, code);
    }

    [Fact]
    public void Should_seat_the_player_when_they_released_the_seat_they_held()
    {
        // Arrange
        // The guard is about seats held at once, not about a player ever having sat down.
        GameState state = TestGame.Lobby(5, humans: 0);
        state = TestGame.Unwrap(Sut.ClaimSeat(state, 0, "player-abc", "Ada"));
        state = TestGame.Unwrap(Sut.ReleaseSeat(state, 0));

        // Act
        GameState claimed = TestGame.Unwrap(Sut.ClaimSeat(state, 1, "player-abc", "Ada"));

        // Assert
        Assert.Equal("player-abc", claimed.SeatAt(1).PlayerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_throw_when_the_player_id_is_not_a_real_identity(string playerId)
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        // Assert
        Assert.Throws<ArgumentException>(() => Sut.ClaimSeat(state, 0, playerId, "Ada"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Should_reject_the_claim_when_there_is_no_such_seat(int seatIndex)
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, seatIndex, "player-abc", "Ada"));

        // Assert
        Assert.Equal(GameErrorCode.SeatOutOfRange, code);
    }

    [Fact]
    public void Should_reject_the_claim_when_the_game_has_already_been_dealt()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, 4, "player-abc", "Ada"));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
