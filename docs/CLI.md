# The `cleanslices` CLI

`cleanslices` is a .NET tool that creates projects from this template and grows them afterwards. It asks a few questions, like `npm create astro` does. Every question also has a flag, so it works in scripts too.

```
cleanslices                          create a new solution (interactive)
cleanslices list                     list the options the template offers
cleanslices add <option>...          merge options into the current solution
cleanslices add feature              add a vertical slice to the current solution
```

## Install

The tool isn't on NuGet yet, so build it from the template repository:

```powershell
git clone https://github.com/steverzag/clean-slices.git
cd clean-slices
dotnet pack cli/CleanSlices.Cli -c Release
dotnet tool install -g CleanSlices.Cli --add-source ./cli/artifacts
```

To update it later, pull, bump `<Version>` in `cli/CleanSlices.Cli/CleanSlices.Cli.csproj`, pack again, and run `dotnet tool update -g CleanSlices.Cli --add-source ./cli/artifacts`.

You need the .NET 10 SDK and git. `dotnet-ef` is optional; it's only needed if you want `add feature` to create migrations for you.

## Options

Options are optional building blocks, such as `result` (Result&lt;T&gt; instead of exceptions). They're grouped by concept:

| Concept | An option belongs here if it… | Choose |
|---|---|---|
| Patterns | changes how code is written | any number |
| Infrastructure | picks which product sits behind a seam (database, queue, cache, …) | at most one per group |
| Strategies | adds behavior to entities | any number |
| Extras | adds files around the code | any number |

Some options have **choices**, settings that only matter when the option is selected. An option is written `<id>`, or `<id>:<choice>=<value>[,<choice>=<value>]` to pick choices; choices not given get their default.

The CLI makes the options work together:

- Options that need something get it. Pieces several options share (for example the integration events behind the outbox and the queues) are added automatically, and the summary says why.
- When an option needs another option and several could do it, the CLI asks which one, or tells you which `--with` to add.
- Some options are **recommended** once your selection makes them possible: picking the outbox makes the inbox available. The CLI asks whether to add it (yes by default), or without prompts prints a tip. It never adds one silently.
- Options that can't be combined, or two from the same group, are rejected before anything is generated, with the reason.
- Options that use a commercial package or product print a notice, and their license key setting is generated empty.

`cleanslices list` shows every option, with its choices and what it needs. Inside a solution it also marks the options the solution has.

Available options:

| Option | What it does |
|---|---|
| `result` | Domain methods return `Result` / `Result<T>` instead of throwing, endpoints compose them with `Map`/`Tap`, and `ResultExtensions` turn them into ProblemDetails responses. |
| `outbox` | Integration events are stored with the data, in the same save, and delivered by a background processor with retries. Brings integration events with it. |
| `inbox` | Each integration event handler runs once per event, even when it's delivered again. Needs the outbox; recommended with it. |
| `sqlserver` | SQL Server, the default database: in a container from the AppHost, or an existing server. |
| `postgresql` | PostgreSQL instead of SQL Server, with snake_case names and case-insensitive emails. Added to an existing solution, it replaces SQL Server. |

## Create a solution

```powershell
cleanslices
```

You'll be asked for:

1. **Solution name**, e.g. `Acme.Orders`. Projects become `Acme.Orders.Api`, `Acme.Orders.Core`, and so on.
2. **Sample features**: whether to include the Tasks/Users example code (off by default). The samples show every option you pick, as a reference for your own features.
3. **Options**, concept by concept, then the choices of the ones you picked. A summary shows what will be applied, and you confirm it.
4. **Git**: whether to create a repository with an initial commit (on by default).

The solution is created in `./<name>` and its packages are restored, so it builds straight away.

Without prompts:

```powershell
cleanslices new Acme.Orders -y                              # defaults: no samples, no options, git on
cleanslices new Acme.Orders --samples --with result -y
```

