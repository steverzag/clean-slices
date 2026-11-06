namespace CleanSlices.Cli.Infrastructure;

internal sealed record FileChange(char Status, string Path);

internal sealed record MergeOutcome(IReadOnlyList<FileChange> Changes, IReadOnlyList<string> ConflictedFiles)
{
    public bool HasConflicts => ConflictedFiles.Count > 0;
}

/// <summary>
/// Applies "template before → template after" onto the user's code with a real three-way merge:
/// both renders become commits (outside any branch), and <c>git merge-tree --merge-base</c> merges
/// the after-render into HEAD using the before-render as the common ancestor.
/// User edits are preserved; overlapping edits surface as normal conflict markers.
/// </summary>
internal static class SolutionMerger
{
    // Identity for the synthetic, unreferenced base/target commits; the user's own commit uses their identity.
    private static readonly Dictionary<string, string> SyntheticIdentity = new()
    {
        ["GIT_AUTHOR_NAME"] = "cleanslices",
        ["GIT_AUTHOR_EMAIL"] = "cli@cleanslices.invalid",
        ["GIT_COMMITTER_NAME"] = "cleanslices",
        ["GIT_COMMITTER_EMAIL"] = "cli@cleanslices.invalid",
    };

    /// <returns>An error message when the solution isn't a git repository with a clean working tree, otherwise null.</returns>
    public static async Task<string?> CheckRepositoryAsync(string root, CancellationToken cancellationToken)
    {
        var head = await Git.RunAsync(root, ["rev-parse", "--verify", "HEAD"], cancellationToken);
        if (!head.Succeeded)
        {
            return "Adding an option needs a git repository with at least one commit (run `git init` and commit first).";
        }

        var status = await Git.RunOrThrowAsync(root, ["status", "--porcelain"], cancellationToken);
        return status.Length > 0
            ? "The working tree has uncommitted changes. Commit or stash them first, so the option's changes can be reviewed (and undone) on their own."
            : null;
    }

    public static async Task<MergeOutcome> MergeAsync(string root, string baseRender, string targetRender, CancellationToken cancellationToken)
    {
        var gitDir = await Git.RunOrThrowAsync(root, ["rev-parse", "--absolute-git-dir"], cancellationToken);

        var baseTree = await WriteTreeAsync(gitDir, baseRender, cancellationToken);
        var targetTree = await WriteTreeAsync(gitDir, targetRender, cancellationToken);

        var baseCommit = await Git.RunOrThrowAsync(
            root, ["commit-tree", baseTree, "-m", "cleanslices: template before"], cancellationToken, SyntheticIdentity);
        var targetCommit = await Git.RunOrThrowAsync(
            root, ["commit-tree", targetTree, "-p", baseCommit, "-m", "cleanslices: template after"], cancellationToken, SyntheticIdentity);

        // Exit code 0 = clean merge, 1 = conflicts (the tree then contains files with conflict markers).
        var merge = await Git.RunAsync(
            root, ["merge-tree", "--write-tree", "--name-only", "--merge-base", baseCommit, "HEAD", targetCommit], cancellationToken);
        if (merge.ExitCode is not (0 or 1))
        {
            throw new ToolException("git merge-tree failed.", merge.Output);
        }

        // Output: <tree>\n[<conflicted path>\n...]\n\n<informational messages>
        var lines = merge.StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var mergedTree = lines[0].Trim();
        var conflicted = merge.ExitCode == 1
            ? lines.Skip(1).TakeWhile(line => line.Length > 0).Distinct().ToList()
            : [];

        var diff = await Git.RunOrThrowAsync(root, ["diff", "--name-status", "--no-renames", "HEAD", mergedTree], cancellationToken);
        var changes = diff
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t', 2))
            .Select(parts => new FileChange(parts[0][0], parts[1]))
            .ToList();

        // Two-tree read-tree moves index + working tree from HEAD to the merged tree (adds, edits and deletes).
        await Git.RunOrThrowAsync(root, ["read-tree", "-u", "-m", "HEAD", mergedTree], cancellationToken);

        return new MergeOutcome(changes, conflicted);
    }

    /// <summary>Stores a rendered directory as a tree object in the user's repository via a temporary index.</summary>
    private static async Task<string> WriteTreeAsync(string gitDir, string renderDirectory, CancellationToken cancellationToken)
    {
        var indexFile = Path.Combine(Path.GetTempPath(), "cleanslices", $"index-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.GetDirectoryName(indexFile)!);
        var environment = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = indexFile };
        string[] repository = [$"--git-dir={gitDir}", $"--work-tree={renderDirectory}"];

        try
        {
            await Git.RunOrThrowAsync(renderDirectory, [.. repository, "add", "-A", "."], cancellationToken, environment);
            return await Git.RunOrThrowAsync(renderDirectory, [.. repository, "write-tree"], cancellationToken, environment);
        }
        finally
        {
            File.Delete(indexFile);
        }
    }
}
