import { describe, expect, it, vi } from 'vitest';
import { api } from '../../api';
import { establishAuthenticatedSession } from '../sessionApi';

describe('establishAuthenticatedSession', () => {
  it('fetches antiforgery before invoking the auth request and refreshes it afterward', async () => {
    const calls: string[] = [];
    const auth = { userId: '11111111-1111-1111-1111-111111111111', email: 'learner@example.com' };

    const antiforgerySpy = vi.spyOn(api, 'getAntiforgery').mockImplementation(async () => {
      calls.push('csrf');
    });

    const request = vi.fn(async () => {
      calls.push('request');
      return auth;
    });

    await expect(establishAuthenticatedSession(request)).resolves.toEqual(auth);

    expect(request).toHaveBeenCalledTimes(1);
    expect(calls).toEqual(['csrf', 'request', 'csrf']);

    antiforgerySpy.mockRestore();
  });
});
