# Inbox

Integration events are delivered **at least once**, so a handler can be called again for an event it already handled. The inbox makes each handler run **once per event**. Handlers don't change: it wraps them all (`Inbox/InboxFilter.cs`).

## How it works

- Before a handler runs, the inbox looks for a record of (event, handler) in `ProcessedMessages`. If there is one, the handler is skipped.
- Otherwise it adds the record to the handler's `AppDbContext`, runs the handler, and saves. The record is saved **together with the handler's own changes**: either both are kept, or neither is, and the event is retried.
- Each handler has its own record. When one of an event's handlers fails, the retry skips the ones that succeeded and runs only the one that failed.
- When two API instances handle the same event at the same moment, the second save finds the record already there and the event counts as handled.
- An event is recognized by its ID: the outbox row's ID, or the broker message's ID, which stays the same on every redelivery.

Records older than `Inbox:Retention` (7 days by default) are deleted every hour. Keep it longer than an event can take to be delivered again.

## What it can't do

The guarantee covers what the handler saves through `AppDbContext`. An email sent before a failure is still sent, and is sent again on the retry. So the rules from [integration-events.md](integration-events.md) still apply: save at most once, at the end, and make external calls last and safe to repeat.

## The table

<!--#if (postgresql)-->
The table, `processed_messages`, is created by your migrations:
<!--#else-->
`ProcessedMessages` is created by your migrations:
<!--#endif-->

```powershell
dotnet ef migrations add AddInbox --project src/CleanSlices.Infrastructure
```
