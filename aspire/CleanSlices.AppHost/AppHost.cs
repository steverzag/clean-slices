var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CleanSlices_Api>("cleanslices-api");

builder.Build().Run();
