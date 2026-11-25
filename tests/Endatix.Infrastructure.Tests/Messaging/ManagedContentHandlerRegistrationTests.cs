using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases;
using Endatix.Core.UseCases.CustomQuestions.List;
using Endatix.Infrastructure.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Messaging;

public class ManagedContentHandlerRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Registered_handlers_never_select_the_direct_call_adapter(bool alreadyScanned)
    {
        var services = new ServiceCollection();
        if (alreadyScanned)
        {
            services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ListCustomQuestionsHandler).Assembly));
            services.AddTransient<IRequestHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>, ListCustomQuestionsHandler>();
        }
        services.AddMediatRMessaging();
        var types = typeof(ListCustomQuestionsHandler).Assembly.DefinedTypes;
        var adapters = types.Where(type => type.IsDefined(typeof(DirectCallOnlyAttribute), false)).ToHashSet<Type>();
        var registeredOwners = types.Where(type => type.BaseType is { IsGenericType: true } parent
                && parent.GetGenericTypeDefinition() == typeof(RegisteredContentHandler<,>))
            .Select(type => type.DeclaringType!);
        Assert.Contains(typeof(ListCustomQuestionsHandler), adapters);
        Assert.True(adapters.SetEquals(registeredOwners));
        foreach (var adapter in adapters)
        {
            var contract = adapter.GetInterfaces().Single(type => type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));
            var registration = Assert.Single(services, descriptor => descriptor.ServiceType == contract);
            Assert.NotNull(registration.ImplementationType);
            Assert.Equal(adapter, registration.ImplementationType.DeclaringType);
        }
    }

    [Fact]
    public void Intentional_question_handler_replacement_survives_registration()
    {
        var replacement = Substitute.For<IRequestHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>>();
        var services = new ServiceCollection();
        services.AddSingleton(replacement);
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();

        Assert.Same(replacement, provider.GetRequiredService<IRequestHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>>());
    }

    [Fact]
    public void Missing_tenant_dependency_does_not_activate_the_released_question_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IRepository<CustomQuestion>>());
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<IRequestHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>>());
        Assert.Contains(nameof(ITenantContext), error.Message);
    }

    [Fact]
    public async Task Registered_question_list_with_no_current_tenant_does_not_query_content()
    {
        var rows = Substitute.For<IRepository<CustomQuestion>>();
        var services = new ServiceCollection();
        services.AddSingleton(rows);
        services.AddSingleton(Substitute.For<ITenantContext>());
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>>();

        var result = await handler.Handle(new ListCustomQuestionsQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Empty(rows.ReceivedCalls());
    }
}
