# Mini CRM + Conversion Tracker

A small CRM for leads. When a lead is marked as `converted`, the app stores an
outbound conversion event, and a background worker sends it to the tracker API,
with retries and full request/response logging.

**Flow:** lead created → status changed to `converted` → outbound conversion request

## Stack

- Backend: ASP.NET Core 8 Web API, EF Core 8, SQL Server
- Frontend: React 18 + TypeScript (Vite), RTK Query
- Tests: xUnit (unit tests with EF InMemory, integration tests with a real SQL Server)

## Project structure

```
backend/MiniCrm.Api/     API, background worker, EF migrations
  Controllers/           thin: call a service, map the result to an HTTP status
  Services/              use cases (LeadService), macro renderer, tracker client, worker
  Models/                entities with their rules (Lead, TrackerEvent)
  Data/                  DbContext, seeded defaults, migrations
backend/MiniCrm.Tests/   unit, integration and concurrency tests
frontend/                React UI (leads page and tracker settings page)
requests.http            ready-made calls to the API
```

## Setup

### Prerequisites

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) (`global.json` pins 8.0.410 and allows newer 8.0.x)
- [Node.js](https://nodejs.org/) 20 or newer
- SQL Server:
  - **Windows:** SQL Server LocalDB (comes with Visual Studio). Nothing to configure.
  - **Mac/Linux:** SQL Server in Docker (see below).

### 1. Add your API key

```bash
cp .env.example .env
```

Open `.env` and set `Tracker__ApiKey` to your personal key. `.env` is ignored
by git.

### 2. Database

**Windows:** nothing to do. The app uses LocalDB by default.

**Mac/Linux:** start SQL Server in Docker:

```bash
docker run -d --name minicrm-sql -p 1433:1433 \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_strong_pass1" \
  mcr.microsoft.com/mssql/server:2022-latest
```

Then uncomment the `ConnectionStrings__Default` line in `.env` (the password
there matches the command above).

The app applies EF migrations at startup, so it creates the database, the
tables and the seeded rows (lead statuses, tracker endpoints) by itself.

### 3. Run the backend

```bash
dotnet run --project backend/MiniCrm.Api
```

The API runs on http://localhost:5080. Swagger: http://localhost:5080/swagger.
If the key is missing, the app logs a warning at startup.

### 4. Run the frontend

In a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173. The Vite dev server forwards `/api` to the
backend, so no CORS setup is needed.

### 5. Run the tests

```bash
dotnet test
```

Unit tests need nothing. Integration tests need SQL Server: on Windows they use
LocalDB (`MiniCrm_Tests` database). On Mac/Linux, point them to Docker:

```bash
export MINICRM_TEST_DB="Server=localhost,1433;Database=MiniCrm_Tests;User Id=sa;Password=Your_strong_pass1;TrustServerCertificate=True"
dotnet test
```

The tests delete and recreate this database on each run. To make sure a real
database is never wiped, they refuse to run if the name doesn't end with
`_Tests`. They use a fake tracker and the key `test-key`, so they never call the
real tracker.

## Environment variables

Secrets are never committed. Copy `.env.example` to `.env` and fill in the values.
Real environment variables win over values in `.env`.

| Variable | Required | Description |
|---|---|---|
| `Tracker__ApiKey` | yes | Your personal tracker API key |
| `ConnectionStrings__Default` | no | SQL Server connection string. Defaults to LocalDB (set in `appsettings.json`) |
| `MINICRM_TEST_DB` | no | Tests only: SQL Server for the integration tests. The database name must end with `_Tests` |

The double underscore is how ASP.NET Core reads nested settings:
`Tracker__ApiKey` becomes `Tracker:ApiKey`.

## How to create a lead and trigger a conversion

### In the UI

1. Click **New lead**. Fill in a name, an email or phone, an amount and a
   currency. Save.
2. Open the lead. In **Change status**, pick `converted` and click **Save status**.
3. **Tracker events** under the lead shows the event (`conv_<id>`) as `pending`.
   Within about 5 seconds the worker sends it, and the log shows the attempt:
   the URL, the request body, the status code and the response body.

### With the API

Use [requests.http](requests.http) (VS Code REST Client, Visual Studio or
JetBrains), or curl:

```bash
# Create a lead
curl -s -X POST http://localhost:5080/api/leads \
  -H "Content-Type: application/json" \
  -d '{"name":"Ann Lee","email":"ann@example.com","amount":199.00,"currency":"USD","source":"facebook"}'

# Convert it (use the id from the answer above)
curl -s -X POST http://localhost:5080/api/leads/1/status \
  -H "Content-Type: application/json" \
  -d '{"status":"converted"}'

# See the event and its attempts (wait about 5 seconds)
curl -s http://localhost:5080/api/leads/1/events
```

### From the client's website form (pixel or postback)

1. Open **Tracker settings**. The **Lead capture from your website** card shows
   the pixel URL and the postback URL, with the token already in them, plus ready
   examples (a script for the pixel, an HTML form and `curl` for the postback).
   Click **Copy** and put one of them in the website's form.
2. When a visitor sends the form, a new lead appears on the **Leads** page with
   status `new` and source `pixel` or `postback` (or the form's own `source`).
3. From there it is a normal lead: add an amount if needed and convert it.

The same with curl (take the token from `GET /api/inbound/settings`):

```bash
curl -s "http://localhost:5080/api/inbound/pixel.png?token=<token>&name=Ann&email=ann@example.com" -o pixel.png
curl -s -X POST "http://localhost:5080/api/inbound/postback?token=<token>" -d name=Bob -d phone=%2B12025550123
```

How the token and the limits work: see
[Lead capture from the client's website](#lead-capture-from-the-clients-website-pixel-and-postback).

### API overview

| Method | Path | What it does |
|---|---|---|
| GET | `/api/leads?name=&email=&phone=&source=&currency=&status=&id=&amount_from=&amount_to=&created_from=&created_to=&page=1&page_size=20` | List leads, newest first, one page at a time: `{ items, page, page_size, total_count }`. All filters optional. Text filters match part of the value and ignore case (`name=ann` finds "Joanna"); `status` and `id` are exact; amount (0–1,000,000,000) and created (exact moments like `2026-10-16T00:00:00Z`, both ends included; the UI turns the picked days into moments in the user's time zone) are ranges. `page` is 1–1,000,000, `page_size` is 1–100 |
| POST | `/api/leads` | Create a lead (always starts as `new`) |
| GET | `/api/leads/{id}` | One lead |
| PUT | `/api/leads/{id}` | Edit a lead's fields. Can't change status |
| DELETE | `/api/leads/{id}` | Delete. 409 if the lead has tracker events |
| POST | `/api/leads/{id}/status` | Change status: `{ "status": "converted", "simulate_server_error": false }` |
| GET | `/api/leads/{id}/events` | The lead's outbound events with every attempt |
| GET | `/api/leads/statuses` | Allowed statuses |
| GET | `/api/tracker/ping` | Calls the tracker's `/ping` with the key |
| GET | `/api/tracker/endpoints` | The tracker endpoints and their templates |
| GET | `/api/tracker/macros` | The macros templates can use |
| POST | `/api/tracker/endpoints/{id}/preview` | Renders the given templates for a sample lead, without saving. Problems are listed in `errors` |
| PUT | `/api/tracker/endpoints/{id}` | Saves `{ "url_template", "body_template", "event_id_template" }`. `400` per field if a rule is broken |
| POST | `/api/tracker/endpoints/{id}/reset` | Puts the templates back to the defaults |
| GET | `/api/inbound/settings` | The token for the pixel and postback URLs, and the fields they take |
| GET | `/api/inbound/pixel.png?token=…&name=…&email=…` | Creates a lead from the query string and returns a 1×1 transparent PNG |
| POST | `/api/inbound/postback?token=…` | Creates a lead from form fields (`application/x-www-form-urlencoded`), `201` with the lead |

Errors: bad input → `400` with the field names (`errors.email`, `errors.status`);
a broken rule (e.g. converting a lost lead) → `409` with a readable message;
unknown lead → `404`.

## The 5-step test sequence

| # | What to do | Expected |
|---|---|---|
| 1 | `GET /api/tracker/ping` | `{"status_code":200, "body":"{\"ok\":true,...}"}` |
| 2 | Create a lead, convert it | event `conv_<id>`, attempt `201`, event `sent` |
| 3 | Convert the same lead again (UI: **Convert again**) | same event, new attempt `200` with `duplicate:true`, still `sent` |
| 4 | Send a conversion without email and phone | `422` (see below) |
| 5 | Create a new lead, convert it with **Test retries** ticked (API: `"simulate_server_error": true`) | attempt 1: `500`, attempt 2 (about 10 s later): `201`. Same `event_id` |

**Step 4:** the app itself refuses to convert a lead without email and phone
(`409`), so this request never reaches the tracker from the app. To show the
tracker's `422`, call it directly. In bash (or Git Bash on Windows), set the key
in your shell only, never in a file:

```bash
export TRACKER_API_KEY=<your key>
curl -s -X POST https://bipro2interface.sseku.com/api/candidate-tracker/conversions \
  -H "Authorization: Bearer $TRACKER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"event_id":"conv_test_422","event_name":"purchase"}'
```

Expected: `422` with `"contact": ["Provide email or phone."]`.

**Step 5:** the simulate flag is added to the **first attempt only**. The stored
body stays clean, so the automatic retry sends the same `event_id` without
`simulate` and gets `201`.

## Design decisions

### Outbox table + background worker (instead of a queue)

The task asks to persist the event before sending it. I chose an **outbox
table** and a `BackgroundService` worker:

1. `POST /api/leads/{id}/status` changes the status and adds a `TrackerEvent`
   row (`pending`) in **one** `SaveChanges`. Both are saved, or neither is.
2. The controller returns right away. It never calls the tracker.
3. The worker checks every 5 seconds for pending events that are due, sends
   them, and saves an attempt row for each send.

Why: it needs no extra infrastructure (no Redis, no Hangfire), it is easy to
read, and the event survives an app restart because it lives in the database.

### Retry rules

| Tracker answer | Result |
|---|---|
| `2xx` (`201`, or `200` with `duplicate:true`) | `sent` |
| `5xx`, `429`, timeout, network error | stays `pending`, retried after 10 s, 20 s, 40 s, 80 s. After 5 attempts: `failed` |
| other `4xx` (`401`, `422`) | `failed`. Sending the same request again won't help |

### Idempotency

- The `event_id` is `conv_{lead_id}`, and it is unique in the database. There is
  one event per lead per endpoint.
- Retries and "convert again" reuse the **same** event row and the same
  `event_id`. They never create a second event.
- "Convert again" on a converted lead re-queues the same event. The tracker
  answers `duplicate: true`, which counts as success. This also lets you retry
  an event that ended as `failed`. An event that is still `pending` is left
  alone.
- If 10 "convert" requests arrive at the same time, the unique index lets only
  one create the event. Every answer is `200` or `409`, never a `500` (covered
  by a concurrency test).

### Request/response logging

Every send is stored as a `TrackerAttempt`: time, URL, request body, status
code, response body, or the error (e.g. timeout). The UI event log and
`GET /api/leads/{id}/events` show them. Headers are **not** stored, so the API
key never ends up in the database.

### Snapshot at conversion

The URL, the body and `occurred_at` are rendered when the lead converts and
stored on the event. Every automatic retry sends exactly the same request, even
if the lead or the endpoint settings are edited in the meantime.

**"Convert again" uses the current settings.** It renders the URL and body again
from the endpoint templates and the lead as they are now. So if an event failed
because of a wrong URL (or a wrong email on the lead), you fix it and click
**Convert again**, and the next attempt goes to the new URL with the new data.
The `event_id` and `occurred_at` stay the same as the first time, so the tracker
still sees the same conversion and never counts it twice. An event that is still
`pending` is not changed (the worker may be sending it at that moment); wait
until it is `sent` or `failed`, then convert again.

### Tracker endpoints with macros

The tracker URLs live in a `TrackerEndpoints` table (seeded by the migration
from `DefaultEndpoints`), not in config. The body is a template with
placeholders like `{event_id}`, `{email}` and `{amount}`. Values are
URL-encoded in the URL and JSON-escaped in the body, so a name like
`Ann "A" & B` can't break the request. `{amount}` has no quotes, so it is sent
as a number.

The **Tracker settings** page edits these templates. It shows the macro list, a
live preview of the request for a sample lead, **Reset to default** and
**Test connection** (ping). Only the URL, body and `event_id` templates can be
changed; the name, method and trigger status stay fixed, because the code
relies on them. On save, the backend rejects:

- unknown macros, and any macro on an endpoint that isn't sent for a lead (ping);
- an empty URL, or a template longer than its database column;
- a URL that isn't a full `https://` address, so the key is never sent unencrypted;
- a body that isn't valid JSON once filled in with the sample lead, or a body on a `GET`;
- an `event_id` that uses anything other than `{lead_id}` and `{event_type}`, or
  doesn't contain `{lead_id}` (every lead would get the same `event_id`), or could
  be longer than 128 characters.

An edit changes new events and events sent with "convert again". Automatic
retries of an existing event keep its stored URL and body. The `event_id` of an
existing event never changes, so the tracker still sees the same conversion.

### Lead statuses in a table

Statuses live in a `LeadStatuses` table, seeded with the 5 statuses from the
task. `Leads.Status` has a foreign key to it, so the database rejects unknown
statuses. The UI reads the list from `GET /api/leads/statuses`. The code knows
only the 3 names its rules need (`new`, `converted`, `lost`).

### Code structure

Controllers are thin. They call a service (`LeadService`), which returns a
result (success, 404, 400 or 409) instead of throwing, and the controller maps
it to an HTTP status. The rules themselves live on the entities
(`Lead.TryChangeStatus`, `TrackerEvent.RecordAttempt`), so they can be unit
tested without HTTP or a database. There is no repository layer: the service
uses the EF `DbContext` directly.

### Secrets

- The API key lives only in `.env` (ignored by git) or a real environment variable.
- `.env.example` is the committed template, with no values.
- `requests.http` calls only our API and never contains the key.
- Attempts store URL and body, never headers. HTTP client logging redacts the
  `Authorization` header.

### Lead capture from the client's website (pixel and postback)

**Tracker settings** shows two ready-to-copy URLs that the client puts in their
own website form, so new leads land in the CRM:

- **Pixel:** `GET /api/inbound/pixel.png?token=…&name=Ann&email=ann@example.com`.
  The browser loads it as an image after the form is sent. It always answers
  with `Cache-Control: no-store`, so a cached image never skips a lead.
- **Postback:** `POST /api/inbound/postback?token=…` with form fields, from an
  HTML `<form method="post">` or from the client's server (`curl -d name=Ann -d email=…`).

Fields: `name`, `email`, `phone`, `source`, `amount`, `currency`, with the same
rules as `POST /api/leads`. A new lead is always `new`; a `status` field is
ignored. Without a `source`, the lead gets `pixel` or `postback`.

Security, because these URLs are public:
- **Token:** every request needs `?token=`, already filled in the URLs on the
  settings page. The app creates it at the first startup (64 random hex
  characters) and keeps it in the one-row `InboundSettings` table, so it stays the
  same after restarts and nothing needs setting up. It is not in the migration,
  because a token in code would be the same for everyone who clones the repo. It
  is compared in fixed time, in an authorization filter that runs before the
  fields are read, so a caller without it gets `401` and learns nothing about the fields.
- **Rate limit:** 30 requests a minute per IP, then `429` (built-in ASP.NET Core
  rate limiter, no extra package).
- The token is not the tracker API key, and the tracker key is never shown.

## Assumptions

- **`converted` is final.** A converted lead can't move to another status.
  Converting it again is allowed: it re-sends the same event.
- **Converting needs a contact and an amount**, and a `lost` lead can't be
  converted (from the task).
- **`lost` is final too.** A lost lead can't move to another status, otherwise
  `lost → contacted → converted` would get around the task rule. Other status
  changes are free.
- **`currency` is required when `amount` is set**, so a converted lead always
  has both. It is stored in upper case.
- **Status changes only go through `POST /api/leads/{id}/status`.** `PUT` can't
  change status, and a new lead always starts as `new`.
- **A converted lead must keep its amount**, otherwise "convert again" would fail.
- **Deleting a lead that has tracker events is blocked** (`409`), to keep the
  audit trail.
- **`source` is free text**, stored trimmed and in lower case, so `Facebook` and
  `facebook` are the same source.
- **An empty field is sent as an empty string.** For example, a lead with only
  an email is sent with `"phone": ""`.
- **`occurred_at` is the time of conversion**, in UTC.
- **`429` (rate limit) is retried** like a `5xx`. The worker sends at most 2
  events every 5 seconds (24 a minute), below the tracker's limit of 30.

## Known limits

- **Crash after sending but before saving:** the event is sent again. The
  tracker answers `duplicate: true`, which counts as success. This is why a
  stable `event_id` matters.
- **One app instance:** with several instances, two workers could pick the same
  event. That would need row locks (`UPDLOCK, READPAST`) or Hangfire.
- **Polling every 5 s:** up to 5 seconds before an event is sent. A signal from
  the request to the worker could make it instant.
- **No host restriction on tracker URLs:** the Bearer key is sent to whatever URL
  is in `TrackerEndpoints`, and anyone who can open the settings page can change
  it. Only `https://` is allowed, so the key is never sent unencrypted. In a real
  product, editing endpoints would be admin-only, with a list of allowed hosts.
- **Settings edits are "last save wins":** if two people save the same endpoint
  at the same time, the later save replaces the earlier one (never a mix of the
  two). A real product would warn about the other person's change.
- **Very long URLs:** a URL template near 2000 characters plus long lead values
  could render a URL longer than the 2000-character column, and that conversion
  would fail with a `500`.
- **Changing the `event_id` template can clash with old events:** e.g. after
  changing `conv_{lead_id}` to `conv_1{lead_id}`, lead 23 gets `conv_123`, which
  lead 123 may already have. The unique index blocks it (`409`, not a second
  event), but the message ("changed by another request") doesn't say why. Keep a
  separator after the prefix, or use a new prefix.
- **No authentication on the API.** It is a local demo; a real CRM would need
  login and per-user access to leads.
- **The inbound token only helps once the API has a login:** today anyone who
  can reach the pixel can also call `POST /api/leads` directly (no token, no rate
  limit), or read the token from `/api/inbound/settings`. The token and the rate
  limit are built for the day the rest of the API needs a login, when the two
  inbound URLs stay the only public ones.
- **The inbound token is not truly secret:** a pixel in a public web page shows
  it to every visitor. It stops random spam, not a determined attacker. A real
  product would add a captcha or send leads only from the client's server, and
  a way to change the token from the UI (today: delete the `InboundSettings` row
  and restart). It is in the URL, so it can also end up
  in server access logs.
- **Inbound duplicates:** a page reload fires the pixel again and creates a
  second lead. There is no de-duplication by email or phone.
- **Inbound rate limit per IP:** visitors behind one shared IP (an office, a
  proxy) share the 30 a minute. Behind the Vite dev proxy or a reverse proxy,
  every request comes from the proxy's IP, so all visitors share one limit
  (a real setup would read `X-Forwarded-For` from a trusted proxy). Not covered by a test, because the limit is per
  minute and all integration tests share one app. Checked by hand: requests 1–30 pass, request 31 gets `429`.
- **Inbound error field names:** form and query errors name the field in C#
  style (`Email`), JSON errors in JSON style (`email`).
- **Statuses can be deleted when unused:** the foreign key only protects statuses
  in use. There is no API to edit statuses, but someone with direct database
  access could delete `new` or `lost` and break the rules. A real product would
  block that.
- **Upper/lower case depends on the database:** status lookups ignore case
  because SQL Server's default collation does.

## Proof against the real tracker

_To be added in the final check: the attempt log from running the 5 steps._
