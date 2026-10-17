import { useState, type FormEvent } from 'react';
import { errorMessage, fieldErrors } from '../../../utils/apiError';
import { useClearTrackerKeyMutation, useResetTrackerAuthMutation, useUpdateTrackerAuthMutation } from '../api/settingsApi';
import type { TrackerAuth } from '../types';

const keySourceText: Record<TrackerAuth['key_source'], string> = {
  settings: 'Using the key saved here.',
  env: 'Using Tracker__ApiKey from .env.',
  none: 'No key set: the tracker will answer 401.',
};

// The auth header and API key shared by ping and purchase. The key is write-only:
// the backend never sends it back, so the field is always empty and an empty field
// keeps the current key. The backend checks every rule on save.
export default function TrackerAuthEditor({ auth }: Readonly<{ auth: TrackerAuth }>) {
  const [headerName, setHeaderName] = useState(auth.header_name);
  const [headerTemplate, setHeaderTemplate] = useState(auth.header_template);
  const [apiKey, setApiKey] = useState('');
  const [saved, setSaved] = useState(false);
  const [updateAuth, updateState] = useUpdateTrackerAuthMutation();
  const [resetAuth, resetState] = useResetTrackerAuthMutation();
  const [clearKey, clearState] = useClearTrackerKeyMutation();

  const busy = updateState.isLoading || resetState.isLoading || clearState.isLoading;
  const errors = updateState.error ? fieldErrors(updateState.error) : {};
  const otherError = resetState.error ?? clearState.error;

  function resetMessages() {
    setSaved(false);
    updateState.reset();
    resetState.reset();
    clearState.reset();
  }

  function showSaved(result: TrackerAuth) {
    setHeaderName(result.header_name);
    setHeaderTemplate(result.header_template);
    setApiKey('');
    setSaved(true);
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    resetMessages();
    try {
      showSaved(await updateAuth({ header_name: headerName, header_template: headerTemplate, api_key: apiKey }).unwrap());
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  async function handleReset() {
    if (!window.confirm('Reset the auth header to the .env default? The saved key is kept.')) return;
    resetMessages();
    try {
      showSaved(await resetAuth().unwrap());
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  async function handleClearKey() {
    if (!window.confirm('Delete the saved key? Tracker__ApiKey from .env will be used.')) return;
    resetMessages();
    try {
      showSaved(await clearKey().unwrap());
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  return (
    <form className="endpoint card" onSubmit={handleSave}>
      <div className="event-header">
        <h2>Tracker auth</h2>
        <span className="muted">used by ping and purchase</span>
      </div>

      {updateState.error && Object.keys(errors).length === 0 && <p className="error">{errorMessage(updateState.error)}</p>}
      {otherError && <p className="error">{errorMessage(otherError)}</p>}
      {saved && <p className="success">Saved. Every request from now on uses it, including retries of queued events.</p>}

      <label>
        Header name
        <input value={headerName} onChange={(e) => { setHeaderName(e.target.value); resetMessages(); }} />
        {errors.header_name && <span className="field-error">{errors.header_name.join(' ')}</span>}
      </label>

      <label>
        Header value
        <input value={headerTemplate} onChange={(e) => { setHeaderTemplate(e.target.value); resetMessages(); }} />
        <span className="hint">{'{api_key}'} is replaced with the key when the request is sent.</span>
        {errors.header_template && <span className="field-error">{errors.header_template.join(' ')}</span>}
      </label>

      <label>
        API key
        <input
          type="password"
          // "off" is ignored for password fields; without this the browser could fill in a
          // saved login password, and Save would store it as the tracker key.
          autoComplete="new-password"
          placeholder={auth.key_source === 'settings' ? 'Saved. Type a new key to replace it.' : 'Type a key to save it here.'}
          value={apiKey}
          onChange={(e) => { setApiKey(e.target.value); resetMessages(); }}
        />
        <span className="hint">{keySourceText[auth.key_source]} A saved key is never shown again.</span>
        {errors.api_key && <span className="field-error">{errors.api_key.join(' ')}</span>}
      </label>

      <h3>Preview</h3>
      <pre>{headerName}: {headerTemplate.split('{api_key}').join('***')}</pre>

      <div className="actions">
        <button type="submit" className="primary" disabled={busy}>
          {updateState.isLoading ? 'Saving…' : 'Save'}
        </button>
        <button type="button" onClick={handleReset} disabled={busy}>Reset to default</button>
        {auth.key_source === 'settings' && (
          <button type="button" onClick={handleClearKey} disabled={busy}>Delete saved key</button>
        )}
      </div>
    </form>
  );
}
