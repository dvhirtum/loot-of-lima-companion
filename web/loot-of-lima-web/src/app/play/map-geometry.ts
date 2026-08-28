import { Terrain } from '../core/contracts';

/**
 * Where things go on the island, in SVG user units.
 *
 * Pure arithmetic about a picture, and deliberately nothing more. Nothing here counts a token,
 * decides an answer or reads a hand -- it turns a sector index into a place to draw, which is
 * presentation in exactly the way reading the terrain letter off a location id is. The sector a
 * location belongs to is never worked out here either: it arrives from `GET /api/board`, the same
 * JSON the engine loads, so the map and the rules cannot disagree about where anything is.
 *
 * It lives in its own file so it can be tested without a browser. Geometry that is wrong by a
 * quarter turn looks plausible in a screenshot and is obvious in an assertion.
 */

export interface Point {
  readonly x: number;
  readonly y: number;
}

/** The eight sectors, each a 45° slice. */
export const SECTOR_SPAN = 45;

export const SECTOR_COUNT = 8;

/**
 * The line each terrain's tokens sit on, which is also the middle of that terrain's band.
 *
 * Shore outermost, then forest, then the peaks in the middle, which is how the real board is
 * painted -- a player looking from the physical island to the screen should not have to translate.
 */
export const TOKEN_RADIUS: Readonly<Record<Terrain, number>> = {
  Beach: 91,
  Forest: 73,
  Mountain: 55,
};

/** Small enough that two bands' tokens do not touch: the rings are 18 apart, so this leaves one. */
export const TOKEN_SIZE = 8.5;

/**
 * Half a band's width, which is also half the distance between two token rings.
 *
 * So the three bands meet exactly and the island is one solid disc of terrain rather than three
 * hoops with sea showing between them. The map's subject is the terrain, and terrain has no gaps.
 */
export const BAND_HALF_WIDTH = 9;

/** An annulus: the two radii `arcPath` wants, and the two an SVG ring is drawn from. */
export interface Band {
  readonly inner: number;
  readonly outer: number;
}

/**
 * Where each terrain is painted, centred on the line its own tokens sit on.
 *
 * Derived from `TOKEN_RADIUS` rather than written out a second time. A band whose tokens sat off
 * its own centre would be a picture disagreeing with itself, and nothing but an eye would catch it.
 */
export const BAND_RADIUS: Readonly<Record<Terrain, Band>> = {
  Beach: bandAround(TOKEN_RADIUS.Beach),
  Forest: bandAround(TOKEN_RADIUS.Forest),
  Mountain: bandAround(TOKEN_RADIUS.Mountain),
};

/** The island itself: the inside of the peaks out to the far edge of the shore. */
export const ISLAND_INNER_RADIUS = BAND_RADIUS.Mountain.inner;
export const ISLAND_OUTER_RADIUS = BAND_RADIUS.Beach.outer;

/** Just beyond the shore, at the far end of a compass line. */
export const COMPASS_RADIUS = 108;

/** Room for the compass letters on every side. */
export const VIEW_BOX = '-116 -116 232 232';

/**
 * The screen angle of the line between sector `k - 1` and sector `k`.
 *
 * North is straight up, which is −90° in SVG's coordinates, and angles increase clockwise because
 * y points down. So sector indices, compass order and increasing angle all run the same way.
 */
export function boundaryAngle(sectorIndex: number): number {
  return sectorIndex * SECTOR_SPAN - 90;
}

/** The angle down the middle of a sector, which is where its three tokens sit. */
export function sectorMidAngle(sectorIndex: number): number {
  return boundaryAngle(sectorIndex) + SECTOR_SPAN / 2;
}

export function pointAt(angleDegrees: number, radius: number): Point {
  const radians = (angleDegrees * Math.PI) / 180;
  return { x: round(Math.cos(radians) * radius), y: round(Math.sin(radians) * radius) };
}

export function tokenPoint(sectorIndex: number, terrain: Terrain): Point {
  return pointAt(sectorMidAngle(sectorIndex), TOKEN_RADIUS[terrain]);
}

/**
 * How many 45° slices the wedge covers, from 1 to 8.
 *
 * `fullCircleWhenEqual` is not a guess: it comes from the `arcSemantics` the board endpoint serves,
 * which is the same file `BoardLayout.Load()` reads. Coincident arrows sweeping the whole circle is
 * the surprising half of this game's geometry, and it would be a poor thing for the map to have its
 * own opinion about.
 */
