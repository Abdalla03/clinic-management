# Clinic Management

A production-oriented clinic management system for a single Egyptian doctor's practice, built with ASP.NET Core (.NET 10 LTS), EF Core and PostgreSQL. It serves three roles — **doctors**, **secretaries** and **patients** — over a JWT-secured REST API.

**All eight phases are complete** (foundation → billing → docs). The suite has **324 automated tests**: 243 unit + 81 integration (integration tests need Docker and skip automatically when it is unavailable), plus **8 frontend flow tests** in `web/` (Vitest + Testing Library).

## Solution layout

| Project | Responsibility |
| --- | --- |
| `src/ClinicManagement.Domain` | Entities and business invariants. No external dependencies. |
| `src/ClinicManagement.Application` | Use-case contracts, service interfaces, FluentValidation validators, paging, TOTP (RFC 6238). |
| `src/ClinicManagement.Infrastructure` | EF Core persistence, Identity, JWT issuing, AES-GCM secret protection, services, seeding. |
| `src/ClinicManagement.Api` | HTTP composition root: controllers, authorization policies, Swagger, middleware. |
| `tests/ClinicManagement.UnitTests` | Fast unit tests: domain rules, validators, model/migration sync, RFC 6238/4648 vectors. |
| `tests/ClinicManagement.IntegrationTests` | PostgreSQL (Testcontainers) + `WebApplicationFactory` endpoint tests. |

## Prerequisites

- .NET SDK 10 (see `global.json`).
- PostgreSQL 17, either locally or via Docker.
- Docker (for the integration tests and the Compose stack).
- `dotnet-ef` is restored automatically from `dotnet-tools.json` (`dotnet tool restore`).

## Run with Docker Compose

1. Copy `.env.example` to `.env` and fill in the secrets:

   ```powershell
   Copy-Item .env.example .env
   # POSTGRES_PASSWORD, Jwt__SigningKey and Clinic__Encryption__MasterKeyBase64 are required
   openssl rand -base64 48   # Jwt__SigningKey
   openssl rand -base64 32   # Clinic__Encryption__MasterKeyBase64
   ```

2. Start the stack:

   ```powershell
   docker compose --env-file .env up --build
   ```

   The API applies migrations on startup (`--migrate`) and listens on `http://localhost:8080`.
   Readiness: `GET http://localhost:8080/health/ready`.
   Swagger (Development/Testing only): `http://localhost:8080/swagger`.
   The frontend (`web/`) is built into an nginx image and served at `http://localhost:3000`.
   `WEB_API_URL` (default `http://localhost:8080`) is passed to the `web` service as a build arg (`VITE_API_URL`) and injected at container start into `config.js`, which the SPA reads at runtime — change it without rebuilding.

The Compose setup uses HTTP for local development. **Production must terminate TLS in front of the API**; outside Development/Testing the API enforces HSTS and HTTPS redirects.

`docker compose up -d --wait` blocks until the database, API and frontend pass their health checks. All three services define health checks (`/health/ready` for the API, `/healthz` for nginx).

## Production deployment

