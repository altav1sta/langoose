import type { Dashboard } from '../api';
import { Metric } from './Metric';

type ProgressPanelProps = {
  dashboard?: Dashboard;
};

export function ProgressPanel({ dashboard }: ProgressPanelProps) {
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
