import { errorMessage } from '../../../utils/apiError';
import { useGetEndpointsQuery, useGetMacrosQuery } from '../api/settingsApi';
import ConnectionTest from './ConnectionTest';
import EndpointEditor from './EndpointEditor';
import InboundUrls from './InboundUrls';

// The tracker settings: test the connection, and edit each endpoint's templates.
export default function SettingsPage() {
  const endpoints = useGetEndpointsQuery();
  const macros = useGetMacrosQuery();

  return (
    <main className="main settings">
      <header className="page-header">
        <h1>Tracker settings</h1>
      </header>

      <ConnectionTest />
      <InboundUrls />

      {macros.data && (
        <section className="card">
          <h2>Macros</h2>
          <p className="hint">
            Filled in from the lead when it converts. URL-encoded in the URL, JSON-escaped in the body.
            Write <code>{'"amount": {amount}'}</code> without quotes to send a number.
          </p>
          <p className="macros">
            {macros.data.macros.map((macro) => <code key={macro}>{`{${macro}}`}</code>)}
          </p>
          <p className="hint">
            event_id may only use {macros.data.event_id_macros.map((macro) => `{${macro}}`).join(' and ')}, and
            must contain {'{lead_id}'}: values that never change, so the same conversion always gets the same event_id.
          </p>
        </section>
      )}

      {endpoints.error && <p className="error">{errorMessage(endpoints.error)}</p>}
      {macros.error && <p className="error">{errorMessage(macros.error)}</p>}
      {endpoints.isLoading && <p className="empty">Loading…</p>}

      {endpoints.data?.map((endpoint) => <EndpointEditor key={endpoint.id} endpoint={endpoint} />)}
    </main>
  );
}
