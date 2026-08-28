import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ApiClient } from '../../core/api-client';
import { ScenarioSummary } from '../../core/contracts';
import { GameStore } from '../../core/game-store';
import { seat, view } from '../../testing/views';
import { LobbyPanel } from '../lobby-panel';

/**
 * Picking a printed scenario by its book and its number.
 *
 * The claim under test is that the two selects say what the panel will actually deal. It is worth
 * pinning because the way they used to be written broke it silently in one direction only: a
 * `value` binding on a select is written in the same update pass that creates the options, so on
 * the pass that re-creates this block the assignment lands on an empty select and does nothing,
 * and it is never re-applied because the signal behind it has not changed. The panel then shows
 * book 1 scenario 1 while still holding the pair the player chose, and deals that instead.
 *
 * Nothing about it looks wrong on a first render, because the defaults are the first option of
 * each list. It takes a round trip away from the scenario picker and back to separate what is
 * shown from what is held.
 */
describe('LobbyPanel choosing', () => {
  let store: GameStore;

  /** Two books that are not adjacent, so a select falling back to the first option is unmistakable. */
  const SCENARIOS: readonly ScenarioSummary[] = [1, 7].flatMap((book) =>
    [1, 2, 3].map((number) => ({ id: `b${book}s${number}`, book, number, playerCount: 5 })),
  );

  async function render(): Promise<ComponentFixture<LobbyPanel>> {
    store.apply(view({ phase: 'Lobby', playerCount: 5, viewerSeat: 0, seats: [seat(0, { isViewer: true }), seat(1)] }));

    const fixture = TestBed.createComponent(LobbyPanel);
    fixture.detectChanges();

    // The scenario list arrives from a resource, and the two selects are built out of it.
    await fixture.whenStable();
    fixture.detectChanges();

    return fixture;
  }

  function button(fixture: ComponentFixture<LobbyPanel>, text: string): HTMLButtonElement {
    const found = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (candidate) => (candidate.textContent ?? '').trim() === text,
    );

    expect(found, `no button reading "${text}"`).toBeDefined();
    return found as HTMLButtonElement;
  }

  function selects(fixture: ComponentFixture<LobbyPanel>): readonly HTMLSelectElement[] {
    return [...(fixture.nativeElement as HTMLElement).querySelectorAll('select')];
  }

  function click(fixture: ComponentFixture<LobbyPanel>, text: string): void {
    button(fixture, text).click();
    fixture.detectChanges();
  }

  function choose(fixture: ComponentFixture<LobbyPanel>, index: number, value: string): void {
    const select = selects(fixture)[index];
    select.value = value;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [LobbyPanel],
      providers: [{ provide: ApiClient, useValue: { scenarios: () => Promise.resolve(SCENARIOS) } }],
    });

    store = TestBed.inject(GameStore);
  });

  it('Should_show_the_chosen_book_and_number_when_the_picker_is_left_and_come_back_to', async () => {
    // Arrange
    const fixture = await render();
    click(fixture, 'Play a printed scenario');
    choose(fixture, 0, '7');
    choose(fixture, 1, '3');

    // Act
    // Away and back, which is what destroys the two selects and builds them again.
    click(fixture, 'Shuffle a new deal');
    click(fixture, 'Play a printed scenario');

    // Assert
    expect(selects(fixture)[0].value).toBe('7');
    expect(selects(fixture)[1].value).toBe('3');
  });

  it('Should_deal_the_scenario_the_selects_are_showing_when_the_picker_is_left_and_come_back_to', async () => {
    // Arrange
    // The half that matters, and it has to be asserted as an agreement rather than as a value.
    // Dealing b7s3 was never in doubt -- the component holds the pair the player chose whatever the
    // selects are painted with. What broke was the two disagreeing, so the expectation is read off
    // the screen and the deal is checked against it.
    const fixture = await render();
    const dealt: (string | null)[] = [];
    fixture.componentInstance.start.subscribe((id) => dealt.push(id));

    click(fixture, 'Play a printed scenario');
    choose(fixture, 0, '7');
    choose(fixture, 1, '3');
    click(fixture, 'Shuffle a new deal');
    click(fixture, 'Play a printed scenario');

    // Act
    const shown = `b${selects(fixture)[0].value}s${selects(fixture)[1].value}`;
    click(fixture, 'Deal and begin');

    // Assert
    expect(dealt).toEqual([shown]);
    expect(dealt).toEqual(['b7s3']);
  });

  it('Should_move_to_the_first_scenario_of_a_book_when_that_book_does_not_have_the_chosen_number', async () => {
    // Arrange
    // Every book but one holds all twelve, so this is rare — but landing on a book's first scenario
    // beats leaving a pair selected that names nothing the app holds.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [LobbyPanel],
      providers: [
        {
          provide: ApiClient,
          useValue: {
            scenarios: () =>
              Promise.resolve([
                { id: 'b1s1', book: 1, number: 1, playerCount: 5 },
                { id: 'b1s3', book: 1, number: 3, playerCount: 5 },
                { id: 'b7s1', book: 7, number: 1, playerCount: 5 },
              ]),
          },
        },
      ],
    });

    store = TestBed.inject(GameStore);
    const fixture = await render();
    click(fixture, 'Play a printed scenario');
    choose(fixture, 1, '3');

    // Act
    choose(fixture, 0, '7');

    // Assert
    expect(selects(fixture)[1].value).toBe('1');
  });
});
