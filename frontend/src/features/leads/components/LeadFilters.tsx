import type { LeadFilterValues, LeadStatus } from '../types';

export const emptyFilters: LeadFilterValues = {
  id: '',
  name: '',
  email: '',
  phone: '',
  source: '',
  status: '',
  currency: '',
  amount_from: '',
  amount_to: '',
  created_from: '',
  created_to: '',
};

interface Props {
  statuses: LeadStatus[];
  filters: LeadFilterValues;
  onChange: (filters: LeadFilterValues) => void;
}

// Text fields match part of the value: "ann" finds "Joanna".
const textFields: { key: keyof LeadFilterValues; label: string; placeholder: string }[] = [
  { key: 'name', label: 'Name', placeholder: 'e.g. ann' },
  { key: 'email', label: 'Email', placeholder: 'e.g. gmail' },
  { key: 'phone', label: 'Phone', placeholder: 'e.g. 555' },
  { key: 'source', label: 'Source', placeholder: 'e.g. face' },
  { key: 'currency', label: 'Currency', placeholder: 'e.g. USD' },
];

export default function LeadFilters({ statuses, filters, onChange }: Readonly<Props>) {
  const set = (key: keyof LeadFilterValues, value: string) => onChange({ ...filters, [key]: value });

  return (
    <div className="filters">
      <label className="filter-short">
        <span>ID</span>
        <input type="number" min={1} value={filters.id} onChange={(e) => set('id', e.target.value)} />
      </label>

      {textFields.map(({ key, label, placeholder }) => (
        <label key={key}>
          <span>{label}</span>
          <input placeholder={placeholder} value={filters[key]} onChange={(e) => set(key, e.target.value)} />
        </label>
      ))}

      <label>
        <span>Status</span>
        <select value={filters.status} onChange={(e) => set('status', e.target.value)}>
          <option value="">All</option>
          {statuses.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </label>

      <label className="filter-short">
        <span>Amount from</span>
        <input type="number" min={0} value={filters.amount_from} onChange={(e) => set('amount_from', e.target.value)} />
      </label>
      <label className="filter-short">
        <span>Amount to</span>
        <input type="number" min={0} value={filters.amount_to} onChange={(e) => set('amount_to', e.target.value)} />
      </label>

      <label>
        <span>Created from</span>
        <input type="date" value={filters.created_from} onChange={(e) => set('created_from', e.target.value)} />
      </label>
      <label>
        <span>Created to</span>
        <input type="date" value={filters.created_to} onChange={(e) => set('created_to', e.target.value)} />
      </label>

      <button className="filters-clear" onClick={() => onChange(emptyFilters)}>
        Clear
      </button>
    </div>
  );
}