export function sectorsCovered(startIndex: number, endIndex: number, fullCircleWhenEqual: boolean): number {
  const span = (((endIndex - startIndex) % SECTOR_COUNT) + SECTOR_COUNT) % SECTOR_COUNT;

  if (span !== 0) {
    return span;
  }

  return fullCircleWhenEqual ? SECTOR_COUNT : 0;
}

/**
 * Whether one sector falls inside the wedge, so its tokens can be lit along with it.
 *
 * This decides what is *drawn brightly* and nothing else. It never counts, never sums and is never
 * asked how many tokens it found: the answer to a question comes from the server like every other
 * answer, and a client that could total the tokens in an arc would be the beginning of the rules
 * engine this app deliberately does not have on the client.
 *
 * Measured as an offset clockwise from the start against the same `sectorsCovered` the wedge is
 * drawn from, so the shape and the lit tokens cannot disagree about where the arc ends.
 */
export function sectorInRange(
  sectorIndex: number,
  startIndex: number,
  endIndex: number,
  fullCircleWhenEqual: boolean,
): boolean {
  const offset = (((sectorIndex - startIndex) % SECTOR_COUNT) + SECTOR_COUNT) % SECTOR_COUNT;

  return offset < sectorsCovered(startIndex, endIndex, fullCircleWhenEqual);
}

/**
 * The `d` of one band's highlighted segment: an annulus swept clockwise from one compass line to
 * another, between that band's own two radii.
 *
 * Drawn line to line rather than token to token, and that is the whole reason the picture is worth
 * having. The dial's two arrows point at the eight compass *lines*, and the tokens sit in the gaps
 * between them, so a wedge bounded by the lines contains exactly the tokens the question asks about
 * -- start included, end excluded -- without this file ever having to know that rule.
 *
 * `inner` and `outer` have no defaults on purpose. A question is asked about one terrain or about
 * all three, and the map says which by lighting one band or three. A wedge that spanned every ring
 * by default would be a fourth shape, saying something no question ever says.
 */
export function arcPath(
  startIndex: number,
  endIndex: number,
  fullCircleWhenEqual: boolean,
  inner: number,
  outer: number,
): string {
  const sectors = sectorsCovered(startIndex, endIndex, fullCircleWhenEqual);

  if (sectors === 0) {
    return '';
  }

  if (sectors === SECTOR_COUNT) {
    return ring(inner, outer);
  }

  const from = boundaryAngle(startIndex);
  const to = from + sectors * SECTOR_SPAN;

  // More than half the circle is the one case an SVG arc cannot infer, since two points are joined
  // by both a short way round and a long one.
  const large = sectors > SECTOR_COUNT / 2 ? 1 : 0;

  const outerFrom = pointAt(from, outer);
  const outerTo = pointAt(to, outer);
  const innerTo = pointAt(to, inner);
  const innerFrom = pointAt(from, inner);

  return [
    `M ${outerFrom.x} ${outerFrom.y}`,
    `A ${outer} ${outer} 0 ${large} 1 ${outerTo.x} ${outerTo.y}`,
    `L ${innerTo.x} ${innerTo.y}`,
    `A ${inner} ${inner} 0 ${large} 0 ${innerFrom.x} ${innerFrom.y}`,
    'Z',
  ].join(' ');
}

/**
 * A closed ring, for coincident arrows.
 *
 * Two circles' worth of half-arcs rather than one, because a single elliptical arc cannot return to
 * where it started. The inner one is swept the other way so the nonzero fill rule punches it out
 * instead of filling the whole disc.
 */
function ring(inner: number, outer: number): string {
  return [halfArcs(outer, 1), halfArcs(inner, 0)].join(' ');
}

function halfArcs(radius: number, sweep: 0 | 1): string {
  const top = pointAt(-90, radius);
  const bottom = pointAt(90, radius);

  return [
    `M ${top.x} ${top.y}`,
    `A ${radius} ${radius} 0 0 ${sweep} ${bottom.x} ${bottom.y}`,
    `A ${radius} ${radius} 0 0 ${sweep} ${top.x} ${top.y}`,
    'Z',
  ].join(' ');
}

function bandAround(radius: number): Band {
  return { inner: radius - BAND_HALF_WIDTH, outer: radius + BAND_HALF_WIDTH };
}

/** Two decimals is finer than a pixel at any size this is drawn, and keeps the path readable. */
function round(value: number): number {
  return Math.round(value * 100) / 100;
}
