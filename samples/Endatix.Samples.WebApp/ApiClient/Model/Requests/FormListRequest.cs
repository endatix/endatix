namespace Endatix.Samples.WebApp.ApiClient.Model.Requests;

/// <summary>
/// Represents a request for listing forms with pagination.
/// </summary>
/// <param name="Page">The page number to retrieve.</param>
/// <param name="PageSize">The number of forms to retrieve per page.</param>
public record FormListRequest(
    int Page = 1,
    int PageSize = 20);