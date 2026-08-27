import { expect, test } from '@playwright/test';
import { arcPath, deal, field, knownOnMap, leave, mapTokenState, openTable, sitDown, takeTokens } from './fixtures/table';

/**
 * The island, as a player looks at it.
 *
 * The map is the one component that draws every location in the game whether or not the viewer
 * knows anything about it, which makes it the easiest place in this app to leak a hand by accident:
 * one wrong class and somebody else's tokens light up. `Server.Tests` cannot see this at all --
 * every id is legitimately in the frame, because the map is built from the public board layout --
 * so the DOM is the only place the claim can be made.
 *
 * The wedge is here for a different reason. Coincident arrows sweeping the whole circle is the
 * surprising half of this game's geometry, and the map is where a player would first notice the app
 * disagreeing with the board in front of them.
 */
test.describe('The island map', () => {
  test('Should_mark_only_the_hand_and_the_reveal_when_a_scenario_is_dealt', async ({ browser }) => {
    // Arrange
    // Book 1, scenario 1 deals seat 1 B1 B2 F6 M3, turns B8 and M6 face up, and keeps B3 and B5 as
    // the treasure. Six of the 24 are knowable, and the two that matter most are not among them.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Assert
    expect((await knownOnMap(ada.page)).sort()).toEqual(['B1', 'B2', 'B8', 'F6', 'M3', 'M6']);

    expect(await mapTokenState(ada.page, 'B1')).toBe('mine');
    expect(await mapTokenState(ada.page, 'B8')).toBe('revealed');

    // The whole design in one assertion. The treasure is in the server's state from the moment of
    // the deal, and every other location on this map is drawn from a list this client was sent.
    expect(await mapTokenState(ada.page, 'B3')).toBe('unknown');
    expect(await mapTokenState(ada.page, 'B5')).toBe('unknown');

    await leave([ada]);
  });

  test('Should_leave_the_other_player_hand_unknown_when_two_are_seated', async ({ browser }) => {
    // Arrange
    // Two contexts, because the seat lives in `localStorage` and two pages of one context are the
    // same person twice. Grace is dealt F1 F2 F3 F8 by the book.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);

    // Assert
    for (const location of ['F1', 'F2', 'F3', 'F8']) {
      expect(await mapTokenState(ada.page, location), `Grace's ${location} is lit on Ada's map`).toBe('unknown');
    }

    for (const location of ['B1', 'B2', 'F6', 'M3']) {
      expect(await mapTokenState(grace.page, location), `Ada's ${location} is lit on Grace's map`).toBe('unknown');
    }

    // Each still sees their own, so the check above is not passing because the map is blank.
    expect((await knownOnMap(grace.page)).sort()).toEqual(['B8', 'F1', 'F2', 'F3', 'F8', 'M6']);

    await leave([ada, grace]);
  });

  test('Should_sweep_the_whole_island_when_the_two_arrows_are_set_to_one_direction', async ({ browser }) => {
    // Arrange
    // The dial's arrows point at the eight compass lines rather than at sectors, so two arrows on
    // one line leave every token between them. A map that drew nothing here — or a single sliver —
    // would quietly contradict the 244,736 published answers the engine is checked against.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    await field(ada.page, 'From').selectOption('N');
    await field(ada.page, 'Up to').selectOption('N');

    // Assert
    await expect(ada.page.getByText('The whole island')).toBeVisible();

    // A closed ring is two subpaths: one arc cannot return to where it started.
    const ring = await arcPath(ada.page).getAttribute('d');
    expect(ring?.match(/M /g)).toHaveLength(2);

    await leave([ada]);
  });

  test('Should_draw_one_wedge_when_the_arrows_are_set_to_different_directions', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    await field(ada.page, 'From').selectOption('N');
    await field(ada.page, 'Up to').selectOption('SE');
    await field(ada.page, 'Terrain').selectOption('Forest');

    // Assert
    await expect(ada.page.getByText('N up to SE')).toBeVisible();
    await expect(ada.page.getByText('forest only')).toBeVisible();

    const wedge = await arcPath(ada.page).getAttribute('d');
    expect(wedge?.match(/M /g)).toHaveLength(1);

    await leave([ada]);
  });
});
