import { describe, expect, it } from 'vitest';
import { locationLabel } from '../locations';

/**
 * Writing a location the way the physical game writes it.
 *
 * The box stamps the number first, so `B1` is `1B` on the token in a player's hand. The id itself
 * is never rewritten: the server sends `B1`, the board layout and all 239 scenario files are keyed
 * by it, and an accusation puts it back on the wire. This flips it on the way to a player's eyes
 * and nowhere else.
 */
describe('locations.locationLabel', () => {
  it('Should_put_the_number_first_when_given_one_of_the_twenty_four_locations', () => {
    // Arrange, Act, Assert
    expect(locationLabel('B1')).toBe('1B');
    expect(locationLabel('F6')).toBe('6F');
    expect(locationLabel('M8')).toBe('8M');
  });

  it('Should_write_the_terrain_in_upper_case_when_the_id_is_lower_case', () => {
    // Arrange
    // The letter is a label rather than data, so it should read the same however it arrived.

    // Act, Assert
    expect(locationLabel('m3')).toBe('3M');
  });

  it('Should_hand_the_string_back_when_it_is_not_a_location_this_build_knows', () => {
    // Arrange
    // Half a label is worse than an unrecognised one: `X1` shown as `1X` claims the app understood
    // something it did not, and there is no token on the table stamped that way.

    // Act, Assert
    expect(locationLabel('X1')).toBe('X1');
    expect(locationLabel('')).toBe('');
  });
});
