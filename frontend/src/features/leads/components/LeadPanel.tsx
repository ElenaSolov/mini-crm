import { useState } from 'react';
import { errorMessage } from '../../../utils/apiError';
import { useGetLeadQuery } from '../api/leadsApi';
import type { LeadStatus } from '../types';
import LeadDetail from './LeadDetail';
import LeadForm from './LeadForm';

interface Props {
  leadId: number;
  statuses: LeadStatus[];
  onClose: () => void;
}

// Loads one lead and shows either its details or the edit form.
export default function LeadPanel({ leadId, statuses, onClose }: Readonly<Props>) {
  const [editing, setEditing] = useState(false);
  const { data: lead, error } = useGetLeadQuery(leadId);

  if (error) {
    return (
      <>
        <p className="error">{errorMessage(error)}</p>
        <button onClick={onClose}>Close</button>
      </>
    );
  }
  if (!lead)
    return <p className="empty">Loading…</p>;

  if (editing)
    return <LeadForm lead={lead} onSaved={() => setEditing(false)} onCancel={() => setEditing(false)} />;

  return (
    <LeadDetail
      lead={lead}
      statuses={statuses}
      onEdit={() => setEditing(true)}
      onClose={onClose}
    />
  );
}
