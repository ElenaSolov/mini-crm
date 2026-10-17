# Mini CRM + Conversion Tracker

A small CRM for leads. When you mark a lead as `converted`, the app saves a
conversion event, and a background worker sends it to the tracker. Failed sends
are retried, and every request and response is saved, so you can always see
what happened.

**Flow:** lead created → status changed to `converted` → outbound conversion request

## Stack

- Backend: ASP.NET Core 8 Web API, EF Core 8, SQL Server
- Frontend: React 18 + TypeScript (Vite), RTK Query
- Tests: xUnit

## Project structure

```
backend/MiniCrm.Api/     API, background worker, EF migrations
  Controllers/           thin: call a service, turn the result into an HTTP status
  Services/              the use cases, macro renderer, tracker client, worker
  Models/                entities with their rules (Lead, TrackerEvent)
  Data/                  DbContext, default data, migrations
backend/MiniCrm.Tests/   unit, integration and concurrency tests
frontend/                React UI
requests.http            ready-made calls to the API
```

## Setup

### What you need

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) 20 or newer
- SQL Server: LocalDB on Windows (comes with Visual Studio), Docker on Mac/Linux

### 1. Add your API key

```bash
cp .env.example .env
```

Then set `Tracker__ApiKey` in `.env`, or later on the **Tracker settings** page.
The tracker URLs and auth header defaults are already filled in.

### 2. Database

**Windows with LocalDB 13.0+:** nothing to do.

**Mac/Linux, or Windows without a recent LocalDB:** start SQL Server in Docker, then uncomment the
`ConnectionStrings__Default` line in `.env` (its password matches this command).

Works the same in Bash and PowerShell:

```bash
docker run -d --name minicrm-sql -p 1433:1433 -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_strong_pass1" mcr.microsoft.com/mssql/server:2022-latest
```

Docker Desktop must be running. Check the container with `docker ps`.

The app creates the database and its starting data (lead statuses, tracker
endpoints, auth header) when it starts.

### 3. Run the backend

```bash
dotnet run --project backend/MiniCrm.Api
```

API: http://localhost:5080, Swagger: http://localhost:5080/swagger.
If there is no key (in `.env` or saved on the settings page), you'll see a warning in the log.

### 4. Run the frontend

In a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173.

### 5. Run the tests

```bash
dotnet test
```

Integration tests need SQL Server. On Windows they use LocalDB. On Mac/Linux,
point them to Docker:

```bash
export MINICRM_TEST_DB="Server=localhost,1433;Database=MiniCrm_Tests;User Id=sa;Password=Your_strong_pass1;TrustServerCertificate=True"
dotnet test
```

The tests wipe their database on every run, so they only run on a database
whose name ends with `_Tests`. They use a fake tracker, never the real one.

## Environment variables

Real environment variables win over `.env`.

| Variable | Required | What it is |
|---|---|---|
| `Tracker__ApiKey` | yes, unless set on the settings page | Your tracker API key. A key saved on the settings page wins |
| `Tracker__PingUrl` | yes | Default URL of the `ping` endpoint |
| `Tracker__ConversionsUrl` | yes | Default URL of the `purchase` endpoint |
| `Tracker__AuthHeaderName` | yes | Default auth header name for ping and purchase, e.g. `Authorization` |
| `Tracker__AuthHeaderTemplate` | yes | Default auth header value, e.g. `'Bearer {api_key}'`. `{api_key}` is replaced with the key when a request is sent |
| `ConnectionStrings__Default` | no | SQL Server connection string. Default: LocalDB (in `appsettings.json`) |
| `MINICRM_TEST_DB` | no | Tests only. The database name must end with `_Tests` |

## Create a lead and trigger a conversion

### From a website form (pixel or postback)

The **Tracker settings** page has two ready URLs with the token already in them,
plus examples to copy. Put one in the website's form. Each sent form creates a
new lead (`new`, source `pixel` or `postback` unless the form sends its own).
From there it's a normal lead. See [Lead capture](#lead-capture-from-a-website)
for how it works.

```bash
# Take the token from GET /api/inbound/settings
curl -s "http://localhost:5080/api/inbound/pixel.png?token=<token>&name=Ann&email=ann@example.com" -o pixel.png
curl -s -X POST "http://localhost:5080/api/inbound/postback?token=<token>" -d name=Bob -d phone=%2B12025550123
```

