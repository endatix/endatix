namespace Endatix.Core.UseCases.Submissions.ListByFormId;

public record ListByFormIdDto(
    IEnumerable<SubmissionDto> Data,
    long TotalCount,
    int Page,
    int PageSize)
{
    public long TotalPages => PageSize <= 0 ? 0 : (long)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
