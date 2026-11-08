#if (after_commit)
using CleanSlices.Infrastructure.Persistence.AfterCommit;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanSlices.Api.Tests.Persistence;

public class AfterCommitTests
{
	private readonly AfterCommitActions _actions = new();
	private readonly List<string> _ran = [];

	private AfterCommitStep Step => new(_actions, NullLogger<AfterCommitStep>.Instance);

	[Fact]
	public async Task Queued_actions_run_after_the_commit_in_order()
	{
		Enqueue("first");
		Enqueue("second");
		Assert.Empty(_ran);

		await Step.AfterCommitAsync(null!, TestContext.Current.CancellationToken);

		Assert.Equal(["first", "second"], _ran);
	}

	[Fact]
	public async Task Queued_actions_are_dropped_when_the_save_fails()
	{
		Enqueue("dropped");

		await Step.SaveFailedAsync(null!, TestContext.Current.CancellationToken);
		await Step.AfterCommitAsync(null!, TestContext.Current.CancellationToken);

		Assert.Empty(_ran);
	}

	[Fact]
	public async Task A_failing_action_doesnt_stop_the_others()
	{
		_actions.Enqueue(_ => throw new InvalidOperationException("Cache unavailable"));
		Enqueue("after the failure");

		await Step.AfterCommitAsync(null!, TestContext.Current.CancellationToken);

		Assert.Equal(["after the failure"], _ran);
	}

	[Fact]
	public async Task Actions_run_once_even_when_the_context_saves_again()
	{
		Enqueue("once");

		await Step.AfterCommitAsync(null!, TestContext.Current.CancellationToken);
		await Step.AfterCommitAsync(null!, TestContext.Current.CancellationToken);

		Assert.Equal(["once"], _ran);
	}

	private void Enqueue(string name) => _actions.Enqueue(_ =>
	{
		_ran.Add(name);
		return Task.CompletedTask;
	});
}
#endif
