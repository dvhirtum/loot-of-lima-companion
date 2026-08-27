import { expect, test } from '@playwright/test';
import {
  accuse,
  askQuestion,
  deal,
  giveUpButton,
  leave,
  mapTokenState,
  openTable,
  ownHand,
  sitDown,
  takeTokens,
} from './fixtures/table';

/**
 * A table of one, which is the shape the printed scenario books are written for.
 *
 * Everywhere else a wrong accusation puts you out of the game. Here it cannot: eliminating the only
 * player finishes the game, and Finished is the single phase in which the treasure is projected, so
 * the punishment for guessing wrong would be being handed the answer. The rulebook penalty is not
 * available at this table size, so the game simply carries on and the pair is ruled out.
 *
 * That leaves one thing that has to exist and one thing that has to not exist yet. Somebody who
 * cannot solve it needs a way to see the answer, and that way must not be a button sitting there
 * from the first turn — it appears only once a wrong accusation is behind you, so giving up costs
 * what giving up costs: you had to have committed to a pair and found out it was not the pair.
 *
 * Book 1, scenario 1: Ada holds B1, B2, F6 and M3, and the treasure is B3 and B5. Named because a
 * generated deal is seeded from `Random.Shared.Next()` and repeats nothing, and because naming two
 * of your own tokens is the only way to be sure a spec tests a wrong accusation and not a lucky one.
 */
test.describe('Playing on alone', () => {
  test('Should_stay_in_the_game_when_the_only_player_accuses_wrongly', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    expect(await ownHand(ada.page)).toEqual(['B1', 'B2', 'F6', 'M3']);

    // Act
    await accuse(ada.page, 'B1', 'B2');

    // Assert
    await expect(ada.page.locator('.log .entry').first()).toContainText('and was wrong');
    await expect(ada.page.getByText('You are out.', { exact: true })).toHaveCount(0);
    await expect(ada.page.getByText('Nobody wins.', { exact: true })).toHaveCount(0);

    // The whole reason for not ending it. Both halves of the answer are still nothing on the map.
    await expect(ada.page.getByRole('heading', { name: 'The treasure' })).toHaveCount(0);
    expect(await mapTokenState(ada.page, 'B3')).toBe('unknown');
    expect(await mapTokenState(ada.page, 'B5')).toBe('unknown');

    // Still playing, and still asking. Ada holds B1 and B2, so a beach question sweeping the whole
    // island comes back 2 — from the same hand, at the same table, one turn later.
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();
    await askQuestion(ada.page, { of: 'Bot 3', from: 'N', upTo: 'N', terrain: 'Beach' });
    await expect(ada.page.locator('.log .entry').first().locator('.answer')).toBeVisible();

    await leave([ada]);
  });

  test('Should_still_win_when_the_only_player_gets_it_right_at_the_second_attempt', async ({ browser }) => {
    // Arrange
    // Carrying on has to mean carrying on to the ordinary ending, not to some consolation one.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await accuse(ada.page, 'B1', 'B2');

    // Act
    await accuse(ada.page, 'B3', 'B5');

    // Assert
    await expect(ada.page.getByText('Ada wins.', { exact: true })).toBeVisible();
    expect(await mapTokenState(ada.page, 'B3')).toBe('treasure');

    await leave([ada]);
  });

  test('Should_offer_no_way_to_see_the_treasure_until_a_pair_has_been_named_and_missed', async ({ browser }) => {
    // Arrange
    // The one control in this app whose whole purpose is to reveal the treasure. A puzzle nobody
    // has attempted yet has nothing to give up on, only an answer to spoil.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();
    await expect(giveUpButton(ada.page)).toHaveCount(0);

    // Act
    await accuse(ada.page, 'B1', 'B2');

    // Assert
    await expect(giveUpButton(ada.page)).toBeVisible();

    await leave([ada]);
  });

  test('Should_end_the_game_and_show_the_treasure_when_the_only_player_gives_up', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await accuse(ada.page, 'B1', 'B2');

    // Act
    await giveUpButton(ada.page).click();

    // Assert
    await expect(ada.page.getByText('You gave up. Here is the treasure.', { exact: true })).toBeVisible();
    await expect(ada.page.getByRole('heading', { name: 'The treasure' })).toBeVisible();
    expect(await mapTokenState(ada.page, 'B3')).toBe('treasure');
    expect(await mapTokenState(ada.page, 'B5')).toBe('treasure');

    await leave([ada]);
  });

  test('Should_offer_no_way_to_give_up_when_a_second_human_is_at_the_table', async ({ browser }) => {
    // Arrange
    // Two humans have the rulebook's own way out: accusing wrongly puts you out and leaves the
    // other one playing. Conceding there would end somebody else's game as well.
    //
    // Three seated, because eliminating one of two ends the game outright and there would be no
    // running game left to look for the button in.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    const hedy = await sitDown(browser, roomCode, 'Hedy', 2);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace, hedy]);

    // Act
    await accuse(ada.page, 'B1', 'B2');

    // Assert
    await expect(ada.page.getByText('You are out.', { exact: true })).toBeVisible();
    await expect(giveUpButton(ada.page)).toHaveCount(0);
    await expect(giveUpButton(grace.page)).toHaveCount(0);

    await leave([ada, grace, hedy]);
  });
});
