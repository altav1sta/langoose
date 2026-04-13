import type { AuthResponse, DictionaryListItem } from '../api';

function statusPillClass(entry: DictionaryListItem): string {
  if (entry.isPublic) return '';
  if (entry.enrichmentStatus === 'pending') return 'pill-pending';
  if (entry.enrichmentStatus === 'enriched') return 'pill-enriched';
  if (entry.enrichmentStatus === 'providerError') return 'pill-failed';
  if (entry.enrichmentStatus === 'invalidSource'
    || entry.enrichmentStatus === 'invalidTarget'
    || entry.enrichmentStatus === 'invalidLink') return 'pill-warning';
  return '';
}

type DictionaryPanelProps = {
  auth?: AuthResponse;
  customEntryCount: number;
  dictionary: DictionaryListItem[];
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
          <article key={entry.userDictionaryEntryId ?? entry.dictionaryEntryId} className="dictionary-row">
            <div>
              <strong>{entry.text}</strong>
            </div>
            <div className="pill-row">
              <span className={statusPillClass(entry)}>{entry.isPublic ? 'base' : entry.enrichmentStatus ?? 'custom'}</span>
              <span>{entry.partOfSpeech ?? 'noun'}</span>
              {entry.difficulty ? <span>{entry.difficulty}</span> : null}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
