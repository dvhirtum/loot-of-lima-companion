import { describe, expect, it } from 'vitest';
import { TableUi } from '../table-ui';

/**
 * Naming locations by tapping the island.
 *
 * The map is the only way to name a location without reading it off a list, so the tapping has to
 * behave the way a finger expects: tap to name, tap again to unname, and keep working once two are
 * named. An accusation cannot be taken back, so the one thing this must never do is quietly hold on
 * to something the player thought they had let go of.
 */
describe('TableUi.togglePick', () => {
  it('Should_fill_the_first_empty_slot_when_a_location_is_tapped', () => {
    // Arrange
    const ui = new TableUi();

    // Act
    ui.togglePick('B1');

    // Assert
    expect(ui.picks()).toEqual(['B1', null]);
    expect(ui.named()).toEqual(['B1']);
    expect(ui.canAccuse()).toBe(false);
  });

  it('Should_complete_the_pair_when_a_second_location_is_tapped', () => {
    // Arrange
    const ui = new TableUi();
    ui.togglePick('B1');

    // Act
    ui.togglePick('M8');

    // Assert
    expect(ui.picks()).toEqual(['B1', 'M8']);
    expect(ui.canAccuse()).toBe(true);
  });

  it('Should_let_the_location_go_when_one_already_named_is_tapped_again', () => {
    // Arrange
    const ui = new TableUi();
    ui.togglePick('B1');
    ui.togglePick('M8');

    // Act
    ui.togglePick('B1');

    // Assert
    // The other one keeps its slot rather than sliding into the vacancy, so the select that owns
    // that slot does not change under the player's hand.
    expect(ui.picks()).toEqual([null, 'M8']);
    expect(ui.canAccuse()).toBe(false);
  });

  it('Should_roll_the_older_choice_off_when_a_third_location_is_tapped', () => {
    // Arrange
    // A third tap is somebody changing their mind. Refusing it would leave the map dead until they
    // found the token they no longer wanted and tapped it again.
    const ui = new TableUi();
    ui.togglePick('B1');
    ui.togglePick('M8');

    // Act
    ui.togglePick('F4');

    // Assert
    expect(ui.picks()).toEqual(['M8', 'F4']);
  });

  it('Should_name_two_different_locations_when_the_pair_is_complete', () => {
    // Arrange
    // The engine refuses an accusation naming one location twice, and tapping can never build one:
    // the second tap on a named token takes it away instead of doubling it.
    const ui = new TableUi();

    // Act
    ui.togglePick('B1');
    ui.togglePick('B1');
    ui.togglePick('B1');

    // Assert
    expect(ui.picks()).toEqual(['B1', null]);
    expect(ui.canAccuse()).toBe(false);
  });
});
