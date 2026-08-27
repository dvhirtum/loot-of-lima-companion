import { describe, expect, it } from 'vitest';
import { terrainClass, terrainOf } from '../locations';

/**
 * Reading the terrain off a location id.
 *
 * Presentation only: it decides what colour a token is drawn. Nothing the game turns on is
 * computed here, and nothing here is allowed to grow into a rules engine on the client.
 */
describe('locations.terrainOf', () => {
  it('Should_name_the_terrain_when_given_one_of_the_twenty_four_locations', () => {
    // Arrange, Act, Assert
    expect(terrainOf('B1')).toBe('Beach');
    expect(terrainOf('F6')).toBe('Forest');
    expect(terrainOf('M8')).toBe('Mountain');
  });

  it('Should_name_the_terrain_when_the_id_is_lower_case', () => {
    // Arrange, Act, Assert
    expect(terrainOf('m3')).toBe('Mountain');
  });

  it('Should_name_nothing_when_the_id_is_not_one_this_build_knows', () => {
    // Arrange
    // A location the server invented, or a string that is not one at all. Guessing a terrain would
    // colour it as a real token, which is worse than drawing it plain.

    // Act, Assert
    expect(terrainOf('X1')).toBeNull();
    expect(terrainOf('')).toBeNull();
  });

  it('Should_give_no_modifier_class_when_the_terrain_is_unknown', () => {
    // Arrange, Act, Assert
    expect(terrainClass('B1')).toBe('token--beach');
    expect(terrainClass('X1')).toBe('');
  });
});
