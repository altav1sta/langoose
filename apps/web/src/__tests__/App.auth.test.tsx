// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, type AuthResponse, type Dashboard, type UserDictionaryEntry } from '../api';

const sessionApiMocks = vi.hoisted(() => ({
  bootstrapSession: vi.fn(),
  establishAuthenticatedSession: vi.fn(),
  loadSession: vi.fn(),
  loadStudyState: vi.fn(),
  refreshAuthenticatedSession: vi.fn(),
  resetAntiforgery: vi.fn()
}));

const apiMocks = vi.hoisted(() => ({
  signOut: vi.fn()
}));

vi.mock('../lib/sessionApi', () => sessionApiMocks);
vi.mock('../api', async () => {
  const actual = await vi.importActual<typeof import('../api')>('../api');

  return {
    ...actual,
    api: {
      ...actual.api,
      signOut: apiMocks.signOut
    }
  };
});

import App from '../App';

const auth: AuthResponse = {
  userId: '11111111-1111-1111-1111-111111111111',
  email: 'learner@example.com'
};

const dictionary: UserDictionaryEntry[] = [
  {
    id: '22222222-2222-2222-2222-222222222222',
    userId: '11111111-1111-1111-1111-111111111111',
    dictionaryEntryId: null,
    sourceLanguage: 'ru',
    targetLanguage: 'en',
    userInputTerm: 'look for',
    enrichmentStatus: 'pending',
    enrichmentAttempts: 0,
    tags: [],
    type: 'phrase',
    createdAtUtc: '2026-04-01T00:00:00Z',
    updatedAtUtc: '2026-04-01T00:00:00Z'
  }
];

const dashboard: Dashboard = {
  totalEntries: 1,
  dueNow: 1,
  newEntries: 1,
  studiedToday: 0
};

beforeEach(() => {
  vi.clearAllMocks();
  sessionApiMocks.bootstrapSession.mockRejectedValue(new ApiError(401, 'Unauthorized'));
  sessionApiMocks.establishAuthenticatedSession.mockResolvedValue(auth);
  sessionApiMocks.loadSession.mockResolvedValue({ dictionary, dashboard, card: undefined });
  sessionApiMocks.loadStudyState.mockResolvedValue({ dashboard, card: undefined });
  sessionApiMocks.refreshAuthenticatedSession.mockResolvedValue({
    auth,
    snapshot: { dictionary, dashboard, card: undefined }
  });
  sessionApiMocks.resetAntiforgery.mockResolvedValue(undefined);
  apiMocks.signOut.mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
});

describe('App auth flow', () => {
  it('treats bootstrap 401 as a normal signed-out state', async () => {
    render(<App />);

    await waitFor(() => expect(sessionApiMocks.bootstrapSession).toHaveBeenCalledTimes(1));

    expect(screen.getByRole('button', { name: 'Sign in' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Create account' })).toBeTruthy();
  });

  it('hydrates the authenticated UI after sign in', async () => {
    render(<App />);
    await waitFor(() => expect(sessionApiMocks.bootstrapSession).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await screen.findByText(auth.email);
    expect(sessionApiMocks.establishAuthenticatedSession).toHaveBeenCalledTimes(1);
    expect(screen.getByText('You are signed in and ready to study.')).toBeTruthy();
  });

  it('hydrates the authenticated UI after sign up', async () => {
    render(<App />);
    await waitFor(() => expect(sessionApiMocks.bootstrapSession).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    await screen.findByText(auth.email);
    expect(sessionApiMocks.establishAuthenticatedSession).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Your account is ready and you are signed in.')).toBeTruthy();
  });

  it('returns to the signed-out state after sign out', async () => {
    sessionApiMocks.bootstrapSession.mockResolvedValue(auth);

    render(<App />);
    await screen.findByText(auth.email);

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(apiMocks.signOut).toHaveBeenCalledTimes(1));
    await screen.findByRole('button', { name: 'Sign in' });
    expect(screen.getByText('Signed out.')).toBeTruthy();
  });

  it('moves back to signed-out when a refresh hits 401', async () => {
    sessionApiMocks.bootstrapSession.mockResolvedValue(auth);
    sessionApiMocks.refreshAuthenticatedSession.mockRejectedValue(new ApiError(401, 'Unauthorized'));

    render(<App />);
    await screen.findByText(auth.email);

    fireEvent.click(screen.getByRole('button', { name: 'Sync session' }));

    await waitFor(() => expect(sessionApiMocks.resetAntiforgery).toHaveBeenCalledTimes(1));
    await screen.findByRole('button', { name: 'Sign in' });
    expect(screen.getByText('Your session expired. Sign in again.')).toBeTruthy();
  });
});
