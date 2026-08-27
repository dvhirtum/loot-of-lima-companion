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
