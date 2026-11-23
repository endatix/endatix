using System.Text.RegularExpressions;

namespace Endatix.Modules.Jobs.Tests.Domain;

/// <summary>
/// <c>JobScopeStorage</c> is the one place a job's stored tenant is read as a scope. Code that compares a job's tenant to
/// zero instead would bring back the sentinel the scope replaced, so no code in <c>src/</c> may. The scan reads source
/// text, so a job held in a variable whose name does not mention a job is out of its reach.
/// </summary>
public sealed partial class JobScopeStorageTests
{
    private const string StorageFile = "JobScopeStorage.cs";

    [Fact]
    public void Source_ComparesNoJobTenantToZeroOutsideJobScopeStorage()
    {
        // Arrange
        var srcRoot = Path.Combine(RepositoryRoot(), "src");

        // Act
        var offenders = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path) && Path.GetFileName(path) != StorageFile)
            .SelectMany(path => FindJobTenantZeroComparisons(File.ReadAllText(path))
                .Select(match => $"{Path.GetRelativePath(srcRoot, path)}: {match}"))
            .ToList();

        // Assert
        offenders.Should().BeEmpty("a job's scope is read through JobScope, never by comparing its tenant to zero");
    }

    [Theory]
    [InlineData("if (job.TenantId == 0)")]
    [InlineData("var system = claimedJob.TenantId <= 0;")]
    [InlineData("return backgroundJob.TenantId != 0;")]
    [InlineData("if (0 == job.TenantId)")]
    [InlineData(".Where(job => job.TenantId > 0)")]
    public void FindJobTenantZeroComparisons_JobTenantAgainstZero_IsReported(string code)
    {
        // Arrange
        // Act
        var found = FindJobTenantZeroComparisons(code);

        // Assert
        found.Should().ContainSingle();
    }

    [Theory]
    [InlineData("if (request.TenantId <= 0)")]
    [InlineData("job.TenantId == request.TenantId")]
    [InlineData("job.TenantId == 10")]
    [InlineData("message.TenantId == OutboxAppLevel.NoTenant")]
    public void FindJobTenantZeroComparisons_OtherComparison_IsNotReported(string code)
    {
        // Arrange
        // Act
        var found = FindJobTenantZeroComparisons(code);

        // Assert
        found.Should().BeEmpty();
    }

    private static IEnumerable<string> FindJobTenantZeroComparisons(string source) =>
        JobTenantZeroComparison().Matches(source).Select(match => match.Value);

    [GeneratedRegex(
        @"\b\w*[Jj]ob\w*\.TenantId\s*(==|!=|<=|>=|<|>)\s*0\b|\b0\s*(==|!=|<=|>=|<|>)\s*\w*[Jj]ob\w*\.TenantId\b")]
    private static partial Regex JobTenantZeroComparison();

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
}
