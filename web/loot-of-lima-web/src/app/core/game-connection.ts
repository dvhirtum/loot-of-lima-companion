import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import {
  AbilityType,
  AccuseRequest,
  AskQuestionRequest,
  AskSecretQuestionRequest,
  Direction,
  ErrorDto,
  GameEventDto,
  PlayerView,
  StartGameRequest,
  Terrain,
} from './contracts';
import { GameStore } from './game-store';

/** The three methods the server calls on a client, and nothing else. */
const STATE = 'State';
const EVENT = 'Event';
const ERROR = 'Error';

/**
 * The client half of the hub.
 *
 * One connection per page, owned here, so that the rest of the app talks about claiming a seat and
 * asking a question rather than about transports and reconnects. Every command is a plain method;
 * none of them returns state, because state arrives on `State` and nowhere else.
 */
@Injectable({ providedIn: 'root' })
export class GameConnection {
  private readonly store = inject(GameStore);
  private readonly _lastEvent = signal<GameEventDto | null>(null);

  private connection: HubConnection | null = null;
  private roomCode: string | null = null;
  private playerToken: string | null = null;

  /**
   * The most recent nudge, for a component that wants to animate or chime.
   *
   * Never read as state. The authoritative view arrives alongside every event and is the only
   * thing that says what the game looks like -- an event says something happened, not what is now
   * true.
   */
  readonly lastEvent = this._lastEvent.asReadonly();

  /**
   * Attaches to a game, as a player when given a token and as a spectator otherwise.
   *
   * Safe to call again for the same room: the second call is a no-op rather than a second socket,
   * which matters because a route can be re-entered by a navigation the player did not think of as
   * leaving.
   */
  async connect(roomCode: string, playerToken: string | null): Promise<void> {
    if (this.connection !== null && this.roomCode === roomCode && this.playerToken === playerToken) {
      return;
    }

    await this.disconnect();

    this.roomCode = roomCode;
    this.playerToken = playerToken;

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/game')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // Every handler below is gated on this connection still being the current one.
    //
    // `disconnect` stops a socket but cannot un-fire what is already in flight: a frame the server
    // sent before the stop, and the `onclose` the stop itself causes, both arrive afterwards, and
    // the store they would write to is a root singleton shared with whatever connection replaced
    // this one. Leaving the store to a socket the app has finished with is how a live client ends
    // up displaying "Disconnected" for good, and how a frame from the previous table lands in the
    // new one.
    const isCurrent = (): boolean => this.connection === connection;

    // Handlers before the connection opens, so nothing the server pushes can arrive unheard.
    //
    // Every one of them has a statement body and returns nothing on purpose. The server pushes
    // these with SendAsync and is not waiting for an answer, so a handler that happens to return a
    // value -- `apply` hands back whether it took the frame -- makes the client try to send one,
    // and the connection logs "Result given for 'State' method but server is not expecting a
    // result" on every single frame.
    connection.on(STATE, (view: PlayerView) => {
      if (isCurrent()) {
        this.store.apply(view);
      }
    });

    connection.on(EVENT, (happened: GameEventDto) => {
      if (isCurrent()) {
        this._lastEvent.set(happened);
      }
    });

    connection.on(ERROR, (error: ErrorDto) => {
      if (isCurrent()) {
        this.store.noteError(error);
      }
    });

    connection.onreconnecting(() => {
      if (isCurrent()) {
        this.store.setStatus('reconnecting');
      }
    });

    // A reconnect is a new connection with a new id, and the server detached the old one when it
    // dropped. Without rejoining, this client would be connected to a hub that has no idea which
    // game it is watching, and would sit there looking healthy and receiving nothing.
    connection.onreconnected(() => {
      if (isCurrent()) {
        void this.rejoin();
      }
    });

    connection.onclose(() => {
      if (isCurrent()) {
        this.store.setStatus('disconnected');
      }
    });

    this.connection = connection;
    this.store.setStatus('connecting');

    try {
      await connection.start();
      await this.rejoin();
    } catch {
      // The failed connection has to be let go of here, not just reported.
      //
      // `connect` returns early when it is already holding this room and token, so a connection
      // left in place by a failure makes every retry a no-op that reports success while nothing is
      // attached -- and the api container being down, which is a state the composition treats as
      // ordinary, is precisely when a retry is what the caller will do.
      await this.disconnect();
      this.store.setStatus('disconnected');
      throw new Error(`Could not connect to game ${roomCode}.`);
    }
  }

