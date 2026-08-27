import { describe, expect, it } from 'vitest';
import { ARC_OUTER_RADIUS, arcPath, boundaryAngle, pointAt } from '../map-geometry';

/**
 * The highlighted wedge, as an SVG path.
 *
 * A path is a string, so these read the numbers back out of it rather than comparing whole strings:
 * an assertion on the exact text would break on a rounding change that moved nothing a player could
 * see, and would say nothing about the two flags that actually decide the shape.
 */
describe('mapGeometry.arcPath', () => {
  it('Should_start_on_the_outer_ring_at_the_starting_line_when_a_wedge_is_drawn', () => {
    // Arrange
    // Bounded by the compass lines rather than by the tokens: the dial's arrows point at the lines,
    // and the tokens sit in the gaps, which is what makes start-inclusive and end-exclusive fall
    // out of the picture instead of having to be drawn in.
    const start = 2;
    const expected = pointAt(boundaryAngle(start), ARC_OUTER_RADIUS);

    // Act
    const path = arcPath(start, 4, true);

    // Assert
    expect(path.startsWith(`M ${expected.x} ${expected.y} `)).toBe(true);
  });

  it('Should_take_the_short_way_round_when_the_wedge_is_half_the_island_or_less', () => {
    // Arrange
    // Two points on a circle are joined by both a short arc and a long one, and the large-arc flag
    // is the only thing that says which. Getting it wrong highlights the complement of the question.
    const start = 0;
    const end = 4;

    // Act
    const flags = arcFlags(arcPath(start, end, true));

    // Assert
    expect(flags).toEqual([0, 0]);
  });

  it('Should_take_the_long_way_round_when_the_wedge_is_more_than_half_the_island', () => {
    // Arrange
    const start = 0;
    const end = 7;

    // Act
    const flags = arcFlags(arcPath(start, end, true));

    // Assert
    expect(flags).toEqual([1, 1]);
  });

  it('Should_draw_a_closed_ring_when_the_arrows_coincide', () => {
    // Arrange
    // One elliptical arc cannot come back to where it started, so the full circle is four half
    // arcs in two subpaths -- the inner one swept the other way, so the middle is punched out
    // rather than filled.
    const start = 5;

    // Act
    const path = arcPath(start, start, true);

    // Assert
    expect(path.match(/M /g)).toHaveLength(2);
    expect(path.match(/A /g)).toHaveLength(4);
  });

  it('Should_draw_nothing_when_the_wedge_covers_no_sector_at_all', () => {
    // Arrange
    // Only reachable if the board ever said coincident arrows mean an empty arc. An empty `d` draws
    // nothing, which beats a stray dot at the centre of the island.
    const start = 5;

    // Act
    const path = arcPath(start, start, false);

    // Assert
    expect(path).toBe('');
  });
});

/** The large-arc flag of each `A` command, which is the fifth number in it. */
function arcFlags(path: string): number[] {
  return [...path.matchAll(/A (?:[-\d.]+) (?:[-\d.]+) 0 (\d) /g)].map((match) => Number(match[1]));
}
