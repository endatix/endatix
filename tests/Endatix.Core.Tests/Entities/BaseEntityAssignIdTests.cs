using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class BaseEntityAssignIdTests
{
    [Fact]
    public void AssignId_UnsetId_AssignsGeneratedValue()
    {
        TestEntity entity = new();
        long nextId = 42;

        entity.AssignId(() => nextId);

        entity.Id.Should().Be(42);
    }

    [Fact]
    public void AssignId_ExistingId_DoesNotOverwrite()
    {
        TestEntity entity = new() { Id = 7 };
        bool factoryCalled = false;

        entity.AssignId(() =>
        {
            factoryCalled = true;
            return 99;
        });

        entity.Id.Should().Be(7);
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public void AssignId_NullFactory_ThrowsArgumentNullException()
    {
        TestEntity entity = new();

        Action act = () => entity.AssignId(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("createId");
    }

    private sealed class TestEntity : BaseEntity;
}
