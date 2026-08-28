import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { HistoryEntryView } from '../../core/contracts';
import { GameStore } from '../../core/game-store';
import { HistoryPanel } from '../history-panel';
import { seat, view } from '../../testing/views';

/**
 * What the log actually puts on screen for each history type.
 *
 * The redacted types are the point. A viewer who was not party to a secret question receives
 * `secretQuestionOccurred`, which has no property for the range or the answer, and a failed
 * accusation reaches everybody else as `accusationOccurred` with no property for the pair. The
 * template renders those as their own cases rather than as the full case with missing values, and
 * these tests are what keeps that true when somebody later tries to tidy the two branches into one.
 */
describe('HistoryPanel rendering', () => {
  let store: GameStore;

  function render(history: readonly HistoryEntryView[]): string {
    store.apply(
      view({
        viewerSeat: 0,
        seats: [seat(0, { isViewer: true, displayName: 'Ada' }), seat(1, { displayName: 'Grace' })],
        history,
      }),
    );

    const fixture = TestBed.createComponent(HistoryPanel);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HistoryPanel] });
    store = TestBed.inject(GameStore);
  });

  it('Should_read_back_the_question_when_it_was_asked_in_public', () => {
    // Arrange
    // The arc is spelled out rather than abbreviated, because half-open is the part of this game
    // people get wrong and the log has to be checkable against the tokens on the table.
    const entry: HistoryEntryView = {
      type: 'publicQuestion',
      sequence: 1,
      turnNumber: 3,
      askerSeat: 0,
      targetSeat: 1,
      start: 'N',
      end: 'SE',
      terrain: 'Forest',
      answer: 2,
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('Ada asked Grace');
    expect(text).toContain('forest locations from N up to SE');
    expect(text).toContain('2');
  });

  it('Should_say_the_whole_island_when_the_arrows_coincide', () => {
    // Arrange
    // start == end sweeps all eight sectors, not none. Rendering it as "from N up to N" would
    // read as an empty range.
    const entry: HistoryEntryView = {
      type: 'publicQuestion',
      sequence: 1,
      turnNumber: 1,
      askerSeat: 0,
      targetSeat: 1,
      start: 'N',
      end: 'N',
      terrain: null,
      answer: 4,
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('locations anywhere');
    expect(text).not.toContain('up to');
  });

  it('Should_show_the_range_and_the_answer_when_the_viewer_was_party_to_the_secret', () => {
    // Arrange
    const entry: HistoryEntryView = {
      type: 'secretQuestion',
      sequence: 1,
      turnNumber: 4,
      askerSeat: 0,
      targetSeat: 1,
      start: 'E',
      end: 'W',
      terrain: null,
      answer: 1,
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('Secretly, Ada asked Grace');
    expect(text).toContain('from E up to W');
  });

  it('Should_name_the_terrain_when_the_secret_question_was_restricted_to_one', () => {
    // Arrange
    // A Pistol question may name a terrain like any other, so a viewer entitled to the question is
    // entitled to the whole of it. Reading it back as "locations" would misquote what was asked.
    const entry: HistoryEntryView = {
      type: 'secretQuestion',
      sequence: 1,
      turnNumber: 4,
      askerSeat: 0,
      targetSeat: 1,
      start: 'E',
      end: 'W',
      terrain: 'Mountain',
      answer: 1,
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('mountain locations from E up to W');
  });

  it('Should_show_only_that_it_happened_when_the_viewer_was_not_party_to_the_secret', () => {
    // Arrange
    // A different DTO with no field for the range or the answer, so there is nothing here for a
    // forgotten branch to leak.
    const entry: HistoryEntryView = {
      type: 'secretQuestionOccurred',
      sequence: 1,
      turnNumber: 4,
      askerSeat: 0,
      targetSeat: 1,
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('Ada asked Grace a secret question.');
    expect(text).not.toContain('up to');
  });

  it('Should_name_the_pair_when_the_accusation_was_correct', () => {
    // Arrange
    const entry: HistoryEntryView = {
      type: 'accusation',
      sequence: 1,
      turnNumber: 6,
      accuserSeat: 0,
      named: ['B3', 'B5'],
      correct: true,
    };

    // Act
    const text = render([entry]);

    // Assert
    // Named as the box names them: the entry carries `B3`, and the log reads `3B`.
    expect(text).toContain('Ada named');
    expect(text).toContain('3B');
    expect(text).toContain('5B');
    expect(text).toContain('the treasure.');
  });

  it('Should_name_no_pair_when_the_accusation_failed_and_the_viewer_was_not_the_accuser', () => {
    // Arrange
    // The rulebook says an incorrect accusation reveals nothing, and a ruled-out pair is a real
    // deduction. So the entry has no property for the pair and the log must not imply one.
    const entry: HistoryEntryView = {
      type: 'accusationOccurred',
      sequence: 1,
      turnNumber: 6,
      accuserSeat: 1,
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('Grace accused, and was wrong. Nothing was revealed.');
    expect(text).not.toContain('named');
  });

  it('Should_say_only_the_terrain_when_a_seat_announced_its_fewest', () => {
    // Arrange
    // No word about whether the choice broke a tie. A player breaking one must not reveal that
    // there was one, and there is no field here that could.
    const entry: HistoryEntryView = {
      type: 'fewestTerrainAnnounced',
      sequence: 1,
      turnNumber: 0,
      seatIndex: 1,
      terrain: 'Mountain',
    };

    // Act
    const text = render([entry]);

    // Assert
    expect(text).toContain('Grace holds fewest mountain.');
    expect(text.toLowerCase()).not.toContain('tie');
  });

  it('Should_say_nothing_happened_when_the_history_is_empty', () => {
    // Arrange, Act
    const text = render([]);

    // Assert
    expect(text).toContain('Nothing yet.');
  });
});
