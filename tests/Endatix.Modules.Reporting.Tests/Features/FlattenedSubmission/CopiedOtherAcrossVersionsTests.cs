using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A checkbox copy that offered its source's Other, then got an Other of its own (#1763). The submission's form
/// version tells which Other the respondent saw; with no version to read, only the source's answer can show it.
/// </summary>
public sealed class CopiedOtherAcrossVersionsTests
{
    private const string SOURCE = """{"type":"checkbox","name":"src","choices":["1","2"],"showOtherItem":true}""";

    private static readonly string _before = Survey(
        SOURCE,
        """{"type":"checkbox","name":"pick","choicesFromQuestion":"src","choicesFromQuestionMode":"selected"}""");

    private static readonly string _after = Survey(
        SOURCE,
        """{"type":"checkbox","name":"pick","choicesFromQuestion":"src","choicesFromQuestionMode":"selected","showOtherItem":true}""");

    [Fact]
    public void Flatten_NoSubmittedDefinition_LeavesTheCopysOwnOtherEmpty()
    {
        // Arrange
        var merged = Recompile(new FormSchemaCompiler().CompilePersisted(_before), _after, FormSchemaCompileMode.Merge);

        // Act
        var cells = FlattenWithoutDefinition(merged, """{"src":["1"],"pick":["1"]}""");

        // Assert
        cells["other"].Should().BeNull("with no form version to read, nothing proves the copy showed an Other");
    }

    [Fact]
    public void Flatten_OwnOtherWithoutSubmittedDefinition_LeavesUnpickedOtherEmpty()
    {
        // Arrange: this copy never offered the source's Other, so its catalog has no otherFrom.
        var compiled = new FormSchemaCompiler().CompilePersisted(_after);

        // Act
        var cells = FlattenWithoutDefinition(compiled, """{"src":["other"],"src-Comment":"typed","pick":["1"]}""");

        // Assert
        cells["other"].Should().BeNull();
    }

    [Fact]
    public void Flatten_NoSubmittedDefinitionAndTheSourcePickedOtherWithText_WritesZero()
    {
        // Arrange
        var merged = Recompile(new FormSchemaCompiler().CompilePersisted(_before), _after, FormSchemaCompileMode.Merge);

        // Act
        var cells = FlattenWithoutDefinition(merged, """{"src":["1","other"],"src-Comment":"typed","pick":["1"]}""");

        // Assert
        cells["other"].Should().Be("0");
    }

    private static Dictionary<string, string?> FlattenWithoutDefinition(FormSchemaCompileResult compiled, string submission) =>
        Cells(FlattenedSubmissionFlattener.Flatten(
            Json(submission),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson, true)));
}
