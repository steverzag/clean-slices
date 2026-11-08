# Outbox

An integration event is stored as a row in `OutboxMessages` **in the same save as the data**, and a background processor delivers it. If the data is saved, the event will be delivered; if the save fails, there is no event. How to publish and handle events is in [integration-events.md](integration-events.md).

## How a row is delivered

1. `IIntegrationEventPublisher.Publish` adds the row to the current save, with the request's trace.
2. The processor (`Outbox/OutboxProcessor.cs`, run by `OutboxWorker` in the API) claims a batch of pending rows, oldest first. A claimed row is reserved until `LeaseDuration` passes, so several API instances never handle the same row at the same time.
<!--#if (message_broker)-->
3. Each row is sent to the broker. Once the broker confirms it has the message, the row is marked processed; from there the broker delivers it to the handlers and retries them (see [messaging.md](messaging.md)).
4. If sending fails (the broker can't be reached), the row is tried again after `RetryDelay`, doubling each time (5 s, 10 s, 20 s, 40 s). After `MaxAttempts` it's marked **failed** and left for you to inspect. A row whose event name no class has any more fails at once.
<!--#else-->
3. Each row is handed to its event's handlers. If they all succeed, the row is marked processed.
4. If one fails, the row is tried again after `RetryDelay`, doubling each time (5 s, 10 s, 20 s, 40 s). After `MaxAttempts` it's marked **failed** and left for you to inspect. A row whose event name no class has any more fails at once.
<!--#endif-->
5. Processed rows are deleted after `Retention`.

In the Aspire dashboard, the processing of a row appears in the same trace as the request that published it.

## Settings

All optional, in `appsettings.json`:

```json
"Outbox": {
  "PollInterval": "00:00:02",
  "BatchSize": 20,
  "MaxAttempts": 5,
  "RetryDelay": "00:00:05",
  "LeaseDuration": "00:02:00",
  "Retention": "7.00:00:00"
}
```

`LeaseDuration` should be longer than your slowest handler: a row whose lease runs out while it's being handled can be handled twice.

## The table

<!--#if (postgresql)-->
The table, `outbox_messages`, is created by your migrations, like every other table:
<!--#else-->
`OutboxMessages` is created by your migrations, like every other table:
<!--#endif-->

```powershell
dotnet ef migrations add AddOutbox --project src/CleanSlices.Infrastructure
```

Until it exists, every save that publishes an event fails (and with it the request that made it), and the processor logs an error every poll. Requests that publish nothing work as before.

Failed rows stay in the table, with the last error:

<!--#if (postgresql)-->
```sql
SELECT id, name, attempts, last_error, created_at FROM outbox_messages WHERE failed_at IS NOT NULL ORDER BY created_at;
```
<!--#else-->
```sql
SELECT Id, Name, Attempts, LastError, CreatedAt FROM OutboxMessages WHERE FailedAt IS NOT NULL ORDER BY CreatedAt;
```
<!--#endif-->

To retry one after fixing the cause, clear `FailedAt` and `NextAttemptAt` and set `Attempts` to 0.

## Trade-offs

- **Delivery is asynchronous and at least once.** Handlers run a few seconds after the request, and can run more than once for the same event. Write them for that, or add the inbox, which runs each handler once per event.
- **No ordering guarantee.** Rows are claimed oldest first, but a row that's retried is handled after newer ones.
- **Polling.** The processor checks for rows every `PollInterval`, so a quiet app still queries the table. Raise the interval if that matters more than latency.
