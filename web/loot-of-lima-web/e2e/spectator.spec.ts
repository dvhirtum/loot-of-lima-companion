import { expect, test } from '@playwright/test';
import { deal, leave, openTable, seatCard, sitDown, takeTokens } from './fixtures/table';

/**
 * Watching a table without a seat.
 *
 * The host screen links straight here, so arriving as a spectator is a normal route rather than a
 * contrived one — and until now every open seat offered a "Sit here" button that could only ever
 * come back refused, because the hub will not take `ClaimSeat` from somebody who has not joined and
 * joining happens at `/join`, behind the QR code.
 *
 * The obvious fix is the wrong one, and that is why both halves are here. Gating the button on
 * "has no seat" would take it away from the player who has just come through the QR code and has
 * not sat down yet, which is the single most common state anybody is ever in on this page. The
 * question is whether this browser holds a token, not whether it holds a seat.
 */
test.describe('Watching a table', () => {
  test('Should_offer_the_way_in_rather_than_a_seat_when_the_watcher_has_not_joined', async ({ browser }) => {
    // Arrange
    // No `sitDown`, so this context has no player token for this table at all.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const watcher = await browser.newContext();
    const page = await watcher.newPage();

    // Act
    await page.goto(`/play/${roomCode}`);

    // Assert
    await expect(seatCard(page, 0)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sit here' })).toHaveCount(0);

    const join = page.getByRole('link', { name: 'Join to sit here' }).first();
    await expect(join).toBeVisible();
    await join.click();
    await page.waitForURL(new RegExp(`/join/${roomCode}$`));

    await watcher.close();
  });

  test('Should_offer_every_open_seat_when_the_player_has_joined_but_not_sat_down', async ({ browser }) => {
    // Arrange
    // A joined player has a token and no seat, which is exactly the state a spectator check has to
    // be careful not to catch. `sitDown` walks the whole route — join, land on the play page, take
    // the first free seat — so it only returns if the button was there to be pressed.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);

    // Act
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Assert
    await expect(seatCard(ada.page, 0).getByText('you')).toBeVisible();
    await expect(ada.page.getByRole('link', { name: 'Join to sit here' })).toHaveCount(0);

    // Four seats are still bots, and Ada is entitled to be offered any of them if she wants to move.
    await expect(ada.page.getByRole('button', { name: 'Sit here' })).toHaveCount(4);

    await leave([ada]);
  });

  test('Should_offer_no_way_in_at_all_once_the_game_has_begun', async ({ browser }) => {
    // Arrange
    // `ClaimSeat` starts with `Reject(state, GamePhase.Lobby)`, so the deal closes the door. Until
    // now the game page kept a panel open beside it reading "Watching. Take a seat to play." — an
    // instruction for a thing the engine would refuse, which is the same defect as a seat button a
    // spectator could not press, one screen further on.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    const watcher = await browser.newContext();
    const page = await watcher.newPage();
    await page.goto(`/play/${roomCode}`);

    // The offer is real while the lobby is open, so its disappearance below means something.
    await expect(page.getByRole('link', { name: 'Join to sit here' }).first()).toBeVisible();

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    // Assert
    await expect(page.getByText('Take a seat to play')).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Join to sit here' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Sit here' })).toHaveCount(0);

    // Still watching, and still told so — by the panel that would otherwise hold a hand. Asserted
    // because the fix for an untrue offer must not be a page that says nothing about who you are.
    await expect(page.getByText('You are watching this table without a seat.')).toBeVisible();
    await expect(page.locator('.map .mark').first()).toBeVisible();

    await watcher.close();
    await leave([ada]);
  });

  test('Should_show_a_watcher_none_of_the_setup_panels_while_the_table_sets_up', async ({ browser }) => {
    // Arrange
    // A shuffled deal, because a five-player scenario skips passing and answers the announcements
    // out of the book: the three setup panels only all appear on a generated one. Structural
    // assertions only, since nothing about a shuffled table repeats.
    //
    // Every command behind these panels reaches the hub through `WithSeatAsync` — `StartGame`
    // included, which is why the lobby's "Deal and begin" is in here too. Shown to a watcher the
    // dealing panel did not merely clutter: it reads its count off `mySeat()`, so it asked
    // somebody with no seat to take *any 0 tokens*.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    const watcher = await browser.newContext();
    const page = await watcher.newPage();
    await page.goto(`/play/${roomCode}`);
    await expect(page.locator('.seat').first()).toBeVisible();

    // Dealing belongs to the people playing, but the way in is real here, so it is still offered.
    await expect(page.getByRole('button', { name: 'Deal and begin' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Join to sit here' }).first()).toBeVisible();

    // Act
    await deal(ada.page, null);

    // Assert
    // Each phase is waited for on the watcher's own header before its panel is looked for. The
    // watcher's slot is empty in every phase, so an absence asserted too early would pass whatever
    // the page did — the wait is what makes each of these mean anything.
    await expect(page.locator('.head')).toContainText('Dealing');
    await expect(page.getByRole('heading', { name: 'Set up behind your screen', exact: true })).toHaveCount(0);

    // Act
    await takeTokens([ada]);

    // Assert
    // Anchored on the heading, not the text. The log says "Bot 2 passed a token to Bot 3", and
    // `getByText` matches case-insensitive substrings — so a text match would find the history
    // panel and report a leak that is not there.
    await expect(page.locator('.head')).toContainText('Passing');
    await expect(page.getByRole('heading', { name: 'Pass a token', exact: true })).toHaveCount(0);

    // Act
    await ada.page.getByRole('button', { name: 'Done passing' }).click();

    // Assert
    await expect(page.locator('.head')).toContainText('Announcing');
    await expect(page.getByRole('heading', { name: 'Announce your fewest terrain', exact: true })).toHaveCount(0);

    // Ada has been offered all three throughout, so none of the above passed by the panels simply
    // never being built.
    await expect(ada.page.getByRole('heading', { name: 'Announce your fewest terrain', exact: true })).toBeVisible();

    await watcher.close();
    await leave([ada]);
  });
});

/**
 * The laptop in the middle of the table.
 *
 * Its whole job is the QR code, and the deal ends that job: `ClaimSeat` is refused outside `Lobby`,
 * while joining is not, so a latecomer scanning the code afterwards gets a player token and *then* a
 * seat the hub will not give them. Leaving the code on screen is worse than a dead button — it is a
 * dead end two steps long, entered from another device.
 *
 * Nothing cheaper than a browser can see this. It is a route changing by itself in response to a
 * frame that arrived because somebody else pressed a button on a different machine.
 */
test.describe('The host screen', () => {
  test('Should_take_itself_to_the_table_when_the_game_has_begun', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await expect(host.getByRole('img', { name: 'QR code linking to this table' })).toBeVisible();

    // Act
    // Dealt from Ada's phone. The host screen is told by the same frame everybody else gets.
    await deal(ada.page, { book: 1, number: 1 });

    // Assert
    await host.waitForURL(new RegExp(`/play/${roomCode}$`));
    await expect(host.getByRole('img', { name: 'QR code linking to this table' })).toHaveCount(0);

    // Still nobody, which is the point of this screen: it never joined, so it holds no token and is
    // projected as a spectator. A hand on the display everybody can see would be the worst leak in
    // the app.
    await expect(host.getByText('You are watching this table without a seat.')).toBeVisible();

    await leave([ada]);
  });

  test('Should_not_come_back_to_a_screen_that_only_redirects_when_the_laptop_goes_back', async ({ browser }) => {
    // Arrange
    // The redirect replaces the history entry rather than adding one. Without that, Back lands on a
    // screen whose only behaviour is to send you forward again, and the laptop is stuck at the table
    // with no way out — a trap built out of a convenience.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await host.waitForURL(new RegExp(`/play/${roomCode}$`));

    // Act
    await host.goBack();

    // Assert
    // Wherever the laptop was before it opened the table, which is the page it created it from.
    await expect(host).toHaveURL(/\/new$/);

    await leave([ada]);
  });

  test('Should_still_offer_the_code_and_the_seat_when_nobody_has_dealt_yet', async ({ browser }) => {
    // Arrange
    // The other half of the same claim. A host screen that hid the code too eagerly would break the
    // one thing it exists for, and would do it silently.
    const host = await browser.newPage();

    // Act
    const roomCode = await openTable(host, 5);

    // Assert
    await expect(host.getByRole('img', { name: 'QR code linking to this table' })).toBeVisible();
    await expect(host.getByRole('link', { name: 'Sit down here' })).toBeVisible();
    await expect(host).toHaveURL(new RegExp(`/host/${roomCode}$`));
  });
});
