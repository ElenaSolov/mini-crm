// Field names match the API's JSON (snake_case).

export interface TrackerEndpoint {
  id: number;
  name: string;
  method: string;
  trigger_status: string | null; // null: not sent for a lead (e.g. ping)
  url_template: string;
  body_template: string | null;
  event_id_template: string;
  is_active: boolean;
}

// What the editor sends for preview and save. '' means "empty".
export interface EndpointTemplates {
  url_template: string;
  body_template: string;
  event_id_template: string;
}

// The request the templates would send for a sample lead. errors is empty when they can be saved.
export interface EndpointPreview {
  event_id: string | null;
  url: string;
  body: string | null;
  errors: Partial<Record<keyof EndpointTemplates, string>>;
}

export interface MacroList {
  macros: string[];
  event_id_macros: string[];
}

// The pixel and postback URLs for the client's own website form.
// token is null only if the backend hasn't created it yet (it does at startup).
export interface InboundSettings {
  token: string | null;
  fields: string[];
}

// The tracker's own answer to ping. status_code is null when it didn't answer.
export interface PingResult {
  status_code: number | null;
  body: string | null;
  error: string | null;
}
