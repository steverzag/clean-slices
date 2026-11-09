# Logging

**Serilog** writes the logs. Code keeps using `ILogger<T>`; nothing in features changes. The setup is in `Api/Configuration/Logging/LoggingRegistration.cs`:

| Where | What |
|---|---|
| Console | readable text in Development, JSON (one object per line) elsewhere |
| Aspire dashboard | over OTLP, when the AppHost runs the API: the sink reads the `OTEL_*` variables the AppHost sets, so the logs sit next to the traces |
<!--#if (seq)-->
| Seq | through the `seq` connection string, when the AppHost runs it (below) |
<!--#endif-->

ServiceDefaults doesn't add its own OpenTelemetry logging, so logs aren't sent twice; traces and metrics still go through OpenTelemetry.

## Levels

Information and above, with ASP.NET Core and EF Core lowered to Warning, so a request writes one line instead of a dozen. The defaults are in code; a `"Serilog"` section in `appsettings.json` overrides them:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": { "Microsoft.EntityFrameworkCore.Database.Command": "Information" }
  }
}
```

That one shows every SQL statement EF runs. The section can also add sinks and enrichers (see Serilog's `ReadFrom.Configuration`).

## Requests

`UseRequestLogging` writes one line per request, when it completes:

```
HTTP GET /tasks/0198… responded 200 in 12.3 ms
```

with `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`, the trace and span, and `UserId` (`ICurrentUser.UserId`, empty for anonymous requests). A failed request is logged at Error with its exception.

## Traces

Every log written during a request or a traced background job carries its trace ID, so a log line leads to its trace in the dashboard and back. Background work started from a stored trace (the outbox, a queue consumer) logs under the original request's trace.

<!--#if (seq)-->
## Seq

The AppHost runs Seq in a container, with a data volume so logs survive restarts; its address is on the Seq resource in the dashboard. Search there with Seq's query language, for example `StatusCode >= 500` or `UserId = '…'`.

Seq is free for individuals and for development, and paid for teams in production: check its license before you run it in production. Without the AppHost, set `ConnectionStrings:seq` to a Seq server's URL; with no connection string, nothing is sent to Seq.

<!--#endif-->
## Tests

Serilog also writes to sinks registered in the services (`ReadFrom.Services`). The tests use that to record what the API logs: `RequestLoggingTests` registers a recording sink and checks the request line.
