import { Terrain } from './contracts';

/**
 * Presentation helpers for the location ids the server sends.
 *
 * Locations arrive as strings such as `B1`, `F6` and `M3`, so the terrain is the first character.
 * Reading it here is presentation and nothing more -- it decides what colour a token is drawn --
 * and it is emphatically not the start of a rules engine on the client. Anything the game turns on
 * arrives already decided in the view.
 */
const TERRAIN_BY_CODE: Readonly<Record<string, Terrain>> = {
  B: 'Beach',
  F: 'Forest',
  M: 'Mountain',
};

export function terrainOf(location: string): Terrain | null {
  return TERRAIN_BY_CODE[location.charAt(0).toUpperCase()] ?? null;
}

/** The modifier class for a token, or nothing when the id is not one this build knows. */
export function terrainClass(location: string): string {
  const terrain = terrainOf(location);
  return terrain === null ? '' : `token--${terrain.toLowerCase()}`;
}

/**
 * How a location is written on the physical tokens: the number first, so `B1` reads as `1B`.
 *
 * Presentation only, and the id itself is never rewritten. `B1` is what the server sends, what the
 * board layout and all 239 scenario files are keyed by, and what goes back on the wire in an
 * accusation, so the flip happens at the last possible moment -- on the way to a player's eyes.
 * A player reading `B1` on a screen next to a token stamped `1B` has to translate between the app
 * and the box, which is exactly the bookkeeping this app exists to take off them.
 *
 * Anything this does not recognise is handed back untouched rather than mangled into half a label.
 */
export function locationLabel(location: string): string {
  return terrainOf(location) === null ? location : `${location.slice(1)}${location.charAt(0).toUpperCase()}`;
}
