using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.IntegrationTests;

/// <summary>
/// A signed-in request must carry a tenant: a token that names none, or tenant 0, is rejected with 403
/// before it reaches an endpoint. Anonymous requests keep their own handling.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SignedInTenantFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    [Theory]
    [InlineData("0")]
    [InlineData(null)]
    public async Task Signed_in_request_without_a_tenant_gets_403_problem_details(string? tenantId)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var admin = world.Tenants[0].Admin;
        using var client = world.CreateClientFactory();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateAccessToken(admin.Id, tenantId, world.Options.AuthSettings));

        // Act
        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Detail.Should().NotBeNullOrWhiteSpace();
        problem.Type.Should().Be("https://www.rfc-editor.org/rfc/rfc9110.html#name-403-forbidden");
        problem.Title.Should().Be("Forbidden access");
        problem.Instance.Should().Be("/api/auth/me");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task Signed_in_request_with_its_tenant_still_succeeds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var admin = world.Tenants[0].Admin;
        using var client = world.CreateClientFactory();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateAccessToken(admin.Id, admin.TenantId.ToString(), world.Options.AuthSettings));

        // Act
        using var response = await client.GetAsync(new Uri("/api/forms", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Form_access_token_still_reaches_the_public_endpoint()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var tenantId = world.Tenants[0].Id;
        await world.Seed.SeedFormAsync(tenantId, "Form access token form", cancellationToken);
        using var scope = world.Services.CreateScope();
        var formId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Forms
            .IgnoreQueryFilters()
            .Where(form => form.TenantId == tenantId && form.Name == "Form access token form")
            .Select(form => form.Id)
            .SingleAsync(cancellationToken);
        var ownDataListId = await SeedDataListAsync(scope, tenantId, cancellationToken);
        var otherDataListId = await SeedDataListAsync(
            scope,
            await IntegrationTenants.InsertAsync(fixture.Database, cancellationToken),
            cancellationToken);
        var formAccessToken = scope.ServiceProvider.GetRequiredService<IFormAccessTokenService>()
            .CreateToken(formId, tenantId).Value.Token;
        using var client = world.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", formAccessToken);

        // Act
        using var ownResponse = await client.GetAsync(
            new Uri($"/api/forms/{formId}/data-lists/{ownDataListId}/search", UriKind.Relative),
            cancellationToken);
        using var otherResponse = await client.GetAsync(
            new Uri($"/api/forms/{formId}/data-lists/{otherDataListId}/search", UriKind.Relative),
            cancellationToken);

        // Assert
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        otherResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, "the request runs at the token's tenant");
    }

    private static async Task<long> SeedDataListAsync(IServiceScope scope, long tenantId, CancellationToken cancellationToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DataList dataList = new(tenantId, $"Fruits-{Guid.NewGuid():N}");
        dataList.AddItem("Apple", "apple");
        db.DataLists.Add(dataList);
        await db.SaveChangesAsync(cancellationToken);
        return dataList.Id;
    }

    [Fact]
    public async Task Anonymous_request_is_unchanged()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        using var client = world.AnonymousClient();

        // Act
        using var response = await client.GetAsync(new Uri("/api/forms", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string CreateAccessToken(long userId, string? tenantId, IntegrationTestAuthSettings settings)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(settings.SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims = [new(JwtRegisteredClaimNames.Sub, userId.ToString())];
        if (tenantId is not null)
        {
            claims.Add(new Claim(ClaimNames.TenantId, tenantId));
        }

        JwtSecurityToken token = new(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
