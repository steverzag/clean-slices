# Working on the template

This repository is three things at once:

| Part | Where | What it is |
|---|---|---|
| Project template | repo root (`.template.config/`) | `dotnet new cleanslices`: the solution itself |
| Feature template | `templates/cs-feature/` | `dotnet new cs-feature`: one vertical slice, written for a placeholder entity called `Widget` |
| CLI | `cli/CleanSlices.Cli/` | the `cleanslices` tool, which drives both templates and applies patterns |

`templates/`, `cli/`, `CleanSlices.Dev.slnx` and this file are excluded from the project template, so generated solutions don't get them.

## Solutions

- **`CleanSlices.Dev.slnx`**: open this one while you work. It has the template projects, the CLI and the template configs.
- **`CleanSlices.slnx`**: the template's own solution. Every generated project gets a renamed copy, so it must never reference `cli/`.

## Testing changes locally

The CLI reads templates from git, not from your working folder, so **commit** before testing. Point it at your local clone so you don't have to push:

```powershell
$env:CLEANSLICES_TEMPLATE_REPO = "C:\path\to\clean-slices"

cd $env:TEMP
dotnet run --project C:\path\to\clean-slices\cli\CleanSlices.Cli -- new Acme.Test --samples -y
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

## How patterns work

A pattern is a branch named `pattern/<name>`, based on `main`, whose commits add the pattern. The CLI lists every `pattern/*` branch it finds, and shows the subject of the branch's first commit (minus a `type(scope): ` prefix) as the description. Later commits on the branch (fixes, docs) don't change it.

To build a solution with patterns, the CLI checks out the template commit the solution came from (recorded in `.cleanslices.json`), cherry-picks each pattern branch's own commits on top, and renders the result. `add pattern` renders the solution before and after the pattern and merges the difference into the user's code with `git merge-tree`.

### Adding a pattern

1. Branch from `main`: `git switch -c pattern/<name> main`.
2. Make the change in the project template **and** in `templates/cs-feature/`, so features generated in a solution with the pattern match it.
3. Commit with a subject that reads well in a list, e.g. `feat(pattern): Result<T> for expected failures`.
4. Check that both a fresh solution and a solution with the pattern added later build:
   ```powershell
   cleanslices new Acme.A --samples --pattern <name> -y
   cleanslices new Acme.B --samples -y; cd Acme.B; cleanslices add feature Product -y; git add -A; git commit -m wip; cleanslices add pattern <name> -y
   ```
5. Push the branch: `git push -u origin pattern/<name>`. It shows up in the CLI right away.

A pattern that *replaces* something on `main` can be made by reverting a commit: `pattern/result` is a revert of the commit on `main` that removed `Result<T>`.

### Keeping patterns healthy

- After changing `main`, rebase each pattern onto it (`git switch pattern/<name>; git rebase main`), resolve any conflicts as the rebase stops on each commit, and push with `git push --force-with-lease`. Only the pattern's own commits are applied, so a stale branch still works as long as it applies cleanly.
- **Don't merge `main` into a pattern branch.** The CLI cherry-picks the pattern's own commits (everything after its fork point from `main`), so the branch must stay a straight stack of commits on top of `main`. A merge commit can't be cherry-picked, and conflict fixes made in it never reach the CLI.
- **Why the force-push:** rebasing gives the pattern's commits new IDs, so GitHub still has the old ones and a normal push is rejected. `--force-with-lease` replaces them only if the branch on GitHub is still what you last fetched; if someone pushed to it since, the push is refused instead of silently erasing their commits. Avoid plain `--force`, which skips that check. The check compares against your last fetch, so a fetch (yours or your IDE's) counts as having seen whatever it brought in. Before force-pushing, run `git status`: if the branch is `behind` by more than the commits you rebased, look at the extra ones first.
- Patterns should add files rather than edit shared ones where they can. Every edited line is a possible conflict with the user's code or with other patterns.
- Combinations are applied in the order the user chose them. If two patterns edit the same lines, the second one fails to apply, and the CLI reports it.

## Template rules worth knowing

- `sourceName` is `CleanSlices`, so every `CleanSlices` and `cleanslices` in file names and contents gets the user's name. Aspire's `Projects.CleanSlices_Api` and the resource name `cleanslices-api` have their own derived symbols in `template.json`.
- Sample-only code is wrapped in `//#if (samples)` … `//#endif` (C#) or `# #if (samples)` (in `.http` files). Using comment syntax keeps the template solution itself compiling.
- New features must be additive: no `DbSet` (use `context.Set<T>()` and `ToTable(...)`), and no registration edits (endpoints and handlers are found by reflection).
- The CLI's default template repository is `TemplateRepository.DefaultSource` in `cli/CleanSlices.Cli/Infrastructure/TemplateRepository.cs`. Update it if the repository moves.
