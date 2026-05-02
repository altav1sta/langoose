import http from 'node:http';

const BASE_URL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://app-web';
const HEALTH_URL = `${BASE_URL.replace(/\/$/, '')}/api/health`;
const TIMEOUT_MS = 60_000;
const POLL_INTERVAL_MS = 500;

async function probe(): Promise<boolean> {
  return new Promise(resolve => {
    const req = http.get(HEALTH_URL, { timeout: 1_000 }, response => {
      resolve(response.statusCode === 200);
      response.resume();
    });
    req.on('error', () => resolve(false));
    req.on('timeout', () => {
      req.destroy();
      resolve(false);
    });
  });
}

export default async function globalSetup(): Promise<void> {
  const deadline = Date.now() + TIMEOUT_MS;

  while (Date.now() < deadline) {
    if (await probe()) return;
    await new Promise(resolve => setTimeout(resolve, POLL_INTERVAL_MS));
  }

  throw new Error(
    `API health probe never returned 200 at ${HEALTH_URL} within ${TIMEOUT_MS}ms`
  );
}
