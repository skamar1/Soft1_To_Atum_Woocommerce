var builder = DistributedApplication.CreateBuilder(args);

var workerService = builder.AddProject<Projects.Soft1_To_Atum_Worker>("worker");

var apiService = builder.AddProject<Projects.Soft1_To_Atum_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Soft1_To_Atum_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.Soft1_To_Atum_Blazor>("blazor")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
