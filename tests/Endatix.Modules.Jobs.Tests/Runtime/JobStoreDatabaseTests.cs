using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobStoreDatabaseTests
{
    [Theory]
    [InlineData("Host=db;Database=endatix;Username=endatix;Password=s3cret-pg", true)]
    [InlineData("Server=db;Database=endatix;User ID=sa;Password=s3cret-sql;TrustServerCertificate=True", false)]
    public void ToString_AnyProvider_NamesTheDatabaseButNotTheConnectionString(string connectionString, bool isPostgreSql)
    {
        // Arrange — a singleton passed to logs, debugger views and assertion messages.
        var database = new JobStoreDatabase(connectionString, isPostgreSql, true);

        // Act
        var printed = database.ToString();

        // Assert
        printed.Should().Contain("'endatix'");
        printed.Should().NotContain("s3cret").And.NotContainEquivalentOf("Password").And.NotContain(connectionString);
    }
}
