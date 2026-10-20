using Endatix.Api.Endpoints.Submissions;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class SubmissionModelTests
{
    [Fact]
    public void Constructor_NothingSet_StringsAreEmpty()
    {
        // Act
        var model = new SubmissionModel();

        // Assert
        model.Id.Should().BeEmpty();
        model.JsonData.Should().BeEmpty();
        model.FormId.Should().BeEmpty();
        model.FormDefinitionId.Should().BeEmpty();
    }

    [Fact]
    public void PartialUpdateSubmissionByTokenRequest_TokenNotSet_IsEmpty()
    {
        // Act
        var request = new PartialUpdateSubmissionByTokenRequest();

        // Assert
        request.SubmissionToken.Should().BeEmpty();
    }
}
