import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { GameConnection } from '../../core/game-connection';
import { GameStore } from '../../core/game-store';
import { view } from '../../testing/views';
import { Play } from './play';

/**
 * What the page does when it is pointed at a different table.
 *
 * /play/ABCD to /play/WXYZ is one route with a changed parameter, so Angular reuses this component
 * rather than rebuilding it and the teardown that resets the store never runs. The store is a root
 * singleton, so without clearing it here the previous table's seats, hand and history stay on
 * screen under the new table's heading -- and, worse, `apply` takes a frame from a different game
 * unconditionally, so a late arrival from the old socket would be shown as current.
 */
describe('Play table change', () => {
  /** The page's lifecycle is what is under test, so the hub is stubbed out entirely. */
  class StubConnection {
    readonly connectedTo: string[] = [];

    async connect(roomCode: string): Promise<void> {
      this.connectedTo.push(roomCode);
    }

    async disconnect(): Promise<void> {}
  }

  let store: GameStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Play],
      providers: [{ provide: GameConnection, useClass: StubConnection }],
    });
    store = TestBed.inject(GameStore);
  });

  it('Should_forget_the_previous_table_when_the_room_code_changes', () => {
    // Arrange
    const fixture = TestBed.createComponent(Play);
    fixture.componentRef.setInput('roomCode', 'ABCD');
    fixture.detectChanges();
    store.apply(view({ gameId: 'game-abcd', roomCode: 'ABCD', version: 7 }));

    // Act
    fixture.componentRef.setInput('roomCode', 'WXYZ');
    fixture.detectChanges();

    // Assert
    expect(store.view()).toBeNull();
  });

  it('Should_keep_the_view_when_the_room_code_has_not_changed', () => {
    // Arrange
    // The effect re-runs whenever the identity changes too -- claiming a seat is exactly that --
    // and clearing the table every time would blank the page mid-game.
    const fixture = TestBed.createComponent(Play);
    fixture.componentRef.setInput('roomCode', 'ABCD');
    fixture.detectChanges();
    store.apply(view({ gameId: 'game-abcd', roomCode: 'ABCD', version: 7 }));

    // Act
    fixture.componentRef.setInput('roomCode', 'ABCD');
    fixture.detectChanges();

    // Assert
    expect(store.roomCode()).toBe('ABCD');
  });
});
