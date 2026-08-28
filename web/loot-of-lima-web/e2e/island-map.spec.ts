import { expect, test } from '@playwright/test';
import {
  arcPaths,
  deal,
  field,
  leave,
  litOnMap,
  mapCaption,
  mapToken,
  mapTokens,
  openTable,
  sitDown,
  takeTokens,
} from './fixtures/table';

/**
 * The island, as a player looks at it.
 *
 * The map is about terrain. Three coloured bands and 24 coloured tokens, the same for everybody at
 * the table, plus a highlight showing which band and which slice the question being built covers.
 * It does not say whose a token is, and `IslandMap` could not say so if it wanted to: the component
 * injects the board layout and the ephemeral UI, and not `GameStore`. So the claim the old
 * per-viewer leak assertions were making is now made in one line — two players, one island, byte
 * for byte — and anything that reintroduced ownership would have to make the two differ.
 *
 * The highlight is here for a different reason. Coincident arrows sweeping the whole circle is the
 * surprising half of this game's geometry, and the map is where a player would first notice the app
 * disagreeing with the board in front of them.
 */
test.describe('The island map', () => {
  test('Should_colour_all_24_locations_by_terrain_when_a_scenario_is_dealt', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Assert
    // All 24 drawn, all the time. Where a location is and what it is made of are both printed on
    // the board in front of the player.
    expect(await mapTokens(ada.page)).toHaveLength(24);

    // One of Ada's, one of the treasure, one she has never been told a thing about — drawn alike,
    // because the only thing a disc's colour says is its terrain.
    await expect(mapToken(ada.page, 'B1').locator('.mark__disc')).toHaveClass(/mark__disc--beach/);
    await expect(mapToken(ada.page, 'B3').locator('.mark__disc')).toHaveClass(/mark__disc--beach/);
    await expect(mapToken(ada.page, 'F6').locator('.mark__disc')).toHaveClass(/mark__disc--forest/);
    await expect(mapToken(ada.page, 'M3').locator('.mark__disc')).toHaveClass(/mark__disc--mountain/);

    // Addressed by the server's id and drawn as the box writes it. The split is the whole of the
    // renaming: this is the one place both halves are visible in a single assertion, and if the
    // label ever went back to matching the id there would be nothing else to notice it.
    await expect(mapToken(ada.page, 'B1').locator('.mark__label')).toHaveText('1B');

    await leave([ada]);
  });

  test('Should_draw_an_identical_island_for_both_players_when_two_are_seated', async ({ browser }) => {
    // Arrange
    // Two contexts, because the seat lives in `localStorage` and two pages of one context are the
    // same person twice. Book 1, scenario 1 deals Ada B1 B2 F6 M3 and Grace F1 F2 F3 F8 — two
    // hands with nothing in common, so a map that knew either of them could not come back equal.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);

    // Assert
    const island = await mapTokens(ada.page);
    expect(island).toHaveLength(24);
    expect(await mapTokens(grace.page)).toEqual(island);

    // And the same to a screen reader, which is the one place a map can leak without being seen to.
    expect(await mapCaption(grace.page)).toEqual(await mapCaption(ada.page));

    await leave([ada, grace]);
  });

  test('Should_light_every_band_and_every_token_when_the_two_arrows_are_set_to_one_direction', async ({ browser }) => {
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

    // No terrain chosen, so the question is about all three: one segment per band.
    await expect(arcPaths(ada.page)).toHaveCount(3);

    // A closed ring is two subpaths: one arc cannot return to where it started.
    const ring = await arcPaths(ada.page).first().getAttribute('d');
    expect(ring?.match(/M /g)).toHaveLength(2);

    expect(await litOnMap(ada.page)).toHaveLength(24);

    await leave([ada]);
  });

  test('Should_light_one_band_and_its_own_tokens_when_the_question_names_a_terrain', async ({ browser }) => {
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

    const wedge = arcPaths(ada.page);
    await expect(wedge).toHaveCount(1);
    await expect(wedge).toHaveAttribute('data-terrain', 'Forest');
    expect((await wedge.getAttribute('d'))?.match(/M /g)).toHaveLength(1);

    // The segment and the tokens are two drawings of one range, so they are asserted together:
    // N up to SE is the first three sectors, and on the forest band that is F1, F2 and F3.
    expect((await litOnMap(ada.page)).sort()).toEqual(['F1', 'F2', 'F3']);

    await leave([ada]);
  });
});
