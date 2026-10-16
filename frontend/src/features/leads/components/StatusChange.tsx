import { useState } from 'react';
import { errorMessage, fieldErrors } from '../../../utils/apiError';
import { useChangeStatusMutation } from '../api/leadsApi';
import type { Lead, LeadStatus } from '../types';

interface Props {
  lead: Lead;
  statuses: LeadStatus[];
}

// The dedicated status action. The backend checks the rules (e.g. a lost lead
// can't be converted) and answers 409 with a readable message.
export default function StatusChange({ lead, statuses }: Readonly<Props>) {
  const [newStatus, setNewStatus] = useState<LeadStatus>(lead.status);
  const [simulateServerError, setSimulateServerError] = useState(false);
  const [changeStatus, { error, isLoading, reset }] = useChangeStatusMutation();

  const isConvertAgain = lead.status === 'converted' && newStatus === 'converted';
  // Saving the same status does nothing, except for converted: that re-sends the event.
  const canSave = newStatus !== lead.status || isConvertAgain;

  // The old error was about the old choice, so it goes away with it.
  function handleSelect(status: LeadStatus) {
    setNewStatus(status);
    reset();
  }

  async function handleSave() {
    try {
      await changeStatus({
        id: lead.id,
        status: newStatus,
        simulateServerError: newStatus === 'converted' && simulateServerError,
      }).unwrap();
      setSimulateServerError(false);
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  return (
    <section className="status-change">
      <h3>Change status</h3>
      {error && (
        <p className="error">
          {/* A 400 lists the reason under "status"; a 409 has it in the message. */}
          {fieldErrors(error).status?.join(' ') ?? errorMessage(error)}
        </p>
      )}
      <div className="row">
        <select value={newStatus} onChange={(e) => handleSelect(e.target.value)}>
          {statuses.map((status) => (
            <option key={status} value={status}>{status}</option>
          ))}
        </select>
        <button className="primary" onClick={handleSave} disabled={isLoading || !canSave}>
          {isConvertAgain ? 'Convert again' : 'Save status'}
        </button>
      </div>
      {newStatus === 'converted' && (
        <label className="checkbox">
          <input
            type="checkbox"
            checked={simulateServerError}
            onChange={(e) => setSimulateServerError(e.target.checked)}
          />
          Test retries: the tracker answers 500 on the first send
        </label>
      )}
    </section>
  );
}
