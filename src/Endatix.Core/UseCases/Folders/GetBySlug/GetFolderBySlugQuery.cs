using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Folders.GetBySlug;

/// <summary>
/// Query for getting a folder by slug
/// </summary>
/// <param name="Slug">The slug of the folder to get.</param>
public sealed record GetFolderBySlugQuery(string Slug) : IQuery<Result<FolderDto>>;
