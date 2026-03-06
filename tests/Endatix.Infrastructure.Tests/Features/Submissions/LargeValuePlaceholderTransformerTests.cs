using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Transformers;
using Xunit;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class LargeValuePlaceholderTransformerTests
{
    private static SubmissionExportRow Row() => new() { FormId = 1, Id = 100 };

    private static TransformationContext<SubmissionExportRow> Ctx(SubmissionExportRow? row = null) =>
        new(row ?? Row(), null, null);

    [Fact]
    public void Transform_ReturnsFilePlaceholder_WhenDataUriString()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var node = JsonValue.Create("data:image/jpeg;base64,/9j/4AAQSkZJRg");
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        Assert.True(result is JsonValue v && v.GetValue<string>() == "[file]");
    }

    [Fact]
    public void Transform_ReturnsFilePlaceholder_WhenDataUriWithMimeType()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var node = JsonValue.Create("data:image/png;base64,iVBORw0KGgo=");
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        Assert.Equal("[file]", result.GetValue<string>());
    }

    [Fact]
    public void Transform_ReturnsUnchanged_WhenShortString()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var node = JsonValue.Create("hello");
        var result = sut.Transform(node, Ctx());
        Assert.Same(node, result);
        Assert.Equal("hello", result!.GetValue<string>());
    }

    [Fact]
    public void Transform_ReturnsTruncatedPlaceholder_WhenStringExceedsDefaultMax()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var longString = new string('x', LargeValuePlaceholderTransformer.DefaultMaxValueLength + 1);
        var node = JsonValue.Create(longString);
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        Assert.Equal("[truncated]", result.GetValue<string>());
    }

    [Fact]
    public void Transform_ReturnsUnchanged_WhenStringEqualsMaxLength()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var exactMax = new string('a', LargeValuePlaceholderTransformer.DefaultMaxValueLength);
        var node = JsonValue.Create(exactMax);
        var result = sut.Transform(node, Ctx());
        Assert.Same(node, result);
        Assert.Equal(exactMax, result!.GetValue<string>());
    }

    [Fact]
    public void Transform_ReturnsNullUnchanged_WhenNodeIsNull()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var result = sut.Transform<SubmissionExportRow>(null, Ctx());
        Assert.Null(result);
    }

    [Fact]
    public void Transform_ReturnsUnchanged_WhenRowIsNotSubmissionExportRow()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var node = JsonValue.Create("data:image/jpeg;base64,abc");
        var context = new TransformationContext<DynamicExportRow>(new DynamicExportRow(), null, null);
        var result = sut.Transform(node, context);
        Assert.Same(node, result);
        Assert.Equal("data:image/jpeg;base64,abc", result!.GetValue<string>());
    }

    [Fact]
    public void Transform_ReplacesDataUriInObject()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var node = JsonNode.Parse("""{"content":"data:image/jpeg;base64,/9j/4AAQ","name":"photo"}""")!;
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.Equal("[file]", obj["content"]!.GetValue<string>());
        Assert.Equal("photo", obj["name"]!.GetValue<string>());
    }

    [Fact]
    public void Transform_ReplacesLongStringInArray()
    {
        var sut = new LargeValuePlaceholderTransformer(5);
        var node = JsonNode.Parse("""["waytoolong"]""")!;
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        var arr = result.AsArray();
        Assert.Equal("[truncated]", arr[0]!.GetValue<string>());
    }

    [Fact]
    public void Transform_ReplacesDataUriInNestedObject()
    {
        var sut = new LargeValuePlaceholderTransformer();
        var node = JsonNode.Parse("""{"inner":{"content":"data:text/plain;base64,SGVsbG8="}}""")!;
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        var inner = result["inner"]!.AsObject();
        Assert.Equal("[file]", inner["content"]!.GetValue<string>());
    }

    [Fact]
    public void Constructor_WithCustomMaxValueLength_RespectsLimit()
    {
        var sut = new LargeValuePlaceholderTransformer(100);
        var node = JsonValue.Create(new string('y', 101));
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        Assert.Equal("[truncated]", result.GetValue<string>());
    }

    [Fact]
    public void Constructor_WithZeroMaxValueLength_UsesDefault()
    {
        var sut = new LargeValuePlaceholderTransformer(0);
        var node = JsonValue.Create(new string('z', LargeValuePlaceholderTransformer.DefaultMaxValueLength + 1));
        var result = sut.Transform(node, Ctx());
        Assert.NotNull(result);
        Assert.Equal("[truncated]", result.GetValue<string>());
    }
}
