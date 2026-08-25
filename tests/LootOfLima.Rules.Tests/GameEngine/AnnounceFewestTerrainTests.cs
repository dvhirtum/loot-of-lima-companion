using Rule = LootOfLima.Rules.FewestTerrainRule;
using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class AnnounceFewestTerrainTests
{
    /// <summary>A generated five-player game, dealt and passed, waiting on its announcements.</summary>
    private static GameState Announcing(int humans = 3)
    {
        GameState state = TestGame.Dealing(5, humans);

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, seat.Index));
        }

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = TestGame.Unwrap(Sut.ConfirmPassDone(state, seat.Index));
        }

        return state;
    }

    private static Terrain Truthful(GameState state, int seatIndex) =>
        Rule.Minima(state.SeatAt(seatIndex).Hand)[0];

    [Fact]
    public void Should_record_the_announcement_when_a_seat_announces()
    {
        // Arrange
        GameState state = Announcing();
        Terrain terrain = Truthful(state, 0);

        // Act
        GameState announced = TestGame.Unwrap(Sut.AnnounceFewestTerrain(state, 0, terrain));

        // Assert
        Assert.Equal(terrain, announced.SeatAt(0).FewestTerrain);
        FewestTerrainAnnouncedEntry entry = announced.History.OfType<FewestTerrainAnnouncedEntry>().First();
        Assert.Equal(0, entry.SeatIndex);
        Assert.Equal(terrain, entry.Terrain);
    }

    [Fact]
    public void Should_let_the_player_choose_when_two_terrains_tie()
    {
        // Arrange
        // "If there is a tie, the player chooses which type to announce." Both are legal, so the
        // engine must accept either and never substitute its own preference.
        GameState state = Announcing();
        int seat = Enumerable.Range(0, 5).First(index =>
            state.SeatAt(index).IsHuman && Rule.Minima(state.SeatAt(index).Hand).Count > 1);
        state = AnnounceUpTo(state, seat);
        IReadOnlyList<Terrain> tied = Rule.Minima(state.SeatAt(seat).Hand);

        // Act
        GameState first = TestGame.Unwrap(Sut.AnnounceFewestTerrain(state, seat, tied[0]));
        GameState second = TestGame.Unwrap(Sut.AnnounceFewestTerrain(state, seat, tied[1]));

        // Assert
        Assert.Equal(tied[0], first.SeatAt(seat).FewestTerrain);
        Assert.Equal(tied[1], second.SeatAt(seat).FewestTerrain);
    }

    [Fact]
    public void Should_reject_the_announcement_when_the_seat_holds_more_of_that_terrain()
    {
        // Arrange
        // The app answers for its players, so it will not let one announce something untrue.
        GameState state = Announcing();
        Terrain untruthful = Terrains.All.First(terrain => !Rule.IsTruthful(state.SeatAt(0).Hand, terrain));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.AnnounceFewestTerrain(state, 0, untruthful));

        // Assert
        Assert.Equal(GameErrorCode.NotAFewestTerrain, code);
    }

    [Fact]
    public void Should_announce_in_seat_order_when_the_phase_runs()
    {
        // Arrange
        // The rulebook says "in turn order", and it matters: a player breaking a tie has heard
        // everybody before them, so the order can change what they choose.
        GameState state = Announcing();

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.AnnounceFewestTerrain(state, 1, Truthful(state, 1)));

        // Assert
        Assert.Equal(0, state.ActiveSeat);
        Assert.Equal(GameErrorCode.NotYourTurn, code);
    }

    [Fact]
    public void Should_move_on_to_the_next_human_when_a_seat_has_announced()
    {
        // Arrange
        GameState state = Announcing();

        // Act
        GameState announced = TestGame.Unwrap(Sut.AnnounceFewestTerrain(state, 0, Truthful(state, 0)));

        // Assert
        Assert.Equal(1, announced.ActiveSeat);
        Assert.Equal(GamePhase.Announcing, announced.Phase);
    }

    [Fact]
    public void Should_announce_for_the_bots_when_the_phase_reaches_them()
    {
        // Arrange
        // Bots have no screen, so the engine announces on their behalf.
        GameState state = Announcing();

        // Act
        state = AnnounceUpTo(state, 5);

        // Assert
        Assert.All(state.Seats, seat => Assert.NotNull(seat.FewestTerrain));
        Assert.All(
            state.Seats,
            seat => Assert.True(Rule.IsTruthful(seat.Hand, seat.FewestTerrain!.Value)));
    }

    [Fact]
    public void Should_start_play_when_the_last_seat_has_announced()
    {
        // Arrange
        GameState state = Announcing();

        // Act
        state = AnnounceUpTo(state, 5);

        // Assert
        Assert.Equal(GamePhase.InProgress, state.Phase);
        Assert.Equal(1, state.TurnNumber);
        Assert.Equal(0, state.ActiveSeat);
    }

    [Fact]
    public void Should_announce_at_a_three_player_table_too_when_there_was_no_passing()
    {
        // Arrange
        // The rule is general: passing is skipped at three players, announcing is not.
        GameState state = TestGame.Dealing(3, humans: 2);
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 1));

        // Act
        // Assert
        Assert.Equal(GamePhase.Announcing, state.Phase);
        Assert.Equal(0, state.ActiveSeat);
    }

    [Fact]
    public void Should_break_a_bot_tie_without_always_choosing_the_same_terrain()
    {
        // Arrange
        // A fixed tie-break would leak: announcing "the first tied terrain in declaration order"
        // tells everybody the terrains before it are strictly more common, which is more than the
        // rule reveals. Across many seeds, a bot's tied choice must land on both terrains.
        HashSet<Terrain> chosen = [];

        for (int seed = 1; seed <= 60; seed++)
        {
            GameState state = TestGame.InProgress(5, humans: 1, seed: seed);
            foreach (Seat seat in state.Seats.Where(s => !s.IsHuman && Rule.Minima(s.Hand).Count > 1))
            {
                // Act
                chosen.Add(seat.FewestTerrain!.Value);
            }
        }

        // Assert
        Assert.True(chosen.Count > 1, $"Bots only ever announced {string.Join(", ", chosen)} on a tie.");
    }

    [Fact]
    public void Should_reject_the_announcement_when_the_game_is_not_announcing()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.AnnounceFewestTerrain(state, 0, Terrain.Beach));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }

    /// <summary>Announces truthfully for every human seat before <paramref name="seatIndex"/>.</summary>
    private static GameState AnnounceUpTo(GameState state, int seatIndex)
    {
        while (state.Phase == GamePhase.Announcing
            && state.ActiveSeat is { } active
            && active < seatIndex)
        {
            state = TestGame.Unwrap(Sut.AnnounceFewestTerrain(state, active, Truthful(state, active)));
        }

        return state;
    }
}
