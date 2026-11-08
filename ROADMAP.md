# Roadmap: from template to project configurator

This document describes where CleanSlices is heading and the design rules that should get it there. It's for people working on the template and the CLI; generated solutions don't get it.

The details of every option live in [docs/roadmap/](docs/roadmap/README.md).

## The goal

Today the CLI creates a solution with options, adds features to it, and merges options into it later. Options are described in a catalog and applied as template conditionals; there is one so far (`result`).

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

9. **No hidden licenses.** When an option depends on a commercial package or product (Seq, for example), the CLI prints a notice when it's chosen and the setting for the license key is generated empty.

10. **The default solution only contains what it uses itself.** A change to the base template must make the default solution better on its own. A seam that only options use goes into a [hidden unit](docs/roadmap/hidden-units.md), added when an option requires it. Testability alone isn't a reason to change the default solution.

## How options are applied

All options use one mechanism: **template conditionals**.

- **Whole files** are included only when an option is selected (`Result.cs`, `OutboxProcessor.cs`), or only when it isn't (`Exceptions.cs`).
- **Inline `#if (option)` blocks** cover what differs inside shared files: an entity method, a handler, a registration call, an AppHost resource. They're real preprocessor directives, read by both the template engine and the compiler, so the template repository builds and tests every variant (`dotnet test -p:TemplateOptions=result`). A member that differs in more than one place is written whole on each side of the `#if`, so each variant reads top to bottom.
- **Documentation** that an option replaces is a whole file per variant; documentation an option adds is its own file.

