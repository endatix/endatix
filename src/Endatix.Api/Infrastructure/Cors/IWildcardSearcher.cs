namespace Endatix.Api.Infrastructure.Cors;

/// <summary>
/// Implementation of this interface should search for wildcards given an input on string params
/// </summary>
public interface IWildcardSearcher
{
    /// <summary>
    /// Searches for wildcard symbols in input of string params
    /// </summary>
    /// <param name="inputParams">Array of strings to search into</param>
    /// <returns><see cref="CorsWildcardResult"/> result</returns>
    CorsWildcardResult SearchForWildcard(IList<string> inputParams);
}
