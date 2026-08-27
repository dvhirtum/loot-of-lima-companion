import { describe, expect, it } from 'vitest';
import { sectorsCovered } from '../map-geometry';

/**
 * How wide the highlighted wedge is.
 *
 * The clockwise wrap and the coincident-arrow case are the two things about this game's geometry
 * that surprise people, and the map is where a player would first notice the app disagreeing with
 * the board. `fullCircleWhenEqual` arrives from `GET /api/board` rather than being assumed here,
 * so these tests pass it both ways on purpose.
 */
describe('mapGeometry.sectorsCovered', () => {
  it('Should_cover_one_slice_when_the_arrows_are_neighbours', () => {
    // Arrange
    const start = 0;
    const end = 1;

    // Act
    const covered = sectorsCovered(start, end, true);

    // Assert
    expect(covered).toBe(1);
  });

  it('Should_wrap_clockwise_past_north_when_the_end_is_a_lower_index_than_the_start', () => {
    // Arrange
    // W up to NE is W, NW, N -- three slices going clockwise, not the five going the other way.
    const start = 6;
    const end = 1;

    // Act
    const covered = sectorsCovered(start, end, true);

    // Assert
    expect(covered).toBe(3);
  });

  it('Should_cover_half_the_island_when_the_arrows_are_opposite', () => {
    // Arrange
    const start = 0;
    const end = 4;

    // Act
    const covered = sectorsCovered(start, end, true);

    // Assert
    expect(covered).toBe(4);
  });

  it('Should_sweep_the_whole_circle_when_the_arrows_coincide_and_the_board_says_so', () => {
    // Arrange
    // The dial's arrows point at compass lines, not at sectors, so two arrows on one line leave
    // every token between them. This is the rule the oracle's 244,736 answers rest on.
    const start = 3;

    // Act
    const covered = sectorsCovered(start, start, true);

    // Assert
    expect(covered).toBe(8);
  });

  it('Should_sweep_nothing_when_the_arrows_coincide_and_the_board_says_otherwise', () => {
    // Arrange
    // Not a case this board produces, and that is the point: the semantics are the server's to
    // state, so this file must not have quietly hard-coded them.
    const start = 3;

    // Act
    const covered = sectorsCovered(start, start, false);

    // Assert
    expect(covered).toBe(0);
  });
});
