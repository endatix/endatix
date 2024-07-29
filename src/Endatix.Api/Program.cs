using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;
using Serilog.Extensions.Logging;
using Themes = Serilog.Sinks.SystemConsole.Themes;
using Endatix.Core.Configuration;
using Endatix.SqlServer;
using Endatix.Api.Infrastructure;
using Endatix.Infrastructure.Email;
using Endatix.Samples.CustomEventHandlers;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Async(wt => wt.Console(theme: Themes.AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput: true))
    .CreateLogger();

Log.Information("Starting web host");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(context.Configuration));

    var logger = new SerilogLoggerFactory(Log.Logger)
         .CreateLogger<Program>();

    // Add services to the container.
    builder.Services.AddEndatix(configuration => configuration
                    .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                    .UseSnowflakeIds(0)
                    .UseSampleData()
                    .UseCustomTablePrefix());

    builder.Services.AddEndatixInfrastructure(configuration => configuration
                    .AddSecurityServices(options => options
                        .AddApiAuthentication(builder.Configuration)
                        .ReadDevUsersFromConfig()
                    ));

    builder.Services
                .AddCors()
                .AddFastEndpoints()
                .SwaggerDocument(o =>
                    {
                        o.ShortSchemaNames = true;
                        o.DocumentSettings = s =>
                        {
                            s.Version = "v0";
                            s.DocumentName = "Internal MVP (Alpha) Release";
                            s.Title = "Endatix API";
                        };
                    });

    builder.Services.AddContactUsFormOptions();
    builder.Services.AddMediatRInfrastructure(options =>
                    {
                        options.UsePipelineLogging();
                        options.AdditionalAssemblies =
                            [
                                Endatix.Samples.CustomEventHandlers.AssemblyReference.Assembly
                            ];
                    });

    builder.Services.AddEmailSender<SendGridEmailSender, SendGridSettings>();

    var app = builder.Build();

    app.UseDefaultExceptionHandler(logger, true, true);
    app.UseHsts();

    app.UseAuthentication()
        .UseAuthorization();

    app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod())
        .UseFastEndpoints(fastEndpoints =>
            {
                fastEndpoints.Versioning.Prefix = "v";
                fastEndpoints.Endpoints.RoutePrefix = "api";
                fastEndpoints.Serializer.Options.Converters.Add(new LongToStringConverter());
            })
        .UseSwaggerGen();

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
}
finally
{
    Log.CloseAndFlush();
}