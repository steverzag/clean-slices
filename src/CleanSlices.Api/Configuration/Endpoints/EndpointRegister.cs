namespace CleanSlices.Api.Configuration.Endpoints;

public static class EndpointRegister
{
	extension(IEndpointRouteBuilder app)
	{ 
		public void RegisterEndpoints()
		{
			static bool IsAssignableToIEndpoint(Type t) =>
				!t.IsAbstract
				&& !t.IsInterface
				&& typeof(IEndpoint).IsAssignableFrom(t);

			var endpointAssembly = typeof(IEndpoint).Assembly;
			var endpointTypes = endpointAssembly.GetTypes()
				.Where(IsAssignableToIEndpoint);

			List<string> failedImplementations = [];

			foreach (var t in endpointTypes)
			{
				var ctor = t.GetConstructor(Type.EmptyTypes);

				if(ctor is null)
				{
					failedImplementations.Add($"{t.Name}: IEndpoint implementing classes must present an empty constructor");
					continue;
				}

				var appEndpoint = (IEndpoint)Activator.CreateInstance(t)!;
				appEndpoint.Register(app);
			}

			if(failedImplementations.Count > 0)
			{
				throw new EndpointsRegisterFailedException(
					"Some IEndpoint implementations failed to register",
					failedImplementations);
			}
		}
	}

	public class EndpointsRegisterFailedException(string message, IEnumerable<string> failingImplementations) : Exception(message)
	{
		internal IEnumerable<string> FailingImplementations { get; set; } = failingImplementations;
	}
}
