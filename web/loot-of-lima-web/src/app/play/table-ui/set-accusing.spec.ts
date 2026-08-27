import { describe, expect, it } from 'vitest';
import { TableUi } from '../table-ui';

/**
 * Opening and closing the accusation panel.
 *
 * The panel is the one place in this app where a misclick costs the game, so what it forgets
 * matters as much as what it remembers.
 */
describe('TableUi.setAccusing', () => {
  it('Should_start_from_an_empty_pair_when_the_panel_is_opened', () => {
    // Arrange
    // Reopening on locations chosen minutes ago and forgotten is how somebody accuses with one they
    // meant and one they do not remember choosing — and a wrong accusation cannot be taken back.
    const ui = new TableUi();
    ui.togglePick('B1');
    ui.setAccusing(true);
    ui.setAccusing(false);

    // Act
    ui.setAccusing(true);

    // Assert
    expect(ui.accusing()).toBe(true);
    expect(ui.picks()).toEqual([null, null]);
  });

  it('Should_forget_the_pair_when_the_panel_is_closed', () => {
    // Arrange
    const ui = new TableUi();
    ui.setAccusing(true);
    ui.togglePick('B1');
    ui.togglePick('M8');

    // Act
    ui.setAccusing(false);

    // Assert
    expect(ui.accusing()).toBe(false);
    expect(ui.named()).toEqual([]);
  });

  it('Should_be_closed_and_empty_when_the_page_has_just_opened', () => {
    // Arrange
    const ui = new TableUi();

    // Act
    const accusing = ui.accusing();

    // Assert
    expect(accusing).toBe(false);
    expect(ui.picks()).toEqual([null, null]);
    expect(ui.highlight()).toBeNull();
  });
});
