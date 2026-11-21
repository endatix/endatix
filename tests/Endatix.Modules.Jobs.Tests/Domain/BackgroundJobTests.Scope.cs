using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;

namespace Endatix.Modules.Jobs.Tests.Domain;

/// <summary>A job's scope: a tenant job keeps its tenant, and a system job is made only by name.</summary>
public partial class BackgroundJobTests
{
    [Fact]
    public void ForSystem_CreatesAPendingSystemJob()
    {
        // Arrange
        // Act
        var job = BackgroundJob.ForSystem("SignupWork", "{}", Now);

        // Assert
        job.Scope.Should().Be(JobScope.System);
        job.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public void FromRequest_SystemScope_CreatesASystemJob()
    {
        // Arrange
        var request = new BackgroundJobRequest("SignupWork", "{}", JobScope.System, DedupKey: "9:SignupWork");

        // Act
        var job = BackgroundJob.FromRequest(request, Now, traceId: null);

        // Assert
        job.Scope.Should().Be(JobScope.System);
        job.DedupKey.Should().Be("9:SignupWork");
    }

    [Fact]
    public void FromRequest_TenantScope_KeepsTheTenant()
    {
        // Arrange
        var request = new BackgroundJobRequest("SubmissionExport", "{}", JobScope.ForTenant(7));

        // Act
        var job = BackgroundJob.FromRequest(request, Now, traceId: null);

        // Assert
        job.Scope.Should().Be(JobScope.ForTenant(7));
        job.TenantId.Should().Be(7);
    }
}