  /**
   * Lets go of the current connection, if there is one.
   *
   * The fields are cleared before the stop is awaited, deliberately: from this line on the
   * connection is no longer current, so anything still arriving on it is ignored rather than
   * racing whatever connects next.
   */
  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.roomCode = null;
    this.playerToken = null;

    if (connection !== null) {
      await connection.stop();
    }
  }

  claimSeat(seatIndex: number): Promise<void> {
    return this.send('ClaimSeat', seatIndex);
  }

  releaseSeat(): Promise<void> {
    return this.send('ReleaseSeat');
  }

  startGame(scenarioId: string | null): Promise<void> {
    const request: StartGameRequest = { scenarioId };
    return this.send('StartGame', request);
  }

  confirmTokensTaken(): Promise<void> {
    return this.send('ConfirmTokensTaken');
  }

  confirmPassDone(): Promise<void> {
    return this.send('ConfirmPassDone');
  }

  announceFewestTerrain(terrain: Terrain): Promise<void> {
    return this.send('AnnounceFewestTerrain', terrain);
  }

  spendAbility(ability: AbilityType): Promise<void> {
    return this.send('SpendAbility', ability);
  }

  askQuestion(targetSeat: number, start: Direction, end: Direction, terrain: Terrain | null): Promise<void> {
    const request: AskQuestionRequest = { targetSeat, start, end, terrain };
    return this.send('AskQuestion', request);
  }

  askSecretQuestion(
    targetSeat: number,
    start: Direction,
    end: Direction,
    terrain: Terrain | null,
  ): Promise<void> {
    const request: AskSecretQuestionRequest = { targetSeat, start, end, terrain };
    return this.send('AskSecretQuestion', request);
  }

  accuse(first: string, second: string): Promise<void> {
    const request: AccuseRequest = { first, second };
    return this.send('Accuse', request);
  }

  /**
   * Ends a solo game and asks to be shown the treasure.
   *
   * Takes no argument on purpose. Who is giving up is the connection's seat, the same as every
   * other command here, and whether they are allowed to is the server's alone: a table of one, and
   * a wrong accusation already behind them.
   */
  giveUp(): Promise<void> {
    return this.send('GiveUp');
  }

  /**
   * Tells the server which game this connection is watching, and who is holding it.
   *
   * Idempotent on the server, and called both when the connection first opens and after every
   * reconnect. The frame it triggers is sent to this connection alone -- an arrival changes no
   * seat, no hand and no history, so nobody else is owed anything.
   */
  private async rejoin(): Promise<void> {
    if (this.connection === null || this.roomCode === null) {
      return;
    }

    await this.connection.invoke('Rejoin', this.roomCode, this.playerToken);
    this.store.setStatus('connected');
  }

  /**
   * Sends one command.
   *
   * A refusal does not come back through here: the server answers it on `Error`, addressed to this
   * caller alone, and the store keeps it. What can still fail here is the send itself, which is a
   * broken connection rather than a rejected move, and is reported as such.
   */
  private async send(method: string, ...args: unknown[]): Promise<void> {
    if (this.connection === null || this.connection.state !== HubConnectionState.Connected) {
      this.store.noteError({ code: 'NotConnected', message: 'Not connected to the game.' });
      return;
    }

    this.store.clearError();

    try {
      await this.connection.invoke(method, ...args);
    } catch {
      this.store.noteError({ code: 'SendFailed', message: `Could not send ${method}. Check the connection.` });
    }
  }
}
