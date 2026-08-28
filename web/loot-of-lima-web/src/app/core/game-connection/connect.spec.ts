import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { view } from '../../testing/views';
import { GameConnection } from '../game-connection';
import { GameStore } from '../game-store';

/**
 * What happens to a connection the app has finished with.
 *
 * All three cases are the same thing seen from different sides: a `HubConnection` outlives the
 * moment the app stopped caring about it. Its close still fires and its frames still arrive, and
 * the `GameStore` they write to is a root singleton shared with whatever connected next -- so a
 * socket nobody is listening to can put "Disconnected" on a live page, or drop the previous
 * table's view into this one. Both need a real socket to reproduce, which is why the hub is faked
 * here rather than driven.
 */

/** The bits of a HubConnection this class actually touches, plus a way to push down it. */
interface FakeHub {
  readonly closeHandlers: Array<() => void>;
  readonly invoked: string[];
  state: number;
  push(method: string, payload: unknown): void;
}

const harness = vi.hoisted(() => {
  const built: unknown[] = [];
  const control = { startRejects: false };
  return { built, control };
});

vi.mock('@microsoft/signalr', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@microsoft/signalr')>();

  class Hub {
    readonly handlers = new Map<string, (payload: unknown) => void>();
    readonly closeHandlers: Array<() => void> = [];
    readonly invoked: string[] = [];
    state = actual.HubConnectionState.Disconnected;

    on(method: string, handler: (payload: unknown) => void): void {
      this.handlers.set(method, handler);
    }

    onclose(handler: () => void): void {
      this.closeHandlers.push(handler);
    }

    onreconnecting(): void {}

    onreconnected(): void {}

    async start(): Promise<void> {
      if (harness.control.startRejects) {
        throw new Error('refused');
      }

      this.state = actual.HubConnectionState.Connected;
    }

    async stop(): Promise<void> {
      this.state = actual.HubConnectionState.Disconnected;
      for (const handler of this.closeHandlers) {
        handler();
      }
    }

    async invoke(method: string): Promise<void> {
      this.invoked.push(method);
    }

    push(method: string, payload: unknown): void {
      this.handlers.get(method)?.(payload);
    }
  }

  class Builder {
    withUrl(): this {
      return this;
    }

    withAutomaticReconnect(): this {
      return this;
    }

    configureLogging(): this {
      return this;
    }

    build(): Hub {
      const hub = new Hub();
      harness.built.push(hub);
      return hub;
    }
  }

  // Object.assign rather than a spread: this factory is hoisted above the module's imports, and
  // the spread compiles to a tslib helper that is not there yet when it runs.
  return Object.assign({}, actual, { HubConnectionBuilder: Builder });
});

describe('GameConnection.connect', () => {
  const built = harness.built as FakeHub[];

  let connection: GameConnection;
  let store: GameStore;

  beforeEach(() => {
    built.length = 0;
    harness.control.startRejects = false;
    TestBed.configureTestingModule({});
    connection = TestBed.inject(GameConnection);
    store = TestBed.inject(GameStore);
  });

  afterEach(() => {
    store.reset();
  });

  it('Should_connect_when_the_first_attempt_failed', async () => {
    // Arrange
    // The api container being down is a state the composition treats as ordinary -- nginx answers
    // 502 on purpose while it is stopped -- so retrying is exactly what the caller will do. A
    // connection left in place by the failure turned the retry into an early return that reported
    // success while nothing was attached.
    harness.control.startRejects = true;
    await expect(connection.connect('ABCD', 'token-1')).rejects.toThrow();

    // Act
    harness.control.startRejects = false;
    await connection.connect('ABCD', 'token-1');

    // Assert
    expect(built).toHaveLength(2);
    expect(built[1].invoked).toContain('Rejoin');
    expect(store.status()).toBe('connected');
  });

  it('Should_keep_the_status_when_a_replaced_connection_finally_closes', async () => {
    // Arrange
    // Leaving one table for another is two async chains: the old socket's stop and the new one's
    // start. If the stop resolves last, its close handler used to set 'disconnected' on a client
    // that is connected and receiving, leaving "Reload the page" up for good.
    await connection.connect('ABCD', 'token-1');
    const abandoned = built[0];
    await connection.connect('WXYZ', 'token-2');

    // Act
    abandoned.closeHandlers.forEach((handler) => handler());

    // Assert
    expect(store.status()).toBe('connected');
  });

  it('Should_build_one_socket_when_a_second_connect_arrives_before_the_first_has_finished', async () => {
    // Arrange
    // Taking a seat does exactly this. The name is now asked for at the seat card, so joining writes
    // a token down on the page that already holds a connection, and two things then reconnect with
    // it: the claim, which cannot send ClaimSeat down a spectator's socket, and the page's effect,
    // which watches the identity.
    //
    // The early return recognises only a connection already in place, and the attach spends its
    // first await holding none -- it has just cleared the fields and is waiting on the old socket's
    // stop. So both calls used to get past it, and the loser's socket was built, started, and then
    // abandoned without ever being stopped: silent, because its handlers check whether they are
    // current and the page looks perfectly healthy on the winner.
    await connection.connect('ABCD', null);
    built.length = 0;

    // Act
    // Started together rather than awaited in turn, which is the whole point of the case.
    await Promise.all([connection.connect('ABCD', 'token-1'), connection.connect('ABCD', 'token-1')]);

    // Assert
    expect(built).toHaveLength(1);
    expect(store.status()).toBe('connected');
  });

  it('Should_ignore_the_frame_when_it_arrives_on_a_replaced_connection', async () => {
    // Arrange
    // A frame the server sent before the stop is already in flight and cannot be un-sent. The
    // store treats a differing gameId as a new game and takes it unconditionally -- correctly, as
    // versions count per game -- so without this guard the previous table's view lands in the
    // current one without even counting as stale.
    await connection.connect('ABCD', 'token-1');
    const abandoned = built[0];
    await connection.connect('WXYZ', 'token-2');
    built[1].push('State', view({ gameId: 'game-wxyz', roomCode: 'WXYZ', version: 4 }));

    // Act
    abandoned.push('State', view({ gameId: 'game-abcd', roomCode: 'ABCD', version: 9 }));

    // Assert
    expect(store.roomCode()).toBe('WXYZ');
    expect(store.staleFramesDropped()).toBe(0);
  });
});
