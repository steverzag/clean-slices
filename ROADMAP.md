# Roadmap: from template to project configurator

This document describes where CleanSlices is heading and the design rules that should get it there. It's for people working on the template and the CLI; generated solutions don't get it.

The details of every option live in [docs/roadmap/](docs/roadmap/README.md).

## The goal

Today the CLI creates a solution, adds features to it, and merges optional patterns into it. There is one pattern (`result`).

The goal is a **project configurator**: you describe the setup you want, choosing from several *concepts*, each with several *options*, and the CLI generates a solution where all the chosen options work together. Both lists, concepts and the options within them, should be able to grow without limit.

The CLI is for anyone. The template's own style, pragmatic and without ceremony, is the **default**, not the only choice: widely used approaches such as repositories or CQRS are options.

| Concept | An option belongs here if it… | Choose | Options |
|---|---|---|---|
| **[Patterns](docs/roadmap/patterns.md)** | changes *how code is written* | any number | Result&lt;T&gt;, outbox, inbox, repository / unit of work, CQRS, idempotency keys, request validation |
| **[Infrastructure](docs/roadmap/infrastructure.md)** | picks *which product* sits behind a seam | at most one per group | Database, queue, cache, authentication, realtime |
| **[Strategies](docs/roadmap/strategies.md)** | adds *behavior to entities* | any number | Soft delete, audit |
| **[Extras](docs/roadmap/extras.md)** | adds files around the code, without changing how it's written | any number | Logging (Serilog), API reference (Scalar) |

The same choices must also hold *after* creation: `add feature` generates code that fits the configuration, and adding an option to an existing solution merges it into the user's code.

Similar tools exist in other ecosystems: JHipster (Java) is the closest, Spring Initializr and ABP are smaller versions. They show it's feasible, and that most of the long-term cost is keeping combinations working, not writing options.

## The core problem: cohesion

Options interact. If the user picks RabbitMQ, integration events must go through RabbitMQ whether or not the outbox is selected. If they pick PostgreSQL, everything that touches the database must work on PostgreSQL.

The number of possible interactions grows much faster than the number of options: 10 options make 45 pairs. If every pair needs its own code, the project stops scaling long before the list gets interesting.

## Design rules

These rules are what make the goal achievable. A new option that breaks one of them should be redesigned before it's merged.

1. **Options meet only through seams.** A seam is an interface or registration point. One option *provides* an implementation; another *uses* the interface. Neither knows the other exists.

   Example: integration events are handed to an `IMessageDispatcher`. The outbox never mentions RabbitMQ; the RabbitMQ option registers an `IMessageDispatcher` that sends to the broker. "Outbox + RabbitMQ" needs no code of its own.

2. **Every seam works with nothing selected.** The base template, or the unit that introduces the seam, ships a default implementation (an in-process dispatcher, an in-memory cache, an anonymous current user). A solution with no options builds and runs, and every option can be added on its own.

3. **Code that is specific to a combination lives in a glue unit.** A glue unit is a small piece applied only when both options are selected. Design it away first. **The number of glue units is the health metric of the design:** if it grows with every option, the seams are in the wrong place. Known glue units today: realtime × Redis (SignalR backplane) and realtime × ASP.NET Core Identity (token in the query string).

4. **Add files rather than edit shared ones.** Every edited line is a possible conflict with the user's code or with another option. An option should ideally be new files plus a few small conditionals at known places. This is also why the CLI never converts the user's existing entities (see [strategies](docs/roadmap/strategies.md)).

5. **Provider-specific artifacts are generated on the user's side.** Migrations differ per database provider. Generated solutions don't ship migrations (`template.json` excludes them); users run `dotnet ef migrations add`.

6. **The feature template honors the configuration.** `add feature` must generate code that fits the chosen options.

7. **Options don't change what clients see unless that is their purpose.** The same failure gives the same HTTP response with or without `result`; a soft-deleted entity behaves exactly like a deleted one; request validation and domain validation return the same shape.

8. **Code stays provider-neutral.** Entity type configurations use no provider-specific column types, no code depends on provider error numbers, and times are always UTC.

9. **No hidden licenses.** When an option depends on a commercial package or product (MediatR, Seq), the CLI prints a notice when it's chosen and the setting for the license key is generated empty.

10. **The default solution only contains what it uses itself.** A change to the base template must make the default solution better on its own. A seam that only options use goes into a [hidden unit](docs/roadmap/hidden-units.md), added when an option requires it. Testability alone isn't a reason to change the default solution.

## How options are applied

All options use one mechanism: **template conditionals**.

- **Whole files** are included only when an option is selected (`Result.cs`, `OutboxProcessor.cs`), or only when it isn't (`Exceptions.cs`).
- **Inline `//#if (option)` blocks** cover the lines that differ inside shared files: a handler body, a registration call, an AppHost resource. The comment form keeps the template compiling in its default state.

Rule 4 keeps the inline blocks few. Pattern branches (`pattern/result`) are retired once `result` moves to conditionals.

Adding an option *to an existing solution* doesn't depend on the mechanism: `add <option>` renders the solution before and after the option and merges the difference into the user's code with `git merge-tree`, as `add pattern` does today.

**The sample features (Tasks/Users) only come with the default setup.** `--samples` together with an invasive pattern or an authentication option is rejected. The feature template is the code that must support every option.

