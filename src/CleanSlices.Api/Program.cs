using CleanSlices.Api.Configuration;
using CleanSlices.Api.Configuration.Endpoints;
#if (cqrs)
using CleanSlices.Api.Configuration.Cqrs;
#endif
#if (idempotency)
using CleanSlices.Api.Configuration.Idempotency;
#endif
#if (validation)
using CleanSlices.Api.Configuration.Validation;
#endif
using CleanSlices.Api.Handlers;
using CleanSlices.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();

builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddProblemDetails(options =>
	options.CustomizeProblemDetails = ProblemDetailsCustomization.Apply);

builder.Services.ConfigureHttpJsonOptions(options =>
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
#if (validation)
builder.Services.AddRequestValidation();
#endif
#if (idempotency)
builder.Services.AddIdempotency();
#endif
#if (cqrs)
builder.Services.AddCqrs();
#endif

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
