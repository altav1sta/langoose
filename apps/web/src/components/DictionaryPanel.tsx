import type { AuthResponse, UserDictionaryEntry } from '../api';

type DictionaryPanelProps = {
  auth?: AuthResponse;
  customEntryCount: number;
  dictionary: UserDictionaryEntry[];
  onClearCustomData: () => void;
};

export function DictionaryPanel({
  auth,
  customEntryCount,
  dictionary,
  onClearCustomData
}: DictionaryPanelProps) {
  return (
    <section className="panel">
      <div className="section-header">
        <div>
          <h2>Dictionary</h2>
          <span>{dictionary.length} entries, {customEntryCount} custom</span>
        </div>
        <button
          type="button"
          className="secondary danger"
          onClick={onClearCustomData}
          disabled={!auth || customEntryCount === 0}
        >
          Clear my custom data
        </button>
      </div>
      <div className="dictionary-list">
        {dictionary.map(entry => (
          <article key={entry.id} className="dictionary-row">
            <div>
              <strong>{entry.userInputTerm}</strong>
            </div>
            <div className="pill-row">
              <span>{entry.enrichmentStatus}</span>
              <span>{entry.type ?? 'word'}</span>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
