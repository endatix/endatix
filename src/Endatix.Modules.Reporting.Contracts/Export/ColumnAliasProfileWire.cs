namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Wire values for <see cref="ColumnAliasProfile"/> in APIs and persisted settings JSON.
/// </summary>
public static class ColumnAliasProfileWire
{
    public const string Native = "native";
    public const string Crunch = "crunch";

    public static string ToWireValue(ColumnAliasProfile profile) =>
        profile switch
        {
            ColumnAliasProfile.Crunch => Crunch,
            _ => Native,
        };

    public static bool TryParse(string? value, out ColumnAliasProfile profile)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            profile = ColumnAliasProfile.Native;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case Native:
                profile = ColumnAliasProfile.Native;
                return true;
            case Crunch:
                profile = ColumnAliasProfile.Crunch;
                return true;
            default:
                profile = default;
                return false;
        }
    }
}
