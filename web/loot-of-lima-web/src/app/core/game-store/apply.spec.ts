import { describe, expect, it } from 'vitest';
import { GameStore } from '../game-store';
import { view } from '../../testing/views';

/**
 * The frame ordering guard.
 *
 * `PlayerView.Version` rises by one per command applied. Commands are ordered by the server's
 * store; the sends that follow them are not, because each is a separate await over a separate
 * connection, so a frame can overtake its successor on the way out. Keeping the highest version
 * seen and dropping anything older is the whole of the client's half of that.
 *
 * Tests live in a folder named for the type under test with one file per method, and are named
 * `Should_<expected>_when_<condition>`, matching the C# suites so the two halves of this repository
 * read the same way.
 */
describe('GameStore.apply', () => {
  it('Should_take_the_frame_when_it_is_the_first_one', () => {
    // Arrange
    const store = new GameStore();

    // Act
    const taken = store.apply(view({ version: 7 }));

    // Assert
    expect(taken).toBe(true);
    expect(store.view()?.version).toBe(7);
  });

  it('Should_take_the_frame_when_its_version_is_newer', () => {
    // Arrange
    const store = new GameStore();
    store.apply(view({ version: 4 }));

    // Act
    const taken = store.apply(view({ version: 5, turnNumber: 2 }));

    // Assert
    expect(taken).toBe(true);
    expect(store.view()?.turnNumber).toBe(2);
  });

  it('Should_drop_the_frame_when_its_version_is_older', () => {
    // Arrange
    // The overtaking frame. It describes a revision the client has already moved past, so putting
    // it on screen would rewind the game in front of a player who did nothing wrong.
    const store = new GameStore();
    store.apply(view({ version: 9, turnNumber: 5 }));

    // Act
    const taken = store.apply(view({ version: 8, turnNumber: 4 }));

    // Assert
    expect(taken).toBe(false);
    expect(store.view()?.turnNumber).toBe(5);
  });

  it('Should_keep_dropping_when_several_old_frames_arrive_after_a_new_one', () => {
    // Arrange
    // The mark is the highest ever seen, not the version of whatever arrived last, so a run of
    // stragglers cannot walk the client backwards one frame at a time.
    const store = new GameStore();
    store.apply(view({ version: 20, turnNumber: 9 }));

    // Act
    const taken = [17, 18, 19].map((version) => store.apply(view({ version })));

    // Assert
    expect(taken).toEqual([false, false, false]);
    expect(store.view()?.version).toBe(20);
  });

  it('Should_count_the_frame_when_one_is_dropped', () => {
    // Arrange
    // A silent guard is a guard nobody can tell has stopped working.
    const store = new GameStore();
    store.apply(view({ version: 3 }));

    // Act
    store.apply(view({ version: 2 }));
    store.apply(view({ version: 1 }));

    // Assert
    expect(store.staleFramesDropped()).toBe(2);
  });

  it('Should_take_the_frame_when_its_version_repeats', () => {
    // Arrange
    // Not a duplicate. Rejoining with a token projects the same revision to a seat rather than to
    // a spectator, so the second frame carries a hand the first one could not.
    const store = new GameStore();
    store.apply(view({ version: 6, viewerSeat: null }));

    // Act
    const taken = store.apply(view({ version: 6, viewerSeat: 2 }));

    // Assert
    expect(taken).toBe(true);
    expect(store.view()?.viewerSeat).toBe(2);
  });

  it('Should_take_the_frame_when_it_belongs_to_a_different_game', () => {
    // Arrange
    // Versions count per game. A new table starting at 1 would otherwise be judged stale by the
    // last table this browser watched.
    const store = new GameStore();
    store.apply(view({ gameId: 'first', version: 40 }));

    // Act
    const taken = store.apply(view({ gameId: 'second', version: 1 }));

    // Assert
    expect(taken).toBe(true);
    expect(store.view()?.gameId).toBe('second');
  });

  it('Should_drop_an_old_frame_again_when_the_mark_moved_to_a_different_game', () => {
    // Arrange
    // The mark follows the game that is being watched now, rather than staying at the high-water
    // of every game this browser has ever seen.
    const store = new GameStore();
    store.apply(view({ gameId: 'first', version: 40 }));
    store.apply(view({ gameId: 'second', version: 3 }));

    // Act
    const taken = store.apply(view({ gameId: 'second', version: 2 }));

    // Assert
    expect(taken).toBe(false);
    expect(store.view()?.version).toBe(3);
  });
});
