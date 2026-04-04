type MetricProps = {
  label: string;
  value: number;
};

export function Metric({ label, value }: MetricProps) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
