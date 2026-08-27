import { FullConfig } from '@playwright/test';

/**
 * Waits for something to be serving before any spec runs.
 *
 * The default config gets this free: Playwright polls each `webServer.url` and holds the run until
 * it answers. The docker config has no `webServer` -- there is nothing for it to start -- and
 * dropping that entry drops the readiness poll with it, which is easy to miss because the two look
 * like one setting.
 *
 * It matters here specifically. `docker compose up -d` returns as soon as the containers are
 * created, not when Kestrel is listening, and the composition deliberately has no `depends_on`:
 * nginx answering 502 while nothing is on 5000 is a state it treats as ordinary. So the documented
 * sequence really does race, and the failure is not a clear one -- the first spec gets a 502 from
 * `POST /api/games` and dies somewhere inside `waitForURL`, pointing at the page rather than at the
 * server that was not up yet.
 */
export default async function waitForServer(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL;
  if (baseURL === undefined) {
    throw new Error('No baseURL is configured, so there is nothing to wait for.');
  }

  // The API rather than the page: nginx serves the bundle from disk and answers long before the
  // server behind it does, so waiting on the page would wait for the wrong half.
  const probe = new URL('/api/scenarios', baseURL);
  const deadline = Date.now() + 120_000;
  let lastProblem = 'no attempt was made';

  while (Date.now() < deadline) {
    try {
      const response = await fetch(probe, { signal: AbortSignal.timeout(5_000) });
      if (response.ok) {
        return;
      }

      lastProblem = `${probe} answered ${response.status}`;
    } catch (problem) {
      lastProblem = problem instanceof Error ? problem.message : String(problem);
    }

    await new Promise((resume) => setTimeout(resume, 500));
  }

  throw new Error(
    `Nothing was serving at ${baseURL} after two minutes: ${lastProblem}. ` +
      'Is `docker compose up -d` finished, or is something else holding port 4200?',
  );
}
