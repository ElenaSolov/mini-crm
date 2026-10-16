import StatusBadge from '../../../components/StatusBadge';
import { errorMessage } from '../../../utils/apiError';
import { formatAmount, formatDate } from '../../../utils/format';
import { useDeleteLeadMutation } from '../api/leadsApi';
import type { Lead, LeadStatus } from '../types';
import EventLog from './EventLog';
import StatusChange from './StatusChange';

interface Props {
  lead: Lead;
  statuses: LeadStatus[];
  onEdit: () => void;
  onClose: () => void; // also called after a delete
}

export default function LeadDetail({ lead, statuses, onEdit, onClose }: Readonly<Props>) {
  const [deleteLead, { error: deleteError, isLoading: deleting }] = useDeleteLeadMutation();

  async function handleDelete() {
    if (!window.confirm(`Delete lead #${lead.id} (${lead.name})?`)) return;
    try {
      await deleteLead(lead.id).unwrap();
      onClose();
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  return (
    <div className="lead-detail">
      <div className="panel-header">
        <h2>Lead #{lead.id}</h2>
        <button onClick={onClose} aria-label="Close">×</button>
      </div>

      {deleteError && <p className="error">{errorMessage(deleteError)}</p>}

      <dl className="fields">
        <dt>Name</dt><dd>{lead.name}</dd>
        <dt>Email</dt><dd>{lead.email}</dd>
        <dt>Phone</dt><dd>{lead.phone}</dd>
        <dt>Source</dt><dd>{lead.source}</dd>
        <dt>Status</dt><dd><StatusBadge status={lead.status} /></dd>
        <dt>Outbound</dt>
        <dd>{lead.outbound_status ? <StatusBadge status={lead.outbound_status} /> : 'Not sent yet'}</dd>
        <dt>Amount</dt><dd>{formatAmount(lead.amount, lead.currency)}</dd>
        <dt>Created</dt><dd>{formatDate(lead.created_at)}</dd>
        <dt>Updated</dt><dd>{formatDate(lead.updated_at)}</dd>
      </dl>

      <div className="actions">
        <button onClick={onEdit} disabled={deleting}>Edit</button>
        <button className="danger" onClick={handleDelete} disabled={deleting}>Delete</button>
      </div>

      <StatusChange lead={lead} statuses={statuses} />

      <section>
        <h3>Tracker events</h3>
        <EventLog leadId={lead.id} />
      </section>
    </div>
  );
}
