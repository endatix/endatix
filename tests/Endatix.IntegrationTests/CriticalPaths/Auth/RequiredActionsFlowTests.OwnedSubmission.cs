using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task A_creator_deletes_their_own_new_submission_with_a_cold_ownership_cache()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownership = new OwnershipReads();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(ownership));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var creator = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var me = await creator.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.False(me.GetProperty("isAdmin").GetBoolean());
        var target = await SubmissionTarget.CreateAsync(creator, ct);
        ownership.Queries.Clear();

        using var deleted = await creator.DeleteAsync(target.Item, ct);

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.DoesNotContain("JsonData", Assert.Single(ownership.Queries), StringComparison.OrdinalIgnoreCase);
        using var readBack = await creator.GetAsync(target.Item, ct);
        Assert.Equal(HttpStatusCode.NotFound, readBack.StatusCode);
    }

    private sealed class OwnershipReads : DbCommandInterceptor
    {
        internal List<string> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;
            if (sql.Contains("Submissions", StringComparison.Ordinal) && sql.Contains("AppUserId", StringComparison.Ordinal)
                && sql.Contains("ExternalSubjectId", StringComparison.Ordinal))
            {
                Queries.Add(sql);
            }
            return ValueTask.FromResult(result);
        }
    }
}
