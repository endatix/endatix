using Endatix.Api.Builders;
using Endatix.Api.Setup;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Setup;

/// <summary>
/// A test server that runs the Endatix API pipeline with one anonymous endpoint, so the API docs can be read without a
/// database. FastEndpoints sets FluentValidation's global name resolvers when it starts, so the host puts them back on
/// dispose, and its tests run in <see cref="ApiDocsHostCollection"/>, apart from the validator tests.
/// </summary>
internal sealed class ApiDocsHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ValidatorGlobals _globals;

    private ApiDocsHost(WebApplication app, ValidatorGlobals globals)
    {
        _app = app;
        _globals = globals;
    }

    /// <summary>Starts the host with <c>UseEndatixApi</c>, around which a test can add its own calls.</summary>
    public static Task<ApiDocsHost> StartAsync(
        Action<ApiOptions>? configure = null,
        Action<WebApplication>? beforeApi = null,
        Action<WebApplication, ApiOptions>? afterApi = null) =>
        StartWithPipelineAsync((app, _) =>
        {
            ApiOptions? used = null;
            beforeApi?.Invoke(app);
            app.UseEndatixApi(options =>
            {
                options.UseExceptionHandler = false;
                options.UseCors = false;
                configure?.Invoke(options);
                used = options;
            });
            afterApi?.Invoke(app, used!);
        });

    /// <summary>Starts the host with a pipeline the test composes, given the builder that registered the API.</summary>
    public static async Task<ApiDocsHost> StartWithPipelineAsync(Action<WebApplication, ApiConfigurationBuilder> pipeline)
    {
        var globals = ValidatorGlobals.Capture();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddFastEndpoints(discovery =>
        {
            discovery.DisableAutoDiscovery = true;
            discovery.Assemblies = [typeof(PingEndpoint).Assembly];
            discovery.Filter = type => type == typeof(PingEndpoint);
        });
        var api = new ApiConfigurationBuilder(builder.Services).AddSwagger();

        var app = builder.Build();
        pipeline(app, api);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new ApiDocsHost(app, globals);
    }

    /// <summary>A client that does not follow redirects.</summary>
    public HttpClient CreateClient() => _app.GetTestClient();

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        _globals.Restore();
    }

    private sealed record ValidatorGlobals(
        Func<Type, System.Reflection.MemberInfo, System.Linq.Expressions.LambdaExpression, string> DisplayNameResolver,
        Func<Type, System.Reflection.MemberInfo, System.Linq.Expressions.LambdaExpression, string> PropertyNameResolver)
    {
        public static ValidatorGlobals Capture() =>
            new(ValidatorOptions.Global.DisplayNameResolver, ValidatorOptions.Global.PropertyNameResolver);

        public void Restore()
        {
            ValidatorOptions.Global.DisplayNameResolver = DisplayNameResolver;
            ValidatorOptions.Global.PropertyNameResolver = PropertyNameResolver;
        }
    }

    internal sealed class PingEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("ping");
            AllowAnonymous();
        }

        public override Task HandleAsync(CancellationToken ct) => Send.NoContentAsync(ct);
    }
}

/// <summary>Runs the API docs host tests alone, since a running host changes FluentValidation's globals.</summary>
[CollectionDefinition(nameof(ApiDocsHostCollection), DisableParallelization = true)]
public sealed class ApiDocsHostCollection;
