#if (cqrs && handlers)
namespace CleanSlices.Api.Configuration.Cqrs;

// The names and shapes of the Mediator package, so slices read the same with either dispatcher.
public readonly record struct Unit
{
	public static readonly Unit Value;
}

public interface ICommand<out TResponse>;

public interface ICommand : ICommand<Unit>;

public interface IQuery<out TResponse>;

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
	public ValueTask<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit> where TCommand : ICommand<Unit>;

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
	public ValueTask<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}

public interface ISender
{
	public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

	public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
#endif
