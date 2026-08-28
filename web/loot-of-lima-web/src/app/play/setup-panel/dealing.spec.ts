import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { GameStore } from '../../core/game-store';
import { seat, view } from '../../testing/views';
import { SetupPanel } from '../setup-panel';

/**
 * What the app tells a player to do with the physical tokens.
 *
 * This is a rules claim rather than a copy preference, which is why it is pinned. Twenty-two of the
 * twenty-four tokens are dealt into hands or turned face up, and exactly two are the treasure -- so
 * if every player took the tokens the app named, whatever was left on the table would *be* the
 * treasure. That holds at three, four and five players alike, because the arithmetic is 24 - 22 and
 * has nothing to do with the table size.
 *
 * So the tokens on the boards are counters and the hand lives only in the app. An instruction to
 * find the named ones would hand the game away, and it would do it at setup, before anybody had
 * asked a single question.
 */
describe('SetupPanel dealing', () => {
  let store: GameStore;

  function render(handSize: number): HTMLElement {
    const hand = Array.from({ length: handSize }, (_unused, index) => `B${index + 1}`);

    store.apply(
      view({
        phase: 'Dealing',
        viewerSeat: 0,
        seats: [
          { ...seat(0, { isViewer: true, displayName: 'Ada' }), knownHand: hand, handIsComplete: true, handSize },
          seat(1, { displayName: 'Grace' }),
        ],
      }),
    );

    const fixture = TestBed.createComponent(SetupPanel);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SetupPanel] });
    store = TestBed.inject(GameStore);
  });

  it('Should_ask_for_any_tokens_rather_than_the_ones_it_dealt_when_the_deal_lands', () => {
    // Arrange
    // Four each, which is a five-player table.
    const handSize = 4;

    // Act
    const text = (render(handSize).textContent ?? '').replace(/\s+/g, ' ');

    // Assert
    expect(text).toContain('any 4');
    expect(text).toContain('face down');
    expect(text).toContain('counters, not your hand');
  });

  it('Should_say_why_taking_the_named_tokens_would_lose_the_game', () => {
    // Arrange
    // A helpful player will otherwise do exactly the wrong thing, so the reason is on screen rather
    // than in a comment nobody at the table can read.
    const handSize = 5;

    // Act
    const text = (render(handSize).textContent ?? '').replace(/\s+/g, ' ');

    // Assert
    expect(text).toContain('Do not hunt for the ones named below');
    expect(text).toContain('would be the treasure');
  });

  it('Should_ask_for_seven_when_three_are_playing', () => {
    // Arrange
    // Hand size comes off the seat, which the projection fills in for every table size: 7, 5 or 4.
    const handSize = 7;

    // Act
    const text = (render(handSize).textContent ?? '').replace(/\s+/g, ' ');

    // Assert
    expect(text).toContain('any 7');
  });
});
