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

  it('Should_still_hand_back_the_identity_when_storage_refuses_to_keep_it', () => {
    // Arrange
    // Out of quota, or storage disabled. This page load can still play; only the reconnect after
    // a refresh is lost, and losing that is much better than failing to join at all.
    const store = new PlayerIdentityStore();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('quota');
    });

    // Act
    const identity = store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Assert
    expect(identity.playerToken).toBe('token-1');
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
