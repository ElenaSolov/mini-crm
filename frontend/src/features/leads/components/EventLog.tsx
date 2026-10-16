import { useEffect, useRef, useState } from 'react';
import { useDispatch } from 'react-redux';
import { errorMessage } from '../../../utils/apiError';
import { leadsApi, useGetLeadEventsQuery } from '../api/leadsApi';
import EventCard from './EventCard';

// While an event is pending, the worker is still sending it, so reload often
// to show new attempts as they happen.
const pollEveryMs = 3000;

// A lead's outbound events with all their attempts.
export default function EventLog({ leadId }: Readonly<{ leadId: number }>) {
  const dispatch = useDispatch();
  const [pollingInterval, setPollingInterval] = useState(0);
  // skipPollingIfUnfocused: no polling while the browser tab is in the background.
  const { data: events = [], error } = useGetLeadEventsQuery(leadId, {
    pollingInterval,
    skipPollingIfUnfocused: true,
  });

  const hasPendingEvent = events.some((event) => event.status === 'pending');
  const wasPending = useRef(hasPendingEvent);
  useEffect(() => {
    setPollingInterval(hasPendingEvent ? pollEveryMs : 0);
    // The send just finished: reload the lead, so its "Outbound" field shows sent or failed.
    if (wasPending.current && !hasPendingEvent)
      dispatch(leadsApi.util.invalidateTags([{ type: 'Lead', id: leadId }]));
    wasPending.current = hasPendingEvent;
  }, [hasPendingEvent, leadId, dispatch]);

  if (error)
    return <p className="error">{errorMessage(error)}</p>;
  if (events.length === 0)
    return <p className="empty">No events yet. Converting the lead creates one.</p>;

  return (
    <div className="event-log">
      {events.map((event) => <EventCard key={event.id} event={event} />)}
    </div>
  );
}
