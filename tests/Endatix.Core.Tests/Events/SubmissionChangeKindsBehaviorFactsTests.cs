using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Events;

/// <summary>
/// Catalog of <see cref="SubmissionChangeKinds"/> behavior facts for unit-level documentation.
/// Fact IDs are stable references for reviews and architecture notes.
/// </summary>
public class SubmissionChangeKindsBehaviorFactsTests
{
    // ── Flag layout (CK-*) ───────────────────────────────────────────────────

    [Fact]
    public void CK_01_None_is_zero()
    {
        ((int)SubmissionChangeKinds.None).Should().Be(0);
    }

    [Fact]
    public void CK_02_flags_are_distinct_powers_of_two()
    {
        SubmissionChangeKinds.Answers.Should().HaveFlag(SubmissionChangeKinds.Answers);
        ((int)SubmissionChangeKinds.Answers).Should().Be(1);
        ((int)SubmissionChangeKinds.Metadata).Should().Be(2);
        ((int)SubmissionChangeKinds.Definition).Should().Be(4);
        ((int)SubmissionChangeKinds.Submitter).Should().Be(8);
    }

    // ── SubmissionData mask (SD-*) ───────────────────────────────────────────

    [Fact]
    public void SD_01_submission_data_mask_is_answers_or_definition()
    {
        SubmissionChangeKindsMasks.SubmissionData.Should()
            .Be(SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition);
    }

    [Fact]
    public void SD_02_submission_data_mask_excludes_metadata_and_submitter()
    {
        SubmissionChangeKindsMasks.SubmissionData.HasFlag(SubmissionChangeKinds.Metadata).Should().BeFalse();
        SubmissionChangeKindsMasks.SubmissionData.HasFlag(SubmissionChangeKinds.Submitter).Should().BeFalse();
    }

    // ── AffectsSubmissionData (ASD-*) ────────────────────────────────────────

    public static TheoryData<string, SubmissionChangeKinds, bool> AffectsSubmissionDataFacts =>
        new()
        {
            { "ASD-01", SubmissionChangeKinds.None, false },
            { "ASD-02", SubmissionChangeKinds.Answers, true },
            { "ASD-03", SubmissionChangeKinds.Definition, true },
            { "ASD-04", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition, true },
            { "ASD-05", SubmissionChangeKinds.Metadata, false },
            { "ASD-06", SubmissionChangeKinds.Submitter, false },
            { "ASD-07", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Metadata, true },
            { "ASD-08", SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Submitter, false },
            { "ASD-09", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Submitter, true },
        };

    [Theory]
    [MemberData(nameof(AffectsSubmissionDataFacts))]
    public void AffectsSubmissionData_fact(string factId, SubmissionChangeKinds changeKind, bool expected)
    {
        changeKind.AffectsSubmissionData().Should().Be(expected, factId);
    }

    // ── ParseWireValue (PW-*) ────────────────────────────────────────────────

    public static TheoryData<string, string?, SubmissionChangeKinds> ParseWireValueFacts =>
        new()
        {
            { "PW-01", null, SubmissionChangeKinds.None },
            { "PW-02", "", SubmissionChangeKinds.None },
            { "PW-03", "   ", SubmissionChangeKinds.None },
            { "PW-04", "answers", SubmissionChangeKinds.Answers },
            { "PW-05", "metadata", SubmissionChangeKinds.Metadata },
            { "PW-06", "definition", SubmissionChangeKinds.Definition },
            { "PW-07", "submitter", SubmissionChangeKinds.Submitter },
            { "PW-08", "answers,definition", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition },
            { "PW-09", "metadata,submitter", SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Submitter },
            { "PW-10", " answers , definition ", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition },
            { "PW-11", "ANSWERS", SubmissionChangeKinds.Answers },
            { "PW-12", "Definition", SubmissionChangeKinds.Definition },
            { "PW-13", "answers,unknown,metadata", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Metadata },
            { "PW-14", "unknown", SubmissionChangeKinds.None },
        };

    [Theory]
    [MemberData(nameof(ParseWireValueFacts))]
    public void ParseWireValue_fact(string factId, string? wireValue, SubmissionChangeKinds expected)
    {
        SubmissionChangeKindsExtensions.ParseWireValue(wireValue).Should().Be(expected, factId);
    }

    // ── ToWireValue (TW-*) ───────────────────────────────────────────────────

    [Fact]
    public void TW_01_none_serializes_to_empty_string()
    {
        SubmissionChangeKinds.None.ToWireValue().Should().BeEmpty();
    }

    public static TheoryData<string, SubmissionChangeKinds, string> ToWireValueFacts =>
        new()
        {
            { "TW-02", SubmissionChangeKinds.Answers, "answers" },
            { "TW-03", SubmissionChangeKinds.Metadata, "metadata" },
            { "TW-04", SubmissionChangeKinds.Definition, "definition" },
            { "TW-05", SubmissionChangeKinds.Submitter, "submitter" },
            { "TW-06", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition, "answers,definition" },
            { "TW-07", SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Submitter, "metadata,submitter" },
            {
                "TW-08",
                SubmissionChangeKinds.Answers | SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Definition | SubmissionChangeKinds.Submitter,
                "answers,metadata,definition,submitter"
            },
        };

    [Theory]
    [MemberData(nameof(ToWireValueFacts))]
    public void ToWireValue_fact(string factId, SubmissionChangeKinds changeKind, string expectedWireValue)
    {
        changeKind.ToWireValue().Should().Be(expectedWireValue, factId);
    }

    // ── Round-trip (RT-*) ────────────────────────────────────────────────────

    public static TheoryData<string, SubmissionChangeKinds> RoundTripFacts =>
        new()
        {
            { "RT-01", SubmissionChangeKinds.None },
            { "RT-02", SubmissionChangeKinds.Answers },
            { "RT-03", SubmissionChangeKinds.Definition },
            { "RT-04", SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition },
            { "RT-05", SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Submitter },
            {
                "RT-06",
                SubmissionChangeKinds.Answers | SubmissionChangeKinds.Metadata | SubmissionChangeKinds.Definition | SubmissionChangeKinds.Submitter
            },
        };

    [Theory]
    [MemberData(nameof(RoundTripFacts))]
    public void RoundTrip_fact(string factId, SubmissionChangeKinds changeKind)
    {
        if (changeKind == SubmissionChangeKinds.None)
        {
            SubmissionChangeKindsExtensions.ParseWireValue(changeKind.ToWireValue()).Should().Be(SubmissionChangeKinds.None, factId);
            return;
        }

        string wireValue = changeKind.ToWireValue();
        SubmissionChangeKindsExtensions.ParseWireValue(wireValue).Should().Be(changeKind, factId);
    }
}
