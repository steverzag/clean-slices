#if (cqrs && mediator)
global using Mediator;
#elif (cqrs)
global using CleanSlices.Api.Configuration.Cqrs;
#endif
