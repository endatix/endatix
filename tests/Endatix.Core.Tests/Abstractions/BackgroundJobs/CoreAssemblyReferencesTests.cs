using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

/// <summary>
/// Handlers live in Core-referencing assemblies and must never see the scheduler that runs them, so the
/// scheduler has to stay out of Core's dependency graph altogether.
/// </summary>
public sealed class CoreAssemblyReferencesTests
{
    [Fact]
    public void CoreAssembly_References_ExcludeQuartz()
    {
        // Arrange
        var coreAssembly = typeof(IBackgroundJobHandler).Assembly;

        // Act
        var referenced = coreAssembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);

        // Assert
        referenced.Should().NotContain(name => name.StartsWith("Quartz", StringComparison.OrdinalIgnoreCase));
    }
}
