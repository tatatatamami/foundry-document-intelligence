var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();
await host.StartAsync();
await host.StopAsync();
