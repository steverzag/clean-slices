# The `cleanslices` CLI

`cleanslices` is a .NET tool that creates projects from this template and grows them afterwards. It asks a few questions, like `npm create astro` does. Every question also has a flag, so it works in scripts too.

```
cleanslices                          create a new solution (interactive)
cleanslices add feature              add a vertical slice to the current solution
cleanslices add pattern              merge an optional pattern (e.g. Result<T>) into the current solution
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

## Create a solution

```powershell
cleanslices
```

You'll be asked for:

1. **Solution name**, e.g. `Acme.Orders`. Projects become `Acme.Orders.Api`, `Acme.Orders.Core`, and so on.
2. **Sample features**: whether to include the Tasks/Users example code (off by default).
3. **Patterns**: optional building blocks to include from the start, such as `result` (Result&lt;T&gt; instead of exceptions).
4. **Git**: whether to create a repository with an initial commit (on by default).

The solution is created in `./<name>` and its packages are restored, so it builds straight away.

Without prompts:

```powershell
cleanslices new Acme.Orders -y                            # defaults: no samples, no patterns, git on
cleanslices new Acme.Orders --samples --pattern result -y
```

| Option | Meaning |
|---|---|
| `[name]` | Solution name. Asked for when omitted. |
| `-o, --output <DIR>` | Output folder (default `./<name>`). |
| `--samples` / `--no-samples` | Include the Tasks/Users sample features. |
| `-p, --pattern <NAME>` | Include a pattern. Repeat it to add several. |
| `--git` / `--no-git` | Create a git repository with an initial commit. |
| `--no-restore` | Skip `dotnet restore`. |
| `--template-repo <SOURCE>` | Take the templates from another repository (path or git URL). |
| `-y, --yes` | Don't ask; use the defaults for anything not passed. |

## Add a feature

Run this anywhere inside the solution:

```powershell
cleanslices add feature              # asks for the entity name and its plural
cleanslices add feature Product
cleanslices add feature Person --plural People --migration
```

It generates a complete slice across the three projects:

- **Core**: the entity (with `Name`, `Create`, `UpdateName`, `Delete`), its domain events and its errors
- **Infrastructure**: the EF configuration and event handlers
- **Api**: CRUD endpoints (`POST /products`, `GET /products`, `GET /products/{id}`, `PATCH /products/{id}/name`, `DELETE /products/{id}`)

Nothing existing is edited. Endpoints, event handlers and EF configurations are picked up automatically, and the entity is reached through `context.Set<Product>()`, so `AppDbContext` stays as it is. The generated code follows the solution's patterns: in a solution with `result`, the feature uses `Result<T>`.

| Option | Meaning |
|---|---|
| `[entity]` | Singular PascalCase name, e.g. `OrderLine`. |
| `-p, --plural <PLURAL>` | Plural, used for folders, namespaces, the table and the route (default: an English plural, e.g. `Categories`). Routes are kebab-case: `/order-lines`. |
| `--migration` / `--no-migration` | Create an EF Core migration `Add<Entity>` (needs `dotnet-ef`). |
| `-y, --yes` | Don't ask. |

Names that clash with .NET types, such as `Task` or `Thread`, are rejected.

## Add a pattern

Patterns are optional pieces you can add to a solution after it was created:

```powershell
cleanslices add pattern              # pick from the list of available patterns
cleanslices add pattern result
```

The solution must be a git repository with nothing uncommitted. The pattern is applied as a real git merge:

1. The CLI renders the solution twice: as the template produced it, and as it would look with the pattern. Both renders include your features.
2. It merges the difference into your code. Your own changes are kept, and the features you added are converted too.
3. If nothing conflicts, the result is committed as `feat: apply <pattern> pattern`. Undo it with `git reset --hard HEAD~1`.

If one of your changes touches the same lines as the pattern, the affected files are listed and contain the usual `<<<<<<<` / `>>>>>>>` markers. Resolve them, then `git add -A` and `git commit`, or run `git reset --hard HEAD` to back out.

| Option | Meaning |
|---|---|
| `[pattern]` | Pattern name. Asked for when omitted. |
| `--no-commit` | Stage the changes without committing them. |
| `--template-repo <SOURCE>` | Take the patterns from another repository. |
| `-y, --yes` | Don't ask. |

Available patterns:

| Pattern | What it does |
|---|---|
| `result` | Domain methods return `Result` / `Result<T>` instead of throwing, endpoints compose them with `Map`/`Tap`, and `ResultExtensions` turn them into ProblemDetails responses. |

## `.cleanslices.json`

`cleanslices new` writes this file at the solution root. Commit it with the code. It records how the solution was generated:

```json
{
  "name": "Acme.Orders",
  "options": { "samples": false },
  "templateRepo": "https://github.com/steverzag/clean-slices.git",
  "templateCommit": "a63f5be9d2a0…",
  "patterns": [ "result" ],
  "slices": [ { "name": "Product", "plural": "Products" } ]
}
```

`add feature` and `add pattern` read it to render code from the same template version and with the same patterns, and they update it. Solutions created without the CLI also work: the first `add feature` detects the layout from the `.slnx` and creates the file.

## Where the templates come from

The CLI downloads the templates from this repository, so new patterns appear without updating the tool. The repository is chosen in this order:

1. `--template-repo <path-or-url>` on the command
2. `templateRepo` in `.cleanslices.json`
3. the `CLEANSLICES_TEMPLATE_REPO` environment variable
4. `https://github.com/steverzag/clean-slices.git`

If GitHub can't be reached, `cleanslices new` falls back to locally installed templates (`dotnet new install <template-repo-folder>`), without patterns.

Working on the templates or the CLI themselves? See [CONTRIBUTING.md](https://github.com/steverzag/clean-slices/blob/main/CONTRIBUTING.md).
