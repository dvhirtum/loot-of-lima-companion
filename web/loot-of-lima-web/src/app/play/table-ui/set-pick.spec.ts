import { describe, expect, it } from 'vitest';
import { TableUi } from '../table-ui';

/**
 * Naming locations from the two selects.
 *
 * The selects and the map write to the same pair, which is why the pair has fixed slots: a select
 * owns its slot outright, and a list that closed up its gaps would move the second choice into the
 * first select the moment the first was cleared.
 */
describe('TableUi.setPick', () => {
  it('Should_fill_the_slot_it_was_given_when_a_location_is_chosen', () => {
    // Arrange
    const ui = new TableUi();

    // Act
    ui.setPick(1, 'F4');

    // Assert
    // The second slot, with the first still empty. A list would have made this the first.
    expect(ui.picks()).toEqual([null, 'F4']);
    expect(ui.canAccuse()).toBe(false);
  });

  it('Should_empty_the_slot_when_the_choice_is_cleared', () => {
    // Arrange
    const ui = new TableUi();
    ui.setPick(0, 'B1');
    ui.setPick(1, 'F4');

    // Act
    ui.setPick(0, null);

    // Assert
    expect(ui.picks()).toEqual([null, 'F4']);
  });

  it('Should_give_up_the_other_slot_when_the_same_location_is_chosen_twice', () => {
    // Arrange
    // The engine refuses an accusation that names one location twice. Rather than leave a pair the
    // server would only refuse, the older slot lets go — the player has plainly changed their mind
    // about where that location belongs.
    const ui = new TableUi();
    ui.setPick(0, 'B1');
    ui.setPick(1, 'F4');

    // Act
    ui.setPick(1, 'B1');

    // Assert
    expect(ui.picks()).toEqual([null, 'B1']);
    expect(ui.canAccuse()).toBe(false);
  });

  it('Should_be_ready_to_accuse_when_both_slots_name_different_locations', () => {
    // Arrange
    const ui = new TableUi();

    // Act
    ui.setPick(0, 'B1');
    ui.setPick(1, 'M8');

    // Assert
    expect(ui.canAccuse()).toBe(true);
    expect(ui.named()).toEqual(['B1', 'M8']);
  });
});
