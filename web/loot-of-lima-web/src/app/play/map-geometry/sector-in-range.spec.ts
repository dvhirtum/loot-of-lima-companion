import { describe, expect, it } from 'vitest';
import { sectorInRange } from '../map-geometry';

/**
 * Which sectors the wedge lights up.
 *
 * The lit tokens and the lit segment are two drawings of one range, so this is measured against the
 * same `sectorsCovered` the segment is drawn from -- a map whose highlight ended one sector before
 * its tokens did would be lying about the question in a way only an eye would catch.
 *
 * It answers yes or no about one sector at a time and is never asked for a total. Counting the
 * tokens in an arc is the server's job and the whole of the game; this only decides what to draw.
 */
describe('mapGeometry.sectorInRange', () => {
  it('Should_include_the_starting_sector_when_the_arc_begins_there', () => {
    // Arrange
    // Start-inclusive is half of the rule the picture exists to make visible.
    const start = 2;
    const end = 5;

    // Act
    const inside = sectorInRange(start, start, end, true);

    // Assert
    expect(inside).toBe(true);
  });

  it('Should_exclude_the_ending_sector_when_the_arc_stops_there', () => {
    // Arrange
    // And end-exclusive is the other half. The arrows point at the lines between sectors, so the
    // sector the second arrow points *past* is not in the question.
    const start = 2;
    const end = 5;

    // Act
    const inside = sectorInRange(end, start, end, true);

    // Assert
    expect(inside).toBe(false);
  });

  it('Should_include_a_sector_the_arc_wraps_past_north_to_reach', () => {
    // Arrange
    // W up to NE is W, NW, N. North is index 0 and lower than either arrow, so a comparison that
    // forgot to wrap would light nothing here at all.
    const start = 6;
    const end = 1;

    // Act
    const inside = sectorInRange(0, start, end, true);

    // Assert
    expect(inside).toBe(true);
  });

  it('Should_exclude_a_sector_outside_an_arc_that_wraps_past_north', () => {
    // Arrange
    const start = 6;
    const end = 1;

    // Act
    const inside = sectorInRange(3, start, end, true);

    // Assert
    expect(inside).toBe(false);
  });

  it('Should_include_every_sector_when_the_arrows_coincide_and_the_board_says_so', () => {
    // Arrange
    // The surprising half of this game's geometry, and the one the map must not have its own
    // opinion about: `fullCircleWhenEqual` arrives from `GET /api/board`.
    const start = 3;

    // Act
    const covered = [0, 1, 2, 3, 4, 5, 6, 7].map((sector) => sectorInRange(sector, start, start, true));

    // Assert
    expect(covered.every(Boolean)).toBe(true);
  });

  it('Should_include_no_sector_when_the_arrows_coincide_and_the_board_says_otherwise', () => {
    // Arrange
    const start = 3;

    // Act
    const covered = [0, 1, 2, 3, 4, 5, 6, 7].map((sector) => sectorInRange(sector, start, start, false));

    // Assert
    expect(covered.some(Boolean)).toBe(false);
  });
});
