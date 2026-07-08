using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Events;

/// <summary>
/// Catalog of <see cref="SubmissionChangeKind"/> behavior facts for unit-level documentation.
/// Fact IDs are stable references for reviews and architecture notes.
/// </summary>
public class SubmissionChangeKindBehaviorFactsTests
{
    // ── Flag layout (CK-*) ───────────────────────────────────────────────────

    [Fact]
    public void CK_01_None_is_zero()
    {
        ((int)SubmissionChangeKind.None).Should().Be(0);
    }

    [Fact]
    public void CK_02_flags_are_distinct_powers_of_two()
    {
        SubmissionChangeKind.Answers.Should().HaveFlag(SubmissionChangeKind.Answers);
        ((int)SubmissionChangeKind.Answers).Should().Be(1);
        ((int)SubmissionChangeKind.Metadata).Should().Be(2);
        ((int)SubmissionChangeKind.Definition).Should().Be(4);
        ((int)SubmissionChangeKind.Submitter).Should().Be(8);
    }

    // ── SubmissionData mask (SD-*) ───────────────────────────────────────────

    [Fact]
    public void SD_01_submission_data_mask_is_answers_or_definition()
    {
        SubmissionChangeKindMasks.SubmissionData.Should()
            .Be(SubmissionChangeKind.Answers | SubmissionChangeKind.Definition);
    }

    [Fact]
    public void SD_02_submission_data_mask_excludes_metadata_and_submitter()
    {
        SubmissionChangeKindMasks.SubmissionData.HasFlag(SubmissionChangeKind.Metadata).Should().BeFalse();
        SubmissionChangeKindMasks.SubmissionData.HasFlag(SubmissionChangeKind.Submitter).Should().BeFalse();
    }

    // ── AffectsSubmissionData (ASD-*) ────────────────────────────────────────

    public static TheoryData<string, SubmissionChangeKind, bool> AffectsSubmissionDataFacts =>
        new()
        {
            { "ASD-01", SubmissionChangeKind.None, false },
            { "ASD-02", SubmissionChangeKind.Answers, true },
            { "ASD-03", SubmissionChangeKind.Definition, true },
            { "ASD-04", SubmissionChangeKind.Answers | SubmissionChangeKind.Definition, true },
            { "ASD-05", SubmissionChangeKind.Metadata, false },
            { "ASD-06", SubmissionChangeKind.Submitter, false },
            { "ASD-07", SubmissionChangeKind.Answers | SubmissionChangeKind.Metadata, true },
            { "ASD-08", SubmissionChangeKind.Metadata | SubmissionChangeKind.Submitter, false },
            { "ASD-09", SubmissionChangeKind.Answers | SubmissionChangeKind.Metadata | SubmissionChangeKind.Submitter, true },
        };

    [Theory]
    [MemberData(nameof(AffectsSubmissionDataFacts))]
    public void AffectsSubmissionData_fact(string factId, SubmissionChangeKind changeKind, bool expected)
    {
        changeKind.AffectsSubmissionData().Should().Be(expected, factId);
    }

    // ── ParseWireValue (PW-*) ────────────────────────────────────────────────

    public static TheoryData<string, string?, SubmissionChangeKind> ParseWireValueFacts =>
        new()
        {
            { "PW-01", null, SubmissionChangeKind.None },
            { "PW-02", "", SubmissionChangeKind.None },
            { "PW-03", "   ", SubmissionChangeKind.None },
            { "PW-04", "answers", SubmissionChangeKind.Answers },
            { "PW-05", "metadata", SubmissionChangeKind.Metadata },
            { "PW-06", "definition", SubmissionChangeKind.Definition },
            { "PW-07", "submitter", SubmissionChangeKind.Submitter },
            { "PW-08", "answers,definition", SubmissionChangeKind.Answers | SubmissionChangeKind.Definition },
            { "PW-09", "metadata,submitter", SubmissionChangeKind.Metadata | SubmissionChangeKind.Submitter },
            { "PW-10", " answers , definition ", SubmissionChangeKind.Answers | SubmissionChangeKind.Definition },
            { "PW-11", "ANSWERS", SubmissionChangeKind.Answers },
            { "PW-12", "Definition", SubmissionChangeKind.Definition },
            { "PW-13", "answers,unknown,metadata", SubmissionChangeKind.Answers | SubmissionChangeKind.Metadata },
            { "PW-14", "unknown", SubmissionChangeKind.None },
        };

    [Theory]
    [MemberData(nameof(ParseWireValueFacts))]
    public void ParseWireValue_fact(string factId, string? wireValue, SubmissionChangeKind expected)
    {
        SubmissionChangeKindExtensions.ParseWireValue(wireValue).Should().Be(expected, factId);
    }

    // ── ToWireValue (TW-*) ───────────────────────────────────────────────────

    [Fact]
    public void TW_01_none_serializes_to_empty_string()
    {
        SubmissionChangeKind.None.ToWireValue().Should().BeEmpty();
    }

    public static TheoryData<string, SubmissionChangeKind, string> ToWireValueFacts =>
        new()
        {
            { "TW-02", SubmissionChangeKind.Answers, "answers" },
            { "TW-03", SubmissionChangeKind.Metadata, "metadata" },
            { "TW-04", SubmissionChangeKind.Definition, "definition" },
            { "TW-05", SubmissionChangeKind.Submitter, "submitter" },
            { "TW-06", SubmissionChangeKind.Answers | SubmissionChangeKind.Definition, "answers,definition" },
            { "TW-07", SubmissionChangeKind.Metadata | SubmissionChangeKind.Submitter, "metadata,submitter" },
            {
                "TW-08",
                SubmissionChangeKind.Answers | SubmissionChangeKind.Metadata | SubmissionChangeKind.Definition | SubmissionChangeKind.Submitter,
                "answers,metadata,definition,submitter"
            },
        };

    [Theory]
    [MemberData(nameof(ToWireValueFacts))]
    public void ToWireValue_fact(string factId, SubmissionChangeKind changeKind, string expectedWireValue)
    {
        changeKind.ToWireValue().Should().Be(expectedWireValue, factId);
    }

    // ── Round-trip (RT-*) ────────────────────────────────────────────────────

    public static TheoryData<string, SubmissionChangeKind> RoundTripFacts =>
        new()
        {
            { "RT-01", SubmissionChangeKind.None },
            { "RT-02", SubmissionChangeKind.Answers },
            { "RT-03", SubmissionChangeKind.Definition },
            { "RT-04", SubmissionChangeKind.Answers | SubmissionChangeKind.Definition },
            { "RT-05", SubmissionChangeKind.Metadata | SubmissionChangeKind.Submitter },
            {
                "RT-06",
                SubmissionChangeKind.Answers | SubmissionChangeKind.Metadata | SubmissionChangeKind.Definition | SubmissionChangeKind.Submitter
            },
        };

    [Theory]
    [MemberData(nameof(RoundTripFacts))]
    public void RoundTrip_fact(string factId, SubmissionChangeKind changeKind)
    {
        if (changeKind == SubmissionChangeKind.None)
        {
            SubmissionChangeKindExtensions.ParseWireValue(changeKind.ToWireValue()).Should().Be(SubmissionChangeKind.None, factId);
            return;
        }

        string wireValue = changeKind.ToWireValue();
        SubmissionChangeKindExtensions.ParseWireValue(wireValue).Should().Be(changeKind, factId);
    }
}
