import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { GameStore } from '../../core/game-store';
import { HistoryEntryView, PlayerView, SeatView } from '../../core/contracts';
import { accusation, seat, view } from '../../testing/views';
import { AccusationPanel } from '../accusation-panel';
import { TableUi } from '../table-ui';

/**
 * When the panel offers to end a solo game.
 *
 * Giving up is the only way out of a one-player table short of solving it, because a wrong
 * accusation no longer eliminates anybody there -- eliminating the only player would finish the
 * game, and finishing the game is the single moment the treasure is projected, so guessing wrong
 * would be rewarded with the answer.
 *
 * That makes this button the one control in the app whose whole purpose is to reveal the treasure,
 * so what it must not do is appear before there is something to give up on. The server refuses it
 * either way; this is the panel not offering a button whose only outcome would be a refusal.
 */
describe('AccusationPanel giving up', () => {
  let store: GameStore;

  function render(overrides: Partial<PlayerView>, seats: readonly SeatView[]): HTMLElement {
    store.apply(view({ phase: 'InProgress', viewerSeat: 0, seats: [...seats], ...overrides }));

    const fixture = TestBed.createComponent(AccusationPanel);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  /** A solo table: the viewer, and bots. */
  const soloSeats: readonly SeatView[] = [
    seat(0, { isViewer: true, isActive: true, displayName: 'Ada' }),
    seat(1, { kind: 'Bot' }),
    seat(2, { kind: 'Bot' }),
  ];

  const wrongOnce: readonly HistoryEntryView[] = [accusation(1, { accuserSeat: 0, correct: false })];

  function giveUpButton(element: HTMLElement): HTMLButtonElement | null {
    return (
      [...element.querySelectorAll('button')].find((button) => (button.textContent ?? '').includes('Give up')) ?? null
    );
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [GameStore, TableUi] });
    store = TestBed.inject(GameStore);
  });

  it('Should_offer_to_reveal_the_treasure_when_a_solo_player_has_accused_and_been_wrong', () => {
    // Arrange, Act
    const element = render({ history: wrongOnce }, soloSeats);

    // Assert
    expect(giveUpButton(element)).not.toBeNull();
  });

  it('Should_not_offer_to_reveal_the_treasure_when_a_solo_player_has_not_yet_accused', () => {
    // Arrange
    // The puzzle is intact and nobody has committed to anything, so there is nothing to give up on
    // -- only an answer to spoil.

    // Act
    const element = render({ history: [] }, soloSeats);

    // Assert
    expect(giveUpButton(element)).toBeNull();
  });

  it('Should_not_offer_to_reveal_the_treasure_when_the_solo_accusation_was_right', () => {
    // Arrange
    // A correct accusation finishes the game on its own, so the panel is not on screen -- but the
    // count has to ignore it either way, or solving it would look like failing.

    // Act
    const element = render({ history: [accusation(1, { accuserSeat: 0, correct: true })] }, soloSeats);

    // Assert
    expect(giveUpButton(element)).toBeNull();
  });

  it('Should_not_offer_to_reveal_the_treasure_when_a_second_human_is_at_the_table', () => {
    // Arrange
    // Two humans have their own way out: accusing wrongly puts you out and leaves the other one
    // playing. Conceding here would end somebody else's game as well, and the server refuses it.

    // Act
    const element = render({ history: wrongOnce }, [
      seat(0, { isViewer: true, isActive: true }),
      seat(1, { displayName: 'Grace' }),
      seat(2, { kind: 'Bot' }),
    ]);

    // Assert
    expect(giveUpButton(element)).toBeNull();
  });

  it('Should_say_a_wrong_pair_is_not_the_end_of_it_when_the_table_is_solo', () => {
    // Arrange
    // The copy is the rule as the player meets it. Telling a solo player they would be "out for the
    // rest of the game" would be describing a table they are not sitting at.

    // Act
    const element = render({ history: [] }, soloSeats);

    // Assert
    expect(element.textContent).not.toContain('out for the rest of the game');
    expect(element.textContent).toContain('carry on');
  });

  it('Should_say_a_wrong_pair_puts_you_out_when_a_second_human_is_at_the_table', () => {
    // Arrange, Act
    const element = render({ history: [] }, [
      seat(0, { isViewer: true, isActive: true }),
      seat(1, { displayName: 'Grace' }),
    ]);

    // Assert
    expect(element.textContent).toContain('out for the rest of the game');
  });
});
