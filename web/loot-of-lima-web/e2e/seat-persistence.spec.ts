import { expect, test } from '@playwright/test';
import { deal, faceUpTokens, leave, openTable, ownHand, seatCard, sitDown, takeTokens } from './fixtures/table';

/**
 * Getting the seat back.
 *
 * This is a phone on a table, so the page goes away constantly -- a screen lock, a notification, a
 * tab swapped to look something up. Losing the seat to any of that would mean losing the hand,
 * which cannot be dealt again. Nothing else in the repository exercises the reload: the identity
 * store's own tests know what `localStorage` holds, and no unit test can say the app comes back up
 * around it.
 */
test.describe('Coming back to a table', () => {
  test('Should_still_be_holding_the_hand_when_the_page_is_reloaded_mid_game', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    const before = await ownHand(ada.page);

    // Act
    await ada.page.reload();

    // Assert
    await expect(ada.page.getByText('InProgress')).toBeVisible();
    await expect(seatCard(ada.page, 0).getByText('you', { exact: true })).toBeVisible();
    expect(await ownHand(ada.page)).toEqual(before);

    await leave([ada]);
  });

  test('Should_serve_the_game_when_a_play_url_is_opened_directly', async ({ browser }) => {
    // Arrange
    // Every route is client-side, so `/play/ABCD` is not a file. Under the dev server this is the
    // proxy's fallback and under nginx it is `try_files ... /index.html`; in the compose mode this
    // spec is the only thing that would notice the SPA fallback going missing and turning a
    // refreshed game into a 404.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    const response = await ada.page.goto(`/play/${roomCode}`);

    // Assert
    expect(response?.status()).toBe(200);
    await expect(ada.page.getByRole('heading', { name: 'Loot of Lima' })).toBeVisible();
    await expect(seatCard(ada.page, 0).getByText('you', { exact: true })).toBeVisible();

    await leave([ada]);
  });

  test('Should_watch_without_a_seat_when_a_browser_has_never_joined', async ({ browser }) => {
    // Arrange
    // Every arrival lands here -- the QR code and the host screen's own button both open the table
    // -- so a page with no token is the ordinary case rather than an edge one. What makes this
    // browser a watcher is only that nobody has tapped a seat in it.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    const watcher = await browser.newContext();
    const watching = await watcher.newPage();
    await watching.goto(`/play/${roomCode}`);

    // Assert
    await expect(watching.getByText('You are watching this table without a seat.')).toBeVisible();

    // Ada's hand is checked before it is used. The loop below proves something is *absent*, so an
    // empty list would satisfy it without testing anything, and a hand read a moment too early is
    // an empty list. This is the assertion that stops the next one passing for free.
    const adasHand = await ownHand(ada.page);
    expect(adasHand).toHaveLength(4);

    for (const token of adasHand) {
      await expect(watching.locator('.token', { hasText: token })).toHaveCount(0);
    }

    // The watcher is not simply looking at a blank page: the face-up locations are public and
    // really are on screen, so the absence above is redaction rather than an empty render.
    expect(await faceUpTokens(watching)).toHaveLength(2);

    await watcher.close();
    await leave([ada]);
  });
});
