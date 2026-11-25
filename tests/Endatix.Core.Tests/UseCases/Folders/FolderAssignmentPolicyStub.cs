using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.Tests;

/// <summary>
/// Builds a <see cref="FolderAssignmentPolicy"/> that does not require folder assignment and skips folder existence checks when folder id is unset.
/// </summary>
internal static class FolderAssignmentPolicyStub
{
    public static FolderAssignmentPolicy Relaxed(long tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        return Relaxed(tenantContext);
    }

    public static FolderAssignmentPolicy Relaxed(ITenantContext tenantContext)
    {
        var tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();
        var folderRepository = Substitute.For<IRepository<Folder>>();
        return new FolderAssignmentPolicy(tenantSettingsRepository, folderRepository, tenantContext);
    }
}
