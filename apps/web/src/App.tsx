import { useEffect, useState, type Dispatch, type SetStateAction } from 'react';
import {
  api,
  type AddDictionaryItemRequest,
  type AuthResponse,
  type Dashboard,
  type DictionaryItem,
  type StudyAnswerResult,
  type StudyCard
} from './api';
import './styles.css';

type SessionState = {
  auth?: AuthResponse;
  dashboard?: Dashboard;
  dictionary: DictionaryItem[];
  card?: StudyCard;
  result?: StudyAnswerResult;
  error?: string;
  notice?: string;
  lastSyncedAt?: string;
};

type SessionSnapshot = {
  dictionary: DictionaryItem[];
  dashboard: Dashboard;
  card?: StudyCard;
};

type AuthFormState = {
  email: string;
  name: string;
};

type QuickAddFormState = {
  englishText: string;
  russianText: string;
};

const initialState: SessionState = {
  dictionary: []
};

const initialAuthForm: AuthFormState = {
  email: 'learner@example.com',
  name: 'Learner'
};

const initialQuickAddForm: QuickAddFormState = {
  englishText: 'look for',
  russianText: 'искать'
};

function verdictClassName(verdict: StudyAnswerResult['verdict'] | undefined) {
  if (!verdict) {
    return '';
  }

  return verdict;
}

function verdictLabel(verdict: StudyAnswerResult['verdict'] | undefined) {
  if (verdict === 'correct') {
    return 'Correct';
  }

  if (verdict === 'almost_correct') {
    return 'Almost correct';
  }

  return 'Try again';
}

