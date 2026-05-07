using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Folders.List;

/// <summary>
/// Query for listing folders.
/// </summary>
/// <param name="IncludeInactive">Whether to include inactive folders.</param>
public sealed record ListFoldersQuery(bool IncludeInactive = false) : IQuery<Result<IEnumerable<FolderDto>>>;
