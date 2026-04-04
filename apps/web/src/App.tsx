import { useEffect, useState } from 'react';
import { api, type AuthResponse } from './api';
import { CsvPanel } from './components/CsvPanel';
import { DictionaryPanel } from './components/DictionaryPanel';
import { HeroSection } from './components/HeroSection';
import { ProgressPanel } from './components/ProgressPanel';
import { QuickAddPanel } from './components/QuickAddPanel';
import { StudyPanel } from './components/StudyPanel';
import { getAuthErrorMessage } from './lib/authErrors';
import {
  bootstrapSession,
  establishAuthenticatedSession,
  loadSession,
  loadStudyState,
  refreshAuthenticatedSession,
  resetAntiforgery
} from './lib/sessionApi';
import {
  buildQuickAddPayload,
  buildRefreshNotice,
  describeError,
  getCustomItemCount,
  initialAuthForm,
  initialQuickAddForm,
  initialState,
  isUnauthorized,
  type AuthFormState,
  type QuickAddFormState,
  type SessionSnapshot,
  type SessionState
} from './lib/session';
import './styles.css';

export default function App() {
  const [state, setState] = useState<SessionState>(initialState);
  const [authForm, setAuthForm] = useState<AuthFormState>(initialAuthForm);
  const [answer, setAnswer] = useState('');
  const [quickAddForm, setQuickAddForm] = useState<QuickAddFormState>(initialQuickAddForm);
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [importSummary, setImportSummary] = useState('');

  useEffect(() => {
    void bootstrap();
  }, []);

  async function bootstrap() {
    try {
      const auth = await bootstrapSession();
      await hydrate(auth);
    } catch (error) {
      if (isUnauthorized(error)) {
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Could not restore your session')
      }));
    }
  }

  async function hydrate(auth: AuthResponse, notice?: string) {
    const snapshot = await loadSession();
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

  async function moveToSignedOut(notice: string) {
    await resetAntiforgery();
    setAnswer('');
    setImportSummary('');
    setCsvFile(null);
    setState({
      ...initialState,
      notice
    });
  }

  async function refreshSession() {
    if (!state.auth) {
      return;
    }

    setAnswer('');
    setImportSummary('');
    setState(current => ({ ...current, result: undefined }));

    try {
      const { auth, snapshot } = await refreshAuthenticatedSession();
      setState(current => ({
        ...current,
        auth,
        dictionary: snapshot.dictionary,
        dashboard: snapshot.dashboard,
        card: snapshot.card,
        result: undefined,
        error: undefined,
        notice: buildRefreshNotice(current.card, snapshot),
        lastSyncedAt: new Date().toISOString()
      }));
    } catch (error) {
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Could not sync the session')
      }));
    }
  }

  async function signIn() {
    try {
      const auth = await establishAuthenticatedSession(api.signIn(authForm.email, authForm.password));
      await hydrate(auth, 'You are signed in and ready to study.');
    } catch (error) {
      const authError = getAuthErrorMessage(error, 'Sign-in failed');

      if (authError) {
        setState(current => ({
          ...current,
          error: authError
        }));
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Sign-in failed')
      }));
    }
  }

  async function signUp() {
    try {
      const auth = await establishAuthenticatedSession(api.signUp(authForm.email, authForm.password));
      await hydrate(auth, 'Your account is ready and you are signed in.');
    } catch (error) {
      const authError = getAuthErrorMessage(error, 'Sign-up failed');

      if (authError) {
        setState(current => ({
          ...current,
          error: authError
        }));
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Sign-up failed')
      }));
    }
  }

  async function signOut() {
    try {
      await api.signOut();
      await moveToSignedOut('Signed out.');
    } catch (error) {
      setState(current => ({
        ...current,
        error: describeError(error, 'Sign-out failed')
      }));
    }
  }

  async function addItem() {
    if (!state.auth) {
      return;
    }

    try {
      const beforeCount = getCustomItemCount(state.dictionary);
      const savedItem = await api.addDictionaryItem(buildQuickAddPayload(quickAddForm));
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
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Could not add item')
      }));
    }
  }

  async function importItems() {
    if (!state.auth || !csvFile) {
      return;
    }

    try {
      const content = await csvFile.text();
      const result = await api.importCsv(csvFile.name, content);
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
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Import failed')
      }));
    }
  }

  async function submitAnswer() {
    if (!state.auth || !state.card) {
      return;
    }

    try {
      const result = await api.submitAnswer(state.card.itemId, answer);
      const studyState = await loadStudyState();
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
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Answer submission failed')
      }));
    }
  }

  async function exportItems() {
    if (!state.auth) {
      return;
    }

    try {
      const csv = await api.exportCsv();
      await navigator.clipboard.writeText(csv);
      setState(current => ({
        ...current,
        error: undefined,
        notice: 'Export copied to clipboard.'
      }));
    } catch (error) {
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Export failed')
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
      await api.clearCustomData();
      setAnswer('');
      setImportSummary('');
      setCsvFile(null);
      setState(current => ({ ...current, result: undefined }));
      await hydrate(
        state.auth,
        'Cleared your custom words, imports, and progress. Base study content is still available.'
      );
    } catch (error) {
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Could not clear custom data')
      }));
    }
  }

  async function reportIssue() {
    if (!state.auth || !state.card) {
      return;
    }

    try {
      await api.reportIssue(state.card.itemId, 'awkward sentence', 'Reported from study card UI');
      setAnswer('');
      setImportSummary('');
      setState(current => ({ ...current, result: undefined }));
      await hydrate(state.auth, 'Thanks. We hid that card for now and loaded a different one.');
    } catch (error) {
      if (isUnauthorized(error)) {
        await moveToSignedOut('Your session expired. Sign in again.');
        return;
      }

      setState(current => ({
        ...current,
        error: describeError(error, 'Issue report failed')
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
        onSignOut={() => void signOut()}
        onSignUp={() => void signUp()}
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
