import { useEffect, useState } from 'react';
import { useDebounce } from '../../../hooks/useDebounce';
import { errorMessage } from '../../../utils/apiError';
import { useGetLeadsQuery, useGetStatusesQuery } from '../api/leadsApi';
import Pagination from '../../../components/Pagination';
import type { LeadFilterValues, LeadsQuery } from '../types';
import LeadFilters, { emptyFilters } from './LeadFilters';
import LeadsTable from './LeadsTable';
import LeadForm from './LeadForm';
import LeadPanel from './LeadPanel';

const PAGE_SIZE = 20;

// Drops empty filters, so the URL only has the ones in use.
function toQuery(filters: LeadFilterValues, page: number): LeadsQuery {
  const used = Object.entries(filters).filter(([, value]) => value.trim() !== '');
  const query: LeadsQuery = { ...Object.fromEntries(used), page, page_size: PAGE_SIZE };

  // The date inputs give days in the user's time zone. The API wants exact moments:
  // the start of the "from" day and the end of the "to" day, in the user's time zone.
  // (A date-time without "Z" is read as local time.)
  if (query.created_from) query.created_from = new Date(`${query.created_from}T00:00:00`).toISOString();
  if (query.created_to) query.created_to = new Date(`${query.created_to}T23:59:59.999`).toISOString();

  return query;
}

// What the side panel shows. It keeps only the lead id: the panel loads the
// lead itself, so it stays open even when the lead leaves the current filter.
type Panel =
  | { mode: 'closed' }
  | { mode: 'create' }
  | { mode: 'lead'; leadId: number };

export default function LeadsPage() {
  const [filters, setFilters] = useState(emptyFilters);
  const debouncedFilters = useDebounce(filters, 300);
  const [page, setPage] = useState(1);
  const [panel, setPanel] = useState<Panel>({ mode: 'closed' });

  const leads = useGetLeadsQuery(toQuery(debouncedFilters, page));
  const statuses = useGetStatusesQuery();
  const pageCount = Math.max(1, Math.ceil((leads.data?.total_count ?? 0) / PAGE_SIZE));

  // After deleting the only lead on the last page, that page is empty: go back one.
  // currentData is only set for the current filters and page, never for older ones.
  useEffect(() => {
    if (leads.currentData && page > pageCount) setPage(pageCount);
  }, [leads.currentData, page, pageCount]);

  // New filters give a new result, so start again from page 1.
  const changeFilters = (newFilters: LeadFilterValues) => {
    setFilters(newFilters);
    setPage(1);
  };

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
          filters={filters}
          onChange={changeFilters}
        />

        {leads.error && <p className="error">{errorMessage(leads.error)}</p>}
        {statuses.error && <p className="error">{errorMessage(statuses.error)}</p>}

        {leads.isLoading && <p className="empty">Loading…</p>}
        {/* Only with data: after a failed load, "No leads found" would be wrong. */}
        {leads.data && (
          <>
            <LeadsTable
              leads={leads.data.items}
              selectedId={panel.mode === 'lead' ? panel.leadId : null}
              onSelect={(lead) => openLead(lead.id)}
            />
            <Pagination
              page={page}
              pageCount={pageCount}
              totalCount={leads.data.total_count}
              onPageChange={setPage}
            />
          </>
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
