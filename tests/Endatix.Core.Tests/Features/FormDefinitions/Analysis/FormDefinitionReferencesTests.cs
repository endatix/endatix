using Endatix.Core.Features.FormDefinitions.Analysis;

namespace Endatix.Core.Tests.Features.FormDefinitions.Analysis;

public class FormDefinitionReferencesTests
{
    [Fact]
    public void Analyze_reads_string_and_numeric_data_list_ids_at_any_depth()
    {
        const string json = """
            {"pages":[{"name":"p1","elements":[
              {"type":"dropdown","name":"a","edxDataListId":"11"},
              {"type":"panel","name":"outer","elements":[{"type":"panel","name":"inner","elements":[
                {"type":"tagbox","name":"b","edxDataListId":12}]}]},
              {"type":"paneldynamic","name":"dp","templateElements":[{"type":"dropdown","name":"c","edxDataListId":" 13 "}]},
              {"type":"matrixdropdown","name":"m","columns":[{"name":"col","cellType":"dropdown","edxDataListId":14}]},
              {"type":"matrixdynamic","name":"md","columns":[{"name":"col","cellType":"dropdown","edxDataListId":"15"}]}
            ]}]}
            """;

        var references = FormDefinitionReferences.Analyze(json);

        references.DataListIds.Should().BeEquivalentTo([11L, 12L, 13L, 14L, 15L]);
    }

    [Fact]
    public void Analyze_reads_ids_after_multibyte_text()
    {
        const string json = """{"title":"Анкета 調査 🙂","elements":[{"title":"Größe","edxDataListId":"11"}]}""";

        FormDefinitionReferences.Analyze(json).DataListIds.Should().Equal(11L);
    }

    [Fact]
    public void Analyze_reports_each_data_list_once()
    {
        const string json = """{"elements":[{"edxDataListId":"11"},{"edxDataListId":11},{"edxDataListId":"12"}]}""";

        FormDefinitionReferences.Analyze(json).DataListIds.Should().BeEquivalentTo([11L, 12L]);
    }

    [Fact]
    public void Analyze_matches_an_escaped_property_name()
    {
        const string json = """{"elements":[{"edxDataListId":"11"}]}""";

        FormDefinitionReferences.Analyze(json).DataListIds.Should().Equal(11L);
    }

    [Theory]
    [InlineData("""{"elements":[{"name":"a","edxDataListIdX":"11","dataListId":"12","EDXDATALISTID":"13"}]}""")]
    [InlineData("""{"elements":[{"name":"a","edxDataListId":""}]}""")]
    [InlineData("""{"elements":[{"name":"a","edxDataListId":"abc"}]}""")]
    [InlineData("""{"elements":[{"name":"a","edxDataListId":-5}]}""")]
    [InlineData("""{"elements":[{"name":"a","edxDataListId":1.5}]}""")]
    [InlineData("""{"elements":[{"name":"a","edxDataListId":{"id":"11"}}]}""")]
    [InlineData("""{"elements":[{"name":"a","edxDataListId":["11"]}]}""")]
    [InlineData("""{"elements":[{"name":"a","title":"edxDataListId 11"}]}""")]
    [InlineData("""{"elements":["edxDataListId","11"]}""")]
    public void Analyze_ignores_anything_that_is_not_a_data_list_binding(string json)
    {
        FormDefinitionReferences.Analyze(json).DataListIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"pages\":[")]
    [InlineData("not json")]
    [InlineData("""{"elements":[{"edxDataListId":"11"}]} trailing""")]
    [InlineData("""{"elements":[{"edxDataListId":"11"},""")]
    public void Analyze_finds_nothing_in_a_missing_or_malformed_definition(string? json)
    {
        FormDefinitionReferences.Analyze(json).DataListIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"elements":[{"edxDataListId":"\uD800"}]}""")]
    [InlineData("""{"elements":[{"edxDataListId":"11"},{"edxDataListId":"\uDC00"}]}""")]
    public void Analyze_finds_nothing_in_a_definition_with_an_invalid_escaped_id(string json)
    {
        FormDefinitionReferences.Analyze(json).DataListIds.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_finds_nothing_in_a_definition_nested_past_the_depth_limit()
    {
        var json = string.Concat(Enumerable.Repeat("{\"a\":", 300)) + "{\"edxDataListId\":\"11\"}" + new string('}', 300);

        FormDefinitionReferences.Analyze(json).DataListIds.Should().BeEmpty();
    }
}
