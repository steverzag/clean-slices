using CleanSlices.Api.Configuration.Endpoints;
using CleanSlices.Api.Handlers;
using CleanSlices.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();

builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddProblemDetails(options =>
	options.CustomizeProblemDetails = context =>
	{
		context.ProblemDetails.Instance =
			$"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
		context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
		var activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
		context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
	}
);

builder.Services.ConfigureHttpJsonOptions(options =>
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.RegisterEndpoints();

await app.RunAsync();
