import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { GameStore } from '../../core/game-store';
import { seat, view } from '../../testing/views';
import { SeatClaim, SeatStrip } from '../seat-strip';

/**
 * Where this app asks anybody who they are.
 *
 * Once: at the seat, at the moment it is taken. It used to be a page in front of the table, so
 * everybody who scanned the QR code paid a name before they had seen whether they wanted a seat,
 * and a watcher who had skipped it was shown a link back to it beside every open seat instead of
 * the seat button itself.
 *
 * The two halves below are the whole rule, and they are opposites: a browser that has not joined
 * must be asked, and a browser that has must not be. The second is not a nicety -- the identity
 * carries the name the table reads off the seat, and asking again is how one person becomes two
 * people at one table.
 */
describe('SeatStrip claiming', () => {
  let store: GameStore;

  /** The lobby of a three-seat table, with every seat still a bot unless told otherwise. */
  function lobby(viewerSeat: number | null = null): void {
    store.apply(
      view({
        phase: 'Lobby',
        viewerSeat,
        seats: [0, 1, 2].map((index) =>
          index === viewerSeat
            ? seat(index, { kind: 'Human', isViewer: true, displayName: 'Ada' })
            : seat(index, { kind: 'Bot', displayName: `Bot ${index + 1}` }),
        ),
      }),
    );
  }

  function render(needsName: boolean): { element: HTMLElement; claims: SeatClaim[]; detect: () => void } {
    const fixture = TestBed.createComponent(SeatStrip);
    const claims: SeatClaim[] = [];

    fixture.componentRef.setInput('needsName', needsName);
    fixture.componentInstance.claim.subscribe((claim) => claims.push(claim));
    fixture.detectChanges();

    return {
      element: fixture.nativeElement as HTMLElement,
      claims,
      detect: () => fixture.detectChanges(),
    };
  }

  function buttonSaying(element: HTMLElement, text: string): HTMLButtonElement {
    const found = [...element.querySelectorAll('button')].find(
      (button) => (button.textContent ?? '').trim() === text,
    );

    if (found === undefined) {
      throw new Error(`No button reading "${text}". There were: ${[...element.querySelectorAll('button')]
        .map((button) => `"${(button.textContent ?? '').trim()}"`)
        .join(', ')}`);
    }

    return found as HTMLButtonElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SeatStrip] });
    store = TestBed.inject(GameStore);
  });

  it('Should_ask_for_a_name_when_this_browser_has_not_joined_the_table', () => {
    // Arrange
    lobby();
    const { element, claims, detect } = render(true);

    // Act
    buttonSaying(element, 'Sit here').click();
    detect();

    // Assert
    // Nothing has been claimed yet: the tap opened the field rather than sending the seat.
    expect(element.querySelector('input[name="displayName"]')).not.toBeNull();
    expect(claims).toHaveLength(0);
  });

  it('Should_claim_under_the_typed_name_when_the_watcher_has_said_who_they_are', () => {
    // Arrange
    lobby();
    const { element, claims, detect } = render(true);
    buttonSaying(element, 'Sit here').click();
    detect();

    // Act
    const field = element.querySelector('input[name="displayName"]') as HTMLInputElement;
    field.value = '  Ada  ';
    field.dispatchEvent(new Event('input'));
    detect();
    buttonSaying(element, 'Sit down').click();
    detect();

    // Assert
    // Trimmed here as well as on the page, because both sides of this hand-off put the name in
    // front of the whole table.
    expect(claims).toEqual([{ seatIndex: 0, name: 'Ada' }]);
  });

  it('Should_claim_outright_when_this_browser_has_already_joined_the_table', () => {
    // Arrange
    // A player who has left one seat and is taking another. They have a name already, and it is
    // the one the rest of the table has been reading.
    lobby();
    const { element, claims, detect } = render(false);

    // Act
    buttonSaying(element, 'Sit here').click();
    detect();

    // Assert
    expect(claims).toEqual([{ seatIndex: 0, name: null }]);
    expect(element.querySelector('input[name="displayName"]')).toBeNull();
  });

  it('Should_offer_no_seat_at_all_when_this_viewer_is_already_sitting_in_one', () => {
    // Arrange
    // `ClaimSeat` refuses a second seat with `PlayerAlreadySeated` -- one identity, one hand, or
    // the projector would build a full view of two hands for one person. A button whose only
    // possible answer is a refusal is the defect this page has already been fixed for once.
    lobby(0);

    // Act
    const { element } = render(false);

    // Assert
    const captions = [...element.querySelectorAll('button')].map((button) => (button.textContent ?? '').trim());
    expect(captions).toEqual(['Leave seat']);
  });

  it('Should_forget_the_half_typed_name_when_a_different_seat_is_tapped', () => {
    // Arrange
    // Opening one seat's field on a name typed for another and abandoned is how somebody sits down
    // as the wrong person.
    lobby();
    const { element, detect } = render(true);
    buttonSaying(element, 'Sit here').click();
    detect();

    const first = element.querySelector('input[name="displayName"]') as HTMLInputElement;
    first.value = 'Ada';
    first.dispatchEvent(new Event('input'));
    detect();

    // Act
    // The second seat's own button, which is the only "Sit here" left while the first is a field.
    buttonSaying(element, 'Sit here').click();
    detect();

    // Assert
    const reopened = element.querySelector('input[name="displayName"]') as HTMLInputElement;
    expect(reopened.value).toBe('');
  });
});
