using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Filters discovered migrations and model snapshots to a provider-specific namespace.
/// Used when a single DbContext type shares PostgreSQL and SQL Server migration folders
/// in one assembly. Prefer provider-split DbContext types (E813) to avoid this filter.
/// </summary>
internal sealed class NamespaceFilteringMigrationsAssembly : IMigrationsAssembly
{
    private readonly IMigrationsAssembly _inner;
    private readonly string _namespacePrefix;

    public NamespaceFilteringMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions dbContextOptions,
        IMigrationsIdGenerator migrationsIdGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
    {
        var namespacePrefix = dbContextOptions.FindExtension<ProviderMigrationsNamespaceExtension>()?.MigrationsNamespace;
        if (string.IsNullOrWhiteSpace(namespacePrefix))
        {
            throw new InvalidOperationException(
                "Provider migrations namespace is not configured. Call ConfigureProviderScopedMigrations first.");
        }

        _namespacePrefix = namespacePrefix;
#pragma warning disable EF1001 // EF has no public IMigrationsAssembly base; this filter wraps the internal assembly for provider-scoped migration folders (see class summary).
        _inner = new MigrationsAssembly(currentContext, dbContextOptions, migrationsIdGenerator, logger);
#pragma warning restore EF1001
    }

    public Assembly Assembly => _inner.Assembly;

    public IReadOnlyDictionary<string, TypeInfo> Migrations =>
        field ??= _inner.Migrations
            .Where(pair => pair.Value.Namespace?.StartsWith(_namespacePrefix, StringComparison.Ordinal) == true)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    public ModelSnapshot? ModelSnapshot =>
        field ??= ResolveModelSnapshot();

    public string? FindMigrationId(string nameOrId)
    {
        if (!_inner.Migrations.TryGetValue(nameOrId, out var migrationType))
        {
            return null;
        }

        if (migrationType.Namespace?.StartsWith(_namespacePrefix, StringComparison.Ordinal) != true)
        {
            return null;
        }

        return _inner.FindMigrationId(nameOrId);
    }

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        if (migrationClass.Namespace?.StartsWith(_namespacePrefix, StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(
                $"Migration type '{migrationClass.Name}' is outside namespace '{_namespacePrefix}'.");
        }

        return _inner.CreateMigration(migrationClass, activeProvider);
    }

    private ModelSnapshot? ResolveModelSnapshot()
    {
        if (_inner.ModelSnapshot?.GetType().Namespace?.StartsWith(_namespacePrefix, StringComparison.Ordinal) == true)
        {
            return _inner.ModelSnapshot;
        }

        var snapshotType = Assembly.GetTypes()
            .FirstOrDefault(type =>
                typeof(ModelSnapshot).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.Namespace?.StartsWith(_namespacePrefix, StringComparison.Ordinal) == true);

        return snapshotType is null ? null : (ModelSnapshot)Activator.CreateInstance(snapshotType)!;
    }
}

internal sealed class ProviderMigrationsNamespaceExtension : IDbContextOptionsExtension
{
    public string MigrationsNamespace { get; private set; } = string.Empty;

    public ProviderMigrationsNamespaceExtension WithNamespace(string migrationsNamespace)
    {
        return new ProviderMigrationsNamespaceExtension
        {
            MigrationsNamespace = migrationsNamespace
        };
    }

    public DbContextOptionsExtensionInfo Info =>
        field ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        private readonly ProviderMigrationsNamespaceExtension _extension;

        public ExtensionInfo(ProviderMigrationsNamespaceExtension extension)
            : base(extension)
        {
            _extension = extension;
        }

        public override bool IsDatabaseProvider => false;

        public override string LogFragment => $"MigrationsNamespace={_extension.MigrationsNamespace}";

        public override int GetServiceProviderHashCode() => _extension.MigrationsNamespace.GetHashCode();

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo otherInfo
            && otherInfo._extension.MigrationsNamespace == _extension.MigrationsNamespace;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["MigrationsNamespace"] = _extension.MigrationsNamespace;
    }
}
