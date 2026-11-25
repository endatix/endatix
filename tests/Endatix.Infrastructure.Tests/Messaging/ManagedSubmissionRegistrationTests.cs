using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.DeleteTests;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.UseCases.Submissions.SetTest;
using Endatix.Infrastructure.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Messaging;

public class ManagedSubmissionRegistrationTests
{
    [Fact]
    public async Task Test_commands_reject_a_different_tenant_before_accessing_the_form()
    {
        var forms = Substitute.For<IRepository<Form>>();
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(2);
        var services = new ServiceCollection();
        services.AddSingleton(forms);
        services.AddSingleton(tenant);
        services.AddSingleton(Substitute.For<IRepository<Submission>>());
        services.AddSingleton(Substitute.For<IUnitOfWork>());
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ct = TestContext.Current.CancellationToken;
        var set = scope.ServiceProvider.GetRequiredService<IRequestHandler<SetSubmissionTestCommand, Result<SubmissionDto>>>();
        var delete = scope.ServiceProvider.GetRequiredService<IRequestHandler<DeleteTestSubmissionsCommand, Result<DeletedTestSubmissions>>>();

        Assert.Equal(ResultStatus.Unauthorized, (await set.Handle(new(1, 3, 4, true), ct)).Status);
        Assert.Equal(ResultStatus.Unauthorized, (await delete.Handle(new(1, 3), ct)).Status);
        Assert.Empty(forms.ReceivedCalls());
    }

    [Fact]
    public void Registered_patch_and_put_each_hold_one_submission_repository()
    {
        var count = 0;
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ITenantContext>());
        services.AddSingleton(Substitute.For<IRepository<SubmissionVersion>>());
        services.AddTransient(_ =>
        {
            count++;
            return Substitute.For<IRepository<Submission>>();
        });
        services.AddMediatRMessaging();
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        first.ServiceProvider.GetRequiredService<IRequestHandler<PartialUpdateSubmissionCommand, Result<Submission>>>();
        Assert.Equal(1, count);
        second.ServiceProvider.GetRequiredService<IRequestHandler<UpdateSubmissionCommand, Result<Submission>>>();
        Assert.Equal(2, count);
    }
}
