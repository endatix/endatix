using Endatix.Api.Endpoints.Forms;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Entities;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class SubmissionMapperTests
{
    [Fact]
    public void Map_Collection_MapsEverySubmission()
    {
        // Arrange
        var submissions = new[]
        {
            Submission.Create(new SubmissionCreateArgs(TenantId: 1, FormId: 2, FormDefinitionId: 3, JsonData: "{}")),
            Submission.Create(new SubmissionCreateArgs(TenantId: 1, FormId: 2, FormDefinitionId: 3, JsonData: "{}")),
        };

        // Act
        var models = SubmissionMapper.Map<SubmissionModel>(submissions).ToList();

        // Assert
        models.Should().HaveCount(2);
        models.Should().OnlyContain(m => m.FormId == "2");
    }

    [Fact]
    public void Map_FormCollection_MapsEveryForm()
    {
        // Arrange
        var forms = new[]
        {
            Form.Create(new FormCreateArgs(TenantId: 1, Name: "One")),
            Form.Create(new FormCreateArgs(TenantId: 1, Name: "Two")),
        };

        // Act
        var models = FormMapper.Map<FormModel>(forms).ToList();

        // Assert
        models.Select(m => m.Name).Should().Equal("One", "Two");
    }

    [Fact]
    public void MapToSubmissionDetails_LoadedTheme_IsTheThemeModel()
    {
        var submission = SubmissionWithForm(themeJson: "{\"cssVariables\":{}}");

        var model = SubmissionMapper.MapToSubmissionDetails(submission);

        model.FormDefinition!.ThemeModel.Should().Be("{\"cssVariables\":{}}");
    }

    [Fact]
    public void MapToSubmissionDetails_NoThemeLoaded_HasNoThemeModel()
    {
        var submission = SubmissionWithForm(themeJson: null);

        var model = SubmissionMapper.MapToSubmissionDetails(submission);

        model.FormDefinition!.ThemeModel.Should().BeNull();
    }

    private static Submission SubmissionWithForm(string? themeJson)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: 1, Name: "Form"));
        if (themeJson is not null)
        {
            form.SetTheme(new Theme(1, "Theme", jsonData: themeJson) { Id = 5 });
        }

        var definition = new FormDefinition(1, jsonData: "{}");
        var submission = Submission.Create(new SubmissionCreateArgs(TenantId: 1, FormId: 2, FormDefinitionId: 3, JsonData: "{}"));
        SetNavigation(submission, nameof(Submission.Form), form);
        SetNavigation(submission, nameof(Submission.FormDefinition), definition);
        return submission;
    }

    private static void SetNavigation(Submission submission, string name, object value) =>
        typeof(Submission).GetProperty(name)!.SetValue(submission, value);
}