function feedbackLabel(result: StudyAnswerResult) {
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

function formatTime(value?: string) {
  if (!value) {
    return 'Not synced yet';
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(new Date(value));
}

function buildRefreshNotice(previousCard: StudyCard | undefined, snapshot: SessionSnapshot) {
  const cardChanged = previousCard?.itemId !== snapshot.card?.itemId;
  const cardMessage = snapshot.card
    ? cardChanged
      ? 'Loaded a different due card.'
      : 'The same card is still next in line.'
    : 'You have no due cards right now.';

  return `Session synced. ${cardMessage} Due now: ${snapshot.dashboard.dueNow}. Custom items: ${snapshot.dashboard.customItems}.`;
}

function getCustomItemCount(items: DictionaryItem[]) {
  return items.filter(item => item.ownerId).length;
}

function buildQuickAddPayload(form: QuickAddFormState): AddDictionaryItemRequest {
  return {
    englishText: form.englishText,
    russianGlosses: form.russianText
      .split(',')
      .map(value => value.trim())
      .filter(Boolean),
    itemKind: form.englishText.includes(' ') ? 'phrase' : 'word',
    createdByFlow: 'quick-add'
  };
}

function parseStoredAuth() {
  const stored = window.localStorage.getItem('langoose-auth');
  if (!stored) {
    return undefined;
  }

  try {
    return JSON.parse(stored) as AuthResponse;
  } catch {
    window.localStorage.removeItem('langoose-auth');
    return undefined;
  }
}

async function loadSession(auth: AuthResponse): Promise<SessionSnapshot> {
  const [dictionary, dashboard] = await Promise.all([
    api.getDictionary(auth.token),
    api.getDashboard(auth.token)
  ]);

  const card = await api.getNextCard(auth.token).catch(() => undefined);
  return { dictionary, dashboard, card };
}

async function loadStudyState(token: string) {
  const [dashboard, card] = await Promise.all([
    api.getDashboard(token),
    api.getNextCard(token).catch(() => undefined)
  ]);

  return { dashboard, card };
}

export default function App() {
  const [state, setState] = useState<SessionState>(initialState);
  const [authForm, setAuthForm] = useState<AuthFormState>(initialAuthForm);
  const [answer, setAnswer] = useState('');
  const [quickAddForm, setQuickAddForm] = useState<QuickAddFormState>(initialQuickAddForm);
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [importSummary, setImportSummary] = useState('');

  useEffect(() => {
    const auth = parseStoredAuth();
    if (!auth) {
      return;
    }

    void hydrate(auth).catch(error => {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Could not restore your session'
      }));
    });
  }, []);

  async function hydrate(auth: AuthResponse, notice?: string) {
    const snapshot = await loadSession(auth);
    setState(current => ({
      ...current,
      auth,
      dictionary: snapshot.dictionary,
      dashboard: snapshot.dashboard,
      card: snapshot.card,
      notice,
      error: undefined,
      lastSyncedAt: new Date().toISOString()
    }));

    return snapshot;
  }

  async function refreshSession() {
    if (!state.auth) {
      return;
    }

    setAnswer('');
    setImportSummary('');
    setState(current => ({ ...current, result: undefined }));

    try {
      const snapshot = await loadSession(state.auth);
      setState(current => ({
        ...current,
        dictionary: snapshot.dictionary,
        dashboard: snapshot.dashboard,
        card: snapshot.card,
        result: undefined,
        error: undefined,
        notice: buildRefreshNotice(current.card, snapshot),
        lastSyncedAt: new Date().toISOString()
      }));
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Could not sync the session'
      }));
    }
  }

  async function signIn() {
    try {
      const auth = await api.signIn(authForm.email, authForm.name);
      window.localStorage.setItem('langoose-auth', JSON.stringify(auth));
      await hydrate(auth, 'You are signed in and ready to study.');
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Sign-in failed'
      }));
    }
  }

  async function addItem() {
    if (!state.auth) {
      return;
    }

    try {
      const beforeCount = getCustomItemCount(state.dictionary);
      const savedItem = await api.addDictionaryItem(state.auth.token, buildQuickAddPayload(quickAddForm));
      const snapshot = await hydrate(state.auth);
      const afterCount = getCustomItemCount(snapshot.dictionary);
      const merged = afterCount === beforeCount;

      setState(current => ({
        ...current,
        notice: savedItem.sourceType === 'base'
          ? 'That term is already in the base dictionary, so we kept the existing entry.'
          : merged
            ? 'Updated your existing custom entry instead of creating a duplicate.'
            : 'Saved to your dictionary and added to the study pool.'
      }));
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Could not add item'
      }));
    }
  }

  async function importItems() {
    if (!state.auth || !csvFile) {
      return;
    }

    try {
      const content = await csvFile.text();
      const result = await api.importCsv(state.auth.token, csvFile.name, content);
      setImportSummary(
        `Imported ${result.importedRows} of ${result.totalRows} rows${result.skippedRows
          ? ', skipped rows that were already in your custom or base dictionary'
          : ''
        }.`
      );
      setCsvFile(null);

      await hydrate(
        state.auth,
        result.skippedRows > 0
          ? 'Import finished. Existing custom entries and base terms were skipped so the dictionary stays clean.'
          : 'Import finished. New rows were added to your custom dictionary.'
      );
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Import failed'
      }));
    }
  }

  async function submitAnswer() {
    if (!state.auth || !state.card) {
      return;
    }

    try {
      const result = await api.submitAnswer(state.auth.token, state.card.itemId, answer);
      const studyState = await loadStudyState(state.auth.token);
      setAnswer('');

      setState(current => ({
        ...current,
        dashboard: studyState.dashboard,
        card: studyState.card,
        result,
        error: undefined,
        notice: 'Answer saved.',
        lastSyncedAt: new Date().toISOString()
      }));
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Answer submission failed'
      }));
    }
  }

  async function exportItems() {
    if (!state.auth) {
      return;
    }

    try {
      const csv = await api.exportCsv(state.auth.token);
      await navigator.clipboard.writeText(csv);
      setState(current => ({
        ...current,
        error: undefined,
        notice: 'Export copied to clipboard.'
      }));
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Export failed'
      }));
    }
  }

  async function clearCustomData() {
    if (!state.auth) {
      return;
    }

    const confirmed = window.confirm(
      'Clear all custom words, imports, and progress for your account? Base content will stay available.'
    );

    if (!confirmed) {
      return;
    }

    try {
      await api.clearCustomData(state.auth.token);
      setAnswer('');
      setImportSummary('');
      setCsvFile(null);
      setState(current => ({ ...current, result: undefined }));
      await hydrate(
        state.auth,
        'Cleared your custom words, imports, and progress. Base study content is still available.'
      );
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Could not clear custom data'
      }));
    }
  }

  async function reportIssue() {
    if (!state.auth || !state.card) {
      return;
    }

    try {
      await api.reportIssue(state.auth.token, state.card.itemId, 'awkward sentence', 'Reported from study card UI');
      setAnswer('');
      setImportSummary('');
      setState(current => ({ ...current, result: undefined }));
      await hydrate(state.auth, 'Thanks. We hid that card for now and loaded a different one.');
    } catch (error) {
      setState(current => ({
        ...current,
        error: error instanceof Error ? error.message : 'Issue report failed'
      }));
    }
  }

  const customItemCount = getCustomItemCount(state.dictionary);

  return (
    <main className="app-shell">
      <HeroSection
        auth={state.auth}
        authForm={authForm}
        lastSyncedAt={state.lastSyncedAt}
        onAuthChange={setAuthForm}
        onRefresh={() => void refreshSession()}
        onSignIn={() => void signIn()}
      />

      {state.error ? <p className="error-banner">{state.error}</p> : null}
      {state.notice ? <p className="notice-banner">{state.notice}</p> : null}

      <section className="grid two-up">
        <ProgressPanel dashboard={state.dashboard} />
        <StudyPanel
          answer={answer}
          card={state.card}
          result={state.result}
          onAnswerChange={setAnswer}
          onReportIssue={() => void reportIssue()}
          onSubmit={() => void submitAnswer()}
        />
      </section>

      <section className="grid two-up">
        <QuickAddPanel
          auth={state.auth}
          form={quickAddForm}
          onFormChange={setQuickAddForm}
          onSubmit={() => void addItem()}
        />
        <CsvPanel
          auth={state.auth}
          csvFile={csvFile}
          importSummary={importSummary}
          onCsvFileChange={setCsvFile}
          onExport={() => void exportItems()}
          onImport={() => void importItems()}
        />
      </section>

      <DictionaryPanel
        auth={state.auth}
        customItemCount={customItemCount}
        dictionary={state.dictionary}
        onClearCustomData={() => void clearCustomData()}
      />
    </main>
  );
}

