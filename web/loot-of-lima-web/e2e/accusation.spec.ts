import { expect, test } from '@playwright/test';
import {
  accuse,
  askQuestion,
  deal,
  leave,
  mapTokenState,
  openTable,
  ownHand,
  sitDown,
  takeTokens,
} from './fixtures/table';

/**
 * Naming the treasure.
 *
 * The rulebook is emphatic that a wrong accusation reveals *nothing*, and it is worth being just as
 * emphatic here: a pair ruled out is a real deduction, so the two locations somebody named have to
 * stay off every other player's screen. That needs two contexts, and it needs the whole round trip
 * — the projection sends a different entry type, the store keeps it, and the log has to render it
 * as its own case rather than as the full one with holes in it.
 *
 * Three humans, and not two, wherever the game has to survive the accusation. Eliminating one of
 * two leaves a single player standing and the engine ends the game there and then, so a two-handed
 * table can show a wrong accusation but never what happens *after* one.
 *
 * The treasure of book 1, scenario 1 is B3 and B5, which is what makes the winning half of this
 * testable at all: a shuffled deal is seeded from `Random.Shared.Next()` and repeats nothing.
 */
test.describe('Accusing', () => {
  test('Should_reveal_nothing_and_leave_the_accuser_answering_when_the_accusation_is_wrong', async ({ browser }) => {
    // Arrange
    // Ada names two of her own tokens, which she knows perfectly well are not the treasure. That is
    // the only way to be sure a spec is testing a wrong accusation rather than a lucky one.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    const hedy = await sitDown(browser, roomCode, 'Hedy', 2);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace, hedy]);
    expect(await ownHand(ada.page)).toEqual(['1B', '2B', '6F', '3M']);

    // Act
    await accuse(ada.page, 'B1', 'B2');

    // Assert
    // Ada sees what she named, because she named it.
    await expect(ada.page.getByText('You are out.', { exact: true })).toBeVisible();
    await expect(ada.page.locator('.log .entry').first()).toContainText('and was wrong');
    await expect(ada.page.locator('.log .entry').first()).toContainText('1B');

    // Grace is told it happened and nothing else. The entry she receives has no property for the
    // pair, so there is nowhere on this line for one to appear. Named the way the log writes them:
    // asserting the absence of `B1` would pass for the wrong reason, since no log line says `B1`.
    const seen = grace.page.locator('.log .entry').first();
    await expect(seen).toContainText('Ada accused, and was wrong. Nothing was revealed.');
    await expect(seen).not.toContainText('1B');
    await expect(seen).not.toContainText('2B');
    expect(await mapTokenState(grace.page, 'B1')).toBe('unknown');

    // Out, but still answerable: the turn has passed to Grace and Ada owes her an answer from the
    // hand she can no longer do anything with. Ada holds B1 and B2, so a beach question sweeping
    // the whole island comes back 2.
    await expect(grace.page.getByText('Your turn', { exact: true })).toBeVisible();
    await askQuestion(grace.page, { of: 'Ada', from: 'N', upTo: 'N', terrain: 'Beach' });
    await expect(grace.page.locator('.log .entry').first().locator('.answer')).toHaveText('2');

    await leave([ada, grace, hedy]);
  });

  test('Should_be_open_to_a_player_whose_turn_it_is_not_when_the_game_is_running', async ({ browser }) => {
    // Arrange
    // Accusation is legal at any moment, including somebody else's turn. That is a rule which would
    // be invisible if the only route to it were the turn panel, so the panel sits outside it.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    const hedy = await sitDown(browser, roomCode, 'Hedy', 2);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace, hedy]);
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    // Act
    // Grace's own tokens, so this is a wrong accusation made out of turn rather than a win.
    await accuse(grace.page, 'F1', 'F2');

    // Assert
    await expect(grace.page.getByText('You are out.', { exact: true })).toBeVisible();

    // Being eliminated ends your turn, but only your turn — and this one was never Grace's to end.
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();
    await expect(ada.page.locator('.log .entry').first()).toContainText('Grace accused, and was wrong');

    await leave([ada, grace, hedy]);
  });

  test('Should_show_the_treasure_only_once_somebody_has_named_it', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // The field the whole design exists to protect. It is in the server's state from the deal, and
    // null in every projection until this moment.
    expect(await mapTokenState(ada.page, 'B3')).toBe('unknown');
    await expect(ada.page.getByRole('heading', { name: 'The treasure' })).toHaveCount(0);

    // Act
    await accuse(ada.page, 'B3', 'B5');

    // Assert
    await expect(ada.page.getByText('Ada wins.', { exact: true })).toBeVisible();
    await expect(ada.page.getByRole('heading', { name: 'The treasure' })).toBeVisible();
    expect(await mapTokenState(ada.page, 'B3')).toBe('treasure');
    expect(await mapTokenState(ada.page, 'B5')).toBe('treasure');

    await leave([ada]);
  });
});
