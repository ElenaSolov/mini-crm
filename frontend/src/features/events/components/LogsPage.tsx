import { useEffect, useState } from 'react';
import Pagination, { DEFAULT_PAGE_SIZE } from '../../../components/Pagination';
import { errorMessage } from '../../../utils/apiError';
import { formatStatus } from '../../../utils/format';
import { EventCard, type TrackerEventStatus } from '../../leads';
import { useGetEventsQuery } from '../api/eventsApi';

const eventStatuses: TrackerEventStatus[] = ['pending', 'sent', 'failed'];
const pollEveryMs = 3000;

// Every outbound event of every lead, newest first, with all attempts.
export default function LogsPage() {
  const [status, setStatus] = useState<TrackerEventStatus | ''>('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  // While an event on this page is pending, reload often to show new attempts.
  const [pollingInterval, setPollingInterval] = useState(0);
  const { data, error, isLoading } = useGetEventsQuery(
    { status: status || undefined, page, page_size: pageSize },
    { pollingInterval, skipPollingIfUnfocused: true },
  );
  const hasPendingEvent = data?.items.some((item) => item.event.status === 'pending') ?? false;
  useEffect(() => setPollingInterval(hasPendingEvent ? pollEveryMs : 0), [hasPendingEvent]);

  const pageCount = Math.max(1, Math.ceil((data?.total_count ?? 0) / pageSize));

  const changeStatus = (newStatus: TrackerEventStatus | '') => {
    setStatus(newStatus);
    setPage(1);
  };
  const changePageSize = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPage(1);
  };

  return (
    <main className="main logs">
      <header className="page-header">
        <h1>Logs</h1>
      </header>
      <p className="hint">Every conversion sent to the tracker: the request, the answer, and each retry.</p>

      <div className="filters">
        <label>
          <span>Outbound status</span>
          <select value={status} onChange={(e) => changeStatus(e.target.value as TrackerEventStatus | '')}>
            <option value="">All</option>
            {eventStatuses.map((s) => (
              <option key={s} value={s}>{formatStatus(s)}</option>
            ))}
          </select>
        </label>
      </div>

      {error && <p className="error">{errorMessage(error)}</p>}
      {isLoading && <p className="empty">Loading…</p>}
      {data && data.items.length === 0 && <p className="empty">No events yet. Converting a lead creates one.</p>}

      {data && data.items.length > 0 && (
        <>
          {data.items.map(({ lead_name, event }) => (
            <EventCard
              key={event.id}
              event={event}
              subtitle={`Lead #${event.lead_id}${lead_name ? ` · ${lead_name}` : ''}`}
            />
          ))}
          <Pagination
            page={page}
            pageCount={pageCount}
            totalCount={data.total_count}
            onPageChange={setPage}
            pageSize={pageSize}
            onPageSizeChange={changePageSize}
          />
        </>
      )}
    </main>
  );
}
