using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class FormTemplateTests
{
    [Fact]
    public void MoveToFolder_WhenAllowed_UpdatesFolderId()
    {
        var template = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1);

        var moved = template.MoveToFolder(101);

        moved.Should().BeTrue();
        template.FolderId.Should().Be(101);
    }

    [Fact]
    public void ClearFolder_WhenAssigned_RemovesFolderId()
    {
        var template = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1, folderId: 5);

        var cleared = template.ClearFolder();

        cleared.Should().BeTrue();
        template.FolderId.Should().BeNull();
    }
}
