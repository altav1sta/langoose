import type { StudyAnswerResult, StudyCard } from '../api';
import { feedbackLabel, verdictClassName, verdictLabel } from '../lib/session';

type StudyPanelProps = {
  answer: string;
  card?: StudyCard;
  result?: StudyAnswerResult;
  onAnswerChange: (value: string) => void;
  onReportIssue: () => void;
  onSubmit: () => void;
};

export function StudyPanel({
  answer,
  card,
  result,
  onAnswerChange,
  onReportIssue,
  onSubmit
}: StudyPanelProps) {
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
          {card.sentenceTranslation ? <p className="hint">{card.sentenceTranslation}</p> : null}
          {card.translations.length > 0 ? (
            <p className="translations">{card.translations.join(', ')}</p>
          ) : null}
          {card.grammarHint ? <p className="grammar-hint">{card.grammarHint}</p> : null}
          {card.difficulty ? <span className="difficulty-badge">{card.difficulty}</span> : null}
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
