using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.SubmitterProfile;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed partial class ShojiCodebookGeneratorTests
{
    private const string PanelTitle = "Internal Panel Group";
    private const string LoopTitle = "Loop Question (Based on Brands)";

    [Fact]
    public void Generate_AllQuestions_GroupsTheOrderIntoMetaDataCalculatedValuesAndQuestions()
    {
        // Arrange
        var order = AllQuestionsTable().GetProperty("order");

        // Act
        var folders = ShojiOrder.FolderNames(order);

        // Assert
        folders.Should().Equal(ShojiFolderNames.MetaData, ShojiFolderNames.CalculatedValues, ShojiFolderNames.Questions);
        ShojiOrder.TopLevelAliases(order).Should().BeEmpty();
        ShojiOrder.Aliases(ShojiOrder.Folder(order, ShojiFolderNames.MetaData)).Should().Equal(
            "Id",
            "IsComplete",
            "CollectionStatus",
            "CreatedAt",
            "ModifiedAt",
            "StartedAt",
            "CompletedAt",
            "DurationSeconds",
            "SubmitterId",
            "SubmitterDisplayId");
        ShojiOrder.Aliases(ShojiOrder.Folder(order, ShojiFolderNames.CalculatedValues)).Should().Equal("qCalculatedTotal");
    }

    [Fact]
    public void Generate_AllQuestions_HidesFormIdAndLeavesItOutOfTheOrder()
    {
        // Arrange & Act
        var table = AllQuestionsTable();

        // Assert
        table.GetProperty("hidden").EnumerateArray().Select(alias => alias.GetString()).Should().Equal("FormId");
        ShojiOrder.Aliases(table.GetProperty("order")).Should().NotContain("FormId");
        table.GetProperty("metadata").TryGetProperty("FormId", out _).Should().BeTrue();
    }

    [Fact]
    public void Generate_AllQuestions_PanelsAndLoopsBecomeSubFoldersOfQuestionsWhereTheyFirstAppear()
    {
        // Arrange
        var questions = ShojiOrder.Folder(AllQuestionsTable().GetProperty("order"), ShojiFolderNames.Questions);

        // Act
        var subFolders = ShojiOrder.FolderNames(questions);
        var topLevel = ShojiOrder.TopLevelAliases(questions);

        // Assert
        subFolders.Should().Equal(PanelTitle, LoopTitle);
        ShojiOrder.Aliases(ShojiOrder.Folder(questions, PanelTitle)).Should().Equal("qPanelText");
        topLevel.Should().StartWith(["qRadioGroup", "qRating", "qSlider", "qRangeSlider--min", "qRangeSlider--max", "qDropdown"]);
        topLevel.Should().NotContain(alias => alias.StartsWith("qLoop--", StringComparison.Ordinal));
        var entries = questions.EnumerateArray().ToList();
        var panelIndex = entries.FindIndex(entry => entry.ValueKind == JsonValueKind.Object);
        entries[panelIndex - 1].GetString().Should().Be("qExpression");
    }

    [Fact]
    public void Generate_AllQuestions_EveryAliasOfTheLoopIsInTheLoopSubFolder()
    {
        // Arrange
        var table = AllQuestionsTable();
        var order = table.GetProperty("order");
        var loopAliases = table.GetProperty("metadata").EnumerateObject()
            .Select(variable => variable.Name)
            .Where(alias => alias.StartsWith("qLoop--", StringComparison.Ordinal));

        // Act
        var loop = ShojiOrder.Aliases(ShojiOrder.Folder(order, ShojiFolderNames.Questions, LoopTitle));

        // Assert
        loop.Should().BeEquivalentTo(loopAliases);
        loop.IndexOf("qLoop--adidas--qLoopBoolean").Should().BeLessThan(loop.IndexOf("qLoop--qLoopColor"));
        loop.IndexOf("qLoop--qLoopColor").Should().BeLessThan(loop.IndexOf("qLoop--adidas--qLoopColor--other_text"));
    }

    [Fact]
    public void Generate_AudienceValues_GoInParticipantVariablesBeforeTheQuestions()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[{"type":"text","name":"qCity","title":"City"}]}]}
            """;
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);
        compiled = compiler.IncludePersonalization(
            definitionJson,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            PersonalizationColumns.From(PersonalizationReader.Read("""{"identifier":"CG-1","variables":{"city":"Columbus"}}""")));

        // Act
        var order = GenerateTable(compiled).GetProperty("order");

        // Assert
        ShojiOrder.FolderNames(order).Should().Equal(
            ShojiFolderNames.MetaData,
            ShojiFolderNames.ParticipantVariables,
            ShojiFolderNames.Questions);
        ShojiOrder.Aliases(ShojiOrder.Folder(order, ShojiFolderNames.ParticipantVariables))
            .Should().Equal(PersonalizationColumns.IdentifierKey, "audience_city");
        ShojiOrder.Aliases(ShojiOrder.Folder(order, ShojiFolderNames.Questions)).Should().Equal("qCity");
    }

    [Fact]
    public async Task Generate_SubmitterProfileFields_GoInTheSubmitterFolderAfterTheParticipantVariables()
    {
        // Arrange
        var (pipeline, _) = SubmitterProfileGoldenTests.Flattened();

        // Act
        using var document = JsonDocument.Parse(
            await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken));
        var order = document.RootElement.GetProperty("body").GetProperty("table").GetProperty("order");

        // Assert
        ShojiOrder.FolderNames(order).Should().Equal(
            ShojiFolderNames.MetaData,
            ShojiFolderNames.ParticipantVariables,
            ShojiFolderNames.Submitter,
            ShojiFolderNames.Questions);
        ShojiOrder.Aliases(ShojiOrder.Folder(order, ShojiFolderNames.Submitter)).Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_FormWithoutPanels_HasNoSubFoldersAndLeavesEmptyFoldersOut()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[{"type":"text","name":"qName"},{"type":"comment","name":"qNotes"}]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var order = GenerateTable(compiled).GetProperty("order");

        // Assert
        ShojiOrder.FolderNames(order).Should().Equal(ShojiFolderNames.MetaData, ShojiFolderNames.Questions);
        ShojiOrder.Folder(order, ShojiFolderNames.Questions).EnumerateArray().Select(alias => alias.GetString())
            .Should().Equal("qName", "qNotes");
    }

    [Fact]
    public void Generate_UntitledPanel_SubFolderTakesThePanelName()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[
              {"type":"text","name":"qBefore"},
              {"type":"panel","name":"pContact","elements":[{"type":"text","name":"qPhone"},{"type":"text","name":"qEmail"}]},
              {"type":"text","name":"qAfter"}
            ]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var questions = ShojiOrder.Folder(GenerateTable(compiled).GetProperty("order"), ShojiFolderNames.Questions);

        // Assert
        ShojiOrder.FolderNames(questions).Should().Equal("pContact");
        ShojiOrder.Aliases(ShojiOrder.Folder(questions, "pContact")).Should().Equal("qPhone", "qEmail");
        ShojiOrder.TopLevelAliases(questions).Should().Equal("qBefore", "qAfter");
    }

    [Fact]
    public void Generate_TwoPanelsWithOneTitle_GetTwoSubFoldersInSurveyOrder()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[
              {"type":"panel","name":"pFirst","title":"Details","elements":[{"type":"text","name":"qA"}]},
              {"type":"text","name":"qMiddle"},
              {"type":"panel","name":"pSecond","title":"Details","elements":[{"type":"text","name":"qB"}]}
            ]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var questions = ShojiOrder.Folder(GenerateTable(compiled).GetProperty("order"), ShojiFolderNames.Questions);

        // Assert
        ShojiOrder.FolderNames(questions).Should().Equal("Details", "Details (2)");
        ShojiOrder.Aliases(questions).Should().Equal("qA", "qMiddle", "qB");
    }

    [Fact]
    public void Generate_PanelInAPanel_NestsItsSubFolderWhereItAppears()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[
              {"type":"panel","name":"pOuter","title":"Outer","elements":[
                {"type":"text","name":"qO1"},
                {"type":"panel","name":"pInner","title":"Inner","elements":[{"type":"text","name":"qI1"}]},
                {"type":"text","name":"qO2"}
              ]}
            ]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var outer = ShojiOrder.Folder(GenerateTable(compiled).GetProperty("order"), ShojiFolderNames.Questions, "Outer");

        // Assert
        ShojiOrder.FolderNames(outer).Should().Equal("Inner");
        ShojiOrder.Aliases(outer).Should().Equal("qO1", "qI1", "qO2");
    }

    [Fact]
    public void Generate_CodebookWithoutParentPanels_FoldersByPanelTitle()
    {
        // Arrange: a codebook compiled before panel paths were stored has only the nearest panel's title.
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[
              {"type":"text","name":"qBefore"},
              {"type":"panel","name":"pContact","title":"Contact","elements":[{"type":"text","name":"qPhone"}]}
            ]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);
        var codebook = System.Text.Json.Nodes.JsonNode.Parse(compiled.CodebookJson)!;
        codebook["questions"]!["qPhone"]!.AsObject().Remove("parentPanels");

        // Act
        using var document = JsonDocument.Parse(ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson,
            codebook.ToJsonString(),
            ExportFormatSettings.InterimCrunchKeySeparator));
        var questions = ShojiOrder.Folder(document.RootElement.GetProperty("body").GetProperty("table").GetProperty("order"), ShojiFolderNames.Questions);

        // Assert
        ShojiOrder.FolderNames(questions).Should().Equal("Contact");
        ShojiOrder.Aliases(ShojiOrder.Folder(questions, "Contact")).Should().Equal("qPhone");
    }

    [Fact]
    public void Compile_QuestionInNestedPanels_CodebookListsItsPanelsOuterFirst()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[
              {"type":"panel","name":"pOuter","title":"Outer","elements":[
                {"type":"panel","name":"pInner","elements":[{"type":"text","name":"qI1"}]}
              ]}
            ]}]}
            """;

        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Assert
        using var codebook = JsonDocument.Parse(compiled.CodebookJson);
        var question = codebook.RootElement.GetProperty("questions").GetProperty("qI1");
        question.GetProperty("parentPanelTitle").GetString().Should().Be("pInner");
        question.GetProperty("parentPanels").GetRawText().Should().Be(
            """[{"name":"pOuter","title":"Outer"},{"name":"pInner","title":"pInner"}]""");
    }

    private static JsonElement AllQuestionsTable() =>
        GenerateTable(new FormSchemaCompiler().CompilePersisted(
            FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json")));
}
