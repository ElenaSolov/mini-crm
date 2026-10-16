import { useState } from 'react';
import { errorMessage } from '../../../utils/apiError';
import { useGetInboundSettingsQuery } from '../api/settingsApi';
import TokenEditor from './TokenEditor';

// A code block with a "Copy" button.
function CopyBlock({ text }: Readonly<{ text: string }>) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // The browser blocked the clipboard (e.g. not on https). The text can still be selected by hand.
    }
  }

  return (
    <div className="copy-block">
      <pre>{text}</pre>
      <button type="button" onClick={handleCopy}>{copied ? 'Copied' : 'Copy'}</button>
    </div>
  );
}

// The URLs the client puts in their own website form, so new leads land in the CRM.
// They use this page's address; on a real server that is the CRM's public address.
export default function InboundUrls() {
  const { data, error } = useGetInboundSettingsQuery();

  if (error) return <p className="error">{errorMessage(error)}</p>;
  if (!data) return null;

  if (!data.token) {
    return (
      <section className="card">
        <h2>Lead capture from your website</h2>
        <p className="hint">No token yet. Set one below, or restart the backend: it creates one at startup.</p>
        <TokenEditor token={null} />
      </section>
    );
  }

  const base = `${window.location.origin}/api/inbound`;
  const token = encodeURIComponent(data.token);
  const pixelUrl = `${base}/pixel.png?token=${token}`;
  const postbackUrl = `${base}/postback?token=${token}`;
  const emptyFields = data.fields.map((field) => `&${field}=`).join('');

  // FormData sends the form's fields by their name attributes; empty ones are saved as empty.
  const pixelScript = `<script>
  // Call this when your form is sent, e.g. in your submit handler:
  const form = document.querySelector("form");
  new Image().src = "${pixelUrl}&" + new URLSearchParams(new FormData(form));
</script>`;

  const postbackForm = `<form method="post" action="${postbackUrl}">
  <input name="name" required>
  <input name="email" type="email">
  <input name="phone">
  <input name="source" type="hidden" value="website">
  <button type="submit">Send</button>
</form>`;

  const postbackCurl = `curl -X POST "${postbackUrl}" -d name=Ann -d email=ann@example.com`;

  return (
    <section className="card">
      <h2>Lead capture from your website</h2>
      <p className="hint">
        Each request creates a new lead with status "new". The same rules as the CRM form apply (name, and email
        or phone). Keep the token private where you can: anyone who has it can add leads. Fields:
      </p>
      <p className="macros">{data.fields.map((field) => <code key={field}>{field}</code>)}</p>

      <TokenEditor token={data.token} />

      <h3>Pixel (image URL)</h3>
      <p className="hint">Put the values after each field name, URL-encoded. Returns a 1×1 transparent PNG.</p>
      <CopyBlock text={pixelUrl + emptyFields} />
      <CopyBlock text={pixelScript} />

      <h3>Postback URL (GET or POST, with form fields or query parameters)</h3>
      <p className="hint">
        For an HTML form, for your own server, or as a plain link. Send the fields as form fields (POST), in the
        URL like the pixel (GET or POST), or both (a form field wins). Returns 201 with the new lead as JSON; with a plain HTML form
        the visitor sees that JSON, so to stay on your page, send it from your server or with fetch(). Source is
        "pixel" or "postback" when the form doesn't send one.
      </p>
      <CopyBlock text={postbackUrl} />
      <CopyBlock text={postbackForm} />
      <CopyBlock text={postbackCurl} />
    </section>
  );
}
