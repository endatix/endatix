using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Entities;

/// <summary>Persisted operation kinds; changing a value also changes which rows the snapshot index covers.</summary>
[Experimental("ENDATIX0007")]
public static class FormDraftOperationKinds
{
    /// <summary>Maximum length of the stored code.</summary>
    public const int MAX_LENGTH = 32;
    /// <summary>An editor journal record.</summary>
    public const string JOURNAL = "journal";
    /// <summary>A complete survey document.</summary>
    public const string SNAPSHOT = "snapshot";
}

/// <summary>Persisted reasons for a complete survey document. These values must not change.</summary>
[Experimental("ENDATIX0007")]
public static class DraftSnapshotReasons
{
    /// <summary>Maximum length of the stored code.</summary>
    public const int MAX_LENGTH = 32;
    internal const string SEED = "seed";
    internal const string WHOLE_JSON = "whole_json";
    internal const string DISCARD = "discard";
    internal const string PERIODIC = "periodic";
}

/// <summary>Persisted writer channels assigned by the server. These values must not change.</summary>
[Experimental("ENDATIX0007")]
public static class DraftChannels
{
    /// <summary>Maximum length of the stored code.</summary>
    public const int MAX_LENGTH = 32;
    internal const string API = "api";
    internal const string SYSTEM = "system";
}
