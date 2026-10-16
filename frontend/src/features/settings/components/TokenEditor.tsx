import { useState, type FormEvent } from 'react';
import { errorMessage, fieldErrors } from '../../../utils/apiError';
import { useGenerateInboundTokenMutation, useSetInboundTokenMutation } from '../api/settingsApi';

// The token in the pixel and postback URLs. By default it is a random value made at
// startup; the client can type their own or generate a new one (e.g. if it leaked).
// The backend checks the value: 16-64 characters, only letters, digits, - and _.
export default function TokenEditor({ token }: Readonly<{ token: string | null }>) {
  const [value, setValue] = useState('');
  const [setToken, setResult] = useSetInboundTokenMutation();
  const [generateToken, generateResult] = useGenerateInboundTokenMutation();
  const isBusy = setResult.isLoading || generateResult.isLoading;
  const error = setResult.error ?? generateResult.error;

  // Both actions break the URLs already on the website, so ask first.
  const confirmed = () =>
    token === null || window.confirm('The old token stops working at once. Update the URLs on your website after this. Continue?');

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    if (!confirmed()) return;
    generateResult.reset();
    try {
      await setToken(value.trim()).unwrap();
      setValue('');
    } catch {
      // Shown below from setResult.error.
    }
  }

  async function handleGenerate() {
    if (!confirmed()) return;
    setResult.reset();
    await generateToken();
  }

  return (
    <div className="token-editor">
      <h3>Token</h3>
      {token && <p className="hint">Current token: <code>{token}</code></p>}
      {error && <p className="error">{fieldErrors(error).token?.join(' ') ?? errorMessage(error)}</p>}
      <form className="row" onSubmit={handleSave}>
        <input
          placeholder="Your own token, 16-64 characters"
          minLength={16}
          maxLength={64}
          pattern="[A-Za-z0-9_\-]+"
          title="Letters, digits, - and _"
          value={value}
          onChange={(e) => setValue(e.target.value)}
        />
        <button type="submit" disabled={isBusy || value.trim() === ''}>Save token</button>
        <button type="button" onClick={handleGenerate} disabled={isBusy}>Generate new</button>
      </form>
    </div>
  );
}
