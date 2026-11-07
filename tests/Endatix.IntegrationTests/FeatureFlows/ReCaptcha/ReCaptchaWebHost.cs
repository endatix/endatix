extern alias EndatixWebHost;

using System.Collections.Concurrent;
using System.Net;
using Endatix.Infrastructure.ReCaptcha;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// A web host on the collection's database whose siteverify calls are answered with one recorded
/// Google response body, so a public completion runs the real pipeline without calling Google.
/// reCAPTCHA stays off until <see cref="Enable"/> names the seeded tenant.
/// </summary>
internal sealed class ReCaptchaWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private const string ReCaptchaCategory = "Endatix.Infrastructure.ReCaptcha";

    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;
    private readonly ConcurrentQueue<Uri?> _siteverifyRequests = new();
    private ReCaptchaOptions _options = new();

    public ReCaptchaWebHost(EndatixIntegrationWebHostFixture fixture, string siteverifyBody)
    {
        Database = fixture.Database;
        _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting($"Logging:LogLevel:{ReCaptchaCategory}", "Information");
                builder.ConfigureTestServices(services =>
                {
                    services.AddHttpClient<IReCaptchaHttpClient, ReCaptchaHttpClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => new RecordedSiteverifyHandler(siteverifyBody, _siteverifyRequests));
                    services.AddSingleton<IOptions<ReCaptchaOptions>>(new CurrentOptions(() => _options));
                    services.AddSingleton<ILoggerProvider>(Events);
                });
            });

        // Reading Services starts the host; a failed start would otherwise leave it running.
        try
        {
            Seed = new IntegrationSeedBuilder(_factory.Services);
        }
        catch
        {
            _factory.Dispose();
            throw;
        }
    }

    public DatabaseInfrastructureFixture Database { get; }

    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    public IServiceProvider Services => _factory.Services;

    public IntegrationSeedBuilder Seed { get; }

    /// <summary>The event ids the reCAPTCHA code logged, in order.</summary>
    public ReCaptchaEventLog Events { get; } = new(ReCaptchaCategory);

    /// <summary>The siteverify requests the host made; none of them left the process.</summary>
    public IReadOnlyCollection<Uri?> SiteverifyRequests => _siteverifyRequests;

    public HttpClient CreateClient() => _factory.CreateClient();

    public void Enable(long tenantId, double minimumScore) => _options = new ReCaptchaOptions
    {
        IsEnabled = true,
        SecretKey = "recorded-response",
        MinimumScore = minimumScore,
        EnabledForTenantIds = [tenantId]
    };

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    /// <summary>Read on every resolve, so a test can switch reCAPTCHA on after it seeds the tenant.</summary>
    private sealed class CurrentOptions(Func<ReCaptchaOptions> current) : IOptions<ReCaptchaOptions>
    {
        public ReCaptchaOptions Value => current();
    }

    private sealed class RecordedSiteverifyHandler(string body, ConcurrentQueue<Uri?> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Enqueue(request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}

/// <summary>Records the level and event id of every log entry under one category prefix.</summary>
internal sealed class ReCaptchaEventLog(string categoryPrefix) : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, int EventId)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, int EventId)> Entries => _entries;

    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(categoryPrefix, StringComparison.Ordinal) ? new Recorder(_entries) : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class Recorder(ConcurrentQueue<(LogLevel, int)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue((logLevel, eventId.Id));
    }
}
