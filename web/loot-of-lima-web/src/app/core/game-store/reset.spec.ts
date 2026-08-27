import { describe, expect, it } from 'vitest';
import { GameStore } from '../game-store';
import { view } from '../../testing/views';

describe('GameStore.reset', () => {
  it('Should_forget_everything_when_the_client_stops_watching', () => {
    // Arrange
    const store = new GameStore();
    store.apply(view({ version: 5 }));
    store.noteError({ code: 'NotYourTurn', message: 'Wait your turn.' });
    store.setStatus('connected');

    // Act
    store.reset();

    // Assert
    expect(store.view()).toBeNull();
    expect(store.error()).toBeNull();
    expect(store.status()).toBe('idle');
    expect(store.staleFramesDropped()).toBe(0);
  });

  it('Should_forget_the_high_water_mark_when_it_resets', () => {
    // Arrange
    // Reset is for leaving a game, not for losing the connection. Leaving means the next frame
    // could be the first of a different table, which may well number lower.
    const store = new GameStore();
    store.apply(view({ version: 50 }));
    store.reset();

    // Act
    const taken = store.apply(view({ version: 1 }));

    // Assert
    expect(taken).toBe(true);
    expect(store.view()?.version).toBe(1);
  });

  it('Should_hold_the_last_refusal_when_the_server_rejects_a_command', () => {
    // Arrange
    // Refusals are addressed to the one caller and never broadcast, so the store keeps exactly one.
    const store = new GameStore();

    // Act
    store.noteError({ code: 'NotYourTurn', message: 'Wait your turn.' });

    // Assert
    expect(store.error()?.code).toBe('NotYourTurn');

    // Act
    store.clearError();

    // Assert
    expect(store.error()).toBeNull();
  });
});
