import { describe, expect, it } from 'vitest';
import { ApiError } from '../../api';
import { buildQuickAddPayload, describeError } from '../session';

describe('buildQuickAddPayload', () => {
  it('builds a user entry request from form state', () => {
    const payload = buildQuickAddPayload({
      englishText: 'watch over',
      russianText: ' \u043f\u0440\u0438\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\u0442\u044c,  \u043e\u043f\u0435\u043a\u0430\u0442\u044c , '
    });

    expect(payload).toEqual({
      userInputTerm: 'watch over',
      sourceLanguage: 'ru',
      targetLanguage: 'en'
    });
  });
});

describe('describeError', () => {
  it('prefers the first field validation message from ApiError', () => {
    const error = new ApiError(400, 'Request failed', undefined, {
      Email: ['The Email field is required.'],
      Password: ['The Password field is required.']
    });

    expect(describeError(error, 'Fallback')).toBe('The Email field is required.');
  });
});
