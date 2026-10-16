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
frontend/                React UI (one page)
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

The URL, the body and `occurred_at` are rendered once, when the lead converts,
and stored on the event. Every retry sends exactly the same request, even if the
lead is edited later.

### Tracker endpoints with macros

The tracker URLs live in a `TrackerEndpoints` table (seeded by the migration
from `DefaultEndpoints`), not in config. The body is a template with
placeholders like `{event_id}`, `{email}` and `{amount}`. Values are
URL-encoded in the URL and JSON-escaped in the body, so a name like
`Ann "A" & B` can't break the request. `{amount}` has no quotes, so it is sent
as a number. A settings page to edit these templates is planned as an extra.

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

## Assumptions

- **`converted` is final.** A converted lead can't move to another status.
  Converting it again is allowed: it re-sends the same event.
- **Converting needs a contact and an amount**, and a `lost` lead can't be
  converted (from the task). Other status changes are free.
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
  is in `TrackerEndpoints`. In a real product, editing endpoints would be
  admin-only, with a list of allowed hosts.
- **No authentication on the API.** It is a local demo; a real CRM would need
  login and per-user access to leads.
- **Statuses can be deleted when unused:** the foreign key only protects statuses
  in use. There is no API to edit statuses, but someone with direct database
  access could delete `new` or `lost` and break the rules. A real product would
  block that.
- **Upper/lower case depends on the database:** status lookups ignore case
  because SQL Server's default collation does.

## Proof against the real tracker

_To be added in the final check: the attempt log from running the 5 steps._
