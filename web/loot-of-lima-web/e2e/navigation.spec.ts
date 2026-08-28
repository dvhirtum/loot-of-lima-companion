import { expect, test } from '@playwright/test';
import { accuse, deal, leave, openTable, sitDown, takeTokens } from './fixtures/table';

/**
 * Getting off the game page.
 *
 * There was no way. Once a phone had scanned the code it was on `/play/ABCD` for good: no link
 * home, no way to start another table, and nothing on the screen that ends a game to suggest what
 * to do next. Back was no way out either -- it went to the join form, which led straight back to
 * the same seat at the same table.
 *
 * Both links are plain navigation and neither disturbs the game — the seat is held by a token in
 * this browser's storage, so leaving and coming back to the same code sits back down. That is why
 * "Leave table" is safe to show at any moment, and why the finished banner is the one place a fresh
 * game is worth offering unprompted.
 */
test.describe('Leaving the table', () => {
  test('Should_reach_the_start_page_when_a_player_leaves_the_table', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    await ada.page.getByRole('link', { name: 'Leave table', exact: true }).click();

    // Assert
    await expect(ada.page.getByRole('heading', { name: 'Open a table' })).toBeVisible();
    await expect(ada.page.getByRole('link', { name: 'New game', exact: true })).toBeVisible();

    await leave([ada]);
  });

  test('Should_keep_the_seat_when_a_player_comes_back_to_the_same_table', async ({ browser }) => {
    // Arrange
    // The reason leaving needs no confirming. The seat lives in this browser's storage, so the link
    // is a navigation rather than a resignation.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await ada.page.getByRole('link', { name: 'Leave table', exact: true }).click();

    // Act
    await ada.page.goto(`/play/${roomCode}`);

    // Assert
    await expect(ada.page.getByRole('heading', { name: 'Your tokens', exact: true })).toBeVisible();
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    await leave([ada]);
  });

  test('Should_offer_a_fresh_game_when_this_one_is_over', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    await accuse(ada.page, 'B3', 'B5');

    // Assert
    await expect(ada.page.getByText('Ada wins.', { exact: true })).toBeVisible();
    await ada.page.getByRole('link', { name: 'New game', exact: true }).click();
    await expect(ada.page.getByRole('heading', { name: 'New game' })).toBeVisible();

    await leave([ada]);
  });
});
