#if (saasManagement)
using Endatix.SaaS.Management;
#endif
using Endatix.Hosting;

var builder = WebApplication.CreateBuilder(args);

#if (saasManagement)
builder.Host.ConfigureEndatixWithDefaults(endatix =>
{
    endatix.UseModule(SaaSManagementModule.Instance);
});
#else
builder.Host.ConfigureEndatix();
#endif

var app = builder.Build();

app.UseEndatix();

await app.RunAsync();
