import { expect, test } from '@playwright/test';
import {
  askQuestion,
  deal,
  leave,
  openTable,
  ownHand,
  seatCard,
  sitDown,
  statedHandSize,
  takeTokens,
  visibleTokens,
} from './fixtures/table';

/**
 * Two humans at one table, which is the only way to see the hidden-information design work.
 *
 * A browser context per player is what makes this real: the seat is held by a token in
 * `localStorage`, so two contexts are two people, while two pages of one context would be the same
 * person twice and would make every assertion below vacuous.
 *
 * `Server.Tests` already asserts redaction over the raw bytes of every frame, which is stricter than
 * anything the DOM can say. What it cannot say is whether the page a player is looking at shows
 * them somebody else's hand -- there are two more layers between the frame and the screen, and this
 * is the only suite that sees them.
 */
test.describe('Two players at one table', () => {
  test('Should_show_each_player_only_their_own_hand_when_two_are_seated', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);

    // Assert
    const adasHand = await ownHand(ada.page);
    const gracesHand = await ownHand(grace.page);

    expect(adasHand).toEqual(['B1', 'B2', 'F6', 'M3']);
    expect(gracesHand).toHaveLength(4);
    expect(adasHand).not.toEqual(gracesHand);

    // The real claim, and the one that goes red if the seat projection ever stops redacting: not a
    // single token of Grace's hand appears anywhere on Ada's page -- not in the seat strip, not in
    // the log, not face up. Asserting on her seat card alone would pass a leak that arrived
    // somewhere else on the screen.
    const onAdasScreen = await visibleTokens(ada.page);
    for (const token of gracesHand) {
      expect(onAdasScreen, `Grace's ${token} is on Ada's screen`).not.toContain(token);
    }

    const onGracesScreen = await visibleTokens(grace.page);
    for (const token of adasHand) {
      expect(onGracesScreen, `Ada's ${token} is on Grace's screen`).not.toContain(token);
    }

    await leave([ada, grace]);
  });

  test('Should_agree_about_what_is_public_when_two_players_look_at_one_table', async ({ browser }) => {
    // Arrange
    // Hand sizes and the setup announcements are public by the rules -- tokens are counted openly
    // and the announcement is made aloud -- so hiding them would be a bug in the other direction.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);
    await expect(ada.page.getByText('InProgress')).toBeVisible();
    await expect(grace.page.getByText('InProgress')).toBeVisible();

    // Assert
    for (let seat = 0; seat < 5; seat += 1) {
      expect(await statedHandSize(ada.page, seat)).toBe(4);
      expect(await statedHandSize(grace.page, seat)).toBe(4);
    }

    const announced = ['forest', 'mountain', 'forest', 'forest', 'mountain'];
    for (const [index, terrain] of announced.entries()) {
      await expect(seatCard(ada.page, index)).toContainText(`fewest ${terrain}`);
      await expect(seatCard(grace.page, index)).toContainText(`fewest ${terrain}`);
    }

    await leave([ada, grace]);
  });

  test('Should_reach_both_screens_when_one_player_asks_a_question', async ({ browser }) => {
    // Arrange
    // A public question is public: the asker's page is not the only one that learns the answer, and
    // every push goes out per connection rather than to a group, so "everyone got it" is a claim
    // about five separate sends.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);

    // Act
    await askQuestion(ada.page, { of: 'Bot 3', from: 'N', upTo: 'N', terrain: 'Forest' });

    // Assert
    await expect(grace.page.locator('.log .entry').first()).toContainText('Ada asked Bot 3');
    await expect(grace.page.locator('.log .entry').first()).toContainText('0');

    await leave([ada, grace]);
  });
});
