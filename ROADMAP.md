# Roadmap: from template to project configurator

This document describes where CleanSlices is heading and the design rules that should get it there. It's for people working on the template and the CLI; generated solutions don't get it.

## The goal

Today the CLI creates a solution, adds features to it, and merges optional patterns into it. There is one pattern (`result`).

The goal is a **project configurator**: you describe the setup you want, choosing from several *concepts*, each with several *options*, and the CLI generates a solution where all the chosen options work together. Both lists, concepts and the options within them, should be able to grow without limit.

| Concept | What it decides | Example options |
|---|---|---|
| **Patterns** | How the code is structured | Result&lt;T&gt;, outbox |
| **Tools** | Which infrastructure the solution uses | Database provider (SQL Server, PostgreSQL), cache (in-memory, Redis), queue (RabbitMQ, Azure Service Bus) |
| **Strategies** | Cross-cutting behavior applied to entities | Soft delete, audit history |

The same choices must also hold *after* creation: `add feature` generates code that fits the configuration, and adding an option to an existing solution merges it into the user's code.

Similar tools exist in other ecosystems: JHipster (Java) is the closest, Spring Initializr and ABP are smaller versions. They show it's feasible, and that most of the long-term cost is keeping combinations working, not writing options.

## The core problem: cohesion

Options interact. If the user picks RabbitMQ as the queue, the outbox should deliver its events through RabbitMQ instead of in-process. If they pick PostgreSQL, everything that touches the database must work on PostgreSQL.

The number of possible interactions grows much faster than the number of options: 10 options make 45 pairs. If every pair needs its own code, the project stops scaling long before the list gets interesting.

## Design rules

These rules are what make the goal achievable. A new option that breaks one of them should be redesigned before it's merged.

1. **Options meet only through seams in the base project.** A seam is an interface or registration point that the base template defines. One option *provides* an implementation; another *uses* the interface. Neither knows the other exists.

   Example: the outbox stores events and hands each one to an `IMessageDispatcher`. It never mentions RabbitMQ. The RabbitMQ tool registers an `IMessageDispatcher` that sends to the broker. "Outbox + RabbitMQ" needs no code of its own: it falls out of dependency injection.

2. **Every seam works with nothing selected.** The base template ships a default implementation for each seam (e.g. an in-process dispatcher that calls the handlers directly). A solution with no options builds and runs, and every option can be added on its own.

3. **Code that is specific to a combination lives in a glue unit.** Sometimes a pair really needs specific code. A glue unit is a small piece applied only when both options are selected. Prefer designing it away first (e.g. a provider-neutral query instead of one per database). **The number of glue units is the health metric of the design:** if it grows with every option, the seams are in the wrong place.

4. **Add files rather than edit shared ones.** Every edited line is a possible conflict with the user's code or with another option. An option should ideally be new files plus one registration call.

5. **Provider-specific artifacts are generated on the user's side.** Migrations are the main case: they differ per database provider. Generated solutions already don't ship migrations (`template.json` excludes `src/CleanSlices.Infrastructure/Migrations/**`); users run `dotnet ef migrations add`. Options keep it that way.

6. **The feature template honors the configuration.** `add feature` must generate code that fits the chosen options. This already happens for `result` (`templates/cs-feature/` changes with it). Strategies make it more important: with soft delete selected, new entities get the soft-delete marker.

## Seams

The seams the planned options need. None exist yet except where noted.

| Seam | Default (base template) | Provided by | Used by |
|---|---|---|---|
| `IPublisher` / `INotificationHandler<T>` | In-process publisher | exists today | Domain event handlers |
| `IMessageDispatcher` | In-process: forwards to `IPublisher` | Queue tools (RabbitMQ, Service Bus) | Outbox |
| Database provider registration | SQL Server | DB provider tools | `AppDbContext`, Aspire AppHost |
| `ICache` (or `HybridCache`) | In-memory | Cache tools (Redis) | Features that cache reads |
| `ISoftDeletable` + EF query filter | none | Soft delete strategy | Entities, feature template |
| `IAuditable` + EF interceptor | none | Audit strategy | Entities, feature template |

Seams that need nothing from the base until an option uses them (soft delete, audit) can be introduced by the option itself, as long as no other option depends on them.

## How options are applied

