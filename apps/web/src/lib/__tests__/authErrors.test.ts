import { describe, expect, it } from 'vitest';
import { ApiError } from '../../api';
import { getAuthErrorMessage } from '../authErrors';

describe('getAuthErrorMessage', () => {
  it('maps known auth statuses to user-facing messages', () => {
    expect(getAuthErrorMessage(new ApiError(401, 'Unauthorized'), 'Fallback')).toBe('Invalid email or password.');
    expect(getAuthErrorMessage(new ApiError(409, 'Conflict'), 'Fallback')).toBe('That email is already registered.');
    expect(getAuthErrorMessage(new ApiError(423, 'Locked'), 'Fallback')).toBe('Too many failed attempts. Try again later.');
  });

  it('falls back for unknown statuses and non-ApiError values', () => {
    expect(getAuthErrorMessage(new ApiError(500, 'Server error'), 'Fallback')).toBe('Fallback');
    expect(getAuthErrorMessage(new Error('boom'), 'Fallback')).toBeUndefined();
  });
});
