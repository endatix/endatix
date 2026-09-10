using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// The domain factories do not allocate Ids. <c>Create(args)</c> leaves <see cref="BaseEntity.Id"/>
/// at 0 (the EF <c>OnAdd</c> snowflake generator fills it on add); <c>Create(long id, args)</c> is the
/// explicit-Id path for tests, imports, seeding and data migrations.
/// </summary>
public class FormCreateAssignsIdTests
{
    [Fact]
    public void Create_WithArgsOnly_LeavesIdUnset()
    {
        Form form = Form.Create(new FormCreateArgs(TenantId: 1, Name: "A"));

        form.Id.Should().Be(0);
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
    public void Create_WithNegativeId_Throws()
    {
        Action act = () => Form.Create(-1L, new FormCreateArgs(TenantId: 1, Name: "A"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FormDefinition_Create_WithExplicitId_AssignsThatId()
    {
        FormDefinition definition = FormDefinition.Create(7L, tenantId: 1, isDraft: true);

        definition.Id.Should().Be(7);
        definition.TenantId.Should().Be(1);
        definition.IsDraft.Should().BeTrue();
    }

    [Fact]
    public void FormDefinition_Constructor_LeavesIdUnset()
    {
        FormDefinition definition = new(tenantId: 1, isDraft: true);

        definition.Id.Should().Be(0);
    }
}

public class SubmissionCreateAssignsIdTests
{
    private static SubmissionCreateArgs Args() => new(
        TenantId: 1,
        FormId: 2,
        FormDefinitionId: 3,
        JsonData: "{}");

    [Fact]
    public void Create_WithArgsOnly_LeavesIdUnset()
    {
        Submission submission = Submission.Create(Args());

        submission.Id.Should().Be(0);
    }

    [Fact]
    public void Create_WithExplicitId_AssignsThatId()
    {
        Submission submission = Submission.Create(11L, Args());

        submission.Id.Should().Be(11);
    }

    [Fact]
    public void Create_WithZeroId_Throws()
    {
        Action act = () => Submission.Create(0L, Args());

        act.Should().Throw<ArgumentException>();
    }
}
