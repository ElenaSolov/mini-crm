import type { LeadStatus } from '../types';

interface Props {
  statuses: LeadStatus[];
  status: string;
  source: string;
  onStatusChange: (status: string) => void;
  onSourceChange: (source: string) => void;
}

export default function LeadFilters({ statuses, status, source, onStatusChange, onSourceChange }: Readonly<Props>) {
  return (
    <div className="filters">
      <label>
        <span>Status</span>
        <select value={status} onChange={(e) => onStatusChange(e.target.value)}>
          <option value="">All</option>
          {statuses.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </label>
      <label>
        <span>Source</span>
        <input
          placeholder="e.g. facebook"
          value={source}
          onChange={(e) => onSourceChange(e.target.value)}
        />
      </label>
    </div>
  );
}
