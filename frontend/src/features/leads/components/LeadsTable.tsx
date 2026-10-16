import StatusBadge from '../../../components/StatusBadge';
import { formatAmount, formatDate } from '../../../utils/format';
import type { Lead } from '../types';

interface Props {
  leads: Lead[];
  selectedId: number | null;
  onSelect: (lead: Lead) => void;
}

export default function LeadsTable({ leads, selectedId, onSelect }: Readonly<Props>) {
  if (leads.length === 0)
    return <p className="empty">No leads found.</p>;

  return (
    <table className="leads-table">
      <thead>
        <tr>
          <th>ID</th>
          <th>Name</th>
          <th>Contact</th>
          <th>Source</th>
          <th>Status</th>
          <th>Amount</th>
          <th>Created</th>
        </tr>
      </thead>
      <tbody>
        {leads.map((lead) => (
          <tr
            key={lead.id}
            className={lead.id === selectedId ? 'selected' : undefined}
            onClick={() => onSelect(lead)}
          >
            <td>{lead.id}</td>
            <td>{lead.name}</td>
            <td>{lead.email ?? lead.phone}</td>
            <td>{lead.source}</td>
            <td><StatusBadge status={lead.status} /></td>
            <td>{formatAmount(lead.amount, lead.currency)}</td>
            <td>{formatDate(lead.created_at)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