### In the UI

1. Click **New lead**. Add a name, an email or phone, an amount and a currency.
2. Open the lead, pick `converted` in **Change status** and click **Save status**.
3. Under **Tracker events** you'll see the event `conv_<id>` as `pending`. In
   about 5 seconds it is sent, and you can see the URL, the request body, the
   status code and the response.

### With the API

Use [requests.http](requests.http), or curl:

```bash
# Create a lead
curl -s -X POST http://localhost:5080/api/leads -H "Content-Type: application/json" -d '{"name":"Ann Lee","email":"ann@example.com","amount":199.00,"currency":"USD","source":"facebook"}'

# Convert it (use the id from the answer above)
curl -s -X POST http://localhost:5080/api/leads/1/status -H "Content-Type: application/json" -d '{"status":"converted"}'

# About 5 seconds later: the event and its attempts
curl -s http://localhost:5080/api/leads/1/events
```



### API overview

| Method | Path | What it does |
|---|---|---|
| GET | `/api/leads` | Leads, newest first, 10 a page. Filters: `name`, `email`, `phone`, `source`, `currency` (one or more), `status`, `id`, `amount_from/to`, `created_from/to`, `page`, `page_size`. Text filters match part of the value and ignore case. Each lead has `outbound_status` (its newest event's status) |
| POST | `/api/leads` | Create a lead (always starts as `new`) |
| GET | `/api/leads/{id}` | One lead |
| PUT | `/api/leads/{id}` | Edit a lead. Can't change status |
| DELETE | `/api/leads/{id}` | Soft delete: the lead is hidden, its row and tracker events stay |
| POST | `/api/leads/{id}/status` | Change status: `{ "status": "converted", "simulate_server_error": false }` |
| GET | `/api/leads/{id}/events` | The lead's events with every attempt |
| GET | `/api/events?status=` | All events, newest first (the Logs page) |
| GET | `/api/leads/statuses` | Allowed statuses |
| GET | `/api/tracker/ping` | Calls the tracker's `/ping` with the key |
| GET | `/api/tracker/endpoints` | Tracker endpoints and their templates |
| GET | `/api/tracker/macros` | Macros the templates can use |
| POST | `/api/tracker/endpoints/{id}/preview` | Shows the request for a sample lead, without saving |
| PUT | `/api/tracker/endpoints/{id}` | Saves the URL, body and `event_id` templates |
| POST | `/api/tracker/endpoints/{id}/reset` | Back to the default templates |
| GET | `/api/tracker/auth` | The shared auth header, and where the key comes from (never the key) |
| PUT | `/api/tracker/auth` | Saves the auth header, and the key if one is sent |
| POST | `/api/tracker/auth/reset` | Auth header back to the `.env` default (keeps the key) |
| DELETE | `/api/tracker/auth/key` | Deletes the saved key, so `.env` is used again |
| GET | `/api/inbound/settings` | The token and the fields the pixel and postback take |
| PUT | `/api/inbound/token` | Set your own token (16–64 letters, digits, `-`, `_`) |
| POST | `/api/inbound/token/generate` | New random token |
| GET | `/api/inbound/pixel.png?token=…` | Creates a lead, returns a 1×1 image |
| GET, POST | `/api/inbound/postback?token=…` | Creates a lead from form fields or the query string |

Errors: bad input → `400` with the field names; a broken rule (like converting
a lost lead) → `409` with a message; unknown lead → `404`.

## The 5-step test sequence

| # | What to do | Expected |
|---|---|---|
| 1 | `GET /api/tracker/ping` | `{"status_code":200, "body":"{\"ok\":true,...}"}` |
| 2 | Create a lead, convert it | event `conv_<id>`, attempt `201`, event `sent` |
| 3 | Convert the same lead again (UI: **Convert again**) | same event, new attempt `200` with `duplicate:true`, still `sent` |
| 4 | Send a conversion without email and phone | `422` (see below) |
| 5 | Create a lead, convert it through the API with `"simulate_server_error": true` | attempt 1: `500`, attempt 2 (about 10 s later): `201`, same `event_id` |

**Step 4:** the app won't convert a lead without email and phone (`409`), so
this request can't reach the tracker from the app. To see the `422`, call the
tracker directly. Set the key in your shell only, never in a file:

```bash
export TRACKER_API_KEY=<your key>
curl -s -X POST https://bipro2interface.sseku.com/api/candidate-tracker/conversions -H "Authorization: Bearer $TRACKER_API_KEY" -H "Content-Type: application/json" -d '{"event_id":"conv_test_422","event_name":"purchase"}'
```

**Step 5:** only the first attempt gets the `simulate` field. The saved body
stays clean, so the retry sends the same event without it and gets `201`.

## Design decisions

### Outbox table + background worker

The task says to save the event before sending it. I save it in a table and
send it from a background worker:

1. Converting a lead changes its status and adds a `pending` event in one save.
   Both are saved, or neither.
2. The request returns right away. It doesn't wait for the tracker.
3. Every 5 seconds the worker sends up to 2 due events, oldest first, and saves
   each attempt.

Why: nothing extra to run (no Redis, no Hangfire), easy to follow, and events
survive a restart because they live in the database. This is the task's
"table + retry command" option: the worker retries by itself, and
**Convert again** is the manual retry.

### Retry rules

| Tracker answer | Result |
|---|---|
| `2xx` | `sent` |
| `5xx`, `429`, timeout, no connection | stays `pending`, retried after 10 s, 20 s, 40 s. Still failing after that: `failed` |
| other `4xx` (`401`, `422`) | `failed` right away. Sending the same request again won't help |

### No double conversions

- One event per lead: the `event_id` is `conv_{lead_id}`, and it is unique in
  the database.
- Retries and **Convert again** reuse the same event and the same `event_id`.
  The tracker answers `duplicate: true`, which counts as success.
- 10 converts at the same time still create one event. Every answer is `200`
  or `409` (covered by a concurrency test).

### What gets saved

Every attempt: time, URL, request body, status code, response body or error.
Headers are not saved, so the API key never ends up in the attempt log. You can
see the attempts under the lead, on the Logs page, and in
`GET /api/leads/{id}/events`.

The URL and body are built once, when the lead converts, and saved on the
event. Automatic retries send exactly that, even if the lead or the settings
change in the meantime.

**Convert again** builds them again from the current lead and settings. So if a
send failed because of a wrong URL or email, you fix it and convert again. The
`event_id` and `occurred_at` stay the same, so the tracker sees the same
conversion. A `pending` event can't be converted again: wait until it is `sent`
or `failed`.

### Editable tracker endpoints

The tracker URLs and request bodies are templates in the `TrackerEndpoints`
table, with macros like `{email}` and `{amount}`. Values are escaped, so a name
like `Ann "A" & B` can't break the request. `{amount}` is sent as a number.

The default URLs come from `.env`. At startup the app adds an endpoint to the
table only if it isn't there yet (a new database gets both). After that the
table is used, and `.env` is only what **Reset to default** puts back. A missing
or invalid default stops the app at startup with a clear message.

The **Tracker settings** page edits them, with the macro list, a live preview,
**Reset to default** and **Test connection**. Only the URL, body and `event_id`
templates can change. Saving is refused for:

- unknown macros, or macros on ping (it isn't sent for a lead);
- an empty or too long template;
- a URL that isn't `https://`, so the key is never sent unencrypted;
- a body that isn't valid JSON, or a body on a `GET`;
- an `event_id` without `{lead_id}`, with other macros than `{lead_id}` and
  `{event_type}`, or longer than 128 characters.

An edit applies to new events and to **Convert again**, never to automatic
retries.

### Tracker auth (header and API key)

Ping and purchase share one auth setting, in the `TrackerAuthSettings` table
(one row, created at the first start from `.env`). The **Tracker auth** card on
the settings page edits it:

- **Header name and value**, e.g. `Authorization` + `Bearer {api_key}`, or
  `X-Api-Key` + `{api_key}`. **Reset to default** puts the `.env` values back.
- **API key.** A key saved here wins over `Tracker__ApiKey` in `.env`;
  **Delete saved key** goes back to `.env`. The key is write-only: the API never
  sends it back, the page only says where the key in use comes from, and the
  preview shows `***`. An empty key field keeps the current key.

Saving is refused for a header name that isn't one word of letters, digits and
`-` (or is one .NET sets itself: `Host`, `Content-Type`, ...), a header value
without `{api_key}`, with other macros or a line break, and a key with spaces or
line breaks. `{api_key}` can't be used in URL or body templates.

The header and key are read when each request is sent (they are not copied into
the event), so a fixed key also fixes the retries of events already queued.

### Lead statuses in a table

The 5 statuses from the task live in a `LeadStatuses` table, and leads point to
it with a foreign key, so an unknown status can't be saved. The UI reads the
list from the API. The code only knows the 3 statuses its rules need: `new`,
`converted`, `lost`.

### Code structure

Controllers are thin. They call a service, which returns a result (success,
404, 400 or 409) instead of throwing, and the controller turns it into an HTTP
status. The rules live on the entities (`Lead.TryChangeStatus`,
`TrackerEvent.RecordAttempt`), so they can be unit tested without HTTP or a
database. No repository layer: services use the EF `DbContext` directly.

### Secrets

- The API key lives in `.env` (ignored by git), a real environment variable, or,
  if set on the settings page, in the `TrackerAuthSettings` table. It is never in
  committed files and never sent back by the API.
- **Known risk:** a key saved on the settings page is stored as **plain text**.
  Anyone who can read the database (or a backup) can read the key, and anyone who
  can open the settings page can replace it (the app has no login). A real product
  would encrypt it (e.g. ASP.NET Data Protection) and limit the page to admins.
- `.env.example` is the committed template, with no key (only the public tracker URLs).
- `requests.http` calls only our API and never has the key.
- Attempts never save headers, and HTTP client logs hide every request header
  (the auth header name can be changed).

### Lead capture from a website

- **Pixel:** `GET /api/inbound/pixel.png?token=…&name=Ann&email=…`. The browser
  loads it as an image after the form is sent. It is never cached, so no lead
  is skipped.
- **Postback:** `POST /api/inbound/postback?token=…` with form fields, from an
  HTML form or the client's server. The fields can also go in the query string,
  and `GET` works too. If a field is in both, the form field wins. JSON is
  refused (`415`).

Fields: `name`, `email`, `phone`, `source`, `amount`, `currency`, with the same
rules as `POST /api/leads`. A `status` field is ignored.

These URLs are public, so:

- **Token:** every request needs `?token=`. The app makes a random one at the
  first start and keeps it in the database, so each install has its own. It is
  checked before any field is read, and in fixed time. Without it: `401`.
- **Rate limit:** 30 requests a minute per IP, then `429`.
- The token is not the tracker API key, and the key is never shown.

## Assumptions

- **`converted` is final.** A converted lead can't change status. Converting it
  again is allowed: it re-sends the same event.
- **Converting needs a contact and an amount**, and a `lost` lead can't be
  converted (from the task).
- **`lost` is final too.** Otherwise `lost → contacted → converted` would get
  around the task rule. Other status changes are free.
- **`currency` is required when `amount` is set.** It is saved in upper case.
- **Status only changes through `POST /api/leads/{id}/status`.** A new lead
  always starts as `new`.
- **A converted lead must keep its amount**, or **Convert again** would fail.
- **Delete is a soft delete.** The lead gets a `DeletedAt` and disappears from the
  API (404), but the row and its tracker events stay, so the Logs page keeps the
  history. Events already queued are still sent: the conversion really happened.
- **`source` is free text**, saved trimmed and in lower case, so `Facebook` and
  `facebook` are the same.
- **An empty field is sent as an empty string**, e.g. `"phone": ""`.
- **`occurred_at` is the time of conversion**, in UTC.

## Known limits

- **Crash after sending but before saving:** the event is sent again. The
  tracker answers `duplicate: true`, so nothing is counted twice.
- **One app instance only:** with more, two workers could send the same event.
  That would need row locks (`UPDLOCK, READPAST`) or Hangfire.
- **Up to 5 seconds before an event is sent**, longer when many are waiting
  (2 per run).

## Proof against the real tracker

Cannot be done without API key, everythig were reproduced on local envoirment  