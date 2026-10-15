using System.Net;
using System.Net.Http.Json;
using Endatix.Core.Abstractions.Submissions;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>The personalization catalog exports read: a form's properties as they are now, deleted ones included.</summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task The_personalization_catalog_lists_current_names_and_order_with_deleted_properties()
    {
        // Arrange
        var world = await WorldAsync();
        var api = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await api.CreateFormAsync();
        var city = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var age = await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        var plan = await api.CreatePropertyAsync(formId, new { name = "Plan", dataType = "text" });
        await api.CreatePropertyAsync(await api.CreateFormAsync(), new { name = "Other", dataType = "text" });
        var propertiesUrl = $"/api/forms/{formId}/audience/properties";
        await ExpectOkAsync(api.Client.PatchAsJsonAsync($"{propertiesUrl}/{city}", new { name = "Town", sortOrder = 7 }, api.CancellationToken));
        await ExpectOkAsync(api.Client.DeleteAsync($"{propertiesUrl}/{age}", api.CancellationToken));
        await ExpectOkAsync(api.Client.DeleteAsync($"{propertiesUrl}/{plan}", api.CancellationToken));
        await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "text" });

        // Act
        await using var scope = world.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IPersonalizationCatalog>();
        var variables = await catalog.ListForFormAsync(world.Tenants[0].Id, formId, api.CancellationToken);

        // Assert
        Assert.Equal(
            [("age", "Age", false), ("city", "Town", false), ("plan", "Plan", true)],
            variables.Select(variable => (variable.VariableName, variable.Name, variable.IsDeleted)).OrderBy(variable => variable.VariableName));
        Assert.Equal(7, variables.Single(variable => variable.VariableName == "city").SortOrder);
    }

    private static async Task ExpectOkAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
