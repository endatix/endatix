namespace Endatix.Api.Endpoints.Submissions;

public record ListByFormIdResponse(
    IEnumerable<SubmissionModel> Data,
    long TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage);
