#if (validation)
using FluentValidation;

namespace CleanSlices.Api.Configuration.Validation;

public static class RequestValidation
{
	// The same response as the domain's own validation, so clients can't tell which one rejected a request.
	public const string Title = "Validation failed";
	public const string Detail = "One or more validation errors occurred.";

	extension(IServiceCollection services)
	{
		public IServiceCollection AddRequestValidation() =>
			services.AddValidatorsFromAssembly(typeof(RequestValidation).Assembly, includeInternalTypes: true);
	}

	// Added once to every endpoint: an endpoint with no validated argument gets no filter at all.
	public static EndpointFilterDelegate Factory(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
	{
		var registered = context.ApplicationServices.GetRequiredService<IServiceProviderIsService>();
		var validated = context.MethodInfo.GetParameters()
			.Select((parameter, index) => (Index: index, Validator: typeof(IValidator<>).MakeGenericType(parameter.ParameterType)))
			.Where(argument => registered.IsService(argument.Validator))
			.ToList();

		if (validated.Count == 0)
		{
			return next;
		}

		return async invocation =>
		{
			var errors = new Dictionary<string, string[]>();
			foreach (var (index, validatorType) in validated)
			{
				if (invocation.Arguments[index] is not { } argument)
				{
					continue;
				}

				var validator = (IValidator)invocation.HttpContext.RequestServices.GetRequiredService(validatorType);
				var result = await validator.ValidateAsync(new ValidationContext<object>(argument), invocation.HttpContext.RequestAborted);
				foreach (var (field, messages) in result.ToDictionary())
				{
					errors[field] = [.. errors.GetValueOrDefault(field, []), .. messages];
				}
			}

			return errors.Count == 0
				? await next(invocation)
				: Results.ValidationProblem(errors, detail: Detail, title: Title, type: ProblemDetailsDefaults.RFC400Type);
		};
	}
}
#endif
