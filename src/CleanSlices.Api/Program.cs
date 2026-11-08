using CleanSlices.Api.Configuration;
#if (authentication)
using CleanSlices.Api.Configuration.Authentication;
#endif
#if (entra)
using CleanSlices.Api.Configuration.Authentication.Entra;
#endif
#if (identity)
using CleanSlices.Api.Configuration.Authentication.Identity;
#endif
#if (keycloak)
using CleanSlices.Api.Configuration.Authentication.Keycloak;
#endif
using CleanSlices.Api.Configuration.Endpoints;
#if (cqrs)
using CleanSlices.Api.Configuration.Cqrs;
#endif
#if (idempotency)
using CleanSlices.Api.Configuration.Idempotency;
#endif
#if (signalr)
using CleanSlices.Api.Configuration.Realtime;
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
#if (keycloak)
builder.AddKeycloakAuthentication();
#elif (entra)
builder.AddEntraAuthentication();
#elif (identity)
builder.AddIdentityAuthentication();
#endif
#if (signalr)
builder.AddRealtime();
#endif

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
#if (authentication)
    app.MapOpenApi().AllowAnonymous();
#else
    app.MapOpenApi();
#endif
}

app.UseHttpsRedirection();
#if (authentication)
// After the status code pages, so 401 and 403 get a ProblemDetails body.
#if (signalr)
app.UseHubAccessTokens();
#endif
app.UseAuthentication();
app.UseAuthorization();
#endif
app.RegisterEndpoints();
#if (identity)
app.MapIdentityEndpoints();
#endif
#if (signalr)
app.MapRealtime();
#endif

await app.RunAsync();
