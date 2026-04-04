import { api, type AuthResponse } from '../api';
import type { SessionSnapshot } from './session';

export async function loadSession(): Promise<SessionSnapshot> {
  const [dictionary, dashboard] = await Promise.all([
    api.getDictionary(),
    api.getDashboard()
  ]);

  const card = await api.getNextCard().catch(() => undefined);

  return { dictionary, dashboard, card };
}

export async function loadStudyState() {
  const [dashboard, card] = await Promise.all([
    api.getDashboard(),
    api.getNextCard().catch(() => undefined)
  ]);

  return { dashboard, card };
}

export async function bootstrapSession() {
  await api.getAntiforgery();

  return api.getMe();
}

export async function refreshAuthenticatedSession() {
  const auth = await api.getMe();
  const snapshot = await loadSession();

  return { auth, snapshot };
}

export async function establishAuthenticatedSession(request: () => Promise<AuthResponse>) {
  await api.getAntiforgery();

  const auth = await request();

  await api.getAntiforgery();

  return auth;
}

export async function resetAntiforgery() {
  await api.getAntiforgery().catch(() => undefined);
}
