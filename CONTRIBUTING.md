# Working on the template

This repository is three things at once:

| Part | Where | What it is |
|---|---|---|
| Project template | repo root (`.template.config/`) | `dotnet new cleanslices`: the solution itself |
| Feature template | `templates/cs-feature/` | `dotnet new cs-feature`: one vertical slice, written for a placeholder entity called `Widget` |
| CLI | `cli/CleanSlices.Cli/` | the `cleanslices` tool, which drives both templates and applies options |
| Catalog | `catalog.json` | the options the CLI offers and how they relate, validated by `catalog.schema.json` |

`templates/`, `cli/`, `CleanSlices.Dev.slnx`, the catalog files, this file, [ROADMAP.md](ROADMAP.md) and `docs/roadmap/` are excluded from the project template, so generated solutions don't get them. ROADMAP.md describes where the CLI is heading (a project configurator) and the design rules new options should follow.

## Solutions

- **`CleanSlices.Dev.slnx`**: open this one while you work. It has the template projects, the CLI and its tests, the catalog and the template configs.
- **`CleanSlices.slnx`**: the template's own solution. Every generated project gets a renamed copy, so it must never reference `cli/`.

## Testing changes locally

Run the tests with the dev solution. The repository root has two solution files, so `dotnet` needs to be told which one:

```powershell
dotnet test --solution CleanSlices.Dev.slnx
```

The integration tests (`tests/CleanSlices.Api.Tests`) need Docker running. Generated solutions have a single solution file, so plain `dotnet test` works there. `cli/CleanSlices.Cli.Tests` tests the CLI, including the consistency tests that keep `catalog.json` and both `template.json` files in step.

