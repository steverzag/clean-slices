# Configurator catalog

Every option planned for the CLI, with the decisions taken for it. The vision, design rules and phases are in [ROADMAP.md](../../ROADMAP.md).

Each option page uses the same headings: **What**, **Design**, **Sub-options**, **Feature template**, **Interactions** and **Trade-offs to document**.

## Base template

Changes to `main` that the options rely on: [base-template.md](base-template.md).

## Options

| Concept | Option | Group / sub-options | Status |
|---|---|---|---|
| Pattern | [Result&lt;T&gt;](patterns.md#resultt) | – | done (branch); moves to conditionals |
| Pattern | [Outbox](patterns.md#outbox) | – | designed |
| Pattern | [Inbox](patterns.md#inbox) | – | designed |
| Pattern | [Repository / unit of work](patterns.md#repository--unit-of-work) | – | designed |
| Pattern | [CQRS](patterns.md#cqrs) | `dispatcher`: handlers (default), mediator, mediatr | designed |
| Pattern | [Idempotency keys](patterns.md#idempotency-keys) | – | designed |
| Pattern | [Request validation](patterns.md#request-validation) | – | designed (FluentValidation) |
| Hidden unit | [Integration events](patterns.md#integration-events-hidden-unit) | – | designed |
| Infrastructure | [SQL Server](infrastructure.md#database), [PostgreSQL](infrastructure.md#database) | group `database` | SQL Server is the default |
| Infrastructure | [RabbitMQ, Azure Service Bus](infrastructure.md#queue) | group `queue` | designed |
| Infrastructure | [Redis](infrastructure.md#cache) | group `cache` | designed |
| Infrastructure | [Keycloak, Entra ID, ASP.NET Core Identity](infrastructure.md#authentication) | group `auth` | designed |
| Infrastructure | [SignalR](infrastructure.md#realtime) | group `realtime` | designed |
| Strategy | [Soft delete](strategies.md#soft-delete) | – | designed |
| Strategy | [Audit](strategies.md#audit) | – | designed |
| Strategy | [Optimistic concurrency](strategies.md#parked-optimistic-concurrency) | – | **parked** |
| Extra | [Logging (Serilog)](extras.md#logging-serilog) | `seq`: off (default), on | designed |
| Extra | [API reference (Scalar)](extras.md#api-reference-scalar) | – | designed |

Parked items and open questions are listed at the end of [ROADMAP.md](../../ROADMAP.md#parked-and-open).
