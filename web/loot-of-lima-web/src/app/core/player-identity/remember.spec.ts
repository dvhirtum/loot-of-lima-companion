import { afterEach, describe, expect, it, vi } from 'vitest';
import { PlayerIdentityStore } from '../player-identity';

describe('PlayerIdentityStore.remember', () => {
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('Should_survive_a_reload_when_a_player_has_joined', () => {
    // Arrange
    // The whole point of storing it: locking the phone or refreshing the page puts a player back
    // in the seat they were already in.
    const store = new PlayerIdentityStore();
    store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Act
    const afterReload = new PlayerIdentityStore().find('ABCD');

    // Assert
    expect(afterReload).toEqual({ playerToken: 'token-1', playerId: 'player-1', displayName: 'Ada' });
  });

  it('Should_replace_the_identity_when_the_same_table_is_joined_again', () => {
    // Arrange
    const store = new PlayerIdentityStore();
    store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Act
    store.remember('ABCD', { playerToken: 'token-2', playerId: 'player-2' }, 'Grace');

    // Assert
    expect(store.find('ABCD')?.playerToken).toBe('token-2');
  });

  it('Should_still_find_the_identity_when_storage_refuses_to_keep_it', () => {
    // Arrange
    // Out of quota, or storage disabled. Joining and playing are two routes and the second one
    // re-reads the identity rather than being handed it, so a `remember` that only returned the
    // identity would leave a player who had just joined arriving at the table as a spectator. What
    // is lost without storage is surviving a refresh, not the seat.
    const store = new PlayerIdentityStore();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('quota');
    });

    // Act
    const identity = store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Assert
    expect(identity.playerToken).toBe('token-1');
    expect(store.find('ABCD')?.playerToken).toBe('token-1');
  });

  it('Should_forget_the_identity_here_too_when_a_player_gives_up_a_table_storage_never_kept', () => {
    // Arrange
    // Leaving has to clear both places, or the seat this browser was told to give up comes back
    // on the next navigation.
    const store = new PlayerIdentityStore();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('quota');
    });
    store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Act
    store.forget('abcd');

    // Assert
    expect(store.find('ABCD')).toBeNull();
  });

  it('Should_leave_no_identity_when_a_player_gives_up_the_table', () => {
    // Arrange
    const store = new PlayerIdentityStore();
    store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Act
    store.forget('abcd');

    // Assert
    expect(store.find('ABCD')).toBeNull();
  });
});
