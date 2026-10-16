// Field names match the API's JSON (snake_case).

// A name from the LeadStatuses table. The list comes from the API (GET /api/leads/statuses).
export type LeadStatus = string;

export interface Lead {
  id: number;
  name: string;
  email: string | null;
  phone: string | null;
  source: string | null;
  status: LeadStatus;
  amount: number | null;
  currency: string | null;
  created_at: string;
  updated_at: string;
}

// What the form sends for create and edit. Status is not here: it changes only
// through the separate change-status action.
export interface LeadInput {
  name: string;
  email: string | null;
  phone: string | null;
  source: string | null;
  amount: number | null;
  currency: string | null;
}

// Empty filters are left out of the URL.
export interface LeadsQuery {
  status?: string;
  source?: string;
}

export type TrackerEventStatus = 'pending' | 'sent' | 'failed';

export interface TrackerAttempt {
  id: number;
  attempted_at: string;
  url: string;
  request_body: string | null;
  status_code: number | null;
  response_body: string | null;
  error: string | null;
}

export interface TrackerEvent {
  id: number;
  event_id: string;
  endpoint: string | null;
  status: TrackerEventStatus;
  method: string;
  url: string;
  body: string | null;
  occurred_at: string;
  created_at: string;
  attempt_count: number;
  next_attempt_at: string | null;
  last_error: string | null;
  attempts: TrackerAttempt[];
}
