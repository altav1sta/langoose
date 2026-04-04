import type { Dispatch, SetStateAction } from 'react';
import type { AuthResponse } from '../api';
import type { AuthFormState } from '../lib/session';
import { formatTime } from '../lib/session';

type HeroSectionProps = {
  auth?: AuthResponse;
  authForm: AuthFormState;
  lastSyncedAt?: string;
  onAuthChange: Dispatch<SetStateAction<AuthFormState>>;
  onRefresh: () => void;
  onSignIn: () => void;
  onSignOut: () => void;
  onSignUp: () => void;
};

export function HeroSection({
  auth,
  authForm,
  lastSyncedAt,
  onAuthChange,
  onRefresh,
  onSignIn,
  onSignOut,
  onSignUp
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
            type="password"
            value={authForm.password}
            onChange={event => onAuthChange(current => ({ ...current, password: event.target.value }))}
            placeholder="Password"
          />
          <div className="actions-row">
            <button type="submit">Sign in</button>
            <button type="button" className="secondary" onClick={onSignUp}>Create account</button>
          </div>
        </form>
      ) : (
        <div className="welcome-panel">
          <span>{auth.email}</span>
          <div className="actions-row">
            <button type="button" onClick={onRefresh}>Sync session</button>
            <button type="button" className="secondary" onClick={onSignOut}>Sign out</button>
          </div>
          <p className="helper-text">
            Sync refreshes the dashboard and reloads whichever card is currently next in your study queue.
          </p>
          <p className="sync-meta">Last synced: {formatTime(lastSyncedAt)}</p>
        </div>
      )}
    </section>
  );
}
