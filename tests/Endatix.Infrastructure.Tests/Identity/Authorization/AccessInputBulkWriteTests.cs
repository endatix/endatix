using System.Text.RegularExpressions;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

/// <summary>
/// A bulk <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> or raw SQL bypasses the change tracker, so it changes an access
/// input without changing the access version. No code in <c>src/</c> may run one over an access input. The scan reads
/// statements, so a bulk write over a query held in a variable is out of its reach.
/// </summary>
public sealed partial class AccessInputBulkWriteTests
{
    [Fact]
    public void Model_DeclaresTheAccessInputs()
    {
        // Arrange
        using var db = CreateContext();

        // Act
        var inputs = AccessInputs(db).Select(entityType => entityType.ClrType);

        // Assert
        inputs.Should().BeEquivalentTo(
            [typeof(AppRole), typeof(AppUser), typeof(Permission), typeof(RolePermission), typeof(IdentityUserRole<long>)]);
    }

    [Fact]
    public void Source_RunsNoBulkWriteOverAnAccessInput()
    {
        // Arrange
        var patterns = AccessInputPatterns();
        var srcRoot = Path.Combine(RepositoryRoot(), "src");

        // Act
        var offenders = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .SelectMany(path => FindBulkWrites(File.ReadAllText(path), patterns)
                .Select(statement => $"{Path.GetRelativePath(srcRoot, path)}: {statement}"))
            .ToList();

        // Assert
        offenders.Should().BeEmpty(
            "a bulk write over an access input skips the access version; load the rows and save them through the context instead");
    }

    [Theory]
    [InlineData("await db.Roles.Where(r => r.Id == id).ExecuteDeleteAsync(ct);")]
    [InlineData("identityDbContext.UserRoles.Where(x => x.UserId == 1).ExecuteDelete();")]
    [InlineData("await db.RolePermissions.Where(x => x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);")]
    [InlineData("db.Set<AppUser>().Where(u => u.TenantId == 1).ExecuteUpdate(s => s.SetProperty(u => u.TenantId, 0));")]
    [InlineData("await db.Database.ExecuteSqlRawAsync(\"DELETE FROM identity.\\\"UserRoles\\\" WHERE 1 = 1\", ct);")]
    public void FindBulkWrites_BulkWriteOverAnAccessInput_IsReported(string code)
    {
        // Arrange
        var patterns = AccessInputPatterns();

        // Act
        var found = FindBulkWrites(code, patterns);

        // Assert
        found.Should().ContainSingle();
    }

    [Theory]
    [InlineData("await db.Forms.Where(f => f.Id == id).ExecuteDeleteAsync(ct);")]
    [InlineData("var roles = await db.Roles.Where(r => r.IsActive).ToListAsync(ct);")]
    public void FindBulkWrites_OtherStatement_IsNotReported(string code)
    {
        // Arrange
        var patterns = AccessInputPatterns();

        // Act
        var found = FindBulkWrites(code, patterns);

        // Assert
        found.Should().BeEmpty();
    }

    /// <summary>The statements in <paramref name="code"/> that run a bulk write naming an access input set, entity or table.</summary>
    private static IEnumerable<string> FindBulkWrites(string code, IReadOnlyList<Regex> accessInputs) =>
        Statements(code)
            .Where(statement => BulkWrite().IsMatch(statement) && accessInputs.Any(pattern => pattern.IsMatch(statement)))
            .Select(statement => Whitespace().Replace(statement, " ").Trim());

    /// <summary>Splits code at statement and block boundaries, so a fluent chain stays one statement.</summary>
    private static IEnumerable<string> Statements(string code) => code.Split([';', '{', '}']);

    /// <summary>Matches a reference to an access input: its <c>DbSet</c>, <c>Set&lt;T&gt;()</c>, or its table name in SQL.</summary>
    private static List<Regex> AccessInputPatterns()
    {
        using var db = CreateContext();
        var contextProperties = typeof(AppIdentityDbContext).GetProperties();
        List<Regex> patterns = [];
        foreach (var entityType in AccessInputs(db))
        {
            var setNames = contextProperties
                .Where(property => property.PropertyType.IsGenericType
                    && property.PropertyType.GetGenericArguments()[0] == entityType.ClrType)
                .Select(property => property.Name);
            foreach (var setName in setNames)
            {
                patterns.Add(new Regex($@"\.{setName}\b"));
            }

            patterns.Add(new Regex($@"Set<{Regex.Escape(ShortName(entityType.ClrType))}>"));
            patterns.Add(new Regex($@"\b{Regex.Escape(entityType.GetTableName()!)}\b"));
        }

        return patterns;
    }

    private static string ShortName(Type type) =>
        type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(ShortName))}>" : type.Name;

    private static IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IEntityType> AccessInputs(AppIdentityDbContext db) =>
        db.Model.GetEntityTypes().Where(entityType => entityType.IsAccessInput());

    private static AppIdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppIdentityDbContext>().UseNpgsql("Host=unused").Options);

    private static bool IsGenerated(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Endatix.slnx not found above the test output.");
    }

    [GeneratedRegex(@"\.Execute(Update|Delete|Sql)\w*\b")]
    private static partial Regex BulkWrite();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
