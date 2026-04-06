import { describe, expect, it } from 'vitest';
import { resolveApiBase } from '../api';

describe('resolveApiBase', () => {
  it('prefers runtime config when present', () => {
    expect(resolveApiBase('/api', 'https://configured.example', false)).toBe('/api');
  });

  it('falls back to configured env when runtime config is absent', () => {
    expect(resolveApiBase(undefined, 'https://configured.example', false)).toBe('https://configured.example');
  });

  it('uses localhost during dev when no override is configured', () => {
    expect(resolveApiBase(undefined, undefined, true)).toBe('http://localhost:5000');
  });

  it('uses same-origin api path outside dev when no override is configured', () => {
    expect(resolveApiBase(undefined, undefined, false)).toBe('/api');
  });
});
