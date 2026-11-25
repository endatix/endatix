using System.Net;
using System.Text;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Managed_import_checks_target_visibility_before_reading_invalid_items(bool enforced)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (enforced)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var visible = await CreateDataListAsync(owner, ct);
        var foreign = await CreateDataListAsync(other, ct);
        var server = Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());
        var upload = new ObservedImport(server, """{"format":"json"}""");

        Assert.Equal((HttpStatusCode.NotFound, false), await upload.SendAsync(owner, foreign, ct));
        Assert.Equal((HttpStatusCode.NotFound, false), await upload.SendAsync(owner, "/api/data-lists/9223372036854775807", ct));
        Assert.Equal((HttpStatusCode.BadRequest, true), await upload.SendAsync(owner, visible, ct));
    }

    [Theory]
    [InlineData("""{"format":"csv","csv":"value,default\r\na,Apple\r\n"}""", HttpStatusCode.OK)]
    [InlineData("""{"format":"json","items":[{"value":"a","label":"Apple"}]}""", HttpStatusCode.OK)]
    [InlineData("""{"format":"json"}""", HttpStatusCode.BadRequest)]
    public async Task Managed_data_list_import_reads_the_body_only_after_admission(string payload, HttpStatusCode admitted)
    {
        var ct = TestContext.Current.CancellationToken;
        var pending = new MutableProvider("setup", "/setup") { Pending = false };
        var conflict = new MutableProvider("conflict", "/other") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(pending);
            services.AddSingleton<IRequiredActionProvider>(conflict);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var colleague = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var limited = await RestrictedUserAsync(world, ct);
        var item = await CreateDataListAsync(owner, ct);
        var otherTenantItem = await CreateDataListAsync(colleague, ct);
        var server = Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());
        var upload = new ObservedImport(server, payload);
        pending.Pending = true;
        await InvalidateAsync(world, ct);
        Assert.Equal((HttpStatusCode.Forbidden, false), await upload.SendAsync(owner, item, ct));
        conflict.Pending = true;
        await InvalidateAsync(world, ct);
        Assert.Equal((HttpStatusCode.ServiceUnavailable, false), await upload.SendAsync(owner, item, ct));
        Assert.Equal((HttpStatusCode.Forbidden, false), await upload.SendAsync(limited, item, ct));
        Assert.Equal((HttpStatusCode.ServiceUnavailable, false), await upload.SendAsync(owner, otherTenantItem, ct));
        pending.Pending = conflict.Pending = false;
        await InvalidateAsync(world, ct);
        Assert.Equal((HttpStatusCode.NotFound, false), await upload.SendAsync(owner, otherTenantItem, ct));
        Assert.Equal((HttpStatusCode.BadRequest, false), await upload.SendAsync(owner, "/api/data-lists/0", ct));
        Assert.Equal((HttpStatusCode.BadRequest, false), await upload.SendAsync(owner, "/api/data-lists/invalid", ct));
        Assert.Equal((admitted, true), await upload.SendAsync(owner, item, ct));
    }

    private sealed class ObservedImport(TestServer server, string payload)
    {
        public async Task<(HttpStatusCode, bool)> SendAsync(HttpClient actor, string item, CancellationToken ct)
        {
            using var stream = new ObservedBody(Encoding.UTF8.GetBytes(payload));
            var result = await server.SendAsync(context =>
            {
                var request = context.Request;
                request.Method = HttpMethods.Put;
                request.Path = $"{item}/import";
                request.ContentType = "application/json";
                request.ContentLength = stream.Length;
                request.Headers.Authorization = actor.DefaultRequestHeaders.Authorization?.ToString();
                request.Body = stream;
            }, ct);
            return ((HttpStatusCode)result.Response.StatusCode, stream.ReadCalled);
        }
    }

    private sealed class ObservedBody(byte[] content) : MemoryStream(content)
    {
        public bool ReadCalled { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalled = true;
            return base.ReadAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalled = true;
            return base.Read(buffer, offset, count);
        }
    }
}
