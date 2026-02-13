using System.Text.Json.Nodes;
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

        using var provider = services.BuildServiceProvider();

        var transformers = provider.GetServices<IValueTransformer>().ToList();

        Assert.Single(transformers);
        Assert.IsType<FirstTransformer>(transformers[0]);
    }

    [Fact]
    public void AddExportTransformer_ShouldPreserveRegistrationOrder()
    {
        var services = new ServiceCollection();

        services.AddExportTransformer<FirstTransformer>();
        services.AddExportTransformer<SecondTransformer>();

        using var provider = services.BuildServiceProvider();

        var transformers = provider.GetServices<IValueTransformer>().ToList();

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

        using var provider = services.BuildServiceProvider();

        var transformers = provider.GetServices<IValueTransformer>().ToList();

        Assert.Single(transformers);
        Assert.IsType<FirstTransformer>(transformers[0]);
    }

    private sealed class FirstTransformer : IValueTransformer
    {
        public JsonNode? Transform<T>(JsonNode? node, TransformationContext<T> context) => node;
    }

    private sealed class SecondTransformer : IValueTransformer
    {
        public JsonNode? Transform<T>(JsonNode? node, TransformationContext<T> context) => node;
    }
}

