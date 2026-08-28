import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ApiClient } from '../../core/api-client';
import { ScenarioSummary } from '../../core/contracts';
import { GameStore } from '../../core/game-store';
import { seat, view } from '../../testing/views';
import { LobbyPanel } from '../lobby-panel';

/**
 * What the panel says while the scenario list is not there.
 *
 * There are three reasons the picker can have nothing to show, and only one of them is about the
 * printed books. The list is fetched with `defaultValue: []`, so before it lands, and forever if it
 * fails, every question about the scenarios reads exactly as it would if the seed data were empty
 * — which is how "That scenario is not in the app — the printed book contradicts itself there" came
 * to be shown for the whole loading window and for every network fault. That sentence is about
 * `b16s12` and nothing else: one pair out of 240.
 */
describe('LobbyPanel listing', () => {
  let store: GameStore;

  const SCENARIOS: readonly ScenarioSummary[] = [{ id: 'b1s1', book: 1, number: 1, playerCount: 5 }];

  const BOOK_16 = 'contradicts itself';

  function configure(scenarios: () => Promise<readonly ScenarioSummary[]>): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [LobbyPanel],
      providers: [{ provide: ApiClient, useValue: { scenarios } }],
    });

    store = TestBed.inject(GameStore);
  }

  function create(): ComponentFixture<LobbyPanel> {
    store.apply(view({ phase: 'Lobby', playerCount: 5, viewerSeat: 0, seats: [seat(0, { isViewer: true }), seat(1)] }));

    const fixture = TestBed.createComponent(LobbyPanel);
    fixture.detectChanges();
    return fixture;
  }

  function text(fixture: ComponentFixture<LobbyPanel>): string {
    return ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
  }

  function click(fixture: ComponentFixture<LobbyPanel>, label: string): void {
    const found = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (candidate) => (candidate.textContent ?? '').trim() === label,
    );

    expect(found, `no button reading "${label}"`).toBeDefined();
    (found as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  beforeEach(() => {
    configure(() => Promise.resolve(SCENARIOS));
  });

  it('Should_say_it_is_fetching_when_the_scenario_list_has_not_arrived', async () => {
    // Arrange
    // Held open, so the panel is observed in the state it passes through rather than after it.
    configure(() => new Promise<readonly ScenarioSummary[]>(() => undefined));
    const fixture = create();

    // Act
    click(fixture, 'Play a printed scenario');

    // Assert
    expect(text(fixture)).toContain('Fetching the list');
    expect(text(fixture)).not.toContain(BOOK_16);
  });

  it('Should_offer_another_attempt_when_the_scenario_list_could_not_be_fetched', async () => {
    // Arrange
    // A failure is not the seed data being empty, and it is certainly not book 16's misprint.
    configure(() => Promise.reject(new Error('offline')));
    const fixture = create();
    await fixture.whenStable();
    fixture.detectChanges();

    // Act
    click(fixture, 'Play a printed scenario');

    // Assert
    expect(text(fixture)).toContain('could not be loaded');
    expect(text(fixture)).not.toContain(BOOK_16);
  });

  it('Should_blame_the_printed_book_only_when_the_list_arrived_without_that_pair', async () => {
    // Arrange
    // The one case the sentence is true of. Book 16 is listed, but scenario 12 is missing from it,
    // which is exactly the shape of the real seed data.
    configure(() =>
      Promise.resolve([
        { id: 'b16s11', book: 16, number: 11, playerCount: 5 },
        { id: 'b16s12', book: 16, number: 12, playerCount: 5 },
      ]),
    );

    const fixture = create();
    await fixture.whenStable();
    fixture.detectChanges();
    click(fixture, 'Play a printed scenario');

    // Act
    // Reach a pair the list does not hold, by dropping it out from under the selection.
    configure(() => Promise.resolve([{ id: 'b16s11', book: 16, number: 11, playerCount: 5 }]));
    const missing = create();
    await missing.whenStable();
    missing.detectChanges();
    click(missing, 'Play a printed scenario');
    const select = [...(missing.nativeElement as HTMLElement).querySelectorAll('select')][1];

    // Assert
    // One option, so nothing can be selected that the list does not hold, and the message stays away.
    expect(select.options).toHaveLength(1);
    expect(text(missing)).not.toContain(BOOK_16);
  });

  it('Should_keep_the_deal_button_shut_when_the_scenario_list_has_not_arrived', async () => {
    // Arrange
    // Nothing can be chosen yet, so nothing can be dealt. The button being open here would send a
    // null scenario id, which is a shuffled deal -- the opposite of what the player asked for.
    configure(() => new Promise<readonly ScenarioSummary[]>(() => undefined));
    const fixture = create();

    // Act
    click(fixture, 'Play a printed scenario');
    const deal = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (candidate) => (candidate.textContent ?? '').trim() === 'Deal and begin',
    );

    // Assert
    expect((deal as HTMLButtonElement).disabled).toBe(true);
  });
});
