using Endatix.Hosting;
using Endatix.Modules.Reporting;

var builder = WebApplication.CreateBuilder(args);

// Modules are registered by the host, not by Endatix.Hosting, so the hosting library stays
// independent of them. UseModule honours Endatix:FeatureFlags:ReportingModule.
builder.Host.ConfigureEndatixWithDefaults(endatix => endatix.UseModule(ReportingModule.Instance));

var app = builder.Build();

app.UseEndatix();

app.Run();