type HeroSectionProps = {
  auth?: AuthResponse;
  authForm: AuthFormState;
  lastSyncedAt?: string;
  onAuthChange: Dispatch<SetStateAction<AuthFormState>>;
  onRefresh: () => void;
  onSignIn: () => void;
};

function HeroSection({
  auth,
  authForm,
  lastSyncedAt,
  onAuthChange,
  onRefresh,
  onSignIn
}: HeroSectionProps) {
  return (
    <section className="hero-card">
      <div>
        <p className="eyebrow">Russian speakers learning English</p>
        <h1>Langoose</h1>
        <p className="lede">
          A sentence-based learning loop with a private dictionary layered into the base training set.
        </p>
      </div>
      {!auth ? (
        <form
          className="auth-panel"
          onSubmit={event => {
            event.preventDefault();
            onSignIn();
          }}
        >
          <input
            value={authForm.email}
            onChange={event => onAuthChange(current => ({ ...current, email: event.target.value }))}
            placeholder="Email"
          />
          <input
            value={authForm.name}
            onChange={event => onAuthChange(current => ({ ...current, name: event.target.value }))}
            placeholder="Name"
          />
          <button type="submit">Start learning</button>
        </form>
      ) : (
        <div className="welcome-panel">
          <span>{auth.name}</span>
          <button type="button" onClick={onRefresh}>Sync session</button>
          <p className="helper-text">
            Sync refreshes the dashboard and reloads whichever card is currently next in your study queue.
          </p>
          <p className="sync-meta">Last synced: {formatTime(lastSyncedAt)}</p>
        </div>
      )}
    </section>
  );
}

function ProgressPanel({ dashboard }: { dashboard?: Dashboard }) {
  return (
    <article className="panel stats-panel">
      <h2>Progress</h2>
      <div className="stats-grid">
        <Metric label="Total items" value={dashboard?.totalItems ?? 0} />
        <Metric label="Due now" value={dashboard?.dueNow ?? 0} />
        <Metric label="New" value={dashboard?.newItems ?? 0} />
        <Metric label="Custom" value={dashboard?.customItems ?? 0} />
      </div>
    </article>
  );
}

type StudyPanelProps = {
  answer: string;
  card?: StudyCard;
  result?: StudyAnswerResult;
  onAnswerChange: (value: string) => void;
  onReportIssue: () => void;
  onSubmit: () => void;
};

