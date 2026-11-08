#if (integration_events)
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Infrastructure.IntegrationEvents;

internal sealed record HandlerInvocation(IntegrationEventMessage Message, string Handler, IServiceProvider Services);

internal interface IIntegrationEventHandlerFilter
{
	public Task Invoke(HandlerInvocation invocation, Func<Task> next, CancellationToken ct);
}

// Runs every handler of a message, whatever delivered it: in process, or a broker's consumer.
internal sealed class IntegrationEventHandlers(IServiceScopeFactory scopes)
{
	private static readonly ConcurrentDictionary<Type, Func<object, IIntegrationEvent, CancellationToken, Task>> Invokers = [];

	public async Task RunAsync(IntegrationEventMessage message, CancellationToken ct)
	{
		var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(message.Event.GetType());

		// The handler types, from a throwaway scope; each one then runs in a scope of its own.
		IReadOnlyList<Type> handlers;
		using (var probe = scopes.CreateScope())
		{
			handlers = [.. probe.ServiceProvider.GetServices(handlerType).Select(h => h!.GetType())];
		}

		var failures = new List<Exception>();
		foreach (var handler in handlers)
		{
			try
			{
				await Invoke(message, handlerType, handler, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
			{
				failures.Add(ex);
			}
		}

		if (failures.Count > 0)
		{
			throw new AggregateException($"{failures.Count} of {handlers.Count} handler(s) failed for integration event {message.Id}.", failures);
		}
	}

	private async Task Invoke(IntegrationEventMessage message, Type handlerType, Type handler, CancellationToken ct)
	{
		await using var scope = scopes.CreateAsyncScope();
		var instance = scope.ServiceProvider.GetServices(handlerType).First(h => h!.GetType() == handler)!;
		var invoke = Invokers.GetOrAdd(message.Event.GetType(), CreateInvoker);

		Func<Task> next = () => invoke(instance, message.Event, ct);
		var invocation = new HandlerInvocation(message, handler.FullName ?? handler.Name, scope.ServiceProvider);
		foreach (var filter in scope.ServiceProvider.GetServices<IIntegrationEventHandlerFilter>().Reverse())
		{
			var inner = next;
			next = () => filter.Invoke(invocation, inner, ct);
		}

		await next();
	}

	private static Func<object, IIntegrationEvent, CancellationToken, Task> CreateInvoker(Type eventType) =>
		(Func<object, IIntegrationEvent, CancellationToken, Task>)typeof(IntegrationEventHandlers)
			.GetMethod(nameof(InvokeTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
			.MakeGenericMethod(eventType)
			.CreateDelegate(typeof(Func<object, IIntegrationEvent, CancellationToken, Task>));

	private static Task InvokeTyped<TEvent>(object handler, IIntegrationEvent integrationEvent, CancellationToken ct)
		where TEvent : IIntegrationEvent =>
		((IIntegrationEventHandler<TEvent>)handler).Handle((TEvent)integrationEvent, ct);
}
#endif
