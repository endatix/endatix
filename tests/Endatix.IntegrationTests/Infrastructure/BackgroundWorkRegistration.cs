using Endatix.Modules.Audience.Features.Imports;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

/// <summary>
/// Test hosts start with their <see cref="BackgroundWork"/> off: the outbox relay asks a switch that is off, the jobs
/// scheduler is built but not started, and the daily import sweep is not registered.
/// </summary>
internal static class BackgroundWorkRegistration
{
    /// <summary>Registers the switches. Runs after the application's own registrations, whose relay gate it wraps.</summary>
    public static void AddSwitchedOff(IServiceCollection services)
    {
        services.AddSingleton<OutboxRelaySwitch>();
        services.AddSingleton<JobsSchedulerSwitch>();
        services.AddSingleton<BackgroundWork>();
        SwitchOutboxRelay(services);

        // First, so it runs before the relay starts polling.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, SwitchedOutboxRelayCheck>());

        // Only the hosted service reads the option from here, to decide whether to start the scheduler it built. The
        // scheduler's thread pool is sized from configuration, which still says the host runs jobs, so a test can
        // start it later.
        services.PostConfigure<BackgroundJobsOptions>(options => options.RunInProcess = false);

        // Its first sweep comes minutes after start, in whichever test is running then. Tests run the sweep themselves.
        services.AddSingleton(new RemovedHostedServices(ImportSweeps: RemoveHostedService<ImportSweepService>(services)));
    }

    private static void SwitchOutboxRelay(IServiceCollection services)
    {
        var own = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IOutboxRelayGate) && !descriptor.IsKeyedService);
        if (own is null)
        {
            return;
        }

        services.Remove(own);
        services.AddScoped<IOutboxRelayGate>(provider =>
            new SwitchedOutboxRelayGate(Create(own, provider), provider.GetRequiredService<OutboxRelaySwitch>()));
    }

    private static IOutboxRelayGate Create(ServiceDescriptor own, IServiceProvider provider) =>
        (IOutboxRelayGate)(own.ImplementationInstance
            ?? own.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, own.ImplementationType!));

    private static int RemoveHostedService<THostedService>(IServiceCollection services)
        where THostedService : IHostedService
    {
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationType == typeof(THostedService))
            .ToList();
        foreach (var descriptor in registrations)
        {
            services.Remove(descriptor);
        }

        return registrations.Count;
    }
}

/// <summary>How many registrations of each hosted service the test host took out, so a test can see the removal still applies.</summary>
/// <param name="ImportSweeps">Registrations of the daily import sweep.</param>
internal sealed record RemovedHostedServices(int ImportSweeps);

/// <summary>
/// Fails the host's start when its relay would not ask the switch, as when a later registration replaced the relay
/// gate the switch wraps: that relay would run whatever the test asked for.
/// </summary>
internal sealed class SwitchedOutboxRelayCheck(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (scope.ServiceProvider.GetService<IOutboxRelayGate>() is { } gate and not SwitchedOutboxRelayGate)
        {
            throw new InvalidOperationException(
                $"The test host's outbox relay gate is {gate.GetType().Name}, not the switched one: a registration made after the test host's replaced it.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
