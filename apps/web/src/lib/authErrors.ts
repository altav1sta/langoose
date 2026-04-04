import { ApiError } from '../api';

export function getAuthErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof ApiError)) {
    return undefined;
  }

  if (error.status === 401) {
    return 'Invalid email or password.';
  }

  if (error.status === 409) {
    return 'That email is already registered.';
  }

  if (error.status === 423) {
    return 'Too many failed attempts. Try again later.';
  }

  return fallback;
}
