import {
  ApiError,
  type AddDictionaryItemRequest,
  type AuthResponse,
  type Dashboard,
  type DictionaryItem,
  type StudyAnswerResult,
  type StudyCard
} from '../api';

export type SessionState = {
  auth?: AuthResponse;
  dashboard?: Dashboard;
  dictionary: DictionaryItem[];
  card?: StudyCard;
  result?: StudyAnswerResult;
  error?: string;
  notice?: string;
  lastSyncedAt?: string;
};

export type SessionSnapshot = {
  dictionary: DictionaryItem[];
  dashboard: Dashboard;
  card?: StudyCard;
};

export type AuthFormState = {
  email: string;
  password: string;
};

export type QuickAddFormState = {
  englishText: string;
  russianText: string;
};

export const initialState: SessionState = {
  dictionary: []
};

export const initialAuthForm: AuthFormState = {
  email: 'learner@example.com',
  password: 'password123'
};

export const initialQuickAddForm: QuickAddFormState = {
  englishText: 'look for',
  russianText: '\u0438\u0441\u043a\u0430\u0442\u044c'
};

export function verdictClassName(verdict: StudyAnswerResult['verdict'] | undefined) {
  if (!verdict) {
    return '';
  }

  return verdict;
}

export function verdictLabel(verdict: StudyAnswerResult['verdict'] | undefined) {
  if (verdict === 'correct') {
    return 'Correct';
  }

  if (verdict === 'almost_correct') {
    return 'Almost correct';
  }

  return 'Try again';
}

export function feedbackLabel(result: StudyAnswerResult) {
  switch (result.feedbackCode) {
    case 'exact_match':
      return 'Perfect. That answer matches the expected wording.';
    case 'accepted_variant':
      return 'Close enough. We accepted a valid variant of the target answer.';
    case 'missing_article':
      return 'Almost there. The meaning is right, but the article is missing or different.';
    case 'inflection_mismatch':
      return 'Almost there. The base meaning is correct, but the word form is slightly off.';
    case 'minor_typo':
      return 'Almost there. That looks like a minor typo.';
    default:
      return 'Not quite. Try the target phrase that best fits this sentence.';
  }
}

export function formatTime(value?: string) {
  if (!value) {
    return 'Not synced yet';
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(new Date(value));
}

export function buildRefreshNotice(previousCard: StudyCard | undefined, snapshot: SessionSnapshot) {
  const cardChanged = previousCard?.itemId !== snapshot.card?.itemId;
  const cardMessage = snapshot.card
    ? cardChanged
      ? 'Loaded a different due card.'
      : 'The same card is still next in line.'
    : 'You have no due cards right now.';

  return `Session synced. ${cardMessage} Due now: ${snapshot.dashboard.dueNow}. Custom items: ${snapshot.dashboard.customItems}.`;
}

export function getCustomItemCount(items: DictionaryItem[]) {
  return items.filter(x => x.ownerId).length;
}

export function buildQuickAddPayload(form: QuickAddFormState): AddDictionaryItemRequest {
  return {
    englishText: form.englishText,
    russianGlosses: form.russianText
      .split(',')
      .map(x => x.trim())
      .filter(Boolean),
    itemKind: form.englishText.includes(' ') ? 'phrase' : 'word',
    createdByFlow: 'quick-add'
  };
}

export function isUnauthorized(error: unknown) {
  return error instanceof ApiError && error.status === 401;
}

export function firstFieldError(error: ApiError | undefined) {
  if (!error?.fieldErrors) {
    return undefined;
  }

  for (const messages of Object.values(error.fieldErrors)) {
    const message = messages.find(Boolean);

    if (message) {
      return message;
    }
  }

  return undefined;
}

export function describeError(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    return firstFieldError(error) ?? error.message;
  }

  return error instanceof Error ? error.message : fallback;
}
