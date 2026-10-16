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

  // Converted and lost are final (the backend enforces it). Locking the dropdown
  // just saves the user a click that would end in a 409.
  const isConverted = lead.status === 'converted';
  const isLost = lead.status === 'lost';
  const isFinal = isConverted || isLost;
  // A locked dropdown always shows the real status, never an older choice.
  const selectedStatus = isFinal ? lead.status : newStatus;

  // Saving the same status does nothing, except for converted: that re-sends the event.
  const canSave = isConverted || (!isLost && selectedStatus !== lead.status);

  // The old error was about the old choice, so it goes away with it.
  function handleSelect(status: LeadStatus) {
    setNewStatus(status);
    reset();
  }

  async function handleSave() {
    try {
      await changeStatus({
        id: lead.id,
        status: selectedStatus,
        simulateServerError: selectedStatus === 'converted' && simulateServerError,
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
        <select
          value={selectedStatus}
          onChange={(e) => handleSelect(e.target.value)}
          disabled={isFinal}
        >
          {statuses.map((status) => (
            <option key={status} value={status}>{status}</option>
          ))}
        </select>
        <button className="primary" onClick={handleSave} disabled={isLoading || !canSave}>
          {isConverted ? 'Convert again' : 'Save status'}
        </button>
      </div>
      {isConverted && <p className="hint">A converted lead can't change status. You can only convert it again.</p>}
      {isLost && <p className="hint">A lost lead can't change status or be converted.</p>}
      {selectedStatus === 'converted' && (
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
