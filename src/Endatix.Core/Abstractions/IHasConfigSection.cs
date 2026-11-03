namespace Endatix.Core.Abstractions;

/// <summary>
/// Use this if you want to register AppSettings Config section alongside with your implementation
/// </summary>
/// <typeparam name="TSettings">The POCO class used for settings with the <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/options">IOptions</see> pattern.</typeparam>
public interface IHasConfigSection<TSettings> where TSettings : class
{
}