function StudyPanel({ answer, card, result, onAnswerChange, onReportIssue, onSubmit }: StudyPanelProps) {
  return (
    <article className="panel study-panel">
      <h2>Study</h2>
      {card ? (
        <form
          onSubmit={event => {
            event.preventDefault();
            onSubmit();
          }}
        >
          <p className="prompt">{card.prompt}</p>
          <p className="hint">{card.translationHint}</p>
          <input
            value={answer}
            onChange={event => onAnswerChange(event.target.value)}
            placeholder="Type the missing English word or phrase"
          />
          <div className="actions-row study-actions">
            <button type="submit">Check answer</button>
            <button type="button" className="secondary" onClick={onReportIssue}>Report issue</button>
          </div>
        </form>
      ) : (
        <p>No due cards right now.</p>
      )}
      {result ? (
        <div className={`result-chip ${verdictClassName(result.verdict)}`}>
          <strong>{verdictLabel(result.verdict)}</strong>
          <span>{feedbackLabel(result)}</span>
          <span>Expected answer: {result.expectedAnswer}</span>
        </div>
      ) : null}
    </article>
  );
}

type QuickAddPanelProps = {
  auth?: AuthResponse;
  form: QuickAddFormState;
  onFormChange: Dispatch<SetStateAction<QuickAddFormState>>;
  onSubmit: () => void;
};

function QuickAddPanel({ auth, form, onFormChange, onSubmit }: QuickAddPanelProps) {
  return (
    <article className="panel">
      <h2>Quick add</h2>
      <form
        onSubmit={event => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <input
          value={form.englishText}
          onChange={event => onFormChange(current => ({ ...current, englishText: event.target.value }))}
          placeholder="English word or phrase"
        />
        <input
          value={form.russianText}
          onChange={event => onFormChange(current => ({ ...current, russianText: event.target.value }))}
          placeholder="Russian glosses, comma separated"
        />
        <button type="submit" disabled={!auth}>Add to my dictionary</button>
      </form>
    </article>
  );
}

type CsvPanelProps = {
  auth?: AuthResponse;
  csvFile: File | null;
  importSummary: string;
  onCsvFileChange: (file: File | null) => void;
  onExport: () => void;
  onImport: () => void;
};

function CsvPanel({
  auth,
  csvFile,
  importSummary,
  onCsvFileChange,
  onExport,
  onImport
}: CsvPanelProps) {
  return (
    <article className="panel">
      <h2>CSV import / export</h2>
      <p className="helper-text">
        Import uses a CSV file only. Required columns: <code>English term</code>,{' '}
        <code>Russian translation(s)</code>, <code>Type</code>. Optional columns: <code>Notes</code>,{' '}
        <code>Tags</code>.
      </p>
      <label className="file-picker">
        <span>Choose CSV file</span>
        <input
          type="file"
          accept=".csv,text/csv"
          onChange={event => onCsvFileChange(event.target.files?.[0] ?? null)}
        />
      </label>
      <div className="actions-row">
        <button type="button" onClick={onImport} disabled={!auth || !csvFile}>Import CSV</button>
        <button type="button" className="secondary" onClick={onExport} disabled={!auth}>Export CSV</button>
      </div>
      {csvFile ? <p className="helper-text">Selected file: {csvFile.name}</p> : null}
      {importSummary ? <p className="helper-text">{importSummary}</p> : null}
    </article>
  );
}

type DictionaryPanelProps = {
  auth?: AuthResponse;
  customItemCount: number;
  dictionary: DictionaryItem[];
  onClearCustomData: () => void;
};

function DictionaryPanel({
  auth,
  customItemCount,
  dictionary,
  onClearCustomData
}: DictionaryPanelProps) {
  return (
    <section className="panel">
      <div className="section-header">
        <div>
          <h2>Dictionary</h2>
          <span>{dictionary.length} visible items, {customItemCount} custom</span>
        </div>
        <button
          type="button"
          className="secondary danger"
          onClick={onClearCustomData}
          disabled={!auth || customItemCount === 0}
        >
          Clear my custom data
        </button>
      </div>
      <div className="dictionary-list">
        {dictionary.map(item => (
          <article key={item.id} className="dictionary-row">
            <div>
              <strong>{item.englishText}</strong>
              <p>{item.russianGlosses.join(', ')}</p>
            </div>
            <div className="pill-row">
              <span>{item.sourceType}</span>
              <span>{item.itemKind}</span>
              <span>{item.difficulty}</span>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
