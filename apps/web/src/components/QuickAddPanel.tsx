import type { Dispatch, SetStateAction } from 'react';
import type { AuthResponse } from '../api';
import type { QuickAddFormState } from '../lib/session';

type QuickAddPanelProps = {
  auth?: AuthResponse;
  form: QuickAddFormState;
  onFormChange: Dispatch<SetStateAction<QuickAddFormState>>;
  onSubmit: () => void;
};

export function QuickAddPanel({ auth, form, onFormChange, onSubmit }: QuickAddPanelProps) {
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
