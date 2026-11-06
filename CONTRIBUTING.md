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

The CLI reads templates from git, not from your working folder, so **commit** before testing. Point it at your local clone so you don't have to push:

```powershell
$env:CLEANSLICES_TEMPLATE_REPO = "C:\path\to\clean-slices"

cd $env:TEMP
dotnet run --project C:\path\to\clean-slices\cli\CleanSlices.Cli -- new Acme.Test --samples --with result -y
```

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
- **Whole files** that exist only with or without an option are excluded in `template.json`, not wrapped in `#if`: a wrapped file is still generated, empty. Wrap them in `#if` as well only so the repository build skips them (`Result.cs` is `#if (result)` … `#endif` *and* excluded when `!result`).
- **The template engine removes every `#if` it sees**, including `#if DEBUG`, and keeps a block only if its condition is true for the template. Wrap a compiler directive that must survive in `//-:cnd:noEmit` … `//+:cnd:noEmit`.
- **The feature template isn't compiled here.** Check it by generating a solution with a feature (below).
- **Documentation** follows the same idea: text an option *replaces* is a whole file with a variant (`docs/error-handling.md` and `docs/error-handling.result.md`, picked and renamed by a conditional modifier in `template.json`); text an option *adds* is its own file. The README's options list uses one `<!--#if (option)-->` line per option.
- **Never write the CLI's name (`cleanslices`) in a file the template processes.** `sourceName` renames it to the user's solution name. `docs/CLI.md` is copied unprocessed for that reason.

### Adding an option

1. Add the unit to `catalog.json`, using the format in `catalog.schema.json` (editors complete it). Order in the file is the order in prompts and in `cleanslices list`.
2. Add the parameter (and choice parameters and computed values) to `.template.config/template.json`, and to `templates/cs-feature/.template.config/template.json` if features change. Add the file excludes.
3. Write the code with `#if` blocks, in the project template, the feature template and the samples. The samples show how to use the option; set `"samples": false` only when it can't be shown meaningfully, and say why in its documentation.
4. Check, before committing:
   ```powershell
   dotnet test --solution CleanSlices.Dev.slnx                          # default + catalog consistency
   dotnet test --solution CleanSlices.Dev.slnx -p:TemplateOptions=<id>  # the option
   ```
5. Commit, then check the CLI flows against your clone (`$env:CLEANSLICES_TEMPLATE_REPO`): a new solution with the option, and the option added later to a solution that has a feature:
   ```powershell
   cleanslices new Acme.A --samples --with <id> -y; cd Acme.A; cleanslices add feature Product -y; dotnet test
   cleanslices new Acme.B --samples -y; cd Acme.B; cleanslices add feature Product -y; git add -A; git commit -m wip; cleanslices add <id> -y; dotnet test
   ```
   With prompts (no `-y`), also look at how the option appears in the interactive list.

Options should add files rather than edit shared ones where they can (rule 4 in ROADMAP.md): every edited line is a possible conflict with the user's code.

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
