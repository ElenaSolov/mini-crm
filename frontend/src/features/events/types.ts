import type { TrackerEvent, TrackerEventStatus } from '../leads';

// GET /api/events. Status is left out of the URL when it's undefined (all statuses).
export interface TrackerEventsQuery {
  status?: TrackerEventStatus;
  page: number;
  page_size: number;
}

export interface TrackerEventLogItem {
  lead_name: string | null;
  event: TrackerEvent;
}

export interface TrackerEventPage {
  items: TrackerEventLogItem[];
  page: number;
  page_size: number;
  total_count: number;
}
