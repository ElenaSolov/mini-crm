import type { ReactNode } from 'react';
import StatusBadge from '../../../components/StatusBadge';
import { formatDate } from '../../../utils/format';
import type { TrackerEvent } from '../types';

interface Props {
  event: TrackerEvent;
  // Extra line under the title, e.g. the lead on the Logs page.
  subtitle?: ReactNode;
}

// One outbound event with all its attempts: what was sent and what the tracker
// answered. Used in the lead detail and on the Logs page. Bodies are plain text, never HTML.
export default function EventCard({ event, subtitle }: Readonly<Props>) {
  return (
    <article className="event">
      <div className="event-header">
        <strong>{event.event_id}</strong>
        <StatusBadge status={event.status} />
      </div>
      {subtitle && <p>{subtitle}</p>}
      <p className="muted">
        {event.method} {event.url}
        <br />
        Attempts: {event.attempts.length}
        {/* attempt_count restarts at 0 on "Convert again"; the list keeps every attempt. */}
        {event.attempt_count !== event.attempts.length &&
          ` (${event.attempt_count} since last "Convert again")`}
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
            {attempt.response_message && <p>{attempt.response_message}</p>}
            {attempt.error && <p className="error">{attempt.error}</p>}
            {/* The full request and response stay one click away, so a reviewer can see exactly what happened. */}
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
  );
}

// Only colors the code. Whether to retry is the backend's call: the event's
// status (pending / failed) shows what it decided.
function isSuccess(statusCode: number | null) {
  return statusCode !== null && statusCode >= 200 && statusCode < 300;
}
