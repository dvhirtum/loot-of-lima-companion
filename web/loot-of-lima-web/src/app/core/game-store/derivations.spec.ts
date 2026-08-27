import { describe, expect, it } from 'vitest';
import { GameStore } from '../game-store';
import { accusation, publicQuestion, seat, view, viewFor } from '../../testing/views';

/**
 * The computed read surface.
 *
 * Not one method, so not one file per method: these are the slices components read, and they are
 * covered together because they are one claim -- that the store derives nothing the server did not
 * already say. Every one of them is a lookup or a sort. If one of them ever needs a rule to
 * compute, that is the moment this client has started keeping a second copy of the game.
 */
describe('GameStore derivations', () => {
  it('Should_find_the_seat_when_the_view_names_a_viewer', () => {
    // Arrange
    const store = new GameStore();

    // Act
    store.apply(viewFor(1, ['B1', 'F2']));

    // Assert
    expect(store.mySeat()?.index).toBe(1);
    expect(store.myHand()).toEqual(['B1', 'F2']);
    expect(store.isSpectator()).toBe(false);
  });

  it('Should_be_a_spectator_when_the_view_names_no_viewer', () => {
    // Arrange
    // The host screen in the middle of the table. It is projected as nobody on purpose.
    const store = new GameStore();

    // Act
    store.apply(view({ viewerSeat: null, seats: [seat(0), seat(1)] }));

    // Assert
    expect(store.mySeat()).toBeNull();
    expect(store.myHand()).toEqual([]);
    expect(store.isSpectator()).toBe(true);
  });

  it('Should_be_my_turn_when_my_own_seat_is_the_active_one', () => {
    // Arrange
    // Read off the seat rather than compared against `activeSeat` by hand, because the server has
    // already decided it and two ways of asking is one way to disagree.
    const store = new GameStore();

    // Act
    store.apply(
      view({
        viewerSeat: 0,
        activeSeat: 0,
        seats: [seat(0, { isViewer: true, isActive: true }), seat(1)],
      }),
    );

    // Assert
    expect(store.isMyTurn()).toBe(true);
  });

  it('Should_not_be_my_turn_when_somebody_else_is_active', () => {
    // Arrange
    const store = new GameStore();

    // Act
    store.apply(
      view({
        viewerSeat: 0,
        activeSeat: 1,
        seats: [seat(0, { isViewer: true }), seat(1, { isActive: true })],
      }),
    );

    // Assert
    expect(store.isMyTurn()).toBe(false);
  });

  it('Should_read_newest_first_when_history_arrives', () => {
    // Arrange
    // The server sends history in the order it happened; a table reads a log the other way round.
    const store = new GameStore();

    // Act
    store.apply(view({ history: [publicQuestion(1), publicQuestion(2), publicQuestion(3)] }));

    // Assert
    expect(store.history().map((entry) => entry.sequence)).toEqual([3, 2, 1]);
  });

  it('Should_leave_the_arriving_view_alone_when_history_is_sorted', () => {
    // Arrange
    // The sort must not be in place. The view is the frame the connection handed over, and
    // reordering it would mutate something a caller is entitled to treat as what arrived.
    const store = new GameStore();
    const arriving = view({ history: [publicQuestion(1), publicQuestion(2)] });

    // Act
    store.apply(arriving);
    store.history();

    // Assert
    expect(arriving.history.map((entry) => entry.sequence)).toEqual([1, 2]);
  });

  it('Should_have_no_treasure_when_the_game_is_still_running', () => {
    // Arrange
    // The field the whole hidden-information design exists to protect. Null in every phase but
    // Finished, and the client has no way to fill it in even if it wanted to.
    const store = new GameStore();

    // Act
    store.apply(view({ phase: 'InProgress', treasure: null }));

    // Assert
    expect(store.treasure()).toBeNull();
    expect(store.isFinished()).toBe(false);
  });

  it('Should_have_the_treasure_when_the_game_is_finished', () => {
    // Arrange
    const store = new GameStore();

    // Act
    store.apply(view({ phase: 'Finished', treasure: ['B1', 'M4'], winnerSeat: 0 }));

    // Assert
    expect(store.treasure()).toEqual(['B1', 'M4']);
    expect(store.isFinished()).toBe(true);
  });

  it('Should_be_solo_when_one_seat_of_five_is_human', () => {
    // Arrange
    // Counted from a field every viewer is sent in full. Who is a bot is public from the lobby on.
    const store = new GameStore();

    // Act
    store.apply(
      view({
        viewerSeat: 0,
        seats: [seat(0, { isViewer: true }), seat(1, { kind: 'Bot' }), seat(2, { kind: 'Bot' })],
      }),
    );

    // Assert
    expect(store.isSolo()).toBe(true);
  });

  it('Should_not_be_solo_when_a_second_human_has_sat_down', () => {
    // Arrange
    const store = new GameStore();

    // Act
    store.apply(view({ viewerSeat: 0, seats: [seat(0, { isViewer: true }), seat(1), seat(2, { kind: 'Bot' })] }));

    // Assert
    expect(store.isSolo()).toBe(false);
  });

  it('Should_count_only_the_viewers_own_failures_when_accusations_are_in_the_history', () => {
    // Arrange
    // Somebody else's wrong accusation reaches this viewer as `accusationOccurred`, with no pair
    // and no verdict, so it could not be counted here even by mistake. Their own correct one is a
    // different matter: it is in full, and it is not a failure.
    const store = new GameStore();

    // Act
    store.apply(
      view({
        viewerSeat: 0,
        seats: [seat(0, { isViewer: true }), seat(1)],
        history: [
          accusation(1, { accuserSeat: 0, correct: false }),
          accusation(2, { accuserSeat: 0, correct: true }),
          { type: 'accusationOccurred', sequence: 3, turnNumber: 3, accuserSeat: 1 },
          accusation(4, { accuserSeat: 0, correct: false }),
        ],
      }),
    );

    // Assert
    expect(store.myWrongAccusations()).toBe(2);
  });

  it('Should_count_nothing_when_the_viewer_holds_no_seat', () => {
    // Arrange
    // A spectator is sent the redacted shape for everything, and has never accused anything.
    const store = new GameStore();

    // Act
    store.apply(
      view({
        viewerSeat: null,
        seats: [seat(0), seat(1)],
        history: [{ type: 'accusationOccurred', sequence: 1, turnNumber: 1, accuserSeat: 0 }],
      }),
    );

    // Assert
    expect(store.myWrongAccusations()).toBe(0);
  });
});
