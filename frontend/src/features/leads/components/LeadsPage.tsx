import { useState } from 'react';
import { useDebounce } from '../../../hooks/useDebounce';
import { errorMessage } from '../../../utils/apiError';
import { useGetLeadsQuery, useGetStatusesQuery } from '../api/leadsApi';
import LeadFilters from './LeadFilters';
import LeadsTable from './LeadsTable';
import LeadForm from './LeadForm';
import LeadPanel from './LeadPanel';

// What the side panel shows. It keeps only the lead id: the panel loads the
// lead itself, so it stays open even when the lead leaves the current filter.
type Panel =
  | { mode: 'closed' }
  | { mode: 'create' }
  | { mode: 'lead'; leadId: number };

export default function LeadsPage() {
  const [status, setStatus] = useState('');
  const [source, setSource] = useState('');
  const debouncedSource = useDebounce(source, 300);
  const [panel, setPanel] = useState<Panel>({ mode: 'closed' });

  const leads = useGetLeadsQuery({ status: status || undefined, source: debouncedSource || undefined });
  const statuses = useGetStatusesQuery();

  const openLead = (leadId: number) => setPanel({ mode: 'lead', leadId });
  const closePanel = () => setPanel({ mode: 'closed' });

  return (
    <>
      <main className="main">
        <header className="page-header">
          <h1>Leads</h1>
          <button className="primary" onClick={() => setPanel({ mode: 'create' })}>
            New lead
          </button>
        </header>

        <LeadFilters
          statuses={statuses.data ?? []}
          status={status}
          source={source}
          onStatusChange={setStatus}
          onSourceChange={setSource}
        />

        {leads.error && <p className="error">{errorMessage(leads.error)}</p>}
        {statuses.error && <p className="error">{errorMessage(statuses.error)}</p>}

        {leads.isLoading && <p className="empty">Loading…</p>}
        {/* Only with data: after a failed load, "No leads found" would be wrong. */}
        {leads.data && (
          <LeadsTable
            leads={leads.data}
            selectedId={panel.mode === 'lead' ? panel.leadId : null}
            onSelect={(lead) => openLead(lead.id)}
          />
        )}
      </main>

      {panel.mode !== 'closed' && (
        <aside className="panel">
          {panel.mode === 'create' && (
            <LeadForm onSaved={(lead) => openLead(lead.id)} onCancel={closePanel} />
          )}
          {panel.mode === 'lead' && (
            <LeadPanel
              key={panel.leadId}
              leadId={panel.leadId}
              statuses={statuses.data ?? []}
              onClose={closePanel}
            />
          )}
        </aside>
      )}
    </>
  );
}
