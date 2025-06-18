namespace Endatix.Core.UseCases.Submissions.GetFiles;

public record FileDescriptor(string FileName, string MimeType, Stream Content);

public record GetFilesResult(
    string FormName,
    long SubmissionId,
    IReadOnlyList<FileDescriptor> Files
); 