There are two kinds of options, and they need different mechanisms.

| Kind | Example | Mechanism |
|---|---|---|
| **Additive**: new files plus a few registration calls | Tools, strategies, outbox | A **module**: its files, plus edits at a few known *anchors* (`InfrastructureRegistration`, `AppHost.cs`, `Directory.Packages.props`, `appsettings.json`) |
| **Invasive**: rewrites code across the solution | Result&lt;T&gt; (35 files) | A **pattern branch**, cherry-picked onto the template commit, as today |

Most future options are additive. Modules almost never conflict, because they mostly add files. Invasive options should stay rare.

Adding an option *to an existing solution* doesn't depend on the mechanism. `add pattern` already renders the solution before and after the option and merges the difference into the user's code with `git merge-tree`, and that approach works for modules too.

## The catalog

Each option declares what it is and how it relates to others, in a catalog the CLI reads. A sketch:

```jsonc
{
  "id": "outbox",
  "concept": "pattern",
  "title": "Outbox for reliable domain events",
  "mechanism": "module",           // or "branch"
  "requires": ["message-dispatcher"],
  "provides": ["outbox"],
  "conflicts": [],
  "featureTemplate": {}            // symbols passed to cs-feature, if any
}
```

```jsonc
{
  "id": "rabbitmq",
  "concept": "tool",
  "group": "queue",                // at most one option per group
  "title": "RabbitMQ",
  "mechanism": "module",
  "provides": ["message-dispatcher"],
  "conflicts": []
}
```

The CLI resolves the selection like a package manager:

1. Collect the selected options.
2. For each `requires` capability that no selected option provides, use the base template's default (if there is one) or ask.
3. Reject conflicts and two options in the same group before generating anything, with a message that says why.
4. Add the glue units whose conditions are met.
5. Apply everything in a deterministic order.

`.cleanslices.json` records the resolved configuration, not just the options the user typed, so later commands (`add feature`, adding another option) see the same picture.

## Testing

Combinations can't be tested by hand. CI must generate and build:

- the base template with no options,
- each option alone,
- every **pair** of options (most interaction bugs involve two options),
- one solution with everything compatible selected.

For each, it should: generate a new solution, add a feature with `add feature`, add each option later with `add <option>` on a solution that has a feature, and build. Once the solution has tests, run them too.

Set this up before the catalog grows past about five options. Without it, combinations break silently.

## Versioning

- The template commit a solution came from is recorded in `.cleanslices.json`; options are applied against that commit. Keep it that way.
- The catalog lives in the template repository, so it's versioned with the template.
- When the catalog format changes, the CLI must still read older solutions' manifests, or say clearly that it can't.

## Roadmap

Each phase ends with something usable and tests one part of the design.

| Phase | What | Proves |
|---|---|---|
| 0, done | Patterns as git branches; `result` | Composing and merging options into existing code |
| 1 | **Outbox** pattern, with `IMessageDispatcher` and an in-process default from day one. Built as a pattern branch, since modules don't exist yet; it's additive, so it can move to a module in phase 2 | Seams and defaults (rules 1 and 2) |
| 2 | **Database provider** tool: PostgreSQL alongside SQL Server | The module mechanism and anchors; provider-specific concerns |
| 3 | **Catalog and resolver** in the CLI; `.cleanslices.json` records the resolved configuration | requires / provides / conflicts / groups |
| 4 | **CI matrix** | Combinations stay working |
| 5 | **Queue** tool (RabbitMQ) | Outbox + RabbitMQ with zero glue |
| 6 | **Strategies**: soft delete, audit | Options that change the feature template |
| 7 | **Cache** tool; publish the CLI on NuGet | Ready for other people |

## Open questions

- **Do patterns stay git branches?** Branches suit invasive changes like Result&lt;T&gt;, but they're the hardest to combine. Could Result&lt;T&gt; become a module plus template conditionals instead?
- **Removing an option.** Today options can only be added. Is removal a goal?
- **Strategies on existing entities.** When soft delete is added to a solution that already has features, should the CLI convert the existing entities, or only new ones?
- **Interactive UX.** With many options, the prompts need grouping (by concept) and sensible defaults.
- **Upgrades.** Can a solution move to a newer template commit, and what happens to its options?
- **Naming.** "Tools" might read better as "infrastructure".
