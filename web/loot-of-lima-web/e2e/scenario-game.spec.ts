import { expect, test } from '@playwright/test';
import { askQuestion, deal, historyLines, openTable, ownHand, seatCard, sitDown, takeTokens } from './fixtures/table';

/**
 * Book 1, scenario 1, on screen.
 *
 * This is the one spec that asserts values rather than structure, and it can only be this one: a
 * generated deal is seeded from `Random.Shared.Next()` and `CreateGameRequest` has no seed field,
 * so nothing about a shuffled table repeats between runs. A printed scenario replays exactly, and
 * the book says what the answers are.
 *
 * The point is not that the engine is right -- 244,736 published answers already say so, far more
 * cheaply. It is that what the book says comes out of the projection, through the wire, through the
 * store, and onto the page a player is actually looking at.
 */
test.describe('A scenario game', () => {
  test('Should_show_the_printed_announcements_and_answers_when_book_1_scenario_1_is_dealt', async ({ browser }) => {
    // Arrange
    const page = await browser.newPage();
    const roomCode = await openTable(page, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Assert
    // The hand the book deals seat 1, which is also the only hand this page is allowed to show in
    // full. Announcing resolves itself here: a scenario deal prescribes every answer, so nobody is
    // asked to choose and play starts as soon as the tokens are confirmed.
    await expect(ada.page.getByText('InProgress')).toBeVisible();
    expect(await ownHand(ada.page)).toEqual(['1B', '2B', '6F', '3M']);

    // F M F F M, exactly as printed. Seat 2 announcing mountain is the part worth having a browser
    // for: that hand holds no beach and no mountain, so the two tie at zero and the choice was the
    // book's to make. Recomputing it would disagree with the page in the player's hands.
    const announced = ['forest', 'mountain', 'forest', 'forest', 'mountain'];
    for (const [index, terrain] of announced.entries()) {
      await expect(seatCard(ada.page, index)).toContainText(`fewest ${terrain}`);
    }
  });

  test('Should_answer_from_the_dealt_hand_when_a_question_is_asked_of_a_bot', async ({ browser }) => {
    // Arrange
    // Bot 3 holds B7 M1 M2 M5, so it has no forest at all and three mountains inside N to SW.
    const page = await browser.newPage();
    const roomCode = await openTable(page, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    // Coincident arrows sweep the whole circle, so N to N is "anywhere" rather than "nothing" --
    // the arc is clockwise from start, inclusive of start and exclusive of end.
    await askQuestion(ada.page, { of: 'Bot 3', from: 'N', upTo: 'N', terrain: 'Forest' });
    await askQuestion(ada.page, { of: 'Bot 3', from: 'N', upTo: 'SW', terrain: 'Mountain' });

    // Assert
    const log = await historyLines(ada.page);
    expect(log[1]).toContain('Ada asked Bot 3');
    expect(log[1]).toMatch(/forest.*0$/i);
    expect(log[0]).toMatch(/mountain.*3$/i);
  });

  test('Should_keep_the_treasure_hidden_when_the_game_is_still_being_played', async ({ browser }) => {
    // Arrange
    // The two set-aside locations are the answer to the whole game. `PlayerView.treasure` is null
    // in every phase but Finished, and this is the last place that could leak it -- everything
    // upstream is already asserted over raw frames by Server.Tests.
    const page = await browser.newPage();
    const roomCode = await openTable(page, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Assert
    await expect(ada.page.getByText('InProgress')).toBeVisible();
    await expect(ada.page.getByRole('heading', { name: 'The treasure' })).toHaveCount(0);
  });
});
