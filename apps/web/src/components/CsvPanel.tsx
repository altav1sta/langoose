import type { AuthResponse } from '../api';

type CsvPanelProps = {
  auth?: AuthResponse;
  csvFile: File | null;
  importSummary: string;
  onCsvFileChange: (file: File | null) => void;
  onExport: () => void;
  onImport: () => void;
};

export function CsvPanel({
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
