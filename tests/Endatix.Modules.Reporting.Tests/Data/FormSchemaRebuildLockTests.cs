using Endatix.Modules.Reporting.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Tests.Data;

public class FormSchemaRebuildLockTests
{
    private static readonly FormSchemaLockTarget Target = new(TenantId: 1, FormId: 100);

    [Fact]
    public void KeyFor_SameTenantAndForm_ReturnsSameKey()
    {
        // Arrange
        FormSchemaLockTarget sameForm = new(TenantId: 1, FormId: 100);

        // Act
        var key = FormSchemaRebuildLock.KeyFor(sameForm);

        // Assert
        key.Should().Be(FormSchemaRebuildLock.KeyFor(Target));
    }

    [Fact]
    public void KeyFor_OtherFormOrTenant_ReturnsOtherKeys()
    {
        // Arrange
        FormSchemaLockTarget otherForm = new(TenantId: 1, FormId: 101);
        FormSchemaLockTarget otherTenant = new(TenantId: 2, FormId: 100);
        FormSchemaLockTarget swappedIds = new(TenantId: 100, FormId: 1);

        // Act
        long[] keys = [.. new[] { Target, otherForm, otherTenant, swappedIds }.Select(FormSchemaRebuildLock.KeyFor)];

        // Assert
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task AcquireAsync_OnSqlServer_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        await using DbContext context = new(options);

        // Act
        var act = () => FormSchemaRebuildLock.AcquireAsync(context.Database, Target, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