That runs the template **without options**. To build and test it with options, pass them as compiler symbols (see [How options work](#how-options-work)):

```powershell
dotnet test --solution CleanSlices.Dev.slnx -p:TemplateOptions=result
```

The CLI reads templates from git, not from your working folder, so **commit** before testing. Point it at your local clone so you don't have to push, and at `HEAD` so it uses the branch you're on instead of `main`:

```powershell
$env:CLEANSLICES_TEMPLATE_REPO = "C:\path\to\clean-slices"
$env:CLEANSLICES_TEMPLATE_REF = "HEAD"

cd $env:TEMP
dotnet run --project C:\path\to\clean-slices\cli\CleanSlices.Cli -- new Acme.Test --samples --with result -y
```

The CI jobs below do exactly this, and can be run locally too.

`dotnet run` always uses the current CLI code. To test the installed tool instead, bump `<Version>` in `cli/CleanSlices.Cli/CleanSlices.Cli.csproj`, then:

```powershell
dotnet pack cli/CleanSlices.Cli -c Release
dotnet tool update -g CleanSlices.Cli --add-source ./cli/artifacts
```

Without a version bump, NuGet's cache may keep serving the old package. In that case, uninstall and reinstall the tool.

Plain `dotnet new` uses the templates installed from this folder. Reinstall them after changing a `template.json`:

```powershell
dotnet new install . --force
```

## How options work

Every option lives on `main`, as **template conditionals**. Three places describe it:

| Where | Says |
|---|---|
| `catalog.json` | *what* the option is and how it relates to others: concept, group, choices, requires / provides / conflicts, sample support, license |
| `.template.config/template.json` (and `templates/cs-feature/...` if features change) | *how* it renders: a parameter, and which files exist only with or without it |
| the code | `#if (<option>)` blocks inside shared files |

The CLI reads the catalog **at the template commit a solution came from**, resolves the user's selection (choice defaults, hidden units for requirements, groups, conflicts, glue units) and passes the result to `dotnet new` as parameters. `add <option>` renders the solution before and after, with the same template commit, and merges the difference into the user's code with `git merge-tree`.

### Symbols

- An option's template parameter is its id, with `-` replaced by `_` (`integration-events` → `integration_events`). It's a `bool` defaulting to `false`.
- A choice is a `choice` parameter named `<option>_<choice>` (`cqrs_dispatcher`), plus one **computed bool per value**, named after the value: `"mediator": { "type": "computed", "value": "(cqrs_dispatcher == \"mediator\")" }`. Code tests the bool (`#if (mediator)`), because the compiler can't read string comparisons.
  The computed bool of the default value is true even when the option is off (`handlers` is true in a solution without CQRS), so always test it together with its option: `#if (cqrs && handlers)`, `"condition": "(!cqrs || !handlers)"`. In this repository, pass the option and the value: `-p:TemplateOptions=cqrs%3Bmediator`.
- Ids, choice parameters and choice values share one namespace; `samples`, `name`, `app` and `plural` are reserved.
- The feature template only declares the options its code depends on; the CLI passes each template just the parameters it defines.
- `CatalogValidator` and `ConsistencyTests` (in `cli/CleanSlices.Cli.Tests`) fail when the catalog and the `template.json` files disagree, or when the catalog has a broken reference.

### Writing conditional code

- **Real `#if`, not `//#if`.** `#if (result)` is read by both the template engine and the compiler, so the repository can build every variant. `Directory.Build.props` has a block, removed from generated solutions, that adds `$(TemplateOptions)` to the compiler's symbols:
  ```powershell
  dotnet build CleanSlices.Dev.slnx -p:TemplateOptions=result
  dotnet build CleanSlices.Dev.slnx "-p:TemplateOptions=result%3Bmediator"   # several: %3B, since , and ; split -p values
  ```
  `samples` is always defined there, because the sample code is always compiled in this repository. The sample blocks themselves stay `//#if (samples)`.
- **Whole members.** When a member differs in more than one place, or its declaration differs, put the whole member in each branch: `#if (result)` *complete method* `#else` *complete method* `#endif`. Neighbouring members that differ can share one block. A single, contiguous difference keeps a small block. Each variant should read top to bottom.
- **Several options in one member: flat chains.** When options meet in the same member (`result`, repository and CQRS in a handler), write one complete member per combination in a flat chain with combined conditions, never nested inside the member:
  ```csharp
  #if (repository && result)
  	public async Task<IResult> Handler(...) { ... }
  #elif (repository)
  	public async Task<IResult> Handler(...) { ... }
  #elif (result)
  	public async Task<IResult> Handler(...) { ... }
  #else
  	public async Task<IResult> Handler(...) { ... }
  #endif
  ```
  Each combination reads as plain code, and all of them sit side by side, which makes keeping them in step easier. The prototype that settled this (`UpdateWidgetName` in every shape) is described in `docs/roadmap/patterns.md` under CQRS. If a file ever outgrows this, one file per combination (renamed by `template.json`) is the fallback.
- **Whole files** that exist only with or without an option are excluded in `template.json`, not wrapped in `#if`: a wrapped file is still generated, empty. Wrap them in `#if` as well only so the repository build skips them (`Result.cs` is `#if (result)` … `#endif` *and* excluded when `!result`).
- **The template engine removes every `#if` it sees**, including `#if DEBUG`, and keeps a block only if its condition is true for the template. Wrap a compiler directive that must survive in `//-:cnd:noEmit` … `//+:cnd:noEmit`.
- **The feature template isn't compiled here.** Check it by generating a solution with a feature (below).
- **Packages are XML conditionals** in `Directory.Packages.props` and the `.csproj` files. In this repository they're comments, so the build references every option's packages at once. That hides version conflicts a generated solution can have: Npgsql alone pulls an older EF Core relational package, which the repository got from the SQL Server package. Package resolution is only tested by generating solutions (the CI matrix, or `run <job>` locally).
- **JSON files can't have conditionals.** The template engine doesn't uncomment lines, and the repository's JSON would be invalid with both branches. Use a whole-file variant renamed by `template.json` (`appsettings.postgresql.json` → `appsettings.json`), like the docs variants.
- **Background services in tests.** An option with a hosted worker (the outbox) removes it in `ApiFactory` and lets tests call the work directly, so the tests are deterministic.
- **Documentation** follows the same idea: text an option *replaces* is a whole file with a variant (`docs/error-handling.md` and `docs/error-handling.result.md`, picked and renamed by a conditional modifier in `template.json`); text an option *adds* is its own file. The README's options list uses one `<!--#if (option)-->` line per option.
- **Never write the CLI's name (`cleanslices`) in a file the template processes.** `sourceName` renames it to the user's solution name. `docs/CLI.md` is copied unprocessed for that reason.

### Adding an option

1. Add the unit to `catalog.json`, using the format in `catalog.schema.json` (editors complete it). Order in the file is the order in prompts and in `cleanslices list`. Set `addsTables` if it brings entity configurations, and `recommended` if it should be offered once its requirements are met.
2. Add the parameter (and choice parameters and computed values) to `.template.config/template.json`, and to `templates/cs-feature/.template.config/template.json` if features change. Add the file excludes.
3. Write the code with `#if` blocks, in the project template, the feature template and the samples. The samples show how to use the option; set `"samples": false` only when it can't be shown meaningfully, and say why in its documentation.
4. Check, before committing:
   ```powershell
   dotnet test --solution CleanSlices.Dev.slnx                          # default + catalog consistency
   dotnet test --solution CleanSlices.Dev.slnx -p:TemplateOptions=<id>  # the option, with the hidden units it requires (e.g. idempotency%3Bcurrent_user%3Bcache)
   ```
5. Commit, then run the option's CI jobs locally (see [CI](#ci)): the new jobs appear in `list` by themselves.
   ```powershell
   dotnet run --project cli/CleanSlices.Matrix -- list
   dotnet run --project cli/CleanSlices.Matrix -- run <id>-samples
   dotnet run --project cli/CleanSlices.Matrix -- run <id>-later
   ```
   With prompts, also look at how the option appears in the interactive list (`cleanslices new Acme.Try` with `CLEANSLICES_TEMPLATE_REF=HEAD`).

Options should add files rather than edit shared ones where they can (rule 4 in ROADMAP.md): every edited line is a possible conflict with the user's code.

## CI

`.github/workflows/ci.yml` tests the template repository on GitHub Actions, on Linux (the API tests need Docker):

- **Template repository**: `dotnet test --solution CleanSlices.Dev.slnx`, the template without options, the CLI tests and the catalog consistency tests.
- **The matrix**: one job per combination of options. `cli/CleanSlices.Matrix` derives the combinations from `catalog.json` through the CLI's resolver, so a new option is tested without touching the workflow. Combinations that can't exist (conflicts, two of a group, no sample support) are skipped with the resolver's reason; an option that needs another gets its first provider; combinations that resolve to the same configuration run once.

| Tier | Jobs | When |
|---|---|---|
| push | the default with and without samples; each option alone, and each value of each choice, with and without samples; each option added later to a solution with a feature; the listed combinations marked push | pull requests |
| full | push, plus every pair (with samples) and everything that fits together | pushes to `main`, nightly, and **Run workflow** (tier selectable) |

**Listed combinations** (`cli/CleanSlices.Matrix/Combinations.cs`) add what pairs miss: options that meet in the same members, where each handler has one variant per combination. `result` + repository + CQRS runs on every push (and with Mediator in the full tier). Each is written like `--with` values and gets the tier it says; a test in `ConsistencyTests` fails when one stops resolving against the real catalog, so a renamed option can't drop it silently.

A job generates a solution with the CLI built from source and the template at the commit under test, adds a feature, and runs the solution's tests. For one job per set of options it also runs the template repository's tests with those options (`-p:TemplateOptions=…`).

Every job runs locally the same way, with Docker running:

```powershell
dotnet run --project cli/CleanSlices.Matrix -- list                 # what CI would run, and what it skips and why
dotnet run --project cli/CleanSlices.Matrix -- list --tier push
dotnet run --project cli/CleanSlices.Matrix -- run result-later     # one job; the solution stays in %TEMP%\cleanslices-matrix\<job>
dotnet run --project cli/CleanSlices.Matrix -- run result --ref main
dotnet run --project cli/CleanSlices.Matrix -- run cqrs-dispatcher-mediator-samples --work C:\m   # Windows: a short --work folder for long job names (path length limit)
```

`run` uses the template at `HEAD` (committed changes) unless `--ref` says otherwise.

GitHub runs at most 256 jobs per matrix. With every pair that's reached at around 20 options; before then, the full tier should only pair options that touch the same files.

The last job, **CI passed**, succeeds only when the repository job and every matrix job did. Checks don't block merges by themselves: to require them, add a branch protection rule (or ruleset) for `main` in the repository's GitHub settings that requires *CI passed*. It's one fixed name, while the matrix jobs change with the catalog.

## Template rules worth knowing

- `sourceName` is `CleanSlices`, so every `CleanSlices` and `cleanslices` in file names and contents gets the user's name. Aspire's `Projects.CleanSlices_Api` and the resource name `cleanslices-api` have their own derived symbols in `template.json`.
- Sample-only code is wrapped in `//#if (samples)` … `//#endif` (C#), `# #if (samples)` (`.http` files) or `<!--#if (samples)-->` (Markdown).
- New features must be additive: no `DbSet` (use `context.Set<T>()` and `ToTable(...)`), and no registration edits (endpoints and handlers are found by reflection).
- The CLI's default template repository is `TemplateRepository.DefaultSource` in `cli/CleanSlices.Cli/Infrastructure/TemplateRepository.cs`. Update it if the repository moves.

## Pending

These concern this repository only. Its tracked migrations (`src/CleanSlices.Infrastructure/Migrations`) are excluded from generated solutions.

- Add a migration that matches the current model:
  - drop the `Version` columns
  - remove `Id` and `CreatedAt` from `TaskItemUserAssignments`
  - make the composite primary key
- Optionally remove `AppDbContextFactory` and run EF with `--startup-project src/CleanSlices.Api`, so the connection string comes only from appsettings.
