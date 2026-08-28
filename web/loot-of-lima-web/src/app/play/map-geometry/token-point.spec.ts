import { describe, expect, it } from 'vitest';
import { SECTOR_COUNT, TOKEN_RADIUS, sectorMidAngle, tokenPoint } from '../map-geometry';

/**
 * The island as the player already knows it: north at the top, sectors clockwise, and the shore
 * outside the forest outside the peaks.
 *
 * Asserted by quadrant and by ordering rather than by coordinate, because a coordinate test passes
 * on a map rotated a whole turn and reads as noise besides. What matters is that a token the
 * rulebook calls north is drawn at the top.
 */
describe('mapGeometry.tokenPoint', () => {
  it('Should_draw_the_north_sector_at_the_top_when_the_first_sector_is_asked_for', () => {
    // Arrange
    // Sector 0 is N, and its three tokens sit down the middle of the slice -- which is 22.5° east
    // of straight up, not straight up itself, because the sector spans the gap between two lines.
    const sector = 0;

    // Act
    const point = tokenPoint(sector, 'Beach');

    // Assert
    // y grows downwards in SVG, so the top of the screen is negative.
    expect(point.y).toBeLessThan(0);
    expect(Math.abs(point.y)).toBeGreaterThan(Math.abs(point.x));
    expect(point.x).toBeGreaterThan(0);
  });

  it('Should_run_clockwise_through_east_south_and_west_when_the_sectors_advance', () => {
    // Arrange
    // Sector order is N, NE, E, SE, S, SW, W, NW -- so 2, 4 and 6 are the other three cardinals.

    // Act
    const east = tokenPoint(2, 'Beach');
    const south = tokenPoint(4, 'Beach');
    const west = tokenPoint(6, 'Beach');

    // Assert
    expect(east.x).toBeGreaterThan(0);
    expect(Math.abs(east.x)).toBeGreaterThan(Math.abs(east.y));

    expect(south.y).toBeGreaterThan(0);
    expect(Math.abs(south.y)).toBeGreaterThan(Math.abs(south.x));

    expect(west.x).toBeLessThan(0);
    expect(Math.abs(west.x)).toBeGreaterThan(Math.abs(west.y));
  });

  it('Should_put_the_beach_outside_the_forest_outside_the_mountain_when_one_sector_is_drawn', () => {
    // Arrange
    // Concentric shore, forest and peak, matching the painted board. A player glancing between the
    // table and the screen should not have to translate.
    const sector = 3;

    // Act
    const distances = (['Beach', 'Forest', 'Mountain'] as const).map((terrain) => {
      const point = tokenPoint(sector, terrain);
      return Math.hypot(point.x, point.y);
    });

    // Assert
    expect(distances[0]).toBeGreaterThan(distances[1]);
    expect(distances[1]).toBeGreaterThan(distances[2]);
    expect(distances[0]).toBeCloseTo(TOKEN_RADIUS.Beach, 1);
  });

  it('Should_place_all_three_terrains_on_one_bearing_when_they_share_a_sector', () => {
    // Arrange
    // Sector k holds exactly Bk, Fk and Mk, so the three sit on one radial line and read as one
    // slice rather than as three unrelated tokens.
    const sector = 5;
    const expected = sectorMidAngle(sector);

    // Act
    const bearings = (['Beach', 'Forest', 'Mountain'] as const).map((terrain) => {
      const point = tokenPoint(sector, terrain);
      return (Math.atan2(point.y, point.x) * 180) / Math.PI;
    });

    // Assert
    for (const bearing of bearings) {
      expect(bearing).toBeCloseTo(expected, 1);
    }
  });

  it('Should_keep_every_token_inside_the_drawing_when_all_eight_sectors_are_laid_out', () => {
    // Arrange
    // The view box is 232 units across with the origin in the middle, and the compass letters live
    // outside the shore, so nothing on a token ring may reach the edge.
    const halfViewBox = 116;

    // Act
    const furthest = Array.from({ length: SECTOR_COUNT }, (_unused, sector) => {
      const point = tokenPoint(sector, 'Beach');
      return Math.hypot(point.x, point.y);
    }).reduce((most, next) => Math.max(most, next));

    // Assert
    expect(furthest).toBeLessThan(halfViewBox);
  });
});
