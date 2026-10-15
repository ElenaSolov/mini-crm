# Take-home assignment: Mini CRM + Conversion tracker
Timebox: **6–8 hours**. A smaller, complete solution is better than a
large unfinished one.
Stack: any modern backend you are comfortable with (Laravel, Node,
Django, etc.). A simple UI or a documented API + Postman/Insomnia
collection is enough. Design polish is not required.
You will receive a personal API key. Do not share it.
---
## What to build
A small CRM for leads. When a lead is marked as converted, your app must
send a conversion event to our tracker.
This is one flow, not two separate apps:
**lead created → status changed to converted → outbound conversion
request**
### 1. Leads (CRM)
A lead has at least:
| Field | Notes |
|---|---|
| `id` | your internal id |
| `name` | required |
| `email` | required if phone is empty |
| `phone` | required if email is empty |
| `source` | e.g. `facebook`, `google`, `manual` |
| `status` | see statuses below |
| `amount` | required before conversion |
| `currency` | 3-letter ISO code, e.g. `USD` |
| `created_at` / `updated_at` | |
Statuses:
- `new`
- `contacted`
- `qualified`
- `converted`
- `lost`
Required behavior:
- CRUD for leads
- List with a filter by status and/or source
- Status change is a dedicated action, not a generic “update any field”
- A lead cannot move to `converted` without a contact (email or phone)
and an `amount`
- You cannot convert a `lost` lead
### 2. Conversion events
When a lead becomes `converted`, your app must `POST` a conversion event
to our tracker.
Do **not** call the tracker from the HTTP controller as a fire-and-forget
`Http::post()`. Persist an outbound job / event first, then send it.
Each outbound event should have a status of its own: `pending`, `sent`,
`failed`.
Rules:
- Generate a stable `event_id` per conversion (for example
`conv_{lead_id}`). Sending the same conversion twice must reuse the same
`event_id`.
- If the tracker returns `5xx`, retry with backoff. Do not create a
second event.
- If the tracker returns `duplicate: true`, treat it as success.
- Store the request and the response (status code + body) so a reviewer
can see what happened.
- Secrets must not be committed to git. Use env vars.
You may use a queue, a table + retry command, or an equivalent. Explain
the choice in the README.
---
## Our tracker API
Base URL:
`https://bipro2interface.sseku.com/api/candidate-tracker`
Auth (required on every request):
```http
Authorization: Bearer <YOUR_API_KEY>
```
Rate limit: 30 requests per minute per key.
### `GET /ping`
Connectivity check.
```bash
curl -s https://bipro2interface.sseku.com/api/candidate-tracker/ping \
 -H "Authorization: Bearer <YOUR_API_KEY>"
```
Success (`200`):
```json
{
 "ok": true,
 "candidate": "<your-slug>",
 "time": "2026-09-02T14:00:00+03:00"
}
```
### `POST /conversions`
```http
POST /api/candidate-tracker/conversions
Authorization: Bearer <YOUR_API_KEY>
Content-Type: application/json
```
```json
{
 "event_id": "conv_42",
 "event_name": "purchase",
 "email": "lead@example.com",
 "phone": "+12025550123",
 "lead_id": "42",
 "amount": 199.00,
 "currency": "USD",
 "occurred_at": "2026-09-02T14:00:00+03:00"
}
```
| Field | Required | Notes |
|---|---|---|
| `event_id` | yes | string, max 128. Your idempotency key |
| `event_name` | no | e.g. `purchase` |
| `email` | email **or** phone | valid email |
| `phone` | email **or** phone | string, max 32 |
| `lead_id` | no | your CRM lead id |
| `amount` | no | numeric |
| `currency` | no | exactly 3 letters |
| `occurred_at` | no | parseable datetime |
| `simulate` | no | send `"server_error"` only when testing retries |
### Responses
**Created** (`201`)
```json
{
 "ok": true,
 "duplicate": false,
 "event_id": "conv_42",
 "received_at": "2026-09-02T14:00:00+03:00"
}
```
**Duplicate** (`200`) — same `event_id` was already accepted for your key
```json
{
 "ok": true,
 "duplicate": true,
 "event_id": "conv_42",
 "received_at": "2026-09-02T14:00:00+03:00"
}
```
**Validation error** (`422`) — missing `event_id`, or neither email nor
phone
```json
{
 "ok": false,
 "error": "validation_error",
 "errors": {
 "contact": ["Provide email or phone."]
 }
}
```
**Unauthorized** (`401`) — missing or invalid key
```json
{
 "ok": false,
 "error": "unauthorized",
 "message": "Missing or invalid API key. Send Authorization: Bearer
<key>."
}
```
**Simulated failure** (`500`) — only if you send `"simulate":
"server_error"`
```json
{
 "ok": false,
 "error": "server_error",
 "message": "Simulated 500. Retry this event_id."
}
```
Use `simulate` only to prove that your retry logic works. Do not send it
on the happy path.
---
## Suggested test sequence
1. `GET /ping` with your key → `200`
2. Create a lead, convert it → tracker returns `201`
3. Convert the same lead again → tracker returns `200` + `duplicate:
true`
4. Send a conversion without email and phone → `422`
5. Send one event with `"simulate": "server_error"`, then retry the same
`event_id` without `simulate` → first `500`, then `201`
---
## What we look at
- Lead status rules and validation
- Outbound conversion is persisted, not only posted
- Idempotency (`event_id` reused on retry / double convert)
- 5xx handling and retries
- Request/response logging
- Secrets stay out of git
- A README that explains how to run the app and how to reproduce a
conversion
We do **not** score UI design, extra entities, or framework choice.
## Out of scope
- Multi-brand, roles, billing, real Facebook / Stripe / Google APIs
- Pixel-perfect frontend
- Docker, unless you already want it for yourself
## How to submit
1. Public or private Git repo (invite us if private)
2. README: setup, env vars, how to create a lead and trigger a conversion
3. Do not commit `.env` or the API key
If something in the tracker is unclear, document the assumption in the
README and keep going.