using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Tabular;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class CrunchColumnAliasTransformerTests
{
    [Theory]
    [InlineData("qRadioGroup__email", "ChoiceIndicator", "qRadioGroup", null, "qRadioGroup")]
    [InlineData("qMatrix__speed", "MatrixRow", "qMatrix", null, "qMatrix")]
    [InlineData("qMatrixDropdown__alice__punctuality", "MatrixCell", "qMatrixDropdown", "alice", "qMatrixDropdown__alice")]
    [InlineData("qMatrixDropdown__alice__punctuality", "MatrixCell", null, "alice", "qMatrixDropdown__alice__punctuality")]
    [InlineData("qLoop__adidas__qLoopBoolean__true", "ChoiceIndicator", "qLoopBoolean", "true", "qLoop__adidas__qLoopBoolean")]
    public void ResolveAliasGroupKey_UsesExpectedGroup(
        string canonicalKey,
        string kind,
        string? sourceQuestion,
        string? matrixRowValue,
        string expectedGroupKey)
    {
        ExportColumnAliasInput input = new(
            canonicalKey,
            sourceQuestion,
            null,
            matrixRowValue,
            kind);

        string actualGroupKey = CrunchColumnAliasTransformer.ResolveAliasGroupKey(input);

        actualGroupKey.Should().Be(expectedGroupKey);
    }

    [Fact]
    public void BuildExportKeys_WithRepresentativeColumns_AssignsSequentialCrunchAliases()
    {
        IReadOnlyList<ExportColumnAliasInput> columns =
        [
            new(SubmissionExportRow.SystemColumns.Id, null, null, null, "System"),
            new("qRadioGroup__email", "qRadioGroup", "email", null, "ChoiceIndicator"),
            new("qRadioGroup__phone", "qRadioGroup", "phone", null, "ChoiceIndicator"),
            new("qMatrix__speed", "qMatrix", null, "speed", "MatrixRow"),
            new("qMatrix__quality", "qMatrix", null, "quality", "MatrixRow"),
            new("qText", "qText", null, null, "Simple"),
        ];

        IReadOnlyDictionary<string, string> exportKeys = CrunchColumnAliasTransformer._instance.BuildExportKeys(columns);

        exportKeys.Should().ContainKey(SubmissionExportRow.SystemColumns.Id).WhoseValue.Should().Be(SubmissionExportRow.SystemColumns.Id);
        exportKeys["qRadioGroup__email"].Should().Be("Q1_1");
        exportKeys["qRadioGroup__phone"].Should().Be("Q1_2");
        exportKeys["qMatrix__speed"].Should().Be("Q2_1");
        exportKeys["qMatrix__quality"].Should().Be("Q2_2");
        exportKeys["qText"].Should().Be("Q3");
    }
}
