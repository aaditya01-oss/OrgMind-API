# OrgMind API

The .NET 8 API of OrgMind AI. It is the only thing the browser talks to. It owns identity, roles,
tickets, forms, staff messaging and the audit log, and it forwards AI work to the Python engine
(OrgMind repository).

## What works today

- **Sign-in and sign-up:** organization creation, staff invites (with expiry), customer sign-up.
  Passwords are hashed with BCrypt; sign-in uses short-lived JWTs (8 hours).
- **Four tiers:** visitor, customer, employee (with a department), admin. Role and department come
  from the signed token, never from the browser.
- **Tickets:** visible to their creator, to the handling department, and to admins. Nobody can change
  the status of their own ticket.
- **Forms:** admins build templates (audience, handling department, optional scan-to-fill).
  Submissions go to the handling department. Sensitive answers are hidden in the UI and only shown
  through an audited "reveal". Rejecting needs a reason. Closed requests can only be reopened by an admin.
- **Conversations** on tickets and submissions (customer can reply; staff identity is shown as
  "Support team").
- **Staff messages:** private direct messages, and department groups (members plus admins).
- **Audit log:** filterable page and CSV export. Tickets, forms and messages are logged by id only, never their text. Events for questions to the assistant keep a short preview of the question; this is on the list to tighten.
- **Live updates** over SignalR (WebSocket). Live messages carry no text, only "refresh" signals.
- **Rate limits** per user or IP, stricter on sign-in and anything that costs AI quota.

## Known limits (honest list)

- **No automated test suite yet.** Access rules are enforced in code and checked by hand; automated
  tests for them are the next planned step.
- Sensitive form answers are hidden from other people but **not yet encrypted at rest**.
- A password change does not yet invalidate tokens already issued (they expire after 8 hours).
- The audit log is protected by application code only. The database does not stop a direct edit.
- Several tables have no database-level foreign key to `organizations`; the API enforces the link.
- Live updates work on a single server. More than one server would need a SignalR backplane (e.g. Redis).
- CORS allows only `http://localhost:3000`. It must be made configurable before deployment.
- Not yet built: public (anonymous) forms, company settings, feedback on answers, compliance reports.

## Run it locally

You need: the .NET 8 SDK, PostgreSQL 16 with pgvector, and the AI engine running.

1. **Database:** `CREATE DATABASE orgmind;` then load `db/schema.sql`.
   With Docker, for example:

```powershell
docker run --name orgmind-db -e POSTGRES_PASSWORD=CHANGE_ME -p 5432:5432 -d pgvector/pgvector:pg16
docker exec orgmind-db psql -U postgres -c "CREATE DATABASE orgmind"
docker cp db\schema.sql orgmind-db:/tmp/schema.sql
docker exec orgmind-db psql -U postgres -d orgmind -f /tmp/schema.sql
```

2. **Settings.** Copy `OrgMind.API/appsettings.Development.example.json` to
   `OrgMind.API/appsettings.Development.json` and fill in your own values.
   - `JwtSecret` must be at least 32 random characters. The API refuses to start otherwise.
   - `InternalApiKey` must equal `INTERNAL_API_KEY` in the engine's `.env`.
   - `AiEngineUrl` is optional; it defaults to `http://localhost:8000`.
3. **Start:**

```powershell
cd OrgMind.API
dotnet run
```

The API listens on `http://localhost:5147`.

## Security notes

- `appsettings.Development.json` is git-ignored. Never commit real keys.
- Every query is scoped by the organization in the token. A missing item and an item you may not see
  give the same "not found" answer.