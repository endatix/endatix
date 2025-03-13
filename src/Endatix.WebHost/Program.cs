using Endatix.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureEndatix();

var app = builder.Build();

app.UseEndatix();

app.Run();
