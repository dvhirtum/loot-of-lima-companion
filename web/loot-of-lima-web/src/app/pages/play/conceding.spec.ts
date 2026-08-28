import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { ApiClient } from '../../core/api-client';
import { GameConnection } from '../../core/game-connection';
import { GameStore } from '../../core/game-store';
import { PlayerView } from '../../core/contracts';
import { seat, view } from '../../testing/views';
import { Play } from './play';

/**
 * Who the finished banner is addressing when a solo game was given up on.
 *
 * `Conceded` is reachable only at a table of one, which makes "You gave up" look safe — but the
 * one is not the only person looking. The host laptop sends itself to this very page the moment
 * the deal happens, so the screen most likely to be showing this banner is a spectator's, and it
 * was being told it had done something it cannot even do: `GiveUp` reaches the hub through
 * `WithSeatAsync`, so a viewer with no seat is refused it outright.
 *
 * The seated player still reads "You". Everybody else is told whose game it was, which is public
 * from the lobby onwards.
 */
describe('Play conceding', () => {
  class StubConnection {
    async connect(): Promise<void> {}
    async disconnect(): Promise<void> {}
  }

  let store: GameStore;

  function conceded(viewerSeat: number | null): PlayerView {
    return view({
      phase: 'Finished',
      viewerSeat,
      endReason: 'Conceded',
      winnerSeat: null,
      treasure: ['B1', 'M4'],
      seats: [
        seat(0, { displayName: 'Ada', isViewer: viewerSeat === 0 }),
        seat(1, { kind: 'Bot', displayName: 'Bot 2' }),
        seat(2, { kind: 'Bot', displayName: 'Bot 3' }),
      ],
    });
  }

  function render(viewerSeat: number | null): string {
    const fixture = TestBed.createComponent(Play);
    fixture.componentRef.setInput('roomCode', 'ABCD');
    fixture.detectChanges();

    store.apply(conceded(viewerSeat));
    fixture.detectChanges();

    return ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Play],
      providers: [
        provideRouter([]),
        { provide: GameConnection, useClass: StubConnection },
        { provide: ApiClient, useValue: { join: () => Promise.reject(new Error('not used')) } },
      ],
    });

    store = TestBed.inject(GameStore);
  });

  it('Should_say_you_gave_up_when_the_viewer_is_the_player_who_gave_up', () => {
    // Arrange, Act
    const text = render(0);

    // Assert
    expect(text).toContain('You gave up');
  });

  it('Should_name_the_player_when_the_viewer_has_no_seat', () => {
    // Arrange
    // The host laptop, which this PR redirects here on the deal. It has no seat and never had one.

    // Act
    const text = render(null);

    // Assert
    expect(text).toContain('Ada gave up');
    expect(text).not.toContain('You gave up');
  });
});
