using Endatix.Core.Abstractions.Exporting;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Setup;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddExportTransformer_ShouldRegisterTransformerInEnumerable()
    {
        var services = new ServiceCollection();

        services.AddExportTransformer<FirstTransformer>();

        using ServiceProvider provider = services.BuildServiceProvider();

        List<IValueTransformer> transformers = provider.GetServices<IValueTransformer>().ToList();

        Assert.Single(transformers);
        Assert.IsType<FirstTransformer>(transformers[0]);
    }

    [Fact]
    public void AddExportTransformer_ShouldPreserveRegistrationOrder()
    {
        var services = new ServiceCollection();

        services.AddExportTransformer<FirstTransformer>();
        services.AddExportTransformer<SecondTransformer>();

        using ServiceProvider provider = services.BuildServiceProvider();

        List<IValueTransformer> transformers = provider.GetServices<IValueTransformer>().ToList();

        Assert.Equal(2, transformers.Count);
        Assert.IsType<FirstTransformer>(transformers[0]);
        Assert.IsType<SecondTransformer>(transformers[1]);
    }

    [Fact]
    public void AddExportTransformer_ShouldBeIdempotent_ForSameTransformerType()
    {
        var services = new ServiceCollection();

        services.AddExportTransformer<FirstTransformer>();
        services.AddExportTransformer<FirstTransformer>();

        using ServiceProvider provider = services.BuildServiceProvider();

        List<IValueTransformer> transformers = provider.GetServices<IValueTransformer>().ToList();

        Assert.Single(transformers);
        Assert.IsType<FirstTransformer>(transformers[0]);
    }

    private sealed class FirstTransformer : IValueTransformer
    {
        public object? Transform<T>(object? value, TransformationContext<T> context) => value;
    }

    private sealed class SecondTransformer : IValueTransformer
    {
        public object? Transform<T>(object? value, TransformationContext<T> context) => value;
    }
}

