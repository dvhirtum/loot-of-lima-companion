import { afterEach, describe, expect, it, vi } from 'vitest';
import { PlayerIdentityStore } from '../player-identity';

describe('PlayerIdentityStore.find', () => {
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('Should_find_nothing_when_this_browser_has_never_joined', () => {
    // Arrange
    const store = new PlayerIdentityStore();

    // Act
    const found = store.find('ABCD');

    // Assert
    expect(found).toBeNull();
  });

  it('Should_find_the_identity_when_the_code_differs_only_in_case', () => {
    // Arrange
    // Room codes are typed by hand on the home page and scanned in upper case from the QR link, so
    // the same seat is reachable by either spelling.
    const store = new PlayerIdentityStore();
    store.remember('abcd', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');

    // Act
    const found = store.find('ABCD');

    // Assert
    expect(found?.playerToken).toBe('token-1');
  });

  it('Should_keep_them_apart_when_this_browser_has_joined_two_tables', () => {
    // Arrange
    // A phone that joins a second table has not stopped being welcome at the first.
    const store = new PlayerIdentityStore();
    store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');
    store.remember('WXYZ', { playerToken: 'token-2', playerId: 'player-2' }, 'Grace');

    // Act
    const first = store.find('ABCD');
    const second = store.find('WXYZ');

    // Assert
    expect(first?.playerToken).toBe('token-1');
    expect(second?.playerToken).toBe('token-2');
  });

  it('Should_find_nothing_when_what_was_stored_is_not_an_identity', () => {
    // Arrange
    // Written by an older build, or half-written by a tab that died mid-set. Throwing out of a
    // getter would take the whole page down over a stale string.
    const store = new PlayerIdentityStore();
    localStorage.setItem('lol.player.ABCD', '{ not json');

    // Act
    const found = store.find('ABCD');

    // Assert
    expect(found).toBeNull();
  });

  it('Should_find_nothing_when_the_stored_object_has_no_token', () => {
    // Arrange
    const store = new PlayerIdentityStore();
    localStorage.setItem('lol.player.ABCD', JSON.stringify({ playerId: 'player-1' }));

    // Act
    const found = store.find('ABCD');

    // Assert
    expect(found).toBeNull();
  });

  it('Should_find_the_identity_when_storage_throws_but_this_page_load_joined', () => {
    // Arrange
    // The case a private window actually produces: the join succeeded, the server holds the seat,
    // and the only thing missing is somewhere to write the token down. Falling back to null here
    // sent the player to their own table as a spectator with no hand.
    const store = new PlayerIdentityStore();
    store.remember('ABCD', { playerToken: 'token-1', playerId: 'player-1' }, 'Ada');
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked');
    });

    // Act
    const found = store.find('abcd');

    // Assert
    expect(found).toEqual({ playerToken: 'token-1', playerId: 'player-1', displayName: 'Ada' });
  });

  it('Should_find_nothing_when_storage_itself_throws', () => {
    // Arrange
    // A private window, or a browser set to block site data, does not merely return null --
    // touching localStorage throws. A companion app that fell over there would be broken for the
    // one player most likely to be borrowing somebody else's phone.
    const store = new PlayerIdentityStore();
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked');
    });

    // Act
    const found = store.find('ABCD');

    // Assert
    expect(found).toBeNull();
  });
});
