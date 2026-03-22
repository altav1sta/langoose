import { useEffect, useState } from 'react';
import { api, type AuthResponse, type Dashboard, type DictionaryItem, type StudyAnswerResult, type StudyCard } from './api';
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

const initialState: SessionState = {
  dictionary: []
};

function verdictClassName(verdict: StudyAnswerResult['verdict'] | undefined) {
  if (!verdict) {
    return '';
  }

  return String(verdict).toLowerCase();
}

function verdictLabel(verdict: StudyAnswerResult['verdict'] | undefined) {
  if (verdict === 'Correct') {
    return 'Correct';
  }

  if (verdict === 'AlmostCorrect') {
    return 'Almost correct';
  }

  return 'Try again';
}

function feedbackLabel(result: StudyAnswerResult) {
  const code = String(result.feedbackCode);
  switch (code) {
    case 'ExactMatch':
      return 'Perfect. That answer matches the expected wording.';
    case 'AcceptedVariant':
      return 'Close enough. We accepted a valid variant of the target answer.';
    case 'MissingArticle':
      return 'Almost there. The meaning is right, but the article is missing or different.';
    case 'InflectionMismatch':
      return 'Almost there. The base meaning is correct, but the word form is slightly off.';
    case 'MinorTypo':
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

export default function App() {
  const [state, setState] = useState<SessionState>(initialState);
  const [email, setEmail] = useState('learner@example.com');
  const [name, setName] = useState('Learner');
  const [answer, setAnswer] = useState('');
  const [englishText, setEnglishText] = useState('look for');
  const [russianText, setRussianText] = useState('\u0438\u0441\u043a\u0430\u0442\u044c');
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [importSummary, setImportSummary] = useState<string>('');

  useEffect(() => {
    const stored = window.localStorage.getItem('langoose-auth');
    if (!stored) {
      return;
    }

    const auth = JSON.parse(stored) as AuthResponse;
    hydrate(auth).catch((error: Error) => setState(current => ({ ...current, error: error.message })));
  }, []);

  async function loadSession(auth: AuthResponse): Promise<SessionSnapshot> {
    const [dictionary, dashboard] = await Promise.all([
      api.getDictionary(auth.token),
      api.getDashboard(auth.token)
    ]);

    let card: StudyCard | undefined;
    try {
      card = await api.getNextCard(auth.token);
    } catch {
      card = undefined;
    }

    return { dictionary, dashboard, card };
  }

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
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Could not sync the session' }));
    }
  }

  async function signIn() {
    try {
      const auth = await api.signIn(email, name);
      window.localStorage.setItem('langoose-auth', JSON.stringify(auth));
      await hydrate(auth, 'You are signed in and ready to study.');
    } catch (error) {
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Sign-in failed' }));
    }
  }

  async function addItem() {
    if (!state.auth) {
      return;
    }

    try {
      const beforeCount = state.dictionary.filter(item => item.ownerId).length;
      const savedItem = await api.addDictionaryItem(state.auth.token, {
        englishText,
        russianGlosses: russianText.split(',').map(value => value.trim()).filter(Boolean),
        itemKind: englishText.includes(' ') ? 'phrase' : 'word',
        createdByFlow: 'quick-add'
      });

      const snapshot = await hydrate(state.auth);
      const afterCount = snapshot.dictionary.filter(item => item.ownerId).length;
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
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Could not add item' }));
    }
  }

  async function importItems() {
    if (!state.auth || !csvFile) {
      return;
    }

    try {
      const content = await csvFile.text();
      const result = await api.importCsv(state.auth.token, csvFile.name, content);
      setImportSummary(`Imported ${result.importedRows} of ${result.totalRows} rows${result.skippedRows ? `, skipped ${result.skippedRows} rows that were already in your custom or base dictionary` : ''}.`);
      setCsvFile(null);
      await hydrate(state.auth, result.skippedRows > 0
        ? 'Import finished. Existing custom entries and base terms were skipped so the dictionary stays clean.'
        : 'Import finished. New rows were added to your custom dictionary.');
    } catch (error) {
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Import failed' }));
    }
  }

  async function submitAnswer() {
    if (!state.auth || !state.card) {
      return;
    }

    try {
      const result = await api.submitAnswer(state.auth.token, state.card.itemId, answer);
      setAnswer('');
      const dashboard = await api.getDashboard(state.auth.token);
      let card: StudyCard | undefined;
      try {
        card = await api.getNextCard(state.auth.token);
      } catch {
        card = undefined;
      }

      setState(current => ({ ...current, dashboard, card, result, notice: 'Answer saved.', lastSyncedAt: new Date().toISOString() }));
    } catch (error) {
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Answer submission failed' }));
    }
  }

  async function exportItems() {
    if (!state.auth) {
      return;
    }

    try {
      const csv = await api.exportCsv(state.auth.token);
      await navigator.clipboard.writeText(csv);
      setState(current => ({ ...current, notice: 'Export copied to clipboard.' }));
    } catch (error) {
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Export failed' }));
    }
  }

  async function clearCustomData() {
    if (!state.auth) {
      return;
    }

    const confirmed = window.confirm('Clear all custom words, imports, and progress for your account? Base content will stay available.');
    if (!confirmed) {
      return;
    }

    try {
      await api.clearCustomData(state.auth.token);
      setAnswer('');
      setImportSummary('');
      setCsvFile(null);
      setState(current => ({ ...current, result: undefined }));
      await hydrate(state.auth, 'Cleared your custom words, imports, and progress. Base study content is still available.');
    } catch (error) {
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Could not clear custom data' }));
    }
  }

  async function reportIssue() {
    if (!state.auth || !state.card) {
      return;
    }

    try {
      await api.reportIssue(state.auth.token, state.card.itemId, 'awkward sentence', 'Reported from study card UI');
      setAnswer('');
      setState(current => ({ ...current, result: undefined }));
      await hydrate(state.auth, 'Thanks. We hid that card for now and loaded a different one.');
      setImportSummary('');
    } catch (error) {
      setState(current => ({ ...current, error: error instanceof Error ? error.message : 'Issue report failed' }));
    }
  }

  const customItemCount = state.dictionary.filter(item => item.ownerId).length;

  return (
    <main className="app-shell">
      <section className="hero-card">
        <div>
          <p className="eyebrow">Russian speakers learning English</p>
          <h1>Langoose</h1>
          <p className="lede">A sentence-based learning loop with a private dictionary layered into the base training set.</p>
        </div>
        {!state.auth ? (
          <form className="auth-panel" onSubmit={event => { event.preventDefault(); void signIn(); }}>
            <input value={email} onChange={event => setEmail(event.target.value)} placeholder="Email" />
            <input value={name} onChange={event => setName(event.target.value)} placeholder="Name" />
            <button type="submit">Start learning</button>
          </form>
        ) : (
          <div className="welcome-panel">
            <span>{state.auth.name}</span>
            <button type="button" onClick={() => void refreshSession()}>Sync session</button>
            <p className="helper-text">Sync refreshes the dashboard and reloads whichever card is currently next in your study queue.</p>
            <p className="sync-meta">Last synced: {formatTime(state.lastSyncedAt)}</p>
          </div>
        )}
      </section>

      {state.error ? <p className="error-banner">{state.error}</p> : null}
      {state.notice ? <p className="notice-banner">{state.notice}</p> : null}

      <section className="grid two-up">
        <article className="panel stats-panel">
          <h2>Progress</h2>
          <div className="stats-grid">
            <Metric label="Total items" value={state.dashboard?.totalItems ?? 0} />
            <Metric label="Due now" value={state.dashboard?.dueNow ?? 0} />
            <Metric label="New" value={state.dashboard?.newItems ?? 0} />
            <Metric label="Custom" value={state.dashboard?.customItems ?? 0} />
          </div>
        </article>

        <article className="panel study-panel">
          <h2>Study</h2>
          {state.card ? (
            <form onSubmit={event => { event.preventDefault(); void submitAnswer(); }}>
              <p className="prompt">{state.card.prompt}</p>
              <p className="hint">{state.card.translationHint}</p>
              <input value={answer} onChange={event => setAnswer(event.target.value)} placeholder="Type the missing English word or phrase" />
              <div className="actions-row">
                <button type="submit">Check answer</button>
                <button type="button" className="secondary" onClick={() => void reportIssue()}>Report issue</button>
              </div>
            </form>
          ) : (
            <p>No due cards right now.</p>
          )}
          {state.result ? (
            <div className={`result-chip ${verdictClassName(state.result.verdict)}`}>
              <strong>{verdictLabel(state.result.verdict)}</strong>
              <span>{feedbackLabel(state.result)}</span>
              <span>Expected answer: {state.result.expectedAnswer}</span>
            </div>
          ) : null}
        </article>
      </section>

      <section className="grid two-up">
        <article className="panel">
          <h2>Quick add</h2>
          <form onSubmit={event => { event.preventDefault(); void addItem(); }}>
            <input value={englishText} onChange={event => setEnglishText(event.target.value)} placeholder="English word or phrase" />
            <input value={russianText} onChange={event => setRussianText(event.target.value)} placeholder="Russian glosses, comma separated" />
            <button type="submit" disabled={!state.auth}>Add to my dictionary</button>
          </form>
        </article>

        <article className="panel">
          <h2>CSV import / export</h2>
          <p className="helper-text">Import uses a CSV file only. Required columns: <code>English term</code>, <code>Russian translation(s)</code>, <code>Type</code>. Optional columns: <code>Notes</code>, <code>Tags</code>.</p>
          <label className="file-picker">
            <span>Choose CSV file</span>
            <input
              type="file"
              accept=".csv,text/csv"
              onChange={event => setCsvFile(event.target.files?.[0] ?? null)}
            />
          </label>
          <div className="actions-row">
            <button type="button" onClick={() => void importItems()} disabled={!state.auth || !csvFile}>Import CSV</button>
            <button type="button" className="secondary" onClick={() => void exportItems()} disabled={!state.auth}>Export CSV</button>
          </div>
          {csvFile ? <p className="helper-text">Selected file: {csvFile.name}</p> : null}
          {importSummary ? <p className="helper-text">{importSummary}</p> : null}
        </article>
      </section>

      <section className="panel">
        <div className="section-header">
          <div>
            <h2>Dictionary</h2>
            <span>{state.dictionary.length} visible items, {customItemCount} custom</span>
          </div>
          <button type="button" className="secondary danger" onClick={() => void clearCustomData()} disabled={!state.auth || customItemCount === 0}>Clear my custom data</button>
        </div>
        <div className="dictionary-list">
          {state.dictionary.map(item => (
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
    </main>
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
