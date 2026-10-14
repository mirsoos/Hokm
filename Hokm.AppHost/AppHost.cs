var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Hokm_Presentation>("hokm-presentation");

builder.Build().Run();
