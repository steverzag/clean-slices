#if (cqrs && handlers)
using CleanSlices.Api.Configuration.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Configuration;

public class SenderTests
{
	public sealed record Double(int Value) : ICommand<int>;

	public sealed record Remember(string Value) : ICommand;

	public sealed record Echo(string Value) : IQuery<string>;

	public sealed record Unhandled : ICommand;

	private sealed class DoubleHandler : ICommandHandler<Double, int>
	{
		public ValueTask<int> Handle(Double command, CancellationToken cancellationToken) => ValueTask.FromResult(command.Value * 2);
	}

	private sealed class RememberHandler(List<string> memory) : ICommandHandler<Remember>
	{
		public ValueTask<Unit> Handle(Remember command, CancellationToken cancellationToken)
		{
			memory.Add(command.Value);
			return ValueTask.FromResult(Unit.Value);
		}
	}

	private sealed class EchoHandler : IQueryHandler<Echo, string>
	{
		public ValueTask<string> Handle(Echo query, CancellationToken cancellationToken) => ValueTask.FromResult(query.Value);
	}

	private readonly List<string> _memory = [];

	[Fact]
	public async Task A_command_reaches_its_handler_and_returns_its_response()
	{
		Assert.Equal(42, await Sender().Send(new Double(21), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task A_command_without_a_response_runs_its_handler()
	{
		var response = await Sender().Send(new Remember("Ana"), TestContext.Current.CancellationToken);

		Assert.Equal(Unit.Value, response);
		Assert.Equal(["Ana"], _memory);
	}

	[Fact]
	public async Task A_query_reaches_its_handler()
	{
		Assert.Equal("Ana", await Sender().Send(new Echo("Ana"), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task A_message_without_a_handler_fails_naming_the_handler()
	{
		var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await Sender().Send(new Unhandled(), TestContext.Current.CancellationToken));

		Assert.Contains(nameof(Unhandled), error.Message);
	}

	private ISender Sender() => new ServiceCollection()
		.AddCqrs()
		.AddSingleton(_memory)
		.AddScoped<ICommandHandler<Double, int>, DoubleHandler>()
		.AddScoped<ICommandHandler<Remember, Unit>, RememberHandler>()
		.AddScoped<IQueryHandler<Echo, string>, EchoHandler>()
		.BuildServiceProvider()
		.CreateScope().ServiceProvider
		.GetRequiredService<ISender>();
}
#endif
