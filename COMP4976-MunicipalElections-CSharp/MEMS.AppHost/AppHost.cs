var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MEMS_Api>("api")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.MEMS_Admin>("admin")
    .WithReference(api)
    .WaitFor(api);

builder.AddProject<Projects.MEMS_Public>("public");

builder.Build().Run();
