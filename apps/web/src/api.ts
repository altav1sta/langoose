export type AuthResponse = {
  userId: string;
  email: string;
  name: string;
  token: string;
};

export type DictionaryItem = {
  id: string;
  ownerId?: string | null;
  sourceType: 'base' | 'custom';
  englishText: string;
  russianGlosses: string[];
  itemKind: 'word' | 'phrase';
  partOfSpeech: string;
  difficulty: string;
  status: 'active' | 'flagged' | 'archived';
  createdByFlow: string;
  notes: string;
  tags: string[];
};

export type StudyCard = {
  itemId: string;
  prompt: string;
  translationHint: string;
  glosses: string[];
  itemKind: 'word' | 'phrase';
  sourceType: 'base' | 'custom';
  difficulty: string;
};

export type StudyAnswerResult = {
  verdict: 'Correct' | 'AlmostCorrect' | 'Incorrect';
  normalizedAnswer: string;
  acceptedVariant?: string | null;
  expectedAnswer: string;
  feedbackCode: string;
  nextDueAtUtc: string;
};

export type Dashboard = {
  totalItems: number;
  dueNow: number;
  newItems: number;
  baseItems: number;
  customItems: number;
  studiedToday: number;
};

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options.headers ?? {})
    }
  });

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`);
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
  signIn(email: string, name?: string) {
    return request<AuthResponse>('/auth/email-sign-in', {
      method: 'POST',
      body: JSON.stringify({ email, name })
    });
  },
  getDictionary(token: string) {
    return request<DictionaryItem[]>('/dictionary/items', {}, token);
  },
  addDictionaryItem(token: string, payload: Record<string, unknown>) {
    return request<DictionaryItem>('/dictionary/items', {
      method: 'POST',
      body: JSON.stringify(payload)
    }, token);
  },
  importCsv(token: string, fileName: string, csvContent: string) {
    return request<{ totalRows: number; importedRows: number; skippedRows: number; errors: string[] }>('/dictionary/import', {
      method: 'POST',
      body: JSON.stringify({ fileName, csvContent })
    }, token);
  },
  exportCsv(token: string) {
    return request<string>('/dictionary/export', {}, token);
  },
  clearCustomData(token: string) {
    return request<void>('/dictionary/custom-data', {
      method: 'DELETE'
    }, token);
  },
  getNextCard(token: string) {
    return request<StudyCard>('/study/next', {}, token);
  },
  submitAnswer(token: string, itemId: string, submittedAnswer: string) {
    return request<StudyAnswerResult>('/study/answer', {
      method: 'POST',
      body: JSON.stringify({ itemId, submittedAnswer })
    }, token);
  },
  getDashboard(token: string) {
    return request<Dashboard>('/study/dashboard', {}, token);
  },
  reportIssue(token: string, itemId: string, reason: string, details: string) {
    return request<void>('/content/report-issue', {
      method: 'POST',
      body: JSON.stringify({ itemId, reason, details })
    }, token);
  }
};

