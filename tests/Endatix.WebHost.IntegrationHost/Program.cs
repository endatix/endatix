using Endatix.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureEndatix();

WebApplication app = builder.Build();

app.UseEndatix();

app.Run();

public partial class Program { }
