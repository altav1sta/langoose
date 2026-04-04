import type { AuthResponse, DictionaryItem } from '../api';

type DictionaryPanelProps = {
  auth?: AuthResponse;
  customItemCount: number;
  dictionary: DictionaryItem[];
  onClearCustomData: () => void;
};

export function DictionaryPanel({
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
