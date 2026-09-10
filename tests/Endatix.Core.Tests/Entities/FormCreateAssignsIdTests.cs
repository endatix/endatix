using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class FormCreateAssignsIdTests
{
    [Fact]
    public void Create_WithIdGenerator_AssignsIdBeforeReturn()
    {
        IIdGenerator<long> idGenerator = Substitute.For<IIdGenerator<long>>();
        idGenerator.CreateId().Returns(42L, 43L);

        Form first = Form.Create(idGenerator, new FormCreateArgs(TenantId: 1, Name: "A"));
        Form second = Form.Create(idGenerator, new FormCreateArgs(TenantId: 1, Name: "B"));

        first.Id.Should().Be(42);
        second.Id.Should().Be(43);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_WithExplicitId_AssignsThatId()
    {
        Form form = Form.Create(99L, new FormCreateArgs(TenantId: 1, Name: "A"));
        form.Id.Should().Be(99);
    }

    [Fact]
    public void Create_WithZeroId_Throws()
    {
        Action act = () => Form.Create(0L, new FormCreateArgs(TenantId: 1, Name: "A"));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FormDefinition_Create_WithIdGenerator_AssignsIdBeforeReturn()
    {
        IIdGenerator<long> idGenerator = Substitute.For<IIdGenerator<long>>();
        idGenerator.CreateId().Returns(7L);

        FormDefinition definition = FormDefinition.Create(idGenerator, tenantId: 1, isDraft: true);

        definition.Id.Should().Be(7);
        definition.TenantId.Should().Be(1);
        definition.IsDraft.Should().BeTrue();
    }
}

public class SubmissionCreateAssignsIdTests
{
    [Fact]
    public void Create_WithIdGenerator_AssignsIdBeforeReturn()
    {
        IIdGenerator<long> idGenerator = Substitute.For<IIdGenerator<long>>();
        idGenerator.CreateId().Returns(11L);

        Submission submission = Submission.Create(idGenerator, new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}"));

        submission.Id.Should().Be(11);
    }
}