A production overlay sits behind a Caddy reverse proxy that terminates TLS (Let's Encrypt), adds HSTS and other security headers, and proxies `/api` and `/health` to the API while serving the SPA:

```powershell
# 1. Fill in the production values (see .env.example) and generate the two required secrets
pwsh -File scripts/generate-secrets.ps1        # writes Jwt__SigningKey + Clinic__Encryption__MasterKeyBase64
# set POSTGRES_PASSWORD, DOMAIN, ACME_EMAIL in .env

# 2. Bring up the hardened stack
docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env up -d --build --wait
```

What the overlay changes:

- the API runs as **Production**: HSTS + HTTPS redirect on, Swagger off, **no demo accounts or data seeded**, and the process **refuses to start** if `Jwt__SigningKey` or `Clinic__Encryption__MasterKeyBase64` is missing, too short, or still a default;
- only **Caddy** is published to the host (80/443); the database, API and web ports bind to `127.0.0.1` only;
- CORS is restricted to the single public `https://$DOMAIN` origin.

The API trusts `X-Forwarded-Proto`/`X-Forwarded-For` from the proxy so HTTPS detection and HSTS behave correctly behind TLS termination.

### Backups

`scripts/backup-db.ps1` writes a compressed `pg_dump` (custom format) to `backups/`, and `scripts/restore-db.ps1 -DumpFile <file>` restores it with `--clean --if-exists`. Both run `pg_dump`/`pg_restore` inside the `db` container, so they work for the dev and production stacks alike:

```powershell
pwsh -File scripts/backup-db.ps1
pwsh -File scripts/restore-db.ps1 -DumpFile backups\clinic-20260101-120000.dump
```

Back up the `.env` encryption key alongside the database: without it, AES-GCM-protected clinical fields cannot be read.

## Run without Docker

1. Start PostgreSQL and create a database/user.
2. Provide configuration through `src/ClinicManagement.Api/appsettings.Development.json`, user secrets or environment variables:
   - `ConnectionStrings__ClinicDb` (preferred), or `Database__Host` / `Database__Name` / `Database__User` / `Database__Password`.
   - `Jwt__SigningKey` and `Clinic__Encryption__MasterKeyBase64`.
3. Apply the schema and run:

   ```powershell
   dotnet run --project src/ClinicManagement.Api -- --migrate
   dotnet run --project src/ClinicManagement.Api
   ```

In Development the startup migration also seeds the demo accounts below. Set `Seed__DoctorPassword` / `Seed__SecretaryPassword` / `Seed__PatientPassword` to override the demo passwords from configuration; without them the demo logins are not created.

## Demo accounts (Development/Testing seed)

| Email | Password (default from `appsettings.Development.json`) | Role |
| --- | --- | --- |
| `doctor@clinic.local` | `Doctor!Clinic2026` | Doctor (linked doctor profile) |
| `secretary@clinic.local` | `Secretary!Clinic2026` | Secretary |
| `patient@clinic.local` | `Patient!Clinic2026` | Patient (linked patient record) |

Sign in with `POST /api/auth/login` using `emailOrUserName` + `password`. MFA is optional: accounts without TOTP enrolled receive a full token immediately; enrolled accounts complete login via `POST /api/auth/mfa/verify`.

### Seeded demo data (Development only)

On top of the three demo accounts, a Development startup seeds a small but realistic practice so every screen has content on first run: four doctors (general + pediatrics, dermatology, orthopedics), nine patients with allergies and medical history, working-hour schedules, appointments across every state (booked, confirmed, completed, cancelled, no-show), visits, prescriptions and invoices covering all billing states with payments. The seeder is **idempotent** — it is safe to restart and never duplicates rows. It runs only when `ASPNETCORE_ENVIRONMENT=Development`; Testing seeds accounts but not clinical data, and Production seeds neither.

## Frontend (React SPA)

The `web/` workspace is a React 19 + TypeScript + Vite SPA that talks to the API. It ships an English/Arabic (RTL) UI, an in-memory JWT store with silent refresh, login + TOTP MFA, and a role-aware app shell. The API base URL resolves from `window.__CLINIC_API_URL__` (`config.js`, written by the Docker entrypoint from `WEB_API_URL`) with a fallback to the build-time `VITE_API_URL` constant.

The UI is themeable and brandable: an animated ambient background, glass panels, skeleton loaders, a **persisted dark mode** toggle (light/system/dark) and print-friendly invoices/prescriptions. White-label the clinic name and palette with two optional build-time variables:

```powershell
VITE_CLINIC_NAME=Clinic Management   # shown in the title and header
VITE_PRIMARY_COLOR=#217d6b           # base colour; a full palette is derived via color-mix
```

Prerequisites: Node.js 22+ (Node 24 tested). On Windows use `npm.cmd` (PowerShell blocks `npm.ps1`).

1. Install and configure:

   ```powershell
   cd web
   npm.cmd install
   Copy-Item .env.example .env   # VITE_API_URL defaults to http://localhost:8080
   ```

2. Generate the typed API client from the running API's Swagger document:

   ```powershell
   npm.cmd run gen:api        # writes src/lib/api/schema.d.ts from %VITE_API_URL%/swagger/v1/swagger.json
   ```

3. Run the dev server (port **3000**):

   ```powershell
   npm.cmd run dev
   ```

   Open `http://localhost:3000`. The API allows this origin via `Cors__AllowedOrigins__0` (default `http://localhost:3000`); run the REST API first (Docker Compose or `dotnet run`).

4. Production build, tests and preview:

   ```powershell
   npm.cmd run build          # tsc -b && vite build → web/dist
   npm.cmd run lint           # oxlint
   npm.cmd run test           # vitest (jsdom) — frontend flow tests
   npm.cmd run preview
   ```

5. Docker image (`web/` only, nginx with SPA fallback — see `web/Dockerfile` and `web/nginx.conf`):

   ```powershell
   docker build -t clinic-web web
   docker run -p 3000:80 -e WEB_API_URL=http://localhost:8080 clinic-web
   ```

Pages: sign-in (`/login`) → optional MFA (`/mfa-verify`); dashboard (`/`); patient search/create/edit and medical history for doctors/secretaries (`/patients`, `/patients/new`, `/patients/:id/edit`, `/patients/:id/medical-history`, `/patients/:id/visits`, `/patients/:id/prescriptions`); read-only own record for patients (`/me`); account security with TOTP enrolment (`/account/security`); appointments week calendar/booking/detail for staff (`/appointments`, `/appointments/new`, `/appointments/:id`) and self-service "my appointments" for patients — browse doctors, pick a free slot, book and cancel (list `/appointments/me`, role-aware booking form `/appointments/new`); doctor working-hours and slot management (`/doctors/schedule`); visits list/record/edit/detail for staff (`/visits`, `/visits/new`, `/visits/:id`, `/visits/:id/edit`) and "my visits" for patients (`/visits/me`); prescriptions list/issue/detail for staff (`/prescriptions`, `/prescriptions/new`, `/prescriptions/:id`) and "my prescriptions" for patients (`/prescriptions/me`); invoices list, create with dynamic line items, and print-friendly detail with issue/payment/void actions for staff (`/invoices`, `/invoices/new`, `/invoices/:id`) and "my invoices" for patients (`/invoices/me`); reports dashboard with revenue/outstanding/visits tabs, date-range filter, Recharts charts and CSV export (`/reports`). Sign in with any of the demo accounts above.

## API overview

All endpoints require a Bearer token except `POST /api/auth/login|refresh|verify` and health checks. Failures return RFC 7807 `application/problem+json` with a stable machine-readable `code`.

| Area | Endpoints | Access |
| --- | --- | --- |
| Auth | `POST /api/auth/login`, `mfa/verify`, `refresh`, `logout`, `mfa/enroll`, `mfa/confirm`, `mfa/disable`; `GET /api/auth/me` | Anonymous / authenticated |
| Patients | `GET/POST /api/patients`, `GET/PUT/DELETE /api/patients/{id}`, `GET/POST /api/patients/{id}/medical-history`, `GET /api/patients/me`, `GET /api/patients/me/medical-history` | Secretary (CRUD), ClinicalStaff (history), Patient (own) |
| Appointments | Staff: `GET/POST /api/appointments`, `GET/PUT /api/appointments/{id}`, `POST /api/appointments/{id}/confirm|complete|no-show|cancel`. Patient (own): `GET /api/appointments/me`, `POST /api/appointments/me`, `POST /api/appointments/me/{id}/cancel`; `GET /api/appointments/{id}` for an appointment they own | ClinicalStaff / Patient (own) |
| Doctor schedules | `GET /api/doctors`, `GET /api/doctors/{id}/availability` (any authenticated user, so patients can book); `GET/POST /api/doctors/{id}/schedule`, `DELETE /api/doctors/{id}/schedule/{scheduleId}` | AuthenticatedUser (read) / ClinicalStaff (manage) |
| Visits | `GET /api/visits`, `GET /api/visits/me`, `GET /api/visits/{id}`, `POST /api/visits`, `PUT /api/visits/{id}`, `GET /api/patients/{id}/visits` | ClinicalStaff / Doctor (write) / Patient (own) |
| Prescriptions | `GET /api/prescriptions`, `GET /api/prescriptions/me`, `GET /api/prescriptions/{id}`, `POST /api/prescriptions`, `GET /api/patients/{id}/prescriptions` | ClinicalStaff / Doctor (write) / Patient (own) |
| Billing | `GET /api/invoices`, `GET /api/invoices/me`, `GET /api/invoices/{id}`, `POST /api/invoices`, `POST /api/invoices/{id}/issue|payments|void` | ClinicalStaff (read) / Secretary (write) / Patient (own) |
| Reports | `GET /api/reports/revenue`, `GET /api/reports/outstanding`, `GET /api/reports/visits` | Secretary (financial) / ClinicalStaff (visits) |
| Health | `GET /health/live` (liveness), `GET /health/ready` (checks the database) | Anonymous |

Pagination: list endpoints accept `page`/`pageSize` (max 100) and return `items`, `page`, `pageSize`, `totalCount`. Time values are UTC (`...Utc` suffix). Swagger UI ships request-body examples for the main write endpoints.

## Database migrations

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/ClinicManagement.Infrastructure --startup-project src/ClinicManagement.Api --output-dir Persistence/Migrations
dotnet ef migrations script --project src/ClinicManagement.Infrastructure --startup-project src/ClinicManagement.Api --output migration.sql
```

`dotnet ef migrations list/remove` require a reachable database. In Development the API applies pending migrations on startup; Compose passes `--migrate` to the container command.

## Tests

```powershell
dotnet test ClinicManagement.slnx                    # everything (unit + integration)
dotnet test tests/ClinicManagement.UnitTests         # fast, no Docker
dotnet test tests/ClinicManagement.IntegrationTests  # needs Docker
```

Integration tests use Testcontainers (`postgres:17-alpine`) and report as **skipped** when Docker is unavailable.

Frontend tests (Vitest 5 + Testing Library in jsdom) cover the main flows: sign-in, patient creation, appointment booking and invoice payment/voiding.

```powershell
npm.cmd run test        # one-shot  (in web/)
npm.cmd run test:watch  # watch mode
```

## Authentication and security

- **JWT bearer** access tokens (15 minutes) with `token_use` of `full` or `mfa_pending`; refresh tokens (7 days) rotate on every use and reuse revokes all sessions for the account.
- Refresh tokens are bound to the Identity security stamp, so a password change invalidates outstanding sessions.
- **TOTP MFA** (RFC 6238, HMAC-SHA1, ±1 time step). Secrets are stored AES-GCM encrypted with HKDF-derived per-purpose subkeys.
- Global fallback policy requires an authenticated **full** token; named policies `AuthenticatedUser`, `DoctorOnly`, `SecretaryOnly`, `PatientOnly`, `ClinicalStaff` protect every action explicitly.
- **Field-level encryption at rest**: clinical columns marked `[EncryptedColumn]` (visit notes/diagnosis, prescription text, allergy notes) are AES-GCM encrypted; the master key comes from configuration, never the database.
- **Audit trail**: every access or change to patient-linked data writes a hash-chained `AuditLogEntries` row (action, entity, patient, changed fields, actor).
- Billing invoices carry immutable `Payments` rows; invoice state transitions (draft → issued → partially paid/paid → void) are enforced in the domain.
- Passwords: 12+ characters, five failed attempts trigger a five-minute lockout.
- Security headers, CORS with explicit origins, rate limiting on the auth endpoints, ProblemDetails errors with stable `code` values.
- Serilog request logging records method, path, status and a correlation id only — never bodies, headers, user identifiers or IP addresses.

## Configuration reference

See `.env.example` for all environment keys (`Database__*`, `Jwt__*`, `Clinic__Encryption__*`, `Cors__*`, `RateLimiting__*`, `Serilog__*`, `Seed__*Password`, plus the frontend `WEB_API_URL` / `WEB_PORT`). The production overlay additionally reads `DOMAIN` and `ACME_EMAIL`. Frontend-only build variables (`web/.env`): `VITE_API_URL`, `VITE_CLINIC_NAME`, `VITE_PRIMARY_COLOR`.

Helper scripts: `scripts/generate-secrets.ps1`, `scripts/backup-db.ps1`, `scripts/restore-db.ps1`.

## Phases completed

- **Phase 1:** solution structure, domain model, database schema, identity/roles, TOTP MFA, health checks, Docker/Compose.
- **Phase 2:** audit trail with hash chaining, field-level AES-GCM encryption, paging/validation infrastructure, ProblemDetails.
- **Phase 3:** demo account seeding, named role policies on every endpoint, authorization hardening, `.env.example`.
- **Phase 4:** patients CRUD, search, medical history, own-record endpoints for patients.
- **Phase 5:** appointments with doctor schedules, availability search, double-booking protection, lifecycle transitions (confirm/complete/no-show/cancel).
- **Phase 6:** visits and prescriptions (encrypted clinical text, allergy checks, patient own-copies).
- **Phase 7:** billing (invoices, payments, void rules) and reports (revenue, outstanding balances, visits by doctor).
- **Phase 8:** Swagger JWT authorize button + request examples, README, final verification.
- **Frontend:** React + TypeScript SPA in `web/` (typed OpenAPI client, in-memory JWT with silent refresh, login + TOTP MFA, EN/AR RTL, patients module, patient portal, appointments + doctor schedules, visits + prescriptions, invoices + payments + reports), frontend flow tests (Vitest + Testing Library) and an nginx Docker image with runtime API-URL injection.
- **Patient self-service (Phase 9):** patients can browse doctors, view live availability and book/cancel their own appointments (`AuthenticatedUser` on doctor reads, `PatientOnly` on `/api/appointments/me`).
- **Redesign (Phase 10):** premium themed UI — ambient background, glass panels, skeleton loaders, split login, per-role dashboards with Recharts, WCAG-AA contrast, responsive layout, persisted light/system/dark mode, print-friendly invoices/prescriptions and `VITE_CLINIC_NAME` / `VITE_PRIMARY_COLOR` white-labelling.
- **Production hardening (Phase 11):** rich idempotent demo data, container health checks, `docker-compose.prod.yml` behind Caddy (TLS + HSTS + restricted CORS, no seeding, enforced secrets), forwarded-header support, secret generation and `pg_dump` backup/restore scripts.
