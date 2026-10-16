using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Domain;

public sealed class FormSchemaTests
{
    [Fact]
    public void UpdateSearchKeys_NewerRevision_StoresKeysAndRevision()
    {
        // Arrange
        var schema = NewSchema();

        // Act
        schema.UpdateSearchKeys("""[{"key":"q1"}]""", revision: 2);

        // Assert
        (schema.SearchKeys, schema.SearchKeysRevision).Should().Be(("""[{"key":"q1"}]""", 2));
    }

    [Fact]
    public void UpdateSearchKeys_OlderRevision_ThrowsInvalidOperationException()
    {
        // Arrange
        var schema = NewSchema();
        schema.UpdateSearchKeys("[]", revision: 3);

        // Act
        var update = () => schema.UpdateSearchKeys("[]", revision: 2);

        // Assert
        update.Should().Throw<InvalidOperationException>();
    }

    private static FormSchemaEntity NewSchema() =>
        new(1, 100, 1, FormSchemaEntity.EmptyFlatteningMapJson, FormSchemaEntity.EmptyCodebookJson);
}
