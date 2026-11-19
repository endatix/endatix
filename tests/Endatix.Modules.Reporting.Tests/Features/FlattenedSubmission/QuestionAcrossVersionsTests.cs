using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A checkbox with its own choices that only some form versions have (#1758). A merge compile keeps its columns for
/// every row; a complete row saved on a version without the question never saw it, so it exports empty, not 0.
/// </summary>
public sealed class QuestionAcrossVersionsTests
{
    private const string PICK = """{"type":"checkbox","name":"pick","showOtherItem":true,"choices":["1","2","3"]}""";
    private const string OTHER_QUESTION = """{"type":"text","name":"x"}""";
    private const string UNANSWERED = """{"x":"a"}""";

    private static readonly string _withoutPick = Survey(OTHER_QUESTION);
    private static readonly string _withPick = Survey(OTHER_QUESTION, PICK);

    private static readonly Dictionary<string, string?> _empty = Cells(("1", null), ("2", null), ("3", null), ("other", null));
    private static readonly Dictionary<string, string?> _zero = Cells(("1", "0"), ("2", "0"), ("3", "0"), ("other", "0"));

    [Fact]
    public void Flatten_RowSavedBeforeTheQuestionWasAdded_IsEmptyInEveryColumn()
    {
        // Arrange
        var merged = Merged(_withoutPick, _withPick);

        // Act
        var cells = FlattenOn(merged, _withoutPick);

        // Assert
        cells.Should().Equal(_empty);
    }

    [Fact]
    public void Flatten_RowSavedOnAVersionWithTheQuestion_StillWritesZero()
    {
        // Arrange
        var merged = Merged(_withoutPick, _withPick);

        // Act
        var cells = FlattenOn(merged, _withPick);

        // Assert
        cells.Should().Equal(_zero);
    }

    [Fact]
    public void Flatten_RowSavedAfterTheQuestionWasRemoved_IsEmptyInEveryColumn()
    {
        // Arrange
        var merged = Merged(_withPick, _withoutPick);

        // Act
        var cells = FlattenOn(merged, _withoutPick);

        // Assert
        cells.Should().Equal(_empty);
    }

    [Fact]
    public void Flatten_RowSavedBeforeTheQuestionWasRemoved_StillWritesZero()
    {
        // Arrange
        var merged = Merged(_withPick, _withoutPick);

        // Act
        var cells = FlattenOn(merged, _withPick);

        // Assert
        cells.Should().Equal(_zero);
    }

    [Fact]
    public void Flatten_QuestionInAPanelOfTheRowsVersion_WritesZero()
    {
        // Arrange
        var inPanel = Survey(OTHER_QUESTION, $$"""{"type":"panel","name":"box","elements":[{{PICK}}]}""");
        var merged = Merged(_withoutPick, inPanel);

        // Act
        var cells = FlattenOn(merged, inPanel);

        // Assert
        cells.Should().Equal(_zero);
    }

    [Fact]
    public void Flatten_RowsVersionUnknown_WritesZeroAsBefore()
    {
        // Arrange
        var merged = Merged(_withoutPick, _withPick);

        // Act
        var cells = FlattenOn(merged, submittedDefinition: null);

        // Assert
        cells.Should().Equal(_zero);
    }

    [Fact]
    public void Flatten_IncompleteRowSavedOnAVersionWithTheQuestion_IsEmptyAsBefore()
    {
        // Arrange
        var merged = Merged(_withoutPick, _withPick);

        // Act
        var cells = FlattenOn(merged, _withPick, complete: false);

        // Assert
        cells.Should().Equal(_empty);
    }

    private static FormSchemaCompileResult Merged(string first, string second) =>
        Recompile(new FormSchemaCompiler().CompilePersisted(first), second, FormSchemaCompileMode.Merge);

    private static Dictionary<string, string?> FlattenOn(
        FormSchemaCompileResult compiled,
        string? submittedDefinition,
        bool complete = true) =>
        Cells(FlattenedSubmissionFlattener.Flatten(
            Json(UNANSWERED),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson, complete, submittedDefinition)));
}
