import { useState, type FormEvent } from 'react';
import { useDebounce } from '../../../hooks/useDebounce';
import { errorMessage } from '../../../utils/apiError';
import { usePreviewEndpointQuery, useResetEndpointMutation, useUpdateEndpointMutation } from '../api/settingsApi';
import type { EndpointTemplates, TrackerEndpoint } from '../types';

interface Props {
  endpoint: TrackerEndpoint;
}

function templatesOf(endpoint: TrackerEndpoint): EndpointTemplates {
  return {
    url_template: endpoint.url_template,
    body_template: endpoint.body_template ?? '',
    event_id_template: endpoint.event_id_template,
  };
}

// Edits one endpoint's templates. The preview shows the request for a sample lead
// while typing; the backend checks every rule, both in the preview and on save.
export default function EndpointEditor({ endpoint }: Readonly<Props>) {
  const [values, setValues] = useState(() => templatesOf(endpoint));
  const debouncedValues = useDebounce(values, 300);
  const preview = usePreviewEndpointQuery({ id: endpoint.id, templates: debouncedValues });
  const [updateEndpoint, updateState] = useUpdateEndpointMutation();
  const [resetEndpoint, resetState] = useResetEndpointMutation();
  const [saved, setSaved] = useState(false);

  const sendsForLead = endpoint.trigger_status !== null;
  const hasBody = endpoint.method !== 'GET';
  const actionError = updateState.error ?? resetState.error;
  const busy = updateState.isLoading || resetState.isLoading;

  // The preview checks the same rules as save, so its errors are the ones to show.
  const errors = preview.data?.errors ?? {};

  function handleChange(field: keyof EndpointTemplates, value: string) {
    setValues({ ...values, [field]: value });
    setSaved(false);
    updateState.reset();
    resetState.reset();
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    try {
      const result = await updateEndpoint({ id: endpoint.id, templates: values }).unwrap();
      setValues(templatesOf(result));
      setSaved(true);
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  async function handleReset() {
    if (!window.confirm(`Reset the "${endpoint.name}" templates to the defaults?`)) return;
    try {
      const result = await resetEndpoint(endpoint.id).unwrap();
      setValues(templatesOf(result));
      setSaved(true);
      updateState.reset();
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  return (
    <form className="endpoint card" onSubmit={handleSave}>
      <div className="event-header">
        <h2>{endpoint.name}</h2>
        <span className="muted">
          {endpoint.method}
          {sendsForLead ? ` · sent when a lead becomes "${endpoint.trigger_status}"` : ' · not sent for a lead, no macros'}
        </span>
      </div>

      {actionError && <p className="error">{errorMessage(actionError)}</p>}
      {saved && <p className="success">Saved. New events use these templates; events already created keep theirs.</p>}

      <label>
        URL
        <input value={values.url_template} onChange={(e) => handleChange('url_template', e.target.value)} />
        {errors.url_template && <span className="field-error">{errors.url_template}</span>}
      </label>

      {hasBody && (
        <label>
          Body (JSON)
          <textarea
            rows={12}
            value={values.body_template}
            onChange={(e) => handleChange('body_template', e.target.value)}
          />
          {errors.body_template && <span className="field-error">{errors.body_template}</span>}
        </label>
      )}

      {sendsForLead && (
        <label>
          event_id
          <input value={values.event_id_template} onChange={(e) => handleChange('event_id_template', e.target.value)} />
          {errors.event_id_template && <span className="field-error">{errors.event_id_template}</span>}
        </label>
      )}

      <h3>Preview with a sample lead</h3>
      {preview.error && <p className="error">{errorMessage(preview.error)}</p>}
      {preview.data && (
        <>
          {preview.data.event_id !== null && <p className="muted">event_id: {preview.data.event_id}</p>}
          <pre>{endpoint.method} {preview.data.url}</pre>
          {preview.data.body !== null && <pre>{preview.data.body}</pre>}
        </>
      )}

      <div className="actions">
        <button type="submit" className="primary" disabled={busy}>
          {updateState.isLoading ? 'Saving…' : 'Save'}
        </button>
        <button type="button" onClick={handleReset} disabled={busy}>Reset to default</button>
      </div>
    </form>
  );
}
