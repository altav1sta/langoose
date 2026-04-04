import { describe, expect, it } from 'vitest';
import { ApiError } from '../../api';
import { buildQuickAddPayload, describeError } from '../session';

describe('buildQuickAddPayload', () => {
  it('splits, trims, and filters glosses while detecting phrase items', () => {
    const payload = buildQuickAddPayload({
      englishText: 'watch over',
      russianText: ' \u043f\u0440\u0438\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\u0442\u044c,  \u043e\u043f\u0435\u043a\u0430\u0442\u044c , '
    });

    expect(payload).toEqual({
      englishText: 'watch over',
      russianGlosses: ['\u043f\u0440\u0438\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\u0442\u044c', '\u043e\u043f\u0435\u043a\u0430\u0442\u044c'],
      itemKind: 'phrase',
      createdByFlow: 'quick-add'
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
