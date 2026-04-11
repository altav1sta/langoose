export type AuthResponse = {
  userId: string;
  email: string;
};

type AntiforgeryTokenResponse = {
  requestToken: string;
};

type ProblemDetailsShape = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

export type DictionaryListItem = {
  dictionaryEntryId: string;
  text: string;
  language: string;
  difficulty?: string | null;
  isPublic: boolean;
  userDictionaryEntryId?: string | null;
  enrichmentStatus?: 'pending' | 'enriched' | 'failed' | null;
  type?: string | null;
  notes?: string | null;
  tags: string[];
};

export type StudyCard = {
  dictionaryEntryId: string;
  prompt: string;
  translationHint: string;
  difficulty?: string | null;
};

export type StudyAnswerResult = {
  verdict: 'correct' | 'almost_correct' | 'incorrect';
  normalizedAnswer: string;
  expectedAnswer: string;
  feedbackCode?:
    | 'exact_match'
    | 'accepted_variant'
    | 'missing_article'
    | 'inflection_mismatch'
    | 'minor_typo'
    | 'meaning_mismatch'
    | null;
  nextDueAtUtc: string;
};

export type Dashboard = {
  totalEntries: number;
  dueNow: number;
  newEntries: number;
  studiedToday: number;
};

export type SignInRequest = {
  email: string;
  password: string;
};

export type SignUpRequest = {
  email: string;
  password: string;
};

export type AddUserEntryRequest = {
  userInputTerm: string;
  userInputTranslation?: string | null;
  sourceLanguage: string;
  targetLanguage: string;
  notes?: string | null;
  tags?: string[];
  type?: string | null;
};

export type ImportCsvRequest = {
  fileName: string;
  csvContent: string;
};

export type ImportCsvResult = {
  rowCount: number;
  pendingCount: number;
  errors: string[];
};

export type SubmitAnswerRequest = {
  entryId: string;
  submittedAnswer: string;
};

export type ReportIssueRequest = {
  dictionaryEntryId: string;
  reason: string;
};

export class ApiError extends Error {
  readonly status: number;
  readonly detail?: string;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(status: number, message: string, detail?: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.detail = detail;
    this.fieldErrors = fieldErrors;
  }
}

const runtimeConfig = typeof window === 'undefined' ? undefined : window.LANGOOSE_CONFIG;

export function resolveApiBase(
  runtimeApiBaseUrl?: string,
  configuredApiBaseUrl?: string,
  isDev = import.meta.env.DEV
) {
  return runtimeApiBaseUrl
    ?? configuredApiBaseUrl
    ?? (isDev ? 'http://localhost:5000' : '/api');
}

const API_BASE = resolveApiBase(runtimeConfig?.apiBaseUrl, import.meta.env.VITE_API_BASE_URL);

let csrfRequestToken: string | undefined;

function isUnsafeMethod(method: string | undefined) {
  const normalized = (method ?? 'GET').toUpperCase();

  return normalized !== 'GET' && normalized !== 'HEAD' && normalized !== 'OPTIONS' && normalized !== 'TRACE';
}

function getFirstValidationMessage(errors: Record<string, string[]> | undefined) {
  if (!errors) {
    return undefined;
  }

  for (const messages of Object.values(errors)) {
    const message = messages.find(Boolean);

    if (message) {
      return message;
    }
  }

  return undefined;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers ?? {});
  const method = options.method ?? 'GET';

  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  if (isUnsafeMethod(method) && csrfRequestToken && !headers.has('X-CSRF-TOKEN')) {
    headers.set('X-CSRF-TOKEN', csrfRequestToken);
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    method,
    credentials: 'include',
    headers
  });

  if (!response.ok) {
    let problemDetails: ProblemDetailsShape | undefined;

    try {
      problemDetails = await response.json() as ProblemDetailsShape;
    } catch {
      problemDetails = undefined;
    }

    const fieldErrors = problemDetails?.errors;
    const message = getFirstValidationMessage(fieldErrors)
      ?? problemDetails?.detail
      ?? problemDetails?.title
      ?? `Request failed: ${response.status}`;

    throw new ApiError(response.status, message, problemDetails?.detail, fieldErrors);
  }

  if (response.status === 202 || response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('text/csv')) {
    return await response.text() as T;
  }

  const raw = await response.text();
  if (!raw.trim()) {
    return undefined as T;
  }

  return JSON.parse(raw) as T;
}

export const api = {
  async getAntiforgery() {
    const response = await request<AntiforgeryTokenResponse>('/auth/antiforgery');
    csrfRequestToken = response.requestToken;
  },
  signUp(email: string, password: string) {
    const payload: SignUpRequest = { email, password };
    return request<AuthResponse>('/auth/sign-up', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  signIn(email: string, password: string) {
    const payload: SignInRequest = { email, password };
    return request<AuthResponse>('/auth/sign-in', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  signOut() {
    return request<void>('/auth/sign-out', {
      method: 'POST'
    });
  },
  getMe() {
    return request<AuthResponse>('/auth/me');
  },
  getDictionary() {
    return request<DictionaryListItem[]>('/dictionary/entries');
  },
  addUserEntry(payload: AddUserEntryRequest) {
    return request<void>('/dictionary/entries', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  importCsv(fileName: string, csvContent: string) {
    const payload: ImportCsvRequest = { fileName, csvContent };
    return request<ImportCsvResult>('/dictionary/import', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  exportCsv() {
    return request<string>('/dictionary/export');
  },
  clearCustomData() {
    return request<void>('/dictionary/custom-data', {
      method: 'DELETE'
    });
  },
  getNextCard() {
    return request<StudyCard>('/study/next');
  },
  submitAnswer(entryId: string, submittedAnswer: string) {
    const payload: SubmitAnswerRequest = { entryId, submittedAnswer };
    return request<StudyAnswerResult>('/study/answer', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  getDashboard() {
    return request<Dashboard>('/study/dashboard');
  },
  reportIssue(dictionaryEntryId: string, reason: string) {
    const payload: ReportIssueRequest = { dictionaryEntryId, reason };
    return request<void>('/content/report-issue', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }
};
