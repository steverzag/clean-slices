# Configurator catalog

Every option planned for the CLI, with the decisions taken for it. The vision, design rules and phases are in [ROADMAP.md](../../ROADMAP.md).

Each option page uses the same headings: **What**, **Design**, **Sub-options**, **Feature template**, **Samples**, **Interactions** and **Trade-offs to document**.

**Feature template** is the minimum code `add feature` generates for the option. **Samples** is how the sample features show the option in use, as a reference users can copy (see [ROADMAP.md](../../ROADMAP.md#how-options-are-applied)). Samples entries for options not built yet are proposals, settled when the option is built.

## Base template

Changes to `main` that improve the default solution on their own: [base-template.md](base-template.md). Seams only options use are [hidden units](hidden-units.md).

## Options

| Concept | Option | Group / sub-options | Status |
|---|---|---|---|
| Pattern | [Result&lt;T&gt;](patterns.md#resultt) | – | **done** (conditionals) |
| Pattern | [Outbox](patterns.md#outbox) | – | **done** |
| Pattern | [Inbox](patterns.md#inbox) | – | **done** (recommended with the outbox) |
| Pattern | [Repository / unit of work](patterns.md#repository--unit-of-work) | – | **done** |
| Pattern | [CQRS](patterns.md#cqrs) | `dispatcher`: handlers (default, Mediator vocabulary), mediator | **done**; MediatR dropped |
| Pattern | [Idempotency keys](patterns.md#idempotency-keys) | – | **done** (in-memory store) |
| Pattern | [Request validation](patterns.md#request-validation) | – | **done** (FluentValidation) |
| Hidden unit | [Current user](hidden-units.md#current-user) | – | **done** (anonymous) |
| Hidden unit | [Model rules](hidden-units.md#model-rules) | – | **done** (phase 5, for PostgreSQL) |
| Hidden unit | [Cache](hidden-units.md#cache) | – | **done** (`IDistributedCache`: in memory, or Redis) |
| Hidden unit | [HybridCache](hidden-units.md#hybridcache) | – | **done** (phase 7: Microsoft's, or FusionCache) |
| Hidden unit | [After commit](hidden-units.md#after-commit) | – | **done** (phase 7, first used by cache invalidation) |
| Hidden unit | [Integration events](hidden-units.md#integration-events) | – | **done** (without the after-commit publisher, see its page) |
| Infrastructure | [SQL Server](infrastructure.md#database), [PostgreSQL](infrastructure.md#database) | group `database` (required) | **done**; SQL Server is the default |
| Infrastructure | [RabbitMQ](infrastructure.md#queue) | group `queue` | **done** (phase 7b) |
| Infrastructure | [Azure Service Bus](infrastructure.md#queue) | group `queue` | designed (next in 7b) |
| Hidden unit | [Message broker](hidden-units.md#message-broker) | – | **done** (phase 7b: shared by the queue options) |
| Infrastructure | [Redis](infrastructure.md#cache) | group `cache` | **done** |
| Infrastructure | [FusionCache](infrastructure.md#cache) | group `cache-library` | **done** (phase 7) |
| Glue | [FusionCache × Redis](infrastructure.md#cache) | – | **done**: Redis as second level and backplane (the first glue unit) |
| Infrastructure | [Keycloak, Entra ID, ASP.NET Core Identity](infrastructure.md#authentication) | group `auth` | designed |
| Infrastructure | [SignalR](infrastructure.md#realtime) | group `realtime` | designed |
| Strategy | [Soft delete](strategies.md#soft-delete) | – | designed |
| Strategy | [Audit](strategies.md#audit) | – | designed |
| Strategy | [Optimistic concurrency](strategies.md#parked-optimistic-concurrency) | – | **parked** |
| Extra | [Logging (Serilog)](extras.md#logging-serilog) | `seq`: off (default), on | designed |
| Extra | [API reference (Scalar)](extras.md#api-reference-scalar) | – | designed |

Parked items and open questions are listed at the end of [ROADMAP.md](../../ROADMAP.md#parked-and-open).
