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

**Feature template.** No change; endpoint tags and names already group the page.

**Samples.** No change.
