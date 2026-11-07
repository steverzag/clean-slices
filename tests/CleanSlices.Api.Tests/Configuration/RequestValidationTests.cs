#if (validation)
using System.Reflection;
using CleanSlices.Api.Configuration.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Configuration;

public class RequestValidationTests
{
	public sealed record Probe(string Name);

	public sealed class ProbeValidator : AbstractValidator<Probe>
	{
		public ProbeValidator() => RuleFor(p => p.Name).NotEmpty().WithMessage("Name cannot be empty.");
	}

	private static readonly IServiceProvider Services = new ServiceCollection()
		.AddScoped<IValidator<Probe>, ProbeValidator>()
		.BuildServiceProvider();

	[Fact]
	public async Task An_invalid_argument_gets_the_domains_validation_response_and_the_handler_doesnt_run()
	{
		var (result, handlerRan) = await InvokeAsync(nameof(Validated), new Probe(""), 7);

		var problem = Assert.IsType<HttpValidationProblemDetails>(Assert.IsType<ProblemHttpResult>(result).ProblemDetails);
		Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
		Assert.Equal(RequestValidation.Title, problem.Title);
		Assert.Equal(RequestValidation.Detail, problem.Detail);
		Assert.Equal(["Name cannot be empty."], problem.Errors["Name"]);
		Assert.False(handlerRan);
	}

	[Fact]
	public async Task A_valid_argument_reaches_the_handler()
	{
		var (_, handlerRan) = await InvokeAsync(nameof(Validated), new Probe("Ana"), 7);

		Assert.True(handlerRan);
	}

	[Fact]
	public void An_endpoint_without_validated_arguments_gets_no_filter()
	{
		EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(null);

		Assert.Same(next, RequestValidation.Factory(Context(nameof(NotValidated)), next));
	}

	private static Task<IResult> Validated(Probe probe, int other) => Task.FromResult(Results.Ok());

	private static Task<IResult> NotValidated(int value) => Task.FromResult(Results.Ok());

	private static async Task<(object? Result, bool HandlerRan)> InvokeAsync(string method, params object?[] arguments)
	{
		var handlerRan = false;
		var filter = RequestValidation.Factory(Context(method), _ =>
		{
			handlerRan = true;
			return ValueTask.FromResult<object?>(Results.Ok());
		});

		var httpContext = new DefaultHttpContext { RequestServices = Services };
		var result = await filter(new DefaultEndpointFilterInvocationContext(httpContext, arguments));
		return (result, handlerRan);
	}

	private static EndpointFilterFactoryContext Context(string method) => new()
	{
		MethodInfo = typeof(RequestValidationTests).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!,
		ApplicationServices = Services,
	};
}
#endif
