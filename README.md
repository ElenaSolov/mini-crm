# Mini CRM + Conversion Tracker

A small CRM for leads. When a lead is marked as `converted`, the app stores an
outbound conversion event and a background worker sends it to the tracker API,
with retries and full request/response logging.

**Flow:** lead created → status changed to `converted` → outbound conversion request

## Stack

- Backend: ASP.NET Core 8 Web API, EF Core 8, SQL Server
- Frontend: React 18 + TypeScript (Vite)
- Tests: xUnit

## Project structure

```
backend/    ASP.NET Core API + background worker
frontend/   React UI
```

## Setup

_TODO_

## Environment variables

Secrets are never committed. Copy `.env.example` to `.env` and fill in the values.

| Variable | Required | Description |
|---|---|---|
| `Tracker__BaseUrl` | yes | Tracker API base URL |
| `Tracker__ApiKey` | yes | Your personal tracker API key |
| `ConnectionStrings__Default` | no | SQL Server connection string (defaults to LocalDB) |

## How to create a lead and trigger a conversion

_TODO_

## Design decisions

_TODO_

## Assumptions

_TODO_
