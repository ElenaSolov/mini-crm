import { useState, type FormEvent } from 'react';
import { errorMessage, fieldErrors } from '../../../utils/apiError';
import { useCreateLeadMutation, useUpdateLeadMutation } from '../api/leadsApi';
import type { Lead, LeadInput } from '../types';

interface Props {
  lead?: Lead; // set when editing, empty when creating
  onSaved: (lead: Lead) => void;
  onCancel: () => void;
}

type FieldName = 'name' | 'email' | 'phone' | 'source' | 'amount' | 'currency';
type FormValues = Record<FieldName, string>;

const fields: { name: FieldName; label: string; placeholder?: string }[] = [
  { name: 'name', label: 'Name *' },
  { name: 'email', label: 'Email' },
  { name: 'phone', label: 'Phone' },
  { name: 'source', label: 'Source', placeholder: 'facebook, google, manual…' },
  { name: 'amount', label: 'Amount' },
  { name: 'currency', label: 'Currency', placeholder: 'USD' },
];

// Create and edit use the same form. The backend checks every rule;
// this form only shows its answer next to the right field.
export default function LeadForm({ lead, onSaved, onCancel }: Readonly<Props>) {
  const [values, setValues] = useState<FormValues>({
    name: lead?.name ?? '',
    email: lead?.email ?? '',
    phone: lead?.phone ?? '',
    source: lead?.source ?? '',
    amount: lead?.amount?.toString() ?? '',
    currency: lead?.currency ?? '',
  });
  const [createLead, createState] = useCreateLeadMutation();
  const [updateLead, updateState] = useUpdateLeadMutation();
  const { error, isLoading: saving } = lead ? updateState : createState;

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const input = toInput(values);

    try {
      const saved = lead
        ? await updateLead({ id: lead.id, input }).unwrap()
        : await createLead(input).unwrap();
      onSaved(saved);
    } catch {
      // The error is kept in the mutation state and shown below.
    }
  }

  const errorsByField = fieldErrors(error);

  return (
    <form className="lead-form" onSubmit={handleSubmit}>
      <h2>{lead ? `Edit lead #${lead.id}` : 'New lead'}</h2>
      <p className="hint">Email or phone is required. Currency is required when amount is set.</p>

      {error && <p className="error">{errorMessage(error)}</p>}

      {fields.map((field) => (
        <label key={field.name}>
          {field.label}
          <input
            type={field.name === 'amount' ? 'number' : 'text'}
            step={field.name === 'amount' ? '0.01' : undefined}
            placeholder={field.placeholder}
            value={values[field.name]}
            onChange={(e) => setValues({ ...values, [field.name]: e.target.value })}
          />
          {errorsByField[field.name]?.map((message) => (
            <span key={message} className="field-error">{message}</span>
          ))}
        </label>
      ))}

      <div className="actions">
        <button type="submit" className="primary" disabled={saving}>
          {saving ? 'Saving…' : 'Save'}
        </button>
        <button type="button" onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}

// Empty text becomes null, so the API sees "no value" rather than "".
function toInput(values: FormValues): LeadInput {
  const textOrNull = (value: string) => value.trim() || null;

  return {
    name: values.name,
    email: textOrNull(values.email),
    phone: textOrNull(values.phone),
    source: textOrNull(values.source),
    amount: values.amount.trim() === '' ? null : Number(values.amount),
    currency: textOrNull(values.currency),
  };
}
