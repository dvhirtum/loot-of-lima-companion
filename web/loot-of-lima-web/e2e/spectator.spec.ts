import { expect, test } from '@playwright/test';
import { deal, leave, openTable, seatCard, sitDown, takeTokens } from './fixtures/table';

/**
 * Watching a table, and sitting down at one.
 *
 * Everybody arrives without a seat now: the QR code opens the table itself, so a phone that has
 * just scanned it and the laptop in the middle of the table are in exactly the same state. What
 * separates a watcher from a player is one tap on a seat card and the name it asks for.
 *
 * Where that name is asked is the claim these specs pin, and it is the only thing this app ever
 * asks anybody. It used to be a form in front of the table, which cost a watcher the seat button
 * entirely: the hub will not take `ClaimSeat` from a browser holding no token, so every open seat
 * offered somebody who had skipped the form a link back to it instead. A page was withholding a
 * working button because of a step taken on a different screen.
 */
test.describe('Watching a table', () => {
  test('Should_ask_for_a_name_at_the_seat_when_a_watcher_has_never_joined', async ({ browser }) => {
    // Arrange
    // No `sitDown`, so this context holds no player token for this table at all — which is now the
    // state every single arrival is in, including the one this spec is named for.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const watcher = await browser.newContext();
    const page = await watcher.newPage();

    // Act
    await page.goto(`/play/${roomCode}`);

    // Assert
    // Every seat is offered, and nothing has asked who this is. The second half is the point: a
    // name up front is exactly what this flow removed, so a field on arrival is the regression.
    await expect(seatCard(page, 0)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sit here' })).toHaveCount(5);
    await expect(page.getByLabel('Your name')).toHaveCount(0);

    // Act
    await seatCard(page, 0).getByRole('button', { name: 'Sit here' }).click();

    // Assert
    // Asked in the card being claimed, not over the table, and asked only of that one seat.
    await expect(seatCard(page, 0).getByLabel('Your name')).toBeVisible();
    await expect(page.getByLabel('Your name')).toHaveCount(1);

    // Act
    await seatCard(page, 0).getByLabel('Your name').fill('Ada');
    await seatCard(page, 0).getByRole('button', { name: 'Sit down', exact: true }).click();

    // Assert
    // One tap and one name is the whole distance from watching to playing.
    //
    // `exact`: the field that was open a line ago is captioned "Your name", and `getByText` matches
    // case-insensitive substrings, so a loose match here would go green on the form rather than on
    // the seat and would assert nothing at all about the claim.
    await expect(seatCard(page, 0).getByText('you', { exact: true })).toBeVisible();
    await expect(seatCard(page, 0)).toContainText('Ada');

    await watcher.close();
  });

  test('Should_offer_no_second_seat_once_a_player_is_sitting_in_one', async ({ browser }) => {
    // Arrange
    // `ClaimSeat` refuses a second seat with `PlayerAlreadySeated` — one identity, one hand, or the
    // projector would build a full view of two hands for one person. The four other cards used to
    // carry a "Sit here" apiece anyway, which is the same defect as the button a watcher could not
    // press, pointing the other way: an offer whose only possible answer is a refusal.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);

    // Act
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Assert
    await expect(seatCard(ada.page, 0).getByText('you', { exact: true })).toBeVisible();
    await expect(ada.page.getByRole('button', { name: 'Sit here' })).toHaveCount(0);

    // The way to move is to give this one up first, and that button is on her own card.
    await expect(seatCard(ada.page, 0).getByRole('button', { name: 'Leave seat' })).toBeVisible();

    await leave([ada]);
  });

  test('Should_take_the_seat_without_asking_again_when_this_browser_has_already_joined', async ({ browser }) => {
    // Arrange
    // The identity holds the name, so a seat taken after the first is taken under it. Asking again
    // would let one person be two people at one table, and the name is what the rest of the table
    // reads off the seat.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await seatCard(ada.page, 0).getByRole('button', { name: 'Leave seat' }).click();
    await expect(seatCard(ada.page, 3).getByRole('button', { name: 'Sit here' })).toBeVisible();

    // Act
    await seatCard(ada.page, 3).getByRole('button', { name: 'Sit here' }).click();

    // Assert
    await expect(seatCard(ada.page, 3).getByText('you', { exact: true })).toBeVisible();
    await expect(seatCard(ada.page, 3)).toContainText('Ada');
    await expect(ada.page.getByLabel('Your name')).toHaveCount(0);

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
    await expect(page.getByRole('button', { name: 'Sit here' }).first()).toBeVisible();

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    // Assert
    await expect(page.getByText('Take a seat to play')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Sit here' })).toHaveCount(0);
    await expect(page.getByLabel('Your name')).toHaveCount(0);

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
    await expect(page.getByRole('button', { name: 'Sit here' }).first()).toBeVisible();

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
 * Its whole job is the QR code, and the deal ends that job: what the code offers is a seat, and
 * `ClaimSeat` is refused outside `Lobby`. Scanning it late is no longer a trap — the code encodes
 * the table itself, so a latecomer lands on the game and watches it — but an offer of something
 * nobody can have is still worth taking down, and by then the laptop should be showing the table.
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
    await expect(host).toHaveURL(new RegExp(`/host/${roomCode}$`));

    // One button where there were two. "Sit down here" and "Watch the table" led to the same page
    // the moment the name stopped being asked for in front of it.
    await expect(host.getByRole('link', { name: 'Join table' })).toBeVisible();
    await expect(host.getByRole('link', { name: 'Watch the table' })).toHaveCount(0);
  });
});