| Option | Meaning |
|---|---|
| `[name]` | Solution name. Asked for when omitted. |
| `-o, --output <DIR>` | Output folder (default `./<name>`). |
| `--samples` / `--no-samples` | Include the Tasks/Users sample features. |
| `-w, --with <OPTION>` | Include an option: `<id>` or `<id>:<choice>=<value>`. Repeat it to add several. |
| `--git` / `--no-git` | Create a git repository with an initial commit. |
| `--no-restore` | Skip `dotnet restore`. |
| `--template-repo <SOURCE>` | Take the templates from another repository (path or git URL). |
| `--template-ref <REF>` | Create the solution from a branch, tag, commit or `HEAD` of the template repository instead of `main` (see [below](#where-the-templates-come-from)). |
| `-y, --yes` | Don't ask; use the defaults for anything not passed. |

## Add a feature

Run this anywhere inside the solution:

```powershell
cleanslices add feature              # asks for the entity name and its plural
cleanslices add feature Product
cleanslices add feature Person --plural People --migration
```

It generates a complete slice across the three projects, plus its tests:

- **Core**: the entity (with `Name`, `Create`, `UpdateName`, `Delete`), its domain events and its errors
- **Infrastructure**: the EF configuration and event handlers
- **Api**: CRUD endpoints (`POST /products`, `GET /products` (paged, newest first), `GET /products/{id}`, `PATCH /products/{id}/name`, `DELETE /products/{id}`)
- **Tests**: `ProductTests` for the entity and `ProductEndpointsTests` for the endpoints. Solutions created before test projects existed don't get them.

Nothing existing is edited. Endpoints, event handlers and EF configurations are picked up automatically, and the entity is reached through `context.Set<Product>()`, so `AppDbContext` stays as it is. The generated code follows the solution's options: in a solution with `result`, the feature uses `Result<T>`.

| Option | Meaning |
|---|---|
| `[entity]` | Singular PascalCase name, e.g. `OrderLine`. |
| `-p, --plural <PLURAL>` | Plural, used for folders, namespaces, the table and the route (default: an English plural, e.g. `Categories`). Routes are kebab-case: `/order-lines`. |
| `--migration` / `--no-migration` | Create an EF Core migration `Add<Entity>` (needs `dotnet-ef`). |
| `-y, --yes` | Don't ask. |

Names that clash with .NET types, such as `Task` or `Thread`, are rejected.

## Add options

Options can also be added to a solution after it was created:

```powershell
cleanslices add                      # pick from the options the solution doesn't have yet
cleanslices add result
cleanslices add <option> <option>    # several options in one merge
```

`feature` is the one word that isn't an option: `cleanslices add feature` adds a feature.

The solution must be a git repository with nothing uncommitted. The options are applied as a real git merge:

1. The CLI renders the solution twice, from the template version it was created with: as it is now, and with the new options. Both renders include your features.
2. It merges the difference into your code. Your own changes are kept, and the features you added are converted too.
3. If nothing conflicts, the result is committed as `feat: add <options>`. Undo it with `git reset --hard HEAD~1`.

If one of your changes touches the same lines as an option, the affected files are listed and contain the usual `<<<<<<<` / `>>>>>>>` markers. Resolve them, then `git add -A` and `git commit`, or run `git reset --hard HEAD` to back out.

Picking an option from a group the solution already has an option in **replaces** it (for example another database provider). Migrations are specific to a database provider, so the CLI reminds you to recreate them.

Options that add database tables (the outbox, the inbox) print the migration to create after the merge, for example `dotnet ef migrations add AddOutbox --project src/Acme.Orders.Infrastructure`. The CLI doesn't run it for you.

Options can't be removed, and their choices can't be changed after they're applied, yet.

| Option | Meaning |
|---|---|
| `[options]` | Options to add: `<id>` or `<id>:<choice>=<value>`. Asked for when omitted. |
| `--no-commit` | Stage the changes without committing them. |
| `--template-repo <SOURCE>` | Take the templates from another repository. |
| `-y, --yes` | Don't ask. |

## `.cleanslices.json`

`cleanslices new` writes this file at the solution root. Commit it with the code. It records how the solution was generated:

```json
{
  "manifestVersion": 2,
  "name": "Acme.Orders",
  "cliVersion": "0.6.0",
  "samples": false,
  "templateRepo": "https://github.com/steverzag/clean-slices.git",
  "templateCommit": "a63f5be9d2a0…",
  "options": [ { "id": "result" } ],
  "slices": [ { "name": "Product", "plural": "Products" } ]
}
```

`options` is the resolved configuration: the options you picked with their choices (`{ "id": "cqrs", "dispatcher": "mediator" }`), and the pieces added automatically, with the options they were added for (`"addedBy": [ "outbox" ]`).

`add feature` and `add <option>` read it to render code from the same template version with the same options, and they update it. Solutions created without the CLI also work: the first `add feature` detects the layout from the `.slnx` and creates the file.

Older solutions:

- **Created by cleanslices 0.1 or 0.2 without patterns**: the file is upgraded to the new format the next time a command writes it. Their template version predates options, so `add feature` works, but options can't be added to them.
- **Created by 0.2 with a pattern** (`"patterns": [ "result" ]`): this version can't extend them. Use cleanslices 0.2.x, or create the solution again.

## Where the templates come from

The CLI downloads the templates from this repository, so new options appear without updating the tool. The repository is chosen in this order:

1. `--template-repo <path-or-url>` on the command
2. `templateRepo` in `.cleanslices.json`
3. the `CLEANSLICES_TEMPLATE_REPO` environment variable
4. `https://github.com/steverzag/clean-slices.git`

A new solution is created from the repository's `main`. To try another version of the templates, pass `--template-ref <ref>` to `new` or `list`, or set `CLEANSLICES_TEMPLATE_REF`. A ref is a branch, a tag, a commit, or `HEAD`: what the template repository has checked out, which with `CLEANSLICES_TEMPLATE_REPO` pointing at your clone is the branch you're working on (committed changes only). The CLI says which template it uses when it isn't `main`.

The ref only chooses where a solution starts. `.cleanslices.json` records the commit, and every later command renders from that commit. A solution created from a branch therefore depends on that commit staying reachable in its template repository: if the branch is deleted, or was never pushed and the solution later uses GitHub, `add feature` and `add <option>` can't find its template anymore.

If GitHub can't be reached, `cleanslices new` falls back to locally installed templates (`dotnet new install <template-repo-folder>`), without options.

If the template repository uses a newer catalog format than your cleanslices understands, the CLI asks you to update it.

Working on the templates or the CLI themselves? See [CONTRIBUTING.md](https://github.com/steverzag/clean-slices/blob/main/CONTRIBUTING.md).
