import { useEffect, useState } from 'react';
import StatusBadge from '../../../components/StatusBadge';
import { errorMessage } from '../../../utils/apiError';
import { formatDate } from '../../../utils/format';
import { useGetLeadEventsQuery } from '../api/leadsApi';

// While an event is pending, the worker is still sending it, so reload often
// to show new attempts as they happen.
const pollEveryMs = 3000;

// Every outbound event with all its attempts: what was sent and what the
// tracker answered. Bodies are shown as plain text, never as HTML.
export default function EventLog({ leadId }: Readonly<{ leadId: number }>) {
  const [pollingInterval, setPollingInterval] = useState(0);
  // skipPollingIfUnfocused: no polling while the browser tab is in the background.
  const { data: events = [], error } = useGetLeadEventsQuery(leadId, {
    pollingInterval,
    skipPollingIfUnfocused: true,
  });

  const hasPendingEvent = events.some((event) => event.status === 'pending');
  useEffect(() => setPollingInterval(hasPendingEvent ? pollEveryMs : 0), [hasPendingEvent]);

  if (error)
    return <p className="error">{errorMessage(error)}</p>;
  if (events.length === 0)
    return <p className="empty">No events yet. Converting the lead creates one.</p>;

  return (
    <div className="event-log">
      {events.map((event) => (
        <article key={event.id} className="event">
          <div className="event-header">
            <strong>{event.event_id}</strong>
            <StatusBadge status={event.status} />
          </div>
          <p className="muted">
            {event.method} {event.url}
            <br />
            Attempts: {event.attempt_count}
            {event.status === 'pending' && event.next_attempt_at &&
              ` · next try ${formatDate(event.next_attempt_at)}`}
          </p>
          {event.last_error && <p className="error">{event.last_error}</p>}

          <details>
            <summary>Request body</summary>
            <pre>{event.body}</pre>
          </details>

          <ol className="attempts">
            {event.attempts.map((attempt) => (
              <li key={attempt.id}>
                <div>
                  <span className={`code ${isSuccess(attempt.status_code) ? 'ok' : 'fail'}`}>
                    {attempt.status_code ?? 'no answer'}
                  </span>{' '}
                  {formatDate(attempt.attempted_at)}
                </div>
                {attempt.error && <p className="error">{attempt.error}</p>}
                <details>
                  <summary>Request and response</summary>
                  <p className="muted">Request: {attempt.url}</p>
                  <pre>{attempt.request_body}</pre>
                  <p className="muted">Response:</p>
                  <pre>{attempt.response_body}</pre>
                </details>
              </li>
            ))}
          </ol>
        </article>
      ))}
    </div>
  );
}

// Only colors the code. Whether to retry is the backend's call: the event's
// status (pending / failed) shows what it decided.
function isSuccess(statusCode: number | null) {
  return statusCode !== null && statusCode >= 200 && statusCode < 300;
}
