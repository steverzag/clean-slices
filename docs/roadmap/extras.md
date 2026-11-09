# Extras

An extra adds files around the code without changing how it's written, replacing a seam, or adding entity behavior. Extras are the cheapest options to build and maintain. See the [catalog index](README.md).

Tests aren't an extra: they're part of every generated solution (see [base-template.md](base-template.md#test-projects)). A CI workflow extra is parked.

## Logging (Serilog)

**What.** Serilog replaces the logging pipeline. Code keeps using `ILogger<T>`.

**Design.**

- Serilog writes to OTLP with `Serilog.Sinks.OpenTelemetry`, using the endpoint Aspire provides, so logs still reach the Aspire dashboard, with Serilog's properties.
- `ServiceDefaults` skips its own OpenTelemetry logging when the extra is selected, so logs aren't sent twice (a conditional).
- Configuration comes from the `"Serilog"` section of `appsettings.json`: levels, overrides that quiet framework noise, sinks.
- Console output: readable text in Development, JSON elsewhere.
- `UseSerilogRequestLogging`: one line per request with method, path, status, duration, `traceId` and `UserId` from `ICurrentUser`.

**Built (phase 9).** Documented for users in `docs/logging.md`.

**As built.**

- `Api/Configuration/Logging/LoggingRegistration.cs`: `AddSerilogLogging()` and `UseRequestLogging()` (after the status code pages). Requires `current-user` for the request line's `UserId`.
- **Settings in code, not in `appsettings.json`** (changed from the design): the defaults (Information; ASP.NET Core and EF Core at Warning; console text in Development, `RenderedCompactJsonFormatter` elsewhere) are set in code, then `ReadFrom.Configuration` lets a `"Serilog"` section override them. JSON can't have conditionals, and `appsettings.json` already has a PostgreSQL variant.
- **OTLP**: `WriteTo.OpenTelemetry()` when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The sink reads `OTEL_EXPORTER_OTLP_ENDPOINT`, `_PROTOCOL`, `_HEADERS` (the dashboard's API key), `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` itself (checked in the assembly), so the AppHost needs no change. ServiceDefaults skips `builder.Logging.AddOpenTelemetry` under `#if (!serilog)`.
- **Choice `store`** (renamed from `seq: off/on`: choice values become template symbols, and `on`/`off` make poor ones): `dashboard` (default) or `seq`. With `seq`: `AddSeq("seq").WithDataVolume()` in the AppHost (`Aspire.Hosting.Seq`, which accepts Seq's license), `Serilog.Sinks.Seq` when the `seq` connection string is set, and the license notice on the choice value.
- **Tests**: Serilog writes to sinks registered in the services (`ReadFrom.Services`), so `RequestLoggingTests` adds a recording sink to a test host and checks the request line (path, method, status, elapsed, trace, `UserId`; with an authentication option and the samples, the logged-in user's ID). Sabotage-proved: without the enrichment both fail.
- **The request logger is the host's** (found by the tests): `UseSerilogRequestLogging` writes through the static `Log.Logger` unless given one, and every host `AddSerilog` builds replaces it, so with several test hosts in parallel a request line could land in another host's pipeline (the logged-in test failed only in full runs, with Identity and Entra). `options.Logger` is the app's `Serilog.ILogger`; four full runs green after. Reasoned, not reproduced step by step.
- Verified with the AppHost of a generated solution (Seq, Scalar, Keycloak, samples): Seq received the request lines with path, status, trace and the Keycloak user's ID; `/scalar` served; the OpenAPI document named the client. The dashboard's log view wasn't checked (it needs a browser).

**Sub-options.** `seq`, off by default: adds Seq as an Aspire resource (`AddSeq("seq")`) and a Seq sink, for stored, searchable logs. Seq is free for individuals and development, paid for teams in production, so the CLI prints a notice (rule 9).

**Feature template.** No change.

**Samples.** No change.

**Interactions.** Background work (outbox, queue consumers) keeps the original `traceId` through the stored `traceparent`.

## API reference (Scalar)

**What.** An interactive page for browsing and trying the API, on top of the OpenAPI document the API already serves.

**Design.**

- Scalar (`Scalar.AspNetCore`, MIT) only. Swagger UI may come later as a sub-option if anyone asks.
- `MapScalarApiReference()` next to `MapOpenApi()`, only in Development, at `/scalar`.
- The Aspire dashboard gets a link to it on the API resource (a conditional in `AppHost.cs`).
- Logins work with no glue: Scalar reads the security scheme each authentication option declares in the OpenAPI document.

**Built (phase 9).** Documented for users in `docs/api-reference.md`.

**As built.**

- `Scalar.AspNetCore` 2.19 (MIT); `MapScalarApiReference()` in the `IsDevelopment()` block of `Program.cs`, `.AllowAnonymous()` under an authentication option. The AppHost adds *API reference* to the API resource (`WithUrlForEndpoint("https", …)`).
- **The login, as built:** an OAuth2 flow also needs a client ID, which OpenAPI has no field for. Keycloak and Entra ID add Scalar's documented extensions to their own authorization code flow (`x-scalar-client-id`: the realm's client, the Entra client ID; `x-usePkce: SHA-256`), unconditionally: other tools ignore them, and the Scalar extra stays glue-free. Keycloak's realm already allows the API's addresses as redirect URIs; Entra ID needs a single-page application redirect on its registration (documented); Identity's bearer scheme takes a pasted token.
- Tests: `/scalar` answers without a login (`ApiReferenceTests`); with Keycloak or Entra ID, the OpenAPI document carries the client ID and PKCE (`AuthenticationTests`).

**Feature template.** No change; endpoint tags and names already group the page.

**Samples.** No change.

## Publishing (set aside)

Discussed in phase 9, then set aside by decision: the extras first. What it needs, as proposed:

- **A license.** The repository has none, so nobody may use the code yet. MIT matches the packages the template chose; the generated solutions are the user's code, so they wouldn't get the file (excluded in `template.json`).
- **Package metadata** for `CleanSlices.Cli` (free on nuget.org, like `CleanSlices`): license expression, project and repository URLs, the README shown on nuget.org, tags.
- **A landing README** at `.github/README.md`, which GitHub shows instead of the root one (the template's README, with its `<!--#if-->` lines). `.github/` is already excluded from generated solutions.
- **The first version:** `1.0.0-preview.1`, since the catalog and manifest formats haven't been used by others yet.
- **Publishing:** a workflow that packs and pushes on a `v*` tag, with a `NUGET_API_KEY` secret; or the first push by hand.
- **Check MassTransit's current license terms** before the docs for users are final (the queue options cite them as one reason for the official clients).