Rule 4 keeps the inline blocks few. How to write them is in [CONTRIBUTING.md](CONTRIBUTING.md#how-options-work).

Adding an option *to an existing solution* doesn't depend on the mechanism: `add <option>` renders the solution before and after the option, from the template commit it came from, and merges the difference into the user's code with `git merge-tree`.

**The sample features (Tasks/Users) are the reference implementation of every option.** The feature template generates the *minimum* each option needs; the samples show *how to use* it (an integration event published from a domain event handler, a cached query invalidated by tags, a notification when a task is assigned), so users can copy it into their own features. Options support the samples by default; an option that can't be shown meaningfully declares `"samples": false` and says why in its documentation, and the CLI then rejects it together with `--samples`. Each option page in [docs/roadmap/](docs/roadmap/README.md) has a **Samples** heading next to **Feature template**.

## The catalog

Each option declares what it is and how it relates to others in `catalog.json`, at the repository root. `catalog.schema.json` validates the format and gives editors completion. The catalog says *what* exists and how it relates; the `template.json` files say *how* it renders, and consistency tests keep the two in step.

```jsonc
{
  "$schema": "./catalog.schema.json",
  "catalogVersion": 1,
  "concepts": {                               // data too: concepts can grow
    "pattern":        { "title": "Patterns",       "select": "many" },
    "infrastructure": { "title": "Infrastructure", "select": "onePerGroup" }
  },
  "groups": {
    "database": { "title": "Database", "required": true, "default": "sqlserver" },
    "queue":    { "title": "Queue",    "required": false }   // empty = the seam's default
  },
  "units": [
    {
      "id": "cqrs", "kind": "option", "concept": "pattern", "title": "CQRS", "description": "…",
      "choices": {
        "dispatcher": {
          "default": "handlers",
          "values": {
            "handlers": { "title": "Own handlers" },
            "mediator": { "title": "Mediator (source-generated, MIT)" }
          }
        }
      }
    },
    {
      "id": "rabbitmq", "kind": "option", "concept": "infrastructure", "group": "queue", "title": "RabbitMQ", "description": "…",
      "requires": ["integration-events"],
      "provides": ["message-dispatcher", "at-least-once-delivery"]
    },
    { "id": "integration-events", "kind": "hidden" },          // never picked by the user, added by the resolver
    { "id": "signalr-redis", "kind": "glue", "when": ["signalr", "redis"] }
  ]
}
```

- **Units** are `option` (picked by the user), `hidden` (added when a selected unit requires it) or `glue` (added when every unit in `when` is selected). Every unit provides its own id; `requires` and `conflicts` name ids or capabilities.
- **Groups and choices are both "pick one"**, with different meanings. A *choice* (sub-option) only matters when its option is selected: the CQRS dispatcher. A *group* is a choice other options depend on: the database provider. Licenses sit on an option or on a choice value.
- **Hidden units** are pieces several options need but no user picks directly, such as `integration-events`, required by the outbox and the queue options. The others are `current-user`, `model-rules` and `cache`; all of them are in [hidden-units.md](docs/roadmap/hidden-units.md).
- **The order in the file** is the order of prompts and of `cleanslices list`.

The CLI resolves the selection like a package manager, before generating anything:

1. Collect the selected options and their choices (asking for, or defaulting, the choices not given).
2. Apply the groups: two options from one group are rejected, another option of a group replaces the solution's current one, and required groups get their default.
3. For each `requires` that no selected unit provides:
   - a **hidden unit** provides it: add it (it may require more),
   - only **options** provide it (the inbox needs the outbox or a queue): never pick one for the user; ask which to add (or drop the option), or, without prompts, fail and list them,
   - **nothing** provides it: a catalog error, caught by its tests before a release.
4. Reject conflicts and options without sample support when the samples are selected, with a message that says why.
5. Add the glue units whose conditions are met.
6. Print the license notices, and confirm.
7. Render once, with every unit as a template parameter, in catalog order.

Interactive lists show what an option still needs (*"Inbox (needs Outbox, RabbitMQ or Azure Service Bus)"*) instead of hiding it.

Two more flags on a unit:

- **`recommended`**: once the selection meets the option's requirements, the CLI offers it (asked interactively, yes by default; a tip without prompts). It's never added silently, and declining isn't recorded. The inbox is recommended with the outbox.
- **`addsTables`**: after `add`, the CLI prints the migration to create (`dotnet ef migrations add AddOutbox …`), since migrations are generated on the user's side (rule 5).

`.cleanslices.json` records the resolved configuration, choices included (`{ "id": "cqrs", "dispatcher": "mediator" }`) and the units added automatically with what they were added for (`"addedBy": ["outbox"]`), not just the options the user typed, so later commands see the same picture.

## Testing

**Generated solutions have tests.** The base template ships test projects, and `add feature` generates tests for each new slice (see [base-template.md](docs/roadmap/base-template.md)). So every generated solution runs tests for its own combination of options.

**The template repository builds and tests every variant itself.** `dotnet test --solution CleanSlices.Dev.slnx -p:TemplateOptions=<options>` compiles the template with those options, samples included. The feature template isn't compiled there, so it's checked by generating solutions.

**The template repository tests combinations in CI.** It must generate, build and test:

- the base template with no options, with and without samples,
- each option alone, and each value of each sub-option, with samples and without (without catches code that only compiles because of the samples),
- every **pair** of options, with samples (the samples cover more code than the feature template; most interaction bugs involve two options),
- one solution with everything compatible selected.

For each, it should: generate a new solution, add a feature with `add feature`, add each option later on a solution that has a feature, build, and run the tests.

This runs on GitHub Actions (`.github/workflows/ci.yml`, see [CONTRIBUTING.md](CONTRIBUTING.md#ci)). The combinations are derived from the catalog through the resolver by `cli/CleanSlices.Matrix`, so the matrix grows with the catalog and skips what can't exist. Pull requests run the default and each option alone; pushes to `main`, the nightly run and manual runs add every pair and everything together. Every CI job can be run locally with the same command.

Without it, combinations break silently. GitHub's limit of 256 jobs per matrix is reached at around 20 options; before then, pairs should be limited to options that touch the same files.

## Versioning

- The template commit a solution came from is recorded in `.cleanslices.json`; options are applied against that commit. Keep it that way.
- The catalog lives in the template repository, so it's versioned with the template. A commit without a catalog predates options: the CLI renders it without option parameters and offers no options for it.
- `catalogVersion` and `manifestVersion` number the two formats. A CLI that finds a newer one asks to be updated; newer CLIs must keep reading older ones, or say clearly that they can't (solutions from cleanslices 0.2 with a pattern branch are the one case it can't).

## Phases

Each phase ends with something usable. The order is a proposal.

| Phase | What | Proves |
|---|---|---|
| 0, done | Patterns as git branches; `result` | Composing and merging options into existing code |
| 1, done | **Base template changes** ([backlog](docs/roadmap/base-template.md)): test projects, ordered save pipeline and domain events before the save, pagination, Aspire-managed database | A better default solution, and the save pipeline options plug into |
| 2, done | **Conditionals**: `result` moved from its branch to template conditionals (identical output, checked against the branch); catalog, schema and resolver in the CLI; `new --with`, `add <option>`, `list`; manifest format 2 | One mechanism; requires / provides / conflicts / groups / choices |
| 3, done | **CI matrix**: combinations from the catalog through the resolver; template ref (`--template-ref`, `CLEANSLICES_TEMPLATE_REF`, `HEAD`) so CI tests the commit under test | Combinations stay working |
| 4, done | **Outbox, inbox, integration events**: the first hidden unit, `requires` / `provides` between real options (the inbox gets the outbox), `recommended` and `addsTables`, three sample integration events. The CI matrix found a concurrency bug in the outbox's claim before release | Seams with defaults and hidden units |
| 5, done | **PostgreSQL**: the first infrastructure group (`database`, SQL Server by default), the model-rules hidden unit, snake_case names and a case-insensitive email through model rules | Provider-neutral code |
| 6, done | **Repository, CQRS, request validation, idempotency keys**: three options meeting in every handler, written as flat `#elif` chains (decided by a prototype); the first choice (`cqrs` dispatcher: own handlers or Mediator); the `current-user` and `cache` hidden units; listed matrix combinations (`result` + repository + CQRS) | Several invasive patterns in one feature template |
| 7 | **Queue, cache, authentication, realtime**. 7a done: Redis and FusionCache (two cache groups), the first glue unit (`fusioncache-redis`), the `hybrid-cache` and `after-commit` hidden units. 7b done: RabbitMQ and Azure Service Bus (the `message-broker` hidden unit, the inbox without the outbox). 7c: Keycloak and Entra ID built (the `authentication` hidden unit, the `login` capability, provider roles as role claims); Identity next | Zero-glue combinations, and the first glue units |
| 8 | **Strategies**: soft delete, audit | Options on entities and the save pipeline order |
| 9 | **Extras**; publish the CLI on NuGet | Ready for other people |

## Parked and open

Parked: discussed or proposed, deliberately not now.

- **`TimeProvider`** as an option instead of part of the base template: built, then reverted (see [base-template.md](docs/roadmap/base-template.md#timeprovider)). Which concept it belongs to is still to decide.
- **Optimistic concurrency**, with a full proposed design in [strategies.md](docs/roadmap/strategies.md#parked-optimistic-concurrency).
- **CI workflow** extra (GitHub Actions for generated solutions).
- **Multi-tenancy** strategy. EF Core named query filters make it compatible with soft delete later.
- **Strongly typed IDs** pattern.
- **MediatR** as a CQRS dispatcher: dropped in phase 6 (commercial since version 13, and a vocabulary the other dispatchers can't share). The docs can show how to switch.
- **Rename the domain-event contracts** (base template) to `IDomainEventHandler<T>` and `IDomainEventPublisher`. Today they are `INotification`, `INotificationHandler<T>` and `IPublisher` in `Core/Messages`, the same names as the Mediator package's: no clash in generated code, but an Api file that imports both namespaces gets `CS0104` (found in phase 6, documented in `docs/cqrs.md`). The new names also say what they carry. Rejected alternative: using Mediator's contracts for domain events with the `mediator` choice (it would give Core a package dependency and tie the base's domain-event dispatch to the CQRS choice).
- **SSE** as a second realtime option; **Azure SignalR Service** for scale-out.
- **Database-backed idempotency store**, as a `store` sub-option, for teams that need the transactional guarantee.
- **Public-only integration events** refinements, and a **separate read store** for CQRS.
- **Permissions** pattern (proposed in phase 7c), requiring the `login` capability that every authentication option provides. Phase 7c prepares it: every option turns its provider's roles into standard role claims. The option would add `.RequirePermission("tasks.write")` (a policy provider that builds a policy for any `perm:` name, so nothing is registered per permission), permissions from a role → permissions map in settings (defaults in code), perhaps a choice to read them from token claims instead (Keycloak client roles, Entra app roles), 403 as ProblemDetails, `widgets.read` / `widgets.write` in the feature template, and in the samples only admins delete users. Per-resource checks ("only the owner can edit") stay app code; the docs would show `IAuthorizationService`.
- **Containers**, **dev container** (extras); **API versioning**, **rate limiting** (pattern candidates).

Open questions:

- **Removing an option.** Today options can only be added. Is removal a goal? If so, the resolver's requirement rules answer what happens to dependents (removing the outbox orphans the inbox: refuse, or offer to remove it too), and `addedBy` says which automatic units can go.
- **Changing a choice** of an applied option (CQRS `handlers` → `mediator`). Rejected today.
- **Upgrades.** Can a solution move to a newer template commit, and what happens to its options? Solutions from before the catalog can only get options this way.
- **Interactive UX.** The prompts are grouped by concept, with follow-up questions for choices and requirements; revisit once the catalog has a few dozen options (search, presets?).

To verify before the docs for users are written: MassTransit's current license terms. Already settled: the `Mediator` package is MIT (phase 6), and the phase 6 `HybridCache` question didn't arise (see [hidden-units.md](docs/roadmap/hidden-units.md#cache)).
