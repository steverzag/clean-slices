#if (cqrs && handlers)
using System.Collections.Concurrent;

namespace CleanSlices.Api.Configuration.Cqrs;

// Finds the handler of a command or query in the request's services and calls it.
internal sealed class Sender(IServiceProvider services) : ISender
{
	public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
		Dispatch<TResponse>(typeof(CommandDispatcher<,>), command, cancellationToken);

	public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
		Dispatch<TResponse>(typeof(QueryDispatcher<,>), query, cancellationToken);

	private ValueTask<TResponse> Dispatch<TResponse>(Type dispatcher, object message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// One small typed dispatcher per message type, made once, so calls after the first need no reflection.
		var typed = Dispatchers<TResponse>.ByMessage.GetOrAdd(message.GetType(), type =>
			(IDispatcher<TResponse>)Activator.CreateInstance(dispatcher.MakeGenericType(type, typeof(TResponse)))!);

		return typed.Handle(message, services, cancellationToken);
	}

	private static class Dispatchers<TResponse>
	{
		public static readonly ConcurrentDictionary<Type, IDispatcher<TResponse>> ByMessage = new();
	}

	private interface IDispatcher<TResponse>
	{
		public ValueTask<TResponse> Handle(object message, IServiceProvider services, CancellationToken cancellationToken);
	}

	private sealed class CommandDispatcher<TCommand, TResponse> : IDispatcher<TResponse> where TCommand : ICommand<TResponse>
	{
		public ValueTask<TResponse> Handle(object message, IServiceProvider services, CancellationToken cancellationToken) =>
			services.GetRequiredService<ICommandHandler<TCommand, TResponse>>().Handle((TCommand)message, cancellationToken);
	}

	private sealed class QueryDispatcher<TQuery, TResponse> : IDispatcher<TResponse> where TQuery : IQuery<TResponse>
	{
		public ValueTask<TResponse> Handle(object message, IServiceProvider services, CancellationToken cancellationToken) =>
			services.GetRequiredService<IQueryHandler<TQuery, TResponse>>().Handle((TQuery)message, cancellationToken);
	}
}
#endif
