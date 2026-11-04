using CleanSlices.Api.Configuration;
using CleanSlices.Api.Configuration.Endpoints;
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
