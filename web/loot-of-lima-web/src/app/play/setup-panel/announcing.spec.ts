import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { GameStore } from '../../core/game-store';
import { seat, view } from '../../testing/views';
import { SetupPanel } from '../setup-panel';

/**
 * Which terrains the panel lets this seat announce.
 *
 * The engine refuses an untruthful announcement, so a wrong press was never able to break a game --
 * it came back as "Seat 0 does not hold Beach fewest of", which is a refusal naming an internal
 * index at somebody who did nothing wrong except be offered the button. Offering a control whose
 * only outcome is a refusal is the same defect as the seat a spectator could not take.
 *
 * The untruthful terrains stay on screen, disabled, with the viewer's own count beside each. That
 * count is the hand they are already looking at, so it gives nothing away, and it is what lets a
 * player check the offer rather than take the app's word for it.
 *
 * What must never happen here is the panel resolving a forced choice by itself. That is tested
 * below, and the reason is in the component: every seat announcing the moment the phase reaches it
 * would make a seat that pauses a seat that was tied, told to the whole table.
 */
describe('SetupPanel announcing', () => {
  let store: GameStore;

  function render(hand: readonly string[]): HTMLElement {
    store.apply(
      view({
        phase: 'Announcing',
        viewerSeat: 0,
        activeSeat: 0,
        seats: [
          { ...seat(0, { isViewer: true, displayName: 'Ada' }), knownHand: hand, handIsComplete: true },
          seat(1, { displayName: 'Grace' }),
        ],
      }),
    );

    const fixture = TestBed.createComponent(SetupPanel);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  /** The three terrain buttons, in declaration order. */
  function terrainButtons(element: HTMLElement): HTMLButtonElement[] {
    return [...element.querySelectorAll<HTMLButtonElement>('.row button')];
  }

  function enabled(element: HTMLElement): string[] {
    return terrainButtons(element)
      .filter((button) => !button.disabled)
      .map((button) => button.textContent?.trim().split(/\s+/)[0] ?? '');
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SetupPanel] });
    store = TestBed.inject(GameStore);
  });

  it('Should_offer_only_the_one_terrain_when_the_hand_settles_the_answer', () => {
    // Arrange
    // Two beach, three forest, three mountain. Beach is the only truthful answer, so it is the only
    // one that may be pressed.
    const hand = ['B1', 'B2', 'F1', 'F2', 'F3', 'M1', 'M2', 'M3'];

    // Act
    const element = render(hand);

    // Assert
    expect(enabled(element)).toEqual(['Beach']);
    expect(terrainButtons(element)).toHaveLength(3);
  });

  it('Should_offer_both_terrains_when_two_are_tied_at_the_minimum', () => {
    // Arrange
    // No beach and no mountain, which is the case the rulebook's tie clause exists for: the choice
    // is the player's, and the table is never told there was one.
    const hand = ['F1', 'F2', 'F3', 'F8'];

    // Act
    const element = render(hand);

    // Assert
    expect(enabled(element)).toEqual(['Beach', 'Mountain']);
  });

  it('Should_offer_all_three_when_the_hand_holds_the_same_of_each', () => {
    // Arrange
    const hand = ['B1', 'F1', 'M1'];

    // Act
    const element = render(hand);

    // Assert
    expect(enabled(element)).toEqual(['Beach', 'Forest', 'Mountain']);
  });

  it('Should_still_show_the_counts_of_the_terrains_it_will_not_accept', () => {
    // Arrange
    // Disabled rather than hidden. A player who cannot see why a button is shut has to trust the
    // app about their own hand, which is the one thing they can check for themselves.
    const hand = ['B1', 'F1', 'F2', 'M1', 'M2'];

    // Act
    const text = render(hand).textContent ?? '';

    // Assert
    expect(text).toContain('Forest');
    expect(text).toContain('(2)');
    expect(enabled(render(hand))).toEqual(['Beach']);
  });

  it('Should_wait_for_the_player_even_when_the_choice_is_forced', () => {
    // Arrange
    // The load-bearing one. Announcing a forced answer automatically would resolve every untied
    // seat instantly, leaving the tied seats as the only ones anybody waits for -- so the pause
    // would say "this player had a tie" to the whole table, which is the one thing the rulebook
    // says must not be revealed.
    const hand = ['B1', 'F1', 'F2', 'M1', 'M2'];

    // Act
    const element = render(hand);

    // Assert
    // Still asked, still on the Announcing panel, and still holding one pressable button.
    expect(element.textContent).toContain('Announce your fewest terrain');
    expect(element.textContent).toContain('Say this aloud, then confirm');
    expect(enabled(element)).toHaveLength(1);
  });

  it('Should_say_the_choice_is_the_players_own_when_the_minimum_is_tied', () => {
    // Arrange
    const hand = ['F1', 'F2', 'F3', 'F8'];

    // Act
    const text = render(hand).textContent ?? '';

    // Assert
    expect(text).toContain('the choice is yours');
    expect(text).toContain('not told there was a tie');
  });
});