## The catalog

Each option declares what it is and how it relates to others, in a catalog the CLI reads:

```jsonc
{
  "id": "cqrs",
  "concept": "pattern",
  "title": "CQRS",
  "choices": {
    "dispatcher": { "values": ["handlers", "mediator", "mediatr"], "default": "handlers" }
  },
  "requires": [],
  "provides": [],
  "conflicts": []
}
```

```jsonc
{
  "id": "rabbitmq",
  "concept": "infrastructure",
  "group": "queue",                       // at most one option per group
  "title": "RabbitMQ",
  "requires": ["integration-events"],
  "provides": ["message-dispatcher", "at-least-once-delivery"]
}
```

```jsonc
{
  "id": "integration-events",
  "hidden": true,                         // never picked by the user, added by the resolver
  "provides": ["integration-events"]
}
```

- **Groups and sub-options are both "pick one"**, with different meanings. A *sub-option* (`choices`) only matters when its parent is selected: the CQRS dispatcher. A *group* is a choice other options depend on: the database provider.
- **Hidden units** are pieces several options need but no user picks directly, such as `integration-events`, required by the outbox and the queue options. The others are `current-user`, `model-rules` and `cache`; all of them are in [hidden-units.md](docs/roadmap/hidden-units.md).

The CLI resolves the selection like a package manager:

1. Collect the selected options and their choices (asking for, or defaulting, the choices not given).
2. For each `requires` capability that no selected option provides, add the hidden unit or default that provides it, or ask.
3. Reject conflicts and two options in the same group before generating anything, with a message that says why.
4. Add the glue units whose conditions are met.
5. Print the license notices.
6. Apply everything in a deterministic order.

`.cleanslices.json` records the resolved configuration, choices included (`{ "id": "cqrs", "dispatcher": "mediator" }`), not just the options the user typed, so later commands see the same picture.

## Testing

**Generated solutions have tests.** The base template ships test projects, and `add feature` generates tests for each new slice (see [base-template.md](docs/roadmap/base-template.md)). So every generated solution runs tests for its own combination of options.

**The template repository tests combinations in CI.** It must generate, build and test:

- the base template with no options,
- each option alone, and each value of each sub-option,
- every **pair** of options (most interaction bugs involve two options),
- one solution with everything compatible selected.

For each, it should: generate a new solution, add a feature with `add feature`, add each option later on a solution that has a feature, build, and run the tests.

Set this up before the catalog grows past about five options. Without it, combinations break silently.

## Versioning

- The template commit a solution came from is recorded in `.cleanslices.json`; options are applied against that commit. Keep it that way.
- The catalog lives in the template repository, so it's versioned with the template.
- When the catalog format changes, the CLI must still read older solutions' manifests, or say clearly that it can't.

## Phases

Each phase ends with something usable. The order is a proposal.

| Phase | What | Proves |
|---|---|---|
| 0, done | Patterns as git branches; `result` | Composing and merging options into existing code |
| 1 | **Base template changes** ([backlog](docs/roadmap/base-template.md)): test projects (done), ordered save pipeline and domain events before the save (done), pagination, lower-case emails, Aspire-managed database | A better default solution, and the save pipeline options plug into |
| 2 | **Conditionals**: move `result` from its branch to template conditionals; catalog and resolver in the CLI | One mechanism; requires / provides / conflicts / groups / choices |
| 3 | **CI matrix** | Combinations stay working |
| 4 | **Outbox, inbox, integration events** | Seams with defaults and hidden units |
| 5 | **PostgreSQL** | Provider-neutral code |
| 6 | **Repository, CQRS, request validation, idempotency keys** | Several invasive patterns in one feature template |
| 7 | **Queue, cache, authentication, realtime** | Zero-glue combinations, and the first glue units |
| 8 | **Strategies**: soft delete, audit | Options on entities and the save pipeline order |
| 9 | **Extras**; publish the CLI on NuGet | Ready for other people |

## Parked and open

Parked: discussed or proposed, deliberately not now.

- **`TimeProvider`** as an option instead of part of the base template: built, then reverted (see [base-template.md](docs/roadmap/base-template.md#timeprovider)). Which concept it belongs to is still to decide.
- **Optimistic concurrency**, with a full proposed design in [strategies.md](docs/roadmap/strategies.md#parked-optimistic-concurrency).
- **CI workflow** extra (GitHub Actions for generated solutions).
- **Multi-tenancy** strategy. EF Core named query filters make it compatible with soft delete later.
- **Strongly typed IDs** pattern.
- **SSE** as a second realtime option; **Azure SignalR Service** for scale-out.
- **Database-backed idempotency store**, as a `store` sub-option, for teams that need the transactional guarantee.
- **Public-only integration events** refinements, and a **separate read store** for CQRS.
- **Containers**, **dev container** (extras); **API versioning**, **rate limiting** (pattern candidates).

Open questions:

- **Removing an option.** Today options can only be added. Is removal a goal?
- **Upgrades.** Can a solution move to a newer template commit, and what happens to its options?
- **Interactive UX.** With many options, the prompts need grouping by concept, sensible defaults, and follow-up questions for choices.

To verify before the docs for users are written: MediatR's and MassTransit's current license terms, and whether `HybridCache` skips the in-memory `IDistributedCache` as its second level.
