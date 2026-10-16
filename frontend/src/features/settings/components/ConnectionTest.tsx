import { errorMessage } from '../../../utils/apiError';
import { usePingMutation } from '../api/settingsApi';

// Calls the tracker's ping with the saved ping URL and the API key from .env.
// A wrong or missing key shows as the tracker's 401.
export default function ConnectionTest() {
  const [ping, { data, error, isLoading }] = usePingMutation();
  const ok = data?.status_code === 200;

  return (
    <section className="card">
      <div className="event-header">
        <h2>Tracker connection</h2>
        <button className="primary" onClick={() => ping()} disabled={isLoading}>
          {isLoading ? 'Testing…' : 'Test connection'}
        </button>
      </div>
      <p className="hint">Uses the saved ping URL below and the API key from .env.</p>

      {error && <p className="error">{errorMessage(error)}</p>}
      {data && (
        <>
          <p>
            Tracker answered <span className={`code ${ok ? 'ok' : 'fail'}`}>{data.status_code}</span>
            {data.status_code === 401 && ' — check Tracker__ApiKey in .env'}
          </p>
          {data.body && <pre>{data.body}</pre>}
        </>
      )}
    </section>
  );
}
